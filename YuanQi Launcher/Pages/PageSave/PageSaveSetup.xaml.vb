Public Class PageSaveSetup

    Private SavePath As String = ""
    Private SaveName As String = ""
    Private Inst As McInstance = Nothing
    Private SavesRoot As String = ""
    Private InvEnderMode As Boolean = False
    Private CoverZoomed As Boolean = False
    Private Players As List(Of McPlayerInfo)

    Private TabButtons As Button()
    Private TabPanels As FrameworkElement()
    Private CurrentTab As Integer = 0

    Public Sub Load(Instance As McInstance, SaveFolder As String, Folder As String)
        Inst = Instance
        SavePath = Folder
        SaveName = SaveFolder
        SavesRoot = IO.Path.GetDirectoryName(Folder.TrimEnd("\"c))
        TabButtons = {BtnTab0, BtnTab1, BtnTab2, BtnTab3, BtnTab4, BtnTab5}
        TabPanels = {PanOverview, PanWorld, PanPlayer, PanInventory, PanSnapshot, PanData}
        LabName.Text = SaveFolder
        Try
            Dim IconPath = Folder.TrimEnd("\"c) & "\icon.png"
            If IO.File.Exists(IconPath) Then ImgIcon.Source = IconPath Else ImgIcon.Source = PathImage & "Blocks/Grass.png"
        Catch
        End Try
        SwitchTab(0, False)
        RefreshAll()
    End Sub

    ' ===== Tab 切换 =====

    Private Sub SwitchTab(Index As Integer, Optional Animate As Boolean = True)
        If TabButtons Is Nothing OrElse TabPanels Is Nothing Then Return
        If Index = CurrentTab Then Return
        Dim OldIndex = CurrentTab
        For I = 0 To TabButtons.Length - 1
            TabButtons(I).Style = CType(FindResource(If(I = Index, "NavTabSel", "NavTab")), Style)
        Next
        CurrentTab = Index
        If Not Animate Then
            For I = 0 To TabPanels.Length - 1
                TabPanels(I).Visibility = If(I = Index, Visibility.Visible, Visibility.Collapsed)
                TabPanels(I).Opacity = If(I = Index, 1, 0)
                TabPanels(I).RenderTransform = New TranslateTransform(0, 0)
            Next
            Return
        End If
        AnimateTabSwitch(OldIndex, Index)
    End Sub

    Private Sub AnimateTabSwitch(OldIndex As Integer, NewIndex As Integer)
        Dim OutPanel = TabPanels(OldIndex)
        Dim InPanel = TabPanels(NewIndex)
        Dim Forward = NewIndex > OldIndex
        Dim OutTarget = If(Forward, -60.0, 60.0)
        Dim InStart = If(Forward, 60.0, -60.0)
        If TypeOf OutPanel.RenderTransform IsNot TranslateTransform Then OutPanel.RenderTransform = New TranslateTransform(0, 0)
        InPanel.RenderTransform = New TranslateTransform(InStart, 0)
        InPanel.Opacity = 0
        InPanel.Visibility = Visibility.Visible
        Dim OutX = CType(OutPanel.RenderTransform, TranslateTransform).X
        AniStart({
            AaOpacity(OutPanel, -OutPanel.Opacity, 70, 10),
            AaTranslateX(OutPanel, OutTarget - OutX, 90, 10),
            AaCode(Sub() OutPanel.Visibility = Visibility.Collapsed, After:=True),
            AaOpacity(InPanel, 1 - InPanel.Opacity, 70, 100),
            AaTranslateX(InPanel, -InStart, 160, 100, Ease:=New AniEaseOutFluent(AniEasePower.ExtraStrong))
        }, "PageSaveSetup TabSwitch " & PageUuid, True)
    End Sub

    Private Sub BtnTab_Click(sender As Object, e As RoutedEventArgs) Handles BtnTab0.Click, BtnTab1.Click, BtnTab2.Click, BtnTab3.Click, BtnTab4.Click, BtnTab5.Click
        Dim Index = Array.IndexOf(TabButtons, CType(sender, Button))
        If Index >= 0 Then SwitchTab(Index)
    End Sub

    ' ===== 页面进出动画（整页水平滑动，与启动器风格一致） =====

    Public Overrides Sub TriggerEnterAnimation(ParamArray Elements As FrameworkElement())
        Dim RealElements = Elements.Where(Function(e) e IsNot Nothing)
        Dim AniList As New List(Of AniData)
        For Each Element In RealElements
            Element.Visibility = Visibility.Visible
            Element.Opacity = 0
            Element.RenderTransform = New TranslateTransform(80, 0)
            AniList.Add(AaOpacity(Element, 1, 160, 0, New AniEaseOutFluent(AniEasePower.Weak)))
            AniList.Add(AaTranslateX(Element, -80, 280, 0, New AniEaseOutFluent(AniEasePower.Strong)))
        Next
        AniList.Add(AaCode(Sub() PageOnEnterAnimationFinished(),, True))
        AniStart(AniList, "PageRight PageChange " & PageUuid, True)
    End Sub

    Public Overrides Sub TriggerExitAnimation(ParamArray Elements As FrameworkElement())
        Dim RealElements = Elements.Where(Function(e) e IsNot Nothing)
        Dim AniList As New List(Of AniData)
        For Each Element In RealElements
            If TypeOf Element.RenderTransform IsNot TranslateTransform Then Element.RenderTransform = New TranslateTransform(0, 0)
            Dim X = CType(Element.RenderTransform, TranslateTransform).X
            AniList.Add(AaOpacity(Element, -Element.Opacity, 140, 0))
            AniList.Add(AaTranslateX(Element, 80 - X, 200, 0, New AniEaseInFluent(AniEasePower.Weak)))
        Next
        AniList.Add(AaCode(
        Sub()
            For Each Element In RealElements
                Element.Visibility = Visibility.Collapsed
            Next
            PageOnExitAnimationFinished()
        End Sub,, True))
        AniStart(AniList, "PageRight PageChange " & PageUuid)
    End Sub

    ' ===== 数据加载 =====

    Private Sub RefreshAll()
        RunInNewThread(
        Sub()
            Try
                Dim Info = ModSave.LoadSaveInfo(SavePath)
                Dim Players = ModSave.LoadPlayers(SavePath, If(Inst IsNot Nothing, Inst.PathIndie, ""))
                Dim Size = ModSave.GetFolderSize(SavePath)
                ' 将耗时的文件 IO（备份列表 / 物品）也放到后台线程，避免阻塞进场动画
                Dim Backups = ModSave.ListBackups(SavePath)
                Dim BackupSizes As New Dictionary(Of String, Long)
                For Each B In Backups
                    Try : BackupSizes(B) = ModSave.GetFolderSize(B) : Catch : End Try
                Next
                Dim InvItems = ModSave.LoadInventory(SavePath, InvEnderMode)
                RunInUi(Sub() FillInfo(Info, Players, Size, Backups, BackupSizes, InvItems))
            Catch ex As Exception
                Logger.Error(ex, "加载存档信息失败")
            End Try
        End Sub, "Save Info Load")
    End Sub

    Private Sub FillInfo(Info As McSaveInfo, Players As List(Of McPlayerInfo), Size As Long, Backups As List(Of String), BackupSizes As Dictionary(Of String, Long), InvItems As List(Of ModSave.McItemStack))
        ' Hero
        SetColoredName(LabWorldName, Info.LevelName, "未命名世界")
        LabVersion.Text = If(String.IsNullOrWhiteSpace(Info.GameVersion), "未知版本", Info.GameVersion)
        ' 概览
        LabSeed.Text = If(Info.HasSeed, Info.Seed.ToString, "不可用")
        BtnCopySeed.IsEnabled = Info.HasSeed
        LabVersionFact.Text = If(String.IsNullOrWhiteSpace(Info.GameVersion), "未知", Info.GameVersion)
        LabLastPlayed.Text = If(Info.LastPlayed > 0, (New Date(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(Info.LastPlayed).ToLocalTime).ToString("yyyy/M/d HH:mm"), "—")
        LabSize.Text = FormatSize(Size)
        LabPath.Text = Info.SavePath
        ' 玩家（支持联机多人选择）
        Me.Players = Players
        ComboPlayer.Items.Clear()
        For Each P In Players
            ComboPlayer.Items.Add(New MyComboBoxItem With {.Content = If(String.IsNullOrWhiteSpace(P.Name), P.Uuid, P.Name)})
        Next
        If Players.Count > 0 Then
            ComboPlayer.SelectedIndex = 0
            RefreshPlayerFields(Players(0))
        Else
            ComboPlayer.SelectedIndex = -1
            LabPlayerNameFact.Text = "不可用"
            LabDimensionFact.Text = "不可用"
            LabPlayerName2.Text = "不可用"
            LabDimension2.Text = "不可用"
            TextPlayerX.Text = ""
            TextPlayerY.Text = ""
            TextPlayerZ.Text = ""
            TextPlayerXp.Text = ""
        End If
        ' 世界
        ComboGameType.SelectedIndex = Math.Max(0, Math.Min(3, Info.GameType))
        ComboDifficulty.SelectedIndex = Math.Max(0, Math.Min(3, Info.Difficulty))
        CheckCheats.Checked = Info.AllowCommands
        CheckRuleKeepInventory.Checked = RuleVal(Info, "keepInventory")
        CheckRuleDaylight.Checked = RuleVal(Info, "doDaylightCycle")
        CheckRuleMobGrief.Checked = RuleVal(Info, "mobGriefing")
        CheckRuleFireTick.Checked = RuleVal(Info, "doFireTick")
        CheckRuleWeather.Checked = RuleVal(Info, "doWeatherCycle")
        TextSpawnX.Text = Info.SpawnX.ToString
        TextSpawnY.Text = Info.SpawnY.ToString
        TextSpawnZ.Text = Info.SpawnZ.ToString
        TextTime.Text = Info.Time.ToString
        TextLevelName.Text = Info.LevelName
        ' 快照
        Dim Keep As Integer = 10
        Try : Keep = Settings.Get(Of Integer)("SaveBackupCount") : Catch : End Try
        If Keep < 1 Then Keep = 10
        TextBackupCount.Text = Keep.ToString
        BuildBackupList(Backups, BackupSizes)
        ' 物品
        RefreshInventory(InvItems)
    End Sub

    Private Sub SetColoredName(Tb As TextBlock, Raw As String, Fallback As String)
        Tb.Inlines.Clear()
        If String.IsNullOrWhiteSpace(Raw) Then
            Tb.Text = Fallback
        ElseIf Raw.Contains("§") Then
            Try
                For Each R In McColorText.BuildInlines(Raw)
                    Tb.Inlines.Add(R)
                Next
            Catch
                Tb.Text = Raw
            End Try
        Else
            Tb.Text = Raw
        End If
    End Sub

    Private Function RuleVal(Info As McSaveInfo, Name As String) As Boolean
        Return If(Info.GameRules.ContainsKey(Name), Info.GameRules(Name), True)
    End Function

    Private Function FormatSize(Bytes As Long) As String
        If Bytes >= 1024L * 1024 * 1024 Then Return (Bytes / 1024 / 1024 / 1024).ToString("0.0") & " GB"
        If Bytes >= 1024L * 1024 Then Return (Bytes / 1024 / 1024).ToString("0.0") & " MB"
        If Bytes >= 1024 Then Return (Bytes / 1024).ToString("0") & " KB"
        Return Bytes & " B"
    End Function

    Private Sub RefreshPlayerFields(P As McPlayerInfo)
        Dim Name = If(String.IsNullOrWhiteSpace(P.Name), If(P.Uuid.Length >= 8, P.Uuid.Substring(0, 8), P.Uuid), P.Name)
        Dim DimStr = If(String.IsNullOrWhiteSpace(P.Dimension), "未知", P.Dimension)
        LabPlayerNameFact.Text = Name
        LabDimensionFact.Text = DimStr
        LabPlayerName2.Text = Name
        LabDimension2.Text = DimStr
        TextPlayerX.Text = P.X.ToString("0.0")
        TextPlayerY.Text = P.Y.ToString("0.0")
        TextPlayerZ.Text = P.Z.ToString("0.0")
        TextPlayerXp.Text = P.XpLevel.ToString
    End Sub

    Private Sub ComboPlayer_SelectionChanged() Handles ComboPlayer.SelectionChanged
        If Players Is Nothing Then Return
        Dim I = ComboPlayer.SelectedIndex
        If I >= 0 AndAlso I < Players.Count Then RefreshPlayerFields(Players(I))
    End Sub

    ' ===== 备份 / 快照 =====

    Private Sub BuildBackupList()
        Dim Backups = ModSave.ListBackups(SavePath)
        Dim Sizes As New Dictionary(Of String, Long)
        For Each B In Backups
            Try : Sizes(B) = ModSave.GetFolderSize(B) : Catch : End Try
        Next
        BuildBackupList(Backups, Sizes)
    End Sub

    Private Sub BuildBackupList(Backups As List(Of String), Sizes As Dictionary(Of String, Long))
        PanBackupList.Children.Clear()
        LabBackupTitle.Text = "备份历史（" & Backups.Count & "）"
        BtnTab4.Content = "备份 · " & Backups.Count
        If Not Backups.Any Then
            Dim T As New TextBlock With {.Text = "暂无备份", .FontSize = 12, .Margin = New Thickness(2, 2, 0, 2)}
            T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
            PanBackupList.Children.Add(T)
            Return
        End If
        For Each B In Backups
            PanBackupList.Children.Add(BuildBackupRow(B, If(Sizes.ContainsKey(B), Sizes(B), 0L)))
        Next
    End Sub

    Private Function BuildBackupRow(Folder As String, Size As Long) As FrameworkElement
        Dim Row As New Grid With {.Margin = New Thickness(0, 2, 0, 2)}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Name = IO.Path.GetFileName(Folder.TrimEnd("\"c))
        Dim Info As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Dim T As New TextBlock With {.Text = Name, .FontSize = 12.5, .FontWeight = FontWeights.SemiBold, .TextTrimming = TextTrimming.CharacterEllipsis}
        T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText1")
        Info.Children.Add(T)
        Dim S As New TextBlock With {.Text = FormatSize(Size), .FontSize = 11.5, .Margin = New Thickness(0, 1, 0, 0)}
        S.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
        Info.Children.Add(S)
        Row.Children.Add(Info)
        Dim Actions As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim BtnRestore As New Button With {.Content = "恢复", .Style = FindResource("BtnBase"), .Height = 26, .Padding = New Thickness(10, 0, 10, 0)}
        AddHandler BtnRestore.Click, Sub() RestoreBackup(Folder)
        Actions.Children.Add(BtnRestore)
        Dim BtnDel As New Button With {.Content = "删除", .Style = FindResource("BtnDanger"), .Height = 26, .Padding = New Thickness(10, 0, 10, 0), .Margin = New Thickness(8, 0, 0, 0)}
        AddHandler BtnDel.Click, Sub()
                                     Try
                                         If MyMsgBox("确定删除该备份吗？", "删除备份", "删除", "取消", IsWarn:=True) = 1 Then
                                             ModSave.DeleteBackup(Folder)
                                             BuildBackupList()
                                         End If
                                     Catch ex As Exception
                                         Logger.Error(ex, "删除备份失败")
                                     End Try
                                 End Sub
        Actions.Children.Add(BtnDel)
        Grid.SetColumn(Actions, 1)
        Row.Children.Add(Actions)
        Return Row
    End Function

    Private Sub RestoreBackup(Folder As String)
        If Not CheckNotRunning() Then Return
        Try
            If MyMsgBox("恢复该备份将覆盖当前存档（恢复前会自动备份当前存档）。确定继续？", "恢复备份", "恢复", "取消", IsWarn:=True) = 1 Then
                If ModSave.RestoreBackup(SavePath, Folder) Then
                    Hint("已恢复备份", HintType.Green)
                    RefreshAll()
                Else
                    Hint("恢复失败", HintType.Red)
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, "恢复备份失败")
        End Try
    End Sub

    Private Function CheckNotRunning() As Boolean
        If ModWatcher.HasRunningMinecraft Then
            Hint("游戏正在运行，请先退出游戏再操作", HintType.Red)
            Return False
        End If
        Return True
    End Function

    ' ===== 世界设置 =====

    Private Sub BtnApplySettings_Click() Handles BtnApplySettings.Click
        If Not CheckNotRunning() Then Return
        Try
            Dim GameType = ComboGameType.SelectedIndex
            Dim Difficulty = ComboDifficulty.SelectedIndex
            Dim Cheats = CheckCheats.Checked
            Dim SpawnX = Val(TextSpawnX.Text)
            Dim SpawnY = Val(TextSpawnY.Text)
            Dim SpawnZ = Val(TextSpawnZ.Text)
            Dim TimeVal = Val(TextTime.Text)
            Dim KeepInv = CheckRuleKeepInventory.Checked
            Dim Daylight = CheckRuleDaylight.Checked
            Dim MobGrief = CheckRuleMobGrief.Checked
            Dim FireTick = CheckRuleFireTick.Checked
            Dim Weather = CheckRuleWeather.Checked
            Dim LevelName = TextLevelName.Text.Trim
            Dim Ok = ModSave.ApplySettings(SavePath, Sub(Data, Root)
                                                          ModSave.SetLevelName(Data, LevelName)
                                                          ModSave.SetGameType(Data, GameType)
                                                          ModSave.SetDifficulty(Data, Difficulty)
                                                          ModSave.SetAllowCommands(Data, Cheats)
                                                          ModSave.SetSpawn(Data, CInt(SpawnX), CInt(SpawnY), CInt(SpawnZ))
                                                          ModSave.SetWorldTime(Data, CLng(TimeVal))
                                                          ModSave.SetGameRule(Data, "keepInventory", KeepInv)
                                                          ModSave.SetGameRule(Data, "doDaylightCycle", Daylight)
                                                          ModSave.SetGameRule(Data, "mobGriefing", MobGrief)
                                                          ModSave.SetGameRule(Data, "doFireTick", FireTick)
                                                          ModSave.SetGameRule(Data, "doWeatherCycle", Weather)
                                                      End Sub)
            If Ok Then
                Hint("设置已保存", HintType.Green)
                RefreshAll()
            Else
                Hint("保存失败（level.dat 不可写）", HintType.Red)
            End If
        Catch ex As Exception
            Logger.Error(ex, "应用世界设置失败")
            Hint("保存失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    ' ===== Hero 操作（含更多菜单） =====

    Private Sub DoChangeCover()
        Try
            Dim F = Dialogs.SelectFile("选择封面图片（建议 64×64 PNG）", False)
            If F.Count = 0 Then Return
            Dim Icon = SavePath.TrimEnd("\"c) & "\icon.png"
            IO.File.Copy(F(0), Icon, True)
            ImgIcon.Source = Icon
            Hint("封面已更新", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "修改封面失败")
            Hint("修改封面失败", HintType.Red)
        End Try
    End Sub

    Private Sub DoRename()
        If Not CheckNotRunning() Then Return
        Try
            Dim NewName = MyMsgBoxInput("重命名存档", "请输入新的存档名称：", DefaultInput:=SaveName, Button1:="重命名", Button2:="取消")
            If NewName Is Nothing Then Return
            NewName = NewName.Trim
            If NewName = "" OrElse NewName = SaveName Then Return
            If NewName.IndexOfAny(IO.Path.GetInvalidFileNameChars) >= 0 Then
                Hint("名称包含非法字符", HintType.Red) : Return
            End If
            Dim Target = SavesRoot & "\" & NewName
            If IO.Directory.Exists(Target) Then
                Hint("已存在同名存档", HintType.Red) : Return
            End If
            IO.Directory.Move(SavePath, Target)
            SavePath = Target
            SaveName = NewName
            LabName.Text = NewName
            Hint("已重命名为 " & NewName, HintType.Green)
            RefreshAll()
        Catch ex As Exception
            Logger.Error(ex, "重命名存档失败")
            Hint("重命名失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub DoOpenFolder()
        Try
            If IO.Directory.Exists(SavePath) Then OpenExplorer(SavePath) Else Hint("存档文件夹不存在", HintType.Red)
        Catch ex As Exception
            Logger.Error(ex, "打开存档文件夹失败")
        End Try
    End Sub

    Private Sub DoExport()
        Try
            Dim Dirs = Dialogs.SelectFolder("选择导出位置", False)
            If Dirs.Count = 0 Then Return
            Dim Target = ModSave.ExportSave(SavePath, Dirs(0))
            If Target <> "" Then Hint("已导出到：" & Target, HintType.Green) Else Hint("导出失败", HintType.Red)
        Catch ex As Exception
            Logger.Error(ex, "导出存档失败")
        End Try
    End Sub

    Private Sub DoDelete()
        If Not CheckNotRunning() Then Return
        Try
            If MyMsgBox($"确定要删除存档「{SaveName}」吗？删除后会移动到回收站。", "删除存档", "删除", "取消", IsWarn:=True) = 1 Then
                DirectoryUtils.Delete(SavePath, True)
                Hint("已删除存档 " & SaveName, HintType.Green)
                FrmMain.PageBack()
            End If
        Catch ex As Exception
            Logger.Error(ex, "删除存档失败")
            Hint("删除失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnOpenFolder_Click() Handles BtnOpenFolder.Click
        DoOpenFolder()
    End Sub
    Private Sub BtnExport_Click() Handles BtnExport.Click
        DoExport()
    End Sub

    Private Sub BtnCover_Click() Handles BtnCover.Click
        Try
            CoverZoomed = Not CoverZoomed
            Dim Target = If(CoverZoomed, 1.18, 1.0)
            Dim Trans = CType(PanCover.RenderTransform, ScaleTransform)
            AniStart(AaScaleTransform(PanCover, Target - Trans.ScaleX, 200, , New AniEaseOutFluent), "PageSaveSetup CoverZoom " & PageUuid, True)
        Catch ex As Exception
            Logger.Error(ex, "切换封面缩放失败")
        End Try
    End Sub

    Private Sub MenuChangeCover_Click(sender As Object, e As RoutedEventArgs)
        DoChangeCover()
    End Sub
    Private Sub MenuRename_Click(sender As Object, e As RoutedEventArgs)
        DoRename()
    End Sub
    Private Sub MenuOpenFolder_Click(sender As Object, e As RoutedEventArgs)
        DoOpenFolder()
    End Sub
    Private Sub MenuExport_Click(sender As Object, e As RoutedEventArgs)
        DoExport()
    End Sub
    Private Sub MenuDelete_Click(sender As Object, e As RoutedEventArgs)
        DoDelete()
    End Sub

    Private Sub BtnMore_Click() Handles BtnMore.Click
        Try
            BtnMore.ContextMenu.PlacementTarget = BtnMore
            BtnMore.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
            BtnMore.ContextMenu.IsOpen = True
        Catch ex As Exception
            Logger.Error(ex, "打开更多菜单失败")
        End Try
    End Sub

    Private Sub BtnLaunch_Click() Handles BtnLaunch.Click
        Try
            If Inst Is Nothing OrElse Inst IsNot McInstanceSelected Then
                Hint("请先在主页选择该存档对应的版本", HintType.Blue)
                Return
            End If
            FrmMain.PageBack()
            FrmLaunchLeft.LaunchButtonClick()
        Catch ex As Exception
            Logger.Error(ex, "启动世界失败")
        End Try
    End Sub

    Private Sub BtnBackup_Click() Handles BtnBackup.Click
        DoBackup()
    End Sub
    Private Sub BtnCreateBackup_Click() Handles BtnCreateBackup.Click
        DoBackup()
    End Sub

    Private Sub DoBackup()
        Try
            SaveBackupCount()
            Dim Target = ModSave.MakeBackup(SavePath, "手动")
            If Target <> "" Then
                Hint("备份已创建", HintType.Green)
                BuildBackupList()
            Else
                Hint("创建备份失败", HintType.Red)
            End If
        Catch ex As Exception
            Logger.Error(ex, "创建备份失败")
        End Try
    End Sub

    Private Sub BtnCopy_Click() Handles BtnCopy.Click
        If Not CheckNotRunning() Then Return
        Try
            Dim Target = ModSave.MakeCopy(SavePath)
            If Target <> "" Then Hint("副本已创建：" & IO.Path.GetFileName(Target), HintType.Green) Else Hint("创建副本失败", HintType.Red)
        Catch ex As Exception
            Logger.Error(ex, "创建副本失败")
        End Try
    End Sub

    Private Sub BtnImport_Click() Handles BtnImport.Click
        Try
            Dim Dirs = Dialogs.SelectFolder("选择要导入的存档文件夹", False)
            If Dirs.Count = 0 Then Return
            Dim Target = ModSave.ImportSave(SavesRoot, Dirs(0))
            If Target <> "" Then Hint("已导入：" & IO.Path.GetFileName(Target), HintType.Green) Else Hint("导入失败", HintType.Red)
        Catch ex As Exception
            Logger.Error(ex, "导入存档失败")
        End Try
    End Sub

    Private Sub SaveBackupCount()
        Try
            Dim N = CInt(Val(TextBackupCount.Text))
            If N < 1 Then N = 10
            Settings.Set("SaveBackupCount", N)
        Catch
        End Try
    End Sub

    ' ===== 玩家数据 =====

    Private Sub BtnApplyPlayer_Click() Handles BtnApplyPlayer.Click
        If Not CheckNotRunning() Then Return
        Try
            Dim X = Val(TextPlayerX.Text)
            Dim Y = Val(TextPlayerY.Text)
            Dim Z = Val(TextPlayerZ.Text)
            Dim Xp = CInt(Val(TextPlayerXp.Text))
            Dim Uuid = ""
            If Players IsNot Nothing AndAlso ComboPlayer.SelectedIndex >= 0 AndAlso ComboPlayer.SelectedIndex < Players.Count Then
                Uuid = Players(ComboPlayer.SelectedIndex).Uuid
            End If
            If MyMsgBox("修改玩家数据前会自动备份当前存档。确定应用？", "修改玩家数据", "应用", "取消", IsWarn:=True) = 1 Then
                ModSave.MakeBackup(SavePath, "修改玩家数据前")
                If ModSave.SetPlayerPositionAndXp(SavePath, Uuid, X, Y, Z, Xp) Then
                    Hint("玩家数据已更新", HintType.Green)
                    RefreshAll()
                Else
                    Hint("更新失败（未找到 playerdata）", HintType.Red)
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, "修改玩家数据失败")
            Hint("修改失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    ' ===== 物品 =====

    Private Sub RefreshInvModeStyle()
        BtnInvInventory.Style = CType(FindResource(If(Not InvEnderMode, "NavTabSel", "NavTab")), Style)
        BtnInvEnder.Style = CType(FindResource(If(InvEnderMode, "NavTabSel", "NavTab")), Style)
    End Sub

    Private Sub RefreshInventory()
        Dim Items As List(Of ModSave.McItemStack)
        Try
            Items = ModSave.LoadInventory(SavePath, InvEnderMode)
        Catch ex As Exception
            Logger.Error(ex, "读取背包列表失败")
            Items = New List(Of ModSave.McItemStack)
        End Try
        RefreshInventory(Items)
    End Sub

    Private Sub RefreshInventory(Items As List(Of ModSave.McItemStack))
        RefreshInvModeStyle()
        PanInvList.Children.Clear()
        Try
            If Not Items.Any Then
                Dim T As New TextBlock With {.Text = If(InvEnderMode, "末影箱为空", "背包为空"), .FontSize = 12, .Margin = New Thickness(2, 2, 0, 2)}
                T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
                PanInvList.Children.Add(T)
                Return
            End If
            For Each It In Items
                PanInvList.Children.Add(BuildInvRow(It))
            Next
        Catch ex As Exception
            Logger.Error(ex, "刷新背包列表失败")
        End Try
    End Sub

    Private Function BuildInvRow(Item As ModSave.McItemStack) As FrameworkElement
        Dim Row As New Grid With {.Margin = New Thickness(0, 2, 0, 2)}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Info As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Dim T As New TextBlock With {.Text = $"槽位 {Item.Slot} · {Item.Id} × {Item.Count}", .FontSize = 12.5, .TextTrimming = TextTrimming.CharacterEllipsis}
        T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText1")
        Info.Children.Add(T)
        Row.Children.Add(Info)
        Dim Actions As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim BtnSub As New Button With {.Content = "−", .Style = FindResource("BtnBase"), .Height = 24, .Width = 30, .Padding = New Thickness(0)}
        AddHandler BtnSub.Click, Sub() ChangeItemCount(Item.Slot, Item.Count - 1)
        Actions.Children.Add(BtnSub)
        Dim BtnAdd As New Button With {.Content = "＋", .Style = FindResource("BtnBase"), .Height = 24, .Width = 30, .Padding = New Thickness(0), .Margin = New Thickness(4, 0, 0, 0)}
        AddHandler BtnAdd.Click, Sub() ChangeItemCount(Item.Slot, Item.Count + 1)
        Actions.Children.Add(BtnAdd)
        Dim BtnDel As New Button With {.Content = "删除", .Style = FindResource("BtnDanger"), .Height = 24, .Padding = New Thickness(8, 0, 8, 0), .Margin = New Thickness(8, 0, 0, 0)}
        AddHandler BtnDel.Click, Sub() ChangeItemCount(Item.Slot, 0)
        Actions.Children.Add(BtnDel)
        Grid.SetColumn(Actions, 1)
        Row.Children.Add(Actions)
        Return Row
    End Function

    Private Sub ChangeItemCount(Slot As Integer, NewCount As Integer)
        If Not CheckNotRunning() Then Return
        Try
            If ModSave.SetItemCount(SavePath, InvEnderMode, Slot, NewCount) Then
                RefreshInventory()
            Else
                Hint("修改失败", HintType.Red)
            End If
        Catch ex As Exception
            Logger.Error(ex, "修改物品数量失败")
        End Try
    End Sub

    Private Sub BtnInvInventory_Click() Handles BtnInvInventory.Click
        SwitchInvMode(False)
    End Sub
    Private Sub BtnInvEnder_Click() Handles BtnInvEnder.Click
        SwitchInvMode(True)
    End Sub

    Private Sub SwitchInvMode(Ender As Boolean)
        If InvEnderMode = Ender Then Return
        InvEnderMode = Ender
        PanInvList.Opacity = 0
        RefreshInventory()
        AniStart(AaOpacity(PanInvList, 1 - PanInvList.Opacity, 150, 0, New AniEaseOutFluent), "PageSaveSetup InvFade " & PageUuid, True)
    End Sub

    Private Sub BtnAddItem_Click() Handles BtnAddItem.Click
        If Not CheckNotRunning() Then Return
        Try
            Dim Id = TextAddItemId.Text.Trim
            Dim Count = CInt(Val(TextAddItemCount.Text))
            If Count < 1 Then Count = 1
            If ModSave.AddItem(SavePath, InvEnderMode, Id, Count) Then
                Hint("已添加", HintType.Green)
                TextAddItemId.Text = ""
                RefreshInventory()
            Else
                Hint("添加失败（ID 为空或无空槽位）", HintType.Red)
            End If
        Catch ex As Exception
            Logger.Error(ex, "添加物品失败")
        End Try
    End Sub

    ' ===== 数据：NBT / 统计 / 进度 =====

    Private Sub BtnToggleNbt_Click() Handles BtnToggleNbt.Click
        Try
            Dim Open = PanNbt.Visibility <> Visibility.Visible
            PanNbt.Visibility = If(Open, Visibility.Visible, Visibility.Collapsed)
            BtnToggleNbt.Content = If(Open, "收起", "展开")
            If Open Then PopulateNbt()
        Catch ex As Exception
            Logger.Error(ex, "切换 NBT 视图失败")
        End Try
    End Sub

    Private Sub PopulateNbt()
        PanNbt.Children.Clear()
        Try
            Dim Root = ModSave.ReadLevelDat(SavePath.TrimEnd("\"c) & "\level.dat")
            Dim C As NbtCompound = Nothing
            If Root IsNot Nothing Then
                Dim Data = Root.GetTag("Data")
                If Data IsNot Nothing AndAlso TypeOf Data.Value Is NbtCompound Then
                    C = CType(Data.Value, NbtCompound)
                End If
            End If
            If C Is Nothing Then
                AddNbtLine("无法读取 level.dat 的 Data 节点")
                Return
            End If
            For Each Line In ModSave.FlattenNbtTop(C)
                AddNbtLine(Line)
            Next
        Catch ex As Exception
            AddNbtLine("读取失败：" & ex.Message)
        End Try
    End Sub

    Private Sub AddNbtLine(Text As String)
        Dim T As New TextBlock With {.Text = Text, .FontSize = 12, .Margin = New Thickness(0, 0, 0, 3), .TextWrapping = TextWrapping.Wrap}
        T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText2")
        PanNbt.Children.Add(T)
    End Sub

    Private Sub BtnToggleStats_Click() Handles BtnToggleStats.Click
        ToggleBody(PanStatsBody, BtnToggleStats, AddressOf PopulateStats)
    End Sub
    Private Sub BtnToggleAdv_Click() Handles BtnToggleAdv.Click
        ToggleBody(PanAdvBody, BtnToggleAdv, AddressOf PopulateAdv)
    End Sub

    Private Sub ToggleBody(Body As StackPanel, Btn As Button, Populate As Action)
        Try
            Dim Open = Body.Visibility <> Visibility.Visible
            Body.Visibility = If(Open, Visibility.Visible, Visibility.Collapsed)
            Btn.Content = If(Open, "收起", "展开")
            If Open Then Populate()
        Catch ex As Exception
            Logger.Error(ex, "切换数据视图失败")
        End Try
    End Sub

    Private Sub PopulateStats()
        PanStatsList.Children.Clear()
        For Each Line In ModSave.LoadStats(SavePath)
            AddLine(PanStatsList, Line)
        Next
        If PanStatsList.Children.Count = 0 Then AddLine(PanStatsList, "无统计数据")
    End Sub

    Private Sub PopulateAdv()
        PanAdvList.Children.Clear()
        For Each Line In ModSave.LoadAdvancements(SavePath)
            AddLine(PanAdvList, Line)
        Next
        If PanAdvList.Children.Count = 0 Then AddLine(PanAdvList, "无进度数据")
    End Sub

    Private Sub AddLine(Panel As StackPanel, Text As String)
        Dim T As New TextBlock With {.Text = Text, .FontSize = 12, .Margin = New Thickness(2, 0, 0, 3), .TextWrapping = TextWrapping.Wrap}
        T.SetResourceReference(TextBlock.ForegroundProperty, "BrushText2")
        Panel.Children.Add(T)
    End Sub

    Private Sub BtnClearStats_Click() Handles BtnClearStats.Click
        If Not CheckNotRunning() Then Return
        Try
            If MyMsgBox("确定清空该玩家的所有统计吗？", "清空统计", "清空", "取消", IsWarn:=True) = 1 Then
                ModSave.MakeBackup(SavePath, "清空统计前")
                If ModSave.ClearStats(SavePath) Then Hint("统计已清空", HintType.Green) Else Hint("清空失败", HintType.Red)
                PopulateStats()
            End If
        Catch ex As Exception
            Logger.Error(ex, "清空统计失败")
        End Try
    End Sub

    Private Sub BtnClearAdv_Click() Handles BtnClearAdv.Click
        If Not CheckNotRunning() Then Return
        Try
            If MyMsgBox("确定重置该玩家的全部进度吗？", "重置进度", "重置", "取消", IsWarn:=True) = 1 Then
                ModSave.MakeBackup(SavePath, "重置进度前")
                If ModSave.ClearAdvancements(SavePath) Then Hint("进度已重置", HintType.Green) Else Hint("重置失败", HintType.Red)
                PopulateAdv()
            End If
        Catch ex As Exception
            Logger.Error(ex, "重置进度失败")
        End Try
    End Sub

    Private Sub BtnCopySeed_Click() Handles BtnCopySeed.Click
        Try
            Clipboard.SetText(LabSeed.Text)
            Hint("种子已复制", HintType.Green)
        Catch
            Hint("复制失败", HintType.Red)
        End Try
    End Sub

    ''' <summary>跳转到种子地图，并带上这个存档的种子与游戏版本。</summary>
    Private Sub BtnSeedMap_Click() Handles BtnSeedMap.Click
        Try
            Dim SeedText As String = If(LabSeed.Text, "").Trim()
            If SeedText = "" OrElse SeedText = "不可用" OrElse SeedText = "—" Then
                Hint("这个存档读不到世界种子", HintType.Red)
                Return
            End If
            Dim Ver As String = If(LabVersionFact.Text, "").Trim()
            If Ver = "未知" OrElse Ver = "—" Then Ver = ""
            PageSeedMap.PendingSeed = SeedText
            PageSeedMap.PendingVersion = Ver
            FrmMain.PageChange(FormMain.PageType.SeedMap)
        Catch ex As Exception
            Logger.Warn(ex, "跳转到种子地图失败")
            Hint("跳转到种子地图失败", HintType.Red)
        End Try
    End Sub

End Class
