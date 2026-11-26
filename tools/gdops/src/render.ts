// 渲染层：订阅 state 的总线事件重绘各面板；交互回调只调 state 动作与 bridge，不持有业务状态。
import { $, pad, on, esc, now, dstr, sanitize, blip } from './util';
import * as S from './state';
import { openModal } from './modal';
import type { Bridge } from './bridge';
import type { GameId, Level, RuntimeFlags } from './types';
import { ackFlagFor, chainRowsFor, shopFlagsFor, registeredOf } from './summary';
import { kick } from './live';
import { syncChainButtons } from './chain';

let bridge: Bridge;

/* ── 游戏卡 ── */
function renderGames(): void {
  const el = $('#games'); el.innerHTML = '';
  if (S.GAME_IDS.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'gcard';
    empty.innerHTML = '<div class="r1"><span class="dot idle"></span><span class="nm">没有注册的插件路</span><span class="code">PLUGINS 0</span><span class="st">idle</span></div>' +
      '<div class="r2"><span class="plan">lib/*-module.ps1 一个都没有：装上 ark / r1999 插件包后这边自动长卡</span></div>' +
      '<div class="r3"><span class="inc">下轮包含</span><button class="btn mini run" disabled>只跑这一路</button></div>';
    el.appendChild(empty);
    return;
  }
  S.GAME_IDS.forEach((k) => {
    const g = S.GAMES[k];
    if (!g) return;
    const card = document.createElement('div');
    card.className = 'gcard' + (S.sel === k ? ' sel' : '');
    const dotCls = g.status === 'idle' ? 'idle' : (g.status === 'fail' ? 'crit' : (g.status === 'stopped' ? 'warn' : ''));
    const frac = g.steps.length ? Math.round(g.done / g.steps.length * 100) + '%' : '—';
    const fracText = g.steps.length ? g.done + '/' + g.steps.length : 'live';
    card.innerHTML = '<div class="r1"><span class="dot ' + dotCls + '"></span><span class="nm">' + esc(g.cn) + '</span><span class="code">' + esc(g.code) + '</span><span class="st">' + esc(g.status) + '</span></div>' +
      '<div class="r2"><span class="plan">' + esc(g.plan) + '</span><span class="bar"><i style="width:' + frac + '"></i></span><span class="frac">' + esc(fracText) + '</span></div>' +
      '<div class="r3"><span class="inc' + (g.inc ? ' on' : '') + '" data-inc="' + k + '"><i></i>下轮包含</span><button class="btn mini run" data-run="' + k + '">只跑这一路</button></div>';
    const rb = card.querySelector('[data-run]') as HTMLButtonElement | null;
    if (rb) {
      rb.disabled = S.running;
      rb.title = S.running ? '已有一轮在跑（等它结束或先点「停」）' : ('起一轮：' + g.cn + '（task 优先；无 task 走自提权入口）');
    }
    card.addEventListener('click', async (e) => {
      const t = e.target as HTMLElement;
      if (t.dataset.run) { await runOne(t.dataset.run as GameId); return; }
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

// 只跑这一路：live 走内核真点火（一次点击即确认——按钮本身已写明只跑哪一路）；demo 走演示时间线。
async function runOne(k: GameId): Promise<void> {
  if (bridge.mode !== 'live') { S.startRound([k]); return; }
  if (S.running) { S.pushLog('WARN', '已有一轮在跑 · 忽略本次点火'); return; }
  S.pushLog('STEP', '点火：只跑 ' + k + '（内核通道）');
  const r = await bridge.startRound(k);
  if (r.ok) S.pushLog('OK', '已发起（' + (r.carrier === 'task' ? '计划任务' : '自提权入口') + '）· ' + (r.note || 'pid ' + (r.pid || 0)));
  else S.pushLog('ERROR', '点火失败：' + (r.error || '未知错误'));
  kick();
}

/* ── 任务链 ── */
function renderChain(): void {
  syncChainButtons();   // 真轮的「停」要随运行锁翻牌，不只跟预演
  const el = $('#chain'); el.innerHTML = '';
  const g = S.GAMES[S.sel];
  if (!g) {
    $('#chainEn').textContent = 'CHAIN · —';
    const e = document.createElement('div');
    e.className = 'step';
    e.innerHTML = '<span class="n">—</span><span>没有注册的插件路：装上插件包后这里显示门/探针/最近一轮的实读</span>';
    el.appendChild(e);
    return;
  }
  $('#chainEn').textContent = 'CHAIN · ' + g.code;

  // live：没有逐步骤，列门/探针/最近一轮 + 本轮 pid（不冒充阶段进度）。
  if (bridge.mode === 'live') {
    if (!S.model) {
      const e = document.createElement('div');
      e.className = 'step';
      e.innerHTML = '<span class="n">!</span><span>内核 summary 读不出（panel-cs.exe / 插件注册表）</span>';
      el.appendChild(e);
      return;
    }
    const rows = chainRowsFor(S.model, S.sel, S.run);
    if (!rows.length) {
      const e = document.createElement('div');
      e.className = 'step';
      e.textContent = '这一路还没有门/探针/历史读数（跑一轮后落盘）';
      el.appendChild(e);
      return;
    }
    rows.forEach((r, i) => {
      const d = document.createElement('div');
      d.className = 'step ' + r.cls;
      d.innerHTML = '<span class="n">' + pad(i + 1) + '</span><span>' + esc(r.text) + '</span>' + (r.badge ? '<span class="badge">' + esc(r.badge) + '</span>' : '');
      el.appendChild(d);
    });
    return;
  }

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
function emptyDec(text: string): void {
  const e = document.createElement('div');
  e.className = 'dec empty';
  e.textContent = text;
  $('#decs').appendChild(e);
}

function decCard(title: string, why: string, acts: { label: string; attr: string; value: string; disabled?: boolean }[]): void {
  const d = document.createElement('div');
  d.className = 'dec';
  d.innerHTML = '<div class="t">' + esc(title) + '</div><div class="why">' + esc(why) + '</div><div class="acts">' +
    acts.map((a) => '<button class="btn mini" ' + a.attr + '="' + esc(a.value) + '"' + (a.disabled ? ' disabled title="有一轮在跑：门旗标等本轮结束再落"' : '') + '>' + esc(a.label) + '</button>').join('') +
    '</div>';
  $('#decs').appendChild(d);
}

async function markFlag(flag: string): Promise<void> {
  if (S.running) { S.pushLog('WARN', '本轮在跑：门旗标等结束再落'); return; }
  S.pushLog('STEP', '落门：-' + flag + ' -DryRun -NoElevate（只写字号门，不点火）');
  const r = await bridge.markFlag(flag);
  if (r.ok) S.pushLog('OK', '-' + flag + ' 已写入（干跑只落门状态）');
  else S.pushLog('ERROR', '-' + flag + ' 没写成：exit ' + (r.code ?? '?') + (r.error ? ' · ' + r.error : ''));
  for (const l of (r.lines || []).slice(-6)) S.pushLog(r.ok ? 'INFO' : 'WARN', l);
  kick();
}

function renderDecsLive(): void {
  const el = $('#decs'); el.innerHTML = '';
  const m = S.model;
  if (!m) { emptyDec('内核 summary 读不出：决策要读核内的门状态'); return; }
  const ids = m.games.map((g) => g.id);
  if (!ids.length) { emptyDec('没有注册的插件路：装插件后才有关可确认'); return; }

  // 主线确认：旗标取入口声明（单插件树 Ack；合并树 -Ack1999 兜底，见 summary.ts）。
  const ackFlag = ackFlagFor(m);
  const ackGame = ids.includes('r1999') ? 'r1999' : ids[0];
  if (ackFlag && !S.ack1999) {
    decCard(ackGame + ' 主线未确认 · event-pending',
      '确认后写 -' + ackFlag + '；干跑只落门、不点火。',
      [
        { label: '确认主线已打 (-' + ackFlag + ')', attr: 'data-ackflag', value: ackFlag, disabled: S.running },
        { label: '看审计证据', attr: 'data-evi', value: '1' },
      ]);
  }

  for (const k of ids) {
    const g = S.GAMES[k];
    const code = g ? g.code : k.toUpperCase();
    const sf = shopFlagsFor(m, k);
    if (!sf) {
      decCard(code + ' 商店领空：入口未声明旗标',
        '这一路没有 -ShopDone 类开关；面板不造按钮（要开先在入口声明）。', []);
      continue;
    }
    const marked = !!S.shop[k];
    decCard(code + ' 商店领空标记：' + (marked ? '已领空' : '未标记'),
      '标记后活动轮停商店队列；干跑只落门、不点火。',
      [
        marked
          ? { label: '撤销 (-' + sf.reset + ')', attr: 'data-flag', value: sf.reset, disabled: S.running }
          : { label: '标记领空 (-' + sf.done + ')', attr: 'data-flag', value: sf.done, disabled: S.running },
      ]);
  }
  if (!el.children.length) emptyDec('当前无待决策 · 门全绿');

  el.querySelectorAll<HTMLButtonElement>('button[data-ackflag],button[data-flag]').forEach((b) => {
    b.addEventListener('click', () => {
      const flag = b.dataset.ackflag || b.dataset.flag;
      if (flag) void markFlag(flag);
    });
  });
  el.querySelectorAll<HTMLButtonElement>('button[data-evi]').forEach((b) => {
    b.addEventListener('click', () => { void bridge.auditEvidence().then((h) => openModal('活动审计证据', h)); });
  });
}

function renderDecs(): void {
  if (bridge.mode === 'live') { renderDecsLive(); return; }
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
}

/* ── 快捷开关 ── */
// live 从 settings.json 实读，单键保存后重新读数；写的是下一轮配置，不冒充当前进程已热切换。
let runtimeFlags: RuntimeFlags = {};
let runtimePresent = false;
let runtimeTried = false;
let runtimeLoading = false;
let runtimeBusy = '';

async function loadRuntimeFlags(): Promise<boolean> {
  if (runtimeLoading) return false;
  runtimeLoading = true;
  runtimeTried = true;
  renderSws();
  try {
    const s = await bridge.assistState();
    if (s) {
      runtimePresent = s.settingsPresent;
      runtimeFlags = s.runtimeFlags || {};
      return true;
    }
    S.pushLog('ERROR', '右栏开关读数失败：assist_state 没有返回');
  } catch {
    S.pushLog('ERROR', '右栏开关读数失败');
  } finally {
    runtimeLoading = false;
    renderSws();
  }
  return false;
}

async function saveRuntimeFlag(key: string): Promise<void> {
  if (runtimeBusy || runtimeLoading) return;
  const old = runtimeFlags[key];
  if (typeof old !== 'boolean') return;
  runtimeBusy = key;
  renderSws();
  try {
    const path = key === 'notify.toast' ? key : 'runtime.' + key;
    const r = await bridge.assistSavePlan([{ key: path, value: !old }]);
    const label = S.SW.find((s) => s.key === key)?.label || key;
    if (!r.ok) {
      S.pushLog('ERROR', '开关保存失败：' + key + ' · ' + (r.error || r.note || '未知错误'));
      if (r.lines?.length) for (const l of r.lines.slice(-6)) S.pushLog('WARN', l);
    } else {
      S.pushLog('OK', label + '：配置已保存（下一轮／下一监督进程按新值运行）');
      if (r.note) S.pushLog('INFO', r.note);
    }
    // 后端可能已写盘、只是写后回读校验失败；失败分支也必须重新读数，不能继续拿旧值冒充现状。
    if (await loadRuntimeFlags()) {
      const desired = !old;
      const label = S.SW.find((s) => s.key === key)?.label || key;
      if (runtimeFlags[key] === desired) {
        if (!r.ok) S.pushLog('WARN', label + '：保存回执失败，但回读已是要写入的值；请以回读值为准');
      } else if (r.ok) {
        S.pushLog('WARN', label + '：保存回执成功，但回读值不一致，请点助手页刷新核对');
      } else {
        S.pushLog('WARN', label + '：保存回执失败，回读也不是目标值；请点助手页刷新核对');
      }
    } else {
      // 写后没能重新读数就把状态降为未知，不能把旧值继续冒充已保存后的现值。
      runtimeFlags = {};
      S.pushLog('ERROR', label + '：写后回读失败；当前值标为未知，请点助手页刷新核对');
    }
  } catch {
    S.pushLog('ERROR', '开关保存失败：' + key);
  } finally {
    runtimeBusy = '';
    renderSws();
  }
}

function renderSws(): void {
  const el = $('#sws'); el.innerHTML = '';
  if (bridge.mode === 'live' && !runtimeTried && !runtimeLoading) void loadRuntimeFlags();
  S.SW.forEach((s) => {
    const d = document.createElement('div');
    const unknown = bridge.mode === 'live' && typeof runtimeFlags[s.key] !== 'boolean';
    const on = bridge.mode === 'live' ? runtimeFlags[s.key] === true : s.on;
    const waiting = bridge.mode === 'live' && (runtimeLoading || runtimeBusy === s.key);
    d.className = 'sw' + (unknown || waiting ? ' ro' : (on ? ' on' : ''));
    d.dataset.key = s.key;
    d.dataset.value = unknown ? 'unknown' : String(on);
    d.setAttribute('role', 'button');
    d.tabIndex = 0;
    d.setAttribute('aria-disabled', String(unknown || waiting || (bridge.mode === 'live' && !runtimePresent)));
    d.setAttribute('aria-pressed', String(on));
    d.title = bridge.mode !== 'live' ? '演示开关'
      : unknown ? (runtimePresent ? '配置里没有可识别的布尔值，不猜默认值' : 'settings.json 不在')
      : waiting ? '正在读取／保存' : '保存 settings.json；下一轮／下一监督进程生效';
    d.innerHTML = '<i></i><span>' + esc(s.label) + '</span><span class="key">' +
      (unknown ? '未知' : waiting ? '忙' : on ? '开' : '关') + '</span>';
    const click = (): void => {
      if (bridge.mode !== 'live') {
        void bridge.setSwitch(s.key, !s.on);
        S.toggleSwitch(s.key);
      } else if (!runtimePresent || unknown || waiting) {
        S.pushLog('WARN', s.label + '：当前没有可保存的真实布尔值');
      } else {
        void saveRuntimeFlag(s.key);
      }
    };
    d.addEventListener('click', click);
    d.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); click(); }
    });
    el.appendChild(d);
  });
  if (bridge.mode === 'live') {
    const n = document.createElement('div');
    n.className = 'sw ro';
    n.innerHTML = '<i></i><span>保存后下一轮生效</span><span class="key">settings.json</span>';
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
  const c = S.live.capture;
  const labels: Record<string, string> = {
    ready: '实时采集中', dark: '画面偏暗', connecting: '正在连接画面', paused: '实时画面已暂停',
    missing: '没有可用窗口', minimized: '窗口最小化', offline: '设备画面不可用',
    failed: '捕获失败', unsupported: '当前环境不支持',
  };
  const hasFrame = c.updated > 0;
  const img = $('#previewImg') as HTMLImageElement;
  img.hidden = !hasFrame;
  $('#previewEmpty').hidden = hasFrame;
  $('#previewTag').textContent = '实时游戏画面 · ' + (c.stale ? '帧已过期' : (labels[c.state] || c.state));
  const stamp = c.updated ? new Date(c.updated).toLocaleTimeString('zh-CN', { hour12: false }) : '—';
  const fps = c.fps && c.fps > 0 ? ' · ' + c.fps.toFixed(1) + ' fps' : '';
  const frameText = hasFrame
    ? (c.source === 'adb' ? 'M9A · ADB 设备画面' : '端游窗口画面') + fps + ' · ' + stamp
    : (bridge.mode === 'live' ? '等待游戏路读数；不占用前台、不注入操作' : '浏览器演示模式不捕获真实游戏画面');
  $('#previewCap').textContent = hasFrame && (c.error || c.note)
    ? frameText + ' · ' + (c.error || c.note)
    : c.error || c.note || frameText;
  const metrics = [
    c.captureMs !== undefined ? '捕获 ' + Math.round(c.captureMs) + 'ms' : '',
    c.encodedBytes !== undefined ? '画面 ' + (c.encodedBytes / 1024).toFixed(1) + 'KiB' : '',
  ].filter(Boolean).join(' · ');
  $('#previewCap').title = [($('#previewCap').textContent || ''), metrics].filter(Boolean).join(' · ');
  const btn = $('#previewBtn') as HTMLButtonElement;
  btn.disabled = bridge.mode !== 'live' || !S.sel;
  btn.textContent = c.active ? '暂停画面' : '开启画面';
  btn.classList.toggle('on', c.active);
  $('#vframe').classList.toggle('capture-error', !!c.error && c.active);
  $('#vframe').classList.toggle('capture-live', hasFrame && !c.stale && !c.error);
  $('#vframe').dataset.capture = c.state;
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
  $('#t0').textContent = S.times[0] || '--:--';
  $('#t1').textContent = S.times[1] || '--:--';
  if (bridge.mode === 'live') {
    const m = S.model;
    if (m) {
      const reg = registeredOf(m);
      const names = m.time.map((r) => {
        const t = (r.text.match(/^(\d{1,2}:\d{2})/) || [])[1] || r.text;
        return t + (/未注册/.test(r.text) ? '·未注册' : '·已注册');
      });
      $('#taskNames').textContent = names.length ? names.join(' / ') : '没有时刻（config 里 schedule.times 为空）';
      $('#taskNames').title = '注册 ' + reg.ok + '/' + reg.total + ' · TIME 行来自核内计划任务查询';
    } else {
      $('#taskNames').textContent = '内核 summary 读不出';
    }
    $('#lastRound').innerHTML = sanitize(S.lastRoundHtml);
    return;
  }
  $('#taskNames').textContent = 'GameDaily-' + (S.times[0] || '----').replace(':', '') + ' / -' + (S.times[1] || '----').replace(':', '') + ' / -Manual';
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