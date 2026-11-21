"""gdagent.engine 离线自检(M2)：镜像 tools\\test-agent.ps1 的 a1–a7。

跑法(tools 目录下)： python gdagent/tests/test_agent.py
                   python -m unittest discover -s gdagent/tests -t .
约定同全家：不联网、不读真 config、不起真子进程 —— 写盘只发生在临时沙盒；
模型/verb/快照/确认全部走 send_exchange 的注入缝，一次网都不联。
"""
import io
import json
import os
import sys
import tempfile
import time
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))

from gdagent import engine as EN                       # noqa: E402
from gdagent import live as LV                         # noqa: E402
from gdagent import progression as PG                  # noqa: E402
from gdagent import drops as DR                        # noqa: E402
import kb_notices as KN                                # noqa: E402  tools 根同目录

fake_spec_parser = EN.fake_spec_parser                  # 对拍/自检同一件：家只有一个（engine.py 顶部）


# ---------- [a1] set-config 白名单与取值校验 ----------

class TestConfigGate(unittest.TestCase):
    def test_kinds_table(self):
        self.assertEqual(EN.CONFIG_KINDS.get('ark.farmTargets'), 'farmTargets')
        self.assertEqual(EN.CONFIG_KINDS.get('ark.farmAppend'), 'bool')
        self.assertEqual(EN.CONFIG_KINDS.get('runtime.supervisor'), 'bool')
        self.assertGreaterEqual(len(EN.CONFIG_KINDS), 5)

    def test_path_whitelist(self):
        bad = EN.test_action({'type': 'set-config', 'path': 'schedule.daily', 'value': 'x'})
        self.assertFalse(bad['Ok'])
        self.assertIn('schedule.daily', bad['Why'])

    def test_string_true_normalizes_to_bool(self):
        r = EN.test_action({'type': 'set-config', 'path': 'ark.farmAppend', 'value': 'true'})
        self.assertTrue(r['Ok'])
        self.assertIs(r['Value'], True)

    def test_weekday_plan(self):
        wp = EN.check_config_value('weekdayPlan', {'1': 'CE-5', '8': 'LS-5'})
        self.assertFalse(wp['Ok'])
        wp2 = EN.check_config_value('weekdayPlan', {'6': 'CE-5,LS-5'})
        self.assertTrue(wp2['Ok'])
        self.assertEqual(wp2['Value']['6'], 'CE-5,LS-5')

    def test_max_times(self):
        self.assertFalse(EN.check_config_value('maxTimes', 0)['Ok'])
        self.assertFalse(EN.check_config_value('maxTimes', 31)['Ok'])
        r = EN.check_config_value('maxTimes', '12')
        self.assertTrue(r['Ok'])
        self.assertEqual(r['Value'], 12)

    def test_stage_list(self):
        known = {'CE-5': True}
        st1 = EN.check_config_value('stageList', 'CE-5,LS-5', known)
        self.assertFalse(st1['Ok'])
        self.assertIn('LS-5', st1['Why'])
        self.assertFalse(EN.check_config_value('stageList', 'CE-5!!', known)['Ok'])

    def test_farm_targets(self):
        empty = EN.check_config_value('farmTargets', None, spec_parser=fake_spec_parser)
        self.assertTrue(empty['Ok'])
        self.assertEqual(empty['Value'], [])
        good = EN.check_config_value('farmTargets', ['特种芯片组=8@PR-D-2'], spec_parser=fake_spec_parser)
        self.assertTrue(good['Ok'])
        bad = EN.check_config_value('farmTargets', ['ok一行', '坏行#bad'], spec_parser=fake_spec_parser)
        self.assertFalse(bad['Ok'])
        self.assertIn('坏行#bad', bad['Why'])            # 消息嵌的是解析器 Raw
        hole = EN.check_config_value('farmTargets', ['a=1', '   '], spec_parser=fake_spec_parser)
        self.assertFalse(hole['Ok'])
        self.assertIn('空条目', hole['Why'])
        no_parser = EN.check_config_value('farmTargets', ['a=1'], spec_parser=None)
        self.assertFalse(no_parser['Ok'])
        self.assertIn('panel-cs', no_parser['Why'])      # 刀⑬(2026-11-06)：验不了的判据从 ark-farm.ps1 改指 C# 判定层

    def test_unknown_action_type(self):
        r = EN.test_action({'type': 'reboot-mumu', 'reason': '模型突发奇想'})
        self.assertFalse(r['Ok'])


# ---------- [a2] run-verb 白名单 ----------

class TestVerbGate(unittest.TestCase):
    def test_over_gate(self):
        self.assertFalse(EN.check_verb_args(['-Force'])['Ok'])

    def test_dry_is_not_mutating(self):
        r = EN.check_verb_args(['-ArkStage', 'CE-5', '-DryRun'])
        self.assertTrue(r['Ok'])
        self.assertTrue(r['DryRun'])
        self.assertFalse(r['Mutating'])                  # 曾被误判成真跑的那条

    def test_no_dry_is_mutating(self):
        r = EN.check_verb_args(['-ArkStage', 'CE-5'])
        self.assertTrue(r['Ok'])
        self.assertTrue(r['Mutating'])

    def test_dup_and_value_rules(self):
        self.assertFalse(EN.check_verb_args(['-DryRun', '-DryRun'])['Ok'])
        self.assertFalse(EN.check_verb_args(['-Games'])['Ok'])
        self.assertFalse(EN.check_verb_args(['-Games', 'zzz'])['Ok'])

    def test_max_args(self):
        # 10-12 第 1 刀：-AckArk 已随入口参数退场，白名单少一枚 ⇒ 这里改用剩下的合法开关凑满超限
        # (合法项必须逐枚点名：check_verb_args 是先查"不在白名单"再查个数，混进非法参数会走错分支)。
        many = ['-DryRun', '-Ack1999', '-ArkShopDone', '-RShopDone',
                '-ArkShopReset', '-RShopReset', '-FarmArk', '-Games', 'ark']
        r = EN.check_verb_args(many)
        self.assertFalse(r['Ok'])
        self.assertIn('最多', r['Why'])


# ---------- [a3] 模型回复解析 ----------

class TestReplyParse(unittest.TestCase):
    def test_fence_and_chatter(self):
        p1 = EN.resolve_reply('好的。```json\n{"reply":"收到","actions":[{"type":"preview"}]}\n```。')
        self.assertTrue(p1['Ok'])
        self.assertEqual(p1['Reply'], '收到')
        self.assertEqual(len(p1['Actions']), 1)
        p2 = EN.resolve_reply('今天天气不错，自己去玩吧')
        self.assertFalse(p2['Ok'])
        self.assertIn('天气', p2['Reply'])               # 纯聊天：原话照抄、零动作
        self.assertEqual(p2['Actions'], [])

    def test_broken_json_does_not_throw(self):
        p3 = EN.resolve_reply('{"reply":"半截","actions":[')
        self.assertFalse(p3['Ok'])
        self.assertIn('JSON', p3['Why'])

    def test_truncate_at_four(self):
        six = '{"reply":"多动作","actions":[' + ','.join(['{"type":"preview"}'] * 6) + ']}'
        p4 = EN.resolve_reply(six)
        self.assertEqual(len(p4['Actions']), 4)
        self.assertIn('截', p4['Why'])

    def test_bare_action_array_is_accepted(self):
        # 实测坑：模型第二轮只吐裸 actions 数组(没有 reply/actions 外壳)。
        # 容错 = 认它、按空 reply 执行动作；不许把原始 JSON 和报错甩给玩家。
        raw = '[{"type":"set-config","path":"ark.farmTargets","value":["特种芯片组=8@PR-D-2"]},' \
              '{"type":"preview"}]'
        p = EN.resolve_reply(raw)
        self.assertTrue(p['Ok'])                          # 不再判解析失败
        self.assertEqual(p['Reply'], '')                  # 空 reply：引擎不印"agent："空行
        self.assertEqual(len(p['Actions']), 2)
        self.assertEqual(p['Actions'][0]['type'], 'set-config')
        self.assertEqual(p['Why'], '')

    def test_bare_array_over_four_truncates(self):
        p = EN.resolve_reply('[' + ','.join(['{"type":"preview"}'] * 6) + ']')
        self.assertTrue(p['Ok'])
        self.assertEqual(len(p['Actions']), 4)
        self.assertIn('截', p['Why'])

    def test_bare_array_of_non_objects_stays_chat(self):
        p = EN.resolve_reply('["刷 CE-6 就行"]')           # 不是动作列表：仍旧按纯聊天处理
        self.assertFalse(p['Ok'])
        self.assertEqual(p['Actions'], [])
        self.assertIn('CE-6', p['Reply'])


# ---------- [a4] 多轮消息组装 ----------

class TestMessages(unittest.TestCase):
    def test_order_and_content(self):
        hist = [{'role': 'user', 'text': 'u1'}, {'role': 'assistant', 'text': 'a1'}]
        m1 = EN.build_messages('BOOK', hist, 'SNAP', 'NOW')
        self.assertEqual(len(m1), 4)
        self.assertEqual(m1[0], {'role': 'system', 'content': 'BOOK'})
        self.assertEqual(m1[3]['role'], 'user')
        self.assertIn('SNAP', m1[3]['content'])
        self.assertIn('NOW', m1[3]['content'])

    def test_history_cap(self):
        fat = [{'role': 'user', 'text': 'm%d' % i} for i in range(1, 31)]
        m2 = EN.build_messages('B', fat, '', 'NOW', max_turns=2)
        self.assertEqual(len(m2), 6)                     # system + 最近 4 条 + 本轮 user
        self.assertEqual(len(EN.build_messages('B', [], '', 'Q')), 2)


# ---------- [a5] journal 回滚计划 ----------

class TestRollbackPlan(unittest.TestCase):
    def test_only_set_reversed(self):
        lines = [
            '{"ts":"2026-09-26 10:00:00","op":"set","path":"ark.farmAppend","old":true,"new":false,"backup":""}',
            '这不是 JSON(半行/人工注释)，回滚计划必须跳过它',
            '{"ts":"2026-09-26 10:01:00","op":"rollback","items":2}',
            '{"ts":"2026-09-26 10:02:00","op":"Set","path":"ark.defaultStage","old":"CE-5","new":"LS-5","backup":""}',
        ]
        plan = EN.rollback_plan(lines)
        self.assertEqual(len(plan), 2)                   # -eq 不分大小写：'Set' 也算
        self.assertEqual(plan[0]['path'], 'ark.defaultStage')   # 最新的先恢复


# ---------- [a6] 活层 apply→journal→rollback(临时沙盒) ----------

class TestSandboxLive(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-agent-test-')
        self.paths = EN.Paths(self.dir)
        self.paths.settings.parent.mkdir(parents=True, exist_ok=True)
        self._write('{"ark":{"farmAppend":true,"farmMaxTimesPerRound":12},"runtime":{"supervisor":true}}')

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def _write(self, text):
        self.paths.settings.write_text(text, encoding='utf-8')

    def _read(self):
        return EN.read_json(self.paths.settings)

    def test_apply_journal_rollback(self):
        norm = EN.test_action({'type': 'set-config', 'path': 'runtime.supervisor', 'value': False},
                              spec_parser=fake_spec_parser)
        self.assertTrue(norm['Ok'])
        ap = EN.apply_set_config(self.paths, norm)
        self.assertTrue(ap['Ok'])
        self.assertIn('回读一致=True', ap['Detail'])
        rb = EN.test_action({'type': 'set-config', 'path': 'ark.farmMaxTimesPerRound', 'value': 8})
        ap2 = EN.apply_set_config(self.paths, rb, backup_done=True)
        self.assertTrue(ap2['Ok'])
        self.assertTrue(self.paths.journal.is_file())
        jlines = [ln for ln in self.paths.journal.read_text(encoding='utf-8').splitlines() if ln.strip()]
        self.assertEqual(len(jlines), 2)
        self.assertEqual(len(list(self.paths.backups.glob('settings.json*.bak'))), 1)   # 第一条才备份
        dry = EN.rollback_settings(self.paths, dry=True)
        self.assertIn('待回滚 2 项', dry)
        self.assertIs(self._read()['runtime']['supervisor'], False)   # -Dry 只报数，一个字不改
        txt = EN.rollback_settings(self.paths)
        self.assertIn('已回滚 2 项', txt)
        back = self._read()
        self.assertIs(back['runtime']['supervisor'], True)
        self.assertEqual(back['ark']['farmMaxTimesPerRound'], 12)
        raw = self.paths.journal.read_text(encoding='utf-8')
        self.assertIn('"op":"rollback"', raw)            # compact 形状是 rollback 标记失配事故的命门
        self.assertIn('没有变更', EN.rollback_settings(self.paths))

    def test_missing_settings(self):
        p2 = EN.Paths(os.path.join(self.dir, 'nowhere'))
        r = EN.apply_set_config(p2, {'Path': 'ark.farmAppend', 'Value': False})
        self.assertFalse(r['Ok'])
        self.assertIn('settings.json', r['Detail'])


# ---------- [a7] 会话循环：假模型喂动作、真跑必须过确认钩子 ----------

class TestExchangeLoop(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-agent-loop-')
        self.paths = EN.Paths(self.dir)
        self.paths.settings.parent.mkdir(parents=True, exist_ok=True)
        self.paths.settings.write_text('{"ark":{"farmAppend":true},"runtime":{"supervisor":true}}', encoding='utf-8')
        self.verb_calls = []
        self.model_calls = []
        self.queue = [
            '{"reply":"先关监督并把预览跑一下","actions":['
            '{"type":"set-config","path":"runtime.supervisor","value":false},'
            '{"type":"preview"},'
            '{"type":"run-verb","args":["-FarmArk"]},'
            '{"type":"set-config","path":"schedule.daily","value":"x"}]}',
            '{"reply":"都办完了","actions":[]}',
        ]

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def _model(self, msgs):
        self.model_calls.append(msgs)
        if self.queue:
            return {'Ok': True, 'Text': self.queue.pop(0)}
        return {'Ok': False, 'Error': '自检钉死：不该再问第 %d 次' % (len(self.model_calls) + 1)}

    def _verb(self, args, allowed=False):
        self.verb_calls.append({'args': list(args), 'allowed': allowed})
        return {'Ran': True, 'Exit': 0, 'Detail': '假执行：' + ' '.join(args)}

    def test_one_exchange_two_rounds(self):
        history = []
        confirm_asked = []
        final = EN.send_exchange(self.paths, '关掉监督，预览一下，再试材料轮', history,
                                 self._model, self._verb,
                                 known_stages={}, spec_parser=fake_spec_parser,
                                 confirm_fn=lambda d: confirm_asked.append(d) or False,
                                 snapshot_fn=lambda: 'SNAP', printer=lambda t: None)
        self.assertEqual(len(self.model_calls), 2)       # 第二次带回执，没超 MaxRounds 空转
        second = '\n'.join(m['content'] for m in self.model_calls[1])
        self.assertIn('schedule.daily', second)          # 白名单外动作进了回执
        self.assertIn('不在白名单', second)
        dry_calls = [c for c in self.verb_calls if '-DryRun' in c['args']]
        self.assertEqual(len(dry_calls), 1)              # preview 自动放行
        self.assertEqual([c for c in self.verb_calls if '-DryRun' not in c['args']], [])
        self.assertTrue(any('FarmArk' in d for d in confirm_asked))   # 真跑走了确认钩子
        self.assertIn('都办完', final)
        self.assertEqual(history[0]['role'], 'user')
        self.assertIn('关掉监督', history[0]['text'])
        self.assertGreaterEqual(len(history), 4)
        self.assertIs(self._supervisor(), False)         # 自动应用档生效(没等人确认)

    def _supervisor(self):
        return EN.read_json(self.paths.settings)['runtime']['supervisor']

    def test_bare_array_round_executes_actions(self):
        # 实测坑的第二层：裸数组轮不能只解析得动，动作要真执行、且不许向玩家喷报错。
        self.queue = [
            '[{"type":"set-config","path":"runtime.supervisor","value":false},'
            '{"type":"preview"}]',
            '{"reply":"监督已关，预览也跑了","actions":[]}',
        ]
        printed = []
        history = []
        final = EN.send_exchange(self.paths, '关掉监督并预览', history,
                                 self._model, self._verb,
                                 known_stages={}, spec_parser=fake_spec_parser,
                                 snapshot_fn=lambda: 'SNAP', printer=printed.append)
        self.assertIs(self._supervisor(), False)          # set-config 落地
        self.assertEqual(len([c for c in self.verb_calls if '-DryRun' in c['args']]), 1)
        self.assertNotIn('解析失败', '\n'.join(printed))
        self.assertIn('监督已关', final)
        self.assertEqual(len(self.model_calls), 2)        # 空 reply 轮照常把回执送回模型

    def test_model_failure_breaks_cleanly(self):
        history = []
        final = EN.send_exchange(self.paths, '随便', history,
                                 lambda m: {'Ok': False, 'Error': '没网'}, lambda a, allowed=False: None,
                                 printer=lambda t: None, snapshot_fn=lambda: '')
        self.assertIn('问模型没成', final)


# ---------- M3：kb-search 第四动作(闸/包装/频控/去重/回路) ----------

class TestKbSearchGate(unittest.TestCase):
    def test_accepts_and_defaults(self):
        n = EN.test_action({'type': 'kb-search', 'query': '密合值满了会怎样', 'top': 2})
        self.assertTrue(n['Ok'])
        self.assertEqual(n['Query'], '密合值满了会怎样')
        self.assertEqual(n['Top'], 2)
        n = EN.test_action({'type': 'KB-Search', 'query': '  共鸣 触发 '})   # type 大小写不敏感同 PS
        self.assertTrue(n['Ok'])
        self.assertEqual(n['Top'], EN.KB_TOP_DEFAULT)
        self.assertEqual(n['Query'], '共鸣 触发')                              # 首尾空白剥掉

    def test_game_field_ignored(self):
        n = EN.test_action({'type': 'kb-search', 'query': 'x', 'game': 'ark'})  # 模型不许填 game：填了也只是忽略
        self.assertTrue(n['Ok'])
        self.assertNotIn('Game', n)

    def test_rejects(self):
        for act in ({'type': 'kb-search'},
                    {'type': 'kb-search', 'query': '   '},
                    {'type': 'kb-search', 'query': '长' * 201},
                    {'type': 'kb-search', 'query': 'x', 'top': 0},
                    {'type': 'kb-search', 'query': 'x', 'top': 6},
                    {'type': 'kb-search', 'query': 'x', 'top': '很多'}):
            self.assertFalse(EN.test_action(act)['Ok'], act)


class TestKbReceipt(unittest.TestCase):
    def _hit(self, i, text='正文'):
        return {'id': 'x%d' % i, 'game': 'r1999', 'title': '条目%d' % i, 'text': text,
                'score': 0.5, 'source': 'src%d' % i, 'updated': '2026-09-27'}

    def test_no_hits_wording(self):
        r = EN.format_kb_receipt({'ok': True, 'hits': [], 'why': '没找到'})
        self.assertIn('no_hits', r)
        self.assertIn('禁止编', r)

    def test_failure_wording(self):
        self.assertIn('知识库检索没成', EN.format_kb_receipt({'ok': False, 'hits': [], 'why': '模型不符'}))
        self.assertIn('知识库检索没成', EN.format_kb_receipt(None))

    def test_wrap_header_and_citations(self):
        r = EN.format_kb_receipt({'ok': True, 'hits': [self._hit(1)], 'why': ''})
        self.assertTrue(r.startswith(EN.KB_RECEIPT_HEADER))
        self.assertIn('出处：src1', r)
        self.assertIn('知识截至 2026-09-27', r)

    def test_per_block_truncation(self):
        r = EN.format_kb_receipt({'ok': True, 'hits': [self._hit(1, '字' * 2000)], 'why': ''},
                                 budget=100000)
        self.assertIn('块已截断', r)
        self.assertNotIn('字' * (EN.KB_PER_BLOCK + 1), r)

    def test_budget_drops_tail(self):
        hits = [self._hit(i, '字' * 800) for i in range(1, 6)]
        r = EN.format_kb_receipt({'ok': True, 'hits': hits, 'why': ''}, budget=1500)
        self.assertIn('回执预算被省略', r)
        self.assertIn('[1]', r)
        self.assertNotIn('[5]', r)

    def test_game_attribution_label(self):
        # P5 联合检索：回执每条钉【归属】——跨游戏引用对模型可见，unknown 域保留原值
        r = EN.format_kb_receipt({'ok': True, 'hits': [self._hit(1),
                                                        dict(self._hit(2), game='ark')], 'why': ''})
        self.assertIn('【逆向1999】条目1', r)
        self.assertIn('【明日方舟】条目2', r)
        r2 = EN.format_kb_receipt({'ok': True, 'hits': [{'game': 'zz', 'title': 'T', 'text': 'x'}], 'why': ''})
        self.assertIn('【zz】T', r2)
        r3 = EN.format_kb_receipt({'ok': True, 'hits': [{'game': 'general', 'title': 'G', 'text': 'x'}], 'why': ''})
        self.assertIn('【通用】G', r3)


class TestKbExchange(unittest.TestCase):
    """回路：模型提 kb-search → 引擎进程内检索 → 包装回执进下一轮 → 频控/去重话术。"""

    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-agent-kb-')
        self.paths = EN.Paths(self.dir)
        self.paths.settings.parent.mkdir(parents=True, exist_ok=True)
        self.paths.settings.write_text('{}', encoding='utf-8')
        self.kb_calls = []

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def _kb(self, query, top):
        self.kb_calls.append((query, top))
        return {'ok': True, 'hits': [{'id': 'a', 'game': 'r1999', 'title': 'T', 'text': query + '的正文',
                                      'score': 0.6, 'source': 's', 'updated': '2026-09-01'}],
                'rerank': True, 'why': ''}

    def _run(self, queue):
        holder = {'i': 0}

        def model(msgs):
            txt = queue[holder['i']] if holder['i'] < len(queue) else '{"reply":"完","actions":[]}'
            holder['i'] += 1
            return {'Ok': True, 'Text': txt}

        history = []
        state = {'seen': set()}
        EN.send_exchange(self.paths, '查点知识', history, model, lambda a, allowed=False: None,
                         snapshot_fn=lambda: '', printer=lambda t: None,
                         kb_fn=self._kb, kb_state=state)
        return history, state

    @staticmethod
    def _acts(q):
        return '{"reply":"查","actions":[{"type":"kb-search","query":"%s"}]}' % q

    def test_receipt_wrapped_and_passed_to_next_round(self):
        history, _ = self._run([self._acts('密合 机制'), '{"reply":"根据资料作答","actions":[]}'])
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn(EN.KB_RECEIPT_HEADER, joined)          # 外层包装进了回执与历史
        self.assertIn('出处：s', joined)

    def test_dedup_same_query_within_session(self):
        # 第 2 轮拿空白变体重查第 1 轮已查过的内容：吃"已检索过"，且不占频控预算
        queue = [self._acts('共鸣'), self._acts(' 共 鸣 '), self._acts('密合'),
                 '{"reply":"完","actions":[]}']
        history, _ = self._run(queue)
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('已检索过', joined)
        self.assertEqual([q for q, _ in self.kb_calls], ['共鸣', '密合'])   # 真检索只发生了两次

    def test_round_cap_two(self):
        # 同一份模型回复里排四条 kb-search：前两条真检索，后两条吃封顶话术
        queue = ['{"reply":"查","actions":[{"type":"kb-search","query":"a"},'
                 '{"type":"kb-search","query":"b"},{"type":"kb-search","query":"c"},'
                 '{"type":"kb-search","query":"d"}]}',
                 '{"reply":"完","actions":[]}']
        history, state = self._run(queue)
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('本轮已经检索过两次', joined)
        self.assertEqual(len(self.kb_calls), 2)
        self.assertEqual(state['round_used'], 2)

    def test_kb_fn_exception_becomes_receipt_not_crash(self):
        def bad(query, top):
            raise RuntimeError('索引炸了')
        queue = [self._acts('x'), '{"reply":"收到","actions":[]}']

        def model(msgs):
            return {'Ok': True, 'Text': queue.pop(0) if queue else '{"reply":"完","actions":[]}'}

        history = []
        EN.send_exchange(self.paths, '查', history, model,
                         lambda a, allowed=False: None, snapshot_fn=lambda: '',
                         printer=lambda t: None, kb_fn=bad)
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('检索出了意外', joined)
        self.assertIn('索引炸了', joined)

    def test_no_kb_fn_engine_says_not_connected(self):
        history = []
        EN.send_exchange(self.paths, '查', history,
                         lambda m: {'Ok': True, 'Text': self._acts('x')},
                         lambda a, allowed=False: None, snapshot_fn=lambda: '',
                         printer=lambda t: None)          # 不接 kb_fn 的旧装配(对拍/训练)：动作仍过闸，回执说没接
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('没接 kb_fn', joined)


# ---------- 第五动词 progression-lookup(数值走结构化表；同 kb-search 是 Python-only 增量，golden 不含) ----------

class TestProgGate(unittest.TestCase):
    def test_basic_and_case_insensitive(self):
        n = EN.test_action({'type': 'PROGRESSION-LOOKUP', 'char': ' 37 '})
        self.assertTrue(n['Ok'])
        self.assertEqual(n['Char'], '37')                  # trim 进规整值
        self.assertEqual(n['Line'], '')
        self.assertEqual(n['Stage'], '')

    def test_missing_char_rejected(self):
        self.assertFalse(EN.test_action({'type': 'progression-lookup'})['Ok'])
        self.assertFalse(EN.check_progression_lookup({'char': '   '})['Ok'])

    def test_length_limits(self):
        self.assertFalse(EN.check_progression_lookup({'char': '字' * (EN.PROG_CHAR_MAX + 1)})['Ok'])
        self.assertFalse(EN.check_progression_lookup({'char': 'a', 'line': 'x' * 21})['Ok'])
        self.assertFalse(EN.check_progression_lookup({'char': 'a', 'stage': 'x' * 21})['Ok'])

    def test_game_is_not_taken_from_model(self):
        n = EN.test_action({'type': 'progression-lookup', 'char': '37', 'game': 'ark'})
        self.assertTrue(n['Ok'])
        self.assertNotIn('Game', n)                        # game 由引擎按会话注入，模型填的被忽略


class TestProgLookupData(unittest.TestCase):
    """数据层用临时沙盒表钉死，不依赖本机 state/progression(那是本地件)。"""

    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-prog-')
        tab = {"_meta": {"updated": "2026-01-01"},
               "characters": {"37": {"insight": {"I": {"微尘": 8}, "III": {"币": 200000, "全知:": 16}},
                                    "resonance": {"15": {"币": 3200000}},
                                    "skills": [{"slot": "1", "name": "甲技", "levels": {"1": "效果一"}},
                                               {"slot": "3", "name": "丙技",
                                                "levels": {"1": "效果三",
                                                           "Rank Ⅲ": {"desc": "效果三满",
                                                                      "init": "38", "cost": "50",
                                                                      "dur": ""}}}],
                                    "gap": "洞悉线缺 II"},
                                    "无技能": {"insight": {"I": {"币": 1}}, "skills": []}}}
        pd = os.path.join(self.dir, 'state', 'progression')
        os.makedirs(pd)
        with open(os.path.join(pd, 'r1999.json'), 'w', encoding='utf-8') as f:
            f.write(json.dumps(tab, ensure_ascii=False))

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def test_full_hit(self):
        r = PG.lookup(self.dir, 'r1999', '37')
        self.assertTrue(r['ok'])
        self.assertIn('insight', r['lines'])
        self.assertEqual(r['gap'], '洞悉线缺 II')

    def test_line_stage_narrow(self):
        r = PG.lookup(self.dir, 'r1999', '37', 'insight', 'III')
        self.assertEqual(r['lines']['insight'], {'III': {'币': 200000, '全知:': 16}})

    def test_misses_are_honest(self):
        self.assertFalse(PG.lookup(self.dir, 'ark', '37')['ok'])            # 没这游戏的表
        self.assertFalse(PG.lookup(self.dir, 'r1999', '不存在')['ok'])
        self.assertFalse(PG.lookup(self.dir, 'r1999', '37', 'nosuchline')['ok'])
        self.assertFalse(PG.lookup(self.dir, 'r1999', '37', 'insight', 'VII')['ok'])

    def test_skills_line_cn_alias_and_slot(self):
        """技能文本走同一条查表路：中文线名"技能"→skills，stage 按槽筛(一/1 都认)。"""
        self.assertTrue(PG.lookup(self.dir, 'r1999', '37', '技能')['ok'])
        one = PG.lookup(self.dir, 'r1999', '37', '技能', '一')
        self.assertEqual([s['slot'] for s in one['lines']['skills']], ['1'])
        self.assertEqual(PG.lookup(self.dir, 'r1999', '37', 'skills', '3')['lines']['skills'],
                         PG.lookup(self.dir, 'r1999', '37', '技能', '三')['lines']['skills'])
        self.assertFalse(PG.lookup(self.dir, 'r1999', '37', '技能', '9')['ok'])   # 无此槽

    def test_skills_absent_is_honest_refusal(self):
        self.assertTrue(PG.lookup(self.dir, 'r1999', '无技能')['ok'])         # 角色在表内
        r = PG.lookup(self.dir, 'r1999', '无技能', '技能')
        self.assertFalse(r['ok'])
        self.assertIn('无技能文本', r['why'])

    def test_full_dump_includes_skills(self):
        self.assertIn('skills', PG.lookup(self.dir, 'r1999', '37')['lines'])


class TestProgSkillsFormat(unittest.TestCase):
    """_fmt_skills 排版：专精档带 初始/消耗/持续，与上一档完全相同的描述降成「同前」(省预算)。"""

    def test_rank_rows_and_dedupe(self):
        txt = PG._fmt_skills([{'slot': '3', 'name': '甲', 'levels': {
            '1': '造成250%伤害', '7': '造成320%伤害',
            'Rank Ⅰ': {'desc': '造成320%伤害', 'init': '38', 'cost': '53', 'dur': ''},
            'Rank Ⅲ': {'desc': '造成380%伤害', 'init': '38', 'cost': '50', 'dur': ''}}}], )
        self.assertIn('技能3【甲】', txt)
        self.assertIn('Rank Ⅰ：初始38/消耗53/持续-：同前', txt)     # 与 7 级逐字相同 → 不重排全文
        self.assertIn('Rank Ⅲ：初始38/消耗50/持续-：造成380%伤害', txt)

    def test_empty_slots(self):
        self.assertEqual(PG._fmt_skills([]), '')
        self.assertEqual(PG._fmt_skills(None), '')


class TestProgReceipt(unittest.TestCase):
    def test_ok_receipt_carries_meta_and_lines(self):
        r = EN.format_prog_receipt({'ok': True, 'why': '', 'game': 'r1999', 'char': '37',
                                    'meta': {'updated': '2026-09-28'},
                                    'lines': {'insight': {'III': {'币': 200000}}}})
        self.assertIn('养成表实查(r1999 · 37 · 数据采集 2026-09-28)', r)
        self.assertIn('200000', r)

    def test_miss_and_none(self):
        self.assertIn('养成表没查到：表内无角色',
                      EN.format_prog_receipt({'ok': False, 'why': '表内无角色「x」'}))
        self.assertIn('没给原因', EN.format_prog_receipt(None))

    def test_skills_receipt_is_prose_not_json(self):
        r = EN.format_prog_receipt({'ok': True, 'why': '', 'game': 'ark', 'char': '望',
                                    'meta': {'updated': '2026-09-28'},
                                    'lines': {'skills': [{'slot': '3', 'name': '天下劫',
                                                          'levels': {'1': '造成250%伤害'}}]}})
        self.assertIn('技能3【天下劫】', r)
        self.assertIn('造成250%伤害', r)
        self.assertNotIn('"levels"', r)          # 人话排版，不吐原始 JSON

    def test_budget_truncation(self):
        big = {'ok': True, 'why': '', 'game': 'ark', 'char': 'y', 'meta': {},
               'lines': {'skillup': {str(i): {'材': 99} for i in range(400)}}}
        r = EN.format_prog_receipt(big)
        self.assertIn('表太长已截断', r)
        self.assertLessEqual(len(r), EN.PROG_RECEIPT_BUDGET + 200)


class TestProgExchange(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-prog-x-')
        self.paths = EN.Paths(self.dir)
        self.paths.settings.parent.mkdir(parents=True, exist_ok=True)
        self.paths.settings.write_text('{}', encoding='utf-8')
        self.calls = []

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def _prog(self, char, line, stage):
        self.calls.append((char, line, stage))
        return {'ok': True, 'why': '', 'game': 'r1999', 'char': char,
                'meta': {'updated': '2026-09-28'},
                'lines': {'insight': {'III': {'币': 200000}}}}

    @staticmethod
    def _acts(char):
        return '{"reply":"查表","actions":[{"type":"progression-lookup","char":"%s"}]}' % char

    def _run(self, queue, prog_fn):
        holder = {'i': 0}

        def model(msgs):
            txt = queue[holder['i']] if holder['i'] < len(queue) else '{"reply":"完","actions":[]}'
            holder['i'] += 1
            return {'Ok': True, 'Text': txt}

        history = []
        EN.send_exchange(self.paths, '37 洞悉三要多少', history, model,
                         lambda a, allowed=False: None, snapshot_fn=lambda: '',
                         printer=lambda t: None, prog_fn=prog_fn)
        return history

    def test_receipt_into_next_round(self):
        history = self._run([self._acts('37'), '{"reply":"按表作答","actions":[]}'], self._prog)
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('养成表实查', joined)
        self.assertIn('200000', joined)
        self.assertEqual(self.calls, [('37', '', '')])

    def test_prog_fn_exception_becomes_receipt_not_crash(self):
        def bad(char, line, stage):
            raise RuntimeError('表文件炸了')
        history = self._run([self._acts('37'), '{"reply":"收到","actions":[]}'], bad)
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('查表出了意外', joined)
        self.assertIn('表文件炸了', joined)

    def test_no_prog_fn_engine_says_not_connected(self):
        history = []
        EN.send_exchange(self.paths, '查', history,
                         lambda m: {'Ok': True, 'Text': self._acts('37')},
                         lambda a, allowed=False: None, snapshot_fn=lambda: '',
                         printer=lambda t: None)          # 旧装配(对拍/训练)不接 prog_fn：动作过闸、回执如实说没接
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('没接 prog_fn', joined)


# ---------- 第六动词 farm-lookup(刷本效率走结构化表；推荐在数据层现算，golden 不含) ----------

class TestFarmGate(unittest.TestCase):
    def test_basic_and_case_insensitive(self):
        n = EN.test_action({'type': 'FARM-LOOKUP', 'material': ' 龙门币 '})
        self.assertTrue(n['Ok'])
        self.assertEqual(n['Material'], '龙门币')            # trim 进规整值
        self.assertEqual(n['Mode'], '')

    def test_missing_material_rejected(self):
        self.assertFalse(EN.test_action({'type': 'farm-lookup'})['Ok'])
        self.assertFalse(EN.check_farm_lookup({'material': '   '})['Ok'])

    def test_mode_whitelist(self):
        self.assertTrue(EN.check_farm_lookup({'material': 'x', 'mode': 'cheapest'})['Ok'])
        self.assertTrue(EN.check_farm_lookup({'material': 'x', 'mode': 'ALL'})['Ok'])  # 大小写归一
        self.assertFalse(EN.check_farm_lookup({'material': 'x', 'mode': 'bestest'})['Ok'])

    def test_length_limit(self):
        self.assertFalse(EN.check_farm_lookup({'material': '字' * (EN.FARM_MATERIAL_MAX + 1)})['Ok'])

    def test_game_is_not_taken_from_model(self):
        n = EN.test_action({'type': 'farm-lookup', 'material': '碳', 'game': 'ark'})
        self.assertTrue(n['Ok'])
        self.assertNotIn('Game', n)                          # game 由引擎按会话注入


class TestFarmLookupData(unittest.TestCase):
    """数据层用临时沙盒表钉死推荐口径，不依赖本机 state/drops(那是本地件)。"""

    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-farm-')
        # 龙门币：只有 CE 材料本(理智 10/20/36)→ 推荐应取最高档 36
        # 轻锰矿：只有主线概率掉落关(理智 15/18)→ 推荐应取理智最低 15
        # 芯片组：材料本(36)+剿灭(60)混在→ 材料本优先，取 36
        tab = {"_meta": {"updated": "2026-05-05"}, "by_material": {
            "龙门币": [
                {"stage": "CE-1_a", "name": "CE-1", "cost": 10, "kind": "材料本", "slot": "common"},
                {"stage": "CE-3_b", "name": "CE-3", "cost": 20, "kind": "材料本", "slot": "common"},
                {"stage": "CE-6_c", "name": "CE-6", "cost": 36, "kind": "材料本", "slot": "common"}],
            "轻锰矿": [
                {"stage": "3-2_x", "name": "3-2", "cost": 15, "kind": "关卡", "slot": "common"},
                {"stage": "4-7_y", "name": "4-7", "cost": 18, "kind": "关卡", "slot": "common"}],
            "近卫芯片组": [
                {"stage": "PR-D-2_z", "name": "PR-D-2", "cost": 36, "kind": "材料本", "slot": "common"},
                {"stage": "炎国 市区", "name": "市区", "cost": 60, "kind": "剿灭", "slot": "first"}],
            "技巧概要·卷1": [
                {"stage": "CA-1_k", "name": "CA-1", "cost": 10, "kind": "材料本", "slot": "common"},
                {"stage": "CA-5_m", "name": "CA-5", "cost": 30, "kind": "材料本", "slot": "common"}],
            "碳": [{"stage": "SK-1_c", "name": "SK-1", "cost": None, "kind": "材料本", "slot": "common"}],
        }}
        dd = os.path.join(self.dir, 'state', 'drops')
        os.makedirs(dd)
        with open(os.path.join(dd, 'ark.json'), 'w', encoding='utf-8') as f:
            f.write(json.dumps(tab, ensure_ascii=False))

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def test_material_ben_recommends_top_tier(self):
        r = DR.lookup(self.dir, 'ark', '龙门币')
        self.assertTrue(r['ok'])
        self.assertEqual(r['best']['stage'], 'CE-6_c')       # 材料本取理智最高档
        self.assertEqual(r['ranked'][0]['stage'], 'CE-6_c')  # 降序展示

    def test_no_ben_recommends_cheapest(self):
        r = DR.lookup(self.dir, 'ark', '轻锰矿')
        self.assertEqual(r['best']['stage'], '3-2_x')        # 无专用本→理智最低

    def test_ben_beats_exterminatus(self):
        r = DR.lookup(self.dir, 'ark', '近卫芯片组')
        self.assertEqual(r['best']['stage'], 'PR-D-2_z')     # 材料本优先于剿灭
        self.assertEqual(r['series'], 'PR')

    def test_cheapest_mode_forces_low(self):
        r = DR.lookup(self.dir, 'ark', '龙门币', 'cheapest')
        self.assertEqual(r['best']['stage'], 'CE-1_a')       # cheapest 强制最低

    def test_all_mode_lists_everyone_no_best(self):
        r = DR.lookup(self.dir, 'ark', '近卫芯片组', 'all')
        self.assertIsNone(r['best'])
        self.assertEqual(r['total_sources'], 2)

    def test_unique_substring_resolves(self):
        r = DR.lookup(self.dir, 'ark', '近卫芯片')            # 是"近卫芯片组"的…不，反过来
        # "近卫芯片" 不是任何键的子串命中多个? 近卫芯片组 含 近卫芯片 → 唯一命中
        self.assertTrue(r['ok'])
        self.assertEqual(r['material'], '近卫芯片组')

    def test_none_cost_stage_still_recommended_as_ben(self):
        r = DR.lookup(self.dir, 'ark', '碳')                  # 材料本但 cost=None
        self.assertTrue(r['ok'])
        self.assertEqual(r['best']['stage'], 'SK-1_c')

    def test_misses_are_honest(self):
        self.assertFalse(DR.lookup(self.dir, 'r1999', 'x')['ok'])           # 没这游戏的表
        self.assertFalse(DR.lookup(self.dir, 'ark', '不存在的材料')['ok'])
        r = DR.lookup(self.dir, 'ark', '')
        self.assertFalse(r['ok'])
        self.assertIn('没给材料名', r['why'])


class TestFarmReceipt(unittest.TestCase):
    def test_ok_receipt_has_best_and_note(self):
        r = EN.format_farm_receipt({'ok': True, 'why': '', 'game': 'ark', 'material': '龙门币',
                                    'meta': {'updated': '2026-09-28'}, 'series': 'CE',
                                    'note': '材料本取最高档', 'total_sources': 6,
                                    'best': {'stage': 'CE-6_c', 'name': 'CE-6', 'cost': 36, 'kind': '材料本'},
                                    'ranked': [{'stage': 'CE-6_c', 'name': 'CE-6', 'cost': 36, 'kind': '材料本'}]})
        self.assertIn('刷本实查(ark · 材料「龙门币」· 数据采集 2026-09-28)', r)
        self.assertIn('最划算：CE-6', r)
        self.assertIn('36理智', r)

    def test_miss_and_none(self):
        self.assertIn('刷本表没查到：表内无材料',
                      EN.format_farm_receipt({'ok': False, 'why': '表内无材料「x」'}))
        self.assertIn('没给原因', EN.format_farm_receipt(None))

    def test_unknown_cost_shows_question(self):
        r = EN.format_farm_receipt({'ok': True, 'why': '', 'game': 'ark', 'material': '碳',
                                    'meta': {}, 'note': 'x', 'total_sources': 1,
                                    'best': {'stage': 'SK-1_c', 'name': 'SK-1', 'cost': None, 'kind': '材料本'},
                                    'ranked': []})
        self.assertIn('理智?', r)

    def test_receipt_with_eff_shows_community_footer(self):
        r = EN.format_farm_receipt({'ok': True, 'why': '', 'game': 'ark', 'material': '龙门币',
                                    'meta': {'updated': '2026-09-28'}, 'series': 'CE', 'has_eff': True,
                                    'note': '按每理智期望掉落件数排序(Penguin-Stats 社区统计)', 'total_sources': 2,
                                    'best': {'stage': 'CE-6_c', 'name': 'CE-6', 'cost': 36, 'kind': '材料本',
                                             'qty': 10000.0, 'eff': 277.778},
                                    'ranked': [{'stage': 'CE-6_c', 'name': 'CE-6', 'cost': 36, 'kind': '材料本',
                                                'qty': 10000.0, 'eff': 277.778}]})
        self.assertIn('每局≈10000.0件·277.778件/理智', r)
        self.assertIn('Penguin-Stats 社区统计', r)
        self.assertNotIn('不含每场数量/掉率', r)


class TestFarmEffPriority(unittest.TestCase):
    """DROP2：表内有 qty/eff(Penguin 统计)时按 件/理智 降序推荐；无 eff 关排后；cheapest 不受影响。"""

    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-eff-')
        tab = {"_meta": {"updated": "2026-09-28"}, "by_material": {
            "龙门币": [
                {"stage": "CE-6_c", "name": "CE-6", "cost": 36, "kind": "材料本", "slot": "common",
                 "qty": 10000.0, "eff": 277.778},
                {"stage": "10-8_m", "name": "10-8", "cost": 21, "kind": "关卡", "slot": "common",
                 "qty": 3480.0, "eff": 165.714},
                {"stage": "剿灭X_y", "name": "剿灭X", "cost": 60, "kind": "剿灭", "slot": "first"}],
            # 效率最高档不是理智最低关：证明按 eff 而非旧"最低理智"口径
            "轻锰矿": [
                {"stage": "3-2_x", "name": "3-2", "cost": 15, "kind": "关卡", "slot": "common",
                 "qty": 0.3, "eff": 0.02},
                {"stage": "5-4_y", "name": "5-4", "cost": 21, "kind": "关卡", "slot": "common",
                 "qty": 1.05, "eff": 0.05}],
        }}
        dd = os.path.join(self.dir, 'state', 'drops')
        os.makedirs(dd)
        with open(os.path.join(dd, 'ark.json'), 'w', encoding='utf-8') as f:
            f.write(json.dumps(tab, ensure_ascii=False))

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def test_eff_ordering_beats_tier_rule(self):
        r = DR.lookup(self.dir, 'ark', '轻锰矿')
        self.assertTrue(r['has_eff'])
        self.assertEqual(r['best']['stage'], '5-4_y')      # eff 高者优先，哪怕理智更高
        self.assertIn('Penguin-Stats', r['note'])

    def test_no_eff_sources_sort_last(self):
        r = DR.lookup(self.dir, 'ark', '龙门币')
        self.assertEqual(r['best']['stage'], 'CE-6_c')
        stages = [e['stage'] for e in r['ranked']]
        self.assertEqual(stages[-1], '剿灭X_y')            # 无统计关沉底

    def test_cheapest_mode_ignores_eff(self):
        # cheapest 恒按理智升序：CE-6(eff最高)让位给理智最低的 10-8(21)
        r = DR.lookup(self.dir, 'ark', '龙门币', 'cheapest')
        self.assertEqual(r['best']['stage'], '10-8_m')

    def test_wiki_fixed_source_named_in_note(self):
        # 固定产出线(龙门币这类 penguin 不统计的)：note 要说 wiki 出处，不冒充社区统计
        with io.open(os.path.join(self.dir, 'state', 'drops', 'ark.json'), encoding='utf-8') as f:
            tab = json.load(f)
        tab['by_material']['高级材料'] = [
            {"stage": "4-1_w", "name": "4-1", "cost": 18, "kind": "关卡", "slot": "common",
             "qty": 2700, "eff": 150.0, "src": "wiki-fixed"}]
        with io.open(os.path.join(self.dir, 'state', 'drops', 'ark.json'), 'w', encoding='utf-8') as f:
            f.write(json.dumps(tab, ensure_ascii=False))
        r = DR.lookup(self.dir, 'ark', '高级材料')
        self.assertTrue(r['has_eff'])
        self.assertIn('wiki 效率表固定产出', r['note'])
        self.assertNotIn('Penguin-Stats', r['note'])

    def test_best_alt_is_top_non_ben(self):
        # "CE 没开"现算替补：排除 CE 系列后效率最高的 10-8，无统计的剿灭关不顶位
        r = DR.lookup(self.dir, 'ark', '龙门币')
        self.assertEqual(r['best_alt']['stage'], '10-8_m')

    def test_receipt_shows_alt_line(self):
        r = EN.format_farm_receipt(DR.lookup(self.dir, 'ark', '龙门币', 'all'))
        self.assertIn('非材料本替补(该系列没开/打不了专用本时用)：10-8', r)

    def test_fallback_when_no_stats_at_all(self):
        # 只有无 eff 的材料：走旧材料本最高档回退口径，note 点明回退
        with io.open(os.path.join(self.dir, 'state', 'drops', 'ark.json'), encoding='utf-8') as f:
            tab = json.load(f)
        tab['by_material']['技巧之章'] = [
            {"stage": "CA-1_k", "name": "CA-1", "cost": 10, "kind": "材料本", "slot": "common"},
            {"stage": "CA-5_m", "name": "CA-5", "cost": 30, "kind": "材料本", "slot": "common"}]
        with open(os.path.join(self.dir, 'state', 'drops', 'ark.json'), 'w', encoding='utf-8') as f:
            f.write(json.dumps(tab, ensure_ascii=False))
        r = DR.lookup(self.dir, 'ark', '技巧之章')
        self.assertFalse(r['has_eff'])
        self.assertEqual(r['best']['stage'], 'CA-5_m')      # 旧口径：材料本最高档
        self.assertIn('回退档位口径', r['note'])


class TestFarmExchange(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-farm-x-')
        self.paths = EN.Paths(self.dir)
        self.paths.settings.parent.mkdir(parents=True, exist_ok=True)
        self.paths.settings.write_text('{}', encoding='utf-8')
        self.calls = []

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def _farm(self, material, mode=''):
        self.calls.append((material, mode))
        return {'ok': True, 'why': '', 'game': 'ark', 'material': material,
                'meta': {'updated': '2026-09-28'}, 'note': 'n', 'series': 'CE', 'total_sources': 1,
                'best': {'stage': 'CE-6_c', 'name': 'CE-6', 'cost': 36, 'kind': '材料本'},
                'ranked': [{'stage': 'CE-6_c', 'name': 'CE-6', 'cost': 36, 'kind': '材料本'}]}

    @staticmethod
    def _acts(material):
        return '{"reply":"查刷本表","actions":[{"type":"farm-lookup","material":"%s"}]}' % material

    def _run(self, queue, farm_fn):
        holder = {'i': 0}

        def model(msgs):
            txt = queue[holder['i']] if holder['i'] < len(queue) else '{"reply":"完","actions":[]}'
            holder['i'] += 1
            return {'Ok': True, 'Text': txt}

        history = []
        EN.send_exchange(self.paths, '刷龙门币去哪划算', history, model,
                         lambda a, allowed=False: None, snapshot_fn=lambda: '',
                         printer=lambda t: None, farm_fn=farm_fn)
        return history

    def test_receipt_into_next_round(self):
        history = self._run([self._acts('龙门币'), '{"reply":"按表作答","actions":[]}'], self._farm)
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('刷本实查', joined)
        self.assertIn('最划算：CE-6', joined)
        self.assertEqual(self.calls, [('龙门币', '')])

    def test_farm_fn_exception_becomes_receipt_not_crash(self):
        def bad(material, mode=''):
            raise RuntimeError('掉落表炸了')
        history = self._run([self._acts('碳'), '{"reply":"收到","actions":[]}'], bad)
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('查表出了意外', joined)
        self.assertIn('掉落表炸了', joined)

    def test_no_farm_fn_engine_says_not_connected(self):
        history = []
        EN.send_exchange(self.paths, '查', history,
                         lambda m: {'Ok': True, 'Text': self._acts('碳')},
                         lambda a, allowed=False: None, snapshot_fn=lambda: '',
                         printer=lambda t: None)      # 旧装配不接 farm_fn：动作过闸、回执如实说没接
        joined = '\n'.join(h['text'] for h in history)
        self.assertIn('没接 farm_fn', joined)

    def test_empty_reply_not_printed(self):
        """教材 §6 话术纪律：纯查表轮 reply 为空是正常的，不许印出"agent："空行刷存在感。"""
        lines = []
        holder = {'i': 0}
        queue = ['{"reply":"","actions":[{"type":"farm-lookup","material":"龙门币"}]}',
                 '{"reply":"刷 10-8。","actions":[]}']

        def model(msgs):
            txt = queue[holder['i']] if holder['i'] < len(queue) else '{"reply":"完","actions":[]}'
            holder['i'] += 1
            return {'Ok': True, 'Text': txt}

        EN.send_exchange(self.paths, 'CE 没开刷龙门币去哪', [], model,
                         lambda a, allowed=False: None, snapshot_fn=lambda: '',
                         printer=lines.append, farm_fn=self._farm)
        agent_lines = [l for l in lines if l.startswith('agent：')]
        self.assertEqual(agent_lines, ['agent：刷 10-8。'])     # 只有结论那一句，没有空行
        self.assertTrue(any(l.startswith('(回执)') for l in lines))


class TestPickKbGames(unittest.TestCase):
    """P5 live 装配纯函数：kbAgentGames 列表优先，没配回落单值 kbAgentGame(旧行为逐字不变)。"""

    def test_games_list_wins(self):
        self.assertEqual(LV.pick_kb_games({'kbAgentGames': ['r1999', 'ark'], 'kbAgentGame': 'r1999'}),
                         ['r1999', 'ark'])

    def test_strip_dedupe_keep_order(self):
        self.assertEqual(LV.pick_kb_games({'kbAgentGames': [' ark ', 'ark', 'r1999']}), ['ark', 'r1999'])

    def test_fallback_single(self):
        self.assertEqual(LV.pick_kb_games({'kbAgentGame': 'ark'}), ['ark'])
        self.assertEqual(LV.pick_kb_games({'kbAgentGames': [], 'kbAgentGame': 'ark'}), ['ark'])
        self.assertEqual(LV.pick_kb_games({'kbAgentGames': [' ', ''], 'kbAgentGame': 'x'}), ['x'])

    def test_empty_means_no_domain(self):
        self.assertEqual(LV.pick_kb_games({}), [])

    def test_env_override_pure(self):
        """P5 补刀：GDAGENT_KB_GAME 设了就优先(域=[X])；未设/空白=None(照走 settings)。"""
        self.assertEqual(LV.kb_game_override({'GDAGENT_KB_GAME': 'ark'}), ['ark'])
        self.assertEqual(LV.kb_game_override({'GDAGENT_KB_GAME': ' ark '}), ['ark'])
        self.assertIsNone(LV.kb_game_override({}))
        self.assertIsNone(LV.kb_game_override({'GDAGENT_KB_GAME': '  '}))
        self.assertIsNone(LV.kb_game_override(None))

    def test_env_override_accepts_list(self):
        """10-06 跨游戏训练轮：逗号列表扩成多域(对比题要两游戏同时在检索域里)；
        单值行为逐字不变，空白段丢弃，全空=None。"""
        self.assertEqual(LV.kb_game_override({'GDAGENT_KB_GAME': 'ark,r1999'}), ['ark', 'r1999'])
        self.assertEqual(LV.kb_game_override({'GDAGENT_KB_GAME': ' ark , r1999 , '}), ['ark', 'r1999'])
        self.assertEqual(LV.kb_game_override({'GDAGENT_KB_GAME': 'ark,ark'}), ['ark'])
        self.assertIsNone(LV.kb_game_override({'GDAGENT_KB_GAME': ' , '}))


# ---------- 活层里可离线钉的两件：降级快照 / 假件装配 ----------

class TestLiveOffline(unittest.TestCase):
    def test_degraded_snapshot_shape(self):
        d = tempfile.mkdtemp(prefix='gd-live-snap-')
        try:
            paths = EN.Paths(d)
            paths.agent_dir.mkdir(parents=True)
            paths.journal.write_text('{"ts":"2026-09-27 10:00:00","op":"set","path":"a.b","old":1,"new":2,"backup":""}\n',
                                     encoding='utf-8')
            txt = LV.degraded_snapshot(paths, {'ark': {'farmTargets': ['X=1@Y'], 'farmAppend': True}})
            self.assertIn('配置：', txt)
            self.assertIn('补刷判表：快照助手没应答', txt)   # 缺什么就明说什么缺
            self.assertIn('最近留底：', txt)
            self.assertNotIn('"ts":', txt.split('最近留底：')[1])
        finally:
            import shutil
            shutil.rmtree(d, ignore_errors=True)

    def test_verb_line_filter(self):
        self.assertTrue(LV.VERB_LINE.search('[STEP] 开始'))
        self.assertTrue(LV.VERB_LINE.search('结果：一切正常'))
        self.assertIsNone(LV.VERB_LINE.search('一行普通日志'))
        self.assertEqual(LV.VERB_LEAD.sub('', '[WARN] 理智不足'), ' 理智不足')


# ---------- M6 GUI 轮：两击确认(第一击只出计划 / 第二击逐条重闸后真跑) ----------

class TestTwoClickPlan(unittest.TestCase):
    """面板那颗"确认执行"按钮的后端契约。全程假模型假 verb：不联网、不起真子进程。"""

    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='gd-agent-plan-')
        self.paths = EN.Paths(self.dir)
        self.paths.settings.parent.mkdir(parents=True, exist_ok=True)
        self.paths.settings.write_text('{"ark":{},"runtime":{}}', encoding='utf-8')
        self.verb_calls = []
        self.confirm_calls = []

    def tearDown(self):
        import shutil
        shutil.rmtree(self.dir, ignore_errors=True)

    def _model(self, queue):
        def f(msgs):
            if queue:
                return {'Ok': True, 'Text': queue.pop(0)}
            return {'Ok': False, 'Error': '自检钉死：不该再问模型'}
        return f

    def _verb(self, args, allowed=False):
        self.verb_calls.append({'args': list(args), 'allowed': allowed})
        return {'Ran': True, 'Exit': 0, 'Detail': '假执行：' + ' '.join(args)}

    def _ask(self, *args):
        return '{"reply":"%s","actions":[%s]}' % (args[0], args[1] if len(args) > 1 else '')

    # ---- 第一击 ----

    def test_first_click_lists_plan_and_never_runs(self):
        queue = [self._ask('要真跑了', '{"type":"run-verb","args":["-FarmArk","-Games","ark"]}'),
                 self._ask('好')]
        r = EN.plan_exchange(self.paths, '明天开始刷方舟', [], self._model(queue), self._verb,
                             spec_parser=fake_spec_parser, snapshot_fn=lambda: 'SNAP')
        self.assertTrue(r['Ok'])
        self.assertEqual(self.verb_calls, [])            # 要真跑的那一步一步都没执行
        self.assertEqual(r['Plan'], [{'Args': ['-FarmArk', '-Games', 'ark'],
                                      'Desc': '-FarmArk -Games ark'}])
        self.assertTrue(EN.PLAN_ID_SHAPE.match(r['Id']))
        self.assertTrue(EN.plan_path(self.paths, r['Id']).is_file())

    def test_pending_wins_over_the_confirm_hook(self):
        # 两个口子同时给了：计划档优先。机读口不该有人在子进程里等一颗键盘上的 y。
        # 夹具用 -Ack1999：它仍入口真参数、仍算 mutating(-AckArk 已于 10-12 随入口退场，白名单不收非法参数)。
        queue = [self._ask('要真跑', '{"type":"run-verb","args":["-Ack1999"]}'), self._ask('好')]
        pending = []
        EN.send_exchange(self.paths, '跑一轮', [], self._model(queue), self._verb,
                         spec_parser=fake_spec_parser, snapshot_fn=lambda: '',
                         printer=lambda t: None, pending=pending,
                         confirm_fn=lambda d: self.confirm_calls.append(d) or True)
        self.assertEqual(self.confirm_calls, [])
        self.assertEqual(self.verb_calls, [])
        self.assertEqual(pending, [{'Args': ['-Ack1999'], 'Desc': '-Ack1999'}])

    def test_plan_receipt_tells_the_model_it_was_not_run(self):
        seen = {}

        def model(msgs):
            if not seen.get('second'):
                seen['second'] = True
                return {'Ok': True, 'Text': self._ask('要真跑',
                                                      '{"type":"run-verb","args":["-Ack1999"]}')}
            seen['receipt'] = '\n'.join(m['content'] for m in msgs)
            return {'Ok': True, 'Text': self._ask('收到')}
        EN.plan_exchange(self.paths, '跑一轮', [], model, self._verb,
                         spec_parser=fake_spec_parser, snapshot_fn=lambda: '')
        self.assertIn('本轮没执行', seen['receipt'])
        self.assertIn('等玩家当面确认', seen['receipt'])

    def test_nothing_to_confirm_returns_empty_id(self):
        queue = [self._ask('只是看看', '{"type":"preview"}'), self._ask('看完了')]
        r = EN.plan_exchange(self.paths, '预览一下', [], self._model(queue), self._verb,
                             spec_parser=fake_spec_parser, snapshot_fn=lambda: '')
        self.assertTrue(r['Ok'])
        self.assertEqual(r['Plan'], [])
        self.assertEqual(r['Id'], '')
        self.assertEqual(len(self.verb_calls), 1)        # 预览是只读的，照旧自动放行
        self.assertIn('-DryRun', self.verb_calls[0]['args'])

    # ---- 计划本体 ----

    def test_plan_id_and_shape_guards(self):
        self.assertEqual(EN.new_plan_id(ts='20260929-121530', salt='ab12'), '20260929-121530-ab12')
        self.assertTrue(EN.PLAN_ID_SHAPE.match(EN.new_plan_id()))
        for bad in ('', 'x', '../../etc/passwd', '2026-09-29 12:15:30', 'ab12'):
            ld = EN.load_plan(self.paths, bad)
            self.assertFalse(ld['Ok'])
            self.assertIn('形状', ld['Why'])             # 坏代号先拒，绝不去拼路径
        self.assertIsNone(EN.plan_items({'items': []}))
        self.assertIsNone(EN.plan_items({'items': [{'args': []}]}))
        # 夹具一律用入口真参数：plan_items 只看形状不查白名单，但写一枚已退场的 -AckArk 会误导后人。
        self.assertIsNone(EN.plan_items({'items': [{'args': ['-ArkShopDone', '']}]}))
        self.assertEqual(EN.plan_items({'items': [{'args': ['-DryRun']}]})[0]['Desc'], '-DryRun')

    def test_save_and_load_roundtrip(self):
        # 夹具换 -FarmArk(入口真参数、mutating)：这几条测的是盘上往返与清理，跟具体开关名无关，
        # 但白名单不收 -AckArk(10-12 退场)之后，留着它会让第二击那几条的"合法/被拒"分组失去意义。
        sv = EN.save_plan(self.paths, {'id': '20260929-121530-ab12',
                                       'items': [{'args': ['-FarmArk'], 'desc': '真跑方舟'}]})
        self.assertTrue(sv['Ok'])
        self.assertTrue(sv['Plan']['created'])            # created 自动补了一个
        ld = EN.load_plan(self.paths, '20260929-121530-ab12')
        self.assertTrue(ld['Ok'])
        self.assertEqual(ld['Items'][0]['Args'], ['-FarmArk'])

    def test_old_plans_get_pruned_and_the_current_one_stays(self):
        ids = ['2026092%d-121530-ab%02d' % (i, i) for i in range(5)]
        for pid in ids:
            self.assertTrue(EN.save_plan(self.paths, {'id': pid, 'created': pid,
                                                      'items': [{'args': ['-FarmArk']}]}, keep=2)['Ok'])
        left = sorted(p.stem for p in self.paths.plans_dir.glob('*.json'))
        self.assertEqual(left, ids[-2:])                  # 每次只留最近 2 份，且刚写的那份永不被清掉

    # ---- 第二击 ----

    def test_second_click_refuses_args_not_on_the_whitelist(self):
        plan = {'items': [{'args': ['-Force'], 'desc': '越门参数'}]}
        r = EN.run_plan(self.paths, plan, self._verb)
        self.assertEqual(self.verb_calls, [])            # 盘上的旧事实不作数：重闸在先
        self.assertFalse(r['Ok'])
        self.assertEqual(r['Results'][0]['Status'], '被拒')
        self.assertIn('白名单', r['Results'][0]['Detail'])

    def test_second_click_runs_with_allowed_flag(self):
        plan = {'items': [{'args': ['-FarmArk', '-Games', 'ark'], 'desc': '刷一轮方舟'}]}
        r = EN.run_plan(self.paths, plan, self._verb)
        self.assertTrue(r['Ok'])
        self.assertEqual(r['Why'], '')
        self.assertEqual(self.verb_calls, [{'args': ['-FarmArk', '-Games', 'ark'], 'allowed': True}])
        rows = [json.loads(ln) for ln in
                self.paths.journal.read_text(encoding='utf-8').splitlines() if ln.strip()]
        self.assertEqual(rows[-1]['op'], 'run-plan')                        # 真跑过什么留得下账
        self.assertEqual(EN.rollback_plan([ln for ln in
                                           self.paths.journal.read_text(encoding='utf-8').splitlines()
                                           if ln.strip()]), [])             # 新 op 不进回滚账

    def test_second_click_stops_after_a_failed_step(self):
        calls = []

        def verb(args, allowed=False):
            calls.append(list(args))
            return {'Ran': True, 'Exit': 1 if len(calls) == 1 else 0, 'Detail': '假失败'}
        plan = {'items': [{'args': ['-FarmArk'], 'desc': 'A'}, {'args': ['-Ack1999'], 'desc': 'B'}]}
        r = EN.run_plan(self.paths, plan, verb)
        self.assertEqual(len(calls), 1)                  # 第一步没成，第二步绝不接着烧理智
        self.assertEqual(r['Results'][1]['Status'], '未执行')
        self.assertFalse(r['Ok'])
        self.assertEqual(r['Skipped'], 1)

    def test_second_click_survives_a_verb_that_throws(self):
        def verb(args, allowed=False):
            raise RuntimeError('子进程起了个意外')
        plan = {'items': [{'args': ['-Ack1999'], 'desc': 'A'}]}
        r = EN.run_plan(self.paths, plan, verb)
        self.assertFalse(r['Ok'])
        self.assertEqual(r['Results'][0]['Status'], '没成')
        self.assertIn('意外', r['Results'][0]['Detail'])

    # ---- 机读口没有 stdin：默认确认钩子的第二层兜底 ----

    def test_default_confirm_without_stdin_is_not_confirmed(self):
        queue = [self._ask('要真跑', '{"type":"run-verb","args":["-Ack1999"]}'), self._ask('了解')]
        old = sys.stdin
        sys.stdin = None                                  # GUI 起子进程时的真实形状：input() 抛 RuntimeError
        buf, sys.stdout = sys.stdout, io.StringIO()       # 那句"按不确认处理"是人话，别喷进自检输出
        try:
            final = EN.send_exchange(self.paths, '跑一轮', [], self._model(queue), self._verb,
                                     spec_parser=fake_spec_parser, snapshot_fn=lambda: '',
                                     printer=lambda t: None)
        finally:
            sys.stdin = old
            sys.stdout = buf
        self.assertIn('了解', final)                      # 整轮没被炸掉
        self.assertEqual(self.verb_calls, [])             # 拿不到回答 = 没确认 = 绝不放行


class TestNoticeParser(unittest.TestCase):
    """公告抓取件(kb_notices)的纯函数夹具，10-05「让agent会读公告」。不碰网。"""

    def _push(self, payload):
        # 官网形状：payload 作为 JS 字符串塞进 __next_f.push，内层引号要转义
        inner = payload.replace('\\', '\\\\').replace('"', '\\"')
        return '<script>self.__next_f.push([1,"%s"])</script>' % inner

    def _list_html(self):
        seg_bad = '<script>self.__next_f.push([1,"bad \\u sequence "])</script>'  # \u 后非 4 hex → 真坏段
        items = [{"cid": "1455", "tab": "1", "title": "[活动预告]矢量突破#3「拟生态」[未闭合",
                  "author": "【明日方舟】运营组", "displayTime": 1790305200, "brief": "关卡开放时间：09月29日"},
                 {"cid": "5101", "tab": "1", "title": "SideStory「昨日海」",
                  "author": "【明日方舟】运营组", "displayTime": 1790996400, "brief": "10月09日 12:00"},
                 {"cid": "0000", "tab": "0", "title": "", "displayTime": 1}]  # 缺 title → 弃
        payload = 'e:{"initialData":{"LATEST":{"list":%s}}}' % json.dumps(items, ensure_ascii=False)
        return seg_bad + self._push(payload)

    def test_extract_items_skips_bad_segment_and_incomplete_rows(self):
        got = KN.extract_notice_items(self._list_html())
        self.assertEqual([it["cid"] for it in got], ["1455", "5101"])
        self.assertEqual(got[0]["displayTime"], 1790305200)
        self.assertIn("[未闭合", got[0]["title"])           # 字符串里的括号不打扰配对

    def test_extract_items_anchor_absent_returns_empty(self):
        self.assertEqual(KN.extract_notice_items("<html>页面形状变了</html>"), [])

    def test_detail_body_cuts_head_date_and_nav_tail(self):
        html = ('<html><head><style>.x{color:red}</style><script>var s="资源收集 不该出现";</script></head>'
                '<body><h1>标题 - 明日方舟</h1><div>2026 // 09 / 25</div>'
                '<p>二、<strong>&ldquo;资源收集&rdquo;</strong>关卡限时全天开放</p>'
                '<p>活动时间： 09月29日 16:00 - 10月20日 03:59</p><p>【明日方舟】运营组</p>'
                '<div>INFO</div><div>返回列表</div></body></html>')
        body = KN.notice_body_text(html)
        # 行内标签(</strong>)按口径替换成空格，只断言关键子串，不断整句连续
        self.assertIn('“资源收集”', body)                    # 实体解码生效
        self.assertIn('关卡限时全天开放', body)
        self.assertIn('09月29日 16:00 - 10月20日 03:59', body)
        self.assertNotIn('不该出现', body)                  # script 内容绝不进正文
        self.assertNotIn('返回列表', body)                  # 导航锚起全弃
        self.assertTrue(body.endswith('【明日方舟】运营组'))

    def test_build_notice_md_front_matter_and_brief_fallback(self):
        it = {"cid": "1455", "title": "[活动预告]矢量突破#3", "author": "运营组",
              "displayTime": 1790305200, "brief": "关卡开放时间：09月29日", "tab": "1"}
        md = KN.build_notice_md(it, "正文一行")
        self.assertIn("game: ark", md)
        self.assertIn("updated: %s" % time.strftime("%Y-%m-%d", time.localtime(1790305200)), md)
        self.assertIn("source: web:https://ak.hypergryph.com/news/1455 官网公告", md)
        self.assertIn("正文一行", md)
        md2 = KN.build_notice_md(it, "")                    # 正文退化：如实标 + 保 brief
        self.assertIn("正文未取到", md2)
        self.assertIn(it["brief"], md2)


if __name__ == '__main__':
    unittest.main(verbosity=2)