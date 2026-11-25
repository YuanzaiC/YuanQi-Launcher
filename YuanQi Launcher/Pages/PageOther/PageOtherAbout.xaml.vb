Public Class PageOtherAbout

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherAbout_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '更新检查：按钮跟着「检查中」状态走，不弹提示气泡
        RemoveHandler ModTools.UpdateCheckStarted, AddressOf OnUpdateCheckStarted
        RemoveHandler ModTools.UpdateCheckFinished, AddressOf OnUpdateCheckFinished
        AddHandler ModTools.UpdateCheckStarted, AddressOf OnUpdateCheckStarted
        AddHandler ModTools.UpdateCheckFinished, AddressOf OnUpdateCheckFinished
        ApplyUpdateCheckState()

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        ItemAboutPcl.Info = ItemAboutPcl.Info.Replace("%VERSION%", VersionDisplay).Replace("%VERSIONCODE%", VersionCode).Replace("%BRANCH%", CInt(BuildType))

        '右键用系统浏览器打开：这几个按钮的左键走各自的 Click（内置浏览器 / 弹窗），
        '右键地址单独标出来，否则主窗口的右键逻辑找不到它们是网页入口。
        CustomEventService.SetWebUrl(BtnAboutWebsite, ModTools.WebsiteUrl)
        If ModTools.IsRepoReady() Then
            CustomEventService.SetWebUrl(BtnAboutGithub, ModTools.RepoUrl)
            CustomEventService.SetWebUrl(BtnAboutFeedback, ModTools.RepoUrl & "/issues")
        End If

    End Sub

    '提交反馈
    Private Sub BtnAboutFeedback_Click() Handles BtnAboutFeedback.Click
        If ModTools.IsRepoReady() Then
            OpenWebsite(ModTools.RepoUrl & "/issues")
        Else
            Hint("反馈入口尚未配置。", HintType.Blue)
        End If
    End Sub

    '前往官网
    Private Sub BtnAboutWebsite_Click() Handles BtnAboutWebsite.Click
        OpenWebsite(ModTools.WebsiteUrl)
    End Sub

    '打开启动器源代码仓库
    Private Sub BtnAboutGithub_Click() Handles BtnAboutGithub.Click
        If Not ModTools.IsRepoReady() Then
            Hint("仓库地址尚未配置。", HintType.Blue)
            Return
        End If
        OpenWebsite(ModTools.RepoUrl)
    End Sub

    '检查更新
    Private Sub BtnAboutCheckUpdate_Click() Handles BtnAboutCheckUpdate.Click
        ModTools.CheckLauncherUpdate()
    End Sub

    Private Sub PageOtherAbout_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        RemoveHandler ModTools.UpdateCheckStarted, AddressOf OnUpdateCheckStarted
        RemoveHandler ModTools.UpdateCheckFinished, AddressOf OnUpdateCheckFinished
    End Sub
    Private Sub OnUpdateCheckStarted()
        RunInUi(Sub() ApplyUpdateCheckState())
    End Sub
    Private Sub OnUpdateCheckFinished()
        RunInUi(Sub() ApplyUpdateCheckState())
    End Sub
    ''' <summary>检查更新按钮跟着检查状态走：检查中禁用并显示「检查中」。</summary>
    Private Sub ApplyUpdateCheckState()
        Try
            Dim Busy As Boolean = ModTools.IsCheckingUpdate
            BtnAboutCheckUpdate.IsEnabled = Not Busy
            BtnAboutCheckUpdate.Text = If(Busy, "检查中……", "检查更新")
        Catch ex As Exception
            Logger.Warn(ex, "刷新检查更新按钮状态失败")
        End Try
    End Sub

End Class