// GdPower.cs - B0(2026-09-29)：这段 P/Invoke 原来内联在 lib\common.ps1 的 Add-GdSleepTypeDef 里
// (Add-Type -TypeDefinition @' ... '@)，现在逐字搬到这里，类名/成员名/注释一个字都没改。
// 装载方式照内核那一套：宿主自己现编，lib\common.ps1 的 Add-GdInterop 对 tools\gdinterop\*.cs
// 做一次 Add-Type -Path，进程内编译、不落 dll、不分发二进制；编不过就静默回退，绝不把 06:00 那轮置于死地。
// 硬约束
// - C#5 上限(5.1 走 CodeDom)：不用字符串插值、不用 ?.(null 条件)、不用表达式体成员、不用 nameof。
// - 只引 BCL。不许在这里出现 System.Drawing 的类型：Store 版 pwsh 7 里 Add-Type 一引用它就 CS0012
//   (找不到 System.Private.Windows.GdiPlus)直接编不过 —— Bitmap/Graphics 留在 PowerShell 侧调。
// - 与 tools\gdcore(那份规则是纯 ASCII)不同：本目录**允许中文注释**。2026-09-29 实测无 BOM UTF-8 的
//   中文 .cs 在两个宿主都能现编通过(pwsh 7 约 191ms / 5.1 约 178ms)，读回的码点逐字相同。
// - 一个类一个文件，不许把这里的类和 PS 侧残留的同名 here-string 编进同一个进程(类型重名会让
//   [类名] 解析变歧义)。迁移必须"文件 + 那处 here-string"同批删掉。
// B0-VERBATIM-BELOW
using System;
using System.Runtime.InteropServices;
public static class GdPower {
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);
    public const uint ES_CONTINUOUS       = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED  = 0x00000001;
    public const uint ES_DISPLAY_REQUIRED = 0x00000002;

    // ES_DISPLAY_REQUIRED 只管显示器电源/睡眠计时；屏保看的是"最后一次输入"，
    // 脚本不动鼠标，到点照样全屏盖住端游窗口 → 必须显式关掉。
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
    public static extern bool SPIGetActive(uint action, uint param, ref bool pvParam, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
    public static extern bool SPISetActive(uint action, uint param, IntPtr pvParam, uint flags);
    public const uint SPI_GETSCREENSAVEACTIVE = 0x0010;
    public const uint SPI_SETSCREENSAVEACTIVE = 0x0011;
    public const uint SPIF_SENDWININICHANGE   = 0x0002;
}
