// 单链预演：前端这一侧的轮状态、按钮门与逐行入账。
// Rust 侧 chain_run 起 pwsh 跑 daily-clear.ps1 -DryRun；事件 chain-line / chain-end 由 live.ts 转进来。
import type { Bridge } from './bridge';
import * as S from './state';
import { parseLine } from './logline';
import { emit } from './util';

type Hooks = {
  setStreaming: (v: boolean) => void;  // 预演期间暂停「快照重灌日志」，免得流到一半被新日志文件整页替换
  refresh: () => void;                 // 结束后立刻取一次新快照（最新日志会变成这一轮的 run-*.log）
};

export const chain = { running: false, pid: 0, lines: 0, t0: 0 };
let hooks: Hooks = { setStreaming: () => { /* 未接时静默 */ }, refresh: () => { /* 未接时静默 */ } };

export function wireChain(_bridge: Bridge, h: Hooks): void { hooks = h; }

// 真点火那一枚始终禁用（本阶段只跑预演）；「今天会跑什么」与「停」按是否在跑切换。
export function syncChainButtons(): void {
  const dry = document.getElementById('dryBtn') as HTMLButtonElement | null;
  const stop = document.getElementById('stopBtn') as HTMLButtonElement | null;
  if (dry) {
    dry.disabled = chain.running;
    dry.title = chain.running ? '预演进行中：等它跑完，或点「停」中断' : '';
  }
  if (stop) {
    stop.disabled = !chain.running;
    stop.title = chain.running ? '中断本轮预演（只结束本应用起的 pwsh 进程树）' : '当前没有在跑的预演';
  }
}

export async function startDry(bridge: Bridge): Promise<void> {
  if (chain.running) { S.pushLog('WARN', '预演已经在跑 · 等它结束或先中断'); return; }
  chain.running = true;
  chain.lines = 0;
  chain.t0 = Date.now();
  hooks.setStreaming(true);
  syncChainButtons();
  S.live.state = 'dry-run';
  S.live.action = 'dry-run · 起 pwsh';
  emit('live');
  S.pushLog('STEP', '=== 预演开始 · daily-clear.ps1 -DryRun -NoElevate（只预演，不点火）===');
  const r = await bridge.chainRun('dry');
  if (!r.ok) {
    chain.running = false;
    hooks.setStreaming(false);
    syncChainButtons();
    S.pushLog('WARN', '预演没有起来：' + (r.error || '未知原因'));
    return;
  }
  chain.pid = r.pid || 0;
  S.pushLog('INFO', 'pwsh pid=' + chain.pid + ' · 入口 ' + (r.script || 'daily-clear.ps1'));
}

export async function stopChain(bridge: Bridge): Promise<void> {
  if (!chain.running) { S.pushLog('INFO', '当前没有在跑的预演 · 停是空操作'); return; }
  S.pushLog('WARN', '收到中断：只结束本应用起的 pwsh 进程树（不碰其它窗口）');
  const ok = await bridge.chainStop().catch(() => false);
  if (!ok) S.pushLog('WARN', '中断未生效：进程可能已经自己结束（等 chain-end 收尾）');
}

// stdout/stderr 每行：stderr 一律按告警档显示（脚本本身不带 stderr 的话通常为空）。
export function onChainLine(stream: string, raw: string): void {
  if (!chain.running) return;
  chain.lines++;
  const p = parseLine(raw);
  if (stream === 'err') {
    p.lv = 'WARN';
    p.html = p.html.replace(/class="lv [A-Z]+"/, 'class="lv WARN"');
  }
  S.pushLogs([p]);
}

export function onChainEnd(payload: { code: number; secs: number; lines: number }): { lv: 'OK' | 'WARN'; msg: string } | null {
  const was = chain.running;
  chain.running = false;
  hooks.setStreaming(false);
  syncChainButtons();
  if (!was) return null;
  const code = Number.isFinite(payload.code) ? payload.code : -1;
  const secs = Number.isFinite(payload.secs) ? payload.secs : (Date.now() - chain.t0) / 1000;
  const lines = Number.isFinite(payload.lines) ? payload.lines : chain.lines;
  const lv = code === 0 ? 'OK' : 'WARN';
  const msg = '=== 预演结束 · 退出码 ' + code + ' · ' + secs.toFixed(1) + 's · ' + lines + ' 行 ===';
  S.pushLog(lv, msg);
  hooks.refresh();
  return { lv, msg };
}
