'渲染性能调节模块
'
'主窗体是分层窗口（AllowsTransparency="True"），这个属性会让 WPF 放弃硬件加速、
'对整窗做软件光栅化，于是每帧成本 ≈ 本帧脏区面积 × 内容复杂度：
'后台游戏把 CPU 抢走时，UI 线程的重绘就直接体现为掉帧。
'
'这里不去改窗口结构（那会丢掉背景透明等外观），而是从三个方向把这个乘积压下去：
'  1. 动画期间给被搬运的元素挂位图缓存：帧内只做纹理合成，不再逐帧重绘子树；
'  2. 背景模糊改成一次性预渲染位图（CreateBlurredSource），不再逐帧执行 BlurEffect；
'  3. 帧耗时监控：连续掉帧时把阴影等着色器特效降档，恢复顺畅后原样还原。

Imports System.Windows.Media.Effects

Public Module ModRenderPerf

#Region "档位"

    ''' <summary>自动：动画期缓存生效，掉帧时自动降档（默认）。</summary>
    Public Const ModeAuto As Integer = 0
    ''' <summary>流畅优先：始终按「已掉帧」处理，动画期特效直接降档。</summary>
    Public Const ModeSmooth As Integer = 1
    ''' <summary>画质优先：关闭所有自动降级，保持原有观感。</summary>
    Public Const ModeQuality As Integer = 2

    ''' <summary>
    ''' 当前性能档位。
    ''' </summary>
    Public ReadOnly Property PerfMode As Integer
        Get
            Dim Value As Integer = ModeAuto
            Try
                Value = Settings.Get(Of Integer)("UiPerfMode")
            Catch ex As Exception
                Return ModeAuto
            End Try
            If Value < ModeAuto OrElse Value > ModeQuality Then Return ModeAuto
            Return Value
        End Get
    End Property

    ''' <summary>动画期间是否给被搬运的元素挂位图缓存。</summary>
    Public ReadOnly Property UseAniCache As Boolean
        Get
            Return PerfMode <> ModeQuality
        End Get
    End Property

    ''' <summary>是否允许在掉帧时自动降低特效开销。</summary>
    Public ReadOnly Property AllowEffectFallback As Boolean
        Get
            Return PerfMode <> ModeQuality
        End Get
    End Property

    ''' <summary>当前是否处于降档状态。</summary>
    Public ReadOnly Property InLowPower As Boolean
        Get
            Return IsLowPower
        End Get
    End Property

    ''' <summary>设置变更后重新应用档位。</summary>
    Public Sub ApplySetting(Optional Value As Object = Nothing)
        Try
            If PerfMode = ModeQuality Then
                SetLowPower(False)
            ElseIf PerfMode = ModeSmooth Then
                SetLowPower(True)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "应用渲染性能档位失败")
        End Try
    End Sub

    Private Function OnUiThread() As Boolean
        Try
            If System.Windows.Application.Current Is Nothing Then Return False
            Return System.Windows.Application.Current.CheckAccess()
        Catch ex As Exception
            Return False
        End Try
    End Function

#End Region

#Region "动画期位图缓存"

    ''' <summary>每个元素被多少个动画组同时缓存（同一元素可能被多个动画组同时搬运）。</summary>
    Private ReadOnly AniCacheRefs As New Dictionary(Of UIElement, Integer)

    ''' <summary>
    ''' 该动画是否只是「合成变换」：移动、缩放、旋转、淡入淡出。
    ''' 这类动画期间子内容一帧都不变，缓存成位图后每帧只剩一次纹理合成。
    ''' </summary>
    Public Function IsCacheableAni(ByVal Anim As AniData) As Boolean
        Const MaxPixels As Double = 2.6E+06
        Dim Element As FrameworkElement = TryCast(Anim.Obj, FrameworkElement)
        If Element Is Nothing OrElse TypeOf Element Is Window Then Return False
        Select Case Anim.TypeMain
            Case AniType.Number
                Select Case Anim.TypeSub
                    Case AniTypeSub.Opacity
                        '做透明的对象也可能是 DropShadowEffect，只处理真正的界面元素
                        If Not Anim.Obj Is Element Then Return False
                    Case AniTypeSub.TranslateX, AniTypeSub.TranslateY
                        '纯合成变换，直接可用
                    Case Else
                        Return False
                End Select
            Case AniType.ScaleTransform, AniType.RotateTransform
                '纯变换，直接可用
            Case Else
                Return False
        End Select
        '还没完成布局的元素没有可缓存的内容
        If Element.ActualWidth < 8 OrElse Element.ActualHeight < 8 Then Return False
        '过大的元素缓存收益低于内存与首次光栅化的代价，交给普通绘制
        If Element.ActualWidth * Element.ActualHeight > MaxPixels Then Return False
        '元素自己已经指定了缓存策略就不覆盖
        If Element.CacheMode IsNot Nothing Then Return False
        Return True
    End Function

    ''' <summary>
    ''' 为一个动画组准备位图缓存，必须在动画开始之前调用。
    ''' </summary>
    Public Sub PrepareAniCache(Anis As List(Of AniData), Cached As List(Of UIElement))
        If Cached Is Nothing Then Return
        If Not UseAniCache OrElse Not OnUiThread() Then Return
        Try
            '同一批动画里还有改尺寸/位置的（会触发重新布局）时不要缓存，
            '否则每帧都要重画缓存，比不缓存更慢。
            Dim LayoutTargets As New HashSet(Of Object)
            For Each Item As AniData In Anis
                Select Case Item.TypeMain
                    Case AniType.Number
                        Select Case Item.TypeSub
                            Case AniTypeSub.X, AniTypeSub.Y, AniTypeSub.Width, AniTypeSub.Height
                                LayoutTargets.Add(Item.Obj)
                        End Select
                    Case AniType.Scale
                        LayoutTargets.Add(Item.Obj)
                End Select
            Next
            For Each Item As AniData In Anis
                If LayoutTargets.Contains(Item.Obj) Then Continue For
                If Not IsCacheableAni(Item) Then Continue For
                Dim Element As UIElement = CType(Item.Obj, UIElement)
                If Cached.Contains(Element) Then Continue For
                AttachAniCache(Element)
                Cached.Add(Element)
            Next
            If Cached.Count > 0 Then Logger.Trace($"动画位图缓存：已缓存 {Cached.Count} 个元素（共 {Anis.Count} 个动画）")
        Catch ex As Exception
            Logger.Warn(ex, "准备动画位图缓存失败")
        End Try
    End Sub

    Private Sub AttachAniCache(Element As UIElement)
        If Element Is Nothing OrElse Not OnUiThread() Then Return
        Dim Count As Integer = 0
        AniCacheRefs.TryGetValue(Element, Count)
        If Count > 0 Then
            AniCacheRefs(Element) = Count + 1
            Return
        End If
        'EnableClearType 必须关：位图缓存承载不了子像素抗锯齿
        Dim Cache As New BitmapCache()
        Cache.RenderAtScale = 1
        Cache.EnableClearType = False
        Cache.SnapsToDevicePixels = False
        Element.CacheMode = Cache
        AniCacheRefs(Element) = 1
    End Sub

    ''' <summary>
    ''' 动画组结束时撤销缓存，恢复清晰的矢量绘制与 ClearType 文本。
    ''' </summary>
    Public Sub ReleaseAniCache(Cached As List(Of UIElement))
        If Cached Is Nothing OrElse Cached.Count = 0 Then Return
        If Not OnUiThread() Then Return
        For Each Element As UIElement In Cached
            Try
                Dim Count As Integer = 0
                If Not AniCacheRefs.TryGetValue(Element, Count) Then Continue For
                If Count > 1 Then
                    AniCacheRefs(Element) = Count - 1
                Else
                    AniCacheRefs.Remove(Element)
                    Element.CacheMode = Nothing
                End If
            Catch ex As Exception
                Logger.Warn(ex, "撤销动画位图缓存失败")
            End Try
        Next
        Cached.Clear()
    End Sub

#End Region

#Region "帧耗时监控"

    Private FrameAvg As Double = 0
    Private FrameBadCount As Integer = 0
    Private FrameGoodCount As Integer = 0
    Private IsLowPower As Boolean = False
    Private LowPowerSince As Long = 0
    Private LastSlowLog As Long = 0
    Private SlowFrameCount As Integer = 0

    ''' <summary>
    ''' 动画帧回调每帧调用一次，用真实帧间隔判断是否正在掉帧。
    ''' </summary>
    Public Sub NotifyFrame(DeltaTimeMs As Double)
        If DeltaTimeMs <= 0 Then Return
        If DeltaTimeMs > 500 Then Return '窗口被遮挡 / 最小化后补的那一帧不参与统计
        '滑动平均：掉帧时快速上升，顺畅时缓慢下降，避免档位来回抖动
        Dim Sample As Double = Math.Min(DeltaTimeMs, 200)
        If FrameAvg <= 0 Then
            FrameAvg = Sample
        Else
            FrameAvg = FrameAvg * 0.8 + Sample * 0.2
        End If
        If FrameAvg >= 34 Then '约 30 FPS 以下
            FrameBadCount += 1
            FrameGoodCount = 0
        ElseIf FrameAvg <= 22 Then '约 45 FPS 以上
            FrameGoodCount += 1
            FrameBadCount = 0
        Else
            FrameBadCount = 0
            FrameGoodCount = 0
        End If
        If IsLowPower Then
            '只有「自动」档才允许在恢复顺畅后自动回退；「流畅优先」要一直保持低开销。
            If PerfMode = ModeAuto AndAlso FrameGoodCount >= 45 AndAlso GetTimeMs() - LowPowerSince > 3000 Then SetLowPower(False)
        ElseIf FrameBadCount >= 15 AndAlso AllowEffectFallback Then
            SetLowPower(True)
        End If
    End Sub

    ''' <summary>
    ''' 两个动画帧间隔过长时的日志。掉帧时逐帧写日志会反过来拖慢 UI 线程，这里做节流。
    ''' </summary>
    Public Sub LogSlowFrame(DeltaTick As Double)
        SlowFrameCount += 1
        Dim Tick As Long = GetTimeMs()
        If Tick - LastSlowLog < 3000 Then Return
        LastSlowLog = Tick
        Logger.Info($"两个动画帧间隔 {Math.Round(DeltaTick)} ms（最近 3 秒内 {SlowFrameCount} 次）")
        SlowFrameCount = 0
    End Sub

    ''' <summary>把特效降到低开销档 / 恢复原样。</summary>
    Private Sub SetLowPower(Enable As Boolean)
        If IsLowPower = Enable Then Return
        If Not OnUiThread() Then Return
        Try
            IsLowPower = Enable
            FrameBadCount = 0
            FrameGoodCount = 0
            If Enable Then
                Logger.Info("渲染性能：降低界面特效开销（阴影与模糊降档）")
                LowPowerSince = GetTimeMs()
                ApplyEffectFallback()
            Else
                Logger.Info("渲染性能：帧率已恢复，还原界面特效")
                RestoreEffectFallback()
            End If
        Catch ex As Exception
            Logger.Warn(ex, "切换界面特效档位失败")
        End Try
    End Sub

    ''' <summary>降档时被摘掉的非阴影特效，用于还原。</summary>
    Private ReadOnly DowngradedEffects As New List(Of KeyValuePair(Of UIElement, Effect))
    ''' <summary>降档时被调小半径的阴影，以及它原来的半径。</summary>
    Private ReadOnly DowngradedShadows As New List(Of KeyValuePair(Of DropShadowEffect, Double))

    ''' <summary>
    ''' 新页面是降档之后才建出来的，里面新加的阴影还没有降过。
    ''' 切页后补一次遍历，把新元素也纳入降档范围（只为「流畅优先」与掉帧中的「自动」档服务）。
    ''' </summary>
    Public Sub RefreshLowPower()
        If Not IsLowPower OrElse Not OnUiThread() Then Return
        ApplyEffectFallback()
    End Sub

    Private Sub ApplyEffectFallback()
        If FrmMain Is Nothing Then Return
        Dim Stack As New Stack(Of DependencyObject)
        Stack.Push(FrmMain)
        Do While Stack.Count > 0
            Dim Node As DependencyObject = Stack.Pop()
            If Node Is Nothing Then Continue Do
            Dim Element As UIElement = TryCast(Node, UIElement)
            If Element IsNot Nothing Then
                Dim Fx As Effect = Element.Effect
                If Fx IsNot Nothing Then
                    Dim Shadow As DropShadowEffect = TryCast(Fx, DropShadowEffect)
                    If Shadow IsNot Nothing Then
                        '阴影保留形状，只把采样质量与半径降下来，观感变化最小
                        If Not HasDowngradedShadow(Shadow) AndAlso DowngradedShadows.Count < 500 Then
                            DowngradedShadows.Add(New KeyValuePair(Of DropShadowEffect, Double)(Shadow, Shadow.BlurRadius))
                        End If
                        Shadow.RenderingBias = RenderingBias.Performance
                        If Shadow.BlurRadius > 8 Then Shadow.BlurRadius = 8
                    ElseIf Not HasDowngradedEffect(Element) AndAlso DowngradedEffects.Count < 500 Then
                        '软件渲染下模糊之类全尺寸着色器最贵，直接摘掉
                        DowngradedEffects.Add(New KeyValuePair(Of UIElement, Effect)(Element, Fx))
                        Element.Effect = Nothing
                    End If
                End If
            End If
            Dim NodeVisual As Visual = TryCast(Node, Visual)
            If NodeVisual Is Nothing Then Continue Do
            Dim ChildCount As Integer = 0
            Try
                ChildCount = VisualTreeHelper.GetChildrenCount(NodeVisual)
            Catch ex As Exception
                ChildCount = 0
            End Try
            Dim Index As Integer
            For Index = 0 To ChildCount - 1
                Stack.Push(VisualTreeHelper.GetChild(NodeVisual, Index))
            Next
        Loop
    End Sub

    Private Function HasDowngradedShadow(Target As DropShadowEffect) As Boolean
        For Each Item As KeyValuePair(Of DropShadowEffect, Double) In DowngradedShadows
            If Item.Key Is Target Then Return True
        Next
        Return False
    End Function

    Private Function HasDowngradedEffect(Target As UIElement) As Boolean
        For Each Item As KeyValuePair(Of UIElement, Effect) In DowngradedEffects
            If Item.Key Is Target Then Return True
        Next
        Return False
    End Function

    Private Sub RestoreEffectFallback()
        For Each Item As KeyValuePair(Of DropShadowEffect, Double) In DowngradedShadows
            Try
                Item.Key.BlurRadius = Item.Value
                Item.Key.RenderingBias = RenderingBias.Quality
            Catch ex As Exception
                Logger.Warn(ex, "还原阴影特效失败")
            End Try
        Next
        DowngradedShadows.Clear()
        For Each Item As KeyValuePair(Of UIElement, Effect) In DowngradedEffects
            Try
                If Item.Key.Effect Is Nothing Then Item.Key.Effect = Item.Value
            Catch ex As Exception
                Logger.Warn(ex, "还原界面特效失败")
            End Try
        Next
        DowngradedEffects.Clear()
    End Sub

#End Region

#Region "背景预模糊"

    ''' <summary>
    ''' 把图片一次性模糊成位图：先缩到最长边 1400 像素再模糊，视觉差别极小但耗时下降一个数量级。
    ''' 失败时返回 Nothing，调用方应回退到实时 BlurEffect。
    ''' </summary>
    Public Function CreateBlurredSource(Source As ImageSource, Radius As Double) As ImageSource
        If Source Is Nothing OrElse Radius <= 1 Then Return Source
        Try
            '先按目标尺寸缩图
            Dim Scale As Double = Math.Min(1, 1400 / Math.Max(Source.Width, Source.Height))
            Dim Converted As New FormatConvertedBitmap()
            Converted.BeginInit()
            If Scale < 1 Then
                Converted.Source = New TransformedBitmap(Source, New ScaleTransform(Scale, Scale))
            Else
                Converted.Source = Source
            End If
            Converted.DestinationFormat = PixelFormats.Bgra32
            Converted.EndInit()
            Dim Width As Integer = Converted.PixelWidth
            Dim Height As Integer = Converted.PixelHeight
            If Width < 4 OrElse Height < 4 Then Return Nothing
            Dim Stride As Integer = Width * 4
            Dim Pixels(Stride * Height - 1) As Byte
            Converted.CopyPixels(Pixels, Stride, 0)
            '三次盒式模糊 ≈ 高斯模糊；缩图后半径同比缩小
            Dim R As Integer = Math.Max(1, CInt(Math.Round(Radius * Math.Max(Scale, 0.35) * 0.9)))
            Dim Pass As Integer
            For Pass = 1 To 3
                BoxBlurHorizontal(Pixels, Width, Height, R)
                BoxBlurVertical(Pixels, Width, Height, R)
            Next
            Dim Result As BitmapSource = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, Nothing, Pixels, Stride)
            Result.Freeze()
            Logger.Info($"背景模糊已预渲染：{Width}×{Height}，半径 {Radius}")
            Return Result
        Catch ex As Exception
            Logger.Warn(ex, "预渲染背景模糊失败，回退到实时模糊")
            Return Nothing
        End Try
    End Function

    ''' <summary>横向盒式模糊（边界钳制，不会产生暗边）。</summary>
    Private Sub BoxBlurHorizontal(Pixels As Byte(), Width As Integer, Height As Integer, Radius As Integer)
        Dim WindowSize As Integer = Radius * 2 + 1
        Dim Temp(Width * 4 - 1) As Byte
        Dim Y As Integer, X As Integer, K As Integer
        For Y = 0 To Height - 1
            Dim RowStart As Integer = Y * Width * 4
            Dim SumB As Integer = 0
            Dim SumG As Integer = 0
            Dim SumR As Integer = 0
            Dim SumA As Integer = 0
            For K = -Radius To Radius
                Dim Index As Integer = RowStart + Math.Min(Math.Max(K, 0), Width - 1) * 4
                SumB += Pixels(Index)
                SumG += Pixels(Index + 1)
                SumR += Pixels(Index + 2)
                SumA += Pixels(Index + 3)
            Next
            For X = 0 To Width - 1
                Dim Target As Integer = X * 4
                Temp(Target) = CByte(SumB \ WindowSize)
                Temp(Target + 1) = CByte(SumG \ WindowSize)
                Temp(Target + 2) = CByte(SumR \ WindowSize)
                Temp(Target + 3) = CByte(SumA \ WindowSize)
                Dim OutX As Integer = Math.Min(Math.Max(X - Radius, 0), Width - 1) * 4 + RowStart
                Dim InX As Integer = Math.Min(Math.Max(X + Radius + 1, 0), Width - 1) * 4 + RowStart
                SumB += CInt(Pixels(InX)) - CInt(Pixels(OutX))
                SumG += CInt(Pixels(InX + 1)) - CInt(Pixels(OutX + 1))
                SumR += CInt(Pixels(InX + 2)) - CInt(Pixels(OutX + 2))
                SumA += CInt(Pixels(InX + 3)) - CInt(Pixels(OutX + 3))
            Next
            Array.Copy(Temp, 0, Pixels, RowStart, Width * 4)
        Next
    End Sub

    ''' <summary>纵向盒式模糊（边界钳制，不会产生暗边）。</summary>
    Private Sub BoxBlurVertical(Pixels As Byte(), Width As Integer, Height As Integer, Radius As Integer)
        Dim WindowSize As Integer = Radius * 2 + 1
        Dim Stride As Integer = Width * 4
        Dim Temp(Height * 4 - 1) As Byte
        Dim X As Integer, Y As Integer, K As Integer
        For X = 0 To Width - 1
            Dim ColumnStart As Integer = X * 4
            Dim SumB As Integer = 0
            Dim SumG As Integer = 0
            Dim SumR As Integer = 0
            Dim SumA As Integer = 0
            For K = -Radius To Radius
                Dim Index As Integer = Math.Min(Math.Max(K, 0), Height - 1) * Stride + ColumnStart
                SumB += Pixels(Index)
                SumG += Pixels(Index + 1)
                SumR += Pixels(Index + 2)
                SumA += Pixels(Index + 3)
            Next
            For Y = 0 To Height - 1
                Dim Target As Integer = Y * 4
                Temp(Target) = CByte(SumB \ WindowSize)
                Temp(Target + 1) = CByte(SumG \ WindowSize)
                Temp(Target + 2) = CByte(SumR \ WindowSize)
                Temp(Target + 3) = CByte(SumA \ WindowSize)
                Dim OutY As Integer = Math.Min(Math.Max(Y - Radius, 0), Height - 1) * Stride + ColumnStart
                Dim InY As Integer = Math.Min(Math.Max(Y + Radius + 1, 0), Height - 1) * Stride + ColumnStart
                SumB += CInt(Pixels(InY)) - CInt(Pixels(OutY))
                SumG += CInt(Pixels(InY + 1)) - CInt(Pixels(OutY + 1))
                SumR += CInt(Pixels(InY + 2)) - CInt(Pixels(OutY + 2))
                SumA += CInt(Pixels(InY + 3)) - CInt(Pixels(OutY + 3))
            Next
            '逐行把该列写回
            For Y = 0 To Height - 1
                Dim SourceIndex As Integer = Y * 4
                Dim TargetIndex As Integer = Y * Stride + ColumnStart
                Pixels(TargetIndex) = Temp(SourceIndex)
                Pixels(TargetIndex + 1) = Temp(SourceIndex + 1)
                Pixels(TargetIndex + 2) = Temp(SourceIndex + 2)
                Pixels(TargetIndex + 3) = Temp(SourceIndex + 3)
            Next
        Next
    End Sub

#End Region

End Module
