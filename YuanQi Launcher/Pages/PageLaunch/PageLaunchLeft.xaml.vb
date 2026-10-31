Imports System.Windows.Threading

Public Class PageLaunchLeft

    '加载当前版本
    Private IsLoad As Boolean = False
    Private IsLoadFinished As Boolean = False
    ''' <summary>
    ''' 页面在切换动画中途挂载（Loaded），此时同步执行整套主页刷新会卡住入场动画。
    ''' 把刷新推迟到动画结束后执行；重复触发自动合并，逻辑与原先一致。
    ''' </summary>
    Private _LoadedRefreshScheduled As Boolean = False
    Private Sub ScheduleLoadedRefresh()
        '实例信息（名称/版本徽标/图标/上次游玩）先立即刷新：从版本选择页回到主页时，
        '用户最关心的就是它变了没有，等 500ms 完整刷新会显得"内容切换慢"。
        Try
            RefreshHomeInfoCore()
        Catch ex As Exception
            Logger.Warn(ex, "立即刷新主页实例信息失败")
        End Try
        If _LoadedRefreshScheduled Then Return
        _LoadedRefreshScheduled = True
        AniStop("Launch Left Loaded Refresh")
        AniStart({AaCode(
            Sub()
                _LoadedRefreshScheduled = False
                RefreshPage(True, False)
            End Sub, 500)}, "Launch Left Loaded Refresh")
    End Sub

    ''' <summary>
    ''' 主页各模块出场：依次淡入并上移归位（Hero 仅位移，避免 3D 内容与透明度动画冲突）。
    ''' 开屏动画播放期间由 FormMain 以 IncludeHero:=False 调用：卡片要落位到 Hero 图标上，
    ''' Hero 一旦带位移，落点就会算在动画中间态上（图标会停偏）。
    ''' </summary>
    Friend Sub AnimateModulesEnter(Optional IncludeHero As Boolean = True)
        Try
            If AniControlEnabled <> 0 Then Return
            Dim Modules As New List(Of KeyValuePair(Of UIElement, Boolean)) From {
                New KeyValuePair(Of UIElement, Boolean)(PanSavesCard, True),
                New KeyValuePair(Of UIElement, Boolean)(PanRecentCard, True),
                New KeyValuePair(Of UIElement, Boolean)(PanAccountCard, True),
                New KeyValuePair(Of UIElement, Boolean)(PanCalendarCard, True),
                New KeyValuePair(Of UIElement, Boolean)(PanInsightCard, True),
                New KeyValuePair(Of UIElement, Boolean)(PanDiscoverCard, True)}
            If IncludeHero Then Modules.Insert(0, New KeyValuePair(Of UIElement, Boolean)(PanHero, False))
            Dim Anims As New List(Of AniData)
            For i As Integer = 0 To Modules.Count - 1
                Dim Elem As UIElement = Modules(i).Key
                Dim WithOpacity As Boolean = Modules(i).Value
                Dim Delay As Integer = 60 + i * 45
                If WithOpacity Then
                    Elem.Opacity = 0
                    Anims.Add(AaOpacity(Elem, 1, 260, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
                End If
                Elem.RenderTransform = New TranslateTransform(0, 18)
                Anims.Add(AaTranslateY(Elem, -18, 340, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
            Next
            AniStart(Anims.ToArray(), "Home Modules Enter")
        Catch ex As Exception
            Logger.Warn(ex, "主页模块出场动画失败")
        End Try
    End Sub

    ''' <summary>
    ''' 开屏动画期间被推迟的处理（由 FormMain 在白色底层开始淡出时调用，只执行一次）。
    ''' 启动流程里主页模块直接就位、不播进入动画，所以这里只补一次头像刷新；
    ''' 程序内切换页面回主页时走 PageLaunchLeft_Loaded 的另一个分支，动画照常播放。
    ''' </summary>
    Private _ModulesEnterDeferred As Boolean = False
    Friend Sub PlayDeferredModulesEnter()
        If Not _ModulesEnterDeferred Then Return
        _ModulesEnterDeferred = False
        ApplyAvatar() '开屏期间被推迟的头像刷新在这里补一次
    End Sub

    ''' <summary>
    ''' 无开屏动画时：白色幕布淡出的同时补播一次模块入场动画（此时模块还是"未入场"状态），
    ''' 让页面不是硬切出现，而是内容依次浮现。
    ''' </summary>
    Friend Sub PlayDeferredModulesEnterAnimated()
        If Not _ModulesEnterDeferred Then Return
        _ModulesEnterDeferred = False
        AnimateModulesEnter(IncludeHero:=False)
        ApplyAvatar()
    End Sub

    Public Sub PageLaunchLeft_Loaded() Handles Me.Loaded
        '开屏动画正在播放时（启动流程）：主页模块直接就位，不播进入动画——
        '此时主页被纯白覆盖层挡着，动画看不见白跑一遍反而抢 CPU；页面本身是新建的，
        '所有模块本来就在最终位置。程序内切换页面回来时不走这个分支，入场动画照旧。
        If FrmMain IsNot Nothing AndAlso FrmMain.SplashPlaying Then
            _ModulesEnterDeferred = True
        Else
            AnimateModulesEnter()
        End If
        If IsLoad Then ScheduleLoadedRefresh()

        AprilPosTrans.X = 0
        AprilPosTrans.Y = 0

        If IsLoad Then Return
        IsLoad = True
        AniControlEnabled += 1

        '开始按钮
        AddHandler McInstanceListLoader.LoadingStateChanged, AddressOf RefreshButtonsUI
        AddHandler McFolderListLoader.LoadingStateChanged, AddressOf RefreshButtonsUI
        '实例列表加载完成后强制重建一次日历/游玩洞察：日历可能在列表就绪前就构建好了，
        '那时按名字查不到实例图标，会一直停在启动器图标占位（RefreshPlayStats 有缓存，必须 Force）
        AddHandler McInstanceListLoader.LoadingStateChanged,
            Sub()
                If McInstanceListLoader.State = LoadState.Finished Then
                    RefreshInstanceLogos()   '先把已登记的图标换成真实图标（不需要整块重建）
                    RefreshRecentPlay()      '最近游玩是另一份列表，也要重绑
                    RefreshPlayStats(True)
                End If
            End Sub
        '兜底：列表可能在页面构建之前就已经加载完（回调不会再触发），这里补两次延迟强制刷新
        If McInstanceListLoader.State = LoadState.Finished Then
            RefreshInstanceLogos()
            RefreshPlayStats(True)
        End If
        AniStart({
            AaCode(Sub() RefreshInstanceLogos(), 900),
            AaCode(Sub() RefreshPlayStats(True), 1200),
            AaCode(Sub() RefreshInstanceLogos(), 2000),
            AaCode(Sub() RefreshPlayStats(True), 3200)
        }, "Home Stats Refresh")
        '皮肤加载完成后刷新头像
        For Each SkinLoader In SkinLoaders
            AddHandler SkinLoader.LoadingStateChanged, AddressOf OnSkinLoaderStateChanged
        Next
        RefreshButtonsUI()
        '启动后延迟再刷一次头像，确保皮肤资源就绪后头像正确显示
        '（开屏动画期间跳过：这两次刷新会分别落在揭示与起飞动画中间，造成可感知的卡顿；
        ' 改为 PlayDeferredModulesEnter 时补一次）
        AniStart({
            AaCode(
            Sub()
                If FrmMain Is Nothing OrElse Not FrmMain.SplashPlaying Then ApplyAvatar()
            End Sub, 600),
            AaCode(
            Sub()
                If FrmMain Is Nothing OrElse Not FrmMain.SplashPlaying Then ApplyAvatar()
            End Sub, 1600)
        })

        '加载版本
        RunInNewThread(
        Sub()
            '自动整合包安装：准备
            Dim PackInstallPath As String = Nothing
            If FileUtils.Exists(Paths.Base & "modpack.zip") Then PackInstallPath = Paths.Base & "modpack.zip"
            If FileUtils.Exists(Paths.Base & "modpack.mrpack") Then PackInstallPath = Paths.Base & "modpack.mrpack"
            If PackInstallPath IsNot Nothing Then
                Logger.Warn($"需自动安装整合包：{PackInstallPath}")
                PageSelectLeft.CreateMcFolderInCurrentPath()
                McFolderListLoader.WaitForExit()
            End If
            '确认 Minecraft 文件夹存在
            If McFolderSelected = "" OrElse Not DirectoryUtils.Exists(McFolderSelected) Then
                '无效的文件夹
                If McFolderSelected = "" Then
                    Logger.Info("没有已储存的 Minecraft 文件夹")
                Else
                    Logger.Warn($"Minecraft 文件夹无效，该文件夹已不存在：{McFolderSelected}")
                End If
                McFolderListLoader.WaitForExit(IsForceRestart:=True)
                McFolderSelected = McFolderList.First.Location
            End If
            If Settings.Get(Of Boolean)("SystemDebugDelay") Then Thread.Sleep(RandomInteger(500, 3000))
            '自动整合包安装
            If PackInstallPath IsNot Nothing Then
                Try
                    Dim InstallLoader = ModpackInstall(PackInstallPath)
                    Logger.Info($"自动安装整合包已开始：{PackInstallPath}")
                    InstallLoader.WaitForExit()
                    If InstallLoader.State = LoadState.Finished Then
                        Logger.Info($"自动安装整合包成功，清理安装包：{PackInstallPath}")
                        FileUtils.Delete(PackInstallPath)
                    End If
                Catch ex As Exception
                    If ex.IsCanceled Then
                        Logger.Info($"自动安装整合包已取消：{PackInstallPath}")
                    Else
                        Logger.Error(ex, $"自动安装整合包失败：{PackInstallPath}", LogBehavior.Alert)
                    End If
                End Try
            End If
            '确认 Minecraft 版本存在
            Dim Selection As String = ReadIni(McFolderSelected & "YQL.ini", "Version")
            Dim Instance As McInstance = If(Selection = "", Nothing, New McInstance(Selection))
            If Instance Is Nothing OrElse Not PathUtils.IsParentOf(McFolderSelected, Instance.PathVersion) OrElse Not Instance.Check() Then
                '无效的版本
                Logger.Info($"当前选择的 Minecraft 版本无效：{If(Instance Is Nothing, "null", Instance.PathVersion)}", If(IsNothing(Instance), LogBehavior.None, LogBehavior.ToastIfDebug))
                If Not McInstanceListLoader.State = LoadState.Finished Then LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                If Not McInstanceList.Any() OrElse McInstanceList.First.Value(0).Logo.Contains("RedstoneBlock") Then
                    Instance = Nothing
                    Logger.Info("无可用 Minecraft 版本")
                Else
                    Instance = McInstanceList.First.Value(0)
                    Logger.Info($"自动选择 Minecraft 版本：{Instance.PathVersion}")
                End If
            End If
            RunInUi(
            Sub()
                McInstanceSelected = Instance '绕这一圈是为了避免 McVersionCheck 触发第二次版本改变
                IsLoadFinished = True
                RefreshButtonsUI()
                RefreshPage(False, False) '有可能选择的版本变化了，需要重新刷新
                If McLoginAble() = "" Then McLoginLoader.Start() '自动登录
                '用于自动化测试生成的程序是否可以正常运行
                If Environment.CommandLine.Contains("--test") Then
                    RunInThread(
                    Sub()
                        Thread.Sleep(500)
                        RunInUi(Sub() FrmMain.PageChange(FormMain.PageType.Download))
                        Thread.Sleep(500)
                        RunInUi(Sub() FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadMod))
                        Thread.Sleep(500)
                        RunInUi(Sub() FrmMain.PageChange(FormMain.PageType.Setup))
                        Thread.Sleep(500)
                        RunInUi(Sub() FrmMain.PageChange(FormMain.PageType.Other))
                        Thread.Sleep(500)
                        RunInUi(Sub() BtnVersion_Click())
                        Thread.Sleep(500)
                        RunInUi(Sub() BtnMore_Click())
                        Thread.Sleep(500)
                        FormMain.EndProgramForce(123)
                    End Sub)
                End If
            End Sub)
        End Sub, "Version Check", ThreadPriority.AboveNormal)

        '改变页面
        Dim LoginType = Settings.Get(Of McLoginType)("LoginType")
        If LoginType = McLoginType.Legacy OrElse LoginType = McLoginType.Ms Then CType(FindName("RadioLoginType" & LoginType), MyRadioButton).Checked = True
        RefreshPage(False, False)

        AniControlEnabled -= 1
    End Sub

#Region "切换大页面"

    ''' <summary>
    ''' 按当前版本账号的类型推断登录页面种类（3 = 统一通行证，4 = 第三方 Authlib）。
    ''' 实例设置里没写登录方式时用它兜底，无法判断时返回 0（正版或离线）。
    ''' </summary>
    Private Function LoginPageTypeFromAccount() As Integer
        Try
            Dim Ref = AccountPool.GetVersionAccount()
            If Ref Is Nothing Then Return 0
            Select Case Ref.Type
                Case McLoginType.Auth
                    Return 4
                Case McLoginType.Nide
                    Return 3
                Case Else
                    Return 0
            End Select
        Catch ex As Exception
            Return 0
        End Try
    End Function

    ''' <summary>
    ''' 切换至启动中页面。
    ''' </summary>
    Public Sub PageChangeToLaunching()
        '修改登陆方式：优先按当前账号的真实类型显示（本次登录也是按账号类型走的），
        '避免「选了第三方账号、页面却停在正版或离线」时显示成「离线登录」
        Dim ShowType As McLoginType = Settings.Get(Of McLoginType)("LoginType")
        Try
            Dim Ref = AccountPool.GetVersionAccount()
            If Ref IsNot Nothing Then ShowType = Ref.Type
        Catch ex As Exception
        End Try
        Select Case ShowType
            Case McLoginType.Legacy
                LabLaunchingMethod.Text = "离线登录"
            Case McLoginType.Ms
                LabLaunchingMethod.Text = "正版登录"
            Case McLoginType.Nide
                LabLaunchingMethod.Text = "统一通行证"
            Case McLoginType.Auth
                Dim ServerName As String = Settings.Get(Of String)("VersionServerAuthName", Instance:=McInstanceSelected)
                LabLaunchingMethod.Text = If(String.IsNullOrEmpty(ServerName), "第三方登录", ServerName)
        End Select
        '初始化页面
        LabLaunchingName.Text = McInstanceSelected.Name
        LabLaunchingStage.Text = "初始化"
        LabLaunchingTitle.Text = If(CurrentLaunchOptions?.SaveBatch Is Nothing, "正在启动游戏", "正在导出启动脚本")
        LabLaunchingProgress.Text = "0.00 %"
        LabLaunchingProgress.Opacity = 1
        LabLaunchingDownload.Visibility = Visibility.Visible
        LabLaunchingProgressLeft.Opacity = 0.6
        LabLaunchingDownload.Visibility = Visibility.Visible
        LabLaunchingDownload.Text = "0 B/s"
        LabLaunchingDownload.Opacity = 0
        LabLaunchingDownload.Visibility = Visibility.Collapsed
        LabLaunchingDownloadLeft.Opacity = 0
        LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed
        ProgressLaunchingFinished.Width = New GridLength(0, GridUnitType.Star)
        ProgressLaunchingUnfinished.Width = New GridLength(1, GridUnitType.Star)
        PanLaunchingHint.Opacity = 0
        PanLaunchingHint.Visibility = Visibility.Collapsed
        PanLaunchingInfo.Width = Double.NaN '重置宽度改变动画
        McLaunchProcess = Nothing
        McLaunchWatcher = Nothing
        '获取 “你知道吗” 提示
        LabLaunchingHint.Text = PageOtherTest.GetRandomHint()
        '初始化其他页面
        PanInput.IsHitTestVisible = False
        PanLaunching.IsHitTestVisible = False
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Run
        PanLaunching.Visibility = Visibility.Visible
        AniStart({
                AaOpacity(PanInput, 0, 50), '略作延迟，这样如果预检测失败，不会出现奇怪的弹一下的动画
                AaOpacity(PanInput, -PanInput.Opacity, 110, , New AniEaseInFluent, True),
                AaScaleTransform(PanInput, 1.2 - CType(PanInput.RenderTransform, ScaleTransform).ScaleX, 160),
                AaOpacity(PanLaunching, 1 - PanLaunching.Opacity, 150, 100),
                AaScaleTransform(PanLaunching, 1 - CType(PanLaunching.RenderTransform, ScaleTransform).ScaleX, 500, 100, New AniEaseOutBack(AniEasePower.Weak)),
                AaCode(Sub() PanLaunching.IsHitTestVisible = True, 150)
            }, "Launch State Page")
    End Sub
    ''' <summary>
    ''' 切换至登录页面。
    ''' </summary>
    Public Sub PageChangeToLogin()
        PageGet(PageCurrent).Reload(KeepInput:=False)
        PanInput.IsHitTestVisible = False
        PanLaunching.IsHitTestVisible = False
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Stop
        PanInput.Visibility = Visibility.Visible
        AniStart({
            AaOpacity(PanLaunching, -PanLaunching.Opacity, 150),
            AaScaleTransform(PanLaunching, 0.8 - CType(PanLaunching.RenderTransform, ScaleTransform).ScaleX, 150,, New AniEaseOutFluent(AniEasePower.Weak)),
            AaOpacity(PanInput, 1 - PanInput.Opacity, 250, 50),
            AaScaleTransform(PanInput, 1 - CType(PanInput.RenderTransform, ScaleTransform).ScaleX, 300, 50, New AniEaseOutBack(AniEasePower.Weak)),
            AaCode(Sub() PanInput.IsHitTestVisible = True, 200)
        }, "Launch State Page", True)
    End Sub

#End Region

#Region "切换登录页面"

    Private Enum PageType
        None
        Legacy
        Nide
        NideSkin
        Auth
        AuthSkin
        Ms
        MsSkin
    End Enum
    ''' <summary>
    ''' 当前页面的种类。
    ''' </summary>
    Private PageCurrent As PageType = PageType.None

    Private Function PageGet(Type As PageType)
        Select Case Type
            Case PageType.Legacy
                If IsNothing(FrmLoginLegacy) Then FrmLoginLegacy = New PageLoginLegacy
                Return FrmLoginLegacy
            Case PageType.Nide
                If IsNothing(FrmLoginNide) Then FrmLoginNide = New PageLoginNide
                Return FrmLoginNide
            Case PageType.NideSkin
                If IsNothing(FrmLoginNideSkin) Then FrmLoginNideSkin = New PageLoginNideSkin
                Return FrmLoginNideSkin
            Case PageType.Auth
                If IsNothing(FrmLoginAuth) Then FrmLoginAuth = New PageLoginAuth
                Return FrmLoginAuth
            Case PageType.AuthSkin
                If IsNothing(FrmLoginAuthSkin) Then FrmLoginAuthSkin = New PageLoginAuthSkin
                Return FrmLoginAuthSkin
            Case PageType.Ms
                If IsNothing(FrmLoginMs) Then FrmLoginMs = New PageLoginMs
                Return FrmLoginMs
            Case PageType.MsSkin
                If IsNothing(FrmLoginMsSkin) Then FrmLoginMsSkin = New PageLoginMsSkin
                Return FrmLoginMsSkin
            Case Else
                Throw New ArgumentOutOfRangeException("Type", "即将切换的登录分页编号越界")
        End Select
    End Function
    ''' <summary>
    ''' 切换现有登录页面种类，返回新页面的实例。
    ''' </summary>
    ''' <param name="Type">新页面的种类。</param>
    ''' <param name="Anim">是否显示动画。</param>
    Private Function PageChange(Type As PageType, Anim As Boolean)
        Dim PageNew As Object = FrmLoginMs '初始化一个东西，避免在执行时出现异常导致雪崩
        Try

#Region "确定更改的页面实例并实例化"
            If PageCurrent = Type Then Return PageNew
            PageNew = PageGet(Type)
#End Region

#Region "切换页面"
            AniStop("FrmLogin PageChange")
            '清除页面关联性
            If Not IsNothing(PageNew) AndAlso Not IsNothing(PageNew.Parent) Then PageNew.SetValue(ContentPresenter.ContentProperty, Nothing)
            If Anim Then
                '动画
                Dispatcher.Invoke(
                Sub()
                    '执行动画
                    AniStart({
                        AaOpacity(PanLogin, -PanLogin.Opacity, 100,, New AniEaseOutFluent),
                        AaCode(
                        Sub()
                            AniControlEnabled += 1
                            PanLogin.Children.Clear()
                            PanLogin.Children.Add(PageNew)
                            AniControlEnabled -= 1
                        End Sub, 100),
                        AaOpacity(PanLogin, 1, 100, 120, New AniEaseInFluent)
                    }, "FrmLogin PageChange")
                End Sub, DispatcherPriority.Render)
            Else
                '无动画
                AniControlEnabled += 1
                PanLogin.Children.Clear()
                PanLogin.Children.Add(PageNew)
                AniControlEnabled -= 1
            End If
#End Region

            PageCurrent = Type
            Return PageNew
        Catch ex As Exception
            Logger.Error(ex, $"切换登录分页失败（{Type}）")
            Return PageNew
        End Try
    End Function

    ''' <summary>
    ''' 确认当前显示的子页面正确，并刷新该页面。
    ''' </summary>
    Public Sub RefreshPage(KeepInput As Boolean, Anim As Boolean)
        '获取页面的可用种类并回写缓存
        Dim Type As PageType
        Dim LoginPageType As Integer
        If McInstanceSelected IsNot Nothing Then
            LoginPageType = Settings.Get(Of Integer)("VersionServerLogin", Instance:=McInstanceSelected)
            '实例本身没设过登录方式（0）时，按当前账号的真实类型推断：
            '否则选了第三方账号、这里却回落到「正版或离线」，启动过程中就会显示成「离线登录」
            If LoginPageType = 0 Then LoginPageType = LoginPageTypeFromAccount()
            '缓存当前版本的页面种类，下一次打开 McInstanceSelected 为空时才能加载出正确的页面
            Settings.Set("LoginPageType", LoginPageType)
        Else
            LoginPageType = Settings.Get(Of Integer)("LoginPageType")
        End If
        Select Case LoginPageType
            Case 0 '正版或离线
UnknownType:
                If Not Settings.Get(Of Boolean)("UiLauncherOfficialLogin") Then
                    '未显示正版选项：隐藏登录方式选择（包括离线），默认离线
                    Type = PageType.Legacy
                    Settings.Set("LoginType", McLoginType.Legacy)
                    PanType.Visibility = Visibility.Collapsed
                    PanTypeOne.Visibility = Visibility.Collapsed
                ElseIf RadioLoginType5.Checked Then
                    If Settings.Get(Of String)("CacheMsV2Access") = "" Then
                        Type = PageType.Ms
                    Else
                        Type = PageType.MsSkin
                    End If
                    Settings.Set("LoginType", McLoginType.Ms)
                    PanType.Visibility = Visibility.Visible
                    PanTypeOne.Visibility = Visibility.Collapsed
                    RadioLoginType5.Visibility = Visibility.Visible
                    RadioLoginType0.Visibility = Visibility.Visible
                Else
                    Type = PageType.Legacy
                    Settings.Set("LoginType", McLoginType.Legacy)
                    PanType.Visibility = Visibility.Visible
                    PanTypeOne.Visibility = Visibility.Collapsed
                    RadioLoginType5.Visibility = Visibility.Visible
                    RadioLoginType0.Visibility = Visibility.Visible
                End If
            Case 1 '仅正版
                If Settings.Get(Of String)("CacheMsV2Access") = "" Then
                    Type = PageType.Ms
                Else
                    Type = PageType.MsSkin
                End If
                Settings.Set("LoginType", McLoginType.Ms)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonShield)
                LabTypeOne.Text = "正版登录"
                RadioLoginType5.Visibility = Visibility.Visible
                RadioLoginType0.Visibility = Visibility.Collapsed
            Case 2 '仅离线
                Type = PageType.Legacy
                Settings.Set("LoginType", McLoginType.Legacy)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonOffline)
                LabTypeOne.Text = "离线登录"
            Case 3 '统一通行证
                If Settings.Get(Of String)("CacheNideAccess") = "" Then
                    Type = PageType.Nide
                Else
                    Type = PageType.NideSkin
                End If
                Settings.Set("LoginType", McLoginType.Nide)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonCard)
                LabTypeOne.Text = "统一通行证登录"
            Case 4 'Authlib-Injector
                If Settings.Get(Of String)("CacheAuthAccess") = "" Then
                    Type = PageType.Auth
                Else
                    Type = PageType.AuthSkin
                End If
                Settings.Set("LoginType", McLoginType.Auth)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonCard)
                LabTypeOne.Text = If(McInstanceSelected Is Nothing, Settings.Get(Of String)("CacheAuthServerName"), Settings.Get(Of String)("VersionServerAuthName", Instance:=McInstanceSelected))
                If LabTypeOne.Text = "" Then LabTypeOne.Text = "第三方登录"
            Case Else
                Logger.Error($"未知的登录页面：{LoginPageType}", LogBehavior.Toast)
                GoTo UnknownType
        End Select
        '刷新离线用户名下拉框
        PanLegacyName.Visibility = If(Settings.Get(Of McLoginType)("LoginType") = McLoginType.Legacy, Visibility.Visible, Visibility.Collapsed)
        ReloadLegacyName(KeepInput)
        '刷新存档列表
        RefreshSaves()
        '刷新页面
        If PageCurrent = Type Then
            '登录页类型没变（切换实例时最常见）：不走 PageChange，但主页上的实例信息
            '（名称、版本徽标、图标、上次游玩等）仍然要立刻刷新，否则换实例后要等别的
            '回调兜底才更新，看起来就是"内容延迟切换"
            RefreshHomeInfo()
            Return
        End If
        PageChange(Type, Anim).Reload(KeepInput)
        Dim Control As MyRadioButton = FindName("RadioLoginType" & Settings.Get(Of McLoginType)("LoginType"))
        If Control IsNot Nothing Then Control.Checked = True
        RefreshHomeInfo()
    End Sub
    Private Sub RadioLoginType_Change(sender As Object, raiseByMouse As Boolean) Handles RadioLoginType0.Check, RadioLoginType5.Check
        If raiseByMouse Then RefreshPage(True, True)
    End Sub

#End Region

#Region "离线用户名与存档"

    ''' <summary>
    ''' 刷新离线用户名下拉框（注册机制：从已注册的名称中选择）。
    ''' </summary>
    Public Sub ReloadLegacyName(Optional KeepInput As Boolean = False)
        Try
            If ComboName Is Nothing Then Return
            Dim Input As String = If(KeepInput, ComboName.Text.Trim, "")
            Dim Names As New List(Of String)
            If Settings.Get(Of String)("LoginLegacyName") <> "" Then
                Names.AddRange(Settings.Get(Of String)("LoginLegacyName").Split("¨").Where(Function(n) n.Trim <> ""))
            End If
            ComboName.ItemsSource = If(Names.Any, Names, Nothing)
            If Names.Contains(Input) Then
                ComboName.SelectedItem = Input
            ElseIf Names.Any Then
                ComboName.SelectedItem = Names(0)
            Else
                ComboName.SelectedItem = Nothing
            End If
            RefreshHomeInfo()
        Catch ex As Exception
            Logger.Error(ex, "刷新离线用户名列表失败")
        End Try
    End Sub

    ''' <summary>
    ''' 注册一个新的离线用户名。
    ''' </summary>
    Private Sub BtnNameAdd_Click(sender As Object, e As EventArgs) Handles BtnNameAdd.Click
        Try
            Dim NewName As String = MyMsgBoxInput("注册离线用户名", "请输入新的离线用户名（仅允许英文、数字、下划线，至多 16 个字符）：", HintText:="玩家名", Button1:="注册", Button2:="取消")
            If NewName Is Nothing Then Return
            NewName = NewName.Trim
            If NewName = "" Then
                Hint("玩家名不能为空！", HintType.Red)
                Return
            End If
            If Not NewName.RegexCheck("^[0-9A-Za-z_]+$") Then
                Hint("玩家名只能包含英文、数字、下划线！", HintType.Red)
                Return
            End If
            If NewName.Length > 16 Then
                Hint("玩家名最多 16 个字符！", HintType.Red)
                Return
            End If
            Dim Names As New List(Of String)
            If Settings.Get(Of String)("LoginLegacyName") <> "" Then Names.AddRange(Settings.Get(Of String)("LoginLegacyName").Split("¨"))
            If Names.Contains(NewName) Then
                Hint("该用户名已注册！", HintType.Red)
                Return
            End If
            Names.Add(NewName)
            Settings.Set("LoginLegacyName", Names.Join("¨"c))
            ReloadLegacyName(False)
            ComboName.SelectedItem = NewName
            Hint($"已注册离线用户名 {NewName}！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "注册离线用户名失败")
        End Try
    End Sub

    ''' <summary>
    ''' 刷新存档数量显示。
    ''' </summary>
    Private Sub RefreshSavesCount()
        If PanSavesList Is Nothing OrElse LabSavesCount Is Nothing Then Return
        Dim Count As Integer = PanSavesList.Children.OfType(Of MyListItem)().Count()
        LabSavesCount.Text = If(Count = 0, "暂无", Count & " 个")
    End Sub

    ''' <summary>
    ''' 刷新当前版本下的存档列表。
    ''' </summary>
    Public Sub RefreshSaves()
        Try
            If PanSavesList Is Nothing Then Return
            '不在此时清空列表：等待后台读取完成后一次性「清空 + 重建」，避免每次进入首页图标闪烁
            If McInstanceSelected Is Nothing Then
                PanSavesList.Children.Clear()
                PanSavesList.Children.Add(New TextBlock With {.Text = "尚未选择版本", .Margin = New Thickness(0, 2, 0, 0), .Foreground = FindResource("ColorBrushGray3"), .Opacity = 0.6})
                RefreshSavesCount()
                Return
            End If
            RunInNewThread(
            Sub()
                Dim Entries As New List(Of (Name As String, Info As String, Logo As String))
                Try
                    Dim SavesFolder = DirectoryUtils.GetInfo(McInstanceSelected.PathIndie & "saves\")
                    If SavesFolder.Exists Then
                        For Each Save In SavesFolder.EnumerateDirectories.OrderByDescending(Function(s) s.LastWriteTime)
                            Dim Sp = Save.FullName
                            Dim Si = ModSave.LoadSaveInfo(Sp)
                            Dim Parts As New List(Of String)
                            If Not String.IsNullOrWhiteSpace(Si.GameVersion) Then Parts.Add(Si.GameVersion)
                            Parts.Add(ModSave.GameTypeName(Si.GameType))
                            Parts.Add("上次修改：" & StringUtils.FormatTimeSpan(Save.LastWriteTime - Date.Now, False))
                            Dim Cover = Sp.TrimEnd("\"c) & "\icon.png"
                            Entries.Add((Save.Name, String.Join(" · ", Parts), If(IO.File.Exists(Cover), Cover, PathImage & "Blocks/Grass.png")))
                        Next
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "读取存档列表失败")
                End Try
                RunInUi(
                Sub()
                    If PanSavesList Is Nothing Then Return
                    PanSavesList.Children.Clear()
                    If Not Entries.Any Then
                        PanSavesList.Children.Add(New TextBlock With {.Text = "该版本还没有存档", .Margin = New Thickness(0, 2, 0, 0), .Foreground = FindResource("ColorBrushGray3"), .Opacity = 0.6})
                        RefreshSavesCount()
                        Return
                    End If
                    For Each Entry In Entries
                        Dim Item As New MyListItem With {
                            .Title = Entry.Name,
                            .Info = Entry.Info,
                            .Height = 40,
                            .Type = MyListItem.CheckType.Clickable,
                            .Logo = Entry.Logo,
                            .LogoScale = 0.9
                        }
                        AddHandler Item.Click, Sub(sender As Object, e As MouseButtonEventArgs)
                                                   OpenSavePage(Entry.Name)
                                               End Sub
                        PanSavesList.Children.Add(Item)
                    Next
                    RefreshSavesCount()
                End Sub)
            End Sub, "Refresh Saves List")
        Catch ex As Exception
            Logger.Error(ex, "刷新存档列表失败")
        End Try
    End Sub

    ''' <summary>打开存档设置页（查看信息 / 修改 / 复制种子）。</summary>
    Private Sub OpenSavePage(SaveName As String)
        Try
            Dim SavePath As String = McInstanceSelected.PathIndie & "saves\" & SaveName
            If Not DirectoryUtils.Exists(SavePath) Then
                Hint("存档文件夹不存在", HintType.Red)
                Return
            End If
            Dim Page As New PageSaveSetup
            Page.Load(McInstanceSelected, SaveName, SavePath)
            FrmMain.PageChange(New FormMain.PageStackData With {
                .Page = FormMain.PageType.SaveInfo,
                .Additional = {"存档设置", Page}})
        Catch ex As Exception
            Logger.Error(ex, "打开存档设置失败")
        End Try
    End Sub

#End Region

#Region "皮肤"

    ''' <summary>
    ''' 皮肤加载状态改变时刷新头像。
    ''' </summary>
    Private Sub OnSkinLoaderStateChanged()
        RunInUi(Sub()
                    Try
                        RefreshHomeInfo()
                    Catch ex As Exception
                        Logger.Warn(ex, "刷新皮肤头像失败")
                    End Try
                End Sub, True)
    End Sub

    '微软正版皮肤
    Public Shared SkinMs As New LoaderTask(Of (String, String), String)("Loader Skin Ms", AddressOf SkinMsLoad, AddressOf SkinMsInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinMsInput() As (String, String)
        '获取名称
        Return (Settings.Get(Of String)("CacheMsV2Name"), Settings.Get(Of String)("CacheMsV2Uuid"))
    End Function
    Private Shared Sub SkinMsLoad(Data As LoaderTask(Of (String, String), String))
        '清空已有皮肤
        '如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        RunInUi(Sub() If FrmLoginMsSkin IsNot Nothing AndAlso FrmLoginMsSkin.Skin IsNot Nothing Then FrmLoginMsSkin.Skin.Clear())
        '获取 Url
        Dim UserName As String = Data.Input.Item1
        Dim Uuid As String = Data.Input.Item2
        If UserName = "" Then
            Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
            Logger.Info("获取微软正版皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(Uuid, "Ms")
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.IsCanceled Then
                Data.Output = ""
                Return
            ElseIf ex.GetDisplay(False).Contains("(429)") Then
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Logger.Error($"获取正版皮肤失败（{UserName}）：获取皮肤太过频繁，请 5 分钟后再试！", LogBehavior.Toast)
            ElseIf ex.GetDisplay(False).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Logger.Info("用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Logger.Error(ex, $"获取微软正版皮肤失败（{UserName}）", LogBehavior.Toast)
            End If
        End Try
Finish:
        '刷新显示
        If FrmLoginMsSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginMsSkin.Skin.Load)
        ElseIf Not Data.IsCanceled Then '如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = Nothing '清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
        End If
    End Sub

    '离线皮肤
    Public Shared SkinLegacy As New LoaderTask(Of (String, String), String)("Loader Skin Legacy", AddressOf SkinLegacyLoad, AddressOf SkinLegacyInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinLegacyInput() As (String, String)
        '根据类型判断输入
        Dim Type As Integer = Settings.Get(Of Integer)("LaunchSkinType")
        Select Case Type
            Case 0
                Dim Name As String = ""
                If FrmLaunchLeft IsNot Nothing AndAlso FrmLaunchLeft.ComboName IsNot Nothing Then
                    Name = FrmLaunchLeft.ComboName.Text.Trim
                End If
                If Name = "" AndAlso Settings.Get(Of String)("LoginLegacyName") <> "" Then
                    Name = Settings.Get(Of String)("LoginLegacyName").ToString.BeforeFirst("¨")
                End If
                Return ("0", Name)
            Case 3
                Return ("3", Settings.Get(Of String)("LaunchSkinID"))
            Case Else
                Return (Type.ToString, "")
        End Select
    End Function
    Private Shared Sub SkinLegacyLoad(Data As LoaderTask(Of (String, String), String))
        '获取 Url
        Select Case CInt(Data.Input.Item1)
            Case 0 '默认
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(Data.Input.Item2)) & ".png"
            Case 1 'Steve
UseDefault:
                Data.Output = PathImage & "Skins/Steve.png"
            Case 2 'Alex
                Data.Output = PathImage & "Skins/Alex.png"
            Case 3 '正版
                Dim ID As String = Data.Input.Item2
                Try
                    If ID.Count < 2 Then
                        Data.Output = PathImage & "Skins/Steve.png"
                    Else
                        Dim Result As String = McLoginMojangUuid(ID, True)
                        If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & ID)
                        Result = McSkinGetAddress(Result, "Mojang")
                        If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & ID)
                        Result = McSkinDownload(Result)
                        If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & ID)
                        Data.Output = Result
                    End If
                Catch ex As Exception
                    If ex.IsCanceled Then
                        Data.Output = ""
                        Return
                    ElseIf ex.GetDisplay(False).Contains("(429)") Then
                        Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(ID)) & ".png"
                        Logger.Info($"获取离线登录使用的正版皮肤失败（{ID}）：获取皮肤太过频繁，请 5 分钟后再试！")
                    Else
                        Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(ID)) & ".png"
                        Logger.Warn(ex, $"获取离线登录使用的正版皮肤失败（{ID}）")
                    End If
                End Try
            Case 4 '自定义
                If Not FileUtils.Exists(Paths.AppDataThenName & "CustomSkin.png") Then
                    Hint("未找到离线皮肤自定义文件，可能它已被删除。缘启 将使用默认的 Steve 皮肤！")
                    Settings.Set("LaunchSkinType", 1)
                    GoTo UseDefault
                End If
                Data.Output = Paths.AppDataThenName & "CustomSkin.png"
        End Select
    End Sub

    '统一通行证皮肤
    Public Shared SkinNide As New LoaderTask(Of (String, String), String)("Loader Skin Nide", AddressOf SkinNideLoad, AddressOf SkinNideInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinNideInput() As (String, String)
        '获取名称
        Return (Settings.Get(Of String)("CacheNideName"), Settings.Get(Of String)("CacheNideUuid"))
    End Function
    Private Shared Sub SkinNideLoad(Data As LoaderTask(Of (String, String), String))
        '清空已有皮肤
        '如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        RunInUi(Sub() If FrmLoginNideSkin IsNot Nothing AndAlso FrmLoginNideSkin.Skin IsNot Nothing Then FrmLoginNideSkin.Skin.Clear())
        '获取 Url
        Dim UserName As String = Data.Input.Item1
        Dim Uuid As String = Data.Input.Item2
        If UserName = "" Then
            Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
            Logger.Info("获取统一通行证皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(Uuid, "Nide")
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.IsCanceled Then
                Data.Output = ""
                Return
            ElseIf ex.GetDisplay(False).Contains("(429)") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Error($"获取统一通行证皮肤失败（{UserName}）：获取皮肤太过频繁，请 5 分钟后再试！", LogBehavior.Toast)
            ElseIf ex.GetDisplay(False).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Info("用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Error(ex, $"获取统一通行证皮肤失败（{UserName}）", LogBehavior.Toast)
            End If
        End Try
Finish:
        '刷新显示
        If FrmLoginNideSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginNideSkin.Skin.Load)
        ElseIf Not Data.IsCanceled Then '如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = Nothing '清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
        End If
    End Sub

    'Authlib-Injector 皮肤
    Public Shared SkinAuth As New LoaderTask(Of (String, String), String)("Loader Skin Auth", AddressOf SkinAuthLoad, AddressOf SkinAuthInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinAuthInput() As (String, String)
        '获取名称
        Return (Settings.Get(Of String)("CacheAuthName"), Settings.Get(Of String)("CacheAuthUuid"))
    End Function
    Private Shared Sub SkinAuthLoad(Data As LoaderTask(Of (String, String), String))
        '清空已有皮肤
        '如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        RunInUi(Sub() If FrmLoginAuthSkin IsNot Nothing AndAlso FrmLoginAuthSkin.Skin IsNot Nothing Then FrmLoginAuthSkin.Skin.Clear())
        '获取 Url
        Dim UserName As String = Data.Input.Item1
        Dim UUID As String = Data.Input.Item2
        If UserName = "" Then
            Data.Output = PathImage & "Skins/Steve.png"
            Logger.Info("获取 Authlib-Injector 皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(UUID, "Auth")
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.IsCanceled Then
                Data.Output = ""
                Return
            ElseIf ex.GetDisplay(False).Contains("(429)") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Error($"获取 Authlib-Injector 皮肤失败（{UserName}）：获取皮肤太过频繁，请 5 分钟后再试！", LogBehavior.Toast)
            ElseIf ex.GetDisplay(False).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Info("用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Error(ex, $"获取 Authlib-Injector 皮肤失败（{UserName}）", LogBehavior.Toast)
            End If
        End Try
Finish:
        '刷新显示
        If FrmLoginAuthSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginAuthSkin.Skin.Load)
        ElseIf Not Data.IsCanceled Then '如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = Nothing '清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
        End If
    End Sub

    '全部皮肤加载器
    '需要放在其中元素的后面，否则会因为它提前被加载而莫名其妙变成 Nothing
    Public Shared SkinLoaders As New List(Of LoaderTask(Of (String, String), String)) From {SkinMs, SkinLegacy, SkinNide, SkinAuth}

#End Region

    '版本选择按钮
    Private Sub BtnVersion_Click() Handles BtnVersion.Click
        If McLaunchLoader.State = LoadState.Loading Then Return
        FrmMain.PageChange(FormMain.PageType.InstanceSelect)
    End Sub
    '游玩洞察卡片：进入工具箱的「游戏统计」页
    Private Sub PanInsightCard_Click(sender As Object, e As MouseButtonEventArgs)
        If FrmMain Is Nothing Then Return
        FrmMain.PageChange(FormMain.PageType.Tools, FormMain.PageSubType.OtherGameStats)
    End Sub
    '启动按钮
    Public Sub LaunchButtonClick() Handles BtnLaunch.Click
        If McLaunchLoader.State = LoadState.Loading OrElse Not BtnLaunch.IsEnabled OrElse
            (FrmMain.PageRight IsNot Nothing AndAlso FrmMain.PageRight.PageState <> MyPageRight.PageStates.ContentStay AndAlso FrmMain.PageRight.PageState <> MyPageRight.PageStates.ContentEnter) Then Return
        '愚人节处理
        If IsAprilEnabled AndAlso Not IsAprilGiveup Then
            IsAprilGiveup = True
            Settings.Set("AprilYear", Date.Now.Year)
            FrmLaunchLeft.AprilScaleTrans.ScaleX = 1
            FrmLaunchLeft.AprilScaleTrans.ScaleY = 1
            FrmLaunchLeft.AprilPosTrans.X = 0
            FrmLaunchLeft.AprilPosTrans.Y = 0
            FrmMain.BtnExtraApril.ShowRefresh()
        End If
        '实际的启动
        If BtnLaunch.Text = "启动游戏" Then
            McLaunchStart()
        ElseIf BtnLaunch.Text = "下载游戏" Then
            FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
        End If
    End Sub
    Private BtnLaunchState As Integer = 0
    Private BtnLaunchInstance As McInstance = Nothing
    Public Sub RefreshButtonsUI() Handles BtnLaunch.Loaded
        If Not BtnLaunch.IsLoaded Then Return
        '按钮挂载同样发生在切换动画中途：重刷新并入统一延迟，避免动画中途卡顿（按钮文字状态仍立即更新）
        ScheduleLoadedRefresh()
        '获取当前状态
        Dim CurrentState As Integer
        If (Not IsLoadFinished) OrElse McInstanceListLoader.State = LoadState.Loading OrElse McFolderListLoader.State = LoadState.Loading Then
            CurrentState = 0
        Else
            If McInstanceSelected Is Nothing Then
                If Settings.Get(Of Boolean)("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow Then
                    CurrentState = 1
                Else
                    CurrentState = 2
                End If
            Else
                CurrentState = 3
            End If
        End If
        '更新状态
        If CurrentState = BtnLaunchState AndAlso
           If(McInstanceSelected Is Nothing, "", McInstanceSelected.PathVersion) = If(BtnLaunchInstance Is Nothing, "", BtnLaunchInstance.PathVersion) Then GoTo ExitRefresh
        BtnLaunchInstance = McInstanceSelected
        BtnLaunchState = CurrentState
        Select Case CurrentState
            Case 0
                Logger.Info("启动按钮：正在加载 Minecraft 版本")
                FrmLaunchLeft.BtnLaunch.Text = "正在加载"
                FrmLaunchLeft.BtnLaunch.IsEnabled = False
                FrmLaunchLeft.LabInstanceName.Text = "正在加载中，请稍候"
                FrmLaunchLeft.BtnVersion.IsEnabled = False
                FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed
            Case 1
                Logger.Info("启动按钮：无 Minecraft 版本，下载已禁用")
                FrmLaunchLeft.BtnLaunch.Text = "启动游戏"
                FrmLaunchLeft.BtnLaunch.IsEnabled = False
                FrmLaunchLeft.LabInstanceName.Text = "未找到可用的游戏版本"
                FrmLaunchLeft.BtnVersion.IsEnabled = True
                FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed
            Case 2
                Logger.Info("启动按钮：无 Minecraft 版本，要求下载")
                FrmLaunchLeft.BtnLaunch.Text = "下载游戏"
                FrmLaunchLeft.BtnLaunch.IsEnabled = True
                FrmLaunchLeft.LabInstanceName.Text = "未找到可用的游戏版本"
                FrmLaunchLeft.BtnVersion.IsEnabled = True
                FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed
            Case 3
                Logger.Info($"启动按钮：Minecraft 版本：{McInstanceSelected.PathVersion}")
                FrmLaunchLeft.BtnLaunch.Text = "启动游戏"
                FrmLaunchLeft.BtnVersion.IsEnabled = True
                FrmLaunchLeft.BtnLaunch.IsEnabled = True
                FrmLaunchLeft.LabInstanceName.Text = McInstanceSelected.Name
                'FrmLaunchLeft.BtnMore.Visibility = Visibility.Visible '由功能隐藏设置修改
        End Select
ExitRefresh:
        '功能隐藏
        FrmLaunchLeft.BtnVersion.Visibility = If(Not PageSetupUI.HiddenForceShow AndAlso Settings.Get(Of Boolean)("UiHiddenFunctionSelect"), Visibility.Collapsed, Visibility.Visible)
        If CurrentState = 3 Then
            FrmLaunchLeft.BtnMore.Visibility = FrmLaunchLeft.BtnVersion.Visibility
        End If
    End Sub
    '取消按钮
    Public Sub BtnCancel_Click() Handles BtnCancel.Click
        If McLaunchLoaderReal IsNot Nothing Then
            McLaunchLoaderReal.Cancel()
            McLaunchLog("已取消启动")
            Try
                If McLaunchWatcher IsNot Nothing Then
                    McLaunchWatcher.Kill()
                ElseIf McLaunchProcess IsNot Nothing Then
                    If Not McLaunchProcess.HasExited Then McLaunchProcess.Kill()
                End If
            Catch ex As Exception
                Logger.Error(ex, "取消启动结束进程失败", LogBehavior.Toast)
            End Try
        End If
    End Sub
    '版本设置按钮
    Private Sub BtnMore_Click() Handles BtnMore.Click
        If McLaunchLoader.State = LoadState.Loading Then Return
        If McInstanceLock.IsLocked(McInstanceSelected) Then
            Hint("该版本正在被修改，暂时无法进入版本设置！", HintType.Red)
            Return
        End If
        McInstanceSelected.Load()
        PageInstanceLeft.Instance = McInstanceSelected
        FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
    End Sub
    ''' <summary>
    ''' 每 0.1s 执行一次，刷新启动的数据 UI 显示。
    ''' </summary>
    Public Sub LaunchingRefresh()
        Try
            If McLaunchLoaderReal.State = LoadState.Canceled Then Return
            '阶段状态获取
            Dim IsLaunched As Boolean = False '是否已经启动游戏，只是在等待窗口
            Try
                For Each Loader In McLaunchLoaderReal.GetLoaderList(False)
                    If Loader.State = LoadState.Loading OrElse Loader.State = LoadState.Waiting Then
                        LabLaunchingStage.Text = Loader.Name
                        IsLaunched = Loader.Name = "等待游戏窗口出现" OrElse Loader.Name = "结束处理"
                        Exit Try
                    End If
                Next
                LabLaunchingStage.Text = "已完成"
            Catch ex As Exception
                Logger.Warn(ex, "获取是否启动完成失败，可能是由于启动状态改变导致集合已修改")
                Return
            End Try
            LabLaunchingTitle.Text = If(IsLaunched OrElse McLaunchLoaderReal.State = LoadState.Finished,
                "已启动游戏",
                If(CurrentLaunchOptions.SaveBatch Is Nothing, "正在启动游戏", "正在导出启动脚本"))
            If AniIsRun("Launch State Page") Then IsLaunched = False '等待页面切换动画完成
            '更新进度
            Dim ActualProgress = McLaunchLoaderReal.Progress
            If ActualProgress >= ShowProgress Then ShowProgress += (ActualProgress - ShowProgress) * 0.1 + 0.0025 '向实际进度靠一点
            If ActualProgress <= ShowProgress Then ShowProgress = ActualProgress '原来或处理后变得比实际进度高，直接回退
            If IsLaunched Then ShowProgress = 1 '如果已经完成了，就不卖关子了
            LabLaunchingProgress.Text = (ShowProgress * 100).ToString("0.00") & " %"
            '更新下载速度
            Dim HasLaunchDownloader As Boolean = False
            Try
                If NetManager.Speed = 0 AndAlso LabLaunchingDownload.Visibility = Visibility.Collapsed Then
                    '可能只是在检查文件，没有实际下载
                    HasLaunchDownloader = False
                Else
                    For Each Loader In NetManager.Tasks
                        If Loader.RealParent IsNot Nothing AndAlso Loader.RealParent.Name = "Minecraft 启动" AndAlso Loader.State = LoadState.Loading Then HasLaunchDownloader = True
                    Next
                End If
            Catch ex As Exception
                Logger.Warn(ex, "获取 Minecraft 启动下载器失败，可能是因为启动被取消")
                HasLaunchDownloader = False
            End Try
            LabLaunchingDownload.Text = StringUtils.FormatByteSize(NetManager.Speed) & "/s"
            '进度改变动画
            Dim AnimList As New List(Of AniData) From {
                 AaGridLengthWidth(ProgressLaunchingFinished, ShowProgress - ProgressLaunchingFinished.Width.Value, 130,, New AniEaseOutFluent),
                 AaGridLengthWidth(ProgressLaunchingUnfinished, 1 - ShowProgress - ProgressLaunchingUnfinished.Width.Value, 130,, New AniEaseOutFluent)
            }
            If HasLaunchDownloader = (LabLaunchingDownload.Visibility = Visibility.Collapsed) Then 'IsDownloadStateChanged
                LabLaunchingDownload.Visibility = Visibility.Visible
                LabLaunchingDownloadLeft.Visibility = Visibility.Visible
                AnimList.AddRange({
                    AaOpacity(LabLaunchingDownload, If(HasLaunchDownloader, 1, 0) - LabLaunchingDownload.Opacity, 90),
                    AaOpacity(LabLaunchingDownloadLeft, If(HasLaunchDownloader, 0.5, 0) - LabLaunchingDownloadLeft.Opacity, 90),
                    AaCode(
                    Sub()
                        If Not HasLaunchDownloader Then
                            LabLaunchingDownload.Visibility = Visibility.Collapsed
                            LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed
                        End If
                    End Sub, 110)
                })
            End If
            If (Not IsLaunched) = (LabLaunchingProgress.Visibility = Visibility.Collapsed) Then 'IsProgressStateChanged
                LabLaunchingProgress.Visibility = Visibility.Visible
                LabLaunchingProgressLeft.Visibility = Visibility.Visible
                If IsLaunched Then PanLaunchingHint.Visibility = Visibility.Visible
                AnimList.AddRange({
                    AaOpacity(LabLaunchingProgress, If(Not IsLaunched, 1, 0) - LabLaunchingProgress.Opacity, 90),
                    AaOpacity(LabLaunchingProgressLeft, If(Not IsLaunched, 0.5, 0) - LabLaunchingProgressLeft.Opacity, 90),
                    AaOpacity(PanLaunchingHint, If(IsLaunched, 1, 0) - PanLaunchingHint.Opacity, 90)
                })
            End If
            AniStart(AnimList, "Launching Progress")
        Catch ex As Exception
            Logger.Error(ex, "刷新启动信息失败")
        End Try
    End Sub
    Private ShowProgress As Double = 0
    '尺寸改变动画
    Private IsWidthAnimating As Boolean = False
    Private ActualUsedWidth As Double
    Private Sub PanLaunchingInfo_SizeChangedW(sender As Object, e As SizeChangedEventArgs) Handles PanLaunchingInfo.SizeChanged
        Dim DeltaWidth As Double = e.NewSize.Width - e.PreviousSize.Width
        If e.PreviousSize.Width = 0 OrElse IsWidthAnimating OrElse Math.Abs(DeltaWidth) < 1 OrElse PanLaunchingInfo.ActualWidth = 0 Then Return
        AniStart({
            AaWidth(PanLaunchingInfo, DeltaWidth, 180,, New AniEaseOutFluent),
            AaCode(Sub()
                       IsWidthAnimating = False
                       PanLaunchingInfo.Width = ActualUsedWidth
                   End Sub,, True)
        }, "Launching Info Width")
        IsWidthAnimating = True
        ActualUsedWidth = PanLaunchingInfo.Width
        PanLaunchingInfo.Width = e.PreviousSize.Width
    End Sub
    Private IsHeightAnimating As Boolean = False
    Private ActualUsedHeight As Double
    Private Sub PanLaunchingInfo_SizeChangedH(sender As Object, e As SizeChangedEventArgs) Handles PanLaunchingInfo.SizeChanged
        Dim DeltaHeight As Double = e.NewSize.Height - e.PreviousSize.Height
        If e.PreviousSize.Height = 0 OrElse IsHeightAnimating OrElse Math.Abs(DeltaHeight) < 1 OrElse PanLaunchingInfo.ActualHeight = 0 Then Return
        AniStart({
            AaHeight(PanLaunchingInfo, DeltaHeight, 180,, New AniEaseOutFluent),
            AaCode(Sub()
                       IsHeightAnimating = False
                       PanLaunchingInfo.Height = ActualUsedHeight
                   End Sub,, True)
        }, "Launching Info Height")
        IsHeightAnimating = True
        ActualUsedHeight = PanLaunchingInfo.Height
        PanLaunchingInfo.Height = e.PreviousSize.Height
    End Sub

#Region "主页信息与交互"

    ''' <summary>
    ''' 当前账号对应的皮肤文件地址（用于头像）。
    ''' </summary>
    Public Shared Function GetAccountSkinAddress() As String
        Try
            Dim AccountType As McLoginType = McLoginType.Legacy
            Try
                '优先按账户池中当前账户的类型取皮肤，避免页面切换时 LoginType 被旧登录页逻辑改写导致头像回退默认
                Dim Ref = AccountPool.GetVersionAccount()
                If Ref IsNot Nothing Then
                    AccountType = Ref.Type
                Else
                    AccountType = Settings.Get(Of McLoginType)("LoginType")
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取账户类型失败")
            End Try
            Dim Loader As LoaderTask(Of (String, String), String) = Nothing
            Select Case AccountType
                Case McLoginType.Ms
                    Loader = SkinMs
                Case McLoginType.Nide
                    Loader = SkinNide
                Case McLoginType.Auth
                    Loader = SkinAuth
                Case Else
                    Loader = SkinLegacy
            End Select
            If Loader IsNot Nothing Then
                If Loader.State <> LoadState.Loading AndAlso Loader.State <> LoadState.Finished Then Loader.Start()
                If Not String.IsNullOrWhiteSpace(Loader.Output) Then Return Loader.Output
            End If
            If SkinLegacy IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(SkinLegacy.Output) Then Return SkinLegacy.Output
        Catch ex As Exception
            Logger.Warn(ex, "获取当前皮肤地址失败")
        End Try
        Return PathImage & "Skins/Steve.png"
    End Function

    ''' <summary>
    ''' 时间差转换为「X 前」。
    ''' </summary>
    Private Function FormatTimeAgo(T As Date) As String
        Try
            Return StringUtils.FormatTimeSpan(T - Date.Now, False)
        Catch ex As Exception
            Return T.ToString("yyyy/MM/dd")
        End Try
    End Function

    ''' <summary>
    ''' 上次游玩时间：以该实例 logs\latest.log 的修改时间为准。
    ''' </summary>
    Private Function GetLastPlayTime(Instance As McInstance) As Date?
        Try
            Dim LogPath As String = Instance.PathIndie & "logs\latest.log"
            If IO.File.Exists(LogPath) Then Return IO.File.GetLastWriteTime(LogPath)
            If IO.Directory.Exists(Instance.PathIndie) Then Return IO.Directory.GetLastWriteTime(Instance.PathIndie)
        Catch ex As Exception
            Logger.Warn(ex, "读取上次游玩时间失败")
        End Try
        Return Nothing
    End Function

    ''' <summary>
    ''' 刷新主页：状态条、当前实例、账户头像、运行环境、存档数量。
    ''' </summary>
    Public Sub RefreshHomeInfo()
        '核心信息（账户、问候语、实例名与图标）同步刷新：全是轻量操作，切换实例后立刻可见；
        '重活（游玩日历重建、最近游玩列表、新闻、环境信息）延后到 Background 优先级，
        '避免卡住页面切换动画——这正是「换实例后主页内容切换慢」的主要来源。
        RefreshHomeInfoCore()
        RefreshHomeInfoExtra()
    End Sub

    ''' <summary>主页核心信息：账户、问候语、实例徽标/名称/元信息/上次游玩/实例图标。</summary>
    Public Sub RefreshHomeInfoCore()
        If LabChipInstance Is Nothing Then Return
        EnsureSkinHook()
        Try
            '账户：优先显示当前版本在账户池中选定的账户
            Dim Ref = AccountPool.GetVersionAccount()
            If Ref IsNot Nothing Then
                LabAccountName.Text = Ref.Name
                LabAccountType.Text = AccountPool.TypeDisplay(Ref.Type)
            Else
                Dim LoginType As McLoginType = McLoginType.Legacy
                Try
                    LoginType = Settings.Get(Of McLoginType)("LoginType")
                Catch ex As Exception
                    Logger.Warn(ex, "读取登录方式失败")
                End Try
                Select Case LoginType
                    Case McLoginType.Ms
                        Dim MsName As String = Settings.Get(Of String)("CacheMsV2Name")
                        LabAccountName.Text = If(String.IsNullOrWhiteSpace(MsName), "未登录", MsName)
                        LabAccountType.Text = "正版"
                    Case McLoginType.Nide
                        Dim NideName As String = Settings.Get(Of String)("CacheNideName")
                        LabAccountName.Text = If(String.IsNullOrWhiteSpace(NideName), "未登录", NideName)
                        LabAccountType.Text = "统一通行证"
                    Case McLoginType.Auth
                        Dim AuthName As String = Settings.Get(Of String)("CacheAuthName")
                        LabAccountName.Text = If(String.IsNullOrWhiteSpace(AuthName), "未登录", AuthName)
                        LabAccountType.Text = "第三方登录"
                    Case Else
                        Dim LegacyName As String = ""
                        If ComboName IsNot Nothing Then LegacyName = If(ComboName.Text, "").Trim
                        LabAccountName.Text = If(String.IsNullOrWhiteSpace(LegacyName), "离线账户", LegacyName)
                        LabAccountType.Text = "离线"
                End Select
            End If
            If SkinHeadAccount IsNot Nothing Then SkinHeadAccount.SetSkin(GetAccountSkinAddress())
            FrmMain?.RefreshAccountAvatar()
            RefreshAccountMenu()

            '问候语
            Try
                Dim HourNow = Date.Now.Hour
                Dim Greeting As String = If(HourNow >= 5 AndAlso HourNow < 11, "早上好", If(HourNow < 13, "中午好", If(HourNow < 18, "下午好", "晚上好")))
                Dim GreetName As String = LabAccountName.Text
                If GreetName = "未登录" OrElse String.IsNullOrWhiteSpace(GreetName) Then GreetName = "冒险家"
                LabGreetingTitle.Text = Greeting & "，" & GreetName
                Dim WeekNames = {"周日", "周一", "周二", "周三", "周四", "周五", "周六"}
                LabGreetingSub.Text = $"{Date.Now.Month} 月 {Date.Now.Day} 日 · {WeekNames(CInt(Date.Now.DayOfWeek))} · 祝你玩得开心"
            Catch ex As Exception
                Logger.Warn(ex, "刷新问候语失败")
            End Try

            '实例
            If PanChipInstance IsNot Nothing Then PanChipInstance.Visibility = If(McInstanceSelected Is Nothing, Visibility.Visible, Visibility.Collapsed)
            If McInstanceSelected Is Nothing Then
                LabChipInstance.Text = "未选择版本"
                LabChipVersion.Text = "—"
                PanChipLoader.Visibility = Visibility.Collapsed
                LabInstanceName.Text = "未选择版本"
                LabInstanceMeta.Text = "前往「版本选择」挑选或安装一个游戏版本"
                LabPlayNoteTitle.Text = "提示"
                LabPlayNote.Text = "尚未选择游戏版本"
                LabPlayNoteRam.Text = ""
            Else
                LabChipInstance.Text = McInstanceSelected.Name
                Dim Display As String = ""
                Try
                    Display = McInstanceSelected.VersionDisplayName()
                Catch ex As Exception
                    Logger.Warn(ex, "读取版本显示名称失败")
                End Try
                Dim Parts = Display.Split({","c}, StringSplitOptions.RemoveEmptyEntries)
                Dim VanillaName As String = ""
                Try
                    VanillaName = McInstanceSelected.Version.VanillaName
                Catch ex As Exception
                    Logger.Warn(ex, "读取原版版本号失败")
                End Try
                LabChipVersion.Text = If(String.IsNullOrWhiteSpace(VanillaName), "—", VanillaName)
                If Parts.Length > 1 Then
                    PanChipLoader.Visibility = Visibility.Visible
                    LabChipLoader.Text = Parts(1).Trim()
                Else
                    PanChipLoader.Visibility = Visibility.Collapsed
                End If
                Dim Isolated As Boolean = False
                Try
                    Isolated = Not String.Equals(McInstanceSelected.PathIndie.TrimEnd("\"c), McInstanceSelected.PathVersion.TrimEnd("\"c), StringComparison.OrdinalIgnoreCase)
                Catch ex As Exception
                    Logger.Warn(ex, "判断版本隔离失败")
                End Try
                LabInstanceName.Text = McInstanceSelected.Name
                LabInstanceMeta.Text = Display.Replace(", ", "  ·  ") & If(Isolated, "  ·  版本隔离已开启", "")
                '上次游玩
                Dim Last As Date? = GetLastPlayTime(McInstanceSelected)
                LabPlayNoteTitle.Text = "上次游玩"
                LabPlayNote.Text = If(Last.HasValue, FormatTimeAgo(Last.Value), "还没有游玩记录")
            End If

            '实例图标（未选择实例时用启动器图标占位，不再显示草方块）
            Try
                Dim LogoPath As String = PathImage & "SplashIcon.png"
                If McInstanceSelected IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(McInstanceSelected.Logo) Then LogoPath = McInstanceSelected.Logo
                ImgInstanceLogo.Source = LogoPath
                If FrmMain IsNot Nothing Then FrmMain.SplashSyncInstanceIcon(ShowCard:=True) '启动动画仍在播放时同步卡片图标并让卡片浮现
            Catch ex As Exception
                Logger.Warn(ex, "刷新实例图标失败")
            End Try
        Catch ex As Exception
            Logger.Error(ex, "刷新主页信息失败")
        End Try
    End Sub

    ''' <summary>主页附加信息（较重）：环境信息、新闻、游玩统计、最近游玩。合并排队，在 UI 空闲时执行。</summary>
    Private _HomeInfoExtraQueued As Boolean = False
    Private Sub RefreshHomeInfoExtra()
        If _HomeInfoExtraQueued Then Return '已在排队：合并同一时间内的多次刷新，避免重复重建
        _HomeInfoExtraQueued = True
        Application.Current.Dispatcher.InvokeAsync(
            Sub()
                _HomeInfoExtraQueued = False
                Try
                    RefreshEnvInfo()
                    RefreshNews()
                    RefreshPlayStats()
                    RefreshRecentPlay()
                Catch ex As Exception
                    Logger.Error(ex, "刷新主页附加信息失败")
                End Try
            End Sub, Threading.DispatcherPriority.Background)
    End Sub

    ''' <summary>
    ''' 刷新运行环境与内存信息。
    ''' </summary>
    Private Sub RefreshEnvInfo()
        Try
            'Java（状态条）
            Dim JavaText As String = "自动选择"
            Try
                If McLaunchJavaSelected IsNot Nothing AndAlso McLaunchJavaSelected.Version IsNot Nothing Then
                    JavaText = "Java " & McLaunchJavaSelected.Version.Major
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取 Java 信息失败")
            End Try
            LabChipJava.Text = If(JavaText = "自动选择", "Java 自动", If(JavaText.StartsWithF("Java", True), JavaText, "Java " & JavaText))

            '分配内存（Hero 备注）
            Dim RamValue As Double = 0
            Try
                If McInstanceSelected IsNot Nothing Then
                    RamValue = PageSetupLaunch.GetRam(McInstanceSelected, False)
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取内存分配失败")
            End Try
            If LabPlayNoteRam IsNot Nothing Then
                LabPlayNoteRam.Text = If(RamValue > 0, "分配内存：" & If(RamValue = Math.Floor(RamValue), RamValue & ".0", RamValue) & " GB", "")
            End If

            '系统内存占用（状态条）
            Try
                Dim TotalRam As Double = My.Computer.Info.TotalPhysicalMemory / 1024 / 1024 / 1024
                Dim UsedRam As Double = TotalRam - My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024
                LabChipRam.Text = $"内存 {UsedRam:0.#} / {TotalRam:0.#} GB"
            Catch ex As Exception
                Logger.Warn(ex, "读取系统内存失败")
            End Try

            If LabSavesCount IsNot Nothing Then RefreshSavesCount()
        Catch ex As Exception
            Logger.Error(ex, "刷新运行环境失败")
        End Try
    End Sub

    '打开当前实例目录
    Private Sub BtnInstanceFolder_Click() Handles BtnInstanceFolder.Click, BtnStatusFolder.Click
        Try
            If McInstanceSelected Is Nothing Then
                OpenExplorer(Paths.Base)
            Else
                OpenExplorer(McInstanceSelected.PathVersion)
            End If
        Catch ex As Exception
            Logger.Error(ex, "打开实例目录失败", LogBehavior.Toast)
        End Try
    End Sub

    '刷新存档列表
    Private Sub BtnSavesRefresh_Click() Handles BtnSavesRefresh.Click
        RefreshSaves()
        Hint("已刷新存档列表")
    End Sub

    '打开存档文件夹
    Private Sub BtnWorldFolder_Click() Handles BtnWorldFolder.Click
        Try
            If McInstanceSelected Is Nothing Then
                OpenExplorer(Paths.Base)
            Else
                Dim Folder As String = McInstanceSelected.PathIndie & "saves\"
                If DirectoryUtils.Exists(Folder) Then
                    OpenExplorer(Folder)
                Else
                    Hint("该版本还没有存档文件夹", HintType.Blue)
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, "打开存档文件夹失败", LogBehavior.Toast)
        End Try
    End Sub

    '快捷入口：打开种子地图
    Private Sub BtnGoSeedMap_Click() Handles BtnGoSeedMap.Click
        Try
            FrmMain.PageChange(FormMain.PageType.SeedMap)
        Catch ex As Exception
            Logger.Error(ex, "打开种子地图失败", LogBehavior.Toast)
        End Try
    End Sub

    '快捷入口：查看游戏统计
    Private Sub BtnGoGameStats_Click() Handles BtnGoGameStats.Click
        Try
            FrmMain.PageChange(FormMain.PageType.Tools, FormMain.PageSubType.OtherGameStats)
        Catch ex As Exception
            Logger.Error(ex, "打开游戏统计失败", LogBehavior.Toast)
        End Try
    End Sub

    '快捷入口：矿石分布
    Private Sub BtnGoOreGuide_Click() Handles BtnGoOreGuide.Click
        Try
            FrmMain.PageChange(FormMain.PageType.Tools, FormMain.PageSubType.OtherOreGuide)
        Catch ex As Exception
            Logger.Error(ex, "打开矿石分布失败", LogBehavior.Toast)
        End Try
    End Sub

    '快捷入口：打开彩色文字生成器
    Private Sub BtnGoColorText_Click() Handles BtnGoColorText.Click
        Try
            FrmMain.PageChange(FormMain.PageType.Tools, FormMain.PageSubType.OtherColorText)
        Catch ex As Exception
            Logger.Error(ex, "打开彩色文字生成器失败", LogBehavior.Toast)
        End Try
    End Sub

#End Region

#Region "账户下拉"

    ''' <summary>
    ''' 点击账户胶囊：展开或收起账号下拉面板。
    ''' </summary>
    Private Sub BtnAccount_Click() Handles BtnAccount.Click
        Try
            AniStop("Account Menu")
            Dim Open As Boolean = PanAccountMenu.Visibility <> Visibility.Visible
            If Open Then
                PanAccountMenu.Visibility = Visibility.Visible
                PanAccountMenu.Opacity = 0
                '重建列表包含多次皮肤图片解码，若在弹出前同步执行会卡住展开动画：
                '先以现有内容弹出，动画结束后再重建以保证内容最新
                AniStart({
                    AaOpacity(PanAccountMenu, 1, 120, 0, New AniEaseOutFluent(AniEasePower.Weak)),
                    AaCode(Sub() RefreshAccountMenu(), 30)
                }, "Account Menu")
            Else
                AniStart({
                    AaOpacity(PanAccountMenu, -PanAccountMenu.Opacity, 90, 0, New AniEaseInFluent(AniEasePower.Weak)),
                    AaCode(Sub() PanAccountMenu.Visibility = Visibility.Collapsed,, True)
                }, "Account Menu")
            End If
            AniStart(AaRotateTransform(PathAccountArrow, If(Open, 180, 0) - CType(PathAccountArrow.RenderTransform, RotateTransform).Angle, 180,, New AniEaseOutFluent(AniEasePower.Weak)), "Account Arrow")
        Catch ex As Exception
            Logger.Error(ex, "打开账号下拉面板失败")
        End Try
    End Sub

    ''' <summary>
    ''' 点击下拉面板以外的区域时收起（胶囊点击由 BtnAccount_Click 处理）。
    ''' </summary>
    Private Sub PageLaunchLeft_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseDown
        If PanAccountMenu.Visibility <> Visibility.Visible Then Return
        Dim Source As DependencyObject = TryCast(e.OriginalSource, DependencyObject)
        Do While Source IsNot Nothing
            If Source Is BtnAccount OrElse Source Is PanAccountMenu Then Return
            Try
                Source = VisualTreeHelper.GetParent(Source)
            Catch
                Source = Nothing
            End Try
        Loop
        PanAccountMenu.Visibility = Visibility.Collapsed
    End Sub

    ''' <summary>
    ''' 「创建账号」入口：展开登录 / 注册页（复用既有登录分页）。
    ''' </summary>
    Private Sub BtnAccountAdd_Click() Handles BtnAccountAdd.Click
        Try
            PanAccountMenu.Visibility = Visibility.Collapsed
            '选择要添加的账户类型
            Dim SelectionControl As New List(Of IMyRadio) From {
                New MyRadioBox With {.Text = "微软正版（Microsoft 账户）"},
                New MyRadioBox With {.Text = "离线账户"},
                New MyRadioBox With {.Text = "统一通行证"},
                New MyRadioBox With {.Text = "Authlib-Injector 第三方登录"},
                New MyRadioBox With {.Text = "LittleSkin 登录"}
            }
            Dim Index As Integer? = MyMsgBoxSelect(SelectionControl, "选择要添加的账户类型", "下一步", "取消")
            If Index Is Nothing Then Return
            Select Case Index.Value
                Case 0
                    AddMsAccount() '正版走 OAuth，无表单
                Case 1
                    Dim Ref = MyMsgBoxAccountAdd(McLoginType.Legacy, "添加离线账户")
                    If Ref IsNot Nothing Then FinishAddAccount(Ref)
                Case 2
                    Dim Ref = MyMsgBoxAccountAdd(McLoginType.Nide, "添加统一通行证账户")
                    If Ref IsNot Nothing Then FinishAddAccount(Ref)
                Case 3
                    Dim Ref = MyMsgBoxAccountAdd(McLoginType.Auth, "添加第三方账户")
                    If Ref IsNot Nothing Then FinishAddAccount(Ref)
                Case 4
                    'LittleSkin 是 Authlib-Injector 的预设服务器，复用第三方登录
                    Dim Ref = MyMsgBoxAccountAdd(McLoginType.Auth, "添加 LittleSkin 账户", "https://littleskin.cn/api/yggdrasil")
                    If Ref IsNot Nothing Then FinishAddAccount(Ref)
            End Select
        Catch ex As Exception
            Logger.Error(ex, "打开创建账号页面失败")
        End Try
    End Sub

    ''' <summary>添加账户成功后：设为当前版本账户（或全局默认）并刷新。</summary>
    Private _AddAsGlobalDefault As Boolean = False
    Private Sub FinishAddAccount(Ref As AccountRef)
        If _AddAsGlobalDefault Then
            AccountPool.SetDefaultAccount(Ref)
            _AddAsGlobalDefault = False
        Else
            AccountPool.SetVersionAccount(Ref)
        End If
        RefreshAccountMenu()
        RefreshHomeInfo()
        PulseAccountCapsule()
        Hint($"已添加账户 {Ref.Name}", HintType.Green)
    End Sub

    ''' <summary>从导航栏头像入口：选择或注册全局默认账户。</summary>
    Public Sub ShowGlobalAccountPicker()
        Try
            PanAccountMenu.Visibility = Visibility.Collapsed
            Dim Accounts = AccountPool.GetAllAccounts()
            Dim Options As New List(Of IMyRadio)
            For Each A In Accounts
                Options.Add(New MyRadioBox With {.Text = $"{AccountPool.TypeDisplay(A.Type)} · {A.Name}"})
            Next
            Options.Add(New MyRadioBox With {.Text = "＋ 创建新账户"})
            Dim Index As Integer? = MyMsgBoxSelect(Options, "选择要全局使用的账户", "设为默认", "取消")
            If Index Is Nothing Then Return
            If Index.Value < Accounts.Count Then
                Dim Ref = Accounts(Index.Value)
                AccountPool.SetDefaultAccount(Ref)
                RefreshAccountMenu()
                RefreshHomeInfo()
                PulseAccountCapsule()
                Hint($"已设为全局默认账户 {Ref.Name}", HintType.Green)
            Else
                _AddAsGlobalDefault = True
                BtnAccountAdd_Click()
            End If
        Catch ex As Exception
            Logger.Error(ex, "打开全局账户选择失败")
        End Try
    End Sub

    ''' <summary>皮肤加载完成后刷新头像（避免启动时头像为空）。</summary>
    Private SkinHookAdded As Boolean = False
    Private Sub EnsureSkinHook()
        If SkinHookAdded Then Return
        SkinHookAdded = True
        AddHandler SkinMs.OnStateChangedUi, AddressOf OnSkinLoaded
        AddHandler SkinLegacy.OnStateChangedUi, AddressOf OnSkinLoaded
        AddHandler SkinNide.OnStateChangedUi, AddressOf OnSkinLoaded
        AddHandler SkinAuth.OnStateChangedUi, AddressOf OnSkinLoaded
    End Sub
    Private Sub OnSkinLoaded(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        If NewState = LoadState.Finished Then
            RunInUi(
            Sub()
                ApplyAvatar()
            End Sub, True)
        End If
    End Sub

    ''' <summary>刷新账户胶囊头像与导航栏头像。</summary>
    Private Sub ApplyAvatar()
        Try
            If SkinHeadAccount IsNot Nothing Then SkinHeadAccount.SetSkin(GetAccountSkinAddress())
            If FrmMain IsNot Nothing Then FrmMain.RefreshAccountAvatar()
        Catch ex As Exception
            Logger.Warn(ex, "刷新头像失败")
        End Try
    End Sub

    ''' <summary>主页「最近游玩」：显示最近玩过的 3 个版本（不含当前版本）。</summary>
    Private Sub RefreshRecentPlay()
        If PanRecentPlay Is Nothing Then Return
        Try
            PanRecentPlay.Children.Clear()
            ClearInstanceLogoTargets(OnlyRecent:=True)
            Dim CurrentName = If(McInstanceSelected Is Nothing, "", McInstanceSelected.Name)
            Dim LastPlay As New Dictionary(Of String, Date)(StringComparer.OrdinalIgnoreCase)
            For Each S In PlayLogModule.LoadSessions()
                If String.IsNullOrWhiteSpace(S.Name) Then Continue For
                If LastPlay.ContainsKey(S.Name) Then
                    If S.EndTime > LastPlay(S.Name) Then LastPlay(S.Name) = S.EndTime
                Else
                    LastPlay(S.Name) = S.EndTime
                End If
            Next
            Dim Recent = LastPlay.Where(Function(kv) Not String.Equals(kv.Key, CurrentName, StringComparison.OrdinalIgnoreCase)).OrderByDescending(Function(kv) kv.Value).Take(3).ToList()
            If Recent.Count = 0 Then
                PanRecentPlay.Children.Add(New TextBlock With {.Text = "暂无记录", .Style = CType(FindResource("MutedText"), Style), .Margin = New Thickness(4, 2, 4, 2)})
                Return
            End If
            For Each Item In Recent
                Dim Name As String = Item.Key
                Dim Btn As New Button With {.Style = CType(FindResource("LinkRow"), Style), .Height = 36, .HorizontalContentAlignment = HorizontalAlignment.Stretch, .Cursor = Cursors.Hand}
                Dim Row As New Grid
                Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
                Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
                Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
                Dim Icon As New Border With {.Width = 28, .Height = 28, .CornerRadius = New CornerRadius(7), .ClipToBounds = True, .VerticalAlignment = VerticalAlignment.Center}
                Dim Ico As New MyImage With {.Stretch = Stretch.UniformToFill}
                Icon.Child = Ico
                SetInstanceLogo(Ico, Name, IntoRecent:=True)
                Row.Children.Add(Icon)
                Dim Txt As New TextBlock With {.Text = Name, .FontSize = 12.5, .VerticalAlignment = VerticalAlignment.Center, .TextTrimming = TextTrimming.CharacterEllipsis, .Margin = New Thickness(10, 0, 0, 0)}
                Dim Time As New TextBlock With {.Text = FormatTimeAgo(Item.Value), .FontSize = 11, .Foreground = CType(FindResource("BrushText3"), Brush), .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(8, 0, 4, 0)}
                Grid.SetColumn(Icon, 0) : Grid.SetColumn(Txt, 1) : Grid.SetColumn(Time, 2)
                Row.Children.Add(Txt) : Row.Children.Add(Time)
                Btn.Content = Row
                AddHandler Btn.Click, Sub(s, e) SwitchVersionByName(Name)
                PanRecentPlay.Children.Add(Btn)
            Next
            '实例列表可能还在扫描：图标没解析出来就短时间内重试，避免一直停在启动器图标
            EnsureInstanceLogosSettled()
        Catch ex As Exception
            Logger.Warn(ex, "刷新最近游玩失败")
        End Try
    End Sub

    '图标回填重试的序号：新的刷新会让旧的轮询自动失效
    Private _LogoSettleSerial As Integer = 0
    Private _LogoSettleTimer As DispatcherTimer = Nothing

    ''' <summary>
    ''' 首次打开启动器时实例列表往往还没扫描完（列表为空），此时「最近游玩」查不到实例，
    ''' 图标会落到启动器图标占位。
    ''' 这里用 DispatcherTimer 每 250ms 回填一次（不能用 AniStart —— 页面构建期 AniControlEnabled 非 0，
    ''' 那些动画会被抑制，之前"一直不恢复"就是这个原因）；仍在解析中就先强制加载一次实例列表。
    ''' </summary>
    Private Sub EnsureInstanceLogosSettled()
        _LogoSettleSerial += 1
        Dim MySerial As Integer = _LogoSettleSerial
        StopLogoSettleTimer()
        Dim Attempt As Integer = 0
        Dim Forced As Boolean = False
        Dim Timer As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(250)}
        AddHandler Timer.Tick,
            Sub(s, e)
                If MySerial <> _LogoSettleSerial Then
                    Timer.Stop()
                    Return
                End If
                RefreshInstanceLogos()
                Attempt += 1
                If AllInstanceLogosResolved() OrElse Attempt >= 40 Then
                    Timer.Stop()
                    Return
                End If
                '还没解析出来：多半是实例列表没就绪，后台强制加载一次（只做一次）
                If Not Forced AndAlso (McInstanceListLoader Is Nothing OrElse McInstanceListLoader.State <> LoadState.Finished) Then
                    Forced = True
                    RunInThread(
                        Sub()
                            Try
                                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun,
                                                MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                            Catch ex As Exception
                                Logger.Warn(ex, "强制加载实例列表失败")
                            End Try
                            RunInUi(
                                Sub()
                                    RefreshInstanceLogos()
                                    RefreshRecentPlay()
                                End Sub)
                        End Sub)
                End If
            End Sub
        _LogoSettleTimer = Timer
        Timer.Start()
    End Sub

    Private Sub StopLogoSettleTimer()
        Try
            If _LogoSettleTimer IsNot Nothing Then
                _LogoSettleTimer.Stop()
                _LogoSettleTimer = Nothing
            End If
        Catch ex As Exception
            Logger.Warn(ex, "停止图标回填计时器失败")
        End Try
    End Sub

    ''' <summary>「最近游玩」里出现的名字是否都已经能在实例列表中找到。</summary>
    Private Function AllInstanceLogosResolved() As Boolean
        If PendingRecentLogos.Count = 0 Then Return True
        For Each T In PendingRecentLogos
            If String.IsNullOrWhiteSpace(T.Name) Then Continue For
            If FindInstanceByName(T.Name) Is Nothing Then Return False
        Next
        Return True
    End Function

    ''' <summary>在实例列表里按名字找实例（名字比较忽略大小写，并去掉首尾空白）。</summary>
    Private Function FindInstanceByName(Name As String) As McInstance
        If String.IsNullOrWhiteSpace(Name) OrElse McInstanceList Is Nothing Then Return Nothing
        Dim Key As String = Name.Trim()
        For Each Group In McInstanceList.Values
            For Each I In Group
                If String.Equals(I.Name.Trim(), Key, StringComparison.OrdinalIgnoreCase) Then Return I
            Next
        Next
        Return Nothing
    End Function

    ''' <summary>按版本名切换到指定版本。</summary>
    Private Sub SwitchVersionByName(Name As String)
        Try
            Dim Inst As McInstance = FindInstanceByName(Name)
            If Inst Is Nothing Then
                '刚打开启动器时实例列表可能还没扫描完（此时列表是空的）：
                '先强制加载一次再找，避免"点击显示版本不存在"
                If McInstanceListLoader IsNot Nothing AndAlso McInstanceListLoader.State <> LoadState.Finished Then
                    Try
                        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun,
                                        MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                    Catch ex As Exception
                        Logger.Warn(ex, "强制加载实例列表失败")
                    End Try
                    Inst = FindInstanceByName(Name)
                End If
            End If
            If Inst Is Nothing Then
                Hint($"未找到版本「{Name}」，它可能已被删除或改名", HintType.Red)
                Return
            End If
            McInstanceSelected = Inst
            RefreshHomeInfo()
            RefreshSaves()
            PulseVersionName()
            If FrmLaunchRight IsNot Nothing Then FrmLaunchRight.ForceRefresh()
            Hint($"已切换到 {Name}", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "切换最近游玩版本失败")
        End Try
    End Sub

    ''' <summary>版本切换反馈：版本名做一次轻微缩放脉冲。</summary>
    Private Sub PulseVersionName()
        Try
            If LabInstanceName Is Nothing Then Return
            LabInstanceName.RenderTransformOrigin = New Point(0.5, 0.5)
            If Not TypeOf LabInstanceName.RenderTransform Is ScaleTransform Then
                LabInstanceName.RenderTransform = New ScaleTransform(1, 1)
            End If
            AniStop("Version Name Pulse")
            AniStart({
                AaScaleTransform(LabInstanceName, -0.08, 90, 0, New AniEaseOutFluent(AniEasePower.Weak)),
                AaScaleTransform(LabInstanceName, 0.12, 200, 90, New AniEaseOutBack(AniEasePower.Weak)),
                AaScaleTransform(LabInstanceName, -0.04, 120, 290, New AniEaseOutFluent(AniEasePower.Weak))
            }, "Version Name Pulse")
        Catch ex As Exception
            Logger.Warn(ex, "版本切换反馈动画失败")
        End Try
    End Sub

    ''' <summary>微软正版：触发 OAuth 登录，成功后自动入池。</summary>
    Private Sub AddMsAccount()
        RunInNewThread(
        Sub()
            Try
                McLoginMsLoader.Start(New McLoginMs, IsForceRestart:=True)
                Do While McLoginMsLoader.State = LoadState.Loading
                    Thread.Sleep(50)
                Loop
                If McLoginMsLoader.State = LoadState.Finished Then
                    RunInUi(Sub() FinishAddAccount(New AccountRef With {.Type = McLoginType.Ms, .Name = Settings.Get(Of String)("CacheMsV2Name")}))
                End If
            Catch ex As Exception
                If Not ex.IsCanceled Then Logger.Error(ex, "微软登录失败", LogBehavior.Alert)
            End Try
        End Sub, "Add Ms Account")
    End Sub

    ''' <summary>
    ''' 重建账号下拉面板：列出「全版本通用账户池」中所有类型的已保存账号。
    ''' </summary>
    Private Sub RefreshAccountMenu()
        If PanAccountList Is Nothing Then Return
        Try
            PanAccountList.Children.Clear()
            Dim CurrentRef = AccountPool.GetVersionAccount()
            Dim CurName As String = If(CurrentRef IsNot Nothing, CurrentRef.Name, "")
            '微软正版
            Try
                Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
                If MsJson IsNot Nothing Then
                    For Each Account In MsJson
                        Dim Name As String = Account.Key
                        If CurrentRef IsNot Nothing AndAlso CurrentRef.Type = McLoginType.Ms AndAlso String.Equals(Name, CurName, StringComparison.Ordinal) Then Continue For
                        Dim Token As String = If(Account.Value Is Nothing, "", Account.Value.ToString)
                        PanAccountList.Children.Add(BuildAccountRow(Name, "正版", GetPoolAccountSkin(McLoginType.Ms, Name),
                            Sub() SwitchMsAccount(Name, Token), Sub() RemoveAccount(Name, McLoginType.Ms)))
                    Next
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取微软账号列表失败")
            End Try
            '离线
            Dim LegacyNames As New List(Of String)
            If Settings.Get(Of String)("LoginLegacyName") <> "" Then
                LegacyNames.AddRange(Settings.Get(Of String)("LoginLegacyName").Split("¨").Where(Function(x) x.Trim <> ""))
            End If
            For Each RawName As String In LegacyNames
                Dim Name As String = RawName.Trim
                If CurrentRef IsNot Nothing AndAlso CurrentRef.Type = McLoginType.Legacy AndAlso String.Equals(Name, CurName, StringComparison.Ordinal) Then Continue For
                PanAccountList.Children.Add(BuildAccountRow(Name, "离线", GetPoolAccountSkin(McLoginType.Legacy, Name),
                    Sub() SwitchLegacyAccount(Name), Sub() RemoveAccount(Name, McLoginType.Legacy)))
            Next
            '统一通行证 / Authlib-Injector
            AddThirdPartyToMenu("Nide", "统一通行证", McLoginType.Nide)
            AddThirdPartyToMenu("Auth", "第三方登录", McLoginType.Auth)
        Catch ex As Exception
            Logger.Error(ex, "刷新账号下拉列表失败")
        End Try
    End Sub

    ''' <summary>把某一第三方登录方式的账号加入下拉面板。</summary>
    Private Sub AddThirdPartyToMenu(Token As String, TypeText As String, Type As McLoginType)
        Try
            Dim Names As New List(Of String), Passs As New List(Of String)
            If Not Settings.Get(Of String)("Login" & Token & "Email") = "" Then Names.AddRange(Settings.Get(Of String)("Login" & Token & "Email").Split("¨"))
            If Not Settings.Get(Of String)("Login" & Token & "Pass") = "" Then Passs.AddRange(Settings.Get(Of String)("Login" & Token & "Pass").Split("¨"))
            Dim Ref = AccountPool.GetVersionAccount()
            For i As Integer = 0 To Names.Count - 1
                Dim Name As String = Names(i)
                If Name.Trim = "" Then Continue For
                If Ref IsNot Nothing AndAlso Ref.Type = Type AndAlso String.Equals(Name, Ref.Name, StringComparison.Ordinal) Then Continue For
                Dim Pass As String = If(i < Passs.Count, Passs(i), "")
                PanAccountList.Children.Add(BuildAccountRow(Name, TypeText, GetPoolAccountSkin(Type, Name),
                    Sub() SwitchThirdPartyAccount(Token, Name, Pass), Sub() RemoveAccount(Name, Type)))
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取第三方账号列表失败")
        End Try
    End Sub

    ''' <summary>
    ''' 构建一行账号（头像 + 名字 + 右侧移除按钮）。
    ''' </summary>
    Private Function BuildAccountRow(Name As String, TypeText As String, SkinAddress As String, OnSelect As Action, OnRemove As Action) As FrameworkElement
        Dim Root As New Grid With {.Height = 40, .Margin = New Thickness(0, 1, 0, 1)}

        Dim SelectBtn As New Button With {
            .Style = FindResource("LinkRow"),
            .Height = 40,
            .Padding = New Thickness(8, 0, 8, 0),
            .HorizontalContentAlignment = HorizontalAlignment.Stretch,
            .Cursor = Cursors.Hand
        }
        Dim Layout As New Grid
        Layout.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Layout.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Layout.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(30)})

        Dim Head As New MySkinHead With {.Width = 28, .Height = 28, .CornerRadius = 7, .VerticalAlignment = VerticalAlignment.Center}
        Head.SetSkin(SkinAddress)
        Layout.Children.Add(Head)

        Dim Info As New StackPanel With {.Margin = New Thickness(9, 0, 4, 0), .VerticalAlignment = VerticalAlignment.Center}
        Dim NameText As New TextBlock With {.Text = Name, .FontSize = 12.5, .FontWeight = FontWeights.SemiBold, .TextTrimming = TextTrimming.CharacterEllipsis}
        NameText.SetResourceReference(TextBlock.ForegroundProperty, "BrushText1")
        Info.Children.Add(NameText)
        If Not String.IsNullOrWhiteSpace(TypeText) Then
            Dim SubText As New TextBlock With {.Text = TypeText, .FontSize = 10.5, .Margin = New Thickness(0, 1, 0, 0), .TextTrimming = TextTrimming.CharacterEllipsis}
            SubText.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
            Info.Children.Add(SubText)
        End If
        Grid.SetColumn(Info, 1)
        Layout.Children.Add(Info)

        SelectBtn.Content = Layout
        SelectBtn.Tag = OnSelect
        AddHandler SelectBtn.Click, AddressOf AccountRow_Click
        Root.Children.Add(SelectBtn)

        Dim RemoveBtn As New MyIconButton With {
            .Tag = Name,
            .Height = 22,
            .Width = 22,
            .Margin = New Thickness(0, 0, 7, 0),
            .VerticalAlignment = VerticalAlignment.Center,
            .HorizontalAlignment = HorizontalAlignment.Right,
            .Logo = "F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z"
        }
        RemoveBtn.Tag = OnRemove
        AddHandler RemoveBtn.Click, AddressOf AccountRemove_Click
        Root.Children.Add(RemoveBtn)

        Return Root
    End Function

    ''' <summary>
    ''' 账号行点击：执行该行绑定的切换动作（动作存储在 Button.Tag）。
    ''' </summary>
    Private Sub AccountRow_Click(sender As Object, e As RoutedEventArgs)
        Try
            Dim Act = TryCast(CType(sender, Button).Tag, Action)
            If Act IsNot Nothing Then Act()
        Catch ex As Exception
            Logger.Error(ex, "切换账号失败")
        End Try
    End Sub

    ''' <summary>
    ''' 账号移除键点击：执行该行绑定的移除动作（动作存储在 MyIconButton.Tag）。
    ''' </summary>
    Private Sub AccountRemove_Click(sender As Object, e As EventArgs)
        Try
            Dim Act = TryCast(CType(sender, MyIconButton).Tag, Action)
            If Act IsNot Nothing Then Act()
        Catch ex As Exception
            Logger.Error(ex, "移除账号失败")
        End Try
    End Sub

    ''' <summary>
    ''' 获取账户池行要显示的皮肤：离线用离线皮肤，其余尽量用已下载的皮肤，否则回退 Steve/Alex。
    ''' </summary>
    Private Function GetPoolAccountSkin(Type As McLoginType, Name As String) As String
        Select Case Type
            Case McLoginType.Legacy
                If SkinLegacy IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(SkinLegacy.Output) Then Return SkinLegacy.Output
            Case McLoginType.Ms
                If SkinMs IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(SkinMs.Output) Then Return SkinMs.Output
            Case McLoginType.Nide
                If SkinNide IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(SkinNide.Output) Then Return SkinNide.Output
            Case McLoginType.Auth
                If SkinAuth IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(SkinAuth.Output) Then Return SkinAuth.Output
        End Select
        Return GetAccountFallbackSkin(Name)
    End Function

    ''' <summary>
    ''' 依据玩家名生成确定的 Steve / Alex 头像兜底。
    ''' </summary>
    Private Function GetAccountFallbackSkin(Name As String) As String
        Try
            Return PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(If(Name, ""))) & ".png"
        Catch ex As Exception
            Return PathImage & "Skins/Steve.png"
        End Try
    End Function

    ''' <summary>账户切换反馈：账户胶囊做一次轻微缩放脉冲。</summary>
    Private Sub PulseAccountCapsule()
        Try
            If BtnAccount Is Nothing Then Return
            BtnAccount.RenderTransformOrigin = New Point(0.5, 0.5)
            If Not TypeOf BtnAccount.RenderTransform Is ScaleTransform Then
                BtnAccount.RenderTransform = New ScaleTransform(1, 1)
            End If
            AniStop("Account Capsule Pulse")
            AniStart({
                AaScaleTransform(BtnAccount, -0.05, 90, 0, New AniEaseOutFluent(AniEasePower.Weak)),
                AaScaleTransform(BtnAccount, 0.08, 200, 90, New AniEaseOutBack(AniEasePower.Weak)),
                AaScaleTransform(BtnAccount, -0.03, 120, 290, New AniEaseOutFluent(AniEasePower.Weak))
            }, "Account Capsule Pulse")
        Catch ex As Exception
            Logger.Warn(ex, "账户卡反馈动画失败")
        End Try
    End Sub

    ''' <summary>
    ''' 切换正版账号：复用微软登录加载器，用该账号的 RefreshToken 换取新 AccessToken。
    ''' </summary>
    Private Sub SwitchMsAccount(Name As String, Token As String)
        If String.IsNullOrWhiteSpace(Token) Then
            Hint("该账号缺少登录令牌，无法切换", HintType.Red)
            Return
        End If
        PanAccountMenu.Visibility = Visibility.Collapsed
        AccountPool.SetVersionAccount(New AccountRef With {.Type = McLoginType.Ms, .Name = Name})
        Hint($"正在切换账号 {Name}…")
        RunInNewThread(
        Sub()
            Try
                McLoginMsLoader.WaitForExit(New McLoginMs With {.OAuthRefreshToken = Token, .UserName = Name}, IsForceRestart:=True)
                RunInUi(
                Sub()
                    Try
                        FrmLaunchLeft.RefreshPage(False, True)
                        RefreshHomeInfo()
                        PulseAccountCapsule()
                        SkinMs.Start(IsForceRestart:=True) '强制刷新新账号皮肤头像
                    Catch ex As Exception
                        Logger.Error(ex, "切换正版账号后刷新失败")
                    End Try
                End Sub)
            Catch ex As Exception
                If Not ex.IsCanceled Then
                    Logger.Error(ex, $"切换正版账号失败（{Name}）", LogBehavior.Toast)
                End If
            End Try
        End Sub, "Ms Switch Account")
    End Sub

    ''' <summary>
    ''' 切换离线账号：复用离线用户名下拉框的选中项。
    ''' </summary>
    Private Sub SwitchLegacyAccount(Name As String)
        Try
            PanAccountMenu.Visibility = Visibility.Collapsed
            AccountPool.SetVersionAccount(New AccountRef With {.Type = McLoginType.Legacy, .Name = Name})
            If ComboName Is Nothing Then Return
            If ComboName.Items.Contains(Name) Then
                ComboName.SelectedItem = Name
            Else
                ComboName.Text = Name
            End If
            RefreshHomeInfo()
            PulseAccountCapsule()
            Hint($"已切换到离线账号 {Name}", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "切换离线账号失败")
        End Try
    End Sub

    ''' <summary>
    ''' 切换第三方账号：回填到对应登录页并展开，由用户点击「登录」完成校验。
    ''' </summary>
    Private Sub SwitchThirdPartyAccount(Token As String, Name As String, Pass As String)
        Try
            PanAccountMenu.Visibility = Visibility.Collapsed
            AccountPool.SetVersionAccount(New AccountRef With {.Type = If(Token = "Nide", McLoginType.Nide, McLoginType.Auth), .Name = Name})
            RefreshHomeInfo()
            PulseAccountCapsule()
            Hint($"已切换到 {Name}", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "切换第三方账号失败")
        End Try
    End Sub

    ''' <summary>
    ''' 移除账号：与既有删除逻辑使用同一套存储（不另造存储）。
    ''' </summary>
    Private Sub RemoveAccount(Name As String, Type As McLoginType)
        Try
            Select Case Type
                Case McLoginType.Ms
                    Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
                    If MsJson IsNot Nothing Then
                        MsJson.Remove(Name)
                        Settings.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None))
                    End If
                    If FrmLoginMs IsNot Nothing AndAlso FrmLoginMs.ComboAccounts IsNot Nothing Then
                        Dim Target As Object = Nothing
                        For Each It As Object In FrmLoginMs.ComboAccounts.Items
                            If TypeOf It Is MyListItem AndAlso CType(It, MyListItem).Title = Name Then
                                Target = It
                                Exit For
                            End If
                        Next
                        If Target IsNot Nothing Then
                            If FrmLoginMs.ComboAccounts.SelectedItem Is Target Then FrmLoginMs.ComboAccounts.SelectedIndex = 0
                            FrmLoginMs.ComboAccounts.Items.Remove(Target)
                        End If
                    End If
                Case McLoginType.Legacy
                    Dim Names As New List(Of String)
                    Names.AddRange(Settings.Get(Of String)("LoginLegacyName").ToString.Split("¨"))
                    Names.Remove(Name)
                    Settings.Set("LoginLegacyName", Names.Join("¨"c))
                    ReloadLegacyName(False)
                Case Else
                    Dim Token As String = Type.ToString()
                    Dim Dict As New Dictionary(Of String, String)
                    Dim Names As New List(Of String), Passs As New List(Of String)
                    If Not Settings.Get(Of String)("Login" & Token & "Email") = "" Then Names.AddRange(Settings.Get(Of String)("Login" & Token & "Email").Split("¨"))
                    If Not Settings.Get(Of String)("Login" & Token & "Pass") = "" Then Passs.AddRange(Settings.Get(Of String)("Login" & Token & "Pass").Split("¨"))
                    For i As Integer = 0 To Names.Count - 1
                        Dict.Add(Names(i), If(i < Passs.Count, Passs(i), ""))
                    Next
                    Dict.Remove(Name)
                    Settings.Set("Login" & Token & "Email", Dict.Keys.Join("¨"c))
                    Settings.Set("Login" & Token & "Pass", Dict.Values.Join("¨"c))
                    Dim KeyList As New List(Of String)(Dict.Keys)
                    Dim ValueList As New List(Of String)(Dict.Values)
                    Select Case Token
                        Case "Nide"
                            If FrmLoginNide IsNot Nothing AndAlso FrmLoginNide.ComboName IsNot Nothing Then
                                FrmLoginNide.ComboName.ItemsSource = KeyList
                                FrmLoginNide.ComboName.Text = If(KeyList.Any, KeyList(0), "")
                                FrmLoginNide.TextPass.Password = If(ValueList.Any, ValueList(0), "")
                            End If
                        Case "Auth"
                            If FrmLoginAuth IsNot Nothing AndAlso FrmLoginAuth.ComboName IsNot Nothing Then
                                FrmLoginAuth.ComboName.ItemsSource = KeyList
                                FrmLoginAuth.ComboName.Text = If(KeyList.Any, KeyList(0), "")
                                FrmLoginAuth.TextPass.Password = If(ValueList.Any, ValueList(0), "")
                            End If
                    End Select
            End Select
            '若删除的是当前版本选中的账户，则清除版本账户引用
            Dim Ref = AccountPool.GetVersionAccount()
            If Ref IsNot Nothing AndAlso Ref.Type = Type AndAlso Ref.Name = Name Then
                AccountPool.ClearVersionAccount()
            End If
            RefreshAccountMenu()
            RefreshHomeInfo()
        Catch ex As Exception
            Logger.Error(ex, "移除账号失败")
        End Try
    End Sub

#End Region

#Region "游玩日历与洞察"

    Private DisplayMonth As Date = New Date(Date.Now.Year, Date.Now.Month, 1)
    Private SelectedDay As Date = Date.Now.Date
    Private LastPlayLogVersion As Integer = -1
    Private LastCalendarMonth As Date = Date.MinValue
    Private LastSelectedDay As Date = Date.MinValue

    ''' <summary>刷新「日历」与「游玩洞察」模块。</summary>
    ''' <param name="Force">忽略缓存强制重建（例如实例列表刚加载完，图标要从占位图换成真实图标）。</param>
    Public Sub RefreshPlayStats(Optional Force As Boolean = False)
        Try
            If PanCalDays Is Nothing Then Return
            Dim V = PlayLogModule.PlayLogVersion
            If Not Force AndAlso V = LastPlayLogVersion AndAlso DisplayMonth = LastCalendarMonth AndAlso SelectedDay = LastSelectedDay AndAlso PanCalDays.Children.Count > 0 Then Return
            LastPlayLogVersion = V
            LastCalendarMonth = DisplayMonth
            LastSelectedDay = SelectedDay
            BuildCalendar()
            BuildInsights()
            '实例列表可能还没加载完，图标先占位；这里开始短周期重试，列表就绪后自动换成真实图标
            EnsureInstanceLogosSettled()
        Catch ex As Exception
            Logger.Warn(ex, "刷新游玩统计失败")
        End Try
    End Sub

    Private Sub BtnCalPrev_Click() Handles BtnCalPrev.Click
        DisplayMonth = DisplayMonth.AddMonths(-1)
        RefreshPlayStats()
    End Sub
    Private Sub BtnCalNext_Click() Handles BtnCalNext.Click
        DisplayMonth = DisplayMonth.AddMonths(1)
        RefreshPlayStats()
    End Sub
    Private Sub SelectDay(D As Date)
        SelectedDay = D.Date
        RefreshPlayStats()
    End Sub

    Private Function GetAccentColor() As Color
        Try
            Dim B = TryCast(TryFindResource("ColorBrush3"), SolidColorBrush)
            If B IsNot Nothing Then Return B.Color
        Catch
        End Try
        Return Colors.Green
    End Function

    Private Sub BuildCalendar()
        LabCalMonth.Text = DisplayMonth.ToString("yyyy年M月")
        '星期表头
        PanCalWeek.Children.Clear()
        For Each W As String In {"一", "二", "三", "四", "五", "六", "日"}
            Dim T As New TextBlock With {.Text = W, .FontSize = 11.5, .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(2)}
            T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
            PanCalWeek.Children.Add(T)
        Next
        '汇总每日分钟数
        Dim Sessions = PlayLogModule.LoadSessions()
        Dim DayMinutes As New Dictionary(Of Long, Integer)
        For Each S In Sessions
            Dim Key = S.StartTime.Date.Ticks
            DayMinutes(Key) = If(DayMinutes.ContainsKey(Key), DayMinutes(Key), 0) + CInt((S.EndTime - S.StartTime).TotalMinutes)
        Next
        '首格日期（周一开头）
        Dim Offset = (CInt(DisplayMonth.DayOfWeek) + 6) Mod 7
        Dim FirstDay = DisplayMonth.AddDays(-Offset)
        Dim Accent = GetAccentColor()
        PanCalDays.Children.Clear()
        For i As Integer = 0 To 41
            Dim D = FirstDay.AddDays(i)
            If D.Month <> DisplayMonth.Month Then
                PanCalDays.Children.Add(New Border With {.Height = 34, .Margin = New Thickness(2), .Background = Nothing})
                Continue For
            End If
            Dim Minutes As Integer = If(DayMinutes.ContainsKey(D.Ticks), DayMinutes(D.Ticks), 0)
            Dim Cell As New Button With {.Style = FindResource("CalDayCell"), .Height = 34, .Margin = New Thickness(2), .Padding = New Thickness(0), .HorizontalContentAlignment = HorizontalAlignment.Center}
            Dim Txt As New TextBlock With {.Text = D.Day.ToString, .FontSize = 12.5, .VerticalAlignment = VerticalAlignment.Center, .HorizontalAlignment = HorizontalAlignment.Center}
            '热度背景
            Dim Op As Double = 0
            If Minutes > 0 Then
                If Minutes < 15 Then
                    Op = 0.16
                ElseIf Minutes < 60 Then
                    Op = 0.32
                ElseIf Minutes < 120 Then
                    Op = 0.5
                ElseIf Minutes < 240 Then
                    Op = 0.7
                Else
                    Op = 0.92
                End If
                Cell.Background = New SolidColorBrush(Color.FromArgb(CByte(Op * 255), Accent.R, Accent.G, Accent.B))
            End If
            '选中的日期用同一圈描边表示（Tag 控制模板里的 Ring），不再往内容里塞额外的 Border，
            '避免悬浮时出现「两层框」或框与热度色块尺寸不一致
            If D = SelectedDay Then
                Txt.FontWeight = FontWeights.Bold
                Cell.BorderThickness = New Thickness(1.8)
                If Op >= 0.5 Then
                    Txt.Foreground = Brushes.White
                    Cell.Tag = "sel-dark"
                    Cell.BorderBrush = Brushes.White
                Else
                    Txt.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
                    Cell.Tag = "sel"
                    Cell.SetResourceReference(Border.BorderBrushProperty, "ColorBrush3")
                End If
            Else
                Cell.BorderThickness = New Thickness(0)
                If Op >= 0.5 Then
                    Txt.Foreground = Brushes.White
                Else
                    Txt.SetResourceReference(TextBlock.ForegroundProperty, "BrushText1")
                End If
            End If
            Cell.Content = Txt
            Dim DayDate = D
            AddHandler Cell.Click, Sub() SelectDay(DayDate)
            PanCalDays.Children.Add(Cell)
        Next
        BuildDayList(Sessions)
    End Sub

    Private Sub BuildDayList(Sessions As List(Of PlayLogModule.PlaySession))
        PanCalDayList.Children.Clear()
        ClearInstanceLogoTargets()
        '标题只放日期：有记录时下面是版本列表，没记录时下面写一句"这天没有游玩"，不用再拼"你游玩了："这种半截话
        LabCalDayTitle.Text = $"{SelectedDay.Month}月{SelectedDay.Day}日"
        Dim Total As New Dictionary(Of String, Long)
        For Each S In Sessions
            If S.StartTime.Date = SelectedDay Then
                Total(S.Name) = If(Total.ContainsKey(S.Name), Total(S.Name), 0) + S.DurationSeconds
            End If
        Next
        If Not Total.Any Then
            '没有记录：占住同一行高度（模块高度不变），用一句自然的灰字说明
            Dim T As New TextBlock With {.Text = "这天没有游玩", .FontSize = 12, .Height = 40,
                                         .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(2, 0, 0, 0)}
            T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
            PanCalDayList.Children.Add(T)
            ResetCalDayScroll()
            Return
        End If
        For Each Kvp In Total.OrderByDescending(Function(k) k.Value)
            PanCalDayList.Children.Add(BuildPlayRow(Kvp.Key, Kvp.Value))
        Next
        ResetCalDayScroll()
    End Sub

    ''' <summary>「当日游玩」区域回到第一条记录（避免切换日期后还停在旧的滚动位置）。</summary>
    Private Sub ResetCalDayScroll()
        Try
            If PanCalDayScroll IsNot Nothing Then PanCalDayScroll.ScrollToVerticalOffset(0)
        Catch ex As Exception
            Logger.Warn(ex, "重置当日游玩滚动失败")
        End Try
    End Sub

    ''' <summary>光标在「当日游玩」区域时，滚轮一次翻一个版本；模块高度固定为一行。</summary>
    Private Sub PanCalDayArea_MouseWheel(sender As Object, e As MouseWheelEventArgs)
        Try
            If PanCalDayScroll Is Nothing Then Return
            Dim StepH As Double = 36
            If PanCalDayList.Children.Count > 0 Then
                Dim Fe = TryCast(PanCalDayList.Children(0), FrameworkElement)
                If Fe IsNot Nothing AndAlso Fe.ActualHeight > 1 Then StepH = Fe.ActualHeight
            End If
            Dim Target As Double = PanCalDayScroll.VerticalOffset - Math.Sign(e.Delta) * StepH
            Target = Math.Max(0, Math.Min(PanCalDayScroll.ScrollableHeight, Target))
            PanCalDayScroll.ScrollToVerticalOffset(Target)
            e.Handled = True
        Catch ex As Exception
            Logger.Warn(ex, "滚动当日游玩记录失败")
        End Try
    End Sub

    Private Function BuildPlayRow(Name As String, Seconds As Long) As FrameworkElement
        '高度与「当日游玩」区域一致（40），保证一次只露出一行、不会看到下一条的边缘
        Dim Row As New Grid With {.Height = 40, .Margin = New Thickness(2, 0, 2, 0)}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim Icon As New Border With {.Width = 30, .Height = 30, .CornerRadius = New CornerRadius(7), .ClipToBounds = True, .VerticalAlignment = VerticalAlignment.Center}
        Dim Ico As New MyImage With {.Stretch = Stretch.UniformToFill}
        Icon.Child = Ico
        SetInstanceLogo(Ico, Name)
        Row.Children.Add(Icon)
        Dim Info As New StackPanel With {.Margin = New Thickness(10, 0, 0, 0), .VerticalAlignment = VerticalAlignment.Center}
        Dim N As New TextBlock With {.Text = Name, .FontSize = 12.5, .FontWeight = FontWeights.SemiBold, .TextTrimming = TextTrimming.CharacterEllipsis}
        N.SetResourceReference(TextBlock.ForegroundProperty, "BrushText1")
        Info.Children.Add(N)
        Dim D As New TextBlock With {.Text = FormatDuration(Seconds), .FontSize = 11.5, .Margin = New Thickness(0, 1, 0, 0)}
        D.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
        Info.Children.Add(D)
        Grid.SetColumn(Info, 1)
        Row.Children.Add(Info)
        Return Row
    End Function

    ''' <summary>
    ''' 实例图标待绑定项：实例列表可能是后加载完的（图标 Logo 那时才有值），
    ''' 所以先用占位图显示，等列表就绪后再统一重新绑定，避免"最近游玩"等处图标一直是启动器图标。
    ''' </summary>
    Private Class InstanceLogoTarget
        Public Img As MyImage
        Public Name As String
    End Class
    Private ReadOnly PendingInstanceLogos As New List(Of InstanceLogoTarget)
    ''' <summary>「最近游玩」自己的登记表（与日历当日列表分开，互不清理）。</summary>
    Private ReadOnly PendingRecentLogos As New List(Of InstanceLogoTarget)

    ''' <summary>设置实例图标并登记，列表加载完成后可再次重绑。</summary>
    Private Sub SetInstanceLogo(Img As MyImage, Name As String, Optional IntoRecent As Boolean = False)
        If Img Is Nothing Then Return
        Img.Source = GetInstanceLogoByName(Name)
        Dim Bucket As List(Of InstanceLogoTarget) = If(IntoRecent, PendingRecentLogos, PendingInstanceLogos)
        For Each T In Bucket
            If ReferenceEquals(T.Img, Img) Then
                T.Name = Name
                Return
            End If
        Next
        Bucket.Add(New InstanceLogoTarget With {.Img = Img, .Name = Name})
    End Sub

    ''' <summary>清空登记（重建列表前调用，避免旧控件泄漏）。</summary>
    Private Sub ClearInstanceLogoTargets(Optional OnlyRecent As Boolean = False)
        If OnlyRecent Then
            PendingRecentLogos.Clear()
        Else
            PendingInstanceLogos.Clear()
        End If
    End Sub

    ''' <summary>重新给所有已登记的图标解析一次（实例列表加载完成 / 延时兜底时调用）。</summary>
    Public Sub RefreshInstanceLogos()
        Try
            For Each T In PendingInstanceLogos
                If T.Img Is Nothing Then Continue For
                T.Img.Source = GetInstanceLogoByName(T.Name)
            Next
            For Each T In PendingRecentLogos
                If T.Img Is Nothing Then Continue For
                T.Img.Source = GetInstanceLogoByName(T.Name)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "刷新实例图标失败")
        End Try
    End Sub

    Private Function GetInstanceLogoByName(Name As String) As String
        Try
            If String.IsNullOrWhiteSpace(Name) Then Return PathImage & "SplashIcon.png"
            Dim Key As String = Name.Trim()
            '优先用当前实例：日历时可能在实例列表加载完成前就构建好了，那时查不到会落到启动器图标占位
            If McInstanceSelected IsNot Nothing AndAlso String.Equals(McInstanceSelected.Name.Trim(), Key, StringComparison.OrdinalIgnoreCase) AndAlso
                Not String.IsNullOrWhiteSpace(McInstanceSelected.Logo) Then
                Return McInstanceSelected.Logo
            End If
            Dim Inst = FindInstanceByName(Key)
            If Inst IsNot Nothing Then
                If Not String.IsNullOrWhiteSpace(Inst.Logo) Then Return Inst.Logo
                Return PathImage & "SplashIcon.png"
            End If
            '列表里也没有（改名 / 加载未完成 / 大小写或空格差异）：直接从版本文件夹里找图标，
            '这样即使实例列表没就绪，历史记录里也能显示真实图标，而不是一直停在启动器图标
            Dim FromDisk As String = FindInstanceLogoOnDisk(Key)
            If Not String.IsNullOrWhiteSpace(FromDisk) Then Return FromDisk
        Catch
        End Try
        Return PathImage & "SplashIcon.png"
    End Function

    ''' <summary>按版本名（= versions 下的文件夹名）直接在磁盘上找图标文件。</summary>
    ''' <summary>
    ''' 直接按版本文件夹解析实例图标（不依赖实例列表，冷启动时列表往往还是空的）：
    ''' ① YQL\Setup.ini 里缓存的 Logo（PCL 加载过版本就会写入，pack:// 或路径都能直接用）
    ''' ② 版本目录里的自定义图标文件  ③ 按加载器推断 PCL 自带的方块图标
    ''' </summary>
    Private Function FindInstanceLogoOnDisk(Name As String) As String
        Try
            If String.IsNullOrWhiteSpace(Name) OrElse String.IsNullOrWhiteSpace(McFolderSelected) Then Return Nothing
            Dim VersionsRoot As String = IO.Path.Combine(McFolderSelected, "versions")
            If Not IO.Directory.Exists(VersionsRoot) Then Return Nothing
            Dim Target As String = IO.Path.Combine(VersionsRoot, Name)
            If Not IO.Directory.Exists(Target) Then
                Target = Nothing
                For Each FolderPath In IO.Directory.EnumerateDirectories(VersionsRoot)
                    If String.Equals(IO.Path.GetFileName(FolderPath), Name, StringComparison.OrdinalIgnoreCase) Then
                        Target = FolderPath
                        Exit For
                    End If
                Next
            End If
            If String.IsNullOrEmpty(Target) Then Return Nothing
            '① 缓存里的 Logo（PCL 自己写进去的那份，例如 Blocks/Grass.png、Blocks/Anvil.png）
            Dim IniLogo As String = ReadIni(IO.Path.Combine(Target, "YQL\Setup.ini"), "Logo", "")
            If Not String.IsNullOrWhiteSpace(IniLogo) Then Return IniLogo
            '② 版本目录里的自定义图标
            For Each SubFolder In {"YQL\", ""}
                For Each Ext In {"png", "jpg", "jpeg", "ico", "bmp"}
                    For Each BaseName In {"Logo", "icon", "logo"}
                        Dim F As String = IO.Path.Combine(Target, SubFolder & BaseName & "." & Ext)
                        If IO.File.Exists(F) Then Return F
                    Next
                Next
            Next
            '③ 兜底：按文件夹名里的加载器推断，和 PCL 选图标的那套保持一致
            Dim Key As String = Name.ToLowerInvariant()
            If Key.IndexOf("neoforge", StringComparison.Ordinal) >= 0 Then Return PathImage & "Blocks/NeoForge.png"
            If Key.IndexOf("fabric", StringComparison.Ordinal) >= 0 OrElse
               Key.IndexOf("quilt", StringComparison.Ordinal) >= 0 Then Return PathImage & "Blocks/Fabric.png"
            If Key.IndexOf("forge", StringComparison.Ordinal) >= 0 Then Return PathImage & "Blocks/Anvil.png"
            If Key.IndexOf("optifine", StringComparison.Ordinal) >= 0 Then Return PathImage & "Blocks/GrassPath.png"
            If Key.IndexOf("liteloader", StringComparison.Ordinal) >= 0 Then Return PathImage & "Blocks/Egg.png"
            Return PathImage & "Blocks/Grass.png"
        Catch ex As Exception
            Logger.Warn(ex, "从磁盘查找实例图标失败")
        End Try
        Return Nothing
    End Function

    Private Function FormatDuration(Seconds As Long) As String
        If Seconds < 60 Then Return Math.Max(0, Seconds) & " 秒"
        Dim Min = CInt(Seconds \ 60)
        If Min < 60 Then Return Min & " 分钟"
        Dim H = Min \ 60, M = Min Mod 60
        Return H & " 小时" & If(M > 0, " " & M & " 分钟", "")
    End Function

    Private Sub BuildInsights()
        Dim Sessions = PlayLogModule.LoadSessions()
        Dim Today = Date.Now.Date
        Dim Offset = (CInt(Today.DayOfWeek) + 6) Mod 7
        Dim WeekStart = Today.AddDays(-Offset)
        Dim LastWeekStart = WeekStart.AddDays(-7)
        Dim ThisSec As Long = 0, LastSec As Long = 0
        Dim Fav As New Dictionary(Of String, Long)
        For Each S In Sessions
            Dim D = S.StartTime.Date
            If D >= WeekStart AndAlso D < WeekStart.AddDays(7) Then
                ThisSec += S.DurationSeconds
                Fav(S.Name) = If(Fav.ContainsKey(S.Name), Fav(S.Name), 0) + S.DurationSeconds
            ElseIf D >= LastWeekStart AndAlso D < WeekStart Then
                LastSec += S.DurationSeconds
            End If
        Next
        Dim WeekText = FormatDuration(ThisSec)
        If LastSec > 0 Then
            Dim Diff = ThisSec - LastSec
            Dim Pct = CInt(Math.Abs(Diff) * 100 / LastSec)
            WeekText &= "（比上周" & If(Diff <= 0, "少", "多") & " " & Pct & "%）"
        End If
        LabInsightWeek.Text = WeekText
        '连续天数
        Dim Days As New HashSet(Of Long)
        For Each S In Sessions : Days.Add(S.StartTime.Date.Ticks) : Next
        Dim Streak As Integer = 0
        Dim Cur As Date = If(Days.Contains(Today.Ticks), Today, Today.AddDays(-1))
        Do While Days.Contains(Cur.Ticks)
            Streak += 1
            Cur = Cur.AddDays(-1)
        Loop
        LabInsightStreak.Text = If(Streak > 0, Streak & " 天", "暂无记录")
        '常玩
        LabInsightFav.Text = If(Fav.Any, Fav.OrderByDescending(Function(k) k.Value).First.Key, "—")
    End Sub

#End Region

#Region "新闻"

    Private Shared NewsCache As String = Nothing
    Private Shared NewsFetchedAt As Date = Date.MinValue
    Private Const NewsApi As String = "https://news.bugjump.net/apis/versions/latest"
    Private Const NewsHomepage As String = "https://news.bugjump.net/News.json"
    Private NewsDetailLink As String = NewsHomepage
    ''' <summary>新闻接口的磁盘缓存有效期（分钟）= 半个月：启动时先用它立刻显示，不必每次等网络。</summary>
    Private Const NewsDiskMaxAgeMinutes As Integer = 15 * 24 * 60

    Private Shared ReadOnly Property NewsDiskPath As String
        Get
            Return Paths.Base & "YQL\Cache\NewsLatest.json"
        End Get
    End Property

    ''' <summary>
    ''' 刷新「新闻」单行信息（数据来自 PCL2-NewsHomepage API）。
    ''' </summary>
    '静默刷新只做一次，避免同一会话反复请求
    Private Shared NewsSilentRefreshed As Boolean = False

    Public Sub RefreshNews(Optional Force As Boolean = False, Optional Silent As Boolean = False)
        If LabNewsLine Is Nothing Then Return
        Try
            If Not Force AndAlso NewsCache IsNot Nothing AndAlso (Date.Now - NewsFetchedAt).TotalMinutes < 10 Then
                SetNewsLine(NewsCache)
                Return
            End If
            '会话内还没拉过：先看磁盘缓存（30 分钟内）直接显示，避免每次启动都等一次网络
            If Not Force AndAlso NewsCache Is Nothing Then
                Try
                    Dim F = NewsDiskPath
                    If FileUtils.Exists(F) AndAlso (Date.Now - IO.File.GetLastWriteTime(F)).TotalMinutes < NewsDiskMaxAgeMinutes Then
                        Dim DiskJson As String = FileUtils.ReadAsString(F)
                        If Not String.IsNullOrWhiteSpace(DiskJson) Then
                            NewsCache = DiskJson
                            NewsFetchedAt = IO.File.GetLastWriteTime(F)
                            SetNewsLine(DiskJson)
                            '缓存只负责"立刻显示"：超过 6 小时就在后台悄悄更新一次，不转圈也不打断
                            If Not NewsSilentRefreshed AndAlso (Date.Now - NewsFetchedAt).TotalHours > 6 Then
                                NewsSilentRefreshed = True
                                AniStart(AaCode(Sub() RefreshNews(Force:=True, Silent:=True), 1200), "News Silent Refresh")
                            End If
                            Return
                        End If
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "读取新闻磁盘缓存失败")
                End Try
            End If
            If Not Silent Then LabNewsLine.Text = "加载中…"
            RunInNewThread(
            Sub()
                Try
                    Dim Json As String = NetRequestByLoader(NewsApi, Timeout:=15000, IsJson:=True)
                    If String.IsNullOrWhiteSpace(Json) Then Throw New Exception("新闻服务器返回为空")
                    NewsCache = Json
                    NewsFetchedAt = Date.Now
                    Try
                        DirectoryUtils.Create(IO.Path.GetDirectoryName(NewsDiskPath))
                        FileUtils.Write(NewsDiskPath, Json)
                    Catch ex As Exception
                        Logger.Warn(ex, "写入新闻磁盘缓存失败")
                    End Try
                    RunInUi(Sub()
                                Try
                                    SetNewsLine(Json)
                                Catch ex As Exception
                                    Logger.Error(ex, "解析新闻数据失败")
                                End Try
                            End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "获取新闻失败")
                    If Not Silent Then RunInUi(Sub() LabNewsLine.Text = "新闻获取失败")
                End Try
            End Sub, "News Fetch")
        Catch ex As Exception
            Logger.Error(ex, "刷新新闻失败")
        End Try
    End Sub

    ''' <summary>
    ''' 把版本数据压缩成单行文本（最新正式版优先，其次快照版）。
    ''' </summary>
    Private Sub SetNewsLine(Json As String)
        Dim Root As JObject = TryCast(Json.DeserializeJson(), JObject)
        If Root Is Nothing Then
            LabNewsLine.Text = "暂无版本信息"
            Return
        End If
        Dim Node As JObject = TryCast(Root("release"), JObject)
        Dim TypeText As String = "正式版"
        If Node Is Nothing Then
            Node = TryCast(Root("snapshot"), JObject)
            TypeText = "快照版"
        End If
        If Node Is Nothing Then
            LabNewsLine.Text = "暂无版本信息"
            Return
        End If
        Dim Version As String = If(Node("title")?.ToString, "")
        If String.IsNullOrWhiteSpace(Version) Then Version = Node("version-id")?.ToString
        LabNewsLine.Text = "最新" & TypeText & " " & If(String.IsNullOrWhiteSpace(Version), "", Version)
        NewsDetailLink = If(Node("homepage-json-link")?.ToString, "")
        If String.IsNullOrWhiteSpace(NewsDetailLink) Then NewsDetailLink = NewsHomepage
        Try
            PageOtherHelpDetail.Prefetch(NewsDetailLink)
        Catch ex As Exception
            Logger.Warn(ex, "预取版本详情失败")
        End Try
    End Sub

    ''' <summary>诊断用：命令行 --opennews 时自动打开新闻页，用于测量耗时。</summary>
    '点击新闻单行，打开最新版本详情
    Private Sub BtnNewsLine_Click() Handles BtnNewsLine.Click
        Try
            PageOtherHelpDetail.Open(NewsDetailLink, "版本详情")
        Catch ex As Exception
            Logger.Error(ex, "打开版本详情失败", LogBehavior.Toast)
        End Try
    End Sub

#End Region

    ' ===== 添加账户（新设计） =====

    Private Sub BtnAddOffline_Click() Handles BtnAddOffline.Click
        Try
            Dim NewName = TextAddOfflineName.Text.Trim
            If NewName = "" Then Hint("玩家名不能为空", HintType.Red) : Return
            If Not NewName.RegexCheck("^[0-9A-Za-z_]+$") Then Hint("玩家名只能包含英文、数字、下划线", HintType.Red) : Return
            If NewName.Length > 16 Then Hint("玩家名至多 16 个字符", HintType.Red) : Return
            Dim Names As New List(Of String)
            If Settings.Get(Of String)("LoginLegacyName") <> "" Then Names.AddRange(Settings.Get(Of String)("LoginLegacyName").Split("¨"))
            If Names.Contains(NewName) Then Hint("该用户名已存在", HintType.Red) : Return
            Names.Add(NewName)
            Settings.Set("LoginLegacyName", Names.Join("¨"c))
            ReloadLegacyName(False)
            AccountPool.SetVersionAccount(New AccountRef With {.Type = McLoginType.Legacy, .Name = NewName})
            PanAddAccount.Visibility = Visibility.Collapsed
            TextAddOfflineName.Text = ""
            RefreshAccountMenu()
            RefreshHomeInfo()
            Hint($"已添加离线账户 {NewName}", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "添加离线账户失败")
        End Try
    End Sub

    Private Sub BtnAddMs_Click() Handles BtnAddMs.Click
        Try
            BtnAddMs.IsEnabled = False
            BtnAddMs.Content = "登录中…"
            RunInNewThread(
            Sub()
                Try
                    McLoginMsLoader.Start(New McLoginMs, IsForceRestart:=True)
                    Do While McLoginMsLoader.State = LoadState.Loading
                        Thread.Sleep(50)
                    Loop
                    If McLoginMsLoader.State = LoadState.Finished Then
                        RunInUi(Sub()
                            AccountPool.SetVersionAccount(New AccountRef With {.Type = McLoginType.Ms, .Name = Settings.Get(Of String)("CacheMsV2Name")})
                            PanAddAccount.Visibility = Visibility.Collapsed
                            BtnAddMs.Content = "登录微软账户"
                            RefreshAccountMenu()
                            RefreshHomeInfo()
                            Hint("微软账户已添加", HintType.Green)
                        End Sub)
                    Else
                        If McLoginMsLoader.Error IsNot Nothing Then Throw McLoginMsLoader.Error
                        Throw New Exception("未知错误")
                    End If
                Catch ex As Exception
                    If Not ex.IsCanceled Then Logger.Error(ex, "微软登录失败", LogBehavior.Alert)
                    RunInUi(Sub()
                        BtnAddMs.IsEnabled = True
                        BtnAddMs.Content = "登录微软账户"
                    End Sub)
                End Try
            End Sub, "Add Ms Account")
        Catch ex As Exception
            Logger.Error(ex, "添加微软账户失败")
        End Try
    End Sub

    Private Sub BtnAddNide_Click() Handles BtnAddNide.Click
        Try
            Dim Server = TextAddNideServer.Text.Trim
            Dim Email = TextAddNideEmail.Text.Trim.Replace("¨", "")
            Dim Pass = TextAddNidePass.Password.Trim.Replace("¨", "")
            If Server = "" Then Hint("请输入服务器 ID", HintType.Red) : Return
            If Email = "" OrElse Pass = "" Then Hint("请输入账号和密码", HintType.Red) : Return
            If McInstanceSelected IsNot Nothing Then Settings.Set("VersionServerNide", Server, Instance:=McInstanceSelected)
            Settings.Set("CacheNideServer", Server)
            AddThirdPartyAccount("Nide", Email, Pass)
            AccountPool.SetVersionAccount(New AccountRef With {.Type = McLoginType.Nide, .Name = Email})
            PanAddAccount.Visibility = Visibility.Collapsed
            TextAddNideServer.Text = "" : TextAddNideEmail.Text = "" : TextAddNidePass.Password = ""
            RefreshAccountMenu()
            RefreshHomeInfo()
            Hint($"已添加统一通行证账户 {Email}", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "添加统一通行证账户失败")
        End Try
    End Sub

    Private Sub BtnAddAuth_Click() Handles BtnAddAuth.Click
        Try
            Dim Server = TextAddAuthServer.Text.Trim
            Dim Email = TextAddAuthEmail.Text.Trim.Replace("¨", "")
            Dim Pass = TextAddAuthPass.Password.Trim.Replace("¨", "")
            If Server = "" Then Hint("请输入认证服务器地址", HintType.Red) : Return
            If Email = "" OrElse Pass = "" Then Hint("请输入账号和密码", HintType.Red) : Return
            If McInstanceSelected IsNot Nothing Then Settings.Set("VersionServerAuthServer", Server, Instance:=McInstanceSelected)
            Settings.Set("CacheAuthServerServer", Server)
            AddThirdPartyAccount("Auth", Email, Pass)
            AccountPool.SetVersionAccount(New AccountRef With {.Type = McLoginType.Auth, .Name = Email})
            PanAddAccount.Visibility = Visibility.Collapsed
            TextAddAuthServer.Text = "" : TextAddAuthEmail.Text = "" : TextAddAuthPass.Password = ""
            RefreshAccountMenu()
            RefreshHomeInfo()
            Hint($"已添加第三方账户 {Email}", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "添加第三方账户失败")
        End Try
    End Sub

    ''' <summary>把第三方账号写入对应的¨分隔存储（存在则更新密码）。</summary>
    Private Sub AddThirdPartyAccount(Token As String, Email As String, Pass As String)
        Dim Names As New List(Of String), Passs As New List(Of String)
        If Settings.Get(Of String)("Login" & Token & "Email") <> "" Then
            Dim RawNames = Settings.Get(Of String)("Login" & Token & "Email").Split("¨")
            Dim RawPasss = Settings.Get(Of String)("Login" & Token & "Pass").Split("¨")
            For i = 0 To RawNames.Length - 1
                If RawNames(i).Trim = "" Then Continue For
                Names.Add(RawNames(i))
                Passs.Add(If(i < RawPasss.Length, RawPasss(i), ""))
            Next
        End If
        Dim Idx = Names.IndexOf(Email)
        If Idx >= 0 Then
            Passs(Idx) = Pass
        Else
            Names.Add(Email) : Passs.Add(Pass)
        End If
        Settings.Set("Login" & Token & "Email", String.Join("¨", Names))
        Settings.Set("Login" & Token & "Pass", String.Join("¨", Passs))
    End Sub

    ' ==================== Hero 卡片悬停高亮 ====================

    ''' <summary>悬停：边框细腻提亮 + 卡内径向光晕淡入 + 背景微提亮（均 220~240ms 平滑过渡，零布局位移）。</summary>
    Private Sub PanHero_MouseEnter(sender As Object, e As MouseEventArgs) Handles PanHero.MouseEnter
        Try
            AniStart({
                AaColor(PanHero, System.Windows.Controls.Control.BorderBrushProperty, "ColorBrush4", 220),
                AaDouble(RectHeroGlow, System.Windows.UIElement.OpacityProperty, 1 - RectHeroGlow.Opacity, 240, , New AniEaseOutFluent(AniEasePower.Weak)),
                AaDouble(RectHeroTint, System.Windows.UIElement.OpacityProperty, 0.22 - RectHeroTint.Opacity, 240, , New AniEaseOutFluent(AniEasePower.Weak))
            }, "Hero Card Hover")
        Catch
        End Try
    End Sub

    Private Sub PanHero_MouseLeave(sender As Object, e As MouseEventArgs) Handles PanHero.MouseLeave
        Try
            AniStart({
                AaColor(PanHero, System.Windows.Controls.Control.BorderBrushProperty, "ColorBrush5", 260),
                AaDouble(RectHeroGlow, System.Windows.UIElement.OpacityProperty, -RectHeroGlow.Opacity, 280, , New AniEaseOutFluent(AniEasePower.Weak)),
                AaDouble(RectHeroTint, System.Windows.UIElement.OpacityProperty, 0.35 - RectHeroTint.Opacity, 280, , New AniEaseOutFluent(AniEasePower.Weak))
            }, "Hero Card Hover")
        Catch
        End Try
    End Sub

    ' ==================== 启动按钮悬停抬升与投影 ====================

    ''' <summary>悬停：轻微上浮，投影扩大加深（配合 MyButton 自带的变色与按压缩放）。</summary>
    Private Sub BtnLaunch_MouseEnter(sender As Object, e As MouseEventArgs) Handles BtnLaunch.MouseEnter
        Try
            If Not BtnLaunch.IsEnabled Then Return
            HeroBtnLift(-2, 36, 6)
        Catch
        End Try
    End Sub

    Private Sub BtnLaunch_MouseLeave(sender As Object, e As MouseEventArgs) Handles BtnLaunch.MouseLeave
        Try
            HeroBtnLift(0, 28, 4)
        Catch
        End Try
    End Sub

    ''' <summary>按下：下压归位，投影收紧（与 MyButton 自带的按压缩放叠加）。</summary>
    Private Sub BtnLaunch_PreviewDown(sender As Object, e As MouseButtonEventArgs) Handles BtnLaunch.PreviewMouseLeftButtonDown
        Try
            If Not BtnLaunch.IsEnabled Then Return
            HeroBtnLift(0, 20, 2)
        Catch
        End Try
    End Sub

    ''' <summary>抬起：恢复悬停态。</summary>
    Private Sub BtnLaunch_PreviewUp(sender As Object, e As MouseButtonEventArgs) Handles BtnLaunch.PreviewMouseLeftButtonUp
        Try
            If Not BtnLaunch.IsEnabled Then Return
            HeroBtnLift(-2, 36, 6)
        Catch
        End Try
    End Sub

    ''' <summary>平滑驱动按钮的上浮位移与投影参数（均为相对当前值的增量）。</summary>
    Private Sub HeroBtnLift(OffsetY As Double, Blur As Double, Depth As Double)
        Dim Trans = CType(HeroBtnBorder.RenderTransform, TranslateTransform)
        AniStart({
            AaTranslateY(HeroBtnBorder, OffsetY - Trans.Y, 160, , New AniEaseOutFluent(AniEasePower.Weak)),
            AaDouble(HeroBtnShadow, System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, Blur - HeroBtnShadow.BlurRadius, 180),
            AaDouble(HeroBtnShadow, System.Windows.Media.Effects.DropShadowEffect.ShadowDepthProperty, Depth - HeroBtnShadow.ShadowDepth, 180)
        }, "Hero Btn Lift")
    End Sub

End Class
