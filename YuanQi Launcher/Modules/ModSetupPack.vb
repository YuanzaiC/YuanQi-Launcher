Imports System.IO

''' <summary>
''' 把「启动器程序 + 整合包」打成单文件安装程序。
''' 复用 YuanQi Setup 安装程序的格式：文件末尾依次是
'''   [安装程序本体][zip 负载][8 字节 int64 负载长度]["YQSETUP1"]
''' 安装程序运行时从自身尾部读回这段 zip 解压到目标目录，所以导出时不需要再重新编译安装程序。
''' </summary>
Public Module SetupPack

    Private Const MarkerText As String = "YQSETUP1"

    ''' <summary>找安装程序桩：启动器目录下的 Resources\Setup，其次启动器根目录。</summary>
    Public Function FindStub() As String
        Dim Candidates As String() = {
            Paths.Base & "Resources\Setup\YuanQi Setup.exe",
            Paths.Base & "YuanQi Setup.exe"
        }
        For Each Candidate In Candidates
            If File.Exists(Candidate) Then Return Candidate
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 用桩程序 + 负载目录生成单文件安装程序。
    ''' </summary>
    ''' <param name="StubPath">不带负载的安装程序本体。</param>
    ''' <param name="PayloadFolder">要装进安装包的内容目录（启动器程序文件、modpack.mrpack 等）。</param>
    ''' <param name="OutputPath">输出的 .exe 路径。</param>
    Public Function BuildInstaller(StubPath As String, PayloadFolder As String, OutputPath As String) As Boolean
        If Not File.Exists(StubPath) Then Throw New FileNotFoundException("找不到安装程序模板：" & StubPath)
        If Not Directory.Exists(PayloadFolder) Then Throw New DirectoryNotFoundException("找不到要打包的目录：" & PayloadFolder)
        Dim TempZip As String = PathTemp & "SetupPayload_" & GetUuid() & ".zip"
        Try
            FileUtils.CreateZipFromDirectory(TempZip, PayloadFolder)
            Dim StubBytes As Byte() = File.ReadAllBytes(StubPath)
            Dim ZipBytes As Byte() = File.ReadAllBytes(TempZip)
            Dim Folder As String = Path.GetDirectoryName(OutputPath)
            If Not String.IsNullOrEmpty(Folder) AndAlso Not Directory.Exists(Folder) Then Directory.CreateDirectory(Folder)
            Using Fs As New FileStream(OutputPath, FileMode.Create, FileAccess.Write, FileShare.None)
                Fs.Write(StubBytes, 0, StubBytes.Length)
                Fs.Write(ZipBytes, 0, ZipBytes.Length)
                Dim LengthBytes As Byte() = BitConverter.GetBytes(CLng(ZipBytes.Length))
                Fs.Write(LengthBytes, 0, LengthBytes.Length)
                Dim MarkBytes As Byte() = Text.Encoding.ASCII.GetBytes(MarkerText)
                Fs.Write(MarkBytes, 0, MarkBytes.Length)
            End Using
            Logger.Info($"已生成包含整合包的安装程序：{OutputPath}（负载 {Math.Round(ZipBytes.Length / 1024.0 / 1024.0, 1)} MB）")
            Return True
        Finally
            Try
                FileUtils.Delete(TempZip)
            Catch ex As Exception
            End Try
        End Try
    End Function

End Module
