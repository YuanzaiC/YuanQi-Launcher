Imports Microsoft.Win32

''' <summary>
''' 绿色便携化。
''' 启动时把历史上散落在注册表和 %AppData% 的数据一次性搬进程序目录（YQL\），
''' 之后启动器只认程序目录，不再往注册表写任何东西。
''' </summary>
Public Module ModPortable

    ''' <summary>注册表里历史设置的键路径（相对 HKEY_CURRENT_USER）。</summary>
    Private Const RegSubPath As String = "Software\" & RegFolder

    Private AlreadyMigrated As Boolean = False
    Private ReadOnly PendingNotes As New List(Of String)

    ''' <summary>
    ''' 执行一次性迁移。必须在任何 Settings 读取之前调用（ModeDebug 就会读设置）。
    ''' </summary>
    Public Sub MigrateToPortable()
        If AlreadyMigrated Then Return
        AlreadyMigrated = True
        Try
            DirectoryUtils.Create(Paths.Base & "YQL\")
        Catch ex As Exception
            Console.Error.WriteLine("创建 YQL 文件夹失败：" & ex.Message)
        End Try
        Try
            PendingNotes.Add(MigrateRegistry())
        Catch ex As Exception
            PendingNotes.Add("注册表迁移失败：" & ex.Message)
        End Try
        Try
            PendingNotes.Add(MigrateAppData())
        Catch ex As Exception
            PendingNotes.Add("AppData 迁移失败：" & ex.Message)
        End Try
        Try
            PendingNotes.Add(MigrateLegacyTemp())
        Catch ex As Exception
            PendingNotes.Add("旧临时目录迁移失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>Logger 就绪后调用，把迁移结果记进日志。</summary>
    Public Sub FlushMigrationLog()
        If PendingNotes.Count = 0 Then Return
        For Each Note As String In PendingNotes
            Logger.Info("便携化迁移：" & Note)
        Next
        PendingNotes.Clear()
    End Sub

    ''' <summary>把 HKCU\Software\YQL 下所有值搬进 YQL\Setup.ini，然后删掉该键。</summary>
    Private Function MigrateRegistry() As String
        Dim Pairs As New List(Of KeyValuePair(Of String, String))
        Using Key As RegistryKey = Registry.CurrentUser.OpenSubKey(RegSubPath, False)
            If Key Is Nothing Then Return "注册表里没有历史设置"
            For Each Name As String In Key.GetValueNames()
                If String.IsNullOrEmpty(Name) Then Continue For
                Dim Raw As Object
                Try
                    Raw = Key.GetValue(Name)
                Catch ex As Exception
                    Continue For
                End Try
                If Raw Is Nothing Then Continue For
                Dim Text As String = CStr(Raw)
                If Text = RegistryUtils.NULL_STRING Then Continue For
                Pairs.Add(New KeyValuePair(Of String, String)(Name, Text))
            Next
        End Using
        If Pairs.Count = 0 Then Return "注册表里没有历史设置"

        '先整体备份一份，万一以后要回滚还有据可查
        Try
            Dim Backup As New Text.StringBuilder
            For Each Pair In Pairs
                Backup.Append(Pair.Key).Append(":").Append(Pair.Value).Append(vbCrLf)
            Next
            FileUtils.Write(Paths.Base & "YQL\RegistryBackup.ini", Backup.ToString())
        Catch ex As Exception
            Logger.Warn(ex, "备份注册表历史设置失败")
        End Try

        Dim Written As Integer = 0
        For Each Pair In Pairs
            If HasIniKey("Setup", Pair.Key) Then Continue For
            WriteIni("Setup", Pair.Key, Pair.Value)
            Written += 1
        Next

        Try
            Registry.CurrentUser.DeleteSubKeyTree(RegSubPath, False)
            Return $"注册表 {Pairs.Count} 项已搬进 YQL\Setup.ini（新写入 {Written} 项），HKCU\{RegSubPath} 已删除"
        Catch ex As Exception
            Return $"注册表 {Pairs.Count} 项已搬进 YQL\Setup.ini（新写入 {Written} 项），但删除旧键失败：{ex.Message}"
        End Try
    End Function

    ''' <summary>把 %AppData%\YQL 里的配置、JIT 热点、离线皮肤、陶瓦数据搬进 YQL\。</summary>
    Private Function MigrateAppData() As String
        Dim Source As String = Paths.AppDataThenName
        If Not DirectoryUtils.Exists(Source) Then Return "AppData 里没有历史数据"
        Dim Target As String = Paths.Base & "YQL\"
        Dim Moved As New List(Of String)

        For Each Name As String In {"config.json", "JitProfileStartup", "CustomSkin.png"}
            Try
                Dim FromPath As String = Source & Name
                Dim ToPath As String = Target & Name
                If FileUtils.Exists(FromPath) AndAlso Not FileUtils.Exists(ToPath) Then
                    FileUtils.Copy(FromPath, ToPath)
                    Moved.Add(Name)
                    '复制成功就删掉原件，机器上不留第二份
                    FileUtils.Delete(FromPath)
                ElseIf FileUtils.Exists(FromPath) AndAlso FileUtils.Exists(ToPath) Then
                    '目标已存在（之前迁移过一半），原件直接清掉
                    FileUtils.Delete(FromPath)
                End If
            Catch ex As Exception
                Logger.Warn(ex, $"迁移 {Name} 失败")
            End Try
        Next

        Try
            Dim FromDir As String = Source & "Terracotta\"
            Dim ToDir As String = Target & "Terracotta\"
            If DirectoryUtils.Exists(FromDir) Then
                DirectoryUtils.Create(ToDir)
                For Each Item As IO.FileInfo In DirectoryUtils.GetInfo(FromDir).GetFiles()
                    Try
                        Dim ToPath As String = ToDir & Item.Name
                        If Not FileUtils.Exists(ToPath) Then
                            Item.CopyTo(ToPath)
                            Moved.Add("Terracotta\" & Item.Name)
                        End If
                        Item.Delete()
                    Catch ex As Exception
                        Logger.Warn(ex, $"迁移陶瓦文件 {Item.Name} 失败")
                    End Try
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "迁移陶瓦联机数据失败")
        End Try

        '旧文件夹全空了就顺手删掉，机器上不留空壳
        Try
            If DirectoryUtils.Exists(Source) AndAlso Not DirectoryUtils.GetInfo(Source).EnumerateFiles("*", IO.SearchOption.AllDirectories).Any() Then
                DirectoryUtils.Delete(Source)
                Moved.Add("已删除旧目录 %AppData%\YQL")
            End If
        Catch ex As Exception
        End Try

        Return If(Moved.Count = 0, "AppData 里没有需要迁移的文件", $"AppData 迁移：{String.Join("、", Moved)}")
    End Function

    ''' <summary>把旧版放在系统临时目录里的持久缓存搬进 YQL\Cache。</summary>
    Private Function MigrateLegacyTemp() As String
        Dim Legacy As String = PathUtils.AddSlashSuffix(IO.Path.GetTempPath() & "YQL\")
        If Legacy = PathTemp Then Return "临时目录已经在程序目录内"
        If Not DirectoryUtils.Exists(Legacy) Then Return "系统临时目录里没有历史缓存"
        Dim Moved As Integer = 0
        For Each Name As String In {"ModHash.ini", "LocalMod.json"}
            Try
                Dim FromPath As String = Legacy & "Cache\" & Name
                Dim ToPath As String = PathCache & Name
                If FileUtils.Exists(FromPath) AndAlso Not FileUtils.Exists(ToPath) Then
                    DirectoryUtils.Create(PathCache)
                    FileUtils.Copy(FromPath, ToPath)
                    Moved += 1
                End If
            Catch ex As Exception
            End Try
        Next
        Return $"旧临时缓存迁移 {Moved} 项（{Legacy} 现在只剩可丢弃的临时文件）"
    End Function

End Module
