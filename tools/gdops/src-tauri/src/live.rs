// 只读实况快照：从仓库现读版本/模块/进程/日志尾/history/活动门/监督心跳/截图。
// 纪律：不读 config/settings.json 的值（只判在位）、不写任何文件、不起自动化。
// 根目录反查顺序：GDOPS_ROOT → 当前 exe 上溯 → 当前工作目录上溯（认 daily-clear.ps1 + lib/common.ps1）。
use serde_json::{json, Value};
use std::fs;
use std::io::{Read, Seek, SeekFrom};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

fn now_secs() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0)
}

fn mtime_secs(p: &Path) -> Option<u64> {
    fs::metadata(p)
        .ok()?
        .modified()
        .ok()?
        .duration_since(UNIX_EPOCH)
        .ok()
        .map(|d| d.as_secs())
}

fn age_secs(p: &Path) -> Option<u64> {
    mtime_secs(p).map(|t| now_secs().saturating_sub(t))
}

fn is_root(p: &Path) -> bool {
    p.join("daily-clear.ps1").is_file() && p.join("lib").join("common.ps1").is_file()
}

pub fn find_root() -> Option<PathBuf> {
    if let Ok(v) = std::env::var("GDOPS_ROOT") {
        let p = PathBuf::from(v);
        if is_root(&p) {
            return Some(p);
        }
    }
    let mut seeds: Vec<PathBuf> = Vec::new();
    if let Ok(exe) = std::env::current_exe() {
        seeds.push(exe);
    }
    if let Ok(cwd) = std::env::current_dir() {
        seeds.push(cwd);
    }
    for seed in seeds {
        let mut cur: Option<&Path> = Some(seed.as_path());
        while let Some(dir) = cur {
            if dir.is_dir() && is_root(dir) {
                return Some(dir.to_path_buf());
            }
            cur = dir.parent();
        }
    }
    None
}

// 只读文件头：给日志开头几行（方案行）用。
fn read_head(p: &Path, max: u64) -> Vec<String> {
    let mut f = match fs::File::open(p) {
        Ok(f) => f,
        Err(_) => return vec![],
    };
    let mut buf = Vec::new();
    if f.by_ref().take(max).read_to_end(&mut buf).is_err() {
        return vec![];
    }
    String::from_utf8_lossy(&buf)
        .lines()
        .map(|l| l.trim_end_matches('\r').to_string())
        .take(60)
        .collect()
}

// 只读文件尾：从末尾 seek 回读，丢掉被截断的首行。
fn read_tail(p: &Path, max: u64) -> Vec<String> {
    let mut f = match fs::File::open(p) {
        Ok(f) => f,
        Err(_) => return vec![],
    };
    let len = f.metadata().map(|m| m.len()).unwrap_or(0);
    let start = len.saturating_sub(max);
    if start > 0 && f.seek(SeekFrom::Start(start)).is_err() {
        return vec![];
    }
    let mut buf = Vec::new();
    if f.by_ref().take(max).read_to_end(&mut buf).is_err() {
        return vec![];
    }
    let mut lines: Vec<String> = String::from_utf8_lossy(&buf)
        .lines()
        .map(|l| l.trim_end_matches('\r').to_string())
        .collect();
    if start > 0 && !lines.is_empty() {
        lines.remove(0);
    }
    if lines.len() > 160 {
        let cut = lines.len() - 160;
        lines.drain(0..cut);
    }
    lines
}

fn newest_file(dir: &Path, pred: impl Fn(&str) -> bool) -> Option<PathBuf> {
    let mut best: Option<(u64, PathBuf)> = None;
    for e in fs::read_dir(dir).ok()?.flatten() {
        let name = e.file_name().to_string_lossy().to_string();
        let p = e.path();
        if !p.is_file() || !pred(&name) {
            continue;
        }
        if let Some(t) = mtime_secs(&p) {
            if best.as_ref().map_or(true, |(bt, _)| t > *bt) {
                best = Some((t, p));
            }
        }
    }
    best.map(|(_, p)| p)
}

fn count_files(dir: &Path, pred: impl Fn(&str) -> bool) -> usize {
    fs::read_dir(dir)
        .map(|rd| {
            rd.flatten()
                .filter(|e| e.path().is_file() && pred(&e.file_name().to_string_lossy()))
                .count()
        })
        .unwrap_or(0)
}

#[cfg(target_os = "windows")]
fn list_procs() -> Vec<String> {
    use std::os::windows::process::CommandExt;
    let out = std::process::Command::new("tasklist")
        .args(["/FO", "CSV", "/NH"])
        .creation_flags(0x0800_0000) // CREATE_NO_WINDOW：别闪黑框
        .output();
    match out {
        Ok(o) => String::from_utf8_lossy(&o.stdout)
            .lines()
            .filter_map(|l| l.split('"').nth(1).map(|x| x.trim_end_matches(".exe").to_string()))
            .collect(),
        Err(_) => vec![],
    }
}

#[cfg(not(target_os = "windows"))]
fn list_procs() -> Vec<String> {
    match std::process::Command::new("ps").args(["-A", "-o", "comm="]).output() {
        Ok(o) => String::from_utf8_lossy(&o.stdout)
            .lines()
            .filter_map(|l| {
                let b = l.trim().rsplit('/').next().unwrap_or("").trim_end_matches(".exe");
                if b.is_empty() {
                    None
                } else {
                    Some(b.to_string())
                }
            })
            .collect(),
        Err(_) => vec![],
    }
}

fn running(list: &[String], names: &[&str]) -> bool {
    list.iter().any(|p| names.iter().any(|n| p.eq_ignore_ascii_case(n)))
}

// history.csv 一行：双引号字段可含逗号。
fn split_csv(line: &str) -> Vec<String> {
    let mut out = Vec::new();
    let mut cur = String::new();
    let mut q = false;
    let mut it = line.chars().peekable();
    while let Some(c) = it.next() {
        match c {
            '"' if q => {
                if it.peek() == Some(&'"') {
                    cur.push('"');
                    it.next();
                } else {
                    q = false;
                }
            }
            '"' => q = true,
            ',' if !q => out.push(std::mem::take(&mut cur)),
            _ => cur.push(c),
        }
    }
    out.push(cur);
    out
}

fn plans_from_head(head: &[String]) -> (Option<String>, Option<String>) {
    let mut ark = None;
    let mut r1999 = None;
    for l in head {
        if let Some(i) = l.find("方舟方案：") {
            ark = Some(l[i + "方舟方案：".len()..].trim().to_string());
        }
        if let Some(i) = l.find("1999 方案：") {
            r1999 = Some(l[i + "1999 方案：".len()..].trim().to_string());
        }
    }
    (ark, r1999)
}

fn read_json(p: &Path) -> Option<Value> {
    let text = fs::read_to_string(p).ok()?;
    serde_json::from_str::<Value>(&text).ok()
}

fn str_of(v: &Value, k: &str) -> Option<String> {
    v.get(k).and_then(|x| x.as_str()).map(|x| x.to_string())
}

fn list_shots(root: &Path) -> Vec<Value> {
    let mut rows: Vec<(u64, Value)> = Vec::new();
    for (kind, dir) in [("rescue", root.join("state").join("rescue")), ("vision", root.join("state").join("vision"))] {
        if let Ok(rd) = fs::read_dir(&dir) {
            for e in rd.flatten() {
                let name = e.file_name().to_string_lossy().to_string();
                if !name.to_lowercase().ends_with(".png") {
                    continue;
                }
                let p = e.path();
                if let Some(t) = mtime_secs(&p) {
                    rows.push((
                        t,
                        json!({
                            "path": p.display().to_string(),
                            "ageSecs": now_secs().saturating_sub(t),
                            "kind": kind,
                        }),
                    ));
                }
            }
        }
    }
    rows.sort_by(|a, b| b.0.cmp(&a.0));
    rows.into_iter().take(8).map(|(_, v)| v).collect()
}

pub fn collect() -> Value {
    let platform = std::env::consts::OS;
    let arch = std::env::consts::ARCH;
    let root = match find_root() {
        Some(p) => p,
        None => {
            return json!({
                "ok": false,
                "error": "find-root-failed",
                "platform": platform,
                "arch": arch,
            })
        }
    };

    let version = fs::read_to_string(root.join("VERSION"))
        .ok()
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())
        .unwrap_or_default();

    let mut modules: Vec<String> = fs::read_dir(root.join("lib"))
        .map(|rd| {
            rd.flatten()
                .filter_map(|e| {
                    let n = e.file_name().to_string_lossy().to_string();
                    n.strip_suffix("-module.ps1").map(|x| x.to_string())
                })
                .collect()
        })
        .unwrap_or_default();
    modules.sort();

    let procs_raw = list_procs();
    let want: [(&str, &[&str]); 4] = [
        ("MAA", &["MAA"]),
        ("Arknights", &["Arknights"]),
        ("m9a", &["m9a"]),
        ("MuMuPlayer", &["MuMuPlayer", "MuMuVMMHeadless"]),
    ];
    let procs: Vec<Value> = want
        .iter()
        .map(|(label, names)| json!({ "name": label, "running": running(&procs_raw, names) }))
        .collect();

    let logs_dir = root.join("logs");
    let log_path = newest_file(&logs_dir, |n| n.starts_with("run-") && n.ends_with(".log"));
    let log_count = count_files(&logs_dir, |n| n.starts_with("run-") && n.ends_with(".log"));
    let mut plans = (None, None);
    let log = log_path.map(|p| {
        let head = read_head(&p, 16 * 1024);
        plans = plans_from_head(&head);
        let tail = read_tail(&p, 64 * 1024);
        let name = p.file_name().map(|x| x.to_string_lossy().to_string()).unwrap_or_default();
        json!({
            "name": name,
            "path": p.display().to_string(),
            "ageSecs": age_secs(&p).unwrap_or(0),
            "size": fs::metadata(&p).map(|m| m.len()).unwrap_or(0),
            "files": log_count,
            "head": head,
            "tail": tail,
        })
    });

    let history: Vec<Value> = {
        let hp = root.join("state").join("history.csv");
        let lines = read_tail(&hp, 256 * 1024);
        let start = lines.len().saturating_sub(80);
        lines[start..]
            .iter()
            .filter_map(|l| {
                let f = split_csv(l);
                if f.len() < 6 {
                    return None;
                }
                Some(json!({
                    "time": f[0], "game": f[1], "plan": f[2],
                    "status": f[3], "dur": f[4], "detail": f[5],
                }))
            })
            .collect()
    };

    let last_notice = read_json(&root.join("state").join("last-notice.json")).map(|v| {
        json!({
            "time": str_of(&v, "time").unwrap_or_default(),
            "title": str_of(&v, "title").unwrap_or_default(),
            "text": str_of(&v, "text").unwrap_or_default(),
            "level": str_of(&v, "level").unwrap_or_default(),
        })
    });

    let ark_gate = read_json(&root.join("state").join("ark-event-probe.json")).map(|v| {
        json!({
            "date": str_of(&v, "Date").unwrap_or_default(),
            "stage": str_of(&v, "Stage").unwrap_or_default(),
            "entered": v.get("Entered").and_then(|x| x.as_bool()).unwrap_or(false),
            "evidence": str_of(&v, "Evidence").unwrap_or_default(),
        })
    });

    let supervision = newest_file(&logs_dir, |n| n.starts_with("supervision-") && n.ends_with(".jsonl"))
        .map(|p| {
            let beats = read_tail(&p, 512 * 1024).len();
            json!({
                "file": p.file_name().map(|x| x.to_string_lossy().to_string()).unwrap_or_default(),
                "ageSecs": age_secs(&p).unwrap_or(0),
                "beats": beats,
            })
        });

    let vision = {
        let p = root.join("state").join("vision").join("calls.jsonl");
        if p.is_file() {
            let calls = fs::read_to_string(&p)
                .map(|t| t.lines().filter(|l| !l.trim().is_empty()).count())
                .unwrap_or(0);
            Some(json!({ "calls": calls, "ageSecs": age_secs(&p).unwrap_or(0) }))
        } else {
            None
        }
    };

    let audit = newest_file(&root.join("state").join("1999-progress-audit"), |n| n.ends_with(".json") || n.ends_with(".png"))
        .map(|p| {
            json!({
                "file": p.file_name().map(|x| x.to_string_lossy().to_string()).unwrap_or_default(),
                "ageSecs": age_secs(&p).unwrap_or(0),
            })
        });

    json!({
        "ok": true,
        "root": root.display().to_string(),
        "version": version,
        "platform": platform,
        "arch": arch,
        "settingsPresent": root.join("config").join("settings.json").is_file(),
        "modules": modules,
        "procs": procs,
        "log": log,
        "history": history,
        "lastNotice": last_notice,
        "arkGate": ark_gate,
        "plans": { "ark": plans.0, "r1999": plans.1 },
        "shots": list_shots(&root),
        "supervision": supervision,
        "vision": vision,
        "audit": audit,
    })
}

pub fn open_log_dir() -> Result<String, String> {
    let root = find_root().ok_or_else(|| "找不到仓库根目录（把 gdops.exe 放进仓库，或设 GDOPS_ROOT）".to_string())?;
    let dir = root.join("logs");
    if !dir.is_dir() {
        return Err(format!("日志目录不存在：{}", dir.display()));
    }
    #[cfg(target_os = "windows")]
    {
        use std::os::windows::process::CommandExt;
        std::process::Command::new("explorer")
            .arg(&dir)
            .creation_flags(0x0800_0000)
            .spawn()
            .map_err(|e| e.to_string())?;
    }
    #[cfg(target_os = "macos")]
    {
        std::process::Command::new("open").arg(&dir).spawn().map_err(|e| e.to_string())?;
    }
    #[cfg(all(unix, not(target_os = "macos")))]
    {
        std::process::Command::new("xdg-open").arg(&dir).spawn().map_err(|e| e.to_string())?;
    }
    Ok(dir.display().to_string())
}
