Public Class PageLoginLegacy

    ''' <summary>
    ''' 刷新页面显示的所有信息（离线用户名已在主页左侧维护）。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.ReloadLegacyName(KeepInput)
    End Sub

    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginData
        Dim UserName As String = ""
        If FrmLaunchLeft IsNot Nothing AndAlso FrmLaunchLeft.ComboName IsNot Nothing Then
            UserName = FrmLaunchLeft.ComboName.Text.Replace("¨", "").Trim
        End If
        Return New McLoginLegacy With {.UserName = UserName, .SkinType = Settings.Get(Of Integer)("LaunchSkinType"), .SkinName = Settings.Get(Of String)("LaunchSkinID")}
    End Function
    ''' <summary>
    ''' 当前页面的登录信息是否有效。
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginLegacy) As String
        If LoginData.UserName.Trim = "" Then Return "玩家名不能为空！"
        If LoginData.UserName.Contains("""") Then Return "玩家名不能包含英文引号！"
        If McInstanceSelected IsNot Nothing AndAlso
           McInstanceSelected.Version.Valid AndAlso McInstanceSelected.Version.Vanilla >= New Version(20, 0, 3) AndAlso
           LoginData.UserName.Trim.Length > 16 Then
            Return "自 1.20.3 起，玩家名至多只能包含 16 个字符！"
        End If
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

End Class
