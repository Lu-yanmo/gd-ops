<#
  tools/check-ps51.ps1 —— 脚本的编码 / 行尾 / 语法守卫。

  （名字是历史口令：2026-10-06 之前它的使命是"5.1 兼容检查"。Windows PowerShell 5.1 已随
   「退役 5.1」的决定整体退役，仓库现在只支持 PowerShell 7；文件名没改是为了不动一堆引用点，
   与 5.1 无关的两件事留下、与 5.1 绑定的一层整体删掉，明细见下。）

  它管两件事：
    1) 用"当前引擎"的解析器解析每个 .ps1 —— 本仓库只支持 PowerShell 7，所以本文件必须用 pwsh 跑。
       在 5.1 里跑不仅没有意义、结果还是假的：7 方言会被 5.1 的解析器整批报成解析错误，
       所以文件开头有一道守卫，5.1 下当场拦下，不给你看一屏假红。
    2) 断言编码与行尾：.ps1 是 UTF-8 带 BOM —— 这一项自 2026-10-06 起从"必须"降为"推荐"：
       缺 BOM 只提示(TIP)、不报红。当初的 BOM 规矩是为 5.1 而立(5.1 读不到 BOM 会把 UTF-8 中文
       按 GBK 拆，连字符串终止符都能拆丢)，5.1 退役后它回归成"全仓统一口径"的惯例。
       .cmd 反过来必须无 BOM + CRLF(cmd.exe 不认 LF、也不吃 BOM) —— 这部分与 5.1 无关，原样保留。
       空脚本(0 字节或只剩 BOM)会同时过掉解析和编码两道检查 → 单独判 EMPTY 报红，不给假绿灯。
       加 -Fix 就按这个规矩就地重写：先断言正文非空，写完回读比对，对不上立刻还原原字节。
       任何批量改字节的操作都照这个套路来 —— 本仓库有过一次教训：探 BOM 用了
       [IO.File]::Create(那是"创建并清空")，一次跑完把仓库里所有 .ps1 截成了 0 字节。

  已退役(2026-10-06，随手记录免得日后有人问"怎么没了")：
    * "能解析但 5.1 运行时会炸"的静态扫描(15 条：?? / ?. / -Parallel / -AsHashtable / Get-Error /
      $IsWindows / -SkipCertificateCheck / $PSStyle / Join-Path 多子路径 / 三元表达式 …) —— 逐条判过，
      15/15 全是"7 合法、只坑 5.1"的形状，7 世界里扫它们没有意义。顺带：那两处常年上提示的老假阳性
      (probe-decisions-golden / probe-process-golden 的 Join-Path 行)随之清零。
    * "走 HTTP 没开 TLS 1.2"启发式 —— 同为 5.1 专属坑(5.1 默认只发 TLS 1.0)；7 默认就走 TLS 1.2+，
      而且 PS 侧的 HTTP 已整体交给 tools\vision\ask.py，这条启发式两头都不再适用。
    * "#Requires -Version 7 会挡 5.1 的门" —— 反过来看，它现在是"5.1 误跑时快速失败"的正经写法；
      本工具不再把它当可疑项(也不强制建议加)。

  用法：
    pwsh -File tools\check-ps51.ps1            # 常规跑法(在 5.1 下跑会被头部守卫拦下)
    pwsh -File tools\check-ps51.ps1 -Fix       # 统一编码与行尾(改字节，先拿副本目录试一次)
    pwsh -File tools\check-ps51.ps1 -Dir <副本> -Fix   # 推荐：先在临时目录的副本上跑一遍
    pwsh -File tools\check-ps51.ps1 -Verbose2  # 把每个文件的编码情况也列出来

  退出码：0=干净；1=有解析错误或编码不合规(必须修)；2=只有提示(TIP 行，自己判断)。
#>
[CmdletBinding()]
param(
    [string]$Dir = '.',
    [switch]$Fix,
    [switch]$Verbose2
)

$ErrorActionPreference = 'Stop'

# 输出里全是中文，重定向到文件时默认按 OEM/GBK 写会变成乱码 —— 和 lib/common.ps1 一样先统一到 UTF-8
try {
    [System.Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    [System.Console]::InputEncoding  = [System.Text.Encoding]::UTF8
} catch { }

# 守卫：本仓库只支持 PowerShell 7。5.1 的解析器读不懂 7 方言，会把每个用了 7 写法的文件都报成"解析错误"
# —— 那不是被检查的仓库有问题，是引擎用错了。当场拦下，别输出一屏假红。
if ($PSVersionTable.PSVersion.Major -lt 6) {
    Write-Host '引擎用错了：本仓库只支持 PowerShell 7，5.1 的解析器会把 7 方言整批报成解析错误(假红)。' -ForegroundColor Red
    Write-Host '请改用：pwsh -File tools\check-ps51.ps1' -ForegroundColor Yellow
    exit 1
}

function Get-GdTextFiles {
    param([string]$Root)
    return @(Get-ChildItem -Path $Root -Recurse -Include '*.ps1', '*.cmd', '*.bat' |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|backups|logs|node_modules|\.git)\\' } |
        Sort-Object FullName)
}

function Read-GdBytes {
    param([string]$Path)
    $fs = [System.IO.File]::OpenRead($Path)
    try {
        $b = New-Object byte[] $fs.Length
        if ($b.Length -gt 0) { $fs.Read($b, 0, $b.Length) | Out-Null }
        return ,$b
    } finally { $fs.Close() }
}

function Get-GdFileKind {
    param([System.IO.FileInfo]$F)
    switch ($F.Extension.ToLowerInvariant()) {
        # .ps1 的 NeedBom 现在是"推荐形"：缺了只提示(BomSoft)、不报红；不该有 BOM 的规则原样。
        '.ps1' { return [pscustomobject]@{ NeedBom = $true;  NeedCrlf = $false; BomSoft = $true  } }
        '.cmd' { return [pscustomobject]@{ NeedBom = $false; NeedCrlf = $true;  BomSoft = $false } }
        '.bat' { return [pscustomobject]@{ NeedBom = $false; NeedCrlf = $true;  BomSoft = $false } }
        default { return $null }
    }
}

function Test-GdBom {
    param([byte[]]$Bytes)
    return ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF)
}

function Get-GdEolStats {
    param([byte[]]$Bytes)
    $lf = 0; $crlf = 0
    for ($i = 0; $i -lt $Bytes.Length; $i++) {
        if ($Bytes[$i] -ne 0x0A) { continue }
        if ($i -gt 0 -and $Bytes[$i - 1] -eq 0x0D) { $crlf++ } else { $lf++ }
    }
    return [pscustomobject]@{ Lf = $lf; Crlf = $crlf }
}

function Get-GdPlainBytes {
    <# 字节 → "去 BOM、统一 LF" 的正文。比对重写前后用同一把尺子。 #>
    param([byte[]]$Bytes)
    $skip = 0
    if (Test-GdBom -Bytes $Bytes) { $skip = 3 }
    if ($Bytes.Length -le $skip) { return '' }
    return ([System.Text.Encoding]::UTF8.GetString($Bytes, $skip, $Bytes.Length - $skip) -replace "`r`n", "`n")
}

function Set-GdFileEncoding {
    <#
      按规矩重写：.ps1 = UTF-8 BOM + LF(推荐形)；.cmd/.bat = UTF-8 无 BOM + CRLF。
      编码写法上的坑：UTF8Encoding($true).GetBytes() 是不写 BOM 的(BOM 只由 preamble/WriteAllText 负责)，
      所以走 [IO.File]::WriteAllText —— 它才会把 encoding.GetPreamble() 拼在最前面。

      三道保险(都是被真实事故喂出来的，这次把整个仓库的 .ps1 截成了 0 字节)：
        1) 空文件拒写：读回来 <=3 字节(0 字节或光剩个 BOM)时，"重写编码"只会把事故固化，直接返回 empty。
           顺带记住反例：探 BOM 绝不能用 [IO.File]::Create(path) —— Create 是"创建并清空"，
           想只读必须用 OpenRead/FileStream(FileMode::Open, FileAccess::Read)。
        2) 正文为空拒写：解码后 IsNullOrWhiteSpace 也当空文件处理。
        3) 写完立刻回读，用同一把尺子(Get-GdPlainBytes + 同样的行尾规则)比对正文；
           不一致就用内存里那份原始字节还原，返回 rolled-back —— 宁可留着"没修好"，也不能丢内容。
      返回：fixed / empty / rolled-back。
    #>
    param([System.IO.FileInfo]$F, [bool]$NeedBom, [bool]$NeedCrlf)
    $orig = Read-GdBytes -Path $F.FullName
    if ($orig.Length -le 3) { return 'empty' }
    $text = Get-GdPlainBytes -Bytes $orig
    if ([string]::IsNullOrWhiteSpace($text)) { return 'empty' }
    if ($NeedCrlf) { $text = $text -replace "`n", "`r`n" }
    [System.IO.File]::WriteAllText($F.FullName, $text, (New-Object System.Text.UTF8Encoding($NeedBom)))
    $after = Get-GdPlainBytes -Bytes (Read-GdBytes -Path $F.FullName)
    if ($NeedCrlf) { $after = $after -replace "`n", "`r`n" }
    if ($after -cne $text) {
        [System.IO.File]::WriteAllBytes($F.FullName, $orig)
        return 'rolled-back'
    }
    return 'fixed'
}

function Get-GdSnip {
    param([string]$Text, [int]$Width = 100)
    $t = ($Text -replace '\s+', ' ').Trim()
    if ($t.Length -gt $Width) { $t = $t.Substring(0, $Width) + '…' }
    return $t
}

$root = (Resolve-Path -LiteralPath $Dir).Path
$files = Get-GdTextFiles -Root $root
Write-Host ("引擎 PowerShell {0}   目录 {1}   待检查 {2} 个文件" -f $PSVersionTable.PSVersion, $root, $files.Count) -ForegroundColor Cyan

$parseErrors = 0
$encodingErrors = 0
$tips = 0

foreach ($f in $files) {
    $kind = Get-GdFileKind -F $f
    if ($null -eq $kind) { continue }

    # ---------- 2) 编码 / 行尾 ----------
    $bytes = Read-GdBytes -Path $f.FullName
    if ($bytes.Length -le 3) {
        $encodingErrors++
        Write-Host ("EMPTY {0,-23} 只有 {1} 字节 —— 空脚本能同时过掉解析和编码两道检查，这是假绿灯；先确认内容是不是被截断了" -f $f.Name, $bytes.Length) -ForegroundColor Red
        continue
    }
    $hasBom = Test-GdBom -Bytes $bytes
    $eol = Get-GdEolStats -Bytes $bytes
    $badBom = ($hasBom -ne $kind.NeedBom)
    $badEol = if ($kind.NeedCrlf) { ($eol.Lf -gt 0) } else { ($eol.Crlf -gt 0) }
    if ($Fix -and ($badBom -or $badEol)) {
        $res = Set-GdFileEncoding -F $f -NeedBom $kind.NeedBom -NeedCrlf $kind.NeedCrlf
        if ($res -eq 'fixed') {
            Write-Host ("FIX  {0,-24} → BOM={1} CRLF={2}" -f $f.Name, $kind.NeedBom, $kind.NeedCrlf) -ForegroundColor Green
        }
        elseif ($res -eq 'empty') {
            Write-Host ("SKIP {0,-24} 空文件，拒绝重写" -f $f.Name) -ForegroundColor Red
        }
        else {
            Write-Host ("ROLL {0,-24} 重写后回读正文不一致，已还原原字节" -f $f.Name) -ForegroundColor Red
        }
        $bytes = Read-GdBytes -Path $f.FullName
        $hasBom = Test-GdBom -Bytes $bytes
        $eol = Get-GdEolStats -Bytes $bytes
        $badBom = ($hasBom -ne $kind.NeedBom)
        $badEol = if ($kind.NeedCrlf) { ($eol.Lf -gt 0) } else { ($eol.Crlf -gt 0) }
    }
    # .ps1 缺 BOM 是唯一从红降为提示(TIP)的一格；"多了不该有的 BOM"与任何行尾问题照旧报红。
    $tipOnly = ($badBom -and $kind.BomSoft -and -not $badEol)
    if ($tipOnly) {
        $tips++
        Write-Host ("TIP  {0,-24} 缺 BOM(2026-10-06 起 .ps1 的 BOM 只是推荐项，不报红；想统一就 -Fix)" -f $f.Name) -ForegroundColor DarkYellow
    }
    elseif ($badBom -or $badEol) {
        $encodingErrors++
        $why = @()
        if ($badBom) { $why += $(if ($hasBom) { '不该有 BOM(cmd.exe 不吃)' } else { '缺 BOM(推荐项)' }) }
        if ($badEol) { $why += $(if ($kind.NeedCrlf) { '应是 CRLF(cmd.exe 要)' } else { '混进了 CRLF' }) }
        Write-Host ("ENC  {0,-24} {1}" -f $f.Name, ($why -join '，')) -ForegroundColor Red
    }
    elseif ($Verbose2) {
        Write-Host ("enc  {0,-24} OK  BOM={1} LF={2} CRLF={3}" -f $f.Name, $hasBom, $eol.Lf, $eol.Crlf) -ForegroundColor DarkGray
    }

    if ($f.Extension.ToLowerInvariant() -ne '.ps1') { continue }

    # ---------- 1) 语法 ----------
    $tokens = $null; $errs = $null
    [System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$tokens, [ref]$errs) | Out-Null
    if (@($errs).Count -gt 0) {
        $parseErrors += @($errs).Count
        Write-Host ("SYN  {0,-24} 解析错误 {1} 个" -f $f.Name, @($errs).Count) -ForegroundColor Red
        foreach ($e in @($errs | Select-Object -First 5)) {
            Write-Host ("     第 {0} 行：{1}" -f $e.Extent.StartLineNumber, (Get-GdSnip ($e.Message -replace "`r?`n", ' ') 140)) -ForegroundColor Yellow
        }
        continue
    }
    elseif ($Verbose2) { Write-Host ("syn  {0,-24} OK" -f $f.Name) -ForegroundColor DarkGray }
}

Write-Host ''
if ($Fix) { Write-Host '已按规矩重写编码/行尾(.ps1 = UTF-8 BOM + LF〔推荐形〕；.cmd = UTF-8 无 BOM + CRLF)。' -ForegroundColor Green }
Write-Host ("解析错误 {0} 个 / 编码不合规 {1} 个 / 提示 {2} 处" -f $parseErrors, $encodingErrors, $tips)
if ($parseErrors -gt 0 -or $encodingErrors -gt 0) {
    Write-Host '结论：有必须修的问题(RED 行)。' -ForegroundColor Red
    exit 1
}
if ($tips -gt 0) {
    Write-Host '结论：解析/编码/行尾合规；TIP 行只是推荐项(不挡发布)。' -ForegroundColor Yellow
    exit 2
}
Write-Host '结论：解析、编码与行尾全部合规。' -ForegroundColor Green
exit 0
