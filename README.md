<div align="center">

# 缘启 YuanQi Launcher

**一个好看、也好用的 Minecraft 启动器**

启动 · 版本 · 下载 · 联机 · 工具，一站搞定

[![Platform](https://img.shields.io/badge/%E5%B9%B3%E5%8F%B0-Windows%2010%20%2B-1f6feb?style=flat-square)](#快速开始)  
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512bd4?style=flat-square)](#从源码构建)  
[![Language](https://img.shields.io/badge/VB.NET-WPF-3b6ea5?style=flat-square)](#从源码构建)  
[![Based on](https://img.shields.io/badge/%E5%9F%BA%E4%BA%8E-Plain%20Craft%20Launcher%202.14-1f883d?style=flat-square)](#许可与致谢)  
[![License](https://img.shields.io/badge/%E8%AE%B8%E5%8F%AF-PCL%20%E5%88%86%E5%8F%91%E6%9C%89%E9%99%90%E8%AE%B8%E5%8F%AF-444444?style=flat-square)](#许可与致谢)

[功能](#功能) · [截图](#截图) · [快速开始](#快速开始) · [从源码构建](#从源码构建) · [许可与致谢](#许可与致谢)

</div>

<img src="docs/images/home.png" alt="缘启主页">

<p align="center"><sub>主页 · 当前实例、存档、最近游玩、游玩日历与账号一屏掌握</sub></p>

## 简介

**缘启（YuanQi Launcher，简称 YQL）** 是基于 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) 2.14 开源版本二次创作的 Minecraft 启动器，面向 Windows 10 及以上，使用 VB.NET / WPF（.NET Framework 4.8）构建。

它在保留 PCL 成熟的启动、下载与版本管理能力的基础上，重做了界面与交互，并补齐了一批实用能力：**内置陶瓦联机、版本归档压缩、空间与环境体检、多开、界面缩放、种子地图与工具集**。

## 功能

<table>
<tr><td width="50%" valign="top">

**🚀 启动与版本**
- 主页一屏看清当前实例、Java、内存分配与上次游玩
- 多个游戏文件夹，可从「添加文件夹」直接导入已有 `.minecraft`
- 版本列表显示**实际占用大小**，支持**归档（深度压缩）**与一键解压还原
- 版本设置内含概览、启动参数、Mod 管理与配置导出；存档可在主页直接查看与打开

</td><td width="50%" valign="top">

**📦 下载**
- 原版游戏：正式版 / 预览版 / 远古版 / 愚人节版
- 社区资源：Mod、整合包、数据包、资源包、光影包
- 下载源与版本列表源可选，支持线程数、速度限制
- 内置下载详情页，实时查看速度、剩余文件与线程占用

</td></tr>
<tr><td valign="top">

**🌐 联机（陶瓦 / Terracotta）**
- 一键创建房间，把邀请码发给朋友即可加入，**无需公网 IP、无需端口映射**
- 自动准备与更新联机模块，默认使用内置节点，通常无需任何手动配置
- 显示连接耗时、房间成员角色，网络较差时提示可能正在走中继
- 支持填写显示名称与自定义公共节点

</td><td valign="top">

**🧰 工具**
- **彩色文字生成器**：样式、颜色、渐变，输出 `§` 码 / `&` 码 / JSON / MiniMessage，带聊天、MotD、标题实时预览
- **指令查询**：按分类检索指令与参数，补全提示直连当前实例版本
- **种子地图**：输入种子即看地形、生物群系与结构分布，支持主世界 / 下界 / 末地
- 另有矿石分布、游戏统计、内存优化、清理垃圾、打开日志文件夹

</td></tr>
<tr><td valign="top">

**🩺 体检与维护**
- **空间体检**：统计各版本占用、清理日志与崩溃报告、列出久未游玩的版本并支持批量归档
- **环境体检**：检查 .NET、WebView2、VC++ 运行库与依赖是否齐全
- **多开**：允许同时运行多个启动器实例，每个实例的联机模块相互独立

</td><td valign="top">

**🎨 界面与配置**
- 多套内置主题，另有自定义 HSL 调色（色相 / 饱和度 / 亮度 / 渐变）
- 背景图片：自适应方式、不透明度、模糊、本地或网络图片
- **界面缩放 80% – 150%**，小屏与高 DPI 都能排得舒服
- 隐藏不需要的页面与入口；设置可导出、导入，链接可选择在启动器内或系统浏览器打开

</td></tr>
</table>

## 截图

<table>
<tr>
<td width="50%"><img src="docs/images/download.png" alt="下载"></td>
<td width="50%"><img src="docs/images/link.png" alt="联机"></td>
</tr>
<tr>
<td align="center"><sub>下载 · 原版游戏与社区资源，正式版 / 预览版 / 远古版 / 愚人节版</sub></td>
<td align="center"><sub>联机 · 创建房间拿邀请码，或输入邀请码加入</sub></td>
</tr>
<tr>
<td colspan="2"><img src="docs/images/seed-map.png" alt="种子地图"></td>
</tr>
<tr>
<td colspan="2" align="center"><sub>种子地图 · 输入种子即可查看地形、生物群系与村庄 / 要塞 / 试炼密室等结构分布</sub></td>
</tr>
<tr>
<td width="50%"><img src="docs/images/tool-color-text.png" alt="彩色文字"></td>
<td width="50%"><img src="docs/images/tool-command.png" alt="指令查询"></td>
</tr>
<tr>
<td align="center"><sub>工具 · 彩色文字生成器，支持 § / &amp; / JSON / MiniMessage</sub></td>
<td align="center"><sub>工具 · 指令查询，按分类检索并按当前版本补全</sub></td>
</tr>
</table>

## 快速开始

**Releases 里只有一个文件：`YuanQi-Setup-<版本号>.exe`。** 双击即可完成安装或更新。

- **首次安装**：默认装到 `%LOCALAPPDATA%\YuanQi Launcher`，安装界面可以改路径
- **已有版本**：安装程序会自动识别已安装位置，只替换程序文件，`YQL`（设置、缓存、日志）与 `.minecraft`（游戏与存档）原样保留
- 安装时会自动关闭正在运行的缘启，装完可选择立即启动
- 卸载：开始菜单里的「缘启」文件夹，或系统「应用和功能」中的「缘启 YuanQi Launcher」，也可以直接运行安装目录下的 `Uninstall.bat`

命令行参数（可选）：

```powershell
YuanQi-Setup-2.14.0.0.exe /S /D="D:\缘启"      # 静默安装到指定目录
YuanQi-Setup-2.14.0.0.exe /uninstall            # 卸载（保留用户数据）
YuanQi-Setup-2.14.0.0.exe /uninstall /removedata /S   # 卸载并删除用户数据
```

首次启动会做一次环境体检，若缺少运行库会给出提示。若想获得完整体验，请确保：

| 依赖 | 说明 |
| --- | --- |
| Windows 10 及以上 | 陶瓦联机模块要求 |
| .NET Framework 4.8 | 启动器本身 |
| WebView2 运行时 | 内置浏览器、部分动画 |
| VC++ 运行库 | 陶瓦联机模块 |

## 从源码构建

```powershell
# 1. 拉取源码（MeloongCore 是子模块，必须带上 --recurse-submodules）
git clone --recurse-submodules <仓库地址>
cd YuanQi-Launcher

# 2. 用 Visual Studio 打开 YuanQi Launcher.sln 直接构建
#    或使用 MSBuild：
msbuild "YuanQi Launcher\YuanQi Launcher.vbproj" /t:Build /p:Configuration=Release
```

产物位于 `YuanQi Launcher\bin\YuanQi Launcher.exe`。

### 打安装包

发布用的单文件安装包由 `pack-setup.ps1` 生成（双击 `生成安装包.bat` 也可以）：

```powershell
powershell -ExecutionPolicy Bypass -File pack-setup.ps1
```

脚本会依次：编译启动器 → 收集程序文件（根目录 exe/dll/config/xml + `Terracotta`、`Videos`，不含 `YQL` 与 `.minecraft`）→ 编译安装程序 → 把压缩包追加到安装程序末尾，输出 `发布\YuanQi-Setup-<版本>.exe`。

安装程序本身位于 `YuanQi Setup\`（VB.NET WinForms），因此 Releases 只需要上传这一个 exe。

- 需要 Visual Studio（含 **.NET Framework 4.8** 目标包与 WPF 工作负载）
- 解决方案内还包含 `MeloongCore`（UI 与动画基础库）、`PCLCS`（C# 编写的启动与安装辅助库）、`cubiomes-src`（种子地图）
- 修改 XAML 后若界面未更新，请确认构建的是 `YuanQi Launcher` 项目而非整个解决方案的缓存

## 与 PCL 的关系

缘启是基于龙腾猫跃 (LTCat) 开发的 **Plain Craft Launcher (PCL) 2.14.0.0 开源版本**，由第三方独立进行二次创作的产物，**与 PCL 官方无关**。

- 本项目遵循《PCL 分发有限许可》与《PCL 存储库合理使用指南》；
- 保留对 Minecraft 正版购买的支持与劝导。

## 许可与致谢

- 特别感谢 **龙腾猫跃 (LTCat)** 与所有 PCL 贡献者：<https://github.com/Meloong-Git/PCL>
- 联机能力由 **陶瓦联机 (Terracotta)** 提供：<https://github.com/burningtnt/Terracotta>
- 种子地图基于 **cubiomes**
- 如果你喜欢这个项目背后的工作，欢迎前往 [爱发电](https://meloong.com/afd/a/LTCat) 支持 PCL 原作者

## 反馈

遇到问题或有建议，欢迎通过仓库的 **Issues** 反馈，并附上以下信息以便定位：

- 启动器版本号（关于页面底部）
- 问题发生时的操作步骤
- `logs` 文件夹中的最新日志（工具箱 → 打开日志文件夹）

<div align="center"><sub>缘启 YuanQi Launcher · 由 缘栽 维护</sub></div>
