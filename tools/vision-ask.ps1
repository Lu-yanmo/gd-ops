# tools/vision-ask.ps1 — 只问不点：把画面交给视觉模型看一眼，把它说的话原样打出来
# 用法: pwsh -NoProfile -File tools\vision-ask.ps1 [-Game ark|r1999] [-Image 某张.png] [-Context "一句话"]
#       需要 PowerShell 7；要 -Image 的那张图自己给路径，本文件不替你找。
#
# 为什么要有这一份：兜底那条链是"卡住了才兜一次"，一次只留一句结论
# (Detail 那一行)，问得不满意也没地方补问。排查时真正想要的是**同一个画面反复问、看模型到底怎么说**，
# 而且绝对不想在这个过程中往游戏里点一下 —— 所以这里复用同一个靶子、同一次问价；11-06 刀⑮ 笔(d)：那两层原来的住处
# (lib\vision-rescue.ps1 的 Get-GdVisionTarget／Invoke-GdVisionRaw)已整件出仓，现在靶子问核内 resolve-rescue-context、问价问 vision-ask-model，两侧都不再是 PS 函数。整份文件只读：不写 settings、不动窗口状态、不改 state(除了那张临时截图)。
#
# 两种输入：
#   -Image 已有一张 PNG(比如从 state\rescue\ 里挑出来的历史截图) → 直接问它，连截屏这一步都省了；
#   -Game ark / r1999 → 向那一路的靶子要一张现拍(端游走 PrintWindow，模拟器走 adb screencap)。
#
# 依赖：调用方点 lib\common.ps1 + lib\modules.ps1，业务库(含 vision 后端)由清单点进来；
# 后端文件不在这棵树上(单游戏仓库只带自己那一份)就如实说"这棵树上没有这一路"，不猜。

[CmdletBinding()]
param(
    [string]$Game = '',
    [string]$Image = '',
    [string]$Context = '',
    [string]$Keep = ''          # 想把这次用的截图留下来就给个目标文件名(默认用完删掉)
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $root 'lib\common.ps1')
. (Join-Path $root 'lib\modules.ps1')
[void](Import-GdModules -Quiet)
foreach ($libPath in (Get-GdModuleLibPaths)) { . $libPath }   # vision-rescue 和各路后端都从清单里点进来

$S = Get-GdSettings -TellAndExit

# 模块 id 和靶子 kind 不是一回事(1999 的兜底对象是"模拟器屏幕"，kind 叫 mumu)，
# 所以对照表写在各自的清单上(VisionKind)，这里只问清单 —— 外壳不许自己记一张 ark/mumu 表。
function Get-GdVisionKindFor {
    param([Parameter(Mandatory)][string]$Id)
    $m = Get-GdModule -Id $Id
    if (-not $m) { return '' }
    if ($m.PSObject.Properties.Name -contains 'VisionKind') { return [string]$m.VisionKind }
    return [string]$Id          # 清单没写就按 id 找：第三个模块不写 VisionKind 也能用
}

if (-not $Game -and -not $Image) {
    $have = @(foreach ($m in @(Get-GdModules -AvailableOnly)) { [string]$m.Id })
    Stop-GdBeforeRun "要拍现成的画面就得说清问哪一路：-Game 给一个($($have -join ' / '))；已经有图就只给 -Image 那张的路径"
}
if ($Image -and -not (Test-Path -LiteralPath $Image)) { Stop-GdBeforeRun "-Image 指的文件不存在：$Image" }

$kind = if ($Game) { Get-GdVisionKindFor -Id $Game } else { '' }
if ($Game -and -not $kind) { Stop-GdBeforeRun "-Game $Game 不是这棵树上的模块(有的：$((Get-GdModuleIds -AvailableOnly) -join ' / '))" }
# 11-06 刀⑮ 笔(c)：靶子不再从 PS 后端文件里要(Get-GdVisionTarget 那族随三件本体出仓)，
# 改问核 resolve-rescue-context —— 六道现场闸的判语与话术全在核里，这里只转达。
# mumu 一路的端点三源两正则也不在这侧：先问 resolve-mumu-adb-endpoint 拿 adb/serial 再喂 ctx。
$ctx = $null
$ep = $null
if ($kind) {
    $body = [ordered]@{ target = $kind }
    if ($kind -eq 'mumu') {
        $ep = Invoke-GdCli -Verb 'resolve-mumu-adb-endpoint' -Body ([pscustomobject]@{})
        $body['adb'] = [string]$ep.Adb
        $body['serial'] = [string]$ep.Serial
    }
    $ctx = Invoke-GdCli -Verb 'resolve-rescue-context' -Body ([pscustomobject]$body)
}

Write-Host '== 问谁 ==' -ForegroundColor Cyan
if ($ctx) {
    Write-Host "  后端    : $($ctx.Kind)($($ctx.Name))"
    Write-Host ('  开关    : ' + $(if ($ctx.Enabled) { '开' } else { "关 —— $($ctx.Skip)" }))
    Write-Host "  要管理员: $(if ($ctx.NeedsAdmin) { "是$(if ($ctx.AdminWhy) { "($($ctx.AdminWhy))" })" } else { '否' })   当前$(if (Test-GdAdmin) { '已提权' } else { '未提权' })"
} else {
    Write-Host '  后端    : (用 -Image 那张已有图，不需要后端)'
}
$model = if ($S.runtime.visionModel) { [string]$S.runtime.visionModel } else { 'qwen3.8-flash(默认)' }
Write-Host "  模型    : $model"
Write-Host "  端点    : $((Invoke-GdCli -Verb 'resolve-vision-endpoint' -Body ([pscustomobject]@{ base = [string]$S.runtime.visionBaseUrl })).Url)"
$py = Get-GdPythonExe -S $S
Write-Host "  Python  : $(if ($py) { $py } else { '没找到可用的 → 跑 setup.ps1 -WithPython' })"
# 11-06 刀⑮ 笔(c)：key 的取法现地内联(与 1999-event-audit.ps1 同一形制)——Get-GdVisionApiKey 那枚壳随三件本体出仓。
$key = $env:DASHSCOPE_API_KEY
if (-not $key -and $S.runtime -and $S.runtime.visionApiKey) { $key = [string]$S.runtime.visionApiKey }
Write-Host "  key     : $(if ($key) { '已配(内容不打印)' } else { '没配 → 后面问不成' })"

# ---------- 拿到一张图 ----------
# $shot 要留着：模型给的是千分比，换算成"这张图上的第几个像素"得有宽高等信息，
# 而这些只有截屏那一步的对象里有。用 -Image 时没有它 → 只报千分比、不报像素。
$path = $Image
$tmp = ''
$shot = $null
if (-not $Image) {
    if (-not $ctx.Enabled) { Stop-GdBeforeRun "这一路的兜底开关是关的($($ctx.Skip))。只想看模型怎么答：把开关打开，或者把 -Image 指到一张已有截图" }
    if ($ctx.NeedsAdmin -and -not (Test-GdAdmin)) { Stop-GdBeforeRun "这一路要管理员权限才拍得到东西：$($ctx.AdminWhy)。用「以管理员身份运行」打开 PowerShell 再跑本文件" }
    if (-not $ctx.CanAsk) { Stop-GdBeforeRun ([string]$ctx.Skip) }
    $tmp = Join-Path (Join-Path (Get-GdDir 'State') 'rescue') ('ask-' + (Get-Date -Format 'MMdd-HHmmss') + '-' + $ctx.Kind + '.png')
    if ($ctx.Kind -eq 'mumu') {
        $shot = Invoke-GdCli -Verb 'mumu-screen-shot' -Body ([pscustomobject]@{ path = $tmp; adb = [string]$ep.Adb; serial = [string]$ep.Serial })
    } else {
        $shot = Invoke-GdCli -Verb 'ark-window-shot' -Body ([pscustomobject]@{ path = $tmp })
    }
    if (-not $shot.Ok) { Stop-GdBeforeRun "截图失败：$($shot.Why)" }
    $path = $shot.Path
    Write-Host "`n== 画面 ==" -ForegroundColor Cyan
    Write-Host "  $($shot.ImgW)x$($shot.ImgH)  平均亮度 $($shot.AvgLum)  存于 $($shot.Path)"
    if ([int]$shot.AvgLum -lt 8) { Write-Host '  这一屏几乎是全黑，模型会一本正经地胡说八道 —— 还是问，但结论别信' -ForegroundColor Yellow }
} else {
    Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue
    $bmp = $null
    try {
        $bmp = New-Object System.Drawing.Bitmap($Image)
        Write-Host "`n== 画面 ==`n  $($bmp.Width)x$($bmp.Height)  ($Image)" -ForegroundColor Cyan
    } catch { Write-Host "  这张图读不动：$($_.Exception.Message)" -ForegroundColor Yellow } finally { if ($bmp) { $bmp.Dispose() } }
}

# ---------- 问一次，只问 ----------
Write-Host "`n== 问模型 ==" -ForegroundColor Cyan
if ($Context) { Write-Host "  带去的上下文：$Context" -ForegroundColor DarkGray }
$scene = if ($ctx) { [string]$ctx.Scene } else { '' }
$userText = if ($ctx) { [string]$ctx.UserText } else { '' }
# 11-06 刀⑮ 笔(c)：问价与抠判定并成一次问核 —— vision-ask-model 的回件里就带 Verdict
# (核内 Vision.Ask 末尾自己跑了一遍 Verdict，空文本/抠不出 JSON 时为 null，与旧 Get-GdVisionVerdictFromText 回 $null 同形)。
$ask = Invoke-GdCli -Verb 'vision-ask-model' -Body ([pscustomobject]@{ imagePath = [string]$path; scene = $scene; userText = $userText; context = $Context })
if (-not $ask.Ok) {
    Write-Host "  没问成：$($ask.Error)" -ForegroundColor Red
    if ($tmp -and -not $Keep) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    exit 1
}
Write-Host "  HTTP $($ask.HttpStatus)  模型 $($ask.Model)  用时 $($ask.ElapsedMs) ms  发出去 $($ask.Attempts) 次"
Write-Host "`n== 模型原话 ==" -ForegroundColor Cyan
Write-Host ([string]$ask.Text)

$v = $null
if ($ask.Verdict -and [bool]$ask.Verdict.Found) { $v = $ask.Verdict }
if (-not $v) {
    Write-Host "`n没从这段回复里抠出 JSON → 按兜底的规矩，这种话不会被拿去点任何东西。" -ForegroundColor Yellow
} else {
    Write-Host "`n== 抠出来的判定 ==" -ForegroundColor Cyan
    Write-Host "  blocking=$($v.Blocking)  what=$($v.What)"
    if (@($v.Clicks).Count) {
        Write-Host '  候选点位(按模型的把握从大到小)：'
        $i = 0
        foreach ($c in @($v.Clicks)) {
            $i++
            $pt = $null
            if ($shot) {
                $cp = Invoke-GdCli -Verb 'vision-click-point' -Body ([pscustomobject]@{
                    nx = $c.X; ny = $c.Y
                    imgW = [int]$shot.ImgW; imgH = [int]$shot.ImgH
                    clientLeft = [int]$shot.ClientLeft; clientTop = [int]$shot.ClientTop
                    clientW = [int]$shot.ClientW; clientH = [int]$shot.ClientH
                    max = 1000; why = [string]$c.Why
                })
                if ([bool]$cp.Found) { $pt = $cp }
            }
            $where = ''
            if ($shot) { $where = if ($pt) { "→ 图上像素 $($pt.BitmapX),$($pt.BitmapY)" } else { '→ 落在这张图外面，兜底会拒掉它' } }
            Write-Host ("    {0} ({1,4},{2,4})  {3} {4}" -f $i, [int]$c.X, [int]$c.Y, $c.Why, $where)
        }
    }
    Write-Host '  本文件没有点任何东西。真要去救火，等卡住时由核内兜底编排自己兜，或直接跑日常入口。' -ForegroundColor Green
}
if ($Keep) {
    Copy-Item -LiteralPath $path -Destination $Keep -Force
    Write-Host "  截图已留到 $Keep" -ForegroundColor DarkGray
}
if ($tmp -and -not $Keep) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
