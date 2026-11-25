// 状态机：数据与演示轮时间线，自原型 hud.html 内联脚本逐行移植。
// 本模块不碰 DOM：改完状态发总线事件（games/chain/log/decs/sws/pips/theme/live/sched），渲染层订阅。
// live 模式下 GAME_IDS/GAMES/times/门状态全部由 summary（核内 emit-panel-summary）灌进来，不写死两路。
import { dstr, now, rnd, esc, emit, REDUCED } from './util';
import { gateState, timesOf, type SummaryModel } from './summary';
import type { Ev, Game, GameId, GameStatus, Level, LogLine, RunLock, SwitchDef, CaptureView } from './types';

export const GAME_IDS: GameId[] = ['ark', 'r1999'];
export const GAMES: Record<GameId, Game> = {
  ark: { cn: '明日方舟', code: 'ARK', plan: 'event · TL-9 全刷', inc: true, done: 0, status: 'idle',
    steps: ['桌面检查', '接管 MAA', 'StartUp 启动', 'Recruit 公招', 'Infrast 基建', 'Mall 商店', 'Award 领取', 'Fight 作战', '收尾记账'] },
  r1999: { cn: '重返未来：1999', code: 'R1999', plan: 'idle · 日常十项', inc: true, done: 0, status: 'idle',
    steps: ['更新闸', '接管', '起 MuMu', '写实例', '常规作战', '银行购物', '智能均衡刷材料', '领取奖励', '关闭游戏', '活动审计闸'] },
};

export let sel: GameId = 'ark';
export let running = false;
export let follow = true;
export let filter: 'ALL' | Level = 'ALL';
export let drill = false;
export let warnAuto = false;
export let rescueUsed = 0;
export let times: string[] = ['07:30', '19:30'];   // live 由 TIME 行覆盖；演示值只是占位
export let ack1999 = false;
export const shop: Record<GameId, boolean> = { ark: false, r1999: false };
export const stepState: Record<string, string> = {};
export const LOGLINES: LogLine[] = [];
// 日志序号：渲染层据此判断「这次重绘有没有新行」，只给最新一行贴入场动画。
export let logSeq = 0;

// live 数据面：summary 是唯一状态真身；run 是 state\running.json 的只读视图。
export let model: SummaryModel | null = null;
export let run: RunLock | null = null;
let lastSig = '';

export const SW: SwitchDef[] = [
  { label: '重试一次', key: 'retryOnce', on: true },
  { label: '方舟视觉救援', key: 'arkVisionRescue', on: true },
  { label: '模拟器视觉救援', key: 'mumuVisionRescue', on: true },
  { label: '允许领日常奖励', key: 'visionAllowReward', on: false },
  { label: '监督心跳', key: 'supervisor', on: true },
  { label: '保持屏幕常亮', key: 'keepScreenOn', on: true },
  { label: 'toast 通知', key: 'notify.toast', on: true },
  { label: '停滞告警即中止', key: 'abortOnMaaStallWarning', on: false },
];

// 执行现场读数（原型里直写 DOM 的那几枚，收进一个对象走 'live' 事件）。
export const live = {
  state: 'idle', alive: '—', age: '—', action: 'none', hot: false,
  vcalls: 'qwen-vl · 200 · 1.8s',
  capture: { active: false, state: 'paused', updated: 0, error: '', stale: false } as CaptureView,
};
export let lastRoundHtml = '上一轮：<b>今日 19:30 · 全成</b> · 24m12s · ark ok / r1999 ok（history.csv 尾行·演示）';

let evq: Ev[] = [];
let evTimer: number | null = null;

/* ── 时钟与倒计时 ── */
function hhmm(s: string): number {
  const p = s.split(':');
  const h = parseInt(p[0], 10);
  const m = parseInt(p[1], 10);
  return (isNaN(h) ? 0 : h) * 60 + (isNaN(m) ? 0 : m);
}
export function nextWindow(): string {
  if (!times.length) return '--:--:--';
  const d = new Date();
  const mins = d.getHours() * 60 + d.getMinutes() + d.getSeconds() / 60;
  let best: number | null = null;
  for (let add = 0; add < 2; add++) {
    for (const s of times) {
      const t = hhmm(s) + add * 1440;
      if (t > mins && (best === null || t < best)) best = t;
    }
  }
  const left = Math.max(0, Math.round(((best === null ? mins : best) - mins) * 60));
  const p = (n: number): string => (n < 10 ? '0' : '') + n;
  return p(Math.floor(left / 3600)) + ':' + p(Math.floor(left % 3600 / 60)) + ':' + p(left % 60);
}

/* ── 日志（真实行格式；动态 msg 一律转义） ── */
export function pushLog(lv: Level, msg: string): void {
  LOGLINES.push({ lv, html: '<span class="t">[' + dstr() + ' ' + now() + '][' + now() + ']</span><span class="lv ' + lv + '">' + lv + '</span><span class="msg">' + esc(msg) + '</span>' });
  if (LOGLINES.length > 200) LOGLINES.shift();
  logSeq++;
  emit('log');
}

// 只读实况用：日志行已在源头拼好 HTML（保留原时间戳），整批入账只发一次重绘。
export function pushLogs(items: { lv: Level; html: string }[]): void {
  if (!items.length) return;
  for (const it of items) LOGLINES.push(it);
  if (LOGLINES.length > 200) LOGLINES.splice(0, LOGLINES.length - 200);
  logSeq++;
  emit('log');
}
export function resetLog(): void {
  LOGLINES.length = 0;
  logSeq++;
  emit('log');
}
export function setLastRound(html: string): void {
  lastRoundHtml = html;
  emit('sched');
}

/* ── live 数据面：summary（核内状态真身）+ 运行锁 ── */
function inRoundGames(): GameId[] {
  if (!run || !run.alive) return [];
  const g = (run.games || '').trim();
  if (!g || g === 'all') return GAME_IDS.slice();
  return g.split(/[, ]+/).filter(Boolean);
}

// 插件注册表是唯一游戏来源：GAME 行有几路就有几张卡；零插件就是零张。
// 上一轮的"下轮包含"勾选、计划行、当前选择按 id 继承（刷新不该把手上的选择抖掉）。
export function applySummary(m: SummaryModel): void {
  model = m;
  const prevSel = sel;
  const incPrev: Record<string, boolean> = {};
  const planPrev: Record<string, string> = {};
  for (const k of GAME_IDS) {
    if (!GAMES[k]) continue;
    incPrev[k] = GAMES[k].inc;
    planPrev[k] = GAMES[k].plan;
  }
  for (const k of Object.keys(GAMES)) delete GAMES[k];
  GAME_IDS.length = 0;
  const live = inRoundGames();
  for (const g of m.games) {
    const last = m.last.filter((r) => r.game === g.id).slice(-1)[0];
    let status: GameStatus = 'idle';
    if (live.includes(g.id)) status = 'run';
    else if (last) status = last.kind === 'ok' ? 'ok' : (last.kind === 'err' ? 'fail' : 'stopped');
    GAME_IDS.push(g.id);
    GAMES[g.id] = {
      cn: g.short,
      code: g.short.toUpperCase(),
      inc: incPrev[g.id] !== false,
      plan: planPrev[g.id] || '待读判定',
      done: 0,
      status,
      steps: [],
    };
  }
  sel = GAME_IDS.includes(prevSel) ? prevSel : (GAME_IDS[0] || '');

  const t = timesOf(m);
  if (t.length) times = t;

  for (const k of Object.keys(shop)) delete shop[k];
  for (const g of m.games) shop[g.id] = gateState(m, g.id, '商店领空：已标记');
  ack1999 = gateState(m, 'r1999', '活动确认：已确认');

  // 只在协议面真的变了才重画：8 秒轮询不该把面板刷得一直闪。
  const sig = JSON.stringify([m.games, m.time, m.last, m.gates, m.flags]);
  if (sig !== lastSig) {
    lastSig = sig;
    emit('games'); emit('chain'); emit('decs'); emit('sched');
  }
}

// live 开机先把演示数据清空：真身没到之前宁可显"没有注册的插件路"，也不拿演示卡糊住面板。
export function enterLive(): void {
  for (const k of Object.keys(GAMES)) delete GAMES[k];
  GAME_IDS.length = 0;
  sel = '';
  times = [];
  for (const k of Object.keys(shop)) delete shop[k];
  ack1999 = false;
  lastRoundHtml = '';
  live.state = 'idle';
  live.alive = '—';
  live.age = '—';
  live.action = 'none';
  live.vcalls = '—';
  live.capture = { active: false, state: 'paused', updated: 0, error: '', stale: false };
}

// 运行锁：起/停只在这里翻一次牌（日志也只写一次），轮询本身不刷屏。
export function setRun(l: RunLock | null): void {
  const was = !!(run && run.alive);
  run = l;
  const alive = !!(l && l.alive);
  running = alive;
  const live = inRoundGames();
  for (const k of GAME_IDS) {
    const g = GAMES[k];
    if (!g) continue;
    if (alive && live.includes(k)) { g.status = 'run'; g.done = 0; }
    else if (g.status === 'run') g.status = 'idle';
  }
  if (was !== alive) {
    if (alive) pushLog('STEP', '本轮开始 · 锁 pid ' + (l ? l.pid : 0) + ' · tag ' + (l ? (l.tag || '?') : '?') + ' · games ' + (l && l.games ? l.games : 'all'));
    else pushLog('OK', '本轮结束 · 运行锁已释放');
  }
  emit('games'); emit('chain'); emit('live');
}

/* ── 选择/开关类动作 ── */
export function setSel(g: GameId): void { sel = g; emit('games'); emit('chain'); }
export function toggleInc(g: GameId): void {
  GAMES[g].inc = !GAMES[g].inc;
  pushLog('INFO', '选路 -Games：' + g + ' include=' + GAMES[g].inc);
  emit('games');
}
export function setFilter(f: 'ALL' | Level): void { filter = f; emit('log'); }
export function toggleFollow(): void { follow = !follow; emit('log'); }
export function toggleDrill(): void {
  drill = !drill; emit('theme');
  pushLog('INFO', '警戒演练 ' + (drill ? '启动' : '解除'));
}
export function bootDrill(): void { drill = true; emit('theme'); }   // hash 钩子用：静默置位
export function setTimes(t: string[]): void { times = t; emit('sched'); }
export function toggleAck(): void {
  ack1999 = true;
  pushLog('OK', '-Ack1999 已标记 · event-pending 解除');
  emit('decs');
}
export function toggleShop(g: GameId): void {
  shop[g] = !shop[g];
  pushLog('INFO', GAMES[g].code + ' 商店标记=' + shop[g]);
  emit('decs');
}
export function toggleSwitch(key: string): void {
  const s = SW.find((x) => x.key === key);
  if (!s) return;
  s.on = !s.on;
  pushLog('INFO', 'runtime.' + key + '=' + s.on + '（演示写配置）');
  emit('sws');
}

/* ── 任务链步态 ── */
function setStep(g: GameId, i: number, st: string): void {
  stepState[g + ':' + i] = st;
  if (sel === g) emit('chain');
}

/* ── 演示轮时间线 ── */
function buildRound(list: GameId[]): Ev[] {
  const q: Ev[] = [];
  list.forEach((k) => {
    const g = GAMES[k];
    q.push({ t: 'game-start', g: k });
    g.steps.forEach((_s, i) => {
      q.push({ t: 'step', g: k, i, lv: 'STEP', msg: '=== ' + g.code + ' · ' + g.steps[i] + ' ===' });
      if (k === 'ark' && i === 7) {
        q.push({ t: 'fx', g: k, i, lv: 'WARN', msg: 'MAA 日志停滞 340s · stall 判定成立', fx: 'warn-on' });
        q.push({ t: 'fx', g: k, i, lv: 'INFO', msg: '视觉救援 stall1 · 截图问模型（预算 1/2）', fx: 'rescue' });
        q.push({ t: 'fx', g: k, i, lv: 'OK', msg: '救援 done · 作战继续', fx: 'warn-off' });
      }
      if (k === 'r1999' && i === 6) {
        q.push({ t: 'fx', g: k, i, lv: 'WARN', msg: '任务失败：empty-queue · 判定重试一次', fx: 'retry' });
        q.push({ t: 'fx', g: k, i, lv: 'OK', msg: 'Rescued 翻案成功 · empty-queue → ok', fx: 'rescued' });
      }
      q.push({ t: 'step-done', g: k, i });
    });
    q.push({ t: 'game-done', g: k });
  });
  q.push({ t: 'round-done' });
  return q;
}

export function startRound(listIn: GameId[]): void {
  if (running) { pushLog('WARN', '已有轮在跑 · 忽略本次点火'); return; }
  const list = listIn.filter((k) => GAMES[k].inc);
  if (!list.length) { pushLog('WARN', '选路为空 · 无游戏可跑'); return; }
  running = true; rescueUsed = 0; emit('pips');
  list.forEach((k) => {
    GAMES[k].done = 0; GAMES[k].status = 'run';
    for (let i = 0; i < GAMES[k].steps.length; i++) stepState[k + ':' + i] = '';
  });
  emit('games'); emit('chain');
  pushLog('STEP', '=== 每日清体力日常开始(tag=manual, games=' + list.join(',') + ') ===');
  pushLog('INFO', '内核=live 契约=GDCore-K2.1 · 演示时间线加速');
  evq = buildRound(list);
  live.state = 'busy'; live.alive = 'true'; live.action = 'none'; emit('live');
  evTimer = window.setInterval(tickEvent, REDUCED ? 200 : 850);
}

function tickEvent(): void {
  const e = evq.shift();
  if (!e) { finishRound(false); return; }
  if (e.lv && e.msg) pushLog(e.lv, e.msg);
  if (e.t === 'game-start' && e.g) { sel = e.g; emit('games'); emit('chain'); }
  if (e.t === 'step' && e.g && e.i !== undefined) {
    setStep(e.g, e.i, 'run');
    live.age = Math.round(rnd(2, 40)) + 's'; emit('live');
  }
  if (e.t === 'step-done' && e.g && e.i !== undefined) {
    setStep(e.g, e.i, 'done');
    GAMES[e.g].done = e.i + 1; emit('games');
  }
  if (e.t === 'fx') {
    if (e.fx === 'warn-on') { warnAuto = true; live.hot = true; emit('theme'); emit('live'); }
    if (e.fx === 'warn-off') { warnAuto = false; live.hot = false; emit('theme'); emit('live'); }
    if (e.fx === 'rescue') {
      rescueUsed = 1; emit('pips');
      live.vcalls = 'qwen-vl · 200 · 1.8s · 今日 4  calls'; emit('live');
    }
    if (e.fx === 'retry' && e.g && e.i !== undefined) setStep(e.g, e.i, 'run retry');
    if (e.fx === 'rescued' && e.g && e.i !== undefined) setStep(e.g, e.i, 'run rescued');
  }
  if (e.t === 'game-done' && e.g) {
    GAMES[e.g].status = 'ok';
    pushLog('OK', GAMES[e.g].code + ' 收尾 · status=ok · 写 history.csv');
    emit('games');
  }
  if (e.t === 'round-done') finishRound(false);
}

function finishRound(interrupted: boolean): void {
  if (evTimer !== null) { window.clearInterval(evTimer); evTimer = null; }
  running = false; warnAuto = false; emit('theme');
  live.hot = false; live.state = 'idle'; live.alive = '—'; live.age = '—'; emit('live');
  if (interrupted) { pushLog('WARN', '本轮中断汇总 · 已写 history.csv status=stopped（演示）'); emit('games'); return; }
  pushLog('OK', '本轮汇总：全成 · 通知 toast+webhook · 演示');
  lastRoundHtml = '上一轮：<b>刚刚 · 全成</b> · 演示时长 ' + Math.round(rnd(18, 26)) + 'm · ark ok / r1999 ok（history.csv·演示）';
  emit('sched'); emit('games');
}

export function stopRound(): void {
  if (!running) { pushLog('INFO', '无在跑轮 · Stop-ScheduledTask 空操作'); return; }
  evq = []; finishRound(true);
  GAME_IDS.forEach((k) => { if (GAMES[k].status === 'run') GAMES[k].status = 'stopped'; });
  pushLog('WARN', '收到 -Stop · 轮中断 · Stop-ScheduledTask');
  emit('games');
}