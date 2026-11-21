// GdWin.cs - B0(2026-09-29)：这段 P/Invoke 原来内联在 lib\vision-ark.ps1 的 Add-GdRescueTypeDef 里
// (Add-Type -TypeDefinition @ ... @)，现在逐字搬到这里，类名/成员名/注释一个字都没改。
// 它是端游视觉兜底的现场手脚：PrintWindow 拍窗口、抢前台、真实鼠标点一下 —— 全是窗口调用，
// 不含任何判定。位图/Graphics 一律留在 PowerShell 侧：Store 版 pwsh 7 里 Add-Type 一引用 System.Drawing
// 就 CS0012(找不到 System.Private.Windows.GdiPlus)，所以 C# 这边只当 user32 的传声筒。
// 装载：lib\common.ps1 的 Add-GdInterop 对本目录 *.cs 做一次 Add-Type -Path 现编，进程内编译、不落 dll。
// 硬约束同 GdPower.cs：C#5 上限(5.1 走 CodeDom，不用字符串插值/?./表达式体成员/nameof)、只引 BCL、
// 本目录允许中文注释(与 tools\gdcore 的纯 ASCII 是两条规矩)。
// GdRect / GdPoint 跟 GdWin 留在同一份文件里：它们是 GdWin 签名的依赖，拆开放不好读。
// 同名类不许编两次：这份加进来而 vision-ark 那份 here-string 没删的话，两个宿主都会响亮抛
// "类型名称 GdWin 已存在"(2026-09-29 实测)，原成员仍可解析而新成员不可达 —— 所以迁移必须
// "新 .cs + 删掉那处 here-string"同批落地。
// 门：tools\test-ark.ps1 的 t14 原来钉 (Get-Command Add-GdRescueTypeDef).Definition 的文本，
// B0③b 起改钉这份文件的文本。
// B0-VERBATIM-BELOW
using System;
using System.Runtime.InteropServices;

public struct GdRect { public int Left; public int Top; public int Right; public int Bottom; }
public struct GdPoint { public int X; public int Y; }

public static class GdWin
{
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out GdRect r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out GdRect r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref GdPoint p);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out GdPoint p);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesktopAccess);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool GetUserObjectInformationW(IntPtr hObj, int nIndex, System.Text.StringBuilder pvInfo, uint nLength, out uint lpnLengthNeeded);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr hDesktop);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint lpdwProcessId);

    public static GdRect WindowRect(IntPtr h)  { GdRect r; GetWindowRect(h, out r);  return r; }

    // 下面三个都是"看一眼现场"的只读调用，给点击环境探测用：不动窗口、不点客户区、不动鼠标。
    public static bool Visible(IntPtr h) { return IsWindowVisible(h); }
    public static string CursorText()
    {
        GdPoint p;
        if (!GetCursorPos(out p)) { return ""; }
        return p.X + "," + p.Y;
    }
    public static string WindowRectText(IntPtr h)
    {
        GdRect r;
        if (!GetWindowRect(h, out r)) { return ""; }
        return r.Left + "," + r.Top + " " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top);
    }

    // SW_RESTORE：把最小化的窗口放回来。掉线/MAA 收尾都会把端游最小化，那时
    // GetWindowRect 给的是屏幕外那个占位矩形(-32000,-32000，168x29)，拍出来必然是空东西。
    // 这是窗口状态调用，不是客户区输入，所以不会误点游戏里的按钮。
    public static bool Restore(IntPtr h) { return ShowWindow(h, 9); }
    public static GdRect ClientRect(IntPtr h)  { GdRect r; GetClientRect(h, out r);  return r; }

    // 客户区左上角在屏幕上的位置：窗口位图(整窗) → 客户区坐标(PostMessage 要的那个)要靠它换算
    public static GdPoint ClientOrigin(IntPtr h)
    {
        GdPoint p = new GdPoint(); p.X = 0; p.Y = 0;
        ClientToScreen(h, ref p);
        return p;
    }

    public static IntPtr PackClient(int x, int y)
    {
        // LPARAM = MAKELONG(x, y)：低 16 位 x、高 16 位 y(客户区坐标，不是屏幕坐标)
        return (IntPtr)(int)(((uint)(y & 0xFFFF) << 16) | (uint)(x & 0xFFFF));
    }

    public static bool ClickOnce(IntPtr h, int x, int y)
    {
        // WM_NCHITTEST 那套不走，游戏客户端要的是客户区的鼠标消息：MOVE → DOWN(带 MK_LBUTTON) → UP
        PostMessage(h, 0x0200, IntPtr.Zero, PackClient(x, y));          // WM_MOUSEMOVE
        System.Threading.Thread.Sleep(120);
        bool a = PostMessage(h, 0x0201, (IntPtr)0x0001, PackClient(x, y));   // WM_LBUTTONDOWN, wParam=MK_LBUTTON
        System.Threading.Thread.Sleep(90);
        bool b = PostMessage(h, 0x0202, IntPtr.Zero, PackClient(x, y));      // WM_LBUTTONUP
        return a && b;
    }

    // 客户区坐标 → 屏幕坐标(真实鼠标只认屏幕坐标)
    public static GdPoint ToScreen(IntPtr h, int clientX, int clientY)
    {
        GdPoint p = new GdPoint(); p.X = clientX; p.Y = clientY;
        ClientToScreen(h, ref p);
        return p;
    }

    public static GdPoint CursorPos() { GdPoint p; GetCursorPos(out p); return p; }

    // 当前"接收鼠标键盘的那个桌面"叫什么：解锁时是 Default，锁屏/切到登录界面时是 Winlogon。
    // 为什么读它：这是"真实点击到底进不进得来"最直接的判据，而且不需要管理员。
    // OpenInputDesktop 只要 DESKTOP_READOBJECTS(0x0002)，UOI_NAME 是 2。拿不到名字返回空串(调用方按"不知道"处理)。
    public static string InputDesktopName()
    {
        IntPtr h = OpenInputDesktop(0x0002, false, 0x0002);
        if (h == IntPtr.Zero) { return ""; }
        try
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            uint need = 0;
            if (!GetUserObjectInformationW(h, 2, sb, 512, out need)) { return ""; }
            return sb.ToString();
        }
        finally { CloseDesktop(h); }
    }

    public static IntPtr ForegroundWindow() { return GetForegroundWindow(); }

    public static uint WindowPid(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid; }

    public static bool TryForeground(IntPtr h)
    {
        // 后台进程抢前台会被系统拒绝；先"按下 ALT"再抢是公认那招。抢不到就返回 false，
        // 调用方必须改走别的路 —— 真实点击是打在"鼠标当前所在的那个窗口"上的，抢不到前台还点，
        // 就等于在用户正在用的窗口里瞎点一下。
        keybd_event(0x12, 0, 0, IntPtr.Zero);                 // VK_MENU down
        if (IsIconic(h)) { ShowWindow(h, 9); }                 // SW_RESTORE：只在最小化时恢复，别把最大化也撤了
        bool ok = SetForegroundWindow(h);
        keybd_event(0x12, 0, 2, IntPtr.Zero);                 // VK_MENU up(KEYEVENTF_KEYUP)
        System.Threading.Thread.Sleep(350);                    // 等系统真的把焦点换过来
        return ok && (GetForegroundWindow() == h);
    }

    public static bool RealClick(int screenX, int screenY)
    {
        // 真实鼠标：光标移过去再按下/抬起，我们自己的兜底点击走这条。
        // 注意别再把它说成"MAA 在这个端游上也是这么点的"：2026-09-25 把整份 asst.log 按动作归类数过，
        // inject_input_event 全部来自基建左滑(SwipeToTheLeft)，7600 多次点击一条注入都没有 —— MAA 的点击
        // 一直是配置里的发消息方式，而那种点击在有人在机器前时是点得动这个端游的。
        if (!SetCursorPos(screenX, screenY)) { return false; } // 目标点在屏幕外/被裁掉了 → 别点
        System.Threading.Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);             // MOUSEEVENTF_LEFTDOWN
        System.Threading.Thread.Sleep(90);
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);             // MOUSEEVENTF_LEFTUP
        return true;
    }
}