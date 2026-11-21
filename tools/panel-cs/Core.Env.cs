// tools/panel-cs/Core.Env.cs —— 批次 0 的 Core 层之一：仓库根定位、目录结构、VERSION 读数、路径语义、备份。
//
// 语义逐条对着 lib\common.ps1 抄(见 docs\ps-migration-batch0.md §3)：迁移期间 PS 转发壳和 C# 共用
// 同一套"空 base 返空串不抛 / 未填写要能显示成一行字而不是内部报错"的口径，两侧读数才不会出现一个格子各说各话。
// 编译注意：系统 csc 是 C# 5 编译器(.NET Framework 4)，2012 年之后的语法一概不能用。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class GdEnv
    {
        public static string Root;
        public static string Logs;
        public static string State;
        public static string Backup;
        static string versionCache;

        // 根定位和 PanelForm 构造里那三级猜测一致：exe 在 <root>\tools\panel-cs\ 下退两级；
        // 被复制走(和仓库摆一起的快捷方式)看当前目录；最后兜底工作目录。认的锚是仓库根那个 VERSION
        // (刀② 2026-10-23 与 Panel.cs:164 那段同批改口，两处必须同一把尺 —— 原来认的是 tools\panel.ps1，
        //  而它是乙档刀③ 要退役的件；VERSION 是全工程唯一版本来源，出货 zip 也带着它)。
        // overrideRoot 给自检喂临时树用(只读自检不许要求机器现场状态的老规矩)。幂等：已定位过就不再找。
        public static void Init(string overrideRoot)
        {
            if (Root != null && overrideRoot == null) return;
            string root = overrideRoot;
            if (string.IsNullOrEmpty(root))
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string guess = Path.GetFullPath(Path.Combine(exeDir, @"..\.."));
                if (File.Exists(Path.Combine(guess, "VERSION"))) root = guess;
                else if (File.Exists(Path.Combine(exeDir, "VERSION"))) root = exeDir;
                else root = Directory.GetCurrentDirectory();
            }
            Root = root;
            Logs = Path.Combine(Root, "logs");
            State = Path.Combine(Root, "state");
            Backup = Path.Combine(Root, "backups");
            foreach (string d in new string[] { Logs, State, Backup })
            {
                try { if (!Directory.Exists(d)) Directory.CreateDirectory(d); } catch { }
            }
        }

        // 自检换根用：连缓存一起清，保证读数是那棵临时树的。
        public static void InitForTest(string tempRoot)
        {
            Root = null;
            versionCache = null;
            Init(tempRoot);
        }

        // ---------- VERSION：全工程唯一来源是仓库根那个文件，这里只是 C# 侧的读数(和 Get-GdVersion 同规矩)：
        // 读不到/形状不对都不抛，回 0.0.0 —— 体检和面板宁可说"版本号读不出来"也别把整段带走。
        public static string Version
        {
            get
            {
                if (versionCache != null) return versionCache;
                string v = "0.0.0";
                try
                {
                    string path = Path.Combine(Root ?? "", "VERSION");
                    if (File.Exists(path))
                    {
                        string[] lines = File.ReadAllLines(path);
                        string first = lines.Length > 0 ? lines[0].Trim() : "";
                        if (Regex.IsMatch(first, @"^\d+\.\d+\.\d+$")) v = first;
                    }
                }
                catch { }
                versionCache = v;
                return v;
            }
        }

        // 照抄 Join-GdPath：base 没填返空串，不抛。全工程的"未填写"显示都指望这一条，
        // 真拿去干活的入口由配置校验先硬停，所以放宽这里不会把错误咽掉。
        public static string JoinPath(string baseDir, params string[] parts)
        {
            if (string.IsNullOrEmpty(baseDir)) return "";
            string p = baseDir;
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                p = Path.Combine(p, part);
            }
            return p;
        }

        // 照抄 Test-GdPathReady：填了且真的存在(文件或目录)才算就绪；空串自己挡掉。
        public static bool PathReady(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try { return File.Exists(path) || Directory.Exists(path); } catch { return false; }
        }

        // 照抄 Backup-GdFile：备份到 backups\<名>.<stamp>.bak，每个文件只留最近 20 份。
        // (面板那份 BackupSettings 是它的 settings.json 专用版，本函数是泛化版，两者规矩一致。)
        public static string BackupFile(string path)
        {
            if (!File.Exists(path)) return null;
            Init(null);
            string name = Path.GetFileName(path);
            try
            {
                string dest = Path.Combine(Backup, name + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");
                File.Copy(path, dest, true);
                List<FileInfo> olds = new List<FileInfo>();
                foreach (string f in Directory.GetFiles(Backup, name + ".*.bak"))
                {
                    if (Path.GetFileName(f).Equals(Path.GetFileName(dest), StringComparison.OrdinalIgnoreCase)) continue;
                    try { olds.Add(new FileInfo(f)); } catch { }
                }
                olds.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                for (int i = 20; i < olds.Count; i++) { try { olds[i].Delete(); } catch { } }
                return dest;
            }
            catch { return null; }
        }
    }
}
