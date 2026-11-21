// tools/panel-cs/Core.Json.cs —— 批次 0 的 Core 层之二：JSON 读写 + 信封(契约 §2.2)。
//
// 为什么这里破例上 JavaScriptSerializer：Panel.Settings.cs 里那句"为一处自检给编译链加引用不值当"
// 判断不变 —— 那次是一处；这次是整个迁移地基(判定层的 data 里嵌套字典/数组/布尔混排)，
// 手写解析错的每一个角都会变成批次 1 的假绿。System.Web.Extensions 是 .NET Framework 自带装配(GAC)，
// 玩家机器零新增依赖。两条已知怪癖由 --coretest 钉死而不是祈祷：
//   ① 数字回读 Int32/Decimal 混发 —— 实测 1.2 回来是 Decimal 而不是 Double! 批次 1 起 Core 内
//     取数一律 Convert.ToInt32/ToDouble，裸强转 (double)x 会 InvalidCastException 当场炸；数组实测为 object[]；
//   ② 反序列化产物是 Dictionary<string,object> / object[] 树(不是 POCO)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace GdPanel
{
    static class GdJson
    {
        // 信封协议版本(照 DumpSummary 的 PROTO 先例)：两侧对不上必须明说，不许静默空格子。
        public const int Proto = 1;

        static JavaScriptSerializer Made()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 64;
            return s;
        }

        // 对齐 Read-GdJson：缺文件抛"找不到文件：<路径>"(PS 原话逐字，转发壳转达时要一模一样)；
        // BOM 自动识别、无 BOM 按 UTF-8 —— PS 那边 5.1 的 GBK 坑在这条边界上不存在。
        public static object Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("找不到文件：" + path);
            return Made().DeserializeObject(File.ReadAllText(path));
        }

        public static object Deserialize(string text)
        {
            return Made().DeserializeObject(text);
        }

        public static string Serialize(object obj)
        {
            return Made().Serialize(obj);
        }

        // 对齐 Write-GdJson：无 BOM 的 UTF-8。目录不存在顺手补(结果文件常落在临时树上)。
        public static void Write(string path, object obj)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, Made().Serialize(obj), new UTF8Encoding(false));
        }

        // ---------- 信封：verb 的机读结果长这样，永远写出、连内部错误也写 ----------
        // data 与 PS 原函数返回值一一对应(键名逐字)；exit 与进程退出码必须一致(--echotest 钉这条)。
        public static Dictionary<string, object> Envelope(string verb, bool ok, int exit, object data, string error)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["proto"] = Proto;
            d["verb"] = verb;
            d["ok"] = ok;
            d["exit"] = exit;
            d["data"] = data ?? new Dictionary<string, object>();
            d["error"] = error;
            return d;
        }

        public static Dictionary<string, object> ReadEnvelope(string path)
        {
            Dictionary<string, object> d = Read(path) as Dictionary<string, object>;
            if (d == null) throw new InvalidDataException("结果文件不是 JSON 对象(信封形状不认识)：" + path);
            return d;
        }
    }
}
