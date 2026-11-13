Public Class PageSetupGeneral

    Private Sub PageSetupGeneral_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

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
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化通用页设置")
            Hint("已初始化通用页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化通用页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

#Region "设置与体检"

    '检查更新
    Private Sub BtnSystemCheckUpdate_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemCheckUpdate.Click
        CheckLauncherUpdate()
    End Sub
    '空间体检
    Private Sub BtnSystemSpaceAudit_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSpaceAudit.Click
        ShowSpaceAudit()
    End Sub
    '环境体检
    Private Sub BtnSystemEnvCheck_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemEnvCheck.Click
        ShowEnvCheck()
    End Sub

    '重新查看使用引导
    Private Sub BtnSystemGuide_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemGuide.Click
        MyMsgBoxGuide()
        MyMsgGuide.MarkFinished()
    End Sub

    '导出设置
    Private Sub BtnSystemSettingExp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingExp.Click
        Try
            Dim FilePath = Dialogs.SaveFile("保存缘启设置", $"缘启设置 {Now:yyyy-MM-dd}.json", SetupConfigDefaultFolder(),
                                            {("json", "缘启设置文件")})
            If FilePath Is Nothing Then Return '用户取消
            Settings.Set("CacheSetupConfig", FilePath) '下次直接从这次选的位置打开
            Dim Count = ExportConfig(FilePath)
            Hint($"已导出 {Count} 项设置！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "导出设置失败")
            MyMsgBox("导出设置失败：" & vbCrLf & ex.Message, "导出设置", "确定", IsWarn:=True)
        End Try
    End Sub

    '导入设置
    Private Sub BtnSystemSettingImp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingImp.Click
        Try
            If MyMsgBox($"导入设置会覆盖当前的启动器设置，且无法撤销。{vbCrLf}建议先使用「导出设置」备份当前的设置。{vbCrLf}{vbCrLf}确定要继续吗？", "导入设置", "继续", "取消") = 2 Then Return
            Dim FileList = Dialogs.SelectFile("选择缘启设置文件", False, SetupConfigDefaultFolder(),
                                              {({"json"}, "缘启设置文件")})
            If FileList Is Nothing OrElse Not FileList.Any() Then Return '用户取消
            Settings.Set("CacheSetupConfig", FileList.First()) '下次直接从这次选的位置打开
            Dim Count = ImportConfig(FileList.First())
            Reload() '刷新当前页面上显示的设置
            MyMsgBox($"导入完成，共应用了 {Count} 项设置。{vbCrLf}部分设置（如主题颜色、界面尺寸）需要重启启动器后才能完全生效。", "导入设置")
        Catch ex As Exception
            Logger.Error(ex, "导入设置失败")
            MyMsgBox("导入设置失败：" & vbCrLf & ex.Message, "导入设置", "确定", IsWarn:=True)
        End Try
    End Sub

    ''' <summary>
    ''' 设置导出/导入对话框的默认位置：优先上次选过的地方（记的是完整文件路径，对话框会落到它所在文件夹），
    ''' 没有记录时用桌面，最后兜底为启动器目录。
    ''' </summary>
    Private Shared Function SetupConfigDefaultFolder() As String
        Try
            Dim Cached As String = Settings.Get(Of String)("CacheSetupConfig")
            If Not String.IsNullOrWhiteSpace(Cached) Then Return Cached
        Catch ex As Exception
        End Try
        Try
            Dim DesktopPath As String = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            If Not String.IsNullOrWhiteSpace(DesktopPath) Then Return DesktopPath & "\"
        Catch ex As Exception
        End Try
        Return Paths.BaseThenName
    End Function
#End Region

End Class
