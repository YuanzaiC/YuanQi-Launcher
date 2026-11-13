Imports Newtonsoft.Json.Linq

''' <summary>
''' 游玩记录：每次游戏进程退出后写入一条记录（跨天自动拆分），供主页「日历 / 游玩洞察」使用。
''' 记录同时保存版本标识（<see cref="InstanceIdentityModule"/>），因此版本改名后记录会跟着改名，
''' 版本被删除后记录不再出现在「最近游玩」里。
''' </summary>
Public Module PlayLogModule

    Private ReadOnly PlayLogLock As New Object
    Private PlayLogCache As JArray = Nothing
    ''' <summary>记录版本号，UI 据此判断是否需要重新构建。</summary>
    Public PlayLogVersion As Integer = 0

    Private ReadOnly Property PlayLogPath As String
        Get
            Return Paths.Base & "YQL\PlayLog.json"
        End Get
    End Property

    Public Class PlaySession
        ''' <summary>版本标识（老记录可能为空）。</summary>
        Public Id As String
        ''' <summary>记录时的版本名，仅作为显示兜底。</summary>
        Public Name As String
        Public StartTime As Date
        Public EndTime As Date
        Public ReadOnly Property DurationSeconds As Long
            Get
                Return CType((EndTime - StartTime).TotalSeconds, Long)
            End Get
        End Property
    End Class

    ''' <summary>读取全部游玩记录（按开始时间升序）。</summary>
    Public Function LoadSessions() As List(Of PlaySession)
        SyncLock PlayLogLock
            Return Parse(LoadRaw())
        End SyncLock
    End Function

    ''' <summary>追加一次游玩记录（自动跨天拆分，仅保留最近 120 天）。</summary>
    Public Sub AddSession(Name As String, StartTime As Date, EndTime As Date, Optional Id As String = "")
        If String.IsNullOrWhiteSpace(Name) OrElse EndTime <= StartTime Then Return
        SyncLock PlayLogLock
            Try
                Dim Array = LoadRaw()
                Dim Cur = StartTime
                Do While Cur < EndTime
                    Dim DayEnd = Cur.Date.AddDays(1)
                    Dim SegEnd = If(EndTime < DayEnd, EndTime, DayEnd)
                    Dim Entry As New JObject From {
                        {"n", Name},
                        {"s", Cur.Ticks},
                        {"e", SegEnd.Ticks}}
                    If Not String.IsNullOrWhiteSpace(Id) Then Entry("i") = Id
                    Array.Add(Entry)
                    Cur = SegEnd
                Loop
                SaveLocked(Array)
            Catch ex As Exception
                Logger.Warn(ex, "写入游玩记录失败")
            End Try
        End SyncLock
    End Sub

    ''' <summary>
    ''' 把历史记录里「只有版本名、没有标识」的条目补上标识（按名字匹配到现存版本后调用）。
    ''' 这样老数据也能跟着版本改名、删除。
    ''' </summary>
    Public Sub BackfillInstanceId(Id As String, Name As String)
        If String.IsNullOrWhiteSpace(Id) OrElse String.IsNullOrWhiteSpace(Name) Then Return
        SyncLock PlayLogLock
            Try
                Dim Array = LoadRaw()
                Dim Changed As Boolean = False
                For Each Item In Array
                    Try
                        If Item("i") IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(Item("i").ToString) Then Continue For
                        If Item("n") Is Nothing Then Continue For
                        If Not String.Equals(Item("n").ToString.Trim(), Name.Trim(), StringComparison.OrdinalIgnoreCase) Then Continue For
                        Item("i") = Id
                        Changed = True
                    Catch
                    End Try
                Next
                If Changed Then SaveLocked(Array)
            Catch ex As Exception
                Logger.Warn(ex, "补齐游玩记录的版本标识失败")
            End Try
        End SyncLock
    End Sub

    ''' <summary>记录对应的实例；版本已被删除时返回 Nothing。</summary>
    Public Function GetSessionInstance(S As PlaySession) As McInstance
        Try
            If S Is Nothing Then Return Nothing
            If Not String.IsNullOrWhiteSpace(S.Id) Then
                Dim ById = InstanceIdentityModule.FindInstanceById(S.Id)
                If ById IsNot Nothing Then Return ById
            End If
            If String.IsNullOrWhiteSpace(S.Name) Then Return Nothing
            '老记录没有标识：按名字找到版本后补上标识，之后它就跟着版本走了
            Dim ByName = FindInstanceByName(S.Name)
            If ByName IsNot Nothing AndAlso String.IsNullOrWhiteSpace(S.Id) Then
                Dim NewId As String = InstanceIdentityModule.GetInstanceId(ByName.PathVersion)
                If Not String.IsNullOrWhiteSpace(NewId) Then
                    S.Id = NewId
                    BackfillInstanceId(NewId, S.Name)
                End If
            End If
            Return ByName
        Catch ex As Exception
            Logger.Warn(ex, "解析游玩记录对应的版本失败")
            Return Nothing
        End Try
    End Function

    ''' <summary>记录的显示名：优先用版本当前的名字（改名后自动更新），版本删掉了才用记录里的名字。</summary>
    Public Function GetSessionDisplayName(S As PlaySession) As String
        Try
            If S Is Nothing Then Return "未知版本"
            Dim Instance = GetSessionInstance(S)
            If Instance IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(Instance.Name) Then Return Instance.Name.Trim()
            Return If(String.IsNullOrWhiteSpace(S.Name), "未知版本", S.Name.Trim())
        Catch ex As Exception
            Logger.Warn(ex, "获取游玩记录显示名失败")
            Return If(String.IsNullOrWhiteSpace(S?.Name), "未知版本", S.Name.Trim())
        End Try
    End Function

    ''' <summary>记录的身份键：有标识用标识，没有就用名字（老记录）。</summary>
    Public Function GetSessionKey(S As PlaySession) As String
        If S Is Nothing Then Return ""
        If Not String.IsNullOrWhiteSpace(S.Id) Then Return "i:" & S.Id
        Return "n:" & If(S.Name, "").Trim()
    End Function

    Private Function FindInstanceByName(Name As String) As McInstance
        Try
            If String.IsNullOrWhiteSpace(Name) OrElse McInstanceList Is Nothing Then Return Nothing
            Dim Key As String = Name.Trim()
            For Each Group In McInstanceList.Values
                For Each Instance In Group
                    If Instance IsNot Nothing AndAlso String.Equals(Instance.Name.Trim(), Key, StringComparison.OrdinalIgnoreCase) Then Return Instance
                Next
            Next
        Catch ex As Exception
            Logger.Warn(ex, "按名字查找版本失败")
        End Try
        Return Nothing
    End Function

    ''' <summary>保存：裁剪到最近 120 天并写盘（调用方需持有 <see cref="PlayLogLock"/>）。</summary>
    Private Sub SaveLocked(Array As JArray)
        Dim Keep = Date.Now.AddDays(-120).Ticks
        Dim Trimmed As New JArray
        For Each Item In Array
            Try
                If CLng(Item("e")) >= Keep Then Trimmed.Add(Item)
            Catch
            End Try
        Next
        PlayLogCache = Trimmed
        PlayLogVersion += 1
        IO.Directory.CreateDirectory(Paths.Base & "YQL")
        IO.File.WriteAllText(PlayLogPath, Trimmed.ToString(Newtonsoft.Json.Formatting.None))
    End Sub

    Private Function LoadRaw() As JArray
        If PlayLogCache IsNot Nothing Then Return PlayLogCache
        Try
            If FileUtils.Exists(PlayLogPath) Then
                PlayLogCache = TryCast(IO.File.ReadAllText(PlayLogPath).DeserializeJson(), JArray)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取游玩记录失败")
        End Try
        If PlayLogCache Is Nothing Then PlayLogCache = New JArray
        Return PlayLogCache
    End Function

    Private Function Parse(Array As JArray) As List(Of PlaySession)
        Dim Result As New List(Of PlaySession)
        For Each Item In Array
            Try
                Result.Add(New PlaySession With {
                    .Id = If(Item("i") Is Nothing, "", Item("i").ToString),
                    .Name = Item("n").ToString,
                    .StartTime = New Date(CLng(Item("s"))),
                    .EndTime = New Date(CLng(Item("e")))})
            Catch
            End Try
        Next
        Result.Sort(Function(a, b) a.StartTime.CompareTo(b.StartTime))
        Return Result
    End Function

End Module
