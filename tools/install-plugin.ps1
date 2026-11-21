# tools/install-plugin.ps1 — 把一款游戏插件装进这棵 gd-ops 树（或列出已装 / 卸载）
#
# 插件是什么：主仓只带内核（面板、核、框架、公共库），不带任何一款游戏的业务知识。
# 一款游戏 = 一个附属仓，仓根带 plugin.json（id / name / files / settings 四样）。
# 装插件 = 把 plugin.json 里点名的那批文件原样搬进本树，再把它的配置片段并进本机 settings.json。
# 不装插件时入口会拒跑并告诉你先装一个（daily-clear.ps1 开头那道闸）。
#
#   pwsh -File tools\install-plugin.ps1 -From ..\game-daily-ark       # 装/升级
#   pwsh -File tools\install-plugin.ps1 -List                         # 看装了哪些
#   pwsh -File tools\install-plugin.ps1 -Uninstall ark                # 卸（settings.json 里的键保留）
#
# 冲突规矩：目标文件已存在、又不是本插件装的、内容也不同 → 整批拒装并逐枚点名（-Force 才覆盖）。
# 本机记录写在 config\plugins.json（.gitignore 里），settings.json 改动前备份到 backups\。
# 退出码：0 成功 / 2 用法或清单不合法 / 1 冲突或复制失败。
[CmdletBinding(DefaultParameterSetName = 'Install')]
param(
    [Parameter(ParameterSetName = 'Install', Mandatory)][string]$From,
    [Parameter(ParameterSetName = 'Install')][switch]$Force,
    [Parameter(ParameterSetName = 'List')][switch]$List,
    [Parameter(ParameterSetName = 'Remove', Mandatory)][string]$Uninstall
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $root 'lib\common.ps1')

$regPath = Join-Path $root 'config\plugins.json'
$cfgPath = Join-Path $root 'config\settings.json'
$tplPath = Join-Path $root 'config\settings.example.json'

function Read-GdPluginRegistry {
    if (Test-Path -LiteralPath $regPath) { return (Read-GdJson -Path $regPath) }
    return ([pscustomobject]@{ installed = [pscustomobject]@{} })
}

function Get-GdPluginInstalledMap {
    param([Parameter(Mandatory)]$Reg)
    $map = @{}
    foreach ($p in $Reg.installed.PSObject.Properties) {
        foreach ($f in @($p.Value.files)) { $map[[string]$f] = [string]$p.Name }
    }
    return $map
}

function Assert-GdPluginRelPath {
    param([string]$Rel)
    if (-not $Rel) { throw 'plugin.json 里有空的 files 项' }
    if ($Rel -match '^[A-Za-z]:' -or $Rel.StartsWith('/') -or $Rel -match '(^|/)\.\.(/|$)') {
        throw "plugin.json 的文件路径必须是仓内相对路径：$Rel"
    }
    if ($Rel -notmatch '^(lib|tools)/') {
        throw "plugin.json 只许装 lib/ 与 tools/ 下的件（内核目录不许被插件覆盖）：$Rel"
    }
}

if ($List) {
    $reg = Read-GdPluginRegistry
    $any = @($reg.installed.PSObject.Properties)
    if (-not $any.Count) {
        Write-Host '这棵树上没有装任何游戏插件。'
        Write-Host '装法：pwsh -File tools\install-plugin.ps1 -From <插件仓目录>'
        exit 0
    }
    foreach ($p in $any) {
        Write-Host ("{0,-8} {1,-10} {2} 件  {3}" -f $p.Name, [string]$p.Value.version, @($p.Value.files).Count, [string]$p.Value.installedAt)
    }
    exit 0
}

if ($Uninstall) {
    $reg = Read-GdPluginRegistry
    $entry = $reg.installed.PSObject.Properties[$Uninstall]
    if (-not $entry) { Write-Host "没有装过这个插件：$Uninstall（-List 看已装）"; exit 2 }
    $removed = 0
    foreach ($f in @($entry.Value.files)) {
        $target = Join-Path $root ([string]$f -replace '/', '\')
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force; $removed++ }
    }
    $reg.installed.PSObject.Properties.Remove($Uninstall)
    Write-GdJson -Path $regPath -Object $reg
    Write-Host "已卸载 $Uninstall：删除 $removed 个文件。settings.json 里的键保留（要清自己删）。"
    exit 0
}

$fromDir = (Resolve-Path -LiteralPath $From -ErrorAction SilentlyContinue)
if (-not $fromDir) { Write-Host "插件目录不存在：$From"; exit 2 }
$fromDir = $fromDir.Path
$manPath = Join-Path $fromDir 'plugin.json'
if (-not (Test-Path -LiteralPath $manPath)) { Write-Host "这不像插件仓：缺 plugin.json（$fromDir）"; exit 2 }
$man = Read-GdJson -Path $manPath
$id = [string]$man.id
if (-not $id -or $id -notmatch '^[a-z0-9][a-z0-9-]*$') { Write-Host "plugin.json 的 id 不合法：'$id'"; exit 2 }
$files = @($man.files)
if (-not $files.Count) { Write-Host 'plugin.json 的 files 是空的'; exit 2 }
foreach ($rel in $files) { Assert-GdPluginRelPath -Rel ([string]$rel) }

$reg = Read-GdPluginRegistry
$installedMap = Get-GdPluginInstalledMap -Reg $reg
$conflicts = @()
$plan = @()
foreach ($rel in $files) {
    $rel = [string]$rel
    $src = Join-Path $fromDir ($rel -replace '/', '\')
    if (-not (Test-Path -LiteralPath $src)) { Write-Host "plugin.json 点名的文件不在插件仓里：$rel"; exit 1 }
    $dst = Join-Path $root ($rel -replace '/', '\')
    $plan += [pscustomobject]@{ Rel = $rel; Src = $src; Dst = $dst }
    if (Test-Path -LiteralPath $dst) {
        $owner = ''
        if ($installedMap.ContainsKey($rel)) { $owner = $installedMap[$rel] }
        if ($owner -ne $id) {
            $same = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash
            if (-not $same -and -not $Force) { $conflicts += $rel }
        }
    }
}
if ($conflicts.Count) {
    Write-Host '这些目标文件已存在、不是这个插件装的、内容也不同 —— 整批拒装：'
    $conflicts | ForEach-Object { Write-Host "  $_" }
    Write-Host '确认要覆盖就加 -Force；或先手工处理这几枚。'
    exit 1
}

foreach ($p in $plan) {
    $dir = Split-Path $p.Dst -Parent
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Copy-Item -LiteralPath $p.Src -Destination $p.Dst -Force
}

$addedKeys = @()
$fragRel = [string]$man.settings
if ($fragRel) {
    $fragPath = Join-Path $fromDir ($fragRel -replace '/', '\')
    if (-not (Test-Path -LiteralPath $fragPath)) { Write-Host "plugin.json 点名的配置片段不在插件仓里：$fragRel"; exit 1 }
    $frag = Read-GdJson -Path $fragPath
    if (-not (Test-Path -LiteralPath $cfgPath)) {
        if (Test-Path -LiteralPath $tplPath) { Copy-Item -LiteralPath $tplPath -Destination $cfgPath -Force }
        else { Write-GdJson -Path $cfgPath -Object ([pscustomobject]@{}) }
    }
    $backupDir = Join-Path $root 'backups'
    if (-not (Test-Path -LiteralPath $backupDir)) { New-Item -ItemType Directory -Path $backupDir -Force | Out-Null }
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    Copy-Item -LiteralPath $cfgPath -Destination (Join-Path $backupDir "plugin-$id-$stamp.settings.json") -Force
    $cfg = Read-GdJson -Path $cfgPath
    $addedKeys = @(Merge-GdJsonNode -Base $cfg -Add $frag)
    Write-GdJson -Path $cfgPath -Object $cfg
    $refDir = Join-Path $root 'config\plugins'
    if (-not (Test-Path -LiteralPath $refDir)) { New-Item -ItemType Directory -Path $refDir -Force | Out-Null }
    Copy-Item -LiteralPath $fragPath -Destination (Join-Path $refDir "$id.example.json") -Force
}

$entry = [pscustomobject]@{
    name        = [string]$man.name
    version     = [string]$man.version
    files       = @($files | ForEach-Object { [string]$_ })
    installedAt = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
}
$reg.installed | Add-Member -NotePropertyName $id -NotePropertyValue $entry -Force
$regDir = Split-Path $regPath -Parent
if (-not (Test-Path -LiteralPath $regDir)) { New-Item -ItemType Directory -Path $regDir -Force | Out-Null }
Write-GdJson -Path $regPath -Object $reg

Write-Host "已装插件 $id（$([string]$man.name) $([string]$man.version)）：$($plan.Count) 个文件"
if ($addedKeys.Count) { Write-Host "settings.json 补入 $($addedKeys.Count) 个键：$($addedKeys -join ' , ')" }
else { Write-Host 'settings.json 没有新键（片段里的键都已存在）' }
Write-Host '下一步：pwsh -File setup.ps1（探测助手路径）→ pwsh -File daily-clear.ps1 -DryRun'
exit 0
