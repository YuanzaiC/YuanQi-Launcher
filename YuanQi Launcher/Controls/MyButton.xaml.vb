Imports System.Windows.Media.Effects
Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyButton

    '声明
    Public Event Click(sender As Object, e As MouseButtonEventArgs) '自定义事件

    '自定义属性
    Public Uuid As Integer = GetUuid()
    Public ReadOnly Property Inlines As InlineCollection
        Get
            Return LabText.Inlines
        End Get
    End Property
    Public Property Text As String
        Get
            Return GetValue(TextProperty)
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property '显示文本
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If sender IsNot Nothing Then CType(sender, MyButton).LabText.Text = e.NewValue
    End Sub)))
    Public Property TextPadding As Thickness
        Get
            Return LabText.Padding
        End Get
        Set(value As Thickness)
            LabText.Padding = value
        End Set
    End Property
    ''' <summary>
    ''' 按钮文字的字号。
    ''' </summary>
    Public Property TextFontSize As Double
        Get
            Return LabText.FontSize
        End Get
        Set(value As Double)
            LabText.FontSize = value
        End Set
    End Property

    ''' <summary>
    ''' 按钮文字的字重。
    ''' </summary>
    Public Property TextFontWeight As FontWeight
        Get
            Return LabText.FontWeight
        End Get
        Set(value As FontWeight)
            LabText.FontWeight = value
        End Set
    End Property

    ''' <summary>
    ''' 强调色填充时是否强制使用白色文字（默认按强调色对比度自动选择白色或墨色）。
    ''' 用于「启动游戏」这类要求固定白字的按钮。
    ''' </summary>
    Public Property ForceWhiteText As Boolean = False
    ''' <summary>
    ''' 强调色填充时实际使用的文字颜色。
    ''' </summary>
    Private ReadOnly Property AccentTextColor As MyColor
        Get
            Return If(ForceWhiteText, New MyColor(255, 255, 255), OnAccentColor)
        End Get
    End Property
    Private _Logo As String = ""
    ''' <summary>
    ''' 按钮文字左侧的图标（路径数据）。
    ''' </summary>
    Public Property Logo As String
        Get
            Return _Logo
        End Get
        Set(value As String)
            _Logo = If(value, "")
            If PathLogo Is Nothing Then Return
            If _Logo = "" Then
                PathLogo.Visibility = Visibility.Collapsed
            Else
                PathLogo.Data = (New GeometryConverter).ConvertFromString(_Logo)
                PathLogo.Visibility = Visibility.Visible
            End If
        End Set
    End Property
    Private _LogoScale As Double = 1
    Public Property LogoScale As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If PathLogo IsNot Nothing Then PathLogo.RenderTransform = New ScaleTransform With {.ScaleX = value, .ScaleY = value}
        End Set
    End Property
    Private _ColorType As ColorState = ColorState.Normal  '配色方案
    Public Property ColorType As ColorState
        Get
            Return _ColorType
        End Get
        Set(value As ColorState)
            _ColorType = value
            RefreshColor()
        End Set
    End Property
    Public Enum ColorState
        Normal = 0
        Highlight = 1
        Red = 2
    End Enum
    '属性穿透
    Public Shared Shadows ReadOnly PaddingProperty As DependencyProperty = DependencyProperty.Register("Padding", GetType(Thickness), GetType(MyButton), New PropertyMetadata(New PropertyChangedCallback(
        Sub(sender As MyButton, e As DependencyPropertyChangedEventArgs) If sender IsNot Nothing Then sender.PanFore.Padding = e.NewValue)))
    Public Overloads Property Padding As Thickness
        Get
            Return PanFore.Padding
        End Get
        Set(value As Thickness)
            PanFore.Padding = value
        End Set
    End Property
    Public Property RealRenderTransform As Transform
        Get
            Return PanFore.RenderTransform
        End Get
        Set(value As Transform)
            PanFore.RenderTransform = value
        End Set
    End Property

    ''' <summary>
    ''' 强调色填充时是否带主题色投影（首页「启动游戏」这类主按钮用）。
    ''' 开启后：常态柔和投影，悬停加深加远，按下收紧，形成「浮起—按下」的层次。
    ''' </summary>
    Public Property AccentShadow As Boolean = False
    ''' <summary>
    ''' 刷新主题色投影（仅 AccentShadow 为 True 时生效）。
    ''' </summary>
    Private Sub UpdateAccentShadow(IsHover As Boolean, Optional IsPressed As Boolean = False)
        If Not AccentShadow Then Return
        Try
            Dim Eff As DropShadowEffect = TryCast(PanFore.Effect, DropShadowEffect)
            If Eff Is Nothing Then
                Eff = New DropShadowEffect With {.Direction = 270, .RenderingBias = RenderingBias.Performance}
                PanFore.Effect = Eff
            End If
            '投影色取主题强调色，随主题变化
            Try
                Eff.Color = CType(Application.Current.FindResource("ColorObject3"), Color)
            Catch
                Eff.Color = Colors.Black
            End Try
            Dim TargetBlur As Double = If(IsPressed, 12.0, If(IsHover, 30.0, 20.0))
            Dim TargetOpacity As Double = If(IsPressed, 0.2, If(IsHover, 0.46, 0.3))
            Dim TargetDepth As Double = If(IsPressed, 2.0, If(IsHover, 6.0, 3.0))
            Dim Ease As AniEase = New AniEaseOutFluent(AniEasePower.Weak)
            AniStart({
                AaDouble(Eff, DropShadowEffect.BlurRadiusProperty, TargetBlur - Eff.BlurRadius, 160, 0, Ease),
                AaDouble(Eff, DropShadowEffect.OpacityProperty, TargetOpacity - Eff.Opacity, 160, 0, Ease),
                AaDouble(Eff, DropShadowEffect.ShadowDepthProperty, TargetDepth - Eff.ShadowDepth, 160, 0, Ease)
            }, "MyButton Shadow " & Uuid)
        Catch ex As Exception
            Logger.Warn(ex, "刷新按钮投影失败")
        End Try
    End Sub

    '自定义事件
    Private Const AnimationColorIn As Integer = 100
    Private Const AnimationColorOut As Integer = 200
    Private Sub RefreshColor(Optional obj = Nothing, Optional e = Nothing) Handles Me.MouseEnter, Me.MouseLeave, Me.Loaded, Me.IsEnabledChanged
        Try
            UpdateAccentShadow(IsMouseOver)
            If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画

                If IsEnabled Then
                    Select Case ColorType
                        Case ColorState.Normal
                            If IsMouseOver Then
                                '指向：边框加深 + 表面微亮
                                AniStart({AaColor(PanFore, Border.BorderBrushProperty, "BrushBorderHover", AnimationColorIn),
                                          AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush1", AnimationColorIn),
                                          AaColor(PanFore, BackgroundProperty, "BrushHoverSoft", AnimationColorIn)}, "MyButton Color " & Uuid)
                            Else
                                '普通：中性边框 + 白色表面 + 深色文字
                                AniStart({AaColor(PanFore, Border.BorderBrushProperty, "BrushBorderStrong", AnimationColorOut),
                                          AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush1", AnimationColorOut),
                                          AaColor(PanFore, BackgroundProperty, "BrushSurface", AnimationColorOut)}, "MyButton Color " & Uuid)
                            End If
                        Case ColorState.Highlight
                            If IsMouseOver Then
                                '指向：深强调色填充
                                AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrush2", AnimationColorIn),
                                          AaColor(LabText, TextBlock.ForegroundProperty, AccentTextColor - LabText.Foreground, AnimationColorIn),
                                          AaColor(PanFore, BackgroundProperty, "ColorBrush2", AnimationColorIn)}, "MyButton Color " & Uuid)
                            Else
                                '高亮：强调色填充 + 白色文字
                                AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrush3", AnimationColorOut),
                                          AaColor(LabText, TextBlock.ForegroundProperty, AccentTextColor - LabText.Foreground, AnimationColorOut),
                                          AaColor(PanFore, BackgroundProperty, "ColorBrush3", AnimationColorOut)}, "MyButton Color " & Uuid)
                            End If
                        Case ColorState.Red
                            If IsMouseOver Then
                                '红色指向
                                AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrushRedLight", AnimationColorIn),
                                          AaColor(LabText, TextBlock.ForegroundProperty, AccentTextColor - LabText.Foreground, AnimationColorIn),
                                          AaColor(PanFore, BackgroundProperty, "ColorBrushRedLight", AnimationColorIn)}, "MyButton Color " & Uuid)
                            Else
                                '红色填充
                                AniStart({AaColor(PanFore, Border.BorderBrushProperty, "ColorBrushRedDark", AnimationColorOut),
                                          AaColor(LabText, TextBlock.ForegroundProperty, AccentTextColor - LabText.Foreground, AnimationColorOut),
                                          AaColor(PanFore, BackgroundProperty, "ColorBrushRedDark", AnimationColorOut)}, "MyButton Color " & Uuid)
                            End If
                    End Select
                Else
                    '不可用
                    AniStart({AaColor(PanFore, Border.BorderBrushProperty, "BrushBorderStrong", AnimationColorOut),
                              AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrushGray4", AnimationColorOut),
                              AaColor(PanFore, BackgroundProperty, "BrushSurface", AnimationColorOut)}, "MyButton Color " & Uuid)
                End If
            Else

                AniStop("MyButton Color " & Uuid)
                If IsEnabled Then
                    Select Case ColorType
                        Case ColorState.Normal
                            PanFore.SetResourceReference(Border.BorderBrushProperty, "BrushBorderStrong")
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
                            PanFore.SetResourceReference(BackgroundProperty, "BrushSurface")
                        Case ColorState.Highlight
                            If IsMouseOver Then
                                PanFore.SetResourceReference(Border.BorderBrushProperty, "ColorBrush2")
                                PanFore.SetResourceReference(BackgroundProperty, "ColorBrush2")
                            Else
                                PanFore.SetResourceReference(Border.BorderBrushProperty, "ColorBrush3")
                                PanFore.SetResourceReference(BackgroundProperty, "ColorBrush3")
                            End If
                            If ForceWhiteText Then LabText.Foreground = New MyColor(255, 255, 255) Else LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushOnAccent")
                        Case ColorState.Red
                            If IsMouseOver Then
                                PanFore.SetResourceReference(Border.BorderBrushProperty, "ColorBrushRedLight")
                                PanFore.SetResourceReference(BackgroundProperty, "ColorBrushRedLight")
                            Else
                                PanFore.SetResourceReference(Border.BorderBrushProperty, "ColorBrushRedDark")
                                PanFore.SetResourceReference(BackgroundProperty, "ColorBrushRedDark")
                            End If
                            If ForceWhiteText Then LabText.Foreground = New MyColor(255, 255, 255) Else LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushOnAccent")
                    End Select
                Else
                    PanFore.SetResourceReference(Border.BorderBrushProperty, "BrushBorderStrong")
                    LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
                    PanFore.SetResourceReference(BackgroundProperty, "BrushSurface")
                End If

            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新按钮颜色出错")
        End Try
    End Sub

    '鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    Private IsMouseDown As Boolean = False
    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        Logger.Info($"按下按钮：{Text}")
        RaiseEvent Click(sender, e)
        RaiseCustomEvent() '自定义事件
        UpdateAccentShadow(IsMouseOver)
    End Sub
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        IsMouseDown = True
        Focus()
        UpdateAccentShadow(True, IsPressed:=True)
        AniStart({
                 AaScaleTransform(PanFore, 0.955 - CType(PanFore.RenderTransform, ScaleTransform).ScaleX, 80,, New AniEaseOutFluent(AniEasePower.ExtraStrong)),
                 AaScaleTransform(PanFore, -0.01, 700,, New AniEaseOutFluent(AniEasePower.Middle))
                 }, "MyButton Scale " & Uuid)
    End Sub
    Private Sub Button_MouseEnter() Handles Me.MouseEnter
    End Sub
    Private Sub Button_MouseUp() Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        IsMouseDown = False
        AniStart({
               AaScaleTransform(PanFore, 1 - CType(PanFore.RenderTransform, ScaleTransform).ScaleX, 300, 10, New AniEaseOutFluent(AniEasePower.Middle))
           }, "MyButton Scale " & Uuid)
    End Sub
    Private Sub Button_MouseLeave() Handles Me.MouseLeave
        If Not IsMouseDown Then Return
        IsMouseDown = False
        AniStart(AaScaleTransform(PanFore, 1 - CType(PanFore.RenderTransform, ScaleTransform).ScaleX, 800,, New AniEaseOutFluent(AniEasePower.Strong)), "MyButton Scale " & Uuid)
    End Sub

End Class
