// GdCore.Json.cs - K1b: the pure-rule residue of the serialization trio, NOT the JSON engine itself.
//
// WHY THE ENGINE STAYS IN POWERSHELL (measured 2026-09-28, both engines, before any C# was written;
// full readings in docs\gd-core-contract.md section 8):
//   * ConvertFrom-Json / ConvertTo-Json are already in-process C# inside the host. Re-implementing them in the
//     kernel would replace Microsoft's maintained parser with ours while the kernel's whole purpose is to move
//     PS-interpreted rules into C#. Zero net gain, new risk surface.
//   * The two engines do not agree on the SHAPE of that output, and the gap is wide: 5.1 numbers are
//     Int32/Decimal where 7 gives Int64/Double; 5.1 indents 4 spaces with two spaces after the colon, 7 indents
//     2 with one; 5.1 renders empty containers across several lines, 7 inlines them; 5.1 escapes < > & ' as
//     <uhh>, 7 leaves them literal; a top-level $null serializes to "null" on 7 and to '' on 5.1.
//   * Files written by Write-GdJson are consumed by programs outside this repo (MAA configuration.json,
//     M9A settings.json). A imperfect re-implementation would not fail a test, it would break a helper app.
//   * A malformed JSON document fails with the host's LOCALIZED message, different per engine and per culture,
//     so contract section 2.8 ("the shell re-throws the original Chinese wording") has no original wording for
//     the kernel to reproduce byte for byte.
// What is left is the rule layer around those calls: BOM handling, the missing-file message, the backup naming
// and retention plan, and the settings path check. All of it is PS-interpreted today, so all of it moves here.
//
// HARD CONSTRAINTS (unchanged from K0/K1a): C#5 ceiling, pure BCL, pure ASCII, no exceptions for business
// outcomes, case-insensitive dictionaries for anything the shell reads back by dot access.
// Message text is NOT typed in this file: GdCore.Msgs.cs is generated from the verbatim literals in
// lib\common.ps1 (tools\gen-gdcore-msgs.ps1), so a wording change in the shell and a rewording here cannot
// drift apart silently - the golden gate compares the assembled string against the original.

using System;
using System.Collections;

namespace GdCore
{
    /// <summary>Read-GdJson rules that carry no parser: byte-order mark and the missing-file wording.</summary>
    public static class Json
    {
        /// <summary>
        /// Strip one leading U+FEFF (the original tests text[0] and takes Substring(1)). A null in is an empty
        /// out: the shell only ever hands over File.ReadAllText output, which is never null.
        /// </summary>
        public static string StripBom(string text)
        {
            if (text == null) { return string.Empty; }
            if (text.Length > 0 && text[0] == '\uFEFF') { return text.Substring(1); }
            return text;
        }

        /// <summary>Read-GdJson's "file not there" wording - the original throws this string, the shell re-throws it.</summary>
        public static string MissingFileMessage(string path)
        {
            return Msgs.ReadMissingFile + (path == null ? string.Empty : path);
        }
    }

    /// <summary>Backup-GdFile rules: how a backup is named and which old ones are past the keep-count.</summary>
    public static class Backup
    {
        /// <summary>
        /// The Join-Path step of the original: Backup-dir + "name.stamp.bak", pwsh7 join semantics via Paths.
        /// Neither argument may be null (Get-FileName output and the date format string always are strings).
        /// </summary>
        public static string BuildPath(string backupDir, string fileName, string stamp)
        {
            string name = fileName == null ? string.Empty : fileName;
            string ts = stamp == null ? string.Empty : stamp;
            return Paths.JoinTwo(backupDir, name + "." + ts + ".bak");
        }

        /// <summary>
        /// Select-Object -Skip keep, on a list the shell already sorted by LastWriteTime descending.
        /// The kernel deliberately does not sort: keeping the sort in the shell preserves the original's
        /// tie-breaking order (whatever order Get-ChildItem yielded), which a re-sort here could change.
        /// keep is passed as the original literal 20 by the shell.
        /// </summary>
        public static string[] PastKeep(string[] namesDescending, int keep)
        {
            if (namesDescending == null || keep < 0) { return new string[0]; }
            if (namesDescending.Length <= keep) { return new string[0]; }
            string[] tail = new string[namesDescending.Length - keep];
            Array.Copy(namesDescending, keep, tail, 0, tail.Length);
            return tail;
        }
    }

    /// <summary>
    /// Get-GdSettings check stage. It receives flat arrays instead of the parsed settings object on purpose:
    /// dot access into a PSCustomObject is PowerShell's dynamic member lookup, and reaching for it from C#
    /// would either need System.Management.Automation (violating pure BCL) or a parser re-implementation.
    /// The shell keeps that lookup and the truthiness test, so what arrives here is already decided data:
    /// an empty value means "the original's -not $p branch", and exists[] is only consulted for filled values.
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// Returns a case-insensitive Hashtable (contract section 2.3) with:
        ///   WarnLines   string[] - the WARN lines in key order, for the shell to pass to Write-GdLog
        ///   EmptyKeys   string[] - the keys that count as not filled in
        ///   StopMessage string   - '' when the run may continue, else the throw wording
        /// enforceStop is what the shell computes from its -Quiet switch (original: "-not $Quiet -and $missing.Count"):
        /// true means "this caller intends to work, so an empty path must stop the run". Naming it after the caller's
        /// decision instead of after the switch keeps the polarity straight - the first cut of this signature read
        /// "quiet" and inverted the whole stop branch, and the golden gate caught it on the first run.
        /// Nothing here throws and nothing here logs: logging and the throw belong to the shell.
        /// </summary>
        public static Hashtable Evaluate(string[] keys, string[] values, bool[] exists, bool enforceStop)
        {
            Hashtable result = new Hashtable(StringComparer.OrdinalIgnoreCase);
            ArrayList warn = new ArrayList();
            ArrayList empty = new ArrayList();
            int count = keys == null ? 0 : keys.Length;
            for (int i = 0; i < count; i++)
            {
                string key = keys[i] == null ? string.Empty : keys[i];
                string value = (values != null && i < values.Length && values[i] != null) ? values[i] : string.Empty;
                if (value.Length == 0)
                {
                    empty.Add(key);
                    warn.Add(Msgs.PathsEmptyPrefix + key + Msgs.PathsEmptySuffix);
                }
                else
                {
                    bool ready = exists != null && i < exists.Length && exists[i];
                    if (!ready) { warn.Add(Msgs.NotExistPrefix + key + Msgs.NotExistMiddle + value); }
                }
            }
            string[] emptyKeys = (string[])empty.ToArray(typeof(string));
            result["WarnLines"] = (string[])warn.ToArray(typeof(string));
            result["EmptyKeys"] = emptyKeys;
            string stop = string.Empty;
            if (enforceStop && emptyKeys.Length > 0)
            {
                stop = Msgs.StopPrefix + emptyKeys.Length + Msgs.StopMiddle
                     + string.Join(" , ", emptyKeys) + Msgs.StopSuffix;
            }
            result["StopMessage"] = stop;
            return result;
        }

        /// <summary>Get-GdSettings wording for "config\settings.json does not exist yet" (the original throws it).</summary>
        public static string MissingConfigMessage()
        {
            return Msgs.NoSettingsFile;
        }
    }
}
