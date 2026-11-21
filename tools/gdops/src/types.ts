// 领域类型：步骤名/键名与真实内核同形，值当前为演示。
export type GameId = 'ark' | 'r1999';
export type Level = 'INFO' | 'STEP' | 'OK' | 'WARN' | 'ERROR';
export type GameStatus = 'idle' | 'run' | 'ok' | 'fail' | 'stopped';

export interface Game {
  cn: string;
  code: string;
  plan: string;
  inc: boolean;
  done: number;
  status: GameStatus;
  steps: string[];
}

export interface LogLine {
  lv: Level;
  html: string;
}

export interface Ev {
  t: 'game-start' | 'step' | 'step-done' | 'fx' | 'game-done' | 'round-done';
  g?: GameId;
  i?: number;
  lv?: Level;
  msg?: string;
  fx?: string;
}

export interface SwitchDef {
  label: string;
  key: string;
  on: boolean;
}

export interface AppInfo {
  app: string;
  version: string;
  platform: string;
  arch: string;
  autodry?: boolean;   // 验收钩子：起壳时自动跑一轮预演（GDOPS_AUTODRY=1）
}

/* ── 只读实况快照（Rust live_snapshot 直出，字段名与后端 json 一一对应） ── */
export interface ProcRow { name: string; running: boolean }
export interface HistoryRow { time: string; game: string; plan: string; status: string; dur: string; detail: string }
export interface ShotRow { path: string; ageSecs: number; kind: string }
export interface SnapshotLog {
  name: string; path: string; ageSecs: number; size: number; files: number;
  head: string[]; tail: string[];
}
export interface Snapshot {
  ok: boolean;
  error?: string;
  root?: string;
  version?: string;
  platform: string;
  arch: string;
  settingsPresent?: boolean;
  modules?: string[];
  procs?: ProcRow[];
  log?: SnapshotLog | null;
  history?: HistoryRow[];
  lastNotice?: { time: string; title: string; text: string; level: string } | null;
  arkGate?: { date: string; stage: string; entered: boolean; evidence: string } | null;
  plans?: { ark: string | null; r1999: string | null };
  shots?: ShotRow[];
  supervision?: { file: string; ageSecs: number; beats: number } | null;
  vision?: { calls: number; ageSecs: number } | null;
  audit?: { file: string; ageSecs: number } | null;
}