Imports System.IO.Compression

''' <summary>
''' 版本「归档」：把一个不常玩但想保留的版本深度压缩成单个压缩包，
''' 版本文件夹里只留版本 json 与归档标记，这样版本列表里仍然能看到它，
''' 但占用空间大幅下降。点选时会提示是否解压还原。
''' </summary>
Public Module ModArchive

    ''' <summary>归档标记文件（存在即代表该版本已归档）。</summary>
    Public Const MarkerName As String = "_archived.json"
    ''' <summary>归档压缩包文件名。</summary>
    Public Const ArchiveName As String = "_archived.zip"

    ''' <summary>该版本是否已归档。</summary>
    Public Function IsArchived(VersionPath As String) As Boolean
        Try
            Return FileUtils.Exists(VersionPath & MarkerName)
        Catch
            Return False
        End Try
    End Function

    ''' <summary>读取归档信息，未归档返回 Nothing。</summary>
    Public Function ReadInfo(VersionPath As String) As JObject
        Try
            Dim FilePath As String = VersionPath & MarkerName
            If Not FileUtils.Exists(FilePath) Then Return Nothing
            Return CType(FileUtils.ReadAsString(FilePath, Encoding.UTF8).DeserializeJson(), JObject)
        Catch ex As Exception
            Logger.Warn(ex, $"读取归档信息失败：{VersionPath}")
            Return Nothing
        End Try
    End Function

    ''' <summary>归档信息的中文摘要，例如「已归档 · 1.2 GB → 480 MB」。</summary>
    Public Function GetSummary(VersionPath As String) As String
        Dim Info = ReadInfo(VersionPath)
        If Info Is Nothing Then Return "已归档"
        Try
            Dim Before As Long = If(Info("sizeBefore") Is Nothing, 0, Info("sizeBefore").ToObject(Of Long))
            Dim After As Long = If(Info("sizeAfter") Is Nothing, 0, Info("sizeAfter").ToObject(Of Long))
            If Before > 0 AndAlso After > 0 Then
                Return $"已归档 · {FormatSize(Before)} → {FormatSize(After)}"
            End If
        Catch
        End Try
        Return "已归档"
    End Function

    Public Function FormatSize(Bytes As Long) As String
        If Bytes >= 1024L * 1024 * 1024 Then Return $"{Bytes / 1024.0 / 1024 / 1024:0.0} GB"
        If Bytes >= 1024L * 1024 Then Return $"{Bytes / 1024.0 / 1024:0.0} MB"
        Return $"{Bytes / 1024.0:0} KB"
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

    ''' <summary>
    ''' 归档指定版本：把除版本 json 与归档标记外的全部内容压缩进 _archived.zip，然后删除原文件。
    ''' </summary>
    ''' <param name="Progress">进度回调（0~1）。</param>
    Public Sub Archive(VersionPath As String, Progress As Action(Of Double))
        If Not DirectoryUtils.Exists(VersionPath) Then Throw New Exception("版本文件夹不存在！")
        If IsArchived(VersionPath) Then Throw New Exception("该版本已经归档过了！")
        Dim ZipPath As String = VersionPath & ArchiveName
        FileUtils.Delete(ZipPath)
        '收集要压缩的文件
        Dim Keep As List(Of String) = GetKeepFiles(VersionPath)
        Dim Files As New List(Of String)
        For Each FilePath As String In DirectoryUtils.EnumerateFiles(VersionPath, True)
            If String.Equals(FilePath, ZipPath, StringComparison.OrdinalIgnoreCase) Then Continue For
            If Keep.Any(Function(K) String.Equals(K, FilePath, StringComparison.OrdinalIgnoreCase)) Then Continue For
            Files.Add(FilePath)
        Next
        If Files.Count = 0 Then Throw New Exception("这个版本没有需要归档的内容。")
        Dim SizeBefore As Long = Files.Sum(Function(F) New FileInfo(F).Length)
        '写入压缩包（ZIP 最高压缩等级）
        DirectoryUtils.Create(IO.Path.GetDirectoryName(ZipPath))
        Using Stream As New FileStream(ZipPath, FileMode.Create, FileAccess.Write)
            Using Zip As New ZipArchive(Stream, ZipArchiveMode.Create)
                For i = 0 To Files.Count - 1
                    Dim FilePath As String = Files(i)
                    Dim EntryName As String = FilePath.Substring(VersionPath.Length).Replace("\"c, "/"c)
                    Try
                        Zip.CreateEntryFromFile(FilePath, EntryName, CompressionLevel.Optimal)
                    Catch ex As Exception
                        Logger.Warn(ex, $"归档时跳过文件：{FilePath}")
                    End Try
                    If i Mod 20 = 0 Then Progress(i / Files.Count * 0.95)
                Next
            End Using
        End Using
        Progress(0.97)
        '删除原文件（保留版本 json）
        For Each FilePath As String In Files
            Try
                FileUtils.Delete(FilePath)
            Catch ex As Exception
                Logger.Warn(ex, $"归档后删除文件失败：{FilePath}")
            End Try
        Next
        '清理空文件夹
        Try
            For Each Folder As String In DirectoryUtils.EnumerateDirectories(VersionPath, True).OrderByDescending(Function(F) F.Length)
                If DirectoryUtils.IsEmpty(Folder) Then DirectoryUtils.Delete(Folder)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "清理归档后的空文件夹失败")
        End Try
        Dim SizeAfter As Long = New FileInfo(ZipPath).Length
        '写标记
        Dim Info As New JObject From {
            {"time", Now.ToString("yyyy-MM-dd HH:mm:ss")},
            {"sizeBefore", SizeBefore},
            {"sizeAfter", SizeAfter},
            {"fileCount", Files.Count},
            {"compression", "zip-deflate"}
        }
        FileUtils.Write(VersionPath & MarkerName, Info.ToString())
        Progress(1)
        Logger.Info($"版本已归档：{VersionPath}（{FormatSize(SizeBefore)} → {FormatSize(SizeAfter)}）")
    End Sub

    ''' <summary>
    ''' 解压已归档的版本：把 _archived.zip 还原回版本文件夹，然后删除压缩包与标记。
    ''' </summary>
    Public Sub Extract(VersionPath As String, Progress As Action(Of Double))
        If Not IsArchived(VersionPath) Then Throw New Exception("该版本没有归档。")
        Dim ZipPath As String = VersionPath & ArchiveName
        If Not FileUtils.Exists(ZipPath) Then Throw New Exception("找不到归档压缩包，可能已被删除！")
        Using Stream As New FileStream(ZipPath, FileMode.Open, FileAccess.Read)
            Using Zip As New ZipArchive(Stream, ZipArchiveMode.Read)
                Dim Total As Integer = Math.Max(Zip.Entries.Count, 1)
                Dim Index As Integer = 0
                For Each Entry As ZipArchiveEntry In Zip.Entries
                    Index += 1
                    If String.IsNullOrEmpty(Entry.Name) Then Continue For '目录项
                    Dim Target As String = VersionPath & Entry.FullName.Replace("/"c, "\"c)
                    DirectoryUtils.Create(IO.Path.GetDirectoryName(Target))
                    Try
                        Entry.ExtractToFile(Target, True)
                    Catch ex As Exception
                        Logger.Warn(ex, $"解压文件失败：{Entry.FullName}")
                    End Try
                    If Index Mod 20 = 0 Then Progress(Index / Total * 0.95)
                Next
            End Using
        End Using
        Progress(0.97)
        FileUtils.Delete(ZipPath)
        FileUtils.Delete(VersionPath & MarkerName)
        Progress(1)
        Logger.Info($"版本已解压还原：{VersionPath}")
    End Sub

End Module
