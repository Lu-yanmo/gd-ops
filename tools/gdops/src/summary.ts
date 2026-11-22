// 协议 v2：核内 emit-panel-summary 的 Lines 解析（字段位置见 docs/gdops-ui-bridge.md §5）。
// 规矩：解析只按固定列位切、尾部字段再拼回去（与 C# RowOf 同形）；不认识的族直接跳过，
// 不做"猜一行补一行"——协议漂移时要看得见，而不是被前端吃掉。
export interface SummaryRow { kind: string; game: string; text: string }

export interface SummaryFlags {
  ack: string;        // 确认旗标（单插件树：Ack；合并树里常为空）
  shopDone: string;   // 商店领空旗标（合并树：ArkShopDone；单插件树：ShopDone）
  hasGames: boolean;  // 入口声明了 -Games
  viaTask: boolean;   // run-via-task.ps1 在位
  admin: string[];    // 需要管理员的模块 id
}

export interface SummaryModel {
  proto: number;
  ids: string[];                 // HEADER 第 2 列：入口声明的游戏 id（空格分隔）
  entry: string;
  engine: string;
  root: string;
  games: { id: string; short: string }[];
  time: SummaryRow[];
  last: SummaryRow[];
  gates: SummaryRow[];
  env: SummaryRow[];
  probes: Record<string, SummaryRow[]>;
  sv: { kind: string; text: string; details: string[] } | null;
  flags: SummaryFlags;
  help: { label: string; text: string }[];
  at: number;
}

function row(f: string[], hasGame: boolean): SummaryRow {
  const kind = f[1] || 'info';
  let game = '';
  let from = 2;
  if (hasGame && f.length > 2) { game = f[2] || ''; from = 3; }
  return { kind, game, text: f.slice(from).join('|') };
}

export function parseSummary(env: unknown): SummaryModel | null {
  const e = env as { ok?: unknown; data?: { Lines?: unknown } } | null;
  if (!e || e.ok !== true || !e.data) return null;
  const raw = e.data.Lines;
  if (!Array.isArray(raw)) return null;
  const lines = raw.filter((x): x is string => typeof x === 'string');
  if (!lines.length) return null;

  const m: SummaryModel = {
    proto: 0, ids: [], entry: '', engine: '', root: '',
    games: [], time: [], last: [], gates: [], env: [], probes: {},
    sv: null,
    flags: { ack: '', shopDone: '', hasGames: false, viaTask: false, admin: [] },
    help: [],
    at: Date.now(),
  };
  for (const l of lines) {
    const f = l.split('|');
    switch (f[0]) {
      case 'PROTO': m.proto = parseInt(f[1] || '0', 10) || 0; break;
      case 'HEADER':
        m.ids = (f[1] || '').split(' ').filter(Boolean);
        // 六列：HEADER|ids|entry|engineName|engineTarget|root；root 在末列，缺列时退回 engineTarget。
        m.entry = f[2] || ''; m.engine = f[3] || ''; m.root = f[5] || f[4] || '';
        break;
      case 'GAME': m.games.push({ id: f[1] || '', short: f[2] || f[1] || '' }); break;
      case 'TIME': m.time.push(row(f, false)); break;
      case 'LAST': m.last.push(row(f, true)); break;
      case 'GATE': m.gates.push(row(f, true)); break;
      case 'ENV': m.env.push(row(f, false)); break;
      case 'PROBE': {
        const r = row(f, true);
        (m.probes[r.game] || (m.probes[r.game] = [])).push(r);
        break;
      }
      case 'SV': m.sv = { kind: f[1] || '', text: f[2] || '', details: f.slice(3) }; break;
      case 'FLAGS':
        m.flags = {
          ack: f[1] || '',
          shopDone: f[2] || '',
          hasGames: f[3] === 'yes',
          viaTask: f[4] === 'yes',
          admin: (f[5] || '').split(',').filter(Boolean),
        };
        break;
      case 'HELP': m.help.push({ label: f[1] || '', text: f.slice(2).join('|') }); break;
      default: break;   // 未知族：留痕在原型（at/proto），不猜
    }
  }
  m.games = m.games.filter((g) => g.id);
  return m;
}

// 时刻芯片只取 TIME 文本开头的 HH:mm（后半句是计划任务状态，留给调度面板正文）。
export function timesOf(m: SummaryModel): string[] {
  const out: string[] = [];
  for (const r of m.time) {
    const t = (r.text.match(/^(\d{1,2}:\d{2})/) || [])[1];
    if (t && !out.includes(t)) out.push(t);
  }
  return out;
}

export function registeredOf(m: SummaryModel): { ok: number; total: number } {
  const total = m.time.length;
  const ok = m.time.filter((r) => !/未注册/.test(r.text)).length;
  return { ok, total };
}

// 确认旗标：优先用入口自己声明的（FLAGS 第 2 列）；合并树那本入口声明的是 -Ack1999
// （daily-clear.ps1:39，PickFlag 只认 AckArk/Ack ⇒ FLAGS 为空），只在合并树这一种形态下兜底。
export function ackFlagFor(m: SummaryModel): string {
  if (m.flags.ack) return m.flags.ack;
  const ids = m.games.map((g) => g.id);
  if (m.entry === 'daily-clear.ps1' && ids.includes('r1999')) return 'Ack1999';
  return '';
}

// 商店旗标按"合并树/单插件树"两种形态折算：合并树 ArkShopDone ⇒ r1999 用 RShopDone/RShopReset；
// 单插件树 ShopDone ⇒ 本路 ShopDone/ShopReset（Reset 由 Done 后缀换名，和入口参数声明同形）。
export function shopFlagsFor(m: SummaryModel, game: string): { done: string; reset: string } | null {
  const f = m.flags.shopDone;
  if (!f) return null;
  if (/^ark/i.test(f)) {
    const done = game === 'ark' ? f : 'R' + f.slice(3);
    return { done, reset: done.replace(/Done$/, 'Reset') };
  }
  return { done: f, reset: f.replace(/Done$/, 'Reset') };
}

// 任务链面板（live）：没有逐步骤读数，就把这一路能查到的"门 / 探针 / 最近一轮"照实列出来。
// run 行只在这一路确实在跑时出现，读数是 pid + tag，不冒充阶段。
export interface ChainRow { cls: string; text: string; badge?: string }
export function chainRowsFor(
  m: SummaryModel,
  game: string,
  run: { alive: boolean; pid: number; tag: string; games: string } | null,
): ChainRow[] {
  const out: ChainRow[] = [];
  if (run && run.alive) {
    const inRound = !run.games || run.games === 'all' || run.games.split(/[, ]+/).includes(game);
    if (inRound) out.push({ cls: 'run', text: '本轮进行中 · pid ' + run.pid + ' · tag ' + (run.tag || '?') });
  }
  for (const r of m.gates.filter((x) => x.game === game)) out.push({ cls: kindCls(r.kind), text: r.text, badge: r.kind.toUpperCase() });
  for (const r of m.probes[game] || []) out.push({ cls: kindCls(r.kind), text: r.text, badge: r.kind.toUpperCase() });
  const last = m.last.filter((x) => x.game === game);
  for (const r of last) out.push({ cls: kindCls(r.kind), text: '最近一轮：' + r.text, badge: r.kind.toUpperCase() });
  return out;
}

// 行档只表达"这一路现在什么脸色"：ok=done（灰），err=fail（红），warn=retry（琥珀），其余中性。
function kindCls(kind: string): string {
  if (kind === 'ok') return 'done';
  if (kind === 'err') return 'fail';
  if (kind === 'warn') return 'retry';
  return '';
}

// 门文案 → 待决策卡片是否该出现（只做显示；写路径永远走 mark_flag）。
export function gateState(m: SummaryModel, game: string, needle: string): boolean {
  return m.gates.some((r) => r.game === game && r.text.includes(needle));
}