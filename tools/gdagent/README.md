# gdagent —— game-daily 的 agent + 游戏知识库层（Python）

技术栈定案与完整设计见仓库根的 `docs/kb-plan.md`。M1 落的是知识库核，M2 起对话回路也在本包
（`engine.py` 纯流程 + `model.py` 模型往返 + `live.py` 活层装配）；里程碑逐轮实录看 kb-plan §7。

## 怎么跑

无需设 PYTHONPATH，用仓库根的 shim：

```
python tools\kb.py import <md文件或目录> --game ark|general|r1999 [--source 名]
python tools\kb.py search "火蓝之心 掉落" --game ark --top 6
python tools\kb.py stats
python tools\kb.py drop --game ark --yes
python tools\kb.py compact
python tools\kb.py rebuild     # 换 embed 模型/索引损坏：无条件全量重算
python tools\kb.py reindex     # 增量对齐：未变块复用向量、省额度
python tools\kb.py rollback    # CURRENT 拨回上一代
python tools\kb.py probe       # 真实 embed 连通性（需 key）
```

所有动词 **stdout 只出一行 JSON 信封**（`{ok, ..., why}`），日志/进度走 stderr。
等价写法（cd 到 tools 后）：`python -m gdagent <verb> ...`。

## agent 的四条口（人用 / 两击确认 / 对拍）

```
python -m gdagent agent                    # 人用终端 REPL（输出是人话，不是信封）
python -m gdagent agent --ask "…"          # 人用终端单发一句，办完就退
python -m gdagent agent-plan --ask "…"     # 两击确认·第一击：整轮照跑，要真跑的步骤只列进计划
python -m gdagent agent-run --plan <代号>  # 两击确认·第二击：按代号取回计划，逐条重闸后真跑
python -m gdagent agent-eval --cases x --out y   # PS↔Python 双跑对拍口（probe-agent-golden 喂它）
```

`agent-plan` / `agent-run` 是给 C# 面板那颗对话小窗用的机读口，两条铁律：
**stdout 只有信封**、**全程不读标准输入**（GUI 起的子进程后面没有键盘，等一颗 y 就是死一轮）。
护栏一条没松：set-config / preview / 三张查表按现行语义当场办（改配置有备份+journal 可回滚），
只有"会真跑一轮 daily-clear"的步骤才要第二击；计划落 `state\agent\plans\<代号>.json`（只留最近 20 份），
第二击**不信盘上那份旧事实** —— 每条参数重新过一遍白名单闸，闸拒的记一条、真跑没成的当场收尾不再往下。

## 前置：key 与端点

- embed/rerank 的 API key **只从环境变量读**：先 `KB_API_KEY`，回落 `DASHSCOPE_API_KEY`。不落命令行、不落文件。
- 端点/模型走 `config\settings.json → runtime`（这些键 agent 永不许改，见 kb-plan §3）：
  `kbEmbedModel`（默认 text-embedding-v3）、`kbRerankModel`（空=不重排）、`kbBaseUrl`、
  `kbEmbedBatch`（默认 10）、`kbTimeoutSeconds`（默认 60）。
- `kbBaseUrl` 没给时从 `visionBaseUrl` 推 base。真接通以 `kb probe` 实测响应为准。

## 存储

`state\kb\`（已 gitignore）：`gen-<N>\{chunks.jsonl, vectors.bin}` + `CURRENT` 指针。
写全是"产出完整新代再切指针"，半写不可见；只保当前+上一代（回滚窗口）。

## 自检 / 基准

```
python tools\gdagent\tests\test_kb.py            # 37 项离线断言，不联网、不读本机配置
python tools\kb_bench.py --n 5000 --dim 1024     # 暴力余弦计时（纯 Python vs numpy）
```

## 模块地图

| 文件 | 职责 | 纯/活 |
|---|---|---|
| `config.py` | settings 只读 + env key + 端点推导 | 活(读文件) |
| `envelope.py` | stdout 信封 / stderr 日志的唯一出口 | 活 |
| `kb/chunk.py` | md 切块 + front matter + 双哈希 | **纯** |
| `kb/vectors.py` | vectors.bin 版本化头 + checksum + 模型校验 | **纯** |
| `kb/store.py` | 代际目录/CURRENT/tombstone/原子写/回滚 | 混合 |
| `kb/search.py` | game 过滤 + 余弦 topK(numpy 快路/纯兜底) + rerank | 混合 |
| `kb/embed.py` | OpenAI 兼容 /embeddings：batch+退避重试+错误分类 | 活(可注入) |
| `kb/manager.py` | import/search/drop/compact/rebuild/reindex 编排 | 活(可注入) |
