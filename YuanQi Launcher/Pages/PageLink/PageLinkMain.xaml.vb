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
    '' <summary>用户点了取消：此后加载器再报失败/完成都不再切面板。</summary>
    Private CancelRequested As Boolean = False

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
            If CancelRequested Then Return '用户已取消，忽略后续状态
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
        JoinNodesFromInvite = False '建房时按自己的设置走
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
        '房主发的整条消息里可能带一行"节点：…"，用它就和房主走同一个中转节点
        JoinNodesFromInvite = False
        JoinNodesOverride = Nothing
        If Code.Contains("节点") Then
            Dim Matched As Text.RegularExpressions.Match = Text.RegularExpressions.Regex.Match(Code, "节点\s*[:：]\s*([^\r\n]+)")
            If Matched.Success Then
                Dim NodeText As String = Matched.Groups(1).Value.Trim
                If NodeText <> "" Then
                    If NodeText <> "-" AndAlso NodeText <> "直连" Then
                        JoinNodesOverride = NodeText.Split({","c, "，"c, ";"c, " "c}, StringSplitOptions.RemoveEmptyEntries).
                            Select(Function(Item) Item.Trim).Where(Function(Item) Item <> "").ToArray
                    End If
                    JoinNodesFromInvite = True
                    Logger.Info($"使用房主消息里的节点：{NodeText}")
                End If
                Code = Code.Replace(Matched.Value, "")
            End If
        End If
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

    '===============================
    '  页面 UI
    '===============================

    ''' <summary>由 Waiting 切换到 Loading。</summary>
    Private Sub SwitchToLoading(OldState As LinkStates)
        CancelRequested = False
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
        StartLyricTimer()
        LabLoadTitle.Text = If(IsServerSide, "房间创建", "加入房间")
        UpdateLoadingPage("正在准备联机模块……", "准备联机模块")
    End Sub

    ''' <summary>由 Loading 切换到 Failed。</summary>
    Private Sub SwitchToFailed(OldState As LinkStates)
        '加载器与进程状态（失败时彻底停止模块，避免残留异常状态影响下次建房）
        LinkLoader.Cancel()
        '不要立刻杀掉模块：它在退出时才会把 application.log 写完。
        '后台等几秒再停，然后把日志末尾贴到失败卡片上，省得玩家到处找文件。
        RunInNewThread(
            Sub()
                '模块的 application.log 只在退出时才刷盘，实例临时目录随后又会被清掉，
                '所以先让它正常退出、把日志复制出来，再兜底强停。
                Dim Tail As String = ModTerracotta.ShutdownAndCollectLog(6000, 25)
                Try
                    ProcessStop(True)
                Catch ex As Exception
                End Try
                If String.IsNullOrWhiteSpace(Tail) Then Return
                RunInUi(
                    Sub()
                        Try
                            If FrmLinkMain Is Nothing OrElse FrmLinkMain.LabLoadDesc Is Nothing Then Return
                            If LinkState <> LinkStates.Failed Then Return
                            FrmLinkMain.LabLoadDesc.Text = FrmLinkMain.LabLoadDesc.Text & vbCrLf & vbCrLf & "模块日志：" & vbCrLf & Tail
                        Catch ex As Exception
                        End Try
                    End Sub)
            End Sub, "Link Failure Diag")
        'UI 更新
        UpdateProgressBar(1)
        UpdateStepBar(LinkLoader.Progress, IsFailed:=True)
        '失败时把内存里的模块日志落盘一份，方便反馈时取用（只写一次，不会拖慢界面）
        ModTerracotta.DumpModuleLog()
        '显示错误信息
        LabLoadTitle.Text = FailReason
        Dim Brief As String = LinkLoader.Error.GetDisplay(False)
        Dim InnerEx As Exception = LinkLoader.Error
        If InnerEx.Message.StartsWithF("$") Then Brief = InnerEx.Message
        '取出最内层带 $ 前缀的那句，它才是写给玩家看的
        Do While InnerEx.InnerException IsNot Nothing
            InnerEx = InnerEx.InnerException
            If InnerEx.Message.StartsWithF("$") Then Brief = InnerEx.Message
        Loop
        DescPhase = "idle"
        DescPendingText = Nothing
        If LabLoadDesc IsNot Nothing Then LabLoadDesc.Opacity = 1
        LabLoadDesc.Text = If(Brief.StartsWithF("$"), Brief.TrimStart("$"), LinkLoader.Error.GetDisplay(True))
        If BtnLoadCancel IsNot Nothing Then BtnLoadCancel.Visibility = Visibility.Collapsed
        If PanLoadRetryGroup IsNot Nothing Then PanLoadRetryGroup.Visibility = Visibility.Visible
        Logger.Warn(LinkLoader.Error, LabLoadTitle.Text)
        StopLyricTimer()
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
            Try
                If FrmLinkMain Is Nothing OrElse FrmLinkMain.LabLoadDesc Is Nothing Then Return
                '文字先挂起，等阶段动画滚到这一步时一起显示
                FrmLinkMain.PendingDesc = Title
                FrmLinkMain.ShowPhase(FailBrief)
            Catch ex As Exception
            End Try
        End Sub)
    End Sub
    Private FailReason As String = "准备联机模块"

    '取消加载 / 返回等待页
    ''' <summary>
    ''' 直接退回「未连接」面板。
    ''' 加载器、模块和三个面板一起复位，保证一定回到选择界面。
    ''' </summary>
    Private Sub BackToWaiting()
        Try
            CancelRequested = True
            LinkLoader.Cancel()
            SyncLock LinkLoader.LockState
                LinkLoader.State = LoadState.Waiting
            End SyncLock
            ProcessStop(False)
            CurrentState = Nothing
            FailCount = 0
            LinkState = LinkStates.Waiting
            StopLyricTimer()
            '按钮状态复位：连接中显示取消，失败组收起
            If BtnLoadCancel IsNot Nothing Then BtnLoadCancel.Visibility = Visibility.Visible
            If PanLoadRetryGroup IsNot Nothing Then PanLoadRetryGroup.Visibility = Visibility.Collapsed
            '回到选择层（用更短的时长，卡片缩回不拖沓）
            ShowLayer(PanSelect, Fast:=True)
            Logger.Info("已取消联机并回到选择界面")
        Catch ex As Exception
            Logger.Warn(ex, "取消联机失败")
        End Try
    End Sub
    Private Sub BtnLoadCancel_Click() Handles BtnLoadCancel.Click
        BackToWaiting()
    End Sub

    '' <summary>失败后重试：清掉失败态，按上次的方式重新连一次。</summary>
    Private Sub BtnLoadRetry_Click() Handles BtnLoadRetry.Click
        Try
            CancelRequested = False
            FailCount = 0
            CurrentState = Nothing
            LinkState = LinkStates.Waiting
            ChangeState(LinkStates.Loading)
        Catch ex As Exception
            Logger.Warn(ex, "联机重试失败")
            BackToWaiting()
        End Try
    End Sub

    '' <summary>失败后返回：回到选择界面。</summary>
    Private Sub BtnLoadBack_Click() Handles BtnLoadBack.Click
        BackToWaiting()
    End Sub

    '===============================
    '  阶段（歌词式滚动）
    '===============================
    '约定：四行文字始终填满（上一行 / 当前 / 下一步 / 再下一步），
    '任何时刻都不留空格；过渡只做"整块上移一行 + 两行字号互换"，由定时器按时间推进。
    '每一步的数值都会写进日志，可以仅凭日志核对动画是否真的在走。

    Private Const LyricRowHeight As Double = 42.0
    ''' <summary>每个阶段的保底停留时间（毫秒）。</summary>
    Private Const LyricMinDwell As Integer = 900
    ''' <summary>划线时长（毫秒）。</summary>
    Private Const LyricStrikeMs As Integer = 180
    ''' <summary>划线结束后停一下再滚。</summary>
    Private Const LyricStrikeHold As Integer = 120
    ''' <summary>整块上移一行的时长（毫秒）。</summary>
    Private Const LyricRollMs As Integer = 620

    Private LyricStages As String() = Nothing
    Private LyricIndex As Integer = -1 '当前中间那一行是第几阶段
    Private LyricWanted As Integer = -1 '流程真正走到第几阶段
    Private LyricPhase As String = "idle" '"dwell" / "strike" / "roll"
    Private LyricPhaseStart As Integer = 0
    Private LyricTimer As System.Windows.Threading.DispatcherTimer = Nothing
    Private LyricLastLogTick As Integer = 0
    Private PendingDesc As String = Nothing
    '描述文字的进场/退场
    Private Const DescFadeOutMs As Integer = 130
    Private Const DescFadeInMs As Integer = 190
    Private DescPhase As String = "idle" '"idle" / "out" / "in"
    Private DescPhaseStart As Integer = 0
    Private DescPendingText As String = Nothing
    Private LyricBrushRow1 As SolidColorBrush = Nothing
    Private LyricBrushRow2 As SolidColorBrush = Nothing
    Private LyricBrushBar As SolidColorBrush = Nothing
    Private BarRollFrom As Double = 0
    Private BarRollTo As Double = 0
    Private BarFullAt165 As Double = 0

    Private Function LyricLabs() As TextBlock()
        If LabLyric0 Is Nothing OrElse LabLyric1 Is Nothing OrElse LabLyric2 Is Nothing OrElse LabLyric3 Is Nothing Then Return Nothing
        Return {LabLyric0, LabLyric1, LabLyric2, LabLyric3}
    End Function

    Private Function LyricBars() As Shapes.Rectangle()
        If BarLyric0 Is Nothing OrElse BarLyric1 Is Nothing OrElse BarLyric2 Is Nothing Then Return Nothing
        Return {BarLyric0, BarLyric1, BarLyric2, Nothing}
    End Function

    ''' <summary>各阶段的名字（房主 / 加入方不同）。</summary>
    Private Function GetStages() As String()
        Return If(IsServerSide,
            {"准备联机模块", "检查联机组件", "等待开放局域网", "接入中转节点", "房间创建完成"},
            {"准备联机模块", "检查联机组件", "加入房间", "连接房主", "进入房间完成"})
    End Function

    ''' <summary>把当前步骤归到阶段序号。</summary>
    Private Function StageFromPhase(PhaseKey As String) As Integer
        If PhaseKey Is Nothing Then Return 0
        If PhaseKey.Contains("房间创建完成") OrElse PhaseKey.Contains("进入房间完成") Then Return 4
        If PhaseKey.Contains("等待开放局域网") Then Return 2
        If IsServerSide Then
            If PhaseKey.Contains("创建房间") OrElse PhaseKey.Contains("接入中转") Then Return 3
        Else
            If PhaseKey.Contains("加入房间") Then Return 2
            If PhaseKey.Contains("连接房主") OrElse PhaseKey.Contains("建立连接") OrElse
               PhaseKey.Contains("等待联机模块") Then Return 3
        End If
        If PhaseKey.Contains("检查") OrElse PhaseKey.Contains("下载") OrElse PhaseKey.Contains("解压") OrElse
           PhaseKey.Contains("启动") OrElse PhaseKey.Contains("CPU") OrElse PhaseKey.Contains("地址") Then Return 1
        Return 0
    End Function

    ''' <summary>步骤变了：阶段与下方文字一起走。</summary>
    Private Sub ShowPhase(PhaseKey As String)
        Try
            SetLyricStage(GetStages(), StageFromPhase(PhaseKey), False)
            FlushDescIfCaught()
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>阶段追上流程了才换下方文字。</summary>
    Private Sub FlushDescIfCaught()
        Try
            If LabLoadDesc Is Nothing Then Return
            If LyricPhase <> "idle" Then Return
            If LyricIndex <> LyricWanted Then Return
            If PendingDesc Is Nothing Then Return
            Dim Text As String = PendingDesc
            PendingDesc = Nothing
            If Text = LabLoadDesc.Text Then Return
            DescPendingText = Text
            '正在淡入淡出就只更新目标文字，等这一轮走完再换
            If DescPhase <> "idle" Then Return
            DescPhase = "out"
            DescPhaseStart = GetTimeMs()
            StartLyricTimer()
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>描述文字：淡出 → 换字 → 淡入（由同一个定时器驱动，不依赖动画对象）。</summary>
    Private Sub TickDesc()
        Try
            If DescPhase = "idle" Then Return
            If LabLoadDesc Is Nothing Then
                DescPhase = "idle"
                Return
            End If
            Dim Elapsed As Integer = GetTimeMs() - DescPhaseStart
            Select Case DescPhase
                Case "out"
                    Dim Q As Double = Math.Min(1.0, Elapsed / DescFadeOutMs)
                    LabLoadDesc.Opacity = 1 - 0.92 * Q
                    If Elapsed >= DescFadeOutMs Then
                        If DescPendingText IsNot Nothing Then
                            LabLoadDesc.Text = DescPendingText
                            DescPendingText = Nothing
                        End If
                        DescPhase = "in"
                        DescPhaseStart = GetTimeMs()
                    End If
                Case "in"
                    Dim Q As Double = Math.Min(1.0, Elapsed / DescFadeInMs)
                    LabLoadDesc.Opacity = 0.08 + 0.92 * Q
                    If Elapsed >= DescFadeInMs Then
                        LabLoadDesc.Opacity = 1
                        DescPhase = "idle"
                    End If
            End Select
        Catch ex As Exception
            DescPhase = "idle"
        End Try
    End Sub

    ''' <summary>等阶段动画彻底走完。</summary>
    Private Sub WaitLyricIdle(MaxMs As Integer)
        Try
            Dim Start As Integer = GetTimeMs()
            Do
                If LyricPhase = "idle" AndAlso DescPhase = "idle" AndAlso LyricIndex >= LyricWanted Then Return
                If GetTimeMs() - Start > MaxMs Then Return
                Thread.Sleep(50)
            Loop
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>失败 / 完成时用。</summary>
    Private Sub UpdateStepBar(Progress As Double, Optional IsFailed As Boolean = False)
        Try
            Dim Stages As String() = GetStages()
            If IsFailed Then
                SetLyricStage(Stages, Math.Max(0, LyricIndex), True)
            ElseIf Progress >= 0.99 Then
                SetLyricStage(Stages, Stages.Length - 1, False)
            End If
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>流程走到第 Index 阶段：直接落位或开始推进动画。</summary>
    Private Sub SetLyricStage(Stages As String(), Index As Integer, Optional IsFailed As Boolean = False)
        Try
            If PanLyricInner Is Nothing OrElse LyricLabs() Is Nothing Then Return
            If Not Dispatcher.CheckAccess() Then
                Dispatcher.BeginInvoke(New Action(Sub() SetLyricStage(Stages, Index, IsFailed)))
                Return
            End If
            Dim Changed As Boolean = (LyricStages Is Nothing OrElse String.Join("|", LyricStages) <> String.Join("|", Stages))
            LyricStages = Stages
            Dim Target As Integer = Math.Max(0, Math.Min(Index, Stages.Length - 1))
            If IsFailed Then
                LyricPhase = "idle"
                If LyricIndex < 0 Then RenderLyric(Target)
                SetAccentColor(True)
                LyricWanted = Math.Max(0, LyricIndex)
                Return
            End If
            If Changed OrElse LyricIndex < 0 OrElse Target < LyricIndex Then
                RenderLyric(Target)
                LyricWanted = Target
                FlushDescIfCaught()
                Return
            End If
            LyricWanted = Target
            If LyricPhase = "idle" Then BeginDwell()
        Catch ex As Exception
            Logger.Warn(ex, "切换联机阶段失败")
        End Try
    End Sub

    Private Sub BeginDwell()
        Try
            If LyricIndex >= LyricWanted Then
                LyricPhase = "idle"
                FlushDescIfCaught()
                Return
            End If
            LyricPhase = "dwell"
            LyricPhaseStart = GetTimeMs()
            StartLyricTimer()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub StartLyricTimer()
        Try
            If LyricTimer Is Nothing Then
                LyricTimer = New System.Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(25)}
                AddHandler LyricTimer.Tick, Sub(Sender As Object, Args As EventArgs) LyricTick()
            End If
            If Not LyricTimer.IsEnabled Then LyricTimer.Start()
        Catch ex As Exception
            Logger.Warn(ex, "启动联机阶段定时器失败")
        End Try
    End Sub

    Private Sub StopLyricTimer()
        Try
            If LyricTimer IsNot Nothing Then LyricTimer.Stop()
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>心跳：停留 → 划线 → 滚一行 → 落位。</summary>
    Private Sub LyricTick()
        Try
            TickDesc()
            If LyricStages Is Nothing Then Exit Sub
            If LyricPhase = "idle" Then
                If LyricIndex < LyricWanted Then
                    BeginDwell()
                ElseIf LyricIndex > LyricWanted Then
                    RenderLyric(LyricWanted)
                Else
                    FlushDescIfCaught()
                End If
                Return
            End If
            Dim Elapsed As Integer = GetTimeMs() - LyricPhaseStart
            Select Case LyricPhase
                Case "dwell"
                    If LyricIndex >= LyricWanted Then
                        LyricPhase = "idle"
                        FlushDescIfCaught()
                        Return
                    End If
                    If Elapsed >= LyricMinDwell Then
                        If LyricIndex >= LyricStages.Length - 1 Then
                            LyricPhase = "idle"
                            FlushDescIfCaught()
                            Return
                        End If
                        LyricPhase = "strike"
                        LyricPhaseStart = GetTimeMs()
                        Logger.Info($"联机阶段：开始划线（第 {LyricIndex + 1} 行，共 {LyricStages.Length} 行）")
                    End If
                Case "strike"
                    ApplyStrike(Math.Min(1.0, Elapsed / LyricStrikeMs))
                    If Elapsed >= LyricStrikeMs + LyricStrikeHold Then
                        LyricPhase = "roll"
                        LyricPhaseStart = GetTimeMs()
                        LyricLastLogTick = 0
                        '划线长度按字号比例换算（16.5 → 13），不重新测量，避免量到缓存值导致"线不变"
                        If BarLyric1 IsNot Nothing Then
                            BarRollFrom = BarLyric1.Width
                            If BarRollFrom <= 1 Then BarRollFrom = MeasureText(LabLyric1) + 2
                            BarRollTo = BarRollFrom * 13.0 / 16.5
                        End If
                        ApplyRoll(0)
                        Logger.Info("联机阶段：开始滚动")
                    End If
                Case "roll"
                    Dim P As Double = EaseRoll(Math.Min(1.0, Elapsed / LyricRollMs))
                    ApplyRoll(P)
                    If Elapsed - LyricLastLogTick >= 120 Then
                        LyricLastLogTick = Elapsed
                        Dim LabsLog As TextBlock() = LyricLabs()
                        Logger.Info($"联机阶段：滚动 {Elapsed}ms 位移={PanLyricInner.Margin.Top:0.0} 上字号={LabsLog(1).FontSize:0.0} 中字号={LabsLog(2).FontSize:0.0} 线长={If(BarLyric1 Is Nothing, -1, BarLyric1.Width):0.0} 上色={ColorText(LabsLog(1).Foreground)} 中色={ColorText(LabsLog(2).Foreground)}")
                    End If
                    If Elapsed >= LyricRollMs Then
                        '先把 p=1 这一帧画出来，落位帧与它完全一致，收尾不会有一点跳动
                        ApplyRoll(1.0)
                        Dim NextIdx As Integer = Math.Min(LyricIndex + 1, LyricStages.Length - 1)
                        RenderLyric(NextIdx)
                        Logger.Info($"联机阶段：落位到第 {NextIdx + 1} 行 [{LabLyric0.Text}|{LabLyric1.Text}|{LabLyric2.Text}|{LabLyric3.Text}] 位移={PanLyricInner.Margin.Top:0.0}")
                        BeginDwell()
                    End If
                Case Else
                    LyricPhase = "idle"
                    FlushDescIfCaught()
            End Select
        Catch ex As Exception
            LyricPhase = "idle"
            Logger.Warn(ex, "联机阶段动画出错")
        End Try
    End Sub

    ''' <summary>划线：当前行从左往右长出线。</summary>
    Private Sub ApplyStrike(Progress As Double)
        Try
            If LabLyric1 Is Nothing OrElse BarLyric1 Is Nothing Then Return
            Dim Full As Double = MeasureText(LabLyric1) + 2
            BarLyric1.Width = Full * Math.Max(0.0, Math.Min(1.0, Progress))
            BarLyric1.Opacity = 0.85
            If Progress >= 1 Then BarFullAt165 = Full
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>
    ''' 滚动：整块上移一行；同时上格缩到 13 号变灰、中格放大到 16.5 变主题色、划线一起缩短。
    ''' Progress 从 0 到 1，所有属性都是绝对赋值，不存在累积误差。
    ''' </summary>
    Private Sub ApplyRoll(Progress As Double)
        Try
            If PanLyricInner Is Nothing Then Return
            Dim Labs As TextBlock() = LyricLabs()
            If Labs Is Nothing Then Return
            Dim P As Double = Math.Max(0.0, Math.Min(1.0, Progress))
            PanLyricInner.Margin = New Thickness(0, -LyricRowHeight * P, 0, 0)
            Labs(1).FontSize = 16.5 - 3.5 * P
            Labs(1).Opacity = 1.0 - 0.58 * P
            Labs(2).FontSize = 13.0 + 3.5 * P
            Labs(2).Opacity = 0.42 + 0.58 * P
            '最下面那一格正在滑进等待位：透明度从 0.16 升到 0.42，落位后正好接上，不会突然变"灰"
            Labs(3).Opacity = 0.16 + 0.26 * P
            '划线：先做这一步，且单独保护，颜色出问题也不影响线长
            Try
                If BarLyric1 IsNot Nothing Then
                    '线长跟着当前字号走：16.5 号时的完整线长 × (当前字号 / 16.5)
                    If BarFullAt165 <= 1 Then BarFullAt165 = MeasureTextAt(Labs(1), 16.5) + 2
                    BarLyric1.Width = BarFullAt165 * (Labs(1).FontSize / 16.5)
                    BarLyric1.Opacity = 0.85 - 0.30 * P
                End If
            Catch ex As Exception
                Logger.Warn(ex, "滚动时调整划线失败")
            End Try
            '颜色：文字与划线一起按进度插值
            Try
                Dim AccentColor As Color = ColorOf(AccentBrush(False), Colors.Green)
                Dim FadedColor As Color = ColorOf(FadedBrush(), Colors.Gray)
                '每帧新建画刷再赋值：改已赋值画刷的 Color 在冻结状态下会抛异常，颜色就永远不变
                Labs(1).Foreground = New SolidColorBrush(LerpColor(AccentColor, FadedColor, P))
                Labs(2).Foreground = New SolidColorBrush(LerpColor(FadedColor, AccentColor, P))
                If BarLyric1 IsNot Nothing Then BarLyric1.Fill = New SolidColorBrush(LerpColor(AccentColor, FadedColor, P))
            Catch ex As Exception
                Logger.Warn(ex, "滚动时颜色过渡失败")
            End Try
        Catch ex As Exception
            Logger.Warn(ex, "滚动失败")
        End Try
    End Sub

    ''' <summary>两端缓动（划线的淡入这类短动画用）。</summary>
    Private Function ColorText(B As Brush) As String
        Dim S As SolidColorBrush = TryCast(B, SolidColorBrush)
        If S Is Nothing Then Return "非纯色"
        Return $"#{S.Color.R:X2}{S.Color.G:X2}{S.Color.B:X2}"
    End Function

    Private Sub EnsureLyricBrushes()
        If LyricBrushRow1 Is Nothing Then LyricBrushRow1 = New SolidColorBrush(Colors.Gray)
        If LyricBrushRow2 Is Nothing Then LyricBrushRow2 = New SolidColorBrush(Colors.Gray)
        If LyricBrushBar Is Nothing Then LyricBrushBar = New SolidColorBrush(Colors.Gray)
    End Sub

    Private Function ColorOf(B As Brush, Fallback As Color) As Color
        Dim S As SolidColorBrush = TryCast(B, SolidColorBrush)
        If S Is Nothing Then Return Fallback
        Return S.Color
    End Function

    ''' <summary>两色按进度插值（颜色也能平滑过渡）。</summary>
    Private Function ClampByte(V As Double) As Byte
        If V <= 0 Then Return 0
        If V >= 255 Then Return 255
        Return CByte(Math.Round(V))
    End Function

    ''' <summary>
    ''' 两色按进度插值。注意：VB 里 Byte - Byte 的运算结果仍是 Byte，
    ''' 通道值下降时（例如 0x9B → 0x8A）会直接抛 OverflowException，
    ''' 所以必须先转成 Double 再算，否则整段颜色代码每帧都会失败。
    ''' </summary>
    Private Function LerpColor(A As Color, B As Color, P As Double) As Color
        Dim T As Double = Math.Max(0.0, Math.Min(1.0, P))
        Dim CA As Color = A, CB As Color = B
        Dim Alpha As Double = CDbl(CA.A) + (CDbl(CB.A) - CDbl(CA.A)) * T
        Dim Red As Double = CDbl(CA.R) + (CDbl(CB.R) - CDbl(CA.R)) * T
        Dim Green As Double = CDbl(CA.G) + (CDbl(CB.G) - CDbl(CA.G)) * T
        Dim Blue As Double = CDbl(CA.B) + (CDbl(CB.B) - CDbl(CA.B)) * T
        Return Color.FromArgb(ClampByte(Alpha), ClampByte(Red), ClampByte(Green), ClampByte(Blue))
    End Function

    Private Function EaseInOut(P As Double) As Double
        Return (1 - Math.Cos(Math.PI * P)) / 2
    End Function

    ''' <summary>
    ''' 滚动专用曲线：起步略快、尾段长距离缓慢衰减（幂次越高，收尾越"软"）。
    ''' 直接线性或余弦曲线在结束瞬间速度归零太快，看起来就像"滑到位又被钉住"。
    ''' </summary>
    Private Function EaseRoll(P As Double) As Double
        Dim X As Double = Math.Max(0.0, Math.Min(1.0, P))
        Return 1 - Math.Pow(1 - X, 2.4)
    End Function

    ''' <summary>直接落位：四行填满、样式归位、位移归零。</summary>
    Private Sub RenderLyric(Index As Integer)
        Try
            If LyricStages Is Nothing OrElse LyricStages.Length = 0 Then Return
            Dim Labs As TextBlock() = LyricLabs()
            Dim Bars As Shapes.Rectangle() = LyricBars()
            If Labs Is Nothing OrElse Bars Is Nothing OrElse PanLyricInner Is Nothing Then Return
            Index = Math.Max(0, Math.Min(Index, LyricStages.Length - 1))
            LyricIndex = Index
            LyricPhase = "idle"
            PanLyricInner.Margin = New Thickness(0)
            PanLyricInner.Opacity = 1
            For k As Integer = 0 To 3
                Dim StageIdx As Integer = Index - 1 + k
                Dim Text As String = If(StageIdx >= 0 AndAlso StageIdx < LyricStages.Length, LyricStages(StageIdx), "")
                Labs(k).Text = Text
                Labs(k).FontWeight = FontWeights.SemiBold
                Select Case k
                    Case 0
                        Labs(k).FontSize = 13
                        Labs(k).Opacity = 0.42
                    Case 1
                        Labs(k).FontSize = 16.5
                        Labs(k).Opacity = 1
                    Case 2
                        Labs(k).FontSize = 13
                        Labs(k).Opacity = 0.42
                    Case Else
                        Labs(k).FontSize = 13
                        Labs(k).Opacity = 0.16
                End Select
                '上一行已完成：带满划线；其余收起
                SetStrike(Bars(k), Labs(k), k = 0 AndAlso Text <> "")
            Next
            If BarLyric1 IsNot Nothing Then
                BarLyric1.Width = 0
                BarLyric1.Opacity = 0
            End If
            BarFullAt165 = 0
            SetAccentColor(False)
        Catch ex As Exception
            Logger.Warn(ex, "渲染联机阶段失败")
        End Try
    End Sub

    Private Sub SetStrike(Bar As Shapes.Rectangle, Line As TextBlock, Visible As Boolean)
        Try
            If Bar Is Nothing OrElse Line Is Nothing Then Return
            If Not Visible Then
                Bar.Width = 0
                Bar.Opacity = 0
                Return
            End If
            Bar.Fill = FadedBrush()
            Bar.Width = MeasureText(Line) + 2
            Bar.Opacity = 0.8
        Catch ex As Exception
        End Try
    End Sub

    Private Function MeasureText(Line As TextBlock) As Double
        Try
            If Line Is Nothing Then Return 120
            Line.Measure(New Size(Double.PositiveInfinity, Double.PositiveInfinity))
            Dim Width As Double = Line.DesiredSize.Width
            If Width <= 1 Then Width = 120
            Return Width
        Catch ex As Exception
            Return 120
        End Try
    End Function

    ''' <summary>按指定字号量宽度（不改变控件当前状态）。</summary>
    Private Function MeasureTextAt(Line As TextBlock, Size As Double) As Double
        Try
            If Line Is Nothing Then Return 120
            Dim Old As Double = Line.FontSize
            Line.FontSize = Size
            Line.Measure(New Size(Double.PositiveInfinity, Double.PositiveInfinity))
            Dim Width As Double = Line.DesiredSize.Width
            Line.FontSize = Old
            If Width <= 1 Then Width = 120
            Return Width
        Catch ex As Exception
            Return 120
        End Try
    End Function

    Private Function AccentBrush(IsFailed As Boolean) As Brush
        Dim Result As Brush = Nothing
        Try
            Result = TryCast(Application.Current.FindResource(If(IsFailed, "ColorBrushRedDark", "ColorBrush3")), Brush)
        Catch ex As Exception
        End Try
        If Result Is Nothing Then Result = Brushes.Green
        Return Result
    End Function

    Private Function FadedBrush() As Brush
        Dim Result As Brush = TryCast(Application.Current.FindResource("ColorBrushGray3"), Brush)
        If Result Is Nothing Then Result = Brushes.Gray
        Return Result
    End Function

    ''' <summary>当前行的强调色 / 失败红。</summary>
    Private Sub SetAccentColor(IsFailed As Boolean)
        Try
            If LabLyric1 IsNot Nothing Then LabLyric1.Foreground = New SolidColorBrush(ColorOf(AccentBrush(IsFailed), Colors.Green))
            If BarLyric1 IsNot Nothing Then BarLyric1.Fill = AccentBrush(IsFailed)
            If LabLyric0 IsNot Nothing Then LabLyric0.Foreground = New SolidColorBrush(ColorOf(FadedBrush(), Colors.Gray))
            If LabLyric2 IsNot Nothing Then LabLyric2.Foreground = FadedBrush()
            If LabLyric3 IsNot Nothing Then LabLyric3.Foreground = FadedBrush()
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
            SetFinishTip(Nothing)
            Copy() '立即复制邀请码
            '房主自己看不到"别人能不能加进来"：连不上中转节点时房间建得出来但谁进不来，
            '必须当面提示房主，否则会一直以为是加入方的问题。
            RunInNewThread(
                Sub()
                    Thread.Sleep(9000)
                    Dim Net As String = ModTerracotta.DescribeNetwork()
                    If Not Net.Contains("一个中转节点都没连上") Then Return
                    RunInUi(
                        Sub()
                            Try
                                If LinkState <> LinkStates.Finished Then Return
                                SetFinishTip("这台电脑连不上中转节点，别人加不进来：换个节点或重启网络后再建房", Warn:=True)
                            Catch ex As Exception
                            End Try
                        End Sub)
                End Sub, "Link Host NetCheck")
        Else
            LabFinishTitle.Text = "已连接房主"
            PanFinishCode.Visibility = Visibility.Collapsed
            PanFinishHall.Visibility = Visibility.Visible
            LabFinishIp.Text = If(CurrentState Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentState.Url), "—", CurrentState.Url)
            BtnFinishExit.Text = "离开房间"
            SetFinishTip("进游戏 → 多人游戏 → 最下方双击「陶瓦联机大厅」")
        End If
        StopLyricTimer()
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
        '复制房间码，并带上房主自己用的节点：两边节点不一致时联机模块永远找不到对方
        Dim RawNode As String = Settings.Get(Of String)("LinkCustomPeer")
        Dim CopyText As String = RoomCode
        If Not String.IsNullOrWhiteSpace(RawNode) Then CopyText &= vbCrLf & "节点：" & RawNode.Trim
        ClipboardSet(CopyText, False)
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
    ''' <summary>这次加入要用的节点（从房主发来的消息里读出来的），Nothing 表示按设置走。</summary>
    Private JoinNodesOverride As String() = Nothing
    Private JoinNodesFromInvite As Boolean = False
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
        Dim GuestRetry As Integer = 0 '模块 15 秒就放弃，这里自己再试两次，免得为了几秒时差让玩家重走流程
        Dim HostNetWait As Double = 0 '房主拿到房间码后，等联机组件真正挂上中转节点的秒数
        Dim HostNetTry As Integer = 0 '房主接入中转失败后重建房间的次数
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
                    '房主拿到房间码不代表别人能进来：联机组件没挂上中转服务器时，房间建得出来但谁都找不到。
                    '所以先等它真的接上，接不上就重建房间，重建之后才有新的房间码。
                    If IsServerSide Then
                        If HostNetWait < 12 Then
                            HostNetWait += 0.3
                            UpdateLoadingPage("正在接入中转节点……", "接入中转节点")
                            Continue Do
                        End If
                        If HostNetTry < 3 AndAlso ModTerracotta.DescribeNetwork().Contains("一个中转节点都没连上") Then
                            HostNetTry += 1
                            Logger.Info($"房主没能接入中转节点，第 {HostNetTry} 次重建房间")
                            Try
                                ModTerracotta.ApiGet("/state/ide", 4000)
                            Catch ex As Exception
                            End Try
                            Thread.Sleep(1200)
                            Try
                                ModTerracotta.CreateRoom(PlayerName, PublicNodes)
                            Catch ex As Exception
                                Logger.Warn(ex, "重建房间失败")
                            End Try
                            HostNetWait = 0
                            Continue Do
                        End If
                    End If
                    '先把最后一格演完再切界面，否则邀请码会抢在动画前面弹出来
                    UpdateLoadingPage(If(IsServerSide, "房间已创建", "已进入房间"),
                                      If(IsServerSide, "房间创建完成", "进入房间完成"))
                    WaitLyricIdle(3200)
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
                    '能拿到邀请码就说明房主早已开放局域网，这一步与"房主开局域网"无关，
                    '真正在等的是与房主之间建立连接通道（直连打洞成功，或通过中转节点成功）。
                    Dim GuestTitle As String = If(GuestRetry = 0, "正在与房主建立连接", $"正在与房主建立连接（第 {GuestRetry + 1} 次）")
                    Dim GuestHint As String = Nothing
                    If GuestTime < 15 Then
                        GuestHint = "正在尝试直连，通常几秒内完成"
                    ElseIf GuestTime < 45 Then
                        GuestTitle = "直连没通，正在尝试中转节点"
                        GuestHint = "跨网络联机需要中转，这一步可能要多等一会儿"
                    Else
                        GuestTitle = "和房主的连接一直没建立"
                        GuestHint = "多为网络无法互通：让双方确认节点可用，或在设置里把节点填成 - 只走直连再试"
                    End If
                    '卡住超过 12 秒就把联机模块自己的最后一行日志亮出来，便于直接看出卡在哪
                    If GuestTime >= 12 Then
                        Dim LogTail As String = ModTerracotta.GetModuleLogTail(2)
                        If Not String.IsNullOrWhiteSpace(LogTail) Then
                            LogTail = LogTail.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
                            If LogTail.Length > 90 Then LogTail = LogTail.Substring(LogTail.Length - 90)
                            GuestHint = "模块日志：" & LogTail
                        End If
                    End If
                    UpdateLoadingPage(GuestTitle, "建立连接" & (If(GuestTime < 20, "1", If(GuestTime < 60, "2", "3"))), GuestHint)
                    '0.5 → 0.85 缓慢推进（90 秒），既不假装完成，也不让进度条一动不动
                    Task.Progress = Math.Min(0.5 + GuestTime / 90 * 0.35, 0.85)
                    '进度在动，停滞判定就永远不会触发，所以这里自己掐 90 秒
                    If GuestTime > 90 Then
                        Panic("与房主的连接没能建立", $"邀请码有效说明房主已开放局域网，卡住的是你与房主之间的连接通道。{vbCrLf}常见原因：{vbCrLf}- 双方网络无法互通，且中转节点不可用{vbCrLf}- 两边联机模块版本不一致{vbCrLf}{vbCrLf}可以这样试：{vbCrLf}- 双方在 设置 → 工具 → 联机 → 额外节点 填同一个可用节点{vbCrLf}- 或两边都填 - 只走直连（同一局域网/同一路由器下最稳）{vbCrLf}- 双方都点「检查模块更新」保证版本一致{vbCrLf}{vbCrLf}联机模块日志：{vbCrLf}{ModTerracotta.GetModuleLogTail(15)}{vbCrLf}{vbCrLf}{GetRecentLog()}")
                        Throw New OperationCanceledException
                    End If
                Case "exception"
                    '模块自己 15 秒就判定"找不到房主"，而房主可能只是晚几秒才出现在中转网络上，
                    '所以加入方自动重试两次，不用玩家自己重走一遍流程。
                    If Not IsServerSide AndAlso Current.ExceptionType = 0 AndAlso GuestRetry < 5 Then
                        GuestRetry += 1
                        Logger.Info($"没找到房主，自动重试第 {GuestRetry} 次")
                        Try
                            ModTerracotta.ApiGet("/state/ide", 4000)
                        Catch ex As Exception
                        End Try
                        Thread.Sleep(800)
                        Try
                            ModTerracotta.JoinRoom(RoomCode, PlayerName, PublicNodes)
                        Catch ex As Exception
                        End Try
                        GuestTime = 0
                        Continue Do
                    End If
                    Dim ExceptionLog As String = ModTerracotta.GetModuleLogTail(15)
                    Panic(ModTerracotta.GetExceptionBrief(Current.ExceptionType),
                          ModTerracotta.GetExceptionDetail(Current.ExceptionType) &
                          vbCrLf & vbCrLf & "网络情况：" & ModTerracotta.DescribeNetwork() &
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
                            Panic("与房主的连接没能建立", $"邀请码有效说明房主已开放局域网，卡住的是你与房主之间的连接通道。{vbCrLf}常见原因：{vbCrLf}- 双方网络无法互通，且中转节点不可用{vbCrLf}- 两边联机模块版本不一致{vbCrLf}{vbCrLf}可以这样试：{vbCrLf}- 双方在 设置 → 工具 → 联机 → 额外节点 填同一个可用节点{vbCrLf}- 或两边都填 - 只走直连（同一局域网/同一路由器下最稳）{vbCrLf}- 双方都点「检查模块更新」保证版本一致{vbCrLf}{vbCrLf}联机模块日志：{vbCrLf}{ModTerracotta.GetModuleLogTail(15)}{vbCrLf}{vbCrLf}{GetRecentLog()}")
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
        '房主发来的消息里带了自己用的节点时，就用房主那个：两边节点不一致是连不上最常见的原因
        If JoinNodesFromInvite Then Return JoinNodesOverride
        Dim Raw As String = Settings.Get(Of String)("LinkCustomPeer")
        If String.IsNullOrWhiteSpace(Raw) Then Return ModTerracotta.GetNodes() '没填就用官方节点列表，取不到才退回内置节点
        '填 "-" 或 "直连" 表示不用节点、只走直连（同一局域网或 NAT 友好时更快）
        If Raw.Trim() = "-" OrElse Raw.Trim() = "直连" Then Return Nothing
        Dim Custom As String() = Raw.Split({","c, "，"c, ";"c, "；"c, vbCr, vbLf, " "c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(Node) Node.Trim).Where(Function(Node) Node <> "").ToArray
        If Custom.Length = 0 Then Return ModTerracotta.GetNodes()
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
        '完成页底部只留一行说明，不再用提示条
        If State.PeopleCount <= 1 Then
            SetFinishTip(If(IsServerSide, "等待朋友加入房间", "正在等待房主开始游戏"))
            Return
        End If
        If Not IsServerSide AndAlso State.Difficulty = "TOUGH" Then
            SetFinishTip("和房主的网络较差，可能走了中继，延迟会偏高", Warn:=True)
            Return
        End If
        SetFinishTip($"房间内已有 {State.PeopleCount} 名玩家：{GetMemberSummary(State)}")
    End Sub

    ''' <summary>完成页底部那一行说明（不是提示条）。</summary>
    Private Sub SetFinishTip(Text As String, Optional Warn As Boolean = False)
        Try
            If LabFinishTip Is Nothing Then Return
            LabFinishTip.Text = If(Text, "")
            Dim TipBrush As Brush = Nothing
            Try
                TipBrush = TryCast(Application.Current.FindResource(If(Warn, "ColorBrushRedDark", "ColorBrushGray4")), Brush)
            Catch ex As Exception
            End Try
            If TipBrush IsNot Nothing Then LabFinishTip.Foreground = TipBrush
        Catch ex As Exception
        End Try
    End Sub

#End Region

End Class
