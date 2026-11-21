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
import { $, pad, dstr } from './util';
import { createBridge } from './bridge';
import * as S from './state';
import { initRender, renderAll, tickClock } from './render';
import { openModal, closeModal, wireModal } from './modal';
import { startLive } from './live';
import { startDry, stopChain, syncChainButtons } from './chain';

const bridge = createBridge();
initRender(bridge);
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
runBtn.addEventListener('click', () => {
  if (S.running) return;
  if (!armed) {
    armed = true; runBtn.classList.add('fire'); runBtn.textContent = '确认点火？';
    armTimer = window.setTimeout(() => {
      armed = false; runBtn.classList.remove('fire'); runBtn.textContent = '开始跑一轮';
    }, 3000);
    return;
  }
  window.clearTimeout(armTimer); armed = false;
  runBtn.classList.remove('fire'); runBtn.textContent = '开始跑一轮';
  S.startRound(S.GAME_IDS);
});
$('#stopBtn').addEventListener('click', () => {
  if (bridge.mode === 'live') { void stopChain(bridge); return; }
  S.stopRound();
});
$('#alertChip').addEventListener('click', () => S.toggleDrill());

/* ── 只读阶段：真点火/写入不接；预演（-DryRun）已接，按钮门由 chain.syncChainButtons 管 ── */
if (bridge.mode === 'live') {
  const run = $<HTMLButtonElement>('#runBtn');
  run.disabled = true;
  run.title = '真点火未接入：本阶段只跑 -DryRun 预演（点「今天会跑什么」里的按钮）';
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
$('#logdirBtn').addEventListener('click', () => { void bridge.openLogDir().then((h) => openModal('打开日志目录', h)); });
$('#askBtn').addEventListener('click', () => {
  if (bridge.mode === 'live') S.pushLog('WARN', '只读阶段：只问不点未接入（下一阶段）');
  else S.pushLog('INFO', 'vision-ask 只问不点 · 临时图用完即删（演示）');
  void bridge.visionAsk().then((h) => openModal('只问不点', h));
});
$('#shotBtn').addEventListener('click', () => { void bridge.latestShot().then((h) => openModal('最新救援截图', h)); });
$('#fullSet').addEventListener('click', () => { void bridge.fullSettings().then((h) => openModal('完整设置', h)); });
$('#diagBtn').addEventListener('click', () => { void bridge.diagnostics().then((h) => openModal('诊断打包', h)); });

/* ── 改定时时刻 ── */
$('#schedBtn').addEventListener('click', () => {
  if (bridge.mode === 'live') { S.pushLog('WARN', '只读阶段：改定时未接入（下一阶段）'); return; }
  openModal('改定时时刻',
    '<input type="time" id="nt0" value="' + pad(S.times[0]) + ':00"> <input type="time" id="nt1" value="' + pad(S.times[1]) + ':00"> <button class="btn mini" id="applyT">应用</button><br><br>真实实现：reschedule-daily 等本轮跑完 → 写 schedule.times → 重注册（触发 UAC）→ 删旧时刻任务。<br>任务名 <code>{taskPrefix}-HHMM</code> + <code>-Manual</code>。');
  const apply = document.querySelector('#applyT');
  if (apply) apply.addEventListener('click', () => {
    const t = [parseInt($<HTMLInputElement>('#nt0').value.slice(0, 2), 10), parseInt($<HTMLInputElement>('#nt1').value.slice(0, 2), 10)];
    S.setTimes(t);
    void bridge.reschedule(t);
    S.pushLog('INFO', 'schedule.times=[' + t.join(',') + '] 重注册（演示）');
    closeModal(); tickClock();
  });
});

/* ── 初始渲染 + 开机日志（只读实况走 startLive 灌真日志尾；web 才打演示行） ── */
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
  if (bridge.mode === 'live') $('#modeTag').textContent = 'TAURI 壳 · 本地只读实况 · 未点火';
  else if (bridge.shell === 'tauri') $('#modeTag').textContent = 'TAURI 壳 · 演示数据 · 未接真实内核';
  // 验收钩子（GDOPS_AUTODRY=1）：开机后自动跑一轮预演，等同点模态里的「真跑 -DryRun」。
  if (bridge.mode === 'live' && info.autodry) window.setTimeout(() => { void startDry(bridge); }, 2200);
});

/* ── hash 钩子（截图/演示用；只读实况下不点演示轮） ── */
if (location.hash === '#alert') S.bootDrill();
if (location.hash === '#run' && bridge.mode === 'demo') window.setTimeout(() => S.startRound(S.GAME_IDS), 1600);