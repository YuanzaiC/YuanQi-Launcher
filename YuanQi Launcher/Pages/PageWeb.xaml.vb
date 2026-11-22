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

    Private Sub ShowState(Text As String, Optional ShowActions As Boolean = False)
        Try
            LabState.Text = Text
            PanState.Opacity = If(String.IsNullOrWhiteSpace(Text), 0, 1)
            PanState.IsHitTestVisible = Not String.IsNullOrWhiteSpace(Text)
            If PanStateActions IsNot Nothing Then
                PanStateActions.Visibility = If(ShowActions AndAlso Not String.IsNullOrWhiteSpace(Text), Visibility.Visible, Visibility.Collapsed)
            End If
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

    ''' <summary>正在导航的地址（用于失败重试时回到同一个地址）。</summary>
    Private NavigatingUrl As String = ""
    ''' <summary>同一个地址已经自动重试过几次。</summary>
    Private NavRetryCount As Integer = 0
    ''' <summary>同一个地址最多自动重试几次（对付首次连接的偶发失败，比如正好赶上代理没热起来）。</summary>
    Private Const MaxNavRetry As Integer = 2

    Private Sub Core_NavigationStarting(sender As Object, e As CoreWebView2NavigationStartingEventArgs)
        ShowProgress(True)
        Dim Url As String = If(e.Uri, "")
        If Not String.Equals(Url, NavigatingUrl, StringComparison.OrdinalIgnoreCase) Then
            NavigatingUrl = Url
            NavRetryCount = 0 '换了地址就重新计数
        End If
    End Sub

    ''' <summary>这些错误多半是"再试一次就好"，值得自动重试。</summary>
    Private Shared Function IsTransientError(Status As CoreWebView2WebErrorStatus) As Boolean
        Select Case Status
            Case CoreWebView2WebErrorStatus.ConnectionAborted,
                 CoreWebView2WebErrorStatus.ConnectionReset,
                 CoreWebView2WebErrorStatus.Timeout,
                 CoreWebView2WebErrorStatus.ServerUnreachable,
                 CoreWebView2WebErrorStatus.ErrorHttpInvalidServerResponse,
                 CoreWebView2WebErrorStatus.CannotConnect,
                 CoreWebView2WebErrorStatus.Disconnected,
                 CoreWebView2WebErrorStatus.HostNameNotResolved,
                 CoreWebView2WebErrorStatus.UnexpectedError,
                 CoreWebView2WebErrorStatus.Unknown 'WebView2 有时把首次连接失败报成 Unknown，重试一次通常就好了
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Async Sub Core_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        ShowProgress(False)
        If e.IsSuccess Then
            '首次加载完成：此时页面已静止，原生网页块才显示，避免在入场动画中凭空出现
            If Not IsBrowserShown Then
                IsBrowserShown = True
                Browser.Visibility = Visibility.Visible
            End If
            ShowState("")
            SaveCookiesFrom(Me) '导航完成即保存登录状态（登录跳转完成后会被捕获）
            'CurseForge 这类站点首次访问可能撞上 Cloudflare 人机验证：盯一下，别让用户对着空白页干等
            CheckVerification(1)
        Else
            If e.WebErrorStatus = CoreWebView2WebErrorStatus.OperationCanceled Then Return '用户自己按了停止
            Dim Url As String = If(NavigatingUrl, If(Browser.Source?.ToString, ""))
            Logger.Warn($"在启动器内打开网页失败：{Url}（{e.WebErrorStatus}）")
            If IsTransientError(e.WebErrorStatus) AndAlso NavRetryCount < MaxNavRetry AndAlso Not String.IsNullOrWhiteSpace(Url) Then
                NavRetryCount += 1
                ShowState($"网页暂时打不开（{e.WebErrorStatus}），正在重试第 {NavRetryCount} 次…")
                '原生网页块会挡住提示文字，重试期间先藏起来
                Try
                    Browser.Visibility = Visibility.Collapsed
                    IsBrowserShown = False
                Catch ex As Exception
                    Logger.Warn(ex, "隐藏网页失败")
                End Try
                Try
                    Await System.Threading.Tasks.Task.Delay(900)
                    If IsCoreReady AndAlso IsLoaded Then
                        ShowProgress(True)
                        Browser.Source = New Uri(Url)
                        Return
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "重试打开网页失败")
                End Try
            End If
            ShowState($"网页加载失败：{e.WebErrorStatus}{vbCrLf}可以点下面的「重试」，或改用系统浏览器打开。", True)
            'WebView2 是原生窗口，永远盖在 WPF 内容上面，必须把它藏起来提示才看得见、点得到
            Try
                Browser.Visibility = Visibility.Collapsed
                IsBrowserShown = False
            Catch ex As Exception
                Logger.Warn(ex, "隐藏加载失败的网页失败")
            End Try
        End If
    End Sub

    ''' <summary>
    ''' 检测当前页面是不是 Cloudflare 之类的人机验证页；是的话隔几秒再看一次，
    ''' 一直过不去就提示用户改用系统浏览器（系统浏览器通常已经带着代理与验证 Cookie）。
    ''' Round：1 = 刚加载完，2 = 等过一次，3 = 已经在提示了，只看验证有没有自己通过。
    ''' </summary>
    Private Async Sub CheckVerification(Round As Integer)
        Try
            If Not IsCoreReady OrElse Browser.CoreWebView2 Is Nothing Then Return
            Dim IsChallenge As Boolean = Await IsChallengePage()
            If Not IsChallenge Then
                If Round > 1 Then
                    Logger.Info("人机验证页面已通过，恢复正常显示")
                    ShowState("")
                End If
                Return
            End If
            Dim Url As String = If(NavigatingUrl, "")
            Select Case Round
                Case 1
                    Logger.Warn($"页面疑似人机验证，先等它自动通过：{Url}")
                    ShowState("正在等待人机验证自动通过…")
                    Await System.Threading.Tasks.Task.Delay(7000)
                    CheckVerification(2)
                Case 2
                    '不遮挡页面：验证页有时需要用户自己点一下，盖住反而没法过
                    Logger.Warn($"人机验证没能在内置浏览器里通过，建议改用系统浏览器：{Url}")
                    ShowState("")
                    Hint("这个网站的人机验证在内置浏览器里过不去，点右上角「在浏览器打开」用系统浏览器访问", HintType.Blue)
                    Await System.Threading.Tasks.Task.Delay(12000)
                    CheckVerification(3)
                Case Else
                    '已经在提示了：等用户操作，重新导航会重新走一遍流程
            End Select
        Catch ex As Exception
            Logger.Warn(ex, "人机验证检测流程失败")
        End Try
    End Sub

    ''' <summary>当前页面是否是人机验证页（脚本判断失败时按"不是"处理）。</summary>
    Private Async Function IsChallengePage() As System.Threading.Tasks.Task(Of Boolean)
        Try
            Dim Result As String = Await Browser.CoreWebView2.ExecuteScriptAsync(VerificationScript)
            Return Result?.Trim = "1"
        Catch ex As Exception
            Logger.Warn(ex, "检测人机验证页面失败")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 判断"是否人机验证页"的脚本：标题命中关键词，或者页面上有 Cloudflare 的验证容器/框架。
    ''' </summary>
    Private Const VerificationScript As String =
        "(function(){try{" &
        "var t=(document.title||'').toLowerCase();" &
        "var f=!!document.querySelector('#challenge-form,#cf-challenge-running,#challenge-stage,iframe[src*=\""challenges.cloudflare.com\""]');" &
        "var k=['just a moment','attention required','checking your browser','verify you are human','请稍候','正在验证','人机验证','安全验证'];" &
        "var m=false;for(var i=0;i<k.length;i++){if(t.indexOf(k[i])>=0){m=true;break;}}" &
        "return (m||f)?1:0;" &
        "}catch(e){return 0;}})()"

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

    ''' <summary>状态面板上的「重试」：重新打开当前地址，并重置自动重试计数。</summary>
    Private Sub BtnStateRetry_Click() Handles BtnStateRetry.Click
        Try
            Dim Url As String = If(NavigatingUrl, If(PendingUrl, ""))
            If String.IsNullOrWhiteSpace(Url) Then Url = If(TxtAddress.Text, "").Trim
            If String.IsNullOrWhiteSpace(Url) Then Return
            NavRetryCount = 0
            NavigatingUrl = Url
            PendingUrl = Url
            ShowState("正在重新加载…")
            Try
                Browser.Visibility = Visibility.Collapsed
                IsBrowserShown = False
            Catch ex As Exception
                Logger.Warn(ex, "隐藏网页失败")
            End Try
            If IsCoreReady Then
                ShowProgress(True)
                Browser.Source = New Uri(Url)
            Else
                InitializeCore()
            End If
        Catch ex As Exception
            Logger.Warn(ex, "重试打开网页失败")
        End Try
    End Sub

    ''' <summary>状态面板上的「在浏览器打开」：改用系统默认浏览器访问当前地址。</summary>
    Private Sub BtnStateExternal_Click() Handles BtnStateExternal.Click
        Try
            Dim Url As String = If(NavigatingUrl, If(PendingUrl, If(TxtAddress.Text, "").Trim))
            If String.IsNullOrWhiteSpace(Url) Then Return
            OpenWebsiteExternal(Url)
        Catch ex As Exception
            Logger.Warn(ex, "用系统浏览器打开网页失败")
        End Try
    End Sub

#End Region

End Class
