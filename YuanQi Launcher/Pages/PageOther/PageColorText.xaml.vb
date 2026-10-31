Imports System.Windows.Threading
Imports System.Windows.Media.Effects

''' <summary>
''' 彩色文字生成器：左侧编辑（输入 / 样式 / 颜色 / 渐变），右侧预览（聊天 / MotD / 标题三场景）与输出（§码、&amp;码、JSON、MiniMessage 四格式）。
''' </summary>
Public Class PageColorText

    ' ==================== 撤销 / 重做（快照栈） ====================
    Private _UndoList As New List(Of String)
    Private _UndoPtr As Integer = -1
    Private _Restoring As Boolean = False
    Private _ProgrammaticOp As Boolean = False
    Private _LastPushTick As Integer = 0
    Private _LastPushTyping As Boolean = False
    Private Const MaxUndo As Integer = 100

    ' ==================== 场景 / 格式 / 渐变 ====================
    Private _Format As McColorText.McOutputFormat = McColorText.McOutputFormat.Section
    Private _GradStart As Color = Color.FromRgb(255, 210, 120)
    Private _GradEnd As Color = Color.FromRgb(255, 40, 0)
    Private _RainbowBar As Boolean = False
    Private _BrushDuo As LinearGradientBrush
    Private _BrushRainbow As LinearGradientBrush

    ' ==================== 混淆动画 ====================
    Private _ObfTimer As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(100)}
    Private _ObfAll As New List(Of Run)

    ' ==================== 色块 ====================
    Private _Swatches As New List(Of Button)

    Private Sub PageColorText_Loaded() Handles Me.Loaded
        InitClassicColors()
        InitGradient()
        '撤销栈种子：保留最初空状态
        _UndoList.Clear()
        _UndoList.Add(TextColorInput.Text)
        _UndoPtr = 0
        UpdateUndoButtons()
        AddHandler _ObfTimer.Tick, AddressOf ObfTimer_Tick
        '挂载处于切页动画的 AniControlEnabled 抑制窗口内：入场动画延迟 30ms 触发，每次进入都播放
        AniStart({AaCode(Sub() AnimateModulesEnter(), 30, True)}, "PageColorText Enter Trigger")
        RefreshAll()
    End Sub

    Private Sub PageColorText_Unloaded() Handles Me.Unloaded
        _ObfTimer.IsEnabled = False
    End Sub

        ' <summary>模块依次淡入上移：页头 + 左右两栏的各卡片交错入场。</summary>
    Private Sub AnimateModulesEnter()
        Try
            If AniControlEnabled <> 0 Then Return
            Dim Order As New List(Of UIElement) From {PanHeader}
            Dim LeftList = PanColLeft.Children.OfType(Of UIElement)().ToList()
            Dim RightList = PanColRight.Children.OfType(Of UIElement)().ToList()
            For i As Integer = 0 To Math.Max(LeftList.Count, RightList.Count) - 1
                If i < LeftList.Count Then Order.Add(LeftList(i))
                If i < RightList.Count Then Order.Add(RightList(i))
            Next
            Dim Anims As New List(Of AniData)
            For i As Integer = 0 To Order.Count - 1
                Dim Elem As UIElement = Order(i)
                Elem.Opacity = 0
                Elem.RenderTransform = New TranslateTransform(0, 16)
                Anims.Add(AaOpacity(Elem, 1, 240, 60 + i * 45, New AniEaseOutFluent(AniEasePower.Weak)))
                Anims.Add(AaTranslateY(Elem, -16, 320, 60 + i * 45, New AniEaseOutFluent(AniEasePower.Weak)))
            Next
            AniStart(Anims.ToArray(), "PageColorText Enter")
        Catch ex As Exception
            Logger.Warn(ex, "彩色文字页入场动画失败")
        End Try
    End Sub

    ' ==================== 撤销 / 重做 ====================

    ''' <summary>在程序化修改文本外包裹此方法，使撤销栈记录为独立操作（不打并与输入合并）。</summary>
    Private Sub ApplyChange(action As Action)
        _ProgrammaticOp = True
        Try
            action()
        Finally
            _ProgrammaticOp = False
        End Try
    End Sub

    Private Sub PushUndo(typing As Boolean)
        Dim text = TextColorInput.Text
        Dim tick = Environment.TickCount
        If _UndoList.Count > 0 AndAlso _UndoPtr >= 0 AndAlso _UndoList(_UndoPtr) = text Then
            _LastPushTyping = typing
            Return
        End If
        If typing AndAlso _LastPushTyping AndAlso _UndoList.Count > 0 AndAlso tick - _LastPushTick < 600 Then
            '连续击键合并为一条快照
            _UndoList(_UndoPtr) = text
        Else
            If _UndoPtr < _UndoList.Count - 1 Then _UndoList.RemoveRange(_UndoPtr + 1, _UndoList.Count - _UndoPtr - 1)
            _UndoList.Add(text)
            If _UndoList.Count > MaxUndo Then _UndoList.RemoveAt(0)
            _UndoPtr = _UndoList.Count - 1
        End If
        _LastPushTick = tick
        _LastPushTyping = typing
        UpdateUndoButtons()
    End Sub

    Private Sub Undo()
        If _UndoPtr <= 0 Then Return
        _UndoPtr -= 1
        RestoreState(_UndoList(_UndoPtr))
    End Sub

    Private Sub Redo()
        If _UndoPtr >= _UndoList.Count - 1 Then Return
        _UndoPtr += 1
        RestoreState(_UndoList(_UndoPtr))
    End Sub

    Private Sub RestoreState(text As String)
        _Restoring = True
        Try
            TextColorInput.Text = text
            TextColorInput.CaretIndex = text.Length
        Finally
            _Restoring = False
        End Try
        RefreshAll()
        UpdateUndoButtons()
    End Sub

    Private Sub UpdateUndoButtons()
        If BtnColorUndo Is Nothing OrElse BtnColorRedo Is Nothing Then Return
        BtnColorUndo.IsEnabled = _UndoPtr > 0
        BtnColorRedo.IsEnabled = _UndoPtr < _UndoList.Count - 1
    End Sub

    Private Sub TextColorInput_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextColorInput.TextChanged
        If _Restoring Then Return
        PushUndo(Not _ProgrammaticOp)
        RefreshAll()
    End Sub

    Private Sub TextColorInput_SelectionChanged(sender As Object, e As RoutedEventArgs) Handles TextColorInput.SelectionChanged
        UpdateCaretState()
    End Sub

    Private Sub BtnColorUndo_Click(sender As Object, e As RoutedEventArgs) Handles BtnColorUndo.Click
        Undo()
    End Sub

    Private Sub BtnColorRedo_Click(sender As Object, e As RoutedEventArgs) Handles BtnColorRedo.Click
        Redo()
    End Sub

    ' ==================== 编辑操作 ====================

    Private Sub InsertAtCaret(code As String)
        Dim pos = Math.Min(TextColorInput.CaretIndex, TextColorInput.Text.Length)
        TextColorInput.Text = TextColorInput.Text.Insert(pos, code)
        TextColorInput.CaretIndex = Math.Min(pos + code.Length, TextColorInput.Text.Length)
    End Sub

    ''' <summary>样式码切换：作用于选中文字（含撤销重建），否则作用于光标处。</summary>
    Private Sub ToggleStyle(code As Char)
        Try
            Dim raw = TextColorInput.Text
            If TextColorInput.SelectionLength > 0 Then
                Dim s = TextColorInput.SelectionStart, l = TextColorInput.SelectionLength
                Dim state = McColorText.GetStateAt(raw, s)
                Dim sel = McColorText.RemoveStyleCode(raw.Substring(s, l), code)
                Dim newSel As String
                If McColorText.IsStyleActive(state, code) Then
                    newSel = "§r" & McColorText.ResumeCodes(state, code) & sel
                Else
                    newSel = "§" & code & sel
                End If
                ApplyChange(Sub()
                                TextColorInput.SelectedText = newSel
                                TextColorInput.SelectionLength = 0
                            End Sub)
            Else
                If McColorText.IsStyleActive(McColorText.GetStateAt(raw, TextColorInput.CaretIndex), code) Then
                    Dim state = McColorText.GetStateAt(raw, TextColorInput.CaretIndex)
                    ApplyChange(Sub() InsertAtCaret("§r" & McColorText.ResumeCodes(state, code)))
                Else
                    ApplyChange(Sub() InsertAtCaret("§" & code))
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "切换样式失败")
        End Try
    End Sub

    ''' <summary>应用颜色：作用于选中文字（剥离旧色并恢复后续状态），否则插入到光标处。</summary>
    Private Sub ApplyColorToTarget(color As Color)
        Try
            Dim raw = TextColorInput.Text
            If TextColorInput.SelectionLength > 0 Then
                Dim s = TextColorInput.SelectionStart, l = TextColorInput.SelectionLength
                Dim state = McColorText.GetStateAt(raw, s)
                Dim sel = raw.Substring(s, l)
                Dim sb As New Text.StringBuilder()
                sb.Append(McColorText.ColorCodeFor(color)).Append(McColorText.StripColorCodes(sel))
                If s + l < raw.Length Then sb.Append(McColorText.ResumeCodes(state))
                ApplyChange(Sub()
                                TextColorInput.SelectedText = sb.ToString()
                                TextColorInput.SelectionLength = 0
                            End Sub)
            Else
                ApplyChange(Sub() InsertAtCaret(McColorText.ColorCodeFor(color)))
            End If
        Catch ex As Exception
            Logger.Warn(ex, "应用颜色失败")
        End Try
    End Sub

    ''' <summary>应用渐变（presetName 为空时使用当前起止色），作用于选中文字或全文。</summary>
    Private Sub ApplyGradientToTarget(presetName As String)
        Try
            Dim raw = TextColorInput.Text
            If raw = "" Then
                Hint("请先输入文字", HintType.Blue)
                Return
            End If
            If TextColorInput.SelectionLength > 0 Then
                Dim s = TextColorInput.SelectionStart, l = TextColorInput.SelectionLength
                Dim state = McColorText.GetStateAt(raw, s)
                Dim src = raw.Substring(s, l)
                Dim grad = If(presetName Is Nothing,
                    McColorText.ApplyGradient(src, _GradStart, _GradEnd),
                    McColorText.PresetGradient(presetName, src))
                Dim sb As New Text.StringBuilder(grad)
                If s + l < raw.Length Then sb.Append(McColorText.ResumeCodes(state))
                ApplyChange(Sub()
                                TextColorInput.SelectedText = sb.ToString()
                                TextColorInput.SelectionLength = 0
                            End Sub)
            Else
                ApplyChange(Sub()
                                TextColorInput.Text = If(presetName Is Nothing,
                                    McColorText.ApplyGradient(raw, _GradStart, _GradEnd),
                                    McColorText.PresetGradient(presetName, raw))
                                TextColorInput.CaretIndex = TextColorInput.Text.Length
                            End Sub)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "应用渐变失败")
        End Try
    End Sub

    Private Sub BtnStyleBold_Click(sender As Object, e As RoutedEventArgs) Handles BtnStyleBold.Click
        ToggleStyle("l"c)
    End Sub

    Private Sub BtnStyleItalic_Click(sender As Object, e As RoutedEventArgs) Handles BtnStyleItalic.Click
        ToggleStyle("o"c)
    End Sub

    Private Sub BtnStyleUnderline_Click(sender As Object, e As RoutedEventArgs) Handles BtnStyleUnderline.Click
        ToggleStyle("n"c)
    End Sub

    Private Sub BtnStyleStrike_Click(sender As Object, e As RoutedEventArgs) Handles BtnStyleStrike.Click
        ToggleStyle("m"c)
    End Sub

    Private Sub BtnStyleObf_Click(sender As Object, e As RoutedEventArgs) Handles BtnStyleObf.Click
        ToggleStyle("k"c)
    End Sub

    ' ==================== 颜色 ====================

    Private Sub InitClassicColors()
        If _Swatches.Count > 0 Then Return
        For Each Info In McColorText.ClassicColors
            Dim Btn As New Button With {
                .Style = CType(Resources("SwatchBtn"), Style),
                .Background = New SolidColorBrush(Info.Color),
                .Tag = Info,
                .Margin = New Thickness(0, 0, 6, 6),
                .ToolTip = $"{Info.Name}（§{Info.Code}）"}
            AddHandler Btn.Click,
                Sub(s, e) ApplyColorToTarget(CType(CType(s, Button).Tag, McColorText.McColorInfo).Color)
            PanClassicColors.Children.Add(Btn)
            _Swatches.Add(Btn)
        Next
    End Sub

    ''' <summary>根据光标处状态刷新样式按钮激活态、颜色选中态与名称。</summary>
    Private Sub UpdateCaretState()
        If PanClassicColors Is Nothing OrElse LabColorName Is Nothing Then Return
        Dim state = McColorText.GetStateAt(TextColorInput.Text, TextColorInput.SelectionStart)
        SetToggle(BtnStyleBold, state.Bold)
        SetToggle(BtnStyleItalic, state.Italic)
        SetToggle(BtnStyleUnderline, state.Underline)
        SetToggle(BtnStyleStrike, state.Strike)
        SetToggle(BtnStyleObf, state.Obf)
        Dim matched As McColorText.McColorInfo = Nothing
        For Each Info In McColorText.ClassicColors
            If Info.Color.Equals(state.Color) Then
                matched = Info
                Exit For
            End If
        Next
        For Each Btn In _Swatches
            Dim Info = CType(Btn.Tag, McColorText.McColorInfo)
            If Info Is matched Then
                Btn.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "ColorBrush3")
                Btn.BorderThickness = New Thickness(2)
                Dim lum = (0.299 * Info.Color.R + 0.587 * Info.Color.G + 0.114 * Info.Color.B) / 255.0
                Btn.Content = New TextBlock With {
                    .Text = "✓", .FontSize = 11, .FontWeight = FontWeights.Bold,
                    .Foreground = If(lum > 0.6, System.Windows.Media.Brushes.Black, System.Windows.Media.Brushes.White)}
            Else
                Btn.ClearValue(System.Windows.Controls.Control.BorderBrushProperty)
                Btn.BorderThickness = New Thickness(1)
                Btn.Content = Nothing
            End If
        Next
        LabColorName.Text = If(matched IsNot Nothing,
            $"当前：{matched.Name}（§{matched.Code}）",
            $"当前：#{state.Color.R:X2}{state.Color.G:X2}{state.Color.B:X2}")
    End Sub

    ''' <summary>切换样式类按钮的激活视觉（激活 = 浅绿底 + 主色描边）。</summary>
    Private Sub SetToggle(btn As Button, active As Boolean)
        If active Then
            btn.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "BrushAccentSoft")
            btn.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "ColorBrush3")
            btn.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "ColorBrush1")
        Else
            btn.ClearValue(System.Windows.Controls.Control.BackgroundProperty)
            btn.ClearValue(System.Windows.Controls.Control.BorderBrushProperty)
            btn.ClearValue(System.Windows.Controls.Control.ForegroundProperty)
        End If
    End Sub

    Private Sub BtnPickColor_Click(sender As Object, e As RoutedEventArgs) Handles BtnPickColor.Click
        Dim c = PickColorDialog()
        If c.HasValue Then ApplyColorToTarget(c.Value)
    End Sub

    Private Sub TextHexInput_KeyDown(sender As Object, e As KeyEventArgs) Handles TextHexInput.KeyDown
        If e.Key = Key.Enter Then
            e.Handled = True
            ApplyHexInput()
        End If
    End Sub

    Private Sub ApplyHexInput()
        Dim c As Color
        If McColorText.TryParseHex(TextHexInput.Text, c) Then
            ApplyColorToTarget(c)
        ElseIf TextHexInput.Text.Trim() <> "" Then
            Hint("HEX 格式不正确，应为 #RRGGBB（如 #54A13D）", HintType.Red)
        End If
    End Sub

    Private Function PickColorDialog() As Nullable(Of Color)
        Try
            Using Dlg As New System.Windows.Forms.ColorDialog()
                Dlg.FullOpen = True
                If Dlg.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
                    Return Color.FromRgb(Dlg.Color.R, Dlg.Color.G, Dlg.Color.B)
                End If
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "打开取色器失败")
        End Try
        Return Nothing
    End Function

    ' ==================== 渐变 ====================

    Private Sub InitGradient()
        _BrushDuo = New LinearGradientBrush With {.StartPoint = New Point(0, 0), .EndPoint = New Point(1, 0)}
        _BrushDuo.GradientStops.Add(New GradientStop(_GradStart, 0))
        _BrushDuo.GradientStops.Add(New GradientStop(_GradEnd, 1))
        _BrushRainbow = New LinearGradientBrush With {.StartPoint = New Point(0, 0), .EndPoint = New Point(1, 0)}
        For Each P In {("#FF5C5C", 0.0), ("#FFE95C", 0.25), ("#6FDF6F", 0.5), ("#5CB8FF", 0.75), ("#B07CFF", 1.0)}
            Dim C As Color
            If McColorText.TryParseHex(P.Item1, C) Then _BrushRainbow.GradientStops.Add(New GradientStop(C, P.Item2))
        Next
        UpdateGradBar()
    End Sub

    Private Sub UpdateGradBar()
        If BdGradBar Is Nothing Then Return
        BdGradBar.Background = If(_RainbowBar, CType(_BrushRainbow, Brush), CType(_BrushDuo, Brush))
        If Not _RainbowBar Then
            _BrushDuo.GradientStops(0).Color = _GradStart
            _BrushDuo.GradientStops(1).Color = _GradEnd
        End If
    End Sub

    Private Sub UpdateGradSwatches()
        BtnGradStart.Background = New SolidColorBrush(_GradStart)
        BtnGradEnd.Background = New SolidColorBrush(_GradEnd)
    End Sub

    Private Sub ClearPresetChips()
        SetToggle(BtnPresetRainbow, False)
        SetToggle(BtnPresetIce, False)
        SetToggle(BtnPresetLava, False)
        SetToggle(BtnPresetPurple, False)
        SetToggle(BtnPresetGold, False)
    End Sub

    Private Sub SetPreset(name As String)
        Select Case name
            Case "彩虹"
                _RainbowBar = True
            Case "冰蓝"
                _RainbowBar = False : _GradStart = Color.FromRgb(120, 255, 255) : _GradEnd = Color.FromRgb(40, 90, 255)
            Case "熔岩"
                _RainbowBar = False : _GradStart = Color.FromRgb(255, 210, 60) : _GradEnd = Color.FromRgb(255, 40, 0)
            Case "紫色梦幻"
                _RainbowBar = False : _GradStart = Color.FromRgb(200, 90, 255) : _GradEnd = Color.FromRgb(70, 130, 255)
            Case "金色"
                _RainbowBar = False : _GradStart = Color.FromRgb(255, 240, 150) : _GradEnd = Color.FromRgb(255, 160, 20)
        End Select
        ClearPresetChips()
        SetToggle(If(name = "彩虹", BtnPresetRainbow,
                 If(name = "冰蓝", BtnPresetIce,
                 If(name = "熔岩", BtnPresetLava,
                 If(name = "紫色梦幻", BtnPresetPurple, BtnPresetGold)))), True)
        UpdateGradBar()
        UpdateGradSwatches()
        ApplyGradientToTarget(name)
    End Sub

    Private Sub BtnGradStart_Click(sender As Object, e As RoutedEventArgs) Handles BtnGradStart.Click
        Dim c = PickColorDialog()
        If c.HasValue Then
            _GradStart = c.Value
            _RainbowBar = False
            ClearPresetChips()
            UpdateGradBar()
            UpdateGradSwatches()
        End If
    End Sub

    Private Sub BtnGradEnd_Click(sender As Object, e As RoutedEventArgs) Handles BtnGradEnd.Click
        Dim c = PickColorDialog()
        If c.HasValue Then
            _GradEnd = c.Value
            _RainbowBar = False
            ClearPresetChips()
            UpdateGradBar()
            UpdateGradSwatches()
        End If
    End Sub

    Private Sub BtnApplyGrad_Click(sender As Object, e As RoutedEventArgs) Handles BtnApplyGrad.Click
        ApplyGradientToTarget(Nothing)
    End Sub

    Private Sub BtnPresetRainbow_Click(sender As Object, e As RoutedEventArgs) Handles BtnPresetRainbow.Click
        SetPreset("彩虹")
    End Sub

    Private Sub BtnPresetIce_Click(sender As Object, e As RoutedEventArgs) Handles BtnPresetIce.Click
        SetPreset("冰蓝")
    End Sub

    Private Sub BtnPresetLava_Click(sender As Object, e As RoutedEventArgs) Handles BtnPresetLava.Click
        SetPreset("熔岩")
    End Sub

    Private Sub BtnPresetPurple_Click(sender As Object, e As RoutedEventArgs) Handles BtnPresetPurple.Click
        SetPreset("紫色梦幻")
    End Sub

    Private Sub BtnPresetGold_Click(sender As Object, e As RoutedEventArgs) Handles BtnPresetGold.Click
        SetPreset("金色")
    End Sub

    ' ==================== 输入卡底部操作 ====================

    Private Sub BtnColorClear_Click(sender As Object, e As RoutedEventArgs) Handles BtnColorClear.Click
        ApplyChange(Sub()
                        TextColorInput.Text = ""
                        TextColorInput.CaretIndex = 0
                    End Sub)
    End Sub

    Private Sub BtnColorReset_Click(sender As Object, e As RoutedEventArgs) Handles BtnColorReset.Click
        ApplyChange(Sub()
                        Dim pos = TextColorInput.CaretIndex
                        TextColorInput.Text = McColorText.StripColorCodes(TextColorInput.Text)
                        TextColorInput.CaretIndex = Math.Min(pos, TextColorInput.Text.Length)
                    End Sub)
    End Sub

    Private Sub BtnInsertExample_Click(sender As Object, e As RoutedEventArgs) Handles BtnInsertExample.Click
        Dim demo = "§a欢迎来到 §6§l缘启服务器§r" & vbCrLf &
                   "§f本周六 §b§l建筑比赛§r§f 即将开始！§r" & vbCrLf &
                   "§x§F§F§D§2§7§8渐§x§F§F§A§8§5§A变§x§F§F§5§3§1§F文§x§F§F§2§8§0§0字 §d§o稀有限定"
        ApplyChange(Sub()
                        TextColorInput.Text = demo
                        TextColorInput.CaretIndex = demo.Length
                    End Sub)
    End Sub

    ' ==================== 场景与输出 ====================

    Private Sub SceneChanged(sender As Object, e As RoutedEventArgs) Handles RSceneChat.Checked, RSceneMotd.Checked, RSceneTitle.Checked
        If PanSceneChat Is Nothing Then Return
        PanSceneChat.Visibility = If(RSceneChat.IsChecked = True, Visibility.Visible, Visibility.Collapsed)
        PanSceneMotd.Visibility = If(RSceneMotd.IsChecked = True, Visibility.Visible, Visibility.Collapsed)
        PanSceneTitle.Visibility = If(RSceneTitle.IsChecked = True, Visibility.Visible, Visibility.Collapsed)
        RefreshAll()
    End Sub

    Private Sub FormatChanged(sender As Object, e As RoutedEventArgs) Handles RFmtSection.Checked, RFmtAmp.Checked, RFmtJson.Checked, RFmtMini.Checked
        If TextColorOutput Is Nothing Then Return
        If RFmtSection.IsChecked = True Then
            _Format = McColorText.McOutputFormat.Section
        ElseIf RFmtAmp.IsChecked = True Then
            _Format = McColorText.McOutputFormat.Amp
        ElseIf RFmtJson.IsChecked = True Then
            _Format = McColorText.McOutputFormat.Json
        Else
            _Format = McColorText.McOutputFormat.MiniMessage
        End If
        UpdateOutput(If(TextColorInput.Text, ""))
    End Sub

    ' ==================== 总刷新 ====================

    Private Sub RefreshAll()
        If TextColorOutput Is Nothing OrElse LabChatPreview Is Nothing Then Return
        Dim raw = If(TextColorInput.Text, "")
        UpdateStats(raw)
        UpdatePreview(raw)
        UpdateOutput(raw)
        UpdateCaretState()
    End Sub

    Private Sub UpdateStats(raw As String)
        Dim visible = McColorText.CountVisibleChars(raw)
        Dim over = visible > 256
        LabStats.Text = $"实际字符 {visible} / 256 · 含格式码 {raw.Length}"
        LabStats.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, If(over, "BrushDanger", "BrushText3"))
        LabHeadCount.Text = $"实际字符 {visible} / 256"
        DotHeadCount.Fill = If(over, TryFindResource("BrushDanger"), TryFindResource("ColorBrush3"))
    End Sub

    Private Sub UpdatePreview(raw As String)
        _ObfAll.Clear()
        FillScene(LabChatPreview, LabChatShadow, LabChatEmpty, raw, "输入文字开始生成…")
        FillScene(LabMotdPreview, LabMotdShadow, LabMotdEmpty, raw, "服务器 MotD 预览")
        FillScene(LabTitlePreview, LabTitleShadow, LabTitleEmpty, raw, "标题预览")
        _ObfTimer.IsEnabled = _ObfAll.Count > 0
    End Sub

    ''' <summary>填充一个场景的主层与阴影层（阴影层为 25% 亮度，右下偏移 2px）。</summary>
    Private Sub FillScene(main As TextBlock, shadow As TextBlock, empty As TextBlock, raw As String, emptyText As String)
        main.Inlines.Clear()
        shadow.Inlines.Clear()
        Dim hasText = False
        For Each P In McColorText.BuildRunPairs(raw)
            If P.Seg.Obfuscated Then _ObfAll.Add(P.Inline)
            main.Inlines.Add(P.Inline)
            If P.Seg.Text.Length > 0 Then hasText = True
        Next
        For Each P In McColorText.BuildRunPairs(raw, Nothing, True)
            If P.Seg.Obfuscated Then _ObfAll.Add(P.Inline)
            shadow.Inlines.Add(P.Inline)
        Next
        empty.Text = emptyText
        Dim vis = If(hasText, Visibility.Visible, Visibility.Collapsed)
        main.Visibility = vis
        shadow.Visibility = vis
        empty.Visibility = If(hasText, Visibility.Collapsed, Visibility.Visible)
    End Sub

    Private Sub ObfTimer_Tick(sender As Object, e As EventArgs)
        For Each R In _ObfAll
            R.Text = McColorText.RandomObfChars(R.Text.Length)
        Next
    End Sub

    Private Sub UpdateOutput(raw As String)
        Dim outText = McColorText.Serialize(raw, _Format)
        TextColorOutput.Text = outText
        LabOutStat.Text = $"输出 {outText.Length} 字符"
        Select Case _Format
            Case McColorText.McOutputFormat.Section
                LabFormatNote.Text = "提示 · §x 彩色码需要 BungeeCord / Spigot 系服务端支持；原版 1.16+ 完整支持请改用 JSON 格式。"
            Case McColorText.McOutputFormat.Amp
                LabFormatNote.Text = "提示 · 常用于 Bukkit / Spigot 插件配置（motd、插件消息），由插件转换为 § 码。"
            Case McColorText.McOutputFormat.Json
                LabFormatNote.Text = "提示 · 适用于 /tellraw、/title 与原版 1.16+ 客户端，完整支持自定义颜色。"
            Case McColorText.McOutputFormat.MiniMessage
                LabFormatNote.Text = "提示 · 适用于 Paper / Adventure 生态的 MiniMessage 格式。"
        End Select
    End Sub

    Private Sub CopyOutput()
        If TextColorOutput.Text <> "" Then ClipboardSet(TextColorOutput.Text)
    End Sub

    Private Sub BtnColorCopy_Click(sender As Object, e As RoutedEventArgs) Handles BtnColorCopy.Click, BtnCopyHeader.Click
        CopyOutput()
    End Sub

    ' ==================== 快捷键 ====================

    Private Sub TextColorInput_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles TextColorInput.PreviewKeyDown
        If Keyboard.Modifiers = ModifierKeys.Control Then
            Select Case e.Key
                Case Key.B : ToggleStyle("l"c) : e.Handled = True
                Case Key.I : ToggleStyle("o"c) : e.Handled = True
                Case Key.U : ToggleStyle("n"c) : e.Handled = True
                Case Key.S : ToggleStyle("m"c) : e.Handled = True
                Case Key.D : ToggleStyle("k"c) : e.Handled = True
                Case Key.Enter : CopyOutput() : e.Handled = True
                Case Key.Z : Undo() : e.Handled = True
                Case Key.Y : Redo() : e.Handled = True
            End Select
        End If
    End Sub

End Class
