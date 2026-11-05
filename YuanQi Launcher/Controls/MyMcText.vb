'我的世界位图字体文本
'
'游戏自带的 ascii.png 是 16×16 格、每格 8×8 的字形图集，按 code & 15 / code >> 4 取格子就是游戏的真字体。
'三个容易踩的点（见 docs/沉浸模式-设计方案.md §3.2）：
'  1. 字宽不能用统一比例，要用原版的推进宽度 —— 否则字形落在半像素边界上，被抗锯齿糊掉；
'  2. 字格必须是 8 的整数倍 —— 否则一个字形像素会被拉伸到非整数像素上；
'  3. 推进宽度从图集里量出来（最右侧有笔画的那一列 + 1），比硬编码表更贴近游戏、也兼容资源包。
'
'取不到游戏字体时退回启动器自带的 Resources/#PCL English，风格弱一些但不会缺字。

Imports System.Globalization

Public Class MyMcText
    Inherits FrameworkElement

    Public Sub New()
        '位图字必须最近邻放大，否则字形像素会被插值糊掉
        RenderOptions.SetBitmapScalingMode(Me, BitmapScalingMode.NearestNeighbor)
        RenderOptions.SetEdgeMode(Me, EdgeMode.Aliased)
        SnapsToDevicePixels = True
        UseLayoutRounding = True
        IsHitTestVisible = False
    End Sub

#Region "属性"

    Private _Text As String = ""
    ''' <summary>要显示的文本，只支持 ASCII。</summary>
    Public Property Text As String
        Get
            Return _Text
        End Get
        Set(value As String)
            value = If(value, "")
            If _Text = value Then Return
            _Text = value
            _Fallback = Nothing
            InvalidateMeasure()
            InvalidateVisual()
        End Set
    End Property

    Private _Cell As Double = 16
    ''' <summary>字格边长（像素）。位图字模式下应为 8 的整数倍。</summary>
    Public Property Cell As Double
        Get
            Return _Cell
        End Get
        Set(value As Double)
            If value < 0 Then value = 0
            If Math.Abs(_Cell - value) < 0.001 Then Return
            _Cell = value
            _Fallback = Nothing
            InvalidateMeasure()
            InvalidateVisual()
        End Set
    End Property

    Private _Foreground As Brush = Brushes.White
    Public Property Foreground As Brush
        Get
            Return _Foreground
        End Get
        Set(value As Brush)
            _Foreground = If(value, Brushes.White)
            InvalidateVisual()
        End Set
    End Property

    Private _ShadowBrush As Brush = New SolidColorBrush(Color.FromArgb(140, 0, 0, 0))
    ''' <summary>硬投影的颜色（游戏内的文字描影是硬阴影，没有模糊）。</summary>
    Public Property ShadowBrush As Brush
        Get
            Return _ShadowBrush
        End Get
        Set(value As Brush)
            _ShadowBrush = value
            InvalidateVisual()
        End Set
    End Property

    Private _ShadowDepth As Double = 2
    ''' <summary>硬投影的偏移像素，0 表示不投影。</summary>
    Public Property ShadowDepth As Double
        Get
            Return _ShadowDepth
        End Get
        Set(value As Double)
            If value < 0 Then value = 0
            If Math.Abs(_ShadowDepth - value) < 0.001 Then Return
            _ShadowDepth = value
            InvalidateMeasure()
            InvalidateVisual()
        End Set
    End Property

#End Region

#Region "度量"

    Private _Fallback As FormattedText

    ''' <summary>没有游戏字体时用的系统字体（启动器自带的 Resources/#PCL English）。</summary>
    Private Shared ReadOnly FallbackTypeface As New Typeface(
        New FontFamily("pack://application:,,,/Resources/#PCL English, Microsoft YaHei UI"),
        FontStyles.Normal, FontWeights.Normal, FontStretches.Normal)

    Private Function GetFallback() As FormattedText
        If _Fallback IsNot Nothing Then Return _Fallback
        Dim Ft As New FormattedText(_Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                    FallbackTypeface, Math.Max(1, _Cell), _Foreground, 1.0)
        _Fallback = Ft
        Return Ft
    End Function

    ''' <summary>实际绘制宽度。位图字模式下每个字形宽度一定是整数像素。</summary>
    Public Function MeasureTextWidth() As Double
        If String.IsNullOrEmpty(_Text) OrElse _Cell <= 0 Then Return 0
        If ModImmersiveAssets.FontReady Then
            Dim Advances As Integer() = ModImmersiveAssets.FontAdvances
            Dim Native As Double = Math.Max(1, ModImmersiveAssets.FontCellSize)
            Dim Total As Double = 0
            For i = 0 To _Text.Length - 1
                Total += Advances(CodeOf(_Text(i))) * _Cell / Native
            Next
            Return Total
        End If
        GetFallback()
        Return _Fallback.WidthIncludingTrailingWhitespace
    End Function

    Private Shared Function CodeOf(Ch As Char) As Integer
        Dim Code As Integer = AscW(Ch)
        If Code < 0 OrElse Code > 255 Then Return 63 '非 ASCII 一律画成问号，宁可显示错也不崩
        Return Code
    End Function

    Protected Overrides Function MeasureOverride(availableSize As Size) As Size
        Return New Size(MeasureTextWidth(), Math.Max(0, _Cell))
    End Function

    ''' <summary>字体图集换成新的之后调用，强制重新排版与重绘。</summary>
    Public Sub Refresh()
        _Fallback = Nothing
        InvalidateMeasure()
        InvalidateVisual()
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)
        If String.IsNullOrEmpty(_Text) OrElse _Cell <= 0 Then Return
        If ModImmersiveAssets.FontReady Then
            '先整段投影，再整段正文：与游戏内文字描影的层次一致
            If _ShadowDepth > 0 Then DrawGlyphs(dc, _ShadowDepth, _ShadowBrush)
            DrawGlyphs(dc, 0, _Foreground)
        Else
            If _ShadowDepth > 0 Then DrawFallback(dc, _ShadowDepth, _ShadowBrush)
            DrawFallback(dc, 0, _Foreground)
        End If
    End Sub

    ''' <summary>
    ''' 逐字画位图字形。每个字形按整格边长缩放后绘制，再按推进宽度前进。
    ''' 因为推进宽度就是「最右侧有笔画的那一列 + 1」，超出的列本来就是空的，所以不需要额外裁切。
    ''' </summary>
    Private Sub DrawGlyphs(dc As DrawingContext, Offset As Double, BrushFill As Brush)
        If BrushFill Is Nothing Then Return
        Dim Glyphs As BitmapSource() = ModImmersiveAssets.FontGlyphs
        Dim Advances As Integer() = ModImmersiveAssets.FontAdvances
        If Glyphs Is Nothing OrElse Advances Is Nothing Then Return
        Dim Native As Double = Math.Max(1, ModImmersiveAssets.FontCellSize)
        Dim CellPx As Double = _Cell
        Dim Plain As Boolean = IsPlainWhite(BrushFill)
        Dim X As Double = 0
        For i = 0 To _Text.Length - 1
            Dim Code As Integer = CodeOf(_Text(i))
            Dim Glyph As BitmapSource = Glyphs(Code)
            '空格没有字形，只前进
            If Glyph IsNot Nothing AndAlso Code <> 32 Then
                Dim Target As New Rect(X + Offset, Offset, CellPx, CellPx)
                If Plain Then
                    dc.DrawImage(Glyph, Target)
                Else
                    '图集里的字形是白色的，要换颜色就只能拿它当蒙版
                    dc.PushOpacityMask(New ImageBrush(Glyph))
                    dc.DrawRectangle(BrushFill, Nothing, Target)
                    dc.Pop()
                End If
            End If
            X += Advances(Code) * CellPx / Native
        Next
    End Sub

    Private Shared Function IsPlainWhite(BrushFill As Brush) As Boolean
        Dim Solid As SolidColorBrush = TryCast(BrushFill, SolidColorBrush)
        Return Solid IsNot Nothing AndAlso Solid.Color = Colors.White
    End Function

    Private Sub DrawFallback(dc As DrawingContext, Offset As Double, BrushFill As Brush)
        If BrushFill Is Nothing Then Return
        Dim Ft As New FormattedText(_Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                    FallbackTypeface, Math.Max(1, _Cell), BrushFill, 1.0)
        dc.DrawText(Ft, New Point(Offset, Offset))
    End Sub

#End Region

End Class
