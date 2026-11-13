' =============================================================================
'  PageLinkMain —— 联机页（陶瓦 / Terracotta）
'  结构：整页只有一张卡片，卡片内分三层——
'        ① PanSelect  选择联机方式（创建房间 / 加入房间）
'        ② PanRoom    连接中与失败（步骤条、进度、取消 / 重试 / 返回）
'        ③ PanDone    已连接（房主看邀请码，访客看服务器地址）
'        三层通过 ShowLayer 在同一张卡片内互换，卡片外框不动。
'  状态：Waiting / Loading / Failed / Finished，由 LinkStates 驱动；
'        加载三步（准备下载 → 下载模块 → 启动并建房/入房）由 LinkLoader 执行。
' =============================================================================
Imports System.Collections.ObjectModel
Imports System.Runtime.InteropServices
Imports 缘启

''' <summary>
''' 联机页面。联机功能由 陶瓦联机（Terracotta）提供：
''' 由它负责 P2P 组网、Minecraft 端口扫描与局域网广播，本页面只负责调用其本地 HTTP 接口。
''' </summary>
Public Class PageLinkMain

    '===============================
    '  状态机与前端页面
    '===============================

#Region "状态管理"

    Public Enum LinkStates
        Waiting
        Loading
        Failed
        Finished
    End Enum
    Public Shared LinkState As LinkStates = LinkStates.Waiting

    ''' <summary>
    ''' 最近一次获取到的房间状态。
    ''' </summary>
    Private Shared CurrentState As ModTerracotta.TerracottaState = Nothing
    ''' <summary>
    ''' 获取房间状态连续失败的次数。
    ''' </summary>
    Private FailCount As Integer = 0

    ''' <summary>
    ''' 切换到指定的状态。
    ''' </summary>
    Private Sub ChangeState(NewState As LinkStates)
        If LinkState = NewState Then Return
        Dim OldState = LinkState
        LinkState = NewState
        Logger.Info($"主状态由 {OldState} 变更为 {NewState}")
        '触发状态切换
        RunInUi(
        Sub()
            Select Case NewState
                Case LinkStates.Waiting
                    SwitchToWaiting(OldState)
                Case LinkStates.Loading
                    SwitchToLoading(OldState)
                Case LinkStates.Failed
                    SwitchToFailed(OldState)
                Case LinkStates.Finished
                    SwitchToFinished(OldState)
            End Select
        End Sub)
    End Sub

    '让加载器的 成功/失败 事件触发状态切换
    Private Sub LoaderInit() Handles Me.Initialized
        '本页不用框架的"加载面板/内容面板"切换（那会把整块界面换掉、看起来像跳页面），
        '改为同一张卡片内部三层互换，所以这里只接管加载器事件。
        PanSelect.Visibility = Visibility.Visible
        PanRoom.Visibility = Visibility.Collapsed
        PanDone.Visibility = Visibility.Collapsed
        PanAlways.Visibility = Visibility.Visible
        AddHandler LinkLoader.OnStateChangedUi,
        Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
            Select Case NewState
                Case LoadState.Finished
                    ChangeState(LinkStates.Finished)
                Case LoadState.Failed
                    Telemetry("联机失败", "Exception", FilterUserName(Loader.Error.GetDisplay(True), "*"))
                    ChangeState(LinkStates.Failed)
            End Select
        End Sub
    End Sub

    '页面动画兼容
    Private Sub UpdatePanelVisibility() Handles Me.PageEnter
        Select Case LinkState
            Case LinkStates.Waiting
                FrmLinkMain.ShowLayer(FrmLinkMain.PanSelect)
            Case LinkStates.Loading, LinkStates.Failed
                FrmLinkMain.ShowLayer(FrmLinkMain.PanRoom)
            Case LinkStates.Finished
                FrmLinkMain.ShowLayer(FrmLinkMain.PanDone)
        End Select
    End Sub

#End Region

#Region "初始页面 (Waiting)"

    '由任意状态切换到 Waiting
    Private Sub SwitchToWaiting(OldState As LinkStates)
        '加载器与进程状态
        LinkLoader.Cancel()
        SyncLock LinkLoader.LockState
            LinkLoader.State = LoadState.Waiting
        End SyncLock
        ProcessStop(IsClosingApp)
        IsClosingApp = False
        CurrentState = Nothing
        FailCount = 0
        '页面切换
        '回到"选择联机方式"层（同一张卡片内切换，用更短的时长）
        ShowLayer(PanSelect, Fast:=True)
    End Sub

    '===============================
    '  页面 UI
    '===============================

    '创建房间
    Private Sub Create_MouseLeftButtonUp() Handles PanSelectCreate.MouseLeftButtonUp
        '陶瓦会自动扫描本机 Minecraft 端口，因此不需要玩家填写端口号
        IsServerSide = True
        RoomCode = Nothing
        Logger.Info("尝试使用陶瓦联机创建房间")
        ChangeState(LinkStates.Loading)
    End Sub

    '加入房间
    Private Shared LastCode As String = Nothing
    ''' <summary>
    ''' 输入邀请码，切换到联机页并立即加入房间。
    ''' </summary>
    ''' <summary>
    ''' 外部入口（自定义事件「加入房间」等）：切到联机页，并把光标放进邀请码输入框。
    ''' 邀请码本身由玩家粘贴，或走下面的 Join(Code) 重载直接加入。
    ''' </summary>
    Public Shared Sub Join()
        Try
            If FrmMain.PageCurrent <> FormMain.PageType.Link Then FrmMain.PageChange(FormMain.PageType.Link)
            If FrmLinkMain IsNot Nothing AndAlso FrmLinkMain.TextJoinCode IsNot Nothing Then FrmLinkMain.TextJoinCode.Focus()
        Catch ex As Exception
            Logger.Warn(ex, "打开联机页失败")
        End Try
    End Sub
    ''' <summary>
    ''' 回退键 / 侧键：联机页是左侧导航的一级页面，按返回不应该跳回首页
    ''' （正在建房或已进房时跳走还会把界面状态丢掉）。有页面栈时仍按正常返回处理。
    ''' </summary>
    Public Overrides Function PageBackInner() As Boolean
        Try
            If FrmMain.PageStack Is Nothing OrElse FrmMain.PageStack.Count = 0 Then
                Hint("联机页请用左侧导航切换页面", HintType.Blue, Key:="LinkBack")
                Return True '吃掉这次返回
            End If
        Catch ex As Exception
        End Try
        Return False
    End Function
    ''' <summary>把输入框里的内容当作邀请码加入（支持直接粘贴整条消息，会自动提取）。</summary>
    Private Sub JoinFromInput()
        Dim Code As String = If(TextJoinCode Is Nothing, "", TextJoinCode.Text)
        If String.IsNullOrWhiteSpace(Code) Then
            Hint("请先粘贴房主发来的邀请码", HintType.Blue)
            If TextJoinCode IsNot Nothing Then TextJoinCode.Focus()
            Return
        End If
        JoinInternal(Code)
    End Sub

    Private Sub BtnJoinInline_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnJoinInline.Click
        JoinFromInput()
    End Sub

    Private Sub TextJoinCode_KeyDown(sender As Object, e As KeyEventArgs) Handles TextJoinCode.KeyDown
        If e.Key = Key.Enter Then
            e.Handled = True
            JoinFromInput()
        End If
    End Sub
    Public Sub JoinInternal(Code As String)
        If Code Is Nothing Then Return
        '基础格式校验
        Dim ValidateResult = ValidateCodeFormat(Code)
        If ValidateResult IsNot Nothing Then
            Hint(ValidateResult, HintType.Red)
            Return
        End If
        Code = FixCodeFormat(Code)
        Logger.Info($"实际使用的邀请码：{Code}")
        '基础信息
        IsServerSide = False
        RoomCode = Code
        LastCode = Code
        '启动
        ChangeState(LinkStates.Loading)
    End Sub
    ''' <summary>
    ''' 校验邀请码格式。格式正确时返回 Nothing，否则返回错误原因。
    ''' </summary>
    Public Shared Function ValidateCodeFormat(Code As String) As String
        If Code Is Nothing Then Return "邀请码为空！"
        Code = FixCodeFormat(Code)
        If Code.Length = 0 Then Return "邀请码为空！"
        '旧版缘启的邀请码
        If Code.Length >= 14 AndAlso Code(0) = "P"c AndAlso Code(5) = "-"c Then
            Return "这是旧版缘启的邀请码，陶瓦联机无法使用。请让房主更新后重新创建房间！"
        End If
        '陶瓦的房间码形如 U/XXXX-XXXX-XXXX-XXXX
        If Code.Length <> 21 OrElse Not Code.StartsWithF("U/") Then
            Return "邀请码格式有误，请让房主使用 缘启 的陶瓦联机创建房间！"
        End If
        For Each Pos As Integer In {6, 11, 16} 'U/ 之后每 5 个字符有一个连字符
            If Code(Pos) <> "-"c Then Return "邀请码格式有误，请检查是否复制完整！"
        Next
        For i = 0 To Code.Length - 1
            If i <= 1 OrElse Code(i) = "-"c Then Continue For '跳过 U/ 前缀与连字符
            If (Code(i) >= "0"c AndAlso Code(i) <= "9"c) OrElse (Code(i) >= "A"c AndAlso Code(i) <= "Z"c) Then Continue For
            Return "邀请码中包含无效字符，请检查是否复制完整！"
        Next
        Return Nothing
    End Function
    ''' <summary>
    ''' 把用户输入（可能是整条消息）修正为标准格式的邀请码。
    ''' </summary>
    Private Shared Function FixCodeFormat(Code As String) As String
        Code = Code.Between("【", "】").Between("[", "]") '从完整消息中提取
        Code = Code.Replace(" ", "").Replace(vbTab, "").Replace(vbCr, "").Replace(vbLf, "") '去掉空白
        Code = Code.Upper.Replace("O", "0").Replace("I", "1") '输入修正
        Return Code
    End Function

    '自动加入

    ''' <summary>
    ''' 切换到联机页并立即加入指定房间。
    ''' </summary>
    Public Shared Sub Join(Code As String)
        If LinkState <> LinkStates.Waiting Then
            Hint("你已经在联机房间中了！", HintType.Red)
        ElseIf FrmMain.PageCurrent = FormMain.PageType.Link Then
            FrmLinkMain.JoinInternal(Code)
        Else
            AutoJoinCode = Code
            FrmMain.PageChange(FormMain.PageType.Link, FormMain.PageSubType.LinkMain)
        End If
    End Sub
    '在进入页面时自动尝试加入房间
    Private Shared AutoJoinCode As String = Nothing
    Private Sub PanSelectJoin_Loaded() Handles PanSelectJoin.Loaded
        If AutoJoinCode Is Nothing Then Return
        If LinkState = LinkStates.Waiting Then JoinInternal(AutoJoinCode)
        AutoJoinCode = Nothing
    End Sub

    '页脚：打开陶瓦联机文档
    Private Sub BtnFooter_Click() Handles BtnFooter.Click
        OpenWebsite("https://burningtnt.github.io/Terracotta/")
    End Sub

#End Region

#Region "加载页面 (Loading | Failed)"

    '由 Waiting 或 Failed 状态切换到 Loading
    Private Sub SwitchToLoading(OldState As LinkStates)
        '清理变量
        FailCount = 0
        CurrentState = Nothing
        '加载器与进程状态
        LinkLoader.Start(IsForceRestart:=True)
        '页面切换会由 Loader 调用 MyPageRight 来触发
        'UI 更新
        UpdateProgressBar(0)
        LinkStartTick = GetTimeMs()
        SetLoadDescStyle(True)
        If BtnLoadCancel IsNot Nothing Then BtnLoadCancel.Visibility = Visibility.Visible
        If PanLoadRetryGroup IsNot Nothing Then PanLoadRetryGroup.Visibility = Visibility.Collapsed
        ShowLayer(PanRoom)
        LabLoadTitle.Text = If(IsServerSide, "房间" & If(IsServerSide, "创建", "加入"), "加入房间")
        UpdateLoadingPage("正在准备联机模块……", "准备联机模块")
    End Sub

    '由 Loading 状态切换到 Failed
    Private Sub SwitchToFailed(OldState As LinkStates)
        '加载器与进程状态（失败时彻底停止模块，避免残留异常状态影响下次建房）
        LinkLoader.Cancel()
        ProcessStop(True)
        'UI 更新
        UpdateProgressBar(1)
        UpdateStepBar(LinkLoader.Progress, IsFailed:=True)
        '显示错误信息
        LabLoadTitle.Text = FailReason
        Dim Brief As String = LinkLoader.Error.GetDisplay(False)
        Dim InnerEx As Exception = LinkLoader.Error
        If InnerEx.Message.StartsWithF("$") Then Brief = InnerEx.Message
        Do Until InnerEx.InnerException Is Nothing
            InnerEx = InnerEx.InnerException
            If InnerEx.Message.StartsWithF("$") Then Brief = InnerEx.Message
        Loop
        LabLoadDesc.Text = If(Brief.StartsWithF("$"), Brief.TrimStart("$"), LinkLoader.Error.GetDisplay(True))
        If BtnLoadCancel IsNot Nothing Then BtnLoadCancel.Visibility = Visibility.Collapsed
        If PanLoadRetryGroup IsNot Nothing Then PanLoadRetryGroup.Visibility = Visibility.Visible
        '失败时收起等待操作提示条
        If HintLoad IsNot Nothing Then HintLoad.Visibility = Visibility.Collapsed
        Logger.Warn(LinkLoader.Error, LabLoadTitle.Text)
    End Sub

    '===============================
    '  页面 UI
    '===============================

    '两行操作的悬停高亮
    Private Sub Row_MouseEnter(sender As Object, e As MouseEventArgs) Handles PanSelectCreate.MouseEnter, PanSelectJoin.MouseEnter
        Try
            DirectCast(sender, Border).Opacity = 0.86
        Catch ex As Exception
        End Try
    End Sub

    Private Sub Row_MouseLeave(sender As Object, e As MouseEventArgs) Handles PanSelectCreate.MouseLeave, PanSelectJoin.MouseLeave
        Try
            DirectCast(sender, Border).Opacity = 1
        Catch ex As Exception
        End Try
    End Sub

    Private Sub SetLoadDescStyle(HintMode As Boolean)
        Try
            If LabLoadDesc Is Nothing Then Return
            LabLoadDesc.FontWeight = If(HintMode, FontWeights.SemiBold, FontWeights.Normal)
            LabLoadDesc.FontSize = If(HintMode, 17.0, 14.0)
        Catch ex As Exception
        End Try
    End Sub

    Private Sub UpdateLoadingPage(Title As String, FailBrief As String, Optional WaitHint As String = Nothing)
        If FailReason = FailBrief & "失败" Then Return
        FailReason = FailBrief & "失败"
        Logger.Info($"开始步骤：{Title}")
        RunInUiWait(
        Sub()
            If FrmLinkMain Is Nothing OrElse Not FrmLinkMain.LabLoadDesc.IsLoaded Then Return
            FrmLinkMain.LabLoadDesc.Text = Title
            '需要在游戏里操作时，用醒目的提示条提醒，避免被误认为启动器卡住
            If FrmLinkMain.HintLoad Is Nothing Then Return
            If String.IsNullOrEmpty(WaitHint) Then
                If FrmLinkMain.HintLoad.Visibility <> Visibility.Collapsed Then FrmLinkMain.HintLoad.Visibility = Visibility.Collapsed
            Else
                If FrmLinkMain.HintLoad.Text <> WaitHint Then FrmLinkMain.HintLoad.Text = WaitHint
                If FrmLinkMain.HintLoad.Visibility <> Visibility.Visible Then FrmLinkMain.HintLoad.Visibility = Visibility.Visible
            End If
        End Sub)
    End Sub
    Private FailReason As String = "准备联机模块"

    '取消加载 / 返回等待页
    ''' <summary>
    ''' 直接退回「未连接」面板。
    ''' 之前只调了 ChangeState(Waiting)，它内部走的是页面级退出流程，取消后画面没有真的切回来，
    ''' 看起来就是"点了没反应"。这里把加载器、模块和三个面板一起复位，保证一定回到选择界面。
    ''' </summary>
    Private Sub BackToWaiting()
        Try
            LinkLoader.Cancel()
            SyncLock LinkLoader.LockState
                LinkLoader.State = LoadState.Waiting
            End SyncLock
            ProcessStop(False)
            CurrentState = Nothing
            FailCount = 0
            LinkState = LinkStates.Waiting
            UpdatePanelVisibility()
            '回到选择层（用更短的时长，卡片缩回不拖沓）
            ShowLayer(PanSelect, Fast:=True)
        Catch ex As Exception
            Logger.Warn(ex, "取消联机失败")
        End Try
    End Sub

    Private Sub BtnLoadCancel_Click() Handles BtnLoadCancel.Click
        BackToWaiting()
    End Sub

    '（原来"点整张卡片重试"已移除：鼠标抬起事件会从取消按钮冒泡上来，
    '  点取消会立刻又被切回加载页，表现就是"点了没反应"。现在重试有独立按钮。）

    '进度条
    ''' <summary>把 0~1 的进度映射到四步指示（准备 / 模块 / 房间 / 就绪）。</summary>
    Private Sub UpdateStepBar(Progress As Double, Optional IsFailed As Boolean = False)
        Try
            If StepDot0 Is Nothing Then Return
            Dim Index As Integer
            If Progress < 0.05 Then
                Index = 0
            ElseIf Progress < 0.4 Then
                Index = 1
            ElseIf Progress < 0.55 Then
                Index = 2
            Else
                Index = 3
            End If
            Dim Dots As Shapes.Ellipse() = {StepDot0, StepDot1, StepDot2, StepDot3}
            If LabLoadStage IsNot Nothing Then
                LabLoadStage.Text = {If(IsServerSide, "准备创建房间", "准备加入房间"), "检查联机模块", If(IsServerSide, "创建房间", "连接房主"), "房间已就绪"}(Index)
            End If
            Dim Accent As Brush = TryCast(Application.Current.FindResource(If(IsFailed, "ColorBrushRed", "ColorBrush3")), Brush)
            Dim Pending As Brush = TryCast(Application.Current.FindResource("ColorBrushGray6"), Brush)
            For i As Integer = 0 To 3
                '走过的和当前节点上色，当前节点稍大，未到的节点是浅灰
                Dots(i).Fill = If(i <= Index, Accent, Pending)
                Dim Size As Double = If(i = Index, 16.0, 12.0)
                Dots(i).Width = Size
                Dots(i).Height = Size
            Next
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>
    ''' 在同一张卡片里换内容：旧层淡出+微缩，新层微缩到正常大小并淡入，卡片外框完全不动。
    ''' </summary>
    Public Sub ShowLayer(Target As FrameworkElement, Optional Fast As Boolean = False)
        Try
            If Target Is Nothing Then Return
            '返回选择界面时用更短的时长：卡片需要缩回较小高度，慢了会显得拖沓
            Dim TimeOut As Integer = If(Fast, 80, 150)
            Dim TimeIn As Integer = If(Fast, 170, 300)
            Dim TimeDelay As Integer = If(Fast, 20, 70)
            Dim ScaleFrom As Double = If(Fast, 0.99, 0.975)
            For Each Layer As FrameworkElement In New FrameworkElement() {PanSelect, PanRoom, PanDone}
                If Layer Is Nothing OrElse Layer Is Target Then Continue For
                If Layer.Visibility = Visibility.Visible Then
                    AniStop("Link Layer " & Layer.Name)
                    AniStart({
                        AaOpacity(Layer, -Layer.Opacity, TimeOut, 0, New AniEaseOutFluent),
                        AaCode(Sub() Layer.Visibility = Visibility.Collapsed, TimeOut + 20)
                    }, "Link Layer " & Layer.Name)
                End If
            Next
            AniStop("Link Layer " & Target.Name)
            Target.Visibility = Visibility.Visible
            Target.RenderTransformOrigin = New Point(0.5, 0.5)
            Target.RenderTransform = New ScaleTransform(ScaleFrom, ScaleFrom)
            Target.Opacity = 0
            AniStart({
                AaOpacity(Target, 1, TimeIn, TimeDelay, New AniEaseOutFluent),
                AaScaleTransform(Target, 1 - ScaleFrom, TimeIn + 30, TimeDelay, New AniEaseOutFluent(AniEasePower.Middle))
            }, "Link Layer " & Target.Name)
        Catch ex As Exception
            Logger.Warn(ex, "切换联机界面层失败")
        End Try
    End Sub

    Private Sub PlayMorphIn(Target As FrameworkElement, Optional Delay As Integer = 0)
        Try
            If Target Is Nothing Then Return
            Target.RenderTransformOrigin = New Point(0.5, 0.5)
            Target.Opacity = 0
            Dim Scale As New ScaleTransform(0.985, 0.985)
            Target.RenderTransform = Scale
            AniStart({
                AaOpacity(Target, 1, 220, Delay, New AniEaseOutFluent),
                AaScaleTransform(Target, 1 - 0.985, 240, Delay, New AniEaseOutFluent(AniEasePower.Middle))
            }, "Link Morph " & GetUuid())
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>进入动画：淡入 + 从下方上浮，避免界面"啪"地一下出现。</summary>
    Private Sub PlayEnterAnim(Target As FrameworkElement, Optional Delay As Integer = 0, Optional FromY As Double = 12)
        Try
            If Target Is Nothing Then Return
            Target.Opacity = 0
            Dim Move As New TranslateTransform(0, FromY)
            Target.RenderTransform = Move
            AniStart({
                AaOpacity(Target, 1, 220, Delay, New AniEaseOutFluent),
                AaTranslateY(Target, -FromY, 260, Delay, New AniEaseOutFluent(AniEasePower.Middle))
            }, "Link Enter " & GetUuid())
        Catch ex As Exception
        End Try
    End Sub

    Private Sub UpdateProgressBar(Optional Value As Double = -1)
        If Value = -1 Then Value = LinkLoader.Progress
        UpdateStepBar(Value)
        Dim DisplayingProgress As Double = ColumnProgressA.Width.Value
        If Math.Round(Value - DisplayingProgress, 3) = 0 Then Return
        If DisplayingProgress <= Value Then
            Dim NewProgress As Double = If(Value = 1, 1, DisplayingProgress +
                (Value - DisplayingProgress) ^ 2 * 0.5)
            AniStart({
                AaGridLengthWidth(ColumnProgressA, NewProgress - ColumnProgressA.Width.Value, 200),
                AaGridLengthWidth(ColumnProgressB, (1 - NewProgress) - ColumnProgressB.Width.Value, 200)
            }, "Link Progress")
        Else
            ColumnProgressA.Width = New GridLength(Value, GridUnitType.Star)
            ColumnProgressB.Width = New GridLength(1 - Value, GridUnitType.Star)
            AniStop("Link Progress")
        End If
        '显示百分比，让进度更直观
        Try
            If LabLoadPercent IsNot Nothing Then LabLoadPercent.Text = $"{Math.Round(Value * 100)}%"
        Catch ex As Exception
            Logger.Warn(ex, "更新联机进度百分比失败")
        End Try
    End Sub
    Private Sub PanRoom_SizeChanged() Handles PanRoom.SizeChanged
        '高度必须与进度条实际高度一致，否则圆角矩形的下半个圆角落在条外，左上角会缺一块
        RectProgressClip.Rect = New Rect(0, 0, PanRoom.ActualWidth, 8)
    End Sub

#End Region

#Region "完成页面 (Finished)"

    '由 Loading 状态切换到 Finished
    Private Sub SwitchToFinished(OldState As LinkStates)
        '由于只可能从加载器的完成事件触发，不需要管加载器
        UpdatePanelVisibility() '页面的实际切换会由 Loader 调用 MyPageRight 来触发

        'UI 更新
        ShowLayer(PanDone)
        Dim CostText As String = ""
        If LinkStartTick > 0 Then CostText = $"{vbCrLf}连接耗时：{(GetTimeMs() - LinkStartTick) / 1000.0:0.0} 秒"
        If IsServerSide Then
            LabFinishTitle.Text = "房间已创建"
            PanFinishCode.Visibility = Visibility.Visible
            PanFinishHall.Visibility = Visibility.Collapsed
            LabFinishCode.Text = If(String.IsNullOrWhiteSpace(RoomCode), "—", RoomCode)
            BtnFinishExit.Text = "关闭房间"
            HintFinish.Visibility = Visibility.Collapsed
            Copy() '立即复制邀请码
        Else
            LabFinishTitle.Text = "已连接房主"
            PanFinishCode.Visibility = Visibility.Collapsed
            PanFinishHall.Visibility = Visibility.Visible
            LabFinishIp.Text = If(CurrentState Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentState.Url), "—", CurrentState.Url)
            BtnFinishExit.Text = "离开房间"
            '访客只需要知道入口在哪：单行提示条，不写解释
            HintFinish.Text = "进游戏 → 多人游戏 → 最下方双击「陶瓦联机大厅」"
        End If
        '立即刷新（不使用网络请求，避免阻塞 UI 线程）
        If CurrentState IsNot Nothing Then RefreshFinishedUi(CurrentState)
    End Sub

    '===============================
    '  页面 UI
    '===============================

    '退出
    Private Sub BtnFinishExit_Click() Handles BtnFinishExit.Click
        TryExit(False, False)
    End Sub
    ''' <summary>
    ''' 退出联机。
    ''' 返回是否弹出了警告窗口并且玩家选择了取消。
    ''' </summary>
    Public Function TryExit(Silent As Boolean, Closing As Boolean) As Boolean
        If LinkState = LinkStates.Waiting OrElse LinkState = LinkStates.Failed Then Return False
        If Not Silent Then
            If IsServerSide AndAlso CurrentState IsNot Nothing AndAlso CurrentState.PeopleCount > 1 Then
                If MyMsgBox("你确定要关闭联机房间吗？" & vbCrLf & "所有玩家都需要重新输入邀请码才可加入游戏！", "退出联机", "确定", "取消", IsWarn:=True) = 2 Then Return True
            ElseIf Closing Then
                If MyMsgBox(If(IsServerSide, "你确定要关闭联机房间吗？", "你确定要离开联机房间吗？"), "退出联机", "确定", "取消") = 2 Then Return True
            End If
        End If
        If Closing Then IsClosingApp = True '启动器退出时才真正结束联机模块
        ChangeState(LinkStates.Waiting)
        Return False
    End Function

    '复制邀请码
    Private Sub Copy() Handles BtnFinishCopy.Click
        '只复制房间码本身，方便直接粘贴
        ClipboardSet(RoomCode, False)
        Settings.Set("LinkLastAutoJoinInviteCode", RoomCode)
        Hint("已复制邀请码！", HintType.Green)
    End Sub

    '复制 IP
    Private Sub BtnFinishIp_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnFinishIp.MouseLeftButtonUp
        ClipboardSet(LabFinishIp.Text, False)
        Hint("已复制服务器地址！", HintType.Green)
    End Sub

#End Region

    '===============================
    '  后端
    '===============================

    ''' <summary>
    ''' 当前是服务端还是客户端。
    ''' </summary>
    Private IsServerSide As Boolean
    ''' <summary>
    ''' 当前使用的房间邀请码（房主在房间就绪后才有值）。
    ''' </summary>
    Private Shared RoomCode As String = Nothing
    ''' <summary>
    ''' 本次建房的起始时间戳，用于统计连接耗时。
    ''' </summary>
    Private Shared LinkStartTick As Long = 0

    ''' <summary>
    ''' 成员列表摘要：标注房主与你。
    ''' </summary>
    Private Function GetMemberSummary(State As ModTerracotta.TerracottaState) As String
        Try
            Dim Parts As New List(Of String)
            For Each P In State.Profiles
                Dim Role As String = ""
                Select Case P.Kind
                    Case "HOST"
                        Role = "（房主）"
                    Case "LOCAL"
                        Role = "（你）"
                    Case "GUEST"
                        Role = ""
                End Select
                Parts.Add($"{P.Name}{Role}")
            Next
            If Parts.Count = 0 Then Return State.PlayerNames
            Return String.Join("、", Parts)
        Catch ex As Exception
            Logger.Warn(ex, "生成成员列表失败")
            Return State.PlayerNames
        End Try
    End Function

#Region "加载"

    ''' <summary>本次下载的联机模块版本，解压完成后写进版本记录。</summary>
    Private Shared PendingModuleVersion As String = Nothing

    Private WithEvents LinkLoader As New LoaderCombo(Of Integer)("联机", {
        New LoaderTask(Of Integer, List(Of NetFile))("准备下载联机模块", AddressOf InitPrepareDownload) With {.ProgressWeight = 5},
        New LoaderDownload("下载联机模块", New List(Of NetFile)) With {.ProgressWeight = 45},
        New LoaderTask(Of Integer, Integer)("启动联机模块", AddressOf InitLaunch) With {.ProgressWeight = 50}
    })

    '1. 检查联机模块，必要时准备下载
    Private Sub InitPrepareDownload(Task As LoaderTask(Of Integer, List(Of NetFile)))
        '获取 CPU 架构
        UpdateLoadingPage("正在获取 CPU 架构……", "获取 CPU 架构")
        Dim Arch As String
        Select Case RuntimeInformation.OSArchitecture
            Case System.Runtime.InteropServices.Architecture.X64
                Arch = "x86_64"
            Case System.Runtime.InteropServices.Architecture.Arm64
                Arch = "arm64"
            Case Else
                Arch = "x86_64"
        End Select
        Logger.Info($"CPU 架构：{Arch}")
        Telemetry("联机开始")
        '检查联机模块
        UpdateLoadingPage("正在检查联机模块……", "检查联机模块")
        Dim RequiredFiles As New List(Of NetFile)
        '两侧启动器版本不同，自带的陶瓦版本也会不同；版本不一致时访客会一直卡在"正在建立连接"。
        '这里把本地版本和官方最新版比一次，不一致就顺带更新。
        Dim LatestModuleVersion As String = ""
        Dim GotLatest As Boolean = ModTerracotta.TryGetLatestVersion(LatestModuleVersion)
        If Not GotLatest Then Logger.Warn("获取联机模块最新版本失败，直接使用现有模块")
        Dim LocalModuleVersion As String = ModTerracotta.GetEffectiveVersion()
        '只有确实查到最新版、且确实比本地新时才更新：断网或本地更新（例如内测版）时不动它
        Dim ModuleOutdated As Boolean = GotLatest AndAlso ModTerracotta.IsNewerVersion(LocalModuleVersion, LatestModuleVersion)
        If ModuleOutdated Then
            Logger.Info($"联机模块需要更新：本地 {LocalModuleVersion} → 最新 {LatestModuleVersion}")
            UpdateLoadingPage($"正在更新联机模块（{LocalModuleVersion} → {LatestModuleVersion}）……", "更新联机模块")
            DirectoryUtils.Create(ModTerracotta.DataPath)
            RequiredFiles.Add(New NetFile(
                ModTerracotta.GetDownloadUrls(Arch, LatestModuleVersion),
                ModTerracotta.DataPath & "terracotta.tar.gz",
                New FileChecker With {.MinSize = 1024 * 1024}))
            PendingModuleVersion = LatestModuleVersion
        ElseIf ModTerracotta.IsCorePresent() Then
            Logger.Info($"已找到联机模块（版本 {LocalModuleVersion}）：{ModTerracotta.BundledCorePath}")
        Else
            Logger.Warn("未找到联机模块，将尝试下载")
            UpdateLoadingPage("正在获取联机模块下载地址……", "获取下载地址")
            DirectoryUtils.Create(ModTerracotta.DataPath)
            RequiredFiles.Add(New NetFile(
                ModTerracotta.GetDownloadUrls(Arch, If(GotLatest, LatestModuleVersion, Nothing)),
                ModTerracotta.DataPath & "terracotta.tar.gz",
                New FileChecker With {.MinSize = 1024 * 1024}))
            '首次下载也要记下版本，否则下次还会以为"本地版本是内置版本"而重复下载
            If GotLatest Then PendingModuleVersion = LatestModuleVersion
        End If
        '开始下载
        UpdateLoadingPage("正在下载联机模块……", "下载联机模块")
        Task.Output = RequiredFiles
    End Sub

    '2. 解压并启动联机模块，然后创建或加入房间
    Private Sub InitLaunch(Task As LoaderTask(Of Integer, Integer))
        '解压文件
        Dim Package As String = ModTerracotta.DataPath & "terracotta.tar.gz"
        If FileUtils.Exists(Package) Then
            UpdateLoadingPage("正在解压联机模块……", "解压联机模块")
            ModTerracotta.ExtractPackage(Package)
            If Not String.IsNullOrWhiteSpace(PendingModuleVersion) Then
                ModTerracotta.MarkCoreVersion(PendingModuleVersion)
                Logger.Info($"联机模块已更新到 {PendingModuleVersion}")
                PendingModuleVersion = Nothing
            End If
        End If
        Task.Progress = 0.05
        '启动进程
        UpdateLoadingPage("正在启动联机模块……", "启动联机模块")
        ModTerracotta.StartCore()
        Task.Progress = 0.15
        '获取玩家名与自定义公共节点
        Dim PlayerName As String = GetPlayerName()
        Dim PublicNodes As String() = GetPublicNodes()
        '创建或加入房间
        If IsServerSide Then
            UpdateLoadingPage("正在创建房间……", "创建房间")
            ModTerracotta.CreateRoom(PlayerName, PublicNodes)
        Else
            UpdateLoadingPage("正在加入房间……", "加入房间")
            Try
                ModTerracotta.JoinRoom(RoomCode, PlayerName, PublicNodes)
            Catch ex As Exception
                '接口返回 400 代表邀请码无效，或联机模块当前不在待机状态
                Throw New Exception($"$邀请码无效，或者房间已经过期。{vbCrLf}请让房主重新创建房间后再试。{vbCrLf}{vbCrLf}{ex.Message}", ex)
            End Try
        End If
        Task.Progress = 0.25
        '等待房间就绪
        Dim FinalState As ModTerracotta.TerracottaState = Nothing
        Dim StateFailCount As Integer = 0
        Dim StallTime As Double = 0
        Dim LastProgress As Double = -1
        Dim ScanTime As Double = 0 'host-scanning 的独立计时器
        Dim GuestTime As Double = 0 'guest-starting 的独立计时器（等待房主开放局域网）
        Do
            Thread.Sleep(300)
            If Task.IsCanceled Then Throw New OperationCanceledException
            '查询状态
            Dim Current As ModTerracotta.TerracottaState
            Try
                Current = ModTerracotta.GetRoomState(3000)
                StateFailCount = 0
            Catch ex As Exception
                StateFailCount += 1
                Logger.Warn(ex, "获取联机状态失败")
                If StateFailCount >= 5 Then
                    Panic("联机模块已停止运行", $"无法连接到联机模块。{vbCrLf}它可能被安全软件拦截，或者已经崩溃。{vbCrLf}{vbCrLf}{GetRecentLog()}")
                    Throw New OperationCanceledException
                End If
                Continue Do
            End Try
            '房间码一出现就记住：房主不必等自己开放局域网才能先把码发给朋友
            If Not String.IsNullOrWhiteSpace(Current.Room) Then RoomCode = Current.Room
            '处理状态
            Select Case Current.State
                Case "host-ok", "guest-ok"
                    FinalState = Current
                    Exit Do
                Case "host-scanning"
                    '房主侧：陶瓦在等游戏里的局域网房间。这里只讲"你该做什么"和"邀请码有没有生成"。
                    ScanTime += 0.3
                    SetLoadDescStyle(True)
                    '房主侧只留一句醒目提示，不再叠一行状态 + 一条提示条
                    Dim ScanTitle As String = If(String.IsNullOrWhiteSpace(RoomCode),
                                                 "请打开游戏局域网功能",
                                                 "邀请码 " & RoomCode & "（请打开游戏局域网功能）")
                    UpdateLoadingPage(ScanTitle, If(ScanTime < 20, "等待开放局域网", If(ScanTime < 60, "等待开放局域网2", "等待开放局域网3")))
                    Task.Progress = Math.Min(0.3 + ScanTime / 600 * 0.25, 0.55)
                    If ScanTime > 600 Then
                        Panic("没有检测到局域网房间", $"请确认你已经在游戏中进入存档，并按 ESC 选择了「对局域网开放」，{vbCrLf}且游戏仍在运行、没有关闭存档。")
                        Throw New OperationCanceledException
                    End If
                Case "host-starting"
                    UpdateLoadingPage("正在创建房间……", "创建房间")
                    Task.Progress = Math.Max(Task.Progress, 0.6)
                Case "guest-connecting"
                    UpdateLoadingPage("正在联系房主……（这一步你无需操作）", "连接房主")
                    Task.Progress = Math.Max(Task.Progress, 0.3)
                Case "guest-starting"
                    '访客侧：房间已经找到，剩下在等房主把局域网开放出来。访客什么都不用做，只提示他去找房主。
                    GuestTime += 0.3
                    Dim GuestTitle As String = "等待房主开放局域网"
                    Dim GuestHint As String = Nothing
                    If GuestTime < 20 Then
                        GuestHint = "这一步由房主完成，你无需操作"
                    ElseIf GuestTime < 60 Then
                        GuestHint = "请联系房主：进入存档 → 按 ESC → 对局域网开放"
                    Else
                        GuestTitle = "房主那边还没有开放局域网"
                        GuestHint = "让房主确认已开放局域网、房间还开着；仍然不行就稍后重试"
                    End If
                    UpdateLoadingPage(GuestTitle, "建立连接" & (If(GuestTime < 20, "1", If(GuestTime < 60, "2", "3"))), GuestHint)
                    '0.5 → 0.85 缓慢推进（90 秒），既不假装完成，也不让进度条一动不动
                    Task.Progress = Math.Min(0.5 + GuestTime / 90 * 0.35, 0.85)
                    '进度在动，停滞判定就永远不会触发，所以这里自己掐 90 秒
                    If GuestTime > 90 Then
                        Panic("等待房主开放局域网超时", $"房主已开放局域网、双方版本也一致时，多半是公共中转节点没连上（跨网络联机必须靠它）。{vbCrLf}请确认：{vbCrLf}- 房主已进入存档，按 ESC → 对局域网开放{vbCrLf}- 双方网络都能访问 etnode.zkitefly.eu.org{vbCrLf}- 在 设置 → 工具 → 联机 里点「检查模块更新」保持版本一致{vbCrLf}{vbCrLf}联机模块日志：{vbCrLf}{ModTerracotta.GetModuleLogTail(15)}{vbCrLf}{vbCrLf}{GetRecentLog()}")
                        Throw New OperationCanceledException
                    End If
                Case "exception"
                    Dim ExceptionLog As String = ModTerracotta.GetModuleLogTail(15)
                    Panic(ModTerracotta.GetExceptionBrief(Current.ExceptionType),
                          ModTerracotta.GetExceptionDetail(Current.ExceptionType) &
                          If(String.IsNullOrWhiteSpace(ExceptionLog), "", vbCrLf & vbCrLf & "联机模块日志：" & vbCrLf & ExceptionLog))
                    Throw New OperationCanceledException
                Case Else
                    UpdateLoadingPage("正在等待联机模块响应……", "等待联机模块响应")
            End Select
            '停滞判定
            If Task.Progress <> LastProgress Then
                LastProgress = Task.Progress
                StallTime = 0
            Else
                StallTime += 0.3
                Dim StallLimit As Double = If(Current.State = "host-scanning", 600, If(Current.State = "guest-starting", 90, 120))
                If StallTime > StallLimit Then
                    Select Case Current.State
                        Case "host-scanning"
                            Panic("没有检测到局域网房间", $"请确认你已经在游戏中按 ESC 并选择「对局域网开放」，{vbCrLf}且游戏仍在运行、没有关闭存档。")
                        Case "guest-starting"
                            Panic("等待房主开放局域网超时", $"房主已开放局域网、双方版本也一致时，多半是公共中转节点没连上（跨网络联机必须靠它）。{vbCrLf}请确认：{vbCrLf}- 房主已进入存档，按 ESC → 对局域网开放{vbCrLf}- 双方网络都能访问 etnode.zkitefly.eu.org{vbCrLf}- 在 设置 → 工具 → 联机 里点「检查模块更新」保持版本一致{vbCrLf}{vbCrLf}联机模块日志：{vbCrLf}{ModTerracotta.GetModuleLogTail(15)}{vbCrLf}{vbCrLf}{GetRecentLog()}")
                        Case "waiting"
                            Panic("联机模块没有响应", $"联机模块没有接受创建或加入房间的请求。{vbCrLf}请点击重试；若反复出现，请向开发者反馈。")
                        Case Else
                            Panic("连接房主超时", $"可能的原因：{vbCrLf}- 你或者房主的网络环境不佳{vbCrLf}- 房主已关闭房间{vbCrLf}- 邀请码输错了{vbCrLf}{vbCrLf}{GetRecentLog()}")
                    End Select
                    Throw New OperationCanceledException
                End If
            End If
        Loop
        '完成
        If FinalState Is Nothing Then Throw New OperationCanceledException
        CurrentState = FinalState
        If Not String.IsNullOrWhiteSpace(FinalState.Room) Then RoomCode = FinalState.Room
        Task.Progress = 0.99
        Logger.Info($"联机房间已就绪：{FinalState.State}，邀请码 {RoomCode}，成员 {FinalState.PlayerNames}")
        Telemetry("联机成功", "Server", If(IsServerSide, "True", "False"), "NAT", If(FinalState.Difficulty, "UNKNOWN"))
    End Sub

    ''' <summary>
    ''' 获取用于展示给其他玩家的名称。
    ''' 优先使用设置中填写的名称，其次使用当前登录的玩家名。
    ''' </summary>
    Private Function GetPlayerName() As String
        Dim Name As String = Settings.Get(Of String)("LinkPlayerName")
        If String.IsNullOrWhiteSpace(Name) Then
            Try
                Name = McLoginName()
            Catch ex As Exception
                Logger.Warn(ex, "获取玩家名失败")
            End Try
        End If
        If String.IsNullOrWhiteSpace(Name) Then Name = Environment.UserName
        If String.IsNullOrWhiteSpace(Name) Then Return Nothing
        Return Name.Trim.BeforeFirst("¨").Trim
    End Function

    ''' <summary>
    ''' 获取玩家填写在设置中的自定义公共节点。
    ''' </summary>
    Private Function GetPublicNodes() As String()
        Dim Raw As String = Settings.Get(Of String)("LinkCustomPeer")
        If String.IsNullOrWhiteSpace(Raw) Then Return ModTerracotta.DefaultPublicNodes '没填就用内置公共节点，否则跨网络联机建不起通道
        '填 "-" 或 "直连" 表示不用节点、只走直连（同一局域网或 NAT 友好时更快）
        If Raw.Trim() = "-" OrElse Raw.Trim() = "直连" Then Return Nothing
        Dim Custom As String() = Raw.Split({","c, "，"c, ";"c, "；"c, vbCr, vbLf, " "c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(Node) Node.Trim).Where(Function(Node) Node <> "").ToArray
        If Custom.Length = 0 Then Return ModTerracotta.DefaultPublicNodes
        Return Custom
    End Function

#End Region

#Region "进程与日志"

    ''' <summary>
    ''' 是否正在退出启动器（决定是彻底结束联机模块，还是只回到待机以便复用）。
    ''' </summary>
    Private Shared IsClosingApp As Boolean = False

    ''' <summary>
    ''' 退出联机。
    ''' 平时只把模块重置为待机并保持运行（下次建房直接复用，也避免反复启停导致 EasyTier 崩溃）；
    ''' 仅在启动器退出时才真正结束模块。
    ''' </summary>
    Public Shared Sub ProcessStop(Optional Closing As Boolean = False)
        If Closing Then
            ModTerracotta.StopCore()
        Else
            ModTerracotta.ResetRoomAsync()
        End If
    End Sub

    ''' <summary>
    ''' 出现意外错误，给出错误信息并结束联机。
    ''' </summary>
    Private Sub Panic(Brief As String, Detail As String)
        '常见原因分析
        If Detail.Contains("public") OrElse Detail.Contains("node") OrElse Detail.Contains("relay") Then
            Detail = $"中转节点不可用。{vbCrLf}请检查双方网络能否访问联机节点；也可以在 设置 → 工具 → 联机 里换一个节点后重试。{vbCrLf}{vbCrLf}{Detail}"
        End If
        If Detail.Contains("failed to listen on ") Then
            Detail = $"监听端口失败。{vbCrLf}请点击重试，如果还是出现此错误，可以重启电脑解决。{vbCrLf}{vbCrLf}{Detail}"
        End If
        '显示信息
        If LinkState = LinkStates.Loading Then
            FailReason = Brief
            LinkLoader.Failed(New Exception(Detail))
        Else
            MyMsgBox(Detail, "联机出错：" & Brief, IsWarn:=True)
            ChangeState(LinkStates.Waiting)
            Telemetry("联机失败", "Exception", FilterUserName(Brief & "：" & Detail, "*"))
        End If
    End Sub

    ''' <summary>
    ''' 获取联机模块最近若干行日志，用于出错时展示。
    ''' </summary>
    Private Function GetRecentLog() As String
        Try
            Dim Log As String = ModTerracotta.ApiGet("/log?fetch=true", 3000)
            If String.IsNullOrWhiteSpace(Log) Then Return "（联机模块没有输出日志）"
            Dim Lines = Log.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Return $"近期日志：{vbCrLf}{Lines.Skip(Math.Max(Lines.Length - 8, 0)).Join(vbCrLf)}"
        Catch ex As Exception
            Logger.Warn(ex, "获取联机模块日志失败")
            Return "（无法获取联机模块日志）"
        End Try
    End Function

#End Region

#Region "定时任务"

    '启动
    Private Sub StartTimerThread() Handles Me.Loaded
        Static IsTimerStarted As Boolean = False
        If IsTimerStarted Then Return
        RunInNewThread(
        Sub()
            Dim Counter As Integer = 0
            Do While True
                Try
                    Thread.Sleep(200)
                    Counter += 200
                    '每秒执行一次
                    If Counter >= 1000 Then
                        Counter = 0
                        Update()
                    End If
                    '每 200ms 更新进度条
                    If LinkState = LinkStates.Loading Then RunInUi(AddressOf UpdateProgressBar)
                Catch ex As Exception
                    Logger.Error(ex, "联机模块主时钟出错")
                    Thread.Sleep(10000)
                End Try
            Loop
        End Sub, "Link Timer")
    End Sub

    '每秒或进入页面时触发
    Private Sub Update() Handles Me.Loaded
        If LinkState <> LinkStates.Finished Then Return
        '获取最新状态
        Dim NewState As ModTerracotta.TerracottaState
        Try
            NewState = ModTerracotta.GetRoomState(3000)
            FailCount = 0
        Catch ex As Exception
            FailCount += 1
            Logger.Warn(ex, "获取联机状态失败")
            If FailCount >= 5 Then
                Panic("联机模块已停止运行", $"无法连接到联机模块。{vbCrLf}它可能被安全软件拦截，或者已经崩溃。{vbCrLf}{vbCrLf}{GetRecentLog()}")
            End If
            Return
        End Try
        CurrentState = NewState
        '异常状态
        If NewState.IsException Then
            Panic(ModTerracotta.GetExceptionBrief(NewState.ExceptionType), ModTerracotta.GetExceptionDetail(NewState.ExceptionType))
            Return
        End If
        '房主关掉局域网后，房间会退回等待状态
        If IsServerSide AndAlso NewState.State = "host-scanning" Then
            Hint("检测到局域网已关闭：朋友现在进不来，重新开放后会自动恢复", HintType.Blue)
        End If
        '房间已关闭
        If NewState.State = "waiting" Then
            MyMsgBox("房间已关闭。", "联机结束")
            ChangeState(LinkStates.Waiting)
            Return
        End If
        '更新 UI
        If FrmMain.PageCurrent = FormMain.PageType.Link Then
            RunInUi(Sub() RefreshFinishedUi(NewState))
        End If
    End Sub

    ''' <summary>
    ''' 刷新完成页面上的信息。
    ''' </summary>
    Private Sub RefreshFinishedUi(State As ModTerracotta.TerracottaState)
        'Logo 旋转动画
        AniStart(AaRotateTransform(ImgFinishLogo, 500, 5000), "Link Logo Rotation")
        '人数显示
        LabFinishPlayer.Text = State.PeopleCount & " 人"
        '地址显示
        If Not IsServerSide Then
            LabFinishIp.Text = If(String.IsNullOrWhiteSpace(State.Url), "—", State.Url)
        End If
        '------------------------
        '  提示条
        '------------------------
        HintFinish.Visibility = Visibility.Collapsed
        If State.PeopleCount <= 1 Then
            HintFinish.Text = If(IsServerSide, "等待朋友加入房间……", "正在等待房主开始游戏……")
            HintFinish.Theme = MyHint.Themes.Blue
            HintFinish.Visibility = Visibility.Visible
            Return
        End If
        If Not IsServerSide AndAlso State.Difficulty = "TOUGH" Then
            HintFinish.Text = "你和房主的网络环境较差，可能正在通过中继连接，延迟会比较高。"
            HintFinish.Theme = MyHint.Themes.Yellow
            HintFinish.Visibility = Visibility.Visible
            Return
        End If
        HintFinish.Text = $"房间内已有 {State.PeopleCount} 名玩家：{GetMemberSummary(State)}"
        HintFinish.Theme = MyHint.Themes.Blue
        HintFinish.Visibility = Visibility.Visible
    End Sub

#End Region

End Class
