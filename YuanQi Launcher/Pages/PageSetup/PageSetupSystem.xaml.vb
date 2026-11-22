Public Class PageSetupSystem


    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        MigrateDownloadSources()
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub

    ''' <summary>
    ''' 把旧的单一「Mod 文件」下载源（ToolDownloadMod）拆进 CurseForge / Modrinth 两项，
    ''' 「资源文件」默认跟随「游戏文件」。新设置默认 -1 表示还没迁移，只迁一次。
    ''' </summary>
    Private Shared Sub MigrateDownloadSources()
        Try
            Dim ModValue As Integer = Settings.Get(Of Integer)("ToolDownloadMod")
            If ModValue < 0 Then ModValue = 2
            If Settings.Get(Of Integer)("ToolDownloadCurseForgeSource") < 0 Then Settings.Set("ToolDownloadCurseForgeSource", ModValue)
            If Settings.Get(Of Integer)("ToolDownloadModrinthSource") < 0 Then Settings.Set("ToolDownloadModrinthSource", ModValue)
            Dim GameValue As Integer = Settings.Get(Of Integer)("ToolDownloadSource")
            If GameValue < 0 Then GameValue = 1
            If Settings.Get(Of Integer)("ToolDownloadAssetSource") < 0 Then Settings.Set("ToolDownloadAssetSource", GameValue)
            Logger.Info($"下载源已细分：CurseForge={Settings.Get(Of Integer)("ToolDownloadCurseForgeSource")}，" &
                        $"Modrinth={Settings.Get(Of Integer)("ToolDownloadModrinthSource")}，" &
                        $"资源文件={Settings.Get(Of Integer)("ToolDownloadAssetSource")}")
        Catch ex As Exception
            Logger.Warn(ex, "迁移细分下载源设置失败")
        End Try
    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化下载页设置")
            Hint("已初始化下载页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化下载页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox("下载线程数过多时会出现明显卡顿。" & vbCrLf &
                     "通常 64 线程已满足多数下载需求，更高的线程数并无必要。", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

    ''' <summary>
    ''' 启动器是否已经是最新版？
    ''' 若返回 Nothing，则代表无更新缓存文件或出错。
    ''' </summary>
    Public Shared Function IsLauncherNewest() As Boolean?
        '缘启不包含自动更新功能，始终视为最新版
        Return True
    End Function

End Class
