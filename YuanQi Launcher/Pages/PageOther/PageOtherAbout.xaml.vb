Public Class PageOtherAbout

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherAbout_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        ItemAboutPcl.Info = ItemAboutPcl.Info.Replace("%VERSION%", VersionDisplay).Replace("%VERSIONCODE%", VersionCode).Replace("%BRANCH%", CInt(BuildType))

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

End Class