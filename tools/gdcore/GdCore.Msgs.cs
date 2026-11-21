// GdCore.Msgs.cs - generated from lib\common.ps1 verbatim literals (K1b).
// DO NOT hand-edit the text: re-run tools\gen-gdcore-msgs.ps1 instead.
// ASCII-only by design (docs\gd-core-contract.md section 5: no non-ASCII in kernel source).
namespace GdCore
{
    public static class Msgs
    {
        public const string ReadMissingFile = "\u627E\u4E0D\u5230\u6587\u4EF6\uFF1A";
        public const string NoSettingsFile = "\u8FD8\u6CA1\u6709 config\u005Csettings.json\u3002\u5148\u8DD1 setup.ps1(\u81EA\u52A8\u63A2\u6D4B\u52A9\u624B\u5B89\u88C5\u4F4D\u7F6E\u5E76\u751F\u6210)\uFF0C\u6216\u628A config\u005Csettings.example.json \u590D\u5236\u6210 settings.json \u540E\u624B\u586B paths\u3002";
        public const string PathsEmptyPrefix = "settings.json \u7684 paths.";
        public const string PathsEmptySuffix = " \u662F\u7A7A\u7684 \u2192 \u8DD1 setup.ps1 \u81EA\u52A8\u63A2\u6D4B\u52A9\u624B\u5B89\u88C5\u4F4D\u7F6E\uFF0C\u6216\u628A config\u005Csettings.example.json \u91CC\u90A3\u4E00\u9879\u624B\u586B\u4E0A";
        public const string NotExistPrefix = "settings.json \u7684 paths.";
        public const string NotExistMiddle = " \u4E0D\u5B58\u5728\uFF1A";
        public const string StopPrefix = "paths \u91CC\u8FD9 ";
        public const string StopMiddle = " \u9879\u8FD8\u7A7A\u7740\uFF1A";
        public const string StopSuffix = " \u2192 \u5148\u8DD1 setup.ps1 \u81EA\u52A8\u63A2\u6D4B\u52A9\u624B\u5B89\u88C5\u4F4D\u7F6E(\u6216\u624B\u586B config\u005Csettings.json)";
    }
}
