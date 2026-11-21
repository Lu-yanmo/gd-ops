# tools/reschedule-daily.ps1 — 换每日执行时间点：等本轮跑完 → 改 settings → 重注册 → 删掉旧时间点的任务
#
#   pwsh -File tools\reschedule-daily.ps1 -Times 06:00,18:00            # 会等正在跑的一轮结束，弹一次 UAC
#   pwsh -File tools\reschedule-daily.ps1 -Times 06:00,18:00 -DryRun    # 只打印将要做什么(不等待、不写、不注册)
#   pwsh -File tools\reschedule-daily.ps1 -Times 06:00,18:00 -NoWait    # 不等当前轮次(正在跑的话会和新任务撞车，慎用)
#
# 为什么需要这个脚本而不是直接跑 install-task.ps1：
#   1) install-task.ps1 只「更新/新建」目标时间点的任务，**不会删旧时间点的** —— 从 08:00/20:00 换成 06:00/18:00
#      如果只跑它，机器上会同时留着 GameDaily-0800/2000/0600/1800，一天跑四遍。
#   2) 改注册的一瞬间如果有一轮正在跑，正在跑的那轮不受影响(脚本已读进内存)，但新任务可能紧接着又点一份，
#      所以要等它跑完。
param(
    [Parameter(Mandatory)][string[]]$Times,
    [switch]$DryRun,
    [switch]$NoWait,
    [int]$MaxWaitMinutes = 150,
    [int]$PollSeconds = 60
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $root 'lib\common.ps1')

# "-Times 06:00,18:00"(提权重跑时是一个带逗号的整串)和 "-Times 06:00 -Times 18:00" 都要能吃下
$Times = @($Times | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$bad = @($Times | Where-Object { $_ -notmatch '^(\d{1,2}):(\d{2})$' })
if ($bad.Count) { throw "时间点格式不对：$($bad -join ' , ')(要 HH:mm，例如 06:00)" }
$norm = @($Times | ForEach-Object {
    $p = $_ -split ':'
    if ([int]$p[0] -gt 23 -or [int]$p[1] -gt 59) { throw "时间点超出范围：$_" }
    ('{0:D2}:{1:D2}' -f [int]$p[0], [int]$p[1])
} | Sort-Object -Unique)
if (-not $norm.Count) { throw '没解析到任何时间点' }
if ($norm.Count -ne $Times.Count) { Write-GdLog "去掉重复/补零后：$($norm -join ' , ')" }

$S = Get-GdSettings
$prefix = [string]$S.schedule.taskPrefix
if (-not $prefix) { $prefix = 'GameDaily' }
$newNames = @($norm | ForEach-Object { "$prefix-$($_ -replace ':', '')" })
$manualName = "$prefix-Manual"

# 提权重跑(install-task 那一步要注册最高权限任务)；-DryRun 永远不提权
if (-not $DryRun -and -not (Test-GdAdmin)) {
    Write-GdLog '改计划任务要管理员权限：以管理员身份重跑本脚本(会弹一次 UAC)' -Level WARN
    $bound = @{ }
    if ($NoWait) { $bound['NoWait'] = $true }
    $code = Start-GdElevatedSelf -Script (Join-Path $root 'tools\reschedule-daily.ps1') -Bound $bound `
                                 -Force @('-Times', ($norm -join ','), '-MaxWaitMinutes', "$MaxWaitMinutes")
    Write-GdLog "提权子进程结束，退出码 $code" -Level $(if ($code -eq 0) { 'OK' } else { 'WARN' })
    exit $code
}

$existing = @(Get-ScheduledTask -TaskPath '\' -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -like "$prefix-*" })
$stale = @($existing | Where-Object { ($_.TaskName -notin $newNames) -and ($_.TaskName -ne $manualName) })
$oldTimes = @($S.schedule.times | ForEach-Object { [string]$_ })

Write-GdLog "计划任务时间点：$(if ($oldTimes.Count) { $oldTimes -join ' , ' } else { '(settings 里没读到)' }) → $($norm -join ' , ')" -Level STEP
if ($stale.Count) { Write-GdLog "将被删除的旧任务：$(($stale.TaskName | Sort-Object) -join ' , ')" -Level WARN } else { Write-GdLog '没有需要清理的旧时间点任务' }

if ($DryRun) {
    Write-GdLog '[dry-run] 不等待、不写 settings.json、不注册/删除任务' -Level STEP
    Write-GdLog "[dry-run] 将注册：$($newNames -join ' , ')；保留 $manualName；删除：$(if ($stale.Count) { ($stale.TaskName | Sort-Object) -join ' , ' } else { '无' })"
    exit 0
}

# ---------- 1. 等正在跑的一轮结束 ----------
if ($NoWait) {
    Write-GdLog '-NoWait：不等当前轮次' -Level WARN
} else {
    $deadline = (Get-Date).AddMinutes($MaxWaitMinutes)
    $waited = 0
    while ($true) {
        $busy = @(Get-CimInstance Win32_Process -Filter "Name='pwsh.exe' OR Name='cmd.exe'" -ErrorAction SilentlyContinue |
                  Where-Object { $_.CommandLine -match 'daily-clear\.ps1' })
        if (-not $busy.Count) { break }
        if ((Get-Date) -gt $deadline) {
            Write-GdLog "等了 $MaxWaitMinutes 分钟，日常脚本还在跑 → 放弃改时间(旧时间点继续照跑，没坏)" -Level ERROR
            exit 3
        }
        Write-GdLog "有一轮日常在跑(进程 $($busy.Count) 个)：等它结束，$PollSeconds 秒后再看…(累计已等 $waited 分钟)"
        Start-Sleep -Seconds $PollSeconds
        $waited++
    }
    Write-GdLog '没有正在跑的日常轮次' -Level OK
}

# ---------- 2. 写 settings.json ----------
$cfgPath = Join-Path $root 'config\settings.json'
$cfg = Read-GdJson -Path $cfgPath
if (@($cfg.schedule.times | ForEach-Object { [string]$_ }) -join ',' -ne ($norm -join ',')) {
    $bak = Backup-GdFile -Path $cfgPath
    $cfg.schedule | Add-Member -NotePropertyName 'times' -NotePropertyValue $norm -Force
    Write-GdJson -Path $cfgPath -Object $cfg -Depth 100
    Write-GdLog "已写 settings.json：schedule.times = $($norm -join ' , ')(备份：$(Split-Path -Leaf $bak))" -Level OK
} else {
    Write-GdLog 'settings.json 里已经是这些时间点，跳过写入'
}

# ---------- 3. 注册新时间点(此刻已是管理员，install-task 不会再弹 UAC) ----------
$LASTEXITCODE = 0    # install-task 成功路径不 exit；StrictMode 1.0 下读未赋值的 $LASTEXITCODE 是终止错误，会把第 4 步删旧任务整段带走
& (Join-Path $root 'install-task.ps1') -Times $norm
$code = $LASTEXITCODE
if ($code -ne 0) { Write-GdLog "install-task.ps1 退出码 $code → 停止，不动旧任务" -Level ERROR; exit $code }

# ---------- 4. 删掉旧时间点的任务 ----------
foreach ($t in $stale) {
    Unregister-ScheduledTask -TaskName $t.TaskName -Confirm:$false
    Write-GdLog "已删除旧任务 $($t.TaskName)"
}

# ---------- 5. 验证 ----------
Write-GdLog ''
Write-GdLog '验证：'
$fail = 0
$names = $newNames + @($manualName) | Where-Object { $_ }
foreach ($name in $names) {
    $info = Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
    if (-not $info) { Write-Host "  $name 不存在" -ForegroundColor Red; $fail++; continue }
    $ni = $info | Get-ScheduledTaskInfo
    $lvl = if ($info.Principal.RunLevel -eq 'Highest') { '最高权限' } else { '普通权限!' }
    Write-Host ("  {0,-20} {1,-8} {2,-9} 下次运行 {3:yyyy-MM-dd HH:mm}" -f $name, $info.State, $lvl, $ni.NextRunTime)
    if ($name -ne $manualName -and $ni.NextRunTime -lt (Get-Date)) { Write-Host "    ↑ 下次运行时间已经过了？检查触发器" -ForegroundColor Yellow }
}
$leftover = @(Get-ScheduledTask -TaskPath '\' -ErrorAction SilentlyContinue |
              Where-Object { $_.TaskName -like "$prefix-*" -and $_.TaskName -notin $names })
if ($leftover.Count) { Write-Host "  还有没清掉的任务：$(($leftover.TaskName) -join ' , ')" -ForegroundColor Yellow; $fail++ }
if ($fail) { Write-GdLog "有 $fail 项不对，按上面的输出手工核一下" -Level ERROR; exit 1 }
Write-GdLog '时间点已换好' -Level OK
exit 0
