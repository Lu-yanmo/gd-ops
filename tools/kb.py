"""kb.py —— 路径无关入口：python tools\\kb.py <verb> ... 无需先 cd tools 或设 PYTHONPATH。

`python -m gdagent` 需要 tools/ 在 sys.path 上；这个 shim 把 tools/ 插进去再转交，
薄转发只干这一件事。所有动词/信封契约见 gdagent/__main__.py。
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gdagent.__main__ import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
