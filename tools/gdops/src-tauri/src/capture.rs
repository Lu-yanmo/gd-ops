// 执行现场实时画面：ARK 走端游窗口 PrintWindow；M9A 走当前实例 ADB screencap。
// 只读捕获，不恢复/激活窗口、不注入输入；序列号和绝对路径只留在 Rust 内。
use crate::assist::{capture_target, scrub, CaptureTarget};
use png::{ColorType, Decoder, Transformations};
use serde_json::{json, Value};
use std::io::Cursor;
use std::sync::{Mutex, MutexGuard};
#[cfg(any(target_os = "windows", test))]
use std::sync::mpsc;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

static CAPTURE_LOCK: Mutex<()> = Mutex::new(());
#[cfg(target_os = "windows")]
static WINDOW_CAPTURE_ACTIVE: std::sync::atomic::AtomicBool = std::sync::atomic::AtomicBool::new(false);

pub fn frame(game: &str) -> Result<Value, String> {
    let target = capture_target(game).map_err(|e| scrub(&e))?;
    if !target.process.is_empty() {
        capture_window(&target.process)
    } else {
        let _guard: MutexGuard<()> = CAPTURE_LOCK.lock().map_err(|_| "画面捕获锁不可用".to_string())?;
        capture_adb(&target)
    }
}

fn now_ms() -> u128 {
    SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_millis()).unwrap_or(0)
}

fn encode_rgba(rgba: &[u8], width: u32, height: u32) -> Result<String, String> {
    let mut out = Vec::new();
    {
        let mut enc = png::Encoder::new(&mut out, width, height);
        enc.set_color(ColorType::Rgba);
        enc.set_depth(png::BitDepth::Eight);
        enc.set_compression(png::Compression::Fast);
        enc.write_header().map_err(|e| format!("画面编码失败：{e}"))?
            .write_image_data(rgba).map_err(|e| format!("画面编码失败：{e}"))?;
    }
    Ok(format!("data:image/png;base64,{}", base64(&out)))
}

fn ready(png: &str, width: u32, height: u32, source: &str, mean_luma: f64) -> Value {
    json!({
        "ok": true, "state": if mean_luma < 8.0 { "dark" } else { "ready" },
        "source": source, "image": png, "width": width, "height": height,
        "capturedAt": now_ms(), "meanLuma": (mean_luma * 10.0).round() / 10.0,
        "note": (if mean_luma < 8.0 { "画面偏暗；若游戏已退出，请核对目标窗口" } else { "" })
    })
}

fn failure(state: &str, error: impl AsRef<str>) -> Result<Value, String> {
    Ok(json!({ "ok": false, "state": state, "error": scrub(error.as_ref()) }))
}

#[cfg(target_os = "windows")]
mod win {
    use super::{encode_rgba, failure, ready};
    use serde_json::Value;
    use windows::Win32::Foundation::{CloseHandle, HWND, LPARAM, RECT};
    use windows::Win32::Graphics::Gdi::{
        BI_RGB, BITMAPINFO, BITMAPINFOHEADER, CreateCompatibleDC, CreateDIBSection,
        DeleteDC, DeleteObject, DIB_RGB_COLORS, GetDC, HGDIOBJ, ReleaseDC, SelectObject,
    };
    use windows::Win32::System::Threading::{OpenProcess, PROCESS_NAME_FORMAT, PROCESS_QUERY_LIMITED_INFORMATION, QueryFullProcessImageNameW};
    use windows::Win32::UI::WindowsAndMessaging::{
        EnumWindows, GetClientRect, GetWindowThreadProcessId, IsIconic, IsWindowVisible,
    };
    use windows::Win32::Storage::Xps::{PrintWindow, PRINT_WINDOW_FLAGS};

    struct WindowPick { best: HWND, best_area: i64, saw: bool, process: String }

    unsafe extern "system" fn visit(hwnd: HWND, param: LPARAM) -> windows::core::BOOL {
        let pick = &mut *(param.0 as *mut WindowPick);
        if !IsWindowVisible(hwnd).as_bool() { return true.into(); }
        let mut pid = 0u32;
        GetWindowThreadProcessId(hwnd, Some(&mut pid));
        let mut name = [0u16; 260];
        let Ok(process_handle) = (unsafe {
            OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid)
        }) else { return true.into(); };
        let mut len = 260u32;
        let ok = unsafe { QueryFullProcessImageNameW(process_handle, PROCESS_NAME_FORMAT(0), windows::core::PWSTR(name.as_mut_ptr()), &mut len) };
        unsafe { let _ = CloseHandle(process_handle); }
        if ok.is_err() || len == 0 { return true.into(); }
        let exe = String::from_utf16_lossy(&name[..len as usize]);
        let process = exe.rsplit(['\\', '/']).next().unwrap_or("").trim_end_matches(".exe");
        if !process.eq_ignore_ascii_case(&pick.process) { return true.into(); }
        pick.saw = true;
        let mut rect = RECT::default();
        if GetClientRect(hwnd, &mut rect).is_err() { return true.into(); }
        let area = (rect.right - rect.left) as i64 * (rect.bottom - rect.top) as i64;
        if area > pick.best_area { pick.best = hwnd; pick.best_area = area; }
        true.into()
    }

    pub fn capture(process: &str) -> Result<Value, String> {
        let mut pick = WindowPick { best: HWND::default(), best_area: 0, saw: false, process: process.to_string() };
        unsafe { EnumWindows(Some(visit), LPARAM(&mut pick as *mut _ as isize)).map_err(|e| format!("枚举游戏窗口失败：{e}"))?; }
        if !pick.saw { return failure("missing", "端游进程没有可见窗口；请先启动游戏，面板不会代为启动"); }
        if pick.best.is_invalid() { return failure("missing", "端游窗口客户区不可用"); }
        if unsafe { IsIconic(pick.best).as_bool() } {
            return failure("minimized", "端游窗口最小化中；为避免抢现场，请自行恢复后再看实时画面");
        }
        capture_hwnd(pick.best)
    }

    fn capture_hwnd(hwnd: HWND) -> Result<Value, String> {
        let mut rect = RECT::default();
        unsafe { GetClientRect(hwnd, &mut rect).map_err(|e| format!("读取游戏客户区失败：{e}"))?; }
        let width = rect.right - rect.left;
        let height = rect.bottom - rect.top;
        if width < 200 || height < 150 { return failure("missing", "游戏客户区尺寸无效，尚不能捕获画面"); }
        let pixels = (width as i64).checked_mul(height as i64).and_then(|n| n.checked_mul(4));
        if width > 7680 || height > 4320 || pixels.map(|n| n > 64 * 1024 * 1024).unwrap_or(true) {
            return failure("failed", "游戏客户区超出实时画面安全尺寸上限");
        }
        let screen = unsafe { GetDC(None) };
        if screen.is_invalid() { return failure("failed", "无法创建画面设备上下文"); }
        let dc = unsafe { CreateCompatibleDC(Some(screen)) };
        if dc.is_invalid() { unsafe { ReleaseDC(None, screen); } return failure("failed", "无法创建内存画布"); }
        let mut bmi = BITMAPINFO::default();
        bmi.bmiHeader = BITMAPINFOHEADER {
            biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
            biWidth: width, biHeight: -height, biPlanes: 1, biBitCount: 32,
            biCompression: BI_RGB.0 as u32, ..Default::default()
        };
        let mut bits: *mut std::ffi::c_void = std::ptr::null_mut();
        let bmp = unsafe { CreateDIBSection(Some(dc), &bmi, DIB_RGB_COLORS, &mut bits, None, 0) };
        let Some(bmp) = bmp.ok() else {
            unsafe {
                let _ = DeleteDC(dc);
                let _ = ReleaseDC(None, screen);
            }
            return failure("failed", "无法创建游戏画面位图");
        };
        let old = unsafe { SelectObject(dc, HGDIOBJ(bmp.0)) };
        if old.is_invalid() {
            unsafe {
                let _ = DeleteObject(HGDIOBJ(bmp.0));
                let _ = DeleteDC(dc);
                let _ = ReleaseDC(None, screen);
            }
            return failure("failed", "无法启用游戏画面位图");
        }
        let ok = unsafe { PrintWindow(hwnd, dc, PRINT_WINDOW_FLAGS(2)) }.as_bool();
        let result = if ok && !bits.is_null() {
            let len = width as usize * height as usize * 4;
            let bgra = unsafe { std::slice::from_raw_parts(bits as *const u8, len) };
            let mut rgba = Vec::with_capacity(len);
            let mut sum = 0u64;
            for px in bgra.chunks_exact(4) {
                rgba.extend_from_slice(&[px[2], px[1], px[0], 255]);
                sum += ((px[2] as u16 * 77 + px[1] as u16 * 150 + px[0] as u16 * 29) / 256) as u64;
            }
            let mean = sum as f64 / (width as u64 * height as u64) as f64;
            encode_rgba(&rgba, width as u32, height as u32).map(|p| ready(&p, width as u32, height as u32, "window", mean))
        } else {
            failure("failed", "窗口捕获失败：目标可能已关闭，或当前权限无法读取该窗口")
        };
        unsafe {
            let _ = SelectObject(dc, old);
            let _ = DeleteObject(HGDIOBJ(bmp.0));
            let _ = DeleteDC(dc);
            let _ = ReleaseDC(None, screen);
        }
        result
    }
}

#[cfg(target_os = "windows")]
fn capture_window(process: &str) -> Result<Value, String> {
    use std::sync::atomic::Ordering;
    if WINDOW_CAPTURE_ACTIVE.compare_exchange(false, true, Ordering::Acquire, Ordering::Relaxed).is_err() {
        return failure("failed", "上一帧仍在捕获；已阻止新的窗口捕获，避免卡住面板");
    }
    let (tx, rx) = mpsc::channel();
    let process = process.to_string();
    std::thread::spawn(move || {
        let result = std::panic::catch_unwind(|| win::capture(&process))
            .unwrap_or_else(|_| Err("窗口捕获线程异常".to_string()));
        // 若 PrintWindow 永不返回，这里不会执行：忙标记保持为真，后续帧快速失败而不堆线程。
        WINDOW_CAPTURE_ACTIVE.store(false, Ordering::Release);
        let _ = tx.send(result);
    });
    wait_capture(rx, Duration::from_secs(3))
}

#[cfg(any(target_os = "windows", test))]
fn wait_capture(rx: mpsc::Receiver<Result<Value, String>>, timeout: Duration) -> Result<Value, String> {
    match rx.recv_timeout(timeout) {
        Ok(result) => result,
        Err(mpsc::RecvTimeoutError::Timeout) => failure("failed", "窗口捕获等待超时；目标窗口没有及时响应，后续帧将等待上一线程退出"),
        Err(mpsc::RecvTimeoutError::Disconnected) => Err("窗口捕获通道异常关闭".to_string()),
    }
}

#[cfg(not(target_os = "windows"))]
fn capture_window(_process: &str) -> Result<Value, String> { failure("unsupported", "当前平台没有端游窗口捕获通道") }

fn capture_adb(target: &CaptureTarget) -> Result<Value, String> {
    let n = now_ms();
    let dir = std::env::temp_dir().join(format!("gdops-live-{}-{n}", std::process::id()));
    fs_cleanup(&dir);
    std::fs::create_dir_all(&dir).map_err(|_| "创建画面临时目录失败".to_string())?;
    let local = dir.join("frame.png");
    let remote = format!("/sdcard/gdops-live-{}-{n}.png", std::process::id());
    let capture = run_adb(&target.adb, &["-s", &target.serial, "shell", "screencap", "-p", &remote], 8);
    let pulled = match capture {
        Ok((0, _, _)) => run_adb(&target.adb, &["-s", &target.serial, "pull", &remote, local.to_str().unwrap_or("frame.png")], 8),
        Ok((code, _, err)) => Err(adb_error(&target.serial, &remote, format!("ADB 截图失败（exit {code}）：{}", tail(err)))),
        Err(e) => Err(adb_error(&target.serial, &remote, e)),
    };
    // 设备端临时文件无论成功失败都尝试清理；不读取、不回传序列号。
    let _ = run_adb(&target.adb, &["-s", &target.serial, "shell", "rm", "-f", &remote], 3);
    let result = match pulled {
        Ok((0, _, _)) => decode_png_file(&local),
        Ok((code, _, err)) => Err(adb_error(&target.serial, &remote, format!("ADB 取回画面失败（exit {code}）：{}", tail(err)))),
        Err(e) => Err(adb_error(&target.serial, &remote, e)),
    };
    fs_cleanup(&dir);
    match result {
        Ok(v) => Ok(v),
        Err(e) => failure("offline", e),
    }
}

fn run_adb(exe: &std::path::Path, args: &[&str], secs: u64) -> Result<(i32, Vec<String>, Vec<String>), String> {
    let mut cmd = std::process::Command::new(exe);
    cmd.args(args);
    crate::control::run_capture(&mut cmd, Duration::from_secs(secs))
}

fn fs_cleanup(dir: &std::path::Path) { let _ = std::fs::remove_dir_all(dir); }
fn adb_error(serial: &str, remote: &str, error: String) -> String {
    scrub(&error).replace(serial, "<设备>").replace(remote, "<临时截图>")
}
fn tail(lines: Vec<String>) -> String { lines.into_iter().rev().take(4).collect::<Vec<_>>().join(" / ") }

fn decode_png_file(path: &std::path::Path) -> Result<Value, String> {
    let meta = std::fs::metadata(path).map_err(|_| "ADB 没有取回可读的画面".to_string())?;
    if meta.len() > 32 * 1024 * 1024 { return Err("设备截图文件超出安全大小上限".to_string()); }
    let bytes = std::fs::read(path).map_err(|_| "ADB 没有取回可读的画面".to_string())?;
    let mut decoder = Decoder::new(Cursor::new(bytes));
    decoder.set_limits(png::Limits { bytes: 64 * 1024 * 1024 });
    decoder.set_transformations(Transformations::EXPAND | Transformations::STRIP_16);
    let mut reader = decoder.read_info().map_err(|e| format!("设备画面不是有效 PNG：{e}"))?;
    let info = reader.info();
    let width = info.width;
    let height = info.height;
    if width > 7680 || height > 4320 || (width as u64).saturating_mul(height as u64).saturating_mul(4) > 64 * 1024 * 1024 {
        return Err("设备画面尺寸超出安全上限".to_string());
    }
    let size = reader.output_buffer_size().ok_or_else(|| "设备画面尺寸超出解码上限".to_string())?;
    if size > 64 * 1024 * 1024 { return Err("设备画面缓冲超出安全上限".to_string()); }
    let mut rgba = vec![0u8; size];
    let frame = reader.next_frame(&mut rgba).map_err(|e| format!("设备画面解码失败：{e}"))?;
    rgba.truncate(frame.buffer_size());
    let mut sum = 0u64;
    for px in rgba.chunks_exact(4) {
        sum += ((px[0] as u16 * 77 + px[1] as u16 * 150 + px[2] as u16 * 29) / 256) as u64;
    }
    let mean = sum as f64 / (width as u64 * height as u64) as f64;
    let png = encode_rgba(&rgba, width, height)?;
    Ok(ready(&png, width, height, "adb", mean))
}

// 小型本地 base64 编码，避免为单帧传输再引一套依赖。
fn base64(data: &[u8]) -> String {
    const T: &[u8; 64] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut out = String::with_capacity((data.len() + 2) / 3 * 4);
    for chunk in data.chunks(3) {
        let n = ((chunk[0] as u32) << 16) | ((chunk.get(1).copied().unwrap_or(0) as u32) << 8) | chunk.get(2).copied().unwrap_or(0) as u32;
        out.push(T[((n >> 18) & 63) as usize] as char);
        out.push(T[((n >> 12) & 63) as usize] as char);
        out.push(if chunk.len() > 1 { T[((n >> 6) & 63) as usize] as char } else { '=' });
        out.push(if chunk.len() > 2 { T[(n & 63) as usize] as char } else { '=' });
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn base64_matches_known_vectors() {
        assert_eq!(base64(b""), "");
        assert_eq!(base64(b"f"), "Zg==");
        assert_eq!(base64(b"fo"), "Zm8=");
        assert_eq!(base64(b"foo"), "Zm9v");
    }
    #[test]
    fn png_round_trip_and_dark_state() {
        let png = encode_rgba(&[20, 30, 40, 255], 1, 1).unwrap();
        assert!(png.starts_with("data:image/png;base64,"));
        assert_eq!(ready(&png, 1, 1, "window", 2.0)["state"], "dark");
        assert_eq!(ready(&png, 1, 1, "window", 80.0)["state"], "ready");
    }
    #[test]
    fn failure_never_returns_image_or_path() {
        let v = failure("offline", r"读取 D:\private\adb.exe 失败").unwrap();
        assert_eq!(v["state"], "offline");
        assert!(v.get("image").is_none());
        assert!(v["error"].as_str().unwrap().contains("<本地路径>"));
    }
    #[test]
    fn capture_wait_is_bounded_and_channel_close_is_an_error() {
        let (tx, rx) = mpsc::channel();
        let late = tx.clone();
        std::thread::spawn(move || {
            std::thread::sleep(Duration::from_millis(30));
            let _ = late.send(failure("ready", "迟到的帧"));
        });
        let timed_out = wait_capture(rx, Duration::from_millis(2)).unwrap();
        assert_eq!(timed_out["state"], "failed");
        assert!(timed_out["error"].as_str().unwrap().contains("超时"));
        let (_, rx) = mpsc::channel();
        assert!(wait_capture(rx, Duration::from_millis(2)).is_err());
    }
}
