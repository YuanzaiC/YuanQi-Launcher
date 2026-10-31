Imports System.Windows.Media

''' <summary>
''' 工具箱 · 矿石分布：按维度展示各矿石的生成范围 / 峰值 / 生成规则，
''' 提供 Y 层检查器（滑块 / 精确输入）与逐矿石的相对分布色带图。
''' 数据为 Java 1.18+ 生成规则的简化模型（三角 / 均匀分布叠加）。
''' </summary>
Public Class PageOreGuide

    Public Class OrePlacement
        Public Label As String
        Public Count As String
        Public MinY As Integer
        Public MaxY As Integer
    End Class

    Public Class OreTri
        Public C As Integer
        Public W As Integer
        Public K As Double
    End Class

    Public Class OreZone
        Public MinY As Integer
        Public MaxY As Integer
        Public K As Double
    End Class

    Public Class OreInfo
        Public Name As String
        Public Accent As Color
        Public ImageUrl As String
        Public ImageUrlFallback As String
        Public BestY As Integer
        Public BestText As String
        Public BestLabel As String
        Public RangeText As String
        Public PeakText As String
        Public MaxVein As String
        Public AirDiscard As String
        Public Note As String
        Public Desc As String
        Public Placements As New List(Of OrePlacement)
        Public Tris As New List(Of OreTri)
        Public Zones As New List(Of OreZone)
    End Class

    Public Class OreDimension
        Public Name As String
        Public MinY As Integer
        Public MaxY As Integer
        Public LayersPerBin As Integer
        Public Ores As New List(Of OreInfo)
    End Class

    Private Dims As List(Of OreDimension)
    Private CurDim As OreDimension
    Private CurOre As OreInfo
    Private CurY As Integer = -58
    Private _SyncingSlider As Boolean = False
    Private _Loaded As Boolean = False

    '图表行缓存：矿石 → 色带格 / 徽标 / 峰值权重
    Private ChartBins As New Dictionary(Of String, List(Of Border))
    Private ChartBadges As New Dictionary(Of String, TextBlock)
    Private ChartMaxWeight As New Dictionary(Of String, Double)
    Private ChartPrevIdx As New Dictionary(Of String, Integer)

    Private Sub PageOreGuide_Loaded() Handles Me.Loaded
        If Not _Loaded Then
            _Loaded = True
            LoadData()
        End If
        '内容保留（重复进入不重建），维度状态按当前选择对齐
        SwitchDimension(If(RadioOverworld.IsChecked = True, RadioOverworld, RadioNether))
        '页面挂载处于切页动画的 AniControlEnabled 抑制窗口内：
        '入场动画延迟 30ms 触发（脱离抑制后与框架页面入场同帧），每次进入都播放
        AniStart({AaCode(
            Sub() AnimateContentEnter({PanDimTabs, PanOreChips, PanTargetCard, PanInspectorCard, PanChartCard}, 120, 55),
            30, True)}, "OreGuide Enter Trigger")
    End Sub

    ' ==================== 数据 ====================

    Private Sub LoadData()
        Dims = New List(Of OreDimension)

        Dim OW As New OreDimension With {.Name = "主世界", .MinY = -64, .MaxY = 319, .LayersPerBin = 4}
        OW.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-deepslate-diamond-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-diamond-ore.png",
            .Name = "钻石", .Accent = Color.FromRgb(72, 199, 190),
            .BestY = -58, .BestText = "Y -58", .BestLabel = "所有主世界生物群系",
            .RangeText = "Y -64~16", .PeakText = "Y -64",
            .MaxVein = "12 方块（大型稀有矿脉）", .AirDiscard = "50% / 70% / 100%",
            .Note = "深板岩鱼骨矿道效率最高",
            .Desc = "四组生成规则叠加，生成位置权重向世界底部持续升高。Y -64 是规则峰值；实际开采通常上移到 Y -58，减少底部基岩干扰。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "小型矿脉", .Count = "7 次/区块", .MinY = -64, .MaxY = 16},
                New OrePlacement With {.Label = "完全埋藏矿脉", .Count = "4 次/区块", .MinY = -64, .MaxY = 16},
                New OrePlacement With {.Label = "大型稀有矿脉", .Count = "平均 1/9 次/区块", .MinY = -64, .MaxY = 16},
                New OrePlacement With {.Label = "中型矿脉", .Count = "2 次/区块", .MinY = -64, .MaxY = 16}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = -64, .W = 80, .K = 1}}})
        OW.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-deepslate-iron-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-iron-ore.png",
            .Name = "铁矿", .Accent = Color.FromRgb(186, 173, 152),
            .BestY = 16, .BestText = "Y 16 / 232", .BestLabel = "所有主世界生物群系",
            .RangeText = "Y -64~256", .PeakText = "Y 16 · 232",
            .MaxVein = "9 方块", .AirDiscard = "—",
            .Note = "Y 232 峰值仅在高山的三角分布中生效",
            .Desc = "三组生成规则叠加：Y 16 与 Y 232（高山）各有一个三角峰值，低海拔另有均匀分布。选矿目标建议 Y 16；高山地形可挖 Y 232。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "三角分布 · 峰值 Y 16", .Count = "1 次/区块", .MinY = -24, .MaxY = 256},
                New OrePlacement With {.Label = "三角分布 · 峰值 Y 232（高山）", .Count = "1 次/区块", .MinY = 80, .MaxY = 256},
                New OrePlacement With {.Label = "均匀分布", .Count = "3 次/区块", .MinY = -64, .MaxY = 72}},
            .Tris = New List(Of OreTri) From {
                New OreTri With {.C = 16, .W = 96, .K = 1},
                New OreTri With {.C = 232, .W = 96, .K = 1}},
            .Zones = New List(Of OreZone) From {New OreZone With {.MinY = -64, .MaxY = 72, .K = 0.45}}})
        OW.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-deepslate-copper-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-copper-ore.png",
            .Name = "铜矿", .Accent = Color.FromRgb(224, 118, 60),
            .BestY = 48, .BestText = "Y 48", .BestLabel = "所有主世界生物群系",
            .RangeText = "Y -16~112", .PeakText = "Y 48",
            .MaxVein = "20 方块（溶洞大型矿脉）", .AirDiscard = "—",
            .Note = "溶洞生物群系内会出现大型铜矿脉",
            .Desc = "生成于 Y -16~112，峰值在 Y 48 附近；溶洞生物群系中额外生成大型铜矿脉，矿脉规模显著更大。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "三角分布 · 峰值 Y 48", .Count = "16 次/区块", .MinY = -16, .MaxY = 112},
                New OrePlacement With {.Label = "溶洞大型矿脉", .Count = "16 次/区块", .MinY = 0, .MaxY = 112}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = 48, .W = 64, .K = 1}},
            .Zones = New List(Of OreZone) From {New OreZone With {.MinY = 0, .MaxY = 112, .K = 0.4}}})
        OW.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-deepslate-gold-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-gold-ore.png",
            .Name = "金矿", .Accent = Color.FromRgb(232, 195, 60),
            .BestY = -16, .BestText = "Y -16", .BestLabel = "所有主世界生物群系",
            .RangeText = "Y -64~32", .PeakText = "Y -16",
            .MaxVein = "9 方块", .AirDiscard = "—",
            .Note = "恶地生物群系在 Y 32~256 额外大量生成",
            .Desc = "主世界生成于 Y -64~32，峰值在 Y -16；恶地生物群系额外在 Y 32~256 高频生成金矿。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "三角分布 · 峰值 Y -16", .Count = "4 次/区块", .MinY = -64, .MaxY = 32},
                New OrePlacement With {.Label = "恶地额外矿脉", .Count = "20 次/区块", .MinY = 32, .MaxY = 256}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = -16, .W = 48, .K = 1}},
            .Zones = New List(Of OreZone) From {New OreZone With {.MinY = 32, .MaxY = 256, .K = 0.55}}})
        OW.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-deepslate-redstone-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-redstone-ore.png",
            .Name = "红石", .Accent = Color.FromRgb(224, 75, 75),
            .BestY = -58, .BestText = "Y -58", .BestLabel = "所有主世界生物群系",
            .RangeText = "Y -64~15", .PeakText = "Y -64",
            .MaxVein = "8 方块", .AirDiscard = "—",
            .Note = "与钻石同层位，挖钻时顺路收集",
            .Desc = "生成于 Y -64~15，权重随深度增加，Y -64 附近为峰值。与钻石层位高度重合，可同层开采。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "三角分布 · 峰值 Y -64", .Count = "4 次/区块", .MinY = -64, .MaxY = 15},
                New OrePlacement With {.Label = "均匀分布", .Count = "4 次/区块", .MinY = -64, .MaxY = 15}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = -64, .W = 44, .K = 1}},
            .Zones = New List(Of OreZone) From {New OreZone With {.MinY = -64, .MaxY = 15, .K = 0.4}}})
        OW.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-deepslate-lapis-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-lapis-ore.png",
            .Name = "青金石", .Accent = Color.FromRgb(96, 122, 216),
            .BestY = 0, .BestText = "Y 0", .BestLabel = "所有主世界生物群系",
            .RangeText = "Y -64~64", .PeakText = "Y 0",
            .MaxVein = "7 方块", .AirDiscard = "—",
            .Note = "",
            .Desc = "生成于 Y -64~64，以 Y 0 为峰值的三角分布为主，另有埋藏形态的三角分布叠加。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "三角分布 · 峰值 Y 0", .Count = "2 次/区块", .MinY = -64, .MaxY = 64},
                New OrePlacement With {.Label = "埋藏三角分布 · 峰值 Y 0", .Count = "4 次/区块", .MinY = -64, .MaxY = 64}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = 0, .W = 56, .K = 1}}})
        OW.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-deepslate-coal-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-coal-ore.png",
            .Name = "煤炭", .Accent = Color.FromRgb(122, 122, 126),
            .BestY = 96, .BestText = "Y 96 / 136", .BestLabel = "所有主世界生物群系",
            .RangeText = "Y 0~320", .PeakText = "Y 96 · 136",
            .MaxVein = "16 方块", .AirDiscard = "—",
            .Note = "露天山体表面也常直接暴露",
            .Desc = "生成于 Y 0~320，在 Y 96 附近与 Y 136（高山）有两个三角峰值，是分布最广的矿石。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "三角分布 · 峰值 Y 96", .Count = "20 次/区块", .MinY = 0, .MaxY = 192},
                New OrePlacement With {.Label = "三角分布 · 峰值 Y 136", .Count = "20 次/区块", .MinY = 80, .MaxY = 320}},
            .Tris = New List(Of OreTri) From {
                New OreTri With {.C = 96, .W = 96, .K = 1},
                New OreTri With {.C = 136, .W = 92, .K = 0.9}}})
        OW.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-deepslate-emerald-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-emerald-ore.png",
            .Name = "绿宝石", .Accent = Color.FromRgb(61, 200, 90),
            .BestY = 232, .BestText = "Y 232", .BestLabel = "仅山地类生物群系",
            .RangeText = "Y -16~320", .PeakText = "Y 232",
            .MaxVein = "3 方块", .AirDiscard = "—",
            .Note = "仅生成于山地类生物群系，露天矿表常见",
            .Desc = "只生成于山地类生物群系，权重随高度上升，Y 232 附近为峰值；高山露天表面常直接可见。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "三角分布 · 峰值 Y 232（仅山地）", .Count = "6 次/区块", .MinY = -16, .MaxY = 320}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = 232, .W = 130, .K = 1}}})

        Dim NE As New OreDimension With {.Name = "下界", .MinY = 0, .MaxY = 127, .LayersPerBin = 2}
        NE.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-nether-quartz-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-nether-quartz-ore.png",
            .Name = "下界石英", .Accent = Color.FromRgb(226, 220, 210),
            .BestY = 63, .BestText = "Y 10~117", .BestLabel = "下界所有生物群系",
            .RangeText = "Y 10~117", .PeakText = "均匀分布",
            .MaxVein = "—", .AirDiscard = "—",
            .Note = "暴露于下界岩浆海沿岸的露天矿最易采集",
            .Desc = "下界最常见矿石，Y 10~117 均匀分布，任意高度开采效率接近。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "均匀分布", .Count = "16 次/区块", .MinY = 10, .MaxY = 117}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = 63, .W = 60, .K = 0.35}},
            .Zones = New List(Of OreZone) From {New OreZone With {.MinY = 10, .MaxY = 117, .K = 0.9}}})
        NE.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-nether-gold-ore.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-nether-gold-ore.png",
            .Name = "下界金矿", .Accent = Color.FromRgb(232, 195, 60),
            .BestY = 15, .BestText = "Y 15", .BestLabel = "下界所有生物群系",
            .RangeText = "Y 10~117", .PeakText = "Y 15",
            .MaxVein = "9 方块", .AirDiscard = "—",
            .Note = "玄武岩三角洲附近分布更密集",
            .Desc = "生成于 Y 10~117，越低越常见，Y 15 附近为峰值。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "低地偏好分布", .Count = "10 次/区块", .MinY = 10, .MaxY = 117}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = 15, .W = 60, .K = 1}},
            .Zones = New List(Of OreZone) From {New OreZone With {.MinY = 10, .MaxY = 117, .K = 0.2}}})
        NE.Ores.Add(New OreInfo With {
            .ImageUrl = "pack://application:,,,/images/Ores/ore-ancient-debris.png", .ImageUrlFallback = "pack://application:,,,/images/Ores/ore-ancient-debris.png",
            .Name = "远古残骸", .Accent = Color.FromRgb(150, 108, 82),
            .BestY = 15, .BestText = "Y 15", .BestLabel = "下界所有生物群系",
            .RangeText = "Y 8~119", .PeakText = "Y 8~22",
            .MaxVein = "1~2 方块", .AirDiscard = "—",
            .Note = "床爆 / 鱼骨挖掘法效率最高",
            .Desc = "下界最稀有矿石：主矿脉 1~2 方块集中于 Y 8~22（峰值 Y 15），另有极低概率散布于 Y 8~119。",
            .Placements = New List(Of OrePlacement) From {
                New OrePlacement With {.Label = "主矿脉 · 1~2 方块", .Count = "约 1 次/区块", .MinY = 8, .MaxY = 22},
                New OrePlacement With {.Label = "低概率散布", .Count = "约 0.4 次/区块", .MinY = 8, .MaxY = 119}},
            .Tris = New List(Of OreTri) From {New OreTri With {.C = 15, .W = 8, .K = 1}},
            .Zones = New List(Of OreZone) From {New OreZone With {.MinY = 8, .MaxY = 119, .K = 0.06}}})

        Dims.Add(OW)
        Dims.Add(NE)
    End Sub

    ' ==================== 交互入口 ====================

    Private Sub DimChanged(sender As Object, e As RoutedEventArgs) Handles RadioOverworld.Checked, RadioNether.Checked
        If Dims Is Nothing OrElse PanOreChips Is Nothing Then Return
        If sender Is RadioOverworld Then
            SwitchDimension(RadioOverworld)
        Else
            SwitchDimension(RadioNether)
        End If
    End Sub

    ''' <summary>容器依次淡入并上移归位（页面进入 / 维度切换 / 矿石切换共用）。</summary>
    Private Sub AnimateContentEnter(Items As IEnumerable(Of UIElement), BaseDelay As Integer, Stagger As Integer)
        Try
            If AniControlEnabled <> 0 Then Return
            Dim Anims As New List(Of AniData)
            Dim i As Integer = 0
            For Each Elem As UIElement In Items
                Elem.Opacity = 0
                Elem.RenderTransform = New TranslateTransform(0, 14)
                Anims.Add(AaOpacity(Elem, 1, 240, BaseDelay + i * Stagger, New AniEaseOutFluent(AniEasePower.Weak)))
                Anims.Add(AaTranslateY(Elem, -14, 300, BaseDelay + i * Stagger, New AniEaseOutFluent(AniEasePower.Weak)))
                i += 1
            Next
            AniStart(Anims.ToArray(), "OreGuide Enter")
        Catch ex As Exception
            Logger.Warn(ex, "矿石分布页入场动画失败")
        End Try
    End Sub

    Private Sub SwitchDimension(Radio As RadioButton)
        If _SyncingSlider Then Return
        Dim Target = If(Radio Is RadioOverworld, Dims(0), Dims(1))
        If CurDim Is Target AndAlso PanChartRows.Children.Count > 0 Then Return
        CurDim = Target
        CurOre = Target.Ores(0)
        CurY = Math.Max(CurDim.MinY, Math.Min(CurDim.MaxY, CurOre.BestY))
        RebuildOreChips()
        UpdateTarget()
        RebuildChart()
        AnimateContentEnter({PanOreChips, PanTargetCard, PanInspectorCard, PanChartCard}, 0, 45)
        _SyncingSlider = True
        SliderY.MaxValue = CurDim.MaxY - CurDim.MinY
        SliderY.Value = CurY - CurDim.MinY
        _SyncingSlider = False
        LabSliderMin.Text = CurDim.MinY
        LabSliderMax.Text = CurDim.MaxY
        TextYInput.Text = CurY
        UpdateInspector()
    End Sub

    Private Sub SliderY_Change(sender As Object, user As Boolean) Handles SliderY.Change
        If _SyncingSlider OrElse CurDim Is Nothing Then Return
        SetY(SliderY.Value + CurDim.MinY)
    End Sub

    Private Sub TextYInput_KeyDown(sender As Object, e As KeyEventArgs) Handles TextYInput.KeyDown
        If e.Key <> Key.Enter Then Return
        e.Handled = True
        Dim Y As Integer
        If Integer.TryParse(If(TextYInput.Text, "").Trim, Y) Then
            SetY(Y)
        Else
            Hint("请输入有效的整数 Y 层", HintType.Red)
        End If
    End Sub

    Private Sub BtnBackToBest_Click(sender As Object, e As RoutedEventArgs) Handles BtnBackToBest.Click
        If CurOre IsNot Nothing Then SetY(CurOre.BestY)
    End Sub

    Private Sub BtnCopy_Click(sender As Object, e As RoutedEventArgs) Handles BtnCopy.Click
        Try
            If CurOre Is Nothing Then Return
            Dim Pct = RelativeAt(CurOre, CurY)
            Dim PctText = If(Pct < 0.03, "无分布", CInt(Pct * 100) & "%")
            Dim Sb As New Text.StringBuilder()
            Sb.Append($"【{CurOre.Name}】实用开采层 {CurOre.BestText}（生成范围 {CurOre.RangeText}，规则峰值 {CurOre.PeakText}）。")
            Sb.Append($"当前 Y {CurY}，相对自身峰值 {PctText}。生成规则：")
            Dim First = True
            For Each P In CurOre.Placements
                If CurY >= P.MinY AndAlso CurY <= P.MaxY Then
                    Sb.Append(If(First, "", "；")).Append($"{P.Label} {P.Count}")
                    First = False
                End If
            Next
            Sb.Append("。")
            If CurOre.Note <> "" Then Sb.Append(CurOre.Note & "。")
            ClipboardSet(Sb.ToString())
        Catch ex As Exception
            Logger.Warn(ex, "复制矿石结论失败")
        End Try
    End Sub

    ' ==================== Y 层平滑滑动 ====================
    Private YSlideTarget As Double
    Private YSlideCurrent As Double
    Private YSlideHooked As Boolean = False

    Private Sub HookYSlide(Active As Boolean)
        If Active AndAlso Not YSlideHooked Then
            YSlideHooked = True
            AddHandler CompositionTarget.Rendering, AddressOf YSlideFrame
        ElseIf Not Active AndAlso YSlideHooked Then
            YSlideHooked = False
            RemoveHandler CompositionTarget.Rendering, AddressOf YSlideFrame
        End If
    End Sub

    ''' <summary>逐帧插值：当前 Y 以 30% 步长逼近目标（色带指示器随之逐格滑动）。</summary>
    Private Sub YSlideFrame(sender As Object, e As EventArgs)
        Try
            YSlideCurrent += (YSlideTarget - YSlideCurrent) * 0.3
            If Math.Abs(YSlideTarget - YSlideCurrent) < 0.51 Then YSlideCurrent = YSlideTarget
            Dim NewY = CInt(Math.Round(YSlideCurrent))
            If NewY <> CurY Then
                CurY = NewY
                ApplyY()
            End If
            If Math.Abs(YSlideTarget - YSlideCurrent) < 0.51 Then HookYSlide(False)
        Catch
        End Try
    End Sub

    ''' <summary>轻量刷新：检查器数值与色带指示（每帧调用；规则列表由签名防抖）。</summary>
    Private Sub ApplyY()
        Try
            LabInspectorY.Text = CurY
            Dim Pct = RelativeAt(CurOre, CurY)
            LabInspectorPercent.Text = If(Pct < 0.005, "此层无生成", $"相对自身峰值 {CInt(Pct * 100)}%")
            DeltaChartHighlight()
            _SyncingSlider = True
            SliderY.Value = CurY - CurDim.MinY
            _SyncingSlider = False
            If TextYInput.Text <> CurY Then TextYInput.Text = CurY
            RebuildInspectorRules()
        Catch
        End Try
    End Sub

    Private Sub SetY(Y As Integer)
        If CurDim Is Nothing Then Return
        Dim Target = Math.Max(CurDim.MinY, Math.Min(CurDim.MaxY, Y))
        If Target = CurY AndAlso Target = YSlideTarget Then
            TextYInput.Text = CurY
            Return
        End If
        YSlideTarget = Target
        If Not YSlideHooked Then
            YSlideCurrent = CurY
            HookYSlide(True)
        End If
        ApplyY()
    End Sub

    ' <summary>增量刷新：仅切换新旧两个色带格的高亮与徽标（全量遍历在拖动滑杆时会卡顿）。</summary>
    Private Sub DeltaChartHighlight()
        For Each Ore In CurDim.Ores
            If Not ChartBins.ContainsKey(Ore.Name) Then Continue For
            Dim Borders = ChartBins(Ore.Name)
            Dim NewIdx = Math.Max(0, Math.Min(Borders.Count - 1, (CurY - CurDim.MinY) \ CurDim.LayersPerBin))
            Dim OldIdx As Integer
            If Not ChartPrevIdx.TryGetValue(Ore.Name, OldIdx) Then OldIdx = -1
            If OldIdx <> NewIdx Then
                If OldIdx >= 0 AndAlso OldIdx < Borders.Count Then Borders(OldIdx).BorderThickness = New Thickness(0)
                Borders(NewIdx).BorderThickness = New Thickness(2)
                Borders(NewIdx).BorderBrush = New SolidColorBrush(Color.FromRgb(25, 25, 25))
                Borders(NewIdx).CornerRadius = New CornerRadius(2)
                ChartPrevIdx(Ore.Name) = NewIdx
            End If
            If ChartBadges.ContainsKey(Ore.Name) Then
                Dim Pct = RelativeAt(Ore, CurY)
                ChartBadges(Ore.Name).Text = If(Pct < 0.03, "无分布", CInt(Pct * 100) & "%")
            End If
        Next
        LabChartCurrent.Text = "当前 Y：" & CurY
    End Sub

    ' ==================== 分布图 ====================

    Private ChipList As New List(Of KeyValuePair(Of OreInfo, Border))

    Private Sub RebuildOreChips()
        PanOreChips.Children.Clear()
        ChipList.Clear()
        For Each Ore In CurDim.Ores
            Dim Chip As New Border With {
                .CornerRadius = New CornerRadius(8), .Padding = New Thickness(9, 7, 12, 7),
                .Margin = New Thickness(0, 0, 8, 8), .Cursor = Cursors.Hand,
                .Background = New SolidColorBrush(Colors.White), .BorderBrush = FindResource("BrushBorder"),
                .BorderThickness = New Thickness(1), .Child = BuildOreChipContent(Ore)}
            AddHandler Chip.MouseLeftButtonUp,
                Sub(sender, e) SelectOre(Ore)
            PanOreChips.Children.Add(Chip)
            ChipList.Add(New KeyValuePair(Of OreInfo, Border)(Ore, Chip))
        Next
        RestyleChips()
    End Sub

    Private Function BuildOreChipContent(Ore As OreInfo) As UIElement
        Dim P As New StackPanel With {.Orientation = Orientation.Horizontal}
        P.Children.Add(BuildOreImageIcon(Ore, 26))
        Dim T As New StackPanel With {.Margin = New Thickness(9, 0, 0, 0), .VerticalAlignment = VerticalAlignment.Center}
        T.Children.Add(New TextBlock With {.Text = Ore.Name, .FontSize = 12.5, .FontWeight = FontWeights.SemiBold, .Foreground = FindResource("BrushText1")})
        T.Children.Add(New TextBlock With {.Text = Ore.BestText, .FontSize = 10, .Foreground = FindResource("BrushText3"), .Margin = New Thickness(0, 1, 0, 0)})
        P.Children.Add(T)
        Return P
    End Function

    Private Sub RestyleChips()
        For Each Pair In ChipList
            Dim Sel = Pair.Key Is CurOre
            Pair.Value.Background = If(Sel, TryFindResource("BrushAccentSoft"), TryFindResource("BrushSurface"))
            Pair.Value.BorderBrush = If(Sel, TryFindResource("ColorBrush3"), TryFindResource("BrushBorder"))
            Pair.Value.BorderThickness = New Thickness(If(Sel, 1.5, 1))
        Next
    End Sub

    Private Sub SelectOre(Ore As OreInfo)
        If CurOre Is Ore Then Return
        '上侧两卡向左滑出 → 换新矿石内容 → 从右侧滑入（两卡同步移动、等高）
        Try
            If AniControlEnabled <> 0 Then
                CurOre = Ore
                RestyleChips()
                UpdateTarget()
                UpdateInspector()
                HighlightChart()
                SetY(Ore.BestY)
                Return
            End If
            AniStart({
                AaTranslateX(PanTargetCard, -30, 150, , New AniEaseInFluent(AniEasePower.Weak)),
                AaOpacity(PanTargetCard, -PanTargetCard.Opacity, 150, , New AniEaseInFluent(AniEasePower.Weak)),
                AaTranslateX(PanInspectorCard, -30, 150, , New AniEaseInFluent(AniEasePower.Weak)),
                AaOpacity(PanInspectorCard, -PanInspectorCard.Opacity, 150, , New AniEaseInFluent(AniEasePower.Weak)),
                AaCode(Sub()
                           CurOre = Ore
                           RestyleChips()
                           UpdateTarget()
                           UpdateInspector()
                           HighlightChart()
                           SetY(Ore.BestY)
                           '从另一侧进入
                           For Each C As FrameworkElement In {PanTargetCard, PanInspectorCard}
                               CType(C.RenderTransform, TranslateTransform).X = 30
                               C.Opacity = 0
                           Next
                           AniStart({
                               AaTranslateX(PanTargetCard, -28, 200, , New AniEaseOutFluent(AniEasePower.Weak)),
                               AaOpacity(PanTargetCard, 1 - PanTargetCard.Opacity, 200, , New AniEaseOutFluent(AniEasePower.Weak)),
                               AaTranslateX(PanInspectorCard, -28, 200, , New AniEaseOutFluent(AniEasePower.Weak)),
                               AaOpacity(PanInspectorCard, 1 - PanInspectorCard.Opacity, 200, , New AniEaseOutFluent(AniEasePower.Weak))
                           }, "OreGuide Card Slide In")
                       End Sub, 160, True)
            }, "OreGuide Card Slide Out")
        Catch ex As Exception
            Logger.Warn(ex, "切换矿石动画失败")
            CurOre = Ore
            RestyleChips()
            UpdateTarget()
            UpdateInspector()
            HighlightChart()
            SetY(Ore.BestY)
        End Try
    End Sub

    ' ==================== 当前目标卡 ====================

    Private Sub UpdateTarget()
        If CurOre Is Nothing OrElse LabTargetOre Is Nothing Then Return
        PanTargetIcon.Child = BuildOreImageIcon(CurOre, 38)
        LabTargetKicker.Text = "当前目标 · " & CurOre.BestLabel
        LabTargetOre.Text = CurOre.Name
        LabTargetBestY.Text = "Y " & CurOre.BestY
        LabTargetDesc.Text = CurOre.Desc
        LabStatRange.Text = CurOre.RangeText
        LabStatPeak.Text = CurOre.PeakText
        LabStatVein.Text = CurOre.MaxVein
        LabStatAir.Text = CurOre.AirDiscard
        LabTargetNote.Text = CurOre.Note
    End Sub

    ' ==================== Y 层检查器 ====================

    Private Sub UpdateInspector()
        If CurOre Is Nothing OrElse CurDim Is Nothing OrElse LabInspectorY Is Nothing Then Return
        LabInspectorY.Text = CurY
        Dim Pct = RelativeAt(CurOre, CurY)
        LabInspectorPercent.Text = If(Pct < 0.005, "此层无生成", $"相对自身峰值 {CInt(Pct * 100)}%")
        RebuildInspectorRules()
    End Sub

    Private _RulesSig As String = ""

    Private Sub RebuildInspectorRules()
        Dim Sig = String.Join("|", CurOre.Placements.Select(Function(P) If(CurY >= P.MinY AndAlso CurY <= P.MaxY, "1", "0")))
        If Sig = _RulesSig AndAlso PanInspectorRules.Children.Count > 0 Then Return
        _RulesSig = Sig
        PanInspectorRules.Children.Clear()
        For Each P In CurOre.Placements
            Dim Active = CurY >= P.MinY AndAlso CurY <= P.MaxY
            Dim Row As New Border With {
                .Background = TryFindResource("BrushSurface2"), .CornerRadius = New CornerRadius(6),
                .Padding = New Thickness(10, 6, 10, 6), .Margin = New Thickness(0, 0, 0, 6)}
            Dim G As New Grid
            G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            Dim Check As New TextBlock With {
                .Text = If(Active, "✓", "○"), .FontSize = 12, .FontWeight = FontWeights.Bold,
                .VerticalAlignment = VerticalAlignment.Center}
            Check.SetResourceReference(TextBlock.ForegroundProperty, If(Active, "ColorBrush3", "BrushText3"))
            G.Children.Add(Check)
            Dim Lab As New TextBlock With {
                .Text = P.Label, .FontSize = 12, .Foreground = FindResource("BrushText1"),
                .Margin = New Thickness(8, 0, 0, 0), .VerticalAlignment = VerticalAlignment.Center}
            If Not Active Then Lab.Opacity = 0.45
            Grid.SetColumn(Lab, 1)
            G.Children.Add(Lab)
            Dim Count As New TextBlock With {
                .Text = P.Count, .FontSize = 10.5, .Foreground = FindResource("BrushText3"),
                .VerticalAlignment = VerticalAlignment.Center}
            If Not Active Then Count.Opacity = 0.45
            Grid.SetColumn(Count, 2)
            G.Children.Add(Count)
            Row.Child = G
            PanInspectorRules.Children.Add(Row)
        Next
    End Sub

    ' ==================== 分布图 ====================

    Private Sub RebuildChart()
        If CurDim Is Nothing OrElse PanChartRows Is Nothing Then Return
        PanChartAxis.Children.Clear()
        PanChartRows.Children.Clear()
        ChartBins.Clear()
        ChartBadges.Clear()
        ChartMaxWeight.Clear()

        Dim BinCount = (CurDim.MaxY - CurDim.MinY + 1) \ CurDim.LayersPerBin

        '轴刻度线：每 4 格一个短竖线（视觉密度适中）
        For i As Integer = 0 To BinCount - 1
            If i Mod 4 = 0 Then
                PanChartAxis.Children.Add(New Border With {
                    .Width = 1, .Height = 5, .VerticalAlignment = VerticalAlignment.Bottom,
                    .HorizontalAlignment = HorizontalAlignment.Center, .Background = TryFindResource("BrushText3")})
            Else
                PanChartAxis.Children.Add(New Border With {.Height = 20})
            End If
        Next
        '轴刻度值：每 8 格一个标签，独占 8 格宽的标签槽（左对齐到刻度线，数字完整显示）
        PanAxisLabels.Children.Clear()
        Dim LabelCount = Math.Max(1, BinCount \ 8)
        PanAxisLabels.Columns = LabelCount
        For j As Integer = 0 To LabelCount - 1
            Dim Val = CurDim.MinY + j * 8 * CurDim.LayersPerBin
            Dim Cell As New Border With {.Height = 20}
            Cell.Child = New TextBlock With {
                .Text = Val, .FontSize = 10.5, .Foreground = FindResource("BrushText3"),
                .HorizontalAlignment = HorizontalAlignment.Left, .VerticalAlignment = VerticalAlignment.Top,
                .Margin = New Thickness(3, 0, 0, 0), .TextTrimming = TextTrimming.None}
            PanAxisLabels.Children.Add(Cell)
        Next

        For Each Ore In CurDim.Ores
            Dim MaxW = 0.00001
            Dim Weights(BinCount - 1) As Double
            For i As Integer = 0 To BinCount - 1
                Dim W = OreWeightAt(Ore, CurDim.MinY + i * CurDim.LayersPerBin + CurDim.LayersPerBin \ 2)
                Weights(i) = W
                If W > MaxW Then MaxW = W
            Next
            ChartMaxWeight(Ore.Name) = MaxW

            Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 7)}
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(172)})
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(76)})

            Dim Head As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
            Head.Children.Add(BuildOreImageIcon(Ore, 26))
            Head.Children.Add(New TextBlock With {
                .Text = Ore.Name, .FontSize = 12.5, .FontWeight = FontWeights.SemiBold,
                .Foreground = FindResource("BrushText1"), .Margin = New Thickness(8, 0, 0, 0),
                .VerticalAlignment = VerticalAlignment.Center})
            Grid.SetColumn(Head, 0)
            Row.Children.Add(Head)

            Dim Bars As New System.Windows.Controls.Primitives.UniformGrid With {.Rows = 1, .Columns = BinCount}
            Dim Borders As New List(Of Border)
            For i As Integer = 0 To BinCount - 1
                Dim Lvl = If(MaxW > 0.0001, Weights(i) / MaxW, 0)
                Dim Alpha = If(Lvl >= 0.75, 235, If(Lvl >= 0.35, 150, If(Lvl >= 0.08, 75, 0)))
                Dim Cell As New Border With {
                    .Margin = New Thickness(0, 2, 0, 2),
                    .Background = If(Alpha > 0, New SolidColorBrush(Color.FromArgb(CByte(Alpha), Ore.Accent.R, Ore.Accent.G, Ore.Accent.B)), TryFindResource("BrushSurface2")),
                    .Cursor = Cursors.Hand, .Tag = CurDim.MinY + i * CurDim.LayersPerBin}
                AddHandler Cell.MouseLeftButtonUp, Sub(sender, e) SetY(CType(CType(sender, Border).Tag, Integer) + CurDim.LayersPerBin \ 2)
                Grid.SetColumn(Cell, i)
                Bars.Children.Add(Cell)
                Borders.Add(Cell)
            Next
            Grid.SetColumn(Bars, 1)
            Row.Children.Add(Bars)

            Dim Badge As New Border With {
                .Background = TryFindResource("BrushSurface2"), .CornerRadius = New CornerRadius(10),
                .Padding = New Thickness(8, 3, 8, 3),
                .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}
            Dim BadgeText As New TextBlock With {
                .FontSize = 10.5, .FontWeight = FontWeights.SemiBold,
                .Foreground = FindResource("BrushText2"), .HorizontalAlignment = HorizontalAlignment.Center}
            Badge.Child = BadgeText
            Grid.SetColumn(Badge, 2)
            Row.Children.Add(Badge)

            PanChartRows.Children.Add(Row)
            ChartBins(Ore.Name) = Borders
            ChartBadges(Ore.Name) = BadgeText
        Next

        LabChartTitle.Text = CurDim.Name & "矿石完整分布"
        LabChartNote.Text = $"每格代表{CurDim.Name} {CurDim.LayersPerBin} 层，颜色取该区间内的相对高度；点击色带可把检查器移动到对应区间。下界矿石色带同理（每格 2 层）。"
        HighlightChart()
    End Sub

    ''' <summary>高亮包含当前 Y 的色带格，并刷新各行徽标百分比。</summary>
    Private Sub HighlightChart()
        If CurDim Is Nothing Then Return
        For Each Ore In CurDim.Ores
            If Not ChartBins.ContainsKey(Ore.Name) Then Continue For
            Dim Borders = ChartBins(Ore.Name)
            For Each B In Borders
                B.BorderThickness = New Thickness(0)
            Next
            Dim Idx = Math.Max(0, Math.Min(Borders.Count - 1, (CurY - CurDim.MinY) \ CurDim.LayersPerBin))
            ChartPrevIdx(Ore.Name) = Idx
            If Idx >= 0 AndAlso Idx < Borders.Count Then
                Borders(Idx).BorderThickness = New Thickness(2)
                Borders(Idx).BorderBrush = New SolidColorBrush(Color.FromRgb(25, 25, 25))
                Borders(Idx).CornerRadius = New CornerRadius(2)
            End If
            If ChartBadges.ContainsKey(Ore.Name) Then
                Dim Pct = RelativeAt(Ore, CurY)
                ChartBadges(Ore.Name).Text = If(Pct < 0.03, "无分布", CInt(Pct * 100) & "%")
            End If
        Next
        LabChartCurrent.Text = "当前 Y：" & CurY
    End Sub

    ' ==================== 分布模型 ====================

    ''' <summary>某矿石在 Y 层的相对权重（0~1，以该矿石峰值归一化）。</summary>
    Private Function OreWeightAt(Ore As OreInfo, Y As Integer) As Double
        Dim W As Double = 0
        For Each T In Ore.Tris
            If Math.Abs(Y - T.C) <= T.W Then W += T.K * (1 - Math.Abs(Y - T.C) / T.W)
        Next
        For Each Z In Ore.Zones
            If Y >= Z.MinY AndAlso Y <= Z.MaxY Then W += Z.K
        Next
        Return W
    End Function

    Private Function RelativeAt(Ore As OreInfo, Y As Integer) As Double
        If Not ChartMaxWeight.ContainsKey(Ore.Name) Then Return 0
        Dim Max = ChartMaxWeight(Ore.Name)
        If Max < 0.0001 Then Return 0
        Return Math.Min(1, OreWeightAt(Ore, Y) / Max)
    End Function

    ' ==================== 矿石图标 ====================

    ''' <summary>矿石强调色的渐变底（图片加载中/失败时的兜底）。</summary>
    Private Function OreAccentBrush(Accent As Color) As Brush
        Dim Dark = Color.FromRgb(CByte(Accent.R * 0.55), CByte(Accent.G * 0.55), CByte(Accent.B * 0.55))
        Dim Light = Color.FromRgb(
            CByte(Accent.R + (255 - Accent.R) * 0.35),
            CByte(Accent.G + (255 - Accent.G) * 0.35),
            CByte(Accent.B + (255 - Accent.B) * 0.35))
        Dim Brush As New LinearGradientBrush With {.StartPoint = New Point(0, 0), .EndPoint = New Point(0, 1)}
        Brush.GradientStops.Add(New GradientStop(Light, 0))
        Brush.GradientStops.Add(New GradientStop(Dark, 1))
        Return Brush
    End Function

    ''' <summary>矿石图标：mcshuo 图鉴贴图（MyImage 网络加载 + 本地缓存），渐变底兜底。</summary>
    Private Function BuildOreImageIcon(Ore As OreInfo, Size As Double) As Border
        Dim Icon As New Border With {
            .Width = Size, .Height = Size, .CornerRadius = New CornerRadius(Size * 0.22),
            .BorderBrush = New SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), .BorderThickness = New Thickness(1),
            .Background = OreAccentBrush(Ore.Accent), .ClipToBounds = True}
        Dim Img As New Image With {
            .Source = New MyBitmap(Ore.ImageUrl), .Stretch = Stretch.UniformToFill}
        Icon.Child = Img
        Return Icon
    End Function

End Class
