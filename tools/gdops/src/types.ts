// 领域类型：步骤名/键名与真实内核同形；demo 模式的值为演示，live 模式由 summary/snapshot 灌。
// GameId 是插件注册表给的 id（不再是固定的两枚），渲染层不假设具体值。
export type GameId = string;
export type Level = 'INFO' | 'STEP' | 'OK' | 'WARN' | 'ERROR';
export type GameStatus = 'idle' | 'run' | 'ok' | 'fail' | 'stopped';

export interface Game {
  cn: string;
  code: string;
  plan: string;
  inc: boolean;
  done: number;
  status: GameStatus;
  steps: string[];   // 仅演示模式有逐步骤；live 任务链读 summary（门/探针/最近一轮）
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
  autoassist?: boolean; // 验收钩子：起壳时自动打开「助手」页（GDOPS_AUTOASSIST=1）
  autotab?: string;     // 验收钩子：自动打开时落在哪个页签（GDOPS_AUTOTAB=m9a）
  autosel?: string;     // 验收钩子：起壳后把「游戏路」选到这一路（GDOPS_AUTOSEL=ark|r1999）
  autodrawer?: string;  // 验收钩子：读数到位后自动打开右侧二级框（GDOPS_AUTODRAWER=1|m9a）
}

/* ── 只读实况快照（Rust live_snapshot 直出，字段名与后端 json 一一对应） ── */
export interface ProcRow { name: string; running: boolean }
export interface HistoryRow { time: string; game: string; plan: string; status: string; dur: string; detail: string }
export interface ShotRow { path: string; ageSecs: number; kind: string }
export interface SnapshotLog {
  name: string; path: string; ageSecs: number; size: number; files: number;
  head: string[]; tail: string[];
}
// state\running.json 的只读视图（Rust 解析；面板不自己读文件）。
export interface RunLock {
  found: boolean;
  alive: boolean;
  pid: number;
  started: string;
  games: string;
  tag: string;
  log: string;
  ageSecs: number;
}
// 控制命令的统一回执（Rust 侧 ok/error/... 原样）。
export interface ActResult {
  ok: boolean;
  error?: string;
  note?: string;
  pid?: number;
  carrier?: string;
  code?: number;
  lines?: string[];
  times?: string[];   // reschedule 回执：真正提交给脚本的时刻表
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
  run?: RunLock | null;
  supervision?: { file: string; ageSecs: number; beats: number } | null;
  vision?: { calls: number; ageSecs: number } | null;
  audit?: { file: string; ageSecs: number } | null;
}

/* ── 助手通道（MAA / M9A）：Rust assist_* 直出；数值与键名一一对应 ── */

export interface MaaPlan {
  defaultStage: string;
  finishAllSanityWith: string;
  keepQueuesEnabled: string[];
  disableQueues: string[];
  farmAppend: boolean;
  farmMaxTimesPerRound: number | null; // null = 缺键或旧值类型异常；面板只读，不猜成 0
  alsoEnableMainFight: boolean;
  quitAssistantAfter: boolean;
  closeArknightsAfter: boolean;
  launchArkFromHelper: boolean;
}

export interface MaaTask {
  idx: number;             // 在 MAA 配置 TaskQueue 数组里的下标（保存时用它定位，别用显示序号）
  type: string;            // TaskType（写回 settings 两枚名单时用它）
  name: string;            // MAA 配置里的中文名
  on: boolean;             // 类型级勾选态：这一类型下一轮是否被整类关掉（Fight 归决策，界面上只表达"是否禁用"）
  cur: boolean;            // MAA 配置里这一条自己的 IsEnable（Fight 的逐条真值，理智作战/剩余理智各一条）
  control: 'settings' | 'decision';  // Fight 归决策，其余归名单
  plan: string[];          // Fight 的 StagePlan
  cfg: Record<string, unknown>;      // 这一条的全量原样（右侧二级框按它渲染）
}

export interface MaaCatalogs {
  stages: string[];                              // resource/stages.json 的 code（618 枚）
  materials: { id: string; name: string }[];     // item_index.json 里 classifyType=MATERIAL
  operators: string[];                           // recruitment.json 的干员名（公招）
  tags: string[];                                // recruitment.json 的 tag 中文
  foldartals: string[];                          // 萨米密文板
  paradigms: string[];                           // 坍缩范式
  items: string[];                               // item_index 全部条目名（信用商店名单用）
  infrasts: string[];                            // resource/custom_infrast/<文件>.json（MAA 按路径读）
  rogueOpers: string[];                          // 各主题 recruitment.json 的开局干员并集
  rogueOpersByTheme?: Record<string, string[]>;  // 开局干员按主题分表，只收 is_start=true
}

export interface MaaState {
  catalogs?: MaaCatalogs;
  dirReady: boolean;
  exeReady: boolean;
  configReady: boolean;
  exeName: string;
  running: boolean;
  gameRunning: boolean;
  queue: string[];
  queueError: string | null;
  tasks: MaaTask[];
  tasksError: string | null;
  plan: MaaPlan;
}

export interface M9aOptionCase {
  name: string;
  label?: string;
  description?: string;
  options?: string[];       // 选中这一 case 后激活的子选项名
}

export interface M9aOptionInput {
  name: string;
  label?: string;
  default?: string;
  password?: boolean;
}

export interface M9aOptionDef {
  name: string;             // 配置身份：interface/tasks 选项对象的外层键
  label?: string;           // 本机 zh_cn 展示名；缺失时回退 name
  description?: string;
  type: 'switch' | 'select' | 'checkbox' | 'input' | 'hotkey';
  cases?: M9aOptionCase[];
  defaultCase?: string | string[];
  inputs?: M9aOptionInput[];
}

export interface M9aOptionNode {
  name: string;
  index?: number;
  selected_case?: string;
  selected_cases?: string[];
  data?: Record<string, string>;  // 仅回非密码 input 的现值；面板不编辑
  sub_options?: M9aOptionNode[];
}

export interface M9aTaskDef {
  name: string;
  entry: string;
  options: M9aOptionDef[];
}

export interface M9aCatalogs {
  resources: { name: string; controllers: string[] }[];
  controllers: string[];
  presets: { name: string; tasks: string[] }[];
  tasks: M9aTaskDef[];
  resourceOptions: Record<string, M9aOptionDef[]>;
  controllerOptions: Record<string, M9aOptionDef[]>;
  globalOptions: M9aOptionDef[];
}

export interface M9aInst {
  id: string;
  name: string;
  taskCount: number;
  tasks: string[];
  allTasks?: string[];     // 右侧二级框的完整任务表（卡面只用 tasks 的前五枚）
  currentTasks?: string[]; // 原样有序队列：显示名<|||>entry
  taskItems?: { name: string; entry?: string; option?: M9aOptionNode[] }[];
  resource?: string;
  controller?: string;
  preset?: string;
  keepEmptyTaskQueue?: boolean;
  resourceOptions?: M9aOptionNode[];
  controllerOptions?: M9aOptionNode[];
  globalOptions?: M9aOptionNode[];
  adbSerial: string;
  adbName: string;
}

export interface M9aPlan {
  idle: string;
  event: string;
  rerun: string;
  mainStoryRequiresAck: boolean;
  shopDoneStopsEvent: boolean;
  vmIndex: string;
  m9aCliArgs: boolean;
  m9aForceStart: boolean;
  closeEmulatorAfter: boolean;
  takeOverAssistant: boolean;
}

export interface M9aState {
  dirReady: boolean;
  exeReady: boolean;
  appReady: boolean;
  instancesReady: boolean;
  running: boolean;
  lastActiveName: string;
  lastActiveId: string;
  noAutoStart: string;
  instances: M9aInst[];
  current: M9aInst | null;
  catalogs?: M9aCatalogs;
  version: { current?: string; repo?: string; error?: string };
  conn: { serial: string; name: string } | null;
  connError: string | null;
  mumu: {
    available: boolean;
    android: boolean;
    process: boolean;
    name: string;
    index: string;
    vms?: { index: string; name: string }[];
    error?: string;
  };
  plan: M9aPlan;
}

// 右栏 runtime 开关：null = 配置缺键或不是布尔值；绝不把缺省值冒充现值。
export interface RuntimeFlags {
  [key: string]: boolean | null;
}

// 执行现场实时帧：独立于 6 秒快照；Rust 不回传窗口标题、ADB 序列号或本机路径。
export interface CaptureFrame {
  ok: boolean;
  state: 'ready' | 'dark' | 'connecting' | 'paused' | 'missing' | 'minimized' | 'offline' | 'failed' | 'unsupported';
  source?: 'window' | 'adb';
  image?: string;
  width?: number;
  height?: number;
  capturedAt?: number;
  meanLuma?: number;
  captureMs?: number;
  encodedBytes?: number;
  error?: string;
  note?: string;
}
export interface CaptureView {
  active: boolean;
  state: CaptureFrame['state'];
  source?: CaptureFrame['source'];
  updated: number;
  error: string;
  note: string;
  stale: boolean;
  fps?: number;
  captureMs?: number;
  encodedBytes?: number;
}

export interface AssistState {
  settingsPresent: boolean;
  runtimeFlags?: RuntimeFlags;
  maa: MaaState | null;
  m9a: M9aState | null;
  note?: string;
}