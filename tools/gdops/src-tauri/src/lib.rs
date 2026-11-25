// 壳后端：app_info（平台/架构/版本现量）+ 本地只读快照（live_snapshot）+ 打开日志目录
// + 单链预演（chain_run/chain_stop）+ 控制通道（summary/真点火/停止/门旗标/改定时/只问不点）
// + 助手通道（MAA/M9A 读数、计划改写、几枚真操作）。
mod assist;
mod capture;
mod chain;
mod control;
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
        // 验收钩子：GDOPS_AUTOASSIST=1 起壳后自动打开「助手」页（只剩读数，不点任何操作）。
        "autoassist": std::env::var("GDOPS_AUTOASSIST").map(|v| v == "1").unwrap_or(false),
        // 验收钩子：GDOPS_AUTOTAB=m9a 让自动打开的「助手」页直接落在 M9A 页签（仍不点任何操作）。
        "autotab": std::env::var("GDOPS_AUTOTAB").unwrap_or_default(),
        // 验收钩子：GDOPS_AUTOSEL=ark|r1999 起壳读完备后把「游戏路」选到这一路（只切选中，不点火）。
        "autosel": std::env::var("GDOPS_AUTOSEL").unwrap_or_default(),
        // 验收钩子：GDOPS_AUTODRAWER=1|m9a 读数到位后自动拉起右侧二级框（只开不保存）。
        "autodrawer": std::env::var("GDOPS_AUTODRAWER").unwrap_or_default(),
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

// 控制通道一律走阻塞线程：起 panel-cs.exe / pwsh 等子进程，别卡 WebView 主线程。
macro_rules! blocking_cmd {
    ($name:ident, $call:expr) => {
        #[tauri::command]
        async fn $name() -> Result<serde_json::Value, String> {
            tauri::async_runtime::spawn_blocking($call)
                .await
                .map_err(|e| e.to_string())?
        }
    };
}

blocking_cmd!(panel_summary, control::summary);

#[tauri::command]
async fn live_capture(game: String) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || capture::frame(&game))
        .await
        .map_err(|e| e.to_string())?
}

#[tauri::command]
async fn start_round(games: Option<String>) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || control::start_round(games))
        .await
        .map_err(|e| e.to_string())?
}

blocking_cmd!(stop_round, control::stop_round);

#[tauri::command]
async fn mark_flag(flag: String) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || control::mark_flag(flag))
        .await
        .map_err(|e| e.to_string())?
}

#[tauri::command]
async fn reschedule(times: Vec<String>) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || control::reschedule(times))
        .await
        .map_err(|e| e.to_string())?
}

#[tauri::command]
async fn vision_ask_live(game: Option<String>) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || control::vision_ask(game))
        .await
        .map_err(|e| e.to_string())?
}

blocking_cmd!(assist_state, assist::state);
blocking_cmd!(assist_maa_close, assist::maa_close);
blocking_cmd!(assist_ark_start, assist::ark_start);
blocking_cmd!(assist_m9a_stop, assist::m9a_stop);
blocking_cmd!(assist_mumu_start, assist::mumu_start);
blocking_cmd!(assist_mumu_stop, assist::mumu_stop);
blocking_cmd!(assist_m9a_update, assist::m9a_update);

#[tauri::command]
async fn assist_save_plan(edits: Vec<serde_json::Value>) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || assist::save_plan(edits))
        .await
        .map_err(|e| e.to_string())?
}

#[tauri::command]
async fn assist_m9a_switch(id: String, name: String) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || assist::m9a_switch(id, name))
        .await
        .map_err(|e| e.to_string())?
}

#[tauri::command]
async fn assist_maa_set_task(kind: String, enable: bool) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || assist::maa_set_task(kind, enable))
        .await
        .map_err(|e| e.to_string())?
}

#[tauri::command]
async fn assist_maa_set_all(enable: bool) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || assist::maa_set_all(enable))
        .await
        .map_err(|e| e.to_string())?
}

#[tauri::command]
async fn assist_m9a_save_instance(
    id: String,
    name: String,
    patch: serde_json::Value,
) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || assist::m9a_save_instance(id, name, patch))
        .await
        .map_err(|e| e.to_string())?
}

// 右侧二级框保存：一条 MAA 任务的若干键（原位改写 + 备份 + 回读校验；MAA 在跑拒写）。
#[tauri::command]
async fn assist_maa_save_task(
    idx: usize,
    task_type: String,
    name: String,
    patch: serde_json::Value,
) -> Result<serde_json::Value, String> {
    tauri::async_runtime::spawn_blocking(move || assist::maa_save_task(idx, task_type, name, patch))
        .await
        .map_err(|e| e.to_string())?
}

pub fn run() {
    tauri::Builder::default()
        .manage(ChainState(Default::default()))
        .invoke_handler(tauri::generate_handler![
            app_info,
            live_snapshot,
            live_capture,
            open_log_dir,
            chain_run,
            chain_stop,
            panel_summary,
            start_round,
            stop_round,
            mark_flag,
            reschedule,
            vision_ask_live,
            assist_state,
            assist_save_plan,
            assist_m9a_switch,
            assist_m9a_save_instance,
            assist_maa_set_task,
            assist_maa_set_all,
            assist_maa_save_task,
            assist_maa_close,
            assist_ark_start,
            assist_m9a_stop,
            assist_mumu_start,
            assist_mumu_stop,
            assist_m9a_update
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
