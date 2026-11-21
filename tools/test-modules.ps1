# tools/test-modules.ps1 — 框架层自检：模块清单齐不齐、注册表对不对、框架有没有掺进游戏知识
# 用法: pwsh -NoProfile -File tools\test-modules.ps1
#       只读：不改配置、不启动任何程序、不点鼠标。判定钩子会读 state，所以照样存/还原。
# 这一份会原样进两个导出仓库，所以全部断言都按"这棵树上有哪些模块"来判，
# 绝不写死 ark / r1999：单游戏仓库里只有一款，它也必须全绿。
param([switch]$NoExit)

. (Join-Path $PSScriptRoot 'test-lib.ps1')

# test-lib 只点判定层和助手库；框架和清单要自己点(清单文件由 Import-GdModules 扫出来，别在这儿写死名字)
$modsLib = Join-Path $root 'lib\modules.ps1'
if (-not (Test-Path -LiteralPath $modsLib)) {
    Write-Host "`n  这棵树上没有 lib\modules.ps1 → 框架自检没意义" -ForegroundColor Yellow
    Check 'lib\modules.ps1 在(框架层没被裁掉)' $false
    $res = Show-GdTestResult -Tag 'modules'
    if ($NoExit) { return $res }
    exit 1
}
. $modsLib

Write-Host "`n[m1] 装载与注册表"
$loaded = @()
$loadErr = ''
try { $loaded = @(Import-GdModules -Quiet) } catch { $loadErr = $_.Exception.Message }
Check 'Import-GdModules 全程不抛(清单体检通过)' ($loadErr -eq '') $loadErr
# 2026-11-21 拓扑换代：内核主仓(gd-ops)不带任何游戏插件，注册表为空是它的**正常状态**；
# "注册表非空"那条因此只在装了插件的树上成立，空树改用下面 [m1] 的空树行为段来验。
$kernelOnly = ($loaded.Count -eq 0)
if (-not $kernelOnly) { Check '注册表非空' ($loaded.Count -gt 0) "$($loaded.Count) 款：$($loaded -join ' / ')" }
# 入口那三行的顺序也在这里演一遍：注册表只存清单，业务库要在顶层 dot 才留得下函数定义。
foreach ($libPath in (Get-GdModuleLibPaths)) { . $libPath }
foreach ($m in @(Get-GdModules -AvailableOnly)) {
    $id = [string]$m.Id
    $want = @()
    foreach ($libName in @($m.Libs)) {
        $libFile = Join-Path $root "lib\$libName"
        if (-not (Test-Path -LiteralPath $libFile)) { continue }
        $want += @([regex]::Matches([System.IO.File]::ReadAllText($libFile), '(?m)^\s*function\s+([A-Za-z][\w-]+)') | ForEach-Object { $_.Groups[1].Value })
    }
    $want = @($want | Sort-Object -Unique)
    $gone = @($want | Where-Object { -not (Get-Command $_ -ErrorAction SilentlyContinue) })
    Check "[$id] 顶层点库后清单声明的 $($want.Count) 个函数全在" ($gone.Count -eq 0 -and $want.Count -gt 0) $(if ($gone.Count) { "缺：$($gone -join ' / ')" } else { '' })
}

$modFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'lib') -Filter '*-module.ps1' | Sort-Object Name)
Check 'lib\*-module.ps1 有几个就注册了几个(没有静默丢失)' ($loaded.Count -eq $modFiles.Count) "文件 $($modFiles.Count) / 注册 $($loaded.Count)"
$expectOrder = @($modFiles | ForEach-Object { ($_.BaseName -split '-module')[0] })
Check '注册顺序 = 文件名顺序(汇总/收尾的先后因此可预期)' ((@($loaded | Sort-Object) -join ',') -eq (@($expectOrder | Sort-Object) -join ',')) "$($loaded -join ' / ')"

$avail = @(Get-GdModules -AvailableOnly)
Check '这棵树上注册的模块全都装了(没有"注册了但助手库不在")' ($avail.Count -eq $loaded.Count) "装了 $($avail.Count) / 注册 $($loaded.Count)"
# 注册表的判据(助手库在不在)必须和催路径的判据(Get-GdRequiredPathKeys 看同一个 lib)对齐，
# 否则会出现"面板里能看到这一路、setup 却从不催它的路径"这种对不上的口子。
$reqKeys = @(Get-GdRequiredPathKeys $root)
$declaredKeys = @()
foreach ($m in $avail) { $declaredKeys += @($m.PathKeys) }
$declaredUniq = @($declaredKeys | Sort-Object -Unique)
Check '已装模块声明的 PathKeys 全在 Get-GdRequiredPathKeys 名单里' ((@($declaredUniq | Where-Object { $reqKeys -notcontains $_ }).Count) -eq 0) "$($declaredUniq -join ' / ') ⊆ $($reqKeys -join ' / ')"
Check 'Get-GdModuleIds 与 Get-GdModules 数一致' ((@(Get-GdModuleIds).Count) -eq $loaded.Count)

# 重复注册：静默覆盖比报错更难查(改了哪一份全看点库顺序)
if ($avail.Count) {
    $dupId = [string]$avail[0].Id
    $before = $global:GdModules.Count
    [void](Register-GdModule -Manifest $avail[0])
    Check "同 Id 重复注册不覆盖、只丢 WARN($dupId)" ($global:GdModules.Count -eq $before) "$before → $($global:GdModules.Count)"
    Check '清单没 Id 时注册直接抛' $(try { [void](Register-GdModule -Manifest [pscustomobject]@{ Id = '' }); $false } catch { $true })
}
Check '取不存在的模块返回 $null(不抛，由调用方决定怎么说)' ($null -eq (Get-GdModule -Id '不存在的模块'))

if ($kernelOnly) {
    # 空树 = 内核主仓的卖相：入口必须拒跑并教人怎么装，体检必须绿着说"还没装"。
    # 这两条就是 gd-ops 交付时的对外行为，所以它们必须在本仓自己的自检里现跑，而不是等人手动试。
    Write-Host "`n[m1b] 内核主仓：没有插件时的对外行为"
    Check '空树就是空树：lib\*-module.ps1 一个都没有' ($modFiles.Count -eq 0) "文件 $($modFiles.Count) 个"
    $eng = (Get-Process -Id $PID).Path
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $entryOut = & $eng -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'daily-clear.ps1') -DryRun 2>&1
        $entryRc = $LASTEXITCODE
        $entryAll = (@($entryOut | ForEach-Object { "$_" }) -join "`n")
        Check '入口在无插件树上拒跑(退出码 2，而不是带着空名册跑一轮)' ($entryRc -eq 2) "退出码 $entryRc"
        Check '拒跑话术点名了插件安装命令' ($entryAll -match '没有安装任何游戏插件' -and $entryAll -match 'install-plugin') ''
        Check '拒跑发生在预演之前(没打出预演完成行)' (-not ($entryAll -match '\[dry-run\] 完成')) ''
        if (Test-Path -LiteralPath (Join-Path $root 'config\settings.json')) {
            $stOut = & $eng -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'status.ps1') -Offline 2>&1
            $stRc = $LASTEXITCODE
            $stAll = (@($stOut | ForEach-Object { "$_" }) -join "`n")
            Check 'status 在无插件树上是绿的(退出码 0)' ($stRc -eq 0) "退出码 $stRc"
            Check 'status 说清楚了"还没装插件"而不是抛一屏异常' ($stRc -eq 0 -and $stAll -match '游戏插件') ''
        } else {
            Check 'status 在无插件树上是绿的(这台树还没有 settings.json → 没跑)' $true '跳过：先跑 setup.ps1 才会验这一条'
        }
    } finally { $ErrorActionPreference = $prevEap }
}

Write-Host "`n[m2] 每份清单的完备性(11-07 SC-C 刀⑰ 笔(c)-2 第 2 批 B2：判定面整段换家 check-module-manifests)"
# 换家史注(按在册「判据不删只换家」＋「改弱了的钉要写下它不再量什么」)：这一段原本 40 行、11 枚逐条 Check……（细节见该帧）
$b2Body = [pscustomobject]@{
    root             = $root
    entry            = (Join-Path $root 'daily-clear.ps1')
    requiredPathKeys = @(Get-GdRequiredPathKeys $root)
    requiredHooks    = @($script:GdModuleRequiredHooks)
}
$b2 = $null
$b2Err = ''
try { $b2 = Invoke-GdCli -Verb 'check-module-manifests' -Body $b2Body } catch { $b2Err = $_.Exception.Message }
Check '[m2] check-module-manifests 交回读数(核缺席／入闸拒跑都算红，不许静默跳过)' ($b2Err -eq '' -and $null -ne $b2) $b2Err
$b2Mods = @()
if ($null -ne $b2) { $b2Mods = @($b2.Modules) }
$b2RegIds = @(Get-GdModuleIds | ForEach-Object { [string]$_ })
if ($kernelOnly) {
    # 零行在空树上是正确答案，但"答了没有"仍然要判：Err 为空 + 真有回执 = 核侧确实读到了这棵树。
    Check '[m2] 空树也答得出来：核侧清单数与注册表同为 0(没问照样红)' (
        $b2Err -eq '' -and $null -ne $b2 -and $b2Mods.Count -eq 0 -and $b2RegIds.Count -eq 0) ('核 ' + $b2Mods.Count + ' / 注册 ' + $b2RegIds.Count)
} else {
    Check ('[m2] 核侧看到的清单数 = 注册表数(零行是"没问"不是"没意见"，故两形都要过)') (($b2Mods.Count -gt 0) -and ($b2Mods.Count -eq $b2RegIds.Count)) ('核 ' + $b2Mods.Count + ' / 注册 ' + $b2RegIds.Count)
}
$b2Ids = @($b2Mods | ForEach-Object { [string]$_.Id } | Sort-Object)
Check '[m2] Id 名册逐字对平(两侧同集合才算承接，防"核读错树"的假绿)' ((@($b2Ids) -join ',') -eq (@($b2RegIds | Sort-Object) -join ',')) ($b2Ids -join ' / ')
$b2Man = @()
foreach ($r in $b2Mods) { foreach ($q in @($r.Problems)) { $b2Man += ([string]$r.Id + ':' + [string]$q) } }
Check ('[m2] 清单侧零意见(原 11 枚逐条 Check 现由核点名，命中 ' + $b2Man.Count + ' 枚)') (($b2Err -eq '') -and ($b2Man.Count -eq 0)) ($b2Man -join ' ‖ ')

function Get-GdCodeOnlyText {
    <# 把注释剥干净，只留代码。两步都要：整块注释(井号+尖括号包起来那种)如果只按行剥，
        块里那些不带井号的中间行会原样留着 —— 而"框架里引用了游戏专有函数"这句话恰恰写在块注释中间。
        注释里别写那两个成对符号本身：写了就是提前把这块注释闭合掉，本函数第一个版本就栽在这。 #>
    param([Parameter(Mandatory)][string]$Text)
    $t = [regex]::Replace($Text, '<\#[\s\S]*?\#>', ' ')
    return ((@($t -split "`n") | ForEach-Object { $_ -replace '#.*$', '' }) -join "`n")
}

Write-Host "`n[m3] 框架纯洁性(业务知识只能待在清单里)"
$fwCode = Get-GdCodeOnlyText -Text ([System.IO.File]::ReadAllText($modsLib))
Check '剥注释这招真的能拿到块注释中间的函数名(自检本身先自检)' ($fwCode -notmatch 'Resolve-GdArkDecision')
foreach ($pat in @('Resolve-GdArk', 'Start-GdArkDaily', 'Stop-MaaAndGame', 'MaaAgentBinary', 'Resolve-GdR1999', 'Start-GdR1999Daily', 'Stop-M9A', 'Stop-GdMuMu', 'Get-R1999Event', 'Show-GdArkPreview')) {
    Check "modules.ps1 的代码里没有 $pat" ($fwCode -notmatch [regex]::Escape($pat))
}
Check 'modules.ps1 不硬编码某一款的提权文案(全走 AdminWhy/AdminHint)' ($fwCode -notmatch '端游要管理员')
Check 'modules.ps1 里没有出现 ark/r1999 的分支字面量(选路由 -Ids 传进来)' ($fwCode -notmatch "'ark'" -and $fwCode -notmatch "'r1999'")
# 反过来：清单里也不该出现另一款游戏的库(导出时会把不存在的路径点进来)。
$sharedLibs = @{}
foreach ($m in @(Get-GdModules)) {
    foreach ($n in @($m.Libs | ForEach-Object { [string]$_ })) {
        if (-not $sharedLibs.ContainsKey($n)) { $sharedLibs[$n] = @{} }
        $sharedLibs[$n][[string]$m.Id] = $true
    }
}
$shared = @($sharedLibs.Keys | Where-Object { @($sharedLibs[$_].Keys).Count -ge 2 })
foreach ($m in @(Get-GdModules)) {
    # 刀⑰ 笔(c)-2 第 1 批 B1 换家(§11.276)：老形是 ReadAllText 整本 + 逐枚 -match 手拼，
    $m3Self = Join-Path $root "lib\$([string]$m.Id)-module.ps1"
    $other = @(Get-GdModules | Where-Object { [string]$_.Id -ne [string]$m.Id } | ForEach-Object { @($_.Libs) } |
        ForEach-Object { [string]$_ } | Where-Object { $_ -notin $shared })
    $leaked = @()
    foreach ($o3 in $other) {
        $m3Scan = Invoke-GdCli -Verb 'scan-source-patterns' -Body ([pscustomobject]@{
            files = @($m3Self); pattern = [regex]::Escape($o3); expect = 'zero' })
        if (-not [bool]$m3Scan.Passed) { $leaked += $o3 }
    }
    Check "[$([string]$m.Id)] 清单没引用别的模块的业务库" ($leaked.Count -eq 0) ($leaked -join ' / ')
}
# 这份自检查原样进两个导出仓库 → 涉及"两路共用"的断言按注册表里有几款判，绝不写死 ark / r1999
$vrOnDisk = Test-Path -LiteralPath (Join-Path $root 'lib\vision-rescue.ps1')
if (@(Get-GdModules).Count -ge 2) {
    $vrInRoster = (@($shared) -contains 'vision-rescue.ps1')
    Check 'vision-rescue 的名册登记与件在场同一个事实(同在同不在，半迁移红)' ($vrInRoster -eq $vrOnDisk) ("名册=$vrInRoster 件=$vrOnDisk")
}
Check 'Get-GdModuleLibPaths 把共享库去重了(入口点两遍会重复定义一堆函数)' (
    (@(Get-GdModuleLibPaths).Count -eq @(Get-GdModuleLibPaths | ForEach-Object { [System.IO.Path]::GetFileName($_) } | Sort-Object -Unique).Count)) `
    ("$(@(Get-GdModuleLibPaths).Count) 个文件 / $(@($shared).Count) 个共享")
# 后端按命名约定接：内核不许认识任何一路，各路的靶子函数得在自己那一份后端里
$vrKernelTooth = (-not (Get-Command Invoke-GdVisionRescue -ErrorAction SilentlyContinue)) -and
    (-not (@($shared) -contains 'vision-rescue.ps1'))
Check '兜底内核不认识端游也不认识模拟器(只按 New-GdVisionTarget-<kind> 找；件不在改咬没复活)' $vrKernelTooth "件=$vrOnDisk"
# 自检脚手架点库的名单是另一份硬编码(tools\test-lib.ps1 第 10 行)：清单加了库而它没跟上，
$tlText = [System.IO.File]::ReadAllText((Join-Path $root 'tools\test-lib.ps1'))
Check '兜底 PS 后端名册已摘净：没有任何一路的 Libs 再声明 vision-*.ps1(件不在而名册还写着＝半迁移红)' (
    @(Get-GdModules | ForEach-Object { @($_.Libs) } | Where-Object { [string]$_ -like 'vision-*.ps1' }).Count -eq 0)
# 11-06 刀⑮ 笔(d)-2 复跑红一次照登：上面那格首拍咬的是 $tlText 全文，而 tools\test-lib.ps1:21 那句史注
$tlCode203 = Get-GdCodeOnlyText -Text $tlText
Check '自检脚手架的代码面名单也不点那三份(注释剥后再判；名单与清单一起摘才算闭合：只删清单＝少装载一路，只删名单＝多一个恒不命中的名字)' (
    $tlCode203 -notmatch "'vision-rescue'" -and $tlCode203 -notmatch "'vision-ark'" -and $tlCode203 -notmatch "'vision-mumu'")

# 刀⑳ 笔(a)-3 清单完备性对面格：test-lib 头注那句「名单由 $GdTestDotLibs 交给 [m3] 逐条对账，漏一项当场变红」
$tlCode303 = Get-GdCodeOnlyText -Text $tlText
$tlList303 = @()
if ($tlCode303 -match '(?m)^\$script:GdTestDotLibs\s*=\s*@\(([^)]*)\)') {
    $tlList303 = @([regex]::Matches($Matches[1], "'([^']+)'") | ForEach-Object { $_.Groups[1].Value })
}
$tlUnion303 = @(Get-GdModules | ForEach-Object { @($_.Libs) } |
    ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension([string]$_) })
$tlExpect303 = @((@($tlUnion303) + @('decisions')) | Sort-Object -Unique)
$tlExtra303 = @($tlList303 | Where-Object { $tlExpect303 -notcontains $_ })
$tlMissing303 = @($tlExpect303 | Where-Object { $tlList303 -notcontains $_ })
$tlReadout303 = ("名单=[$($tlList303 -join ' ')] 并集加底座=[$($tlExpect303 -join ' ')] 多=[$($tlExtra303 -join ' ')] 少=[$($tlMissing303 -join ' ')]")
if (@(Get-GdModules).Count -ge 2) {
    Check '自检脚手架名单与两份清单 Libs 并集闭合(双向差集：多一名＝恒不命中的死名，少一名＝那一路自检整段静默跳过)' (
        @($tlExtra303).Count -eq 0 -and @($tlMissing303).Count -eq 0 -and @($tlList303).Count -ge 1) $tlReadout303
} else {
    Check '本路清单声明的库都在自检脚手架名单里(单游戏导出仓只量这一路，另一路的库不在这棵树上)' (
        @($tlMissing303).Count -eq 0 -and @($tlList303).Count -ge 1) $tlReadout303
}

Write-Host "`n[m4] 还活着的钩子真调得动(EnvProbes)＋面板取段的形状耦合格"
foreach ($m in @(Get-GdModules -AvailableOnly)) {
    $id = [string]$m.Id
    $rows = $null
    $err = ''
    # 11-03 转接刀：这几格量的一直是 EnvProbes 钩子自己的返回形状，那枚薄壳只转发明再加一条 null→空表；
    try { $rows = & $m.Hooks.EnvProbes $S } catch { $err = $_.Exception.Message }
    Check "[$id] EnvProbes 调得动" ($err -eq '') $err
    Check "[$id] 环境探测至少一项" (@($rows).Count -gt 0) "$(@($rows).Count) 项"
    $labels = @($rows.Keys)
    Check "[$id] 探测项标签非空" (@($labels | Where-Object { -not [string]$_ }).Count -eq 0) ($labels -join ' / ')
    Check "[$id] 探测结果是有序表(体检里那几行的先后不能变)" ($rows -is [System.Collections.Specialized.OrderedDictionary])
    Check "[$id] 探测项的值都落在 paths.* 上(框架不猜路径)" (@($labels).Count -eq @($rows.Values | Where-Object { $_ -is [string] -or $null -eq $_ }).Count)
}
# 11-04 刀⑤ 作废登记：这一段原来还点了 Resolve(带 -Force，前后各存/还原一次 state)与 Preview 两发真调。
$panelSrc = ''
try { $panelSrc = [System.IO.File]::ReadAllText((Join-Path $root 'tools\panel-cs\Panel.Summary.cs')) } catch { }
Check '面板仍按「^\s*EnvProbes\s*=\s*\{\s*$」在清单里取环境探测段(EnvProbes 唯一活着的对面)' (
    $panelSrc.Contains('EnvProbes\s*=\s*\{\s*$')) $(if ($panelSrc.Length -eq 0) { 'tools\panel-cs\Panel.Summary.cs 不在场(尺没接到电)' } else { '那句字面量变了' })

Write-Host "`n[m5] 选择与参数名"
# 11-03 转接刀：这一段原本量的是 Select-GdModules 那枚 PS 真身(不传 Id ／ -Ids all ／ 只挑一款 ／
foreach ($id in @(Get-GdModuleIds)) {
    Check "[$id] 过 Get-GdGameId 校验(注册表与判定层的口径一致)" $(try { $null = Assert-GdGameId -Game $id; $true } catch { $false })
}
$regIds5 = @(Get-GdModuleIds)
if ($regIds5.Count) {
    Check 'Get-GdGameIds 就是注册表里的 Id' ((@(Get-GdGameIds) -join ',') -eq ($regIds5 -join ',')) ((@(Get-GdGameIds)) -join ' / ')
} else {
    # 内核主仓(没装插件)：判定层的兜底名册还没搬进插件仓(decisions-common 的已知残留)，
    # 量的是它别变空、也别多出第三款；装了插件那一路归上面那格管。
    Check '空树：Get-GdGameIds 仍是判定层兜底的两款(拆分未完的已知残留)' ((@(Get-GdGameIds) -join ',') -eq 'ark,r1999') ((@(Get-GdGameIds)) -join ' / ')
}
Check '认不出来的游戏名会被判定层挡下' $(try { Assert-GdGameId -Game '__no_such__'; $false } catch { $true })
# 每款游戏 settings 里那块名字不一定等于 Id(re1999 vs r1999)，取块必须走 Get-GdSettingsBlock
# 模板里只放内核键，游戏块由插件片段补进来 ⇒ 有效模板 = 模板 + config\plugins\*.example.json(setup 也走这条)。
$tplEff = Merge-GdPluginFragments -Object (Read-GdJson -Path (Join-Path $root 'config\settings.example.json')) -Root $root
foreach ($m in @(Get-GdModules)) {
    $blk = [string]$m.SettingsBlock
    $got = Get-GdSettingsBlock -Game ([string]$m.Id)
    Check "[$([string]$m.Id)] Get-GdSettingsBlock 取到的是清单写的那块" ($got -eq $blk) "$blk"
    Check "[$([string]$m.Id)] settings 模板(含插件片段)里有 $($blk) 这一块" ($null -ne $tplEff.$blk)
}

Write-Host "`n[m6] 入口动词与真参数对得上(菜单/面板印的开关名不能是编的；11-07 B2 换家)"
# 换家史注：这一段原本 44 行(现读 daily-clear.ps1 的参数表 + 逐枚开关名正查 + ark×Ack 反向负锚)……（细节见该帧）
$b6 = $null
$b6Err = ''
try { $b6 = Invoke-GdCli -Verb 'check-module-manifests' -Body $b2Body } catch { $b6Err = $_.Exception.Message }
$b6Entry = @()
if ($null -ne $b6) { $b6Entry = @($b6.EntryProblems) }
Check ('[m6] 入口侧零意见(参数表读得到／逐枚开关名在参数表里／ark×Ack 反向负锚，命中 ' + $b6Entry.Count + ' 枚)') (($b6Err -eq '') -and ($b6Entry.Count -eq 0)) ($b6Entry -join ' ‖ ')
Check '合体判断走注册表(Test-GdMergedEntry 返回布尔)' ($null -ne (Test-GdMergedEntry)) "merged=$(Test-GdMergedEntry)"

Write-Host "`n[m7] 注册表被清空时的行为(工程裁坏了要说人话，别少印四段)"
$snap = [ordered]@{}
foreach ($k in @($global:GdModules.Keys)) { $snap[$k] = $global:GdModules[$k] }
$global:GdModules.Clear()
Check '清空后 Get-GdModules 返回空(不抛)' (@(Get-GdModules).Count -eq 0)
# 11-03 转接刀：这一格原本拿清空后的注册表去点 Select-GdModules 的那句 throw，真身已摘 ⇒ 作废；
Check '清空后 Get-GdGameIds 落到老的两款兜底' ((@(Get-GdGameIds) -join ',') -match 'ark')
foreach ($k in @($snap.Keys)) { $global:GdModules[$k] = $snap[$k] }
Check '还原后注册表数量不变' ($global:GdModules.Count -eq $snap.Count) "$($snap.Count) → $($global:GdModules.Count)"

Write-Host "`n[m8] 本机专属值不许出现在源码/文案里(导出前那道闸，前移到这里)"
# 判据和"发出去之前扫导出目录"用的是同一个函数(lib\common.ps1)，所以这里先红一次，就不用等
$m8Vals = @(Get-GdLocalOnlyValues -LocalSettings (Join-Path $root 'config\settings.json'))
Check "本机专属值取到 $($m8Vals.Count) 项(有的扫)" ($m8Vals.Count -gt 0)
$m8Hits = @(Test-GdExportLeaks -Dir $root -Values $m8Vals)
Check '源码 / README / 自检样本里一项本机专属值都没有' ($m8Hits.Count -eq 0) (@($m8Hits | ForEach-Object { $_ }) -join ' ｜ ')

Write-Host "`n[m9] 问模型那一层(tools\vision\ask.py + PowerShell 侧的桥)"
# 为什么值得单独一段：这一层的规矩是"key 只走环境变量、Python 只发请求、点位判断留在 PowerShell"。
$askPy = Join-Path $root 'tools\vision\ask.py'
Check 'tools\vision\ask.py 在(它是内核的一部分，两路仓库都要带上)' (Test-Path -LiteralPath $askPy)
$pySrc = ''
if (Test-Path -LiteralPath $askPy) {
    $pyBytes = [System.IO.File]::ReadAllBytes($askPy)
    Check 'ask.py 不带 BOM(python 会把 BOM 当语法错误)' (
        $pyBytes.Length -lt 3 -or -not ($pyBytes[0] -eq 0xEF -and $pyBytes[1] -eq 0xBB -and $pyBytes[2] -eq 0xBF))
    $pySrc = [System.Text.Encoding]::UTF8.GetString($pyBytes)
    Check 'ask.py 用 LF(和 .gitattributes 给文本定的规矩一致)' (-not ($pySrc -match "`r"))
    Check 'ask.py 只用标准库(不需要 pip：embeddable 包里就这些)' (
        $pySrc -match '(?m)^import urllib\.request' -and
        -not ($pySrc -match '(?m)^\s*(from|import) (requests|httpx|openai|dashscope|PIL|numpy)'))
    Check 'ask.py 里的 key 只从环境变量读，没有 --api-key 这种命令行口子' (
        $pySrc -match 'os\.environ' -and -not ($pySrc -match '--api[-_]key'))
    Check 'ask.py 里没有长得像 key 的字面量' (-not ($pySrc -match 'sk-[A-Za-z0-9]{12,}'))
    Check 'ask.py 自己带离线自检(--selftest)' ($pySrc -match 'def run_selftest')
    Check 'ask.py 只有一处真的发请求(urlopen 不被自检绕过)' (([regex]::Matches($pySrc, 'urllib\.request\.urlopen')).Count -eq 1)
}
# 2026-11-06 刀⑮ 笔(d)：Invoke-GdVisionRaw 的真身随 lib\vision-rescue.ps1 出仓。它管的那四件事(起的是 python + ask.py
$visionRescueOnDisk = Test-Path -LiteralPath (Join-Path $root 'lib\vision-rescue.ps1')
Check '兜底内核件与 Invoke-GdVisionRaw 命令同一个事实(同在同不在，半迁移红)' (
    $visionRescueOnDisk -eq [bool](Get-Command Invoke-GdVisionRaw -ErrorAction SilentlyContinue)) "件=$visionRescueOnDisk"
Check '问模型这一层在 PS 侧不再有第二份实现(Invoke-GdVisionRaw 没在别处复活)' (
    -not (Get-Command Invoke-GdVisionRaw -ErrorAction SilentlyContinue))
$liveAsk = ''
try { $liveAsk = [System.IO.File]::ReadAllText((Join-Path $root 'tools\panel-cs\Live.cs')) } catch { }
Check '那四条形制住在核侧(起 python + ask.py／key 只进环境变量／提示词走 --request／命令行上没有 key 的口子)' (
    $liveAsk.Length -gt 0 -and $liveAsk.Contains('ask.py') -and $liveAsk.Contains('DASHSCOPE_API_KEY') -and
    $liveAsk.Contains('--request') -and -not $liveAsk.Contains('--api-key')) ('Live.cs=' + $liveAsk.Length + ' 字符')
$gateDef = ''
if (Get-Command Get-GdPythonExe -ErrorAction SilentlyContinue) { $gateDef = (Get-Command Get-GdPythonExe).Definition }
Check '找 Python 会跳过 WindowsApps 那个商店占位程序' ($gateDef -match 'WindowsApps')
Check '找到的解释器要真 import 得动 ask.py 要用的那几个库' ($gateDef -match 'Test-GdPythonUsable')
# 2026-10-28 甲档第二十刀刀③：这一格原来现读 Invoke-GdVisionRescue 的定义文本比两个 IndexOf，那枚壳…（细节见该帧）
$pyExe = ''
if (Get-Command Get-GdPythonExe -ErrorAction SilentlyContinue) { $pyExe = Get-GdPythonExe }
if ($pyExe -and (Test-Path -LiteralPath $askPy)) {
    $st = Invoke-GdChild -File $pyExe -Arguments @($askPy, '--selftest') -TimeoutSeconds 60
    $sj = $null
    try { $sj = ("$($st.Out)").Trim() | ConvertFrom-Json } catch { }
    Check 'ask.py --selftest 全过(离线：不联网、不碰窗口)' ($st.Code -eq 0 -and $sj -and $sj.ok) $(
        if ($sj) { ($sj.failures -join ' / ') } else { "退出码 $($st.Code) / $($st.Err)" })
} else {
    Check 'ask.py --selftest 全过(这台机器没有可用的 Python → 没跑)' $true '跳过：跑 setup.ps1 -WithPython 之后这一条才会真验'
}

Write-Host "`n[m10] 兜底后端与清单对账(kind 是清单上的字段，不是外壳里的一张表)"
# 为什么单独一段：2026-09-26 把兜底后端泛化成两套(端游 ark / 模拟器 mumu)之后，"这一路该找哪个后端"…（细节见该帧）
foreach ($m in @(Get-GdModules -AvailableOnly)) {
    $id = [string]$m.Id
    $kind = if ($m.PSObject.Properties.Name -contains 'VisionKind') { [string]$m.VisionKind } else { '' }
    Check "[$id] 清单声明了 VisionKind(外壳不许自己记 ark/mumu 对照表)" ($kind.Length -gt 0) "kind=[$kind]"
    Check "[$id] VisionKind 是小写短名(它是喂给核内 resolve-rescue-context 的那一枚 target，大小写或空格都会让认不到)" (
        $kind -match '^[a-z][a-z0-9]*$') $kind
    # 2026-11-06 刀⑮ 笔(d)：下面三格原来咬"PS 后端文件在不在／写没写进 Libs／靶子函数点没点到"，那三件事随三件出仓。
    Check "[$id] 这一路的 PS 后端已随三件出仓(lib\vision-$kind.ps1 不许留在树上)" (
        -not (Test-Path -LiteralPath (Join-Path $root "lib\vision-$kind.ps1")))
    Check "[$id] 靶子命令没在现树复活(New-GdVisionTarget-$kind 由核内 resolve-rescue-context 顶替)" (
        -not (Get-Command "New-GdVisionTarget-$kind" -ErrorAction SilentlyContinue))
}
# VisionKind 摘了后端不等于摘了消费者：入口那一路仍只问清单(在册那条"业务知识只存一份"的判据换了宿主，没换出处)。
$askSrcT = ''
try { $askSrcT = [System.IO.File]::ReadAllText((Join-Path $root 'tools\vision-ask.ps1')) } catch { }
Check 'VisionKind 仍被入口消费(tools\vision-ask.ps1 只问清单，不自己记 kind 表；读不到那一份就红)' (
    $askSrcT.Length -gt 0 -and $askSrcT.Contains('VisionKind')) ('vision-ask.ps1=' + $askSrcT.Length + ' 字符')
# 框架与工具层不许硬写某一路的 kind：内核只按命名约定拼函数名，外壳只问清单。
foreach ($f in @('lib\vision-rescue.ps1', 'lib\modules.ps1', 'tools\vision-ask.ps1', 'daily-clear.ps1', 'status.ps1', 'panel.ps1')) {
    $p = Join-Path $root "tools\$f"
    if (-not (Test-Path -LiteralPath $p)) { $p = Join-Path $root $f }
    if (-not (Test-Path -LiteralPath $p)) { continue }
    $code = Get-GdCodeOnlyText -Text ([System.IO.File]::ReadAllText($p))
    $hard = $code -match "-Target\s+'(ark|mumu)'" -or $code -match "-Kind\s+'(ark|mumu)'" -or
            $code -match 'New-GdVisionTarget-(ark|mumu)\b'
    Check "$(Split-Path -Leaf $p) 没有把兜底 kind 当字面量硬写(-Target/-Kind/New-GdVisionTarget-xxx)" (-not $hard)
}
# 三份模板都得有那个领奖白名单键：导出闸门(Test-GdTemplateCoversCode)要等到打发布包才查得到……（细节见该帧）
foreach ($tpl in @('config\settings.example.json', 'publish\src\ark\config\settings.example.json', 'publish\src\r1999\config\settings.example.json')) {
    $p = Join-Path $root $tpl
    if (-not (Test-Path -LiteralPath $p)) {
        # publish\src\ 那两份**只有合体工程里有**：导出仓库整个 publish\ 都不发(见 SPLIT-PLAN)，
        if ($tpl -like 'publish\*') { Write-Host "  (这棵树没有 $tpl —— 导出仓库里不发 publish\，跳过)"; continue }
        Check "$tpl 在(模板缺了就是安装即崩)" $false; continue
    }
    $txt = [System.IO.File]::ReadAllText($p)
    Check "$tpl 带 visionAllowReward=false(默认硬禁领奖)" ($txt -match '"visionAllowReward"\s*:\s*false')
}

Write-Host "`n[sv] 监督 agent(11-02 刀③ 后只剩轮档静态锚，PS 两件已出仓)"
# 这一段现在只剩源级静态锚(量的是 lib\rust-round.ps1 的接线形状)：不喂字典、不起进程、不碰现场状态。

# ---- 监督族刀①(11-02 接线)静态锚 → 刀⑩(11-05 进核)按新轴翻形：轮档件不再答监督，钉的是"确实不再答" ----
$rrSrc = ''
try { $rrSrc = [System.IO.File]::ReadAllText((Join-Path $root 'lib\rust-round.ps1')) } catch { }
$rrSrc = ($rrSrc -replace '(?s)<\#.*?\#>', ' ') -replace '(?m)^\s*#.*$', ' '
$coreRsForSv = ''
try { $coreRsForSv = [System.IO.File]::ReadAllText((Join-Path $root 'tools\rustcore\src\main.rs')) } catch { }
Check '⑩臂出册(start)：轮档码面不再问 start-supervisor(监督那一档的家已进核，留臂就是死臂)' (
    -not $rrSrc.Contains("Invoke-GdCli -Verb 'start-supervisor'")) $(
    if ($rrSrc.Length -eq 0) { 'lib\rust-round.ps1 不在场(尺没接到电)' } else { "len=$($rrSrc.Length)" })
Check '⑩臂出册(stop)：轮档码面不再问 stop-supervisor(同上)' (
    -not $rrSrc.Contains("Invoke-GdCli -Verb 'stop-supervisor'"))
Check '⑩名册只剩提权那一枚：外发名册里不再有 round-supervisor-* 的动作字面(一次删两侧，名册与臂同批)' (
    -not $rrSrc.Contains('round-supervisor-start') -and -not $rrSrc.Contains('round-supervisor-stop'))
Check '③回落臂摘净(start)：轮档码面不再有 Start-GdSupervisor 那枚 PS 真身(原"回落臂在"翻负，它不再量兜底路完好)' (
    -not $rrSrc.Contains('Start-GdSupervisor'))
Check '③回落臂摘净(stop)：轮档码面不再有 Stop-GdSupervisor 那枚 PS 真身(同上)' (
    -not $rrSrc.Contains('Stop-GdSupervisor'))
Check '⑩回执键形随臂出册：Available/Err 那枚拼串在轮档码面归零(键形的事实源改由核内 §28 R-sup-direct 钉)' (
    ([regex]::Matches($rrSrc, 'Available = \[bool\]\$core\.Available; Err = \[string\]\$core\.Err')).Count -eq 0) $(
    'hits=' + ([regex]::Matches($rrSrc, 'Available = \[bool\]\$core\.Available')).Count)
Check '⑩语义边界换了家：轮档码面不再喂 runtime 四键的 body，整枚 settings 更不许往下传(四键的现读归 C# 侧)' (
    -not $rrSrc.Contains('settings = [pscustomobject]@{ runtime = [pscustomobject]@{') -and
    -not [regex]::IsMatch($rrSrc, 'settings\s*=\s*\$S\b'))
Check '⑩顺序锚换轴：段 8 睡眠屏蔽改由侧信道回调补位，且仍"只设一次＋按 $esHeld 撤位"(旧 ask 臂末尾那形不许复活)' (
    $rrSrc.Contains('$esBox.Value = Start-GdRoundHostNoSleep -S $S') -and
    $rrSrc.Contains('Stop-GdRoundHostNoSleep -WasSet $esHeld') -and
    -not $rrSrc.Contains('if (-not $esHeld) {'))
Check '⑩触发串两侧同源：轮档件那根针与核内段 8 那句人话是同一段字面(改一侧的措辞当场红，屏蔽不许静默失效)' (
    $rrSrc.Contains(".Contains('已临时禁止系统睡眠')") -and $coreRsForSv.Contains('"已临时禁止系统睡眠')) $(
    'rr=' + $rrSrc.Contains(".Contains('已临时禁止系统睡眠')") + ' core=' + $coreRsForSv.Contains('"已临时禁止系统睡眠'))

# settings 模板覆盖：supervisor 四键在每份 example 里都要有(publish-split 的键覆盖检查兜运行时引用，这里先钉静态)
foreach ($tpl in @('config\settings.example.json', 'publish\src\ark\config\settings.example.json', 'publish\src\r1999\config\settings.example.json')) {
    $tp = Join-Path $root $tpl
    if (-not (Test-Path -LiteralPath $tp)) { continue }
    $tt = [System.IO.File]::ReadAllText($tp)
    $miss = @('supervisor', 'supervisorIntervalSeconds', 'supervisorQuietMinutes', 'supervisorMaxInterventions') |
            Where-Object { $tt -notmatch ('"' + [regex]::Escape($_) + '"') }
    Check "$tpl 覆盖监督的 4 个 runtime 键" ($miss.Count -eq 0) ($miss -join ' ')
}

Write-Host "`n[m12] 版本号：唯一来源 + 三个读数都接上(2026-09-26 起)"
# 为什么钉这一条：v2026.09.26 那次发布时，版本只活在 git tag 上 —— 代码/exe/面板里都没有读数，
$verFile = Join-Path $root 'VERSION'
Check '仓库根 VERSION 在(它是唯一的版本来源)' (Test-Path -LiteralPath $verFile)
$verNow = ''
if (Test-Path -LiteralPath $verFile) { $verNow = Get-GdVersion }
Check "VERSION 的形状是 x.y.z(读到 '$verNow')" ($verNow -match '^\d+\.\d+\.\d+$')
Check 'Get-GdVersion 读的就是仓库根那份(不是硬编码回来的)' ((Get-GdVersion -File $verFile) -eq $verNow)
# 坏值一律退成 0.0.0：只读的体检/面板不许因为一个文件写坏就抛(喂临时假文件，不要求机器现场状态)
$verTmp = Join-Path $env:TEMP ('gd-ver-' + $PID)
[void](New-Item -ItemType Directory -Path $verTmp -Force)
try {
    'v1.2.3' | Set-Content -LiteralPath (Join-Path $verTmp 'v.txt') -Encoding ascii
    '1.2'    | Set-Content -LiteralPath (Join-Path $verTmp 'short.txt') -Encoding ascii
    ''       | Set-Content -LiteralPath (Join-Path $verTmp 'empty.txt') -Encoding ascii
    $bad = @((Get-GdVersion -File (Join-Path $verTmp 'v.txt')), (Get-GdVersion -File (Join-Path $verTmp 'short.txt')),
             (Get-GdVersion -File (Join-Path $verTmp 'empty.txt')), (Get-GdVersion -File (Join-Path $verTmp 'nope.txt')))
    Check '坏值(带 v / 段数不对 / 空 / 文件不在)全退成 0.0.0，且不抛' (@($bad | Where-Object { $_ -ne '0.0.0' }).Count -eq 0) ($bad -join ' ')
} finally { Remove-Item -LiteralPath $verTmp -Recurse -Force -ErrorAction SilentlyContinue }
# 第二处写死：拿同一个扫描函数扫工作副本，命中只允许是 VERSION 自己
# 依赖锁文件豁免(2026-11-21)：Cargo.lock / package-lock.json 里的版本是第三方 crate / npm 包
# 的版本，不是"第二处写死仓库版本"；安装记录 config\plugins.json 里的 version 是装那一刻从插件
# plugin.json 抄下来的本机状态(gitignore，不随仓发)，也不是第二处版本来源。豁免只认这四种名字的
# 路径末尾，源码面照钉。
# 判据自身带正反例自证——放行的名字若写成会漏源码面的形状，那一格当场红。
$verLockRe = '(?i)(^|[\\/])(Cargo\.lock|package-lock\.json|npm-shrinkwrap\.json|plugins\.json) <-'
Check '版本号扫描的豁免针只认依赖锁与安装记录(正例咬住 / 反例不误伤)' (
    ('tools\gdops\src-tauri\Cargo.lock <- 版本号' -match $verLockRe) -and
    ('config\plugins.json <- 版本号' -match $verLockRe) -and
    ('tools\panel-cs\Panel.cs <- 版本号' -notmatch $verLockRe) -and
    ('lib\my-plugins.json <- 版本号' -notmatch $verLockRe))
if ($verNow -and $verNow -ne '0.0.0') {
    $verRaw = @(Test-GdExportLeaks -Dir $root -Values @([pscustomobject]@{ Label = '版本号'; Value = $verNow }) |
        Where-Object { $_ -notmatch '^VERSION <-' })
    $verHits = @($verRaw | Where-Object { $_ -notmatch $verLockRe })
    Check "全仓只有 VERSION 自己写着 '$verNow'(没有第二处写死版本号；依赖锁/安装记录豁免 $($verRaw.Count - $verHits.Count) 条)" ($verHits.Count -eq 0) (@($verHits) -join ' ｜ ')
}
# 三个读数各自接上来源(源级钉法：改了来源却忘了下游，这里红，而不是等玩家截图里两个版本打架)。
function Read-GdSrcIfPresent {
    param([string]$Rel)
    $p = Join-Path $root $Rel
    if (-not (Test-Path -LiteralPath $p)) { return $null }
    return [System.IO.File]::ReadAllText($p)
}
$stText = Read-GdSrcIfPresent 'status.ps1'
Check 'status.ps1 的「版本」行走 Get-GdVersion' ([string]::IsNullOrEmpty($stText) -or ($stText -match 'Get-GdVersion' -and $stText -match "'版本'")) `
    $(if ($stText) { '' } else { '跳过：这一棵树没有 status.ps1' })
$pData = Read-GdSrcIfPresent 'tools\panel-cs\Panel.Data.cs'
$pMain = Read-GdSrcIfPresent 'tools\panel-cs\Panel.cs'
Check 'C# 面板读仓库根 VERSION 当标题，且自己不含版本号字面量' (
    ([string]::IsNullOrEmpty($pData) -or [string]::IsNullOrEmpty($pMain)) -or
    (($pData -match '"VERSION"') -and ($pMain -match '状态面板 · v" \+ version') -and
     -not ($pData -match 'Version\("\d' -or $pMain -match '· v\d'))) `
    $(if ($pData -and $pMain) { '' } else { '跳过：这一棵树没有 panel-cs 源码' })
$launcher = Read-GdSrcIfPresent 'publish\exe\GDLauncher.cs'
Check 'exe 有 --version，读的是释放出来的 app\VERSION(不是常量)' (
    ([string]::IsNullOrEmpty($launcher)) -or
    (($launcher -match '"--version"') -and ($launcher -match 'Path\.Combine\(appDir, "VERSION"\)'))) `
    $(if ($launcher) { '' } else { '跳过：exe 外壳只在本地那棵树里，不进导出仓库' })
# 顺手钉一件本轮真踩到的事：往 GDLauncher.cs 里插方法时，把下一个方法的签名挤到了注释同一行 ——
if ($launcher) {
    $swallowed = @()
    foreach ($ln in ($launcher -split "`n")) {
        $t = $ln.Trim()
        if ($t.StartsWith('//') -and $t -match '\b(private|public|internal|protected)\s+(static\s+)?[\w\[\]<>,\s]+\s+\w+\s*\(') { $swallowed += $t.Substring(0, 40) }
    }
    Check 'GDLauncher.cs 里没有"方法签名被并进注释行"(那只有 build-exe 编得到，自检平时碰不到)' ($swallowed.Count -eq 0) ($swallowed -join ' ｜ ')
}
$beText = Read-GdSrcIfPresent 'tools\build-exe.ps1'
Check 'build-exe 拒绝形状不对的 VERSION，并把它盖进 exe 的文件属性' (
    ([string]::IsNullOrEmpty($beText)) -or
    (($beText -match "'VERSION'") -and ($beText -match 'AssemblyFileVersion') -and ($beText -match '不打包'))) `
    $(if ($beText) { '' } else { '跳过：打包脚本不进导出仓库' })
$psText = Read-GdSrcIfPresent 'tools\publish-split.ps1'
Check 'VERSION 进导出归属表的共用内核(两仓都带)' (
    ([string]::IsNullOrEmpty($psText)) -or ($psText -match "'VERSION',")) `
    $(if ($psText) { '' } else { '跳过：导出脚本本身不进导出仓库' })

$res = Show-GdTestResult -Tag 'modules'
if ($NoExit) { return $res }
exit $(if ($res.Fail) { 1 } else { 0 })
