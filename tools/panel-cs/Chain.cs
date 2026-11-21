// tools/panel-cs/Chain.cs —— B-3②-b2(2026-10-14)：链体丙档的**直答件**——现场 verb。
// (题面逐字见 docs\rust-core-plan.md §11.52 ④ 那句"②-b2＝panel-cs 侧链体丙档十三枚直答件(含 obs／skip 的
//  '读日志尾'那半)；不升号"。本批两枚契约号一字不动，也不改任何既有 verb 的入参／回执形状 ——
//  §11.50 ②-1 那条判据不因 b 系列放松，需要动既有 verb 回执的，当场停手登记，不"顺手加键"。)
//
// 这一份管的是"链体那十一枚 ask 里，必须由这台机器亲手干的那一半"：
//   takeover-processes         Invoke-GdTakeover(lib\common.ps1:665；⚠ 11-05 转接刀·运行面刀⑨ 该真身已自 PS 侧出仓，这枚坐标自此按历史引文读、不换算)：接管关闭，先判 takeOverAssistant。
//   stop-maa-and-game          Stop-MaaAndGame(旧 lib\maa.ps1 真身，11-03 转接刀·方舟拉起族整枚出仓；这一路的唯一事实源自此是本件)。
//   stop-m9a                   Stop-M9A(旧 lib\m9a.ps1:289 的 PS 壳；⚠ 11-06 转接刀·运行面刀⑫ 笔(a) 整枚出仓，这一路的唯一事实源自此是本件——本族坐标(历史 :289／话术 :293)自此按历史引文读、不换算)。
//                              (⚠ 10-26 第五刀刀②、11-01 坐标清账刀两趟换算留档；那句"已关闭 M9A($n 个进程)"的真身现只在本件 StopM9A 一处活着，双源登记自此只剩一源)。
//   read-maa-fight-skip        Get-GdMaaFightSkip(lib\maa.ps1:60 起；⚠ 11-05 转接刀·运行面刀⑨ 那枚壳已出仓，坐标按历史引文读)的**读日志尾那半**：gui.log 全量 → 递给已在场的
//                              detect-maa-fight-skip(Orchestration.cs 批次 5)，判据一枚都不在这儿重写。
//   read-maa-stage-observation Get-GdMaaStageObservation(lib\maa.ps1:83 起；⚠ 11-05 转接刀·运行面刀⑨ 那枚壳已出仓，坐标按历史引文读)的同一半：asst.log 尾 16MB + gui.log 全量
//                              + 当天日期 → 递给 observe-maa-stage-open。
//   —— 起进程／写盘侧(start-ark-client／start-mumu／apply-maa-daily-config／apply-m9a-instance／
//      update-ark-farm-ledger)在同一份文件的**第二笔**里落码；落码分两笔、**入库记一笔**
//      (新件只能整枚进，拆两笔提交要给一枚未入库的文件做 hunk 级暂存，不值)。
//
// 与"桌子"(②-b1)的分工照旧：这一份仍然**不裁任何判定**。`read-maa-*` 两枚只做"现场读盘 + 形状整理"，
// 判定本体逐字留在 Orchestration.cs —— 与 lib\maa.ps1 那两个壳自己的注释说的是同一句话(「本壳只做现场读盘与
// 形状整理，不加任何 if 判定」)。这里把那句话原样兑现，不因为"到了 C# 一侧"就多长出一个 if。
//
// 三处定档(通读 #450 时改的口，落码前登记，②-b3 的答手照这三条办)：
//   1. **直答件必须整枚答完**：核内 `launch`／`mumu` 各只发一发 ask 就等布尔，链体没有"再等一次"的第二发。
//      所以 start-ark-client 要把"等窗口 + 暖身 + 启动器更新页的循环与真点击"一并干完，start-mumu 要把
//      "launch + 轮询到 AndroidStarted"干完。⚠ 此前"相位循环留给核内(②-b3)"的分岔**作废**：那不是把活留给核，
//      而是把 10-11 那次现场缺陷的修法(更新页按画面确认)整段砍掉。判据仍一枚不重写 —— 相位映射继续住
//      Live.cs 的 check-ark-launcher，本文件只做"睡眠、循环、真点击、落日志"那四样活，与 lib\maa.ps1:629-630
//      那条分工("判据全在 C# 的 check-ark-launcher；这里只做活")同构，只是把"这里"从 PS 换成了 Chain.cs。
//   2. **verb 回执比 PS 真身多几格**(Found／Done／Lines 这类)不算扩面：这些键只给 --roundtest 与将来的答手读，
//      不进 journal、不进核的判据。既有 verb 的回执一格没动。
//   3. **"读不出"这一档必须能表达**：skip 那枚旧身返 $null ⇒ 核内 `matches!(d, JVal::Null)` 走 None 分支，
//      与"读到但 Skipped=false"是**两回事**(前者不判、后者判成没认栽)。所以两枚 read-maa-* 都回 `Found`：
//      Found=false ⇒ 答手必须发 JVal::Null。obs 那枚不设这一格也安全(drive_obsrec 只按 Found 早退，
//      $null 与 Found=false 同效)，但仍回 `LogsRead` 一格把"两本日志都没读到"与"读到了没 Fight 行"分开 ——
//      这两档在话术里不是一回事，别让它糊掉。
//
// 双源登记(改一句要两侧同改；生产接线(②-d)之前 lib 那两个壳仍是事实源)：
//   * "…正在运行；要么手动关掉再跑，要么把 settings.runtime.takeOverAssistant 设为 true"、
//     "$Label 正在运行 → 先关掉再改配置(避免它退出时把配置写回旧值)" ↔ lib\common.ps1:676-678(⚠ 11-05 转接刀·运行面刀⑨：Invoke-GdTakeover 出仓，那两句话术随之离开 PS 侧；本锚在原写 HEAD 帧就指错了段落——真句在 HEAD:616／618，不换算也追不上，双源自此只剩本件一源)；
//   * "已关闭 MAA($n 个进程)"、"已关闭明日方舟端游($n 个进程)" ↔ lib\maa.ps1:744／748；
//     "已关闭 M9A($n 个进程)" ↔ 旧 lib\m9a.ps1:293(⚠ 11-06 刀⑫ 笔(a) 该 PS 壳整枚出仓，坐标按历史引文读、不换算；10-26 第五刀刀② 换算、原写 342 留档) —— 三句的括号里都带进程数、都不带句号，逐字照抄；
//   * gui.log／asst.log 两枚路径与 `debug\asst.log` 那条兜底、asst.log 只取尾 16MB(16777216) 那把尺
//     ↔ lib\maa.ps1:47-53(11-03 转接刀·方舟拉起族换算：摘件批给那本头注加了一行，整本自 13 行起推一格；原写 46-52) 与 Get-GdMaaStageObservation 的注释(「asst.log 连日不轮转能涨到几十 MB」；⚠ 11-05 转接刀·运行面刀⑨ 那枚壳连同这句注释已自 lib\maa.ps1 出仓，这句话如今只在本文件 :447 那条注释里活着)。
//
// 原样怪癖(照抄，不许"顺手修")：
//   1. **`-not $S.runtime.takeOverAssistant` 与 `-ne $false` 不是同一把尺**：前者"没填"就算**关**(缺键 ⇒ $null ⇒ 抛)，
//      后者"没填"算开着。所以本文件用 Gd.Truthy 判这几枚开关，**绝不复用 Round.FlagEnabled**(那是 §11.50 钉过的
//      `-ne $false` 语义，用在这儿会把"没配"的一轮从"当场拒绝"变成"默默接管关闭")。quitAssistantAfter／
//      closeArknightsAfter 同理，都是真值判。--roundtest 各钉一格缺键档。
//   2. **Stop-GdProcessTree 名曰树、实为按进程名**，返回的是匹配到的进程数(②-a 头注第 3 笔)。这里直调
//      Round.ProcTree 拿同一把尺，不另数一遍。
//   3. **接管那一轮之后 `Start-Sleep -Seconds 4` 是硬编码**，不读 settings、不按进程数缩放(真身逐字)。
//      它是"等它把配置写完再改"的那口气 —— 这里保持 4 秒默认，自检喂 sleepSeconds=0 免挂钟。
//   4. **$null 成员占位**：skip 的 plan／evidence 在旧侧走 `@($sv.Plan)`，空数组与 @(单枚 null) 的 Count 不同
//      (批次 9 那三坑之一)。这里 Gd.List 回来什么就原样给什么，不做"顺手压成空表"的整理。
//   5. **takeover 的 names 由调用方组装**：ark 那路是 MAA + 端游两枚名，m9a 那路只 M9A 一枚名 ——
//      那是链体的编排(lib\ark-daily.ps1 与 lib\r1999-daily.ps1 各自 executor 的 takeover 答臂——那两枚调用原本在 PS 回退臂里还各另有一份，随 10-24 刀②③ 两份都整段出仓了，按坐标清账批口径不引行号)，不进 verb。verb 只认 names 数组，
//      空名照旧 continue(真身那一句挡的是"paths 没填 ⇒ 文件名算出空串")。
//   6. **paths 没填 ⇒ 进程名算出空串 ⇒ 这一档不抛**：Stop-MaaAndGame 旧真身(11-03 摘件前实拍)在 maaDir／maaExe 空时会拿空名去
//      Get-Process(恒空 ⇒ 0 个进程 ⇒ 一句都不吭)。这里同形：ProcTreeValue 见空名直接回 0，不把那句
//      "没给进程名(这一枚按进程名匹配，不给就是空打)"的 GdRuleException 提前引到 stop 两枚里 —— 那是
//      ②-a 给 stop-process-tree 立的话术，两条路对"空名"的处理本来就不一样。
//
// 安全边界(与 Round.cs／Round.Data.cs 同一条自检纪律)：
//   * --roundtest **不真杀进程、不真起子进程、不读真盘**：进程名册喂 running／fakeProcs＋noKill，
//     日志面喂 guiLog／asstLog 指到临时树，settings 喂夹具(临时树里故意不建 config\settings.json，
//     漏喂就当场红)；本文件**一枚都不读 runtime 整块**，只按名取 takeOverAssistant／quitAssistantAfter／
//     closeArknightsAfter 三枚布尔 —— visionApiKey／webhook 那类本机专属值射程之外；
//   * 日志尾读进来的原文**不回给调用方**：回执只有判定后的七键／四键，Raw 一律不进信封(②-b1 那两枚的
//     SENTINEL 不外泄纪律在这儿同样成立)。
//
// ==================== 第二笔(同一批的后五枚，2026-10-14)：起进程与写盘那五枚 ====================
//  ① `start-ark-client`   Start-GdArkClient＋Wait-GdArkLauncherReady(旧 maa.ps1 真身两枚，11-03 转接刀·方舟拉起族整枚出仓；那两枚坐标 702／622 自此按历史引文读)＋
//                          Wait-GdCondition(common.ps1:979；⚠ 刀⑰ 笔(b) 该真身已自 lib\common.ps1 出仓，坐标按历史引文读)＋Get-GdWindowByTitle(common.ps1:1012；⚠ 11-05 转接刀·运行面刀⑨ 该真身已自 lib\common.ps1 出仓，坐标按历史引文读)：
//                          路径闸 → 窗口名册 → 起进程 → 等窗口 → 暖身 → 更新页那一段真点击。
//  ② `start-mumu`         Start-GdMuMu(mumu.ps1:38)＋Get-GdMuMuState(mumu.ps1:21)＋Invoke-GdNative(common.ps1:493)：
//                          info 读数 → 已在跑就吭一声 → control launch → 轮询到 AndroidStarted。
//  ③ `apply-maa-daily-config` Set-MaaDailyConfig(maa.ps1:116，11-03 拉起族摘件后现读、原写 115)：现场检查＋读盘＋递给 plan-maa-queue＋
//                          备份／写盘／回读逐队列比 IsEnable。规则段一枚都不在这儿( Processes.cs 第 3 批)。
//  ④ `apply-m9a-instance` Set-M9AInstance(m9a.ps1:73，10-26 第五刀刀② 后现读、原写 93；10-25 那趟写 83；⚠ 11-01 坐标清账刀再换算：那一格实拍是空行；⚠ 11-05 转接刀·运行面刀⑨ 该真身已自 lib\m9a.ps1 出仓，以上坐标全按历史引文读)：点号字面键那两格＋三句原话＋回读校验。
//  ⑤ `update-ark-farm-ledger` Update-GdArkFarmLedger(ark-farm.ps1:125)＋Get-GdArkItemMaps(:53)＋
//                          Read-GdArkFarmLedger(lib\ark-farm.ps1:93)：首跑按 DepotData 标定、offset 续读、记两本账、
//                          回一句总结(链体那句 `[string](Update-GdArkFarmLedger)` 要的就是 bare string)。
//
// 第二笔的双源登记(改一句要两侧同改；生产接线(②-d)之前 lib 那几份仍是事实源)：
//   * "端游路径不对：{exe}(改 settings.paths.arknightsExe)"、窗口标题默认式 '明日方舟|Arknights'、
//     "端游窗口已存在：[Name#Id] Title"、"端游进程在跑但还没有窗口(name)，等它出登录界面"、
//     "启动端游：{exe}"、"[dry-run] 跳过实际启动"、"端游窗口就绪：[Name#Id] Title"、
//     "等了 N 秒还没看到端游窗口(要登录/在更新/路径不对?)" ↔ lib\maa.ps1:702-737；
//   * 启动器那六句话术(问不动／点击没发出去／已点下去／这一下没点实／认不出这一档相位／等了 N 秒还没进游戏)
//     ↔ lib\maa.ps1:622-700，其中 needs-click 那一路点完睡 30、waiting 睡 60 是**硬编码**；
//   * "MuMu 目录还没填(先跑 setup.ps1)"、"MuMuManager 无法读取虚拟机信息(index)：Raw"、
//     "MuMu 虚拟机 Index(Name)已在运行"、"启动 MuMu 虚拟机 Index(Name)"、"launch 返回码 C：ErrOut" ↔ lib\mumu.ps1:11-52；
//   * "MAA 正在运行，请先关闭再改配置"、"MAA 配置不存在：…"、"MAA 配置回读不一致：…(备份：leaf)"、
//     "MAA 配置已更新并校验通过(备份：leaf)"、"(无需改动)" ↔ lib\maa.ps1:116-194(11-03 拉起族摘件后现读、原写 115-193)；
//   * "M9A 正在运行，请先关闭再改实例"、"M9A appsettings 不存在：…"、"M9A 实例切换回读失败(备份：leaf)"、
//     "M9A 实例已切到[Name](备份：leaf)"、"(M9A 在跑，交给命令行 -i 转发)"、"(已是 Name，无需改动)" ↔ lib\m9a.ps1:72-102(10-26 第五刀刀② 后现读、原写 93-123；⚠ 11-05 转接刀·运行面刀⑨ 那三十一行整段出仓，这三句话术自此只在本件的 ApplyM9AInstance 里活着，双源登记自此只剩一源)；
//   * "stages.json 解析失败：…"、"未能建立 MAA 关卡表，理智计划不会被校验" ↔ lib\ark-decisions.ps1:14-40；
//     "item_index.json 解析失败：…"、ark-inventory／ark-droptable 那两句读不懂的话、
//     首跑标定那三句、"记了 N 场掉落、入账 +P"、"N 个名字没在材料表认出来(前 3 以 / 连)" ↔ lib\ark-farm.ps1:53-216；
//   * "命令不存在：File"、"timeout after Ns"、`-join ' '` 拼参数 ↔ lib\common.ps1:493-517。
//
// 第二笔新增的原样怪癖(照抄，不许"顺手修")：
//   7. **`arkLauncherMaxChecks` 的值填进的是 `maxClicks` 槽**：真身把"每轮最多问模型几次"直接当"最多点几次"用
//      (maa.ps1:626 那枚变量名就叫 maxClicks)，所以点数的上限等于问模型的上限。这里逐字，不拆成两把尺。
//   8. **`$clicks++` 在 if/else 之前**：那一下**没点实也算点过**(maa.ps1:676) —— 于是 PostMessage 一直失败时
//      预算照样烧光，到 arkLauncherMaxChecks 次就转 uncertain 放行。这是止损而不是笔误，照抄。
//   9. **Invoke-GdNative 拼参数不加引号**：`$Arguments -join ' '` ⇒ 带空格的参数会被对方拆成两枚。
//      MuMuManager 那三路(info／control launch／shutdown)都不带空格，所以现场一直没炸；这里同形。
//  10. **Wait-GdCondition 的 Sleep 在 return 之前无条件执行**：命中那一档不睡(先判再睡)，但**超时那一档**
//      最后一次判完还多睡一轮才退出循环。挂钟差一轮 poll，不修。
//  11. **`Get-GdStageTable` 与 `Get-GdArkItemMaps` 的进程内缓存**：PS 侧是 `$global:` 一次读定终身。
//      这里只在**现场读**那一路建缓存(夹具给了就不缓存)，免得 --roundtest 喂第二份夹具时被上一份顶掉。
//  12. **ark-farm 的总结句可能带本机绝对路径**(offset.path 就是 guiLog 全路径)：真身把 path 原样落进
//      inventory.json，但**不回话术** —— 这里同样只落盘不进 Summary，别"顺手"把路径打给调用方。
//
// 第二笔的安全边界(与上面同一条，只是面更宽)：
//   * --roundtest **不真起任何子进程、不开任何窗口、不写真配置**：窗口名册喂 `windows`，
//     原生命令喂 `nativeRuns`(按序消费、取完重复最后一枚 —— 于是 info→launch→info 那三步的顺序可以直接钉死)，
//     MAA／M9A 的配置树喂 `config`／`appSettings` 指到临时树，关卡表与材料表喂 `table`／`itemIndex`，
//     库存缓存喂 `depotData`，state 三本喂 `stateDir`，启动器那一路喂 `launcherAsks`＋`shot`；
//     所有睡眠喂 0。漏喂就当场红(临时树里故意不建文件)。
//   * `start-ark-client` 的"真起端游"与"先没窗口后等到"两格**记 unobserved**：自检只走 dry-run 与夹具名册，
//     真启动那一句要等人工实跑。
//   * 启动器那一路的 needs-click 两档**同样记 unobserved**(10-14 落笔前实测改口，此处曾计划"注入无效句柄
//     照真身 else 分支钉 Sent=false"，那条作废)：`ark-window-click` 是一枚既有 verb、没有 dry-run 档
//     (护栏不许在这里加键)，而它的真身 Live.WindowClick → GdWin.TryForeground 会**真的发一遍
//     keybd_event(VK_MENU down/up)＋SetForegroundWindow＋Sleep(350)**，落空还叠 PostMessage×3 ——
//     自检的纪律是"不碰输入注入、不碰别人的窗口"(只读自检不许要求机器现场状态)，为钉一句话术去按 ALT
//     不是便宜买卖。于是这一档只验到"相位算得出 needs-click、点位换算对"，Sent 那半留给实跑。
//   * 写盘两枚只在 `apply=true` 时才落笔，且路径必须落在临时树里；预览档(apply=false)一枚字节都不许写。
//   * 读进来的日志／JSON 原文不回给调用方：inventory 与 droptable 的**内容**不进回执，只进 Summary 那一句
//     计数与 Done／Blocks／Plus／Unknown 这几格判定后的数。
//   * 其余四格同样**记 unobserved**，不是因为难写，是因为钉它们等于自证不了的东西：
//     ① asst.log 那 16MB 尾窗本体(造一份 16MB 假日志只测得出"会裁"，测不出裁得对不对，而口径本身
//        是"只取尾窗"这一条算术，已在常量旁钉死)；
//     ② takeover 里硬编码那 4 秒睡眠本体(睡眠长度可由 sleepSeconds=0 绕开，绕不开的那半是挂钟)；
//     ③ `MAA 配置回读不一致` 那一抛(要它就得让写盘真写坏，临时树里写坏一次就是给自检加一台造假机)。
//   * 原第④条「`[dry-run] 跳过启动器更新页检查` 经 start-ark-client 不可达」**已作废**(10-14 落自检时改口)：
//     不可达是真事，但结论不是"记 unobserved"而是"把这枚私有助手的可见性放到 internal，让 --roundtest 直调它"
//     —— 相位那五档与 dry-run／开关关掉这两句从此都照钉，判定本体仍旧一枚不在自检里重写(全在 check-ark-launcher)。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GdPanel
{
    static class Chain
    {
        // ==================== 入参取数：缺键=自己现读(现场)，有键=用夹具 ====================
        // 与 Round.cs:84-132／Round.Data.cs:75-129 同形的那把尺。本批**不动那两份的可见性**(题面＝新立，不改装)，
        // 所以这里自带第三份；唯一复用过去的是 Round.ProcTree(那把"名曰树"的尺在 C# 侧只许有一个真身)。

        static Dictionary<string, object> View(Dictionary<string, object> raw)
        {
            Dictionary<string, object> d = Gd.Ci(raw);
            if (d == null) d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return d;
        }

        static bool Given(Dictionary<string, object> c, string k)
        {
            object v;
            return c.TryGetValue(k, out v) && v != null;
        }

        static string OptStr(Dictionary<string, object> c, string k, Func<string> def)
        {
            object v;
            if (c.TryGetValue(k, out v) && v != null) return Gd.Str(v);
            return def == null ? "" : def();
        }

        static bool OptBool(Dictionary<string, object> c, string k)
        {
            object v;
            if (!c.TryGetValue(k, out v) || v == null) return false;
            return Gd.Truthy(v);
        }

        static long OptLng(Dictionary<string, object> c, string k, long def)
        {
            object v;
            if (!c.TryGetValue(k, out v) || v == null) return def;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        static List<string> OptList(Dictionary<string, object> c, string k)
        {
            object v;
            if (!c.TryGetValue(k, out v)) return new List<string>();
            List<string> outList = new List<string>();
            foreach (object o in Gd.List(v)) outList.Add(Gd.Str(o));
            return outList;
        }

        static Dictionary<string, object> Sub(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        static long Num(object v)
        {
            if (v == null) return 0;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return 0; }
        }

        // 真身那几句 Write-GdLog 的落地：进 Lines(回执里给人看的那串)＋进 GdLog(verb 子进程的 stdout／信封)。
        // 与 Round.Data.Say 同一档，不另起一套。
        // 第二笔起把档位判据补全：真身回放 Logs 时那句是 `if ($lvl -and ($lvl -ne 'INFO'))` —— 空档与 INFO(不分大小写)
        // 都走默认档那一路，与"明写 -Level INFO"落到同一行日志上；第一笔那五枚传的就是 WARN／INFO，语义不变。
        static void Say(List<object> lines, string level, string msg)
        {
            lines.Add(msg);
            if (string.IsNullOrEmpty(level) || level.Equals("INFO", StringComparison.OrdinalIgnoreCase))
                GdLog.Line(msg);
            else GdLog.Line(msg, level);
        }

        // ==================== settings：现场读只在该读时才读(缺 settings 夹具才会落到真盘) ====================

        static Dictionary<string, object> settingsCache;

        static Dictionary<string, object> SettingsOf(Dictionary<string, object> c)
        {
            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "settings"));
            if (given != null) return given;
            if (settingsCache != null) return settingsCache;
            List<string> missing;
            // quiet=true ＋ requirePaths 给空表：只取键、不催助手目录(催是调用方 Stop-GdBeforeRun 的事)。
            settingsCache = GdSettings.Load(out missing, true, new List<string>());
            return settingsCache;
        }

        static Dictionary<string, object> PathsOf(Dictionary<string, object> c)
        {
            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "paths"));
            if (given != null) return given;
            return Gd.Map(Gd.Get(SettingsOf(c), "paths")) ?? new Dictionary<string, object>();
        }

        // 原样怪癖第 1 笔的那把尺：真值判(缺键 ⇒ 关)。与 Round.FlagEnabled(`-ne $false`，缺键 ⇒ 开)**故意不同**。
        static bool RuntimeFlagOn(Dictionary<string, object> c, string key)
        {
            return Gd.Truthy(Gd.Get(Gd.Get(SettingsOf(c), "runtime"), key));
        }

        // ==================== 现场量夹具：路径、日期、日志尾、进程名册 ====================

        static string StrOf(object v, string def) { return v == null ? def : Gd.Str(v); }

        static string BaseName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            try { return Path.GetFileNameWithoutExtension(path); } catch { return ""; }
        }

        // Get-MaaPaths(maa.ps1:46) 的现场读：只取这一份要用的那四格，拼装走 GdEnv.JoinPath(= Join-GdPath，
        // base 没填返空串不抛)。guiLog／asstLog 两格可被夹具顶掉(自检把日志指到临时树上的那份文件)。
        static Dictionary<string, string> MaaPathsNow(Dictionary<string, object> c)
        {
            Dictionary<string, object> p = PathsOf(c);
            string dir = StrOf(Gd.Get(p, "maaDir"), "");
            string exe = StrOf(Gd.Get(p, "maaExe"), "");
            string asstRel = StrOf(Gd.Get(p, "maaAsstLog"), "");
            if (asstRel.Length == 0) asstRel = @"debug\asst.log";      // maa.ps1:48 那条 MAA 默认兜底，逐字
            Dictionary<string, string> m = new Dictionary<string, string>();
            m["Name"] = BaseName(exe);
            m["Exe"] = GdEnv.JoinPath(dir, new string[] { exe });
            m["Config"] = GdEnv.JoinPath(dir, new string[] { StrOf(Gd.Get(p, "maaConfig"), "") });
            m["GuiLog"] = OptStr(c, "guiLog", delegate { return GdEnv.JoinPath(dir, new string[] { StrOf(Gd.Get(p, "maaGuiLog"), "") }); });
            m["AsstLog"] = OptStr(c, "asstLog", delegate { return GdEnv.JoinPath(dir, new string[] { asstRel }); });
            return m;
        }

        static string M9ANameNow(Dictionary<string, object> c)
        {
            return BaseName(StrOf(Gd.Get(PathsOf(c), "m9aExe"), ""));
        }

        // Get-GdFileTail(common.ps1:995；⚠ 刀⑰ 笔(b) 该真身已自 lib\common.ps1 出仓，坐标按历史引文读) 的逐字复刻：文件不在 ⇒ Text='' Offset=0；
        // Offset 越界 ⇒ 归零全读(那是"日志被轮转"那一档，不是错误)；FileShare=ReadWrite(MAA 正在往里写)；
        // 编码 UTF8 —— 与 PS 那侧 [System.Text.Encoding]::UTF8 同一个构造(能吃 BOM，无 BOM 也按 UTF8 解)。
        sealed class Tail
        {
            public string Text = "";
            public long Offset = 0;
        }

        static Tail ReadTail(string path, long offset)
        {
            Tail t = new Tail();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return t;
            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long size = fs.Length;                              // 必须在 StreamReader 之前取：sr.Dispose 会把 fs 一起关掉
                long from = offset > size ? 0 : offset;             // 轮转 ⇒ 从头读
                try { fs.Seek(from, SeekOrigin.Begin); } catch { fs.Seek(0, SeekOrigin.Begin); }
                using (StreamReader sr = new StreamReader(fs, new UTF8Encoding(false)))
                {
                    try { t.Text = sr.ReadToEnd() ?? ""; } catch { t.Text = ""; }
                }
                t.Offset = size;
                return t;
            }
        }

        // verb: read-file-tail —— 刀⑰ 笔(b)(2026-11-07)：Get-GdFileTail(common.ps1 真身，随批出仓)的出口露出。
        // 规则零新增：读的就是上面 ReadTail 那把尺(逐字复刻语义)；body {path, offset(缺省 0)}，回执 {Text, Offset}。
        // 已登记的语义差：PS 侧空 Path 会在 Test-Path 当场抛，核侧 File.Exists('')=false ⇒ ('',0) ——
        // 消费方(run-via-task 镜像续读)传的是实路径，不落这一档，故不改 ReadTail 也不在此补闸。
        internal static object ReadFileTail(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string path = OptStr(c, "path", null);
            long offset = OptLng(c, "offset", 0);
            Tail t = ReadTail(path, offset);
            return Gd.Out("Text", t.Text, "Offset", t.Offset);
        }

        // verb: scan-source-patterns —— 刀⑰ 笔(c)-2 第 1 批(2026-11-07) B1：门脚本族「拿 PS 文本当尺」判据的对面。
        // 契约格见 docs\rust-core-plan.md §11.276：body {files(数组), pattern(正则), expect(缺省 any)}，回执 {Hits,Count,Passed,Expect}。
        // 本枚与笔(b) 三枚「只复用在场真身」不同——规则正当新增且只新增「扫描与判定」这一条：判定就是本体的本体，
        // PS 门侧自此只余「组参＋按格 Check」两件事。输入闸四枚全走 GdRuleException＝拒跑非判红(形制随本件既有 throw 族；
        // §11.276 正文写的 GdUsageException 是话术瑕疵，随下帧 docs 改口)：空 files／空 pattern／缺文件／坏正则。
        // 合法而零命中是正当读数(Count=0)；大小写敏感为默认——PS -match 原生不敏感的语义差逐格登记，要原语义把 (?i) 写进 pattern。
        internal static object ScanSourcePatterns(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<string> files = OptList(c, "files");
            if (files.Count == 0) throw new GdRuleException("scan-source-patterns：files 缺键或空数组——没问任何文件却交回零命中，不许成立");
            string pattern = OptStr(c, "pattern", null);
            if (string.IsNullOrEmpty(pattern)) throw new GdRuleException("scan-source-patterns：pattern 缺键或空串——空正则匹配一切");
            Regex rx;
            try { rx = new Regex(pattern, RegexOptions.CultureInvariant); }
            catch (ArgumentException ex) { throw new GdRuleException("scan-source-patterns：正则编译失败：" + ex.Message); }
            string expect = OptStr(c, "expect", delegate { return "any"; });
            foreach (string f in files)
                if (!File.Exists(f)) throw new GdRuleException("scan-source-patterns：文件不存在：" + f);
            List<object> hits = new List<object>();
            foreach (string f in files)
            {
                string[] lines;
                try { lines = File.ReadAllLines(f, new UTF8Encoding(false)); }
                catch (Exception ex) { throw new GdRuleException("scan-source-patterns：读文件失败：" + f + "(" + ex.Message + ")"); }
                for (int i = 0; i < lines.Length; i++)
                    if (rx.IsMatch(lines[i]))
                        hits.Add(Sub("File", f, "Line", (long)(i + 1), "Text", lines[i]));
            }
            long count = hits.Count;
            bool passed;
            if (expect == "zero") passed = (count == 0);
            else if (expect == "nonzero") passed = (count > 0);
            else if (expect == "any") passed = true;
            else throw new GdRuleException("scan-source-patterns：expect 只认 zero／nonzero／any，收到：" + expect);
            return Gd.Out("Hits", hits.ToArray(), "Count", count, "Passed", passed, "Expect", expect);
        }

        // ==================== 刀⑰ 笔(c)-2 第 2 批(2026-11-07) B2：check-module-manifests ====================
        // 契约格定档见 docs\rust-core-plan.md §11.278，本批落码时的四处改判(四键必填／第十三枚规则／
        // Ack 退场名册进核／参数表与名册比对走大小写不敏感)按「错账用新笔登记」写进 §11.279。
        //
        // 射程＝tools\test-modules.ps1 的 [m2]「每份清单的完备性」与 [m6]「入口动词与真参数对得上」两段里那些
        // 「拿清单文本＋入口参数表当尺」的判据：读文本与集合比对两步全住核内，PS 门侧自此只余「组参＋按格 Check」
        // 两件事——与上一批 B1(scan-source-patterns)同形制＝门只问不自己算。
        //
        // 三档分档法(§11.278)在这里划的线：只有 PS 运行时才量得到的那一档(真注册、真 dot-source、真 scriptblock
        // 类型、真参数绑定)**不写文本假对面**——「清单体检没有意见」的运行时面继续由 lib\modules.ps1 的
        // Import-GdModules 装载期 throw 承接，[m6] 那枚「Test-GdMergedEntry 返回布尔」留在 PS 门里。
        // 唯一一处降档＝「钩子是 scriptblock」换成文本档「值以 { 起」，语义差当场登记：文本档认不出
        // 「以 { 起但其实是一枚串」那种写法，而现树两本清单的必需钩子 EnvProbes 与 status 一族全是块体，无格翻。
        //
        // 这里头没抄第二份名册：Get-GdRequiredPathKeys 的 key↔lib 对照表(lib\common.ps1:213)与必需钩子名册
        // (lib\modules.ps1:24)都由**门侧现问、当入参喂进来**(这就是四键必填的理由)；核只做集合比对。
        // 唯一进核的一张表是「哪一路的 Ack 已退场」(AckExitedIds，原住 test-modules.ps1:306 的那句硬编码判语)。

        sealed class ManifestRow
        {
            public string Id = "";
            public string LibFile = "";
            public bool BodyOk = false;      // 顶层 @{} 读得出才谈得上逐键判定(§11.279 改判⑤：读不出只落那一枚，不排一串空键假细节)
            public List<string> Problems = new List<string>();
            public Dictionary<string, string> Merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // 五枚平键＝[m2]:79 那串；三枚基础动词＝[m6]:293；四枚白名单＝[m6]:302；退场名册＝[m6]:306。
        static readonly string[] ManifestFlatKeys = new string[] { "Id", "Name", "ShortName", "AssistantLib", "SettingsBlock" };
        static readonly string[] EntryVerbBase = new string[] { "Ack", "ShopDone", "ShopReset" };
        static readonly string[] MergedVerbWhitelist = new string[] { "Ack", "ShopDone", "ShopReset", "Farm" };
        static readonly string[] AckExitedIds = new string[] { "ark" };

        // 串感知的"跳过一段字符串"：返回收尾引号的下标(调用方 i=q 后循环自增)，找不到回 -1。
        // 单引号串里 '' 是转义；双引号串里反引号连下一枚一起吃。here-string(@' / @")不在射程：
        // 现树两本清单与入口都没在这儿用 here-string，真用了本枚会当"读不出"落 manifest-body-missing／
        // entry-param-block 那一档——不是静默放过。
        static int SkipPsString(string t, int i)
        {
            char q = t[i];
            for (int j = i + 1; j < t.Length; j++)
            {
                char ch = t[j];
                if (q == '"' && ch == '`') { j++; continue; }
                if (ch == q)
                {
                    if (q == '\'' && j + 1 < t.Length && t[j + 1] == '\'') { j++; continue; }
                    return j;
                }
            }
            return -1;
        }

        // 从 open(指向 '{' / '(' / '[')找配平的闭括号下标；只数这一种括号，串内不算，# 到行尾跳过。
        // 为什么只数一种：清单里 `@{ Ack = 'x' }` 这种嵌套靠同种括号计深度就够，混着数会被值里的括号骗。
        static int MatchClose(string t, int open)
        {
            if (open < 0 || open >= t.Length) return -1;
            char c0 = t[open];
            char cl = c0 == '{' ? '}' : (c0 == '(' ? ')' : ']');
            int depth = 0;
            for (int i = open; i < t.Length; i++)
            {
                char ch = t[i];
                if (ch == '\'' || ch == '"')
                {
                    int q = SkipPsString(t, i);
                    if (q < 0) return -1;
                    i = q;
                    continue;
                }
                if (ch == '#')
                {
                    int e = t.IndexOf('\n', i);
                    if (e < 0) return -1;            // 注释一直吃到文件尾 ⇒ 后面没有配平了
                    i = e - 1;
                    continue;
                }
                if (ch == c0) depth++;
                else if (ch == cl) { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        // 把"括号内部"(start..end)按顶层分隔符切段：深度按 { ( [ 三种一起数(属性块 [ValidateSet(...)] 里的
        // 逗号不算分隔符，正是要防的那条)，串内不算，行内 # 注释整段换成等长空格(位置不漂)。
        static List<string> SplitTop(string t, int start, int end, string seps)
        {
            List<string> segs = new List<string>();
            StringBuilder cur = new StringBuilder();
            int depth = 0;
            for (int i = start; i < end; i++)
            {
                char ch = t[i];
                if (ch == '\'' || ch == '"')
                {
                    int q = SkipPsString(t, i);
                    if (q < 0 || q >= end) q = end - 1;
                    for (int k = i; k <= q; k++) cur.Append(t[k]);
                    i = q;
                    continue;
                }
                if (ch == '#')
                {
                    int e = t.IndexOf('\n', i);
                    if (e < 0 || e > end) e = end;
                    for (int k = i; k < e; k++) cur.Append(' ');
                    i = e - 1;
                    continue;
                }
                if (ch == '{' || ch == '(' || ch == '[') { depth++; cur.Append(ch); continue; }
                if (ch == '}' || ch == ')' || ch == ']') { if (depth > 0) depth--; cur.Append(ch); continue; }
                if (depth == 0 && seps.IndexOf(ch) >= 0)
                {
                    if (cur.ToString().Trim().Length > 0) segs.Add(cur.ToString());
                    cur.Length = 0;
                    continue;
                }
                cur.Append(ch);
            }
            if (cur.ToString().Trim().Length > 0) segs.Add(cur.ToString());
            return segs;
        }

        // 一段 "KEY = 值" 在第一个**顶层** '=' 处分成家(值里可以有 '='，键里不会)。分不出家交回 null 由调用方跳过。
        static PsPair SplitKeyValue(string seg)
        {
            int depth = 0;
            for (int i = 0; i < seg.Length; i++)
            {
                char ch = seg[i];
                if (ch == '\'' || ch == '"')
                {
                    int q = SkipPsString(seg, i);
                    if (q < 0) return null;
                    i = q;
                    continue;
                }
                if (ch == '#')
                {
                    int e = seg.IndexOf('\n', i);
                    if (e < 0) return null;
                    i = e - 1;
                    continue;
                }
                if (ch == '{' || ch == '(' || ch == '[') { depth++; continue; }
                if (ch == '}' || ch == ')' || ch == ']') { if (depth > 0) depth--; continue; }
                if (ch == '=' && depth == 0)
                {
                    PsPair p = new PsPair();
                    p.Key = PsScalar(seg.Substring(0, i));
                    p.Value = seg.Substring(i + 1).Trim();
                    return p;
                }
            }
            return null;
        }

        // 标量取原文：'x' / "x" 去外壳(单引号串里的 '' 还原成一枚 ')，其它(裸值、$true、[pscustomobject]@{…})原样交回。
        static string PsScalar(string v)
        {
            string t = (v == null ? "" : v).Trim();
            if (t.Length >= 2 && ((t[0] == '\'' && t[t.Length - 1] == '\'') || (t[0] == '"' && t[t.Length - 1] == '"')))
            {
                string inner = t.Substring(1, t.Length - 2);
                if (t[0] == '\'') return inner.Replace("''", "'");
                return inner;
            }
            return t;
        }

        // 值 → 字符串名册。分四档，"读了而读出来是空"与"读不出"分开：
        //   @('a','b') ⇒ 抠所有带引号的项(含空项 '' —— 老形 Assert 那句"PathKeys 里有空项"就靠它才不丢)；
        //   @()        ⇒ 零项(空表，合法读数)；
        //   $xxx       ⇒ 零项(PS 里 $null 展开成空，@($null) 的 Count 是 0)；
        //   一枚裸串    ⇒ 一项(与 PS `@('maa.ps1')` 与 @(...) 两形同结果)。
        static List<string> PsValueList(string v)
        {
            List<string> outList = new List<string>();
            string t = (v == null ? "" : v).Trim();
            if (t.Length == 0) return outList;
            if (t.StartsWith("$")) return outList;
            int p = t.IndexOf('(');
            if (t.StartsWith("@") && p > 0)
            {
                int q = t.LastIndexOf(')');
                string inner = q > p ? t.Substring(p + 1, q - p - 1) : t.Substring(p + 1);
                foreach (Match m in Regex.Matches(inner, @"'((?:[^']|'')*)'|""([^""]*)"""))
                    outList.Add(m.Groups[1].Success ? m.Groups[1].Value.Replace("''", "'") : m.Groups[2].Value);
                return outList;
            }
            if (t.StartsWith("@") || t.StartsWith("[")) return outList;      // @{…}／[pscustomobject]@{…} 不是名册
            outList.Add(PsScalar(t));
            return outList;
        }

        // 清单里 RequiresAdmin 写的是裸 $true／$false(不是串)。
        static bool PsValueBool(string v)
        {
            string t = (v == null ? "" : v).Trim();
            return t.Equals("$true", StringComparison.OrdinalIgnoreCase);
        }

        // 值文本 → 顶层 @{} 的键值表：先 Trim 再取第一个 '{' 当家，交回 parsed=false 表示"这块读不出"(与"空表"分档)。
        static Dictionary<string, string> PsHashtableOf(string valueText, out bool parsed)
        {
            parsed = false;
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string t = (valueText == null ? "" : valueText).Trim();
            int open = t.IndexOf('{');
            if (open < 0) return d;
            int close = MatchClose(t, open);
            if (close < 0) return d;
            foreach (string seg in SplitTop(t, open + 1, close, ",;\r\n"))
            {
                PsPair p = SplitKeyValue(seg);
                if (p == null || p.Key.Length == 0) continue;
                d[p.Key] = p.Value;
            }
            parsed = true;
            return d;
        }

        static string Val(Dictionary<string, string> d, string k)
        {
            if (d == null) return "";
            string v;
            return d.TryGetValue(k, out v) ? v : "";
        }

        // PS 的 -contains／hashtable.ContainsKey 对字符串键都是**大小写不敏感**的，本枚逐字对齐(§11.279 登记的口径)。
        static bool HasItem(List<string> xs, string v)
        {
            if (xs == null) return false;
            for (int i = 0; i < xs.Count; i++)
                if (string.Equals(xs[i], v, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool HasItem(string[] xs, string v)
        {
            if (xs == null) return false;
            for (int i = 0; i < xs.Length; i++)
                if (string.Equals(xs[i], v, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // 剥注释两刀：<# … #> 整块换成空格(块里那些不带井号的中间行正是"注释里写了一枚像键的行"的来源，
        // 与 PS 侧 Get-GdCodeOnlyText 同一判语)；行首(允许缩进)整行 # 丢掉。行内 # 交给扫描器在串外处理。
        static string StripPsComments(string text)
        {
            string t = Regex.Replace(text ?? "", "<\\#[\\s\\S]*?\\#>", " ");
            string[] lines = t.Split('\n');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i];
                string trimmed = s.TrimStart();
                if (trimmed.StartsWith("#")) continue;
                sb.Append(s);
                if (i + 1 < lines.Length) sb.Append('\n');
            }
            return sb.ToString();
        }

        // 开关名：清单 EntryVerbs.Merged 写了这一枚动词就用它那串(带前导 -)，缺键回落 -<动词名>。
        // 与 lib\decisions-common.ps1:94 Get-GdEntryVerb 的语义差逐格登记：那枚先问合体闸 Test-GdMergedEntry，
        // 本枚**不问**——现树两棵清单的 Merged 把动词写全，合体与否走的都是同一个名字；单游戏导出树上
        // [m6] 问的入口也仍是 daily-clear.ps1(合体形)，同值。留 PS 那枚 Test-GdMergedEntry 继续量合体本身。
        static string FlagFor(ManifestRow m, string verb)
        {
            string v = Val(m.Merged, verb);
            if (v.Length > 0)
            {
                string s = PsScalar(v);
                if (s.Length > 0) return s.StartsWith("-") ? s : "-" + s;
            }
            return "-" + verb;
        }

        static Dictionary<string, object> ManifestRowToDict(ManifestRow m)
        {
            return Sub("Id", m.Id, "LibFile", m.LibFile, "Problems", m.Problems.ToArray());
        }

        // verb: check-module-manifests —— 门侧现问的四键喂进来，核内做"读文本＋集合比对"两步，交回恰四键回执。
        // 入闸四枚全走 GdRuleException＝拒跑非判红(缺任一枚就核自己抄一份名册，正是 §11.186 点名的病灶)：
        //   root(仓库根，去 lib\ 扫 *-module.ps1)／entry(入口件，读它的 param 块)／
        //   requiredPathKeys(＝Get-GdRequiredPathKeys 现量)／requiredHooks(＝$script:GdModuleRequiredHooks)。
        //   后两枚**只判在场不判非空**：空表是合法读数(那棵树上一路都没装)，缺键才是没问。
        // 出＝{Modules:[{Id,LibFile,Problems[]}], EntryProblems[], TotalProblems, Passed}；
        //   TotalProblems 是**命中条数之和**不是段数(老形一格里 throw 十条意见，新形逐条点名)；
        //   Passed 由核内判(TotalProblems==0)，不交给回执码背——回执码 0 只说"核跑成了"。
        internal static object CheckModuleManifests(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string root = RequireStr(c, "root", "拿它去 lib\\ 扫 *-module.ps1");
            string entry = RequireStr(c, "entry", "入口件的实路径，读它的 param 块当参数表");
            List<string> reqKeys = RequireList(c, "requiredPathKeys", "这一路该催的路径键名册");
            List<string> reqHooks = RequireList(c, "requiredHooks", "清单必需的钩子名册");

            string libDir = Path.Combine(root, "lib");
            string[] files;
            try { files = Directory.Exists(libDir) ? Directory.GetFiles(libDir, "*-module.ps1") : new string[0]; }
            catch (Exception ex) { throw new GdRuleException("check-module-manifests：lib 目录读不动：" + libDir + "(" + ex.Message + ")"); }
            Array.Sort<string>(files, StringComparer.Ordinal);      // 与 PS 侧 Sort-Object Name 同序(现树两名两把尺同结果)

            List<ManifestRow> rows = new List<ManifestRow>();
            foreach (string f in files)
            {
                ManifestRow m = new ManifestRow();
                m.LibFile = Path.GetFileName(f);
                string code = "";
                bool readable = true;
                try { code = StripPsComments(File.ReadAllText(f, new UTF8Encoding(false))); }
                catch (Exception ex) { readable = false; m.Problems.Add("manifest-body-missing｜读文件失败：" + ex.Message); }
                Dictionary<string, string> top = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (readable)
                {
                    int reg = code.IndexOf("Register-GdModule", StringComparison.OrdinalIgnoreCase);
                    int open = reg < 0 ? -1 : code.IndexOf('{', reg);
                    if (open < 0) m.Problems.Add("manifest-body-missing｜这份件里没有 Register-GdModule 的顶层 @{}");
                    else
                    {
                        bool bodyParsed;
                        top = PsHashtableOf(code.Substring(open), out bodyParsed);
                        if (!bodyParsed)
                        {
                            m.Problems.Add("manifest-body-missing｜顶层 Register-GdModule 的 @{} 读不出配平的括号");
                            top = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                        else
                        {
                            m.Id = PsScalar(Val(top, "Id"));
                            m.BodyOk = true;
                        }
                    }
                }
                if (m.BodyOk)
                {
                    // ① 五枚平键非空([m2]:79-81)。② 短名形状([m2]:112)——空串同样过不了 ^\S+$，与老形两格同红。
                    foreach (string k in ManifestFlatKeys)
                        if (PsScalar(Val(top, k)).Length == 0) m.Problems.Add("flat-key-empty｜" + k);
                    string shortName = PsScalar(Val(top, "ShortName"));
                    if (!Regex.IsMatch(shortName, @"^\S+$")) m.Problems.Add("short-name-shape｜" + shortName);

                    // ③④ PathKeys([m2]:82 ＋ Assert:96-97 的"不在名单里")。
                    List<string> pathKeys = PsValueList(Val(top, "PathKeys"));
                    if (pathKeys.Count == 0)
                        m.Problems.Add("path-keys-empty｜" + (Val(top, "PathKeys").Length == 0 ? "键不在清单里" : "读了，是空表"));
                    foreach (string k in pathKeys)
                        if (!HasItem(reqKeys, k)) m.Problems.Add("path-key-not-required｜" + (k.Length == 0 ? "(空项)" : k));

                    // ⑤⑥⑦ Libs／助手库在册／声明的库在不在盘([m2]:87、:92、:91)。
                    //      老形 :87 的后半把与 :92 是同一把尺写两遍，换家后合成 assistant-lib-unlisted 一条，覆盖面不减。
                    List<string> libs = PsValueList(Val(top, "Libs"));
                    string assistant = PsScalar(Val(top, "AssistantLib"));
                    if (libs.Count == 0) m.Problems.Add("libs-empty｜" + (Val(top, "Libs").Length == 0 ? "键不在清单里" : "读了，是空表"));
                    else if (!HasItem(libs, assistant)) m.Problems.Add("assistant-lib-unlisted｜" + (assistant.Length == 0 ? "(助手库空)" : assistant));
                    foreach (string n in libs)
                    {
                        bool onDisk = false;
                        try { onDisk = File.Exists(Path.Combine(libDir, n)); } catch { onDisk = false; }
                        if (!onDisk) m.Problems.Add("lib-file-missing｜" + (n.Length == 0 ? "(空项)" : n));
                    }

                    // ⑧⑨ 提权两枚配套([m2]:93、:95 ＋ Assert:100)。
                    if (PsValueBool(Val(top, "RequiresAdmin")))
                    {
                        if (PsScalar(Val(top, "AdminWhy")).Length == 0) m.Problems.Add("admin-why-missing｜RequiresAdmin=$true");
                        if (PsScalar(Val(top, "ElevationFlag")).Length == 0) m.Problems.Add("elevation-flag-missing｜RequiresAdmin=$true");
                    }

                    // ⑩⑪⑫ Hooks：值以 { 起(scriptblock 的文本档，语义差见本段头注)＋键 ⊆ 必需名册∪Status* ＋
                    //     有 StatusSection 就得给 StatusSectionTitle([m2]:104-106、:107-109、:111)。
                    bool hooksParsed;
                    Dictionary<string, string> hooks = PsHashtableOf(Val(top, "Hooks"), out hooksParsed);
                    if (!hooksParsed) m.Problems.Add("hook-value-not-brace｜Hooks 这块读不出顶层 @{}");
                    foreach (KeyValuePair<string, string> hp in hooks)
                    {
                        if (!hp.Value.TrimStart().StartsWith("{")) m.Problems.Add("hook-value-not-brace｜" + hp.Key);
                        if (!HasItem(reqHooks, hp.Key) && !hp.Key.StartsWith("Status", StringComparison.OrdinalIgnoreCase))
                            m.Problems.Add("hook-key-stray｜" + hp.Key);
                        if (hp.Key.Equals("StatusSection", StringComparison.OrdinalIgnoreCase) &&
                            PsScalar(Val(top, "StatusSectionTitle")).Length == 0)
                            m.Problems.Add("status-section-title-missing｜StatusSection");
                    }

                    // 开关名的家：EntryVerbs.Merged(内层那层 @{})——[m6] 的动词表由它自己声明，四枚白名单筛过才收。
                    // 读不出(EntryVerbs 缺键／Merged 缺键／括号不配平)一律交回空表：回落名 -<动词> 随后照样在入口侧翻红，
                    // 不会两头不落。
                    bool evParsed;
                    Dictionary<string, string> ev = PsHashtableOf(Val(top, "EntryVerbs"), out evParsed);
                    bool mgParsed;
                    m.Merged = PsHashtableOf(Val(ev, "Merged"), out mgParsed);
                    if (!evParsed || !mgParsed) m.Merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                rows.Add(m);
            }

            // ---------------- 入口侧：读 daily-clear.ps1 的 param 块，与清单声明的开关名对照 ----------------
            List<string> entryProblems = new List<string>();
            List<string> entryParams = new List<string>();
            bool entryReadable = false;
            if (!File.Exists(entry)) entryProblems.Add("entry-file-missing｜" + entry);
            else
            {
                string ecode = "";
                try { ecode = StripPsComments(File.ReadAllText(entry, new UTF8Encoding(false))); entryReadable = true; }
                catch (Exception ex) { entryProblems.Add("entry-file-missing｜读文件失败：" + ex.Message); }
                if (entryReadable)
                {
                    // 行首 param 块(脚本级那一枚，取**第一个**命中：入口的 param 在所有函数之前)。
                    Match pm = Regex.Match(ecode, @"(?mi)^[ \t]*param[ \t]*\(");
                    if (!pm.Success) entryProblems.Add("entry-param-block｜读不到行首的 param 块");
                    else
                    {
                        int po = ecode.IndexOf('(', pm.Index);
                        int pc = MatchClose(ecode, po);
                        if (pc < 0) entryProblems.Add("entry-param-block｜param 块的括号配不平(吃到文件尾)");
                        else
                        {
                            foreach (string seg in SplitTop(ecode, po + 1, pc, ","))
                            {
                                // 段里第一个 $名字 就是参数名(属性 [ValidateSet(…)] 不带 $，默认值 = $PSScriptRoot 那种排在后面)。
                                Match nm = Regex.Match(seg, @"\$[A-Za-z_][A-Za-z0-9_]*");
                                if (nm.Success) entryParams.Add(nm.Value.Substring(1));
                            }
                            if (entryParams.Count == 0) entryProblems.Add("entry-param-table-empty｜读了，零枚参数");
                        }
                    }
                }
            }

            foreach (ManifestRow m in rows)
            {
                if (!m.BodyOk) continue;     // 清单本体没读出来的那一行，入口侧不补假细节(改判⑤同批)
                List<string> verbs = new List<string>(EntryVerbBase);
                foreach (string k in m.Merged.Keys)
                    if (HasItem(MergedVerbWhitelist, k) && !HasItem(verbs, k)) verbs.Add(k);
                foreach (string verb in verbs)
                {
                    string flag = FlagFor(m, verb);
                    string pname = flag.TrimStart('-');
                    if (HasItem(AckExitedIds, m.Id) && verb.Equals("Ack", StringComparison.OrdinalIgnoreCase))
                    {
                        // [m6]:306-316 那枚反向负锚：这一路入口**必须不认识**它，谁把参数加回来这里翻红。
                        if (HasItem(entryParams, pname))
                            entryProblems.Add("ack-gate-exited｜" + m.Id + "." + verb + "｜入口又认识 -" + pname + " 了");
                        continue;
                    }
                    if (!HasItem(entryParams, pname)) entryProblems.Add("verb-flag-absent｜" + m.Id + "." + verb + "｜" + flag);
                }
            }

            List<object> moduleRows = new List<object>();
            long manifestHits = 0;
            foreach (ManifestRow m in rows) { moduleRows.Add(ManifestRowToDict(m)); manifestHits += m.Problems.Count; }
            long total = manifestHits + entryProblems.Count;
            return Gd.Out("Modules", moduleRows.ToArray(), "EntryProblems", entryProblems.ToArray(),
                "TotalProblems", total, "Passed", total == 0);
        }

        // 必填两枚的入闸：缺键与空串同档(都是"没问")，走拒跑不是判红。
        static string RequireStr(Dictionary<string, object> c, string k, string why)
        {
            if (!Given(c, k)) throw new GdRuleException("check-module-manifests：" + k + " 缺键——" + why + "，核不许自己抄一份");
            string v = Gd.Str(Gd.Get(c, k));
            if (string.IsNullOrEmpty(v)) throw new GdRuleException("check-module-manifests：" + k + " 是空串——" + why);
            return v;
        }

        // 必填两枚名册的入闸：**只判在场**，空表是合法读数(那棵树上一路都没装)，缺键才是没问。
        static List<string> RequireList(Dictionary<string, object> c, string k, string why)
        {
            if (!Given(c, k)) throw new GdRuleException("check-module-manifests：" + k + " 缺键——" + why + "，核不许自己抄一份");
            List<string> xs = OptList(c, k);
            return xs;
        }

        sealed class PsPair
        {
            public string Key = "";
            public string Value = "";
        }

        // Test-GdProcessRunning(common.ps1:474) 同形：读不到就当没在跑(Get-Process -ErrorAction SilentlyContinue)。
        static bool Running(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;    // 空名 ⇒ PS 那侧恒空，这里同形
            Process[] ps;
            try { ps = Process.GetProcessesByName(name); } catch { return false; }
            bool any = ps != null && ps.Length > 0;
            if (ps != null) foreach (Process p in ps) { try { p.Dispose(); } catch { } }
            return any;
        }

        // 直调 Round.ProcTree(②-a 那把"名曰树、实为按进程名"的尺)，只取 Matched 那一格。
        // noKill／fakeProcs 原样透传，于是 --roundtest 能钉"关了几格"而不真杀一颗进程。
        // 空名走第 6 笔那条：不回它那句 GdRuleException，直接当"匹配到 0 颗"。
        static long ProcTreeMatched(Dictionary<string, object> c, string name)
        {
            if (name.Length == 0) return 0;
            Dictionary<string, object> r = Gd.Map(Round.ProcTree(Sub(
                "name", name,
                "noKill", Gd.Get(c, "noKill"),
                "fakeProcs", Gd.Get(c, "fakeProcs"))));
            if (r == null) return 0;
            return Num(Gd.Get(r, "Matched"));
        }

        // ==================== verb: takeover-processes ====================
        // Invoke-GdTakeover(common.ps1:665，⚠ 11-05 转接刀·运行面刀⑨ 该真身已自 lib\common.ps1 出仓——HEAD 帧实拍在 :605-622，那枚 :665 本就指错段落) 逐字：空名 continue、没在跑 continue、没开接管就当场抛原话、
        // 否则 WARN 一句 → Stop-GdProcessTree → **Start-Sleep 4**(第 3 笔那条硬编码)。
        // 为什么要接管：真身那句注释说的是"MAA/M9A 只有在自己退出时才会写配置，运行中改配置会被覆盖"。
        // 回执比真身多的 Done／Stopped 只给自检与答手读(定档第 2 条)；链体那一侧要的只是 $data=$true。
        // 夹具：running 给了就按名册判在跑(否则现查真进程)；noKill＋fakeProcs 让那一步不落刀。

        internal static object Takeover(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<string> names = OptList(c, "names");
            string label = OptStr(c, "label", delegate { return "助手"; });
            int sleepSeconds = (int)OptLng(c, "sleepSeconds", 4);      // 真身硬编码 4；自检喂 0
            bool runningGiven = Given(c, "running");
            List<string> runningNames = OptList(c, "running");
            bool allow = RuntimeFlagOn(c, "takeOverAssistant");

            List<object> lines = new List<object>();
            List<object> stopped = new List<object>();
            foreach (string n in names)
            {
                if (n.Length == 0) continue;                           // 真身 `if (-not $n)`：paths 没填算出的空名
                bool alive = runningGiven ? runningNames.Contains(n) : Running(n);
                if (!alive) continue;
                if (!allow)
                    throw new GdRuleException(label + "(" + n + ") 正在运行；要么手动关掉再跑，要么把 settings.runtime.takeOverAssistant 设为 true");
                Say(lines, "WARN", label + " 正在运行 → 先关掉再改配置(避免它退出时把配置写回旧值)");
                ProcTreeMatched(c, n);                                  // 真身 [void](Stop-GdProcessTree -Name $n)：那个数不进回执
                stopped.Add(n);
                if (sleepSeconds > 0) System.Threading.Thread.Sleep(sleepSeconds * 1000);
            }
            return Gd.Out("Done", true, "Label", label, "Stopped", stopped.ToArray(), "Lines", lines.ToArray());
        }

        // ==================== verb: stop-maa-and-game ====================
        // Stop-MaaAndGame(旧 lib\maa.ps1 真身，11-03 转接刀·方舟拉起族整枚出仓；那句 :739 在摘件前就已是历史引文)：两档**分开的开关**，各自一把 Stop-GdProcessTree，只有真关掉了才吭那一句。
        // 真身把进程数只写进话术、不返出去(maa.ps1:744 那个 $n)；这里 MaaClosed／ArkClosed 只为自检与答手而多。

        internal static object StopMaaAndGame(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            Dictionary<string, string> m = MaaPathsNow(c);
            List<object> lines = new List<object>();

            long maaClosed = 0;
            if (RuntimeFlagOn(c, "quitAssistantAfter"))
            {
                maaClosed = ProcTreeMatched(c, m["Name"]);
                if (maaClosed > 0) Say(lines, "INFO", "已关闭 MAA(" + maaClosed + " 个进程)");
            }
            long arkClosed = 0;
            string arkExe = StrOf(Gd.Get(PathsOf(c), "arknightsExe"), "");
            if (RuntimeFlagOn(c, "closeArknightsAfter") && arkExe.Length > 0)
            {
                arkClosed = ProcTreeMatched(c, BaseName(arkExe));
                if (arkClosed > 0) Say(lines, "INFO", "已关闭明日方舟端游(" + arkClosed + " 个进程)");
            }
            // MaaAgentBinary(视觉/识别子进程)的收尾杀段(11-03 刀① 从 lib\ark-module.ps1 Stop 钩子随臂进核，
            // 原真身 :86)：无条件杀、逐枚 try/catch 吞错、进程数不进话术也不进 Lines —— 与真身逐字同形，
            // 不吃 quitAssistantAfter 那把开关(当年入口 finally 无条件杀，挪进收尾钩子后改判"真跑过方舟"，现由调用方定时机)。
            foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName("MaaAgentBinary"))
            {
                try { p.Kill(); } catch { }
            }
            return Gd.Out("Done", true, "MaaClosed", maaClosed, "ArkClosed", arkClosed, "Lines", lines.ToArray());
        }

        // ==================== verb: stop-m9a ====================
        // Stop-M9A(旧 m9a.ps1:289；⚠ 11-06 刀⑫ 笔(a) 该 PS 壳整枚出仓，坐标按历史引文读、不换算——本件 StopM9A 是唯一事实源)：**只有一档** quitAssistantAfter，话术是"M9A"不是"MAA"
        // (两侧话术不许统一，②-a 那条纪律在这儿同样成立)。

        internal static object StopM9A(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            long closed = 0;
            if (RuntimeFlagOn(c, "quitAssistantAfter"))
            {
                closed = ProcTreeMatched(c, M9ANameNow(c));
                if (closed > 0) Say(lines, "INFO", "已关闭 M9A(" + closed + " 个进程)");
            }
            return Gd.Out("Done", true, "M9aClosed", closed, "Lines", lines.ToArray());
        }

        // ==================== verb: read-maa-fight-skip ====================
        // Get-GdMaaFightSkip(maa.ps1:60 起，⚠ 11-05 转接刀·运行面刀⑨ 那枚壳已自 lib\maa.ps1 出仓，坐标按历史引文读)的现场半段：gui.log **全量**(真身 -Offset 0 就是这个意思)、
        // 文件不在或读到空 ⇒ Found=false(答手据此发 JVal::Null，定档第 3 条)；否则整段文本递给已在场的
        // detect-maa-fight-skip，回执四键原样摆出来。判据("最后一次 GUI started 之后""关卡计划内没有当日开放的关卡"
        // ""任务跳过"计数、以及加载期修剪成空计划那第二档)一枚都不在这儿重写。

        internal static object ReadFightSkip(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            Dictionary<string, string> m = MaaPathsNow(c);
            string path = m["GuiLog"];
            if (!GdEnv.PathReady(path)) return Gd.Out("Found", false, "Reason", "gui-log-missing");
            Tail t = ReadTail(path, 0);
            if (t.Text.Length == 0) return Gd.Out("Found", false, "Reason", "gui-log-empty");
            Dictionary<string, object> d = Gd.Map(Orchestration.MaaFightSkip(Sub("text", t.Text)));
            if (d == null) return Gd.Out("Found", false, "Reason", "rule-empty");
            return Gd.Out("Found", true,
                "Skipped", d["Skipped"], "Plan", d["Plan"], "TaskSkips", d["TaskSkips"], "Evidence", d["Evidence"]);
        }

        // ==================== verb: read-maa-stage-observation ====================
        // Get-GdMaaStageObservation(maa.ps1:83 起，⚠ 11-05 转接刀·运行面刀⑨ 那枚壳已自 lib\maa.ps1 出仓，坐标按历史引文读)的现场半段：asst.log 只取**尾 16MB**(真身那句注释：
        // asst.log 连日不轮转能涨到几十 MB，整份喂 CLI 不划算)、gui.log 全量、日期由壳侧注入(nowText 夹具)；
        // 两本都读不到才算 Found=false(旧身返 $null 那一档)；观测七键全由 observe-maa-stage-open 判，这里不加 if。
        // LogsRead 那一格见定档第 3 条：把"两本日志都没读到"与"读到了但本轮没递 Fight 任务"分开。

        const long AsstTailWindow = 16777216;      // maa.ps1 那个 16MB 窗口，逐字

        internal static object ReadStageObservation(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            Dictionary<string, string> m = MaaPathsNow(c);

            string asstText = "";
            if (GdEnv.PathReady(m["AsstLog"]))
            {
                long len = 0;
                try { len = new FileInfo(m["AsstLog"]).Length; } catch { len = 0; }
                asstText = ReadTail(m["AsstLog"], Math.Max(0, len - AsstTailWindow)).Text;
            }
            string guiText = "";
            if (GdEnv.PathReady(m["GuiLog"])) guiText = ReadTail(m["GuiLog"], 0).Text;
            if (asstText.Length == 0 && guiText.Length == 0)
                return Gd.Out("Found", false, "LogsRead", false, "Reason", "no-logs");

            Dictionary<string, object> d = Gd.Map(Orchestration.MaaStageObserve(Sub(
                "asstText", asstText, "guiText", guiText,
                "date", OptStr(c, "nowText", delegate { return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }))));
            if (d == null) return Gd.Out("Found", false, "LogsRead", true, "Reason", "rule-empty");
            return Gd.Out("Found", d["Found"], "LogsRead", true, "Date", d["Date"], "Stage", d["Stage"],
                "LastBattle", d["LastBattle"], "SawGuiPlan", d["SawGuiPlan"], "Survivors", d["Survivors"],
                "Selected", d["Selected"]);
        }

        // ==================================================================
        // 第二笔的公共件：原生命令、窗口名册、等到某个条件
        // ==================================================================

        // Invoke-GdNative(common.ps1:493) 的同形：{Code;Out;Err} 三格，超时那一档返 -1 与那句 timeout。
        // 夹具 `nativeRuns` 是一个数组，每项 {Code;Out;Err}：按序消费、取完**重复最后一枚**(于是
        // Get-GdMuMuState→launch→再 Get-GdMuMuState 那三步的顺序可以直接钉死，不必铺无穷多份夹具)。
        // 没喂夹具就走现场真起 —— 与 Running() 那枚"给了按名册、没给查真进程"同一档规矩。
        sealed class Native
        {
            public int Code = 0;
            public string Out = "";
            public string Err = "";
        }

        internal static int NativeCallsUsed;      // 只给 --roundtest 钉顺序用，不进任何 verb 的回执
        static int nativeCursor;

        internal static void ResetNativeCursor() { nativeCursor = 0; NativeCallsUsed = 0; }

        static Native RunNative(Dictionary<string, object> c, string file, List<string> args, int timeoutSeconds)
        {
            Native n = new Native();
            object seq = Gd.Get(c, "nativeRuns");
            object[] runs = Gd.List(seq);
            if (Given(c, "nativeRuns") && runs.Length > 0)
            {
                int idx = nativeCursor < runs.Length ? nativeCursor : runs.Length - 1;
                nativeCursor++;
                NativeCallsUsed++;
                Dictionary<string, object> r = Gd.Map(runs[idx]);
                if (r == null) return n;
                n.Code = (int)Num(Gd.Get(r, "Code"));
                n.Out = StrOf(Gd.Get(r, "Out"), "");
                n.Err = StrOf(Gd.Get(r, "Err"), "");
                return n;
            }
            // 现场档：真身那一句"命令不存在"是终止错误，这里同档(GdRuleException ⇒ exit 2，原话给壳继续 throw)。
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
                throw new GdRuleException("命令不存在：" + file);
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = file;
            psi.Arguments = string.Join(" ", args.ToArray());        // 原样怪癖第 9 笔：不加引号
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            try { psi.WorkingDirectory = Path.GetDirectoryName(file); } catch { psi.WorkingDirectory = ""; }
            using (Process p = Process.Start(psi))
            {
                NativeCallsUsed++;
                Task<string> outTask = p.StandardOutput.ReadToEndAsync();
                Task<string> errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutSeconds * 1000))
                {
                    try { p.Kill(); } catch { }
                    n.Code = -1; n.Out = ""; n.Err = "timeout after " + timeoutSeconds + "s";
                    return n;
                }
                n.Code = p.ExitCode;
                try { n.Out = outTask.Result ?? ""; } catch { n.Out = ""; }
                try { n.Err = errTask.Result ?? ""; } catch { n.Err = ""; }
                return n;
            }
        }

        // Get-GdWindowByTitle(common.ps1:1012，⚠ 11-05 转接刀·运行面刀⑨ 该真身已自 lib\common.ps1 出仓，坐标按历史引文读) 的同形：只看有主窗口的进程，给了进程名就按名过滤(不分大小写)，
        // 标题走 -match(也不分大小写)，命中回第一枚 {Name;Id;Title}。整段在逐颗进程的 try/catch 里
        // —— 坏正则／取不到的窗口在真身就是被静默吞掉，这里同形(不抛)。
        // 夹具 `windows` = [{Name;Id;Title}]；给了就只按名册判(--roundtest 一律走这一路，不碰真窗口)。
        static Dictionary<string, object> FindByTitle(Dictionary<string, object> c, string pattern, string procName)
        {
            object[] rows = Gd.List(Gd.Get(c, "windows"));
            if (Given(c, "windows"))
            {
                foreach (object o in rows)
                {
                    Dictionary<string, object> r = Gd.Map(o);
                    if (r == null) continue;
                    string nm = StrOf(Gd.Get(r, "Name"), "");
                    if (procName.Length > 0 && !nm.Equals(procName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!TitleMatches(pattern, StrOf(Gd.Get(r, "Title"), ""))) continue;
                    Dictionary<string, object> hit = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    hit["Name"] = nm;
                    hit["Id"] = Num(Gd.Get(r, "Id"));
                    hit["Title"] = StrOf(Gd.Get(r, "Title"), "");
                    return hit;
                }
                return null;
            }
            Process[] ps;
            try { ps = Process.GetProcesses(); } catch { return null; }
            foreach (Process p in ps)
            {
                try
                {
                    if (p.MainWindowHandle.ToInt64() == 0) continue;
                    string nm = p.ProcessName ?? "";
                    if (procName.Length > 0 && !nm.Equals(procName, StringComparison.OrdinalIgnoreCase)) continue;
                    string title = p.MainWindowTitle ?? "";
                    if (!TitleMatches(pattern, title)) continue;
                    Dictionary<string, object> hit = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    hit["Name"] = nm;
                    hit["Id"] = (long)p.Id;
                    hit["Title"] = title;
                    return hit;
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
            return null;
        }

        static bool TitleMatches(string pattern, string title)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            try { return Regex.IsMatch(title ?? "", pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5)); }
            catch { return false; }        // 坏正则：真身那一下也被逐颗进程的 catch 吞掉
        }

        // Wait-GdCondition(common.ps1:979；⚠ 刀⑰ 笔(b) 该真身已自 lib\common.ps1 出仓，坐标按历史引文读) 逐字：先判再睡，睡在 return 之前**无条件**执行
        // (原样怪癖第 10 笔：超时那一档最后一次判完还多睡一轮)。
        static bool WaitUntil(Dictionary<string, object> c, Func<bool> test, int timeoutSeconds,
                              int pollSeconds, string what, List<object> lines)
        {
            DateTime deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            while (DateTime.Now < deadline)
            {
                bool ok = false;
                try { ok = test(); } catch { ok = false; }
                if (ok) return true;
                if (pollSeconds > 0) System.Threading.Thread.Sleep(pollSeconds * 1000);
            }
            Say(lines, "WARN", "等待超时(" + timeoutSeconds + "s)：" + what);
            return false;
        }

        // verb: wait-adb-game-pid —— 刀⑰ 笔(b)(2026-11-07)：Wait-GdCondition(common.ps1 真身，随批出仓)在
        // 1999-event-audit:114 那唯一活调用点的**单用途形**：test = 进程内问一枚 Live.AdbGamePid 的 Found，
        // 睡与判用的都是上面 WaitUntil 那把尺(先判再睡、超时 WARN 原话、异常吞成 false，一条规则都不新写)。
        // 通用 wait-condition{verb,body,field} 形已在设计轮拒掉(需分派链内部重入＝形状变过大，见 k45a 契约格)。
        // body：timeoutSeconds/pollSeconds(缺省 5)/what(缺省 '1999 客户端进程')；
        // adb/serial/package 与夹具旋钮 noSpawn/fakeOut **给了才透传**——缺省时落到 Live.AdbGamePid 自己的现场读，
        // 与 PS 那侧"每轮现问端点"同语义(这里不预读，避免给空串把现场读档顶成实路径)。
        // 回执 {Met, Lines}；Lines 进 GdLog 即落日志(verb 子进程的 stdout／信封)，PS 壳改指后不回放
        // (mumu 批定谳沿用，避免双份日志)。
        internal static object WaitAdbGamePid(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            int timeoutSeconds = (int)OptLng(c, "timeoutSeconds", 0);
            int pollSeconds = (int)OptLng(c, "pollSeconds", 5);
            string what = OptStr(c, "what", delegate { return "1999 客户端进程"; });
            Dictionary<string, object> inner = new Dictionary<string, object>();
            string[] pass = new string[] { "adb", "serial", "package", "noSpawn", "fakeOut" };
            for (int i = 0; i < pass.Length; i++)
            {
                object v;
                if (c.TryGetValue(pass[i], out v) && v != null) inner[pass[i]] = v;
            }
            List<object> lines = new List<object>();
            bool met = WaitUntil(c, delegate
            {
                Dictionary<string, object> d = Live.AdbGamePid(inner) as Dictionary<string, object>;
                return d != null && Gd.Truthy(Gd.Get(d, "Found"));
            }, timeoutSeconds, pollSeconds, what, lines);
            return Gd.Out("Met", met, "Lines", lines.ToArray());
        }

        static int RuntimeInt(Dictionary<string, object> c, string key, object fixture, int def)
        {
            if (fixture != null) return (int)Num(fixture);
            return (int)Num(Gd.Get(Gd.Get(SettingsOf(c), "runtime"), key));
        }

        // ==================== 启动器更新页那一段(旧 Wait-GdArkLauncherReady 真身，11-03 转接刀·方舟拉起族整枚出仓；maa.ps1:622 按历史引文读) ====================
        // 分工逐字照真身那句注释："判据(五档相位、话术、点击预算、千分比→客户区的换算、越界拒绝)全在 C# 的
        // check-ark-launcher；这里只做活 —— 循环、睡眠、真点击、落日志"。本件做的还是那四样活，
        // 只是"这里"从 PS 换成了 Chain.cs。相位一条都不在这儿判。
        // 语义是**止损不是新闸门**(真身头注)：问不动／判不出／开关关掉／dry-run 全返真，
        // 只有"到上限还没进游戏"和"窗口没了"返假。
        // 开关用 Round.FlagEnabled(`-eq $false` 才关 ⇒ 缺键算开)，与本文件 takeover 那一路的真值判**故意不同**：
        // 真身这里写的就是 `$S.runtime.arkAutoUpdateClick -eq $false`，那是 §11.50 钉过的 `-ne $false` 一族。
        //
        // 夹具：launcherAsks(按**问模型次数**按序消费、取完重复最后一枚，每项整块喂 check-ark-launcher)／shot／handle／path
        // 透传；waitSeconds 顶掉 arkUpdateWaitSeconds、clickBudget 顶掉 arkLauncherMaxChecks、
        // sleepSeconds 顶掉那两枚硬编码的 30/60(自检喂 0)；arkAutoUpdateClick／dryRun 走夹具。
        // 为什么按"问模型次数"而不是"点了几次"取序列(10-14 落自检时改的口)：真身里 clicks 只在 needs-click
        // 那一档自增，而自检不许碰输入注入(needs-click 两档已整段记 unobserved) —— 按 clicks 取就永远推不动
        // [waiting, clear] 这类序列，只会反复吃第一枚。每问一次模型取一项，与"问一次 ⇒ 判一相"的事实源同构；
        // clicks 自己那把尺(点击预算、含原样怪癖第 8 笔"没点实也算点过")一字未动。

        static Dictionary<string, object> LauncherAskBody(Dictionary<string, object> c, int askIdx, int clicks, int maxClicks)
        {
            object[] seq = Gd.List(Gd.Get(c, "launcherAsks"));
            Dictionary<string, object> body;
            if (Given(c, "launcherAsks") && seq.Length > 0)
            {
                int idx = askIdx < seq.Length ? askIdx : seq.Length - 1;
                Dictionary<string, object> inj = Gd.Map(seq[idx]);
                body = inj == null ? new Dictionary<string, object>() : Gd.Ci(inj);
            }
            else body = View(c);          // 现场／半夹具档：整块透传，shot 那类注入键就靠这一步下去
            body["clicksAlready"] = clicks;
            body["maxClicks"] = maxClicks;
            return body;
        }

        // 可见性：internal 是给 --roundtest **直调**留的口(②-b2)。为什么非得直调不可：经 `start-ark-client`
        // 那一路它其实**走不到** —— 窗口名册喂的是静态夹具，命中就在"端游窗口已存在"处早退，不命中就
        // 一直撞同一份静态名册直到 WaitUntil 超时，两条路都不进相位循环。而 Chain.cs 是本批新件里的私有助手，
        // 不是"既有 verb"，改它的可见性不触动 §11.50 ②-1 那条入参/回执形状的判据。
        internal static bool LauncherReady(Dictionary<string, object> c, List<object> lines)
        {
            object switchV = Gd.Get(c, "arkAutoUpdateClick");
            if (switchV == null) switchV = Gd.Get(Gd.Get(SettingsOf(c), "runtime"), "arkAutoUpdateClick");
            if (!Round.FlagEnabled(switchV)) return true;                                   // 明写关掉 ⇒ 回到 10-11 之前的旧行为
            if (OptBool(c, "dryRun")) { Say(lines, "STEP", "[dry-run] 跳过启动器更新页检查"); return true; }

            int waitSec = RuntimeInt(c, "arkUpdateWaitSeconds", Gd.Get(c, "waitSeconds"), 0);
            if (waitSec <= 0) waitSec = 2400;
            int maxClicks = RuntimeInt(c, "arkLauncherMaxChecks", Gd.Get(c, "clickBudget"), 0);
            if (maxClicks <= 0) maxClicks = 6;                                              // 两边都不许让配置一坏就变成"一次都不许点"的死闸
            int sleepSeconds = (int)OptLng(c, "sleepSeconds", -1);
            if (sleepSeconds < 0) sleepSeconds = -1;          // -1 = 用真身那两枚硬编码(30／60)
            DateTime deadline = DateTime.Now.AddSeconds(waitSec);
            int clicks = 0;
            int checks = 0;

            while (DateTime.Now < deadline)
            {
                Dictionary<string, object> d;
                try
                {
                    d = Gd.Map(Live.ArkLauncherCheck(LauncherAskBody(c, checks, clicks, maxClicks)));
                }
                catch (Exception ex)
                {
                    Say(lines, "WARN", "启动器检查：这一枚问不动(" + ex.Message + ")，分不清是不是更新页，按原样放行");
                    return true;
                }
                if (d == null)
                {
                    Say(lines, "WARN", "启动器检查：这一枚问不动(没有回执)，分不清是不是更新页，按原样放行");
                    return true;
                }
                checks++;
                string line = StrOf(Gd.Get(d, "LogLine"), "");
                if (line.Length > 0)
                {
                    string lvl = StrOf(Gd.Get(d, "Level"), "");
                    Say(lines, lvl.Length > 0 && !lvl.Equals("INFO", StringComparison.OrdinalIgnoreCase) ? lvl : "", line);
                }
                string phase = StrOf(Gd.Get(d, "Phase"), "");
                if (phase == "clear") return true;
                if (phase == "no-window") return false;             // 窗口都没了，检查没有对象(等下去也不会自己长回来)
                if (phase == "uncertain") return true;              // 判不出 = 交回原路，不拦
                if (phase == "needs-click")
                {
                    Dictionary<string, object> cl;
                    try
                    {
                        cl = Gd.Map(Live.WindowClick(Sub(
                            "handle", Gd.Get(d, "Handle"),
                            "clientX", Gd.Get(d, "ClientX"),
                            "clientY", Gd.Get(d, "ClientY"),
                            "name", "启动器窗口",
                            "what", Gd.Get(d, "What"),
                            "why", Gd.Get(d, "Why"))));
                    }
                    catch (Exception ex)
                    {
                        Say(lines, "WARN", "启动器检查：点击没发出去(" + ex.Message + ")，按原样放行");
                        return true;
                    }
                    clicks++;                                       // 原样怪癖第 8 笔：没点实也算点过
                    bool sent = cl != null && Gd.Truthy(Gd.Get(cl, "Sent"));
                    string mode = cl == null ? "" : StrOf(Gd.Get(cl, "Mode"), "");
                    if (sent) Say(lines, "STEP", "启动器检查：已点下去(" + mode + ")，等它开始拉资源");
                    else Say(lines, "WARN", "启动器检查：这一下没点实(" + mode + ")，再看看");
                    SleepFor(sleepSeconds, 30);
                    continue;
                }
                if (phase == "waiting") { SleepFor(sleepSeconds, 60); continue; }
                Say(lines, "WARN", "启动器检查：认不出这一档相位([" + phase + "])，按原样放行");
                return true;
            }
            Say(lines, "WARN", "启动器检查：等了 " + waitSec + " 秒(问模型 " + checks + " 次、点过 " + clicks + " 次)还没进游戏，这一轮到此为止");
            return false;
        }

        // 那两枚硬编码睡眠：夹具给 0 就一步不睡(自检)，没给按真身的 30／60。
        static void SleepFor(int given, int real)
        {
            int s = given < 0 ? real : given;
            if (s > 0) System.Threading.Thread.Sleep(s * 1000);
        }

        // ==================== verb: start-ark-client ====================
        // Start-GdArkClient(旧 maa.ps1:702 真身，11-03 转接刀·方舟拉起族整枚出仓)的整枚直答：路径闸 → 已有窗口就吭一声 → 起进程 → 等窗口 → 暖身 →
        // 更新页那一段按画面确认。回执比真身的 [bool] 多 Done／Window／Lines(定档第 2 条)，
        // 链体那一侧要的仍是 [bool](答手取 Started)。

        internal static object StartArkClient(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            string exe = StrOf(Gd.Get(PathsOf(c), "arknightsExe"), "");
            object exeFixture = Gd.Get(c, "arknightsExe");
            if (exeFixture != null) exe = Gd.Str(exeFixture);
            if (exe.Length == 0 || !File.Exists(exe))
                throw new GdRuleException("端游路径不对：" + exe + "(改 settings.paths.arknightsExe)");
            string name = BaseName(exe);
            string pattern = StrOf(Gd.Get(Gd.Get(SettingsOf(c), "ark"), "windowTitlePattern"), "");
            object pf = Gd.Get(c, "windowTitlePattern");
            if (pf != null) pattern = Gd.Str(pf);
            if (pattern.Length == 0) pattern = "明日方舟|Arknights";

            Dictionary<string, object> win = FindByTitle(c, pattern, name);
            if (win != null)
            {
                Say(lines, "", "端游窗口已存在：[" + StrOf(win["Name"], "") + "#" + Num(win["Id"]) + "] " + StrOf(win["Title"], ""));
                return Gd.Out("Done", true, "Started", true, "Window", win, "Lines", lines.ToArray());
            }

            bool runningGiven = Given(c, "running");
            List<string> runningNames = OptList(c, "running");
            bool alive = runningGiven ? runningNames.Contains(name) : Running(name);
            if (alive)
            {
                Say(lines, "WARN", "端游进程在跑但还没有窗口(" + name + ")，等它出登录界面");
            }
            else
            {
                Say(lines, "STEP", "启动端游：" + exe);
                if (OptBool(c, "dryRun"))
                {
                    Say(lines, "STEP", "[dry-run] 跳过实际启动");
                    return Gd.Out("Done", true, "Started", false, "Window", null, "Lines", lines.ToArray());
                }
                StartWholeProcess(exe);
            }

            int timeout = RuntimeInt(c, "arkWindowTimeoutSeconds", Gd.Get(c, "windowTimeoutSeconds"), 0);
            int poll = (int)OptLng(c, "pollSeconds", 5);
            Dictionary<string, object> found = null;
            bool got = WaitUntil(c, delegate { found = FindByTitle(c, pattern, name); return found != null; },
                                 timeout, poll, "端游窗口", lines);
            if (got)
            {
                Dictionary<string, object> w2 = FindByTitle(c, pattern, name);
                if (w2 != null) found = w2;
                Say(lines, "OK", "端游窗口就绪：[" + StrOf(found["Name"], "") + "#" + Num(found["Id"]) + "] " + StrOf(found["Title"], ""));
                int warmup = RuntimeInt(c, "arkWarmupSeconds", Gd.Get(c, "warmupSeconds"), 0);
                if (warmup > 0) System.Threading.Thread.Sleep(warmup * 1000);
                bool ready = LauncherReady(c, lines);
                return Gd.Out("Done", true, "Started", ready, "Window", found, "Lines", lines.ToArray());
            }
            Say(lines, "WARN", "等了 " + timeout + " 秒还没看到端游窗口(要登录/在更新/路径不对?)");
            return Gd.Out("Done", true, "Started", false, "Window", null, "Lines", lines.ToArray());
        }

        // Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) 的同形。
        // .NET FW 的 UseShellExecute 默认就是 true ⇒ 与 Start-Process 同一档语义(不给流、不开窗)。
        static void StartWholeProcess(string exe)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.UseShellExecute = true;
            try { psi.WorkingDirectory = Path.GetDirectoryName(exe); } catch { psi.WorkingDirectory = ""; }
            try { Process.Start(psi); } catch (Exception ex) { throw new GdRuleException("启动端游失败：" + ex.Message); }
        }

        // ==================== verb: start-mumu ====================
        // Start-GdMuMu(mumu.ps1:38) 整枚直答：info 读数(递给已在场的 parse-mumu-state)→ 已在运行就吭一声 →
        // control launch → 轮询到 AndroidStarted。JSON 解读那一段一枚判据都不在这儿(批次 3 的 Processes.cs)。
        // 夹具 nativeRuns 的"取完重复最后一枚"正好钉住 info→launch→info 那三步的顺序与 NativeCallsUsed 计数。

        // Get-GdMuMu(mumu.ps1:11) 的现场读：四格里这一份只用 Manager 与 Index(Name 只进话册、Adb 不归这枚)。
        static Dictionary<string, string> MumuNow(Dictionary<string, object> c)
        {
            Dictionary<string, object> p = PathsOf(c);
            Dictionary<string, object> m = Gd.Map(Gd.Get(SettingsOf(c), "mumu")) ?? new Dictionary<string, object>();
            Dictionary<string, string> r = new Dictionary<string, string>();
            r["Manager"] = OptStr(c, "manager", delegate
            { return GdEnv.JoinPath(StrOf(Gd.Get(p, "mumuDir"), ""), new string[] { StrOf(Gd.Get(p, "mumuManager"), "") }); });
            r["Index"] = OptStr(c, "vmIndex", delegate { return Gd.Str(Gd.Get(m, "vmIndex")); });
            r["Name"] = OptStr(c, "vmNameHint", delegate { return Gd.Str(Gd.Get(m, "vmNameHint")); });
            return r;
        }

        // Get-GdMuMuState(mumu.ps1:21) 的同形：目录没填那一档**不喂空串给 RunNative**(真身那条注释说的是
        // "只读体检里看到的就是一句内部报错")，而是回那一句专门的话；Available=false 时 Name 那一格真身没有，
        // 这里补空串(链体只读 $($st.Name) 进话术，空串就是空串)。
        static Dictionary<string, object> MuMuStateNow(Dictionary<string, object> c)
        {
            Dictionary<string, string> m = MumuNow(c);
            if (!GdEnv.PathReady(m["Manager"]))
                return Gd.Out("Available", false, "Raw", "MuMu 目录还没填(先跑 setup.ps1)",
                    "AndroidStarted", false, "ProcessStarted", false, "Name", "");
            Native n = RunNative(c, m["Manager"], new List<string> { "info", "-v", m["Index"] }, 40);
            Dictionary<string, object> d = Gd.Map(Processes.MumuState(Sub("code", n.Code, "out", n.Out, "err", n.Err)));
            if (d == null)
                return Gd.Out("Available", false, "Raw", "", "AndroidStarted", false, "ProcessStarted", false, "Name", "");
            d["Name"] = StrOf(Gd.Get(d, "Name"), "");
            return d;
        }

        internal static object StartMuMu(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            Dictionary<string, string> m = MumuNow(c);
            Dictionary<string, object> st = MuMuStateNow(c);
            if (!Gd.Truthy(Gd.Get(st, "Available")))
                throw new GdRuleException("MuMuManager 无法读取虚拟机信息(" + m["Index"] + ")：" + StrOf(Gd.Get(st, "Raw"), ""));
            if (Gd.Truthy(Gd.Get(st, "AndroidStarted")))
            {
                Say(lines, "", "MuMu 虚拟机 " + m["Index"] + "(" + StrOf(Gd.Get(st, "Name"), "") + ")已在运行");
                return Gd.Out("Done", true, "Started", true, "Name", StrOf(Gd.Get(st, "Name"), ""), "Lines", lines.ToArray());
            }
            Say(lines, "STEP", "启动 MuMu 虚拟机 " + m["Index"] + "(" + StrOf(Gd.Get(st, "Name"), "") + ")");
            if (OptBool(c, "dryRun"))
            {
                Say(lines, "STEP", "[dry-run] 跳过实际启动");
                return Gd.Out("Done", true, "Started", false, "Name", StrOf(Gd.Get(st, "Name"), ""), "Lines", lines.ToArray());
            }
            Native r = RunNative(c, m["Manager"],
                new List<string> { "control", "-v", m["Index"], "launch" }, 120);
            if (r.Code != 0) Say(lines, "WARN", "launch 返回码 " + r.Code + "：" + r.Err + r.Out);
            int timeout = RuntimeInt(c, "mumuBootTimeoutSeconds", Gd.Get(c, "bootTimeoutSeconds"), 0);
            int poll = (int)OptLng(c, "pollSeconds", 6);
            bool got = WaitUntil(c, delegate { return Gd.Truthy(Gd.Get(MuMuStateNow(c), "AndroidStarted")); },
                                 timeout, poll, "MuMu 安卓系统启动", lines);
            return Gd.Out("Done", true, "Started", got, "Name", StrOf(Gd.Get(st, "Name"), ""), "Lines", lines.ToArray());
        }

        // ==================== verb: stop-mumu ====================
        // Stop-GdMuMu(mumu.ps1:52，11-03 刀① 从 lib\r1999-module.ps1 Stop 钩子随臂进核)的同形：
        // 目录没填那一档 MuMuStateNow 回 Available=false 对象(不抛，与真身 Get-GdMuMuState :26-28 同形)
        // ⇒ 落"未在运行"档；两枚 Started 都没起也落这一档，只回一句 INFO 就完(真身 :56 同句)；
        // 否则 STEP 一句 + control shutdown。真身 [void](Invoke-GdNative ...) 不看退出码 —— 这里同样不判 r.Code。

        internal static object StopMuMu(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            Dictionary<string, string> m = MumuNow(c);
            Dictionary<string, object> st = MuMuStateNow(c);
            if (!Gd.Truthy(Gd.Get(st, "AndroidStarted")) && !Gd.Truthy(Gd.Get(st, "ProcessStarted")))
            {
                Say(lines, "", "MuMu 未在运行，无需关闭");
                return Gd.Out("Done", true, "Lines", lines.ToArray());
            }
            Say(lines, "STEP", "关闭 MuMu 虚拟机 " + m["Index"]);
            RunNative(c, m["Manager"], new List<string> { "control", "-v", m["Index"], "shutdown" }, 120);
            return Gd.Out("Done", true, "Lines", lines.ToArray());
        }

        // ==================== verb: mumu-state ====================
        // Get-GdMuMuState(mumu.ps1:21，11-03 刀② 从 round-preview:r1999 臂进核)的整枚直答：
        // 判据一枚不在这儿重写 —— MuMuStateNow(上几行)就是那具同形(目录没填回 Available=false 对象不抛，
        // info 读数递给已在场的 parse-mumu-state)，round 臂只读 AndroidStarted 进话术。
        // 回执比真身多一枚 Index(MumuNow 同源现读)：真身那头 "$($S.mumu.vmIndex)" 的号牌在核内直答臂拼，
        // 而 ctx 里没有 mumu 段 ⇒ 号牌只能由回执带来 —— 与 stop-mumu 那笔「回执比真身多 Lines」同一个先例形。

        internal static object MuMuState(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            Dictionary<string, object> d = MuMuStateNow(c);
            d["Index"] = MumuNow(c)["Index"];
            return d;
        }

        // ==================== verb: read-m9a-connection ====================
        // Get-M9AConnection(m9a.ps1:105，11-03 刀② 从 round-preview:r1999 臂进核)的同形：
        // 实例 json 的 AdbDevice 只点名取三格(AdbPath／AdbSerial／Name)；adb 空或不在盘上回落 MuMu 那把
        // (真身 `(Get-GdMuMu -S).Adb` = Join-GdPath mumuDir mumuAdb，落点本身不 Test-Path，照抄)；
        // 没 serial 抛同字面。json 读不动/文件不在由 GdJson.Read 自己抛(FileNotFoundException，真身
        // Read-GdJson 同档 terminating error)，这里不替它补存在性闸 —— 真身没有的闸不许加。
        // instance 入参形状照 apply-m9a-instance 那枚(Id/Name/Path)：apply 那枚不读 Path(它读 appsettings)，
        // 探活读的是实例自己的 json —— 两份文件不是一份，这就是它得单独立一枚出口的理由。

        internal static object ReadM9AConnection(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            Dictionary<string, object> inst = Gd.Map(Gd.Get(c, "instance")) ?? new Dictionary<string, object>();
            string name = StrOf(Gd.Get(inst, "Name"), "");
            string path = StrOf(Gd.Get(inst, "Path"), "");
            object cfg = Gd.Get(c, "config");
            if (cfg == null) cfg = GdJson.Read(path);
            Dictionary<string, object> dev = Gd.Map(Gd.Get(cfg, "AdbDevice"));
            string serial = dev == null ? "" : StrOf(Gd.Get(dev, "AdbSerial"), "");
            if (dev == null || serial.Length == 0)
                throw new GdRuleException("实例[" + name + "]里没有 AdbDevice.AdbSerial，无法探活");
            string adb = StrOf(Gd.Get(dev, "AdbPath"), "");
            if (adb.Length == 0 || !File.Exists(adb))
            {
                Dictionary<string, object> p = PathsOf(c);
                adb = GdEnv.JoinPath(StrOf(Gd.Get(p, "mumuDir"), ""), new string[] { StrOf(Gd.Get(p, "mumuAdb"), "") });
            }
            return Gd.Out("Adb", adb, "Serial", serial, "Name", StrOf(Gd.Get(dev, "Name"), ""));
        }

        // ==================== verb: apply-maa-daily-config ====================
        // Set-MaaDailyConfig(maa.ps1:116，11-03 拉起族摘件后现读、原写 115) 的现场半段：在跑判据／配置在不在／读盘／把 15 枚平铺值递给
        // plan-maa-queue(规则段整段住 Processes.cs，批次 3 已冻结)／按序回放 Logs／备份—写盘—逐队列回读比 IsEnable。
        // farm 那四格从 decision.Farm 抠出来(farmPlan 不在 ⇒ ''/0/0/0/Active=false)，verb 不认 Decision 的形状 ——
        // 这句是 maa.ps1:136(11-03 拉起族摘件后现读、原写 135)那段注释的原样兑现。

        static Dictionary<string, object> StageTableNow(Dictionary<string, object> c, List<object> lines)
        {
            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "table"));
            if (given != null) return given;
            if (stageTableCache != null) return stageTableCache;
            Dictionary<string, object> t = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string path = GdEnv.JoinPath(StrOf(Gd.Get(PathsOf(c), "maaDir"), ""),
                new string[] { "resource", "stages.json" });
            if (GdEnv.PathReady(path))
            {
                try
                {
                    object j = GdJson.Read(path);
                    List<object> items = new List<object>();
                    object[] arr = Gd.List(j);
                    if (j != null && arr.Length > 0 && Gd.Map(j) == null) items.AddRange(arr);   // 新版 MAA 是数组
                    if (items.Count == 0 && Gd.Map(j) != null)
                        foreach (KeyValuePair<string, object> kv in Gd.Map(j)) items.Add(kv.Value);  // 老版本是对象
                    foreach (object e in items)
                    {
                        string code = StrOf(Gd.Get(e, "code"), "");
                        if (code.Length == 0 || code == "System.Object[]") continue;
                        t[code] = Gd.Int(Gd.Get(e, "apCost"), 0);
                    }
                }
                catch (Exception ex) { Say(lines, "WARN", "stages.json 解析失败：" + ex.Message); }
            }
            if (t.Count == 0) Say(lines, "WARN", "未能建立 MAA 关卡表，理智计划不会被校验");
            stageTableCache = t;
            return t;
        }

        static Dictionary<string, object> stageTableCache;

        internal static object ApplyMaaDailyConfig(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            Dictionary<string, bool> flags = ApplyFlags(c);
            Dictionary<string, object> dec = Gd.Map(Gd.Get(c, "decision")) ?? new Dictionary<string, object>();
            Dictionary<string, string> m = MaaPathsNow(c);
            bool runningGiven = Given(c, "running");
            List<string> runningNames = OptList(c, "running");
            bool alive = runningGiven ? runningNames.Contains(m["Name"]) : Running(m["Name"]);
            if (alive)
            {
                if (flags["Apply"]) throw new GdRuleException("MAA 正在运行，请先关闭再改配置");
                Say(lines, "WARN", "MAA 正在运行：预览不受影响，真正跑时会先接管关闭(残留的高权限 MAA 需要由提权那一轮来收)");
            }
            if (!File.Exists(m["Config"])) throw new GdRuleException("MAA 配置不存在：" + m["Config"]);

            object cfg = Gd.Get(c, "config");
            if (cfg == null) cfg = GdJson.Read(m["Config"]);

            Dictionary<string, object> farmPlan = Gd.Map(Gd.Get(dec, "Farm"));
            Dictionary<string, object> body = Sub(
                "config", cfg,
                "wantFinish", StrOf(Gd.Get(Gd.Get(SettingsOf(c), "ark"), "finishAllSanityWith"), ""),
                "alsoEnableMainFight", Gd.Get(Gd.Get(SettingsOf(c), "ark"), "alsoEnableMainFight"),
                "launchArkFromHelper", Gd.Get(Gd.Get(SettingsOf(c), "runtime"), "launchArkFromHelper"),
                "keepOn", (object)QueueList(c, "keepOn", "keepQueuesEnabled").ToArray(),
                "turnOff", (object)QueueList(c, "turnOff", "disableQueues").ToArray(),
                "table", StageTableNow(c, lines),
                "mode", StrOf(Gd.Get(dec, "Mode"), ""),
                "clearSanity", Gd.Get(dec, "ClearSanity"),
                "stage", StrOf(Gd.Get(dec, "Stage"), ""),
                "farmActive", farmPlan != null && Gd.Truthy(Gd.Get(farmPlan, "Active")),
                "farmMode", StrOf(Gd.Get(dec, "FarmMode"), ""),
                "farmItemId", farmPlan == null ? "" : StrOf(Gd.Get(farmPlan, "ItemId"), ""),
                "farmGap", farmPlan == null ? 0 : Gd.Int(Gd.Get(farmPlan, "Gap"), 0),
                "farmTarget", farmPlan == null ? 0 : Gd.Int(Gd.Get(farmPlan, "Target"), 0),
                "farmTimes", farmPlan == null ? 0 : Gd.Int(Gd.Get(farmPlan, "Times"), 0));
            Dictionary<string, object> d = Gd.Map(Processes.PlanMaaQueue(body));
            if (d == null) throw new GdRuleException("plan-maa-queue 没有回执");

            foreach (object o in Gd.List(Gd.Get(d, "Logs")))
            {
                Dictionary<string, object> l = Gd.Map(o);
                if (l == null) continue;
                string lvl = StrOf(Gd.Get(l, "Level"), "");
                Say(lines, lvl, StrOf(Gd.Get(l, "Text"), ""));
            }
            List<object> changes = new List<object>();
            foreach (object o in Gd.List(Gd.Get(d, "Changes"))) changes.Add(StrOf(o, ""));
            List<object> chains = new List<object>();
            foreach (object o in Gd.List(Gd.Get(d, "ExpectedChains"))) chains.Add(StrOf(o, ""));

            if (flags["Apply"] && changes.Count > 0)
            {
                string bak = GdEnv.BackupFile(m["Config"]);
                string leaf = "";
                try { if (!string.IsNullOrEmpty(bak)) leaf = Path.GetFileName(bak); } catch { leaf = ""; }
                GdJson.Write(m["Config"], Gd.Get(d, "NewConfig"));
                Dictionary<string, bool> intent = new Dictionary<string, bool>(StringComparer.Ordinal);
                foreach (object o in Gd.List(Gd.Get(d, "Intent")))
                {
                    Dictionary<string, object> i = Gd.Map(o);
                    if (i == null) continue;
                    intent[StrOf(Gd.Get(i, "Name"), "")] = Gd.Truthy(Gd.Get(i, "Enable"));
                }
                object verify = GdJson.Read(m["Config"]);
                List<string> bad = new List<string>();
                foreach (object o in Gd.List(Gd.Get(Gd.Get(Gd.Get(verify, "Configurations"), "Default"), "TaskQueue")))
                {
                    string n = StrOf(Gd.Get(o, "Name"), "");
                    if (!intent.ContainsKey(n)) continue;
                    bool actual = Gd.Truthy(Gd.Get(o, "IsEnable"));
                    if (actual != intent[n]) bad.Add(n + "(期望 " + Gd.Str(intent[n]) + "，实际 " + Gd.Str(actual) + ")");
                }
                if (bad.Count > 0) throw new GdRuleException("MAA 配置回读不一致：" + string.Join("; ", bad.ToArray()) + "(备份：" + leaf + ")");
                Say(lines, "OK", "MAA 配置已更新并校验通过(备份：" + leaf + ")");
            }
            if (changes.Count == 0) changes.Add("(无需改动)");

            return Gd.Out("Done", true, "Path", m["Config"], "Changes", changes.ToArray(),
                "ExpectedChains", chains.ToArray(), "Stage", StrOf(Gd.Get(d, "Stage"), ""),
                "Applied", flags["Apply"], "Lines", lines.ToArray());
        }

        // apply／dryRun 两枚开关的取法：链体的 ctx 里给的是布尔，缺键按"预览"办(真身 [switch]$Apply 缺省 false)。
        static Dictionary<string, bool> ApplyFlags(Dictionary<string, object> c)
        {
            Dictionary<string, bool> m = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            m["Apply"] = OptBool(c, "apply");
            return m;
        }

        // settings.ark 那两枚队列名单(keepQueuesEnabled／disableQueues)的取法：夹具给了按夹具，缺 ⇒ 现场读。
        // 真身那一句是 `@($S.ark.keepQueuesEnabled)` —— 没填 ⇒ 空数组、填一枚 ⇒ 单元素数组，Gd.List 同形。
        static List<string> QueueList(Dictionary<string, object> c, string fixture, string arkKey)
        {
            object[] given = Gd.List(Gd.Get(c, fixture));
            if (Given(c, fixture))
            {
                List<string> outList = new List<string>();
                foreach (object o in given) outList.Add(Gd.Str(o));
                return outList;
            }
            object[] fromSettings = Gd.List(Gd.Get(Gd.Get(SettingsOf(c), "ark"), arkKey));
            List<string> live = new List<string>();
            foreach (object o in fromSettings) live.Add(Gd.Str(o));
            return live;
        }

        // ==================== verb: apply-m9a-instance ====================
        // Set-M9AInstance(m9a.ps1:73，10-26 第五刀刀② 后现读、原写 93；⚠ 11-01 坐标清账刀再换算：原写 :72 是空行；⚠ 11-05 转接刀·运行面刀⑨ 该真身已自 lib\m9a.ps1 出仓，以上坐标全按历史引文读)：点号字面键('Instances.LastActive')是 M9A 自己 appsettings 的形状，
        // Gd.Get 不做点号拆分 ⇒ 直接当字面键取，这一格与 PS 的 $cfg.'Instances.LastActive' 同效。
        // 命令行入口那枚开关(m9aCliArgs)的语义是"没写就是开着"(null ⇒ true)，与 takeover 那一路的
        // 真值判同族、与 arkAutoUpdateClick 的 `-ne $false` 又一族 —— 三族各照各的，不许统一。

        static Dictionary<string, string> M9APathsNow(Dictionary<string, object> c)
        {
            Dictionary<string, object> p = PathsOf(c);
            Dictionary<string, string> r = new Dictionary<string, string>();
            string exe = StrOf(Gd.Get(p, "m9aExe"), "");
            r["Name"] = BaseName(exe);
            r["AppSettings"] = OptStr(c, "appSettings", delegate
            { return GdEnv.JoinPath(StrOf(Gd.Get(p, "m9aDir"), ""), new string[] { StrOf(Gd.Get(p, "m9aAppSettings"), "") }); });
            return r;
        }

        static bool M9ACliOn(Dictionary<string, object> c)
        {
            object v = Gd.Get(c, "m9aCliArgs");
            if (v == null) v = Gd.Get(Gd.Get(SettingsOf(c), "runtime"), "m9aCliArgs");
            if (v == null) return true;             // 真身：`$null -eq $S.runtime.m9aCliArgs ⇒ return $true`
            return Gd.Truthy(v);
        }

        internal static object ApplyM9AInstance(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            Dictionary<string, bool> flags = ApplyFlags(c);
            Dictionary<string, object> inst = Gd.Map(Gd.Get(c, "instance")) ?? new Dictionary<string, object>();
            string id = StrOf(Gd.Get(inst, "Id"), "");
            string name = StrOf(Gd.Get(inst, "Name"), "");
            Dictionary<string, string> p = M9APathsNow(c);

            bool runningGiven = Given(c, "running");
            List<string> runningNames = OptList(c, "running");
            bool alive = runningGiven ? runningNames.Contains(p["Name"]) : Running(p["Name"]);
            if (alive)
            {
                if (!M9ACliOn(c)) throw new GdRuleException("M9A 正在运行，请先关闭再改实例");
                Say(lines, "WARN", "M9A 正在运行：改实例这一步跳过，启动时由命令行 -i/-f 接管(会转发给正在跑的实例)");
                return Gd.Out("Done", true, "Changes",
                    new object[] { "(M9A 在跑，交给命令行 -i 转发)" },
                    "Instance", inst, "Applied", false, "Lines", lines.ToArray());
            }
            if (!File.Exists(p["AppSettings"])) throw new GdRuleException("M9A appsettings 不存在：" + p["AppSettings"]);

            object cfg = Gd.Get(c, "config");
            if (cfg == null) cfg = GdJson.Read(p["AppSettings"]);
            Dictionary<string, object> tree = Gd.Map(cfg);
            if (tree == null)
            {
                tree = new Dictionary<string, object>();
                cfg = tree;
            }
            string cur = StrOf(Gd.Get(cfg, "Instances.LastActive"), "");
            List<object> changes = new List<object>();
            if (!cur.Equals(id, StringComparison.Ordinal))
                changes.Add("Instances.LastActive: " + cur + " → " + id + "(" + name + ")");
            string auto = StrOf(Gd.Get(cfg, "NoAutoStart"), "");
            if (!auto.Equals("False", StringComparison.OrdinalIgnoreCase))
                changes.Add("NoAutoStart: " + auto + " → False(启动即自动开始)");

            if (flags["Apply"] && changes.Count > 0)
            {
                string bak = GdEnv.BackupFile(p["AppSettings"]);
                string leaf = "";
                try { if (!string.IsNullOrEmpty(bak)) leaf = Path.GetFileName(bak); } catch { leaf = ""; }
                tree["Instances.LastActive"] = id;
                tree["Instances.LastActiveName"] = name;
                tree["NoAutoStart"] = "False";
                GdJson.Write(p["AppSettings"], tree);
                object verify = GdJson.Read(p["AppSettings"]);
                if (!StrOf(Gd.Get(verify, "Instances.LastActive"), "").Equals(id, StringComparison.Ordinal))
                    throw new GdRuleException("M9A 实例切换回读失败(备份：" + leaf + ")");
                Say(lines, "OK", "M9A 实例已切到[" + name + "](备份：" + leaf + ")");
            }
            if (changes.Count == 0) changes.Add("(已是 " + name + "，无需改动)");
            return Gd.Out("Done", true, "Changes", changes.ToArray(), "Instance", inst,
                "Applied", flags["Apply"], "Lines", lines.ToArray());
        }

        // ==================== verb: update-ark-farm-ledger ====================
        // Update-GdArkFarmLedger(ark-farm.ps1:125) 整枚直答：offset 续读 → 递给已在场的 parse-ark-drop-text →
        // 记 inventory／droptable 两本 → 认不出的名字留 30 条 → 回一句总结。
        // 掉落行的解析规则(剥前缀／块头／配对／拒绝表)一枚都不在这儿重写(批次 5 的 Orchestration.cs)；
        // 材料名↔代号两张表(ark-farm.ps1:53)与关卡表同一族：现场读一次定终身，夹具档不写缓存。
        // ⚠ 原样怪癖第 12 笔：offset.path 落进 inventory.json 是**本机绝对路径**，只落盘，不进 Summary／不进回执。

        static Dictionary<string, object> itemMapsCache;

        static Dictionary<string, object> ItemMapsNow(Dictionary<string, object> c, List<object> lines)
        {
            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "itemIndex"));
            if (given != null) return BuildItemMaps(given, lines);
            if (itemMapsCache != null) return itemMapsCache;
            string path = GdEnv.JoinPath(StrOf(Gd.Get(PathsOf(c), "maaDir"), ""),
                new string[] { "resource", "item_index.json" });
            object j = null;
            if (GdEnv.PathReady(path))
            {
                try { j = GdJson.Read(path); }
                catch (Exception ex) { Say(lines, "WARN", "item_index.json 解析失败：" + ex.Message); j = null; }
            }
            itemMapsCache = BuildItemMaps(j, lines);
            return itemMapsCache;
        }

        // Get-GdArkItemMaps(ark-farm.ps1:53) 的解读段：PS 那侧用 `@{}` 建表 ⇒ hashtable 键**不分大小写**，
        // 所以这里两张表都用 OrdinalIgnoreCase(材料名里有英文大小写混排的，按区分大小写就会认不出)。
        // "首现胜"那一格(`if (-not $n2i.ContainsKey($nm))`)同形；IdToName 是后写覆盖前写，也同形。
        static Dictionary<string, object> BuildItemMaps(object j, List<object> lines)
        {
            Dictionary<string, object> n2i = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, object> i2n = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, object> root = Gd.Map(j);
            if (root != null)
            {
                foreach (KeyValuePair<string, object> kv in root)
                {
                    string id = kv.Key ?? "";
                    string nm = StrOf(Gd.Get(kv.Value, "name"), "");
                    if (id.Length == 0 || nm.Length == 0) continue;
                    if (!n2i.ContainsKey(nm)) n2i[nm] = id;      // 首现胜(真身 `-not $n2i.ContainsKey($nm)`)
                    i2n[id] = nm;
                }
            }
            Dictionary<string, object> r = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            r["NameToId"] = n2i;
            r["IdToName"] = i2n;
            return r;
        }

        static string StateFile(Dictionary<string, object> c, string leaf)
        {
            string dir = OptStr(c, "stateDir", delegate
            {
                GdEnv.Init(null);
                return GdEnv.State;
            });
            return GdEnv.JoinPath(dir, new string[] { leaf });
        }

        internal static object UpdateFarmLedger(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            Dictionary<string, string> m = MaaPathsNow(c);
            string guiLog = m["GuiLog"];
            if (!GdEnv.PathReady(guiLog))
                return Gd.Out("Done", true, "Summary", "", "Blocks", 0, "Plus", 0L, "Unknown", 0, "Lines", lines.ToArray());

            Dictionary<string, object> maps = ItemMapsNow(c, lines);
            Dictionary<string, object> nameToId = Gd.Map(Gd.Get(maps, "NameToId")) ?? new Dictionary<string, object>();

            string invPath = StateFile(c, "ark-inventory.json");
            string dtPath = StateFile(c, "ark-droptable.json");
            string dropsPath = StateFile(c, "ark-farm-drops.json");

            Dictionary<string, object> byId = new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> origin = new Dictionary<string, object>(StringComparer.Ordinal);
            long savedOffset = 0;
            string savedOffsetPath = "";

            if (File.Exists(invPath))
            {
                try
                {
                    object j = GdJson.Read(invPath);
                    Dictionary<string, object> bb = Gd.Map(Gd.Get(j, "byId"));
                    if (bb != null)
                        foreach (KeyValuePair<string, object> kv in bb)
                        {
                            byId[kv.Key] = Gd.Int(Gd.Get(kv.Value, "count"), 0);
                            origin[kv.Key] = StrOf(Gd.Get(kv.Value, "origin"), "");
                        }
                    Dictionary<string, object> off = Gd.Map(Gd.Get(j, "offset"));
                    if (off != null)
                    {
                        savedOffsetPath = StrOf(Gd.Get(off, "path"), "");
                        savedOffset = Num(Gd.Get(off, "offset"));
                    }
                }
                catch (Exception ex) { Say(lines, "WARN", "ark-inventory.json 读不了(" + ex.Message + ")，按空账处理"); }
            }
            Dictionary<string, object> dt = new Dictionary<string, object>(StringComparer.Ordinal);
            if (File.Exists(dtPath))
            {
                try
                {
                    Dictionary<string, object> root = Gd.Map(GdJson.Read(dtPath));
                    if (root != null)
                        foreach (KeyValuePair<string, object> kv in root)
                        {
                            Dictionary<string, object> one = new Dictionary<string, object>(StringComparer.Ordinal);
                            Dictionary<string, object> src = Gd.Map(kv.Value);
                            if (src != null)
                                foreach (KeyValuePair<string, object> st in src)
                                    one[st.Key] = Gd.Out("runs", Gd.Int(Gd.Get(st.Value, "runs"), 0),
                                        "qty", Gd.Int(Gd.Get(st.Value, "qty"), 0));
                            dt[kv.Key] = one;
                        }
                }
                catch (Exception ex) { Say(lines, "WARN", "ark-droptable.json 读不了(" + ex.Message + ")，学习关系按空处理"); }
            }

            // 首跑标定：账本文件还不存在 + MAA 有 DepotData 缓存 → 拿它当"估算初值"(origin=depot)。
            string initialized = "";
            if (!File.Exists(invPath))
            {
                string depot = OptStr(c, "depotData", delegate
                { return GdEnv.JoinPath(StrOf(Gd.Get(PathsOf(c), "maaDir"), ""), new string[] { "data", "DepotData.json" }); });
                if (GdEnv.PathReady(depot))
                {
                    try
                    {
                        Dictionary<string, object> node = Gd.Map(Gd.Get(GdJson.Read(depot), "data"));
                        if (node != null)
                            foreach (KeyValuePair<string, object> kv in node)
                            {
                                int count;
                                if (int.TryParse(StrOf(kv.Value, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                                    && count > 0)
                                {
                                    byId[kv.Key] = count;
                                    origin[kv.Key] = "depot";
                                }
                            }
                        initialized = "已按 MAA 库存缓存标定初值(" + byId.Count + " 项，估算)";
                    }
                    catch { initialized = "DepotData 读不动，初值按 0 起账"; }
                }
                else initialized = "没有库存缓存，初值按 0 起账(会先多刷一轮才收敛)";
            }

            long startOff = 0;
            long len = 0;
            try { len = new FileInfo(guiLog).Length; } catch { len = 0; }
            if (savedOffsetPath.Length > 0 && savedOffsetPath == guiLog && len >= savedOffset) startOff = savedOffset;
            Tail t = ReadTail(guiLog, startOff);
            Dictionary<string, object> parsed = Gd.Map(Orchestration.FarmDropText(Sub("text", t.Text)));
            object[] blocks = parsed == null ? new object[0] : Gd.List(Gd.Get(parsed, "Blocks"));

            Dictionary<string, object> unk = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            long plus = 0;
            foreach (object o in blocks)
            {
                Dictionary<string, object> b = Gd.Map(o);
                if (b == null) continue;
                string stage = StrOf(Gd.Get(b, "Stage"), "");
                foreach (object io in Gd.List(Gd.Get(b, "Items")))
                {
                    Dictionary<string, object> it = Gd.Map(io);
                    if (it == null) continue;
                    string nm = StrOf(Gd.Get(it, "Name"), "");
                    object idv = nameToId.ContainsKey(nm) ? nameToId[nm] : null;
                    string id = StrOf(idv, "");
                    if (id.Length == 0) { unk[nm] = true; continue; }
                    long have = byId.ContainsKey(id) ? Num(byId[id]) : 0;
                    long delta = Num(Gd.Get(it, "Delta"));
                    if (delta > 0)
                    {
                        byId[id] = have + delta;
                        origin[id] = "drops";
                        plus += delta;
                    }
                    if (stage.Length > 0)
                    {
                        Dictionary<string, object> one = dt.ContainsKey(id) ? Gd.Map(dt[id]) : null;
                        if (one == null) { one = new Dictionary<string, object>(StringComparer.Ordinal); dt[id] = one; }
                        long runs = 0, qty = 0;
                        if (one.ContainsKey(stage))
                        {
                            Dictionary<string, object> prev = Gd.Map(one[stage]);
                            if (prev != null) { runs = Num(Gd.Get(prev, "runs")); qty = Num(Gd.Get(prev, "qty")); }
                        }
                        one[stage] = Gd.Out("runs", runs + 1, "qty", qty + delta);
                    }
                }
            }
            if (blocks.Length == 0 && initialized.Length == 0)
                return Gd.Out("Done", true, "Summary", "", "Blocks", 0, "Plus", 0L, "Unknown", unk.Count, "Lines", lines.ToArray());

            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Dictionary<string, object> byIdOut = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (string k in SortedKeys(byId))
                byIdOut[k] = Gd.Out("count", byId[k], "origin", origin.ContainsKey(k) ? origin[k] : "", "updated", stamp);
            Dictionary<string, object> invOut = Gd.Out("version", 1,
                "offset", Gd.Out("path", guiLog, "offset", t.Offset), "byId", byIdOut);
            GdJson.Write(invPath, invOut);

            Dictionary<string, object> dtOut = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (string k in SortedKeys(dt))
            {
                Dictionary<string, object> one = Gd.Map(dt[k]) ?? new Dictionary<string, object>();
                Dictionary<string, object> stageOut = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (string st in SortedKeys(one)) stageOut[st] = one[st];
                dtOut[k] = stageOut;
            }
            GdJson.Write(dtPath, dtOut);

            if (unk.Count > 0)
            {
                List<object> known = new List<object>();
                if (File.Exists(dropsPath))
                {
                    try { known.AddRange(Gd.List(GdJson.Read(dropsPath))); } catch { known = new List<object>(); }
                }
                foreach (string nm in SortedKeys(unk))
                {
                    bool has = false;
                    foreach (object k in known) if (StrOf(k, "").Equals(nm, StringComparison.OrdinalIgnoreCase)) { has = true; break; }
                    if (!has) known.Add(nm);
                }
                if (known.Count > 30) known = new List<object>(known.GetRange(known.Count - 30, 30));
                GdJson.Write(dropsPath, known);
            }

            List<string> bits = new List<string>();
            if (initialized.Length > 0) bits.Add(initialized);
            if (blocks.Length > 0) bits.Add("记了 " + blocks.Length + " 场掉落、入账 +" + plus);
            if (unk.Count > 0)
            {
                List<string> names = SortedKeys(unk);
                List<string> head = new List<string>();
                for (int i = 0; i < names.Count && i < 3; i++) head.Add(names[i]);
                bits.Add(unk.Count + " 个名字没在材料表认出来(" + string.Join("/", head.ToArray()) + ")");
            }
            return Gd.Out("Done", true, "Summary", string.Join("；", bits.ToArray()),
                "Blocks", blocks.Length, "Plus", plus, "Unknown", unk.Count, "Lines", lines.ToArray());
        }

        // ==================== verb: resolve-ark-farm-decision ====================
        // Resolve-GdArkFarmDecision(lib\ark-farm.ps1:293-331) 的活层整段(转接刀·判定面进核 2026-11-03 随臂搬家，1ev.①)：
        // 三道前置 + 取数 + 纯判定。纯判定真身是 Orchestration.FarmPlan(verb resolve-ark-farm-plan 的直身，
        // 进程内直调——与 :1343 FarmDropText 的调用形同)，这里搬的是壳侧接线：
        //   前置一(:299)  farmAppend 显式布尔 false 才关(PS 的 `-eq $false`：$null/字符串都不命中，别拿 Truthy 顶)；
        //   前置二(:300)  非 dedicated 且本轮不是 idle 方案 → 不补(活动期理智金贵)；
        //   前置三(:303)  非 dedicated 且今日已认栽(Test-GdArkFightSkipMarkToday :37-45：mark 文件不在/读坏都答 false，
        //                 "宁可失效也不拦正常轮"；文件名 ark-fight-skip-mark.json 在 state 目录，Date==今天才红) → 那一句原话。
        //   取数：item 表复用 ItemMapsNow、关卡表复用 StageTableNow(进程内缓存同源)；
        //         两本账照 Read-GdArkFarmLedger(:93-123) 逐字(读坏的两句 WARN 原话同 UpdateFarmLedger :1286/:1306；
        //         offset 那格不搬——它的消费面只在写账侧，归 update-ark-farm-ledger 真身)；
        //         targets 归一照 Get-GdArkFarmTargetsNormalized(:242-251)，逐条进程内调 Orchestration.FarmTargetSpec
        //         (verb parse-farm-target-spec 的直身＝PS Parse-GdArkFarmTargetSpec 的等值件，Bad 的留着让判定层去说)；
        //         depot 纠偏照 Sync-GdArkFarmHaveFromDepot(:253-291) 逐字——"缓存比自家账本新才采信"那条规矩
        //         (账本吃全量+增量，缓存比它旧时采信会反向多刷)，只改内存不写盘，纠偏说明走 INFO 行(真身 :312)。
        // maxTimes 缺省 12(:315 TryParse)；!Active 档 dedicated 那句 WARN 原话 :322。
        // 回执 Farm=farmOpt(Active)或 null——核臂把 Farm 原样挂进 decision(PS :87-88 挂原对象同形)。
        internal static object ResolveArkFarmDecision(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<object> lines = new List<object>();
            bool dedicated = OptBool(c, "dedicated");
            Dictionary<string, object> dec = Gd.Map(Gd.Get(c, "decision")) ?? new Dictionary<string, object>();
            Dictionary<string, object> ark = Gd.Map(Gd.Get(SettingsOf(c), "ark")) ?? new Dictionary<string, object>();

            object farmAppend = Gd.Get(ark, "farmAppend");
            if (!dedicated && farmAppend is bool && !(bool)farmAppend)
                return Gd.Out("Farm", null, "Lines", lines.ToArray());
            if (!dedicated && StrOf(Gd.Get(dec, "Mode"), "") != "idle")
                return Gd.Out("Farm", null, "Lines", lines.ToArray());

            string markPath = StateFile(c, "ark-fight-skip-mark.json");
            if (!dedicated && File.Exists(markPath))
            {
                bool today = false;
                try
                {
                    Dictionary<string, object> mk = Gd.Map(GdJson.Read(markPath));
                    if (mk != null)
                        today = StrOf(Gd.Get(mk, "Date"), "") == DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                catch { today = false; }
                if (today)
                {
                    Say(lines, "INFO", "补刷前置今日已认栽(MAA 判关卡全关) → 本轮不前置补刷关，按长草计划走");
                    return Gd.Out("Farm", null, "Lines", lines.ToArray());
                }
            }

            Dictionary<string, object> maps = ItemMapsNow(c, lines);
            Dictionary<string, object> nameToId = Gd.Map(Gd.Get(maps, "NameToId")) ?? new Dictionary<string, object>();
            Dictionary<string, object> idToName = Gd.Map(Gd.Get(maps, "IdToName")) ?? new Dictionary<string, object>();

            // 两本账(Read-GdArkFarmLedger :93-123)：文件不在就是空账，不抛——首跑 Resolve 先于任何记账。
            Dictionary<string, object> byId = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, object> origin = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string invPath = StateFile(c, "ark-inventory.json");
            if (File.Exists(invPath))
            {
                try
                {
                    Dictionary<string, object> j = Gd.Map(GdJson.Read(invPath));
                    Dictionary<string, object> bb = j == null ? null : Gd.Map(Gd.Get(j, "byId"));
                    if (bb != null)
                        foreach (KeyValuePair<string, object> kv in bb)
                        {
                            byId[kv.Key] = Gd.Int(Gd.Get(kv.Value, "count"), 0);
                            origin[kv.Key] = StrOf(Gd.Get(kv.Value, "origin"), "");
                        }
                }
                catch (Exception ex) { Say(lines, "WARN", "ark-inventory.json 读不了(" + ex.Message + ")，按空账处理"); }
            }
            Dictionary<string, object> dt = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string dtPath = StateFile(c, "ark-droptable.json");
            if (File.Exists(dtPath))
            {
                try
                {
                    Dictionary<string, object> root = Gd.Map(GdJson.Read(dtPath));
                    if (root != null)
                        foreach (KeyValuePair<string, object> kv in root)
                        {
                            Dictionary<string, object> one = new Dictionary<string, object>(StringComparer.Ordinal);
                            Dictionary<string, object> src = Gd.Map(kv.Value);
                            if (src != null)
                                foreach (KeyValuePair<string, object> st in src)
                                    one[st.Key] = Gd.Out("runs", Gd.Int(Gd.Get(st.Value, "runs"), 0),
                                        "qty", Gd.Int(Gd.Get(st.Value, "qty"), 0));
                            dt[kv.Key] = one;
                        }
                }
                catch (Exception ex) { Say(lines, "WARN", "ark-droptable.json 读不了(" + ex.Message + ")，学习关系按空处理"); }
            }

            // targets 归一(Get-GdArkFarmTargetsNormalized :242-251)
            List<object> targetsNorm = new List<object>();
            foreach (object rawT in Gd.List(Gd.Get(ark, "farmTargets")))
            {
                Dictionary<string, object> spec = Gd.Map(Orchestration.FarmTargetSpec(Sub("raw", rawT)));
                if (spec == null || !Gd.Truthy(Gd.Get(spec, "Found"))) continue;
                targetsNorm.Add(Gd.Get(spec, "Spec"));
            }

            // depot 纠偏(Sync-GdArkFarmHaveFromDepot :253-291)：只认"缓存比账本新"。
            List<string> wantIds = new List<string>();
            foreach (object t0 in targetsNorm)
            {
                Dictionary<string, object> t = Gd.Map(t0);
                if (t == null || StrOf(Gd.Get(t, "Bad"), "").Length > 0) continue;
                string key = StrOf(Gd.Get(t, "Key"), "");
                string id = "";
                if (System.Text.RegularExpressions.Regex.IsMatch(key, @"^\d{3,8}$")) id = key;
                else if (nameToId.ContainsKey(key)) id = StrOf(nameToId[key], "");
                if (id.Length > 0 && !wantIds.Contains(id)) wantIds.Add(id);
            }
            if (wantIds.Count > 0)
            {
                string depot = GdEnv.JoinPath(StrOf(Gd.Get(PathsOf(c), "maaDir"), ""), new string[] { "data", "DepotData.json" });
                if (GdEnv.PathReady(depot))
                {
                    DateTime depotM = DateTime.MinValue, invM = DateTime.MinValue;
                    try { depotM = File.GetLastWriteTime(depot); } catch { }
                    try { if (File.Exists(invPath)) invM = File.GetLastWriteTime(invPath); } catch { }
                    bool invNewer = File.Exists(invPath) && depotM <= invM;
                    if (!invNewer)
                    {
                        Dictionary<string, object> d = null;
                        try { d = Gd.Map(GdJson.Read(depot)); } catch { d = null; }
                        Dictionary<string, object> node = d == null ? null : Gd.Map(Gd.Get(d, "data"));
                        if (node != null)
                        {
                            List<string> bits = new List<string>();
                            foreach (string id in wantIds)
                            {
                                int cnt = 0;
                                int.TryParse(StrOf(Gd.Get(node, id), ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out cnt);
                                int old = byId.ContainsKey(id) ? Gd.Int(byId[id], 0) : -1;
                                if (old != cnt)
                                {
                                    byId[id] = cnt;
                                    origin[id] = "depot";
                                    bits.Add("id=" + id + " 现值 " + old + "→" + cnt);
                                }
                            }
                            if (bits.Count > 0)
                                Say(lines, "INFO", "补刷纠偏：库存缓存比账本新，已按实时读数纠偏：" + string.Join("；", bits.ToArray()));
                        }
                    }
                }
            }

            Dictionary<string, object> known = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (string k in StageTableNow(c, lines).Keys) known[k] = true;
            // PS :315 逐字：$mt = 12 后 TryParse——解析失败时 .NET 把 out **清 0**(12 只是摆设)，钳 1 归 FarmPlan 内做。
            // 别在这里把 0 救回 12：那不是真身。「缺键 ⇒ 单轮封顶 1」这格怪癖由自检钉住。
            int mt = 12;
            int.TryParse(StrOf(Gd.Get(ark, "farmMaxTimesPerRound"), ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out mt);

            Dictionary<string, object> fp = Gd.Map(Orchestration.FarmPlan(Sub(
                "targets", targetsNorm.ToArray(),
                "nameToId", nameToId,
                "idToName", idToName,
                "inventory", byId,
                "origin", origin,
                "dropTable", dt,
                "knownStages", known,
                "maxTimes", mt)));
            if (fp == null || !Gd.Truthy(Gd.Get(fp, "Active")))
            {
                if (dedicated)
                    Say(lines, "WARN", "专用材料轮没有可刷的目标：" + StrOf(Gd.Get(fp, "Reason"), ""));
                return Gd.Out("Farm", null, "Lines", lines.ToArray());
            }
            return Gd.Out("Farm", fp, "Lines", lines.ToArray());
        }

        // ==================== 刀㉔(2026-11-08)：m9a-update 两枚待承接纯规则进核 ====================
        // 射程＝tools\m9a-update.ps1 那两枚「读盘＋纯比较」：
        //   read-m9a-install    ← Get-GdM9AInstall(定义 :32／调用点 :169)：定位装包目录＋现读 interface.json 的 version/github
        //   compare-version-gap ← Test-GdM9AVersionGap(定义 :66／调用点 :180)：剥 v/V 前缀后比字面
        // 两句异常话术逐字照抄(装包目录不在／版本源不在／没有 version 字段)——它们是玩家真会看到的那三句。
        // 语义照抄不收紧：PS `-match` 与 `-ne` 对字符串都**不分大小写**，这里用 IgnoreCase／OrdinalIgnoreCase 对齐；
        // 标成严格相等看起来更"干净"，但那不是真身。
        internal static object ReadM9AInstall(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            Dictionary<string, object> settings = Gd.Map(Gd.Get(c, "settings"));
            if (settings == null) throw new GdRuleException("read-m9a-install：settings 缺键(装包目录只能从 settings.paths 来)");
            Dictionary<string, object> paths = Gd.Map(Gd.Get(settings, "paths"));
            string dir = paths == null ? "" : StrOf(Gd.Get(paths, "m9aDir"), "");
            if (dir.Length == 0 || !Directory.Exists(dir)) throw new GdRuleException("M9A 装包目录不在：" + dir);
            string iface = Path.Combine(dir, "interface.json");
            if (!File.Exists(iface)) throw new GdRuleException("M9A 版本源 interface.json 不在：" + iface + "(本工具不猜版本)");
            Dictionary<string, object> m;
            try { m = Gd.Map(GdJson.Read(iface)); }
            catch (Exception ex) { throw new GdRuleException("M9A 版本源 interface.json 读不出：" + ex.Message); }
            string cur = m == null ? "" : StrOf(Gd.Get(m, "version"), "");
            if (cur.Length == 0) throw new GdRuleException("interface.json 里没有 version 字段，判不了现版本");
            string repo = "MAA1999/M9A";
            string gh = m == null ? "" : StrOf(Gd.Get(m, "github"), "");
            if (gh.Length > 0)
            {
                // PS :47 `if ($gh -and $gh -match 'github\.com/([^/]+/[^/]+)') { $repo = ($Matches[1] -replace '/+$', '') }`
                Match mm = Regex.Match(gh, "github\\.com/([^/]+/[^/]+)", RegexOptions.IgnoreCase);
                if (mm.Success) repo = Regex.Replace(mm.Groups[1].Value, "/+$", "");
            }
            string exe = StrOf(Gd.Get(paths, "m9aExe"), "");
            if (exe.Length == 0) exe = "m9a.exe";
            return Gd.Out("Dir", dir, "Current", cur, "Repo", repo, "Exe", exe);
        }

        internal static object CompareVersionGap(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string cur = StrOf(Gd.Get(c, "current"), "");
            string latest = StrOf(Gd.Get(c, "latest"), "");
            string a = Regex.Replace(cur, "^[vV]", "");
            string b = Regex.Replace(latest, "^[vV]", "");
            return Gd.Out("Gap", !string.Equals(a, b, StringComparison.OrdinalIgnoreCase));
        }

        // PS 的 `$keys | Sort-Object` 那一把尺：字符串按序排(账本里都是数字 id／英文关卡名，
        // Ordinal 与 CurrentCulture 在这两族上同结果)；unk 名单走的是 `-notcontains`(不分大小写)那一族，
        // 排序仍按 Ordinal，取前 3 的口径与 `Select-Object -First 3` 同形(枚举序 = 排序后的序)。
        static List<string> SortedKeys(Dictionary<string, object> d)
        {
            List<string> keys = new List<string>(d.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }
    }
}
