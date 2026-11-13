Imports System.IO
Imports System.IO.Compression

''' <summary>
''' 种子地图的磁盘缓存。
''' 缓存的是「采样结果」——64×64 的群系 ID 网格（可选带地表高度），不是上色后的图：
''' 颜色会随群系筛选、高亮、浮雕开关变化，缓存 ID 才能在改设置后继续复用。
''' 一张瓦片重新采样约 20 ms（开浮雕再 +11.5 ms），命中缓存只剩读盘 + 上色（约 0.3 ms）。
''' 目录：YQL\Cache\SeedMap\&lt;种子&gt;_v&lt;版本&gt;_d&lt;维度&gt;\L&lt;级别&gt;\&lt;x&gt;_&lt;z&gt;.bin / .h
''' </summary>
Public Module SeedMapCache

    Private Const TileCells As Integer = 64
    Private Const SaveCapBytes As Long = 20L * 1024 * 1024   '单个存档上限
    Private Const GlobalCapBytes As Long = 200L * 1024 * 1024 '全部存档合计上限

    Private ReadOnly Sync As New Object()
    Private _WriteTick As Integer

    ''' <summary>设置里是否开启了缓存。</summary>
    Public ReadOnly Property Enabled As Boolean
        Get
            Try
                Return Settings.Get(Of Boolean)("SeedMapDiskCache")
            Catch ex As Exception
                Return False
            End Try
        End Get
    End Property

    Public Function RootDir() As String
        Return IO.Path.Combine(Paths.Base, "YQL\Cache\SeedMap")
    End Function

    Private Function SaveDir(Seed As Long, Ver As Integer, DimId As Integer) As String
        Return IO.Path.Combine(RootDir(), Seed.ToString() & "_v" & Ver.ToString() & "_d" & DimId.ToString())
    End Function

    Private Function TilePath(Seed As Long, Ver As Integer, DimId As Integer,
                              Level As Integer, Tx As Integer, Tz As Integer, Heights As Boolean) As String
        Return IO.Path.Combine(IO.Path.Combine(SaveDir(Seed, Ver, DimId), "L" & Level.ToString()),
                               Tx.ToString() & "_" & Tz.ToString() & If(Heights, ".h", ".bin"))
    End Function

    ''' <summary>尝试读取缓存。命中时 Ids（需要时连 Heights）直接可用，调用方跳过采样即可。</summary>
    Public Function TryLoad(Seed As Long, Ver As Integer, DimId As Integer,
                            Level As Integer, Tx As Integer, Tz As Integer,
                            NeedHeights As Boolean, ByRef Ids As Integer(), ByRef Heights As Single()) As Boolean
        Ids = Nothing
        Heights = Nothing
        If Not Enabled Then Return False
        Try
            Dim IdsPath As String = TilePath(Seed, Ver, DimId, Level, Tx, Tz, False)
            Dim Raw As Byte() = ReadRaw(IdsPath)
            If Raw Is Nothing OrElse Raw.Length <> TileCells * TileCells * 2 Then Return False
            Dim Arr(TileCells * TileCells - 1) As Integer
            For i As Integer = 0 To Arr.Length - 1
                Arr(i) = BitConverter.ToUInt16(Raw, i * 2)
            Next
            Ids = Arr
            If NeedHeights Then
                Dim HPath As String = TilePath(Seed, Ver, DimId, Level, Tx, Tz, True)
                Dim RawH As Byte() = ReadRaw(HPath)
                '高度按 1/4 方块量化存 ushort：只要够浮雕着色用，体积从 16 KB 降到 8 KB 且极易压缩
                If RawH IsNot Nothing AndAlso RawH.Length = TileCells * TileCells * 2 Then
                    Dim HArr(TileCells * TileCells - 1) As Single
                    For i As Integer = 0 To HArr.Length - 1
                        HArr(i) = CSng(BitConverter.ToUInt16(RawH, i * 2)) / 4.0F
                    Next
                    Heights = HArr
                End If
            End If
            '命中就摸一下时间戳，LRU 淘汰时按它判断新旧
            Try
                IO.File.SetLastWriteTimeUtc(IdsPath, Date.UtcNow)
            Catch ex As Exception
            End Try
            Return True
        Catch ex As Exception
            Logger.Warn(ex, "读取种子地图缓存失败")
            Return False
        End Try
    End Function

    ''' <summary>把采样结果异步写盘（不占用 UI 线程与采样线程）。</summary>
    Public Sub SaveAsync(Seed As Long, Ver As Integer, DimId As Integer,
                         Level As Integer, Tx As Integer, Tz As Integer,
                         Ids As Integer(), Heights As Single())
        If Not Enabled OrElse Ids Is Nothing Then Return
        Dim Copy(Ids.Length - 1) As Integer
        Array.Copy(Ids, Copy, Ids.Length)
        Dim HCopy As Single() = Nothing
        If Heights IsNot Nothing AndAlso Heights.Length = Ids.Length Then
            ReDim HCopy(Heights.Length - 1)
            Array.Copy(Heights, HCopy, Heights.Length)
        End If
        Threading.Tasks.Task.Run(
            Sub()
                Try
                    WriteTile(Seed, Ver, DimId, Level, Tx, Tz, Copy, HCopy)
                Catch ex As Exception
                    Logger.Warn(ex, "写入种子地图缓存失败")
                End Try
            End Sub)
    End Sub

    Private Sub WriteTile(Seed As Long, Ver As Integer, DimId As Integer,
                          Level As Integer, Tx As Integer, Tz As Integer,
                          Ids As Integer(), Heights As Single())
        Dim Raw(Ids.Length * 2 - 1) As Byte
        For i As Integer = 0 To Ids.Length - 1
            Dim Id As Integer = Ids(i)
            If Id < 0 Then Id = 0
            If Id > UShort.MaxValue Then Id = UShort.MaxValue
            Raw(i * 2) = CByte(Id And &HFF)
            Raw(i * 2 + 1) = CByte((Id >> 8) And &HFF)
        Next
        WriteRaw(TilePath(Seed, Ver, DimId, Level, Tx, Tz, False), Raw)
        If Heights IsNot Nothing Then
            Dim Rh(Heights.Length * 2 - 1) As Byte
            For i As Integer = 0 To Heights.Length - 1
                Dim Q As Long = CLng(Math.Round(Heights(i) * 4.0))
                If Q < 0 Then Q = 0
                If Q > UShort.MaxValue Then Q = UShort.MaxValue
                Rh(i * 2) = CByte(Q And &HFF)
                Rh(i * 2 + 1) = CByte((Q >> 8) And &HFF)
            Next
            WriteRaw(TilePath(Seed, Ver, DimId, Level, Tx, Tz, True), Rh)
        End If
        EnforceCaps(Seed, Ver, DimId)
    End Sub

    Private Function ReadRaw(Path As String) As Byte()
        If Not IO.File.Exists(Path) Then Return Nothing
        Using Fs As New IO.FileStream(Path, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
            Using Ds As New DeflateStream(Fs, CompressionMode.Decompress)
                Using Ms As New IO.MemoryStream()
                    Ds.CopyTo(Ms)
                    Return Ms.ToArray()
                End Using
            End Using
        End Using
    End Function

    Private Sub WriteRaw(Path As String, Data As Byte())
        Dim Folder As String = IO.Path.GetDirectoryName(Path)
        If Not IO.Directory.Exists(Folder) Then IO.Directory.CreateDirectory(Folder)
        Dim Tmp As String = Path & ".tmp"
        Using Fs As New IO.FileStream(Tmp, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.None)
            Using Ds As New DeflateStream(Fs, CompressionMode.Compress)
                Ds.Write(Data, 0, Data.Length)
            End Using
        End Using
        If IO.File.Exists(Path) Then IO.File.Delete(Path)
        IO.File.Move(Tmp, Path)
    End Sub

    ''' <summary>每写入 64 张检查一次总量，超限按最久未使用淘汰。</summary>
    Private Sub EnforceCaps(Seed As Long, Ver As Integer, DimId As Integer)
        SyncLock Sync
            _WriteTick += 1
            If _WriteTick Mod 64 <> 0 Then Return
        End SyncLock
        Try
            TrimTo(SaveDir(Seed, Ver, DimId), SaveCapBytes)
            TrimTo(RootDir(), GlobalCapBytes)
        Catch ex As Exception
            Logger.Warn(ex, "清理种子地图缓存失败")
        End Try
    End Sub

    Private Sub TrimTo(Folder As String, Cap As Long)
        If Not IO.Directory.Exists(Folder) Then Return
        Dim Files As IO.FileInfo() = New IO.DirectoryInfo(Folder).GetFiles("*", IO.SearchOption.AllDirectories)
        Dim Total As Long = 0
        For Each F In Files
            Total += F.Length
        Next
        If Total <= Cap Then Return
        For Each F In Files.OrderBy(Function(X) X.LastWriteTimeUtc).ToArray()
            If Total <= Cap Then Exit For
            Total -= F.Length
            Try
                F.Delete()
            Catch ex As Exception
            End Try
        Next
    End Sub

    Public Function UsageBytes() As Long
        Try
            Dim Folder As String = RootDir()
            If Not IO.Directory.Exists(Folder) Then Return 0
            Dim Total As Long = 0
            For Each F In New IO.DirectoryInfo(Folder).GetFiles("*", IO.SearchOption.AllDirectories)
                Total += F.Length
            Next
            Return Total
        Catch ex As Exception
            Return 0
        End Try
    End Function

    ''' <summary>缓存占用的人话描述，给设置页显示。</summary>
    Public Function DescribeUsage() As String
        Dim Bytes As Long = UsageBytes()
        If Bytes < 1024 Then Return "当前没有缓存"
        If Bytes < 1024L * 1024 Then Return "当前占用 " & Math.Round(Bytes / 1024.0, 0).ToString() & " KB"
        Return "当前占用 " & Math.Round(Bytes / 1024.0 / 1024.0, 1).ToString() & " MB"
    End Function

    Public Sub Clear()
        Try
            Dim Folder As String = RootDir()
            If IO.Directory.Exists(Folder) Then IO.Directory.Delete(Folder, True)
            Logger.Info("已清空种子地图缓存")
        Catch ex As Exception
            Logger.Warn(ex, "清空种子地图缓存失败")
        End Try
    End Sub

End Module
