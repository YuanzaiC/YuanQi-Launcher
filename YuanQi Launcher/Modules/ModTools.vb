Imports Microsoft.Win32

''' <summary>
''' 杂项工具：更新检查、环境体检、空间体检。
''' <para/>更新相关的仓库地址目前是占位值，等 GitHub 仓库建好后改 <see cref="RepoUrl"/> 即可生效。
''' </summary>
Public Module ModTools

#Region "仓库与版本"

    ''' <summary>
    ''' 缘启的 GitHub 仓库地址。仓库尚未建立，先留占位；填好后更新检查与反馈入口即可用。
    ''' </summary>
    Public Const RepoUrl As String = "https://github.com/YuanQi-Launcher/YuanQi-Launcher"

    ''' <summary>
    ''' 仓库地址是否还是占位（占位时不去请求 API，直接提示"尚未发布"）。
    ''' </summary>
    Public Function IsRepoReady() As Boolean
        Return Not RepoUrl.Contains("YuanQi-Launcher/YuanQi-Launcher")
    End Function

#End Region

#Region "启动器更新（预留入口）"

    ''' <summary>
    ''' 检查启动器更新。仓库尚未建立时只给出提示。
    ''' </summary>
    ''' <param name="Silent">自动检测时为 True：没有更新就静默，不做任何弹窗。</param>
    Public Sub CheckLauncherUpdate(Optional Silent As Boolean = False)
        Try
            If Not IsRepoReady() Then
                If Not Silent Then Hint("暂时无法检查更新，请稍后再试。", HintType.Blue)
                Logger.Info("启动器仓库尚未建立，跳过更新检查")
                Return
            End If
            Dim Raw As String = NetRequestByClient($"https://api.github.com/repos/{RepoUrl.Replace("https://github.com/", "")}/releases/latest",
                                                   RequireJson:=True, Timeout:=10000)
            Dim Tag = CType(Raw.DeserializeJson(), JObject)?("tag_name")
            If Tag Is Nothing Then
                If Not Silent Then Hint("无法解析更新信息。", HintType.Red)
                Return
            End If
            Dim TagRaw As String = Tag.ToString
            Dim Latest As String = TagRaw.TrimStart("v"c)
            Logger.Info($"启动器最新版本：{Latest}，当前版本：{VersionBaseName}")
            If Latest = VersionBaseName Then
                If Not Silent Then MyMsgBox($"当前已是最新版本（{VersionBaseName}）。", "检查更新")
                Return
            End If
            If Silent Then
                '自动检测：只提示，不打扰
                Hint($"缘启有新版本 {Latest} 可用，可在设置中检查更新。", HintType.Green)
                Return
            End If
            If MyMsgBox($"发现新版本：{Latest}{vbCrLf}当前版本：{VersionBaseName}{vbCrLf}{vbCrLf}是否立即下载安装包并更新？{vbCrLf}更新只替换程序文件，设置、游戏与存档都会保留。",
                        "检查更新", "立即更新", "稍后") = 1 Then
                DownloadInstaller(TagRaw, Latest)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "检查启动器更新失败")
            If Not Silent Then MyMsgBox("检查更新失败：" & vbCrLf & ex.Message, "检查更新", IsWarn:=True)
        End Try
    End Sub

    ''' <summary>
    ''' 下载发布页里的单文件安装包，下载完成后引导用户启动它完成更新。
    ''' </summary>
    Private Sub DownloadInstaller(Tag As String, Latest As String)
        Dim FileName As String = $"YuanQi-Setup-{Latest}.exe"
        Dim Url As String = $"{RepoUrl}/releases/download/{Tag}/{FileName}"
        Dim SavePath As String = IO.Path.Combine(IO.Path.GetTempPath(), FileName)
        Hint($"正在下载安装包（约 17 MB）……", HintType.Blue, False)
        RunInNewThread(
        Sub()
            Try
                Using Client As New Net.WebClient()
                    Client.DownloadFile(Url, SavePath)
                End Using
                RunInUi(
                Sub()
                    If MyMsgBox($"安装包已下载完成：{FileName}{vbCrLf}{vbCrLf}立即启动安装程序吗？{vbCrLf}安装程序会自动关闭缘启并替换程序文件，设置与存档不会丢失。",
                                "检查更新", "立即安装", "稍后") = 1 Then
                        StartProcess(SavePath)
                        FrmMain.Close()
                    Else
                        Hint($"安装包已保存到：{SavePath}", HintType.Blue)
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Warn(ex, "下载安装包失败")
                RunInUi(
                Sub()
                    Hint("下载安装包失败，已为你打开下载页面。", HintType.Red)
                    OpenWebsite(RepoUrl & "/releases/latest")
                End Sub)
            End Try
        End Sub, "Download Installer", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>启动时按设置自动检测更新（默认关闭）。</summary>
    Public Sub AutoCheckLauncherUpdate()
        Try
            If Not Settings.Get(Of Boolean)("UiAutoCheckUpdate") Then Return
            RunInNewThread(Sub()
                               Thread.Sleep(8000) '错开启动高峰
                               CheckLauncherUpdate(True)
                           End Sub, "Auto Update Check", ThreadPriority.BelowNormal)
        Catch ex As Exception
            Logger.Warn(ex, "自动更新检测启动失败")
        End Try
    End Sub

#End Region

#Region "陶瓦联机模块更新"

    ''' <summary>
    ''' 检查陶瓦联机模块是否有新版本（对比本地模块与 GitHub 最新 Release）。
    ''' </summary>
    Public Sub CheckTerracottaUpdate()
        Try
            Dim LocalVersion As String = GetLocalTerracottaVersion()
            Hint("正在检查联机模块更新……", HintType.Blue, False)
            Dim Latest As String = ModTerracotta.GetLatestVersion()
            If String.IsNullOrWhiteSpace(Latest) Then
                MyMsgBox("无法获取联机模块的最新版本，请检查网络后重试。", "联机模块更新", IsWarn:=True)
                Return
            End If
            Dim Message As String = $"本地联机模块：{If(String.IsNullOrWhiteSpace(LocalVersion), "未知", LocalVersion)}{vbCrLf}" &
                $"最新版本：{Latest}"
            If String.IsNullOrWhiteSpace(LocalVersion) Then
                Message &= vbCrLf & vbCrLf & "本地版本未知（可能是手动放置的模块）。"
            ElseIf CompareVersion(LocalVersion, Latest) >= 0 Then
                MyMsgBox(Message & vbCrLf & vbCrLf & "已是最新版本。", "联机模块更新")
                Return
            End If
            If MyMsgBox(Message & vbCrLf & vbCrLf & "是否打开下载页面？", "联机模块更新", "打开", "取消") = 1 Then
                OpenWebsite("https://github.com/burningtnt/Terracotta/releases/latest")
            End If
        Catch ex As Exception
            Logger.Warn(ex, "检查联机模块更新失败")
            MyMsgBox("检查失败：" & vbCrLf & ex.Message, "联机模块更新", IsWarn:=True)
        End Try
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

    ''' <summary>比较形如 0.4.2 的版本号，返回 -1/0/1。</summary>
    Private Function CompareVersion(A As String, B As String) As Integer
        Try
            Dim PA = A.Split("."c), PB = B.Split("."c)
            For i = 0 To Math.Max(PA.Length, PB.Length) - 1
                Dim VA As Integer = If(i < PA.Length AndAlso Integer.TryParse(PA(i), VA), 0, 0)
                Dim VB As Integer = 0
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
        'tar.exe（解压联机模块用）
        Try
            Result.Add(New KeyValuePair(Of String, String)("系统 tar.exe",
                If(FileUtils.Exists(Environment.SystemDirectory & "\tar.exe"), "OK", "缺失（无法自动解压联机模块）")))
        Catch
        End Try
        Return Result
    End Function

    ''' <summary>显示环境体检结果。</summary>
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
                    Item.Bytes = ModArchive.GetFolderSize(Folder, Nothing)
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

    ''' <summary>列出长期未游玩、适合归档的版本，支持多选后一键归档。</summary>
    Private Sub ShowArchiveCandidates(Items As List(Of SpaceItem))
        Try
            Dim Sessions = PlayLogModule.LoadSessions()
            Dim LastPlay As New Dictionary(Of String, Date)
            For Each S In Sessions
                If Not LastPlay.ContainsKey(S.Name) OrElse LastPlay(S.Name) < S.EndTime Then
                    LastPlay(S.Name) = S.EndTime
                End If
            Next
            Dim Candidates = Items.Where(Function(I) Not I.IsArchived).
                Select(Function(I) New With {
                    .Item = I,
                    .Last = If(LastPlay.ContainsKey(I.Name), LastPlay(I.Name), Date.MinValue)
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
