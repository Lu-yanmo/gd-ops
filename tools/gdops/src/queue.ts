// 左列「任务队列」：跟随「游戏路」选中切换 MAA 任务清单 / M9A 实例清单。
// 判定仍在核内：勾选写的是 settings 的两枚名单（下一轮由内核重建 MAA 队列），
// 这里只做收值、两击确认与回执落日志。齿轮 → 右侧二级框（taskcfg.ts）看/改这一条的全量参数。
import { $, esc, on } from './util';
import * as S from './state';
import type { Bridge } from './bridge';
import type { ActResult, AssistState, MaaState, MaaTask, M9aState } from './types';
import { openMaaTaskDrawer, openM9aDrawer } from './taskcfg';
import { closeDrawer, drawerKey, drawerOpen } from './drawer';

let bridge: Bridge;
let data: AssistState | null = null;
let loading = false;
let busy = false;
let armed: Record<string, number> = {};
let wired = false;

export function initQueue(b: Bridge): void {
  bridge = b;
  on('chain', renderQueue);         // 选路切换会发 'chain'（state.setSel）
  if (!wired) {
    wired = true;
    $('#queue').addEventListener('click', onClick);
  }
  void reload();
}

export async function reload(): Promise<void> {
  if (loading) return;
  loading = true;
  renderQueue();
  data = await bridge.assistState();
  loading = false;
  renderQueue();
}

/* ── 渲染 ── */

function chip(label: string, on: boolean, yes: string): string {
  return '<span class="qChip' + (on ? ' on' : '') + '">' + esc(label) + ' <b>' + esc(on ? yes : '否') + '</b></span>';
}

function renderQueue(): void {
  const el = $('#queue');
  if (!el) return;
  const wantM9a = S.sel === 'r1999';
  const en = $('#qEn');
  if (en) en.textContent = 'QUEUE · ' + (wantM9a ? 'M9A' : 'MAA');
  if (!bridge) return;
  if (bridge.mode !== 'live') {
    el.innerHTML = '<div class="qEmpty">演示数据不带 MAA / M9A 读数：装进 Tauri 壳里才读现场。</div>';
    return;
  }
  if (!data) {
    el.innerHTML = '<div class="qEmpty">' + (loading ? '读数中…' : '读数没回来：点「刷新」再试') + '</div>';
    return;
  }
  if (!data.settingsPresent) {
    el.innerHTML = '<div class="qEmpty warn">config/settings.json 不在：先跑 setup.ps1</div>';
    return;
  }
  el.innerHTML = (wantM9a ? m9aHtml(data.m9a) : maaHtml(data.maa)) + footHtml(wantM9a);
}

function footHtml(m9a: boolean): string {
  const b = busy ? ' disabled' : '';
  return '<div class="qFoot">' +
    (m9a
      ? '<button class="btn mini" data-act="mumu-start" data-cf="再点一次：启动 MuMu"' + b + '>启动 MuMu</button>' +
        '<button class="btn mini" data-act="mumu-stop" data-cf="再点一次：关闭 MuMu"' + b + '>关闭 MuMu</button>'
      : '<button class="btn mini" data-all="1"' + b + '>全选</button>' +
        '<button class="btn mini" data-all="0"' + b + '>清空</button>') +
    '<button class="btn mini" data-reload="1"' + b + '>刷新</button>' +
    '</div>';
}

function maaHtml(m: MaaState | null): string {
  if (!m) return '<div class="qEmpty warn">读不出 MAA 这一块（assist_state 没回数据）</div>';
  let head = '<div class="qHead">' + chip('MAA', m.running, '在') + chip('配置', m.configReady, '在位') +
    (m.gameRunning ? chip('端游', true, '在') : '') + '</div>';
  if (m.tasksError) return head + '<div class="qEmpty warn">' + esc(m.tasksError) + '</div>';
  if (!m.tasks.length) return head + '<div class="qEmpty">MAA 队列是空的</div>';
  const seen = new Set<string>();
  const rows = m.tasks.map((t) => {
    const first = !seen.has(t.type.toLowerCase());
    seen.add(t.type.toLowerCase());
    return rowHtml(t, first);
  }).join('');
  return head + rows;
}

function rowHtml(t: MaaTask, showCk: boolean): string {
  const on = t.on;
  const dis = busy ? ' disabled' : '';
  // 同一 TaskType 可能有多条（理智作战/剩余理智）：勾选按类型统一，只在第一条给可点的框。
  const ck = showCk
    ? '<button class="qCk' + (on ? ' on' : '') + '" data-k="' + esc(t.type) + '" data-v="' + (on ? '1' : '0') + '"' +
      ' title="' + (t.control === 'decision' ? 'Fight 由决策定：勾/去勾只是整类进出禁用名单' : '勾上 = 下一轮开；取消 = 下一轮关') + '"' + dis + '></button>'
    : '<span class="qCkPh" title="同一类型由上一行的勾选统一控制"></span>';
  return '<div class="qRow' + (on ? ' on' : '') + '">' + ck +
    '<span class="qName" title="' + esc(t.name) + (t.control === 'decision' ? ' · MAA 现状 ' + (t.cur ? '开' : '关') : '') + '">' + esc(t.name) + '</span>' +
    (t.control === 'decision' ? '<span class="qTag">决策</span>' : '') +
    (t.plan.length ? '<span class="qMeta">' + esc(t.plan.join(',')) + '</span>' : '') +
    '<button class="qGear" data-g="maa:' + t.idx + '" title="详细参数（右侧二级框）"' + dis + '>⚙</button>' +
    '</div>';
}

function m9aHtml(m: M9aState | null): string {
  if (!m) return '<div class="qEmpty warn">读不出 M9A 这一块（assist_state 没回数据）</div>';
  const ver = m.version && m.version.current ? m.version.current : '';
  let head = '<div class="qHead">' + chip('M9A', m.running, '在') + chip('MuMu', !!m.mumu && m.mumu.android, '已启动') +
    chip('实例目录', m.instancesReady, '在位') + (ver ? '<span class="qChip">版本 <b>' + esc(ver) + '</b></span>' : '') + '</div>';
  if (!m.instances.length) return head + '<div class="qEmpty">实例目录里没有 json</div>';
  const rows = m.instances.map((it) => {
    const cur = !!m.current && m.current.id === it.id;
    return '<div class="qRow' + (cur ? ' on cur' : '') + '">' +
      '<span class="qDot"></span>' +
      '<span class="qName">' + esc(it.name || it.id) + '</span>' +
      (cur ? '<span class="qTag ok">当前</span>' : '') +
      '<span class="qMeta">' + it.taskCount + ' 个任务</span>' +
      '<button class="qGear" data-g="m9a:' + esc(it.id) + '" title="实例详情（右侧二级框）"' + (busy ? ' disabled' : '') + '>⚙</button>' +
      (cur ? '' : '<button class="btn mini" data-sw="' + esc(it.id) + '" data-name="' + esc(it.name) + '" data-cf="再点一次：切到这台"' + (busy ? ' disabled' : '') + '>切到</button>') +
      '</div>';
  }).join('');
  return head + rows;
}

/* ── 交互 ── */

function onClick(e: MouseEvent): void {
  const t = (e.target as HTMLElement).closest('button') as HTMLButtonElement | null;
  if (!t || busy) return;
  if (t.dataset.g !== undefined) {
    openGear(t.dataset.g);
    return;
  }
  if (t.dataset.k !== undefined && t.classList.contains('qCk')) {
    void setTask(t.dataset.k, t.dataset.v !== '1');
    return;
  }
  if (t.dataset.all !== undefined) { void setAll(t.dataset.all === '1'); return; }
  if (t.dataset.reload !== undefined) { void reload(); return; }
  if (t.dataset.sw !== undefined) {
    if (!arm(t, 'sw:' + t.dataset.sw)) return;
    void act(() => bridge.assistM9aSwitch(t.dataset.sw || '', t.dataset.name || ''), '切实例');
    return;
  }
  if (t.dataset.act) {
    if (!arm(t, t.dataset.act)) return;
    const a = t.dataset.act;
    const fn = a === 'mumu-start' ? () => bridge.assistMumuStart()
      : a === 'mumu-stop' ? () => bridge.assistMumuStop()
        : null;
    if (fn) void act(fn, t.dataset.cf || a);
  }
}

/** 齿轮：右侧二级框。MAA 一条任务 = 全量参数；M9A 一台实例 = 只读详情。
 *  同一条齿轮再点一次 = 收回（另一条则直接换开）。 */
function openGear(g: string): void {
  if (!data) return;
  if (drawerOpen() && drawerKey() === g) {
    closeDrawer();
    return;
  }
  if (g.startsWith('maa:')) {
    const idx = Number(g.slice(4));
    const task = data.maa ? data.maa.tasks.find((x) => x.idx === idx) : null;
    if (task && data.maa) openMaaTaskDrawer(task, data.maa, bridge, () => { void reload(); });
    return;
  }
  if (g.startsWith('m9a:')) {
    const id = g.slice(4);
    const inst = data.m9a ? data.m9a.instances.find((x) => x.id === id) : null;
    if (inst) openM9aDrawer(inst, !!data.m9a?.current && data.m9a.current.id === id, bridge, () => { void reload(); });
  }
}

/** 验收钩子（GDOPS_AUTODRAWER）：不点任何按钮，等读数到位后自动拉起二级框。
 *  只读——抽屉里不自动按保存/切实例，等同人工点了一次齿轮。 */
export async function autoOpenDrawer(kind: 'maa' | 'm9a', taskType?: string): Promise<void> {
  // 冷启动第一拍 assist_state 要起 panel-cs 读两套配置，最长等 15s。
  for (let i = 0; i < 60 && !data; i++) await new Promise((r) => window.setTimeout(r, 250));
  if (!data) { S.pushLog('WARN', 'AUTODRAWER：读数没回来，未能打开二级框'); return; }
  try {
    if (kind === 'm9a') {
      const m = data.m9a;
      const inst = m ? (m.current || m.instances[0] || null) : null;
      if (m && inst) openM9aDrawer(inst, !!m.current && m.current.id === inst.id, bridge, () => { void reload(); });
      else S.pushLog('WARN', 'AUTODRAWER：没有可打开的 M9A 实例');
      return;
    }
    const maa = data.maa;
    const want = (taskType || '').toLowerCase();
    const task = maa
      ? (want ? maa.tasks.find((x) => x.type.toLowerCase() === want) : null) ||
        maa.tasks.find((x) => x.type === 'StartUp') || maa.tasks[0] || null
      : null;
    if (maa && task) openMaaTaskDrawer(task, maa, bridge, () => { void reload(); });
    else S.pushLog('WARN', 'AUTODRAWER：没有可打开的 MAA 任务' + (taskType ? '（' + taskType + '）' : ''));
  } catch (e) {
    S.pushLog('ERROR', 'AUTODRAWER 失败：' + String(e));
  }
}

// 两击确认：4s 内再点一次才执行，超时自动收枪。
function arm(el: HTMLElement, key: string): boolean {
  const now = Date.now();
  if ((armed[key] || 0) > now) {
    delete armed[key];
    el.classList.remove('arm');
    if (el.dataset.orig) el.textContent = el.dataset.orig;
    return true;
  }
  armed[key] = now + 4000;
  el.dataset.orig = el.textContent || '';
  el.textContent = '确认？';
  el.classList.add('arm');
  window.setTimeout(() => {
    if (!el.isConnected) return;
    el.classList.remove('arm');
    if (el.dataset.orig) el.textContent = el.dataset.orig;
  }, 4000);
  return false;
}

async function act(fn: () => Promise<ActResult>, label: string): Promise<void> {
  busy = true;
  renderQueue();
  let r: ActResult;
  try { r = await fn(); } catch (e) { r = { ok: false, error: String(e) }; }
  busy = false;
  report(label, r);
  await reload();
}

function report(label: string, r: ActResult): void {
  if (r.ok) {
    S.pushLog('OK', label + ' 完成' + (r.note ? ' · ' + r.note : ''));
  } else {
    S.pushLog('ERROR', label + ' 失败：' + (r.error || ('exit ' + (r.code ?? '?'))));
  }
}

async function setTask(kind: string, enable: boolean): Promise<void> {
  busy = true;
  renderQueue();
  let r: ActResult;
  try { r = await bridge.assistMaaSetTask(kind, enable); } catch (e) { r = { ok: false, error: String(e) }; }
  busy = false;
  report(kind + (enable ? ' 开' : ' 关'), r);
  await reload();
}

async function setAll(enable: boolean): Promise<void> {
  busy = true;
  renderQueue();
  let r: ActResult;
  try { r = await bridge.assistMaaSetAll(enable); } catch (e) { r = { ok: false, error: String(e) }; }
  busy = false;
  report(enable ? '全选' : '清空', r);
  await reload();
}