<#
  status.ps1 — 只读体检：环境 + 今日判定 + MAA/M9A 现状 + 最近记录。不会启动任何程序、不改任何配置。
  用法： pwsh -File status.ps1

  2026-09-23 拆分：本文件只剩"外壳"——分节编号、路径染色、以及那五行都适用的记录段。
  两款各自的体检段落函数在 lib\ark-status.ps1 / lib\r1999-status.ps1。
  ⚠ 改口(11-04 刀④)：那两本已整件出仓 —— EnvRows/ProcNames 两枚名册与三态判尺住核
  (verb resolve-status-env-rows／resolve-status-proc-names，真身 tools\panel-cs\Detect.cs)，
  展示话术逐字内联进 lib\ark-module.ps1／lib\r1999-module.ps1 的 Status* 钩子；本壳只留染色与分节。
  2026-09-25 模块化：外壳不再写"ark 这一段、r1999 那一段"，改成遍历 lib\modules.ps1 注册表里
  这一棵树真的装了的模块，逐段问它的钩子在不在(在就打印，不在就整段跳过)。
  所以将来只发布一款的仓库里，同一个 status.ps1 自动只长那一款的样子；再加第三款游戏也不用改这个文件 ——
  只要它的 <id>-module.ps1 里带上 StatusRows / StatusPlan 这几个钩子。
#>
# 运行环境：PowerShell 7(pwsh)。本仓库只支持 7，不再为系统自带的 Windows PowerShell 5.1 留兼容退路。
[CmdletBinding()]
param([switch]$Offline)

$ErrorActionPreference = 'Stop'
# 有什么点什么：lib 里的模块按这个顺序 dot-source，文件不在就跳过。
# 本地工作副本都在；只发布一款时导出的仓库里没有另一款那几个文件，于是体检自动只剩那一路 —— 不靠改代码。
# (11-04 刀④：ark-status／r1999-status 两本整件出仓，名册改问核、话术进两本清单的钩子 —— 这一段自此不需要点它们了)
foreach ($lib in @('common', 'decisions', 'modules', 'maa', 'm9a', 'mumu')) {
    $libPath = Join-Path $PSScriptRoot "lib\$lib.ps1"
    if (Test-Path -LiteralPath $libPath) { . $libPath }
}

# 内核接线(与 daily-clear 同一套；装载已归一进 lib\common.ps1 段尾，刀⑲ 笔(c))：体检自己就要用 Get-GdVersion/Read-GdJson，先编再读才和真实跑的是同一条路。
# 编不成不会抛，只会在这屏最后一行显示"内核=fallback"，其余读数照旧。
$S = Get-GdSettings -Quiet

$script:sec = 0
function Show-GdSectionHeader {
    param([Parameter(Mandatory)][string]$Title)
    $script:sec++
    Write-Host "`n[$script:sec] $Title" -ForegroundColor Cyan
}
function Show-GdEnvRows {
    <# 刀④(11-04)：三态判尺(没填／在／不在)随名册一起搬进核(verb resolve-status-env-rows 逐行作答)，
       这壳只留三态的染色话术 —— 没填那一项仍要单列状态：访客第一次跑时最该看到的是"哪几项还没填"。 #>
    param([Parameter(Mandatory)]$Rows)
    foreach ($r in @($Rows)) {
        $k = [string]$r.label
        $v = [string]$r.shown
        $st = [string]$r.state
        if ($st -eq 'empty') { Write-Host ("  {0,-10} {1} {2}" -f $k, '未填', '(settings.json 里这一项空着 → 跑 setup.ps1)') -ForegroundColor Yellow; continue }
        Write-Host ("  {0,-10} {1} {2}" -f $k, $(if ($st -eq 'ok') { 'OK  ' } else { '缺失' }), $v) -ForegroundColor $(if ($st -eq 'ok') { 'Gray' } else { 'Red' })
    }
}
function Invoke-GdTolerant {
    <# 体检是只读的：某一段读不出来(助手没装、路径没填、配置文件换了版本)就印一行红字继续往下走。
        整段抛异常退出会让人以为 status.ps1 本身坏了，反而看不到"哪几项没就绪"这件真正有用的事。 #>
    param([Parameter(Mandatory)][scriptblock]$Body)
    try { & $Body } catch { Write-Host "  这一段读不出来：$($_.Exception.Message)" -ForegroundColor Red }
}
function Get-GdStatusModule {
    <# 这棵树上该出现在体检里的模块 = 注册了、而且这一路的助手库在。
        注册表空 = 这棵树缺了 lib\modules.ps1 或 *-module.ps1(工程被裁坏了)，
        宁可在这里说清楚，也不要印一份"看着正常其实少了四段"的体检 —— 那种半成品会把人往错方向带。 #>
    # 判据从"函数在不在"(Test-GdFunc，刀④ 随两本 status 一起退役)换成"件在不在"——同一条事实的更直写法。
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'lib\modules.ps1'))) { throw 'status.ps1 点不到 lib\modules.ps1 —— 这个工程缺文件，体检没法做' }
    $mods = @(Get-GdModules -AvailableOnly)
    if (-not $mods.Count) {
        # 内核主仓(gd-ops)不装任何插件时是合法状态：没有可体检的游戏段，但这不是工程被裁坏。
        # 与"注册了模块、助手库却不在"(那是半装坏的树，仍然抛)分开判。
        if (-not @(Get-GdModuleIds).Count) { return @() }
        throw '注册的模块都没装全(清单在、助手库不在)——这棵树是半装坏的，先重跑 install-plugin 或 setup'
    }
    return $mods
}

# 注册表要先装满，后面 Get-GdEntryVerb 现问的、体检分段问的都是它
[void](Import-GdModules -Quiet)

# 提示语里"下一步敲什么"必须跟着本仓库入口的参数名走：合体工程 → -ArkShopDone/-RShopDone，单游戏仓库 → -ShopDone/-ShopReset。
# 以前这六行映射只写在 daily-clear.ps1 里，于是 status 打出来的是短名、合体入口却不认(2026-09-24 发现)。
# 追加(2026-10-12 第 1 刀)：方舟的 -AckArk 已从入口退场(活动改每轮机器自证)，所以这里的举例不再用它，
#   但 ark 清单 EntryVerbs.Merged 里那枚 Ack 键仍在(Rust 链体的 entryverb ask 名册没变)——Hooks.Ack 占位格已随 11-04 刀⑤ 出仓，见 lib\ark-module.ps1 的注释。
# 追加(11-01 丙形真转接，§11.176 ⑥)：这里不再点同步件把长名盖进进程内的表，Get-GdEntryVerb 现问上面装满的注册表。

$mods = @(Get-GdStatusModule)
if (-not $mods.Count) {
    Write-Host "`n  这棵树上还没有安装任何游戏插件 —— 体检只到环境层，没有可展示的游戏段。" -ForegroundColor Yellow
    Write-Host '  装法：pwsh -File tools\install-plugin.ps1 -From <插件仓目录>（-List 看已装）' -ForegroundColor Yellow
    exit 0
}

Show-GdSectionHeader '环境'
foreach ($m in $mods) {
    if ($m.Hooks.StatusRows) { Show-GdEnvRows (& $m.Hooks.StatusRows $S) }
}
foreach ($m in $mods) {
    if ($m.Hooks.StatusVersion) { & $m.Hooks.StatusVersion $S }
}
Invoke-GdTolerant {
    # 屏保不属于任何一路，属于这台机器：06:00 / 18:00 方舟连败的根因就是它接管了输入桌面，
    # 所以它必须出现在体检第一段里，而不是藏在某一路的钩子后面。
    # 稳态读数与四档判据都只住核内(verb read-screensaver-config ／ resolve-screensaver-verdict)，
    # 这里现问两枚出口；Found:false(键开不了)必须折成 cfg=null 交下去 —— 否则 unknown 档会被读成 none，
    # 那是"读不到"冒充"没事"，与真身那句"绝不许据此判没装屏保"同源。
    $sd = Invoke-GdCli -Verb 'read-screensaver-config' -Body ([pscustomobject]@{})
    $scv = Invoke-GdCli -Verb 'resolve-screensaver-verdict' -Body ([pscustomobject]@{ cfg = $(if ($sd.Found) { $sd } else { $null }) })
    $col = switch ($scv.Kind) { 'armed' { 'Red' } 'none' { 'Green' } 'off' { 'Green' } default { 'Yellow' } }
    # 长话本身以「屏保：」开头，这一列的列名也叫"屏保" —— 把前缀剥掉，别让体检里念成「屏保  屏保：…」
    $txt = [string]$scv.Detail
    if ($txt.StartsWith('屏保：')) { $txt = $txt.Substring(3) }
    Write-Host ("  {0,-10} {1}" -f '屏保', $txt) -ForegroundColor $col
}
Invoke-GdTolerant {
    # 版本号只有仓库根那个 VERSION 文件一处；报问题前先跑一次 status，第一屏就能说清"我用的是哪版"。
    Write-Host ("  {0,-10} {1}" -f '版本', (Get-GdVersion)) -ForegroundColor Gray
    # 内核现状一行(与日志里自印的那句同一个函数)：报"这台机器跑得不对"时先看它是 live 还是 fallback。
    Write-Host ('  ' + (Get-GdCoreReadout)) -ForegroundColor Gray
}

Show-GdSectionHeader '进程 / 模拟器'
foreach ($m in $mods) {
    if (-not $m.Hooks.StatusProcs) { continue }
    foreach ($n in @(& $m.Hooks.StatusProcs $S)) {
        Write-Host ("  {0,-10} {1}" -f $n, $(if (Test-GdProcessRunning -Name $n) { '正在运行' } else { '未运行' }))
    }
}
foreach ($m in $mods) {
    if ($m.Hooks.StatusProcExtra) { & $m.Hooks.StatusProcExtra $S }
}

Show-GdSectionHeader '今日判定'
foreach ($m in $mods) {
    if (-not $m.Hooks.StatusPlan) { continue }
    $mm = $m
    Invoke-GdTolerant { & $mm.Hooks.StatusPlan $S ([bool]$Offline) }
}

# 每一路自己那一节(MAA 队列 / M9A 实例)：标题由模块自己给，所以两节仍然按注册顺序出现在这里
foreach ($m in $mods) {
    if (-not ($m.Hooks.StatusSection -and $m.StatusSectionTitle)) { continue }
    Show-GdSectionHeader ([string]$m.StatusSectionTitle)
    $mm = $m
    Invoke-GdTolerant { & $mm.Hooks.StatusSection $S }
}

# 标题用"活动门"而不是"活动确认"：两路的第一道门已经不是一种东西了 ——
# ark 是每轮机器自证(Show-GdGateState 里读 state\ark-event-probe.json)，r1999 仍是人敲的确认门。
Show-GdSectionHeader '活动门 / 商店领空状态'
foreach ($m in $mods) { Show-GdGateState -Game ([string]$m.Id) }

Show-GdSectionHeader '最近 5 条运行记录'
$hist = Join-Path (Get-GdDir 'State') 'history.csv'
if (Test-Path -LiteralPath $hist) { Get-Content -LiteralPath $hist -Tail 5 | ForEach-Object { Write-Host "  $_" } }
else { Write-Host '  (还没有跑过)' }
Write-Host ''
