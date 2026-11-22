Public Class PageOtherTest

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Hint("该功能已在缘启精简版中移除……")
    End Sub

    ''' <summary>
    ''' 今日人品：和原版一致 —— 以「日期 + 启动器标识」为随机种子，
    ''' 所以同一天无论点多少次都是同一个值，换一天才会变。
    ''' </summary>
    Public Shared Sub Jrrp()
        Try
            Dim Score As Integer = New Random(DailyLuckSeed()).Next(0, 101)
            Dim Rating As String = GetLuckRating(Score)
            Dim Title As String = "今日人品 - " & Now.ToString("yyyy/M/d")
            If Score >= 60 Then
                MyMsgBox($"你今天的人品值是：{Score}！{Rating}", Title)
            Else
                MyMsgBox($"你今天的人品值是：{Score}... {Rating}", Title, IsWarn:=Score <= 30)
            End If
        Catch ex As Exception
            Logger.Error(ex, "抽取今日人品失败")
            Hint("抽取今日人品失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    ''' <summary>今日人品的随机种子：日期 + 本机标识，保证当天固定、换一天才变。</summary>
    Private Shared Function DailyLuckSeed() As Integer
        Dim Tag As String = ""
        Try
            Tag = Settings.Get(Of String)("Identify")
        Catch
        End Try
        If String.IsNullOrEmpty(Tag) Then Tag = "YQL"
        '和原版一致的 djb2 哈希
        Dim Hash As Long = 5381L
        For Each Ch As Char In (Now.ToString("yyyyMMdd") & Tag)
            Hash = (Hash * 33L + AscW(Ch)) And &HFFFFFFFFL
        Next
        Return CInt(Hash And &H7FFFFFFFL)
    End Function

    ''' <summary>按运气值给出评语（分档与文案和原版一致）。</summary>
    Private Shared Function GetLuckRating(Luck As Integer) As String
        Select Case Luck
            Case 100
                Return "100！100！" & vbCrLf & "隐藏主题 欧皇…… 不对，这个版本应该没有这玩意……"
            Case Is >= 95
                Return "差一点就到100了呢..."
            Case Is >= 90
                Return "好评如潮！"
            Case Is >= 60
                Return "还行啦，还行啦"
            Case Is >= 40
                Return "勉强还行吧..."
            Case Is >= 30
                Return "呜..."
            Case Is >= 10
                Return "不会吧！"
            Case Else
                Return "（是百分制哦）"
        End Select
    End Function

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
