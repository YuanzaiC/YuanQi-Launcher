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

End Class
