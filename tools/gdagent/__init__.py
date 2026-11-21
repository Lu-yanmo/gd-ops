"""gdagent —— game-daily 的 agent 需求管家 + 游戏知识库层（Python）。

技术栈定案见 docs/kb-plan.md §0：agent + KB 整层用 Python，Gd.exe/C# 判定半边不动。
本包 M1 只落地知识库核（kb/*）；engine.py / model.py（回路迁 Python）在 M2 补。

约定：所有对外动词一律 stdout 出 JSON 信封（{ok, ..., why}），日志/进度走 stderr。
"""

__all__ = ["config", "envelope", "kb"]
