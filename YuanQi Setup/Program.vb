''' <summary>
''' 安装程序入口。支持参数：
'''   /S                     静默安装（不显示界面）
'''   /D=&lt;路径&gt;              指定安装目录
'''   /uninstall             卸载
'''   /removedata            卸载时同时删除 YQL 与 .minecraft
''' </summary>
Public Module Program

    <STAThread>
    Public Sub Main(Args As String())
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        Installer.ParseArgs(Args)
        Installer.Trace("启动参数：" & String.Join(" | ", If(Args, New String() {})))

        If Installer.UninstallMode Then
            Installer.RunUninstall()
            Return
        End If

        If Installer.Silent Then
            Installer.RunSilent()
        Else
            Application.Run(New SetupForm())
        End If
    End Sub

End Module
