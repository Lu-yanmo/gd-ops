# lib/common.ps1 — 公共工具(PowerShell 7)
# 被 daily-clear.ps1 / status.ps1 / install-task.ps1 dot-source 引入

# 只用 1.0：允许访问不存在的 JSON 属性(返回 $null)，配置文件字段经常随版本变化
Set-StrictMode -Version 1.0
$ErrorActionPreference = 'Stop'
$PSDefaultParameterValues['*:Encoding'] = 'utf8'

# 中文输出别乱码：把当前进程的控制台/管道编码统一到 UTF-8
try {
    [System.Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    [System.Console]::InputEncoding  = [System.Text.Encoding]::UTF8
} catch { }

$global:GD = [pscustomobject]@{
    Root    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    Logs    = $null
    State   = $null
    Backup  = $null
    LogFile = $null
}
$global:GD.Logs   = Join-Path $global:GD.Root 'logs'
$global:GD.State  = Join-Path $global:GD.Root 'state'
$global:GD.Backup = Join-Path $global:GD.Root 'backups'
foreach ($d in @($global:GD.Logs, $global:GD.State, $global:GD.Backup)) {
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

function Get-GdRoot { $global:GD.Root }
function Get-GdDir  { param([string]$Name) $global:GD.$Name }

# ---------- 版本号：全工程只有一处写着它，就是仓库根那个 VERSION 文件 ----------…（细节见该帧）
$global:GdVersionCache = $null
function Get-GdVersion {
    param([string]$File, [switch]$Refresh)
    # -File 只给自检喂假文件用(规矩：只读自检不许要求机器现场状态)，这一支不碰缓存。
    $path = Join-Path $global:GD.Root 'VERSION'
    if ($File) { $path = $File }
    elseif ($global:GdVersionCache -and -not $Refresh) { return $global:GdVersionCache }
    $v = ''
    if ($script:GdCoreState -ne 'live') { throw '内核缺席：Get-GdVersion 无 PS 原身可退，本轮拒跑(刀⑲ 笔(d))' }
    # 刀⑲ 笔(d)：Trim＋形状校验＋0.0.0 兜底这条纯规则只有内核一份(双引擎实编对平，§11.287)。
    $raw = ''
    if (Test-Path -LiteralPath $path) { try { $raw = [string](Get-Content -LiteralPath $path -TotalCount 1) } catch { $raw = '' } }
    $v = [string]([GdCore.Versions]::Normalize($raw))
    if (-not $File) { $global:GdVersionCache = $v }
    return $v
}

# 进程内缓存(StrictMode 下必须先声明再读)
$global:GdStageTable = $null
# Python 定位结果：GdPythonExe 存找到的路径(空串=确实没有)，GdPythonProbed 区分"还没查过"和"查过没有"
# —— 少了后者的话，没装 Python 的机器上每问一次模型就要重跑一遍 `python -c`(还会撞上商店占位程序)
$global:GdPythonExe = ''
$global:GdPythonProbed = $false

function Write-GdLog {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Message,
        [ValidateSet('INFO', 'WARN', 'ERROR', 'OK', 'STEP')][string]$Level = 'INFO',
        [switch]$Quiet
    )
    $ts = Get-Date -Format 'HH:mm:ss'
    $line = "[$ts][$Level] $Message"
    if ($global:GD.LogFile) { Add-Content -LiteralPath $global:GD.LogFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')]$line" -Encoding utf8 }
    if (-not $Quiet) {
        switch ($Level) {
            'ERROR' { Write-Host $line -ForegroundColor Red }
            'WARN'  { Write-Host $line -ForegroundColor Yellow }
            'OK'    { Write-Host $line -ForegroundColor Green }
            'STEP'  { Write-Host $line -ForegroundColor Cyan }
            default { Write-Host $line }
        }
    }
}

function Start-GdTranscript {
    param([Parameter(Mandatory)][string]$Tag)
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $global:GD.LogFile = Join-Path $global:GD.Logs "run-$stamp-$Tag.log"
    Write-GdLog "日志文件：$($global:GD.LogFile)"
}

function Read-GdJson {
    param([Parameter(Mandatory)][string]$Path)
    if ($script:GdCoreState -ne 'live') { throw '内核缺席：Read-GdJson 无 PS 原身可退，本轮拒跑(刀⑲ 笔(d))' }
    # 刀⑲ 笔(d)：缺文件文案 + BOM 剥离只有内核一份；解析照旧交给宿主 cmdlet(契约 §8)。
    if (-not (Test-Path -LiteralPath $Path)) { throw ([GdCore.Json]::MissingFileMessage($Path)) }
    $text = [GdCore.Json]::StripBom([System.IO.File]::ReadAllText($Path))
    return $text | ConvertFrom-Json
}

function Write-GdJson {
    # 内核化定档(K1b)：本函数**不进内核**。序列化真身是宿主的 ConvertTo-Json，
    # 落盘编码是一句 BCL 调用，中间没有"PS 解释执行的纯规则"可收；
    # 且这里写出去的文件由 MAA/M9A 等外部程序读，复刻不全伤的是助手配置(实测见 docs\gd-core-contract.md §8)。
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Object, [int]$Depth = 100)
    $json = $Object | ConvertTo-Json -Depth $Depth
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Merge-GdJsonNode {
    <# 只补不覆盖：插件片段里已有同名键不进；对象递归，数组与标量整枚让给已存在的值。
       安装器(并进 settings.json)与 setup(并进模板)共用这一份，别各写一套合并规矩。 #>
    param([Parameter(Mandatory)]$Base, [Parameter(Mandatory)]$Add)
    $added = @()
    foreach ($p in $Add.PSObject.Properties) {
        $name = [string]$p.Name
        $cur = $Base.PSObject.Properties[$name]
        if (-not $cur) {
            $Base | Add-Member -NotePropertyName $name -NotePropertyValue $p.Value
            $added += $name
            continue
        }
        if ($p.Value -is [pscustomobject] -and $cur.Value -is [pscustomobject]) {
            $added += @(Merge-GdJsonNode -Base $cur.Value -Add $p.Value | ForEach-Object { "$name.$_" })
        }
    }
    return @($added)
}

function Merge-GdPluginFragments {
    <# 有效配置模板 = 内核模板 + 已装插件留下的片段(config\plugins\*.example.json，安装器抄的那份)。
       插件装/卸只动自己那份片段；settings.json 被删了重跑 setup，也能把各游戏的 settings 块接回来。 #>
    param([Parameter(Mandatory)]$Object, [Parameter(Mandatory)][string]$Root)
    $dir = Join-Path $Root 'config\plugins'
    if (Test-Path -LiteralPath $dir) {
        foreach ($f in @(Get-ChildItem -LiteralPath $dir -Filter '*.example.json' -File | Sort-Object Name)) {
            $frag = Read-GdJson -Path $f.FullName
            if ($frag) { [void](Merge-GdJsonNode -Base $Object -Add $frag) }
        }
    }
    return $Object
}

# ---------- PS→C# 转发(批次 1 起判定层的真身住在 tools\panel-cs\Decisions.cs) ----------
# 契约全文在 docs\ps-migration-batch0.md §2：机读结果只认 --out 信封文件，stdout 不参与判定。
# 这套握手的判据面归 C# 自证：`panel-cs.exe --echotest`(回环/--exit=2/未知参数/子进程各走一遍)＋`--coretest`(verb 名册双向对账)，随 build.ps1 -Verify 跑；⚠ 2026-10-26 甲档第七刀 起 `tools\probe-cli-contract.ps1` 整件退役——它测的是本函数**草案期**的副本(自己定义 -In/-Out 三件，与下面的 -Body 签名分家)，对面根本不在这里。
function Get-GdCliExe {
    <# 判定层 exe 是编译产物、永不入仓(.gitignore 的规矩，和 C# 面板现编现用同源)：
       没编译过/被人清掉 → 现场自愈编一次(build.ps1 -Quiet 里已含 System.Web.Extensions 引用链)；
       连 csc 都没有(Quiet 退 3)或编失败 → 才说一句能照着做的人话。 #>
    $exe = Join-Path $global:GD.Root 'tools\panel-cs\panel-cs.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        Write-GdLog 'C# 判定层未编译，现场编一次(之后复用)' -Level WARN
        # 必须掐掉这条调用的成功流：csc 的一句 warning(如 CS0219)会原样流进 build.ps1 的输出，
        # 而这个 & 写在函数体内 —— 返回值会被污染成 Object[]，首元素是"…\tools\panel-cs\x.cs(行,列): warning…"。
        # 之后 `& $exe` 拿到相对路径形态的串会被 PowerShell 按"模块\命令"解析，炸出
        # CouldNotAutoLoadModule"无法加载模块 'tools'"(2026-09-27 导出验收链两树各红一步的根因)。
        & (Join-Path $global:GD.Root 'tools\panel-cs\build.ps1') -Quiet | Out-Null
        if (-not (Test-Path -LiteralPath $exe)) { throw "C# 判定层编译失败：跑 tools\panel-cs\build.ps1 看具体报错(产物 $exe 仍不存在)" }
    }
    return $exe
}

function Invoke-GdCli {
    <#
      唯一转发通道：把 Body 序列化成临时 JSON 走 --in，读回 --out 信封后把 data 交出去。
      退出码语义(0 完成 / 1 内部错 / 2 前置缺或规则拒)：非 0 一律 throw —— 判定层拿不到可信结论时，
      宁可这一轮拒跑，也不带着猜出来的配置继续跑(与 Stop-GdBeforeRun 同哲学)。
      throw 的是信封 error 原话；调用方怎么处置这句，各家不一样(10-19 C-3 首刀〗i 现读；⚠ 10-19 〗q 补一层：下面两枚 catch 包的都是 Resolve-R1999Instance，原话经 lib\r1999-decisions.ps1:98 那一发 Invoke-GdCli 传上来，而直接调 Invoke-GdCli 的那发不在这一对 try/catch 面内)：
      lib\r1999-daily.ps1:84 catch 后打一条 ERROR 日志再原样 throw(〗u 重取：catch 紧跟 try 那行)；lib\r1999-status.ps1:48 catch 把它接在 Reason
      后面打印，但不改 $r.Reason。原注那句"调用方(Show-GdDecision)会把这句并进 Reason"随那枚真身一起作废——
      (⚠ 11-04 刀④：r1999-status 整件出仓，那枚 catch 逐字搬进 lib\r1999-module.ps1 的 StatusPlan 钩子 —— 处置形状没变，"status.ps1:48"这个坐标自此指史)
      "并进 Reason"那种写法全仓只有 decisions.ps1 里那一处，〗i 之后它已不在场(读数见 docs\rust-core-plan.md §11.81)。
    #>
    param(
        [Parameter(Mandatory)][string]$Verb,
        [Parameter(Mandatory)]$Body,
        [string[]]$More = @()
    )
    $stamp = [guid]::NewGuid().ToString('N')
    $inPath = Join-Path ([System.IO.Path]::GetTempPath()) "gd-cli-$stamp.in.json"
    $outPath = Join-Path ([System.IO.Path]::GetTempPath()) "gd-cli-$stamp.out.json"
    try {
        Write-GdJson -Path $inPath -Object $Body -Depth 32
        $exe = Get-GdCliExe
        & $exe $Verb "--in=$inPath" "--out=$outPath" @($More) | Out-Null
        $code = $LASTEXITCODE
        if (-not (Test-Path -LiteralPath $outPath)) { throw "C# 侧($Verb)没吐出结果文件(退出码 $code)" }
        $env0 = Read-GdJson -Path $outPath
        if ($null -eq $env0 -or [string]$env0.verb -ne $Verb) { throw "C# 侧($Verb)信封不对或缺 verb 字段" }
        if ([int]$env0.exit -ne $code) { throw "C# 侧($Verb)退出码($code)与信封 exit($($env0.exit))不一致" }
        if (-not $env0.ok) { throw ([string]$env0.error) }
        return $env0.data
    } finally {
        Remove-Item -LiteralPath $inPath, $outPath -Force -ErrorAction SilentlyContinue
    }
}

function Backup-GdFile {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $name = [System.IO.Path]::GetFileName($Path)
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    if ($script:GdCoreState -ne 'live') { throw '内核缺席：Backup-GdFile 无 PS 原身可退，本轮拒跑(刀⑲ 笔(d))' }
    # 刀⑲ 笔(d)：命名规则与剪枝计划只有内核一份；复制/列举/删除照旧走 cmdlet，
    # 排序故意留在壳侧(内核再排一次会改判 tie-break)，PastKeep 只接"壳已按 LastWriteTime 降序排好"的名字表。
    $dest = [GdCore.Backup]::BuildPath($global:GD.Backup, $name, $stamp)
    Copy-Item -LiteralPath $Path -Destination $dest -Force
    $existing = @(Get-ChildItem -LiteralPath $global:GD.Backup -Filter "$name.*.bak" |
        Sort-Object LastWriteTime -Descending | ForEach-Object { $_.Name })
    foreach ($doomed in @([GdCore.Backup]::PastKeep($existing, 20))) {
        Remove-Item -LiteralPath (Join-Path $global:GD.Backup ([string]$doomed)) -Force
    }
    return $dest
}

# 该催哪几个助手目录 = 这个工程里真的装了哪几路的助手库。
# 合体工程里三键都催(mumuDir 挂 m9a.ps1：11-07 刀⑯ 'mumu.ps1' 整件出仓后 1999 路的库只剩 m9a.ps1，
# 但 mumuDir 仍是那一路的必需路径——催装跟路走不跟件走)；只发一款时，另一个游戏的库不在树里，
# 就不该为一个玩家根本不会去装的路径天天报警 —— 那类警告只会让人以为 setup 没做完。
function Get-GdRequiredPathKeys {
    param([string]$Root)
    if (-not $Root) { $Root = $global:GD.Root }
    $keys = @()
    foreach ($pair in @(@{ Key = 'maaDir'; Lib = 'maa.ps1' }, @{ Key = 'm9aDir'; Lib = 'm9a.ps1' }, @{ Key = 'mumuDir'; Lib = 'm9a.ps1' })) {
        if (Test-Path -LiteralPath (Join-Path $Root "lib\$($pair.Lib)")) { $keys += $pair.Key }
    }
    return $keys
}

function Stop-GdBeforeRun {
    <#
      入口脚本专用的"这一轮没跑"退法：一句话写进日志、一句话打在控制台上，然后按 2 号退出码走人。
      比裸 throw 好的地方：throw 到了 PowerShell 顶层会变成一屏
        "Exception: C:\...\common.ps1:120 / Line | / 120 | throw ..."
      这种内部报错格式 —— 双击 exe 的玩家看了只会以为程序坏了(2026-09-24 的"exe 打不开"就有这一半原因)。
    #>
    param([Parameter(Mandatory)][string]$Message, [int]$Code = 2)
    Write-GdLog "这一轮没跑：$Message" -Level ERROR
    exit $Code
}

function Get-GdSettings {
    param([switch]$Quiet, [string[]]$RequirePaths, [switch]$TellAndExit)
    if ($script:GdCoreState -ne 'live') { throw '内核缺席：Get-GdSettings 无 PS 原身可退，本轮拒跑(刀⑲ 笔(e) 补笔)' }
    $path = Join-Path $global:GD.Root 'config\settings.json'
    if (-not (Test-Path -LiteralPath $path)) {
            $msg = [string]([GdCore.Settings]::MissingConfigMessage())
        if ($TellAndExit) { Stop-GdBeforeRun $msg }
        throw $msg
    }
    $s = Read-GdJson -Path $path
    if (-not $RequirePaths) { $RequirePaths = @(Get-GdRequiredPathKeys) }
    if ($script:GdCoreState -ne 'live') { throw '内核缺席：Get-GdSettings 无 PS 原身可退，本轮拒跑(刀⑲ 笔(d))' }
    # 刀⑲ 笔(d)：paths 缺项点名 + 三句 WARN/硬停话术 + "非 -Quiet 才硬停"的判据只有内核一份。
    # $s.paths.$k 动态成员访问必须留壳侧(C# 拿不到 PS 动态绑定，硬要拿就得引 System.Management.Automation，
    # 破 pure-BCL 约束)：内核收到的是已经判过的平行数组。
    $keys = @($RequirePaths)
    $vals = @()
    $exs = @()
    foreach ($k in $keys) {
        $raw = $s.paths.$k
        $vals += ($( if ($raw) { [string]$raw } else { '' }))
        $exs += [bool](Test-GdPathReady ([string]$raw))
    }
    $r = [GdCore.Settings]::Evaluate([string[]]$keys, [string[]]$vals, [bool[]]$exs, [bool](-not $Quiet))
    foreach ($line in @($r.WarnLines)) {
        if ($line) { Write-GdLog ([string]$line) -Level WARN -Quiet:$Quiet }
    }
    $msg = [string]$r.StopMessage
    if ($msg) {
        if ($TellAndExit) { Stop-GdBeforeRun $msg }
        throw $msg
    }
    return $s
}

function Test-GdPathReady {
    <# 填了且真的存在才算就绪。空串要自己挡掉：Test-Path -LiteralPath '' 在 5.1 和 7 上都是抛异常，不是返 $false #>
    param([AllowEmptyString()][string]$Path)
    if (-not $Path) { return $false }
    return [bool](Test-Path -LiteralPath $Path)
}

function Join-GdPath {
    <#
      base 没填(paths.* 是空串)时返回空串，而不是抛"无法将自变量绑定到参数 'Base'"：
      环境检查/体检要能把这一项显示成「未填写」，让访客看到的是一行"缺什么"，不是一段内部报错。
      真正会拿这个结果去干活的入口由 Get-GdSettings(非 -Quiet)先硬停，所以放宽这里不会把错误咽掉。
    #>
    param([AllowEmptyString()][string]$Base, [Parameter(Mandatory)][string[]]$Parts)
    if ($script:GdCoreState -ne 'live') { throw '内核缺席：Join-GdPath 无 PS 原身可退，本轮拒跑(刀⑲ 笔(d))' }
    # 刀⑲ 笔(d)：空 base→空串 + 跳过空段 + Join-Path 拼接只有内核一份(param 块绑定校验照旧留壳侧)。
    return [GdCore.Paths]::Join($Base, @($Parts))
}

# --- B0(2026-09-29)：P/Invoke 现场件也从 here-string 搬进真 .cs，宿主各自现编 ---…（细节见该帧）
$script:GdInteropState = 'off'    # off|live|fallback
$script:GdInteropMs = -1          # 本进程现编 interop 花了几毫秒(-1 = 没编过)
function Add-GdInterop {
    <# 幂等装载 tools\gdinterop\*.cs：一次 Add-Type -Path 全量现编。任何异常吞掉只落状态——本函数永不向上抛(回退语义)。 #>
    param([string]$SourceDir)
    if ($script:GdInteropState -ne 'off') { return $script:GdInteropState }
    if (-not $SourceDir) { $SourceDir = Join-Path $PSScriptRoot '..\tools\gdinterop' }
    $ErrorActionPreference = 'Stop'    # 不加这句：Add-Type 编译失败在非交互宿主里可能只写 ErrorRecord，try/catch 抓不住而类型却没落地
    try {
        # 按目录现扫、不一份份列(publish-split 里 gdcore 那条教训)：漏一份 .cs = 克隆者那边现编失败、本地却一直绿
        $srcs = @(Get-ChildItem -LiteralPath $SourceDir -Filter '*.cs' -File | ForEach-Object { $_.FullName })
        if (-not $srcs.Count) { throw "interop 源码目录没有 .cs：$SourceDir" }
        $sw = New-Object System.Diagnostics.Stopwatch
        $sw.Start()
        try { Add-Type -Path $srcs } finally { $sw.Stop(); $script:GdInteropMs = [int]$sw.Elapsed.TotalMilliseconds }
        $script:GdInteropState = 'live'
    } catch {
        $script:GdInteropState = 'fallback'
    }
    return $script:GdInteropState
}

# --- 防止系统在跑日常时睡眠/被屏保盖屏 ---
$global:GdSleepHandle = $false
# $global:GdDeskTypeDef 已删(刀⑰ 笔(b))：Get-GdInputDesktopName 的"只做一次"闸随真身一起出仓，[GdDesk]:: 全树只在那函数体内。
function Add-GdSleepTypeDef {
    <# B0 起这里不再内联 C#：签名在 tools\gdinterop\GdPower.cs，现编由 Add-GdInterop 负责。
       编不过要**抛**(与旧 `Add-Type ... -ErrorAction Stop` 同形)——丙档落码刀①删掉 Enter/Exit-GdNoSleep 那两枚真身后，接这一抛的是
       转接层 Start-GdRoundHostNoSleep 的 catch(吞成"没设位、本轮照跑")；Set-GdScreensaverActive 自己不接、原样上抛(`Get-GdScreensaverActive` 那枚已于丙档落码刀②删除，去向见 docs\rust-core-plan.md §11.118)。 #>
    if ($global:GdSleepHandle) { return }
    if ((Add-GdInterop) -ne 'live') { throw "interop 现编失败(GdPower 不可用)，状态=$script:GdInteropState" }
    $global:GdSleepHandle = $true
}
# Get-GdScreensaverConfig 已删(转接刀·屏保稳态族，docs\rust-core-plan.md §11.215)：只读注册表那半段的真身在核内…（细节见该帧）

# Get-GdScreensaverStateFile 已删(丙档落码刀②，docs\rust-core-plan.md §11.118)：唯一调用方随 Enter-GdNoSleep 一起退役，screensaver.restore 那本 state 自此没有 PS 侧的读写者。
function Set-GdScreensaverActive {
    param([Parameter(Mandatory)][bool]$Active)
    Add-GdSleepTypeDef
    $pv = if ($Active) { [uint32]1 } else { [uint32]0 }
    return [bool][GdPower]::SPISetActive([GdPower]::SPI_SETSCREENSAVEACTIVE, $pv, [IntPtr]::Zero,
                                         [GdPower]::SPIF_SENDWININICHANGE)
}
# Enter-GdNoSleep／Exit-GdNoSleep 两枚真身已删(丙档落码刀①，docs\rust-core-plan.md §11.116)：生产面零调用方。

# --- 进程工具 ---
function Test-GdProcessRunning {
    param([Parameter(Mandatory)][string]$Name)
    return [bool](Get-Process -Name $Name -ErrorAction SilentlyContinue)
}
# Stop-GdProcessTree 真身已删(11-06 转接刀·运行面刀⑭ 笔(a))：PS 面最后一处消费点随刀⑫ 笔(a) 摘 Stop-M9A 壳归零……（细节见该帧）

function Invoke-GdNative {
    <# 运行外部命令，返回 @{Code;Out;Err} #>
    param(
        [Parameter(Mandatory)][string]$File,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 60
    )
    if (-not (Test-Path -LiteralPath $File)) { throw "命令不存在：$File" }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $File
    $psi.Arguments = ($Arguments -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.WorkingDirectory = Split-Path -Parent $File
    $p = [System.Diagnostics.Process]::Start($psi)
    $script:GdChildProcs++      # 接线批的"一轮进程数"读数口径：两个包装函数各数一次，见 GdCore 块头注
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
        try { $p.Kill() } catch { }
        return @{ Code = -1; Out = ''; Err = "timeout after ${TimeoutSeconds}s" }
    }
    return @{ Code = $p.ExitCode; Out = $outTask.Result; Err = $errTask.Result }
}

function Invoke-GdChild {
    <# 和 Invoke-GdNative 同一件事，多两点：**能给子进程单独设环境变量** + 带空格的参数自己加引号。
        为什么不能直接用上面那个：① 它没有环境变量的口子，而视觉兜底的 API key 只允许走环境变量 ——
        写进命令行等于挂在进程列表和每一行日志上(Invoke-GdNative 的 -join ' ' 还会被工具页原样印出来)；
        ② 它是 `$Arguments -join ' '`，路径里有空格(用户名带空格太常见了)就会散成两个参数。
        返回值同 Invoke-GdNative：@{Code;Out;Err}，超时 Code=-1 并把进程杀掉。 #>
    param(
        [Parameter(Mandatory)][string]$File,
        [string[]]$Arguments = @(),
        [hashtable]$Env = @{},
        [int]$TimeoutSeconds = 60
    )
    if (-not (Test-Path -LiteralPath $File)) { throw "命令不存在：$File" }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $File
    $psi.Arguments = (@($Arguments | ForEach-Object {
        $a = "$_"
        if ($a -match '[\s"]') { '"' + ($a -replace '"', '\"') + '"' } else { $a }
    }) -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.WorkingDirectory = $global:GD.Root
    foreach ($k in @($Env.Keys)) { $psi.EnvironmentVariables[[string]$k] = [string]$Env[[string]$k] }
    $p = [System.Diagnostics.Process]::Start($psi)
    $script:GdChildProcs++      # 同上：视觉兜底走的是这一个包装
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
        try { $p.Kill() } catch { }
        return @{ Code = -1; Out = ''; Err = "timeout after ${TimeoutSeconds}s" }
    }
    return @{ Code = $p.ExitCode; Out = $outTask.Result; Err = $errTask.Result }
}

# --- Python：视觉兜底的模型调用层(tools\vision\ask.py)用它跑，见那个文件的头注 ---
function Get-GdPythonBundledDir { Join-Path $global:GD.State 'python' }
function Get-GdPythonBundledExe { Join-Path (Get-GdPythonBundledDir) 'python.exe' }

function Test-GdPythonUsable {
    <# 光"文件在"不够：Windows 在没装 Python 的机器上会放一个 `python.exe` 商店占位程序，
        跑起来只会打印一句 "Python was not found" 并回一个非 0，第一次接上时就骗过了路径检查
        (报错还报在 ask.py 那一层，人看到的是"模型调用失败"，根本不是真因)。
        所以这里真的起一次进程，并要求它把 ask.py 要用的那几个标准库都 import 成功、且是 Python 3：
        embeddable 包是精简版，万一哪天缺 ssl 就得在兜底闸门这里说清楚，而不是半夜的日志里。
        只起一次(调用方缓存)，不联网、不动任何窗口。 #>
    param([Parameter(Mandatory)][string]$Exe, [int]$TimeoutSeconds = 20)
    if (-not (Test-Path -LiteralPath $Exe)) { return $false }
    try {
        $r = Invoke-GdChild -File $Exe -TimeoutSeconds $TimeoutSeconds -Arguments @('-c', 'import sys,json,base64,ssl,urllib.request;print(sys.version_info[0])')
    } catch { return $false }
    return ($r.Code -eq 0 -and ("$($r.Out)").Trim() -eq '3')
}

function Get-GdPythonExe {
    <# 找一个能跑 tools\vision\ask.py 的 python.exe，找不到返回空串(调用方负责说人话)。
        顺序和理由：
          1) settings.runtime.pythonExe —— 人手工指定的最大，只认"绝对路径 + 文件真在 + 用得住"
          2) 环境变量 GD_PYTHON —— 不落盘的临时指定，换机器/试另一份解释器时用，也不用改配置
          3) 本仓库自己下的 embeddable 包(state\python\python.exe) —— setup.ps1 -WithPython 放进去的。
             排在 PATH 前面：我们自己下的那份行为可预期(版本、stdlib 都定死)，而 PATH 上那份可能是
             别人装的旧版、或上面说的那个商店占位程序
          4) PATH 上的 python.exe / py.exe —— 最后兜底；WindowsApps 那个占位程序直接跳过，
             连试都不试(它一定会失败，试一次白等一次进程)
        结果缓存到 $global:GdPythonExe + $global:GdPythonProbed：一轮里状态页/兜底/诊断入口会问好几次，
        而"没找到"也要缓存 —— 不然每次问都白起一遍探测进程。-Refresh 强制重查(刚装完 python 时用)。 #>
    param($S = $null, [switch]$Refresh)
    if ($Refresh) { $global:GdPythonProbed = $false }
    elseif ($global:GdPythonProbed) { return [string]$global:GdPythonExe }
    $found = ''
    $candidates = @()
    $manual = ''
    if ($S -and $S.runtime -and $S.runtime.pythonExe) { $manual = [string]$S.runtime.pythonExe }
    if (-not $manual -and $env:GD_PYTHON) { $manual = [string]$env:GD_PYTHON }
    if ($manual) { $candidates += $manual }
    $candidates += (Get-GdPythonBundledExe)
    foreach ($n in @('python', 'py')) {
        foreach ($c in @(Get-Command $n -ErrorAction SilentlyContinue)) {
            $src = [string]$c.Source
            if (-not $src) { continue }
            if ($src -like '*\WindowsApps\*') { continue }   # 商店占位程序：见 Test-GdPythonUsable
            $candidates += $src
        }
    }
    $seen = @{}
    foreach ($c in $candidates) {
        $key = $c.Trim().Trim('"').ToLowerInvariant()
        if (-not $key -or $seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        if (Test-GdPythonUsable -Exe $key) { $found = $key; break }
    }
    $global:GdPythonExe = $found
    $global:GdPythonProbed = $true
    return $found
}

# embeddable 包：官方"绿色版"解释器，解压即用、不写注册表、不动 PATH，正适合塞在 state\ 下面。
# 版本必须钉死：换版本要换 URL，别让它悄悄跟着 python.org 的最新走(装完才发现 stdlib 行为不同)。
$global:GdPythonEmbedVersion = '3.12.10'
$global:GdPythonEmbedUrlFormat = 'https://www.python.org/ftp/python/{0}/python-{0}-embed-{1}.zip'

function Install-GdPythonEmbeddable {
    <# setup.ps1 -WithPython 的实现：下一个 embeddable 包到 state\python\，装完立刻用同一把尺子验
        (Test-GdPythonUsable)，验不过就保留旧的那份 —— 半下载的包比没有包更坏，会把兜底变成"每次都白等"。
        先解到临时目录再整目录换名，就是为了"要么换成好的，要么什么都不动"。
        URL 可以用环境变量 GD_PYTHON_EMBED_URL 整体覆盖(公司网络/镜像)，或 GD_PYTHON_EMBED_VERSION 换版本；
        它不是 settings 键：这纯粹是装东西的事，日常跑起来一个字段都不读。 #>
    param([switch]$Force)
    $dir = Get-GdPythonBundledDir
    if ((Test-Path -LiteralPath $dir) -and (Get-GdPythonExe) -and -not $Force) {
        return [pscustomobject]@{ Ok = $true; Exe = (Get-GdPythonExe); Detail = '已经有可用的 Python，不重下(-Force 才重装)' }
    }
    $ver = if ($env:GD_PYTHON_EMBED_VERSION) { [string]$env:GD_PYTHON_EMBED_VERSION } else { [string]$global:GdPythonEmbedVersion }
    $arch = if ("$env:PROCESSOR_ARCHITECTURE".ToLowerInvariant() -eq 'arm64') { 'arm64' } else { 'amd64' }
    $url = if ($env:GD_PYTHON_EMBED_URL) { [string]$env:GD_PYTHON_EMBED_URL } else { ($global:GdPythonEmbedUrlFormat -f $ver, $arch) }
    Enable-GdTls12
    $prevProgress = $ProgressPreference
    $zip = Join-Path ([System.IO.Path]::GetTempPath()) "gd-python-$ver-$arch-embed.zip"
    $tmp = "$dir.tmp"
    try {
        $ProgressPreference = 'SilentlyContinue'      # 5.1 的进度条能把下载拖慢一个数量级
        Write-GdLog "下载 Python $ver ($arch) embeddable 包 → $dir(约 10MB，只放进 state\，不进仓库)" -Level STEP
        Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing -TimeoutSec 300
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force }
        [void](New-Item -ItemType Directory -Path $tmp -Force)
        Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
        if (-not (Test-GdPythonUsable -Exe (Join-Path $tmp 'python.exe') -TimeoutSeconds 30)) {
            return [pscustomobject]@{ Ok = $false; Exe = ''; Detail = "包解压了但里面的 python 用不了：$url(换 GD_PYTHON_EMBED_VERSION 或 GD_PYTHON_EMBED_URL 再试)" }
        }
        if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
        Move-Item -LiteralPath $tmp -Destination $dir -Force
        $exe = Get-GdPythonExe -Refresh
        if (-not $exe) { return [pscustomobject]@{ Ok = $false; Exe = ''; Detail = '装完了但当场没找到 state\python\python.exe(被杀软删了？)' } }
        return [pscustomobject]@{ Ok = $true; Exe = $exe; Detail = "已装到 $dir" }
    } catch {
        return [pscustomobject]@{ Ok = $false; Exe = ''; Detail = "下载/解压失败：$($_.Exception.Message)(地址 $url；也可自己装一份 Python 3 再填 runtime.pythonExe)" }
    } finally {
        $ProgressPreference = $prevProgress
        if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# Invoke-GdTakeover 已删(11-05 转接刀·运行面刀⑨)：原段头那句"日常编排通用两件：接管关闭 / 单步包异常"里两件…（细节见该帧）

# Invoke-GdStep 已删(10-25 甲档摘件，docs\rust-core-plan.md §11.132)：生产面自 10-24 刀③ 起整词 0 命中；…（细节见该帧）
function Enable-GdTls12 {
    <#
      5.1 的 ServicePointManager 默认只到 TLS 1.0/1.1，多数 CDN 直接握手失败；PowerShell 7 默认就带 TLS 1.2+，
      所以这里只做"或上去"、不覆盖系统已有值，且整体包 try —— 拿不到就按引擎默认走，不该因此弄挂日常。
      调用方在 Invoke-RestMethod / Invoke-WebRequest 之前点一下即可(幂等，重复调用无副作用)。
    #>
    try {
        $cur = [int][System.Net.ServicePointManager]::SecurityProtocol
        if (-not ($cur -band 3072)) {   # 3072 = Tls12；Tls13 在 .NET Framework 上会被 setter 拒掉，故只补 1.2
            [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]($cur -bor 3072)
        }
    } catch { }
}

# --- 权限(端游 + MAA 要管理员) ---
function Test-GdAdmin {
    <# 当前进程是否已提权(管理员) #>
    try {
        $id = [Security.Principal.WindowsIdentity]::GetCurrent()
        return (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch { return $false }
}

function Get-GdRelaunchArgList {
    <# 转发壳(批次 10)：skip 判据(-in 不分大小写)、DryRun 永远不带、flag 真值才加、串值 " → \" 转义
       全在 C# Processes.cs，verb build-relaunch-args。"哪些参数被绑了、是什么类型"是 PS 运行时的事，
       留在这里逐项分类成 {K,T,V} 喂进去(K=名 / T=flag|null|str / V=值)，$Bound.Keys 枚举序原样保持；
       数组值走 [string] 空格拼串就是 PS 的 [string]@() 语义。返回仍是字符串数组(test-ark [t8]/[t9] 钉着)。 #>
    param(
        [Parameter(Mandatory)][string]$Script,
        [hashtable]$Bound = @{},
        [string[]]$Force = @(),
        [string[]]$Omit = @()
    )
    $entries = @(foreach ($k in $Bound.Keys) {
        $v = $Bound[$k]
        if ($null -eq $v) { [pscustomobject]@{ K = $k; T = 'null'; V = $null } }
        elseif ($v -is [switch] -or $v -is [bool]) { [pscustomobject]@{ K = $k; T = 'flag'; V = [bool]$v } }
        else { [pscustomobject]@{ K = $k; T = 'str'; V = [string]$v } }
    })
    $d = Invoke-GdCli -Verb 'build-relaunch-args' -Body ([pscustomobject]@{
        script  = $Script
        omit    = @($Omit)
        force   = @($Force)
        entries = $entries
    })
    return , @([string[]]$d.Args)
}

function Get-GdLocalPwsh {
    <# 找"能直接 RunAs"的 pwsh：本机安装版(Program Files)可以用；Store/WindowsApps 版不行 #>
    $cands = @()
    if ($PSHOME) { $cands += (Join-Path $PSHOME 'pwsh.exe') }
    $cands += 'C:\Program Files\PowerShell\7\pwsh.exe'
    foreach ($c in $cands) {
        if ($c -and (Test-Path -LiteralPath $c) -and $c -notmatch '\\WindowsApps\\') { return $c }
    }
    return $null
}

function Get-GdElevateBatchLine {
    <# 纯函数：拼出临时批处理里的那一行(便于单测，注意逗号优先级别再把参数拆散) #>
    param([Parameter(Mandatory)][string]$Pwsh, [Parameter(Mandatory)][string[]]$ArgList)
    $exe = if ($Pwsh -match '\s') { ('"{0}"' -f $Pwsh) } else { $Pwsh }
    return ('{0} {1}' -f $exe, ($ArgList -join ' '))
}

function Start-GdElevatedSelf {
    <#
      以管理员身份重跑当前脚本，等待子进程结束并返回其退出码；-1 表示 UAC 被取消/失败。
      为什么要 cmd 载体：pwsh 若是 Microsoft Store(WindowsApps) 版，ShellExecute 的 runas
       verb 提不了它，报错还会写成"操作已被用户取消"，看起来像用户点了"否"。
    #>
    param(
        [Parameter(Mandatory)][string]$Script,
        [hashtable]$Bound = @(),
        [string[]]$Force = @()
    )
    $argList = Get-GdRelaunchArgList -Script $Script -Bound $Bound -Force $Force
    $workdir = Split-Path -Parent $Script

    $local = Get-GdLocalPwsh
    if ($local) {
        try {
            $p = Start-Process -FilePath $local -ArgumentList ($argList -join ' ') -Verb RunAs -WorkingDirectory $workdir -PassThru
            $p.WaitForExit()
            return [int]$p.ExitCode
        } catch {
            Write-GdLog "本机 pwsh 提权失败($($_.Exception.Message)) → 改用 cmd 载体" -Level WARN
        }
    }

    $pwsh = (Get-Process -Id $PID -ErrorAction SilentlyContinue).Path
    if (-not $pwsh) { $pwsh = (Get-Command pwsh.exe -ErrorAction SilentlyContinue).Source }
    if (-not $pwsh) { throw '找不到 pwsh.exe，无法提权重跑' }

    $line = Get-GdElevateBatchLine -Pwsh $pwsh -ArgList $argList
    $tmp  = Join-Path $env:TEMP ('gd-elevate-{0}.cmd' -f (Get-Date -Format 'yyyyMMddHHmmss'))
    Set-Content -LiteralPath $tmp -Value "@echo off`r`nchcp 65001 >nul`r`n$line`r`nexit /b %ERRORLEVEL%" -Encoding ascii
    try {
        $cmdExe = Join-Path $env:SystemRoot 'System32\cmd.exe'
        $p = Start-Process -FilePath $cmdExe -ArgumentList @('/d', '/c', ('"{0}"' -f $tmp)) -Verb RunAs -WindowStyle Hidden -PassThru
        $p.WaitForExit()
        $code = [int]$p.ExitCode
    } catch {
        Write-GdLog "提权被取消或失败：$($_.Exception.Message)" -Level ERROR
        $code = -1
    }
    Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    return $code
}

function Show-GdVersionFile {
    <# 体检用：从助手的 json 里挑一个能当版本号的字段，读不到只打一行黄字，绝不抛 #>
    param([Parameter(Mandatory)][string]$Label, [AllowEmptyString()][string]$Dir, [Parameter(Mandatory)][string]$File)
    if (-not $Dir) { Write-Host "  $Label 版本：助手目录还没填(跑 setup.ps1)" -ForegroundColor Yellow; return }
    $p = Join-Path $Dir $File
    if (-not (Test-Path -LiteralPath $p)) { return }
    try {
        $j = Read-GdJson -Path $p
        $v = if ($j.Update.Name) { $j.Update.Name } elseif ($j.version) { $j.version } else { '?' }
        Write-Host "  $Label 版本：$v($([System.IO.Path]::GetFileName($p))，$((Get-Item -LiteralPath $p).LastWriteTime.ToString('yyyy-MM-dd')))"
    } catch { Write-Host "  $Label 版本读取失败：$($_.Exception.Message)" -ForegroundColor Yellow }
}

# --- 活体标记(跨进程互斥用，面板只读不写) ---
# 为什么需要它：面板只看得见自己起的子进程。定时任务、命令行、另一个面板窗口起的轮次它一概不知道，
# 于是"再来一轮"会把两轮叠到同一个助手/端游上(实机撞过：19:11 从命令行点火，面板全程显示空闲)。
# 判据为什么是 PID 而不是"日志最近有没有新行"：实机量过，MAA 跑基建(Infrast)那一段能连着
# 10 分 41 秒一行都不写(2026-09-25 18:50:44 Recruit → 19:01:25 Infrast)，按日志新鲜度判会把
# "正在跑基建"看成空闲。只有进程活没活不会骗人。
function Start-GdRunLock {
    param(
        [Parameter(Mandatory)][string]$Games,
        [string]$Tag = ''
    )
    $file = Join-Path $global:GD.State 'running.json'
    # 已有别的轮在跑：这里只把话说出来，不拦。拦的活交给面板和计划任务那两侧 ——
    # 在判定层硬拦会让 06:00/18:00 撞上时整轮直接失败，而"失败"对无人值守的人来说
    # 比"多跑一遍"更难发现。要改成硬拦，得先有"两轮叠跑真把助手搞坏了"的证据。
    $prev = Get-GdRunLock
    if ($prev -and $prev.Alive) {
        Write-GdLog "已有一轮在跑(PID $($prev.Pid)，$($prev.Started) 起，games=$($prev.Games)) —— 本轮照跑，两轮会抢同一个助手/端游" -Level WARN
    }
    $obj = [ordered]@{
        pid     = $PID
        started = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
        games   = $Games
        tag     = $Tag
        log     = [string]$global:GD.LogFile
    }
    try {
        $obj | ConvertTo-Json -Compress | Set-Content -LiteralPath $file -Encoding utf8
    } catch {
        Write-GdLog "活体标记没写进去：$($_.Exception.Message)(面板这一轮就认不出有人在跑)" -Level WARN
    }
}

# 读标记并判活。返回 $null = 压根没有标记；.Alive=$false = 上一轮崩了没清掉的残留
# (残留按"没在跑"处理是刻意的：否则一次硬杀会把面板永久挡在门外)。
function Get-GdRunLock {
    $file = Join-Path $global:GD.State 'running.json'
    if (-not (Test-Path -LiteralPath $file)) { return $null }
    try {
        $o = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
        if ($null -eq $o.pid) { return $null }
        $alive = $true
        try {
            # 只问"有没有这个 PID"。Get-Process 在 5.1/7 上找不到进程时抛的类型并不一致
            # (ProcessException / InvalidOperationException 都见过)，所以不分类型，一律按"没了"处理。
            $p = Get-Process -Id ([int]$o.pid) -ErrorAction Stop
            $p.Dispose()
        } catch { $alive = $false }
        return [pscustomobject]@{
            Alive   = $alive
            Pid     = [int]$o.pid
            Started = [string]$o.started
            Games   = [string]$o.games
            Tag     = [string]$o.tag
            Log     = [string]$o.log
        }
    } catch { return $null }
}

function Stop-GdRunLock {
    $file = Join-Path $global:GD.State 'running.json'
    if (-not (Test-Path -LiteralPath $file)) { return }
    if ($script:GdCoreState -ne 'live') { throw '内核缺席：Stop-GdRunLock 无 PS 原身可退，本轮拒跑(刀⑲ 笔(d))' }
    try {
        # 只删自己登记的那一份：万一别的轮已经覆盖了它，删就等于把别人的撞车保护拆了。
        # K2 实测：-match 不分大小写，所以 {"PID":123} 也算自己的 —— 内核用 RegexOptions.IgnoreCase 复刻这一格。
        $txt = [string](Get-Content -LiteralPath $file -Raw)
        $mine = [bool]([GdCore.RunLock]::OwnsText($txt, $PID))
        if ($mine) { Remove-Item -LiteralPath $file -Force }
    } catch { }
}

# Wait-GdCondition 已删(刀⑰ 笔(b)，docs\rust-core-plan.md §11.272)：真身是 tools\panel-cs\Chain.cs::WaitUntil 的逐字复刻……（细节见该帧）

# Get-GdWindowByTitle 已删(11-05 转接刀·运行面刀⑨)：按主窗口标题找进程那把闸的对面在…（细节见该帧）

# --- 计划任务(免 UAC 的管理员通道) ---
function Get-GdTaskState {
    param([Parameter(Mandatory)][string]$Name)
    $t = Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue
    if (-not $t) { return $null }
    $info = $t | Get-ScheduledTaskInfo
    return [pscustomobject]@{
        State = [string]$t.State
        Last  = [string]$info.LastRunTime
        Result = $(try { [int]$info.LastTaskResult } catch { $null })
        Next  = [string]$info.NextRunTime
    }
}

# --- 任意"键值映射"读写(JSON 对象 / PSCustomObject / hashtable 都兼容) ---
function Get-GdMapValue {
    param($Map, [Parameter(Mandatory)][string]$Key)
    if ($null -eq $Map) { return $null }
    if ($Map -is [System.Collections.IDictionary]) {
        if ($Map.Contains($Key)) { return $Map[$Key] }
        return $null
    }
    $prop = $Map.PSObject.Properties[$Key]
    if ($prop) { return $prop.Value }
    return $null
}

function Set-GdMapValue {
    <# 返回被修改后的映射(hashtable 原地改，PSCustomObject 用 Add-Member -Force) #>
    param($Map, [Parameter(Mandatory)][string]$Key, $Value)
    if ($null -eq $Map) { throw 'Set-GdMapValue：映射为空' }
    if ($Map -is [System.Collections.IDictionary]) { $Map[$Key] = $Value; return $Map }
    $Map | Add-Member -NotePropertyName $Key -NotePropertyValue $Value -Force
    return $Map
}

function Convert-GdMapToHashtable {
    param($Map)
    $h = @{}
    if ($null -eq $Map) { return $h }
    if ($Map -is [System.Collections.IDictionary]) {
        foreach ($k in $Map.Keys) { $h[[string]$k] = $Map[$k] }
        return $h
    }
    foreach ($p in $Map.PSObject.Properties) { $h[$p.Name] = $p.Value }
    return $h
}

# ---------- 本机专属值：两道扫描共用的判据(2026-09-25 从 tools\publish-split.ps1 挪上来) ----------…（细节见该帧）

function Get-GdLocalOnlyValues {
    <# 只有这台机器才成立的东西：settings.json 里的绝对路径、MuMu 虚拟机名、M9A 给每个实例生成的短 hash。
       这些东西一旦跟着源码出去，就等于把"我装在哪、我几点跑、我的实例叫什么"发给陌生仓库。
       值全部在运行时从本机配置里取，本文件里不出现任何一条字面量 —— 所以这道检查不会自己命中自己。 #>
    param([string]$LocalSettings)
    $vals = @()
    if (Test-Path -LiteralPath $LocalSettings) {
        $cfg = Read-GdJson -Path $LocalSettings
        foreach ($k in @('maaDir', 'm9aDir', 'mumuDir', 'arknightsExe')) {
            $v = [string]$cfg.paths.$k
            if ($v) { $vals += [pscustomobject]@{ Label = "paths.$k"; Value = $v } }
        }
        $hint = [string]$cfg.mumu.vmNameHint
        if ($hint) { $vals += [pscustomobject]@{ Label = 'mumu.vmNameHint'; Value = $hint } }
        # 密钥类同样算"本机专属"：视觉兜底的模型 key、通知 webhook(里面常带 token)。
        # 它们本该只活在 settings.json 里，但一旦有人在代码/README 里粘了一份，这道扫描要能抓到。
        $vkey = [string]$cfg.runtime.visionApiKey
        if ($vkey) { $vals += [pscustomobject]@{ Label = 'runtime.visionApiKey'; Value = $vkey } }
        $hook = [string]$cfg.notify.webhook
        if ($hook) { $vals += [pscustomobject]@{ Label = 'notify.webhook'; Value = $hook } }
        # 单游戏仓(方舟那一路)和新机器上 m9a 那几项可能压根没配 → 别把 $null 塞进 Join-GdPath 的
        # -Parts(它是 Mandatory，绑 null 会直接抛"无法将自变量绑定到参数 'Parts'")。
        $m9aDir = [string]$cfg.paths.m9aDir
        $instRel = [string]$cfg.paths.m9aInstancesDir
        $instDir = if ($m9aDir -and $instRel) { Join-GdPath $m9aDir @($instRel) } else { '' }
        if ($instDir -and (Test-Path -LiteralPath $instDir)) {
            $i = 0
            foreach ($f in @(Get-ChildItem -LiteralPath $instDir -Filter '*.json')) {
                $i++
                $vals += [pscustomobject]@{ Label = "m9a-instance-id#$i"; Value = $f.BaseName }
            }
        }
    }
    $vals += [pscustomobject]@{ Label = 'env.USERPROFILE'; Value = $env:USERPROFILE }
    return @($vals | Where-Object { $_.Value -and $_.Value.Trim().Length -ge 6 } |
        Sort-Object -Property Value -Unique | ForEach-Object { [pscustomobject]@{ Label = $_.Label; Value = $_.Value.Trim() } })
}

function Test-GdExportLeaks {
    <# 对一个目录做一遍"本机数据"扫描；返回 "相对路径 <- 是哪一项" 的列表，空 = 干净。
       两处调用：publish-split 扫导出目录(发出去之前那道闸)，test-modules [m8] 扫工作副本的源码。
       跳过运行时产物(logs/state/backups)、.git、构建产物目录(target，见 tools\rustcore\.gitignore「永远不进仓」)
       和 settings.json 本身 —— 那些本来就该带着本机值。
       ⚠ 10-27 第十八刀补 target：rust 构建的 .d/.pdb 里带编译期家目录绝对路径，扫工作副本时会被当成泄漏，
       而 target 是仓内明设不进仓的构建产物(只读自检不许要求机器现场状态)。
       ⚠ 11-21 三仓拆分补三档：target-alt／node_modules／dist 与 target 同类(依赖与前端产物，导出表整段不含)；
       ui-hitech-reference 是外部站点快照的归档(同样不进仓)，里面的版本号/路径是别人的东西，不算本仓的第二处写死。 #>
    param([string]$Dir, [object[]]$Values)
    if (-not $Values -or -not @($Values).Count) { return @() }
    $hits = @()
    foreach ($f in @(Get-ChildItem -LiteralPath $Dir -Recurse -File |
            Where-Object { $_.FullName -notmatch '\\(logs|state|backups|\.git|target|target-alt|node_modules|dist|ui-hitech-reference)\\' -and $_.FullName -notmatch '\\config\\settings\.json$' -and $_.Extension -notin '.png', '.ico', '.exe', '.dll', '.zip' })) {
        $text = Get-Content -LiteralPath $f.FullName -Raw -Encoding utf8
        if (-not $text) { continue }
        $rel = $f.FullName.Substring($Dir.Length + 1)
        foreach ($v in $Values) {
            if ($text.IndexOf([string]$v.Value, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $hits += "$rel <- $($v.Label)"
            }
        }
    }
    return @($hits | Sort-Object -Unique)
}

# ---------- 新宿主(Rust 核)出货链：壳侧期望契约号 + 二进制逐字节泄漏门 ----------…（细节见该帧）
function Get-GdRustContract { 'RustCore-R11.0' }   # 与 tools\rustcore\src\main.rs 的 const CONTRACT 同改；同一轮内不许单侧动

function Get-GdBinaryLeakSet {
    <# 二进制门要扫的东西 = 文本门那一份(Get-GdLocalOnlyValues，唯一口径) + 三条**从环境变量现推**的短串/形状。
       短串必须按词边界数：实测过 `sk-` 这类短前缀裸着数会抓到清单里 `task-lifecycle` 中间那截，
       第一次真跑就红在两句人话上(判据 §5.3-5 那条坑)。用户名/用户目录根一律从 $env 现推 ——
       往源码里写它们的字面量本身就算泄漏(注释也算，这条纪律在案)。 #>
    param([string]$LocalSettings)
    $set = @()
    foreach ($v in @(Get-GdLocalOnlyValues -LocalSettings $LocalSettings)) {
        $set += [pscustomobject]@{ Label = $v.Label; Value = $v.Value; Kind = 'text'; Word = ($v.Value.Length -lt 10) }
    }
    $user = [string]$env:USERNAME
    if ($user) { $set += [pscustomobject]@{ Label = 'env.USERNAME'; Value = $user; Kind = 'text'; Word = $true } }
    $usersRoot = ''
    if ($env:USERPROFILE) { $usersRoot = [string](Split-Path -Parent ([string]$env:USERPROFILE)) }
    if ($usersRoot) { $set += [pscustomobject]@{ Label = 'env.USERSROOT'; Value = $usersRoot; Kind = 'text'; Word = $false } }
    # 10-26 推仓55 在案纠正：本形不加词边界时，会把 rodata 里**相邻字符串字面量的跨界拼接**误判成密钥——…（细节见该帧）
    $set += [pscustomobject]@{ Label = 'key-shape'; Value = '(?<![A-Za-z0-9])sk-[A-Za-z0-9]{12}'; Kind = 'regex'; Word = $false }
    return @($set)
}

function Test-GdBinaryLeaks {
    <# 对一个目录里的**二进制**做逐字节本机专属值扫描；返回 "相对路径 <- 是哪一项"，空 = 干净。
       覆盖的正是文本门按扩展名跳开那一批(外加 Rust 产物常见的 .pdb/.lib/.rlib/.o)。
       调用点：tools\publish-split.ps1 扫导出目录、tools\build-exe.ps1 扫 zip 之前的 stage 树。 #>
    param([string]$Dir, [object[]]$Set)
    if (-not $Set -or -not @($Set).Count) { return @() }
    $hits = @()
    foreach ($f in @(Get-ChildItem -LiteralPath $Dir -Recurse -File |
            Where-Object { $_.FullName -notmatch '\\(logs|state|backups|\.git)\\' -and $_.FullName -notmatch '\\config\\settings\.json$' -and
                $_.Extension -in '.png', '.ico', '.exe', '.dll', '.zip', '.pdb', '.lib', '.rlib', '.o' })) {
        $bytes = @([System.IO.File]::ReadAllBytes($f.FullName))
        if (-not $bytes.Count) { continue }
        $views = @()
        $views += [System.Text.Encoding]::GetEncoding(28591).GetString($bytes)   # Latin1：一字节一字符，UTF-8/ASCII 原形
        $views += [System.Text.Encoding]::Unicode.GetString($bytes)              # UTF-16LE：csc 产物里字符串的形状
        $rel = $f.FullName.Substring($Dir.Length + 1)
        foreach ($n in $Set) {
            $needle = [string]$n.Value
            if (-not $needle) { continue }
            foreach ($view in $views) {
                $hit = $false
                if ($n.Kind -eq 'regex') { $hit = [regex]::IsMatch($view, $needle) }
                elseif ($n.Word) { $hit = [regex]::IsMatch($view, ('(?<![A-Za-z0-9])' + [regex]::Escape($needle) + '(?![A-Za-z0-9])')) }
                else { $hit = ($view.IndexOf($needle, [System.StringComparison]::Ordinal) -ge 0) }
                if ($hit) { $hits += "$rel <- $($n.Label)"; break }
            }
        }
    }
    return @($hits | Sort-Object -Unique)
}

# ---------- 原生命令收流：5.1 那颗地雷的双引擎兜法(本轮实测逼出来的) ----------
function Invoke-GdNativeCapture {
    <# 5.1 + $ErrorActionPreference='Stop' + `2>&1` = **原生命令往 stderr 写一行，就当场炸一次终止错误**
       (NativeCommandError)；7 侧同一段代码只是把那一行当普通文本收进来。
       这不是理论问题，本轮就踩在出货链第 0 步上：`gdprobe --selftest` 里有一枚**故意**的负对照
       (拿 `Z:\…\envelope.json` 试"写不下去时读数不许凭空消失"，见 main.rs 的 render_envelope)，
       它按设计会往 stderr 印一行 —— 于是 5.1 下"探针 159 项全绿、退出码 0"与"打包链路当场抛"能同时发生。
       cargo 自己也把进度打在 stderr，同一族。
       本函数**只**管收流：在那一句里把 EAP 降到 Continue、stdout/stderr 都收成字符串数组、退出码原样交回。
       ⚠ 判据一律留在调用方：不许在这里替调用方判成败(判据只住一处的纪律不因收流方式而变)。 #>
    param([Parameter(Mandatory)][string]$Exe, [string[]]$Arguments = @())
    $prev = $ErrorActionPreference
    $code = -1
    $lines = @()
    $ErrorActionPreference = 'Continue'
    try {
        $lines = @(& $Exe @Arguments 2>&1 | ForEach-Object { [string]$_ })
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    return [pscustomobject]@{ ExitCode = $code; Lines = @($lines) }
}

# ---------- 日志侧信道(B-2c②)：核内按产生顺序逐行追加，壳侧按字节偏移增量排空 ----------
function Read-GdSinkIncrement {
    <# 只认整行：末段没有 LF 的那一截是半截行(核正在写)，本轮不消费、偏移不推进，下一轮从同一处再读。
       偏移一律用**字节数**：话术里满屏中文，一行 UTF-8 是三十几字节，按字符推进会把偏移算小 ⇒ 同一行反复重放。
       坏行(JSON 半截/编码错)照旧推进偏移并丢弃 —— 日志通道不许把整轮卡死，宁可少一行也不许停在等一行上。 #>
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][ref]$Offset)
    $bytes = $null
    $read = 0
    try {
        if (-not (Test-Path -LiteralPath $Path)) { return @() }
        $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $skip = [long]$Offset.Value
            if ($fs.Length -le $skip) { return @() }
            $null = $fs.Seek($skip, [System.IO.SeekOrigin]::Begin)
            $bytes = New-Object byte[] ([int]($fs.Length - $skip))
            $got = 0
            while ($got -lt $bytes.Length) {
                $n = $fs.Read($bytes, $got, $bytes.Length - $got)
                if ($n -le 0) { break }
                $got += $n
            }
            $read = $got
        } finally { $fs.Dispose() }
    } catch { return @() }
    if ($read -le 0) { return @() }
    if ($read -lt $bytes.Length) { $bytes = $bytes[0..($read - 1)] }
    $last = [Array]::LastIndexOf($bytes, [byte]10)
    if ($last -lt 0) { return @() }
    $consumed = $last + 1
    $text = [System.Text.Encoding]::UTF8.GetString($bytes, 0, $consumed)
    $Offset.Value = [long]$Offset.Value + [long]$consumed
    $rows = @()
    foreach ($ln in ($text -split "`n")) {
        $t = $ln
        if ($t.EndsWith("`r")) { $t = $t.Substring(0, $t.Length - 1) }
        if (-not $t) { continue }
        $obj = $null
        try { $obj = $t | ConvertFrom-Json } catch { $obj = $null }
        if ($null -eq $obj) { continue }
        $rows += [pscustomobject]@{ Level = [string]$obj.level; Text = [string]$obj.text }
    }
    return @($rows)
}

function Start-GdNativeStreamed {
    <# Invoke-GdNativeCapture 是"跑完再收"的同步缓冲管道 —— 它返回时链上日志早落完了，侧信道就白开了：
       这一把要的是**边跑边排空**(06:00 那一轮跑二十多分钟，卡住时一行不印就等于没留证)。
       本函数只做两件事：起进程 + 在等待循环里排空 sink 并逐行回调，退出后再末次排空一次。
       ⚠ 判据一律留在调用方：回调里怎么打日志、信封怎么判、动作怎么执行，全在调用方(判据只住一处的纪律不因收流方式而变)。
       ⚠ 两条实测约束：① stdout/stderr 必须**两路同时异步收**(ReadToEndAsync)，否则管道一满子进程就卡在写流上；
         ② 这里不走 `2>&1` 管道，5.1 那颗 stderr→终止错误的雷天然碰不到，但**本函数只在 pwsh7 上验过** —— 别当双引擎可用。
       ⚠ 10-20 加第三档(见 -HardWaitMs)：子进程**自己不退出**时，旧写法的等待循环是无界的，壳会陪着核一起睡到
         计划任务的执行时限(PT3H)把整班硬砍 —— 那一刀不落台账，等于缺勤一天。这一档与第二道保险是两种病，
         返回值分开计数、调用方分开说话，不许并成一句。 #>
    param(
        [Parameter(Mandatory)][string]$Exe,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)][string]$SinkPath,
        [Parameter(Mandatory)][ref]$SinkOffset,
        [scriptblock]$OnSinkRow,
        # 第二道保险的等待上限(毫秒)：只在"子进程已退、管道却没等到 EOF"这一支生效，健康路径上 Wait 当场为真。
        [int]$StreamDrainTimeoutMs = 5000,
        # 第三道保险(10-20)：子进程**没退出**的硬上限(毫秒)。0 = 照旧无界(三门的夹具桩与其余调用点行为一字不变)；…（细节见该帧）
        [int]$HardWaitMs = 0
    )
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe
    foreach ($a in @($Arguments)) { $psi.ArgumentList.Add([string]$a) }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $psi
    $null = $p.Start()
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    # 第三道保险的deadline：$HardWaitMs=0 时保持无界($null)，三门夹具桩与其余调用点一字不变。
    $waitTimedOut = $false
    $dline = if ($HardWaitMs -gt 0) { (Get-Date).AddMilliseconds([double]$HardWaitMs) } else { $null }
    try {
        while (-not $p.WaitForExit(200)) {
            foreach ($r in @(Read-GdSinkIncrement -Path $SinkPath -Offset $SinkOffset)) {
                if ($OnSinkRow) { & $OnSinkRow $r }
            }
            # 每 200ms 排空之后才问一次"到点没"：判据顺序不许反过来，否则排空回调会被deadline抢掉。
            # 到点这一支只收尸不判成败 —— 结束它是调用方的决断，这里只交回"等满"这个事实。
            if ($null -ne $dline -and (Get-Date) -gt $dline) {
                $waitTimedOut = $true
                try { $null = $p.Kill() } catch { }
                try { $null = $p.WaitForExit(5000) } catch { }
                break
            }
        }
    } finally {
        # 末次排空：退出前后落的那几行(典型是收尾那一句)只能在这里收，漏了就是整句日志凭空消失
        foreach ($r in @(Read-GdSinkIncrement -Path $SinkPath -Offset $SinkOffset)) {
            if ($OnSinkRow) { & $OnSinkRow $r }
        }
    }
    # Kill 那一支上 ExitCode 也可能读不到(收尸没等到)，读不到按 -1 交回，不许让这一行自己炸掉整轮。
    $code = -1
    try { $code = $p.ExitCode } catch { }
    # 第二道保险(10-13 现场定因)：子进程已退，但**被它起的进程继承了 stdout/stderr 管道的写端** ⇒ EOF 永不到……（细节见该帧）
    $timeouts = 0
    $lines = @()
    foreach ($tk in @($outTask, $errTask)) {
        $text = ''
        if ($tk.Wait($StreamDrainTimeoutMs)) { $text = [string]$tk.Result } else { $timeouts += 1 }
        $lines += @($text -split "`r?`n")
    }
    $p.Dispose()
    # 三种病分开交回：WaitTimedOut=核没退出(被这一档收尸) / StreamTimeouts>0=核退了但管道没等到 EOF / 两者皆无=真的没输出。
    # 新键加在末尾：既有调用方与三门夹具桩都不读它，读不存在的键得 $null(falsy)，行为等价。
    return [pscustomobject]@{ ExitCode = $code; Lines = @($lines); StreamTimeouts = $timeouts; WaitTimedOut = $waitTimedOut }
}

# ---------- GdCore 进程内内核加载器(K0 收官件，形态=丙·宿主各自现编；契约全文 docs\gd-core-contract.md) ----------…（细节见该帧）
$script:GdCoreState = 'off'      # off|live|fallback
# 刀⑲ 笔(d)：白名单与 coreOff 热开关同批退役——核心六枚纯规则自此只有内核一份，路由判据(Test-GdCoreFn)无臂可路由。
$script:GdCoreMs = -1            # 本进程现编一次花了多少毫秒(-1 = 没编过)
$script:GdChildProcs = 0         # 本进程经 Invoke-GdNative / Invoke-GdChild 起过的外部子进程累计

function Get-GdCoreContract { 'GDCore-K2.1' }   # 壳侧期望契约号；每内核化一批就同步 +1 并在 tools\gdcore\GdCore.cs 同改

function Add-GdCore {
    <# 幂等装载：已定态直接回现状。任何异常吞掉只落状态——本函数永不向上抛(回退语义)。 #>
    param([string]$SourceDir)
    if ($script:GdCoreState -ne 'off') { return $script:GdCoreState }
    if (-not $SourceDir) { $SourceDir = Join-Path $PSScriptRoot '..\tools\gdcore' }
    $ErrorActionPreference = 'Stop'
    try {
        $srcs = @(Get-ChildItem -LiteralPath $SourceDir -Filter '*.cs' -File | ForEach-Object { $_.FullName })
        if (-not $srcs.Count) { throw "内核源码目录没有 .cs：$SourceDir" }
        # 只量 Add-Type 这一段：它是本批唯一的新增开销，编译炸了也要留下它花了多少毫秒(finally 落表)
        $sw = New-Object System.Diagnostics.Stopwatch
        $sw.Start()
        try { Add-Type -Path $srcs } finally { $sw.Stop(); $script:GdCoreMs = [int]$sw.Elapsed.TotalMilliseconds }
        if ([GdCore.Meta]::Contract() -ne (Get-GdCoreContract)) {
            throw "契约号不匹配：内核=$([GdCore.Meta]::Contract()) 壳=$(Get-GdCoreContract)"
        }
        $script:GdCoreState = 'live'
    } catch {
        $script:GdCoreState = 'fallback'
        $script:GdCoreNote = '内核：回退PS'
    }
    return $script:GdCoreState
}

function Get-GdCoreStatus {
    <# 日志就绪后由入口自印的一行读数：live 不吵人(空串)，回退只说'内核：回退PS'。 #>
    if ($script:GdCoreState -eq 'fallback') { return $script:GdCoreNote }
    return ''
}

function Get-GdCoreReadout {
    <# 接线批(09-29)的性能读数出口：一句现状 —— 内核状态 / 契约号 / 现编花了几毫秒 / 内核侧路由了几件(笔(d) 起恒＝核心六枚全走内核) /
       本宿主起过几个外部子进程 / 宿主 PS 版本，入口在起跑与收尾各印一次(-ElapsedSec 只收尾传)。
       与上面那句的分工：Get-GdCoreStatus 是**契约钉死的回退警报**(live 恒空串，门里逐字比过)，
       本函数永远给全量、只进日志不判分 —— 所以想改这句的措辞随时可以，想改那句得先过契约门。 #>
    param([double]$ElapsedSec = -1)
    $ms = '未编'
    if ($script:GdCoreMs -ge 0) { $ms = [string]$script:GdCoreMs }
    $parts = @()
    $parts += '内核=' + $script:GdCoreState
    $parts += '契约=' + (Get-GdCoreContract)
    $parts += '现编=' + $ms + 'ms'
    $parts += '子进程=' + [string]$script:GdChildProcs
    $parts += '宿主=ps' + [string]$PSVersionTable.PSVersion
    if ($ElapsedSec -ge 0) { $parts += '挂钟=' + [string]([math]::Round($ElapsedSec, 1)) + 's' }
    return ($parts -join ' ')
}

# ---------- 装载归一(刀⑲ 笔(c)，2026-11-07) ----------
# 内核装载原来由 daily-clear / run-via-task / status 三本入口各写一次 `$null = Add-GdCore`(覆盖面 3 处)，
# 现在收进本文件段尾：谁 dot-source common.ps1，谁收尾就把内核编好或干净回退——覆盖面由 3 处变全量(本仓 30 本消费者)。
# 代价实测在案(raw/b26-coldstart.txt)：dot-source 173/169/180ms，段尾现编 +555/228/244ms，总计 772/456/488ms；
# 六枚壳函数 PS/C# 同帧对平 78 格全绿(raw/b27-core-parity.txt)，路由改由内核答不会改变任何一格的观测值。
# Add-GdCore 幂等且永不向上抛：源码缺 / 编译被拦 / 契约号两舷不一致都只落 state='fallback'，宿主仍照跑其余壳侧函数，但核心六枚当场拒跑。
$null = Add-GdCore
