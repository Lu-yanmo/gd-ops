// tools/panel-cs/Round.Data.cs —— B-3②-b1(2026-10-13)：**判定段与门表段的"桌子"**——数据面六枚 verb。
// (切分与射程见 docs\rust-core-plan.md §11.52 ④ 那句"②-b1＝panel-cs 侧判定段与门表段的桌子：①那 2、3、5 项的
//  取数与写盘件逐枚新立(活动两路／ack／shopDone／门表写三枚)"，本批不升号、不改任何既有 verb 的回执形状。)
//
// 这一份管的是"一轮判定开跑之前必须先从这台机器上取到、以及跑完之后必须落回盘上"的那些值：
//   read-ark-activity     Get-ArkEvent(lib\ark-decisions.ps1:75) 的**取数半段**：接口 → MAA 本地缓存 → 无数据，
//                         三档顺序与那两条 WARN 原话照抄；后半段(把值喂进 decide-ark)不在这儿——那是判定，已住 Decisions.cs。
//   read-r1999-activity   Get-R1999Event(lib\r1999-decisions.ps1:39) 的**取数半段**：读 M9A 自带活动数据 + 算 DataAge(天)。
//   read-decision-state   "五本 state 读"一次取齐：<game>-event-ack.json / <game>-shop-done.json / ark-event-probe.json，
//                         外加领空门"给不给"的**读值半段**(原 lib\decisions-common.ps1::Test-GdShopDoneEnabled 已出仓，见 :504)。
//   compose-decision-inputs Resolve-ArkPlan／Resolve-R1999Plan 的**组值半段**(转接刀·判定面进核 2026-11-03 新立)：
//                         dow(周日=7)／now(毫秒)／ack／领空折好的 shopDone／probe／ark·re1999 块与 paths 块一次摆桌，
//                         给核臂 round-resolve:* 组 decide-* 的 body 用。
//   set-ark-shop-state    Set-GdArkShopState(lib\ark-daily.ps1:44)；
//   set-r1999-ack         Set-GdR1999Ack(lib\r1999-daily.ps1:45)；
//   set-r1999-shop-state  Set-GdR1999ShopState(lib\r1999-daily.ps1:56)。
//   —— 门表写三枚的名字**逐字对着那三个 PS 函数**，将来 ②-d 转接时是一一替换关系，不是"另起一套"。
//
// 与本批切分有关的三件事(为什么只有读没有判定)：
//   * 判定规则一枚都不许新写：`decide-ark` / `decide-r1999` / `decide-ark-event-probe` 全在场(批次 1／5／第 1 刀)，
//     缺的从来只是"把值取来放桌上"。所以 ArkActivity / R1999Activity 只回 Data+Source，不回 Mode／EventKey／Reason(判定结论)。
//   * 门表写三枚的"先取数后写盘、没活动只 WARN 不写盘"那半段属于**调用方**：本文件收 eventKey 非空当已取过数，
//     空串就是那一句 WARN 的形状(照抄)，不自己去看活动 —— 否则判定链会被复制第二份。
//   * 领空门的"给不给"那一步仍留在调用方(lib\ark-decisions.ps1:58-61 的冻结头明写它是协议的一部分)：
//     本 verb 只把**原表**和**那把 flag 尺的读数**(ShopDoneGateOpen)一起摆上桌，谁用谁自己折。
//
// 双源登记(改一句要两侧同改；生产接线(②-d)之前 PS 那侧仍是事实源)：
//   * 上面每一句 WARN/OK 原话 ↔ lib\ark-daily.ps1:48-58 / lib\r1999-daily.ps1:49-68 / lib\ark-decisions.ps1:87-109；
//   * state 文件名与外层键(`$Game-event-ack`+`acked`、`$Game-shop-done`+`done`、`ark-event-probe` 的五键)
//     ↔ lib\decisions-common.ps1:57-139 与 lib\ark-decisions.ps1:119-156(写盘半段 158-189 已于 11-06 转接刀·运行面刀⑯ 出仓，本枚为唯一真身) —— 键名与时间戳格式('yyyy-MM-dd HH:mm')是磁盘协议，
//     PS 那侧还在按这个名字读，这里写错了就是"门自己关不上"；
//   * **r1999 的 `DataAge` 后处理是 §11.52 ⑤ 新登记的那处双源，本批不定档**：PS 侧 Get-R1999Event(lib\r1999-decisions.ps1:84-85) 拿 age 追加
//     到 `$result.Reason` 那句"(活动数据 N 天前)"里。本 verb 只回 `DataAgeDays` 这个**数**，不做那句追加(追加属于判定结论)；
//     谁在 ②-b3／②-d 接这一步，必须同时决定 PS 那两行删不删 —— 不许两边都留。
//   * 落盘形状：真身走 ConvertTo-Json(缩进多行)，这里走 JavaScriptSerializer(单行)。与 Round.cs:510 那笔"形状差"同一档：
//     读的只有 PS 的 ConvertFrom-Json 和面板，两者都不看缩进 —— 但**键序不是不变量**(PS 那侧 hashtable 换进程就重排)，
//     所以这里按"写入顺序"落即可，不承诺与 PS 落盘逐字节相同。
//
// 原样怪癖(照抄，不许"顺手修")：
//   1. **ark 领空话术里那个 dow 是原始 0-6**(lib\ark-daily.ps1:51 `"$([int](Get-Date).DayOfWeek)"`)，
//      而判定层 Resolve-ArkPlan 用的是"周日=7"那把尺。于是周日那天这句人话读到的是 `planByWeekday."0"`(通常没配)
//      → 回落 defaultStage。它只影响**一句人话**，不影响判定 —— 这里照抄 0-6，别拿 ctx 那枚 Dow(1-7) 喂进来。
//   2. **`[int]$S.ark.activityCacheMaxHours` 缺键时是 0**(PS 的 [int]$null)，于是"没配这一项"＝"缓存永远算过期"，
//      每轮都吭那一句 WARN。这里 Gd.Int(...,0) 同形，不给 24 那种"更合理"的默认值。
//   3. **`if (-not $data)` 在 PS 里对 ConvertFrom-Json 的产物恒假**(pscustomobject 哪怕空也是真)，
//      而空数组 `@()` 算假。C# 侧 DeserializeObject 给的是字典／object[]，HasData 按"非 null 且非空数组"判 ——
//      空对象 `{}` 两侧同算"拉到东西了"(下一句 source 就是"MAA 活动接口")，这一格 --roundtest 钉住。
//   4. **1999 的确认话术里那个箭头前没有空格**："…[$tip]→ 日常将使用活动方案"(lib\r1999-daily.ps1:52 原文如此)，
//      而 ark 那两句是"…[$tip] 商店领空 → …"有空格。两侧本来就不一致，②-a 那条"话术不许统一"的纪律在这儿同样成立。
//   5. **ark 那一路只读不写 ack 表**：Set-GdArkShopState 只碰 shop-done，ark 的 ack 门已随 -AckArk 退场(10-12 第 1 刀)，
//      判定层 Decisions.cs:158 也明写"ack／verbAck 两枚入参不再读"。所以名册里没有 set-ark-ack —— 那是给一枚不存在的门造桌子。
//
// 安全边界(与 Round.cs / Live.cs 同一条自检纪律)：
//   * --roundtest **不真打 MAA 活动接口、不读真 config\settings.json、不动真 state 盘**：
//     网络面 apiJson／apiErr 两枚夹具、缓存面 cacheJson+cacheAgeHours、读盘面 path+mtime 全部可注入；
//     写盘面只打在 GdEnv.InitForTest 的临时树上，收尾整目录删掉，不落仓内；
//   * 漏喂 settings 夹具就会落到真盘 ⇒ 当场红(临时树里故意不建 config\settings.json，见 Round.SelfTest.cs 头注)；
//   * **整块 runtime 绝不外泄**照旧：本文件六枚**一枚都不读 runtime**(读的是 ark／re1999／paths 三块)，
//     visionApiKey 那类本机专属值射程之外；
//   * 游戏 id 会被拼进状态文件名，认不得的必须当场挡住(与 Assert-GdGameId 同形)：名册默认就是这棵树上的两路，
//     将来模块清单扩了游戏，由调用方点名 `gameIds` 传进来，不在这里硬编码第三路。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace GdPanel
{
    static class RoundData
    {
        // ==================== 入参取数：缺键=自己现读(现场)，有键=用夹具 ====================
        // 与 Round.cs:84-132 同形的那把尺，本批**不动 Round.cs 的可见性**(题面＝新立，不改装)所以这里自带一份；
        // 唯一复用过去的是 Round.FlagEnabled —— 那把 `-ne $false` 的尺在 C# 侧只许有一个真身。

        static Dictionary<string, object> View(Dictionary<string, object> raw)
        {
            Dictionary<string, object> d = Gd.Ci(raw);
            if (d == null) d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return d;
        }

        static bool Given(Dictionary<string, object> c, string k)
        {
            object v;
            return c.TryGetValue(k, out v) && v != null;
        }

        static string OptStr(Dictionary<string, object> c, string k, Func<string> def)
        {
            object v;
            if (c.TryGetValue(k, out v) && v != null) return Gd.Str(v);
            return def == null ? "" : def();
        }

        static bool OptFlag(Dictionary<string, object> c, string k)
        {
            object v;
            return c.TryGetValue(k, out v) && Gd.Truthy(v);
        }

        static long OptLng(Dictionary<string, object> c, string k, long def)
        {
            object v;
            if (!c.TryGetValue(k, out v) || v == null) return def;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        static double OptDbl(Dictionary<string, object> c, string k, double def)
        {
            object v;
            if (!c.TryGetValue(k, out v) || v == null) return def;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        static List<string> OptList(Dictionary<string, object> c, string k)
        {
            object v;
            if (!c.TryGetValue(k, out v)) return new List<string>();
            List<string> outList = new List<string>();
            foreach (object o in Gd.List(v)) outList.Add(Gd.Str(o));
            return outList;
        }

        static Dictionary<string, object> Sub(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        static void Say(List<object> lines, string level, string msg)
        {
            lines.Add(msg);
            GdLog.Line(msg, level);        // 与 Round.Notice 同一档：verb 子进程默认不落盘，只进 stdout／信封
        }

        // ==================== 名册：游戏 id → settings 块名(照 Get-GdGameIds / Get-GdSettingsBlock) ====================

        static readonly string[] FallbackGameIds = new string[] { "ark", "r1999" };

        static List<string> GameIds(Dictionary<string, object> c)
        {
            List<string> given = OptList(c, "gameIds");
            if (given.Count > 0) return given;
            return new List<string>(FallbackGameIds);
        }

        // Assert-GdGameId 同形：PS 的 `-notcontains` 不分大小写，这里也不分(键名进文件名，宁可挡在门外)。
        // 一处形状差登记在此：PS 真身把**原样的** $Game 回传(于是 'ARK' 会写出 ARK-event-ack.json)，
        // 这里归一成名册里的写法(ark-event-ack.json)。NTFS 不分大小写 ⇒ 同一枚文件、同一个读数，
        // 差的只有回执里那格 Game 的拼法；真身那写法是"顺手把大小写带进文件名"，不值得复刻。
        static string AssertGame(Dictionary<string, object> c)
        {
            string game = OptStr(c, "game", null);
            List<string> ids = GameIds(c);
            if (game.Length == 0)
                throw new GdRuleException("不认识的游戏 id：''(这棵树上认得：" + string.Join(" / ", ids.ToArray()) + ")");
            foreach (string id in ids)
                if (id.Equals(game, StringComparison.OrdinalIgnoreCase)) return id;   // 归一成名册里的写法
            throw new GdRuleException("不认识的游戏 id：'" + game + "'(这棵树上认得：" + string.Join(" / ", ids.ToArray()) + ")");
        }

        // 块名不许按 id 字符串拼(r1999 的 id 是 r1999、settings 块叫 re1999)，与 Get-GdSettingsBlock 同形：
        // 默认两路，第三路来了由调用方点 `settingsBlock`。
        static string BlockName(Dictionary<string, object> c, string game)
        {
            string given = OptStr(c, "settingsBlock", null);
            if (given.Length > 0) return given;
            return game == "ark" ? "ark" : "re1999";
        }

        static Dictionary<string, object> SettingsOf(Dictionary<string, object> c)
        {
            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "settings"));
            if (given != null) return given;
            List<string> missing;
            return GdSettings.Load(out missing, true, new List<string>());
        }

        static Dictionary<string, object> BlockOf(Dictionary<string, object> c, string game)
        {
            Dictionary<string, object> b = Gd.Map(Gd.Get(SettingsOf(c), BlockName(c, game)));
            return b ?? new Dictionary<string, object>();
        }

        static Dictionary<string, object> PathsOf(Dictionary<string, object> c)
        {
            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "paths"));
            if (given != null) return given;
            return Gd.Map(Gd.Get(SettingsOf(c), "paths")) ?? new Dictionary<string, object>();
        }

        // ==================== 磁盘那五本账：路径与读写(键名是协议，见文件头双源登记) ====================

        static string StateDir()
        {
            GdEnv.Init(null);
            return GdEnv.State;
        }

        internal static string AckFile(string game) { return Path.Combine(StateDir(), game + "-event-ack.json"); }
        internal static string ShopDoneFile(string game) { return Path.Combine(StateDir(), game + "-shop-done.json"); }
        const string ProbeFile = "ark-event-probe.json";

        // state 读件(原 Get-GdStateObject，刀⑰(a) 已退役) + Get-GdMapValue + Convert-GdMapToHashtable 三段合成：文件不在→空表、外层键缺→空表、
        // 文件坏了→照 PS 那样抛(那两枚 Get-Gd*Map 没有 try，坏了就该把这一轮拦下来，不许当"没人领过")。
        // 表必须是**不分大小写**的：PS 的 @{} 默认 OrdinalIgnoreCase，于是 $map['CE5'] 与 $map['ce5'] 是同一格，
        // ContainsKey 也按这把尺判 —— 这里若用区分大小写的字典，撤销那一步会在盘上留一格谁也认不出的孤儿。
        internal static Dictionary<string, object> ReadGateMap(string file, string outer)
        {
            Dictionary<string, object> m = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(file)) return m;
            object o = GdJson.Read(file);
            Dictionary<string, object> src = Gd.Map(Gd.Get(o, outer));
            if (src != null) foreach (KeyValuePair<string, object> kv in src) m[kv.Key] = kv.Value;
            return m;
        }

        static void WriteGateMap(string file, string outer, Dictionary<string, object> map)
        {
            GdJson.Write(file, Gd.Out(outer, map));
        }

        // StampMapKey：取表→盖这一格→整表写回(原 lib\decisions-common.ps1::Set-GdShopDoneKey／Set-GdAckKey 已出仓，语义由核内本真身与 set-*-shop-state／set-r1999-ack 出口承接)。
        static int StampMapKey(string file, string outer, string key, string time)
        {
            Dictionary<string, object> m = ReadGateMap(file, outer);
            m[key] = time;
            WriteGateMap(file, outer, m);
            return m.Count;
        }

        // DropMapKey：不在就回 false(**且不写盘** —— 原 lib\decisions-common.ps1::Remove-GdShopDoneKey 已出仓，该形由 set-*-shop-state 的 Action=miss 承接)。
        static bool DropMapKey(string file, string outer, string key, out int count)
        {
            Dictionary<string, object> m = ReadGateMap(file, outer);
            if (!m.ContainsKey(key)) { count = m.Count; return false; }
            m.Remove(key);
            WriteGateMap(file, outer, m);
            count = m.Count;
            return true;
        }

        // ReadProbe：文件不在／读坏了／没有 EventKey —— 三种"没探过"一律回 null(原 lib\ark-decisions.ps1::Get-GdArkEventProbe 已出仓，判定层经 read-decision-state 的 Probe 直答取齐)。
        internal static Dictionary<string, object> ReadProbe()
        {
            string p = Path.Combine(StateDir(), ProbeFile);
            if (!File.Exists(p)) return null;
            object o;
            try { o = GdJson.Read(p); } catch { return null; }
            Dictionary<string, object> m = Gd.Map(o);
            if (m == null) return null;
            string key = Gd.Str(Gd.Get(m, "EventKey"));
            if (key.Length == 0) return null;
            return Gd.Out(
                "Date", Gd.Str(Gd.Get(m, "Date")),
                "EventKey", key,
                "Stage", Gd.Str(Gd.Get(m, "Stage")),
                "Entered", Gd.Truthy(Gd.Get(m, "Entered")),
                "Evidence", Gd.Str(Gd.Get(m, "Evidence")));
        }

        // PS 那一句时间戳(Get-Date -Format 'yyyy-MM-dd HH:mm')：这一格只进磁盘表，不进判定。
        static string Stamp(Dictionary<string, object> c)
        {
            return OptStr(c, "nowText", delegate { return DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); });
        }

        // ==================== verb: read-ark-activity ====================
        // Get-ArkEvent 的取数半段(lib\ark-decisions.ps1:75-112)：三档顺序一把不差 ——
        //   接口(非 offline 且 apiUrl 非空，一把 try) → MAA 本地缓存(读盘 + mtime 算小时 + 过期 WARN) → 无数据。
        // 那两句 WARN 原话逐字抄；`Resolve-ArkPlan` 那一步不在这里(它是转发壳，真身是 decide-ark)。
        internal static object ArkActivity(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            Dictionary<string, object> ark = BlockOf(c, "ark");
            Dictionary<string, object> paths = PathsOf(c);

            bool offline = OptFlag(c, "offline");
            string apiUrl = OptStr(c, "apiUrl", delegate { return Gd.Str(Gd.Get(ark, "activityApiUrl")); });
            // 怪癖第 2 笔：缺键就是 0(PS 的 [int]$null)，不是 24 —— 于是"没配这一项"＝缓存永远算过期。
            // 两句分家照抄真身：比较用的是**强转后的数**(`$age -gt [int]$S.ark.activityCacheMaxHours`)，
            // 而那句 WARN 里插的是**原值**(`$($S.ark.activityCacheMaxHours)`)：缺键时 PS 打印的是**空**，不是 0。
            object maxRaw = Given(c, "cacheMaxHours") ? Gd.Get(c, "cacheMaxHours") : Gd.Get(ark, "activityCacheMaxHours");
            int maxHours = Gd.Int(maxRaw, 0);
            string maxText = Gd.Str(maxRaw);

            object data = null;
            string source = "";
            object ageOut = null;
            bool stale = false;

            if (!offline && apiUrl.Length > 0)
            {
                if (Given(c, "apiJson"))
                {
                    data = Gd.Get(c, "apiJson");
                    source = "MAA 活动接口";
                }
                else
                {
                    string fakeErr = OptStr(c, "apiErr", null);
                    if (fakeErr.Length > 0)
                    {
                        Say(lines, "WARN", "活动接口拉取失败(" + fakeErr + ")，改用本地缓存");
                    }
                    else
                    {
                        try
                        {
                            data = GetJson(apiUrl);
                            source = "MAA 活动接口";
                        }
                        catch (Exception ex)
                        {
                            // 原话逐字(lib\ark-decisions.ps1:90)。⚠ 这一支在 --roundtest 里只走夹具档：真打接口 unobserved。
                            Say(lines, "WARN", "活动接口拉取失败(" + ex.Message + ")，改用本地缓存");
                        }
                    }
                }
            }

            if (!HasData(data))
            {
                object cacheData = null;
                double hours = 0;
                bool found = false;
                if (Given(c, "cacheJson"))
                {
                    found = true;
                    cacheData = Gd.Get(c, "cacheJson");
                    hours = OptDbl(c, "cacheAgeHours", 0);
                }
                else
                {
                    // Join-GdPath $S.paths.maaDir @($S.paths.maaActivityCache)：maaDir 没填→空串→PathReady 假。
                    // (真身那里用的是裸 Test-Path，空串在 PS7 会绑参报错；那一支要 maaDir 空才会走到，
                    //  而 maaDir 是 RequiredPathKeys 里催着的键 ⇒ 生产上到不了，这里按"没有缓存"处理，形状差登记在此。)
                    string cache = GdEnv.JoinPath(Gd.Str(Gd.Get(paths, "maaDir")), Gd.Str(Gd.Get(paths, "maaActivityCache")));
                    if (GdEnv.PathReady(cache))
                    {
                        found = true;
                        cacheData = GdJson.Read(cache);
                        try { hours = (DateTime.Now - File.GetLastWriteTime(cache)).TotalHours; } catch { hours = 0; }
                    }
                }
                if (found)
                {
                    data = cacheData;
                    double rounded = Math.Round(hours, 1);
                    ageOut = rounded;
                    source = "MAA 本地缓存(" + Gd.Str(rounded) + " 小时前)";
                    if (hours > maxHours)
                    {
                        stale = true;
                        Say(lines, "WARN", "活动缓存超过 " + maxText + " 小时未刷新，结论可能滞后(MAA 启动后会自行更新)");
                    }
                }
                else
                {
                    Say(lines, "WARN", "既拉不到活动接口、也没有本地缓存 → 按长草期处理");
                    source = "无数据";
                }
            }

            return Gd.Out(
                "Data", data,
                "Source", source,
                "CacheAgeHours", ageOut,
                "CacheStale", stale,
                "ApiUrlGiven", apiUrl.Length > 0,
                "Offline", offline,
                "Lines", lines.ToArray());
        }

        // 怪癖第 3 笔：PS 的 `-not $data` 对"JSON 给的对象"恒假(空对象也算有)，对空数组算真(没数据)。
        static bool HasData(object v)
        {
            if (v == null) return false;
            object[] a = v as object[];
            if (a != null) return a.Length > 0;
            return true;
        }

        // Invoke-WebRequest -TimeoutSec 8 -UseBasicParsing 的同形(GET + 8s + 只看正文)，TLS 那句同 Round.PostJson。
        static object GetJson(string url)
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072 | (SecurityProtocolType)768;
            }
            catch { }
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 8000;
            req.ReadWriteTimeout = 8000;
            using (WebResponse resp = req.GetResponse())
            using (Stream st = resp.GetResponseStream())
            using (StreamReader sr = new StreamReader(st, Encoding.UTF8))
            {
                return GdJson.Deserialize(sr.ReadToEnd());
            }
        }

        // ==================== verb: read-r1999-activity ====================
        // Get-R1999Event(lib\r1999-decisions.ps1:39-87) 的取数半段：读 M9A 自带活动数据 + 算 DataAge(**天**)。
        // 那九键 idle 信封**不在这里造**(那是判定结论，现住在 PS 壳里)：这里只回 Ready／Why／Reason 三格，
        // 其中 Reason 就是"$why，按长草处理"那一句原话 —— ②-b3 若要自己造信封，从这一格取，别在 Rust 侧再抄一遍。
        // DataAge 那句追加(双源第④处，§11.52 ⑤)本批不定档，见文件头。
        internal static object R1999Activity(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);

            // 路径三枚入参里 `path` 是**直答**：给了就代表调用方已经把这文件名拼好了(或自检要指哪读哪)，
            // 这时**不去现读 settings** —— 真身那句 `Join-GdPath $S.paths.m9aDir @($S.paths.m9aActivityData)`
            // 只在没人点名 path 时才需要。这样自检指一份临时树上的文件就能测"真读盘"那半段，不必修一份假 settings。
            string path;
            if (Given(c, "path")) path = Gd.Str(Gd.Get(c, "path"));
            else
            {
                Dictionary<string, object> paths = PathsOf(c);
                string dir = OptStr(c, "m9aDir", delegate { return Gd.Str(Gd.Get(paths, "m9aDir")); });
                string rel = OptStr(c, "activityDataPath", delegate { return Gd.Str(Gd.Get(paths, "m9aActivityData")); });
                path = GdEnv.JoinPath(dir, rel);
            }

            bool ready = GdEnv.PathReady(path);
            object data = null;
            object ageOut = null;
            string why = "";

            if (!ready)
            {
                // Test-GdPathReady 而不是 Test-Path：没跑 setup 时 path 是空串，"缺少 ，按长草处理"那种半截话玩家看不懂。
                why = path.Length > 0 ? "缺少 " + path : "M9A 目录还没填(先跑 setup.ps1)";
            }
            else
            {
                data = Given(c, "dataJson") ? Gd.Get(c, "dataJson") : GdJson.Read(path);
                double days = OptDbl(c, "dataAgeDays", double.NaN);
                if (double.IsNaN(days))
                {
                    try { days = (DateTime.Now - File.GetLastWriteTime(path)).TotalDays; }
                    catch { days = 0; }
                }
                ageOut = Math.Round(days, 1);
            }

            return Gd.Out(
                "Ready", ready,
                "Data", data,
                "DataAgeDays", ageOut,
                "Why", why,
                "Reason", ready ? null : why + "，按长草处理",
                "Path", path);
        }

        // ==================== verb: compose-decision-inputs ====================
        // Resolve-ArkPlan(lib\ark-decisions.ps1:42-73)／Resolve-R1999Plan(lib\r1999-decisions.ps1:17-37) 的**组值半段**：
        // 判定开跑之前把"一轮判定需要的值"组装好摆上桌 —— 核臂(round-resolve:*)手里没有 settings
        // (核侧 settings 一律经 verb，main.rs round_answer 名册头注)，这里组的就是 decide-ark／decide-r1999
        // 的 body 里除 data/source 外那几枚键，核臂取走后自己拼。
        // 逐字真身：dow 周日=7(lib\ark-decisions.ps1:56-57 原样，老行为)；now=Get-GdNowMs 同形毫秒
        // (lib\decisions-common.ps1:39 [DateTimeOffset]::Now.ToUnixTimeMilliseconds())；
        // ack/shopDone 两本门表(ReadGateMap)；领空表"给不给"那一折真身在核：FlagEnabled(shopDoneStopsEvent)
        // 缺省即开启、显式 false 才关(Round.cs:166-174 一把尺)＋刀㉑ 笔(b)-1 新收的可选入键 ignoreShopDone(缺省 false)；
        // 原两本 PS 壳各写两行折算(lib\ark-decisions.ps1:66-67／lib\r1999-decisions.ps1:34-35 旧形)，本批起壳只传键。
        // probe 只喂 decide-ark(Resolve-R1999Plan 的 body 没有 probe 键)，这里统一回，谁用谁自己取。
        internal static object ComposeDecisionInputs(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string game = AssertGame(c);

            Dictionary<string, object> block = BlockOf(c, game);
            Dictionary<string, object> paths = PathsOf(c);

            int dow = (int)DateTime.Now.DayOfWeek;
            if (dow == 0) { dow = 7; }
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            object ack = ReadGateMap(AckFile(game), "acked");
            object shopDoneRead = ReadGateMap(ShopDoneFile(game), "done");
            Dictionary<string, object> emptyMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            // 刀㉑ 笔(b)-1(2026-11-08)：领空折图多一枚**可选入键** ignoreShopDone(布尔，缺省 false)。
            // 原形＝两本 PS 壳各写两行(lib\ark-decisions.ps1:66-67／lib\r1999-decisions.ps1:34-35)：
            // 非 IgnoreShopDone 且 FlagEnabled(shopDoneStopsEvent) 才给盘上表，否则给空表；本批起折算只有核这一处，
            // 壳只传键。键缺席(Rust 核臂两发只传 game)恒按 false＝旧调用方逐字不变；回执八枚键一枚没动⇒信封形状不变。
            object igRaw = Gd.Get(c, "ignoreShopDone");
            bool ignoreShopDone = igRaw is bool ? (bool)igRaw
                : string.Equals(igRaw as string, "true", StringComparison.OrdinalIgnoreCase);
            object shopDone = (!ignoreShopDone && Round.FlagEnabled(Gd.Get(block, "shopDoneStopsEvent")))
                ? (object)shopDoneRead : emptyMap;

            return Gd.Out(
                "Game", game,
                "Block", block,
                "Paths", paths,
                "Dow", dow,
                "Now", now,
                "Ack", ack,
                "ShopDone", shopDone,
                "Probe", ReadProbe());
        }

        // ==================== verb: read-decision-state ====================
        // "五本 state 读"一次取齐(①-2 那一族)。want 逐枚点名：默认三样都要，调用方可以只要 ack。
        // ShopDoneGateOpen 就是领空门"给不给"的读值半段(原 lib\decisions-common.ps1::Test-GdShopDoneEnabled 已出仓，本读数是其唯一真身)：
        // **缺省即开启，显式写 false 才关掉**，
        // 复用 Round.FlagEnabled(C# 侧唯一真身，原样怪癖第 6 笔在 Round.cs 头注)。
        // 领空表的"给不给"仍归调用方 —— 这里只把原表和读数一起摆上桌。
        internal static object DecisionState(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string game = AssertGame(c);

            List<string> want = OptList(c, "want");
            if (want.Count == 0) want = new List<string>(new string[] { "ack", "shopDone", "probe" });

            object ack = null;
            object shopDone = null;
            object probe = null;
            bool wAck = Wants(want, "ack"), wShop = Wants(want, "shopDone"), wProbe = Wants(want, "probe");
            if (wAck) ack = ReadGateMap(AckFile(game), "acked");
            if (wShop) shopDone = ReadGateMap(ShopDoneFile(game), "done");
            if (wProbe) probe = ReadProbe();

            object flag = Gd.Get(BlockOf(c, game), "shopDoneStopsEvent");
            return Gd.Out(
                "Game", game,
                "Ack", ack,
                "ShopDone", shopDone,
                "ShopDoneGateOpen", Round.FlagEnabled(flag),
                "Probe", probe,
                "Want", want.ToArray());
        }

        static bool Wants(List<string> want, string key)
        {
            foreach (string w in want) if (w.Equals(key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ==================== verb: set-ark-shop-state ====================
        // Set-GdArkShopState(lib\ark-daily.ps1:44-60) 的**写盘＋话术**两半(取数半段归调用方，见文件头)。
        // 四分支一把不差：空 key 只 WARN 不写盘 / done=真 标记 / done=假 撤销成功 / 撤销落空(本来就没有)。
        internal static object ArkShopState(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            string key = OptStr(c, "eventKey", null);
            string tip = OptStr(c, "eventTip", null);
            bool done = OptFlag(c, "done");
            string time = Stamp(c);
            string file = ShopDoneFile("ark");
            int count = 0;

            if (key.Length == 0)
            {
                Say(lines, "WARN", "方舟当前没有进行中的活动可标记：" + OptStr(c, "reason", null));
                return Out("none", false, key, time, 0, lines);
            }
            if (done)
            {
                string plan = OptStr(c, "idlePlan", delegate { return ArkIdlePlan(c); });
                count = StampMapKey(file, "done", key, time);
                Say(lines, "OK", "已标记方舟活动 " + key + "[" + tip + "] 商店领空 → 之后剩余理智只刷 " + plan);
                return Out("mark", true, key, time, count, lines);
            }
            if (DropMapKey(file, "done", key, out count))
            {
                Say(lines, "OK", "已撤销 " + key + " 的领空标记 → 恢复自动刷活动代币");
                return Out("reset", true, key, time, count, lines);
            }
            Say(lines, "WARN", key + " 本来就没有领空标记");
            return Out("miss", false, key, time, count, lines);
        }

        // 怪癖第 1 笔：这里要的是 [int](Get-Date).DayOfWeek 的原始 0-6，不是判定层那把"周日=7"。
        static string ArkIdlePlan(Dictionary<string, object> c)
        {
            Dictionary<string, object> ark = BlockOf(c, "ark");
            int dow = (int)OptLng(c, "dow", (long)DateTime.Now.DayOfWeek);
            Dictionary<string, object> table = Gd.Map(Gd.Get(ark, "planByWeekday"));
            string plan = Gd.Str(Gd.Get(table, dow.ToString(CultureInfo.InvariantCulture)));
            if (plan.Length == 0) plan = Gd.Str(Gd.Get(ark, "defaultStage"));
            return plan;
        }

        // ==================== verb: set-r1999-ack ====================
        // Set-GdR1999Ack(lib\r1999-daily.ps1:45-54)：只有"标记"一档(这道门没有撤销口)。
        internal static object R1999Ack(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            string key = OptStr(c, "eventKey", null);
            string tip = OptStr(c, "eventTip", null);
            string time = Stamp(c);

            if (key.Length == 0)
            {
                Say(lines, "WARN", "1999 当前没有待确认活动：" + OptStr(c, "reason", null));
                return Out("none", false, key, time, 0, lines);
            }
            int count = StampMapKey(AckFile("r1999"), "acked", key, time);
            // 怪癖第 4 笔：箭头前真身就没有空格，别"顺手补齐"。
            Say(lines, "OK", "已确认 1999 活动 " + key + "[" + tip + "]→ 日常将使用活动方案");
            return Out("mark", true, key, time, count, lines);
        }

        // ==================== verb: set-r1999-shop-state ====================
        // Set-GdR1999ShopState(lib\r1999-daily.ps1:56-70)：与 ark 那枚同构，差两句人话 + 回落值是实例名不是关卡。
        internal static object R1999ShopState(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            string key = OptStr(c, "eventKey", null);
            string tip = OptStr(c, "eventTip", null);
            bool done = OptFlag(c, "done");
            string time = Stamp(c);
            string file = ShopDoneFile("r1999");
            int count = 0;

            if (key.Length == 0)
            {
                Say(lines, "WARN", "1999 当前没有进行中的活动可标记：" + OptStr(c, "reason", null));
                return Out("none", false, key, time, 0, lines);
            }
            if (done)
            {
                string inst = OptStr(c, "idleInstance", delegate
                {
                    return Gd.Str(Gd.Get(Gd.Map(Gd.Get(BlockOf(c, "r1999"), "planToInstance")), "idle"));
                });
                count = StampMapKey(file, "done", key, time);
                Say(lines, "OK", "已标记 1999 活动 " + key + "[" + tip + "] 商店领空 → 之后跑[" + inst + "]实例");
                return Out("mark", true, key, time, count, lines);
            }
            if (DropMapKey(file, "done", key, out count))
            {
                Say(lines, "OK", "已撤销 " + key + " 的领空标记 → 恢复活动方案");
                return Out("reset", true, key, time, count, lines);
            }
            Say(lines, "WARN", key + " 本来就没有领空标记");
            return Out("miss", false, key, time, count, lines);
        }

        // 三枚写件共用同一副回执形状：Action 四档(none|mark|reset|miss) + 是否真写了盘 + 表内还剩几格。
        static Dictionary<string, object> Out(string action, bool written, string key, string time,
                                            int count, List<object> lines)
        {
            return Gd.Out(
                "Action", action,
                "Written", written,
                "Key", key,
                "Time", time,
                "Count", count,
                "Lines", lines.ToArray());
        }

        // ==================== verb: update-ark-event-probe(R9-a 收编 round-probe:ark) ====================
        // Update-GdArkEventProbe(lib\ark-decisions.ps1:158-189；PS 原身已于 11-06 转接刀·运行面刀⑯ 从 lib\ark-decisions.ps1 出仓，本枚自此为唯一真身) 的**整枚**：两枚前置闸(EventKey／StageHint
        // 给不出就不采)、四档判据、落盘、三句话术，全在这里 —— 判据那半**直调同装配的
        // Orchestration.ArkEventProbe 本体**(`decide-ark-event-probe`，不在核内也不在这里抄第二份)。
        // 与同文件那三枚写件的差别：这一枚**不打 GdLog**，把要说的话连同**档位**一起带回回执
        // (NoticeLevel／NoticeText)。理由＝verb 是子进程、它的 stdout 被核收掉就丢，而真身那三句用的是
        // WARN／OK／WARN —— 走 DirectOut.lines 只会按 INFO 落侧信道(降档)，所以由核内段 9 的消费点
        // 用 `fx_log` 转成一行账外日志，档位才保得住。空串＝这一轮没什么要说(那两枚前置闸没过那一档)。
        // 磁盘那一本沿用同文件既有的 ProbeFile 常量与 StateDir()(键名与时间戳格式是磁盘协议，见文件头)。
        internal static object ArkEventProbeUpdate(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            Dictionary<string, object> dec = Gd.Map(Gd.Get(c, "decision"));
            string evKey = dec == null ? "" : Gd.Str(Gd.Get(dec, "EventKey"));
            string target = dec == null ? "" : Gd.Str(Gd.Get(dec, "StageHint"));
            // 真身那两枚前置闸：`if (-not $Decision -or -not $Decision.EventKey) { return $null }`、
            // `$target = [string]$Decision.StageHint; if (-not $target) { return $null }` —— 都不吭话、都不写盘。
            if (evKey.Length == 0 || target.Length == 0)
                return Gd.Out("Written", false, "NoticeLevel", "", "NoticeText", "");

            string date = OptStr(c, "date", delegate { return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); });
            Dictionary<string, object> v = Gd.Map(Orchestration.ArkEventProbe(Sub(
                "date", date,
                "eventKey", evKey,
                "stage", target,
                "obs", Round.Raw(c, "obs"))));     // 用三态那把尺：obs 写了 null 才算"本轮没有观测读数"

            string evidence = Gd.Str(Gd.Get(v, "Evidence"));
            if (!Gd.Truthy(Gd.Get(v, "Valid")))
                // 原话逐字(原 lib\ark-decisions.ps1:172；PS 原身已随 11-06 刀⑯ 出仓)：这一档**不落地**(留着昨天的结论也没意义)，下轮照常再试。
                return Gd.Out("Written", false, "NoticeLevel", "WARN", "NoticeText", "活动图自证：" + evidence);

            bool entered = Gd.Truthy(Gd.Get(v, "Entered"));
            string stage = Gd.Str(Gd.Get(v, "Stage"));
            Dictionary<string, object> obj = Gd.Out(
                "Date", Gd.Str(Gd.Get(v, "Date")),
                "EventKey", Gd.Str(Gd.Get(v, "EventKey")),
                "Stage", stage,
                "Entered", entered,
                "Evidence", evidence);
            string dir = OptStr(c, "stateDir", delegate { return StateDir(); });
            GdJson.Write(Path.Combine(dir, ProbeFile), obj);
            // 两句人话逐字(原 lib\ark-decisions.ps1:184/186；PS 原身已随 11-06 刀⑯ 出仓)，档位 OK／WARN 也逐字。
            if (entered)
                return Gd.Out("Written", true, "NoticeLevel", "OK",
                    "NoticeText", "活动图自证：" + stage + " 进得去 → 下一轮照常按活动图刷(" + evidence + ")");
            return Gd.Out("Written", true, "NoticeLevel", "WARN",
                "NoticeText", "活动图自证：" + stage + " 进不去 → 判为还没打通，当日剩余轮改刷长草(" + evidence + ")");
        }
    }
}
