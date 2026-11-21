# rust-round.ps1 — B-4②「入口转接」：把合体入口那一句日常交给核内的十段骨架
<#
  本文件是什么(2026-10-16 落定，判据与批次依据见 docs\rust-core-plan.md §11.64 与其就地补记、§11.65)：
  它是 daily-clear.ps1 与 tools\rustcore\gdcore.exe(--daily-round) 之间的**转接层**，不是第三条日常流程。
  十段的编排、话术、退出码判据全在核内(main.rs 的 daily_round_chain 是事实源)；这里只干三件事——
    ① 把命令行那 13 枚实参装配成 ctx(一轮一定稿，键名册 15 枚逐枚点名)；
    ② 起核、按 ask 回执把**外发那些格**交给这棵树上的真身件去做，再写 res 起下一轮；
    ③ 把 done 里的 result.code 交给入口 exit(真退出码只在这一格，rc 恒 0，两者不许混)。

  ⚠ 外发名册的**枚数随批次变**(10-17 R9-a「四格收编」由 18 收成 13)，唯一口径是下面那枚
  `Get-GdRoundOuterActs`，本头注不抄数(抄了就会变成第二处事实源、且是第一处过期那一处)。
  同批新立的 `--relaunch-entry` 是一枚**可选入程参数**：值现取现交、落 %TEMP%、会话结束即删，
  绝不进 journal／ctx／ask／侧信道；递不到就不带 ⇒ 核那一臂答 None ⇒ 老路照旧(见那两处注释)。

  默认档(2026-10-16 所有者拍板)：**默认走这里**。它原来那一条显式回退路＝PS 十段(lib\modules.ps1 的
  Invoke-GdDailyRound)，已于 2026-10-20 乙批② 连真身一块划掉(退役单元「断兜底」)：拿不到内置件、
  主闸被人拨关、ctx 装配炸 —— 这三种形状现在一律是**整轮拒跑(2)**，这一路不再有第二条腿。
  ⚠ 代价照实在册(§11.105)，两条不对称写在这里而不是藏在日志里：
    · 出货那棵树带内置件 gdcore.exe(进包名单 1 件、过契约号门才进) ⇒ 玩家那一路不受影响；
    · 公开仓按口径是"源码进仓、产物不入仓" ⇒ 拿 git 克隆直跑入口的人从此需要先自己编内置件，
      落点是一句「先跑 tools\rustcore\build.ps1」，不是悄悄换人跑。

  原先「为什么这里不动 Invoke-GdDailyRound 一个字」那一整段随真身一块退场：它讲的是钉位与
  tree-drift，而乙批① 已经把轮骨架门 old 侧换成 git blob、第三侧摘除 ⇒ 改那五份真身不再当场红。

  为什么不复用 lib\ark-daily.ps1 / lib\r1999-daily.ps1 那两枚同名候选序件：那两处的理由(装载顺序
  不许变成隐式依赖)在这里同样成立，且本文件只被 daily-clear.ps1 点，而那两路各自的业务库是**条件装载**的
  (树上没装 1999 就没有 r1999-daily.ps1)。代价照旧登记进 §11.44 那笔"多处一份字面"的欠账：三处。

  ⚠ 改口(11-04 转接刀·运行面刀⑦ 笔(c/d))：上面那段自此按历史引文读——那两本编排岛整件出仓(岛外零活调用点
  逐枚现拍的账在 §11.235)，本文件里指它们的句子当场没有现树对面了。逐处登记(按所在段点名，不写行号：
  行号随本批增删而漂，现树真值一律现读)——本段、Get-GdRoundOuterActs 里那句"不要把驱动档白名单当这枚名册用"、
  Get-GdRoundMaaRunPayload 与 Get-GdRoundM9aRunPayload 两枚头注里"同一名册／同把"那两句、m9aRun 段里
  instance 那句"真身是判定层按方案挑的"、以及十段回放里那句"口径照两枚现出口定档"。留着它们不是给已出仓的
  文件续命，是留着**为什么这样取键**的理由——那几条理由一个字没变：装载顺序不许成隐式依赖、取键逐枚点名、
  visionApiKey 不许进核。对面真身如今在 tools\rustcore\src\main.rs(daily_round_chain 与驱动档链体)和
  tools\panel-cs\*(Chain.cs／Round.Data.cs／Orchestration.cs)。

  ⚠ 改口(11-06 转接刀·运行面刀⑪ 笔(c/d))：上面那段点名的"Get-GdRoundMaaRunPayload 与 Get-GdRoundM9aRunPayload
  两枚头注里『同一名册／同把』那两句"随两枚定义体一起出仓——轮 ctx 的装配整块换核(tools\panel-cs\Round.Host.cs
  的 build-round-ctx 直身)，本文件自此只剩"递壳才拿得动的那几枚旋钮＋回放"。那几条理由一个字没变，只是家换了：
  取键逐枚点名、visionApiKey 不许进核、名册一轮一定稿、pid 必须由壳写明——如今住在 Round.Host.cs 的头注与
  panel-cs --roundtest 的 build-round-ctx 那一节里，验收面随 test-ark 那两格一起搬靶；
  逐字节等值的同帧证据在 ship 账本 raw\k948-cut11-parity-ps51.log 与 raw\k948-cut11-compare.log，
  那一节每一格的牙在 raw\k947-cut11-selftest-mutation-report.txt(逐发 mutation，账上现拍 CELLS-UNCOVERED=NONE)。

  ⚠ 本文件里**不许出现任何判据**：判定、话术、三档退出码、崩路语义都在核内；这里只做"取数、装配、回放、回执"。
  多打一行日志就等于同一句日志打两遍(链上每一句由核内侧信道产生，这里只照原级别回放)。
#>

function Get-GdRoundCoreExe {
    <# 内置件 gdcore.exe 的候选序：出货树形态(随包走) → cargo 形态(开发树直跑)。
       两者都不在返回 ''，由调用方处理 —— 与两枚现出口那两处逐字同形(第三份字面，欠账登记见本文件头注)。 #>
    $cands = @(
        (Join-Path $global:GD.Root 'tools\rustcore\gdcore.exe'),
        (Join-Path $global:GD.Root 'tools\rustcore\target\release\gdcore.exe')
    )
    foreach ($c in $cands) { if (Test-Path -LiteralPath $c) { return $c } }
    return ''
}

function Get-GdRoundOuterActs {
    <# **外发 1 格的名册(唯一口径)**。尺子的出处＝§11.64 ③：按"码面动作字面量 × 两路"展开，
       与核内 round_answer 那个 `_ => return Ok(None)` 的注释名单同源；直答那一族不在这里(核内自己答)，
       take_act 那三格(mark/obsrec/admin)不发 ask 因此也不在。
       ⚠ **R9-a(10-17) 把这一族从 18 收成 13**：`round-history:ark`／`round-history:r1999`／`round-probe:ark`／
       `svdetail`／`round-screensaver` 五枚改由核内直答(换答方不换语义，判据与话术仍在 panel-cs 那四枚 verb 里)，
       这里**同批删臂**——两侧只改一边就是"核还出 ask 而壳不认识"＝整轮拒跑(2)，对账由 round 门的应答册钉。
       ⚠ **刀①(2026-11-03) 再收四枚，13→9**：`round-gate:ark`／`round-gate:r1999`／`stop:ark`／`stop:r1999`
       改核内直答(问三枚 set-* verb 与 stop-maa-and-game/stop-m9a(+stop-mumu)，问法与落账形状逐字不动)，
       臂与名册同批摘；closeEmulatorAfter 随 stop 臂回 ROUND_RUNTIME_KEYS(§11.55 ③ 那条取键裁决翻案)。
       ⚠ **刀②(2026-11-03) 再收两枚，9→7**：`round-preview:ark`／`round-preview:r1999` 改核内直答
       (问 apply-maa-daily-config/apply-m9a-instance/ark-find-window 与 read-m9a-connection/mumu-state，
       日志面四段/三段逐字不动)，臂与名册同批摘；模块清单的 Preview 钩子体暂留(摘臂不摘格，1eq.④ 先例)。
       ⚠ **刀③(2026-11-03) 再收两枚，7→5**：`round-resolve:ark`／`round-resolve:r1999` 改核内直答
       (判定面进核：六发问序走判定族十枚 verb——compose-decision-inputs/read-ark-activity(注入缝)/
       decide-ark/apply-ark-daily-intent×2/resolve-ark-farm-decision 与 read-r1999-activity/
       decide-r1999/apply-r1999-daily-intent/r1999-instance/read-m9a-connection，data 形状逐字同 PS 臂)，
       臂与名册同批摘；Resolve 钩子体与 decisions 三本暂留(摘臂不摘格，1eq.④ 先例)。
       ⚠ **刀⑧(2026-11-04) 再收两枚，5→3**：`updgate`／`audit` 改核内直答(两枚闸的"起工具子进程＋挑行＋
       全量拼串"整段搬进 panel-cs 的 Gate.Host.cs，问 run-m9a-update／run-1999-event-audit 两枚 verb，
       落账仍是小写三枚 {code,line,joined})，臂与名册同批摘；
       tools\m9a-update.ps1／tools\1999-event-audit.ps1 两把**工具本体**按表账「不许动」永久留壳——
       这一刀搬的是壳里那 23 行起子进程的写法，不是那 404 行的工具真身。
       ⚠ **刀⑩(2026-11-05) 再收两枚，3→1**：`round-supervisor-start`／`round-supervisor-stop` 改核内直答
       (问 start-supervisor／stop-supervisor 两枚 verb，真身 tools\panel-cs\Supervisor.Host.cs 一字未动；
       回执仍是 Available/Err 两枚键，段 7 的读法与那句"Available 缺席按在处理"照旧)，臂与名册同批摘；
       失败档不冒泡：hooks.call 抛 Err 时核内交 `Available=false/Err=""`(与刀③ 摘回落臂那档同形)。
       ⚠ 摘这两臂**顺带走了段 8 睡眠屏蔽的触发点**：它原来钉在"看到 round-supervisor-start 那一发 ask"上，
       那一发自此不再问出去 ⇒ 屏蔽改由侧信道那句「已临时禁止系统睡眠(跑完自动恢复)」驱动(见枚函数头注)。
       ⚠ **`round-elevate` 刻意留在名册里**：它按拍板"全收编"走 `--relaunch-entry` 那枚**可选**入程参数，
       缺参数／文件读不动／形状坏 ⇒ 核答 `None` ⇒ 照旧出 ask(§11.66 那条"恒绿的判据就是没验过的判据"的正面用法：
       入程通道是新立的，必须留一条"没递到就退回老路"的落点，而不是把它升级成整轮拒跑)。
       刀⑩ 之后它是名册上**仅存**的一枚：这一档问的是"这一轮要不要另起一台提升过的宿主"，核内起不了自己
       ——留着是设计，不是漏摘(同一句判定在核内 §28 的 S28_ROSTER 头注里，两册同数)。
       白名单之外收到的一律当场拒跑("先改 main.rs 再改这里，不许放行"——两枚现出口那条老规矩)：
       两侧有一边漏改，宁可这一轮没跑，也不许把不认识的动作当成成功回执吞掉。
       ⚠ 不要把驱动档那十四枚白名单(lib\ark-daily.ps1)当这枚名册用，那是另一族的数(§11.64 ③ 倒数第二段)。 #>
    return @(
        'round-elevate'
    )
}

function Test-GdRustRoundPath {
    <# 主闸(乙批② 起只剩这一把)：读的就是"内置件整体上不上路径"那两枚现成的键，与 lib\common.ps1 里
       那把已随 10-24 刀④ 出仓的换头主闸同源同判据($env:GD_RUST_OFF=1、runtime.rustOff 含 'gdcore')；轮档不许越过主闸。
       ⚠ 这里**不再读** $env:GD_RUST_ROUND_OFF 与 runtime.rustRoundOff：那两枚开关的内容是"退回 PS 十段"，
       十段真身已随乙批② 划掉 ⇒ 键留着就是两处"拨了没反应"的骗人配置，比没有键更坏。
       (判据变更本要拍板，这一条的所有者决定就是 10-20 那句「两扇都要，按乙→丙排」里的乙档。)
       返回 $false 的语义也从"走原身"换成"这一轮拒跑(2)"(见 Invoke-GdDailyRoundDispatch)：
       有人拨了"不许起内置件"、而树上已没有第二条腿 ⇒ 只能停下来把话说清楚，不许嘴上答应、手上照样起核。
       缺键/形状坏一律当**不关**(缺省＝起核)，与那把已出仓的换头主闸同形(缺键宽容)；
       真正的现场保险不在这把闸上：拿不到内置件或判定 exe 时 dispatch 自己会拒跑(不是跳过、不是假成功)。 #>
    param($S)
    try { if ($env:GD_RUST_OFF -eq '1') { return $false } } catch { }
    try {
        if ($S -and $S.runtime) {
            $off = @()
            if ($S.runtime.rustOff) { $off = @($S.runtime.rustOff | ForEach-Object { [string]$_ }) }
            if ($off -contains 'gdcore') { return $false }
        }
    } catch { }
    return $true
}

function Start-GdRoundHostNoSleep {
    <# 宿主侧的睡眠屏蔽(返回答yes/no，不打日志——那句"已临时禁止系统睡眠"由核内骨架说，这里再说一遍就是打两遍)。
       为什么必须有这一枚：SetThreadExecutionState 的有效期是**调用进程活着的那段时间**，而驱动协议一 ask 一进程 ——
       核内段 8 那次直答设的位在 gdcore 退出时就散了。真身那一版里设位的是 daily-clear 自己这个进程，
       它整轮都在 ⇒ 屏蔽真的生效。转接之后这个"谁在设"的事实变了，只能在壳侧补回来，否则 -BlockSleep 变成一句空话。
       落点(刀⑩ 2026-11-05 换代)：**侧信道里核内那句「已临时禁止系统睡眠」落地的那一刻**。
       它原来钉在"看到 round-supervisor-start 那一发 ask 之后"——那是真身顺序(段 7 监督 → 段 8 屏蔽)留在
       转接层里的最后一个形状痕迹；监督两臂改核内直答后那一发不再问出去 ⇒ 触发点跟着换轴，判据不放宽：
       那句人话只在核内段 8 **真设过位并自证过**时才印，所以"看到这句"与"该补位"仍是同一件事。
       dry-run／门只记录／判定炸／提权重启那几档段 8 根本不出场 ⇒ 这句不落地 ⇒ 不误设(与换代前同读数)。 #>
    param([Parameter(Mandatory)]$S)
    try {
        if (-not $S.runtime.blockSleep) { return $false }
        Add-GdSleepTypeDef
        $flags = [GdPower]::ES_CONTINUOUS -bor [GdPower]::ES_SYSTEM_REQUIRED
        if ($S.runtime.keepScreenOn) { $flags = $flags -bor [GdPower]::ES_DISPLAY_REQUIRED }
        [void][GdPower]::SetThreadExecutionState($flags)
        return $true
    } catch { return $false }
}

function Stop-GdRoundHostNoSleep {
    <# 撤位只在真设过时做(Exit-GdNoSleep 那句 $global:GdSleepHandle 的语义)；撤失败一句都不吭(真身整枚 catch{})。 #>
    param([bool]$WasSet)
    if (-not $WasSet) { return }
    try { [void][GdPower]::SetThreadExecutionState([GdPower]::ES_CONTINUOUS) } catch { }
}

function Invoke-GdDailyRoundRust {
    <# 轮档执行器：问-答-重放的驱动循环。返回**本轮退出码**(0/1/2)，由入口 exit；
       拒跑那一族不裸 throw，一律走 Stop-GdBeforeRun 的人面形状(一句"这一轮没跑：…"+退 2) ——
       这一档的调用方是入口自己，没有人在上面 catch，裸 throw 到顶层就是一屏 "Exception: rust-round.ps1:…"。
       回执形状是协议：ok 真不许带 err 键、假不许带 data 键(main.rs drive_merge_res 逐字校验)。
       前后缀话术由核内拼装(「手动门没敲进去(id verb)：」「…这一路今天判定不出来：」「…的预览没做成：」
       「活动图自证没做起来：…(不影响本轮)」)，这里**只回异常原文**，不许把话术抄一份到壳侧。 #>
    param(
        [Parameter(Mandatory)]$S,
        [Parameter(Mandatory)]$Ctx,
        [Parameter(Mandatory)][string]$ElevateScript,
        [hashtable]$BoundParams = @{},
        [string]$CoreExe = '',
        [string]$PanelCs = ''
    )
    # 两枚内置件由**调用方**(dispatch)定妥才进来：那一边负责"拿不到就退原身"那一档，
    # 走到这里还缺就是内部装配漏了 —— 这一处只把话说死，不再自己找第二遍(两处各找一次会给出两个不同答案)。
    if (-not $CoreExe -or -not $PanelCs) { Stop-GdBeforeRun '转接层装配漏了内置件或判定 exe 的落点(内部错，非现场问题)' }
    $exe = $CoreExe
    $panel = $PanelCs
    # 刀⑧(2026-11-04) 起这里不再有"外挂子进程"：曾有的 `$selfEngine`（引擎自路径）随两枚闸臂同批出册。

    # 一次日常 = 一本账：ctx 换了 journal 就不能复用(内核按 init 与 ctx 全等校验)，每次开头清场重起。
    $journal = Join-Path $global:GD.State 'rust-round-journal.ndjson'
    $ctxFile = Join-Path $global:GD.State 'rust-round-ctx.json'
    $resFile = Join-Path $global:GD.State 'rust-round-res.json'
    $sink = Join-Path $global:GD.State 'rust-round-log-sink.ndjson'
    foreach ($f in @($journal, $resFile, $sink)) { if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force } }
    # 侧信道一本会话一个文件(核侧只追加不截断)：偏移在这里归零，之后由 Read-GdSinkIncrement 按**字节**推进。
    # 一律用 long：[ref] 会记住初值的类型，这里留 int 以后写 long 会被往回截(偏移算错=同一句日志重放)。
    $sinkOffset = [long]0
    Write-GdJson -Path $ctxFile -Object $Ctx

    # R9-a：提权那一发的**入程参数**(可选)。拼参语义仍住 PS 侧(`Get-GdRelaunchArgList` 是事实源、不搬)……（细节见该帧）
    $entryFile = ''
    try {
        $entryArgs = @(Get-GdRelaunchArgList -Script $ElevateScript -Bound $BoundParams -Force @('-NoElevate'))
        $entryFile = Join-Path ([System.IO.Path]::GetTempPath()) ('gd-round-entry-' + [guid]::NewGuid().ToString('N') + '.json')
        Write-GdJson -Path $entryFile -Object ([ordered]@{ Script = [string]$ElevateScript; ArgList = $entryArgs })
    } catch {
        $entryFile = ''
        Write-GdLog "提权入参没拼出来($($_.Exception.Message)) → 本轮的 round-elevate 照旧由转接层代答" -Level WARN
    }

    # 外层硬闸(10-20，与两枚现出口同源同形)：这一档手上**没有**专用的"round 超时"，取两路业务超时里宽的那枚 +15。
    $hardMin = [int]$S.runtime.arkTimeoutMinutes
    $m9aMin = [int]$S.runtime.r1999TimeoutMinutes
    if ($m9aMin -gt $hardMin) { $hardMin = $m9aMin }
    if ($hardMin -le 0) { $hardMin = 85 }
    $hardMs = ($hardMin + 15) * 60000
    $outer = @(Get-GdRoundOuterActs)
    $esHeld = $false
    # 刀⑩(2026-11-05)：屏蔽的触发点从 ask 搬进侧信道回调(见 Start-GdRoundHostNoSleep 头注)，
    # 回调里要写回外层那枚 $esHeld —— 闭包**读**外层变量靠动态作用域链(实测成立)，**写**只会写进
    # 回调自己的作用域(当场丢)，所以走 [ref] 框：与下面 `-SinkOffset ([ref]$sinkOffset)` 同一形制。
    $esBox = [ref]$esHeld
    $round = 0
    try {
        while ($true) {
            $round += 1
            if ($round -gt 500) { Stop-GdBeforeRun "转接层驱动轮次超上限(疑似死循环)：journal=$journal" }
            # 信封落点每轮换新文件，防读到上一轮读数；写不下去时 gdcore 退 stdout 末行(内核侧既定纪律)
            $envFile = Join-Path ([System.IO.Path]::GetTempPath()) ('gd-round-env-' + [guid]::NewGuid().ToString('N') + '.json')
            try {
                $coreArgs = @(
                    '--daily-round', '--i-round', '--journal', $journal, '--ctx', $ctxFile,
                    '--res', $resFile, '--panel-cs', $panel, '--state', $global:GD.State,
                    '--log-sink', $sink, '--root', $global:GD.Root, '--out', $envFile
                )
                # 第八道闸之外的都是**可选**参数：这一枚不带 ⇒ 核那一臂答 None ⇒ 照旧 ask(见上面入参那段)
                if ($entryFile) { $coreArgs = $coreArgs + @('--relaunch-entry', $entryFile) }
                $cap = Start-GdNativeStreamed -Exe $exe -SinkPath $sink -SinkOffset ([ref]$sinkOffset) -Arguments $coreArgs -OnSinkRow {
                    param($r)
                    $lvl = [string]$r.Level
                    $txt = [string]$r.Text
                    if ($lvl -and ($lvl -ne 'INFO')) { Write-GdLog $txt -Level $lvl } else { Write-GdLog $txt }
                    # 刀⑩：段 8 睡眠屏蔽的触发点。核内那句「已临时禁止系统睡眠(跑完自动恢复)」是**段 8 自己…（细节见该帧）
                    if (-not $esBox.Value -and ([string]$r.Text).Contains('已临时禁止系统睡眠')) { $esBox.Value = Start-GdRoundHostNoSleep -S $S }
                } -HardWaitMs $hardMs
                $envText = ''
                if ([int]$cap.StreamTimeouts -gt 0) {
                    # 口径照两枚现出口(lib\ark-daily.ps1 那处 #473 定档)：这一格每轮必现、不是异常也不是回归……（细节见该帧）
                    Write-GdLog "转接层：收流按有界等待放弃 $([int]$cap.StreamTimeouts) 路 stdout/stderr(外部常驻服务持写端，stdout 兜底那一路本轮不可用；真日志见 --log-sink)"
                }
                if ($cap.WaitTimedOut) {
                    # 第三种病(10-20，与两枚现出口同形)：核没退出、被外层硬闸收尸。这一支往下必然走…（细节见该帧）
                    Write-GdLog "转接层：核 $([int]($hardMs / 1000))s 仍没退出，已按外层硬闸结束它(它是没答，不是答错) —— 本轮按失败收" -Level WARN
                }
                if (Test-Path -LiteralPath $envFile) { $envText = [System.IO.File]::ReadAllText($envFile) }
                else {
                    foreach ($ln in @($cap.Lines)) { $t = ([string]$ln).TrimStart(); if ($t.StartsWith('{')) { $envText = $t } }
                }
                if (-not $envText) {
                    Stop-GdBeforeRun "转接层：gdcore 没给出信封(rc=$($cap.ExitCode))；输出末三行：$((@($cap.Lines) | Select-Object -Last 3) -join ' / ')"
                }
                $envelope = $envText | ConvertFrom-Json
                if ([string]$envelope.role -ne 'B3b-round-daily') {
                    Stop-GdBeforeRun "转接层：信封 role 不对($($envelope.role)) —— 这枚 gdcore.exe 不是轮骨架档那件？"
                }
                $want = Get-GdRustContract
                if ([string]$envelope.contract -ne $want) {
                    Stop-GdBeforeRun "转接层：gdcore 契约号($($envelope.contract))与壳侧期望($want)不一致 —— 先重跑 tools\rustcore\build.ps1 再跑本轮"
                }
                # 链上每一句已在上面边跑边从侧信道按原级别回放完毕；信封 logs 键留而不读(B-2c② 起恒为空)，读两遍就是打两遍。
                $kind = [string]$envelope.kind
                if ($kind -eq 'done') {
                    $dr = $envelope.result
                    # 真身那句 Write-Host $summary(reachedSummary 为假的那几档＝没走到汇总，本来就不该印)
                    if ([bool]$dr.reachedSummary) { Write-Host ([string]$dr.summary) }
                    return [int]$dr.code
                }
                if ($kind -eq 'fail') {
                    # Fail 信封＝核内**进十段之前**那两句 throw(段 1 选人／段 2 之前 ctx 坏)……（细节见该帧）
                    Write-GdLog ([string]$envelope.error) -Level ERROR
                    return 1
                }
                if ($kind -ne 'ask') { Stop-GdBeforeRun "转接层：信封 kind 不认识($kind) —— 协议两侧不同源？" }

                $seq = [int]$envelope.seq
                $act = [string]$envelope.action
                # 刀⑩：信封的 params 面在壳侧**再无消费者**(唯一活着的 round-elevate 只回 Code，监督臂那枚…（细节见该帧）
                if ($act -notin $outer) {
                    # 白名单之外走拒跑通道而不是 throw：不认识的动作＝协议不同源，既不许被当成"动作失败"吞掉……（细节见该帧）
                    Stop-GdBeforeRun "转接层：收到不认识的动作 '$act'(seq=$seq) —— 先改 main.rs 再改这里，不许放行"
                }
                $ok = $true
                $data = $null
                $err = $null
                try {
                    switch ($act) {
                        'round-elevate' {
                            # R9-a 之后这一臂是**回落通道**：入程参数递到了就由核内直答(改调 start-elevated-round……（细节见该帧）
                            $ec = Start-GdElevatedSelf -Script $ElevateScript -Bound $BoundParams -Force @('-NoElevate')
                            $data = [pscustomobject]@{ Code = [int]$ec }
                        }
                        # ---- 刀⑩ 收编(2026-11-05)：`round-supervisor-start`／`round-supervisor-stop` 两臂摘净——…（细节见该帧）
                    }
                } catch {
                    $ok = $false
                    $err = $_.Exception.Message
                }
                $rf = [ordered]@{ seq = $seq; action = $act; ok = $ok }
                if ($ok) { $rf.data = $data } else { $rf.err = $err }
                Write-GdJson -Path $resFile -Object $rf
            } finally {
                if (Test-Path -LiteralPath $envFile) { Remove-Item -LiteralPath $envFile -Force }
            }
        }
    } finally {
        Stop-GdRoundHostNoSleep -WasSet $esHeld
        # 入程参数那一份带本机绝对路径，会话一结束就删(删失败一句不吭：那是 %TEMP% 的残渣，不是本轮的结论)
        if ($entryFile -and (Test-Path -LiteralPath $entryFile)) { try { Remove-Item -LiteralPath $entryFile -Force } catch { } }
    }
}

function Invoke-GdDailyRoundDispatch {
    <# 调度器(乙批② 起只有一条腿)：入口链不动、参数面一字不加，它只做四件事——
       读主闸、定位内置件与判定 exe、装配 ctx、把整轮交给 Invoke-GdDailyRoundRust。
       返回退出码(0/1/2)，三分语义与核内 done 信封的 result.code 同源 ⇒ 入口那句 exit $code 不必改。
       ⚠ 参数表**必须与核内 daily_round 那 13 枚实参一一对应**(入口转接批立的那族锚验的就是这件事)，
         差别只在下面两枚门/自检专用注入档：
         -CoreExe/-PanelCs 让轮骨架门的转接层侧能喂假内置件与假判定 exe(生产永不带，同 --fixture-* 那一族)。
       ⚠ 原「拿不到内置件就落一句 WARN、再逐字走 PS 原身十段」那一档与「显式拨关走原身」那一档，
         已于 2026-10-20 乙批② 一块换成**拒跑**(Stop-GdBeforeRun：一句人话 + 退 2，不是一屏内部报错)。
         "内置件永远不是硬依赖"那句老话在**轮档这一路**不再成立(单游戏那两路的 legacy 照旧在)；
         代价、受影响的形态(公开仓 git 克隆侧)与两枚退场的开关都写在本文件头注。 #>
    param(
        [Parameter(Mandatory)]$S,
        [Parameter(Mandatory)][string]$ElevateScript,
        [hashtable]$BoundParams = @{},
        [AllowEmptyCollection()][AllowNull()][array]$Gates = @(),
        [string[]]$Ids = @(),
        [hashtable]$Opt = @{},
        [switch]$Force,
        [switch]$DryRun,
        [switch]$SkipCleanup,
        [switch]$NoElevate,
        [ValidateSet('Id', 'ShortName')][string]$NameField = 'Id',
        [string]$NoticeOwner = '每日',
        [string]$Tag = 'manual',
        [string]$CoreExe = '',
        [string]$PanelCs = ''
    )
    if (-not (Test-GdRustRoundPath -S $S)) {
        Stop-GdBeforeRun '主闸把内置件拨下了线(GD_RUST_OFF=1 或 runtime.rustOff 含 gdcore)，而轮档这条路的 PS 十段兜底已退役 ⇒ 本轮不跑(要把日常交回内置件就把主闸拨回来)'
    }
    $core = $CoreExe
    if (-not $core) { $core = Get-GdRoundCoreExe }
    $panel = $PanelCs
    if (-not $panel) { try { $panel = Get-GdCliExe } catch { $panel = '' } }
    if (-not $core -or -not $panel) {
        $miss = '内置件 gdcore.exe'
        if (-not $core -and -not $panel) { $miss = '内置件 gdcore.exe 与判定 exe' }
        elseif (-not $panel) { $miss = '判定 exe' }
        Stop-GdBeforeRun "转接层拿不到${miss} ⇒ 本轮不跑(开发树先跑 tools\rustcore\build.ps1 出内置件；用 git 克隆的公开仓按口径不带编译产物，这一路从乙批② 起没有 PS 十段兜底)"
    }
    $ctx = $null
    try {
        # 刀⑪(2026-11-06)：轮 ctx 的装配整块进核(panel-cs 的 build-round-ctx 直身)，壳这一侧只递**壳才拿得动**的…（细节见该帧）
        $ctx = Invoke-GdCli -Verb 'build-round-ctx' -Body @{
            ids         = $Ids
            gates       = $Gates
            opt         = $Opt
            force       = [bool]$Force
            dryRun      = [bool]$DryRun
            skipCleanup = [bool]$SkipCleanup
            noElevate   = [bool]$NoElevate
            nameField   = [string]$NameField
            noticeOwner = [string]$NoticeOwner
            tag         = [string]$Tag
            logPath     = [string]$global:GD.LogFile
            ownerPid    = $PID
        }
    } catch {
        # ctx 装配炸＝连"这一轮要问哪些格"都没定下来，比真身的"判定不出来"更早 ⇒ 同样走拒跑通道，别一屏内部报错
        Stop-GdBeforeRun "本轮的轮上下文没装配起来：$($_.Exception.Message)"
    }
    return Invoke-GdDailyRoundRust -S $S -Ctx $ctx -ElevateScript $ElevateScript -BoundParams $BoundParams -CoreExe $core -PanelCs $panel
}
