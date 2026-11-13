'缘启 · 休眠观赏页（沉浸待机）
'
'这不是启动页，是一个休眠 / 观赏页：启动器放着不管的时候点一下「全屏」，窗口变成无边框全屏，
'画面只剩风景、时间、日期和当前版本，没有任何操作 —— 想回去就点一下屏幕或按 ESC。
'设计与预览见 docs\沉浸模式-设计方案.md 与 docs\immersive-preview.html。
'
'画面只有三样东西：字标 / 时间 / 当前版本，全部压在左上与左下，中央与右上完全留给风景。
'全部尺寸按视口高度算（不按宽度），否则 21:9 上 UI 会缩成一小撮。

Imports System.Globalization
Imports System.Windows.Media.Animation
Imports System.Windows.Media.Effects
Imports System.Windows.Media.Media3D
Imports System.Windows.Threading

Public Class MySleepOverlay

    '动画曲线：与预览页 CSS 的 cubic-bezier 一一对应
    '慢速落位：中段仍在动，尾巴收得软
    Private Shared ReadOnly EaseSettle As New KeySpline(0.25, 0.6, 0.4, 1)
    '退出必须快，做长了会显得迟钝
    Private Shared ReadOnly EaseExit As New KeySpline(0.4, 0, 0.2, 1)
    '先亮起来
    Private Shared ReadOnly EaseLight As New KeySpline(0.2, 0.8, 0.25, 1)

    Private ReadOnly TimerClock As New DispatcherTimer()
    Private ReadOnly WeekNames As String() = {"周日", "周一", "周二", "周三", "周四", "周五", "周六"}

    Private _Active As Boolean = False
    ''' <summary>进入时刻，用来忽略「进入的那一下」被同一个点击顺手带走。</summary>
    Private _EnteredAt As Long = 0
    ''' <summary>资源加载令牌：退出或重新进入后，迟到的加载结果直接丢掉。</summary>
    Private _LastMinute As Integer = -1
    Private _LastInstance As McInstance
    ''' <summary>进入前抓下的启动器画面，用来遮住窗口尺寸突变的那一帧。</summary>
    Private _Phantom As BitmapSource
    ''' <summary>该画面在覆盖层坐标系里的位置。</summary>
    Private _PhantomRect As Rect

    Public Sub New()
        InitializeComponent()

        AddHandler SizeChanged, AddressOf OnOverlaySizeChanged
        AddHandler BdClick.MouseLeftButtonUp, AddressOf OnClickExit

        RectGrain.Fill = MakeGrainBrush()
        'WPF 的文字没有 text-shadow，用零模糊的 DropShadowEffect 得到游戏内那种硬阴影
        ApplyTextShadow(LabDate)
        ApplyTextShadow(LabInstanceMeta)
        ApplyTextShadow(LabHint)

        TimerClock.Interval = TimeSpan.FromSeconds(10)
        AddHandler TimerClock.Tick, AddressOf OnClockTick
    End Sub

#Region "对外接口"

    ''' <summary>是否正处在休眠态（进场动画播放中也算）。</summary>
    Public ReadOnly Property IsSleeping As Boolean
        Get
            Return _Active
        End Get
    End Property

    ''' <summary>
    ''' 铺场：只把「启动器画面」摆好，先不播动画。
    ''' 主窗口在显示休眠页之前调用它 —— 摆好以后屏幕看起来和刚才一模一样。
    ''' </summary>
    ''' <param name="Instance">当前实例，用于显示实例名与真实版本；可为 Nothing。</param>
    ''' <param name="Phantom">进入前抓下的启动器画面；为 Nothing 时这一层不显示。</param>
    ''' <param name="PhantomRect">该画面在覆盖层坐标系里的位置（原尺寸摆回原来的地方）。</param>
    Public Sub Prepare(Instance As McInstance, Phantom As BitmapSource, PhantomRect As Rect)
        _LastInstance = Instance
        _Phantom = Phantom
        _PhantomRect = PhantomRect
        _Active = True
        _EnteredAt = GetTimeMs()

        Visibility = Visibility.Visible
        ResetVisualState()

        '先按住风景与文字、把启动器画面摆上，再去做重活。
        '下面读字体、算度量要花一百多毫秒，这期间窗口可能已经画出去一帧；
        '要是那会儿风景还是可见的，就会闪出一帧完整场景（进全屏时"先闪一下"就是这么来的）。
        ClearAnim(PanWorld, UIElement.OpacityProperty)
        PanWorld.Opacity = 0
        ClearAnim(PanUi, UIElement.OpacityProperty)
        PanUi.Opacity = 0
        ShowPhantom()

        '位图字体图集只有 2KB，同步读掉即可；读不到就退回启动器自带像素字，不影响页面
        Try
            ModImmersiveAssets.EnsureFont(Instance)
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：加载位图字体失败")
        End Try
        For Each Element As MyMcText In {LabBrandSub, LabClock, LabInstanceName, LabEsc}
            Element.Refresh()
        Next
        '窗口刚被改成全屏，布局可能还没跑完；先强制一次，尺寸不对时 SizeChanged 会再补
        UpdateLayout()
        ApplyMetrics()
        RefreshClock(True)
        RefreshInstance(Instance)
        UpdateLayout()
        '全景在进场之前就建好：进场第一帧就是真背景，不会先闪一下渐变底再「换掉」
        TryBuildPanoramaNow()
    End Sub

    ''' <summary>
    ''' 立刻把全景搭起来。素材在启动器里预热过，这里只是拼六个面，很快；
    ''' 万一没预热，也在这里同步读一次（一次性代价），换来的是进场第一帧就对。
    ''' </summary>
    Private Sub TryBuildPanoramaNow()
        '先收掉上一次留下的场景，免得这一套读不出来时还挂着上一套
        Viewport.Visibility = Visibility.Collapsed
        Viewport.Children.Clear()
        RectFallback.Visibility = Visibility.Visible
        RectFallbackGlow.Visibility = Visibility.Visible
        Try
            Dim Faces As BitmapSource() = ModImmersiveAssets.GetPanoramaFaces(_LastInstance)
            If Faces IsNot Nothing Then BuildPanorama(Faces)
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：构建全景失败")
        End Try
    End Sub

    ''' <summary>窗口已经贴到全屏，开始播进场动画。</summary>
    Public Sub PlayEnter()
        _EnteredAt = GetTimeMs()
        ClearAnim(PanUi, UIElement.OpacityProperty)
        PanUi.Opacity = 1
        PlayIntro()
        TimerClock.Start()
    End Sub

    ''' <summary>
    ''' 进入休眠态。由主窗口在窗口全屏化、隐藏标题栏之后调用。
    ''' </summary>
    ''' <param name="Instance">当前实例，用于显示实例名与真实版本；可为 Nothing。</param>
    ''' <param name="Phantom">进入前抓下的启动器画面，用于顶住窗口尺寸突变的那几帧；可为 Nothing。</param>
    ''' <param name="PhantomRect">该画面在覆盖层坐标系里的位置（原尺寸摆回原来的地方）。</param>
    Public Sub Begin(Instance As McInstance, Optional Phantom As BitmapSource = Nothing, Optional PhantomRect As Rect = Nothing)
        Prepare(Instance, Phantom, PhantomRect)
        PlayEnter()
    End Sub

    ''' <summary>把幻影按当前 _PhantomRect 摆好并显示出来（含那层假桌面）。</summary>
    Private Sub ShowPhantom()
        If _Phantom Is Nothing OrElse _PhantomRect.Width <= 0 OrElse _PhantomRect.Height <= 0 Then
            PanPhantom.Visibility = Visibility.Collapsed
            Return
        End If
        ClearAnim(PanPhantom, UIElement.OpacityProperty)
        PanPhantom.Opacity = 1
        PanPhantom.Visibility = Visibility.Visible

        ClearAnim(ImgPhantom, UIElement.OpacityProperty)
        ImgPhantom.Opacity = 1
        ImgPhantom.RenderTransform = New ScaleTransform(1, 1)
        PlacePhantom()
    End Sub

    ''' <summary>
    ''' 退出休眠态：文字先走、风景放大淡出，同时把启动器画面浮回来。
    ''' 覆盖层本身由主窗口在窗口缩回去的同一刻折叠，这样看不到「先瞬间变小再黑一下」。
    ''' </summary>
    Public Sub [End]()
        If Not _Active Then Return
        _Active = False
        TimerClock.Stop()
        PlayOutro()
    End Sub

#End Region

#Region "尺寸"

    ''' <summary>取整到 8 的倍数：位图字的硬要求，否则字形像素会被拉伸到非整数像素上。</summary>
    Private Shared Function Q8(Value As Double) As Double
        Return Math.Round(Value / 8) * 8
    End Function

    Private Sub OnOverlaySizeChanged(sender As Object, e As SizeChangedEventArgs)
        ApplyMetrics()
    End Sub

    ''' <summary>
    ''' 全部按视口高度算，宽度只作上限。字格必须是 8 的倍数。
    ''' </summary>
    Private Sub ApplyMetrics()
        Dim W As Double = ActualWidth
        Dim H As Double = ActualHeight
        If W <= 0 OrElse H <= 0 Then Return

        Dim Pad As Double = Math.Round(Math.Min(84, Math.Max(28, Math.Min(W * 0.042, H * 0.072))))
        PanBrand.Margin = New Thickness(Pad, Pad, 0, 0)
        PanDock.Margin = New Thickness(Pad, 0, 0, Pad)
        PanHint.Margin = New Thickness(0, 0, Pad, Pad + 10)

        '硬投影的深浅跟预览页一致：字标最重，时钟次之，其余更淡
        LabBrandSub.ShadowBrush = ShadowBrushOf(200)
        LabClock.ShadowBrush = ShadowBrushOf(112)
        LabInstanceName.ShadowBrush = ShadowBrushOf(140)
        ImgWordmark.ShadowBrush = ShadowBrushOf(140)

        '时钟字格：占高度 10.5%，再受宽度约束，夹在 56~160
        Dim ClockCell As Double = Math.Max(56, Math.Min(160, Math.Min(Q8(H * 0.105), Q8(W * 0.075))))
        LabClock.Cell = ClockCell
        LabClock.ShadowDepth = Math.Max(2, Math.Round(ClockCell / 19))

        '字标与 YUANQI 共用颗粒尺寸：字格 16/24/32 → 位图字每个字形像素 2/3/4px
        Dim BrandCell As Double = Math.Max(16, Math.Min(32, Q8(H * 0.0178)))
        LabBrandSub.Cell = BrandCell
        LabBrandSub.ShadowDepth = Math.Max(1, Math.Round(BrandCell / 8))
        ImgWordmark.BlockSize = CInt(BrandCell / 8)
        ImgWordmark.ShadowDepth = 3
        '字标与 YUANQI 之间的间距只在这里给一次，控件自身的 Margin 必须为 0，否则会叠成两倍
        LabBrandSub.Margin = New Thickness(0, Math.Max(8, Math.Min(18, Math.Round(H * 0.011))), 0, 0)

        '实例名字格
        Dim VersionCell As Double = Math.Max(24, Math.Min(48, Q8(H * 0.0355)))
        LabInstanceName.Cell = VersionCell
        LabInstanceName.ShadowDepth = Math.Max(1, Math.Round(VersionCell / 16))
        LabInstanceNameText.FontSize = Math.Max(17, Math.Min(26, H * 0.021))

        '提示键帽：中文提示字号跟着键帽走，两者才不会一大一小
        Dim HintCell As Double = Math.Max(16, Math.Min(24, Q8(H * 0.0155)))
        LabEsc.Cell = HintCell
        LabHint.FontSize = Math.Max(10, HintCell * 0.78)
        BdEsc.Padding = New Thickness(Math.Round(HintCell * 0.25), Math.Round(HintCell * 0.19),
                                      Math.Round(HintCell * 0.34), Math.Round(HintCell * 0.26))

        LabDate.FontSize = Math.Max(12.5, Math.Min(17, H * 0.015))
        LabInstanceMeta.FontSize = Math.Max(11.5, Math.Min(15, H * 0.013))
        '位图字要落在整数像素上才不会被抗锯齿糊掉，所以位置相关的量一律取整
        LabDate.Margin = New Thickness(0, Math.Round(Math.Max(10, H * 0.012)), 0, 0)
        RectRule.Width = Math.Max(40, Math.Min(54, H * 0.03))
        RectRule.Margin = New Thickness(0, Math.Round(Math.Max(14, H * 0.017)), 0, 0)
        PanInstance.Margin = New Thickness(0, Math.Round(Math.Max(12, H * 0.016)), 0, 0)
        RectSep.Height = Math.Max(18, Math.Min(26, H * 0.02))
    End Sub

    ''' <summary>硬投影的颜色：纯黑，按 alpha 深浅区分层级。</summary>
    Private Shared Function ShadowBrushOf(Alpha As Byte) As Brush
        Dim Brush As New SolidColorBrush(Color.FromArgb(Alpha, 0, 0, 0))
        Brush.Freeze()
        Return Brush
    End Function

#End Region

#Region "内容"

    Private Sub OnClockTick(sender As Object, e As EventArgs)
        RefreshClock(False)
    End Sub

    Private Sub RefreshClock(Force As Boolean)
        Dim Now As Date = Date.Now
        If Not Force AndAlso Now.Minute = _LastMinute Then Return
        _LastMinute = Now.Minute
        Dim ClockText As String = Now.ToString("HH:mm", CultureInfo.InvariantCulture)
        If LabClock.Text <> ClockText Then LabClock.Text = ClockText
        LabDate.Text = $"{Now.Month}月{Now.Day}日 · {WeekNames(CInt(Now.DayOfWeek))}"
    End Sub

    ''' <summary>
    ''' 实例名与真实版本信息是两个字段，不是重复信息：
    ''' 前者是启动器「当前实例」那一行（用户可改，也可以叫「我的生存」），后者是发布通道 + 实际游戏版本。
    ''' </summary>
    Private Sub RefreshInstance(Instance As McInstance)
        If Instance Is Nothing Then
            PanInstance.Visibility = Visibility.Collapsed
            Return
        End If
        PanInstance.Visibility = Visibility.Visible

        Dim Name As String = If(Instance.Name, "")
        '位图字只有 ASCII；中文实例名（「我的生存」之类）和过长的整合包名退回系统字体，避免排到屏幕外
        Dim UseBitmap As Boolean = Name.Length > 0 AndAlso Name.Length <= 8 AndAlso IsAsciiText(Name)
        If UseBitmap Then
            LabInstanceName.Visibility = Visibility.Visible
            LabInstanceNameText.Visibility = Visibility.Collapsed
            If LabInstanceName.Text <> Name Then LabInstanceName.Text = Name
        Else
            LabInstanceName.Visibility = Visibility.Collapsed
            LabInstanceNameText.Visibility = Visibility.Visible
            LabInstanceNameText.Text = Name
        End If

        Dim Meta As String = ""
        Try
            Meta = Instance.VersionDisplayName()
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：读取版本信息失败")
        End Try
        If String.IsNullOrEmpty(Meta) Then
            LabInstanceMeta.Visibility = Visibility.Collapsed
            RectSep.Visibility = Visibility.Collapsed
        Else
            LabInstanceMeta.Visibility = Visibility.Visible
            RectSep.Visibility = Visibility.Visible
            LabInstanceMeta.Text = Meta
        End If
    End Sub

    Private Shared Function IsAsciiText(Text As String) As Boolean
        For i = 0 To Text.Length - 1
            Dim Code As Integer = AscW(Text(i))
            If Code < 32 OrElse Code > 126 Then Return False
        Next
        Return True
    End Function

#End Region

#Region "全景"

    ''' <summary>
    ''' 相机水平视角。立方体每个面张角 90°，70° 时一屏约看到一个面的 7 成宽，
    ''' 与预览页的取景基本一致；调小会明显「推近」，画面只剩几个方块。
    ''' </summary>
    Private Const PanoramaFieldOfView As Double = 70

    ''' <summary>
    ''' 搭一个朝内的立方体，相机放在中心：拿到的是与游戏标题界面完全一致的观感。
    ''' 每个面同时设置 Material 与 BackMaterial，相机在内部也看得到。
    ''' </summary>
    Private Sub BuildPanorama(Faces As BitmapSource())
        If Faces Is Nothing OrElse Faces.Length < 6 Then Return
        Try
            Const Half As Double = 1200
            Dim Group As New Model3DGroup()
            'DiffuseMaterial 需要光源才不发黑；纯白环境光等于原图亮度
            Group.Children.Add(New AmbientLight(Colors.White))

            '顺序与 panorama_0..5 一致：前 / 右 / 后 / 左 / 上 / 下
            '每个面的第一个顶点对应贴图的左上角（u,v = 0,0），所以侧面的第一个顶点必须是 y = +1，
            '写反了整幅全景会上下颠倒（看起来像世界被吊在天花板上）
            AddFace(Group, Faces(0), {-1, 1, -1}, {1, 1, -1}, {1, -1, -1}, {-1, -1, -1}, Half)
            AddFace(Group, Faces(1), {1, 1, -1}, {1, 1, 1}, {1, -1, 1}, {1, -1, -1}, Half)
            AddFace(Group, Faces(2), {1, 1, 1}, {-1, 1, 1}, {-1, -1, 1}, {1, -1, 1}, Half)
            AddFace(Group, Faces(3), {-1, 1, 1}, {-1, 1, -1}, {-1, -1, -1}, {-1, -1, 1}, Half)
            AddFace(Group, Faces(4), {-1, 1, 1}, {1, 1, 1}, {1, 1, -1}, {-1, 1, -1}, Half)
            AddFace(Group, Faces(5), {-1, -1, -1}, {1, -1, -1}, {1, -1, 1}, {-1, -1, 1}, Half)

            '只绕 Y 轴匀速旋转。不再叠加俯仰摆动：那条 26s 的上下漂移在待机页上会被读成「画面在起伏」
            Dim Yaw As New AxisAngleRotation3D(New Vector3D(0, 1, 0), 0)
            Dim Transform As New Transform3DGroup()
            Transform.Children.Add(New RotateTransform3D(Yaw))

            Dim Visual As New ModelVisual3D()
            Visual.Content = Group
            Visual.Transform = Transform

            Viewport.Children.Clear()
            Viewport.Camera = New PerspectiveCamera With {
                .Position = New Point3D(0, 0, 0),
                .LookDirection = New Vector3D(0, 0, -1),
                .UpDirection = New Vector3D(0, 1, 0),
                .FieldOfView = PanoramaFieldOfView,
                .NearPlaneDistance = 1,
                .FarPlaneDistance = 10000}
            Viewport.Children.Add(Visual)
            Viewport.Visibility = Visibility.Visible
            RectFallback.Visibility = Visibility.Collapsed
            RectFallbackGlow.Visibility = Visibility.Collapsed

            StartPanoramaSpin(Yaw)
        Catch ex As Exception
            Logger.Warn(ex, "休眠页：构建全景失败")
        End Try
    End Sub

    ''' <summary>
    ''' 六个面用同一组角点坐标，相邻面共用一条边 —— 既不裂开也不会互相压住。
    ''' （早先为了盖住接缝让每个面外扩 0.2%，两个面在接缝处叠在一起会打架，
    '''   转起来就是一层会跑的波纹，所以这里改成严格共边。）
    ''' </summary>

    Private Shared Sub AddFace(Group As Model3DGroup, Texture As BitmapSource,
                               TL As Double(), TR As Double(), BR As Double(), BL As Double(), Half As Double)
        If Texture Is Nothing Then Return
        '四个角直接按 ±1 落在立方体角上：相邻两面算出来的边完全重合，接缝处不裂也不叠
        Dim Mesh As New MeshGeometry3D()
        Mesh.Positions.Add(ToPoint(TL, Half))
        Mesh.Positions.Add(ToPoint(TR, Half))
        Mesh.Positions.Add(ToPoint(BR, Half))
        Mesh.Positions.Add(ToPoint(BL, Half))
        Mesh.TextureCoordinates.Add(New Point(0, 0))
        Mesh.TextureCoordinates.Add(New Point(1, 0))
        Mesh.TextureCoordinates.Add(New Point(1, 1))
        Mesh.TextureCoordinates.Add(New Point(0, 1))
        Mesh.TriangleIndices.Add(0)
        Mesh.TriangleIndices.Add(1)
        Mesh.TriangleIndices.Add(2)
        Mesh.TriangleIndices.Add(0)
        Mesh.TriangleIndices.Add(2)
        Mesh.TriangleIndices.Add(3)

        Dim Fill As New ImageBrush(Texture) With {.Stretch = Stretch.Fill}
        Fill.Freeze()
        Dim Material As New DiffuseMaterial(Fill)
        Dim Model As New GeometryModel3D(Mesh, Material)
        Model.BackMaterial = Material
        Group.Children.Add(Model)
    End Sub

    Private Shared Function ToPoint(Corner As Double(), Half As Double) As Point3D
        Return New Point3D(Corner(0) * Half, Corner(1) * Half, Corner(2) * Half)
    End Function

    ''' <summary>
    ''' 绕 Y 轴 360° / 190s 匀速旋转（1.9°/s）。
    ''' 全部走 GPU；静止时 CPU 基本为 0，这正是待机页需要的。
    ''' </summary>
    Private Sub StartPanoramaSpin(Yaw As AxisAngleRotation3D)
        '每次进场从随机方向开始：同一个机位看久了太死板
        Dim StartAngle As Double = SpinRandom.Next(0, 360)
        Logger.Info($"休眠页：全景起始方向 {StartAngle:0}°")
        '省电 / 无障碍：不转，随机停在一个方向，画面依然完整
        If Not SystemParameters.ClientAreaAnimation OrElse (RenderCapability.Tier >> 16) < 2 Then
            Yaw.Angle = StartAngle
            Return
        End If
        Yaw.Angle = StartAngle
        Dim Spin As New DoubleAnimation(StartAngle, StartAngle + 360, New Duration(TimeSpan.FromSeconds(190))) With {
            .RepeatBehavior = RepeatBehavior.Forever}
        Yaw.BeginAnimation(AxisAngleRotation3D.AngleProperty, Spin)
    End Sub

    ''' <summary>随机起始方向用（每次进场换一个机位）。</summary>
    Private Shared ReadOnly SpinRandom As New Random()

#End Region

#Region "过场动画"

    ''' <summary>
    ''' 进入：一次「整体落位」。场景、界面两层同时起步，共用同一条缓动、不同幅度，形成视差。
    ''' 观感是镜头穿过窗口后慢慢停稳，收尾的是风景。
    ''' </summary>
    Private Sub PlayIntro()
        '场景层：略大处收缩到位，先亮起来
        TfWorld.ScaleX = 1.08
        TfWorld.ScaleY = 1.08
        PanWorld.Opacity = 0
        Anim(TfWorld, ScaleTransform.ScaleXProperty, 1.08, 1.0, 1050, 0, EaseSettle)
        Anim(TfWorld, ScaleTransform.ScaleYProperty, 1.08, 1.0, 1050, 0, EaseSettle)
        Anim(PanWorld, UIElement.OpacityProperty, 0, 1, 420, 0, EaseLight)

        '启动器幻影：按原尺寸留在原处，只轻微放大并溶解，观感是镜头穿过窗口走进风景
        PlayPhantomIntro()

        '界面层：逐块落位，时间与幅度错开，读起来是一个动作而不是七段各自播放的淡入
        RiseIn(ImgWordmark, 50, 700, 10, 1.03)
        RiseIn(LabBrandSub, 200, 760, 9, 1.02)
        RiseIn(LabClock, 160, 800, 16, 1.07)
        RiseIn(LabDate, 260, 880, 10, 1.02)
        GrowRule(RectRule, 320, 900)
        RiseIn(PanInstance, 360, 820, 11, 1.03)
        RiseIn(PanHint, 900, 820, 9, 1.0)
    End Sub

    ''' <summary>
    ''' 进场时把启动器画面摆回原来的位置，轻微放大 + 溶解。
    ''' 四周不铺任何底色：这层是透明的，露出来的是真实桌面，所以看不到「先黑一下」。
    ''' 没有抓到画面时整层不显示。
    ''' </summary>
    Private Sub PlayPhantomIntro()
        If _Phantom Is Nothing OrElse _PhantomRect.Width <= 0 OrElse _PhantomRect.Height <= 0 Then
            PanPhantom.Visibility = Visibility.Collapsed
            Return
        End If
        PlacePhantom()
        PanPhantom.Visibility = Visibility.Visible
        PanPhantom.Opacity = 1
        Dim Scale As New ScaleTransform(1, 1)
        ImgPhantom.RenderTransform = Scale
        ImgPhantom.Opacity = 1
        '放大一点点再溶解：幅度小才像镜头推近，大了就像被拽走
        Anim(Scale, ScaleTransform.ScaleXProperty, 1, 1.06, 700, 0, EaseSettle)
        Anim(Scale, ScaleTransform.ScaleYProperty, 1, 1.06, 700, 0, EaseSettle)
        Anim(ImgPhantom, UIElement.OpacityProperty, 1, 0, 620, 0, EaseLight)
    End Sub

    ''' <summary>把幻影按原尺寸摆到它在覆盖层里的位置。</summary>
    Private Sub PlacePhantom()
        ImgPhantom.Source = _Phantom
        Canvas.SetLeft(ImgPhantom, Math.Round(_PhantomRect.X))
        Canvas.SetTop(ImgPhantom, Math.Round(_PhantomRect.Y))
        ImgPhantom.Width = Math.Max(1, Math.Round(_PhantomRect.Width))
        ImgPhantom.Height = Math.Max(1, Math.Round(_PhantomRect.Height))
    End Sub

    ''' <summary>
    ''' 退出必须快（约 0.42s）：文字先走，风景放大淡出。
    ''' 这里**不再把幻影浮回来**：这一层底色是全透明的，风景淡掉之后露出来的本来就是启动器本体；
    ''' 再叠一张 0.96→1 的快照，屏幕上就是「两个启动器同时在放大」，看着像入场动画播了两遍。
    ''' </summary>
    Private Sub PlayOutro()
        Anim(PanUi, UIElement.OpacityProperty, PanUi.Opacity, 0, 120, 0, EaseExit)
        '退出时全景是放大出去的（继续「往里走」的惯性），不是缩回去 —— 反过来会像录像倒放
        Anim(TfWorld, ScaleTransform.ScaleXProperty, TfWorld.ScaleX, 1.06, 320, 0, EaseExit)
        Anim(TfWorld, ScaleTransform.ScaleYProperty, TfWorld.ScaleY, 1.06, 320, 0, EaseExit)
        Anim(PanWorld, UIElement.OpacityProperty, PanWorld.Opacity, 0, 320, 0, EaseExit)
        '幻影收掉：退出阶段它没有存在的意义（真实启动器就在后面）
        PanPhantom.Visibility = Visibility.Collapsed
    End Sub

    ''' <summary>上浮 + 淡入。缩放原点放在左下角，读起来像从基线长出来。</summary>
    Private Sub RiseIn(Element As FrameworkElement, DelayMs As Integer, Ms As Integer, RisePx As Double, StartScale As Double)
        If Element Is Nothing Then Return
        Element.RenderTransformOrigin = New Point(0, 1)
        Dim Scale As New ScaleTransform(StartScale, StartScale)
        Dim Move As New TranslateTransform(0, RisePx)
        Dim Group As New TransformGroup()
        Group.Children.Add(Scale)
        Group.Children.Add(Move)
        Element.RenderTransform = Group
        Element.Opacity = 0
        Anim(Element, UIElement.OpacityProperty, 0, 1, Ms, DelayMs, EaseSettle)
        Anim(Scale, ScaleTransform.ScaleXProperty, StartScale, 1, Ms, DelayMs, EaseSettle)
        Anim(Scale, ScaleTransform.ScaleYProperty, StartScale, 1, Ms, DelayMs, EaseSettle)
        Anim(Move, TranslateTransform.YProperty, RisePx, 0, Ms, DelayMs, EaseSettle)
    End Sub

    ''' <summary>分隔线从左侧长出来。</summary>
    Private Sub GrowRule(Element As FrameworkElement, DelayMs As Integer, Ms As Integer)
        If Element Is Nothing Then Return
        Element.RenderTransformOrigin = New Point(0, 0.5)
        Dim Scale As New ScaleTransform(0, 1)
        Element.RenderTransform = Scale
        Element.Opacity = 0
        Anim(Element, UIElement.OpacityProperty, 0, 1, Ms, DelayMs, EaseSettle)
        Anim(Scale, ScaleTransform.ScaleXProperty, 0, 1, Ms, DelayMs, EaseSettle)
    End Sub

    ''' <summary>
    ''' 把动画值从 From 线性插值到 To，插值曲线由 CSS 风格的 cubic-bezier 控制点给出。
    ''' 只用 opacity 与 transform，不碰 Margin / Width 这些会触发布局的属性。
    ''' </summary>
    Private Shared Function Anim(Obj As IAnimatable, Prop As DependencyProperty,
                                 FromValue As Double, ToValue As Double,
                                 Ms As Integer, DelayMs As Integer, Spline As KeySpline) As DoubleAnimationUsingKeyFrames
        Dim Result As New DoubleAnimationUsingKeyFrames()
        Result.KeyFrames.Add(New DiscreteDoubleKeyFrame(FromValue, KeyTime.FromTimeSpan(TimeSpan.Zero)))
        Dim Last As New SplineDoubleKeyFrame(ToValue, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(Math.Max(1, Ms))))
        Last.KeySpline = Spline
        Result.KeyFrames.Add(Last)
        If DelayMs > 0 Then Result.BeginTime = TimeSpan.FromMilliseconds(DelayMs)
        Result.FillBehavior = FillBehavior.HoldEnd
        Obj.BeginAnimation(Prop, Result)
        Return Result
    End Function

    ''' <summary>重新进入前把上一次留下的动画与属性清干净，否则会停在上一轮结束时的状态。</summary>
    Private Sub ResetVisualState()

        ClearAnim(PanUi, UIElement.OpacityProperty)
        PanUi.Opacity = 1
        ClearAnim(PanWorld, UIElement.OpacityProperty)
        PanWorld.Opacity = 1
        ClearAnim(TfWorld, ScaleTransform.ScaleXProperty)
        ClearAnim(TfWorld, ScaleTransform.ScaleYProperty)
        TfWorld.ScaleX = 1
        TfWorld.ScaleY = 1

        For Each Element As FrameworkElement In {ImgWordmark, LabBrandSub, LabClock, LabDate, RectRule, PanInstance, PanHint}
            ClearAnim(Element, UIElement.OpacityProperty)
            Element.Opacity = 1
        Next
        For Each Element As FrameworkElement In {ImgWordmark, LabBrandSub, LabClock, LabDate, RectRule, PanInstance, PanHint}
            If Element.RenderTransform IsNot Nothing Then
                If TypeOf Element.RenderTransform Is ScaleTransform Then
                    ClearAnim(Element.RenderTransform, ScaleTransform.ScaleXProperty)
                    ClearAnim(Element.RenderTransform, ScaleTransform.ScaleYProperty)
                ElseIf TypeOf Element.RenderTransform Is TranslateTransform Then
                    ClearAnim(Element.RenderTransform, TranslateTransform.YProperty)
                End If
            End If
        Next

        '幻影层每轮都从干净状态开始
        ClearAnim(PanPhantom, UIElement.OpacityProperty)
        PanPhantom.Opacity = 1
        PanPhantom.Visibility = Visibility.Collapsed
        ClearAnim(ImgPhantom, UIElement.OpacityProperty)
        ImgPhantom.Opacity = 1
        ImgPhantom.RenderTransform = New ScaleTransform(1, 1)

    End Sub

    Private Shared Sub ClearAnim(Obj As IAnimatable, Prop As DependencyProperty)
        If Obj Is Nothing Then Return
        Obj.BeginAnimation(Prop, Nothing)
    End Sub

#End Region

#Region "交互"

    Private Sub OnClickExit(sender As Object, e As MouseButtonEventArgs)
        If Not _Active Then Return
        '刚进来的 0.9s 内忽略，否则「进入的那一下」会被同一个点击顺手当成退出，页面一闪就没
        If GetTimeMs() - _EnteredAt < 900 Then Return
        e.Handled = True
        If FrmMain IsNot Nothing Then FrmMain.ExitImmersiveMode()
    End Sub

#End Region

#Region "装饰"

    ''' <summary>5% 颗粒：把干净的风景压成「待机屏」的质感，同时压掉渐变的色带。</summary>
    Private Shared Function MakeGrainBrush() As Brush
        Const Size As Integer = 128
        Dim Stride As Integer = Size * 4
        Dim Pixels(Stride * Size - 1) As Byte
        Dim Rnd As New Random(20261102)
        For i = 0 To Size * Size - 1
            Dim V As Byte = CByte(Rnd.Next(0, 256))
            Pixels(i * 4) = V
            Pixels(i * 4 + 1) = V
            Pixels(i * 4 + 2) = V
            Pixels(i * 4 + 3) = 255
        Next
        Dim Bmp = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, Nothing, Pixels, Stride)
        Bmp.Freeze()
        Dim Fill As New ImageBrush(Bmp) With {
            .TileMode = TileMode.Tile,
            .ViewportUnits = BrushMappingMode.Absolute,
            .Viewport = New Rect(0, 0, Size, Size),
            .Stretch = Stretch.None}
        Fill.Freeze()
        Return Fill
    End Function

    ''' <summary>游戏内的文字描影是硬阴影（无模糊），用零模糊的投影模拟。</summary>
    Private Shared Sub ApplyTextShadow(Element As TextBlock)
        If Element Is Nothing Then Return
        Element.Effect = New DropShadowEffect With {
            .Color = Colors.Black,
            .BlurRadius = 0,
            .ShadowDepth = 2,
            .Direction = 315,
            .Opacity = 0.6,
            .RenderingBias = RenderingBias.Performance}
    End Sub

#End Region

End Class
