// 单链执行：桌面壳起 PowerShell 7 跑 daily-clear.ps1 -DryRun，把 stdout/stderr 逐行回传给前端。
// 纪律：只跑预演档（-DryRun -NoElevate，不点火）；同一时刻只允许一轮；终止只针对本应用起的进程树；
// 不读 config/settings.json 的值，也不写任何配置。
use serde_json::{json, Value};
use std::io::{BufRead, BufReader, Read};
use std::process::{Command, Stdio};
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::{Arc, Mutex};
use std::time::Instant;
use tauri::{AppHandle, Emitter};

pub type Slot = Arc<Mutex<Option<u32>>>;

pub fn start(app: AppHandle, slot: Slot) -> Result<Value, String> {
    let root = crate::live::find_root()
        .ok_or_else(|| "找不到仓库根目录（把 gdops.exe 放进仓库，或设 GDOPS_ROOT）".to_string())?;
    let script = root.join("daily-clear.ps1");
    if !script.is_file() {
        return Err(format!("缺少入口脚本：{}", script.display()));
    }
    {
        let g = slot.lock().map_err(|_| "进程槽不可用".to_string())?;
        if g.is_some() {
            return Err("已有一轮预演在跑（等它结束或先中断）".to_string());
        }
    }

    let mut cmd = Command::new("pwsh");
    cmd.current_dir(&root)
        .arg("-NoProfile")
        .arg("-NonInteractive")
        .arg("-File")
        .arg(&script)
        .arg("-DryRun")
        .arg("-NoElevate")
        .arg("-Tag")
        .arg("manual")
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped());
    #[cfg(target_os = "windows")]
    {
        use std::os::windows::process::CommandExt;
        cmd.creation_flags(0x0800_0000); // CREATE_NO_WINDOW：别闪黑框
    }

    let mut child = cmd
        .spawn()
        .map_err(|e| format!("起 pwsh 失败：{e}（需要 PowerShell 7 在 PATH）"))?;
    let pid = child.id();
    *slot.lock().map_err(|_| "进程槽不可用".to_string())? = Some(pid);

    let lines = Arc::new(AtomicUsize::new(0));
    let mut pumps: Vec<std::thread::JoinHandle<()>> = Vec::new();
    if let Some(out) = child.stdout.take() {
        let a = app.clone();
        let n = lines.clone();
        pumps.push(std::thread::spawn(move || pump(out, "out", a, n)));
    }
    if let Some(err) = child.stderr.take() {
        let a = app.clone();
        let n = lines.clone();
        pumps.push(std::thread::spawn(move || pump(err, "err", a, n)));
    }

    let slot2 = slot.clone();
    std::thread::spawn(move || {
        let t0 = Instant::now();
        let st = child.wait();
        for h in pumps {
            let _ = h.join();
        }
        let secs = t0.elapsed().as_secs_f64();
        let code = st.ok().and_then(|s| s.code()).unwrap_or(-1);
        if let Ok(mut g) = slot2.lock() {
            *g = None;
        }
        let _ = app.emit(
            "chain-end",
            json!({ "code": code, "secs": secs, "lines": lines.load(Ordering::Relaxed) }),
        );
    });

    Ok(json!({ "ok": true, "pid": pid, "script": script.display().to_string() }))
}

// 逐行泵：按 \n 切、剥行尾 CR，非法 UTF-8 用 lossy 兜底（别让一行坏字节断掉整条流）。
fn pump<R: Read + Send + 'static>(r: R, stream: &'static str, app: AppHandle, n: Arc<AtomicUsize>) {
    let mut rd = BufReader::new(r);
    let mut buf: Vec<u8> = Vec::new();
    loop {
        buf.clear();
        match rd.read_until(b'\n', &mut buf) {
            Ok(0) | Err(_) => break,
            Ok(_) => {}
        }
        let raw = String::from_utf8_lossy(&buf);
        let line = raw.trim_end_matches(['\r', '\n']);
        if line.is_empty() {
            continue;
        }
        n.fetch_add(1, Ordering::Relaxed);
        let text: String = line.chars().take(4000).collect();
        let _ = app.emit("chain-line", json!({ "stream": stream, "line": text }));
    }
}

pub fn stop(slot: Slot) -> Result<bool, String> {
    let pid = slot.lock().map_err(|_| "进程槽不可用".to_string())?.take();
    let Some(pid) = pid else { return Ok(false) };

    #[cfg(target_os = "windows")]
    {
        use std::os::windows::process::CommandExt;
        let st = Command::new("taskkill")
            .args(["/PID", &pid.to_string(), "/T", "/F"])
            .creation_flags(0x0800_0000)
            .status();
        return st.map(|s| s.success()).map_err(|e| e.to_string());
    }
    #[cfg(not(target_os = "windows"))]
    {
        let st = Command::new("kill").args(["-TERM", &pid.to_string()]).status();
        return st.map(|s| s.success()).map_err(|e| e.to_string());
    }
}
