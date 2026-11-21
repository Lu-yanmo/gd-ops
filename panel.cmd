@echo off
rem panel.cmd —— 双击打开图形状态面板(tools\panel-cs\panel-cs.exe)，给不想碰命令行的人。
rem 引擎挑选：优先 PATH 里的 pwsh，其次 Program Files 装机版；缺了会提示装 PowerShell 7。
rem 面板只是外壳：所有动作都转调 daily-clear.ps1 / status.ps1 / run-via-task.ps1，判定逻辑一份都不重复。
rem 所以「面板里看到的结论」和命令行跑同一套脚本得到的结论是同一个 —— 它不自己算。
rem 2026-10-23 乙档定案(所有者拍板"兜底可以撤了")：PowerShell 那版面板(tools\panel.ps1)整件退役，
rem 判定面的真身就是 tools\panel-cs\Panel.Summary.cs —— 本文件因此**不再有"编不出来回落 PS 面板"那一路**，
rem 编不出来就明说编不出来。带参数照旧，只是交给 panel-cs.exe(--selftest 之类是它的无头档)。
rem 注意：本文件必须是 CRLF 行尾、且不能带 UTF-8 BOM(cmd.exe 两样都不认)。
chcp 65001 >nul
setlocal
title 每日清体力 状态面板
set "PS="
where pwsh.exe >nul 2>nul && set "PS=pwsh.exe"
if not defined PS if exist "%ProgramFiles%\PowerShell\7\pwsh.exe" set "PS=%ProgramFiles%\PowerShell\7\pwsh.exe"
if not defined PS echo 没找到 PowerShell 7（pwsh）。本仓库只支持 PowerShell 7，请先安装：winget install Microsoft.PowerShell
if not defined PS echo 装好后重新双击本文件。
if not defined PS pause
if not defined PS exit /b 1
rem 裸开、带参数都先要那枚 exe：没有就用 Windows 自带的 csc 现编一份(几秒钟，不用装任何东西)。
if not exist "%~dp0tools\panel-cs\panel-cs.exe" (
    "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\panel-cs\build.ps1" -Quiet
)
if not exist "%~dp0tools\panel-cs\panel-cs.exe" goto nobuild
rem 裸开=起窗口后立刻交回终端(定时任务不受影响)；带参数=同步跑完并把退出码交回调用方(给自动化用)。
if not "%~1"=="" goto runargs
start "" "%~dp0tools\panel-cs\panel-cs.exe"
exit /b 0
:runargs
"%~dp0tools\panel-cs\panel-cs.exe" %*
set "RC=%ERRORLEVEL%"
echo.
if not "%RC%"=="0" echo 面板退出码 %RC%。多半是 WinForms 起不来(极精简系统/组策略)。命令行做的是同一件事，例如：
if not "%RC%"=="0" echo   "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0daily-clear.ps1" -DryRun
if not "%RC%"=="0" echo 全部参数和排查步骤见 README.md。
if not "%RC%"=="0" pause
exit /b %RC%
:nobuild
echo 编译不出 C# 面板(tools\panel-cs\panel-cs.exe)：这台机器多半没有系统自带 csc.exe(.NET Framework 4 编译器)。
echo 判定层已经不住在 PowerShell 面板里了，所以这里没有"回落 PS 面板"那一步可走。命令行做的是同一件事，例如：
echo   "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0status.ps1" -Offline
echo 全部参数和排查步骤见 README.md。
pause
exit /b 3
