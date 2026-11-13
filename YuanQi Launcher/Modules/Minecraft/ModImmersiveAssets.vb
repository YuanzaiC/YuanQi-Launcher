'休眠观赏页（全屏待机页）所需的游戏素材读取
'
'全景贴图与位图字体全部在运行时从玩家自己的游戏目录读取，启动器既不内置、也不分发任何 Mojang 资产。
'取不到时由页面自行回退（自绘渐变 / 内置像素字 / 系统字体），这里只负责安静地返回 Nothing，不报错、不弹窗。

Imports System.Windows.Media.Imaging

Public Module ModImmersiveAssets

#Region "资源定位"

    ''' <summary>
    ''' 游戏标题界面全景的六个面，顺序为 前 / 右 / 后 / 左 / 上 / 下，与 panorama_0..5 一一对应。
    ''' </summary>
    Public ReadOnly PanoramaFacePaths As String() = {
        "minecraft/textures/gui/title/background/panorama_0.png",
        "minecraft/textures/gui/title/background/panorama_1.png",
        "minecraft/textures/gui/title/background/panorama_2.png",
        "minecraft/textures/gui/title/background/panorama_3.png",
        "minecraft/textures/gui/title/background/panorama_4.png",
        "minecraft/textures/gui/title/background/panorama_5.png"}

    ''' <summary>
    ''' 游戏自带的位图字形图集（16×16 格，每格 8×8）。注意现代版本已经不再包含它，见 EnsureFont。
    ''' </summary>
    Public Const AsciiFontPath As String = "minecraft/textures/font/ascii.png"

    ''' <summary>已解析的资源索引缓存：索引名 → objects 表（Nothing 表示解析失败，同样缓存以免反复读盘）。</summary>
    Private ReadOnly IndexCache As New Dictionary(Of String, JObject)

    ''' <summary>已解码的图片缓存，避免切换实例后重复解码。</summary>
    Private ReadOnly BitmapCache As New Dictionary(Of String, BitmapSource)

    ''' <summary>
    ''' 按资源索引把一个逻辑资源路径解析成磁盘上的真实文件路径。找不到返回 Nothing。
    ''' </summary>
    ''' <param name="Instance">用于确定资源索引名的实例，可为 Nothing（此时只按 legacy 索引查找）。</param>
    ''' <param name="SourcePath">形如 minecraft/textures/font/ascii.png 的逻辑路径。</param>
    Public Function ResolveAssetPath(Instance As McInstance, SourcePath As String) As String
        Dim Folder As String = McFolderSelected
        If String.IsNullOrEmpty(Folder) OrElse String.IsNullOrEmpty(SourcePath) Then Return Nothing
        Dim Relative As String = SourcePath.Replace("/", "\")

        '1. 常规路径：assets\indexes\<索引>.json → objects\<hash 前两位>\<hash>
        Dim IndexName As String = "legacy"
        Try
            If Instance IsNot Nothing Then IndexName = McAssetsGetIndexName(Instance)
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：获取资源索引名失败，改用 legacy")
        End Try
        Dim Objects As JObject = GetIndexObjects(IndexName)
        If Objects IsNot Nothing Then
            Dim Entry As JToken = Objects(SourcePath)
            If Entry IsNot Nothing AndAlso Entry("hash") IsNot Nothing Then
                Dim Hash As String = Entry("hash").ToString
                If Hash.Length >= 2 Then
                    Dim HashPath As String = Folder & "assets\objects\" & Hash.Substring(0, 2) & "\" & Hash
                    If FileUtils.Exists(HashPath) Then Return HashPath
                End If
            End If
            '老版本索引（virtual）会把文件平铺到 assets\virtual\legacy 下
            Dim VirtualPath As String = Folder & "assets\virtual\legacy\" & Relative
            If FileUtils.Exists(VirtualPath) Then Return VirtualPath
        End If

        '2. 少数整合包把 assets 直接解包在目录里
        Dim DirectPath As String = Folder & "assets\" & Relative
        If FileUtils.Exists(DirectPath) Then Return DirectPath

        '3. 版本隔离时 map_to_resources 会把资源重映射到版本目录的 resources 下
        Try
            If Instance IsNot Nothing Then
                Dim MappedPath As String = Instance.PathIndie & "resources\" & Relative
                If FileUtils.Exists(MappedPath) Then Return MappedPath
            End If
        Catch
            'PathIndie 可能因为版本 JSON 损坏而抛异常，这里只是回退路径，忽略即可
        End Try
        Return Nothing
    End Function

    ''' <summary>读取并缓存资源索引的 objects 表。</summary>
    Private Function GetIndexObjects(IndexName As String) As JObject
        SyncLock IndexCache
            If IndexCache.ContainsKey(IndexName) Then Return IndexCache(IndexName)
        End SyncLock
        Dim Result As JObject = Nothing
        Try
            Dim IndexFile As String = McFolderSelected & "assets\indexes\" & IndexName & ".json"
            If FileUtils.Exists(IndexFile) Then
                Dim Json As JObject = FileUtils.ReadAsJson(IndexFile)
                If Json IsNot Nothing Then Result = TryCast(Json("objects"), JObject)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：解析资源索引失败 " & IndexName)
        End Try
        SyncLock IndexCache
            IndexCache(IndexName) = Result
        End SyncLock
        Return Result
    End Function

    ''' <summary>读取并缓存一张图片，失败返回 Nothing。返回值已冻结，可以跨线程使用。</summary>
    Public Function LoadBitmapCached(Path As String) As BitmapSource
        If String.IsNullOrEmpty(Path) Then Return Nothing
        SyncLock BitmapCache
            Dim Cached As BitmapSource = Nothing
            If BitmapCache.TryGetValue(Path, Cached) Then Return Cached
        End SyncLock
        Dim Result As BitmapSource = Nothing
        Try
            Dim Image As New BitmapImage()
            Image.BeginInit()
            'OnLoad：读完就释放文件句柄，玩家在游戏里替换资源包也不会把文件锁住
            Image.CacheOption = BitmapCacheOption.OnLoad
            Image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile
            Image.UriSource = New Uri(Path, UriKind.Absolute)
            Image.EndInit()
            Image.Freeze()
            Result = Image
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：读取图片失败 " & Path)
        End Try
        SyncLock BitmapCache
            BitmapCache(Path) = Result
        End SyncLock
        Return Result
    End Function

#End Region

#Region "全景贴图"

    ''' <summary>资源包里全景的固定路径。</summary>
    Private Const PanoramaFolder As String = "assets/minecraft/textures/gui/title/background/"

    ''' <summary>一套全景的来源：六个面要么在磁盘上，要么在某个资源包（zip）里。</summary>
    Private Class PanoramaSource
        Public Key As String = ""
        Public Files As String() = New String(5) {}
        Public Zip As String = ""
        Public Entries As String() = New String(5) {}
    End Class

    Private _FaceKey As String = ""
    Private _FaceBitmaps As BitmapSource()
    ''' <summary>全景缓存的锁：预热在后台线程跑，进场在 UI 线程读。</summary>
    Private ReadOnly FaceLock As New Object()
    ''' <summary>扫出来的全部全景套装（启动器自带 + 资源包）。</summary>
    Private _Sources As List(Of PanoramaSource)
    ''' <summary>扫描缓存的有效期标记：资源包目录变了才重扫。</summary>
    Private _SourcesStamp As String = ""
    ''' <summary>上一次挑中的那套，用来避免连续两次进到同一张。</summary>
    Private _LastSourceKey As String = ""
    Private ReadOnly FaceRandom As New Random()

    ''' <summary>
    ''' 取得六个面的贴图。预热过就直接给缓存，没有就当场挑一套读出来。
    ''' 一套都没有时返回 Nothing，由调用方回退到自绘背景。
    ''' </summary>
    Public Function GetPanoramaFaces(Instance As McInstance) As BitmapSource()
        SyncLock FaceLock
            If _FaceBitmaps IsNot Nothing Then Return _FaceBitmaps
        End SyncLock
        '没预热（比如刚启动就点全屏）：同步补一次
        WarmPanorama(Instance)
        SyncLock FaceLock
            Return _FaceBitmaps
        End SyncLock
    End Function

    ''' <summary>
    ''' 后台预热一套全景：解码结果留在缓存里，进场时第一帧就是真背景，不会先闪一下渐变底。
    ''' 已经在缓存里就直接返回，不重复读盘。
    ''' </summary>
    Public Sub WarmPanorama(Instance As McInstance)
        Try
            SyncLock FaceLock
                If _FaceBitmaps IsNot Nothing Then Return
            End SyncLock
            Dim Sources As List(Of PanoramaSource) = GetPanoramaSources(Instance)
            If Sources Is Nothing OrElse Sources.Count = 0 Then Return
            '最多试三套：某套读不出来（对象缺失、资源包损坏）就换一套，别直接掉到渐变底
            For Attempt As Integer = 1 To Math.Min(3, Sources.Count)
                Dim Pick As PanoramaSource = PickSource(Sources)
                If Pick Is Nothing Then Continue For
                Dim Faces(5) As BitmapSource
                If String.IsNullOrEmpty(Pick.Zip) Then
                    Dim Ok As Boolean = True
                    For i = 0 To 5
                        Faces(i) = LoadBitmapCached(Pick.Files(i))
                        If Faces(i) Is Nothing Then
                            Ok = False
                            Exit For
                        End If
                    Next
                    If Not Ok Then Continue For
                Else
                    Faces = LoadFacesFromZip(Pick.Zip, Pick.Entries)
                    If Faces Is Nothing Then Continue For
                End If
                '只在内存里留最近一套（1024² × 6 约 24MB），换来源时旧的随缓存回收
                SyncLock FaceLock
                    _FaceKey = Pick.Key
                    _FaceBitmaps = Faces
                End SyncLock
                Return
            Next
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：预热全景失败")
        End Try
    End Sub

    ''' <summary>丢掉缓存：退出休眠页后调用，下次进场换一套新的。</summary>
    Public Sub ClearPanoramaCache()
        SyncLock FaceLock
            _FaceBitmaps = Nothing
            _FaceKey = ""
        End SyncLock
    End Sub

    ''' <summary>随机挑一套，尽量不要和上一次相同。</summary>
    Private Function PickSource(Sources As List(Of PanoramaSource)) As PanoramaSource
        If Sources.Count = 1 Then
            _LastSourceKey = Sources(0).Key
            Return Sources(0)
        End If
        Dim Index As Integer = FaceRandom.Next(Sources.Count)
        If Sources(Index).Key = _LastSourceKey Then Index = (Index + 1) Mod Sources.Count
        _LastSourceKey = Sources(Index).Key
        Return Sources(Index)
    End Function

    ''' <summary>
    ''' 找出所有可用全景。优先用**启动器自带**的那几套（随包发布，不碰游戏目录），
    ''' 再补上玩家装的资源包；自带的一套都没有（旧版启动器 / 文件被删）才回退去扫游戏目录的索引。
    ''' </summary>
    Private Function GetPanoramaSources(Instance As McInstance) As List(Of PanoramaSource)
        Dim Root As String = McFolderSelected
        Dim PackDir As String = If(String.IsNullOrEmpty(Root), "", Root & "resourcepacks")
        Dim Stamp As String = BundledStamp() & "|" & If(String.IsNullOrEmpty(Root), "",
                           Root & "|" & PackDirStamp(PackDir) & "|" & PackDirStamp(Root & "assets\indexes"))
        If _SourcesStamp = Stamp AndAlso _Sources IsNot Nothing Then Return _Sources

        Dim Result As New List(Of PanoramaSource)
        Dim Seen As New HashSet(Of String)

        '1. 启动器自带：Resources\SleepPanorama\<版本>\panorama_0..5.png
        Dim BuiltIn As List(Of PanoramaSource) = GetBundledSources()
        If BuiltIn IsNot Nothing Then Result.AddRange(BuiltIn)

        '2. 资源包：文件夹和 zip 都认，缺任何一个面就不算一套（玩家自己装的，有就加）
        Dim PackCount As Integer = 0
        Try
            If Not String.IsNullOrEmpty(Root) AndAlso IO.Directory.Exists(PackDir) Then
                For Each Item As String In IO.Directory.GetFileSystemEntries(PackDir)
                    Dim Src As PanoramaSource = ProbeResourcePack(Item)
                    If Src IsNot Nothing Then
                        Result.Add(Src)
                        PackCount += 1
                    End If
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：扫描资源包失败")
        End Try

        '3. 兜底：没有自带套装时才去扫游戏目录（当前实例 + 其它版本索引）
        Dim IndexCount As Integer = 0
        Dim InstanceOk As Boolean = False
        If Result.Count = 0 AndAlso Not String.IsNullOrEmpty(Root) Then
            Dim Vanilla As New PanoramaSource
            Dim Complete As Boolean = True
            For i = 0 To 5
                Vanilla.Files(i) = ResolveAssetPath(Instance, PanoramaFacePaths(i))
                If Vanilla.Files(i) Is Nothing Then
                    Complete = False
                    Exit For
                End If
            Next
            If Complete Then
                Vanilla.Key = "assets|" & String.Join("|", Vanilla.Files)
                Seen.Add(SignatureOf(Vanilla.Files))
                Result.Add(Vanilla)
                InstanceOk = True
            End If
            Try
                IndexCount = AddIndexSources(Result, Seen, MaxPanoramaSources)
            Catch ex As Exception
                Logger.Warn(ex, "休眠页：扫描版本索引失败")
            End Try
        End If

        _Sources = Result
        _SourcesStamp = Stamp
        Return Result
    End Function

    ''' <summary>一次最多收多少套，够用又不会把启动盘读爆。</summary>
    Private Const MaxPanoramaSources As Integer = 8

    ''' <summary>自带全景所在的目录（相对启动器 exe）。</summary>
    Private ReadOnly Property BundledDir As String
        Get
            Return IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\SleepPanorama")
        End Get
    End Property

    ''' <summary>自带全景目录的失效标记：目录改动时间 + 套装数。</summary>
    Private Function BundledStamp() As String
        Try
            Dim Dir As String = BundledDir
            If Not IO.Directory.Exists(Dir) Then Return "none"
            Return New IO.DirectoryInfo(Dir).LastWriteTimeUtc.Ticks.ToString() & ":" & IO.Directory.GetDirectories(Dir).Length.ToString()
        Catch
            Return "err"
        End Try
    End Function

    ''' <summary>
    ''' 启动器自带的全景套装：Resources\SleepPanorama 下每个子文件夹一套，
    ''' 文件夹名就是来源版本（1.12 / 1.16 / 1.19 …），只用来在日志里分辨。
    ''' </summary>
    Private Function GetBundledSources() As List(Of PanoramaSource)
        Try
            Dim Dir As String = BundledDir
            If Not IO.Directory.Exists(Dir) Then Return Nothing
            Dim Result As New List(Of PanoramaSource)
            For Each Folder As String In IO.Directory.GetDirectories(Dir)
                Dim Src As New PanoramaSource
                Dim Complete As Boolean = True
                For i = 0 To 5
                    Dim File As String = IO.Path.Combine(Folder, "panorama_" & i & ".jpg")
                    If Not FileUtils.Exists(File) Then
                        Complete = False
                        Exit For
                    End If
                    Src.Files(i) = File
                Next
                If Not Complete Then Continue For
                Src.Key = "builtin|" & IO.Path.GetFileName(Folder)
                Result.Add(Src)
            Next
            Return Result
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：读取自带全景失败")
            Return Nothing
        End Try
    End Function

    ''' <summary>六个面的对象名拼起来当去重签名（对象名就是哈希，同一套图必然同名）。</summary>
    Private Function SignatureOf(Files As String()) As String
        Dim Parts(5) As String
        For i = 0 To 5
            Parts(i) = If(Files(i) Is Nothing, "", IO.Path.GetFileName(Files(i)))
        Next
        Return String.Join("|", Parts)
    End Function

    ''' <summary>
    ''' 扫 assets\indexes 下的所有索引，把互不相同的全景各收一套。
    ''' 索引文件越大版本越新，先扫大的，这样优先拿到新版本的画面。
    ''' </summary>
    Private Function AddIndexSources(Result As List(Of PanoramaSource), Seen As HashSet(Of String), Limit As Integer) As Integer
        Dim Added As Integer = 0
        Dim IndexDir As String = McFolderSelected & "assets\indexes"
        If Not IO.Directory.Exists(IndexDir) Then Return 0
        Dim Files As New List(Of IO.FileInfo)
        Files.AddRange(New IO.DirectoryInfo(IndexDir).GetFiles("*.json"))
        Files.Sort(Function(A, B) B.Length.CompareTo(A.Length))
        For Each File As IO.FileInfo In Files
            If Result.Count >= Limit Then Exit For
            Dim IndexName As String = IO.Path.GetFileNameWithoutExtension(File.Name)
            Dim Hashes As String() = PanoramaHashesInIndex(IndexName)
            If Hashes Is Nothing Then Continue For
            Dim Key As String = String.Join("|", Hashes)
            If Seen.Contains(Key) Then Continue For
            Dim Src As New PanoramaSource
            Dim Ok As Boolean = True
            For i = 0 To 5
                Dim Path As String = McFolderSelected & "assets\objects\" & Hashes(i).Substring(0, 2) & "\" & Hashes(i)
                If Not FileUtils.Exists(Path) Then
                    Ok = False
                    Exit For
                End If
                Src.Files(i) = Path
            Next
            If Not Ok Then Continue For
            Seen.Add(Key)
            Src.Key = "index|" & IndexName
            Result.Add(Src)
            Added += 1
        Next
        Return Added
    End Function

    ''' <summary>
    ''' 读一个索引里六个全景面的哈希；缺任何一个就返回 Nothing。
    ''' 这里单独解析、读完即弃，不走 GetIndexObjects 的缓存，免得十几个索引的 JSON 全留在内存里。
    ''' </summary>
    Private Function PanoramaHashesInIndex(IndexName As String) As String()
        Try
            Dim File As String = McFolderSelected & "assets\indexes\" & IndexName & ".json"
            If Not FileUtils.Exists(File) Then Return Nothing
            Dim Json As JObject = FileUtils.ReadAsJson(File)
            If Json Is Nothing Then Return Nothing
            Dim Objects As JObject = TryCast(Json("objects"), JObject)
            If Objects Is Nothing Then Return Nothing
            Dim Result(5) As String
            For i = 0 To 5
                Dim Entry As JToken = Objects(PanoramaFacePaths(i))
                If Entry Is Nothing OrElse Entry("hash") Is Nothing Then Return Nothing
                Result(i) = Entry("hash").ToString
            Next
            Return Result
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>资源包目录的失效标记：目录改动时间 + 条目数。</summary>
    Private Function PackDirStamp(PackDir As String) As String
        Try
            If Not IO.Directory.Exists(PackDir) Then Return "none"
            Dim Info As New IO.DirectoryInfo(PackDir)
            Return Info.LastWriteTimeUtc.Ticks.ToString() & ":" & IO.Directory.GetFileSystemEntries(PackDir).Length.ToString()
        Catch
            Return "err"
        End Try
    End Function

    ''' <summary>看一个资源包（文件夹或 zip）里有没有全套全景；有就返回来源描述。</summary>
    Private Function ProbeResourcePack(Path As String) As PanoramaSource
        Try
            If IO.Directory.Exists(Path) Then
                Dim Src As New PanoramaSource
                For i = 0 To 5
                    Dim File As String = IO.Path.Combine(Path, (PanoramaFolder & "panorama_" & i & ".png").Replace("/"c, IO.Path.DirectorySeparatorChar))
                    If Not FileUtils.Exists(File) Then Return Nothing
                    Src.Files(i) = File
                Next
                Src.Key = "dir|" & Path
                Return Src
            End If
            If Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) AndAlso FileUtils.Exists(Path) Then
                Using Archive As ZipArchive = ZipFile.OpenRead(Path)
                    Dim Src As New PanoramaSource With {.Zip = Path}
                    For i = 0 To 5
                        Dim Wanted As String = PanoramaFolder & "panorama_" & i & ".png"
                        Dim Found As String = Nothing
                        For Each Entry As ZipArchiveEntry In Archive.Entries
                            If String.Equals(Entry.FullName, Wanted, StringComparison.OrdinalIgnoreCase) Then
                                Found = Entry.FullName
                                Exit For
                            End If
                        Next
                        If Found Is Nothing Then Return Nothing
                        Src.Entries(i) = Found
                    Next
                    Src.Key = "zip|" & Path
                    Return Src
                End Using
            End If
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：检查资源包失败 " & Path)
        End Try
        Return Nothing
    End Function

    ''' <summary>从资源包里读六个面。整个包只打开一次。</summary>
    Private Function LoadFacesFromZip(ZipPath As String, Entries As String()) As BitmapSource()
        Try
            Using Archive As ZipArchive = ZipFile.OpenRead(ZipPath)
                Dim Faces(5) As BitmapSource
                For i = 0 To 5
                    Dim Entry As ZipArchiveEntry = Archive.GetEntry(Entries(i))
                    If Entry Is Nothing Then Return Nothing
                    Using Data As IO.Stream = Entry.Open()
                        '压缩包的流不可定位，直接塞给 BitmapImage 会导致它无法 Freeze（跨线程就不安全了），
                        '所以先整段读进内存流
                        Dim Buffer As New IO.MemoryStream()
                        Data.CopyTo(Buffer)
                        Buffer.Position = 0
                        Dim Image As New BitmapImage()
                        Image.BeginInit()
                        Image.CacheOption = BitmapCacheOption.OnLoad
                        Image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile
                        Image.StreamSource = Buffer
                        Image.EndInit()
                        Image.Freeze()
                        Faces(i) = Image
                    End Using
                Next
                Return Faces
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：读取资源包全景失败 " & ZipPath)
            Return Nothing
        End Try
    End Function

#End Region

#Region "位图字体"

    Private _FontKey As String = Nothing
    Private _FontAttempted As Boolean = False
    Private _FontAtlas As BitmapSource
    Private _FontGlyphs As BitmapSource()
    Private _FontAdvances As Integer()
    Private _FontCell As Integer = 8

    ''' <summary>位图字体是否可用。可用时文本控件按图集逐字绘制，否则退回系统字体。</summary>
    Public ReadOnly Property FontReady As Boolean
        Get
            Return _FontGlyphs IsNot Nothing AndAlso _FontAdvances IsNot Nothing
        End Get
    End Property

    ''' <summary>256 个字符各自的字形（图集里的一格），索引即 ASCII 码。</summary>
    Public ReadOnly Property FontGlyphs As BitmapSource()
        Get
            Return _FontGlyphs
        End Get
    End Property

    ''' <summary>
    ''' 256 个字符各自的推进宽度，单位是图集字格的像素。
    ''' 与游戏内一致：取最右侧有笔画的那一列 + 1；空字形按 4 处理（空格即 4）。
    ''' </summary>
    Public ReadOnly Property FontAdvances As Integer()
        Get
            Return _FontAdvances
        End Get
    End Property

    ''' <summary>图集里单个字格的边长（原版为 8）。</summary>
    Public ReadOnly Property FontCellSize As Integer
        Get
            Return _FontCell
        End Get
    End Property

    ''' <summary>
    ''' 加载（或切换）位图字体图集。同一路径只解析一次。
    ''' 现代版本的资源里已经没有 ascii.png（1.13 起字体改由 unifont 等提供方组成），
    ''' 取不到时改用启动器内置的像素字，保证时钟与 YUANQI 在任何安装上都是同一套观感。
    ''' </summary>
    Public Sub EnsureFont(Instance As McInstance)
        Dim Path As String = ResolveAssetPath(Instance, AsciiFontPath)
        '用「是否尝试过」而不是「路径是否相同」判断：两者都取不到（Nothing）时也必须真的加载过一次
        If _FontAttempted AndAlso Path = _FontKey Then Return
        _FontAttempted = True
        _FontKey = Path
        _FontAtlas = Nothing
        _FontGlyphs = Nothing
        _FontAdvances = Nothing

        If Path Is Nothing Then
            BuildBuiltInFont()
            Logger.Info("休眠页：未找到游戏位图字体，改用内置像素字")
            Return
        End If

        Dim Loaded As Boolean = False
        Try
            Dim Atlas As BitmapSource = LoadBitmapCached(Path)
            If Atlas Is Nothing Then Return
            '统一转成 Bgra32 才能稳定地按 Alpha 判断笔画
            Dim Converted As BitmapSource = Atlas
            If Atlas.Format <> PixelFormats.Bgra32 Then
                Dim Format As New FormatConvertedBitmap(Atlas, PixelFormats.Bgra32, Nothing, 0)
                Format.Freeze()
                Converted = Format
            End If
            Dim Cell As Integer = Math.Max(1, Converted.PixelWidth \ 16)
            Dim Stride As Integer = Converted.PixelWidth * 4
            Dim Pixels(Stride * Converted.PixelHeight - 1) As Byte
            Converted.CopyPixels(Pixels, Stride, 0)

            Dim Glyphs(255) As BitmapSource
            Dim Advances(255) As Integer
            For Code = 0 To 255
                Dim Col As Integer = Code And 15
                Dim Row As Integer = Code >> 4
                Dim X0 As Integer = Col * Cell
                Dim Y0 As Integer = Row * Cell
                Dim W As Integer = Math.Min(Cell, Converted.PixelWidth - X0)
                Dim H As Integer = Math.Min(Cell, Converted.PixelHeight - Y0)
                If W <= 0 OrElse H <= 0 Then
                    Advances(Code) = 4
                    Continue For
                End If
                Dim Crop As New CroppedBitmap(Converted, New Int32Rect(X0, Y0, W, H))
                Crop.Freeze()
                Glyphs(Code) = Crop
                '推进宽度 = 最右侧有笔画的那一列 + 1（与游戏内按图集推算宽度的做法一致）
                Dim Last As Integer = -1
                For X = 0 To W - 1
                    For Y = 0 To H - 1
                        If Pixels((Y0 + Y) * Stride + (X0 + X) * 4 + 3) > 16 Then
                            Last = X
                            Exit For
                        End If
                    Next
                Next
                Advances(Code) = If(Last < 0, 4, Last + 1)
            Next
            _FontCell = Cell
            _FontAtlas = Atlas
            _FontGlyphs = Glyphs
            _FontAdvances = Advances
            Loaded = True
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：解析位图字体图集失败")
            _FontGlyphs = Nothing
            _FontAdvances = Nothing
        End Try
    End Sub

    ''' <summary>内置像素字的一个字形：5 格宽、7 格高，'#' 为笔画。</summary>
    Private Structure PixelGlyph
        Public Ch As Char
        Public Advance As Integer
        Public Rows As String()
    End Structure

    Private Function G(Ch As Char, Advance As Integer, Rows As String) As PixelGlyph
        Return New PixelGlyph With {.Ch = Ch, .Advance = Advance, .Rows = Rows.Split("/"c)}
    End Function

    ''' <summary>
    ''' 内置像素字的字形表。风格对齐游戏自带的位图字：5 格笔画 + 1 格间距，冒号句点只占 2 格。
    ''' 只做页面用得到的字符，其余留空（只占位不画）。
    ''' </summary>
    Private ReadOnly PixelGlyphData As PixelGlyph() = {
        G(" "c, 4, "....."),
        G("0"c, 6, ".###./#...#/#..##/#.#.#/##..#/#...#/.###."),
        G("1"c, 6, "..#../.##../..#../..#../..#../..#../.###."),
        G("2"c, 6, ".###./#...#/....#/...#./..#../.#.../#####"),
        G("3"c, 6, "####./....#/....#/.###./....#/....#/####."),
        G("4"c, 6, "...#./..##./.#.#./#..#./#####/...#./...#."),
        G("5"c, 6, "#####/#..../####./....#/....#/#...#/.###."),
        G("6"c, 6, "..##./.#.../#..../####./#...#/#...#/.###."),
        G("7"c, 6, "#####/....#/...#./..#../..#../.#.../.#..."),
        G("8"c, 6, ".###./#...#/#...#/.###./#...#/#...#/.###."),
        G("9"c, 6, ".###./#...#/#...#/.####/....#/...#./.##.."),
        G("A"c, 6, ".###./#...#/#...#/#####/#...#/#...#/#...#"),
        G("B"c, 6, "####./#...#/#...#/####./#...#/#...#/####."),
        G("C"c, 6, ".###./#...#/#..../#..../#..../#...#/.###."),
        G("D"c, 6, "####./#...#/#...#/#...#/#...#/#...#/####."),
        G("E"c, 6, "#####/#..../#..../####./#..../#..../#####"),
        G("F"c, 6, "#####/#..../#..../####./#..../#..../#...."),
        G("G"c, 6, ".###./#...#/#..../#.###/#...#/#...#/.####"),
        G("H"c, 6, "#...#/#...#/#...#/#####/#...#/#...#/#...#"),
        G("I"c, 6, ".###./..#../..#../..#../..#../..#../.###."),
        G("J"c, 6, "...##/....#/....#/....#/#...#/#...#/.###."),
        G("K"c, 6, "#...#/#..#./#.#../##.../#.#../#..#./#...#"),
        G("L"c, 6, "#..../#..../#..../#..../#..../#..../#####"),
        G("M"c, 6, "#...#/##.##/#.#.#/#.#.#/#...#/#...#/#...#"),
        G("N"c, 6, "#...#/##..#/#.#.#/#..##/#...#/#...#/#...#"),
        G("O"c, 6, ".###./#...#/#...#/#...#/#...#/#...#/.###."),
        G("P"c, 6, "####./#...#/#...#/####./#..../#..../#...."),
        G("Q"c, 6, ".###./#...#/#...#/#...#/#.#.#/#..#./.##.#"),
        G("R"c, 6, "####./#...#/#...#/####./#.#../#..#./#...#"),
        G("S"c, 6, ".####/#..../#..../.###./....#/....#/####."),
        G("T"c, 6, "#####/..#../..#../..#../..#../..#../..#.."),
        G("U"c, 6, "#...#/#...#/#...#/#...#/#...#/#...#/.###."),
        G("V"c, 6, "#...#/#...#/#...#/#...#/#...#/.#.#./..#.."),
        G("W"c, 6, "#...#/#...#/#...#/#.#.#/#.#.#/##.##/#...#"),
        G("X"c, 6, "#...#/#...#/.#.#./..#../.#.#./#...#/#...#"),
        G("Y"c, 6, "#...#/#...#/.#.#./..#../..#../..#../..#.."),
        G("Z"c, 6, "#####/....#/...#./..#../.#.../#..../#####"),
        G(":"c, 2, "...../#..../...../...../#..../...../....."),
        G("."c, 2, "...../...../...../...../...../...../##..."),
        G(","c, 2, "...../...../...../...../...../##.../#...."),
        G("-"c, 6, "...../...../...../#####/...../...../....."),
        G("_"c, 6, "...../...../...../...../...../...../#####"),
        G("/"c, 6, "....#/....#/...#./..#../.#.../#..../#...."),
        G("!"c, 2, "#..../#..../#..../#..../#..../...../#...."),
        G("?"c, 6, ".###./#...#/....#/...#./..#../...../..#.."),
        G("#"c, 6, ".#.#./.#.#./#####/.#.#./#####/.#.#./.#.#."),
        G("+"c, 6, "...../..#../..#../#####/..#../..#../....."),
        G("="c, 6, "...../...../#####/...../#####/...../....."),
        G("("c, 4, "...#./..#../.#.../.#.../.#.../..#../...#."),
        G(")"c, 4, ".#.../..#../...#./...#./...#./..#../.#..."),
        G("["c, 4, ".###./.#.../.#.../.#.../.#.../.#.../.###."),
        G("]"c, 4, ".###./...#./...#./...#./...#./...#./.###."),
        G("'"c, 2, "#..../#..../...../...../...../...../....."),
        G(""""c, 4, "#.#../#.#../...../...../...../...../....."),
        G("%"c, 6, "##..#/##..#/...#./..#../.#.../#..##/#..##")}

    ''' <summary>
    ''' 用内置字形表拼出一张和游戏图集同格式（16×16 格、每格 8×8）的位图，
    ''' 这样上层的文本控件完全不用区分字体来源。
    ''' </summary>
    Private Sub BuildBuiltInFont()
        Const Cell As Integer = 8
        Const AtlasSize As Integer = 128
        Dim Stride As Integer = AtlasSize * 4
        Dim Pixels(Stride * AtlasSize - 1) As Byte
        Dim Advances(255) As Integer
        For Code = 0 To 255
            Advances(Code) = 4
        Next

        For Each Item As PixelGlyph In PixelGlyphData
            Dim Code As Integer = AscW(Item.Ch)
            If Code < 0 OrElse Code > 127 Then Continue For
            Dim Col As Integer = Code And 15
            Dim Row As Integer = Code >> 4
            For Y = 0 To Math.Min(Item.Rows.Length, Cell) - 1
                Dim Line As String = Item.Rows(Y)
                For X = 0 To Math.Min(Line.Length, Cell) - 1
                    If Line(X) = "#"c Then
                        Dim Offset As Integer = (Row * Cell + Y) * Stride + (Col * Cell + X) * 4
                        Pixels(Offset) = 255
                        Pixels(Offset + 1) = 255
                        Pixels(Offset + 2) = 255
                        Pixels(Offset + 3) = 255
                    End If
                Next
            Next
            Advances(Code) = Item.Advance
        Next
        '小写字母复用大写字形：游戏字体里小写是独立的，内置字只求整体观感一致
        For Code = AscW("a"c) To AscW("z"c)
            Dim Upper As Integer = Code - 32
            Dim Col As Integer = Code And 15
            Dim Row As Integer = Code >> 4
            Dim SrcCol As Integer = Upper And 15
            Dim SrcRow As Integer = Upper >> 4
            For Y = 0 To Cell - 1
                For X = 0 To Cell - 1
                    Dim Src As Integer = (SrcRow * Cell + Y) * Stride + (SrcCol * Cell + X) * 4
                    If Pixels(Src + 3) > 0 Then
                        Dim Dst As Integer = (Row * Cell + Y) * Stride + (Col * Cell + X) * 4
                        Pixels(Dst) = 255
                        Pixels(Dst + 1) = 255
                        Pixels(Dst + 2) = 255
                        Pixels(Dst + 3) = 255
                    End If
                Next
            Next
            Advances(Code) = Advances(Upper)
        Next

        '推进宽度按「最后一列笔画 + 1 列空白」重新算一遍。
        '手写表里的 Advance 只当空字形（空格）的兜底：冒号、句点的笔画本来就只有一两格，
        '照抄表里的 2 会让下一个字形直接贴着它落下来 —— 时钟 20:08 的「:」和「08」就是这么粘住的。
        For Code = 0 To 255
            Dim Col As Integer = Code And 15
            Dim Row As Integer = Code >> 4
            Dim Last As Integer = -1
            For X = 0 To Cell - 1
                For Y = 0 To Cell - 1
                    If Pixels((Row * Cell + Y) * Stride + (Col * Cell + X) * 4 + 3) > 0 Then
                        Last = X
                        Exit For
                    End If
                Next
            Next
            If Last >= 0 Then Advances(Code) = Math.Min(Cell, Last + 2)
        Next

        Dim Atlas As BitmapSource = BitmapSource.Create(AtlasSize, AtlasSize, 96, 96, PixelFormats.Pbgra32, Nothing, Pixels, Stride)
        Atlas.Freeze()
        Dim Glyphs(255) As BitmapSource
        For Code = 0 To 255
            Dim Col As Integer = Code And 15
            Dim Row As Integer = Code >> 4
            Dim Crop As New CroppedBitmap(Atlas, New Int32Rect(Col * Cell, Row * Cell, Cell, Cell))
            Crop.Freeze()
            Glyphs(Code) = Crop
        Next
        _FontCell = Cell
        _FontAtlas = Atlas
        _FontGlyphs = Glyphs
        _FontAdvances = Advances
    End Sub

#End Region

End Module
