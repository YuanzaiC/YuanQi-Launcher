Imports System.Collections.Concurrent

''' <summary>
''' 工具箱 · 下载：把任意直链下载到本地（自定义下载）。
''' 交互参考 PCL CE 的「自定义下载」：粘贴地址 → 可选文件名 / 保存目录 → 开始下载。
''' 页面上不显示进度，进度统一由任务管理页（右下角下载按钮）展示。
''' 额外提供「多线程分段下载」：服务器支持 Accept-Ranges 时把文件切成几段并发拉，速度更快。
''' </summary>
Public Class PageOtherDownload

    ''' <summary>下载时使用的 UA（不少直链会拦非浏览器 UA）。</summary>
    Private Const BrowserUA As String =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36"

    ''' <summary>本次下载的完整目标路径（用于完成提示）。</summary>
    Private LastTarget As String = ""

    Private Sub PageOtherDownload_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True
        If String.IsNullOrWhiteSpace(TextFolder.Text) Then TextFolder.Text = DefaultFolder()
        '入场动画：卡片依次淡入
        AniStart(AaStack(PanMain, 130, 45), "PageOtherDownload Enter", True)
    End Sub

    ''' <summary>默认保存目录：启动器目录下的 Downloads。</summary>
    Private Function DefaultFolder() As String
        Try
            Dim Folder As String = Paths.Base & "Downloads\"
            DirectoryUtils.Create(Folder)
            Return Folder
        Catch ex As Exception
            Logger.Warn(ex, "创建下载目录失败")
            Return Paths.Base
        End Try
    End Function

    Private Sub BtnFolder_Click(sender As Object, e As RoutedEventArgs) Handles BtnFolder.Click
        Try
            Dim Folder As String = Dialogs.SelectFolder("选择保存目录", False).FirstOrDefault
            If Not String.IsNullOrEmpty(Folder) Then TextFolder.Text = Folder
        Catch ex As Exception
            Logger.Warn(ex, "选择保存目录失败")
        End Try
    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As RoutedEventArgs) Handles BtnOpenFolder.Click
        Try
            Dim Folder As String = If(String.IsNullOrWhiteSpace(TextFolder.Text), DefaultFolder(), TextFolder.Text.Trim)
            DirectoryUtils.Create(Folder)
            OpenExplorer(Folder)
        Catch ex As Exception
            Logger.Warn(ex, "打开下载目录失败")
            Hint("打开目录失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnStart_Click(sender As Object, e As RoutedEventArgs) Handles BtnStart.Click
        Try
            Dim Url As String = If(TextUrl.Text, "").Trim()
            If Not (Url.StartsWithF("http://") OrElse Url.StartsWithF("https://")) Then
                Hint("请填写以 http:// 或 https:// 开头的直链地址", HintType.Red)
                TextUrl.Focus()
                Return
            End If

            Dim Folder As String = If(TextFolder.Text, "").Trim()
            If Folder = "" Then Folder = DefaultFolder()
            DirectoryUtils.Create(Folder)

            Dim FileName As String = If(TextFileName.Text, "").Trim()
            If FileName = "" Then FileName = GuessFileName(Url)
            FileName = FixFileName(FileName)
            Dim Target As String = PathUtils.AddSlashSuffix(Folder) & FileName

            If FileUtils.Exists(Target) Then
                If MyMsgBox($"这个文件已经存在：{vbCrLf}{Target}{vbCrLf}{vbCrLf}要覆盖它吗？",
                            "文件已存在", "覆盖", "取消", IsWarn:=True) <> 1 Then Return
                FileUtils.Delete(Target)
            End If

            Dim Segments As Integer = 1
            Try
                If ComboMode.SelectedItem IsNot Nothing Then Segments = Math.Max(1, Val(ComboMode.SelectedItem.Tag))
            Catch
            End Try

            LastTarget = Target
            If Segments > 1 Then
                StartSegmentedDownload(Url, Target, FileName, Segments)
            Else
                StartSimpleDownload(Url, Target, FileName)
            End If
        Catch ex As Exception
            Logger.Error(ex, "开始下载失败")
            Hint("开始下载失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    '===============================
    '  单线程：走启动器自带的下载器（能复用镜像、重试、限速等逻辑）
    '===============================
    Private Sub StartSimpleDownload(Url As String, Target As String, FileName As String)
        Dim Loader As New LoaderCombo(Of Integer)("下载文件：" & FileName, {
            New LoaderDownload("下载文件", {New NetFile({Url}, Target, Nothing, True)}) With {.ProgressWeight = 1}
        })
        LoaderTaskbarAdd(Loader)
        AddHandler Loader.OnStateChangedUi, AddressOf SimpleDownloadStateChanged
        Loader.Start(1)
        FrmMain.BtnExtraDownload.ShowRefresh()
    End Sub

    Private Sub SimpleDownloadStateChanged(L As LoaderBase, NewState As LoadState, OldState As LoadState)
        Try
            Select Case NewState
                Case LoadState.Finished
                    Hint($"下载完成：{IO.Path.GetFileName(LastTarget)}", HintType.Green)
                Case LoadState.Failed
                    Hint("下载失败：" & L.Error.GetDisplay(False), HintType.Red)
                Case LoadState.Canceled
                    Hint("已取消下载", HintType.Blue)
            End Select
        Catch ex As Exception
            Logger.Warn(ex, "处理下载状态失败")
        End Try
    End Sub

    '===============================
    '  多线程：探测 Accept-Ranges → 切成 N 段并发下载
    '===============================
    Private Sub StartSegmentedDownload(Url As String, Target As String, FileName As String, Segments As Integer)
        Dim Task As New LoaderTask(Of Integer, Boolean)("多线程下载：" & FileName,
            Sub(T)
                Dim Total As Long = ProbeRangeSupport(Url)
                If Total <= 0 Then
                    '服务器不支持分段，就老实走单线程 HTTP 流
                    DownloadSingleStream(Url, Target, T)
                    Return
                End If
                DownloadSegmented(Url, Target, Total, Segments, T)
                T.Output = True
            End Sub) With {.ProgressWeight = 1}
        Dim Combo As New LoaderCombo(Of Integer)("下载文件：" & FileName, {Task})
        LoaderTaskbarAdd(Combo)
        AddHandler Combo.OnStateChangedUi, AddressOf SegmentedStateChanged
        Combo.Start(0)
        FrmMain.BtnExtraDownload.ShowRefresh()
    End Sub

    Private Sub SegmentedStateChanged(L As LoaderBase, NewState As LoadState, OldState As LoadState)
        Try
            Select Case NewState
                Case LoadState.Finished
                    Hint($"下载完成：{IO.Path.GetFileName(LastTarget)}", HintType.Green)
                Case LoadState.Failed
                    Hint("下载失败：" & L.Error.GetDisplay(False), HintType.Red)
                Case LoadState.Canceled
                    Hint("已取消下载", HintType.Blue)
            End Select
        Catch ex As Exception
            Logger.Warn(ex, "处理多线程下载状态失败")
        End Try
    End Sub

    ''' <summary>发一个 Range: bytes=0-0 的请求，判断服务器是否支持分段，返回文件总大小（不支持返回 -1）。</summary>
    Private Function ProbeRangeSupport(Url As String) As Long
        Try
            Dim Req As Net.HttpWebRequest = CType(Net.WebRequest.Create(Url), Net.HttpWebRequest)
            Req.UserAgent = BrowserUA
            Req.Timeout = 15000
            Req.ReadWriteTimeout = 15000
            Req.AddRange(0, 0)
            Using Resp As Net.HttpWebResponse = CType(Req.GetResponse(), Net.HttpWebResponse)
                If Resp.StatusCode <> Net.HttpStatusCode.PartialContent Then Return -1
                Dim ContentRange As String = Resp.Headers("Content-Range")
                If String.IsNullOrEmpty(ContentRange) Then Return -1
                Dim TotalText As String = ContentRange.AfterLast("/")
                Dim Total As Long
                If Long.TryParse(TotalText, Total) AndAlso Total > 0 Then Return Total
                Return -1
            End Using
        Catch ex As Exception
            Logger.Info($"分段探测失败，将退回单线程：{ex.Message}")
            Return -1
        End Try
    End Function

    ''' <summary>把文件切成 Segments 段并发下载到同一个文件。</summary>
    Private Sub DownloadSegmented(Url As String, Target As String, Total As Long, Segments As Integer,
                                  Task As LoaderTask(Of Integer, Boolean))
        '先占位：分段写入需要文件已存在且长度固定
        Using Fs As New IO.FileStream(Target, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.ReadWrite)
            Fs.SetLength(Total)
        End Using

        Dim Token As Threading.CancellationToken = Task.CreateCancellationToken()
        Dim Done As Long = 0
        Dim DoneLock As New Object
        Dim Errors As New ConcurrentBag(Of Exception)
        Dim Chunk As Long = Math.Max(1, Total \ Segments)

        '只把总进度喂给任务管理页，页面本身不再画进度条
        Dim Report As Action(Of Long) =
            Sub(Delta As Long)
                SyncLock DoneLock
                    Done += Delta
                    Task.Progress = Math.Min(0.995, Done / CDbl(Total))
                End SyncLock
            End Sub

        Dim Threads As New List(Of Threading.Thread)
        For i As Integer = 0 To Segments - 1
            Dim From As Long = i * Chunk
            Dim [To] As Long = If(i = Segments - 1, Total - 1, (i + 1) * Chunk - 1)
            If From > Total - 1 Then Continue For
            Dim Worker As New Threading.Thread(
                Sub()
                    DownloadRange(Url, Target, From, [To], Report, Token, Errors)
                End Sub)
            Worker.IsBackground = True
            Threads.Add(Worker)
            Worker.Start()
        Next
        For Each Worker In Threads
            Worker.Join()
        Next

        If Token.IsCancellationRequested Then
            FileUtils.Delete(Target)
            Throw New OperationCanceledException("已取消下载")
        End If
        If Errors.Any() Then
            FileUtils.Delete(Target)
            Throw New Exception($"{Errors.Count} 个分段下载失败：{Errors.First.Message}")
        End If
        Dim Actual As Long = 0
        Try
            Actual = New IO.FileInfo(Target).Length
        Catch
        End Try
        If Actual <> Total Then
            FileUtils.Delete(Target)
            Throw New Exception($"分段结果不完整（{Actual} / {Total}）")
        End If
        Task.Progress = 1
        Logger.Info($"多线程下载完成：{Target}（{Segments} 段，{ModArchive.FormatSize(Total)}）")
    End Sub

    ''' <summary>下载一个字节区间并写进目标文件的对应位置。</summary>
    Private Sub DownloadRange(Url As String, Target As String, From As Long, [To] As Long,
                              Report As Action(Of Long), Token As Threading.CancellationToken,
                              Errors As ConcurrentBag(Of Exception))
        Try
            Dim Req As Net.HttpWebRequest = CType(Net.WebRequest.Create(Url), Net.HttpWebRequest)
            Req.UserAgent = BrowserUA
            Req.Timeout = 30000
            Req.ReadWriteTimeout = 30000
            Req.AddRange(From, [To])
            Using Resp As Net.HttpWebResponse = CType(Req.GetResponse(), Net.HttpWebResponse)
                Using S As IO.Stream = Resp.GetResponseStream()
                    Using Fs As New IO.FileStream(Target, IO.FileMode.Open, IO.FileAccess.Write, IO.FileShare.ReadWrite)
                        Fs.Seek(From, IO.SeekOrigin.Begin)
                        Dim Buf(16383) As Byte
                        Dim Pos As Long = From
                        Do While Pos <= [To]
                            If Token.IsCancellationRequested Then Throw New OperationCanceledException("已取消下载")
                            Dim Want As Integer = CInt(Math.Min(Buf.Length, [To] - Pos + 1))
                            Dim ReadCount As Integer = S.Read(Buf, 0, Want)
                            If ReadCount <= 0 Then Exit Do
                            Fs.Write(Buf, 0, ReadCount)
                            Pos += ReadCount
                            Report(ReadCount)
                        Loop
                    End Using
                End Using
            End Using
        Catch ex As Exception
            If Not (TypeOf ex Is OperationCanceledException) Then Errors.Add(ex)
        End Try
    End Sub

    ''' <summary>服务器不支持分段时的兜底：单连接顺序下载。</summary>
    Private Sub DownloadSingleStream(Url As String, Target As String, Task As LoaderTask(Of Integer, Boolean))
        Dim Token As Threading.CancellationToken = Task.CreateCancellationToken()
        Dim Req As Net.HttpWebRequest = CType(Net.WebRequest.Create(Url), Net.HttpWebRequest)
        Req.UserAgent = BrowserUA
        Req.Timeout = 30000
        Req.ReadWriteTimeout = 30000
        Using Resp As Net.HttpWebResponse = CType(Req.GetResponse(), Net.HttpWebResponse)
            Dim Total As Long = Resp.ContentLength
            Dim Done As Long = 0
            Using S As IO.Stream = Resp.GetResponseStream()
                Using Fs As New IO.FileStream(Target, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.ReadWrite)
                    Dim Buf(16383) As Byte
                    Do
                        If Token.IsCancellationRequested Then
                            Fs.Close()
                            FileUtils.Delete(Target)
                            Throw New OperationCanceledException("已取消下载")
                        End If
                        Dim ReadCount As Integer = S.Read(Buf, 0, Buf.Length)
                        If ReadCount <= 0 Then Exit Do
                        Fs.Write(Buf, 0, ReadCount)
                        Done += ReadCount
                        If Total > 0 Then Task.Progress = Math.Min(0.995, Done / CDbl(Total))
                    Loop
                End Using
            End Using
        End Using
        Task.Progress = 1
    End Sub

    '---- 文件名处理 ----

    ''' <summary>从地址里猜一个文件名；猜不出来就用时间戳。</summary>
    Private Function GuessFileName(Url As String) As String
        Try
            Dim PathPart As String = Url.BeforeFirst("?").BeforeFirst("#")
            Dim Name As String = IO.Path.GetFileName(PathPart.Replace("/"c, "\"c))
            If Not String.IsNullOrWhiteSpace(Name) Then Return Uri.UnescapeDataString(Name)
        Catch ex As Exception
            Logger.Warn(ex, "从下载地址推断文件名失败")
        End Try
        Return $"download_{Now:yyyyMMdd_HHmmss}.bin"
    End Function

    ''' <summary>去掉文件名里 Windows 不允许的字符。</summary>
    Private Function FixFileName(Name As String) As String
        Dim Result As String = Name
        For Each Ch As Char In IO.Path.GetInvalidFileNameChars()
            Result = Result.Replace(Ch, "_"c)
        Next
        If String.IsNullOrWhiteSpace(Result) Then Result = "download.bin"
        Return Result
    End Function

End Class
