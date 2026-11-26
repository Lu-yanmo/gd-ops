// 执行现场实时画面：ARK 走端游窗口 PrintWindow；M9A 走当前实例 ADB exec-out screencap。
// 只读捕获，不恢复/激活窗口、不注入输入；序列号和绝对路径只留在 Rust 内。
use crate::assist::{capture_target, scrub, CaptureTarget};
use base64::Engine;
use jpeg_encoder::{ColorType, Encoder};
use png::{Decoder, Transformations};
use serde_json::{json, Value};
use std::io::{Cursor, Read};
use std::sync::{mpsc, Mutex, MutexGuard};
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

static CAPTURE_LOCK: Mutex<()> = Mutex::new(());
#[cfg(target_os = "windows")]
static WINDOW_CAPTURE_ACTIVE: std::sync::atomic::AtomicBool = std::sync::atomic::AtomicBool::new(false);

const PREVIEW_MAX_WIDTH: u32 = 960;
const PREVIEW_MAX_HEIGHT: u32 = 540;
const ADB_PNG_LIMIT: usize = 32 * 1024 * 1024;

pub fn frame(game: &str) -> Result<Value, String> {
    let started = Instant::now();
    let target = capture_target(game).map_err(|e| scrub(&e))?;
    let result = if !target.process.is_empty() {
        capture_window(&target.process)
    } else {
        let _guard: MutexGuard<()> = CAPTURE_LOCK.lock().map_err(|_| "画面捕获锁不可用".to_string())?;
        capture_adb(&target)
    };
    match result {
        Ok(mut value) => {
            value["captureMs"] = json!(started.elapsed().as_millis());
            Ok(value)
        }
        Err(e) => Err(scrub(&e)),
    }
}

fn now_ms() -> u128 {
    SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_millis()).unwrap_or(0)
}

fn scaled_dimensions(width: u32, height: u32) -> (u32, u32) {
    if width <= PREVIEW_MAX_WIDTH && height <= PREVIEW_MAX_HEIGHT { return (width, height); }
    let w = width as u64;
    let h = height as u64;
    let (numerator, denominator) = if w * PREVIEW_MAX_HEIGHT as u64 > h * PREVIEW_MAX_WIDTH as u64 {
        (PREVIEW_MAX_WIDTH as u64, w)
    } else {
        (PREVIEW_MAX_HEIGHT as u64, h)
    };
    let out_w = (w * numerator / denominator).max(1) as u32;
    let out_h = (h * numerator / denominator).max(1) as u32;
    (out_w.min(PREVIEW_MAX_WIDTH), out_h.min(PREVIEW_MAX_HEIGHT))
}

struct EncodedPreview {
    image: String,
    width: u32,
    height: u32,
    bytes: usize,
    mean_luma: f64,
}

// 面板只显示缩略实况：先缩到 ≤960×540，再以轻量 JPEG 传输；不改变游戏窗口本身。
// 输入保留 BGRA/RGBA 原序，省掉端游每帧一次全分辨率 RGBA 拷贝。
fn encode_preview(pixels: &[u8], width: u32, height: u32, color: ColorType) -> Result<EncodedPreview, String> {
    if width == 0 || height == 0 || width > 7680 || height > 4320 { return Err("画面尺寸无效".to_string()); }
    let expected = (width as usize).checked_mul(height as usize).and_then(|n| n.checked_mul(4));
    if expected != Some(pixels.len()) { return Err("画面像素缓冲长度不符".to_string()); }
    let (out_w, out_h) = scaled_dimensions(width, height);
    let mut scaled = Vec::with_capacity(out_w as usize * out_h as usize * 4);
    let mut sum = 0.0f64;
    for y in 0..out_h {
        let sy = ((y as u64 * 2 + 1) * height as u64 / (out_h as u64 * 2)).min(height as u64 - 1) as usize;
        for x in 0..out_w {
            let sx = ((x as u64 * 2 + 1) * width as u64 / (out_w as u64 * 2)).min(width as u64 - 1) as usize;
            let at = (sy * width as usize + sx) * 4;
            scaled.extend_from_slice(&pixels[at..at + 4]);
            let (r, g, b) = match color {
                ColorType::Bgra => (pixels[at + 2], pixels[at + 1], pixels[at]),
                _ => (pixels[at], pixels[at + 1], pixels[at + 2]),
            };
            sum += r as f64 * 0.2126 + g as f64 * 0.7152 + b as f64 * 0.0722;
        }
    }
    let mut jpeg = Vec::new();
    Encoder::new(&mut jpeg, 72)
        .encode(&scaled, out_w as u16, out_h as u16, color)
        .map_err(|e| format!("画面编码失败：{e}"))?;
    let bytes = jpeg.len();
    let image = format!("data:image/jpeg;base64,{}", base64::engine::general_purpose::STANDARD.encode(&jpeg));
    Ok(EncodedPreview { image, width: out_w, height: out_h, bytes, mean_luma: sum / (out_w as u64 * out_h as u64) as f64 })
}

fn ready(preview: EncodedPreview, source: &str) -> Value {
    let dark = preview.mean_luma < 8.0;
    json!({
        "ok": true, "state": if dark { "dark" } else { "ready" },
        "source": source, "image": preview.image, "width": preview.width, "height": preview.height,
        "capturedAt": now_ms(), "meanLuma": (preview.mean_luma * 10.0).round() / 10.0,
        "encodedBytes": preview.bytes,
        "note": (if dark { "画面偏暗；若游戏已退出，请核对目标窗口" } else { "" })
    })
}

fn failure(state: &str, error: impl AsRef<str>) -> Result<Value, String> {
    Ok(json!({ "ok": false, "state": state, "error": scrub(error.as_ref()) }))
}

#[cfg(target_os = "windows")]
mod win {
    use super::{encode_preview, failure, ready, ColorType};
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
            encode_preview(bgra, width as u32, height as u32, ColorType::Bgra)
                .map(|p| ready(p, "window"))
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
    // exec-out 直接传二进制 PNG：不再写设备临时文件，也不再 pull/rm 三次往返。
    let result = match run_adb_screenshot(&target.adb, &["-s", &target.serial, "exec-out", "screencap", "-p"], 8) {
        Ok((0, bytes, _)) => decode_png_bytes(&bytes),
        Ok((code, _, err)) => Err(adb_error(&target.serial, format!("ADB 截图失败（exit {code}）：{}", tail(&err)))),
        Err(e) => Err(adb_error(&target.serial, e)),
    };
    match result {
        Ok(v) => Ok(v),
        Err(e) => failure("offline", e),
    }
}

#[cfg(target_os = "windows")]
struct KillOnClose(windows::Win32::Foundation::HANDLE);

#[cfg(target_os = "windows")]
impl Drop for KillOnClose {
    fn drop(&mut self) {
        unsafe { let _ = windows::Win32::Foundation::CloseHandle(self.0); }
    }
}

#[cfg(target_os = "windows")]
fn spawn_adb_child(cmd: &mut std::process::Command) -> Result<(std::process::Child, KillOnClose), String> {
    use std::os::windows::io::AsRawHandle;
    use windows::Win32::Foundation::HANDLE;
    use windows::Win32::System::JobObjects::{
        AssignProcessToJobObject, CreateJobObjectW, JOBOBJECT_EXTENDED_LIMIT_INFORMATION,
        JobObjectExtendedLimitInformation, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE, SetInformationJobObject,
    };
    unsafe {
        let job = CreateJobObjectW(None, None).map_err(|e| format!("创建 ADB 进程组失败：{e}"))?;
        let guard = KillOnClose(job);
        let mut limits = JOBOBJECT_EXTENDED_LIMIT_INFORMATION::default();
        limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        SetInformationJobObject(job, JobObjectExtendedLimitInformation,
            &limits as *const _ as *const std::ffi::c_void, std::mem::size_of_val(&limits) as u32)
            .map_err(|e| format!("设置 ADB 进程组失败：{e}"))?;
        let mut child = cmd.spawn().map_err(|e| format!("起 ADB 进程失败：{e}"))?;
        if let Err(e) = AssignProcessToJobObject(job, HANDLE(child.as_raw_handle() as *mut std::ffi::c_void)) {
            let _ = child.kill();
            let _ = child.wait();
            return Err(format!("接管 ADB 进程组失败：{e}"));
        }
        Ok((child, guard))
    }
}

#[cfg(not(target_os = "windows"))]
fn spawn_adb_child(cmd: &mut std::process::Command) -> Result<(std::process::Child, ()), String> {
    cmd.spawn().map_err(|e| format!("起 ADB 进程失败：{e}"))
}

// adb exec-out 的输出可能含 NUL/任意字节，不能走按行 UTF-8 drain。
// 读取端到 32MiB+1 即止，超限时立即中止子进程；进程和管道两端都有超时。
fn run_adb_screenshot(exe: &std::path::Path, args: &[&str], secs: u64) -> Result<(i32, Vec<u8>, String), String> {
    use std::process::{Command, Stdio};
    use std::sync::atomic::{AtomicBool, Ordering};
    use std::sync::Arc;

    let mut cmd = Command::new(exe);
    cmd.args(args).stdin(Stdio::null()).stdout(Stdio::piped()).stderr(Stdio::piped());
    #[cfg(target_os = "windows")]
    {
        use std::os::windows::process::CommandExt;
        cmd.creation_flags(0x0800_0000); // CREATE_NO_WINDOW
    }
    let (mut child, _job) = spawn_adb_child(&mut cmd).map_err(|e| format!("起 ADB 进程失败：{e}"))?;
    let stdout = child.stdout.take().ok_or_else(|| "ADB 截图输出管道不可用".to_string())?;
    let stderr = child.stderr.take();
    let oversized = Arc::new(AtomicBool::new(false));
    let (tx, rx) = mpsc::channel::<Result<Vec<u8>, String>>();
    let out_flag = Arc::clone(&oversized);
    let out_thread = std::thread::spawn(move || {
        let mut stdout = stdout;
        let result: Result<Vec<u8>, String> = (|| {
            let mut bytes = Vec::new();
            let mut chunk = [0u8; 64 * 1024];
            loop {
                let n = match stdout.read(&mut chunk) {
                    Ok(n) => n,
                    Err(e) => return Err(format!("读取 ADB 截图失败：{e}")),
                };
                if n == 0 { break; }
                if bytes.len() + n > ADB_PNG_LIMIT {
                    out_flag.store(true, Ordering::Release);
                    return Err("设备截图超出安全大小上限".to_string());
                }
                bytes.extend_from_slice(&chunk[..n]);
            }
            Ok(bytes)
        })();
        let _ = tx.send(result);
    });
    let (etx, erx) = mpsc::channel();
    if let Some(stderr) = stderr {
        std::thread::spawn(move || { let _ = etx.send(bounded_stderr(stderr)); });
    }

    let started = Instant::now();
    let mut code = None;
    let mut stopped = false;
    loop {
        if oversized.load(Ordering::Acquire) { stopped = true; break; }
        match child.try_wait() {
            Ok(Some(status)) => { code = status.code(); break; }
            Ok(None) => {
                if started.elapsed() >= Duration::from_secs(secs) { stopped = true; break; }
                std::thread::sleep(Duration::from_millis(20));
            }
            Err(e) => {
                #[cfg(target_os = "windows")]
                unsafe { let _ = windows::Win32::System::JobObjects::TerminateJobObject(_job.0, 1); }
                let _ = child.kill();
                let _ = child.wait();
                return Err(format!("等待 ADB 截图失败：{e}"));
            }
        }
    }
    if stopped || code.is_none() {
        #[cfg(target_os = "windows")]
        unsafe { let _ = windows::Win32::System::JobObjects::TerminateJobObject(_job.0, 1); }
        let _ = child.kill();
        let _ = child.wait();
    }
    if stopped {
        return Err(if oversized.load(Ordering::Acquire) { "设备截图超出安全大小上限".to_string() }
            else { "ADB 截图等待超时".to_string() });
    }
    let bytes = rx.recv_timeout(Duration::from_millis(750)).map_err(|_| "ADB 截图输出读取超时".to_string())??;
    let _ = out_thread.join();
    let err = erx.recv_timeout(Duration::from_millis(500)).unwrap_or_default();
    Ok((code.unwrap_or(-1), bytes, err))
}

fn bounded_stderr(mut reader: impl Read) -> String {
    let mut raw = Vec::new();
    let mut chunk = [0u8; 2048];
    loop { // 保留前 8KiB，但仍持续排空，避免子进程被 stderr 管道反压。
        match reader.read(&mut chunk) {
            Ok(0) => break,
            Ok(n) => {
                let keep = n.min(8192usize.saturating_sub(raw.len()));
                raw.extend_from_slice(&chunk[..keep]);
            }
            Err(_) => break,
        }
    }
    String::from_utf8_lossy(&raw).into_owned()
}

fn adb_error(serial: &str, error: String) -> String { scrub(&error).replace(serial, "<设备>") }
fn tail(error: &str) -> String { error.lines().rev().take(4).collect::<Vec<_>>().join(" / ") }

fn normalize_png_rgba(bytes: Vec<u8>, color: png::ColorType) -> Result<Vec<u8>, String> {
    match color {
        png::ColorType::Rgba => Ok(bytes),
        png::ColorType::Rgb => {
            let mut rgba = Vec::with_capacity(bytes.len() / 3 * 4);
            for px in bytes.chunks_exact(3) { rgba.extend_from_slice(&[px[0], px[1], px[2], 255]); }
            Ok(rgba)
        }
        png::ColorType::Grayscale => {
            let mut rgba = Vec::with_capacity(bytes.len() * 4);
            for gray in bytes { rgba.extend_from_slice(&[gray, gray, gray, 255]); }
            Ok(rgba)
        }
        png::ColorType::GrayscaleAlpha => {
            let mut rgba = Vec::with_capacity(bytes.len() * 2);
            for px in bytes.chunks_exact(2) { rgba.extend_from_slice(&[px[0], px[0], px[0], px[1]]); }
            Ok(rgba)
        }
        png::ColorType::Indexed => Err("画面色彩格式不受支持".to_string()),
    }
}

fn decode_png_bytes(bytes: &[u8]) -> Result<Value, String> {
    if bytes.is_empty() { return Err("设备没有返回画面数据".to_string()); }
    if bytes.len() > ADB_PNG_LIMIT { return Err("设备截图超出安全大小上限".to_string()); }
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
    let rgba = normalize_png_rgba(rgba, reader.output_color_type().0)?;
    let preview = encode_preview(&rgba, width, height, ColorType::Rgba)?;
    Ok(ready(preview, "adb"))
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn base64_matches_known_vectors() {
        assert_eq!(base64::engine::general_purpose::STANDARD.encode(b""), "");
        assert_eq!(base64::engine::general_purpose::STANDARD.encode(b"f"), "Zg==");
        assert_eq!(base64::engine::general_purpose::STANDARD.encode(b"fo"), "Zm8=");
        assert_eq!(base64::engine::general_purpose::STANDARD.encode(b"foo"), "Zm9v");
    }
    #[test]
    fn preview_scaling_is_proportional_and_never_upscales() {
        assert_eq!(scaled_dimensions(1920, 1080), (960, 540));
        assert_eq!(scaled_dimensions(1080, 2400), (243, 540));
        assert_eq!(scaled_dimensions(4, 3), (4, 3));
        assert_eq!(scaled_dimensions(7680, 2160), (960, 270));
    }
    #[test]
    fn jpeg_preview_is_bounded_and_preserves_channel_order() {
        let mut pixels = vec![0u8; 1280 * 720 * 4];
        for px in pixels.chunks_exact_mut(4) { px.copy_from_slice(&[20, 40, 60, 255]); }
        let preview = encode_preview(&pixels, 1280, 720, ColorType::Rgba).unwrap();
        assert_eq!((preview.width, preview.height), (960, 540));
        assert!(preview.image.starts_with("data:image/jpeg;base64,"));
        assert!(preview.bytes < 500_000);
        let jpeg = base64::engine::general_purpose::STANDARD.decode(&preview.image[23..]).unwrap();
        assert_eq!(jpeg.len(), preview.bytes);
        assert_eq!(&jpeg[..2], &[0xff, 0xd8]);
        let expected_luma = 20.0 * 0.2126 + 40.0 * 0.7152 + 60.0 * 0.0722;
        assert!((preview.mean_luma - expected_luma).abs() < 0.1);
        assert_eq!(ready(preview, "window")["state"], "ready");
        let rgba = encode_preview(&[20, 40, 60, 255], 1, 1, ColorType::Rgba).unwrap();
        let bgra = encode_preview(&[60, 40, 20, 255], 1, 1, ColorType::Bgra).unwrap();
        assert!((bgra.mean_luma - expected_luma).abs() < 0.1, "BGRA 必须按 B,G,R,A 解释");
        assert_eq!(bgra.image, rgba.image, "等价的 RGBA/BGRA 必须编码出相同 JPEG，不能只验证亮度");
        let dark = encode_preview(&[2, 2, 2, 255], 1, 1, ColorType::Bgra).unwrap();
        assert_eq!(ready(dark, "window")["state"], "dark");
    }
    #[test]
    fn adb_png_decodes_to_jpeg_and_bad_data_has_no_image() {
        let mut png = Vec::new();
        {
            let mut encoder = png::Encoder::new(&mut png, 5, 3);
            encoder.set_color(png::ColorType::Rgba);
            encoder.set_depth(png::BitDepth::Eight);
            encoder.write_header().unwrap().write_image_data(&[80u8; 5 * 3 * 4]).unwrap();
        }
        let value = decode_png_bytes(&png).unwrap();
        assert_eq!(value["source"], "adb");
        assert_eq!((value["width"].as_u64().unwrap(), value["height"].as_u64().unwrap()), (5, 3));
        assert!(value["image"].as_str().unwrap().starts_with("data:image/jpeg;base64,"));
        assert!(value["encodedBytes"].as_u64().unwrap() > 0);
        let mut rgb_png = Vec::new();
        {
            let mut encoder = png::Encoder::new(&mut rgb_png, 2, 1);
            encoder.set_color(png::ColorType::Rgb);
            encoder.set_depth(png::BitDepth::Eight);
            encoder.write_header().unwrap().write_image_data(&[20, 40, 60, 80, 100, 120]).unwrap();
        }
        assert!(decode_png_bytes(&rgb_png).is_ok(), "无透明通道的 PNG 也必须规范化为 RGBA");
        assert!(decode_png_bytes(b"not a png").is_err());
        assert!(decode_png_bytes(&[]).is_err());
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
