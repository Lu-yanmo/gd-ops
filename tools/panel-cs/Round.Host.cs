// tools/panel-cs/Round.Host.cs —— 转接刀·运行面刀⑪(2026-11-05)：lib\rust-round.ps1 的 ctx 三枚
// (Get-GdRoundCtx / Get-GdRoundMaaRunPayload / Get-GdRoundM9aRunPayload)的 C# 直身，出口＝verb build-round-ctx。
//
// 这一件只干一件事：把"一轮一定稿"的那枚轮 ctx 按**同一份名册、同一个声明序**装配出来。
// 名册 15 枚一枚不多一枚不少(journal 的 init 与 ctx 做全等校验，事后补不进任何一格 —— 这是真身头注那句
// "换 ctx 就得起新 journal"的形状来历)。maaRun 的 paths 六枚＋runtime 七枚、m9aRun 的 paths 三枚＋conn 两枚＋
// instance 两枚＋package＋runtime 六/七枚，键名与 config\settings.json 逐字同名(核内 maarun_parse／
// round_ctx_parse 用的是同一把尺)。
//
// 四条硬约束(逐条都是**在册账**，不是本件自创)：
//   1. **取键逐枚点名**。$S.runtime 整块里住着视觉兜底的 API key(visionApiKey)，序列化进 body 等于把 key
//      递进核、递进 ctx/journal 两本会留盘的账。所以整块 settings 绝不进 body：本件吃的是壳给的
//      **旋钮**(ids/gates/opt/四个开关/nameField/noticeOwner/tag/logPath/ownerPid)，settings 由本件现读
//      (或自检夹具给)，取键按名册逐枚点名。
//   2. **pid 必须由壳写明**(body 的 ownerPid)。真身取的是 `$PID`——撤锁只撤自己登记那一份，核自己的进程号
//      在第一发 ask 之后就不是它了(tools\rustcore\src\main.rs 的 round_ctx_parse 把这一档写成了**必填且不许
//      回落**的 Err)。本件**不补闸**：缺键就是 Gd.Lng 的 0，由核内那句 Err 定罪——同一句判语只留一份事实源。
//   3. **真身没有的闸不许加**。nameField 只认 Id/ShortName、opt 必须是对象这两档，真身侧靠的是
//      [string]/[hashtable] 参数约束与核内 round_ctx_parse，本件一律如实出数、不另立判据。
//   4. **m9aRun 给不出"真值"，只给"不许退化的回落档"**(B-4② 实测推翻题面那一结，§11.65)：
//      instance 两格与 conn.serial 留空由核内段 5(round_m9a_apply_decision)拿判定回执就地覆盖；
//      conn.adb **不许留空**——核内 m9a_scen_core 的硬闸是 m9aDir/adb 为空即 Err，而 Err 之后整份载荷
//      退化成默认体(paths/package/超时全丢，段 5 补不回其余)。回落档取 MuMu 的 Adb 算术(纯路径算术、
//      不起进程、不读实例配置)，与真身 Get-M9AConnection 缺 AdbPath 时同一处来路(那枚 PS 真身已随 11-07 刀⑱ 笔(a) 出仓，回落算术只住核内这一处)。
//      m9aCliArgs 走三态：settings 里这枚键**没写**＝真身"没填"档＝开命令行入口 ⇒ 干脆不加键，不许塌成 false。
//
// 三处口径分岔(照登不粉饰，均登记 unobserved)：
//   · 第一处分岔**已消**(11-16 摘闸)：原文登记的是 11-06 刀⑪ 把真身那句
//     `Get-Command Get-GdMuMu -EA SilentlyContinue` 在没有命令表的 C# 侧翻成**在位性**判据
//     `<root>\lib\mumu.ps1`。11-07 刀⑯ 笔(d)-2 退役那本模块时把该文件从树上删了，闸没随动，
//     于是这处分岔在生产上不是"两侧各漂一半"而是**恒退化**(adb 恒空)——账见下面 AdbOf 的史注。
//     现两侧同源：回落档只看 settings 的 paths 算术，与 PS 装载面无关。
//   · `[bool]`／`[int]` 的转换语义按 PS 口径实现(逐档见下面两枚助手的注释)；清单/对象这类真身本来到不了的形
//     一律抛，抛到 Cli 的通用 catch，由 Invoke-GdCli 那句"ok 否则 throw error 原话"送回 dispatch 的**同一个**
//     catch ⇒ 话术不因换核分岔。
//   · Split-Path -Leaf 对根路径的读数(5.1 与 7 两引擎实测同形)：'C:\' 与 '\' **都回 'C:\'**、'C:' 回**当前盘的
//     目录叶**(那是机器现场状态)、空串**当场抛**(真身那句 `if ($logFile)` 就是为这一档挡的)。本件对 'C:\' 回
//     'C:'、对 '\' 与空串回空串、一律不抛。日志路径不会是盘符根，这一档只在非生产形态分岔，不补闸；
//     分岔的两枚读数由 --roundtest 段 33 那格按**实测形**钉住(不是按推断)。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GdPanel
{
    static class RoundHost
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ==================== verb: build-round-ctx ====================

        internal static object BuildCtx(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = Round.View(raw);
            Dictionary<string, object> s = SettingsOf(c);
            Dictionary<string, object> paths = CiMap(Gd.Get(s, "paths"));
            Dictionary<string, object> runtime = CiMap(Gd.Get(s, "runtime"));
            Dictionary<string, object> re1999 = CiMap(Gd.Get(s, "re1999"));

            List<string> ids = new List<string>();
            foreach (object x in Gd.List(Gd.Get(c, "ids"))) ids.Add(Gd.Str(x));

            List<object> gates = new List<object>();
            foreach (object g in Gd.List(Gd.Get(c, "gates")))
            {
                if (!Gd.Truthy(g)) continue;   // 真身 `$Gates | Where-Object { $_ }`：空项静默滤，不算错
                Dictionary<string, object> gi = CiMap(g);
                gates.Add(Gd.Out("id", Gd.Str(Gd.Get(gi, "Id")), "verb", Gd.Str(Gd.Get(gi, "Verb"))));
            }

            string logPath = Gd.Str(Gd.Get(c, "logPath"));

            return Gd.Out(
                "ids", ids,
                "gates", gates,
                "opt", OptOf(c),
                "force", PsBool(Gd.Get(c, "force")),
                "dryRun", PsBool(Gd.Get(c, "dryRun")),
                "skipCleanup", PsBool(Gd.Get(c, "skipCleanup")),
                "noElevate", PsBool(Gd.Get(c, "noElevate")),
                "nameField", Round.OptStr(c, "nameField", delegate { return "Id"; }),
                "noticeOwner", Round.OptStr(c, "noticeOwner", delegate { return "每日"; }),
                "tag", Round.OptStr(c, "tag", delegate { return "manual"; }),
                "logLeaf", LeafOf(logPath),
                "logPath", logPath,
                "pid", Gd.Lng(Gd.Get(c, "ownerPid")),
                "maaRun", MaaRun(paths, runtime),
                "m9aRun", M9aRun(s, paths, runtime, re1999));
        }

        // ==================== 名册两格 ====================

        // ctx 的 maaRun(paths 六枚＋runtime 七枚)：与核内 maarun_parse 同把名册，逐枚点名取键。
        static Dictionary<string, object> MaaRun(Dictionary<string, object> paths, Dictionary<string, object> runtime)
        {
            List<string> benign = new List<string>();
            foreach (object bm in Gd.List(Gd.Get(runtime, "benignMaaErrors"))) benign.Add(Gd.Str(bm));
            return Gd.Out(
                "paths", Gd.Out(
                    "maaDir", Gd.Str(Gd.Get(paths, "maaDir")),
                    "maaExe", Gd.Str(Gd.Get(paths, "maaExe")),
                    "maaConfig", Gd.Str(Gd.Get(paths, "maaConfig")),
                    "maaGuiLog", Gd.Str(Gd.Get(paths, "maaGuiLog")),
                    "maaAsstLog", Gd.Str(Gd.Get(paths, "maaAsstLog")),
                    "arknightsExe", Gd.Str(Gd.Get(paths, "arknightsExe"))),
                "runtime", Gd.Out(
                    "arkStartTimeoutMinutes", PsInt(Gd.Get(runtime, "arkStartTimeoutMinutes")),
                    "arkTimeoutMinutes", PsInt(Gd.Get(runtime, "arkTimeoutMinutes")),
                    "stallTimeoutMinutes", PsInt(Gd.Get(runtime, "stallTimeoutMinutes")),
                    "idleConfirmSeconds", PsInt(Gd.Get(runtime, "idleConfirmSeconds")),
                    "abortOnMaaStallWarning", PsBool(Gd.Get(runtime, "abortOnMaaStallWarning")),
                    "visionMaxRescue", PsInt(Gd.Get(runtime, "visionMaxRescue")),
                    "benignMaaErrors", benign));
        }

        // ctx 的 m9aRun(paths 三枚＋conn 两枚＋instance 两枚＋package＋runtime 六/七枚)。
        static Dictionary<string, object> M9aRun(Dictionary<string, object> s, Dictionary<string, object> paths,
                                                 Dictionary<string, object> runtime, Dictionary<string, object> re1999)
        {
            Dictionary<string, object> rt = new Dictionary<string, object>();
            rt["m9aInstanceArg"] = Gd.Str(Gd.Get(runtime, "m9aInstanceArg"));
            rt["m9aForceStart"] = PsBool(Gd.Get(runtime, "m9aForceStart"));
            rt["visionMaxRescue"] = PsInt(Gd.Get(runtime, "visionMaxRescue"));
            rt["r1999StartTimeoutMinutes"] = PsInt(Gd.Get(runtime, "r1999StartTimeoutMinutes"));
            rt["r1999TimeoutMinutes"] = PsInt(Gd.Get(runtime, "r1999TimeoutMinutes"));
            rt["stallTimeoutMinutes"] = PsInt(Gd.Get(runtime, "stallTimeoutMinutes"));
            object cli = Gd.Get(runtime, "m9aCliArgs");
            if (cli != null) rt["m9aCliArgs"] = PsBool(cli);   // 三态：键没写就是不加键，不许塌成 false
            return Gd.Out(
                "paths", Gd.Out(
                    "m9aDir", Gd.Str(Gd.Get(paths, "m9aDir")),
                    "m9aExe", Gd.Str(Gd.Get(paths, "m9aExe")),
                    "m9aLogDir", Gd.Str(Gd.Get(paths, "m9aLogDir"))),
                "conn", Gd.Out("adb", AdbOf(s), "serial", ""),
                "instance", Gd.Out("id", "", "name", ""),
                "package", Gd.Str(Gd.Get(re1999, "package")),
                "runtime", rt);
        }

        // 回落档那枚 adb：复用 Gate.Host.cs 里 Get-GdMuMu 的直身(MuMuPaths 的 Adb 格)，不另抄一份路径算术。
        // 真身那侧 try/catch 只包"调用"这一步：拿不到就回空串，不抛(空串的账由核内 m9a_scen_core 结)。
        // ⚠ 改口(11-16 现场缺陷修复)：此处原有第二道闸 `if (!File.Exists(<root>\lib\mumu.ps1)) return "";`
        //   —— 它是 11-06 刀⑪ 把真身那句 `Get-Command Get-GdMuMu` 翻成在位性判据而来。11-07 刀⑯ 笔(d)-2
        //   把 lib\mumu.ps1 整本退役(文件从树上删除)时这道闸没随动：生产树上它恒假 ⇒ conn.adb 恒空 ⇒
        //   核内 m9a_scen_core 恒 Err ⇒ 段 5 之前整份载荷塌成默认体 ⇒ r1999 连班 error(11-07 06:15 首班起)。
        //   摘闸不留换靶：MuMuPaths 是纯路径算术、不起进程、不读实例配置，对 PS 装载面已零依赖，
        //   任何"换一枚仍在那儿的文件来挡"都是给退役壳换靶续命。GdEnv.Init 仍要留——现读 config\settings.json 吃 Root。
        static string AdbOf(Dictionary<string, object> s)
        {
            try
            {
                GdEnv.Init(null);
                return Gd.Str(Gd.Get(GateHost.MuMuPaths(s), "Adb")) ?? "";
            }
            catch { return ""; }
        }

        // ==================== 入参侧助手 ====================

        // settings 事实源：body 给了夹具(settings)就用夹具，否则现读 config\settings.json
        // (连那枚进程内缓存一起复用 Round.SettingsNow —— 另起一份"只给 ctx 用"的读数件就是本主线在删的双账)。
        static Dictionary<string, object> SettingsOf(Dictionary<string, object> c)
        {
            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "settings"));
            return given != null ? given : Round.SettingsNow();
        }

        // opt 原样透传(键 stringify、值不碰)：真身那侧是调用方手构的 hashtable，本件不给它加判据。
        static Dictionary<string, object> OptOf(Dictionary<string, object> c)
        {
            Dictionary<string, object> outOpt = new Dictionary<string, object>();
            Dictionary<string, object> src = Gd.Map(Gd.Get(c, "opt"));
            if (src == null) return outOpt;
            foreach (KeyValuePair<string, object> kv in src) outOpt[Gd.Str(kv.Key)] = kv.Value;
            return outOpt;
        }

        // PS 的属性访问自带大小写宽容，JSON 字典默认敏感 ⇒ 吃"settings 的块"与"壳手构的 gate"都先过 CI 视图。
        static Dictionary<string, object> CiMap(object v)
        {
            Dictionary<string, object> d = Gd.Ci(v);
            if (d == null) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return d;
        }

        // PS `[int]$x` 的同形：$null→0、布尔→1/0、空串→0、可解析的串与数走 Convert.ToInt32
        // (**银行家舍入**——[int]2.5 两侧都是 2，别按 C# 直觉改成四舍五入)；其余抛(真身没挡，话术见文件头)。
        static int PsInt(object v)
        {
            if (v == null) return 0;
            if (v is bool) return (bool)v ? 1 : 0;
            if (v is string)
            {
                string s = ((string)v).Trim();
                if (s.Length == 0) return 0;
                double d;
                if (double.TryParse(s, NumberStyles.Any, Inv, out d)) return Convert.ToInt32(d, Inv);
                throw new FormatException("Cannot convert value \"" + s + "\" to type \"System.Int32\".");
            }
            return Convert.ToInt32(v, Inv);
        }

        // PS `[bool]$x` 的同形：$null/空串→false、"true"/"false" 不分大小写、数字按非零、其余串抛。
        // (与 Round.FlagEnabled 不是同一把尺：那枚算的是真身里 `-ne $false` 的"没填算开着"，本枚算的是强转。)
        static bool PsBool(object v)
        {
            if (v == null) return false;
            if (v is bool) return (bool)v;
            if (v is string)
            {
                string s = ((string)v).Trim();
                if (s.Length == 0) return false;
                if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                double d;
                if (double.TryParse(s, NumberStyles.Any, Inv, out d)) return d != 0.0;
                throw new FormatException("Cannot convert value \"" + s + "\" to type \"System.Boolean\".");
            }
            if (v is IDictionary<string, object>)
                throw new FormatException("Cannot convert a JSON object to type \"System.Boolean\".");
            return Convert.ToDouble(v, Inv) != 0.0;
        }

        // Split-Path -Leaf 的同形：先裁尾分隔符再取最后一段。空串那一档真身专门挡过
        // (Split-Path -Leaf '' 当场抛"无法将路径为空"——"没日志文件"这件事不该炸掉整枚 ctx 装配)，这里同样不抛。
        // 根路径('C:\'、'\')两形与本件的分岔读数见文件头那第三条(实测，不推断)。
        static string LeafOf(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            string t = p.TrimEnd('\\', '/');
            if (t.Length == 0) return "";
            int i = t.LastIndexOfAny(new char[] { '\\', '/' });
            return i < 0 ? t : t.Substring(i + 1);
        }
    }
}
