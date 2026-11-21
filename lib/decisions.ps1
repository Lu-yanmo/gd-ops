# lib/decisions.ps1 — "今天该跑什么"的门面：把两边判定拼起来(聚合件，本体在下面三份里)
#
# 2026-09-23 拆分：判定本体已经按游戏搬走，本文件只留聚合(所以老代码 . lib\decisions.ps1 照旧可用)。
#   lib\decisions-common.ps1   两个游戏共用的底座：时间换算 + 活动确认门/商店领空标记的读写
#   lib\ark-decisions.ps1      方舟侧：Get-GdStageTable / Resolve-ArkPlan / Get-ArkEvent(⚠ 11-02 转接刀摘掉 Test-GdStages 那枚转发壳；逐枚名册以表账为准。⚠ 刀⑯ 收官批 2026-11-06：Get-ArkEvent 整枚出仓，取数半段全在核 verb read-ark-activity，消费方各自组合式现问)
#   lib\r1999-decisions.ps1    1999 侧：Resolve-R1999Plan / Get-R1999Event / Resolve-R1999Instance
# 只发布一个游戏的仓库时，本文件可以原样复制过去：下面按"文件在不在"决定点不点，
# 单游戏仓库里没有另一侧那个文件，就自动少一侧的函数，不会因为 dot 一个不存在的文件而炸。
# 依赖关系：这三个文件只用 lib\common.ps1 里的函数，而调用方(daily-clear / status / 自检)本来就先把
# common.ps1 点进来了，所以这里不再重复点 —— 重复点会把控制台编码再设一遍，白做工。
# ⚠ 10-19 凌晨 C-3 首刀(C-0〗i)：这个文件原本"另带一个跨游戏便捷入口 Show-GdDecision"，那一枚已整枚删除。
#   它是删除前对照表里唯一一枚"生产 CODE 整词零命中、且除自家尺 tools\ps-map\ 之外没有任何门或测试钉着"的函数
#   (其余十枚生产零命中的各有钉家，读数、两处注释改口与三发换靶的连带账见 docs\rust-core-plan.md §11.81)。
foreach ($part in @('decisions-common', 'ark-decisions', 'r1999-decisions')) {
    $partPath = Join-Path $PSScriptRoot "$part.ps1"
    if (Test-Path -LiteralPath $partPath) { . $partPath }
}
