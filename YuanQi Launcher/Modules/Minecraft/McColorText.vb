Imports System.Windows.Media
Imports System.Windows.Documents
Imports System.Text
Imports System.Globalization
Imports System.Linq

''' <summary>
''' Minecraft 彩色文字引擎：解析 § 格式码与 §x§RRGGBB 十六进制颜色，生成 WPF 彩色 Inlines，
''' 提供渐变与快捷配色，并支持 §码、&amp;码、JSON、MiniMessage 四种格式序列化。
''' </summary>
Public Module McColorText

    Public Class McColorInfo
        Public Code As String
        Public Name As String
        Public Color As Color
        Public Sub New(c As String, n As String, r As Byte, g As Byte, b As Byte)
            Code = c : Name = n : Color = Color.FromRgb(r, g, b)
        End Sub
    End Class

    ''' <summary>官方经典 16 色（§0-§f）。</summary>
    Public ReadOnly ClassicColors As List(Of McColorInfo) = New List(Of McColorInfo) From {
        New McColorInfo("0", "黑色", 0, 0, 0),
        New McColorInfo("1", "深蓝色", 0, 0, 170),
        New McColorInfo("2", "深绿色", 0, 170, 0),
        New McColorInfo("3", "深水色", 0, 170, 170),
        New McColorInfo("4", "深红色", 170, 0, 0),
        New McColorInfo("5", "深紫色", 170, 0, 170),
        New McColorInfo("6", "金色", 255, 170, 0),
        New McColorInfo("7", "灰色", 170, 170, 170),
        New McColorInfo("8", "深灰色", 85, 85, 85),
        New McColorInfo("9", "蓝色", 85, 85, 255),
        New McColorInfo("a", "绿色", 85, 255, 85),
        New McColorInfo("b", "水色", 85, 255, 255),
        New McColorInfo("c", "红色", 255, 85, 85),
        New McColorInfo("d", "浅紫色", 255, 85, 255),
        New McColorInfo("e", "黄色", 255, 255, 85),
        New McColorInfo("f", "白色", 255, 255, 255)
    }

    ''' <summary>经典色 → MiniMessage 具名颜色（按下标与 ClassicColors 对应）。</summary>
    Private ReadOnly MiniMessageNames As String() = {
        "black", "dark_blue", "dark_green", "dark_aqua", "dark_red", "dark_purple", "gold", "gray",
        "dark_gray", "blue", "green", "aqua", "red", "light_purple", "yellow", "white"
    }

    Public Class McRun
        Public Text As String
        Public Color As Color
        Public Bold As Boolean
        Public Italic As Boolean
        Public Underline As Boolean
        Public Strike As Boolean
        Public Obfuscated As Boolean
        ''' <summary>本段文本在源字符串中的起始偏移（格式码不计入）。</summary>
        Public Start As Integer
    End Class

    ''' <summary>某一位置的格式状态（用于样式按钮激活态与恢复码重建）。</summary>
    Public Structure McFormatState
        Public Color As Color
        Public Bold As Boolean
        Public Italic As Boolean
        Public Underline As Boolean
        Public Strike As Boolean
        Public Obf As Boolean
    End Structure

    Public Enum McOutputFormat
        Section = 0
        Amp = 1
        Json = 2
        MiniMessage = 3
    End Enum

    Public Class McInline
        Public Seg As McRun
        Public Inline As Run
    End Class

    Private Class ParseCoreResult
        Public Runs As New List(Of McRun)
        Public FinalState As McFormatState
    End Class

    ''' <summary>未指定颜色时的基准色：游戏内聊天默认白色。</summary>
    Public ReadOnly DefaultTextColor As Color = Color.FromRgb(255, 255, 255)

    ''' <summary>解析彩色文本为若干文本段（Start 记录源偏移）。</summary>
    Public Function Parse(Text As String, Optional DefaultColor As Nullable(Of Color) = Nothing) As List(Of McRun)
        Dim Def As Color = If(DefaultColor.HasValue, DefaultColor.Value, DefaultTextColor)
        Return ParseCore(Text, Def).Runs
    End Function

    Private Function ParseCore(Text As String, Def As Color) As ParseCoreResult
        Dim Result As New ParseCoreResult
        Result.FinalState.Color = Def
        If String.IsNullOrEmpty(Text) Then Return Result
        Dim State As McFormatState = Result.FinalState
        Dim Cur As New StringBuilder()
        Dim RunStart As Integer = 0
        Dim Flush =
        Sub()
            If Cur.Length > 0 Then
                Dim R As New McRun With {
                    .Text = Cur.ToString(), .Color = State.Color, .Bold = State.Bold, .Italic = State.Italic,
                    .Underline = State.Underline, .Strike = State.Strike, .Obfuscated = State.Obf, .Start = RunStart}
                Result.Runs.Add(R)
                Cur.Clear()
            End If
        End Sub
        Dim i As Integer = 0
        While i < Text.Length
            Dim ch As Char = Text(i)
            If ch = "§"c AndAlso i + 1 < Text.Length Then
                Flush()
                Dim code As Char = Char.ToLower(Text(i + 1))
                '§x§R§R§G§G§B§B 十六进制颜色
                If code = "x"c AndAlso i + 13 < Text.Length Then
                    Dim hex As String = Text(i + 3) & Text(i + 5) & Text(i + 7) & Text(i + 9) & Text(i + 11) & Text(i + 13)
                    Dim rgb As Integer = 0
                    If Integer.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, rgb) Then
                        State.Color = Color.FromRgb(CByte((rgb >> 16) And &HFF), CByte((rgb >> 8) And &HFF), CByte(rgb And &HFF))
                        i += 14
                        Continue While
                    End If
                End If
                Select Case code
                    Case "l"c : State.Bold = True
                    Case "o"c : State.Italic = True
                    Case "n"c : State.Underline = True
                    Case "m"c : State.Strike = True
                    Case "k"c : State.Obf = True
                    Case "r"c : State = New McFormatState With {.Color = Def}
                    Case "0"c To "9"c, "a"c To "f"c
                        Dim Info = ClassicColors.FirstOrDefault(Function(x) x.Code = code.ToString())
                        If Info IsNot Nothing Then State.Color = Info.Color
                End Select
                i += 2
            Else
                If Cur.Length = 0 Then RunStart = i
                Cur.Append(ch)
                i += 1
            End If
        End While
        Flush()
        Result.FinalState = State
        Return Result
    End Function

    ''' <summary>获取源文本中某偏移处（即光标处新输入文字将应用的）格式状态。</summary>
    Public Function GetStateAt(Text As String, Caret As Integer, Optional DefaultColor As Nullable(Of Color) = Nothing) As McFormatState
        Dim Def As Color = If(DefaultColor.HasValue, DefaultColor.Value, DefaultTextColor)
        If String.IsNullOrEmpty(Text) OrElse Caret <= 0 Then
            Return New McFormatState With {.Color = Def}
        End If
        If Caret > Text.Length Then Caret = Text.Length
        Return ParseCore(Text.Substring(0, Caret), Def).FinalState
    End Function

    ''' <summary>判断某样式码在状态中是否激活。</summary>
    Public Function IsStyleActive(State As McFormatState, code As Char) As Boolean
        Select Case Char.ToLower(code)
            Case "l"c : Return State.Bold
            Case "o"c : Return State.Italic
            Case "n"c : Return State.Underline
            Case "m"c : Return State.Strike
            Case "k"c : Return State.Obf
            Case Else : Return False
        End Select
    End Function

    ''' <summary>构建 WPF Run 列表（用于彩色文字显示）。Darken=True 时输出 25% 亮度的阴影层。</summary>
    Public Function BuildInlines(Text As String, Optional DefaultColor As Nullable(Of Color) = Nothing, Optional Darken As Boolean = False) As List(Of Run)
        Return BuildRunPairs(Text, DefaultColor, Darken).Select(Function(p) p.Inline).ToList()
    End Function

    ''' <summary>构建 Run 并保留段落信息（用于混淆段动画）。</summary>
    Public Function BuildRunPairs(Text As String, Optional DefaultColor As Nullable(Of Color) = Nothing, Optional Darken As Boolean = False) As List(Of McInline)
        Dim Result As New List(Of McInline)
        For Each R In Parse(Text, DefaultColor)
            If String.IsNullOrEmpty(R.Text) Then Continue For
            Dim Run As New Run(R.Text) With {.Foreground = New SolidColorBrush(If(Darken, DarkenColor(R.Color, 0.25), R.Color))}
            If R.Bold Then Run.FontWeight = FontWeights.Bold
            If R.Italic Then Run.FontStyle = FontStyles.Italic
            Dim Decor As New TextDecorationCollection()
            If R.Underline Then Decor.Add(TextDecorations.Underline)
            If R.Strike Then Decor.Add(TextDecorations.Strikethrough)
            If Decor.Count > 0 Then Run.TextDecorations = Decor
            Result.Add(New McInline With {.Seg = R, .Inline = Run})
        Next
        Return Result
    End Function

    ''' <summary>把颜色按比例调暗（用于模拟 Minecraft 文字阴影）。</summary>
    Public Function DarkenColor(c As Color, factor As Double) As Color
        Return Color.FromRgb(CByte(c.R * factor), CByte(c.G * factor), CByte(c.B * factor))
    End Function

    ''' <summary>移除全部格式码（含 §x 序列），返回纯文本。</summary>
    Public Function StripColorCodes(text As String) As String
        If String.IsNullOrEmpty(text) Then Return ""
        Dim sb As New StringBuilder()
        Dim i As Integer = 0
        While i < text.Length
            If text(i) = "§"c AndAlso i + 1 < text.Length Then
                If Char.ToLower(text(i + 1)) = "x"c AndAlso i + 13 < text.Length Then
                    i += 14
                    Continue While
                End If
                i += 2
                Continue While
            End If
            sb.Append(text(i))
            i += 1
        End While
        Return sb.ToString()
    End Function

    ''' <summary>移除文本中指定的单个样式码（不区分大小写）。</summary>
    Public Function RemoveStyleCode(text As String, code As Char) As String
        If String.IsNullOrEmpty(text) Then Return ""
        Dim sb As New StringBuilder()
        Dim i As Integer = 0
        While i < text.Length
            If text(i) = "§"c AndAlso i + 1 < text.Length AndAlso Char.ToLower(text(i + 1)) = Char.ToLower(code) Then
                i += 2
                Continue While
            End If
            sb.Append(text(i))
            i += 1
        End While
        Return sb.ToString()
    End Function

    ''' <summary>统计不含格式码的可见字符数。</summary>
    Public Function CountVisibleChars(text As String) As Integer
        Return StripColorCodes(text).Length
    End Function

    ''' <summary>颜色的最短格式码：经典色用 §0-§f，其余用 §x 十六进制。</summary>
    Public Function ColorCodeFor(c As Color) As String
        Dim Info = ClassicColors.FirstOrDefault(Function(x) x.Color.Equals(c))
        If Info IsNot Nothing Then Return "§" & Info.Code
        Return HexEncode(c)
    End Function

    ''' <summary>构建"§r 之后恢复某状态"所需的格式码序列。ExcludeStyle 用于撤销某一样式。</summary>
    Public Function ResumeCodes(State As McFormatState, Optional ExcludeStyle As Char = Nothing) As String
        Dim sb As New StringBuilder()
        If Not State.Color.Equals(DefaultTextColor) Then sb.Append(ColorCodeFor(State.Color))
        For Each P In {("l"c, State.Bold), ("o"c, State.Italic), ("n"c, State.Underline), ("m"c, State.Strike), ("k"c, State.Obf)}
            If P.Item2 AndAlso P.Item1 <> ExcludeStyle Then sb.Append("§").Append(P.Item1)
        Next
        If sb.Length = 0 Then Return "§r"
        Return sb.ToString()
    End Function

    ''' <summary>尝试解析 #RRGGBB / RRGGBB 颜色。</summary>
    Public Function TryParseHex(hex As String, ByRef color As Color) As Boolean
        If String.IsNullOrWhiteSpace(hex) Then Return False
        Dim s = hex.Trim()
        If s.StartsWith("#") Then s = s.Substring(1)
        If s.Length <> 6 Then Return False
        Dim rgb As Integer = 0
        If Not Integer.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, rgb) Then Return False
        color = Color.FromRgb(CByte((rgb >> 16) And &HFF), CByte((rgb >> 8) And &HFF), CByte(rgb And &HFF))
        Return True
    End Function

    ''' <summary>按目标格式序列化文本。</summary>
    Public Function Serialize(Text As String, Format As McOutputFormat, Optional DefaultColor As Nullable(Of Color) = Nothing) As String
        Dim Def As Color = If(DefaultColor.HasValue, DefaultColor.Value, DefaultTextColor)
        Select Case Format
            Case McOutputFormat.Section
                Return If(Text, "")
            Case McOutputFormat.Amp
                Return If(Text, "").Replace("§"c, "&"c)
            Case McOutputFormat.Json
                Return SerializeJson(Text, Def)
            Case McOutputFormat.MiniMessage
                Return SerializeMiniMessage(Text, Def)
            Case Else
                Return If(Text, "")
        End Select
    End Function

    Private Function SerializeJson(Text As String, Def As Color) As String
        Dim Runs = Parse(Text, Def)
        If Runs.Count = 0 Then Return "{""text"":""""}"
        Dim Parts As New List(Of String)
        For Each R In Runs
            If String.IsNullOrEmpty(R.Text) Then Continue For
            Dim sb As New StringBuilder("{""text"":""" & JsonEscape(R.Text) & """")
            If Not R.Color.Equals(Def) Then
                sb.Append(""",""color"":""#").Append(Hex6(R.Color)).Append("""")
            End If
            If R.Bold Then sb.Append(",""bold"":true")
            If R.Italic Then sb.Append(",""italic"":true")
            If R.Underline Then sb.Append(",""underlined"":true")
            If R.Strike Then sb.Append(",""strikethrough"":true")
            If R.Obfuscated Then sb.Append(",""obfuscated"":true")
            sb.Append("}")
            Parts.Add(sb.ToString())
        Next
        If Parts.Count = 0 Then Return "{""text"":""""}"
        If Parts.Count = 1 Then Return Parts(0)
        Return "[" & String.Join(",", Parts) & "]"
    End Function

    Private Function SerializeMiniMessage(Text As String, Def As Color) As String
        Dim Runs = Parse(Text, Def)
        If Runs.Count = 0 Then Return ""
        Dim sb As New StringBuilder()
        For Each R In Runs
            If String.IsNullOrEmpty(R.Text) Then Continue For
            Dim ColorName As String = Nothing
            Dim Info = ClassicColors.FirstOrDefault(Function(x) x.Color.Equals(R.Color))
            If Info IsNot Nothing Then ColorName = MiniMessageNames(Array.IndexOf(ClassicColors.ToArray, Info))
            Dim Open As New StringBuilder(), Close As New StringBuilder()
            If Not R.Color.Equals(Def) Then
                If ColorName IsNot Nothing Then
                    Open.Append("<").Append(ColorName).Append(">")
                    Close.Insert(0, "</" & ColorName & ">")
                Else
                    Open.Append("<#").Append(Hex6(R.Color)).Append(">")
                    Close.Insert(0, "</#" & Hex6(R.Color) & ">")
                End If
            End If
            If R.Bold Then Open.Append("<bold>") : Close.Insert(0, "</bold>")
            If R.Italic Then Open.Append("<italic>") : Close.Insert(0, "</italic>")
            If R.Underline Then Open.Append("<underlined>") : Close.Insert(0, "</underlined>")
            If R.Strike Then Open.Append("<strikethrough>") : Close.Insert(0, "</strikethrough>")
            If R.Obfuscated Then Open.Append("<obfuscated>") : Close.Insert(0, "</obfuscated>")
            sb.Append(Open.ToString()).Append(R.Text).Append(Close.ToString())
        Next
        Return sb.ToString()
    End Function

    Private Function Hex6(c As Color) As String
        Return c.R.ToString("X2") & c.G.ToString("X2") & c.B.ToString("X2")
    End Function

    Private Function JsonEscape(s As String) As String
        Dim sb As New StringBuilder()
        For Each ch As Char In s
            Select Case ch
                Case "\"c : sb.Append("\\")
                Case """"c : sb.Append("\""")
                Case Microsoft.VisualBasic.ChrW(10) : sb.Append("\n")
                Case Microsoft.VisualBasic.ChrW(13) : '跳过 CR
                Case Microsoft.VisualBasic.ChrW(9) : sb.Append("\t")
                Case "§"c : sb.Append("\u00a7")
                Case Else
                    If AscW(ch) < 32 Then
                        sb.Append("\u").Append(AscW(ch).ToString("X4"))
                    Else
                        sb.Append(ch)
                    End If
            End Select
        Next
        Return sb.ToString()
    End Function

    ''' <summary>生成指定长度的混淆段随机字符（用于模拟 §k 乱码滚动）。</summary>
    Private ReadOnly Rnd As New Random()
    Private Const ObfCharset As String = "▓▒░█▄▀╣║╔╝╚┐└┴┬├─┼#@%&?!*+=0123456789ABCDEFGHXZY"
    Public Function RandomObfChars(length As Integer) As String
        If length <= 0 Then Return ""
        Dim sb As New StringBuilder(length)
        For i As Integer = 1 To length
            sb.Append(ObfCharset(Rnd.Next(ObfCharset.Length)))
        Next
        Return sb.ToString()
    End Function

    ''' <summary>把 Double 安全转换为 0~255 的 Byte（避免算术溢出）。</summary>
    Private Function ToByteSafe(d As Double) As Byte
        If Double.IsNaN(d) OrElse d < 0 Then Return 0
        If d > 255 Then Return 255
        Return CByte(Math.Round(d))
    End Function

    ''' <summary>纯整数颜色插值：i 为位置（0..n），杜绝浮点/CByte 溢出。</summary>
    Public Function LerpColor(a As Color, b As Color, i As Integer, n As Integer) As Color
        Dim denom As Integer = Math.Max(1, n)
        Dim r As Integer = CInt(a.R) + (CInt(b.R) - CInt(a.R)) * i \ denom
        Dim g As Integer = CInt(a.G) + (CInt(b.G) - CInt(a.G)) * i \ denom
        Dim bl As Integer = CInt(a.B) + (CInt(b.B) - CInt(a.B)) * i \ denom
        If r < 0 Then r = 0 Else If r > 255 Then r = 255
        If g < 0 Then g = 0 Else If g > 255 Then g = 255
        If bl < 0 Then bl = 0 Else If bl > 255 Then bl = 255
        Return Color.FromRgb(CByte(r), CByte(g), CByte(bl))
    End Function

    Public Function HexEncode(c As Color) As String
        Return "§x§" & c.R.ToString("X2")(0) & "§" & c.R.ToString("X2")(1) &
               "§" & c.G.ToString("X2")(0) & "§" & c.G.ToString("X2")(1) &
               "§" & c.B.ToString("X2")(0) & "§" & c.B.ToString("X2")(1)
    End Function

    ''' <summary>渐变最大逐字长度，超出部分用结束色整段渲染，避免预览区产生海量 Run 导致卡死。</summary>
    Public Const MaxGradientLength As Integer = 200

    ''' <summary>应用渐变，返回带 §x 十六进制格式码的文本。</summary>
    Public Function ApplyGradient(text As String, start As Color, [end] As Color) As String
        Dim sb As New StringBuilder()
        If String.IsNullOrEmpty(text) Then Return ""
        Dim n As Integer = Math.Min(text.Length, MaxGradientLength)
        For i As Integer = 0 To n - 1
            sb.Append(HexEncode(LerpColor(start, [end], i, n - 1))).Append(text(i))
        Next
        If text.Length > n Then
            sb.Append(HexEncode([end])).Append(text.Substring(n))
        End If
        Return sb.ToString()
    End Function

    Private Function HsvToRgb(h As Double, s As Double, v As Double) As Color
        h = h Mod 360
        If h < 0 Then h += 360
        Dim c = v * s
        Dim x = c * (1 - Math.Abs((h / 60) Mod 2 - 1))
        Dim m = v - c
        Dim r As Double = 0, g As Double = 0, b As Double = 0
        If h < 60 Then
            r = c : g = x
        ElseIf h < 120 Then
            r = x : g = c
        ElseIf h < 180 Then
            g = c : b = x
        ElseIf h < 240 Then
            g = x : b = c
        ElseIf h < 300 Then
            r = x : b = c
        Else
            r = c : b = x
        End If
        Return Color.FromRgb(ToByteSafe((r + m) * 255), ToByteSafe((g + m) * 255), ToByteSafe((b + m) * 255))
    End Function

    ''' <summary>快捷配色：彩虹 / 冰蓝 / 熔岩 / 紫色梦幻 / 金色。</summary>
    Public Function PresetGradient(name As String, text As String) As String
        If String.IsNullOrEmpty(text) Then Return ""
        Select Case name
            Case "彩虹"
                Dim sb As New StringBuilder()
                Dim n As Integer = Math.Min(text.Length, MaxGradientLength)
                For i As Integer = 0 To n - 1
                    sb.Append(HexEncode(HsvToRgb(360 * i / Math.Max(1, n), 1, 1))).Append(text(i))
                Next
                If text.Length > n Then
                    sb.Append(HexEncode(HsvToRgb(360 * (n - 1) / Math.Max(1, n), 1, 1))).Append(text.Substring(n))
                End If
                Return sb.ToString()
            Case "冰蓝" : Return ApplyGradient(text, Color.FromRgb(120, 255, 255), Color.FromRgb(40, 90, 255))
            Case "熔岩" : Return ApplyGradient(text, Color.FromRgb(255, 210, 60), Color.FromRgb(255, 40, 0))
            Case "紫色梦幻" : Return ApplyGradient(text, Color.FromRgb(200, 90, 255), Color.FromRgb(70, 130, 255))
            Case "金色" : Return ApplyGradient(text, Color.FromRgb(255, 240, 150), Color.FromRgb(255, 160, 20))
            Case Else : Return ApplyGradient(text, Color.FromRgb(255, 255, 255), Color.FromRgb(255, 255, 255))
        End Select
    End Function

    ''' <summary>把格式码转换为 RGB/HEX 说明文本（示例）。</summary>
    Public Function ToHexSummary(Text As String) As String
        Dim sb As New StringBuilder()
        For Each R In Parse(Text)
            sb.Append("[").Append(R.Text).Append("] #").Append(R.Color.R.ToString("X2")).Append(R.Color.G.ToString("X2")).Append(R.Color.B.ToString("X2")).Append("  ")
        Next
        Return sb.ToString().Trim()
    End Function

End Module
