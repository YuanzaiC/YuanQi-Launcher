Public Class PageSelectRight

    '窗口基础
    Private Sub PageSelectRight_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
        PanBack.ScrollToHome()
    End Sub
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanAllBack, Nothing, McInstanceListLoader, AddressOf McInstanceListUI, AutoRun:=False)
    End Sub
    Private Sub Load_Click(sender As Object, e As MouseButtonEventArgs) Handles Load.Click
        If McInstanceListLoader.State = LoadState.Failed Then
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        End If
    End Sub

    '窗口属性
    ''' <summary>
    ''' 是否显示隐藏的 Minecraft 版本。
    ''' </summary>
    Public ShowHidden As Boolean = False

#Region "结果 UI 化"

    Private Sub McInstanceListUI(Loader As LoaderTask(Of String, Integer))
        Try
            '加载 UI
            PanMain.Children.Clear()

            For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList.ToArray
                '确认是否为隐藏版本显示状态
                If Card.Key = McInstanceCardType.Hidden Xor ShowHidden Then Continue For
                '已归档的版本不混在各自的分类里：统一收进页面最下面的「已归档」卡片
                Dim Instances = Card.Value.Where(Function(Instance) Not McInstanceLock.IsLocked(Instance) AndAlso Not IsArchivedSafe(Instance)).ToList
                If Not Instances.Any Then Continue For
#Region "确认卡片名称"
                Dim CardName As String = ""
                Select Case Card.Key
                    Case McInstanceCardType.OriginalLike
                        CardName = "常规版本"
                    Case McInstanceCardType.API
                        Dim IsForgeExists As Boolean = False
                        Dim IsNeoForgeExists As Boolean = False
                        Dim IsFabricExists As Boolean = False
                        Dim IsLiteExists As Boolean = False
                        For Each Instance As McInstance In Instances
                            If Instance.Version.ModLoaders.HasFlagF(ModLoaders.Fabric) Then IsFabricExists = True
                            If Instance.Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader) Then IsLiteExists = True
                            If Instance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then IsForgeExists = True
                            If Instance.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) Then IsNeoForgeExists = True
                        Next
                        If If(IsLiteExists, 1, 0) + If(IsForgeExists, 1, 0) + If(IsFabricExists, 1, 0) + If(IsNeoForgeExists, 1, 0) > 1 Then
                            CardName = "可安装版本"
                        ElseIf IsForgeExists Then
                            CardName = "Forge 版本"
                        ElseIf IsNeoForgeExists Then
                            CardName = "NeoForge 版本"
                        ElseIf IsLiteExists Then
                            CardName = "LiteLoader 版本"
                        Else
                            CardName = "Fabric 版本"
                        End If
                    Case McInstanceCardType.Error
                        CardName = "错误的版本"
                    Case McInstanceCardType.Hidden
                        CardName = "隐藏的版本"
                    Case McInstanceCardType.Rubbish
                        CardName = "不常用版本"
                    Case McInstanceCardType.Star
                        CardName = "收藏夹"
                    Case McInstanceCardType.Fool
                        CardName = "愚人节版本"
                    Case Else
                        Throw New ArgumentException("未知的卡片种类（" & Card.Key & "）")
                End Select
#End Region
                '建立控件
                Dim CardTitle As String = CardName & If(CardName = "收藏夹", "", " (" & Instances.Count & ")")
                Dim NewCard As New MyCard With {.Title = CardTitle, .Margin = New Thickness(0, 0, 0, 15)}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0)}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                NewCard.SetContentBuilder(Instances, Function(Instance) Instance.ToListItem(AddressOf McInstanceListContent))
                PanMain.Children.Add(NewCard)
                '确定卡片是否展开
                If Card.Key = McInstanceCardType.Rubbish OrElse Card.Key = McInstanceCardType.Error OrElse Card.Key = McInstanceCardType.Fool Then
                    NewCard.IsSwapped = True
                Else
                    NewCard.BuildContent()
                End If
            Next

            '已归档的版本：单独一张卡片排在最后，默认收起
            Dim ArchivedAll As New List(Of McInstance)
            For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList.ToArray
                If Card.Key = McInstanceCardType.Hidden Xor ShowHidden Then Continue For
                For Each Instance In Card.Value
                    If McInstanceLock.IsLocked(Instance) Then Continue For
                    If IsArchivedSafe(Instance) Then ArchivedAll.Add(Instance)
                Next
            Next
            If ArchivedAll.Any Then
                ArchivedAll.Sort(Function(A, B) String.Compare(A.Name, B.Name, StringComparison.CurrentCultureIgnoreCase))
                Dim ArchivedCard As New MyCard With {.Title = "已归档 (" & ArchivedAll.Count & ")", .Margin = New Thickness(0, 0, 0, 15)}
                Dim ArchivedStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0)}
                ArchivedCard.Children.Add(ArchivedStack)
                ArchivedCard.SwapControl = ArchivedStack
                ArchivedCard.SetContentBuilder(ArchivedAll, Function(Instance) Instance.ToListItem(AddressOf McInstanceListContent))
                PanMain.Children.Add(ArchivedCard)
                '归档版本平时不用，默认收起；整页只剩它时下面的「强制展开」会把它打开
                ArchivedCard.IsSwapped = True
            End If

            '若只有一个卡片，则强制展开
            If PanMain.Children.Count = 1 AndAlso CType(PanMain.Children(0), MyCard).IsSwapped Then
                CType(PanMain.Children(0), MyCard).IsSwapped = False
            End If

            '判断应该显示哪一个页面
            If PanMain.Children.Count = 0 Then
                PanEmpty.Visibility = Visibility.Visible
                PanBack.Visibility = Visibility.Collapsed
                If ShowHidden Then
                    LabEmptyTitle.Text = "无隐藏版本"
                    LabEmptyContent.Text = "没有版本被隐藏，你可以在版本设置的版本分类选项中隐藏版本。" & vbCrLf & "再次按下 F11 即可退出隐藏版本查看模式。"
                    BtnEmptyDownload.Visibility = Visibility.Collapsed
                Else
                    LabEmptyTitle.Text = "无可用版本"
                    LabEmptyContent.Text = "未找到任何版本的游戏，请先下载任意版本的游戏。" & vbCrLf & "若有已存在的游戏，请在左边的列表中选择添加文件夹，选择 .minecraft 文件夹将其导入。"
                    BtnEmptyDownload.Visibility = If(Settings.Get(Of Boolean)("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
                End If
            Else
                PanBack.Visibility = Visibility.Visible
                PanEmpty.Visibility = Visibility.Collapsed
            End If

        Catch ex As Exception
            Logger.Error(ex, "将版本列表转换显示时失败")
        End Try
    End Sub

    ''' <summary>
    ''' 版本是否已归档。读不到信息时按「未归档」处理：
    ''' 这里只是用来分组，不该因为某个版本文件夹异常就把整页列表搞崩。
    ''' </summary>
    Private Shared Function IsArchivedSafe(Instance As McInstance) As Boolean
        Try
            Return ModArchive.IsArchived(Instance.PathVersion)
        Catch ex As Exception
            Logger.Warn(ex, $"读取版本归档状态失败：{Instance.PathVersion}")
            Return False
        End Try
    End Function

    Public Shared Sub McInstanceListContent(sender As MyListItem, e As EventArgs)
        Dim Instance As McInstance = sender.Tag
        '注册点击事件
        AddHandler sender.Click, AddressOf Item_Click
        '图标按钮
        Dim BtnStar As New MyIconButton
        If Instance.IsStar Then
            BtnStar.ToolTip = "取消收藏"
            BtnStar.LogoScale = 1.1
            BtnStar.Logo = Logo.IconButtonLikeFill
        Else
            BtnStar.ToolTip = "收藏"
            BtnStar.LogoScale = 1.1
            BtnStar.Logo = Logo.IconButtonLikeLine
        End If
        AddHandler BtnStar.Click,
        Sub()
            WriteIni(Instance.PathVersion & "YQL\Setup.ini", "IsStar", Not Instance.IsStar)
            McInstanceListForceRefresh = True
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        End Sub
        Dim BtnDel As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonDelete}
        BtnDel.ToolTip = "删除"
        AddHandler BtnDel.Click, Sub() DeleteInstance(sender, Instance)
        '已归档的版本：整行做"封存"外观（名字灰、挂琥珀色标、图标压暗、摘要换成琥珀色），
        '和正常版本一眼就能分开；只能解压还原，不能直接启动或进设置
        If ModArchive.IsArchived(Instance.PathVersion) Then
            Try
                sender.Info = $"{ModArchive.GetSummary(Instance.PathVersion)}{vbCrLf}{sender.Info}"
            Catch ex As Exception
                Logger.Warn(ex, "显示归档信息失败")
            End Try
            ApplyArchivedLook(sender)
            Dim BtnExtract As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonOpen}
            BtnExtract.ToolTip = "解压还原该版本"
            AddHandler BtnExtract.Click, Sub() ExtractInstance(Instance)
            AddHandler sender.MouseRightButtonUp, Sub() ExtractInstance(Instance)
            sender.Buttons = {BtnStar, BtnDel, BtnExtract}
            Return
        End If
        '描述里补上该版本的占用大小（后台计算 + 缓存，避免列表滚动卡顿）
        ShowInstanceSize(sender, Instance)
        If Instance.State <> McInstance.McInstanceState.Error Then
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonSetup}
            BtnCont.ToolTip = "设置"
            AddHandler BtnCont.Click,
            Sub()
                PageInstanceLeft.Instance = Instance
                FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
            End Sub
            '归档：深度压缩以节省空间
            Dim BtnArchive As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonSave}
            BtnArchive.ToolTip = "归档（深度压缩，节省空间）"
            AddHandler BtnArchive.Click, Sub() ArchiveInstance(Instance)
            AddHandler sender.MouseRightButtonUp,
            Sub()
                PageInstanceLeft.Instance = Instance
                FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
            End Sub
            sender.Buttons = {BtnStar, BtnDel, BtnCont, BtnArchive}
        Else
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.15, .Logo = Logo.IconButtonOpen}
            BtnCont.ToolTip = "打开文件夹"
            AddHandler BtnCont.Click, Sub() PageInstanceOverall.OpenInstanceFolder(Instance)
            AddHandler sender.MouseRightButtonUp, Sub() PageInstanceOverall.OpenInstanceFolder(Instance)
            sender.Buttons = {BtnStar, BtnDel, BtnCont}
        End If
    End Sub

    ''' <summary>版本占用大小直接从持久化缓存取（首次统计后写入 YQL\Cache\InstanceSizes.json）。</summary>

    ''' <summary>在条目描述里追加该版本的占用大小。</summary>
    Public Shared Sub ShowInstanceSize(Item As MyListItem, Instance As McInstance)
        Try
            Dim Cached As Long = InstanceSizeStore.TryGet(Instance.PathVersion)
            If Cached > 0 Then
                ApplyInstanceSize(Item, Instance, Cached)
                Return
            End If
            RunInNewThread(
            Sub()
                Try
                    Dim Size As Long = ModArchive.GetFolderSize(Instance.PathVersion, Nothing)
                    InstanceSizeStore.Store(Instance.PathVersion, Size)
                    RunInUi(Sub() ApplyInstanceSize(Item, Instance, Size))
                Catch ex As Exception
                    Logger.Warn(ex, "统计版本占用大小失败")
                End Try
            End Sub, "Instance Size")
        Catch ex As Exception
            Logger.Warn(ex, "显示版本占用大小失败")
        End Try
    End Sub

    Private Shared Sub ApplyInstanceSize(Item As MyListItem, Instance As McInstance, Size As Long)
        Try
            If Size <= 0 OrElse Item Is Nothing Then Return
            '虚拟化列表会复用条目：确认这条条目现在仍然对应这个版本
            If Item.Tag IsNot Instance Then Return
            Dim Suffix As String = $" · 占用 {ModArchive.FormatSize(Size)}"
            If Item.Info Is Nothing Then
                Item.Info = $"占用 {ModArchive.FormatSize(Size)}"
            ElseIf Not Item.Info.EndsWithF(Suffix, True) Then
                Item.Info &= Suffix
            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新版本占用大小失败")
        End Try
    End Sub

    ''' <summary>归档指定版本（深度压缩）。</summary>
    Public Shared Sub ArchiveInstance(Instance As McInstance)
        Try
            If McInstanceLock.IsLocked(Instance) Then
                Hint("该版本正在被修改，请稍后再归档！", HintType.Red)
                Return
            End If
            Dim Size As Long = ModArchive.GetFolderSize(Instance.PathVersion, Nothing)
            '压缩率预估：jar/zip 本身已是压缩包（实测约省 17%），其余内容约省 58%
            Dim JarBytes As Long = 0
            Try
                For Each FilePath As String In DirectoryUtils.EnumerateFiles(Instance.PathVersion, True)
                    Dim Ext As String = IO.Path.GetExtension(FilePath).ToLower
                    If Ext = ".jar" OrElse Ext = ".zip" Then JarBytes += New FileInfo(FilePath).Length
                Next
            Catch
            End Try
            Dim Estimate As Long = CLng(JarBytes * 0.83 + (Size - JarBytes) * 0.42)
            Dim EstimateText As String = $"预计归档后约 {ModArchive.FormatSize(Estimate)}（按实测压缩率估算）"
            Dim Message As String = $"归档会把这个版本压缩成单个压缩包以节省空间。{vbCrLf}" &
                $"归档后版本仍会显示在列表里（标注「已归档」），但不能直接启动，点选时会提示解压。{vbCrLf}{vbCrLf}" &
                $"版本：{Instance.Name}{vbCrLf}当前占用：{ModArchive.FormatSize(Size)}{vbCrLf}{EstimateText}{vbCrLf}{vbCrLf}确定要归档吗？"
            If MyMsgBox(Message, "归档版本", "归档", "取消", IsWarn:=True) = 2 Then Return
            RunArchiveTask(Instance, True)
        Catch ex As Exception
            Logger.Error(ex, "归档版本失败")
            MyMsgBox(ex.Message, "归档失败", IsWarn:=True)
        End Try
    End Sub

    ''' <summary>解压还原已归档的版本。</summary>
    Public Shared Sub ExtractInstance(Instance As McInstance)
        Try
            If McInstanceLock.IsLocked(Instance) Then
                Hint("该版本正在被修改，请稍后再解压！", HintType.Red)
                Return
            End If
            Dim Message As String = $"该版本已归档，需要先解压才能使用。{vbCrLf}{vbCrLf}" &
                $"版本：{Instance.Name}{vbCrLf}{ModArchive.GetSummary(Instance.PathVersion)}{vbCrLf}{vbCrLf}现在解压还原吗？"
            If MyMsgBox(Message, "解压归档版本", "解压", "取消") = 2 Then Return
            RunArchiveTask(Instance, False)
        Catch ex As Exception
            Logger.Error(ex, "解压归档版本失败")
            MyMsgBox(ex.Message, "解压失败", IsWarn:=True)
        End Try
    End Sub

    ''' <summary>
    ''' 批量归档多个版本（供空间体检的归档候选使用），在任务栏显示整体进度。
    ''' 单个版本出错不再中断整批：能归档的继续归档，最后一起报结果；
    ''' 不管成功失败，已经归档好的版本都要刷掉占用缓存、刷新版本列表，否则列表里还是"未归档"的样子。
    ''' </summary>
    Public Shared Sub RunArchiveBatch(Paths As List(Of String))
        If Paths Is Nothing OrElse Paths.Count = 0 Then Return
        Dim AllPaths As New List(Of String)(Paths)
        Dim Done As New List(Of String) '已经归档好的
        Dim Fails As New List(Of String) '失败的：名字 + 原因
        Dim DoneLock As New Object
        Dim FailsLock As New Object
        Dim Task As New LoaderTask(Of Integer, Boolean)("批量归档",
            Sub(T)
                For i As Integer = 0 To AllPaths.Count - 1
                    Dim P As String = AllPaths(i)
                    Dim ShortName As String = IO.Path.GetFileName(P.TrimEnd("\"c))
                    Dim Base As Double = i / CDbl(AllPaths.Count)
                    Try
                        If ModArchive.IsArchived(P) Then
                            '别人正好归档过了：当成完成，不必重复压
                            SyncLock DoneLock
                                Done.Add(P)
                            End SyncLock
                        Else
                            ModArchive.Archive(P, Sub(Pr As Double)
                                                      T.Progress = Base + Math.Max(0.0, Math.Min(1.0, Pr)) / AllPaths.Count
                                                  End Sub)
                            SyncLock DoneLock
                                Done.Add(P)
                            End SyncLock
                        End If
                    Catch ex As Exception
                        Logger.Warn(ex, $"批量归档失败：{P}")
                        SyncLock FailsLock
                            Fails.Add($"{ShortName}：{ex.Message}")
                        End SyncLock
                    End Try
                Next
                T.Progress = 1
                If Done.Count = 0 AndAlso Fails.Count > 0 Then
                    Throw New Exception(String.Join(vbCrLf, Fails)) '全失败才让任务栏标红
                End If
                T.Output = True
            End Sub) With {.ProgressWeight = 1}
        Dim Combo As New LoaderCombo(Of Integer)("批量归档", {Task})
        LoaderTaskbarAdd(Combo)
        AddHandler Combo.OnStateChangedUi,
            Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
                If NewState <> LoadState.Finished AndAlso NewState <> LoadState.Failed AndAlso
                   NewState <> LoadState.Canceled Then Return
                Dim OkList As New List(Of String)
                SyncLock DoneLock
                    OkList.AddRange(Done)
                End SyncLock
                Dim FailList As New List(Of String)
                SyncLock FailsLock
                    FailList.AddRange(Fails)
                End SyncLock
                '已经归档好的：刷掉占用缓存并强制刷新版本列表（这一步以前漏了，所以"压完了却不显示已归档"）
                For Each P In OkList
                    InstanceSizeStore.Remove(P)
                Next
                If OkList.Count > 0 Then
                    McInstanceListForceRefresh = True
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                End If
                If NewState = LoadState.Canceled Then
                    If OkList.Count > 0 Then Hint($"已归档 {OkList.Count} 个版本，其余已取消", HintType.Blue)
                    Return
                End If
                If FailList.Count = 0 Then
                    Hint($"已归档 {OkList.Count} 个版本！", HintType.Green)
                ElseIf OkList.Count = 0 Then
                    MyMsgBox("一个都没归档成功：" & vbCrLf & vbCrLf & String.Join(vbCrLf, FailList), "批量归档", IsWarn:=True)
                Else
                    MyMsgBox($"已归档 {OkList.Count} 个版本，{FailList.Count} 个没成功（没成功的保持原样，可以再试）：" & vbCrLf & vbCrLf &
                             String.Join(vbCrLf, FailList), "批量归档", IsWarn:=True)
                End If
            End Sub
        Combo.Start(0)
    End Sub

    ''' <summary>在后台线程执行归档/解压，并在任务栏显示进度。</summary>
    Private Shared Sub RunArchiveTask(Instance As McInstance, IsArchive As Boolean)
        Dim ComboName As String = If(IsArchive, "归档版本", "解压版本")
        Dim Task As New LoaderTask(Of Integer, Boolean)(If(IsArchive, "深度压缩", "解压还原"),
            Sub(T)
                If IsArchive Then
                    ModArchive.Archive(Instance.PathVersion, Sub(P) T.Progress = P)
                Else
                    ModArchive.Extract(Instance.PathVersion, Sub(P) T.Progress = P)
                End If
                T.Output = True
            End Sub) With {.ProgressWeight = 1}
        Dim Combo As New LoaderCombo(Of Integer)(ComboName, {Task})
        LoaderTaskbarAdd(Combo)
        AddHandler Combo.OnStateChangedUi,
            Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
                Select Case NewState
                    Case LoadState.Finished
                        Hint(If(IsArchive, "已归档该版本！", "已解压还原该版本！"), HintType.Green)
                        InstanceSizeStore.Remove(Instance.PathVersion)
                        McInstanceListForceRefresh = True
                        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                    Case LoadState.Failed
                        '出错也要刷一遍：可能已经压掉了一部分文件，列表不能停在旧样子
                        InstanceSizeStore.Remove(Instance.PathVersion)
                        McInstanceListForceRefresh = True
                        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                        MyMsgBox(Loader.Error.GetDisplay(True), If(IsArchive, "归档失败", "解压失败"), IsWarn:=True)
                End Select
            End Sub
        Combo.Start(0)
    End Sub

#End Region

#Region "页面事件"

    ''' <summary>
    ''' 已归档版本的外观：名字灰掉、名字后面挂一个琥珀色「已归档」色标、左侧图标压暗、摘要行换成琥珀色。
    ''' 目的是让人扫一眼就能把"封存着、要用得先解压"的版本和正常版本分开。
    ''' </summary>
    Private Shared Sub ApplyArchivedLook(Item As MyListItem)
        Try
            '名字整体灰掉
            For Each Inline In Item.Inlines
                Dim R As Run = TryCast(Inline, Run)
                If R IsNot Nothing Then R.Foreground = New MyColor(150, 150, 150)
            Next
            '名字后面挂色标
            Item.Inlines.Add(New Run("  已归档  ") With {
                .FontSize = 10,
                .Foreground = New MyColor(255, 255, 255),
                .Background = New MyColor(184, 121, 46)})
            '左侧图标压暗
            If Item.PathLogo IsNot Nothing Then Item.PathLogo.Opacity = 0.28
            '摘要行（已归档 · 多大 → 多小）用琥珀色，正常版本那行是灰色
            If Item.LabInfo IsNot Nothing Then
                Item.LabInfo.Foreground = New MyColor(168, 110, 40)
                Item.LabInfo.Opacity = 0.95
                Item.LabInfo.FontWeight = FontWeights.SemiBold
            End If
        Catch ex As Exception
            Logger.Warn(ex, "应用归档外观失败")
        End Try
    End Sub

    ''' <summary>单击列表项时的处理。</summary>
    Public Shared Sub Item_Click(sender As MyListItem, e As EventArgs)
        Dim Instance As McInstance = sender.Tag
        '已归档的版本：先询问是否解压
        If ModArchive.IsArchived(Instance.PathVersion) Then
            ExtractInstance(Instance)
            Return
        End If
        If McInstanceLock.IsLocked(Instance) Then
            Hint("该版本正在被修改，请稍后再选择！", HintType.Red)
            Return
        End If
        If New McInstance(Instance.PathVersion).Check Then
            '正常版本
            McInstanceSelected = Instance
            FrmMain.PageBack()
        Else
            '错误版本
            PageInstanceOverall.OpenInstanceFolder(Instance)
        End If
    End Sub

    Private Sub BtnDownload_Click(sender As Object, e As EventArgs) Handles BtnEmptyDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
    End Sub

    '修改此代码时，同时修改 PageInstanceOverall 中的代码
    Public Shared Sub DeleteInstance(Item As MyListItem, Instance As McInstance)
        Try
            If McInstanceLock.IsLocked(Instance) Then
                Hint("该版本正在被修改，请稍后再删除！", HintType.Red)
                Return
            End If
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = Instance.State <> McInstance.McInstanceState.Error AndAlso Instance.PathIndie <> McFolderSelected
            Dim SavesFolder = DirectoryUtils.GetInfo(Instance.PathIndie & "saves\")
            Dim SaveEntries = If(IsHintIndie AndAlso SavesFolder.Exists, SavesFolder.EnumerateDirectories.OrderByDescending(Function(Save) Save.LastWriteTime).Select(Function(Save) $"{Save.Name}（上次修改：{StringUtils.FormatTimeSpan(Save.LastWriteTime - Date.Now, False)}）").ToList, New List(Of String))
            '已归档的版本：内容全在 _archived.zip 里，删除等于把唯一的一份备份删掉，单独提醒一句
            Dim ArchiveWarn As String = If(ModArchive.IsArchived(Instance.PathVersion),
                vbCrLf & "注意：该版本已归档，全部内容都在 _archived.zip 里，压缩包删掉就再也找不回来了" &
                If(IsShiftPressed, "（Shift 删除不会进回收站）！", "（会进回收站，但整套版本都在这一个文件里）。"), "")
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除版本 {Instance.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "该版本对应的存档、资源包、Mod 等文件也将被一并删除！", "") &
                        ArchiveWarn &
                        If(SaveEntries.Any, vbCrLf & vbCrLf & "这会删除以下存档：" & vbCrLf & "· " & SaveEntries.Join(vbCrLf & "· "), ""),
                        "版本删除确认", , "取消",, True)
                Case 1
                    Instance.ResetSettingsCache()
                    DirectoryUtils.Delete(Instance.PathVersion, Not IsShiftPressed)
                    Hint("版本 " & Instance.Name & " 已删除！", HintType.Green)
                Case 2
                    Return
            End Select
            '从 UI 中移除
            If Instance.DisplayType = McInstanceCardType.Hidden OrElse Not Instance.IsStar Then
                '仅出现在当前卡片
                Dim Parent As StackPanel = Item.Parent
                If Parent.Children.Count > 2 Then '当前的项目与一个占位符
                    '删除后还有剩
                    Dim Card As MyCard = Parent.Parent
                    Card.Title = Card.Title.Replace(Parent.Children.Count - 1, Parent.Children.Count - 2) '有一个占位符
                    Parent.Children.Remove(Item)
                    If McInstanceSelected IsNot Nothing AndAlso PathUtils.IsSame(Instance.PathVersion, McInstanceSelected.PathVersion) Then
                        '删除当前版本就更改选择
                        McInstanceSelected = MyVirtualizingElement.TryInit(Parent.Children(0)).Tag
                    End If
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.UpdateOnly, MaxDepth:=1, ExtraPath:="versions\")
                Else
                    '删除后没剩了
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                End If
            Else
                '同时出现在当前卡片与收藏夹
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            End If
        Catch ex As OperationCanceledException
            Logger.Warn(ex, $"删除版本 {Instance.Name} 被主动取消")
        Catch ex As Exception
            Logger.Error(ex, $"删除版本 {Instance.Name} 失败", LogBehavior.Alert)
        End Try
    End Sub

    Public Sub BtnEmptyDownload_Loaded() Handles BtnEmptyDownload.Loaded
        Dim NewVisibility = If((Settings.Get(Of Boolean)("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow) OrElse ShowHidden, Visibility.Collapsed, Visibility.Visible)
        If BtnEmptyDownload.Visibility <> NewVisibility Then
            BtnEmptyDownload.Visibility = NewVisibility
            PanLoad.TriggerForceResize()
        End If
    End Sub

#End Region

End Class
