Imports System.Runtime.InteropServices
Imports System.Windows.Media

''' <summary>
''' 种子地图核心：封装 cubiomes 原生库（MIT，源码见 PCL-src\cubiomes-src），
''' 按世界种子确定性计算 Minecraft Java 版（1.8 – 1.21）的生物群系与结构位置。
''' 原生接口内部持有单例生成器，所有调用经 NativeLock 串行化，可在后台线程使用。
''' 地图以正方形采样网格（瓦片）为单位按需生成，支持无限平移。
''' </summary>
Public Module ModuleSeedMap

    '===============================
    '  P/Invoke（cubiomes.dll 随程序分发）
    '===============================

    Private Const DllName As String = "cubiomes.dll"

    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_mc_newest() As Integer
    End Function

    ''' <summary>可选择的版本数量 / 第 index 个版本的 cubiomes 版本 id。</summary>
    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_ver_count() As Integer
    End Function
    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_ver_id(index As Integer) As Integer
    End Function

    ''' <summary>估计主世界出生点（用于初始视野居中）。</summary>
    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Sub cb_spawn(mc As Integer, seed As ULong, <Out> ByRef x As Integer, <Out> ByRef z As Integer)
    End Sub

    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Sub cb_setup(mc As Integer, dimId As Integer, seed As ULong)
    End Sub

    ''' <summary>主世界：两遍采样（近似地表高度 + 高度处群系）生成网格；≤1.17 自动退化为分层算法。step 为 1:4 单位步长。</summary>
    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_map_surface(x4 As Integer, z4 As Integer, w As Integer, h As Integer, step4 As Integer, <Out> ids As Integer()) As Integer
    End Function

    ''' <summary>任意维度：按步进逐点采样固定 y 的群系网格（y 为 1:4 单位）。</summary>
    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_sample_grid(scale As Integer, x As Integer, z As Integer, w As Integer, h As Integer, step4 As Integer, y As Integer, <Out> ids As Integer()) As Integer
    End Function

    ''' <summary>地表高程网格：与 cb_sample_grid 同尺度（1:4），每格一个方块高度。</summary>
    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_heightmap(x4 As Integer, z4 As Integer, w As Integer, h As Integer, step4 As Integer, <Out> heights As Single()) As Integer
    End Function

    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_biome_at(scale As Integer, x As Integer, y As Integer, z As Integer) As Integer
    End Function

    ''' <summary>扫描区域内的结构（含生物群系校验），返回命中数。</summary>
    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_find_structures(stype As Integer, mc As Integer, seed As ULong, minX As Integer, minZ As Integer, maxX As Integer, maxZ As Integer,
                                        <Out> outX As Integer(), <Out> outZ As Integer(), maxCount As Integer) As Integer
    End Function

    ''' <summary>迭代前 N 个要塞（含生物群系校验的精确位置）。</summary>
    <DllImport(DllName, CallingConvention:=CallingConvention.Cdecl)>
    Private Function cb_find_strongholds(mc As Integer, seed As ULong, <Out> outX As Integer(), <Out> outZ As Integer(), maxCount As Integer) As Integer
    End Function

    ''' <summary>DllImport 在首次调用时解析；构造时试探一次以便尽早暴露缺失。</summary>
    Public ReadOnly IsNativeAvailable As Boolean = TryInitNative()
    Private Function TryInitNative() As Boolean
        Try
            Return cb_mc_newest() > 0
        Catch ex As Exception
            Logger.Warn(ex, "cubiomes.dll 不可用，种子地图功能将无法生成")
            Return False
        End Try
    End Function

    ''' <summary>当前库支持的最新 Java 版本（内部枚举值）。</summary>
    Public ReadOnly CubiomesVersion As Integer = If(IsNativeAvailable, cb_mc_newest(), 0)

    '===============================
    '  生成接口
    '===============================

    Private ReadOnly NativeLock As New Object()

    ''' <summary>估计主世界出生点（方块坐标）。</summary>
    Public Function GetSpawn(Seed As Long, Version As Integer) As Point
        SyncLock NativeLock
            Dim X As Integer = 0, Z As Integer = 0
            cb_spawn(Version, ToUnsignedSeed(Seed), X, Z)
            Return New Point(X, Z)
        End SyncLock
    End Function

    ''' <summary>
    ''' 地表高程网格（用于 3D 立体预览）：与 SampleBiomeGrid 同尺度。
    ''' 返回每格的方块高度；Step4 为每格跨的 1:4 单位（4 方块 = 1）。
    ''' </summary>
    Public Function SampleHeights(Version As Integer, Seed As Long, OriginX4 As Integer, OriginZ4 As Integer,
                                  Cells As Integer, Step4 As Integer) As Single()
        Dim Out(Cells * Cells - 1) As Single
        Dim StepVal As Integer = Math.Max(1, Step4)
        SyncLock NativeLock
            cb_setup(Version, 0, ToUnsignedSeed(Seed)) '高程只在主世界有意义
            cb_heightmap(OriginX4, OriginZ4, Cells, Cells, StepVal, Out)
        End SyncLock
        Return Out
    End Function

    ''' <summary>
    ''' 分带采样的高程网格：每次只算 BandRows 行并回调进度（0–1），用于第一人称等需要加载进度条的场景。
    ''' 结果与 SampleHeights 完全一致。
    ''' </summary>
    Public Function SampleHeightsBanded(Version As Integer, Seed As Long, OriginX4 As Integer, OriginZ4 As Integer,
                                        Cells As Integer, Step4 As Integer, BandRows As Integer,
                                        Progress As Action(Of Double)) As Single()
        Dim Out(Cells * Cells - 1) As Single
        Dim StepVal As Integer = Math.Max(1, Step4)
        BandRows = Math.Max(1, BandRows)
        SyncLock NativeLock
            cb_setup(Version, 0, ToUnsignedSeed(Seed))
            Dim Done As Integer = 0
            Do While Done < Cells
                Dim Rows As Integer = Math.Min(BandRows, Cells - Done)
                Dim Band(Rows * Cells - 1) As Single
                cb_heightmap(OriginX4, OriginZ4 + Done * StepVal, Cells, Rows, StepVal, Band)
                Array.Copy(Band, 0, Out, Done * Cells, Rows * Cells)
                Done += Rows
                Progress?.Invoke(Done / CDbl(Cells))
            Loop
        End SyncLock
        Return Out
    End Function

    ''' <summary>
    ''' 生成一块正方形群系采样网格（cells×cells，行优先，自西北角起）。
    ''' 维度：0 主世界 / 1 下界 / 2 末地；BlocksPerCell 为每格方块数（瓦片层级）。
    ''' </summary>
    Public Function SampleBiomeGrid(Seed As Long, Version As Integer, Dimension As Integer,
                                    OriginX As Integer, OriginZ As Integer, Cells As Integer, BlocksPerCell As Integer) As Integer()
        Dim Ids(Cells * Cells - 1) As Integer
        Dim Step4 As Integer = Math.Max(1, BlocksPerCell \ 4)
        Dim X4 As Integer = CInt(Math.Floor(OriginX / 4.0))
        Dim Z4 As Integer = CInt(Math.Floor(OriginZ / 4.0))
        SyncLock NativeLock
            cb_setup(Version, {0, -1, 1}(Dimension), ToUnsignedSeed(Seed))
            Dim Code As Integer
            If Dimension = 0 Then
                Code = cb_map_surface(X4, Z4, Cells, Cells, Step4, Ids)
            Else
                Dim Y4 As Integer = If(Dimension = 1, 12, 16) '下界 y≈48，末地 y=64
                Code = cb_sample_grid(4, X4, Z4, Cells, Cells, Step4, Y4, Ids)
            End If
            If Code <> 0 Then Throw New Exception($"cubiomes 群系采样失败（代码 {Code}）")
        End SyncLock
        Return Ids
    End Function

    ''' <summary>
    ''' 分带采样的主世界群系网格：每次只算 BandRows 行并回调进度（0–1），结果与 SampleBiomeGrid（主世界）一致。
    ''' </summary>
    Public Function SampleBiomeGridBanded(Seed As Long, Version As Integer,
                                          OriginX As Integer, OriginZ As Integer, Cells As Integer, BlocksPerCell As Integer,
                                          BandRows As Integer, Progress As Action(Of Double)) As Integer()
        Dim Ids(Cells * Cells - 1) As Integer
        Dim Step4 As Integer = Math.Max(1, BlocksPerCell \ 4)
        Dim X4 As Integer = CInt(Math.Floor(OriginX / 4.0))
        Dim Z4 As Integer = CInt(Math.Floor(OriginZ / 4.0))
        BandRows = Math.Max(1, BandRows)
        SyncLock NativeLock
            cb_setup(Version, 0, ToUnsignedSeed(Seed))
            Dim Done As Integer = 0
            Do While Done < Cells
                Dim Rows As Integer = Math.Min(BandRows, Cells - Done)
                Dim Band(Rows * Cells - 1) As Integer
                Dim Code As Integer = cb_map_surface(X4, Z4 + Done * Step4, Cells, Rows, Step4, Band)
                If Code <> 0 Then Throw New Exception($"cubiomes 群系采样失败（代码 {Code}）")
                Array.Copy(Band, 0, Ids, Done * Cells, Rows * Cells)
                Done += Rows
                Progress?.Invoke(Done / CDbl(Cells))
            Loop
        End SyncLock
        Return Ids
    End Function

    ''' <summary>
    ''' 扫描指定方块范围内的结构位置（含生物群系校验）。
    ''' 返回 结构类型 → 位置列表；要塞（StStronghold）仅在主世界扫描前 8 个。
    ''' </summary>
    Public Function ScanStructures(Seed As Long, Version As Integer, Dimension As Integer,
                                   StructTypes As Integer(), MinX As Integer, MinZ As Integer, MaxX As Integer, MaxZ As Integer) As Dictionary(Of Integer, List(Of Point))
        Dim Ret As New Dictionary(Of Integer, List(Of Point))
        SyncLock NativeLock
            cb_setup(Version, {0, -1, 1}(Dimension), ToUnsignedSeed(Seed))
            For Each St In StructTypes
                Dim List As New List(Of Point)
                If St = StStronghold Then
                    If Dimension = 0 Then
                        Dim Hx(7) As Integer, Hz(7) As Integer
                        Dim N = cb_find_strongholds(Version, ToUnsignedSeed(Seed), Hx, Hz, 8)
                        For i = 0 To N - 1
                            If Hx(i) >= MinX AndAlso Hx(i) <= MaxX AndAlso Hz(i) >= MinZ AndAlso Hz(i) <= MaxZ Then
                                List.Add(New Point(Hx(i), Hz(i)))
                            End If
                        Next
                    End If
                ElseIf St >= 0 Then
                    Dim Sx(255) As Integer, Sz(255) As Integer
                    Dim N = cb_find_structures(St, Version, ToUnsignedSeed(Seed), MinX, MinZ, MaxX, MaxZ, Sx, Sz, 256)
                    For i = 0 To Math.Min(N, 256) - 1
                        List.Add(New Point(Sx(i), Sz(i)))
                    Next
                End If
                Ret(St) = List
            Next
        End SyncLock
        Return Ret
    End Function

    '===============================
    '  常量与版本列表
    '===============================

    'cubiomes StructureType 枚举（与 finders.h 对齐，负值为本模块的虚拟类型）
    Public Const StDesertPyramid As Integer = 1
    Public Const StJungleTemple As Integer = 2
    Public Const StSwampHut As Integer = 3
    Public Const StIgloo As Integer = 4
    Public Const StVillage As Integer = 5
    Public Const StOceanRuin As Integer = 6
    Public Const StShipwreck As Integer = 7
    Public Const StMonument As Integer = 8
    Public Const StMansion As Integer = 9
    Public Const StOutpost As Integer = 10
    Public Const StRuinedPortal As Integer = 11
    Public Const StAncientCity As Integer = 13
    Public Const StTreasure As Integer = 14
    Public Const StMineshaft As Integer = 15
    Public Const StFortress As Integer = 18
    Public Const StBastion As Integer = 19
    Public Const StEndCity As Integer = 20
    Public Const StEndGateway As Integer = 21
    Public Const StTrailRuins As Integer = 23
    Public Const StTrialChambers As Integer = 24
    ''' <summary>要塞（Stronghold）在 cubiomes 中使用专用迭代器，以 -1 作为虚拟类型。</summary>
    Public Const StStronghold As Integer = -1

    ''' <summary>可选版本的 cubiomes id（与 VersionNames 一一对应）。</summary>
    Public ReadOnly VersionIds As New List(Of Integer)
    ''' <summary>可选版本的显示名（自新到旧，与 C 侧 VER_LIST 顺序一致；26.x 暂按 1.21 Winter Drop 算法近似）。</summary>
    Public ReadOnly VersionNames As New List(Of String) From {
        "26.4", "26.3", "26.2", "26.1", "26.0",
        "1.21", "1.20", "1.19", "1.18", "1.17", "1.16", "1.15", "1.14",
        "1.13", "1.12", "1.11", "1.10", "1.9", "1.8", "1.7", "1.6",
        "1.5", "1.4", "1.3", "1.2", "1.1", "1.0", "Beta 1.8", "Beta 1.7"}

    '<summary>把网络拉到的版本号并入可选列表：新版本映射到"不大于它的最新受支持版本"的算法 id。</summary>
    ' <returns>列表是否发生变化。</returns>
    Public Function MergeNetVersions(NetNames As IEnumerable(Of String)) As Boolean
        Dim Changed As Boolean = False
        Try
            For Each RawName In NetNames
                Dim Name As String = If(RawName, "").Trim()
                If Name = "" OrElse IndexOfVersionName(Name) >= 0 Then Continue For
                Dim Num As Double = VersionSortKey(Name)
                If Num < 0 Then Continue For   'Beta 之类的名字不参与
                '找到插入位置（列表按新到旧）
                Dim Pos As Integer = VersionNames.Count
                For i = 0 To VersionNames.Count - 1
                    If VersionSortKey(VersionNames(i)) < Num Then
                        Pos = i
                        Exit For
                    End If
                Next
                '算法锚点：取"不大于它的最近版本"，没有就用最新的一个
                Dim Anchor As Integer = If(VersionIds.Count > 0, VersionIds(0), 0)
                For i = 0 To VersionNames.Count - 1
                    Dim Key As Double = VersionSortKey(VersionNames(i))
                    If Key >= 0 AndAlso Key <= Num Then
                        Anchor = VersionIds(i)
                        Exit For
                    End If
                Next
                VersionNames.Insert(Pos, Name)
                VersionIds.Insert(Pos, Anchor)
                Changed = True
            Next
        Catch ex As Exception
            Logger.Warn(ex, "合并种子地图版本列表失败")
        End Try
        Return Changed
    End Function

    ''' <summary>按显示名查找版本下标，找不到返回 -1。</summary>
    Public Function IndexOfVersionName(Name As String) As Integer
        For i = 0 To VersionNames.Count - 1
            If String.Equals(VersionNames(i), Name, StringComparison.OrdinalIgnoreCase) Then Return i
        Next
        Return -1
    End Function

    ''' <summary>把 "1.21" / "26.3" 转成可比较的数值（主.次），Beta 等返回 -1。</summary>
    Private Function VersionSortKey(Name As String) As Double
        Try
            Dim Parts = If(Name, "").Split("."c)
            If Parts.Length < 2 Then Return -1
            Dim Major As Integer, Minor As Integer
            If Not Integer.TryParse(Parts(0), Major) Then Return -1
            If Not Integer.TryParse(Parts(1), Minor) Then Return -1
            Return Major * 1000 + Minor
        Catch
            Return -1
        End Try
    End Function

    '===============================
    '  生物群系表（id → 中文名 / 地图色）
    '===============================

    Public ReadOnly BiomeNames(255) As String
    Public ReadOnly BiomeColors(255) As Color

    Sub New()
        If IsNativeAvailable Then
            For i = 0 To cb_ver_count() - 1
                VersionIds.Add(cb_ver_id(i))
            Next
        End If
        '通用回填
        For i = 0 To 255
            BiomeColors(i) = Color.FromRgb(&H99, &H99, &H99)
        Next
        '主世界 —— 配色取自 seedatlas.net
        Add(0, "海洋", &H19, &H19, &H70)
        Add(1, "平原", &H6A, &H8C, &H3C)
        Add(2, "沙漠", &HF2, &H8C, &H1F)
        Add(3, "风袭丘陵", &H5B, &H5B, &H5B)
        Add(4, "森林", &H28, &H66, &H31)
        Add(5, "针叶林", &H22, &H6B, &H69)
        Add(6, "沼泽", &H16, &HCE, &HAD)
        Add(7, "河流", &H00, &H00, &HFF)
        Add(8, "下界荒地", &H57, &H29, &H2B)
        Add(9, "末地", &HDE, &HDE, &H9C)
        Add(10, "冻洋", &H66, &H72, &HE8)
        Add(11, "冻河", &H9A, &H9C, &HF7)
        Add(12, "雪原", &HFF, &HFF, &HFF)
        Add(13, "雪山", &HAB, &HAB, &HAB)
        Add(14, "蘑菇岛", &HE6, &H25, &HD4)
        Add(15, "蘑菇岛岸", &HBF, &H00, &HFF)
        Add(16, "沙滩", &HF5, &HE6, &H7D)
        Add(17, "沙漠丘陵", &HD2, &H69, &H1E)
        Add(18, "林地丘陵", &H55, &H7F, &H3F)
        Add(19, "针叶林丘陵", &H1E, &H3B, &H2A)
        Add(20, "山地边缘", &H7E, &H7E, &H99)
        Add(21, "丛林", &H5E, &H7D, &H31)
        Add(22, "丛林丘陵", &H1A, &H59, &H1A)
        Add(23, "稀疏丛林", &H6D, &H8A, &H36)
        Add(24, "深海", &H0B, &H0B, &H1A)
        Add(25, "石岸", &H8E, &H8C, &H7C)
        Add(26, "积雪沙滩", &HF2, &HF2, &HD5)
        Add(27, "白桦林", &H4A, &H7B, &H3B)
        Add(28, "白桦林丘陵", &H1F, &H5C, &H24)
        Add(29, "黑森林", &H1B, &H3B, &H1A)
        Add(30, "积雪针叶林", &H37, &H4C, &H52)
        Add(31, "积雪针叶林丘陵", &H26, &H3B, &H42)
        Add(32, "原始松木针叶林", &H57, &H69, &H50)
        Add(33, "原始松木针叶林丘陵", &H3D, &H4A, &H3B)
        Add(34, "风袭森林", &H26, &H5C, &H30)
        Add(35, "热带草原", &HB5, &HA6, &H67)
        Add(36, "热带草原高原", &HA5, &H99, &H68)
        Add(37, "恶地", &HE6, &H54, &H1A)
        Add(38, "疏林恶地", &H9E, &H8D, &H68)
        Add(39, "恶地高原", &HA3, &H70, &H5F)
        Add(40, "末地小型岛屿", &HA8, &HA8, &H6C)
        Add(41, "末地内陆", &HD3, &HD3, &H89)
        Add(42, "末地高地", &HDB, &HDB, &H9C)
        Add(43, "末地荒岛", &HC4, &HC4, &H8A)
        Add(44, "暖水海洋", &H00, &H00, &HCD)
        Add(45, "温水海洋", &H1C, &H39, &HBB)
        Add(46, "冷水海洋", &H1A, &H2B, &H5C)
        Add(47, "深暖水海洋", &H0A, &H0A, &H31)
        Add(48, "深温水海洋", &H19, &H19, &H71)
        Add(49, "深冷水海洋", &H0B, &H0C, &H1B)
        Add(50, "深冻洋", &H25, &H2A, &H69)
        Add(127, "虚空", &H0E, &H0A, &H18)
        '变种（1.18 前的 id，仍可能出现）
        Add(129, "向日葵平原", &HB5, &HD3, &H64)
        Add(130, "沙漠湖泊", &HFF, &HA5, &H00)
        Add(131, "风袭砂砾丘陵", &H86, &H86, &H8A)
        Add(132, "繁花森林", &H4D, &HA7, &H45)
        Add(133, "针叶林山地", &H14, &H73, &H6D)
        Add(134, "沼泽丘陵", &H3E, &HE8, &HCF)
        Add(140, "冰刺平原", &HB0, &HDD, &HE0)
        Add(149, "丛林变种", &H6B, &H96, &H24)
        Add(151, "丛林边缘变种", &H6E, &H96, &H25)
        Add(155, "高白桦林", &H5C, &H9A, &H7A)
        Add(156, "高白桦丘陵", &H2A, &H7A, &H61)
        Add(157, "黑森林丘陵", &H1F, &H40, &H1D)
        Add(158, "积雪针叶林山地", &H2C, &H5B, &H5B)
        Add(160, "原始云杉针叶林", &H6B, &H73, &H62)
        Add(161, "原始云杉针叶林丘陵", &H5B, &H66, &H49)
        Add(162, "砂砾山地变种", &H7A, &H7F, &H85)
        Add(163, "风袭热带草原", &HE8, &HE3, &H9B)
        Add(164, "碎裂热带草原高原", &HC1, &HB4, &H83)
        Add(165, "侵蚀恶地", &HF4, &H65, &H3C)
        Add(166, "疏林恶地高原变种", &H9F, &H89, &H66)
        Add(167, "恶地高原变种", &HD9, &H99, &H7B)
        '1.14+
        Add(168, "竹林", &H80, &H9E, &H2E)
        Add(169, "竹林丘陵", &H4B, &H75, &H25)
        '1.16 下界
        Add(170, "灵魂沙峡谷", &H58, &H47, &H33)
        Add(171, "绯红森林", &H7E, &H2F, &H3A)
        Add(172, "诡异森林", &H23, &H55, &H5A)
        Add(173, "玄武岩三角洲", &H4C, &H4C, &H55)
        '1.17 洞穴
        Add(174, "钟乳石洞穴", &HA1, &H8D, &H7D)
        Add(175, "繁茂洞穴", &HEA, &HA1, &H43)
        '1.18 山地群系
        Add(177, "草甸", &H75, &H9A, &H54)
        Add(178, "雪林", &H84, &HA6, &HA6)
        Add(179, "积雪山坡", &HD4, &HE4, &HF0)
        Add(180, "尖峭山峰", &HA6, &HA6, &HA6)
        Add(181, "霜冻山峰", &HBF, &HBD, &HF2)
        Add(182, "裸岩山峰", &HCD, &HCD, &HCD)
        '1.19+
        Add(183, "深暗之域", &H00, &H00, &H00)
        Add(184, "红树林沼泽", &H36, &HCF, &H9E)
        Add(185, "樱花树林", &HF4, &HBD, &HE4)
        '1.21 冬季掉落
        Add(186, "苍白花园", &H6C, &H6A, &H8D)
        '旧版本色值补充（无 seedatlas 对应，保持中性）
        Add(18, "林地丘陵", &H55, &H7F, &H3F)
    End Sub

    Private Sub Add(Id As Integer, Name As String, R As Byte, G As Byte, B As Byte)
        BiomeNames(Id) = Name
        BiomeColors(Id) = Color.FromRgb(R, G, B)
    End Sub

    ''' <summary>获取群系中文名；未知 id 回退为编号。</summary>
    Public Function BiomeName(Id As Integer) As String
        If Id >= 0 AndAlso Id <= 255 AndAlso BiomeNames(Id) IsNot Nothing Then Return BiomeNames(Id)
        Return $"未知群系 ({Id})"
    End Function

    ''' <summary>
    ''' Long → ULong 的按位重解释。
    ''' 直接用 CULng(Seed) 时，负数种子（存档里很常见）会抛 OverflowException，
    ''' 表现为「种子地图不显示地形」（瓦片任务全部失败）。这里按位转换，负数种子也能正常生成。
    ''' </summary>
    Private Function ToUnsignedSeed(Seed As Long) As ULong
        If Seed >= 0 Then Return CULng(Seed)
        Return CULng(Seed And &H7FFFFFFFFFFFFFFF) Or &H8000000000000000UL
    End Function

End Module
