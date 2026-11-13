Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports Microsoft.Win32

''' <summary>
''' 安装 / 更新的核心逻辑。安装包是「安装程序 + 内嵌压缩包」的单文件：
''' 末尾依次是 [zip 数据][8 字节长度][8 字节标记]，安装时从自身文件里把 zip 取出来。
''' </summary>
Public Module Installer

    Public Const AppExeName As String = "YuanQi Launcher.exe"
    Private Const Marker As String = "YQSETUP1"
    Private Const RegAppKey As String = "Software\YuanQiLauncher"
    Private Const RegUninstallKey As String = "Software\Microsoft\Windows\CurrentVersion\Uninstall\YuanQiLauncher"
    ' 属于「程序」的根目录文件与子目录；其余（YQL、.minecraft）都是用户数据，更新时绝不触碰
    Private ReadOnly ProgramFileExts As String() = {".exe", ".dll", ".config", ".pdb", ".xml"}
    Private ReadOnly ProgramDirs As String() = {"Terracotta", "Videos", "Resources"}

    ' 命令行状态
    Public Silent As Boolean
    Public UninstallMode As Boolean
    Public RemoveData As Boolean
    Public NoLaunch As Boolean
    Public NoShortcut As Boolean
    Public ArgTarget As String

    ''' <summary>安装包内嵌的启动器版本。</summary>
    Public ReadOnly Property AppVersion As String
        Get
            Return BuildInfo.AppVersion
        End Get
    End Property

    ''' <summary>
    ''' 运行过程记录：只写到调试输出，不在磁盘上生成任何日志文件
    ''' （安装包放在桌面 / 下载目录时不该多出一个 log 文件）。
    ''' </summary>
    Public Sub Trace(Text As String)
        Try
            Diagnostics.Debug.WriteLine("[YuanQiSetup] " & Text)
        Catch
        End Try
    End Sub

    Public Sub ParseArgs(Args As String())
        If Args Is Nothing Then Return
        For Each Raw In Args
            If String.IsNullOrWhiteSpace(Raw) Then Continue For
            Dim Arg = Raw.Trim()
            Dim Lower = Arg.ToLowerInvariant()
            If Lower = "/s" OrElse Lower = "-s" OrElse Lower = "/silent" Then
                Silent = True
            ElseIf Lower = "/uninstall" OrElse Lower = "-uninstall" Then
                UninstallMode = True
            ElseIf Lower = "/removedata" Then
                RemoveData = True
            ElseIf Lower = "/nolaunch" Then
                NoLaunch = True
            ElseIf Lower = "/noshortcut" Then
                NoShortcut = True
            ElseIf Lower.StartsWith("/d=") Then
                ArgTarget = Arg.Substring(3).Trim(""""c)
            ElseIf Not Arg.StartsWith("/") AndAlso Not Arg.StartsWith("-") AndAlso Directory.Exists(Arg) Then
                ArgTarget = Arg
            End If
        Next
    End Sub

#Region "内嵌数据"

    ''' <summary>从自身文件尾部读出内嵌的 zip 数据；没有则返回 Nothing。</summary>
    Public Function ReadPayload() As Byte()
        Try
            Dim SelfPath As String = Assembly.GetEntryAssembly().Location
            Dim All As Byte() = File.ReadAllBytes(SelfPath)
            Dim Mark As Byte() = Encoding.ASCII.GetBytes(Marker)
            If All.Length < Mark.Length + 16 Then Return Nothing
            For i = 0 To Mark.Length - 1
                If All(All.Length - Mark.Length + i) <> Mark(i) Then Return Nothing
            Next
            Dim Length As Long = BitConverter.ToInt64(All, All.Length - Mark.Length - 8)
            If Length <= 0 OrElse Length > All.Length - Mark.Length - 8 Then Return Nothing
            Dim Data(CInt(Length) - 1) As Byte
            Array.Copy(All, All.Length - Mark.Length - 8 - Length, Data, 0, Length)
            Return Data
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>把内嵌 zip 解压到临时目录，返回解压后的根目录。</summary>
    Private Function ExtractPayload(Payload As Byte(), BaseDir As String) As String
        Dim Root As String = If(String.IsNullOrEmpty(BaseDir), Path.GetTempPath(), BaseDir)
        Dim TempRoot As String = Path.Combine(Root, "_yqsetup_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(TempRoot)
        Dim ZipPath As String = Path.Combine(TempRoot, "payload.zip")
        File.WriteAllBytes(ZipPath, Payload)
        Dim FilesDir As String = Path.Combine(TempRoot, "files")
        ZipFile.ExtractToDirectory(ZipPath, FilesDir)
        Try
            File.Delete(ZipPath)
        Catch
        End Try
        Return FilesDir
    End Function

#End Region

#Region "安装路径与环境"

    ''' <summary>自动判断是「更新」还是「全新安装」，并给出目标目录。</summary>
    Public Function DetectInstallPath() As String
        '1. 安装包就放在已有的启动器目录里（用户在旧版本文件夹里双击安装包）
        Try
            Dim SelfDir As String = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
            If File.Exists(Path.Combine(SelfDir, AppExeName)) Then Return SelfDir
        Catch
        End Try
        '2. 注册表记录的安装位置
        Try
            Using Key = Registry.CurrentUser.OpenSubKey(RegAppKey)
                If Key IsNot Nothing Then
                    Dim Installed = TryCast(Key.GetValue("InstallPath"), String)
                    If Not String.IsNullOrWhiteSpace(Installed) AndAlso File.Exists(Path.Combine(Installed, AppExeName)) Then Return Installed
                End If
            End Using
        Catch
        End Try
        '3. 默认安装到当前用户目录，避免写入需要管理员权限的位置
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YuanQi Launcher")
    End Function

    ''' <summary>目标目录里是否已经有可更新的启动器。</summary>
    Public Function IsInstalled(Target As String) As Boolean
        Try
            Return File.Exists(Path.Combine(Target, AppExeName))
        Catch
            Return False
        End Try
    End Function

    ''' <summary>测试某目录（或其最近的已存在上级目录）是否可写。</summary>
    Public Function CanWriteTo(Target As String) As Boolean
        Try
            Dim DirPath As String = Target
            Dim Probe As String
            If Directory.Exists(DirPath) Then
                Probe = Path.Combine(DirPath, "_yq_probe_" & Guid.NewGuid().ToString("N") & ".tmp")
                Using Stream = File.Create(Probe)
                End Using
                File.Delete(Probe)
                Return True
            End If
            '目标还不存在：向上找到第一个存在的目录，试用它能否创建子目录
            DirPath = DirPath.TrimEnd("\"c)
            Do While Not String.IsNullOrEmpty(DirPath) AndAlso Not Directory.Exists(DirPath)
                DirPath = Path.GetDirectoryName(DirPath)
            Loop
            If String.IsNullOrEmpty(DirPath) Then Return False
            Dim ProbeDir As String = Path.Combine(DirPath, "_yq_probe_" & Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(ProbeDir)
            Directory.Delete(ProbeDir)
            Return True
        Catch
            Return False
        End Try
    End Function

    ''' <summary>当前进程是否以管理员身份运行。</summary>
    Public ReadOnly Property IsElevated As Boolean
        Get
            Try
                Dim Identity = Security.Principal.WindowsIdentity.GetCurrent()
                Return New Security.Principal.WindowsPrincipal(Identity).IsInRole(Security.Principal.WindowsBuiltInRole.Administrator)
            Catch
                Return False
            End Try
        End Get
    End Property

    ''' <summary>
    ''' 以管理员身份重新启动本安装程序并静默完成安装。返回提权后的进程；用户取消 UAC 时返回 Nothing。
    ''' </summary>
    Public Function StartElevatedInstall(Target As String, CreateShortcut As Boolean) As Process
        Try
            Dim SelfPath As String = Assembly.GetEntryAssembly().Location
            Dim Arguments As String = $"/S /D=""{Target}"" /nolaunch"
            If Not CreateShortcut Then Arguments &= " /noshortcut"
            Trace("申请管理员权限安装：" & Arguments)
            Return Process.Start(New ProcessStartInfo(SelfPath, Arguments) With {.UseShellExecute = True, .Verb = "runas"})
        Catch ex As Exception
            '用户点了 UAC 的“否”，或系统不允许提权
            Trace("申请管理员权限失败：" & ex.Message)
            Return Nothing
        End Try
    End Function

    ''' <summary>把常见的权限异常翻译成看得懂的提示。</summary>
    Public Function DescribeError(Ex As Exception, Target As String) As String
        If TypeOf Ex Is UnauthorizedAccessException OrElse TypeOf Ex Is Security.SecurityException Then
            Return $"没有权限写入：{vbCrLf}{Target}{vbCrLf}{vbCrLf}" &
                   "如果安装到了 Program Files 之类的受保护目录，请改到用户目录安装，" & vbCrLf &
                   "或右键安装包选择「以管理员身份运行」。"
        End If
        Return Ex.Message
    End Function

    ''' <summary>如果缘启正在运行，等它退出；超时则强制结束。</summary>
    Private Sub CloseRunningApp(Target As String)
        Try
            For Each Proc In Process.GetProcesses()
                Try
                    If String.Equals(Proc.ProcessName & ".exe", AppExeName, StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(Proc.ProcessName, "YuanQi Launcher", StringComparison.OrdinalIgnoreCase) Then
                        Proc.Kill()
                        Proc.WaitForExit(5000)
                    End If
                Catch
                End Try
            Next
        Catch
        End Try
    End Sub

#End Region

#Region "安装"

    ''' <summary>
    ''' 执行安装或更新。Progress 回调参数为 (0~1 进度, 状态文本)。
    ''' </summary>
    Public Sub Install(Target As String, CreateShortcut As Boolean, Progress As Action(Of Double, String))
        Dim Payload As Byte() = ReadPayload()
        If Payload Is Nothing Then Throw New Exception("安装包数据不完整，请重新下载安装包。")
        If String.IsNullOrWhiteSpace(Target) Then Throw New Exception("安装路径不能为空。")

        Report(Progress, 0.04, "正在准备安装……")
        CloseRunningApp(Target)

        Directory.CreateDirectory(Target)

        ' 先解压到安装目录里的临时文件夹：解压失败时旧版本仍然可用
        Report(Progress, 0.12, "正在解压安装文件……")
        Dim SourceDir As String = ExtractPayload(Payload, Target)

        Try
            ' 更新模式：解压成功后再清掉旧版本的「程序文件」，用户数据原样保留
            If IsInstalled(Target) Then
                Report(Progress, 0.22, "正在清理旧版本程序文件……")
                RemoveProgramFiles(Target)
            End If

            Report(Progress, 0.3, "正在写入新版本文件……")
            CopyFolder(SourceDir, Target, Progress)
        Finally
            ' 清理解压出来的临时文件
            Try
                Directory.Delete(Path.GetDirectoryName(SourceDir), True)
            Catch
            End Try
        End Try

        Report(Progress, 0.82, "正在设置文件夹权限……")
        GrantUsersAccess(Target)

        Report(Progress, 0.86, "正在注册安装信息……")
        WriteRegistry(Target)
        WriteUninstallScript(Target)

        If CreateShortcut Then
            Report(Progress, 0.93, "正在创建快捷方式……")
            CreateShortcuts(Target)
        End If

        Report(Progress, 1.0, "安装完成")
    End Sub

    Private Sub Report(Progress As Action(Of Double, String), Value As Double, Text As String)
        If Progress IsNot Nothing Then Progress(Value, Text)
    End Sub

    ''' <summary>删除目标目录里的程序文件（不动 YQL、.minecraft 等用户数据）。</summary>
    Private Sub RemoveProgramFiles(Target As String)
        Dim SelfPath As String = Assembly.GetEntryAssembly().Location
        ' 根目录下的程序文件
        For Each FilePath In Directory.GetFiles(Target)
            Dim Ext As String = Path.GetExtension(FilePath).ToLowerInvariant()
            If Not ProgramFileExts.Contains(Ext) Then Continue For
            If String.Equals(FilePath, SelfPath, StringComparison.OrdinalIgnoreCase) Then Continue For
            Try
                File.Delete(FilePath)
            Catch
            End Try
        Next
        ' 程序目录整体替换
        For Each DirName In ProgramDirs
            Dim DirPath As String = Path.Combine(Target, DirName)
            Try
                If Directory.Exists(DirPath) Then Directory.Delete(DirPath, True)
            Catch
            End Try
        Next
    End Sub

    Private Sub CopyFolder(SourceDir As String, TargetDir As String, Progress As Action(Of Double, String))
        Dim Files As New List(Of String)()
        Files.AddRange(Directory.GetFiles(SourceDir, "*", SearchOption.AllDirectories))
        Dim Total As Integer = Math.Max(Files.Count, 1)
        Dim Index As Integer = 0
        For Each FilePath In Files
            Dim Relative As String = FilePath.Substring(SourceDir.Length).TrimStart("\"c)
            Dim DestPath As String = Path.Combine(TargetDir, Relative)
            Dim DestDir As String = Path.GetDirectoryName(DestPath)
            If Not Directory.Exists(DestDir) Then Directory.CreateDirectory(DestDir)
            File.Copy(FilePath, DestPath, True)
            Index += 1
            If Index Mod 3 = 0 OrElse Index = Total Then
                Report(Progress, 0.3 + 0.54 * Index / Total, "正在写入文件（" & Index & "/" & Total & "）……")
            End If
        Next
    End Sub

#End Region

#Region "注册表与快捷方式"

    ''' <summary>写入安装位置记录与「应用和功能」中的卸载项。</summary>
    Private Sub WriteRegistry(Target As String)
        Try
            Using Key = Registry.CurrentUser.CreateSubKey(RegAppKey)
                Key.SetValue("InstallPath", Target, RegistryValueKind.String)
                Key.SetValue("Version", AppVersion, RegistryValueKind.String)
            End Using
            Using Key = Registry.CurrentUser.CreateSubKey(RegUninstallKey)
                Key.SetValue("DisplayName", "缘启 YuanQi Launcher", RegistryValueKind.String)
                Key.SetValue("DisplayVersion", AppVersion, RegistryValueKind.String)
                Key.SetValue("Publisher", "缘栽", RegistryValueKind.String)
                Key.SetValue("InstallLocation", Target, RegistryValueKind.String)
                Key.SetValue("DisplayIcon", Path.Combine(Target, AppExeName), RegistryValueKind.String)
                Key.SetValue("UninstallString",
                             "cmd /c " & Chr(34) & Chr(34) & Path.Combine(Target, "Uninstall.bat") & Chr(34) & Chr(34),
                             RegistryValueKind.String)
                Key.SetValue("NoModify", 1, RegistryValueKind.DWord)
                Key.SetValue("NoRepair", 1, RegistryValueKind.DWord)
            End Using
            Trace("注册表写入完成：" & RegUninstallKey)
        Catch ex As Exception
            Trace("注册表写入失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>写一个尽量简单的卸载脚本（纯 ASCII，避免编码问题）。</summary>
    Private Sub WriteUninstallScript(Target As String)
        Try
            Dim Lines As String() = {
                "@echo off",
                "setlocal",
                "title Uninstall YuanQi Launcher",
                "set ""DIR=%~dp0""",
                "echo Stopping YuanQi Launcher...",
                "taskkill /f /im ""YuanQi Launcher.exe"" >nul 2>&1",
                "taskkill /f /im ""terracotta.exe"" >nul 2>&1",
                "echo Removing program files...",
                "for %%E in (exe dll config pdb xml) do del /f /q ""%DIR%*.%%E"" >nul 2>&1",
                "for %%D in (Terracotta Videos Resources) do if exist ""%DIR%%%D"" rd /s /q ""%DIR%%%D"" >nul 2>&1",
                "echo Removing shortcuts...",
                "if exist ""%DIR%Uninstall.ps1"" powershell -NoProfile -ExecutionPolicy Bypass -File ""%DIR%Uninstall.ps1"" >nul 2>&1",
                "echo Removing registry entries...",
                "reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\YuanQiLauncher"" /f >nul 2>&1",
                "reg delete ""HKCU\Software\YuanQiLauncher"" /f >nul 2>&1",
                "choice /c YN /n /m ""Delete user data too (YQL, .minecraft)? [Y/N] """,
                "if errorlevel 2 goto keep",
                "echo Removing user data...",
                "rd /s /q ""%DIR%YQL"" >nul 2>&1",
                "rd /s /q ""%DIR%.minecraft"" >nul 2>&1",
                "echo All done.",
                "echo Cleaning up...",
                "cd /d ""%TEMP%""",
                "powershell -NoProfile -ExecutionPolicy Bypass -Command ""Start-Sleep -Milliseconds 600; Remove-Item -LiteralPath ('%DIR%'.TrimEnd([char]92)) -Recurse -Force -ErrorAction SilentlyContinue""",
                "exit /b",
                ":keep",
                "echo Done. User data kept in %DIR%",
                "echo Cleaning up...",
                "powershell -NoProfile -ExecutionPolicy Bypass -Command ""Start-Sleep -Milliseconds 500; Remove-Item -LiteralPath '%~f0' -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '%DIR%Uninstall.ps1' -Force -ErrorAction SilentlyContinue""",
                "exit /b"
            }
            File.WriteAllLines(Path.Combine(Target, "Uninstall.bat"), Lines, Encoding.ASCII)
            '清理快捷方式交给 PowerShell：.lnk 里是 Unicode 路径，批处理里写不了中文文件名
            Dim PsLines As String() = {
                "$target = 'YuanQi Launcher.exe'",
                "$shell = New-Object -ComObject WScript.Shell",
                "$dirs = @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))",
                "foreach ($dir in $dirs) {",
                "    if (-not (Test-Path -LiteralPath $dir)) { continue }",
                "    Get-ChildItem -LiteralPath $dir -Filter *.lnk -Recurse -ErrorAction SilentlyContinue | ForEach-Object {",
                "        try {",
                "            $sc = $shell.CreateShortcut($_.FullName)",
                "            if ($sc.TargetPath -like ('*' + $target)) {",
                "                Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue",
                "                $parent = Split-Path $_.FullName -Parent",
                "                if ((Test-Path -LiteralPath $parent) -and -not (Get-ChildItem -LiteralPath $parent -Force -ErrorAction SilentlyContinue) -and ($parent -ne $dir)) {",
                "                    Remove-Item -LiteralPath $parent -Force -ErrorAction SilentlyContinue",
                "                }",
                "            }",
                "        } catch { }",
                "    }",
                "}"
            }
            File.WriteAllLines(Path.Combine(Target, "Uninstall.ps1"), PsLines, Encoding.ASCII)
        Catch
        End Try
    End Sub

    Private Sub CreateShortcuts(Target As String)
        Try
            Dim ExePath As String = Path.Combine(Target, AppExeName)
            Dim Shell As Object = CreateObject("WScript.Shell")
            Dim Targets As New List(Of String)()
            Dim DesktopDir As String = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            If Not String.IsNullOrEmpty(DesktopDir) Then Targets.Add(Path.Combine(DesktopDir, "缘启.lnk"))
            Dim ProgramsDir As String = Environment.GetFolderPath(Environment.SpecialFolder.Programs)
            If Not String.IsNullOrEmpty(ProgramsDir) Then
                Dim MenuDir As String = Path.Combine(ProgramsDir, "缘启")
                If Not Directory.Exists(MenuDir) Then Directory.CreateDirectory(MenuDir)
                Targets.Add(Path.Combine(MenuDir, "缘启 YuanQi Launcher.lnk"))
            End If
            For Each LinkPath In Targets
                Try
                    Dim Shortcut = Shell.CreateShortcut(LinkPath)
                    Shortcut.TargetPath = ExePath
                    Shortcut.WorkingDirectory = Target
                    Shortcut.IconLocation = ExePath & ",0"
                    Shortcut.Description = "缘启 YuanQi Launcher"
                    Shortcut.Save()
                Catch
                End Try
            Next
        Catch
        End Try
    End Sub

#End Region

#Region "静默安装与卸载"

    Public Sub RunSilent()
        Try
            Dim Target As String = If(String.IsNullOrWhiteSpace(ArgTarget), DetectInstallPath(), ArgTarget)
            Trace("静默安装目标：" & Target)
            If Not CanWriteTo(Target) Then
                Trace("目标目录没有写入权限，已中止。请改用用户目录，或以管理员身份重新运行：" & Target)
                System.Environment.Exit(2)
                Return
            End If
            Install(Target, Not NoShortcut, Nothing)
            Trace("静默安装完成")
            If Not NoLaunch Then StartApp(Target)
        Catch ex As Exception
            Trace("静默安装失败：" & ex.ToString())
            If TypeOf ex Is UnauthorizedAccessException Then Trace("提示：目标目录没有写入权限，请改用用户目录或管理员身份运行")
            System.Environment.Exit(1)
        End Try
    End Sub

    Public Sub RunUninstall()
        Try
            Dim Target As String = If(String.IsNullOrWhiteSpace(ArgTarget), DetectInstallPath(), ArgTarget)
            If Not Silent Then
                Dim Answer = MessageBox.Show("确定要卸载缘启吗？" & vbCrLf & vbCrLf & "安装位置：" & Target & vbCrLf & "用户数据（YQL、.minecraft）默认保留。",
                                             "卸载 缘启 YuanQi Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If Answer <> DialogResult.Yes Then Return
            End If
            CloseRunningApp(Target)
            RemoveProgramFiles(Target)
            Try
                File.Delete(Path.Combine(Target, "Uninstall.bat"))
            Catch
            End Try
            Try
                Registry.CurrentUser.DeleteSubKeyTree(RegAppKey, False)
            Catch
            End Try
            Try
                Registry.CurrentUser.DeleteSubKeyTree(RegUninstallKey, False)
            Catch
            End Try
            If RemoveData Then
                For Each DirName In {"YQL", ".minecraft"}
                    Try
                        Dim DirPath As String = Path.Combine(Target, DirName)
                        If Directory.Exists(DirPath) Then Directory.Delete(DirPath, True)
                    Catch
                    End Try
                Next
            End If
            ' 清掉空目录
            Try
                If Directory.Exists(Target) AndAlso Directory.GetFileSystemEntries(Target).Length = 0 Then Directory.Delete(Target)
            Catch
            End Try
            If Not Silent Then MessageBox.Show("卸载完成。", "缘启 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            If Not Silent Then MessageBox.Show("卸载失败：" & vbCrLf & ex.Message, "缘启 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' 启动缘启。安装程序可能以管理员身份运行，这里通过资源管理器启动，
    ''' 让启动器以普通用户权限运行（否则写 YQL 会被管理员令牌带偏，也不该让游戏继承管理员权限）。
    ''' </summary>
    Public Sub StartApp(Target As String)
        Try
            Dim ExePath As String = Path.Combine(Target, AppExeName)
            If Not File.Exists(ExePath) Then Return
            If IsElevated Then
                Try
                    Process.Start(New ProcessStartInfo("explorer.exe", """" & ExePath & """") With {.UseShellExecute = True})
                    Return
                Catch ex As Exception
                    Trace("通过资源管理器启动失败：" & ex.Message)
                End Try
            End If
            Process.Start(New ProcessStartInfo(ExePath) With {.WorkingDirectory = Target, .UseShellExecute = True})
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' 给 Users 组授予安装目录的修改权限，避免安装后普通权限运行启动器时写不了 YQL。
    ''' 只在安装根目录设置可继承的 ACE（不加 /T），否则会把 .minecraft、WebView2 缓存等十几万文件全扫一遍，非常慢。
    ''' </summary>
    Public Sub GrantUsersAccess(Target As String)
        Try
            '不重定向输出：icacls 在十万级文件上会刷大量日志，读空了缓冲区会卡死等待
            Dim Psi As New ProcessStartInfo("icacls.exe", $"""{Target}"" /grant *S-1-5-32-545:(OI)(CI)M /C /Q") With {
                .UseShellExecute = False, .CreateNoWindow = True}
            Using Proc = Process.Start(Psi)
                If Proc Is Nothing Then Return
                If Not Proc.WaitForExit(15000) Then
                    Try
                        Proc.Kill()
                    Catch
                    End Try
                    Trace("授予文件夹权限超时，已跳过：" & Target)
                    Return
                End If
            End Using
            Trace("已为 Users 组授予安装目录的修改权限：" & Target)
        Catch ex As Exception
            Trace("授予文件夹权限失败：" & ex.Message)
        End Try
    End Sub

#End Region

End Module
