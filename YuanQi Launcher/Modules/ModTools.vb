Imports Microsoft.Win32

''' <summary>
''' 杂项工具：更新检查、环境体检、空间体检。
''' </summary>
Public Module ModTools

#Region "仓库与版本"

    ''' <summary>
    ''' 缘启的 GitHub 仓库地址，更新检查与反馈入口都基于它。
    ''' </summary>
    Public Const RepoUrl As String = "https://github.com/YuanzaiC/YuanQi-Launcher"

    ''' <summary>
    ''' 缘启官网地址，关于页的「前往官网」按钮使用它。
    ''' </summary>
    Public Const WebsiteUrl As String = "https://yuanzaic.github.io/YuanQi-Launcher/"

    ''' <summary>
    ''' 发布包（单文件安装包）的命名规则，需与 Releases 里的资产名一致。
    ''' </summary>
    Public Function InstallerAssetName(Version As String) As String
        Return $"YuanQi-Setup-{Version}.exe"
    End Function

    ''' <summary>
    ''' 仓库是否可用（地址为空时直接跳过联网检查）。
    ''' </summary>
    Public Function IsRepoReady() As Boolean
        Return Not String.IsNullOrWhiteSpace(RepoUrl)
    End Function

#End Region

#Region "启动器更新"

    ''' <summary>临时开关：检测期间关掉启动器更新检查。恢复时改成 False 即可。</summary>
    Friend Const UpdateCheckPaused As Boolean = False
    ''' <summary>
    ''' 测试开关：不比较版本号，一律按"有更新"处理，直接走下载/安装流程。
    ''' 恢复正常行为时改成 False。
    ''' </summary>
    Friend Const UpdateCheckIgnoreVersion As Boolean = False

    ''' <summary>自动检查时用户点过「跳过此版本」的版本号，记在设置里，跨启动有效。</summary>
    Public Const SkipVersionKey As String = "UpdateSkipVersion"
    ''' <summary>已经下载好、但用户选了「稍后」的安装包路径。</summary>
    Public Const PendingInstallerKey As String = "UpdatePendingInstaller"
    ''' <summary>下载完成后是否直接安装（设置项）。</summary>
    Public Const AutoInstallKey As String = "UpdateAutoInstall"

    ''' <summary>本次运行里已经弹过提示的版本，避免自动检查重复打扰。</summary>
    Private PromptedVersion As String = ""
    ''' <summary>是否正在检查更新，防止连点「立即检查更新」并发起多次请求。</summary>
    Private Checking As Boolean = False
    Private ReadOnly CheckLock As New Object

    ''' <summary>是否正在检查更新：界面据此把按钮切成「检查中」。</summary>
    Public ReadOnly Property IsCheckingUpdate As Boolean
        Get
            Return Checking
        End Get
    End Property
    ''' <summary>开始检查更新（手动、自动都会触发）。</summary>
    Public Event UpdateCheckStarted As Action
    ''' <summary>检查结束，无论成功还是失败。</summary>
    Public Event UpdateCheckFinished As Action

    ''' <summary>
    ''' 一次更新检查的结果：版本、发布时间、发布说明与安装包信息。
    ''' </summary>
    Public Class LauncherRelease
        Public Version As String = ""
        Public Tag As String = ""
        Public Title As String = ""
        Public PublishedAt As Date
        Public Body As String = ""
        Public PageUrl As String = ""
        Public AssetName As String = ""
        Public AssetUrl As String = ""
        Public AssetSize As Long = -1
        Public AssetHash As String = ""
    End Class

    ''' <summary>
    ''' 检查启动器更新。
    ''' </summary>
    ''' <param name="Silent">自动检测时为 True：没有更新就静默，有新版本才弹窗。</param>
    Public Sub CheckLauncherUpdate(Optional Silent As Boolean = False)
        If UpdateCheckPaused Then
            Logger.Info("启动器更新检查已临时关闭（UpdateCheckPaused = True）")
            If Not Silent Then Hint("更新检查已临时关闭", HintType.Blue)
            Return
        End If
        If Not IsRepoReady() Then
            If Not Silent Then Hint("暂时无法检查更新，请稍后再试。", HintType.Blue)
            Logger.Info("启动器仓库地址为空，跳过更新检查")
            Return
        End If
        '网络请求不能在 UI 线程执行，统一放到后台线程，界面操作再切回 UI 线程
        SyncLock CheckLock
            '已经在检查了就直接返回：按钮这会儿本来就是「检查中」，不再弹提示
            If Checking Then Return
            Checking = True
        End SyncLock
        RaiseEvent UpdateCheckStarted()
        RunInNewThread(
        Sub()
            Try
                DoCheckLauncherUpdate(Silent)
            Catch ex As Exception
                Logger.Warn(ex, "检查启动器更新失败")
                If Not Silent Then
                    RunInUiWait(
                    Sub()
                        If ex.Message.Contains("404") Then
                            MyMsgBox("仓库里还没有发布任何版本，暂时无法检查更新。", "检查更新")
                        Else
                            MyMsgBox("检查更新失败：" & vbCrLf & ex.Message, "检查更新", IsWarn:=True)
                        End If
                    End Sub)
                End If
            Finally
                Checking = False
                RaiseEvent UpdateCheckFinished()
            End Try
        End Sub, "Check Launcher Update", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>把 tag 规范化成版本号（去掉开头的 v/V，大小写都兼容）。</summary>
    Private Function NormalizeVersion(Tag As String) As String
        Dim V As String = If(Tag, "").Trim()
        V = V.TrimStart("v"c, "V"c)
        Return V.Trim()
    End Function

    ''' <summary>在后台线程执行真正的更新检查，弹窗统一切回 UI 线程。</summary>
    Private Sub DoCheckLauncherUpdate(Silent As Boolean)
        Dim Release As LauncherRelease = FetchLatestRelease()
        If Release Is Nothing Then
            If Not Silent Then RunInUiWait(Sub() Hint("无法解析更新信息。", HintType.Red))
            Return
        End If
        '按版本号大小比较，不能只看字符串是否相等：
        '本地版本比仓库里的发布还新（比如刚打包还没发布）时必须按「已是最新」处理，
        '否则点「立即更新」会把启动器装回旧版本。
        Dim CompareResult As Integer = CompareVersion(Release.Version, VersionBaseName)
        Logger.Info($"启动器最新版本：{Release.Version}（tag：{Release.Tag}），当前版本：{VersionBaseName}，比较结果：{CompareResult}")
        If CompareResult <= 0 AndAlso Not UpdateCheckIgnoreVersion Then
            If CompareResult < 0 Then Logger.Info($"仓库里的最新发布（{Release.Version}）比当前版本旧，按已是最新处理")
            If Not Silent Then RunInUiWait(Sub() MyMsgBox($"当前已是最新版本（{VersionBaseName}）。", "检查更新"))
            Return
        End If
        '自动检查：用户主动跳过过的版本不再打扰
        If Silent AndAlso Settings.Get(Of String)(SkipVersionKey) = Release.Version Then
            Logger.Info($"用户已跳过版本 {Release.Version}，本次不再提示")
            Return
        End If
        '先把发布说明存下来：装完之后新版本要拿它弹更新日志
        SaveUpdateNotes(Release)
        RunInUiWait(Sub() PromptUpdate(Release, Silent))
    End Sub

    ''' <summary>读取仓库里最新的 Release（版本、发布时间、发布说明与安装包资产）。</summary>
    Private Function FetchLatestRelease() As LauncherRelease
        Dim Raw As String = NetRequestByClient($"https://api.github.com/repos/{RepoUrl.Replace("https://github.com/", "")}/releases/latest",
                                               RequireJson:=True, Timeout:=10000)
        Dim ReleaseJson = CType(Raw.DeserializeJson(), JObject)
        Dim Tag = ReleaseJson?("tag_name")
        If Tag Is Nothing Then Return Nothing
        Dim Result As New LauncherRelease With {
            .Tag = Tag.ToString.Trim(),
            .Version = NormalizeVersion(Tag.ToString),
            .Title = If(ReleaseJson?("name")?.ToString(), "").Trim(),
            .Body = If(ReleaseJson?("body")?.ToString(), "").Trim(),
            .PageUrl = If(ReleaseJson?("html_url")?.ToString(), "").Trim()
        }
        If String.IsNullOrWhiteSpace(Result.Version) Then Return Nothing
        If String.IsNullOrWhiteSpace(Result.PageUrl) Then Result.PageUrl = RepoUrl & "/releases/latest"
        Try
            Dim Published As String = ReleaseJson?("published_at")?.ToString()
            If Not String.IsNullOrWhiteSpace(Published) Then Result.PublishedAt = Date.Parse(Published).ToLocalTime()
        Catch ex As Exception
            Logger.Warn(ex, "解析发布发布时间失败")
        End Try
        '直接从 release 的资产里取安装包，不依赖固定命名（tag 大小写、资产名都能对上）
        Dim Assets = TryCast(ReleaseJson?("assets"), JArray)
        If Assets IsNot Nothing Then
            For Each Item In Assets
                Dim Name As String = If(Item?("name")?.ToString(), "").Trim()
                If Not Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) Then Continue For
                '已经挑到带 Setup 的资产就不再换
                If Result.AssetUrl <> "" AndAlso Name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) < 0 Then Continue For
                Result.AssetName = Name
                Result.AssetUrl = If(Item?("browser_download_url")?.ToString(), "").Trim()
                '顺手取大小和官方给的 sha256：有了这两个，下载引擎才能分段多线程并做完整性校验
                Try
                    If Item?("size") IsNot Nothing Then Result.AssetSize = Item("size").ToObject(Of Long)()
                Catch ex As Exception
                End Try
                Try
                    Dim Digest As String = If(Item?("digest")?.ToString(), "")
                    If Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) Then Result.AssetHash = Digest.Substring(7)
                Catch ex As Exception
                End Try
                If Name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0 Then Exit For
            Next
        End If
        If String.IsNullOrWhiteSpace(Result.AssetUrl) Then
            '没有可用的资产：退回按约定命名拼地址
            Result.AssetName = InstallerAssetName(Result.Version)
            Result.AssetUrl = $"{RepoUrl}/releases/download/{Result.Tag}/{Result.AssetName}"
        End If
        Return Result
    End Function

    ''' <summary>
    ''' 弹出更新提示。自动检查时第三个按钮是「跳过此版本」，手动检查时第三个按钮打开完整更新日志。
    ''' </summary>
    Private Sub PromptUpdate(Release As LauncherRelease, Silent As Boolean)
        Try
            '同一次运行里，自动检查对同一版本只弹一次
            If Silent Then
                If PromptedVersion = Release.Version Then Return
                PromptedVersion = Release.Version
            End If
            Dim Notes As String = FormatReleaseNotes(Release.Body, 6)
            Dim Content As New StringBuilder
            Content.AppendLine($"发现新版本：{Release.Version}")
            Content.Append($"当前版本：{VersionBaseName}")
            If Release.PublishedAt > Date.MinValue Then Content.Append($"　·　{Release.PublishedAt:yyyy-MM-dd}")
            Content.AppendLine()
            If Notes <> "" Then
                Content.AppendLine()
                Content.AppendLine("更新内容：")
                Content.AppendLine(Notes)
            End If
            Content.AppendLine()
            Content.Append("更新只替换程序文件，设置、游戏与存档都会保留。")
            Dim Choice As Integer
            If Silent Then
                Choice = MyMsgBox(Content.ToString, $"发现新版本 {Release.Version}", "立即更新", "稍后", "跳过此版本")
            Else
                Choice = MyMsgBox(Content.ToString, $"发现新版本 {Release.Version}", "立即更新", "稍后", "完整更新日志",
                                  Button3Action:=Sub() OpenWebsite(Release.PageUrl))
            End If
            Select Case Choice
                Case 1
                    DownloadInstaller(Release)
                Case 3
                    If Silent Then
                        Settings.Set(SkipVersionKey, Release.Version)
                        Logger.Info($"用户跳过版本 {Release.Version}")
                    End If
            End Select
        Catch ex As Exception
            Logger.Warn(ex, "显示更新提示失败")
        End Try
    End Sub

    ''' <summary>
    ''' 把 GitHub 上的发布说明整理成弹窗里能看的几行：去掉 Markdown 记号、空行与代码块。
    ''' </summary>
    Public Function FormatReleaseNotes(Body As String, Optional MaxLines As Integer = 8) As String
        Dim Result As New List(Of String)
        If String.IsNullOrWhiteSpace(Body) Then Return ""
        For Each RawLine In Body.Replace(vbCrLf, vbLf).Split(ControlChars.Lf)
            Dim Line As String = RawLine.Trim()
            If Line = "" Then Continue For
            If Line.StartsWith("<!--") OrElse Line.StartsWith("```") OrElse Line.StartsWith("|") Then Continue For
            If Line.StartsWith("---") Then Continue For
            Line = Line.TrimStart("#"c).Trim()
            Line = Line.Replace("**", "").Replace("`", "").Replace("~~", "")
            If Line.StartsWith("- ") OrElse Line.StartsWith("* ") Then Line = "· " & Line.Substring(2).Trim()
            If Line.Length > 140 Then Line = Line.Substring(0, 140) & "…"
            Result.Add(Line)
            If Result.Count >= MaxLines Then Exit For
        Next
        Return Result.Join(vbCrLf)
    End Function

    ''' <summary>更新说明存在 YQL\Cache\Update 下，按版本号命名。</summary>
    Private Function UpdateNotesPath(Version As String) As String
        Return PathCache & "Update\" & Version & ".json"
    End Function

    ''' <summary>
    ''' 把这次更新的说明存一份。安装包替换程序文件时不会动 YQL，所以更新完成后新版本还能读到它。
    ''' </summary>
    Public Sub SaveUpdateNotes(Release As LauncherRelease)
        Try
            If Release Is Nothing OrElse String.IsNullOrWhiteSpace(Release.Version) Then Return
            Dim Dir As String = PathCache & "Update\"
            If Not IO.Directory.Exists(Dir) Then IO.Directory.CreateDirectory(Dir)
            Dim Json As New JObject
            Json("version") = Release.Version
            Json("title") = Release.Title
            Json("published") = If(Release.PublishedAt > Date.MinValue, Release.PublishedAt.ToString("yyyy-MM-dd"), "")
            Json("url") = Release.PageUrl
            Json("body") = Release.Body
            IO.File.WriteAllText(UpdateNotesPath(Release.Version), Json.ToString(), New Text.UTF8Encoding(False))
            Logger.Info($"已保存 {Release.Version} 的更新说明，更新后用于弹出更新日志")
        Catch ex As Exception
            Logger.Warn(ex, "保存更新说明失败")
        End Try
    End Sub

    ''' <summary>
    ''' 取出当前版本对应的更新说明（取完即删，只弹一次），顺手清掉旧版本留下的。
    ''' 没有说明时返回 Nothing，调用方退回内置的更新日志。
    ''' </summary>
    Public Function ConsumeUpdateNotes() As JObject
        Try
            Dim Dir As String = PathCache & "Update\"
            If Not IO.Directory.Exists(Dir) Then Return Nothing
            For Each NotesFile In IO.Directory.GetFiles(Dir, "*.json")
                If IO.Path.GetFileNameWithoutExtension(NotesFile) <> VersionBaseName Then
                    Try : FileUtils.Delete(NotesFile) : Catch : End Try
                End If
            Next
            Dim NotesPath As String = UpdateNotesPath(VersionBaseName)
            If Not IO.File.Exists(NotesPath) Then Return Nothing
            Dim NotesText As String = IO.File.ReadAllText(NotesPath)
            FileUtils.Delete(NotesPath)
            Return CType(NotesText.DeserializeJson(), JObject)
        Catch ex As Exception
            Logger.Warn(ex, "读取更新说明失败")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 走启动器自己的下载器下载安装包：任务列表里能看到进度，支持多源切换与分段下载。
    ''' </summary>
    Private Sub DownloadInstaller(Release As LauncherRelease)
        Try
            Dim FileName As String = If(String.IsNullOrWhiteSpace(Release.AssetName), "YuanQi-Setup.exe", Release.AssetName)
            Dim SavePath As String = IO.Path.Combine(PathTemp, FileName)
            '上一次剩下的半截文件会让下载器以为已经下完，先清掉
            Try
                If FileUtils.Exists(SavePath) Then FileUtils.Delete(SavePath)
            Catch ex As Exception
            End Try
            Dim Urls As New List(Of String) From {Release.AssetUrl}
            'GitHub 直链在国内经常连不上，再挂一个镜像，下载器会自己挑能用的
            If Release.AssetUrl.Contains("https://github.com/") Then
                Urls.Add(Release.AssetUrl.Replace("https://github.com/", "https://ghfast.top/https://github.com/"))
            End If
            Dim Checker As FileChecker = Nothing
            If Release.AssetSize > 0 Then Checker = New FileChecker With {.ActualSize = Release.AssetSize}
            If Not String.IsNullOrWhiteSpace(Release.AssetHash) Then
                If Checker Is Nothing Then Checker = New FileChecker
                Checker.Hash = Release.AssetHash
            End If
            Dim Files As New List(Of NetFile) From {New NetFile(Urls, SavePath, Checker)}
            Dim Tasks As New List(Of LoaderBase) From {New LoaderDownload("下载安装包", Files) With {.Block = True}}
            Dim Loader As New LoaderCombo(Of Integer)("启动器更新", Tasks) With {
                .OnStateChanged =
                Sub(MyLoader)
                    LoaderStateChangedHintOnly(MyLoader)
                    Select Case MyLoader.State
                        Case LoadState.Finished
                            '下载前已经存过一份，这里再兜一次，确保装完能弹更新日志
                            SaveUpdateNotes(Release)
                            If Settings.Get(Of Boolean)(AutoInstallKey) Then
                                ApplyUpdate(SavePath)
                            Else
                                PromptInstallInstaller(SavePath, FileName, Release)
                            End If
                        Case LoadState.Failed
                            Hint("下载安装包失败，已为你打开下载页面。", HintType.Red)
                            OpenWebsite(Release.PageUrl)
                    End Select
                End Sub
            }
            Loader.Start(1)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            Hint("已开始下载安装包，进度在右下角任务列表里", HintType.Blue, False)
        Catch ex As Exception
            Logger.Warn(ex, "启动安装包下载失败")
            Hint("下载安装包失败，已为你打开下载页面。", HintType.Red)
            OpenWebsite(Release.PageUrl)
        End Try
    End Sub

    ''' <summary>下载完成后询问是否立即安装。</summary>
    Private Sub PromptInstallInstaller(SavePath As String, FileName As String, Release As LauncherRelease)
        Try
            RunInUi(
            Sub()
                Dim SizeText As String = ""
                Try
                    SizeText = $"（{New IO.FileInfo(SavePath).Length / 1024 / 1024:0.0} MB）"
                Catch ex As Exception
                End Try
                If MyMsgBox($"安装包已下载完成：{FileName}{SizeText}{vbCrLf}{vbCrLf}" &
                            "点「立即安装」后，缘启会自动关闭、替换程序文件并重新打开。" & vbCrLf &
                            "设置、游戏与存档都会保留。",
                            $"安装缘启 {Release.Version}", "立即安装", "稍后") = 1 Then
                    ApplyUpdate(SavePath)
                Else
                    Settings.Set(PendingInstallerKey, SavePath)
                    Hint("安装包已保存，下次启动会再问一次，也可以自己双击安装。", HintType.Blue)
                End If
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "询问是否安装更新失败")
        End Try
    End Sub

    ''' <summary>
    ''' 静默安装已下载的更新包，然后关闭缘启（安装程序会重新拉起）。
    ''' 参数说明：/s 静默、/d= 原地更新、/update 更新模式（安装失败时会弹窗说明原因）。
    ''' 老版本安装程序不认识 /update 也没关系，/s /d= 已经能完成原地更新。
    ''' </summary>
    Public Sub ApplyUpdate(InstallerPath As String)
        Try
            If Not FileUtils.Exists(InstallerPath) Then
                Hint("安装包不见了，请重新检查更新。", HintType.Red)
                Settings.Set(PendingInstallerKey, "")
                Return
            End If
            Dim Target As String = Paths.Base.TrimEnd("\"c, "/"c)
            Dim Args As String = $"/s /d=""{Target}"" /update"
            Logger.Info($"开始安装更新：{InstallerPath} {Args}")
            Settings.Set(PendingInstallerKey, "")
            StartProcess(InstallerPath, Args)
            Hint("正在安装更新，缘启即将自动重启……", HintType.Blue, False)
            '稍等一下再退出，确保安装程序已经起来
            RunInNewThread(
            Sub()
                Thread.Sleep(600)
                RunInUi(
                Sub()
                    Try
                        '不用 Close()：那会走「还有下载任务未完成」的确认，更新时不该再拦一道
                        FrmMain.EndProgram(False)
                    Catch ex As Exception
                        Logger.Warn(ex, "关闭缘启以完成更新失败")
                    End Try
                End Sub, True)
            End Sub, "Apply Update")
        Catch ex As Exception
            Logger.Warn(ex, "启动更新安装失败")
            Hint("无法自动安装更新，已为你打开安装包。", HintType.Red)
            Try
                StartProcess(InstallerPath)
            Catch ex2 As Exception
                Logger.Warn(ex2, "打开安装包失败")
            End Try
        End Try
    End Sub

    ''' <summary>上次下载好但没装的更新包：启动时问一次要不要装。</summary>
    Public Sub PromptPendingInstaller()
        Try
            Dim PendingPath As String = Settings.Get(Of String)(PendingInstallerKey)
            If String.IsNullOrWhiteSpace(PendingPath) Then Return
            If Not FileUtils.Exists(PendingPath) Then
                Settings.Set(PendingInstallerKey, "")
                Return
            End If
            RunInNewThread(
            Sub()
                Thread.Sleep(2500) '等窗口和开屏动画起来
                If ModGuide.IsRunning Then Return
                RunInUiWait(
                Sub()
                    If MyMsgBox($"上次下载的安装包还在：{IO.Path.GetFileName(PendingPath)}{vbCrLf}{vbCrLf}" &
                                "现在安装吗？安装后缘启会自动重启，设置与存档都会保留。",
                                "安装已下载的更新", "立即安装", "忽略") = 1 Then
                        ApplyUpdate(PendingPath)
                    Else
                        Settings.Set(PendingInstallerKey, "")
                    End If
                End Sub)
            End Sub, "Pending Update Install", ThreadPriority.BelowNormal)
        Catch ex As Exception
            Logger.Warn(ex, "提示待安装更新失败")
        End Try
    End Sub

    ''' <summary>启动时按设置自动检测更新（默认关闭）。</summary>
    Public Sub AutoCheckLauncherUpdate()
        Try
            If UpdateCheckPaused Then Return
            If Not Settings.Get(Of Boolean)("UiAutoCheckUpdate") Then Return
            RunInNewThread(
            Sub()
                Thread.Sleep(8000) '错开启动高峰
                '教程、开屏动画期间不弹更新提示，免得抢镜
                Dim Waited As Integer = 0
                Do While ModGuide.IsRunning AndAlso Waited < 60000
                    Thread.Sleep(500)
                    Waited += 500
                Loop
                If ModGuide.IsRunning Then Return
                CheckLauncherUpdate(True)
            End Sub, "Auto Update Check", ThreadPriority.BelowNormal)
        Catch ex As Exception
            Logger.Warn(ex, "自动更新检测启动失败")
        End Try
    End Sub

#End Region

#Region "陶瓦联机模块更新"

    ''' <summary>联机模块检查是否正在进行（界面据此把按钮切成「检查中」）。</summary>
    Public ReadOnly Property IsCheckingTerracotta As Boolean
        Get
            Return CheckingTerracotta
        End Get
    End Property
    ''' <summary>开始检查联机模块更新。</summary>
    Public Event TerracottaCheckStarted As Action
    ''' <summary>联机模块检查结束，无论成功还是失败。</summary>
    Public Event TerracottaCheckFinished As Action
    Private CheckingTerracotta As Boolean = False
    Private ReadOnly TerracottaCheckLock As New Object

    ''' <summary>
    ''' 检查陶瓦联机模块是否有新版本（对比本地模块与 GitHub 最新 Release）。
    ''' </summary>
    Public Sub CheckTerracottaUpdate()
        SyncLock TerracottaCheckLock
            '已经在查了就直接返回：按钮这会儿本来就是「检查中」，不再弹提示气泡
            If CheckingTerracotta Then Return
            CheckingTerracotta = True
        End SyncLock
        RaiseEvent TerracottaCheckStarted()
        Dim LocalVersion As String = GetLocalTerracottaVersion()
        '网络请求不能在 UI 线程执行
        RunInNewThread(
        Sub()
            Try
                Dim Latest As String = ModTerracotta.GetLatestVersion()
                If String.IsNullOrWhiteSpace(Latest) Then
                    RunInUiWait(Sub() MyMsgBox("无法获取联机模块的最新版本，请检查网络后重试。", "联机模块更新", IsWarn:=True))
                    Return
                End If
                Dim Message As String = $"本地联机模块：{If(String.IsNullOrWhiteSpace(LocalVersion), "未知", LocalVersion)}{vbCrLf}" &
                    $"最新版本：{Latest}"
                If String.IsNullOrWhiteSpace(LocalVersion) Then
                    Message &= vbCrLf & vbCrLf & "本地版本未知（可能是手动放置的模块）。"
                ElseIf CompareVersion(LocalVersion, Latest) >= 0 Then
                    RunInUiWait(Sub() MyMsgBox(Message & vbCrLf & vbCrLf & "已是最新版本。", "联机模块更新"))
                    Return
                End If
                RunInUiWait(
                Sub()
                    If MyMsgBox(Message & vbCrLf & vbCrLf & "是否打开下载页面？", "联机模块更新", "打开", "取消") = 1 Then
                        OpenWebsite("https://github.com/burningtnt/Terracotta/releases/latest")
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Warn(ex, "检查联机模块更新失败")
                RunInUiWait(Sub() MyMsgBox("检查失败：" & vbCrLf & ex.Message, "联机模块更新", IsWarn:=True))
            Finally
                CheckingTerracotta = False
                RaiseEvent TerracottaCheckFinished()
            End Try
        End Sub, "Check Terracotta Update", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>读取本地联机模块版本（从 exe 文件版本信息），读不到返回空串。</summary>
    Private Function GetLocalTerracottaVersion() As String
        Try
            If FileUtils.Exists(ModTerracotta.BundledCorePath) Then
                Dim Info = Diagnostics.FileVersionInfo.GetVersionInfo(ModTerracotta.BundledCorePath)
                If Not String.IsNullOrWhiteSpace(Info.FileVersion) Then Return Info.FileVersion.Trim
            End If
            If FileUtils.Exists(ModTerracotta.CorePath) Then
                Dim Info = Diagnostics.FileVersionInfo.GetVersionInfo(ModTerracotta.CorePath)
                If Not String.IsNullOrWhiteSpace(Info.FileVersion) Then Return Info.FileVersion.Trim
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取联机模块版本失败")
        End Try
        Return ""
    End Function

    ''' <summary>比较形如 0.4.2 / 2.15.3.0 的版本号，返回 -1/0/1。</summary>
    Private Function CompareVersion(A As String, B As String) As Integer
        Try
            Dim PA = A.Split("."c), PB = B.Split("."c)
            For i = 0 To Math.Max(PA.Length, PB.Length) - 1
                '注意：不能写成 If(i < PA.Length AndAlso Integer.TryParse(PA(i), VA), 0, 0)
                '那样两边分支都是 0，A 的每一段都会被当成 0，任何版本比较都恒等于「A 更旧」
                Dim VA As Integer = 0
                Dim VB As Integer = 0
                If i < PA.Length Then Integer.TryParse(PA(i), VA)
                If i < PB.Length Then Integer.TryParse(PB(i), VB)
                If VA <> VB Then Return Math.Sign(VA - VB)
            Next
            Return 0
        Catch
            Return String.CompareOrdinal(A, B)
        End Try
    End Function

#End Region

#Region "环境体检"

    ''' <summary>
    ''' 检查运行环境并返回逐项结果（True = 正常）。
    ''' </summary>
    Public Function RunEnvCheck() As List(Of KeyValuePair(Of String, String))
        Dim Result As New List(Of KeyValuePair(Of String, String))
        'Windows 版本
        Try
            Dim IsWin10OrNewer As Boolean = Environment.OSVersion.Version.Major >= 10
            Result.Add(New KeyValuePair(Of String, String)("Windows 版本",
                If(IsWin10OrNewer, $"OK · {Environment.OSVersion.Version}", "需要 Windows 10 及以上（联机模块要求）")))
        Catch
        End Try
        '…NET Framework
        Try
            Dim ReleaseKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")?.GetValue("Release")
            Dim ReleaseValue As Integer = If(ReleaseKey Is Nothing, 0, CInt(ReleaseKey))
            Result.Add(New KeyValuePair(Of String, String)(".NET Framework 4.8",
                If(ReleaseValue >= 528040, "OK", $"需要 4.8（当前 Release={ReleaseValue}）")))
        Catch
            Result.Add(New KeyValuePair(Of String, String)(".NET Framework 4.8", "无法检测"))
        End Try
        'WebView2 运行时
        Try
            Dim Found As Boolean = False
            For Each Root In {Registry.LocalMachine, Registry.CurrentUser}
                For Each Key In {"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
                                 "SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"}
                    Try
                        Using SubKey = Root.OpenSubKey(Key)
                            If SubKey?.GetValue("pv") IsNot Nothing Then Found = True
                        End Using
                    Catch
                    End Try
                Next
            Next
            Result.Add(New KeyValuePair(Of String, String)("WebView2 运行时",
                If(Found, "OK", "未检测到（内嵌网页/彩蛋动画需要）")))
        Catch
        End Try
        'VC++ 运行库（联机模块依赖）
        Try
            Dim HasVc As Boolean = FileUtils.Exists(ModTerracotta.BundledDataPath & "VCRUNTIME140.dll") OrElse
                                   FileUtils.Exists(ModTerracotta.DataPath & "VCRUNTIME140.dll")
            Result.Add(New KeyValuePair(Of String, String)("VC++ 运行库",
                If(HasVc, "OK", "缺少 VCRUNTIME140.dll（联机模块无法启动）")))
        Catch
        End Try
        '内嵌依赖（我们自带，缺失说明安装不完整）
        Try
            Result.Add(New KeyValuePair(Of String, String)("System.Numerics.Vectors",
                If(FileUtils.Exists(Paths.Base & "System.Numerics.Vectors.dll"), "OK", "缺失（彩蛋动画的透明视频会失效）")))
        Catch
        End Try
        '联机模块（联机功能本体）
        Try
            Result.Add(New KeyValuePair(Of String, String)("联机模块",
                If(ModTerracotta.IsCorePresent(), "OK", "缺失（联机功能不可用，点下面的按钮下载）")))
        Catch
        End Try
        'tar.exe（解压联机模块用）
        Try
            Result.Add(New KeyValuePair(Of String, String)("系统 tar.exe",
                If(FileUtils.Exists(Environment.SystemDirectory & "\tar.exe"), "OK", "缺失（无法自动解压联机模块）")))
        Catch
        End Try
        Return Result
    End Function

    ''' <summary>联机模块是否正在下载，防止连点重复建任务。</summary>
    Private TerracottaDownloading As Boolean = False

    ''' <summary>下载并安装联机模块（体检发现缺失时用）。走启动器自己的下载器，进度显示在右下角任务列表。</summary>
    Public Sub DownloadTerracottaCore()
        If TerracottaDownloading Then
            Hint("联机模块正在下载中，进度在右下角任务列表里", HintType.Blue, False)
            Return
        End If
        TerracottaDownloading = True
        Hint("正在获取联机模块版本……", HintType.Blue, False)
        RunInNewThread(
        Sub()
            Try
                Dim Latest As String = ModTerracotta.GetLatestVersion()
                If String.IsNullOrWhiteSpace(Latest) Then Throw New Exception("无法获取联机模块的最新版本，请检查网络")
                '资源名用的是 x86_64 / arm64 / x86，不是 x64
                Dim Arch As String
                Select Case System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
                    Case System.Runtime.InteropServices.Architecture.X64
                        Arch = "x86_64"
                    Case System.Runtime.InteropServices.Architecture.Arm64
                        Arch = "arm64"
                    Case Else
                        Arch = "x86"
                End Select
                Dim Urls = ModTerracotta.GetDownloadUrls(Arch, Latest)
                If Urls Is Nothing OrElse Urls.Count = 0 Then Throw New Exception("没有找到可用的下载地址")
                Dim Local As String = PathTemp & "TerracottaCore.tar.gz"
                DirectoryUtils.Create(PathTemp)
                '上一次剩下的半截文件会让下载器以为已经下完，先清掉
                Try
                    If FileUtils.Exists(Local) Then FileUtils.Delete(Local)
                Catch ex As Exception
                End Try
                RunInUi(Sub() StartTerracottaInstallLoader(Urls, Local, Latest))
            Catch ex As Exception
                TerracottaDownloading = False
                Logger.Warn(ex, "下载联机模块失败")
                RunInUi(Sub() Hint("联机模块下载失败：" & ex.Message, HintType.Red))
            End Try
        End Sub, "Prepare Terracotta Download", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>把联机模块的下载与解压挂到任务列表上，用户能在右下角看到进度。</summary>
    Private Sub StartTerracottaInstallLoader(Urls As List(Of String), Local As String, Latest As String)
        Dim Tasks As New List(Of LoaderBase) From {
            New LoaderDownload("下载联机模块", New List(Of NetFile) From {
                New NetFile(Urls, Local, New FileChecker With {.MinSize = 1024 * 1024})}) With {.ProgressWeight = 80},
            New LoaderTask(Of Integer, Integer)("解压联机模块", Sub(InstallTask)
                                                                    InstallTask.Progress = 0.1
                                                                    ModTerracotta.ExtractPackage(Local)
                                                                    InstallTask.Progress = 0.85
                                                                    ModTerracotta.MarkCoreVersion(Latest)
                                                                End Sub) With {.ProgressWeight = 20}
        }
        Dim Loader As New LoaderCombo(Of Integer)("安装联机模块", Tasks) With {
            .OnStateChanged =
            Sub(MyLoader)
                LoaderStateChangedHintOnly(MyLoader)
                Select Case MyLoader.State
                    Case LoadState.Finished
                        TerracottaDownloading = False
                        Hint($"联机模块 {Latest} 已就绪", HintType.Green)
                    Case LoadState.Failed
                        TerracottaDownloading = False
                        Hint("联机模块下载失败，可稍后重试", HintType.Red)
                End Select
            End Sub
        }
        Loader.Start(1)
        LoaderTaskbarAdd(Loader)
        FrmMain.BtnExtraDownload.ShowRefresh()
        Hint("已开始下载联机模块，进度在右下角任务列表里", HintType.Blue, False)
    End Sub
    Public Sub ShowEnvCheck()
        Try
            Dim Items = RunEnvCheck()
            Dim Lines As New List(Of String)
            For Each Item In Items
                Lines.Add($"{If(Item.Value.StartsWithF("OK"), "✓", "✗")} {Item.Key}：{Item.Value}")
            Next
            Dim HasProblem As Boolean = Items.Any(Function(I) Not I.Value.StartsWithF("OK"))
            MyMsgBox(String.Join(vbCrLf, Lines) & If(HasProblem, vbCrLf & vbCrLf & "带 ✗ 的项目可能导致部分功能不可用，可尝试重装缘启或安装对应运行库。", ""),
                     "环境体检", "确定", IsWarn:=HasProblem)
        Catch ex As Exception
            Logger.Error(ex, "环境体检失败")
            MyMsgBox("环境体检失败：" & vbCrLf & ex.Message, "环境体检", IsWarn:=True)
        End Try
    End Sub

    ''' <summary>首次启动时静默体检一次，只在发现问题时提示。</summary>
    Public Sub AutoEnvCheck()
        Try
            If Settings.Get(Of Integer)("SystemEnvCheckVersion") >= 1 Then Return
            Settings.Set("SystemEnvCheckVersion", 1)
            RunInNewThread(Sub()
                               Try
                                   Thread.Sleep(3000)
                                   Dim Items = RunEnvCheck()
                                   Dim Problems = Items.Where(Function(I) Not I.Value.StartsWithF("OK")).ToList()
                                   If Not Problems.Any() Then Return
                                   RunInUi(Sub() ShowEnvCheck())
                               Catch ex As Exception
                                   Logger.Warn(ex, "自动环境体检失败")
                               End Try
                           End Sub, "Auto Env Check", ThreadPriority.BelowNormal)
        Catch ex As Exception
            Logger.Warn(ex, "启动自动环境体检失败")
        End Try
    End Sub

#End Region

#Region "空间体检"

    ''' <summary>单个版本的空间占用。</summary>
    Public Class SpaceItem
        Public Name As String
        Public Path As String
        Public Bytes As Long
        Public IsArchived As Boolean
    End Class

    ''' <summary>统计当前文件夹下所有版本与可清理项的占用。</summary>
    Public Function RunSpaceAudit() As List(Of SpaceItem)
        Dim Result As New List(Of SpaceItem)
        Try
            Dim VersionsPath As String = McFolderSelected & "versions\"
            If Not DirectoryUtils.Exists(VersionsPath) Then Return Result
            For Each Folder As String In DirectoryUtils.EnumerateDirectories(VersionsPath)
                Try
                    Dim Item As New SpaceItem With {
                        .Name = IO.Path.GetFileName(Folder.TrimEnd("\"c)),
                        .Path = Folder,
                        .IsArchived = ModArchive.IsArchived(Folder)
                    }
                    Item.Bytes = InstanceSizeStore.TryGet(Folder)
                    If Item.Bytes <= 0 Then
                        Item.Bytes = ModArchive.GetFolderSize(Folder, Nothing)
                        If Item.Bytes > 0 Then InstanceSizeStore.Store(Folder, Item.Bytes)
                    End If
                    Result.Add(Item)
                Catch ex As Exception
                    Logger.Warn(ex, $"统计版本占用失败：{Folder}")
                End Try
            Next
            Result = Result.OrderByDescending(Function(I) I.Bytes).ToList()
        Catch ex As Exception
            Logger.Warn(ex, "空间体检失败")
        End Try
        Return Result
    End Function

    ''' <summary>
    ''' 枚举可安全清理的日志与崩溃报告文件。启动器日志只清理 7 天以前的，避免动到正在写入的文件。
    ''' </summary>
    Private Function EnumerateCleanableFiles() As List(Of String)
        Dim Files As New List(Of String)
        '游戏的日志与崩溃报告：可以全部清理
        For Each Folder In {McFolderSelected & "crash-reports\", McFolderSelected & "logs\"}
            Try
                If DirectoryUtils.Exists(Folder) Then Files.AddRange(DirectoryUtils.EnumerateFiles(Folder, True))
            Catch ex As Exception
                Logger.Warn(ex, $"枚举清理目录失败：{Folder}")
            End Try
        Next
        '启动器日志：保留最近 7 天
        Try
            Dim LogFolder As String = Paths.BaseThenName & "logs\"
            If DirectoryUtils.Exists(LogFolder) Then
                For Each FilePath As String In DirectoryUtils.EnumerateFiles(LogFolder, True)
                    Try
                        If IO.File.GetLastWriteTime(FilePath) < Now.AddDays(-7) Then Files.Add(FilePath)
                    Catch
                    End Try
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "枚举启动器日志失败")
        End Try
        Return Files
    End Function

    ''' <summary>可安全清理的临时内容（日志、崩溃报告）占用。</summary>
    Public Function GetCleanableBytes() As Long
        Dim Total As Long = 0
        Try
            For Each FilePath In EnumerateCleanableFiles()
                Try
                    Total += New IO.FileInfo(FilePath).Length
                Catch
                End Try
            Next
        Catch ex As Exception
            Logger.Warn(ex, "统计可清理内容失败")
        End Try
        Return Total
    End Function

    ''' <summary>显示空间体检结果，并提供一键清理日志/崩溃报告。</summary>
    Public Sub ShowSpaceAudit()
        Try
            Dim Items = RunSpaceAudit()
            If Not Items.Any() Then
                Hint("当前文件夹下没有版本。", HintType.Blue)
                Return
            End If
            Dim Total As Long = Items.Sum(Function(I) I.Bytes)
            Dim Cleanable As Long = GetCleanableBytes()
            Dim Lines As New List(Of String) From {
                $"当前文件夹：{McFolderSelected}",
                $"版本总数：{Items.Count}　合计占用：{ModArchive.FormatSize(Total)}",
                $"可清理（日志/崩溃报告）：{ModArchive.FormatSize(Cleanable)}",
                ""
            }
            For Each Item In Items.Take(8)
                Lines.Add($"{If(Item.IsArchived, "〔已归档〕", "")}{Item.Name}　{ModArchive.FormatSize(Item.Bytes)}")
            Next
            If Items.Count > 8 Then Lines.Add($"……另有 {Items.Count - 8} 个版本未列出")
            Dim Answer = MyMsgBox(String.Join(vbCrLf, Lines) & vbCrLf & vbCrLf &
                                  "是否清理日志与崩溃报告？（不会影响游戏与存档）",
                                  "空间体检", "清理", "关闭", "归档候选")
            If Answer = 1 Then CleanupSpace()
            If Answer = 3 Then ShowArchiveCandidates(Items)
        Catch ex As Exception
            Logger.Error(ex, "空间体检失败")
            MyMsgBox("空间体检失败：" & vbCrLf & ex.Message, "空间体检", IsWarn:=True)
        End Try
    End Sub

    ''' <summary>清理日志与崩溃报告。</summary>
    Private Sub CleanupSpace()
        Try
            Dim Count As Integer = 0
            For Each FilePath In EnumerateCleanableFiles()
                Try
                    FileUtils.Delete(FilePath)
                    Count += 1
                Catch ex As Exception
                    Logger.Warn(ex, $"清理失败：{FilePath}")
                End Try
            Next
            Hint($"已清理 {Count} 个临时文件！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "清理空间失败")
        End Try
    End Sub

    ''' <summary>某个版本的最近游玩时间：优先按版本标识匹配（改名后依然有效），老记录才退回按名字。</summary>
    Private Function GetInstanceLastPlay(LastPlay As Dictionary(Of String, Date), Item As SpaceItem) As Date
        Try
            If Item Is Nothing Then Return Date.MinValue
            Dim Id As String = InstanceIdentityModule.GetInstanceId(Item.Path, CreateIfMissing:=False)
            If Not String.IsNullOrWhiteSpace(Id) Then
                Dim ById As Date
                If LastPlay.TryGetValue("i:" & Id, ById) Then Return ById
            End If
            Dim ByName As Date
            If LastPlay.TryGetValue("n:" & Item.Name, ByName) Then Return ByName
        Catch ex As Exception
            Logger.Warn(ex, "读取版本最近游玩时间失败")
        End Try
        Return Date.MinValue
    End Function

    ''' <summary>列出长期未游玩、适合归档的版本，支持多选后一键归档。</summary>
    Private Sub ShowArchiveCandidates(Items As List(Of SpaceItem))
        Try
            Dim Sessions = PlayLogModule.LoadSessions()
            '按「版本标识」归并：版本改名后，它的游玩记录仍然算在它头上
            Dim LastPlay As New Dictionary(Of String, Date)(StringComparer.OrdinalIgnoreCase)
            For Each S In Sessions
                Dim Key As String = PlayLogModule.GetSessionKey(S)
                If String.IsNullOrWhiteSpace(Key) OrElse Key = "n:" Then Continue For
                If Not LastPlay.ContainsKey(Key) OrElse LastPlay(Key) < S.EndTime Then
                    LastPlay(Key) = S.EndTime
                End If
            Next
            Dim Candidates = Items.Where(Function(I) Not I.IsArchived).
                Select(Function(I) New With {
                    .Item = I,
                    .Last = GetInstanceLastPlay(LastPlay, I)
                }).
                Where(Function(X) X.Last = Date.MinValue OrElse (Now - X.Last).TotalDays >= 60).
                OrderByDescending(Function(X) X.Item.Bytes).ToList()
            If Not Candidates.Any() Then
                MyMsgBox("没有发现长期未游玩（60 天以上）的版本。", "归档候选")
                Return
            End If
            '构建勾选列表
            Dim ListItems As New List(Of MyListItem)
            For Each C In Candidates
                Dim LastText As String = If(C.Last = Date.MinValue, "从未游玩", $"最后游玩 {C.Last:yyyy-MM-dd}")
                Dim ListItem As New MyListItem With {
                    .Title = C.Item.Name,
                    .Info = $"占用 {ModArchive.FormatSize(C.Item.Bytes)} · {LastText}",
                    .Height = 40,
                    .Type = MyListItem.CheckType.CheckBox
                }
                ListItem.Tag = C.Item.Path
                ListItems.Add(ListItem)
            Next
            Dim Selected = MyMsgBoxSelectMultiple(ListItems, $"找到 {Candidates.Count} 个久未游玩的版本", "归档选中", "取消")
            If Selected Is Nothing OrElse Selected.Count = 0 Then Return
            Dim Paths As New List(Of String)
            For Each Index In Selected
                Paths.Add(CType(ListItems(Index).Tag, String))
            Next
            Dim Check As Integer = MyMsgBox($"将深度压缩归档 {Paths.Count} 个版本。{vbCrLf}归档不会删除存档与配置，之后可在版本列表右侧点击「解压」还原。{vbCrLf}{vbCrLf}确定开始归档吗？",
                                            "批量归档", "开始归档", "取消")
            If Check <> 1 Then Return
            PageSelectRight.RunArchiveBatch(Paths)
        Catch ex As Exception
            Logger.Error(ex, "列出归档候选失败")
            MyMsgBox("归档候选处理失败：" & vbCrLf & ex.Message, "归档候选", IsWarn:=True)
        End Try
    End Sub

#End Region

End Module
