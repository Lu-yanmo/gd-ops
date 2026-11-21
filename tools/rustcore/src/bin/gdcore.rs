// gdcore —— R3 内置件这枚 bin 的壳：源码与 gdprobe 是**同一枚**(src\main.rs)。
//
// 为什么是这个形状（两条备选都实测过、都记在这，省得下一个改的人再试一遍）：
//   - `include!("../main.rs")`：编不过。main.rs 第 29 行那枚 `#![allow(non_snake_case)]`
//     是**内层属性**，include! 展开处不接受（rustc 原话 "an inner attribute is not permitted in this context"）。
//   - Cargo.toml 两条 [[bin]] 指同一个 `path`：cargo 1.98.1 实测**能编能跑**（两枚产物还是硬链接、同 inode），
//     但每条 manifest 都会带一句 warning（"file found to be present in multiple build targets"）——
//     那是历史错误降级来的，将来可能再收回去；本仓拿编译输出当读数面，不背这条。
//   - 本形状（`#[path]` 模块）：模块文件**允许**内层属性打头，main.rs 一行不用挪；
//     代价只有一处 —— 模块外的 crate root 要调 `imp::main()`，所以 main.rs 的 `fn main` 是 `pub` 的。
// 两边跑的是同一套自检与同一套参数解析：build.ps1 对 Cargo.toml 里点到的每个 bin 各跑一遍 --selftest + 契约号门。
#[path = "../main.rs"]
mod imp;

fn main() {
    imp::main()
}
