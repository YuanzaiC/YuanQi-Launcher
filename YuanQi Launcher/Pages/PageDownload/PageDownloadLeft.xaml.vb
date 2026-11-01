Public Class PageDownloadLeft
    Implements IRefreshable

#Region "页面切换"

    ''' <summary>
    ''' 当前页面的编号。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.DownloadInstall

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As FrameworkElement, e As RouteEventArgs) Handles ItemInstall.Check, ItemMod.Check, ItemPack.Check, ItemResourcePack.Check, ItemShader.Check, ItemDataPack.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.DownloadInstall
                If FrmDownloadInstall Is Nothing Then FrmDownloadInstall = New PageDownloadInstall
                Return FrmDownloadInstall
            Case FormMain.PageSubType.DownloadMod
                If FrmDownloadMod Is Nothing Then FrmDownloadMod = New PageDownloadMod
                Return FrmDownloadMod
            Case FormMain.PageSubType.DownloadPack
                If FrmDownloadPack Is Nothing Then FrmDownloadPack = New PageDownloadPack
                Return FrmDownloadPack
            Case FormMain.PageSubType.DownloadResourcePack
                If FrmDownloadResourcePack Is Nothing Then FrmDownloadResourcePack = New PageDownloadResourcePack
                Return FrmDownloadResourcePack
            Case FormMain.PageSubType.DownloadShader
                If FrmDownloadShader Is Nothing Then FrmDownloadShader = New PageDownloadShader
                Return FrmDownloadShader
            Case FormMain.PageSubType.DownloadDataPack
                If FrmDownloadDataPack Is Nothing Then FrmDownloadDataPack = New PageDownloadDataPack
                Return FrmDownloadDataPack
            Case Else
                Throw New Exception("未知的下载子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' 切换现有页面。
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
        Catch ex As Exception
            Logger.Error(ex, $"切换分页面失败（ID {ID}）")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight") '停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
            AaCode(
            Sub()
                CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                FrmMain.PanMainRight.Child = FrmMain.PageRight
                FrmMain.PageRight.Opacity = 0
            End Sub, 130),
            AaCode(
            Sub()
                '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                FrmMain.PageRight.Opacity = 1
                FrmMain.PageRight.PageOnEnter()
            End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

#End Region

    '强制刷新
    Public Sub Refresh_Click(sender As Object, e As EventArgs) '由边栏按钮匿名调用
        Refresh(Val(sender.Tag), Force:=True)
    End Sub
    Public Sub Refresh() Implements IRefreshable.Refresh
        Refresh(FrmMain.PageCurrentSub)
    End Sub
    ''' <summary>
    ''' 设定各版本列表加载器的重复拉取间隔（只设一次）。
    ''' 原来每次进入「下载」页都用 IsForceRestart 强制重拉 8 个版本清单，网络压力很大；
    ''' 这些清单本身更新很慢（游戏版本 / 加载器版本），改为两天才刷新一次，手动点刷新可立即重拉。
    ''' </summary>
    Private Sub EnsureListReloadTimeouts()
        Const Interval As Integer = 2 * 24 * 60 * 60 * 1000 '2 天
        If DlClientListLoader.ReloadTimeout = Interval Then Return
        DlClientListLoader.ReloadTimeout = Interval
        DlOptiFineListLoader.ReloadTimeout = Interval
        DlForgeListLoader.ReloadTimeout = Interval
        DlNeoForgeListLoader.ReloadTimeout = Interval
        DlLiteLoaderListLoader.ReloadTimeout = Interval
        DlFabricListLoader.ReloadTimeout = Interval
        DlFabricApiLoader.ReloadTimeout = Interval
        DlOptiFabricLoader.ReloadTimeout = Interval
        Logger.Info("下载页版本列表：已设为 2 天内不重复拉取")
    End Sub

    ''' <param name="Force">用户手动点刷新时为 True，此时才强制重新拉取。</param>
    Public Sub Refresh(SubType As FormMain.PageSubType, Optional Force As Boolean = False)
        ResourceProject.Cache.Clear()
        ResourceVersion.ProjectFilesCache.Clear()
        Select Case SubType
            Case FormMain.PageSubType.DownloadInstall
                '进入页面：2 天内复用上次结果；手动刷新（Force）才重新拉取（并让接口缓存也让路）
                If Force Then NetCacheBypassUntil = GetTimeMs() + 120000
                EnsureListReloadTimeouts()
                DlClientListLoader.Start(IsForceRestart:=Force)
                DlOptiFineListLoader.Start(IsForceRestart:=Force)
                DlForgeListLoader.Start(IsForceRestart:=Force)
                DlNeoForgeListLoader.Start(IsForceRestart:=Force)
                DlLiteLoaderListLoader.Start(IsForceRestart:=Force)
                DlFabricListLoader.Start(IsForceRestart:=Force)
                DlFabricApiLoader.Start(IsForceRestart:=Force)
                DlOptiFabricLoader.Start(IsForceRestart:=Force)
                ItemInstall.Checked = True
            Case FormMain.PageSubType.DownloadMod
                If FrmDownloadMod IsNot Nothing Then
                    FrmDownloadMod.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadMod.Content.Page = 0
                    FrmDownloadMod.PageLoaderRestart(IsForceRestart:=Force)
                End If
                ItemMod.Checked = True
            Case FormMain.PageSubType.DownloadPack
                If FrmDownloadPack IsNot Nothing Then
                    FrmDownloadPack.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadPack.Content.Page = 0
                    FrmDownloadPack.PageLoaderRestart(IsForceRestart:=Force)
                End If
                ItemPack.Checked = True
            Case FormMain.PageSubType.DownloadResourcePack
                If FrmDownloadResourcePack IsNot Nothing Then
                    FrmDownloadResourcePack.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadResourcePack.Content.Page = 0
                    FrmDownloadResourcePack.PageLoaderRestart(IsForceRestart:=Force)
                End If
                ItemResourcePack.Checked = True
            Case FormMain.PageSubType.DownloadShader
                If FrmDownloadShader IsNot Nothing Then
                    FrmDownloadShader.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadShader.Content.Page = 0
                    FrmDownloadShader.PageLoaderRestart(IsForceRestart:=Force)
                End If
                ItemShader.Checked = True
            Case FormMain.PageSubType.DownloadDataPack
                If FrmDownloadDataPack IsNot Nothing Then
                    FrmDownloadDataPack.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadDataPack.Content.Page = 0
                    FrmDownloadDataPack.PageLoaderRestart(IsForceRestart:=Force)
                End If
                ItemDataPack.Checked = True
        End Select
        Hint("正在刷新……", Log:=False)
    End Sub

    '点击返回
    Private Sub ItemInstall_Click(sender As Object, e As MouseButtonEventArgs) Handles ItemInstall.Click
        If Not ItemInstall.Checked Then Return
        FrmDownloadInstall.ExitSelectPage()
    End Sub

End Class
