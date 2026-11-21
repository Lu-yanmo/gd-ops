// tools/panel-cs/Vision.SelfTest.cs —— --visiontest：批次 4(Vision.cs)的无头自检。
// 期望串逐字抄自 lib\vision-rescue.ps1 / lib\vision-ark.ps1 的旧实现(改壳前先在活实现上跑过 29 项冒烟对拍，
// 全绿后才搬来这里的期望)—— 这里红了就是移植走样，改 Vision.cs，不改这里的期望(§7 同一条纪律)。
// 批次纪律：不起子进程、不联网、不碰窗口、不写文件 —— 模型回复/签名串/画面现场值全部由场景直接喂进来。
// 特别钉两处 here-string 的"粘连"(旧实现的既有形态，不是笔误)：'位置。3. 绝对不要给' 与 '正中。4. 关闭按钮'
// —— 谁"顺手"给规则补换行，这里立刻红。

using System;
using System.Collections.Generic;

namespace GdPanel
{
    static class VisionSelfTest
    {
        sealed class Box
        {
            public int Total = 0;
            public int Fails = 0;
            public void Chk(string name, Func<bool> test)
            {
                Total++;
                bool ok = false;
                string detail = null;
                try { ok = test(); }
                catch (Exception ex) { ok = false; detail = ex.GetType().Name + ": " + ex.Message; }
                if (!ok)
                {
                    Fails++;
                    GdLog.Line("FAIL " + name + (detail != null ? "：" + detail : ""), "ERROR");
                }
            }
            public int Result(string tag)
            {
                if (Fails == 0) GdLog.Line(tag + " 全绿：" + Total + "/" + Total + " 项", "OK");
                else GdLog.Line(tag + " 有红项：" + Fails + "/" + Total, "ERROR");
                return Fails == 0 ? 0 : 1;
            }
        }

        static Dictionary<string, object> D(object v) { return v as Dictionary<string, object>; }

        static Dictionary<string, object> Inp(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        // 16x9=144 格的粗亮度签名(测试用)：全 20，指定格(1 基)改成别的值 —— 与 tools\test-ark.ps1 的 SigWith 同构
        static string SigAll(int val)
        {
            string[] bits = new string[144];
            for (int i = 0; i < 144; i++) bits[i] = val.ToString();
            return string.Join(",", bits);
        }
        static string SigWith(int[] idxs, int val)
        {
            string[] bits = new string[144];
            for (int i = 0; i < 144; i++) bits[i] = "20";
            foreach (int i in idxs) bits[i - 1] = val.ToString();
            return string.Join(",", bits);
        }

        public static int VisionTest()
        {
            Box b = new Box();
            PromptGroup(b);
            VerdictGroup(b);
            PointGroup(b);
            EndpointGroup(b);
            ChangedGroup(b);
            ReplyGroup(b);
            EntryGroup(b);
            DesktopGroup(b);
            return b.Result("--visiontest");
        }

        // ---------- 提示词 ----------
        static void PromptGroup(Box b)
        {
            string sp = Vision.BuildPrompt("", false);
            b.Chk("默认 head 是端游那段(逐字)", () => sp.StartsWith(
                "你是明日方舟端游自动化流程里的\"看屏幕的人\"。自动化助手(MAA)不认识界面上没收录过的弹窗，有时会一直在原地\n点同一个按钮。"));
            b.Chk("提示词还要求\"只回 JSON + 千分比坐标\"", () => sp.Contains("只输出一个 JSON") && sp.Contains("千分比"));
            // 2026-11-08 刀㉕ 笔(a)：从 tools/test-ark.ps1 [t13] 的源文本格换家而来——那一格原本量六串，其中
            // 四串本族已有格(只输出一个 JSON／千分比 见上一格；clicks 最多 3 个 见领奖档那格；绝对不要给 见规则 3
            // 那两格)，剩「按你的把握从大到小排」与「关闭/取消/暂不/跳过/知道了」两串只在那格被量 ⇒ 这里补成整行逐字，
            // 覆盖面不比原格窄，且比六串分量的 -and 更紧(整行一字改口当场红)。
            b.Chk("规则 2 整行逐字(clicks 最多 3 个→按把握从大到小排→只许关弹窗类按钮)", () => sp.Contains(
                "2. clicks 最多 3 个，按你的把握从大到小排。只允许给\"关闭/取消/暂不/跳过/知道了/X\"这类**只会把弹窗关掉**的位置。"));
            b.Chk("规则 2 与规则 3 之间是无换行粘连(旧 here-string 语义，不许\"顺手补行\")", () => sp.Contains("**只会把弹窗关掉**的位置。3. 绝对不要给"));
            b.Chk("规则 3(默认档)与规则 4 之间同样粘连", () => sp.Contains("即使它在画面正中。4. 关闭按钮"));
            b.Chk("结尾无多余换行(旧实现以 '@ 收尾)", () => sp.EndsWith("6. 不要输出坐标以外的多余字段。") && !sp.EndsWith("\n"));
            b.Chk("默认档\"领取\"和\"不要给\"绑在同一句且只有一句", () => Count(sp, "绝对不要给\"领取\"") == 1);
            b.Chk("默认档不含领奖口径", () => !sp.Contains("已允许"));
            string spA = Vision.BuildPrompt("", true);
            b.Chk("勾了领奖档 → 规则 3 换成允许领取的口径", () => spA.Contains("3. 这一轮已允许\"领取日常奖励\"") && !spA.Contains("绝对不要给\"领取\""));
            b.Chk("领奖档与规则 4 之间同样粘连", () => spA.Contains("绝对不给，即使它在画面正中。4. 关闭按钮"));
            b.Chk("领奖档仍禁花钱/推进进度那类", () => spA.Contains("充值") && spA.Contains("前往参与") && spA.Contains("绝对不给"));
            b.Chk("领奖档其余规则一字不动(点位上限/千分比/拿不准给空数组)", () =>
                spA.Contains("clicks 最多 3 个") && spA.Contains("千分比") && spA.Contains("拿不准也一律给空数组"));
            b.Chk("领奖档规则 3 内部换行 + 第二行行首三空格(逐字)", () => spA.Contains("可以给出它。\n   但\"前往参与\""));
            string spS = Vision.BuildPrompt("你是模拟器里 1999 的画面。", false);
            b.Chk("给了 Scene 就原样替换 head(不裁不补)", () => spS.StartsWith("你是模拟器里 1999 的画面。\n只输出一个 JSON 对象"));
            b.Chk("Scene 分支共用五条规则段", () => spS.EndsWith("6. 不要输出坐标以外的多余字段。") && spS.Contains("**只会把弹窗关掉**的位置。3. 绝对不要给"));
            // 12 刀从已退役的 vision 等值门(场景 prompt/scene-nl)抄来：Scene 原样用、只加一个分隔换行 ——
            // 尾部自带换行时成品里就是两个空行(旧实现不裁；裁尾那一步在靶子定义处)。
            b.Chk("Scene 尾部的换行原样保留(旧实现不裁 Scene)", () =>
                Vision.BuildPrompt("两行场景第一行\n第二行\n", false).StartsWith("两行场景第一行\n第二行\n\n只输出一个 JSON 对象"));

            // ---------- 启动器场景(10-11)：head/body 整段换掉，输出协议不变 ----------
            string spL = Vision.BuildPrompt("ark-launcher", "", false);
            b.Chk("sceneId=ark-launcher → head 换成启动器/更新器那一段", () => spL.StartsWith(
                "你是明日方舟端游**启动器/更新器**自动化流程里的\"看屏幕的人\""));
            b.Chk("启动器档不再出现\"关掉弹窗\"那套规则(规则 3 禁点的东西正好是启动器要点的东西)", () =>
                !spL.Contains("绝对不要给\"领取\"\"前往参与\"\"确定\"\"开始行动\"\"充值\"这类会推进游戏状态的按钮")
                && !spL.Contains("**只会把弹窗关掉**的位置"));
            b.Chk("启动器档白名单逐字：更新/立即下载/开始更新/修复/检查更新/开始游戏/进入游戏/继续", () =>
                spL.Contains("更新、立即下载、开始更新、修复、检查更新、开始游戏、进入游戏、继续"));
            b.Chk("启动器档\"下载中\"这一格必须是 true+空 clicks(点了会打断下载)", () =>
                spL.Contains("clicks 必须给空数组：这时候点哪儿都可能把下载打断"));
            b.Chk("启动器档 blocking 语义改成\"还不能进游戏\"", () => spL.Contains("还不能进游戏"));
            b.Chk("启动器档仍禁花钱/退登录/清缓存那几类", () =>
                spL.Contains("充值") && spL.Contains("退出登录") && spL.Contains("清除缓存"));
            b.Chk("启动器档拿不准 → true+空数组(和默认档的\"给空数组\"方向一致，但含义是等不是放)", () =>
                spL.Contains("拿不准 → blocking 给 true、clicks 给空数组"));
            b.Chk("启动器档输出协议没跑偏(还是 blocking/what/clicks + 千分比)", () =>
                spL.Contains("只输出一个 JSON 对象") && spL.Contains("千分比")
                && spL.Contains("\"clicks\": [{\"x\": 整数, \"y\": 整数, \"why\": \"不超过14字\"}]"));
            b.Chk("启动器档以规则 7 收尾、尾部无多余换行", () => spL.EndsWith("7. 不要输出坐标以外的多余字段。") && !spL.EndsWith("\n"));
            b.Chk("sceneId 传空/传别的/传大小写混的都走默认那条(老调用方一字不受影响)", () =>
                Vision.BuildPrompt(null, "x", false) == Vision.BuildPrompt("x", false)
                && Vision.BuildPrompt("", "x", false) == Vision.BuildPrompt("x", false)
                && Vision.BuildPrompt("ARK-LAUNCHER ", "", false) == spL
                && Vision.BuildPrompt("ark-mumu", "", false).Contains("明日方舟端游自动化流程"));
            // 这条钉的是"两档话术互不渗"：启动器档里不许出现默认档的任何一条规则原文
            b.Chk("启动器档不含默认档的规则 4/5 原文(不是拼在两档之后)", () =>
                !spL.Contains("关闭按钮通常在该弹窗的右上角") && !spL.Contains("拿不准也一律给空数组"));
        }
        static int Count(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        // ---------- verdict 抠取 ----------
        static void VerdictGroup(Box b)
        {
            Dictionary<string, object> v1 = D(Vision.Verdict(Inp("text",
                "{\"blocking\":true,\"what\":\"中秋活动弹窗\",\"clicks\":[{\"x\":912,\"y\":233,\"why\":\"右上角关闭\"}]}")));
            b.Chk("按格式回 → 抠出弹窗与坐标", () =>
                Bool(v1["Found"]) && Bool(v1["Blocking"]) && ((string)v1["What"]).Contains("弹窗")
                && IntOf(v1["X"]) == 912 && IntOf(v1["Y"]) == 233);
            string fence = "好的，我看了一下：\n```json\n{\"blocking\":true,\"what\":\"公告\",\"clicks\":[{\"x\":80,\"y\":90,\"why\":\"知道了\"}]}\n```";
            Dictionary<string, object> v2 = D(Vision.Verdict(Inp("text", fence)));
            b.Chk("裹了 markdown 代码块和一句话也能抠出 JSON", () => Bool(v2["Found"]) && IntOf(v2["X"]) == 80);
            Dictionary<string, object> v3 = D(Vision.Verdict(Inp("text", "{\"blocking\":false,\"what\":\"主界面\",\"clicks\":[]}")));
            b.Chk("没有弹窗时 blocking=false 且 X 为空", () =>
                Bool(v3["Found"]) && !Bool(v3["Blocking"]) && v3["X"] == null && Gd.Str(v3["Why"]) == "");
            b.Chk("回复不成 JSON → Found:false(不拿猜测当依据)", () =>
                !Bool(D(Vision.Verdict(Inp("text", "我看有个弹窗，你点右上角试试")))["Found"]));
            b.Chk("空回复不炸", () => !Bool(D(Vision.Verdict(Inp("text", "")))["Found"]));
            b.Chk("what 里带半个括号也只抠第一个完整对象", () =>
                Bool(D(Vision.Verdict(Inp("text", "{\"blocking\":true,\"what\":\"带{的花活\",\"clicks\":[]}")))["Found"]));
            Dictionary<string, object> vMany = D(Vision.Verdict(Inp("text",
                "{\"blocking\":true,\"what\":\"公告\",\"clicks\":[{\"x\":null,\"y\":null,\"why\":\"没坐标\"},{\"x\":700,\"y\":200,\"why\":\"右上角关闭\"},{\"x\":\"abc\",\"y\":300,\"why\":\"不是数\"},{\"x\":800,\"y\":210,\"why\":\"备用\"},{\"x\":900,\"y\":220,\"why\":\"备用2\"},{\"x\":950,\"y\":230,\"why\":\"超标的第4个\"}]}")));
            object[] clicks = Gd.List(vMany["Clicks"]);
            b.Chk("Clicks 只留整数点位、最多 3 个(坏点位不带走好点位)", () =>
                clicks.Length == 3 && IntOf(Gd.Get(clicks[0], "X")) == 700 && IntOf(Gd.Get(clicks[1], "X")) == 800);
            b.Chk("X/Y/Why 恒等于第 1 个点位(老代码与 t13 认这三个字段)", () =>
                IntOf(vMany["X"]) == 700 && IntOf(vMany["Y"]) == 200 && ((string)vMany["Why"]).Contains("关闭"));
            Dictionary<string, object> vBad = D(Vision.Verdict(Inp("text", "{\"blocking\":true,\"what\":\"x\",\"clicks\":[{\"x\":null,\"y\":null}]}")));
            b.Chk("点位全坏 → Clicks 空数组、X 为 null", () =>
                Gd.List(vBad["Clicks"]).Length == 0 && vBad["X"] == null);
            // 12 刀从 v/prose 抄来：JSON 前后都夹人话(结尾那句也得忽略)。
            b.Chk("JSON 前后都夹人话 → 仍只抠对象本身", () =>
            {
                Dictionary<string, object> vp = D(Vision.Verdict(Inp("text",
                    "我看了一下。{\"blocking\":true,\"what\":\"活动弹窗\",\"clicks\":[{\"x\":812,\"y\":171,\"why\":\"右上角 X\"}]} 就这些。")));
                return Bool(vp["Found"]) && IntOf(vp["X"]) == 812 && IntOf(vp["Y"]) == 171;
            });
            // 从 v/array 抄来：顶层是数组没有对象 → 不算回复(与"空白/空回复"同归 Found:false)。
            b.Chk("整段是数组没有对象 → 不算回复", () => !Bool(D(Vision.Verdict(Inp("text", "[1,2,3]")))["Found"]));
            // 从 v/cjk-why 抄来：0/0 是合法千分比(左上角)，"0 当没给"的坑只能踩在换算层，不能在抠取层丢。
            b.Chk("坐标为 0/0 照收(0 不等于\"没给\")", () =>
            {
                Dictionary<string, object> vz = D(Vision.Verdict(Inp("text",
                    "{\"blocking\":true,\"what\":\"协议页\",\"clicks\":[{\"x\":0,\"y\":0,\"why\":\"左上角\"}]}")));
                object[] cs = Gd.List(vz["Clicks"]);
                return Bool(vz["Found"]) && IntOf(vz["X"]) == 0 && IntOf(vz["Y"]) == 0
                    && cs.Length == 1 && Gd.Str(Gd.Get(cs[0], "Why")) == "左上角";
            });
            // 从 v/badpoint 抄来：x=null、y=文字 这一对坏点位剔掉后，好点位必须留下(坏的不带走好的)。
            b.Chk("点位 x 为空/y 是文字 → 只剔这一枚，后面的好点位照留", () =>
            {
                Dictionary<string, object> vb = D(Vision.Verdict(Inp("text",
                    "{\"blocking\":true,\"what\":\"x\",\"clicks\":[{\"x\":null,\"y\":\"右上角\",\"why\":\"坏\"},{\"x\":500,\"y\":400,\"why\":\"好\"}]}")));
                object[] cs = Gd.List(vb["Clicks"]);
                return cs.Length == 1 && IntOf(Gd.Get(cs[0], "X")) == 500 && IntOf(Gd.Get(cs[0], "Y")) == 400;
            });
        }

        // ---------- 千分比 → 点击点 ----------
        static void PointGroup(Box b)
        {
            // 1920x1080 整窗位图：客户区原点 (8,31)，客户区 1904x1041 —— 与 test-ark 的夹具同一份
            Dictionary<string, object> shot = Inp("imgW", 1920, "imgH", 1080, "clientLeft", 8, "clientTop", 31, "clientW", 1904, "clientH", 1041);
            Dictionary<string, object> p1 = D(Vision.ClickPoint(Merge(shot, Inp("nx", 500, "ny", 500, "max", 1000, "why", ""))));
            b.Chk("千分比 → 客户区坐标(要减掉标题栏/边框原点)", () =>
                Bool(p1["Found"]) && IntOf(p1["ClientX"]) == 952 && IntOf(p1["ClientY"]) == 509
                && IntOf(p1["BitmapX"]) == 960 && IntOf(p1["BitmapY"]) == 540);
            b.Chk("指到标题栏上会被拒(不给越界点击)", () =>
                !Bool(D(Vision.ClickPoint(Merge(shot, Inp("nx", 500, "ny", 10, "max", 1000, "why", ""))))["Found"]));
            b.Chk("模型没给坐标时不给点击点", () =>
                !Bool(D(Vision.ClickPoint(Merge(shot, Inp("nx", null, "ny", null, "max", 1000, "why", ""))))["Found"]));
            b.Chk("坐标超出 0-1000 视为胡说，拒掉", () =>
                !Bool(D(Vision.ClickPoint(Merge(shot, Inp("nx", 5000, "ny", 5, "max", 1000, "why", ""))))["Found"]));
            b.Chk("点右下角最后一个像素也算越界(>= 不是 >)", () =>
                !Bool(D(Vision.ClickPoint(Merge(shot, Inp("nx", 1000, "ny", 1000, "max", 1000, "why", ""))))["Found"]));
            Dictionary<string, object> dev = Inp("imgW", 1280, "imgH", 720, "clientLeft", 0, "clientTop", 0, "clientW", 1280, "clientH", 720);
            Dictionary<string, object> pm = D(Vision.ClickPoint(Merge(dev, Inp("nx", 500, "ny", 500, "max", 1000, "why", "右上角 X"))));
            b.Chk("模拟器夹具(客户区=整屏)算出来就是设备像素", () =>
                Bool(pm["Found"]) && IntOf(pm["ClientX"]) == 640 && IntOf(pm["ClientY"]) == 360 && Gd.Str(pm["Why"]) == "右上角 X");

            // 12 刀从 pt/zero 抄来：nx=0 换算到位图是 0，减掉客户区原点(8,31)后为负 → 越界拒掉。
            b.Chk("千分比给 0 → 减客户区原点后为负，拒掉", () =>
                !Bool(D(Vision.ClickPoint(Merge(shot, Inp("nx", 0, "ny", 0, "max", 1000, "why", "左上"))))["Found"]));
            // 从 pt/near-max 抄来：999/999 在整窗图上算出的 x/y 都越过客户区边(标题栏吃掉的那截) → 拒。
            b.Chk("999,999 在含标题栏的整窗图上越界(不能只判 1000)", () =>
                !Bool(D(Vision.ClickPoint(Merge(shot, Inp("nx", 999, "ny", 999, "max", 1000, "why", "边角"))))["Found"]));
            // 从 pt/last-in 抄来：客户区里最后一段(995,962)合法，算出来 1902,1008。
            b.Chk("客户区最后一段 995,962 → 1902,1008", () =>
            {
                Dictionary<string, object> p = D(Vision.ClickPoint(Merge(shot, Inp("nx", 995, "ny", 962, "max", 1000, "why", "客户区内最后一段"))));
                return Bool(p["Found"]) && IntOf(p["ClientX"]) == 1902 && IntOf(p["ClientY"]) == 1008
                    && IntOf(p["BitmapX"]) == 1910 && IntOf(p["BitmapY"]) == 1039;
            });
            // 从 pt/nonnum 抄来：文字坐标不是数 → 拒(不猜、不默认 0)。
            b.Chk("文字坐标(\"abc\"/\"12\")不是数 → 拒掉", () =>
                !Bool(D(Vision.ClickPoint(Merge(shot, Inp("nx", "abc", "ny", "12", "max", 1000, "why", "文字坐标"))))["Found"]));
            // 从 pt/emu-br 抄来：模拟器整屏(2560x1440)上 999,999 合法 = 设备像素 2557,1439 ——
            // 与整窗图那一格相反，正是"减不减原点"的分水岭。
            b.Chk("模拟器整屏 999,999 → 2557,1439(与整窗图相反)", () =>
            {
                Dictionary<string, object> emu = Inp("imgW", 2560, "imgH", 1440, "clientLeft", 0, "clientTop", 0, "clientW", 2560, "clientH", 1440);
                Dictionary<string, object> p = D(Vision.ClickPoint(Merge(emu, Inp("nx", 999, "ny", 999, "max", 1000, "why", "整屏右下"))));
                return Bool(p["Found"]) && IntOf(p["ClientX"]) == 2557 && IntOf(p["ClientY"]) == 1439;
            });
            // 从 pt/max500 抄来：Max=500 时 250/500 与 500/1000 是同一个点(分母语义，不是裁剪)。
            b.Chk("Max=500 时 250 与 500/1000 等价", () =>
            {
                Dictionary<string, object> p = D(Vision.ClickPoint(Merge(shot, Inp("nx", 250, "ny", 250, "max", 500, "why", "X"))));
                return Bool(p["Found"]) && IntOf(p["ClientX"]) == 952 && IntOf(p["ClientY"]) == 509
                    && IntOf(p["NormX"]) == 250 && IntOf(p["NormY"]) == 250;
            });
        }
        static Dictionary<string, object> Merge(Dictionary<string, object> a, Dictionary<string, object> b2)
        {
            Dictionary<string, object> d = new Dictionary<string, object>(a);
            foreach (KeyValuePair<string, object> kv in b2) d[kv.Key] = kv.Value;
            return d;
        }

        // ---------- 端点归一化 ----------
        static void EndpointGroup(Box b)
        {
            b.Chk("基础地址以 /v1 结尾会补成 /chat/completions(末尾多几个斜杠也一样)", () =>
                Url(Inp("base", "https://example.com/compatible-mode/v1/")) == "https://example.com/compatible-mode/v1/chat/completions");
            b.Chk("已经写全的地址不会再补第二个", () =>
                Url(Inp("base", "https://example.com/compatible-mode/v1/chat/completions")) == "https://example.com/compatible-mode/v1/chat/completions");
            b.Chk("地址留空回落到百炼公共端点", () =>
                Url(Inp("base", "")) == "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions");
            // 12 刀从 ep/blank 抄来：只有空白与全空同一条回落(旧实现先 Trim 再判空)。
            b.Chk("只有空白也回落到百炼公共端点", () =>
                Url(Inp("base", "   ")) == "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions");
            b.Chk("大小写不同的收尾也算写全(/CHAT/COMPLETIONS 不重复补)", () =>
                Url(Inp("base", "https://example.com/x/CHAT/COMPLETIONS")) == "https://example.com/x/CHAT/COMPLETIONS");
        }
        static string Url(Dictionary<string, object> inp) { return (string)D(Vision.Endpoint(inp))["Url"]; }

        // ---------- 画面变没变 ----------
        static void ChangedGroup(Box b)
        {
            // 旧夹具的"底"是 20(SigWith @() 0 里那个 0 只给指定格，空 idxs 时整串仍是 20) —— 别改成 0，改了噪点那格就变 20 档
            string sA = SigAll(20);
            Dictionary<string, object> c0 = D(Vision.ShotChanged(Inp("beforeSig", sA, "afterSig", sA, "minCells", 2, "minDelta", 3)));
            b.Chk("同一张画面 → 判\"没变\"", () => Bool(c0["Known"]) && !Bool(c0["Changed"]) && IntOf(c0["Cells"]) == 0
                && (string)c0["Detail"] == "画面变了 0 格(判据：≥2 格、每格 ≥3 档)");
            Dictionary<string, object> c1 = D(Vision.ShotChanged(Inp("beforeSig", sA, "afterSig", SigWith(new[] { 7 }, 21), "minCells", 2, "minDelta", 3)));
            b.Chk("只有一格抖 1 档(噪点动画) → 仍判\"没变\"", () => !Bool(c1["Changed"]));
            Dictionary<string, object> c2 = D(Vision.ShotChanged(Inp("beforeSig", sA, "afterSig", SigWith(new[] { 60, 61, 69, 70 }, 3), "minCells", 2, "minDelta", 3)));
            b.Chk("弹窗那一块整体暗下去 → 判\"变了\"、数得出 4 格", () =>
                Bool(c2["Known"]) && Bool(c2["Changed"]) && IntOf(c2["Cells"]) == 4);
            Dictionary<string, object> c3 = D(Vision.ShotChanged(Inp("beforeSig", "", "afterSig", sA, "minCells", 2, "minDelta", 3)));
            b.Chk("拿不到签名(截图那步就没成)时不硬判", () =>
                !Bool(c3["Known"]) && !Bool(c3["Changed"]) && IntOf(c3["Cells"]) == -1
                && (string)c3["Detail"] == "没有画面签名可比对(截图那一步就没成)");
            Dictionary<string, object> c4 = D(Vision.ShotChanged(Inp("beforeSig", sA, "afterSig", "5,5,5", "minCells", 2, "minDelta", 3)));
            b.Chk("两次画面格数不同 → 说不清，别硬判", () =>
                !Bool(c4["Known"]) && IntOf(c4["Cells"]) == -1
                && (string)c4["Detail"] == "两次画面尺寸不同(144 格 vs 3 格)");
            // 12 刀从 sc/two 抄来：恰好 2 格各变 4 档 = 刚好压线，>= minCells 必须算"变了"。
            b.Chk("恰好 2 格变 4 档 → 判\"变了\"(等于阈值也算)", () =>
            {
                Dictionary<string, object> c = D(Vision.ShotChanged(Inp("beforeSig", sA, "afterSig", SigWith(new[] { 1, 2 }, 24), "minCells", 2, "minDelta", 3)));
                return Bool(c["Known"]) && Bool(c["Changed"]) && IntOf(c["Cells"]) == 2;
            });
            // 从 sc/thresh 抄来：阈值是可调的(MinCells=1/MinDelta=2)，话术里的数字跟着参数走。
            b.Chk("阈值可调(1 格 / 2 档)时 2 格各 3、2 档也算变了", () =>
            {
                Dictionary<string, object> c = D(Vision.ShotChanged(Inp("beforeSig", "10,50,20,20,20,20",
                    "afterSig", "13,52,20,20,20,20", "minCells", 1, "minDelta", 2)));
                return Bool(c["Known"]) && Bool(c["Changed"]) && IntOf(c["Cells"]) == 2
                    && (string)c["Detail"] == "画面变了 2 格(判据：≥1 格、每格 ≥2 档)";
            });
        }

        // ---------- ask.py stdout 回扫 ----------
        static void ReplyGroup(Box b)
        {
            Dictionary<string, object> r1 = D(Vision.Reply(Inp("out", "site 版本警告一行\n{\"ok\":true,\"text\":\"hi\",\"http_status\":200}")));
            b.Chk("解释器前面先打了警告 → 仍取到那一个 JSON", () =>
                Bool(r1["Found"]) && Bool(Gd.Get(r1["Parsed"], "ok")) && IntOf(Gd.Get(r1["Parsed"], "http_status")) == 200);
            b.Chk("整段没有 JSON → Found:false", () => !Bool(D(Vision.Reply(Inp("out", "no json here")))["Found"]));
            b.Chk("空输出不炸", () => !Bool(D(Vision.Reply(Inp("out", "")))["Found"]));
            Dictionary<string, object> r4 = D(Vision.Reply(Inp("out", "{bad\n{\"ok\":false}")));
            b.Chk("半行坏 JSON 被跳过、取后面能解析的那行", () =>
                Bool(r4["Found"]) && !Bool(Gd.Get(r4["Parsed"], "ok")));
            // 12 刀从 rp/fenced 抄来：```json 围栏各自成行时，围栏里那一行要能取到。
            b.Chk("```json 围栏单独成行 → 取围栏里那一行", () =>
            {
                Dictionary<string, object> r = D(Vision.Reply(Inp("out", "```json\n{\"ok\":true,\"http_status\":200}\n```")));
                return Bool(r["Found"]) && Bool(Gd.Get(r["Parsed"], "ok")) && IntOf(Gd.Get(r["Parsed"], "http_status")) == 200;
            });
            // 从 rp/two 抄来：两段都合法时取**第一个**(后面的不覆盖)。
            b.Chk("两段合法 JSON → 取第一个", () =>
            {
                Dictionary<string, object> r = D(Vision.Reply(Inp("out", "{\"ok\":true,\"n\":1}\n{\"ok\":false,\"n\":2}")));
                return Bool(r["Found"]) && IntOf(Gd.Get(r["Parsed"], "n")) == 1;
            });
            // 从 rp/small 抄来：字段不全(429 那形)也照解析，缺键不丢(字段补齐是调用方的事)。
            b.Chk("字段不全(429 那形)也照解析", () =>
            {
                Dictionary<string, object> r = D(Vision.Reply(Inp("out", "{\"ok\":true,\"text\":\"\",\"http_status\":429,\"attempts\":3}")));
                return Bool(r["Found"]) && IntOf(Gd.Get(r["Parsed"], "http_status")) == 429
                    && IntOf(Gd.Get(r["Parsed"], "attempts")) == 3;
            });
        }

        // ---------- 救火账本字段合成 ----------
        static void EntryGroup(Box b)
        {
            Dictionary<string, object> e1 = D(Vision.Entry(Inp("time", "2026-09-26 12:00:00", "game", "ark", "target", "",
                "fallbackTarget", "arkfb", "tag", "stuck1", "tried", true, "done", false, "detail", "第一行\n第二行")));
            b.Chk("-Target 没给 → 回退到结果自带的靶子名", () => (string)e1["Target"] == "arkfb");
            b.Chk("detail 换行压平成 ' | '(账本是给人翻的)", () => (string)e1["Detail"] == "第一行 | 第二行");
            b.Chk("CRLF 也一并压平", () =>
                (string)D(Vision.Entry(Inp("detail", "a\r\nb")))["Detail"] == "a | b");
            Dictionary<string, object> e2 = D(Vision.Entry(Inp("time", "t", "game", "r1999", "target", "mumu",
                "fallbackTarget", "arkfb", "tag", "", "tried", false, "done", false, "detail", "")));
            b.Chk("给了 -Target 就以它为准(账本归属是调用方的事)", () =>
                (string)e2["Target"] == "mumu" && (string)e2["Game"] == "r1999" && !Bool(e2["Tried"]));
        }

        // ---------- 端游点击环境快照的文案拼装 ----------
        static void DesktopGroup(Box b)
        {
            Dictionary<string, object> d0 = D(Vision.DesktopState(Inp("desk", "", "hwndFound", false, "onFg", false,
                "fgProcess", "", "iconic", false, "visible", true, "rectText", "", "cursor", "")));
            b.Chk("桌面名读不到 → 说\"无法判断\"，不写成锁屏", () =>
                d0["Locked"] == null && !Bool(d0["LockedKnown"])
                && (string)d0["Detail"] == "输入桌面=读不到(无法判断是否锁屏)；端游窗口没找到");
            Dictionary<string, object> d1 = D(Vision.DesktopState(Inp("desk", "Default", "hwndFound", false, "onFg", false,
                "fgProcess", "", "iconic", false, "visible", true, "rectText", "", "cursor", "100,200")));
            b.Chk("未锁屏 + 没窗口 + 有光标", () =>
                Bool(d1["Locked"]) == false && (string)d1["Detail"] == "输入桌面=Default(未锁屏)；端游窗口没找到；光标在 100,200");
            Dictionary<string, object> d2 = D(Vision.DesktopState(Inp("desk", "Winlogon", "hwndFound", true, "onFg", false,
                "fgProcess", "msedge", "iconic", false, "visible", true, "rectText", "0,0 1936x1056", "cursor", "")));
            b.Chk("屏保/登录桌面 + 前台被人占着 → 两句都要说清", () =>
                Bool(d2["Locked"]) && (string)d2["Detail"] ==
                "输入桌面=Winlogon(会话不在正常桌面，鼠标键盘进不来)；端游在前台=否(前台是 msedge)；端游窗口状态=0,0 1936x1056");
            Dictionary<string, object> d3 = D(Vision.DesktopState(Inp("desk", "default", "hwndFound", true, "onFg", true,
                "fgProcess", "", "iconic", true, "visible", false, "rectText", "", "cursor", "")));
            b.Chk("桌面名比 Default 用忽略大小写(旧 -ne 语义)；最小化/不可见并排", () =>
                Bool(d3["Locked"]) == false && (string)d3["Detail"] ==
                "输入桌面=Default(未锁屏)；端游在前台=是；端游窗口状态=最小化/不可见");
            Dictionary<string, object> d4 = D(Vision.DesktopState(Inp("desk", "Default", "hwndFound", true, "onFg", false,
                "fgProcess", "", "iconic", false, "visible", true, "rectText", "", "cursor", "")));
            b.Chk("前台进程叫不出 → \"未知窗口\"；窗口状态全正常时不硬凑\"端游窗口状态=\"前缀", () =>
                (string)d4["Detail"] == "输入桌面=Default(未锁屏)；端游在前台=否(前台是 未知窗口)");
            // 12 刀从 ds/fg-yes 抄来：前台是端游时的完整一句(矩形与光标都要在)。
            b.Chk("前台是端游 + 矩形 + 光标", () =>
            {
                Dictionary<string, object> d = D(Vision.DesktopState(Inp("desk", "Default", "hwndFound", true, "onFg", true,
                    "fgProcess", "Arknights", "iconic", false, "visible", true, "rectText", "0,0 1936x1056", "cursor", "500,600")));
                return (string)d["Detail"] == "输入桌面=Default(未锁屏)；端游在前台=是；端游窗口状态=0,0 1936x1056；光标在 500,600";
            });
            // 从 ds/iconic-invis 抄来：最小化+不可见+矩形三样并排、中间用 '/' 连(顺序固定)。
            b.Chk("最小化+不可见+矩形三样并排", () =>
            {
                Dictionary<string, object> d = D(Vision.DesktopState(Inp("desk", "default", "hwndFound", true, "onFg", false,
                    "fgProcess", "explorer", "iconic", true, "visible", false, "rectText", "-32000,-32000 168x29", "cursor", "0,0")));
                return (string)d["Detail"] == "输入桌面=Default(未锁屏)；端游在前台=否(前台是 explorer)；端游窗口状态=最小化/不可见/-32000,-32000 168x29；光标在 0,0";
            });
        }

        // ---------- 小工具 ----------
        static bool Bool(object v)
        {
            if (v == null) return false;
            if (v is bool) return (bool)v;
            return Gd.Str(v).Trim().Length > 0 && Gd.Str(v).Trim() != "0" && !Gd.Str(v).Trim().Equals("False", StringComparison.OrdinalIgnoreCase);
        }
        static int IntOf(object v)
        {
            try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return int.MinValue; }
        }
    }
}
