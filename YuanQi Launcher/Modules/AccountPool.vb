Imports Newtonsoft.Json.Linq

''' <summary>账户池中的一个账户引用。</summary>
Public Class AccountRef
    Public Type As McLoginType
    Public Name As String
End Class

''' <summary>
''' 全版本通用的账户池：统一聚合各登录方式的已保存账号，支持按版本选定账户并解析登录数据。
''' </summary>
Public Module AccountPool

    Public Const VersionAccountKey As String = "VersionAccount"
    Public Const DefaultAccountKey As String = "DefaultAccount"

    ''' <summary>账户类型显示名。</summary>
    Public Function TypeDisplay(T As McLoginType) As String
        Select Case T
            Case McLoginType.Ms : Return "正版"
            Case McLoginType.Legacy : Return "离线"
            Case McLoginType.Nide : Return "统一通行证"
            Case McLoginType.Auth : Return "第三方登录"
            Case Else : Return "未知"
        End Select
    End Function

    ''' <summary>列出账户池中的所有账号（正版 + 离线 + 统一通行证 + Authlib）。</summary>
    Public Function GetAllAccounts() As List(Of AccountRef)
        Dim Result As New List(Of AccountRef)
        '正版
        Try
            Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
            If MsJson IsNot Nothing Then
                For Each A In MsJson
                    Result.Add(New AccountRef With {.Type = McLoginType.Ms, .Name = A.Key})
                Next
            End If
        Catch
        End Try
        '离线
        If Settings.Get(Of String)("LoginLegacyName") <> "" Then
            For Each N In Settings.Get(Of String)("LoginLegacyName").Split("¨").Where(Function(x) x.Trim <> "")
                Result.Add(New AccountRef With {.Type = McLoginType.Legacy, .Name = N.Trim})
            Next
        End If
        '第三方
        For Each Token In {"Nide", "Auth"}
            If Settings.Get(Of String)("Login" & Token & "Email") <> "" Then
                For Each N In Settings.Get(Of String)("Login" & Token & "Email").Split("¨")
                    If N.Trim <> "" Then
                        Result.Add(New AccountRef With {.Type = If(Token = "Nide", McLoginType.Nide, McLoginType.Auth), .Name = N})
                    End If
                Next
            End If
        Next
        Return Result
    End Function

    ''' <summary>当前版本的选中账户；未单独设置时回退到全局默认账户，都没有则返回 Nothing。</summary>
    Public Function GetVersionAccount() As AccountRef
        Try
            If McInstanceSelected IsNot Nothing Then
                Dim V = Settings.Get(Of String)(VersionAccountKey, Instance:=McInstanceSelected)
                If Not String.IsNullOrWhiteSpace(V) Then
                    Dim Ref = ParseRef(V)
                    If Ref IsNot Nothing Then Return Ref
                End If
            End If
            Return GetDefaultAccount()
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>设置当前版本的选中账户。</summary>
    Public Sub SetVersionAccount(Ref As AccountRef)
        If McInstanceSelected Is Nothing OrElse Ref Is Nothing Then Return
        Settings.Set(VersionAccountKey, Ref.Type.ToString() & "|" & Ref.Name, Instance:=McInstanceSelected)
        Settings.Set("LoginType", Ref.Type) '同步全局登录方式，兼容旧逻辑
    End Sub

    ''' <summary>清除当前版本的选中账户。</summary>
    Public Sub ClearVersionAccount()
        If McInstanceSelected Is Nothing Then Return
        Settings.Set(VersionAccountKey, "", Instance:=McInstanceSelected)
    End Sub

    ''' <summary>全局默认账户：所有未单独指定账户的版本默认使用。</summary>
    Public Function GetDefaultAccount() As AccountRef
        Try
            Dim Raw = Settings.Get(Of String)(DefaultAccountKey)
            If String.IsNullOrWhiteSpace(Raw) Then Return Nothing
            Return ParseRef(Raw)
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>设置全局默认账户（传 Nothing 清除）。</summary>
    Public Sub SetDefaultAccount(Ref As AccountRef)
        If Ref Is Nothing Then
            Settings.Set(DefaultAccountKey, "")
        Else
            Settings.Set(DefaultAccountKey, Ref.Type.ToString() & "|" & Ref.Name)
            Settings.Set("LoginType", Ref.Type)
        End If
    End Sub

    Private Function ParseRef(Raw As String) As AccountRef
        If String.IsNullOrWhiteSpace(Raw) Then Return Nothing
        Dim Parts = Raw.Split("|"c)
        If Parts.Length < 2 Then Return Nothing
        Return New AccountRef With {
            .Type = CType([Enum].Parse(GetType(McLoginType), Parts(0)), McLoginType),
            .Name = Parts(1)}
    End Function

    ''' <summary>按账户解析登录数据（供启动时 McLoginInput 使用）。</summary>
    Public Function ResolveLoginData(Ref As AccountRef) As McLoginData
        If Ref Is Nothing Then Return Nothing
        Select Case Ref.Type
            Case McLoginType.Ms
                Dim Token As String = ""
                Try
                    Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
                    If MsJson IsNot Nothing AndAlso MsJson(Ref.Name) IsNot Nothing Then Token = MsJson(Ref.Name).ToString
                Catch
                End Try
                Return New McLoginMs With {.OAuthRefreshToken = Token, .UserName = Ref.Name}
            Case McLoginType.Legacy
                Return New McLoginLegacy With {.UserName = Ref.Name, .SkinType = Settings.Get(Of Integer)("LaunchSkinType"), .SkinName = Settings.Get(Of String)("LaunchSkinID")}
            Case McLoginType.Nide
                Dim Server = GetNideServerName()
                If Server = "" Then Throw New Exception("尚未配置统一通行证服务器，请先在登录页选择服务器并登录一次")
                Return New McLoginServer(McLoginType.Nide) With {.Token = "Nide", .UserName = GetThirdPartyStoredName("Nide", Ref.Name), .Password = GetThirdPartyPass("Nide", Ref.Name), .Description = "统一通行证", .Type = McLoginType.Nide, .BaseUrl = "https://auth.mc-user.com:233/" & Server & "/authserver"}
            Case McLoginType.Auth
                Dim Server = GetAuthServerUrl()
                If Server = "" Then Throw New Exception("尚未配置 Authlib-Injector 服务器，请先在登录页填写服务器地址并登录一次")
                Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server & "/authserver", .UserName = GetThirdPartyStoredName("Auth", Ref.Name), .Password = GetThirdPartyPass("Auth", Ref.Name), .Description = "Authlib-Injector", .Type = McLoginType.Auth}
            Case Else
                Return Nothing
        End Select
    End Function

    ''' <summary>
    ''' 按账户名取第三方密码。账户名忽略大小写匹配：
    ''' 设置里可能同时存在 Yuanzai_c@outlook.com 与 Yuanzai_C@outlook.com 两种写法，
    ''' 严格比较会取不到密码，导致启动时报「密码不能为空」。
    ''' </summary>
    Private Function GetThirdPartyPass(Token As String, Name As String) As String
        Try
            Dim Names = If(Settings.Get(Of String)("Login" & Token & "Email"), "").Split("¨")
            Dim Passs = If(Settings.Get(Of String)("Login" & Token & "Pass"), "").Split("¨")
            Dim Target As String = If(Name, "").Trim()
            For i = 0 To Math.Min(Names.Length, Passs.Length) - 1
                If String.Equals(Names(i).Trim(), Target, StringComparison.OrdinalIgnoreCase) Then Return Passs(i).Trim
            Next
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>取设置里实际保存的账户名（大小写以此为准），找不到则返回原值。</summary>
    Private Function GetThirdPartyStoredName(Token As String, Name As String) As String
        Try
            For Each RawName In If(Settings.Get(Of String)("Login" & Token & "Email"), "").Split("¨")
                If String.Equals(RawName.Trim(), If(Name, "").Trim(), StringComparison.OrdinalIgnoreCase) AndAlso RawName.Trim() <> "" Then
                    Return RawName.Trim()
                End If
            Next
        Catch
        End Try
        Return Name
    End Function

    ''' <summary>添加离线账户；成功返回空串，失败返回错误原因。</summary>
    Public Function AddOfflineAccount(Name As String) As String
        Name = Name.Trim
        If Name = "" Then Return "玩家名不能为空"
        If Not Name.RegexCheck("^[0-9A-Za-z_]+$") Then Return "玩家名只能包含英文、数字、下划线"
        If Name.Length > 16 Then Return "玩家名至多 16 个字符"
        Dim Names As New List(Of String)
        If Settings.Get(Of String)("LoginLegacyName") <> "" Then Names.AddRange(Settings.Get(Of String)("LoginLegacyName").Split("¨"))
        If Names.Contains(Name) Then Return "该用户名已存在"
        Names.Add(Name)
        Settings.Set("LoginLegacyName", String.Join("¨", Names))
        Return ""
    End Function

    ''' <summary>添加/更新第三方账户（Nide / Auth / LittleSkin 的 ¨ 分隔存储）。</summary>
    Public Sub AddThirdPartyAccount(Token As String, Email As String, Pass As String)
        Dim Names As New List(Of String), Passs As New List(Of String)
        If Settings.Get(Of String)("Login" & Token & "Email") <> "" Then
            Dim RawNames = Settings.Get(Of String)("Login" & Token & "Email").Split("¨")
            Dim RawPasss = Settings.Get(Of String)("Login" & Token & "Pass").Split("¨")
            For i = 0 To RawNames.Length - 1
                If RawNames(i).Trim = "" Then Continue For
                Names.Add(RawNames(i))
                Passs.Add(If(i < RawPasss.Length, RawPasss(i), ""))
            Next
        End If
        Dim Idx = Names.FindIndex(Function(N) String.Equals(N.Trim(), Email.Trim(), StringComparison.OrdinalIgnoreCase))
        If Idx >= 0 Then
            Passs(Idx) = Pass
        Else
            Names.Add(Email) : Passs.Add(Pass)
        End If
        Settings.Set("Login" & Token & "Email", String.Join("¨", Names))
        Settings.Set("Login" & Token & "Pass", String.Join("¨", Passs))
    End Sub

End Module
