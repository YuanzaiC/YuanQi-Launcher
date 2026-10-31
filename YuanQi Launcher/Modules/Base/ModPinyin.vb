''' <summary>
''' 汉字到拼音首字母的对照表：只覆盖 Minecraft 物品 / 方块 / 实体名里会用到的字，
''' 用于指令台的拼音首字母搜索（输入 zs 命中「钻石」）。
''' </summary>
Public Module ModPinyin

    Private ReadOnly Table As Dictionary(Of Char, Char) = BuildTable()

    Private Function BuildTable() As Dictionary(Of Char, Char)
        Dim D As New Dictionary(Of Char, Char)
        Add(D, "a"c, "按安爱矮鞍暗案岸")
        Add(D, "b"c, "卜北兵冰玻波驳斑板绊孢宝豹悲贝比笔闭避边标不布巴绑搏臂蝙保把本柄棒白补被饱鲅靶背包煲爆")
        Add(D, "c"c, "丛传催叉唱垂层巢床成插村橙残沉测潮磁簇粗翅翠船苍草菜葱虫缠车采长锤锄丑冲唇彩出刺川穿炽诚")
        Add(D, "d"c, "丁丹刀凋夺地堆大动带斗打杜的盾蛋豆锭雕锻滴淡洞灯炖袋钓毒蚪蠹鲷冻定岛弹多笛蒂蝶调甸电道钉都")
        Add(D, "e"c, "恶耳恩饵饿额")
        Add(D, "f"c, "发富覆斧方浮粉符筏绯肥腐蝠蜂风附反夫复封峰繁飞锋饭房非费份丰佛福付妇服腹")
        Add(D, "g"c, "光公告嘎感岗工弓怪挂杆果构根格棍瑰瓜甘菇耕膏革骨鬼高龟锅钩罐灌鲑轨诡镐古刮匠击谷竿管糕盖干刚钢哥歌阁个给跟更攻功共贡勾狗姑孤鼓固故寡乖关观官馆惯贯广归规柜贵滚郭过顾")
        Add(D, "h"c, "化回唤嚎坏合幻护河活火灰海混滑狐环瑚画盒红花胡魂骸黄黑核桦缓蝴互华呼和害恢惠横欢氧汇会绘婚或货获户怀换荒毁昏")
        Add(D, "j"c, "举佳僵具剑剪劫基家尖径晶架极机校阱阶镜酵金甲界监茎菌菊荚蕨脚箭结继浆溅激焦鹃鸡捷掘疾眷积精集较饥久交加净军记讲间降节荆巨拣捡检简见件建健渐江将姜郊胶角叫教接街杰洁姐解介巾今紧谨进近禁京经井警静境九酒旧居局剧据卷决绝君骏寄急戟棘锯")
        Add(D, "k"c, "傀克刻可哭库开快捆枯框盔苦葵蝌铠块壳空裤骷亢抗扣宽矿亏困阔昆烤窥")
        Add(D, "l"c, "乐令例儡兰力利卵林楼拉料流浪泪炉炼灵烈狼狸璃砾粒络绿萝莲莱蓝蜡螺骆驴骡鹿龙鳞颅铃链漏裂裸零丽劳冷两亮量类理立连凉梁粮疗列劣临领留六露陆录旅乱轮论罗落洛掠栏笼镰雷陵髅")
        Add(D, "m"c, "名命姆帽墨木末模玫母毛民猫磨谜美绵膜蔓蘑螨脉蜜魔麦锚苗明眸矛茂蛮亡们免密慢摸某梅媒每门蒙梦迷米棉面灭敏鸣摩莫谋亩幕目牧哞沫煤霉马")
        Add(D, "n"c, "凝匿南囊女奶恼弩牛泥溺纳脑黏内农暖耐能拟逆年念娘酿鸟您宁弄奴怒虐诺钮")
        Add(D, "p"c, "品喷坯屏怕拼排泡派皮盆瓶片牌苹蒲频疲脾飘匹平坡迫破盘旁陪培佩配朋批披漂撇贫铺仆普谱朴")
        Add(D, "q"c, "切去奇墙强泣潜曲旗气球鞘青轻犰器锹丘区千前浅枪抢峭巧亲琴勤倾清情晴请穷秋求取全泉缺却雀确裙群鳍")
        Add(D, "r"c, "人刃热日染肉绒饶熔仍容如乳入软瑞弱溶")
        Add(D, "s"c, "三上丝书刷史商士失实守射尸山式手扇拴损收杉死水沙沼溯深湿熟燧索砂石示碎矢生睡绳笋色薯蚀识试送输闪霜颂首饰兽树锁鳃匙摔时赛伤伺傻升双塞始师寿疏率爽顺说司私思斯四松搜速宿素酸随岁孙所扫森术杀束渗烁盛瞬身蛇视")
        Add(D, "t"c, "团土头套兔图毯汤涂炭腾腿胎苔藤甜桶梯铁铜陶驼探提鲀台唾填屠天条跳听停庭通同统突退吞托脱拓投体恸糖豚透")
        Add(D, "w"c, "位卫危外巫武物玩瓦碗纹网涡鹉望胃萎蛙鳚五伪喂温文无舞勿误悟挖挽尾")
        Add(D, "x"c, "下仙像削嗅响啸型心性悬旋星小橡雪靴香须险陷熊熏锈镶玄鳕循袭西限祥细信修向吸咸喜先显现线想消效些写谢新行醒姓幸胸休需许序续宣学寻训迅息峡绣蓄藓稀穴箱")
        Add(D, "y"c, "与云原压友叶圆幽异影悦怨樱药荧荫营萤虞虚衣跃运远野阳钥釉音鱼鱿鹦羊翼羽眼疑疣移芽英螈员园域屿岩引曜有样液涌渔源滨滩漠牙牡狳珀珊洋烟焰夜验郁隐雄玉")
        Add(D, "z"c, "中之作侦制子展帜张指挚掌掷柱栅株栽棕植枝砖砧种竹紫纸织者脂装足追造障赭重针钟钻锥蛛猪珠珍沾滞烛肢蔗蜘诅兆准庄忠折抓择斋窄债寨战丈帐账找召照遮这真诊阵争征整正政症支只汁芝知直职止至志质治致智置终肿众周洲昼朱主煮助住注祝驻专转赚壮状捉桌资姿字自总走租族阻组嘴最罪尊左坐座炸泽咒")
        Return D
    End Function

    Private Sub Add(D As Dictionary(Of Char, Char), Letter As Char, Chars As String)
        For Each C As Char In Chars
            D(C) = Letter
        Next
    End Sub

    ''' <summary>取一段文字的拼音首字母（非汉字直接跳过，英文字母与数字原样保留）。</summary>
    Public Function Initials(Text As String) As String
        If String.IsNullOrEmpty(Text) Then Return ""
        Dim Sb As New StringBuilder()
        For Each C As Char In Text
            Dim L As Char = ChrW(0)
            If Table.TryGetValue(C, L) Then
                Sb.Append(L)
            ElseIf (C >= "a"c AndAlso C <= "z"c) OrElse (C >= "A"c AndAlso C <= "Z"c) OrElse (C >= "0"c AndAlso C <= "9"c) Then
                Sb.Append(Char.ToLowerInvariant(C))
            End If
        Next
        Return Sb.ToString()
    End Function

End Module
