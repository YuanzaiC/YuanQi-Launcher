Imports System.IO
Imports System.Runtime.InteropServices

''' <summary>
''' 从其他启动器导入游戏文件：扫描常见位置、复制（带进度与取消）、登记到文件夹列表。
''' 删除原文件夹时只走回收站，失败就报失败，绝不静默改成永久删除。
''' </summary>
Public Module GameImport

    ''' <summary>扫描到的一个候选游戏文件夹。</summary>
    Public Class GameFolder
        ''' <summary>来源名称，例如「官方启动器」「PCL」。</summary>
        Public Source As String = ""
        ''' <summary>文件夹完整路径，带结尾反斜杠。</summary>
        Public Path As String = ""
        ''' <summary>versions 下有多少个版本。</summary>
        Public VersionCount As Integer
    End Class

    ''' <summary>本启动器自己的游戏目录：扫描时不把它当成「其他启动器」。</summary>
    Private ReadOnly Property OwnFolder As String
        Get
            Return PathUtils.AddSlashSuffix(PathUtils.ForCompare(Paths.Base & ".minecraft"))
        End Get
    End Property

    ''' <summary>
    ''' 扫描常见位置，返回看起来可用的游戏文件夹（按版本数排序，最多 6 个）。
    ''' 只做目录存在性检查，不递归统计体积，保证在引导弹窗里秒回。
    ''' </summary>
    Public Function Detect() As List(Of GameFolder)
        Dim Result As New List(Of GameFolder)
        Dim Seen As New List(Of String)
        Try
            Dim AppData As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            Dim UserProfile As String = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            AddCandidate(Result, Seen, IO.Path.Combine(AppData, ".minecraft"), "官方启动器")
            AddCandidate(Result, Seen, IO.Path.Combine(UserProfile, ".minecraft"), "其他启动器")
            AddCandidate(Result, Seen, IO.Path.Combine(AppData, "BakaXL", ".minecraft"), "BakaXL")
            '各磁盘根目录，以及根目录下一层（PCL / HMCL 常被放在 D:\PCL 这类位置）
            For Each Drive As DriveInfo In DriveInfo.GetDrives()
                Try
                    If Drive.DriveType <> DriveType.Fixed OrElse Not Drive.IsReady Then Continue For
                    Dim Root As String = Drive.RootDirectory.FullName
                    AddCandidate(Result, Seen, IO.Path.Combine(Root, ".minecraft"), "其他启动器")
                    Dim Dirs As String() = Nothing
                    Try
                        Dirs = IO.Directory.GetDirectories(Root)
                    Catch ex As Exception
                    End Try
                    If Dirs Is Nothing Then Continue For
                    For Each Dir As String In Dirs
                        Try
                            AddCandidate(Result, Seen, IO.Path.Combine(Dir, ".minecraft"), IO.Path.GetFileName(Dir))
                        Catch ex As Exception
                        End Try
                    Next
                Catch ex As Exception
                End Try
            Next
        Catch ex As Exception
            Logger.Warn(ex, "扫描其他启动器的游戏文件夹失败")
        End Try
        Return Result.OrderByDescending(Function(F) F.VersionCount).Take(6).ToList()
    End Function

    Private Sub AddCandidate(List As List(Of GameFolder), Seen As List(Of String), Folder As String, Source As String)
        Try
            If String.IsNullOrWhiteSpace(Folder) Then Return
            Dim Full As String = PathUtils.AddSlashSuffix(PathUtils.ForCompare(Folder))
            If PathUtils.IsSame(Full, OwnFolder) Then Return
            For Each Item As String In Seen
                If PathUtils.IsSame(Item, Full) Then Return
            Next
            Dim Versions As String = IO.Path.Combine(Full, "versions")
            If Not DirectoryUtils.Exists(Versions) Then Return
            Dim Count As Integer = IO.Directory.GetDirectories(Versions).Length
            If Count <= 0 Then Return
            Seen.Add(Full)
            List.Add(New GameFolder With {.Source = Source, .Path = Full, .VersionCount = Count})
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>
    ''' 把游戏文件夹复制到 Target：除 versions 外的内容（libraries / assets / 存档等）整份复制，
    ''' versions 下只复制 Versions 里列出的版本。每 20 个文件回报一次进度；Cancel 返回 True 时中止。
    ''' 返回 True 表示复制完整，False 表示被取消或枚举失败。
    ''' </summary>
    Public Function CopyFolder(Source As String, Target As String, Versions As List(Of String),
                               Progress As Action(Of Double, String), Cancel As Func(Of Boolean)) As Boolean
        Dim Files As New List(Of String)
        Try
            '根目录文件
            Files.AddRange(IO.Directory.GetFiles(Source))
            '除 versions 以外的子目录整份复制
            For Each Dir As String In IO.Directory.GetDirectories(Source)
                If String.Equals(IO.Path.GetFileName(Dir), "versions", StringComparison.OrdinalIgnoreCase) Then Continue For
                Try
                    Files.AddRange(IO.Directory.GetFiles(Dir, "*", SearchOption.AllDirectories))
                Catch ex As Exception
                    Logger.Warn(ex, $"枚举目录失败：{Dir}")
                End Try
            Next
            'versions 只挑选中的
            Dim VersionsDir As String = IO.Path.Combine(Source, "versions")
            If Versions IsNot Nothing Then
                For Each Name As String In Versions
                    Dim VersionDir As String = IO.Path.Combine(VersionsDir, Name)
                    If Not DirectoryUtils.Exists(VersionDir) Then Continue For
                    Try
                        Files.AddRange(IO.Directory.GetFiles(VersionDir, "*", SearchOption.AllDirectories))
                    Catch ex As Exception
                        Logger.Warn(ex, $"枚举版本目录失败：{VersionDir}")
                    End Try
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "枚举待复制的游戏文件失败")
            Return False
        End Try
        Dim Total As Integer = Math.Max(Files.Count, 1)
        DirectoryUtils.Create(Target)
        Dim Done As Integer = 0
        For Each FilePath As String In Files
            If Cancel IsNot Nothing AndAlso Cancel() Then Return False
            Try
                Dim Rel As String = FilePath.Substring(Source.Length).TrimStart("\"c)
                Dim Dest As String = IO.Path.Combine(Target, Rel)
                DirectoryUtils.Create(IO.Path.GetDirectoryName(Dest))
                IO.File.Copy(FilePath, Dest, True)
            Catch ex As Exception
                Logger.Warn(ex, $"复制游戏文件失败：{FilePath}")
            End Try
            Done += 1
            If Done Mod 20 = 0 OrElse Done = Total Then
                If Progress IsNot Nothing Then Progress(Done / Total, $"正在复制（{Done} / {Total}）……")
            End If
        Next
        Return True
    End Function

    ''' <summary>把文件夹删除到回收站。失败返回 False，调用方据此提示，不做永久删除。</summary>
    Public Function DeleteToRecycleBin(Folder As String) As Boolean
        Try
            If Not DirectoryUtils.Exists(Folder) Then Return True
            'FOF_ALLOWUNDO 走回收站；不设 FOF_NOCONFIRMATION 而是 FOF_WANTNUKEWARNING，
            '这样万一这个位置没法回收（例如网络盘），系统会先问一句，而不是直接永久删除
            Dim Op As New SHFILEOPSTRUCT With {
                .wFunc = FO_DELETE,
                .pFrom = PathUtils.ForApi(Folder.TrimEnd("\"c)) & vbNullChar & vbNullChar,
                .fFlags = FOF_ALLOWUNDO Or FOF_SILENT Or FOF_NOERRORUI Or FOF_WANTNUKEWARNING}
            Dim Code As Integer = SHFileOperation(Op)
            If Code <> 0 Then
                Logger.Warn($"删除到回收站失败（错误码 {Code}）：{Folder}")
                Return False
            End If
            If Op.fAnyOperationsAborted Then
                Logger.Info($"删除到回收站被取消：{Folder}")
                Return False
            End If
            Return Not DirectoryUtils.Exists(Folder)
        Catch ex As Exception
            Logger.Warn(ex, "删除到回收站失败")
            Return False
        End Try
    End Function

    '---- 回收站删除用的 Win32 结构 ----
    Private Const FO_DELETE As UInteger = 3
    Private Const FOF_SILENT As UShort = &H4
    Private Const FOF_NOERRORUI As UShort = &H400
    Private Const FOF_ALLOWUNDO As UShort = &H40
    Private Const FOF_WANTNUKEWARNING As UShort = &H4000

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure SHFILEOPSTRUCT
        Public hwnd As IntPtr
        Public wFunc As UInteger
        Public pFrom As String
        Public pTo As String
        Public fFlags As UShort
        Public fAnyOperationsAborted As Boolean
        Public hNameMappings As IntPtr
        Public lpszProgressTitle As String
    End Structure

    <DllImport("shell32.dll", CharSet:=CharSet.Unicode, EntryPoint:="SHFileOperationW")>
    Private Function SHFileOperation(ByRef lpFileOp As SHFILEOPSTRUCT) As Integer
    End Function

    ''' <summary>把文件夹登记进启动器的文件夹列表，并设为当前文件夹。</summary>
    Public Sub Register(Folder As String, Name As String)
        Try
            Dim Full As String = PathUtils.AddSlashSuffix(PathUtils.ForCompare(Folder))
            Dim List As New List(Of String)
            For Each Item As String In Settings.Get(Of String)("LaunchFolders").Split("|"c, True)
                If String.IsNullOrWhiteSpace(Item) OrElse Not Item.Contains(">") Then Continue For
                If PathUtils.IsSame(Item.AfterFirst(">"), Full) Then Continue For
                List.Add(Item)
            Next
            List.Add($"{If(String.IsNullOrWhiteSpace(Name), "导入的文件夹", Name)}>{Full}")
            Settings.Set("LaunchFolders", List.Join("|"c))
            Settings.Set("LaunchFolderSelect", Full)
            McFolderListLoader.Start(IsForceRestart:=True)
            Logger.Info($"已导入游戏文件夹：{Full}")
        Catch ex As Exception
            Logger.Warn(ex, "登记游戏文件夹失败")
        End Try
    End Sub

End Module
