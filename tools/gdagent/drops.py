# -*- coding: utf-8 -*-
"""刷本效率的只读查询 —— 第六动词 farm-lookup 的数据层(DROP 轮立项)。

数据口径(kb-plan 既定：数值走结构化表、不进向量库)：逐关理智+掉落归属实抓落成
state/drops/<game>.json(merge_drops.py，_meta.updated 记采集日)。本模块是唯一实现。

"划不划算"的推荐**在查询侧现算、不写死进表**：
  * 表内有 qty/eff 时(DROP2 两条线并入，条目 src 标出处：penguin=Penguin-Stats 逐关统计、
    wiki-fixed=prts 道具页效率表的固定产出)，直接按**件/理智(eff)降序**推荐，无统计关沉底；
  * 该材料所有来源都没统计时回退旧档位口径：材料本(AP/CA/CE/LS/PR/SK)取**理智最高档**、
    无专用本取**理智最低**关；mode=cheapest 恒给理智最低关；mode=all 给全量清单。
纯本地只读：没有表/没有材料都如实拒，绝不编数。仓导出环境没有 state/drops 时
返回 ok:false 的人话拒绝 —— 设计行为，不是故障。qty/eff 是统计期望值，不是掉率承诺。
"""
import io
import json
import os

_MATERIAL_PREFIX = ('AP-', 'CA-', 'CE-', 'LS-', 'PR-', 'SK-')


def load_table(root, game):
    path = os.path.join(str(root), "state", "drops", "%s.json" % game)
    if not os.path.exists(path):
        return None, "没有 %s 的掉落表(%s)，先跑采集建表" % (game, path)
    with io.open(path, encoding="utf-8") as f:
        return json.load(f), ""


def _is_material_ben(key):
    return any(str(key).startswith(p) for p in _MATERIAL_PREFIX)


def _resolve(bm, material):
    """材料名匹配：精确 → 大小写 → 唯一子串；命中多个子串时返回候选让上层如实说。"""
    if material in bm:
        return material, []
    low = material.lower()
    for k in bm:
        if k.lower() == low:
            return k, []
    subs = [k for k in bm if low in k.lower()]
    if len(subs) == 1:
        return subs[0], []
    return None, subs


def lookup(root, game, material, mode=""):
    """返回信封 dict：{ok, why, game, material, meta, best, best_alt, ranked, series, note, candidates?}。
    best_alt=排除该材料专用本系列后效率最高的关，供"CE 没开/打不了专用本"这类场合现算替补。"""
    game = str(game or "").strip()
    material = str(material or "").strip()
    mode = str(mode or "").strip().lower()
    if not game:
        return {"ok": False, "why": "没给游戏(表按游戏分档：ark/r1999)"}
    if not material:
        return {"ok": False, "why": "没给材料名(查的是『刷某个材料去哪/划不划算』)"}
    table, why = load_table(root, game)
    if table is None:
        return {"ok": False, "why": why}
    bm = table.get("by_material") or {}
    key, cands = _resolve(bm, material)
    if key is None:
        if cands:
            return {"ok": False, "why": "材料「%s」匹配到多个，说全名之一：%s" % (
                material, "、".join(cands[:12])), "candidates": cands[:12]}
        return {"ok": False, "why": "表内无材料「%s」；已收录 %d 种(可按名查，勿凭记忆编)" % (
            material, len(bm))}
    entries = bm[key]
    if not entries:
        return {"ok": False, "why": "「%s」无掉落来源记录" % key}

    material_ben = [e for e in entries if _is_material_ben(e.get("stage", ""))]
    costed = [e for e in entries if e.get("cost") is not None]
    effed = [e for e in entries if (e.get("eff") or 0) > 0]

    def _eff(e):
        try:
            return float(e.get("eff") or 0)
        except (TypeError, ValueError):
            return 0.0

    best = None
    note = ""
    ranked = []
    series = ""
    if mode == "cheapest":
        pool = costed or entries
        ranked = sorted(pool, key=lambda e: (e["cost"] if e.get("cost") is not None else 9999))
        best = ranked[0] if ranked else None
        note = "理智最低优先(打不动高难本时的省理智选择)"
    elif effed:
        # DROP2：有每局件数/理智效率(eff)时直接按 件/理智 降序。出处按条目 src 如实组合：
        # penguin=社区统计期望；wiki-fixed=关卡固定产出(wiki 道具页效率表)
        ranked = sorted(entries, key=lambda e: (-_eff(e), e.get("cost") if e.get("cost") is not None else 9999))
        best = ranked[0]
        if _is_material_ben(best.get("stage", "")):
            series = str(best.get("stage", "")).split("-")[0]
        srcs = set(str(e.get("src") or "penguin") for e in effed)
        parts = []
        if "penguin" in srcs:
            parts.append("Penguin-Stats 社区统计期望")
        if "wiki-fixed" in srcs:
            parts.append("wiki 效率表固定产出")
        note = "按每理智掉落件数排序(%s，非官方、非实时；剿灭/险地等无统计关排后)" % "、".join(parts)
    elif material_ben:
        # 材料本系列：按理智降序即按档位降序，最高档=每理智掉落最多=最划算(无统计时回退)
        ranked = sorted(material_ben, key=lambda e: -(e["cost"] if e.get("cost") is not None else -1))
        best = ranked[0]
        series = str(best.get("stage", "")).split("-")[0]
        note = "%s 材料本按理智折算，取最高档最划算(表内该材料专用本；该关无社区统计，回退档位口径)" % series
    else:
        pool = costed or entries
        ranked = sorted(pool, key=lambda e: (e["cost"] if e.get("cost") is not None else 9999))
        best = ranked[0] if ranked else None
        note = "无专用材料本，给理智最低的掉落关(概率掉落关卡)"

    if mode == "all":
        best = None
        ranked = sorted(entries, key=lambda e: (-_eff(e), e.get("cost") if e.get("cost") is not None else 9999)) \
            if effed else sorted(entries, key=lambda e: (e["cost"] if e.get("cost") is not None else 9999))
        note = "全量掉落关清单(%s)" % ("按理智效率降序" if effed else "按理智升序")

    # "CE 没开"类问题的现算替补：排除专用材料本系列后效率最高的关(全清单前 6 常被子系列占满，单列一条)
    best_alt = None
    if effed:
        alts = [e for e in ranked if not _is_material_ben(e.get("stage", ""))]
        best_alt = alts[0] if alts else None

    out = {
        "ok": True, "why": "", "game": game, "material": key,
        "asked": material, "meta": table.get("_meta", {}),
        "best": best, "ranked": ranked, "series": series, "note": note,
        "has_eff": bool(effed), "best_alt": best_alt,
        "material_ben_count": len(material_ben), "total_sources": len(entries),
    }
    return out
