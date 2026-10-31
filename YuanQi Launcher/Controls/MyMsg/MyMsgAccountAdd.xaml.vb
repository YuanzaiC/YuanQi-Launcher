Public Class MyMsgAccountAdd

    Private ReadOnly MyConverter As MyMsgBoxConverter
    Private ReadOnly Uuid As Integer = GetUuid()
    Private AccountType As McLoginType

    Public Sub New(Converter As MyMsgBoxConverter)
        Try
            InitializeComponent()
            Btn1.Name = Btn1.Name & GetUuid()
            Btn2.Name = Btn2.Name & GetUuid()
            MyConverter = Converter
            AccountType = Converter.AccountType
            LabTitle.Text = Converter.Title
            Btn1.Text = Converter.Button1
            Btn2.Text = Converter.Button2
            ShapeLine.StrokeThickness = GetWPFSize(1)
            '按类型显示对应表单
            PanOffline.Visibility = If(AccountType = McLoginType.Legacy, Visibility.Visible, Visibility.Collapsed)
            PanNide.Visibility = If(AccountType = McLoginType.Nide, Visibility.Visible, Visibility.Collapsed)
            PanAuth.Visibility = If(AccountType = McLoginType.Auth, Visibility.Visible, Visibility.Collapsed)
            PanLittleSkin.Visibility = Visibility.Collapsed '已并入 Authlib（预设服务器）
            If AccountType = McLoginType.Auth AndAlso Not String.IsNullOrEmpty(MyConverter.DefaultServer) Then
                TextAuthServer.Text = MyConverter.DefaultServer
            End If
        Catch ex As Exception
            Logger.Error(ex, "添加账户弹窗初始化失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub Load(sender As Object, e As EventArgs) Handles MyBase.Loaded
        Try
            Opacity = 0
            AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(90, 0, 0, 0) - FrmMain.PanMsg.Background, 200), "PanMsg Background")
            AniStart({
                AaOpacity(Me, 1, 120, 60),
                AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 300, 60, New AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 300, 60, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyMsgBox " & Uuid)
        Catch ex As Exception
            Logger.Error(ex, "添加账户弹窗加载失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub Close()
        MyConverter.WaitFrame.Continue = False
        Interop.ComponentDispatcher.PopModal()
        AniStart({
            AaCode(
            Sub()
                If Not WaitingMyMsgBox.Any() Then
                    AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsg.Background, 200, Ease:=New AniEaseOutFluent(AniEasePower.Weak)))
                End If
            End Sub, 30),
            AaOpacity(Me, -Opacity, 80, 20),
            AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 150, 0, New AniEaseOutFluent),
            AaDouble(Sub(i) TransformRotate.Angle += i, 6 - TransformRotate.Angle, 150, 0, New AniEaseInFluent(AniEasePower.Weak)),
            AaCode(Sub() CType(Parent, Grid).Children.Remove(Me), , True)
        }, "MyMsgBox " & Uuid)
    End Sub

    Public Sub Btn1_Click() Handles Btn1.Click '添加
        If MyConverter.IsExited Then Return
        Try
            Dim Ref As AccountRef = BuildAccount()
            If Ref Is Nothing Then Return
            MyConverter.IsExited = True
            MyConverter.Result = Ref
            Close()
        Catch ex As Exception
            Logger.Error(ex, "添加账户失败")
            Hint(ex.Message, HintType.Red)
        End Try
    End Sub

    Public Sub Btn2_Click() Handles Btn2.Click '取消
        If MyConverter.IsExited Then Return
        MyConverter.IsExited = True
        MyConverter.Result = Nothing
        Close()
    End Sub

    Private Function BuildAccount() As AccountRef
        Select Case AccountType
            Case McLoginType.Legacy
                Dim Name = TextOfflineName.Text.Trim
                Dim Err = AccountPool.AddOfflineAccount(Name)
                If Err <> "" Then Hint(Err, HintType.Red) : Return Nothing
                Return New AccountRef With {.Type = McLoginType.Legacy, .Name = Name}
            Case McLoginType.Nide
                Dim Server = TextNideServer.Text.Trim
                Dim Email = TextNideEmail.Text.Trim.Replace("¨", "")
                Dim Pass = TextNidePass.Password.Trim.Replace("¨", "")
                If Server = "" Then Hint("请输入服务器 ID", HintType.Red) : Return Nothing
                If Email = "" OrElse Pass = "" Then Hint("请输入账号和密码", HintType.Red) : Return Nothing
                If McInstanceSelected IsNot Nothing Then Settings.Set("VersionServerNide", Server, Instance:=McInstanceSelected)
                Settings.Set("CacheNideServer", Server)
                AccountPool.AddThirdPartyAccount("Nide", Email, Pass)
                Return New AccountRef With {.Type = McLoginType.Nide, .Name = Email}
            Case McLoginType.Auth
                Dim Server = TextAuthServer.Text.Trim
                Dim Email = TextAuthEmail.Text.Trim.Replace("¨", "")
                Dim Pass = TextAuthPass.Password.Trim.Replace("¨", "")
                If Server = "" Then Hint("请输入认证服务器地址", HintType.Red) : Return Nothing
                If Email = "" OrElse Pass = "" Then Hint("请输入账号和密码", HintType.Red) : Return Nothing
                If McInstanceSelected IsNot Nothing Then Settings.Set("VersionServerAuthServer", Server, Instance:=McInstanceSelected)
                Settings.Set("CacheAuthServerServer", Server)
                AccountPool.AddThirdPartyAccount("Auth", Email, Pass)
                Return New AccountRef With {.Type = McLoginType.Auth, .Name = Email}
            Case Else
                Return Nothing
        End Select
    End Function

End Class
