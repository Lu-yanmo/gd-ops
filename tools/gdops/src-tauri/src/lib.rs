// 壳后端：app_info（平台/架构/版本现量）+ 本地只读快照（live_snapshot）+ 打开日志目录
// + 单链预演（chain_run/chain_stop：起 pwsh 跑 daily-clear.ps1 -DryRun，逐行回传）。
// 纪律：只跑预演档、不写配置、不读 settings.json 的值；真点火留给后续阶段。
mod chain;
mod live;

// 预演进程槽：存 pwsh 的 pid，同一时刻只允许一轮，中断只针对本应用起的进程树。
pub struct ChainState(pub chain::Slot);

#[tauri::command]
fn chain_run(app: tauri::AppHandle, state: tauri::State<ChainState>) -> Result<serde_json::Value, String> {
    chain::start(app, state.0.clone())
}

#[tauri::command]
fn chain_stop(state: tauri::State<ChainState>) -> Result<bool, String> {
    chain::stop(state.0.clone())
}

#[tauri::command]
fn app_info() -> serde_json::Value {
    serde_json::json!({
        "app": "gd-ops",
        "version": env!("CARGO_PKG_VERSION"),
        "platform": std::env::consts::OS,
        "arch": std::env::consts::ARCH,
        // 验收钩子：GDOPS_AUTODRY=1 起壳后自动跑一轮 -DryRun（等同点模态里的按钮；不点火）。
        "autodry": std::env::var("GDOPS_AUTODRY").map(|v| v == "1").unwrap_or(false),
    })
}

// 快照要 spawn tasklist / 读盘：放到阻塞线程，别卡 WebView 主线程。
#[tauri::command]
async fn live_snapshot() -> serde_json::Value {
    tauri::async_runtime::spawn_blocking(live::collect)
        .await
        .unwrap_or_else(|e| serde_json::json!({ "ok": false, "error": e.to_string() }))
}

#[tauri::command]
fn open_log_dir() -> Result<String, String> {
    live::open_log_dir()
}

pub fn run() {
    tauri::Builder::default()
        .manage(ChainState(Default::default()))
        .invoke_handler(tauri::generate_handler![
            app_info,
            live_snapshot,
            open_log_dir,
            chain_run,
            chain_stop
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
