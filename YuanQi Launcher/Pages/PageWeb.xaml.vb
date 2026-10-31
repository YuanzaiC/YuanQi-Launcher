Imports Microsoft.Web.WebView2.Core

''' <summary>
''' 启动器内嵌网页页：用 WebView2（Edge Chromium 内核）在启动器里单独打开网页。
''' </summary>
Public Class PageWeb

    Private PendingUrl As String
    Private IsCoreReady As Boolean
    Private IsInitializing As Boolean
    ''' <summary>网页是否已成功加载并显示（首次显示由 NavigationCompleted 驱动，规避原生白块与入场动画不匹配）。</summary>
    Private IsBrowserShown As Boolean = False

    ''' <summary>
    ''' 设置要打开的网址（页面未加载时延迟到 Loaded 后执行）。
    ''' </summary>
    Public Sub Navigate(Url As String)
        PendingUrl = Url
        If TxtAddress IsNot Nothing Then TxtAddress.Text = Url
        If IsLoaded Then
            If IsCoreReady Then
                DoNavigate()
            Else
                InitializeCore()
            End If
        End If
    End Sub

    Private Sub PageWeb_Loaded() Handles Me.Loaded
        AnimateEnter()
        If Not String.IsNullOrWhiteSpace(PendingUrl) Then
            If IsCoreReady Then
                DoNavigate()
            Else
                InitializeCore()
            End If
        End If
        '从页面栈返回时：网页已加载完成则直接显示
        If IsBrowserShown Then Browser.Visibility = Visibility.Visible
    End Sub

    ''' <summary>网页页组件入场：工具栏各组件与网页卡片依次淡入并上移归位。</summary>
    Private Sub AnimateEnter()
        Try
            Dim Elems As UIElement() = {BtnBack, BtnForward, BtnRefresh, TxtAddress, BtnExternal, PanWebCard}
            Dim Anims As New List(Of AniData)
            For i As Integer = 0 To Elems.Length - 1
                Dim Elem As UIElement = Elems(i)
                Dim Frame As FrameworkElement = CType(Elem, FrameworkElement)
                Dim Delay As Integer = i * 45
                Frame.Visibility = Visibility.Visible
                Elem.IsHitTestVisible = True
                Elem.Opacity = 0
                Elem.RenderTransform = New TranslateTransform(0, 14)
                Anims.Add(AaOpacity(Elem, 1, 220, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
                Anims.Add(AaTranslateY(Elem, -14, 300, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
            Next
            AniStart(Anims.ToArray(), "PageWeb Enter")
        Catch ex As Exception
            Logger.Warn(ex, "网页页入场动画失败")
        End Try
    End Sub

    ''' <summary>
    ''' 网页页组件退场：依次淡出并上移，与入场动画对应。
    ''' 退出动画窗口约 110ms（随后页面即被替换），故节奏压缩；结束时必须推进页面状态机。
    ''' </summary>
    Public Overrides Sub TriggerExitAnimation(ParamArray Elements As FrameworkElement())
        Try
            Dim Elems As UIElement() = {BtnBack, BtnForward, BtnRefresh, TxtAddress, BtnExternal, PanWebCard}
            Dim Anims As New List(Of AniData)
            For i As Integer = 0 To Elems.Length - 1
                Dim Elem As UIElement = Elems(i)
                Dim Frame As FrameworkElement = CType(Elem, FrameworkElement)
                If Frame.Visibility = Visibility.Collapsed Then Continue For
                Dim Delay As Integer = i * 12
                Elem.IsHitTestVisible = False
                Anims.Add(AaOpacity(Elem, -Elem.Opacity, 70, Delay, New AniEaseInFluent(AniEasePower.Weak)))
                Anims.Add(AaTranslateY(Elem, -8, 70, Delay, New AniEaseInFluent(AniEasePower.Weak)))
            Next
            Anims.Add(AaCode(
            Sub()
                For Each Elem In Elems
                    CType(Elem, FrameworkElement).Visibility = Visibility.Collapsed
                Next
                '快速切页时 PageOnForceExit 可能已把状态重置为 Empty 并取消本动画（同框架键），
                '此时不再推进状态机，避免重复触发异常
                If PageState = PageStates.PageExit Then PageOnExitAnimationFinished()
            End Sub,, True))
            AniStart(Anims.ToArray(), "PageRight PageChange " & PageUuid)
        Catch ex As Exception
            Logger.Warn(ex, "网页页退场动画失败")
            If PageState = PageStates.PageExit Then PageOnExitAnimationFinished() '动画失败时也要推进页面状态机
        End Try
    End Sub

    ''' <summary>
    ''' 异步初始化 WebView2 内核，完成后打开待加载地址。
    ''' </summary>
    Private Async Sub InitializeCore()
        If IsCoreReady OrElse IsInitializing Then Return
        IsInitializing = True
        Try
            ShowState("正在初始化浏览器内核…")
            Dim Env As CoreWebView2Environment = Await CoreWebView2Environment.CreateAsync(Nothing, Paths.Base & "YQL\WebView2")
            Await Browser.EnsureCoreWebView2Async(Env)
            IsCoreReady = True
            AddHandler Browser.CoreWebView2.NavigationStarting, AddressOf Core_NavigationStarting
            AddHandler Browser.CoreWebView2.NavigationCompleted, AddressOf Core_NavigationCompleted
            AddHandler Browser.CoreWebView2.SourceChanged, AddressOf Core_SourceChanged
            AddHandler Browser.CoreWebView2.HistoryChanged, AddressOf Core_HistoryChanged
            AddHandler Browser.CoreWebView2.NewWindowRequested, AddressOf Core_NewWindowRequested
            RegisterWebInstance(Me)
            RestoreCookiesTo(Me) '先恢复网页登录状态，再打开地址
            ShowState("")
            UpdateNavButtons()
            DoNavigate()
        Catch ex As Exception
            Logger.Error(ex, "初始化 WebView2 内核失败")
            ShowState("无法初始化浏览器内核（WebView2）" & vbCrLf &
                      "可能缺少 Microsoft Edge WebView2 运行时，请先在系统中安装。" & vbCrLf &
                      "也可以点右上角「在浏览器打开」。")
        Finally
            IsInitializing = False
        End Try
    End Sub

    Private Sub DoNavigate()
        Try
            If String.IsNullOrWhiteSpace(PendingUrl) Then Return
            If Not IsCoreReady Then Return
            '首次加载保留整页提示；后续导航只显示顶部进度条，不遮挡已显示的内容
            If Browser.Source Is Nothing Then ShowState("正在加载…")
            ShowProgress(True)
            Browser.Source = New Uri(PendingUrl)
        Catch ex As Exception
            ShowProgress(False)
            Logger.Warn(ex, $"在启动器内打开网页失败：{PendingUrl}")
            ShowState("无法打开该网页：" & ex.Message & vbCrLf & "可以点右上角「在浏览器打开」")
        End Try
    End Sub

    Private Sub ShowState(Text As String)
        Try
            LabState.Text = Text
            PanState.Opacity = If(String.IsNullOrWhiteSpace(Text), 0, 1)
            PanState.IsHitTestVisible = Not String.IsNullOrWhiteSpace(Text)
        Catch ex As Exception
            Logger.Warn(ex, "更新网页状态失败")
        End Try
    End Sub

    ''' <summary>切换顶部加载进度条与「刷新 / 停止」按钮状态。</summary>
    Private Sub ShowProgress(IsLoading As Boolean)
        Try
            PanProgress.Visibility = If(IsLoading, Visibility.Visible, Visibility.Collapsed)
            BtnRefresh.Content = If(IsLoading, "停止", "刷新")
        Catch ex As Exception
            Logger.Warn(ex, "更新网页加载进度失败")
        End Try
    End Sub

    ''' <summary>根据浏览历史同步后退 / 前进按钮的可用状态。</summary>
    Private Sub UpdateNavButtons()
        Try
            If Not IsCoreReady Then Return
            BtnBack.IsEnabled = Browser.CoreWebView2.CanGoBack
            BtnForward.IsEnabled = Browser.CoreWebView2.CanGoForward
        Catch ex As Exception
            Logger.Warn(ex, "更新网页导航按钮状态失败")
        End Try
    End Sub

#Region "内核事件"

    Private Sub Core_NavigationStarting(sender As Object, e As CoreWebView2NavigationStartingEventArgs)
        ShowProgress(True)
    End Sub

    Private Sub Core_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        ShowProgress(False)
        If e.IsSuccess Then
            '首次加载完成：此时页面已静止，原生网页块才显示，避免在入场动画中凭空出现
            If Not IsBrowserShown Then
                IsBrowserShown = True
                Browser.Visibility = Visibility.Visible
            End If
            ShowState("")
            SaveCookiesFrom(Me) '导航完成即保存登录状态（登录跳转完成后会被捕获）
        Else
            ShowState("网页加载失败：" & e.WebErrorStatus.ToString() & vbCrLf & "可以点右上角「在浏览器打开」")
        End If
    End Sub

    Private Sub Core_HistoryChanged(sender As Object, e As EventArgs)
        UpdateNavButtons()
    End Sub

    ''' <summary>网页请求打开新窗口（如 target=_blank 链接）时改为在当前视图中打开。</summary>
    Private Sub Core_NewWindowRequested(sender As Object, e As CoreWebView2NewWindowRequestedEventArgs)
        Try
            e.Handled = True
            If Not String.IsNullOrWhiteSpace(e.Uri) Then Browser.Source = New Uri(e.Uri)
        Catch ex As Exception
            Logger.Warn(ex, "在当前视图打开新窗口链接失败")
        End Try
    End Sub

    Private Sub Core_SourceChanged(sender As Object, e As CoreWebView2SourceChangedEventArgs)
        Try
            If Browser.Source IsNot Nothing Then
                TxtAddress.Text = Browser.Source.ToString
                PendingUrl = TxtAddress.Text
            End If
        Catch ex As Exception
            Logger.Warn(ex, "同步网页地址失败")
        End Try
    End Sub

#End Region

#Region "网页登录状态持久化"

    'WebView2 的会话 Cookie（无过期时间的登录态）只保存在内存中，启动器关闭即失效；
    '强杀进程还可能丢失刚写入的持久 Cookie。此处把全部 Cookie 定期落盘，并在内核初始化时恢复，
    '使通过启动器打开的网页登录（含微软登录网页、论坛、皮肤站等）不随启动器关闭而失效。

    Private Shared ReadOnly CookieFilePath As String = Paths.Base & "YQL\WebCookies.json"
    Private Shared ReadOnly WebInstances As New List(Of PageWeb)
    Private Shared CookieSaveTimer As System.Windows.Threading.DispatcherTimer

    Private Shared Sub RegisterWebInstance(P As PageWeb)
        Try
            SyncLock WebInstances
                If Not WebInstances.Contains(P) Then WebInstances.Add(P)
            End SyncLock
            If CookieSaveTimer Is Nothing Then
                CookieSaveTimer = New System.Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromSeconds(20)}
                AddHandler CookieSaveTimer.Tick, Sub() SaveCookiesAny()
                CookieSaveTimer.Start()
            End If
        Catch ex As Exception
            Logger.Warn(ex, "注册网页实例失败")
        End Try
    End Sub

    Private Sub PageWeb_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        Try
            SyncLock WebInstances
                WebInstances.Remove(Me)
            End SyncLock
        Catch
        End Try
    End Sub

    ''' <summary>从任意已就绪的网页实例保存全部 Cookie（定时与退出时调用）。</summary>
    Public Shared Sub SaveCookiesAny()
        Try
            SyncLock WebInstances
                For Each P As PageWeb In WebInstances
                    If P.IsCoreReady Then
                        SaveCookiesFrom(P)
                        Return
                    End If
                Next
            End SyncLock
        Catch ex As Exception
            Logger.Warn(ex, "保存网页登录状态失败")
        End Try
    End Sub

    ''' <summary>把内核中的全部 Cookie 序列化到本地文件（会话 Cookie 一并保存）。</summary>
    Private Shared Async Sub SaveCookiesFrom(P As PageWeb)
        Try
            If P?.IsCoreReady <> True Then Return
            Dim Cookies = Await P.Browser.CoreWebView2.CookieManager.GetCookiesAsync("")
            Dim Arr As New JArray
            For Each C In Cookies
                Dim Obj As New JObject
                Obj("Name") = C.Name
                Obj("Value") = C.Value
                Obj("Domain") = C.Domain
                Obj("Path") = C.Path
                Obj("IsSession") = C.IsSession
                Obj("Expires") = C.Expires '会话 Cookie 为 null
                Obj("IsSecure") = C.IsSecure
                Obj("IsHttpOnly") = C.IsHttpOnly
                Obj("SameSite") = C.SameSite.ToString()
                Arr.Add(Obj)
            Next
            Dim TempPath = CookieFilePath & ".tmp"
            IO.File.WriteAllText(TempPath, Arr.ToString(Newtonsoft.Json.Formatting.None))
            If IO.File.Exists(CookieFilePath) Then IO.File.Delete(CookieFilePath)
            IO.File.Move(TempPath, CookieFilePath)
            Logger.Trace($"已保存网页登录状态（{Arr.Count} 条 Cookie）")
        Catch ex As Exception
            Logger.Warn(ex, "保存网页登录状态失败")
        End Try
    End Sub

    ''' <summary>从本地文件恢复全部 Cookie，在首次导航前调用。</summary>
    Private Shared Sub RestoreCookiesTo(P As PageWeb)
        Try
            If P?.IsCoreReady <> True Then Return
            If Not IO.File.Exists(CookieFilePath) Then Return
            Dim Arr As JArray = JArray.Parse(IO.File.ReadAllText(CookieFilePath))
            Dim Manager = P.Browser.CoreWebView2.CookieManager
            Dim Count As Integer = 0
            For Each Obj As JObject In Arr
                Try
                    Dim Cookie = Manager.CreateCookie(Obj("Name").ToString, Obj("Value").ToString, Obj("Domain").ToString, Obj("Path").ToString)
                    If Obj("IsSession") IsNot Nothing AndAlso Not Obj("IsSession").ToObject(Of Boolean) AndAlso
                       Obj("Expires") IsNot Nothing AndAlso Obj("Expires").Type <> JTokenType.Null Then
                        Cookie.Expires = Obj("Expires").ToObject(Of Date)
                    End If
                    If Obj("IsSecure") IsNot Nothing Then Cookie.IsSecure = Obj("IsSecure").ToObject(Of Boolean)
                    If Obj("IsHttpOnly") IsNot Nothing Then Cookie.IsHttpOnly = Obj("IsHttpOnly").ToObject(Of Boolean)
                    If Obj("SameSite") IsNot Nothing Then Cookie.SameSite = CType([Enum].Parse(GetType(CoreWebView2CookieSameSiteKind), Obj("SameSite").ToString), CoreWebView2CookieSameSiteKind)
                    Manager.AddOrUpdateCookie(Cookie)
                    Count += 1
                Catch inner As Exception
                    Logger.Warn(inner, "恢复单条网页 Cookie 失败")
                End Try
            Next
            If Count > 0 Then Logger.Info($"已恢复网页登录状态（{Count} 条 Cookie）")
        Catch ex As Exception
            Logger.Warn(ex, "恢复网页登录状态失败")
        End Try
    End Sub

#End Region

#Region "工具栏"

    Private Sub BtnBack_Click() Handles BtnBack.Click
        Try
            If IsCoreReady AndAlso Browser.CoreWebView2.CanGoBack Then Browser.CoreWebView2.GoBack()
        Catch ex As Exception
            Logger.Warn(ex, "网页后退失败")
        End Try
    End Sub

    Private Sub BtnForward_Click() Handles BtnForward.Click
        Try
            If IsCoreReady AndAlso Browser.CoreWebView2.CanGoForward Then Browser.CoreWebView2.GoForward()
        Catch ex As Exception
            Logger.Warn(ex, "网页前进失败")
        End Try
    End Sub

    ''' <summary>
    ''' 供主窗口鼠标侧键调用：后退一页浏览历史。
    ''' </summary>
    Public Function TryBack() As Boolean
        Try
            If IsCoreReady AndAlso Browser.CoreWebView2.CanGoBack Then
                Browser.CoreWebView2.GoBack()
                Return True
            End If
        Catch ex As Exception
            Logger.Warn(ex, "侧键网页后退失败")
        End Try
        Return False
    End Function

    ''' <summary>
    ''' 供主窗口鼠标侧键调用：前进一页浏览历史。
    ''' </summary>
    Public Function TryForward() As Boolean
        Try
            If IsCoreReady AndAlso Browser.CoreWebView2.CanGoForward Then
                Browser.CoreWebView2.GoForward()
                Return True
            End If
        Catch ex As Exception
            Logger.Warn(ex, "侧键网页前进失败")
        End Try
        Return False
    End Function

    Private Sub BtnRefresh_Click() Handles BtnRefresh.Click
        Try
            If Not IsCoreReady Then Return
            If PanProgress.Visibility = Visibility.Visible Then
                Browser.CoreWebView2.Stop()
            Else
                Browser.CoreWebView2.Reload()
            End If
        Catch ex As Exception
            Logger.Warn(ex, "网页刷新失败")
        End Try
    End Sub

    Private Sub TxtAddress_KeyDown(sender As Object, e As KeyEventArgs) Handles TxtAddress.KeyDown
        If e.Key <> Key.Enter Then Return
        e.Handled = True
        Dim Url As String = If(TxtAddress.Text, "").Trim
        If Url = "" Then Return
        If Not Url.Contains("://") Then
            If Url.Contains(".") Then
                Url = "https://" & Url
            Else
                Url = "https://www.bing.com/search?q=" & Uri.EscapeDataString(Url)
            End If
        End If
        PendingUrl = Url
        If IsCoreReady Then
            DoNavigate()
        Else
            InitializeCore()
        End If
    End Sub

    Private Sub BtnExternal_Click() Handles BtnExternal.Click
        Try
            Dim Url As String = If(PendingUrl, "")
            If String.IsNullOrWhiteSpace(Url) Then Url = If(TxtAddress.Text, "").Trim
            If String.IsNullOrWhiteSpace(Url) Then Return
            OpenWebsiteExternal(Url)
        Catch ex As Exception
            Logger.Warn(ex, "用系统浏览器打开网页失败")
        End Try
    End Sub

#End Region

End Class
