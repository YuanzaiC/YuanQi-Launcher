Public Class MyMsgGuide

    Private ReadOnly MyConverter As MyMsgBoxConverter
    Private ReadOnly Uuid As Integer = GetUuid()
    ''' <summary>
    ''' 当前显示到了第几步，从 0 开始。
    ''' </summary>
    Private CurrentStep As Integer = 0
    Private Const StepCount As Integer = 5

    Public Sub New(Converter As MyMsgBoxConverter)
        Try
            InitializeComponent()
            MyConverter = Converter
            ShapeLine.StrokeThickness = GetWPFSize(1)
        Catch ex As Exception
            Logger.Error(ex, "使用引导弹窗初始化失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub Load(sender As Object, e As EventArgs) Handles MyBase.Loaded
        Try
            'UI 初始化
            UpdateStep()
            '动画
            Opacity = 0
            AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(110, 0, 0, 0) - FrmMain.PanMsg.Background, 200), "PanMsg Background")
            AniStart({
                AaOpacity(Me, 1, 120, 60),
                AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 300, 60, New AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 300, 60, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyMsgGuide " & Uuid)
            '记录日志
            Logger.Info("显示使用引导弹窗")
        Catch ex As Exception
            Logger.Error(ex, "使用引导弹窗加载失败", LogBehavior.Toast)
        End Try
    End Sub

    ''' <summary>刷新步骤面板、进度点与按钮状态（带翻页动画）。</summary>
    Private Sub UpdateStep(Optional Animate As Boolean = False)
        Dim Panels = {PanStep1, PanStep2, PanStep3, PanStep4, PanStep5}
        Dim Dots = {Dot1, Dot2, Dot3, Dot4, Dot5}
        Dim OldStep As Integer = -1
        For i = 0 To StepCount - 1
            If Panels(i).Visibility = Visibility.Visible AndAlso i <> CurrentStep Then OldStep = i
        Next
        If Animate AndAlso OldStep >= 0 Then
            SwitchStep(Panels(OldStep), Panels(CurrentStep))
        Else
            For i = 0 To StepCount - 1
                Panels(i).Visibility = If(i = CurrentStep, Visibility.Visible, Visibility.Collapsed)
            Next
        End If
        For i = 0 To StepCount - 1
            Dots(i).Opacity = If(i = CurrentStep, 1, 0.22)
        Next
        BtnPrev.Visibility = If(CurrentStep = 0, Visibility.Collapsed, Visibility.Visible)
        BtnNext.Text = If(CurrentStep = 0, "同意并继续", If(CurrentStep = StepCount - 1, "开始使用", "下一步"))
        BtnDisagree.Visibility = If(CurrentStep = 0, Visibility.Visible, Visibility.Collapsed)
        '协议页不给"跳过"，必须明确同意或不同意
        BtnSkip.Visibility = If(CurrentStep = 0, Visibility.Collapsed, Visibility.Visible)
        LabTitle.Text = If(CurrentStep = 0, "欢迎使用 缘启", "关于缘启")
        '最后一页：进入时自动体检一次
        If CurrentStep = StepCount - 1 AndAlso Not EnvChecked AndAlso Not EnvRunning Then StartEnvCheck()
    End Sub

    ''' <summary>旧页淡出并轻微上移，新页随后淡入并自下方回弹。</summary>
    Private Sub SwitchStep(Old As StackPanel, New1 As StackPanel)
        EnsurePanelTransform(Old)
        EnsurePanelTransform(New1)
        Dim Ot = CType(Old.RenderTransform, TranslateTransform)
        Dim Nt = CType(New1.RenderTransform, TranslateTransform)
        New1.Opacity = 0
        Nt.Y = 14
        New1.Visibility = Visibility.Visible
        AniStart({
            AaOpacity(Old, 0, 90),
            AaDouble(Sub(i) Ot.Y += i, -10 - Ot.Y, 90),
            AaCode(
            Sub()
                Old.Visibility = Visibility.Collapsed
                Old.Opacity = 1
                Ot.Y = 0
                AniStart({
                    AaOpacity(New1, 1, 170),
                    AaDouble(Sub(i) Nt.Y += i, 0 - Nt.Y, 210, 0, New AniEaseOutFluent(AniEasePower.Weak))
                }, "MyMsgGuide Step In " & Uuid)
            End Sub, 0, True)
        }, "MyMsgGuide Step Out " & Uuid)
    End Sub

    Private Sub EnsurePanelTransform(P As StackPanel)
        If P Is Nothing Then Return
        If TypeOf P.RenderTransform IsNot TranslateTransform Then P.RenderTransform = New TranslateTransform()
    End Sub

    '========== 环境体检 ==========
    Private EnvRunning As Boolean = False
    Private EnvChecked As Boolean = False

    Private Sub BtnEula_Click() Handles BtnEula.Click
        OpenWebsite("https://shimo.im/docs/rGrd8pY8xWkt6ryW")
    End Sub

    Private Sub BtnEnvRun_Click() Handles BtnEnvRun.Click
        StartEnvCheck()
    End Sub

    ''' <summary>后台跑体检，结果逐条填进当前页。</summary>
    Private Sub StartEnvCheck()
        If EnvRunning Then Return
        EnvRunning = True
        BtnEnvRun.IsEnabled = False
        BtnEnvRun.Text = "检查中…"
        PanEnvResult.Children.Clear()
        Dim Tip As New TextBlock With {.Text = "正在检查…", .FontSize = 13, .Opacity = 0.6, .Margin = New Thickness(0, 2, 0, 2)}
        Tip.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        PanEnvResult.Children.Add(Tip)
        RunInNewThread(
        Sub()
            Dim Items As List(Of KeyValuePair(Of String, String)) = Nothing
            Try
                Items = RunEnvCheck()
            Catch ex As Exception
                Logger.Warn(ex, "环境体检失败")
            End Try
            RunInUi(
            Sub()
                EnvRunning = False
                EnvChecked = True
                BtnEnvRun.IsEnabled = True
                BtnEnvRun.Text = "重新检查"
                ShowEnvResult(Items)
            End Sub)
        End Sub, "Guide Env Check", ThreadPriority.BelowNormal)
    End Sub

    Private Sub ShowEnvResult(Items As List(Of KeyValuePair(Of String, String)))
        PanEnvResult.Children.Clear()
        If Items Is Nothing OrElse Items.Count = 0 Then
            Dim Fail As New TextBlock With {.Text = "检查没能完成（可能是网络问题），可以重试，也可以直接继续。", .FontSize = 13, .TextWrapping = TextWrapping.Wrap, .Opacity = 0.75}
            Fail.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            PanEnvResult.Children.Add(Fail)
            Return
        End If
        For Each Item In Items
            Dim Ok1 As Boolean = Item.Value.StartsWithF("OK")
            Dim Line As New TextBlock With {.Text = If(Ok1, "✓ ", "✗ ") & Item.Key & "：" & If(Ok1, "正常", Item.Value),
                                            .FontSize = 13, .TextWrapping = TextWrapping.Wrap, .LineHeight = 20, .Margin = New Thickness(0, 1, 0, 1)}
            If Ok1 Then
                Line.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
            Else
                Line.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
                Line.FontWeight = FontWeights.SemiBold
            End If
            PanEnvResult.Children.Add(Line)
        Next
        '联机模块缺失：给一个直接下载的按钮
        Dim MissingCore As Boolean = Items.Any(Function(I) I.Key = "联机模块" AndAlso Not I.Value.StartsWithF("OK"))
        If MissingCore Then
            Dim BtnDl As New MyButton With {.Text = "下载联机模块", .Padding = New Thickness(13, 6, 13, 6), .ColorType = MyButton.ColorState.Highlight,
                                            .HorizontalAlignment = HorizontalAlignment.Left, .Margin = New Thickness(0, 8, 0, 0)}
            AddHandler BtnDl.Click, Sub() DownloadTerracottaCore()
            PanEnvResult.Children.Add(BtnDl)
        End If
        Dim Problems = Items.Where(Function(I) Not I.Value.StartsWithF("OK")).Count
        Dim Sum As New TextBlock With {.Text = If(Problems = 0, "全部通过，可以放心开始。", $"有 {Problems} 项需要注意，先看上面的说明。"),
                                       .FontSize = 12, .Opacity = 0.7, .Margin = New Thickness(0, 8, 0, 0), .TextWrapping = TextWrapping.Wrap}
        Sum.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        PanEnvResult.Children.Add(Sum)
    End Sub
    Private Sub BtnNext_Click() Handles BtnNext.Click
        If CurrentStep < StepCount - 1 Then
            CurrentStep += 1
            UpdateStep(True)
            Return
        End If
        CloseGuide(1)
    End Sub

    Private Sub BtnPrev_Click() Handles BtnPrev.Click
        If CurrentStep > 0 Then
            CurrentStep -= 1
            UpdateStep(True)
        End If
    End Sub

    Private Sub BtnDisagree_Click() Handles BtnDisagree.Click
        ModGuide.EulaDeclined = True
        CloseGuide(3)
    End Sub

    Private Sub BtnSkip_Click() Handles BtnSkip.Click
        CloseGuide(2)
    End Sub

    ''' <summary>
    ''' 结束引导。
    ''' Result 为 1 表示完整看完，2 表示跳过。
    ''' </summary>
    Private Sub CloseGuide(Result As Integer)
        If MyConverter Is Nothing OrElse MyConverter.IsExited Then Return
        MyConverter.IsExited = True
        MyConverter.Result = Result
        If MyConverter.ForceWait Then MyConverter.WaitFrame.Continue = False
        Interop.ComponentDispatcher.PopModal()
        '动画
        AniStart({
            AaCode(
            Sub()
                If Not WaitingMyMsgBox.Any() Then
                    AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsg.Background, 200, Ease:=New AniEaseOutFluent(AniEasePower.Weak)))
                End If
            End Sub, 30),
            AaOpacity(Me, -Opacity, 80, 20),
            AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 150, 0, New AniEaseOutFluent),
            AaDouble(Sub(i) TransformRotate.Angle += i, 5 - TransformRotate.Angle, 150, 0, New AniEaseInFluent(AniEasePower.Weak)),
            AaCode(Sub() CType(Parent, Grid).Children.Remove(Me), , True)
        }, "MyMsgGuide " & Uuid)
    End Sub

    ''' <summary>
    ''' 是否已经看完了引导。
    ''' </summary>
    Public Shared ReadOnly Property IsFinished As Boolean
        Get
            Return Settings.Get(Of Integer)("SystemGuideVersion") >= GuideVersion
        End Get
    End Property
    ''' <summary>
    ''' 当前的引导版本号。修改引导内容后把它加一，即可让所有用户重新看到引导。
    ''' </summary>
    Public Const GuideVersion As Integer = 1

    ''' <summary>
    ''' 标记引导已经完成。
    ''' </summary>
    Public Shared Sub MarkFinished()
        If Settings.Get(Of Integer)("SystemGuideVersion") < GuideVersion Then Settings.Set("SystemGuideVersion", GuideVersion)
    End Sub

    ''' <summary>
    ''' 拖动窗口（仅标题附近）。
    ''' </summary>
    Private Sub Drag(sender As Object, e As MouseButtonEventArgs) Handles PanBorder.MouseLeftButtonDown, LabTitle.MouseLeftButtonDown
        On Error Resume Next
        If e.GetPosition(ShapeLine).Y <= 2 Then FrmMain.DragMove()
    End Sub

End Class
