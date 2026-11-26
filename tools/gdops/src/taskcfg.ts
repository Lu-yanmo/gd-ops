// 右侧二级框里的内容：按 MAA 原应用的字段集与中文文案，渲染「这一条任务」的全部参数。
//
// 标签来源：MAA 官方 zh-cn 文案（MaaWpfGui resource）；字段集来源：现场 gui.new.json 的 TaskQueue。
// 控件纪律：**可编辑项一律是下拉/开关，没有文字输入框**。候选表来自 MAA 自带 resource：
//   关卡 stages.json、材料与条目 item_index.json、公招 tags/干员 recruitment.json、
//   肉鸽开局干员（各主题 recruitment.json priority[].opers[].name）、萨米密文板、坍缩范式、
//   自定义基建文件名；分队/职业组/刷开局奖励取自 MAA zh-cn 文案。没有候选表的自由文本项
//   只给「现值 / 空」，不编假选项。
// 两层写盘：
//   * 「面板计划源」组写 settings（ark./runtime.，走 assistSavePlan 的白名单行内替换）；
//   * 其余写 MAA 自己的 gui.new.json（assistMaaSaveTask：只动这几个键的字节 + 备份 + 回读校验）。
// 内核每轮会重写的 Fight 字段只读并挂徽标——手改下一轮就被覆盖，不如如实说。
import { esc } from './util';
import type { Bridge } from './bridge';
import type { ActResult, M9aCatalogs, M9aInst, M9aOptionCase, M9aOptionDef, M9aOptionNode, MaaTask, MaaState, MaaCatalogs } from './types';
import { openDrawer, type DrawerHandle } from './drawer';
import * as S from './state';

type Cfg = Record<string, unknown>;
type Kind = 'bool' | 'int' | 'enum' | 'list' | 'rooms' | 'ro' | 'single';
type Enc = 'arr' | ';' | ', ' | ',';
type Src = 'stages' | 'materials' | 'tags' | 'opers' | 'foldartals' | 'paradigms' | 'items' | 'infrasts' | 'squads' | 'roles' | 'awards' | 'modes';

interface F {
  k: string;
  label: string;
  kind?: Kind;
  opts?: [string, string][];
  src?: Src;
  enc?: Enc;
  nums?: number[];
  labels?: Record<string, string>;
  hint?: string;
  ro?: boolean;
  allowEmpty?: boolean;
  max?: number;
  when?: (cfg: Cfg) => boolean;
}

interface G {
  title: string;
  fields: F[];
}

/** 内核每轮会重写的 Fight 字段（Processes.PlanMaaQueue / ApplyFarm），抽屉里只读。 */
const KERNEL_FIGHT = new Set([
  'StagePlan',
  'EnableTargetDrop',
  'DropId',
  'DropCount',
  'IsInventoryTarget',
  'EnableTimesLimit',
  'TimesLimit',
  'UseMedicine',
  'UseStone',
  'UseExpiringMedicine',
]);

/** 身份/归属键：不在这里改（Name 是内核匹配依据，IsEnable 归队列勾选）。 */
const HIDDEN = new Set(['$type', 'TaskType', 'Name', 'IsEnable']);

const ROOM_CN: Record<string, string> = {
  Power: '发电站',
  Office: '办公室',
  Control: '控制中枢',
  Mfg: '制造站',
  Trade: '贸易站',
  Reception: '会客室',
  Recycle: '资源回收站',
  Dorm: '宿舍',
  Processing: '加工站',
  Training: '训练室',
  AssistantChange: '副手更换',
};

/* ── MAA zh-cn 里的固定候选（2026-11 版文案；值 = 配置里真正存的短名） ── */

const THEME_OPTS: [string, string][] = [
  ['Phantom', '傀影'],
  ['Mizuki', '水月'],
  ['Sami', '萨米'],
  ['Sarkaz', '萨卡兹'],
  ['JieGarden', '界园'],
  ['BlackFlow', '黑流树海'],
];

const MODE_BASE: [string, string][] = [
  ['Exp', '刷等级'],
  ['Investment', '刷源石锭'],
  ['Collectible', '刷开局'],
  ['Squad', '刷月度小队'],
  ['Exploration', '刷深入调查'],
];
const BLACKFLOW_MODES: [string, string][] = [
  ['Exp', '刷等级，快速飞三层'],
  ['Investment', '刷源石锭，投资完成后自动退出'],
  ['BlackFlowStartRun', '刷开局，刷到目标后退出并保留这一局'],
];
const MODE_EXTRA: Record<string, [string, string][]> = {
  Sami: [['CLP_PDS', '刷坍缩范式']],
  JieGarden: [['FindPlaytime', '刷常乐节点']],
};

const DRONE_OPTS: [string, string][] = [
  ['Money', '贸易站-龙门币'],
  ['SyntheticJade', '贸易站-合成玉'],
  ['CombatRecord', '制造站-经验书'],
  ['PureGold', '制造站-赤金'],
  ['OriginStone', '制造站-源石碎片'],
  ['Chip', '制造站-芯片组'],
  ['NotUse', '不使用无人机'],
];

const SQUAD_DEFAULT: Record<string, string[]> = {
  Phantom: ['集群分队', '矛头分队', '研究分队'],
  Mizuki: ['集群分队', '矛头分队', '心胜于物分队', '物尽其用分队', '以人为本分队', '研究分队'],
  Sami: ['集群分队', '矛头分队', '永恒狩猎分队', '生活至上分队', '科学主义分队', '特训分队'],
  Sarkaz_1: ['集群分队', '矛头分队', '博闻广记分队', '蓝图测绘分队', '点刺成锭分队', '拟态学者分队'],
  Sarkaz: ['集群分队', '矛头分队', '魂灵护送分队', '博闻广记分队', '蓝图测绘分队', '因地制宜分队', '异想天开分队', '点刺成锭分队', '拟态学者分队', '专业人士分队'],
  JieGarden: ['特勤分队', '高台突破分队', '地面突破分队', '游客分队', '司岁台分队', '天师府分队', '花团锦簇分队', '棋行险着分队', '岁影回音分队', '代理人分队', '知学分队', '商贾分队'],
  BlackFlow: ['特勤分队', '矛头分队', '高台突破分队', '地面突破分队', '本源研修分队', '文明开化分队', '开拓者分队', '多边贸易分队', '地质调查分队', '团结阵线分队', '踏足遍历分队'],
};
const SQUAD_COMMON = ['指挥分队', '后勤分队', '突击战术分队', '堡垒战术分队', '远程战术分队', '破坏战术分队'];

const ROLES_BASE: [string, string][] = [
  ['先手必胜', '先手必胜（先锋、狙击、特种）'],
  ['稳扎稳打', '稳扎稳打（重装、术师、狙击）'],
  ['取长补短', '取长补短（近卫、辅助、医疗）'],
  ['随心所欲', '随心所欲（三张随机）'],
];
const ROLES_EXTRA: [string, string][] = [
  ['灵活部署', '灵活部署（先锋、辅助、特种）'],
  ['坚不可摧', '坚不可摧（重装、术师、医疗）'],
];

/** RoguelikeCollectibleAward（flags 枚举，配置里存英文名、逗号串）。 */
const AWARDS_BASE: [string, string][] = [
  ['HotWater', '热水壶'], ['Shield', '盾'], ['Ingot', '锭'], ['Hope', '希望'], ['Random', '随机奖励'],
];
const AWARDS_EXTRA: Record<string, [string, string][]> = {
  Mizuki: [['Key', '钥匙'], ['Dice', '骰子']],
  Sarkaz: [['Idea', '构想']],
  JieGarden: [['Ticket', '票券']],
};

const BF_REWARDS: [string, string][] = [
  ['UnnumberedItem', '未编号物'], ['SurveyAdvance', '调查预付款'], ['SpaceLease', '空间租赁'],
  ['RegressionCompensation', '退行补偿'], ['ForestRide', '林间代步'], ['NestParasitism', '巢寄生'],
  ['Pegasus', '襁褓天马'], ['Hippogriff', '襁褓骏鹰'], ['Dragon', '襁褓巨龙'],
  ['Hydra', '襁褓九头蛇'], ['BaiZe', '襁褓白泽'], ['GoldenCrow', '襁褓金乌'],
];
const BF_REWARD_ORDER = ['Pegasus', 'SpaceLease', 'ForestRide', 'UnnumberedItem', 'RegressionCompensation', 'SurveyAdvance', 'NestParasitism', 'Hippogriff', 'Dragon', 'Hydra', 'BaiZe', 'GoldenCrow'];
const BF_ANIMALS: [string, string][] = [
  ['None', '不要求'], ['Cat', '襁褓中的猫'], ['Dog', '襁褓中的狗'],
  ['Cerberus', '襁褓三头犬'], ['FeatheredSerpent', '襁褓羽蛇'],
];
const BF_CULTIVATION: Record<string, string> = {
  Cat: '襁褓中的猫', Dog: '襁褓中的狗', Cerberus: '襁褓三头犬', FeatheredSerpent: '襁褓羽蛇',
};

const FAST_FLIGHT_LOCKED = new Set(['Squad', 'SquadCollectible', 'Roles', 'StartingOpers', 'BlackFlowEnding']);
const PREP_FLIGHT_LOCKED = new Set(['SquadCollectible']);
const DIFFICULTY_MAX: Record<string, number> = {
  Phantom: 15, Mizuki: 18, Sami: 15, Sarkaz: 18, JieGarden: 18, BlackFlow: 16,
};

function fastFlightLocked(cfg: Cfg): boolean {
  return cfg.Theme === 'BlackFlow' && cfg.Mode === 'Exp' && cfg.BlackFlowFastFlight === true;
}

function prepFlightLocked(cfg: Cfg): boolean {
  return cfg.Theme === 'BlackFlow' && cfg.Mode === 'BlackFlowStartRun' && cfg.BlackFlowStartRunFastFlight === true;
}

function flightLockReason(cfg: Cfg, key: string): string {
  if (fastFlightLocked(cfg) && FAST_FLIGHT_LOCKED.has(key)) return '快速飞三层锁定';
  if (prepFlightLocked(cfg) && PREP_FLIGHT_LOCKED.has(key)) return '准备局快速飞三层锁定';
  return '';
}

function flightLockedKeys(cfg: Cfg): Set<string> {
  return fastFlightLocked(cfg) ? FAST_FLIGHT_LOCKED : prepFlightLocked(cfg) ? PREP_FLIGHT_LOCKED : new Set<string>();
}

function difficultyNums(cfg: Cfg): number[] {
  const max = DIFFICULTY_MAX[String(cfg.Theme || '')];
  return max === undefined ? [-1, 2147483647] : [-1, ...Array.from({ length: max + 1 }, (_, i) => i), 2147483647];
}

function fieldVisible(f: F, cfg: Cfg): boolean {
  return !f.when || f.when(cfg);
}

const GENERIC_NUMS = [0, 1, 2, 3, 4, 5, 6, 8, 10, 20, 30, 50, 99, 100, 999];
const FARM_COUNTS = Array.from({ length: 30 }, (_, i) => i + 1);
const MINUTE_SECS = [60, 120, 180, 240, 300, 360, 420, 480, 540, 600, 720, 780, 900];

/** 每个任务类型：按 MAA 原界面的分组与顺序。字段在 cfg 里缺的自动不渲染。 */
function schema(type: string): G[] {
  switch (type.toLowerCase()) {
    case 'startup':
      return [
        {
          title: '启动',
          fields: [
            { k: 'AccountSwitchEnabled', label: '账号切换' },
            {
              k: 'AccountName',
              label: '切换到的账号',
              allowEmpty: true,
              hint: '填登录界面显示的内容（如 123****4567 的后四位）；空=禁用。无候选表，只能保留/清空',
            },
          ],
        },
      ];
    case 'recruit':
      return [
        {
          title: '公招',
          fields: [
            { k: 'MaxTimes', label: '每次最大招募次数', kind: 'int', nums: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9] },
            {
              k: 'ExtraTagMode',
              label: '多选 Tag 策略',
              kind: 'int',
              nums: [0, 1, 2],
              labels: { '0': '默认（不选额外 Tag）', '1': '总是选三个高星 Tag', '2': '尽可能多选高星 Tag' },
            },
            { k: 'PreferTagEnabled', label: '启用 3 星 Tag 倾向' },
            { k: 'Level3PreferTags', label: '3 星 Tag 倾向', kind: 'list', src: 'tags', enc: 'arr' },
            { k: 'PreserveTagEnabled', label: '启用保留 Tag' },
            { k: 'PreserveTagList', label: '保留 Tag', kind: 'list', src: 'tags', enc: 'arr' },
          ],
        },
        {
          title: '识别与确认',
          fields: [
            { k: 'RefreshLevel3', label: '自动刷新 3 星 Tags' },
            { k: 'ForceRefresh', label: '无招聘许可时继续尝试刷新 Tags' },
            { k: 'Level3Choose', label: '自动确认 3 星' },
            { k: 'Level4Choose', label: '自动确认 4 星' },
            { k: 'Level5Choose', label: '自动确认 5 星' },
            { k: 'Level6Choose', label: '自动确认 6 星' },
            { k: 'Level3RecruitmentPermitReserveEnabled', label: '保留招聘许可' },
            { k: 'Level3RecruitmentPermitReserve', label: '保留招聘许可数量', kind: 'int', nums: [0, 1, 2, 3, 4, 5, 6, 8, 10] },
            { k: 'Level3Time', label: '3 星招募时长（秒）', kind: 'int', nums: MINUTE_SECS },
            { k: 'Level4Time', label: '4 星招募时长（秒）', kind: 'int', nums: MINUTE_SECS },
          ],
        },
      ];
    case 'infrast':
      return [
        {
          title: '换班模式',
          fields: [
            {
              k: 'Mode',
              label: '基建模式',
              kind: 'enum',
              opts: [
                ['Normal', '常规模式'],
                ['Rotation', '队列轮换'],
                ['Custom', '自定义基建配置'],
              ],
            },
            { k: 'UsesOfDrones', label: '无人机用途', kind: 'enum', opts: DRONE_OPTS },
            { k: 'DormThreshold', label: '工作心情阈值（%）', kind: 'int', nums: [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100] },
            { k: 'DormTrustEnabled', label: '宿舍空余位置蹭信赖' },
            { k: 'OriginiumShardAutoReplenishment', label: '源石碎片自动补货' },
            { k: 'DormFilterNotStationed', label: '不将已进驻干员放入宿舍' },
            { k: 'ContinueTraining', label: '训练完成后继续专精当前技能' },
            { k: 'UseWeeklySchedule', label: '启用周计划' },
          ],
        },
        {
          title: '会客室',
          fields: [
            { k: 'ReceptionMessageBoard', label: '信息板收取信用' },
            { k: 'ReceptionClueExchange', label: '进行线索交流' },
            { k: 'SendClue', label: '赠送线索' },
          ],
        },
        {
          title: '自定义排班',
          fields: [
            {
              k: 'CustomFileType',
              label: '配置文件类型',
              kind: 'enum',
              opts: [
                ['user_defined', '用户自定义'],
                ['default', '默认配置'],
              ],
            },
            {
              k: 'Filename',
              label: '自定义基建配置文件',
              src: 'infrasts',
              allowEmpty: true,
              hint: '列的是 MAA 自带 resource/custom_infrast 里的文件名；MAA 新版按路径存，选完请在 MAA 里确认一次',
            },
            { k: 'PlanSelect', label: '基建计划序号（-1=自动）', kind: 'int', nums: [-1, 0, 1, 2, 3, 4, 5, 6, 7, 8] },
            {
              k: 'FiammettaRecoveryEnabled',
              label: '换班目标恢复后启用',
              hint: 'MAA 5 换班目标项；语义以 MAA 原界面为准',
            },
            { k: 'FiammettaTarget1', label: '换班目标干员 1', src: 'opers', allowEmpty: true },
            { k: 'FiammettaTarget2', label: '换班目标干员 2', src: 'opers', allowEmpty: true },
            { k: 'FiammettaTarget3', label: '换班目标干员 3', src: 'opers', allowEmpty: true },
          ],
        },
        {
          title: '体系',
          fields: [
            { k: 'UsePinusSylvestris', label: '启用「松」体系' },
            { k: 'UsePerceptionInformation', label: '启用「感知信息」体系' },
            { k: 'UseWorldlyPlight', label: '启用「世事无常」体系' },
            { k: 'UseAbyssalHunter', label: '启用「深海猎人」体系' },
          ],
        },
        {
          title: '房间启用',
          fields: [{ k: 'RoomList', label: '房间', kind: 'rooms' }],
        },
      ];
    case 'fight':
      return [
        {
          title: '理智与次数',
          fields: [
            { k: 'UseMedicine', label: '使用理智药' },
            { k: 'MedicineCount', label: '理智药数量', kind: 'int', nums: [0, 1, 2, 3, 5, 10, 20, 50, 100, 999] },
            { k: 'UseStone', label: '使用源石' },
            { k: 'StoneCount', label: '源石数量', kind: 'int', nums: [0, 1, 2, 3, 4, 5, 6, 10] },
            { k: 'UseExpiringMedicine', label: '无限吃 48 小时内过期的理智药' },
            { k: 'MedicineExpireDays', label: '临期判定天数', kind: 'int', nums: [0, 1, 2, 3, 5, 7] },
            { k: 'UseExpireMedicineForActivity', label: '活动时使用临期药' },
            { k: 'EnableTimesLimit', label: '限制战斗次数' },
            {
              k: 'TimesLimit',
              label: '战斗次数上限',
              kind: 'int',
              nums: [1, 2, 3, 4, 5, 6, 10, 20, 30, 50, 99, 100, 999, 9999, 999999],
            },
            { k: 'Series', label: '代理倍率（0=自动，-1=当前）', kind: 'int', nums: [-1, 0, 1, 2, 3, 4, 5, 6] },
            { k: 'IsDrGrandet', label: '博朗台模式', hint: '等 1 点理智恢复完再确认' },
          ],
        },
        {
          title: '关卡与材料',
          fields: [
            { k: 'StagePlan', label: '关卡', kind: 'list', src: 'stages', enc: 'arr', hint: '多关卡按顺序刷（由面板计划每轮重写）' },
            { k: 'IsStageManually', label: '手动输入关卡名' },
            { k: 'UseOptionalStage', label: '使用备选关卡' },
            { k: 'EnableTargetDrop', label: '指定材料（刷够即停）' },
            { k: 'DropId', label: '材料', src: 'materials' },
            {
              k: 'DropCount',
              label: '材料数量',
              kind: 'int',
              nums: [1, 2, 3, 5, 10, 20, 50, 99, 100, 999, 9999, 999999],
            },
            { k: 'IsInventoryTarget', label: '按仓库库存判断' },
            { k: 'HideUnavailableStage', label: '隐藏当日不开放关卡' },
            {
              k: 'StageResetMode',
              label: '未开放关卡重置为',
              kind: 'enum',
              opts: [
                ['Current', '当前 / 上次'],
                ['Ignore', '忽略 / 不变'],
              ],
            },
            { k: 'HideSeries', label: '隐藏代理倍率' },
            { k: 'UseWeeklySchedule', label: '启用周计划' },
            { k: 'UseStoneAllowSave', label: '允许使用源石保存状态', hint: '危险项，与原应用同一警告' },
          ],
        },
        {
          title: '剿灭',
          fields: [
            { k: 'UseCustomAnnihilation', label: '自定义剿灭关卡' },
            { k: 'AnnihilationStage', label: '剿灭关卡', src: 'stages', hint: '默认 Annihilation；候选是 stages.json 里的关卡代码' },
          ],
        },
      ];
    case 'mall':
      return [
        {
          title: '信用购物',
          fields: [
            { k: 'Shopping', label: '信用交易所自动购物' },
            { k: 'FirstList', label: '优先购买', kind: 'list', src: 'items', enc: ';', hint: '子串匹配，依次购买' },
            { k: 'BlackList', label: '黑名单', kind: 'list', src: 'items', enc: ';', hint: '子串匹配，命中即跳过' },
            { k: 'ShoppingIgnoreBlackListWhenFull', label: '信用溢出时无视黑名单' },
            { k: 'OnlyBuyDiscount', label: '只购买打折的信用商品' },
            { k: 'ReserveMaxCredit', label: '信用点低于 300 时停止购买' },
          ],
        },
        {
          title: '信用作战与访问',
          fields: [
            { k: 'CreditFight', label: '借助战打 OF-1 赚信用' },
            { k: 'CreditFightFormation', label: '编队序号', kind: 'int', nums: [0, 1, 2, 3, 4, 5, 6, 7, 8] },
            { k: 'CreditFightOnceADay', label: '一日只执行一次' },
            { k: 'VisitFriends', label: '访问好友' },
            { k: 'VisitFriendsOnceADay', label: '一日只执行一次' },
          ],
        },
        {
          title: 'MAA 记账（只读）',
          fields: [
            { k: 'CreditFightLastTime', label: '上次信用作战', ro: true },
            { k: 'VisitFriendsLastTime', label: '上次访问好友', ro: true },
            { k: 'IsCreditFightAvailable', label: '信用作战可用', ro: true },
            { k: 'IsVisitFriendsAvailable', label: '访问好友可用', ro: true },
          ],
        },
      ];
    case 'award':
      return [
        {
          title: '领取奖励',
          fields: [
            { k: 'Award', label: '每日/每周任务奖励' },
            { k: 'Mail', label: '所有邮件奖励' },
            { k: 'Orundum', label: '幸运墙每日合成玉' },
            { k: 'Mining', label: '限时开采许可' },
            { k: 'SpecialAccess', label: '周年赠送月卡奖励' },
            { k: 'SignInEvent', label: '签到活动' },
            { k: 'FreeGacha', label: '免费单抽', hint: '危险项：真的会抽卡，与原应用同一警告' },
          ],
        },
      ];
    case 'roguelike':
      return [
        {
          title: '主题与策略',
          fields: [
            { k: 'Theme', label: '肉鸽主题', kind: 'enum', opts: THEME_OPTS },
            {
              k: 'Difficulty',
              label: '难度',
              kind: 'int',
              labels: { '-1': '自动（不切换）', '2147483647': 'MAX（自动）' },
            },
            { k: 'Mode', label: '刷取策略', kind: 'enum', src: 'modes' },
            {
              k: 'StartCount',
              label: '开始探索次数上限',
              kind: 'int',
              nums: [1, 2, 3, 5, 10, 20, 50, 100, 999, 9999, 99999],
            },
            { k: 'Squad', label: '开局分队', src: 'squads', allowEmpty: true },
            { k: 'SquadCollectible', label: '刷开局使用分队', src: 'squads', allowEmpty: true },
            { k: 'Roles', label: '开局职业组', src: 'roles', allowEmpty: true },
            { k: 'StartingOpers', label: '开局干员', kind: 'list', hint: '最多三位；每项独立选择干员与是否使用助战' },
            { k: 'UseAdditionalStartingOpers', label: '启用追加开局干员' },
          ],
        },
        {
          title: '投资',
          fields: [
            { k: 'Investment', label: '投资源石锭' },
            { k: 'InvestCount', label: '投资数量', kind: 'int', nums: [0, 1, 2, 3, 5, 8, 10, 20, 50, 99, 999] },
            { k: 'InvestWithMoreScore', label: '投资模式启用购物、招募、进2层' },
            { k: 'StopWhenDepositFull', label: '储备源石锭达到上限时停止' },
          ],
        },
        {
          title: '开局与凹点',
          fields: [
            { k: 'CollectibleStartAwards', label: '刷开局期望奖励', kind: 'list', src: 'awards', enc: ', ' },
            { k: 'CollectibleShopping', label: '刷开局模式启用购物' },
            { k: 'StartWithEliteTwo', label: '凹直升精二' },
            { k: 'StartWithEliteTwoOnly', label: '只凹直升精二，不进行作战' },
            { k: 'StartWithSeed', label: '使用指定种子开局' },
            { k: 'Seed', label: '种子', allowEmpty: true, hint: '无候选表，只能保留/清空' },
            { k: 'RefreshTraderWithDice', label: '刷新商店（指路鳞）' },
            { k: 'UseSupportNonFriend', label: '可以使用非好友助战' },
          ],
        },
        {
          title: '萨米密文板',
          fields: [
            { k: 'SamiFirstFloorFoldartal', label: '凹第一层远见密文板，不进行作战' },
            { k: 'SamiFirstFloorFoldartals', label: '第一层密文板', kind: 'list', src: 'foldartals', enc: ';', max: 3, hint: '最多三个' },
            { k: 'SamiNewSquad2StartingFoldartal', label: '生活队凹开局密文板' },
            { k: 'SamiNewSquad2StartingFoldartals', label: '生活队开局密文板', kind: 'list', src: 'foldartals', enc: ';', max: 3, hint: '最多三个' },
            { k: 'SquadIsFoldartal', label: '分队按密文板处理' },
          ],
        },
        {
          title: '黑流树海',
          fields: [
            { k: 'BlackFlowEnding', label: '目标结局', kind: 'enum', opts: [['Ending1', '一结局 · 强制重启']], hint: '刷等级且快速飞三层开启时不能选择结局' },
            { k: 'BlackFlowEnding2', label: '二结局（旧字段）', ro: true },
            { k: 'BlackFlowEnding3', label: '三结局（旧字段）', ro: true },
            { k: 'BlackFlowFastFlight', label: '快速飞三层', hint: '仅刷等级策略生效；开启后锁定分队、职业组、开局干员与结局' },
            {
              k: 'BlackFlowStartRunFastFlight', label: '准备局快速飞三层',
              when: (cfg) => cfg.Theme === 'BlackFlow' && cfg.Mode === 'BlackFlowStartRun',
              hint: '准备局固定使用满天赋机械师，只招机械师并直飞第三层；仅锁定特勤分队，其余开局设置留给目标局',
            },
            { k: 'BlackFlowStartUtopia', label: '凹第一层实托邦', hint: '需难度 6 及以上' },
            { k: 'BlackFlowStartAnimal1', label: '襁褓动物 · 第一只', kind: 'enum', opts: BF_ANIMALS },
            { k: 'BlackFlowStartAnimal2', label: '襁褓动物 · 第二只', kind: 'enum', opts: BF_ANIMALS, hint: '两只不能同时要求襁褓羽蛇' },
            { k: 'BlackFlowCultivationTarget', label: '培育目标（旧字段）', ro: true },
            { k: 'BlackFlowStartRewards', label: '期望奖励（任一命中即停）', hint: '羽蛇奖励仅在选择襁褓羽蛇时生效；未生效的勾选会保留' },
            { k: 'BlackFlowStartRewardPriority', label: '开局奖励选择优先级', hint: '从上到下选择；十二项各一次，用每行上下箭头调序' },
            { k: 'CoreChar', label: '开局干员（旧字段）', ro: true, hint: '旧 JSON 字段；请使用上方的 StartingOpers' },
          ],
        },
        {
          title: '停止条件',
          fields: [
            { k: 'StopAtFinalBoss', label: '在第五层 BOSS 前暂停' },
            { k: 'StopWhenLevelMax', label: '满级后自动停止' },
          ],
        },
        {
          title: '月度 / 深入调查 / 常乐',
          fields: [
            { k: 'MonthlySquadAutoIterate', label: '月度小队自动切换' },
            { k: 'MonthlySquadCheckComms', label: '月度小队通讯' },
            { k: 'DeepExplorationAutoIterate', label: '深入调查自动切换' },
            {
              k: 'FindPlaytimeTarget',
              label: '目标常乐节点',
              kind: 'enum',
              opts: [
                ['Ling', '令 - 掷地有声'],
                ['Shu', '黍 - 种因得果'],
                ['Nian', '年 - 三缺一'],
              ],
            },
          ],
        },
        {
          title: '坍缩范式',
          fields: [
            { k: 'ExpectedCollapsalParadigms', label: '期望范式', kind: 'list', src: 'paradigms', enc: ';', hint: '留空=默认列表' },
          ],
        },
      ];
    default:
      return [];
  }
}

/* ── 候选表 ── */

function catOpts(src: Src, c?: MaaCatalogs, cfg?: Cfg): [string, string][] {
  const theme = String(cfg?.Theme || '');
  const knownTheme = THEME_OPTS.some((x) => x[0] === theme);
  switch (src) {
    case 'stages':
      return (c?.stages ?? []).map((x) => [x, x] as [string, string]);
    case 'materials':
      return (c?.materials ?? []).map((m) => [m.id, (m.name || m.id) + ' · ' + m.id] as [string, string]);
    case 'tags':
      return (c?.tags ?? []).map((x) => [x, x] as [string, string]);
    case 'opers': {
      const all = Array.from(new Set([...(c?.rogueOpers ?? []), ...(c?.operators ?? [])]));
      all.sort((a, b) => a.localeCompare(b, 'zh-CN'));
      return all.map((x) => [x, x] as [string, string]);
    }
    case 'foldartals':
      return (c?.foldartals ?? []).map((x) => [x, x] as [string, string]);
    case 'paradigms':
      return (c?.paradigms ?? []).map((x) => [x, x] as [string, string]);
    case 'items':
      return (c?.items ?? []).map((x) => [x, x] as [string, string]);
    case 'infrasts':
      // 值是 MAA 读取的相对路径；标签只露文件名，不把本机绝对路径塞进界面。
      return (c?.infrasts ?? []).map((x) => [x, x.split(/[\\/]/).pop() || x] as [string, string]);
    case 'modes':
      if (!knownTheme) return [];
      return theme === 'BlackFlow' ? BLACKFLOW_MODES : [...MODE_BASE, ...(MODE_EXTRA[theme] || [])];
    case 'squads': {
      const mode = String(cfg?.Mode || '');
      const baseKey = theme === 'Sarkaz' && mode === 'Investment' ? 'Sarkaz_1' : theme;
      const names = SQUAD_DEFAULT[baseKey] || [];
      const all = [...names, ...SQUAD_COMMON, ...(knownTheme && theme !== 'BlackFlow' ? ['高规格分队'] : [])];
      return all.map((x) => [x, x] as [string, string]);
    }
    case 'roles':
      return [...ROLES_BASE, ...(theme === 'JieGarden' || theme === 'BlackFlow' ? ROLES_EXTRA : [])];
    case 'awards': {
      if (!knownTheme) return [];
      const extra = AWARDS_EXTRA[theme] || [];
      return theme === 'JieGarden' ? [...AWARDS_BASE.filter((x) => x[0] !== 'Hope'), ...extra] : [...AWARDS_BASE, ...extra];
    }
    default:
      return [];
  }
}

/* ── 值 ↔ 控件 ── */

function isRo(task: MaaTask, f: F): boolean {
  return !!f.ro || (task.type === 'Fight' && KERNEL_FIGHT.has(f.k));
}

function fmt(v: unknown): string {
  if (v === null || v === undefined) return '—';
  if (Array.isArray(v)) return v.length ? v.map((x) => (typeof x === 'object' ? JSON.stringify(x) : String(x))).join(' / ') : '（空）';
  if (typeof v === 'object') return JSON.stringify(v);
  if (typeof v === 'boolean') return v ? '开' : '关';
  return String(v);
}

function inferKind(v: unknown): Kind {
  if (typeof v === 'boolean') return 'bool';
  if (typeof v === 'number') return 'int';
  if (Array.isArray(v)) return 'list';
  if (v !== null && typeof v === 'object') return 'ro';
  return 'single';
}

/** 单值下拉：现值不在候选里就补一条「现值」，绝不把现值弄丢。 */
function selHtml(attrs: string, cur: string, opts: [string, string][], blankLabel?: string): string {
  const list = opts.slice();
  if (!list.some((o) => o[0] === cur)) {
    list.unshift([cur, cur === '' ? (blankLabel || '（空）') : cur + '（现值）']);
  }
  return '<select class="drSel" ' + attrs + '>' +
    list.map((o) => '<option value="' + esc(o[0]) + '"' + (o[0] === cur ? ' selected' : '') + '>' + esc(o[1]) + '</option>').join('') +
    '</select>';
}

function intOpts(f: F, cur: unknown, cfg?: Cfg): [string, string][] {
  const base = f.k === 'Difficulty' && cfg ? difficultyNums(cfg) : f.nums ?? GENERIC_NUMS;
  const c = typeof cur === 'number' ? cur : Number(cur);
  const all = Number.isFinite(c) ? Array.from(new Set([...base, c])).sort((a, b) => a - b) : base.slice();
  return all.map((n) => [String(n), (f.labels && f.labels[String(n)]) || String(n)] as [string, string]);
}

function splitVals(v: unknown, enc: Enc): string[] {
  if (Array.isArray(v)) return v.filter((x) => typeof x === 'string' && String(x).trim().length > 0).map((x) => String(x));
  if (typeof v !== 'string' || !v) return [];
  const sep = enc === ';' ? /[;；]/ : /[,，]/;
  return v.split(sep).map((x) => x.trim()).filter((x) => x.length > 0);
}

/** 多项编辑器通用体：每行一枚下拉 + ×；末尾「＋ 添加一项」选中即插一行。
 *  attrs 决定归属（MAA 配置的 data-list 还是计划源 settings 的 data-plist）。 */
function listBox(vals: string[], opts: [string, string][], enc: Enc, attrs: string, canAdd: boolean): string {
  const rows = vals.map((v) =>
    '<div class="drLi">' + selHtml('data-lv="1"', v, opts) +
    '<button type="button" class="drRm" data-lrm="1" title="删除这一项" aria-label="删除这一项">×</button></div>').join('');
  const add = canAdd
    ? '<div class="drLi drAdd"><select class="drSel" data-ladd="1"><option value="">＋ 添加一项</option>' +
      opts.map((o) => '<option value="' + esc(o[0]) + '">' + esc(o[1]) + '</option>').join('') +
      '</select></div>'
    : '';
  if (!vals.length && !add) return '<div class="drRo">（空）</div>';
  return '<div class="drList"' + attrs + ' data-enc="' + esc(enc) + '">' + rows + add + '</div>';
}

function listHtml(task: MaaTask, f: F, cat?: MaaCatalogs): string {
  const enc = f.enc || 'arr';
  const vals = splitVals(task.cfg[f.k], enc);
  const opts = f.src ? catOpts(f.src, cat, task.cfg) : vals.map((v) => [v, v] as [string, string]);
  const full = f.max !== undefined && vals.length >= f.max;
  const hint = f.hint ? '<i class="drHint">' + esc(f.hint) + '</i>' : '';
  return listBox(vals, opts, enc, ' data-list="' + esc(f.k) + '"', !!f.src && !full) + hint;
}

type StartingOper = { Name: string; UseSupport?: boolean };

function validStartingOpers(v: unknown): StartingOper[] | null {
  return startingOpersForEdit(v, false);
}

// 草稿允许空位，便于主题重绘时保留用户尚未整理完的三行；保存仍按严格规则收口。
function startingOpersForEdit(v: unknown, allowEmpty: boolean): StartingOper[] | null {
  if (!Array.isArray(v) || v.length > 3) return null;
  const out: StartingOper[] = [];
  for (const item of v) {
    if (!item || typeof item !== 'object' || Array.isArray(item)) return null;
    const o = item as Record<string, unknown>;
    if (typeof o.Name !== 'string' || (!allowEmpty && !o.Name) || (o.UseSupport !== undefined && typeof o.UseSupport !== 'boolean')) return null;
    if (Object.keys(o).some((k) => k !== 'Name' && k !== 'UseSupport')) return null;
    out.push({ Name: o.Name, ...(o.UseSupport === undefined ? {} : { UseSupport: o.UseSupport }) });
  }
  return out;
}

function startingHtml(task: MaaTask, cat?: MaaCatalogs): string {
  const old = startingOpersForEdit(task.cfg.StartingOpers, true);
  if (!old) return '<div class="drRo">' + esc(fmt(task.cfg.StartingOpers)) + '</div><i class="drHint">对象结构异常，整项只读；请在 MAA 内修复</i>';
  const theme = String(task.cfg.Theme || '');
  const names = Array.from(new Set([...(cat?.rogueOpersByTheme?.[theme] || []), ...old.map((x) => x.Name)]))
    .sort((a, b) => a.localeCompare(b, 'zh-CN'));
  const opts: [string, string][] = names.map((x) => [x, x] as [string, string]);
  const locked = fastFlightLocked(task.cfg);
  const rows = Array.from({ length: 3 }, (_, i) => {
    const item = old[i] || { Name: '' };
    const dis = locked ? ' disabled' : '';
    return '<div class="drSoRow"><span>第 ' + (i + 1) + ' 位</span><div class="drSoGrid">' +
      selHtml('data-so-name="' + i + '"' + dis, item.Name, [['', '（不使用这位）'], ...opts]) +
      selHtml('data-so-support="' + i + '"' + dis, item.UseSupport === undefined ? '' : item.UseSupport ? '1' : '0',
        [['', '助战：沿用原值'], ['1', '使用助战'], ['0', '不使用助战']]) + '</div></div>';
  }).join('');
  return '<div class="drStarting" data-special="StartingOpers" data-dirty="0">' + rows + '</div>' +
    (locked ? '<i class="drHint">快速飞三层开启时锁定</i>' : '<i class="drHint">空名不占位；原值不在候选里的干员仍保留</i>');
}

function bfRewardsHtml(task: MaaTask): string {
  const v = task.cfg.BlackFlowStartRewards;
  if (typeof v !== 'string') return '<div class="drRo">' + esc(fmt(v)) + '</div>';
  const selected = new Set(splitVals(v, ', '));
  const serpent = task.cfg.BlackFlowStartAnimal1 === 'FeatheredSerpent' || task.cfg.BlackFlowStartAnimal2 === 'FeatheredSerpent';
  const opts = BF_REWARDS.map((o, i) => '<option value="' + esc(o[0]) + '"' + (selected.has(o[0]) ? ' selected' : '') +
    (i >= 6 && !serpent ? ' disabled' : '') + '>' + esc(o[1]) + (i >= 6 && !serpent ? '（需羽蛇）' : '') + '</option>').join('');
  const unknown = [...selected].filter((x) => x !== 'None' && x !== 'SerpentRewards' && !BF_REWARDS.some((o) => o[0] === x));
  const extra = unknown.map((x) => '<option disabled selected value="' + esc(x) + '">' + esc(x) + '（现值，未知项）</option>').join('');
  return '<select multiple class="drSel" size="7" data-special="BlackFlowStartRewards" data-dirty="0" data-bf-rewards="1">' + opts + extra + '</select>' +
    '<i class="drHint">按住 Ctrl 多选；不选 = None。羽蛇奖励未生效时仍保留勾选</i>';
}

function bfPriorityHtml(task: MaaTask): string {
  const v = task.cfg.BlackFlowStartRewardPriority;
  if (!Array.isArray(v) || v.some((x) => typeof x !== 'string' || !BF_REWARD_ORDER.includes(x))) {
    return '<div class="drRo">' + esc(fmt(v)) + '</div><i class="drHint">含未知值，顺序项整项只读</i>';
  }
  const used = new Set<string>();
  const values = Array.from({ length: 12 }, (_, i) => {
    const candidate = String(v[i] || '');
    if (candidate && !used.has(candidate)) { used.add(candidate); return candidate; }
    const next = BF_REWARD_ORDER.find((x) => !used.has(x)) || '';
    used.add(next);
    return next;
  });
  const rows = values.map((value, i) => {
    const opts = BF_REWARDS.map((o) => {
      const taken = values.includes(o[0]) && o[0] !== value;
      return '<option value="' + esc(o[0]) + '"' + (o[0] === value ? ' selected' : '') + (taken ? ' disabled' : '') + '>' + esc(o[1]) + '</option>';
    }).join('');
    return '<div class="drPriRow"><span>' + String(i + 1).padStart(2, '0') + '</span>' +
      '<span class="drPriBtns"><button type="button" class="drPriMove" data-pri-move="-1" data-pri-pos="' + i + '"' + (i === 0 ? ' disabled' : '') +
      ' aria-label="第 ' + (i + 1) + ' 项上移">↑</button><button type="button" class="drPriMove" data-pri-move="1" data-pri-pos="' + i + '"' + (i === 11 ? ' disabled' : '') +
      ' aria-label="第 ' + (i + 1) + ' 项下移">↓</button></span>' +
      '<select class="drSel" data-bf-priority-pos="' + i + '">' + opts + '</select></div>';
  }).join('');
  return '<div class="drPriority" data-special="BlackFlowStartRewardPriority" data-dirty="0">' + rows + '</div>';
}

function inputHtml(task: MaaTask, f: F, cat?: MaaCatalogs): string {
  const v = task.cfg[f.k];
  if (isRo(task, f)) {
    const badge = task.type === 'Fight' && KERNEL_FIGHT.has(f.k) ? '<b class="drK">每轮覆盖</b>' : '';
    const shown = f.k === 'BlackFlowCultivationTarget' && typeof v === 'string'
      ? (BF_CULTIVATION[v] || v) + ' · ' + v : fmt(v);
    return '<div class="drRo">' + esc(shown) + badge + '</div>';
  }
  const hint = f.hint ? '<i class="drHint">' + esc(f.hint) + '</i>' : '';
  const kind = f.kind || inferKind(v);
  if (f.k === 'StartingOpers') return startingHtml(task, cat) + hint;
  if (f.k === 'BlackFlowStartRewards') return bfRewardsHtml(task) + hint;
  if (f.k === 'BlackFlowStartRewardPriority') return bfPriorityHtml(task) + hint;
  if (flightLockReason(task.cfg, f.k)) {
    return '<div class="drRo">' + esc(fmt(v)) + '<b class="drK">' + esc(flightLockReason(task.cfg, f.k)) + '</b></div>' + hint;
  }
  if (kind === 'bool') {
    return '<div class="drCtl">' + selHtml('data-fk="' + esc(f.k) + '" data-ft="bool"', v === true ? '1' : '0', [['1', '开'], ['0', '关']]) + '</div>' + hint;
  }
  if (kind === 'int') {
    return '<div class="drCtl">' + selHtml('data-fk="' + esc(f.k) + '" data-ft="int"', String(typeof v === 'number' ? v : 0), intOpts(f, v, task.cfg)) + '</div>' + hint;
  }
  if (kind === 'enum') {
    const opts = f.src ? catOpts(f.src, cat, task.cfg) : f.opts || [];
    if (f.k === 'BlackFlowEnding' && flightLockReason(task.cfg, f.k)) {
      return '<div class="drRo">' + esc(fmt(v)) + '<b class="drK">' + esc(flightLockReason(task.cfg, f.k)) + '</b></div>' + hint;
    }
    if (f.k === 'BlackFlowStartAnimal1' || f.k === 'BlackFlowStartAnimal2') {
      const otherKey = f.k === 'BlackFlowStartAnimal1' ? 'BlackFlowStartAnimal2' : 'BlackFlowStartAnimal1';
      const opts2 = opts.map((o) => [o[0], task.cfg[otherKey] === 'FeatheredSerpent' && o[0] === 'FeatheredSerpent' ? o[1] + '（另一只已选）' : o[1]] as [string, string]);
      let html = selHtml('data-fk="' + esc(f.k) + '" data-ft="single" data-animal="1"', String(v ?? 'None'), opts2);
      if (task.cfg[otherKey] === 'FeatheredSerpent') html = html.replace('<option value="FeatheredSerpent"', '<option disabled value="FeatheredSerpent"');
      return '<div class="drCtl">' + html + '</div>' + hint;
    }
    return '<div class="drCtl">' + selHtml('data-fk="' + esc(f.k) + '" data-ft="single"', String(v ?? ''), opts) + '</div>' + hint;
  }
  if (kind === 'rooms') {
    const rooms = Array.isArray(v) ? v as { Room?: unknown; IsEnabled?: unknown }[] : [];
    return '<div class="drRooms">' + rooms.map((r, roomIndex) => {
      const key = String(r.Room || '');
      const on = r.IsEnabled !== false;
      return '<div class="drRoomRow"><span class="drRoomName">' + esc(ROOM_CN[key] || key) + '</span>' +
        selHtml('data-room="' + esc(key) + '" data-room-index="' + roomIndex + '"', on ? '1' : '0', [['1', '启用'], ['0', '禁用']]) + '</div>';
    }).join('') + '</div>' + hint;
  }
  if (kind === 'ro') {
    return '<div class="drRo">' + esc(fmt(v)) + '</div>';
  }
  if (kind === 'list') {
    return listHtml(task, f, cat);
  }
  // single：有候选表就列候选表；没有就只列现值/空（不编假选项）
  const opts = f.src ? catOpts(f.src, cat, task.cfg) : [];
  if (f.k === 'Filename' && typeof v === 'string' && v && !opts.some((o) => o[0] === v)) {
    // 自定义绝对路径可能是用户隐私；界面只给保留哨兵，不把路径回显到标签。
    return '<div class="drCtl">' + selHtml('data-fk="Filename" data-ft="single"', '__KEEP__',
      [['__KEEP__', '当前自定义路径（保留）'], ['', '（空 / 不设置）'], ...opts]) + '</div>' + hint;
  }
  const withEmpty: [string, string][] = f.allowEmpty ? [['', '（空 / 不设置）'], ...opts] : opts;
  return '<div class="drCtl">' + selHtml('data-fk="' + esc(f.k) + '" data-ft="single"', String(v ?? ''), withEmpty) + '</div>' + hint;
}

function rowHtml(task: MaaTask, f: F, cat?: MaaCatalogs, hidden = false): string {
  return '<div class="drF" data-cfg-key="' + esc(f.k) + '"' + (hidden ? ' style="display:none"' : '') + '><span class="drL">' + esc(f.label) + '</span><div class="drV">' + inputHtml(task, f, cat) + '</div></div>';
}

/* ── 面板计划源（写 settings，不是 MAA 配置） ── */

function planFields(task: MaaTask): F[] {
  const t = task.type.toLowerCase();
  if (t === 'fight') {
    return [
      { k: 'ark.defaultStage', label: '默认关卡', kind: 'list', src: 'stages', enc: ',', hint: '内核每轮按顺序挑第一关能打的写进 StagePlan；可排多关做轮换' },
      { k: 'ark.farmMaxTimesPerRound', label: '每轮最多补刷', kind: 'int', nums: FARM_COUNTS, labels: { '0': '0 · 旧值（内核实际按 1 次）' }, hint: '合法范围 1~30；不补刷请关闭「长草追加补刷」' },
      { k: 'ark.farmAppend', label: '长草追加补刷', kind: 'bool' },
      { k: 'ark.alsoEnableMainFight', label: '同时开主关卡', kind: 'bool' },
    ];
  }
  if (t === 'startup') {
    return [{ k: 'runtime.launchArkFromHelper', label: '由助手拉起端游', kind: 'bool', hint: '关掉后：「关 MAA + 端游」只关 MAA' }];
  }
  return [];
}

function planValue(maa: MaaState, key: string): unknown {
  const p = maa.plan as unknown as Record<string, unknown>;
  const short = key.replace(/^ark\./, '').replace(/^runtime\./, '');
  return p[short];
}

function planInput(key: string, f: F, maa: MaaState, cat?: MaaCatalogs): string {
  const v = planValue(maa, key);
  if ((f.kind || 'single') === 'bool') {
    return selHtml('data-sk="' + esc(key) + '" data-st="bool"', v === true ? '1' : '0', [['1', '开'], ['0', '关']]);
  }
  if (f.kind === 'int') {
    if (key === 'ark.farmMaxTimesPerRound' && (typeof v !== 'number' || !Number.isSafeInteger(v))) {
      return '<div class="drRo">现值类型异常，面板不覆盖；请先在配置里修正为 1~30 的整数</div>';
    }
    return selHtml('data-sk="' + esc(key) + '" data-st="int"', String(typeof v === 'number' ? v : 0), intOpts(f, v));
  }
  if (f.kind === 'list') {
    const enc = f.enc || ',';
    const vals = splitVals(v, enc);
    const opts = f.src ? catOpts(f.src, cat) : vals.map((x) => [x, x] as [string, string]);
    return listBox(vals, opts, enc, ' data-plist="' + esc(key) + '" data-orig="' + esc(String(v ?? '')) + '"', !!f.src);
  }
  const opts = f.src ? catOpts(f.src, cat) : [];
  return selHtml('data-sk="' + esc(key) + '" data-st="text"', String(v ?? ''), opts);
}

/* ── 抽屉装配 ── */

export function openMaaTaskDrawer(task: MaaTask, maa: MaaState, bridge: Bridge, after: () => void): void {
  const cat = maa.catalogs;
  const groups = schema(task.type);
  const covered = new Set<string>(HIDDEN);
  for (const g of groups) for (const f of g.fields) covered.add(f.k);
  const extra = Object.keys(task.cfg).filter((k) => !covered.has(k));

  let body = '';
  const plan = planFields(task);
  if (plan.length) {
    body += '<section class="drG"><h3>面板计划源 <small>写 settings（下一轮生效）</small></h3>' +
      plan.map((f) => '<div class="drF"><span class="drL">' + esc(f.label) + '</span><div class="drV">' + planInput(f.k, f, maa, cat) +
        (f.hint ? '<i class="drHint">' + esc(f.hint) + '</i>' : '') + '</div></div>').join('') +
      '</section>';
  }
  for (const g of groups) {
    const rows = g.fields.filter((f) => f.k in task.cfg).map((f) => rowHtml(task, f, cat, !fieldVisible(f, task.cfg))).join('');
    if (rows) body += '<section class="drG"><h3>' + esc(g.title) + '</h3>' + rows + '</section>';
  }
  if (extra.length) {
    body += '<section class="drG"><h3>高级参数 <small>MAA 原键名</small></h3>' +
      extra.map((k) => {
        const v = task.cfg[k];
        const kind = inferKind(v);
        return rowHtml(task, { k, label: k, kind }, cat);
      }).join('') + '</section>';
  }
  if (task.type === 'Fight') {
    body += '<div class="drNote">带「每轮覆盖」的项由内核每轮按理智计划重写（StagePlan / 材料 / 次数 / 理智药与源石）；在这里看的是现场值。</div>';
  }
  body += '<div class="drNote">候选表来自 MAA 自带 resource（关卡 / 条目 / 公招 Tag / 干员 / 密文板 / 坍缩范式 / 自定义基建）；没有候选表的项只给「现值 / 空」，不做文字输入。</div>';
  body += '<div class="drMsg" data-msg></div>';

  const h = openDrawer(
    task.name,
    task.type + ' · 队列第 ' + (task.idx + 1) + ' 条 · 改动写 MAA 的 gui.new.json',
    body,
    '<span class="drFootHint">MAA 在跑时拒写；改前备份到 backups/</span>' +
    '<button type="button" class="btn mini" data-cancel="1">取消</button>' +
    '<button type="button" class="btn mini" data-save="1">保存</button>',
    'maa:' + task.idx,
  );

  const msg = h.body.querySelector('[data-msg]') as HTMLElement;
  const say = (ok: boolean, text: string) => {
    msg.className = 'drMsg ' + (ok ? 'ok' : 'bad');
    msg.textContent = text;
  };

  // 主题/Mode/快速飞会改变候选与锁定面。重绘前先收当前 DOM 草稿，不能回到 task.cfg 旧快照。
  let lastDraft: Cfg = JSON.parse(JSON.stringify(task.cfg)) as Cfg;
  const fieldMap = new Map<string, F>();
  for (const g of groups) for (const f of g.fields) fieldMap.set(f.k, f);
  const dirtySpecials = (): Map<string, string> => new Map(Array.from(h.body.querySelectorAll<HTMLElement>('[data-special]'))
    .map((el) => [el.dataset.special || '', el.dataset.dirty || '0']));
  const collectDraft = (): Cfg => {
    const next: Cfg = JSON.parse(JSON.stringify(lastDraft)) as Cfg;
    for (const el of Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-fk]'))) {
      const k = el.dataset.fk || '';
      if (k === 'Filename' && el.value === '__KEEP__') continue;
      // 锁定控件已被替换成只读 DOM；这里只收仍可编辑的草稿，不按切换后的目标状态倒扣刚做的修改。
      if (el.dataset.ft === 'bool') next[k] = el.value === '1';
      else if (el.dataset.ft === 'int') next[k] = Math.trunc(Number(el.value));
      else next[k] = el.value;
    }
    for (const box of Array.from(h.body.querySelectorAll<HTMLElement>('[data-list]'))) {
      const k = box.dataset.list || '';
      const enc = (box.dataset.enc as Enc) || 'arr';
      const vals = Array.from(box.querySelectorAll<HTMLSelectElement>('select[data-lv]')).map((s) => s.value).filter((v) => v !== '');
      next[k] = enc === 'arr' ? Array.from(new Set(vals)) : Array.from(new Set(vals)).join(enc);
    }
    const starting = h.body.querySelector<HTMLElement>('[data-special="StartingOpers"]');
    if (starting?.dataset.dirty === '1' && starting.querySelector('[data-so-name]')) {
      const items: StartingOper[] = [];
      for (let i = 0; i < 3; i++) {
        const name = starting.querySelector<HTMLSelectElement>('[data-so-name="' + i + '"]')?.value || '';
        const support = starting.querySelector<HTMLSelectElement>('[data-so-support="' + i + '"]')?.value || '';
        items.push({ Name: name, ...(support === '' ? {} : { UseSupport: support === '1' }) });
      }
      next.StartingOpers = items;
    }
    const rewards = h.body.querySelector<HTMLElement>('[data-bf-rewards]');
    if (rewards?.dataset.dirty === '1') {
      const selected = Array.from(rewards.querySelectorAll<HTMLOptionElement>('option:checked')).map((x) => x.value);
      next.BlackFlowStartRewards = selected.length ? selected.join(', ') : 'None';
    }
    const priority = h.body.querySelector<HTMLElement>('[data-special="BlackFlowStartRewardPriority"]');
    if (priority?.dataset.dirty === '1') {
      next.BlackFlowStartRewardPriority = Array.from(priority.querySelectorAll<HTMLSelectElement>('[data-bf-priority-pos]')).map((x) => x.value);
    }
    const roomEls = Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-room]'));
    if (roomEls.length) {
      const old = Array.isArray(lastDraft.RoomList) ? lastDraft.RoomList as { Room?: unknown }[] : [];
      next.RoomList = old.map((r) => {
        const key = String(r.Room || '');
        const hit = roomEls.find((el) => el.dataset.room === key);
        return hit?.value === '0' ? { Room: key, IsEnabled: false } : { Room: key };
      });
    }
    return next;
  };
  const rerenderRogue = (keys: string[]): void => {
    const dirty = dirtySpecials();
    const prev = lastDraft;
    const next = collectDraft();
    const notes: string[] = [];
    const themeChanged = next.Theme !== prev.Theme;
    const modeChanged = next.Mode !== prev.Mode;
    if (themeChanged) {
      const modes = new Set(catOpts('modes', cat, next).map((x) => x[0]));
      if (!modes.has(String(next.Mode ?? ''))) {
        next.Mode = 'Exp';
        notes.push('新主题不支持原刷取策略，已改为刷等级');
      }
      if (next.Theme !== 'BlackFlow') {
        next.BlackFlowFastFlight = false;
        next.BlackFlowStartRunFastFlight = false;
      }
      if (typeof next.Difficulty === 'number') {
        const max = DIFFICULTY_MAX[String(next.Theme || '')];
        if (max !== undefined && next.Difficulty > max && next.Difficulty !== 2147483647) {
          next.Difficulty = max;
          notes.push('原难度超过新主题上限，已改为该主题最高难度');
        }
      }
      const roles = new Set(catOpts('roles', cat, next).map((x) => x[0]));
      if (typeof next.Roles === 'string' && next.Roles && !roles.has(next.Roles)) {
        next.Roles = '';
        notes.push('新主题不支持原开局职业组，已清空');
      }
      const awards = new Set(catOpts('awards', cat, next).map((x) => x[0]));
      if (Array.isArray(next.CollectibleStartAwards)) {
        next.CollectibleStartAwards = next.CollectibleStartAwards.filter((x) => awards.has(String(x)));
      } else if (typeof next.CollectibleStartAwards === 'string') {
        next.CollectibleStartAwards = splitVals(next.CollectibleStartAwards, ', ').filter((x) => awards.has(x)).join(', ');
      }
    }
    if (themeChanged || modeChanged) {
      const squads = new Set(catOpts('squads', cat, next).map((x) => x[0]));
      for (const k of ['Squad', 'SquadCollectible']) {
        if (typeof next[k] === 'string' && next[k] && !squads.has(next[k] as string)) {
          next[k] = '';
          notes.push((k === 'Squad' ? '开局分队' : '刷开局使用分队') + '在新主题/策略下不可用，已清空');
        }
      }
    }
    lastDraft = JSON.parse(JSON.stringify(next)) as Cfg;
    const view = { ...task, cfg: next };
    for (const key of keys) {
      const f = fieldMap.get(key);
      const row = Array.from(h.body.querySelectorAll<HTMLElement>('.drF')).find((el) => el.dataset.cfgKey === key);
      if (f && row) {
        row.style.display = fieldVisible(f, next) ? '' : 'none';
        const value = row.querySelector('.drV');
        if (value) value.innerHTML = inputHtml(view, f, cat);
      }
    }
    for (const el of Array.from(h.body.querySelectorAll<HTMLElement>('[data-special]'))) {
      if (dirty.get(el.dataset.special || '') === '1') el.dataset.dirty = '1';
    }
    if (notes.length) say(false, notes.join('；'));
  };

  // 填错后改了格，红边跟着撤掉。
  h.body.addEventListener('input', (e) => (e.target as HTMLElement).classList.remove('bad'));
  h.body.addEventListener('change', (e) => {
    const el = e.target as HTMLElement;
    el.classList.remove('bad');
    const special = el.closest<HTMLElement>('[data-special]');
    if (special) special.dataset.dirty = '1';
    if (el.dataset.animal !== undefined) {
      const otherKey = el.dataset.fk === 'BlackFlowStartAnimal1' ? 'BlackFlowStartAnimal2' : 'BlackFlowStartAnimal1';
      const other = h.body.querySelector<HTMLSelectElement>('[data-fk="' + otherKey + '"]');
      if (other) for (const op of Array.from(other.options)) {
        if (op.value === 'FeatheredSerpent') op.disabled = (el as HTMLSelectElement).value === 'FeatheredSerpent';
      }
      const serpent = (el as HTMLSelectElement).value === 'FeatheredSerpent' || other?.value === 'FeatheredSerpent';
      for (const box of Array.from(h.body.querySelectorAll<HTMLElement>('[data-bf-rewards]'))) {
        const opts = Array.from(box.querySelectorAll<HTMLOptionElement>('option'));
        opts.forEach((op, i) => { if (i >= 6 && i < 18) op.disabled = !serpent; });
      }
    }
    if (el.dataset.bfPriorityPos !== undefined) {
      const selects = Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-bf-priority-pos]'));
      const values = selects.map((x) => x.value);
      for (const select of selects) {
        for (const op of Array.from(select.options)) op.disabled = values.includes(op.value) && op.value !== select.value;
      }
    }
    if (el.dataset.fk === 'Theme') {
      rerenderRogue(['Mode', 'Difficulty', 'Squad', 'SquadCollectible', 'Roles', 'StartingOpers', 'CollectibleStartAwards', 'BlackFlowFastFlight', 'BlackFlowStartRunFastFlight', 'BlackFlowEnding']);
    } else if (el.dataset.fk === 'Mode') {
      rerenderRogue(['Squad', 'SquadCollectible', 'Roles', 'StartingOpers', 'BlackFlowStartRunFastFlight', 'BlackFlowEnding']);
    } else if (el.dataset.fk === 'BlackFlowFastFlight') {
      rerenderRogue(['Squad', 'SquadCollectible', 'Roles', 'StartingOpers', 'BlackFlowEnding']);
    } else if (el.dataset.fk === 'BlackFlowStartRunFastFlight') {
      rerenderRogue(['SquadCollectible']);
    }
    if (!(el instanceof HTMLSelectElement) || el.dataset.ladd === undefined || !el.value) return;
    // 「＋ 添加一项」：选中即落成一条新的值行，并把自己复位
    const wrap = el.closest('[data-list]') as HTMLElement | null;
    if (!wrap) return;
    const li = document.createElement('div');
    li.className = 'drLi';
    const s = document.createElement('select');
    s.className = 'drSel';
    s.dataset.lv = '1';
    for (const o of Array.from(el.options)) {
      if (!o.value) continue;
      const op = document.createElement('option');
      op.value = o.value;
      op.textContent = o.textContent;
      if (o.value === el.value) op.selected = true;
      s.appendChild(op);
    }
    li.appendChild(s);
    const rm = document.createElement('button');
    rm.type = 'button';
    rm.className = 'drRm';
    rm.dataset.lrm = '1';
    rm.title = '删除这一项';
    rm.setAttribute('aria-label', '删除这一项');
    rm.textContent = '×';
    li.appendChild(rm);
    wrap.insertBefore(li, el.closest('.drLi'));
    el.value = '';
  });

  (h.foot.querySelector('[data-cancel]') as HTMLElement).addEventListener('click', () => h.close());

  h.body.addEventListener('click', (e) => {
    const t = (e.target as HTMLElement).closest('button') as HTMLButtonElement | null;
    if (!t) return;
    if (t.dataset.lrm !== undefined) {
      t.closest('.drLi')?.remove();
      return;
    }
    if (t.dataset.priMove !== undefined && t.dataset.priPos !== undefined) {
      const root = t.closest<HTMLElement>('[data-special="BlackFlowStartRewardPriority"]');
      const selects = Array.from(root?.querySelectorAll<HTMLSelectElement>('[data-bf-priority-pos]') || []);
      const pos = Number(t.dataset.priPos);
      const to = pos + Number(t.dataset.priMove);
      if (!selects.length || !selects[pos] || !selects[to]) return;
      const values = selects.map((x) => x.value);
      [values[pos], values[to]] = [values[to], values[pos]];
      selects.forEach((select, i) => {
        select.value = values[i];
        for (const op of Array.from(select.options)) op.disabled = values.includes(op.value) && op.value !== select.value;
      });
      root!.dataset.dirty = '1';
    }
  });

  (h.foot.querySelector('[data-save]') as HTMLButtonElement).addEventListener('click', () => {
    void save(h, task, maa, bridge, after, say);
  });
}

interface Edit { key: string; value: unknown }

async function save(
  h: DrawerHandle,
  task: MaaTask,
  maa: MaaState,
  bridge: Bridge,
  after: () => void,
  say: (ok: boolean, text: string) => void,
): Promise<void> {
  const btn = h.foot.querySelector('[data-save]') as HTMLButtonElement;
  if (btn.disabled) return;

  const theme = h.body.querySelector<HTMLSelectElement>('[data-fk="Theme"]')?.value ?? task.cfg.Theme;
  const mode = h.body.querySelector<HTMLSelectElement>('[data-fk="Mode"]')?.value ?? task.cfg.Mode;
  const fast = h.body.querySelector<HTMLSelectElement>('[data-fk="BlackFlowFastFlight"]')?.value;
  const prepFast = h.body.querySelector<HTMLSelectElement>('[data-fk="BlackFlowStartRunFastFlight"]')?.value;
  const saveCfg: Cfg = {
    ...task.cfg, Theme: theme, Mode: mode,
    BlackFlowFastFlight: fast === undefined ? task.cfg.BlackFlowFastFlight === true : fast === '1',
    BlackFlowStartRunFastFlight: prepFast === undefined ? task.cfg.BlackFlowStartRunFastFlight === true : prepFast === '1',
  };
  const locked = flightLockedKeys(saveCfg);

  const edits: Edit[] = [];
  for (const el of Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-sk]'))) {
    const key = el.dataset.sk || '';
    const st = el.dataset.st;
    const before = planValue(maa, key);
    let value: unknown;
    if (st === 'bool') value = el.value === '1';
    else if (st === 'int') {
      const n = Number(el.value);
      if (!Number.isSafeInteger(n)) { el.classList.add('bad'); say(false, key + '：要整数'); return; }
      if (key === 'ark.farmMaxTimesPerRound' && !(n >= 1 && n <= 30) && !(n === 0 && before === 0)) {
        el.classList.add('bad'); say(false, key + '：合法范围是 1~30；旧值 0 只能原样保留'); return;
      }
      value = n;
    } else value = el.value;
    // 未变化的计划项不提交；异常旧值也不能借保存别的项被覆盖。
    if (JSON.stringify(value) !== JSON.stringify(before)) edits.push({ key, value });
  }

  // 计划源里的多项（默认关卡）：settings 存逗号串，按 DOM 顺序拼回去；空值拒写。
  for (const box of Array.from(h.body.querySelectorAll<HTMLElement>('[data-plist]'))) {
    const key = box.dataset.plist || '';
    const enc = (box.dataset.enc as Enc) || ',';
    const vals = Array.from(box.querySelectorAll<HTMLSelectElement>('select[data-lv]'))
      .map((s) => s.value).filter((v) => v !== '');
    const now = Array.from(new Set(vals)).join(enc);
    if (!now) { say(false, key + '：至少留一关'); return; }
    const orig = splitVals(box.dataset.orig || '', enc).join(enc);
    if (now !== orig) edits.push({ key, value: now });
  }

  const patch: Cfg = {};
  const saveFields = new Map<string, F>();
  for (const group of schema(task.type)) for (const field of group.fields) saveFields.set(field.k, field);
  for (const el of Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-fk]'))) {
    const k = el.dataset.fk || '';
    if (KERNEL_FIGHT.has(k) && task.type === 'Fight') continue;
    if (locked.has(k)) continue;
    const field = saveFields.get(k);
    if (field && !fieldVisible(field, saveCfg)) continue;
    if (k === 'Filename' && el.value === '__KEEP__') continue;
    const ft = el.dataset.ft;
    let val: unknown;
    if (ft === 'bool') {
      val = el.value === '1';
    } else if (ft === 'int') {
      const n = Number(el.value);
      if (!Number.isFinite(n)) { el.classList.add('bad'); say(false, k + '：要整数'); return; }
      val = Math.trunc(n);
    } else {
      val = el.value;
    }
    el.classList.remove('bad');
    if (JSON.stringify(val) !== JSON.stringify(task.cfg[k])) patch[k] = val;
  }

  // 多项列表：值行按 DOM 顺序收，空/重复丢掉；落盘形态按 data-enc（数组 / 分号串 / 逗号串）。
  for (const box of Array.from(h.body.querySelectorAll<HTMLElement>('[data-list]'))) {
    const k = box.dataset.list || '';
    if (KERNEL_FIGHT.has(k) && task.type === 'Fight') continue;
    if (locked.has(k)) continue;
    const enc = (box.dataset.enc as Enc) || 'arr';
    const vals = Array.from(box.querySelectorAll<HTMLSelectElement>('select[data-lv]'))
      .map((s) => s.value).filter((v) => v !== '');
    const now = Array.from(new Set(vals));
    if (enc === 'arr') {
      const before = Array.isArray(task.cfg[k]) ? splitVals(task.cfg[k], enc) : [];
      if (JSON.stringify(now) !== JSON.stringify(before)) patch[k] = now;
    } else {
      const before = typeof task.cfg[k] === 'string' ? String(task.cfg[k]) : splitVals(task.cfg[k], enc).join(enc);
      if (now.join(enc) !== before) patch[k] = now.join(enc);
    }
  }

  // 黑流两只动物互斥；快速飞三层锁住的控件不会进 DOM，因此不会误写。
  const a1 = h.body.querySelector<HTMLSelectElement>('[data-fk="BlackFlowStartAnimal1"]');
  const a2 = h.body.querySelector<HTMLSelectElement>('[data-fk="BlackFlowStartAnimal2"]');
  if (a1 && a2 && a1.value === 'FeatheredSerpent' && a2.value === 'FeatheredSerpent') {
    a2.classList.add('bad');
    say(false, '两只襁褓动物不能同时选择襁褓羽蛇');
    return;
  }

  // 专用对象/Flags/有序数组：只有用户真正动过才收，避免打开抽屉就规范化旧配置。
  const starting = h.body.querySelector<HTMLElement>('[data-special="StartingOpers"]');
  if (!locked.has('StartingOpers') && starting?.dataset.dirty === '1') {
    const items: StartingOper[] = [];
    let emptySeen = false;
    for (let i = 0; i < 3; i++) {
      const name = starting.querySelector<HTMLSelectElement>('[data-so-name="' + i + '"]')?.value || '';
      const support = starting.querySelector<HTMLSelectElement>('[data-so-support="' + i + '"]')?.value || '';
      if (!name) { emptySeen = true; continue; }
      if (emptySeen) { say(false, 'StartingOpers：空位后面的干员不会生效，请把干员移到前面'); return; }
      if (items.some((x) => x.Name === name)) { say(false, 'StartingOpers：同一位干员只能选一次'); return; }
      items.push({ Name: name, ...(support === '' ? {} : { UseSupport: support === '1' }) });
    }
    if (JSON.stringify(items) !== JSON.stringify(validStartingOpers(task.cfg.StartingOpers))) patch.StartingOpers = items;
  }

  const rewards = h.body.querySelector<HTMLElement>('[data-bf-rewards]');
  if (rewards?.dataset.dirty === '1') {
    const selected = Array.from(rewards.querySelectorAll<HTMLOptionElement>('option:checked')).map((x) => x.value);
    if (selected.some((x) => !BF_REWARDS.some((o) => o[0] === x))) {
      say(false, '期望奖励含未知值；请先在 MAA 内清理，面板不猜它的含义');
      return;
    } else {
      const value = selected.length ? selected.join(', ') : 'None';
      if (value !== task.cfg.BlackFlowStartRewards) patch.BlackFlowStartRewards = value;
    }
  }

  const priority = h.body.querySelector<HTMLElement>('[data-special="BlackFlowStartRewardPriority"]');
  if (priority?.dataset.dirty === '1') {
    const values = Array.from(priority.querySelectorAll<HTMLSelectElement>('[data-bf-priority-pos]')).map((x) => x.value);
    if (values.length !== 12 || new Set(values).size !== 12 || values.some((x) => !BF_REWARD_ORDER.includes(x))) {
      say(false, '开局奖励优先级必须十二项各一次');
      return;
    } else if (JSON.stringify(values) !== JSON.stringify(task.cfg.BlackFlowStartRewardPriority)) {
      patch.BlackFlowStartRewardPriority = values;
    }
  }

  // 房间矩阵：只改用户实际切换的行；未动房间的额外键与显式 IsEnabled:true 原样保留。
  const roomEls = Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-room]'));
  if (roomEls.length) {
    const old = Array.isArray(task.cfg.RoomList) ? task.cfg.RoomList : [];
    let rooms: unknown[] | null = null;
    for (const el of roomEls) {
      const index = Number(el.dataset.roomIndex);
      const original = old[index];
      if (!Number.isInteger(index) || index < 0 || !original || typeof original !== 'object' || Array.isArray(original)) continue;
      const row = original as Record<string, unknown>;
      const on = row.IsEnabled !== false;
      const nextOn = el.value === '1';
      if (on === nextOn) continue;
      if (!rooms) rooms = old.slice();
      const next = { ...row };
      if (nextOn) delete next.IsEnabled;
      else next.IsEnabled = false;
      rooms[index] = next;
    }
    if (rooms && JSON.stringify(rooms) !== JSON.stringify(old)) patch.RoomList = rooms;
  }

  if (!edits.length && !Object.keys(patch).length) { say(true, '跟原来一样，没有写盘'); return; }

  btn.disabled = true;
  btn.textContent = '保存中…';
  say(true, '正在写…');
  const errs: string[] = [];
  if (edits.length) {
    const r = await bridge.assistSavePlan(edits).catch((e) => ({ ok: false, error: String(e) }) as { ok: boolean; error?: string });
    if (r.ok) S.pushLog('OK', '计划源已写 settings（' + edits.map((x) => x.key).join(' / ') + '）');
    else errs.push('计划源：' + (r.error || '失败'));
  }
  if (Object.keys(patch).length) {
    const r = await bridge.assistMaaSaveTask(task.idx, task.type, task.name, patch)
      .catch((e) => ({ ok: false, error: String(e) }) as { ok: boolean; error?: string; note?: string });
    if (r.ok) {
      S.pushLog('OK', task.name + ' 参数已写 MAA 配置（' + Object.keys(patch).join(' / ') + '）' + (r.note ? ' · ' + r.note : ''));
    } else {
      errs.push('MAA 配置：' + (r.error || '失败'));
    }
  }
  if (errs.length) {
    btn.disabled = false;
    btn.textContent = '保存';
    say(false, errs.join('；'));
    S.pushLog('ERROR', task.name + ' 保存失败：' + errs.join('；'));
    return;
  }
  h.close();
  after();
}

/* ── M9A 实例：全下拉配置框 ── */

type M9aSource = 'task' | 'resource' | 'controller' | 'global';
export interface M9aQueueTask { key: string; name: string; entry: string }
interface M9aQueueDraft {
  source: string;
  observed: string;
  queue: M9aQueueTask[];
  dirty: boolean;
  saved: boolean;
}
const m9aQueueDrafts = new Map<string, M9aQueueDraft>();
const m9aQueueSaves = new Set<string>();
interface M9aOptionEdit {
  source: M9aSource;
  task?: number;
  path: string[];
  selected_case?: string;
  selected_cases?: string[];
}

function cloneNodes(nodes?: M9aOptionNode[]): M9aOptionNode[] {
  return nodes ? JSON.parse(JSON.stringify(nodes)) as M9aOptionNode[] : [];
}

export function parseM9aHistory(key: string): M9aQueueTask | null {
  if (key.includes('<|||>')) {
    if (key.split('<|||>').length !== 2) return null;
    const [name, entry] = key.split('<|||>');
    return name && entry ? { key, name, entry } : null;
  }
  if (key.includes('<') || key.split(':').length !== 2) return null;
  const [name, entry] = key.split(':');
  return name && entry ? { key, name, entry } : null;
}

/** 任务身份只认名称与入口：旧 `名称:entry` 与目录的 `名称<|||>entry` 必须视作同一项。 */
export function m9aQueueIdentity(task: Pick<M9aQueueTask, 'name' | 'entry'>): string {
  return task.name + '<|||>' + task.entry;
}

function m9aQueueSource(inst: M9aInst): string {
  return JSON.stringify(inst.currentTasks ?? inst.allTasks ?? inst.tasks ?? []);
}

function m9aInitialQueue(inst: M9aInst): M9aQueueTask[] {
  return (inst.currentTasks ?? inst.allTasks ?? inst.tasks ?? []).map((key) => {
    const parsed = parseM9aHistory(key);
    if (parsed) return parsed;
    return { key, name: key, entry: '' };
  });
}

function m9aQueueDraft(inst: M9aInst): M9aQueueDraft {
  const source = m9aQueueSource(inst);
  let draft = m9aQueueDrafts.get(inst.id);
  if (!draft) {
    draft = { source, observed: source, queue: m9aInitialQueue(inst), dirty: false, saved: false };
    m9aQueueDrafts.set(inst.id, draft);
    return draft;
  }
  if (source === draft.source) {
    draft.observed = source;
    draft.saved = false;
  } else if (draft.saved && source === draft.observed) {
    // 保存成功但 reload 还没回来：旧 inst 对象不能把刚保存的队列回退掉。
  } else if (!draft.dirty) {
    draft = { source, observed: source, queue: m9aInitialQueue(inst), dirty: false, saved: false };
    m9aQueueDrafts.set(inst.id, draft);
  } else {
    draft.observed = source;
  }
  return draft;
}

export function m9aQueueFor(inst: M9aInst): M9aQueueTask[] {
  return m9aQueueDraft(inst).queue;
}

export function m9aQueueIsDirty(inst: M9aInst): boolean {
  return m9aQueueDraft(inst).dirty;
}

export function m9aQueueSet(inst: M9aInst, queue: M9aQueueTask[]): void {
  const draft = m9aQueueDraft(inst);
  draft.queue = queue.map((t) => ({ ...t }));
  draft.dirty = JSON.stringify(draft.queue.map((t) => t.key)) !== draft.source;
  draft.saved = false;
}

/** 纯函数收口勾选语义：按身份防陈旧下标错删，按身份防旧新格式重复追加。 */
export function m9aQueueToggle(queue: M9aQueueTask[], key: string, index: number): M9aQueueTask[] | null {
  const next = queue.map((t) => ({ ...t }));
  if (index >= 0) {
    if (!Number.isInteger(index) || index >= next.length || m9aQueueIdentity(next[index]) !== key) return null;
    next.splice(index, 1);
    return next;
  }
  const task = parseM9aHistory(key);
  if (!task || next.some((t) => m9aQueueIdentity(t) === key)) return null;
  next.push(task);
  return next;
}

export function m9aQueueCommit(inst: M9aInst, keys?: string[]): void {
  const draft = m9aQueueDrafts.get(inst.id);
  if (!draft) return;
  // 只把已落盘快照立为新基线；保存请求在途时的新编辑必须留在草稿里，不能被旧快照覆盖。
  draft.source = JSON.stringify(keys || draft.queue.map((t) => t.key));
  draft.dirty = JSON.stringify(draft.queue.map((t) => t.key)) !== draft.source;
  draft.saved = !draft.dirty;
}

export function m9aQueueSaveInFlight(inst: M9aInst): boolean {
  return m9aQueueSaves.has(inst.id);
}

export function m9aQueueSaveBegin(inst: M9aInst): boolean {
  if (m9aQueueSaves.has(inst.id)) return false;
  m9aQueueSaves.add(inst.id);
  return true;
}

export function m9aQueueSaveEnd(inst: M9aInst): void {
  m9aQueueSaves.delete(inst.id);
}

export async function saveM9aQueueDraft(inst: M9aInst, bridge: Bridge): Promise<ActResult> {
  const draft = m9aQueueDraft(inst);
  if (!draft.dirty) return { ok: true, note: '任务队列没有改动' };
  if (!m9aQueueSaveBegin(inst)) return { ok: false, error: '这个实例正在保存，请等上一笔完成' };
  try {
    const currentTasks = draft.queue.map((t) => t.key);
    if (currentTasks.some((key) => !parseM9aHistory(key))) {
      return { ok: false, error: '队列里有无法识别的旧任务格式，请先移除该项再保存' };
    }
    const r = await bridge.assistM9aSaveInstance(inst.id, inst.name, { name: inst.name, currentTasks });
    if (r.ok) m9aQueueCommit(inst, currentTasks);
    return r;
  } finally {
    m9aQueueSaveEnd(inst);
  }
}

function m9aNode(nodes: M9aOptionNode[], name: string): M9aOptionNode | undefined {
  return nodes.find((n) => n.name === name);
}

function m9aOne(def: M9aOptionDef, node?: M9aOptionNode): string {
  const cases = def.cases || [];
  if (node?.selected_case && cases.some((c) => c.name === node.selected_case)) return node.selected_case;
  if (typeof node?.index === 'number' && cases[node.index]) return cases[node.index].name;
  if (typeof def.defaultCase === 'string' && cases.some((c) => c.name === def.defaultCase)) return def.defaultCase;
  return cases[0]?.name || '';
}

function m9aMany(def: M9aOptionDef, node?: M9aOptionNode): string[] {
  // 目录外的已落盘 case 不展示、不可编辑，但不能在修改已知项时被前端过滤掉。
  if (node?.selected_cases !== undefined) return node.selected_cases.slice();
  const valid = new Set((def.cases || []).map((c) => c.name));
  return Array.isArray(def.defaultCase) ? def.defaultCase.filter((x) => valid.has(x)) : [];
}

function m9aUnknownCases(def: M9aOptionDef, node?: M9aOptionNode): string[] {
  const valid = new Set((def.cases || []).map((c) => c.name));
  return m9aMany(def, node).filter((x) => !valid.has(x));
}

function m9aMergeKnownCases(def: M9aOptionDef, node: M9aOptionNode | undefined, selected: string[]): string[] {
  const valid = new Set((def.cases || []).map((c) => c.name));
  const chosen = new Set(selected.filter((x) => valid.has(x)));
  // 保留旧数组里未知项与原已知项的位置；只移除取消的已知项，新勾选项按操作顺序追加。
  const out = m9aMany(def, node).filter((x) => !valid.has(x) || chosen.has(x));
  for (const name of selected) if (valid.has(name) && !out.includes(name)) out.push(name);
  return out;
}

function m9aChildren(def: M9aOptionDef, node?: M9aOptionNode): string[] {
  const selected = def.type === 'checkbox' ? m9aMany(def, node) : [m9aOne(def, node)];
  return selected.flatMap((name) => (def.cases || []).find((c) => c.name === name)?.options || []);
}

function m9aTaskDef(cat: M9aCatalogs, task: M9aQueueTask): M9aOptionDef[] | null {
  const def = cat.tasks.find((t) => t.name === task.name && t.entry === task.entry);
  return def ? def.options : null;
}

function m9aSetNode(nodes: M9aOptionNode[], path: string[], value: Partial<M9aOptionNode>): void {
  let arr = nodes;
  for (let i = 0; i < path.length; i++) {
    const name = path[i];
    let node = arr.find((n) => n.name === name);
    if (!node) { node = { name }; arr.push(node); }
    if (i + 1 === path.length) {
      delete node.selected_case;
      delete node.selected_cases;
      delete node.index;
      Object.assign(node, value);
      return;
    }
    if (!node.sub_options) node.sub_options = [];
    arr = node.sub_options;
  }
}

function m9aOptAttrs(source: M9aSource, path: string[], task?: string): string {
  return ' data-m9a-opt="1" data-src="' + source + '"' + (task === undefined ? '' : ' data-task="' + esc(task) + '"') +
    ' data-path="' + esc(JSON.stringify(path)) + '"';
}

// M9A 的 switch 常只给英文 case 名；固定语义补中文，其余仍用目录标签，不翻译配置值。
const M9A_CASE_CN: Record<string, string> = {
  No: '关', Yes: '开', Current: '当前', MAX: '最高',
  '24h': '24 小时内', '7d': '7 天内', '14d': '14 天内',
};

function m9aCaseLabel(c: M9aOptionCase): string {
  return M9A_CASE_CN[c.name] || c.label || c.name;
}

function m9aInputHtml(def: M9aOptionDef, node?: M9aOptionNode): string {
  const vals = (def.inputs || []).map((input) => input.password
    ? esc(input.label || input.name) + '：<span class="drRo">密码字段不展示</span>'
    : esc(input.label || input.name) + '：' + esc(node?.data?.[input.name] ?? input.default ?? '（空）'));
  return '<div class="drRo">' + (vals.join('<br>') || '只读字段') + '</div><i class="drHint">自由输入只读；需要时请在 M9A 内修改</i>';
}

function m9aRootDefs(defs: M9aOptionDef[]): M9aOptionDef[] {
  // 目录是 DFS 前序：只在「被更早父项引用」时判为子项，后代的回环引用不能把根项抹掉。
  const children = new Set<string>();
  return defs.filter((d) => {
    const root = !children.has(d.name);
    for (const c of d.cases || []) for (const name of c.options || []) children.add(name);
    return root;
  });
}

function m9aPlainText(text?: string): string {
  return (text || '').replace(/<br\s*\/?>/gi, ' ').replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim();
}

function m9aOptionsHtml(
  defs: M9aOptionDef[],
  nodes: M9aOptionNode[],
  allDefs: M9aOptionDef[],
  source: M9aSource,
  task: string | undefined,
  prefix: string[] = [],
  depth = 0,
  filterRoots = true,
): string {
  return (filterRoots ? m9aRootDefs(defs) : defs).map((def) => {
    const node = m9aNode(nodes, def.name);
    const path = [...prefix, def.name];
    const attrs = m9aOptAttrs(source, path, task);
    let value: string;
    if (def.type === 'switch' || def.type === 'select') {
      const opts: [string, string][] = (def.cases || []).map((c) => [c.name, m9aCaseLabel(c)]);
      value = opts.length ? selHtml(attrs, m9aOne(def, node), opts) : '<div class="drRo">（目录没给候选）</div>';
    } else if (def.type === 'checkbox') {
      const cases = def.cases || [];
      const selected = new Set(m9aMany(def, node));
      const unknownCount = m9aUnknownCases(def, node).length;
      value = cases.length
        ? '<select multiple class="drSel"' + attrs + ' size="' + Math.min(5, Math.max(2, cases.length)) + '">' +
          cases.map((c) => '<option value="' + esc(c.name) + '"' + (selected.has(c.name) ? ' selected' : '') + '>' + esc(m9aCaseLabel(c)) + '</option>').join('') +
          '</select><i class="drHint">按住 Ctrl 可多选；不选 = 关闭全部已知项' +
          (unknownCount ? '；另有 ' + unknownCount + ' 个目录外现值只读保留' : '') + '</i>'
        : '<div class="drRo">（目录没给候选）</div>';
    } else value = m9aInputHtml(def, node);
    const children = m9aChildren(def, node);
    const childDefs = allDefs.filter((d) => children.includes(d.name) && !path.includes(d.name));
    const sub = childDefs.length
      ? '<div class="m9aSub">' + m9aOptionsHtml(childDefs, node?.sub_options || [], allDefs, source, task, path, depth + 1, false) + '</div>'
      : '';
    const label = def.label || def.name;
    const description = m9aPlainText(def.description);
    const title = description ? ' title="' + esc(description) + '"' : '';
    return '<div class="m9aOpt" style="--depth:' + depth + '"><div class="drF"><span class="drL"' + title + '>' + esc(label) + '</span><div class="drV">' + value + '</div></div>' + sub + '</div>';
  }).join('');
}

function collectM9aEdits(
  defs: M9aOptionDef[],
  nodes: M9aOptionNode[],
  base: M9aOptionNode[],
  source: M9aSource,
  task: number | undefined,
  out: M9aOptionEdit[],
  prefix: string[] = [],
  allDefs: M9aOptionDef[] = defs,
  filterRoots = true,
): void {
  for (const def of filterRoots ? m9aRootDefs(defs) : defs) {
    const path = [...prefix, def.name];
    const node = m9aNode(nodes, def.name);
    const old = m9aNode(base, def.name);
    if (def.type === 'switch' || def.type === 'select') {
      const now = m9aOne(def, node);
      if (now && now !== m9aOne(def, old)) out.push({ source, task, path, selected_case: now });
    } else if (def.type === 'checkbox') {
      const now = m9aMany(def, node).slice().sort();
      const before = m9aMany(def, old).slice().sort();
      if (JSON.stringify(now) !== JSON.stringify(before)) out.push({ source, task, path, selected_cases: now });
    }
    const children = m9aChildren(def, node);
    const childDefs = allDefs.filter((d) => children.includes(d.name) && !path.includes(d.name));
    if (childDefs.length) collectM9aEdits(childDefs, node?.sub_options || [], old?.sub_options || [], source, task, out, path, allDefs, false);
  }
}

export function openM9aDrawer(inst: M9aInst, isCur: boolean, bridge: Bridge, after: () => void, catalogs?: M9aCatalogs, focusKey?: string): void {
  const key = 'm9a:' + inst.id + (focusKey ? ':task:' + focusKey : '');
  const readOnlyRows = [
    ['实例 ID', inst.id],
    ['实例名', inst.name || '—'],
    ['ADB', (inst.adbSerial || '—') + (inst.adbName ? '（' + inst.adbName + '）' : '')],
  ].map(([k, v]) => '<div class="drF"><span class="drL">' + esc(k) + '</span><div class="drV"><div class="drRo">' + esc(v) + '</div></div></div>').join('');
  if (!catalogs) {
    const h = openDrawer(inst.name || inst.id, 'M9A · 实例配置',
      '<section class="drG"><h3>连接</h3>' + readOnlyRows + '</section><div class="drNote">M9A 候选目录没读回来：刷新后再开齿轮。</div>',
      '<button type="button" class="btn mini" data-cancel="1">关闭</button>', key);
    (h.foot.querySelector('[data-cancel]') as HTMLElement).addEventListener('click', () => h.close());
    return;
  }

  const cat = catalogs;
  const queueNow = (): M9aQueueTask[] => m9aQueueFor(inst);
  let resource = inst.resource || '';
  let controller = inst.controller || '';
  let preset = inst.preset || '';
  let busy = false;
  let didFocusTask = false;
  const taskNodes = new Map<string, M9aOptionNode[]>();
  const baseScopedNodes: Record<'resource' | 'controller', Map<string, M9aOptionNode[]>> = {
    resource: new Map(), controller: new Map(),
  };
  const scopedNodes: Record<'resource' | 'controller', Map<string, M9aOptionNode[]>> = {
    resource: new Map(), controller: new Map(),
  };
  if (inst.resource) baseScopedNodes.resource.set(inst.resource, cloneNodes(inst.resourceOptions));
  if (inst.controller) baseScopedNodes.controller.set(inst.controller, cloneNodes(inst.controllerOptions));
  let globalNodes = cloneNodes(inst.globalOptions);
  const baseTaskNodes = new Map<string, M9aOptionNode[]>();
  for (const item of inst.taskItems || []) baseTaskNodes.set(item.name, cloneNodes(item.option));

  const h = openDrawer(inst.name || inst.id, 'M9A · 实例配置 · 全下拉',
    '', '<button type="button" class="btn mini" data-sw="1" data-cf="再点一次：切到这台">切到这台</button>' +
    '<button type="button" class="btn mini primary" data-save="1">保存</button>' +
    '<button type="button" class="btn mini" data-cancel="1">关闭</button>', key);

  function nodesForTask(name: string): M9aOptionNode[] {
    let nodes = taskNodes.get(name);
    if (!nodes) { nodes = cloneNodes(baseTaskNodes.get(name)); taskNodes.set(name, nodes); }
    return nodes;
  }

  function nodesForScoped(source: 'resource' | 'controller', scope: string): M9aOptionNode[] {
    let nodes = scopedNodes[source].get(scope);
    if (!nodes) { nodes = cloneNodes(baseScopedNodes[source].get(scope)); scopedNodes[source].set(scope, nodes); }
    return nodes;
  }

  function compatible(): boolean {
    const r = cat.resources.find((x) => x.name === resource);
    return !r || !r.controllers.length || r.controllers.some((x) => x.toLowerCase() === controller.toLowerCase());
  }

  function bodyHtml(): string {
    const queue = queueNow();
    const rootRows =
      '<div class="drF"><span class="drL">资源</span><div class="drV">' + selHtml('data-root="resource"', resource, cat.resources.map((r) => [r.name, r.name] as [string, string])) + '</div></div>' +
      '<div class="drF"><span class="drL">控制器</span><div class="drV">' + selHtml('data-root="controller"', controller, cat.controllers.map((c) => [c, c] as [string, string])) + '</div></div>' +
      '<div class="drF"><span class="drL">预设</span><div class="drV">' + selHtml('data-root="preset"', preset, [['', '（不使用预设）'], ...cat.presets.map((p) => [p.name, p.name] as [string, string])], '（不使用预设）') + '</div></div>';
    const optionGroups = queue.map((t) => {
      const defs = m9aTaskDef(cat, t);
      const focusClass = t.key === focusKey ? ' taskFocus' : '';
      const attrs = ' class="drG' + focusClass + '" data-m9a-task-section="' + esc(t.key) + '"';
      if (!defs) return '<section' + attrs + '><h3>' + esc(t.name) + '<small>目录里没有对应定义</small></h3><div class="drNote">旧任务只保留队列身份；选项不可编辑。</div></section>';
      if (!defs.length) return '<section' + attrs + '><h3>' + esc(t.name) + '<small>' + esc(t.entry) + '</small></h3><div class="drNote">这个任务没有可编辑选项。</div></section>';
      return '<section' + attrs + '><h3>' + esc(t.name) + '<small>' + esc(t.entry) + '</small></h3>' +
        m9aOptionsHtml(defs, nodesForTask(t.name), defs, 'task', t.key) + '</section>';
    }).join('');
    const resourceDefs = cat.resourceOptions[resource] || [];
    const controllerDefs = cat.controllerOptions[controller] || [];
    const extraOptions =
      (resourceDefs.length ? '<section class="drG"><h3>资源选项<small>' + esc(resource) + '</small></h3>' + m9aOptionsHtml(resourceDefs, nodesForScoped('resource', resource), resourceDefs, 'resource', undefined) + '</section>' : '') +
      (controllerDefs.length ? '<section class="drG"><h3>控制器选项<small>' + esc(controller) + '</small></h3>' + m9aOptionsHtml(controllerDefs, nodesForScoped('controller', controller), controllerDefs, 'controller', undefined) + '</section>' : '') +
      (cat.globalOptions.length ? '<section class="drG"><h3>全局选项</h3>' + m9aOptionsHtml(cat.globalOptions, globalNodes, cat.globalOptions, 'global', undefined) + '</section>' : '');
    return '<section class="drG"><h3>连接</h3>' + readOnlyRows + '</section>' +
      '<section class="drG"><h3>实例设置</h3>' + rootRows + '</section>' +
      optionGroups + extraOptions +
      '<div class="drNote">保存会原位改写并先备份；M9A 正在运行时拒写。任务勾选与增删在左侧「02 任务队列」的次级栏里操作，不调整顺序。自由输入与密码字段只读。</div>' +
      (compatible() ? '' : '<div class="drMsg bad">当前资源不支持这个控制器，请改选兼容组合。</div>');
  }

  function render(focusEl?: HTMLElement): void {
    const focus = focusEl?.dataset.m9aOpt !== undefined
      ? { kind: 'opt' as const, src: focusEl.dataset.src, task: focusEl.dataset.task, path: focusEl.dataset.path }
      : focusEl?.dataset.root ? { kind: 'root' as const, root: focusEl.dataset.root } : null;
    const top = h.body.scrollTop;
    h.body.innerHTML = bodyHtml();
    h.body.scrollTop = top;
    if (focus) {
      const found = Array.from(h.body.querySelectorAll<HTMLElement>(focus.kind === 'root' ? '[data-root]' : '[data-m9a-opt]')).find((el) => focus.kind === 'root'
        ? el.dataset.root === focus.root
        : el.dataset.src === focus.src && el.dataset.task === focus.task && el.dataset.path === focus.path);
      found?.focus();
    }
    if (focusKey && !didFocusTask) {
      didFocusTask = true;
      const target = Array.from(h.body.querySelectorAll<HTMLElement>('[data-m9a-task-section]'))
        .find((el) => el.dataset.m9aTaskSection === focusKey);
      if (target) {
        target.tabIndex = -1;
        target.focus({ preventScroll: true });
        target.scrollIntoView({ block: 'start' });
      }
    }
    const save = h.foot.querySelector<HTMLButtonElement>('[data-save]');
    if (save) { save.disabled = busy || !compatible(); save.textContent = busy ? '保存中…' : '保存'; }
    const sw = h.foot.querySelector<HTMLButtonElement>('[data-sw]');
    if (sw && isCur) sw.remove();
  }

  h.body.addEventListener('change', (e) => {
    const el = (e.target as HTMLElement).closest<HTMLElement>('[data-root],[data-m9a-opt]');
    if (!el || busy) return;
    if (el.dataset.root) {
      if (el.dataset.root === 'resource') resource = (el as HTMLSelectElement).value;
      if (el.dataset.root === 'controller') controller = (el as HTMLSelectElement).value;
      if (el.dataset.root === 'preset') preset = (el as HTMLSelectElement).value;
      render(el);
      return;
    }
    if (el.dataset.m9aOpt !== undefined) {
      const source = el.dataset.src as M9aSource;
      const path = JSON.parse(el.dataset.path || '[]') as string[];
      const select = el as HTMLSelectElement;
      const taskKey = el.dataset.task;
      const queue = queueNow();
      const task = taskKey === undefined ? undefined : queue.findIndex((item) => item.key === taskKey);
      let nodes: M9aOptionNode[];
      if (source === 'task') {
        if (task === undefined || task < 0) return;
        const item = queue[task];
        if (!item) return;
        nodes = nodesForTask(item.name);
      } else nodes = source === 'resource'
        ? nodesForScoped('resource', resource)
        : source === 'controller' ? nodesForScoped('controller', controller) : globalNodes;
      if (select.multiple) {
        const defs = source === 'task' ? (task === undefined ? [] : m9aTaskDef(cat, queue[task]) || []) : source === 'resource' ? cat.resourceOptions[resource] || [] : source === 'controller' ? cat.controllerOptions[controller] || [] : cat.globalOptions;
        const def = defs.find((d) => d.name === path[path.length - 1]);
        let current: M9aOptionNode | undefined;
        let scope = nodes;
        for (const name of path) {
          current = scope.find((n) => n.name === name);
          if (!current) break;
          scope = current.sub_options || [];
        }
        const picked = Array.from(select.selectedOptions).map((o) => o.value);
        m9aSetNode(nodes, path, { selected_cases: def ? m9aMergeKnownCases(def, current, picked) : picked });
      } else {
        const defs = source === 'task' ? (task === undefined ? [] : m9aTaskDef(cat, queue[task]) || []) : source === 'resource' ? cat.resourceOptions[resource] || [] : source === 'controller' ? cat.controllerOptions[controller] || [] : cat.globalOptions;
        const def = defs.find((d) => d.name === path[path.length - 1]);
        const idx = (def?.cases || []).findIndex((c) => c.name === select.value);
        m9aSetNode(nodes, path, { selected_case: select.value, index: idx >= 0 ? idx : undefined });
      }
      render(el);
    }
  });

  (h.foot.querySelector('[data-cancel]') as HTMLElement).addEventListener('click', () => h.close());
  const sw = h.foot.querySelector('[data-sw]') as HTMLButtonElement | null;
  if (sw) {
    let armed = 0;
    sw.addEventListener('click', () => {
      const now = Date.now();
      if (now < armed) {
        sw.disabled = true;
        void bridge.assistM9aSwitch(inst.id, inst.name).then((r) => {
          if (r.ok) S.pushLog('OK', '已切到实例 ' + (inst.name || inst.id));
          else S.pushLog('ERROR', '切实例失败：' + (r.error || ''));
          h.close(); after();
        });
        return;
      }
      armed = now + 4000;
      sw.textContent = '确认？';
      window.setTimeout(() => { if (sw.isConnected && !sw.disabled) sw.textContent = sw.dataset.cf || '切到这台'; }, 4000);
    });
  }

  const save = h.foot.querySelector<HTMLButtonElement>('[data-save]') as HTMLButtonElement;
  save.addEventListener('click', () => {
    if (busy) return;
    if (!compatible()) { S.pushLog('ERROR', inst.name + ' 保存被拦：资源与控制器不兼容'); return; }
    const patch: Record<string, unknown> = { name: inst.name };
    if (resource !== (inst.resource || '')) patch.resource = resource;
    if (controller !== (inst.controller || '')) patch.controller = controller;
    if (preset !== (inst.preset || '')) patch.preset = preset;
    const queue = queueNow();
    const keys = queue.map((t) => t.key);
    if (m9aQueueIsDirty(inst)) {
      if (keys.some((k) => !parseM9aHistory(k))) { S.pushLog('ERROR', inst.name + ' 保存被拦：队列里有无法识别的旧任务格式'); return; }
      patch.currentTasks = keys;
    }
    const optionEdits: M9aOptionEdit[] = [];
    const seenTasks = new Set<string>();
    queue.forEach((t, i) => {
      if (seenTasks.has(t.name)) return; // 同名重复项共用 M9A 按任务名保存的同一份选项。
      seenTasks.add(t.name);
      const defs = m9aTaskDef(cat, t);
      if (defs) collectM9aEdits(defs, nodesForTask(t.name), cloneNodes(baseTaskNodes.get(t.name)), 'task', i, optionEdits);
    });
    collectM9aEdits(cat.resourceOptions[resource] || [], nodesForScoped('resource', resource), baseScopedNodes.resource.get(resource) || [], 'resource', undefined, optionEdits);
    collectM9aEdits(cat.controllerOptions[controller] || [], nodesForScoped('controller', controller), baseScopedNodes.controller.get(controller) || [], 'controller', undefined, optionEdits);
    collectM9aEdits(cat.globalOptions, globalNodes, inst.globalOptions || [], 'global', undefined, optionEdits);
    if (optionEdits.length) patch.optionEdits = optionEdits;
    if (Object.keys(patch).length === 1) { S.pushLog('OK', inst.name + ' 跟文件里一致，没有写盘'); return; }
    if (!m9aQueueSaveBegin(inst)) { S.pushLog('ERROR', inst.name + ' 正在保存，请等上一笔完成'); return; }
    busy = true; render();
    void bridge.assistM9aSaveInstance(inst.id, inst.name, patch).then((r) => {
      busy = false;
      m9aQueueSaveEnd(inst);
      if (r.ok) {
        if (patch.currentTasks) m9aQueueCommit(inst, patch.currentTasks as string[]);
        S.pushLog('OK', inst.name + ' 实例配置已保存（原位写入、备份、回读对平）');
        h.close(); after();
      } else {
        render();
        S.pushLog('ERROR', inst.name + ' 保存失败：' + (r.error || '未知错误'));
      }
    }).catch((e) => { busy = false; m9aQueueSaveEnd(inst); render(); S.pushLog('ERROR', inst.name + ' 保存失败：' + String(e)); });
  });

  render();
}