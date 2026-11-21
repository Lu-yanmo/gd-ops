# lib/modules.ps1 — 框架层：游戏模块的注册表与装载器("跑一轮"那一格已交出去，见本文件末尾那段)
#
# 为什么要这一层：在它有之前，"今天跑哪几路、每路要探哪几条路径、要不要管理员、跑完调谁收尾、
# 汇总里先写谁"这些问题，答案是写死在 daily-clear.ps1 的 ValidateSet / foreach (@('ark','r1999')) /
# Test-Path lib\maa.ps1 里的，同一套流程还被合体入口和两份单游戏薄壳各抄一遍。
# 现在：模块只声明"我叫什么、要哪几条路径、每一步调哪个函数"，框架只认这份声明。
# 加一款游戏 = 往 lib\ 里放一份 <id>-module.ps1，入口和面板都不用再改。
#
# 装载方式沿用本仓库一贯的口径 —— 按文件在不在决定有没有那一路，不猜配置：
# Import-GdModules 扫 lib\*-module.ps1，按文件名排序逐个点进来，各自 Register-GdModule。
# 单游戏导出仓库里只有那一份模块文件，于是注册表天然只有一款，入口一个字都不用改。
#
# 依赖：本文件只用 lib\common.ps1 里的零件(Write-GdLog / Get-GdSettings …；Add-GdHistory 曾在此列，已于 11-07 刀⑰ 笔(c)-1 整枚出仓，死名随摘)，
# 所以调用方必须已经点过 common.ps1；反过来本文件不点任何游戏专有库(那正是模块清单的事)。

# 注册表：[id] -> 清单对象。用全局是因为这些库都是 dot-source 进入口脚本作用域的，
# 各入口自己那份 $script: 变量互相看不见；和 $global:GD / $global:GdStageTable 同一个路子。
if (-not (Get-Variable -Name GdModules -Scope Global -ErrorAction SilentlyContinue)) {
    $global:GdModules = [ordered]@{}
}

# 一个模块清单必须齐这些东西才算能用。少哪一项就报哪一项 —— 将来加模块的人只会看这一处。
$script:GdModuleRequiredKeys = @('Id', 'Name', 'ShortName', 'AssistantLib', 'RequiresAdmin', 'ElevationFlag', 'AdminWhy', 'AdminHint', 'AdminOkNote', 'SettingsBlock', 'PathKeys', 'EntryVerbs', 'Libs', 'Hooks')   # 11-04 刀⑦ 笔(a)：十五枚→十四枚，OrchestrationLib 随合体判据换源退役(判语见 lib\decisions-common.ps1 的 Test-GdMergedEntry 头注)
$script:GdModuleRequiredHooks = @('EnvProbes')   # 11-04 刀⑤ 七枚→一枚(判语见本文件末尾那段，此处一行不改行数是为了不掀漂 :88 那枚名册循环的表账锚)

function Register-GdModule {
    <# 模块清单入库。同一棵树里同一个 Id 注册两次 = 有两份 <id>-module.ps1(或者入口多点了一次)，
        直接把后一份丢掉并说清楚：静默覆盖会让"我改的到底是哪一份"变成排查噩梦。 #>
    param([Parameter(Mandatory)]$Manifest)
    $id = [string]$Manifest.Id
    if (-not $id) { throw 'Register-GdModule：清单里没有 Id' }
    if ($global:GdModules.Contains($id)) {
        Write-GdLog "模块 $id 已经注册过(注册表不动，本次忽略)—— 检查 lib\ 下是不是有两份 $id-module.ps1" -Level WARN
        return $null
    }
    $global:GdModules[$id] = $Manifest
    return $Manifest
}

function Get-GdModules {
    <# 注册表里的模块，按注册顺序(= 文件名顺序)。-AvailableOnly 只要这棵树上真的装了的那几路：
        判据现问 Test-GdModuleInstalled(唯一那把尺)——这里原本是"助手库在不在"的第二份 Test-Path 写法，
        11-04 刀⑦ 笔(a) 收成一处(§11.186 那笔口径)，合体判据 Test-GdMergedEntry 自此也走这一处。 #>
    param([switch]$AvailableOnly)
    $all = @($global:GdModules.Values)
    if (-not $AvailableOnly) { return $all }
    return @($all | Where-Object { Test-GdModuleInstalled -Module $_ })
}

function Get-GdModule {
    param([Parameter(Mandatory)][string]$Id)
    if (-not $global:GdModules.Contains($Id)) { return $null }
    return $global:GdModules[$Id]
}

function Get-GdModuleIds {
    param([switch]$AvailableOnly)
    return @((Get-GdModules -AvailableOnly:$AvailableOnly) | ForEach-Object { [string]$_.Id })
}

function Test-GdModuleInstalled {
    <# 这一路"装没装在这棵树上"——唯一判据 = 助手库在不在(→ "这一路能不能跑")。
        11-04 刀⑦ 笔(a)：-Key 那把「编排文件在不在」的第二尺随 OrchestrationLib 必需键同批退役，
        合体判据 Test-GdMergedEntry 与 Get-GdModules -AvailableOnly 自此都现问这一处。
        注意：合体/单游戏的判据函数 Test-GdMergedEntry 住在 lib\decisions-common.ps1(老消费方都在那里用)，
        本文件不重复定义同名函数 —— 两份库先后 dot-source 时"后点的覆盖先点的"是顺序依赖坑，别踩。 #>
    param([Parameter(Mandatory)]$Module)
    $f = [string]$Module.AssistantLib
    if (-not $f) { return $false }
    return (Test-Path -LiteralPath (Join-Path (Get-GdRoot) "lib\$f"))
}

function Assert-GdModuleManifest {
    <# 清单体检：字段齐不齐、钩子是不是真调得动、声明的 PathKeys 跟 Get-GdRequiredPathKeys 认不认得对。
        这是"新增/改动模块清单"唯一的自动防线 —— 少写一个钩子在 dot-source 阶段是什么错都不会报的，
        要等到夜里计划任务跑到那一步才炸，正是本仓库最不该犯的那类错。 #>
    param([Parameter(Mandatory)]$Module)
    $bad = @()
    foreach ($k in $script:GdModuleRequiredKeys) {
        $v = $Module.$k
        if ($null -eq $v) { $bad += "缺字段 $k" }
    }
    $hooks = $Module.Hooks
    if ($null -eq $hooks) {
        $bad += '缺 Hooks 表'
    }
    else {
        foreach ($h in $script:GdModuleRequiredHooks) {
            $sb = $hooks.$h
            if ($null -eq $sb) { $bad += "缺钩子 $h" }
            elseif ($sb -isnot [scriptblock]) { $bad += "钩子 $h 不是 scriptblock(是 $($sb.GetType().Name))" }
        }
    }
    # 每一项 PathKeys 都得是 paths.<键>，且这一路真该催它(Get-GdRequiredPathKeys 只看助手库)
    foreach ($k in @($Module.PathKeys)) {
        if (-not ([string]$k)) { $bad += 'PathKeys 里有空项'; continue }
        if ((@(Get-GdRequiredPathKeys) -notcontains [string]$k)) { $bad += "PathKeys 里的 $k 不在 Get-GdRequiredPathKeys 的名单里" }
    }
    # 要管理员却没写为什么 → 提权那句会印成"方舟 → 以管理员身份重启本轮"，看着像漏了字
    if ($Module.RequiresAdmin -and -not [string]$Module.AdminWhy) { $bad += 'RequiresAdmin=true 却没写 AdminWhy' }
    return @($bad)
}

function Import-GdModules {
    <# 装载 lib\*-module.ps1。返回装载到的模块 id 列表。
        本函数只做"注册清单 + 体检"这一件事，不点各模块的业务库 ——
        在函数里 dot-source 一个文件，它定义的函数会随这个函数的作用域一起消失，
        钩子稍后就再也找不到它们(清单里存的是 scriptblock，对象能活，函数定义不能活)。
        所以点库这件事交给入口在顶层照 Get-GdModuleLibPaths 点菜，见 daily-clear.ps1 开头那三行。
        已经注册过的不重复注册(Register-GdModule 会 WARN)，所以入口可以被多次点进来。 #>
    param([switch]$Quiet, [switch]$Tolerant)
    $lib = Join-Path (Get-GdRoot) 'lib'
    $files = @(Get-ChildItem -LiteralPath $lib -Filter '*-module.ps1' -ErrorAction SilentlyContinue | Sort-Object Name)
    # Out-Null 不是摆设：清单文件里那句 Register-GdModule(...) 会把清单对象吐到输出流，
    # 在函数里 dot 就会顺着 Import-GdModules 的返回值漏出去 —— 调用方拿到的"模块 id 列表"
    # 前面就多出两个长得很像 id 的对象(test-modules 的 [m1] 第一条就是这么发现的)。
    foreach ($f in $files) { . $f.FullName | Out-Null }
    $bad = @()
    foreach ($m in @(Get-GdModules)) {
        foreach ($b in @(Assert-GdModuleManifest -Module $m)) { $bad += "$([string]$m.Id)：$b" }
    }
    if ($bad.Count) {
        $msg = "模块清单不完整：" + ($bad -join '；')
        if ($Tolerant) { Write-GdLog $msg -Level ERROR } else { throw $msg }
    }
    if (-not $Quiet -and $files.Count) {
        Write-GdLog ("已加载游戏模块：$((Get-GdModuleIds) -join ' / ')")
    }
    return @((Get-GdModules) | ForEach-Object { [string]$_.Id })
}

function Get-GdModuleLibPaths {
    <# 各模块声明的业务库(注册顺序去重后)。入口在顶层 dot-source 它们，钩子才调得动。
        -MissingOnly 只报"清单写了但文件不在"的那几项 —— 那是工程被裁坏了，不是"这一路没装"：
        助手库(maa.ps1 / m9a.ps1)可以不在(单游戏仓库正常形态)，但 *-module.ps1 声明了编排库却缺文件，
        跑起来只会在半夜的计划任务里炸出一句看不懂的"无法将…识别为 cmdlet"。 #>
    param([switch]$MissingOnly)
    $lib = Join-Path (Get-GdRoot) 'lib'
    $seen = @{}
    $out = @()
    foreach ($m in @(Get-GdModules)) {
        foreach ($f in @($m.Libs)) {
            $name = [string]$f
            if (-not $name -or $seen.ContainsKey($name)) { continue }
            $seen[$name] = $true
            $p = Join-Path $lib $name
            if (Test-Path -LiteralPath $p) { if ($MissingOnly) { continue } ; $out += $p }
            else { if ($MissingOnly) { $out += $name } }
        }
    }
    return @($out)
}

# —— "跑一轮"这一格现在不住在这里(2026-10-20 乙批②) ——
# 十段的 PS 原身 Invoke-GdDailyRound 与它的逐路执行器 Invoke-GdModuleRuns 已一并划掉。带走后者用的判据
# 与退役单元① 同形：删掉调用方之后，被删函数自己的唯一调用方同时消失，那才是"级联可削面"而不是"漏删"。
# 这棵树上的整轮从此只有一条路：
#   daily-clear.ps1 → lib\rust-round.ps1 的 Invoke-GdDailyRoundDispatch → 内置件 gdcore.exe --daily-round，
# 编排、话术、三档退出码的事实源是 tools\rustcore\src\main.rs 的 daily_round_chain(逐行复刻那份十段)。
# 拿不到内置件、主闸被人拨关(GD_RUST_OFF=1 或 runtime.rustOff 含 gdcore)、轮上下文装配炸 ——
# 这三种形状一律整轮拒跑(2)并落一句人话，不再有"换人跑"的第二条腿；代价与受影响的形态见转接层头注。
# 本文件剩下的是**还被两头问回来**的零件：注册表与装载器(Register-GdModule / Import-GdModules)。
# ⚠ 刀⑤(2026-11-04) 追记：上一版这句末尾还挂着「钩子唯一收口(Invoke-GdModuleHook)」，本批它随臂一起出仓——
#   §11.230 现读：全树对它只剩门内两发真调(tools\test-modules.ps1 的 [m4] EnvProbes／Resolve)，生产面 0 命中。
#   「唯一收口」在摘臂之后是死陈述：它不量任何东西，只替一枚没有调用者的壳续命；那两发本批改成直点钩子。
# ⚠ 刀①(2026-11-03) 追记：上一版名册里的「手动门分发(Set-GdModuleState)」已随 rust-round 四臂摘除
#   (round-gate:<id> 两枚改核内直答)一并出仓——它的唯一消费面就是那两枚臂，臂摘了它就是零活；
#   手动门真身自此只剩 panel-cs 那三枚 set-* verb(Round.Data.cs)与核内 round_answer 的直答臂。
# ⚠ 11-02 刀③ 作废登记：上一句末尾原本还挂着半句按位置分派「谁在点」的口径，已不作数——
#   那半句把 选人 Select-GdModules 与 环境读数 Get-GdModuleEnvRows 也算成被生产在点，而它们的生产调用点早已归零（只剩自检还点）；
#   那半句里的「面板」那一路，也随 tools\panel.ps1 整件退役一起没了。
#   谁在调不由注释供数：表账 tools\ps-map\ps-map.tsv 每枚一行、第六列改由人工逐枚现量＋仓外对平尺复核(11-02 改口：供数那本门 tools\ps-map\probe-ps-map.ps1 已整件出仓，旧句「由门逐行现量」不作数)。
# ⚠ 11-03 转接刀 落码：上面那段零件名册里的「选人」「环境读数」本批真的摘件了，那两行表账随之划除。
#   选路只剩核内 tools\rustcore\src\main.rs 的 select_gd_modules（清单＝注册、助手库＝装了、编排库＝合体，三枚文件各判一件事）；
#   要探的路径清单只剩那本文件里的两张静态表（ark 与 r1999 各一张，行文本「标签 → 展示值」由它供）。
#   PS 侧自此没有「选哪几路／探哪几行」的出口，连自检也不点它们：tools\test-modules.ps1 的 [m4] 改成直接点 EnvProbes 钩子，
#   [m5] 与 [m7] 里那几格量 PS 真身的钉随真身一并作废——那本门绿着的地方自此不再替这一路作证。
# ⚠ 刀⑤(2026-11-04) 落码：必需钩子名册从七枚收成一枚(只剩 EnvProbes)，两本清单的其余非 Status 块体同日出仓。
#   为什么这六枚(Resolve/Preview/Run/Stop/Ack/ShopState)还能被摘：它们的 round 消费面已随转接刀·运行面
#   刀①(gate/stop)／刀②(preview)／刀③(resolve 判定面)逐枚改成核内直答，PS 侧一处调用点都不剩；
#   留着"新模块必须写满七格"这条规矩，只会让下一路清单抄六枚没人走的壳。
#   EnvProbes 为什么是唯一留下的必需格：它的对面**不是函数调用而是源文本解析**——
#   tools\panel-cs\Panel.Summary.cs 的 ProbeSpecs 按 `^\s*EnvProbes\s*=\s*\{\s*$` 在清单里取那一段，
#   删格＝面板"环境"段少几行、且装载校验与门都不会当场报错。摘它之前必须先换掉那把尺(已登记为下一刀候选)。
#   名册归一(§11.186 那笔「多处名册先问能不能收成一处现问」的现役兑现)：tools\test-modules.ps1:91 原本
#   逐字抄了同一串七枚，本批删掉那两本硬编码名册，自检改成"每一格必是 scriptblock"的通形尺＋负锚，
#   事实源自此只有 :24 这一处。Status 一族从来是可选钩子(由 status.ps1 逐枚按在不在决定整段跳不跳)，不在这本名册里。
