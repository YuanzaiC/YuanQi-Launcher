Imports System.Windows.Media

''' <summary>
''' 左侧主图标栏的导航项（图标在上、文字在下），对齐 yql-preview 的 .nav-item 规格。
''' </summary>
Public Class MyNavItem
    Inherits Grid

    Public Event Check(sender As Object, raiseByMouse As Boolean)

    Private ShapeLogo As Shapes.Path
    Private LabText As TextBlock
    Private PanPill As Border
    Private _Checked As Boolean = False
    Private IsMouseDown As Boolean = False
    Public Uuid As Integer = GetUuid()

    '规格：展开 56×56，收起 36×40
    Private Const RailItemSize As Double = 56
    Private Const RailItemSizeCollapsed As Double = 36
    Private Const RailItemHeightCollapsed As Double = 40

    Public Sub New()
        Width = RailItemSize
        Height = RailItemSize
        Cursor = Cursors.Hand
        UseLayoutRounding = True
        SnapsToDevicePixels = True
        PanPill = New Border With {
            .CornerRadius = New CornerRadius(7),
            .Background = Brushes.Transparent,
            .VerticalAlignment = VerticalAlignment.Center,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .Width = RailItemSize,
            .Height = RailItemSize
        }
        Children.Add(PanPill)
        '图标 + 文字整体居中：折叠时以文字高度平滑收起代替移出布局，分组全程连续居中、无瞬移
        Dim Panel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center, .HorizontalAlignment = HorizontalAlignment.Center}
        PanPill.Child = Panel
        ShapeLogo = New Shapes.Path With {
            .Stretch = Stretch.Uniform,
            .Width = 20,
            .Height = 20,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .Margin = New Thickness(0, 0, 0, 3)
        }
        ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, "ColorBrushGray3")
        Panel.Children.Add(ShapeLogo)
        LabText = New TextBlock With {
            .FontSize = 11,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .LineHeight = 11.5,
            .LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        }
        LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray3")
        Panel.Children.Add(LabText)
    End Sub

    Public Property Logo As String
        Get
            Return ShapeLogo.Data.ToString
        End Get
        Set(value As String)
            ShapeLogo.Data = New GeometryConverter().ConvertFromString(value)
        End Set
    End Property

    Private _LogoScale As Double = 1
    Public Property LogoScale() As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If ShapeLogo IsNot Nothing Then ShapeLogo.RenderTransform = New ScaleTransform With {.ScaleX = value, .ScaleY = value}
        End Set
    End Property

    Private _Collapsed As Boolean = False
    ''' <summary>
    ''' 导航栏是否处于收起状态（仅显示图标）。
    ''' </summary>
    Public Property Collapsed As Boolean
        Get
            Return _Collapsed
        End Get
        Set(value As Boolean)
            If _Collapsed = value Then Return
            _Collapsed = value
            '收起/展开：尺寸弹性过渡；文字高度与透明度平滑动画，
            '并在动画末尾强制落位（Height=0 / Height=auto）——即使掉帧导致动画停在中间值，文字也必定恢复
            AniStop("NavItem Collapse " & Uuid)
            If value Then
                AniStart({
                    AaWidth(Me, RailItemSizeCollapsed - Width, 280, , New AniEaseOutBack(AniEasePower.Weak)),
                    AaHeight(Me, RailItemHeightCollapsed - Height, 180, , New AniEaseOutFluent(AniEasePower.Weak)),
                    AaWidth(PanPill, RailItemSizeCollapsed - PanPill.Width, 280, , New AniEaseOutBack(AniEasePower.Weak)),
                    AaHeight(PanPill, RailItemSizeCollapsed - PanPill.Height, 180, , New AniEaseOutFluent(AniEasePower.Weak)),
                    AaOpacity(LabText, -LabText.Opacity, 90),
                    AaHeight(LabText, -LabText.ActualHeight, 140),
                    AaCode(Sub()
                               LabText.Height = 0
                               LabText.Opacity = 0
                           End Sub, 160, True)
                }, "NavItem Collapse " & Uuid)
            Else
                '文字与图标运动同步开始（无延迟），在展开过程中淡入归位
                '高度增量基于 Height 属性值（收起收尾已置 0）；ActualHeight 在此处为过期值，会导致文字高度动画失效
                AniStart({
                    AaWidth(Me, RailItemSize - Width, 280, , New AniEaseOutBack(AniEasePower.Weak)),
                    AaHeight(Me, RailItemSize - Height, 180, , New AniEaseOutFluent(AniEasePower.Weak)),
                    AaWidth(PanPill, RailItemSize - PanPill.Width, 280, , New AniEaseOutBack(AniEasePower.Weak)),
                    AaHeight(PanPill, RailItemSize - PanPill.Height, 180, , New AniEaseOutFluent(AniEasePower.Weak)),
                    AaOpacity(LabText, 1 - LabText.Opacity, 200),
                    AaHeight(LabText, 11.5 - LabText.Height, 200),
                    AaCode(Sub()
                               LabText.Height = Double.NaN
                               LabText.Opacity = 1
                           End Sub, 210, True)
                }, "NavItem Collapse " & Uuid)
            End If
        End Set
    End Property

    Private _Checked2 As Boolean = False
    Public Property Checked As Boolean
        Get
            Return _Checked2
        End Get
        Set(value As Boolean)
            SetChecked(value, False, True)
        End Set
    End Property

    Public Sub SetChecked(value As Boolean, raiseByMouse As Boolean, anime As Boolean)
        Try
            Dim IsChanged As Boolean = False
            If _Checked2 <> value Then
                _Checked2 = value
                IsChanged = True
            End If
            '保证同级只有一个选中
            If Parent IsNot Nothing Then
                Dim NavList As New List(Of MyNavItem)
                Dim CheckedCount As Integer = 0
                For Each Control In CType(Parent, Object).Children
                    If TypeOf Control Is MyNavItem Then
                        NavList.Add(Control)
                        If Control.Checked Then CheckedCount += 1
                    End If
                Next
                Select Case CheckedCount
                    Case 0
                        If NavList.Any Then NavList(0).Checked = True
                    Case Is > 1
                        If Me.Checked Then
                            For Each Control As MyNavItem In NavList
                                If Control.Checked AndAlso Not Control.Equals(Me) Then Control.Checked = False
                            Next
                        Else
                            Dim FirstChecked = False
                            For Each Control As MyNavItem In NavList
                                If Control.Checked Then
                                    If FirstChecked Then Control.Checked = False Else FirstChecked = True
                                End If
                            Next
                        End If
                End Select
            End If
            If Not IsChanged Then Return
            RefreshColor()
            If Checked Then RaiseEvent Check(Me, raiseByMouse)
        Catch ex As Exception
            Logger.Error(ex, "导航项勾选改变错误", LogBehavior.Toast)
        End Try
    End Sub

    Public Property Text As String
        Get
            Return If(LabText Is Nothing, "", LabText.Text)
        End Get
        Set(value As String)
            If LabText IsNot Nothing Then LabText.Text = value
        End Set
    End Property

    Private Sub RefreshColor(Optional obj = Nothing, Optional e = Nothing) Handles Me.MouseEnter, Me.MouseLeave, Me.Loaded
        Try
            If Not IsLoaded Then Return
            If AniControlEnabled <> 0 Then
                ApplyColorInstant()
                Return
            End If
            If Checked Then
                '选中：强调色浅底 + 强调色图标与文字
                AniStart({
                    AaColor(PanPill, BackgroundProperty, "BrushAccentSoft", 120),
                    AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", 120),
                    AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", 120)
                }, "MyNavItem " & Uuid)
            ElseIf IsMouseOver Then
                '悬停：浅灰底 + 深色图标与文字
                AniStart({
                    AaColor(PanPill, BackgroundProperty, "BrushHover", 100),
                    AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush1", 100),
                    AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush1", 100)
                }, "MyNavItem " & Uuid)
            Else
                '普通：透明底 + 灰色图标与文字
                AniStart({
                    AaColor(PanPill, BackgroundProperty, "ColorBrushTransparent", 150),
                    AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrushGray3", 150),
                    AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrushGray3", 150)
                }, "MyNavItem " & Uuid)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新导航项颜色出错")
        End Try
    End Sub

    Private Sub ApplyColorInstant()
        AniStop("MyNavItem " & Uuid)
        If Checked Then
            PanPill.SetResourceReference(BackgroundProperty, "BrushAccentSoft")
            ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, "ColorBrush3")
            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
        Else
            PanPill.Background = Brushes.Transparent
            ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, "ColorBrushGray3")
            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray3")
        End If
    End Sub

    Private Sub NavItem_MouseUp() Handles Me.MouseLeftButtonUp
        If Checked Then Return
        If Not IsMouseDown Then Return
        IsMouseDown = False
        SetChecked(True, True, True)
    End Sub
    Private Sub NavItem_MouseDown() Handles Me.MouseLeftButtonDown
        If Checked Then Return
        IsMouseDown = True
        RefreshColor()
    End Sub
    Private Sub NavItem_MouseLeave2() Handles Me.MouseLeave
        IsMouseDown = False
    End Sub

End Class
