// 入口：装配桥接层/渲染层，接顶栏与模态按钮，启动时钟与开机动画。
// 字体四角色离线 bundling（OFL）：Michroma=品牌/启动幕；Martian Mono=纯数字读数；
// Chakra Petch=按钮/标题/标签；Kode Mono=日志/代码/键名。中文落系统雅黑/PingFang 栈。
import '@fontsource/michroma/latin-400.css';
import '@fontsource/chakra-petch/latin-400.css';
import '@fontsource/chakra-petch/latin-500.css';
import '@fontsource/chakra-petch/latin-600.css';
import '@fontsource/chakra-petch/latin-700.css';
import '@fontsource-variable/kode-mono';
import '@fontsource-variable/martian-mono';
import { $, dstr, esc, sanitize } from './util';
import { createBridge } from './bridge';
import * as S from './state';
import { initRender, renderAll, tickClock } from './render';
import { openModal, closeModal, wireModal } from './modal';
import { startLive, kick } from './live';
import { initPreview } from './preview';
import { chain, startDry, stopChain, syncChainButtons } from './chain';
import { openAssist } from './assist';
import { initQueue, autoOpenDrawer } from './queue';
import { closeDrawer, drawerKey, drawerOpen, openDrawer } from './drawer';

const bridge = createBridge();
initRender(bridge);
initPreview(bridge);
initQueue(bridge);
wireModal();

/* ── 开机动画 ── */
window.setTimeout(() => $('#boot').classList.add('done'), 1400);
window.setTimeout(() => { const b = document.querySelector('#boot'); if (b) b.remove(); }, 2100);

/* ── 时钟 ── */
tickClock();
window.setInterval(tickClock, 1000);

/* ── 顶栏：点火（二次确认 3s）/ 停 / 警戒演练 ── */
const runBtn = $<HTMLButtonElement>('#runBtn');
let armed = false;
let armTimer: number | undefined;
runBtn.addEventListener('click', async () => {
  if (S.running) { S.pushLog('WARN', '已有一轮在跑 · 等它结束或先点「停」'); return; }
  if (!armed) {
    armed = true; runBtn.classList.add('fire'); runBtn.textContent = '确认点火？';
    armTimer = window.setTimeout(() => {
      armed = false; runBtn.classList.remove('fire'); runBtn.textContent = '开始跑一轮';
    }, 3000);
    return;
  }
  window.clearTimeout(armTimer); armed = false;
  runBtn.classList.remove('fire'); runBtn.textContent = '开始跑一轮';

  if (bridge.mode !== 'live') { S.startRound(S.GAME_IDS); return; }

  // live：选路 = 游戏卡上的「下轮包含」；全勾就交给入口默认（all）。
  const inc = S.GAME_IDS.filter((k) => S.GAMES[k] && S.GAMES[k].inc);
  if (!inc.length) { S.pushLog('WARN', '下轮包含一个都没勾 · 没有可跑的路'); return; }
  const games = inc.length === S.GAME_IDS.length ? null : inc.join(',');
  // 内核 -Games 只收单路或 all：多路勾选（3+ 插件且只勾一部分）先在这里拦住。
  if (games && games.includes(',')) {
    S.pushLog('WARN', '内核 -Games 只收单路或全部 · 现在勾的是 ' + inc.join(' / ') + '，请只勾一路或全勾');
    return;
  }
  S.pushLog('STEP', '点火：' + (games || '全部注册路') + '（task 优先；无 task 走自提权入口）');
  const r = await bridge.startRound(games);
  if (r.ok) S.pushLog('OK', '已发起（' + (r.carrier === 'task' ? '计划任务' : '自提权入口') + '）· ' + (r.note || 'pid ' + (r.pid || 0)));
  else S.pushLog('ERROR', '点火失败：' + (r.error || '未知错误'));
  kick();
});
$('#stopBtn').addEventListener('click', async () => {
  if (bridge.mode !== 'live') { S.stopRound(); return; }
  if (chain.running) { void stopChain(bridge); return; }   // 面板自己起的预演：收本应用进程树
  if (!S.running) { S.pushLog('INFO', '当前没有在跑的轮 · 停止是空操作'); return; }
  S.pushLog('STEP', '停止：run-via-task.ps1 -Stop（提权轮不硬杀）');
  const r = await bridge.stopRound();
  if (r.ok) S.pushLog('OK', '停止命令已执行（exit 0）');
  else S.pushLog('ERROR', '停止失败：' + (r.error || ('exit ' + (r.code ?? '?'))));
  for (const l of (r.lines || []).slice(-8)) S.pushLog(r.ok ? 'INFO' : 'WARN', l);
  kick();
});
$('#alertChip').addEventListener('click', () => S.toggleDrill());

/* ── 预演按钮门（-DryRun）照旧；真点火按钮已可用 ── */
if (bridge.mode === 'live') {
  syncChainButtons();
}

/* ── 自绘标题栏：拖拽由 data-tauri-drag-region 接管；这里接窗口按钮（仅 Tauri 壳） ── */
const TW = (window as any).__TAURI__;
if (bridge.shell === 'tauri' && TW?.window?.getCurrentWindow) {
  const appWin = TW.window.getCurrentWindow();
  const wctl = document.getElementById('wctl');
  const syncMax = (): void => {
    void appWin.isMaximized().then((m: boolean) => {
      const btn = document.querySelector('[data-w=max]');
      if (btn) btn.classList.toggle('maxed', m);
    });
  };
  if (wctl) {
    wctl.classList.add('on');
    wctl.addEventListener('click', (e) => {
      const btn = (e.target as HTMLElement).closest('[data-w]') as HTMLElement | null;
      if (!btn) return;
      const w = btn.dataset.w;
      if (w === 'min') void appWin.minimize();
      else if (w === 'max') void appWin.toggleMaximize().then(syncMax);
      else if (w === 'close') void appWin.close();
    });
    syncMax();
    window.addEventListener('resize', syncMax);
  }
}

/* ── 模态按钮（web 走演示文案；Tauri 壳走只读实况快照 + 预演入口） ── */
$('#dryBtn').addEventListener('click', () => {
  const live = bridge.mode === 'live';
  void bridge.dryRun().then((h) => {
    openModal(live ? '最近一轮怎么跑 · 实读日志' : '今天会跑什么 · -DryRun', h);
    if (!live) return;
    const go = document.querySelector('#dryGo');
    if (go) go.addEventListener('click', () => { closeModal(); void startDry(bridge); });
  });
});
$('#healthBtn').addEventListener('click', () => {
  const t = bridge.mode === 'live' ? '环境体检 · 本地只读快照' : '环境体检 · status.ps1 六节';
  void bridge.health().then((h) => openModal(t, h));
});
$('#assistBtn').addEventListener('click', () => openAssist(bridge));
$('#logdirBtn').addEventListener('click', () => { void bridge.openLogDir().then((h) => openModal('打开日志目录', h)); });
$('#askBtn').addEventListener('click', async () => {
  if (bridge.mode !== 'live') {
    S.pushLog('INFO', 'vision-ask 只问不点 · 临时图用完即删（演示）');
    void bridge.visionAsk().then((h) => openModal('只问不点', h));
    return;
  }
  if (!S.sel) { S.pushLog('WARN', '没有注册的插件路 · 没得问'); return; }
  S.pushLog('STEP', '只问不点：' + S.sel + '（拍临时图问模型，不注入点击）');
  const r = await bridge.visionAskLive(S.sel);
  const lines = (r.lines || []).slice(-40).map((l) => esc(l)).join('<br>');
  openModal('只问不点 · ' + S.sel,
    '<div class="sec">' + (r.ok ? '已跑完（exit 0）' : '失败（exit ' + (r.code ?? '?') + '）') + '</div>' +
    (r.error ? esc(r.error) + '<br>' : '') +
    (lines || '<span class="mut">脚本没有输出行</span>') +
    '<div class="sec">说明</div>临时截图用完即删；不注入任何点击。');
});
$('#shotBtn').addEventListener('click', () => { void bridge.latestShot().then((h) => openModal('最新救援截图', h)); });
function openSettingsDrawer(): void {
  if (drawerOpen() && drawerKey() === 'settings') { closeDrawer(); return; }
  void bridge.fullSettings().then((h) => {
    const drawer = openDrawer('完整设置', 'SETTINGS · READ ONLY',
      '<div class="settingsDrawer">' + sanitize(h) + '</div>',
      '<button type="button" class="btn mini" data-close-settings="1">关闭</button>',
      'settings', { anchor: 'right' });
    drawer.foot.querySelector<HTMLButtonElement>('[data-close-settings]')?.addEventListener('click', () => drawer.close());
  }).catch((e) => S.pushLog('ERROR', '完整设置读取失败：' + String(e)));
}
document.addEventListener('click', (e) => {
  const t = (e.target as HTMLElement).closest<HTMLButtonElement>('[data-drawer-toggle="settings"]');
  if (t) openSettingsDrawer();
});
$('#diagBtn').addEventListener('click', () => { void bridge.diagnostics().then((h) => openModal('诊断打包', h)); });

/* ── 改定时时刻 ── */
function openSchedLive(): void {
  const t0 = S.times[0] || '07:30';
  const t1 = S.times[1] || '19:30';
  openModal('改定时时刻',
    '<div class="sec">当前（TIME 行实读）</div><code>' + esc(S.times.join(' / ') || '—') + '</code><br><br>' +
    '<input type="time" id="nt0" value="' + esc(t0) + '"> <input type="time" id="nt1" value="' + esc(t1) + '"> ' +
    '<button class="btn mini" id="applyT">应用</button><br><br>' +
    '真实实现：<code>reschedule-daily.ps1 -Times … -NoWait</code>（自提权；等本轮结束 → 重注册 → 删旧时刻）。<br>' +
    'UAC 弹窗点「是」后生效；结果看本面板底部的 TIME 行（未注册会标出来）。');
  const apply = document.querySelector('#applyT');
  if (!apply) return;
  apply.addEventListener('click', async () => {
    const a = $<HTMLInputElement>('#nt0').value;
    const b = $<HTMLInputElement>('#nt1').value;
    const list = [a, b].filter((x) => /^\d{2}:\d{2}$/.test(x));
    if (list.length !== 2) { S.pushLog('WARN', '时刻格式不对（要 HH:mm）'); return; }
    const r = await bridge.rescheduleTimes(list);
    if (r.ok) {
      S.pushLog('OK', '改定时已发起：' + (r.times || list).join(' / ') + '（UAC 点「是」后生效）');
      if (r.note) S.pushLog('INFO', r.note);
    } else S.pushLog('ERROR', '改定时失败：' + (r.error || ('exit ' + (r.code ?? '?'))));
    closeModal(); tickClock(); kick();
  });
}

$('#schedBtn').addEventListener('click', () => {
  if (bridge.mode === 'live') { openSchedLive(); return; }
  openModal('改定时时刻',
    '<input type="time" id="nt0" value="' + esc(S.times[0] || '07:30') + '"> <input type="time" id="nt1" value="' + esc(S.times[1] || '19:30') + '"> <button class="btn mini" id="applyT">应用</button><br><br>真实实现：reschedule-daily 等本轮跑完 → 写 schedule.times → 重注册（触发 UAC）→ 删旧时刻任务。<br>任务名 <code>{taskPrefix}-HHMM</code> + <code>-Manual</code>。');
  const apply = document.querySelector('#applyT');
  if (apply) apply.addEventListener('click', () => {
    const t = [$<HTMLInputElement>('#nt0').value, $<HTMLInputElement>('#nt1').value].filter((x) => /^\d{2}:\d{2}$/.test(x));
    if (t.length !== 2) return;
    S.setTimes(t);
    void bridge.rescheduleTimes(t);
    S.pushLog('INFO', 'schedule.times=[' + t.join(' / ') + '] 重注册（演示）');
    closeModal(); tickClock();
  });
});

/* ── 初始渲染 + 开机日志（只读实况走 startLive 灌真日志尾；web 才打演示行） ── */
if (bridge.mode === 'live') S.enterLive();   // 真身没到之前不显示演示卡/演示时刻
renderAll();
if (bridge.mode === 'live') {
  void startLive(bridge);
} else {
  S.pushLog('INFO', '日志文件：logs/run-' + dstr().replace(/-/g, '') + '-demo-daily-manual.log（演示）');
  S.pushLog('INFO', 'GD-OPS 指挥台上线 · 演示数据 · 未接真实内核');
  S.pushLog('STEP', '=== 待命 · 下次窗口见顶栏倒计时 ===');
}

/* ── 平台读数（Tauri 壳走 Rust app_info；浏览器里回落 web） ── */
void bridge.appInfo().then((info) => {
  $('#platTag').textContent = 'GDCore-K2.1 · ' + info.platform + '/' + info.arch + ' · v' + info.version;
  if (bridge.mode === 'live') $('#modeTag').textContent = 'TAURI 壳 · 本地实况 · 面板受控';
  else if (bridge.shell === 'tauri') $('#modeTag').textContent = 'TAURI 壳 · 演示数据 · 未接真实内核';
  // 验收钩子（GDOPS_AUTODRY=1）：开机后自动跑一轮预演，等同点模态里的「真跑 -DryRun」。
  if (bridge.mode === 'live' && info.autodry) window.setTimeout(() => { void startDry(bridge); }, 2200);
  // 验收钩子（GDOPS_AUTOASSIST=1）：开机后自动打开「助手」页，只读数、不点操作；
  // GDOPS_AUTOTAB=m9a 时直接落在 M9A 页签。
  if (bridge.mode === 'live' && info.autoassist) {
    const tab = info.autotab === 'm9a' ? 'm9a' : 'maa';
    window.setTimeout(() => openAssist(bridge, tab), 1600);
  }
  // 验收钩子（GDOPS_AUTOSEL=ark|r1999）：读完备后把「游戏路」选到这一路，只切选中、不点火；
  // 用于两态左列卡（MAA 清单 / M9A 实例）的无点击截图核对。
  const want = info.autosel;
  if (bridge.mode === 'live' && (want === 'ark' || want === 'r1999')) {
    window.setTimeout(() => { if (S.GAME_IDS.includes(want)) S.setSel(want); }, 2400);
  }
  // 验收钩子（GDOPS_AUTODRAWER=1|maa|m9a|settings|maa:<TaskType>）：不点按钮，读数到位后自动拉起右侧二级框
  //（= 人工点一次齿轮或完整设置；只开不保存）。用于 UIA 点不到 WebView2 时的截图核对。
  const dw = info.autodrawer || '';
  const isM9a = dw === 'm9a';
  const maaType = dw.startsWith('maa:') ? dw.slice(4) : '';
  if (bridge.mode === 'live' && dw === 'settings') {
    window.setTimeout(openSettingsDrawer, 3400);
  } else if (bridge.mode === 'live' && (dw === '1' || dw === 'maa' || isM9a || maaType)) {
    window.setTimeout(() => { void autoOpenDrawer(isM9a ? 'm9a' : 'maa', maaType); }, 3400);
  }
});

/* ── hash 钩子（截图/演示用；只读实况下不点演示轮） ── */
if (location.hash === '#alert') S.bootDrill();
if (location.hash === '#run' && bridge.mode === 'demo') window.setTimeout(() => S.startRound(S.GAME_IDS), 1600);