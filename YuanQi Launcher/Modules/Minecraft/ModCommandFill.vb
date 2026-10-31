''' <summary>指令台里一个参数该补全成什么。</summary>
Public Enum FillKind
    None = 0
    Command
    Literal
    Player
    Item
    Block
    Entity
    Enchant
    Effect
    Attribute
    Biome
    Particle
    Sound
    Struct
    GameRule
    Coord
    Number
    Free
    Gamemode
    Difficulty
    Weather
    TimeValue
    YesNo
    Duration
    Level
    Count
    Nbt
    SelectorKey
    Fixed
End Enum

''' <summary>NBT / 组件编辑走到哪一步了。</summary>
Public Enum NbtStep
    None = 0
    ''' <summary>该选组件名 / 属性名了。</summary>
    Key
    ''' <summary>该选附魔了。</summary>
    EnchId
    ''' <summary>该选附魔等级了。</summary>
    EnchLevel
    ''' <summary>一个值写完了：可以继续加，或补右括号收尾。</summary>
    More
    ''' <summary>只差一个右括号。</summary>
    Close
End Enum

''' <summary>语法编译后的一个槽位：字面量或参数。</summary>
Public Class FillSlot
    Public Property IsArg As Boolean = False
    Public Property Text As String = ""
End Class

''' <summary>输入串里的一个词及其位置。</summary>
Public Class TokenSpan
    Public Property Start As Integer = 0
    Public Property Length As Integer = 0
    Public Property Text As String = ""
End Class

''' <summary>当前输入的补全上下文。</summary>
Public Class FillContext
    Public Property Input As String = ""
    Public Property Command As McCommand = Nothing
    Public Property Slots As List(Of FillSlot) = Nothing
    ''' <summary>当前该填第几个槽位；-1 表示已超出语法或无法定位。</summary>
    Public Property SlotIndex As Integer = -1
    Public Property ArgName As String = ""
    Public Property Kind As FillKind = FillKind.None
    ''' <summary>正在输入的词在输入串里的替换区间。</summary>
    Public Property TokenStart As Integer = 0
    Public Property TokenEnd As Integer = 0
    ''' <summary>正在输入的词（可能是空串，表示刚敲完空格）。</summary>
    Public Property Typed As String = ""
    ''' <summary>NBT / 组件编辑当前处在哪一步。</summary>
    Public Property NbtStep As NbtStep = NbtStep.None
    ''' <summary>NBT 编辑用的是组件写法（[]）还是旧版 NBT（{}）。</summary>
    Public Property NbtComponent As Boolean = True
    ''' <summary>当前这段 NBT 原文（从括号开始），用来算还要补几个右括号。</summary>
    Public Property NbtText As String = ""
    ''' <summary>填的时候要不要顺手吃掉前面那个空格（物品后面新敲的 [ / {）。</summary>
    Public Property MergeSpaceBefore As Boolean = False
    ''' <summary>填完不加空格（NBT 里继续往下写）。</summary>
    Public Property NoSpaceAfter As Boolean = False
    ''' <summary>正在选择器 [] 里面（@e[type=…]）。</summary>
    Public Property InSelector As Boolean = False
    ''' <summary>固定候选（选择器里的 limit、sort 之类）。</summary>
    Public Property FixedValues As List(Of FillCandidate) = Nothing
End Class

''' <summary>一条候选。</summary>
Public Class FillCandidate
    ''' <summary>点选后填进去的文本。</summary>
    Public Property Value As String = ""
    ''' <summary>主标题（中文名 / 说明）。</summary>
    Public Property Title As String = ""
    ''' <summary>副标题（ID / 参数说明）。</summary>
    Public Property SubText As String = ""
    ''' <summary>拼音首字母，用于 zw 这类搜索。</summary>
    Public Property Initials As String = ""
    ''' <summary>不管输入什么都要显示的候选（例如选择器的「结束」「继续加条件」）。</summary>
    Public Property AlwaysShow As Boolean = False
    ''' <summary>只能追加到末尾、不覆盖当前值（结束 / 继续加条件这类结构性候选）。</summary>
    Public Property AppendOnly As Boolean = False
    ''' <summary>同分时的排序权重（小的在前），让条件名、组件名保持固定顺序。</summary>
    Public Property Order As Integer = 0
End Class

''' <summary>
''' 指令台的补全引擎：分词 → 定位当前参数 → 生成候选 → 填充。
''' 纯逻辑，不碰界面，方便单独调试。
''' </summary>
Public Module ModCommandFill

    ''' <summary>按名字或别名找指令。</summary>
    Public Function FindCommand(Name As String) As McCommand
        If String.IsNullOrWhiteSpace(Name) Then Return Nothing
        Dim Key As String = Name.Trim().TrimStart("/"c).ToLowerInvariant()
        If Key.Length = 0 Then Return Nothing
        For Each C In ModCommand.Commands
            If C.Name.Equals(Key, StringComparison.OrdinalIgnoreCase) Then Return C
        Next
        '别名必须是完整的一个词才算命中（以前用 IndexOf，"s" 会误命中 seed 的关键词）
        For Each C In ModCommand.Commands
            For Each Word In C.Keywords.Split(New Char() {" "c, "、"c, ","c}, StringSplitOptions.RemoveEmptyEntries)
                If Word.Equals(Key, StringComparison.OrdinalIgnoreCase) Then Return C
            Next
        Next
        Return Nothing
    End Function

    ''' <summary>把输入按空格切开，引号与花括号内的内容算同一个词。</summary>
    Public Function TokenizeSpans(Input As String) As List(Of TokenSpan)
        Dim Out As New List(Of TokenSpan)
        If String.IsNullOrEmpty(Input) Then Return Out
        Dim I As Integer = 0
        While I < Input.Length
            If Input(I) = " "c Then
                I += 1
                Continue While
            End If
            Dim StartIdx As Integer = I
            Dim Depth As Integer = 0
            Dim InQuote As Boolean = False
            While I < Input.Length
                Dim Ch As Char = Input(I)
                If InQuote Then
                    If Ch = """"c Then InQuote = False
                ElseIf Ch = """"c Then
                    InQuote = True
                ElseIf Ch = "{"c Then
                    Depth += 1
                ElseIf Ch = "}"c Then
                    If Depth > 0 Then Depth -= 1
                ElseIf Ch = " "c AndAlso Depth = 0 Then
                    Exit While
                End If
                I += 1
            End While
            Out.Add(New TokenSpan With {
                .Start = StartIdx,
                .Length = I - StartIdx,
                .Text = Input.Substring(StartIdx, I - StartIdx)
            })
        End While
        Return Out
    End Function

    ''' <summary>把一条语法行编译成槽位（{参数} 是参数，其余按空格切成字面量）。</summary>
    Public Function CompileSyntax(Syntax As String) As List(Of FillSlot)
        Dim Out As New List(Of FillSlot)
        If String.IsNullOrEmpty(Syntax) Then Return Out
        Dim Buf As New StringBuilder()
        Dim I As Integer = 0
        Do While I < Syntax.Length
            Dim Ch As Char = Syntax(I)
            If Ch = "{"c Then
                Dim E As Integer = Syntax.IndexOf("}"c, I + 1)
                If E < 0 Then Exit Do
                FlushLiteral(Out, Buf)
                Out.Add(New FillSlot With {.IsArg = True, .Text = Syntax.Substring(I + 1, E - I - 1)})
                I = E + 1
            ElseIf Ch = " "c Then
                FlushLiteral(Out, Buf)
                I += 1
            Else
                Buf.Append(Ch)
                I += 1
            End If
        Loop
        FlushLiteral(Out, Buf)
        Return Out
    End Function

    Private Sub FlushLiteral(Out As List(Of FillSlot), Buf As StringBuilder)
        If Buf.Length = 0 Then Return
        Out.Add(New FillSlot With {.IsArg = False, .Text = Buf.ToString()})
        Buf.Clear()
    End Sub

    ''' <summary>参数名 → 候选类型。</summary>
    Public Function MapKind(ArgName As String) As FillKind
        Select Case ArgName
            Case "目标玩家", "目标", "目的地", "成员", "骑乘者", "载具", "目标容器"
                Return FillKind.Player
            Case "物品"
                Return FillKind.Item
            Case "方块"
                Return FillKind.Block
            Case "实体"
                Return FillKind.Entity
            Case "附魔"
                Return FillKind.Enchant
            Case "效果"
                Return FillKind.Effect
            Case "属性"
                Return FillKind.Attribute
            Case "生物群系"
                Return FillKind.Biome
            Case "粒子"
                Return FillKind.Particle
            Case "音效"
                Return FillKind.Sound
            Case "结构"
                Return FillKind.Struct
            Case "规则名"
                Return FillKind.GameRule
            Case "模式"
                Return FillKind.Gamemode
            Case "难度"
                Return FillKind.Difficulty
            Case "天气"
                Return FillKind.Weather
            Case "时间"
                Return FillKind.TimeValue
            Case "坐标", "起点", "终点", "中心", "目标位置", "x", "y", "z"
                Return FillKind.Coord
            Case "数量"
                Return FillKind.Count
            Case "等级"
                Return FillKind.Level
            Case "持续时间"
                Return FillKind.Duration
            Case "隐藏粒子", "是否同队", "值"
                Return FillKind.YesNo
            Case "数值", "分数", "直径", "刻数", "每秒刻数", "端口", "分钟", "最大值", "最小值", "范围"
                Return FillKind.Number
            Case Else
                Return FillKind.Free
        End Select
    End Function

    ''' <summary>定位"现在该填哪个参数"。</summary>
    Public Function Locate(Input As String) As FillContext
        Dim Ctx As New FillContext With {.Input = If(Input, "")}
        Dim Toks As List(Of TokenSpan) = TokenizeSpans(Ctx.Input)
        If Toks.Count = 0 Then
            Ctx.Kind = FillKind.Command
            Return Ctx
        End If
        Dim CmdName As String = Toks(0).Text
        Dim Cmd As McCommand = FindCommand(CmdName)
        Ctx.Command = Cmd
        '还没敲完指令名
        If Cmd Is Nothing Then
            Ctx.Kind = FillKind.Command
            Ctx.TokenStart = Toks(0).Start
            Ctx.TokenEnd = Toks(0).Start + Toks(0).Length
            Ctx.Typed = Toks(0).Text.TrimStart("/"c)
            Return Ctx
        End If

        Dim AtNew As Boolean = Ctx.Input.EndsWith(" "c)
        Dim TypingIdx As Integer = If(AtNew, Toks.Count, Toks.Count - 1)
        Dim BestSlots As List(Of FillSlot) = Nothing
        Dim BestSlot As Integer = -1
        For Each Line In Cmd.Syntaxes
            Dim Slots As List(Of FillSlot) = CompileSyntax(Line)
            Dim SlotAt As Integer = -1
            If WalkLine(Slots, Toks, TypingIdx, SlotAt) AndAlso SlotAt > BestSlot Then
                BestSlot = SlotAt
                BestSlots = Slots
            End If
        Next
        If BestSlots Is Nothing Then
            Ctx.Kind = FillKind.Free
            Return Ctx
        End If
        Ctx.Slots = BestSlots
        Ctx.SlotIndex = BestSlot
        Dim Slot As FillSlot = BestSlots(BestSlot)
        If Slot.IsArg Then
            Ctx.ArgName = Slot.Text
            Ctx.Kind = MapKind(Slot.Text)
        Else
            Ctx.ArgName = ""
            Ctx.Kind = FillKind.Literal
        End If
        If TypingIdx < Toks.Count Then
            Ctx.TokenStart = Toks(TypingIdx).Start
            Ctx.TokenEnd = Toks(TypingIdx).Start + Toks(TypingIdx).Length
            Ctx.Typed = Toks(TypingIdx).Text
            '物品 / 方块 / 实体后面接 NBT（{}）或组件（[]）时，切到 NBT 补全
            Dim IsSelectorToken As Boolean = (Ctx.Typed.Length > 0 AndAlso Ctx.Typed(0) = "@"c)
            Dim IsIdSlot As Boolean = (Ctx.Kind = FillKind.Item OrElse Ctx.Kind = FillKind.Block OrElse Ctx.Kind = FillKind.Entity)
            Dim NbtStart As Integer = -1
            Dim NbtText As String = ""
            If IsSelectorToken Then
                '以 @ 开头的是选择器，先按选择器处理（@e[ 里的 [ 不是 NBT 组件）
                ApplySelectorContext(Ctx, Ctx.Typed, Ctx.TokenStart)
            ElseIf IsIdSlot Then
                Dim B1 As Integer = Ctx.Typed.IndexOf("{"c)
                Dim B2 As Integer = Ctx.Typed.IndexOf("["c)
                Dim B As Integer = -1
                If B1 >= 0 AndAlso B2 >= 0 Then
                    B = Math.Min(B1, B2)
                Else
                    B = Math.Max(B1, B2)
                End If
                If B > 0 Then
                    '同一个词里：diamond[enchantments=…
                    NbtStart = Toks(TypingIdx).Start + B
                    NbtText = Ctx.Typed.Substring(B)
                End If
            ElseIf Ctx.SlotIndex > 0 AndAlso Ctx.Slots IsNot Nothing Then
                '上一格刚填完物品，这里又新敲了一个以 { 或 [ 开头的词：把它贴到物品后面
                Dim PrevSlot As FillSlot = Ctx.Slots(Ctx.SlotIndex - 1)
                Dim PrevKind As FillKind = If(PrevSlot.IsArg, MapKind(PrevSlot.Text), FillKind.None)
                If PrevKind = FillKind.Item OrElse PrevKind = FillKind.Block OrElse PrevKind = FillKind.Entity Then
                    If Ctx.Typed.Length > 0 AndAlso (Ctx.Typed(0) = "{"c OrElse Ctx.Typed(0) = "["c) Then
                        NbtStart = Toks(TypingIdx).Start
                        NbtText = Ctx.Typed
                        Ctx.MergeSpaceBefore = (NbtStart > 0 AndAlso Ctx.Input(NbtStart - 1) = " "c)
                    End If
                End If
            End If
            If NbtStart >= 0 Then
                ApplyNbtContext(Ctx, NbtText, NbtStart, Toks(TypingIdx).Start + Toks(TypingIdx).Length)
            ElseIf Not IsSelectorToken AndAlso TypingIdx > 0 AndAlso Ctx.Typed.Length > 0 Then
                '容错：@e[ 和 type= 被空格拆成两个词时，仍然按选择器补全
                Dim PrevTok As String = Toks(TypingIdx - 1).Text
                If PrevTok.StartsWith("@", StringComparison.Ordinal) AndAlso PrevTok.Contains("[") AndAlso Not PrevTok.EndsWith("]") Then
                    ApplySelectorTail(Ctx, Ctx.Typed, Ctx.TokenStart)
                End If
            End If
        Else
            Ctx.TokenStart = Ctx.Input.Length
            Ctx.TokenEnd = Ctx.Input.Length
            Ctx.Typed = ""
        End If
        Return Ctx
    End Function

    ''' <summary>把上下文切成「NBT 编辑」：算出当前步骤、可替换区间和用于过滤的半截词。</summary>
    Private Sub ApplyNbtContext(Ctx As FillContext, NbtText As String, NbtStart As Integer, NbtEnd As Integer)
        Ctx.Kind = FillKind.Nbt
        Ctx.ArgName = "NBT"
        Ctx.NoSpaceAfter = True
        Ctx.NbtComponent = (NbtText.Length > 0 AndAlso NbtText(0) = "["c)
        Ctx.NbtText = NbtText
        '先找出最后一个分隔符：它后面是「正在敲的半截词」，前面才是结构
        Dim Cut As Integer = -1
        For I As Integer = 0 To NbtText.Length - 1
            If IsNbtDelimiter(NbtText(I)) Then Cut = I
        Next
        Dim Half As String = If(Cut >= 0, NbtText.Substring(Cut + 1), NbtText)
        Dim CurStep As NbtStep
        If Half.Length > 0 AndAlso Cut >= 0 Then
            '正在敲某个名字：步骤由前面那个分隔符决定
            CurStep = StepAfterDelimiter(NbtText.Substring(0, Cut + 1), Half)
        Else
            CurStep = DetectNbtStep(NbtText)
        End If
        Ctx.NbtStep = CurStep
        If CurStep = NbtStep.None OrElse CurStep = NbtStep.More OrElse CurStep = NbtStep.Close Then
            '这几种情况是「接着往后写」：插在光标处，不替换已有内容
            Ctx.TokenStart = NbtEnd
            Ctx.TokenEnd = NbtEnd
            Ctx.Typed = ""
            Return
        End If
        '其余步骤：替换最后一个分隔符之后的半截词
        Ctx.TokenStart = NbtStart + Cut + 1
        Ctx.TokenEnd = NbtEnd
        Ctx.Typed = Half
    End Sub

    ''' <summary>某个分隔符后面该填什么。</summary>
    Private Function StepAfterDelimiter(T As String, Half As String) As NbtStep
        If T.Length = 0 Then Return NbtStep.Key
        Dim D As Char = T(T.Length - 1)
        Dim Before As String = T.Substring(0, T.Length - 1)
        Select Case D
            Case "{"c
                If Before.Contains("levels:") Then Return NbtStep.EnchId
                Return NbtStep.Close
            Case "["c
                If Before.Contains("Enchantments") Then Return NbtStep.EnchId
                Return NbtStep.Key
            Case ","c
                Return NbtStep.Key
            Case ":"c
                If Before.Contains("levels:") OrElse Before.Contains("lvl:") Then
                    '等级写完（纯数字）就进入「继续加 / 收尾」，否则继续给等级候选
                    If Half.Length > 0 AndAlso IsAllDigits(Half) Then Return NbtStep.More
                    Return NbtStep.EnchLevel
                End If
                Return NbtStep.None
            Case Else
                Return NbtStep.None
        End Select
    End Function

    Private Function IsAllDigits(Text As String) As Boolean
        If String.IsNullOrEmpty(Text) Then Return False
        For Each C As Char In Text
            If Not Char.IsDigit(C) Then Return False
        Next
        Return True
    End Function

    Private Function IsNbtDelimiter(Ch As Char) As Boolean
        Select Case Ch
            Case "["c, "]"c, "{"c, "}"c, ","c, ":"c, "="c, """"c, "'"c, " "c
                Return True
            Case Else
                Return False
        End Select
    End Function

    ''' <summary>判断 NBT / 组件编辑现在走到哪一步。</summary>
    Private Function DetectNbtStep(NbtText As String) As NbtStep
        If String.IsNullOrEmpty(NbtText) Then Return NbtStep.None
        Dim Inner As String = If(NbtText.Length > 1, NbtText.Substring(1), "")
        Dim T As String = Inner.TrimEnd()
        If T.Length = 0 Then Return NbtStep.Key
        Dim Last As Char = T(T.Length - 1)
        If Last = ","c Then Return NbtStep.Key
        If Last = "{"c Then
            If T.Contains("levels:") Then Return NbtStep.EnchId
            Return NbtStep.Close
        End If
        If Last = "["c Then
            If T.Contains("Enchantments") Then Return NbtStep.EnchId
            Return NbtStep.Key
        End If
        If Last = ":"c Then
            If T.Contains("levels:") OrElse T.Contains("lvl:") Then Return NbtStep.EnchLevel
            Return NbtStep.None
        End If
        If Last = "="c Then Return NbtStep.None
        If Last = "}"c OrElse Last = "]"c Then
            If IsNbtBalanced(NbtText) Then Return NbtStep.None
            Return NbtStep.More
        End If
        If Char.IsDigit(Last) OrElse Last = """"c OrElse Last = "'"c Then Return NbtStep.More
        Return NbtStep.None
    End Function

    Private Function IsNbtBalanced(Text As String) As Boolean
        Dim Depth As Integer = 0
        For Each C As Char In Text
            If C = "["c OrElse C = "{"c Then
                Depth += 1
            ElseIf C = "]"c OrElse C = "}"c Then
                Depth -= 1
            End If
        Next
        Return Depth <= 0
    End Function

    ''' <summary>把还没闭合的括号按倒序补出来（[a={b={… → }}]）。</summary>
    Private Function NbtCloseSuffix(Text As String) As String
        Dim Stack As New List(Of Char)
        For Each C As Char In Text
            If C = "["c OrElse C = "{"c Then
                Stack.Add(C)
            ElseIf C = "]"c OrElse C = "}"c Then
                If Stack.Count > 0 Then Stack.RemoveAt(Stack.Count - 1)
            End If
        Next
        Dim Sb As New StringBuilder()
        For I As Integer = Stack.Count - 1 To 0 Step -1
            Sb.Append(If(Stack(I) = "["c, "]"c, "}"c))
        Next
        Return Sb.ToString()
    End Function

    ''' <summary>容错：选择器被空格拆开时（@e[ type=…），当前这个词直接按选择器内容解析。</summary>
    Private Sub ApplySelectorTail(Ctx As FillContext, Typed As String, TokStart As Integer)
        Ctx.InSelector = True
        Ctx.NoSpaceAfter = True
        Dim Eq As Integer = Typed.IndexOf("="c)
        If Eq < 0 Then
            Ctx.Kind = FillKind.SelectorKey
            Ctx.TokenStart = TokStart
            Ctx.TokenEnd = TokStart + Typed.Length
            Ctx.Typed = Typed.Trim()
            Return
        End If
        Dim Key As String = Typed.Substring(0, Eq).Trim().ToLowerInvariant()
        Ctx.ArgName = Key
        Ctx.TokenStart = TokStart + Eq + 1
        Ctx.TokenEnd = TokStart + Typed.Length
        Ctx.Typed = Typed.Substring(Eq + 1).Trim()
        SetSelectorValueKind(Ctx, Key)
    End Sub

    ''' <summary>选择器补全：@e[ → 条件名；@e[type= → 实体列表；limit= 之类给固定值。</summary>
    Private Sub ApplySelectorContext(Ctx As FillContext, Typed As String, TokStart As Integer)
        Ctx.InSelector = True
        Ctx.NoSpaceAfter = True
        '已经用 ] 闭合了，就不要再按选择器处理
        If Typed.EndsWith("]", StringComparison.Ordinal) Then
            Ctx.InSelector = False
            Ctx.NoSpaceAfter = False
            Return
        End If
        Dim Open As Integer = Typed.LastIndexOf("["c)
        If Open < 0 Then
            '还没写 [ ：先给选择器本身和「带条件」的入口
            Ctx.Kind = FillKind.Player
            Ctx.TokenStart = TokStart
            Ctx.TokenEnd = TokStart + Typed.Length
            Ctx.Typed = Typed
            Return
        End If
        Dim SegStart As Integer = Open + 1
        Dim LastComma As Integer = Typed.LastIndexOf(","c)
        If LastComma > Open Then SegStart = LastComma + 1
        Dim Segment As String = Typed.Substring(SegStart)
        Dim Eq As Integer = Segment.IndexOf("="c)
        If Eq < 0 Then
            '该选条件名了
            Ctx.Kind = FillKind.SelectorKey
            Ctx.TokenStart = TokStart + SegStart
            Ctx.TokenEnd = TokStart + Typed.Length
            Ctx.Typed = Segment.Trim()
            Return
        End If
        Dim Key As String = Segment.Substring(0, Eq).Trim().ToLowerInvariant()
        Ctx.TokenStart = TokStart + SegStart + Eq + 1
        Ctx.TokenEnd = TokStart + Typed.Length
        Ctx.Typed = Segment.Substring(Eq + 1).Trim()
        Ctx.ArgName = Key
        SetSelectorValueKind(Ctx, Key)
    End Sub

    ''' <summary>选择器里某个条件的值该给什么候选。</summary>
    Private Sub SetSelectorValueKind(Ctx As FillContext, Key As String)
        Select Case Key
            Case "type"
                Ctx.Kind = FillKind.Entity
            Case "gamemode"
                Ctx.Kind = FillKind.Gamemode
            Case "limit"
                Ctx.Kind = FillKind.Fixed
                Ctx.FixedValues = FixedList("1", "2", "3", "4", "5", "10")
            Case "distance"
                Ctx.Kind = FillKind.Fixed
                Ctx.FixedValues = FixedList("..5", "..10", "..16", "..32", "0..10")
            Case "level"
                Ctx.Kind = FillKind.Fixed
                Ctx.FixedValues = FixedList("0..", "5..", "10..", "..30")
            Case "sort"
                Ctx.Kind = FillKind.Fixed
                Ctx.FixedValues = FixedList("nearest", "furthest", "random", "arbitrary")
            Case "x", "y", "z"
                Ctx.Kind = FillKind.Fixed
                Ctx.FixedValues = FixedList("0", "64", "100")
            Case "dx", "dy", "dz"
                Ctx.Kind = FillKind.Fixed
                Ctx.FixedValues = FixedList("1", "5", "10")
            Case Else
                Ctx.Kind = FillKind.Free
        End Select
    End Sub

    Private Function FixedList(ParamArray Values As String()) As List(Of FillCandidate)
        Dim L As New List(Of FillCandidate)
        For Each V In Values
            L.Add(New FillCandidate With {.Value = V, .Title = V, .SubText = ""})
        Next
        Return L
    End Function

    ''' <summary>坐标类参数会占几个词：坐标 3 个、中心/角度 2 个，其余 1 个。</summary>
    Public Function ArgTokenCount(ArgName As String) As Integer
        Select Case ArgName
            Case "坐标", "起点", "终点", "目标位置"
                Return 3
            Case "中心", "角度"
                Return 2
            Case Else
                Return 1
        End Select
    End Function

    Private Function WalkLine(Slots As List(Of FillSlot), Toks As List(Of TokenSpan), TypingIdx As Integer, ByRef SlotAt As Integer) As Boolean
        SlotAt = -1
        Dim Si As Integer = 0
        Dim Ti As Integer = 0
        While Ti < TypingIdx
            If Si >= Slots.Count Then Return False
            If Slots(Si).IsArg Then
                '坐标类参数吃掉多个词，否则后面全部错位
                Dim Take As Integer = Math.Max(1, Math.Min(ArgTokenCount(Slots(Si).Text), TypingIdx - Ti))
                Ti += Take
                Si += 1
            Else
                '比较时忽略开头的斜杠：输入框里的 / 是独立的前缀标签
                If Not Slots(Si).Text.TrimStart("/"c).Equals(Toks(Ti).Text.TrimStart("/"c), StringComparison.OrdinalIgnoreCase) Then Return False
                Ti += 1
                Si += 1
            End If
        End While
        If Si >= Slots.Count Then Return False
        Dim Typed As String = If(TypingIdx < Toks.Count, Toks(TypingIdx).Text, "")
        If Slots(Si).IsArg Then
            SlotAt = Si
            Return True
        End If
        If Slots(Si).Text.TrimStart("/"c).StartsWith(Typed.TrimStart("/"c), StringComparison.OrdinalIgnoreCase) Then
            SlotAt = Si
            Return True
        End If
        Return False
    End Function

    ''' <summary>生成候选列表（已按输入过滤排序）。</summary>
    Public Function GetCandidates(Ctx As FillContext, Query As String) As List(Of FillCandidate)
        Dim Out As New List(Of FillCandidate)
        If Ctx Is Nothing Then Return Out
        Dim Q As String = If(Query, "")
        Select Case Ctx.Kind
            Case FillKind.Command
                For Each C In ModCommand.Commands
                    Out.Add(New FillCandidate With {.Value = C.Name, .Title = "/" & C.Name, .SubText = C.Description})
                Next
            Case FillKind.Literal
                If Ctx.Command IsNot Nothing Then
                    For Each Line In Ctx.Command.Syntaxes
                        Dim Slots As List(Of FillSlot) = CompileSyntax(Line)
                        If Ctx.SlotIndex >= 0 AndAlso Ctx.SlotIndex < Slots.Count AndAlso Not Slots(Ctx.SlotIndex).IsArg Then
                            AddUniqueCandidate(Out, Slots(Ctx.SlotIndex).Text, "", "")
                        End If
                    Next
                End If
            Case FillKind.Player
                Out.Add(New FillCandidate With {.Value = "@s", .Title = "@s", .SubText = "自己"})
                Out.Add(New FillCandidate With {.Value = "@p", .Title = "@p", .SubText = "最近的玩家"})
                Out.Add(New FillCandidate With {.Value = "@a", .Title = "@a", .SubText = "所有玩家"})
                Out.Add(New FillCandidate With {.Value = "@r", .Title = "@r", .SubText = "随机一名玩家"})
                Out.Add(New FillCandidate With {.Value = "@e", .Title = "@e", .SubText = "所有实体"})
                '带条件的写法只给入口，具体条件（type= / distance= …）由后面的分步补全给出
                Out.Add(New FillCandidate With {.Value = "@e[", .Title = "@e[", .SubText = "筛选实体：type= / distance= / limit= …"})
                Out.Add(New FillCandidate With {.Value = "@a[", .Title = "@a[", .SubText = "筛选玩家：distance= / gamemode= / limit= …"})
                Out.Add(New FillCandidate With {.Value = "@p[", .Title = "@p[", .SubText = "筛选最近的玩家"})
            Case FillKind.Item
                FromEntries(ModCommandRegistry.Items, Out)
            Case FillKind.Block
                FromEntries(ModCommandRegistry.Blocks, Out)
            Case FillKind.Entity
                FromEntries(ModCommandRegistry.Entities, Out)
            Case FillKind.Enchant
                FromEntries(ModCommandRegistry.Enchantments, Out)
            Case FillKind.Effect
                FromEntries(ModCommandRegistry.Effects, Out)
            Case FillKind.Attribute
                If ModCommandRegistry.Attributes.Count > 0 Then
                    FromEntries(ModCommandRegistry.Attributes, Out)
                Else
                    FromPlain(Out, "minecraft:", {"max_health", "movement_speed", "attack_damage", "attack_speed", "armor", "armor_toughness", "knockback_resistance", "follow_range", "scale", "gravity", "step_height"})
                End If
            Case FillKind.Biome
                FromEntries(ModCommandRegistry.Biomes, Out)
            Case FillKind.Particle
                If ModCommandRegistry.Particles.Count > 0 Then
                    FromEntries(ModCommandRegistry.Particles, Out)
                Else
                    FromPlain(Out, "minecraft:", {"flame", "smoke", "explosion", "happy_villager", "heart", "crit", "enchant", "portal", "cloud", "dust", "snowflake", "lava", "soul", "totem_of_undying", "electric_spark"})
                End If
            Case FillKind.Sound
                If ModCommandRegistry.Sounds.Count > 0 Then
                    FromEntries(ModCommandRegistry.Sounds, Out)
                Else
                    FromPlain(Out, "minecraft:", {"entity.player.levelup", "entity.player.hurt", "entity.generic.explode", "ui.button.click", "block.chest.open", "block.anvil.use", "block.note_block.pling", "ambient.cave", "music.game", "weather.rain"})
                End If
            Case FillKind.Struct
                If ModCommandRegistry.Structures.Count > 0 Then
                    FromEntries(ModCommandRegistry.Structures, Out)
                Else
                    FromPlain(Out, "minecraft:", {"village_plains", "ancient_city", "desert_pyramid", "jungle_temple", "swamp_hut", "igloo", "mansion", "monument", "shipwreck", "pillager_outpost", "stronghold", "mineshaft", "nether_fortress", "bastion_remnant", "end_city", "trial_chambers"})
                End If
            Case FillKind.GameRule
                If ModCommandRegistry.GameRules.Count > 0 Then
                    FromEntries(ModCommandRegistry.GameRules, Out)
                Else
                    FromPlain(Out, "", {"keepInventory", "doDaylightCycle", "doWeatherCycle", "doMobSpawning", "doMobLoot", "mobGriefing", "naturalRegeneration", "doFireTick", "randomTickSpeed", "commandBlockOutput", "sendCommandFeedback", "spawnRadius"})
                End If
            Case FillKind.Gamemode
                AddEnum(Out, "survival", "生存模式（简写 s）")
                AddEnum(Out, "creative", "创造模式（简写 c）")
                AddEnum(Out, "adventure", "冒险模式（简写 a）")
                AddEnum(Out, "spectator", "旁观模式（简写 sp）")
            Case FillKind.Difficulty
                AddEnum(Out, "peaceful", "和平")
                AddEnum(Out, "easy", "简单")
                AddEnum(Out, "normal", "普通")
                AddEnum(Out, "hard", "困难")
            Case FillKind.Weather
                AddEnum(Out, "clear", "晴天")
                AddEnum(Out, "rain", "下雨")
                AddEnum(Out, "thunder", "雷雨")
            Case FillKind.TimeValue
                AddEnum(Out, "day", "白天（1000）")
                AddEnum(Out, "noon", "正午（6000）")
                AddEnum(Out, "night", "夜晚（13000）")
                AddEnum(Out, "midnight", "午夜（18000）")
                AddEnum(Out, "0", "清晨（0）")
            Case FillKind.Coord
                If Ctx.ArgName = "中心" OrElse Ctx.ArgName = "角度" Then
                    AddEnum(Out, "~ ~", "当前位置")
                    AddEnum(Out, "0 0", "世界原点")
                    AddEnum(Out, "100 100", "坐标 100,100")
                Else
                    AddEnum(Out, "~ ~ ~", "相对当前位置")
                    AddEnum(Out, "~ ~1 ~", "当前位置上方 1 格")
                    AddEnum(Out, "~ ~-1 ~", "当前位置下方 1 格")
                    AddEnum(Out, "^ ^ ^", "相对朝向（本地坐标）")
                    AddEnum(Out, "0 64 0", "世界原点")
                End If
            Case FillKind.Number
                AddEnum(Out, "1", "1")
                AddEnum(Out, "2", "2")
                AddEnum(Out, "5", "5")
                AddEnum(Out, "10", "10")
                AddEnum(Out, "16", "16")
                AddEnum(Out, "32", "32")
                AddEnum(Out, "64", "64（一组）")
                AddEnum(Out, "100", "100")
                AddEnum(Out, "1000", "1000")
            Case FillKind.YesNo
                AddEnum(Out, "true", "是 / 开启")
                AddEnum(Out, "false", "否 / 关闭")
            Case FillKind.Duration
                AddEnum(Out, "30", "30 秒")
                AddEnum(Out, "60", "1 分钟")
                AddEnum(Out, "120", "2 分钟")
                AddEnum(Out, "300", "5 分钟")
                AddEnum(Out, "600", "10 分钟")
                AddEnum(Out, "3600", "1 小时")
                AddEnum(Out, "infinite", "永久（不会自然结束）")
            Case FillKind.Level
                AddEnum(Out, "0", "I 级")
                AddEnum(Out, "1", "II 级")
                AddEnum(Out, "2", "III 级")
                AddEnum(Out, "3", "IV 级")
                AddEnum(Out, "4", "V 级")
                AddEnum(Out, "5", "VI 级")
                AddEnum(Out, "9", "X 级")
            Case FillKind.Count
                AddEnum(Out, "1", "1 个")
                AddEnum(Out, "2", "2 个")
                AddEnum(Out, "4", "4 个")
                AddEnum(Out, "8", "8 个")
                AddEnum(Out, "16", "16 个")
                AddEnum(Out, "32", "32 个")
                AddEnum(Out, "64", "64 个（一组）")
            Case FillKind.SelectorKey
                AddSelectorKeys(Out)
            Case FillKind.Fixed
                If Ctx.FixedValues IsNot Nothing Then
                    For Each One In Ctx.FixedValues
                        Out.Add(One)
                    Next
                End If
            Case FillKind.Nbt
                AddNbtCandidates(Ctx, Out)
            Case Else
                'Free：没有候选
        End Select
        '在选择器里填值时，始终附带「结束」「继续加条件」，方便收尾
        If Ctx.InSelector AndAlso Ctx.Kind <> FillKind.SelectorKey AndAlso Ctx.Kind <> FillKind.Player Then
            Out.Add(New FillCandidate With {.Value = "]", .Title = "结束", .SubText = "闭合这个选择器", .AlwaysShow = True, .AppendOnly = True})
            Out.Add(New FillCandidate With {.Value = ",", .Title = "继续加条件", .SubText = "再加一个参数", .AlwaysShow = True, .AppendOnly = True})
        End If
        Return FilterCandidates(Out, Q)
    End Function

    ''' <summary>选择器里能用的条件名。</summary>
    Private Sub AddSelectorKeys(Out As List(Of FillCandidate))
        AddSelectorKey(Out, 0, "type", "实体类型，例如 minecraft:zombie")
        AddSelectorKey(Out, 1, "distance", "距离范围，例如 ..10")
        AddSelectorKey(Out, 2, "limit", "最多选几个")
        AddSelectorKey(Out, 3, "sort", "多个目标时的排序方式")
        AddSelectorKey(Out, 4, "gamemode", "游戏模式")
        AddSelectorKey(Out, 5, "level", "经验等级范围")
        AddSelectorKey(Out, 6, "name", "玩家名")
        AddSelectorKey(Out, 7, "team", "队伍名")
        AddSelectorKey(Out, 8, "tag", "实体标签")
        AddSelectorKey(Out, 9, "scores", "计分板条件")
        AddSelectorKey(Out, 10, "nbt", "NBT 条件")
        AddSelectorKey(Out, 11, "x", "绝对 X 坐标")
        AddSelectorKey(Out, 12, "y", "绝对 Y 坐标")
        AddSelectorKey(Out, 13, "z", "绝对 Z 坐标")
        AddSelectorKey(Out, 14, "dx", "X 方向范围")
        AddSelectorKey(Out, 15, "dy", "Y 方向范围")
        AddSelectorKey(Out, 16, "dz", "Z 方向范围")
    End Sub

    Private Sub AddSelectorKey(Out As List(Of FillCandidate), Order As Integer, Key As String, Hint As String)
        Out.Add(New FillCandidate With {.Value = Key & "=", .Title = Key & "=", .SubText = Hint, .Order = Order})
    End Sub

    ''' <summary>NBT / 组件编辑的分步候选：组件名 → 附魔 → 等级 → 继续加 / 收尾。</summary>
    Private Sub AddNbtCandidates(Ctx As FillContext, Out As List(Of FillCandidate))
        Dim Comp As Boolean = Ctx.NbtComponent
        Select Case Ctx.NbtStep
            Case NbtStep.Key
                If Comp Then
                    AddKey(Out, "enchantments={levels:{", "附魔", "选完附魔还能选等级、再加下一个")
                    AddKey(Out, "stored_enchantments={levels:{", "附魔（附魔书）", "附魔书专用")
                    AddKey(Out, "custom_name='{""text"":""""}'", "自定义名称", "支持颜色等 JSON 文本")
                    AddKey(Out, "lore=['{""text"":""""}']", "描述文本", "鼠标悬停时显示的说明")
                    AddKey(Out, "unbreakable={}", "无法破坏", "不会掉耐久")
                    AddKey(Out, "damage=100", "耐久损耗", "已消耗的耐久值")
                    AddKey(Out, "max_stack_size=64", "最大堆叠", "1~99")
                    AddKey(Out, "rarity=""epic""", "稀有度", "common / uncommon / rare / epic")
                    AddKey(Out, "custom_model_data=1", "自定义模型数据", "资源包用来换模型")
                    AddKey(Out, "dyed_color={rgb:16711680}", "染色", "皮革类物品颜色")
                    AddKey(Out, "food={nutrition:4,saturation:2.4}", "食物属性", "可食用次数与回复量")
                    AddKey(Out, "tool={damage_per_block:1,default_mining_speed:1}", "工具属性", "挖掘速度与耐久消耗")
                    AddKey(Out, "can_break={blocks:""minecraft:stone""}", "可破坏方块", "冒险模式也能挖")
                    AddKey(Out, "can_place_on={blocks:""minecraft:stone""}", "可放置方块", "冒险模式也能放")
                    AddKey(Out, "attribute_modifiers={modifiers:[]}", "属性修饰", "改攻击力、移速等")
                Else
                    AddKey(Out, "Enchantments:[", "附魔", "选完附魔还能选等级、再加下一个")
                    AddKey(Out, "display:{Name:'{""text"":""""}'}", "自定义名称", "支持颜色等 JSON 文本")
                    AddKey(Out, "Lore:['{""text"":""""}']", "描述文本", "鼠标悬停时显示的说明")
                    AddKey(Out, "Unbreakable:1b", "无法破坏", "不会掉耐久")
                    AddKey(Out, "Damage:100", "耐久损耗", "已消耗的耐久值")
                    AddKey(Out, "CustomModelData:1", "自定义模型数据", "资源包用来换模型")
                    AddKey(Out, "AttributeModifiers:[", "属性修饰", "改攻击力、移速等")
                    AddKey(Out, "HideFlags:1", "隐藏提示", "隐藏附魔等提示行")
                    AddKey(Out, "NoAI:1b", "不会移动（实体）", "实体 NBT")
                    AddKey(Out, "Silent:1b", "静音（实体）", "实体 NBT")
                    AddKey(Out, "Glowing:1b", "发光（实体）", "实体 NBT")
                    AddKey(Out, "Invulnerable:1b", "无敌（实体）", "实体 NBT")
                    AddKey(Out, "CustomName:'{""text"":""""}'", "自定义名称（实体）", "实体 NBT")
                End If
            Case NbtStep.EnchId
                For Each E In ModCommandRegistry.Enchantments
                    Dim Name As String = If(String.IsNullOrWhiteSpace(E.Name), E.Id.Replace("minecraft:", ""), E.Name)
                    If Comp Then
                        Out.Add(New FillCandidate With {.Value = """" & FullId(E.Id) & """:", .Title = Name, .SubText = E.Id, .Initials = E.Initials})
                    Else
                        Out.Add(New FillCandidate With {.Value = "{id:""" & FullId(E.Id) & """,lvl:", .Title = Name, .SubText = E.Id, .Initials = E.Initials})
                    End If
                Next
            Case NbtStep.EnchLevel
                For I As Integer = 1 To 5
                    AddEnum(Out, I.ToString(), RomanLevel(I) & " 级")
                Next
                '等级写完后可以直接收尾
                Dim CloseLv As String = NbtCloseSuffix(Ctx.NbtText)
                If CloseLv.Length > 0 Then Out.Add(New FillCandidate With {.Value = CloseLv, .Title = "结束", .SubText = "补上 " & CloseLv, .AlwaysShow = True, .AppendOnly = True})
            Case NbtStep.More
                Dim Close As String = NbtCloseSuffix(Ctx.NbtText)
                If Close.Length = 0 Then Close = If(Comp, "]", "}")
                '继续添加下一个组件：先把里层的括号补上，再加逗号，否则新组件会掉进附魔列表里
                Dim MoreValue As String = ","
                If Close.Length > 1 Then MoreValue = Close.Substring(0, Close.Length - 1) & ","
                Out.Add(New FillCandidate With {.Value = MoreValue, .Title = "继续添加", .SubText = If(Comp, "再加一个组件", "再加一个属性"), .AppendOnly = True})
                '正处在附魔列表里时，还能在同一个列表里再加一个附魔
                If Ctx.NbtText.Contains("levels:") AndAlso Close.Length > 1 Then
                    Out.Add(New FillCandidate With {.Value = ",", .Title = "再加一个附魔", .SubText = "在同一个附魔列表里继续加", .AppendOnly = True})
                End If
                Out.Add(New FillCandidate With {.Value = Close, .Title = "结束", .SubText = "补上 " & Close, .AppendOnly = True})
            Case NbtStep.Close
                Out.Add(New FillCandidate With {.Value = "}", .Title = "补上 }", .SubText = "结束这个值", .AppendOnly = True})
                Dim Close2 As String = NbtCloseSuffix(Ctx.NbtText)
                If Close2.Length > 0 Then Out.Add(New FillCandidate With {.Value = Close2, .Title = "结束", .SubText = "补上 " & Close2, .AppendOnly = True})
        End Select
    End Sub

    ''' <summary>补上 minecraft: 命名空间。</summary>
    Private Function FullId(Id As String) As String
        If String.IsNullOrEmpty(Id) Then Return Id
        If Id.Contains(":") Then Return Id
        Return "minecraft:" & Id
    End Function

    Private Sub AddKey(Out As List(Of FillCandidate), Value As String, Title As String, Hint As String)
        Out.Add(New FillCandidate With {.Value = Value, .Title = Title, .SubText = Hint, .Order = Out.Count})
    End Sub

    Private Function RomanLevel(N As Integer) As String
        Select Case N
            Case 1
                Return "I"
            Case 2
                Return "II"
            Case 3
                Return "III"
            Case 4
                Return "IV"
            Case 5
                Return "V"
            Case Else
                Return N.ToString()
        End Select
    End Function

    Private Sub AddEnum(Out As List(Of FillCandidate), Value As String, Hint As String)
        Out.Add(New FillCandidate With {.Value = Value, .Title = Value, .SubText = Hint})
    End Sub

    Private Sub AddUniqueCandidate(Out As List(Of FillCandidate), Value As String, Title As String, SubText As String)
        For Each C In Out
            If C.Value = Value Then Return
        Next
        Out.Add(New FillCandidate With {.Value = Value, .Title = If(Title.Length > 0, Title, Value), .SubText = SubText})
    End Sub

    Private Sub FromPlain(Out As List(Of FillCandidate), Prefix As String, Ids As String())
        For Each Id In Ids
            Out.Add(New FillCandidate With {.Value = Prefix & Id, .Title = Prefix & Id, .SubText = ""})
        Next
    End Sub

    Private Sub FromEntries(Source As List(Of McIdEntry), Out As List(Of FillCandidate))
        If Source Is Nothing Then Return
        For Each E In Source
            Out.Add(New FillCandidate With {
                .Value = E.Id,
                .Title = If(String.IsNullOrWhiteSpace(E.Name), E.Id, E.Name),
                .SubText = E.Id,
                .Initials = E.Initials
            })
        Next
    End Sub

    ''' <summary>按关键词过滤并排序：前缀命中优先，其次是包含命中。</summary>
    Public Function FilterCandidates(Source As List(Of FillCandidate), Query As String) As List(Of FillCandidate)
        Dim Q As String = If(Query, "").Trim().ToLowerInvariant()
        Dim Hits As New List(Of KeyValuePair(Of Integer, FillCandidate))
        For Each C In Source
            If C.AlwaysShow Then
                '「结束」「继续加条件」这类结构性候选不参与过滤，固定排在后面
                Hits.Add(New KeyValuePair(Of Integer, FillCandidate)(90, C))
            Else
                Dim Score As Integer = ScoreCandidate(C, Q)
                If Score >= 0 Then Hits.Add(New KeyValuePair(Of Integer, FillCandidate)(Score, C))
            End If
        Next
        Hits.Sort(AddressOf CompareCandidateHit)
        Dim Out As New List(Of FillCandidate)
        For Each H In Hits
            Out.Add(H.Value)
        Next
        Return Out
    End Function

    Private Function CompareCandidateHit(A As KeyValuePair(Of Integer, FillCandidate), B As KeyValuePair(Of Integer, FillCandidate)) As Integer
        If A.Key <> B.Key Then Return A.Key.CompareTo(B.Key)
        '同分时按候选自带的顺序（条件名、组件名保持固定次序）
        If A.Value.Order <> B.Value.Order Then Return A.Value.Order.CompareTo(B.Value.Order)
        '再同分时短名字优先（钻石 排在 钻石剑 前面）
        Dim LenA As Integer = If(A.Value.Title, "").Length
        Dim LenB As Integer = If(B.Value.Title, "").Length
        If LenA <> LenB Then Return LenA.CompareTo(LenB)
        Return String.Compare(A.Value.Title, B.Value.Title, StringComparison.Ordinal)
    End Function

    ''' <summary>0 前缀命中 / 1 包含命中 / 2 拼音首字母命中 / -1 不命中。</summary>
    Private Function ScoreCandidate(C As FillCandidate, Q As String) As Integer
        If Q.Length = 0 Then Return 1
        Dim Title As String = If(C.Title, "").ToLowerInvariant()
        Dim Value As String = If(C.Value, "").ToLowerInvariant()
        Dim Sub2 As String = If(C.SubText, "").ToLowerInvariant()
        If Title.StartsWith(Q, StringComparison.Ordinal) OrElse Value.StartsWith(Q, StringComparison.Ordinal) Then Return 0
        '忽略命名空间再比一次前缀，输入 dia 时 minecraft:diamond 排在前面
        Dim Bare As String = Value
        If Bare.StartsWith("minecraft:", StringComparison.Ordinal) Then Bare = Bare.Substring(10)
        If Bare.StartsWith(Q, StringComparison.Ordinal) Then Return 0
        If Title.IndexOf(Q, StringComparison.Ordinal) >= 0 Then Return 1
        If Value.IndexOf(Q, StringComparison.Ordinal) >= 0 Then Return 1
        If Not String.IsNullOrEmpty(C.Initials) Then
            '拼音首字母：完全相同 → 前缀命中 → 包含命中（zs 时「钻石」最靠前）
            If C.Initials.Equals(Q, StringComparison.Ordinal) Then Return 2
            If C.Initials.StartsWith(Q, StringComparison.Ordinal) Then Return 3
            If C.Initials.IndexOf(Q, StringComparison.Ordinal) >= 0 Then Return 4
        End If
        If Sub2.IndexOf(Q, StringComparison.Ordinal) >= 0 Then Return 5
        Return -1
    End Function

    ''' <summary>候选是不是「还没写完」：以 [ { = , : 结尾时不补空格，方便接着输入。</summary>
    Private Function EndsOpen(Value As String) As Boolean
        If String.IsNullOrEmpty(Value) Then Return False
        Select Case Value(Value.Length - 1)
            Case "["c, "{"c, "="c, ","c, ":"c
                Return True
            Case Else
                Return False
        End Select
    End Function

    ''' <summary>把候选填进输入串，并在后面补一个空格。</summary>
    Public Function ApplyCandidate(Ctx As FillContext, Value As String, Optional InsertOnly As Boolean = False) As String
        If Ctx Is Nothing Then Return Value
        Dim Text As String = If(Ctx.Input, "")
        Dim Start As Integer
        Dim EndIdx As Integer
        If InsertOnly Then
            '结构性候选只往末尾追加，不覆盖已经填好的值
            Start = Math.Min(Ctx.TokenEnd, Text.Length)
            EndIdx = Start
        Else
            Start = Math.Min(Ctx.TokenStart, Text.Length)
            EndIdx = Math.Min(Ctx.TokenEnd, Text.Length)
        End If
        '物品后面新敲的 [ / { ：顺手把中间那个空格吃掉，贴完就是 diamond[...]
        If Ctx.MergeSpaceBefore AndAlso Start > 0 AndAlso Text(Start - 1) = " "c Then Start -= 1
        Dim After As String = Text.Substring(EndIdx)
        Dim Result As String = Text.Substring(0, Start) & Value & After
        'NBT 里是继续往下写，不补空格
        If Ctx.NoSpaceAfter Then Return Result
        '以 [ { = , : 结尾的候选后面不能补空格，否则会把这个词截断（@e[ + 空格 → type= 就成了新词）
        If EndsOpen(Value) Then Return Result
        '后面紧跟的如果不是空格，就补一个，方便直接接着敲下一个参数
        Dim TailStart As Integer = Start + Value.Length
        If TailStart >= Result.Length Then
            Result &= " "
        ElseIf Result(TailStart) <> " "c Then
            Result = Result.Substring(0, TailStart) & " " & Result.Substring(TailStart)
        End If
        Return Result
    End Function

    ''' <summary>轻量校验：参数个数、坐标格式之类，返回提示文字（空串表示没问题）。</summary>
    Public Function Validate(Input As String, Ctx As FillContext) As String
        If String.IsNullOrWhiteSpace(Input) Then Return ""
        If Ctx Is Nothing OrElse Ctx.Command Is Nothing Then Return ""
        Dim Toks As List(Of TokenSpan) = TokenizeSpans(Input)
        If Toks.Count <= 1 Then Return ""
        '算出这条指令最多能写多少个词（坐标类参数按占多个词计算）
        Dim MaxTokens As Integer = 0
        For Each Line In Ctx.Command.Syntaxes
            Dim Slots As List(Of FillSlot) = CompileSyntax(Line)
            Dim N As Integer = 0
            For Each S In Slots
                N += If(S.IsArg, ArgTokenCount(S.Text), 1)
            Next
            MaxTokens = Math.Max(MaxTokens, N)
        Next
        If MaxTokens > 0 AndAlso Toks.Count > MaxTokens Then
            Return $"参数比语法多（{Ctx.Command.Name} 最多 {MaxTokens} 个词）"
        End If
        Return ""
    End Function

End Module
