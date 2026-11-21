// GdCore kernel source anchor (batch K0, route "C": each host compiles this file in-process via Add-Type -Path;
// no prebuilt dll, no shipping artifact). Contract docs: docs\gd-core-contract.md (Chinese).
// HARD CONSTRAINTS (pinned by K0 probes, do not break):
//   * C# 5 syntax ceiling -- Windows PowerShell 5.1 CodeDom must compile this file.
//     (5.1 support retired 2026-10-06 batch C; the ceiling itself is retained as-is by decision.)
//   * Pure BCL references only -- NO System.Web.Extensions (pwsh7: JSS.DeserializeObject explodes at first use).
//   * Pure ASCII source (5.1 reads BOM-less UTF-8 as GBK; comments here stay English on purpose).
//     (5.1 motivation retired with 5.1 on 2026-10-06; the ASCII rule itself is kept.)
//   * Case-insensitive dictionaries only for PS-facing returns (Hashtable index is case-sensitive,
//     PSCustomObject property access is not -- contract section 2.3).
//   * No business failures via exceptions (PS wraps them into MethodInvocationException with English shell;
//     business failure travels by return value, only original-PS "throw" semantics are re-thrown in Chinese by the shell).
using System;

namespace GdCore
{
    public static class Meta
    {
        // Contract number: shell (lib\common.ps1 Get-GdCoreContract) compares this on every load;
        // mismatch => silent fallback to the frozen PS originals. Bump once per kernelized function from K1 on.
        //   GDCore-K1.1 = Paths (Join-GdPath) kernelized -- see GdCore.Paths.cs and tools\probe-kernel-paths-golden.ps1
        //   GDCore-K1.2 = Json/Backup/Settings rule layer (Read-GdJson, Backup-GdFile, Get-GdSettings) --
        //                 see GdCore.Json.cs + GdCore.Msgs.cs and tools\probe-kernel-json-golden.ps1.
        //                 Write-GdJson deliberately stays in PowerShell (measured engine-divergence surface,
        //                 docs\gd-core-contract.md section 8); the whitelist entry list is the contract's own scope list.
        //   GDCore-K2.1 = Versions/Csv/RunLock rule layer (Get-GdVersion, Stop-GdRunLock; Add-GdHistory used to be listed here -- its PowerShell body was retired 11-07 cut17(c)-1, the Csv rule layer itself stays in the kernel and panel-cs is its only consumer now) --
        //                 see GdCore.Logs.cs and tools\probe-kernel-log-golden.ps1. Write-GdLog and Show-GdVersionFile
        //                 are retired the same way; K2 matrix readings are in docs\gd-core-contract.md section 9.
        public static string Contract()
        {
            return "GDCore-K2.1";
        }
    }
}
