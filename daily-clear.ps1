<#
  daily-clear.ps1 — 每日《明日方舟》(端游 + MAA) 与《重返未来：1999》(MuMu + M9A) 清体力日常编排

  .\daily-clear.ps1 -DryRun                # 只看今天会怎么跑，不碰任何程序
  .\daily-clear.ps1                        # 完整跑一轮(方舟 + 1999)
  .\daily-clear.ps1 -Games ark             # 只跑方舟
  .\daily-clear.ps1 -Ack1999               # 确认"1999 主线活动我已手动打过"，之后每日自动刷
  .\daily-clear.ps1 -ArkShopDone           # 确认"方舟活动商店已领空"，之后剩余理智改刷长草计划
  .\daily-clear.ps1 -RShopDone             # 同上，1999 活动商店
  .\daily-clear.ps1 -ArkShopReset          # 撤销上面的"领空"标记(误点/活动又开新一批兑换)
  .\daily-clear.ps1 -FarmArk               # 方舟本轮只刷"目标材料缺口"(config 里 ark.farmTargets 配的目标)
  .\daily-clear.ps1 -NoElevate             # 禁止自动提权(只跑 1999 或不介意 MAA 连不上端游时用)
  计划任务每日 08:00 / 20:00 调用：daily-clear.ps1 -Tag auto
  权限：方舟端游 + MAA 要管理员。非提权运行时脚本会弹一次 UAC 并以管理员身份重跑自己(-NoElevate 可禁止)。

  退出码：0=全部完成；1=部分完成；2=有失败/这一轮没跑成。

  —— 本文件现在是什么 ——
  它只是一个"命令行外壳"：解析参数、决定跑哪几个模块，然后把整轮流程交给 lib\rust-round.ps1 的
  Invoke-GdDailyRoundDispatch —— 它起内置件跑核内十段骨架(gdcore.exe --daily-round：环境探测 → 提权闸门 →
  手动门 → 判定 → 跑 → 收尾 → 汇总/历史/通知)，返回值就是退出码、由这里 exit。PS 十段原身已于 2026-10-20
  乙批② 划掉：拿不到内置件 / 主闸被人拨关 / 轮上下文装配炸 一律整轮拒跑(2)，这一路不再有回退档。
  这棵树上有哪些游戏，由 lib\<id>-module.ps1 说了算 —— 加一款游戏不用改这里，
  只要往 lib\ 放一份清单；反过来，这里除了"哪个开关属于哪一路"那张表，不该再出现任何游戏名。
#>
# 运行环境：PowerShell 7(pwsh)。本仓库只支持 7，不再为系统自带的 Windows PowerShell 5.1 留兼容退路。
[CmdletBinding()]
param(
    # 不给 ValidateSet：合法值现在等于"名册上注册了哪些模块"，写死 ('all','ark','r1999') 就等于
    # 判了第三个模块死刑。认错了由本文件下面那道入口闸先挡成一句人话(查不到就把这棵树上的模块列出来)，
    # 核内 select_gd_modules 那一路也照同一口径报"没有这个模块：xxx(这棵树上有 …)"——
    # 都比 PowerShell 自己那句"参数值对于 ValidateSet 集无效"好读。
    [string]$Games = 'all',
    [ValidateSet('auto', 'manual')][string]$Tag = 'manual',
    [switch]$DryRun,
    # -AckArk 于 2026-10-12 第 1 刀退场(用户口径「活动要打，需要打通活动…用 maa 进入活动最后一关，
    # 如果通了就说明通关了」)：方舟活动图改由每轮机器自证(state\ark-event-probe.json)，
    # 不再需要人敲确认。1999 那一路的 -Ack1999 照旧保留(它没有可用的自证通道)。
    [switch]$Ack1999,
    [Alias('MarkShopDone')][switch]$ArkShopDone,
    [Alias('MarkRShopDone')][switch]$RShopDone,
    [switch]$ArkShopReset,
    [switch]$RShopReset,
    [switch]$FarmArk,
    [switch]$SkipCleanup,
    [switch]$Force,
    [switch]$NoElevate,
    [string]$ArkStage,
    [ValidateSet('auto', 'idle', 'event', 'rerun')][string]$RPlan = 'auto'
)

$ErrorActionPreference = 'Stop'
# ---------- 内核三件套(公共零件 / 判定门面 / 框架层) ----------
. (Join-Path $PSScriptRoot 'lib\common.ps1')
. (Join-Path $PSScriptRoot 'lib\decisions.ps1')     # 门面：仓库里有哪些判定文件就点哪些
. (Join-Path $PSScriptRoot 'lib\modules.ps1')
. (Join-Path $PSScriptRoot 'lib\rust-round.ps1')   # B-4② 入口转接层：默认档走核内十段骨架，PS 十段路降为显式回退路
# 监督面自 11-02 刀③ 起不在这里装载(旧 dot-source 那行已随 lib\supervisor.ps1 退役；真身 tools\panel-cs\Supervisor.Host.cs，轮档段 7 现问 start-supervisor)

Start-GdTranscript -Tag "daily-$Tag"
Write-GdLog "=== 每日清体力日常开始(tag=$Tag, dryRun=$DryRun, games=$Games)===" -Level STEP

# ---------- 内核接线(K 系列接线批，契约见 docs\gd-core-contract.md) ----------
# 装载已归一进 lib\common.ps1 段尾(刀⑲ 笔(c)，11-07)：dot-source 收尾时内核就已经编好或已回退，
# 原来"显式 $null = Add-GdCore 必须早于第一次 Get-GdSettings"的理由由段尾自动满足——读配置这一步照样走内核。
# 编不成(源码没带 / 编译被拦 / 契约号两舷不一致)时 Add-GdCore 自己吞掉并落 fallback，绝不抛 ——
# 内核永远不是硬依赖，06:00 那一轮不许因为它变成红色。
$coreT0 = Get-Date
$coreNote = Get-GdCoreStatus
if ($coreNote) { Write-GdLog $coreNote -Level WARN }
Write-GdLog (Get-GdCoreReadout)

# ---------- 装配模块 ----------
# 顺序不能反：先注册清单(Import-GdModules 只点 lib\*-module.ps1，那里只有 Register-GdModule 调用)，
# 再在"本脚本的顶层"把这个工程真正要跑的那一路的业务库点进来 —— 这一步不能在函数里做，
# 函数里 dot-source 出来的函数定义会随作用域消失，钩子稍后就调不到(见 modules.ps1 里 Import-GdModules 的注释)。
[void](Import-GdModules)
foreach ($libPath in (Get-GdModuleLibPaths)) { . $libPath }

# 主仓(gd-ops)默认不带任何游戏插件：这棵树上一个助手库都没有时，先把话说清楚再走(退出码 2)。
# 判据与注册表同一把尺(Test-GdModuleInstalled = 助手库在不在)，不在这里另造一套。
if (-not @(Get-GdModuleIds -AvailableOnly).Count) {
    Stop-GdBeforeRun '这棵树上没有安装任何游戏插件。装一个：pwsh -File tools\install-plugin.ps1 -From <插件仓目录>（-List 看已装）'
}

# 提示语里"下一步敲什么"的参数名不再靠这里点一次同步件(11-01 丙形真转接，§11.176 ⑥)：
# Get-GdEntryVerb 每次现问上面的注册表，问得到就用该路清单自己声明的长名，问不到回落干净短名。
# 这个入口两款混在一起，参数名必须带前缀(-AckArk / -RShopDone…)，而判定层的提示语默认写的是
# 单游戏仓库的干净短名 —— 长名由 EntryVerbs.Merged 自己声明(lib\ark-module.ps1 / r1999-module.ps1)，
# 上面 Import-GdModules 已把清单装进注册表，现问即问到；旧那句"点完库再同步一次"防的漏调用形随之出局。

# 没配置这件事由脚本自己用日志的格式说清楚就走(-TellAndExit)：裸 throw 到了顶层是一屏
# "Exception: C:\...\common.ps1:120 / Line | ..." 那种内部报错，双击的人只会以为程序坏了。
$S = Get-GdSettings -TellAndExit

# settings.runtime.coreOff 是函数级热开关(列了就把那件退回 PS 原身)：只能在拿到 settings 之后灌，
# 所以"读配置"这一步本身永远走内核或原身的初始判定，不受这份名单影响(缺键宽容=没配就是全开)。

# ---------- 命令行开关 -> (模块 id, 门) ----------
# -Games 在这里就把话说明白：核内 select_gd_modules 那句 throw 认不得也会报，但报出来是一屏
# "Exception: ...modules.ps1:185 | throw ..."(那是**历史报错样例的引文**——11-03 转接刀把 PS 侧选人件
# Select-GdModules 连同它的 throw 一起摘进了核，这个行号不代表现场，别当活锚读)，对敲错参数的玩家没有意义
# (本仓库的规矩：用户会撞到的错误一律走日志格式，见 common.ps1 的 Stop-GdBeforeRun)。
if ($Games -and $Games -ne 'all' -and -not (Get-GdModule -Id $Games)) {
    Stop-GdBeforeRun "-Games $Games 不认识。这棵树上的模块：$((Get-GdModuleIds -AvailableOnly) -join ' / ')(全部就写 -Games all 或不带这个参数)"
}

# 这张表是本文件唯一认识"哪款游戏对应哪个开关"的地方，也是它作为外壳的本职。
# 顺序保持和拆分前一致：方舟确认 → 1999 确认 → 方舟商店 → 1999 商店。
$gates = @()
if ($Ack1999) { $gates += @{ Id = 'r1999'; Verb = 'Ack' } }
if ($ArkShopDone) { $gates += @{ Id = 'ark'; Verb = 'ShopDone' } }
if ($ArkShopReset) { $gates += @{ Id = 'ark'; Verb = 'ShopReset' } }
if ($RShopDone) { $gates += @{ Id = 'r1999'; Verb = 'ShopDone' } }
if ($RShopReset) { $gates += @{ Id = 'r1999'; Verb = 'ShopReset' } }

# -ArkStage / -RPlan 是两款各自的"这一轮跑什么"，框架不认识它们的含义，只原样转交给该模块的 Resolve 钩子。
# -FarmArk 不是门(不写任何状态)：它只是方舟这一路的"本轮专用材料补刷"开关，语义全在核内(verb resolve-ark-farm-decision)。
$opt = @{ Stage = $ArkStage; Plan = $RPlan; Farm = [bool]$FarmArk }

# 一行写完不用反引号续行：行尾多出一个看不见的空格就会把续行断成两句语法错误，
# 而这类错误偏偏只在"编辑时看不见"的时候才出现。
# B-4②(2026-10-16)：这一句换成了调度器。名字变了、13 枚实参一字未动，语义也一字未动(返回退出码由这里 exit)；
# 它把整轮交给 lib\rust-round.ps1 → gdcore.exe --daily-round。**乙批②(2026-10-20)起这里没有第二条腿了**：
# PS 十段原身(lib\modules.ps1 的 Invoke-GdDailyRound)与它的两枚回退开关(GD_RUST_ROUND_OFF／
# runtime.rustRoundOff)一块退役 ⇒ 拿不到内置件就是整轮拒跑(2)，"内核永远不是硬依赖"这句老话在轮档这路作废。
$code = Invoke-GdDailyRoundDispatch -S $S -ElevateScript (Join-Path $PSScriptRoot 'daily-clear.ps1') -BoundParams $PSBoundParameters -Gates $gates -Ids @($Games) -Opt $opt -Force:$Force -DryRun:$DryRun -SkipCleanup:$SkipCleanup -NoElevate:$NoElevate -NameField Id -NoticeOwner '每日' -Tag $Tag

# 收尾再印一次：这一句里的 子进程= 与 挂钟= 就是本宿主的性能读数出口(基线与本轮实测见 docs\gd-core-contract.md 的接线实录)
Write-GdLog ('收尾 ' + (Get-GdCoreReadout -ElapsedSec ((Get-Date) - $coreT0).TotalSeconds))
exit $code
