// 助手面板：MAA / M9A 的现状读数 + 计划改写 + 几枚真操作。
// 读数与操作全部走 bridge → Rust assist_*（Rust 再问核内 verb）；本层不判定、不读文件。
// 计划的保存是"只改白名单键的值"：Rust 侧备份 + 行内替换 + 回读校验，界面只负责收值与展示回执。
import { $, esc, now } from './util';
import type { Bridge } from './bridge';
import { openModal } from './modal';
import type { AssistState, MaaState, M9aState } from './types';

type Tab = 'maa' | 'm9a';

let bridge: Bridge | null = null;
let data: AssistState | null = null;
let view: Tab = 'maa';
let busy = false;
let busySince = 0;
let busyTick: number | undefined;
let armed: Record<string, number> = {};
let wired = false;

const MAX_LOG = 80;
const logs: string[] = [];

function logLine(lv: string, text: string): void {
  const cls = lv === 'ERROR' ? 'mcrit' : lv === 'WARN' ? 'mwarn' : lv === 'OK' ? 'mok' : 'mut';
  logs.push('<span class="' + cls + '">[' + now() + '] ' + esc(text) + '</span>');
  if (logs.length > MAX_LOG) logs.splice(0, logs.length - MAX_LOG);
  const box = document.getElementById('aLog');
  if (box) box.innerHTML = logs.join('<br>');
}

function setBusy(on: boolean, text = ''): void {
  const was = busy;
  busy = on;
  if (busyTick !== undefined && (!on || !was)) { window.clearInterval(busyTick); busyTick = undefined; }
  const box = document.getElementById('aBusy');
  if (on) {
    if (!was) busySince = Date.now();
    const paint = (): void => {
      const el = document.getElementById('aBusy');
      if (!el) return;
      const s = Math.floor((Date.now() - busySince) / 1000);
      el.innerHTML = '<b class="mwarn">忙</b> · ' + esc(text) + (s > 2 ? ' · ' + s + 's' : '');
    };
    paint();
    if (busyTick === undefined) busyTick = window.setInterval(paint, 1000);
  } else if (box) {
    box.innerHTML = '<span class="mut">就绪</span>';
  }
  document.querySelectorAll<HTMLButtonElement>('#aBody [data-act], #aBody [data-save], #aBody [data-tog], #aBody [data-tab]').forEach((b) => {
    b.disabled = on;
  });
}

function chip(label: string, ok: boolean | null, on = '在'): string {
  if (ok === null) return '<span class="asChip">' + esc(label) + ' <b class="mut">—</b></span>';
  return '<span class="asChip' + (ok ? ' on' : '') + '">' + esc(label) + ' <b class="' + (ok ? 'mok' : 'mwarn') + '">' + esc(ok ? on : '否') + '</b></span>';
}

function field(label: string, key: string, value: string, opt: { list?: boolean; int?: boolean; hint?: string } = {}): string {
  const attrs = 'class="asIn" data-k="' + esc(key) + '" value="' + esc(value) + '"' +
    (opt.list ? ' data-list="1"' : '') + (opt.int ? ' data-int="1" type="number"' : '');
  return '<label class="asF"><span>' + esc(label) + '</span><input ' + attrs + '>' +
    (opt.hint ? '<i>' + esc(opt.hint) + '</i>' : '') + '</label>';
}

function toggle(label: string, key: string, on: boolean, hint = ''): string {
  return '<div class="asF"><span>' + esc(label) + '</span>' +
    '<button class="asTog' + (on ? ' on' : '') + '" data-tog="' + esc(key) + '" data-v="' + (on ? '1' : '0') + '">' + (on ? '开' : '关') + '</button>' +
    (hint ? '<i>' + esc(hint) + '</i>' : '') + '</div>';
}

function readonly(label: string, value: string): string {
  return '<div class="asF"><span>' + esc(label) + '</span><b class="asRo">' + esc(value || '—') + '</b></div>';
}

/* ── 两块页面 ── */

function maaHtml(m: MaaState | null): string {
  if (!m) return '<div class="mwarn">读不出 MAA 这一块（assist_state 没回数据）。</div>' + shellFoot();
  const p = m.plan;
  const q = m.queue.length
    ? m.queue.slice(0, 14).map((x) => '<span class="asQ">' + esc(x) + '</span>').join('') +
      (m.queue.length > 14 ? '<span class="mut"> …共 ' + m.queue.length + ' 行</span>' : '')
    : (m.queueError ? '<b class="mwarn">' + esc(m.queueError) + '</b>' : '<span class="mut">队列读出来是空的</span>');
  return (
    '<div class="asHead">' +
      chip('MAA', m.running, '运行中') + chip('端游', m.gameRunning, '运行中') +
      chip('目录', m.dirReady, '在位') + chip('配置', m.configReady, '在位') +
      '<span class="asChip">进程名 <b class="mut">' + esc(m.exeName || '—') + '</b></span>' +
    '</div>' +
    '<div class="sec">日常队列（MAA 配置件实读，只展示）</div><div class="asQRow">' + q + '</div>' +
    '<div class="sec">计划（保存后写回 config/settings.json；下一轮按新计划跑）</div>' +
    '<div class="asGrid">' +
      field('默认关卡', 'ark.defaultStage', p.defaultStage, { hint: '活动未开时刷这关' }) +
      field('理智清空方式', 'ark.finishAllSanityWith', p.finishAllSanityWith) +
      field('每轮最多补刷', 'ark.farmMaxTimesPerRound', String(p.farmMaxTimesPerRound), { int: true }) +
      field('保留队列', 'ark.keepQueuesEnabled', p.keepQueuesEnabled.join(', '), { list: true, hint: '逗号分隔' }) +
      field('禁用队列', 'ark.disableQueues', p.disableQueues.join(', '), { list: true, hint: '逗号分隔' }) +
      toggle('长草追加补刷', 'ark.farmAppend', p.farmAppend) +
      toggle('同时开主关卡', 'ark.alsoEnableMainFight', p.alsoEnableMainFight) +
      toggle('跑完关 MAA', 'runtime.quitAssistantAfter', p.quitAssistantAfter, '关掉后"关闭 MAA"不会动手') +
      toggle('跑完关端游', 'runtime.closeArknightsAfter', p.closeArknightsAfter, '关掉后"关闭 MAA + 端游"只关 MAA') +
      toggle('由助手拉起端游', 'runtime.launchArkFromHelper', p.launchArkFromHelper) +
    '</div>' +
    '<div class="asActions"><button class="btn mini" data-save="maa">保存计划</button>' +
      '<span class="mut">只改上面这几项的值；改前自动备份到 backups/</span></div>' +
    '<div class="sec">操作</div><div class="asActions">' +
      '<button class="btn mini" data-act="ark-start" data-cf="再点一次：启动端游">启动端游</button>' +
      '<button class="btn mini danger" data-act="maa-close" data-cf="再点一次：关闭 MAA 与端游">关闭 MAA + 端游</button>' +
      '<button class="btn mini" data-act="reload">刷新读数</button>' +
    '</div>' +
    '<div class="mut asNote">启动端游会一直等到窗口出现（最长约 25 分钟，含登录/更新/热更）；关闭动作受上面两枚「跑完关」开关支配。</div>' +
    shellFoot()
  );
}

function m9aHtml(m: M9aState | null): string {
  if (!m) return '<div class="mwarn">读不出 M9A 这一块（assist_state 没回数据）。</div>' + shellFoot();
  const p = m.plan;
  const ver = m.version && m.version.current ? m.version.current : '';
  const verHtml = m.version && m.version.error
    ? '<b class="mwarn">' + esc(m.version.error) + '</b>'
    : (ver ? '<b class="mok">' + esc(ver) + '</b>' + (m.version && m.version.repo ? ' <span class="mut">(' + esc(m.version.repo) + ')</span>' : '') : '<span class="mut">—</span>');
  const rows = m.instances.length
    ? m.instances.map((it) => {
        const cur = m.current && m.current.id === it.id;
        return '<div class="asInst' + (cur ? ' cur' : '') + '">' +
          '<b>' + esc(it.name || it.id) + '</b>' +
          (cur ? ' <span class="mok">当前</span>' : '') +
          '<span class="mut"> · ' + it.taskCount + ' 个任务' + (it.tasks.length ? '：' + esc(it.tasks.join(' / ')) : '') + '</span>' +
          '<span class="mut"> · ADB ' + esc(it.adbSerial || '—') + (it.adbName ? '（' + esc(it.adbName) + '）' : '') + '</span>' +
          (cur ? '' : ' <button class="btn mini" data-act="switch" data-id="' + esc(it.id) + '" data-name="' + esc(it.name) + '" data-cf="再点一次：切到这台">切到这台</button>') +
          '</div>';
      }).join('')
    : '<span class="mwarn">实例目录里没有 json</span>';
  const conn = m.conn
    ? '<b class="mok">' + esc(m.conn.serial || '—') + '</b>' + (m.conn.name ? ' <span class="mut">' + esc(m.conn.name) + '</span>' : '')
    : (m.connError ? '<b class="mwarn">' + esc(m.connError) + '</b>' : '<span class="mut">当前实例没有连接信息</span>');
  const mm = m.mumu;
  return (
    '<div class="asHead">' +
      chip('M9A', m.running, '运行中') + chip('MuMu 安卓', mm.android, '已启动') + chip('MuMu 进程', mm.process, '在') +
      chip('实例目录', m.instancesReady, '在位') +
      '<span class="asChip">版本 ' + verHtml + '</span>' +
    '</div>' +
    '<div class="sec">实例（' + m.instances.length + ' 台 · 当前 ' + esc(m.current ? (m.current.name || m.current.id) : '未定') + '）</div>' + rows +
    '<div class="sec">连接（当前实例，只读）</div>' + conn +
    '<div class="sec">MuMu</div>' +
    '<div class="asRow">' + (mm.name ? '<span class="asChip">' + esc(mm.name) + '</span>' : '<span class="mut">未读到模拟器名</span>') +
      (mm.index ? '<span class="asChip">编号 ' + esc(mm.index) + '</span>' : '') +
      (mm.error ? ' <b class="mwarn">' + esc(mm.error) + '</b>' : '') + '</div>' +
    '<div class="sec">计划（保存后写回 config/settings.json）</div>' +
    '<div class="asGrid">' +
      field('长草 → 实例', 're1999.planToInstance.idle', p.idle) +
      field('活动 → 实例', 're1999.planToInstance.event', p.event) +
      field('复刻 → 实例', 're1999.planToInstance.rerun', p.rerun) +
      field('模拟器编号', 'mumu.vmIndex', p.vmIndex) +
      toggle('主线需确认', 're1999.mainStoryRequiresAck', p.mainStoryRequiresAck) +
      toggle('商店买完即停活动', 're1999.shopDoneStopsEvent', p.shopDoneStopsEvent) +
      toggle('CLI 转发参数', 'runtime.m9aCliArgs', p.m9aCliArgs) +
      toggle('强制启动 M9A', 'runtime.m9aForceStart', p.m9aForceStart) +
      toggle('跑完关模拟器', 'runtime.closeEmulatorAfter', p.closeEmulatorAfter) +
      toggle('接管 MAA', 'runtime.takeOverAssistant', p.takeOverAssistant) +
    '</div>' +
    '<div class="asActions"><button class="btn mini" data-save="m9a">保存计划</button>' +
      '<span class="mut">只改上面这几项的值；改前自动备份</span></div>' +
    '<div class="sec">操作</div><div class="asActions">' +
      '<button class="btn mini" data-act="mumu-start" data-cf="再点一次：启动 MuMu">启动 MuMu</button>' +
      '<button class="btn mini" data-act="mumu-stop" data-cf="再点一次：关闭 MuMu">关闭 MuMu</button>' +
      '<button class="btn mini danger" data-act="m9a-stop" data-cf="再点一次：杀掉 M9A">关 M9A</button>' +
      '<button class="btn mini danger" data-act="m9a-update" data-cf="再点一次：联网更新 M9A">M9A 更新</button>' +
      '<button class="btn mini" data-act="reload">刷新读数</button>' +
    '</div>' +
    '<div class="mut asNote">「M9A 更新」真跑 tools\\m9a-update.ps1 -Apply（联网下载并覆盖，可能十几分钟）；「关 M9A」受 runtime.quitAssistantAfter 支配。</div>' +
    shellFoot()
  );
}

function shellFoot(): string {
  return '<div class="sec">执行记录</div><div class="asLog" id="aLog">' + (logs.length ? logs.join('<br>') : '<span class="mut">这里会显示每次操作的回执（已经脱掉本机路径）</span>') + '</div>';
}

function render(): void {
  const body = document.getElementById('aBody');
  if (!body) return;
  body.innerHTML = view === 'maa' ? maaHtml(data && data.maa) : m9aHtml(data && data.m9a);
  const tabs = document.querySelectorAll<HTMLElement>('#aTabs [data-tab]');
  tabs.forEach((t) => t.classList.toggle('on', t.dataset.tab === view));
  if (busy) setBusy(true, busyLabel);
}

let busyLabel = '';
function shellHtml(): string {
  const settingsNote = data && !data.settingsPresent
    ? '<div class="mwarn">config/settings.json 不在：先跑 setup.ps1 生成配置（面板不会替你补一份）。</div>'
    : '';
  return settingsNote +
    '<div class="asTabs" id="aTabs">' +
      '<button class="asTab' + (view === 'maa' ? ' on' : '') + '" data-tab="maa">MAA · 明日方舟</button>' +
      '<button class="asTab' + (view === 'm9a' ? ' on' : '') + '" data-tab="m9a">M9A · 重返未来1999</button>' +
    '</div>' +
    '<div class="asBusy" id="aBusy"><span class="mut">就绪</span></div>' +
    '<div id="aBody"></div>';
}

/* ── 动作 ── */

async function reload(): Promise<void> {
  if (!bridge || busy) return;
  setBusy(true, '读取现状：MAA 队列 / M9A 实例 / MuMu 状态…');
  busyLabel = '读取现状：MAA 队列 / M9A 实例 / MuMu 状态…';
  const s = await bridge.assistState();
  setBusy(false);
  if (!s) {
    logLine('ERROR', '读数失败：Rust 没回 assist_state（或仓库根目录找不到）');
    render();
    return;
  }
  data = s;
  render();
}

async function doSave(): Promise<void> {
  if (!bridge || busy) return;
  const edits: { key: string; value: unknown }[] = [];
  document.querySelectorAll<HTMLInputElement>('#aBody input[data-k]').forEach((f) => {
    const key = f.dataset.k || '';
    if (!key) return;
    if (f.dataset.list) {
      const list = f.value.split(/[,，、\n]/).map((s) => s.trim()).filter((s) => s.length > 0);
      edits.push({ key, value: list });
    } else if (f.dataset.int) {
      const n = Number(f.value);
      if (!Number.isFinite(n)) { logLine('WARN', key + ' 不是数字，没提交'); return; }
      edits.push({ key, value: Math.round(n) });
    } else {
      edits.push({ key, value: f.value.trim() });
    }
  });
  document.querySelectorAll<HTMLElement>('#aBody [data-tog]').forEach((t) => {
    const key = t.dataset.tog || '';
    if (key) edits.push({ key, value: t.dataset.v === '1' });
  });
  if (!edits.length) { logLine('WARN', '没有可提交的项'); return; }
  setBusy(true, '写 settings.json（先备份 → 行内替换 → 回读校验）…');
  busyLabel = '写 settings.json（先备份 → 行内替换 → 回读校验）…';
  const r = await bridge.assistSavePlan(edits) as { ok: boolean; error?: string; changed?: string[]; backup?: string; note?: string };
  setBusy(false);
  if (!r.ok) {
    logLine('ERROR', '保存被拒：' + (r.error || '未知错误'));
    render();
    return;
  }
  const changed = r.changed || [];
  if (!changed.length) logLine('OK', '没有变化，' + (r.note || '没写盘'));
  else logLine('OK', '已写入 ' + changed.length + ' 项：' + changed.join('、') + (r.backup ? '（改前备份 ' + r.backup + '）' : ''));
  if (r.note) logLine('INFO', r.note);
  data = await bridge.assistState() || data;
  render();
}

const LABEL: Record<string, string> = {
  'ark-start': '启动端游（等窗口，最长约 25 分钟）…',
  'maa-close': '关闭 MAA 与端游…',
  'm9a-stop': '关闭 M9A…',
  'mumu-start': '启动 MuMu 并等安卓系统…',
  'mumu-stop': '关闭 MuMu…',
  'm9a-update': 'M9A 更新（联网下载，可能十几分钟）…',
  'switch': '切换当前实例…',
  reload: '刷新读数…',
};

type Act = { ok: boolean; error?: string; note?: string; lines?: string[]; code?: number; started?: boolean; maaClosed?: number; arkClosed?: number; m9aClosed?: number; applied?: boolean; changes?: string[]; line?: string; vm?: string };

async function doAct(btn: HTMLElement): Promise<void> {
  if (!bridge || busy) return;
  const act = btn.dataset.act || '';
  if (!act) return;
  if (act === 'reload') { armed = {}; void reload(); return; }
  if (btn.dataset.cf) {
    if (!armed[act]) {
      btn.dataset.tx = btn.textContent || '';
      armed[act] = window.setTimeout(() => {
        delete armed[act];
        btn.classList.remove('fire');
        btn.textContent = btn.dataset.tx || '';
      }, 4000);
      btn.classList.add('fire');
      btn.textContent = btn.dataset.cf;
      return;
    }
    window.clearTimeout(armed[act]);
    delete armed[act];
    btn.classList.remove('fire');
    btn.textContent = btn.dataset.tx || '';
  }
  const label = LABEL[act] || act;
  setBusy(true, label);
  busyLabel = label;
  let r: Act;
  switch (act) {
    case 'ark-start': r = await bridge.assistArkStart(); break;
    case 'maa-close': r = await bridge.assistMaaClose(); break;
    case 'm9a-stop': r = await bridge.assistM9aStop(); break;
    case 'mumu-start': r = await bridge.assistMumuStart(); break;
    case 'mumu-stop': r = await bridge.assistMumuStop(); break;
    case 'm9a-update': r = await bridge.assistM9aUpdate(); break;
    case 'switch': r = await bridge.assistM9aSwitch(btn.dataset.id || '', btn.dataset.name || ''); break;
    default: r = { ok: false, error: '面板不认识这个动作：' + act };
  }
  setBusy(false);
  report(act, r);
  if (act === 'maa-close' || act === 'm9a-stop' || act === 'mumu-start' || act === 'mumu-stop' || act === 'switch') {
    const s = await bridge.assistState();
    if (s) data = s;
  }
  render();
}

function report(act: string, r: Act): void {
  if (!r.ok) {
    logLine('ERROR', (LABEL[act] || act) + ' 失败：' + (r.error || r.note || (r.code !== undefined ? '退出码 ' + r.code : '未知错误')));
    for (const l of (r.lines || []).slice(-12)) logLine('WARN', l);
    return;
  }
  const bits: string[] = [];
  if (typeof r.started === 'boolean') bits.push(r.started ? '已启动' : '没等到窗口/系统');
  if (typeof r.maaClosed === 'number') bits.push('关掉 MAA ' + r.maaClosed + ' 个');
  if (typeof r.arkClosed === 'number') bits.push('关掉端游 ' + r.arkClosed + ' 个');
  if (typeof r.m9aClosed === 'number') bits.push('关掉 M9A ' + r.m9aClosed + ' 个');
  if (typeof r.applied === 'boolean') bits.push(r.applied ? '已切换' : '内核没落笔（见下）');
  if (r.code !== undefined) bits.push('退出码 ' + r.code);
  logLine(bits.some((b) => b.includes('没等到') || b.includes('没落笔')) ? 'WARN' : 'OK', (LABEL[act] || act).replace(/…$/, '') + ' 完成' + (bits.length ? '：' + bits.join(' · ') : ''));
  for (const c of r.changes || []) logLine('INFO', c);
  if (r.line) logLine('INFO', r.line);
  for (const l of (r.lines || []).slice(-12)) logLine('INFO', l);
  if (r.note) logLine('INFO', r.note);
}

/* ── 装配 ── */

export function openAssist(b: Bridge, tab: Tab = 'maa'): void {
  bridge = b;
  view = tab;
  logs.length = 0;
  armed = {};
  data = null;
  busy = false;
  openModal('助手 · MAA / M9A', shellHtml());
  $('#modal').classList.add('assist');
  render();
  if (!wired) {
    wired = true;
    const body = $('#mBody');
    body.addEventListener('click', (e) => {
      const t = e.target as HTMLElement;
      const tab = t.closest('[data-tab]') as HTMLElement | null;
      if (tab && tab.dataset.tab) { view = tab.dataset.tab as Tab; render(); return; }
      const tog = t.closest('[data-tog]') as HTMLElement | null;
      if (tog) {
        if (busy) return;
        const on = tog.dataset.v !== '1';
        tog.dataset.v = on ? '1' : '0';
        tog.classList.toggle('on', on);
        tog.textContent = on ? '开' : '关';
        return;
      }
      const save = t.closest('[data-save]') as HTMLElement | null;
      if (save) { void doSave(); return; }
      const act = t.closest('[data-act]') as HTMLElement | null;
      if (act) { void doAct(act); }
    });
  }
  void reload();
}