Public Class PageSetupLink

    Private Sub PageSetupLink_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '联机模块更新检查：按钮跟着「检查中」状态走，不弹提示气泡
        RemoveHandler ModTools.TerracottaCheckStarted, AddressOf OnTerracottaCheckStarted
        RemoveHandler ModTools.TerracottaCheckFinished, AddressOf OnTerracottaCheckFinished
        AddHandler ModTools.TerracottaCheckStarted, AddressOf OnTerracottaCheckStarted
        AddHandler ModTools.TerracottaCheckFinished, AddressOf OnTerracottaCheckFinished
        ApplyTerracottaCheckState()

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
    Private Sub PageSetupLink_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        RemoveHandler ModTools.TerracottaCheckStarted, AddressOf OnTerracottaCheckStarted
        RemoveHandler ModTools.TerracottaCheckFinished, AddressOf OnTerracottaCheckFinished
    End Sub
    Private Sub OnTerracottaCheckStarted()
        RunInUi(Sub() ApplyTerracottaCheckState())
    End Sub
    Private Sub OnTerracottaCheckFinished()
        RunInUi(Sub() ApplyTerracottaCheckState())
    End Sub
    ''' <summary>检查模块更新按钮跟着检查状态走：检查中禁用并显示「检查中」。</summary>
    Private Sub ApplyTerracottaCheckState()
        Try
            Dim Busy As Boolean = ModTools.IsCheckingTerracotta
            BtnLinkCheckUpdate.IsEnabled = Not Busy
            BtnLinkCheckUpdate.Text = If(Busy, "检查中……", "检查模块更新")
        Catch ex As Exception
            Logger.Warn(ex, "刷新检查模块更新按钮状态失败")
        End Try
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
        If MyMsgBox("清空全部存档的地图缓存？" & vbCrLf & "仅删除地图瓦片缓存，不影响存档与游戏本身。", "清空地图缓存", "清空", "取消", IsWarn:=True) <> 1 Then Return
        SeedMapCache.Clear()
        RefreshSeedCacheUsage()
        Hint("已清空地图缓存", HintType.Green)
    End Sub

End Class