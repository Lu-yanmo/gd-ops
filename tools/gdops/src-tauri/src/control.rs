// 控制通道：面板 → 内核（panel-cs.exe 的 verb 信封）＋真点火/停止/门旗标/改定时/只问不点。
//
// 纪律（与 chain.rs 同一条底线，另加三条运行规矩）：
//   * 状态真身只在核内：读数一律走 emit-panel-summary（--root 钉到检测到的仓库根），面板不自己拼。
//   * 真点火：优先 run-via-task.ps1（计划任务，脱离本进程）；没有它才退回 daily-clear.ps1（自提权弹 UAC）。
//   * 停止：只走 run-via-task.ps1 -Stop；提权起来的轮次不许 taskkill（那是另一个权限域，硬杀会把锁留成残骸）。
//   * 门旗标（确认/商店领空/撤销）：走入口 `-<flag> -DryRun -NoElevate`。核内段 4 在干跑分支之前落状态，
//     所以干跑只写门、不碰游戏；正在跑一轮时拒绝写入（免得两个进程抢 state）。
//   * 不读 settings.json 的值、不写任何配置；拼进命令行的只有校验过的旗标名/游戏 id/时刻。
use serde_json::{json, Value};
use std::fs;
use std::io::{BufRead, BufReader, Read};
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Stdio};
use std::thread;
use std::time::{Duration, Instant};

#[cfg(target_os = "windows")]
use std::os::windows::process::CommandExt;

const CREATE_NO_WINDOW: u32 = 0x0800_0000;
const MAX_LINES: usize = 400;

fn root() -> Result<PathBuf, String> {
    crate::live::find_root()
        .ok_or_else(|| "找不到仓库根目录（把 gdops.exe 放进仓库，或设 GDOPS_ROOT）".to_string())
}

fn is_hex_or_word(s: &str) -> bool {
    let b = s.as_bytes();
    !b.is_empty()
        && b.len() <= 32
        && b[0].is_ascii_alphabetic()
        && b.iter().all(|c| c.is_ascii_alphanumeric())
}

fn norm_game(g: &str, allowed: &[String]) -> Result<String, String> {
    let g = g.trim();
    if !is_hex_or_word(g) {
        return Err("游戏 id 不合法".to_string());
    }
    // 插件注册表是唯一的 id 来源：核里没有这路就当失败说清楚，别让 PS 侧抛内部错。
    if !allowed.is_empty() && !allowed.iter().any(|x| x == g) {
        return Err(format!("这个仓库没有注册这条游戏路：{g}"));
    }
    Ok(g.to_string())
}

fn norm_games(games: Option<&str>, allowed: &[String]) -> Result<Option<String>, String> {
    let Some(g) = games else { return Ok(None) };
    let g = g.trim();
    if g.is_empty() || g == "all" {
        return Ok(None);
    }
    // 面板勾选可能拼成逗号串，但内核 -Games 只收单路或 all
    // （run-via-task.ps1 的 ValidateSet 直接拒逗号），多路要当场说清楚。
    let mut list = Vec::new();
    for part in g.split(',') {
        let part = part.trim();
        if part.is_empty() {
            continue;
        }
        list.push(norm_game(part, allowed)?);
    }
    match list.len() {
        0 => Ok(None),
        1 => Ok(list.pop()),
        _ => Err(format!(
            "一次只能跑一路（收到 {}）：内核 -Games 只收单路或全部。请只勾一路，或全部勾上走整轮。",
            list.join(",")
        )),
    }
}

/* ── 进程：捕获输出 / 脱手起 ── */

fn drain<R: Read + Send + 'static>(r: R) -> thread::JoinHandle<Vec<String>> {
    thread::spawn(move || {
        let rd = BufReader::new(r);
        let mut out = Vec::new();
        for line in rd.lines() {
            let Ok(l) = line else { break };
            out.push(l.trim_end_matches('\r').to_string());
            if out.len() >= MAX_LINES {
                break;
            }
        }
        out
    })
}

/// 跑一个子进程并等它结束（有超时）；返回 (退出码, stdout 行, stderr 行)。
fn run_capture(cmd: &mut Command, dur: Duration) -> Result<(i32, Vec<String>, Vec<String>), String> {
    cmd.stdin(Stdio::null()).stdout(Stdio::piped()).stderr(Stdio::piped());
    #[cfg(target_os = "windows")]
    cmd.creation_flags(CREATE_NO_WINDOW);
    let mut child: Child = cmd.spawn().map_err(|e| format!("起进程失败：{e}"))?;
    let ho = child.stdout.take().map(drain);
    let he = child.stderr.take().map(drain);
    let t0 = Instant::now();
    let mut code: Option<i32> = None;
    loop {
        match child.try_wait() {
            Ok(Some(st)) => {
                code = Some(st.code().unwrap_or(-1));
                break;
            }
            Ok(None) => {
                if t0.elapsed() >= dur {
                    let _ = child.kill();
                    let _ = child.wait();
                    break;
                }
                thread::sleep(Duration::from_millis(50));
            }
            Err(e) => return Err(e.to_string()),
        }
    }
    let out = ho.map(|h| h.join().unwrap_or_default()).unwrap_or_default();
    let err = he.map(|h| h.join().unwrap_or_default()).unwrap_or_default();
    Ok((code.unwrap_or(-1), out, err))
}

fn spawn_detached(cmd: &mut Command) -> Result<u32, String> {
    cmd.stdin(Stdio::null()).stdout(Stdio::null()).stderr(Stdio::null());
    #[cfg(target_os = "windows")]
    cmd.creation_flags(CREATE_NO_WINDOW);
    let child = cmd.spawn().map_err(|e| format!("起进程失败：{e}"))?;
    Ok(child.id())
}

fn pwsh(root: &Path, script: &Path) -> Command {
    let mut c = Command::new("pwsh");
    c.current_dir(root)
        .arg("-NoProfile")
        .arg("-NonInteractive")
        .arg("-ExecutionPolicy")
        .arg("Bypass")
        .arg("-File")
        .arg(script);
    c
}

/* ── 内核 verb：临时 --in/--out，收信封 ── */

fn temp_pair(tag: &str) -> (PathBuf, PathBuf) {
    let n = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_nanos())
        .unwrap_or(0);
    let dir = std::env::temp_dir().join(format!("gdops-{tag}-{}-{n}", std::process::id()));
    let _ = fs::create_dir_all(&dir);
    (dir.join("in.json"), dir.join("out.json"))
}

fn panel_exe(root: &Path) -> Result<PathBuf, String> {
    let p = root.join("tools").join("panel-cs").join("panel-cs.exe");
    if p.is_file() {
        Ok(p)
    } else {
        Err(format!(
            "缺少内核 CLI：{}（先在仓库里跑 tools\\panel-cs\\build.ps1）",
            p.display()
        ))
    }
}

fn call_verb(root: &Path, verb: &str, input: &Value, extra: &[String]) -> Result<Value, String> {
    call_verb_t(root, verb, input, extra, 25)
}

/// 带超时的内核 verb 调用。25s 对只读出口够用，但拉起端游／MuMu 启动/关闭／M9A 更新
/// 这几枚会跑上几十秒到几十分钟（Gate.Host 的内层上限是 2400s），必须按用途放宽。
pub(crate) fn call_verb_t(
    root: &Path,
    verb: &str,
    input: &Value,
    extra: &[String],
    secs: u64,
) -> Result<Value, String> {
    let exe = panel_exe(root)?;
    let (inp, outp) = temp_pair(verb);
    fs::write(&inp, input.to_string()).map_err(|e| format!("写 verb 入参失败：{e}"))?;
    let _ = fs::remove_file(&outp);

    let mut cmd = Command::new(&exe);
    cmd.current_dir(root).arg(verb);
    cmd.arg(format!("--in={}", inp.display()));
    cmd.arg(format!("--out={}", outp.display()));
    for a in extra {
        cmd.arg(a);
    }
    let (code, _out, err) = run_capture(&mut cmd, Duration::from_secs(secs))?;

    let text = fs::read_to_string(&outp).ok();
    let _ = fs::remove_dir_all(inp.parent().unwrap_or(Path::new(".")));
    let Some(text) = text else {
        let tail = err
            .iter()
            .rev()
            .take(6)
            .cloned()
            .collect::<Vec<_>>()
            .into_iter()
            .rev()
            .collect::<Vec<_>>()
            .join(" / ");
        return Err(format!("内核 verb 没有回结果（exit {code}）{}{}", if tail.is_empty() { "" } else { "：" }, tail));
    };
    serde_json::from_str::<Value>(&text).map_err(|e| format!("内核结果不是 JSON：{e}"))
}

/* ── 面板公开命令 ── */

/// 状态真身：emit-panel-summary（协议 v2）。返回信封原样（ok/exit/data.Lines）。
pub fn summary() -> Result<Value, String> {
    let root = root()?;
    call_verb(
        &root,
        "emit-panel-summary",
        &json!({}),
        &[format!("--root={}", root.display())],
    )
}

/// 从 summary 里取插件 id 表（给 start_round 做白名单）。
fn game_ids(root: &Path) -> Vec<String> {
    match call_verb(
        root,
        "emit-panel-summary",
        &json!({}),
        &[format!("--root={}", root.display())],
    ) {
        Ok(v) => v
            .get("data")
            .and_then(|d| d.get("Lines"))
            .and_then(|l| l.as_array())
            .map(|rows| {
                rows.iter()
                    .filter_map(|r| r.as_str())
                    .filter_map(|l| l.strip_prefix("GAME|"))
                    .filter_map(|rest| rest.split('|').next())
                    .map(|s| s.to_string())
                    .collect()
            })
            .unwrap_or_default(),
        Err(_) => Vec::new(),
    }
}

/// 真点火：跑一轮。games=None 即全部注册路。
pub fn start_round(games: Option<String>) -> Result<Value, String> {
    let root = root()?;
    if let Some(lk) = crate::live::run_lock(&root) {
        if lk.get("alive").and_then(|v| v.as_bool()).unwrap_or(false) {
            return Err(format!(
                "已有一轮在跑（pid {}，tag {}）——等它结束或先点「停」",
                lk.get("pid").and_then(|v| v.as_i64()).unwrap_or(0),
                lk.get("tag").and_then(|v| v.as_str()).unwrap_or("?")
            ));
        }
    }
    let allowed = game_ids(&root);
    let games = norm_games(games.as_deref(), &allowed)?;

    let via_task = root.join("run-via-task.ps1").is_file();
    let script = if via_task {
        root.join("run-via-task.ps1")
    } else {
        root.join("daily-clear.ps1")
    };
    if !script.is_file() {
        return Err(format!("缺少入口脚本：{}", script.display()));
    }
    let mut cmd = pwsh(&root, &script);
    if via_task {
        cmd.arg("-NoWait");
    } else {
        cmd.arg("-Tag").arg("manual");
    }
    if let Some(g) = &games {
        cmd.arg("-Games").arg(g);
    }
    let pid = spawn_detached(&mut cmd)?;
    Ok(json!({
        "ok": true,
        "carrier": if via_task { "task" } else { "uac" },
        "pid": pid,
        "note": if via_task {
            "已交给计划任务（脱离本窗口）；进度看日志与下方现场。"
        } else {
            "已发起入口（自提权）：若弹 UAC 请在系统弹窗点「是」；进度看日志与下方现场。"
        },
    }))
}

/// 停止：只走 run-via-task.ps1 -Stop（与原生面板同一路）。
pub fn stop_round() -> Result<Value, String> {
    let root = root()?;
    let script = root.join("run-via-task.ps1");
    if !script.is_file() {
        return Err(
            "缺少 run-via-task.ps1：提权轮次不能用面板硬停（跨权限域），请在任务计划或原生面板里停"
                .to_string(),
        );
    }
    let mut cmd = pwsh(&root, &script);
    cmd.arg("-Stop");
    let (code, out, err) = run_capture(&mut cmd, Duration::from_secs(90))?;
    let mut lines = out;
    lines.extend(err.into_iter().map(|l| "[ERR] ".to_string() + &l));
    Ok(json!({ "ok": code == 0, "code": code, "lines": lines }))
}

/// 门旗标：确认主线 / 商店领空 / 撤销（-<flag> -DryRun -NoElevate，只写门不点火）。
pub fn mark_flag(flag: String) -> Result<Value, String> {
    let root = root()?;
    if !is_hex_or_word(&flag) {
        return Err("旗标名不合法".to_string());
    }
    if let Some(lk) = crate::live::run_lock(&root) {
        if lk.get("alive").and_then(|v| v.as_bool()).unwrap_or(false) {
            return Err("正在跑一轮：门旗标等本轮结束再落（免得两个进程抢 state）".to_string());
        }
    }
    let script = root.join("daily-clear.ps1");
    if !script.is_file() {
        return Err(format!("缺少入口脚本：{}", script.display()));
    }
    let mut cmd = pwsh(&root, &script);
    cmd.arg(format!("-{flag}"))
        .arg("-DryRun")
        .arg("-NoElevate")
        .arg("-Tag")
        .arg("manual");
    let (code, out, mut lines) = run_capture(&mut cmd, Duration::from_secs(180))?;
    let skip = out.len().saturating_sub(80);
    lines.extend(out.into_iter().skip(skip).filter(|l| !l.is_empty()));

    // 干跑退出码就是结论：0=门已落，2=没敲进去（入口会自己写 ERROR 行）。
    Ok(json!({ "ok": code == 0, "code": code, "lines": lines }))
}

/// 改定时：reschedule-daily.ps1（自提权→重注册计划任务→删旧时刻），脱手返回。
pub fn reschedule(times: Vec<String>) -> Result<Value, String> {
    let root = root()?;
    if times.len() < 1 || times.len() > 4 {
        return Err("时刻数量不对（1~4 个）".to_string());
    }
    let mut clean: Vec<String> = Vec::new();
    for t in &times {
        let s = t.trim();
        let bytes = s.as_bytes();
        if !(bytes.len() == 4 || bytes.len() == 5) || !s.contains(':') {
            return Err(format!("时刻格式不对：{s}（要 HH:mm）"));
        }
        let (h, m) = s.split_once(':').unwrap();
        let (Ok(h), Ok(m)) = (h.parse::<u32>(), m.parse::<u32>()) else {
            return Err(format!("时刻格式不对：{s}（要 HH:mm）"));
        };
        if h > 23 || m > 59 {
            return Err(format!("时刻超范围：{s}"));
        }
        clean.push(format!("{h:02}:{m:02}"));
    }
    let script = root.join("tools").join("reschedule-daily.ps1");
    if !script.is_file() {
        return Err(format!("缺少改定时脚本：{}", script.display()));
    }
    let mut cmd = pwsh(&root, &script);
    cmd.arg("-Times").arg(clean.join(",")).arg("-NoWait");
    let pid = spawn_detached(&mut cmd)?;
    Ok(json!({
        "ok": true,
        "pid": pid,
        "times": clean,
        "note": "已发起改定时（自提权）：UAC 弹窗点「是」后由它等本轮结束再重注册；结果看「调度」面板的 TIME 行。",
    }))
}

/// 只问不点：vision-ask.ps1 拍临时图问模型，不注入点击。
pub fn vision_ask(game: Option<String>) -> Result<Value, String> {
    let root = root()?;
    let allowed = game_ids(&root);
    // 问哪一路只认单个 id（不给就拍当前屏），逗号串在这里不当选择器用。
    let game = match game.as_deref().map(str::trim) {
        None | Some("") | Some("all") => None,
        Some(g) if g.contains(',') => return Err("一次只能问一路：别传逗号串".to_string()),
        Some(g) => Some(norm_game(g, &allowed)?),
    };
    let script = root.join("tools").join("vision-ask.ps1");
    if !script.is_file() {
        return Err(format!("缺少只问不点脚本：{}", script.display()));
    }
    let mut cmd = pwsh(&root, &script);
    if let Some(g) = &game {
        cmd.arg("-Game").arg(g);
    }
    let (code, out, err) = run_capture(&mut cmd, Duration::from_secs(180))?;
    let mut lines = out;
    lines.extend(err.into_iter().map(|l| "[ERR] ".to_string() + &l));
    Ok(json!({ "ok": code == 0, "code": code, "lines": lines }))
}
// ── 自检：只验"拒绝/守卫"这一半，不起任何真实进程、不落任何门状态。 ──
// 用法：cargo test control_guards -- --nocapture
#[cfg(test)]
mod selftest {
    use super::*;

    fn fixture_root(tag: &str) -> PathBuf {
        let d = std::env::temp_dir().join(format!("gdops-selftest-{tag}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&d);
        fs::create_dir_all(d.join("lib")).unwrap();
        fs::create_dir_all(d.join("state")).unwrap();
        fs::write(d.join("daily-clear.ps1"), "").unwrap();
        fs::write(d.join("lib").join("common.ps1"), "").unwrap();
        d
    }

    #[test]
    fn control_guards() {
        let root = fixture_root("guards");
        // 活锁：pid 用本测试进程，pid_alive 一定为真。
        fs::write(
            root.join("state").join("running.json"),
            json!({ "pid": std::process::id(), "tag": "manual", "games": "ark", "log": "" }).to_string(),
        )
        .unwrap();
        std::env::set_var("GDOPS_ROOT", &root);

        let e = start_round(None).unwrap_err();
        assert!(e.contains("已有一轮在跑"), "start_round 该被活锁挡住，实得：{e}");

        let e = mark_flag("bad-name".to_string()).unwrap_err();
        assert!(e.contains("旗标名不合法"), "mark_flag 该拒绝畸形旗标，实得：{e}");

        let e = stop_round().unwrap_err();
        assert!(e.contains("run-via-task.ps1"), "stop_round 缺脚本该说清楚，实得：{e}");

        let e = reschedule(vec![]).unwrap_err();
        assert!(e.contains("时刻数量不对"), "reschedule 空表该拒绝，实得：{e}");
        let e = reschedule(vec!["25:00".to_string()]).unwrap_err();
        assert!(e.contains("时刻超范围"), "reschedule 越界该拒绝，实得：{e}");
        let e = reschedule(vec!["0730".to_string()]).unwrap_err();
        assert!(e.contains("时刻格式不对"), "reschedule 缺冒号该拒绝，实得：{e}");

        let e = vision_ask(Some("x y".to_string())).unwrap_err();
        assert!(e.contains("游戏 id 不合法"), "vision_ask 畸形 id 该拒绝，实得：{e}");

        // 注意：这里不许删 fixture 的标记件再调 start_round —— GDOPS_ROOT 失效后会退回真实仓库，
        // 那一下就是真点火（2026-11-21 19:52 实测踩过：删掉 daily-clear.ps1 后它起了真任务，5 秒内已 -Stop）。
        let _ = fs::remove_dir_all(&root);
        std::env::remove_var("GDOPS_ROOT");
    }

    // 选路契约：单路/all 才进内核，多路当场拒绝（不碰环境变量、不起进程）。
    #[test]
    fn game_selection_contract() {
        let allowed = vec!["ark".to_string(), "r1999".to_string()];
        assert_eq!(norm_games(None, &allowed).unwrap(), None);
        assert_eq!(norm_games(Some("all"), &allowed).unwrap(), None);
        assert_eq!(norm_games(Some(" ark "), &allowed).unwrap(), Some("ark".to_string()));
        assert_eq!(norm_games(Some("ark, "), &allowed).unwrap(), Some("ark".to_string()));
        assert!(norm_games(Some("ark,r1999"), &allowed)
            .unwrap_err()
            .contains("一次只能跑一路"));
        assert!(norm_games(Some("nope"), &allowed)
            .unwrap_err()
            .contains("没有注册"));
        assert!(norm_games(Some("a b"), &allowed)
            .unwrap_err()
            .contains("不合法"));
    }

    // 真内核读数：只在显式给了 GDOPS_SELFTEST_ROOT 时跑（默认跳过，免得测试依赖本机仓库）。
    // 用法：$env:GDOPS_SELFTEST_ROOT='<仓库根>'; cargo test --lib summary_reads_kernel -- --nocapture
    #[test]
    fn summary_reads_kernel() {
        let Ok(root) = std::env::var("GDOPS_SELFTEST_ROOT") else {
            eprintln!("SKIP：没设 GDOPS_SELFTEST_ROOT（这不是失败）");
            return;
        };
        std::env::set_var("GDOPS_ROOT", &root);
        let v = summary().expect("emit-panel-summary 该跑通");
        assert_eq!(v["ok"], json!(true), "内核回执 ok 不是 true：{v}");
        let lines = v["data"]["Lines"].as_array().expect("缺 data.Lines");
        let text: Vec<&str> = lines.iter().filter_map(|x| x.as_str()).collect();
        assert!(text.iter().any(|l| *l == "PROTO|2"), "没有 PROTO|2：{text:?}");
        let games: Vec<&str> = text.iter().filter(|l| l.starts_with("GAME|")).copied().collect();
        eprintln!("GAMES={games:?}");
        assert!(!games.is_empty(), "GAME 行为空（这棵树零插件也算合法，但本测试要两路仓库）");
        std::env::remove_var("GDOPS_ROOT");
        std::env::remove_var("GDOPS_SELFTEST_ROOT");
    }
}
