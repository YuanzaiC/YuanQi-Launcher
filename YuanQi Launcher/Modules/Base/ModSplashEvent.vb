Imports System.Globalization

''' <summary>
''' 特殊苏醒动画（春节 / 国庆 / 彩蛋）的触发规则。
''' <para/>只负责「本次启动该播放哪个动画」的判定，具体绘制在 FormMain 中实现。
''' </summary>
Public Module ModSplashEvent

    ''' <summary>
    ''' 特殊苏醒动画的种类。
    ''' </summary>
    Public Enum SpecialSplashTypes
        ''' <summary>不播放特殊动画。</summary>
        None = 0
        ''' <summary>彩蛋动画，每次启动有 2% 概率出现。</summary>
        EasterEgg = 1
        ''' <summary>春节动画，春节当天及前后 3 天出现。</summary>
        SpringFestival = 2
        ''' <summary>国庆动画，10 月 1 日及前后 3 天出现。</summary>
        NationalDay = 3
    End Enum

    ''' <summary>
    ''' 彩蛋动画每次启动的出现概率（百分比）。
    ''' </summary>
    Public Const EasterEggChance As Integer = 2
    ''' <summary>
    ''' 节日动画在节日当天前后各延伸的天数。
    ''' </summary>
    Public Const FestivalExtendDays As Integer = 3

    ''' <summary>
    ''' 本次启动被快捷方式参数强制指定的动画。Nothing 表示未指定。
    ''' </summary>
    Public ReadOnly Property ForcedType As SpecialSplashTypes?
        Get
            Return ParseForcedType(Environment.GetCommandLineArgs())
        End Get
    End Property

    ''' <summary>
    ''' 从命令行参数中解析被强制指定的动画类型。
    ''' 支持 -spring / -national / -egg（也接受 --spring 等双横线写法与 splash- 前缀）。
    ''' </summary>
    Public Function ParseForcedType(Args As IEnumerable(Of String)) As SpecialSplashTypes?
        If Args Is Nothing Then Return Nothing
        For Each RawArg As String In Args
            If String.IsNullOrWhiteSpace(RawArg) Then Continue For
            Dim Arg As String = RawArg.Trim().ToLowerInvariant().TrimStart("-"c, "/"c)
            If Arg.StartsWithF("splash-") Then Arg = Arg.Substring("splash-".Length)
            Select Case Arg
                Case "spring", "springfestival", "chunjie"
                    Return SpecialSplashTypes.SpringFestival
                Case "national", "nationalday", "guoqing"
                    Return SpecialSplashTypes.NationalDay
                Case "egg", "easteregg", "caidan"
                    Return SpecialSplashTypes.EasterEgg
            End Select
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 获取指定年份春节（农历正月初一）对应的公历日期。
    ''' 若无法推算则返回 Nothing。
    ''' </summary>
    Public Function GetSpringFestivalDate(Year As Integer) As Date?
        Try
            Dim Calendar As New ChineseLunisolarCalendar()
            If Year < Calendar.MinSupportedDateTime.Year OrElse Year > Calendar.MaxSupportedDateTime.Year Then Return Nothing
            '农历年的年份参数即该年春节所在的公历年份
            Return Calendar.ToDateTime(Year, 1, 1, 0, 0, 0, 0)
        Catch ex As Exception
            Logger.Warn(ex, $"推算 {Year} 年春节日期失败")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 判断某一天是否处于春节动画的显示窗口（春节当天及前后各 3 天）。
    ''' </summary>
    Public Function IsSpringFestivalWindow(Day As Date) As Boolean
        Dim Today As Date = Day.Date
        '农历跨年：1 月初的窗口可能属于上一年春节，2 月的属于本年
        For Each Year As Integer In {Today.Year, Today.Year - 1, Today.Year + 1}
            Dim Festival = GetSpringFestivalDate(Year)
            If Festival Is Nothing Then Continue For
            Dim StartDay As Date = Festival.Value.Date.AddDays(-FestivalExtendDays)
            Dim EndDay As Date = Festival.Value.Date.AddDays(FestivalExtendDays)
            If Today >= StartDay AndAlso Today <= EndDay Then Return True
        Next
        Return False
    End Function

    ''' <summary>
    ''' 判断某一天是否处于国庆动画的显示窗口（10 月 1 日及前后各 3 天）。
    ''' </summary>
    Public Function IsNationalDayWindow(Day As Date) As Boolean
        Dim StartDay As Date = New DateTime(Day.Year, 10, 1).AddDays(-FestivalExtendDays)
        Dim EndDay As Date = New DateTime(Day.Year, 10, 1).AddDays(FestivalExtendDays)
        Return Day.Date >= StartDay AndAlso Day.Date <= EndDay
    End Function

    ''' <summary>
    ''' 当前是否处于国庆期间（10 月 1 日及前后各 3 天）。
    ''' 国庆期间会把主题临时切换为红色、背景改为淡红色，节后自动恢复。
    ''' 用快捷方式参数 -national 强制预览时也一并生效。
    ''' </summary>
    Public Function IsNationalDayActive() As Boolean
        If ForcedType = SpecialSplashTypes.NationalDay Then Return True
        Return IsNationalDayWindow(Date.Now)
    End Function

    ''' <summary>
    ''' 当前是否处于春节期间（春节当天及前后各 3 天）。
    ''' 春节期间会把主题临时切换为暖红/灯火色板，节后自动恢复。
    ''' 用快捷方式参数 -spring 强制预览时也一并生效。
    ''' </summary>
    Public Function IsSpringFestivalActive() As Boolean
        If ForcedType = SpecialSplashTypes.SpringFestival Then Return True
        Return IsSpringFestivalWindow(Date.Now)
    End Function

    ''' <summary>
    ''' 决定本次启动要播放的特殊动画。
    ''' 优先级：快捷方式强制指定 &gt; 春节 &gt; 国庆 &gt; 彩蛋（2% 概率）。
    ''' 若设置中关闭了特殊动画，则除强制指定外一律返回 None。
    ''' </summary>
    Public Function Resolve() As SpecialSplashTypes
        '1. 快捷方式强制指定（无视日期与设置）
        Dim Forced = ForcedType
        If Forced IsNot Nothing Then
            Logger.Info($"快捷方式指定了特殊苏醒动画：{Forced.Value}")
            Return Forced.Value
        End If
        '2. 设置中关闭了特殊动画
        If Not Settings.Get(Of Boolean)("UiLauncherSpecialSplash") Then Return SpecialSplashTypes.None
        '3. 节日
        Dim Now0 As Date = Date.Now
        If IsSpringFestivalWindow(Now0) Then Return SpecialSplashTypes.SpringFestival
        If IsNationalDayWindow(Now0) Then Return SpecialSplashTypes.NationalDay
        '4. 彩蛋
        If RandomInteger(1, 100) <= EasterEggChance Then Return SpecialSplashTypes.EasterEgg
        Return SpecialSplashTypes.None
    End Function

    ''' <summary>
    ''' 动画类型的中文名称，用于日志与调试。
    ''' </summary>
    Public Function GetTypeName(Type As SpecialSplashTypes) As String
        Select Case Type
            Case SpecialSplashTypes.SpringFestival : Return "春节"
            Case SpecialSplashTypes.NationalDay : Return "国庆"
            Case SpecialSplashTypes.EasterEgg : Return "彩蛋"
            Case Else : Return "无"
        End Select
    End Function

End Module
