Imports System.Reflection
Imports System.Windows.Threading
Imports System.Windows.Input

Public Class Application

    ''' <summary>
    ''' 文本框上下留白处的点击。
    ''' 模板里的内容宿主是 VerticalAlignment=Center，只有文字那一行能点中；框子做高之后
    ''' （比如彩色文字页那个 84px 的多行输入框），上下留白点下去没有任何反应。
    ''' 这里把留白处的点击转成「聚焦 + 光标落到最近的一端」，整个框就都能点了。
    ''' 注意：点在文字区里必须原样放过，否则会把光标位置抢成开头/结尾。
    ''' </summary>
    Private Sub MyTextBoxBlankMouseDown(sender As Object, e As MouseButtonEventArgs)
        Dim Blank As Border = TryCast(sender, Border)
        If Blank Is Nothing Then Return
        Dim Box As MyTextBox = TryCast(Blank.TemplatedParent, MyTextBox)
        If Box Is Nothing Then Return
        '点在文字那一行上：交给 TextView 自己定位光标，这里一点都不能碰
        If IsInsideContentHost(Blank, TryCast(e.OriginalSource, DependencyObject)) Then Return
        If Not Box.IsKeyboardFocusWithin Then Box.Focus()
        '留白处也要落到「鼠标正上下方的那个字」上，而不是简单扔到首尾
        Dim P As Point = e.GetPosition(Box)
        Dim Index As Integer = -1
        Try
            Index = Box.GetCharacterIndexFromPoint(P, True)
        Catch
        End Try
        If Index < 0 Then
            Index = If(P.Y * 2 < Box.ActualHeight, 0, Box.Text.Length)
        Else
            '落在「这个字之前」还是「之后」：取前后两个光标位置，比距离
            Dim Before As Double = Double.NaN
            Dim After As Double = Double.NaN
            Try
                Before = Box.GetRectFromCharacterIndex(Index, False).Left
            Catch
            End Try
            Try
                After = Box.GetRectFromCharacterIndex(Math.Min(Index + 1, Box.Text.Length), False).Left
            Catch
            End Try
            If Not Double.IsNaN(Before) AndAlso Not Double.IsNaN(After) AndAlso P.X > (Before + After) / 2 Then Index += 1
        End If
        Box.CaretIndex = Math.Max(0, Math.Min(Index, Box.Text.Length))
        e.Handled = True
    End Sub

    ''' <summary>这次点击是不是落在文字区（模板里的 PART_ContentHost）里。</summary>
    Private Function IsInsideContentHost(Blank As Border, Source As DependencyObject) As Boolean
        Dim Current As DependencyObject = Source
        Do While Current IsNot Nothing
            If TypeOf Current Is ScrollViewer Then Return True
            If ReferenceEquals(Current, Blank) Then Return False
            If TypeOf Current Is Visual Then
                Current = VisualTreeHelper.GetParent(Current)
            Else
                Current = LogicalTreeHelper.GetParent(Current)
            End If
        Loop
        Return False
    End Function

    ''' <summary>
    ''' 全局悬浮提示延迟的根治方案：直接覆写 ToolTipService 附加属性的元数据默认值。
    ''' 之前尝试过「页面切换时遍历可视化树逐个设置」和「MouseEnter 时补写」，都会失效：
    ''' 前者覆盖不到 Popup 与后来创建的元素；后者与 ToolTipService 内部的计时逻辑存在
    ''' 静态构造顺序竞态（它先读到旧默认值 100ms 就开始计时）。元数据默认值对包括
    ''' Popup 内容、动态创建元素在内的所有元素无条件生效，且必须在最早的托管代码里执行。
    ''' </summary>
    Shared Sub New()
        Try
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(ModMain.ToolTipShowDelay))
            '相邻按钮之间切换时也必须等满延迟（默认 100ms 会立刻弹出）
            ToolTipService.BetweenShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(0))
        Catch ex As Exception
            '覆写失败只影响提示延迟，不阻断启动
            Console.Error.WriteLine("ToolTipService 元数据覆写失败：" & ex.Message)
        End Try
    End Sub

    '开始
    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Try
            '核心初始化
            MeloongCore.Main.Init("YQL")
            MeloongCore.Wpf.Main.Init()
            If Not (e.Args.Contains("--update") OrElse e.Args.Contains("--gpu") OrElse e.Args.Contains("--memory")) Then '若是通过命令行调用，则不检查单例、不轮换日志（#9137）
                WaitingMutex(e)
                '不再使用系统级 SplashScreen（那张单独飘在屏幕中央的小图标）：
                '开屏视觉统一交给主窗口里的白色覆盖层 + 卡片（UiLauncherSplash），
                '否则会出现"一个小图标 → 又一张卡片"的重复观感。
                Logger.Instance = New PclLogger With {.logFolder = Paths.BaseThenName, .MinLevel = If(ModeDebug, LogLevel.Trace, LogLevel.Info)}
            End If
            '记录命令行参数，便于排查快捷方式参数（如 -egg）是否真的传进来了
            Try
                Dim RawArgs As String() = Environment.GetCommandLineArgs()
                If RawArgs.Length > 1 Then
                    Logger.Info($"命令行参数：{String.Join(" ", RawArgs.Skip(1))}")
                End If
            Catch ex As Exception
                Logger.Warn(ex, "记录命令行参数失败")
            End Try
            '提升主线程优先级
            Thread.CurrentThread.Priority = ThreadPriority.Highest
            '多核 JIT：记录启动期 JIT 热点，下次启动由后台线程并行编译，加快热启动
            Try
                DirectoryUtils.Create(Paths.AppDataThenName)
                System.Runtime.ProfileOptimization.SetProfileRoot(Paths.AppDataThenName)
                System.Runtime.ProfileOptimization.StartProfile("JitProfileStartup")
            Catch ex As Exception
                Logger.Warn(ex, "多核 JIT 配置失败")
            End Try
            '执行开发版测试
            If BuildType = BuildTypes.Debug Then
                Try
                    ModDevelop.Start()
                Catch ex As Exception
                    Logger.Error(ex, "开发者模式测试出错", LogBehavior.Alert)
                End Try
            End If
            '检查 .NET Framework 版本
            Try
                If CInt(RegistryUtils.TryRead("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", 999999)) < 528040 Then OldEnvironmentAssert()
            Catch ex As Exception
                Logger.Warn(ex, "检查 .NET Framework 版本失败")
            End Try
            '检查参数调用
            If e.Args.Length > 0 Then
                If e.Args(0) = "--update" Then
                    '自动更新
                    UpdateReplace(e.Args(1), e.Args(2).Trim(""""), e.Args(3).Trim(""""), e.Args(4))
                    Environment.Exit(ProcessReturnValues.TaskDone)
                ElseIf e.Args(0) = "--gpu" Then
                    '调整显卡设置
                    Try
                        SetGPUPreference(e.Args(1).Trim(""""))
                        Environment.Exit(ProcessReturnValues.TaskDone)
                    Catch ex As Exception
                        Environment.Exit(ProcessReturnValues.Fail)
                    End Try
                ElseIf e.Args(0) = "--memory" Then
                    '内存优化
                    Dim Ram = My.Computer.Info.AvailablePhysicalMemory
                    Try
                        PageOtherTest.MemoryOptimizeInternal(False)
                    Catch ex As Exception
                        MsgBox(ex.Message, MsgBoxStyle.Critical, "内存优化失败")
                        Environment.Exit(-1)
                    End Try
                    If My.Computer.Info.AvailablePhysicalMemory < Ram Then '避免 ULong 相减出现负数
                        Environment.Exit(0)
                    Else
                        Environment.Exit((My.Computer.Info.AvailablePhysicalMemory - Ram) / 1024) '返回清理的内存量（K）
                    End If
                End If
            End If
            '修复 WPF 字体加载（#3467）
            '使用系统消息字体而不是 Fonts.SystemTypefaces.First，避免同步枚举全部系统字体（本机约 1175 个，耗时约 160 ms）。
            '若仍触发 UriFormatException，再回退到原来的全量枚举逻辑。
            Try
                Dim _unused As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                                 New Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                                                 96, New MyColor, DPI)
            Catch ex As UriFormatException
                Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVariableTarget.User)
                Dim _unused As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
            End Try
            '初始化文件结构（失败时先重试：刚更新完或安全软件扫描时文件可能被短暂占用）
            Try
                DirectoryUtils.Create(Paths.Base & "YQL\Pictures\")
                DirectoryUtils.Create(Paths.Base & "YQL\Musics\")
                CheckPermissionWithException(Paths.Base & "YQL\")
            Catch ex As Exception
                Dim Fixed As Boolean = False
                For i = 1 To 6
                    Thread.Sleep(350)
                    Try
                        DirectoryUtils.Create(Paths.Base & "YQL\")
                        CheckPermissionWithException(Paths.Base & "YQL\")
                        Fixed = True
                        Exit For
                    Catch
                    End Try
                Next
                If Not Fixed Then
                    MsgBox($"缘启没有对当前文件夹的权限（{Paths.Base}YQL\），请尝试：" & vbCrLf &
                      "1. 将缘启移动到其他文件夹" & If(PathUtils.GetDiskName(Paths.Base) = "C"c, "，例如 C 盘和桌面以外的其他位置。", "。") & vbCrLf &
                      "2. 删除当前目录中的 YQL 文件夹，然后再试。" & vbCrLf &
                      "3. 右键 缘启 选择属性，打开 兼容性 中的 以管理员身份运行此程序。" & vbCrLf &
                      "4. 若刚完成安装或更新，请等安装程序结束后再启动缘启。" & vbCrLf & vbCrLf &
                      "错误详情：" & ex.Message,
                    MsgBoxStyle.Critical, "运行环境错误")
                    Environment.[Exit](ProcessReturnValues.Cancel)
                End If
            End Try
            Try
                DirectoryUtils.Create(PathTemp)
                CheckPermissionWithException(PathTemp)
            Catch ex As Exception
                MyMsgBox("缘启无法访问缓存文件夹，可能导致程序出错或无法正常使用！" & vbCrLf & vbCrLf & "错误原因：" & ex.GetDisplay(True), "缓存文件夹不可用")
            End Try
            DirectoryUtils.Create(PathTemp & "Cache\")
            DirectoryUtils.Create(PathCache)
            '把旧版留在临时目录的缓存搬过来，避免升级后第一次还要重新算一遍
            For Each CacheName In {"ModHash.ini", "LocalMod.json"}
                Try
                    Dim OldPath As String = PathTemp & "Cache\" & CacheName
                    Dim NewPath As String = PathCache & CacheName
                    If Not FileUtils.Exists(NewPath) AndAlso FileUtils.Exists(OldPath) Then
                        FileUtils.Copy(OldPath, NewPath)
                        Logger.Info($"已迁移旧缓存：{CacheName}")
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, $"迁移旧缓存失败：{CacheName}")
                End Try
            Next
            DirectoryUtils.Create(Paths.AppDataThenName)
            '设置网络配置默认值
            ServicePointManager.Expect100Continue = False
            ServicePointManager.DefaultConnectionLimit = 10000
            ServicePointManager.UseNagleAlgorithm = False
            ServicePointManager.EnableDnsRoundRobin = True
            ServicePointManager.ReusePort = True
            If Environment.OSVersion.Version < New Version(6, 2) Then 'Windows 7 回退（#8519）
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 Or SecurityProtocolType.Tls Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls12
            End If
            '基础信息
            Logger.Info($"程序版本：{VersionDisplay} ({VersionCode}{If(CommitHash = "", "", $"，#{CommitHash}")})")
            If BuildType = BuildTypes.Snapshot Then
                Logger.Info($"识别码：{Identify}（主题颜色已全部开放）")
            Else
                Logger.Info($"识别码：{Identify}")
            End If
            Logger.Info($"程序路径：{PathExe}")
            Logger.Info($"系统编码：{Encoding.Default.HeaderName} ({Encoding.Default.CodePage}, GBK={IsGBKEncoding})")
            Logger.Info($"管理员权限：{WindowsUtils.HasAdminRole()}")
            '检测异常环境
            If Paths.Base.Contains(Path.GetTempPath()) OrElse Paths.Base.Contains("AppData\Local\Temp\") Then
                MyMsgBox("请将缘启从压缩包中解压后再使用！" & vbCrLf & "如果不会解压，可以在网上寻找教程。", "需要解压！", "我知道了", IsWarn:=True, ForceWait:=True)
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
            End If
            If Not Environment.Is64BitOperatingSystem Then
                MyMsgBox("缘启和新版 Minecraft 均不再支持 32 位系统，请重装为 64 位系统后再进行游戏！", "环境警告", "我知道了", IsWarn:=True, ForceWait:=True)
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
            End If
            '计时
            Logger.Info($"第一阶段加载用时：{GetTimeMs() - ApplicationStartTick} ms")
            ApplicationStartTick = GetTimeMs()
            AniControlEnabled += 1
        Catch ex As Exception
            Dim FilePath As String = Nothing
            Try
                FilePath = PathExe
            Catch
            End Try
            MsgBox(ex.GetDisplay(True) & vbCrLf & "缘启所在路径：" & If(String.IsNullOrEmpty(FilePath), "获取失败", FilePath), MsgBoxStyle.Critical, "缘启初始化错误")
            FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Try
    End Sub
    ''' <summary>
    ''' 要求程序以单例运行。
    ''' 如果已在运行，则将其窗口拖出来并播放提示音，然后结束当前进程。
    ''' 可在 设置 → 其他 中开启「允许多开」以关闭该限制。
    ''' </summary>
    Private Shared Sub WaitingMutex(e As StartupEventArgs)
        If BuildType = BuildTypes.Debug Then Return
        Try
            '允许同时开启多个实例
            Try
                If Settings.Get(Of Boolean)("SystemMultiInstance") Then
                    Logger.Info("已启用多开，跳过单例检查")
                    Return
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取多开设置失败，将按单例运行")
            End Try
            Dim MutexCreatedNew As Boolean
            PclMutex = New Mutex(True, "YQL_SingletonMutex", MutexCreatedNew)
            If MutexCreatedNew Then Return
            Logger.Warn("已有一个缘启实例正在运行")
            '等待已有的缘启退出
            If e.Args.Length > 0 AndAlso e.Args(0) = "--wait" Then
                Try
                    If PclMutex.WaitOne(10000) Then Return '等待至多 10 秒
                Catch Ignored As AbandonedMutexException
                    Return '已有实例异常退出，继续启动
                End Try
            End If
            '将已有的缘启窗口拖出来（最小化的窗口会先做系统级还原）
            Dim WindowHwnd As IntPtr = FindWindow(Nothing, "缘启 YuanQi Launcher　")
            If WindowHwnd = IntPtr.Zero Then WindowHwnd = FindWindow(Nothing, "YuanQi Launcher　")
            If WindowHwnd <> IntPtr.Zero Then ShowWindowToTop(WindowHwnd)
            '带 -spring / -national / -egg 之类参数启动被单例拦下时，说明本次启动不会播放动画，明确告知用户
            Try
                If ModSplashEvent.ForcedType IsNot Nothing Then
                    MsgBox("已有一个缘启实例正在运行，本次快捷方式的特殊动画参数不会生效。" & vbCrLf & vbCrLf &
                           "请先关闭已打开的缘启再试，或在 设置 → 其他 → 辅助功能 中开启「允许多开启动器」。",
                           MsgBoxStyle.Information, "特殊动画参数未生效")
                End If
            Catch
            End Try
            '退出（不再播放系统提示音，避免"只响一声却没出现"的误会）
            Environment.[Exit](ProcessReturnValues.Cancel)
        Catch ex As Exception
            Logger.Warn(ex, "单例检查失败")
        End Try
    End Sub
    Private Shared PclMutex As Mutex

    '结束
    Private Sub Application_SessionEnding(sender As Object, e As SessionEndingCancelEventArgs) Handles Me.SessionEnding
        FrmMain?.EndProgram(False)
    End Sub

    '异常
    Private Sub Application_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs) Handles Me.DispatcherUnhandledException
        On Error Resume Next
        '触发页面的 Dispatcher
        If FrmMain?.PageLeft IsNot Nothing AndAlso TypeOf FrmMain.PageLeft Is IDispatcherUnhandledException Then
            CType(FrmMain.PageLeft, IDispatcherUnhandledException).DispatcherUnhandledException(sender, e)
            If e.Handled Then Return
        End If
        If FrmMain?.PageRight IsNot Nothing AndAlso TypeOf FrmMain.PageRight Is IDispatcherUnhandledException Then
            CType(FrmMain.PageRight, IDispatcherUnhandledException).DispatcherUnhandledException(sender, e)
            If e.Handled Then Return
        End If
        '正常处理
        e.Handled = True
        If IsProgramEnding Then Return
        FeedbackInfo()
        Dim Detail As String = e.Exception.GetDisplay(True)
        If Detail.Contains("System.Windows.Threading.Dispatcher.Invoke") OrElse Detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") OrElse Detail.Contains("未能加载文件或程序集") OrElse
           Detail.Contains(".NET Framework") Then ' “自动错误判断” 的结果分析
            OldEnvironmentAssert(e.Exception)
        Else
            Logger.Error(e.Exception, "程序出现未知错误", LogBehavior.AlertThenCrash)
        End If
    End Sub
    Private Shared Sub OldEnvironmentAssert(Optional ex As Exception = Nothing)
        OpenWebsite("https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net48-offline-installer")
        If ex Is Nothing Then
            Logger.Error($"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{vbCrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash)
        Else
            Logger.Error(ex, $"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{vbCrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash)
        End If
    End Sub

    '动态 DLL 加载
    Private Sub New() '这里必须尽早调用，且不能使用任何库，否则加载 MeloongCore 就会导致崩溃
        Static Prefixes As String() = {"NAudio", "Newtonsoft.Json", "Ookii.Dialogs.Wpf", "Imazen.WebP", "CacheCow.Common", "CacheCow.Client.FileStore", "CacheCow.Client", "ThrottleDebounce", "System.Net.Http.Formatting", "Microsoft.Win32.Registry", "PCLCS", "MeloongCore.Wpf", "MeloongCore"}
        Static LoadedAssemblies As New ConcurrentDictionary(Of String, Lazy(Of Assembly))(StringComparer.Ordinal) '缓存
        AddHandler AppDomain.CurrentDomain.AssemblyResolve,
        Function(sender As Object, Args As ResolveEventArgs) As Assembly
            Dim Prefix As String = Prefixes.FirstOrDefault(Function(p) Args.Name.StartsWith(p, StringComparison.Ordinal))
            If Prefix Is Nothing Then Return Nothing
            Dim LazyAssembly = LoadedAssemblies.GetOrAdd(Prefix, Function(p) New Lazy(Of Assembly)(
            Function()
                Dim LoadedAssembly = Assembly.Load(DirectCast(My.Resources.ResourceManager.GetObject(p), Byte()))
                If p = "Imazen.WebP" Then ExtractLibwebp() 'WebP 特判
                Return LoadedAssembly
            End Function, LazyThreadSafetyMode.ExecutionAndPublication))
            Return LazyAssembly.Value
        End Function
    End Sub
    Private Shared Sub ExtractLibwebp() '这个方法会调用 ModBase，进而调用 MeloongCore，所以不能放在 New 里
        SetDllDirectory(PathUtils.RemoveSlashSuffix(PathPure.Value))
        Try
            ExtractResources(PathPure.Value & "libwebp.dll", "libwebp64")
        Catch ex As Exception
            Logger.Warn(ex, "写入 libwebp.dll 失败") '防止同时加载多个图片时，同时写入文件导致文件占用，进而导致崩溃
        End Try
    End Sub
    Private Declare Function SetDllDirectory Lib "kernel32" Alias "SetDllDirectoryA" (lpPathName As String) As Boolean

    '控件模板事件
    Private Sub MyIconButton_Click(sender As Object, e As EventArgs)
        Select Case Settings.Get(Of McLoginType)("LoginType")
            Case McLoginType.Ms
                '微软
                Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
                MsJson.Remove(sender.Tag)
                Settings.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None))
                If FrmLoginMs.ComboAccounts.SelectedItem Is sender.Parent Then FrmLoginMs.ComboAccounts.SelectedIndex = 0
                FrmLoginMs.ComboAccounts.Items.Remove(sender.Parent)
            Case McLoginType.Legacy
                '离线
                Dim Names As New List(Of String)
                Names.AddRange(Settings.Get(Of String)("LoginLegacyName").ToString.Split("¨"))
                Names.Remove(sender.Tag)
                Settings.Set("LoginLegacyName", Names.Join("¨"c))
                If FrmLaunchLeft IsNot Nothing Then
                    FrmLaunchLeft.ComboName.ItemsSource = If(Names.Any, Names, Nothing)
                    FrmLaunchLeft.ComboName.SelectedItem = If(Names.Any, Names(0), Nothing)
                End If
            Case Else
                '第三方
                Dim Token As String = Settings.Get(Of McLoginType)("LoginType").ToString()
                Dim Dict As New Dictionary(Of String, String)
                Dim Names As New List(Of String)
                Dim Passs As New List(Of String)
                If Not Settings.Get(Of String)("Login" & Token & "Email") = "" Then Names.AddRange(Settings.Get(Of String)("Login" & Token & "Email").Split("¨"))
                If Not Settings.Get(Of String)("Login" & Token & "Pass") = "" Then Passs.AddRange(Settings.Get(Of String)("Login" & Token & "Pass").Split("¨"))
                For i = 0 To Names.Count - 1
                    Dict.Add(Names(i), Passs(i))
                Next
                Dict.Remove(sender.Tag)
                Settings.Set("Login" & Token & "Email", Dict.Keys.Join("¨"c))
                Settings.Set("Login" & Token & "Pass", Dict.Values.Join("¨"c))
                Select Case Token
                    Case "Nide"
                        FrmLoginNide.ComboName.ItemsSource = Dict.Keys
                        FrmLoginNide.ComboName.Text = If(Dict.Keys.Any, Dict.Keys(0), "")
                        FrmLoginNide.TextPass.Password = If(Dict.Values.Any, Dict.Values(0), "")
                    Case "Auth"
                        FrmLoginAuth.ComboName.ItemsSource = Dict.Keys
                        FrmLoginAuth.ComboName.Text = If(Dict.Keys.Any, Dict.Keys(0), "")
                        FrmLoginAuth.TextPass.Password = If(Dict.Values.Any, Dict.Values(0), "")
                End Select
        End Select
    End Sub

End Class
