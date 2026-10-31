Public Class MyMsgGuide

    Private ReadOnly MyConverter As MyMsgBoxConverter
    Private ReadOnly Uuid As Integer = GetUuid()
    ''' <summary>
    ''' 当前显示到了第几步，从 0 开始。
    ''' </summary>
    Private CurrentStep As Integer = 0
    Private Const StepCount As Integer = 4

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

    ''' <summary>
    ''' 刷新步骤面板、进度点与按钮状态。
    ''' </summary>
    Private Sub UpdateStep()
        Dim Panels = {PanStep1, PanStep2, PanStep3, PanStep4}
        Dim Dots = {Dot1, Dot2, Dot3, Dot4}
        For i = 0 To StepCount - 1
            Panels(i).Visibility = If(i = CurrentStep, Visibility.Visible, Visibility.Collapsed)
            Dots(i).Opacity = If(i = CurrentStep, 1, 0.22)
        Next
        BtnPrev.Visibility = If(CurrentStep = 0, Visibility.Collapsed, Visibility.Visible)
        BtnNext.Text = If(CurrentStep = StepCount - 1, "开始使用", "下一步")
        LabTitle.Text = If(CurrentStep = 0, "欢迎使用 缘启", "使用引导（" & (CurrentStep + 1) & " / " & StepCount & "）")
    End Sub

    Private Sub BtnNext_Click() Handles BtnNext.Click
        If CurrentStep < StepCount - 1 Then
            CurrentStep += 1
            UpdateStep()
            Return
        End If
        CloseGuide(1)
    End Sub

    Private Sub BtnPrev_Click() Handles BtnPrev.Click
        If CurrentStep > 0 Then
            CurrentStep -= 1
            UpdateStep()
        End If
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
