<#
  install-task.ps1 — 注册/移除每日计划任务(跑在当前登录用户会话里，助手 GUI 才连得上游戏)

  方舟端游要管理员权限，所以所有任务都注册成「最高权限 + 仅登录时运行」。注册这一个动作需要管理员，
  脚本会自动弹一次 UAC 重跑自己；之后定时跑 / 手动跑(run-daily.cmd 走 schtasks /Run)都不再弹。

  pwsh -File install-task.ps1 -DryRun              # 只打印将要创建的任务
  pwsh -File install-task.ps1                      # 按 settings.json 里的 schedule.times 注册
  pwsh -File install-task.ps1 -Times 07:30,19:30   # 自定义时间点
  pwsh -File install-task.ps1 -NoManual            # 不建手动触发的 GameDaily-Manual
  pwsh -File install-task.ps1 -Uninstall           # 全部删除
#>
# 运行环境：PowerShell 7(pwsh)。本仓库只支持 7，不再为系统自带的 Windows PowerShell 5.1 留兼容退路。
[CmdletBinding()]
param(
    [switch]$Uninstall,
    [switch]$DryRun,
    [switch]$NoManual,
    [string[]]$Times,
    [string]$Prefix,
    [string]$Description
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib\common.ps1')

# 任务描述里别提"另一款游戏"：单游戏发布仓库注册出来的任务只干一款的事。
if (-not $Description) {
    $Description = '自动清理《明日方舟》(端游/MAA，需最高权限) 与《重返未来：1999》(MuMu/M9A) 每日体力'
}

# 没做第一次设置就退出码 2 走人，并留一句话(而不是让 exe 窗口里出现一屏 Exception 格式)
$S = Get-GdSettings -TellAndExit
if ($Prefix) { $taskPrefix = $Prefix } else { $taskPrefix = $S.schedule.taskPrefix }
if (-not $Times) { $Times = @($S.schedule.times) }
$script = Join-Path $PSScriptRoot 'daily-clear.ps1'
if (-not (Test-Path -LiteralPath $script)) { throw "找不到 $script" }

# 方舟端游要管理员权限 → 任务必须「以最高权限运行」，而注册这种任务本身也要管理员
if (-not $DryRun -and -not (Test-GdAdmin)) {
    Write-GdLog '注册最高权限计划任务需要管理员：以管理员身份重跑本脚本(会弹一次 UAC)' -Level WARN
    $code = Start-GdElevatedSelf -Script (Join-Path $PSScriptRoot 'install-task.ps1') -Bound $PSBoundParameters
    Write-GdLog "提权子进程结束，退出码 $code" -Level $(if ($code -eq 0) { 'OK' } else { 'WARN' })
    exit $code
}

$existing = @(Get-ScheduledTask -TaskPath '\' -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -like "$taskPrefix*" })
if ($Uninstall) {
    if ($existing.Count -eq 0) { Write-GdLog "没有以 $taskPrefix 开头的计划任务"; exit 0 }
    foreach ($t in $existing) {
        Write-GdLog "删除计划任务 $($t.TaskName)"
        if (-not $DryRun) { Unregister-ScheduledTask -TaskName $t.TaskName -Confirm:$false }
    }
    Write-GdLog '已移除' -Level OK
    exit 0
}

# 找"用哪个 pwsh.exe 注册任务"：钉进计划任务的这条路径必须**不随 pwsh 自升级漂移**。
# 商店/winget 版真身在带版本号的包目录里(...\WindowsApps\Microsoft.PowerShell_<主.次.修>_x64__<包名>\pwsh.exe)，
# 一次 7.6.6.0→7.6.7.0 的自升级就把旧目录删了，两班任务当场 0x80070002(系统找不到指定的文件)；更糟的是
# 脚本根本没启动 ⇒ logs 零本、台账零行，缺勤是静默的(10-19 起连缺三班，直到 10-20 现读 Last Task Result 才成形)。
# 旧写法那句"PATH 照旧当末档兜底"正是把带版本号的路径钉进任务的出处，故改按稳定度现扫、取第一枚在位的：
#   ① 装机版/exe 自带引擎(Get-GdLocalPwsh：路径不含 WindowsApps，升级不换目录，仍第一优先)
#   ② 当前用户的应用执行别名(别名里不进版本号；10-20 实测计划任务拿它当 Execute 起得来、Last Task Result 0x00000000)
#   ③ 两枚都不在才退回 PATH 那一枚，但钉之前先量一把尺：所在目录名带 主.次.修 版本号 ⇒ 当场留一行 WARN 说明
#      "这枚会随升级断班"，不许静默把雷埋回任务里。
$pwshPath = Get-GdLocalPwsh
$pinHow = '装机版/自带引擎'
if (-not $pwshPath) {
    $aliasPwsh = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\pwsh.exe'
    if (Test-Path -LiteralPath $aliasPwsh) { $pwshPath = $aliasPwsh; $pinHow = '应用执行别名(版本无关)' }
}
if (-not $pwshPath) {
    $pwshPath = [string](Get-Command pwsh.exe -ErrorAction SilentlyContinue).Source
    $pinHow = 'PATH 末档'
    $pinDir = ''
    if ($pwshPath) {
        try { $pinDir = [System.IO.Path]::GetFileName([System.IO.Path]::GetDirectoryName($pwshPath)) } catch { $pinDir = '' }
    }
    if ($pinDir -match '_\d+\.\d+\.\d+') {
        Write-GdLog "钉的是带版本号的包目录($pinDir)——pwsh 一自升级这条路径就失效、任务会静默缺勤(10-19 那三班的出处)；建议改装 MSI 版 PowerShell 7，或让应用执行别名在位后重跑本脚本" -Level WARN
    }
}
if (-not $pwshPath) { throw '找不到 pwsh.exe(PowerShell 7)。先装 PowerShell 7：winget install Microsoft.PowerShell（装完新开一个终端再跑）' }
$actionArgs = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$script`" -Tag auto"

Write-GdLog ('pwsh：' + [string]$pwshPath + '(取处：' + $pinHow + ')')
Write-GdLog "脚本：$script"
Write-GdLog "时间点：$($Times -join ', ')"

foreach ($t in $Times) {
    $name = "$taskPrefix-$($t -replace ':', '')"
    $action = New-ScheduledTaskAction -Execute $pwshPath -Argument $actionArgs -WorkingDirectory $PSScriptRoot
    $trigger = New-ScheduledTaskTrigger -Daily -At $t
    $principal = New-ScheduledTaskPrincipal -UserId "$($env:USERDOMAIN)\$($env:USERNAME)" -LogonType Interactive -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet `
        -StartWhenAvailable `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Hours 3) `
        -Priority 7 `
        -MultipleInstances IgnoreNew
    if ($DryRun) {
        Write-GdLog "[dry-run] 将注册任务 $name ($t，最高权限运行，错过后尽快补跑，最长 3 小时)" -Level STEP
        continue
    }
    if ($existing | Where-Object { $_.TaskName -eq $name }) {
        Set-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Settings $settings -Principal $principal | Out-Null
        Write-GdLog "已更新任务 $name" -Level OK
    } else {
        Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
            -Description $Description | Out-Null
        Write-GdLog "已注册任务 $name($t)" -Level OK
    }
}

$manualName = "$taskPrefix-Manual"
if (-not $NoManual) {
    $mAction = New-ScheduledTaskAction -Execute $pwshPath `
        -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$script`" -Tag manual" `
        -WorkingDirectory $PSScriptRoot
    $mPrincipal = New-ScheduledTaskPrincipal -UserId "$($env:USERDOMAIN)\$($env:USERNAME)" -LogonType Interactive -RunLevel Highest
    $mSettings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Hours 3) -Priority 7 -MultipleInstances IgnoreNew
    if ($DryRun) {
        Write-GdLog "[dry-run] 将注册按需任务 $manualName(无时间表，只由 run-daily.cmd / schtasks /Run 触发，免 UAC)" -Level STEP
    }
    elseif (@($existing | Where-Object { $_.TaskName -eq $manualName }).Count) {
        Set-ScheduledTask -TaskName $manualName -Action $mAction -Settings $mSettings -Principal $mPrincipal | Out-Null
        Write-GdLog "已更新按需任务 $manualName" -Level OK
    }
    else {
        Register-ScheduledTask -TaskName $manualName -Action $mAction -Settings $mSettings -Principal $mPrincipal `
            -Description '手动一键跑每日日常(最高权限，免 UAC)' | Out-Null
        Write-GdLog "已注册按需任务 $manualName" -Level OK
    }
}

if (-not $DryRun) {
    Write-GdLog ''
    Write-GdLog '验证：'
    $names = @($Times | ForEach-Object { "$taskPrefix-$($_ -replace ':', '')" })
    if (-not $NoManual) { $names += $manualName }
    foreach ($name in $names) {
        $info = Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
        if ($info) {
            $ni = $info | Get-ScheduledTaskInfo
            $lvl = if ($info.Principal.RunLevel -eq 'Highest') { '最高权限' } else { '普通权限!' }
            Write-Host ("  {0,-20} {1,-8} {2,-9} 下次运行 {3}" -f $name, $info.State, $lvl, $ni.NextRunTime)
        } else {
            Write-Host ("  {0,-20} 不存在" -f $name) -ForegroundColor Red
        }
    }
    Write-GdLog '注意：任务以「最高权限 + 仅在当前用户登录时」运行 —— 端游和 MAA 要管理员，助手又得连本机 GUI/模拟器。锁屏会导致端游截图失败。' -Level WARN
}
