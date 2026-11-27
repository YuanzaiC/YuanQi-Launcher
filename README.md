<div align="center">

# 缘启 YuanQi Launcher

**一个好看、也好用的 Minecraft 启动器**

启动、版本、下载、联机、存档、工具，一站搞定

[**⬇️ 下载最新版**](https://github.com/YuanzaiC/YuanQi-Launcher/releases/latest)　・　[官网 · 在线浏览](https://yuanzaic.github.io/YuanQi-Launcher/)　・　[更新日志](https://github.com/YuanzaiC/YuanQi-Launcher/releases)　・　[反馈](https://github.com/YuanzaiC/YuanQi-Launcher/issues)

[![Release](https://img.shields.io/github/v/release/YuanzaiC/YuanQi-Launcher?style=flat-square&label=%E7%89%88%E6%9C%AC&color=3d8a2d)](https://github.com/YuanzaiC/YuanQi-Launcher/releases/latest)
[![Platform](https://img.shields.io/badge/%E5%B9%B3%E5%8F%B0-Windows%2010%20%2B-1f6feb?style=flat-square)](#快速开始)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512bd4?style=flat-square)](#快速开始)
[![Based on](https://img.shields.io/badge/%E5%9F%BA%E4%BA%8E-Plain%20Craft%20Launcher%202.14-1f883d?style=flat-square)](#关于)
[![License](https://img.shields.io/badge/%E8%AE%B8%E5%8F%AF-PCL%20%E5%88%86%E5%8F%91%E6%9C%89%E9%99%90%E8%AE%B8%E5%8F%AF-444444?style=flat-square)](#关于)

</div>

<img src="docs/images/home.png?v=2" alt="缘启主页">

<p align="center"><sub>主页 · 当前实例、存档、最近游玩、游玩日历、游玩洞察与账户一屏掌握</sub></p>

**缘启（YuanQi Launcher，简称 YQL）** 是基于 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) 2.14.0.0 开源版本二次创作的 Minecraft 启动器，面向 Windows 10 及以上，VB.NET / WPF（.NET Framework 4.8）构建。保留 PCL 的启动、下载与版本管理能力，重做界面与配置项，再加上联机、存档修改、体检、种子地图等一整套工具。

## 为什么用它

- **绿色便携** —— 从 2.15.0 起，设置、账户、缓存、日志、临时文件、自动下载的 Java、自定义皮肤、联机模块与它的数据全部收在程序目录的 `YQL` 文件夹里。不写 `%AppData%`，把 `YQL` 一起拷走就等于带走了全部状态。
- **内置联机** —— 陶瓦联机（Terracotta）直接内置，建个房间发邀请码就能一起玩，不需要公网 IP、不需要端口映射。
- **存档能改** —— 种子、模式、难度、规则、出生点、玩家坐标与经验、背包物品都能在启动器里直接改，写入前自动备份。
- **种子地图** —— 本地渲染地形、生物群系与村庄 / 要塞 / 试炼密室等结构，支持三维度，右键按预设格式复制坐标。
- **全屏赏景** —— 一键进入待机观赏页，整屏游戏全景配时间、日期与当前版本，键盘鼠标一动就退出。
- **体检与维护** —— 空间体检、环境体检、久未游玩版本批量归档、多开，该清理的一次清完。

## 快速开始

Windows 10 及以上。Releases 里只有一个文件 `YuanQi-Setup-<版本号>.exe`，**安装与更新都双击它**。

| 场景 | 做法 |
| --- | --- |
| 首次安装 | 双击安装包，默认装到 `%LOCALAPPDATA%\YuanQi Launcher`，安装界面可改路径 |
| 覆盖更新 | 再次双击新版安装包，自动识别安装位置、替换程序文件，`YQL` 与 `.minecraft` 原样保留 |
| 启动器内升级 | 设置 → 关于 → 检查更新 |
| 卸载 | 开始菜单「缘启」、系统「应用和功能」，或安装目录下的 `Uninstall.bat` |

**运行环境**：Windows 10 及以上 · .NET Framework 4.8 · WebView2 运行时 · VC++ 运行库（后两项供内置浏览器与联机模块使用）。

> 安装包未签名，首次运行时 SmartScreen 提示请选「更多信息 → 仍要运行」。联机模块首次使用会自动下载。

## 界面

<table>
<tr>
<td width="50%"><img src="docs/images/link.png?v=2" alt="联机"></td>
<td width="50%"><img src="docs/images/save-world.png?v=2" alt="版本管理"></td>
</tr>
<tr>
<td align="center"><sub>联机 · 一键建房，或粘贴邀请码加入</sub></td>
<td align="center"><sub>版本管理 · 可安装与已归档分开列，占用与压缩比一眼看到</sub></td>
</tr>
<tr>
<td colspan="2"><img src="docs/images/seed-map.png?v=2" alt="种子地图"></td>
</tr>
<tr>
<td colspan="2" align="center"><sub>种子地图 · 主世界地形与远古城市、废弃传送门等结构，带坐标轴与生物群系筛选</sub></td>
</tr>
<tr>
<td colspan="2"><img src="docs/images/immersive.jpg?v=2" alt="全屏待机赏景"></td>
</tr>
<tr>
<td colspan="2" align="center"><sub>全屏待机赏景 · 游戏全景配时间、日期与当前版本，点一下或按 ESC 返回</sub></td>
</tr>
</table>

<details>
<summary>更多截图：下载、存档概览、彩色文字、指令查询、矿石分布、游戏统计</summary>

<table>
<tr>
<td width="50%"><img src="docs/images/download.png?v=2" alt="下载"></td>
<td width="50%"><img src="docs/images/save-overview.png?v=2" alt="存档概览"></td>
</tr>
<tr>
<td align="center"><sub>下载 · 社区资源搜索：来源与类型筛选、下载量与更新时间、来源站一目了然</sub></td>
<td align="center"><sub>存档 · 概览（种子、版本、大小、玩家、维度与所在目录）</sub></td>
</tr>
<tr>
<td><img src="docs/images/tool-color-text.png?v=2" alt="彩色文字"></td>
<td><img src="docs/images/tool-command.png?v=2" alt="指令查询"></td>
</tr>
<tr>
<td align="center"><sub>工具 · 彩色文字：样式、颜色与渐变，右侧实时预览，输出 &amp; 码 / JSON / MiniMessage</sub></td>
<td align="center"><sub>工具 · 指令查询：指令台带候选项，分类浏览，常用指令附示例</sub></td>
</tr>
<tr>
<td><img src="docs/images/tool-ore.png?v=2" alt="矿石分布"></td>
<td><img src="docs/images/tool-stats.png?v=2" alt="游戏统计"></td>
</tr>
<tr>
<td align="center"><sub>工具 · 矿石分布：按 Y 层给出挖掘建议，并标出该层命中的生成规则</sub></td>
<td align="center"><sub>工具 · 游戏统计：总时长、每日曲线、实例排行与时段分布</sub></td>
</tr>
</table>

</details>

## 功能详解

<details>
<summary>展开完整功能清单</summary>

### 启动与版本

- 主页一屏看清当前实例、Java、内存分配与上次游玩
- 多个游戏文件夹，可从「添加文件夹」导入已有 `.minecraft`
- 首次使用可直接导入 PCL / HMCL / 官方启动器的游戏文件夹，可选导入哪些版本，复制后还能把原文件夹移入回收站
- 版本列表显示实际占用，支持归档（深度压缩）与一键解压还原
- 版本设置含概览、启动参数、Mod 管理与配置导出 / 导入
- 游戏运行时启动按钮变为「关闭游戏」，可直接结束本次运行

### 下载

- 原版游戏：正式版 / 预览版 / 远古版 / 愚人节版
- 社区资源：Mod、整合包、数据包、资源包、光影包
- 前置 Mod 自动检测（含前置的前置），可一键一起下载
- 列表按当前实例的版本与加载器筛选；官方源慢时自动切镜像
- 下载源与版本列表源可选，可设线程数与速度上限
- 检查更新也走启动器自己的下载器：进度进任务列表，支持镜像源、分段下载与 sha256 校验

### 联机（陶瓦 / Terracotta）

- 一键创建房间，发邀请码即可加入；无需公网 IP、无需端口映射
- 加入房间在页面内粘贴邀请码，回车即连
- 四步进度轨道显示连接阶段（准备 → 模块 → 房间 → 就绪），失败时附上模块日志
- 默认使用内置公共节点，节点表实时获取，取不到时退回内置
- 显示连接耗时与房间成员，网络较差时提示可能走中继

### 存档查看与修改

- 六个页签：概览 / 世界 / 玩家 / 物品 / 备份 / 数据
- 概览（只读）：种子（可复制、可在种子地图打开）、版本、上次游玩、占用、玩家与维度、目录
- 世界：名称、模式、难度、作弊、常用规则、出生点、世界时间
- 玩家：坐标与经验等级读写，写入前自动备份
- 物品：背包与末影箱，按物品 ID 与数量添加
- 备份：创建 / 恢复 / 导入 / 导出 / 副本，可设保留份数与自动清理
- 数据：NBT 只读查看，统计与进度可重置

### 工具

- 彩色文字生成器：样式、颜色、渐变，输出 `§` / `&` / JSON / MiniMessage，带聊天、MotD、标题预览
- 指令查询：按分类检索，补全直连当前实例版本
- 种子地图：本地渲染地形、生物群系与结构，支持三维度与右键复制坐标
- 另有矿石分布、游戏统计、内存优化、清理垃圾、打开日志文件夹

### 体检与维护

- 空间体检：各版本占用、清理日志与崩溃报告、久未游玩版本批量归档
- 环境体检：.NET、WebView2、VC++ 运行库
- 多开：同时运行多个启动器，联机模块按实例相互独立

### 账户

- 账户池统一管理各类账户，支持微软账户与其他登录方式，按最近使用排序
- 每个账户各自一份头像；全局账户与版本账户分离，左下角固定代表全局账户
- 主页问候语的名字可选：当前实例账户 / 全局账户 / 自定义

### 界面与配置

- 14 套预设主题 + 自定义调色（色相 / 饱和度 / 亮度 / 顶栏渐变）
- 背景：本地或网络图片、自适应方式、不透明度、模糊
- 界面缩放 80% – 150%，支持高 DPI；可隐藏页面与入口；设置可导出、导入

### 更新与缓存

- 一个安装包，安装与更新都双击它，保留设置与存档
- 元数据、版本占用、Mod 信息、账户头像与搜索结果缓存在 `YQL\Cache`（默认 2 天）
- 无广告、无遥测；源码、安装程序与打包脚本全部公开

</details>

## 便携目录 `YQL`

<details>
<summary>展开目录说明与首次启动的自动迁移</summary>

| 路径 | 内容 |
| --- | --- |
| `YQL\Setup.ini` | 全部设置（2.15.0 起从注册表迁入） |
| `YQL\Cache` | 网络缓存（默认 2 天）、版本占用、Mod 信息、搜索结果 |
| `YQL\Runtime` | 自动下载的 Java |
| `YQL\Terracotta` | 联机模块与其数据、`module.log` |
| `YQL\Temp` | 临时文件，每次启动清理 |
| `YQL\CustomSkin.png` | 自定义皮肤 |

首次启动会自动迁移：注册表里的历史设置搬进 `YQL\Setup.ini`，`%AppData%\YQL` 里的账户与缓存搬进程序目录，迁移前留一份 `YQL\RegistryBackup.ini` 备份。唯一还会写注册表的是「使用高性能显卡」（默认关闭）。

</details>

## 命令行参数

<details>
<summary>展开安装包的静默安装与卸载参数</summary>

| 参数 | 作用 |
| --- | --- |
| `/S` | 静默安装，不显示界面 |
| `/D=<路径>` | 指定安装目录（与 `/S` 搭配） |
| `/nolaunch` | 安装完成后不自动启动 |
| `/uninstall` | 卸载（保留用户数据） |
| `/uninstall /removedata /S` | 卸载并删除 `YQL` 与 `.minecraft` |

</details>

## 从源码构建

<details>
<summary>展开构建步骤与项目结构</summary>

```powershell
git clone https://github.com/YuanzaiC/YuanQi-Launcher.git
cd YuanQi-Launcher
msbuild "YuanQi Launcher.sln" /t:Restore
msbuild "YuanQi Launcher.sln" /t:Build /p:Configuration=Release
```

产物在 `YuanQi Launcher\bin\YuanQi Launcher.exe`，需要 Visual Studio（含 .NET Framework 4.8 目标包与 WPF 工作负载）。

| 项目 | 说明 |
| --- | --- |
| `YuanQi Launcher` | 启动器本体（VB.NET / WPF） |
| `YuanQi Setup` | 单文件安装 / 更新程序（VB.NET / WinForms） |
| `MeloongCore` | UI 与动画基础库 |
| `PCLCS` | 启动与安装辅助库 |

打安装包：`powershell -ExecutionPolicy Bypass -File pack-setup.ps1`，可用 `-Version 2.15.0` 指定版本、`-Bump` 末位加一，输出到 `发布\`。

</details>

## 关于

- **与 PCL 的关系**：缘启基于龙腾猫跃 (LTCat) 开发的 **Plain Craft Launcher 2.14.0.0 开源版本**，由第三方独立二次创作，**与 PCL 官方无关**。遵循《PCL 分发有限许可》与《PCL 存储库合理使用指南》，支持 Minecraft 正版。
- **致谢**：龙腾猫跃 (LTCat) 与所有 [PCL 贡献者](https://github.com/Meloong-Git/PCL)、[陶瓦联机 (Terracotta)](https://github.com/burningtnt/Terracotta)、cubiomes。想支持上游工作，欢迎前往 [爱发电](https://meloong.com/afd/a/LTCat) 赞助 PCL 原作者。
- **反馈**：欢迎提交 [Issue](https://github.com/YuanzaiC/YuanQi-Launcher/issues)（问题反馈 / 功能建议），请附上启动器版本号（关于页底部）、操作步骤与最新日志（工具箱 → 打开日志文件夹）。

<div align="center"><sub>缘启 YuanQi Launcher · 由 缘栽 维护</sub></div>
