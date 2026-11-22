陶瓦联机（Terracotta）联机模块
=============================

本目录内置了陶瓦联机（Terracotta）的 Windows x86_64 发行版，启动器会把它
复制到 %APPDATA%\YuanQi\Terracotta\ 下运行，因此不需要玩家额外下载。

    terracotta.exe      陶瓦联机主程序（内含 EasyTier 网络组件）
    VCRUNTIME140.DLL    它依赖的 VC++ 运行库

当前版本：0.4.2
来源：https://github.com/burningtnt/Terracotta/releases

如何更新
--------
1. 下载与启动器架构一致的发行包：
     Windows x86_64 → terracotta-<版本>-windows-x86_64-pkg.tar.gz
     Windows ARM64  → terracotta-<版本>-windows-arm64-pkg.tar.gz
2. 用资源管理器或 `tar -xzf <包名>` 解压，得到 terracotta-<版本>-windows-*.exe
   与 VCRUNTIME140.DLL。
3. 把 .exe 重命名为 terracotta.exe，与 VCRUNTIME140.DLL 一起覆盖本目录中的同名文件。

说明
----
1. 如果本目录为空，启动器会尝试从 GitHub / Gitee / CNB 自动下载联机模块。
2. 陶瓦联机需要 Windows 10 及以上版本。
3. 联机相关的设置项（显示名称、自定义公共节点）位于「设置 → 联机」。
