''' <summary>
''' 一条 Minecraft 指令的说明（用于工具箱「指令查询」）。
''' </summary>
Public Class McCommand

    ''' <summary>指令名，不含斜杠。</summary>
    Public Property Name As String = ""
    ''' <summary>分类：玩家 / 世界 / 物品 / 实体 / 数据 / 服务器 / 其他。</summary>
    Public Property Category As String = "其他"
    ''' <summary>所需权限等级：0 任何玩家、2 管理员、3 服主、4 仅控制台。</summary>
    Public Property Level As Integer = 2
    ''' <summary>功能说明。</summary>
    Public Property Description As String = ""
    ''' <summary>语法行；参数用 {名称} 标记。</summary>
    Public Property Syntaxes As New List(Of String)
    ''' <summary>参数名 → 说明。</summary>
    Public Property Args As New Dictionary(Of String, String)
    ''' <summary>示例。</summary>
    Public Property Example As String = ""
    ''' <summary>额外搜索关键词（别名等）。</summary>
    Public Property Keywords As String = ""

End Class

''' <summary>
''' 内置的 Minecraft 指令表（Java 版 1.21 前后的常用指令）。
''' </summary>
Public Module ModCommand

    ''' <summary>全部指令，按分类与常用程度排序。</summary>
    Public ReadOnly Commands As List(Of McCommand) = BuildCommands()

    ''' <summary>所有出现过的分类（保持表内顺序）。</summary>
    Public ReadOnly Categories As List(Of String) = GetCategories()

    Private Function GetCategories() As List(Of String)
        Dim Out As New List(Of String) From {"全部"}
        For Each C In Commands
            If Not Out.Contains(C.Category) Then Out.Add(C.Category)
        Next
        Return Out
    End Function

    ''' <summary>权限等级的显示文字。</summary>
    Public Function LevelText(Level As Integer) As String
        Select Case Level
            Case 0
                Return "任何玩家"
            Case 2
                Return "需要管理员"
            Case 3
                Return "需要服主"
            Case 4
                Return "仅控制台"
            Case Else
                Return $"权限等级 {Level}"
        End Select
    End Function

    Private Function Cmd(Name As String, Category As String, Level As Integer, Description As String,
                         Syntaxes As String(), Args As String(),
                         Optional Example As String = "", Optional Keywords As String = "") As McCommand
        Dim C As New McCommand With {
            .Name = Name,
            .Category = Category,
            .Level = Level,
            .Description = Description,
            .Example = Example,
            .Keywords = Keywords
        }
        If Syntaxes IsNot Nothing Then
            For Each Line In Syntaxes
                C.Syntaxes.Add(Line)
            Next
        End If
        If Args IsNot Nothing Then
            For I = 0 To Args.Length - 2 Step 2
                C.Args(Args(I)) = Args(I + 1)
            Next
        End If
        Return C
    End Function

    Private Function BuildCommands() As List(Of McCommand)
        Dim L As New List(Of McCommand)

        '================ 物品 ================

        L.Add(Cmd("give", "物品", 2, "把指定物品直接放进玩家背包。",
                  {"/give {目标玩家} {物品} {数量}"},
                  {"目标玩家", "给予对象。可用 @s 自己、@a 全部玩家、@p 最近的玩家、@r 随机玩家，也可以写玩家名。",
                   "物品", "物品 ID，例如 minecraft:diamond_sword、minecraft:apple，后面可以跟 NBT 数据。",
                   "数量", "要给予的数量，省略时为 1。"},
                  "/give @s minecraft:diamond 64"))

        L.Add(Cmd("clear", "物品", 2, "清空玩家背包，或只清掉指定物品。",
                  {"/clear {目标玩家} {物品} {最大数量}"},
                  {"目标玩家", "要清理的玩家，@a 表示所有玩家。",
                   "物品", "只清理这一种物品，省略时清空整个背包。",
                   "最大数量", "最多清理多少个，省略时全部清掉。"},
                  "/clear @s minecraft:stone"))

        L.Add(Cmd("enchant", "物品", 2, "给手持物品附加附魔。",
                  {"/enchant {目标玩家} {附魔} {等级}"},
                  {"目标玩家", "手持物品的玩家，通常写 @s。",
                   "附魔", "附魔 ID，例如 sharpness 锋利、protection 保护、mending 经验修补。",
                   "等级", "附魔等级，不能超过该附魔自身的上限。"},
                  "/enchant @s minecraft:sharpness 5"))

        L.Add(Cmd("item", "物品", 2, "替换或修改容器方块、实体背包里的物品（1.17+）。",
                  {"/item replace {目标容器} {槽位} {物品} {数量}"},
                  {"目标容器", "目标容器，例如 block 100 64 0 container.0，或 entity @s armor.head。",
                   "槽位", "容器中的槽位名，例如 container.0 ~ container.26、weapon.mainhand。",
                   "物品", "要放入的物品 ID。",
                   "数量", "放入的数量。"},
                  "/item replace entity @s weapon.mainhand with minecraft:netherite_sword"))

        L.Add(Cmd("loot", "物品", 2, "把战利品表抽出来的结果给玩家、放进容器或丢在地上。",
                  {"/loot give {目标玩家} loot {战利品表}"},
                  {"目标玩家", "接收物品的玩家。",
                   "战利品表", "战利品表 ID，例如 minecraft:chests/simple_dungeon。"},
                  "/loot give @s loot minecraft:chests/simple_dungeon"))

        L.Add(Cmd("recipe", "物品", 2, "给予或收回玩家的合成配方。",
                  {"/recipe give {目标玩家} {配方}"},
                  {"目标玩家", "目标玩家，@a 表示所有玩家。",
                   "配方", "配方 ID，或 * 表示全部配方。"},
                  "/recipe give @s *"))

        '================ 玩家 ================

        L.Add(Cmd("tp", "玩家", 2, "把实体传送到某个坐标，或传送到另一个实体身边。",
                  {"/tp {目标} {x} {y} {z}", "/tp {目标} {目的地}"},
                  {"目标", "被传送的实体，可以写玩家名，也可以用 @s、@p、@a、@e[type=…] 这类选择器。",
                   "目的地", "要传送到的实体，例如另一名玩家或 @e[type=villager,limit=1]。",
                   "x", "目标 X 坐标，用 ~ 表示相对当前位置，^ 表示相对朝向。",
                   "y", "目标 Y 坐标（高度）。",
                   "z", "目标 Z 坐标。"},
                  "/tp @s 100 64 -200", "teleport 传送"))

        L.Add(Cmd("gamemode", "玩家", 2, "切换游戏模式。",
                  {"/gamemode {模式} {目标玩家}"},
                  {"模式", "survival 生存、creative 创造、adventure 冒险、spectator 旁观，也可用简写 s / c / a / sp。",
                   "目标玩家", "要切换的玩家，省略时为自己。"},
                  "/gamemode creative @a"))

        L.Add(Cmd("defaultgamemode", "玩家", 2, "设置新玩家进入世界时的默认游戏模式。",
                  {"/defaultgamemode {模式}"},
                  {"模式", "survival / creative / adventure / spectator。"},
                  "/defaultgamemode survival"))

        L.Add(Cmd("spawnpoint", "玩家", 2, "设置玩家的重生点。",
                  {"/spawnpoint {目标玩家} {坐标} {角度}"},
                  {"目标玩家", "要设置重生点的玩家。",
                   "坐标", "重生位置，省略时为玩家当前位置。",
                   "角度", "重生时面朝的方向，例如 0 0。"},
                  "/spawnpoint @s 0 64 0"))

        L.Add(Cmd("spectate", "玩家", 2, "以旁观者视角观察某个实体。",
                  {"/spectate {目标}"},
                  {"目标", "要观察的实体，省略时退出观察。"},
                  "/spectate Steve"))

        L.Add(Cmd("xp", "玩家", 2, "给予、设置或查询经验值与等级。",
                  {"/xp add {目标玩家} {数量}", "/xp set {目标玩家} {数量}", "/xp query {目标玩家} {查询项}"},
                  {"目标玩家", "目标玩家，@a 表示所有玩家。",
                   "数量", "经验点数；加 L 后缀表示等级，例如 30L。",
                   "查询项", "level 等级 或 points 点数。"},
                  "/xp add @s 30L", "experience 经验"))

        L.Add(Cmd("spreadplayers", "玩家", 2, "把玩家随机分散到一片区域里。",
                  {"/spreadplayers {中心} {扩散距离} {最大范围} {是否同队} {目标}"},
                  {"中心", "区域中心坐标，例如 0 0。",
                   "扩散距离", "玩家之间的最小间隔（格）。",
                   "最大范围", "从中心向外扩散的最大半径。",
                   "是否同队", "true 时同队玩家会被分到一起。",
                   "目标", "要被分散的玩家。"},
                  "/spreadplayers 0 0 10 100 false @a"))

        '================ 世界 ================

        L.Add(Cmd("time", "世界", 2, "查询或修改世界时间。",
                  {"/time set {时间}", "/time add {时间}", "/time query {查询项}"},
                  {"时间", "day 白天、noon 正午、night 夜晚、midnight 午夜，也可以写 0~24000 的刻度值。",
                   "查询项", "daytime 当天时间、day 已过天数、gametime 世界总时间。"},
                  "/time set day"))

        L.Add(Cmd("weather", "世界", 2, "设置当前天气。",
                  {"/weather {天气} {持续时间}"},
                  {"天气", "clear 晴天、rain 下雨、thunder 雷雨。",
                   "持续时间", "持续多少秒，省略时随机（大约 5~15 分钟）。"},
                  "/weather clear 600"))

        L.Add(Cmd("gamerule", "世界", 2, "查看或修改游戏规则。",
                  {"/gamerule {规则名}", "/gamerule {规则名} {值}"},
                  {"规则名", "例如 keepInventory 死亡不掉落、doDaylightCycle 昼夜循环、mobGriefing 生物破坏。",
                   "值", "true / false，或具体数字（视规则而定）。"},
                  "/gamerule keepInventory true"))

        L.Add(Cmd("difficulty", "世界", 2, "设置游戏难度。",
                  {"/difficulty {难度}"},
                  {"难度", "peaceful 和平、easy 简单、normal 普通、hard 困难。"},
                  "/difficulty hard"))

        L.Add(Cmd("seed", "世界", 2, "显示当前世界的种子。",
                  {"/seed"}, Nothing, "/seed", "种子 world seed"))

        L.Add(Cmd("setworldspawn", "世界", 2, "设置世界出生点。",
                  {"/setworldspawn {坐标} {角度}"},
                  {"坐标", "新的出生点坐标，省略时为当前位置。",
                   "角度", "出生时面朝的方向。"},
                  "/setworldspawn 0 64 0"))

        L.Add(Cmd("locate", "世界", 2, "定位最近的指定结构、生物群系或兴趣点。",
                  {"/locate structure {结构}", "/locate biome {生物群系}", "/locate poi {兴趣点}"},
                  {"结构", "结构 ID，例如 minecraft:village_plains、minecraft:ancient_city。",
                   "生物群系", "生物群系 ID，例如 minecraft:cherry_grove。",
                   "兴趣点", "兴趣点类型，例如 minecraft:armorer。"},
                  "/locate structure minecraft:ancient_city"))

        L.Add(Cmd("forceload", "世界", 2, "强制加载或取消加载区块，让远处机器继续运行。",
                  {"/forceload add {起点} {终点}", "/forceload remove {起点} {终点}", "/forceload query"},
                  {"起点", "区块范围的起点（方块坐标）。",
                   "终点", "区块范围的终点（方块坐标）。"},
                  "/forceload add 0 0 512 512"))

        L.Add(Cmd("worldborder", "世界", 2, "设置世界边界的大小与位置。",
                  {"/worldborder set {直径} {时间}"},
                  {"直径", "边界的边长（格）。",
                   "时间", "用多少秒平滑变化到目标大小。"},
                  "/worldborder set 2000 30"))

        L.Add(Cmd("fill", "世界", 2, "用方块填充一块长方体区域。",
                  {"/fill {起点} {终点} {方块} {填充方式}"},
                  {"起点", "区域的一个对角坐标。",
                   "终点", "区域的对角坐标。",
                   "方块", "要填充的方块 ID，例如 minecraft:stone。",
                   "填充方式", "replace 全部替换、keep 只填空气、hollow 挖空内部、outline 只填外壳。"},
                  "/fill ~-5 ~-1 ~-5 ~5 ~-1 ~5 minecraft:stone"))

        L.Add(Cmd("fillbiome", "世界", 2, "修改一块区域的生物群系（1.19.4+）。",
                  {"/fillbiome {起点} {终点} {生物群系}"},
                  {"起点", "区域的一个对角坐标。",
                   "终点", "区域的对角坐标。",
                   "生物群系", "目标生物群系 ID。"},
                  "/fillbiome ~-10 ~-2 ~-10 ~10 ~2 ~10 minecraft:cherry_grove"))

        L.Add(Cmd("setblock", "世界", 2, "在指定位置放置一个方块。",
                  {"/setblock {坐标} {方块} {替换方式}"},
                  {"坐标", "放置位置。",
                   "方块", "方块 ID，可以带方块状态或 NBT。",
                   "替换方式", "replace 覆盖、keep 只在空气处放置、destroy 先破坏再放置。"},
                  "/setblock ~ ~ ~ minecraft:chest"))

        L.Add(Cmd("clone", "世界", 2, "把一块区域整体复制到另一个位置。",
                  {"/clone {起点} {终点} {目标位置} {复制方式}"},
                  {"起点", "源区域的一个对角坐标。",
                   "终点", "源区域的对角坐标。",
                   "目标位置", "目标区域的下角坐标。",
                   "复制方式", "replace 覆盖、masked 只复制非空气、filtered 只复制指定方块。"},
                  "/clone ~-5 ~ ~-5 ~5 ~5 ~5 ~10 ~ ~"))

        L.Add(Cmd("place", "世界", 2, "放置结构、地物或拼图模板。",
                  {"/place structure {结构} {坐标}", "/place feature {地物} {坐标}"},
                  {"结构", "结构 ID，例如 minecraft:village_plains。",
                   "地物", "地物 ID，例如 minecraft:oak_tree。",
                   "坐标", "放置位置。"},
                  "/place structure minecraft:village_plains ~ ~ ~"))

        L.Add(Cmd("particle", "世界", 2, "在指定位置生成粒子效果。",
                  {"/particle {粒子} {坐标} {扩散} {速度} {数量}"},
                  {"粒子", "粒子 ID，例如 minecraft:flame、minecraft:happy_villager。",
                   "坐标", "生成位置。",
                   "扩散", "粒子的散布范围（三个方向）。",
                   "速度", "粒子速度，0 表示静止。",
                   "数量", "生成多少个粒子。"},
                  "/particle minecraft:flame ~ ~1 ~ 0.2 0.2 0.2 0 20"))

        L.Add(Cmd("playsound", "世界", 2, "在指定位置播放音效。",
                  {"/playsound {音效} {音源} {目标玩家} {坐标} {音量} {音调}"},
                  {"音效", "音效 ID，例如 minecraft:entity.player.levelup。",
                   "音源", "master / music / record / weather / block / hostile / neutral / player / ambient / voice。",
                   "目标玩家", "能听到这个音效的玩家。",
                   "坐标", "声音来源位置。",
                   "音量", "音量，超过 1 会扩大可听范围。",
                   "音调", "音调，1 为原调。"},
                  "/playsound minecraft:entity.player.levelup master @a ~ ~ ~ 1 1"))

        L.Add(Cmd("stopsound", "世界", 2, "停止正在播放的音效。",
                  {"/stopsound {目标玩家} {音源} {音效}"},
                  {"目标玩家", "要停止声音的玩家。",
                   "音源", "同上，* 表示全部音源。",
                   "音效", "要停止的音效 ID，省略时停止该音源下所有声音。"},
                  "/stopsound @a master"))

        '================ 实体 ================

        L.Add(Cmd("summon", "实体", 2, "在指定位置生成一个实体。",
                  {"/summon {实体} {坐标} {NBT数据}"},
                  {"实体", "实体 ID，例如 minecraft:zombie、minecraft:armor_stand、minecraft:item。",
                   "坐标", "生成位置，用 ~ 表示相对坐标。",
                   "NBT数据", "可选，实体的 NBT 标签，例如 {NoAI:1b}。"},
                  "/summon minecraft:zombie ~ ~ ~ {NoAI:1b}"))

        L.Add(Cmd("kill", "实体", 2, "清除（杀死）指定实体。",
                  {"/kill {目标}"},
                  {"目标", "要清除的实体，例如 @e[type=zombie]、@a；省略时是自己。"},
                  "/kill @e[type=item]"))

        L.Add(Cmd("effect", "实体", 2, "给予或移除状态效果。",
                  {"/effect give {目标} {效果} {持续时间} {等级} {隐藏粒子}", "/effect clear {目标} {效果}"},
                  {"目标", "接受效果的实体。",
                   "效果", "效果 ID，例如 speed 速度、regeneration 生命恢复、minecraft:night_vision 夜视。",
                   "持续时间", "持续秒数，可以写 30、30s 或 1d，infinite 为永久。",
                   "等级", "效果等级，0 表示 I 级，1 表示 II 级，依次类推。",
                   "隐藏粒子", "true 时不在实体身上显示粒子。"},
                  "/effect give @s minecraft:night_vision 999 0 true"))

        L.Add(Cmd("damage", "实体", 2, "对实体造成指定类型的伤害（1.19.4+）。",
                  {"/damage {目标} {伤害值} {伤害类型}"},
                  {"目标", "受到伤害的实体。",
                   "伤害值", "伤害点数，1 点等于半颗心。",
                   "伤害类型", "例如 minecraft:generic、minecraft:fall、minecraft:on_fire。"},
                  "/damage @s 4 minecraft:fall"))

        L.Add(Cmd("ride", "实体", 2, "让实体骑上载具或离开载具。",
                  {"/ride {骑乘者} mount {载具}", "/ride {骑乘者} dismount"},
                  {"骑乘者", "要骑上去的实体。",
                   "载具", "被骑乘的实体，例如 @e[type=boat,limit=1]。"},
                  "/ride @s mount @e[type=minecraft:boat,limit=1]"))

        L.Add(Cmd("attribute", "实体", 2, "查看或修改实体属性（最大生命值、移动速度等）。",
                  {"/attribute {目标} {属性} get", "/attribute {目标} {属性} base set {数值}"},
                  {"目标", "目标实体，例如 @s、@e[type=zombie,limit=1]。",
                   "属性", "属性 ID，例如 minecraft:generic.max_health、minecraft:generic.movement_speed。",
                   "数值", "要设置的属性数值。"},
                  "/attribute @s minecraft:generic.max_health base set 40"))

        L.Add(Cmd("tag", "实体", 2, "给实体添加或移除标签，方便用选择器批量筛选。",
                  {"/tag {目标} add {标签名}", "/tag {目标} remove {标签名}", "/tag {目标} list"},
                  {"目标", "要操作的实体。",
                   "标签名", "自定义标签名，之后可以用 @e[tag=标签名] 选中它们。"},
                  "/tag @e[type=zombie] add boss"))

        '================ 数据 ================

        L.Add(Cmd("execute", "数据", 2, "以指定身份、位置或条件执行另一条指令，是命令方块和数据包的核心。",
                  {"/execute as {目标} at @s run {指令}", "/execute if block {坐标} {方块} run {指令}"},
                  {"目标", "改变执行者身份的实体，@s 指代它。",
                   "指令", "真正要执行的指令，不含斜杠。",
                   "坐标", "条件判断的方块位置。",
                   "方块", "要判断的方块 ID。"},
                  "/execute as @a at @s run summon minecraft:lightning_bolt ~ ~ ~"))

        L.Add(Cmd("data", "数据", 2, "读取、合并或修改方块、实体、存储中的 NBT 数据。",
                  {"/data get {目标} {路径}", "/data merge {目标} {NBT数据}"},
                  {"目标", "block 坐标 / entity 选择器 / storage 命名空间:名称。",
                   "路径", "NBT 路径，例如 Pos、Inventory[0]。",
                   "NBT数据", "要写入的 NBT 复合标签。"},
                  "/data get entity @s Inventory"))

        L.Add(Cmd("function", "数据", 2, "运行数据包中的函数。",
                  {"/function {函数名}"},
                  {"函数名", "函数 ID，例如 mypack:start，对应 data/mypack/function/start.mcfunction。"},
                  "/function mypack:start"))

        L.Add(Cmd("return", "数据", 2, "从函数中提前返回，并可以带回一个结果值（1.20.2+）。",
                  {"/return {返回值}"},
                  {"返回值", "返回的整数结果，或 fail 表示失败。"},
                  "/return 1"))

        L.Add(Cmd("advancement", "数据", 2, "授予或收回玩家的进度（成就）。",
                  {"/advancement grant {目标玩家} only {进度}"},
                  {"目标玩家", "目标玩家。",
                   "进度", "进度 ID，例如 minecraft:story/mine_stone，* 表示全部。"},
                  "/advancement grant @s only minecraft:story/mine_stone"))

        '================ 计分板 / 聊天 ================

        L.Add(Cmd("scoreboard", "计分板", 2, "创建计分项、修改分数、设置显示位置。",
                  {"/scoreboard objectives add {名称} {准则}", "/scoreboard players set {目标} {名称} {分数}"},
                  {"名称", "计分项名字，例如 kills。",
                   "准则", "计分准则，例如 dummy（只由指令修改）、deathCount、health。",
                   "目标", "要修改分数的实体或虚拟名字。",
                   "分数", "要设置的分数值。"},
                  "/scoreboard objectives add kills deathCount"))

        L.Add(Cmd("trigger", "计分板", 0, "让普通玩家通过指令修改自己的 trigger 计分项。",
                  {"/trigger {名称} {数值}"},
                  {"名称", "用 /scoreboard objectives add 名称 trigger 创建的计分项。",
                   "数值", "要写入的数值。"},
                  "/trigger menu set 1"))

        L.Add(Cmd("team", "计分板", 2, "创建队伍、管理成员与队伍属性。",
                  {"/team add {队伍名}", "/team join {队伍名} {成员}"},
                  {"队伍名", "队伍名称。",
                   "成员", "要加入队伍的实体。"},
                  "/team join red @s"))

        L.Add(Cmd("title", "聊天", 2, "在屏幕中央显示标题、副标题或动作栏文字。",
                  {"/title {目标玩家} title {文本}", "/title {目标玩家} subtitle {文本}", "/title {目标玩家} actionbar {文本}"},
                  {"目标玩家", "看到文字的玩家。",
                   "文本", "要显示的文字，可以是 JSON 文本组件。"},
                  "/title @a title {""text"":""欢迎！""}"))

        L.Add(Cmd("tellraw", "聊天", 2, "向玩家发送一段 JSON 格式的富文本。",
                  {"/tellraw {目标玩家} {JSON文本}"},
                  {"目标玩家", "接收消息的玩家。",
                   "JSON文本", "文本组件 JSON，例如 {""text"":""你好"",""color"":""gold""}。"},
                  "/tellraw @a {""text"":""开服啦"",""color"":""green""}"))

        L.Add(Cmd("say", "聊天", 2, "以 [Server] 的身份向所有玩家广播一句话。",
                  {"/say {消息}"},
                  {"消息", "要广播的内容。"},
                  "/say 服务器将在 5 分钟后重启"))

        L.Add(Cmd("msg", "聊天", 0, "私聊某个玩家。",
                  {"/msg {目标玩家} {消息}"},
                  {"目标玩家", "接收私聊的玩家名。",
                   "消息", "要发送的内容。"},
                  "/msg Steve 你好", "tell w 私聊"))

        L.Add(Cmd("me", "聊天", 0, "以第三人称动作的形式广播一句话。",
                  {"/me {动作}"},
                  {"动作", "描述动作的文字。"},
                  "/me 正在挖矿", "action 动作"))

        L.Add(Cmd("teammsg", "聊天", 0, "只给同队伍的玩家发消息。",
                  {"/teammsg {消息}"},
                  {"消息", "要发送给队友的内容。"},
                  "/teammsg 集合！", "tm"))

        '================ 服务器 ================

        L.Add(Cmd("list", "服务器", 0, "列出当前在线的玩家。",
                  {"/list"}, Nothing, "/list", "在线玩家"))

        L.Add(Cmd("reload", "服务器", 2, "重新加载数据包、战利品表等资源。",
                  {"/reload"}, Nothing, "/reload", "重载"))

        L.Add(Cmd("datapack", "服务器", 3, "启用或禁用数据包。",
                  {"/datapack list", "/datapack enable {数据包}"},
                  {"数据包", "数据包名称，例如 file/mypack.zip。"},
                  "/datapack list"))

        L.Add(Cmd("op", "服务器", 3, "把玩家设为管理员。",
                  {"/op {目标玩家}"},
                  {"目标玩家", "要授权的玩家名。"},
                  "/op Steve", "管理员"))

        L.Add(Cmd("deop", "服务器", 3, "取消玩家的管理员权限。",
                  {"/deop {目标玩家}"},
                  {"目标玩家", "要取消权限的玩家名。"},
                  "/deop Steve"))

        L.Add(Cmd("whitelist", "服务器", 3, "管理服务器白名单。",
                  {"/whitelist add {目标玩家}", "/whitelist remove {目标玩家}", "/whitelist list"},
                  {"目标玩家", "要加入或移出白名单的玩家名。"},
                  "/whitelist add Steve", "白名单"))

        L.Add(Cmd("kick", "服务器", 3, "把玩家踢出服务器。",
                  {"/kick {目标玩家} {原因}"},
                  {"目标玩家", "要被踢出的玩家。",
                   "原因", "显示给玩家的原因，可省略。"},
                  "/kick Steve 挂机太久"))

        L.Add(Cmd("ban", "服务器", 3, "封禁玩家，使其无法再进入服务器。",
                  {"/ban {目标玩家} {原因}"},
                  {"目标玩家", "要封禁的玩家名。",
                   "原因", "封禁原因，可省略。"},
                  "/ban Steve 使用外挂", "封禁"))

        L.Add(Cmd("pardon", "服务器", 3, "解除对某个玩家的封禁。",
                  {"/pardon {目标玩家}"},
                  {"目标玩家", "要解封的玩家名。"},
                  "/pardon Steve", "解封"))

        L.Add(Cmd("banlist", "服务器", 3, "查看封禁列表。",
                  {"/banlist"}, Nothing, "/banlist", "封禁列表"))

        L.Add(Cmd("save-all", "服务器", 4, "立刻把世界写入磁盘。",
                  {"/save-all"}, Nothing, "/save-all", "保存"))

        L.Add(Cmd("stop", "服务器", 4, "保存并关闭服务器。",
                  {"/stop"}, Nothing, "/stop", "关服 关闭服务器"))

        L.Add(Cmd("publish", "服务器", 4, "把单人世界开放到局域网。",
                  {"/publish {端口}"},
                  {"端口", "监听的端口，省略时自动选择。"},
                  "/publish 25565", "局域网"))

        L.Add(Cmd("transfer", "服务器", 3, "把玩家转移到另一台服务器（1.20.5+）。",
                  {"/transfer {主机} {端口}"},
                  {"主机", "目标服务器地址。",
                   "端口", "目标服务器端口。"},
                  "/transfer play.example.com 25565"))

        L.Add(Cmd("tick", "服务器", 3, "冻结、单步或加速游戏刻（1.20.5+）。",
                  {"/tick freeze", "/tick step {刻数}", "/tick rate {每秒刻数}"},
                  {"刻数", "要前进的游戏刻数量。",
                   "每秒刻数", "每秒执行的游戏刻数，默认 20。"},
                  "/tick freeze"))

        L.Add(Cmd("debug", "服务器", 3, "开始或结束性能调试记录。",
                  {"/debug start", "/debug stop"}, Nothing, "/debug start", "性能 调试"))

        '================ 其他 ================

        L.Add(Cmd("help", "其他", 0, "列出所有可用指令，或查看某条指令的用法。",
                  {"/help", "/help {指令名}"},
                  {"指令名", "要查看用法的指令名，例如 give。"},
                  "/help give", "? 帮助"))

        L.Add(Cmd("bossbar", "其他", 2, "创建和管理屏幕顶部的 Boss 血条。",
                  {"/bossbar add {名称} {显示文字}", "/bossbar set {名称} value {数值}"},
                  {"名称", "血条 ID，例如 mypack:boss。",
                   "显示文字", "血条上显示的文字，可省略。",
                   "数值", "当前值（0~最大值）。"},
                  "/bossbar add mypack:boss {""text"":""世界 Boss""}"))

        L.Add(Cmd("random", "其他", 2, "生成随机数，或从一组值里随机抽取。",
                  {"/random value {最小值}..{最大值}"},
                  {"最小值", "随机范围的下界。",
                   "最大值", "随机范围的上界。"},
                  "/random value 1..100"))

        L.Add(Cmd("setidletimeout", "其他", 3, "设置挂机多久后踢出玩家。",
                  {"/setidletimeout {分钟}"},
                  {"分钟", "允许的挂机分钟数，0 表示从不踢出。"},
                  "/setidletimeout 30"))

        Return L
    End Function

End Module
