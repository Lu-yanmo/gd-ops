// GdSettingChange.cs - B0(2026-09-29)：这段 P/Invoke 原来内联在 tools\unarm-screensaver.ps1 的
// Set-GdSettingChangedBroadcast 里，逐字搬来，类名/成员名一字未改。
// 它只干一件事：往 HWND_BROADCAST 发一条 WM_SETTINGCHANGE，让正在跑的会话重读
// HKCU\Control Panel\Desktop —— 不注入输入、不碰别的进程，所以不提权也不会替你把屏保点掉。
// 装载：lib\common.ps1 的 Add-GdInterop 对 tools\gdinterop\*.cs 现编一次(unarm 那把工具点 common.ps1，
// 所以这个函数在那棵树上一直可用)。编不过时 Add-GdInterop 只落状态不抛，本类的调用方照旧走它自己的
// catch —— 那里印的就是"广播失败，注册表已经改好了，注销/重登一次生效"，与旧行为同形。
// 硬约束同 GdPower.cs：C#5 上限、只引 BCL、不出现 System.Drawing 类型、本目录允许中文注释。
// B0-VERBATIM-BELOW
using System;
using System.Runtime.InteropServices;
public static class GdSettingChange {
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
}
