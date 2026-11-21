// GdCore.Paths.cs - K1a: pure-rule path joining, replicating PowerShell 7 Join-Path (FileSystem provider).
//
// HARD CONSTRAINTS (see docs\gd-core-contract.md, all still binding):
//   * C#5 language ceiling - the 5.1 host compiles this through CodeDom (no interpolation, no ?., no nameof,
//     no expression-bodied members). Keep it plain.
//   * Pure BCL only - no System.Web.Extensions, no NuGet. Both hosts must compile the identical source.
//   * Pure ASCII in this file - the 5.1 host reads a BOM-less .ps1/.cs as GBK and mangles CJK.
//   * No exceptions for business outcomes - "nothing to join" is a value, not a fault. The shell decides
//     whether a result is acceptable; a thrown exception here would surface as an English MethodInvocationException.
//
// MEASURED SEMANTICS (2026-09-28, 44-case matrix, both engines; the rule below reproduces pwsh7 cell for cell):
//   child starting with a separator loses exactly ONE leading separator ('\\b' -> '\b', '\\\b' -> '\\b',
//   '/' -> ''), then a single backslash is inserted only when the base does not already end with a separator,
//   and finally every '/' in the joined string becomes '\'. Base interior/trailing and child trailing/interior
//   separator runs are left exactly as typed ("C:\\a" + "b" -> "C:\\a\b", "C:\\a\\" + "b" -> "C:\\a\\b").
//   Rooted-looking children are NOT treated as absolute ("C:\a" + "D:\b" -> "C:\a\D:\b"), and '..'/'.' are
//   never collapsed ("x" + "..\y" -> "x\..\y").
//
// KNOWN ENGINE DIVERGENCE (pre-existing in the PS original, not introduced here): with 2+ leading separators
// in a child, 5.1 concatenates without trimming ("C:\a" + "\\b" -> "C:\a\\b") while 7 collapses to "C:\a\b";
// 5.1 additionally throws on a "\\?\" device-namespace base. The kernel deliberately carries 7's answer,
// because the production entry points are pinned to pwsh7 (install-task.ps1 refuses to run without it) and
// both spellings resolve to the same file. The golden gate pins this exception list verbatim.

using System;

namespace GdCore
{
    /// <summary>Path rules that carry no I/O: existence checks stay in the shell on purpose.</summary>
    public static class Paths
    {
        private static bool IsSeparator(char c)
        {
            return c == '\\' || c == '/';
        }

        /// <summary>One Join-Path step, pwsh7 semantics. Neither argument may be null; empty child returns base.</summary>
        public static string JoinTwo(string basePath, string child)
        {
            if (child == null || child.Length == 0) { return basePath; }
            string trimmed = child;
            if (IsSeparator(child[0])) { trimmed = child.Substring(1); }
            // The bridge separator appears only when neither side already carries one: base must not end with a
            // separator, and the (possibly trimmed once) child must not start with one. That is what makes
            // "C:\a" + "\\b" come out as "C:\a\b" while "C:\a" + "\\\b" keeps exactly one extra.
            bool baseHasSeparator = basePath.Length > 0 && IsSeparator(basePath[basePath.Length - 1]);
            bool childHasSeparator = trimmed.Length > 0 && IsSeparator(trimmed[0]);
            string bridge = (baseHasSeparator || childHasSeparator) ? string.Empty : "\\";
            string joined = basePath + bridge + trimmed;
            return joined.Replace('/', '\\');
        }

        /// <summary>
        /// Join-GdPath body: fold JoinTwo over the parts, skipping null/empty ones (whitespace counts as content).
        /// An empty base short-circuits to empty string - that is the "not filled in yet" signal the health
        /// panel renders as "missing", so it must survive to the caller as '' and never as a fault.
        /// </summary>
        public static string Join(string basePath, string[] parts)
        {
            if (string.IsNullOrEmpty(basePath)) { return string.Empty; }
            string current = basePath;
            if (parts == null) { return current; }
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part)) { continue; }
                current = JoinTwo(current, part);
            }
            return current;
        }
    }
}
