// tools/panel-cs/Vision.cs —— 批次 4：视觉兜底层的"纯规则段"本体，从 lib\vision-rescue.ps1 / lib\vision-ark.ps1 搬来。
//
// 单一事实源纪律：自本批起 PS 侧的 Get-GdVisionSystemPrompt / Get-GdVisionVerdictFromText /
// Get-GdRescueClickPoint / Resolve-GdVisionEndpoint 的转发壳，与已随第二十刀刀③ 出仓的 Test-GdShotChanged／
// Add-GdRescueRecord 两枚壳的字段合成段、
// Get-GdArkDesktopState 的文案拼装段、Invoke-GdVisionRaw 的 stdout 回扫段 —— 规则只写 C# 这一份。
// 文案必须与 PS 原话逐字符一致：期望串逐字钉在 Vision.SelfTest.cs(--visiontest，12 刀起接替已退役的
// tools\probe-vision-golden.ps1 等值门)，
// 有差异回修这里，禁止改期望、禁止放宽比对(docs\ps-migration-batch0.md §7)。
//
// 切分口径(与批次 3 的"外部进程层"同一刀法，落到视觉层)：
//   * 起子进程(ask.py)、拍屏(PrintWindow/adb)、点窗口(抢前台/input tap)、靶子组装与兜底总编排
//     Invoke-GdVisionRescue 当年按"活段，归批次 5/6 编排层"全留 PS；编排 10-23 起进核(B-2b)，
//     那枚 PS 真身已于 2026-10-28 甲档第二十刀刀③ 出仓 —— 本文件那批纯规则段的对面只剩核与 C#。
//   * vision-mumu 整体留壳不动：test-r1999 把 `d{1,3}` 正则与 `$ons.Count -eq 1` **钉在函数定义文本上**
//     (384–405 行的肯定式断言)，抠成薄壳就红 —— 断言一字不改是批次纪律，所以那两段正则的归属等断言面挪走再谈。
//   * Get-GdVisionApiKey 不迁：key 绝不允许经过 JSON 信封/命令行/日志(安全边界，见 vision-rescue 文件头)。
//   * "无结论"照批次 2/3 协议编码成 Found:false，PS 壳翻回 $null。
//
// 数字读回 Int32/Decimal 混发(批次 0 钉死)——这里一律 Gd.Str/Int 与 Convert.*，不裸强转。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class Vision
    {
        static readonly RegexOptions IC = RegexOptions.IgnoreCase;

        // PowerShell 口径的小零件(与本层旧实现逐条对齐)
        static string PsStr(object v)
        {
            // [string]$x：null→""，布尔→"True"/"False"，数字→不变文化，其余→ToString。
            if (v == null) return "";
            if (v is bool) return ((bool)v) ? "True" : "False";
            if (v is string) return (string)v;
            if (v is int || v is long || v is decimal || v is double || v is float)
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            return v.ToString();
        }

        static bool Bool(object v)
        {
            if (v == null) return false;
            if (v is bool) return (bool)v;
            if (v is string)
            {
                string s = ((string)v).Trim();
                if (s.Length == 0) return false;
                if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
                double d;
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d != 0.0;
                return true;   // 有意行为差：旧 PS 对 [bool]"abc" 是抛转换错，这里按真处理(同 Processes.Bool 的口径)
            }
            if (v is IDictionary<string, object>) return ((IDictionary<string, object>)v).Count > 0;
            if (v is object[]) return ((object[])v).Length > 0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture) != 0.0; } catch { return true; }
        }

        // [int]::TryParse([string]$x)：允许首尾空白与正负号，小数点一律不过(与 Processes.IntCast 同一族口径)
        static bool IntTry(object v, out int parsed)
        {
            parsed = 0;
            string s = PsStr(v);
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        static int IntOf(object v, int def)
        {
            int x;
            return IntTry(v, out x) ? x : def;
        }

        // ==================== verb: build-vision-prompt(原 Get-GdVisionSystemPrompt) ====================
        // 提示词的组装规则本体在这里。head 只有"这是什么画面"一段可换，五条规则两路共用、一个字不松；
        // t13 直接对返回串做 -match 断言(安全边界)，所以逐字符不许漂移。
        // 行尾必须是 LF：lib 下的 .ps1 是 LF，旧实现里 here-string 装的就是 `n。

        const string ArkHead =
            "你是明日方舟端游自动化流程里的\"看屏幕的人\"。自动化助手(MAA)不认识界面上没收录过的弹窗，有时会一直在原地\n" +
            "点同一个按钮。给你一张端游窗口截图，请判断画面上有没有挡住主流程的弹窗，并指出该点哪里把它关掉。";

        // here-string 语义逐条照抄(与旧实现一致的两处"粘连"是原样，不是笔误)：
        // @' 后第一个换行与 '@ 前最后一个换行都**不进内容** —— 所以 Body1 以 位置。收尾、Body2 以 4. 开头，
        // 拼出来的成品里 "2...位置。3. 绝对不要给" 是**没有换行直接粘连**的，改不得。
        const string Body1 =
            "\n只输出一个 JSON 对象，不要 markdown 代码块、不要任何解释文字：\n" +
            "{\"blocking\": true 或 false, \"what\": \"不超过20字，说明挡住的是什么\", \"clicks\": [{\"x\": 整数, \"y\": 整数, \"why\": \"不超过14字\"}]}\n" +
            "\n" +
            "必须遵守的规则：\n" +
            "1. x、y 是相对这张图片宽、高的千分比(0-1000)，不是像素。换算：像素x = x/1000*图片宽。\n" +
            "2. clicks 最多 3 个，按你的把握从大到小排。只允许给\"关闭/取消/暂不/跳过/知道了/X\"这类**只会把弹窗关掉**的位置。";

        const string Rule3Default =
            "3. 绝对不要给\"领取\"\"前往参与\"\"确定\"\"开始行动\"\"充值\"这类会推进游戏状态的按钮，即使它在画面正中。";

        const string Rule3Allow =
            "3. 这一轮已允许\"领取日常奖励\"：画面里有**明确的**领取/一键领取/邮箱领奖励按钮时可以给出它。\n" +
            "   但\"前往参与\"\"开始行动\"\"确定\"\"充值\"这类会改变游戏进度或花钱的按钮绝对不给，即使它在画面正中。";

        const string Body2 =
            "4. 关闭按钮通常在该弹窗的右上角(常常是一个 X 或小圆圈)。弹窗有多个关闭位时，把最像关闭的那个排第一。\n" +
            "5. 画面上没有挡住流程的弹窗 → blocking 给 false、clicks 给空数组。拿不准也一律给空数组。\n" +
            "6. 不要输出坐标以外的多余字段。";

        // ==================== 启动器场景(sceneId='ark-launcher'，10-11 新增) ====================
        // 由来(2026-10-11 06:00 那班)：端游到版本日会停在启动器的更新页上，要有人点『更新』它才开始拉资源。
        // 旧判定只看"窗口标题出现了没"，而**更新页和主界面的标题都是"明日方舟"** —— 于是"窗口就绪"照样放行、
        // MAA 对着一个没有游戏可连的页面把五个任务全跑错。标题分不开，只有画面分得开，所以这一路必须问模型。
        // 为什么不能复用默认那五条规则：更新页上要点的那个按钮恰好就叫"更新/下载/修复"，正是规则 3 明令禁点的
        // 那一类(它会推进状态) —— 拿默认提示词去问更新页，模型只会回 blocking=false，或干脆不给点位。
        // 输出协议一个字没改(还是 blocking/what/clicks)：parse-vision-verdict、tools\vision\ask.py、
        // 千分比换算全都不动，"这一路语义不同"只体现在话术与 verb 里的相位映射(Live.cs check-ark-launcher)。
        // 默认路径逐字节不许漂移：--visiontest(prompt 组)与 t13 对的是 BuildPrompt 的成品串。
        const string LauncherHead =
            "你是明日方舟端游**启动器/更新器**自动化流程里的\"看屏幕的人\"。给你一张启动器窗口的截图，" +
            "请判断这一页现在能不能开始进游戏：是需要点一下某个按钮，还是正在下载资源，还是已经不用管了。";

        const string LauncherBody =
            "\n只输出一个 JSON 对象，不要 markdown 代码块、不要任何解释文字：\n" +
            "{\"blocking\": true 或 false, \"what\": \"不超过20字，说明这一页在干什么\", \"clicks\": [{\"x\": 整数, \"y\": 整数, \"why\": \"不超过14字\"}]}\n" +
            "\n" +
            "必须遵守的规则：\n" +
            "1. x、y 是相对这张图片宽、高的千分比(0-1000)，不是像素。换算：像素x = x/1000*图片宽。\n" +
            "2. 这一路的 blocking 意思是\"还不能进游戏\"。要点的按钮**只限**这些：更新、立即下载、开始更新、修复、" +
            "检查更新、开始游戏、进入游戏、继续。clicks 最多 3 个，按把握从大到小排，最像\"让更新/进游戏开始起来\"的那个排第一。\n" +
            "3. 画面正在下载/校验/解压资源(有进度条、百分比、速度，或者写着\"正在更新\")→ blocking 给 true，" +
            "但 clicks 必须给空数组：这时候点哪儿都可能把下载打断。\n" +
            "4. 已经在游戏里了(登录完成、主界面、游戏内的 loading)→ blocking 给 false。\n" +
            "5. 绝对不要给：充值、商城、购买、活动/公告弹窗的\"前往参与\"、退出登录、切换服务器、清除缓存。\n" +
            "6. 拿不准 → blocking 给 true、clicks 给空数组(宁可多等一轮，不要放行一个可能挡住自动化的页面)。\n" +
            "7. 不要输出坐标以外的多余字段。";

        // 旧实现：给了 Scene 就**原样用**(裁尾那一步在靶子定义处做的)，只有默认 head 才 .TrimEnd()。
        static string BuildArkPrompt(string scene, bool allowReward)
        {
            string head = string.IsNullOrEmpty(scene) ? ArkHead : scene;
            return head + Body1 + (allowReward ? Rule3Allow : Rule3Default) + Body2;
        }

        internal static string BuildPrompt(string scene, bool allowReward)
        {
            return BuildArkPrompt(scene, allowReward);
        }

        // sceneId 认 "ark-launcher"(不分大小写)，其余一律走默认那一条 —— 包括 sceneId 缺失的老调用方。
        internal static string BuildPrompt(string sceneId, string scene, bool allowReward)
        {
            if (!string.IsNullOrEmpty(sceneId) &&
                string.Equals(sceneId.Trim(), "ark-launcher", StringComparison.OrdinalIgnoreCase))
                return LauncherHead + LauncherBody;
            return BuildArkPrompt(scene, allowReward);
        }

        // PS 的 .TrimEnd()：只裁尾部空白
        static string TrimEnd(string s) { return s.TrimEnd(null); }

        internal static object Prompt(Dictionary<string, object> inp)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Prompt"] = BuildPrompt(Gd.Str(Gd.Get(inp, "sceneId")), Gd.Str(Gd.Get(inp, "scene")), Bool(Gd.Get(inp, "allowReward")));
            return data;
        }

        // ==================== verb: parse-vision-verdict(原 Get-GdVisionVerdictFromText) ====================
        // 从模型回复里"抠"第一个 JSON 对象而不是假设整段就是 JSON(实测会裹 ```json 或前后带一句话)。
        // 括号深度扫描**不处理字符串内的花括号** —— 与旧实现逐字符同构，包括这个已知局限(what 里带 { 时两版一起翻车)。
        // X/Y/Why 恒等于 Clicks 第一个：老代码与 t13 认这三个字段，加字段不破约、改名字才破约。

        static string ExtractFirstJson(string text)
        {
            if (text == null) return null;
            string t = Regex.Replace(text, "```[a-zA-Z]*", "", IC);
            int start = t.IndexOf('{');
            if (start < 0) return null;
            int depth = 0;
            StringBuilder json = new StringBuilder();
            for (int i = start; i < t.Length; i++)
            {
                char ch = t[i];
                if (ch == '{') depth++;
                else if (ch == '}') depth--;
                if (depth < 0) break;
                json.Append(ch);                       // 收尾那个 } 也要留着，否则抠出来的是半个 JSON
                if (depth == 0 && i > start) break;
            }
            return json.Length == 0 ? null : json.ToString();
        }

        internal static object Verdict(Dictionary<string, object> inp)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Found"] = false;
            string text = Gd.Str(Gd.Get(inp, "text"));
            if (text.Trim().Length == 0) return data;         // IsNullOrWhiteSpace
            string json = ExtractFirstJson(text);
            if (json == null) return data;
            object parsed;
            try { parsed = GdJson.Deserialize(json); }
            catch { return data; }
            Dictionary<string, object> o = parsed as Dictionary<string, object>;
            if (o == null) return data;

            // 只留 x/y 真是整数的那几个：{"x":null} 或 y 写成"右上角"要单独丢掉，不能让一个坏点位带走整批好点位
            object[] rawClicks = Gd.List(Gd.Get(o, "clicks"));
            List<object> clicks = new List<object>();
            foreach (object c in rawClicks)
            {
                if (c == null) continue;
                Dictionary<string, object> cm = c as Dictionary<string, object>;
                if (cm == null) continue;
                int cx, cy;
                if (!IntTry(Gd.Get(cm, "x"), out cx)) continue;
                if (!IntTry(Gd.Get(cm, "y"), out cy)) continue;
                Dictionary<string, object> one = new Dictionary<string, object>();
                one["X"] = cx;
                one["Y"] = cy;
                one["Why"] = PsStr(Gd.Get(cm, "why"));
                clicks.Add(one);
            }
            if (clicks.Count > 3) clicks = clicks.GetRange(0, 3);   // Select-Object -First 3

            object first = clicks.Count > 0 ? clicks[0] : null;
            data["Found"] = true;
            data["Blocking"] = Bool(Gd.Get(o, "blocking"));
            data["What"] = PsStr(Gd.Get(o, "what"));
            data["X"] = first == null ? null : ((Dictionary<string, object>)first)["X"];
            data["Y"] = first == null ? null : ((Dictionary<string, object>)first)["Y"];
            data["Why"] = first == null ? "" : ((Dictionary<string, object>)first)["Why"];
            data["Clicks"] = clicks.ToArray();
            return data;
        }

        // ==================== verb: vision-click-point(原 Get-GdRescueClickPoint) ====================
        // 千分比(相对整张截图)→ 后端点击要的那幅图的坐标。ark 一路要减客户区原点(整窗位图含标题栏/边框)，
        // mumu 一路靶子把 ClientLeft/Top 报 0、ClientW/H 报图宽高 → 同一函数算出来正好是设备像素。
        // 越界一律拒绝(宁可这轮不救)；"右下角最后一个像素"也算越界(测试钉过：>= 客户区宽高，不是 >)。

        internal static object ClickPoint(Dictionary<string, object> inp)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Found"] = false;
            object nxRaw = Gd.Get(inp, "nx");
            object nyRaw = Gd.Get(inp, "ny");
            if (nxRaw == null || nyRaw == null) return data;      // 模型没给坐标
            int nx, ny;
            if (!IntTry(nxRaw, out nx)) return data;
            if (!IntTry(nyRaw, out ny)) return data;
            int max = IntOf(Gd.Get(inp, "max"), 1000);
            if (nx < 0 || nx > max || ny < 0 || ny > max) return data;

            int imgW = IntOf(Gd.Get(inp, "imgW"), 0);
            int imgH = IntOf(Gd.Get(inp, "imgH"), 0);
            int clientLeft = IntOf(Gd.Get(inp, "clientLeft"), 0);
            int clientTop = IntOf(Gd.Get(inp, "clientTop"), 0);
            int clientW = IntOf(Gd.Get(inp, "clientW"), 0);
            int clientH = IntOf(Gd.Get(inp, "clientH"), 0);

            // [int][math]::Round($nx / $Max * $ImgW)：除法是浮点，Round 取偶，再转回整数
            int px = Convert.ToInt32(Math.Round((double)nx / max * imgW), CultureInfo.InvariantCulture);
            int py = Convert.ToInt32(Math.Round((double)ny / max * imgH), CultureInfo.InvariantCulture);
            int cx = px - clientLeft;
            int cy = py - clientTop;
            if (cx < 0 || cy < 0) return data;
            if (cx >= clientW || cy >= clientH) return data;

            data["Found"] = true;
            data["ClientX"] = cx;
            data["ClientY"] = cy;
            data["BitmapX"] = px;
            data["BitmapY"] = py;
            data["NormX"] = nx;
            data["NormY"] = ny;
            data["Why"] = Gd.Str(Gd.Get(inp, "why"));
            return data;
        }

        // ==================== verb: resolve-vision-endpoint(原 Resolve-GdVisionEndpoint) ====================
        // 控制台复制的多是"基础地址"(/v1 结尾)，POST 要打到 /chat/completions；两种写法给同一个结果，
        // 免得玩家对着 README 发愣：末尾少一截就 404，而那看起来像"模型不可用"。

        internal static object Endpoint(Dictionary<string, object> inp)
        {
            string b = Gd.Str(Gd.Get(inp, "base"));
            b = b.Trim();
            b = b.TrimEnd('/');
            if (b.Length == 0) b = "https://dashscope.aliyuncs.com/compatible-mode/v1";
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Url"] = Regex.IsMatch(b, "/chat/completions$", IC) ? b : b + "/chat/completions";
            return data;
        }

        // ==================== verb: shot-changed(原 Test-GdShotChanged) ====================
        // 点完到底有没有真变：比两张图的 16x9 粗亮度签名，数"变了多少格"。
        // 为什么不用文件哈希：端游背景有噪点动画，同一画面两次截图哈希都不一样(2026-09-24 就是这么误判过一次)。
        // 阈值按端游实测噪声定：噪点顶多抖 1~2 档，关掉一个弹窗必然动到一整片。

        internal static object ShotChanged(Dictionary<string, object> inp)
        {
            string before = Gd.Str(Gd.Get(inp, "beforeSig"));
            string after = Gd.Str(Gd.Get(inp, "afterSig"));
            int minCells = IntOf(Gd.Get(inp, "minCells"), 2);
            int minDelta = IntOf(Gd.Get(inp, "minDelta"), 3);
            Dictionary<string, object> data = new Dictionary<string, object>();
            if (before.Length == 0 || after.Length == 0)
            {
                data["Known"] = false;
                data["Changed"] = false;
                data["Cells"] = -1;
                data["Detail"] = "没有画面签名可比对(截图那一步就没成)";
                return data;
            }
            string[] sa = Regex.Split(before, ",");
            string[] sb = Regex.Split(after, ",");
            if (sa.Length != sb.Length)
            {
                data["Known"] = false;
                data["Changed"] = false;
                data["Cells"] = -1;
                data["Detail"] = "两次画面尺寸不同(" + sa.Length + " 格 vs " + sb.Length + " 格)";
                return data;
            }
            int diff = 0;
            for (int i = 0; i < sa.Length; i++)
            {
                int a = IntOf(sa[i], 0);
                int b = IntOf(sb[i], 0);
                if (Math.Abs(a - b) >= minDelta) diff++;
            }
            data["Known"] = true;
            data["Changed"] = diff >= minCells;
            data["Cells"] = diff;
            data["Detail"] = "画面变了 " + diff + " 格(判据：≥" + minCells + " 格、每格 ≥" + minDelta + " 档)";
            return data;
        }

        // ==================== verb: parse-vision-reply(原 Invoke-GdVisionRaw 的 stdout 回扫段) ====================
        // ask.py 保证 stdout 只有一个 JSON 对象；万一解释器往前面打了别的(版本警告、site 报错)，
        // 从前往后取第一行"以 { 开头且真能解析"的就够了 —— 别整段 ConvertFrom-Json，一句警告就能把兜底弄挂。

        internal static object Reply(Dictionary<string, object> inp)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Found"] = false;
            string outText = Gd.Str(Gd.Get(inp, "out"));
            foreach (string line in Regex.Split(outText, @"\r?\n"))
            {
                if (!Regex.IsMatch(line, @"^\s*\{")) continue;
                object parsed;
                try { parsed = GdJson.Deserialize(line); }
                catch { continue; }
                if (parsed == null) continue;
                data["Found"] = true;
                data["Parsed"] = parsed;
                break;
            }
            return data;
        }

        // ==================== verb: rescue-entry(原 Add-GdRescueRecord 的字段合成段) ====================
        // 只出"这一笔"的七个字段：target 空时回退到结果自带的(调用方没写 -Target 时用靶子名自报)，
        // detail 里的换行压平成 ' | '(账本是给人翻的，不是日志归档)。时间戳由 PS 壳给 —— 保持确定性、
        // 对拍两侧才比得出规则而不是时钟。读写盘与裁剪留壳(Test-Path 那句还被 t16 钉着)。

        internal static object Entry(Dictionary<string, object> inp)
        {
            string target = Gd.Str(Gd.Get(inp, "target"));
            string fallback = Gd.Str(Gd.Get(inp, "fallbackTarget"));
            string detail = Gd.Str(Gd.Get(inp, "detail"));
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Time"] = Gd.Str(Gd.Get(inp, "time"));
            data["Game"] = Gd.Str(Gd.Get(inp, "game"));
            data["Target"] = target.Length > 0 ? target : fallback;
            data["Tag"] = Gd.Str(Gd.Get(inp, "tag"));
            data["Tried"] = Bool(Gd.Get(inp, "tried"));
            data["Done"] = Bool(Gd.Get(inp, "done"));
            data["Detail"] = Regex.Replace(detail, @"\r?\n", " | ");
            return data;
        }

        // ==================== verb: compose-ark-desktop-state(原 Get-GdArkDesktopState 的文案拼装段) ====================
        // "这一轮的点击环境长什么样"的一句话现场快照 —— 无人值守那几轮(06:00)失败得一模一样，
        // 事后只能靠这行分辨"弹窗挡着 / 抢不到前台 / 屏保桌面接管"。判据来源(桌面名、前台归属、
        // 窗口状态、光标位置)由壳用 P/Invoke 现场读完送进来，这里只负责怎么说。
        // 桌面名读不到时按"不知道"处理，别把未知说成"锁屏了"(OpenInputDesktop 会被个别安全策略挡)。

        internal static object DesktopState(Dictionary<string, object> inp)
        {
            string desk = Gd.Str(Gd.Get(inp, "desk"));
            bool hwndFound = Bool(Gd.Get(inp, "hwndFound"));
            bool onFg = Bool(Gd.Get(inp, "onFg"));
            string fgProcess = Gd.Str(Gd.Get(inp, "fgProcess"));
            bool iconic = Bool(Gd.Get(inp, "iconic"));
            bool visible = Bool(Gd.Get(inp, "visible"));
            string rectText = Gd.Str(Gd.Get(inp, "rectText"));
            string cursor = Gd.Str(Gd.Get(inp, "cursor"));

            // PS 的 -ne 比较字符串是忽略大小写的
            object locked = null;
            if (desk.Length > 0) locked = !string.Equals(desk, "Default", StringComparison.OrdinalIgnoreCase);

            List<string> bits = new List<string>();
            if (desk.Length == 0) bits.Add("输入桌面=读不到(无法判断是否锁屏)");
            else if ((bool)locked) bits.Add("输入桌面=" + desk + "(会话不在正常桌面，鼠标键盘进不来)");
            else bits.Add("输入桌面=Default(未锁屏)");

            if (!hwndFound)
            {
                bits.Add("端游窗口没找到");
            }
            else
            {
                if (onFg) bits.Add("端游在前台=是");
                else
                {
                    string who = fgProcess.Length > 0 ? fgProcess : "未知窗口";
                    bits.Add("端游在前台=否(前台是 " + who + ")");
                }
                // 窗口自己这一侧的状态：锁屏时这几项往往还是"可见 + 正常矩形"，
                // 所以它是用来**排除**"窗口没了/最小化了"的
                List<string> st = new List<string>();
                if (iconic) st.Add("最小化");
                if (!visible) st.Add("不可见");
                if (rectText.Length > 0) st.Add(rectText);
                if (st.Count > 0) bits.Add("端游窗口状态=" + string.Join("/", st.ToArray()));
            }
            if (cursor.Length > 0) bits.Add("光标在 " + cursor);

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Locked"] = locked;
            data["LockedKnown"] = desk.Length > 0;
            data["Detail"] = string.Join("；", bits.ToArray());
            return data;
        }
    }
}
