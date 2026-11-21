# tools/panel-cs/build.ps1 —— 用系统自带的 csc.exe(.NET Framework 4)把本目录的 .cs 编成 panel-cs.exe。
# 不依赖 dotnet SDK：玩家机器只要有 .NET Framework 4(Win10/11 出厂自带)就能跑产物。
# 源码从 2026-09-26 起是一个 partial class 的多份(Panel.cs + Panel.*.cs)，这里**按目录扫 *.cs 全给 csc**，
# 新加一份就自动进编译，不需要回来改这条命令行。
# 用法： pwsh -File tools\panel-cs\build.ps1 [-Verify] [-ConsoleTest] [-Quiet]
#   -Verify      = 编完把十三档自检(批次 0~7 + agent + B-2b + B-3②-a + 面板自证)逐条起、逐条等到退
#                  (2026-10-23 刀② 之前这一档印的 "OK" 是不可信的，原因见下面「跑 panel-cs.exe 的那把尺」那段头注)
#   -ConsoleTest = 再跑一遍 --consoletest(主控台四页：取数、写配置的转义与备份、代跑参数、诊断包不含 key)
#   -ExpTest     = 再跑一遍 --exptest(「实验功能」三个布尔键的读写回环，只打在 %TEMP% 副本上)
#   -HeadlessTest= 再跑一遍 --headless(窗体挪到屏幕外、真起一次预演，验"子进程输出→重定向→泵→忙碌复位"这条链)
#                  乙档刀② 把它单独立成一档，而不是塞进 -Verify 那十三档里：**这一档会起真子进程、要求配置在场**，
#                  而其余十三档的纪律是"只读自检不许要求机器现场状态"。验收链(publish-split)按内置件在场分档，
#                  也只有这一档需要那套分档判据。
#   -Quiet       = 只留失败信息，给 panel.cmd 自动编译用
[CmdletBinding()]
param([switch]$Verify, [switch]$ConsoleTest, [switch]$ExpTest, [switch]$HeadlessTest, [switch]$Quiet)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$cs   = @(Get-ChildItem -LiteralPath $here -Filter '*.cs' -File | Sort-Object Name | ForEach-Object { $_.FullName })
if ($cs.Count -eq 0) { throw "本目录里没有 .cs 源码：$here" }

# B-2b(2026-10-08)：Live.cs 那七枚现场 verb 要用的那批 user32 声明，**真身仍只有一份** =…（细节见该帧）
$gdwin = Join-Path (Split-Path $here) 'gdinterop\GdWin.cs'
if (-not (Test-Path -LiteralPath $gdwin)) {
    if ($Quiet) { exit 4 }
    throw ('缺 tools\gdinterop\GdWin.cs(现场 verb 的 user32 声明，真身只这一份)：' + $gdwin)
}
$cs += $gdwin
# R9-a(2026-10-17)：轮骨架四格收编之后，panel-cs 要多带两份**别处已有唯一真身**的 .cs……（细节见该帧）
$gdpower = Join-Path (Split-Path $here) 'gdinterop\GdPower.cs'
if (-not (Test-Path -LiteralPath $gdpower)) {
    if ($Quiet) { exit 4 }
    throw ('缺 tools\gdinterop\GdPower.cs(屏保 SPI 声明，真身只这一份)：' + $gdpower)
}
$cs += $gdpower
$gdlogs = Join-Path (Split-Path $here) 'gdcore\GdCore.Logs.cs'
if (-not (Test-Path -LiteralPath $gdlogs)) {
    if ($Quiet) { exit 4 }
    throw ('缺 tools\gdcore\GdCore.Logs.cs(history.csv 的 CSV 规则，真身只这一份)：' + $gdlogs)
}
$cs += $gdlogs
$exe  = Join-Path $here 'panel-cs.exe'
$icon = Join-Path (Split-Path $here) 'assets\icon.png'

# 64 位优先；极端机器没有 Framework64 再退回 32 位
$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $csc)) {
    if ($Quiet) { exit 3 }   # 自动编译场合：这台机器真没有系统 csc，安静让路
    throw '找不到系统自带 csc.exe(.NET Framework 4 编译器)'
}

$args = @(
    '/nologo', '/target:winexe', '/optimize+', '/codepage:65001',   # 源码是无 BOM 的 UTF-8，别按机器 ANSI 码页猜中文
    "/out:$exe",
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    # 批次 0(Core.Json.cs)破例上 JavaScriptSerializer：那次 Panel.Settings.cs 说"为一处自检不值当加引用"判断不变……（细节见该帧）
    '/reference:System.Web.Extensions.dll'
)
if (Test-Path -LiteralPath $icon) { $args += "/resource:$icon,gdpanel.icon.png" }
$args += $cs

if (-not $Quiet) { Write-Host ('csc: ' + $csc) }
& $csc @args
if ($LASTEXITCODE -ne 0) {
    if ($Quiet) { exit $LASTEXITCODE }   # 自动编译失败：安静退(2026-10-23 刀② 起 panel.cmd 不再回落 PowerShell 面板，这里只把"编不出来"这件事本身交回调用方)
    throw ('编译失败，退出码 ' + $LASTEXITCODE)
}
if (-not $Quiet) { Write-Host ('产物: ' + $exe + ' (' + (Get-Item -LiteralPath $exe).Length + ' bytes)') }

# ---------- 跑 panel-cs.exe 的那把尺(2026-10-23 乙档刀② 立) ----------…（细节见该帧）
function Invoke-GdPanelCsExe {
    <# 起一枚自检动词、等到它退、把 stdout 读回来。返回 @{Rc;Text;Last}。
        2026-10-23 版号轮起 Panel.cs 的 Main 在任何输出前按"已被重定向"装 UTF-8 写手(Cli.cs 的 UseUtf8IfRedirected)，stdout 自此落 UTF-8
        (此前本机现测是 cp936；旧 OEM 眼镜若留着，Get-GdSelfTestTally 判的那句中文「全绿|有红项」就成了空尺)。
        落盘的临时件在 %TEMP%，不进仓(仓内临时件会被 test-modules 的 [m8]/[m12] 内容门扫成假红)。 #>
    param([string]$Flag, [string]$SinkDir)
    $outPath = Join-Path $SinkDir ('stdout-' + $Flag.TrimStart('-') + '.txt')
    $errPath = Join-Path $SinkDir ('stderr-' + $Flag.TrimStart('-') + '.txt')
    foreach ($f in @($outPath, $errPath)) { if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force } }
    $proc = Start-Process -FilePath $exe -ArgumentList @($Flag) -NoNewWindow -Wait -PassThru -RedirectStandardOutput $outPath -RedirectStandardError $errPath
    if ($null -eq $proc.ExitCode) {
        throw ('拿不到 panel-cs ' + $Flag + ' 的退出码：这台机器的引擎用 Start-Process -Wait -PassThru 读不出 ExitCode(5.1 的老坑)。判红不判绿。')
    }
    $rc = [int]$proc.ExitCode
    $txt = ''
    if (Test-Path -LiteralPath $outPath) {
        $enc = [System.Text.Encoding]::UTF8
        $txt = [System.IO.File]::ReadAllText($outPath, $enc)   # 与 Panel.cs 的 Main 里那次换 UTF-8 写手同批：产物改编码，读数眼镜必须跟着翻
    }
    $nonEmpty = @($txt -split "`r?`n" | Where-Object { "$_".Trim().Length -gt 0 })
    $last = ''
    if ($nonEmpty.Count -gt 0) { $last = [string](@($nonEmpty | Select-Object -Last 1)[0]) }
    return @{ Rc = $rc; Text = $txt; Last = $last }
}

function Get-GdSelfTestTally {
    <# 自检自己报的那一句汇总(GdLog.Line 印的「tag 全绿：N/N 项」/「tag 有红项：N/M」)。
        有就交出来，让验收链的报告带着真读数；没有(面板那几档自报的是「布局自检: …」「结论: …」)就回空串，
        由调用方改用末行读数 —— 这里不造第二把尺，判据仍然是退出码。 #>
    param([string]$Text)
    $hit = @($Text -split "`r?`n" | Where-Object { "$_" -match '(全绿|有红项)[：:]' })
    if ($hit.Count -eq 0) { return '' }
    return [string](@($hit | Select-Object -Last 1)[0])
}

function Invoke-GdPanelCsSuite {
    <# 一条一条等到退：退出码非 0 就是红；自报了「有红项」也判红(与 rc=0 互相矛盾时绝不放过去)；
        绿的时候把自报读数一并印出来，报告面上那一档不再是光秃秃一句 "OK"。 #>
    param([object[]]$Steps, [string]$SinkDir)
    foreach ($st in $Steps) {
        $r = Invoke-GdPanelCsExe -Flag $st.Flag -SinkDir $SinkDir
        $tally = Get-GdSelfTestTally -Text $r.Text
        if ($r.Rc -ne 0) {
            throw ('panel-cs ' + $st.Flag + ' 有红项(退出码 ' + $r.Rc + $(if ($tally.Length -gt 0) { '，自报：' + $tally } elseif ($r.Last.Length -gt 0) { '，末行：' + $r.Last } else { '' }) + ')')
        }
        if ($tally -match '有红项') { throw ('panel-cs ' + $st.Flag + ' 退了 0 却自报有红项(两把尺对不上)：' + $tally) }
        if ($Quiet) { continue }
        $readout = $tally
        if ($readout.Length -eq 0) { $readout = $r.Last }
        Write-Host ('panel-cs ' + $st.Flag + ' OK' + $(if ($readout.Length -gt 0) { ' · ' + $readout } else { ' · (没收到任何自报行，判据＝退出码 0)' }))
    }
}

# 三档各自要跑的自检动词(注释逐条随动词一起搬进表里，一条都不丢)。
$verifySteps = @(
    # 批次 0 的 Core/契约自检先跑：轻、无窗体、只喂临时树(见 docs\ps-migration-batch0.md §6)
    @{ Flag = '--coretest'; Note = '批次 0 Core/契约' },
    @{ Flag = '--echotest'; Note = '批次 0 参数面' },
    # 批次 1 判定层：假数据回环，期望文案逐字抄自旧 PS(移植走样在这里就红)
    @{ Flag = '--dectest'; Note = '批次 1 判定层' },
    # 批次 2 探测/状态行：纯假树回环，期望串逐字抄自旧实现(同上一条纪律)
    @{ Flag = '--statetest'; Note = '批次 2 探测/状态行' },
    # 批次 3 外部进程层：纯函数回环(不起进程/不写文件)，期望串逐字抄自旧实现(同上一条纪律)
    @{ Flag = '--proctest'; Note = '批次 3 外部进程层' },
    # 批次 4 视觉兜底层：纯规则段回环(不联网/不碰窗口/不起子进程)，期望串逐字抄自旧实现(同上一条纪律)
    @{ Flag = '--visiontest'; Note = '批次 4 视觉兜底层' },
    # 批次 5 编排/排程层：纯规则段回环(不起进程/不读盘/不写文件)，期望串逐字抄自旧实现(同上一条纪律)
    @{ Flag = '--orchtest'; Note = '批次 5 编排层' },
    # 批次 6 监督层纯判定段：四 verb 回环(不读盘/不起进程)，期望串逐字抄自旧实现(同上一条纪律)
    @{ Flag = '--svtest'; Note = '批次 6 监督纯判定' },
    # 批次 7 活段判据：tick 剧本回环(读数/回执场景直喂，不碰挂钟/盘/进程)，期望串逐字抄自旧实现(同上一条纪律)
    @{ Flag = '--looptest'; Note = '批次 7 活段判据' },
    # 对话小窗那条链的纯规则段：找解释器的顺序 / CRT 命令行转义 / 信封投影(不起进程、不联网、不要求本机装了 Python)
    @{ Flag = '--agenttest'; Note = 'agent 层纯规则段' },
    # B-2b 现场件七枚(Live.cs)的纯规则段回环：真窗口/真 HTTP/真 key 一个都不碰，期望串逐字抄自旧实现(同上一条纪律)
    @{ Flag = '--livetest'; Note = 'B-2b 现场件纯规则段' },
    # B-3②-a 轮骨架现场件七枚(Round.cs)的纯规则段回环：不弹 UAC/不打 webhook/不起气泡/不真杀进程/不读真任务，
    # 写盘的两枚(通知与运行锁)打在 %TEMP% 临时树上(只读自检不许要求机器现场状态，同上一条纪律)
    @{ Flag = '--roundtest'; Note = 'B-3②-a 轮骨架纯规则段' },
    # 面板自证：读一次协议(核内直取，2026-10-23 刀② 起不再起 pwsh 子进程) + 渲染 + 逐页布局断言 + 主题切换链
    @{ Flag = '--selftest'; Note = '面板自证' }
)

if ($Verify -or $ConsoleTest -or $ExpTest -or $HeadlessTest) {
    $sink = Join-Path ([System.IO.Path]::GetTempPath()) ('gd-panelcs-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $sink | Out-Null
    try {
        if ($Verify) { Invoke-GdPanelCsSuite -Steps $verifySteps -SinkDir $sink }
        if ($ConsoleTest) {
            # 主控台四页：转义往返 / 只在 settings 临时副本上写 / 四页版式 / 代跑参数怎么拼 / 诊断包里搜不搜得到 key
            Invoke-GdPanelCsSuite -Steps @(@{ Flag = '--consoletest'; Note = '主控台四页' }) -SinkDir $sink
        }
        if ($ExpTest) {
            # 「实验功能」三个布尔键的读写回环：翻一次读得回来 / 只有那一行动过 / 补出来的那行过得了 ConvertFrom-Json /
            # runtime 形状认不出时必须拒着不写。全程打在 %TEMP% 副本上，收尾逐字节比对确认那份真 settings.json 没动。
            Invoke-GdPanelCsSuite -Steps @(@{ Flag = '--exptest'; Note = '实验功能三键' }) -SinkDir $sink
        }
        if ($HeadlessTest) {
            # 面板链路自证：真起一次预演(薄入口 -DryRun，不启动任何游戏/助手)，验"子进程输出→重定向文件→…（细节见该帧）
            $r = Invoke-GdPanelCsExe -Flag '--headless' -SinkDir $sink
            if ($r.Rc -ne 0) {
                Write-Host $r.Text
                throw ('panel-cs --headless 退出码 ' + $r.Rc + '(结论 ' + $r.Last + ')')
            }
            if (-not ($r.Text -match 'PASS')) {
                throw ('panel-cs --headless 退了 0 却没交出 PASS 读数(退出码与话术两把尺对不上)：' + $r.Last)
            }
            if (-not $Quiet) { Write-Host ('panel-cs --headless OK · ' + $r.Last) }
        }
    } finally {
        Remove-Item -LiteralPath $sink -Recurse -Force -ErrorAction SilentlyContinue
    }
}
