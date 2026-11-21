<#
  run-via-task.ps1 — 免 UAC 的手动入口：触发 GameDaily-Manual(注册时就是最高权限)，等它跑完并回传退出码。

  pwsh -File run-via-task.ps1                 # 完整一轮(方舟 + 1999)，不弹 UAC
  pwsh -File run-via-task.ps1 -NoWait         # 只点火，不等结果
  pwsh -File run-via-task.ps1 -Stop           # 结束正在跑的这一轮(连它起的助手/端游一起收)
  pwsh -File run-via-task.ps1 -Games ark      # 只跑一路 → 计划任务参数是固定的，会自动改成本机直跑(可能弹一次 UAC)
  pwsh -File run-via-task.ps1 -Direct         # 强制本机直跑
  (单游戏发布仓库里只装得下一路的代码，-Games 这个参数就是空转：不用管它)
#>
# 运行环境：PowerShell 7(pwsh)。本仓库只支持 7，不再为系统自带的 Windows PowerShell 5.1 留兼容退路。
[CmdletBinding()]
param(
    [ValidateSet('all', 'ark', 'r1999')][string]$Games = 'all',
    [string]$TaskName,
    [switch]$NoWait,
    [switch]$Stop,
    [switch]$Direct
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib\common.ps1')

# 内核接线(接线批；装载已归一进 lib\common.ps1 段尾，刀⑲ 笔(c))：这份文件只用 Get-GdSettings / Join-GdPath / Stop-GdRunLock 那几件，全在 whitelist 里。
# 它是"按下按钮"的短命宿主，所以只在**回退**时吭一声，live 时不给面板添一行噪音。
$coreNote = Get-GdCoreStatus
if ($coreNote) { Write-GdLog $coreNote -Level WARN }

# 这份文件会原样进两个导出仓库，所以先问"这棵树上装了哪一路"：只有一路时 -Games 没有意义，
# 而且单游戏入口 daily-clear.ps1 压根没有 -Games 参数，硬传会当场报"找不到参数"。
# 判据按 lib\<id>-module.ps1 有几个来数(模块清单文件本身就是"这一路在不在"的标记)，
# 这样将来加第三款游戏不用回来改这句 -xor；本文件也不为此要多点一份 modules.ps1 —— 它只要知道"几款"。
$moduleIds = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'lib') -Filter '*-module.ps1' -ErrorAction SilentlyContinue |
    ForEach-Object { $_.Name -replace '-module\.ps1$', '' } | Sort-Object)
$onlyGame = $null
if ($moduleIds.Count -eq 1) {
    $onlyGame = $moduleIds[0]
    if ($Games -notin @('all', $onlyGame)) { Write-GdLog "本仓库只有 $onlyGame 这一路，-Games $Games 被忽略" -Level WARN }
}
$S = Get-GdSettings -Quiet
$task = if ($TaskName) { $TaskName } else { "$($S.schedule.taskPrefix)-Manual" }
$daily = Join-Path $PSScriptRoot 'daily-clear.ps1'

function Invoke-Direct {
    param([string]$Why)
    if ($Why) { Write-GdLog "$Why → 本机直跑 daily-clear.ps1(非管理员时会弹一次 UAC)" -Level WARN }
    if ($onlyGame) { & $daily -Tag manual } else { & $daily -Games $Games -Tag manual }
    exit $LASTEXITCODE
}

# 只跑一路时给任务覆盖参数。以前这里看见 -Games ark 就直接回落本机直跑(要弹一次 UAC)，
# 而"只补跑某一路"恰恰是最常在人不在机器前发生的场景，弹 UAC 等于那一轮根本跑不起来。
# 返回 $null = 没拼出来，调用方照旧回落直跑 —— 宁可多弹一次 UAC，也不能把参数拆错、跑成别的东西。
#
# ── 冻结(2026-09-27，PS→C# 批次 5)：下面函数里"剥 -Games/-Tag + 按引号切词"的规则段
# 已迁到 tools\panel-cs\Orchestration.cs(verb：split-task-args；契约见 docs\ps-migration-batch0.md)。
# 壳里留的是活：Get-ScheduledTask 读任务、动作数守卫、Count<3 的"不像话"拒绝、追加 -Tag manual -Games、
# 以及一切回落直跑的决断。改切词规则请改 C# 并跑 tools\panel-cs\build.ps1 -Verify(orchtest)，不要在这里加规则。
# ──────────────────────────────────────────────────────────────────────────────
function Get-GdTaskArgsWithGames {
    param([Parameter(Mandatory)][string]$Task, [Parameter(Mandatory)][string]$WithGames)
    try {
        # @() 必须套在 .Actions 外面，不能只套 Get-ScheduledTask：
        # 那样是"@一个任务对象" 再成员枚举出 .Actions，单动作时拿到的是单个对象而不是数组，
        # $act.Count 就成了 $null，下面 "-ne 1" 永真 → 参数覆盖永远静默回落直跑(2026-09-25 实测踩过)。
        $act = @(@(Get-ScheduledTask -TaskName $Task -ErrorAction Stop).Actions)
        if ($act.Count -ne 1) { Write-GdLog "任务 $Task 有 $($act.Count) 个动作，参数没法覆盖 → 只跑一路改走直跑" -Level WARN; return $null }
        $text = [string]$act[0].Arguments
        if (-not $text) { return $null }
        # 任务定义里的 Arguments 是一整条字符串(-File "带空格的路径" 那种)，
        # 而 Start-ScheduledTask -Argument 要的是参数数组。先剥掉原有的 -Games/-Tag，再按引号切：
        # 剥参与切词的规则在 C#(split-task-args)，引号内整体成一项(去掉引号)、其余按空白切。
        $pieces = @((Invoke-GdCli -Verb 'split-task-args' -Body ([pscustomobject]@{ text = $text })).Pieces)
        if ($pieces.Count -lt 3) { Write-GdLog "任务参数拆出来只剩 $($pieces.Count) 段，不像话 → 只跑一路改走直跑" -Level WARN; return $null }
        $pieces += @('-Tag', 'manual', '-Games', $WithGames)
        return ,$pieces
    } catch {
        Write-GdLog "读任务参数失败($($_.Exception.Message)) → 只跑一路改走直跑" -Level WARN
        return $null
    }
}

$exists = [bool](Get-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue)

# ---------- 结束正在跑的这一轮 ----------
# 为什么走"停任务"而不是让面板直接 Kill 进程：那一轮是提权进程，普通完整性的面板没权限结束它；
# Stop-ScheduledTask 由任务调度服务(系统身份)执行，才使得上劲。
if ($Stop) {
    if (-not $exists) { Write-GdLog "还没注册 $task，也没有要走任务的轮次可停" -Level WARN; exit 0 }
    $cur = Get-GdTaskState -Name $task
    if ([string]$cur.State -ne 'Running') {
        Write-GdLog "任务 $task 现在没在跑(状态=$($cur.State))，不用停"
        exit 0
    }
    Write-GdLog "结束计划任务 $task —— 它起的助手/端游会跟着收，但不会等它们跑完再收尾" -Level STEP
    try { Stop-ScheduledTask -TaskName $task }
    catch { Write-GdLog "结束失败：$($_.Exception.Message)(这台机器的任务权限配置不认这个操作，去任务计划程序里手工结束)" -Level ERROR; exit 2 }
    # Stop 是异步的，立刻回查还是 Running，等它真停下来再下结论
    $till = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $till) {
        Start-Sleep -Seconds 2
        if ([string](Get-GdTaskState -Name $task).State -ne 'Running') { break }
    }
    $cur = Get-GdTaskState -Name $task
    if ([string]$cur.State -eq 'Running') {
        Write-GdLog "已经请求结束，但 30 秒后任务还是 Running —— 去任务计划程序里手工结束它" -Level ERROR
        exit 2
    }
    # 被掐掉的那一轮走不到自己 finally 里的收尾，活体标记留在原地。虽然读方按 PID 判活会识破它，
    # 主动清掉更干净：省下面板一次"这是残留"的判断。
    Stop-GdRunLock
    # 睡眠屏蔽是线程句柄，任务进程一死系统自动撤销；屏保自 2026-09-26 起"只关不复"，没有遗留要复原。
    # 所以这里两样都不碰 —— 跨进程去改注册表反而是新增风险。
    Write-GdLog "任务已停止(状态=$($cur.State))。助手/端游要是还开着，是它没跟着任务一起退，手动关掉即可。" -Level OK
    exit 0
}

if (-not $exists) { Invoke-Direct -Why "还没注册按需任务 $task(先跑 install-task.ps1)" }
if ($Direct) { Invoke-Direct -Why '-Direct' }
# 只跑一路：先试着给任务覆盖参数(免 UAC)，拼不出来才走直跑那条会弹 UAC 的路。
$taskArgs = $null
if (-not $onlyGame -and $Games -ne 'all') { $taskArgs = Get-GdTaskArgsWithGames -Task $task -WithGames $Games }
if (-not $onlyGame -and $Games -ne 'all' -and -not $taskArgs) { Invoke-Direct -Why "没能覆盖任务参数，-Games $Games 走不了任务" }

Write-GdLog ("触发计划任务 $task(最高权限、免 UAC)" + $(if ($taskArgs) { "，本轮只跑 $Games" } else { '' })) -Level STEP
try {
    if ($taskArgs) { Start-ScheduledTask -TaskName $task -Argument $taskArgs } else { Start-ScheduledTask -TaskName $task }
} catch {
    Invoke-Direct -Why "触发 $task 失败：$($_.Exception.Message)"
}
if ($NoWait) { Write-GdLog '已点火(-NoWait 不等待结果)'; exit 0 }

$deadline = (Get-Date).AddMinutes(([double]$S.runtime.taskWaitMinutes))
$logHint = ''
$mirrorPath = ''
$mirrorOffset = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 15
    $st = Get-GdTaskState -Name $task
    if ($st.State -ne 'Running') { break }
    $newest = Get-ChildItem -LiteralPath (Get-GdDir 'Logs') -Filter 'run-*.log' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($newest) {
        $logHint = $newest.FullName
        # 只回显新增行(按字节偏移续读)：每轮重抄最后一行会把控制台刷成一屏重复
        if ($mirrorPath -ne $newest.FullName) { $mirrorPath = $newest.FullName; $mirrorOffset = 0 }
        if ((Get-Item -LiteralPath $newest).Length -lt $mirrorOffset) { $mirrorOffset = 0 }
        # 刀⑰ 笔(b)(2026-11-07)：续读改问核(read-file-tail，Chain.ReadFileTail 直身＝当年 Get-GdFileTail 的逐字复刻)；
        # 回执 Text/Offset 两键与旧壳同名同语义(不在⇒('',0)、轮转⇒归零全读)；Get-GdFileTail 真身随批出仓。
        $tail = Invoke-GdCli -Verb 'read-file-tail' -Body ([pscustomobject]@{ path = $mirrorPath; offset = [long]$mirrorOffset })
        $mirrorOffset = $tail.Offset
        foreach ($line in (@($tail.Text -split "`r?`n") | Where-Object { $_.Trim() })) {
            Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] $line"
        }
    }
}

$st = Get-GdTaskState -Name $task
$code = if ($null -ne $st.Result) { $st.Result } else { 99 }
Write-GdLog "任务 $task 结束：状态=$($st.State) 退出码=$code(0 全部完成 / 1 部分完成 / 2 有失败) 起跑时间=$($st.Last)" -Level $(if ($code -eq 0) { 'OK' } else { 'WARN' })
if ($logHint) { Write-GdLog "本轮日志：$logHint" }
exit $code
