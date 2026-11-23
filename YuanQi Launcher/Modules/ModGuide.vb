Imports System.Windows.Shapes
Imports System.Windows.Threading
Imports System.Windows.Media.Animation
Imports System.Windows.Controls.Primitives

''' <summary>
''' 首次使用引导：先弹一个介绍启动器的信息弹窗，再进入分步教程。
''' 教程不替用户翻页——界面压暗、圈出目标控件，用户必须亲手点到它（点导航就是自己翻页）才进入下一步。
''' 高亮圈与说明卡在步骤之间做位移 + 变形的补间动画，不会瞬移。
''' </summary>
Public Module ModGuide

    Public Class GuideStep
        ''' <summary>要高亮的控件；返回 Nothing 则只在中间显示说明。</summary>
        Public Target As Func(Of FrameworkElement)
        Public Title As String
        Public Text As String
        ''' <summary>补充小字提示，可为空。</summary>
        Public Hint As String
        ''' <summary>是否要求真的点中目标；False 表示这一步只讲解，点击任意处推进。</summary>
        Public Hook As Boolean = True
        ''' <summary>是否要求这一步之后真的建出一个账户（没有账户时用于第一次注册）。</summary>
        Public RequireAccount As Boolean = False
        ''' <summary>点中目标后延迟多久再进下一步（毫秒）。默认 300，耗时操作可加大。</summary>
        Public AdvanceDelay As Integer = 300
        '' <summary>目标不存在时直接跳到下一步（例如跳过前置步骤后，返回键步骤也应一起跳过）。</summary>
        Public SkipIfMissing As Boolean = False
        '' <summary>不提供「跳过本步」（例如返回键这类必须真的点）。</summary>
        Public NoSkip As Boolean = False
        '' <summary>目标在这段时间后仍未出现就自动跳到下一步（0 表示不启用）。用于详情页这类可能被前置步骤跳过的步骤。</summary>
        Public SkipIfMissingAfterMs As Integer = 0
        '' <summary>上一步被跳过时，本步一起跳过（例如详情页依赖前面的点击）。</summary>
        Public DependsOnPrevious As Boolean = False
        '' <summary>等待目标出现的上限（毫秒）。列表需要联网加载时调大。</summary>
        Public WaitMs As Integer = 5000
    End Class

    Public Const SettingsKey As String = "GuideFinished"
    '' <summary>教程（分步演示）是否走完过；与介绍弹窗分开记，介绍看过但教程被打断时下次直接续教程。</summary>
    Public Const TourKey As String = "GuideTourFinished"

    Private Steps As New List(Of GuideStep)
    Private CurrentIndex As Integer = -1
    Private Overlay As Canvas = Nothing
    Private Callout As Border = Nothing
    Private Ring As Border = Nothing
    Private Catcher As Rectangle
    Private Glow As Border = Nothing
    Private BarFill As Border = Nothing
    Private BarTrack As Border = Nothing
    Private DarkTop, DarkBottom, DarkLeft, DarkRight As Rectangle
    Private Hooked As FrameworkElement = Nothing
    Private Running As Boolean = False
    Private ResizeHandler As SizeChangedEventHandler = Nothing
    Private WaitCount As Integer = 0
    '介绍向导
    Private IntroOverlay As Canvas = Nothing
    Private IntroCard As Border = Nothing
    Private IntroPage As Integer = 0
    Private IntroEnvItems As List(Of KeyValuePair(Of String, String)) = Nothing
    Private IntroEnvRunning As Boolean = False
    '' <summary>本次首次使用是否把协议确认并入了通知弹窗。</summary>
    Public EulaPending As Boolean = False
    '' <summary>用户在通知弹窗里选择了不同意。</summary>
    Public EulaDeclined As Boolean = False
    Private KeyHandler As KeyEventHandler = Nothing
    Private ModalTimer As DispatcherTimer = Nothing
    Private OverlayHiddenByModal As Boolean = False
    '' <summary>上一步是否是被跳过（而不是用户真的点了目标）。</summary>
    Private SkippedPrevious As Boolean = False
    Private IntroResizeHandler As SizeChangedEventHandler = Nothing
    Private ReadOnly IntroTitles As String() = {"先做个体检"}
    Private ReadOnly IntroBodies As String() = {
        "检查这台电脑能不能跑游戏：Java、内存、显卡和下载源都会过一遍。有问题会直接写出来，也可以先跳过，之后再在「设置 → 通用 → 环境体检」里做。"}
    '环境体检步骤的等待
    Private EnvCheckPending As Boolean = False

    '补间动画
    Private Anim As DispatcherTimer = Nothing
    Private FromRect, ToRect As Rect
    Private DrawnRect As Rect = Rect.Empty
    Private AnimTick As Integer = 0
    Private Const AnimFrames As Integer = 16
    Private Const CalloutWidth As Double = 356

    ''' <summary>是否应该自动开始引导（首次使用）。看过旧版说明的老用户不再自动弹。</summary>
    Public Function ShouldAutoStart() As Boolean
        Try
            If Settings.Get(Of Boolean)(SettingsKey) Then Return False
            '老用户（已经看过旧版说明弹窗）不打扰，需要时可以在设置里重看
            If Settings.Get(Of Integer)("SystemGuideVersion") >= MyMsgGuide.GuideVersion Then Return False
            Return True
        Catch
            Return False
        End Try
    End Function

    ''' <summary>教程（分步演示）是否该开始：介绍看过但教程被打断时下次直接续上。</summary>
    Public Function ShouldStartTour() As Boolean
        Try
            Return Not Settings.Get(Of Boolean)(TourKey)
        Catch
            Return False
        End Try
    End Function

    Public Sub MarkTourFinished()
        Try
            Settings.Set(TourKey, True)
        Catch
        End Try
    End Sub
    Public Sub MarkFinished()
        Try
            Settings.Set(SettingsKey, True)
        Catch
        End Try
    End Sub

    ''' <summary>首次使用：分页介绍启动器（一页一件事），最后进入教程。</summary>
    Public Sub ShowIntro()
        Try
            If FrmMain Is Nothing Then Return
            '必须在 UI 线程：FormMain 的初始化序列不一定在 UI 线程上
            RunInUi(
            Sub()
                Try
                    '通知弹窗：介绍启动器本身，最后一页做环境体检
                    EulaDeclined = False
                    Dim Seen As Boolean = MyMsgBoxGuide()
                    If EulaDeclined Then
                        '不同意协议：不进入启动器
                        If FrmMain IsNot Nothing Then FrmMain.EndProgram(False)
                        Return
                    End If
                    If Seen Then
                        If EulaPending Then
                            Try : Settings.Set("SystemEulaVersion", 2) : Catch : End Try
                            EulaPending = False
                        End If
                        MarkFinished() '介绍+体检已看完，下次启动不再重复
                        StartGuide()
                    ElseIf Not EulaPending Then
                        MarkFinished() '用户点了跳过引导
                    End If
                    '跳过了且协议未确认时什么都不记，下次启动会重新弹出
                Catch ex As Exception
                    Logger.Warn(ex, "显示引导介绍失败")
                End Try
            End Sub, True)
        Catch ex As Exception
            Logger.Warn(ex, "显示引导介绍失败")
        End Try
    End Sub
    Private Sub EnsureIntroOverlay()
        If IntroOverlay IsNot Nothing AndAlso IntroOverlay.Parent IsNot Nothing Then Return
        IntroOverlay = New Canvas With {.IsHitTestVisible = True}
        Panel.SetZIndex(IntroOverlay, 9100)
        Dim Dim1 As New Rectangle With {.Fill = New MyColor(0.55, 0, 0, 0), .IsHitTestVisible = True}
        IntroOverlay.Children.Add(Dim1)
        IntroCard = New Border With {.CornerRadius = New CornerRadius(14), .Padding = New Thickness(28, 24, 28, 18), .Width = 520}
        IntroCard.SetResourceReference(Border.BackgroundProperty, "ColorBrush8")
        IntroCard.SetResourceReference(Border.BorderBrushProperty, "BrushBorder")
        IntroCard.BorderThickness = New Thickness(1)
        IntroOverlay.Children.Add(IntroCard)
        FrmMain.PanForm.Children.Add(IntroOverlay)
        If IntroResizeHandler Is Nothing Then
            IntroResizeHandler = Sub() RunInUi(Sub() LayoutIntro(), True)
            AddHandler FrmMain.SizeChanged, IntroResizeHandler
        End If
    End Sub

    Private Sub RenderIntroPage()
        Dim Stack As New StackPanel
        Dim IsEnvPage As Boolean = True
        Dim Step1 As New TextBlock With {.Text = $"第 {IntroPage + 1} / {IntroTitles.Length} 页", .FontSize = 11, .Opacity = 0.5}
        Step1.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Stack.Children.Add(Step1)
        Dim Head As New TextBlock With {.Text = IntroTitles(IntroPage), .FontSize = 19, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 6, 0, 0), .TextWrapping = TextWrapping.Wrap}
        Head.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Stack.Children.Add(Head)
        Dim Body As New TextBlock With {.Text = IntroBodies(IntroPage), .FontSize = 12.5, .Margin = New Thickness(0, 12, 0, 0), .TextWrapping = TextWrapping.Wrap, .LineHeight = 21}
        Body.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Stack.Children.Add(Body)
        '体检页：结果区
        If IsEnvPage Then
            Dim Pan As New StackPanel With {.Margin = New Thickness(0, 12, 0, 0)}
            If IntroEnvRunning Then
                Dim Run1 As New TextBlock With {.Text = "正在检查…", .FontSize = 12, .Opacity = 0.7}
                Run1.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
                Pan.Children.Add(Run1)
            ElseIf IntroEnvItems IsNot Nothing Then
                For Each Item In IntroEnvItems
                    Dim Ok1 As Boolean = Item.Value.StartsWithF("OK")
                    Dim Line As New TextBlock With {.Text = If(Ok1, "✓ ", "✗ ") & Item.Key & "：" & If(Ok1, "正常", Item.Value),
                                                    .FontSize = 12, .TextWrapping = TextWrapping.Wrap, .LineHeight = 19, .Margin = New Thickness(0, 1, 0, 1)}
                    If Ok1 Then
                        Line.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
                    Else
                        Line.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
                        Line.FontWeight = FontWeights.SemiBold
                    End If
                    Pan.Children.Add(Line)
                Next
                Dim Problems = IntroEnvItems.Where(Function(I) Not I.Value.StartsWithF("OK")).Count
                Dim Sum As New TextBlock With {.Text = If(Problems = 0, "全部通过，可以放心开始。", $"有 {Problems} 项需要注意，先看上面的说明。"),
                                               .FontSize = 11.5, .Opacity = 0.7, .Margin = New Thickness(0, 8, 0, 0), .TextWrapping = TextWrapping.Wrap}
                Sum.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
                Pan.Children.Add(Sum)
            End If
            Stack.Children.Add(Pan)
        End If
        '圆点指示
        Dim Dots As New TextBlock With {.FontSize = 10, .Opacity = 0.55, .Margin = New Thickness(0, 20, 0, 0), .VerticalAlignment = VerticalAlignment.Center}
        Dim D As New Text.StringBuilder
        For i = 0 To IntroTitles.Length - 1
            D.Append(If(i = IntroPage, "● ", "○ "))
        Next
        Dots.Text = D.ToString.Trim
        Dots.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
        Stack.Children.Add(Dots)
        '按钮行
        Dim Row As New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Right, .Margin = New Thickness(0, 14, 0, 0)}
        If IsEnvPage Then
            If IntroEnvRunning Then
                Row.Children.Add(MakeButton("跳过体检", MyButton.ColorState.Normal, 92, Sub() FinishIntro(True)))
            ElseIf IntroEnvItems Is Nothing Then
                Row.Children.Add(MakeButton("跳过体检", MyButton.ColorState.Normal, 92, Sub() FinishIntro(True)))
                Row.Children.Add(MakeButton("开始体检", MyButton.ColorState.Highlight, 96, Sub() StartEnvCheckInIntro(), 8))
            Else
                Row.Children.Add(MakeButton("重新检查", MyButton.ColorState.Normal, 92, Sub() StartEnvCheckInIntro()))
                Row.Children.Add(MakeButton("开始教程", MyButton.ColorState.Highlight, 96, Sub() FinishIntro(True), 8))
            End If
            If IntroPage > 0 Then Row.Children.Add(MakeButton("上一步", MyButton.ColorState.Normal, 80, Sub() IntroPrev(), 8))
        Else
            Row.Children.Add(MakeButton("跳过介绍", MyButton.ColorState.Normal, 88, Sub() FinishIntro(False)))
            If IntroPage > 0 Then Row.Children.Add(MakeButton("上一步", MyButton.ColorState.Normal, 80, Sub() IntroPrev(), 8))
            Row.Children.Add(MakeButton("下一步", MyButton.ColorState.Highlight, 96, Sub() IntroNext(), 8))
        End If
        Stack.Children.Add(Row)
        IntroCard.Child = Stack
        LayoutIntro()
        AniStart({AaCode(Sub() LayoutIntro(), 60), AaCode(Sub() LayoutIntro(), 220)}, "Intro Layout")
    End Sub

    ''' <summary>在介绍向导里跑环境体检（后台线程，结果回到卡片里显示）。</summary>
    Private Sub StartEnvCheckInIntro()
        If IntroEnvRunning Then Return
        IntroEnvRunning = True
        IntroEnvItems = Nothing
        RenderIntroPage()
        RunInNewThread(
        Sub()
            Dim Items As List(Of KeyValuePair(Of String, String)) = Nothing
            Try
                Items = RunEnvCheck()
            Catch ex As Exception
                Logger.Warn(ex, "引导中的环境体检失败")
            End Try
            RunInUi(
            Sub()
                IntroEnvRunning = False
                IntroEnvItems = If(Items, New List(Of KeyValuePair(Of String, String)))
                If IntroOverlay IsNot Nothing Then RenderIntroPage()
            End Sub)
        End Sub, "Guide Env Check", ThreadPriority.BelowNormal)
    End Sub
    Private Sub IntroNext()
        If IntroPage < IntroTitles.Length - 1 Then
            IntroPage += 1
            RenderIntroPage()
        Else
            FinishIntro(True)
        End If
    End Sub

    Private Sub IntroPrev()
        If IntroPage <= 0 Then Return
        IntroPage -= 1
        RenderIntroPage()
    End Sub

    Private Sub FinishIntro(Start As Boolean)
        RemoveIntroOverlay()
        If Start Then
            StartGuide()
        Else
            MarkFinished()
        End If
    End Sub

    Private Sub RemoveIntroOverlay()
        Try
            If IntroResizeHandler IsNot Nothing Then
                Try : RemoveHandler FrmMain.SizeChanged, IntroResizeHandler : Catch : End Try
                IntroResizeHandler = Nothing
            End If
            If IntroOverlay IsNot Nothing Then
                Try : FrmMain.PanForm.Children.Remove(IntroOverlay) : Catch : End Try
                IntroOverlay = Nothing
            End If
            IntroCard = Nothing
        Catch ex As Exception
            Logger.Warn(ex, "关闭介绍界面失败")
        End Try
    End Sub

    Private Sub LayoutIntro()
        Try
            If IntroOverlay Is Nothing OrElse IntroCard Is Nothing Then Return
            Dim W = FrmMain.PanForm.ActualWidth, H = FrmMain.PanForm.ActualHeight
            If W <= 0 OrElse H <= 0 Then Return
            Dim DimRect = TryCast(IntroOverlay.Children(0), Rectangle)
            If DimRect IsNot Nothing Then
                DimRect.Width = W : DimRect.Height = H
                Canvas.SetLeft(DimRect, 0) : Canvas.SetTop(DimRect, 0)
            End If
            Dim Ch = If(IntroCard.ActualHeight > 0, IntroCard.ActualHeight, 220)
            Canvas.SetLeft(IntroCard, Math.Max(12, (W - 520) / 2))
            Canvas.SetTop(IntroCard, Math.Max(12, (H - Ch) / 2))
        Catch ex As Exception
        End Try
    End Sub
    ''' <summary>开始（或重放）教程。</summary>
    Public Sub StartGuide()
        Try
            If FrmMain Is Nothing Then Return
            RemoveIntroOverlay()
            If Running Then [Stop]()
            BuildSteps()
            CurrentIndex = -1
            DrawnRect = Rect.Empty
            Running = True
            '等窗口首帧与页面构建完再开始，否则高亮框算不出位置
            RunInUi(
            Sub()
                AniStart({AaCode(Sub() NextStep(), 220)}, "Guide Start")
            End Sub, True)
        Catch ex As Exception
            Logger.Warn(ex, "启动引导失败")
            [Stop]()
        End Try
    End Sub

    ''' <summary>
    ''' 步骤表：一律不主动切页。要点开某个页面，就把高亮圈放在对应的导航按钮上，由用户自己点过去。
    ''' </summary>
    Private Sub BuildSteps()
        Steps.Clear()
        '入口不在主页时，先回到主页
        If FrmMain.PageCurrent <> FormMain.PageType.Launch Then
            Steps.Add(New GuideStep With {
                .Target = Function() FindByName("BtnTitleSelect0"),
                .Title = "返回主页",
                .Text = "点击左上角「启动」返回主页，教程自主页开始。"})
        End If
        '主页 · 版本
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnVersion"),
            .Title = "选择游戏版本",
            .Text = "版本名称为版本管理的入口。点击进入后可安装新版本、调整内存与隔离设置，或归档不常使用的版本。",
            .Hint = "尚未安装版本时，可在版本管理页选择「安装新版本」。"})
        '版本管理 · 右下角返回键
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnFloatingBack"),
            .SkipIfMissing = True,
            .NoSkip = True,
            .Title = "返回主页",
            .Text = "版本管理为二级页面，点击右下角的圆形返回键可回到主页。",
            .Hint = "二级页面均会在右下角显示该返回键。"})
        '主页 · 账户
        Dim HasAcc = HasAccount()
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnAccount"),
            .RequireAccount = Not HasAcc,
            .Title = If(HasAcc, "账户池", "注册账户"),
            .Text = If(HasAcc,
                "点击展开账户池，可切换账户、删除账户或新增账户，支持微软正版、离线与第三方登录。",
                "当前没有账户。点击此处将直接进入注册流程，按提示完成注册。"),
            .Hint = If(HasAcc,
                "不同账户可同时多开游戏，互不影响。",
                "注册完成后，教程将自动继续。")})
        '主页 · 左下角全局账户
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnAvatar"),
            .Hook = False,
            .Title = "全局账户入口",
            .Text = "左下角的头像为全局账户入口，显示当前账户与登录状态。此处设定的账户是所有版本的默认选择账号；已经单独指定过账户的版本不受影响。",
            .Hint = "点击可打开账户切换器，与主页右上角的账户胶囊作用相同。"})
        '主页 · 存档
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanSavesCard"),
            .Hook = False,
            .Title = "存档",
            .Text = "当前版本的存档列于本卡片，右上角可刷新列表。点击存档条目可查看详情、修改设置或复制种子。",
            .Hint = "右键存档条目可查看更多操作。"})
        '主页 · 最近游玩
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanRecentCard"),
            .Hook = False,
            .Title = "最近游玩",
            .Text = "最近启动过的实例按时间排序，点击即可切换，无需进入版本管理。",
            .Hint = "切换后，上方的实例信息与存档列表会同步更新。"})
        '主页 · 游玩日历
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanCalendarCard"),
            .Hook = False,
            .Title = "游玩日历",
            .Text = "日历标注当月有游玩记录的日期，可用左右箭头切换月份。"})
        '主页 · 游玩洞察
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanInsightCard"),
            .Hook = False,
            .Title = "游玩洞察",
            .Text = "显示本周游玩时长、与上周的对比以及连续游玩天数，点击可进入完整的游戏统计。",
            .Hint = "统计数据取自本地日志，不会上传。"})
        '主页 · 启动按钮
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnLaunch"),
            .Hook = False,
            .Title = "启动游戏",
            .Text = "选定版本与账户后点击此处启动，启动前会检查 Java、内存与登录状态。",
            .Hint = "本步骤无需点击，以免提前启动游戏。"})
        '下载 · 打开下载页（默认已在「游戏」分类）
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnTitleSelect1"),
            .Title = "进入「下载」",
            .Text = "点击上方「下载」。进入后默认选中「游戏」分类，页面列出可安装的 Minecraft 版本。",
            .Hint = "左侧可切换 Mod、整合包、地图、资源包与光影等分类。"})
        '下载 · 点最新版本，介绍加载器
        Steps.Add(New GuideStep With {
            .Target = Function() FindFirstItem("PanMinecraft"),
            .DependsOnPrevious = True,
            .WaitMs = 30000,
            .SkipIfMissingAfterMs = 10000,
            .Title = "选择最新版本",
            .Text = "点击列表中的最新版本，进入安装详情。",
            .Hint = "详情页可同时选择 Fabric、Forge、NeoForge、OptiFine 等加载器，选好后点击「开始下载」。"})
        '下载 · 版本详情页信息
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnStart"),
            .Hook = False,
            .DependsOnPrevious = True,
            .SkipIfMissingAfterMs = 3000,
            .WaitMs = 15000,
            .Title = "版本详情",
            .Text = "此页用于选择要安装的组件：原版 Minecraft，以及 Forge、NeoForge、Fabric、OptiFine 等加载器可在此一并选择。",
            .Hint = "选定后点击「开始下载」，启动器会依次下载并安装。"})
        '下载 · 退出详情
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnBack"),
            .SkipIfMissing = True,
            .NoSkip = True,
            .Title = "退出安装详情",
            .Text = "点击左上角的返回按钮，回到版本列表。"})
        '下载 · 进入 Mod
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("ItemMod"),
            .DependsOnPrevious = True,
            .Title = "进入「Mod」",
            .Text = "点击左侧「Mod」，此处可按版本、加载器与标签筛选 Mod。"})
        '下载 · 第一个 Mod 详情
        Steps.Add(New GuideStep With {
            .Target = Function() FindFirstItem("PanProjects"),
            .DependsOnPrevious = True,
            .WaitMs = 30000,
            .SkipIfMissingAfterMs = 10000,
            .Title = "打开一个 Mod",
            .Text = "点击列表中的第一个 Mod，进入它的详情页。",
            .Hint = "详情页安装时会自动放入当前所选版本的 mods 目录，并自动下载它依赖的前置 Mod。"})
        '下载 · Mod 详情页信息
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("CardIntro"),
            .Hook = False,
            .DependsOnPrevious = True,
            .SkipIfMissingAfterMs = 3000,
            .WaitMs = 15000,
            .Title = "Mod 详情",
            .Text = "详情页展示该 Mod 的名称、作者、简介、支持的版本与加载器、下载量以及可用文件列表。",
            .Hint = "在此页安装时会自动放入当前所选版本的 mods 目录，并自动下载前置 Mod。"})
        '下载 · 展开第一个版本条
        Steps.Add(New GuideStep With {
            .Target = Function() FindFirstItem("PanResults"),
            .WaitMs = 20000,
            .DependsOnPrevious = True,
            .SkipIfMissingAfterMs = 6000,
            .Title = "展开版本条",
            .Text = "点击第一个版本条可展开文件列表，其中列出可下载的具体版本与对应加载器。",
            .Hint = "展开后即可选择具体文件安装。"})
        '下载 · 退出 Mod 详情
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnFloatingBack"),
            .SkipIfMissing = True,
            .NoSkip = True,
            .Title = "退出 Mod 详情",
            .Text = "点击右下角的圆形返回键，回到 Mod 列表。"})
        '联机
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnTitleSelect2"),
            .Title = "进入「联机」",
            .Text = "点击上方「联机」，与好友联机无需配置端口。"})
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanSelect"),
            .DependsOnPrevious = True,
            .Hook = False,
            .Title = "创建与加入房间",
            .Text = "上半部分是创建房间：点击即可开一间房，把邀请码发给好友。下半部分是加入房间：粘贴好友发来的邀请码，回车即可加入。",
            .Hint = "本步骤的点击只用于继续教程，不会真的建房或加入。"})
        '种子
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnTitleSelectSeedMap"),
            .Title = "进入「种子」",
            .Text = "点击左侧「种子」，此处为种子地图入口。"})
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PageRight"),
            .Hook = False,
            .DependsOnPrevious = True,
            .Title = "种子地图",
            .Text = "输入种子与游戏版本即可查看地形、生物群系与结构分布，支持缩放与离线渲染。",
            .Hint = "从存档详情页也能直接为某个存档打开种子地图。"})
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanTopBar"),
            .Hook = False,
            .DependsOnPrevious = True,
            .Title = "顶部菜单",
            .Text = "顶部菜单从左到右依次是：世界种子输入框与「查看地图」按钮、当前指针的坐标与所在生物群系、当前缩放比例与「1 格 ≈ N 方块」的换算。",
            .Hint = "拖动可平移，滚轮缩放。"})
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanToolRail"),
            .Hook = False,
            .DependsOnPrevious = True,
            .Title = "左侧工具条",
            .Text = "左侧竖排工具条依次为：结构（标注生物群系结构位置）、定位（按坐标或结构搜索）、跳转、图层、网格、史莱姆区块、地形起伏、精度、3D 视角与 FPS 显示。",
            .Hint = "再次点击同一按钮可关闭对应叠加。"})
        '设置
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnTitleSelect3"),
            .Title = "进入「设置」",
            .Text = "点击上方「设置」，启动器的大部分行为可在此调整。"})
        '设置 · 分类菜单（讲解，不点进去）
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanItem"),
            .Hook = False,
            .DependsOnPrevious = True,
            .Title = "设置分类",
            .Text = "设置分为六类：启动、通用、个性化、系统、联机与高级。左侧点击可切换分类，右侧显示对应的选项。",
            .Hint = "主题色、背景图与标题栏样式位于「个性化」。"})
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PageRight"),
            .Hook = False,
            .DependsOnPrevious = True,
            .Title = "选项区域",
            .Text = "右侧显示当前分类的全部选项，修改后立即生效；大部分选项右侧带有说明文字。"})        '工具箱
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnTitleSelect4"),
            .Title = "进入「工具箱」",
            .Text = "点击上方「工具箱」，此处提供与游戏内容相关的工具。"})
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PanItem"),
            .Hook = False,
            .DependsOnPrevious = True,
            .Title = "工具列表",
            .Text = "左侧为工具分类：彩字生成、矿石分布、游戏统计、命令查询等，点击可切换。"})
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("PageRight"),
            .Hook = False,
            .DependsOnPrevious = True,
            .Title = "工具内容",
            .Text = "右侧为所选工具的界面，参数与结果都显示在这一区域。"})
        Steps.Add(New GuideStep With {
            .Target = Function() FindByName("BtnTitleSelect0"),
            .Title = "返回主页",
            .Text = "点击左上角「启动」返回主页，教程结束。"})
        '收尾
        Steps.Add(New GuideStep With {
            .Target = Function() Nothing,
            .Hook = False,
            .Title = "教程结束",
            .Text = "点击任意位置结束教程。需要重看时，可在「设置 → 通用 → 重新查看使用引导」中再次打开。",
            .Hint = "设置中还提供「空间体检」与「环境体检」。"})
    End Sub
    ''' <summary>进入下一步（不切页面，页面由用户自己点）。</summary>
    Private Sub NextStep()
        Try
            Unhook()
            CurrentIndex += 1
            If CurrentIndex >= Steps.Count Then
                [Stop]()
                Return
            End If
            CloseAccountMenu()
            '上一步被跳过时，依赖它的步骤直接跳过（不显示说明卡）
            If Steps(CurrentIndex).DependsOnPrevious AndAlso SkippedPrevious Then
                SkippedPrevious = True
                NextStep()
                Return
            End If
            '前置步骤被跳过时，依赖它的"返回键"步骤一起跳过
            If Steps(CurrentIndex).SkipIfMissing Then
                Dim Need As FrameworkElement = Nothing
                If Steps(CurrentIndex).Target IsNot Nothing Then
                    Try : Need = Steps(CurrentIndex).Target() : Catch : End Try
                End If
                If Not IsTargetReady(Need) Then
                    SkippedPrevious = True
                    NextStep()
                    Return
                End If
            End If
            EnsureOverlay()
            ShowCallout(Steps(CurrentIndex))
            WaitCount = 0
            TryHook(Steps(CurrentIndex))
        Catch ex As Exception
            Logger.Warn(ex, "引导进入下一步失败")
            [Stop]()
        End Try
    End Sub

    ''' <summary>目标控件可能还没随页面构建出来：每 200ms 重试，最多约 5 秒。</summary>
    Private Sub TryHook(S As GuideStep)
        If Not Running Then Return
        Dim T As FrameworkElement = Nothing
        If S.Target IsNot Nothing Then
            Try : T = S.Target() : Catch : End Try
        End If
        If IsTargetReady(T) Then
            RefreshLayout()
            If S.Hook Then
                Hooked = T
                AddHandler T.PreviewMouseLeftButtonDown, AddressOf OnTargetClick
            End If
            '页面入场动画会让目标继续移动，多补几次定位
            AniStart({AaCode(Sub() RefreshLayout(), 160),
                      AaCode(Sub() RefreshLayout(), 420),
                      AaCode(Sub() RefreshLayout(), 800)}, "Guide Settle")
            Return
        End If
        RefreshLayout()
        WaitCount += 1
        '前置步骤被跳过时目标不会出现，到点就跳过本步（纯讲解步骤同样适用）
        If S.SkipIfMissingAfterMs > 0 AndAlso WaitCount * 200 >= S.SkipIfMissingAfterMs Then
            SkippedPrevious = True
            NextStep()
            Return
        End If
        If S.Target Is Nothing Then Return '纯讲解步骤：不等目标
        If WaitCount * 200 > S.WaitMs Then Return
        AniStart({AaCode(Sub() TryHook(S), 200)}, "Guide Wait")
    End Sub
    Private Sub EnsureOverlay()
        If Overlay IsNot Nothing AndAlso Overlay.Parent IsNot Nothing Then Return
        Overlay = New Canvas With {.IsHitTestVisible = True}
        Panel.SetZIndex(Overlay, 9000)
        DarkTop = MakeMask()
        DarkBottom = MakeMask()
        DarkLeft = MakeMask()
        DarkRight = MakeMask()
        For Each R In {DarkTop, DarkBottom, DarkLeft, DarkRight}
            Overlay.Children.Add(R)
        Next
        '目标外围的呼吸光晕，先加在下面
        Glow = New Border With {.CornerRadius = New CornerRadius(11), .IsHitTestVisible = False, .Opacity = 0.18}
        Glow.SetResourceReference(Border.BackgroundProperty, "ColorBrush3")
        Overlay.Children.Add(Glow)
        Ring = New Border With {.BorderThickness = New Thickness(2), .CornerRadius = New CornerRadius(8), .IsHitTestVisible = False}
        Ring.SetResourceReference(Border.BorderBrushProperty, "ColorBrush3")
        Overlay.Children.Add(Ring)
        '纯讲解步骤用：铺满窗口的透明层，吞掉这次点击（不会触发下面的任何控件），点一下进下一步
        Catcher = New Rectangle With {.Fill = New MyColor(0.01, 0, 0, 0), .IsHitTestVisible = True, .Visibility = Visibility.Collapsed}
        AddHandler Catcher.MouseLeftButtonDown, Sub() NextStep()
        Overlay.Children.Add(Catcher)
        Callout = New Border With {.CornerRadius = New CornerRadius(12), .Padding = New Thickness(18, 15, 18, 13), .Width = CalloutWidth}
        Callout.SetResourceReference(Border.BackgroundProperty, "ColorBrush8")
        Callout.SetResourceReference(Border.BorderBrushProperty, "BrushBorder")
        Callout.BorderThickness = New Thickness(1)
        Overlay.Children.Add(Callout)
        FrmMain.PanForm.Children.Add(Overlay)
        '整层淡入，避免"啪"地一下出现
        Overlay.Opacity = 0
        AniStart({AaOpacity(Overlay, 1, 180)}, "Guide Fade In")
        '呼吸：亮度来回摆，不是闪
        Glow.BeginAnimation(UIElement.OpacityProperty,
            New DoubleAnimation(0.12, 0.34, New Duration(TimeSpan.FromMilliseconds(1200))) With {.AutoReverse = True, .RepeatBehavior = RepeatBehavior.Forever})
        If ResizeHandler Is Nothing Then
            ResizeHandler = Sub() RunInUi(Sub() RefreshLayout(False), True)
            AddHandler FrmMain.SizeChanged, ResizeHandler
        End If
        '注册弹窗等 MyMsgBox 会被遮罩挡住：有弹窗时自动收起遮罩，关掉后自动恢复
        If ModalTimer Is Nothing Then
            ModalTimer = New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(200)}
            AddHandler ModalTimer.Tick, Sub(s, e) SyncOverlayWithModal()
        End If
        If Not ModalTimer.IsEnabled Then ModalTimer.Start()
        If KeyHandler Is Nothing Then
            '兜底：随时按 ESC 退出教程
            KeyHandler = Sub(s, e) If e.Key = Key.Escape Then [Stop]() : e.Handled = True
            AddHandler FrmMain.PreviewKeyDown, KeyHandler
        End If
    End Sub

    Private Function MakeMask() As Rectangle
        Return New Rectangle With {.Fill = New MyColor(0.45, 0, 0, 0)}
    End Function

    Private Sub ShowCallout(S As GuideStep)
        Dim Stack As New StackPanel
        Dim Head As New TextBlock With {.Text = S.Title, .FontSize = 15, .FontWeight = FontWeights.SemiBold, .TextWrapping = TextWrapping.Wrap}
        Head.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Stack.Children.Add(Head)
        Dim Body As New TextBlock With {.Text = S.Text, .FontSize = 12.5, .Margin = New Thickness(0, 7, 0, 0), .TextWrapping = TextWrapping.Wrap, .LineHeight = 20}
        Body.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Stack.Children.Add(Body)
        If Not String.IsNullOrWhiteSpace(S.Hint) Then
            Dim Hint As New TextBlock With {.Text = S.Hint, .FontSize = 11.5, .Margin = New Thickness(0, 7, 0, 0), .TextWrapping = TextWrapping.Wrap, .Opacity = 0.62, .LineHeight = 18}
            Hint.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            Stack.Children.Add(Hint)
        End If
        '进度条
        Dim Bar As New Grid With {.Height = 4, .Margin = New Thickness(0, 14, 0, 12)}
        BarTrack = New Border With {.CornerRadius = New CornerRadius(2), .Height = 4, .HorizontalAlignment = HorizontalAlignment.Stretch}
        BarTrack.SetResourceReference(Border.BackgroundProperty, "ColorBrush6")
        Bar.Children.Add(BarTrack)
        BarFill = New Border With {.CornerRadius = New CornerRadius(2), .Height = 4, .HorizontalAlignment = HorizontalAlignment.Left}
        BarFill.SetResourceReference(Border.BackgroundProperty, "ColorBrush3")
        BarFill.Width = Math.Max(6, (CalloutWidth - 36) * (CurrentIndex + 1) / Math.Max(1, Steps.Count))
        Bar.Children.Add(BarFill)
        Stack.Children.Add(Bar)
        '底栏：步数 + 按钮
        Dim Foot As New Grid
        Dim Lbl As New TextBlock With {.Text = $"第 {CurrentIndex + 1} / {Steps.Count} 步", .FontSize = 11, .Opacity = 0.55, .VerticalAlignment = VerticalAlignment.Center}
        Lbl.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Foot.Children.Add(Lbl)
        Dim Row As New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Right}
        If CurrentIndex = Steps.Count - 1 Then
            Dim TipEnd As New TextBlock With {.Text = "点击任意位置结束", .FontSize = 11, .Opacity = 0.62, .VerticalAlignment = VerticalAlignment.Center}
            TipEnd.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            Row.Children.Add(TipEnd)
        ElseIf S.Hook Then
            If Not S.NoSkip Then Row.Children.Add(MakeButton("跳过本步", MyButton.ColorState.Normal, 90, Sub() SkipThisStep()))
        Else
            Dim Tip As New TextBlock With {.Text = "点击任意位置继续", .FontSize = 11, .Opacity = 0.62, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 6, 0)}
            Tip.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            Row.Children.Add(Tip)
        End If
        If CurrentIndex <> Steps.Count - 1 Then Row.Children.Add(MakeButton("退出引导", MyButton.ColorState.Red, 90, Sub() [Stop](), 8))
        Foot.Children.Add(Row)
        Stack.Children.Add(Foot)
        Callout.Child = Stack
        '说明卡内容淡入 + 轻微上浮
        If TypeOf Callout.RenderTransform IsNot TranslateTransform Then Callout.RenderTransform = New TranslateTransform()
        Dim Ct = CType(Callout.RenderTransform, TranslateTransform)
        Ct.Y = 10 : Callout.Opacity = 0
        AniStart({AaOpacity(Callout, 1, 170), AaDouble(Sub(i) Ct.Y += i, 0 - Ct.Y, 200, 0, New AniEaseOutFluent(AniEasePower.Weak))}, "Guide Callout In")
        '讲解类步骤：整窗透明层吞掉点击，点任意处进下一步，且不会触发下面的控件
        If Catcher IsNot Nothing Then Catcher.Visibility = If(S.Hook, Visibility.Collapsed, Visibility.Visible)
        Dim Goal = GoalRect()
        If Goal.IsEmpty Then
            '目标还没出来：首帧先把说明卡摆在中间，等目标出现再飞过去
            If DrawnRect.IsEmpty Then
                Dim W = FrmMain.PanForm.ActualWidth, H = FrmMain.PanForm.ActualHeight
                If W > 0 AndAlso H > 0 Then
                    Dim C As New Rect(W / 2 - 1, H / 2 - 1, 2, 2)
                    StopAnim() : DrawAt(C) : DrawnRect = C
                End If
            End If
            Return
        End If
        RefreshLayout(False)
    End Sub

    Private Function MakeButton(Text As String, Color As MyButton.ColorState, MinWidth As Double, ClickAction As Action, Optional MarginLeft As Double = 0) As MyButton
        Dim Btn As New MyButton With {.Text = Text, .Height = 30, .MinWidth = MinWidth, .Padding = New Thickness(10, 0, 10, 0), .ColorType = Color}
        If MarginLeft > 0 Then Btn.Margin = New Thickness(MarginLeft, 0, 0, 0)
        AddHandler Btn.Click, Sub() ClickAction()
        Return Btn
    End Function

    ''' <summary>按目标控件的位置摆放遮罩洞、高亮框与说明卡；Immediate 为 False 时做补间。</summary>
    Private Sub RefreshLayout(Optional Immediate As Boolean = False)
        Try
            If Overlay Is Nothing OrElse Callout Is Nothing Then Return
            Dim Goal = GoalRect()
            If Goal.IsEmpty Then Return
            If Immediate OrElse DrawnRect.IsEmpty Then
                StopAnim()
                DrawAt(Goal)
                DrawnRect = Goal
            ElseIf Math.Abs(Goal.X - DrawnRect.X) < 0.5 AndAlso Math.Abs(Goal.Y - DrawnRect.Y) < 0.5 AndAlso
                   Math.Abs(Goal.Width - DrawnRect.Width) < 0.5 AndAlso Math.Abs(Goal.Height - DrawnRect.Height) < 0.5 Then
                '位置没变，只补一次说明卡位置（文字可能刚测量完）
                DrawAt(Goal)
                DrawnRect = Goal
            Else
                StartMorph(Goal)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "摆放引导界面失败")
        End Try
    End Sub

    ''' <summary>当前目标应在的位置；找不到目标就退化为屏幕中间的小点。</summary>
    Private Function GoalRect() As Rect
        Dim W = FrmMain.PanForm.ActualWidth, H = FrmMain.PanForm.ActualHeight
        If W <= 0 OrElse H <= 0 Then Return Rect.Empty
        Dim S As GuideStep = If(CurrentIndex >= 0 AndAlso CurrentIndex < Steps.Count, Steps(CurrentIndex), Nothing)
        If S Is Nothing Then Return Rect.Empty
        If S.Target Is Nothing Then Return New Rect(W / 2 - 1, H / 2 - 1, 2, 2) '纯说明：居中
        Dim Target As FrameworkElement = Nothing
        Try : Target = S.Target() : Catch : End Try
        If Not IsTargetReady(Target) Then
            Return Rect.Empty '目标还没出来：保持上一帧，别瞬移
        End If
        Dim R As Rect
        Try
            R = Target.TransformToAncestor(FrmMain.PanForm).TransformBounds(New Rect(0, 0, Target.ActualWidth, Target.ActualHeight))
        Catch
            'Popup 里的控件（如右下角浮动返回键）不在同一棵可视树，改用屏幕坐标换算
            Try
                Dim Pt = FrmMain.PanForm.PointFromScreen(Target.PointToScreen(New Point(0, 0)))
                R = New Rect(Pt.X, Pt.Y, Target.ActualWidth, Target.ActualHeight)
            Catch
                Return New Rect(W / 2 - 1, H / 2 - 1, 2, 2)
            End Try
        End Try
        R.Inflate(6, 6)
        Return R
    End Function

    ''' <summary>按给定矩形画遮罩、光晕、高亮圈与说明卡。</summary>
    Private Sub DrawAt(R As Rect)
        Dim W = FrmMain.PanForm.ActualWidth, H = FrmMain.PanForm.ActualHeight
        DarkTop.Width = W : DarkTop.Height = Math.Max(0, R.Top) : Canvas.SetLeft(DarkTop, 0) : Canvas.SetTop(DarkTop, 0)
        DarkBottom.Width = W : DarkBottom.Height = Math.Max(0, H - R.Bottom) : Canvas.SetLeft(DarkBottom, 0) : Canvas.SetTop(DarkBottom, R.Bottom)
        DarkLeft.Width = Math.Max(0, R.Left) : DarkLeft.Height = R.Height : Canvas.SetLeft(DarkLeft, 0) : Canvas.SetTop(DarkLeft, R.Top)
        DarkRight.Width = Math.Max(0, W - R.Right) : DarkRight.Height = R.Height : Canvas.SetLeft(DarkRight, R.Right) : Canvas.SetTop(DarkRight, R.Top)
        If Catcher IsNot Nothing Then
            Catcher.Width = W : Catcher.Height = H : Canvas.SetLeft(Catcher, 0) : Canvas.SetTop(Catcher, 0)
        End If
        Glow.Width = R.Width + 10 : Glow.Height = R.Height + 10 : Canvas.SetLeft(Glow, R.Left - 5) : Canvas.SetTop(Glow, R.Top - 5)
        Ring.Width = R.Width : Ring.Height = R.Height : Canvas.SetLeft(Ring, R.Left) : Canvas.SetTop(Ring, R.Top)
        PlaceCallout(R, W, H)
    End Sub

    ''' <summary>说明卡跟随高亮框滑动：优先下方，其次上方，最后贴底。</summary>
    Private Sub PlaceCallout(R As Rect, W As Double, H As Double)
        Dim CalloutH = If(Callout.ActualHeight > 0, Callout.ActualHeight, 170)
        Dim Cy As Double = R.Bottom + 16
        If Cy + CalloutH > H - 8 Then Cy = R.Top - CalloutH - 16
        If Cy < 8 Then Cy = Math.Max(8, H - CalloutH - 8)
        Dim Cx As Double = Math.Min(Math.Max(8, R.Left), Math.Max(8, W - CalloutWidth - 8))
        Canvas.SetLeft(Callout, Cx)
        Canvas.SetTop(Callout, Cy)
    End Sub

    Private Sub StartMorph(Goal As Rect)
        If Anim Is Nothing Then
            Anim = New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(16)}
            AddHandler Anim.Tick, AddressOf OnAnimTick
        End If
        FromRect = DrawnRect
        ToRect = Goal
        AnimTick = 0
        If Not Anim.IsEnabled Then Anim.Start()
    End Sub

    Private Sub OnAnimTick(sender As Object, e As EventArgs)
        AnimTick += 1
        Dim T = Math.Min(1.0, AnimTick / CDbl(AnimFrames))
        Dim K = 1 - Math.Pow(1 - T, 3) 'ease-out
        Dim R As New Rect(FromRect.X + (ToRect.X - FromRect.X) * K,
                          FromRect.Y + (ToRect.Y - FromRect.Y) * K,
                          FromRect.Width + (ToRect.Width - FromRect.Width) * K,
                          FromRect.Height + (ToRect.Height - FromRect.Height) * K)
        DrawAt(R)
        DrawnRect = R
        If T >= 1 Then
            Anim.Stop()
            DrawAt(ToRect)
            DrawnRect = ToRect
        End If
    End Sub

    Private Sub StopAnim()
        If Anim IsNot Nothing AndAlso Anim.IsEnabled Then Anim.Stop()
    End Sub

    Private Sub SkipThisStep()
        SkippedPrevious = True
        NextStep()
    End Sub

    Private Sub OnTargetClick(sender As Object, e As MouseButtonEventArgs)
        '延迟一点再推进：先让目标自己的点击逻辑跑完（切页面、开弹窗、换主题）
        SkippedPrevious = False
        Dim S As GuideStep = If(CurrentIndex >= 0 AndAlso CurrentIndex < Steps.Count, Steps(CurrentIndex), Nothing)
        If S IsNot Nothing AndAlso S.RequireAccount AndAlso Not HasAccount() Then
            '这一步要用户真的注册出账户：等账户建好再自动继续（中途关掉弹窗也不会卡死，可用「跳过本步」）
            AniStart({AaCode(Sub() WaitAccount(S, 0), 500)}, "Guide Advance")
        Else
            AniStart({AaCode(Sub() NextStep(), S.AdvanceDelay)}, "Guide Advance")
        End If
    End Sub

    ''' <summary>等账户池里出现账户，最多约 90 秒。</summary>
    Private Sub WaitAccount(S As GuideStep, Tick As Integer)
        If Not Running Then Return
        If HasAccount() Then
            NextStep()
            Return
        End If
        If Tick > 180 Then Return
        AniStart({AaCode(Sub() WaitAccount(S, Tick + 1), 500)}, "Guide WaitAccount")
    End Sub

    ''' <summary>账户池里是否已有账户。</summary>
    Private Function HasAccount() As Boolean
        Try
            Dim List = AccountPool.GetAllAccounts()
            Return List IsNot Nothing AndAlso List.Count > 0
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Sub Unhook()
        If Hooked Is Nothing Then Return
        Try : RemoveHandler Hooked.PreviewMouseLeftButtonDown, AddressOf OnTargetClick : Catch : End Try
        Hooked = Nothing
    End Sub

    ''' <summary>结束引导并清理界面。</summary>
    Public Sub [Stop]()
        Try
            Running = False
            Unhook()
            StopAnim()
            If Anim IsNot Nothing Then
                Try : RemoveHandler Anim.Tick, AddressOf OnAnimTick : Catch : End Try
                Anim = Nothing
            End If
            If ResizeHandler IsNot Nothing Then
                Try : RemoveHandler FrmMain.SizeChanged, ResizeHandler : Catch : End Try
                ResizeHandler = Nothing
            End If
            If KeyHandler IsNot Nothing Then
                Try : RemoveHandler FrmMain.PreviewKeyDown, KeyHandler : Catch : End Try
                KeyHandler = Nothing
            End If
            If ModalTimer IsNot Nothing Then
                Try : ModalTimer.Stop() : Catch : End Try
                ModalTimer = Nothing
            End If
            '呼吸动画是 Forever 的，必须显式停掉，否则脱离可视树后仍在走时钟
            If Glow IsNot Nothing Then
                Try : Glow.BeginAnimation(UIElement.OpacityProperty, Nothing) : Catch : End Try
            End If
            If Overlay IsNot Nothing Then
                Try : FrmMain.PanForm.Children.Remove(Overlay) : Catch : End Try
                Overlay = Nothing
            End If
            Callout = Nothing
            Catcher = Nothing
            Ring = Nothing
            Glow = Nothing
            BarFill = Nothing
            BarTrack = Nothing
            DrawnRect = Rect.Empty
            OverlayHiddenByModal = False
            MarkTourFinished()
        Catch ex As Exception
            Logger.Warn(ex, "结束引导失败")
        End Try
    End Sub

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return Running
        End Get
    End Property

    ''' <summary>账户下拉面板会被遮罩挡住，离开这一步时顺手收起。</summary>
    Private Sub CloseAccountMenu()
        Try
            Dim Menu = FindByName("PanAccountMenu")
            If Menu IsNot Nothing AndAlso Menu.Visibility = Visibility.Visible Then Menu.Visibility = Visibility.Collapsed
        Catch ex As Exception
            Logger.Warn(ex, "收起账户面板失败")
        End Try
    End Sub

    ''' <summary>先在窗体上找，找不到再遍历可视树（页面里的控件不在窗体命名作用域内）。</summary>
    Private Function FindByName(Name As String) As FrameworkElement
        '左右面板没有名字，按属性取
        If Name = "PageRight" Then Return FrmMain.PageRight
        If Name = "PageLeft" Then Return FrmMain.PageLeft
        Try
            Dim O = FrmMain.FindName(Name)
            If TypeOf O Is FrameworkElement Then Return O
        Catch
        End Try
        Dim R = FindInTree(FrmMain, Name)
        If R IsNot Nothing Then Return R
        'Popup 的内容不在主可视树里（例如右下角浮动返回键）
        Dim Pop = TryCast(FrmMain.FindName("PopFloatingBack"), Popup)
        If Pop IsNot Nothing Then
            Dim Child = TryCast(Pop.Child, FrameworkElement)
            If Child IsNot Nothing Then
                If Child.Name = Name Then Return Child
                Return FindInTree(Child, Name)
            End If
        End If
        Return Nothing
    End Function

    Private Function FindInTree(Parent As DependencyObject, Name As String) As FrameworkElement
        If Parent Is Nothing Then Return Nothing
        Dim Count = VisualTreeHelper.GetChildrenCount(Parent)
        For i = 0 To Count - 1
            Dim Child = VisualTreeHelper.GetChild(Parent, i)
            Dim Fe = TryCast(Child, FrameworkElement)
            If Fe IsNot Nothing AndAlso Fe.Name = Name Then Return Fe
            Dim R = FindInTree(Child, Name)
            If R IsNot Nothing Then Return R
        Next
        Return Nothing
    End Function

    ''' <summary>目标是否已经可以高亮（Popup 里的控件在弹层打开时才算就绪）。</summary>
    Private Function IsTargetReady(T As FrameworkElement) As Boolean
        If T Is Nothing OrElse T.ActualWidth <= 0 OrElse T.ActualHeight <= 0 Then Return False
        Dim P = TryCast(T.Parent, Popup)
        If P IsNot Nothing Then Return P.IsOpen
        Return T.IsVisible
    End Function
    ''' <summary>有 MyMsgBox 弹窗（注册账户、确认框）时把遮罩收起来，否则它会挡住弹窗。</summary>
    Private Sub SyncOverlayWithModal()
        Try
            If Overlay Is Nothing Then Return
            Dim HasModal As Boolean = False
            Try
                'WaitingMyMsgBox 会在弹窗显示时出队，判不准；直接看 PanMsg 里有没有对话框
                HasModal = FrmMain.PanMsg IsNot Nothing AndAlso FrmMain.PanMsg.Children.Count > 0
            Catch ex As Exception
            End Try
            If Not HasModal Then
                Try
                    HasModal = WaitingMyMsgBox IsNot Nothing AndAlso WaitingMyMsgBox.Count > 0
                Catch ex As Exception
                End Try
            End If
            If HasModal Then
                If Not OverlayHiddenByModal Then
                    OverlayHiddenByModal = True
                    AniStart({AaOpacity(Overlay, 0, 130),
                              AaCode(Sub() If Overlay IsNot Nothing Then Overlay.Visibility = Visibility.Collapsed, , True)}, "Guide Hide For Modal")
                End If
            Else
                If OverlayHiddenByModal Then
                    OverlayHiddenByModal = False
                    Overlay.Visibility = Visibility.Visible
                    AniStart({AaOpacity(Overlay, 1, 160)}, "Guide Show After Modal")
                End If
            End If
        Catch ex As Exception
        End Try
    End Sub
    ''' <summary>取容器里的第一个列表项（版本列表是 MyListItem，资源列表是 MyResourceItem，都没有固定名称）。</summary>
    Private Function FindFirstItem(ContainerName As String) As FrameworkElement
        Try
            Dim Container = FindByName(ContainerName)
            If Container Is Nothing Then Return Nothing
            Dim Fallback As FrameworkElement = Nothing
            Dim Hit = FindDescendant(Container, {"MyListItem", "MyResourceItem"}, Fallback)
            If Hit IsNot Nothing Then Return Hit
            Return Fallback '实在没有，就退到第一个卡片
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function FindDescendant(Parent As DependencyObject, TypeNames As String(), ByRef Fallback As FrameworkElement) As FrameworkElement
        If Parent Is Nothing Then Return Nothing
        Dim Count = VisualTreeHelper.GetChildrenCount(Parent)
        For i = 0 To Count - 1
            Dim Child = VisualTreeHelper.GetChild(Parent, i)
            Dim Fe = TryCast(Child, FrameworkElement)
            If Fe IsNot Nothing AndAlso Fe.IsVisible AndAlso Fe.ActualWidth > 0 Then
                Dim N = Fe.GetType().Name
                If Array.IndexOf(TypeNames, N) >= 0 Then Return Fe
                If N = "MyCard" AndAlso Fallback Is Nothing Then Fallback = Fe
            End If
            Dim R = FindDescendant(Child, TypeNames, Fallback)
            If R IsNot Nothing Then Return R
        Next
        Return Nothing
    End Function
End Module
