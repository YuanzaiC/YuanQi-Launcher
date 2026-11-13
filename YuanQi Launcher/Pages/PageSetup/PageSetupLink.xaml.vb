Public Class PageSetupLink

    Private Sub PageSetupLink_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()
        RefreshSeedCacheUsage()

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
    End Sub

    '初始化
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化联机页设置")
            Hint("已初始化联机页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化联机页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '检查联机模块更新
    Private Sub BtnLinkCheckUpdate_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLinkCheckUpdate.Click
        CheckTerracottaUpdate()
    End Sub

    '---- 种子地图磁盘缓存 ----

    Private Sub RefreshSeedCacheUsage()
        Try
            If LabSeedCacheUsage IsNot Nothing Then LabSeedCacheUsage.Text = SeedMapCache.DescribeUsage()
        Catch ex As Exception
            Logger.Warn(ex, "统计种子地图缓存占用失败")
        End Try
    End Sub

    Private Sub BtnSeedCacheClear_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSeedCacheClear.Click
        If MyMsgBox("清空全部存档的地图缓存？" & vbCrLf & "只删除地图瓦片缓存，不影响存档和游戏本身。", "清空地图缓存", "清空", "取消", IsWarn:=True) <> 1 Then Return
        SeedMapCache.Clear()
        RefreshSeedCacheUsage()
        Hint("已清空地图缓存", HintType.Green)
    End Sub

End Class