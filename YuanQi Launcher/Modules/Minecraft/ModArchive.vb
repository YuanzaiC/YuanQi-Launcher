Imports System.IO.Compression

''' <summary>
''' 版本「归档」：把一个不常玩但想保留的版本压缩成压缩包，
''' 版本文件夹里只留版本 json 与归档标记，这样版本列表里仍然能看到它，
''' 但占用空间大幅下降。点选时会提示是否解压还原。
''' <para/>安全前提（写这个模块时反复确认过）：压缩包里那份数据是用户内容的【唯一副本】，
''' 任何一步出问题都必须保留它，宁可整个操作失败。
''' </summary>
Public Module ModArchive

    ''' <summary>归档标记文件（存在即代表该版本已归档）。</summary>
    Public Const MarkerName As String = "_archived.json"
    ''' <summary>单卷归档压缩包文件名。</summary>
    Public Const ArchiveName As String = "_archived.zip"
    ''' <summary>分卷归档压缩包名前缀（_archived.part01.zip、_archived.part02.zip…）。</summary>
    Public Const PartPrefix As String = "_archived.part"
    ''' <summary>归档过程中的临时文件后缀（写完并校验通过才改名成正式压缩包）。</summary>
    Private Const TempSuffix As String = ".tmp"
    ''' <summary>
    ''' 单卷压缩包的体积软上限（按【压缩后】的包体计算）。
    ''' .NET Framework 的 ZipArchive【不支持 ZIP64】：实测写入 4.3 GB 的条目时，
    ''' 条目长度字段会直接在 2^32 处回绕（4294967296 → 数据静默损坏），而且包还能"正常"打开。
    ''' 所以包体积接近 4 GB 就必须收卷，留 0.4 GB 余量。
    ''' </summary>
    Friend Const PartLimitDefault As Long = 3600L * 1024 * 1024
    ''' <summary>测试用：非 0 时替代 <see cref="PartLimitDefault"/>（正式运行时始终为 0）。</summary>
    Friend PartLimitOverride As Long = 0

    '---- 同一个版本不允许同时跑两个归档/解压（双击、批量与手动撞车都会踩到）----
    Private ReadOnly RunningOps As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly RunningLock As New Object()

    Private Function TryBeginOperation(VersionPath As String) As Boolean
        VersionPath = NormPath(VersionPath)
        SyncLock RunningLock
            If RunningOps.Contains(VersionPath) Then Return False
            RunningOps.Add(VersionPath)
            Return True
        End SyncLock
    End Function

    Private Sub EndOperation(VersionPath As String)
        VersionPath = NormPath(VersionPath)
        SyncLock RunningLock
            RunningOps.Remove(VersionPath)
        End SyncLock
    End Sub

    Private Function PartLimit() As Long
        Return If(PartLimitOverride > 0, PartLimitOverride, PartLimitDefault)
    End Function

    ''' <summary>
    ''' 版本路径统一成"以反斜杠结尾"的形式。
    ''' 归档包和标记必须是【版本文件夹里面】的 _archived.zip / _archived.json；
    ''' 调用方有的传 "…\versions\名字\"、有的传 "…\versions\名字"，
    ''' 不统一的话就会拼成 "…\versions\名字_archived.zip"（跑到文件夹外面去），
    ''' 结果就是"明明压缩好了，列表却看不出来已归档"。
    ''' </summary>
    Public Function NormPath(VersionPath As String) As String
        If String.IsNullOrEmpty(VersionPath) Then Return VersionPath
        If VersionPath.EndsWith("\"c) OrElse VersionPath.EndsWith("/"c) Then Return VersionPath
        Return VersionPath & "\"
    End Function

    ''' <summary>分卷文件名：_archived.part01.zip。</summary>
    Private Function PartName(Index As Integer) As String
        Return $"{PartPrefix}{Index:00}.zip"
    End Function

    ''' <summary>判断某个相对路径是不是"归档自己的文件"（压缩包 / 分卷 / 标记 / 临时文件）。</summary>
    Private Function IsOwnFile(Rel As String) As Boolean
        If String.IsNullOrEmpty(Rel) Then Return False
        If Rel.IndexOf("\"c) >= 0 Then Return False '只看版本根目录下的文件
        If String.Equals(Rel, MarkerName, StringComparison.OrdinalIgnoreCase) Then Return True
        If String.Equals(Rel, ArchiveName, StringComparison.OrdinalIgnoreCase) Then Return True
        If Rel.StartsWithF(PartPrefix, True) AndAlso Rel.EndsWithF(".zip" & TempSuffix, True) Then Return True
        If Rel.StartsWithF(PartPrefix, True) AndAlso Rel.EndsWithF(".zip", True) Then Return True
        If Rel.StartsWithF(PartPrefix, True) Then Return True
        If Rel.EndsWithF(TempSuffix, True) AndAlso Rel.StartsWithF(ArchiveName, True) Then Return True
        Return False
    End Function

    ''' <summary>目录里现存的归档压缩包（单卷或分卷），按卷号排序。</summary>
    Private Function GetArchiveFiles(VersionPath As String) As List(Of String)
        Dim Result As New List(Of String)
        VersionPath = NormPath(VersionPath)
        Try
            For Each FilePath As String In Directory.GetFiles(VersionPath)
                Dim Name As String = IO.Path.GetFileName(FilePath)
                If String.Equals(Name, ArchiveName, StringComparison.OrdinalIgnoreCase) Then
                    Result.Insert(0, FilePath)
                ElseIf Name.StartsWithF(PartPrefix, True) AndAlso Name.EndsWithF(".zip", True) Then
                    Result.Add(FilePath)
                End If
            Next
        Catch ex As Exception
            Logger.Warn(ex, $"枚举归档包失败：{VersionPath}")
        End Try
        Return Result
    End Function

    ''' <summary>
    ''' 该版本是否已归档。
    ''' 标记丢了但压缩包还在也算已归档：用户手工把 _archived.zip 放回版本文件夹时，
    ''' 也应该能直接点「解压」把内容还原出来。
    ''' </summary>
    Public Function IsArchived(VersionPath As String) As Boolean
        Try
            VersionPath = NormPath(VersionPath)
            If FileUtils.Exists(VersionPath & MarkerName) Then Return True
            Return GetArchiveFiles(VersionPath).Any()
        Catch
            Return False
        End Try
    End Function

    ''' <summary>读取归档信息，未归档返回 Nothing。</summary>
    Public Function ReadInfo(VersionPath As String) As JObject
        VersionPath = NormPath(VersionPath)
        Try
            Dim FilePath As String = VersionPath & MarkerName
            If FileUtils.Exists(FilePath) Then
                Return CType(FileUtils.ReadAsString(FilePath, Encoding.UTF8).DeserializeJson(), JObject)
            End If
            '没有标记但有压缩包（手工放回来的）：现读一下压缩包，凑一份信息出来
            Dim Files As List(Of String) = GetArchiveFiles(VersionPath)
            If Files.Count = 0 Then Return Nothing
            Dim Count As Integer = 0
            Dim Total As Long = 0
            For Each ZipPath As String In Files
                Total += New IO.FileInfo(ZipPath).Length
                Try
                    Using Stream As New IO.FileStream(ZipPath, IO.FileMode.Open, IO.FileAccess.Read)
                        Using Zip As New ZipArchive(Stream, ZipArchiveMode.Read)
                            Count += Zip.Entries.Count
                        End Using
                    End Using
                Catch ex As Exception
                    Logger.Warn(ex, $"读取归档包失败（可能已损坏）：{ZipPath}")
                End Try
            Next
            Return New JObject From {
                {"time", IO.File.GetLastWriteTime(Files(0)).ToString("yyyy-MM-dd HH:mm:ss")},
                {"sizeBefore", 0},
                {"sizeAfter", Total},
                {"fileCount", Count},
                {"parts", Files.Count},
                {"compression", "zip-deflate"},
                {"noMarker", True}
            }
        Catch ex As Exception
            Logger.Warn(ex, $"读取归档信息失败：{VersionPath}")
            Return Nothing
        End Try
    End Function

    ''' <summary>归档信息的中文摘要，例如「已归档 · 1.2 GB → 480 MB」。</summary>
    Public Function GetSummary(VersionPath As String) As String
        VersionPath = NormPath(VersionPath)
        If GetArchiveFiles(VersionPath).Count = 0 AndAlso FileUtils.Exists(VersionPath & MarkerName) Then
            Return "已归档（压缩包丢失，无法解压）"
        End If
        Dim Info = ReadInfo(VersionPath)
        If Info Is Nothing Then Return "已归档"
        Try
            Dim Before As Long = If(Info("sizeBefore") Is Nothing, 0, Info("sizeBefore").ToObject(Of Long))
            Dim After As Long = If(Info("sizeAfter") Is Nothing, 0, Info("sizeAfter").ToObject(Of Long))
            Dim Parts As Integer = If(Info("parts") Is Nothing, 1, Info("parts").ToObject(Of Integer))
            Dim Sizes As String
            If Before > 0 AndAlso After > 0 Then
                Sizes = $"{FormatSize(Before)} → {FormatSize(After)}"
            ElseIf After > 0 Then
                Sizes = $"压缩包 {FormatSize(After)}"
            Else
                Return "已归档"
            End If
            If Parts > 1 Then Return $"已归档 · {Parts} 卷 · {Sizes}"
            Return $"已归档 · {Sizes}"
        Catch
        End Try
        Return "已归档"
    End Function

    Public Function FormatSize(Bytes As Long) As String
        If Bytes >= 1024L * 1024 * 1024 Then Return $"{Bytes / 1024.0 / 1024 / 1024:0.0} GB"
        If Bytes >= 1024L * 1024 Then Return $"{Bytes / 1024.0 / 1024:0.0} MB"
        If Bytes >= 1024L Then Return $"{Bytes / 1024.0:0} KB"
        Return $"{Bytes} B"
    End Function

    ''' <summary>统计文件夹占用空间（不含指定的若干文件）。</summary>
    Public Function GetFolderSize(Folder As String, Excludes As String()) As Long
        Dim Total As Long = 0
        If Not DirectoryUtils.Exists(Folder) Then Return 0
        For Each FilePath As String In DirectoryUtils.EnumerateFiles(Folder, True)
            If Excludes IsNot Nothing AndAlso Excludes.Any(Function(E) String.Equals(E, FilePath, StringComparison.OrdinalIgnoreCase)) Then Continue For
            Try
                Total += New FileInfo(FilePath).Length
            Catch
            End Try
        Next
        Return Total
    End Function

    ''' <summary>该版本归档时应当保留在文件夹里的文件（版本 json，供版本列表识别）。</summary>
    Private Function GetKeepFiles(VersionPath As String) As List(Of String)
        Dim Result As New List(Of String)
        VersionPath = NormPath(VersionPath)
        Try
            For Each FilePath As String In Directory.GetFiles(VersionPath)
                Dim Name As String = IO.Path.GetFileName(FilePath)
                If Name.EndsWithF(".json", True) AndAlso Not Name = MarkerName Then Result.Add(FilePath)
            Next
        Catch ex As Exception
            Logger.Warn(ex, $"枚举版本 json 失败：{VersionPath}")
        End Try
        Return Result
    End Function

    '===============================
    '  归档
    '===============================

    ''' <summary>
    ''' 归档指定版本：把除版本 json 与归档标记外的全部内容压缩进压缩包，然后删除原文件。
    ''' 体积超过单卷上限时自动分卷（_archived.part01.zip、part02…），因为 .NET 的 zip 不支持 ZIP64。
    ''' <para/>执行顺序保证：所有分卷都写进临时文件并通过校验之后，才会改名、删原文件、写标记。
    ''' 中途任何一步失败 → 删掉临时文件，原文件一个都不动。
    ''' </summary>
    ''' <param name="Progress">进度回调（0~1）。</param>
    Public Sub Archive(VersionPath As String, Progress As Action(Of Double))
        VersionPath = NormPath(VersionPath)
        If Not TryBeginOperation(VersionPath) Then Throw New Exception("这个版本正在归档或解压中，等它跑完再试。")
        Try
            ArchiveCore(VersionPath, Progress)
        Finally
            EndOperation(VersionPath)
        End Try
    End Sub

    Private Sub ArchiveCore(VersionPath As String, Progress As Action(Of Double))
        If Not DirectoryUtils.Exists(VersionPath) Then Throw New Exception("版本文件夹不存在！")
        If FileUtils.Exists(VersionPath & MarkerName) Then Throw New Exception("该版本已经归档过了！")
        Dim ExistArchives As List(Of String) = GetArchiveFiles(VersionPath)
        If ExistArchives.Count > 0 Then
            Throw New Exception("这个版本里已经有归档压缩包，但缺少归档标记。请先点「解压」把它还原，再重新归档。")
        End If
        Dim TempFiles As New List(Of String)

        '收集要压缩的文件：一律按“相对路径”判断，跟调用方传的路径写法无关
        Dim Keep As List(Of String) = GetKeepFiles(VersionPath)
        Dim Files As New List(Of String)
        For Each FilePath As String In DirectoryUtils.EnumerateFiles(VersionPath, True)
            Dim Rel As String = FilePath.Substring(VersionPath.Length)
            If IsOwnFile(Rel) Then Continue For
            If Keep.Any(Function(K) String.Equals(K, FilePath, StringComparison.OrdinalIgnoreCase)) Then Continue For
            Files.Add(FilePath)
        Next
        If Files.Count = 0 Then Throw New Exception("这个版本没有需要归档的内容。")

        Dim SizeBefore As Long = Files.Sum(Function(F) New FileInfo(F).Length)
        If SizeBefore > 200L * 1024 * 1024 * 1024 Then Throw New Exception($"这个版本有 {FormatSize(SizeBefore)}，太大了，不适合归档。")

        '---- 第一步：写卷 + 逐个校验（这一步绝不删任何原文件）----
        '收卷依据是"压缩包已经写出去的体积"（Stream.Position），不是原始体积：
        '这样 3.6 GB 的整合包如果压完只有 2.6 GB，仍然是一卷，不必白切。
        Dim Groups As New List(Of List(Of String))
        Try
            Dim Index As Integer = 0
            Do While Index < Files.Count
                Dim Group As New List(Of String)
                Dim GroupBytes As Long = 0
                Dim Fail As New List(Of String)
                Dim TempPath As String = VersionPath & ArchiveName & $".{Groups.Count + 1:00}" & TempSuffix
                TempFiles.Add(TempPath)
                FileUtils.Delete(TempPath)
                Using Stream As New FileStream(TempPath, FileMode.Create, FileAccess.Write)
                    Using Zip As New ZipArchive(Stream, ZipArchiveMode.Create)
                        Do While Index < Files.Count
                            '已经写了至少一个文件、并且当前包体积到软上限 → 收卷
                            If Group.Count > 0 AndAlso Stream.Position > PartLimit() Then Exit Do
                            Dim FilePath As String = Files(Index)
                            Dim EntryName As String = FilePath.Substring(VersionPath.Length).Replace("\"c, "/"c)
                            Dim Len As Long = 0
                            Try
                                Len = New FileInfo(FilePath).Length
                            Catch
                            End Try
                            Try
                                Zip.CreateEntryFromFile(FilePath, EntryName, CompressionLevel.Optimal)
                                Group.Add(FilePath)
                                GroupBytes += Len
                            Catch ex As Exception
                                Logger.Warn(ex, $"归档时跳过文件：{FilePath}")
                                Fail.Add(FilePath)
                            End Try
                            Index += 1
                            Progress(Index / Files.Count * 0.9)
                        Loop
                    End Using
                End Using
                If Fail.Count > 0 Then
                    Throw New Exception($"有 {Fail.Count} 个文件没能写进归档包（{String.Join("、", Fail.Take(3))}），已放弃归档，原文件未改动。")
                End If
                If Group.Count = 0 Then Throw New Exception("归档包里没有内容，已放弃归档，原文件未改动。")
                '写完必须重新打开核对：条目数、解压后总字节数、能不能真的读到数据
                Dim Why As String = VerifyArchiveFile(TempPath, Group.Count, GroupBytes)
                If Why IsNot Nothing Then
                    Throw New Exception($"归档包校验失败（{Why}），已放弃归档，原文件未改动。")
                End If
                Groups.Add(Group)
            Loop
            Dim IsSplit As Boolean = Groups.Count > 1

            '---- 第二步：全部校验通过，才改名、删原文件、写标记 ----
            Progress(0.93)
            For g As Integer = 0 To Groups.Count - 1
                Dim FinalName As String = If(IsSplit, PartName(g + 1), ArchiveName)
                Dim FinalPath As String = VersionPath & FinalName
                Dim TempPath As String = TempFiles(g)
                FileUtils.Delete(FinalPath)
                IO.File.Move(TempPath, FinalPath)
            Next
            TempFiles.Clear()
            For Each Group As List(Of String) In Groups
                For Each FilePath As String In Group
                    Try
                        FileUtils.Delete(FilePath)
                    Catch ex As Exception
                        Logger.Warn(ex, $"归档后删除文件失败：{FilePath}")
                    End Try
                Next
            Next
        Catch ex As Exception
            '任何一步出问题：删掉临时文件，原文件一个都不删
            For Each TempPath As String In TempFiles
                Try
                    FileUtils.Delete(TempPath)
                Catch
                End Try
            Next
            Throw
        End Try

        '清理空文件夹
        Try
            For Each Folder As String In DirectoryUtils.EnumerateDirectories(VersionPath, True).OrderByDescending(Function(F) F.Length)
                If DirectoryUtils.IsEmpty(Folder) Then DirectoryUtils.Delete(Folder)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "清理归档后的空文件夹失败")
        End Try

        Dim SizeAfter As Long = GetArchiveFiles(VersionPath).Sum(Function(F) New IO.FileInfo(F).Length)
        Dim Info As New JObject From {
            {"time", Now.ToString("yyyy-MM-dd HH:mm:ss")},
            {"sizeBefore", SizeBefore},
            {"sizeAfter", SizeAfter},
            {"fileCount", Files.Count},
            {"parts", Groups.Count},
            {"split", Groups.Count > 1},
            {"compression", "zip-deflate"}
        }
        Try
            FileUtils.Write(VersionPath & MarkerName, Info.ToString())
        Catch ex As Exception
            '标记只用来显示"已归档 · 大小"，写不进去不影响数据：有压缩包就算已归档
            Logger.Warn(ex, "写入归档标记失败（不影响解压）")
        End Try
        Progress(1)
        Logger.Info($"版本已归档：{VersionPath}（{FormatSize(SizeBefore)} → {FormatSize(SizeAfter)}，{Files.Count} 个文件，{Groups.Count} 卷）")
    End Sub

    ''' <summary>
    ''' 校验一个归档包：能重新打开、条目数一致、解压后总字节数一致，并且最后一个条目真的读得出内容。
    ''' 通过返回 Nothing，否则返回失败原因。
    ''' </summary>
    Private Function VerifyArchiveFile(ZipPath As String, ExpectedCount As Integer, ExpectedBytes As Long) As String
        Try
            Using Stream As New FileStream(ZipPath, FileMode.Open, FileAccess.Read)
                Using Zip As New ZipArchive(Stream, ZipArchiveMode.Read)
                    If Zip.Entries.Count <> ExpectedCount Then
                        Return $"条目数应 {ExpectedCount}、实际 {Zip.Entries.Count}"
                    End If
                    Dim Total As Long = 0
                    For Each Entry As ZipArchiveEntry In Zip.Entries
                        Total += Entry.Length
                    Next
                    If Total <> ExpectedBytes Then
                        Return $"内容大小应 {FormatSize(ExpectedBytes)}、实际 {FormatSize(Total)}"
                    End If
                    '抽最后一个条目读几个字节，确认数据区真的可读（ZIP64 回绕这类问题会在这里暴露）
                    If Zip.Entries.Count > 0 Then
                        Dim Last As ZipArchiveEntry = Zip.Entries(Zip.Entries.Count - 1)
                        If Last.Length > 0 Then
                            Using S As IO.Stream = Last.Open()
                                Dim Buf(15) As Byte
                                If S.Read(Buf, 0, Buf.Length) <= 0 Then Return "最后一个文件读不出内容"
                            End Using
                        End If
                    End If
                End Using
            End Using
            Return Nothing
        Catch ex As Exception
            Return ex.Message
        End Try
    End Function

    '===============================
    '  解压
    '===============================

    ''' <summary>
    ''' 解压已归档的版本：把所有归档包（单卷或分卷）还原回版本文件夹，然后删除压缩包与标记。
    ''' 安全约定：只要有一个条目没还原成功，就保留全部压缩包与标记并报错——
    ''' 宁可留着压缩包让用户自己处理，也绝不把唯一的备份删掉。
    ''' </summary>
    Public Sub Extract(VersionPath As String, Progress As Action(Of Double))
        VersionPath = NormPath(VersionPath)
        If Not TryBeginOperation(VersionPath) Then Throw New Exception("这个版本正在归档或解压中，等它跑完再试。")
        Try
            ExtractCore(VersionPath, Progress)
        Finally
            EndOperation(VersionPath)
        End Try
    End Sub

    Private Sub ExtractCore(VersionPath As String, Progress As Action(Of Double))
        If Not IsArchived(VersionPath) Then Throw New Exception("该版本没有归档。")
        Dim Archives As List(Of String) = GetArchiveFiles(VersionPath)
        If Archives.Count = 0 Then
            Throw New Exception("只找到归档标记，压缩包却不见了。这个版本的内容已经无法从归档里还原，" &
                                "请从备份里找回 _archived.zip 放到版本文件夹里再试。")
        End If
        Dim OkCount As Integer = 0
        Dim OkBytes As Long = 0
        Dim BadList As New List(Of String)
        Try
            '先算总体积和总条目数，顺手确认每个包都能打开、磁盘空间够用
            Dim Need As Long = 0
            Dim TotalEntries As Integer = 0
            For Each ZipPath As String In Archives
                Using Stream As New FileStream(ZipPath, FileMode.Open, FileAccess.Read)
                    Using Zip As New ZipArchive(Stream, ZipArchiveMode.Read)
                        For Each Entry As ZipArchiveEntry In Zip.Entries
                            If Not String.IsNullOrEmpty(Entry.Name) Then
                                Need += Entry.Length
                                TotalEntries += 1
                            End If
                        Next
                    End Using
                End Using
            Next
            Dim Free As Long = -1
            Try
                Free = New IO.DriveInfo(IO.Path.GetPathRoot(IO.Path.GetFullPath(VersionPath))).AvailableFreeSpace
            Catch
            End Try
            If Free >= 0 AndAlso Need > Free Then
                Throw New Exception($"磁盘空间不够：解压需要 {FormatSize(Need)}，当前只剩 {FormatSize(Free)}。" &
                                    "压缩包已保留没动，腾出空间后再来解压。")
            End If

            '逐卷还原
            Dim Done As Integer = 0
            For Each ZipPath As String In Archives
                Using Stream As New FileStream(ZipPath, FileMode.Open, FileAccess.Read)
                    Using Zip As New ZipArchive(Stream, ZipArchiveMode.Read)
                        For Each Entry As ZipArchiveEntry In Zip.Entries
                            ExtractOneEntry(Entry, VersionPath, OkCount, OkBytes, BadList, 0)
                            Done += 1
                            If Done Mod 20 = 0 Then Progress(Done / Math.Max(TotalEntries, 1) * 0.9)
                        Next
                    End Using
                End Using
            Next
        Catch ex As Exception
            If TypeOf ex Is InvalidDataException Then
                Throw New Exception($"压缩包已损坏，读不出目录结构。这通常是恢复得不完整，需要重新抓一次；压缩包原地保留没动。" & vbCrLf & ex.Message)
            End If
            Throw
        End Try
        Progress(0.95)
        If BadList.Count > 0 Then
            Throw New Exception($"有 {BadList.Count} 项没能还原（{String.Join("；", BadList.Take(3))}）。" &
                                "压缩包已原样保留，修好问题（比如关掉占用文件的程序、腾出磁盘空间）后再解压一次即可。")
        End If
        If OkCount = 0 Then Throw New Exception("归档包里没有可还原的文件，压缩包已保留没删。")
        '全部还原成功：删压缩包，再删标记（标记删不掉也不影响，反正没压缩包就不算已归档）
        For Each ZipPath As String In Archives
            FileUtils.Delete(ZipPath)
        Next
        Try
            FileUtils.Delete(VersionPath & MarkerName)
        Catch ex As Exception
            Logger.Warn(ex, "删除归档标记失败（不影响使用）")
        End Try
        Progress(1)
        Logger.Info($"版本已解压还原：{VersionPath}（{OkCount} 个文件，{FormatSize(OkBytes)}，{Archives.Count} 卷）")
    End Sub

    ''' <summary>
    ''' 还原单个条目。若条目本身是一个压缩包（旧版归档把上一次的包套娃套了进来），
    ''' 就把它解出来再展开一层，把里面的真实文件放回去。
    ''' </summary>
    Private Sub ExtractOneEntry(Entry As ZipArchiveEntry, VersionPath As String, ByRef OkCount As Integer,
                                ByRef OkBytes As Long, BadList As List(Of String), Depth As Integer)
        Dim InnerPath As String = Nothing
        Try
            If String.IsNullOrEmpty(Entry.Name) Then Return '目录项
            '以前版本路径少了结尾的反斜杠，包内条目会以 "/" 开头，这里统一去掉
            Dim Rel As String = Entry.FullName.Replace("/"c, "\"c).TrimStart("\"c)
            If String.IsNullOrEmpty(Rel) Then Return
            '套娃：解出来再展开
            If Depth < 3 AndAlso Rel.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) AndAlso
               (IO.Path.GetFileName(Rel).StartsWithF("_archived", True)) Then
                InnerPath = VersionPath & "_recover_" & Guid.NewGuid().ToString("N") & ".zip"
                Entry.ExtractToFile(InnerPath, True)
                Using InnerStream As New FileStream(InnerPath, FileMode.Open, FileAccess.Read)
                    Using Iz As New ZipArchive(InnerStream, ZipArchiveMode.Read)
                        For Each InnerEntry As ZipArchiveEntry In Iz.Entries
                            ExtractOneEntry(InnerEntry, VersionPath, OkCount, OkBytes, BadList, Depth + 1)
                        Next
                    End Using
                End Using
                Return
            End If
            '防止压缩包里的路径跑到版本文件夹外面去
            Dim Root As String = IO.Path.GetFullPath(VersionPath)
            Dim Target As String = IO.Path.GetFullPath(VersionPath & Rel)
            If Not Target.StartsWith(Root, StringComparison.OrdinalIgnoreCase) Then
                BadList.Add($"{Entry.FullName}（路径越界）")
                Return
            End If
            DirectoryUtils.Create(IO.Path.GetDirectoryName(Target))
            Entry.ExtractToFile(Target, True)
            '落地后核对大小：写一半断掉、被占用、磁盘满都会在这里暴露，
            '这样"看着解完了其实不完整"会变成明确的失败（压缩包会保留）
            Dim Actual As Long = -1
            Try
                Actual = New IO.FileInfo(Target).Length
            Catch
            End Try
            If Actual <> Entry.Length Then
                BadList.Add($"{Entry.FullName}（大小不符：{Actual} / {Entry.Length}）")
                Return
            End If
            OkCount += 1
            OkBytes += Actual
        Catch ex As Exception
            Logger.Warn(ex, $"解压文件失败：{Entry.FullName}")
            BadList.Add($"{Entry.FullName}：{ex.Message}")
        Finally
            If Not String.IsNullOrEmpty(InnerPath) Then
                Try
                    FileUtils.Delete(InnerPath)
                Catch
                End Try
            End If
        End Try
    End Sub

End Module

''' <summary>
''' 版本占用大小的持久化缓存：用存储换响应速度，避免每次启动都遍历所有版本目录。
''' 失效条件：版本目录的 LastWriteTime 变化，或缓存条目超过 7 天。
''' </summary>
Public Module InstanceSizeStore

    Private Const CacheFileName As String = "InstanceSizes.json"
    Private Const MaxAgeDays As Double = 7
    Private Const SaveThrottleMs As Long = 3000

    Private ReadOnly SyncRoot As New Object()
    Private IsLoaded As Boolean = False
    Private IsDirty As Boolean = False
    Private LastSaveTick As Long = 0
    Private ReadOnly Entries As New Dictionary(Of String, SizeEntry)(StringComparer.OrdinalIgnoreCase)

    Private Class SizeEntry
        Public Bytes As Long
        Public Ticks As Long
        Public Saved As Date
    End Class

    Private ReadOnly Property CachePath As String
        Get
            Return Paths.Base & "YQL\Cache\" & CacheFileName
        End Get
    End Property

    Private Sub EnsureLoaded()
        If IsLoaded Then Return
        SyncLock SyncRoot
            If IsLoaded Then Return
            IsLoaded = True
            Try
                If FileUtils.Exists(CachePath) Then
                    Dim Root As JObject = TryCast(FileUtils.ReadAsString(CachePath, Encoding.UTF8).DeserializeJson(), JObject)
                    If Root IsNot Nothing Then
                        For Each Pair As KeyValuePair(Of String, JToken) In Root
                            Dim Node As JObject = TryCast(Pair.Value, JObject)
                            If Node Is Nothing Then Continue For
                            Dim Entry As New SizeEntry
                            Long.TryParse(If(Node("bytes")?.ToString, ""), Entry.Bytes)
                            Long.TryParse(If(Node("ticks")?.ToString, ""), Entry.Ticks)
                            Date.TryParse(If(Node("saved")?.ToString, ""), Entry.Saved)
                            Entries(Pair.Key) = Entry
                        Next
                        Logger.Info($"版本占用缓存：已载入 {Entries.Count} 项")
                    End If
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取版本占用缓存失败")
            End Try
        End SyncLock
    End Sub

    ''' <summary>读取缓存的大小；未命中或已失效返回 -1。</summary>
    Public Function TryGet(VersionPath As String) As Long
        EnsureLoaded()
        Try
            SyncLock SyncRoot
                Dim Entry As SizeEntry = Nothing
                If Not Entries.TryGetValue(VersionPath, Entry) Then Return -1
                If Entry.Bytes <= 0 Then Return -1
                Dim Age As Double = (Date.Now - Entry.Saved).TotalDays
                If Age > MaxAgeDays Then Return -1
                If IO.Directory.GetLastWriteTimeUtc(VersionPath).Ticks <> Entry.Ticks Then
                    '目录时间变了（启动游戏会往里写日志/配置），3 天内仍先信任缓存，超过再重算
                    If Age > 3 Then Return -1
                End If
                Return Entry.Bytes
            End SyncLock
        Catch
            Return -1
        End Try
    End Function

    ''' <summary>记录某版本的大小（节流后落盘）。</summary>
    Public Sub Store(VersionPath As String, Bytes As Long)
        EnsureLoaded()
        Try
            SyncLock SyncRoot
                Dim Entry As New SizeEntry With {
                    .Bytes = Bytes,
                    .Ticks = IO.Directory.GetLastWriteTimeUtc(VersionPath).Ticks,
                    .Saved = Date.Now
                }
                Entries(VersionPath) = Entry
                IsDirty = True
            End SyncLock
            RequestSave()
        Catch ex As Exception
            Logger.Warn(ex, "记录版本占用缓存失败")
        End Try
    End Sub

    Public Sub Remove(VersionPath As String)
        EnsureLoaded()
        Try
            Dim Changed As Boolean = False
            SyncLock SyncRoot
                Changed = Entries.Remove(VersionPath)
                If Changed Then IsDirty = True
            End SyncLock
            If Changed Then RequestSave()
        Catch ex As Exception
            Logger.Warn(ex, "移除版本占用缓存失败")
        End Try
    End Sub

    Private Sub RequestSave()
        SyncLock SyncRoot
            If Not IsDirty Then Return
            If GetTimeMs() - LastSaveTick < SaveThrottleMs Then Return
            LastSaveTick = GetTimeMs()
        End SyncLock
        RunInNewThread(Sub()
                           SaveNow()
                       End Sub, "Save Instance Size Cache", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>立即落盘。</summary>
    Public Sub SaveNow()
        SyncLock SyncRoot
            If Not IsDirty Then Return
            Try
                Dim Root As New JObject
                For Each Pair As KeyValuePair(Of String, SizeEntry) In Entries.ToList()
                    If (Date.Now - Pair.Value.Saved).TotalDays > MaxAgeDays Then Continue For
                    Dim Node As New JObject
                    Node("bytes") = Pair.Value.Bytes
                    Node("ticks") = Pair.Value.Ticks
                    Node("saved") = Pair.Value.Saved.ToString("o")
                    Root(Pair.Key) = Node
                Next
                DirectoryUtils.Create(IO.Path.GetDirectoryName(CachePath) & "\")
                FileUtils.Write(CachePath, Root.ToString())
                IsDirty = False
                Logger.Info($"版本占用缓存已保存：{Root.Count} 项")
            Catch ex As Exception
                Logger.Warn(ex, "保存版本占用缓存失败")
            End Try
        End SyncLock
    End Sub

End Module
