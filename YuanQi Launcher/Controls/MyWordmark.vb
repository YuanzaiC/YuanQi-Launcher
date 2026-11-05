'缘启字标（中文像素字）
'
'游戏位图字只有 ASCII，中文没有现成的像素字可用；直接用系统字体会和整页的像素语言脱节。
'做法与 docs/immersive-preview.html 的 buildWordmark() 一一对应：
'  1. 在 40×4 = 160px 下渲染，按包围盒裁紧
'  2. 平滑下采样到 40 格宽（这里是面积平均，等价于浏览器的高质量下采样）—— 每个字格拿到真实覆盖率
'  3. alpha 按 128 阈值二值化，丢掉灰边
'  4. 去掉孤点（4 邻域少于 2 个邻居的像素），修掉笔画末端的毛刺
'  5. 用整数块硬填充输出，1:1 显示，不靠缩放
'
'颗粒尺寸跟右侧 YUANQI 位图字走（位图字字格 16/24/32 时每个字形像素是 2/3/4px），两者颗粒一致才是同一套语言。

Imports System.Globalization

Public Class MyWordmark
    Inherits FrameworkElement

    Public Sub New()
        RenderOptions.SetEdgeMode(Me, EdgeMode.Aliased)
        IsHitTestVisible = False
    End Sub

#Region "属性"

    Private _Text As String = "缘启"
    Public Property Text As String
        Get
            Return _Text
        End Get
        Set(value As String)
            value = If(value, "")
            If _Text = value Then Return
            _Text = value
            Rebuild()
        End Set
    End Property

    Private _BlockSize As Integer = 2
    ''' <summary>输出时每个字格对应的方块边长（2 / 3 / 4px）。</summary>
    Public Property BlockSize As Integer
        Get
            Return _BlockSize
        End Get
        Set(value As Integer)
            value = Math.Max(1, value)
            If _BlockSize = value Then Return
            _BlockSize = value
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
    Public Property ShadowBrush As Brush
        Get
            Return _ShadowBrush
        End Get
        Set(value As Brush)
            _ShadowBrush = value
            InvalidateVisual()
        End Set
    End Property

    Private _ShadowDepth As Double = 3
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

#Region "字形生成"

    ''' <summary>字格数：40 格宽，实测能撑住 12 画的「缘」。笔画更多的字先把 BASE 调大。</summary>
    Private Const BASE As Integer = 40
    ''' <summary>超采样倍数。</summary>
    Private Const SS As Integer = 4
    ''' <summary>二值化阈值。</summary>
    Private Const TH As Integer = 128

    Private Mask As Boolean()
    Private MaskW As Integer
    Private MaskH As Integer
    ''' <summary>字形是否已经生成过。首帧布局时才生成，避免在控件构造阶段做重活。</summary>
    Private _Built As Boolean = False

    Private Shared ReadOnly MarkTypeface As New Typeface(
        New FontFamily("Microsoft YaHei UI, Microsoft YaHei"),
        FontStyles.Normal, FontWeights.Bold, FontStretches.Normal)

    ''' <summary>重新生成字形。字号、文本、字体变化时调用。</summary>
    Public Sub Rebuild()
        _Built = True
        BuildMask()
        InvalidateMeasure()
        InvalidateVisual()
    End Sub

    Private Sub BuildMask()
        Mask = Nothing
        MaskW = 0
        MaskH = 0
        If String.IsNullOrEmpty(_Text) Then Return

        Dim Px As Integer = BASE * SS
        Dim Pad As Integer = Px
        Dim Ft As New FormattedText(_Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                    MarkTypeface, Px, Brushes.White, 1.0)
        Dim W As Integer = CInt(Math.Ceiling(Ft.WidthIncludingTrailingWhitespace)) + Pad * 2
        Dim H As Integer = CInt(Math.Ceiling(Ft.Height)) + Pad * 2
        If W <= 0 OrElse H <= 0 Then Return

        Dim Visual As New DrawingVisual()
        Using Dc = Visual.RenderOpen()
            Dc.DrawText(Ft, New Point(Pad, Pad))
        End Using
        Dim High As New RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32)
        High.Render(Visual)

        Dim Stride As Integer = W * 4
        Dim Pixels(Stride * H - 1) As Byte
        High.CopyPixels(Pixels, Stride, 0)

        '收紧包围盒：不同字体在四周留的白边宽度差很多，不裁会算出大小不一的字标
        Dim MinX As Integer = W, MinY As Integer = H, MaxX As Integer = -1, MaxY As Integer = -1
        For Y = 0 To H - 1
            For X = 0 To W - 1
                If Pixels(Y * Stride + X * 4 + 3) > 8 Then
                    If X < MinX Then MinX = X
                    If X > MaxX Then MaxX = X
                    If Y < MinY Then MinY = Y
                    If Y > MaxY Then MaxY = Y
                End If
            Next
        Next
        If MaxX < 0 Then Return '字体没加载出来就放弃，别画个空框
        Dim Bw As Integer = MaxX - MinX + 1
        Dim Bh As Integer = MaxY - MinY + 1

        Dim Lw As Integer = Math.Max(1, CInt(Math.Round(Bw / SS)))
        Dim Lh As Integer = Math.Max(1, CInt(Math.Round(Bh / SS)))

        '面积平均下采样：每个字格拿到的是真实覆盖率，而不是低分辨率下已经丢失的笔画信息
        Dim Low(Lw * Lh - 1) As Boolean
        For Ly = 0 To Lh - 1
            Dim Sy0 As Integer = MinY + CInt(Math.Floor(Ly * Bh / Lh))
            Dim Sy1 As Integer = MinY + CInt(Math.Floor((Ly + 1) * Bh / Lh)) - 1
            If Sy1 < Sy0 Then Sy1 = Sy0
            For Lx = 0 To Lw - 1
                Dim Sx0 As Integer = MinX + CInt(Math.Floor(Lx * Bw / Lw))
                Dim Sx1 As Integer = MinX + CInt(Math.Floor((Lx + 1) * Bw / Lw)) - 1
                If Sx1 < Sx0 Then Sx1 = Sx0
                Dim Sum As Integer = 0, Count As Integer = 0
                For Y = Sy0 To Sy1
                    For X = Sx0 To Sx1
                        Sum += Pixels(Y * Stride + X * 4 + 3)
                        Count += 1
                    Next
                Next
                Low(Ly * Lw + Lx) = Count > 0 AndAlso Sum / Count >= TH
            Next
        Next

        '去孤点：4 邻域少于 2 个邻居的像素判定为毛刺
        Dim Clean(Lw * Lh - 1) As Boolean
        Array.Copy(Low, Clean, Low.Length)
        For Y = 0 To Lh - 1
            For X = 0 To Lw - 1
                If Not Low(Y * Lw + X) Then Continue For
                Dim Neighbors As Integer = 0
                If X > 0 AndAlso Low(Y * Lw + X - 1) Then Neighbors += 1
                If X < Lw - 1 AndAlso Low(Y * Lw + X + 1) Then Neighbors += 1
                If Y > 0 AndAlso Low((Y - 1) * Lw + X) Then Neighbors += 1
                If Y < Lh - 1 AndAlso Low((Y + 1) * Lw + X) Then Neighbors += 1
                If Neighbors < 2 Then Clean(Y * Lw + X) = False
            Next
        Next

        Mask = Clean
        MaskW = Lw
        MaskH = Lh
    End Sub

#End Region

#Region "绘制"

    Protected Overrides Function MeasureOverride(availableSize As Size) As Size
        If Not _Built Then
            _Built = True
            BuildMask()
        End If
        Return New Size(MaskW * _BlockSize, MaskH * _BlockSize)
    End Function

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)
        If Mask Is Nothing OrElse MaskW <= 0 Then Return
        If _ShadowDepth > 0 Then FillBlocks(dc, _ShadowBrush, _ShadowDepth)
        FillBlocks(dc, _Foreground, 0)
    End Sub

    ''' <summary>按整数块填充。同一行的连续方块合成一个矩形，避免几千次 DrawRectangle。</summary>
    Private Sub FillBlocks(dc As DrawingContext, BrushFill As Brush, Offset As Double)
        If BrushFill Is Nothing Then Return
        Dim B As Double = _BlockSize
        For Y = 0 To MaskH - 1
            Dim X As Integer = 0
            Do While X < MaskW
                If Not Mask(Y * MaskW + X) Then
                    X += 1
                    Continue Do
                End If
                Dim Start As Integer = X
                Do While X < MaskW AndAlso Mask(Y * MaskW + X)
                    X += 1
                Loop
                dc.DrawRectangle(BrushFill, Nothing,
                                 New Rect(Start * B + Offset, Y * B + Offset, (X - Start) * B, B))
            Loop
        Next
    End Sub

#End Region

End Class
