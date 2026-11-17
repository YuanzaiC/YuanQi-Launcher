''' <summary>
''' 皮肤头像：裁切皮肤图片中的脸部（含头部第二层）显示为圆角方块。
''' 支持 pack:// 内置皮肤与本地皮肤文件，自动识别 64×64 / 128×128 等倍数尺寸。
''' </summary>
Public Class MySkinHead
    Inherits Grid

    Private ReadOnly ImgFace As New Image With {.Stretch = Stretch.Fill, .SnapsToDevicePixels = True, .IsHitTestVisible = False}
    Private ReadOnly ImgHat As New Image With {.Stretch = Stretch.Fill, .SnapsToDevicePixels = True, .IsHitTestVisible = False}

    Private _CornerRadius As Double = 8
    ''' <summary>
    ''' 圆角半径。设为宽度的一半即为圆形头像。
    ''' </summary>
    Public Property CornerRadius As Double
        Get
            Return _CornerRadius
        End Get
        Set(value As Double)
            _CornerRadius = value
            UpdateClip()
        End Set
    End Property

    Public Sub New()
        Width = 44
        Height = 44
        ClipToBounds = True
        RenderOptions.SetBitmapScalingMode(ImgFace, BitmapScalingMode.NearestNeighbor)
        RenderOptions.SetBitmapScalingMode(ImgHat, BitmapScalingMode.NearestNeighbor)
        Children.Add(ImgFace)
        Children.Add(ImgHat)
        SetSkin(Nothing)
    End Sub

    Private Sub UpdateClip() Handles Me.SizeChanged
        If ActualWidth <= 0 OrElse ActualHeight <= 0 Then Return
        Clip = New RectangleGeometry(New Rect(0, 0, ActualWidth, ActualHeight), _CornerRadius, _CornerRadius)
    End Sub

    ''' <summary>
    ''' 设置要显示的皮肤（pack:// 地址或本地文件路径），无效时回退到 Steve。
    ''' 文件暂时读不到或解码失败时会隔一小会儿重试一次：账户头像文件可能正被
    ''' 后台线程替换，读到写了一半的图就会解码失败，那不该直接变成默认头像。
    ''' </summary>
    Public Sub SetSkin(Address As String)
        SetSkinInternal(Address, 0)
    End Sub

    Private Sub SetSkinInternal(Address As String, RetryLevel As Integer)
        Dim Original As String = Address
        Try
            If String.IsNullOrWhiteSpace(Address) Then Address = PathImage & "Skins/Steve.png"
            If Not Address.StartsWithF(PathImage) AndAlso Not FileUtils.Exists(Address) Then
                If RetryLevel = 0 AndAlso Not String.IsNullOrWhiteSpace(Original) Then
                    ScheduleRetry(Original)
                    Return
                End If
                Logger.Warn($"皮肤头像文件不存在，改用默认头像：{Address}")
                Address = PathImage & "Skins/Steve.png"
            End If
            Dim Bmp As New MyBitmap(Address)
            Dim Scale As Integer = Math.Max(1, CInt(Math.Floor(Bmp.Pic.Width / 64.0)))
            ImgFace.Source = Bmp.Clip(8 * Scale, 8 * Scale, 8 * Scale, 8 * Scale)
            If Bmp.Pic.Width >= 64 * Scale AndAlso Bmp.Pic.Height >= 64 * Scale Then
                ImgHat.Source = Bmp.Clip(40 * Scale, 8 * Scale, 8 * Scale, 8 * Scale)
            Else
                ImgHat.Source = Nothing
            End If
        Catch ex As Exception
            If RetryLevel = 0 AndAlso Not String.IsNullOrWhiteSpace(Original) Then
                ScheduleRetry(Original)
                Return
            End If
            Logger.Warn(ex, $"加载皮肤头像失败：{Original}")
            Try
                ImgHat.Source = Nothing
                Dim DefaultBmp As New MyBitmap(PathImage & "Skins/Steve.png")
                Dim DefaultScale As Integer = Math.Max(1, CInt(Math.Floor(DefaultBmp.Pic.Width / 64.0)))
                ImgFace.Source = DefaultBmp.Clip(8 * DefaultScale, 8 * DefaultScale, 8 * DefaultScale, 8 * DefaultScale)
            Catch
            End Try
        End Try
    End Sub

    ''' <summary>隔 160 ms 再试一次加载，给对方线程把文件写完的时间。</summary>
    Private Sub ScheduleRetry(Address As String)
        Try
            Dim Timer As New Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(160)}
            AddHandler Timer.Tick,
                Sub(Sender As Object, E As EventArgs)
                    Timer.Stop()
                    SetSkinInternal(Address, 1)
                End Sub
            Timer.Start()
        Catch ex As Exception
        End Try
    End Sub

End Class
