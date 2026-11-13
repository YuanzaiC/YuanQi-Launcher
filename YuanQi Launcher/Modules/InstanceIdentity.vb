Imports System.Collections.Concurrent

''' <summary>
''' 版本身份标识。
''' 在每个版本目录的 <c>YQL\InstanceId</c> 里保存一个 GUID，用于把「游玩记录」等数据
''' 绑定到版本本身，而不是绑定到版本名：
''' 版本改名时整个文件夹一起移动，标识文件跟着走；版本被删除时标识随之消失。
''' </summary>
Public Module InstanceIdentityModule

    ''' <summary>版本目录 → 标识（避免每次都读文件）。</summary>
    Private ReadOnly IdCache As New ConcurrentDictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>取版本标识；没有就按需生成并写入版本目录。</summary>
    Public Function GetInstanceId(PathVersion As String, Optional CreateIfMissing As Boolean = True) As String
        Try
            If String.IsNullOrWhiteSpace(PathVersion) Then Return ""
            Dim Path As String = PathUtils.AddSlashSuffix(PathVersion)
            Dim Cached As String = ""
            If IdCache.TryGetValue(Path, Cached) AndAlso Not String.IsNullOrWhiteSpace(Cached) Then Return Cached
            Dim IdPath As String = Path & "YQL\InstanceId"
            Dim Id As String = ""
            Try
                If FileUtils.Exists(IdPath) Then Id = FileUtils.ReadAsString(IdPath).Trim()
            Catch ex As Exception
                Logger.Warn(ex, "读取版本标识失败")
            End Try
            If String.IsNullOrWhiteSpace(Id) Then
                If Not CreateIfMissing Then Return ""
                Id = Guid.NewGuid().ToString("N")
                Try
                    DirectoryUtils.Create(Path & "YQL")
                    FileUtils.Write(IdPath, Id)
                Catch ex As Exception
                    Logger.Warn(ex, "写入版本标识失败")
                    Return ""
                End Try
            End If
            IdCache(Path) = Id
            Return Id
        Catch ex As Exception
            Logger.Warn(ex, "获取版本标识失败")
            Return ""
        End Try
    End Function

    ''' <summary>按标识找实例；版本被删除后返回 Nothing。</summary>
    Public Function FindInstanceById(Id As String) As McInstance
        Try
            If String.IsNullOrWhiteSpace(Id) OrElse McInstanceList Is Nothing Then Return Nothing
            For Each Group In McInstanceList.Values
                For Each Instance In Group
                    If Instance Is Nothing Then Continue For
                    If String.Equals(GetInstanceId(Instance.PathVersion, CreateIfMissing:=False), Id, StringComparison.OrdinalIgnoreCase) Then Return Instance
                Next
            Next
        Catch ex As Exception
            Logger.Warn(ex, "按标识查找版本失败")
        End Try
        Return Nothing
    End Function

    ''' <summary>实例列表是否已经扫描完成（没完成时不能因为「查不到」就判定版本被删）。</summary>
    Public Function IsInstanceListReady() As Boolean
        Try
            If McInstanceList Is Nothing Then Return False
            If McInstanceListLoader IsNot Nothing AndAlso McInstanceListLoader.State = LoadState.Loading Then Return False
            Return McInstanceList.Values.Any(Function(G) G IsNot Nothing AndAlso G.Count > 0)
        Catch
            Return False
        End Try
    End Function

End Module
