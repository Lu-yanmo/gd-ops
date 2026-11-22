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
import type { M9aInst, MaaTask, MaaState, MaaCatalogs } from './types';
import { openDrawer, type DrawerHandle } from './drawer';
import * as S from './state';

type Cfg = Record<string, unknown>;
type Kind = 'bool' | 'int' | 'enum' | 'list' | 'rooms' | 'ro' | 'single';
type Enc = 'arr' | ';' | ', ' | ',';
type Src = 'stages' | 'materials' | 'tags' | 'opers' | 'foldartals' | 'paradigms' | 'items' | 'infrasts' | 'squads' | 'roles' | 'awards';

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
];

const MODE_OPTS: [string, string][] = [
  ['Exp', '刷等级'],
  ['Investment', '刷源石锭'],
  ['Collectible', '刷开局'],
  ['CLP_PDS', '刷坍缩范式'],
  ['Squad', '刷月度小队'],
  ['Exploration', '刷深入调查'],
  ['FindPlaytime', '刷常乐节点'],
];

const DRONE_OPTS: [string, string][] = [
  ['Money', '贸易站-龙门币'],
  ['SyntheticJade', '贸易站-合成玉'],
  ['CombatRecord', '制造站-经验书'],
  ['PureGold', '制造站-赤金'],
  ['OriginStone', '制造站-源石碎片'],
  ['Chip', '制造站-芯片组'],
  ['NotUse', '不使用无人机'],
];

const SQUADS: [string, string][] = [
  '默认分队', '心胜于物分队', '物尽其用分队', '以人为本分队', '永恒狩猎分队', '生活至上分队',
  '科学主义分队', '特训分队', '魂灵护送分队', '博闻广记分队', '蓝图测绘分队', '因地制宜分队',
  '异想天开分队', '点刺成锭分队', '拟态学者分队', '专业人士分队', '高台突破分队', '地面突破分队',
  '游客分队', '司岁台分队', '天师府分队', '花团锦簇分队', '棋行险着分队', '岁影回音分队',
  '代理人分队', '知学分队', '商贾分队', '特勤分队', '指挥分队', '集群分队', '后勤分队',
  '矛头分队', '突击战术分队', '堡垒战术分队', '远程战术分队', '破坏战术分队', '研究分队', '高规格分队',
].map((x) => [x, x] as [string, string]);

const ROLES: [string, string][] = [
  ['默认职业组', '默认职业组'],
  ['先手必胜', '先手必胜（先锋、狙击、特种）'],
  ['稳扎稳打', '稳扎稳打（重装、术师、狙击）'],
  ['取长补短', '取长补短（近卫、辅助、医疗）'],
  ['随心所欲', '随心所欲（三张随机）'],
  ['灵活部署', '灵活部署（先锋、辅助、特种）'],
  ['坚不可摧', '坚不可摧（重装、术师、医疗）'],
];

/** RoguelikeCollectibleAward（flags 枚举，配置里存英文名、逗号串）。 */
const AWARDS: [string, string][] = [
  ['HotWater', '热水壶'],
  ['Shield', '盾'],
  ['Ingot', '锭'],
  ['Hope', '希望'],
  ['Random', '随机奖励'],
  ['Key', '钥匙'],
  ['Dice', '骰子'],
  ['Idea', '构想'],
  ['Ticket', '票券'],
];

const GENERIC_NUMS = [0, 1, 2, 3, 4, 5, 6, 8, 10, 20, 30, 50, 99, 100, 999];
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
              nums: [-1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 2147483647],
              labels: { '-1': '自动', '2147483647': 'MAX（自动）' },
            },
            { k: 'Mode', label: '刷取策略', kind: 'enum', opts: MODE_OPTS },
            {
              k: 'StartCount',
              label: '开始探索次数上限',
              kind: 'int',
              nums: [1, 2, 3, 5, 10, 20, 50, 100, 999, 9999, 99999],
            },
            { k: 'Squad', label: '开局分队', src: 'squads', allowEmpty: true },
            { k: 'Roles', label: '开局职业组', src: 'roles', allowEmpty: true },
            { k: 'StartingOpers', label: '开局干员', kind: 'list', src: 'opers', enc: 'arr' },
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

function catOpts(src: Src, c?: MaaCatalogs): [string, string][] {
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
      return (c?.infrasts ?? []).map((x) => [x, x] as [string, string]);
    case 'squads':
      return SQUADS;
    case 'roles':
      return ROLES;
    case 'awards':
      return AWARDS;
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

function intOpts(f: F, cur: unknown): [string, string][] {
  const base = f.nums ?? GENERIC_NUMS;
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
  const opts = f.src ? catOpts(f.src, cat) : vals.map((v) => [v, v] as [string, string]);
  const full = f.max !== undefined && vals.length >= f.max;
  const hint = f.hint ? '<i class="drHint">' + esc(f.hint) + '</i>' : '';
  return listBox(vals, opts, enc, ' data-list="' + esc(f.k) + '"', !!f.src && !full) + hint;
}

function inputHtml(task: MaaTask, f: F, cat?: MaaCatalogs): string {
  const v = task.cfg[f.k];
  if (isRo(task, f)) {
    const badge = task.type === 'Fight' && KERNEL_FIGHT.has(f.k) ? '<b class="drK">每轮覆盖</b>' : '';
    return '<div class="drRo">' + esc(fmt(v)) + badge + '</div>';
  }
  const hint = f.hint ? '<i class="drHint">' + esc(f.hint) + '</i>' : '';
  const kind = f.kind || inferKind(v);
  if (kind === 'bool') {
    return '<div class="drCtl">' + selHtml('data-fk="' + esc(f.k) + '" data-ft="bool"', v === true ? '1' : '0', [['1', '开'], ['0', '关']]) + '</div>' + hint;
  }
  if (kind === 'int') {
    return '<div class="drCtl">' + selHtml('data-fk="' + esc(f.k) + '" data-ft="int"', String(typeof v === 'number' ? v : 0), intOpts(f, v)) + '</div>' + hint;
  }
  if (kind === 'enum') {
    return '<div class="drCtl">' + selHtml('data-fk="' + esc(f.k) + '" data-ft="single"', String(v ?? ''), f.opts || []) + '</div>' + hint;
  }
  if (kind === 'rooms') {
    const rooms = Array.isArray(v) ? v as { Room?: unknown; IsEnabled?: unknown }[] : [];
    return '<div class="drRooms">' + rooms.map((r) => {
      const key = String(r.Room || '');
      const on = r.IsEnabled !== false;
      return '<div class="drRoomRow"><span class="drRoomName">' + esc(ROOM_CN[key] || key) + '</span>' +
        selHtml('data-room="' + esc(key) + '"', on ? '1' : '0', [['1', '启用'], ['0', '禁用']]) + '</div>';
    }).join('') + '</div>' + hint;
  }
  if (kind === 'ro') {
    return '<div class="drRo">' + esc(fmt(v)) + '</div>';
  }
  if (kind === 'list') {
    return listHtml(task, f, cat);
  }
  // single：有候选表就列候选表；没有就只列现值/空（不编假选项）
  const opts = f.src ? catOpts(f.src, cat) : [];
  const withEmpty: [string, string][] = f.allowEmpty ? [['', '（空 / 不设置）'], ...opts] : opts;
  return '<div class="drCtl">' + selHtml('data-fk="' + esc(f.k) + '" data-ft="single"', String(v ?? ''), withEmpty) + '</div>' + hint;
}

function rowHtml(task: MaaTask, f: F, cat?: MaaCatalogs): string {
  return '<div class="drF"><span class="drL">' + esc(f.label) + '</span><div class="drV">' + inputHtml(task, f, cat) + '</div></div>';
}

/* ── 面板计划源（写 settings，不是 MAA 配置） ── */

function planFields(task: MaaTask): F[] {
  const t = task.type.toLowerCase();
  if (t === 'fight') {
    return [
      { k: 'ark.defaultStage', label: '默认关卡', kind: 'list', src: 'stages', enc: ',', hint: '内核每轮按顺序挑第一关能打的写进 StagePlan；可排多关做轮换' },
      { k: 'ark.farmMaxTimesPerRound', label: '每轮最多补刷', kind: 'int', nums: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
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
    const rows = g.fields.filter((f) => f.k in task.cfg).map((f) => rowHtml(task, f, cat)).join('');
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

  // 填错后改了格，红边跟着撤掉。
  h.body.addEventListener('input', (e) => (e.target as HTMLElement).classList.remove('bad'));
  h.body.addEventListener('change', (e) => {
    const el = e.target as HTMLElement;
    el.classList.remove('bad');
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
    }
  });

  (h.foot.querySelector('[data-save]') as HTMLButtonElement).addEventListener('click', () => {
    void save(h, task, bridge, after, say);
  });
}

interface Edit { key: string; value: unknown }

async function save(
  h: DrawerHandle,
  task: MaaTask,
  bridge: Bridge,
  after: () => void,
  say: (ok: boolean, text: string) => void,
): Promise<void> {
  const btn = h.foot.querySelector('[data-save]') as HTMLButtonElement;
  if (btn.disabled) return;

  const edits: Edit[] = [];
  for (const el of Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-sk]'))) {
    const key = el.dataset.sk || '';
    const st = el.dataset.st;
    if (st === 'bool') {
      edits.push({ key, value: el.value === '1' });
    } else if (st === 'int') {
      const n = Number(el.value);
      if (!Number.isFinite(n)) { el.classList.add('bad'); say(false, key + '：要整数'); return; }
      edits.push({ key, value: Math.trunc(n) });
    } else {
      edits.push({ key, value: el.value });
    }
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
  let bad = false;
  for (const el of Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-fk]'))) {
    const k = el.dataset.fk || '';
    if (KERNEL_FIGHT.has(k) && task.type === 'Fight') continue;
    const ft = el.dataset.ft;
    let val: unknown;
    if (ft === 'bool') {
      val = el.value === '1';
    } else if (ft === 'int') {
      const n = Number(el.value);
      if (!Number.isFinite(n)) { el.classList.add('bad'); bad = true; continue; }
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

  // 房间矩阵：按原顺序重建 RoomList（只喂 Room/IsEnabled 两键）。
  const roomEls = Array.from(h.body.querySelectorAll<HTMLSelectElement>('[data-room]'));
  if (roomEls.length) {
    const old = Array.isArray(task.cfg.RoomList) ? task.cfg.RoomList as { Room?: unknown }[] : [];
    const rooms = old.map((r) => {
      const key = String(r.Room || '');
      const hit = roomEls.find((el) => el.dataset.room === key);
      const on = hit ? hit.value === '1' : true;
      return on ? { Room: key } : { Room: key, IsEnabled: false };
    });
    if (JSON.stringify(rooms) !== JSON.stringify(old)) patch.RoomList = rooms;
  }

  if (bad) { say(false, '有数字格的值不是整数，先改好再保存'); return; }
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

/* ── M9A 实例：只读详情框 ── */

export function openM9aDrawer(inst: M9aInst, isCur: boolean, bridge: Bridge, after: () => void): void {
  const tasks = inst.allTasks && inst.allTasks.length ? inst.allTasks : inst.tasks;
  const rows = [
    ['实例 ID', inst.id],
    ['资源', inst.resource || '—'],
    ['控制器', inst.controller || '—'],
    ['预设', inst.preset || '—'],
    ['ADB', (inst.adbSerial || '—') + (inst.adbName ? '（' + inst.adbName + '）' : '')],
  ].map(([k, v]) => '<div class="drF"><span class="drL">' + esc(k) + '</span><div class="drV"><div class="drRo">' + esc(v) + '</div></div></div>').join('');

  const list = tasks.length
    ? '<ol class="drTasks">' + tasks.map((t) => '<li>' + esc(t) + '</li>').join('') + '</ol>'
    : '<div class="drRo">（没有任务）</div>';

  const h = openDrawer(
    inst.name || inst.id,
    'M9A · ' + inst.taskCount + ' 个任务 · 实例现状（只读）',
    '<section class="drG"><h3>连接</h3>' + rows + '</section>' +
    '<section class="drG"><h3>任务表</h3>' + list + '</section>' +
    '<div class="drNote">实例文件的编辑通道未接：任务增删仍在 M9A 里做；切到这一台走 <code>apply-m9a-instance</code>（面板已有该通道）。</div>',
    (isCur ? '' : '<button type="button" class="btn mini" data-sw="1" data-cf="再点一次：切到这台">切到这台</button>') +
    '<button type="button" class="btn mini" data-cancel="1">关闭</button>',
    'm9a:' + inst.id,
  );
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
          h.close();
          after();
        });
        return;
      }
      armed = now + 4000;
      sw.textContent = '确认？';
      window.setTimeout(() => { if (sw.isConnected && !sw.disabled) sw.textContent = sw.dataset.cf || '切到这台'; }, 4000);
    });
  }
}