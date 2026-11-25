Public Class PclLogger
    Inherits FileLogger

    ''' <summary>
    ''' 是否已经有错误弹窗在排队或显示中。
    ''' 一个异常往往会被多处 Catch 记好几条日志，不加限制就会连环弹窗；
    ''' 而每个弹窗都会让调用线程进一次嵌套消息循环，叠起来界面就彻底点不动了。
    ''' </summary>
    Private Shared ErrorDialogBusy As Integer = 0

    ''' <inheritdoc/>
    Public Overrides Function Format(Text As String, Level As LogLevel, FilePath As String, Ex As Exception) As String
        Text = MyBase.Format(Text, Level, FilePath, Ex)
        Text = FilterUserName(FilterAccessToken(Text, "*"c), "*"c)
        Return Text
    End Function

    ''' <inheritdoc/>
    Public Overrides Sub HandleBehavior(RawMessage As String, FormattedMessage As String, Behavior As LogBehavior, Ex As Exception)
        If IsProgramEnding Then Return
        MyBase.HandleBehavior(RawMessage, FormattedMessage, Behavior, Ex)
        Dim BriefText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(False))
        BriefText = FilterUserName(FilterAccessToken(BriefText, "*"c), "*"c)
        Dim DetailText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(True))
        DetailText = FilterUserName(FilterAccessToken(DetailText, "*"c), "*"c)
        Select Case Behavior
            Case LogBehavior.None
                '啥也不干
            Case LogBehavior.ToastIfDebug
                If BuildType = BuildTypes.Debug OrElse ModeDebug Then Hint("[调试模式] " & BriefText, HintType.Blue, False)
            Case LogBehavior.Toast
                Hint(BriefText, HintType.Red, False)
            Case LogBehavior.Alert
                ShowErrorDialog(DetailText, False)
            Case LogBehavior.AlertThenFeedback
                If PageSetupSystem.IsLauncherNewest Then
                    ShowErrorDialog(DetailText & vbCrLf & vbCrLf & "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！", True)
                Else
                    ShowErrorDialog(DetailText & vbCrLf & vbCrLf & "将缘启更新至最新版或许可以解决这个问题……", False)
                End If
            Case LogBehavior.AlertThenCrash
                '兜底的严重错误：必须让人看见，所以放到 UI 线程上弹系统框
                '（系统框有主窗口归属，不会跑到窗口后面去，也不会出现"看不见又点不动"的情况）
                Static FirstTrigger As Boolean = True
                If FirstTrigger Then
                    FirstTrigger = False
                    Try
                        RunInUiWait(
                        Sub()
                            Try
                                If PageSetupSystem.IsLauncherNewest Then
                                    If MsgBox(DetailText & vbCrLf & vbCrLf & "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！", MsgBoxStyle.Critical + MsgBoxStyle.YesNo, "错误") = MsgBoxResult.Yes Then
                                        OpenExplorer(Paths.Base & "YQL\Log1.txt")
                                        OpenWebsite("https://github.com/Meloong-Git/PCL/issues/")
                                    End If
                                Else
                                    MsgBox(DetailText & vbCrLf & vbCrLf & "将缘启更新至最新版或许可以解决这个问题……", MsgBoxStyle.Critical, "错误")
                                End If
                            Catch ex2 As Exception
                                Logger.SendToDebug(ex2, "显示崩溃提示失败")
                            End Try
                        End Sub)
                    Catch ex2 As Exception
                        Logger.SendToDebug(ex2, "切换到 UI 线程显示崩溃提示失败")
                    End Try
                Else
                    '多次触发，说明有线程在循环抛异常：不再来回弹窗，直接退出
                    Thread.Sleep(2000)
                End If
                FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Select
        '遥测
        If Behavior >= LogBehavior.Toast Then Telemetry("错误日志", "Exception", DetailText)
    End Sub

    ''' <summary>
    ''' 显示错误弹窗。统一放到后台线程执行，并且同一时间只允许存在一个。
    ''' 直接在 UI 线程上弹（比如 50ms 主时钟里抛的异常）会让调用方进入嵌套消息循环，
    ''' 这时再有任何异常就会一层层套下去，表现就是「报错之后整个启动器连鼠标都点不动」。
    ''' </summary>
    Private Shared Sub ShowErrorDialog(Text As String, AllowFeedback As Boolean)
        If Interlocked.CompareExchange(ErrorDialogBusy, 1, 0) <> 0 Then
            '已经有一个错误框了：这次只留一条提示，不再叠弹窗
            Try
                Hint("又出现了一个错误，详情见 YQL\Log1.txt", HintType.Red, False)
            Catch ex As Exception
            End Try
            Return
        End If
        RunInNewThread(
        Sub()
            Try
                '等主窗体准备好（最多 10 秒）：窗体还没起来时弹出来的系统框没有归属窗口，
                '会跑到后面去、点也点不到，看起来就是"启动器卡死了"
                Dim Waited As Integer = 0
                Do While (FrmMain Is Nothing OrElse FrmMain.PanMsg Is Nothing) AndAlso Waited < 10000
                    Thread.Sleep(100)
                    Waited += 100
                Loop
                If FrmMain Is Nothing OrElse FrmMain.PanMsg Is Nothing Then
                    Try
                        Hint("启动过程中出现错误，详情见 YQL\Log1.txt", HintType.Red, False)
                    Catch ex As Exception
                    End Try
                    Return
                End If
                If AllowFeedback Then
                    If MyMsgBox(Text, "错误", "反馈", "取消", IsWarn:=True, ForceWait:=True) = 1 Then
                        OpenExplorer(Paths.Base & "YQL\Log1.txt")
                        OpenWebsite("https://github.com/Meloong-Git/PCL/issues/")
                    End If
                Else
                    MyMsgBox(Text, "错误", IsWarn:=True, ForceWait:=True)
                End If
            Catch ex As Exception
                Logger.SendToDebug(ex, "显示错误弹窗失败")
            Finally
                Interlocked.Exchange(ErrorDialogBusy, 0)
            End Try
        End Sub, "Error Dialog", ThreadPriority.AboveNormal)
    End Sub

End Class
