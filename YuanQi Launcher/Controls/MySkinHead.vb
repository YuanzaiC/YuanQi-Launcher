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
    ''' </summary>
    Public Sub SetSkin(Address As String)
        Try
            If String.IsNullOrWhiteSpace(Address) Then Address = PathImage & "Skins/Steve.png"
            If Not Address.StartsWithF(PathImage) AndAlso Not FileUtils.Exists(Address) Then Address = PathImage & "Skins/Steve.png"
            Dim Bmp As New MyBitmap(Address)
            Dim Scale As Integer = Math.Max(1, CInt(Math.Floor(Bmp.Pic.Width / 64.0)))
            ImgFace.Source = Bmp.Clip(8 * Scale, 8 * Scale, 8 * Scale, 8 * Scale)
            If Bmp.Pic.Width >= 64 * Scale AndAlso Bmp.Pic.Height >= 64 * Scale Then
                ImgHat.Source = Bmp.Clip(40 * Scale, 8 * Scale, 8 * Scale, 8 * Scale)
            Else
                ImgHat.Source = Nothing
            End If
        Catch ex As Exception
            Logger.Warn(ex, "加载皮肤头像失败")
            Try
                ImgHat.Source = Nothing
                Dim DefaultBmp As New MyBitmap(PathImage & "Skins/Steve.png")
                ImgFace.Source = DefaultBmp.Clip(8, 8, 8, 8)
            Catch
            End Try
        End Try
    End Sub

End Class
