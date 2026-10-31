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
            Hint("反馈入口正在准备中，敬请期待～", HintType.Blue)
        End If
    End Sub

End Class