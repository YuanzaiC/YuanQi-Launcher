Imports System.Diagnostics
Imports System.Threading.Tasks

''' <summary>安装 / 更新的图形界面（纯代码构建，避免额外的设计器文件）。</summary>
Public Class SetupForm
    Inherits Form

    Private ReadOnly LabTitle As New Label()
    Private ReadOnly LabSub As New Label()
    Private ReadOnly LabPath As New Label()
    Private ReadOnly TxtPath As New TextBox()
    Private ReadOnly BtnBrowse As New Button()
    Private ReadOnly CheckShortcut As New CheckBox()
    Private ReadOnly CheckLaunch As New CheckBox()
    Private ReadOnly Bar As New ProgressBar()
    Private ReadOnly LabStatus As New Label()
    Private ReadOnly BtnInstall As New Button()
    Private ReadOnly BtnCancel As New Button()

    Private TargetPath As String = ""
    Private IsUpdating As Boolean
    Private IsBusy As Boolean

    Public Sub New()
        '窗体
        Text = "缘启 YuanQi Launcher 安装程序"
        FormBorderStyle = FormBorderStyle.FixedSingle
        MaximizeBox = False
        MinimizeBox = False
        StartPosition = FormStartPosition.CenterScreen
        ClientSize = New Size(560, 322)
        Font = New Font("Microsoft YaHei UI", 9.0F)
        BackColor = Color.FromArgb(250, 250, 250)

        '标题
        LabTitle.Text = "缘启 YuanQi Launcher"
        LabTitle.Font = New Font("Microsoft YaHei UI", 16.0F, FontStyle.Bold)
        LabTitle.ForeColor = Color.FromArgb(31, 41, 55)
        LabTitle.AutoSize = True
        LabTitle.Location = New Point(28, 26)
        Controls.Add(LabTitle)

        '副标题（安装 / 更新状态）
        LabSub.AutoSize = False
        LabSub.Size = New Size(504, 40)
        LabSub.Location = New Point(30, 68)
        LabSub.ForeColor = Color.FromArgb(107, 114, 128)
        Controls.Add(LabSub)

        '路径
        LabPath.Text = "安装位置"
        LabPath.AutoSize = True
        LabPath.Location = New Point(30, 126)
        LabPath.ForeColor = Color.FromArgb(55, 65, 81)
        Controls.Add(LabPath)

        TxtPath.Location = New Point(94, 122)
        TxtPath.Size = New Size(368, 25)
        Controls.Add(TxtPath)

        BtnBrowse.Text = "浏览…"
        BtnBrowse.Location = New Point(470, 121)
        BtnBrowse.Size = New Size(64, 27)
        AddHandler BtnBrowse.Click, AddressOf OnBrowseClick
        Controls.Add(BtnBrowse)

        '选项
        CheckShortcut.Text = "创建桌面快捷方式"
        CheckShortcut.Checked = True
        CheckShortcut.AutoSize = True
        CheckShortcut.Location = New Point(94, 160)
        Controls.Add(CheckShortcut)

        CheckLaunch.Text = "安装完成后启动缘启"
        CheckLaunch.Checked = True
        CheckLaunch.AutoSize = True
        CheckLaunch.Location = New Point(94, 186)
        Controls.Add(CheckLaunch)

        '进度
        Bar.Location = New Point(30, 226)
        Bar.Size = New Size(504, 14)
        Bar.Style = ProgressBarStyle.Continuous
        Bar.Visible = False
        Controls.Add(Bar)

        LabStatus.Location = New Point(30, 246)
        LabStatus.Size = New Size(504, 22)
        LabStatus.ForeColor = Color.FromArgb(107, 114, 128)
        LabStatus.Text = ""
        Controls.Add(LabStatus)

        '按钮
        BtnInstall.Text = "安装"
        BtnInstall.Location = New Point(348, 278)
        BtnInstall.Size = New Size(92, 30)
        BtnInstall.BackColor = Color.FromArgb(34, 139, 84)
        BtnInstall.ForeColor = Color.White
        BtnInstall.FlatStyle = FlatStyle.Flat
        BtnInstall.FlatAppearance.BorderSize = 0
        AddHandler BtnInstall.Click, AddressOf OnInstallClick
        Controls.Add(BtnInstall)

        BtnCancel.Text = "取消"
        BtnCancel.Location = New Point(448, 278)
        BtnCancel.Size = New Size(86, 30)
        AddHandler BtnCancel.Click, AddressOf OnCancelClick
        Controls.Add(BtnCancel)

        AcceptButton = BtnInstall
        CancelButton = BtnCancel
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        Try
            TargetPath = If(String.IsNullOrWhiteSpace(Installer.ArgTarget), Installer.DetectInstallPath(), Installer.ArgTarget)
            IsUpdating = Installer.IsInstalled(TargetPath)
            TxtPath.Text = TargetPath
            RefreshMode()
        Catch ex As Exception
            LabStatus.Text = "读取安装信息失败：" & ex.Message
        End Try
    End Sub

    ''' <summary>根据是否已安装刷新文案。更新模式下默认不再创建桌面快捷方式。</summary>
    Private Sub RefreshMode()
        If IsUpdating Then
            LabSub.Text = $"检测到已安装的缘启，将更新到 {Installer.AppVersion}。" & vbCrLf & "程序文件会被替换，YQL（设置与缓存）和 .minecraft 保持不动。"
            BtnInstall.Text = "更新"
            '更新时快捷方式已经存在，默认不勾选
            CheckShortcut.Checked = False
        Else
            LabSub.Text = $"将安装缘启 {Installer.AppVersion}。" & vbCrLf & "建议安装到当前用户目录，避免写入需要管理员权限的位置。"
            BtnInstall.Text = "安装"
            CheckShortcut.Checked = True
        End If
    End Sub


    Private Sub OnBrowseClick(sender As Object, e As EventArgs)
        If IsBusy Then Return
        Using Dialog As New FolderBrowserDialog()
            Dialog.Description = "选择缘启的安装位置"
            Dialog.ShowNewFolderButton = True
            Try
                Dim Current = TxtPath.Text.Trim()
                If Directory.Exists(Current) Then Dialog.SelectedPath = Current
            Catch
            End Try
            If Dialog.ShowDialog(Me) = DialogResult.OK Then
                TxtPath.Text = Dialog.SelectedPath
                TargetPath = Dialog.SelectedPath
                IsUpdating = Installer.IsInstalled(TargetPath)
                RefreshMode()
                LabStatus.ForeColor = Color.FromArgb(107, 114, 128)
            End If
        End Using
    End Sub

    Private Sub OnCancelClick(sender As Object, e As EventArgs)
        If IsBusy Then Return
        Close()
    End Sub

    Private Async Sub OnInstallClick(sender As Object, e As EventArgs)
        If IsBusy Then Return
        Dim Target As String = TxtPath.Text.Trim()
        If Target = "" Then
            MessageBox.Show(Me, "请先选择安装位置。", "缘启 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Try
            Target = Path.GetFullPath(Target)
        Catch
            MessageBox.Show(Me, "安装路径不合法，请重新选择。", "缘启 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End Try

        If Installer.ReadPayload() Is Nothing Then
            MessageBox.Show(Me, "安装包数据不完整，请重新下载后再试。", "缘启 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim NeedShortcut As Boolean = CheckShortcut.Checked
        Dim NeedLaunch As Boolean = CheckLaunch.Checked

        '目标目录需要管理员权限时，直接申请提权，由提权后的静默安装完成
        If Not Installer.CanWriteTo(Target) Then
            If Installer.IsElevated Then
                MessageBox.Show(Me, "当前已是管理员身份，但仍无法写入该位置：" & vbCrLf & Target & vbCrLf & vbCrLf & "请换一个安装目录。",
                                "无法写入", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            SetBusy(True)
            LabStatus.Text = "该位置需要管理员权限，正在申请……"
            Dim ElevatedProcess As Process = Installer.StartElevatedInstall(Target, NeedShortcut)
            If ElevatedProcess Is Nothing Then
                SetBusy(False)
                LabStatus.Text = ""
                MessageBox.Show(Me, "已取消管理员权限申请，安装未完成。" & vbCrLf & vbCrLf &
                                "也可以换一个用户目录安装，例如：" & vbCrLf &
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YuanQi Launcher"),
                                "缘启 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            LabStatus.Text = "正在以管理员身份安装……"
            ElevatedProcess.WaitForExit()
            SetBusy(False)
            If ElevatedProcess.ExitCode = 0 Then
                If NeedLaunch Then Installer.StartApp(Target)
                Close()
            Else
                MessageBox.Show(Me, "以管理员身份安装失败（退出码 " & ElevatedProcess.ExitCode & "）。", "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
            Return
        End If

        SetBusy(True)
        Dim Succeeded As Boolean = False
        Try
            Dim Reporter As IProgress(Of KeyValuePair(Of Double, String)) = New System.Progress(Of KeyValuePair(Of Double, String))(
                Sub(Item)
                    Bar.Value = Math.Min(100, CInt(Math.Round(Item.Key * 100)))
                    LabStatus.Text = Item.Value
                End Sub)
            Await Task.Run(Sub()
                               Installer.Install(Target, NeedShortcut,
                                   Sub(Value, Text) Reporter.Report(New KeyValuePair(Of Double, String)(Value, Text)))
                           End Sub)
            Succeeded = True
            Bar.Value = 100
            LabStatus.Text = "安装完成"
        Catch ex As Exception
            LabStatus.Text = "安装失败"
            MessageBox.Show(Me, "安装失败：" & vbCrLf & Installer.DescribeError(ex, Target), "缘启 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
        SetBusy(False)

        If Succeeded Then
            If NeedLaunch Then Installer.StartApp(Target)
            Close()
        End If
    End Sub

    Private Sub SetBusy(Busy As Boolean)
        IsBusy = Busy
        Bar.Visible = Busy
        BtnInstall.Enabled = Not Busy
        BtnCancel.Enabled = Not Busy
        BtnBrowse.Enabled = Not Busy
        TxtPath.Enabled = Not Busy
        CheckShortcut.Enabled = Not Busy
        CheckLaunch.Enabled = Not Busy
        BtnCancel.Text = If(Busy, "请稍候…", "取消")
        If Busy Then LabStatus.Text = "正在准备安装……"
    End Sub

End Class
