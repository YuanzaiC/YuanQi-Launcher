Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports Newtonsoft.Json.Linq

Public Class McSaveInfo
    Public FolderName As String = ""
    Public SavePath As String = ""
    Public LevelName As String = ""
    Public Seed As Long = 0
    Public HasSeed As Boolean = False
    Public GameType As Integer = 0
    Public Difficulty As Integer = 2
    Public AllowCommands As Boolean = False
    Public LastPlayed As Long = 0
    Public Time As Long = 0
    Public DayTime As Long = 0
    Public SpawnX As Integer = 0
    Public SpawnY As Integer = 0
    Public SpawnZ As Integer = 0
    Public GameVersion As String = ""
    Public GameRules As New Dictionary(Of String, Boolean)
End Class

Public Class McPlayerInfo
    Public Name As String = ""
    Public Uuid As String = ""
    Public X As Double = 0
    Public Y As Double = 0
    Public Z As Double = 0
    Public Dimension As String = ""
    Public XpLevel As Integer = 0
End Class

''' <summary>NBT 标签（树形结构，支持读写）。</summary>
Public Class NbtTag
    Public Type As Byte
    Public Name As String = ""
    Public Value As Object = Nothing
End Class

''' <summary>NBT 复合标签（有序子标签列表）。</summary>
Public Class NbtCompound
    Public Entries As New List(Of NbtTag)
    Public Function GetTag(Name As String) As NbtTag
        For Each T In Entries
            If T.Name = Name Then Return T
        Next
        Return Nothing
    End Function
    Public Function Ensure(Name As String, Type As Byte) As NbtTag
        Dim T = GetTag(Name)
        If T Is Nothing Then
            T = New NbtTag With {.Name = Name, .Type = Type}
            Entries.Add(T)
        End If
        Return T
    End Function
End Class

Public Module ModSave

    ' ===== 读取 =====

    ''' <summary>读取 level.dat 的根复合标签（含 Data）。</summary>
    Public Function ReadLevelDat(Path As String) As NbtCompound
        Dim Bytes = File.ReadAllBytes(Path)
        Try
            Using InStream = New MemoryStream(Bytes), Gz = New GZipStream(InStream, CompressionMode.Decompress), Out = New MemoryStream()
                Gz.CopyTo(Out)
                Return ReadNbtRoot(New BinaryReader(New MemoryStream(Out.ToArray())))
            End Using
        Catch
        End Try
        Return ReadNbtRoot(New BinaryReader(New MemoryStream(Bytes)))
    End Function

    ''' <summary>读取任意 gzip NBT 文件的根复合标签。</summary>
    Public Function ReadNbtFile(Path As String) As NbtCompound
        Return ReadLevelDat(Path)
    End Function

    Private Function ReadNbtRoot(R As BinaryReader) As NbtCompound
        If R.ReadByte() <> 10 Then Return Nothing
        ReadName(R)
        Return ReadCompound(R)
    End Function

    Private Function ReadName(R As BinaryReader) As String
        Return Encoding.UTF8.GetString(R.ReadBytes(ReadUShort(R)))
    End Function
    Private Function ReadUShort(R As BinaryReader) As Integer
        Dim B = R.ReadBytes(2) : Array.Reverse(B) : Return BitConverter.ToUInt16(B, 0)
    End Function
    Private Function ReadInt(R As BinaryReader) As Integer
        Dim B = R.ReadBytes(4) : Array.Reverse(B) : Return BitConverter.ToInt32(B, 0)
    End Function
    Private Function ReadLong(R As BinaryReader) As Long
        Dim B = R.ReadBytes(8) : Array.Reverse(B) : Return BitConverter.ToInt64(B, 0)
    End Function
    Private Function ReadShort(R As BinaryReader) As Short
        Dim B = R.ReadBytes(2) : Array.Reverse(B) : Return BitConverter.ToInt16(B, 0)
    End Function
    Private Function ReadFloat(R As BinaryReader) As Single
        Dim B = R.ReadBytes(4) : Array.Reverse(B) : Return BitConverter.ToSingle(B, 0)
    End Function
    Private Function ReadDouble(R As BinaryReader) As Double
        Dim B = R.ReadBytes(8) : Array.Reverse(B) : Return BitConverter.ToDouble(B, 0)
    End Function

    Private Function ReadCompound(R As BinaryReader) As NbtCompound
        Dim C As New NbtCompound
        Do
            Dim Type = R.ReadByte()
            If Type = 0 Then Exit Do
            Dim T As New NbtTag With {.Type = Type, .Name = ReadName(R)}
            T.Value = ReadPayload(R, Type)
            C.Entries.Add(T)
        Loop
        Return C
    End Function

    Private Function ReadPayload(R As BinaryReader, Type As Byte) As Object
        Select Case Type
            Case 1 : Return R.ReadByte()
            Case 2 : Return ReadShort(R)
            Case 3 : Return ReadInt(R)
            Case 4 : Return ReadLong(R)
            Case 5 : Return ReadFloat(R)
            Case 6 : Return ReadDouble(R)
            Case 7 : Dim N = ReadInt(R) : Return R.ReadBytes(N)
            Case 8 : Return ReadName(R)
            Case 9 :
                Dim Elem = R.ReadByte()
                Dim Count = ReadInt(R)
                Dim Lst As New List(Of NbtTag)
                For i = 0 To Count - 1
                    Lst.Add(New NbtTag With {.Type = Elem, .Value = ReadPayload(R, Elem)})
                Next
                Return Lst
            Case 10 : Return ReadCompound(R)
            Case 11 : Dim N = ReadInt(R) : Dim Arr As New List(Of Integer) : For i = 0 To N - 1 : Arr.Add(ReadInt(R)) : Next : Return Arr
            Case 12 : Dim N = ReadInt(R) : Dim Arr As New List(Of Long) : For i = 0 To N - 1 : Arr.Add(ReadLong(R)) : Next : Return Arr
            Case Else : Return Nothing
        End Select
    End Function

    ' ===== 写入 =====

    ''' <summary>把根复合标签写回 gzip 的 NBT 文件。</summary>
    Public Function WriteLevelDat(Path As String, Root As NbtCompound) As Boolean
        Try
            Dim Out As New MemoryStream
            Using W = New BinaryWriter(Out)
                W.Write(CByte(10))
                WriteName(W, "")
                WriteCompound(W, Root)
            End Using
            Dim Raw = Out.ToArray()
            Dim GzOut As New MemoryStream
            Using Gz = New GZipStream(GzOut, CompressionLevel.Optimal, True), W2 = New BinaryWriter(Gz)
                W2.Write(Raw)
            End Using
            '写临时文件后替换，避免直接写坏
            Dim Tmp = Path & ".tmp"
            File.WriteAllBytes(Tmp, GzOut.ToArray())
            If File.Exists(Path) Then File.Delete(Path)
            File.Move(Tmp, Path)
            Return True
        Catch ex As Exception
            Logger.Error(ex, "写入 level.dat 失败：" & Path)
            Return False
        End Try
    End Function

    Private Sub WriteName(W As BinaryWriter, Name As String)
        Dim B = Encoding.UTF8.GetBytes(Name)
        WriteUShort(W, B.Length)
        W.Write(B)
    End Sub
    Private Sub WriteUShort(W As BinaryWriter, V As Integer)
        Dim B = BitConverter.GetBytes(CUShort(V)) : Array.Reverse(B) : W.Write(B)
    End Sub
    Private Sub WriteInt(W As BinaryWriter, V As Integer)
        Dim B = BitConverter.GetBytes(V) : Array.Reverse(B) : W.Write(B)
    End Sub
    Private Sub WriteLong(W As BinaryWriter, V As Long)
        Dim B = BitConverter.GetBytes(V) : Array.Reverse(B) : W.Write(B)
    End Sub
    Private Sub WriteShort(W As BinaryWriter, V As Short)
        Dim B = BitConverter.GetBytes(V) : Array.Reverse(B) : W.Write(B)
    End Sub
    Private Sub WriteFloat(W As BinaryWriter, V As Single)
        Dim B = BitConverter.GetBytes(V) : Array.Reverse(B) : W.Write(B)
    End Sub
    Private Sub WriteDouble(W As BinaryWriter, V As Double)
        Dim B = BitConverter.GetBytes(V) : Array.Reverse(B) : W.Write(B)
    End Sub

    Private Sub WriteCompound(W As BinaryWriter, C As NbtCompound)
        For Each T In C.Entries
            W.Write(T.Type)
            WriteName(W, T.Name)
            WritePayload(W, T)
        Next
        W.Write(CByte(0))
    End Sub

    Private Sub WritePayload(W As BinaryWriter, T As NbtTag)
        Select Case T.Type
            Case 1 : W.Write(CByte(T.Value))
            Case 2 : WriteShort(W, CShort(T.Value))
            Case 3 : WriteInt(W, CInt(T.Value))
            Case 4 : WriteLong(W, CLng(T.Value))
            Case 5 : WriteFloat(W, CSng(T.Value))
            Case 6 : WriteDouble(W, CDbl(T.Value))
            Case 7 : Dim B = CType(T.Value, Byte()) : WriteInt(W, B.Length) : W.Write(B)
            Case 8 : WriteName(W, CStr(T.Value))
            Case 9 :
                Dim Lst = CType(T.Value, List(Of NbtTag))
                If Lst.Count = 0 Then
                    W.Write(CByte(0)) : WriteInt(W, 0)
                Else
                    W.Write(Lst(0).Type) : WriteInt(W, Lst.Count)
                    For Each E In Lst : WritePayload(W, E) : Next
                End If
            Case 10 : WriteCompound(W, CType(T.Value, NbtCompound))
            Case 11 : Dim L = CType(T.Value, List(Of Integer)) : WriteInt(W, L.Count) : For Each V In L : WriteInt(W, V) : Next
            Case 12 : Dim L = CType(T.Value, List(Of Long)) : WriteInt(W, L.Count) : For Each V In L : WriteLong(W, V) : Next
        End Select
    End Sub

    ' ===== 存档信息 =====

    ''' <summary>递归在 NBT 树里找名为 seed / RandomSeed 的整型标签（新版本 level.dat 格式兜底）。</summary>
    Private Function FindSeedTag(C As NbtCompound, Depth As Integer) As NbtTag
        If C Is Nothing OrElse Depth > 8 Then Return Nothing
        For Each T In C.Entries
            If T.Name = "seed" OrElse T.Name = "RandomSeed" Then
                If T.Type = 4 OrElse T.Type = 3 Then Return T 'Long / Int
                If T.Type = 8 Then
                    '个别格式把种子存成字符串，能转数字就当种子用
                    Dim Num As Long
                    If Long.TryParse(CStr(T.Value), Num) Then Return T
                End If
            End If
        Next
        For Each T In C.Entries
            If T.Type = 10 AndAlso TypeOf T.Value Is NbtCompound Then
                Dim R = FindSeedTag(CType(T.Value, NbtCompound), Depth + 1)
                If R IsNot Nothing Then Return R
            ElseIf T.Type = 9 AndAlso TypeOf T.Value Is List(Of NbtTag) Then
                For Each E In CType(T.Value, List(Of NbtTag))
                    If E.Type = 10 AndAlso TypeOf E.Value Is NbtCompound Then
                        Dim R = FindSeedTag(CType(E.Value, NbtCompound), Depth + 1)
                        If R IsNot Nothing Then Return R
                    End If
                Next
            End If
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 26.x 起，世界生成设置（含真正的种子）从 level.dat 移到了
    ''' <c>data\minecraft\world_gen_settings.dat</c>：根标签下的 seed 才是种子，
    ''' level.dat 里只剩时间/难度等运行时数据。这里单独读这个文件兜底。
    ''' </summary>
    Private Function TryReadSeedFromWorldGenSettings(SavePath As String, Info As McSaveInfo) As Boolean
        Dim Candidates As String() = {
            Path.Combine(SavePath, "data\minecraft\world_gen_settings.dat"),
            Path.Combine(SavePath, "data\world_gen_settings.dat")
        }
        For Each F In Candidates
            If Not File.Exists(F) Then Continue For
            Try
                Dim Root = ReadLevelDat(F)
                If Root Is Nothing Then Continue For
                '根下一般是 data（小写）复合标签，个别版本可能是 Data 或直接就是设置本体
                Dim Scope As NbtCompound = Root
                Dim Inner = Root.GetTag("data")
                If Inner Is Nothing Then Inner = Root.GetTag("Data")
                If Inner IsNot Nothing AndAlso TypeOf Inner.Value Is NbtCompound Then Scope = CType(Inner.Value, NbtCompound)
                Dim Direct = Scope.GetTag("seed")
                If Direct IsNot Nothing AndAlso Direct.Value IsNot Nothing Then
                    Info.Seed = CLng(Direct.Value)
                    Info.HasSeed = True
                    Return True
                End If
                Dim Found = FindSeedTag(Scope, 0)
                If Found Is Nothing Then Found = FindSeedTag(Root, 0)
                If Found IsNot Nothing AndAlso Found.Value IsNot Nothing Then
                    Info.Seed = CLng(Found.Value)
                    Info.HasSeed = True
                    Return True
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取 world_gen_settings.dat 失败：" & F)
            End Try
        Next
        Return False
    End Function

    ''' <summary>
    ''' 从存档路径推断所属实例名（…\versions\&lt;实例名&gt;\saves\&lt;存档名&gt;）。
    ''' 26.x 的 level.dat 里已经没有 Data.Version，用路径反推最可靠。
    ''' </summary>
    Private Function GuessVersionFromPath(SavePath As String) As String
        Try
            Dim Parts = SavePath.Split({IO.Path.DirectorySeparatorChar, IO.Path.AltDirectorySeparatorChar},
                                       StringSplitOptions.RemoveEmptyEntries)
            For i As Integer = Parts.Length - 2 To 1 Step -1
                If String.Equals(Parts(i), "versions", StringComparison.OrdinalIgnoreCase) AndAlso i + 1 <= Parts.Length - 1 Then
                    'versions 后面那一段才是实例名
                    Return Parts(i + 1)
                End If
            Next
        Catch ex As Exception
            Logger.Warn(ex, "从路径推断存档版本失败")
        End Try
        Return ""
    End Function

    Public Function LoadSaveInfo(SavePath As String) As McSaveInfo
        Dim Path2 = SavePath.TrimEnd("\"c)
        Dim Info As New McSaveInfo With {.SavePath = Path2, .FolderName = Path.GetFileName(Path2)}
        Dim LevelDat = Path2 & "\level.dat"
        If Not File.Exists(LevelDat) Then
            'level.dat 缺失（服务器存档或异常存档）：仍然尝试从世界生成设置里读种子
            TryReadSeedFromWorldGenSettings(Path2, Info)
            Return Info
        End If
        Try
            Dim Root = ReadLevelDat(LevelDat)
            If Root Is Nothing Then Return Info
            Dim DataTag = Root.GetTag("Data")
            If DataTag Is Nothing OrElse Not TypeOf DataTag.Value Is NbtCompound Then Return Info
            Dim Data = CType(DataTag.Value, NbtCompound)
            Info.LevelName = GetStr(Data, "LevelName", Info.FolderName)
            '种子：WorldGenSettings.seed（1.18+）→ RandomSeed（≤1.17）→ Data.seed（新格式，WorldGenSettings 已被移除）→ 递归兜底
            Dim SeedTag = Data.GetTag("RandomSeed")
            If SeedTag IsNot Nothing Then
                Info.Seed = CLng(SeedTag.Value) : Info.HasSeed = True
            Else
                Dim Wgs = Data.GetTag("WorldGenSettings")
                If Wgs IsNot Nothing AndAlso TypeOf Wgs.Value Is NbtCompound Then
                    Dim SeedTag2 = CType(Wgs.Value, NbtCompound).GetTag("seed")
                    If SeedTag2 IsNot Nothing Then Info.Seed = CLng(SeedTag2.Value) : Info.HasSeed = True
                End If
                If Not Info.HasSeed Then
                    Dim SeedTag3 = Data.GetTag("seed")
                    If SeedTag3 IsNot Nothing Then Info.Seed = CLng(SeedTag3.Value) : Info.HasSeed = True
                End If
            End If
            If Not Info.HasSeed Then
                '兜底：格式再怎么调整，种子的标签名也只会是 seed / RandomSeed；
                '递归找第一个名为它们的 Long/Int 标签，避免再出现"新版本读不到种子"
                Dim Found = FindSeedTag(Data, 0)
                If Found IsNot Nothing Then
                    Try
                        Info.Seed = CLng(Found.Value) : Info.HasSeed = True
                    Catch
                    End Try
                End If
            End If
            If Not Info.HasSeed Then
                '26.x：种子被搬到了 data\minecraft\world_gen_settings.dat
                TryReadSeedFromWorldGenSettings(Path2, Info)
            End If
            Info.GameType = GetInt(Data, "GameType", 0)
            Info.Difficulty = GetInt(Data, "Difficulty", 2)
            Info.AllowCommands = GetByte(Data, "allowCommands", 0) <> 0
            Info.LastPlayed = GetLong(Data, "LastPlayed", 0)
            Info.Time = GetLong(Data, "Time", 0)
            Info.DayTime = GetLong(Data, "DayTime", 0)
            Info.SpawnX = GetInt(Data, "SpawnX", 0)
            Info.SpawnY = GetInt(Data, "SpawnY", 64)
            Info.SpawnZ = GetInt(Data, "SpawnZ", 0)
            Dim Ver = Data.GetTag("Version")
            If Ver IsNot Nothing AndAlso TypeOf Ver.Value Is NbtCompound Then
                Info.GameVersion = GetStr(CType(Ver.Value, NbtCompound), "Name", "")
            End If
            If String.IsNullOrWhiteSpace(Info.GameVersion) Then
                '26.x 的 level.dat 里没有 Data.Version：改用存档所在实例名（…\versions\X\saves\…）
                Info.GameVersion = GuessVersionFromPath(Path2)
            End If
            Dim Rules = Data.GetTag("GameRules")
            If Rules IsNot Nothing AndAlso TypeOf Rules.Value Is NbtCompound Then
                For Each R In CType(Rules.Value, NbtCompound).Entries
                    Info.GameRules(R.Name) = String.Equals(CStr(R.Value), "true", StringComparison.OrdinalIgnoreCase)
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取存档 level.dat 失败：" & LevelDat)
        End Try
        Return Info
    End Function

    Private Function GetStr(C As NbtCompound, Name As String, Def As String) As String
        Dim T = C.GetTag(Name)
        Return If(T IsNot Nothing AndAlso T.Value IsNot Nothing, CStr(T.Value), Def)
    End Function
    Private Function GetInt(C As NbtCompound, Name As String, Def As Integer) As Integer
        Dim T = C.GetTag(Name)
        If T IsNot Nothing AndAlso T.Value IsNot Nothing Then Return CInt(T.Value)
        Return Def
    End Function
    Private Function GetLong(C As NbtCompound, Name As String, Def As Long) As Long
        Dim T = C.GetTag(Name)
        If T IsNot Nothing AndAlso T.Value IsNot Nothing Then Return CLng(T.Value)
        Return Def
    End Function
    Private Function GetByte(C As NbtCompound, Name As String, Def As Byte) As Byte
        Dim T = C.GetTag(Name)
        If T IsNot Nothing AndAlso T.Value IsNot Nothing Then Return CByte(T.Value)
        Return Def
    End Function

    ''' <summary>读取所有玩家信息（联机世界的多个 playerdata 文件）。</summary>
    Public Function LoadPlayers(SavePath As String, IndieRoot As String) As List(Of McPlayerInfo)
        Dim Result As New List(Of McPlayerInfo)
        Dim Pd = SavePath.TrimEnd("\"c) & "\playerdata\"
        If Not Directory.Exists(Pd) Then Return Result
        Try
            Dim Names As New Dictionary(Of String, String)
            Try
                Dim CacheFile = IndieRoot.TrimEnd("\"c) & "\usercache.json"
                If File.Exists(CacheFile) Then
                    Dim Arr = TryCast(File.ReadAllText(CacheFile).DeserializeJson(), JArray)
                    If Arr IsNot Nothing Then
                        For Each It In Arr
                            Names(It("uuid").ToString.ToLower) = It("name").ToString
                        Next
                    End If
                End If
            Catch
            End Try
            For Each F In Directory.GetFiles(Pd, "*.dat")
                Try
                    Dim Root = ReadLevelDat(F)
                    If Root Is Nothing Then Continue For
                    Dim Info As New McPlayerInfo
                    Info.Uuid = Path.GetFileNameWithoutExtension(F)
                    Dim N = Info.Uuid.ToLower
                    Info.Name = If(Names.ContainsKey(N), Names(N), Info.Uuid.Substring(0, 8))
                    Dim Pos = Root.GetTag("Pos")
                    If Pos IsNot Nothing AndAlso TypeOf Pos.Value Is List(Of NbtTag) Then
                        Dim L = CType(Pos.Value, List(Of NbtTag))
                        If L.Count >= 3 Then
                            Info.X = CDbl(L(0).Value) : Info.Y = CDbl(L(1).Value) : Info.Z = CDbl(L(2).Value)
                        End If
                    End If
                    Info.Dimension = GetStr(Root, "Dimension", "")
                    Info.XpLevel = GetInt(Root, "XpLevel", 0)
                    Result.Add(Info)
                Catch
                End Try
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取玩家数据失败")
        End Try
        Return Result
    End Function

    ''' <summary>读取玩家信息（第一个玩家，兼容旧调用）。</summary>
    Public Function LoadPlayerInfo(SavePath As String, IndieRoot As String) As McPlayerInfo
        Dim L = LoadPlayers(SavePath, IndieRoot)
        Return If(L.Count > 0, L(0), New McPlayerInfo)
    End Function

    ''' <summary>递归统计文件夹大小。</summary>
    Public Function GetFolderSize(Path As String) As Long
        Try
            Dim Total As Long = 0
            For Each F In Directory.GetFiles(Path, "*", SearchOption.AllDirectories)
                Try : Total += New FileInfo(F).Length : Catch : End Try
            Next
            Return Total
        Catch
            Return 0
        End Try
    End Function

    ' ===== 世界设置修改 =====

    ''' <summary>把世界设置写回 level.dat（自动先备份）。</summary>
    Public Function ApplySettings(SavePath As String, Apply As Action(Of NbtCompound, NbtCompound)) As Boolean
        Dim Path2 = SavePath.TrimEnd("\"c)
        Dim LevelDat = Path2 & "\level.dat"
        If Not File.Exists(LevelDat) Then Return False
        Try
            Dim Root = ReadLevelDat(LevelDat)
            If Root Is Nothing Then Return False
            Dim DataTag = Root.GetTag("Data")
            If DataTag Is Nothing OrElse Not TypeOf DataTag.Value Is NbtCompound Then Return False
            Dim Data = CType(DataTag.Value, NbtCompound)
            Apply(Data, Root)
            '修改前自动备份
            MakeBackup(SavePath, "修改前")
            Return WriteLevelDat(LevelDat, Root)
        Catch ex As Exception
            Logger.Error(ex, "应用世界设置失败：" & LevelDat)
            Return False
        End Try
    End Function

    Private Sub SetInt(C As NbtCompound, Name As String, Type As Byte, V As Integer)
        Dim T = C.Ensure(Name, Type) : T.Type = Type : T.Value = V
    End Sub
    Private Sub SetByte(C As NbtCompound, Name As String, V As Byte)
        Dim T = C.Ensure(Name, 1) : T.Type = 1 : T.Value = V
    End Sub
    Private Sub SetLong(C As NbtCompound, Name As String, V As Long)
        Dim T = C.Ensure(Name, 4) : T.Type = 4 : T.Value = V
    End Sub
    Private Sub SetStr(C As NbtCompound, Name As String, V As String)
        Dim T = C.Ensure(Name, 8) : T.Type = 8 : T.Value = V
    End Sub

    Public Sub SetGameType(Data As NbtCompound, V As Integer)
        SetInt(Data, "GameType", 3, V)
    End Sub
    Public Sub SetDifficulty(Data As NbtCompound, V As Integer)
        SetByte(Data, "Difficulty", CByte(V))
    End Sub
    Public Sub SetAllowCommands(Data As NbtCompound, V As Boolean)
        SetByte(Data, "allowCommands", If(V, CByte(1), CByte(0)))
    End Sub
    Public Sub SetLevelName(Data As NbtCompound, V As String)
        SetStr(Data, "LevelName", V)
    End Sub
    Public Sub SetSpawn(Data As NbtCompound, X As Integer, Y As Integer, Z As Integer)
        SetInt(Data, "SpawnX", 3, X) : SetInt(Data, "SpawnY", 3, Y) : SetInt(Data, "SpawnZ", 3, Z)
    End Sub
    Public Sub SetWorldTime(Data As NbtCompound, V As Long)
        SetLong(Data, "Time", V) : SetLong(Data, "DayTime", V)
    End Sub
    Public Sub SetGameRule(Data As NbtCompound, Name As String, V As Boolean)
        Dim RulesTag = Data.GetTag("GameRules")
        Dim Rules As NbtCompound
        If RulesTag Is Nothing OrElse Not TypeOf RulesTag.Value Is NbtCompound Then
            Rules = New NbtCompound
            RulesTag = New NbtTag With {.Name = "GameRules", .Type = 10, .Value = Rules}
            Data.Entries.Add(RulesTag)
        Else
            Rules = CType(RulesTag.Value, NbtCompound)
        End If
        SetStr(Rules, Name, If(V, "true", "false"))
    End Sub

    ' ===== 备份 / 副本 / 导入导出 =====

    Private ReadOnly Property BackupRoot As String
        Get
            Return Paths.Base & "YQL\Backups\"
        End Get
    End Property

    ''' <summary>创建一份备份，返回备份文件夹路径。</summary>
    Public Function MakeBackup(SavePath As String, Optional Tag As String = "手动") As String
        Try
            Dim Name = Path.GetFileName(SavePath.TrimEnd("\"c))
            Dim Root = BackupRoot & Name & "\"
            Directory.CreateDirectory(Root)
            Dim Stamp = Date.Now.ToString("yyyyMMdd-HHmmss")
            Dim Target = Root & Name & " " & Stamp & " " & Tag & "\"
            CopyDirectory(SavePath, Target)
            '清理旧备份（保留最近 N 份，可在页面中设置）
            Dim Keep As Integer = 10
            Try : Keep = Settings.Get(Of Integer)("SaveBackupCount") : Catch : End Try
            If Keep < 1 Then Keep = 10
            Dim Dirs = Directory.GetDirectories(Root).OrderByDescending(Function(d) d).ToList
            For i = Keep To Dirs.Count - 1
                Try : DirectoryUtils.Delete(Dirs(i), True) : Catch : End Try
            Next
            Return Target
        Catch ex As Exception
            Logger.Error(ex, "创建存档备份失败")
            Return ""
        End Try
    End Function

    ''' <summary>备份列表（按时间倒序）。</summary>
    Public Function ListBackups(SavePath As String) As List(Of String)
        Dim Result As New List(Of String)
        Try
            Dim Name = Path.GetFileName(SavePath.TrimEnd("\"c))
            Dim Root = BackupRoot & Name & "\"
            If Directory.Exists(Root) Then
                Result = Directory.GetDirectories(Root).OrderByDescending(Function(d) d).ToList
            End If
        Catch
        End Try
        Return Result
    End Function

    ''' <summary>删除一个备份文件夹。</summary>
    Public Sub DeleteBackup(Folder As String)
        Try
            If Directory.Exists(Folder) Then DirectoryUtils.Delete(Folder, True)
        Catch ex As Exception
            Logger.Error(ex, "删除备份失败")
        End Try
    End Sub

    ''' <summary>创建存档副本（重命名）。</summary>
    Public Function MakeCopy(SavePath As String) As String
        Try
            Dim Parent = Path.GetDirectoryName(SavePath.TrimEnd("\"c))
            Dim Base = Path.GetFileName(SavePath.TrimEnd("\"c))
            Dim Target = Parent & "\" & Base & " (副本)"
            Dim I = 2
            While Directory.Exists(Target)
                Target = Parent & "\" & Base & " (副本 " & I & ")"
                I += 1
            End While
            CopyDirectory(SavePath, Target)
            Return Target
        Catch ex As Exception
            Logger.Error(ex, "创建存档副本失败")
            Return ""
        End Try
    End Function

    ''' <summary>复制文件夹（递归）。</summary>
    Private Sub CopyDirectory(Source As String, Target As String)
        Directory.CreateDirectory(Target)
        For Each F In Directory.GetFiles(Source)
            File.Copy(F, Path.Combine(Target, Path.GetFileName(F)), True)
        Next
        For Each D In Directory.GetDirectories(Source)
            CopyDirectory(D, Path.Combine(Target, Path.GetFileName(D)))
        Next
    End Sub

    ''' <summary>恢复备份（覆盖当前存档；调用前需确认，并自动备份当前）。</summary>
    Public Function RestoreBackup(SavePath As String, BackupFolder As String) As Boolean
        Try
            If Not Directory.Exists(BackupFolder) Then Return False
            MakeBackup(SavePath, "恢复前")
            DirectoryUtils.Delete(SavePath, True)
            CopyDirectory(BackupFolder, SavePath)
            Return True
        Catch ex As Exception
            Logger.Error(ex, "恢复备份失败")
            Return False
        End Try
    End Function

    ''' <summary>导出存档到指定文件夹。</summary>
    Public Function ExportSave(SavePath As String, TargetParent As String) As String
        Try
            Dim Name = Path.GetFileName(SavePath.TrimEnd("\"c))
            Dim Target = TargetParent.TrimEnd("\"c) & "\" & Name
            Dim I = 2
            While Directory.Exists(Target)
                Target = TargetParent.TrimEnd("\"c) & "\" & Name & " (" & I & ")"
                I += 1
            End While
            CopyDirectory(SavePath, Target)
            Return Target
        Catch ex As Exception
            Logger.Error(ex, "导出存档失败")
            Return ""
        End Try
    End Function

    ''' <summary>导入存档文件夹到当前版本的 saves 目录。</summary>
    Public Function ImportSave(SavesRoot As String, SourceFolder As String) As String
        Try
            Dim Name = Path.GetFileName(SourceFolder.TrimEnd("\"c))
            Dim Target = SavesRoot.TrimEnd("\"c) & "\" & Name
            Dim I = 2
            While Directory.Exists(Target)
                Target = SavesRoot.TrimEnd("\"c) & "\" & Name & " (" & I & ")"
                I += 1
            End While
            CopyDirectory(SourceFolder, Target)
            Return Target
        Catch ex As Exception
            Logger.Error(ex, "导入存档失败")
            Return ""
        End Try
    End Function

    ''' <summary>游戏模式名称。</summary>
    Public Function GameTypeName(V As Integer) As String
        Select Case V
            Case 0 : Return "生存"
            Case 1 : Return "创造"
            Case 2 : Return "冒险"
            Case 3 : Return "旁观"
            Case Else : Return "未知"
        End Select
    End Function

    ''' <summary>修改指定玩家（UUID）的坐标与经验等级。</summary>
    Public Function SetPlayerPositionAndXp(SavePath As String, Uuid As String, X As Double, Y As Double, Z As Double, XpLevel As Integer) As Boolean
        Try
            Dim Pd = SavePath.TrimEnd("\"c) & "\playerdata\"
            If Not Directory.Exists(Pd) Then Return False
            Dim Target = If(String.IsNullOrWhiteSpace(Uuid), Nothing, Pd & Uuid & ".dat")
            For Each F In Directory.GetFiles(Pd, "*.dat")
                If Target IsNot Nothing AndAlso Not String.Equals(F, Target, StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim Root = ReadLevelDat(F)
                If Root Is Nothing Then Continue For
                Dim Lst As New List(Of NbtTag)
                Lst.Add(New NbtTag With {.Type = 6, .Value = X})
                Lst.Add(New NbtTag With {.Type = 6, .Value = Y})
                Lst.Add(New NbtTag With {.Type = 6, .Value = Z})
                Dim PosTag = Root.Ensure("Pos", 9)
                PosTag.Type = 9 : PosTag.Value = Lst
                Dim Xp = Root.Ensure("XpLevel", 3)
                Xp.Type = 3 : Xp.Value = XpLevel
                Return WriteLevelDat(F, Root)
            Next
        Catch ex As Exception
            Logger.Error(ex, "修改玩家数据失败")
        End Try
        Return False
    End Function

    ''' <summary>把 NBT 复合标签的顶层字段扁平化为「名称 = 值」文本（只读查看）。</summary>
    Public Function FlattenNbtTop(C As NbtCompound) As List(Of String)
        Dim Result As New List(Of String)
        If C Is Nothing Then Return Result
        For Each T In C.Entries
            Result.Add(T.Name & " = " & FormatNbtValue(T))
        Next
        Return Result
    End Function

    Private Function FormatNbtValue(T As NbtTag) As String
        Try
            Select Case T.Type
                Case 1 : Return CStr(CByte(T.Value)) & "  (byte)"
                Case 2 : Return CStr(CShort(T.Value)) & "  (short)"
                Case 3 : Return CStr(CInt(T.Value)) & "  (int)"
                Case 4 : Return CStr(CLng(T.Value)) & "  (long)"
                Case 5 : Return CStr(CSng(T.Value)) & "  (float)"
                Case 6 : Return CStr(CDbl(T.Value)) & "  (double)"
                Case 8 : Return CStr(T.Value)
                Case 7 : Return "byte[" & CType(T.Value, Byte()).Length & "]"
                Case 9 : Return "list[" & CType(T.Value, List(Of NbtTag)).Count & "]"
                Case 10 : Return "{...}"
                Case 11 : Return "int[" & CType(T.Value, List(Of Integer)).Count & "]"
                Case 12 : Return "long[" & CType(T.Value, List(Of Long)).Count & "]"
                Case Else : Return "?"
            End Select
        Catch
            Return "?"
        End Try
    End Function

    ' ===== 背包 / 末影箱 =====

    Public Class McItemStack
        Public Slot As Integer
        Public Id As String
        Public Count As Integer
    End Class

    Private Function GetPlayerFile(SavePath As String) As String
        Try
            Dim Pd = SavePath.TrimEnd("\"c) & "\playerdata\"
            If Not Directory.Exists(Pd) Then Return Nothing
            For Each F In Directory.GetFiles(Pd, "*.dat")
                Return F
            Next
        Catch
        End Try
        Return Nothing
    End Function

    Private Function GetPlayerRoot(SavePath As String) As NbtCompound
        Dim F = GetPlayerFile(SavePath)
        If F Is Nothing Then Return Nothing
        Return ReadLevelDat(F)
    End Function

    ''' <summary>读取背包（Inventory）或末影箱（EnderItems）物品列表。</summary>
    Public Function LoadInventory(SavePath As String, Ender As Boolean) As List(Of McItemStack)
        Dim Result As New List(Of McItemStack)
        Try
            Dim Root = GetPlayerRoot(SavePath)
            If Root Is Nothing Then Return Result
            Dim TagName = If(Ender, "EnderItems", "Inventory")
            Dim Tag = Root.GetTag(TagName)
            If Tag Is Nothing OrElse Not TypeOf Tag.Value Is List(Of NbtTag) Then Return Result
            For Each Item In CType(Tag.Value, List(Of NbtTag))
                If Item.Type <> 10 OrElse Not TypeOf Item.Value Is NbtCompound Then Continue For
                Dim C = CType(Item.Value, NbtCompound)
                Result.Add(New McItemStack With {
                    .Slot = GetInt(C, "Slot", -1),
                    .Id = GetStr(C, "id", "?"),
                    .Count = GetInt(C, "Count", 0)})
            Next
            Result.Sort(Function(a, b) a.Slot.CompareTo(b.Slot))
        Catch ex As Exception
            Logger.Warn(ex, "读取背包物品失败")
        End Try
        Return Result
    End Function

    ''' <summary>修改物品数量；NewCount &lt;= 0 时删除该物品。</summary>
    Public Function SetItemCount(SavePath As String, Ender As Boolean, Slot As Integer, NewCount As Integer) As Boolean
        Try
            Dim File = GetPlayerFile(SavePath)
            If File Is Nothing Then Return False
            Dim Root = ReadLevelDat(File)
            If Root Is Nothing Then Return False
            Dim TagName = If(Ender, "EnderItems", "Inventory")
            Dim Tag = Root.GetTag(TagName)
            If Tag Is Nothing OrElse Not TypeOf Tag.Value Is List(Of NbtTag) Then Return False
            Dim Lst = CType(Tag.Value, List(Of NbtTag))
            For i = Lst.Count - 1 To 0 Step -1
                Dim Item = Lst(i)
                If Item.Type <> 10 OrElse Not TypeOf Item.Value Is NbtCompound Then Continue For
                Dim C = CType(Item.Value, NbtCompound)
                If GetInt(C, "Slot", -1) = Slot Then
                    If NewCount <= 0 Then
                        Lst.RemoveAt(i)
                    Else
                        Dim CountTag = C.Ensure("Count", 1)
                        CountTag.Type = 1 : CountTag.Value = CByte(Math.Min(NewCount, 255))
                    End If
                    Return WriteLevelDat(File, Root)
                End If
            Next
        Catch ex As Exception
            Logger.Error(ex, "修改物品数量失败")
        End Try
        Return False
    End Function

    ''' <summary>删除指定槽位的物品。</summary>
    Public Function RemoveItem(SavePath As String, Ender As Boolean, Slot As Integer) As Boolean
        Return SetItemCount(SavePath, Ender, Slot, 0)
    End Function

    ''' <summary>添加物品到第一个空槽位。</summary>
    Public Function AddItem(SavePath As String, Ender As Boolean, Id As String, Count As Integer) As Boolean
        Try
            If String.IsNullOrWhiteSpace(Id) Then Return False
            Dim File = GetPlayerFile(SavePath)
            If File Is Nothing Then Return False
            Dim Root = ReadLevelDat(File)
            If Root Is Nothing Then Return False
            Dim TagName = If(Ender, "EnderItems", "Inventory")
            Dim Tag = Root.Ensure(TagName, 9)
            If Not TypeOf Tag.Value Is List(Of NbtTag) Then
                Tag.Type = 9 : Tag.Value = New List(Of NbtTag)
            End If
            Dim Lst = CType(Tag.Value, List(Of NbtTag))
            Dim Used As New HashSet(Of Integer)
            For Each Item In Lst
                If Item.Type = 10 AndAlso TypeOf Item.Value Is NbtCompound Then
                    Used.Add(GetInt(CType(Item.Value, NbtCompound), "Slot", -1))
                End If
            Next
            Dim MaxSlot = If(Ender, 27, 36)
            Dim FreeSlot As Integer = -1
            For s = 0 To MaxSlot - 1
                If Not Used.Contains(s) Then FreeSlot = s : Exit For
            Next
            If FreeSlot < 0 Then Return False
            Dim NewItem As New NbtCompound
            NewItem.Entries.Add(New NbtTag With {.Name = "Slot", .Type = 1, .Value = CByte(FreeSlot)})
            NewItem.Entries.Add(New NbtTag With {.Name = "id", .Type = 8, .Value = Id.Trim})
            NewItem.Entries.Add(New NbtTag With {.Name = "Count", .Type = 1, .Value = CByte(Math.Max(1, Math.Min(Count, 64)))})
            Lst.Add(New NbtTag With {.Type = 10, .Value = NewItem})
            Return WriteLevelDat(File, Root)
        Catch ex As Exception
            Logger.Error(ex, "添加物品失败")
        End Try
        Return False
    End Function

    ' ===== 统计 / 进度 =====

    ''' <summary>读取统计（扁平化：分类.键 = 值）。</summary>
    Public Function LoadStats(SavePath As String) As List(Of String)
        Dim Result As New List(Of String)
        Try
            Dim Root = GetPlayerRoot(SavePath)
            If Root Is Nothing Then Return Result
            Dim Stats = Root.GetTag("Stats")
            If Stats Is Nothing OrElse Not TypeOf Stats.Value Is NbtCompound Then Return Result
            For Each Cat In CType(Stats.Value, NbtCompound).Entries
                If Cat.Type = 10 AndAlso TypeOf Cat.Value Is NbtCompound Then
                    For Each Entry In CType(Cat.Value, NbtCompound).Entries
                        Result.Add(Cat.Name & "." & Entry.Name & " = " & CInt(Entry.Value))
                    Next
                End If
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取统计失败")
        End Try
        Return Result
    End Function

    ''' <summary>读取进度（进度 ID 列表，含完成状态）。</summary>
    Public Function LoadAdvancements(SavePath As String) As List(Of String)
        Dim Result As New List(Of String)
        Try
            Dim Root = GetPlayerRoot(SavePath)
            If Root Is Nothing Then Return Result
            Dim Adv = Root.GetTag("advancements")
            If Adv Is Nothing OrElse Not TypeOf Adv.Value Is NbtCompound Then Return Result
            For Each E In CType(Adv.Value, NbtCompound).Entries
                Dim Done = ""
                If E.Type = 10 AndAlso TypeOf E.Value Is NbtCompound Then
                    Dim C = CType(E.Value, NbtCompound)
                    Dim DoneTag = C.GetTag("done")
                    If DoneTag IsNot Nothing AndAlso CByte(DoneTag.Value) <> 0 Then Done = "  [已完成]"
                End If
                Result.Add(E.Name & Done)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取进度失败")
        End Try
        Return Result
    End Function

    ''' <summary>清空统计。</summary>
    Public Function ClearStats(SavePath As String) As Boolean
        Return RemoveRootTag(SavePath, "Stats")
    End Function

    ''' <summary>重置进度。</summary>
    Public Function ClearAdvancements(SavePath As String) As Boolean
        Return RemoveRootTag(SavePath, "advancements")
    End Function

    Private Function RemoveRootTag(SavePath As String, Name As String) As Boolean
        Try
            Dim File = GetPlayerFile(SavePath)
            If File Is Nothing Then Return False
            Dim Root = ReadLevelDat(File)
            If Root Is Nothing Then Return False
            For i = Root.Entries.Count - 1 To 0 Step -1
                If Root.Entries(i).Name = Name Then Root.Entries.RemoveAt(i)
            Next
            Return WriteLevelDat(File, Root)
        Catch ex As Exception
            Logger.Error(ex, "删除 NBT 标签失败")
            Return False
        End Try
    End Function

End Module
