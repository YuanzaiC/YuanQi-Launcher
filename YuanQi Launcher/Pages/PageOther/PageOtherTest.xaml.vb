Public Class PageOtherTest

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Hint("该功能已在缘启精简版中移除……")
    End Sub

    ''' <summary>
    ''' 今日人品：随机生成今日运气指数。
    ''' </summary>
    Public Shared Sub Jrrp()
        Static Rnd As New Random
        Dim Score As Integer = Rnd.Next(0, 101)
        Dim Comment As String
        Select Case Score
            Case Is >= 90
                Comment = "大吉！今天做什么都会很顺利！"
            Case Is >= 70
                Comment = "吉，运气不错！"
            Case Is >= 50
                Comment = "中吉，普普通通的一天。"
            Case Is >= 30
                Comment = "小凶，出门记得带伞。"
            Case Is >= 10
                Comment = "凶，今天还是少干点大事。"
            Case Else
                Comment = "大凶！建议窝在家里打 Minecraft。"
        End Select
        MyMsgBox($"你今天的运气指数是 {Score} 分！{vbCrLf}{vbCrLf}{Comment}", "今日人品")
    End Sub

    ''' <summary>
    ''' 清理垃圾：清除任务临时文件与各类可再生的缓存。
    ''' </summary>
    Public Shared Sub RubbishClear()
        Try
            Dim Count As Integer = 0
            Dim Size As Long = 0
            Dim Targets As String() = {
                PathTemp & "TaskTemp",
                PathTemp & "Download",
                PathTemp & "Cache\Http",
                PathTemp & "Cache\Custom.xaml",
                PathTemp & "Cache\LocalMod.json"
            }
            For Each Target In Targets
                Try
                    If FileUtils.Exists(Target) Then
                        Try
                            Size += New IO.FileInfo(Target).Length
                        Catch
                        End Try
                        Count += 1
                        FileUtils.Delete(Target)
                    ElseIf DirectoryUtils.Exists(Target) Then
                        For Each File In DirectoryUtils.EnumerateFiles(Target, True)
                            Try
                                Size += New IO.FileInfo(File).Length
                                Count += 1
                            Catch
                            End Try
                        Next
                        DirectoryUtils.Delete(Target)
                    End If
                Catch
                End Try
            Next
            Hint($"清理垃圾完成，共删除 {Count} 个文件，释放 {Size / 1024 / 1024:F1} MB 空间！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "清理垃圾失败")
            Hint("清理垃圾失败！", HintType.Red)
        End Try
    End Sub

    ''' <summary>
    ''' 内存优化：强制回收托管内存。
    ''' </summary>
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        MemoryOptimizeInternal(ShowHint)
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        Try
            Dim Before As Long = GC.GetTotalMemory(False)
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            Dim After As Long = GC.GetTotalMemory(False)
            Dim Freed As Double = Math.Max(0, Before - After) / 1024 / 1024
            If ShowHint Then Hint($"内存优化完成，已释放 {Freed:F1} MB 内存！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "内存优化失败")
            If ShowHint Then Hint("内存优化失败！", HintType.Red)
        End Try
    End Sub

    Public Shared Function GetRandomCave() As String
        Return "洞窟里的旅人：把启动器目录加入杀毒软件白名单，可以避免很多莫名其妙的问题。"
    End Function
    Public Shared Function GetRandomHint() As String
        Static Rnd As New Random
        Select Case Rnd.Next(0, 8)
            Case 0
                Return "把启动器目录加入杀毒软件白名单，可以避免很多莫名其妙的问题。"
            Case 1
                Return "正版账号与离线账号可以并存，登录时选择合适的账号类型即可。"
            Case 2
                Return "整合包安装前建议先在版本设置中修改内存大小，以获得更好体验。"
            Case 3
                Return "如果下载速度过慢，可以在设置 → 启动器 → 下载 中更换下载源。"
            Case 4
                Return "Mod 加载器需要与对应 MC 版本匹配，安装前请先确认版本。"
            Case 5
                Return "游戏崩溃与启动器崩溃是两回事，遇到问题先查看日志定位原因。"
            Case 6
                Return "自定义主页可以自由排版，打造属于你自己的启动器首页。"
            Case Else
                Return "缘启 (YuanQi Launcher) 基于 PCL 开源版本二次创作，感谢原作者龙腾猫跃。"
        End Select
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return GetRandomHint()
    End Function

End Class
