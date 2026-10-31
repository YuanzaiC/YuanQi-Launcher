''' <summary>
''' 工具箱 · 指令查询：上半部分是「指令台」（输入指令、按参数逐步补全，候选来自当前实例的完整 ID 表），
''' 下半部分是指令列表（可搜索、按分类筛选，点一条就把它的固定前缀填进指令台）。
''' </summary>
Public Class PageCommandQuery

    ''' <summary>「全部」分类的显示名。</summary>
    Private Const CatAll As String = "全部"
    ''' <summary>候选面板一次最多渲染多少行（剩下的靠继续输入缩小范围）。</summary>
    Private Const CandLimit As Integer = 40
    ''' <summary>最近使用最多保留多少条。</summary>
    Private Const HistoryLimit As Integer = 40

    Private ReadOnly MonoFont As New FontFamily("Consolas")

    '=== 列表 ===
    Private CurCat As String = CatAll
    Private CurQuery As String = ""
    Private IsBuilt As Boolean = False
    Private ReadOnly Cards As New List(Of Border)
    Private ReadOnly CardCommand As New Dictionary(Of Border, McCommand)
    Private ReadOnly CatButtons As New List(Of Button)
    Private FilterTimer As Windows.Threading.DispatcherTimer = Nothing

    '=== 指令台 ===
    Private Ctx As FillContext = Nothing
    Private Cands As New List(Of FillCandidate)
    Private CandIndex As Integer = 0
    Private ReadOnly CandRows As New List(Of Border)
    Private SuppressConsole As Boolean = False
    Private ConsoleTimer As Windows.Threading.DispatcherTimer = Nothing
    Private ReadOnly History As New List(Of String)

    Private ReadOnly Property HistoryFile As String
        Get
            Return Paths.Base & "YQL\Cache\CommandHistory.json"
        End Get
    End Property

    Private Sub PageCommandQuery_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Not IsBuilt Then
            IsBuilt = True
            BuildChips()
            BuildCards()
            AddHandler PanSearch.TextChanged, Sub() QueueFilter()
            AddHandler TxtCmd.TextChanged, Sub() QueueConsole()
            AddHandler TxtCmd.PreviewKeyDown, AddressOf TxtCmd_PreviewKeyDown
            AddHandler BtnCopyCmd.Click, Sub() CopyCommand()
            AddHandler BtnClearCmd.Click, Sub() ClearConsole()
            ApplyChipVisual()
            LoadHistory()
            RenderRecent()
            UpdateArgHint()
            ModCommandRegistry.EnsureLoadedAsync(
                Sub()
                    Try
                        LabConsoleState.Text = $"输入指令，↑↓ 选候选，Tab / Enter 填入 · 候选表：{ModCommandRegistry.SourceText}"
                    Catch
                    End Try
                    UpdateArgHint()
                    '候选表就绪后，若用户已经在输入，补一次候选
                    If Not String.IsNullOrWhiteSpace(TxtCmd.Text) Then RefreshConsole()
                End Sub)
        End If
        ApplyFilter(False)
        RefreshConsole()
    End Sub

    '===============================
    '  进入动画
    '===============================

    ''' <summary>
    ''' 进入动画：指令台先浮现，随后搜索框、分类、前几张指令卡依次进场。
    ''' </summary>
    Public Overrides Sub TriggerEnterAnimation(ParamArray Elements As FrameworkElement())
        For Each El In Elements
            If El IsNot Nothing Then El.Visibility = Visibility.Visible
        Next
        Dim Anims As New List(Of AniData)
        Dim Delay As Integer = 0
        For Each Blk In {PanConsole, PanSearch, PanCats}
            If Blk Is Nothing Then Continue For
            Blk.Opacity = 0
            Blk.RenderTransform = New TranslateTransform(0, 14)
            Anims.Add(AaOpacity(Blk, 1, 220, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
            Anims.Add(AaTranslateY(Blk, -14, 320, Delay, New AniEaseOutFluent(AniEasePower.Middle)))
            Delay += 50
        Next
        Dim Idx As Integer = 0
        For Each Card In Cards
            If Card Is Nothing OrElse Card.Visibility <> Visibility.Visible Then Continue For
            If Idx < 8 Then
                Card.Opacity = 0
                Card.RenderTransform = New TranslateTransform(0, 14)
                Anims.Add(AaOpacity(Card, 1, 220, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
                Anims.Add(AaTranslateY(Card, -14, 320, Delay, New AniEaseOutFluent(AniEasePower.Middle)))
                Delay += 26
            Else
                Card.Opacity = 1
                Card.RenderTransform = Nothing
            End If
            Idx += 1
        Next
        AniStart(Anims.ToArray(), "CommandQuery Enter")
    End Sub

    '===============================
    '  指令台：输入与候选
    '===============================

    Private Sub QueueConsole()
        If SuppressConsole Then Return
        If ConsoleTimer Is Nothing Then
            ConsoleTimer = New Windows.Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 0, 0, 90)}
            AddHandler ConsoleTimer.Tick,
                Sub(Sender As Object, E As EventArgs)
                    ConsoleTimer.Stop()
                    RefreshConsole()
                End Sub
        End If
        ConsoleTimer.Stop()
        ConsoleTimer.Start()
    End Sub

    Private Sub RefreshConsole()
        Try
            Ctx = ModCommandFill.Locate(TxtCmd.Text)
            Dim Typed As String = If(Ctx Is Nothing, "", If(Ctx.Typed, ""))
            '空输入时不弹候选，避免一进页面就铺满一大片列表
            If String.IsNullOrWhiteSpace(TxtCmd.Text) Then
                Cands.Clear()
            Else
                Cands = ModCommandFill.GetCandidates(Ctx, Typed)
            End If
            If CandIndex >= Cands.Count Then CandIndex = Math.Max(0, Cands.Count - 1)
            If CandIndex < 0 Then CandIndex = 0
            RenderCandidates()
            UpdateArgHint()
        Catch ex As Exception
            Logger.Warn(ex, "刷新指令台候选失败")
        End Try
    End Sub

    Private Sub UpdateArgHint()
        If String.IsNullOrWhiteSpace(TxtCmd.Text) Then
            LabArgHint.Text = "先输入指令名，例如 give、tp、time；也可以点下面任意一条指令把前缀填进来。"
            Return
        End If
        If Ctx Is Nothing OrElse Ctx.Command Is Nothing Then
            LabArgHint.Text = "还没有匹配到指令，继续输入，或从下面列表里点一条。"
            Return
        End If
        Dim Warn As String = ModCommandFill.Validate(TxtCmd.Text, Ctx)
        Dim Head As String
        If Ctx.SlotIndex < 0 Then
            Head = $"「{Ctx.Command.Name}」的参数已经填完了，可以直接复制。"
        ElseIf Ctx.InSelector AndAlso Ctx.Kind = FillKind.SelectorKey Then
            Head = "选择要加的选择器条件（type= / distance= / limit= / gamemode= …）"
        ElseIf Ctx.InSelector Then
            Head = $"正在填选择器条件「{Ctx.ArgName}」的值；填完可以选「结束」闭合，或选「继续加条件」"
        ElseIf Ctx.Kind = FillKind.Nbt Then
            Select Case Ctx.NbtStep
                Case NbtStep.Key
                    Head = If(Ctx.NbtComponent, "选择要添加的组件（[] 组件写法）", "选择要添加的属性（{} 旧版写法）")
                Case NbtStep.EnchId
                    Head = "选择附魔 —— 选完还能接着选等级"
                Case NbtStep.EnchLevel
                    Head = "选择附魔等级（1 = I 级，2 = II 级…）"
                Case NbtStep.More
                    Head = "选「继续添加」接着加下一个，或选「结束」把括号补齐"
                Case NbtStep.Close
                    Head = "补上右花括号收尾"
                Case Else
                    Head = "NBT：也可以自己接着写"
            End Select
        ElseIf Ctx.Kind = FillKind.Literal Then
            Dim Options As String = ""
            For Each Cand In Cands
                If Options.Length < 60 Then Options &= If(Options.Length = 0, "", " / ") & Cand.Value
            Next
            Head = $"「{Ctx.Command.Name}」下一步要选：{Options}"
        Else
            Dim Desc As String = ""
            Ctx.Command.Args.TryGetValue(Ctx.ArgName, Desc)
            If String.IsNullOrWhiteSpace(Desc) Then Desc = "没有更多说明"
            Head = $"现在该填：{Ctx.ArgName} —— {Desc}"
        End If
        If Warn.Length > 0 Then Head = "⚠ " & Warn & "　" & Head
        LabArgHint.Text = Head
    End Sub

    Private Sub RenderCandidates()
        Dim WasOpen As Boolean = PanCand.Visibility = Visibility.Visible
        PanCandList.Children.Clear()
        CandRows.Clear()
        If Cands.Count = 0 Then
            PanCand.Visibility = Visibility.Collapsed
            Return
        End If
        Dim Show As Integer = Math.Min(Cands.Count, CandLimit)
        For I As Integer = 0 To Show - 1
            Dim Cand As FillCandidate = Cands(I)
            Dim Row As New Border With {.Style = TryCast(FindResource("CandRow"), Style)}
            Dim G As New Grid()
            G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            G.Children.Add(New TextBlock With {
                .Text = Cand.Title,
                .FontSize = 12.5,
                .Foreground = GetBrush("BrushText1"),
                .TextTrimming = TextTrimming.CharacterEllipsis,
                .VerticalAlignment = VerticalAlignment.Center
            })
            If Not String.IsNullOrEmpty(Cand.SubText) Then
                Dim Sub2 As New TextBlock With {
                    .Text = Cand.SubText,
                    .FontSize = 11,
                    .Foreground = GetBrush("BrushText3"),
                    .Margin = New Thickness(14, 0, 0, 0),
                    .TextTrimming = TextTrimming.CharacterEllipsis,
                    .VerticalAlignment = VerticalAlignment.Center
                }
                Grid.SetColumn(Sub2, 1)
                G.Children.Add(Sub2)
            End If
            Row.Child = G
            Dim Idx As Integer = I
            AddHandler Row.MouseLeftButtonUp,
                Sub(Sender As Object, E As MouseButtonEventArgs)
                    CandIndex = Idx
                    ApplyCandidate(Idx)
                End Sub
            AddHandler Row.MouseEnter,
                Sub()
                    CandIndex = Idx
                    HighlightCandidates()
                End Sub
            CandRows.Add(Row)
            PanCandList.Children.Add(Row)
        Next
        If Cands.Count > Show Then
            LabCandHead.Text = $"候选 {Cands.Count} 项 · 显示前 {Show} 项，继续输入可缩小范围"
        Else
            LabCandHead.Text = $"候选 {Cands.Count} 项"
        End If
        PanCand.Visibility = Visibility.Visible
        HighlightCandidates()
        If Not WasOpen Then
            PanCand.Opacity = 0
            PanCand.RenderTransform = New TranslateTransform(0, -6)
            AniStart({
                AaOpacity(PanCand, 1, 150, , New AniEaseOutFluent(AniEasePower.Weak)),
                AaTranslateY(PanCand, 6, 190, , New AniEaseOutFluent(AniEasePower.Middle))
            }, "CommandQuery Cand")
        Else
            PanCand.Opacity = 1
            PanCand.RenderTransform = Nothing
        End If
    End Sub

    Private Sub HighlightCandidates()
        For I As Integer = 0 To CandRows.Count - 1
            Dim Row As Border = CandRows(I)
            If I = CandIndex Then
                Row.Background = GetBrush("BrushAccentSoft")
            Else
                Row.Background = Brushes.Transparent
            End If
        Next
    End Sub

    ''' <summary>键盘选择时把选中行滚进可见范围。</summary>
    Private Sub EnsureCandidateVisible()
        If CandIndex < 0 OrElse CandIndex >= CandRows.Count Then Return
        Try
            Dim Row As Border = CandRows(CandIndex)
            Dim Y As Double = Row.TransformToAncestor(PanCandList).Transform(New Point(0, 0)).Y
            Dim H As Double = Row.ActualHeight
            Dim Top As Double = PanCandScroll.VerticalOffset
            Dim ViewH As Double = PanCandScroll.ViewportHeight
            If Y < Top Then
                PanCandScroll.ScrollToVerticalOffset(Math.Max(0, Y - 4))
            ElseIf Y + H > Top + ViewH Then
                PanCandScroll.ScrollToVerticalOffset(Y + H - ViewH + 4)
            End If
        Catch
        End Try
    End Sub

    Private Sub ApplyCandidate(Idx As Integer)
        If Ctx Is Nothing OrElse Idx < 0 OrElse Idx >= Cands.Count Then Return
        Dim Cand As FillCandidate = Cands(Idx)
        '「结束」「继续加条件」这类结构性候选只追加，不能覆盖已经填好的值
        Dim NewText As String = ModCommandFill.ApplyCandidate(Ctx, Cand.Value, Cand.AppendOnly)
        SuppressConsole = True
        TxtCmd.Text = NewText
        SuppressConsole = False
        TxtCmd.CaretIndex = TxtCmd.Text.Length
        TxtCmd.Focus()
        CandIndex = 0
        RefreshConsole()
    End Sub

    Private Sub TxtCmd_PreviewKeyDown(Sender As Object, E As KeyEventArgs)
        Try
            If PanCand.Visibility = Visibility.Visible AndAlso Cands.Count > 0 Then
                Select Case E.Key
                    Case Key.Down
                        CandIndex = Math.Min(Cands.Count - 1, CandIndex + 1)
                        HighlightCandidates()
                        EnsureCandidateVisible()
                        E.Handled = True
                        Return
                    Case Key.Up
                        CandIndex = Math.Max(0, CandIndex - 1)
                        HighlightCandidates()
                        EnsureCandidateVisible()
                        E.Handled = True
                        Return
                    Case Key.Tab, Key.Enter
                        ApplyCandidate(CandIndex)
                        E.Handled = True
                        Return
                    Case Key.Escape
                        PanCand.Visibility = Visibility.Collapsed
                        E.Handled = True
                        Return
                End Select
            End If
            If E.Key = Key.Enter Then
                Dim Text As String = GetFullCommand()
                If Text.Length > 1 Then
                    PushHistory(Text)
                    LabConsoleState.Text = "已记入最近使用"
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "指令台按键处理失败")
        End Try
    End Sub

    Private Function GetFullCommand() As String
        Dim Text As String = If(TxtCmd.Text, "").Trim()
        If Text.Length = 0 Then Return ""
        Return "/" & Text
    End Function

    Private Sub CopyCommand()
        Dim Text As String = GetFullCommand()
        If Text.Length = 0 Then Return
        ClipboardSet(Text)
        PushHistory(Text)
    End Sub

    Private Sub ClearConsole()
        SuppressConsole = True
        TxtCmd.Text = ""
        SuppressConsole = False
        TxtCmd.Focus()
        CandIndex = 0
        PanCand.Visibility = Visibility.Collapsed
        RefreshConsole()
    End Sub

    ''' <summary>点指令卡片：把它的固定前缀填进指令台，并立刻给出第一个参数的候选。</summary>
    Private Sub FillConsoleFromCommand(Cmd As McCommand)
        If Cmd Is Nothing Then Return
        SuppressConsole = True
        TxtCmd.Text = Cmd.Name & " "
        SuppressConsole = False
        TxtCmd.CaretIndex = TxtCmd.Text.Length
        TxtCmd.Focus()
        CandIndex = 0
        RefreshConsole()
        '指令台卡片闪一下，提示内容填到哪儿了
        PanInput.BorderBrush = GetBrush("ColorBrush4")
        AniStart(AaCode(Sub()
                            Try
                                PanInput.ClearValue(Border.BorderBrushProperty)
                            Catch
                            End Try
                        End Sub, 420), "CommandQuery Fill Flash")
    End Sub

    '===============================
    '  最近使用
    '===============================

    Private Sub LoadHistory()
        Try
            RunInNewThread(
            Sub()
                Try
                    If Not FileUtils.Exists(HistoryFile) Then Return
                    Dim Json = FileUtils.ReadAsString(HistoryFile).DeserializeJson()
                    If Json Is Nothing Then Return
                    Dim Arr = Json("Commands")
                    If Arr Is Nothing Then Return
                    Dim Loaded As New List(Of String)
                    For Each Node In Arr
                        Dim S As String = Node.ToString()
                        If Not String.IsNullOrWhiteSpace(S) Then Loaded.Add(S)
                    Next
                    RunInUi(
                    Sub()
                        History.Clear()
                        For Each S In Loaded
                            If History.Count >= HistoryLimit Then Exit For
                            If Not History.Contains(S) Then History.Add(S)
                        Next
                        RenderRecent()
                    End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "读取指令历史失败")
                End Try
            End Sub, "Command History Load")
        Catch ex As Exception
            Logger.Warn(ex, "读取指令历史失败")
        End Try
    End Sub

    Private Sub SaveHistory()
        Try
            Dim Snapshot As New List(Of String)(History)
            RunInNewThread(
            Sub()
                Try
                    DirectoryUtils.Create(IO.Path.GetDirectoryName(HistoryFile))
                    Dim Arr As New JArray()
                    For Each S In Snapshot
                        Arr.Add(S)
                    Next
                    Dim Json As New JObject From {{"Commands", Arr}}
                    FileUtils.Write(HistoryFile, Json.ToString(Newtonsoft.Json.Formatting.None))
                Catch ex As Exception
                    Logger.Warn(ex, "保存指令历史失败")
                End Try
            End Sub, "Command History Save")
        Catch ex As Exception
            Logger.Warn(ex, "保存指令历史失败")
        End Try
    End Sub

    Private Sub PushHistory(Text As String)
        If String.IsNullOrWhiteSpace(Text) Then Return
        History.Remove(Text)
        History.Insert(0, Text)
        While History.Count > HistoryLimit
            History.RemoveAt(History.Count - 1)
        End While
        RenderRecent()
        SaveHistory()
    End Sub

    Private Sub RenderRecent()
        If PanRecent Is Nothing Then Return
        PanRecent.Children.Clear()
        If History.Count = 0 Then
            PanRecent.Children.Add(New TextBlock With {
                .Text = "最近使用：还没有记录",
                .FontSize = 11,
                .Foreground = GetBrush("BrushText3"),
                .VerticalAlignment = VerticalAlignment.Center
            })
            Return
        End If
        PanRecent.Children.Add(New TextBlock With {
            .Text = "最近使用",
            .FontSize = 11,
            .Foreground = GetBrush("BrushText3"),
            .Margin = New Thickness(0, 0, 8, 0),
            .VerticalAlignment = VerticalAlignment.Center
        })
        Dim Count As Integer = 0
        For Each S In History
            If Count >= 6 Then Exit For
            Dim Btn As New MyTextButton With {
                .Content = TrimCommandForChip(S),
                .FontSize = 11,
                .Margin = New Thickness(0, 0, 12, 0),
                .VerticalAlignment = VerticalAlignment.Center,
                .Cursor = Cursors.Hand
            }
            ToolTipService.SetToolTip(Btn, S)
            Dim Text As String = S
            AddHandler Btn.Click,
                Sub()
                    SuppressConsole = True
                    TxtCmd.Text = Text.TrimStart("/"c)
                    SuppressConsole = False
                    TxtCmd.CaretIndex = TxtCmd.Text.Length
                    TxtCmd.Focus()
                    CandIndex = 0
                    RefreshConsole()
                End Sub
            PanRecent.Children.Add(Btn)
            Count += 1
        Next
    End Sub

    Private Shared Function TrimCommandForChip(Text As String) As String
        If Text.Length <= 26 Then Return Text
        Return Text.Substring(0, 24) & "…"
    End Function

    '===============================
    '  分类胶囊
    '===============================

    Private Sub BuildChips()
        For Each Cat In ModCommand.Categories
            Dim B As New Button With {
                .Style = TryCast(FindResource("CatChip"), Style),
                .Content = Cat,
                .Tag = Cat
            }
            If CatButtons.Count = 0 Then B.Margin = New Thickness(0, 6, 0, 0)
            AddHandler B.Click,
                Sub(Sender As Object, E As RoutedEventArgs)
                    If CurCat = Cat Then Return
                    CurCat = Cat
                    ApplyChipVisual()
                    ApplyFilter(True)
                End Sub
            CatButtons.Add(B)
            PanCats.Children.Add(B)
        Next
    End Sub

    Private Sub ApplyChipVisual()
        For I As Integer = 0 To CatButtons.Count - 1
            Dim B As Button = CatButtons(I)
            If CStr(B.Tag) = CurCat Then
                B.Background = GetBrush("BrushAccentSoft")
                B.Foreground = GetBrush("ColorBrush3")
            Else
                B.Background = Brushes.Transparent
                B.Foreground = GetBrush("BrushText2")
            End If
        Next
    End Sub

    '===============================
    '  指令卡片
    '===============================

    Private Sub BuildCards()
        For Each C In ModCommand.Commands
            Dim Card As Border = BuildCard(C)
            CardCommand(Card) = C
            Cards.Add(Card)
            PanList.Children.Add(Card)
        Next
    End Sub

    Private Function BuildCard(C As McCommand) As Border
        Dim Card As New Border With {.Style = TryCast(FindResource("CmdCard"), Style), .Cursor = Cursors.Hand}
        Dim Pan As New StackPanel()

        Dim Head As New Grid()
        Dim PanName As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        PanName.Children.Add(New TextBlock With {
            .Text = "/" & C.Name,
            .FontFamily = MonoFont,
            .FontSize = 14.5,
            .FontWeight = FontWeights.SemiBold,
            .Foreground = GetBrush("BrushText1"),
            .VerticalAlignment = VerticalAlignment.Center
        })
        Dim CatTag As New Border With {
            .Background = GetBrush("BrushSurface2"),
            .BorderBrush = GetBrush("BrushBorder"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(4),
            .Padding = New Thickness(6, 1, 6, 2),
            .Margin = New Thickness(9, 0, 0, 0),
            .VerticalAlignment = VerticalAlignment.Center
        }
        CatTag.Child = New TextBlock With {
            .Text = C.Category,
            .FontSize = 10.5,
            .Foreground = GetBrush("BrushText3"),
            .VerticalAlignment = VerticalAlignment.Center
        }
        PanName.Children.Add(CatTag)
        Head.Children.Add(PanName)
        Head.Children.Add(New TextBlock With {
            .Text = ModCommand.LevelText(C.Level),
            .FontSize = 11,
            .Foreground = GetBrush("BrushText3"),
            .HorizontalAlignment = HorizontalAlignment.Right,
            .VerticalAlignment = VerticalAlignment.Center
        })
        Pan.Children.Add(Head)

        For Each Line In C.Syntaxes
            Pan.Children.Add(BuildSyntaxLine(Line, C))
        Next

        Pan.Children.Add(New TextBlock With {
            .Text = C.Description,
            .FontSize = 12.5,
            .Foreground = GetBrush("BrushText2"),
            .TextWrapping = TextWrapping.Wrap,
            .Margin = New Thickness(0, 9, 0, 0)
        })

        If Not String.IsNullOrWhiteSpace(C.Example) Then
            Dim Foot As New Grid With {.Margin = New Thickness(0, 8, 0, 0)}
            Foot.Children.Add(New TextBlock With {
                .Text = "示例  " & C.Example,
                .FontFamily = MonoFont,
                .FontSize = 11.5,
                .Foreground = GetBrush("BrushText3"),
                .TextTrimming = TextTrimming.CharacterEllipsis,
                .VerticalAlignment = VerticalAlignment.Center,
                .Margin = New Thickness(0, 0, 64, 0)
            })
            Dim BtnCopy As New MyTextButton With {
                .Content = "复制",
                .FontSize = 11.5,
                .HorizontalAlignment = HorizontalAlignment.Right,
                .VerticalAlignment = VerticalAlignment.Center,
                .Cursor = Cursors.Hand
            }
            ToolTipService.SetToolTip(BtnCopy, "把示例复制到剪贴板")
            AddHandler BtnCopy.Click, Sub() ClipboardSet(C.Example, False)
            Foot.Children.Add(BtnCopy)
            Pan.Children.Add(Foot)
        End If

        Card.Child = Pan
        AddHandler Card.MouseLeftButtonUp,
            Sub(Sender As Object, E As MouseButtonEventArgs)
                '点卡片 = 把固定前缀填进指令台
                FillConsoleFromCommand(C)
            End Sub
        Return Card
    End Function

    ''' <summary>语法行：字面量用等宽字体，参数是灰色小框 + 悬停说明。</summary>
    Private Function BuildSyntaxLine(Syntax As String, C As McCommand) As WrapPanel
        Dim Wrap As New WrapPanel With {
            .Margin = New Thickness(0, 7, 0, 0),
            .VerticalAlignment = VerticalAlignment.Center
        }
        For Each Slot In ModCommandFill.CompileSyntax(Syntax)
            If Slot.IsArg Then
                Dim Desc As String = ""
                C.Args.TryGetValue(Slot.Text, Desc)
                If String.IsNullOrWhiteSpace(Desc) Then Desc = Slot.Text
                Dim Box As New Border With {
                    .Background = GetBrush("BrushHover"),
                    .CornerRadius = New CornerRadius(4),
                    .Padding = New Thickness(6, 2, 6, 3),
                    .Margin = New Thickness(0, 0, 4, 2),
                    .VerticalAlignment = VerticalAlignment.Center
                }
                Box.Child = New TextBlock With {
                    .Text = Slot.Text,
                    .FontSize = 11.5,
                    .Foreground = GetBrush("BrushText2"),
                    .VerticalAlignment = VerticalAlignment.Center
                }
                ToolTipService.SetToolTip(Box, Slot.Text & "：" & Desc)
                Wrap.Children.Add(Box)
            Else
                Wrap.Children.Add(New TextBlock With {
                    .Text = Slot.Text,
                    .FontFamily = MonoFont,
                    .FontSize = 12.5,
                    .Foreground = GetBrush("BrushText2"),
                    .Margin = New Thickness(0, 0, 4, 0),
                    .VerticalAlignment = VerticalAlignment.Center
                })
            End If
        Next
        Return Wrap
    End Function

    '===============================
    '  列表搜索与过滤
    '===============================

    Private Sub QueueFilter()
        If FilterTimer Is Nothing Then
            FilterTimer = New Windows.Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 0, 0, 120)}
            AddHandler FilterTimer.Tick,
                Sub(Sender As Object, E As EventArgs)
                    FilterTimer.Stop()
                    CurQuery = If(PanSearch.Text, "").Trim()
                    ApplyFilter(True)
                End Sub
        End If
        FilterTimer.Stop()
        FilterTimer.Start()
    End Sub

    Private Sub ApplyFilter(Animate As Boolean)
        Dim Q As String = Normalize(CurQuery)
        Dim Shown As Integer = 0
        Dim Newly As New List(Of Border)
        For Each Card In Cards
            Dim C As McCommand = Nothing
            If Not CardCommand.TryGetValue(Card, C) Then Continue For
            Dim Hit As Boolean = MatchCommand(C, Q)
            Dim Want As Visibility = If(Hit, Visibility.Visible, Visibility.Collapsed)
            If Card.Visibility <> Want Then
                Card.Visibility = Want
                If Want = Visibility.Visible Then Newly.Add(Card)
            End If
            If Hit Then Shown += 1
        Next
        If Animate AndAlso Newly.Count > 0 Then
            Dim Anims As New List(Of AniData)
            Dim Delay As Integer = 0
            For I As Integer = 0 To Newly.Count - 1
                Dim Card As Border = Newly(I)
                If I < 8 Then
                    Card.Opacity = 0
                    Card.RenderTransform = New TranslateTransform(0, 10)
                    Anims.Add(AaOpacity(Card, 1, 190, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
                    Anims.Add(AaTranslateY(Card, -10, 280, Delay, New AniEaseOutFluent(AniEasePower.Middle)))
                    Delay += 22
                Else
                    Card.Opacity = 1
                    Card.RenderTransform = Nothing
                End If
            Next
            If Anims.Count > 0 Then AniStart(Anims.ToArray(), "CommandQuery Filter")
        End If
        LabEmpty.Visibility = If(Shown = 0, Visibility.Visible, Visibility.Collapsed)
        If Animate Then PanBack.ScrollToTop()
    End Sub

    Private Shared Function Normalize(Text As String) As String
        If String.IsNullOrWhiteSpace(Text) Then Return ""
        Dim S As String = Text.Trim()
        If S.StartsWith("/"c) Then S = S.Substring(1)
        Return S.ToLowerInvariant()
    End Function

    Private Function MatchCommand(C As McCommand, Q As String) As Boolean
        If CurCat <> CatAll AndAlso C.Category <> CurCat Then Return False
        If Q.Length = 0 Then Return True
        If Contains(C.Name, Q) Then Return True
        If Contains(C.Keywords, Q) Then Return True
        If Contains(C.Description, Q) Then Return True
        If Contains(C.Category, Q) Then Return True
        If Contains(C.Example, Q) Then Return True
        For Each Line In C.Syntaxes
            If Contains(Line, Q) Then Return True
        Next
        For Each A As KeyValuePair(Of String, String) In C.Args
            If Contains(A.Key, Q) Then Return True
            If Contains(A.Value, Q) Then Return True
        Next
        Return False
    End Function

    Private Shared Function Contains(Source As String, Q As String) As Boolean
        If String.IsNullOrEmpty(Source) Then Return False
        Return Source.IndexOf(Q, StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Function GetBrush(Key As String) As Brush
        Return TryCast(FindResource(Key), Brush)
    End Function

End Class
