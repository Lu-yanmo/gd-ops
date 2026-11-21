// 渲染层：订阅 state 的总线事件重绘各面板；交互回调只调 state 动作与 bridge，不持有业务状态。
import { $, pad, on, esc, now, dstr, sanitize, blip } from './util';
import * as S from './state';
import { openModal } from './modal';
import type { Bridge } from './bridge';
import type { GameId, Level } from './types';

let bridge: Bridge;

/* ── 游戏卡 ── */
function renderGames(): void {
  const el = $('#games'); el.innerHTML = '';
  S.GAME_IDS.forEach((k) => {
    const g = S.GAMES[k];
    const card = document.createElement('div');
    card.className = 'gcard' + (S.sel === k ? ' sel' : '');
    const dotCls = g.status === 'idle' ? 'idle' : (g.status === 'fail' ? 'crit' : (g.status === 'stopped' ? 'warn' : ''));
    card.innerHTML = '<div class="r1"><span class="dot ' + dotCls + '"></span><span class="nm">' + esc(g.cn) + '</span><span class="code">' + esc(g.code) + '</span><span class="st">' + esc(g.status) + '</span></div>' +
      '<div class="r2"><span class="plan">' + esc(g.plan) + '</span><span class="bar"><i style="width:' + (g.done / g.steps.length * 100) + '%"></i></span><span class="frac">' + g.done + '/' + g.steps.length + '</span></div>' +
      '<div class="r3"><span class="inc' + (g.inc ? ' on' : '') + '" data-inc="' + k + '"><i></i>下轮包含</span><button class="btn mini run" data-run="' + k + '">只跑这一路</button></div>';
    if (bridge.mode === 'live') {
      const rb = card.querySelector('[data-run]') as HTMLButtonElement | null;
      if (rb) { rb.disabled = true; rb.title = '只读阶段：点火未接入（下一阶段）'; }
    }
    card.addEventListener('click', (e) => {
      const t = e.target as HTMLElement;
      if (bridge.mode === 'live' && (t.dataset.run || t.dataset.inc || (t.parentElement && t.parentElement.dataset.inc))) {
        S.pushLog('WARN', '只读阶段：点火/选路写入未接入（下一阶段）');
        return;
      }
      if (t.dataset.run) { S.startRound([t.dataset.run as GameId]); return; }
      const inc = t.dataset.inc || (t.parentElement ? t.parentElement.dataset.inc : undefined);
      if (inc) { S.toggleInc(inc as GameId); return; }
      S.setSel(k);
    });
    el.appendChild(card);
  });
  const slot = document.createElement('div');
  slot.className = 'gslot'; slot.textContent = '+ 扩展路槽 · 模块注册表驱动';
  el.appendChild(slot);
}

/* ── 任务链 ── */
function renderChain(): void {
  const g = S.GAMES[S.sel];
  $('#chainEn').textContent = 'CHAIN · ' + g.code;
  const el = $('#chain'); el.innerHTML = '';
  g.steps.forEach((s, i) => {
    const st = S.stepState[S.sel + ':' + i] || '';
    let badge = '';
    if (S.sel === 'ark' && i === 7) badge = '<span class="badge">retry/rescue 位</span>';
    if (S.sel === 'r1999' && i === 6) badge = '<span class="badge">Rescued 翻案位</span>';
    const d = document.createElement('div');
    d.className = 'step ' + st;
    d.innerHTML = '<span class="n">' + pad(i + 1) + '</span><span>' + esc(s) + '</span>' + badge;
    el.appendChild(d);
  });
}

/* ── 日志 ── */
let seenLogSeq = -1;
function renderLog(): void {
  const el = $('#log');
  const rows = S.LOGLINES.filter((l) => S.filter === 'ALL' || l.lv === S.filter);
  // l.html 在 pushLog 源头已对动态 msg 做 esc()，其余为固定 span 骨架与枚举级别名。
  el.innerHTML = rows.map((l) => '<div class="line ' + l.lv + '">' + l.html + '</div>').join('');
  // 只有「真来了新行」且过滤后末行就是最新行时，才给那一行贴一次性入场动画。
  if (S.logSeq !== seenLogSeq) {
    seenLogSeq = S.logSeq;
    const newest = S.LOGLINES[S.LOGLINES.length - 1];
    if (rows.length && rows[rows.length - 1] === newest) el.lastElementChild?.classList.add('new');
  }
  if (S.follow) el.scrollTop = el.scrollHeight;
  $('#followChip').classList.toggle('on', S.follow);
}

/* ── 待决策 ── */
function renderDecs(): void {
  const el = $('#decs'); el.innerHTML = '';
  if (!S.ack1999) {
    const d = document.createElement('div'); d.className = 'dec';
    d.innerHTML = '<div class="t">1999 主线未确认 · event-pending</div><div class="why">mainStoryRequiresAck=true · 本轮判定 ok 后等人确认；审计闸视觉判 yes 才会自动 ack。</div><div class="acts"><button class="btn mini" data-a="ack">确认主线已打 (-Ack1999)</button><button class="btn mini" data-a="evi">看审计证据</button></div>';
    el.appendChild(d);
  }
  S.GAME_IDS.forEach((k) => {
    const d = document.createElement('div'); d.className = 'dec shop';
    d.innerHTML = '<div class="t">' + esc(S.GAMES[k].code) + ' 商店领空标记：' + (S.shop[k] ? '已领空' : '未标记') + '</div><div class="why">shopDoneStopsEvent · 标记后活动轮停商店队列</div><div class="acts"><button class="btn mini" data-shop="' + k + '">' + (S.shop[k] ? '撤销 (-ShopReset)' : '标记领空 (-ShopDone)') + '</button></div>';
    el.appendChild(d);
  });
  if (S.ack1999 && S.shop.ark && S.shop.r1999) {
    const e = document.createElement('div'); e.className = 'dec empty'; e.textContent = '当前无待决策 · 门全绿';
    el.appendChild(e);
  }
  el.querySelectorAll('button').forEach((b) => {
    b.addEventListener('click', () => {
      if (b.dataset.a === 'ack') { S.toggleAck(); void bridge.ack1999(); }
      if (b.dataset.a === 'evi') { void bridge.auditEvidence().then((h) => openModal('活动审计证据', h)); }
      if (b.dataset.shop) {
        const k = b.dataset.shop as GameId;
        S.toggleShop(k); void bridge.shopMark(k, S.shop[k]);
      }
    });
  });
  if (bridge.mode === 'live') {
    // 审计证据是只读查看，留着；确认/商店标记是写路径，只读阶段禁掉。
    el.querySelectorAll<HTMLButtonElement>('button[data-a="ack"], button[data-shop]').forEach((b) => {
      b.disabled = true;
      b.title = '只读阶段：写入未接入（下一阶段）';
    });
    const n = document.createElement('div');
    n.className = 'dec empty';
    n.textContent = '只读阶段：确认 / 商店标记写入未接入';
    el.appendChild(n);
  }
}

/* ── 快捷开关 ── */
function renderSws(): void {
  const el = $('#sws'); el.innerHTML = '';
  S.SW.forEach((s) => {
    const d = document.createElement('div');
    d.className = 'sw' + (s.on ? ' on' : '') + (bridge.mode === 'live' ? ' ro' : '');
    if (bridge.mode === 'live') d.title = '只读阶段：写配置未接入（下一阶段）';
    d.innerHTML = '<i></i><span>' + esc(s.label) + '</span><span class="key">' + esc(s.key) + '</span>';
    d.addEventListener('click', () => {
      if (bridge.mode === 'live') { S.pushLog('WARN', '只读阶段：开关写配置未接入（下一阶段）'); return; }
      void bridge.setSwitch(s.key, !s.on);
      S.toggleSwitch(s.key);
    });
    el.appendChild(d);
  });
  if (bridge.mode === 'live') {
    const n = document.createElement('div');
    n.className = 'sw ro';
    n.innerHTML = '<i></i><span>只读阶段 · 开关写配置未接入</span><span class="key">read-only</span>';
    el.appendChild(n);
  }
}

/* ── 现场/主题/预算 ── */
// 读数变更才闪一下（WAAPI 一次性），值没变不重复动画。
function setV(sel: string, v: string): void {
  const el = $(sel);
  if (el.textContent !== v) { el.textContent = v; blip(el); }
}
function renderLive(): void {
  setV('#svState', S.live.state);
  setV('#svAlive', S.live.alive);
  setV('#svAge', S.live.age);
  setV('#svAction', S.live.action);
  $('#vframe').classList.toggle('hot', S.live.hot);
  $('#vcap').textContent = S.live.vcap;
  $('#vcalls').textContent = S.live.vcalls;
}
function paintPips(): void {
  $('#pips').innerHTML = '<i class="' + (S.rescueUsed > 0 ? 'used' : '') + '"></i><i class="' + (S.rescueUsed > 1 ? 'used' : '') + '"></i><span class="v" style="margin-left:4px">' + (2 - S.rescueUsed) + '/2</span>';
}
function paintTheme(): void {
  document.body.classList.toggle('alert', S.drill);
  document.body.classList.toggle('warn', !S.drill && S.warnAuto);
  $('#alertLevel').textContent = S.drill ? 'RED·演练' : (S.warnAuto ? 'AMBER·救援' : 'GREEN');
  $('#alertDot').className = 'dot' + (S.drill ? ' crit' : (S.warnAuto ? ' warn' : ''));
}

/* ── 调度/时钟/跑马灯 ── */
function renderSched(): void {
  $('#t0').textContent = pad(S.times[0]) + ':00';
  $('#t1').textContent = pad(S.times[1]) + ':00';
  $('#taskNames').textContent = 'GameDaily-' + pad(S.times[0]) + '00 / -' + pad(S.times[1]) + '00 / -Manual';
  $('#lastRound').innerHTML = sanitize(S.lastRoundHtml);   // 内部模板（<b>+数值），仍过白名单清洗
}
export function tickClock(): void {
  $('#clock').textContent = now();
  $('#dateline').textContent = dstr() + ' · UTC+8';
  const cd = S.nextWindow();
  $('#nextWin').textContent = 'T-' + cd;
  const big = $('#cdBig');
  // 每秒只换数字不闪（等宽字体已保不跳）；整分钟 crossing 才给一次微闪当拍点。
  if (big.textContent !== cd) {
    big.textContent = cd;
    if (cd.endsWith(':00')) blip(big, 0.5);
  }
}
const TICK = 'TODAY <b>2 轮</b>|OK <b>100%</b>|KERNEL <b>live</b>|GATES <b>56/46/90/43</b>|PS51 <b>TIP-only</b>|VISION <b>3 calls</b>|SUPERVISOR <b>beats 30</b>|HISTORY <b>csv 尾5</b>';
function renderTicker(): void {
  const half = TICK.split('|').map((s) => '<span>' + s + '</span>').join('');
  $('#train').innerHTML = half + half;
}

/* ── 装配 ── */
export function initRender(b: Bridge): void {
  bridge = b;
  on('games', renderGames); on('chain', renderChain); on('log', renderLog);
  on('decs', renderDecs); on('sws', renderSws); on('pips', paintPips);
  on('theme', paintTheme); on('live', renderLive); on('sched', renderSched);
  document.querySelectorAll<HTMLElement>('.fchip[data-f]').forEach((c) => {
    c.addEventListener('click', () => {
      S.setFilter(c.dataset.f as 'ALL' | Level);
      document.querySelectorAll('.fchip[data-f]').forEach((x) => x.classList.toggle('on', x === c));
    });
  });
  $('#followChip').addEventListener('click', () => S.toggleFollow());
}
export function renderAll(): void {
  renderGames(); renderChain(); renderLog(); renderDecs(); renderSws();
  paintPips(); paintTheme(); renderLive(); renderSched(); tickClock(); renderTicker();
}