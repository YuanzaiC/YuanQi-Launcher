Imports System.Collections.Concurrent
Imports System.Windows.Threading
Imports System.Windows.Media.Animation
Imports System.Windows.Controls.Primitives
Imports System.Windows.Media
Imports System.Windows.Media.Effects
Imports System.Windows.Media.Imaging
Imports System.Windows.Shapes

''' <summary>
''' 种子地图：模仿 seedatlas.net 的世界种子地图浏览页。
''' 通过 cubiomes 原生库（ModuleSeedMap）按种子计算真实生物群系与结构位置。
''' 地图无边界的瓦片流式加载：拖到哪、生成到哪（64×64 采样格的瓦片按需生成，视野越远层级越粗）；
''' 结构按视野中心 ±2560 方块滚动扫描；支持 1.8 – 1.21 版本切换与三维度切换。
''' </summary>
Public Class PageSeedMap

    '===============================
    '  常量与状态
    '===============================

    '地图坐标系：1 地图单位 = 8 方块，世界原点在单位 (0,0)
    Private Const BlocksPerUnit As Double = 8
    '瓦片：64×64 采样格；层级 = 每格方块数（8 / 32 / 128）
    Private Const TileCells As Integer = 64
    Private ReadOnly Levels As Integer() = {4, 8, 16, 32, 64, 128}

    Private SeedValue As Long = 7906073100618442319
    Private VersionIndex As Integer = 1 '默认 26.3（按 1.21 Winter Drop 算法近似）
    Private Dimension As Integer = 0 '0 主世界 / 1 下界 / 2 末地
    Private Zoom As Double = 4
    Private InitialZoom As Double = 0
    Private GeneratedOnce As Boolean = False
    Private IsDragging As Boolean = False
    Private DragLast As Point

    '结构扫描窗口（以视野中心为圆心的边长，方块）
    Private Const ScanRadius As Integer = 2560
    Private LastScanCenter As New Point(Double.NaN, Double.NaN)

    '地形筛选：选中的群系 id（空 = 不过滤，全部不透明显示）
    Private SelectedBiomes As New HashSet(Of Integer)
    '地形面板行：群系 id → 行控件（用于刷新选中态）
    Private BiomeRows As New Dictionary(Of Integer, Border)
    Private BiomeRowIds As New List(Of Integer)

    ''' <summary>结构索引（当前维度清单）→ 生成位置（方块坐标）。</summary>
    Private StructPoints As New List(Of List(Of Point))

    '结构定义（名称 / 标记色 / cubiomes 结构类型 / 是否显示 / 图标资源名，图标取自 seedatlas.net）
    Private Class StructDef
        Public Name As String
        Public Color As Color
        Public SType As Integer
        Public Count As Integer
        Public Enabled As Boolean = False
        ''' <summary>该维度默认显示的结构（主世界村庄 / 下界要塞 / 末地城）。</summary>
        Public IsDefault As Boolean = False
        Public IconKey As String
        ''' <summary>cubiomes 是否支持定位该结构（地牢/怪物房不支持：随洞穴随机生成，无法由种子推算）。</summary>
        Public Supported As Boolean = True
    End Class
    '按维度分组：0 主世界 / 1 下界 / 2 末地；Structs 指向当前维度的列表
    Private ReadOnly StructLists As New List(Of List(Of StructDef))
    Private Structs As List(Of StructDef)
    Private StructCountLabels As New List(Of TextBlock)
    Private VersionButtons As New List(Of Button)

    Private Shared Function ClampD(Value As Double, Min As Double, Max As Double) As Double
        If Min > Max Then Dim T = Min : Min = Max : Max = T
        If Value < Min Then Return Min
        If Value > Max Then Return Max
        Return Value
    End Function

    '结构图标缓存（pack URI 加载内嵌资源，图标取自 seedatlas.net）
    Private Shared ReadOnly IconCache As New Dictionary(Of String, BitmapImage)
    Private Shared Function GetStructIcon(Key As String) As ImageSource
        If IconCache.ContainsKey(Key) Then Return IconCache(Key)
        Dim Bmp As New BitmapImage()
        Bmp.BeginInit()
        Bmp.UriSource = New Uri("pack://application:,,,/Images/SeedMap/" & Key & ".png", UriKind.Absolute)
        Bmp.CacheOption = BitmapCacheOption.OnLoad
        Bmp.EndInit()
        Bmp.Freeze()
        IconCache(Key) = Bmp
        Return Bmp
    End Function

    '===============================
    '  初始化
    '===============================

    Public Sub New()
        '此调用是设计器所必需的。
        InitializeComponent()
        Dim Overworld As New List(Of StructDef) From {
            New StructDef With {.Name = "村庄", .Color = Color.FromRgb(&H7B, &HB6, &H61), .SType = ModuleSeedMap.StVillage, .Count = 0, .IconKey = "village", .IsDefault = True},
            New StructDef With {.Name = "掠夺者前哨站", .Color = Color.FromRgb(&HC0, &H8A, &H4F), .SType = ModuleSeedMap.StOutpost, .Count = 0, .IconKey = "outpost"},
            New StructDef With {.Name = "沉船", .Color = Color.FromRgb(&H5B, &H8D, &HB8), .SType = ModuleSeedMap.StShipwreck, .Count = 0, .IconKey = "shipwreck"},
            New StructDef With {.Name = "废弃传送门", .Color = Color.FromRgb(&H8C, &H6F, &HD1), .SType = ModuleSeedMap.StRuinedPortal, .Count = 0, .IconKey = "portal"},
            New StructDef With {.Name = "要塞", .Color = Color.FromRgb(&H4F, &HA3, &HA5), .SType = ModuleSeedMap.StStronghold, .Count = 0, .IconKey = "fortress"},
            New StructDef With {.Name = "沙漠神殿", .Color = Color.FromRgb(&HD9, &HB4, &H5B), .SType = ModuleSeedMap.StDesertPyramid, .Count = 0, .IconKey = "temple"},
            New StructDef With {.Name = "丛林神庙", .Color = Color.FromRgb(&H4E, &H9A, &H4E), .SType = ModuleSeedMap.StJungleTemple, .Count = 0, .IconKey = "jungle"},
            New StructDef With {.Name = "雪屋", .Color = Color.FromRgb(&HA8, &HD8, &HE8), .SType = ModuleSeedMap.StIgloo, .Count = 0, .IconKey = "igloo"},
            New StructDef With {.Name = "女巫小屋", .Color = Color.FromRgb(&H6B, &H4F, &H8A), .SType = ModuleSeedMap.StSwampHut, .Count = 0, .IconKey = "hut"},
            New StructDef With {.Name = "海底神殿", .Color = Color.FromRgb(&H3E, &H7E, &HA6), .SType = ModuleSeedMap.StMonument, .Count = 0, .IconKey = "guardian"},
            New StructDef With {.Name = "林地府邸", .Color = Color.FromRgb(&H55, &H70, &H3A), .SType = ModuleSeedMap.StMansion, .Count = 0, .IconKey = "mansion"},
            New StructDef With {.Name = "远古城市", .Color = Color.FromRgb(&H20, &H38, &H40), .SType = ModuleSeedMap.StAncientCity, .Count = 0, .IconKey = "ancient_city"},
            New StructDef With {.Name = "废弃矿洞", .Color = Color.FromRgb(&HA0, &H84, &H5C), .SType = ModuleSeedMap.StMineshaft, .Count = 0, .IconKey = "mineshaft"},
            New StructDef With {.Name = "试炼密室", .Color = Color.FromRgb(&H3E, &H7D, &HA6), .SType = ModuleSeedMap.StTrialChambers, .Count = 0, .IconKey = "trial_chambers"},
            New StructDef With {.Name = "地牢", .Color = Color.FromRgb(&H7A, &H6A, &H5A), .SType = 0, .Count = 0, .IconKey = "dungeon", .Supported = False}
        }
        Dim Nether As New List(Of StructDef) From {
            New StructDef With {.Name = "下界要塞", .Color = Color.FromRgb(&HB0, &H56, &H3C), .SType = ModuleSeedMap.StFortress, .Count = 0, .IconKey = "fortress", .IsDefault = True},
            New StructDef With {.Name = "堡垒遗迹", .Color = Color.FromRgb(&HD9, &HA0, &H5B), .SType = ModuleSeedMap.StBastion, .Count = 0, .IconKey = "bastion"},
            New StructDef With {.Name = "废弃传送门", .Color = Color.FromRgb(&H8C, &H6F, &HD1), .SType = ModuleSeedMap.StRuinedPortal, .Count = 0, .IconKey = "portal"}
        }
        Dim Finale As New List(Of StructDef) From {
            New StructDef With {.Name = "末地城", .Color = Color.FromRgb(&HB5, &H8F, &HD8), .SType = ModuleSeedMap.StEndCity, .Count = 0, .IconKey = "end_city", .IsDefault = True},
            New StructDef With {.Name = "末地折跃门", .Color = Color.FromRgb(&H7F, &HD8, &HC8), .SType = ModuleSeedMap.StEndGateway, .Count = 0, .IconKey = "end_gateway"}
        }
        StructLists.AddRange({Overworld, Nether, Finale})
        Structs = Overworld
        LoadStructsAll()
        EnsureDefaultStruct()
        BuildStructList()
        LoadBiomeSel(Dimension)
        BuildBiomeList()
        BuildVersionList()
        LoadSwitches()
        '默认版本按名字定位：版本列表可能会联网补充，索引不再固定
        Dim DefaultIndex As Integer = ModuleSeedMap.IndexOfVersionName("26.3")
        If DefaultIndex >= 0 Then
            VersionIndex = DefaultIndex
            HighlightVersion()
            LabVersion.Text = "Java 版 " & ModuleSeedMap.VersionNames(VersionIndex)
        End If
        '联网拉取官方版本列表，把新版本并入下拉
        RefreshVersionListFromNet()
        '每次进入页面都尝试刷新默认种子（同一实例只填一次；用户手动改过就不覆盖）
        AddHandler TileRetryTimer.Tick, AddressOf TileRetryTick
        AddHandler PageEnter,
            Sub()
                '存档详情页点了「地图」按钮：优先用带过来的种子 + 版本
                PageActive = True
                If ApplyPendingSeed() Then Return
                LoadSeedFromRecentSave()
                RefreshTiles(True)
            End Sub
    End Sub

    Private Sub PageSeedMap_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PageActive = True
        '点面板以外的地方 / 离开页面时收起 3D 预览（只挂一次）
        If Not _View3DHooked Then
            _View3DHooked = True
            AddHandler PreviewMouseDown, AddressOf PageSeedMap_PreviewMouseDown
            AddHandler PageExit, Sub()
                                     PageActive = False
                                     TileRetryTimer.Stop()
                                     CloseView3DNow()
                                 End Sub
            AddHandler PanView3D.SizeChanged, AddressOf PanView3D_SizeChanged
        End If
        If Not GeneratedOnce Then
            GeneratedOnce = True
            EnsureInitialView()
            '首次进入：如果是从存档详情带种子过来的，直接按它生成，别先用默认种子跑一遍
            If Not ApplyPendingSeed() Then RegenerateAll(True)
        End If
        '默认种子：当前实例最近修改的存档（读不到就保留占位种子）
        LoadSeedFromRecentSave()
        '清晰度按钮初始态
        If SeedMapQuality > 0 Then
            BtnDetail.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnDetail.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
        End If
    End Sub

    Private _View3DHooked As Boolean = False

    ''' <summary>离开种子地图（页面被摘下）时收起 3D 预览。</summary>
    Private Sub PageSeedMap_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        PageActive = False
        TileRetryTimer.Stop()
        CloseView3DNow()
    End Sub

    ''' <summary>3D 面板尺寸变化：按新宽高比重算画布并重绘。</summary>
    Private Sub PanView3D_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        If PanView3D Is Nothing OrElse PanView3D.Visibility <> Visibility.Visible Then Return
        Dim OldW = _3DVbW, OldH = _3DVbH
        Sync3DBitmapSize()
        If _3DVbW <> OldW OrElse _3DVbH <> OldH Then Render3D()
    End Sub

    Private Sub PageSeedMap_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles Me.SizeChanged
        If InitialZoom <= 0 Then Return
        '窗口过小可能突破缩放下限：按新尺寸收紧
        If Zoom < ZoomMinimum() Then Zoom = ZoomMinimum()
        ApplyTransform()
        RefreshTiles(True)
        StartTileWorker() '万一 worker 都空闲退出了，这里重新拉起来
    End Sub

    '===============================
    '  坐标换算与视野
    '===============================

    Private Function ToBlock(ux As Double, uy As Double) As Point
        Return New Point(Math.Round(ux * BlocksPerUnit), Math.Round(uy * BlocksPerUnit))
    End Function

    Private Function ToUnit(X As Double, Z As Double) As Point
        Return New Point(X / BlocksPerUnit, Z / BlocksPerUnit)
    End Function

    '===============================
    '  相机（俯视 ↔ 斜视）
    '===============================

    ''' <summary>屏幕点 → 地图单位。</summary>
    Private Function ScreenToUnit(P As Point) As Point
        Return New Point((P.X - MapPos.X) / Zoom, (P.Y - MapPos.Y) / Zoom)
    End Function

    ''' <summary>当前视野对应的地图单位矩形。</summary>
    Private Function ViewUnitRect() As Rect
        Dim Vw = PanMapViewport.ActualWidth, Vh = PanMapViewport.ActualHeight
        If Vw < 50 Then Vw = 1280
        If Vh < 50 Then Vh = 700
        Dim U0 = -MapPos.X / Zoom, U1 = (Vw - MapPos.X) / Zoom
        Dim V0 = -MapPos.Y / Zoom, V1 = (Vh - MapPos.Y) / Zoom
        Return New Rect(U0, V0, Math.Max(0.0001, U1 - U0), Math.Max(0.0001, V1 - V0))
    End Function

    '视野中心（方块坐标）
    Private Function ViewCenter() As Point
        Dim R = ViewUnitRect()
        Return ToBlock((R.Left + R.Right) / 2, (R.Top + R.Bottom) / 2)
    End Function

    Private Function ZoomMinimum() As Double
        Dim Vw = PanMapViewport.ActualWidth
        If Vw < 50 Then Vw = 1280
        Return Vw / 1280 '最远可见约 10240 方块宽
    End Function

    Private Sub ApplyTransform()
        MapScale.ScaleX = Zoom : MapScale.ScaleY = Zoom
        UpdateMarkerScale()
        UpdateSkeletonScale()
        UpdateZoomLabel()
        '缩放时网格/标尺/史莱姆都要跟着重算（都有覆盖范围缓存，重复调用很便宜）
        UpdateOverlays()
    End Sub

    ''' <summary>初始视野：以主世界出生点（或原点）为中心，约 2400 方块宽。</summary>
    Private Sub EnsureInitialView()
        If InitialZoom > 0 Then Return
        Dim Vw = PanMapViewport.ActualWidth
        If Vw < 50 Then Vw = 1280
        InitialZoom = DefaultZoomForDim(0)
        Zoom = DefaultZoomForDim(Dimension)
        ApplyTransform()
        Dim Center As New Point(0, 0)
        If Dimension = 0 AndAlso ModuleSeedMap.IsNativeAvailable Then
            Try
                Center = ModuleSeedMap.GetSpawn(SeedValue, ModuleSeedMap.VersionIds(VersionIndex))
            Catch ex As Exception
                Logger.Warn(ex, "估计出生点失败，改用世界原点")
            End Try
        End If
        CenterOn(Center.X, Center.Y)
    End Sub

    ''' <summary>各维度的默认缩放系数（相对初始缩放）。末地为 50%。</summary>
    Private Function DefaultZoomFactor(DimIndex As Integer) As Double
        Return If(DimIndex = 2, 0.5, 1.0)
    End Function

    ''' <summary>默认视野：竖直方向大致能看到多少方块（主世界 / 下界）。</summary>
    Private Const DefaultVisibleBlocks As Double = 600

    ''' <summary>该维度的默认缩放：主世界/下界竖直可见约 400 方块，末地再缩到一半（视野翻倍）。</summary>
    Private Function DefaultZoomForDim(DimIndex As Integer) As Double
        Dim Vh As Double = PanMapViewport.ActualHeight
        If Vh < 50 Then Vh = 700
        Dim Z As Double = Vh * BlocksPerUnit / DefaultVisibleBlocks
        If DimIndex = 2 Then Z *= 0.5
        Return Math.Max(ZoomMinimum(), Z)
    End Function
    '===============================
    '  网格 / 坐标标尺 / 地形精度
    '===============================

    ''' <summary>精细模式多一档：4 方块/采样（cubiomes 的最小群系尺度）。</summary>
    ''' <summary>是否使用精细地形（4 方块/采样，瓦片更小、数量更多）。默认开启，标准档在默认缩放下明显偏糊。</summary>
    ' <summary>地图清晰度：0 低 / 1 中 / 2 高（对应不同的瓦片预算）。</summary>
    Private SeedMapQuality As Integer = 2
    ''' <summary>当前网格间距（方块），用于避免重复重建网格。</summary>
    Private LastGridSpacing As Double = 0

    '网格/坐标标尺复用对象：以前每次刷新都要新建几十个 LineGeometry + TextBlock（还各带一个 Effect），
    '拖拽时每帧都做一遍，是页面里最大的固定开销。现在只建一次，之后只改数据。
    Private GridLinePath As Path
    Private GridAxisPath As Path
    Private ReadOnly GridLineBrush As Brush = MakeFrozenBrush(Color.FromArgb(118, 255, 255, 255))
    Private ReadOnly GridAxisBrush As Brush = MakeFrozenBrush(Color.FromArgb(200, 255, 232, 150))
    '已绘制网格的覆盖范围（地图单位）。视口仍在其中时直接跳过重建——网格和地图是同一坐标系，
    '平移时本来就跟着一起动，只有移出覆盖范围才需要重画。
    Private GridCover As Rect = Rect.Empty
    Private GridCoverSpacing As Double = 0
    Private GridCoverZoom As Double = 0

    Private Shared Function MakeFrozenBrush(C As Color) As Brush
        Dim B As New SolidColorBrush(C)
        B.Freeze()
        Return B
    End Function

    ''' <summary>网格线与坐标轴的两条 Path 只创建一次，之后原地替换 Data。</summary>
    Private Sub EnsureGridPaths()
        If GridLinePath Is Nothing Then
            GridLinePath = New Path With {.Stroke = GridLineBrush, .IsHitTestVisible = False}
            GridAxisPath = New Path With {.Stroke = GridAxisBrush, .IsHitTestVisible = False}
            PanGrid.Children.Clear()
            PanGrid.Children.Add(GridLinePath)
            PanGrid.Children.Add(GridAxisPath)
        End If
    End Sub

    ''' <summary>
    ''' 按当前缩放选择网格间距（方块），让屏幕格距落在 48~140px，
    ''' 并把网格线按当前视野（外扩余量）画进 PanGrid（与坐标标尺共用同一套间距）。
    ''' </summary>
    Private Sub UpdateGrid()
        Try
            If PanGrid Is Nothing OrElse Zoom <= 0 Then Return
            Dim Vw = PanMapViewport.ActualWidth, Vh = PanMapViewport.ActualHeight
            If Vw < 50 OrElse Vh < 50 Then Return
            Dim Spacing As Double = 16
            Do While Spacing * Zoom / BlocksPerUnit < 48 AndAlso Spacing < 8192
                Spacing *= 2
            Loop
            Do While Spacing * Zoom / BlocksPerUnit > 140 AndAlso Spacing > 8
                Spacing /= 2
            Loop
            LastGridSpacing = Spacing
            '网格关闭：清一次数据后直接返回（标尺仍然要跟随视野）
            If Not GridVisible Then
                If GridLinePath IsNot Nothing Then
                    GridLinePath.Data = Nothing
                    GridAxisPath.Data = Nothing
                End If
                GridCover = Rect.Empty
                UpdateRulers(Spacing)
                Return
            End If
            Dim Units As Double = Spacing / BlocksPerUnit
            Dim R0 = ViewUnitRect()
            Dim U0 As Double = R0.Left, U1 As Double = R0.Right
            Dim V0 As Double = R0.Top, V1 As Double = R0.Bottom
            '间距和缩放不变、视口还在已绘制范围内 → 什么都不用做
            If GridCoverSpacing = Spacing AndAlso Math.Abs(GridCoverZoom - Zoom) < 0.0001 AndAlso
               Not GridCover.IsEmpty AndAlso
               GridCover.Left <= U0 AndAlso GridCover.Right >= U1 AndAlso
               GridCover.Top <= V0 AndAlso GridCover.Bottom >= V1 Then
                If PanGrid.Visibility <> Visibility.Visible Then PanGrid.Visibility = Visibility.Visible
                UpdateRulers(Spacing)
                Return
            End If
            '向外多画两格，普通拖拽就不用反复重建
            Dim Cover As New Rect(U0 - Units * 2, V0 - Units * 2, (U1 - U0) + Units * 4, (V1 - V0) + Units * 4)
            Dim Geo As New StreamGeometry()
            Using Ctx = Geo.Open()
                Dim Ux As Double = Math.Floor(Cover.Left / Units) * Units
                Do While Ux <= Cover.Right
                    Ctx.BeginFigure(New Point(Ux, Cover.Top), False, False)
                    Ctx.LineTo(New Point(Ux, Cover.Bottom), True, False)
                    Ux += Units
                Loop
                Dim Uy As Double = Math.Floor(Cover.Top / Units) * Units
                Do While Uy <= Cover.Bottom
                    Ctx.BeginFigure(New Point(Cover.Left, Uy), False, False)
                    Ctx.LineTo(New Point(Cover.Right, Uy), True, False)
                    Uy += Units
                Loop
            End Using
            Geo.Freeze()
            'x=0 / z=0 两条坐标轴加粗提亮，方便定位原点
            Dim AxisGeo As New StreamGeometry()
            Dim HasAxis As Boolean = False
            Using Ctx = AxisGeo.Open()
                If Cover.Left <= 0 AndAlso Cover.Right >= 0 Then
                    Ctx.BeginFigure(New Point(0, Cover.Top), False, False)
                    Ctx.LineTo(New Point(0, Cover.Bottom), True, False)
                    HasAxis = True
                End If
                If Cover.Top <= 0 AndAlso Cover.Bottom >= 0 Then
                    Ctx.BeginFigure(New Point(Cover.Left, 0), False, False)
                    Ctx.LineTo(New Point(Cover.Right, 0), True, False)
                    HasAxis = True
                End If
            End Using
            AxisGeo.Freeze()
            '线宽随缩放略微加粗（屏幕 0.8px → 1.5px），保持纤细不抢地形
            Dim GridPx As Double = Math.Max(0.8, Math.Min(1.5, 0.8 + (Zoom - 4) * 0.05))
            EnsureGridPaths()
            PanGrid.Visibility = Visibility.Visible
            GridLinePath.Data = Geo
            GridLinePath.StrokeThickness = GridPx / Zoom
            GridAxisPath.Data = If(HasAxis, AxisGeo, Nothing)
            GridAxisPath.StrokeThickness = Math.Min(2.4, GridPx * 1.7) / Zoom
            GridCover = Cover
            GridCoverSpacing = Spacing
            GridCoverZoom = Zoom
            UpdateRulers(Spacing)
        Catch ex As Exception
            Logger.Warn(ex, "更新网格失败")
        End Try
    End Sub
    ''' <summary>在地图四周画出与网格对应的方块坐标（上下为 X，左右为 Z）。</summary>
    '标尺文字池：坐标标尺在屏幕坐标系里，拖拽时每帧都要重摆位置。
    '以前是 Clear + 逐个 New TextBlock（每个还配一个 DropShadowEffect），开销很大；
    '现在复用固定的一批 TextBlock，只改文本和坐标。
    Private ReadOnly RulerPool As New List(Of TextBlock)
    Private RulerUsed As Integer = 0

    Private Sub UpdateRulers(Optional SpacingBlocks As Double = 0)
        Try
            If PanRuler Is Nothing Then Return
            If InitialZoom <= 0 OrElse Zoom <= 0 Then HideUnusedRulers(0) : Return
            Dim Vw = PanMapViewport.ActualWidth, Vh = PanMapViewport.ActualHeight
            If Vw < 80 OrElse Vh < 80 Then HideUnusedRulers(0) : Return
            If SpacingBlocks <= 0 Then SpacingBlocks = If(LastGridSpacing > 0, LastGridSpacing, 16)
            '数字比网格线稀疏一档，避免挤在一起
            Dim LabelStep As Double = SpacingBlocks
            Do While LabelStep * Zoom / BlocksPerUnit < 72
                LabelStep *= 2
            Loop
            RulerUsed = 0
            '坐标标尺：X 轴（上下两条）与 Z 轴（左右两条）都用同一个 Zoom
            Dim X0 As Double = Math.Floor((-MapPos.X / Zoom * BlocksPerUnit) / LabelStep) * LabelStep
            Dim XEnd As Double = (Vw - MapPos.X) / Zoom * BlocksPerUnit
            Dim Xv As Double = X0
            Do While Xv <= XEnd
                Dim Px As Double = MapPos.X + Xv / BlocksPerUnit * Zoom
                If Px >= 2 AndAlso Px <= Vw - 56 Then
                    Dim T = CLng(Xv).ToString()
                    AddRulerLabel(Px + 3, 3, T)
                    AddRulerLabel(Px + 3, Vh - 22, T)
                End If
                Xv += LabelStep
            Loop
            Dim Z0 As Double = Math.Floor((-MapPos.Y / Zoom * BlocksPerUnit) / LabelStep) * LabelStep
            Dim ZEnd As Double = (Vh - MapPos.Y) / Zoom * BlocksPerUnit
            Dim Zv As Double = Z0
            Do While Zv <= ZEnd
                Dim Py As Double = MapPos.Y + Zv / BlocksPerUnit * Zoom
                If Py >= 16 AndAlso Py <= Vh - 18 Then
                    Dim T = CLng(Zv).ToString()
                    AddRulerLabel(4, Py - 8, T)
                    AddRulerLabel(Vw - 74, Py - 8, T)
                End If
                Zv += LabelStep
            Loop
            HideUnusedRulers(RulerUsed)
        Catch ex As Exception
            Logger.Warn(ex, "更新坐标标尺失败")
        End Try
    End Sub

    Private Sub AddRulerLabel(X As Double, Y As Double, Text As String)
        Dim T As TextBlock
        If RulerUsed < RulerPool.Count Then
            T = RulerPool(RulerUsed)
        Else
            '只加字号与字重，不加底衬（底衬在密集区会连成一条黑带）
            T = New TextBlock With {.FontSize = 12, .FontWeight = FontWeights.SemiBold,
                                    .Foreground = Brushes.White, .Opacity = 1,
                                    .IsHitTestVisible = False}
            RulerPool.Add(T)
            PanRuler.Children.Add(T)
        End If
        RulerUsed += 1
        If T.FontSize < 12 Then
            T.FontSize = 12
            T.FontWeight = FontWeights.SemiBold
            T.Opacity = 1
            T.ClearValue(TextBlock.BackgroundProperty)
            T.ClearValue(TextBlock.PaddingProperty)
        End If
        If T.Text <> Text Then T.Text = Text
        If T.Visibility <> Visibility.Visible Then T.Visibility = Visibility.Visible
        Canvas.SetLeft(T, X)
        Canvas.SetTop(T, Y)
    End Sub

    ''' <summary>把池里没用到的标尺文字收起来。</summary>
    Private Sub HideUnusedRulers(UsedCount As Integer)
        For i As Integer = UsedCount To RulerPool.Count - 1
            If RulerPool(i).Visibility <> Visibility.Collapsed Then RulerPool(i).Visibility = Visibility.Collapsed
        Next
    End Sub

    ''' <summary>地形精度切换：标准 8 方块/采样 ↔ 精细 4 方块/采样（旧瓦片作废重建）。</summary>
    Private Sub BtnDetail_Click(sender As Object, e As RoutedEventArgs) Handles BtnDetail.Click
        '三档循环：低 → 中 → 高 → 低
        SeedMapQuality = (SeedMapQuality + 1) Mod 3
        SetToggleLook(BtnDetail, SeedMapQuality > 0)
        Select Case SeedMapQuality
            Case 0 : Hint("地图清晰度：低（加载快、让路给游戏）", HintType.Blue)
            Case 1 : Hint("地图清晰度：中", HintType.Blue)
            Case Else : Hint("地图清晰度：高（更清晰，加载更慢）", HintType.Blue)
        End Select
        '只改预算，不清缓存：更细的直接请求、更粗的自动隐藏，切换瞬时完成
        RefreshTiles(True)
        ApplyLevelVisibility()
        StartTileWorker()
        SaveSwitches()
    End Sub

    '===============================
    '  界面状态记忆（永久保存到启动器设置）
    '===============================

    ''' <summary>该维度是否已经显式保存过结构勾选（保存过就不再套用默认勾选）。</summary>
    Private ReadOnly StructSaved As Boolean() = {False, False, False}

    Private Sub SetToggleLook(Btn As Button, Active As Boolean)
        If Btn Is Nothing Then Return
        Btn.Background = If(Active, TryCast(FindResource("BrushAccentSoft"), Brush), Brushes.Transparent)
        Btn.Foreground = If(Active, TryCast(FindResource("ColorBrush3"), Brush), TryCast(FindResource("BrushText2"), Brush))
    End Sub

    ''' <summary>读取上次保存的五个开关，并同步按钮外观。</summary>
    Private Sub LoadSwitches()
        Try
            TerrainVisible = Settings.Get(Of Boolean)("SeedMapLayerVisible")
            GridVisible = Settings.Get(Of Boolean)("SeedMapGridVisible")
            SlimeEnabled = Settings.Get(Of Boolean)("SeedMapSlimeVisible")
            ReliefEnabled = Settings.Get(Of Boolean)("SeedMapRelief")
            SeedMapQuality = Math.Max(0, Math.Min(2, Settings.Get(Of Integer)("SeedMapQuality")))
        Catch ex As Exception
            Logger.Warn(ex, "读取种子地图开关失败")
        End Try
        '地形按钮是"隐藏"时才高亮，所以取反
        SetToggleLook(BtnToolLayer, Not TerrainVisible)
        SetToggleLook(BtnToolGrid, Not GridVisible)
        SetToggleLook(BtnToolSlime, SlimeEnabled)
        SetToggleLook(BtnToolRelief, ReliefEnabled)
        SetToggleLook(BtnDetail, SeedMapQuality > 0)
        '地形图层是靠透明度隐藏的，这里要把它同步过来，否则按钮显示隐藏但画面还在画
        If PanTerrain IsNot Nothing Then PanTerrain.Opacity = If(TerrainVisible, 1.0, 0.0)
        '群系筛选标签同步
        RefreshBiomeStates()
    End Sub

    Private Sub SaveSwitches()
        Try
            Settings.Set("SeedMapLayerVisible", TerrainVisible)
            Settings.Set("SeedMapGridVisible", GridVisible)
            Settings.Set("SeedMapSlimeVisible", SlimeEnabled)
            Settings.Set("SeedMapRelief", ReliefEnabled)
            Settings.Set("SeedMapQuality", SeedMapQuality)
        Catch ex As Exception
            Logger.Warn(ex, "保存种子地图开关失败")
        End Try
    End Sub

    ''' <summary>读取三个维度各自保存的结构勾选。空字符串 = 从未保存过，保留默认勾选。</summary>
    Private Sub LoadStructsAll()
        For D As Integer = 0 To 2
            Try
                Dim Raw As String = Settings.Get(Of String)("SeedMapStructs" & D)
                If Raw Is Nothing Then Raw = ""
                Raw = Raw.Trim()
                If Raw = "" Then Continue For '从未保存：保留 EnsureDefaultStruct 的默认勾选
                StructSaved(D) = True
                Dim Names As New HashSet(Of String)(Raw.Split(New Char() {","c}, StringSplitOptions.RemoveEmptyEntries))
                For Each S In StructLists(D)
                    S.Enabled = Names.Contains(S.Name)
                Next
            Catch ex As Exception
                Logger.Warn(ex, "读取种子地图结构勾选失败")
            End Try
        Next
    End Sub

    ''' <summary>保存当前维度的结构勾选。一项都没勾时存 "-"，避免下次启动又被默认值覆盖。</summary>
    Private Sub SaveStructs()
        Try
            Dim D As Integer = Dimension
            If D < 0 OrElse D > 2 Then Return
            Dim Names As New List(Of String)
            For Each S In StructLists(D)
                If S.Enabled Then Names.Add(S.Name)
            Next
            StructSaved(D) = True
            Settings.Set("SeedMapStructs" & D, If(Names.Count = 0, "-", String.Join(",", Names)))
        Catch ex As Exception
            Logger.Warn(ex, "保存种子地图结构勾选失败")
        End Try
    End Sub

    ''' <summary>读取指定维度保存的群系筛选。</summary>
    Private Sub LoadBiomeSel(D As Integer)
        SelectedBiomes.Clear()
        If D < 0 OrElse D > 2 Then Return
        Try
            Dim Raw As String = Settings.Get(Of String)("SeedMapBiomes" & D)
            If String.IsNullOrEmpty(Raw) Then Return
            For Each Part In Raw.Split(New Char() {","c}, StringSplitOptions.RemoveEmptyEntries)
                Dim Id As Integer
                If Integer.TryParse(Part, Id) Then SelectedBiomes.Add(Id)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取种子地图群系筛选失败")
        End Try
    End Sub

    Private Sub SaveBiomes()
        Try
            If Dimension < 0 OrElse Dimension > 2 Then Return
            Settings.Set("SeedMapBiomes" & Dimension, String.Join(",", SelectedBiomes))
        Catch ex As Exception
            Logger.Warn(ex, "保存种子地图群系筛选失败")
        End Try
    End Sub
    ''' <summary>单个维度独立的视野状态：缩放 + 中心方块坐标。末地与主世界/下界互不影响。</summary>
    Private Class DimViewState
        Public Zoom As Double
        Public CenterX As Double
        Public CenterZ As Double
        Public Valid As Boolean
    End Class

    Private ReadOnly DimViews As DimViewState() = {New DimViewState, New DimViewState, New DimViewState}

    ''' <summary>记下指定维度当前的视野（缩放 + 视野中心）。</summary>
    Private Sub SaveDimView(DimIndex As Integer)
        Try
            If DimIndex < 0 OrElse DimIndex > 2 Then Return
            Dim C = ViewCenter()
            Dim V = DimViews(DimIndex)
            V.Zoom = Zoom
            V.CenterX = C.X
            V.CenterZ = C.Y
            V.Valid = True
        Catch ex As Exception
            Logger.Warn(ex, "保存维度视野失败")
        End Try
    End Sub

    ''' <summary>把当前维度的视野记录回它自己的状态（供动画结束回调使用）。</summary>
    Private Sub SaveDimViewCurrent()
        SaveDimView(Dimension)
    End Sub

    ''' <summary>
    ''' 切到目标维度并套用它的视野：
    ''' · 末地是独立维度——有自己的位置与缩放，首次进入回到主岛中心、使用 50% 默认缩放；
    ''' · 主世界 ↔ 下界按 Minecraft 的 8:1 坐标换算（主世界 → 下界 ÷8，下界 → 主世界 ×8），缩放保持；
    ''' · 之后再进同一维度，恢复它自己上次的位置与缩放。
    ''' </summary>
    Private Sub ApplyDimView(Previous As Integer, Target As Integer)
        Try
            Dim V = DimViews(Target)
            If Not V.Valid Then
                Dim Src = DimViews(Previous)
                Dim Cx As Double, Cz As Double, Z As Double
                If Src.Valid Then
                    Cx = Src.CenterX : Cz = Src.CenterZ : Z = Src.Zoom
                Else
                    Dim C = ViewCenter()
                    Cx = C.X : Cz = C.Y : Z = Zoom
                End If
                If Target = 2 Then
                    '末地：独立位置，首次进入回到 (0,0) 主岛，50% 缩放
                    Cx = 0 : Cz = 0
                    Z = If(InitialZoom > 0, InitialZoom, Math.Max(2.5, PanMapViewport.ActualWidth / 300)) * DefaultZoomFactor(2)
                ElseIf Target = 1 AndAlso Previous = 0 Then
                    '主世界 → 下界：坐标 ÷8
                    Cx = Cx / 8 : Cz = Cz / 8
                ElseIf Target = 0 AndAlso Previous = 1 Then
                    '下界 → 主世界：坐标 ×8
                    Cx = Cx * 8 : Cz = Cz * 8
                End If
                V.CenterX = Cx : V.CenterZ = Cz : V.Zoom = Z : V.Valid = True
            End If
            Zoom = Math.Max(ZoomMinimum(), V.Zoom)
            ApplyTransform()
            CenterOn(V.CenterX, V.CenterZ)
            SaveDimView(Target) '把实际生效的缩放/中心写回（Zoom 可能被 ZoomMinimum 夹过）
        Catch ex As Exception
            Logger.Warn(ex, "套用维度视野失败")
        End Try
    End Sub

    Private Sub CenterOn(BlockX As Double, BlockZ As Double)
        Dim Vw = PanMapViewport.ActualWidth, Vh = PanMapViewport.ActualHeight
        If Vw < 50 Then Vw = 1280
        If Vh < 50 Then Vh = 700
        Dim U = ToUnit(BlockX, BlockZ)
        MapPos.X = Vw / 2 - U.X * Zoom
        MapPos.Y = Vh / 2 - U.Y * Zoom
        ApplyTransform()
        RefreshTiles(True)
        StartTileWorker() '万一 worker 都空闲退出了，这里重新拉起来
    End Sub

    ''' <summary>
    ''' 当前瓦片层级：取铺满这一屏所需块数不超过预算的最细一档。
    ''' 以前按瓦片在屏幕上够 80px 就升级来选档，不看这一屏要多少块；中等缩放会选到过细的档、一屏两百多块，铺不满。
    ''' </summary>
    Private Function CurrentLevel() As Integer
        Dim UseLevels As Integer() = Levels
        Dim R = ViewUnitRect()
        For Each L In UseLevels
            If TileCountFor(L, R.Left, R.Right, R.Top, R.Bottom) <= FineBudget() Then Return L
        Next
        Return UseLevels(UseLevels.Length - 1)
    End Function


    ''' <summary>
    ''' 粗层底图层级：永远取最粗那一档（128 方块/采样，一块 8192 方块）。
    ''' 一块就能盖住整屏，所以首屏只要一块的时间就能看见地形轮廓，细节随后补齐。
    ''' </summary>
    Private Function CoarseLevel() As Integer
        Dim UseLevels As Integer() = Levels
        Return UseLevels(UseLevels.Length - 1)
    End Function

    Private Function UnitsPerTile(L As Integer) As Double
        Return TileCells * L / BlocksPerUnit
    End Function

    '===============================
    '  瓦片流式加载
    '===============================

    Private Class TileEntry
        Public Image As Image
        Public Ids As Integer()
        ''' <summary>该瓦片的地表高度（浮雕用；未开启浮雕时为 Nothing）。</summary>
        Public Heights As Single()
        Public Level As Integer
        Public TileX As Integer
        Public TileZ As Integer
    End Class

    Private Structure TileJob
        Public Serial As Integer
        Public Level As Integer
        Public TileX As Integer
        Public TileZ As Integer
        Public Seed As Long
        Public Version As Integer
        Public Dimension As Integer
        ''' <summary>排队优先级：数值越小越先做（0 起为粗层底图，1 起为细层，再按离视野中心的距离微调）。</summary>
        Public Priority As Double
    End Structure

    '结构扫描任务：所有参数在 UI 线程打包，worker 不得访问任何 UI 控件
    '（用 Class 而不是 Structure：入队后还可能把新勾选的结构类型合并进去）
    Private Class ScanJob
        Public Serial As Integer
        Public Version As Integer
        Public Dimension As Integer
        Public Seed As Long
        Public CenterX As Integer
        Public CenterZ As Integer
        Public Types As Integer()
    End Class

    Private ReadOnly JobSync As New Object()
    ''' <summary>待办队列（TileJob / ScanJob 混排）：每次取优先级最高的一个，见 DequeueBest。</summary>
    Private ReadOnly JobQueue As New List(Of Object)
    ''' <summary>入队即置位、worker 取空即等待：避免以前 Thread.Sleep(80) 带来的最多 80ms 启动延迟。
    ''' 用信号量而不是事件，是为了能一次唤醒多个 worker（几个任务就 release 几次）。</summary>
    Private ReadOnly JobSignal As New Threading.SemaphoreSlim(0)
    ''' <summary>兜底重试定时器：停手后继续补请求缺失的瓦片。</summary>
    Private ReadOnly TileRetryTimer As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(400)}
    ''' <summary>当前是否停留在种子地图页面：离开后要停止一切瓦片请求和重绘，否则后台一直空转。
    ''' 尤其是开着游戏时，后台重绘会直接抢 GPU/CPU，表现为启动器整体变卡。</summary>
    Private PageActive As Boolean = False

    ''' <summary>主窗口是否处于最小化：最小化时不刷新、并释放位图缓存给游戏让内存。</summary>
    Private HostMinimized As Boolean = False

    ''' <summary>主窗口最小化时调用（由 FormMain 的 StateChanged 触发）。</summary>
    Public Sub OnHostMinimized()
        HostMinimized = True
        Try
            TileRetryTimer.Stop()
            ClearTiles()
        Catch ex As Exception
            Logger.Warn(ex, "最小化时释放种子地图缓存失败")
        End Try
    End Sub

    ''' <summary>主窗口还原时调用。</summary>
    Public Sub OnHostRestored()
        If Not HostMinimized Then Return
        HostMinimized = False
        If PageActive Then RefreshTiles(True)
    End Sub
    Private ReadOnly TilePending As New HashSet(Of Long)
    Private ReadOnly Tiles As New Dictionary(Of Long, TileEntry)
    ''' <summary>L0 骨架占位：已入队但还没算完的瓦片，先在地图上画一个虚线格子，让"在加载"看得见。</summary>
    Private ReadOnly TileSkeletons As New Dictionary(Of Long, FrameworkElement)
    Private ReadOnly SkeletonFill As Brush = MakeFrozenBrush(Color.FromArgb(26, 120, 170, 230))
    Private ReadOnly SkeletonStroke As Brush = MakeFrozenBrush(Color.FromArgb(110, 150, 190, 240))
    Private SkeletonAnim As DoubleAnimation
    Private GenSerial As Integer = 0
    Private ScanQueued As Boolean = False
    ''' <summary>本轮（种子/版本/维度）已覆盖扫描的结构类型：勾选这些结构时无需再扫。</summary>
    Private ReadOnly ScanCovered As New HashSet(Of Integer)
    ''' <summary>当前存活的 worker 线程数（空闲超时会退出，不长期占用线程）。</summary>
    Private ActiveWorkers As Integer = 0

    Private Function TileKey(L As Integer, Tx As Integer, Tz As Integer) As Long
        Return (CLng(L) << 48) Or (CLng(Tx + 65536) << 24) Or CLng(Tz + 65536)
    End Function

    ''' <summary>
    ''' 拖拽 / 缩放时的高频刷新：网格、坐标标尺、史莱姆叠加层。
    ''' 这三层都已经做成「覆盖范围不变就跳过」，每帧调用也不会卡。
    ''' 瓦片与结构扫描另外走 RefreshTiles 的 120ms 节流。
    ''' </summary>
    Private Sub UpdateOverlays()
        UpdateGrid()
        RefreshSlime()
    End Sub

    '节流：拖拽 / 缩放动画期间高频触发，两次刷新至少间隔 120ms（Force 跳过限制）
    Private LastRefreshTick As Long = 0
    ''' <summary>上一次用过的瓦片层级：档位变化时要立刻刷新，别等节流。</summary>
    Private LastUsedLevel As Integer = 0
    ''' <summary>细层一屏最多排多少块：档位就按这个预算选，保证一两次刷新就能铺满。</summary>
    ''' <summary>高精度：一屏最多排多少块（预算越大，退档点越靠外）。</summary>
    Private Const FineBudgetHigh As Integer = 120
    ''' <summary>低精度：预算缩小，退档点随之往里收。</summary>
    Private Const FineBudgetLow As Integer = 32

    ''' <summary>当前精度对应的细层预算。</summary>
    Private Function FineBudget() As Integer
        Select Case SeedMapQuality
            Case 0 : Return FineBudgetLow
            Case 1 : Return (FineBudgetHigh + FineBudgetLow) \ 2
            Case Else : Return FineBudgetHigh
        End Select
    End Function

    ''' <summary>
    ''' 按当前视野请求缺失的瓦片（三级流水：粗层底图 → 细层），顺带做取消、淘汰与进度统计。
    ''' 视野移动超过阈值时请求结构重扫。
    ''' </summary>
    Private Sub RefreshTiles(Optional Force As Boolean = False)
        '不在种子地图页面就不干活：离开后后台还在重绘的话，开着游戏时会明显抢资源
        If Not PageActive OrElse HostMinimized Then Return
        '精度档位一变就必须立刻重建：以前要等 120ms 节流甚至等缩放动画结束，
        '表现就是"缩放到点了精度却没跟着变"。
        Dim LvNow As Integer = CurrentLevel()
        If LvNow <> LastUsedLevel Then
            LastUsedLevel = LvNow
            Force = True
        End If
        If Not Force AndAlso CLng(Environment.TickCount) - LastRefreshTick < 120 Then Return
        LastRefreshTick = CLng(Environment.TickCount)
        If InitialZoom <= 0 Then Return
        Dim Vw = PanMapViewport.ActualWidth, Vh = PanMapViewport.ActualHeight
        If Vw < 50 OrElse Vh < 50 Then Return
        Dim R = ViewUnitRect()
        Dim U0 = R.Left, U1 = R.Right
        Dim V0 = R.Top, V1 = R.Bottom
        Dim Lf = CurrentLevel()
        Dim Lc = CoarseLevel()

        '① 粗层底图：整屏只要 1~2 块，优先级最高。它是"细层还没算完"时的垫底，
        '   单块采样点数与细层相同，所以能在一两帧内把整屏铺满。
        RequestLevel(Lc, U0, U1, V0, V1, 0, Lf = Lc)

        ' 中继层：取阶梯里比目标粗一档，让"由糊到清"是两级过渡而不是一次跳变
        Dim UseLevels As Integer() = Levels
        Dim LvIdx As Integer = Array.IndexOf(UseLevels, Lf)
        If LvIdx >= 0 AndAlso LvIdx < UseLevels.Length - 1 Then
            Dim LMid As Integer = UseLevels(LvIdx + 1)
            If TileCountFor(LMid, U0, U1, V0, V1) <= 8 Then RequestLevel(LMid, U0, U1, V0, V1, 0.5, True)
        End If

        '③ 细层：按离视野中心由近及远排队，缺块挂骨架占位
        If Lf <> Lc Then RequestLevel(Lf, U0, U1, V0, V1, 1, True)

        '④ 取消已经离开视野（外扩一屏）的待办：单线程 worker 不该为看不见的地方干活
        CancelPendingOutside(U0 - (U1 - U0), U1 + (U1 - U0), V0 - (V1 - V0), V1 + (V1 - V0))

        '⑤ 结构重扫判定（参数在 UI 线程打包）
        Dim Center = ViewCenter()
        If Double.IsNaN(LastScanCenter.X) OrElse Math.Abs(Center.X - LastScanCenter.X) > 768 OrElse Math.Abs(Center.Y - LastScanCenter.Y) > 768 Then
            LastScanCenter = Center
            EnqueueScan(Center)
        End If

        '⑥ 史莱姆区块叠加层、网格与坐标标尺跟随视野刷新
        UpdateOverlays()

        '⑦ 缓存淘汰 + 顶栏进度
        EvictFarTiles(Center)
        ApplyLevelVisibility()
        SweepStaleSkeletons()
        UpdateLoadUi()
        '只要还有没到齐的瓦片，就让定时器继续补请求：用户停手后不会再触发刷新，
        '光靠交互驱动会漏掉几块，表现就是"某几个区块一直空着"
        If TileSkeletons.Count > 0 OrElse TilePending.Count > 0 Then TileRetryTimer.Start()

    End Sub

    ''' <summary>兜底重试：每 400ms 再请求一次缺的瓦片，直到没有占位框为止。</summary>
    Private Sub TileRetryTick(sender As Object, e As EventArgs)
        If Not PageActive Then
            TileRetryTimer.Stop()
            Return
        End If
        If TileSkeletons.Count = 0 AndAlso TilePending.Count = 0 Then
            TileRetryTimer.Stop()
            Return
        End If
        RefreshTiles(True)
        StartTileWorker() '万一 worker 都空闲退出了，这里重新拉起来
    End Sub

    ''' <summary>
    ''' 兜底：占位虚线框既不在待办里、也没有对应瓦片，说明这个任务丢了（算失败 / 被反复取消），
    ''' 直接撤掉，避免屏幕上一直挂着几个永远不消失的格子。
    ''' </summary>
    Private Sub SweepStaleSkeletons()
        If TileSkeletons.Count = 0 Then Return
        Dim Drop As New List(Of Long)
        SyncLock JobSync
            For Each Key In TileSkeletons.Keys
                If Not TilePending.Contains(Key) AndAlso Not Tiles.ContainsKey(Key) Then Drop.Add(Key)
            Next
        End SyncLock
        For Each Key In Drop
            RemoveSkeleton(Key)
        Next
    End Sub

    ''' <summary>某一层级铺满指定视野需要多少块瓦片（用来判断某个中继层值不值得插进来）。</summary>
    Private Function TileCountFor(L As Integer, U0 As Double, U1 As Double, V0 As Double, V1 As Double) As Integer
        Dim Upt = UnitsPerTile(L)
        Return (CInt(Math.Floor(U1 / Upt)) - CInt(Math.Floor(U0 / Upt)) + 1) *
               (CInt(Math.Floor(V1 / Upt)) - CInt(Math.Floor(V0 / Upt)) + 1)
    End Function

    ''' <summary>
    ''' 请求某一层级覆盖指定视野（地图单位矩形）的瓦片，缺的才入队。返回入队数量。
    ''' BasePriority 越小越先做；WithSkeleton 时给缺块挂上虚线骨架，让加载过程可见。
    ''' </summary>
    Private Function RequestLevel(L As Integer, U0 As Double, U1 As Double, V0 As Double, V1 As Double,
                                  BasePriority As Double, WithSkeleton As Boolean) As Integer
        Dim Upt = UnitsPerTile(L)
        Dim Tx0 = CInt(Math.Floor(U0 / Upt)), Tx1 = CInt(Math.Floor(U1 / Upt))
        Dim Tz0 = CInt(Math.Floor(V0 / Upt)), Tz1 = CInt(Math.Floor(V1 / Upt))
        Dim Cx = (Tx0 + Tx1) / 2.0, Cz = (Tz0 + Tz1) / 2.0
        Dim Want As New List(Of TileJob)
        For Tz = Tz0 To Tz1
            For Tx = Tx0 To Tx1
                Dim Key = TileKey(L, Tx, Tz)
                If Tiles.ContainsKey(Key) Then
                    RemoveSkeleton(Key)
                    Continue For
                End If
                If WithSkeleton Then AddSkeleton(L, Tx, Tz)
                If TilePending.Contains(Key) Then Continue For
                Want.Add(New TileJob With {.Serial = GenSerial, .Level = L, .TileX = Tx, .TileZ = Tz,
                    .Seed = SeedValue, .Version = ModuleSeedMap.VersionIds(VersionIndex), .Dimension = Dimension,
                    .Priority = BasePriority + (Math.Abs(Tx - Cx) + Math.Abs(Tz - Cz)) * 0.01})
            Next
        Next
        If Want.Count = 0 Then Return 0
        '单轮上限：一次拖到天边也不会塞几千个任务进队列（超出的连骨架一起撤掉）
        Dim MaxPerRefresh As Integer = FineBudget() + 32
        If Want.Count > MaxPerRefresh Then
            Want.Sort(Function(A, B) A.Priority.CompareTo(B.Priority))
            For i As Integer = MaxPerRefresh To Want.Count - 1
                RemoveSkeleton(TileKey(Want(i).Level, Want(i).TileX, Want(i).TileZ))
            Next
            Want = Want.Take(MaxPerRefresh).ToList()
        End If
        Dim Count As Integer = 0
        SyncLock JobSync
            For Each J In Want
                Dim Key = TileKey(J.Level, J.TileX, J.TileZ)
                If TilePending.Contains(Key) Then Continue For
                TilePending.Add(Key)
                JobQueue.Add(J)
                Count += 1
            Next
        End SyncLock
        If Count > 0 Then
            JobSignal.Release(Count)
            StartTileWorker()
        End If
        Return Count
    End Function

    ''' <summary>撤掉落在指定范围之外的待办瓦片（正在算的那一块撤不掉，没关系）。</summary>
    Private Sub CancelPendingOutside(MinU As Double, MaxU As Double, MinV As Double, MaxV As Double)
        Dim Removed As New List(Of Long)
        SyncLock JobSync
            For i As Integer = JobQueue.Count - 1 To 0 Step -1
                If Not (TypeOf JobQueue(i) Is TileJob) Then Continue For
                Dim J As TileJob = CType(JobQueue(i), TileJob)
                Dim Upt = UnitsPerTile(J.Level)
                Dim Left = J.TileX * Upt, Top = J.TileZ * Upt
                If Left + Upt < MinU OrElse Left > MaxU OrElse Top + Upt < MinV OrElse Top > MaxV Then
                    Removed.Add(TileKey(J.Level, J.TileX, J.TileZ))
                    JobQueue.RemoveAt(i)
                End If
            Next
            For Each Key In Removed
                TilePending.Remove(Key)
            Next
        End SyncLock
        For Each Key In Removed
            RemoveSkeleton(Key)
        Next
    End Sub

    ''' <summary>取出下一个任务：结构扫描最优先，其次按优先级（粗层 → 细层近处 → 细层远处）。</summary>
    Private Function DequeueBest() As Object
        If JobQueue.Count = 0 Then Return Nothing
        Dim Best As Integer = 0
        Dim BestPriority As Double = Double.MaxValue
        For i As Integer = 0 To JobQueue.Count - 1
            If TypeOf JobQueue(i) Is ScanJob Then
                Best = i
                Exit For
            End If
            Dim J As TileJob = CType(JobQueue(i), TileJob)
            If J.Priority < BestPriority Then
                BestPriority = J.Priority
                Best = i
            End If
        Next
        Dim Job As Object = JobQueue(Best)
        JobQueue.RemoveAt(Best)
        Return Job
    End Function

    ''' <summary>
    ''' 只显示"当前档位及更粗"的瓦片。已经缓存的高精度瓦片在缩小后不该继续盖在上面，
    ''' 否则同一屏里会出现精度不一致的补丁。缓存保留，放大回来直接复用。
    ''' </summary>
    Private Sub ApplyLevelVisibility()
        Dim Lf As Integer = CurrentLevel()
        For Each T In Tiles.Values
            Dim Vis As Visibility = If(T.Level >= Lf, Visibility.Visible, Visibility.Collapsed)
            If T.Image.Visibility <> Vis Then T.Image.Visibility = Vis
        Next
    End Sub
    ''' <summary>
    ''' 位图缩放方式：高精度一律锐利；低精度只有"格子已经大到明显是马赛克"（≥14px）才平滑，
    ''' 中等大小仍保持锐利，否则整片都会发糊。
    ''' </summary>

    ''' <summary>缓存淘汰：粗层常驻（它是远景底图），细层超过预算就丢掉离视野中心最远的那批。</summary>
    Private Sub EvictFarTiles(Center As Point)
        Const MaxTiles As Integer = 1200
        Const KeepTiles As Integer = 900
        If Tiles.Count <= MaxTiles Then Return
        Dim Lc = CoarseLevel()
        Dim Far = Tiles.Where(Function(Kv) Kv.Value.Level <> Lc).
            OrderByDescending(Function(Kv)
                                  Dim T = Kv.Value
                                  Dim Bx = (T.TileX + 0.5) * UnitsPerTile(T.Level) * BlocksPerUnit
                                  Dim Bz = (T.TileZ + 0.5) * UnitsPerTile(T.Level) * BlocksPerUnit
                                  Return (Bx - Center.X) ^ 2 + (Bz - Center.Y) ^ 2
                              End Function).ToList()
        Dim RemoveCount = Tiles.Count - KeepTiles
        For Each Kv In Far
            If RemoveCount <= 0 Then Exit For
            Dim T = Kv.Value
            Dim Key = TileKey(T.Level, T.TileX, T.TileZ)
            If Tiles.Remove(Key) Then
                If T.Level = Lc Then PanTilesCoarse.Children.Remove(T.Image) Else PanTiles.Children.Remove(T.Image)
                RemoveCount -= 1
            End If
        Next
    End Sub

    ''' <summary>L0 骨架：入队瞬间就在瓦片位置画一个虚线格，加载过程因此看得见。</summary>
    Private Sub AddSkeleton(L As Integer, Tx As Integer, Tz As Integer)
        Dim Key = TileKey(L, Tx, Tz)
        If Tiles.ContainsKey(Key) OrElse TileSkeletons.ContainsKey(Key) Then Return
        Dim Upt = UnitsPerTile(L)
        Dim Px As Double = 1 / Math.Max(0.0001, Zoom) '屏幕 1px 对应的地图单位
        Dim Rc As New Rectangle With {
            .Width = Math.Max(0.5, Upt - Px * 2), .Height = Math.Max(0.5, Upt - Px * 2),
            .Fill = SkeletonFill, .Stroke = SkeletonStroke,
            .StrokeThickness = Px,
            .StrokeDashArray = New DoubleCollection From {3.0, 3.0},
            .IsHitTestVisible = False}
        Canvas.SetLeft(Rc, Tx * Upt + Px)
        Canvas.SetTop(Rc, Tz * Upt + Px)
        PanSkeleton.Children.Add(Rc)
        TileSkeletons(Key) = Rc
        StartSkeletonPulse()
    End Sub

    Private Sub RemoveSkeleton(Key As Long)
        Dim El As FrameworkElement = Nothing
        If TileSkeletons.TryGetValue(Key, El) Then
            TileSkeletons.Remove(Key)
            PanSkeleton.Children.Remove(El)
        End If
        If TileSkeletons.Count = 0 Then StopSkeletonPulse()
    End Sub

    ''' <summary>骨架层整体呼吸（只跑一条动画，不是每格一条）。</summary>
    Private Sub StartSkeletonPulse()
        If SkeletonAnim IsNot Nothing Then Return
        SkeletonAnim = New DoubleAnimation(0.45, 0.95, New Duration(TimeSpan.FromMilliseconds(820))) With {
            .AutoReverse = True, .RepeatBehavior = RepeatBehavior.Forever}
        PanSkeleton.BeginAnimation(UIElement.OpacityProperty, SkeletonAnim)
    End Sub

    Private Sub StopSkeletonPulse()
        If SkeletonAnim Is Nothing Then Return
        SkeletonAnim = Nothing
        PanSkeleton.BeginAnimation(UIElement.OpacityProperty, Nothing)
        PanSkeleton.Opacity = 1
    End Sub

    ''' <summary>缩放变了：骨架虚线保持屏幕 1px，不然放大后会变成粗边。</summary>
    Private Sub UpdateSkeletonScale()
        If TileSkeletons.Count = 0 OrElse Zoom <= 0 Then Return
        Dim Px As Double = 1 / Math.Max(0.0001, Zoom)
        For Each El In TileSkeletons.Values
            Dim Rc = TryCast(El, Rectangle)
            If Rc Is Nothing Then Continue For
            Rc.StrokeThickness = Px
        Next
    End Sub

    ''' <summary>顶栏进度：视野内细层瓦片的到齐比例（没有细层底子时显示"粗层…"）。</summary>
    Private Sub UpdateLoadUi()
        Try
            If PanLoad Is Nothing OrElse InitialZoom <= 0 Then Return
            Dim Vw = PanMapViewport.ActualWidth, Vh = PanMapViewport.ActualHeight
            If Vw < 50 OrElse Vh < 50 Then Return
            Dim Lf = CurrentLevel()
            Dim Upt = UnitsPerTile(Lf)
            Dim R = ViewUnitRect()
            Dim U0 = R.Left, U1 = R.Right
            Dim V0 = R.Top, V1 = R.Bottom
            Dim Tx0 = CInt(Math.Floor(U0 / Upt)), Tx1 = CInt(Math.Floor(U1 / Upt))
            Dim Tz0 = CInt(Math.Floor(V0 / Upt)), Tz1 = CInt(Math.Floor(V1 / Upt))
            Dim Need As Integer = 0, Done As Integer = 0
            For Tz = Tz0 To Tz1
                For Tx = Tx0 To Tx1
                    Need += 1
                    If Tiles.ContainsKey(TileKey(Lf, Tx, Tz)) Then Done += 1
                Next
            Next
            If Done >= Need Then
                PanLoad.Visibility = Visibility.Collapsed
                LabScaleHint.Visibility = Visibility.Visible
                Return
            End If
            LabScaleHint.Visibility = Visibility.Collapsed
            PanLoad.Visibility = Visibility.Visible
            Dim Pct As Double = If(Need = 0, 1, Done / CDbl(Need))
            RectLoadBar.Width = Math.Max(0, 56 * Pct)
            LabLoad.Text = If(Done = 0 AndAlso TilePending.Count > 0, "粗层…", CInt(Pct * 100) & "%")
        Catch ex As Exception
            Logger.Warn(ex, "更新加载进度失败")
        End Try
    End Sub

    ''' <summary>清空全部瓦片 / 骨架 / 待办（换种子、换精度、开关浮雕时用）。</summary>
    Private Sub ClearTiles()
        Dim Lc = CoarseLevel()
        For Each T In Tiles.Values
            If T.Level = Lc Then PanTilesCoarse.Children.Remove(T.Image) Else PanTiles.Children.Remove(T.Image)
        Next
        Tiles.Clear()
        For Each El In TileSkeletons.Values
            PanSkeleton.Children.Remove(El)
        Next
        TileSkeletons.Clear()
        StopSkeletonPulse()
        SyncLock JobSync
            JobQueue.Clear()
            TilePending.Clear()
        End SyncLock
        '配色/筛选可能变了：近景高度场要重画
    End Sub

    ''' <summary>打包结构扫描任务（必须在 UI 线程调用，读取视野中心与当前清单）。
    ''' 只扫描「已勾选显示」的结构：cubiomes 一次扫描的代价与结构类型数成正比，
    ''' 全量 14 种比只扫 1~2 种慢好几倍；没勾选的类型反正不会画标记，不必算。</summary>
    Private Sub EnqueueScan(Center As Point)
        Dim Types As New List(Of Integer)
        For Each S In Structs
            If S.Supported AndAlso S.Enabled Then Types.Add(S.SType)
        Next
        If Types.Count = 0 Then Return
        Dim Job As New ScanJob With {.Serial = GenSerial, .Version = ModuleSeedMap.VersionIds(VersionIndex),
            .Dimension = Dimension, .Seed = SeedValue, .CenterX = CInt(Center.X), .CenterZ = CInt(Center.Y),
            .Types = Types.ToArray()}
        SyncLock JobSync
            If ScanQueued Then
                '已经有一个扫描在队列里：把这次勾选的类型合并进去，避免勾选请求被丢掉
                For Each Existing In JobQueue
                    Dim SJ = TryCast(Existing, ScanJob)
                    If SJ Is Nothing OrElse SJ.Serial <> GenSerial Then Continue For
                    Dim Has As New List(Of Integer)(SJ.Types)
                    Dim Added As Boolean = False
                    For Each T In Types
                        If Not Has.Contains(T) Then
                            Has.Add(T)
                            ScanCovered.Add(T)
                            Added = True
                        End If
                    Next
                    If Added Then SJ.Types = Has.ToArray()
                    Return
                Next
            End If
            ScanQueued = True
            For Each T In Types
                ScanCovered.Add(T)
            Next
            JobQueue.Add(Job)
        End SyncLock
        JobSignal.Release(1)
        StartTileWorker()
    End Sub

    ''' <summary>
    ''' 并行 worker 数：每个线程一份 cubiomes 句柄，互不共享状态。
    ''' 旧 DLL（没有句柄式接口）时自动退回 1 个，行为与以前一致。
    ''' </summary>
    Private ReadOnly WorkerCount As Integer = CalcWorkerCount()

    Private Shared Function CalcWorkerCount() As Integer
        Try
            If Not ModuleSeedMap.HasCtxApi Then Return 1
            Dim N As Integer = Environment.ProcessorCount - 2 '给 UI 线程和系统留核
            If N < 2 Then N = 2
            If N > 4 Then N = 4
            Return N
        Catch ex As Exception
            Return 1
        End Try
    End Function

    ''' <summary>启动工作线程池（只启动一次，之后常驻；退出程序时随之结束）。</summary>
    Private Sub StartTileWorker()
        SyncLock JobSync
            If ActiveWorkers > 0 Then Return
            ActiveWorkers = WorkerCount
        End SyncLock
        For i As Integer = 1 To WorkerCount
            '必须用独立线程，不能用线程池：这几个循环是常驻的（一直等队列），
            '占着线程池会把启动器自己的加载任务（版本列表 / 实例列表）挤到后面，
            '表现就是主页"开始游戏"按钮先卡在"正在加载"一会儿。
            Dim Th As New Threading.Thread(AddressOf WorkerLoop) With {
                .IsBackground = True,
                .Name = "SeedMap Worker " & i,
                .Priority = Threading.ThreadPriority.BelowNormal}
            Th.Start()
        Next
    End Sub
    ''' <summary>一个 worker：从同一个优先级队列里取任务 → 算 → 回 UI 线程贴图。多个 worker 并行。</summary>
    Private Sub WorkerLoop()
        Dim IdleCount As Integer = 0
        While True
            Dim Job As Object = Nothing
            SyncLock JobSync
                Job = DequeueBest()
                If TypeOf Job Is ScanJob Then ScanQueued = False
            End SyncLock
            If Job Is Nothing Then
                '队列空了就等信号；连续约 10 秒没活就退出线程，别长期占着
                IdleCount += 1
                If IdleCount > 40 Then
                    SyncLock JobSync
                        ActiveWorkers -= 1
                        If ActiveWorkers < 0 Then ActiveWorkers = 0
                    End SyncLock
                    Return
                End If
                JobSignal.Wait(250)
                Continue While
            End If
            IdleCount = 0
            Try
                If TypeOf Job Is TileJob Then
                    Dim J As TileJob = CType(Job, TileJob)
                    '磁盘缓存命中：直接用存下来的群系 ID 上色，跳过采样（一张约 20ms，开浮雕再 +11.5ms）
                    Dim CacheRelief As Boolean = ReliefEnabled AndAlso J.Dimension = 0
                    Dim CachedIds As Integer() = Nothing
                    Dim CachedHs As Single() = Nothing
                    If SeedMapCache.TryLoad(J.Seed, J.Version, J.Dimension, J.Level, J.TileX, J.TileZ,
                                            CacheRelief, CachedIds, CachedHs) Then
                        '浮雕开着、但缓存里没带高度（这张是关浮雕时缓存的）：当作未命中重新采样，
                        '顺带把高度补写进缓存，否则这块区域会一直是纯群系配色
                        If Not CacheRelief OrElse CachedHs IsNot Nothing Then
                            RunInUi(Sub() ApplyTile(J, CachedIds, CachedHs))
                            Continue While
                        End If
                    End If
                    '原生调用：走本线程自己的句柄，多线程并行；位图着色留在 UI 线程以读取最新的地形筛选
                    Dim Ids = ModuleSeedMap.SampleBiomeGrid(J.Seed, J.Version, J.Dimension,
                                                            J.TileX * TileCells * J.Level, J.TileZ * TileCells * J.Level, TileCells, J.Level)
                    '浮雕：同一块区域再采一次地表高度（只在主世界有意义）
                    Dim Hs As Single() = Nothing
                    If ReliefEnabled AndAlso J.Dimension = 0 Then
                        Dim Step4 As Integer = Math.Max(1, J.Level \ 4)
                        Hs = ModuleSeedMap.SampleHeights(J.Version, J.Seed,
                                                         (J.TileX * TileCells * J.Level) \ 4,
                                                         (J.TileZ * TileCells * J.Level) \ 4,
                                                         TileCells, Step4)
                    End If
                    '采样结果落盘，下次翻回同一片区域就不必再算
                    SeedMapCache.SaveAsync(J.Seed, J.Version, J.Dimension, J.Level, J.TileX, J.TileZ, Ids, Hs)
                    RunInUi(Sub() ApplyTile(J, Ids, Hs))
                Else
                    Dim S As ScanJob = CType(Job, ScanJob)
                    Dim Result = ModuleSeedMap.ScanStructures(S.Seed, S.Version, S.Dimension, S.Types,
                        S.CenterX - ScanRadius, S.CenterZ - ScanRadius,
                        S.CenterX + ScanRadius, S.CenterZ + ScanRadius)
                    RunInUi(Sub()
                                If S.Serial = GenSerial Then ApplyScan(Result)
                            End Sub)
                End If
            Catch ex As Exception
                Logger.Warn(ex, "种子地图瓦片任务失败")
                RunInUi(Sub() RefreshPendingClear())
            End Try
        End While
    End Sub

    ''' <summary>任务失败时清空挂起标记，避免同键瓦片被永久跳过。</summary>
    Private Sub RefreshPendingClear()
        SyncLock JobSync
            JobQueue.Clear()
            TilePending.Clear()
        End SyncLock
        For Each El In TileSkeletons.Values
            PanSkeleton.Children.Remove(El)
        Next
        TileSkeletons.Clear()
        StopSkeletonPulse()
        UpdateLoadUi()
    End Sub

    Private Sub ApplyTile(J As TileJob, Ids As Integer(), Optional Heights As Single() = Nothing)
        Dim Key = TileKey(J.Level, J.TileX, J.TileZ)
        SyncLock JobSync
            TilePending.Remove(Key)
        End SyncLock
        If J.Serial <> GenSerial OrElse Tiles.ContainsKey(Key) Then
            '任务作废 / 已有同键瓦片：也要把占位框撤掉，否则那个虚线格子会一直挂在那儿
            RemoveSkeleton(Key)
            UpdateLoadUi()
            Return
        End If
        Dim Upt = UnitsPerTile(J.Level)
        Dim Img As New Image With {
            .Source = BuildBitmap(Ids, Heights, TileCells, TileCells), .Width = Upt, .Height = Upt, .Opacity = 0,
            .Stretch = Stretch.Fill}
        '高精度用最近邻（格子锐利），低精度用线性插值（平滑过渡，不再一块块马赛克）
        RenderOptions.SetBitmapScalingMode(Img, BitmapScalingMode.NearestNeighbor)
        Canvas.SetLeft(Img, J.TileX * Upt)
        Canvas.SetTop(Img, J.TileZ * Upt)
        '粗层走独立画布（永远垫在细层下面），细层照旧
        '精度决定叠放层级：越精细越靠上（1000 - 采样方块数）。
        '以前靠添加顺序决定谁在上面，来回缩放后旧的粗瓦片会盖住细瓦片，
        '同一屏里就出现一块块精度不一样的补丁。
        Panel.SetZIndex(Img, 2000 - J.Level)
        '比当前档位更精细的瓦片先不显示（缩小状态下不该露出高精度补丁）
        If J.Level < CurrentLevel() Then Img.Visibility = Visibility.Collapsed
        If J.Level = CoarseLevel() Then PanTilesCoarse.Children.Add(Img) Else PanTiles.Children.Add(Img)
        Tiles(Key) = New TileEntry With {.Image = Img, .Ids = Ids, .Heights = Heights, .Level = J.Level, .TileX = J.TileX, .TileZ = J.TileZ}
        RemoveSkeleton(Key)
        AniStart({AaOpacity(Img, 1, If(J.Level = CoarseLevel(), 140, 200), , New AniEaseOutFluent)}, "SeedMap Tile " & Key, True)
        UpdateLoadUi()
    End Sub

    '群系 id 网格 → 着色位图（可在后台线程构建，构建后冻结）。
    '地形筛选生效时，未选中的群系以高透明度显示，凸显选中地形。
    Private Function BuildBitmap(Ids As Integer(), Heights As Single(), W As Integer, H As Integer) As BitmapSource
        Dim Filter As Boolean = SelectedBiomes.Count > 0
        Dim Px(W * H * 4 - 1) As Byte
        Dim UseRelief As Boolean = ReliefEnabled AndAlso Heights IsNot Nothing AndAlso Heights.Length >= W * H
        For J As Integer = 0 To H - 1
            For i As Integer = 0 To W - 1
                Dim k As Integer = J * W + i
                Dim Id As Integer = Ids(k)
                '浮雕需要西北两个邻居的高度；边界上退化成自己（等高线判定自然为假）
                Dim Hh As Double = If(UseRelief, Heights(k), 0)
                Dim Hw As Double = If(UseRelief AndAlso i > 0, Heights(k - 1), Hh)
                Dim Hn As Double = If(UseRelief AndAlso J > 0, Heights(k - W), Hh)
                Dim C As Color = TerrainColor(Id, Hh, Hw, Hn, UseRelief)
                Dim R2 As Integer = C.R, G2 As Integer = C.G, B2 As Integer = C.B
                Dim Idx As Integer = If(Id < 0 OrElse Id > 255, 127, Id)
                If Filter AndAlso Not SelectedBiomes.Contains(Idx) Then
                    '未选中的群系：压暗并混向地图底色，保持完全不透明。
                    '以前是把 alpha 降到 38，结果下面的粗层瓦片透上来，看起来像多出一层图层。
                    R2 = (R2 * 22 + 14 * 78) \ 100
                    G2 = (G2 * 22 + 18 * 78) \ 100
                    B2 = (B2 * 22 + 26 * 78) \ 100
                End If
                'Bgra32 的字节序为 B,G,R,A
                Px(k * 4) = CByte(B2) : Px(k * 4 + 1) = CByte(G2) : Px(k * 4 + 2) = CByte(R2)
                Px(k * 4 + 3) = 255
            Next
        Next
        Dim Bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, Nothing, Px, W * 4)
        Bmp.Freeze()
        Return Bmp
    End Function

    ''' <summary>
    ''' 群系 + 高度 → 顶面颜色。2D 瓦片与（后续的）近景高度场渲染共用这一个函数，
    ''' 保证两条渲染路径切换时颜色不跳。Hw / Hn 为西北两个邻居的高度（浮雕坡向用）。
    ''' </summary>
    Private Shared Function TerrainColor(Id As Integer, Hh As Double, Hw As Double, Hn As Double, UseRelief As Boolean) As Color
        If Id < 0 OrElse Id > 255 Then Id = 127
        Dim C As Color = ModuleSeedMap.BiomeColors(Id)
        If Not UseRelief Then Return C
        '浮雕：高度 → 明暗，叠加西北光照的坡向着色与等高线
        Dim T As Double = Math.Max(0, Math.Min(1, (Hh - 40) / 120))
        Dim Br As Double = 0.55 + T * 0.75
        Dim Slope As Double = (Hh - Hw) + (Hh - Hn)
        Br += Math.Max(-0.22, Math.Min(0.22, Slope * 0.05))
        '每 8 格高度一条等高线（和截图里的分层线一致）
        If Math.Floor(Hh / 8) <> Math.Floor(Hw / 8) Then Br *= 0.8
        If Math.Floor(Hh / 8) <> Math.Floor(Hn / 8) Then Br *= 0.86
        If Hh < 63 Then Br *= 0.9 '水面略压暗
        Return Color.FromRgb(CByte(CInt(Math.Max(0, Math.Min(255, C.R * Br)))),
                             CByte(CInt(Math.Max(0, Math.Min(255, C.G * Br)))),
                             CByte(CInt(Math.Max(0, Math.Min(255, C.B * Br)))))
    End Function

    ''' <summary>筛选变化后，按缓存的群系网格重新为所有瓦片着色。
    ''' 分批重算（每批 24 张）并让出消息循环，避免一次性重算几百张 64×64 位图把界面卡住。</summary>
    Private ColorSerial As Integer = 0
    Private Sub RefreshTileColors()
        ColorSerial += 1
        Dim MySerial As Integer = ColorSerial
        Dim Snap = Tiles.Values.ToList()
        If Snap.Count = 0 Then Return
        Dim Idx As Integer = 0
        Dim StepBatch As Action = Nothing
        StepBatch =
            Sub()
                If MySerial <> ColorSerial Then Return
                Dim N As Integer = 0
                While Idx < Snap.Count AndAlso N < 24
                    Dim T = Snap(Idx)
                    T.Image.Source = BuildBitmap(T.Ids, T.Heights, TileCells, TileCells)
                    Idx += 1
                    N += 1
                End While
                If Idx < Snap.Count Then Dispatcher.BeginInvoke(StepBatch, DispatcherPriority.Background)
            End Sub
        StepBatch()
        '筛选变了，3D 预览里的淡化/高亮也要跟着变
        Refresh3DIfOpen()
    End Sub

    '地图单位处的群系名（先查细层瓦片，没算完就退回粗层底图，避免鼠标位置一直显示"…"）
    Private Function BiomeNameAt(ux As Double, uy As Double) As String
        Dim Entry As TileEntry = Nothing
        Dim L As Integer = CurrentLevel()
        Dim Upt As Double = UnitsPerTile(L)
        Dim Tx As Integer = CInt(Math.Floor(ux / Upt)), Tz As Integer = CInt(Math.Floor(uy / Upt))
        If Not Tiles.TryGetValue(TileKey(L, Tx, Tz), Entry) Then
            L = CoarseLevel()
            Upt = UnitsPerTile(L)
            Tx = CInt(Math.Floor(ux / Upt)) : Tz = CInt(Math.Floor(uy / Upt))
            If Not Tiles.TryGetValue(TileKey(L, Tx, Tz), Entry) Then Return "…"
        End If
        Dim Cell = ClampD((ux - Tx * Upt) / (L / BlocksPerUnit), 0, TileCells - 1)
        Dim CellV = ClampD((uy - Tz * Upt) / (L / BlocksPerUnit), 0, TileCells - 1)
        Return ModuleSeedMap.BiomeName(Entry.Ids(CInt(CellV) * TileCells + CInt(Cell)))
    End Function

    '===============================
    '  重新生成（种子 / 维度 / 版本变化）
    '===============================

    Private Sub RegenerateAll(Recenter As Boolean)
        If Not ModuleSeedMap.IsNativeAvailable Then
            Hint("缺少 cubiomes.dll，无法生成种子地图", HintType.Red)
            Return
        End If
        '重新「查看地图」（换种子 / 换版本 / 切维度）时，先收起 3D 预览
        CloseView3DNow()
        GenSerial += 1
        '种子/版本/维度变了，之前的扫描结果全部作废
        ScanCovered.Clear()
        SyncLock JobSync
            ScanQueued = False
        End SyncLock
        ClearTiles()
        PanMarkers.Children.Clear()
        StructPoints.Clear()
        LastScanCenter = New Point(Double.NaN, Double.NaN)
        '种子/维度/版本变了，网格与史莱姆的覆盖缓存也要作废
        GridCover = Rect.Empty
        SlimeCover = Rect.Empty
        SlimeCache.Clear()
        For i = 0 To Structs.Count - 1
            Structs(i).Count = 0
            If i < StructCountLabels.Count Then StructCountLabels(i).Text = "…"
        Next
        If Recenter Then
            Dim Center As New Point(0, 0)
            If Dimension = 0 Then
                Try
                    Center = ModuleSeedMap.GetSpawn(SeedValue, ModuleSeedMap.VersionIds(VersionIndex))
                Catch ex As Exception
                    Logger.Warn(ex, "估计出生点失败，改用世界原点")
                End Try
            End If
            Zoom = DefaultZoomForDim(Dimension)
            CenterOn(Center.X, Center.Y)
            SaveDimView(Dimension)
        Else
            RefreshTiles(True)
        End If
        '立刻排一次结构扫描，不依赖瓦片刷新那 120ms 的节流，版本/种子一换标记就尽快出来
        EnqueueScan(ViewCenter())
    End Sub

    ''' <summary>应用结构扫描结果：更新计数与标记。
    ''' 扫描任务只包含已勾选的结构，其余类型按"未扫描"处理（空列表，计数显示 …）。</summary>
    Private Sub ApplyScan(Result As Dictionary(Of Integer, List(Of Point)))
        StructPoints.Clear()
        For i = 0 To Structs.Count - 1
            Dim Def = Structs(i)
            Dim Pts As List(Of Point) = Nothing
            If Def.Supported AndAlso Def.Enabled AndAlso Result.TryGetValue(Def.SType, Pts) Then
                StructPoints.Add(Pts)
                Def.Count = Pts.Count
                If i < StructCountLabels.Count Then StructCountLabels(i).Text = Pts.Count & " 处"
            Else
                '未勾选 / 不支持：没有扫描数据
                StructPoints.Add(New List(Of Point))
                Def.Count = 0
                If i < StructCountLabels.Count Then StructCountLabels(i).Text = If(Def.Supported, "…", "不支持")
            End If
        Next
        RefreshStructStates()
        RebuildMarkers()
        '结构位置刚扫出来：已打开的 3D 预览补上结构标记
        Refresh3DIfOpen()
    End Sub

    '===============================
    '  结构标记
    '===============================

    Private Sub RebuildMarkers()
        PanMarkers.Children.Clear()
        For si = 0 To Structs.Count - 1
            If Not Structs(si).Enabled Then Continue For
            If si >= StructPoints.Count Then Continue For
            For Each P In StructPoints(si)
                Dim U = ToUnit(P.X, P.Y)
                PanMarkers.Children.Add(BuildMarker(Structs(si), U.X, U.Y, P))
            Next
        Next
        UpdateMarkerScale()
    End Sub

    '构建单个结构标记：图标本体直接钉在结构坐标上，纯文字名称悬浮在图标下方（110×40，锚点在图标中心）
    Private Function BuildMarker(Def As StructDef, ux As Double, uy As Double, Block As Point) As FrameworkElement
        Dim Root As New Grid With {.Width = 110, .Height = 46, .RenderTransformOrigin = New Point(0.5, 17.0 / 46.0)}
        '透明热区：图标本身只有 22px，太小不好瞄；放大到 34px 只影响命中范围，不影响外观
        Dim Hit As New Border With {.Width = 34, .Height = 34, .CornerRadius = New CornerRadius(17),
            .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Top,
            .Background = Brushes.Transparent, .Cursor = Cursors.Hand}
        Dim Ico As New Image With {.Source = GetStructIcon(Def.IconKey), .Width = 22, .Height = 22,
            .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center,
            .RenderTransformOrigin = New Point(0.5, 0.5), .RenderTransform = New ScaleTransform(1, 1)}
        Hit.Child = Ico
        Root.Children.Add(Hit)
        Dim Lab As New TextBlock With {.Text = Def.Name, .FontSize = 10.5, .Foreground = Brushes.White,
            .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Top,
            .Margin = New Thickness(0, 30, 0, 0), .IsHitTestVisible = False}
        '结构名字是否常显：关闭时只在鼠标移上去才显示，视野更干净
        Dim NamesAlways As Boolean = True
        Try
            NamesAlways = Settings.Get(Of Boolean)("SeedMapStructNamesAlways")
        Catch ex As Exception
        End Try
        Lab.Visibility = If(NamesAlways, Visibility.Visible, Visibility.Collapsed)
        Lab.Effect = New DropShadowEffect With {.BlurRadius = 2, .ShadowDepth = 1, .Direction = 270,
            .Opacity = 0.85, .Color = Colors.Black, .RenderingBias = RenderingBias.Performance}
        Root.Children.Add(Lab)
        Canvas.SetLeft(Root, ux - 55)
        Canvas.SetTop(Root, uy - 17)
        Root.RenderTransform = New ScaleTransform(1 / Zoom, 1 / Zoom)

        '---- 悬停表现：图标弹性放大、整块提到最上层、信息卡立刻弹出 ----
        Dim IconScale As ScaleTransform = CType(Ico.RenderTransform, ScaleTransform)
        Dim TipText As New TextBlock With {.FontSize = 12.5, .Foreground = TryCast(FindResource("BrushText1"), Brush)}
        Dim Tip As New ToolTip With {.Content = TipText, .Placement = PlacementMode.Mouse, .Padding = New Thickness(9, 6, 9, 6)}
        Hit.ToolTip = Tip
        ToolTipService.SetInitialShowDelay(Hit, 0)
        ToolTipService.SetBetweenShowDelay(Hit, 0)
        ToolTipService.SetShowDuration(Hit, 60000)

        '文字必须在 ToolTip 打开之前填好：以前是等 Opened 才赋值，
        '弹出层已经按"空内容"测好尺寸，所以卡片是空的
        Dim RefreshTip As Action =
            Sub()
                Dim Bio As String = "…"
                Try
                    Bio = BiomeNameAt(ux, uy)
                Catch ex As Exception
                End Try
                TipText.Text = $"{Def.Name}{vbCrLf}X {Block.X} · Z {Block.Y}{vbCrLf}{Bio}"
            End Sub
        RefreshTip()

        AddHandler Hit.MouseEnter,
            Sub()
                RefreshTip()
                If Not NamesAlways Then Lab.Visibility = Visibility.Visible
                AnimateMarkerScale(IconScale, 1.4, 150)
                Panel.SetZIndex(Root, 900) '盖住相邻标记
            End Sub
        AddHandler Hit.MouseLeave,
            Sub()
                If Not NamesAlways Then Lab.Visibility = Visibility.Collapsed
                AnimateMarkerScale(IconScale, 1.0, 190)
                Panel.SetZIndex(Root, 0)
            End Sub
        Return Root
    End Function

    ''' <summary>标记悬停缩放：带一点回弹的短动画，比直接跳变自然。</summary>
    Private Shared Sub AnimateMarkerScale(T As ScaleTransform, Target As Double, Ms As Integer)
        If T Is Nothing Then Return
        Dim FromV As Double = T.ScaleX '动画进行中读到的是当前插值，从当前值接着走
        Dim Dur As New Duration(TimeSpan.FromMilliseconds(Ms))
        Dim Ease As New BackEase With {.Amplitude = 0.35, .EasingMode = EasingMode.EaseOut}
        T.BeginAnimation(ScaleTransform.ScaleXProperty, New DoubleAnimation(FromV, Target, Dur) With {.EasingFunction = Ease})
        T.BeginAnimation(ScaleTransform.ScaleYProperty, New DoubleAnimation(FromV, Target, Dur) With {.EasingFunction = Ease})
    End Sub
    '更新全部标记的反向缩放（纵向要额外除以 sinθ：父层被压扁了，图标得撑回去才不变形）
    Private Sub UpdateMarkerScale()
        Dim Sy As Double = 1 / Math.Max(0.0001, Zoom)
        For Each El In PanMarkers.Children
            Dim M = TryCast(El, FrameworkElement)
            If M IsNot Nothing AndAlso TypeOf M.RenderTransform Is ScaleTransform Then
                CType(M.RenderTransform, ScaleTransform).ScaleX = 1 / Zoom
                CType(M.RenderTransform, ScaleTransform).ScaleY = Sy
            End If
        Next
    End Sub

    '===============================
    '  结构面板
    '===============================

    Private Sub BuildStructList()
        PanStructList.Children.Clear()
        StructCountLabels.Clear()
        Dim HoverBrush = TryCast(FindResource("BrushHover"), Brush)
        For i = 0 To Structs.Count - 1
            Dim Index = i
            Dim Row As New Border With {.Height = 34, .CornerRadius = New CornerRadius(7),
                .Padding = New Thickness(13, 0, 12, 0), .Cursor = Cursors.Hand, .Background = Brushes.Transparent,
                .Tag = Index, .SnapsToDevicePixels = True}
            Dim G As New Grid
            G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            Dim Left As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
            Dim Ico As New Image With {.Source = GetStructIcon(Structs(i).IconKey), .Width = 16, .Height = 16, .VerticalAlignment = VerticalAlignment.Center}
            Dim Lab As New TextBlock With {.Text = Structs(i).Name, .FontSize = 12.5, .VerticalAlignment = VerticalAlignment.Center,
                .Margin = New Thickness(9, 0, 0, 0), .Foreground = TryCast(FindResource("BrushText1"), Brush)}
            Left.Children.Add(Ico) : Left.Children.Add(Lab)
            Grid.SetColumn(Left, 0)
            G.Children.Add(Left)
            Dim Count As New TextBlock With {.Text = If(Structs(i).Supported, "…", "不支持"), .FontSize = 11,
                .Foreground = TryCast(FindResource("BrushText3"), Brush), .VerticalAlignment = VerticalAlignment.Center}
            Grid.SetColumn(Count, 1)
            G.Children.Add(Count)
            StructCountLabels.Add(Count)
            Row.Child = G
            AddHandler Row.MouseEnter, Sub(s, e) Row.Background = HoverBrush
            AddHandler Row.MouseLeave, Sub(s, e) Row.Background = Brushes.Transparent
            If Structs(i).Supported Then
                AddHandler Row.MouseLeftButtonUp, Sub(s, e) SetStructEnabled(Index, Not Structs(Index).Enabled)
            Else
                Row.Cursor = Cursors.Arrow
                Row.ToolTip = "cubiomes 不支持定位地牢：怪物房随洞穴随机生成，无法由种子推算"
            End If
            PanStructList.Children.Add(Row)
        Next
        RefreshStructStates()
    End Sub

    Private Sub SetStructEnabled(Index As Integer, Value As Boolean)
        If Not Structs(Index).Supported Then Return
        Structs(Index).Enabled = Value
        RefreshStructStates()
        RebuildMarkers()
        '刚勾选的结构若还没被这一轮扫描覆盖过：立刻补一次（只扫勾选的类型，代价很小）
        If Value AndAlso Not ScanCovered.Contains(Structs(Index).SType) Then
            EnqueueScan(ViewCenter())
        End If
        '结构变了，已打开的 3D 预览也要跟着更新
        Refresh3DIfOpen()
        SaveStructs()
    End Sub

    ''' <summary>
    ''' 某个维度一项结构都没勾选时，默认勾上它的主结构：
    ''' 主世界→村庄，下界→下界要塞，末地→末地城。
    ''' </summary>
    Private Sub EnsureDefaultStruct()
        If Structs Is Nothing Then Return
        '该维度保存过勾选就不要再套默认值（哪怕用户是刻意全部取消）
        Dim DimIdx As Integer = StructLists.IndexOf(Structs)
        If DimIdx >= 0 AndAlso DimIdx < 3 AndAlso StructSaved(DimIdx) Then Return
        For Each S In Structs
            If S.Enabled AndAlso S.Supported Then Return
        Next
        For Each S In Structs
            If S.IsDefault AndAlso S.Supported Then
                S.Enabled = True
                Return
            End If
        Next
    End Sub

    ''' <summary>3D 预览开着的时候，地形筛选 / 结构变化后重新算一遍颜色和标记。</summary>
    Private Sub Refresh3DIfOpen()
        Try
            If PanView3D Is Nothing OrElse PanView3D.Visibility <> Visibility.Visible Then Return
            If _3DN <= 0 OrElse _3DHeights Is Nothing Then Return
            Prepare3DCells()
            Render3D()
        Catch ex As Exception
            Logger.Warn(ex, "刷新 3D 预览失败")
        End Try
    End Sub

    Private Sub RefreshStructStates()
        Dim Selected = 0
        For i = 0 To Structs.Count - 1
            If Structs(i).Enabled Then Selected += 1
            Dim Row = TryCast(PanStructList.Children(i), Border)
            If Row IsNot Nothing Then Row.Opacity = If(Structs(i).Enabled, 1.0, 0.4)
        Next
        LabStructCount.Text = $"已选 {Selected} 项"
    End Sub

    Private Sub TxtStructSearch_TextChanged(sender As Object, e As TextChangedEventArgs)
        Dim Query = TxtStructSearch.Text.Trim()
        For i = 0 To Structs.Count - 1
            Dim Row = TryCast(PanStructList.Children(i), Border)
            If Row IsNot Nothing Then Row.Visibility = If(Query = "" OrElse Structs(i).Name.Contains(Query), Visibility.Visible, Visibility.Collapsed)
        Next
    End Sub

    Private Sub BtnStructAll_Click(sender As Object, e As RoutedEventArgs) Handles BtnStructAll.Click
        For i = 0 To Structs.Count - 1
            Structs(i).Enabled = True
        Next
        RefreshStructStates()
        RebuildMarkers()
        SaveStructs()
    End Sub

    Private Sub BtnStructNone_Click(sender As Object, e As RoutedEventArgs) Handles BtnStructNone.Click
        For i = 0 To Structs.Count - 1
            Structs(i).Enabled = False
        Next
        RefreshStructStates()
        RebuildMarkers()
        SaveStructs()
    End Sub

    '结构面板开关
    Private STRUCT_PANEL_OPEN As Boolean = False
    Private Sub SetStructPanel(Open As Boolean)
        If STRUCT_PANEL_OPEN = Open Then Return
        If Open Then SetBiomePanel(False)
        STRUCT_PANEL_OPEN = Open
        AniStop("SeedMap StructPanel")
        If Open Then
            PanStructures.Visibility = Visibility.Visible
            PanStructures.RenderTransform = New TranslateTransform(-18, 0)
            PanStructures.Opacity = 0
            AniStart({
                AaTranslateX(PanStructures, 18, 300, , New AniEaseOutBack(AniEasePower.Weak)),
                AaOpacity(PanStructures, 1, 130)
            }, "SeedMap StructPanel", True)
        Else
            PanStructures.RenderTransform = New TranslateTransform(0, 0)
            AniStart({
                AaTranslateX(PanStructures, -14, 140, , New AniEaseInFluent),
                AaOpacity(PanStructures, -1, 140),
                AaCode(Sub() PanStructures.Visibility = Visibility.Collapsed, 150, True)
            }, "SeedMap StructPanel", True)
        End If
        If Open Then
            BtnToolStruct.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnToolStruct.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
        Else
            BtnToolStruct.Background = Brushes.Transparent
            BtnToolStruct.Foreground = TryCast(FindResource("BrushText2"), Brush)
        End If
    End Sub

    Private Sub BtnStructClose_Click(sender As Object, e As RoutedEventArgs) Handles BtnStructClose.Click
        SetStructPanel(False)
    End Sub

    '===============================
    '  地形寻找面板（筛选高亮群系）
    '===============================

    '地形面板清单（按维度；对齐 seedatlas 的地形分组）
    Private Function BiomeGroups() As List(Of (Title As String, Ids As Integer()))
        Select Case Dimension
            Case 1
                Return New List(Of (String, Integer())) From {("下界", {8, 170, 171, 172, 173})}
            Case 2
                Return New List(Of (String, Integer())) From {("末地", {9, 40, 41, 42, 43})}
            Case Else
                Return New List(Of (String, Integer())) From {
                    ("平原", {1, 12, 129, 140, 14, 35}),
                    ("林地", {4, 5, 21, 23, 27, 29, 30, 32, 132, 155, 160, 168, 178, 185, 186}),
                    ("洞穴", {174, 175, 183}),
                    ("山地", {3, 34, 25, 36, 131, 163, 177, 179, 180, 181, 182}),
                    ("沼泽", {6, 184}),
                    ("沙漠", {2, 16, 26, 37, 38, 165}),
                    ("海洋", {0, 7, 10, 11, 24, 44, 45, 46, 47, 48, 49, 50}),
                    ("旧版地形", {13, 15, 17, 18, 19, 20, 22, 28, 31, 33, 39, 130, 133, 134, 149, 151, 156, 157, 158, 161, 162, 164, 166, 167, 169})
                }
        End Select
    End Function

    Private Sub BuildBiomeList()
        PanBiomeList.Children.Clear()
        BiomeRows.Clear()
        BiomeRowIds.Clear()
        Dim HoverBrush = TryCast(FindResource("BrushHover"), Brush)
        Dim AccentBrush = TryCast(FindResource("BrushAccentSoft"), Brush)
        For Each Group In BiomeGroups()
            Dim Header As New TextBlock With {.Text = Group.Title, .FontSize = 11, .Foreground = TryCast(FindResource("BrushText3"), Brush),
                .Margin = New Thickness(13, 9, 0, 3)}
            PanBiomeList.Children.Add(Header)
            For Each Id In Group.Ids
                Dim BiomeId = Id
                BiomeRowIds.Add(Id)
                Dim Row As New Border With {.Height = 30, .CornerRadius = New CornerRadius(7),
                    .Padding = New Thickness(13, 0, 12, 0), .Cursor = Cursors.Hand, .Background = Brushes.Transparent,
                    .SnapsToDevicePixels = True}
                Dim G As New Grid
                Dim Swatch As New Border With {.Width = 13, .Height = 13, .CornerRadius = New CornerRadius(3),
                    .Background = New SolidColorBrush(ModuleSeedMap.BiomeColors(Id)), .VerticalAlignment = VerticalAlignment.Center}
                Dim Lab As New TextBlock With {.Text = ModuleSeedMap.BiomeName(Id), .FontSize = 12.5, .VerticalAlignment = VerticalAlignment.Center,
                    .Margin = New Thickness(9, 0, 0, 0), .Foreground = TryCast(FindResource("BrushText1"), Brush)}
                Dim Sp As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
                Sp.Children.Add(Swatch) : Sp.Children.Add(Lab)
                G.Children.Add(Sp)
                Row.Child = G
                AddHandler Row.MouseEnter,
                    Sub(s, e)
                        If Not SelectedBiomes.Contains(BiomeId) Then Row.Background = HoverBrush
                    End Sub
                AddHandler Row.MouseLeave, Sub(s, e) Row.Background = If(SelectedBiomes.Contains(BiomeId), AccentBrush, Brushes.Transparent)
                AddHandler Row.MouseLeftButtonUp, Sub(s, e) ToggleBiome(BiomeId)
                BiomeRows(Id) = Row
                PanBiomeList.Children.Add(Row)
            Next
        Next
        RefreshBiomeStates()
    End Sub

    Private Sub ToggleBiome(Id As Integer)
        If SelectedBiomes.Contains(Id) Then
            SelectedBiomes.Remove(Id)
        Else
            SelectedBiomes.Add(Id)
        End If
        RefreshBiomeStates()
        RefreshTileColors()
        SaveBiomes()
    End Sub

    Private Sub RefreshBiomeStates()
        Dim AccentBrush = TryCast(FindResource("BrushAccentSoft"), Brush)
        For Each Pair In BiomeRows
            Pair.Value.Background = If(SelectedBiomes.Contains(Pair.Key), AccentBrush, Brushes.Transparent)
        Next
        LabBiomeCount.Text = If(SelectedBiomes.Count = 0, "未筛选", $"已选 {SelectedBiomes.Count} 项")
        RefreshBiomeButton()
    End Sub

    Private Sub TxtBiomeSearch_TextChanged(sender As Object, e As TextChangedEventArgs)
        Dim Query = TxtBiomeSearch.Text.Trim()
        For Each Pair In BiomeRows
            Pair.Value.Visibility = If(Query = "" OrElse ModuleSeedMap.BiomeName(Pair.Key).Contains(Query), Visibility.Visible, Visibility.Collapsed)
        Next
    End Sub

    Private Sub BtnBiomeAll_Click(sender As Object, e As RoutedEventArgs) Handles BtnBiomeAll.Click
        SelectedBiomes.Clear()
        For Each Id In BiomeRowIds
            SelectedBiomes.Add(Id)
        Next
        RefreshBiomeStates()
        RefreshTileColors()
        SaveBiomes()
    End Sub

    Private Sub BtnBiomeNone_Click(sender As Object, e As RoutedEventArgs) Handles BtnBiomeNone.Click
        SelectedBiomes.Clear()
        RefreshBiomeStates()
        RefreshTileColors()
        SaveBiomes()
    End Sub

    '地形面板开关（与结构面板互斥）
    Private BIOME_PANEL_OPEN As Boolean = False
    Private Sub SetBiomePanel(Open As Boolean)
        If BIOME_PANEL_OPEN = Open Then Return
        If Open Then SetStructPanel(False)
        BIOME_PANEL_OPEN = Open
        AniStop("SeedMap BiomePanel")
        If Open Then
            PanBiomes.Visibility = Visibility.Visible
            PanBiomes.RenderTransform = New TranslateTransform(-18, 0)
            PanBiomes.Opacity = 0
            AniStart({
                AaTranslateX(PanBiomes, 18, 300, , New AniEaseOutBack(AniEasePower.Weak)),
                AaOpacity(PanBiomes, 1, 130)
            }, "SeedMap BiomePanel", True)
        Else
            PanBiomes.RenderTransform = New TranslateTransform(0, 0)
            AniStart({
                AaTranslateX(PanBiomes, -14, 140, , New AniEaseInFluent),
                AaOpacity(PanBiomes, -1, 140),
                AaCode(Sub() PanBiomes.Visibility = Visibility.Collapsed, 150, True)
            }, "SeedMap BiomePanel", True)
        End If
        If Open OrElse (SelectedBiomes IsNot Nothing AndAlso SelectedBiomes.Count > 0) Then
            '面板开着、或筛选仍然生效时都保持高亮（不然关掉面板后就看不出筛选还在起作用）
            BtnToolLocate.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnToolLocate.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
        Else
            BtnToolLocate.Background = Brushes.Transparent
            BtnToolLocate.Foreground = TryCast(FindResource("BrushText2"), Brush)
        End If
    End Sub

    ''' <summary>地形筛选变化后刷新「地形」按钮的高亮状态。</summary>
    Private Sub RefreshBiomeButton()
        Dim Active As Boolean = BIOME_PANEL_OPEN OrElse (SelectedBiomes IsNot Nothing AndAlso SelectedBiomes.Count > 0)
        If Active Then
            BtnToolLocate.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnToolLocate.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
        Else
            BtnToolLocate.Background = Brushes.Transparent
            BtnToolLocate.Foreground = TryCast(FindResource("BrushText2"), Brush)
        End If
    End Sub

    Private Sub BtnBiomeClose_Click(sender As Object, e As RoutedEventArgs) Handles BtnBiomeClose.Click
        SetBiomePanel(False)
    End Sub

    '===============================
    '  平移与缩放（无限平移，无边界的钳制）
    '===============================

    Private Sub PanMapViewport_MouseWheel(sender As Object, e As MouseWheelEventArgs) Handles PanMapViewport.MouseWheel
        ZoomTo(Zoom * If(e.Delta > 0, 1.22, 1 / 1.22), e.GetPosition(PanMapViewport))
    End Sub

    ''' <summary>带滑动过渡的缩放：以 Anchor（屏幕坐标）为中心动画到目标倍率。</summary>
    Private Sub ZoomTo(TargetZoom As Double, Anchor As Point)
        TargetZoom = ClampD(TargetZoom, ZoomMinimum(), InitialZoom * 14)
        If Math.Abs(TargetZoom - Zoom) < 0.0001 Then Return
        AniStop("SeedMap Zoom")
        '记录锚点对应的地图坐标，动画全程保持锚点钉在屏幕原位
        Dim AnchorU As Point = ScreenToUnit(Anchor)
        AniStart({
            AaDouble(Sub(d)
                         Zoom += d
                                          MapScale.ScaleX = Zoom : MapScale.ScaleY = Zoom
                         MapPos.X = Anchor.X - AnchorU.X * Zoom
                         MapPos.Y = Anchor.Y - AnchorU.Y * Zoom
                         UpdateMarkerScale()
                         UpdateSkeletonScale()
                         UpdateZoomLabel()
                         '缩放过程中照常重画（低分辨率），不要退回贴图——否则滚轮一动立体就消失
                         RefreshTiles()
                     End Sub, TargetZoom - Zoom, 190, , New AniEaseOutFluent(AniEasePower.Middle)),
            AaCode(Sub()
                       RefreshTiles(True)
                   End Sub, 200, True)
        }, "SeedMap Zoom", True)
    End Sub

    Private Sub PanMapViewport_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles PanMapViewport.MouseLeftButtonDown
        '第一人称选点：按下即进入，且不要拖动地图
        If FpsSelecting Then
            Dim Uf = ScreenToUnit(e.GetPosition(PanMapViewport))
            EnterFirstPerson(ToBlock(Uf.X, Uf.Y))
            Return
        End If
        '这一下按下是不是"刚刚用来关掉 3D 预览"的那一次点击；是的话松手时不要再开一次
        _3DSuppressOpenThisClick = _3DCloseTick <> 0 AndAlso CLng(Environment.TickCount) - _3DCloseTick < 400
        If _3DSuppressOpenThisClick Then _3DCloseTick = 0
        If Select3DMode Then Return '框选模式：不拖动地图
        IsDragging = True
        DragLast = e.GetPosition(PanMapViewport)
        PanMapViewport.CaptureMouse()
        PanMapViewport.Cursor = Cursors.SizeAll
    End Sub

    Private Sub PanMapViewport_MouseMove(sender As Object, e As MouseEventArgs) Handles PanMapViewport.MouseMove
        Dim P = e.GetPosition(PanMapViewport)
        If Select3DMode Then
            UpdateSelect3DBox(P)
            Dim U0 = ScreenToUnit(P)
            Dim B0 = ToBlock(U0.X, U0.Y)
            LabInfoPos.Text = $"X {B0.X} · Z {B0.Y}"
            LabInfoBiome.Text = BiomeNameAt(U0.X, U0.Y)
            Return
        End If
        If IsDragging Then
            MapPos.X += P.X - DragLast.X
            MapPos.Y += P.Y - DragLast.Y
            DragLast = P
            '网格/标尺/史莱姆每帧跟随（本身有覆盖范围缓存，很便宜），瓦片与结构扫描走 120ms 节流
            UpdateOverlays()
            RefreshTiles(False)
        End If
        Dim U = ScreenToUnit(P)
        Dim B = ToBlock(U.X, U.Y)
        LabInfoPos.Text = $"X {B.X} · Z {B.Y}"
        LabInfoBiome.Text = BiomeNameAt(U.X, U.Y)
    End Sub

    ''' <summary>右键取消 3D 框选。</summary>
    ''' <summary>右键：3D 框选模式下取消框选；普通模式下按设置里的格式复制该位置的坐标。</summary>
    Private Sub PanMapViewport_MouseRightUp(sender As Object, e As MouseButtonEventArgs) Handles PanMapViewport.MouseRightButtonUp
        If Select3DMode Then
            BtnTool3D_Click(Nothing, Nothing)
            Return
        End If
        Try
            Dim U = ScreenToUnit(e.GetPosition(PanMapViewport))
            Dim B = ToBlock(U.X, U.Y)
            Dim Text As String = FormatCopyText(CInt(B.X), CInt(B.Y))
            '剪贴板可能被输入法 / 剪贴板管理器短暂占用，但这里绝不能长时间阻塞：
            'copy:=True 会触发 OLE 刷新（占用时能卡住好几秒），Clear 还会清掉用户原有内容，
            '所以只做"写入 + 极少次数重试"，总耗时控制在 100ms 内。
            Dim Done As Boolean = False
            For i As Integer = 1 To 3
                Try
                    Clipboard.SetDataObject(Text, False)
                    Done = True
                    Exit For
                Catch
                    Thread.Sleep(30)
                End Try
            Next
            If Done Then
                Hint($"已复制坐标 {Text}", HintType.Green, Key:="SeedMapCopy")
            Else
                '实在抢不到剪贴板时，把坐标直接显示出来，至少能手动抄
                Hint($"复制失败（剪贴板被其他程序占用），坐标：{Text}", HintType.Red, Key:="SeedMapCopy")
            End If
        Catch ex As Exception
            Logger.Warn(ex, "复制坐标失败")
            Hint("复制坐标失败", HintType.Red)
        End Try
    End Sub


    ''' <summary>按设置里的「右键复制格式」生成要放进剪贴板的文本。</summary>
    Private Shared Function FormatCopyText(X As Integer, Z As Integer) As String
        Select Case Settings.Get(Of Integer)("SeedMapCopyFormat")
            Case 1
                Return $"{X} {Z}"
            Case 2
                Return $"{X}, {Z}"
            Case 3
                Return $"/tp {X} ~ {Z}"
            Case 4
                Return $"/tp @s {X} ~ {Z}"
            Case 5
                Return $"{X} ~ {Z}"
            Case Else
                Return $"X: {X} ,Z: {Z}"
        End Select
    End Function

    Private Sub PanMapViewport_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles PanMapViewport.MouseLeftButtonUp
        If Select3DMode Then
            '这次点击是用来关预览的（按下时已收起），别再开一遍
            If _3DSuppressOpenThisClick Then
                _3DSuppressOpenThisClick = False
                Return
            End If
            UpdateSelect3DBox(e.GetPosition(PanMapViewport))
            Open3DAt(Sel3DCenter)
            '一次性操作：打开预览后立即退出框选模式，按钮也不再保持选中
            Exit3DModeVisuals()
            Return
        End If
        If Not IsDragging Then Return
        IsDragging = False
        PanMapViewport.ReleaseMouseCapture()
        PanMapViewport.Cursor = Cursors.ScrollAll
        RefreshTiles(True)
        StartTileWorker() '万一 worker 都空闲退出了，这里重新拉起来
    End Sub

    Private Sub ZoomAtCenter(Factor As Double)
        Dim Vw = PanMapViewport.ActualWidth, Vh = PanMapViewport.ActualHeight
        If Vw < 50 Then Return
        ZoomTo(Zoom * Factor, New Point(Vw / 2, Vh / 2))
    End Sub

    ''' <summary>回到原点（跳转坐标面板里的按钮；复位功能已并入该面板）。</summary>
    Private Sub BtnPanReset_Click(sender As Object, e As RoutedEventArgs) Handles BtnPanReset.Click
        PopPanTo.IsOpen = False
        ResetView()
        Hint("已回到原点", HintType.Blue)
    End Sub

    ''' <summary>复位：回到出生点（主世界）/ 原点，恢复初始缩放。</summary>
    Private Sub ResetView()
        EnsureInitialView()
        Dim Target As New Point(0, 0)
        If Dimension = 0 AndAlso ModuleSeedMap.IsNativeAvailable Then
            Try
                Target = ModuleSeedMap.GetSpawn(SeedValue, ModuleSeedMap.VersionIds(VersionIndex))
            Catch
            End Try
        End If
        Dim Vw = PanMapViewport.ActualWidth
        If Vw < 50 Then Return
        Dim U = ToUnit(Target.X, Target.Y)
        Dim Tz = DefaultZoomForDim(Dimension)
        Dim Tx = Vw / 2 - U.X * Tz
        AniStop("SeedMap View")
        '竖直方向受俯角影响，动画结束后统一用 CenterOn 对齐（顺便刷瓦片）
        AniStart({
            AaDouble(AddressOf AnimStepZoom, Tz - Zoom, 320, , New AniEaseOutFluent(AniEasePower.Middle)),
            AaDouble(AddressOf AnimStepPanX, Tx - MapPos.X, 320, , New AniEaseOutFluent(AniEasePower.Middle)),
            AaCode(Sub()
                       CenterOn(Target.X, Target.Y)
                   End Sub, 330, True),
            AaCode(AddressOf SaveDimViewCurrent, 360, True)
        }, "SeedMap View", True)
    End Sub

    ''' <summary>复位动画的缩放步：同步更新俯角与纵向比例。</summary>
    Private Sub AnimStepZoom(d As Double)
        Zoom += d
        MapScale.ScaleX = Zoom
        MapScale.ScaleY = Zoom
        UpdateMarkerScale()
        UpdateZoomLabel()
    End Sub

    ''' <summary>复位动画的横向平移步。</summary>
    Private Sub AnimStepPanX(d As Double)
        MapPos.X += d
    End Sub

    Private Sub UpdateZoomLabel()
        If InitialZoom <= 0 Then Return
        LabZoom.Text = Math.Round(Zoom / InitialZoom * 100) & "%"
        Dim PerPx = BlocksPerUnit / Zoom
        Dim PxTxt As String = If(PerPx >= 1, $"1px≈{PerPx:0.#}格", $"1px≈{1 / PerPx:0.##}格")
        LabScaleHint.Text = PxTxt & $" · {CurrentLevel()}格采样"
    End Sub

    '===============================
    '  顶栏交互
    '===============================

    '===============================
    '  默认种子：当前实例最近修改的存档
    '===============================

    ''' <summary>已为哪个实例做过默认种子填充（同一实例只填一次；用户手动改过就永不覆盖）。</summary>
    Private SeedAutoFillInstance As String = Nothing
    ''' <summary>XAML 里的占位默认种子：只有它还没被改过时，才允许替换成存档种子。</summary>
    Private Const SeedDefaultInXaml As String = "7906073100618442319"

    ''' <summary>外部（存档详情页「地图」按钮）请求打开时携带的种子与游戏版本，消费后清空。</summary>
    Public Shared PendingSeed As String = Nothing
    Public Shared PendingVersion As String = Nothing

    ''' <summary>应用外部请求的种子 + 版本；没有请求就返回 False，交给"最近存档"那套逻辑。</summary>
    Private Function ApplyPendingSeed() As Boolean
        If String.IsNullOrEmpty(PendingSeed) Then Return False
        Dim SeedText As String = PendingSeed
        Dim VerText As String = If(PendingVersion, "")
        PendingSeed = Nothing
        PendingVersion = Nothing
        Try
            If TxtSeed Is Nothing Then Return False
            TxtSeed.Text = SeedText
            Dim Parsed As Long = 0
            Long.TryParse(SeedText, Parsed)
            SeedValue = Parsed
            '标记已为当前实例填过，避免"最近存档"的自动填充把它覆盖掉
            SeedAutoFillInstance = If(McInstanceSelected Is Nothing, Nothing, McInstanceSelected.Name)
            If Not String.IsNullOrWhiteSpace(VerText) Then
                Dim VI As Integer = FindVersionIndex(VerText)
                If VI >= 0 Then
                    VersionIndex = VI
                    LabVersion.Text = "Java 版 " & ModuleSeedMap.VersionNames(VI)
                    HighlightVersion()
                End If
            End If
            RegenerateAll(True)
            Hint($"已载入存档种子：{SeedText}", HintType.Blue)
            Return True
        Catch ex As Exception
            Logger.Warn(ex, "应用存档种子失败")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 进入页面时把种子默认成「当前选择实例最近修改的存档」的种子（该存档读不到种子就继续往前找）。
    ''' 只在种子仍是占位值、且还没为这个实例填过时生效，避免覆盖用户手动输入的种子。
    ''' </summary>
    Private Sub LoadSeedFromRecentSave()
        Try
            If McInstanceSelected Is Nothing OrElse TxtSeed Is Nothing Then Return
            Dim InstName As String = McInstanceSelected.Name
            If SeedAutoFillInstance = InstName Then Return
            Dim Current As String = TxtSeed.Text.Trim()
            If Current <> "" AndAlso Current <> SeedDefaultInXaml Then
                SeedAutoFillInstance = InstName '用户自己填过，之后不再动它
                Return
            End If
            SeedAutoFillInstance = InstName
            Dim IndiePath As String = McInstanceSelected.PathIndie
            RunInNewThread(
            Sub()
                Dim SeedText As String = Nothing
                Dim SaveName As String = Nothing
                Dim SaveVersion As String = Nothing
                Try
                    Dim SavesFolder = DirectoryUtils.GetInfo(IndiePath & "saves\")
                    If SavesFolder.Exists Then
                        For Each Save In SavesFolder.EnumerateDirectories.OrderByDescending(Function(s) s.LastWriteTime)
                            Dim Info = ModSave.LoadSaveInfo(Save.FullName)
                            If Info.HasSeed Then
                                SeedText = Info.Seed.ToString()
                                SaveName = Save.Name
                                SaveVersion = Info.GameVersion '顺带把存档的版本号也带出来
                                Exit For
                            End If
                        Next
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "读取存档种子失败")
                End Try
                If String.IsNullOrEmpty(SeedText) Then Return
                RunInUi(
                Sub()
                    If Not IsLoaded OrElse TxtSeed Is Nothing Then Return
                    Dim NowText As String = TxtSeed.Text.Trim()
                    If NowText <> "" AndAlso NowText <> SeedDefaultInXaml Then Return '用户已经自己填了
                    TxtSeed.Text = SeedText
                    Dim Parsed As Long = 0
                    Long.TryParse(SeedText, Parsed)
                    SeedValue = Parsed
                    '同时把版本也切到存档的版本（例如存档是 1.20.1 → 选 1.20）
                    Dim VersionText As String = ""
                    Dim VI As Integer = FindVersionIndex(SaveVersion)
                    If VI >= 0 Then
                        If VI <> VersionIndex Then
                            VersionIndex = VI
                            LabVersion.Text = "Java 版 " & ModuleSeedMap.VersionNames(VI)
                            HighlightVersion()
                        End If
                        VersionText = "，版本 " & ModuleSeedMap.VersionNames(VI)
                    End If
                    RegenerateAll(False)
                    Hint($"已载入存档「{SaveName}」的种子：{SeedText}{VersionText}", HintType.Blue)
                End Sub)
            End Sub, "SeedMap Default Seed")
        Catch ex As Exception
            Logger.Warn(ex, "默认种子读取失败")
        End Try
    End Sub

    ''' <summary>
    ''' 把存档记录的游戏版本（如 "1.20.1"、"1.21.4"）映射到种子地图的版本索引；
    ''' 只保留主.次版本号（1.20.1 → 1.20），快照/无法识别的版本返回 -1。
    ''' </summary>
    Private Function FindVersionIndex(GameVersion As String) As Integer
        Try
            If String.IsNullOrWhiteSpace(GameVersion) Then Return -1
            Dim Parts = GameVersion.Trim().Split("."c)
            If Parts.Length < 2 Then Return -1
            Dim Major As Integer, Minor As Integer
            If Not Integer.TryParse(Parts(0), Major) Then Return -1
            '1.20.1 / 1.20.1-forge… 都取前两段；"1.20.1-rc1" 这类第三段带字符也不影响
            Dim MinorText As String = New String(Parts(1).TakeWhile(Function(c) Char.IsDigit(c)).ToArray())
            If Not Integer.TryParse(MinorText, Minor) Then Return -1
            Dim Key As String = Major.ToString() & "." & Minor.ToString()
            For i = 0 To ModuleSeedMap.VersionNames.Count - 1
                If ModuleSeedMap.VersionNames(i) = Key Then Return i
            Next
            '没有完全一致的（比如 1.21.4）就退到同名主.次（1.21）之外再找不大于它的最近一项
            For i = 0 To ModuleSeedMap.VersionNames.Count - 1
                Dim N = ModuleSeedMap.VersionNames(i)
                Dim NP = N.Split("."c)
                If NP.Length = 2 Then
                    Dim NMaj As Integer, NMin As Integer
                    If Integer.TryParse(NP(0), NMaj) AndAlso Integer.TryParse(NP(1), NMin) Then
                        If NMaj < Major OrElse (NMaj = Major AndAlso NMin <= Minor) Then Return i
                    End If
                End If
            Next
            Return -1
        Catch ex As Exception
            Return -1
        End Try
    End Function

    '===============================
    '  弹出菜单：再点一次按钮应该收起
    '===============================

    ''' <summary>
    ''' StaysOpen=False 的 Popup 在点按钮时会先自己关掉（它抢了鼠标捕获），
    ''' 随后的 Click 又把 IsOpen 设回 True，于是"点第二次还是展开"。
    ''' 这里记下关闭时刻：紧跟着的那一次点击只当作"收起"，不再重新打开。
    ''' </summary>
    Private ReadOnly PopClosedTick As New Dictionary(Of Popup, Long)

    Private Sub SeedPopup_Closed(sender As Object, e As EventArgs) Handles PopPanTo.Closed, PopVersion.Closed
        PopClosedTick(CType(sender, Popup)) = CLng(Environment.TickCount)
    End Sub

    ''' <summary>开关弹出菜单：已展开则收起；刚刚因为这次点击被关掉的，不再重新展开。</summary>
    Private Sub TogglePopup(P As Popup, Optional AfterOpen As Action = Nothing)
        If P Is Nothing Then Return
        Dim T As Long = 0
        If PopClosedTick.TryGetValue(P, T) AndAlso CLng(Environment.TickCount) - T < 250 Then Return
        If P.IsOpen Then
            P.IsOpen = False
            Return
        End If
        P.IsOpen = True
        PopClosedTick(P) = 0
        If AfterOpen IsNot Nothing Then AfterOpen()
    End Sub

    '===============================
    '  跳转到坐标
    '===============================

    Private Sub BtnPanTo_Click(sender As Object, e As RoutedEventArgs) Handles BtnPanTo.Click
        '预填当前视野中心，方便在此基础上微调
        Dim C = ViewCenter()
        If String.IsNullOrWhiteSpace(TxtPanX.Text) Then TxtPanX.Text = C.X.ToString("0")
        If String.IsNullOrWhiteSpace(TxtPanZ.Text) Then TxtPanZ.Text = C.Y.ToString("0")
        TogglePopup(PopPanTo, Sub()
                                  TxtPanX.Focus()
                                  TxtPanX.SelectAll()
                              End Sub)
    End Sub

    Private Sub BtnPanGo_Click(sender As Object, e As RoutedEventArgs) Handles BtnPanGo.Click
        JumpToInputCoordinate()
    End Sub

    Private Sub TxtPan_KeyDown(sender As Object, e As KeyEventArgs) Handles TxtPanX.KeyDown, TxtPanZ.KeyDown
        If e.Key <> Key.Enter Then Return
        e.Handled = True
        JumpToInputCoordinate()
    End Sub

    ''' <summary>按输入框里的方块坐标居中并重绘地图。</summary>
    Private Sub JumpToInputCoordinate()
        Dim TxtX As String = If(TxtPanX.Text, "").Trim()
        Dim TxtZ As String = If(TxtPanZ.Text, "").Trim()
        Dim X As Double, Z As Double
        If Not Double.TryParse(TxtX, X) Then
            Hint("X 坐标不是有效的数字", HintType.Red)
            Return
        End If
        If Not Double.TryParse(TxtZ, Z) Then
            Hint("Z 坐标不是有效的数字", HintType.Red)
            Return
        End If
        If Math.Abs(X) > 30000000 OrElse Math.Abs(Z) > 30000000 Then
            Hint("坐标超出可探索范围（±30,000,000）", HintType.Red)
            Return
        End If
        PopPanTo.IsOpen = False
        CenterOn(X, Z)
        Hint($"已跳转到 ({X:0}, {Z:0})", HintType.Blue)
    End Sub

    Private Sub BtnSeedLoad_Click(sender As Object, e As RoutedEventArgs) Handles BtnSeedLoad.Click
        Dim Text = TxtSeed.Text.Trim()
        If Text = "" Then
            Hint("请先输入世界种子！", HintType.Red)
            Return
        End If
        Dim Parsed As Long
        If Not Long.TryParse(Text, Parsed) Then
            '非数字种子：按字符串散列为长整数
            Parsed = 0
            For Each Ch In Text
                Parsed = (Parsed * 31 + AscW(Ch)) And &H7FFFFFFFFFFFFFFFL
            Next
        End If
        SeedValue = Parsed
        RegenerateAll(True)
        Hint($"正在生成种子 {Text} 的地图…", HintType.Blue)
    End Sub

    ''' <summary>是否已经尝试过联网补充版本列表（一次会话只拉一次）。</summary>
    Private _VersionListRefreshed As Boolean = False

    ''' <summary>
    ''' 从 Mojang 的版本清单拉取正式版号，把种子地图还不认识的新版本加进下拉。
    ''' 新版本按其"不大于的最新受支持版本"的算法近似生成地图。
    ''' </summary>
    ''' <summary>合并列表后按名字保持选中项并刷新下拉（UI 线程）。</summary>
    Private Sub RebuildVersionListSafely()
        Try
            Dim CurrentName As String = ""
            If VersionIndex >= 0 AndAlso VersionIndex < ModuleSeedMap.VersionNames.Count Then CurrentName = ModuleSeedMap.VersionNames(VersionIndex)
            RunInUi(Sub() ApplyVersionListAndKeepSelection(CurrentName))
        Catch ex As Exception
            Logger.Warn(ex, "重建种子地图版本下拉失败")
        End Try
    End Sub

    Private Sub ApplyVersionListAndKeepSelection(CurrentName As String)
        Try
            If CurrentName <> "" Then
                Dim NewIndex As Integer = ModuleSeedMap.IndexOfVersionName(CurrentName)
                If NewIndex >= 0 Then VersionIndex = NewIndex
            End If
            BuildVersionList()
            If VersionIndex >= 0 AndAlso VersionIndex < ModuleSeedMap.VersionNames.Count Then
                LabVersion.Text = "Java 版 " & ModuleSeedMap.VersionNames(VersionIndex)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新种子地图版本下拉失败")
        End Try
    End Sub

    ''' <summary>版本清单的磁盘缓存，两天内不再联网。</summary>
    Private Shared ReadOnly Property VersionListCachePath As String
        Get
            Return Paths.Base & "YQL\Cache\SeedMapVersions.txt"
        End Get
    End Property
    Private Const VersionListCacheDays As Double = 2

    Private Sub RefreshVersionListFromNet()
        If _VersionListRefreshed Then Return
        _VersionListRefreshed = True
        '先看磁盘缓存：两天内直接用，不再发请求
        Try
            Dim CacheFile = VersionListCachePath
            If FileUtils.Exists(CacheFile) AndAlso (Date.Now - IO.File.GetLastWriteTime(CacheFile)).TotalDays < VersionListCacheDays Then
                Dim Cached = FileUtils.ReadAsString(CacheFile)
                Dim Names As New List(Of String)
                For Each LineText In If(Cached, "").Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                    If LineText.Trim() <> "" Then Names.Add(LineText.Trim())
                Next
                If Names.Count > 0 Then
                    If ModuleSeedMap.MergeNetVersions(Names) Then RebuildVersionListSafely()
                    Return
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取种子地图版本缓存失败")
        End Try
        RunInNewThread(
        Sub()
            Try
                Dim Raw As String = ""
                For Each Url In {"https://launchermeta.mojang.com/mc/game/version_manifest.json",
                                 "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"}
                    Try
                        Raw = NetRequestByLoader(Url, Timeout:=12000, IsJson:=True)
                        If Not String.IsNullOrWhiteSpace(Raw) Then Exit For
                    Catch ex As Exception
                        Logger.Warn(ex, $"拉取版本清单失败：{Url}")
                    End Try
                Next
                If String.IsNullOrWhiteSpace(Raw) Then Return
                Dim Json = Raw.DeserializeJson()
                Dim Names As New List(Of String)
                For Each Item In Json("versions")
                    If String.IsNullOrWhiteSpace(Item?("id")?.ToString()) Then Continue For
                    If String.IsNullOrWhiteSpace(Item?("type")?.ToString()) Then Continue For
                    If Item("type").ToString() <> "release" Then Continue For
                    Dim Id As String = Item("id").ToString().Trim()
                    '只要主.次版本号（1.21.4 → 1.21 这种不单独列，避免列表爆炸）
                    If Not Text.RegularExpressions.Regex.IsMatch(Id, "^\d+\.\d+$") Then Continue For
                    If Not Names.Contains(Id) Then Names.Add(Id)
                Next
                If Names.Count = 0 Then Return
                Dim CurrentName As String = ""
                RunInUiWait(Sub()
                                If VersionIndex >= 0 AndAlso VersionIndex < ModuleSeedMap.VersionNames.Count Then CurrentName = ModuleSeedMap.VersionNames(VersionIndex)
                            End Sub)
                '写回磁盘缓存（两天内不再联网）
                Try
                    FileUtils.Write(VersionListCachePath, String.Join(vbCrLf, Names))
                Catch ex As Exception
                    Logger.Warn(ex, "写入种子地图版本缓存失败")
                End Try
                If Not ModuleSeedMap.MergeNetVersions(Names) Then Return
                RunInUi(
                Sub()
                    ApplyVersionListAndKeepSelection(CurrentName)
                End Sub)
            Catch ex As Exception
                Logger.Warn(ex, "获取种子地图版本清单失败")
            End Try
        End Sub, "SeedMap Version List", ThreadPriority.BelowNormal)
    End Sub

    '版本下拉
    Private Sub BuildVersionList()
        PanVerList.Children.Clear()
        VersionButtons.Clear()
        For i = 0 To ModuleSeedMap.VersionNames.Count - 1
            Dim Index = i
            Dim Btn As New Button With {.Style = TryCast(FindResource("BtnGhost"), Style), .Height = 30,
                .Content = "Java 版 " & ModuleSeedMap.VersionNames(i), .FontSize = 12.5,
                .HorizontalContentAlignment = HorizontalAlignment.Left, .Padding = New Thickness(10, 0, 10, 0)}
            AddHandler Btn.Click, Sub(s, e) SelectVersion(Index)
            PanVerList.Children.Add(Btn)
            VersionButtons.Add(Btn)
        Next
        HighlightVersion()
    End Sub

    Private Sub HighlightVersion()
        For i = 0 To VersionButtons.Count - 1
            If i = VersionIndex Then
                VersionButtons(i).FontWeight = FontWeights.SemiBold
                VersionButtons(i).Foreground = TryCast(FindResource("ColorBrush3"), Brush)
            Else
                VersionButtons(i).FontWeight = FontWeights.Normal
                VersionButtons(i).Foreground = TryCast(FindResource("BrushText1"), Brush)
            End If
        Next
    End Sub

    Private Sub SelectVersion(Index As Integer)
        If Index < 0 OrElse Index >= ModuleSeedMap.VersionIds.Count Then Return
        PopVersion.IsOpen = False
        If Index = VersionIndex Then Return
        VersionIndex = Index
        LabVersion.Text = "Java 版 " & ModuleSeedMap.VersionNames(Index)
        HighlightVersion()
        RegenerateAll(False)
    End Sub

    Private Sub BtnVersion_Click(sender As Object, e As RoutedEventArgs) Handles BtnVersion.Click
        TogglePopup(PopVersion)
    End Sub

    '版本下拉入场：轻缩放 + 上浮淡入
    Private Sub PopVersion_Opened(sender As Object, e As EventArgs) Handles PopVersion.Opened
        PanVersionCard.RenderTransformOrigin = New Point(0.5, 0)
        PanVersionCard.RenderTransform = New ScaleTransform(0.96, 0.96)
        PanVersionCard.Opacity = 0
        AniStart({
            AaOpacity(PanVersionCard, 1, 110),
            AaScaleTransform(PanVersionCard, 0.04, 200, , New AniEaseOutBack(AniEasePower.Weak))
        }, "SeedMap VersionPop", True)
    End Sub

    '维度切换
    Private Sub SetDimensionIfChecked(Radio As RadioButton, Index As Integer)
        If Not IsLoaded OrElse Not Radio.IsChecked Then Return
        If Dimension = Index Then Return
        '切维度：主世界的高程模型在下界/末地没有意义，直接收起
        CloseView3DNow()
        '先记住当前维度的视野，再切到目标维度：末地独立，主世界↔下界按 8:1 换算坐标
        SaveDimView(Dimension)
        Dim Previous As Integer = Dimension
        Dimension = Index
        Structs = StructLists(Index)
        EnsureDefaultStruct()
        StructPoints.Clear()
        BuildStructList()
        '地形筛选按维度独立：切换时重建清单并清除选择
        LoadBiomeSel(Index)
        BuildBiomeList()
        TxtStructSearch_TextChanged(Nothing, Nothing)
        '维度过渡：地图快速淡出 → 套用目标维度视野并重新生成 → 淡入
        AniStop("SeedMap DimSwitch")
        AniStart({
            AaOpacity(PanMapViewport, -0.75, 140, , New AniEaseInFluent),
            AaCode(
            Sub()
                ApplyDimView(Previous, Index)
                RegenerateAll(False)
            End Sub, 150, True),
            AaOpacity(PanMapViewport, 0.75, 320, , New AniEaseOutFluent)
        }, "SeedMap DimSwitch", True)
    End Sub
    Private Sub RadioDimOver_Checked(sender As Object, e As RoutedEventArgs) Handles RadioDimOver.Checked
        If RadioDimNether Is Nothing Then Return
        SetDimensionIfChecked(RadioDimOver, 0)
    End Sub
    Private Sub RadioDimNether_Checked(sender As Object, e As RoutedEventArgs) Handles RadioDimNether.Checked
        If RadioDimEnd Is Nothing Then Return
        SetDimensionIfChecked(RadioDimNether, 1)
    End Sub
    Private Sub RadioDimEnd_Checked(sender As Object, e As RoutedEventArgs) Handles RadioDimEnd.Checked
        If RadioDimEnd Is Nothing Then Return
        SetDimensionIfChecked(RadioDimEnd, 2)
    End Sub

    '工具栏
    Private Sub BtnToolStruct_Click(sender As Object, e As RoutedEventArgs) Handles BtnToolStruct.Click
        SetStructPanel(Not STRUCT_PANEL_OPEN)
    End Sub
    '===============================
    '  史莱姆区块
    '===============================

    ''' <summary>是否显示史莱姆区块叠加层。</summary>
    Private SlimeEnabled As Boolean = False
    ''' <summary>视野过大时提示过「放大后再看」，避免每次刷新都弹提示。</summary>
    Private SlimeHintShown As Boolean = False
    ''' <summary>单次最多绘制的史莱姆区块数（再多会明显拖慢拖拽/缩放）。</summary>
    Private Const SlimeMaxCells As Integer = 40000

    ''' <summary>Java 版 Random 的最小子集：史莱姆判定必须与 Java 的 LCG 序列完全一致。</summary>
    Private Class JavaRandom
        Private Const Mult As Decimal = 25214903917D
        Private Const Addend As Decimal = 11D
        Private Const Mask48 As Decimal = 281474976710655D
        Private Seed As Decimal
        Public Sub New(InitSeed As Long)
            Seed = CDec((InitSeed Xor &H5DEECE66DL) And &HFFFFFFFFFFFFL)
        End Sub
        Private Function [Next](Bits As Integer) As Integer
            '48 位模运算（Decimal 没有位运算，用取模实现）
            Dim V As Decimal = Seed * Mult + Addend
            Seed = V - Decimal.Floor(V / 281474976710656D) * 281474976710656D
            Return CInt(Decimal.Floor(Seed / CDec(2 ^ (48 - Bits))))
        End Function
        Public Function NextInt(Bound As Integer) As Integer
            Dim Bits As Integer, Val As Integer
            Do
                Bits = [Next](31)
                Val = Bits Mod Bound
            Loop While Bits - Val + (Bound - 1) < 0
            Return Val
        End Function
    End Class

    ''' <summary>
    ''' 该区块是否为史莱姆区块（与原版一致：
    ''' seed + x²·0x4c1906 + x·0x5ac0db + z²·0x4307a7 + z·0x5f24f ^ 0x3ad8025f，再判定 Java Random.nextInt(10) = 0）。
    ''' </summary>
    Private Function IsSlimeChunk(ChunkX As Integer, ChunkZ As Integer) As Boolean
        Try
            '按 Java 的 long 语义（64 位溢出折叠）在 Decimal 里算完再取低位，避免 VB 的溢出异常
            Dim S As Decimal = CDec(SeedValue)
            S += CDec(ChunkX) * ChunkX * 4987142D + CDec(ChunkX) * 5947611D
            S += CDec(ChunkZ) * ChunkZ * 4392871D + CDec(ChunkZ) * 389711D
            Dim Wrap As Decimal = 18446744073709551616D '2^64
            Dim Low As Decimal = S - Decimal.Floor(S / Wrap) * Wrap
            Dim Bits As Long
            If Low >= 9223372036854775808D Then
                Bits = CLng(Low - Wrap)
            Else
                Bits = CLng(Low)
            End If
            Return New JavaRandom(Bits Xor &H3AD8025FL).NextInt(10) = 0
        Catch ex As Exception
            Return False
        End Try
    End Function

    ''' <summary>史莱姆判定结果缓存：同一区块只算一次（判定里用的是 Decimal 除法，很贵）。</summary>
    Private ReadOnly SlimeCache As New Dictionary(Of Long, Boolean)

    Private Shared Function ChunkKey(I As Integer, J As Integer) As Long
        Return ((CLng(I) And &HFFFFFFFL) << 28) Or (CLng(J) And &HFFFFFFFL)
    End Function

    Private Function IsSlimeChunkCached(I As Integer, J As Integer) As Boolean
        Dim Key = ChunkKey(I, J)
        Dim V As Boolean
        If SlimeCache.TryGetValue(Key, V) Then Return V
        V = IsSlimeChunk(I, J)
        If SlimeCache.Count > 200000 Then SlimeCache.Clear()
        SlimeCache(Key) = V
        Return V
    End Function

    '史莱姆叠加层：整层只用一条 Path（以前是一格一个 Rectangle，一屏几千个元素，平移时卡顿明显）
    Private SlimePath As Path
    Private ReadOnly SlimeFillBrush As Brush = MakeFrozenBrush(Color.FromArgb(70, &H58, &HD8, &H46))
    Private ReadOnly SlimeStrokeBrush As Brush = MakeFrozenBrush(Color.FromArgb(210, &H3E, &HC8, &H30))
    Private SlimeCover As Rect = Rect.Empty
    Private SlimeCoverZoom As Double = 0

    ''' <summary>按当前视野重绘史莱姆区块叠加层（视野过大时只提示、不绘制）。</summary>
    Private Sub RefreshSlime()
        If PanSlime Is Nothing Then Return
        If Not SlimeEnabled Then
            If SlimePath IsNot Nothing Then SlimePath.Data = Nothing
            SlimeCover = Rect.Empty
            Return
        End If
        Try
            Dim Vw = PanMapViewport.ActualWidth, Vh = PanMapViewport.ActualHeight
            If Vw < 50 OrElse Vh < 50 OrElse InitialZoom <= 0 Then Return
            '1 区块 = 16 方块 = 2 地图单位
            Dim R = ViewUnitRect()
            Dim U0 = R.Left, U1 = R.Right
            Dim V0 = R.Top, V1 = R.Bottom
            Dim Total As Long = (CLng(Math.Floor(U1 / 2)) - CLng(Math.Floor(U0 / 2)) + 1) *
                                (CLng(Math.Floor(V1 / 2)) - CLng(Math.Floor(V0 / 2)) + 1)
            If Total > SlimeMaxCells Then
                If SlimePath IsNot Nothing Then SlimePath.Data = Nothing
                SlimeCover = Rect.Empty
                If Not SlimeHintShown Then
                    SlimeHintShown = True
                    Hint("视野内的区块太多，先放大一些再显示史莱姆区块", HintType.Blue)
                End If
                Return
            End If
            SlimeHintShown = False
            '覆盖范围还够用且缩放没变 → 跳过（叠加层和地图同一坐标系，平移时本来就跟着动）
            If Math.Abs(SlimeCoverZoom - Zoom) < 0.0001 AndAlso Not SlimeCover.IsEmpty AndAlso
               SlimeCover.Left <= U0 AndAlso SlimeCover.Right >= U1 AndAlso
               SlimeCover.Top <= V0 AndAlso SlimeCover.Bottom >= V1 Then Return
            '向外多画 25%，普通拖拽不必重建
            Dim PadX As Double = Math.Max(16, (U1 - U0) * 0.25)
            Dim PadY As Double = Math.Max(16, (V1 - V0) * 0.25)
            Dim Cover As New Rect(U0 - PadX, V0 - PadY, (U1 - U0) + PadX * 2, (V1 - V0) + PadY * 2)
            Dim Cx0 As Integer = CInt(Math.Floor(Cover.Left / 2)), Cx1 As Integer = CInt(Math.Floor(Cover.Right / 2))
            Dim Cz0 As Integer = CInt(Math.Floor(Cover.Top / 2)), Cz1 As Integer = CInt(Math.Floor(Cover.Bottom / 2))
            '每个史莱姆区块画成「半透明绿填充 + 绿色描边」：填充淡一些不挡地形，描边保证边界清晰
            Dim Geo As New StreamGeometry()
            Dim Drawn As Integer = 0
            Using Ctx = Geo.Open()
                For J As Integer = Cz0 To Cz1
                    For I As Integer = Cx0 To Cx1
                        If Not IsSlimeChunkCached(I, J) Then Continue For
                        Ctx.BeginFigure(New Point(I * 2, J * 2), True, True)
                        Ctx.LineTo(New Point(I * 2 + 2, J * 2), True, False)
                        Ctx.LineTo(New Point(I * 2 + 2, J * 2 + 2), True, False)
                        Ctx.LineTo(New Point(I * 2, J * 2 + 2), True, False)
                        Drawn += 1
                        If Drawn >= 20000 Then Exit For '极端情况保护
                    Next
                    If Drawn >= 20000 Then Exit For
                Next
            End Using
            Geo.Freeze()
            If SlimePath Is Nothing Then
                SlimePath = New Path With {.Fill = SlimeFillBrush, .Stroke = SlimeStrokeBrush, .IsHitTestVisible = False}
                PanSlime.Children.Clear()
                PanSlime.Children.Add(SlimePath)
            End If
            SlimePath.Data = Geo
            SlimePath.StrokeThickness = 1 / Math.Max(0.05, Zoom)
            SlimeCover = Cover
            SlimeCoverZoom = Zoom
        Catch ex As Exception
            Logger.Warn(ex, "绘制史莱姆区块失败")
        End Try
    End Sub

    Private Sub BtnToolSlime_Click(sender As Object, e As RoutedEventArgs) Handles BtnToolSlime.Click
        SlimeEnabled = Not SlimeEnabled
        If SlimeEnabled Then
            BtnToolSlime.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnToolSlime.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
            Hint("史莱姆显示：已开启", HintType.Blue)
        Else
            BtnToolSlime.Background = Brushes.Transparent
            BtnToolSlime.Foreground = TryCast(FindResource("BrushText2"), Brush)
            SlimeHintShown = False
            Hint("史莱姆显示：已关闭", HintType.Blue)
        End If
        RefreshSlime()
        SaveSwitches()
    End Sub

    Private Sub BtnToolLocate_Click(sender As Object, e As RoutedEventArgs) Handles BtnToolLocate.Click
        SetBiomePanel(Not BIOME_PANEL_OPEN)
    End Sub
    '地形显示开关（图层按钮）：隐藏 / 显示群系瓦片
    Private TerrainVisible As Boolean = True
    Private Sub BtnToolLayer_Click(sender As Object, e As RoutedEventArgs) Handles BtnToolLayer.Click
        TerrainVisible = Not TerrainVisible
        AniStop("SeedMap Terrain")
        If TerrainVisible Then
            AniStart({AaOpacity(PanTerrain, 1 - PanTerrain.Opacity, 260, , New AniEaseOutFluent)}, "SeedMap Terrain", True)
            BtnToolLayer.Background = Brushes.Transparent
            BtnToolLayer.Foreground = TryCast(FindResource("BrushText2"), Brush)
            Hint("地形显示：已开启", HintType.Blue)
        Else
            AniStart({AaOpacity(PanTerrain, -PanTerrain.Opacity, 200, , New AniEaseInFluent)}, "SeedMap Terrain", True)
            BtnToolLayer.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnToolLayer.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
            Hint("地形显示：已关闭", HintType.Blue)
        End If
        SaveSwitches()
    End Sub

    '网格显示开关（网格按钮）：隐藏 / 显示坐标网格线
    Private GridVisible As Boolean = True
    Private Sub BtnToolGrid_Click(sender As Object, e As RoutedEventArgs) Handles BtnToolGrid.Click
        GridVisible = Not GridVisible
        If GridVisible Then
            BtnToolGrid.Background = Brushes.Transparent
            BtnToolGrid.Foreground = TryCast(FindResource("BrushText2"), Brush)
            Hint("网格显示：已开启", HintType.Blue)
        Else
            BtnToolGrid.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnToolGrid.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
            Hint("网格显示：已关闭", HintType.Blue)
        End If
        UpdateGrid()
        SaveSwitches()
    End Sub

    '===============================
    '  页面进入 / 退出动画（覆盖默认逐个进入动画）
    '===============================

    Public Overrides Sub TriggerEnterAnimation(ParamArray Elements As FrameworkElement())
        '上次退出动画可能被 PageOnForceExit 中途打断，导致根节点透明度卡在中间值：进入时无条件复位
        AniStop("PageRight PageChange " & PageUuid)
        PanRoot.Opacity = 1
        Child.Visibility = Visibility.Visible
        If Not GeneratedOnce Then
            GeneratedOnce = True
            EnsureInitialView()
            RegenerateAll(True)
        End If
        '重置到初始动画状态
        PanTopBar.RenderTransform = New TranslateTransform(0, -26)
        PanToolRail.RenderTransform = New TranslateTransform(-18, 0)
        PanTopBar.Opacity = 0 : PanToolRail.Opacity = 0
        PanMapViewport.Opacity = 0
        '地图改为纯淡入（原来是 1.045 倍的“从大缩小”）。同时把可能残留的缩放复位，
        '避免上次进入动画被中断后地图一直停在放大状态。
        PanMapViewport.RenderTransform = Nothing
        If PanStructures.Visibility = Visibility.Visible Then
            PanStructures.RenderTransform = New TranslateTransform(-16, 0)
            PanStructures.Opacity = 0
        End If
        If PanBiomes.Visibility = Visibility.Visible Then
            PanBiomes.RenderTransform = New TranslateTransform(-16, 0)
            PanBiomes.Opacity = 0
        End If
        '编排：地图淡入 → 顶部菜单栏下浮 → 左右工具滑入 → 底部信息上浮
        AniStart({
            AaOpacity(PanMapViewport, 1, 300, , New AniEaseOutFluent(AniEasePower.Weak)),
            AaOpacity(PanTopBar, 1, 120, 70),
            AaTranslateY(PanTopBar, 26, 380, 70, New AniEaseOutBack(AniEasePower.Weak)),
            AaOpacity(PanToolRail, 1, 110, 170),
            AaTranslateX(PanToolRail, 18, 340, 170, New AniEaseOutBack(AniEasePower.Weak)),
            AaCode(Sub()
                       If PanStructures.Visibility = Visibility.Visible Then
                           AniStart({
                               AaOpacity(PanStructures, 1, 130),
                               AaTranslateX(PanStructures, 16, 320, , New AniEaseOutBack(AniEasePower.Weak))
                           }, "SeedMap StructPanel", True)
                       End If
                       If PanBiomes.Visibility = Visibility.Visible Then
                           AniStart({
                               AaOpacity(PanBiomes, 1, 130),
                               AaTranslateX(PanBiomes, 16, 320, , New AniEaseOutBack(AniEasePower.Weak))
                           }, "SeedMap BiomePanel", True)
                       End If
                   End Sub, 260, True),
            AaCode(Sub() PageOnEnterAnimationFinished(), 200, True)
        }, "PageRight PageChange " & PageUuid, True)
    End Sub

    Public Overrides Sub TriggerExitAnimation(ParamArray Elements As FrameworkElement())
        AniStop("PageRight PageChange " & PageUuid)
        AniStart({
            AaOpacity(PanRoot, -1, 90),
            AaCode(Sub()
                       PanRoot.Opacity = 1
                       Child.Visibility = Visibility.Collapsed
                       PageOnExitAnimationFinished()
                   End Sub, 100, True)
        }, "PageRight PageChange " & PageUuid, True)
    End Sub


    '===============================
    '  地形浮雕
    '===============================

    ''' <summary>是否按地表高度给地形着色（浮雕）。</summary>
    Private ReliefEnabled As Boolean = False

    ''' <summary>浮雕开关：切换后作废旧瓦片并重新生成。</summary>
    Private Sub BtnToolRelief_Click(sender As Object, e As RoutedEventArgs) Handles BtnToolRelief.Click
        ReliefEnabled = Not ReliefEnabled
        If ReliefEnabled Then
            BtnToolRelief.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnToolRelief.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
            Hint("浮雕：已开启（按地表高度着色）", HintType.Blue)
        Else
            BtnToolRelief.Background = Brushes.Transparent
            BtnToolRelief.Foreground = TryCast(FindResource("BrushText2"), Brush)
            Hint("浮雕：已关闭（纯群系配色）", HintType.Blue)
        End If
        ClearTiles()
        RefreshTiles(True)
        StartTileWorker() '万一 worker 都空闲退出了，这里重新拉起来
        SaveSwitches()
    End Sub
    '===============================
    '  3D 预览（框选 + 鼠标旋转）
    '===============================

    ''' <summary>是否处于「框选 3D 区域」模式。</summary>
    Private Select3DMode As Boolean = False
    ''' <summary>框选区域边长（方块）。</summary>
    Private Const Sel3DBlocks As Double = 512
    ''' <summary>框选中心（世界方块坐标，吸附到区块）。</summary>
    Private Sel3DCenter As New Point(0, 0)

    '3D 模型缓存
    Private _3DN As Integer = 0
    Private _3DStep As Double = 1
    Private _3DHeights As Single()
    Private _3DIds As Integer()
    Private _3DYaw As Double = 0.6
    ''' <summary>俯仰角：0.02 ≈ 平视，1.45 ≈ 近乎俯视。</summary>
    Private _3DPitch As Double = 0.62
    Private _3DDragging As Boolean = False
    Private _3DDragX As Double = 0
    Private _3DDragY As Double = 0
    Private _3DLastRender As Long = 0
    Private Const Pitch3DMin As Double = 0.02
    Private Const Pitch3DMax As Double = 1.45

    '固定比例 + 滚轮缩放：以前每帧按旋转后的包围盒自适应缩放，一转身模型就忽大忽小
    Private _3DZoom As Double = 1.0
    Private Const _3DZoomMin As Double = 0.5
    Private Const _3DZoomMax As Double = 3.0
    Private _3DBaseS As Double = 0
    Private _3DBase As Double = 0
    Private _3DTopH As Double = 0
    Private _3DMinH As Double = 0
    '每格的顶面高度与颜色（与旋转角无关，打开时算一次）
    Private _3DTopY As Double()
    Private _3DColR As Integer()
    Private _3DColG As Integer()
    Private _3DColB As Integer()
    '像素缓冲复用，避免每帧重新分配 2MB
    Private _3DPx As Byte()
    Private _3DPxW As Integer = 0
    Private _3DPxH As Integer = 0
    Private _3DBgRow As Byte()
    '画布尺寸按面板实际宽高比来（1:1 像素），否则 Stretch=Uniform 会在两侧留出空白
    Private _3DVbW As Integer = 900
    Private _3DVbH As Integer = 620

    '预览区域里要画出来的结构标记
    Private Class Struct3D
        Public X As Double
        Public Z As Double
        Public Col As Color
        Public Icon As String
    End Class
    Private ReadOnly _3DStructs As New List(Of Struct3D)

    '结构图标的像素缓存（2D 贴图，始终正对屏幕）
    Private Class IconPix
        Public W As Integer
        Public H As Integer
        Public Data As Byte()
    End Class
    Private Shared ReadOnly StructIconPixCache As New Dictionary(Of String, IconPix)

    ''' <summary>取结构图标的 Bgra32 像素（转成非预乘、冻结，便于直接合成到位图里）。</summary>
    Private Shared Function GetStructIconPixels(Key As String) As IconPix
        If String.IsNullOrEmpty(Key) Then Return Nothing
        SyncLock StructIconPixCache
            If StructIconPixCache.ContainsKey(Key) Then Return StructIconPixCache(Key)
            Dim Result As IconPix = Nothing
            Try
                Dim Src = TryCast(GetStructIcon(Key), BitmapSource)
                If Src IsNot Nothing Then
                    Dim Conv As New FormatConvertedBitmap(Src, PixelFormats.Bgra32, Nothing, 0)
                    Conv.Freeze()
                    Dim W As Integer = Conv.PixelWidth, H As Integer = Conv.PixelHeight
                    Dim Data(W * H * 4 - 1) As Byte
                    Conv.CopyPixels(Data, W * 4, 0)
                    Result = New IconPix With {.W = W, .H = H, .Data = Data}
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取结构图标失败")
            End Try
            StructIconPixCache(Key) = Result
            Return Result
        End SyncLock
    End Function

    ''' <summary>把图标按 alpha 混合贴到位图上（CX 为水平中心，Bottom 为底边），实现"始终面向屏幕"的 2D 标记。</summary>
    Private Shared Sub BlitStructIcon(Px As Byte(), W As Integer, H As Integer, CX As Double, Bottom As Double,
                                      Icon As IconPix, SizePx As Integer)
        If Px Is Nothing OrElse Icon Is Nothing OrElse Icon.Data Is Nothing Then Return
        If SizePx < 6 Then Return
        Dim X0 As Integer = CInt(Math.Round(CX - SizePx / 2.0))
        Dim Y0 As Integer = CInt(Math.Round(Bottom - SizePx))
        For dy As Integer = 0 To SizePx - 1
            Dim Yy As Integer = Y0 + dy
            If Yy < 0 OrElse Yy >= H Then Continue For
            Dim Sy As Integer = Math.Min(Icon.H - 1, dy * Icon.H \ SizePx)
            For dx As Integer = 0 To SizePx - 1
                Dim Xx As Integer = X0 + dx
                If Xx < 0 OrElse Xx >= W Then Continue For
                Dim Sx As Integer = Math.Min(Icon.W - 1, dx * Icon.W \ SizePx)
                Dim Si As Integer = (Sy * Icon.W + Sx) * 4
                Dim A As Integer = Icon.Data(Si + 3)
                If A = 0 Then Continue For
                Dim Di As Integer = (Yy * W + Xx) * 4
                If A = 255 Then
                    Px(Di) = Icon.Data(Si)
                    Px(Di + 1) = Icon.Data(Si + 1)
                    Px(Di + 2) = Icon.Data(Si + 2)
                Else
                    Dim Inv As Integer = 255 - A
                    Px(Di) = CByte((Icon.Data(Si) * A + Px(Di) * Inv) \ 255)
                    Px(Di + 1) = CByte((Icon.Data(Si + 1) * A + Px(Di + 1) * Inv) \ 255)
                    Px(Di + 2) = CByte((Icon.Data(Si + 2) * A + Px(Di + 2) * Inv) \ 255)
                End If
                Px(Di + 3) = 255
            Next
        Next
    End Sub
    ''' <summary>采样网格西北角的方块坐标（用于把结构位置换算成网格下标）。</summary>
    Private _3DOriginX As Double = 0
    Private _3DOriginZ As Double = 0
    ''' <summary>最近一次"点面板外关掉预览"的时刻，用来避免同一次点击又把预览打开。</summary>
    Private _3DCloseTick As Long = 0
    Private _3DSuppressOpenThisClick As Boolean = False

    ''' <summary>退出 3D 框选模式并复位按钮外观（不关闭已打开的 3D 预览）。</summary>
    Private Sub Exit3DModeVisuals()
        Select3DMode = False
        BtnTool3D.Background = Brushes.Transparent
        BtnTool3D.Foreground = TryCast(FindResource("BrushText2"), Brush)
        PanMapViewport.Cursor = Cursors.ScrollAll
        PanSelect3D.Children.Clear()
    End Sub

    Private Sub BtnTool3D_Click(sender As Object, e As RoutedEventArgs) Handles BtnTool3D.Click
        If Select3DMode Then
            '再次点击按钮：退出框选并收起预览
            Exit3DModeVisuals()
            CloseView3DNow()
            Return
        End If
        Select3DMode = True
        BtnTool3D.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
        BtnTool3D.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
        PanMapViewport.Cursor = Cursors.Cross
        '先在视野中心放一个选框，避免开启后看不到反馈
        If PanMapViewport.ActualWidth > 40 AndAlso PanMapViewport.ActualHeight > 40 Then
            UpdateSelect3DBox(New Point(PanMapViewport.ActualWidth / 2, PanMapViewport.ActualHeight / 2))
        End If
        Hint("3D 预览：已开启（移动鼠标框选，左键查看，右键取消）", HintType.Blue)
    End Sub

    ''' <summary>把框选框移到指定屏幕点（中心吸附到 16 方块区块网格）。</summary>
    Private Sub UpdateSelect3DBox(P As Point)
        Try
            If PanSelect3D Is Nothing OrElse Zoom <= 0 Then Return
            Dim U0 = ScreenToUnit(P)
            Dim Ux = U0.X, Uy = U0.Y
            Dim Bx = Math.Round(Ux * BlocksPerUnit / 16) * 16
            Dim Bz = Math.Round(Uy * BlocksPerUnit / 16) * 16
            Sel3DCenter = New Point(Bx, Bz)
            Dim HalfUnits As Double = (Sel3DBlocks / 2) / BlocksPerUnit
            Dim U As New Point(Bx / BlocksPerUnit, Bz / BlocksPerUnit)
            PanSelect3D.Children.Clear()
            Dim FillBrush As New SolidColorBrush(Color.FromArgb(38, 140, 210, 110))
            FillBrush.Freeze()
            Dim Rc As New Rectangle With {
                .Width = HalfUnits * 2, .Height = HalfUnits * 2,
                .Stroke = TryCast(FindResource("ColorBrush3"), Brush),
                .StrokeThickness = 2 / Zoom,
                .Fill = FillBrush}
            Canvas.SetLeft(Rc, U.X - HalfUnits)
            Canvas.SetTop(Rc, U.Y - HalfUnits)
            PanSelect3D.Children.Add(Rc)
        Catch ex As Exception
            Logger.Warn(ex, "更新 3D 框选失败")
        End Try
    End Sub

    ''' <summary>按框选中心取高程与群系，再渲染成可旋转的等轴测模型。</summary>
    Private Sub Open3DAt(Center As Point)
        Try
            If Not ModuleSeedMap.IsNativeAvailable Then
                Hint("缺少 cubiomes.dll，无法生成 3D 预览", HintType.Red)
                Return
            End If
            If Dimension <> 0 Then
                Hint("3D 预览目前只支持主世界", HintType.Red)
                Return
            End If
            Dim N As Integer = 64
            Dim StepB As Double = Sel3DBlocks / N
            Dim Step4 As Integer = Math.Max(1, CInt(Math.Round(StepB / 4)))
            Dim X4 As Integer = CInt(Math.Floor(Center.X / 4)) - (N * Step4) \ 2
            Dim Z4 As Integer = CInt(Math.Floor(Center.Y / 4)) - (N * Step4) \ 2
            Dim Ver As Integer = ModuleSeedMap.VersionIds(VersionIndex)
            _3DHeights = ModuleSeedMap.SampleHeights(Ver, SeedValue, X4, Z4, N, Step4)
            _3DIds = ModuleSeedMap.SampleBiomeGrid(SeedValue, Ver, 0, X4 * 4, Z4 * 4, N, Step4 * 4)
            _3DN = N
            _3DStep = Step4 * 4
            _3DOriginX = X4 * 4
            _3DOriginZ = Z4 * 4
            _3DZoom = 1.0
            '先让面板参与布局，才能拿到真实宽高去定画布尺寸
            PanView3D.Visibility = Visibility.Visible
            PanView3D.UpdateLayout()
            Sync3DBitmapSize()
            Prepare3DCells()
            Render3D()
            LabView3D.Text = String.Format("3D 预览 · {0}×{0} 方块 · 拖动旋转 · 滚轮缩放 · 右键关闭", CInt(Sel3DBlocks))
            ShowView3D()
        Catch ex As Exception
            Logger.Warn(ex, "生成 3D 预览失败")
            Hint("生成 3D 预览失败：" & ex.Message, HintType.Red)
            PanView3D.Visibility = Visibility.Collapsed
        End Try
    End Sub

    ''' <summary>
    ''' 打开时预处理：把与旋转角无关的东西都算好——每格顶面高度、每格颜色、
    ''' 以及「任意角度都不会超出画布」的固定比例（这样旋转时模型不会变大变小）。
    ''' </summary>
    Private Sub Prepare3DCells()
        Dim N As Integer = _3DN
        Dim Count As Integer = N * N
        '地形筛选：未选中的群系向画布背景色（天空色）淡化
        Dim Filtering As Boolean = SelectedBiomes IsNot Nothing AndAlso SelectedBiomes.Count > 0
        Const FilterKeep As Double = 0.18
        Const SkyR As Integer = 224, SkyG As Integer = 205, SkyB As Integer = 176
        Dim MinH As Double = Double.MaxValue, MaxH As Double = Double.MinValue
        For i As Integer = 0 To Count - 1
            Dim v As Double = _3DHeights(i)
            If v < MinH Then MinH = v
            If v > MaxH Then MaxH = v
        Next
        If MaxH <= MinH Then MaxH = MinH + 1
        _3DMinH = MinH
        _3DBase = Math.Min(MinH, 63) - 14
        _3DTopH = Math.Max(MaxH, 63)
        If _3DTopY Is Nothing OrElse _3DTopY.Length <> Count Then
            _3DTopY = New Double(Count - 1) {}
            _3DColR = New Integer(Count - 1) {}
            _3DColG = New Integer(Count - 1) {}
            _3DColB = New Integer(Count - 1) {}
        End If
        For k As Integer = 0 To Count - 1
            Dim Hh As Double = Math.Max(_3DHeights(k), 63)
            _3DTopY(k) = Hh
            Dim BiomeId As Integer = _3DIds(k)
            If BiomeId < 0 OrElse BiomeId > 255 Then BiomeId = 127
            Dim Col As Color = ModuleSeedMap.BiomeColors(BiomeId)
            Dim R As Integer = Col.R, G As Integer = Col.G, B As Integer = Col.B
            If _3DHeights(k) < 63 Then
                R = 58 : G = 104 : B = 196
            Else
                Dim Brt As Double = 0.85 + Math.Min(0.3, (_3DHeights(k) - MinH) / Math.Max(1, MaxH - MinH) * 0.3)
                R = CInt(Math.Min(255, R * Brt))
                G = CInt(Math.Min(255, G * Brt))
                B = CInt(Math.Min(255, B * Brt))
            End If
            '地形筛选生效时：没选中的群系向天空色（画布背景色）淡化，和 2D 地图的表现一致
            If Filtering AndAlso Not SelectedBiomes.Contains(BiomeId) Then
                R = CInt(SkyR + (R - SkyR) * FilterKeep)
                G = CInt(SkyG + (G - SkyG) * FilterKeep)
                B = CInt(SkyB + (B - SkyB) * FilterKeep)
            End If
            _3DColR(k) = R : _3DColG(k) = G : _3DColB(k) = B
        Next
        Collect3DStructs()
        '固定比例：水平取「正对对角线」的宽度，竖直取整段俯仰范围里最大的投影跨度
        Dim Half As Double = N * _3DStep / 2
        Dim HalfDiag As Double = Half * Math.Sqrt(2)
        Dim Span As Double = _3DTopH - _3DBase
        Dim NeedH As Double = 1
        Dim P As Double = Pitch3DMin
        Do While P <= Pitch3DMax + 0.0001
            Dim V As Double = Span * Math.Cos(P) + 2 * HalfDiag * Math.Sin(P)
            If V > NeedH Then NeedH = V
            P += 0.02
        Loop
        Dim NeedW As Double = 2 * HalfDiag
        _3DBaseS = Math.Min((_3DVbW - 12) / Math.Max(1, NeedW), (_3DVbH - 12) / Math.Max(1, NeedH))
    End Sub

    ''' <summary>把当前维度里「已勾选显示」的结构中、落在预览范围内的那些收集起来。</summary>
    Private Sub Collect3DStructs()
        _3DStructs.Clear()
        Try
            If Structs Is Nothing OrElse StructPoints Is Nothing Then Return
            Dim Half As Double = Sel3DBlocks / 2
            Dim Cx As Double = _3DOriginX + _3DN * _3DStep / 2
            Dim Cz As Double = _3DOriginZ + _3DN * _3DStep / 2
            For Idx As Integer = 0 To Structs.Count - 1
                Dim Def = Structs(Idx)
                If Not Def.Enabled OrElse Not Def.Supported Then Continue For
                If Idx >= StructPoints.Count Then Continue For
                Dim Pts = StructPoints(Idx)
                If Pts Is Nothing Then Continue For
                For Each Pt In Pts
                    If Pt.X < Cx - Half OrElse Pt.X > Cx + Half OrElse Pt.Y < Cz - Half OrElse Pt.Y > Cz + Half Then Continue For
                    _3DStructs.Add(New Struct3D With {.X = Pt.X, .Z = Pt.Y, .Col = Def.Color, .Icon = Def.IconKey})
                Next
            Next
        Catch ex As Exception
            Logger.Warn(ex, "收集 3D 结构标记失败")
        End Try
    End Sub

    ''' <summary>按 3D 面板当前宽高比设置画布尺寸（变化时重算固定比例）。</summary>
    Private Sub Sync3DBitmapSize()
        Try
            Dim W As Double = PanView3D.ActualWidth, H As Double = PanView3D.ActualHeight
            If W < 120 OrElse H < 120 Then Return
            '保持和面板完全相同的宽高比，只在总像素过多时按比例缩小（避免两侧或上下留白）
            Dim F As Double = Math.Min(1.0, Math.Sqrt((1500.0 * 1100.0) / (W * H)))
            Dim NewW As Integer = CInt(Math.Max(240, Math.Round(W * F)))
            Dim NewH As Integer = CInt(Math.Max(180, Math.Round(H * F)))
            If NewW = _3DVbW AndAlso NewH = _3DVbH Then Return
            _3DVbW = NewW
            _3DVbH = NewH
            Prepare3DCells()
        Catch ex As Exception
            Logger.Warn(ex, "同步 3D 画布尺寸失败")
        End Try
    End Sub

    ''' <summary>
    ''' 渲染 3D 模型（正交投影 + 按深度画家算法）。始终按整格精度绘制：拖拽时不降采样、
    ''' 不让方块消失；比例固定，旋转只改变朝向（大小交给滚轮）。
    ''' </summary>
    Private Sub Render3D()
        If _3DN <= 0 OrElse _3DHeights Is Nothing Then Return
        Try
            If _3DBaseS <= 0 Then Prepare3DCells()
            Dim N As Integer = _3DN
            Dim Count As Integer = N * N
            Dim CellB As Double = _3DStep
            Dim Half As Double = N * CellB / 2
            Dim SinY As Double = Math.Sin(_3DYaw), CosY As Double = Math.Cos(_3DYaw)
            Dim SinP As Double = Math.Sin(_3DPitch), CosP As Double = Math.Cos(_3DPitch)
            Dim W As Integer = _3DVbW, Hs As Integer = _3DVbH
            Dim S As Double = _3DBaseS * _3DZoom
            '比例固定 → 只按当前俯仰把模型竖直居中，不再按包围盒自适应缩放
            Dim OffX As Double = W / 2
            Dim OffY As Double = Hs / 2 + ((_3DBase + _3DTopH) / 2) * CosP * S
            Dim KC As Double = CosP * S
            '预计算每条网格线位置的投影分量：省掉每格 4 角的三角函数乘法
            Dim PXs(N) As Double, PZs(N) As Double
            For ni As Integer = 0 To N
                Dim A As Double = ni * CellB - Half
                PXs(ni) = A * CosY * S
                PZs(ni) = A * SinY * S
            Next
            '像素缓冲复用 + 整行拷贝清背景（之前每帧新建 2MB 数组并逐像素填充）
            If _3DPx Is Nothing OrElse _3DPxW <> W OrElse _3DPxH <> Hs Then
                _3DPxW = W : _3DPxH = Hs
                _3DPx = New Byte(W * Hs * 4 - 1) {}
                _3DBgRow = New Byte(W * 4 - 1) {}
                For i As Integer = 0 To W - 1
                    _3DBgRow(i * 4) = 224 : _3DBgRow(i * 4 + 1) = 205 : _3DBgRow(i * 4 + 2) = 176 : _3DBgRow(i * 4 + 3) = 255
                Next
            End If
            For r As Integer = 0 To Hs - 1
                Buffer.BlockCopy(_3DBgRow, 0, _3DPx, r * W * 4, W * 4)
            Next
            '按真实视线深度排序：远的先画。depth = Rz·cosP + y·sinP，横向/纵深两维可分离
            Dim Keys(Count - 1) As Double
            Dim Idx(Count - 1) As Integer
            Dim Cx(N - 1) As Double, Cz(N - 1) As Double
            For i As Integer = 0 To N - 1
                Cx(i) = ((i + 0.5) * CellB - Half) * SinY * CosP
            Next
            For j As Integer = 0 To N - 1
                Cz(j) = ((j + 0.5) * CellB - Half) * CosY * CosP
            Next
            For j As Integer = 0 To N - 1
                For i As Integer = 0 To N - 1
                    Dim k As Integer = j * N + i
                    Idx(k) = k
                    Keys(k) = Cx(i) + Cz(j) + _3DTopY(k) * SinP
                Next
            Next
            Array.Sort(Keys, Idx)
            Dim QuadX(3) As Double, QuadY(3) As Double
            Dim TopX(3) As Double, TopY(3) As Double
            Dim DTv(3) As Double
            '两堵可见侧面：+x 面（sinY>0）或 -x 面；+z 面（cosY>0）或 -z 面
            Dim A0 As Integer = If(SinY > 0, 1, 3)
            Dim A1 As Integer = (A0 + 1) Mod 4
            Dim B0 As Integer = If(CosY > 0, 2, 0)
            Dim B1 As Integer = (B0 + 1) Mod 4
            '该侧面对应的邻格方向（用来看"墙有没有露出来"）
            Dim DxA As Integer = If(SinY > 0, 1, -1)
            Dim DzB As Integer = If(CosY > 0, 1, -1)
            '四个角在网格线上的索引偏移：0=(i,j) 1=(i+1,j) 2=(i+1,j+1) 3=(i,j+1)
            Dim Kx As Integer() = {0, 1, 1, 0}
            Dim Kz As Integer() = {0, 0, 1, 1}
            For oi As Integer = 0 To Count - 1
                Dim k As Integer = Idx(oi)
                Dim i As Integer = k Mod N
                Dim j As Integer = k \ N
                Dim Hh As Double = _3DTopY(k)
                Dim TopSy As Double = OffY - Hh * KC
                For c As Integer = 0 To 3
                    Dim nx As Integer = i + Kx(c)
                    Dim nz As Integer = j + Kz(c)
                    Dim DT As Double = PZs(nx) + PXs(nz)
                    DTv(c) = DT
                    TopX(c) = OffX + PXs(nx) - PZs(nz)
                    TopY(c) = TopSy + DT * SinP
                Next
                Dim R As Integer = _3DColR(k), G As Integer = _3DColG(k), B As Integer = _3DColB(k)
                '只画「邻格比自己低」的那堵墙：同一高度不画。
                '以前每格都把侧墙拉到统一底座，相邻同高的墙会从邻格顶面里刺出来，
                '高空俯视时表现为一条条暗色薄片/尖刺，这就是"转到顶端向下看模型出问题"的原因。
                Dim HnA As Double = _3DBase
                Dim NxA As Integer = i + DxA
                If NxA >= 0 AndAlso NxA < N Then HnA = _3DTopY(j * N + NxA)
                If Hh > HnA + 0.0001 Then
                    Dim SyN As Double = OffY - HnA * KC
                    QuadX(0) = TopX(A0) : QuadY(0) = TopY(A0)
                    QuadX(1) = TopX(A1) : QuadY(1) = TopY(A1)
                    QuadX(2) = TopX(A1) : QuadY(2) = SyN + DTv(A1) * SinP + 1
                    QuadX(3) = TopX(A0) : QuadY(3) = SyN + DTv(A0) * SinP + 1
                    FillQuad4(_3DPx, W, Hs, QuadX, QuadY, CByte(R * 0.62), CByte(G * 0.62), CByte(B * 0.62))
                End If
                Dim HnB As Double = _3DBase
                Dim NzB As Integer = j + DzB
                If NzB >= 0 AndAlso NzB < N Then HnB = _3DTopY(NzB * N + i)
                If Hh > HnB + 0.0001 Then
                    Dim SyN As Double = OffY - HnB * KC
                    QuadX(0) = TopX(B0) : QuadY(0) = TopY(B0)
                    QuadX(1) = TopX(B1) : QuadY(1) = TopY(B1)
                    QuadX(2) = TopX(B1) : QuadY(2) = SyN + DTv(B1) * SinP + 1
                    QuadX(3) = TopX(B0) : QuadY(3) = SyN + DTv(B0) * SinP + 1
                    FillQuad4(_3DPx, W, Hs, QuadX, QuadY, CByte(R * 0.78), CByte(G * 0.78), CByte(B * 0.78))
                End If
                '顶面
                For c As Integer = 0 To 3
                    QuadX(c) = TopX(c) : QuadY(c) = TopY(c)
                Next
                FillQuad4(_3DPx, W, Hs, QuadX, QuadY, CByte(R), CByte(G), CByte(B))
            Next
            '当前维度里勾选了显示的结构：在地形之上贴 2D 图标（始终正对屏幕），位置取该处地表投影点
            If _3DStructs.Count > 0 Then
                Dim MCount As Integer = _3DStructs.Count
                Dim MSx(MCount - 1) As Double, MSy(MCount - 1) As Double, MDep(MCount - 1) As Double
                Dim MOrder As New List(Of Integer)
                For si As Integer = 0 To MCount - 1
                    Dim St0 = _3DStructs(si)
                    Dim gi0 = Math.Max(0, Math.Min(N - 1, CInt(Math.Floor((St0.X - _3DOriginX) / _3DStep))))
                    Dim gj0 = Math.Max(0, Math.Min(N - 1, CInt(Math.Floor((St0.Z - _3DOriginZ) / _3DStep))))
                    Dim SurfY0 As Double = _3DTopY(gj0 * N + gi0)
                    Dim relx0 = St0.X - (_3DOriginX + Half), relz0 = St0.Z - (_3DOriginZ + Half)
                    Dim Rx0 = relx0 * CosY - relz0 * SinY
                    Dim Rz0 = relx0 * SinY + relz0 * CosY
                    MSx(si) = OffX + Rx0 * S
                    MSy(si) = OffY - SurfY0 * KC + Rz0 * SinP * S
                    MDep(si) = Rz0 * CosP + SurfY0 * SinP
                    MOrder.Add(si)
                Next
                MOrder.Sort(Function(a, b) MDep(a).CompareTo(MDep(b)))
                '图标大小固定为屏幕像素（跟着滚轮缩放略微变大），不随地形缩放变形
                Dim IcoPx As Integer = CInt(Math.Max(14, Math.Min(46, 24 * _3DZoom)))
                Dim Drawn As Integer = 0
                For Each si In MOrder
                    If Drawn >= 240 Then Exit For '极端情况保护
                    Dim Icon = GetStructIconPixels(_3DStructs(si).Icon)
                    If Icon Is Nothing Then
                        '取不到图标就退化成一个小色块，保证位置仍然可见
                        Dim Sqr As Integer = Math.Max(4, IcoPx \ 3)
                        QuadX(0) = MSx(si) - Sqr : QuadY(0) = MSy(si) - Sqr * 2
                        QuadX(1) = MSx(si) + Sqr : QuadY(1) = MSy(si) - Sqr * 2
                        QuadX(2) = MSx(si) + Sqr : QuadY(2) = MSy(si) - Sqr
                        QuadX(3) = MSx(si) - Sqr : QuadY(3) = MSy(si) - Sqr
                        Dim Mc = _3DStructs(si).Col
                        FillQuad4(_3DPx, W, Hs, QuadX, QuadY, Mc.R, Mc.G, Mc.B)
                    Else
                        BlitStructIcon(_3DPx, W, Hs, MSx(si), MSy(si) + 2, Icon, IcoPx)
                    End If
                    Drawn += 1
                Next
            End If
            Dim Bmp = BitmapSource.Create(W, Hs, 96, 96, PixelFormats.Bgra32, Nothing, _3DPx, W * 4)
            Bmp.Freeze()
            ImgView3D.Source = Bmp
            _3DLastRender = CLng(Environment.TickCount)
        Catch ex As Exception
            Logger.Warn(ex, "渲染 3D 预览失败")
        End Try
    End Sub
    ''' <summary>把四边形用扫描线填进 Bgra32 像素数组。</summary>
    Private Shared Sub FillQuad4(Px As Byte(), W As Integer, H As Integer, Xs As Double(), Ys As Double(), R As Byte, G As Byte, B As Byte)
        Dim MinY As Double = Double.MaxValue, MaxY As Double = Double.MinValue
        For k As Integer = 0 To 3
            If Ys(k) < MinY Then MinY = Ys(k)
            If Ys(k) > MaxY Then MaxY = Ys(k)
        Next
        Dim Y0 As Integer = Math.Max(0, CInt(Math.Floor(MinY)))
        Dim Y1 As Integer = Math.Min(H - 1, CInt(Math.Ceiling(MaxY)))
        If Y1 < Y0 Then Return
        Dim Xa As Double, Xb As Double
        For Y As Integer = Y0 To Y1
            Xa = Double.MaxValue : Xb = Double.MinValue
            For k As Integer = 0 To 3
                Dim k2 As Integer = (k + 1) Mod 4
                Dim AY As Double = Ys(k), BY As Double = Ys(k2)
                If (AY <= Y AndAlso BY > Y) OrElse (BY <= Y AndAlso AY > Y) Then
                    Dim T As Double = (Y - AY) / (BY - AY)
                    Dim Xv As Double = Xs(k) + T * (Xs(k2) - Xs(k))
                    If Xv < Xa Then Xa = Xv
                    If Xv > Xb Then Xb = Xv
                End If
            Next
            If Xa > Xb Then Continue For
            Dim P0 As Integer = Math.Max(0, CInt(Math.Floor(Xa)))
            Dim P1 As Integer = Math.Min(W - 1, CInt(Math.Ceiling(Xb)))
            For X As Integer = P0 To P1
                Dim Off As Integer = (Y * W + X) * 4
                Px(Off) = B : Px(Off + 1) = G : Px(Off + 2) = R : Px(Off + 3) = 255
            Next
        Next
    End Sub

    '---- 3D 面板的出现 / 退出动画：淡入淡出 + 轻微缩放 ----
    Private Sub ShowView3D()
        AniStop("SeedMap View3D")
        PanView3D.Visibility = Visibility.Visible
        PanView3D.Opacity = 0
        PanView3D.RenderTransformOrigin = New Point(0.5, 0.5)
        PanView3D.RenderTransform = New ScaleTransform(0.96, 0.96)
        AniStart({
            AaOpacity(PanView3D, 1, 180, , New AniEaseOutFluent(AniEasePower.Middle)),
            AaScaleTransform(PanView3D, 0.04, 220, , New AniEaseOutFluent(AniEasePower.Middle))
        }, "SeedMap View3D", True)
    End Sub

    Private Sub HideView3D()
        If PanView3D Is Nothing OrElse PanView3D.Visibility <> Visibility.Visible Then Return
        AniStop("SeedMap View3D")
        AniStart({
            AaOpacity(PanView3D, -PanView3D.Opacity, 150, , New AniEaseInFluent(AniEasePower.Middle)),
            AaScaleTransform(PanView3D, -0.04, 170, , New AniEaseInFluent(AniEasePower.Middle)),
            AaCode(Sub()
                       PanView3D.Visibility = Visibility.Collapsed
                       PanView3D.Opacity = 1
                       PanView3D.RenderTransform = New ScaleTransform(1, 1)
                   End Sub, 160)
        }, "SeedMap View3D", True)
    End Sub

    ''' <summary>立刻收起 3D 预览（不留动画）：离开页面、切维度等场合用。</summary>
    Private Sub CloseView3DNow()
        Try
            If PanView3D Is Nothing Then Return
            AniStop("SeedMap View3D")
            PanView3D.Visibility = Visibility.Collapsed
            PanView3D.Opacity = 1
            PanView3D.RenderTransform = New ScaleTransform(1, 1)
        Catch ex As Exception
            Logger.Warn(ex, "收起 3D 预览失败")
        End Try
    End Sub

    Private Function IsInsideView3D(Node As DependencyObject) As Boolean
        Return IsInsideElement(Node, PanView3D)
    End Function

    ''' <summary>Node 是否落在 Target 子树里（用于判断点击落在哪个浮层上）。</summary>
    Private Function IsInsideElement(Node As DependencyObject, Target As DependencyObject) As Boolean
        If Node Is Nothing OrElse Target Is Nothing Then Return False
        Dim V = TryCast(Node, Visual)
        If V Is Nothing Then Return False
        Return V.IsDescendantOf(Target)
    End Function

    ''' <summary>点 3D 面板以外的地图区域收起预览；左侧工具栏与它弹出的面板不算。</summary>
    Private Sub PageSeedMap_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        If PanView3D Is Nothing OrElse PanView3D.Visibility <> Visibility.Visible Then Return
        Dim Src = TryCast(e.OriginalSource, DependencyObject)
        If IsInsideElement(Src, PanView3D) Then Return
        '左侧菜单（工具栏 / 结构面板 / 地形面板）里的操作不打断 3D 预览
        If IsInsideElement(Src, PanToolRail) Then Return
        If IsInsideElement(Src, PanStructures) Then Return
        If IsInsideElement(Src, PanBiomes) Then Return
        CloseView3DNow()
        '记下时刻：如果这一下正好点在地图上，松手时不要再触发一次预览
        _3DCloseTick = CLng(Environment.TickCount)
    End Sub

    '---- 3D 面板交互：拖动旋转（左右转方位、上下转俯仰）/ 滚轮缩放 / 右键关闭 ----
    Private Sub PanView3D_MouseDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton <> MouseButton.Left Then Return
        _3DDragging = True
        Dim P = e.GetPosition(PanView3D)
        _3DDragX = P.X
        _3DDragY = P.Y
        PanView3D.CaptureMouse()
    End Sub

    Private Sub PanView3D_MouseMove(sender As Object, e As MouseEventArgs)
        If Not _3DDragging Then Return
        Dim P = e.GetPosition(PanView3D)
        '方向与鼠标一致：向右拖，模型正面跟着往右转；向下拖，模型向前倾倒露出顶面
        _3DYaw -= (P.X - _3DDragX) * 0.011
        If _3DYaw > Math.PI * 2 Then _3DYaw -= Math.PI * 2
        If _3DYaw < -Math.PI * 2 Then _3DYaw += Math.PI * 2
        _3DPitch = Math.Max(Pitch3DMin, Math.Min(Pitch3DMax, _3DPitch + (P.Y - _3DDragY) * 0.008))
        _3DDragX = P.X
        _3DDragY = P.Y
        '始终整格精度重绘（降采样会让方块变糊、甚至看起来缺块），只按帧间隔节流
        If CLng(Environment.TickCount) - _3DLastRender >= 16 Then Render3D()
    End Sub

    Private Sub PanView3D_MouseUp(sender As Object, e As MouseButtonEventArgs)
        If Not _3DDragging Then Return
        _3DDragging = False
        PanView3D.ReleaseMouseCapture()
        Render3D()
    End Sub

    ''' <summary>滚轮缩放 3D 模型（旋转时比例固定，大小只由滚轮控制）。</summary>
    Private Sub PanView3D_MouseWheel(sender As Object, e As MouseWheelEventArgs)
        If _3DN <= 0 Then Return
        Dim StepFactor As Double = If(e.Delta > 0, 1.12, 1 / 1.12)
        _3DZoom = Math.Max(_3DZoomMin, Math.Min(_3DZoomMax, _3DZoom * StepFactor))
        Render3D()
        e.Handled = True
    End Sub

    Private Sub PanView3D_MouseRightUp(sender As Object, e As MouseButtonEventArgs)
        HideView3D()
        e.Handled = True
    End Sub



    '===============================
    '  第一人称模式（点地图落点 → 覆盖页面加载 → WASD/空格/Shift 走动 → Esc 退出）
    '===============================

    ''' <summary>是否处于第一人称的「选点」状态。</summary>
    Private FpsSelecting As Boolean = False
    ''' <summary>是否已经进入第一人称。</summary>
    Private FpsActive As Boolean = False

    '地形采样缓存（以 FpsCenter 为中心的 N×N 网格，FpsStep 方块/格）
    'cubiomes 的群系/高程最小尺度是 4 方块，所以格距取 4（比原来的 8 细一倍，近处地形不再像断崖）
    Private Const FpsN As Integer = 128
    Private Const FpsStep As Integer = 4
    ''' <summary>视线高度（方块）：Minecraft 玩家身高 1.8、眼睛在 1.62，与游戏内比例一致。</summary>
    Private Const FpsEyeBlock As Double = 1.62
    Private FpsHeights As Single()
    Private FpsIds As Integer()
    Private FpsColR As Integer(), FpsColG As Integer(), FpsColB As Integer()
    Private FpsOriginX As Double = 0, FpsOriginZ As Double = 0
    Private FpsReloading As Boolean = False
    ''' <summary>第一人称会话代数：进入时 +1，退出时 +1；旧会话的后台采样完成后发现代数变了就丢弃。</summary>
    Private FpsGen As Integer = 0

    '玩家状态
    Private FpsX As Double = 0, FpsZ As Double = 0
    Private FpsEyeY As Double = 70
    Private FpsVelY As Double = 0
    Private FpsOnGround As Boolean = True
    ''' <summary>飞行模式（双击空格切换）。</summary>
    Private FpsFlying As Boolean = False
    ''' <summary>上一次按下空格的时间，用于双击判定。</summary>
    Private FpsLastSpaceTick As Long = 0
    Private FpsYaw As Double = 0
    ''' <summary>俯仰角（弧度，正值抬头）。Voxel 渲染里俯仰表现为地平线整体上下移动。</summary>
    Private FpsPitch As Double = 0
    Private ReadOnly FpsKeys As New HashSet(Of Key)
    Private FpsLastTick As Long = 0
    Private FpsRenderTick As Long = 0

    '渲染缓冲
    Private Const FpsW As Integer = 640
    Private Const FpsH As Integer = 400
    ''' <summary>上一屏幕列在每个深度步所落的格子编号（0=无效），用于判断竖直方块描边。</summary>
    Private FpsEdgePrevKey(255) As Integer
    Private FpsPx As Byte()
    Private FpsBmp As WriteableBitmap = Nothing

    Private Sub BtnToolFPS_Click(sender As Object, e As RoutedEventArgs) Handles BtnToolFPS.Click
        FpsSelecting = Not FpsSelecting
        If FpsSelecting Then
            BtnToolFPS.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            BtnToolFPS.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
            PanMapViewport.Cursor = Cursors.Cross
            Hint("第一人称：在地图上点击一个位置进入", HintType.Blue)
        Else
            ResetFpsButton()
        End If
    End Sub

    Private Sub ResetFpsButton()
        FpsSelecting = False
        BtnToolFPS.Background = Brushes.Transparent
        BtnToolFPS.Foreground = TryCast(FindResource("BrushText2"), Brush)
        PanMapViewport.Cursor = If(Select3DMode, Cursors.Cross, Cursors.ScrollAll)
    End Sub

    ''' <summary>进入第一人称：覆盖页面（有动画）→ 后台采样 → 渲染循环。</summary>
    Private Sub EnterFirstPerson(Center As Point)
        Try
            If Not ModuleSeedMap.IsNativeAvailable Then
                Hint("缺少 cubiomes.dll，无法进入第一人称", HintType.Red)
                ResetFpsButton()
                Return
            End If
            If Dimension <> 0 Then
                Hint("第一人称目前只支持主世界", HintType.Red)
                ResetFpsButton()
                Return
            End If
            CloseView3DNow()
            ResetFpsButton()
            FpsGen += 1
            Dim Gen As Integer = FpsGen
            FpsX = Center.X
            FpsZ = Center.Y
            FpsYaw = 0
            FpsPitch = 0
            FpsVelY = 0
            FpsOnGround = True
            FpsFlying = False
            FpsLastSpaceTick = 0
            FpsKeys.Clear()
            '盖上去（淡入 + 轻微放大）
            PanFirstPerson.Visibility = Visibility.Visible
            PanFirstPerson.Cursor = Cursors.None '指针锁定式视角：隐藏光标
            PanFirstPerson.Opacity = 0
            PanFirstPerson.RenderTransformOrigin = New Point(0.5, 0.5)
            PanFirstPerson.RenderTransform = New ScaleTransform(1.03, 1.03)
            PanFpsLoading.Visibility = Visibility.Visible
            PanFpsLoading.Opacity = 1
            RectFpsProgress.Width = 0
            LabFpsStatus.Text = "采样地形高程…"
            AniStart({
                AaOpacity(PanFirstPerson, 1, 260, , New AniEaseOutFluent(AniEasePower.Middle)),
                AaScaleTransform(PanFirstPerson, -0.03, 300, , New AniEaseOutFluent(AniEasePower.Middle))
            }, "SeedMap FPS", True)
            PanFirstPerson.Focus()
            '后台采样
            Dim Ver As Integer = ModuleSeedMap.VersionIds(VersionIndex)
            Dim Seed = SeedValue
            Dim Half As Integer = FpsN * FpsStep \ 2
            '原点对齐到 FpsStep 的整数倍：格子晶格固定在世界网格上，重采样时地形不会整体平移（否则贴图会"游动"）
            Dim OriginX As Integer = CInt(Math.Floor((Center.X - Half) / FpsStep)) * FpsStep
            Dim OriginZ As Integer = CInt(Math.Floor((Center.Y - Half) / FpsStep)) * FpsStep
            FpsReloading = False
            RunInThread(
                Sub()
                    Try
                        '加载进度：高程 0–55% → 群系 55–95% → 着色 95–100%
                        Dim Hs = ModuleSeedMap.SampleHeightsBanded(Ver, Seed, OriginX \ 4, OriginZ \ 4, FpsN, FpsStep \ 4, 8,
                            Sub(P)
                                RunInUi(Sub()
                                            If Gen <> FpsGen Then Return
                                            RectFpsProgress.Width = 220 * (P * 0.55)
                                            LabFpsStatus.Text = $"采样地形高程… {CInt(P * 100)}%"
                                        End Sub)
                            End Sub)
                        Dim Ids = ModuleSeedMap.SampleBiomeGridBanded(Seed, Ver, OriginX, OriginZ, FpsN, FpsStep, 8,
                            Sub(P)
                                RunInUi(Sub()
                                            If Gen <> FpsGen Then Return
                                            RectFpsProgress.Width = 220 * (0.55 + P * 0.4)
                                            LabFpsStatus.Text = $"采样生物群系… {CInt(P * 100)}%"
                                        End Sub)
                            End Sub)
                        RunInUi(Sub()
                                    If Gen <> FpsGen Then Return
                                    RectFpsProgress.Width = 220 * 0.97
                                    LabFpsStatus.Text = "着色地形…"
                                End Sub)
                        RunInUi(
                            Sub()
                                If Gen <> FpsGen Then Return '加载期间已退出（或开始了新一次进入），丢弃本次结果
                                FpsHeights = Hs
                                FpsIds = Ids
                                FpsOriginX = OriginX
                                FpsOriginZ = OriginZ
                                PrepareFpsColors()
                                '出生高度：落点地表 + 眼高
                                Dim Si = ClampI(FpsX - FpsOriginX, 0, FpsN - 1) \ FpsStep
                                Dim Sj = ClampI(FpsZ - FpsOriginZ, 0, FpsN - 1) \ FpsStep
                                Dim Ground As Double = FpsHeights(Math.Min(FpsN * FpsN - 1, Sj * FpsN + Si))
                                FpsEyeY = Math.Max(Ground, 62) + FpsEyeBlock
                                RectFpsProgress.Width = 220
                                PanFpsLoading.Opacity = 1
                                AniStart({
                                    AaOpacity(PanFpsLoading, -1, 240, , New AniEaseOutFluent(AniEasePower.Middle)),
                                    AaCode(Sub() PanFpsLoading.Visibility = Visibility.Collapsed, 250, True)
                                }, "SeedMap FPS Load", True)
                                FpsActive = True
                                FpsLastTick = CLng(Environment.TickCount)
                                PanFirstPerson.Focus() '加载完再补一次键盘焦点，确保 WASD 一定生效
                                Try
                                    '进入即把光标置于覆盖层中心，避免开启瞬间的视角跳变
                                    Dim C0 As Point = PanFirstPerson.PointToScreen(New Point(PanFirstPerson.ActualWidth / 2, PanFirstPerson.ActualHeight / 2))
                                    SetCursorPos(CInt(C0.X), CInt(C0.Y))
                                Catch
                                End Try
                                AddHandler CompositionTarget.Rendering, AddressOf FpsFrame
                                FpsFrame(Nothing, Nothing)
                            End Sub)
                    Catch ex As Exception
                        Logger.Warn(ex, "第一人称地形采样失败")
                        RunInUi(
                            Sub()
                                Hint("生成第一人称地形失败：" & ex.Message, HintType.Red)
                                ExitFirstPerson(False)
                            End Sub)
                    End Try
                End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "进入第一人称失败")
            Hint("进入第一人称失败", HintType.Red)
        End Try
    End Sub

    Private Shared Function ClampI(V As Integer, Min As Integer, Max As Integer) As Integer
        If V < Min Then Return Min
        If V > Max Then Return Max
        Return V
    End Function

    ''' <summary>按群系/高度给每格着色（与 2D 地图同一套配色）。</summary>
    Private Sub PrepareFpsColors()
        If FpsIds Is Nothing Then Return
        Dim Total As Integer = FpsN * FpsN
        If FpsColR Is Nothing OrElse FpsColR.Length <> Total Then
            FpsColR = New Integer(Total - 1) {}
            FpsColG = New Integer(Total - 1) {}
            FpsColB = New Integer(Total - 1) {}
        End If
        For k As Integer = 0 To Total - 1
            Dim Id = FpsIds(k)
            If Id < 0 OrElse Id > 255 Then Id = 127
            Dim C As Color = ModuleSeedMap.BiomeColors(Id)
            Dim R As Integer = C.R, G As Integer = C.G, B As Integer = C.B
            If FpsHeights(k) < 63 Then
                R = 58 : G = 104 : B = 196
            Else
                Dim Brt As Double = 0.82 + Math.Min(0.3, (FpsHeights(k) - 40) / 120 * 0.3)
                R = CInt(Math.Min(255, R * Brt)) : G = CInt(Math.Min(255, G * Brt)) : B = CInt(Math.Min(255, B * Brt))
            End If
            FpsColR(k) = R : FpsColG(k) = G : FpsColB(k) = B
        Next
    End Sub

    ''' <summary>退出第一人称（带淡出动画）。</summary>
    Private Sub ExitFirstPerson(Animate As Boolean)
        FpsGen += 1 '使仍在后台采样的回调失效
        Try
            RemoveHandler CompositionTarget.Rendering, AddressOf FpsFrame
        Catch
        End Try
        FpsActive = False
        FpsFlying = False
        FpsLastSpaceTick = 0
        FpsKeys.Clear()
        PanFirstPerson.Cursor = Cursors.Arrow '恢复光标
        If Not Animate Then
            PanFirstPerson.Visibility = Visibility.Collapsed
            Return
        End If
        AniStart({
            AaOpacity(PanFirstPerson, -PanFirstPerson.Opacity, 200, , New AniEaseInFluent),
            AaCode(Sub()
                       PanFirstPerson.Visibility = Visibility.Collapsed
                       PanFirstPerson.Opacity = 1
                       PanFirstPerson.RenderTransform = Nothing
                   End Sub, 210, True)
        }, "SeedMap FPS", True)
    End Sub

    '---- 渲染循环：移动 + Voxel-space 风格的地形绘制 ----
    Private Sub FpsFrame(sender As Object, e As EventArgs)
        If Not FpsActive OrElse FpsHeights Is Nothing Then Return
        Try
            Dim Now As Long = CLng(Environment.TickCount)
            Dim DtMs As Double = Math.Max(1, Now - FpsLastTick)
            If DtMs > 200 Then DtMs = 200
            FpsLastTick = Now
            TickFpsMovement(DtMs / 1000.0)
            If Now - FpsRenderTick < 30 Then Return
            FpsRenderTick = Now
            RenderFps()
        Catch ex As Exception
            Logger.Warn(ex, "第一人称帧更新失败")
            RemoveHandler CompositionTarget.Rendering, AddressOf FpsFrame
            FpsActive = False
        End Try
    End Sub

    ''' <summary>按键 → 位移与跳跃；走出缓存边缘时后台重采样周围地形。</summary>
    Private Sub TickFpsMovement(Dt As Double)
        Dim Fwd As Double = 0, Side As Double = 0
        If FpsKeys.Contains(Key.W) Then Fwd += 1
        If FpsKeys.Contains(Key.S) Then Fwd -= 1
        If FpsKeys.Contains(Key.A) Then Side -= 1
        If FpsKeys.Contains(Key.D) Then Side += 1
        If Fwd <> 0 OrElse Side <> 0 Then
            '飞行时不能用 Shift 加速（Shift 在飞行中是下降），改用更快的飞行速度
            Dim Speed As Double
            If FpsFlying Then
                Speed = 12.0
            Else
                Speed = If(FpsKeys.Contains(Key.LeftShift), 9.0, 4.6) '接近 Minecraft：步行 4.3 格/秒、疾跑 5.6，这里略快一点
            End If
            Dim SinY As Double = Math.Sin(FpsYaw), CosY As Double = Math.Cos(FpsYaw)
            Dim DX As Double = (Fwd * SinY + Side * CosY) * Speed * Dt
            Dim DZ As Double = (-Fwd * CosY + Side * SinY) * Speed * Dt
            FpsX += DX
            FpsZ += DZ
            LabFpsPos.Text = $"X {CInt(FpsX)} · Z {CInt(FpsZ)}"
            '离缓存中心太远就重采样
            Dim Half As Double = FpsN * FpsStep / 2
            Dim Cx As Double = FpsOriginX + Half, Cz As Double = FpsOriginZ + Half
            If Not FpsReloading AndAlso (Math.Abs(FpsX - Cx) > Half * 0.35 OrElse Math.Abs(FpsZ - Cz) > Half * 0.35) Then
                ReloadFpsTerrain()
            End If
        End If
        '重力与跳跃 / 飞行
        Dim Ground As Double = FpsGroundAt(FpsX, FpsZ)
        Dim Floor As Double = Math.Max(Ground, 62) + FpsEyeBlock
        If FpsFlying Then
            '双击空格进入的飞行：空格上升、Shift 下降，不受重力影响
            Dim Up As Double = 0
            If FpsKeys.Contains(Key.Space) Then Up += 1
            If FpsKeys.Contains(Key.LeftShift) Then Up -= 1
            FpsVelY = 0
            FpsOnGround = False
            FpsEyeY += Up * 11.0 * Dt
            If FpsEyeY < Floor Then FpsEyeY = Floor           '别钻到地里
            If FpsEyeY > Floor + 220 Then FpsEyeY = Floor + 220 '别飞太高丢了地形缓存
        Else
            If FpsKeys.Contains(Key.Space) AndAlso FpsOnGround Then
                FpsVelY = 8.0 '跳跃初速：与 24 的重力配合约 1.33 格，接近游戏内 1.25 格
                FpsOnGround = False
            End If
            FpsVelY -= 24.0 * (Dt / 1.0)
            FpsEyeY += FpsVelY * Dt
            If FpsEyeY <= Floor Then
                FpsEyeY = Floor
                FpsVelY = 0
                FpsOnGround = True
            End If
        End If
    End Sub

    ''' <summary>切换飞行状态并更新提示文字。</summary>
    Private Sub SetFpsFlying(Value As Boolean)
        FpsFlying = Value
        FpsVelY = 0
        If LabFpsInfo IsNot Nothing Then
            LabFpsInfo.Text = If(Value,
                "第一人称（飞行）· WASD 移动 · 空格上升 / Shift 下降 · 双击空格取消 · Esc 退出",
                "第一人称 · WASD 移动 · 空格跳跃 · 双击空格飞行 · Shift 加速 · Esc 退出")
        End If
        Hint(If(Value, "已进入飞行模式（空格上升 / Shift 下降，双击空格取消）", "已退出飞行模式"), HintType.Blue)
    End Sub

    ''' <summary>所在格子（FpsStep 方块/格）的整数地表高度——不插值，保持方块台阶感。</summary>
    Private Function FpsGroundAtBlock(X As Double, Z As Double) As Double
        If FpsHeights Is Nothing Then Return 63
        Dim I0 As Integer = ClampI(CInt(Math.Floor((X - FpsOriginX) / FpsStep)), 0, FpsN - 1)
        Dim J0 As Integer = ClampI(CInt(Math.Floor((Z - FpsOriginZ) / FpsStep)), 0, FpsN - 1)
        Return Math.Round(FpsHeights(J0 * FpsN + I0))
    End Function

    ''' <summary>行走时的地面高度：同样按方块格取值，人站在格子顶面上。</summary>
    Private Function FpsGroundAt(X As Double, Z As Double) As Double
        Return FpsGroundAtBlock(X, Z)
    End Function

    ''' <summary>后台重采样以玩家为中心的地形（移动越远范围越大时才触发）。</summary>
    Private Sub ReloadFpsTerrain()
        FpsReloading = True
        Dim Gen As Integer = FpsGen
        Dim Ver As Integer = ModuleSeedMap.VersionIds(VersionIndex)
        Dim Seed = SeedValue
        Dim Px As Double = FpsX, Pz As Double = FpsZ
        Dim Half As Integer = FpsN * FpsStep \ 2
        '同样对齐到 FpsStep 整数倍，避免重采样后晶格错位导致地形"游动"
        Dim OriginX As Integer = CInt(Math.Floor((Px - Half) / FpsStep)) * FpsStep
        Dim OriginZ As Integer = CInt(Math.Floor((Pz - Half) / FpsStep)) * FpsStep
        LabFpsStatus.Text = "加载周围地形…"
        RunInThread(
            Sub()
                Try
                    Dim Hs = ModuleSeedMap.SampleHeights(Ver, Seed, OriginX \ 4, OriginZ \ 4, FpsN, FpsStep \ 4)
                    Dim Ids = ModuleSeedMap.SampleBiomeGrid(Seed, Ver, 0, OriginX, OriginZ, FpsN, FpsStep)
                    RunInUi(
                        Sub()
                            If Gen <> FpsGen Then Return '期间已退出，丢弃
                            FpsHeights = Hs
                            FpsIds = Ids
                            FpsOriginX = OriginX
                            FpsOriginZ = OriginZ
                            PrepareFpsColors()
                            PanFpsLoading.Visibility = Visibility.Collapsed
                            FpsReloading = False
                            FpsLastTick = CLng(Environment.TickCount)
                        End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "第一人称重采样失败")
                    RunInUi(Sub() FpsReloading = False)
                End Try
            End Sub)
    End Sub

    ''' <summary>按世界坐标双线性采样地形颜色（与 FpsGroundAt 同权重）。
    ''' 之前取最近采样格的阶跃色，移动时单元边界会在地形上连续"爬动"。</summary>
    Private Sub SampleFpsColor(Wx As Double, Wz As Double, ByRef R As Integer, ByRef G As Integer, ByRef B As Integer)
        If FpsColR Is Nothing OrElse FpsColR.Length = 0 Then
            R = 127 : G = 127 : B = 127
            Return
        End If
        Dim Fi As Double = (Wx - FpsOriginX) / FpsStep
        Dim Fj As Double = (Wz - FpsOriginZ) / FpsStep
        Dim I0 As Integer = ClampI(CInt(Math.Floor(Fi)), 0, FpsN - 2)
        Dim J0 As Integer = ClampI(CInt(Math.Floor(Fj)), 0, FpsN - 2)
        Dim Tx As Double = Math.Max(0.0, Math.Min(1.0, Fi - I0))
        Dim Tz As Double = Math.Max(0.0, Math.Min(1.0, Fj - J0))
        Dim K00 As Integer = J0 * FpsN + I0
        Dim K10 As Integer = K00 + 1
        Dim K01 As Integer = K00 + FpsN
        Dim K11 As Integer = K01 + 1
        Dim W00 As Double = (1 - Tx) * (1 - Tz), W10 As Double = Tx * (1 - Tz), W01 As Double = (1 - Tx) * Tz, W11 As Double = Tx * Tz
        R = CInt(FpsColR(K00) * W00 + FpsColR(K10) * W10 + FpsColR(K01) * W01 + FpsColR(K11) * W11)
        G = CInt(FpsColG(K00) * W00 + FpsColG(K10) * W10 + FpsColG(K01) * W01 + FpsColG(K11) * W11)
        B = CInt(FpsColB(K00) * W00 + FpsColB(K10) * W10 + FpsColB(K01) * W01 + FpsColB(K11) * W11)
    End Sub

    ''' <summary>Voxel-space 渲染：每屏幕列沿视线步进高度图，从近到远画竖线。
    ''' 注意不能把列顶钳到地平线：比眼睛高的山必须画到地平线以上，否则远处全是平的。</summary>
    ''' <summary>
    ''' 针孔相机渲染（Voxel-space）：每屏幕列用真实透视——水平方向取焦平面正切 t，
    ''' 沿视线前进深度 S 采样地形，屏幕 y = 地平线 − 焦距 × (地形高 − 眼高) / S。
    ''' 焦距按竖直 FOV 70°（Minecraft 默认）推算，眼高 1.62，所以 1 方块在画面上的
    ''' 张角、2 格高的人物比例都与游戏内一致，不再出现"地图比例太高"的观感。
    ''' </summary>
    Private Sub RenderFps()
        If FpsPx Is Nothing OrElse FpsBmp Is Nothing Then
            FpsPx = New Byte(FpsW * FpsH * 4 - 1) {}
            FpsBmp = New WriteableBitmap(FpsW, FpsH, 96, 96, PixelFormats.Bgra32, Nothing)
            ImgFirstPerson.Source = FpsBmp
        End If
        Dim SinY As Double = Math.Sin(FpsYaw), CosY As Double = Math.Cos(FpsYaw)
        Dim SinP As Double = Math.Sin(FpsPitch), CosP As Double = Math.Cos(FpsPitch)
        '竖直 FOV 70°（游戏默认），水平 FOV 由画面宽高比自然推出
        Const VFovDeg As Double = 70.0
        Dim TanVHalf As Double = Math.Tan(VFovDeg * Math.PI / 360.0)
        Dim Focal As Double = (FpsH / 2.0) / TanVHalf
        Dim Cy As Double = FpsH / 2.0
        Dim Horizon As Double = Cy + Focal * Math.Tan(FpsPitch)
        Dim HorizonI As Integer = ClampI(CInt(Math.Round(Horizon)), 0, FpsH)

        '天空（地平线以上，渐变到天际雾色）
        For y As Integer = 0 To Math.Min(HorizonI, FpsH) - 1
            Dim T As Double = Math.Max(0.0, Math.Min(1.0, y / Math.Max(1.0, Horizon - 1)))
            Dim R As Integer = CInt(120 + 62 * T), G As Integer = CInt(160 + 50 * T), B As Integer = CInt(210 + 30 * T)
            Dim O As Integer = y * FpsW * 4
            For x As Integer = 0 To FpsW - 1
                FpsPx(O) = CByte(B) : FpsPx(O + 1) = CByte(G) : FpsPx(O + 2) = CByte(R) : FpsPx(O + 3) = 255
                O += 4
            Next
        Next
        '地平线以下：先整体铺一层灰绿地面雾（整行拷贝，比逐像素快），近处地表随后会盖掉它
        If HorizonI < FpsH Then
            Dim RowBuf(FpsW * 4 - 1) As Byte
            For x As Integer = 0 To FpsW - 1
                RowBuf(x * 4) = 170 : RowBuf(x * 4 + 1) = 142 : RowBuf(x * 4 + 2) = 124 : RowBuf(x * 4 + 3) = 255
            Next
            For y As Integer = Math.Max(0, HorizonI) To FpsH - 1
                Buffer.BlockCopy(RowBuf, 0, FpsPx, y * FpsW * 4, FpsW * 4)
            Next
        End If

        Dim CamH As Double = FpsEyeY
        'yaw = 0 时朝 -Z 看（与 Minecraft 一致）；右侧为 +X
        Dim FwdX As Double = SinY, FwdZ As Double = -CosY
        Dim RgtX As Double = CosY, RgtZ As Double = SinY
        Const MaxDepth As Double = 520.0
        Const WallShade As Double = 0.74      '侧面比顶面暗（纯色块之间的面差）
        Const EdgeShade As Double = 0.62      '方块描边色（压暗 38%）
        Dim Heights = FpsHeights
        For x As Integer = 0 To FpsW - 1
            Dim Tn As Double = (x + 0.5 - FpsW / 2.0) / Focal
            Dim DirX As Double = FwdX + RgtX * Tn
            Dim DirZ As Double = FwdZ + RgtZ * Tn
            '朝镜头方向的相邻格（世界固定）——用它判断"这一块是不是露出侧面"，
            '不依赖采样顺序，所以走动/转身时侧边阴影不会跟着闪
            Dim SgnX As Integer = If(DirX >= 0, 1, -1)
            Dim SgnZ As Integer = If(DirZ >= 0, 1, -1)
            '本列每个深度步所落在的格子，供下一列比较（用来画"竖直描边"）：
            '同一深度步在相邻两列落在不同格子 => 这一像素正好压在方块边界上
            Array.Clear(FpsEdgePrevKey, 0, FpsEdgePrevKey.Length)
            Dim PrevStepKey As Integer = 0
            Dim StepIdx As Integer = 0
            Dim yBuffer As Double = FpsH
            Dim FirstBand As Boolean = True
            Dim S As Double = 1.2
            Do While S < MaxDepth AndAlso yBuffer > 0
                Dim Wx As Double = FpsX + DirX * S
                Dim Wz As Double = FpsZ + DirZ * S
                Dim I0 As Integer = ClampI(CInt(Math.Floor((Wx - FpsOriginX) / FpsStep)), 0, FpsN - 1)
                Dim J0 As Integer = ClampI(CInt(Math.Floor((Wz - FpsOriginZ) / FpsStep)), 0, FpsN - 1)
                Dim K As Integer = J0 * FpsN + I0
                '格子编号（0 表示无效）
                Dim CellKey As Integer = I0 * 512 + J0 + 1
                Dim VertEdge As Boolean = False
                If StepIdx < FpsEdgePrevKey.Length Then
                    Dim PrevCol As Integer = FpsEdgePrevKey(StepIdx)
                    VertEdge = (PrevCol <> 0 AndAlso PrevCol <> CellKey)
                    FpsEdgePrevKey(StepIdx) = CellKey
                End If
                Dim HorizEdge As Boolean = (PrevStepKey <> 0 AndAlso PrevStepKey <> CellKey)
                PrevStepKey = CellKey
                StepIdx += 1

                Dim Hcell As Double = Heights(K)
                Dim H As Double = Math.Round(Hcell)
                If H < 62 AndAlso S > 26 Then H = 62
                Dim Dh As Double = H - CamH
                Dim Den As Double = S * CosP + Dh * SinP
                If Den < 0.35 Then Den = 0.35
                Dim ScreenY As Double = Cy - Focal * (Dh * CosP - S * SinP) / Den
                Dim YTop As Integer = ClampI(CInt(Math.Round(ScreenY)), 0, FpsH)
                If YTop < yBuffer Then
                    Dim TR As Integer = FpsColR(K), TG As Integer = FpsColG(K), TB As Integer = FpsColB(K)
                    '侧面判定：比"更靠近镜头"的邻居高 => 这一段是朝镜头的外墙
                    Dim Hnear As Double = Hcell
                    Dim I1 As Integer = I0 - SgnX, J1 As Integer = J0 - SgnZ
                    If I1 >= 0 AndAlso I1 < FpsN Then
                        Dim V1 As Double = Heights(J0 * FpsN + I1)
                        If V1 > Hnear Then Hnear = V1
                    End If
                    If J1 >= 0 AndAlso J1 < FpsN Then
                        Dim V2 As Double = Heights(J1 * FpsN + I0)
                        If V2 > Hnear Then Hnear = V2
                    End If
                    '纯色方块 + 描边：面上是纯色，只有压在方块边界的那一像素才画深色描边
                    Dim Shade As Double = If(Hcell > Hnear + 0.4, WallShade, 1.0)
                    If VertEdge Then Shade *= EdgeShade
                    Dim Fog As Double = Math.Max(0, Math.Min(1, (S - 80) / (MaxDepth - 80))) * 0.95
                    Dim R As Integer = CInt(TR * Shade * (1 - Fog) + 182 * Fog)
                    Dim G As Integer = CInt(TG * Shade * (1 - Fog) + 211 * Fog)
                    Dim B As Integer = CInt(TB * Shade * (1 - Fog) + 240 * Fog)
                    '描边色（上沿那一行）：直接作为这一带的首行一起填，
                    '不能像以前那样画在 YTop-1 —— 更远的那一带会把上一行盖掉，导致几乎看不到横线
                    Dim DR As Integer = CInt(R * EdgeShade)
                    Dim DG As Integer = CInt(G * EdgeShade)
                    Dim DB As Integer = CInt(B * EdgeShade)
                    '最近的一段铺到屏幕底部：低头时脚下不会露出底色
                    Dim YEnd As Integer = If(FirstBand, FpsH - 1, CInt(yBuffer) - 1)
                    If YTop <= YEnd Then
                        Dim O0 As Integer = (YTop * FpsW + x) * 4
                        If HorizEdge Then
                            FpsPx(O0) = CByte(Math.Min(255, DB)) : FpsPx(O0 + 1) = CByte(Math.Min(255, DG)) : FpsPx(O0 + 2) = CByte(Math.Min(255, DR)) : FpsPx(O0 + 3) = 255
                        Else
                            FpsPx(O0) = CByte(Math.Min(255, B)) : FpsPx(O0 + 1) = CByte(Math.Min(255, G)) : FpsPx(O0 + 2) = CByte(Math.Min(255, R)) : FpsPx(O0 + 3) = 255
                        End If
                        For y As Integer = YTop + 1 To YEnd
                            Dim O As Integer = (y * FpsW + x) * 4
                            FpsPx(O) = CByte(Math.Min(255, B)) : FpsPx(O + 1) = CByte(Math.Min(255, G)) : FpsPx(O + 2) = CByte(Math.Min(255, R)) : FpsPx(O + 3) = 255
                        Next
                    End If
                    yBuffer = YTop
                    FirstBand = False
                End If
                S *= 1.035
            Loop
        Next
        FpsBmp.WritePixels(New Int32Rect(0, 0, FpsW, FpsH), FpsPx, FpsW * 4, 0)
    End Sub

    '---- 输入 ----
    ''' <summary>页面级键盘兜底：第一人称激活时无论焦点在哪（比如还停在种子输入框），
    ''' WASD/空格/Shift/Esc 都由这里接管，不会再漏到别的控件里。</summary>
    Private Sub PageSeedMap_FpsPreviewKeys(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown
        If Not FpsActive AndAlso PanFirstPerson.Visibility <> Visibility.Visible Then Return
        If e.Key = Key.Escape Then
            If PanFirstPerson.Visibility = Visibility.Visible Then
                ExitFirstPerson(True)
                e.Handled = True
            End If
            Return
        End If
        If Not FpsActive OrElse e.IsRepeat Then Return
        '双击空格：切换飞行（与 Minecraft 创造模式的双击空格一致）
        If e.Key = Key.Space Then
            Dim Now As Long = CLng(Environment.TickCount)
            If FpsLastSpaceTick <> 0 AndAlso Now - FpsLastSpaceTick <= 320 Then
                FpsLastSpaceTick = 0
                SetFpsFlying(Not FpsFlying)
                e.Handled = True
                Return
            End If
            FpsLastSpaceTick = Now
        End If
        FpsKeys.Add(e.Key)
        e.Handled = True
    End Sub

    Private Sub PageSeedMap_FpsPreviewKeyUp(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyUp
        If Not FpsActive Then Return
        FpsKeys.Remove(e.Key)
    End Sub

    Private Sub PanFirstPerson_KeyDown(sender As Object, e As KeyEventArgs)
        If Not FpsActive Then Return
        FpsKeys.Add(e.Key)
        e.Handled = True
    End Sub

    Private Sub PanFirstPerson_KeyUp(sender As Object, e As KeyEventArgs)
        FpsKeys.Remove(e.Key)
    End Sub

    ''' <summary>点击视图任意处都把键盘焦点拿回来（WASD 不失效）。</summary>
    Private Sub PanFirstPerson_MouseDown(sender As Object, e As MouseButtonEventArgs)
        If FpsActive Then PanFirstPerson.Focus()
    End Sub

    Private Declare Function SetCursorPos Lib "user32.dll" (X As Integer, Y As Integer) As Boolean

    ''' <summary>指针锁定式鼠标视角：移动即转视角（水平偏航 + 垂直俯仰），
    ''' 每次移动后把光标拉回覆盖层中心，避免移出窗口后视角卡死。</summary>
    Private Sub PanFirstPerson_MouseMove(sender As Object, e As MouseEventArgs)
        If Not FpsActive Then Return
        Try
            Dim P = e.GetPosition(PanFirstPerson)
            Dim Cx As Double = PanFirstPerson.ActualWidth / 2
            Dim Cy As Double = PanFirstPerson.ActualHeight / 2
            Dim Dx As Double = P.X - Cx, Dy As Double = P.Y - Cy
            If Math.Abs(Dx) < 0.5 AndAlso Math.Abs(Dy) < 0.5 Then Return
            '鼠标右移 → 视角右转（偏航增大）；鼠标上移 → 抬头（俯仰增大）
            FpsYaw += Dx * 0.0035
            FpsPitch = ClampD(FpsPitch - Dy * 0.0032, -1.05, 1.05)
            '把光标钉回中心：必须整体用 PointToScreen 换算成屏幕物理像素，
            '不能拿 DIP 中心直接加（高 DPI 屏会偏出中心，形成"回中→又位移"的死循环）
            Dim CenterScreen As Point = PanFirstPerson.PointToScreen(New Point(Cx, Cy))
            SetCursorPos(CInt(CenterScreen.X), CInt(CenterScreen.Y))
        Catch ex As Exception
            Logger.Warn(ex, "第一人称鼠标视角失败")
        End Try
    End Sub

    Private Sub PageSeedMap_UnloadedFps(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        If FpsActive Then ExitFirstPerson(False)
        FpsSelecting = False
    End Sub

End Class
