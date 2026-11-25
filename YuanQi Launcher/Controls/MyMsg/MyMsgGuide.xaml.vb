Public Class MyMsgGuide

    Private ReadOnly MyConverter As MyMsgBoxConverter
    Private ReadOnly Uuid As Integer = GetUuid()
    ''' <summary>
    ''' 当前显示到了第几步，从 0 开始。
    ''' </summary>
    Private CurrentStep As Integer = 0
    Private Const StepCount As Integer = 6
    ''' <summary>「导入已有的游戏」是第几步（从 0 开始）。</summary>
    Private Const ImportStep As Integer = 3

    Public Sub New(Converter As MyMsgBoxConverter)
        Try
            InitializeComponent()
            MyConverter = Converter
            ShapeLine.StrokeThickness = GetWPFSize(1)
        Catch ex As Exception
            Logger.Error(ex, "使用引导弹窗初始化失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub Load(sender As Object, e As EventArgs) Handles MyBase.Loaded
        Try
            'UI 初始化
            UpdateStep()
            '动画
            Opacity = 0
            AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(110, 0, 0, 0) - FrmMain.PanMsg.Background, 200), "PanMsg Background")
            AniStart({
                AaOpacity(Me, 1, 120, 60),
                AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 300, 60, New AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 300, 60, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyMsgGuide " & Uuid)
            '记录日志
            Logger.Info("显示使用引导弹窗")
        Catch ex As Exception
            Logger.Error(ex, "使用引导弹窗加载失败", LogBehavior.Toast)
        End Try
    End Sub

    ''' <summary>刷新步骤面板、进度点与按钮状态（带翻页动画）。</summary>
    Private Sub UpdateStep(Optional Animate As Boolean = False)
        Dim Panels = {PanStep1, PanStep2, PanStep3, PanStep4, PanStep5, PanStep6}
        Dim Dots = {Dot1, Dot2, Dot3, Dot4, Dot5, Dot6}
        Dim OldStep As Integer = -1
        For i = 0 To StepCount - 1
            If Panels(i).Visibility = Visibility.Visible AndAlso i <> CurrentStep Then OldStep = i
        Next
        If Animate AndAlso OldStep >= 0 Then
            SwitchStep(Panels(OldStep), Panels(CurrentStep))
        Else
            For i = 0 To StepCount - 1
                Panels(i).Visibility = If(i = CurrentStep, Visibility.Visible, Visibility.Collapsed)
            Next
        End If
        For i = 0 To StepCount - 1
            Dots(i).Opacity = If(i = CurrentStep, 1, 0.22)
        Next
        BtnPrev.Visibility = If(CurrentStep = 0, Visibility.Collapsed, Visibility.Visible)
        '每翻一页都把按钮恢复可用：导入过程中会把「下一步」和「跳过引导」临时置灰
        BtnNext.IsEnabled = True
        BtnSkip.IsEnabled = True
        BtnNext.Text = If(CurrentStep = 0, "同意并继续",
                          If(CurrentStep = StepCount - 1, "开始使用",
                             If(CurrentStep = ImportStep AndAlso ImportFolder IsNot Nothing, "导入并继续", "下一步")))
        BtnDisagree.Visibility = If(CurrentStep = 0, Visibility.Visible, Visibility.Collapsed)
        '协议页不给"跳过"，必须明确同意或不同意
        BtnSkip.Visibility = If(CurrentStep = 0, Visibility.Collapsed, Visibility.Visible)
        LabTitle.Text = If(CurrentStep = 0, "欢迎使用 缘启", "关于缘启")
        '最后一页：进入时自动体检一次
        If CurrentStep = StepCount - 1 AndAlso Not EnvChecked AndAlso Not EnvRunning Then StartEnvCheck()
        '导入页：进入时扫一遍其他启动器的游戏文件夹
        If CurrentStep = ImportStep Then
            StartImportDetect()
            UpdateImportNextText()
        End If
    End Sub

    ''' <summary>旧页淡出并轻微上移，新页随后淡入并自下方回弹。</summary>
    Private Sub SwitchStep(Old As StackPanel, New1 As StackPanel)
        EnsurePanelTransform(Old)
        EnsurePanelTransform(New1)
        Dim Ot = CType(Old.RenderTransform, TranslateTransform)
        Dim Nt = CType(New1.RenderTransform, TranslateTransform)
        New1.Opacity = 0
        Nt.Y = 14
        New1.Visibility = Visibility.Visible
        AniStart({
            AaOpacity(Old, 0, 90),
            AaDouble(Sub(i) Ot.Y += i, -10 - Ot.Y, 90),
            AaCode(
            Sub()
                Old.Visibility = Visibility.Collapsed
                Old.Opacity = 1
                Ot.Y = 0
                AniStart({
                    AaOpacity(New1, 1, 170),
                    AaDouble(Sub(i) Nt.Y += i, 0 - Nt.Y, 210, 0, New AniEaseOutFluent(AniEasePower.Weak))
                }, "MyMsgGuide Step In " & Uuid)
            End Sub, 0, True)
        }, "MyMsgGuide Step Out " & Uuid)
    End Sub

    Private Sub EnsurePanelTransform(P As StackPanel)
        If P Is Nothing Then Return
        If TypeOf P.RenderTransform IsNot TranslateTransform Then P.RenderTransform = New TranslateTransform()
    End Sub

    '========== 环境体检 ==========
    Private EnvRunning As Boolean = False
    Private EnvChecked As Boolean = False

    Private Sub BtnEula_Click() Handles BtnEula.Click
        OpenWebsite("https://shimo.im/docs/rGrd8pY8xWkt6ryW")
    End Sub

    Private Sub BtnEnvRun_Click() Handles BtnEnvRun.Click
        StartEnvCheck()
    End Sub

    ''' <summary>后台跑体检，结果逐条填进当前页。</summary>
    Private Sub StartEnvCheck()
        If EnvRunning Then Return
        EnvRunning = True
        BtnEnvRun.IsEnabled = False
        BtnEnvRun.Text = "检查中…"
        PanEnvResult.Children.Clear()
        Dim Tip As New TextBlock With {.Text = "正在检查…", .FontSize = 13, .Opacity = 0.6, .Margin = New Thickness(0, 2, 0, 2)}
        Tip.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        PanEnvResult.Children.Add(Tip)
        RunInNewThread(
        Sub()
            Dim Items As List(Of KeyValuePair(Of String, String)) = Nothing
            Try
                Items = RunEnvCheck()
            Catch ex As Exception
                Logger.Warn(ex, "环境体检失败")
            End Try
            RunInUi(
            Sub()
                EnvRunning = False
                EnvChecked = True
                BtnEnvRun.IsEnabled = True
                BtnEnvRun.Text = "重新检查"
                ShowEnvResult(Items)
            End Sub)
        End Sub, "Guide Env Check", ThreadPriority.BelowNormal)
    End Sub

    Private Sub ShowEnvResult(Items As List(Of KeyValuePair(Of String, String)))
        PanEnvResult.Children.Clear()
        If Items Is Nothing OrElse Items.Count = 0 Then
            Dim Fail As New TextBlock With {.Text = "检查没能完成（可能是网络问题），可以重试，也可以直接继续。", .FontSize = 13, .TextWrapping = TextWrapping.Wrap, .Opacity = 0.75}
            Fail.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            PanEnvResult.Children.Add(Fail)
            Return
        End If
        For Each Item In Items
            Dim Ok1 As Boolean = Item.Value.StartsWithF("OK")
            Dim Line As New TextBlock With {.Text = If(Ok1, "✓ ", "✗ ") & Item.Key & "：" & If(Ok1, "正常", Item.Value),
                                            .FontSize = 13, .TextWrapping = TextWrapping.Wrap, .LineHeight = 20, .Margin = New Thickness(0, 1, 0, 1)}
            If Ok1 Then
                Line.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
            Else
                Line.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
                Line.FontWeight = FontWeights.SemiBold
            End If
            PanEnvResult.Children.Add(Line)
        Next
        '联机模块缺失：给一个直接下载的按钮
        Dim MissingCore As Boolean = Items.Any(Function(I) I.Key = "联机模块" AndAlso Not I.Value.StartsWithF("OK"))
        If MissingCore Then
            Dim BtnDl As New MyButton With {.Text = "下载联机模块", .Padding = New Thickness(13, 6, 13, 6), .ColorType = MyButton.ColorState.Highlight,
                                            .HorizontalAlignment = HorizontalAlignment.Left, .Margin = New Thickness(0, 8, 0, 0)}
            AddHandler BtnDl.Click, Sub() DownloadTerracottaCore()
            PanEnvResult.Children.Add(BtnDl)
        End If
        Dim Problems = Items.Where(Function(I) Not I.Value.StartsWithF("OK")).Count
        Dim Sum As New TextBlock With {.Text = If(Problems = 0, "全部通过，可以放心开始。", $"有 {Problems} 项需要注意，先看上面的说明。"),
                                       .FontSize = 12, .Opacity = 0.7, .Margin = New Thickness(0, 8, 0, 0), .TextWrapping = TextWrapping.Wrap}
        Sum.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        PanEnvResult.Children.Add(Sum)
    End Sub
    Private Sub BtnNext_Click() Handles BtnNext.Click
        '导入页：选了文件夹就先执行导入，成功后自动进下一步
        If CurrentStep = ImportStep AndAlso ImportFolder IsNot Nothing AndAlso Not ImportDone Then
            RunImport()
            Return
        End If
        If CurrentStep < StepCount - 1 Then
            CurrentStep += 1
            UpdateStep(True)
            Return
        End If
        CloseGuide(1)
    End Sub

    Private Sub BtnPrev_Click() Handles BtnPrev.Click
        If CurrentStep > 0 Then
            CurrentStep -= 1
            UpdateStep(True)
        End If
    End Sub

    Private Sub BtnDisagree_Click() Handles BtnDisagree.Click
        ModGuide.EulaDeclined = True
        CloseGuide(3)
    End Sub

    Private Sub BtnSkip_Click() Handles BtnSkip.Click
        CloseGuide(2)
    End Sub

    '========== 导入已有的游戏 ==========
    Private ImportCandidates As List(Of GameImport.GameFolder) = Nothing
    Private ImportFolder As GameImport.GameFolder = Nothing
    Private ImportVersions As New List(Of String)
    Private ImportChecked As New HashSet(Of String)
    Private ImportDetecting As Boolean = False
    Private ImportCopying As Boolean = False
    Private ImportDone As Boolean = False
    Private ImportCancel As Boolean = False
    Private ImportShowVersions As Boolean = False

    ''' <summary>进入导入页时扫一遍其他启动器的游戏文件夹（只扫目录，很快）。</summary>
    Private Sub StartImportDetect()
        If ImportDetecting OrElse ImportCandidates IsNot Nothing Then Return
        ImportDetecting = True
        PanImportList.Children.Clear()
        Dim Tip As New TextBlock With {.Text = "正在查找其他启动器的游戏文件夹…", .FontSize = 13, .Opacity = 0.6, .Margin = New Thickness(0, 2, 0, 2)}
        Tip.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        PanImportList.Children.Add(Tip)
        RunInNewThread(
        Sub()
            Dim Items As List(Of GameImport.GameFolder) = Nothing
            Try
                Items = GameImport.Detect()
            Catch ex As Exception
                Logger.Warn(ex, "扫描游戏文件夹失败")
            End Try
            RunInUi(
            Sub()
                ImportDetecting = False
                ImportCandidates = If(Items, New List(Of GameImport.GameFolder))
                RenderImportCandidates()
            End Sub)
        End Sub, "Guide Import Detect", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>候选列表：一行一个文件夹，点击选中；逐个淡入并轻微右移。</summary>
    Private Sub RenderImportCandidates()
        PanImportList.Children.Clear()
        If ImportCandidates Is Nothing OrElse ImportCandidates.Count = 0 Then
            Dim Empty As New TextBlock With {.Text = "没有找到其他启动器的游戏文件夹，可以点下面手动选择。",
                                             .FontSize = 13, .Opacity = 0.7, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 2, 0, 2)}
            Empty.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            PanImportList.Children.Add(Empty)
            Return
        End If
        Dim Index As Integer = 0
        For Each F As GameImport.GameFolder In ImportCandidates
            Dim Row As Border = MakeImportRow(F)
            PanImportList.Children.Add(Row)
            Dim T = CType(Row.RenderTransform, TranslateTransform)
            Row.Opacity = 0
            T.X = 10
            AniStart({AaOpacity(Row, 1, 160, Index * 45),
                      AaDouble(Sub(i) T.X += i, 0 - T.X, 200, Index * 45, New AniEaseOutFluent(AniEasePower.Weak))},
                     "Guide Import Row " & Uuid & Index)
            Index += 1
        Next
    End Sub

    Private Function MakeImportRow(F As GameImport.GameFolder) As Border
        Dim Row As New Border With {
            .CornerRadius = New CornerRadius(6), .Padding = New Thickness(10, 7, 10, 7),
            .Margin = New Thickness(0, 0, 0, 6), .BorderThickness = New Thickness(1),
            .Background = New MyColor(0.04, 0, 0, 0), .RenderTransform = New TranslateTransform(),
            .Cursor = Cursors.Hand}
        Row.SetResourceReference(Border.BorderBrushProperty, "BrushBorder")
        Dim G As New Grid
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Txt As New StackPanel
        Dim Name As New TextBlock With {.Text = $"{F.Source} · {F.VersionCount} 个版本", .FontSize = 13, .FontWeight = FontWeights.SemiBold}
        Name.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Txt.Children.Add(Name)
        Dim PathText As New TextBlock With {.Text = F.Path, .FontSize = 11.5, .Opacity = 0.6,
                                            .TextTrimming = TextTrimming.CharacterEllipsis, .Margin = New Thickness(0, 3, 0, 0)}
        PathText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Txt.Children.Add(PathText)
        G.Children.Add(Txt)
        Dim Mark As New TextBlock With {.Text = "", .FontSize = 11.5, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(8, 0, 0, 0)}
        Mark.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
        Grid.SetColumn(Mark, 1)
        G.Children.Add(Mark)
        Row.Child = G
        Row.Tag = Mark
        '点一下选中，再点一下取消选择
        AddHandler Row.MouseLeftButtonDown,
            Sub()
                If ImportCopying Then Return
                If ImportFolder IsNot Nothing AndAlso PathUtils.IsSame(ImportFolder.Path, F.Path) Then
                    ClearImportSelection()
                Else
                    SelectImportFolder(F, Row)
                End If
            End Sub
        Return Row
    End Function

    ''' <summary>选中一个文件夹：读出它的版本列表，默认全选，然后问「是否全部导入」。</summary>
    Private Sub SelectImportFolder(F As GameImport.GameFolder, Row As Border)
        If ImportCopying Then Return
        Try
            ImportFolder = F
            ImportVersions = New List(Of String)
            Dim VersionsDir As String = IO.Path.Combine(F.Path, "versions")
            If DirectoryUtils.Exists(VersionsDir) Then
                For Each Dir As String In IO.Directory.GetDirectories(VersionsDir)
                    ImportVersions.Add(IO.Path.GetFileName(Dir))
                Next
                ImportVersions.Sort(StringComparer.OrdinalIgnoreCase)
            End If
            ImportChecked = New HashSet(Of String)(ImportVersions, StringComparer.OrdinalIgnoreCase)
            ImportShowVersions = False
            ImportDone = False
            '选中态：边框高亮 + 右侧「已选择」
            For Each Child As Object In PanImportList.Children
                Dim B = TryCast(Child, Border)
                If B Is Nothing Then Continue For
                B.SetResourceReference(Border.BorderBrushProperty, "BrushBorder")
                Dim M = TryCast(B.Tag, TextBlock)
                If M IsNot Nothing Then M.Text = ""
            Next
            Row.SetResourceReference(Border.BorderBrushProperty, "ColorBrush3")
            CType(Row.Tag, TextBlock).Text = "已选择"
            '问是否全部导入
            PanImportAsk.Visibility = Visibility.Visible
            PanImportVersions.Visibility = Visibility.Collapsed
            LabImportAsk.Text = $"找到 {ImportVersions.Count} 个版本，要全部导入吗？"
            LabImportAsk.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            '默认按「全部导入」，对应的按钮亮起来
            SetImportModeButtons(True)
            FadeIn(PanImportAsk)
            UpdateImportNextText()
        Catch ex As Exception
            Logger.Warn(ex, "选择导入文件夹失败")
        End Try
    End Sub

    Private Sub BtnImportAll_Click() Handles BtnImportAll.Click
        ImportShowVersions = False
        ImportChecked = New HashSet(Of String)(ImportVersions, StringComparer.OrdinalIgnoreCase)
        PanImportVersions.Visibility = Visibility.Collapsed
        LabImportAsk.Text = $"将导入全部 {ImportVersions.Count} 个版本。"
        SetImportModeButtons(True)
        UpdateImportNextText()
    End Sub

    Private Sub BtnImportSome_Click() Handles BtnImportSome.Click
        ImportShowVersions = True
        RenderImportVersions()
        PanImportVersions.Visibility = Visibility.Visible
        FadeIn(PanImportVersions)
        SetImportModeButtons(False)
        RefreshImportSummary()
    End Sub

    ''' <summary>取消选择：再点一次已选中的文件夹就走这里，回到还没选文件夹的状态。</summary>
    Private Sub ClearImportSelection()
        If ImportCopying Then Return
        ImportFolder = Nothing
        ImportVersions.Clear()
        ImportChecked.Clear()
        ImportShowVersions = False
        ImportDone = False
        PanImportAsk.Visibility = Visibility.Collapsed
        PanImportVersions.Visibility = Visibility.Collapsed
        PanImportProgress.Visibility = Visibility.Collapsed
        For Each Child As Object In PanImportList.Children
            Dim Row = TryCast(Child, Border)
            If Row Is Nothing Then Continue For
            Row.SetResourceReference(Border.BorderBrushProperty, "BrushBorder")
            Dim Mark = TryCast(Row.Tag, TextBlock)
            If Mark IsNot Nothing Then Mark.Text = ""
        Next
        UpdateImportNextText()
    End Sub

    ''' <summary>点了哪个模式，哪个按钮就变绿。</summary>
    Private Sub SetImportModeButtons(All As Boolean)
        Try
            BtnImportAll.ColorType = If(All, MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
            BtnImportSome.ColorType = If(All, MyButton.ColorState.Normal, MyButton.ColorState.Highlight)
        Catch ex As Exception
        End Try
    End Sub

    Private Sub BtnImportSelectAll_Click() Handles BtnImportSelectAll.Click
        SetAllImportChecked(True)
    End Sub
    Private Sub BtnImportSelectNone_Click() Handles BtnImportSelectNone.Click
        SetAllImportChecked(False)
    End Sub
    Private Sub BtnImportSelectInvert_Click() Handles BtnImportSelectInvert.Click
        For Each V As String In ImportVersions
            If ImportChecked.Contains(V) Then ImportChecked.Remove(V) Else ImportChecked.Add(V)
        Next
        RenderImportVersions()
        RefreshImportSummary()
    End Sub

    Private Sub SetAllImportChecked(Value As Boolean)
        If Value Then
            ImportChecked = New HashSet(Of String)(ImportVersions, StringComparer.OrdinalIgnoreCase)
        Else
            ImportChecked.Clear()
        End If
        RenderImportVersions()
        RefreshImportSummary()
    End Sub

    ''' <summary>版本勾选列表（全选 / 全不选 / 反选 由上面的按钮驱动）。</summary>
    Private Sub RenderImportVersions()
        PanImportVersionList.Children.Clear()
        For Each V As String In ImportVersions
            Dim VersionName As String = V
            Dim Chk As New MyCheckBox With {.Text = VersionName, .Height = 24, .Checked = ImportChecked.Contains(VersionName)}
            AddHandler Chk.Change,
                Sub(sender As Object, user As Boolean)
                    If Chk.Checked Then ImportChecked.Add(VersionName) Else ImportChecked.Remove(VersionName)
                    RefreshImportSummary()
                End Sub
            PanImportVersionList.Children.Add(Chk)
        Next
    End Sub

    Private Sub RefreshImportSummary()
        LabImportAsk.Text = $"已选 {ImportChecked.Count} / {ImportVersions.Count} 个版本。"
        UpdateImportNextText()
    End Sub

    ''' <summary>「下一步」按钮：选了文件夹就变成「导入并继续」，一个版本都没选时禁用。</summary>
    Private Sub UpdateImportNextText()
        If CurrentStep <> ImportStep Then Return
        If ImportCopying Then
            BtnNext.IsEnabled = False
            Return
        End If
        If ImportFolder Is Nothing OrElse ImportDone Then
            '没选文件夹，或者已经导完了：正常往下走
            BtnNext.Text = "下一步"
            BtnNext.IsEnabled = True
        Else
            BtnNext.Text = "导入并继续"
            BtnNext.IsEnabled = ImportChecked.Count > 0
        End If
    End Sub

    Private Sub BtnImportPick_Click() Handles BtnImportPick.Click
        Try
            Dim Folder As String = Dialogs.SelectFolder("选择已有的 Minecraft 文件夹（含 versions 的那个）", False).FirstOrDefault
            If String.IsNullOrWhiteSpace(Folder) Then Return
            Dim VersionsDir As String = IO.Path.Combine(Folder, "versions")
            If Not DirectoryUtils.Exists(VersionsDir) OrElse IO.Directory.GetDirectories(VersionsDir).Length = 0 Then
                MyMsgBox("这个文件夹里没有找到 versions 目录，不像是 Minecraft 的游戏文件夹。", "选择文件夹", IsWarn:=True)
                Return
            End If
            Dim Synthetic As New GameImport.GameFolder With {
                .Source = IO.Path.GetFileName(Folder.TrimEnd("\"c)),
                .Path = PathUtils.AddSlashSuffix(PathUtils.ForCompare(Folder)),
                .VersionCount = IO.Directory.GetDirectories(VersionsDir).Length}
            PanImportList.Children.Clear()
            Dim Row As Border = MakeImportRow(Synthetic)
            PanImportList.Children.Add(Row)
            SelectImportFolder(Synthetic, Row)
        Catch ex As Exception
            Logger.Warn(ex, "手动选择游戏文件夹失败")
        End Try
    End Sub

    Private Sub CheckImportCopy_Change(sender As Object, user As Boolean) Handles CheckImportCopy.Change
        CheckImportDelete.IsEnabled = CheckImportCopy.Checked
        If Not CheckImportCopy.Checked Then CheckImportDelete.Checked = False
    End Sub

    ''' <summary>
    ''' 出现时轻微上滑。刻意不做透明度补间：这些面板是动态显示/隐藏的，
    ''' 万一动画没跑起来，从 0 淡入会留下一个「看不见的面板」，这里保证始终可见。
    ''' </summary>
    Private Sub FadeIn(El As FrameworkElement)
        If El Is Nothing Then Return
        El.Opacity = 1
        If TypeOf El.RenderTransform IsNot TranslateTransform Then El.RenderTransform = New TranslateTransform()
        Dim T = CType(El.RenderTransform, TranslateTransform)
        T.Y = 8
        AniStart(AaDouble(Sub(i) T.Y += i, 0 - T.Y, 190, 0, New AniEaseOutFluent(AniEasePower.Weak)), "Guide Import Slide " & Uuid)
    End Sub

    Private Sub SetImportBar(P As Double)
        Try
            Dim W As Double = If(ImportBarTrack IsNot Nothing AndAlso ImportBarTrack.ActualWidth > 0, ImportBarTrack.ActualWidth, 460)
            Dim Target As Double = W * Math.Min(1, Math.Max(0, P))
            AniStart(AaDouble(Sub(i) ImportBarFill.Width += i, Target - ImportBarFill.Width, 180), "Guide Import Bar " & Uuid)
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>执行导入：复制（可取消）或直接登记；勾了删除就把原文件夹移进回收站。</summary>
    Private Sub RunImport()
        If ImportCopying OrElse ImportFolder Is Nothing Then Return
        ImportCopying = True
        ImportCancel = False
        ImportDone = False
        BtnSkip.IsEnabled = False
        PanImportProgress.Visibility = Visibility.Visible
        FadeIn(PanImportProgress)
        SetImportBar(0)
        LabImportStatus.Text = "准备中……"
        UpdateImportNextText()
        Dim Source As String = ImportFolder.Path
        Dim SourceName As String = ImportFolder.Source
        Dim DoCopy As Boolean = CheckImportCopy.Checked
        Dim DoDelete As Boolean = DoCopy AndAlso CheckImportDelete.Checked
        Dim Versions As New List(Of String)(ImportChecked)
        RunInNewThread(
        Sub()
            Dim Ok As Boolean = True
            Dim Msg As String = ""
            Try
                If DoCopy Then
                    Dim Target As String = Paths.Base & ".minecraft\"
                    Ok = GameImport.CopyFolder(Source, Target, Versions,
                        Sub(P As Double, Text As String)
                            RunInUi(Sub()
                                        SetImportBar(P)
                                        LabImportStatus.Text = Text
                                    End Sub)
                        End Sub,
                        Function() ImportCancel)
                    If Ok Then
                        GameImport.Register(Target, SourceName)
                        If DoDelete Then
                            RunInUi(Sub() LabImportStatus.Text = "正在把原文件夹移到回收站……")
                            If Not GameImport.DeleteToRecycleBin(Source) Then
                                Msg = "原文件夹没能移入回收站，已保留在原位置。"
                            End If
                        End If
                    Else
                        Msg = If(ImportCancel, "已取消导入，已经复制的文件保留在本启动器目录里。", "复制没有完成。")
                    End If
                Else
                    GameImport.Register(Source, SourceName)
                End If
            Catch ex As Exception
                Logger.Warn(ex, "导入游戏文件失败")
                Ok = False
                Msg = "导入失败：" & ex.Message
            End Try
            RunInUi(
            Sub()
                ImportCopying = False
                ImportDone = Ok
                BtnSkip.IsEnabled = True
                SetImportBar(If(Ok, 1, 0))
                If Ok Then
                    '直接使用时只是把文件夹加进来，不说「导入 N 个版本」免得误会成复制了
                    LabImportStatus.Text = If(Msg <> "", Msg,
                                               If(DoCopy,
                                                  $"已导入 {Versions.Count} 个版本。",
                                                  $"已添加文件夹，{Versions.Count} 个版本可直接使用。"))
                    '导入期间按钮是置灰的，这里先恢复，避免翻页动画没跑时按钮一直是灰的
                    BtnNext.IsEnabled = True
                    '成功就自动进下一步
                    AniStart({AaCode(Sub()
                                         If CurrentStep < StepCount - 1 Then
                                             CurrentStep += 1
                                             UpdateStep(True)
                                         End If
                                     End Sub, 420)}, "Guide Import Done " & Uuid)
                Else
                    LabImportStatus.Text = Msg
                    UpdateImportNextText()
                End If
            End Sub)
        End Sub, "Guide Import", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>
    ''' 结束引导。
    ''' Result 为 1 表示完整看完，2 表示跳过。
    ''' </summary>
    Private Sub CloseGuide(Result As Integer)
        If MyConverter Is Nothing OrElse MyConverter.IsExited Then Return
        MyConverter.IsExited = True
        MyConverter.Result = Result
        If MyConverter.ForceWait Then MyConverter.WaitFrame.Continue = False
        Interop.ComponentDispatcher.PopModal()
        '动画
        AniStart({
            AaCode(
            Sub()
                If Not WaitingMyMsgBox.Any() Then
                    AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsg.Background, 200, Ease:=New AniEaseOutFluent(AniEasePower.Weak)))
                End If
            End Sub, 30),
            AaOpacity(Me, -Opacity, 80, 20),
            AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 150, 0, New AniEaseOutFluent),
            AaDouble(Sub(i) TransformRotate.Angle += i, 5 - TransformRotate.Angle, 150, 0, New AniEaseInFluent(AniEasePower.Weak)),
            AaCode(Sub() CType(Parent, Grid).Children.Remove(Me), , True)
        }, "MyMsgGuide " & Uuid)
    End Sub

    ''' <summary>
    ''' 是否已经看完了引导。
    ''' </summary>
    Public Shared ReadOnly Property IsFinished As Boolean
        Get
            Return Settings.Get(Of Integer)("SystemGuideVersion") >= GuideVersion
        End Get
    End Property
    ''' <summary>
    ''' 当前的引导版本号。修改引导内容后把它加一，即可让所有用户重新看到引导。
    ''' </summary>
    Public Const GuideVersion As Integer = 1

    ''' <summary>
    ''' 标记引导已经完成。
    ''' </summary>
    Public Shared Sub MarkFinished()
        If Settings.Get(Of Integer)("SystemGuideVersion") < GuideVersion Then Settings.Set("SystemGuideVersion", GuideVersion)
    End Sub

    ''' <summary>
    ''' 拖动窗口（仅标题附近）。
    ''' </summary>
    Private Sub Drag(sender As Object, e As MouseButtonEventArgs) Handles PanBorder.MouseLeftButtonDown, LabTitle.MouseLeftButtonDown
        On Error Resume Next
        If e.GetPosition(ShapeLine).Y <= 2 Then FrmMain.DragMove()
    End Sub

End Class
