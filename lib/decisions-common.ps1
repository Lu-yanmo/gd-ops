# lib/decisions-common.ps1 — 两个游戏共用的判定底座：两道手动门的读写(只读状态，不碰 GUI)
# ⚠ 2026-11-03 转接刀改口：原句里「时间换算」那半段已随 Convert-GdUtc8ToMs 出仓，换算只有核内一份(verb convert-utc8-to-ms／format-unix-ms)。
# 方舟侧看 lib/ark-decisions.ps1，1999 侧看 lib/r1999-decisions.ps1；只发布一个游戏时只需要本文件 + 对应那一侧。
#
# ⚠ 刀⑯ 笔(d)-1(2026-11-06)改口：下面九枚本批出仓——三读件 Get-GdAckMap／Get-GdShopDoneMap／Get-GdArkEventProbe
# (ark 那侧)、写件三枚 Set-GdAckKey／Set-GdShopDoneKey／Remove-GdShopDoneKey、领空 flag 尺 Test-GdShopDoneEnabled。
# 读的面自此只有核一把出口：read-decision-state 一次交回 Ack／ShopDone／ShopDoneGateOpen／Probe 四格(原表与
# "给不给"分两枚键，读值半段＝Round.FlagEnabled)；消费方两本壳(Resolve-ArkPlan／Resolve-R1999Plan)自刀㉑ 笔(b)-1
# 起改指 compose-decision-inputs 一次取齐＋可选入键 ignoreShopDone；Show-GdGateState 与门侧夹具仍指 read-decision-state。
# "核不在场就回落 PS 直读 state 文件"的回落臂随件同亡——核不在场 Invoke-GdCli throw＝这一轮拒跑，不再有
# "读不到就当空表/还没结论"的假回落(把核坏了化装成合法读数正是登记过的假绿形)。许可与判据的分家照旧：
# 门格先改指、行为格现出牙，才谈得上摘 PS 本体。
#
# 关于下面的 [ValidateSet('ark','r1999')] 都换成了 Assert-GdGameId：
# 本文件的职责是"按游戏 id 读写状态"，id 该由模块清单说了算(加一款游戏不该来改这里)。
# 但本文件也是老消费方(tools\test-*.ps1 / probe-decisions / 只点 common+decisions 的自检)唯一认识的底座，
# 所以注册表没装载时它必须照旧认这两个 id —— 回落分支就是给那些入口留的，别删。
# ⚠ 刀⑯ 收官批(2026-11-06)：probe-decisions.ps1 整件出仓(取证 raw\k35a-cut16-fin-recon.txt)——上面列举的
#   消费者自此只剩 tools\test-*.ps1 与只点 common+decisions 的自检；回落分支的存活判据不变。

function Get-GdGameIds {
    <# 判定层认得的游戏 id：注册表里有模块就用模块的，否则回落到本仓库一直用的两个。 #>
    if (Get-Command Get-GdModuleIds -ErrorAction SilentlyContinue) {
        $ids = @(Get-GdModuleIds)
        if ($ids.Count) { return $ids }
    }
    return @('ark', 'r1999')
}

function Assert-GdGameId {
    param([Parameter(Mandatory)][string]$Game)
    $ids = @(Get-GdGameIds)
    if ($ids -notcontains $Game) {
        throw "不认识的游戏 id：'$Game'(这棵树上认得：$($ids -join ' / '))"
    }
    return $Game
}

function Get-GdSettingsBlock {
    <# 游戏 id -> settings.json 里的块名。两者不同名(1999 的 id 是 r1999，settings 块叫 re1999)，
        所以这个映射必须由模块清单提供，不能靠字符串拼 —— 以前它就是 Test-GdShopDoneEnabled 里的一句 if。 #>
    param([Parameter(Mandatory)][string]$Game)
    if (Get-Command Get-GdModule -ErrorAction SilentlyContinue) {
        $m = Get-GdModule -Id $Game
        if ($m -and [string]$m.SettingsBlock) { return [string]$m.SettingsBlock }
    }
    return $(if ($Game -eq 'ark') { 'ark' } else { 're1999' })
}

function Get-GdNowMs { [DateTimeOffset]::Now.ToUnixTimeMilliseconds() }


function Show-GdGateState {
    <# status.ps1 的[6]段：把某一款的两道手动门当前状态打出来。放在这里是因为只有这一层拿得到两张表。
       追加(2026-10-12 第 1 刀)：方舟的第一道门(活动要不要自动刷)已换成"每轮机器自证"——
       这里就改报那本账(state\ark-event-probe.json)的最新一条；1999 那一路照旧报 ack 表(它没有自证通道)。
       ⚠ 刀⑯ 笔(d)-1(2026-11-06)改口：旧实现经三枚读件(各带"核不在场就回落 PS 直读 state 文件"的回落臂)。
       那三枚与回落臂本批出仓，本函数改为**一次** read-decision-state 直答取齐(Ack／ShopDone／Probe 三格)——
       核不在场时 Invoke-GdCli throw＝这一段拒跑，不再有"读不到就当还没有结论"的假回落。 #>
    param([Parameter(Mandatory)][string]$Game)
    $ds = Invoke-GdCli -Verb 'read-decision-state' -Body ([pscustomobject]@{ game = $Game })
    if ($Game -eq 'ark') {
        Write-Host "  ark: 活动图每轮自证(拿最后一关当探针，进得去就按活动图刷)"
        $pb = $ds.Probe
        if ($pb) {
            $txt = if ($pb.Entered) { '进得去 → 下一轮照常刷活动图' } else { '进不去 → 判为还没打通，当日改刷长草' }
            Write-Host "  ark自证($($pb.Date)) $($pb.EventKey)/$($pb.Stage)：$txt" -ForegroundColor $(if ($pb.Entered) { 'Green' } else { 'Yellow' })
        } else {
            Write-Host "  ark自证: 还没有结论(下一轮即以活动最后一关为探针)"
        }
    } else {
        $m = Convert-GdMapToHashtable $ds.Ack
        if ($m.Count -eq 0) { Write-Host "  ${Game}: 无已确认活动(新活动会先跳过清体力)" }
        else { foreach ($k in $m.Keys) { Write-Host "  ${Game}已确认: $k → $($m[$k])" } }
    }
    $d = Convert-GdMapToHashtable $ds.ShopDone
    if ($d.Count) { foreach ($k in $d.Keys) { Write-Host "  ${Game}商店已领空: $k → $($d[$k])(剩余理智转长草)" -ForegroundColor Yellow } }
}

# ================= 入口参数名(只给提示语用) =================
# 判定层会告诉用户"下一步敲什么"，但参数名是入口自己定的：单游戏仓库的入口用干净短名(-Ack / -ShopReset)，
# 本地合体工程的入口两款混在一起、必须带前缀(-AckArk / -RShopDone)。
# 所以提示语里不许写死参数名 —— 短名是回落那一支(导出的仓库一个字都不用改)，长名由该路模块清单自己声明。
# tools\menu.ps1 那个方向(菜单去探测入口有哪些参数)是同一件事的另一半。
# ⚠ 11-01 丙形真转接(§11.176 ⑥ 落码)：这一族原来不是"现问"，是"入口点完库调一次同步件、把长名盖进
#   进程内的一张表"。那张表和模块清单自己声明的 EntryVerbs.Merged 是两份名册(不同源)，而它盖没盖取决于
#   这个进程先跑过哪个入口 —— 漏调用的就是"提示语让你敲短名、可本仓库入口只认长名"那一形(2026-09-24 犯过)，
#   调全了也只是把第二份名册按清单再抄一遍。现在 Get-GdEntryVerb 每次现问注册表，与 Rust 核
#   tools\rustcore\src\main.rs 的 round_entry_verb 同一把尺；同步件与那张表的读写侧(现场覆盖件、取表件)
#   连同那张写死的回落表一起退役。
#   代价照登：注册表不在场的进程(只点 common＋decisions 的老消费方)现在只能看到短名 —— 那是回落那一支，
#   tools\test-lib.ps1 [t1c] 的判语随之改成钉这一支；"清单在场⇒长名"那一支由 tools\test-modules.ps1 [m6]
#   钉住(它装载得起来，并且拿入口自己的 param() 做对照，两把尺不同轴，所以不把装载面搬到 test-lib 去)。
function Get-GdEntryVerb {
    <# 现问，不落表：先判这棵树上把几路当一款游戏在管(合体才换长名)，是就现问该路清单自己声明的
        EntryVerbs.Merged；问不到(注册表没装载／这一路不在这棵树上／这一路没声明这枚 verb)就回落干净短名。
        判据与 tools\rustcore\src\main.rs 的 round_entry_verb 逐条对齐：合体闸按"助手库在位"数，
        取长名前再核该路自己在位(同一把尺)，名册里没这枚 verb 才落到 '-' + verb。 #>
    param([Parameter(Mandatory)][string]$Game,
          [Parameter(Mandatory)][ValidateSet('Ack', 'ShopDone', 'ShopReset', 'Farm')][string]$Verb)
    if ((Test-GdMergedEntry) -and (Get-Command Get-GdModules -ErrorAction SilentlyContinue)) {
        foreach ($m in @(Get-GdModules)) {
            if (([string]$m.Id) -ne $Game) { continue }
            if (-not (Test-GdModuleInstalled -Module $m)) { continue }
            $v = $m.EntryVerbs.Merged
            if (-not $v) { continue }
            $name = [string]$v.$Verb
            if ($name) { return $name }
        }
    }
    # 回落：合体闸没过、注册表不在场、或这一路没声明这枚 verb —— 短名按被问的 verb 现拼，不许写死任何一枚。
    return '-' + $Verb
}
function Test-GdMergedEntry {
    <# 这棵树是不是"一个入口管两款"的合体工程：判据用两路的助手库在不在，
        跟 Get-GdRequiredPathKeys"按那路的助手库在不在决定该催哪些路径"是同一招 —— 导出仓库里不猜配置。
        注册表装着时按 Get-GdModules -AvailableOnly 数(加一款游戏不需要来改这里)；没装载时退回按两个文件名探测，
        因为 status.ps1 / 自检这些老消费方本来就只点 common+decisions，不点 modules.ps1。
        ⚠ 11-04 刀⑦ 笔(a) 换源：这一格的判据原本数的是两路的**编排文件**(lib\ark-daily.ps1 / lib\r1999-daily.ps1)，
        那两件是"这棵树上有没有入口把这一路当一款游戏在管"的读数。运行面刀①~⑤ 之后，两路的游戏侧动作全部
        由核内直答，那两枚件只剩 executor 的壳 ⇒ 再拿它当选路判据就是让一份要退役的件决定活着的判据(§11.235 判语①)。
        自此人侧与核内 round_is_merged_entry 同一把尺 = 助手库在不在枚数 >1，OrchestrationLib 那枚必需键同批退役。
        代价照登：回落那一支(下面 $lib 那两行)自此在全树**没有活消费方** —— Get-GdEntryVerb 那一路同闸要求
        注册表在场，而唯一直接问这枚函数的 test-modules.ps1:285 又跑在装载了清单的进程里；
        留着它是"老消费方只点两本库"那条契约的形状，不是"现在有人在按它选路"，账记在枚数不动那一侧。 #>
    if (Get-Command Get-GdModules -ErrorAction SilentlyContinue) {
        $all = @(Get-GdModules)
        if ($all.Count) {
            $n = @(Get-GdModules -AvailableOnly).Count
            return ($n -gt 1)
        }
    }
    $lib = Join-Path $global:GD.Root 'lib'
    return ((Test-Path -LiteralPath (Join-Path $lib 'maa.ps1')) -and (Test-Path -LiteralPath (Join-Path $lib 'm9a.ps1')))
}
