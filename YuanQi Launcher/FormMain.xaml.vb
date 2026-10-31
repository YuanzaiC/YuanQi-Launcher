Imports System.ComponentModel
Imports System.Windows.Interop
Imports System.Runtime.InteropServices

Public Class FormMain

#Region "基础"

    '更新日志
    Private Sub ShowUpdateLog(LastVersion As Integer)
        Dim FeatureCount As Integer = 0, BugCount As Integer = 0
        Dim FeatureList As New List(Of KeyValuePair(Of Integer, String))
        '统计更新日志条目
        If BuildType = BuildTypes.Release Then
            If LastVersion < 411 Then 'Release 2.14.0.0
                FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "新增：允许修改、添加、移除、更新已安装版本的 MC 版本以及各个组件，可从版本设置中进入"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：启动游戏时允许按 Esc 取消"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：允许复制主页的启动日志中的内容"))
                FeatureCount += 19
                BugCount += 26
            End If
            If LastVersion < 408 Then 'Release 2.13.1.1
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：版本设置中，添加打开截图文件夹的快捷方式"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：导出整合包时，可以勾选导出光影设置文件"))
                FeatureCount += 23
                BugCount += 28
            End If
            If LastVersion < 406 Then 'Release 2.13.0.1
                FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "新增：重做 Java 管理与相关设置，允许调整 Java 优先级、指定 Java 版本范围等"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：导出整合包时允许自动导出版本文件夹中的 Java"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：重做弹出提示的样式以及动画，以更符合现代 UI 审美"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：可能无法安装 OptiFine 26.1.2"))
                FeatureCount += 35
                BugCount += 17
            End If
            If LastVersion < 404 Then 'Release 2.12.8.2
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：Windows 7 无法正常联网"))
                FeatureCount += 1
                BugCount += 2
            End If
            If LastVersion < 402 Then 'Release 2.12.8.1
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：升级框架到 .NET Framework 4.8"))
                FeatureCount += 35
                BugCount += 21
            End If
            If LastVersion < 398 Then 'Release 2.12.7.3
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：下载游戏、整合包的稳定性"))
                FeatureCount += 1
                BugCount += 5
            End If
            If LastVersion < 391 Then 'Release 2.12.6.2
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：会为部分 Forge / NeoForge 选择不支持的 Java"))
                BugCount += 4
            End If
            If LastVersion < 389 Then 'Release 2.12.6.1
                If LastVersion = 387 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法使用 Java 25+ 启动 Forge"))
                FeatureCount += 4
                BugCount += 3
            End If
            If LastVersion < 387 Then 'Release 2.12.6
                FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：降低 Minecraft 的内存占用"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：Minecraft 使用 Java 25+ 时的一个性能问题"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法下载 NeoForge 26.1"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：部分关键词搜不到资源，特别是中文 Mod 搜索经常没有结果"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：老版本 Windows 无法启动游戏"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：连接 Mojang 的服务可能失败，提示错误码 421"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法访问爱发电"))
                FeatureCount += 24
                BugCount += 19
            End If
            If LastVersion < 383 Then 'Release 2.12.3
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：部分关键词搜不到资源，特别是中文 Mod 搜索经常没有结果"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：下载可能完全卡住，或是下载进度反复回退"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "新增：内存管理设置，可以选择 G1GC、ZGC 或分代 ZGC"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "优化：来自 Modrinth 的文件下载速度"))
                FeatureCount += 15
                BugCount += 16
            End If
            If LastVersion < 381 Then 'Release 2.12.2
                FeatureCount += 3
                BugCount += 13
            End If
            If LastVersion < 379 Then 'Release 2.12.1
                If LastVersion >= 376 Then
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "删除：暂时隐藏联机入口……不过只是暂时关闭，它还会回来的！"))
                End If
                FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "新增：适配新的 Minecraft 版本号系统与 Unobfuscated 版本"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：巨幅优化各个下载页面和 Mod 管理页面的性能"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "优化：更换披风时会显示当前使用的披风"))
                FeatureCount += 33
                BugCount += 29
            End If
            If LastVersion < 376 Then 'Release 2.11.2
                'FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "新增：联机功能！"))
                FeatureCount += 32
                BugCount += 21
            End If
        Else
            '5：          FEAT+
            '4：     IMP+ FEAT*
            '3：BUG+ IMP* FEAT-
            '2：BUG* IMP-
            '1：BUG-
            If LastVersion < 412 Then 'Snapshot 2.14.0.0
                FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "新增：允许修改、添加、移除、更新已安装版本的 MC 版本以及各个组件，可从版本设置中进入"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：启动游戏时允许按 Esc 取消"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：允许复制主页的启动日志中的内容"))
                FeatureCount += 19
                BugCount += 26
            End If
            If LastVersion < 410 Then 'Snapshot 2.13.1.1
                If LastVersion = 409 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：更新 Mod 时可能更新成另一个 Mod 加载器的版本"))
                FeatureCount += 2
                BugCount += 6
            End If
            If LastVersion < 409 Then 'Snapshot 2.13.1.0
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：版本设置中，添加打开截图文件夹的快捷方式"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：导出整合包时，可以勾选导出光影设置文件"))
                FeatureCount += 21
                BugCount += 22
            End If
            If LastVersion < 407 Then 'Snapshot 2.13.0.1
                If LastVersion = 405 Then
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：使用部分主页预设时崩溃"))
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法删除部分文件夹"))
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：版本设置中指定的 Java 无法被保存"))
                    FeatureCount += 1
                    BugCount += 3
                End If
                FeatureCount += 2
            End If
            If LastVersion < 405 Then 'Snapshot 2.13.0.0
                FeatureList.Add(New KeyValuePair(Of Integer, String)(4, "新增：重做 Java 管理与相关设置，允许调整 Java 优先级、指定 Java 版本范围等"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：导出整合包时允许自动导出版本文件夹中的 Java"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：重做弹出提示的样式以及动画，以更符合现代 UI 审美"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：可能无法安装 OptiFine 26.1.2"))
                FeatureCount += 35
                BugCount += 17
            End If
            If LastVersion < 403 Then 'Snapshot 2.12.8.2
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：Windows 7 无法正常联网"))
                FeatureCount += 1
                BugCount += 2
            End If
            If LastVersion < 401 Then 'Snapshot 2.12.8.1
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：可能无法更换披风"))
                FeatureCount += 8
                BugCount += 6
            End If
            If LastVersion < 399 Then 'Snapshot 2.12.8.0
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：升级框架到 .NET Framework 4.8"))
                FeatureCount += 28
                BugCount += 19
            End If
            If LastVersion < 397 Then 'Snapshot 2.12.7.3
                If LastVersion >= 393 AndAlso LastVersion < 397 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法使用自定义隐藏主题"))
            End If
            If LastVersion < 395 Then 'Snapshot 2.12.7.2
                If LastVersion = 393 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：部分个性化功能失效"))
            End If
            If LastVersion < 393 Then 'Snapshot 2.12.7.1
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：允许永久关闭启动页面的快照版提示"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：下载游戏、整合包的稳定性"))
                FeatureCount += 1
                BugCount += 5
            End If
            If LastVersion < 390 Then 'Snapshot 2.12.6.2
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：会为部分 Forge / NeoForge 选择不支持的 Java"))
                BugCount += 4
            End If
            If LastVersion < 388 Then 'Snapshot 2.12.6.1
                If LastVersion = 386 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法使用 Java 25+ 启动 Forge"))
                FeatureCount += 4
                BugCount += 3
            End If
            If LastVersion < 386 Then 'Snapshot 2.12.6
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：Minecraft 使用 Java 25+ 时的一个性能问题"))
                If LastVersion = 384 OrElse LastVersion = 385 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法正常重命名游戏版本"))
                If LastVersion = 385 Then FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法显示 Modrinth 整合包的版本列表"))
                BugCount += 1
            End If
            If LastVersion < 385 Then 'Snapshot 2.12.5
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法下载 NeoForge 26.1"))
                If LastVersion = 384 Then
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：下载可能失败，提示任务管理刷新线程出错"))
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：无法保存选择的 Minecraft 文件夹"))
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：使用中文搜索 Mod 时，部分结果会忽略筛选条件"))
                End If
                FeatureCount += 3
                BugCount += 6
            End If
            If LastVersion < 384 Then 'Snapshot 2.12.4
                FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：降低 Minecraft 的内存占用"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：部分关键词搜不到资源，特别是中文 Mod 搜索经常没有结果"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：老版本 Windows 无法启动游戏"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：连接 Mojang 的服务可能失败，提示错误码 421"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：无法访问爱发电"))
                FeatureCount += 21
                BugCount += 14
            End If
            If LastVersion < 382 Then 'Snapshot 2.12.3
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "修复：下载可能完全卡住，或是下载进度反复回退"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "新增：内存管理设置，可以选择 G1GC、ZGC 或分代 ZGC"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "优化：来自 Modrinth 的文件下载速度"))
                FeatureCount += 15
                BugCount += 16
            End If
            If LastVersion < 380 Then 'Snapshot 2.12.2
                FeatureCount += 3
                BugCount += 13
            End If
            If LastVersion < 378 Then 'Snapshot 2.12.1
                If LastVersion >= 377 Then
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "修复：在版本列表中存在 OptiFine 时可能无法加载版本列表"))
                    BugCount += 2
                End If
            End If
            If LastVersion < 377 Then 'Snapshot 2.12.0
                If LastVersion >= 373 Then
                    FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "删除：暂时隐藏联机入口……不过只是暂时关闭，它还会回来的！"))
                End If
                FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "新增：适配新的 Minecraft 版本号系统与 Unobfuscated 版本"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：巨幅优化各个下载页面和 Mod 管理页面的性能"))
                FeatureList.Add(New KeyValuePair(Of Integer, String)(1, "优化：更换披风时会显示当前使用的披风"))
                FeatureCount += 33
                BugCount += 29
            End If
            If LastVersion < 375 Then 'Snapshot 2.11.2
                'If LastVersion >= 373 Then
                '    FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：对联机进行了各种各样的优化，以改善稳定性"))
                '    FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：若有加入者的网络环境比房主更好，会提示可以让那位加入者担任房主"))
                'End If
                FeatureCount += 16
                BugCount += 4
            End If
            If LastVersion < 374 Then 'Snapshot 2.11.1
                'If LastVersion >= 373 Then
                '    FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：使用离线登录也可以直接加入联机房间了"))
                '    FeatureList.Add(New KeyValuePair(Of Integer, String)(3, "优化：会从所有共享节点中自动选择负载最低的进行中继连接"))
                '    FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：若复制了邀请码，则可以直接快速加入房间"))
                '    FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "优化：关闭 缘启 时总是会提示是否退出联机，防止在关闭 缘启 时无意地关闭或退出了房间"))
                '    FeatureList.Add(New KeyValuePair(Of Integer, String)(2, "新增：允许自定义要连接的节点"))
                'End If
                FeatureCount += 9
                BugCount += 7
            End If
            If LastVersion < 373 Then 'Snapshot 2.11.0
                'FeatureList.Add(New KeyValuePair(Of Integer, String)(5, "新增：联机功能！"))
                FeatureCount += 7
                BugCount += 10
            End If
        End If
        '整理更新日志文本
        Dim ContentList As New List(Of String)
        Dim SortedFeatures = FeatureList.OrderByDescending(Function(f) f.Key).ToList
        If Not SortedFeatures.Any() AndAlso FeatureCount = 0 AndAlso BugCount = 0 Then ContentList.Add("没有更新日志……")
        For i = 0 To Math.Min(9, SortedFeatures.Count - 1) '最多取 10 项
            ContentList.Add(SortedFeatures(i).Value)
        Next
        If SortedFeatures.Count > 10 Then FeatureCount += SortedFeatures.Count - 10
        If FeatureCount > 0 OrElse BugCount > 0 Then
            ContentList.Add(If(FeatureCount > 0, FeatureCount & " 项小调整与修改", "") &
                If(FeatureCount > 0 AndAlso BugCount > 0, "，", "") &
                If(BugCount > 0, "修复了 " & BugCount & " 个 Bug", "") &
                "，详见完整更新日志")
        End If
        Dim Content As String = "· " & ContentList.Join(vbCrLf & "· ")
        '输出更新日志
        RunInNewThread(
        Sub()
            If MyMsgBox(Content, "缘启已更新至" & VersionDisplay, "确定", "完整更新日志") = 2 Then
                OpenWebsite("https://meloong.com/afd/a/LTCat?tab=feed")
            End If
        End Sub, "UpdateLog Output")
    End Sub

    '窗口加载
    Private IsWindowLoadFinished As Boolean = False
    Public Sub New()
        ApplicationStartTick = GetTimeMs()
        '窗体参数初始化
        FrmMain = Me
        '主页较重（首次构建约 1s）：默认推迟到窗口首帧后再构建，避免拖慢启动；
        '开启“主页苏醒动画”时会在动画前同步构建（动画本身盖住构建耗时）
        '版本号改变
        Dim LastVersion As Integer = Settings.Get(Of Integer)("SystemLastVersionReg")
        If LastVersion < VersionCode Then
            '触发升级
            UpgradeSub(LastVersion)
        ElseIf LastVersion > VersionCode Then
            '触发降级
            DowngradeSub(LastVersion)
        End If
        '版本隔离设置迁移
        If Not Settings.HasSaved("LaunchArgumentIndieV2") Then
            If Settings.HasSaved("LaunchArgumentIndie") Then
                Logger.Info("从老版本迁移版本隔离")
                Settings.Set("LaunchArgumentIndieV2", Settings.Get(Of Integer)("LaunchArgumentIndie"))
            ElseIf HasIniKey("Setup", "LaunchVersionSelect") Then
                Logger.Info("从老版本升级，但此前未调整版本隔离，使用老的版本隔离默认值")
                Settings.Set("LaunchArgumentIndieV2", Settings.GetDefault("LaunchArgumentIndie"))
            Else
                Logger.Info("全新安装，使用新的版本隔离默认值")
                Settings.Set("LaunchArgumentIndieV2", Settings.GetDefault("LaunchArgumentIndieV2"))
            End If
        End If
        '刷新主题
        ThemeCheckAll(False)
        ThemeRefresh(Settings.Get(Of Integer)("UiLauncherTheme"))
        '注册拖拽事件（不能直接加 Handles，否则没用；#6340）
        [AddHandler](DragDrop.DragEnterEvent, New DragEventHandler(AddressOf HandleDrag), handledEventsToo:=True)
        [AddHandler](DragDrop.DragOverEvent, New DragEventHandler(AddressOf HandleDrag), handledEventsToo:=True)
        '加载 UI
        InitializeComponent()
        Opacity = 0
        '开启管理员权限下的文件拖拽
        If WindowsUtils.HasAdminRole() Then
            Static Helper As New DragHelper
            AddHandler SourceInitialized,
            Sub()
                Dim WpfHelper As New WindowInteropHelper(Me)
                Helper.HwndIntPtrSource = HwndSource.FromHwnd(WpfHelper.Handle)
                Helper.AddHook()
            End Sub
            AddHandler Closing, Sub() Helper.RemoveDragHook()
            AddHandler Helper.DragDrop, Sub() FileDrag(Helper.DropFilePaths)
        End If
        '切换到首页
        '模式提醒
        If BuildType = BuildTypes.Debug Then Hint("[开发者模式] 缘启正以开发者模式运行，这可能会造成严重的性能下降，请务必立即向开发者反馈此问题！", HintType.Red)
        If ModeDebug Then Hint("[调试模式] 缘启正以调试模式运行，这可能会导致性能下降，若无必要请不要开启！")
        '尽早执行的加载池
        McFolderListLoader.Start(0) '为了让下载已存在文件检测可以正常运行，必须跑一次；为了让启动按钮尽快可用，需要尽早执行；为了与 PageLaunchLeft 联动，需要为 0 而不是 GetUuid

        Logger.Info($"第二阶段加载用时：{GetTimeMs() - ApplicationStartTick} ms")
    End Sub
    ''' <summary>
    ''' 构建并挂载主页。因主页首次构建约需 1 秒，默认推迟到窗口首帧后的空闲时机执行，
    ''' 显著加快窗口出现时间；可安全重入（已构建时直接返回）。
    ''' </summary>
    Public Sub ConstructLaunchPages()
        If FrmLaunchLeft IsNot Nothing Then Return
        FrmLaunchLeft = New PageLaunchLeft
        FrmLaunchRight = New PageLaunchRight
        If Not IsNothing(FrmLaunchLeft.Parent) Then FrmLaunchLeft.SetValue(ContentPresenter.ContentProperty, Nothing)
        If Not IsNothing(FrmLaunchRight.Parent) Then FrmLaunchRight.SetValue(ContentPresenter.ContentProperty, Nothing)
        '启动页单页全宽：宽度交给列宽分配（左列 1*）而不是显式 Width。
        '此前这里直接挂载页面、没有走 PageChangeAnim 的「PageLeft.Width = PanMain.Width」，
        '页面又待在 Auto 列里，于是被压缩成内容的自然宽度，右侧空出一截没铺满（只有再次改变窗口大小时才被修好）。
        ApplyMainColumns(True)
        FrmLaunchLeft.Width = Double.NaN
        FrmLaunchLeft.HorizontalAlignment = HorizontalAlignment.Stretch
        PanMainLeft.Child = FrmLaunchLeft
        PageLeft = FrmLaunchLeft
        PanMainRight.Child = FrmLaunchRight
        PageRight = FrmLaunchRight
        FrmLaunchRight.PageState = MyPageRight.PageStates.ContentStay
        '开屏动画仍在播放时（构建被推迟到了动画停留期）：实例图标保持隐藏，等待飞行结束的交叉淡化交接
        If PanSplash IsNot Nothing AndAlso PanSplash.Visibility = Visibility.Visible Then
            FrmLaunchLeft.ImgInstanceLogo.Opacity = 0
            SplashSyncInstanceIcon()
        End If
    End Sub

    ''' <summary>
    ''' 首次渲染完成后：播放窗口淡入动画，并在这段淡入上挂开屏动画的其余时序。
    ''' 首帧渲染阻塞 UI 线程期间不能启动动画，否则动画时间被耗尽、表现为无淡入。
    ''' 时序安排在这里（而不是 Loaded），因为此刻窗口才真正开始可见：
    ''' 0ms 窗口淡入与卡片浮现并行 → 520ms 主页构建（画面静止在已浮现的卡片上，构建耗时不表现为掉帧）→ 随后起飞。
    ''' </summary>
    Private Sub FormMain_ContentRendered(sender As Object, e As EventArgs) Handles Me.ContentRendered
        '窗口从全透明浮现：时间压短（340→180ms）并改用先快后缓的曲线——一出手就见画面、收尾不生硬
        '（原来线性 340ms 会显得“慢慢渗出来”）
        Dim ShowAnims As New List(Of AniData) From {
            AaCode(Sub() AniControlEnabled -= 1, 30),
            AaOpacity(Me, Settings.Get(Of Integer)("UiLauncherTransparent") / 1000 + 0.4, 180, 20, New AniEaseOutFluent(AniEasePower.Middle)),
            AaCode(
            Sub()
                IsWindowLoadFinished = True
                Logger.Info($"DPI：{DPI}，系统版本：{Environment.OSVersion.VersionString}，缘启位置：{PathExe}")
            End Sub, , True),
            AaCode(AddressOf SplashShowCard, 140), '卡片随窗口淡入一起浮现（先以启动器图标占位，不等主页）
            AaCode(AddressOf DeferredHomeInit, If(PlainRevealMode, 200, 520)) '有开屏卡片时落在卡片静止停留里；纯白揭示则尽早构建，缩短白屏时间
        }
        '开屏动画会自己等构建完成后才揭示主页，收尾时再补界面刷新；
        '关掉开屏动画时则在这里直接把后续刷新排上
        If Not SplashIsActive Then ShowAnims.Add(AaCode(AddressOf DeferredHomeInitLate, 620))
        AniStart(ShowAnims.ToArray(), "Form Show")
    End Sub

    ''' <summary>主页是否已构建完成（开屏动画据此决定何时揭示）。</summary>
    Private SplashHomeReady As Boolean = False

    ''' <summary>
    ''' 窗口淡入完成后执行：只构建主页（开屏动画的落点依赖它）。
    ''' 背景图/标题栏/音乐这类开销较大的刷新拆到 DeferredHomeInitLate，等开屏动画结束再做，
    ''' 否则它们会和揭示/起飞动画抢 UI 线程（实测会各吃掉 250~300ms，表现为卡顿）。
    ''' 可安全重入。
    ''' </summary>
    Private Sub DeferredHomeInit()
        ConstructLaunchPages()
        SplashHomeReady = True
        '关掉开屏动画时：主页构建完再揭示（等一帧布局，别让用户先看到半成品）
        If PlainRevealMode Then AniStart(AaCode(AddressOf PlainRevealNow, 60), "Form Plain Reveal Prep")
    End Sub

    ''' <summary>开屏动画结束后的界面刷新（背景图、标题栏样式、音乐等）。可安全重入。</summary>
    Private Sub DeferredHomeInitLate()
        Try
            PageSetupUI.HiddenRefresh()
            PageSetupUI.BackgroundRefresh(False, True)
            MusicRefreshPlay(False, True)
        Catch ex As Exception
            Logger.Warn(ex, "启动界面刷新失败")
        End Try
    End Sub

    Private Sub FormMain_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ApplicationStartTick = GetTimeMs()
        Handle = New WindowInteropHelper(Me).Handle
        InstallMouseHook()
        '读取设置（HiddenRefresh/BackgroundRefresh 依赖主页控件，移至主页构建完成后执行）
        UpdateBackgroundAndTitleBar()
        ApplyUiScale() '应用保存的界面缩放比例
        '扩展按钮
        BtnExtraDownload.ShowCheck = AddressOf BtnExtraDownload_ShowCheck
        BtnExtraBack.ShowCheck = AddressOf BtnExtraBack_ShowCheck
        BtnExtraApril.ShowCheck = AddressOf BtnExtraApril_ShowCheck
        BtnExtraShutdown.ShowCheck = AddressOf BtnExtraShutdown_ShowCheck
        BtnExtraApril.ShowRefresh()
        '初始化尺寸改变
        Dim Resizer As New MyResizer(Me)
        Resizer.addResizerDown(ResizerB)
        Resizer.addResizerLeft(ResizerL)
        Resizer.addResizerLeftDown(ResizerLB)
        Resizer.addResizerLeftUp(ResizerLT)
        Resizer.addResizerRight(ResizerR)
        Resizer.addResizerRightDown(ResizerRB)
        Resizer.addResizerRightUp(ResizerRT)
        Resizer.addResizerUp(ResizerT)
        'PLC 彩蛋
        If RandomInteger(1, 1000) = 233 Then
            ShapeTitleLogo.Data = New GeometryConverter().ConvertFromString("M26,29 v-25 h6 a7,7 180 0 1 0,14 h-6 M83,6.5 a10,11.5 180 1 0 0,18 M48,2.5 v24.5 h13.5")
        End If
        '加载窗口
        ThemeRefreshMain()
        '左侧导航栏状态与标题栏面包屑
        Try
            IsRailCollapsed = Settings.Get(Of Boolean)("UiNavCollapsed")
        Catch ex As Exception
            IsRailCollapsed = False
        End Try
        RefreshRailLayout(False)
        LabTitleCrumb.Text = PageTitleText(PageCurrent)
        RefreshAccountAvatar()
        Try
            Height = Settings.Get(Of Integer)("WindowHeight")
            Width = Settings.Get(Of Integer)("WindowWidth")
            '若上次退出时处于最大化状态，窗口尺寸可能等于屏幕尺寸，这里收缩回可用区域
            Dim WorkArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea
            Dim MaxWidth As Double = Math.Max(MinWidth, GetWPFSize(WorkArea.Width) - 40)
            Dim MaxHeight As Double = Math.Max(MinHeight, GetWPFSize(WorkArea.Height) - 40)
            If Width > MaxWidth OrElse Height > MaxHeight Then
                Width = Math.Min(Width, MaxWidth)
                Height = Math.Min(Height, MaxHeight)
            End If
        Catch ex As Exception '修复 #2019
            Logger.Error(ex, "读取窗口默认大小失败", LogBehavior.Toast)
            Height = MinHeight + 100
            Width = MinWidth + 100
        End Try
        'MinHeight = 50
        'MinWidth = 50
        Topmost = False
        If FrmStart IsNot Nothing Then FrmStart.Close(New TimeSpan(0, 0, 0, 0, 400 / AniSpeed))
        '更改窗口
        Top = (GetWPFSize(My.Computer.Screen.WorkingArea.Height) - Height) / 2
        Left = (GetWPFSize(My.Computer.Screen.WorkingArea.Width) - Width) / 2
        IsSizeSaveable = True
        ShowWindowToTop()
        Dim HwndSource As Interop.HwndSource = PresentationSource.FromVisual(Me)
        HwndSource.AddHook(New Interop.HwndSourceHook(AddressOf WndProc))
        '窗口淡入：改在首次渲染完成后（ContentRendered）播放——首帧渲染会阻塞 UI 线 程数百毫秒，
        '若在 Loaded 直接启动淡入，动画时间会在渲染期间耗尽、表现为“窗口瞬间出现”
        '启动动画（主页苏醒）：立即开始，不再等待主页构建；主页构建被排入动画时间线（图标淡入
        '完成后的静止停留期，见 StartSplashAnimation），约 1s 的构建耗时被停留期盖住、肉眼不可见，
        '使“点击启动器 → 开屏动画开始”的间隔缩短约 1 秒（DeferredHomeInit 作为未开动画时的构建入口）
        Dim PlaySplashNow As Boolean = Not IsSplashPlayed
        StartSplashAnimation()

        'Timer 启动
        AniStart()
        TimerMainStart()
        '加载池
        RunInNewThread(
        Sub()
            '开屏动画播放期间先不启动加载池：否则网络/新闻/协议弹窗会和动画抢 CPU，导致掉帧
            '（动画时序现在挂在 ContentRendered 上，用 SplashIsActive 而不是动画组名判断）
            If PlaySplashNow Then
                Dim SplashWaitTick As Long = GetTimeMs()
                Do While SplashIsActive AndAlso GetTimeMs() - SplashWaitTick < 12000 '兜底 12s，避免异常状态下卡住加载池
                    Thread.Sleep(60)
                Loop
            End If
            StartInitLoaders()
        End Sub, "初始化", ThreadPriority.Lowest)

        Logger.Info($"第三阶段加载用时：{GetTimeMs() - ApplicationStartTick} ms")
    End Sub
    ''' <summary>
    ''' 启动后执行的初始化：协议授权、Java 初始化与各加载池。
    ''' 从 FormMain_Loaded 中拆出，以便在启动动画结束后再执行弹窗类初始化。
    ''' </summary>
    Private Sub StartInitLoaders()
        Try
            'EULA 提示
            Const EulaVersion As Integer = 2
            If Settings.Get(Of Integer)("SystemEulaVersion") < EulaVersion Then
                Select Case MyMsgBox(
                    If(Settings.Get(Of Integer)("SystemEulaVersion") = 0,
                        "在使用缘启前，请先阅读用户协议与免责声明。",
                        $"缘启的用户协议与免责声明已更新。{vbCrLf}请阅读更新后的用户协议与免责声明。"),
                        "协议授权", "同意", "拒绝", "查看用户协议与免责声明",
                        Button3Action:=Sub() OpenWebsite("https://shimo.im/docs/rGrd8pY8xWkt6ryW"))
                    Case 1
                        Settings.Set("SystemEulaVersion", EulaVersion)
                    Case 2
                        EndProgram(False)
                End Select
            End If
            '首次使用引导（跳过也视为看过，下次启动不再弹出）
            If MyMsgGuide.GuideVersion > Settings.Get(Of Integer)("SystemGuideVersion") Then
                MyMsgBoxGuide()
                MyMsgGuide.MarkFinished()
            End If
            '启动加载器池
            Try
                JavaInit() '延后到同意协议后再执行，避免在初次启动时进行进程操作
                Thread.Sleep(100)
                ModTerracotta.CleanupStaleInstances() '兜底清理上次被强杀时遗留的联机模块
                AutoEnvCheck() '首次运行做一次环境体检（只在有问题时提示）
                AutoCheckLauncherUpdate() '按设置自动检查更新（默认关闭）
                DlClientListMojangLoader.Start(1) '缘启会同时根据这里的加载结果决定是否使用官方源进行下载
                RunCountSub()
                ServerLoader.Start()
                RunInNewThread(AddressOf TryClearTaskTemp, "TryClearTaskTemp", ThreadPriority.BelowNormal)
            Catch ex As Exception
                Logger.Error(ex, "初始化加载池运行失败")
            End Try
            '清理自动更新文件
            Try
                FileUtils.Delete(Paths.Base & "YQL\YuanQi Launcher.exe")
            Catch ex As Exception
                Logger.Warn(ex, "清理自动更新文件失败")
            End Try
            '上报
            Telemetry("启动")
        Catch ex As Exception
            Logger.Error(ex, "启动初始化执行失败")
        End Try
    End Sub

#Region "启动动画：主页苏醒"

    ''' <summary>启动动画是否已播放（每次进程仅播放一次）。</summary>
    Private IsSplashPlayed As Boolean = False
    ''' <summary>开屏卡片是否已浮现（避免重复触发淡入）。</summary>
    Private SplashCardShown As Boolean = False
    ''' <summary>本次启动选中的特殊苏醒动画。彩蛋需要「先弹图标再播视频」，因此在揭示阶段才真正接管。</summary>
    Private SplashSpecialMode As ModSplashEvent.SpecialSplashTypes = ModSplashEvent.SpecialSplashTypes.None
    ''' <summary>彩蛋视频是否正在播放（播放期间主页可见但不可交互）。</summary>
    Private SplashVideoPlaying As Boolean = False
    ''' <summary>彩蛋视频的 WebView2 是否已初始化完成。</summary>
    Private SplashVideoReady As Boolean = False
    ''' <summary>彩蛋视频的兜底计时器：视频解码异常或未触发 ended 时强制收尾。</summary>
    Private SplashVideoWatchdog As Windows.Threading.DispatcherTimer = Nothing
    ''' <summary>彩蛋视频播到第几秒时揭示主页。</summary>
    Private Const SplashVideoRevealSecond As Double = 2.0
    ''' <summary>彩蛋视频结束前多少秒开始淡出。</summary>
    Private Const SplashVideoFadeOutSeconds As Double = 0.5
    ''' <summary>国庆动画：绛红渐变停留多久再继续原版飞行动画（毫秒）。</summary>
    Private Const SplashNationalDayRedHoldMs As Integer = 2000
    ''' <summary>国庆开屏渐变由白过渡到绛红所需时间（毫秒）。</summary>
    Private Const SplashNationalDayFadeMs As Integer = 800
    ''' <summary>国庆开屏渐变各停靠点，用于做「白 → 绛红」的颜色插值。</summary>
    Private SplashNationalStops As GradientStop() = Nothing
    ''' <summary>彩蛋动画期间始终显示启动器图标，不切换成当前实例的图标。</summary>
    Private SplashKeepLauncherLogo As Boolean = False

    ''' <summary>开屏覆盖层是否正在播放（动画结束后为 False）。</summary>
    Private SplashIsActive As Boolean = False    ''' <summary>开屏覆盖层是否正在播放（供页面判断是否需要让出/推迟自己的入场动画）。</summary>
    Public ReadOnly Property SplashPlaying As Boolean
        Get
            Return SplashIsActive
        End Get
    End Property

    ''' <summary>开屏动画起飞阶段等待主页构建的重试次数。</summary>
    Private SplashFlightRetryCount As Integer = 0

    ''' <summary>
    ''' 主页苏醒启动动画（简约）：窗口从无淡入为纯白界面，开屏卡片以高分辨率启动器图标轻柔淡入并轻微放大；
    ''' 起飞后短时间内图标与当前实例的图标交叉淡化（消失与浮现同时进行），卡片以平滑直线滑入 Hero 实例图标的实时位置，
    ''' 同时纯白底层淡出、主页内容与标题栏导航栏自然淡入；落位后与真实图标交叉淡化交接，
    ''' 动画结束的最后一帧即原生主页。
    ''' 全程基于真实 UI 元素，目标位置在起飞瞬间实时测量，不受窗口缩放与 DPI 影响。
    ''' 时序以「窗口淡入」为起点安排（见 FormMain_ContentRendered），保证任何时刻都没有重活落在动画中间。
    ''' </summary>
    Private Sub StartSplashAnimation()
        If IsSplashPlayed Then Return
        IsSplashPlayed = True
        Try
            '被快捷方式强制指定时，即使设置里关掉了启动动画也要播放（方便随时预览）
            Dim IsForced As Boolean = ModSplashEvent.ForcedType IsNot Nothing
            Dim SplashEnabled As Boolean = Settings.Get(Of Boolean)("UiLauncherSplash") OrElse IsForced
            '特殊苏醒动画（春节 / 国庆 / 彩蛋）：
            '快捷方式参数可强制指定（-spring / -national / -egg），否则按日期与概率自动判定。
            Try
                Dim Special = ModSplashEvent.Resolve()
                If Special <> ModSplashEvent.SpecialSplashTypes.None Then
                    Logger.Info($"本次启动使用特殊苏醒动画：{ModSplashEvent.GetTypeName(Special)}")
                    If SplashEnabled Then
                        '返回 True 表示该动画已完全接管；返回 False 表示先按普通流程走（彩蛋就是这种）
                        Select Case Special
                            Case ModSplashEvent.SpecialSplashTypes.SpringFestival
                                If StartSpringFestivalSplash() Then Return
                            Case ModSplashEvent.SpecialSplashTypes.NationalDay
                                If StartNationalDaySplash() Then Return
                            Case ModSplashEvent.SpecialSplashTypes.EasterEgg
                                If StartEasterEggSplash() Then Return
                        End Select
                    End If
                End If
            Catch ex As Exception
                Logger.Warn(ex, "特殊苏醒动画判定失败，将使用普通动画")
                SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.None
            End Try
            If Not SplashEnabled Then
                '没有开屏动画：改用「白色底层淡出」让启动器页面从无到有浮现（不浮现卡片、不做飞行动画）
                StartPlainReveal()
                Return
            End If
            If PanSplash Is Nothing OrElse PanForm.ActualWidth < 200 Then Return '布局异常时兜底
            SplashIsActive = True
            StartSplashWatchdog()
            '卡片占位图：这样窗口一淡入就能看到卡片，不必对着纯白界面等主页构建完；
            '主页就绪后由 SplashSyncInstanceIcon → SplashSwapIconFade 交叉淡化换成当前实例图标。
            '「打开启动器时显示 缘启 图标」开着时，卡片也用启动器图标（和前面那张系统开屏图一致，观感连贯）；
            '关掉时用 Minecraft 标题图，避免一张缘启图标凭空出现在白底上。
            If String.IsNullOrEmpty(ImgSplashInstance.Source) Then
                ImgSplashInstance.Source = If(Settings.Get(Of Boolean)("UiLauncherLogo"),
                                              PathImage & "SplashIcon.png",
                                              PathImage & "SplashMinecraft.png")
            End If
            '覆盖层可见；标题栏与导航栏暂隐
            PanSplash.Visibility = Visibility.Visible
            SplashBlock.Opacity = 0
            PanTitle.Opacity = 0
            PanNavRail.Opacity = 0
            PanExtraButtons.Opacity = 0
            '主页与背景不参与淡入：它们本来就被纯白覆盖层挡着，逐帧改整页透明度会让页面每帧重排重绘
            '（掉帧的主要来源之一）；这里直接置为最终值，交给白色底层一次性淡出揭示。
            PanMain.Opacity = 1
            PanMain.IsHitTestVisible = False
            ImgBack.Opacity = Settings.Get(Of Integer)("UiBackgroundOpacity") / 1000
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.ImgInstanceLogo.Opacity = 0 '实例图标先隐藏，等待卡片交接
            '这里不再自己排时序：卡片浮现与主页构建都挂在窗口淡入动画上（FormMain_ContentRendered），
            '以免构建耗时落在淡入/浮现动画中间造成掉帧。
        Catch ex As Exception
            Logger.Warn(ex, "启动动画执行失败，直接显示主页")
            SplashFinishInstant()
        End Try
    End Sub

    ''' <summary>让开屏卡片浮现并安排「揭示主页」。可安全重入：只有第一次调用生效。</summary>
    Private Sub SplashShowCard()
        If SplashCardShown OrElse Not SplashIsActive Then Return
        SplashCardShown = True
        Try
            If String.IsNullOrEmpty(ImgSplashInstance.Source) Then SplashSyncInstanceIcon() '兜底时至少放上当前实例图标
            If String.IsNullOrEmpty(ImgSplashInstance.Source) Then
                SplashFinishInstant() '连兜底图都取不到，直接显示主页
                Return
            End If
            AniStart({
                AaOpacity(SplashBlock, 1, 300, 0, New AniEaseOutFluent),
                AaDouble(SplashBlockScale, ScaleTransform.ScaleXProperty, 1 - SplashBlockScale.ScaleX, 300, 0, New AniEaseOutFluent),
                AaDouble(SplashBlockScale, ScaleTransform.ScaleYProperty, 1 - SplashBlockScale.ScaleY, 300, 0, New AniEaseOutFluent),
                AaCode(AddressOf SplashReveal, 460) '浮现后停一下：主页构建（520ms 触发）正好落在这段静止停留里
            }, "Form Splash Card")
        Catch ex As Exception
            Logger.Warn(ex, "开屏卡片浮现失败，直接显示主页")
            SplashFinishInstant()
        End Try
    End Sub

    ''' <summary>揭示重试次数。</summary>
    Private SplashRevealRetryCount As Integer = 0
    ''' <summary>稳定探测的时间戳：要求连续两次探测都稳定才揭示，避免恰好夹在建页/刷新之间。</summary>
    Private SplashSettleTick As Long = 0

    ''' <summary>
    ''' 主页是否已经「安静」下来：构建完成、Hero 图标完成布局、实例列表已就绪、页面自己的延迟刷新已跑完。
    ''' 只有安静下来才允许揭开白底 —— 否则用户会看到主页在卡顿中露出来。
    ''' </summary>
    Private Function IsHomeSettled() As Boolean
        If Not SplashHomeReady OrElse FrmLaunchLeft Is Nothing Then Return False
        Dim HeroImg As FrameworkElement = FrmLaunchLeft.ImgInstanceLogo
        If HeroImg Is Nothing OrElse Not HeroImg.IsArrangeValid Then Return False
        '实例列表正在加载时图标与文案还会变，卡片落点会漂（Waiting/已结束都算可用，避免永远等不到）
        If McInstanceListLoader IsNot Nothing AndAlso McInstanceListLoader.State = LoadState.Loading Then Return False
        '主页自身的延迟刷新还在跑
        If AniIsRun("Launch Left Loaded Refresh") Then Return False
        Return True
    End Function

    ''' <summary>
    ''' 揭示主页：白色底层淡出、标题栏与导航栏归位、主页模块依次出场。
    ''' 关键在于「全部放在起飞之前」：整屏半透明白底每帧都会让整个窗口重绘，
    ''' 若和卡片的位移叠在一起，飞行必然掉帧；先揭示完再起飞，飞行阶段窗口里就只有
    ''' 卡片这一小块（已转位图缓存）在变化，帧率才稳。
    ''' </summary>
    Private Sub SplashReveal()
        If Not SplashIsActive Then Return
        If Not IsHomeSettled() Then
            SplashSettleTick = 0
            SplashRevealRetryCount += 1
            If SplashRevealRetryCount <= 30 Then
                AniStart(AaCode(AddressOf SplashReveal, 80), "Form Splash Retry")
                Return
            End If
            '等太久就不要再拖了：主页只要能测量就直接揭示+起飞（宁可露一点，也不要不播动画）
            SplashRevealRetryCount = 0
            If Not SplashHomeReady OrElse FrmLaunchLeft Is Nothing OrElse FrmLaunchLeft.ImgInstanceLogo Is Nothing OrElse Not FrmLaunchLeft.ImgInstanceLogo.IsArrangeValid Then
                Logger.Warn("开屏动画等待主页就绪超时，直接显示主页")
                SplashFinishInstant()
                Return
            End If
            Logger.Warn("开屏动画等待主页稳定超时，提前揭示")
        End If
        '连续稳定 160ms 再揭示：把可能夹在中间的一次性刷新也等过去
        If SplashSettleTick = 0 Then
            SplashSettleTick = GetTimeMs()
            AniStart(AaCode(AddressOf SplashReveal, 160), "Form Splash Retry")
            Return
        End If
        SplashRevealRetryCount = 0
        SplashSettleTick = 0
        '彩蛋动画：图标已经弹出来了，这里改为播放视频（不再飞向主页）
        If SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.EasterEgg Then
            SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.None
            SplashBeginEasterEggVideo()
            Return
        End If
        '国庆动画：图标已经弹出来了，先把白底渐变成红色，再继续原版飞行动画
        If SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.NationalDay Then
            SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.None
            SplashFadeToFestival("国庆", {CType(PaletteRedDark, Color), CType(PaletteRedDark2, Color),
                                         CType(PaletteRedDeep, Color), CType(PaletteRedMain, Color)})
            Return
        End If
        '春节动画：同样的流程，目标色换成更暖更亮的灯火红
        If SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.SpringFestival Then
            SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.None
            SplashFadeToFestival("春节", {CType(PaletteSpringDark, Color), CType(PaletteSpringDark2, Color),
                                         CType(PaletteSpringRed, Color), CType(PaletteSpringRed2, Color)})
            Return
        End If
        '加载完成后短暂停留 0.7s 再开始（这段时间白底挡住一切，构建/刷新的掉帧都看不见），
        '随后「图标移动」与「主页逐渐显示」同时开始
        AniStart(AaCode(AddressOf SplashBeginFlight, 700), "Form Splash Reveal")
    End Sub

    ''' <summary>让开屏图形与主页当前实例的图标保持同源（实例列表异步加载后也会随之更新）。</summary>
    ''' <param name="ShowCard">主页已经确定当前实例图标时传 True：同步图标后立刻让卡片浮现。</param>
    Public Sub SplashSyncInstanceIcon(Optional ShowCard As Boolean = False)
        Try
            If PanSplash Is Nothing OrElse PanSplash.Visibility <> Visibility.Visible Then Return
            '彩蛋动画固定显示启动器图标，不切换成当前实例的图标
            If SplashKeepLauncherLogo Then
                If ShowCard AndAlso Not SplashCardShown Then SplashShowCard()
                Return
            End If
            Dim HeroSource As String = FrmLaunchLeft?.ImgInstanceLogo?.Source
            '只有确实是「实例图标」时才替换开屏卡片上的图；占位图（启动器图标 / Minecraft 标题图）不覆盖
            If Not String.IsNullOrEmpty(HeroSource) AndAlso Not IsSplashPlaceholder(HeroSource) AndAlso
               HeroSource <> ImgSplashInstance.Source Then
                If SplashBlock.Opacity > 0.05 Then
                    '卡片已经可见：用交叉淡化换图，避免图标突然跳变
                    SplashSwapIconFade()
                Else
                    '卡片尚未浮现：直接换源，随后淡入的就是新图标
                    ImgSplashInstance.Source = HeroSource
                    If SplashIconSwapTick = 0 Then SplashIconSwapTick = GetTimeMs()
                End If
            End If
            If ShowCard AndAlso Not IsSplashPlaceholder(HeroSource) Then SplashShowCard()
        Catch ex As Exception
            Logger.Warn(ex, "同步开屏实例图标失败")
        End Try
    End Sub

    ''' <summary>是否为「还没确定实例图标」时的占位图（启动器图标 / 开屏用的 Minecraft 标题图）。</summary>
    Private Function IsSplashPlaceholder(Source As String) As Boolean
        If String.IsNullOrEmpty(Source) Then Return True
        Dim S As String = Source.Replace("\"c, "/"c).ToLowerInvariant()
        Return S.EndsWith("/images/splashicon.png") OrElse S.EndsWith("/images/splashminecraft.png")
    End Function

    ''' <summary>移动过程中：卡片图标双层同步交叉淡化——启动器图标淡出的同时实例图标浮现，随后双层归位。</summary>
    Private Sub SplashSwapIconFade()
        Try
            If PanSplash Is Nothing OrElse PanSplash.Visibility <> Visibility.Visible Then Return
            If AniIsRun("Form Splash Icon") Then Return '上一次交叉淡化还没结束，避免叠加
            Dim HeroSource As String = FrmLaunchLeft.ImgInstanceLogo?.Source
            If String.IsNullOrEmpty(HeroSource) OrElse HeroSource = ImgSplashInstance.Source Then Return
            If SplashIconSwapTick = 0 Then SplashIconSwapTick = GetTimeMs()
            ImgSplashInstanceNext.Source = HeroSource
            ImgSplashInstanceNext.Opacity = 0
            AniStart({
                AaOpacity(ImgSplashInstanceNext, 1 - ImgSplashInstanceNext.Opacity, 260, , New AniEaseOutFluent),
                AaOpacity(ImgSplashInstance, -ImgSplashInstance.Opacity, 260, , New AniEaseOutFluent),
                AaCode(
                Sub()
                    ImgSplashInstance.Source = HeroSource '上层已完全浮现，将底层同步为新图标并复位双层
                    ImgSplashInstance.Opacity = 1
                    ImgSplashInstanceNext.Opacity = 0
                End Sub, 270)
            }, "Form Splash Icon")
        Catch ex As Exception
            Logger.Warn(ex, "开屏图标渐变切换失败")
        End Try
    End Sub

    ''' <summary>
    ''' 衔接阶段：实时测量 Hero 实例图标位置，让卡片以平滑直线滑入并缩放至图标尺寸；
    ''' 同时主页淡入苏醒，并在移动开始后的短时间内将卡片图标渐变为当前实例的图标。
    ''' 落位后与真实图标交叉淡化，随后折叠覆盖层。
    ''' </summary>
    Private Sub SplashBeginFlight()
        If Not SplashIsActive Then Return
        '等主页构建完成并完成一次布局：只有量到 Hero 图标的实际尺寸才能算出落点，
        '否则会飞向错误位置（甚至原地不动）。IsArrangeValid 保证不是「刚挂载、还没排版」的中间态。
        Dim HeroImg As FrameworkElement = Nothing
        If FrmLaunchLeft IsNot Nothing Then HeroImg = FrmLaunchLeft.ImgInstanceLogo
        If HeroImg Is Nothing OrElse Not HeroImg.IsArrangeValid OrElse HeroImg.ActualWidth <= 4 Then
            '主页尚未构建完成（构建被排在了卡片停留期）：稍后重试；持续不可用则立即结束动画保证主页可用
            SplashFlightRetryCount += 1
            If SplashFlightRetryCount > 40 Then
                Logger.Warn("开屏动画等待主页构建超时，直接显示主页")
                SplashFinishInstant()
            Else
                AniStart(AaCode(AddressOf SplashBeginFlight, 80), "Form Splash Retry")
            End If
            Return
        End If
        SplashFlightRetryCount = 0
        Try
            '落点必须是 Hero 图标的静态位置：先强制走完一次布局/动画合成，避免量到出场动画中途的位移
            PanForm.UpdateLayout()
            Dim BlockCenter As Point = SplashBlock.TransformToAncestor(PanForm).Transform(New Point(SplashBlock.ActualWidth / 2, SplashBlock.ActualHeight / 2))
            Dim HeroCenter As Point = HeroImg.TransformToAncestor(PanForm).Transform(New Point(HeroImg.ActualWidth / 2, HeroImg.ActualHeight / 2))
            Dim HeroSize As Double = HeroImg.ActualWidth
            Dim BlockDelta As New Vector(HeroCenter.X - BlockCenter.X, HeroCenter.Y - BlockCenter.Y)
            Dim BlockScale As Double = Math.Max(0.2, HeroSize / SplashBlock.ActualWidth)
            '沿二次贝塞尔曲线飞行（控制点垂直偏移 10% 距离），以连续缓动驱动：缓起 → 滑行 → 轻落；缩放先行到位
            Dim CtrlX As Double = BlockDelta.X / 2 - BlockDelta.Y * 0.1
            Dim CtrlY As Double = BlockDelta.Y / 2 + BlockDelta.X * 0.1
            Dim FlightProg As Double = 0
            '飞行期间把卡片转成位图缓存：位移/缩放交给合成器，不必每帧重绘圆角、描边与投影
            '（位图缓存会把超出元素边界的投影裁成硬边方形，所以飞行时先把投影去掉——此刻在快速移动，看不出来）
            SplashBlock.Effect = Nothing
            SplashBlock.CacheMode = New BitmapCache(RenderAtScale:=2)
            '此时主页已就绪并静置 0.7 秒：起飞与「主页逐渐显示」同时开始
            '（白色底层淡出、标题栏/导航栏归位、模块依次出场；Hero 不参与模块出场，落点保持不动）
            Dim ShowModules As ThreadStart = AddressOf FrmLaunchLeft.PlayDeferredModulesEnter
            SplashFlightStarted = True '从这里开始"图标在飞"：被切页面等打断时要把卡片收掉
            If SplashIconSwapTick > 0 Then Logger.Info($"开屏：卡片图标切成实例图标 → 开始移动，间隔 {GetTimeMs() - SplashIconSwapTick} ms")
            '国庆的绛红渐变层必须跟白底一起淡出，否则会一直盖住主页直到图标飞完
            AniStart({
                AaCode(AddressOf SplashSwapIconFade, 20),
                AaOpacity(RectSplashWhite, -RectSplashWhite.Opacity, 460, 40, New AniEaseOutFluent),
                AaOpacity(RectSplashNational, -RectSplashNational.Opacity, 460, 40, New AniEaseOutFluent),
                AaOpacity(RectSplashTexture, -RectSplashTexture.Opacity, 460, 40, New AniEaseOutFluent),
                AaOpacity(PanTitle, 1 - PanTitle.Opacity, 300, 80, New AniEaseOutFluent),
                AaOpacity(PanNavRail, 1 - PanNavRail.Opacity, 300, 80, New AniEaseOutFluent),
                AaOpacity(PanExtraButtons, 1 - PanExtraButtons.Opacity, 300, 80, New AniEaseOutFluent),
                AaCode(Sub() PanMain.IsHitTestVisible = True, 40),
                AaCode(ShowModules, 140),
                AaDouble(
                Sub(i)
                    FlightProg += i
                    Dim t As Double = FlightProg.Clamp(0, 1)
                    Dim Inv = 1 - t
                    SplashBlockPos.X = 2 * Inv * t * CtrlX + t * t * BlockDelta.X
                    SplashBlockPos.Y = 2 * Inv * t * CtrlY + t * t * BlockDelta.Y
                End Sub, 1, 880, 0, New AniEaseSplashGlide),
                AaDouble(SplashBlockScale, ScaleTransform.ScaleXProperty, BlockScale - SplashBlockScale.ScaleX, 720, 0, New AniEaseOutFluent(AniEasePower.Weak)),
                AaDouble(SplashBlockScale, ScaleTransform.ScaleYProperty, BlockScale - SplashBlockScale.ScaleY, 720, 0, New AniEaseOutFluent(AniEasePower.Weak)),
                AaOpacity(SplashBlock, -SplashBlock.Opacity, 260, 900, New AniEaseInFluent(AniEasePower.Weak)),
                AaOpacity(FrmLaunchLeft.ImgInstanceLogo, 1, 260, 900, New AniEaseOutFluent),
                AaCode(
                Sub()
                    PanSplash.Visibility = Visibility.Collapsed
                    SplashBlock.CacheMode = Nothing '撤掉位图缓存，恢复正常矢量渲染
                    SplashIsActive = False
                    Logger.Info("启动动画完成")
                    '开屏结束后再补背景图/标题栏/音乐等刷新：此时画面静止，短暂停顿不参与任何动画
                    AniStart(AaCode(AddressOf DeferredHomeInitLate, 200), "Form Splash After")
                End Sub, 1140)
            }, "Form Splash Move")
        Catch ex As Exception
            Logger.Warn(ex, "启动动画飞行阶段失败")
            SplashFinishInstant()
        End Try
    End Sub

    ''' <summary>开屏图标是否已进入「飞行」阶段（此后被打断就需要把卡片收掉）。</summary>
    Private SplashFlightStarted As Boolean = False
    ''' <summary>开屏卡片图标切成「当前实例图标」的时间点（GetTimeMs），用于测量到起飞的间隔。</summary>
    Private SplashIconSwapTick As Long = 0
    ''' <summary>执行过「打断收尾」就不再重复进入。</summary>
    Private SplashAborting As Boolean = False

    ''' <summary>
    ''' 保险：开屏图标飞行途中被外部打断（切换页面、动画被清掉、异常等）时，
    ''' 先让卡片与白底快速淡出（而不是硬切成主页），再走兜底恢复界面状态，
    ''' 避免卡片悬在半空、或 SplashIsActive 一直为 True 卡住后续动画。
    ''' </summary>
    Public Sub AbortSplashFlight(Optional Reason As String = "")
        If SplashAborting Then Return
        Try
            '飞行没开始（还在卡片停留期）就不处理：这时候切页面是动画之前的事
            If Not SplashIsActive AndAlso Not SplashFlightStarted Then Return
            SplashAborting = True
            Logger.Info($"开屏动画被打断（{Reason}），淡出卡片并恢复界面")
            AniStop("Form Splash Move")
            AniStop("Form Splash Card")
            AniStop("Form Splash Icon")
            AniStop("Form Splash Reveal")
            AniStop("Form Splash Retry")
            AniStop("Form Plain Reveal")
            AniStop("Form Plain Reveal Prep")
            Dim NeedFade As Boolean = SplashBlock IsNot Nothing AndAlso SplashBlock.Opacity > 0.01
            If NeedFade Then
                AniStart({
                    AaOpacity(SplashBlock, -SplashBlock.Opacity, 130, , New AniEaseInFluent),
                    AaOpacity(RectSplashWhite, -RectSplashWhite.Opacity, 170, , New AniEaseInFluent),
                    AaCode(AddressOf EndSplashAbort, 180, True)
                }, "Form Splash Abort", True)
            Else
                EndSplashAbort()
            End If
        Catch ex As Exception
            Logger.Warn(ex, "打断开屏动画失败")
            SplashAborting = False
            SplashFinishInstant()
        End Try
    End Sub

    Private Sub EndSplashAbort()
        Try
            SplashAborting = False
            SplashFinishInstant()
        Catch ex As Exception
            Logger.Warn(ex, "开屏打断收尾失败")
        End Try
    End Sub

    '---- 开屏看门狗：任何未知原因导致开屏没结束，超时强制收尾 ----

    Private SplashWatchdog As Windows.Threading.DispatcherTimer = Nothing

    Private Sub StartSplashWatchdog(Optional Seconds As Double = 8)
        StopSplashWatchdog()
        Try
            Dim T As New Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromSeconds(Seconds)}
            AddHandler T.Tick,
                Sub(s, e)
                    T.Stop()
                    If SplashIsActive Then
                        Logger.Warn("开屏动画超时未结束，强制恢复界面")
                        AbortSplashFlight("超时")
                    End If
                End Sub
            SplashWatchdog = T
            T.Start()
        Catch ex As Exception
            Logger.Warn(ex, "启动开屏看门狗失败")
        End Try
    End Sub

    Private Sub StopSplashWatchdog()
        Try
            If SplashWatchdog IsNot Nothing Then
                SplashWatchdog.Stop()
                SplashWatchdog = Nothing
            End If
        Catch ex As Exception
            Logger.Warn(ex, "停止开屏看门狗失败")
        End Try
    End Sub

    ''' <summary>立即结束启动动画并确保界面处于完整可用状态（异常与开关关闭时的兜底）。</summary>
    Private Sub SplashFinishInstant()
        Try
            AniStop("Form Splash")
            AniStop("Form Splash Card")
            AniStop("Form Splash Icon")
            AniStop("Form Splash Reveal")
            AniStop("Form Splash Move")
            AniStop("Form Splash Retry")
            AniStop("Form Plain Reveal")
            AniStop("Form Plain Reveal Prep")
            PlainRevealMode = False
            SplashFlightStarted = False
            SplashIconSwapTick = 0
            SplashAborting = False
            StopSplashWatchdog()
            If PanSplash IsNot Nothing Then PanSplash.Visibility = Visibility.Collapsed
            If RectSplashTexture IsNot Nothing Then RectSplashTexture.Visibility = Visibility.Collapsed
            If SplashBlock IsNot Nothing Then SplashBlock.CacheMode = Nothing
            SplashIsActive = False
            LabTitleLogo.Opacity = 1
            PanTitle.Opacity = 1
            PanNavRail.Opacity = 1
            PanMain.Opacity = 1
            PanMain.IsHitTestVisible = True
            PanExtraButtons.Opacity = 1
            ImgBack.Opacity = Settings.Get(Of Integer)("UiBackgroundOpacity") / 1000
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.ImgInstanceLogo.Opacity = 1
            '兜底结束时也要补上被推迟的界面刷新，避免背景图/标题栏/音乐没初始化
            AniStart(AaCode(AddressOf DeferredHomeInitLate, 200), "Form Splash After")
        Catch ex As Exception
            Logger.Warn(ex, "启动动画兜底恢复失败")
        End Try
    End Sub

    '===============================
    '  无开屏动画时的「纯白揭示」
    '===============================

    ''' <summary>是否处于「无开屏动画」的纯白揭示流程（白底已铺上，等主页构建完再淡出）。</summary>
    Private PlainRevealMode As Boolean = False

    ''' <summary>
    ''' 关掉「主页苏醒动画」时的启动效果：
    ''' 用同一套白色覆盖层当幕布（但不浮现卡片、不飞行），等主页构建完成再让白底淡出，
    ''' 于是启动器页面是「从无到有」地渐变出现，而不是先露一个空壳再突然塞进内容。
    ''' 这里把 SplashIsActive 保持为 True，是为了让加载池等淡入结束再启动，避免和淡入抢 UI 线程。
    ''' </summary>
    Private Sub StartPlainReveal()
        Try
            If PanSplash Is Nothing OrElse PanForm.ActualWidth < 200 Then Return
            PlainRevealMode = True
            SplashIsActive = True
            StartSplashWatchdog()
            SplashCardShown = True '不要浮现开屏卡片
            SplashBlock.Opacity = 0
            RectSplashWhite.Opacity = 1
            PanSplash.Visibility = Visibility.Visible
            '标题栏/导航栏/附加按钮从无到有，跟着页面一起浮现
            PanTitle.Opacity = 0
            PanNavRail.Opacity = 0
            PanExtraButtons.Opacity = 0
            '页面本身直接置于最终值，由白底淡出完成"从无到有"（逐帧改整页透明度会每帧重排，反而卡）
            PanMain.Opacity = 1
            PanMain.IsHitTestVisible = False
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.ImgInstanceLogo.Opacity = 1
        Catch ex As Exception
            Logger.Warn(ex, "准备启动淡入失败")
            SplashFinishInstant()
        End Try
    End Sub

    ''' <summary>主页构建完成后：白色幕布淡出（页面从无到有），标题栏/导航栏一起淡入，随后播放主页模块入场动画。</summary>
    Private Sub PlainRevealNow()
        If Not PlainRevealMode Then Return
        PlainRevealMode = False
        Try
            '主页此刻已构建：实例图标在 StartPlainReveal 时还取不到控件，这里补上（否则 Hero 图标是空的）
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.ImgInstanceLogo.Opacity = 1
            AniStart({
                AaCode(Sub() PanMain.IsHitTestVisible = True, 30),
                AaOpacity(RectSplashWhite, -RectSplashWhite.Opacity, 340, 40, New AniEaseOutFluent(AniEasePower.Middle)),
                AaOpacity(PanTitle, 1 - PanTitle.Opacity, 300, 80, New AniEaseOutFluent(AniEasePower.Middle)),
                AaOpacity(PanNavRail, 1 - PanNavRail.Opacity, 300, 80, New AniEaseOutFluent(AniEasePower.Middle)),
                AaOpacity(PanExtraButtons, 1 - PanExtraButtons.Opacity, 300, 80, New AniEaseOutFluent(AniEasePower.Middle)),
                AaCode(AddressOf PlayHomeModulesEnterAnimated, 150),
                AaCode(
                Sub()
                    PanSplash.Visibility = Visibility.Collapsed
                    SplashIsActive = False
                    Logger.Info("启动淡入完成")
                    '淡入结束后再补背景图/标题栏/音乐等重刷新，避免和淡入抢 UI 线程
                    AniStart(AaCode(AddressOf DeferredHomeInitLate, 200), "Form Splash After")
                End Sub, 420)
            }, "Form Plain Reveal", True)
        Catch ex As Exception
            Logger.Warn(ex, "启动淡入失败")
            SplashFinishInstant()
        End Try
    End Sub

    ''' <summary>补播主页模块入场动画（无开屏动画时模块入场被推迟到这里）。</summary>
    Private Sub PlayHomeModulesEnterAnimated()
        Try
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.PlayDeferredModulesEnterAnimated()
        Catch ex As Exception
            Logger.Warn(ex, "播放主页入场动画失败")
        End Try
    End Sub

    ''' <summary>
    ''' 启动飞行的缓动：smootherstep（6t⁵−15t⁴+10t³）。
    ''' 起止速度均为 0 且二阶连续，没有分段接缝处的速度突变，观感是一整段连续的「缓起 → 滑行 → 轻落」。
    ''' </summary>
    Private Class AniEaseSplashGlide
        Inherits AniEase
        Public Overrides Function GetValue(t As Double) As Double
            t = t.Clamp(0, 1)
            Return t * t * t * (t * (t * 6 - 15) + 10)
        End Function
    End Class

#Region "特殊苏醒动画（春节 / 国庆 / 彩蛋）"

    '以下三个方法由 StartSplashAnimation 调用，触发规则见 ModSplashEvent：
    '  · 春节：春节当天及前后 3 天
    '  · 国庆：10 月 1 日及前后 3 天
    '  · 彩蛋：每次启动 2% 概率
    '  · 快捷方式参数 -spring / -national / -egg 可强制触发
    '
    '约定：返回 True 表示本次动画已由该方法接管（后续自行负责收尾，例如调用 SplashFinishInstant）；
    '      返回 False 表示暂时不做处理，由 StartSplashAnimation 回退到普通苏醒动画。
    '目前动画尚未绘制，三个方法都直接返回 False。

    ''' <summary>
    ''' 国庆苏醒动画（10 月 1 日及前后 3 天）。
    ''' 流程：照常弹出启动器图标（不切换成实例图标）→ 白色底渐变为节日红 → 继续原版的「起飞」动画。
    ''' 这里只登记模式并返回 False，让普通流程先把图标弹出来；接管发生在 SplashReveal。
    ''' </summary>
    Private Function StartNationalDaySplash() As Boolean
        SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.NationalDay
        SplashKeepLauncherLogo = True '国庆动画固定显示启动器图标
        '开屏从纯白开始；图标弹出后再让这条渐变「变色」成绛红（不是叠一层半透明红，避免发灰）
        Try
            RectSplashWhite.Fill = New SolidColorBrush(New MyColor(255, 255, 255))
            If RectSplashNational IsNot Nothing Then
                Dim Brush As New LinearGradientBrush With {.StartPoint = New Point(0.1, 0), .EndPoint = New Point(0.75, 1)}
                Dim Offsets As Double() = {0, 0.35, 0.72, 1}
                Dim Stops(3) As GradientStop
                For i = 0 To 3
                    Stops(i) = New GradientStop(Colors.White, Offsets(i))
                    Brush.GradientStops.Add(Stops(i))
                Next
                SplashNationalStops = Stops
                RectSplashNational.Fill = Brush
                RectSplashNational.Opacity = 1 '整层常驻，靠停靠点变色实现过渡
            End If
            '金色只用在这里：给开屏卡片描一道细金线
            If SplashBlock IsNot Nothing Then SplashBlock.BorderBrush = New SolidColorBrush(PaletteGold)
        Catch ex As Exception
            Logger.Warn(ex, "设置国庆开屏样式失败")
        End Try
        Logger.Info("国庆苏醒动画：图标弹出后白底平滑过渡为绛红渐变，再继续原版动画")
        Return False '先走普通流程弹出图标
    End Function

    ''' <summary>
    ''' 白底平滑过渡到节日渐变（国庆绛红 / 春节暖红），停留一会儿后继续原有的飞行动画。
    ''' </summary>
    ''' <param name="Name">节日名称，仅用于日志。</param>
    ''' <param name="Targets">渐变各停靠点的目标色。</param>
    Private Sub SplashFadeToFestival(Name As String, Targets As Color())
        Try
            If RectSplashNational Is Nothing OrElse SplashNationalStops Is Nothing Then
                AniStart(AaCode(AddressOf SplashBeginFlight, 100), "Form Splash Reveal")
                Return
            End If
            '各停靠点从白插值到自己的目标色：整条渐变平滑「晕染」过去，中途不会发灰
            Dim Ease As New System.Windows.Media.Animation.CubicEase With {.EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut}
            For i = 0 To Math.Min(SplashNationalStops.Length, Targets.Length) - 1
                Dim Anim As New System.Windows.Media.Animation.ColorAnimation(Targets(i), TimeSpan.FromMilliseconds(SplashNationalDayFadeMs)) With {
                    .EasingFunction = Ease, .FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd}
                SplashNationalStops(i).BeginAnimation(GradientStop.ColorProperty, Anim)
            Next
            '过渡完成后让渐变停留一会儿，再继续原版动画
            AniStart(AaCode(AddressOf SplashBeginFlight, SplashNationalDayRedHoldMs), "Form Splash Reveal")
            Logger.Info($"{Name}苏醒动画：白底已平滑过渡为节日渐变（{SplashNationalDayFadeMs}ms），停留 {SplashNationalDayRedHoldMs} ms 后继续原版动画")
        Catch ex As Exception
            Logger.Warn(ex, $"{Name}苏醒动画渐变失败，直接继续普通动画")
            AniStart(AaCode(AddressOf SplashBeginFlight, 100), "Form Splash Reveal")
        End Try
    End Sub

    ''' <summary>
    ''' 春节苏醒动画（春节当天及前后 3 天）。
    ''' 与国庆同样的流程：照常弹出启动器图标 → 白底平滑过渡为暖红渐变 → 停留 → 继续原版动画。
    ''' 配色更暖更亮，金色更多（描边更粗 + 暖金光晕），并叠一层极低透明度的年味纹理。
    ''' </summary>
    Private Function StartSpringFestivalSplash() As Boolean
        SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.SpringFestival
        SplashKeepLauncherLogo = True '春节动画固定显示启动器图标
        Try
            RectSplashWhite.Fill = New SolidColorBrush(New MyColor(255, 255, 255))
            '与国庆共用同一套「停靠点变色」实现，只是目标色不同
            If RectSplashNational IsNot Nothing Then
                Dim Brush As New LinearGradientBrush With {.StartPoint = New Point(0.1, 0), .EndPoint = New Point(0.75, 1)}
                Dim Offsets As Double() = {0, 0.35, 0.72, 1}
                Dim Stops(3) As GradientStop
                For i = 0 To 3
                    Stops(i) = New GradientStop(Colors.White, Offsets(i))
                    Brush.GradientStops.Add(Stops(i))
                Next
                SplashNationalStops = Stops
                RectSplashNational.Fill = Brush
                RectSplashNational.Opacity = 1
            End If
            '低透明度年味纹理（细金线菱格），只做气味不做主角
            If RectSplashTexture IsNot Nothing Then
                RectSplashTexture.Opacity = 0.045
                RectSplashTexture.Visibility = Visibility.Visible
            End If
            '金色比国庆多：卡片描金加粗 + 暖金光晕
            If SplashBlock IsNot Nothing Then
                SplashBlock.BorderBrush = New SolidColorBrush(PaletteSpringGold)
                SplashBlock.BorderThickness = New Thickness(1.5)
                SplashBlock.Effect = New System.Windows.Media.Effects.DropShadowEffect With {
                    .Color = CType(PaletteSpringGold2, Color), .BlurRadius = 20, .ShadowDepth = 0,
                    .Opacity = 0.32, .RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance}
            End If
        Catch ex As Exception
            Logger.Warn(ex, "设置春节开屏样式失败")
        End Try
        Logger.Info("春节苏醒动画：图标弹出后白底平滑过渡为暖红渐变，再继续原版动画")
        Return False '先走普通流程弹出图标
    End Function

    ''' <summary>
    ''' 彩蛋苏醒动画。
    ''' 流程：照常弹出启动器图标 → 播放透明背景的苦力怕爆炸视频 →
    ''' 播到第 2 秒时把主页显示出来（不做额外动画）→ 视频播完后收尾。
    ''' 这里只登记模式并返回 False，让普通流程先把图标弹出来；真正的接管发生在 SplashReveal。
    ''' </summary>
    Private Function StartEasterEggSplash() As Boolean
        Dim VideoPath As String = GetEasterEggVideoPath()
        If String.IsNullOrEmpty(VideoPath) Then
            Logger.Warn("彩蛋动画缺少视频文件，将使用普通苏醒动画")
            Return False
        End If
        SplashSpecialMode = ModSplashEvent.SpecialSplashTypes.EasterEgg
        SplashKeepLauncherLogo = True '彩蛋只用启动器图标，不跟随当前实例
        Logger.Info($"彩蛋苏醒动画：视频 {VideoPath}")
        Return False '先走普通流程弹出图标
    End Function

    ''' <summary>
    ''' 彩蛋动画的视频文件路径。找不到时返回 Nothing。
    ''' </summary>
    Private Function GetEasterEggVideoPath() As String
        Dim Candidates As String() = {
            Paths.Base & "Videos\easter-egg.webm",
            Paths.Base & "YQL\Videos\easter-egg.webm",
            Paths.BaseThenName & "Videos\easter-egg.webm"
        }
        For Each Candidate As String In Candidates
            If FileUtils.Exists(Candidate) Then Return Candidate
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 图标弹出后启动彩蛋视频：显示透明视频层并播放。
    ''' </summary>
    Private Sub SplashBeginEasterEggVideo()
        Try
            If PanSplashVideo Is Nothing OrElse BrowserSplashVideo Is Nothing Then
                SplashFinishInstant()
                Return
            End If
            SplashVideoPlaying = True
            '视频时长未知，看门狗放宽，避免中途被强制收尾
            StartSplashWatchdog(45)
            PanSplashVideo.Visibility = Visibility.Visible
            BrowserSplashVideo.Visibility = Visibility.Visible
            BrowserSplashVideo.IsHitTestVisible = False
            InitSplashVideoBrowser()
        Catch ex As Exception
            Logger.Warn(ex, "彩蛋视频初始化失败，直接结束启动动画")
            FinishEasterEggVideo()
        End Try
    End Sub

    ''' <summary>
    ''' 初始化用于播放彩蛋视频的 WebView2（按需创建，透明背景，映射视频目录后用虚拟域名加载）。
    ''' </summary>
    Private Sub InitSplashVideoBrowser()
        If SplashVideoReady Then
            PlaySplashVideoInBrowser()
            Return
        End If
        Dim VideoPath As String = GetEasterEggVideoPath()
        If String.IsNullOrEmpty(VideoPath) Then
            FinishEasterEggVideo()
            Return
        End If
        '透明背景：让视频里 alpha 为 0 的区域显示下面的主页
        Try
            BrowserSplashVideo.DefaultBackgroundColor = System.Drawing.Color.Transparent
        Catch ex As Exception
            Logger.Warn(ex, "设置视频层透明背景失败")
        End Try
        'WebView2 合成控件的 D3D 表面按 DIP 分配，而页面按 dpr 渲染，于是只有左上角 1/dpr 的区域能进表面、
        '再被放大 dpr 倍显示。让页面内容自己缩到 1/dpr（见 PlaySplashVideoInBrowser 的 CSS）即可正好铺满。
        '这里只做日志，方便排查不同 DPI 下的表现。
        Dim Folder As String = IO.Path.GetDirectoryName(VideoPath)
        Dim FileName As String = IO.Path.GetFileName(VideoPath)
        Dim Env As Microsoft.Web.WebView2.Core.CoreWebView2Environment =
            Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(Nothing, Paths.Base & "YQL\WebView2").GetAwaiter().GetResult()
        BrowserSplashVideo.EnsureCoreWebView2Async(Env).ContinueWith(
            Sub(T)
                RunInUi(
                Sub()
                    Try
                        If T.IsFaulted Then
                            Logger.Warn(T.Exception, "彩蛋视频的 WebView2 初始化失败")
                            FinishEasterEggVideo()
                            Return
                        End If
                        Dim Core = BrowserSplashVideo.CoreWebView2
                        Core.Settings.AreDefaultContextMenusEnabled = False
                        Core.Settings.AreDevToolsEnabled = False
                        Core.Settings.IsStatusBarEnabled = False
                        Core.Settings.IsZoomControlEnabled = False
                        Core.Settings.AreBrowserAcceleratorKeysEnabled = False
                        Core.Settings.IsScriptEnabled = True
                        '把视频所在目录映射成虚拟站点，避免 file:// 被拦截
                        Core.SetVirtualHostNameToFolderMapping("splash.local", Folder,
                            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow)
                        AddHandler Core.WebMessageReceived, AddressOf SplashVideo_MessageReceived
                        SplashVideoReady = True
                        PlaySplashVideoInBrowser()
                    Catch ex As Exception
                        Logger.Warn(ex, "彩蛋视频层初始化失败")
                        FinishEasterEggVideo()
                    End Try
                End Sub)
            End Sub)
    End Sub

    ''' <summary>
    ''' 加载播放彩蛋视频的页面。页面在「第 2 秒」与「播放结束」时通过 postMessage 通知启动器。
    ''' </summary>
    Private Sub PlaySplashVideoInBrowser()
        Try
            Dim VideoPath As String = GetEasterEggVideoPath()
            If String.IsNullOrEmpty(VideoPath) Then
                FinishEasterEggVideo()
                Return
            End If
            Dim FileName As String = IO.Path.GetFileName(VideoPath)
            Dim Html As String =
                "<!DOCTYPE html><html><head><meta charset=""utf-8""><style>" &
                "html,body{margin:0;padding:0;width:100%;height:100%;background:transparent;overflow:hidden}" &
                "#wrap{position:fixed;left:0;top:0;width:100%;height:100%}" &
                "video{position:absolute;left:0;top:0;width:100%;height:100%;object-fit:cover;background:transparent;outline:none}" &
                "</style></head><body><div id=""wrap""><video id=""v"" autoplay muted playsinline preload=""auto""></video></div><script>" &
                "var v=document.getElementById('v');var revealed=false;var fading=false;" &
                "v.src='https://splash.local/" & Uri.EscapeDataString(FileName) & "';" &
                "function tick(){if(!revealed&&v.currentTime>=" & SplashVideoRevealSecond.ToString(System.Globalization.CultureInfo.InvariantCulture) & "){revealed=true;try{chrome.webview.postMessage('reveal');}catch(e){}}" &
                "if(!fading&&isFinite(v.duration)&&v.duration>0&&v.currentTime>=v.duration-" & SplashVideoFadeOutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) & "){fading=true;v.style.transition='opacity " & SplashVideoFadeOutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) & "s linear';v.style.opacity='0';}}" &
                "v.addEventListener('timeupdate',tick);" &
                "(function loop(){tick();requestAnimationFrame(loop);})();" &
                "v.addEventListener('ended',function(){try{chrome.webview.postMessage('ended');}catch(e){}});" &
                "v.addEventListener('error',function(){try{chrome.webview.postMessage('error');}catch(e){}});" &
                "v.play().catch(function(e){try{chrome.webview.postMessage('error');}catch(x){}});" &
                "</script></body></html>"
            BrowserSplashVideo.CoreWebView2.NavigateToString(Html)
            StartSplashVideoWatchdog()
            Try
                Dim Dpi As DpiScale = VisualTreeHelper.GetDpi(Me)
                Logger.Info($"彩蛋视频层：控件 {BrowserSplashVideo.ActualWidth:0}x{BrowserSplashVideo.ActualHeight:0}，DPI {Dpi.DpiScaleX:0.##}")
            Catch ex As Exception
                Logger.Warn(ex, "彩蛋视频尺寸诊断失败")
            End Try
        Catch ex As Exception
            Logger.Warn(ex, "彩蛋视频播放失败")
            FinishEasterEggVideo()
        End Try
    End Sub

    ''' <summary>接收视频页面的消息。</summary>
    Private Sub SplashVideo_MessageReceived(sender As Object, e As Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs)
        Dim Message As String = Nothing
        Try
            Message = e.TryGetWebMessageAsString()
        Catch
        End Try
        RunInUi(
        Sub()
            Select Case If(Message, "")
                Case "reveal"
                    '视频播到第 2 秒：把主页显示出来（不做额外动画）
                    SplashRevealHomeForVideo()
                Case "ended"
                    FinishEasterEggVideo()
                Case "error"
                    Logger.Warn("彩蛋视频播放出错，直接收尾")
                    FinishEasterEggVideo()
            End Select
        End Sub)
    End Sub

    ''' <summary>
    ''' 彩蛋视频播放中把主页显示出来：只把开屏覆盖层收起，不播放任何位移动画。
    ''' 此时 SplashIsActive 仍为 True，弹窗类初始化会继续等待，避免盖住视频。
    ''' </summary>
    Private Sub SplashRevealHomeForVideo()
        Try
            AniStop("Form Splash Card")
            PanSplash.Visibility = Visibility.Collapsed
            If SplashBlock IsNot Nothing Then SplashBlock.CacheMode = Nothing
            LabTitleLogo.Opacity = 1
            PanTitle.Opacity = 1
            PanNavRail.Opacity = 1
            PanExtraButtons.Opacity = 1
            PanMain.Opacity = 1
            PanMain.IsHitTestVisible = False '视频还在播，先不允许操作
            ImgBack.Opacity = Settings.Get(Of Integer)("UiBackgroundOpacity") / 1000
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.ImgInstanceLogo.Opacity = 1
            Logger.Info("彩蛋视频已播放到第 2 秒，主页已显示")
        Catch ex As Exception
            Logger.Warn(ex, "彩蛋视频显示主页失败")
        End Try
    End Sub

    ''' <summary>视频播放异常时的兜底计时器。</summary>
    Private Sub StartSplashVideoWatchdog()
        StopSplashVideoWatchdog()
        Try
            Dim T As New Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromSeconds(40)}
            AddHandler T.Tick,
                Sub(s, e)
                    T.Stop()
                    If SplashVideoPlaying Then
                        Logger.Warn("彩蛋视频超时未结束，强制收尾")
                        FinishEasterEggVideo()
                    End If
                End Sub
            SplashVideoWatchdog = T
            T.Start()
        Catch ex As Exception
            Logger.Warn(ex, "启动彩蛋视频看门狗失败")
        End Try
    End Sub

    Private Sub StopSplashVideoWatchdog()
        Try
            If SplashVideoWatchdog IsNot Nothing Then
                SplashVideoWatchdog.Stop()
                SplashVideoWatchdog = Nothing
            End If
        Catch
        End Try
    End Sub

    ''' <summary>彩蛋视频播放完毕：收起视频层并结束启动动画。</summary>
    Private Sub FinishEasterEggVideo()
        If Not SplashVideoPlaying Then
            SplashFinishInstant()
            Return
        End If
        SplashVideoPlaying = False
        StopSplashVideoWatchdog()
        Try
            If PanSplashVideo IsNot Nothing Then PanSplashVideo.Visibility = Visibility.Collapsed
            If BrowserSplashVideo IsNot Nothing Then BrowserSplashVideo.Visibility = Visibility.Collapsed
            '如视频异常（没触发 reveal），此时也要保证主页可见
            If PanSplash IsNot Nothing Then PanSplash.Visibility = Visibility.Collapsed
            If RectSplashTexture IsNot Nothing Then RectSplashTexture.Visibility = Visibility.Collapsed
            LabTitleLogo.Opacity = 1
            PanTitle.Opacity = 1
            PanNavRail.Opacity = 1
            PanExtraButtons.Opacity = 1
            PanMain.Opacity = 1
            PanMain.IsHitTestVisible = True
            ImgBack.Opacity = Settings.Get(Of Integer)("UiBackgroundOpacity") / 1000
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.ImgInstanceLogo.Opacity = 1
        Catch ex As Exception
            Logger.Warn(ex, "收尾彩蛋视频层失败")
        End Try
        SplashIsActive = False
        StopSplashWatchdog()
        Logger.Info("彩蛋苏醒动画结束")
        '通过快捷方式参数强制触发时给出可见反馈，便于确认参数确实生效
        Try
            If ModSplashEvent.ForcedType IsNot Nothing Then
                Hint("已按快捷方式参数播放彩蛋动画", HintType.Blue, False)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "提示彩蛋动画参数失效")
        End Try
        '视频只播这一次，释放掉浏览器实例，避免整个会话常驻一份 WebView2 内存
        Try
            If BrowserSplashVideo IsNot Nothing Then
                BrowserSplashVideo.Dispose()
            End If
        Catch ex As Exception
            Logger.Warn(ex, "释放彩蛋视频的 WebView2 失败")
        End Try
        Try
            AniStart(AaCode(AddressOf DeferredHomeInitLate, 200), "Form Splash After")
        Catch ex As Exception
            Logger.Warn(ex, "彩蛋动画结束后的界面刷新失败")
        End Try
    End Sub

#End Region

#End Region
    '根据打开次数触发的事件
    Private Sub RunCountSub()
        Settings.Set("SystemCount", Settings.Get(Of Integer)("SystemCount") + 1)
        If BuildType = BuildTypes.Snapshot Then
            Select Case Settings.Get(Of Integer)("SystemCount")
                Case 1
                    MyMsgBox("欢迎使用缘启快照版！" & vbCrLf &
                             "快照版包含尚未在正式版发布的测试性功能，仅用于赞助者本人尝鲜。所以请不要发给其他人或者用于制作整合包哦！" & vbCrLf &
                             "如果你并非通过赞助或赞助者本人邀请进群获得的本程序，那么可能是有人在违规传播，记得提醒他一下啦。", "快照版使用说明")
            End Select

        End If
    End Sub
    '升级与降级事件
    Private Sub UpgradeSub(LastVersionCode As Integer)
        Logger.Info($"版本号从 {LastVersionCode} 升高到 {VersionCode}")
        Settings.Set("SystemLastVersionReg", VersionCode)
        '检查有记录的最高版本号
        Dim HighestVersionCode As Integer
        Const SettingKey = If(BuildType = BuildTypes.Snapshot, "SystemHighestAlphaVersionReg", "SystemHighestBetaVersionReg")
        HighestVersionCode = Settings.Get(Of Integer)(SettingKey)
        If HighestVersionCode < VersionCode Then
            Settings.Set(SettingKey, VersionCode)
            Logger.Info($"最高版本号从 {HighestVersionCode} 升高到 {VersionCode}")
        End If
        '被移除的窗口设置选项
        If Settings.Get(Of Integer)("LaunchArgumentWindowType") = 5 Then Settings.Set("LaunchArgumentWindowType", 1)
        '修改主题设置项名称
        If HighestVersionCode <= 207 Then
            Dim UnlockedTheme As New List(Of String) From {"2"}
            UnlockedTheme.AddRange(New List(Of String)(Settings.Get(Of String)("UiLauncherThemeHide").ToString.Split("|")))
            UnlockedTheme.AddRange(New List(Of String)(Settings.Get(Of String)("UiLauncherThemeHide2").ToString.Split("|")))
            Settings.Set("UiLauncherThemeHide2", UnlockedTheme.Distinct.ToList.Join("|"c))
        End If
        '重置欧皇彩
        If LastVersionCode <= 115 AndAlso Settings.Get(Of String)("UiLauncherThemeHide2").ToString.Split("|").Contains("13") Then
            Dim UnlockedTheme As New List(Of String)(Settings.Get(Of String)("UiLauncherThemeHide2").ToString.Split("|"))
            UnlockedTheme.Remove("13")
            Settings.Set("UiLauncherThemeHide2", UnlockedTheme.Join("|"c))
            MyMsgBox("由于新版缘启修改了欧皇彩的解锁方式，你需要重新解锁欧皇彩。" & vbCrLf &
                     "多谢各位的理解啦！", "重新解锁提醒")
        End If
        '重置滑稽彩
        If LastVersionCode <= 152 AndAlso Settings.Get(Of String)("UiLauncherThemeHide2").ToString.Split("|").Contains("12") Then
            Dim UnlockedTheme As New List(Of String)(Settings.Get(Of String)("UiLauncherThemeHide2").ToString.Split("|"))
            UnlockedTheme.Remove("12")
            Settings.Set("UiLauncherThemeHide2", UnlockedTheme.Join("|"c))
            MyMsgBox("由于新版缘启修改了滑稽彩的解锁方式，你需要重新解锁滑稽彩。" & vbCrLf &
                     "多谢各位的理解啦！", "重新解锁提醒")
        End If
        '移动自定义皮肤
        If LastVersionCode <= 161 AndAlso FileUtils.Exists(Paths.Base & "YQL\CustomSkin.png") AndAlso Not FileUtils.Exists(Paths.AppDataThenName & "CustomSkin.png") Then
            FileUtils.Copy(Paths.Base & "YQL\CustomSkin.png", Paths.AppDataThenName & "CustomSkin.png")
            Logger.Info("已移动离线自定义皮肤 (162)")
        End If
        If LastVersionCode <= 263 AndAlso FileUtils.Exists(PathTemp & "CustomSkin.png") AndAlso Not FileUtils.Exists(Paths.AppDataThenName & "CustomSkin.png") Then
            FileUtils.Copy(PathTemp & "CustomSkin.png", Paths.AppDataThenName & "CustomSkin.png")
            Logger.Info("已移动离线自定义皮肤 (264)")
        End If

        '单向迁移微软登录结果（#4836）
        If Not Settings.Get(Of Boolean)("CacheMsV2Migrated") Then
            Settings.Set("CacheMsV2Migrated", True)
            Settings.Set("CacheMsV2OAuthRefresh", Settings.Get(Of String)("CacheMsOAuthRefresh"))
            Settings.Set("CacheMsV2Access", Settings.Get(Of String)("CacheMsAccess"))
            Settings.Set("CacheMsV2ProfileJson", Settings.Get(Of String)("CacheMsProfileJson"))
            Settings.Set("CacheMsV2Uuid", Settings.Get(Of String)("CacheMsUuid"))
            Settings.Set("CacheMsV2Name", Settings.Get(Of String)("CacheMsName"))
            Logger.Info("已从老版本迁移微软登录结果")
        End If
        'Mod 命名设置迁移
        If Settings.HasSaved("ToolDownloadTranslate") AndAlso Not Settings.HasSaved("ToolDownloadTranslateV2") Then
            Settings.Set("ToolDownloadTranslateV2", Settings.Get(Of Integer)("ToolDownloadTranslate") + 1)
            Logger.Info("已从老版本迁移 Mod 命名设置")
        End If
        '重置 JVM 参数设置
        If LastVersionCode <= 381 AndAlso Settings.HasSaved("LaunchAdvanceJvm") AndAlso
           Settings.Get(Of String)("LaunchAdvanceJvm").ToString.Replace("-XX:+UseG1GC ", "").Replace("-XX:-UseAdaptiveSizePolicy ", "").Trim = Settings.GetDefault("LaunchAdvanceJvm") Then
            Settings.Reset("LaunchAdvanceJvm")
            Logger.Info("已重置 JVM 参数设置")
        End If
        '输出更新日志
        If LastVersionCode <= 0 Then Return
        If HighestVersionCode >= VersionCode Then Return
        ShowUpdateLog(HighestVersionCode)
    End Sub
    Private Sub DowngradeSub(LastVersionCode As Integer)
        Logger.Info($"版本号从 {LastVersionCode} 降低到 {VersionCode}")
        Settings.Set("SystemLastVersionReg", VersionCode)
    End Sub

#End Region

#Region "自定义窗口"

    '关闭
    Private Sub FormMain_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        EndProgram(True)
        e.Cancel = True
    End Sub
    ''' <summary>
    ''' 正常关闭程序。程序将在执行此方法后约 0.3s 退出。
    ''' </summary>
    ''' <param name="SendWarning">是否在还有下载任务未完成时发出警告。</param>
    Public Sub EndProgram(SendWarning As Boolean)
        '强行结束下载任务？
        If HasDownloadingTask() Then
            If SendWarning AndAlso MyMsgBox("还有下载任务尚未完成，是否确定退出？", "提示", "确定", "取消") = 2 Then Return
            RunInNewThread(
            Sub()
                Logger.Info("正在强行停止任务")
                For Each Task As LoaderBase In LoaderTaskbar.ToList()
                    Task.Cancel()
                Next
            End Sub, "强行停止下载任务")
        End If
        '关闭联机？
        If FrmLinkMain?.TryExit(Not SendWarning, True) Then Return
        '关闭
        RunInUiWait(
        Sub()
            Try
                PageWeb.SaveCookiesAny() '退出前尽力保存网页登录状态（正常保存由导航与定时器完成）
            Catch
            End Try
            IsHitTestVisible = False
            If PanBack.RenderTransform Is Nothing Then
                Dim TransformPos As New TranslateTransform(0, 0)
                Dim TransformRotate As New RotateTransform(0)
                Dim TransformScale As New ScaleTransform(1, 1)
                PanBack.RenderTransform = New TransformGroup() With {.Children = New TransformCollection({TransformRotate, TransformPos, TransformScale})}
                AniStart({
                    AaOpacity(Me, -Opacity, 140, 40, New AniEaseOutFluent(AniEasePower.Weak)),
                    AaDouble(
                    Sub(i)
                        TransformScale.ScaleX += i
                        TransformScale.ScaleY += i
                    End Sub, 0.88 - TransformScale.ScaleX, 180),
                    AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 180, 0, New AniEaseOutFluent(AniEasePower.Weak)),
                    AaDouble(Sub(i) TransformRotate.Angle += i, 0.6 - TransformRotate.Angle, 180, 0, New AniEaseInoutFluent(AniEasePower.Weak)),
                    AaCode(
                    Sub()
                        IsHitTestVisible = False
                        Top = -10000
                        ShowInTaskbar = False
                    End Sub, 210),
                    AaCode(AddressOf EndProgramForce, 230)
                }, "Form Close")
            Else
                EndProgramForce()
            End If
            Logger.Info("收到关闭指令")
        End Sub)
    End Sub
    Private Shared IsLogShown As Boolean = False
    Public Shared Sub EndProgramForce(Optional ReturnCode As ProcessReturnValues = ProcessReturnValues.Success)
        On Error Resume Next
        IsProgramEnding = True
        AniControlEnabled += 1
        If IsUpdateWaitingRestart Then UpdateRestart(False)
        If ReturnCode = ProcessReturnValues.Exception Then
            If Not IsLogShown Then
                FeedbackInfo()
                Logger.Info("请在启动器的问题反馈渠道提交错误报告，以便于作者解决此问题！")
                IsLogShown = True
                StartProcess(Paths.Base & "YQL\Log1.txt")
            End If
            Thread.Sleep(500) '防止 缘启 在记事本打开前就被掐掉
        End If
        Logger.Info($"程序已退出，返回值：{ReturnCode}")
        '兜底清理联机模块（用户可能已在待机状态，TryExit 不会处理这种情况）
        On Error Resume Next
        ModTerracotta.StopCore()
        ConfigUtils.SaveAll()
        Logger.Instance.Flush()
        If ReturnCode <> ProcessReturnValues.Success Then Environment.Exit(ReturnCode)
        Process.GetCurrentProcess.Kill()
    End Sub
    Private Sub BtnTitleClose_Click(sender As Object, e As RoutedEventArgs) Handles BtnTitleClose.Click
        EndProgram(True)
    End Sub

    '移动
    Private Sub FormDragMove(sender As Object, e As MouseButtonEventArgs) Handles PanTitle.MouseLeftButtonDown, PanMsg.MouseLeftButtonDown
        On Error Resume Next
        If e.ClickCount = 2 Then
            '双击标题栏 / 弹窗空白处最大化、还原；点在按钮等控件上时不应触发
            If IsInteractiveSource(e.OriginalSource) Then Return
            ToggleMaximize()
            Return
        End If
        If sender.IsMouseDirectlyOver Then DragMove()
    End Sub
    ''' <summary>
    ''' 判断鼠标事件是否发生在按钮、输入框等可交互控件上。
    ''' 弹窗遮罩 PanMsg 覆盖整个窗口，若不排除子控件，快速双击任意按钮都会冒泡成「双击标题栏」。
    ''' </summary>
    Private Function IsInteractiveSource(Source As Object) As Boolean
        Dim Current As DependencyObject = TryCast(Source, DependencyObject)
        Do While Current IsNot Nothing
            If TypeOf Current Is System.Windows.Controls.Primitives.ButtonBase Then Return True
            If TypeOf Current Is MyButton OrElse TypeOf Current Is MyIconButton OrElse TypeOf Current Is MyTextButton OrElse
               TypeOf Current Is MyIconTextButton OrElse TypeOf Current Is MyCheckBox OrElse TypeOf Current Is MyComboBox OrElse
               TypeOf Current Is MyListItem OrElse TypeOf Current Is MyNavItem OrElse TypeOf Current Is MySlider OrElse
               TypeOf Current Is MyTextBox OrElse TypeOf Current Is MyRadioBox OrElse TypeOf Current Is MyRadioButton Then Return True
            If TypeOf Current Is Visual Then
                Current = VisualTreeHelper.GetParent(Current)
            Else
                Current = LogicalTreeHelper.GetParent(Current)
            End If
        Loop
        Return False
    End Function

    '改变大小
    ''' <summary>
    ''' 是否可以向注册表储存尺寸改变信息。以此避免初始化时误储存。
    ''' </summary>
    Public IsSizeSaveable As Boolean = False
    Private Sub FormMain_SizeChanged() Handles Me.SizeChanged, Me.Loaded
        If IsSizeSaveable AndAlso WindowState <> WindowState.Maximized Then
            Settings.Set("WindowHeight", Height)
            Settings.Set("WindowWidth", Width)
        End If
        '先更新圆角与透明边缘状态，再据此计算内容尺寸（避免依赖可能滞后的 ActualWidth）
        RefreshWindowChrome()
        Dim Margin = If(IsMaximizedCustom, 0.0, 8.0)
        '界面缩放时，根容器的布局尺寸要放大为「窗口尺寸 ÷ 缩放比例」，
        '这样 LayoutTransform 缩小后的画面正好铺满窗口，不会留白也不会被裁掉
        Dim S As Double = If(UiScaleFactor <= 0, 1.0, UiScaleFactor)
        Dim BaseW As Double = Width
        Dim BaseH As Double = Height
        If Math.Abs(S - 1) > 0.001 Then
            BaseW = Width / S
            BaseH = Height / S
            If PanBack IsNot Nothing Then
                If Math.Abs(PanBack.Width - BaseW) > 0.5 Then PanBack.Width = BaseW
                If Math.Abs(PanBack.Height - BaseH) > 0.5 Then PanBack.Height = BaseH
            End If
        End If
        Dim ContentW = Math.Max(0, BaseW - Margin * 2)
        Dim ContentH = Math.Max(0, BaseH - Margin * 2)
        RectForm.Rect = New Rect(0, 0, ContentW, ContentH)
        PanForm.Width = ContentW + 0.001
        PanForm.Height = ContentH + 0.001
        PanMain.Width = Math.Max(0, PanForm.Width - RailWidth)
        '启动页全宽：由 ColMainLeft 的 1* 自动铺满，这里不再需要给页面补显式宽度
        PanMain.Height = Math.Max(0, PanForm.Height - PanTitle.ActualHeight)
    End Sub

    ''' <summary>
    ''' 当前界面缩放比例（1.0 表示 100%）。
    ''' </summary>
    Private UiScaleFactor As Double = 1

    ''' <summary>
    ''' 应用界面缩放（80%~150%）。通过根容器的 LayoutTransform 整体缩放，同时把根容器的布局尺寸
    ''' 放大为「窗口尺寸 ÷ 缩放比例」，这样缩放后的画面正好铺满窗口，不会出现空白或内容被裁掉。
    ''' </summary>
    Public Sub ApplyUiScale()
        Try
            If PanBack Is Nothing Then Return
            Dim Scale As Integer = Settings.Get(Of Integer)("UiUiScale")
            If Scale < 80 Then Scale = 80
            If Scale > 150 Then Scale = 150
            Dim S As Double = Scale / 100.0
            UiScaleFactor = S
            If Math.Abs(S - 1) < 0.001 Then
                PanBack.LayoutTransform = Nothing
                PanBack.ClearValue(WidthProperty)
                PanBack.ClearValue(HeightProperty)
            Else
                PanBack.LayoutTransform = New ScaleTransform(S, S)
            End If
            '窗口最小尺寸随缩放调整，保证缩放后仍有相同的可用内容（同时不超过屏幕可用区域）
            Dim Area As Rect = SystemParameters.WorkArea
            MinWidth = Math.Min(1040 * S, Math.Max(600, Area.Width - 40))
            MinHeight = Math.Min(640 * S, Math.Max(420, Area.Height - 40))
            Logger.Info($"界面缩放已应用：{Scale}%")
            FormMain_SizeChanged()
        Catch ex As Exception
            Logger.Warn(ex, "应用界面缩放失败")
        End Try
    End Sub

    '最小化
    Private Sub BtnTitleMin_Click() Handles BtnTitleMin.Click
        WindowState = WindowState.Minimized
    End Sub

    '最大化 / 还原（无边框窗口手动贴合工作区，避免分层窗口的最大化问题）
    Public IsMaximizedCustom As Boolean = False
    Private RestoreRectCustom As Rect
    Private Sub BtnTitleMax_Click() Handles BtnTitleMax.Click
        ToggleMaximize()
    End Sub
    Public Sub ToggleMaximize()
        If WindowState = WindowState.Maximized Then
            WindowState = WindowState.Normal
        Else
            WindowState = WindowState.Maximized
        End If
    End Sub

    ''' <summary>系统最大化/还原状态变化时，同步自定义最大化状态并刷新窗口边框。</summary>
    Private Sub FormMain_StateChanged() Handles Me.StateChanged
        IsMaximizedCustom = (WindowState = WindowState.Maximized)
        RefreshWindowChrome()
    End Sub
    ''' <summary>
    ''' 根据是否最大化调整透明边距与圆角。
    ''' </summary>
    Private Sub RefreshWindowChrome()
        If IsMaximizedCustom Then
            RectForm.RadiusX = 0
            RectForm.RadiusY = 0
            '最大化时贴满工作区：取消透明边缘、禁用原生缩放、隐藏自定义缩放条
            BorderForm.Margin = New Thickness(0)
            ResizeMode = System.Windows.ResizeMode.NoResize
            SetResizers(False)
        Else
            RectForm.RadiusX = 8
            RectForm.RadiusY = 8
            '保留 8px 透明边缘，让边缘的 Resizer* 矩形可被点到，从而恢复窗口拖拽改大小
            BorderForm.Margin = New Thickness(8)
            ResizeMode = System.Windows.ResizeMode.CanResize
            SetResizers(True)
        End If
    End Sub

    ''' <summary>显示或隐藏窗口边缘的 8 个自定义缩放条。</summary>
    Private Sub SetResizers(Visible As Boolean)
        Dim V = If(Visible, Visibility.Visible, Visibility.Collapsed)
        ResizerT.Visibility = V
        ResizerB.Visibility = V
        ResizerL.Visibility = V
        ResizerR.Visibility = V
        ResizerLT.Visibility = V
        ResizerLB.Visibility = V
        ResizerRB.Visibility = V
        ResizerRT.Visibility = V
    End Sub

    '左侧导航栏展开 / 收起
    Public IsRailCollapsed As Boolean = False
    ''' <summary>进入全幅页面（网页 / 种子地图）前的导航栏折叠状态，离开时恢复。</summary>
    Private _RailCollapsedBeforeFullBleed As Boolean = False
    ''' <summary>导航栏折叠/展开动画进行中：内容区宽度由动画逐帧驱动，左侧背景宽度应即时跟随而非叠加二次动画（否则产生回弹）。</summary>
    Private _RailAnimating As Boolean = False
    ''' <summary>
    ''' 左侧主图标栏的当前宽度（展开 72，收起 48）。
    ''' </summary>
    Public ReadOnly Property RailWidth As Double
        Get
            Return If(IsRailCollapsed, 48, 72)
        End Get
    End Property
    Private Sub RefreshRailLayout(animate As Boolean)
        Dim NewWidth As Double = RailWidth
        For Each Item In PanTitleSelect.Children
            If TypeOf Item Is MyNavItem Then CType(Item, MyNavItem).Collapsed = IsRailCollapsed
        Next
        '折叠按钮的箭头指示"点一下会往哪边去"：展开时指左（收起），收起时指右（展开）
        If PathRailToggle IsNot Nothing Then
            PathRailToggle.RenderTransformOrigin = New Point(0.5, 0.5)
            PathRailToggle.RenderTransform = New ScaleTransform(If(IsRailCollapsed, -1.0, 1.0), 1.0)
        End If
        If BtnRailToggle IsNot Nothing Then
            BtnRailToggle.ToolTip = If(IsRailCollapsed, "展开导航", "收起导航")
        End If
        PanHint.Margin = New Thickness(0)
        If animate Then
            '导航栏宽度作为主动画：内容区边距与宽度在其 SizeChanged 中逐帧跟随，
            '使两侧边缘全程贴合、平滑过渡（无瞬间跳位）
            _RailAnimating = True
            AniStart(AaWidth(PanNavRail, NewWidth - PanNavRail.ActualWidth, 320, , New AniEaseOutBack(AniEasePower.Weak)), "FrmMain Rail", True)
            AniStart({AaCode(Sub() _RailAnimating = False, 340, True)}, "FrmMain Rail Done", True)
            ApplyRailWidth(PanNavRail.ActualWidth)
        Else
            AniStop("FrmMain Rail")
            AniStop("FrmMain Rail Done")
            _RailAnimating = False
            PanNavRail.Width = NewWidth
            ApplyRailWidth(NewWidth)
        End If
        PopHint.HorizontalOffset = NewWidth + 16
    End Sub

    ''' <summary>按导航栏当前宽度同步内容区边距与宽度（导航栏动画期间逐帧调用）。</summary>
    Private Sub ApplyRailWidth(RailW As Double)
        Dim ContentWidth As Double = Math.Max(0, PanForm.Width - RailW)
        PanLeft.Margin = New Thickness(RailW, 0, 0, 0)
        PanMain.Margin = New Thickness(RailW, 0, 0, 0)
        PanMain.Width = ContentWidth
        '启动页宽度由左列（1*）随此处逐帧跟随，无需再赋值给页面
    End Sub

    ''' <summary>
    ''' 设置内容区两列的分配方式。
    ''' 启动页是单页全宽布局：左列（页面）取 1* 占满、右列宽度归零，页面宽度完全交给布局计算，
    ''' 不再依赖「什么时候给 PageLeft.Width 赋值」——此前主页改成延迟构建后，挂载时没有赋值，
    ''' 页面在 Auto 列里就塌缩成内容的自然宽度，右侧留下一片没铺满的空区。
    ''' 其余页面沿用原设计：左列 Auto（二级菜单 212 或 0），右列 1*。
    ''' </summary>
    Private Sub ApplyMainColumns(FullBleed As Boolean)
        If PanMain.ColumnDefinitions.Count < 2 Then Return
        If FullBleed Then
            PanMain.ColumnDefinitions(0).Width = New GridLength(1, GridUnitType.Star)
            PanMain.ColumnDefinitions(1).Width = New GridLength(0)
        Else
            PanMain.ColumnDefinitions(0).Width = GridLength.Auto
            PanMain.ColumnDefinitions(1).Width = New GridLength(1, GridUnitType.Star)
        End If
    End Sub

    Private Sub PanNavRail_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles PanNavRail.SizeChanged
        If e.WidthChanged AndAlso _RailAnimating Then ApplyRailWidth(PanNavRail.ActualWidth)
    End Sub

    '消息弹窗位于 Popup（PopHint）中。WPF 的 Popup 在自定义无边框窗口拖动时存在不跟随
    'PlacementTarget 重新定位的缺陷，导致弹窗停在屏幕旧位置；在窗口移动 / 缩放时微调偏移量强制其重新计算位置。
    Private Sub FrmMain_LocationChanged(sender As Object, e As EventArgs) Handles Me.LocationChanged
        RepositionHintPopup()
    End Sub

    Private Sub FrmMain_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles Me.SizeChanged
        RepositionHintPopup()
    End Sub

    Private Sub RepositionHintPopup()
        RepositionPopup(PopHint)
        RepositionPopup(PopFloatingBack)
    End Sub

    ''' <summary>
    ''' 根据浮动返回键是否可见调整消息弹窗高度：
    ''' 返回键出现在左下角时抬高弹窗避让，返回键隐藏时回落，减少对页面内容的遮挡。
    ''' </summary>
    Private Sub UpdateHintOffset()
        Try
            If PopHint Is Nothing Then Return
            Dim Raised As Boolean = PopFloatingBack IsNot Nothing AndAlso PopFloatingBack.IsOpen
            PopHint.VerticalOffset = If(Raised, -270, -220)
        Catch
        End Try
    End Sub

    Private Sub RepositionPopup(P As System.Windows.Controls.Primitives.Popup)
        Try
            If P Is Nothing OrElse Not P.IsOpen Then Return
            Dim Offset = P.HorizontalOffset
            P.HorizontalOffset = Offset + 0.001
            P.HorizontalOffset = Offset
        Catch
        End Try
    End Sub
    Private Sub BtnRailToggle_Click() Handles BtnRailToggle.Click
        IsRailCollapsed = Not IsRailCollapsed
        RefreshRailLayout(True)
        Settings.Set("UiNavCollapsed", IsRailCollapsed)
        Logger.Info($"左侧导航栏{(If(IsRailCollapsed, "收起", "展开"))}")
    End Sub
    ''' <summary>
    ''' 刷新左侧导航栏的皮肤头像。
    ''' </summary>
    Public Sub RefreshAccountAvatar()
        Try
            If ImgAvatarHead Is Nothing Then Return
            ImgAvatarHead.SetSkin(PageLaunchLeft.GetAccountSkinAddress())
        Catch ex As Exception
            Logger.Warn(ex, "刷新导航栏头像失败")
        End Try
    End Sub
    Private Sub BtnAvatar_Click() Handles BtnAvatar.Click
        PageChange(PageType.Launch)
        RunInUi(
        Sub()
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.ShowGlobalAccountPicker()
        End Sub, True)
    End Sub

    '背景图片与标题栏
    Public Shared Sub UpdateBackgroundAndTitleBar(Value) '从设置更新
        If FrmMain Is Nothing OrElse Not FrmMain.IsLoaded Then Return
        FrmMain.UpdateBackgroundAndTitleBar()
    End Sub
    Public Sub UpdateBackgroundAndTitleBar()
        Logger.Info("从设置更新背景图片与标题栏样式")
        '背景图片透明度
        ImgBack.Opacity = Settings.Get(Of Integer)("UiBackgroundOpacity") / 1000
        '背景图片模糊
        Dim BlurRadius As Double = Settings.Get(Of Integer)("UiBackgroundBlur") + 1
        If BlurRadius = 1 Then
            ImgBack.Effect = Nothing
        Else
            ImgBack.Effect = New Effects.BlurEffect With {.Radius = BlurRadius}
        End If
        ImgBack.Margin = New Thickness(-BlurRadius / 1.8)
        '背景图片适应方式
        Dim BackgroundType As Integer = Settings.Get(Of Integer)("UiBackgroundSuit")
        If ImgBack.Background IsNot Nothing Then
            Dim Brush As ImageBrush = CType(ImgBack.Background, ImageBrush)
            If BackgroundType = 0 Then
                '智能：当图片较小时平铺，较大时适应
                If Brush.ImageSource.Width < PanMain.ActualWidth / 2 AndAlso Brush.ImageSource.Height < PanMain.ActualHeight / 2 Then
                    BackgroundType = 4 '平铺
                Else
                    BackgroundType = 2 '适应
                End If
            End If
            Brush.Stretch = Stretch.UniformToFill
            Brush.TileMode = TileMode.None
            Brush.Viewport = New Rect(0, 0, 1, 1)
            Brush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox
            Brush.AlignmentX = AlignmentX.Center
            Brush.AlignmentY = AlignmentY.Center
            Select Case BackgroundType
                Case 1 '中
                    Brush.Stretch = Stretch.None
                Case 3 '拉伸
                    Brush.Stretch = Stretch.Fill
                Case 4 '平铺
                    Brush.Stretch = Stretch.None
                    Brush.TileMode = TileMode.Tile
                    Brush.Viewport = New Rect(0, 0, Brush.ImageSource.Width, Brush.ImageSource.Height)
                    Brush.ViewportUnits = BrushMappingMode.Absolute

                Case 2 '适应
                Case 5 '左上
                    Brush.AlignmentX = AlignmentX.Left
                    Brush.AlignmentY = AlignmentY.Top
                Case 6 '右上
                    Brush.AlignmentX = AlignmentX.Right
                    Brush.AlignmentY = AlignmentY.Top
                Case 7 '左下
                    Brush.AlignmentX = AlignmentX.Left
                    Brush.AlignmentY = AlignmentY.Bottom
                Case 8 '右下
                    Brush.AlignmentX = AlignmentX.Right
                    Brush.AlignmentY = AlignmentY.Bottom
                Case 9 '左
                    Brush.AlignmentX = AlignmentX.Left
                    Brush.AlignmentY = AlignmentY.Center
                Case 10 '右
                    Brush.AlignmentX = AlignmentX.Right
                    Brush.AlignmentY = AlignmentY.Center
                Case 11 '上
                    Brush.AlignmentX = AlignmentX.Center
                    Brush.AlignmentY = AlignmentY.Top
                Case 12 '下
                    Brush.AlignmentX = AlignmentX.Center
                    Brush.AlignmentY = AlignmentY.Bottom
            End Select
        End If
        '标题栏：固定只显示「缘启」二字，隐藏徽标 / 图片 / 英文
        PanTitleBadge.Visibility = Visibility.Collapsed
        ShapeTitleLogo.Visibility = Visibility.Collapsed
        ImageTitleLogo.Visibility = Visibility.Collapsed
        LabTitleLogo.Visibility = Visibility.Visible
        LabTitleLogo.Text = "缘启"
        FrmSetupUI?.CardLogo.TriggerForceResize()
        '标题栏文本是否居左
        PanTitleMain.ColumnDefinitions(0).Width = New GridLength(If(Settings.Get(Of Boolean)("UiLogoLeft") AndAlso Settings.Get(Of Integer)("UiLogoType") = 0, 0, 1), GridUnitType.Star)
    End Sub

#End Region

#Region "窗体事件"

    '按键事件
    Private Sub FormMain_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If e.IsRepeat Then Return
        '修复按下 Alt 后误认为弹出系统菜单导致的冻结
        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then e.Handled = True
        '在有弹窗时：回车选择第一个，Esc 选择最后一个
        If PanMsg.Children.Any Then
            If e.Key = Key.Enter Then
                CType(PanMsg.Children(0), Object).Btn1_Click()
            ElseIf e.Key = Key.Escape Then
                Dim Msg As Object = PanMsg.Children(0)
                If TypeOf Msg IsNot MyMsgInput AndAlso TypeOf Msg IsNot MyMsgSelect AndAlso Msg.Btn3.Visibility = Visibility.Visible Then
                    Msg.Btn3_Click()
                ElseIf Msg.Btn2.Visibility = Visibility.Visible Then
                    Msg.Btn2_Click()
                Else
                    Msg.Btn1_Click()
                End If
            End If
            Return
        End If

        '==========================
        ' 在没有弹窗时：继续检查……
        '==========================

        '按 ESC 取消启动，或返回上一级
        If e.Key = Key.Escape Then
            If PageCurrent = FormMain.PageType.Launch AndAlso McLaunchLoader.State = LoadState.Loading Then
                FrmLaunchLeft.BtnCancel_Click()
                Return
            End If
            TriggerPageBack()
        End If
        '更改隐藏版本可见性
        If e.Key = Key.F11 AndAlso PageCurrent = FormMain.PageType.InstanceSelect Then
            FrmSelectRight.ShowHidden = Not FrmSelectRight.ShowHidden
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Return
        End If
        '更改功能隐藏可见性
        If e.Key = Key.F12 Then
            PageSetupUI.HiddenForceShow = Not PageSetupUI.HiddenForceShow
            If PageSetupUI.HiddenForceShow Then
                Hint("功能隐藏设置已暂时关闭！", HintType.Green)
            Else
                Hint("功能隐藏设置已重新开启！", HintType.Green)
            End If
            PageSetupUI.HiddenRefresh()
            Return
        End If
        '按 F5 刷新页面
        If e.Key = Key.F5 Then
            If TypeOf PageLeft Is IRefreshable Then CType(PageLeft, IRefreshable).Refresh()
            If TypeOf PageRight Is IRefreshable Then CType(PageRight, IRefreshable).Refresh()
            Return
        End If
        '调用启动游戏
        If e.Key = Key.Enter AndAlso PageCurrent = FormMain.PageType.Launch Then
            If IsAprilEnabled AndAlso Not IsAprilGiveup Then
                Hint("木大！")
            Else
                FrmLaunchLeft.LaunchButtonClick()
            End If
        End If
    End Sub
    Private Sub FormMain_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseDown
        '鼠标侧键返回上一级
        If FrmMain.PanMsg.Children.Count > 0 OrElse WaitingMyMsgBox.Any Then Return '弹窗中（#5513）
        If e.ChangedButton = MouseButton.XButton1 OrElse e.ChangedButton = MouseButton.XButton2 Then TriggerPageBack()
    End Sub
    Private Sub TriggerPageBack()
        If PageCurrent = PageType.Download AndAlso PageCurrentSub = PageSubType.DownloadInstall AndAlso FrmDownloadInstall.IsInSelectPage Then
            FrmDownloadInstall.ExitSelectPage()
        Else
            PageBack()
        End If
    End Sub

    '切回窗口
    Private Sub FormMain_Activated() Handles Me.Activated
        '切回窗口时自动刷新
        Try
            If PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.InstanceMod Then
                'Mod 管理
                FrmInstanceMod.ReloadModList()
            ElseIf PageCurrent = PageType.InstanceSetup AndAlso (PageCurrentSub = PageSubType.InstanceSetup OrElse PageCurrentSub = PageSubType.InstanceExport) Then
                '更新当前选用的 Java
                PageInstanceLeft.ReloadCurrentJava()
            ElseIf PageCurrent = PageType.InstanceSelect Then
                '版本选择
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
            End If
        Catch ex As Exception
            Logger.Error(ex, "切回窗口时出错")
        End Try
        '读取剪贴板，自动加入联机房间
        If PageLinkMain.LinkState <> PageLinkMain.LinkStates.Waiting Then Return '已启动联机
        If PageCurrent = PageType.Link Then Return '已在联机界面
        Dim Code = ClipboardGetText() : If Code Is Nothing Then Return '剪贴板无文本
        If Settings.Get(Of String)("LinkLastAutoJoinInviteCode") = Code Then Return
        If PageLinkMain.ValidateCodeFormat(Code) IsNot Nothing Then Return '不是邀请码
        Settings.Set("LinkLastAutoJoinInviteCode", Code)
        RunInThread(
        Sub()
            If MyMsgBox("嘿，是否使用复制的邀请码加入房间？", "加入联机房间", "加入", "取消") = 2 Then Return '防止弹窗阻碍主线程，所以必须放在工作线程
            RunInUi(
            Sub()
                PageLinkMain.Join(Code)
                ClipboardSet(Nothing, False)
            End Sub)
        End Sub)
    End Sub

    '文件拖放
    Private Sub HandleDrag(sender As Object, e As DragEventArgs)
        Try
            If e.Handled AndAlso (e.Effects <> DragDropEffects.None) Then Return
            e.Handled = True
            '缓存
            Static PrevData As IDataObject, PrevEffects As DragDropEffects
            If e.Data Is PrevData Then
                e.Effects = PrevEffects
                Return
            End If
            '确定拖放效果
            e.Effects = DragDropEffects.None
            If e.Data.GetDataPresent(DataFormats.Text) Then
                Dim Str As String = e.Data.GetData(DataFormats.Text)
                If Str.StartsWithF("authlib-injector:yggdrasil-server:") Then
                    e.Effects = DragDropEffects.Copy
                ElseIf Str.StartsWithF("file:///") Then
                    e.Effects = DragDropEffects.Copy
                End If
            ElseIf e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim Files As String() = e.Data.GetData(DataFormats.FileDrop)
                If Files IsNot Nothing AndAlso Files.Length > 0 Then
                    e.Effects = DragDropEffects.Link
                End If
            End If
            PrevData = e.Data
            PrevEffects = e.Effects
            Logger.Info($"设置拖放类型：{e.Effects}")
        Catch ex As Exception
            Logger.Error(ex, "处理拖放时出错")
        End Try
    End Sub
    Private Sub FrmMain_Drop(sender As Object, e As DragEventArgs) Handles Me.Drop
        Try
            ShowWindowToTop()
            If e.Data.GetDataPresent(DataFormats.Text) Then
                '获取文本
                Try
                    Dim Str As String = e.Data.GetData(DataFormats.Text)
                    Logger.Info($"接受文本拖拽：{Str}")
                    If Str.StartsWithF("authlib-injector:yggdrasil-server:") Then
                        'Authlib 拖拽
                        e.Handled = True
                        e.Effects = DragDropEffects.Copy
                        Dim AuthlibServer As String = StringUtils.FormUrlUnescape(Str.Substring("authlib-injector:yggdrasil-server:".Length))
                        Logger.Info($"Authlib 拖拽：{AuthlibServer}")
                        If Not String.IsNullOrEmpty(New ValidateHttp().Validate(AuthlibServer)) Then
                            Hint($"输入的 Authlib 验证服务器不符合网址格式（{AuthlibServer}）！", HintType.Red)
                            Return
                        End If
                        Dim Target = If(PageCurrent = PageType.InstanceSetup, PageInstanceLeft.Instance, McInstanceSelected)
                        If Target Is Nothing Then
                            Hint("请先下载游戏，再设置第三方登录！", HintType.Red)
                            Return
                        End If
                        If AuthlibServer = "https://littleskin.cn/api/yggdrasil" Then
                            'LittleSkin
                            If MyMsgBox($"是否要在版本 {Target.Name} 中开启 LittleSkin 登录？" & vbCrLf &
                                        "你可以在 版本设置 → 设置 → 服务器选项 中修改登录方式。", "第三方登录开启确认", "确定", "取消") = 2 Then
                                Return
                            End If
                            Settings.Set("VersionServerLogin", 4, Instance:=Target)
                            Settings.Set("VersionServerAuthServer", "https://littleskin.cn/api/yggdrasil", Instance:=Target)
                            Settings.Set("VersionServerAuthRegister", "https://littleskin.cn/auth/register", Instance:=Target)
                            Settings.Set("VersionServerAuthName", "LittleSkin 登录", Instance:=Target)
                        Else
                            '第三方 Authlib 服务器
                            If MyMsgBox($"是否要在版本 {Target.Name} 中开启第三方登录？" & vbCrLf &
                                        $"登录服务器：{AuthlibServer}" & vbCrLf & vbCrLf &
                                        "你可以在 版本设置 → 设置 → 服务器选项 中修改登录方式。", "第三方登录开启确认", "确定", "取消") = 2 Then
                                Return
                            End If
                            Settings.Set("VersionServerLogin", 4, Instance:=Target)
                            Settings.Set("VersionServerAuthServer", AuthlibServer, Instance:=Target)
                            Settings.Set("VersionServerAuthRegister", AuthlibServer.Replace("api/yggdrasil", "auth/register"), Instance:=Target)
                            Settings.Set("VersionServerAuthName", "", Instance:=Target)
                        End If
                        If PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.InstanceSetup Then
                            '正在服务器选项页，需要刷新设置项显示
                            FrmInstanceSetup.Reload()
                        ElseIf PageCurrent = PageType.Launch Then
                            '正在主页，需要刷新左边栏
                            FrmLaunchLeft.RefreshPage(True, False)
                        End If
                    ElseIf Str.StartsWithF("file:///") Then
                        '文件拖拽（例如从浏览器下载窗口拖入）
                        Dim FilePath = Net.WebUtility.UrlDecode(Str).Substring("file:///".Length).Replace("/", "\")
                        e.Handled = True
                        e.Effects = DragDropEffects.Copy
                        FileDrag(New List(Of String) From {FilePath})
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "无法接取文本拖拽事件")
                    Return
                End Try
            ElseIf e.Data.GetDataPresent(DataFormats.FileDrop) Then
                '获取文件并检查
                Dim FilePathRaw = e.Data.GetData(DataFormats.FileDrop)
                If FilePathRaw Is Nothing Then '#2690
                    Hint("请将文件解压后再拖入！", HintType.Red)
                    Return
                End If
                e.Handled = True
                e.Effects = DragDropEffects.Link
                FileDrag(CType(FilePathRaw, IEnumerable(Of String)))
            End If
        Catch ex As Exception
            Logger.Error(ex, "接取拖拽事件失败")
        End Try
    End Sub
    Private Sub FileDrag(FilePathList As IEnumerable(Of String))
        RunInNewThread(
        Sub()
            Dim FilePath As String = FilePathList.First
            Logger.Warn($"接受文件拖拽：{FilePath}{If(FilePathList.Any, $" 等 {FilePathList.Count} 个文件", "")}")
            '基础检查
            If DirectoryUtils.Exists(FilePathList.First) AndAlso Not FileUtils.Exists(FilePathList.First) Then
                Hint("请拖入一个文件，而非文件夹！", HintType.Red)
                Return
            ElseIf Not FileUtils.Exists(FilePathList.First) Then
                Hint("拖入的文件不存在：" & FilePathList.First, HintType.Red)
                Return
            End If
            '多文件拖拽
            Dim PathList As List(Of String) = FilePathList.ToList()
            If FilePathList.Count > 1 Then
                '必须要求全部为 jar 文件
                For Each File In PathList
                    If Not {"jar", "litemod", "disabled", "old"}.Contains(PathUtils.GetExtension(File)) Then
                        Hint("一次请只拖入一个文件！", HintType.Red)
                        Return
                    End If
                Next
            End If
            '主页
            Dim Extension As String = PathUtils.GetExtension(FilePath)
            If Extension = "xaml" Then
                Logger.Info("文件后缀为 XAML，作为主页加载")
                Dim CustomPath As String = Settings.Get(Of String)("UiCustomLocal")
                If String.IsNullOrWhiteSpace(CustomPath) Then CustomPath = Paths.Base & "YQL\Custom.xaml"
                If FileUtils.Exists(CustomPath) Then
                    If MyMsgBox("已存在一个主页文件，是否要将它覆盖？", "覆盖确认", "覆盖", "取消") = 2 Then
                        Return
                    End If
                End If
                FileUtils.Copy(FilePath, CustomPath)
                RunInUi(
                Sub()
                    Settings.Set("UiCustomType", 1)
                    FrmLaunchRight.ForceRefresh()
                    Hint("已加载主页自定义文件！", HintType.Green)
                End Sub)
                Return
            End If
            '安装 Mod
            If PageInstanceMod.InstallMods(PathList) Then Return
            '安装整合包
            If {"zip", "rar", "mrpack"}.Any(Function(t) t = Extension) Then '部分压缩包是 zip 格式但后缀为 rar，总之试一试
                Hint("正在读取压缩包……")
                Try
                    Logger.Info("文件为压缩包，尝试作为整合包安装")
                    ModpackInstall(FilePath)
                    RunInUi(Sub() FrmMain.PageChange(FormMain.PageType.TaskManager))
                    Return
                Catch ex As Exception
                    If ex.IsCanceled Then Return
                    '安装失败，继续往后尝试
                End Try
            End If
            'RAR 处理
            If Extension = "rar" Then
                Hint("缘启无法处理 rar 格式的压缩包，请在解压后重新压缩为 zip 格式再试！", HintType.Red)
                Return
            End If
            '错误报告分析
            Try
                Logger.Info("尝试进行错误报告分析")
                Dim Analyzer As New CrashAnalyzer
                Analyzer.Import(FilePath)
                If Not Analyzer.Prepare() Then Exit Try
                Analyzer.Analyze()
                Analyzer.Output(True, New List(Of String))
                Return
            Catch ex As Exception
                Logger.Error(ex, "自主错误报告分析失败")
            End Try
            '未知操作
            Hint("缘启无法确定适合的操作……", HintType.Red)
        End Sub, "文件拖拽")
    End Sub

    '接受到 Windows 窗体事件
    Public IsSystemTimeChanged As Boolean = False
    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure NativePoint
        Public X As Integer
        Public Y As Integer
    End Structure
    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure MinMaxInfo
        Public ptReserved As NativePoint
        Public ptMaxSize As NativePoint
        Public ptMaxPosition As NativePoint
        Public ptMinTrackSize As NativePoint
        Public ptMaxTrackSize As NativePoint
    End Structure
    Private Function WndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = 30 Then
            Dim NowDate = Date.Now
            If NowDate.Date = ApplicationOpenTime.Date Then
                Logger.Info($"系统时间微调为：{NowDate.ToLongDateString} {NowDate.ToLongTimeString}")
                IsSystemTimeChanged = False
            Else
                Logger.Info($"系统时间修改为：{NowDate.ToLongDateString} {NowDate.ToLongTimeString}")
                IsSystemTimeChanged = True
            End If
        ElseIf msg = &H24 Then 'WM_GETMINMAXINFO：让无边框窗口最大化时贴合工作区
            Try
                Dim Info = CType(Runtime.InteropServices.Marshal.PtrToStructure(lParam, GetType(MinMaxInfo)), MinMaxInfo)
                Dim Area = Windows.Forms.Screen.FromHandle(hwnd).WorkingArea
                Info.ptMaxPosition.X = Area.Left
                Info.ptMaxPosition.Y = Area.Top
                Info.ptMaxSize.X = Area.Width
                Info.ptMaxSize.Y = Area.Height
                Runtime.InteropServices.Marshal.StructureToPtr(Info, lParam, True)
            Catch ex As Exception
                Logger.Warn(ex, "处理窗口最大化尺寸失败")
            End Try
        ElseIf msg = &H20B Then 'WM_XBUTTONDOWN：鼠标侧键
            Try
                Dim XButton As Integer = CInt(wParam.ToInt64() And &HFFFF)
                If XButton = 1 Then 'XBUTTON1（后退）
                    SideButtonBack()
                    handled = True
                ElseIf XButton = 2 Then 'XBUTTON2（前进）
                    SideButtonForward()
                    handled = True
                End If
            Catch ex As Exception
                Logger.Warn(ex, "处理鼠标侧键失败")
            End Try
        ElseIf msg = 400 * 16 + 2 Then
            Logger.Info($"收到置顶信息：{hwnd.ToInt64}")
            '窗口尚未加载完成时也要把可见性与窗口状态修好，否则会出现「点了一次没反应」
            ShowWindowToTop()
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    '窗口隐藏与置顶
    Private _Hidden As Boolean = False
    Public Property Hidden As Boolean
        Get
            Return _Hidden
        End Get
        Set(value As Boolean)
            If _Hidden = value Then Return
            _Hidden = value
            If value Then
                '隐藏
                Left -= 10000
                ShowInTaskbar = False
                Visibility = Visibility.Hidden
                Logger.Info($"窗口已隐藏，位置：({Left},{Top})")
            Else
                '取消隐藏
                If Left < -2000 Then Left += 10000
                ShowWindowToTop()
            End If
        End Set
    End Property
    ''' <summary>
    ''' 把当前窗口拖到最前面。
    ''' </summary>
    Public Sub ShowWindowToTop()
        RunInUi(
        Sub()
            Try
                '先把被"隐藏到屏幕外"的窗口挪回来，再处理显示状态
                If Left < -2000 Then Left += 10000
                _Hidden = False
                Visibility = Visibility.Visible
                ShowInTaskbar = True
                WindowState = WindowState.Normal
                '用 Win32 置顶再取消：WPF 里连续写 Topmost = True / False 会被合并，
                '偶尔留下永久置顶，表现就是启动器一直盖着游戏窗口
                Dim Hwnd As IntPtr = New Interop.WindowInteropHelper(Me).Handle
                '系统级还原（最小化时只改 WindowState 有时不会真正出现），再置顶 + 抢前台
                RestoreWindowNative(Hwnd)
                SetWindowTopmost(Hwnd, True) '偶尔 SetForegroundWindow 失效
                SetWindowTopmost(Hwnd, False)
                SetForegroundWindow(Hwnd)
                Activate()
                Focus()
                Logger.Info($"窗口已置顶，位置：({Left}, {Top}), {Width} x {Height}")
            Catch ex As Exception
                Logger.Warn(ex, "窗口置顶失败")
            End Try
        End Sub)
    End Sub

    ''' <summary>确保窗口不在最上层：启动游戏后把前台让给游戏窗口。</summary>
    Public Sub ClearWindowTopmost()
        RunInUi(
        Sub()
            Try
                SetWindowTopmost(New Interop.WindowInteropHelper(Me).Handle, False)
            Catch ex As Exception
                Logger.Warn(ex, "取消窗口置顶失败")
            End Try
        End Sub)
    End Sub

#End Region

#Region "切换页面"

    '页面种类与属性
    ''' <summary>
    ''' 页面种类。
    ''' 该枚举在自定义事件中使用，是公开 API 的一部分。
    ''' </summary>
    Public Enum PageType
        ''' <summary>
        ''' 启动。
        ''' </summary>
        Launch = 0
        ''' <summary>
        ''' 下载。
        ''' </summary>
        Download = 1
        ''' <summary>
        ''' 联机。
        ''' </summary>
        Link = 2
        ''' <summary>
        ''' 设置。
        ''' </summary>
        Setup = 3
        ''' <summary>
        ''' 更多。
        ''' </summary>
        Other = 4
        ''' <summary>
        ''' 版本选择。这是一个副页面。
        ''' </summary>
        InstanceSelect = 5
        ''' <summary>
        ''' 任务管理。这是一个副页面。
        ''' </summary>
        TaskManager = 6
        ''' <summary>
        ''' 版本设置。这是一个副页面。
        ''' </summary>
        InstanceSetup = 7
        ''' <summary>
        ''' 社区资源详情。这是一个副页面。
        ''' </summary>
        ResourceDetail = 8
        ''' <summary>
        ''' 帮助详情。这是一个副页面。
        ''' </summary>
        HelpDetail = 9
        ''' <summary>
        ''' 内嵌网页。这是一个副页面。
        ''' </summary>
        Web = 10
        ''' <summary>
        ''' 存档设置。这是一个副页面。
        ''' </summary>
        SaveInfo = 11
        ''' <summary>
        ''' 工具箱。顶级页面。
        ''' </summary>
        Tools = 12
        ''' <summary>
        ''' 种子地图。顶级页面。
        ''' </summary>
        SeedMap = 13
    End Enum
    ''' <summary>
    ''' 次要页面种类。其数值必须与 StackPanel 中的下标一致。
    ''' 该枚举在自定义事件中使用，是公开 API 的一部分。
    ''' </summary>
    Public Enum PageSubType
        [Default] = 0
        DownloadInstall = 0
        DownloadMod = 2
        DownloadPack = 3
        DownloadDataPack = 4
        DownloadResourcePack = 5
        DownloadShader = 6
        SetupLaunch = 0
        SetupLink = 1
        SetupUI = 2
        SetupSystem = 3
        LinkMain = 0
        OtherAbout = 0
        OtherTools = 1
        OtherColorText = 2
        OtherOreGuide = 3
        OtherGameStats = 4
        OtherCommandQuery = 5
        InstanceOverall = 0
        InstanceSetup = 1
        InstanceModify = 2
        InstanceMod = 3
        InstanceModDisabled = 4
        InstanceExport = 5
    End Enum
    ''' <summary>
    ''' 获取次级页面的名称。若并非次级页面则返回空字符串，故可以以此判断是否为次级页面。
    ''' </summary>
    Private Function PageNameGet(Stack As PageStackData) As String
        Select Case Stack.Page
            Case PageType.InstanceSelect
                Return "版本管理"
            Case PageType.TaskManager
                Return "任务管理"
            Case PageType.InstanceSetup
                Return "版本设置 - " & If(PageInstanceLeft.Instance Is Nothing, "未知版本", PageInstanceLeft.Instance.Name)
            Case PageType.ResourceDetail
                Return CType(Stack.Additional(0), ResourceProject).TranslatedName
            Case PageType.HelpDetail
                Return CType(Stack.Additional(0), String)
            Case PageType.Web
                Return "网页"
            Case PageType.SaveInfo
                Return "存档详情"
            Case Else
                Return ""
        End Select
    End Function
    ''' <summary>
    ''' 获取页面在标题栏面包屑中显示的名称。
    ''' </summary>
    Public Function PageTitleText(Stack As PageStackData) As String
        Select Case Stack.Page
            Case PageType.Launch
                Return "启动"
            Case PageType.Download
                Return "下载"
            Case PageType.Link
                Return "联机"
            Case PageType.SeedMap
                Return "种子地图"
            Case PageType.Setup
                Return "设置"
            Case PageType.Other
                Return "更多"
            Case PageType.Tools
                Return "工具箱"
            Case PageType.InstanceSelect
                Return "版本管理"
            Case PageType.TaskManager
                Return "任务管理"
            Case PageType.InstanceSetup
                Return "版本设置"
            Case PageType.ResourceDetail
                Return "资源详情"
            Case PageType.HelpDetail
                Return "帮助"
            Case PageType.Web
                Return "网页"
            Case PageType.SaveInfo
                Return "存档详情"
            Case Else
                Return "启动"
        End Select
    End Function
    ''' <summary>
    ''' 刷新次级页面的名称。
    ''' </summary>
    Public Sub PageNameRefresh(Type As PageStackData)
        LabTitleInner.Text = PageNameGet(Type)
    End Sub
    ''' <summary>
    ''' 刷新次级页面的名称。
    ''' </summary>
    Public Sub PageNameRefresh()
        PageNameRefresh(PageCurrent)
    End Sub

    '页面状态存储
    ''' <summary>
    ''' 当前的主页面。
    ''' </summary>
    Public PageCurrent As PageStackData = PageType.Launch
    ''' <summary>
    ''' 上一个主页面。
    ''' </summary>
    Public PageLast As PageStackData = PageType.Launch
    ''' <summary>
    ''' 当前的子页面。
    ''' </summary>
    Public ReadOnly Property PageCurrentSub As PageSubType
        Get
            Select Case PageCurrent
                Case PageType.Download
                    If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                    Return FrmDownloadLeft.PageID
                Case PageType.Setup
                    If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                    Return FrmSetupLeft.PageID
                Case PageType.Tools
                    If FrmToolsLeft Is Nothing Then FrmToolsLeft = New PageToolsLeft
                    Return FrmToolsLeft.PageID
                Case PageType.InstanceSetup
                    If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                    Return FrmInstanceLeft.PageID
                Case Else
                    Return 0 '没有子页面
            End Select
        End Get
    End Property
    ''' <summary>
    ''' 上层页面的编号堆栈，用于返回。
    ''' </summary>
    Public PageStack As New List(Of PageStackData)
    Public Class PageStackData

        Public Page As PageType
        Public Additional As Object

        Public Overrides Function Equals(other As Object) As Boolean
            If other Is Nothing Then Return False
            If TypeOf other Is PageStackData Then
                Dim PageOther As PageStackData = other
                If Page <> PageOther.Page Then Return False
                If Additional Is Nothing Then
                    Return PageOther.Additional Is Nothing
                Else
                    Return PageOther.Additional IsNot Nothing AndAlso Additional.Equals(PageOther.Additional)
                End If
            ElseIf TypeOf other Is Integer Then
                If Page <> other Then Return False
                Return Additional Is Nothing
            Else
                Return False
            End If
        End Function
        Public Shared Operator =(left As PageStackData, right As PageStackData) As Boolean
            Return EqualityComparer(Of PageStackData).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As PageStackData, right As PageStackData) As Boolean
            Return Not left = right
        End Operator
        Public Shared Widening Operator CType(Value As PageType) As PageStackData
            Return New PageStackData With {.Page = Value}
        End Operator
        Public Shared Widening Operator CType(Value As PageStackData) As PageType
            Return Value.Page
        End Operator
    End Class
    Public PageLeft As MyPageLeft, PageRight As MyPageRight

    '引发实际页面切换的入口
    Private IsChangingPage As Boolean = False
    ''' <summary>
    ''' 切换页面，并引起对应选择 UI 的改变。
    ''' </summary>
    Public Sub PageChange(Stack As PageStackData, Optional SubType As PageSubType = PageSubType.Default)
        If PageNameGet(Stack) = "" Then
            '切换到主页面
            PageChangeExit()
            IsChangingPage = True '防止下面的勾选直接触发了 PageChangeActual
            Dim NavItem As MyNavItem = Nothing
            For Each Child As Object In PanTitleSelect.Children
                Dim Ni = TryCast(Child, MyNavItem)
                If Ni IsNot Nothing AndAlso Val(Ni.Tag) = CInt(Stack.Page) Then NavItem = Ni : Exit For
            Next
            If NavItem IsNot Nothing Then NavItem.SetChecked(True, True, PageNameGet(PageCurrent) = "")
            IsChangingPage = False
            Select Case Stack.Page
                Case PageType.Download
                    If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                    CheckLeftListItem(FrmDownloadLeft.PanItem, SubType, Stack = PageCurrent)
                Case PageType.Setup
                    If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                    CheckLeftListItem(FrmSetupLeft.PanItem, SubType, Stack = PageCurrent)
            End Select
            PageChangeActual(Stack, SubType)
        Else
            '切换到次页面
            Select Case Stack.Page
                Case PageType.InstanceSetup
                    If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                    CheckLeftListItem(FrmInstanceLeft.PanItem, SubType, Stack = PageCurrent)
            End Select
            PageChangeActual(Stack, SubType)
        End If
    End Sub

    ''' <summary>
    ''' 按 Tag 勾选左侧列表里的对应项（与子页面编号对应）。
    ''' 不能按索引直接取 Children(SubType)：下载页的左列表里混着「游戏 / 社区资源」这类分组标题，
    ''' 索引会落到 TextBlock 上，强转成 MyListItem 会抛 InvalidCastException（点「下载游戏」必现）。
    ''' </summary>
    Private Sub CheckLeftListItem(Panel As System.Windows.Controls.Panel, SubType As PageSubType, Anime As Boolean)
        If Panel Is Nothing Then Return
        For Each Child As Object In Panel.Children
            Dim Item As MyListItem = TryCast(Child, MyListItem)
            If Item Is Nothing OrElse Item.Tag Is Nothing Then Continue For
            If Val(Item.Tag) = CInt(SubType) Then
                Item.SetChecked(True, True, Anime)
                Return
            End If
        Next
    End Sub
    ''' <summary>
    ''' 通过点击导航栏改变页面。
    ''' </summary>
    Private Sub BtnTitleSelect_Click(sender As MyNavItem, raiseByMouse As Boolean) Handles BtnTitleSelect0.Check, BtnTitleSelect1.Check, BtnTitleSelect2.Check, BtnTitleSelectSeedMap.Check, BtnTitleSelect3.Check, BtnTitleSelect4.Check, BtnTitleSelect5.Check
        If IsChangingPage Then Return
        PageChangeActual(Val(sender.Tag))
    End Sub
    ''' <summary>
    ''' 通过点击返回按钮或手动触发返回来改变页面。
    ''' </summary>
    Public Sub PageBack() Handles BtnTitleInner.Click, BtnFloatingBack.Click
        If PageStack.Any() Then
            PageChangeActual(PageStack(0))
        Else
            PageChange(PageType.Launch)
        End If
    End Sub

    ''' <summary>
    ''' 鼠标侧键后退：内嵌网页内优先后退浏览历史，否则返回上一页。
    ''' </summary>
    Private Sub SideButtonBack()
        Try
            Dim Arr = TryCast(PageCurrent.Additional, Object())
            If PageCurrent.Page = PageType.Web AndAlso Arr IsNot Nothing AndAlso Arr.Length > 1 Then
                Dim Web = TryCast(Arr(1), PageWeb)
                If Web IsNot Nothing AndAlso Web.TryBack() Then Return
            End If
            PageBack()
        Catch ex As Exception
            Logger.Warn(ex, "侧键后退失败")
        End Try
    End Sub

    ''' <summary>
    ''' 鼠标侧键前进：仅在内嵌网页有前进历史时生效。
    ''' </summary>
    Private Sub SideButtonForward()
        Try
            Dim Arr = TryCast(PageCurrent.Additional, Object())
            If PageCurrent.Page = PageType.Web AndAlso Arr IsNot Nothing AndAlso Arr.Length > 1 Then
                Dim Web = TryCast(Arr(1), PageWeb)
                If Web IsNot Nothing Then Web.TryForward()
            End If
        Catch ex As Exception
            Logger.Warn(ex, "侧键前进失败")
        End Try
    End Sub

    '=== 全局鼠标钩子：WebView2 原生窗口会吞掉侧键消息，改用低级钩子接管 ===
    Private Const WH_MOUSE_LL As Integer = 14
    Private Const WM_XBUTTONDOWN As Integer = &H20B
    Private Delegate Function LowLevelMouseProcDelegate(nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    Private MouseHookProc As LowLevelMouseProcDelegate
    Private MouseHookId As IntPtr = IntPtr.Zero

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetWindowsHookEx(idHook As Integer, lpfn As LowLevelMouseProcDelegate, hMod As IntPtr, dwThreadId As UInteger) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function CallNextHookEx(hhk As IntPtr, nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function GetForegroundWindow() As IntPtr
    End Function

    Private Sub InstallMouseHook()
        Try
            If MouseHookId <> IntPtr.Zero Then Return
            MouseHookProc = AddressOf MouseHookCallback
            MouseHookId = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, Marshal.GetHINSTANCE(GetType(FormMain).Module), 0)
        Catch ex As Exception
            Logger.Warn(ex, "安装鼠标侧键钩子失败")
        End Try
    End Sub

    Private Function MouseHookCallback(nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        Try
            If nCode >= 0 AndAlso wParam.ToInt32() = WM_XBUTTONDOWN Then
                'MSLLHOOKSTRUCT：pt(8 字节) + mouseData(4 字节)，mouseData 高 16 位为侧键编号
                Dim MouseData As UInteger = CUInt(Marshal.ReadInt32(lParam, 8))
                Dim XButton As Integer = CInt((MouseData >> 16) And &HFFFF)
                '仅当启动器为前台窗口时接管，否则放行给其它程序
                If GetForegroundWindow() = Handle Then
                    If XButton = 1 Then
                        RunInUi(
                        Sub()
                            SideButtonBack()
                        End Sub, True)
                        Return New IntPtr(1) '吞掉消息，避免 WebView2 / WndProc 重复处理
                    ElseIf XButton = 2 Then
                        RunInUi(
                        Sub()
                            SideButtonForward()
                        End Sub, True)
                        Return New IntPtr(1)
                    End If
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "处理侧键钩子失败")
        End Try
        Return CallNextHookEx(MouseHookId, nCode, wParam, lParam)
    End Function

    '实际处理页面切换
    ''' <summary>
    ''' 切换现有页面的实际方法。
    ''' </summary>
    Private Sub PageChangeActual(Stack As PageStackData, Optional SubType As PageSubType = -1)
        '保险：开屏图标正在飞行时切换页面（用户点了导航/版本选择等），落点已失效，
        '先把飞行中的卡片淡出收掉，避免它悬在屏幕上或把 SplashIsActive 卡住
        If SplashFlightStarted Then AbortSplashFlight("切换页面")
        If PageCurrent = Stack AndAlso (PageCurrentSub = SubType OrElse SubType = -1) Then Return
        If Stack.Page = PageType.InstanceSetup AndAlso PageInstanceLeft.Instance IsNot Nothing AndAlso McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
            Hint("该版本正在被修改，暂时无法进入版本设置！", HintType.Red)
            Return
        End If
        AniControlEnabled += 1
        Try

#Region "子页面处理"
            Dim PageName As String = PageNameGet(Stack)
            If PageName = "" Then
                '即将切换到一个顶级页面
                PageChangeExit()
            Else
                '即将切换到一个子页面
                If PageStack.Any Then
                    '子页面 → 另一个子页面，更新（标题栏固定显示「缘启」，无面包屑动画）
                    If PageStack.Contains(Stack) Then
                        '返回到更上层的子页面
                        Do While PageStack.Contains(Stack)
                            PageStack.RemoveAt(0)
                        Loop
                    Else
                        '进入更深层的子页面
                        PageStack.Insert(0, PageCurrent)
                    End If
                Else
                    '主页面 → 子页面，进入（标题栏固定显示「缘启」，显示浮动返回键）
                    PopFloatingBack.IsOpen = True
                    UpdateHintOffset()
                    PageStack.Insert(0, PageCurrent)
                End If
            End If
#End Region

#Region "实际更改页面框架 UI"
            PageLast = PageCurrent
            PageCurrent = Stack
            LabTitleCrumb.Text = PageTitleText(Stack)
            Select Case Stack.Page
                Case PageType.Launch '启动
                    ConstructLaunchPages()
                    PageChangeAnim(FrmLaunchLeft, FrmLaunchRight)
                Case PageType.Download '下载
                    If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                    'PageGet 方法会在未设置 SubType 时指定默认值，并建立相关页面的实例
                    PageChangeAnim(FrmDownloadLeft, FrmDownloadLeft.PageGet(SubType))
                Case PageType.Link '联机
                    If FrmLinkMain Is Nothing Then FrmLinkMain = New PageLinkMain
                    PageChangeAnim(New MyPageLeft, FrmLinkMain)
                Case PageType.SeedMap '种子地图
                    If FrmSeedMapMain Is Nothing Then FrmSeedMapMain = New PageSeedMap
                    PageChangeAnim(New MyPageLeft, FrmSeedMapMain)
                Case PageType.Setup '设置
                    If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                    PageChangeAnim(FrmSetupLeft, FrmSetupLeft.PageGet(SubType))
                Case PageType.Other '关于（原「更多」，无二级菜单）
                    If FrmOtherAbout Is Nothing Then FrmOtherAbout = New PageOtherAbout
                    PageChangeAnim(New MyPageLeft, FrmOtherAbout)
                Case PageType.Tools '工具箱
                    If FrmToolsLeft Is Nothing Then FrmToolsLeft = New PageToolsLeft
                    '从别的页面带子页跳进来时（如主页「游玩洞察」→ 游戏统计），同步左侧选中项；
                    '从导航栏点进来时 SubType 是 -1，沿用上次选中的工具
                    If SubType >= 0 Then FrmToolsLeft.SyncChecked(SubType)
                    PageChangeAnim(FrmToolsLeft, FrmToolsLeft.PageGet(SubType))
                Case PageType.InstanceSelect '版本选择
                    If FrmSelectLeft Is Nothing Then FrmSelectLeft = New PageSelectLeft
                    If FrmSelectRight Is Nothing Then FrmSelectRight = New PageSelectRight
                    PageChangeAnim(FrmSelectLeft, FrmSelectRight)
                Case PageType.TaskManager '任务管理
                    If FrmSpeedLeft Is Nothing Then FrmSpeedLeft = New PageSpeedLeft
                    If FrmSpeedRight Is Nothing Then FrmSpeedRight = New PageSpeedRight
                    PageChangeAnim(FrmSpeedLeft, FrmSpeedRight)
                Case PageType.InstanceSetup '版本设置
                    If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                    PageChangeAnim(FrmInstanceLeft, FrmInstanceLeft.PageGet(SubType))
                Case PageType.ResourceDetail '社区资源详情
                    If FrmDownloadResourceDetail Is Nothing Then FrmDownloadResourceDetail = New PageDownloadResourceDetail
                    PageChangeAnim(New MyPageLeft, FrmDownloadResourceDetail)
                Case PageType.HelpDetail '帮助详情
                    PageChangeAnim(New MyPageLeft, Stack.Additional(1))
                Case PageType.Web '内嵌网页
                    PageChangeAnim(New MyPageLeft, CType(Stack.Additional(1), MyPageRight))
                Case PageType.SaveInfo '存档设置
                    PageChangeAnim(New MyPageLeft, CType(Stack.Additional(1), MyPageRight))
            End Select
#End Region

#Region "设置为最新状态"
            BtnExtraDownload.ShowRefresh()
            BtnExtraApril.ShowRefresh()
#End Region

            Logger.Info($"切换主要页面：{Stack.Page}, {SubType}")
        Catch ex As Exception
            Logger.Error(ex, $"切换主要页面失败（ID {PageCurrent.Page}）")
        Finally
            AniControlEnabled -= 1
        End Try
        '全幅页面（网页 / 种子地图）全宽展示：进入时自动把白色导航栏折叠至左侧一个图标身位，离开时恢复原状态
        '（放在 Try/Finally 之外，避免 AniControlEnabled 抑制折叠动画）
        Try
            If IsFullBleedPage(Stack.Page) AndAlso Not IsFullBleedPage(PageLast.Page) Then
                _RailCollapsedBeforeFullBleed = IsRailCollapsed
                IsRailCollapsed = True
                RefreshRailLayout(True)
            ElseIf IsFullBleedPage(PageLast.Page) AndAlso Not IsFullBleedPage(Stack.Page) Then
                IsRailCollapsed = _RailCollapsedBeforeFullBleed
                RefreshRailLayout(True)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "切换全幅页面时调整导航栏失败")
        End Try
    End Sub

    ''' <summary>是否为占满内容区、进入时需要折叠导航栏的全幅页面（网页 / 种子地图）。</summary>
    Private Function IsFullBleedPage(Page As PageType) As Boolean
        Return Page = PageType.Web OrElse Page = PageType.SeedMap
    End Function
    Private Sub PageChangeAnim(TargetLeft As FrameworkElement, TargetRight As FrameworkElement)
        AniStop("FrmMain LeftChange")
        AniStop("PageLeft PageChange") '停止左边栏变更导致的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        AniControlEnabled += 1
        '清除新页面关联性
        If TargetLeft.Parent IsNot Nothing Then TargetLeft.SetValue(ContentPresenter.ContentProperty, Nothing)
        If TargetRight IsNot Nothing AndAlso TargetRight.Parent IsNot Nothing Then TargetRight.SetValue(ContentPresenter.ContentProperty, Nothing)
        PageLeft = TargetLeft
        PageRight = TargetRight
        '触发页面通用动画
        CType(PanMainLeft.Child, MyPageLeft).TriggerHideAnimation()
        CType(PanMainRight.Child, MyPageRight).PageOnExit()
        AniControlEnabled -= 1
        '执行动画
        AniStart({
            AaCode(
            Sub()
                AniControlEnabled += 1
                '把新页面添加进容器
                PanMainLeft.Child = PageLeft
                PageLeft.Opacity = 0
                PanMainLeft.Background = Nothing
                '启动页采用单页全宽布局，取消左侧分栏
                If TypeOf PageLeft Is PageLaunchLeft Then
                    ApplyMainColumns(True)
                    PageLeft.Width = Double.NaN
                    PageLeft.HorizontalAlignment = HorizontalAlignment.Stretch
                ElseIf PageLeft.GetType() Is GetType(MyPageLeft) Then
                    '无二级菜单的页面（联机、资源详情等）
                    ApplyMainColumns(False)
                    PageLeft.Width = 0
                    PageLeft.HorizontalAlignment = HorizontalAlignment.Left
                Else
                    '二级菜单固定宽度 212
                    ApplyMainColumns(False)
                    PageLeft.Width = 212
                    PageLeft.HorizontalAlignment = HorizontalAlignment.Stretch
                End If
                AniControlEnabled -= 1
                RunInUi(Sub() PanMainLeft_Resize(PanMainLeft.ActualWidth), True)
            End Sub, 110),
            AaCode(
            Sub()
                '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                PageLeft.Opacity = 1
                PageLeft.TriggerShowAnimation()
            End Sub, 30, True)
        }, "FrmMain PageChangeLeft")
        AniStart({
            AaCode(
            Sub()
                AniControlEnabled += 1
                CType(PanMainRight.Child, MyPageRight).PageOnForceExit()
                '把新页面添加进容器
                PanMainRight.Child = PageRight
                PageRight.Opacity = 0
                PanMainRight.Background = Nothing
                AniControlEnabled -= 1
                RunInUi(Sub() BtnExtraBack.ShowRefresh(), True)
            End Sub, 110),
            AaCode(
            Sub()
                '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                PageRight.Opacity = 1
                PageRight.PageOnEnter()
            End Sub, 30, True)
        }, "FrmMain PageChangeRight")
    End Sub
    ''' <summary>
    ''' 退出子界面。
    ''' </summary>
    Private Sub PageChangeExit()
        If PageStack.Any Then
            '子页面 → 主页面，退出（标题栏固定显示「缘启」，隐藏浮动返回键）
            PageStack.Clear()
        End If
        PopFloatingBack.IsOpen = False
        UpdateHintOffset()
    End Sub

    '左边栏改变
    Private Sub PanMainLeft_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles PanMainLeft.SizeChanged
        If Not e.WidthChanged Then Return
        PanMainLeft_Resize(e.NewSize.Width)
    End Sub
    Private Sub PanMainLeft_Resize(NewWidth As Double)
        '导航栏折叠动画期间：背景宽度逐帧即时跟随，避免叠加二次动画产生回弹
        If _RailAnimating Then
            RectLeftBackground.Width = NewWidth
            PanMainLeft.IsHitTestVisible = True
            Return
        End If
        Dim Delta As Double = NewWidth - RectLeftBackground.Width
        If Math.Abs(Delta) > 0.1 AndAlso AniControlEnabled = 0 Then
            If PanMain.Opacity < 0.1 Then PanMainLeft.IsHitTestVisible = False '避免左边栏指向背景未能完美覆盖左边栏
            If NewWidth > 0 Then
                '宽度足够，显示
                AniStart({
                    AaWidth(RectLeftBackground, NewWidth - RectLeftBackground.Width, 180,, New AniEaseOutFluent(AniEasePower.ExtraStrong)),
                    AaOpacity(RectLeftShadow, 1 - RectLeftShadow.Opacity, 180),
                    AaCode(Sub() PanMainLeft.IsHitTestVisible = True, 150)
                }, "FrmMain LeftChange", True)
            Else
                '宽度不足，隐藏
                AniStart({
                    AaWidth(RectLeftBackground, -RectLeftBackground.Width, 180,, New AniEaseOutFluent),
                    AaOpacity(RectLeftShadow, -RectLeftShadow.Opacity, 180),
                    AaCode(Sub() PanMainLeft.IsHitTestVisible = True, 150)
                }, "FrmMain LeftChange", True)
            End If
        Else
            RectLeftBackground.Width = NewWidth
            PanMainLeft.IsHitTestVisible = True
            AniStop("FrmMain LeftChange")
        End If
    End Sub

#End Region

#Region "控件拖动"

    '在时钟中调用，使得即使鼠标在窗口外松开，也可以释放控件
    Public Sub DragTick()
        If DragControl Is Nothing Then Return
        If Not Mouse.LeftButton = MouseButtonState.Pressed Then
            DragStop()
        End If
    End Sub
    '在鼠标移动时调用，以改变 Slider 位置
    Public Sub DragDoing() Handles PanBack.MouseMove
        If DragControl Is Nothing Then Return
        If Mouse.LeftButton = MouseButtonState.Pressed Then
            DragControl.DragDoing()
        Else
            DragStop()
        End If
    End Sub
    Public Sub DragStop()
        '存在其他线程调用的可能性，因此需要确保在 UI 线程运行
        RunInUi(Sub()
                    If DragControl Is Nothing Then Return
                    Dim Control = DragControl
                    DragControl = Nothing
                    Control.DragStop() '控件会在该事件中判断 DragControl，所以得放在后面
                End Sub)
    End Sub

#End Region

#Region "附加按钮"

    '音乐
    Private Sub BtnExtraMusic_Click(sender As Object, e As EventArgs) Handles BtnExtraMusic.Click
        MusicControlPause()
    End Sub
    Private Sub BtnExtraMusic_RightClick(sender As Object, e As EventArgs) Handles BtnExtraMusic.RightClick
        MusicControlNext()
    End Sub

    '任务管理
    Private Sub BtnExtraDownload_Click(sender As Object, e As EventArgs) Handles BtnExtraDownload.Click
        PageChange(PageType.TaskManager)
    End Sub
    Private Function BtnExtraDownload_ShowCheck() As Boolean
        Return HasDownloadingTask() AndAlso Not PageCurrent = PageType.TaskManager
    End Function

    '投降
    Public Sub AprilGiveup() Handles BtnExtraApril.Click
        If IsAprilEnabled AndAlso Not IsAprilGiveup Then
            Hint("=D", HintType.Green)
            IsAprilGiveup = True
            FrmLaunchLeft.AprilScaleTrans.ScaleX = 1
            FrmLaunchLeft.AprilScaleTrans.ScaleY = 1
            BtnExtraApril.ShowRefresh()
        End If
    End Sub
    Public Function BtnExtraApril_ShowCheck() As Boolean
        Return IsAprilEnabled AndAlso Not IsAprilGiveup AndAlso PageCurrent = PageType.Launch
    End Function

    '关闭 Minecraft
    Public Sub BtnExtraShutdown_Click() Handles BtnExtraShutdown.Click
        Try
            If McLaunchLoaderReal IsNot Nothing Then McLaunchLoaderReal.Cancel()
            For Each Watcher In McWatcherList
                Watcher.Kill()
            Next
            Hint("已关闭运行中的 Minecraft！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "强制关闭所有 Minecraft 失败")
        End Try
    End Sub
    Public Function BtnExtraShutdown_ShowCheck() As Boolean
        Return HasRunningMinecraft
    End Function

    ''' <summary>
    ''' 返回顶部。
    ''' </summary>
    Public Sub BackToTop() Handles BtnExtraBack.Click
        Dim RealScroll As MyScrollViewer = BtnExtraBack_GetRealChild()
        If RealScroll IsNot Nothing Then
            RealScroll.PerformVerticalOffsetDelta(-RealScroll.VerticalOffset)
        Else
            Logger.Error("无法返回顶部，未找到合适的 RealScroll", LogBehavior.Toast)
        End If
    End Sub
    Private Function BtnExtraBack_ShowCheck() As Boolean
        Dim RealScroll As MyScrollViewer = BtnExtraBack_GetRealChild()
        Return RealScroll IsNot Nothing AndAlso RealScroll.Visibility = Visibility.Visible AndAlso RealScroll.VerticalOffset > Height + If(BtnExtraBack.Show, 0, 700)
    End Function
    Private Function BtnExtraBack_GetRealChild() As MyScrollViewer
        If PanMainRight.Child Is Nothing OrElse TypeOf PanMainRight.Child IsNot MyPageRight Then Return Nothing
        Dim Page As MyPageRight = PanMainRight.Child
        Return Page.FindName(Page.PanScroll)
    End Function

#End Region

    '愚人节鼠标位置
    Public lastMouseArg As MouseEventArgs = Nothing
    Private Sub FormMain_MouseMove(sender As Object, e As MouseEventArgs) Handles Me.MouseMove
        lastMouseArg = e
    End Sub

End Class
