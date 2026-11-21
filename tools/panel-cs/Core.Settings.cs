// tools/panel-cs/Core.Settings.cs —— 批次 0 的 Core 层之四：settings.json 的读取与 paths 校验。
// 对齐 Get-GdSettings / Get-GdRequiredPathKeys：
//   * "该催哪几个助手目录 = 这棵树上真的装了哪几路的库" —— 单游戏仓不为玩家不会装的助手报警；
//   * Quiet 双人格原样保留：干活的入口要"一次点齐所有空项然后硬停"，只读诊断要"把没填的继续列出来"，
//     硬不停由调用方(verb 运行器)决定 —— 前置不满足 = 退出码 2，见契约 §2.3。
// 文案逐字抄 PS：转发壳过渡期两侧会往同一个日志里写，措辞对不上会被当成两个不同的问题。

using System;
using System.Collections.Generic;
using System.IO;

namespace GdPanel
{
    static class GdSettings
    {
        static readonly string[,] Pairs = new string[,] {
            { "maaDir", "maa.ps1" }, { "m9aDir", "m9a.ps1" }, { "mumuDir", "mumu.ps1" }
        };

        public static List<string> RequiredPathKeys()
        {
            GdEnv.Init(null);
            List<string> keys = new List<string>();
            for (int i = 0; i < Pairs.GetLength(0); i++)
                if (File.Exists(Path.Combine(GdEnv.Root, "lib", Pairs[i, 1]))) keys.Add(Pairs[i, 0]);
            return keys;
        }

        // missing = 空路径的键名列表；不存在的路径只 WARN 不进 missing(和 PS 一致：那是"填了但指错"，另一类问题)。
        // 缺文件/坏格式直接抛 —— 由 verb 运行器接住换算成退出码(配置没生成 = 2，读坏了 = 1)。
        public static Dictionary<string, object> Load(out List<string> missing, bool quiet, List<string> requirePaths)
        {
            GdEnv.Init(null);
            missing = new List<string>();
            string path = Path.Combine(GdEnv.Root, @"config\settings.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("还没有 config\\settings.json。先跑 setup.ps1(自动探测助手安装位置并生成)，或把 config\\settings.example.json 复制成 settings.json 后手填 paths。");
            Dictionary<string, object> s = GdJson.Read(path) as Dictionary<string, object>;
            if (s == null) throw new InvalidDataException("settings.json 顶层不是 JSON 对象：" + path);
            if (requirePaths == null) requirePaths = RequiredPathKeys();
            object pathsObj;
            Dictionary<string, object> paths = s.TryGetValue("paths", out pathsObj) ? pathsObj as Dictionary<string, object> : null;
            foreach (string k in requirePaths)
            {
                object v;
                string p = (paths != null && paths.TryGetValue(k, out v)) ? v as string : null;
                if (string.IsNullOrEmpty(p))
                {
                    missing.Add(k);
                    if (!quiet) GdLog.Line("settings.json 的 paths." + k + " 是空的 → 跑 setup.ps1 自动探测助手安装位置，或把 config\\settings.example.json 里那一项手填上", "WARN");
                }
                else if (!GdEnv.PathReady(p))
                {
                    GdLog.Line("settings.json 的 paths." + k + " 不存在：" + p, "WARN");
                }
            }
            return s;
        }

        // 非 quiet 且有空项时 PS 入口的硬停文案(Stop-GdBeforeRun 同型)：verb 运行器拿它建退出码 2 的信封。
        public static string MissingMessage(List<string> missing)
        {
            return "paths 里这 " + missing.Count + " 项还空着：" + string.Join(" , ", missing.ToArray()) +
                   " → 先跑 setup.ps1 自动探测助手安装位置(或手填 config\\settings.json)";
        }
    }
}
