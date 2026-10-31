<#
.SYNOPSIS
    把缘启启动器打包成「单文件安装/更新程序」。

.DESCRIPTION
    1. 以 Release 配置编译启动器；
    2. 收集「程序文件」（根目录 exe/dll/config/xml + Terracotta、Videos 两个目录），YQL 与 .minecraft 属用户数据，不进包；
    3. 编译安装程序，并把上一步的 zip 追加到安装程序末尾，得到单个 exe；
    4. 输出到 E:\PCL\发布\YuanQi-Setup-<版本>.exe

.PARAMETER SkipBuild
    跳过启动器编译，直接使用 bin\Release 里现有的文件打包。

.PARAMETER OutputDir
    输出目录，默认为 E:\PCL\发布。
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputDir = 'E:\PCL\发布',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$Root        = 'E:\PCL\PCL-src'
$MsBuild     = 'D:\Visual Studio\MSBuild\Current\Bin\MSBuild.exe'
$LauncherPrj = Join-Path $Root 'YuanQi Launcher\YuanQi Launcher.vbproj'
$SetupPrj    = Join-Path $Root 'YuanQi Setup\YuanQi Setup.vbproj'
$LauncherBin = Join-Path $Root "YuanQi Launcher\bin\$Configuration"
# 启动器项目的所有配置都输出到 bin\，这里做一次兼容
if (-not (Test-Path (Join-Path $LauncherBin 'YuanQi Launcher.exe'))) {
    $LauncherBin = Join-Path $Root 'YuanQi Launcher\bin'
}
$SetupBin    = Join-Path $Root "YuanQi Setup\bin\$Configuration"

function Write-Step($Text) { Write-Host "`n== $Text" -ForegroundColor Cyan }
function Write-Ok($Text)   { Write-Host "   $Text" -ForegroundColor Green }

# ── 1. 编译启动器 ──────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Step "编译启动器（$Configuration）"
    & $MsBuild $LauncherPrj /t:Build /p:Configuration=$Configuration /v:m /nologo
    if ($LASTEXITCODE -ne 0) { throw "启动器编译失败。" }
    Write-Ok "编译完成"
}

$LauncherExe = Join-Path $LauncherBin 'YuanQi Launcher.exe'
if (-not (Test-Path $LauncherExe)) { throw "找不到启动器：$LauncherExe" }

# ── 2. 读取版本号 ──────────────────────────────────────────────
$Version = '0.0.0'
$ModBase = Join-Path $Root 'YuanQi Launcher\Modules\Base\ModBase.vb'
if (Test-Path $ModBase) {
    $Match = Select-String -Path $ModBase -Pattern 'VersionBaseName As String = "([^"]+)"' | Select-Object -First 1
    if ($Match) { $Version = $Match.Matches[0].Groups[1].Value }
}
Write-Ok "版本：$Version"

# ── 3. 生成 BuildInfo（供安装程序显示版本） ─────────────────────
$BuildInfo = Join-Path $Root 'YuanQi Setup\BuildInfo.g.vb'
$BuildInfoText = @"
' 由打包脚本自动生成，请勿手动修改
Public Module BuildInfo
    Public Const AppVersion As String = "$Version"
    Public Const BuildDate As String = "$(Get-Date -Format 'yyyy-MM-dd HH:mm')"
End Module
"@
[IO.File]::WriteAllText($BuildInfo, $BuildInfoText, (New-Object Text.UTF8Encoding($false)))

# ── 4. 编译安装程序 ────────────────────────────────────────────
Write-Step "编译安装程序"
& $MsBuild $SetupPrj /t:Rebuild /p:Configuration=Release /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "安装程序编译失败。" }
$SetupExe = Join-Path $SetupBin 'YuanQi Setup.exe'
if (-not (Test-Path $SetupExe)) { throw "找不到安装程序：$SetupExe" }
Write-Ok "编译完成"

# ── 5. 收集程序文件 ────────────────────────────────────────────
Write-Step "收集程序文件"
$TempRoot = Join-Path ([IO.Path]::GetTempPath()) ("YuanQiPack_" + [Guid]::NewGuid().ToString('N'))
$Payload  = Join-Path $TempRoot 'payload'
New-Item -ItemType Directory -Force -Path $Payload | Out-Null

$Exts = @('.exe', '.dll', '.config', '.xml')
$Count = 0
$Bytes = 0
Get-ChildItem $LauncherBin -File | Where-Object {
    $Exts -contains $_.Extension.ToLowerInvariant() -and $_.Name -ne 'YuanQi Setup.exe'
} | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $Payload $_.Name) -Force
    $Count++; $Bytes += $_.Length
}
foreach ($Dir in @('Terracotta', 'Videos')) {
    $Src = Join-Path $LauncherBin $Dir
    if (-not (Test-Path $Src)) { continue }
    Copy-Item $Src (Join-Path $Payload $Dir) -Recurse -Force
    Get-ChildItem $Src -Recurse -File | ForEach-Object { $Count++; $Bytes += $_.Length }
}
Write-Ok ("{0} 个文件，共 {1:N1} MB" -f $Count, ($Bytes / 1MB))

# ── 6. 压缩 ────────────────────────────────────────────────────
Write-Step "压缩程序文件"
$ZipPath = Join-Path $TempRoot 'payload.zip'
Compress-Archive -Path (Join-Path $Payload '*') -DestinationPath $ZipPath -CompressionLevel Optimal -Force
$ZipSize = (Get-Item $ZipPath).Length
Write-Ok ("压缩后 {0:N1} MB" -f ($ZipSize / 1MB))

# ── 7. 合成单文件安装包 ────────────────────────────────────────
Write-Step "生成单文件安装包"
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null }
$OutFile = Join-Path $OutputDir ("YuanQi-Setup-$Version.exe")

$SetupBytes = [IO.File]::ReadAllBytes($SetupExe)
$ZipBytes   = [IO.File]::ReadAllBytes($ZipPath)
$Marker     = [Text.Encoding]::ASCII.GetBytes('YQSETUP1')
$Length     = [BitConverter]::GetBytes([Int64]$ZipBytes.Length)

$Stream = [IO.File]::Create($OutFile)
try {
    $Stream.Write($SetupBytes, 0, $SetupBytes.Length)
    $Stream.Write($ZipBytes, 0, $ZipBytes.Length)
    $Stream.Write($Length, 0, $Length.Length)
    $Stream.Write($Marker, 0, $Marker.Length)
} finally {
    $Stream.Close()
}

Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue

$OutItem = Get-Item $OutFile
Write-Ok ("输出：{0}" -f $OutItem.FullName)
Write-Ok ("大小：{0:N1} MB" -f ($OutItem.Length / 1MB))
Write-Host "`n安装包已就绪：双击即可安装或更新。" -ForegroundColor Cyan
