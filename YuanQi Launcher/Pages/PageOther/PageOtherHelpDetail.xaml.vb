Public Class PageOtherHelpDetail

    ''' <summary>
    ''' 已抓取过的页面 XAML 内容，按 XAML 地址缓存。
    ''' </summary>
    Public Shared ReadOnly PageCache As New System.Collections.Concurrent.ConcurrentDictionary(Of String, String)
    ''' <summary>
    ''' 已解析好的页面控件，重复打开同一页面时直接复用，跳过解析。
    ''' </summary>
    Public Shared ReadOnly ElementCache As New System.Collections.Concurrent.ConcurrentDictionary(Of String, FrameworkElement)
    Private Shared ReadOnly Prefetching As New System.Collections.Concurrent.ConcurrentDictionary(Of String, Boolean)

    ''' <summary>页面 XAML 的磁盘缓存目录：避免每次启动都重新下载同一份页面。</summary>
    Private Shared ReadOnly Property PageDiskDir As String
        Get
            Return Paths.Base & "YQL\Cache\Page\"
        End Get
    End Property

    ''' <summary>磁盘缓存有效期（分钟）= 半个月：有效期内完全不联网，过期后联网失败还会用旧缓存兜底。
    ''' 新闻页地址带 ver 参数，新版本发布时 URL 就变了，所以长缓存不会看到旧版本内容。</summary>
    Private Const PageDiskMaxAgeMinutes As Integer = 15 * 24 * 60

    ''' <summary>URL → 缓存文件名（MD5，避免 ? &amp; = 等字符影响文件名）。</summary>
    Private Shared Function PageDiskFile(XamlUrl As String) As String
        Try
            Using Md5 = System.Security.Cryptography.MD5.Create()
                Dim Bytes = Md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(XamlUrl))
                Dim Sb As New System.Text.StringBuilder()
                For Each B In Bytes
                    Sb.Append(B.ToString("x2"))
                Next
                Return IO.Path.Combine(PageDiskDir, Sb.ToString() & ".xaml")
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "计算页面缓存文件名失败")
            Return Nothing
        End Try
    End Function

    ''' <summary>读磁盘缓存。MaxAgeMinutes=0 表示"不管多旧都要"。</summary>
    Private Shared Function TryReadPageDisk(XamlUrl As String, MaxAgeMinutes As Integer, ByRef Content As String) As Boolean
        Content = Nothing
        Try
            Dim F = PageDiskFile(XamlUrl)
            If String.IsNullOrEmpty(F) OrElse Not FileUtils.Exists(F) Then Return False
            If MaxAgeMinutes > 0 Then
                If (Date.Now - IO.File.GetLastWriteTime(F)).TotalMinutes > MaxAgeMinutes Then Return False
            End If
            Content = FileUtils.ReadAsString(F)
            Return Not String.IsNullOrWhiteSpace(Content)
        Catch ex As Exception
            Logger.Warn(ex, "读取页面磁盘缓存失败")
            Return False
        End Try
    End Function

    Private Shared Sub WritePageDisk(XamlUrl As String, Content As String)
        Try
            Dim F = PageDiskFile(XamlUrl)
            If String.IsNullOrEmpty(F) Then Return
            DirectoryUtils.Create(PageDiskDir)
            FileUtils.Write(F, Content)
        Catch ex As Exception
            Logger.Warn(ex, "写入页面磁盘缓存失败")
        End Try
    End Sub

    ''' <summary>
    ''' 从指定地址打开帮助 / 新闻页面：先立即切页并显示加载状态，再异步填入内容。
    ''' </summary>
    ''' <param name="Location">JSON 地址；也可直接给 XAML 地址。</param>
    ''' <param name="KnownTitle">已知标题，传入后可跳过标题 JSON 请求。</param>
    Public Shared Sub Open(Location As String, Optional KnownTitle As String = Nothing)
        If String.IsNullOrWhiteSpace(Location) Then Return
        Dim XamlUrl As String = NormalizeXamlUrl(Location)
        Dim SwPage As Stopwatch = Stopwatch.StartNew()
        Dim Page As PageOtherHelpDetail = Nothing
        Dim SwitchPage As Action =
        Sub()
            Try
                Page = New PageOtherHelpDetail
                Dim T1 As Long = SwPage.ElapsedMilliseconds
                Page.InitLoading()
                Dim T2 As Long = SwPage.ElapsedMilliseconds
                FrmMain.PageChange(New FormMain.PageStackData With {
                    .Page = FormMain.PageType.HelpDetail,
                    .Additional = {If(String.IsNullOrWhiteSpace(KnownTitle), "正在加载…", KnownTitle), Page}
                })
                Logger.Info($"帮助页面切换耗时：创建 {T1} ms / 加载态 {T2 - T1} ms / 切页 {SwPage.ElapsedMilliseconds - T2} ms")
            Catch ex As Exception
                Logger.Error(ex, "打开帮助页面失败", LogBehavior.Toast)
                Page = Nothing
            End Try
        End Sub
        ' 已经在 UI 线程时直接执行，避免 RunInUi 等待造成的延迟
        If FrmMain.Dispatcher.CheckAccess() Then
            SwitchPage()
        Else
            RunInUi(SwitchPage, True)
        End If
        If Page Is Nothing Then Return
        Dim Sw As Stopwatch = Stopwatch.StartNew()

        '已有解析好的控件时，直接填充（秒开）
        '注意：这里必须"同步填完再启动后台线程"——否则后台线程会先跑到 IsFilled 检查处，
        '看到还没填就又解析一遍（那一遍 0.5s+ 的解析就是"进入新闻页面卡"的来源）。
        Dim CachedElement As FrameworkElement = Nothing
        If ElementCache.TryGetValue(XamlUrl, CachedElement) AndAlso CanReuseCachedElement(CachedElement) Then
            '先标记"已填充"，否则后台线程会抢先看到未填充、又完整解析一遍（那 0.5s 就是卡顿来源）
            Page.IsFilled = True
            Dim FillNow As System.Threading.ThreadStart =
            Sub()
                Try
                    Page.FillElement(CachedElement, Batch:=False) '复用老控件：直接挂，不再拆批
                    UpdateTitle(Page, KnownTitle)
                Catch ex As Exception
                    Logger.Warn(ex, "复用已解析页面失败")
                    Page.ShowError("页面显示失败：" & ex.GetDisplay(False))
                End Try
            End Sub
            '等页面切换动画先起跑，再把内容分批挂进去，动画期间不会有重活
            If FrmMain.Dispatcher.CheckAccess() Then
                AniStart({AaCode(FillNow, 140)}, "HelpDetail FillCache")
            Else
                RunInUi(Sub() AniStart({AaCode(FillNow, 140)}, "HelpDetail FillCache"), True)
            End If
        End If

        RunInNewThread(
        Sub()
            Try
                '标题
                Dim Title As String = KnownTitle
                If String.IsNullOrWhiteSpace(Title) Then
                    Try
                        Dim JsonText As String = NetRequestByClient(Location, Timeout:=12000, SimulateBrowserHeaders:=True)
                        Dim Json As JObject = JsonText.DeserializeJson()
                        If Json IsNot Nothing AndAlso Json("Title") IsNot Nothing Then Title = Json("Title").ToString
                    Catch ex As Exception
                        Logger.Warn(ex, "读取帮助页面标题失败")
                    End Try
                End If
                If String.IsNullOrWhiteSpace(Title) Then Title = "帮助"
                RunInUi(Sub() UpdateTitle(Page, Title), True)

                '内容（若刚才已用缓存元素填充，则不再重复解析）
                If Page.IsFilled Then Return
                Dim Content As String = Nothing
                If Not PageCache.TryGetValue(XamlUrl, Content) OrElse String.IsNullOrWhiteSpace(Content) Then
                    Content = FetchPageContent(XamlUrl)
                    If String.IsNullOrWhiteSpace(Content) Then Throw New Exception("页面内容为空")
                    If PageCache.Count > 30 Then PageCache.Clear()
                    PageCache(XamlUrl) = Content
                End If
                Logger.Info($"帮助页面内容就绪：{XamlUrl}（{Sw.ElapsedMilliseconds} ms，{Content.Length} 字符）")
                '文本预处理（转义 + 去 xmlns）在后台线程做掉，UI 线程只负责解析
                Dim Prepared As String = PrepareContent(Content)
                Dim FillAction As Action =
                Sub()
                    Try
                        Page.ShowLoadingTip("正在解析页面…")
                        Page.FillContentPrepared(Prepared, XamlUrl)
                        Logger.Info($"帮助页面渲染完成：{XamlUrl}（{Sw.ElapsedMilliseconds} ms）")
                    Catch ex As Exception
                        Logger.Error(ex, "解析帮助页面失败")
                        Page.ShowError("页面解析失败：" & ex.GetDisplay(False))
                    End Try
                End Sub
                If FrmMain.Dispatcher.CheckAccess() Then
                    FillAction()
                Else
                    RunInUi(FillAction)
                End If
            Catch ex As Exception
                Logger.Error(ex, "打开帮助页面失败", LogBehavior.Alert)
                RunInUi(Sub() Page.ShowError("打开页面失败：" & ex.GetDisplay(False)))
            End Try
        End Sub, "HelpDetail Open")
    End Sub

    ''' <summary>
    ''' 后台预取页面内容，用户点击时可以立刻显示。
    ''' </summary>
    Public Shared Sub Prefetch(Location As String)
        Try
            If String.IsNullOrWhiteSpace(Location) Then Return
            Dim XamlUrl As String = NormalizeXamlUrl(Location)
            If PageCache.ContainsKey(XamlUrl) Then Return
            If Prefetching.ContainsKey(XamlUrl) Then Return
            Prefetching(XamlUrl) = True
            RunInNewThread(
            Sub()
                Try
                    If PageCache.ContainsKey(XamlUrl) Then Return
                    Dim Content As String = FetchPageContent(XamlUrl)
                    If String.IsNullOrWhiteSpace(Content) Then Return
                    If PageCache.Count > 30 Then PageCache.Clear()
                    PageCache(XamlUrl) = Content
                    '这里只缓存"内容文本"，不在后台解析成控件：
                    '解析 400KB 的新闻页要 0.6~1.5s 且必须占用 UI 线程，放在启动后会直接卡在开屏动画/首屏之后。
                    '解析留给用户真正点开时做一次（结果会缓存，之后复用）。
                Catch ex As Exception
                    Logger.Warn(ex, "预取帮助页面失败")
                Finally
                    Prefetching.TryRemove(XamlUrl, Nothing)
                End Try
            End Sub, "HelpDetail Prefetch")
        Catch ex As Exception
            Logger.Warn(ex, "预取帮助页面失败")
        End Try
    End Sub

    Private Shared Sub UpdateTitle(Page As PageOtherHelpDetail, Title As String)
        Try
            If String.IsNullOrWhiteSpace(Title) OrElse Title = "正在加载…" Then Return
            Dim Stack = FrmMain.PageCurrent
            If Stack Is Nothing OrElse Stack.Additional Is Nothing Then Return
            Stack.Additional(0) = Title
            FrmMain.PageNameRefresh(Stack)
        Catch ex As Exception
            Logger.Warn(ex, "更新帮助页面标题失败")
        End Try
    End Sub

    ''' <summary>
    ''' 把 JSON 地址规范成对应的 XAML 地址（支持 xxx.json?ver=1.2.3）。
    ''' </summary>
    Private Shared Function NormalizeXamlUrl(Location As String) As String
        Dim XamlUrl As String = Location
        Dim LowerUrl As String = XamlUrl.ToLower
        If LowerUrl.Contains(".json?") Then
            Dim SplitIndex As Integer = LowerUrl.IndexOf(".json?")
            XamlUrl = XamlUrl.Substring(0, SplitIndex) & ".xaml" & XamlUrl.Substring(SplitIndex + 5)
        ElseIf LowerUrl.EndsWith(".json") Then
            XamlUrl = XamlUrl.Substring(0, XamlUrl.Length - 5) & ".xaml"
        End If
        Return XamlUrl
    End Function

    ''' <summary>
    ''' 抓取页面 XAML：先看磁盘缓存（45 分钟内不联网）→ 单线程 → 多线程 → 过期缓存兜底。
    ''' </summary>
    Private Shared Function FetchPageContent(XamlUrl As String) As String
        Dim Cached As String = Nothing
        '① 磁盘缓存仍然新鲜：直接用，不联网（这是"每次启动都要重新加载"的主要优化点）
        If TryReadPageDisk(XamlUrl, PageDiskMaxAgeMinutes, Cached) Then
            Logger.Info($"帮助页面命中磁盘缓存：{XamlUrl}")
            Return Cached
        End If
        '② 联网抓取
        Try
            Dim Content As String = NetRequestByClient(XamlUrl, Timeout:=20000, SimulateBrowserHeaders:=True)
            If Not String.IsNullOrWhiteSpace(Content) Then
                WritePageDisk(XamlUrl, Content)
                Return Content
            End If
        Catch ex As Exception
            Logger.Warn(ex, "单线程获取页面内容失败，改用多线程重试")
        End Try
        Try
            Dim Content2 As String = NetRequestByClientMultiple(XamlUrl, Timeout:=30000)
            If Not String.IsNullOrWhiteSpace(Content2) Then
                WritePageDisk(XamlUrl, Content2)
                Return Content2
            End If
        Catch ex As Exception
            Logger.Warn(ex, "多线程获取页面内容失败")
        End Try
        '③ 实在拿不到：退回过期缓存，总比空白页好
        If TryReadPageDisk(XamlUrl, 0, Cached) Then
            Logger.Warn($"帮助页面联网失败，改用过期缓存：{XamlUrl}")
            Return Cached
        End If
        Return Nothing
    End Function

#Region "页面实例"

    ''' <summary>
    ''' 是否已经填入了正文（避免重复解析）。
    ''' </summary>
    Public Property IsFilled As Boolean = False

    ''' <summary>
    ''' 显示加载状态。
    ''' </summary>
    Public Sub InitLoading()
        Try
            PanCustom.Children.Clear()
            Dim Load As New MyLoading With {
                .Text = "正在加载页面…",
                .AutoRun = True,
                .HorizontalAlignment = HorizontalAlignment.Center,
                .Margin = New Thickness(60, 80, 60, 60)
            }
            PanCustom.Children.Add(Load)
        Catch ex As Exception
            Logger.Warn(ex, "显示加载状态失败")
        End Try
    End Sub

    ''' <summary>
    ''' 更新加载提示文字。
    ''' </summary>
    Public Sub ShowLoadingTip(Text As String)
        Try
            PanCustom.Children.Clear()
            Dim Load As New MyLoading With {
                .Text = Text,
                .AutoRun = True,
                .HorizontalAlignment = HorizontalAlignment.Center,
                .Margin = New Thickness(60, 80, 60, 60)
            }
            PanCustom.Children.Add(Load)
        Catch ex As Exception
            Logger.Warn(ex, "更新加载提示失败")
        End Try
    End Sub

    ''' <summary>
    ''' 显示错误信息。
    ''' </summary>
    Public Sub ShowError(Text As String)
        Try
            PanCustom.Children.Clear()
            Dim Label As New TextBlock With {
                .Text = Text,
                .FontSize = 13,
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(40, 60, 40, 40),
                .HorizontalAlignment = HorizontalAlignment.Center
            }
            Label.SetResourceReference(TextBlock.ForegroundProperty, "BrushText3")
            PanCustom.Children.Add(Label)
            IsFilled = True
        Catch ex As Exception
            Logger.Warn(ex, "显示错误信息失败")
        End Try
    End Sub

    ''' <summary>
    ''' 直接放入已解析好的控件。
    ''' 新闻页那种几百 KB 的 XAML 会解析出上千个元素，一次性挂上去要卡 0.5s+；
    ''' 这里先挂外层，再把它的子元素按小批（每批 12 个）在 UI 空闲时分帧塞进去，
    ''' 首屏立刻可见、滚动条立刻可用，剩下的内容在后台补完，不会整页冻住。
    ''' </summary>
    Public Sub FillElement(Element As FrameworkElement, Optional Batch As Boolean = True)
        Dim Sw As Stopwatch = Stopwatch.StartNew()
        PanCustom.Children.Clear()
        If Element.Parent IsNot Nothing Then
            Try
                CType(Element.Parent, Panel).Children.Remove(Element)
            Catch
            End Try
        End If
        Dim Host = TryCast(Element, Panel)
        '分批挂载只在"刚解析出来、还没进过可视树"时做；
        '复用的老控件本身已经完整，再拆一次子元素只会白白多花几百毫秒（实测 381ms → 几十 ms）
        If Batch AndAlso Host IsNot Nothing AndAlso Host.Children.Count > 20 Then
            '先清空子元素，外层先挂上去（视口与滚动条立刻可用）
            Dim Children As New List(Of UIElement)(Host.Children)
            Host.Children.Clear()
            PanCustom.Children.Add(Element)
            IsFilled = True
            PanBack.ScrollToTop()
            AddChildrenInBatches(Host, Children)
            Logger.Info($"帮助页面分批挂载：共 {Children.Count} 个子元素（{Sw.ElapsedMilliseconds} ms）")
        Else
            PanCustom.Children.Add(Element)
            IsFilled = True
            PanBack.ScrollToTop()
            Logger.Info($"帮助页面挂载完成（{Sw.ElapsedMilliseconds} ms）")
        End If
    End Sub

    ''' <summary>把子元素分批加进面板：每批 12 个，其余排到 UI 空闲时继续，避免一次性布局上千元素。</summary>
    Private Sub AddChildrenInBatches(Host As Panel, Children As List(Of UIElement))
        Dim Idx As Integer = 0
        Dim Batch As Action = Nothing
        Batch =
            Sub()
                Try
                    If Host Is Nothing OrElse PanCustom Is Nothing Then Return
                    Dim N As Integer = 0
                    While Idx < Children.Count AndAlso N < 12
                        Host.Children.Add(Children(Idx))
                        Idx += 1
                        N += 1
                    End While
                    If Idx < Children.Count Then
                        Dispatcher.BeginInvoke(Windows.Threading.DispatcherPriority.Background, Batch)
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "分批挂载帮助页面失败")
                End Try
            End Sub
        Batch()
    End Sub

    ''' <summary>
    ''' 解析 XAML 内容并显示，同时缓存解析结果。
    ''' </summary>
    Public Sub FillContent(Content As String, Optional CacheKey As String = Nothing)
        FillContentPrepared(PrepareContent(Content), CacheKey)
    End Sub

    ''' <summary>
    ''' 把转义 / 去 xmlns / 套外层 StackPanel 这些纯字符串处理单独抽出来：
    ''' 它们是"重活但不碰 UI"，可以放到后台线程，UI 线程只做不可省的 XAML→对象 解析。
    ''' </summary>
    Private Shared Function PrepareContent(Content As String) As String
        Content = ArgumentReplace(Content, AddressOf StringUtils.XmlEscape)
        Do While Content.Contains("xmlns")
            Content = Content.RegexReplace("xmlns[^""']*(""|')[^""']*(""|')", "").Replace("xmlns", "")
        Loop
        Return "<StackPanel xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:sys=""clr-namespace:System;assembly=mscorlib"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:YuanQi;assembly=YuanQi Launcher"" xmlns:core=""clr-namespace:MeloongCore;assembly=MeloongCore"" xmlns:corewpf=""clr-namespace:MeloongCore.Wpf;assembly=MeloongCore.Wpf"">" & Content & "</StackPanel>"
    End Function

    ''' <summary>用已经预处理过的内容解析并显示（解析必须在 UI 线程）。</summary>
    Public Sub FillContentPrepared(Prepared As String, Optional CacheKey As String = Nothing)
        Try
            '新闻页那种 40 万字符 / 745 个块的正文，一次性解析 + 排版要 0.6s 以上，
            '全部压在 UI 线程上就是"进页面卡一下"。这里先只渲染第一段，其余按滚动位置懒加载。
            Dim Head As String = Nothing
            Dim Sections As New List(Of String)
            If TryBuildSlices(Prepared, Head, Sections) Then
                Try
                    FillSliced(Head, Sections, CacheKey)
                    Return
                Catch ex As Exception
                    Logger.Warn(ex, "首屏渲染帮助页面失败，改用整体解析")
                End Try
            End If
            Dim Element As FrameworkElement = CType(GetObjectFromXML(Prepared), FrameworkElement)
            FillElement(Element)
            CacheElement(CacheKey, Element)
        Catch ex As Exception
            Logger.Info($"自定义信息内容：{vbCrLf}{Prepared}")
            Logger.Error(ex, "加载帮助 XAML 文件失败", LogBehavior.Alert)
            ShowError("页面解析失败：" & ex.GetDisplay(False))
        End Try
    End Sub

    Private Shared Sub CacheElement(CacheKey As String, Element As FrameworkElement)
        If String.IsNullOrWhiteSpace(CacheKey) OrElse Element Is Nothing Then Return
        If ElementCache.Count > 6 Then ElementCache.Clear()
        ElementCache(CacheKey) = Element
    End Sub

    ''' <summary>
    ''' 缓存的控件能否拿来复用：没父级（新建好还没挂上去）可以；
    ''' 挂在已经废弃的旧帮助页面上也可以拆下来复用；但正显示在当前页面里的不能抢。
    ''' </summary>
    Private Shared Function CanReuseCachedElement(El As FrameworkElement) As Boolean
        If El Is Nothing Then Return False
        Try
            If El.Parent Is Nothing Then Return True
            Dim V = TryCast(El, Visual)
            If V IsNot Nothing AndAlso FrmMain.PageRight IsNot Nothing AndAlso V.IsDescendantOf(FrmMain.PageRight) Then Return False
            Return TypeOf El.Parent Is Panel
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Sub PageOtherHelpDetail_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToTop()
    End Sub

#End Region

#Region "正文分段懒加载"

    ''' <summary>
    ''' 每段正文的字符预算。新闻页正文 39 万字符，拆成十几段后每段解析约 40~60 ms，
    ''' 用户滚动到哪里就补哪一段，界面不会出现半秒级的整体停顿。
    ''' </summary>
    Private Const SectionChars As Integer = 30000

    ''' <summary>离正文底部还有这么远就开始准备下一段（约一屏半的缓冲）。</summary>
    Private Const SectionBufferPx As Double = 1200

    ''' <summary>跳过空白、注释、CDATA、处理指令，定位到下一个标签的 '&lt;'。</summary>
    Private Shared Function SkipToTag(Xml As String, Start As Integer) As Integer
        Dim I As Integer = Start
        Do
            Dim Lt As Integer = Xml.IndexOf("<"c, I)
            If Lt < 0 Then Return Xml.Length
            I = Lt
            If String.CompareOrdinal(Xml, I, "<!--", 0, 4) = 0 Then
                Dim E As Integer = Xml.IndexOf("-->", I + 4, StringComparison.Ordinal)
                If E < 0 Then Return Xml.Length
                I = E + 3
            ElseIf String.CompareOrdinal(Xml, I, "<![CDATA[", 0, 9) = 0 Then
                Dim E As Integer = Xml.IndexOf("]]>", I + 9, StringComparison.Ordinal)
                If E < 0 Then Return Xml.Length
                I = E + 3
            ElseIf String.CompareOrdinal(Xml, I, "<?", 0, 2) = 0 Then
                Dim E As Integer = Xml.IndexOf("?>", I + 2, StringComparison.Ordinal)
                If E < 0 Then Return Xml.Length
                I = E + 2
            Else
                Return I
            End If
        Loop
    End Function

    ''' <summary>从 Start 处的 '&lt;' 读一个标签，返回 '&gt;' 之后的位置。</summary>
    Private Shared Function ScanTagEnd(Xml As String, Start As Integer, ByRef IsClose As Boolean, ByRef IsSelfClose As Boolean, ByRef TagName As String) As Integer
        IsClose = False
        IsSelfClose = False
        TagName = ""
        Dim I As Integer = Start + 1
        If I < Xml.Length AndAlso Xml(I) = "/"c Then
            IsClose = True
            I += 1
        End If
        Dim NameStart As Integer = I
        While I < Xml.Length
            Dim Ch As Char = Xml(I)
            If Ch = " "c OrElse Ch = ">"c OrElse Ch = "/"c OrElse Ch = vbTab OrElse Ch = vbCr OrElse Ch = vbLf Then Exit While
            I += 1
        End While
        TagName = Xml.Substring(NameStart, I - NameStart)
        Dim Quote As Char = ChrW(0)
        While I < Xml.Length
            Dim Ch As Char = Xml(I)
            If Quote <> ChrW(0) Then
                If Ch = Quote Then Quote = ChrW(0)
            ElseIf Ch = """"c OrElse Ch = "'"c Then
                Quote = Ch
            ElseIf Ch = ">"c Then
                IsSelfClose = I > Start AndAlso Xml(I - 1) = "/"c
                Return I + 1
            End If
            I += 1
        End While
        Return -1
    End Function

    ''' <summary>返回与 Start 处元素配对的结束标签之后的位置。</summary>
    Private Shared Function FindElementEnd(Xml As String, Start As Integer) As Integer
        Dim Depth As Integer = 1
        Dim I As Integer = Start
        Do
            I = SkipToTag(Xml, I)
            If I >= Xml.Length Then Return -1
            Dim IsC As Boolean = False
            Dim IsS As Boolean = False
            Dim Nm As String = ""
            Dim E As Integer = ScanTagEnd(Xml, I, IsC, IsS, Nm)
            If E < 0 Then Return -1
            If IsC Then
                Depth -= 1
                If Depth = 0 Then Return E
            ElseIf Not IsS Then
                Depth += 1
            End If
            I = E
        Loop
    End Function

    ''' <summary>收集某个元素在文本里的直接子元素区间，CloseTagStart 返回它的结束标签起点。</summary>
    Private Shared Function CollectElementChildren(Xml As String, OpenEnd As Integer, ByRef CloseTagStart As Integer) As List(Of Integer())
        CloseTagStart = -1
        Dim Out As New List(Of Integer())
        Dim I As Integer = OpenEnd
        Do
            I = SkipToTag(Xml, I)
            If I >= Xml.Length Then Return Nothing
            If I + 1 < Xml.Length AndAlso Xml(I + 1) = "/"c Then
                CloseTagStart = I
                Return Out
            End If
            Dim IsC As Boolean = False
            Dim IsS As Boolean = False
            Dim Nm As String = ""
            Dim E As Integer = ScanTagEnd(Xml, I, IsC, IsS, Nm)
            If E < 0 Then Return Nothing
            Dim ElEnd As Integer
            If IsS Then
                ElEnd = E
            Else
                ElEnd = FindElementEnd(Xml, E)
                If ElEnd < 0 Then Return Nothing
            End If
            Out.Add({I, ElEnd})
            I = ElEnd
        Loop
    End Function

    ''' <summary>
    ''' 把正文里的 FlowDocument 拆成"首段 + 后续若干段"。
    ''' 首段就是原页面，只是正文只留前面一段；后续每段都是一个独立的 FlowDocumentScrollViewer 片段，
    ''' 用户滚到附近时再解析并插到正文后面（已经排好版的段落不会因为插入新段而重新排版）。
    ''' 返回 False 表示这份内容不适合拆（页面太小或结构不认识），调用方退回整体解析。
    ''' </summary>
    Private Shared Function TryBuildSlices(Prepared As String, ByRef Head As String, ByRef Sections As List(Of String)) As Boolean
        Head = Nothing
        Sections = New List(Of String)
        Try
            '① 外层包装标签带着全部 xmlns，分块时直接复用
            Dim WrapEnd As Integer = Prepared.IndexOf(">"c)
            If WrapEnd < 0 Then Return False
            Dim WrapOpen As String = Prepared.Substring(0, WrapEnd + 1)
            Dim SpaceAt As Integer = WrapOpen.IndexOf(" "c)
            If SpaceAt < 0 Then Return False
            Dim NsAttrs As String = WrapOpen.Substring(SpaceAt)

            '② 找正文的 FlowDocument（跳过 FlowDocumentScrollViewer）
            Dim DocOpenEnd As Integer = -1
            Dim Search As Integer = 0
            Do
                Dim P As Integer = Prepared.IndexOf("<FlowDocument", Search, StringComparison.Ordinal)
                If P < 0 Then Return False
                Dim IsC As Boolean = False
                Dim IsS As Boolean = False
                Dim Nm As String = ""
                Dim E As Integer = ScanTagEnd(Prepared, P, IsC, IsS, Nm)
                If E < 0 Then Return False
                If Nm = "FlowDocument" Then
                    If IsS Then Return False
                    DocOpenEnd = E
                    Exit Do
                End If
                Search = E
            Loop
            Dim CloseStart As Integer = -1
            Dim Kids As List(Of Integer()) = CollectElementChildren(Prepared, DocOpenEnd, CloseStart)
            If CloseStart < 0 OrElse Kids Is Nothing OrElse Kids.Count < 40 Then Return False

            '③ 根 Resources：正文里到处是 {StaticResource Lp} 这类显式样式，分段解析时必须带上
            Const ResOpen As String = "<StackPanel.Resources>"
            Const ResClose As String = "</StackPanel.Resources>"
            Dim ResXml As String = ""
            Dim ResStart As Integer = Prepared.IndexOf(ResOpen, StringComparison.Ordinal)
            If ResStart >= 0 Then
                Dim ResEnd As Integer = Prepared.IndexOf(ResClose, ResStart, StringComparison.Ordinal)
                If ResEnd > ResStart Then ResXml = Prepared.Substring(ResStart + ResOpen.Length, ResEnd - ResStart - ResOpen.Length)
            End If
            If String.IsNullOrWhiteSpace(ResXml) Then Return False

            '④ 按字符预算切段（属性元素一律留在首段）
            Dim HeadText As New StringBuilder()
            Dim SecText As New StringBuilder()
            Dim HeadChars As Integer = 0
            Dim SecChars As Integer = 0
            Dim HeadDone As Boolean = False
            For Each Kid As Integer() In Kids
                Dim Text As String = Prepared.Substring(Kid(0), Kid(1) - Kid(0))
                Dim IsC As Boolean = False
                Dim IsS As Boolean = False
                Dim Nm As String = ""
                ScanTagEnd(Prepared, Kid(0), IsC, IsS, Nm)
                If Not HeadDone OrElse Nm.Contains(".") Then
                    HeadText.Append(Text)
                    If Not Nm.Contains(".") Then
                        HeadChars += Text.Length
                        If HeadChars >= SectionChars Then HeadDone = True
                    End If
                    Continue For
                End If
                If SecChars > 0 AndAlso SecChars + Text.Length > SectionChars Then
                    Sections.Add(SecText.ToString())
                    SecText.Clear()
                    SecChars = 0
                End If
                SecText.Append(Text)
                SecChars += Text.Length
            Next
            If SecText.Length > 0 Then Sections.Add(SecText.ToString())
            If Sections.Count = 0 Then Return False

            '⑤ 首段：正文换成前一段，其余结构原样保留
            Head = Prepared.Substring(0, DocOpenEnd) & HeadText.ToString() & Prepared.Substring(CloseStart)

            '⑥ 后续每段：套一个带同样样式资源的 FlowDocumentScrollViewer
            Const SecHead As String = "<FlowDocumentScrollViewer{0}<FlowDocument><FlowDocument.Resources>{1}</FlowDocument.Resources>"
            Const SecTail As String = "</FlowDocument></FlowDocumentScrollViewer>"
            Dim Prefix As String = String.Format(SecHead, NsAttrs, ResXml)
            For I As Integer = 0 To Sections.Count - 1
                Sections(I) = Prefix & Sections(I) & SecTail
            Next
            Return True
        Catch ex As Exception
            Logger.Warn(ex, "拆分帮助页面正文失败")
            Return False
        End Try
    End Function

    ''' <summary>在解析好的界面里找到正文 FlowDocumentScrollViewer。</summary>
    Private Shared Function FindFlowDocumentViewer(El As DependencyObject) As FlowDocumentScrollViewer
        If El Is Nothing Then Return Nothing
        Dim Fdv As FlowDocumentScrollViewer = TryCast(El, FlowDocumentScrollViewer)
        If Fdv IsNot Nothing AndAlso Fdv.Document IsNot Nothing Then Return Fdv
        Dim V As Visual = TryCast(El, Visual)
        If V Is Nothing Then Return Nothing
        Dim Count As Integer = VisualTreeHelper.GetChildrenCount(V)
        For I As Integer = 0 To Count - 1
            Dim R As FlowDocumentScrollViewer = FindFlowDocumentViewer(VisualTreeHelper.GetChild(V, I))
            If R IsNot Nothing Then Return R
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 先只渲染第一段正文（几十毫秒就能看到内容），剩下的等用户滚到附近再一段段补上。
    ''' 每段是独立的 FlowDocumentScrollViewer：插入新段不会让已经排好版的段落重新排版，
    ''' 所以补段时只会占用几十毫秒，不会出现整页停顿。
    ''' </summary>
    Private Sub FillSliced(Head As String, Sections As List(Of String), CacheKey As String)
        Dim Sw As Stopwatch = Stopwatch.StartNew()
        Dim Element As FrameworkElement = CType(GetObjectFromXML(Head), FrameworkElement)
        FillElement(Element, Batch:=False)
        Dim Fdv As FlowDocumentScrollViewer = FindFlowDocumentViewer(Element)
        If Fdv Is Nothing OrElse Fdv.Document Is Nothing Then Throw New Exception("没有找到正文 FlowDocument")
        Dim Host As Panel = TryCast(Fdv.Parent, Panel)
        If Host Is Nothing Then Throw New Exception("没有找到正文所在面板")
        '首段与后续段落用同样的左右内边距、去掉上下内边距，接缝处才不会多出空隙
        Dim Pad As Thickness = Fdv.Document.PagePadding
        Dim SectionPad As New Thickness(Pad.Left, 0, Pad.Right, 0)
        Fdv.Document.PagePadding = SectionPad
        Dim InsertAt As Integer = Host.Children.IndexOf(Fdv)

        Dim Loaded As Integer = 0
        Dim Finished As Boolean = False
        Dim ScrollHandler As ScrollChangedEventHandler = Nothing
        Dim LoadOne As Action = Nothing
        Dim Recheck As Action = Nothing

        LoadOne =
        Sub()
            If Finished OrElse Loaded >= Sections.Count Then Return
            Try
                Dim SwSec As Stopwatch = Stopwatch.StartNew()
                Dim NewFdv As FlowDocumentScrollViewer = TryCast(GetObjectFromXML(Sections(Loaded)), FlowDocumentScrollViewer)
                If NewFdv Is Nothing OrElse NewFdv.Document Is Nothing Then Throw New Exception("正文分段解析失败")
                NewFdv.Document.PagePadding = SectionPad
                InsertAt += 1
                If InsertAt > Host.Children.Count Then InsertAt = Host.Children.Count
                Host.Children.Insert(InsertAt, NewFdv)
                Loaded += 1
                Logger.Info($"帮助页面正文分段 {Loaded}/{Sections.Count} 已加载（{SwSec.ElapsedMilliseconds} ms）")
                If Loaded >= Sections.Count Then
                    Finished = True
                    If ScrollHandler IsNot Nothing Then RemoveHandler PanBack.ScrollChanged, ScrollHandler
                    CacheElement(CacheKey, Element)
                    Logger.Info($"帮助页面正文全部加载完成（{Sw.ElapsedMilliseconds} ms）")
                End If
            Catch ex As Exception
                Logger.Warn(ex, "加载帮助页面正文分段失败")
                Finished = True
            End Try
        End Sub

        '还差 SectionBufferPx 就到已加载内容的底部时，就准备下一段
        Recheck =
        Sub()
            If Finished Then Return
            Try
                '先让布局跑完，ScrollViewer 的 ExtentHeight 才是这一段的真实高度
                PanBack.UpdateLayout()
            Catch
            End Try
            If PanBack.ViewportHeight <= 0 Then Return
            If PanBack.VerticalOffset + PanBack.ViewportHeight >= PanBack.ExtentHeight - SectionBufferPx Then
                LoadOne()
                If Not Finished Then Dispatcher.BeginInvoke(Windows.Threading.DispatcherPriority.Background, Recheck)
            End If
        End Sub

        ScrollHandler =
        Sub(Sender As Object, E As ScrollChangedEventArgs)
            If Finished Then Return
            If PanBack.VerticalOffset + PanBack.ViewportHeight >= PanBack.ExtentHeight - SectionBufferPx Then
                Dispatcher.BeginInvoke(Windows.Threading.DispatcherPriority.Background, Recheck)
            End If
        End Sub
        AddHandler PanBack.ScrollChanged, ScrollHandler
        Logger.Info($"帮助页面首屏渲染完成（{Sw.ElapsedMilliseconds} ms，正文共 {Sections.Count + 1} 段，按滚动加载）")

        '第一段可能还撑不满一屏，先立刻补齐
        Dispatcher.BeginInvoke(Windows.Threading.DispatcherPriority.Background, Recheck)
    End Sub
#End Region

End Class
