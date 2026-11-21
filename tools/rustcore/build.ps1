# tools/rustcore/build.ps1 —— 现场编 tools\rustcore 这枚 crate，并把产物过一遍它自己的门。
# 形状学 tools\panel-cs\build.ps1(源码在仓、产物不入仓、要用的时候现编)，但有一条**故意不一样**：
# panel-cs 找不到系统 csc 时可以 `-Quiet` 安静让路(退回 PowerShell 面板，玩家不受影响)；
# 这里**不许静默跳过** —— 内置件按 §8.3(c) 选的是"进 exe 出货链路"，静默跳过的下场是出一枚
# 不含内置件的 exe，而它看起来和含内置件的那枚一模一样，肉眼分不出来。找不到工具链就当打包失败，
# 并把装它的命令印出来(判据原文 docs\r0-adrs.md §5.2 末段)。
#
#   pwsh -File tools\rustcore\build.ps1            # 编 + 跑 selftest + 契约号门，人读
#   pwsh -File tools\rustcore\build.ps1 -Quiet     # 只留最后一行 GD-RUST <json>，给 tools\build-exe.ps1 解析
[CmdletBinding()]
param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here '..\..')).Path
. (Join-Path $root 'lib\common.ps1')     # 期望契约号只有一份：Get-GdRustContract(壳侧)，与 main.rs 的 const 同改

function Get-GdCargo {
    <# 本机实测：cargo 装在 %USERPROFILE%\.cargo\bin 且**不在默认 PATH**(`command -v cargo` 找不到、显式路径能跑)，
       所以这里按"显式路径优先、PATH 兜底"找，绝不假设它在。找不到返回空串，由调用方报错。 #>
    $cands = @()
    if ($env:GD_CARGO) { $cands += $env:GD_CARGO }
    if ($env:USERPROFILE) { $cands += (Join-Path ([string]$env:USERPROFILE) '.cargo\bin\cargo.exe') }
    if ($env:CARGO_HOME) { $cands += (Join-Path ([string]$env:CARGO_HOME) 'bin\cargo.exe') }
    foreach ($c in $cands) { if ($c -and (Test-Path -LiteralPath $c)) { return [string]$c } }
    foreach ($n in @('cargo.exe', 'cargo')) {
        $cmd = @(Get-Command $n -ErrorAction SilentlyContinue)
        if ($cmd.Count) { return [string]$cmd[0].Source }
    }
    return ''
}

function Get-GdCrateBins {
    <# 产物名从 Cargo.toml 现读，不在本文件里写死 crate 名(gdprobe 会变，写死 = 改了名字这里还编旧的那枚)。
       没有 [[bin]] 段时，cargo 默认产出一枚与 package.name 同名的 exe。 #>
    param([string]$TomlPath)
    $text = [string](Get-Content -LiteralPath $TomlPath -Raw -Encoding utf8)
    if (-not $text) { throw "Cargo.toml 读不到内容：$TomlPath" }
    $names = @()
    $pkg = [regex]::Match($text, '(?ms)^\[package\](.*?)(?=^\[|\z)')
    if ($pkg.Success) {
        $m = [regex]::Match($pkg.Groups[1].Value, '(?m)^\s*name\s*=\s*"([^"]+)"')
        if ($m.Success) { $names += $m.Groups[1].Value }
    }
    foreach ($b in [regex]::Matches($text, '(?ms)^\[\[bin\]\](.*?)(?=^\[|\z)')) {
        $m = [regex]::Match($b.Groups[1].Value, '(?m)^\s*name\s*=\s*"([^"]+)"')
        if ($m.Success) { $names += $m.Groups[1].Value }
    }
    $names = @($names | Where-Object { $_ } | Sort-Object -Unique)
    if (-not $names.Count) { throw "$TomlPath 里既没有 [package].name 也没有 [[bin]].name —— 不知道要产出什么，不编" }
    return @($names)
}

$cargo = Get-GdCargo
if (-not $cargo) {
    throw ('找不到 Rust 工具链(cargo)。这一步不做跳过开关：既然内置件进了出货链路，编不出来就当打包失败。' +
        ' 装它：winget install --id Rustlang.Rustup ；装完 cargo 在 %USERPROFILE%\.cargo\bin\cargo.exe(本机实测就在这个路径、且不在默认 PATH，本脚本已按显式路径找它)')
}

$toml = Join-Path $here 'Cargo.toml'
if (-not (Test-Path -LiteralPath $toml)) { throw "没有 $toml —— 这一目录里没有 crate，不编" }
$bins = @(Get-GdCrateBins -TomlPath $toml)
$releaseDir = Join-Path $here 'target\release'

Push-Location -LiteralPath $here
$prevEap = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    # --locked：Cargo.lock 与源码不一致就当失败(零外部依赖，锁文件本不该漂)
    # --offline：打包期不联网取依赖(这枚 crate 零依赖；联网一步会让"包为什么打不出来"多一个说不清的原因)
    $buildOut = & $cargo @('build', '--release', '--locked', '--offline') 2>&1
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        (@($buildOut | ForEach-Object { "$_" }) -join "`n") | Write-Host
        throw "cargo build --release 退出码 $code，不打包"
    }
} finally {
    $ErrorActionPreference = $prevEap
    Pop-Location
}

$expect = @(Get-GdRustContract)
$wantContract = [string]$expect[0]

# ---------- 契约号静态三面(不依赖编译，先比) ----------
# 2026-10-26 甲档第十四刀：原 `tools\rustcore\probe-events-golden.ps1` 的 H3 半随该门退役搬到这里。
# 产物自报那一面在下面 foreach 里比；其余三面只读文件：源码 const、`probe-connect.ps1` 的字面量
# (要求恰好一种拼法)、`keyfacts.json` 顶层 contract 与 contract_note。任一面失配 = 不打包。
# 搬进来的理由：那门是"改号同批动四处"唯一的静态把守，退役后若只剩产物自报，源码与清单可以带着旧号
# 一直躺着、直到下一次有人真去跑连接探针或读清单。
# ⚠ 本文件**随两仓导出**，而 `probe-connect.ps1`(开发态伴生件)与 `docs\r0-keyfacts.json`(设计文档)
#   不进公开仓：缺件按"这面没量"跳过、不判红；判红只在"件在、字面量对不上"。源码在两侧都在场，
#   缺了就是真失配。
$staticBad = @()
$staticSkip = @()
$srcMainPath = Join-Path $here 'src\main.rs'
$srcMainText = ''
if (Test-Path -LiteralPath $srcMainPath) { $srcMainText = [System.IO.File]::ReadAllText($srcMainPath, [System.Text.Encoding]::UTF8) }
$cmain = [regex]::Match($srcMainText, 'const\s+CONTRACT\s*:\s*&str\s*=\s*"([^"]+)"')
if (-not $cmain.Success) { $staticBad += 'src\main.rs 里读不到 const CONTRACT' }
elseif ($cmain.Groups[1].Value -ne $wantContract) { $staticBad += ('src\main.rs const=' + $cmain.Groups[1].Value + ' 壳=' + $wantContract) }

$shellProbe = Join-Path $here 'probe-connect.ps1'
if (-not (Test-Path -LiteralPath $shellProbe)) {
    $staticSkip += 'probe-connect.ps1 不在场(导出树)'
} else {
    $shellText = [System.IO.File]::ReadAllText($shellProbe, [System.Text.Encoding]::UTF8)
    $shellNums = @([regex]::Matches($shellText, 'RustCore-R\d+\.\d+') | ForEach-Object { $_.Value } | Sort-Object -Unique)
    if (@($shellNums).Count -ne 1) { $staticBad += ('probe-connect.ps1 的字面量集合=' + ((@($shellNums)) -join ',') + '(要求恰好一种拼法)') }
    elseif ($shellNums[0] -ne $wantContract) { $staticBad += ('probe-connect.ps1=' + $shellNums[0] + ' 壳=' + $wantContract) }
}

$kfPath = Join-Path $root 'docs\r0-keyfacts.json'
if (-not (Test-Path -LiteralPath $kfPath)) {
    $staticSkip += 'docs\r0-keyfacts.json 不在场(导出树)'
} else {
    $kfObj = $null
    try { $kfObj = ConvertFrom-Json ([System.IO.File]::ReadAllText($kfPath, [System.Text.Encoding]::UTF8)) } catch { $kfObj = $null }
    if ($null -eq $kfObj) { $staticBad += 'docs\r0-keyfacts.json 读不到或不是合法 JSON' }
    else {
        if ([string]$kfObj.contract -ne $wantContract) { $staticBad += ('keyfacts.contract=' + [string]$kfObj.contract + ' 壳=' + $wantContract) }
        if (-not ($kfObj.PSObject.Properties.Name -contains 'contract_note')) { $staticBad += 'keyfacts 缺 contract_note(升号那一批要留一句为什么)' }
    }
}
if (@($staticSkip).Count -and (-not $Quiet)) {
    foreach ($s in $staticSkip) { Write-Host ('契约号静态面跳过：' + $s) }
}
if (@($staticBad).Count) { throw ('契约号静态面失配，不打包：' + (@($staticBad) -join ' ｜ ')) }

$produced = @()
$lines = @()
foreach ($b in $bins) {
    $exe = Join-Path $releaseDir ($b + '.exe')
    if (-not (Test-Path -LiteralPath $exe)) { throw "cargo 退出码 0 但没有 $exe —— 产物对不上名字，不打包" }
    # 收流走 Invoke-GdNativeCapture：判据仍在这里自己判，函数只负责把两股流收成文本与退出码。
    # (5.1 时代实测：`2>&1` + Stop 会把 stderr 的一行 —— 那枚**故意**的 --out 负对照 —— 炸成终止错误；
    #  5.1 已退役(2026-10-06)，收流函数照旧用。)
    $stRun = Invoke-GdNativeCapture -Exe $exe -Arguments @('--selftest')
    $stCode = [int]$stRun.ExitCode
    $tail = @($stRun.Lines | Where-Object { $_.Trim() })
    $pass = ($stCode -eq 0) -and (@($tail | Where-Object { $_ -like 'RESULT SELFTEST-PASS*' }).Count -gt 0)
    if (-not $pass) {
        ($tail -join "`n") | Write-Host
        throw "$b --selftest 没通过(退出码 $stCode)，不打包"
    }
    # 契约号门：产物**自报**的契约号必须等于壳侧那一份期望值 —— 这就是 §3 那句
    # "没有契约号的 Rust 产物不许进重打步骤"的落点。自报走 --selftest 那行(探针唯一的自报面，见 §11.7 的 CLI 形状)。
    $passLine = [string](@($tail | Where-Object { $_ -like 'RESULT SELFTEST-PASS*' } | Select-Object -Last 1))[0]
    $cm = [regex]::Match($passLine, 'contract=(\S+)')
    $got = if ($cm.Success) { $cm.Groups[1].Value } else { '' }
    if (-not $got) { throw "$b 的 PASS 行里读不到 contract= —— 自报不了契约号的东西不许进包：$passLine" }
    if ($got -ne $wantContract) { throw "契约号不匹配：产物=$got 壳=$wantContract(壳侧期望在 lib\common.ps1 的 Get-GdRustContract)" }
    $chk = [regex]::Match($passLine, 'checks=(\d+)')
    $fi = Get-Item -LiteralPath $exe
    $hash = Get-FileHash -LiteralPath $exe -Algorithm SHA256
    $item = [ordered]@{
        Name     = $b
        Path     = $exe
        Bytes    = [long]$fi.Length
        Sha256   = [string]$hash.Hash
        Contract = $got
        Checks   = if ($chk.Success) { [int]$chk.Groups[1].Value } else { 0 }
    }
    $produced += [pscustomobject]$item
    $lines += ('  {0}.exe  {1} 字节  sha256={2}  契约={3}  selftest={4} 项' -f $b, $item.Bytes, $item.Sha256, $got, $item.Checks)
}

if (-not $Quiet) {
    Write-Host ('cargo: ' + $cargo)
    Write-Host ('crate: ' + $here)
    $lines | ForEach-Object { Write-Host $_ }
}
@{ Ok = $true; Cargo = $cargo; Contract = $wantContract; Bins = @($produced) } | ConvertTo-Json -Compress -Depth 6 |
    ForEach-Object { 'GD-RUST ' + $_ } | Write-Host
exit 0
