# gd-ops — 每日清体力内核（面板 + 核 + 框架）

每天 08:00 / 20:00 把游戏日常跑完的自动化底座。**本仓只带内核，不带任何一款游戏的业务知识**：
面板、Rust 核、模块框架、公共库、安装器；具体某款游戏怎么跑，由独立的**插件仓**提供。

```
gd-ops（本仓）                     game-daily-ark（插件）        game-daily-r1999（插件）
  ├─ 入口 / 体检 / 定时            ├─ lib\maa.ps1               ├─ lib\m9a.ps1
  ├─ C# 面板 + Rust 核             ├─ lib\ark-module.ps1         ├─ lib\r1999-module.ps1
  ├─ 模块框架 + 公共库             ├─ lib\ark-decisions.ps1      ├─ lib\r1999-decisions.ps1
  └─ tools\install-plugin.ps1  ←──  └─ tools\test-ark.ps1        └─ tools\test-r1999.ps1
```

**没装插件的这棵树是"空内核"**：入口会明确拒跑（退出码 2）并告诉你去装一个插件，不会带着空名册假装跑一轮。

---

## 1. 快速开始

需要 **PowerShell 7**（`pwsh`；只支持 7，不兼容系统自带的 5.1）。

```powershell
# 1) 拿内核
git clone https://github.com/Lu-yanmo/gd-ops.git

# 2) 装要玩的插件（装哪个装哪个，两个都装也行）
git clone https://github.com/Lu-yanmo/game-daily-ark.git
git clone https://github.com/Lu-yanmo/game-daily-r1999.git
pwsh -File tools\install-plugin.ps1 -From ..\game-daily-ark
pwsh -File tools\install-plugin.ps1 -From ..\game-daily-r1999

# 3) 探测助手装在哪、生成 config\settings.json
pwsh -File setup.ps1

# 4) 看一眼今天会怎么跑（不启动任何程序）
pwsh -File daily-clear.ps1 -DryRun

# 5) 之后双击 panel.cmd 在主控台里点；先注册计划任务见各插件 README 第 5 节
pwsh -File install-task.ps1
```

**内置件（`gdcore.exe`）不随仓出货**（源码进仓、产物不入仓）：从 git 克隆的树要先编一次
`pwsh -File tools\rustcore\build.ps1`（需要 `cargo`）。拿不到内置件时整轮拒跑（退出码 2），不会换人跑。

## 2. 插件怎么装

插件仓的仓根有一份 `plugin.json`，四样东西：

| 键 | 含义 |
| --- | --- |
| `id` | 插件短名（`ark` / `r1999`），装/卸用它 |
| `files` | 要搬进本树的文件（只许 `lib\` 与 `tools\` 下的相对路径） |
| `settings` | 配置片段路径（仓内相对路径），装的时候并进本机 `config\settings.json` |
| `name` / `version` | 显示用；`version` 跟着插件仓发布走 |

安装器的规矩：

- **只补不覆盖**：配置片段里的键，`settings.json` 里已有就保留本机的值（数组与标量整枚让位），缺的才补；
  改动前把原文件备份到 `backups\plugin-<id>-<时间戳>.settings.json`。
- **冲突整批拒装**：目标文件已存在、又不是这个插件装的、内容还不同 → 逐枚点名后退出（`-Force` 才覆盖）。
- **参考副本**：片段永远留在 `config\plugins\<id>.example.json`，想恢复默认值看它。
- **卸载**：`pwsh -File tools\install-plugin.ps1 -Uninstall ark` 只删它装的件；`settings.json` 里的键保留
  （可能有你自己改过的值，交给你删）。`-List` 看装了哪些。

安装记录写在 `config\plugins.json`（不入仓），所以"装了哪些插件"是每台机器各自的事实。

## 3. 入口与开关

`daily-clear.ps1` 是**合体入口**：装了哪些插件，这一轮就跑哪些；参数里与游戏有关的那几个：

| 开关 | 归属 | 含义 |
| --- | --- | --- |
| `-Games ark` / `-Games r1999` / `-Games all` | 全部 | 这一轮只跑哪几款（默认 `all`） |
| `-Ack1999` | 1999 | 新主线/活动我已手动通一遍（登记后才会自动刷） |
| `-ArkShopDone` / `-RShopDone` | 方舟 / 1999 | 活动商店已领空 |
| `-ArkShopReset` / `-RShopReset` | 方舟 / 1999 | 撤销上面的标记 |
| `-FarmArk` / `-ArkStage` / `-RPlan` | 方舟 / 1999 | 本轮专用：补材料 / 指定关卡 / 指定方案 |
| `-DryRun` / `-Force` / `-NoElevate` / `-SkipCleanup` | 内核 | 预览 / 压过机器判定 / 禁止提权 / 跑完不关游戏 |

退出码：`0` 全部完成 / `1` 部分完成 / `2` 这一轮没跑成（含"没装插件""拿不到内置件"）。

## 4. 目录结构

```
  daily-clear.ps1      入口：选模块 → 整轮交给内置件 gdcore.exe（--daily-round）
  setup.ps1            探测助手路径 → 生成 config\settings.json（只写这一份配置）
  status.ps1           只读体检（没装插件时给出安装提示后退出）
  install-task.ps1     计划任务注册/卸载
  run-via-task.ps1     普通权限点火按需任务（手动跑一轮不弹 UAC）
  run-daily.cmd        双击手动跑
  panel.cmd            双击打开主控台（现编 C# 面板）
  VERSION              唯一版本号来源：status / 面板标题 / exe --version 都读它
  config\settings.example.json  内核配置模板（入仓）
  config\settings.json  本机配置（不入仓）；插件片段装的时候并进来
  config\plugins.json   装了哪些插件（不入仓）
  lib\common.ps1       日志/JSON/备份/防睡眠/进程/通知；把 tools\gdcore 与 tools\gdinterop 现编进进程
  lib\modules.ps1      模块注册表：扫 lib\*-module.ps1，助手库在不在决定这一款算不算装好
  lib\decisions*.ps1   判定门面与共用底座（判定真身在核与 panel-cs）
  lib\rust-round.ps1   入口转接层：daily-clear 与内置件之间的那一段
  tools\install-plugin.ps1  插件装/列/卸
  tools\panel-cs\      C# 主控台（GUI + 命令行动词表 + 各档离线自检）
  tools\rustcore\      内置件 gdcore.exe 的源码（源码进仓、产物不入仓）
  tools\gdcore\ tools\gdinterop\  进程内现编的 C# 内核与 Win32 手脚
  tools\gdops\         跨平台指挥台（Tauri + Vite + TS；依赖与构建产物不入仓）
  tools\vision\ask.py  问模型那一层（截图与点击不在这里）
  tools\test-modules.ps1 / test-lib.ps1  框架与公共库自检
  logs\ state\ backups\  运行时生成（不入仓）
```

## 5. 许可、免责与隐私

- 本仓只有原创的脚本与源码（PowerShell / C# / Rust / 少量 Python），不含 MAA、M9A、MuMu、游戏客户端或任何游戏素材。
  插件仓同样只带脚本，第三方助手软件的许可跟随它们自己（MAA、M9A 为 AGPL-3.0）。
- 游戏用户协议禁止脚本/自动化：是否使用、用在哪台机器上，风险由使用者自行承担。
- 本仓与插件仓都不收 `config\settings.json`、`config\plugins.json`、`logs\`、`state\`、`backups\`：
  绝对路径、助手实例名、账号作息、视觉 Key、通知 webhook 都只活在你自己的机器上。
  导出与发布前有本机数据扫描（文本 + 二进制两路），本机专属值不许跟着源码出去。

MIT，见 [LICENSE](LICENSE)。
