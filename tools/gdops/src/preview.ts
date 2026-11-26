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
let renderedAt: number[] = [];
let inFlight = false;
let rerunRequested = false;

function view(patch: Partial<CaptureView>): void {
  Object.assign(S.live.capture, patch);
  emit('live');
}

function stopLoop(): void {
  generation++;
  rerunRequested = false;
  if (timer !== undefined) window.clearTimeout(timer);
  timer = undefined;
}

function clearFrame(): void {
  renderedAt = [];
  const img = document.getElementById('previewImg') as HTMLImageElement | null;
  if (img) { img.removeAttribute('src'); img.hidden = true; }
  view({ updated: 0, error: '', note: '', stale: false, source: undefined, state: 'connecting', fps: 0, captureMs: undefined, encodedBytes: undefined });
}

// 目标节拍只约束成功帧：端游约 12.5fps、ADB 约 6fps。
// elapsed 已包含捕获、传输和图片解码，慢帧不再额外叠一整个周期。
function delayFor(frame: CaptureFrame | undefined, elapsed: number): number {
  if (!frame || !frame.ok) {
    const exponent = Math.min(Math.max(consecutiveFailures - 1, 0), 3);
    return Math.min(3000 * 2 ** exponent, 8000); // 首次 3s，再 6/8/8s
  }
  return Math.max(0, (frame.source === 'adb' ? 160 : 80) - elapsed);
}

async function apply(frame: CaptureFrame, token: number, game: string): Promise<boolean> {
  if (!frame.ok) {
    view({ state: frame.state || 'failed', error: frame.error || '没有读到实时画面', note: '', stale: true, fps: 0 });
    return false;
  }
  if (!frame.image) { view({ state: 'failed', error: '画面回执缺少图像', note: '', stale: true, fps: 0 }); return false; }
  const img = document.getElementById('previewImg') as HTMLImageElement | null;
  if (!img) return false;
  try {
    // 先在内存图片上解码；解码期间切路/暂停/隐藏，晚回执不会写进可见画面。
    const next = new Image();
    next.src = frame.image;
    await next.decode();
  } catch {
    if (token !== generation || game !== S.sel || !S.live.capture.active || document.hidden) return false;
    view({ state: 'failed', error: '实时画面解码失败', note: '', stale: true, fps: 0 });
    return false;
  }
  if (token !== generation || game !== S.sel || !S.live.capture.active || document.hidden) return false;
  img.src = frame.image;
  const shownAt = performance.now();
  renderedAt = renderedAt.filter((at) => shownAt - at <= 1200);
  renderedAt.push(shownAt);
  const span = renderedAt.length > 1 ? renderedAt[renderedAt.length - 1] - renderedAt[0] : 0;
  const fps = span > 0 ? (renderedAt.length - 1) * 1000 / span : 0;
  const capturedAt = frame.capturedAt || Date.now();
  view({
    state: frame.state || 'ready', source: frame.source, updated: capturedAt,
    error: '', note: frame.note || '', stale: Date.now() - capturedAt > (frame.source === 'adb' ? 5000 : 2500),
    fps, captureMs: frame.captureMs, encodedBytes: frame.encodedBytes,
  });
  return true;
}

async function cycle(): Promise<void> {
  if (!bound || bound.mode !== 'live' || !S.live.capture.active || document.hidden || !S.sel) return;
  // 切路只记一次待执行；旧请求返回前不并发起第二帧。
  if (inFlight) { rerunRequested = true; return; }
  inFlight = true;
  const token = ++generation;
  const game = S.sel;
  const started = performance.now();
  try {
    if (S.live.capture.updated === 0) view({ state: 'connecting', error: '' });
    let frame: CaptureFrame;
    try { frame = await bound.captureFrame(game); }
    catch { frame = { ok: false, state: 'failed', error: '实时画面通道调用失败' }; }
    if (token !== generation || game !== S.sel || !S.live.capture.active || document.hidden) return;
    const shown = await apply(frame, token, game);
    if (token !== generation || game !== S.sel || !S.live.capture.active || document.hidden) return;
    consecutiveFailures = shown ? 0 : consecutiveFailures + 1;
    timer = window.setTimeout(() => { void cycle(); }, delayFor(shown ? frame : undefined, performance.now() - started));
  } finally {
    inFlight = false;
    // 旧回执彻底结束后才切到最新游戏；暂停或隐藏时直接结束。
    if (token !== generation || game !== S.sel) {
      void cycle();
    } else if (rerunRequested) {
      rerunRequested = false;
      if (timer !== undefined) window.clearTimeout(timer);
      timer = undefined;
      void cycle();
    }
  }
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
      view({ state: 'paused', error: '', stale: false, fps: 0 });
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
