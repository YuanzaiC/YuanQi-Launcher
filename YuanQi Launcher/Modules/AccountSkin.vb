Imports Newtonsoft.Json.Linq

''' <summary>
''' 账户头像记录：按「账户名」分别保存每个账户自己的角色与头像文件。
''' 
''' 以前只保存了「当前登录账户」的皮肤，账户列表里其余账户只能借用同一张皮肤，
''' 于是同一个网站下的两个账户会显示成一模一样的头像。
''' 这里为每个账户单独存一份（皮肤文件、角色名、角色 UUID），
''' 没有记录的账户会在后台静默登录一次补齐，之后一直用缓存，不用先进游戏。
''' </summary>
Public Module AccountSkinStore

    ''' <summary>是否已经检查过账户头像（每个会话只检查一次）。</summary>
    Private AccountSkinEnsured As Boolean = False

    Private Function EntryFile(Kind As String, Type As McLoginType) As String
        Return PathCache & "Accounts\" & Kind & Type.ToString() & ".ini"
    End Function

    ''' <summary>取某账户的头像文件路径；没有记录返回空串。</summary>
    Public Function AccountSkinGetSkin(Type As McLoginType, Name As String) As String
        '旧记录可能指向已被清掉的共享皮肤缓存，统一走一次兜底解析
        Return ResolveSkinPath(Type, Name, ReadValue(Type, Name, "Skin"))
    End Function

    ''' <summary>取某账户的角色名；没有记录返回空串。</summary>
    Public Function AccountSkinGetRole(Type As McLoginType, Name As String) As String
        Return ReadValue(Type, Name, "Role")
    End Function

    ''' <summary>取某账户的角色 UUID；没有记录返回空串。</summary>
    Public Function AccountSkinGetUuid(Type As McLoginType, Name As String) As String
        Return ReadValue(Type, Name, "Uuid")
    End Function

    ''' <summary>旧记录可能指向已被清掉的共享皮肤缓存，这里兜一层：文件没了就试账户自己的副本。</summary>
    Private Function ResolveSkinPath(Type As McLoginType, Name As String, Path As String) As String
        If String.IsNullOrWhiteSpace(Path) Then Return Path
        If IO.File.Exists(Path) Then Return Path
        Try
            Dim Key As String = If(Name, "").Trim()
            If Key = "" Then Return Path
            Dim Folder As String = PathCache & "Accounts\Avatar\"
            Dim Target As String = Folder & Type.ToString() & "_" & Key.GetStableHashCode() & IO.Path.GetExtension(Path)
            If IO.File.Exists(Target) Then Return Target
        Catch ex As Exception
        End Try
        Return Path
    End Function

    Private Function ReadValue(Type As McLoginType, Name As String, Kind As String) As String
        Try
            Dim Key As String = If(Name, "").Trim()
            If Key = "" Then Return ""
            Return ReadIni(EntryFile(Kind, Type), Key)
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' 把头像复制成账户自己的独立副本，返回副本路径。
    ''' 以前只记共享皮肤缓存里的路径：手动刷新皮肤、或切到别的账户后那边文件被清掉，
    ''' 这个账户就找不到自己的头像，显示成默认头像。存副本后与共享缓存彻底解耦。
    ''' </summary>
    Private Function EnsureAvatarCopy(Type As McLoginType, Name As String, SkinPath As String) As String
        Try
            If String.IsNullOrWhiteSpace(SkinPath) Then Return SkinPath
            If Not IO.File.Exists(SkinPath) Then Return SkinPath
            Dim Folder As String = PathCache & "Accounts\Avatar\"
            If Not IO.Directory.Exists(Folder) Then IO.Directory.CreateDirectory(Folder)
            Dim Target As String = Folder & Type.ToString() & "_" & Name.GetStableHashCode() & IO.Path.GetExtension(SkinPath)
            '已经就是自己那份副本就不用再复制
            If String.Equals(SkinPath, Target, StringComparison.OrdinalIgnoreCase) Then Return Target
            IO.File.Copy(SkinPath, Target, True)
            Return Target
        Catch ex As Exception
            Logger.Warn(ex, "复制账户头像失败")
            Return SkinPath
        End Try
    End Function

    ''' <summary>记录某个账户的角色与头像。</summary>
    Public Sub AccountSkinSave(Type As McLoginType, Name As String, Role As String, Uuid As String, SkinPath As String)
        Try
            Dim Key As String = If(Name, "").Trim()
            If Key = "" Then Return
            If Not String.IsNullOrWhiteSpace(SkinPath) Then
                SkinPath = EnsureAvatarCopy(Type, Key, SkinPath)
                WriteIni(EntryFile("Skin", Type), Key, SkinPath)
                WriteIni(EntryFile("Time", Type), Key, GetTimeMs().ToString()) '记录更新时间，过期的会自动重新获取
            End If
            If Not String.IsNullOrWhiteSpace(Role) Then WriteIni(EntryFile("Role", Type), Key, Role)
            If Not String.IsNullOrWhiteSpace(Uuid) Then WriteIni(EntryFile("Uuid", Type), Key, Uuid)
        Catch ex As Exception
            Logger.Warn(ex, "保存账户头像失败")
        End Try
    End Sub

    ''' <summary>把当前登录会话刚拿到的皮肤，记到「正在登录的那个账户」名下。</summary>
    Public Sub AccountSkinSaveFromSession(Type As McLoginType, SkinPath As String)
        Try
            If String.IsNullOrWhiteSpace(SkinPath) Then Return
            Select Case Type
                Case McLoginType.Auth
                    AccountSkinSave(Type, Settings.Get(Of String)("CacheAuthUsername"), Settings.Get(Of String)("CacheAuthName"),
                         Settings.Get(Of String)("CacheAuthUuid"), SkinPath)
                Case McLoginType.Nide
                    AccountSkinSave(Type, Settings.Get(Of String)("CacheNideUsername"), Settings.Get(Of String)("CacheNideName"),
                         Settings.Get(Of String)("CacheNideUuid"), SkinPath)
                Case McLoginType.Ms
                    AccountSkinSave(Type, Settings.Get(Of String)("CacheMsV2Name"), Settings.Get(Of String)("CacheMsV2Name"),
                         Settings.Get(Of String)("CacheMsV2Uuid"), SkinPath)
            End Select
        Catch ex As Exception
            Logger.Warn(ex, "记录当前账户头像失败")
        End Try
    End Sub

    ''' <summary>
    ''' 为所有还没有头像记录（或记录已过期）的第三方账户补齐一次（后台静默登录）。
    ''' 全程只做一次：补齐后刷新界面，刷新界面又会重建账户列表，不设标记会无限循环。
    ''' </summary>
    Public Sub AccountSkinEnsureAll()
        If AccountSkinEnsured Then Return
        AccountSkinEnsured = True
        RunInNewThread(
        Sub()
            Dim Changed As Boolean = False
            Try
                For Each Ref As AccountRef In AccountPool.GetAllAccounts()
                    If Ref.Type <> McLoginType.Nide AndAlso Ref.Type <> McLoginType.Auth Then Continue For
                    If Not NeedProbe(Ref) Then Continue For
                    If ProbeAccount(Ref) Then Changed = True
                Next
            Catch ex As Exception
                Logger.Warn(ex, "补齐账户头像失败")
            End Try
            If Not Changed Then Return
            RunInUi(Sub()
                        If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.RefreshAccountUi()
                    End Sub)
        End Sub, "Account Skin Probe")
    End Sub

    ''' <summary>头像记录缺失、文件丢了或超过一天没更新时才需要重新获取。</summary>
    Private Function NeedProbe(Ref As AccountRef) As Boolean
        Try
            Dim SkinPath As String = AccountSkinGetSkin(Ref.Type, Ref.Name)
            If String.IsNullOrWhiteSpace(SkinPath) OrElse Not IO.File.Exists(SkinPath) Then Return True
            Dim Ticks As Long = 0
            Long.TryParse(ReadIni(EntryFile("Time", Ref.Type), Ref.Name), Ticks)
            If Ticks <= 0 Then Return True
            Return GetTimeMs() - Ticks > 24 * 60 * 60 * 1000L
        Catch
            Return True
        End Try
    End Function

    ''' <summary>
    ''' 静默登录某个第三方账户，取回它自己的角色与皮肤（不改动当前登录会话的缓存）。
    ''' 返回是否写入了新的记录。
    ''' </summary>
    Private Function ProbeAccount(Ref As AccountRef) As Boolean
        Try
            Dim Token As String = Ref.Type.ToString()
            Dim BaseUrl As String
            If Ref.Type = McLoginType.Auth Then
                BaseUrl = GetAuthServerUrl()
            Else
                Dim Server As String = GetNideServerName()
                If Server = "" Then Return False
                BaseUrl = "https://auth.mc-user.com:233/" & Server
            End If
            If BaseUrl = "" Then Return False
            Dim Pass As String = AccountPool.GetThirdPartyPass(Token, Ref.Name)
            If Pass = "" Then Return False
            '只用于换取角色信息，返回的令牌直接丢弃
            Dim Request As New JObject From {
                {"username", Ref.Name},
                {"password", Pass},
                {"clientToken", Guid.NewGuid().ToString("N")},
                {"agent", New JObject From {{"name", "Minecraft"}, {"version", 1}}},
                {"requestUser", False}
            }
            Dim Response As String = NetRequestByClientRetry(BaseUrl & "/authserver/authenticate", HttpMethod.Post,
                Content:=Request.ToString(Newtonsoft.Json.Formatting.None),
                ContentType:="application/json; charset=utf-8", RequireJson:=True)
            Dim Result As JObject = Response.DeserializeJson()
            If Result Is Nothing Then Return False
            Dim Profile As JToken = Result("selectedProfile")
            If Profile Is Nothing AndAlso Result("availableProfiles") IsNot Nothing AndAlso Result("availableProfiles").Count > 0 Then
                Profile = Result("availableProfiles")(0)
            End If
            If Profile Is Nothing Then Return False
            Dim Uuid As String = Profile("id").ToString
            Dim Role As String = Profile("name").ToString
            Dim SkinPath As String = ""
            Try
                '清掉这个角色上次记下的皮肤地址，保证玩家在网站上换了皮肤后这里也能更新
                DeleteIniKey(PathCache & "Skin\Index" & Token & ".ini", Uuid)
                SkinPath = McSkinDownload(McSkinGetAddress(Uuid, Token))
            Catch ex As Exception
                Logger.Warn(ex, $"获取账户皮肤失败（{Ref.Name}）")
            End Try
            AccountSkinSave(Ref.Type, Ref.Name, Role, Uuid, SkinPath)
            If String.IsNullOrWhiteSpace(SkinPath) Then
                '没拿到皮肤（例如玩家没设置自定义皮肤）也算检查过，免得每次都重新登录
                WriteIni(EntryFile("Time", Ref.Type), Ref.Name.Trim(), GetTimeMs().ToString())
            End If
            Logger.Info($"账户头像：已记录 {Ref.Name}（角色 {Role}）")
            Return True
        Catch ex As Exception
            Logger.Warn(ex, $"读取账户信息失败（{Ref.Name}）")
            Return False
        End Try
    End Function

End Module
