// 实时画面生命周期：独立于 6 秒状态快照，默认跟随当前选中的游戏路。
// 页面隐藏、暂停或切路时使旧请求失效；不保留后台捕获循环，也不把旧帧标成新帧。
import type { Bridge } from './bridge';
import type { CaptureFrame, CaptureView } from './types';
import * as S from './state';
import { emit, on } from './util';

let bound: Bridge | null = null;
let timer: number | undefined;
let generation = 0;
let currentGame = '';
let initialized = false;
let autoStarted = false;
let consecutiveFailures = 0;

function view(patch: Partial<CaptureView>): void {
  Object.assign(S.live.capture, patch);
  emit('live');
}

function stopLoop(): void {
  generation++;
  if (timer !== undefined) window.clearTimeout(timer);
  timer = undefined;
}

function clearFrame(): void {
  const img = document.getElementById('previewImg') as HTMLImageElement | null;
  if (img) { img.removeAttribute('src'); img.hidden = true; }
  view({ updated: 0, error: '', stale: false, source: undefined, state: 'connecting' });
}

function delayFor(frame?: CaptureFrame): number {
  if (frame?.source === 'adb') return 1500;
  if (frame && !frame.ok) return Math.min(3000 * 2 ** Math.min(consecutiveFailures, 3), 8000);
  return 800;
}

function apply(frame: CaptureFrame): void {
  if (!frame.ok) {
    view({ state: frame.state || 'failed', error: frame.error || '没有读到实时画面', stale: true });
    return;
  }
  if (!frame.image) { view({ state: 'failed', error: '画面回执缺少图像', stale: true }); return; }
  const img = document.getElementById('previewImg') as HTMLImageElement | null;
  if (!img) return;
  img.src = frame.image;
  const capturedAt = frame.capturedAt || Date.now();
  view({
    state: frame.state || 'ready', source: frame.source, updated: capturedAt,
    error: frame.note || '', stale: Date.now() - capturedAt > (frame.source === 'adb' ? 5000 : 2500),
  });
}

async function cycle(): Promise<void> {
  if (!bound || bound.mode !== 'live' || !S.live.capture.active || document.hidden || !S.sel) return;
  const token = ++generation;
  const game = S.sel;
  if (S.live.capture.updated === 0) view({ state: 'connecting', error: '' });
  let frame: CaptureFrame;
  try { frame = await bound.captureFrame(game); }
  catch { frame = { ok: false, state: 'failed', error: '实时画面通道调用失败' }; }
  if (token !== generation || game !== S.sel || !S.live.capture.active || document.hidden) return;
  apply(frame);
  consecutiveFailures = frame.ok ? 0 : consecutiveFailures + 1;
  timer = window.setTimeout(() => { void cycle(); }, delayFor(frame));
}

function restart(): void {
  stopLoop();
  consecutiveFailures = 0;
  clearFrame();
  if (S.live.capture.active && !document.hidden) void cycle();
}

export function initPreview(bridge: Bridge): void {
  if (initialized) return;
  initialized = true;
  bound = bridge;
  const btn = document.getElementById('previewBtn') as HTMLButtonElement | null;
  btn?.addEventListener('click', () => {
    if (!S.sel) return;
    if (S.live.capture.active) {
      S.live.capture.active = false;
      stopLoop();
      view({ state: 'paused', error: '', stale: false });
    } else {
      S.live.capture.active = true;
      restart();
    }
  });
  on('games', () => {
    if (bridge.mode !== 'live') return;
    if (!autoStarted && S.sel) { autoStarted = true; S.live.capture.active = true; }  // 仅首次有效选择自动开启
    if (currentGame !== S.sel) {
      currentGame = S.sel;
      if (S.live.capture.active) restart();
      else { clearFrame(); view({ state: 'paused' }); }
    }
    emit('live');
  });
  document.addEventListener('visibilitychange', () => {
    if (!bound) return;
    if (document.hidden) {
      stopLoop();
      if (S.live.capture.updated) view({ stale: true });
    } else if (S.live.capture.active && S.sel) {
      restart();
    }
  });
  window.addEventListener('beforeunload', stopLoop);
  window.setInterval(() => {
    const c = S.live.capture;
    if (!c.active || !c.updated) return;
    const stale = Date.now() - c.updated > (c.source === 'adb' ? 5000 : 2500);
    if (stale !== c.stale) view({ stale });
  }, 1000);
}
