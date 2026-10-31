''' <summary>
''' 启动器设置的导出与导入。
''' 导出内容为「设置 → 各页面」中可见的普通设置项，不含与设备绑定的加密项。
''' </summary>
Public Module ModConfig

    ''' <summary>
    ''' 设置文件的格式版本。
    ''' </summary>
    Private Const ConfigFormatVersion As Integer = 1

    ''' <summary>
    ''' 不参与导出的设置项。
    ''' </summary>
    Private ReadOnly ExcludedKeys As String() = {
        "Identify",          '设备识别码，与加密设置绑定，换设备后无意义
        "SystemEulaVersion", '协议同意状态，应由用户自己确认
        "SystemCount",       '启动次数统计
        "SystemLaunchCount"
    }

    ''' <summary>
    ''' 判断某个设置项是否可以参与导出与导入。
    ''' </summary>
    Private Function IsExportable(Entry As Settings.Setting) As Boolean
        If Entry Is Nothing Then Return False
        If Entry.Encrypted Then Return False '加密项与设备绑定，导入到别的设备上无法解密
        If Entry.Source = Settings.Sources.Instance Then Return False '版本独立设置需要具体版本
        If ExcludedKeys.Contains(Entry.Key) Then Return False
        Return True
    End Function

    ''' <summary>
    ''' 把当前的全部设置导出到指定文件，返回导出的设置项数量。
    ''' </summary>
    Public Function ExportConfig(FilePath As String) As Integer
        Dim Data As New JObject From {
            {"Launcher", "YuanQi Launcher"},
            {"FormatVersion", ConfigFormatVersion},
            {"VersionCode", VersionCode},
            {"ExportTime", Now.ToString("yyyy-MM-dd HH:mm:ss")},
            {"Settings", New JObject()}
        }
        Dim Count As Integer = 0
        For Each Entry As Settings.Setting In Settings.Entries.Values
            If Not IsExportable(Entry) Then Continue For
            Try
                Data("Settings")(Entry.Key) = JToken.FromObject(Settings.Get(Entry.Key))
                Count += 1
            Catch ex As Exception
                Logger.Warn(ex, $"导出设置项失败：{Entry.Key}")
            End Try
        Next
        FileUtils.Write(FilePath, Data.ToString(Formatting.Indented))
        Logger.Info($"已导出 {Count} 项设置到 {FilePath}")
        Return Count
    End Function

    ''' <summary>
    ''' 从指定文件导入设置，返回实际应用的设置项数量。
    ''' </summary>
    Public Function ImportConfig(FilePath As String) As Integer
        Dim Text As String = FileUtils.ReadAsString(FilePath, Encoding.UTF8)
        If String.IsNullOrWhiteSpace(Text) Then Throw New Exception("设置文件为空！")
        Dim Data = CType(Text.DeserializeJson(), JObject)
        If Data Is Nothing Then Throw New Exception("设置文件的格式有误，无法解析！")
        Dim SettingsNode = Data("Settings")
        If SettingsNode Is Nothing OrElse TypeOf SettingsNode IsNot JObject Then
            Throw New Exception("设置文件的格式有误：找不到 Settings 字段！")
        End If
        Dim Count As Integer = 0, SkipCount As Integer = 0
        For Each Item As JProperty In CType(SettingsNode, JObject).Properties
            Dim Entry As Settings.Setting = Nothing
            If Not Settings.Entries.TryGetValue(Item.Name, Entry) Then
                SkipCount += 1 '该版本已不再使用这一项
                Continue For
            End If
            If Not IsExportable(Entry) Then
                SkipCount += 1
                Continue For
            End If
            Try
                Settings.Set(Entry.Key, Item.Value.ToObject(Entry.Type))
                Count += 1
            Catch ex As Exception
                SkipCount += 1
                Logger.Warn(ex, $"导入设置项失败：{Item.Name}")
            End Try
        Next
        Logger.Info($"已从 {FilePath} 导入 {Count} 项设置，跳过 {SkipCount} 项")
        If Count = 0 Then Throw New Exception($"没有可导入的设置项（跳过 {SkipCount} 项）。{vbCrLf}请确认该文件是由 缘启 导出的设置文件。")
        Return Count
    End Function

End Module
