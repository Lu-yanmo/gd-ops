// tools/panel-cs/Processes.cs —— 批次 3：外部进程层的"纯解析/纯规划"本体，从 lib\maa.ps1 / lib\m9a.ps1 / lib\mumu.ps1 逐条搬来。
//
// 单一事实源纪律：自本批起 PS 侧的 Get-MaaAsstStuckState / Get-MaaAsstStuckHint / Set-MaaDailyConfig 的规则段、
// Get-M9ALaunchArgs(⚠ 10-26 甲档第五刀刀②：那枚 PS 壳已出仓，这一份就是唯一实现)／Read-GdM9ALine／Resolve-M9AStatus、Get-GdMuMuState 的 JSON 解读、Get-GdMuMuVmList 的(⚠ 11-06 刀⑮(a)：那枚壳随 lib\detect.ps1 整件出仓，起 MuMuManager 仍留 PS 侧)
// 属性表转换、Connect-GdAdb / Get-Gd1999Pid 的输出判定 —— 全部只剩薄转发壳，规则只写 C# 这一份。
// 文案必须与 PS 原话逐字符一致：期望串逐字钉在 Processes.SelfTest.cs(--proctest，12 刀起接替已退役的
// tools\probe-process-golden.ps1 等值门)，
// 有差异回修这里，禁止改期望、禁止放宽比对(docs\ps-migration-batch0.md §7)。
//
// 切分口径(与原"批次 3=外部进程层"的有意偏差，落地实录写进 docs)：
//   * 起进程/等进程/点窗口的动作 **不进这里**：Invoke-MaaRun / Invoke-M9ARun 的监控循环、Register-M9AEvent 的
//     状态累计、Start/Stop 系、adb connect 调用本身、MuMuManager 调用本身仍留 PS —— 那是编排(批次 5/6)的地盘。
//   * 这里只收"给一段文本/一棵 JSON/一份现场值，出结论"的纯函数：verb 一律不启动外部程序、除 plan-maa-queue 的
//     NewConfig 回传外不写任何文件。
//   * "无结论/还在跑"编码成 Found:false(信封 data 永不为 null)，PS 壳翻译回 $null —— 与批次 2 探测同一协议。
//
// 数字读回是 Int32/Decimal 混发(批次 0 钉死)——这里一律 Gd.Str/Lng/Int/Truthy 与 Convert.*，不裸强转。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class Processes
    {
        // ---------- PowerShell 运算符口径的小零件(全文件共用) ----------
        // -match / -replace / -notmatch 默认大小写不敏感；String.Contains(-and $Line.Contains) 是序数敏感。
        static readonly RegexOptions IC = RegexOptions.IgnoreCase;

        static Match Rx(string input, string pattern)
        {
            return Regex.Match(input ?? "", pattern, IC);
        }

        static string RxRepl(string input, string pattern, string repl)
        {
            return Regex.Replace(input ?? "", pattern, repl, IC);
        }

        // PS 真值口径下的 [bool] 强转：null/空串/0 为假，"true"/"false" 按字面，其余字符串非空即真
        // (注：旧 PS 对 [bool]"abc" 是直接抛转换错的，这里取"非空即真"是有意的行为差 —— 只在喂进非规范
        //  MAA 配置时可达，golden 不铺这种场景；生产上 MAA 写的都是 true/false)。
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
                return true;
            }
            if (v is IDictionary<string, object>) return ((IDictionary<string, object>)v).Count > 0;
            if (v is object[]) return ((object[])v).Length > 0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture) != 0.0; } catch { return true; }
        }

        static double Dbl(object v)
        {
            if (v == null) return 0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return 0; }
        }

        // PS 的 -ne：左侧是字符串就按"忽略大小写的串比较"(右侧先 [string] 化)；左侧是 bool/数字按同口径拉齐；
        // 左侧 $null 时非右 null 即不等。Set-MaaDailyConfig 那圈 "$q.$k -ne $want[$k]" 全靠它。
        static bool PsNe(object a, object b)
        {
            if (a == null) return b != null;
            if (a is string) return !string.Equals((string)a, Gd.Str(b), StringComparison.OrdinalIgnoreCase);
            if (a is bool) return (bool)a != Bool(b);
            double da;
            try { da = Convert.ToDouble(a, CultureInfo.InvariantCulture); } catch { return !Equals(a, b); }
            double db;
            try { db = Convert.ToDouble(b, CultureInfo.InvariantCulture); } catch { db = Bool(b) ? 1 : 0; }
            return da != db;
        }

        // 字符串数组的 -in(忽略大小写)
        static bool InArr(string v, object[] list)
        {
            foreach (object o in list)
                if (string.Equals(v, Gd.Str(o), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string[] StrArr(object v)
        {
            List<string> outL = new List<string>();
            foreach (object o in Gd.List(v)) outL.Add(Gd.Str(o));
            return outL.ToArray();
        }

        // [int] 强转：向偶数取整，与 Convert.ToInt32 同口径(批次 0 实测坑的延伸)
        static int IntCast(double d) { return Convert.ToInt32(d, CultureInfo.InvariantCulture); }

        // [math]::Round(x, 1) 后 "$x" 的显示：两侧同为 .NET double.ToString(不变文化)
        static string Round1(double d) { return Math.Round(d, 1).ToString(CultureInfo.InvariantCulture); }

        static string FirstJoin(string[] arr, int take, string sep)
        {
            List<string> bits = new List<string>();
            for (int i = 0; i < arr.Length && i < take; i++) bits.Add(arr[i]);
            return string.Join(sep, bits.ToArray());
        }

        // ==================== verb: parse-asst-state(原 Get-MaaAsstStuckState) ====================
        // 逐行扫 asst.log 片段：任务名/重试次数取"最后一次出现的"，点击/注入按行计数。纯字符串、不碰文件。
        // 卡住时整段日志都在同一个任务上循环，所以不用配对、末次即当前值。
        // ⚠ inject_input_event **不是**"点击通道活没活"的指标 —— 2026-09-25 早按动作归类数过整份 asst.log：
        //   全部 2001 次注入都来自同一个子任务 SwipeToTheLeft(action=Swipe，基建向左滑那一屏)，
        //   而 7640 次 ClickSelf / 719 次 ClickRect 一条注入都没有 —— 点击从头到尾都走配置里的发消息方式。
        //   这个计数只说明"这一轮有没有滑过基建"，别拿它推断点得动还是点不动。

        internal sealed class AsstFacts
        {
            public string Task = "";
            public int Exec;
            public int Clicks;
            public int Inject;
            public string Click = "";      // parse-asst-hint 还要"最后点到哪"的坐标串
        }

        static AsstFacts ScanAsst(string text)
        {
            AsstFacts r = new AsstFacts();
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return r;   // IsNullOrWhiteSpace
            foreach (string line in Regex.Split(text, @"\r?\n"))
            {
                Match m = Rx(line, "\"cur_task\":\"([^\"]+)\"");
                if (m.Success) r.Task = m.Groups[1].Value;
                m = Rx(line, "\"exec_times\":(\\d+)");
                if (m.Success) r.Exec = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (Rx(line, "Win32Controller click:\\s*\\(").Success) r.Clicks++;
                if (Rx(line, "Win32Controller::inject_input_event \\| enter").Success) r.Inject++;
                m = Rx(line, "Win32Controller click:\\s*\\(\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\)");
                if (m.Success) r.Click = m.Groups[1].Value + "," + m.Groups[2].Value;
            }
            return r;
        }

        public static object AsstState(Dictionary<string, object> inp)
        {
            AsstFacts f = ScanAsst(Gd.Str(Gd.Get(inp, "text")));
            return Gd.Out("Task", f.Task, "Exec", f.Exec, "Clicks", f.Clicks, "Inject", f.Inject);
        }

        // ==================== verb: parse-asst-hint(原 Get-MaaAsstStuckHint) ====================
        // 为什么要单独读 asst.log：MAA 卡住时 gui.log 可以整整 22 分钟一个字都不写，asst.log 却一直在刷
        //   (2026-09-24 18:00 / 09-25 06:00 两轮实测：StartUp@GameStart 的 exec_times 涨到 311，
        //    每 4 秒往同一个矩形点一下、模板分数始终 0.99 → 点击全数落空)。
        // 这句话先后被写过两种"原因"都不成立，别原样加回来：
        //   一"splash 上叠了个中秋窗口"(视觉兜底拍到的是登录界面、没有窗口)；
        //   二"MAA 抢不到前台退化成 PostMessage 而端游不吃"(inject 数错点得出的错结论，见上面 parse-asst-state)。
        // 所以现在只说被证据撑住的部分：反复点同一处、界面不推进，两种可能都留着。
        public static object AsstHint(Dictionary<string, object> inp)
        {
            string text = Gd.Str(Gd.Get(inp, "text"));
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return Gd.Out("Hint", "");
            AsstFacts f = ScanAsst(text);
            if (f.Task.Length == 0) return Gd.Out("Hint", "");   // -not $task → ''
            List<string> bits = new List<string>();
            bits.Add("MAA 内核其实还在跑");
            if (f.Exec >= 3 && f.Click.Length > 0)
            {
                bits.Add("卡在子任务 " + f.Task + "，已重复 " + f.Exec.ToString(CultureInfo.InvariantCulture) + " 次(最后点到 " + f.Click + ")");
                bits.Add("同一处反复点击而界面不动 → 端游没把这些点击吃进去：要么真有个 MAA 不认识的窗口挡着，要么这一轮窗口不在前台/会话被锁(有人在机器前时，同样的发消息点击是点得动的)");
            }
            else if (f.Exec >= 3)
            {
                bits.Add("卡在子任务 " + f.Task + "，已重复 " + f.Exec.ToString(CultureInfo.InvariantCulture) + " 次");
            }
            else
            {
                bits.Add("最后停在子任务 " + f.Task);
            }
            return Gd.Out("Hint", string.Join("，", bits.ToArray()));
        }

        // ==================== verb: plan-maa-queue(原 Set-MaaDailyConfig 的规则段) ====================
        // 壳负责：进程在跑/配置不存在的现场检查、读文件、写文件(Backup-GdLog/Write-GdJson)、回读核对、日志染色。
        // 这里负责：按判定结果算出每一处 IsEnable/StagePlan/farm 参数改动 + 返回改好的树(NewConfig)与待说话术(Logs)。
        // 关卡校验(Test-GdStages 的查表/合计)在 exe 内直接复用批次 1 的 CheckStages 本体 —— 不再中转一次 verb。
        public static object PlanMaaQueue(Dictionary<string, object> inp)
        {
            object cfg = Gd.Get(inp, "config");
            // 只认"键不存在/不是对象"为缺 Default：旧 PS 是 `if (-not $cfg.Configurations.Default)`，
            // 而 ConvertFrom-Json 给空 {} 的是 PSCustomObject —— 它在 PS 里恒真(Gd.Truthy 对空字典为假，别套用)。
            Dictionary<string, object> root = Gd.Map(Gd.Get(Gd.Get(cfg, "Configurations"), "Default"));
            if (root == null) throw new GdRuleException("MAA 配置里找不到 Configurations.Default，请检查 MAA 版本/实例名");

            string wantFinish = Gd.Str(Gd.Get(inp, "wantFinish"));
            bool alsoMain = Gd.Truthy(Gd.Get(inp, "alsoEnableMainFight"));
            bool launchFromHelper = Gd.Truthy(Gd.Get(inp, "launchArkFromHelper"));
            object[] keepOn = Gd.List(Gd.Get(inp, "keepOn"));
            object[] turnOff = Gd.List(Gd.Get(inp, "turnOff"));
            Dictionary<string, object> table = Gd.Map(Gd.Get(inp, "table")) ?? new Dictionary<string, object>();

            string mode = Gd.Str(Gd.Get(inp, "mode"));
            bool clearSanity = Gd.Truthy(Gd.Get(inp, "clearSanity"));

            List<string> changes = new List<string>();

            // RunDirectly / StartGame 两条全局开关(-ne $true / -ne $false 的 PS 语义 → PsNe)
            object startup = Gd.Get(Gd.Get(root, "Gui"), "StartUpSettings");
            if (PsNe(Gd.Get(startup, "RunDirectly"), true))
            {
                SetIn(startup, "RunDirectly", true);
                changes.Add("StartUpSettings.RunDirectly = true(启动后自动开始)");
            }
            object runtimeNode = Gd.Get(Gd.Get(root, "Gui"), "RuntimeSettings");
            if (launchFromHelper && PsNe(Gd.Get(runtimeNode, "StartGame"), false))
            {
                SetIn(runtimeNode, "StartGame", false);
                changes.Add("RuntimeSettings.StartGame = false(端游改由日常脚本先拉起来，MAA 没存端游路径)");
            }

            // 理智计划拆串/校验(原话顺序：先"为空"拒，再查表；长草+未知关卡=拒，活动+未知关卡=照样写但带 WARN)
            List<string> stageList = new List<string>();
            foreach (string s in Regex.Split(Gd.Str(Gd.Get(inp, "stage")), @"[,，、;；\s]+"))
                if (s.Length > 0) stageList.Add(s);
            if (stageList.Count == 0) throw new GdRuleException("理智计划为空：检查 settings.ark.defaultStage / planByWeekday");
            string stageText = string.Join(",", stageList.ToArray());
            Dictionary<string, object> chk = (Dictionary<string, object>)Decisions.CheckStages(
                Gd.Out("table", table, "stageText", stageText));
            List<Dictionary<string, object>> logs = new List<Dictionary<string, object>>();
            object[] bad = Gd.List(chk["Bad"]);
            if (bad.Length > 0)
            {
                if (string.Equals(mode, "idle", StringComparison.OrdinalIgnoreCase))
                    throw new GdRuleException("长草计划里的关卡代号不被 MAA 识别：" + JoinStr(bad, ", ") + " → 请改 settings.json 的 ark 计划");
                logs.Add(Gd.Out("Level", "WARN", "Text", "活动关卡 " + JoinStr(bad, ", ") + " 还不在 MAA 本地关卡表(资源可能没更到)，仍按接口结果写入"));
            }
            logs.Add(Gd.Out("Level", "INFO", "Text", "理智计划 " + stageText + "(" + Gd.Str(chk["Note"]) + ")"));

            // TaskQueue：@($root.TaskQueue) —— 键缺失/JSON null 在 PS 里是 @($null)(长度 1)，只有 [] 才是 0
            object tq = Gd.Get(root, "TaskQueue");
            object[] queue = tq == null ? new object[] { null } : Gd.List(tq);
            if (queue.Length == 0) throw new GdRuleException("MAA 配置里的 TaskQueue 为空或结构不认识(预期为数组)");

            bool farmActive = Gd.Truthy(Gd.Get(inp, "farmActive"));
            string farmMode = Gd.Str(Gd.Get(inp, "farmMode"));
            bool farmDedicated = farmActive && string.Equals(farmMode, "dedicated", StringComparison.OrdinalIgnoreCase);
            bool farmAppend = farmActive && string.Equals(farmMode, "append", StringComparison.OrdinalIgnoreCase);

            List<KeyValuePair<string, bool>> intent = new List<KeyValuePair<string, bool>>();
            List<string> expected = new List<string>();
            foreach (object qObj in queue)
            {
                Dictionary<string, object> q = Gd.Map(qObj);
                if (q == null) continue;                                  // 原话 if (-not $q){continue}(null 项与非对象项一并跳过)
                string type = Gd.Str(Gd.Get(q, "TaskType"));
                string name = Gd.Str(Gd.Get(q, "Name"));
                object targetRaw = Gd.Get(q, "IsEnable");

                object target = targetRaw;
                if (InArr(type, turnOff)) target = false;
                else if (string.Equals(type, "Fight", StringComparison.OrdinalIgnoreCase))
                {
                    if (!clearSanity) target = false;
                    else if (string.Equals(name, wantFinish, StringComparison.OrdinalIgnoreCase)) target = true;
                    else if (alsoMain && !Rx(name, "剩余").Success) target = true;
                    else target = false;
                }
                else if (InArr(type, keepOn)) target = true;
                bool t = Bool(target);

                if (Bool(targetRaw) != t)
                {
                    changes.Add(name + " → IsEnable=" + Gd.Str(t));
                    q["IsEnable"] = t;
                }

                if (string.Equals(type, "Fight", StringComparison.OrdinalIgnoreCase) && t)
                {
                    if (JoinStr(Gd.List(Gd.Get(q, "StagePlan")), ",") != stageText)
                    {
                        changes.Add(name + " → StagePlan = " + stageText);
                        q["StagePlan"] = stageList.ToArray();
                    }
                    if (farmDedicated)
                    {
                        ApplyFarm(q, name, changes, "材料补刷专用轮", true,
                            Gd.Str(Gd.Get(inp, "farmItemId")), Gd.Int(Gd.Get(inp, "farmGap"), 0), false,
                            Gd.Int(Gd.Get(inp, "farmTimes"), 0));
                    }
                    else if (farmAppend)
                    {
                        ApplyFarm(q, name, changes, "材料补刷·目标库存", true,
                            Gd.Str(Gd.Get(inp, "farmItemId")), Gd.Int(Gd.Get(inp, "farmTarget"), 0), true,
                            Gd.Int(Gd.Get(inp, "farmTimes"), 0));
                    }
                    else
                    {
                        // 默认档护甲(10-04 实锤改口径)：asst.log 撤令后 14 枚 append_task Fight 恰好分成两堆 ——
                        // 补刷协议轮(EnableTargetDrop/EnableTimesLimit 全开)内核收到真 stage，掉旗轮全部 stage:""。
                        // 空 stage 时 Fight 走 Fight@GoLastBattle"回到上次作战"，把游戏记忆的关卡重放到理智耗尽
                        // ——撤令后 09-30~10-02 六轮天天实打 PR-D-2 芯片，写进 StagePlan 的 CE-5 从未进过内核。
                        // 所以无补刷时不再"全掉旗"，改挂一组永不触顶的护甲借用同一条递关卡的路：
                        //   DropId=4001(龙门币)/DropCount=999999 —— 轮内计数口径，单轮最多两三万也碰不到顶；
                        //   TimesLimit=20 —— 数量级护栏，正常情况下理智先停；
                        //   IsInventoryTarget=false —— 仓库快照不认识，防止被陈旧库存当场掐停。
                        // 停手权仍在理智，语义等价"剩余理智打空默认关"，但 GUI 会把默认关真递给内核并打赢
                        // 一场，把"上次作战"覆盖掉。顺带顶掉 09-28 补刷时代残留的 DropId/DropCount/IsInventoryTarget。
                        ApplyFarm(q, name, changes, "默认档护甲", true, "4001", 999999, false, 20);
                    }
                }
                intent.Add(new KeyValuePair<string, bool>(name, t));
                if (t) expected.Add(type);
            }

            // Select-Object -Unique：实测(5.1 与 7 各跑 @('fight','StartUp','Fight'))是**大小写敏感**去重、保首现 ——
            // MAA 配置里 fight/Fight 两种写法都真实存在，合并会把"剩余理智"链从期望里吞掉。
            List<string> chains = new List<string>();
            foreach (string e in expected)
            {
                bool dup = false;
                foreach (string c in chains) if (string.Equals(c, e, StringComparison.Ordinal)) { dup = true; break; }
                if (!dup) chains.Add(e);
            }

            List<Dictionary<string, object>> intentOut = new List<Dictionary<string, object>>();
            foreach (KeyValuePair<string, bool> kv in intent) intentOut.Add(Gd.Out("Name", kv.Key, "Enable", kv.Value));
            return Gd.Out(
                "Changes", changes.ToArray(),
                "Intent", intentOut.ToArray(),
                "ExpectedChains", chains.ToArray(),
                "Stage", stageText,
                "Logs", logs.ToArray(),
                "NewConfig", cfg);
        }

        // 材料补刷两档共用的九项比对(顺序即协议：dedicated/append 只差 DropCount 口径与 IsInventoryTarget)。
        // 两档分野(原 Set-MaaDailyConfig 头注，09-25 定口径)：
        //   append(日常·需求优先) —— farm 关已由 ark-daily 排在 StagePlan 最前，MAA 侧打开
        //     「指定材料→目标库存」(IsInventoryTarget=true，官方语义"参考仓库识别保存的库存数据，只补到设定库存为止")：
        //     DropCount=目标总数(绝对值)，凑够当场停整条 Fight 链；TimesLimit 仍是单轮护栏。
        //   dedicated(-FarmArk 专用轮) —— StagePlan 整条就是 farm 关，维持原双保险：
        //     DropCount=缺口(按本次出击掉落计数) + TimesLimit；IsInventoryTarget 故意 false，
        //     专用轮语义就是"这一轮刷出缺口数"，不去掺仓库快照。
        // 仓库快照可能比真实库存旧(MAA 只在同步仓库时刷新)——判缺口闸门在我们账本这边先过一道，
        // 快照旧最多是这一轮多刷几场(有 TimesLimit 封顶)，不会跨轮失控。
        static void ApplyFarm(Dictionary<string, object> q, string name, List<string> changes, string tag,
                              bool enableTargetDrop, string dropId, int dropCount, bool inventoryTarget, int timesLimit)
        {
            Dictionary<string, object> want = new Dictionary<string, object>();
            want["EnableTargetDrop"] = enableTargetDrop;
            want["DropId"] = dropId;
            want["DropCount"] = dropCount;
            want["IsInventoryTarget"] = inventoryTarget;
            want["EnableTimesLimit"] = true;
            want["TimesLimit"] = timesLimit;
            want["UseMedicine"] = false;
            want["UseStone"] = false;
            want["UseExpiringMedicine"] = false;
            foreach (KeyValuePair<string, object> kv in want)
            {
                if (PsNe(Gd.Get(q, kv.Key), kv.Value))
                {
                    changes.Add(name + " → " + kv.Key + " = " + Gd.Str(kv.Value) + "(" + tag + ")");
                    q[kv.Key] = kv.Value;
                }
            }
        }

        // 往字典节点上写键(键不存在就补上 —— 旧 PS `$startup.RunDirectly = $true` 对 PSCustomObject 也是补属性)；
        // 父节点缺失(连 Gui 都没有的残缺配置)时静默跳过：旧实现那一下是引擎本地化的"给 null 设属性"崩，
        // 本来就双引擎不等形、golden 不铺这种场景。
        static void SetIn(object node, string key, object value)
        {
            Dictionary<string, object> d = Gd.Map(node);
            if (d != null) d[key] = value;
        }

        static string JoinStr(object[] arr, string sep)
        {
            List<string> bits = new List<string>();
            foreach (object o in arr) bits.Add(Gd.Str(o));
            return string.Join(sep, bits.ToArray());
        }

        // ==================== verb: m9a-launch-args(原 Get-M9ALaunchArgs) ====================
        // 一律长参数：短参数 -a 会被 M9A 记成 key "a"、自动开始判定匹配不上(09-23 实测，教训钉死在文案里)。
        public static object M9aLaunchArgs(Dictionary<string, object> inp)
        {
            if (!Gd.Truthy(Gd.Get(inp, "cliArgs"))) return Gd.Out("Args", new string[0]);
            string value = string.Equals(Gd.Str(Gd.Get(inp, "mode")), "name", StringComparison.OrdinalIgnoreCase)
                ? Gd.Str(Gd.Get(inp, "name")) : Gd.Str(Gd.Get(inp, "id"));
            List<string> outArgs = new List<string>();
            outArgs.Add("--instance"); outArgs.Add(value);
            outArgs.Add("--autostart"); outArgs.Add("--quit-after-run");
            if (Gd.Truthy(Gd.Get(inp, "forceStart"))) outArgs.Add("--force-start");
            return Gd.Out("Args", outArgs.ToArray());
        }

        // ==================== verb: parse-m9a-lines(原 Read-GdM9ALine，整批文本进、事件数组出) ====================
        // 单行壳 Read-GdM9ALine 与批量壳 Read-M9ALogEvents(⚠ 10-26 第五刀刀② 已出仓，批量那一路的家只剩核内 m9a_read_log)共用这一个本体：批量场景绝不逐行起进程。
        // 解析顺序敏感(res-latest 必须排在 noise 前)；认不出的行不产出事件，$null 语义由"事件条数"承载。
        public static object M9aLines(Dictionary<string, object> inp)
        {
            string text = Gd.Str(Gd.Get(inp, "text"));
            string instanceId = Gd.Str(Gd.Get(inp, "instanceId"));
            List<Dictionary<string, object>> events = new List<Dictionary<string, object>>();
            foreach (string line in Regex.Split(text, @"\r?\n"))
            {
                if (line.Trim().Length == 0) continue;                    // 壳原话 Where-Object { $_.Trim() }
                Dictionary<string, object> ev = ParseM9aLine(line, instanceId);
                if (ev != null) events.Add(ev);
            }
            return Gd.Out("Events", events.ToArray());
        }

        static Dictionary<string, object> ParseM9aLine(string line, string instanceId)
        {
            // 实测语义(对照 08-30 手工成功日志 + 09-23 命令行真跑日志，别改认法)：
            //   用户操作：启动任务 = 开始命令真的下发了(--autostart 走的就是这条路；短参数 -a 实测不触发)
            //   启动任务完成 / 任务已全部完成 = 两种模式两种形态：
            //     · 手工界面跑：结束打「任务已全部完成！」，进程留着
            //     · 命令行带 --quit-after-run：只打「启动任务完成」+「停止前状态：SUCCEEDED」紧接着「应用已关闭」，
            //       根本没有「任务已全部完成！」(实测 09-23 17:43) —— 所以这里只给中性的 'finish'，
            //       "链路完成还是只连上了"由消费端按上下文判断(Register-M9AEvent)。
            //   09-25 19:24 之后补的三条：任务失败：<中文名>(和「任务运行失败」差一个字，旧解析器漏认)、
            //     当前/最新资源版本(「最新资源版本」含「新资源版本」子串，必须排在 noise 前)、
            //     初始化控制器失败/连接失败(连不上模拟器的硬信号，实测 19:29 AdbSerial 为空、按指纹匹配设备数量=0)。
            Match m;
            if (instanceId.Length > 0 && Rx(line, @"\[inst=").Success && !line.Contains("/" + instanceId + "]")) return null;
            if ((m = Rx(line, "开始任务：(.+?)\\s*$")).Success) return Gd.Out("Kind", "task", "Text", m.Groups[1].Value.Trim());
            if ((m = Rx(line, "准备执行任务队列：任务数量=(\\d+)")).Success) return Gd.Out("Kind", "queue-prepared", "Text", m.Groups[1].Value);
            if (Rx(line, "用户操作：启动任务").Success) return Gd.Out("Kind", "ui-start", "Text", "");
            if (Rx(line, "队列任务开始").Success) return Gd.Out("Kind", "chain-start", "Text", "");
            if (Rx(line, "任务已全部完成").Success) return Gd.Out("Kind", "chain-done", "Text", "");
            if (Rx(line, "启动任务完成").Success) return Gd.Out("Kind", "finish", "Text", "");
            if (Rx(line, "启动任务开始").Success) return Gd.Out("Kind", "connect-start", "Text", "");
            if (Rx(line, "任务运行失败|停止前状态：FAILED").Success)
                return Gd.Out("Kind", "fail", "Text", RxRepl(RxRepl(line, "^.*?op=\\w+\\]\\s*", ""), ">>>\\s*任务\\s*", "").Trim());
            if ((m = Rx(line, "任务失败：(.+?)\\s*$")).Success) return Gd.Out("Kind", "task-fail", "Text", m.Groups[1].Value.Trim());
            if ((m = Rx(line, "当前资源版本：(\\S+)")).Success) return Gd.Out("Kind", "res-current", "Text", m.Groups[1].Value);
            if ((m = Rx(line, "最新资源版本：(\\S+)")).Success) return Gd.Out("Kind", "res-latest", "Text", m.Groups[1].Value);
            if (Rx(line, "初始化控制器失败|连接模拟器时发生错误|按指纹匹配到的设备数量：0|连接任务已在重试耗尽后结束|\\[Record\\]\\s*连接失败").Success)
                return Gd.Out("Kind", "connect-fail", "Text", RxRepl(RxRepl(line, "^(\\[[^\\]]*\\]\\s*)+", ""), "^>>>\\s*任务\\s*", "").Trim());
            if (Rx(line, "截图超时|Agent程序|资源检测|控制器检测|热更新|新资源版本").Success) return Gd.Out("Kind", "noise", "Text", "");
            return null;
        }

        // ==================== verb: resolve-m9a-status(原 Resolve-M9AStatus) ====================
        // "还在跑"回 Found:false(信封 data 永不为 null，$null 语义在壳里翻译回去)。
        // lastLog 不进信封：壳算好 quietSeconds(秒)递进来，停滞判定与"几分钟"文案都吃它。
        // 分支优先级(顺序讲究，动之前先读完)：
        //   0) 第一个任务(启动游戏)就失败，或开始过的任务全失败 = blocked
        //      —— 界面根本没进游戏，后面每条「开始任务」都是空转，重试也没用(实测 09-25 19:24：客户端正在下 1.91G 更新包)
        //   1) 有完成信号且确实跑过任务，或「任务数已达队列声明 + 进程已按 -q 退出」 = ok
        //      (--quit-after-run 让 M9A 在最后一个任务后立刻关闭，日志常来不及写完成行就被读到退出)
        //   2) 有"任务已全部完成"但一条"开始任务"都没有 = empty-queue(实测 08-30 长草/09-23 16:39 这个形态)
        //      —— "队列声明 10 个却一个没开始"别说成队列为空：09-25 19:29 实测是控制器初始化压根没过，
        //        M9A 报的 10 个是它勾选的任务数；一条开始任务都没有时也不算"全失败"(那是 empty-queue/not-started 的形态)
        //   3) 一条认识的事件都没解析出来却已到点/进程没了 = unknown-format(日志文案或实例 id 变了，别按本轮结果下结论)
        //   4) 进程已消失                     = error / not-started(一个任务没跑 = --autostart 没生效 + -q 直接退，快速失败别等)
        //   5) 超时 / 到点没开始 / 日志停滞    = timeout / not-started / stalled
        // updateHint/connectHint 两条"可行动"提示拼在所有非 ok 结论后 —— 09-25 吃了没这两条的亏：
        // 客户端在下 1.91G 更新包、M9A 报了版本差，我们却只说「进程提前退出」然后原样重试一遍。
        public static object M9aStatus(Dictionary<string, object> inp)
        {
            Dictionary<string, object> st = Gd.Map(Gd.Get(inp, "state")) ?? new Dictionary<string, object>();
            double elapsed = Dbl(Gd.Get(inp, "elapsed"));
            double quietSeconds = Dbl(Gd.Get(inp, "quietSeconds"));
            bool processGone = Gd.Truthy(Gd.Get(inp, "processGone"));
            string logName = Gd.Str(Gd.Get(inp, "logName"));
            string instName = Gd.Str(Gd.Get(inp, "instanceName"));
            string instId = Gd.Str(Gd.Get(inp, "instanceId"));
            int startTimeoutMinutes = Gd.Int(Gd.Get(inp, "startTimeoutMinutes"), 0);
            int timeoutMinutes = Gd.Int(Gd.Get(inp, "timeoutMinutes"), 0);
            int stallTimeoutMinutes = Gd.Int(Gd.Get(inp, "stallTimeoutMinutes"), 0);

            string[] tasks = StrArr(Gd.Get(st, "tasks"));
            string progress;
            if (tasks.Length > 0) progress = "已到[" + tasks[tasks.Length - 1] + "]";
            else if (Gd.Truthy(Gd.Get(st, "chainStarted"))) progress = "队列已开始但还没有任务";
            else if (string.Equals(Gd.Str(Gd.Get(st, "connectPhase")), "done", StringComparison.OrdinalIgnoreCase)) progress = "只完成了连接(队列任务从未开始)";
            else if (string.Equals(Gd.Str(Gd.Get(st, "connectPhase")), "started", StringComparison.OrdinalIgnoreCase)) progress = "正在连接模拟器";
            else progress = "尚未开始任何任务";

            object qc = Gd.Get(st, "queueCount");
            bool allQueuedDone = (qc != null) && Gd.Int(qc, 0) > 0 && tasks.Length >= Gd.Int(qc, 0);
            bool seenNothing = Gd.Int(Gd.Get(st, "eventCount"), 0) == 0;
            string[] failedTasks = StrArr(Gd.Get(st, "failedTasks"));
            string resCur = Gd.Str(Gd.Get(st, "resCurrent")), resLat = Gd.Str(Gd.Get(st, "resLatest"));
            string connectFail = Gd.Str(Gd.Get(st, "connectFail"));
            string failed = Gd.Str(Gd.Get(st, "failed"));

            string updateHint = "";
            if (resLat.Length > 0 && resCur.Length > 0 && !string.Equals(resLat, resCur, StringComparison.Ordinal))
                updateHint = "；M9A 报资源/程序落后(当前 " + resCur + " → 最新 " + resLat + ")——这是整包更新，命令行没有更新入口，得在 M9A 界面点『更新』";
            string connectHint = "";
            if (connectFail.Length > 0) connectHint = "；M9A 连不上模拟器(" + connectFail + ")——多半是 MuMu 虚拟机没起来或 adb 设备指纹变了";

            bool firstTaskFailed = failedTasks.Length > 0 && tasks.Length > 0 && Contains(tasks[0], failedTasks);
            bool allStartedFailed = tasks.Length > 0 && failedTasks.Length > 0 && failedTasks.Length >= tasks.Length;

            if (processGone && (firstTaskFailed || allStartedFailed))
            {
                string why = firstTaskFailed
                    ? "第一个任务[" + tasks[0] + "]就失败，后面 " + (tasks.Length - 1).ToString(CultureInfo.InvariantCulture) + " 个都是在没进游戏的情况下空转"
                    : "开始过的 " + tasks.Length.ToString(CultureInfo.InvariantCulture) + " 个任务全部识别失败";
                return Done("blocked",
                    "M9A 有 " + failedTasks.Length.ToString(CultureInfo.InvariantCulture) + " 个任务报『任务失败』(" + why + ")。先看模拟器：客户端多半停在更新/下载界面或已退回桌面，不是判定库的问题"
                    + updateHint + connectHint + "；失败任务：" + FirstJoin(failedTasks, 5, "、") + "；日志：" + logName,
                    tasks, failedTasks);
            }
            bool chainDone = Gd.Truthy(Gd.Get(st, "chainDone"));
            if ((chainDone && tasks.Length > 0 && !firstTaskFailed && !allStartedFailed) || (processGone && allQueuedDone && failed.Length == 0))
            {
                string detail = "实例[" + instName + "] 完成 " + tasks.Length.ToString(CultureInfo.InvariantCulture) + " 个任务";
                if (qc != null) detail += "/队列声明 " + Gd.Str(qc) + " 个";
                if (!chainDone) detail += "(M9A 已按 --quit-after-run 退出，未见完成行)";
                if (failed.Length > 0) detail += "(收尾阶段有告警：" + failed + ")";
                if (failedTasks.Length > 0) detail += "(其中 " + failedTasks.Length.ToString(CultureInfo.InvariantCulture) + " 个任务报过『任务失败』：" + FirstJoin(failedTasks, 3, "、") + ")";
                return Done("ok", detail, tasks);
            }
            if (chainDone && tasks.Length == 0)
            {
                string why;
                if (connectFail.Length > 0) why = "M9A 连模拟器这一步就失败了(声明了 " + Gd.Str(qc) + " 个任务，一个都没开始)";
                else if (qc != null) why = "M9A 自己报的任务数量=" + Gd.Str(qc) + "(声明了却没执行，通常是界面没进游戏)";
                else if (Gd.Truthy(Gd.Get(st, "startRequested"))) why = "开始命令下发了但没有任何任务(任务列表可能全被取消勾选)";
                else why = "M9A 从未收到\"用户操作：启动任务\" → 命令行 --autostart 没生效(注意：短参数 -a 实测无效，必须用长参数)";
                return Done("empty-queue",
                    "M9A 连接完成后直接报『任务已全部完成』，但一条『开始任务：』都没有(用时 " + Round1(elapsed / 60) + " 分钟) → " + why + updateHint + connectHint + "；实例[" + instName + "]",
                    tasks);
            }
            string unknownDetail = "日志里一条认识的标志都没解析出来(实例[" + instName + "] id=" + instId + "，日志：" + logName
                + ") → 多半是 M9A 更新后改了日志文案，或实例重新导入后 id 变了(行内 [inst=名字/id] 过滤把整批行排除了)，也可能是 M9A 压根没往这个文件里写(进程卡在启动/日志目录改了)。"
                + "这轮结果不可信，别按它判定体力是否清完：先跑 tools\\probe-m9a-history.ps1 看实例 id、再用 tools\\replay-m9a-log.ps1 对着这份日志复现，然后更新 lib\\m9a.ps1 的 Read-GdM9ALine";
            if (processGone)
            {
                if (seenNothing) return Done("unknown-format", "M9A 已退出，但" + unknownDetail, tasks);
                if (failed.Length > 0) return Done("error", "M9A 已退出，链路失败：" + failed + "(" + progress + ")" + updateHint + connectHint, tasks);
                if (tasks.Length == 0)
                    return Done("not-started", "M9A 连接后就退出了，一个任务都没跑(" + progress + ") → 命令行 --autostart 没生效(短参数 -a 实测无效)，或资源/程序有新版本要更新" + updateHint + connectHint + "；日志：" + logName, tasks);
                return Done("error", "M9A 进程提前退出(" + progress + "，队列声明 " + (qc != null ? Gd.Str(qc) : "未知") + " 个)" + updateHint + connectHint, tasks);
            }
            if (seenNothing && elapsed > startTimeoutMinutes * 60.0)
                return Done("unknown-format", "启动已 " + Round1(elapsed / 60) + " 分钟，" + unknownDetail, tasks);
            if (elapsed > timeoutMinutes * 60.0)
                return Done("timeout", "超过 " + timeoutMinutes.ToString(CultureInfo.InvariantCulture) + " 分钟未跑完(" + progress + ")", tasks);
            if (!Gd.Truthy(Gd.Get(st, "chainStarted")) && tasks.Length == 0 && elapsed > startTimeoutMinutes * 60.0)
            {
                string hint = string.Equals(Gd.Str(Gd.Get(st, "connectPhase")), "done", StringComparison.OrdinalIgnoreCase)
                    ? "M9A 连上了模拟器却没开始任务队列 → 命令行 --autostart 没生效(短参数 -a 实测无效)，或资源/程序有新版本要更新(日志里有\"还请更新\"这类提示)，或界面被弹窗挡住"
                    : "M9A 连模拟器这一步都没走完";
                return Done("not-started", startTimeoutMinutes.ToString(CultureInfo.InvariantCulture) + " 分钟内没跑任何任务(" + progress + ")。" + hint + updateHint + connectHint + "，日志：" + logName, tasks);
            }
            if (quietSeconds > stallTimeoutMinutes * 60.0)
                return Done("stalled", "M9A 日志 " + IntCast(quietSeconds / 60).ToString(CultureInfo.InvariantCulture) + " 分钟无输出(" + progress + ")，可能卡在某个界面", tasks);
            return Gd.Out("Found", false);
        }

        static bool Contains(string want, string[] arr)
        {
            foreach (string a in arr) if (string.Equals(a, want, StringComparison.OrdinalIgnoreCase)) return true;   // -contains 忽略大小写
            return false;
        }

        // 结论对象：键序照抄旧 hashtable 字面量(Status→Detail→Tasks[→FailedTasks])，Found 恒 true 供壳分流
        static Dictionary<string, object> Done(string status, string detail, string[] tasks)
        {
            return Gd.Out("Found", true, "Status", status, "Detail", detail, "Tasks", tasks);
        }

        static Dictionary<string, object> Done(string status, string detail, string[] tasks, string[] failedTasks)
        {
            return Gd.Out("Found", true, "Status", status, "Detail", detail, "Tasks", tasks, "FailedTasks", failedTasks);
        }

        // ==================== verb: parse-mumu-state(原 Get-GdMuMuState 的三种现场解读) ====================
        // 壳负责起 MuMuManager(Invoke-GdNative)与"目录没填"的第一分支；这里只按 {code,out,err} 出结论。
        public static object MumuState(Dictionary<string, object> inp)
        {
            int code = Gd.Int(Gd.Get(inp, "code"), -1);
            string stdout = Gd.Str(Gd.Get(inp, "out"));
            string stderr = Gd.Str(Gd.Get(inp, "err"));
            if (code != 0 || stdout.Trim().Length == 0)
                return Gd.Out("Available", false, "AndroidStarted", false, "ProcessStarted", false, "Name", null, "Raw", stdout + stderr);
            object j = null;
            try { j = GdJson.Deserialize(stdout); } catch { j = null; }
            if (j == null) return Gd.Out("Available", false, "AndroidStarted", false, "ProcessStarted", false, "Name", null, "Raw", stdout);
            return Gd.Out(
                "Available", true,
                "AndroidStarted", Gd.Truthy(Gd.Get(j, "is_android_started")),
                "ProcessStarted", Gd.Truthy(Gd.Get(j, "is_process_started")),
                "Name", Gd.Str(Gd.Get(j, "name")),
                "Raw", stdout);
        }

        // ==================== verb: parse-mumu-vms(原 Get-GdMuMuVmList 的 JSON→VM 列表) ====================
        // 壳负责"MuMuManager 文件在不在"与起进程；这里只把 info --vmindex all 的输出摊成 Index/Name 对。
        // 空输出/不是合法 JSON/顶层不是对象 → 一律空表(旧壳三条兜底 return @() 的合并)。
        public static object MumuVms(Dictionary<string, object> inp)
        {
            string raw = Gd.Str(Gd.Get(inp, "raw"));
            List<Dictionary<string, object>> vms = new List<Dictionary<string, object>>();
            if (raw.Trim().Length > 0)
            {
                object j = null;
                try { j = GdJson.Deserialize(raw); } catch { j = null; }
                Dictionary<string, object> d = Gd.Map(j);
                if (d != null)
                    foreach (KeyValuePair<string, object> p in d)
                        vms.Add(Gd.Out("Index", p.Key, "Name", Gd.Str(Gd.Get(p.Value, "name"))));
            }
            return Gd.Out("Vms", vms.ToArray());
        }

        // ==================== verb: parse-adb-devices / parse-adb-pidof(原 Connect-GdAdb 判定与 Get-Gd1999Pid) ====================
        public static object AdbDevices(Dictionary<string, object> inp)
        {
            string stdout = Gd.Str(Gd.Get(inp, "out"));
            string serial = Gd.Str(Gd.Get(inp, "serial"));
            bool ok = Rx(stdout, Regex.Escape(serial)).Success && Rx(stdout, @"\tdevice").Success;
            return Gd.Out("Ok", ok);
        }

        public static object AdbPidof(Dictionary<string, object> inp)
        {
            string digits = RxRepl(Gd.Str(Gd.Get(inp, "out")), "\\D", "");
            int pid;
            if (digits.Length > 0 && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out pid))
                return Gd.Out("Found", true, "Pid", pid);
            // 旧实现是 [int]$digits：非数字串/超界会抛转换错。这里回 Found:false 是有意的行为差(壳转 $null，
            // 调用方本来就是"没 pid = 游戏没在跑"的语义)，golden 不铺这种场景。
            return Gd.Out("Found", false);
        }

        // ==================== verb: build-relaunch-args(批次 10，原 lib\common.ps1 Get-GdRelaunchArgList) ====================
        // 基线真身 = common.ps1 Get-GdRelaunchArgList(钉 e269d13)：把已绑定参数还原成提权重启的命令行表。
        // "哪些参数被绑了、是什么类型"是 PS 运行时的事留壳 —— 壳把 $Bound 逐项分类成 {K,T,V} 列表喂进来：
        //   T=flag(开关/布尔，V 存真假) / T=str(其余一律 [string] 强转后的 V，数组值壳侧空格拼串=PS 的 [string]@() 语义) /
        //   T=null(旧代码 -$null 那条 continue 分支)。
        // 口径三条(test-ark [t8]/[t9] 钉着)：skip 判 -in 是大小写不敏感；DryRun 永远不进重启命令(提权只为真跑)；
        // 串值里的 " 按 -replace '"','\"' 逐字复刻成 \"。Get-GdElevateBatchLine 那四行留壳(它吃本 verb 的输出，纯拼串不值得一趟进程)。
        public static object RelaunchArgs(Dictionary<string, object> inp)
        {
            string script = Gd.Str(Gd.Get(inp, "script"));
            List<string> skip = new List<string>();
            foreach (object o in Gd.List(Gd.Get(inp, "omit"))) skip.Add(Gd.Str(o));
            skip.Add("DryRun");

            List<object> args = new List<object> {
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "\"" + script + "\"" };
            foreach (object o in Gd.List(Gd.Get(inp, "force"))) args.Add(Gd.Str(o));

            foreach (object o in Gd.List(Gd.Get(inp, "entries")))
            {
                Dictionary<string, object> e = Gd.Ci(o);
                string k = Gd.Str(Gd.Get(e, "K"));
                bool isSkip = false;
                foreach (string s in skip)
                    if (string.Equals(s, k, StringComparison.OrdinalIgnoreCase)) { isSkip = true; break; }
                if (isSkip) continue;
                string t = Gd.Str(Gd.Get(e, "T"));
                if (t == "null") continue;
                if (t == "flag") { if (Gd.Truthy(Gd.Get(e, "V"))) args.Add("-" + k); continue; }
                args.Add("-" + k);
                args.Add("\"" + Gd.Str(Gd.Get(e, "V")).Replace("\"", "\\\"") + "\"");
            }
            return Gd.Out("Args", args.ToArray());
        }
    }
}
