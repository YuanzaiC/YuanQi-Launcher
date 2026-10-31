Imports System.Windows.Shapes

''' <summary>
''' 游戏统计：按「近 1 天 / 近 1 周 / 近 1 月 / 自定义」统计游玩时长，数据来自 PlayLogModule。
''' 从主页「游玩洞察」卡片或工具箱「游戏统计」进入。
''' </summary>
Public Class PageGameStats

    Private Enum RangeKind
        Day1 = 0
        Week7 = 1
        Month30 = 2
        Custom = 3
    End Enum

    Private Structure Span
        Public StartDay As Date
        Public EndDay As Date
    End Structure

    Private CurRange As RangeKind = RangeKind.Week7
    Private CustomStart As Date = Date.Now.Date.AddDays(-13)
    Private CustomEnd As Date = Date.Now.Date
    Private IsBuilt As Boolean = False

    '图表数据缓存：窗口尺寸变化时直接重绘，不重新统计
    Private DayValues As Double()
    Private DayLabels As String()
    Private HourValues As Double()

    Private Sub PageGameStats_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Not IsBuilt Then
            IsBuilt = True
            AddHandler PanChartDays.SizeChanged, Sub(s, ev) DrawDaysChart()
            AddHandler PanChartHour.SizeChanged, Sub(s, ev) DrawHoursChart()
            AddHandler PageEnter, Sub() Rebuild()
            TxtFrom.Text = CustomStart.ToString("yyyy-MM-dd")
            TxtTo.Text = CustomEnd.ToString("yyyy-MM-dd")
            ApplyRangeChipVisual()
        End If
        Rebuild()
    End Sub

    ''' <summary>
    ''' 进入动画：本页用的是普通 Border（基类只会给 MyCard 做逐个进入动画），
    ''' 这里自己让各块自上而下依次淡入 + 上移归位。
    ''' </summary>
    Public Overrides Sub TriggerEnterAnimation(ParamArray Elements As FrameworkElement())
        For Each El In Elements
            If El IsNot Nothing Then El.Visibility = Visibility.Visible
        Next
        Dim Blocks As FrameworkElement() = {PanHead, PanKpi, PanDaysCard, PanSplit, PanRecentCard}
        Dim Anims As New List(Of AniData)
        Dim Delay As Integer = 0
        For Each Blk In Blocks
            If Blk Is Nothing Then Continue For
            Blk.Opacity = 0
            Blk.RenderTransform = New TranslateTransform(0, 16)
            Anims.Add(AaOpacity(Blk, 1, 230, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
            Anims.Add(AaTranslateY(Blk, -16, 330, Delay, New AniEaseOutFluent(AniEasePower.Middle)))
            Delay += 55
        Next
        '自定义范围条一直是收起状态，等用户点出来时单独淡入
        If PanCustom IsNot Nothing Then
            PanCustom.Opacity = 1
            PanCustom.RenderTransform = Nothing
        End If
        AniStart(Anims.ToArray(), "GameStats Enter")
        '页面里没有 MyCard，无需再走基类那一套
    End Sub

    '===============================
    '  时间范围
    '===============================

    Private Sub ApplyRangeChipVisual()
        SetChip(BtnRangeDay, RangeKind.Day1)
        SetChip(BtnRangeWeek, RangeKind.Week7)
        SetChip(BtnRangeMonth, RangeKind.Month30)
        SetChip(BtnRangeCustom, RangeKind.Custom)
    End Sub

    Private Sub SetChip(B As Button, K As RangeKind)
        If B Is Nothing Then Return
        If K = CurRange Then
            B.Background = TryCast(FindResource("BrushAccentSoft"), Brush)
            B.Foreground = TryCast(FindResource("ColorBrush3"), Brush)
        Else
            B.Background = Brushes.Transparent
            B.Foreground = TryCast(FindResource("BrushText2"), Brush)
        End If
    End Sub

    Private Sub PickRange(K As RangeKind)
        CurRange = K
        ApplyRangeChipVisual()
        If K = RangeKind.Custom Then
            PanCustom.Visibility = Visibility.Visible
            PanCustom.Opacity = 0
            PanCustom.RenderTransform = New TranslateTransform(0, 8)
        Else
            PanCustom.Visibility = Visibility.Collapsed
            PanCustom.Opacity = 1
            PanCustom.RenderTransform = Nothing
        End If
        Rebuild()
        PlayRangeSwitchAnim()
        If K = RangeKind.Custom Then
            AniStart({
                AaOpacity(PanCustom, 1, 170, 60, New AniEaseOutFluent(AniEasePower.Weak)),
                AaTranslateY(PanCustom, -8, 240, 60, New AniEaseOutFluent(AniEasePower.Middle))
            }, "GameStats Range Switch")
        End If
    End Sub

    Private Sub BtnRangeDay_Click(sender As Object, e As RoutedEventArgs) Handles BtnRangeDay.Click
        PickRange(RangeKind.Day1)
    End Sub

    Private Sub BtnRangeWeek_Click(sender As Object, e As RoutedEventArgs) Handles BtnRangeWeek.Click
        PickRange(RangeKind.Week7)
    End Sub

    Private Sub BtnRangeMonth_Click(sender As Object, e As RoutedEventArgs) Handles BtnRangeMonth.Click
        PickRange(RangeKind.Month30)
    End Sub

    Private Sub BtnRangeCustom_Click(sender As Object, e As RoutedEventArgs) Handles BtnRangeCustom.Click
        PickRange(RangeKind.Custom)
    End Sub

    Private Sub BtnApply_Click(sender As Object, e As RoutedEventArgs) Handles BtnApply.Click
        Dim F As Date = Nothing, T As Date = Nothing
        If Not Date.TryParse(TxtFrom.Text.Trim, F) Then
            Hint("开始日期格式不对，应形如 2026-07-01", HintType.Red)
            Return
        End If
        If Not Date.TryParse(TxtTo.Text.Trim, T) Then
            Hint("结束日期格式不对，应形如 2026-07-31", HintType.Red)
            Return
        End If
        If T < F Then
            Dim Tmp = F
            F = T
            T = Tmp
        End If
        CustomStart = F.Date
        CustomEnd = T.Date
        TxtFrom.Text = CustomStart.ToString("yyyy-MM-dd")
        TxtTo.Text = CustomEnd.ToString("yyyy-MM-dd")
        CurRange = RangeKind.Custom
        ApplyRangeChipVisual()
        Rebuild()
        PlayRangeSwitchAnim()
    End Sub

    ''' <summary>范围切换后的内容过渡：四个区块自上而下淡入 + 上移归位。</summary>
    Private Sub PlayRangeSwitchAnim()
        Dim Blocks As FrameworkElement() = {PanKpi, PanDaysCard, PanSplit, PanRecentCard}
        AniStop("GameStats Range Switch")
        For Each Blk In Blocks
            If Blk Is Nothing Then Continue For
            Blk.Opacity = 0
            Blk.RenderTransform = New TranslateTransform(0, 10)
        Next
        AniStart({
            AaOpacity(PanKpi, 1, 170, 0, New AniEaseOutFluent(AniEasePower.Weak)),
            AaTranslateY(PanKpi, -10, 240, 0, New AniEaseOutFluent(AniEasePower.Middle)),
            AaOpacity(PanDaysCard, 1, 170, 40, New AniEaseOutFluent(AniEasePower.Weak)),
            AaTranslateY(PanDaysCard, -10, 240, 40, New AniEaseOutFluent(AniEasePower.Middle)),
            AaOpacity(PanSplit, 1, 170, 80, New AniEaseOutFluent(AniEasePower.Weak)),
            AaTranslateY(PanSplit, -10, 240, 80, New AniEaseOutFluent(AniEasePower.Middle)),
            AaOpacity(PanRecentCard, 1, 170, 120, New AniEaseOutFluent(AniEasePower.Weak)),
            AaTranslateY(PanRecentCard, -10, 240, 120, New AniEaseOutFluent(AniEasePower.Middle))
        }, "GameStats Range Switch")
    End Sub

    ''' <summary>当前统计区间（含首尾两天）。</summary>
    Private Function CurrentSpan() As Span
        Dim Today = Date.Now.Date
        Dim R As New Span
        Select Case CurRange
            Case RangeKind.Day1
                R.StartDay = Today
                R.EndDay = Today
            Case RangeKind.Week7
                R.StartDay = Today.AddDays(-6)
                R.EndDay = Today
            Case RangeKind.Month30
                R.StartDay = Today.AddDays(-29)
                R.EndDay = Today
            Case Else
                Dim A = CustomStart, B = CustomEnd
                If B < A Then
                    Dim Tmp = A
                    A = B
                    B = Tmp
                End If
                R.StartDay = A
                R.EndDay = B
        End Select
        Return R
    End Function

    '===============================
    '  统计与渲染
    '===============================

    Private Sub Rebuild()
        Try
            Dim R = CurrentSpan()
            Dim SpanDays As Integer = CInt((R.EndDay - R.StartDay).TotalDays) + 1
            If SpanDays < 1 Then SpanDays = 1
            LabRangeDesc.Text = $"{R.StartDay:yyyy-MM-dd} ~ {R.EndDay:yyyy-MM-dd} · 共 {SpanDays} 天"

            Dim Sessions = PlayLogModule.LoadSessions()
            Dim InRange As New List(Of PlayLogModule.PlaySession)
            For Each S In Sessions
                Dim D = S.StartTime.Date
                If D >= R.StartDay AndAlso D <= R.EndDay Then InRange.Add(S)
            Next

            Dim PerDay As New Dictionary(Of Long, Long)
            For i As Integer = 0 To SpanDays - 1
                PerDay(R.StartDay.AddDays(i).Ticks) = 0
            Next
            Dim TotalSec As Long = 0
            Dim PerName As New Dictionary(Of String, Long)
            Dim PerHour(23) As Long
            For Each S In InRange
                TotalSec += S.DurationSeconds
                Dim K = S.StartTime.Date.Ticks
                If PerDay.ContainsKey(K) Then PerDay(K) = PerDay(K) + S.DurationSeconds
                Dim Nm = If(String.IsNullOrWhiteSpace(S.Name), "未知版本", S.Name.Trim())
                PerName(Nm) = If(PerName.ContainsKey(Nm), PerName(Nm), 0) + S.DurationSeconds
                Dim Hr As Integer = S.StartTime.Hour
                If Hr >= 0 AndAlso Hr <= 23 Then PerHour(Hr) = PerHour(Hr) + S.DurationSeconds
            Next

            Dim ActiveDays As Integer = 0
            Dim BestSec As Long = 0
            Dim BestDay As Date = Date.MinValue
            For i As Integer = 0 To SpanDays - 1
                Dim D = R.StartDay.AddDays(i)
                Dim V As Long = If(PerDay.ContainsKey(D.Ticks), PerDay(D.Ticks), 0)
                If V > 0 Then ActiveDays += 1
                If V > BestSec Then
                    BestSec = V
                    BestDay = D
                End If
            Next

            '总览
            LabKpiTotal.Text = FormatDuration(TotalSec)
            LabKpiTotalSub.Text = $"{InRange.Count} 段记录"
            LabKpiDays.Text = $"{ActiveDays} 天"
            LabKpiDaysSub.Text = $"区间共 {SpanDays} 天"
            LabKpiAvg.Text = FormatDuration(If(ActiveDays > 0, TotalSec \ ActiveDays, 0))
            LabKpiAvgSub.Text = "按有记录的天数计算"
            If BestSec > 0 Then
                LabKpiBest.Text = FormatDuration(BestSec)
                LabKpiBestSub.Text = BestDay.ToString("M月d日")
            Else
                LabKpiBest.Text = "—"
                LabKpiBestSub.Text = "暂无记录"
            End If
            LabDaysPeak.Text = If(BestSec > 0, $"峰值 {FormatDuration(BestSec)}", "")

            '每日柱状
            DayValues = New Double(SpanDays - 1) {}
            DayLabels = New String(SpanDays - 1) {}
            For i As Integer = 0 To SpanDays - 1
                Dim D = R.StartDay.AddDays(i)
                DayValues(i) = If(PerDay.ContainsKey(D.Ticks), PerDay(D.Ticks), 0) / 3600.0
                DayLabels(i) = D.ToString("M/d")
            Next
            DrawDaysChart()

            '时段分布
            HourValues = New Double(23) {}
            For H As Integer = 0 To 23
                HourValues(H) = PerHour(H) / 3600.0
            Next
            DrawHoursChart()

            BuildRank(PerName)
            BuildRecent(InRange)
        Catch ex As Exception
            Logger.Warn(ex, "构建游戏统计失败")
        End Try
    End Sub

    Private Function AccentColor() As Color
        Dim B = TryCast(FindResource("ColorBrush3"), SolidColorBrush)
        If B Is Nothing Then Return Color.FromRgb(&H3D, &H8A, &H2D)
        Return B.Color
    End Function

    Private Sub DrawDaysChart()
        If DayValues Is Nothing OrElse DayValues.Length = 0 Then Return
        Dim Every As Integer = Math.Max(1, CInt(Math.Ceiling(DayValues.Length / 9.0)))
        DrawBars(PanChartDays, DayValues, DayLabels, Every)
    End Sub

    Private Sub DrawHoursChart()
        If HourValues Is Nothing Then Return
        Dim Labels(23) As String
        For i As Integer = 0 To 23
            Labels(i) = If(i Mod 3 = 0, i & "时", "")
        Next
        DrawBars(PanChartHour, HourValues, Labels, 1)
    End Sub

    ''' <summary>画一组圆角柱状图（Values 单位：小时）。</summary>
    Private Sub DrawBars(C As Canvas, Values As Double(), Labels As String(), LabelEvery As Integer)
        If C Is Nothing Then Return
        C.Children.Clear()
        Dim W As Double = C.ActualWidth, H As Double = C.ActualHeight
        If W < 40 OrElse H < 40 OrElse Values Is Nothing OrElse Values.Length = 0 Then Return
        Dim PadBottom As Double = 16
        Dim PlotH As Double = H - PadBottom
        If PlotH < 10 Then Return
        Dim MaxV As Double = 0
        For Each V In Values
            If V > MaxV Then MaxV = V
        Next
        If MaxV <= 0 Then MaxV = 1
        Dim BorderBrush = TryCast(FindResource("BrushBorder"), Brush)
        '基线
        Dim Base As New Rectangle With {.Width = W, .Height = 1, .Fill = BorderBrush}
        Canvas.SetLeft(Base, 0)
        Canvas.SetTop(Base, PlotH)
        C.Children.Add(Base)
        Dim Accent = AccentColor()
        Dim Slot As Double = W / Values.Length
        Dim BarW As Double = Math.Max(2.0, Math.Min(26.0, Slot * 0.62))
        For i As Integer = 0 To Values.Length - 1
            Dim V = Values(i)
            If V > 0 Then
                Dim Hh As Double = Math.Max(2.0, PlotH * V / MaxV)
                Dim Rc As New Rectangle With {.Width = BarW, .Height = Hh, .RadiusX = 2.5, .RadiusY = 2.5,
                                              .Fill = New SolidColorBrush(Color.FromArgb(232, Accent.R, Accent.G, Accent.B))}
                Canvas.SetLeft(Rc, i * Slot + (Slot - BarW) / 2)
                Canvas.SetTop(Rc, PlotH - Hh)
                C.Children.Add(Rc)
            End If
            If Labels IsNot Nothing AndAlso i < Labels.Length AndAlso LabelEvery > 0 AndAlso
               i Mod LabelEvery = 0 AndAlso Not String.IsNullOrEmpty(Labels(i)) Then
                Dim T As New TextBlock With {.Text = Labels(i), .FontSize = 9.5, .IsHitTestVisible = False}
                T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
                T.Measure(New Size(160, 20))
                Dim Lx As Double = i * Slot + (Slot - T.DesiredSize.Width) / 2
                If Lx < 0 Then Lx = 0
                If Lx + T.DesiredSize.Width > W Then Lx = Math.Max(0, W - T.DesiredSize.Width)
                Canvas.SetLeft(T, Lx)
                Canvas.SetTop(T, PlotH + 3)
                C.Children.Add(T)
            End If
        Next
    End Sub

    '===============================
    '  实例排行 / 最近游玩
    '===============================

    Private Sub BuildRank(PerName As Dictionary(Of String, Long))
        PanRank.Children.Clear()
        If PerName Is Nothing OrElse PerName.Count = 0 Then
            PanRank.Children.Add(MutedLine("这段时间还没有游玩记录"))
            Return
        End If
        Dim MaxSec As Long = PerName.Values.Max()
        Dim Shown As Integer = 0
        For Each Kvp In PerName.OrderByDescending(Function(K) K.Value)
            If Shown >= 6 Then Exit For
            PanRank.Children.Add(BuildRankRow(Kvp.Key, Kvp.Value, MaxSec))
            Shown += 1
        Next
        If PerName.Count > 6 Then PanRank.Children.Add(MutedLine($"另有 {PerName.Count - 6} 个实例未显示"))
    End Sub

    Private Function BuildRankRow(Name As String, Sec As Long, MaxSec As Long) As FrameworkElement
        Dim Sp As New StackPanel With {.Margin = New Thickness(0, 0, 0, 10)}
        Dim Head As New Grid
        Head.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Head.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim N As New TextBlock With {.Text = Name, .FontSize = 12, .FontWeight = FontWeights.SemiBold, .TextTrimming = TextTrimming.CharacterEllipsis}
        N.SetResourceReference(TextBlock.ForegroundProperty, "BrushText1")
        Head.Children.Add(N)
        Dim D As New TextBlock With {.Text = FormatDuration(Sec), .FontSize = 11, .Margin = New Thickness(8, 0, 0, 0)}
        D.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
        Grid.SetColumn(D, 1)
        Head.Children.Add(D)
        Sp.Children.Add(Head)
        '比例条：两列星号分配，宽度自适应
        Dim Pct As Double = If(MaxSec <= 0, 0.0, Sec / MaxSec)
        Pct = Math.Max(0.02, Math.Min(1.0, Pct))
        Dim Bar As New Grid With {.Height = 6, .Margin = New Thickness(0, 5, 0, 0)}
        Bar.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(Pct, GridUnitType.Star)})
        Bar.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1 - Pct, GridUnitType.Star)})
        Dim Track As New Border With {.CornerRadius = New CornerRadius(3)}
        Track.SetResourceReference(Border.BackgroundProperty, "BrushBorder")
        Grid.SetColumnSpan(Track, 2)
        Bar.Children.Add(Track)
        Dim Fill As New Border With {.CornerRadius = New CornerRadius(3), .Background = New SolidColorBrush(AccentColor())}
        Bar.Children.Add(Fill)
        Sp.Children.Add(Bar)
        Return Sp
    End Function

    Private Sub BuildRecent(InRange As List(Of PlayLogModule.PlaySession))
        PanRecentList.Children.Clear()
        If InRange Is Nothing OrElse InRange.Count = 0 Then
            PanRecentList.Children.Add(MutedLine("还没有游玩记录，启动一次游戏后就会出现在这里"))
            Return
        End If
        Dim Shown As Integer = 0
        For Each S In InRange.OrderByDescending(Function(X) X.StartTime)
            If Shown >= 8 Then Exit For
            PanRecentList.Children.Add(BuildRecentRow(S))
            Shown += 1
        Next
    End Sub

    Private Function BuildRecentRow(S As PlayLogModule.PlaySession) As FrameworkElement
        Dim G As New Grid With {.Margin = New Thickness(0, 0, 0, 7)}
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Dot As New Border With {.Width = 6, .Height = 6, .CornerRadius = New CornerRadius(3),
                                    .VerticalAlignment = VerticalAlignment.Center, .Background = New SolidColorBrush(AccentColor())}
        G.Children.Add(Dot)
        Dim N As New TextBlock With {.Text = If(String.IsNullOrWhiteSpace(S.Name), "未知版本", S.Name.Trim()),
                                     .FontSize = 12, .Margin = New Thickness(10, 0, 0, 0), .TextTrimming = TextTrimming.CharacterEllipsis}
        N.SetResourceReference(TextBlock.ForegroundProperty, "BrushText1")
        Grid.SetColumn(N, 1)
        G.Children.Add(N)
        Dim Info As New TextBlock With {
            .Text = $"{S.StartTime.ToString("M月d日 HH:mm")} · {FormatDuration(S.DurationSeconds)}",
            .FontSize = 11, .Margin = New Thickness(12, 0, 0, 0)}
        Info.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
        Grid.SetColumn(Info, 2)
        G.Children.Add(Info)
        Return G
    End Function

    Private Function MutedLine(Text As String) As FrameworkElement
        Dim T As New TextBlock With {.Text = Text, .FontSize = 11.5, .Margin = New Thickness(2, 4, 0, 4), .TextWrapping = TextWrapping.Wrap}
        T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
        Return T
    End Function

    Private Function FormatDuration(Seconds As Long) As String
        If Seconds < 60 Then Return Math.Max(0, Seconds) & " 秒"
        Dim Min = CInt(Seconds \ 60)
        If Min < 60 Then Return Min & " 分钟"
        Dim H = Min \ 60, M = Min Mod 60
        Return H & " 小时" & If(M > 0, " " & M & " 分", "")
    End Function

End Class
