// GdCore.Logs.cs - K2: pure-rule residue of the log / version / history / run-lock group.
//
// HARD CONSTRAINTS (unchanged, see docs\gd-core-contract.md sections 5 and 9):
//   * C#5 language ceiling (the 5.1 host compiles this through CodeDom).
//   * Pure BCL, pure ASCII source, no exceptions for business outcomes.
//
// WHAT IS DELIBERATELY NOT HERE (measured 2026-09-29, K2 boundary matrix; contract section 9):
//   * Write-GdLog's line formatting - both engines produce byte-identical strings, and the disk side is
//     Add-Content -Encoding utf8, whose BOM behaviour is engine-specific (5.1 writes EF BB BF, 7 does not).
//     Moving either half into the kernel would change bytes already on disk for no gain => kernelized: no.
//   * The clock. Get-Date stays in the shell, and it is called TWICE by the original (short stamp for the
//     console, full stamp for the file). Those two calls can straddle a second boundary; that quirk is part
//     of the contract, so the kernel takes two strings and never a single instant.
//   * Numeric type coercion of [math]::Round($anything). A string parameter is a semantic change (7 turns ' '
//     into 0 while 5.1 throws), so the shell still rounds and the kernel only renders the already-rounded value.
//
// MEASURED CELL THAT NEEDED A RULING: "$([math]::Round(-0.5))" is "-0" on 7 and "0" on 5.1 - the same double,
// different ToString. That is a runtime difference, so a per-host source kernel cannot absorb it by accident;
// it has to decide. DurationText below pins the answer to "0", matching the rows already sitting in history.csv.

using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GdCore
{
    /// <summary>VERSION-file rules: trim and shape-check, nothing else (the read stays in the shell).</summary>
    public static class Versions
    {
        // The shell original uses -match, i.e. .NET regex, where \d is Unicode-aware: the matrix showed
        // full-width digits and Arabic-Indic digits PASS as version numbers on both engines. Replicating
        // that with char.IsDigit, or with '0'..'9' range checks, would silently reject them - so the kernel
        // uses the same Regex class the shell does. '$' before a trailing newline behaves the same way too.
        private static readonly Regex Shape = new Regex("^\\d+\\.\\d+\\.\\d+$");
        private const string Fallback = "0.0.0";

        /// <summary>Get-GdVersion's validation step: trim, then keep the shape or fall back to 0.0.0. Never throws.</summary>
        public static string Normalize(string raw)
        {
            string trimmed = raw == null ? string.Empty : raw.Trim();
            return Shape.IsMatch(trimmed) ? trimmed : Fallback;
        }
    }

    /// <summary>CSV rules for history.csv. The file's own encoding and newline stay with Add-Content.</summary>
    public static class Csv
    {
        private static readonly Regex Newline = new Regex("\\r?\\n");

        /// <summary>The shell's CR/LF flatten rule (pattern \r?&#124;n replaced by " | "): CRLF and LF flatten, a lone CR does NOT (measured, both engines).</summary>
        public static string Flatten(string text)
        {
            if (text == null) { return string.Empty; }
            return Newline.Replace(text, " | ");
        }

        /// <summary>One field: wrap in quotes, double any quote inside. null becomes the empty quoted field.</summary>
        public static string QuoteField(string field)
        {
            string value = field == null ? string.Empty : field;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>BuildRow = quote every field, join by comma. An empty array gives "" like the shell's -join does.</summary>
        public static string BuildRow(string[] fields)
        {
            if (fields == null || fields.Length == 0) { return string.Empty; }
            StringBuilder row = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) { row.Append(','); }
                row.Append(QuoteField(fields[i]));
            }
            return row.ToString();
        }

        /// <summary>
        /// Render an already-rounded duration. Integral values up to 2^53 print as integers on both engines
        /// (123456790, 2147483648 - measured); zero always prints "0" including negative zero (the engine
        /// split); anything past the exact-integer range keeps the runtime's own general format, which is
        /// what "$([math]::Round(1e20))" produced on both engines.
        /// </summary>
        public static string DurationText(double rounded)
        {
            if (rounded == 0.0) { return "0"; }
            if (rounded >= -9007199254740992.0 && rounded <= 9007199254740992.0)
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }
            return rounded.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Ownership rule for running.json - "is this marker mine?" only; deleting is the shell's job.</summary>
    public static class RunLock
    {
        // The shell used -match, which is case-insensitive: the matrix showed {"PID":123} counting as ours.
        // The trailing [\s,}] is what stops pid 123 from claiming {"pid":1234} or {"pid":12}.
        private static readonly RegexOptions Flags = RegexOptions.IgnoreCase;

        /// <summary>True when the text carries "pid":pid followed by a delimiter. Null/empty text is never ours.</summary>
        public static bool OwnsText(string jsonText, int pid)
        {
            if (string.IsNullOrEmpty(jsonText)) { return false; }
            string pattern = "\"pid\"\\s*:\\s*" + pid.ToString(CultureInfo.InvariantCulture) + "[\\s,}]";
            return Regex.IsMatch(jsonText, pattern, Flags);
        }
    }
}
