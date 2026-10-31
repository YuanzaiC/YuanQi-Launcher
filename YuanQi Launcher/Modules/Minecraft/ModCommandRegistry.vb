''' <summary>
''' 指令台候选条目：一个 ID + 中文名 + 英文名 + 拼音首字母。
''' </summary>
Public Class McIdEntry
    Public Property Id As String = ""
    Public Property Name As String = ""
    Public Property NameEn As String = ""
    ''' <summary>中文名的拼音首字母串，用于 zs → 钻石 这种搜索。</summary>
    Public Property Initials As String = ""

    Public Sub New()
    End Sub
    Public Sub New(Id As String, Name As String, Optional NameEn As String = "")
        Me.Id = Id
        Me.Name = Name
        Me.NameEn = NameEn
        Me.Initials = ModPinyin.Initials(Name)
    End Sub
End Class

''' <summary>
''' 指令台的 ID 注册表：优先从「当前实例的 jar + 本地语言文件」读出完整表，
''' 结果缓存到 YQL\Cache\CommandIds\，下次启动直接读缓存；读不到时用内置精简表兜底。
''' </summary>
Public Module ModCommandRegistry

    Public ReadOnly Items As New List(Of McIdEntry)
    Public ReadOnly Blocks As New List(Of McIdEntry)
    Public ReadOnly Entities As New List(Of McIdEntry)
    Public ReadOnly Enchantments As New List(Of McIdEntry)
    Public ReadOnly Effects As New List(Of McIdEntry)
    Public ReadOnly Attributes As New List(Of McIdEntry)
    Public ReadOnly Biomes As New List(Of McIdEntry)
    Public ReadOnly Particles As New List(Of McIdEntry)
    Public ReadOnly GameRules As New List(Of McIdEntry)
    Public ReadOnly Structures As New List(Of McIdEntry)
    Public ReadOnly Sounds As New List(Of McIdEntry)

    ''' <summary>是否已经加载过（含失败后的兜底）。</summary>
    Public IsLoaded As Boolean = False
    ''' <summary>数据来源说明，显示在指令台副标题上。</summary>
    Public SourceText As String = "正在准备候选表…"

    Private Loading As Boolean = False
    Private LoadDone As Boolean = False
    Private Waiters As New List(Of Action)

    ''' <summary>缓存格式版本：结构变化时加一，让旧缓存自动失效。</summary>
    Private Const CacheVersion As Integer = 2

    ''' <summary>缓存目录。</summary>
    Private ReadOnly Property CacheDir As String
        Get
            Return Paths.Base & "YQL\Cache\CommandIds\"
        End Get
    End Property

    ''' <summary>在后台线程准备候选表；已就绪时直接回调。回调在 UI 线程执行。</summary>
    Public Sub EnsureLoadedAsync(OnDone As Action)
        If IsLoaded Then
            RunInUi(OnDone)
            Return
        End If
        SyncLock Waiters
            Waiters.Add(OnDone)
        End SyncLock
        If Loading Then Return
        Loading = True
        RunInNewThread(
        Sub()
            Try
                EnsureLoaded()
            Catch ex As Exception
                Logger.Warn(ex, "准备指令候选表失败")
            Finally
                Dim Calls As List(Of Action) = Nothing
                SyncLock Waiters
                    Calls = New List(Of Action)(Waiters)
                    Waiters.Clear()
                End SyncLock
                For Each C In Calls
                    Try
                        RunInUi(C)
                    Catch ex As Exception
                        Logger.Warn(ex, "指令候选表回调失败")
                    End Try
                Next
            End Try
        End Sub, "Command Registry Load")
    End Sub

    ''' <summary>同步准备候选表（会在调用线程里读文件，必须在后台线程调用）。</summary>
    Public Sub EnsureLoaded()
        If LoadDone Then Return
        SyncLock Items
            If LoadDone Then Return
            Dim Sw As Stopwatch = Stopwatch.StartNew()
            Try
                '① 缓存
                Dim Inst = McInstanceSelected
                Dim InstName As String = If(Inst?.IsLoaded, Inst.Name, "")
                If Not String.IsNullOrEmpty(InstName) AndAlso TryReadCache(InstName) Then
                    LoadDone = True
                    Logger.Info($"指令候选表：读缓存（{Items.Count} 物品 / {Blocks.Count} 方块 / {Entities.Count} 实体，{Sw.ElapsedMilliseconds} ms）")
                    Return
                End If
                '② 从实例读
                If Not String.IsNullOrEmpty(InstName) Then
                    If TryReadFromInstance(Inst) Then
                        LoadDone = True
                        WriteCache(InstName)
                        Logger.Info($"指令候选表：从实例读取（{Items.Count} 物品 / {Blocks.Count} 方块 / {Entities.Count} 实体，{Sw.ElapsedMilliseconds} ms）")
                        Return
                    End If
                End If
                '③ 兜底
                BuildFallback()
                LoadDone = True
                Logger.Warn($"指令候选表：使用内置精简表（{Items.Count} 物品 / {Blocks.Count} 方块，{Sw.ElapsedMilliseconds} ms）")
            Catch ex As Exception
                Logger.Warn(ex, "读取指令候选表失败，改用内置表")
                If Items.Count = 0 Then BuildFallback()
                LoadDone = True
            Finally
                IsLoaded = True
            End Try
        End SyncLock
    End Sub

    '===============================
    '  从实例读取
    '===============================

    Private Function TryReadFromInstance(Inst As McInstance) As Boolean
        Dim McFolder As String = McFolderSelected
        If String.IsNullOrEmpty(McFolder) Then Return False
        Dim LangZh = ReadLang(McFolder, Inst, "zh_cn")
        Dim LangEn = ReadLang(McFolder, Inst, "en_us")
        If LangZh Is Nothing AndAlso LangEn Is Nothing Then Return False
        Dim Zh = If(LangZh, New Dictionary(Of String, String))
        Dim En = If(LangEn, New Dictionary(Of String, String))

        '物品 / 方块：直接读 jar 里的资源清单
        Dim JarPath As String = Inst.PathVersion & Inst.Name & ".jar"
        Dim ItemIds As List(Of String) = Nothing
        Dim BlockIds As List(Of String) = Nothing
        Dim ParticleIds As List(Of String) = Nothing
        Dim StructureIds As List(Of String) = Nothing
        If FileUtils.Exists(JarPath) Then
            Try
                Using Zip As IO.Compression.ZipArchive = IO.Compression.ZipFile.OpenRead(JarPath)
                    ItemIds = New List(Of String)
                    BlockIds = New List(Of String)
                    ParticleIds = New List(Of String)
                    StructureIds = New List(Of String)
                    Dim LegacyItems As New List(Of String)
                    For Each Entry In Zip.Entries
                        Dim P As String = Entry.FullName
                        If P.EndsWith(".json", StringComparison.OrdinalIgnoreCase) Then
                            If P.StartsWith("assets/minecraft/items/", StringComparison.OrdinalIgnoreCase) Then
                                ItemIds.Add(IO.Path.GetFileNameWithoutExtension(P))
                            ElseIf P.StartsWith("assets/minecraft/models/item/", StringComparison.OrdinalIgnoreCase) Then
                                LegacyItems.Add(IO.Path.GetFileNameWithoutExtension(P))
                            ElseIf P.StartsWith("assets/minecraft/blockstates/", StringComparison.OrdinalIgnoreCase) Then
                                BlockIds.Add(IO.Path.GetFileNameWithoutExtension(P))
                            ElseIf P.StartsWith("assets/minecraft/particles/", StringComparison.OrdinalIgnoreCase) Then
                                ParticleIds.Add(IO.Path.GetFileNameWithoutExtension(P))
                            ElseIf P.StartsWith("data/minecraft/worldgen/structure/", StringComparison.OrdinalIgnoreCase) Then
                                StructureIds.Add(IO.Path.GetFileNameWithoutExtension(P))
                            End If
                        End If
                    Next
                    '1.21.4 之前没有 items 目录，退回 models/item
                    If ItemIds.Count = 0 Then ItemIds = LegacyItems
                End Using
            Catch ex As Exception
                Logger.Warn(ex, "读取实例 jar 失败")
            End Try
        End If

        'items 目录里还混着盔甲纹饰这类模型变体，只有语言文件里真有名字的才算真正的物品
        Dim CheckZh As Boolean = Zh.Count > 0
        Dim CheckEn As Boolean = Not CheckZh AndAlso En.Count > 0
        If ItemIds IsNot Nothing Then
            For Each Id In ItemIds
                Dim ZhName = GetLang(Zh, "item.minecraft." & Id)
                If String.IsNullOrEmpty(ZhName) Then ZhName = GetLang(Zh, "block.minecraft." & Id)
                Dim EnName = GetLang(En, "item.minecraft." & Id)
                If String.IsNullOrEmpty(EnName) Then EnName = GetLang(En, "block.minecraft." & Id)
                If CheckZh AndAlso String.IsNullOrEmpty(ZhName) Then Continue For
                If CheckEn AndAlso String.IsNullOrEmpty(EnName) Then Continue For
                Items.Add(New McIdEntry("minecraft:" & Id, If(ZhName, ""), If(EnName, "")))
            Next
        End If
        If BlockIds IsNot Nothing Then
            For Each Id In BlockIds
                Dim ZhName = GetLang(Zh, "block.minecraft." & Id)
                Dim EnName = GetLang(En, "block.minecraft." & Id)
                If CheckZh AndAlso String.IsNullOrEmpty(ZhName) Then Continue For
                If CheckEn AndAlso String.IsNullOrEmpty(EnName) Then Continue For
                Blocks.Add(New McIdEntry("minecraft:" & Id, If(ZhName, ""), If(EnName, "")))
            Next
        End If

        '其余类别：从语言文件的键推导
        CollectByPrefix(Zh, En, "entity.minecraft.", Entities)
        CollectByPrefix(Zh, En, "effect.minecraft.", Effects)
        CollectByPrefix(Zh, En, "enchantment.minecraft.", Enchantments)
        CollectByPrefix(Zh, En, "attribute.minecraft.", Attributes)
        CollectByPrefix(Zh, En, "biome.minecraft.", Biomes)
        CollectByPrefix(Zh, En, "particle.minecraft.", Particles)
        CollectByPrefix(Zh, En, "gamerule.", GameRules, SkipSuffix:=".description")
        CollectByPrefix(Zh, En, "structure.minecraft.", Structures)

        '粒子和结构在语言文件里没有名字，直接从 jar 的资源清单拿（比内置表全得多）
        If ParticleIds IsNot Nothing AndAlso ParticleIds.Count > 0 Then
            Particles.Clear()
            ParticleIds.Sort()
            For Each Id In ParticleIds
                Particles.Add(New McIdEntry(Id, "", ""))
            Next
        End If
        If StructureIds IsNot Nothing AndAlso StructureIds.Count > 0 Then
            Structures.Clear()
            StructureIds.Sort()
            For Each Id In StructureIds
                Structures.Add(New McIdEntry(Id, "", ""))
            Next
        End If

        If Structures.Count = 0 Then BuildFallbackStructures()
        If Sounds.Count = 0 Then BuildFallbackSounds()
        If GameRules.Count = 0 Then BuildFallbackGameRules()

        SourceText = $"来自当前实例「{Inst.Name}」"
        Return Items.Count > 0 OrElse Blocks.Count > 0
    End Function

    ''' <summary>按语言键前缀收集 ID（键后缀就是 ID）。</summary>
    Private Sub CollectByPrefix(Zh As Dictionary(Of String, String), En As Dictionary(Of String, String), Prefix As String,
                                Target As List(Of McIdEntry), Optional SkipSuffix As String = "")
        Dim Keys As New List(Of String)
        For Each K In Zh.Keys
            If K.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) Then Keys.Add(K)
        Next
        If Keys.Count = 0 Then
            For Each K In En.Keys
                If K.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) Then Keys.Add(K)
            Next
        End If
        Keys.Sort()
        For Each K In Keys
            If SkipSuffix.Length > 0 AndAlso K.EndsWith(SkipSuffix, StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim Id As String = K.Substring(Prefix.Length)
            If Id.Length = 0 OrElse Id.Contains(".") Then Continue For
            Dim ZhName = GetLang(Zh, K)
            If String.IsNullOrEmpty(ZhName) Then ZhName = GetLang(En, K)
            Target.Add(New McIdEntry(Id, If(ZhName, ""), If(GetLang(En, K), "")))
        Next
    End Sub

    Private Function GetLang(Lang As Dictionary(Of String, String), Key As String) As String
        Dim V As String = Nothing
        If Lang IsNot Nothing AndAlso Lang.TryGetValue(Key, V) Then Return V
        Return Nothing
    End Function

    ''' <summary>通过 assets 索引找到本地语言文件并解析成字典。</summary>
    Private Function ReadLang(McFolder As String, Inst As McInstance, Locale As String) As Dictionary(Of String, String)
        Try
            Dim AssetsName As String = "legacy"
            Try
                If Inst.JsonObject("assets") IsNot Nothing Then AssetsName = Inst.JsonObject("assets").ToString
            Catch
            End Try
            Dim IndexFile As String = McFolder & "assets\indexes\" & AssetsName & ".json"
            If Not FileUtils.Exists(IndexFile) Then Return Nothing
            Dim Index As JObject = FileUtils.ReadAsString(IndexFile).DeserializeJson()
            If Index Is Nothing Then Return Nothing
            Dim Objs As JObject = TryCast(Index("objects"), JObject)
            If Objs Is Nothing Then Return Nothing
            Dim Key As String = "minecraft/lang/" & Locale & ".json"
            Dim LangNode = Objs(Key)
            If LangNode Is Nothing OrElse LangNode("hash") Is Nothing Then Return Nothing
            Dim Hash As String = LangNode("hash").ToString()
            Dim ObjFile As String = McFolder & "assets\objects\" & Hash.Substring(0, 2) & "\" & Hash
            If Not FileUtils.Exists(ObjFile) Then Return Nothing
            Dim Json As JObject = FileUtils.ReadAsString(ObjFile).DeserializeJson()
            If Json Is Nothing Then Return Nothing
            Dim D As New Dictionary(Of String, String)
            For Each P As JProperty In Json.Properties
                D(P.Name) = P.Value.ToString()
            Next
            Return D
        Catch ex As Exception
            Logger.Warn(ex, $"读取语言文件 {Locale} 失败")
            Return Nothing
        End Try
    End Function

    '===============================
    '  缓存
    '===============================

    Private Function CacheFile(InstName As String) As String
        Dim Safe As String = InstName
        For Each C As Char In IO.Path.GetInvalidFileNameChars()
            Safe = Safe.Replace(C, "_"c)
        Next
        Return CacheDir & Safe & ".json"
    End Function

    Private Function TryReadCache(InstName As String) As Boolean
        Try
            Dim F As String = CacheFile(InstName)
            If Not FileUtils.Exists(F) Then Return False
            Dim Json As JObject = FileUtils.ReadAsString(F).DeserializeJson()
            If Json Is Nothing OrElse Json("Instance") Is Nothing Then Return False
            If Json("Instance").ToString() <> InstName Then Return False
            '缓存格式变了（比如新增了粒子/结构）就让旧缓存失效，重新读一次
            If Json("Ver") Is Nothing OrElse Json("Ver").ToString() <> CacheVersion.ToString() Then Return False
            ReadList(Json, "Items", Items)
            ReadList(Json, "Blocks", Blocks)
            ReadList(Json, "Entities", Entities)
            ReadList(Json, "Effects", Effects)
            ReadList(Json, "Enchantments", Enchantments)
            ReadList(Json, "Attributes", Attributes)
            ReadList(Json, "Biomes", Biomes)
            ReadList(Json, "Particles", Particles)
            ReadList(Json, "GameRules", GameRules)
            ReadList(Json, "Structures", Structures)
            ReadList(Json, "Sounds", Sounds)
            SourceText = $"来自当前实例「{InstName}」"
            If Items.Count = 0 AndAlso Blocks.Count = 0 Then Return False
            If Sounds.Count = 0 Then BuildFallbackSounds()
            Return True
        Catch ex As Exception
            Logger.Warn(ex, "读取指令候选表缓存失败")
            Return False
        End Try
    End Function

    Private Sub ReadList(Json As JObject, Key As String, Target As List(Of McIdEntry))
        Target.Clear()
        Dim Arr = Json(Key)
        If Arr Is Nothing Then Return
        For Each Node In Arr
            Dim One As JArray = TryCast(Node, JArray)
            If One Is Nothing OrElse One.Count < 1 Then Continue For
            Dim Id As String = One(0).ToString()
            Dim Name As String = If(One.Count > 1, One(1).ToString(), "")
            Dim En As String = If(One.Count > 2, One(2).ToString(), "")
            Target.Add(New McIdEntry(Id, Name, En))
        Next
    End Sub

    Private Sub WriteCache(InstName As String)
        Try
            DirectoryUtils.Create(CacheDir)
            Dim Json As New JObject From {
                {"Instance", InstName},
                {"Ver", CacheVersion},
                {"Time", Date.Now.ToString("yyyy-MM-dd HH:mm:ss")}
            }
            WriteList(Json, "Items", Items)
            WriteList(Json, "Blocks", Blocks)
            WriteList(Json, "Entities", Entities)
            WriteList(Json, "Effects", Effects)
            WriteList(Json, "Enchantments", Enchantments)
            WriteList(Json, "Attributes", Attributes)
            WriteList(Json, "Biomes", Biomes)
            WriteList(Json, "Particles", Particles)
            WriteList(Json, "GameRules", GameRules)
            WriteList(Json, "Structures", Structures)
            WriteList(Json, "Sounds", Sounds)
            FileUtils.Write(CacheFile(InstName), Json.ToString(Newtonsoft.Json.Formatting.None))
        Catch ex As Exception
            Logger.Warn(ex, "写入指令候选表缓存失败")
        End Try
    End Sub

    Private Sub WriteList(Json As JObject, Key As String, Source As List(Of McIdEntry))
        Dim Arr As New JArray()
        For Each E In Source
            Arr.Add(New JArray From {E.Id, E.Name, E.NameEn})
        Next
        Json(Key) = Arr
    End Sub

    '===============================
    '  内置兜底表
    '===============================

    Private Sub BuildFallback()
        Items.Clear() : Blocks.Clear() : Entities.Clear() : Effects.Clear() : Enchantments.Clear()
        Attributes.Clear() : Biomes.Clear() : Particles.Clear()
        For Each Id In {"diamond", "diamond_sword", "diamond_pickaxe", "diamond_axe", "diamond_shovel", "diamond_hoe",
                        "diamond_helmet", "diamond_chestplate", "diamond_leggings", "diamond_boots", "diamond_block",
                        "iron_ingot", "iron_sword", "iron_pickaxe", "iron_axe", "iron_helmet", "iron_chestplate",
                        "gold_ingot", "golden_apple", "enchanted_golden_apple", "golden_carrot", "emerald", "coal",
                        "charcoal", "redstone", "lapis_lazuli", "quartz", "amethyst_shard", "copper_ingot",
                        "netherite_ingot", "netherite_sword", "nether_star", "ender_pearl", "ender_eye", "blaze_rod",
                        "blaze_powder", "gunpowder", "stick", "string", "feather", "leather", "paper", "book",
                        "writable_book", "written_book", "bucket", "water_bucket", "lava_bucket", "milk_bucket",
                        "apple", "bread", "cooked_beef", "carrot", "potato", "baked_potato", "wheat", "wheat_seeds",
                        "bone", "bone_meal", "arrow", "bow", "crossbow", "shield", "torch", "lantern", "oak_log",
                        "oak_planks", "cobblestone", "stone", "dirt", "grass_block", "sand", "gravel", "glass",
                        "chest", "crafting_table", "furnace", "anvil", "enchanting_table", "beacon", "tnt",
                        "obsidian", "netherrack", "soul_sand", "glowstone", "sea_lantern", "shulker_box",
                        "ender_chest", "command_block", "spawn_egg"}.ToArray
            Items.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
        For Each Id In {"stone", "cobblestone", "dirt", "grass_block", "sand", "gravel", "oak_planks", "oak_log",
                        "glass", "bricks", "stone_bricks", "obsidian", "netherrack", "glowstone", "iron_block",
                        "gold_block", "diamond_block", "emerald_block", "redstone_block", "lapis_block", "coal_block",
                        "quartz_block", "smooth_stone", "bedrock", "barrier", "command_block", "chest", "torch",
                        "water", "lava", "air", "cobweb", "mossy_cobblestone", "deepslate", "tuff", "calcite",
                        "amethyst_block", "copper_block", "waxed_copper_block"}.ToArray
            Blocks.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
        For Each Id In {"zombie", "skeleton", "creeper", "spider", "enderman", "slime", "witch", "villager",
                        "iron_golem", "snow_golem", "cow", "pig", "sheep", "chicken", "horse", "wolf", "cat",
                        "fox", "bee", "axolotl", "armor_stand", "item", "tnt", "arrow", "fireball", "boat",
                        "minecart", "experience_orb", "lightning_bolt", "ender_dragon", "wither", "warden",
                        "allay", "vex", "phantom", "drowned", "husk", "stray", "pillager", "ravager"}.ToArray
            Entities.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
        For Each Id In {"speed", "slowness", "haste", "mining_fatigue", "strength", "instant_health", "instant_damage",
                        "jump_boost", "nausea", "regeneration", "resistance", "fire_resistance", "water_breathing",
                        "invisibility", "blindness", "night_vision", "hunger", "weakness", "poison", "wither",
                        "health_boost", "absorption", "saturation", "glowing", "levitation", "luck", "slow_falling",
                        "conduit_power", "dolphins_grace", "bad_omen", "hero_of_the_village", "darkness"}.ToArray
            Effects.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
        For Each Id In {"sharpness", "smite", "bane_of_arthropods", "knockback", "fire_aspect", "looting",
                        "sweeping_edge", "efficiency", "silk_touch", "unbreaking", "fortune", "power", "punch",
                        "flame", "infinity", "protection", "fire_protection", "feather_falling", "blast_protection",
                        "projectile_protection", "respiration", "aqua_affinity", "thorns", "depth_strider",
                        "frost_walker", "binding_curse", "vanishing_curse", "loyalty", "impaling", "riptide",
                        "channeling", "multishot", "quick_charge", "piercing", "mending"}.ToArray
            Enchantments.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
        For Each Id In {"max_health", "follow_range", "knockback_resistance", "movement_speed", "flying_speed",
                        "attack_damage", "attack_knockback", "attack_speed", "armor", "armor_toughness",
                        "luck", "scale", "step_height", "gravity", "jump_strength"}.ToArray
            Attributes.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
        For Each Id In {"plains", "forest", "desert", "jungle", "taiga", "savanna", "swamp", "badlands",
                        "cherry_grove", "meadow", "grove", "snowy_slopes", "ocean", "river", "beach", "mushroom_fields",
                        "nether_wastes", "the_end", "deep_dark", "lush_caves", "dripstone_caves"}.ToArray
            Biomes.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
        For Each Id In {"flame", "smoke", "explosion", "happy_villager", "angry_villager", "heart", "note",
                        "crit", "enchant", "portal", "cloud", "dust", "snowflake", "lava", "dripping_water",
                        "item_slime", "totem_of_undying", "witch", "soul", "electric_spark"}.ToArray
            Particles.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
        BuildFallbackStructures()
        BuildFallbackGameRules()
        BuildFallbackSounds()
        SourceText = "内置精简表（没找到实例或语言文件）"
    End Sub

    Private Sub BuildFallbackStructures()
        Structures.Clear()
        For Each Id In {"village_plains", "village_desert", "village_savanna", "village_snowy", "village_taiga",
                        "ancient_city", "buried_treasure", "desert_pyramid", "jungle_temple", "swamp_hut",
                        "igloo", "mansion", "monument", "ocean_ruin_cold", "shipwreck", "pillager_outpost",
                        "stronghold", "mineshaft", "ruined_portal", "nether_fortress", "bastion_remnant",
                        "end_city", "trail_ruins", "trial_chambers"}.ToArray
            Structures.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
    End Sub

    Private Sub BuildFallbackGameRules()
        GameRules.Clear()
        For Each Id In {"keepInventory", "doDaylightCycle", "doWeatherCycle", "doMobSpawning", "doMobLoot",
                        "doTileDrops", "mobGriefing", "naturalRegeneration", "doFireTick", "randomTickSpeed",
                        "showCoordinates", "commandBlockOutput", "sendCommandFeedback", "maxEntityCramming",
                        "playersSleepingPercentage", "doInsomnia", "spawnRadius", "doTraderSpawning"}.ToArray
            GameRules.Add(New McIdEntry(Id, "", Id))
        Next
    End Sub

    Private Sub BuildFallbackSounds()
        Sounds.Clear()
        For Each Id In {"entity.player.levelup", "entity.player.hurt", "entity.player.death", "entity.experience_orb.pickup",
                        "entity.ender_dragon.growl", "entity.creeper.primed", "entity.generic.explode",
                        "block.note_block.pling", "block.chest.open", "block.anvil.use", "block.beacon.activate",
                        "ui.button.click", "ui.toast.challenge_complete", "ambient.cave", "music.game",
                        "weather.rain", "entity.villager.ambient", "entity.wither.spawn", "item.totem.use"}.ToArray
            Sounds.Add(New McIdEntry("minecraft:" & Id, "", Id))
        Next
    End Sub

End Module
