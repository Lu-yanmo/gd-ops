// 桥接层：UI 只依赖这个接口。web 浏览器走 DemoBridge（演示数据，与原型逐字同源）；
// Tauri 壳走 TauriBridge（mode=live：只读实况快照；点火/写入留到下一阶段）。
// shell 表示宿主（web 浏览器 / tauri 桌面壳），mode 表示内核（demo 演示 / live 真实）。
import type { AppInfo, GameId, Snapshot } from './types';
import { esc, humanAge, pad } from './util';

export type ChainStart = { ok: boolean; pid?: number; script?: string; error?: string };
export type ChainEnd = { code: number; secs: number; lines: number };

export interface Bridge {
  shell: 'web' | 'tauri';
  mode: 'demo' | 'live';
  appInfo(): Promise<AppInfo>;
  snapshot(fresh?: boolean): Promise<Snapshot | null>;  // 只读实况；浏览器/demo 恒 null
  chainRun(kind: 'dry'): Promise<ChainStart>;         // 起一轮 -DryRun 预演（只有 Tauri 壳能起）
  chainStop(): Promise<boolean>;                      // 结束本应用起的进程树
  onChainLine(cb: (stream: string, line: string) => void): void;
  onChainEnd(cb: (p: ChainEnd) => void): void;
  dryRun(): Promise<string>;         // 今天会跑什么 → 模态 HTML
  health(): Promise<string>;         // 环境体检 → 模态 HTML
  openLogDir(): Promise<string>;     // 打开日志目录
  visionAsk(): Promise<string>;      // 只问不点
  latestShot(): Promise<string>;     // 看最新截图
  fullSettings(): Promise<string>;   // 完整设置
  diagnostics(): Promise<string>;    // 诊断打包
  auditEvidence(): Promise<string>;  // 活动审计证据
  setSwitch(key: string, on: boolean): Promise<void>;
  ack1999(): Promise<void>;
  shopMark(game: GameId, on: boolean): Promise<void>;
  reschedule(times: number[]): Promise<void>;
}

const DEMO_INFO: AppInfo = { app: 'gd-ops', version: '0.1.0', platform: 'web', arch: '—' };

// 模态文案与原型 hud.html 逐字同源；真实实现接内核后由 TauriBridge 返回现量。
export class DemoBridge implements Bridge {
  shell: 'web' | 'tauri' = 'web';
  mode: 'demo' | 'live' = 'demo';
  async appInfo(): Promise<AppInfo> { return DEMO_INFO; }
  async snapshot(): Promise<Snapshot | null> { return null; }
  async chainRun(_kind: 'dry'): Promise<ChainStart> { return { ok: false, error: '浏览器演示模式不起进程（桌面壳里才有预演）' }; }
  async chainStop(): Promise<boolean> { return false; }
  onChainLine(_cb: (stream: string, line: string) => void): void { /* 演示无流 */ }
  onChainEnd(_cb: (p: ChainEnd) => void): void { /* 演示无流 */ }
  async dryRun(): Promise<string> {
    return '<div class="sec">ARK · 方案 event</div>活动 SideStory · 全刷 <code>TL-9</code>；队列 <code>StartUp/Recruit/Infrast/Mall/Award</code> + <code>Fight</code>；长草轮追加 <code>farmTargets</code> 材料补刷。<div class="sec">R1999 · 方案 idle</div>实例日常十项：常规作战 / 银行购物 / 智能均衡刷材料 / … / 关闭游戏；末尾活动审计闸。<div class="sec">门</div>活动门自证（ark-event-probe）· 商店标记 · 1999 主线 ack。<br>真实实现：daily-clear.ps1 -DryRun，只预演不点火。';
  }
  async health(): Promise<string> {
    return '<div class="sec">环境</div>三态 ok · 屏保已 unarm · 版本 K2.1 · 内核 live<div class="sec">进程</div>MAA 未跑 · MuMu 未跑 · 监督 pid 4321<div class="sec">今日判定</div>ark ok / r1999 ok（19:30 轮）<div class="sec">模块 / 门 / 历史</div>模块注册表 2 路在位 · 活动门绿 · history.csv 尾 5 行见历史页<br>演示读数，真实实现调 status.ps1。';
  }
  async openLogDir(): Promise<string> {
    return '<code>logs/</code> · 命名 <code>run-YYYYMMDD-HHMMSS-daily-{auto|manual}.log</code><br>真实实现：Process.Start explorer.exe logs（panel 现有按钮同行为）。<br>演示环境不真正打开资源管理器。';
  }
  async visionAsk(): Promise<string> {
    return 'tools/vision-ask.ps1 · 拍临时图问模型，不注入任何点击。<br>临时图 <code>state/rescue/ask-MMdd-HHmmss-&lt;kind&gt;.png</code> 用完即删。';
  }
  async latestShot(): Promise<string> {
    return '取 <code>state/rescue/*-before.png</code> 与 <code>state/vision/ark-launcher-*.png</code> 按 LastWriteTime 最新一枚；<code>calls.jsonl</code> 的 image 字段反查请求。<br>演示占位帧，不真读图。';
  }
  async fullSettings(): Promise<string> {
    return '分区（对照 panel-cs 配置页）：<div class="sec">PATHS</div><code>maaDir / m9aDir / arknightsExe / mumuDir / vmIndex</code><div class="sec">视觉与通知</div><code>visionModel / visionBaseUrl / visionApiKey / notify.webhook</code><div class="sec">说明</div>路径与 Key 不进快捷区；清空视觉 Key 等破坏性操作留在此层并二次确认。';
  }
  async diagnostics(): Promise<string> {
    return '门账（静态 Check 现量）：<code>test-ark 56 · test-r1999 46 · test-modules 90 · test-lib 43</code> · check-ps51 编码/行尾守卫。<br>打包到桌面 / 先打临时目录（panel-cs 诊断页同行为）。演示不真打包。';
  }
  async auditEvidence(): Promise<string> {
    return '<code>state/1999-progress-audit/</code> · ts/eventKey/verdict/acked/png<br><code>ask-*.json</code> 提示词 + <code>calls.jsonl</code> 模型调用账。<br>最近 verdict=yes · png=audit-1109-1815.png（演示）。';
  }
  async setSwitch(_key: string, _on: boolean): Promise<void> { /* 演示：状态翻转与日志在 state 层 */ }
  async ack1999(): Promise<void> { /* 演示 */ }
  async shopMark(_game: GameId, _on: boolean): Promise<void> { /* 演示 */ }
  async reschedule(_times: number[]): Promise<void> { /* 演示 */ }
}

type Invoke = (cmd: string) => Promise<unknown>;
type Listen = (evt: string, cb: (e: { payload: unknown }) => void) => Promise<() => void>;

function todayStr(): string {
  const d = new Date();
  return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
}
const mb = (n: number): string => (n / 1048576).toFixed(1) + ' MB';

// Tauri 壳：app_info 走 Rust 现量；其余读数走 live_snapshot（只读，不点火）。
export class TauriBridge extends DemoBridge {
  private invoke: Invoke;
  private listen?: Listen;
  private cache: Snapshot | null = null;
  constructor(invoke: Invoke, listen?: Listen) {
    super();
    this.invoke = invoke;
    this.listen = listen;
    this.shell = 'tauri';
    this.mode = 'live';
  }

  override async appInfo(): Promise<AppInfo> {
    try {
      const raw = await this.invoke('app_info');
      const v = raw as Partial<AppInfo>;
      return {
        app: typeof v.app === 'string' ? v.app : DEMO_INFO.app,
        version: typeof v.version === 'string' ? v.version : DEMO_INFO.version,
        platform: typeof v.platform === 'string' ? v.platform : DEMO_INFO.platform,
        arch: typeof v.arch === 'string' ? v.arch : DEMO_INFO.arch,
        autodry: v.autodry === true,
      };
    } catch { return DEMO_INFO; }
  }

  override async snapshot(fresh = false): Promise<Snapshot | null> {
    if (!fresh && this.cache) return this.cache;
    try {
      const raw = await this.invoke('live_snapshot');
      const v = raw as Snapshot;
      if (v && typeof v.ok === 'boolean') { this.cache = v; return v; }
      return this.cache;
    } catch { return this.cache; }
  }

  override async chainRun(_kind: 'dry'): Promise<ChainStart> {
    try {
      const raw = await this.invoke('chain_run') as ChainStart;
      if (raw && typeof raw.ok === 'boolean') return raw;
      return { ok: false, error: 'chain_run 返回形态异常' };
    } catch (e) {
      return { ok: false, error: String(e) };
    }
  }

  override async chainStop(): Promise<boolean> {
    try { return (await this.invoke('chain_stop')) === true; } catch { return false; }
  }

  override onChainLine(cb: (stream: string, line: string) => void): void {
    if (!this.listen) return;
    void this.listen('chain-line', (e) => {
      const p = (e && e.payload) as { stream?: unknown; line?: unknown } | undefined;
      if (p && typeof p.line === 'string') cb(typeof p.stream === 'string' ? p.stream : 'out', p.line);
    });
  }

  override onChainEnd(cb: (p: ChainEnd) => void): void {
    if (!this.listen) return;
    void this.listen('chain-end', (e) => {
      const p = (e && e.payload) as Partial<ChainEnd> | undefined;
      cb({
        code: p && typeof p.code === 'number' ? p.code : -1,
        secs: p && typeof p.secs === 'number' ? p.secs : 0,
        lines: p && typeof p.lines === 'number' ? p.lines : 0,
      });
    });
  }

  override async health(): Promise<string> {
    const s = await this.snapshot();
    if (!s || !s.ok) {
      return '<div class="sec">只读快照不可用</div>找不到仓库根目录：把 <code>gdops.exe</code> 放在仓库内（认 <code>daily-clear.ps1</code> + <code>lib\\common.ps1</code>），或设环境变量 <code>GDOPS_ROOT</code> 指向仓库根。<div class="sec">平台</div>' + esc((s && s.platform ? s.platform : 'unknown') + '/' + (s && s.arch ? s.arch : '?'));
    }
    const today = todayStr();
    const rows = (s.history || []).filter((r) => r.time.slice(0, 10) === today);
    const todayHtml = rows.length
      ? rows.map((r) => esc(r.time.slice(11)) + ' ' + esc(r.game) + ' ' + (r.status === 'ok' ? '<b class="mok">ok</b>' : '<b class="mcrit">' + esc(r.status) + '</b>')).join(' · ')
      : '<span class="mut">今天还没有记录</span>';
    const procs = (s.procs || []).map((p) => esc(p.name) + ' ' + (p.running ? '<b class="mok">运行中</b>' : '<span class="mut">未运行</span>')).join(' · ');
    const gate = s.arkGate
      ? esc(s.arkGate.date) + ' · ' + esc(s.arkGate.stage) + ' · ' + (s.arkGate.entered ? '<b class="mok">进得去</b>' : '<b class="mwarn">未进</b>') + '（' + esc(s.arkGate.evidence) + '）'
      : '<span class="mut">无活动门记录</span>';
    const logLine = s.log
      ? '<code>' + esc(s.log.name) + '</code> · ' + humanAge(s.log.ageSecs) + '前 · ' + mb(s.log.size) + ' · 目录共 ' + s.log.files + ' 本'
      : '<span class="mut">无 run 日志</span>';
    const beat = s.supervision
      ? '<code>' + esc(s.supervision.file) + '</code> · ' + humanAge(s.supervision.ageSecs) + '前 · 尾段 ' + s.supervision.beats + ' 拍'
      : '<b class="mwarn">无监督心跳文件</b>';
    return '<div class="sec">环境</div>根目录 <code>' + esc(s.root || '') + '</code> · 版本 <b>v' + esc(s.version || '?') + '</b> · ' + esc(s.platform) + '/' + esc(s.arch) +
      '<br>settings.json ' + (s.settingsPresent ? '<b class="mok">在位</b>（只判存在，不读值）' : '<b class="mwarn">缺失</b>（跑 setup.ps1）') +
      '<br>模块注册表：' + ((s.modules && s.modules.length) ? s.modules.map(esc).join(' / ') + '（lib/*-module.ps1）' : '<b class="mwarn">没有模块文件</b>') +
      '<div class="sec">进程 / 模拟器</div>' + procs +
      '<div class="sec">今日判定（' + esc(today) + '）</div>' + todayHtml +
      '<div class="sec">活动门</div>' + gate +
      '<div class="sec">日志 / 心跳</div>' + logLine + '<br>' + beat +
      '<div class="sec">说明</div>只读接入阶段：快照全部来自现场文件与进程表，不点火、不写配置；运行接入在下一阶段。';
  }

  override async dryRun(): Promise<string> {
    const s = await this.snapshot();
    if (!s || !s.ok) return '只读快照不可用：找不到仓库根目录。';
    const head = (s.log ? s.log.head : []).filter((l) => /方案：|环境检查|已加载游戏模块|管理员权限|屏保|临时禁止系统睡眠/.test(l)).slice(0, 10);
    const plan = head.length ? head.map((l) => '<div>' + esc(l) + '</div>').join('') : '<span class="mut">最近日志里没有计划行</span>';
    const notice = s.lastNotice
      ? '<div class="sec">最近一轮结论</div>' + esc(s.lastNotice.title) + ' · ' + esc(s.lastNotice.level) + ' · ' + esc(s.lastNotice.time) + '<br>' + esc(s.lastNotice.text).replace(/\n/g, '<br>')
      : '';
    return '<div class="sec">最近一轮 · 实读日志头（' + esc(s.log ? s.log.name : '—') + '）</div>' + plan + notice +
      '<div class="sec">真跑一次预演</div><button class="btn fire" id="dryGo">真跑 -DryRun（约 6s · 只预演，不点火）</button>' +
      '<div class="sec">说明</div>点上面的按钮会起 <code>pwsh -File daily-clear.ps1 -DryRun -NoElevate</code>，stdout/stderr 逐行回到下方日志面板；退出码 0/1/2 照实显示。不碰任何游戏与模拟器，不写配置、不读 <code>settings.json</code> 的值。';
  }

  override async openLogDir(): Promise<string> {
    try {
      const p = await this.invoke('open_log_dir') as string;
      return '已在文件管理器打开 <code>' + esc(p) + '</code>。';
    } catch (e) {
      return '打开失败：' + esc(String(e));
    }
  }

  override async visionAsk(): Promise<string> {
    return '只读阶段未接 <code>tools/vision-ask.ps1</code>（下一阶段：Rust 起 pwsh 只问不点，临时图用完即删）。当前不会注入任何点击。';
  }

  override async latestShot(): Promise<string> {
    const s = await this.snapshot();
    const shots = (s ? s.shots : null) || [];
    if (!shots.length) return 'state/rescue 与 state/vision 下暂无 png。';
    return shots.slice(0, 6).map((x, i) => (i === 0 ? '<b class="mok">最新 </b>' : '') + '<code>' + esc(x.path) + '</code> · ' + humanAge(x.ageSecs) + '前 · ' + esc(x.kind)).join('<br>') +
      '<div class="sec">说明</div>只读阶段只列路径，不内嵌图片；模型调用账见 <code>state/vision/calls.jsonl</code>。';
  }

  override async fullSettings(): Promise<string> {
    const s = await this.snapshot();
    const present = s && s.settingsPresent ? '<b class="mok">在位</b>（只判存在）' : '<b class="mwarn">缺失</b>';
    return '只读阶段不读 <code>config/settings.json</code> 的任何值（Key 永不进界面）。<div class="sec">分区（键名）</div>PATHS：<code>maaDir / m9aDir / arknightsExe / mumuDir / vmIndex</code><div class="sec">视觉与通知</div><code>visionModel / visionBaseUrl / visionApiKey / notify.webhook</code><div class="sec">配置件</div>settings.json ' + present + ' · 路径与 Key 不进快捷区；清空视觉 Key 等破坏性操作留待设置页（下一阶段）。';
  }

  override async diagnostics(): Promise<string> {
    const s = await this.snapshot();
    if (!s || !s.ok) return '只读快照不可用。';
    const mods = (s.modules || []).join(' / ') || '—';
    const hist = (s.history || []).length;
    const log = s.log ? esc(s.log.name) + ' · ' + humanAge(s.log.ageSecs) + '前 · ' + mb(s.log.size) + ' · 共 ' + s.log.files + ' 本' : '无';
    return '<div class="sec">模块 / 日志 / 历史</div>模块：' + esc(mods) + '<br>日志：' + log + '<br>history.csv 快照尾段：' + hist + ' 行' +
      '<div class="sec">说明</div>诊断打包未接（下一阶段：打临时目录 zip 并给路径）。';
  }

  override async auditEvidence(): Promise<string> {
    const s = await this.snapshot();
    const a = s ? s.audit : null;
    return '<div class="sec">1999 进度审计</div>' + (a ? '<code>' + esc(a.file) + '</code> · ' + humanAge(a.ageSecs) + '前' : '<span class="mut">目录里还没有记录</span>') +
      '<div class="sec">说明</div>只读阶段只列最新一枚；ts/eventKey/verdict/acked 解析与 png 预览在下一阶段。';
  }
}

interface TauriGlobal { core?: { invoke?: Invoke }; event?: { listen?: Listen } }
export function createBridge(): Bridge {
  const t = (window as unknown as { __TAURI__?: TauriGlobal }).__TAURI__;
  if (t && t.core && typeof t.core.invoke === 'function') {
    return new TauriBridge(t.core.invoke.bind(t.core), t.event?.listen?.bind(t.event));
  }
  return new DemoBridge();
}
