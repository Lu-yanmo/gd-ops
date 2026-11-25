// 实况：轮询核内 summary（状态真身）+ Rust 快照（日志/截图/进程/运行锁），灌进状态机。
// 只在这里做「协议/文件 → 状态机」的映射；渲染层照旧订阅事件。
import type { Bridge } from './bridge';
import type { Level, Snapshot } from './types';
import * as S from './state';
import { esc, emit, humanAge, pad } from './util';
import { parseLine, lineHtml } from './logline';
import { chain, onChainEnd, onChainLine, syncChainButtons, wireChain } from './chain';

let seen = new Set<string>();
let curFile = '';
let polling = false;
let streaming = false;   // 预演流进行中：暂停快照重灌日志，免得流到一半被新日志文件整页替换
let pendingEnd: { lv: 'OK' | 'WARN'; msg: string } | null = null;  // 整页重灌后要把「预演结束」补回面板
let warned = { summary: false, snapshot: false };
let bound: Bridge | null = null;

// 控制动作之后要立刻看到新状态：借同一条 tick（summary→snapshot），不必等下一个 6s 拍。
export function kick(): void {
  if (bound) void tick(bound);
}

export async function startLive(bridge: Bridge): Promise<void> {
  bound = bridge;
  wireChain(bridge, {
    setStreaming: (v) => {
      streaming = v;
      if (v) pendingEnd = null;
    },
    refresh: () => { void tick(bridge); },
  });
  bridge.onChainLine((stream, line) => onChainLine(stream, line));
  bridge.onChainEnd((p) => { pendingEnd = onChainEnd(p); });
  syncChainButtons();
  const first = await bridge.snapshot(true);
  if (first && first.ok) apply(first);
  else {
    const chip = document.getElementById('modeChip');
    if (chip) { chip.textContent = '本地实况 · 根目录未找到'; chip.classList.add('live'); }
    const core = document.getElementById('coreChip');
    if (core) core.textContent = '—';
    const beat = document.getElementById('svBeat');
    if (beat) beat.textContent = '—';
    const dot = document.getElementById('svDot');
    if (dot) dot.className = 'dot warn';
    const mt = document.getElementById('modeTag');
    if (mt) mt.textContent = 'LOCAL · 实况（根目录未找到）';
    S.pushLogs([{ lv: 'WARN', html: lineHtml('WARN', '快照不可用：找不到仓库根目录（把 gdops.exe 放进仓库，或设 GDOPS_ROOT）') }]);
  }
  await tick(bridge);
  window.setInterval(() => { void tick(bridge); }, 6000);
}

async function tick(bridge: Bridge): Promise<void> {
  if (polling) return;
  polling = true;
  try {
    // 先 summary 后 snapshot：summary 会重建游戏卡（插件注册表驱动），snapshot 再补计划行/日志/锁。
    const m = await bridge.summary();
    if (m) S.applySummary(m);
    else if (!warned.summary && bridge.mode === 'live') {
      warned.summary = true;
      S.pushLog('WARN', '内核 summary 读不出（panel-cs.exe 未编译，或插件注册表为空/损坏）');
    }
    const snap = await bridge.snapshot(true);
    if (snap && snap.ok) apply(snap);
    else if (!warned.snapshot) {
      warned.snapshot = true;
      S.pushLog('WARN', '现场快照读不出：日志/截图/进程/运行锁这次是空读数');
    }
  } finally {
    polling = false;
  }
}

function apply(s: Snapshot): void {
  applyPlans(s);
  applyTopbar(s);
  applyLivePanel(s);
  applyLastRound(s);
  applyTicker(s);
  seedLog(s);
}

// 跑马灯改成实况读数：今日条数/成功率/版本/活动门/日志年龄/心跳。
function applyTicker(s: Snapshot): void {
  const train = document.getElementById('train');
  if (!train) return;
  const d = new Date();
  const today = d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
  const rows = (s.history || []).filter((r) => r.time.slice(0, 10) === today);
  const ok = rows.filter((r) => r.status === 'ok').length;
  const pct = rows.length ? Math.round((ok / rows.length) * 100) : 0;
  const gate = s.arkGate && s.arkGate.entered ? s.arkGate.stage + ' 进得去' : '—';
  const items = [
    'TODAY <b>' + rows.length + ' 条</b>',
    'OK <b>' + pct + '%</b>',
    'KERNEL <b>v' + esc(s.version || '?') + '</b>',
    'GATE <b>' + esc(gate) + '</b>',
    'RUN <b>' + (s.run && s.run.alive ? 'pid ' + s.run.pid : 'idle') + '</b>',
    'LOG <b>' + (s.log ? humanAge(s.log.ageSecs) + '前' : '—') + '</b>',
    'SUPERVISOR <b>' + (s.supervision ? humanAge(s.supervision.ageSecs) + '前' : '无') + '</b>',
    'MODE <b>受控</b>',
  ];
  const half = items.map((x) => '<span>' + x + '</span>').join('');
  train.innerHTML = half + half;
}

function applyPlans(s: Snapshot): void {
  const plans = (s.plans || {}) as Record<string, string | null>;
  let changed = false;
  for (const k of S.GAME_IDS) {
    const g = S.GAMES[k];
    if (!g) continue;
    const p = shortPlan(plans[k] ?? null, k);
    if (p && g.plan !== p) { g.plan = p; changed = true; }
  }
  if (changed) emit('games');
}

// 方案行很长（含活动起止/理由），卡片只留「模式 · 关键锚」。
function shortPlan(raw: string | null, game: string): string | null {
  if (!raw) return null;
  const mode = (raw.match(/^([a-z0-9-]+)/) || [])[1] || '';
  const stage = raw.match(/(TL|CE|LS|AP|SK|PR|CA|GT)-?\d+/i);
  const inst = raw.match(/实例\[([^\]]+)\]/);
  if (game === 'ark' && stage) return (mode ? mode + ' · ' : '') + stage[0] + ' 全刷';
  if (game === 'r1999' && inst) return (mode ? mode + ' · ' : '') + inst[1];
  const cut = raw.split(/[—→]/)[0].trim();
  return cut.length > 24 ? cut.slice(0, 24) + '…' : cut;
}

function applyTopbar(s: Snapshot): void {
  const chip = document.getElementById('modeChip');
  if (chip) { chip.textContent = '本地实况 LIVE · 受控'; chip.classList.add('live'); }
  const core = document.getElementById('coreChip');
  if (core) core.textContent = '受控 · v' + (s.version || '?');
  const beat = document.getElementById('svBeat');
  const dot = document.getElementById('svDot');
  if (beat) {
    if (s.supervision) {
      beat.textContent = 'supervision · ' + humanAge(s.supervision.ageSecs) + '前';
      if (dot) dot.className = 'dot' + (s.supervision.ageSecs < 600 ? '' : ' warn');
    } else {
      beat.textContent = '无心跳文件';
      if (dot) dot.className = 'dot warn';
    }
  }
  const mt = document.getElementById('modeTag');
  if (mt) mt.textContent = 'LOCAL · 本地实况 · 面板受控';
  const pt = document.getElementById('platTag');
  if (pt) pt.textContent = 'GDCore-K2.1 · v' + (s.version || '?') + ' · ' + s.platform + '/' + s.arch;
}

function applyLivePanel(s: Snapshot): void {
  // 运行锁是"这一轮在不在跑"的唯一真身；预演（chain）是面板自己起的 -DryRun，单独一档。
  S.setRun(s.run || null);
  const runAlive = !!(s.run && s.run.alive);
  const busy = s.log ? s.log.ageSecs < 30 : false;
  S.live.state = chain.running ? 'dry-run' : (runAlive ? 'busy' : (busy ? 'busy' : 'idle'));
  const on = (s.procs || []).filter((p) => p.running).map((p) => p.name);
  S.live.alive = on.length ? on.join('+') : 'none';
  S.live.age = s.log ? humanAge(s.log.ageSecs) : '—';
  S.live.action = chain.running ? 'dry-run · pid ' + chain.pid : (runAlive ? 'run · pid ' + (s.run ? s.run.pid : 0) : 'none');
  S.live.hot = false;
  S.live.vcalls = s.vision ? 'calls.jsonl · ' + s.vision.calls + ' 条 · ' + humanAge(s.vision.ageSecs) + '前' : '—';
  emit('live');
}

function applyLastRound(s: Snapshot): void {
  if (s.lastNotice && s.lastNotice.time) {
    const ln = s.lastNotice;
    const first = (ln.text || '').split('\n').map((x) => x.trim()).find((x) => x.length > 0) || '';
    const tag = ln.level === 'ERROR' ? 'mcrit' : (ln.level === 'WARN' ? 'mwarn' : 'mok');
    S.setLastRound('上一轮：<b>' + esc(ln.time) + '</b> · <span class="' + tag + '">' + esc(ln.level) + '</span> · ' + esc(first));
    return;
  }
  const last = (s.history || [])[(s.history || []).length - 1];
  if (last) {
    const tag = last.status === 'ok' ? 'mok' : 'mcrit';
    S.setLastRound('上一轮：<b>' + esc(last.time) + '</b> · <span class="' + tag + '">' + esc(last.game + ' ' + last.status) + '</span> · ' + esc(last.detail));
  }
}

function seedLog(s: Snapshot): void {
  if (streaming || !s.log) return;
  let didReset = false;
  if (s.log.name !== curFile) {
    curFile = s.log.name;
    seen.clear();
    S.resetLog();
    didReset = true;
  }
  if (seen.size > 4000) seen = new Set(s.log.tail);
  const add: { lv: Level; html: string }[] = [];
  for (const raw of s.log.tail) {
    if (!raw || seen.has(raw)) continue;
    seen.add(raw);
    add.push(parseLine(raw));
  }
  S.pushLogs(add);
  // 预演结束后快照会切到这一轮自己的 run-*.log：整页重灌后把结论行补回末尾。
  if (didReset && pendingEnd) { S.pushLog(pendingEnd.lv, pendingEnd.msg); pendingEnd = null; }
}
