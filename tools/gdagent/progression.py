# -*- coding: utf-8 -*-
"""逐角色养成数值的只读查询 —— 第五动词 progression-lookup 的数据层(M4 立项、本轮落码)。

数据口径(kb-plan §0 既定)：精确数值不进向量库，走结构化表 state/progression/<game>.json
(实抓 wiki 落成，_meta.updated 记采集日)。本模块是唯一实现，
tools/progression.py 的 CLI 只是它的薄壳；agent 侧经 live.make_prog_fn 进程内直调。
纯本地只读：没有表/没有角色都如实拒，不编数。仓导出环境里没有 state/progression 时
返回 ok:false 的人话拒绝 —— 这是设计行为，不是故障。
"""
import io
import json
import os


def load_table(root, game):
    path = os.path.join(str(root), "state", "progression", "%s.json" % game)
    if not os.path.exists(path):
        return None, "没有 %s 的养成表(%s)，先补真实采集" % (game, path)
    with io.open(path, encoding="utf-8") as f:
        return json.load(f), ""


_CN_LINES = {
    "技能": "skills", "技能文本": "skills",
    "精英化": "elite", "专精": "skillup", "技能升级": "skillup",
    "洞悉": "insight", "共鸣": "resonance", "调频": "tuning", "塑造": "shaping",
}

_CN_STAGES = {"一": "1", "二": "2", "三": "3", "1": "1", "2": "2", "3": "3",
              "四": "4", "五": "5", "6": "6", "7": "7"}

_RANK_ORDER = ("Rank Ⅰ", "Rank Ⅱ", "Rank Ⅲ")


def _fmt_skills(slots):
    """skills 字段的文本排版：一槽一段，等级档只给效果、专精档给 初始/消耗/持续+效果。
    方舟页内各档描述常只差一个百分数，逐档全文会把 1800 字预算吃光：
    与上一档**完全相同**的降成「同前」，有差异的照排；差异里只标出变化的数字串时留全文(不猜语义)。"""
    out = []
    for s in slots or []:
        out.append("技能%s【%s】" % (s.get("slot") or "?", s.get("name") or "?"))
        levels = s.get("levels") or {}
        prev = None
        order = sorted([k for k in levels if not str(k).startswith("Rank")],
                       key=lambda x: int(x) if str(x).isdigit() else 99) + \
            [k for k in _RANK_ORDER if k in levels]
        for lv in order:
            v = levels[lv]
            if lv.startswith("Rank"):
                desc = (v or {}).get("desc", "")
                head = "  %s：初始%s/消耗%s/持续%s" % (
                    lv, (v or {}).get("init", "?"), (v or {}).get("cost", "?"),
                    (v or {}).get("dur") or "-")
            else:
                desc = v or ""
                head = "  %s级" % lv
            if prev is not None and desc == prev:
                out.append(head + "：同前(仅上方数值变化)")
            else:
                out.append(head + "：" + desc)
            prev = desc
    return "\n".join(out)


def lookup(root, game, char, line="", stage=""):
    """返回信封 dict：{ok, why, game, char, meta, lines, gap?} —— 与 CLI stdout 的 JSON 同形状。"""
    game = str(game or "").strip()
    char = str(char or "").strip()
    line = str(line or "").strip()
    stage = str(stage or "").strip()
    line = _CN_LINES.get(line, line)
    if not game:
        return {"ok": False, "why": "没给游戏(表按游戏分档：ark/r1999)"}
    if not char:
        return {"ok": False, "why": "没给角色名(查询是精确全名匹配)"}
    table, why = load_table(root, game)
    if table is None:
        return {"ok": False, "why": why}
    chars = table.get("characters") or {}
    key = char.lower()
    hit = None
    for name, data in chars.items():
        if key in (name.lower(), str(data.get("name", "")).lower()):
            hit = (name, data)
            break
    if hit is None:
        return {"ok": False, "why": "表内无角色「%s」；已收录 %d 名(可按名查，勿凭记忆编)" % (
            char, len(chars))}
    name, data = hit
    out = {"ok": True, "why": "", "game": game, "char": name,
           "meta": table.get("_meta", {})}
    if line:
        lines = {k: v for k, v in data.items() if k not in ("name", "gap")}
        want = line.lower()
        if line == "skills":
            slots = data.get("skills") or []
            if not slots:
                return {"ok": False, "why": "「%s」无技能文本(页内无技能段或本轮未采集)" % name}
            sel = _CN_STAGES.get(stage, stage)
            if sel:
                slots = [s for s in slots if str(s.get("slot")) == sel]
                if not slots:
                    return {"ok": False, "why": "「%s」无技能槽「%s」；有：%s" % (
                        name, stage, "、".join(str(s.get("slot")) for s in data.get("skills") or []))}
            out["lines"] = {"skills": slots}
        else:
            match = {k: v for k, v in lines.items() if k.lower() == want}
            if not match:
                return {"ok": False, "why": "「%s」无养成线「%s」；有：%s" % (
                    name, line, "、".join(lines.keys()) or "空")}
            if stage:
                src = match.get(line) or list(match.values())[0]
                line_key = line if line in match else list(match.keys())[0]
                sel = {k: v for k, v in (src or {}).items() if k.lower() == stage.lower()}
                if not sel:
                    return {"ok": False, "why": "「%s」线无档「%s」；有：%s" % (
                        line_key, stage, "、".join((src or {}).keys()) or "空")}
                match = {line_key: sel}
            out["lines"] = match
    else:
        out["lines"] = {k: v for k, v in data.items()
                        if k in ("insight", "resonance", "tuning", "elite", "skillup",
                                 "level_breakroom", "level_breakthrough", "shaping", "skills")}
    if data.get("gap"):
        out["gap"] = data["gap"]
    return out
