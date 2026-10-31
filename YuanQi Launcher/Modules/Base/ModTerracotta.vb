Imports System.Net.Http

''' <summary>
''' 陶瓦联机（Terracotta）的本地客户端。
''' 负责定位（或下载）陶瓦可执行文件、启动与停止进程、调用其本地 HTTP API 并解析房间状态。
''' <para/>接口文档：https://burningtnt.github.io/Terracotta/http-api/
''' </summary>
Public Module ModTerracotta

#Region "路径与常量"

    ''' <summary>
    ''' 无法联网获取最新版本时使用的陶瓦版本号。
    ''' </summary>
    Public Const FallbackVersion As String = "0.4.2"

    ''' <summary>
    ''' 陶瓦联机的数据目录，以 \ 结尾。
    ''' </summary>
    Public ReadOnly DataPath As String = Paths.AppDataThenName & "Terracotta\"
    ''' <summary>
    ''' 数据目录中的陶瓦可执行文件路径。
    ''' </summary>
    Public ReadOnly CorePath As String = DataPath & "terracotta.exe"
    ''' <summary>
    ''' 随启动器一同分发的陶瓦文件夹（位于启动器目录下），以 \ 结尾。
    ''' </summary>
    Public ReadOnly BundledDataPath As String = Paths.Base & "Terracotta\"
    ''' <summary>
    ''' 随启动器分发的陶瓦可执行文件路径。
    ''' </summary>
    Public ReadOnly BundledCorePath As String = BundledDataPath & "terracotta.exe"
    ''' <summary>
    ''' 当前启动器实例的进程 ID，用于隔离多开时的联机模块。
    ''' </summary>
    Private ReadOnly InstanceId As Integer = Process.GetCurrentProcess().Id
    ''' <summary>
    ''' 端口交换文件路径。按进程区分，避免多开时互相删除对方的握手文件。
    ''' </summary>
    Public ReadOnly Property PortFilePath As String
        Get
            Return DataPath & $"hmcl-port-{InstanceId}.json"
        End Get
    End Property
    ''' <summary>
    ''' 当前实例专属的临时目录。
    ''' 陶瓦把锁文件与内置 EasyTier 解压到 %TEMP%\terracotta，若多个实例共用会导致后启动的实例
    ''' 直接「附着」到前一个实例上（共用同一个房间）。这里为每个实例单独指定 TEMP，使其彼此独立。
    ''' </summary>
    Public ReadOnly Property InstanceTempPath As String
        Get
            Return DataPath & $"Instance-{InstanceId}\"
        End Get
    End Property

    ''' <summary>
    ''' 陶瓦的本地 HTTP 端口。0 表示尚未启动。
    ''' </summary>
    Public Property ApiPort As Integer = 0
    ''' <summary>
    ''' 陶瓦的本地接口地址前缀。
    ''' </summary>
    Public ReadOnly Property ApiBaseUrl As String
        Get
            Return $"http://127.0.0.1:{ApiPort}"
        End Get
    End Property

    ''' <summary>
    ''' 由 --hmcl 参数拉起的引导进程。它会写出端口文件后自行退出，真正的工作进程由其派生。
    ''' </summary>
    Private BootstrapProcess As Process = Nothing

    ''' <summary>
    ''' 清理上次运行遗留的实例目录与孤儿联机模块进程。
    ''' 启动器被强制结束（任务管理器、崩溃）时来不及清理，多开时更容易残留，因此在启动时兜底处理。
    ''' </summary>
    Public Sub CleanupStaleInstances()
        Try
            If Not DirectoryUtils.Exists(DataPath) Then Return
            For Each Folder As String In DirectoryUtils.EnumerateDirectories(DataPath, searchPattern:="Instance-*").ToList()
                Dim FolderName As String = IO.Path.GetFileName(Folder.TrimEnd("\"c))
                Dim OwnerPid As Integer = 0
                If Not Integer.TryParse(FolderName.Replace("Instance-", ""), OwnerPid) Then Continue For
                If OwnerPid = InstanceId Then Continue For
                '所属启动器仍在运行，说明是正常的其他实例，保留
                Try
                    Process.GetProcessById(OwnerPid)
                    Continue For
                Catch
                End Try
                '结束该实例遗留的联机模块进程
                Try
                    Dim PortFileKey As String = $"hmcl-port-{OwnerPid}.json"
                    Using Searcher As New Management.ManagementObjectSearcher(
                        $"SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'terracotta.exe'")
                        For Each Item As Management.ManagementObject In Searcher.Get()
                            Dim Cmd As String = If(Item("CommandLine"), "")
                            If Not Cmd.Contains(PortFileKey) Then Continue For
                            Dim TargetPid As Integer = CInt(Item("ProcessId"))
                            Logger.Info($"清理遗留的联机模块进程，PID：{TargetPid}")
                            Try
                                Process.GetProcessById(TargetPid).Kill()
                            Catch ex As Exception
                                Logger.Warn(ex, $"结束遗留联机模块失败（PID {TargetPid}）")
                            End Try
                        Next
                    End Using
                Catch ex As Exception
                    Logger.Warn(ex, "查询遗留联机模块进程失败")
                End Try
                Try
                    DirectoryUtils.Delete(Folder)
                Catch ex As Exception
                    Logger.Warn(ex, $"清理遗留实例目录失败：{Folder}")
                End Try
            Next
        Catch ex As Exception
            Logger.Warn(ex, "清理遗留联机实例失败")
        End Try
    End Sub

#End Region

#Region "进程"

    ''' <summary>
    ''' 联机模块是否已经就绪（随启动器分发，或此前已经下载过）。
    ''' </summary>
    Public Function IsCorePresent() As Boolean
        Return FileUtils.Exists(BundledCorePath) OrElse FileUtils.Exists(CorePath)
    End Function

    ''' <summary>
    ''' 把随启动器分发的联机模块复制到数据目录，并返回实际使用的可执行文件路径。
    ''' 若启动器没有附带模块，则直接返回数据目录中的路径（可能并不存在）。
    ''' </summary>
    Public Function EnsureCoreCopy() As String
        If Not FileUtils.Exists(BundledCorePath) Then Return CorePath
        Dim NeedCopy As Boolean = True
        If FileUtils.Exists(CorePath) Then
            NeedCopy = FileUtils.GetInfo(CorePath).Length <> FileUtils.GetInfo(BundledCorePath).Length
        End If
        If Not NeedCopy Then Return CorePath
        Logger.Info($"正在把内置的联机模块复制到数据目录：{BundledDataPath} → {DataPath}")
        DirectoryUtils.Create(DataPath)
        FileUtils.Copy(BundledCorePath, CorePath)
        Dim BundledDll As String = BundledDataPath & "VCRUNTIME140.dll"
        If FileUtils.Exists(BundledDll) Then FileUtils.Copy(BundledDll, DataPath & "VCRUNTIME140.dll")
        Return CorePath
    End Function

    ''' <summary>
    ''' 启动联机模块，并等待其本地接口就绪。
    ''' 若模块已在运行则直接复用，不重复启动。
    ''' 若模块缺失或无法启动则抛出异常。
    ''' </summary>
    Public Sub StartCore()
        '已有可用实例则直接复用（关闭房间后模块会留在待机状态）
        If ApiPort > 0 Then
            Try
                RequestPort(ApiPort, "/meta", 1500)
                '确保回到待机，避免沿用上一次的房间状态
                Try
                    If Not RequestPort(ApiPort, "/state", 1500).Contains("waiting") Then RequestPort(ApiPort, "/state/ide", 3000)
                Catch ex As Exception
                    Logger.Warn(ex, "重置联机模块状态失败")
                End Try
                Logger.Info($"复用已在运行的联机模块，端口 {ApiPort}")
                Return
            Catch ex As Exception
                Logger.Warn(ex, "联机模块无响应，将重新启动")
                ApiPort = 0
            End Try
        End If
        Dim Exe As String = EnsureCoreCopy()
        If Not FileUtils.Exists(Exe) Then
            Throw New Exception($"$找不到联机模块，请重新下载 缘启 或手动放置联机模块。{vbCrLf}预期位置：{Exe}")
        End If
        DirectoryUtils.Create(DataPath)
        '清理上一次的端口文件
        Dim PortFile As String = PortFilePath
        FileUtils.Delete(PortFile)
        '每个实例使用独立的临时目录，避免多开时共用同一个 EasyTier 与房间
        DirectoryUtils.Create(InstanceTempPath)
        '启动
        Dim Info As New ProcessStartInfo With {
            .FileName = Exe,
            .Arguments = $"--hmcl ""{PortFile}""",
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WorkingDirectory = DataPath
        }
        Info.EnvironmentVariables("TEMP") = InstanceTempPath
        Info.EnvironmentVariables("TMP") = InstanceTempPath
        Logger.Info($"正在启动联机模块：{Info.FileName} {Info.Arguments}")
        BootstrapProcess = Process.Start(Info)
        '等待端口文件出现
        Dim StartTime As Date = Now
        Do
            If FileUtils.Exists(PortFile) Then Exit Do
            If (Now - StartTime).TotalSeconds > 20 Then
                Throw New Exception($"$联机模块启动超时。{vbCrLf}它可能被杀毒软件拦截，请把 缘启 与联机模块加入白名单后重试。")
            End If
            Thread.Sleep(100)
        Loop
        '读取端口
        Dim Raw As String = Nothing
        For i = 1 To 20
            Try
                Raw = FileUtils.ReadAsString(PortFile, Encoding.UTF8)
                If Not String.IsNullOrWhiteSpace(Raw) Then Exit For
            Catch ex As Exception
                Logger.Warn(ex, "读取联机模块端口文件失败")
            End Try
            Thread.Sleep(50)
        Next
        FileUtils.Delete(PortFile)
        Dim PortValue As Integer = 0
        Try
            Dim PortToken = CType(Raw.DeserializeJson(), JObject)?("port")
            If PortToken IsNot Nothing Then Integer.TryParse(PortToken.ToString, PortValue)
        Catch ex As Exception
            Logger.Warn(ex, $"无法解析联机模块的端口信息：{Raw}")
        End Try
        If PortValue <= 0 Then Throw New Exception($"$无法获取联机模块的端口。{vbCrLf}请稍后重试，或重启电脑后重试。")
        ApiPort = PortValue
        Logger.Info($"联机模块已启动，端口 {ApiPort}")
        '等待接口可用
        For i = 1 To 50
            Try
                ApiGet("/meta", 1000)
                Return
            Catch ex As Exception
                If i = 50 Then Throw
                Thread.Sleep(100)
            End Try
        Next
    End Sub

    ''' <summary>
    ''' 只把房间状态重置为待机（关闭房间 / 取消加载时使用），联机模块保持运行以便下次复用。
    ''' 不会阻塞调用线程，失败也不会抛出异常。
    ''' </summary>
    Public Sub ResetRoomAsync()
        Dim Port As Integer = ApiPort
        If Port <= 0 Then Return
        RunInNewThread(
        Sub()
            Try
                RequestPort(Port, "/state/ide", 3000)
                Logger.Info("已把联机模块重置为待机")
            Catch ex As Exception
                Logger.Warn(ex, "重置联机模块状态失败")
            End Try
        End Sub, "Terracotta Reset")
    End Sub

    ''' <summary>
    ''' 彻底停止联机模块。即使失败也不会抛出异常。
    ''' 先回到待机让内嵌的 EasyTier 正常断开，再请求进程退出，
    ''' 避免 EasyTier 在仍有连接时被强行连带结束而崩溃。
    ''' 整个过程在后台线程执行，不会阻塞 UI。
    ''' </summary>
    Public Sub StopCore(Optional Graceful As Boolean = True)
        Dim ExitPort As Integer = ApiPort
        ApiPort = 0 '先清零，之后的本地接口请求不再等待这一端口
        If ExitPort > 0 Then
            RunInNewThread(
            Sub()
                If Graceful Then
                    '第一步：回到待机，让 EasyTier 收尾
                    Try
                        RequestPort(ExitPort, "/state/ide", 3000)
                    Catch ex As Exception
                        Logger.Info($"联机模块已退出（{ex.Message}）")
                        Return
                    End Try
                    For i = 1 To 10
                        Try
                            If RequestPort(ExitPort, "/state", 1000).Contains("waiting") Then Exit For
                        Catch ex As Exception
                            Return '进程已经没了
                        End Try
                        Thread.Sleep(100)
                    Next
                End If
                '第二步：请求进程退出
                Try
                    RequestPort(ExitPort, "/panic?peaceful=true", 3000)
                    Logger.Info("已请求联机模块退出")
                Catch ex As Exception
                    '陶瓦会先退出再响应，连接被重置属于正常现象
                    Logger.Info($"联机模块已退出（{ex.Message}）")
                End Try
                '清理本实例专属的临时目录（内含内置 EasyTier 与锁文件）
                Thread.Sleep(600)
                Try
                    DirectoryUtils.Delete(InstanceTempPath)
                Catch ex As Exception
                    Logger.Warn(ex, "清理联机模块临时目录失败")
                End Try
            End Sub, "Terracotta Exit")
        End If
        If BootstrapProcess IsNot Nothing AndAlso Not BootstrapProcess.HasExited Then
            Try
                BootstrapProcess.Kill()
                BootstrapProcess.Close()
            Catch ex As Exception
                Logger.Warn(ex, "结束联机模块引导进程失败")
            End Try
        End If
        BootstrapProcess = Nothing
    End Sub

#End Region

#Region "本地接口"

    Private HttpClientInstance As HttpClient = Nothing
    Private ReadOnly ClientLock As New Object

    Private Function GetHttpClient() As HttpClient
        SyncLock ClientLock
            If HttpClientInstance Is Nothing Then
                '本地回环请求，不能走系统代理
                Dim Handler As New HttpClientHandler With {.UseProxy = False}
                HttpClientInstance = New HttpClient(Handler) With {
                    .Timeout = System.Threading.Timeout.InfiniteTimeSpan
                }
                HttpClientInstance.DefaultRequestHeaders.Add("Accept", "application/json")
            End If
            Return HttpClientInstance
        End SyncLock
    End Function

    ''' <summary>
    ''' 请求联机模块的本地接口，并返回响应正文。
    ''' 请求失败或返回非 200 状态码时抛出异常。
    ''' </summary>
    Public Function ApiGet(Path As String, Optional Timeout As Integer = 5000) As String
        If ApiPort <= 0 Then Throw New Exception("联机模块尚未启动")
        Return RequestPort(ApiPort, Path, Timeout)
    End Function

    ''' <summary>
    ''' 请求指定端口的联机模块本地接口，并返回响应正文。
    ''' </summary>
    Private Function RequestPort(Port As Integer, Path As String, Optional Timeout As Integer = 5000) As String
        Dim Url As String = $"http://127.0.0.1:{Port}{Path}"
        Using Cts As New CancellationTokenSource(Timeout)
            Dim RequestTask = GetHttpClient().GetAsync(Url, Cts.Token)
            Try
                RequestTask.Wait()
            Catch ex As AggregateException
                Dim Inner As Exception = ex.InnerException
                If TypeOf Inner Is TaskCanceledException OrElse TypeOf Inner Is OperationCanceledException Then
                    Throw New Exception($"请求联机模块接口超时（{Path.BeforeFirst("?")}）", Inner)
                End If
                Throw New Exception($"无法连接联机模块（{Path.BeforeFirst("?")}）：{Inner?.Message}", Inner)
            End Try
            Dim Response = RequestTask.Result
            Dim Body As String = Response.Content.ReadAsStringAsync().Result
            If Not Response.IsSuccessStatusCode Then
                Throw New Exception($"联机模块接口返回 {CInt(Response.StatusCode)}（{Path.BeforeFirst("?")}）：{Body}")
            End If
            Return Body
        End Using
    End Function

    ''' <summary>
    ''' 获取当前房间状态。
    ''' </summary>
    Public Function GetRoomState(Optional Timeout As Integer = 5000) As TerracottaState
        Return New TerracottaState(ApiGet("/state", Timeout).DeserializeJson())
    End Function

    ''' <summary>
    ''' 重置为待机状态，用于「返回主页」等操作。
    ''' </summary>
    Public Function ResetRoomState() As Boolean
        Try
            ApiGet("/state/ide", 3000)
            Return True
        Catch ex As Exception
            Logger.Warn(ex, "重置联机状态失败")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 以房主身份创建房间（由联机模块自动扫描本机 Minecraft 端口）。
    ''' </summary>
    Public Function CreateRoom(Optional PlayerName As String = Nothing, Optional PublicNodes As IEnumerable(Of String) = Nothing) As Boolean
        ApiGet("/state/scanning" & BuildQuery(PlayerName, PublicNodes), 10000)
        Return True
    End Function

    ''' <summary>
    ''' 以加入方身份加入房间。
    ''' </summary>
    Public Function JoinRoom(Room As String, Optional PlayerName As String = Nothing, Optional PublicNodes As IEnumerable(Of String) = Nothing) As Boolean
        Dim Query As String = BuildQuery(PlayerName, PublicNodes)
        Dim RoomQuery As String = "room=" & Uri.EscapeDataString(Room)
        If String.IsNullOrEmpty(Query) Then Query = "?" & RoomQuery Else Query = "?" & RoomQuery & "&" & Query.TrimStart("?"c)
        ApiGet("/state/guesting" & Query, 10000)
        Return True
    End Function

    Private Function BuildQuery(PlayerName As String, PublicNodes As IEnumerable(Of String)) As String
        Dim Parts As New List(Of String)
        If Not String.IsNullOrWhiteSpace(PlayerName) Then Parts.Add("player=" & Uri.EscapeDataString(PlayerName.Trim))
        If PublicNodes IsNot Nothing Then
            For Each Node As String In PublicNodes
                If Not String.IsNullOrWhiteSpace(Node) Then Parts.Add("public_nodes=" & Uri.EscapeDataString(Node.Trim))
            Next
        End If
        If Not Parts.Any Then Return ""
        Return "?" & Parts.Join("&")
    End Function

    ''' <summary>
    ''' 把联机模块的异常类型转换为给玩家看的标题。
    ''' </summary>
    Public Function GetExceptionBrief(Type As Integer) As String
        Select Case Type
            Case 0 : Return "无法连接房主"
            Case 1 : Return "与房主的连接已中断"
            Case 2 : Return "联机组件崩溃"
            Case 3 : Return "房主的联机组件崩溃"
            Case 4 : Return "房主已关闭房间"
            Case 5 : Return "联机协议错误"
            Case Else : Return "联机出现未知错误"
        End Select
    End Function

    ''' <summary>
    ''' 把联机模块的异常类型转换为给玩家看的详细说明。
    ''' </summary>
    Public Function GetExceptionDetail(Type As Integer) As String
        Select Case Type
            Case 0 : Return $"房间已关闭，或者网络不稳定。{vbCrLf}请确认房主仍在房间中，然后重试。"
            Case 1 : Return $"房主可能已离开房间，请重新加入。"
            Case 2 : Return $"请重试。若反复出现，请向开发者反馈。"
            Case 3 : Return $"请让房主重试。若反复出现，请向开发者反馈。"
            Case 4 : Return $"房主退出存档后房间会自动关闭。"
            Case 5 : Return $"请重试。若反复出现，请向开发者反馈。"
            Case Else : Return $"请重试。若反复出现，请向开发者反馈。"
        End Select
    End Function

#End Region

#Region "状态模型"

    ''' <summary>
    ''' 房间中的一名成员。
    ''' </summary>
    Public Class TerracottaProfile
        ''' <summary>
        ''' 设备唯一标识。
        ''' </summary>
        Public MachineID As String = Nothing
        ''' <summary>
        ''' 玩家显示名。
        ''' </summary>
        Public Name As String = Nothing
        ''' <summary>
        ''' 来源标识。
        ''' </summary>
        Public Vendor As String = Nothing
        ''' <summary>
        ''' HOST（房主）/ LOCAL（本机玩家）/ GUEST（其他加入者）。
        ''' </summary>
        Public Kind As String = Nothing

        Public Sub New()
        End Sub
        Public Sub New(Token As JToken)
            If Token Is Nothing Then Return
            If Token("machine_id") IsNot Nothing Then MachineID = Token("machine_id").ToString
            If Token("name") IsNot Nothing Then Name = Token("name").ToString
            If Token("vendor") IsNot Nothing Then Vendor = Token("vendor").ToString
            If Token("kind") IsNot Nothing Then Kind = Token("kind").ToString
        End Sub
    End Class

    ''' <summary>
    ''' GET /state 的响应。
    ''' </summary>
    Public Class TerracottaState
        ''' <summary>
        ''' waiting / host-scanning / host-starting / host-ok / guest-connecting / guest-starting / guest-ok / exception。
        ''' </summary>
        Public State As String = "waiting"
        ''' <summary>
        ''' 状态序号，每次状态变化都会单调递增。
        ''' </summary>
        Public Index As Integer = -1
        ''' <summary>
        ''' 房间邀请码，形如 U/XXXX-XXXX-XXXX-XXXX。
        ''' </summary>
        Public Room As String = Nothing
        ''' <summary>
        ''' 加入成功后可直接连接的地址（多人游戏备用直连地址）。
        ''' </summary>
        Public Url As String = Nothing
        ''' <summary>
        ''' 加入时的 NAT 连接难度：UNKNOWN / EASIEST / SIMPLE / MEDIUM / TOUGH。
        ''' </summary>
        Public Difficulty As String = Nothing
        ''' <summary>
        ''' 异常类型，仅在 State 为 exception 时有效。
        ''' </summary>
        Public ExceptionType As Integer = -1
        ''' <summary>
        ''' 房间成员列表。
        ''' </summary>
        Public Profiles As New List(Of TerracottaProfile)

        Public Sub New()
        End Sub
        Public Sub New(Token As JToken)
            If Token Is Nothing Then Return
            If Token("state") IsNot Nothing Then State = Token("state").ToString
            If Token("index") IsNot Nothing Then Integer.TryParse(Token("index").ToString, Index)
            If Token("room") IsNot Nothing Then Room = Token("room").ToString
            If Token("url") IsNot Nothing Then Url = Token("url").ToString
            If Token("difficulty") IsNot Nothing Then Difficulty = Token("difficulty").ToString
            If Token("type") IsNot Nothing Then Integer.TryParse(Token("type").ToString, ExceptionType)
            Dim ProfileList = Token("profiles")
            If ProfileList IsNot Nothing AndAlso TypeOf ProfileList Is JArray Then
                For Each Item As JToken In CType(ProfileList, JArray)
                    Profiles.Add(New TerracottaProfile(Item))
                Next
            End If
        End Sub

        ''' <summary>
        ''' 房间内人数，至少为 1。
        ''' </summary>
        Public ReadOnly Property PeopleCount As Integer
            Get
                Return Math.Max(Profiles.Count, 1)
            End Get
        End Property

        ''' <summary>
        ''' 成员显示名，以「、」分隔。
        ''' </summary>
        Public ReadOnly Property PlayerNames As String
            Get
                Return Profiles.Select(Function(p) p.Name).
                    Where(Function(n) Not String.IsNullOrWhiteSpace(n)).Join("、")
            End Get
        End Property

        Public ReadOnly Property IsException As Boolean
            Get
                Return State = "exception"
            End Get
        End Property

        ''' <summary>
        ''' NAT 连接难度的中文说明。
        ''' </summary>
        Public ReadOnly Property DifficultyText As String
            Get
                Select Case Difficulty
                    Case "EASIEST" : Return "非常容易"
                    Case "SIMPLE" : Return "较容易"
                    Case "MEDIUM" : Return "一般"
                    Case "TOUGH" : Return "较困难"
                    Case Else : Return "未知"
                End Select
            End Get
        End Property
    End Class

#End Region

#Region "下载与解压"

    ''' <summary>
    ''' 根据 CPU 架构获取应当下载的联机模块地址（按优先级排列）。
    ''' </summary>
    Public Function GetDownloadUrls(Architecture As String) As List(Of String)
        Dim Version As String = GetLatestVersion()
        Dim FileName As String = $"terracotta-{Version}-windows-{Architecture}-pkg.tar.gz"
        Return New List(Of String) From {
            $"https://github.com/burningtnt/Terracotta/releases/download/v{Version}/{FileName}",
            $"https://gitee.com/burningtnt/Terracotta/releases/download/v{Version}/{FileName}",
            $"https://cnb.cool/HMCL-Terracotta/Terracotta/-/releases/download/v{Version}/{FileName}"
        }
    End Function

    ''' <summary>
    ''' 获取联机模块的最新版本号。失败时使用内置版本号。
    ''' </summary>
    Public Function GetLatestVersion() As String
        Try
            Dim Raw As String = NetRequestByClient("https://api.github.com/repos/burningtnt/Terracotta/releases/latest",
                RequireJson:=True, Timeout:=8000)
            Dim Tag = CType(Raw.DeserializeJson(), JObject)?("tag_name")
            If Tag IsNot Nothing Then
                Dim Version As String = Tag.ToString.Trim.TrimStart("v"c)
                If Not String.IsNullOrWhiteSpace(Version) Then Return Version
            End If
        Catch ex As Exception
            Logger.Warn(ex, "获取联机模块版本失败，将使用内置版本号")
        End Try
        Return FallbackVersion
    End Function

    ''' <summary>
    ''' 解压下载得到的联机模块压缩包（.tar.gz），返回可执行文件路径。
    ''' </summary>
    Public Function ExtractPackage(FilePath As String) As String
        Dim ExtractPath As String = DataPath & "Extract\"
        DirectoryUtils.Create(ExtractPath)
        Dim TarPath As String = Environment.SystemDirectory & "\tar.exe"
        If Not FileUtils.Exists(TarPath) Then
            Throw New Exception($"$系统缺少 tar.exe，无法解压联机模块。{vbCrLf}联机模块需要 Windows 10 及以上版本。")
        End If
        Dim Info As New ProcessStartInfo With {
            .FileName = TarPath,
            .Arguments = $"-xzf ""{FilePath}"" -C ""{ExtractPath}""",
            .UseShellExecute = False,
            .CreateNoWindow = True
        }
        Logger.Info($"正在解压联机模块：{Info.Arguments}")
        Dim TarProcess = Process.Start(Info)
        TarProcess.WaitForExit(30000)
        '整理文件
        Dim Files As String() = Directory.GetFiles(ExtractPath)
        Dim ExeFile As String = Files.FirstOrDefault(Function(f) f.ToLower.EndsWith(".exe"))
        If ExeFile Is Nothing Then Throw New Exception("联机模块压缩包内容有误，请稍后重试！")
        FileUtils.Delete(CorePath)
        FileUtils.Move(ExeFile, CorePath)
        For Each Dll As String In Files.Where(Function(f) f.ToLower.EndsWith(".dll"))
            FileUtils.Move(Dll, DataPath & IO.Path.GetFileName(Dll))
        Next
        DirectoryUtils.Delete(ExtractPath)
        FileUtils.Delete(FilePath)
        Return CorePath
    End Function

#End Region

End Module
