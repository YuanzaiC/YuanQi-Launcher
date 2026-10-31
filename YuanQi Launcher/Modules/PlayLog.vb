Imports Newtonsoft.Json.Linq

''' <summary>
''' 游玩记录：每次游戏进程退出后写入一条记录（跨天自动拆分），供主页「日历 / 游玩洞察」使用。
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
    Public Sub AddSession(Name As String, StartTime As Date, EndTime As Date)
        If String.IsNullOrWhiteSpace(Name) OrElse EndTime <= StartTime Then Return
        SyncLock PlayLogLock
            Try
                Dim Array = LoadRaw()
                Dim Cur = StartTime
                Do While Cur < EndTime
                    Dim DayEnd = Cur.Date.AddDays(1)
                    Dim SegEnd = If(EndTime < DayEnd, EndTime, DayEnd)
                    Array.Add(New JObject From {
                        {"n", Name},
                        {"s", Cur.Ticks},
                        {"e", SegEnd.Ticks}})
                    Cur = SegEnd
                Loop
                '裁剪到最近 120 天
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
            Catch ex As Exception
                Logger.Warn(ex, "写入游玩记录失败")
            End Try
        End SyncLock
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
