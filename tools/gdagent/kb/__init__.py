"""kb —— 游戏知识库核（切块 / 向量文件 / 代际存储 / 检索 / embed）。

纯函数（chunk / vectors 编解码 / 余弦 / tombstone 合并 / 双哈希）不碰文件不联网，直接喂断言；
活函数（embed / rerank HTTP、store 读写）按命名可被同名注入替换（与 ask.py / lib\\agent.ps1 同一手法）。
"""
