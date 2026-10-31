'由于包含加解密等安全信息，本文件中的部分代码已被删除

Friend Module ModSecret

    '标注 缘启 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "OpenSource"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "YQL"
    '用于微软登录的 ClientId
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("YQL_MS_CLIENT_ID"), "")
    'CurseForge API Key
    Public CurseForgeAPIKey As String = If(Environment.GetEnvironmentVariable("YQL_CURSEFORGE_API_KEY"), "")

#Region "网络鉴权"

    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"YQL/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"YQL/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.open.pcl2.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then Req.Headers.Add("x-api-key", CurseForgeAPIKey)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public ThemeNow As Integer = -1
    Public ColorHue As Integer = 210, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0
    ''' <summary>
    ''' 填充色（强调色）上的文字颜色：按强调色亮度自动取白色或墨色，避免深色主题下黑字看不清。
    ''' 由 ThemeRefresh 每次刷新时更新。
    ''' </summary>
    Public OnAccentColor As New MyColor(255, 255, 255)

    ''' <summary>sRGB 相对亮度（WCAG 定义）。</summary>
    Private Function RelativeLuminance(C As Color) As Double
        Dim F = Function(V As Double) If(V <= 0.03928, V / 12.92, ((V + 0.055) / 1.055) ^ 2.4)
        Return 0.2126 * F(C.R / 255) + 0.7152 * F(C.G / 255) + 0.0722 * F(C.B / 255)
    End Function
    ''' <summary>两个相对亮度之间的对比度（1~21）。</summary>
    Private Function ContrastRatio(Lum1 As Double, Lum2 As Double) As Double
        Dim Hi As Double = Math.Max(Lum1, Lum2), Lo As Double = Math.Min(Lum1, Lum2)
        Return (Hi + 0.05) / (Lo + 0.05)
    End Function

#Region "国庆色板（绛红 / 故宫红）"

    '深底
    Public ReadOnly PaletteRedDark As New MyColor(58, 10, 10)     '#3A0A0A
    Public ReadOnly PaletteRedDark2 As New MyColor(92, 16, 16)    '#5C1010
    '主红
    Public ReadOnly PaletteRedDeep As New MyColor(140, 28, 19)    '#8C1C13
    Public ReadOnly PaletteRedMain As New MyColor(122, 31, 31)    '#7A1F1F
    '点缀朱红
    Public ReadOnly PaletteVermilion As New MyColor(200, 16, 46)  '#C8102E
    '金色（仅细线 / 小图标）
    Public ReadOnly PaletteGold As New MyColor(240, 217, 160)    '#F0D9A0 香槟金（细线用，比 #D4AF37 更亮更干净）
    '米白
    Public ReadOnly PaletteCream As New MyColor(251, 246, 236)    '#FBF6EC
    Public ReadOnly PaletteCreamLight As New MyColor(255, 252, 246) '#FFFCF6
    Public ReadOnly PaletteCreamSoft As New MyColor(241, 234, 218)  '#F1EADA
    '墨色文字
    Public ReadOnly PaletteInk As New MyColor(43, 43, 43)         '#2B2B2B
    '细线
    Public ReadOnly PaletteLine As New MyColor(216, 203, 180)     '#D8CBB4

#End Region

#Region "春节色板（暖红 / 灯火）"

    '深底
    Public ReadOnly PaletteSpringDark As New MyColor(74, 14, 14)      '#4A0E0E
    Public ReadOnly PaletteSpringDark2 As New MyColor(107, 20, 20)    '#6B1414
    '主红（比国庆略暖）
    Public ReadOnly PaletteSpringRed As New MyColor(166, 27, 27)      '#A61B1B
    Public ReadOnly PaletteSpringRed2 As New MyColor(178, 34, 34)     '#B22222
    '朱砂 / 胭脂
    Public ReadOnly PaletteSpringVermilion As New MyColor(216, 74, 58) '#D94A3A
    '暖金（春节金色可以比国庆多一点）
    Public ReadOnly PaletteSpringGold As New MyColor(244, 219, 158)   '#F4DB9E 香槟金
    Public ReadOnly PaletteSpringGold2 As New MyColor(232, 201, 132)   '#E8C984
    '米白
    Public ReadOnly PaletteSpringCream As New MyColor(255, 250, 240)  '#FFFAF0
    Public ReadOnly PaletteSpringCream2 As New MyColor(251, 244, 230) '#FBF4E6
    '墨色文字（偏暖）
    Public ReadOnly PaletteSpringInk As New MyColor(43, 27, 27)       '#2B1B1B
    '少量青绿点缀（只做小面积）
    Public ReadOnly PaletteSpringJade As New MyColor(31, 92, 74)      '#1F5C4A
    '细线
    Public ReadOnly PaletteSpringLine As New MyColor(227, 211, 184)   '#E3D3B8

#End Region

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            If ThemeNow = NewTheme AndAlso NewTheme >= 0 Then Return
            If NewTheme >= 0 Then ThemeNow = NewTheme

            '按主题预设色相 / 饱和度 / 亮度 / 顶栏渐变
            Select Case ThemeNow
                Case 1 '甜柠青
                    ColorHue = 160 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 2 '小草绿
                    ColorHue = 120 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 3 '菠萝黄
                    ColorHue = 50 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 4 '橡木棕
                    ColorHue = 30 : ColorSat = 45 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 5 '玄素黑（灰）
                    ColorHue = 210 : ColorSat = 0 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 6 '铁杆粉
                    ColorHue = 330 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 7 '神秘紫
                    ColorHue = 270 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 8 '秋仪金
                    ColorHue = 42 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 9 '活跃橙
                    ColorHue = 25 : ColorSat = 60 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 10 '跳票红
                    ColorHue = 0 : ColorSat = 60 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 11 '极客蓝
                    ColorHue = 215 : ColorSat = 60 : ColorLightAdjust = -5 : ColorHueTopbarDelta = 0
                Case 12 '滑稽彩
                    ColorHue = 300 : ColorSat = 55 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 13 '欧皇彩
                    ColorHue = 45 : ColorSat = 60 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
                Case 14 '自定义
                    ColorHue = Settings.Get(Of Integer)("UiLauncherHue")
                    ColorSat = Settings.Get(Of Integer)("UiLauncherSat")
                    ColorLightAdjust = Settings.Get(Of Integer)("UiLauncherLight") - 20
                    ColorHueTopbarDelta = Settings.Get(Of Integer)("UiLauncherDelta") - 90
                Case Else '0 草坪绿
                    ColorHue = 110 : ColorSat = 50 : ColorLightAdjust = 0 : ColorHueTopbarDelta = 0
            End Select

            Color1 = New MyColor(27, 27, 27)
            '节日期间使用对应色板（只在本次运行生效，不写入设置，节后自动恢复）
            '国庆：绛红/故宫红（暗红 + 米白 + 朱红/金点缀）
            '春节：暖红 + 米白 + 暖金（比国庆更暖更亮，金色更多一点，但界面仍然克制）
            Dim IsNationalDay As Boolean = ModSplashEvent.IsNationalDayActive()
            Dim IsSpringFestival As Boolean = (Not IsNationalDay) AndAlso ModSplashEvent.IsSpringFestivalActive()
            Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 40 + ColorLightAdjust * 0.6)
            Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 47 + ColorLightAdjust * 0.6)
            Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 56 + ColorLightAdjust * 0.6)
            Color5 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.45, 84 + ColorLightAdjust * 0.15)
            Color6 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.35, 90)
            Color7 = New MyColor(249, 249, 249)
            Color8 = New MyColor(255, 255, 255)
            ColorBg0 = New MyColor(209, 209, 209)
            ColorBg1 = New MyColor(243, 243, 243)
            If IsNationalDay Then
                '墨色文字
                Color1 = PaletteInk
                '主红（按钮、强调）
                Color2 = PaletteRedDeep
                Color3 = PaletteRedMain
                Color4 = PaletteVermilion
                '柔和底色：米白系
                Color5 = PaletteCreamSoft
                Color6 = PaletteCream
                Color7 = PaletteCream
                Color8 = PaletteCreamLight
                ColorBg0 = PaletteLine
                ColorBg1 = PaletteCreamSoft
                Logger.Info("国庆期间：已切换为绛红/故宫红色板（暗红 + 米白 + 朱红/金点缀）")
            ElseIf IsSpringFestival Then
                '暖墨色文字
                Color1 = PaletteSpringInk
                '暖红（按钮、强调）
                Color2 = PaletteSpringRed
                Color3 = PaletteSpringRed2
                Color4 = PaletteSpringVermilion
                '柔和底色：暖米白
                Color5 = PaletteSpringCream2
                Color6 = PaletteSpringCream
                Color7 = PaletteSpringCream
                Color8 = PaletteSpringCream2
                ColorBg0 = PaletteSpringLine
                ColorBg1 = PaletteSpringCream2
                Logger.Info("春节期间：已切换为暖红/灯火色板（暖红 + 米白 + 暖金点缀）")
            End If
            '页面表面：节日期间用米白，其余保持纯白
            Try
                If IsNationalDay Then
                    Application.Current.Resources("BrushSurface") = New SolidColorBrush(PaletteCream)
                    Application.Current.Resources("BrushSurface2") = New SolidColorBrush(PaletteCreamLight)
                ElseIf IsSpringFestival Then
                    Application.Current.Resources("BrushSurface") = New SolidColorBrush(PaletteSpringCream)
                    Application.Current.Resources("BrushSurface2") = New SolidColorBrush(PaletteSpringCream2)
                Else
                    Application.Current.Resources("BrushSurface") = New SolidColorBrush(New MyColor(255, 255, 255))
                    Application.Current.Resources("BrushSurface2") = New SolidColorBrush(New MyColor(250, 250, 250))
                End If
            Catch ex As Exception
                Logger.Warn(ex, "切换节日表面配色失败")
            End Try

            ColorSemiTransparent = New MyColor(1, Color8)
            '柔和强调底色（导航选中、按钮选中、卡片描边等）跟随主题，不再固定为绿色
            Dim SoftAccent As MyColor
            Dim SoftAccent2 As MyColor
            If IsNationalDay Then
                '与绛红色板同族：极淡的绛红底
                SoftAccent = New MyColor(242, 219, 213)
                SoftAccent2 = New MyColor(250, 237, 233)
            ElseIf IsSpringFestival Then
                '与暖红色板同族：极淡的暖红底
                SoftAccent = New MyColor(248, 228, 216)
                SoftAccent2 = New MyColor(253, 242, 232)
            Else
                SoftAccent = New MyColor().FromHSL2(ColorHue, ColorSat * 0.42, 90)
                SoftAccent2 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.28, 94)
            End If
            Application.Current.Resources("BrushAccentSoft") = New SolidColorBrush(SoftAccent)
            Application.Current.Resources("BrushAccentSoft2") = New SolidColorBrush(SoftAccent2)
            '填充色上的文字色：分别算「白字」和「墨字」与强调色的 WCAG 对比度，选更清楚的那个
            Dim AccentColor As Color = CType(Color3, Color)
            Dim AccentLum As Double = RelativeLuminance(AccentColor)
            Dim WhiteRatio As Double = ContrastRatio(1.0, AccentLum)
            Dim InkRatio As Double = ContrastRatio(RelativeLuminance(CType(PaletteInk, Color)), AccentLum)
            OnAccentColor = If(WhiteRatio >= InkRatio, New MyColor(255, 255, 255), New MyColor(43, 43, 43))
            Application.Current.Resources("ColorBrushOnAccent") = New SolidColorBrush(OnAccentColor)
            Application.Current.Resources("ColorObjectOnAccent") = CType(OnAccentColor, Color)
            Logger.Info($"填充色文字：强调色 {AccentColor}，白字对比度 {WhiteRatio:0.00}，墨字对比度 {InkRatio:0.00} → 选用 {(If(WhiteRatio >= InkRatio, "白色", "墨色"))}")
            Application.Current.Resources("ColorBrush1") = New SolidColorBrush(Color1)
            Application.Current.Resources("ColorBrush2") = New SolidColorBrush(Color2)
            Application.Current.Resources("ColorBrush3") = New SolidColorBrush(Color3)
            Application.Current.Resources("ColorBrush4") = New SolidColorBrush(Color4)
            Application.Current.Resources("ColorBrush5") = New SolidColorBrush(Color5)
            Application.Current.Resources("ColorBrush6") = New SolidColorBrush(Color6)
            Application.Current.Resources("ColorBrush7") = New SolidColorBrush(Color7)
            Application.Current.Resources("ColorBrush8") = New SolidColorBrush(Color8)
            Application.Current.Resources("ColorBrushBg0") = New SolidColorBrush(ColorBg0)
            Application.Current.Resources("ColorBrushBg1") = New SolidColorBrush(ColorBg1)
            Application.Current.Resources("ColorObject1") = CType(Color1, Color)
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            ThemeRefreshMain()
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            Dim IsNationalDay As Boolean = ModSplashEvent.IsNationalDayActive()
            '顶部条背景（Windows 11：白色表面）；节日期间换成米白，与暖红/金色点缀更协调
            Dim IsSpring As Boolean = (Not IsNationalDay) AndAlso ModSplashEvent.IsSpringFestivalActive()
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            If IsNationalDay Then
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = PaletteCreamLight})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = PaletteCream})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = PaletteCreamLight})
            ElseIf IsSpring Then
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = PaletteSpringCream})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = PaletteSpringCream2})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = PaletteSpringCream})
            Else
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor(255, 255, 255)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor(252, 252, 252)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor(255, 255, 255)})
            End If
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主窗口底色：节日期间用暖米白，避免残留一块灰白
            Dim FormBg As MyColor = New MyColor(243, 243, 243)
            If IsNationalDay Then FormBg = PaletteCreamSoft
            If IsSpring Then FormBg = PaletteSpringCream2
            FrmMain.PanForm.Background = New SolidColorBrush(FormBg)
            FrmMain.PanForm.Background.Freeze()
            '标题栏「Y」徽章：跟随主题（原先固定为绿色渐变）
            Try
                If FrmMain.PanTitleBadge IsNot Nothing Then
                    Dim Badge As New LinearGradientBrush With {.EndPoint = New Point(0.5, 1), .StartPoint = New Point(0.3, 0)}
                    Badge.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = CType(Color3, Color)})
                    Badge.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = CType(Color2, Color)})
                    FrmMain.PanTitleBadge.Background = Badge
                    FrmMain.PanTitleBadge.Background.Freeze()
                End If
            Catch ex As Exception
                Logger.Warn(ex, "更新标题徽章配色失败")
            End Try
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        Return False
    End Function

#End Region

#Region "更新"

    Friend Sub UpdateCheckByButton()
        Hint("该版本中不包含更新功能……")
    End Sub

    Friend IsUpdateWaitingRestart As Boolean = False
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
    End Sub
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
    End Sub

    ''' <summary>
    ''' 确保 PathTemp/Latest.exe 是最新正式版的 缘启，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
    End Sub

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("YQL 配置更新", Sub() Logger.Info("该版本中不包含更新通知功能……"), Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

#End Region

#Region "赞助等级"

    Public ReadOnly Property CurrentRank As DonationRank
        Get
            Return DonationRank.None
        End Get
    End Property

    ''' <summary>
    ''' 获取设备识别码。
    ''' 仅用于本机设置项的加密密钥，不再对外提供「识别码」入口。
    ''' </summary>
    Friend Function GetIdentify() As String
        Return "0000-0000-0000-0000"
    End Function

#End Region

End Module
