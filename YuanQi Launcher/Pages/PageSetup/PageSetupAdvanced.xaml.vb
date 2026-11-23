' =============================================================================
'  PageSetupAdvanced —— 设置 · 高级页
'  内容：调试相关的四个开关（调试模式、下载不复用文件、随机延迟、动画边界）。
'  说明：该页设置会明显影响性能，只用于排查问题；滑条文案在页面加载时挂上。
' =============================================================================
Public Class PageSetupAdvanced

    Private Sub PageSetupAdvanced_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, "关闭", (v / 10 + 0.1) & "x")
        AniControlEnabled -= 1

        '微软登录教程是个网页入口：右键要能按「网页打开手势」的相反方式打开
        If Not String.IsNullOrWhiteSpace(MsClientIdGuideUrl) Then CustomEventService.SetWebUrl(BtnMsGuideSetup, MsClientIdGuideUrl)

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化高级页设置")
            Hint("已初始化高级页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化高级页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint("部分调试信息将在刷新或启动器重启后切换显示！",, False)
    End Sub

    '获取微软登录用的客户端 ID
    '按「网页打开手势」打开：左键默认走启动器内置浏览器，右键用系统浏览器（可在设置里对调）
    Private Sub BtnMsGuideSetup_Click() Handles BtnMsGuideSetup.Click
        Try
            If String.IsNullOrWhiteSpace(MsClientIdGuideUrl) Then
                Hint("教程链接还没配置：改一下 ModSecret.MsClientIdGuideUrl 就行", HintType.Blue)
                Return
            End If
            OpenWebsite(MsClientIdGuideUrl)
        Catch ex As Exception
            Logger.Error(ex, "打开微软登录教程失败")
        End Try
    End Sub

End Class
