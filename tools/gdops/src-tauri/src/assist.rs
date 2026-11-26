// 「助手」通道：MAA / M9A 的现状读数、日常计划改写、以及几枚真操作。
//
// 三条底线（与 control.rs 同源）：
//   * 判定不在这里：MAA 队列怎么读、M9A 实例怎么扫、MuMu 怎么探活、实例怎么切、M9A 怎么更新，
//     全部问核内已有的 verb（maa-queue-lines／m9a-instance-lines／mumu-state／apply-m9a-instance／
//     run-m9a-update／read-m9a-connection／read-m9a-install）。本件只做"拼入参、拆回执、脱敏"。
//   * 写配置只写白名单里的键：settings.json 按**行内替换**改（只动值那几个字节），改前备份到
//     backups/settings.json.<时间戳>.bak、每文件留 20 份；写盘前先把新文本解析回来，跟"原 JSON 只换
//     这几个键"逐值比一遍，对不上就拒写。缺键不补行——补行是 setup.ps1 的事。
//   * 回给界面的文本一律过 `scrub`：把本机绝对路径换成 `<本地路径>`。Key／token 这类键根本不出这个文件。
use serde_json::{json, Value};
use std::collections::HashSet;
use std::fs;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::sync::{Mutex, MutexGuard};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

// 所有 IPC 保存都经 spawn_blocking；锁必须包住「读旧值 → 备份 → 原位写 → 回读」整段，
// 否则助手页与右栏同时保存时，后写会覆盖先写，而且两边仍可能各自回读成功。
static SETTINGS_WRITE_LOCK: Mutex<()> = Mutex::new(());
static MAA_CONFIG_WRITE_LOCK: Mutex<()> = Mutex::new(());

fn write_lock(lock: &'static Mutex<()>) -> Result<MutexGuard<'static, ()>, String> {
    lock.lock().map_err(|_| "配置写锁中毒：重启面板后再试".to_string())
}

fn root() -> Result<PathBuf, String> {
    crate::live::find_root()
        .ok_or_else(|| "找不到仓库根目录（把 gdops.exe 放进仓库，或设 GDOPS_ROOT）".to_string())
}

fn settings_path(root: &Path) -> PathBuf {
    root.join("config").join("settings.json")
}

fn read_settings(root: &Path) -> Result<Option<Value>, String> {
    let p = settings_path(root);
    if !p.is_file() {
        return Ok(None);
    }
    let raw = fs::read(&p).map_err(|e| format!("读 settings.json 失败：{e}"))?;
    let body = if raw.starts_with(&[0xEF, 0xBB, 0xBF]) { &raw[3..] } else { &raw[..] };
    let text = std::str::from_utf8(body).map_err(|_| "settings.json 不是 UTF-8".to_string())?;
    let v: Value = serde_json::from_str(text).map_err(|e| format!("settings.json 解析失败：{e}"))?;
    Ok(Some(v))
}

/* ── JSON 取值：键名大小写不敏感（与 Gd.Ci 同口径），缺键回缺省 ── */

fn ci<'a>(v: &'a Value, key: &str) -> Option<&'a Value> {
    let o = v.as_object()?;
    if let Some(x) = o.get(key) {
        return Some(x);
    }
    o.iter().find(|(k, _)| k.eq_ignore_ascii_case(key)).map(|(_, x)| x)
}

fn s_ci(v: &Value, key: &str) -> String {
    match ci(v, key) {
        Some(Value::String(s)) => s.clone(),
        Some(Value::Bool(b)) => b.to_string(),
        Some(Value::Number(n)) => n.to_string(),
        _ => String::new(),
    }
}

fn b_ci(v: &Value, key: &str) -> bool {
    ci(v, key).and_then(|x| x.as_bool()).unwrap_or(false)
}

fn i_ci(v: &Value, key: &str) -> i64 {
    ci(v, key).and_then(|x| x.as_i64()).unwrap_or(0)
}

fn farm_count_ci(v: &Value, key: &str) -> Value {
    match ci(v, key) {
        Some(x) if x.as_i64().is_some() => x.clone(),
        _ => Value::Null,
    }
}

fn strs(v: Option<&Value>) -> Vec<String> {
    v.and_then(|x| x.as_array())
        .map(|a| a.iter().filter_map(|x| x.as_str().map(|s| s.to_string())).collect())
        .unwrap_or_default()
}

/// 逐层取对象里的键（大小写不敏感）；任一层缺键就回 None。
fn dag<'a>(v: &'a Value, keys: &[&str]) -> Option<&'a Value> {
    let mut cur = v;
    for k in keys {
        cur = ci(cur, k)?;
    }
    Some(cur)
}

fn listed(list: &[String], t: &str) -> bool {
    list.iter().any(|x| x.eq_ignore_ascii_case(t))
}

// ==================== MAA 任务队列：结构化行 + 勾选落 settings ====================
//
// 口径照核内 Processes.PlanMaaQueue 的四档：禁用名单 → 关；Fight → 决策定；启用名单 → 开；
// 都不在名单 → 维持 MAA 配置里现有的 IsEnable。这里只做**展示聚合**，不改判定：
// 勾选写的是 settings.ark 的两枚名单，下一轮由内核重建 gui.new.json，绝不直接写 MAA 配置。

fn task_rows_from(text: &str, keep: &[String], off: &[String]) -> Result<Vec<Value>, String> {
    let v: Value = serde_json::from_str(text).map_err(|e| format!("MAA 配置解析失败：{e}"))?;
    let arr = match dag(&v, &["Configurations", "Default", "TaskQueue"]).and_then(|x| x.as_array()) {
        Some(a) => a,
        None => return Ok(Vec::new()),
    };
    let mut rows = Vec::new();
    for (i, item) in arr.iter().enumerate() {
        let ty = s_ci(item, "TaskType");
        if ty.is_empty() {
            continue;
        }
        let name = s_ci(item, "Name");
        let cur = ci(item, "IsEnable").and_then(|x| x.as_bool()).unwrap_or(false);
        let fight = ty.eq_ignore_ascii_case("Fight");
        let off_listed = listed(off, &ty);
        // 勾选框反映的是「这一类型下一轮会不会被整类关掉」：Fight 的具体开/关由决策定，
        // 同一类型可能有多条（理智作战/剩余理智），逐条 IsEnable 不归面板管，原样放 cur 里。
        let on = if off_listed {
            false
        } else if fight || listed(keep, &ty) {
            true
        } else {
            cur
        };
        rows.push(json!({
            "idx": i,
            "type": ty,
            "name": if name.is_empty() { s_ci(item, "TaskType") } else { name },
            "on": on,
            "cur": cur,
            "control": if fight { "decision" } else { "settings" },
            "plan": strs(ci(item, "StagePlan")),
            // 全量原样：右侧二级框按它渲染该任务的每一项；保存时只把改动过的键送回来。
            "cfg": item.clone()
        }));
    }
    Ok(rows)
}

fn maa_task_rows(cfg: &Path, ark: &Value) -> (Vec<Value>, Option<String>) {
    let keep = strs(ci(ark, "keepQueuesEnabled"));
    let off = strs(ci(ark, "disableQueues"));
    let raw = match fs::read(cfg) {
        Ok(b) => b,
        Err(_) => return (Vec::new(), Some("MAA 配置读不出（settings.paths.maaDir + maaConfig）".to_string())),
    };
    let body = if raw.starts_with(&[0xEF, 0xBB, 0xBF]) { &raw[3..] } else { &raw[..] };
    let text = match std::str::from_utf8(body) {
        Ok(t) => t,
        Err(_) => return (Vec::new(), Some("MAA 配置不是 UTF-8".to_string())),
    };
    match task_rows_from(text, &keep, &off) {
        Ok(r) => (r, None),
        Err(e) => (Vec::new(), Some(e)),
    }
}

fn valid_kind(kind: &str) -> bool {
    !kind.is_empty()
        && kind.len() <= 32
        && kind.chars().all(|c| c.is_ascii_alphanumeric() || c == '_' || c == '-')
}

/// 勾选换算：开 → 进启用名单、从禁用名单摘掉；关 → 进禁用名单、从启用名单摘掉。
/// 名单里的同名项大小写不敏感比对，重复不加两次。
fn toggle_lists(
    keep: Vec<String>,
    off: Vec<String>,
    kind: &str,
    enable: bool,
) -> (Vec<String>, Vec<String>) {
    let mut keep = keep;
    let mut off = off;
    let same = |s: &String| s.eq_ignore_ascii_case(kind);
    if enable {
        off.retain(|s| !same(s));
        if !keep.iter().any(same) {
            keep.push(kind.to_string());
        }
    } else {
        keep.retain(|s| !same(s));
        if !off.iter().any(same) {
            off.push(kind.to_string());
        }
    }
    (keep, off)
}

fn maa_cfg_path(_root: &Path, settings: &Value) -> PathBuf {
    let p = ci(settings, "paths").cloned().unwrap_or(json!({}));
    join_rel(&s_ci(&p, "maaDir"), &s_ci(&p, "maaConfig"))
}

fn queue_types(cfg: &Path) -> Result<Vec<String>, String> {
    let raw = fs::read(cfg).map_err(|_| "MAA 配置读不出，先确认 paths.maaDir + maaConfig".to_string())?;
    let body = if raw.starts_with(&[0xEF, 0xBB, 0xBF]) { &raw[3..] } else { &raw[..] };
    let text = std::str::from_utf8(body).map_err(|_| "MAA 配置不是 UTF-8".to_string())?;
    let v: Value = serde_json::from_str(text).map_err(|e| format!("MAA 配置解析失败：{e}"))?;
    let mut out: Vec<String> = Vec::new();
    if let Some(arr) = dag(&v, &["Configurations", "Default", "TaskQueue"]).and_then(|x| x.as_array()) {
        for item in arr {
            let ty = s_ci(item, "TaskType");
            if ty.is_empty() || out.iter().any(|x| x.eq_ignore_ascii_case(&ty)) {
                continue;
            }
            out.push(ty);
        }
    }
    Ok(out)
}

/// 任务勾选：只写 settings.ark 的两枚名单，返回核内回执原样（备份/校验在 save_plan 里）。
pub fn maa_set_task(kind: String, enable: bool) -> Result<Value, String> {
    let _write_guard = write_lock(&SETTINGS_WRITE_LOCK)?;
    if !valid_kind(&kind) {
        return Err("任务类型名不合法（只收字母数字/下划线/连字符）".to_string());
    }
    let root = root()?;
    let s = read_settings(&root)?.ok_or_else(|| "config/settings.json 不在：先跑 setup.ps1".to_string())?;
    let ark = ci(&s, "ark").cloned().unwrap_or(json!({}));
    let keep = strs(ci(&ark, "keepQueuesEnabled"));
    let off = strs(ci(&ark, "disableQueues"));
    // Fight 的"开"不是名单能定的：只从禁用名单里摘出来，交给决策（理智计划/alsoEnableMainFight）。
    let (keep, off) = if kind.eq_ignore_ascii_case("Fight") && enable {
        let off: Vec<String> = off.into_iter().filter(|x| !x.eq_ignore_ascii_case(&kind)).collect();
        (keep, off)
    } else {
        toggle_lists(keep, off, &kind, enable)
    };
    let mut r = save_plan_locked(&root, vec![
        json!({"key": "ark.keepQueuesEnabled", "value": keep}),
        json!({"key": "ark.disableQueues", "value": off}),
    ])?;
    if kind.eq_ignore_ascii_case("Fight") && enable {
        r["note"] = json!("Fight 的开关由决策定：已从禁用名单移除，具体开哪条看理智计划与 alsoEnableMainFight");
    }
    r["kind"] = json!(kind);
    r["enable"] = json!(enable);
    Ok(r)
}

/// 全选 / 清空：按 MAA 队列里真实出现的任务类型落名单（Fight 只进出禁用名单）。
pub fn maa_set_all(enable: bool) -> Result<Value, String> {
    let _write_guard = write_lock(&SETTINGS_WRITE_LOCK)?;
    let root = root()?;
    let s = read_settings(&root)?.ok_or_else(|| "config/settings.json 不在：先跑 setup.ps1".to_string())?;
    let cfg = maa_cfg_path(&root, &s);
    let types = queue_types(&cfg)?;
    if types.is_empty() {
        return Err("MAA 队列里没有任务（配置件为空或结构不认识）".to_string());
    }
    let ark = ci(&s, "ark").cloned().unwrap_or(json!({}));
    let mut keep = strs(ci(&ark, "keepQueuesEnabled"));
    let mut off = strs(ci(&ark, "disableQueues"));
    let mut n = 0;
    for ty in &types {
        let fight = ty.eq_ignore_ascii_case("Fight");
        let same = |x: &String| x.eq_ignore_ascii_case(ty);
        if enable {
            off.retain(|x| !same(x));
            if !fight && !keep.iter().any(same) {
                keep.push(ty.clone());
            }
        } else {
            keep.retain(|x| !same(x));
            if !off.iter().any(same) {
                off.push(ty.clone());
            }
        }
        n += 1;
    }
    let r = save_plan_locked(&root, vec![
        json!({"key": "ark.keepQueuesEnabled", "value": keep}),
        json!({"key": "ark.disableQueues", "value": off}),
    ])?;
    let mut out = r;
    out["count"] = json!(n);
    out["enable"] = json!(enable);
    if !enable {
        out["note"] = json!("全部任务已进禁用名单（下一轮生效）；Fight 同样被禁用，要开回请单独勾");
    }
    Ok(out)
}

// ==================== MAA 任务参数：右侧二级框的保存通道 ====================
//
// MAA 的 gui.new.json 不归我们重建（MAA 退出时会按内存回写整份），所以只在该任务对象里
// **逐键替换值的那几个字节**：键序、缩进、CRLF、BOM 全原样；改前备份，写后整份回读对平。
// 身份键不归面板：Name 是内核按名匹配的依据，TaskType/$type 是类型，IsEnable 归队列勾选。

fn queue_array_start(text: &str) -> Option<usize> {
    let top = root_object_start(text)?;
    let (cs, _) = member_span(text, top, "Configurations")?;
    let (ds, _) = member_span(text, cs, "Default")?;
    let (qs, _) = member_span(text, ds, "TaskQueue")?;
    if text.as_bytes().get(qs) == Some(&b'[') {
        Some(qs)
    } else {
        None
    }
}

/// 队列里第 want 个元素（对象）的 '{' 位置；越界回 None。
fn task_obj_start(text: &str, want: usize) -> Option<usize> {
    let b = text.as_bytes();
    let arr = queue_array_start(text)?;
    let end = skip_container(b, arr);
    let mut i = arr + 1;
    let mut n = 0;
    loop {
        i = skip_ws(b, i);
        while i < end && b[i] == b',' {
            i = skip_ws(b, i + 1);
        }
        if i >= end || b[i] == b']' {
            return None;
        }
        if n == want {
            return if b[i] == b'{' { Some(i) } else { None };
        }
        i = skip_value(b, i);
        n += 1;
    }
}

fn jkind(v: &Value) -> &'static str {
    match v {
        Value::Null => "null",
        Value::Bool(_) => "开关",
        Value::Number(_) => "数字",
        Value::String(_) => "文本",
        Value::Array(_) => "列表",
        Value::Object(_) => "对象",
    }
}

/// MAA 6.23 的开局干员是 {Name, UseSupport} 对象数组，最多三项；不接收未知键。
fn check_starting_opers(items: &[Value]) -> Result<(), String> {
    if items.len() > 3 {
        return Err("StartingOpers：最多三位开局干员".to_string());
    }
    let mut seen = std::collections::HashSet::new();
    for it in items {
        let o = it.as_object().ok_or_else(|| "StartingOpers：每一项得是对象".to_string())?;
        let name = o.get("Name").and_then(|x| x.as_str())
            .ok_or_else(|| "StartingOpers：每一项得带文本 Name".to_string())?;
        if name.is_empty() {
            return Err("StartingOpers：Name 不能为空；空位请直接移除该项".to_string());
        }
        if name.contains('\0') {
            return Err("StartingOpers：Name 里有 NUL".to_string());
        }
        if !seen.insert(name) {
            return Err("StartingOpers：同一位干员不能重复".to_string());
        }
        if o.get("UseSupport").is_some_and(|x| !x.is_boolean()) {
            return Err("StartingOpers：UseSupport 只收布尔值".to_string());
        }
        for k in o.keys() {
            if k != "Name" && k != "UseSupport" {
                return Err(format!("StartingOpers：对象多出键 {k}"));
            }
        }
    }
    Ok(())
}

fn check_blackflow_string(key: &str, value: &str) -> Result<(), String> {
    let rewards = [
        "None", "SerpentRewards", "UnnumberedItem", "SurveyAdvance", "SpaceLease",
        "RegressionCompensation", "ForestRide", "NestParasitism", "Pegasus", "Hippogriff",
        "Dragon", "Hydra", "BaiZe", "GoldenCrow",
    ];
    let result = if key.eq_ignore_ascii_case("BlackFlowEnding") {
        (value == "Ending1").then_some(()).ok_or_else(|| "BlackFlowEnding：当前界面只开放 Ending1".to_string())
    } else if key.eq_ignore_ascii_case("BlackFlowStartAnimal1") || key.eq_ignore_ascii_case("BlackFlowStartAnimal2") {
        let animals = ["None", "Cat", "Dog", "Cerberus", "FeatheredSerpent"];
        animals.contains(&value).then_some(()).ok_or_else(|| format!("{key}：动物值不在官方候选里"))
    } else if key.eq_ignore_ascii_case("BlackFlowStartRewards") {
        let values: Vec<&str> = value.split(',').map(str::trim).filter(|x| !x.is_empty()).collect();
        let valid = if values.is_empty() || values == ["None"] {
            true
        } else {
            !values.contains(&"None") && values.iter().all(|x| rewards.contains(x))
        };
        valid.then_some(()).ok_or_else(|| format!("{key}：奖励 Flags 含未知值或把 None 与其他项混用"))
    } else {
        return Ok(());
    };
    result
}

fn check_blackflow_priority(items: &[Value]) -> Result<(), String> {
    let allowed = [
        "Pegasus", "SpaceLease", "ForestRide", "UnnumberedItem", "RegressionCompensation",
        "SurveyAdvance", "NestParasitism", "Hippogriff", "Dragon", "Hydra", "BaiZe", "GoldenCrow",
    ];
    if items.len() != allowed.len() {
        return Err("BlackFlowStartRewardPriority：必须十二项".to_string());
    }
    let mut seen = std::collections::HashSet::new();
    for item in items {
        let value = item.as_str().ok_or_else(|| "BlackFlowStartRewardPriority：每项必须是文本".to_string())?;
        if !allowed.contains(&value) || !seen.insert(value) {
            return Err("BlackFlowStartRewardPriority：十二项必须各一次，且不能含未知值".to_string());
        }
    }
    Ok(())
}

fn check_room_list(old: &[Value], new: &[Value]) -> Result<(), String> {
    if old.len() != new.len() {
        return Err("RoomList：面板只改房间开关，不增删或重排房间".to_string());
    }
    for (i, (before, after)) in old.iter().zip(new.iter()).enumerate() {
        let a = before.as_object().ok_or_else(|| format!("RoomList：第 {i} 间原值不是对象"))?;
        let b = after.as_object().ok_or_else(|| format!("RoomList：第 {i} 间得是对象"))?;
        let room = a.get("Room").and_then(|x| x.as_str()).ok_or_else(|| format!("RoomList：第 {i} 间原值缺 Room"))?;
        if room.is_empty() || b.get("Room").and_then(|x| x.as_str()) != Some(room) {
            return Err(format!("RoomList：第 {i} 间身份或顺序变了"));
        }
        if b.get("IsEnabled").is_some_and(|x| !x.is_boolean()) {
            return Err(format!("RoomList：第 {i} 间 IsEnabled 只收开关"));
        }
        // 高级/未来字段不是面板的编辑面，但必须逐值保留；既不覆盖也不借保存开关删掉。
        for (k, v) in b {
            if !k.eq_ignore_ascii_case("Room") && !k.eq_ignore_ascii_case("IsEnabled") && a.get(k) != Some(v) {
                return Err(format!("RoomList：第 {i} 间未知字段 {k} 变了或不存在"));
            }
        }
        for k in a.keys() {
            if !k.eq_ignore_ascii_case("Room") && !k.eq_ignore_ascii_case("IsEnabled") && !b.contains_key(k) {
                return Err(format!("RoomList：第 {i} 间未知字段 {k} 被删了"));
            }
        }
    }
    Ok(())
}

/// 值收口：新值必须跟原值同一种 JSON 类型；RoomList / StartingOpers 是受控对象数组例外。
fn check_new_value(key: &str, old: &Value, new: &Value) -> Result<(), String> {
    match (old, new) {
        (Value::Bool(_), Value::Bool(_)) => Ok(()),
        (Value::Number(_), Value::Number(n)) => {
            if n.as_i64().is_some() || n.as_u64().is_some() || n.as_f64().map(|f| f.is_finite()).unwrap_or(false) {
                Ok(())
            } else {
                Err(format!("{key}：数字不合法"))
            }
        }
        (Value::String(_), Value::String(s)) => {
            if s.contains('\0') {
                Err(format!("{key}：文本里有 NUL"))
            } else {
                check_blackflow_string(key, s)
            }
        }
        (Value::Array(a), Value::Array(b)) => {
            if b.iter().any(|x| x.is_null()) {
                return Err(format!("{key}：列表里不能有 null"));
            }
            if key.eq_ignore_ascii_case("StartingOpers") {
                return check_starting_opers(b);
            }
            if key.eq_ignore_ascii_case("BlackFlowStartRewardPriority") {
                return check_blackflow_priority(b);
            }
            if key.eq_ignore_ascii_case("RoomList") {
                return check_room_list(a, b);
            }
            let sample = a.first();
            for it in b {
                if it.is_string() {
                    if it.as_str().unwrap_or("").contains('\0') {
                        return Err(format!("{key}：文本里有 NUL"));
                    }
                    continue;
                }
                if it.is_object() {
                    return Err(format!("{key}：对象数组只在 RoomList 里收"));
                }
                if let Some(s) = sample {
                    if std::mem::discriminant(s) != std::mem::discriminant(it) {
                        return Err(format!("{key}：列表元素类型跟原来不一致"));
                    }
                }
            }
            Ok(())
        }
        (Value::Object(_), Value::Object(_)) => {
            Err(format!("{key}：嵌套对象不在这里改（只 RoomList 例外）"))
        }
        _ => Err(format!("{key}：原来是{}，给的是{}", jkind(old), jkind(new))),
    }
}

const TASK_IDENTITY: [&str; 4] = ["$type", "TaskType", "Name", "IsEnable"];

/// 纯函数：在文本里把第 idx 条任务的这些键原位换值，回（新文本，真变了的键）。
fn apply_task_patch(
    text: &str,
    idx: usize,
    task_type: &str,
    name: &str,
    patch: &serde_json::Map<String, Value>,
) -> Result<(String, Vec<String>), String> {
    let before: Value = serde_json::from_str(text).map_err(|e| format!("MAA 配置解析失败：{e}"))?;
    let arr = dag(&before, &["Configurations", "Default", "TaskQueue"])
        .and_then(|x| x.as_array())
        .ok_or_else(|| "MAA 配置里找不到 Configurations.Default.TaskQueue".to_string())?;
    let item = arr.get(idx).ok_or_else(|| format!("队列里没有第 {idx} 条（刷新再试）"))?;
    let ty = s_ci(item, "TaskType");
    if !ty.eq_ignore_ascii_case(task_type) {
        return Err(format!("第 {idx} 条的类型是 {ty}，不是 {task_type}（队列刚变过？刷新再改）"));
    }
    let nm = s_ci(item, "Name");
    if !name.is_empty() && nm != name {
        return Err(format!("第 {idx} 条的名字是「{nm}」，不是「{name}」（队列刚变过？刷新再改）"));
    }
    let obj = task_obj_start(text, idx).ok_or_else(|| format!("定位不到第 {idx} 条的那一段"))?;
    let mut plans: Vec<(usize, usize, String)> = Vec::new();
    let mut keys: Vec<String> = Vec::new();
    for (k, v) in patch {
        if TASK_IDENTITY.iter().any(|x| x.eq_ignore_ascii_case(k)) {
            return Err(format!("{k} 不归面板改（Name/TaskType/$type/IsEnable 由内核与队列勾选管）"));
        }
        let old = ci(item, k).ok_or_else(|| format!("MAA 这一条里没有键 {k}（面板不新增键）"))?;
        check_new_value(k, old, v)?;
        if old == v {
            continue; // 没变的键一个字节都不动
        }
        let (vs, ve) = member_span(text, obj, k).ok_or_else(|| format!("定位不到 {k} 的值"))?;
        let rep = format_value(v, &text[vs..ve], &line_indent(text, vs));
        plans.push((vs, ve, rep));
        keys.push(k.clone());
    }
    if plans.is_empty() {
        return Ok((text.to_string(), Vec::new()));
    }
    let mut out = text.to_string();
    plans.sort_by(|a, b| b.0.cmp(&a.0));
    for (vs, ve, rep) in &plans {
        out.replace_range(*vs..*ve, rep);
    }
    let got: Value = serde_json::from_str(&out)
        .map_err(|e| format!("改后的 MAA 配置解析失败（{e}）——面板不敢写，文件一个字节没动"))?;
    let mut want = before.clone();
    for (k, v) in patch {
        if let Some(slot) = want
            .get_mut("Configurations")
            .and_then(|x| x.get_mut("Default"))
            .and_then(|x| x.get_mut("TaskQueue"))
            .and_then(|x| x.get_mut(idx))
            .and_then(|x| x.get_mut(k))
        {
            *slot = v.clone();
        }
    }
    if got != want {
        return Err("改后的文本回读跟预期对不上——面板不敢写，文件一个字节没动".to_string());
    }
    Ok((out, keys))
}

/// 保存一条 MAA 任务的若干键。MAA 在跑时拒写（它退出时会按内存回写，改了也白改）。
pub fn maa_save_task(idx: usize, task_type: String, name: String, patch: Value) -> Result<Value, String> {
    let _write_guard = write_lock(&MAA_CONFIG_WRITE_LOCK)?;
    if !valid_kind(&task_type) {
        return Err("任务类型名不合法（只收字母数字/下划线/连字符）".to_string());
    }
    let patch = match patch {
        Value::Object(m) => m,
        _ => return Err("改动得是对象（键 → 值）".to_string()),
    };
    if patch.is_empty() {
        return Ok(json!({"ok": true, "changed": [], "note": "没有改动"}));
    }
    if patch.len() > 64 {
        return Err("一次改的项太多（超过 64 个）".to_string());
    }
    let root = root()?;
    let s = read_settings(&root)?.ok_or_else(|| "config/settings.json 不在：先跑 setup.ps1".to_string())?;
    let p = ci(&s, "paths").cloned().unwrap_or(json!({}));
    let exe_name = stem(&s_ci(&p, "maaExe"));
    if !exe_name.is_empty() && crate::live::any_running(&[exe_name.as_str()]) {
        return Err("MAA 正在运行：先在助手页关掉 MAA；否则它退出时会按内存里的旧配置写回".to_string());
    }
    let cfg = maa_cfg_path(&root, &s);
    if !cfg.is_file() {
        return Err("MAA 配置不在（settings.paths.maaDir + maaConfig 指向的那份）".to_string());
    }
    let raw = fs::read(&cfg).map_err(|e| format!("读 MAA 配置失败：{e}"))?;
    let had_bom = raw.starts_with(&[0xEF, 0xBB, 0xBF]);
    let body = if had_bom { &raw[3..] } else { &raw[..] };
    let text = std::str::from_utf8(body)
        .map_err(|_| "MAA 配置不是 UTF-8".to_string())?
        .to_string();
    let (out, changed) = apply_task_patch(&text, idx, &task_type, &name, &patch)?;
    if changed.is_empty() {
        return Ok(json!({"ok": true, "changed": [], "note": "跟文件里一致，没写盘"}));
    }
    let bk = backup(&root, &cfg)
        .ok_or_else(|| "写前备份失败：拒绝改写 MAA 配置".to_string())?;
    let mut bytes = Vec::new();
    if had_bom {
        bytes.extend_from_slice(&[0xEF, 0xBB, 0xBF]);
    }
    bytes.extend_from_slice(out.as_bytes());
    fs::write(&cfg, &bytes).map_err(|e| format!("写 MAA 配置失败：{e}"))?;
    // 写后回读：逐键对平；不对就明说从 backups 拷回。
    let v2 = fs::read(&cfg)
        .ok()
        .map(|b| {
            let body = if b.starts_with(&[0xEF, 0xBB, 0xBF]) { b[3..].to_vec() } else { b };
            serde_json::from_slice::<Value>(&body)
        })
        .and_then(|r| r.ok())
        .ok_or_else(|| "写盘后 MAA 配置读不回来了".to_string())?;
    let item = dag(&v2, &["Configurations", "Default", "TaskQueue"])
        .and_then(|x| x.as_array())
        .and_then(|a| a.get(idx))
        .ok_or_else(|| "写盘后队列里找不到这一条".to_string())?;
    for (k, v) in &patch {
        if ci(item, k) != Some(v) {
            return Err(format!("写盘后回读 {k} 不一致：请从 backups/ 把上一份拷回来，再手动改这一项"));
        }
    }
    Ok(json!({
        "ok": true,
        "changed": changed,
        "backup": bk,
        "note": format!("已写入 MAA 配置第 {idx} 条（{task_type}）：只动这几项的值，其余字节原样")
    }))
}

fn join_rel(dir: &str, rel: &str) -> PathBuf {
    if dir.is_empty() {
        return PathBuf::from(rel);
    }
    let r = PathBuf::from(rel);
    if r.is_absolute() {
        return r;
    }
    Path::new(dir).join(r)
}

/* ── 下拉选项目录（读 MAA 自带 resource 下的 json；读不到就给空表，界面回落现值） ── */

fn read_json_loose(p: &Path) -> Option<Value> {
    let raw = fs::read(p).ok()?;
    let body = if raw.starts_with(&[0xEF, 0xBB, 0xBF]) { &raw[3..] } else { &raw[..] };
    serde_json::from_slice(body).ok()
}

fn res_file(dir: &str, rel: &[&str]) -> Option<Value> {
    let mut p = Path::new(dir).join("resource");
    for r in rel {
        p = p.join(r);
    }
    read_json_loose(&p)
}

fn uniq_strs(v: Vec<String>) -> Vec<String> {
    let mut seen = std::collections::HashSet::new();
    v.into_iter().filter(|s| !s.is_empty() && seen.insert(s.clone())).collect()
}

fn arr_strs(v: Option<&Value>, key: &str) -> Vec<String> {
    v.and_then(|x| x.get(key)).and_then(|x| x.as_array())
        .map(|a| a.iter().filter_map(|x| x.as_str()).map(String::from).collect())
        .unwrap_or_default()
}

/// 二级框的候选表：关卡代码、材料、公招 tags/干员、萨米密文板、坍缩范式、条目名、自定义基建、肉鸽干员。
fn maa_catalogs(dir: &str) -> Value {
    if dir.is_empty() {
        return json!({"stages":[], "materials":[], "operators":[], "tags":[], "foldartals":[], "paradigms":[],
            "items":[], "infrasts":[], "rogueOpers":[], "rogueOpersByTheme":{}});
    }
    let stages = res_file(dir, &["stages.json"])
        .and_then(|v| v.as_array().cloned())
        .map(|a| {
            uniq_strs(a.iter().filter_map(|x| x.get("code").and_then(|c| c.as_str()).map(String::from)).collect())
        })
        .unwrap_or_default();

    let index = res_file(dir, &["item_index.json"]).and_then(|v| v.as_object().cloned());
    let mut materials: Vec<Value> = Vec::new();
    let mut items: Vec<String> = Vec::new();
    if let Some(o) = index.clone() {
        for (id, x) in o.iter() {
            let name = x.get("name").and_then(|c| c.as_str()).unwrap_or("").to_string();
            if name.is_empty() {
                continue;
            }
            items.push(name.clone());
            if x.get("classifyType").and_then(|c| c.as_str()).unwrap_or("").eq_ignore_ascii_case("MATERIAL") {
                materials.push(json!({"id": id, "name": name}));
            }
        }
        materials.sort_by(|a, b| a["id"].as_str().unwrap_or("").cmp(b["id"].as_str().unwrap_or("")));
        items = uniq_strs(items);
        items.sort();
    }

    let (operators, tags) = match res_file(dir, &["recruitment.json"]) {
        Some(v) => {
            let ops = uniq_strs(arr_strs(v.get("operators"), "name"));
            let tags = match v.get("tags") {
                Some(Value::Object(o)) => uniq_strs(o.values().filter_map(|x| x.as_str()).map(String::from).collect()),
                Some(Value::Array(a)) => uniq_strs(a.iter().filter_map(|x| x.as_str()).map(String::from).collect()),
                _ => Vec::new(),
            };
            (ops, tags)
        }
        None => (Vec::new(), Vec::new()),
    };

    let foldartals = res_file(dir, &["roguelike", "Sami", "foldartal.json"])
        .map(|v| {
            let mut out = Vec::new();
            if let Some(groups) = v.get("foldartal").and_then(|x| x.as_array()) {
                for g in groups {
                    if let Some(arr) = g.get("foldartal").and_then(|x| x.as_array()) {
                        for f in arr {
                            if let Some(n) = f.get("name").and_then(|x| x.as_str()) {
                                out.push(n.to_string());
                            }
                        }
                    }
                }
            }
            uniq_strs(out)
        })
        .unwrap_or_default();

    let paradigms = res_file(dir, &["roguelike", "Sami", "collapsal_paradigms.json"])
        .map(|v| {
            let mut out = Vec::new();
            if let Some(arr) = v.get("collapsal_paradigms").and_then(|x| x.as_array()) {
                for p in arr {
                    if let Some(n) = p.get("level_1").and_then(|x| x.as_str()) {
                        out.push(n.to_string());
                    }
                }
            }
            uniq_strs(out)
        })
        .unwrap_or_default();

    // MAA 6.x 的 Filename 按路径读取；候选只列顶层 *.json，不递归、不剥扩展名。
    let infrasts = match fs::read_dir(Path::new(dir).join("resource").join("custom_infrast")) {
        Ok(rd) => {
            let mut v: Vec<String> = rd
                .filter_map(|e| e.ok())
                .filter(|e| e.file_type().map(|t| t.is_file()).unwrap_or(false))
                .map(|e| e.file_name().to_string_lossy().to_string())
                .filter(|n| n.to_ascii_lowercase().ends_with(".json"))
                .map(|n| format!("resource/custom_infrast/{n}"))
                .collect();
            v.sort();
            v
        }
        Err(_) => Vec::new(),
    };

    // 开局干员按主题分表；官方候选逻辑只收 priority[].opers[] 里 is_start=true 的干员。
    let mut rogue_by_theme = serde_json::Map::new();
    let mut rogue_opers: Vec<String> = Vec::new();
    if let Ok(rd) = fs::read_dir(Path::new(dir).join("resource").join("roguelike")) {
        for e in rd.filter_map(|x| x.ok()).filter(|e| e.file_type().map(|t| t.is_dir()).unwrap_or(false)) {
            let theme = e.file_name().to_string_lossy().to_string();
            let mut names = Vec::new();
            if let Some(v) = read_json_loose(&e.path().join("recruitment.json")) {
                if let Some(prio) = v.get("priority").and_then(|x| x.as_array()) {
                    for grp in prio {
                        if let Some(ops) = grp.get("opers").and_then(|x| x.as_array()) {
                            for o in ops {
                                if o.get("is_start").and_then(|x| x.as_bool()) == Some(true) {
                                    if let Some(n) = o.get("name").and_then(|x| x.as_str()) {
                                        names.push(n.to_string());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            let names = uniq_strs(names);
            rogue_opers.extend(names.iter().cloned());
            rogue_by_theme.insert(theme, json!(names));
        }
    }

    json!({
        "stages": stages,
        "materials": materials,
        "operators": operators,
        "tags": tags,
        "foldartals": foldartals,
        "paradigms": paradigms,
        "items": items,
        "infrasts": infrasts,
        "rogueOpers": uniq_strs(rogue_opers),
        "rogueOpersByTheme": Value::Object(rogue_by_theme)
    })
}

fn stem(p: &str) -> String {
    if p.is_empty() {
        return String::new();
    }
    Path::new(p)
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or("")
        .to_string()
}

/* ── 脱敏：本机绝对路径 → <本地路径>（界面与回执都不落真实路径） ── */

pub(crate) fn scrub(s: &str) -> String {
    let chars: Vec<char> = s.chars().collect();
    let mut out = String::new();
    let mut i = 0;
    while i < chars.len() {
        let c = chars[i];
        let drive = c.is_ascii_alphabetic()
            && i + 2 < chars.len()
            && chars[i + 1] == ':'
            && (chars[i + 2] == '\\' || chars[i + 2] == '/');
        let unc = c == '\\' && i + 1 < chars.len() && chars[i + 1] == '\\';
        if drive || unc {
            out.push_str("<本地路径>");
            let mut j = i + 2;
            while j < chars.len() {
                let d = chars[j];
                if d.is_whitespace() || matches!(d, '，' | '。' | '）' | ')' | '；' | ';' | '、' | '|' | '"') {
                    break;
                }
                j += 1;
            }
            i = j;
        } else {
            out.push(c);
            i += 1;
        }
    }
    out
}

fn scrub_lines(lines: Vec<String>) -> Vec<String> {
    lines.into_iter().map(|l| scrub(&l)).collect()
}

/* ── verb 通道：拆信封，只回 data；失败回内核原话（已脱敏） ── */

fn data_of(env: Value) -> Result<Value, String> {
    let ok = env.get("ok").and_then(|x| x.as_bool()).unwrap_or(false);
    if !ok {
        let e = env.get("error").and_then(|x| x.as_str()).unwrap_or("内核拒绝了这个操作");
        return Err(scrub(e));
    }
    Ok(env.get("data").cloned().unwrap_or(Value::Null))
}

fn lines_of(d: &Value) -> Vec<String> {
    scrub_lines(strs(d.get("Lines")))
}

fn call(root: &Path, verb: &str, body: &Value, secs: u64) -> Result<Value, String> {
    let env = crate::control::call_verb_t(root, verb, body, &[], secs)?;
    data_of(env)
}

/* ══ 现状读数 ══ */

pub fn state() -> Result<Value, String> {
    let root = root()?;
    let settings = match read_settings(&root)? {
        Some(s) => s,
        None => {
            return Ok(json!({
                "settingsPresent": false,
                "maa": Value::Null,
                "m9a": Value::Null,
                "note": "config/settings.json 不在：先跑 setup.ps1 生成配置"
            }))
        }
    };
    let maa = maa_state(&root, &settings);
    let m9a = m9a_state(&root, &settings);
    let runtime_flags = runtime_flags(&settings);
    Ok(json!({"settingsPresent": true, "runtimeFlags": runtime_flags, "maa": maa, "m9a": m9a}))
}

/// 右栏开关只报文件现值；缺键/null/类型不对均为未知，不套用 setup 模板默认。
fn runtime_flags(settings: &Value) -> Value {
    let rt = ci(settings, "runtime").cloned().unwrap_or(json!({}));
    let notify = ci(settings, "notify").cloned().unwrap_or(json!({}));
    let mut out = serde_json::Map::new();
    for key in ["retryOnce", "arkVisionRescue", "mumuVisionRescue", "visionAllowReward", "supervisor", "keepScreenOn", "abortOnMaaStallWarning"] {
        out.insert(key.to_string(), match ci(&rt, key) {
            Some(v) if v.is_boolean() => v.clone(),
            _ => Value::Null,
        });
    }
    out.insert("notify.toast".to_string(), match ci(&notify, "toast") {
        Some(v) if v.is_boolean() => v.clone(),
        _ => Value::Null,
    });
    Value::Object(out)
}

fn maa_state(root: &Path, settings: &Value) -> Value {
    let p = ci(settings, "paths").cloned().unwrap_or(json!({}));
    let dir = s_ci(&p, "maaDir");
    let exe_rel = s_ci(&p, "maaExe");
    let cfg_rel = s_ci(&p, "maaConfig");
    let exe = join_rel(&dir, &exe_rel);
    let cfg = join_rel(&dir, &cfg_rel);
    let name = stem(&exe_rel);
    let ark_exe = s_ci(&p, "arknightsExe");

    let (queue, queue_error) = if cfg.is_file() {
        match call(
            root,
            "maa-queue-lines",
            &json!({ "cfgPath": cfg.to_string_lossy() }),
            60,
        ) {
            Ok(d) => (lines_of(&d), None),
            Err(e) => (Vec::new(), Some(e)),
        }
    } else {
        (Vec::new(), Some("MAA 配置不在（settings.paths.maaDir + maaConfig 指向的那份）".to_string()))
    };

    let ark = ci(settings, "ark").cloned().unwrap_or(json!({}));
    let rt = ci(settings, "runtime").cloned().unwrap_or(json!({}));
    let (tasks, tasks_error) = maa_task_rows(&cfg, &ark);
    json!({
        "catalogs": maa_catalogs(&dir),
        "dirReady": !dir.is_empty() && Path::new(&dir).is_dir(),
        "exeReady": !exe_rel.is_empty() && exe.is_file(),
        "configReady": cfg.is_file(),
        "exeName": name,
        "running": !name.is_empty() && crate::live::any_running(&[name.as_str()]),
        "gameRunning": !ark_exe.is_empty() && crate::live::any_running(&[stem(&ark_exe).as_str()]),
        "queue": queue,
        "queueError": queue_error,
        "tasks": tasks,
        "tasksError": tasks_error,
        "plan": {
            "defaultStage": s_ci(&ark, "defaultStage"),
            "finishAllSanityWith": s_ci(&ark, "finishAllSanityWith"),
            "keepQueuesEnabled": strs(ci(&ark, "keepQueuesEnabled")),
            "disableQueues": strs(ci(&ark, "disableQueues")),
            "farmAppend": b_ci(&ark, "farmAppend"),
            "farmMaxTimesPerRound": farm_count_ci(&ark, "farmMaxTimesPerRound"),
            "alsoEnableMainFight": b_ci(&ark, "alsoEnableMainFight"),
            "quitAssistantAfter": b_ci(&rt, "quitAssistantAfter"),
            "closeArknightsAfter": b_ci(&rt, "closeArknightsAfter"),
            "launchArkFromHelper": b_ci(&rt, "launchArkFromHelper")
        }
    })
}

/// 实例清单：读 appsettings 与实例目录下的逐台 json。这里是**展示用**的聚合（名字/任务名/ADB 串），
/// 不复制任何判定规则：切实例仍走 apply-m9a-instance，探活仍走 read-m9a-connection。
fn instances_now(dir: &str, app: &Value, catalogs: &Value) -> (Vec<Value>, Option<Value>) {
    let mut out = Vec::new();
    let rd = match fs::read_dir(dir) {
        Ok(r) => r,
        Err(_) => return (out, None),
    };
    let mut files: Vec<PathBuf> = rd
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.extension().and_then(|x| x.to_str()).map(|x| x.eq_ignore_ascii_case("json")).unwrap_or(false))
        .collect();
    files.sort();
    let want = s_ci(app, "Instances.LastActiveName");
    let want_id = s_ci(app, "Instances.LastActive");
    let want_id = want_id.trim_end_matches(".json").trim_end_matches(".JSON");
    let mut current: Option<Value> = None;
    for f in files {
        let raw = match fs::read(&f) {
            Ok(b) => b,
            Err(_) => continue,
        };
        let body = if raw.starts_with(&[0xEF, 0xBB, 0xBF]) { &raw[3..] } else { &raw[..] };
        let v: Value = match serde_json::from_slice(body) {
            Ok(v) => v,
            Err(_) => continue,
        };
        let name = s_ci(&v, "InstanceName");
        let id = f.file_stem().and_then(|x| x.to_str()).unwrap_or("").to_string();
        let current_tasks = strs(ci(&v, "CurrentTasks"));
        let tasks: Vec<String> = current_tasks
            .iter()
            .map(|t| {
                // 真实件里的分隔是 `<|||>`（三竖线）；老件见过 `<||>`，两种都认，取任务名那一段。
                t.split("<|||>").next().unwrap_or("").split("<||>").next().unwrap_or("").to_string()
            })
            .filter(|t| !t.is_empty())
            .collect();
        let dev = v.get("AdbDevice").cloned().unwrap_or(json!({}));
        let resource = s_ci(&v, "Resource");
        let controller = s_ci(&v, "CurrentControllerName");
        let task_items = m9a_safe_task_items(ci(&v, "TaskItems"), catalogs);
        let row = json!({
            "id": id,
            "name": name,
            "taskCount": tasks.len(),
            "tasks": tasks.iter().take(5).cloned().collect::<Vec<_>>(),
            // 右侧二级框要完整任务表与资源/控制器；卡面仍用上面截断的五枚。
            "allTasks": tasks,
            "currentTasks": current_tasks,
            "taskItems": task_items,
            "resource": resource,
            "controller": controller,
            "preset": s_ci(&v, "InstancePresetKey"),
            "keepEmptyTaskQueue": b_ci(&v, "KeepEmptyTaskQueue"),
            "resourceOptions": m9a_safe_nodes(ci(&v, "ResourceOptionItems").and_then(|x| ci(x, &resource)), &m9a_named_defs(catalogs, "resourceOptions", &resource)),
            "controllerOptions": m9a_safe_nodes(ci(&v, "ControllerOptionItems").and_then(|x| ci(x, &controller)), &m9a_named_defs(catalogs, "controllerOptions", &controller)),
            "globalOptions": m9a_safe_nodes(ci(&v, "GlobalOptionItems"), catalogs.get("globalOptions").and_then(|x| x.as_array()).map(|x| x.as_slice()).unwrap_or(&[])),
            "adbSerial": s_ci(&dev, "AdbSerial"),
            "adbName": s_ci(&dev, "Name")
        });
        if current.is_none()
            && ((!want.is_empty() && row["name"].as_str().unwrap_or("").eq_ignore_ascii_case(&want))
                || (!want_id.is_empty() && id.eq_ignore_ascii_case(want_id)))
        {
            current = Some(row.clone());
        }
        out.push(row);
    }
    (out, current)
}

// 实时画面的目标只在 Rust 内解析；序列号、绝对路径、窗口标题均不交给前端。
pub(crate) struct CaptureTarget {
    pub process: String,
    pub adb: PathBuf,
    pub serial: String,
}

pub(crate) fn capture_target(game: &str) -> Result<CaptureTarget, String> {
    let root = crate::live::find_root()
        .ok_or_else(|| "找不到仓库根目录（把 gdops.exe 放进仓库，或设 GDOPS_ROOT）".to_string())?;
    let settings = read_settings(&root)?.ok_or_else(|| "settings.json 不在，无法定位游戏画面".to_string())?;
    let paths = ci(&settings, "paths").cloned().unwrap_or(json!({}));
    if game.eq_ignore_ascii_case("ark") {
        let process = stem(&s_ci(&paths, "arknightsExe"));
        if process.is_empty() { return Err("端游进程名未配置，无法定位明日方舟窗口".to_string()); }
        return Ok(CaptureTarget { process, adb: PathBuf::new(), serial: String::new() });
    }
    if game.eq_ignore_ascii_case("r1999") {
        let dir = s_ci(&paths, "m9aDir");
        // mumuAdb 的相对基准与内核 Gate.Host.MuMuEndpoint 一致：mumuDir，不是 m9aDir。
        let adb = capture_adb_path(&paths);
        if !adb.is_file() { return Err("模拟器 adb.exe 不在，无法读取 M9A 设备画面".to_string()); }
        let app_path = join_rel(&dir, &s_ci(&paths, "m9aAppSettings"));
        let inst_dir = join_rel(&dir, &s_ci(&paths, "m9aInstancesDir"));
        let app = read_json_loose(&app_path).unwrap_or(json!({}));
        let want = s_ci(&app, "Instances.LastActiveName");
        let want_id = s_ci(&app, "Instances.LastActive");
        let mut chosen: Option<String> = None;
        let mut candidates = Vec::new();
        let Ok(rd) = fs::read_dir(&inst_dir) else {
            return Err("M9A 实例目录读不到，无法确定当前设备".to_string());
        };
        for item in rd.flatten() {
            let p = item.path();
            if p.extension().and_then(|x| x.to_str()).map(|x| x.eq_ignore_ascii_case("json")) != Some(true) { continue; }
            let Some(v) = read_json_loose(&p) else { continue };
            let id = p.file_stem().and_then(|x| x.to_str()).unwrap_or("").to_string();
            let name = s_ci(&v, "InstanceName");
            let dev = v.get("AdbDevice").cloned().unwrap_or(json!({}));
            let serial = s_ci(&dev, "AdbSerial");
            if !serial.is_empty() { candidates.push(serial.clone()); }
            if m9a_instance_matches(&want_id, &want, &id, &name) {
                chosen = Some(serial);
                break;
            }
        }
        let serial = chosen.filter(|x| !x.is_empty()).or_else(|| {
            // 只有当前实例指针整体缺失时才允许唯一候选兜底；指针失效不能悄悄改摄其他实例。
            if want.is_empty() && want_id.is_empty() && candidates.len() == 1 { candidates.first().cloned() } else { None }
        }).ok_or_else(|| "当前 M9A 实例没有可用的 ADB 设备；请先在助手页选中并连接实例".to_string())?;
        return Ok(CaptureTarget { process: String::new(), adb, serial });
    }
    Err("这条插件路没有实时画面通道".to_string())
}

fn m9a_instance_matches(want_id: &str, want_name: &str, id: &str, name: &str) -> bool {
    // ID 是主身份。ID 一旦失效，不许借同名旧实例继续摄画面。
    if !want_id.is_empty() {
        id.eq_ignore_ascii_case(want_id)
    } else {
        !want_name.is_empty() && name.eq_ignore_ascii_case(want_name)
    }
}

fn capture_adb_path(paths: &Value) -> PathBuf {
    join_rel(&s_ci(paths, "mumuDir"), &s_ci(paths, "mumuAdb"))
}

fn mumu_vms(root: &Path, paths: &Value) -> Vec<Value> {
    let manager = join_rel(&s_ci(paths, "mumuDir"), &s_ci(paths, "mumuManager"));
    if !manager.is_file() {
        return Vec::new();
    }
    let mut cmd = Command::new(&manager);
    cmd.args(["info", "--vmindex", "all"]);
    let Ok((code, out, _)) = crate::control::run_capture(&mut cmd, Duration::from_secs(40)) else {
        return Vec::new();
    };
    if code != 0 {
        return Vec::new();
    }
    let raw = out.join("\n");
    if raw.trim().is_empty() {
        return Vec::new();
    }
    // 判定仍归内核；这里只把 Vms[].Index/Name 换成前端的小写字段并脱敏。
    let Ok(d) = call(root, "parse-mumu-vms", &json!({ "raw": raw }), 25) else {
        return Vec::new();
    };
    mumu_vms_from(&d)
}

fn scrub_atom(s: &str) -> String {
    let t = s.trim();
    let bytes = t.as_bytes();
    let drive = bytes.len() >= 3
        && bytes[0].is_ascii_alphabetic()
        && bytes[1] == b':'
        && matches!(bytes[2], b'\\' | b'/');
    let unc = bytes.len() >= 2 && bytes[0] == b'\\' && bytes[1] == b'\\';
    if drive || unc { "<本地路径>".to_string() } else { scrub(s) }
}

fn mumu_vms_from(d: &Value) -> Vec<Value> {
    d.get("Vms")
        .and_then(|x| x.as_array())
        .map(|a| {
            a.iter()
                .filter_map(|vm| {
                    let index = vm.get("Index")?.as_str()?;
                    if index.is_empty() || !index.bytes().all(|b| b.is_ascii_digit()) {
                        return None;
                    }
                    let name = vm.get("Name")?.as_str()?;
                    Some(json!({"index": index, "name": scrub_atom(name)}))
                })
                .collect()
        })
        .unwrap_or_default()
}

fn m9a_state(root: &Path, settings: &Value) -> Value {
    let p = ci(settings, "paths").cloned().unwrap_or(json!({}));
    let dir = s_ci(&p, "m9aDir");
    let exe_rel = s_ci(&p, "m9aExe");
    let app_rel = s_ci(&p, "m9aAppSettings");
    let inst_rel = s_ci(&p, "m9aInstancesDir");
    let exe = join_rel(&dir, &exe_rel);
    let app_path = join_rel(&dir, &app_rel);
    let inst_dir = join_rel(&dir, &inst_rel);
    let name = stem(&exe_rel);

    let app = if app_path.is_file() {
        fs::read(&app_path)
            .ok()
            .and_then(|b| {
                let body = if b.starts_with(&[0xEF, 0xBB, 0xBF]) { &b[3..] } else { &b[..] };
                serde_json::from_slice::<Value>(body).ok()
            })
            .unwrap_or(json!({}))
    } else {
        json!({})
    };

    let catalogs = m9a_catalogs(&dir);
    let (instances, current) = if inst_dir.is_dir() {
        instances_now(&inst_dir.to_string_lossy(), &app, &catalogs)
    } else {
        (Vec::new(), None)
    };

    let version = match call(
        root,
        "read-m9a-install",
        &json!({ "settings": { "paths": p } }),
        60,
    ) {
        Ok(d) => json!({"current": d.get("Current").cloned().unwrap_or(json!("")),
                        "repo": d.get("Repo").cloned().unwrap_or(json!(""))}),
        Err(e) => json!({"error": e}),
    };

    let (conn, conn_error) = match &current {
        Some(c) => {
            let id = c.get("id").and_then(|x| x.as_str()).unwrap_or("");
            let inst_path = inst_dir.join(format!("{id}.json"));
            let body = json!({"instance": {"Name": c.get("name"), "Path": inst_path.to_string_lossy() }});
            match call(root, "read-m9a-connection", &body, 60) {
                Ok(d) => (json!({"serial": d.get("Serial").cloned().unwrap_or(json!("")),
                                 "name": d.get("Name").cloned().unwrap_or(json!(""))}), None),
                Err(e) => (Value::Null, Some(e)),
            }
        }
        None => (Value::Null, None),
    };

    let mut mumu = match call(root, "mumu-state", &json!({}), 90) {
        Ok(d) => json!({
            "available": d.get("Available").and_then(|x| x.as_bool()).unwrap_or(false),
            "android": d.get("AndroidStarted").and_then(|x| x.as_bool()).unwrap_or(false),
            "process": d.get("ProcessStarted").and_then(|x| x.as_bool()).unwrap_or(false),
            "name": d.get("Name").cloned().unwrap_or(json!("")),
            "index": d.get("Index").cloned().unwrap_or(json!(""))
        }),
        Err(e) => json!({"available": false, "android": false, "process": false, "name": "", "index": "", "error": e}),
    };
    // 清单是独立只读旁路：管理器缺失、超时、非零退出或解析失败都只回空表，不影响上面的状态。
    mumu["vms"] = json!(mumu_vms(root, &p));

    let re = ci(settings, "re1999").cloned().unwrap_or(json!({}));
    let map = ci(&re, "planToInstance").cloned().unwrap_or(json!({}));
    let rt = ci(settings, "runtime").cloned().unwrap_or(json!({}));
    let mm = ci(settings, "mumu").cloned().unwrap_or(json!({}));
    json!({
        "dirReady": !dir.is_empty() && Path::new(&dir).is_dir(),
        "exeReady": !exe_rel.is_empty() && exe.is_file(),
        "appReady": app_path.is_file(),
        "instancesReady": inst_dir.is_dir(),
        "running": !name.is_empty() && crate::live::any_running(&[name.as_str()]),
        "lastActiveName": s_ci(&app, "Instances.LastActiveName"),
        "lastActiveId": s_ci(&app, "Instances.LastActive"),
        "noAutoStart": s_ci(&app, "NoAutoStart"),
        "instances": instances,
        "current": current,
        "catalogs": catalogs,
        "version": version,
        "conn": conn,
        "connError": conn_error,
        "mumu": mumu,
        "plan": {
            "idle": s_ci(&map, "idle"),
            "event": s_ci(&map, "event"),
            "rerun": s_ci(&map, "rerun"),
            "mainStoryRequiresAck": b_ci(&re, "mainStoryRequiresAck"),
            "shopDoneStopsEvent": b_ci(&re, "shopDoneStopsEvent"),
            "vmIndex": s_ci(&mm, "vmIndex"),
            "m9aCliArgs": b_ci(&rt, "m9aCliArgs"),
            "m9aForceStart": b_ci(&rt, "m9aForceStart"),
            "closeEmulatorAfter": b_ci(&rt, "closeEmulatorAfter"),
            "takeOverAssistant": b_ci(&rt, "takeOverAssistant")
        }
    })
}

/* ── M9A 配置目录：候选只来自本机 interface / tasks / preset，不硬编码任务 ── */

fn m9a_string_list(v: Option<&Value>) -> Vec<String> {
    match v {
        Some(Value::String(s)) if !s.is_empty() => vec![s.clone()],
        Some(Value::Array(a)) => a.iter().filter_map(|x| x.as_str().map(String::from)).filter(|s| !s.is_empty()).collect(),
        _ => Vec::new(),
    }
}

fn m9a_localized(v: &Value, key: &str, locale: &Value) -> Option<String> {
    let raw = v.get(key).and_then(|x| x.as_str())?;
    if let Some(k) = raw.strip_prefix('$') {
        locale.get(k).and_then(|x| x.as_str()).map(String::from)
    } else if raw.is_empty() { None } else { Some(raw.to_string()) }
}

fn m9a_option_def(v: &Value, fallback_name: &str, locale: &Value) -> Value {
    let cases = v.get("cases").and_then(|x| x.as_array()).map(|a| {
        a.iter().filter_map(|c| {
            let name = c.get("name").and_then(|x| x.as_str())?;
            if name.is_empty() { return None; }
            let mut out = json!({"name": name});
            if let Some(label) = m9a_localized(c, "label", locale) { out["label"] = json!(label); }
            if let Some(desc) = m9a_localized(c, "description", locale) { out["description"] = json!(desc); }
            if let Some(children) = c.get("option").and_then(|x| x.as_array()) {
                out["options"] = json!(children.iter().filter_map(|x| x.as_str().map(String::from)).collect::<Vec<_>>());
            }
            Some(out)
        }).collect::<Vec<_>>()
    }).unwrap_or_default();
    let inputs = v.get("inputs").and_then(|x| x.as_array()).map(|a| {
        a.iter().filter_map(|x| {
            let name = x.get("name").and_then(|s| s.as_str())?;
            if name.is_empty() { return None; }
            let password = x.get("password").and_then(|s| s.as_bool()).unwrap_or(false);
            let mut out = json!({
                "name": name,
                "default": if password { String::new() } else { x.get("default").and_then(|s| s.as_str()).unwrap_or("").to_string() },
                "password": password
            });
            if let Some(label) = m9a_localized(x, "label", locale) { out["label"] = json!(label); }
            Some(out)
        }).collect::<Vec<_>>()
    }).unwrap_or_default();
    let default = v.get("default_case").cloned().unwrap_or(Value::Null);
    let mut out = json!({
        "name": fallback_name,
        "type": v.get("type").and_then(|x| x.as_str()).unwrap_or("input"),
        "cases": cases,
        "inputs": inputs,
        "defaultCase": default
    });
    if let Some(label) = m9a_localized(v, "label", locale) { out["label"] = json!(label); }
    if let Some(desc) = m9a_localized(v, "description", locale) { out["description"] = json!(desc); }
    out
}

fn m9a_collect_def(
    defs: &serde_json::Map<String, Value>,
    name: &str,
    locale: &Value,
    seen: &mut HashSet<String>,
    out: &mut Vec<Value>,
    depth: usize,
) {
    if depth > 16 || !seen.insert(name.to_string()) { return; }
    let Some(raw) = defs.get(name) else { return; };
    // interface/tasks 的 option 是「键名 -> 定义」，定义内部通常没有 name；身份必须取外层键。
    let def = m9a_option_def(raw, name, locale);
    let children: Vec<String> = def.get("cases").and_then(|x| x.as_array()).into_iter().flatten()
        .flat_map(|c| c.get("options").and_then(|x| x.as_array()).cloned().unwrap_or_default())
        .filter_map(|x| x.as_str().map(String::from)).collect();
    out.push(def);
    for child in children { m9a_collect_def(defs, &child, locale, seen, out, depth + 1); }
}

fn m9a_defs_from_names(defs: &serde_json::Map<String, Value>, names: &[String], locale: &Value) -> Vec<Value> {
    // task[].option 只列直接父项；case.option 引用的子项要按定义图递归展开。
    let mut out = Vec::new();
    let mut seen = HashSet::new();
    for name in names { m9a_collect_def(defs, name, locale, &mut seen, &mut out, 0); }
    out
}

fn m9a_named_defs(catalogs: &Value, group: &str, name: &str) -> Vec<Value> {
    catalogs.get(group).and_then(|x| x.get(name)).and_then(|x| x.as_array()).cloned().unwrap_or_default()
}

fn m9a_task_defs(catalogs: &Value, name: &str) -> Vec<Value> {
    catalogs.get("tasks").and_then(|x| x.as_array())
        .and_then(|a| a.iter().find(|t| t.get("name").and_then(|x| x.as_str()) == Some(name)))
        .and_then(|t| t.get("options")).and_then(|x| x.as_array()).cloned().unwrap_or_default()
}

/// 只回面板需要的选择字段；input 仅回非密码现值，密码/未知数据一律不离开 Rust。
fn m9a_safe_nodes(raw: Option<&Value>, defs: &[Value]) -> Vec<Value> {
    let Some(arr) = raw.and_then(|x| x.as_array()) else { return Vec::new() };
    arr.iter().filter_map(|node| {
        let name = node.get("name").and_then(|x| x.as_str())?;
        let def = defs.iter().find(|d| d.get("name").and_then(|x| x.as_str()) == Some(name))?;
        let mut out = json!({"name": name});
        if let Some(i) = node.get("index").and_then(|x| x.as_i64()) { out["index"] = json!(i); }
        if let Some(s) = node.get("selected_case").and_then(|x| x.as_str()) { out["selected_case"] = json!(s); }
        if let Some(cs) = node.get("selected_cases") {
            out["selected_cases"] = json!(m9a_string_list(Some(cs)));
        }
        let ty = def.get("type").and_then(|x| x.as_str()).unwrap_or("");
        if ty == "input" || ty == "hotkey" {
            let mut data = serde_json::Map::new();
            if let Some(saved) = node.get("data").and_then(|x| x.as_object()) {
                for input in def.get("inputs").and_then(|x| x.as_array()).into_iter().flatten() {
                    if input.get("password").and_then(|x| x.as_bool()).unwrap_or(false) { continue; }
                    let key = input.get("name").and_then(|x| x.as_str()).unwrap_or("");
                    if let Some(v) = saved.get(key).and_then(|x| x.as_str()) { data.insert(key.to_string(), json!(v)); }
                }
            }
            if !data.is_empty() { out["data"] = Value::Object(data); }
        }
        // 子选项定义要从同一份目录取；安全节点递归时只保留当前激活分支。
        if let Some(children) = m9a_active_child_defs_with(def, node, defs) {
            if !children.is_empty() {
                let safe = m9a_safe_nodes(node.get("sub_options"), &children);
                if !safe.is_empty() { out["sub_options"] = json!(safe); }
            }
        }
        Some(out)
    }).collect()
}

fn m9a_materialize_active_selection(node: &mut Value, def: &Value) {
    let Some(obj) = node.as_object_mut() else { return; };
    let ty = def.get("type").and_then(|x| x.as_str()).unwrap_or("");
    if ty == "checkbox" {
        if obj.keys().any(|k| k.eq_ignore_ascii_case("selected_cases")) { return; }
        let selected: Vec<String> = m9a_string_list(def.get("defaultCase")).into_iter()
            .filter(|n| m9a_case_index(def, n).is_some()).collect();
        obj.insert("selected_cases".to_string(), json!(selected));
    } else if ty == "switch" || ty == "select" {
        if obj.keys().any(|k| k.eq_ignore_ascii_case("selected_case") || k.eq_ignore_ascii_case("index")) { return; }
        let all = def.get("cases").and_then(|x| x.as_array()).cloned().unwrap_or_default();
        let selected = def.get("defaultCase").and_then(|x| x.as_str())
            .or_else(|| all.first().and_then(|c| c.get("name")).and_then(|x| x.as_str()));
        if let Some(name) = selected {
            if let Some(idx) = m9a_case_index(def, name) {
                obj.insert("index".to_string(), json!(idx));
                obj.insert("selected_case".to_string(), json!(name));
            }
        }
    }
}

fn m9a_active_child_defs_with(def: &Value, node: &Value, defs: &[Value]) -> Option<Vec<Value>> {
    let all = def.get("cases").and_then(|x| x.as_array())?;
    let ty = def.get("type").and_then(|x| x.as_str()).unwrap_or("");
    let selected: Vec<String> = if ty == "checkbox" {
        if node.get("selected_cases").is_some() { m9a_string_list(node.get("selected_cases")) }
        else { m9a_string_list(def.get("defaultCase")) }
    } else if ty == "switch" || ty == "select" {
        let name = node.get("selected_case").and_then(|x| x.as_str()).map(String::from)
            .or_else(|| node.get("index").and_then(|x| x.as_u64()).and_then(|i| all.get(i as usize)).and_then(|c| c.get("name")).and_then(|x| x.as_str()).map(String::from));
        match name {
            Some(n) => vec![n],
            None => match def.get("defaultCase") {
                Some(Value::String(s)) => vec![s.clone()],
                _ => all.first().and_then(|c| c.get("name")).and_then(|x| x.as_str()).map(|s| vec![s.to_string()]).unwrap_or_default(),
            }
        }
    } else { Vec::new() };
    let names: Vec<String> = selected.iter().flat_map(|n| {
        all.iter().find(|c| c.get("name").and_then(|x| x.as_str()) == Some(n))
            .and_then(|c| c.get("options")).and_then(|x| x.as_array()).cloned().unwrap_or_default()
            .into_iter().filter_map(|x| x.as_str().map(String::from)).collect::<Vec<_>>()
    }).collect();
    Some(names.iter().filter_map(|n| defs.iter().find(|d| d.get("name").and_then(|x| x.as_str()) == Some(n)).cloned()).collect())
}

fn m9a_safe_task_items(raw: Option<&Value>, catalogs: &Value) -> Vec<Value> {
    raw.and_then(|x| x.as_array()).map(|a| a.iter().filter_map(|item| {
        let name = item.get("name").and_then(|x| x.as_str())?;
        let mut out = json!({"name": name});
        if let Some(entry) = item.get("entry").and_then(|x| x.as_str()) { out["entry"] = json!(entry); }
        let opts = m9a_safe_nodes(item.get("option"), &m9a_task_defs(catalogs, name));
        if !opts.is_empty() { out["option"] = json!(opts); }
        Some(out)
    }).collect()).unwrap_or_default()
}

fn m9a_catalogs(dir: &str) -> Value {
    let empty = json!({"resources":[],"controllers":[],"presets":[],"tasks":[],
        "resourceOptions":{},"controllerOptions":{},"globalOptions":[]});
    if dir.is_empty() { return empty; }
    let base = Path::new(dir);
    let interface = read_json_loose(&base.join("interface.json")).unwrap_or(json!({}));
    let locale_path = interface.get("languages").and_then(|x| x.get("zh_cn")).and_then(|x| x.as_str()).unwrap_or("locales/zh_cn.json");
    let locale = read_json_loose(&base.join(locale_path)).unwrap_or(json!({}));
    let mut defs: serde_json::Map<String, Value> = interface.get("option").and_then(|x| x.as_object()).cloned().unwrap_or_default();
    let mut task_specs: Vec<(String, String, Vec<String>)> = Vec::new();
    if let Ok(rd) = fs::read_dir(base.join("tasks")) {
        for e in rd.filter_map(|x| x.ok()) {
            let p = e.path();
            if !p.extension().and_then(|x| x.to_str()).map(|x| x.eq_ignore_ascii_case("json")).unwrap_or(false) { continue; }
            let Some(v) = read_json_loose(&p) else { continue; };
            if let Some(o) = v.get("option").and_then(|x| x.as_object()) {
                for (k, val) in o { defs.entry(k.clone()).or_insert_with(|| val.clone()); }
            }
            if let Some(tasks) = v.get("task").and_then(|x| x.as_array()) {
                for t in tasks {
                    let name = t.get("name").and_then(|x| x.as_str()).unwrap_or("");
                    let entry = t.get("entry").and_then(|x| x.as_str()).unwrap_or("");
                    if name.is_empty() || entry.is_empty() { continue; }
                    let names = m9a_string_list(t.get("option"));
                    task_specs.push((name.to_string(), entry.to_string(), names));
                }
            }
        }
    }
    // 资源附带 interface.json 里的选项定义（当前官服为空，但升级资源后可直接跟随）。
    if let Ok(rd) = fs::read_dir(base.join("resource")) {
        for e in rd.filter_map(|x| x.ok()) {
            let p = e.path().join("interface.json");
            if let Some(v) = read_json_loose(&p) {
                if let Some(o) = v.get("option").and_then(|x| x.as_object()) {
                    for (k, val) in o { defs.entry(k.clone()).or_insert_with(|| val.clone()); }
                }
            }
        }
    }
    // 所有任务文件都可能定义别的任务要引用的选项；先并完定义图，再生成每个任务的闭包。
    let mut task_rows: Vec<Value> = task_specs.into_iter().map(|(name, entry, names)| {
        json!({"name": name, "entry": entry, "options": m9a_defs_from_names(&defs, &names, &locale)})
    }).collect();
    let resources: Vec<Value> = interface.get("resource").and_then(|x| x.as_array()).map(|a| a.iter().filter_map(|r| {
        let name = r.get("name").and_then(|x| x.as_str())?;
        let names = m9a_string_list(r.get("option"));
        Some(json!({"name": name, "controllers": m9a_string_list(r.get("controller")),
            "options": m9a_defs_from_names(&defs, &names, &locale)}))
    }).collect()).unwrap_or_default();
    let mut resource_options = serde_json::Map::new();
    for r in &resources {
        let name = r.get("name").and_then(|x| x.as_str()).unwrap_or("");
        resource_options.insert(name.to_string(), r.get("options").cloned().unwrap_or(json!([])));
    }
    let controllers: Vec<Value> = interface.get("controller").and_then(|x| x.as_array()).map(|a| a.iter().filter_map(|c| {
        let name = c.get("name").and_then(|x| x.as_str())?;
        let names = m9a_string_list(c.get("option"));
        Some(json!({"name": name, "options": m9a_defs_from_names(&defs, &names, &locale)}))
    }).collect()).unwrap_or_default();
    let mut controller_options = serde_json::Map::new();
    for c in &controllers {
        let name = c.get("name").and_then(|x| x.as_str()).unwrap_or("");
        controller_options.insert(name.to_string(), c.get("options").cloned().unwrap_or(json!([])));
    }
    let global_names = m9a_string_list(interface.get("global_option"));
    let mut presets: Vec<Value> = Vec::new();
    if let Ok(rd) = fs::read_dir(base.join("tasks").join("preset")) {
        for e in rd.filter_map(|x| x.ok()) {
            let p = e.path();
            if !p.extension().and_then(|x| x.to_str()).map(|x| x.eq_ignore_ascii_case("json")).unwrap_or(false) { continue; }
            let Some(v) = read_json_loose(&p) else { continue; };
            for p in v.get("preset").and_then(|x| x.as_array()).into_iter().flatten() {
                let name = p.get("name").and_then(|x| x.as_str()).unwrap_or("");
                if name.is_empty() { continue; }
                let tasks: Vec<String> = p.get("task").and_then(|x| x.as_array()).map(|a| a.iter().filter_map(|t| {
                    if t.get("enabled").and_then(|x| x.as_bool()) == Some(false) { return None; }
                    t.get("name").and_then(|x| x.as_str()).map(String::from)
                }).collect()).unwrap_or_default();
                presets.push(json!({"name": name, "tasks": tasks}));
            }
        }
    }
    task_rows.sort_by(|a, b| a["name"].as_str().unwrap_or("").cmp(b["name"].as_str().unwrap_or("")));
    presets.sort_by(|a, b| a["name"].as_str().unwrap_or("").cmp(b["name"].as_str().unwrap_or("")));
    json!({
        "resources": resources.iter().map(|r| json!({"name": r["name"], "controllers": r["controllers"]})).collect::<Vec<_>>(),
        "controllers": controllers.iter().map(|c| c["name"].clone()).collect::<Vec<_>>(),
        "presets": presets,
        "tasks": task_rows,
        "resourceOptions": resource_options,
        "controllerOptions": controller_options,
        "globalOptions": m9a_defs_from_names(&defs, &global_names, &locale)
    })
}

/* ══ M9A 实例配置：白名单原位改写 ══ */

fn m9a_ci_key(obj: &serde_json::Map<String, Value>, key: &str) -> Option<String> {
    if obj.contains_key(key) { return Some(key.to_string()); }
    obj.keys().find(|k| k.eq_ignore_ascii_case(key)).cloned()
}

fn m9a_set_key(obj: &mut serde_json::Map<String, Value>, key: &str, val: Value) -> Result<(), String> {
    let actual = m9a_ci_key(obj, key).ok_or_else(|| format!("实例文件里没有 {key}（老配置？请先用 M9A 保存一次）"))?;
    obj.insert(actual, val);
    Ok(())
}

fn m9a_remove_keys(obj: &mut serde_json::Map<String, Value>, keys: &[&str]) {
    let victims: Vec<String> = obj.keys().filter(|k| keys.iter().any(|want| k.eq_ignore_ascii_case(want))).cloned().collect();
    for k in victims { obj.remove(&k); }
}

fn m9a_history_parts(key: &str) -> Option<(String, String)> {
    if key.len() > 512 || key.chars().any(|c| c.is_control()) { return None; }
    if key.matches("<|||>").count() == 1 {
        let (name, entry) = key.split_once("<|||>")?;
        return if !name.is_empty() && !entry.is_empty() { Some((name.to_string(), entry.to_string())) } else { None };
    }
    // 反编译真身只认恰好一个冒号的旧格式；带新分隔符但写法歧义时不猜。
    if key.contains('<') || key.matches(':').count() != 1 { return None; }
    let (name, entry) = key.split_once(':')?;
    if !name.is_empty() && !entry.is_empty() { Some((name.to_string(), entry.to_string())) } else { None }
}

fn m9a_catalog_task<'a>(catalogs: &'a Value, name: &str, entry: &str) -> Option<&'a Value> {
    catalogs.get("tasks")?.as_array()?.iter().find(|t| {
        t.get("name").and_then(|x| x.as_str()) == Some(name) && t.get("entry").and_then(|x| x.as_str()) == Some(entry)
    })
}

fn m9a_case_index(def: &Value, name: &str) -> Option<usize> {
    def.get("cases")?.as_array()?.iter().position(|c| c.get("name").and_then(|x| x.as_str()) == Some(name))
}

fn m9a_apply_selection(node: &mut Value, def: &Value, one: Option<&Value>, many: Option<&Value>) -> Result<(), String> {
    let obj = node.as_object_mut().ok_or_else(|| "选项节点不是对象".to_string())?;
    let name = def.get("name").and_then(|x| x.as_str()).unwrap_or("");
    let ty = def.get("type").and_then(|x| x.as_str()).unwrap_or("");
    match ty {
        "switch" | "select" => {
            let selected = one.and_then(|x| x.as_str()).ok_or_else(|| format!("{name}：只收一个 case 名"))?;
            let idx = m9a_case_index(def, selected).ok_or_else(|| format!("{name}：候选里没有 {selected}"))?;
            m9a_remove_keys(obj, &["selected_cases"]);
            obj.insert("index".to_string(), json!(idx));
            obj.insert("selected_case".to_string(), json!(selected));
        }
        "checkbox" => {
            let arr = many.and_then(|x| x.as_array()).ok_or_else(|| format!("{name}：复选框只收 case 名数组"))?;
            if arr.len() > 256 { return Err(format!("{name}：候选太多")); }
            let old = m9a_string_list(obj.get("selected_cases"));
            let mut requested = Vec::new();
            for x in arr {
                let s = x.as_str().ok_or_else(|| format!("{name}：case 名只能是文本"))?;
                if m9a_case_index(def, s).is_none() && !old.iter().any(|v| v == s) {
                    return Err(format!("{name}：候选里没有 {s}"));
                }
                if m9a_case_index(def, s).is_some() && !requested.iter().any(|v| v == s) {
                    requested.push(s.to_string());
                }
            }
            // 目录外的旧 case 只允许保留，不允许客户端新造；以最新磁盘值的顺序保留它们。
            let mut selected = Vec::new();
            for s in old {
                if m9a_case_index(def, &s).is_none() {
                    if !selected.iter().any(|v| v == &s) { selected.push(s); }
                } else if requested.iter().any(|v| v == &s) {
                    selected.push(s);
                }
            }
            for s in requested {
                if !selected.iter().any(|v| v == &s) { selected.push(s); }
            }
            m9a_remove_keys(obj, &["index", "selected_case"]);
            obj.insert("selected_cases".to_string(), json!(selected));
        }
        _ => return Err(format!("{name}：自由输入/未知类型不归面板编辑")),
    }
    Ok(())
}

fn m9a_upsert_node<'a>(arr: &'a mut Vec<Value>, name: &str) -> &'a mut Value {
    if let Some(i) = arr.iter().position(|n| n.get("name").and_then(|x| x.as_str()) == Some(name)) { return &mut arr[i]; }
    arr.push(json!({"name": name}));
    arr.last_mut().expect("just pushed")
}

fn m9a_apply_option_path(
    arr: &mut Vec<Value>,
    defs: &[Value],
    path: &[String],
    one: Option<&Value>,
    many: Option<&Value>,
) -> Result<(), String> {
    let first = path.first().ok_or_else(|| "选项路径是空的".to_string())?;
    let def = defs.iter().find(|d| d.get("name").and_then(|x| x.as_str()) == Some(first.as_str()))
        .ok_or_else(|| format!("目录里没有选项定义：{first}"))?;
    let node = m9a_upsert_node(arr, first);
    if path.len() == 1 {
        return m9a_apply_selection(node, def, one, many);
    }
    // 面板允许按显示出的默认分支直接改子项；落盘时把父项的有效默认选择显式物化。
    m9a_materialize_active_selection(node, def);
    let active = m9a_active_child_defs_with(def, node, defs).unwrap_or_default();
    let second = &path[1];
    if !active.iter().any(|d| d.get("name").and_then(|x| x.as_str()) == Some(second.as_str())) {
        return Err(format!("{first}：当前 case 没有激活子选项 {second}（先改父选项，再改子项）"));
    }
    let mut children = node.get("sub_options").and_then(|x| x.as_array()).cloned().unwrap_or_default();
    m9a_apply_option_path(&mut children, defs, &path[1..], one, many)?;
    node.as_object_mut().ok_or_else(|| "选项节点不是对象".to_string())?.insert("sub_options".to_string(), Value::Array(children));
    Ok(())
}

fn m9a_pretty(v: &Value, indent: &str, level: usize, nl: &str) -> String {
    match v {
        Value::Array(a) => {
            if a.is_empty() { return "[]".to_string(); }
            let pad = format!("{}{}", indent.repeat(level + 1), "");
            let mut s = String::from("[");
            s.push_str(nl);
            for (i, x) in a.iter().enumerate() {
                s.push_str(&pad);
                s.push_str(&m9a_pretty(x, indent, level + 1, nl));
                if i + 1 < a.len() { s.push(','); }
                s.push_str(nl);
            }
            s.push_str(&indent.repeat(level));
            s.push(']');
            s
        }
        Value::Object(o) => {
            if o.is_empty() { return "{}".to_string(); }
            let pad = format!("{}  ", indent.repeat(level));
            let mut s = String::from("{");
            s.push_str(nl);
            for (i, (k, x)) in o.iter().enumerate() {
                s.push_str(&pad);
                s.push_str(&serde_json::to_string(k).unwrap_or_default());
                s.push_str(": ");
                s.push_str(&m9a_pretty(x, indent, level + 1, nl));
                if i + 1 < o.len() { s.push(','); }
                s.push_str(nl);
            }
            s.push_str(&indent.repeat(level));
            s.push('}');
            s
        }
        _ => serde_json::to_string(v).unwrap_or_default(),
    }
}

fn m9a_format_value(v: &Value, original: &str, indent: &str) -> String {
    // 原值是单行就保持单行；原值跨行才按原行尾展开，避免往 CRLF 文件塞裸 LF。
    if (v.is_array() || v.is_object()) && original.contains('\n') {
        let nl = if original.contains("\r\n") { "\r\n" } else { "\n" };
        m9a_pretty(v, indent, 0, nl)
    } else {
        serde_json::to_string(v).unwrap_or_default()
    }
}

fn m9a_controller_value(name: &str) -> Result<u32, String> {
    match name.to_ascii_lowercase().as_str() {
        "none" => Ok(0),
        "win32" | "pc" => Ok(1),
        "adb" => Ok(2),
        "playcover" => Ok(4),
        "gamepad" => Ok(8),
        "macos" => Ok(16),
        "wlroots" => Ok(32),
        _ => Err(format!("控制器候选里没有 {name}")),
    }
}

/// 纯函数：只接受已由前端限定的 patch；整份 before == after 才允许落盘。
fn apply_m9a_instance_patch(
    text: &str,
    expected_name: &str,
    patch: &Value,
    catalogs: &Value,
) -> Result<(String, Vec<String>), String> {
    let before: Value = serde_json::from_str(text).map_err(|e| format!("实例配置解析失败：{e}"))?;
    if !before.is_object() { return Err("实例配置根节点不是对象".to_string()); }
    if s_ci(&before, "InstanceName") != expected_name { return Err("实例名已经变了：刷新后再编辑".to_string()); }
    let p = patch.as_object().ok_or_else(|| "保存内容不是对象".to_string())?;
    if let Some(name) = p.get("name").and_then(|x| x.as_str()) {
        if name != expected_name { return Err("保存身份与当前实例名不一致：刷新后再编辑".to_string()); }
    }
    for k in p.keys() {
        if !matches!(k.as_str(), "name" | "resource" | "controller" | "preset" | "currentTasks" | "optionEdits") {
            return Err(format!("这个字段不归面板改：{k}"));
        }
    }
    let mut after = before.clone();
    let mut changed: Vec<String> = Vec::new();
    let old_current = strs(ci(&before, "CurrentTasks"));

    if let Some(v) = p.get("resource") {
        let name = v.as_str().ok_or_else(|| "资源只收候选名".to_string())?;
        let ok = catalogs.get("resources").and_then(|x| x.as_array()).map(|a| a.iter().any(|r| r.get("name").and_then(|x| x.as_str()) == Some(name))).unwrap_or(false);
        if !ok { return Err(format!("资源候选里没有 {name}")); }
        m9a_set_key(after.as_object_mut().unwrap(), "Resource", json!(name))?;
        changed.push("Resource".to_string());
    }
    if let Some(v) = p.get("controller") {
        let name = v.as_str().ok_or_else(|| "控制器只收候选名".to_string())?;
        let ok = catalogs.get("controllers").and_then(|x| x.as_array()).map(|a| a.iter().any(|c| c.as_str() == Some(name))).unwrap_or(false);
        if !ok { return Err(format!("控制器候选里没有 {name}")); }
        let num = m9a_controller_value(name)?;
        let obj = after.as_object_mut().unwrap();
        m9a_set_key(obj, "CurrentControllerName", json!(name))?;
        m9a_set_key(obj, "CurrentController", json!(num))?;
        changed.extend(["CurrentControllerName".to_string(), "CurrentController".to_string()]);
    }
    let resource = s_ci(&after, "Resource");
    let controller = s_ci(&after, "CurrentControllerName");
    if !resource.is_empty() && !controller.is_empty() {
        if let Some(r) = catalogs.get("resources").and_then(|x| x.as_array()).and_then(|a| a.iter().find(|r| r.get("name").and_then(|x| x.as_str()) == Some(&resource))) {
            let supported = m9a_string_list(r.get("controllers"));
            if !supported.is_empty() && !supported.iter().any(|x| x.eq_ignore_ascii_case(&controller)) {
                return Err(format!("资源 {resource} 不支持控制器 {controller}"));
            }
        }
    }
    if let Some(v) = p.get("preset") {
        let name = v.as_str().ok_or_else(|| "预设只收候选名".to_string())?;
        if !name.is_empty() {
            let ok = catalogs.get("presets").and_then(|x| x.as_array()).map(|a| a.iter().any(|x| x.get("name").and_then(|n| n.as_str()) == Some(name))).unwrap_or(false);
            if !ok { return Err(format!("预设候选里没有 {name}")); }
        }
        m9a_set_key(after.as_object_mut().unwrap(), "InstancePresetKey", json!(name))?;
        changed.push("InstancePresetKey".to_string());
    }

    let current = if let Some(v) = p.get("currentTasks") {
        let arr = v.as_array().ok_or_else(|| "任务队列只收字符串数组".to_string())?;
        if arr.len() > 512 { return Err("任务队列最多 512 项".to_string()); }
        let mut out: Vec<String> = Vec::new();
        let mut selected_names: Vec<String> = Vec::new();
        for x in arr {
            let key = x.as_str().ok_or_else(|| "任务队列里只能放文本".to_string())?;
            let parts = m9a_history_parts(key).ok_or_else(|| "任务历史格式有歧义：只认 名称<|||>entry 或恰好一个冒号的旧格式".to_string())?;
            if m9a_catalog_task(catalogs, &parts.0, &parts.1).is_some() {
                if !selected_names.iter().any(|n| n == &parts.0) { selected_names.push(parts.0.clone()); }
                out.push(format!("{}<|||>{}", parts.0, parts.1));
            } else if old_current.iter().any(|old| old == key) {
                // 已存在但目录里没有的旧任务原样保留；不允许借保存新增未知任务。
                if let Some((n, _)) = m9a_history_parts(key) { if !selected_names.iter().any(|x| x == &n) { selected_names.push(n); } }
                out.push(format!("{}<|||>{}", parts.0, parts.1));
            } else {
                return Err(format!("目录里没有这个任务：{}{}", parts.0, if parts.1.is_empty() { "" } else { " / " }));
            }
        }
        let obj = after.as_object_mut().unwrap();
        m9a_set_key(obj, "CurrentTasks", Value::Array(out.iter().map(|s| json!(s)).collect()))?;
        let known: Vec<String> = catalogs.get("tasks").and_then(|x| x.as_array()).map(|a| a.iter().filter_map(|t| t.get("name").and_then(|x| x.as_str()).map(String::from)).collect()).unwrap_or_default();
        let mut deleted = strs(ci(&before, "DeletedTaskNames"));
        deleted.retain(|n| !known.iter().any(|k| k == n));
        for n in &known { if !selected_names.iter().any(|s| s == n) && !deleted.iter().any(|d| d == n) { deleted.push(n.clone()); } }
        m9a_set_key(obj, "DeletedTaskNames", json!(deleted))?;
        let mut missing = ci(&before, "MissingTaskSessions").and_then(|x| x.as_object()).cloned().unwrap_or_default();
        let victims: Vec<String> = missing.keys().filter(|k| known.iter().any(|n| n == *k)).cloned().collect();
        for k in victims { missing.remove(&k); }
        m9a_set_key(obj, "MissingTaskSessions", Value::Object(missing))?;
        m9a_set_key(obj, "KeepEmptyTaskQueue", json!(out.is_empty()))?;
        changed.extend(["CurrentTasks".to_string(), "DeletedTaskNames".to_string(), "MissingTaskSessions".to_string(), "KeepEmptyTaskQueue".to_string()]);
        out
    } else { old_current.clone() };

    if let Some(v) = p.get("optionEdits") {
        let edits = v.as_array().ok_or_else(|| "选项改动只收数组".to_string())?;
        if edits.len() > 512 { return Err("一次最多改 512 个选项".to_string()); }
        for e in edits {
            let source = e.get("source").and_then(|x| x.as_str()).unwrap_or("");
            let path: Vec<String> = e.get("path").and_then(|x| x.as_array()).ok_or_else(|| "选项改动缺 path".to_string())?
                .iter().map(|x| x.as_str().map(String::from).ok_or_else(|| "选项路径只能放文本".to_string())).collect::<Result<_, _>>()?;
            if path.is_empty() || path.len() > 16 { return Err("选项路径层数不合法".to_string()); }
            let one = e.get("selected_case");
            let many = e.get("selected_cases");
            match source {
                "task" => {
                    let idx = e.get("task").and_then(|x| x.as_u64()).ok_or_else(|| "任务选项缺 task 下标".to_string())? as usize;
                    let key = current.get(idx).ok_or_else(|| "任务选项下标越界（队列刚变过？）")?;
                    let (name, _) = m9a_history_parts(key).ok_or_else(|| "当前任务历史格式不认识".to_string())?;
                    let defs = m9a_task_defs(catalogs, &name);
                    let mut items = ci(&after, "TaskItems").and_then(|x| x.as_array()).cloned().unwrap_or_default();
                    if !items.iter().any(|it| it.get("name").and_then(|x| x.as_str()) == Some(&name)) {
                        items.push(json!({"name": name}));
                    }
                    let item = items.iter_mut().find(|it| it.get("name").and_then(|x| x.as_str()) == Some(&name)).unwrap();
                    let mut nodes = item.get("option").and_then(|x| x.as_array()).cloned().unwrap_or_default();
                    m9a_apply_option_path(&mut nodes, &defs, &path, one, many)?;
                    item.as_object_mut().unwrap().insert("option".to_string(), Value::Array(nodes));
                    m9a_set_key(after.as_object_mut().unwrap(), "TaskItems", Value::Array(items))?;
                }
                "resource" | "controller" => {
                    let root_key = if source == "resource" { "ResourceOptionItems" } else { "ControllerOptionItems" };
                    let scope = if source == "resource" { &resource } else { &controller };
                    if scope.is_empty() { return Err(format!("当前没有可编辑的{}", source)); }
                    let defs = m9a_named_defs(catalogs, if source == "resource" { "resourceOptions" } else { "controllerOptions" }, scope);
                    let mut groups = ci(&after, root_key).and_then(|x| x.as_object()).cloned().unwrap_or_default();
                    let mut nodes = groups.get(scope).and_then(|x| x.as_array()).cloned().unwrap_or_default();
                    m9a_apply_option_path(&mut nodes, &defs, &path, one, many)?;
                    groups.insert(scope.clone(), Value::Array(nodes));
                    m9a_set_key(after.as_object_mut().unwrap(), root_key, Value::Object(groups))?;
                }
                "global" => {
                    let defs = catalogs.get("globalOptions").and_then(|x| x.as_array()).cloned().unwrap_or_default();
                    let mut nodes = ci(&after, "GlobalOptionItems").and_then(|x| x.as_array()).cloned().unwrap_or_default();
                    m9a_apply_option_path(&mut nodes, &defs, &path, one, many)?;
                    m9a_set_key(after.as_object_mut().unwrap(), "GlobalOptionItems", Value::Array(nodes))?;
                }
                _ => return Err("选项来源只收 task/resource/controller/global".to_string()),
            }
            let label = if source == "task" { format!("TaskItems[{}]{}", e.get("task").and_then(|x| x.as_u64()).unwrap_or(0), path.join(".")) } else { format!("{source}:{}", path.join(".")) };
            if !changed.iter().any(|x| x == &label) { changed.push(label); }
        }
    }
    if after == before { return Ok((text.to_string(), Vec::new())); }
    let top = root_object_start(text).ok_or_else(|| "定位不到实例配置根对象".to_string())?;
    let mut plans: Vec<(usize, usize, String)> = Vec::new();
    let mut planned_roots: Vec<String> = Vec::new();
    for key in &changed {
        let root_key = if key.starts_with("TaskItems") { "TaskItems" }
            else if key.starts_with("resource:") { "ResourceOptionItems" }
            else if key.starts_with("controller:") { "ControllerOptionItems" }
            else if key.starts_with("global:") { "GlobalOptionItems" }
            else { key.as_str() };
        if planned_roots.iter().any(|k| k == root_key) { continue; }
        planned_roots.push(root_key.to_string());
        let val = ci(&after, root_key).ok_or_else(|| format!("改后缺少 {root_key}"))?;
        let (vs, ve) = member_span(text, top, root_key).ok_or_else(|| format!("定位不到 {root_key}，面板不敢改"))?;
        plans.push((vs, ve, m9a_format_value(val, &text[vs..ve], &line_indent(&text, vs))));
    }
    let mut out = text.to_string();
    plans.sort_by(|a, b| b.0.cmp(&a.0));
    for (vs, ve, rep) in plans { out.replace_range(vs..ve, &rep); }
    let got: Value = serde_json::from_str(&out).map_err(|e| format!("改后的实例配置解析失败（{e}），文件未动"))?;
    if got != after { return Err("改后文本与预期配置不一致，文件未动".to_string()); }
    Ok((out, changed))
}

pub fn m9a_save_instance(id: String, name: String, patch: Value) -> Result<Value, String> {
    let _write_guard = write_lock(&SETTINGS_WRITE_LOCK)?;
    let id = id.trim().to_string();
    let name = name.trim().to_string();
    if id.is_empty() || id.len() > 64 || !id.chars().all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_') {
        return Err("实例 id 不合法（实例文件名只收字母数字、- 与 _）".to_string());
    }
    if name.is_empty() || name.chars().count() > 64 || name.chars().any(|c| c.is_control()) {
        return Err("实例名不合法".to_string());
    }
    let root = root()?;
    let settings = read_settings(&root)?.ok_or_else(|| "config/settings.json 不在：先跑 setup.ps1".to_string())?;
    let paths = ci(&settings, "paths").cloned().unwrap_or(json!({}));
    let dir = s_ci(&paths, "m9aDir");
    if dir.is_empty() { return Err("settings.paths.m9aDir 没配".to_string()); }
    let exe_name = stem(&s_ci(&paths, "m9aExe"));
    if !exe_name.is_empty() && crate::live::any_running(&[exe_name.as_str()]) {
        return Err("M9A 正在运行：先退出 M9A 再保存（它退出时会按内存回写整份配置）".to_string());
    }
    let inst_dir = join_rel(&dir, &s_ci(&paths, "m9aInstancesDir"));
    let path = inst_dir.join(format!("{id}.json"));
    if !path.is_file() { return Err("实例文件不在：刷新后再试".to_string()); }
    let raw = fs::read(&path).map_err(|e| scrub(&format!("读实例配置失败：{e}")))?;
    let had_bom = raw.starts_with(&[0xEF, 0xBB, 0xBF]);
    let body = if had_bom { &raw[3..] } else { &raw[..] };
    let text = std::str::from_utf8(body).map_err(|_| "实例配置不是 UTF-8".to_string())?.to_string();
    let catalogs = m9a_catalogs(&dir);
    let (out, changed) = apply_m9a_instance_patch(&text, &name, &patch, &catalogs)?;
    if changed.is_empty() { return Ok(json!({"ok": true, "changed": [], "note": "跟文件里一致，没写盘"})); }

    let backup = backup(&root, &path).ok_or_else(|| "写前备份失败：拒绝改写实例配置".to_string())?;
    let mut bytes = Vec::new();
    if had_bom { bytes.extend_from_slice(&[0xEF, 0xBB, 0xBF]); }
    bytes.extend_from_slice(out.as_bytes());
    fs::write(&path, &bytes).map_err(|e| scrub(&format!("写实例配置失败：{e}")))?;
    let verify_raw = fs::read(&path).map_err(|e| scrub(&format!("写后回读失败：{e}")))?;
    let verify_body = if verify_raw.starts_with(&[0xEF, 0xBB, 0xBF]) { &verify_raw[3..] } else { &verify_raw[..] };
    let verify: Value = serde_json::from_slice(verify_body).map_err(|e| format!("写后配置解析失败：{e}"))?;
    let expected: Value = serde_json::from_str(&out).map_err(|e| format!("写后预期解析失败：{e}"))?;
    if verify != expected || verify_raw != bytes {
        return Err("写盘后回读不一致：请从 backups/ 恢复上一份，并把改动交给 M9A 手动做".to_string());
    }
    Ok(json!({"ok": true, "changed": changed, "backup": backup,
        "note": "已原位写入实例配置（任务队列四字段同步；只动白名单值，已回读对平）"}))
}

/* ══ 真操作（全部走核内 verb，本件不判） ══ */

pub fn maa_close() -> Result<Value, String> {
    let root = root()?;
    let d = call(&root, "stop-maa-and-game", &json!({}), 180)?;
    let maa = i_ci(&d, "MaaClosed");
    let ark = i_ci(&d, "ArkClosed");
    Ok(json!({
        "ok": true, "maaClosed": maa, "arkClosed": ark, "lines": lines_of(&d),
        "note": if maa + ark == 0 {
            "内核按 runtime.quitAssistantAfter / closeArknightsAfter 办；这两档关着时它不动手（本次关掉 0 个进程）"
        } else { "" }
    }))
}

pub fn ark_start() -> Result<Value, String> {
    let root = root()?;
    let d = call(&root, "start-ark-client", &json!({}), 1500)?;
    let started = d.get("Started").and_then(|x| x.as_bool()).unwrap_or(false);
    let title = d
        .get("Window")
        .and_then(|w| w.get("Title"))
        .and_then(|x| x.as_str())
        .map(|s| scrub(s))
        .unwrap_or_default();
    Ok(json!({
        "ok": true, "started": started, "window": title, "lines": lines_of(&d),
        "note": if started { "" } else if title.is_empty() { "窗口没等到：可能在登录/更新，或端游路径不对" } else { "" }
    }))
}

pub fn m9a_stop() -> Result<Value, String> {
    let root = root()?;
    let d = call(&root, "stop-m9a", &json!({}), 150)?;
    let closed = i_ci(&d, "M9aClosed");
    Ok(json!({
        "ok": true, "m9aClosed": closed, "lines": lines_of(&d),
        "note": if closed == 0 { "内核按 runtime.quitAssistantAfter 办；这一档关着时它不动手（本次 0 个进程）" } else { "" }
    }))
}

pub fn mumu_start() -> Result<Value, String> {
    let root = root()?;
    let d = call(&root, "start-mumu", &json!({}), 420)?;
    let started = d.get("Started").and_then(|x| x.as_bool()).unwrap_or(false);
    Ok(json!({
        "ok": true, "started": started, "vm": s_ci(&d, "Name"), "lines": lines_of(&d),
        "note": if started { "" } else { "等了很久没看到安卓系统启动：看 MuMu 自己弹了什么（占用/目录）" }
    }))
}

pub fn mumu_stop() -> Result<Value, String> {
    let root = root()?;
    let d = call(&root, "stop-mumu", &json!({}), 180)?;
    Ok(json!({"ok": true, "lines": lines_of(&d)}))
}

pub fn m9a_switch(id: String, name: String) -> Result<Value, String> {
    let _write_guard = write_lock(&SETTINGS_WRITE_LOCK)?;
    let id = id.trim().to_string();
    let name = name.trim().to_string();
    if id.is_empty()
        || id.len() > 64
        || !id.chars().all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
    {
        return Err("实例 id 不合法（实例文件名只收字母数字、- 与 _）".to_string());
    }
    if name.is_empty() || name.chars().count() > 64 || name.chars().any(|c| c.is_control()) {
        return Err("实例名不合法".to_string());
    }
    let root = root()?;
    let d = call(
        &root,
        "apply-m9a-instance",
        &json!({"instance": {"Id": id, "Name": name}, "apply": true}),
        90,
    )?;
    Ok(json!({
        "ok": true,
        "applied": d.get("Applied").and_then(|x| x.as_bool()).unwrap_or(false),
        "changes": scrub_lines(strs(d.get("Changes"))),
        "lines": lines_of(&d)
    }))
}

pub fn m9a_update() -> Result<Value, String> {
    let root = root()?;
    let d = call(&root, "run-m9a-update", &json!({}), 2500)?;
    let code = d.get("Code").and_then(|x| x.as_i64()).unwrap_or(-1);
    let line = scrub(d.get("Line").and_then(|x| x.as_str()).unwrap_or(""));
    let joined = scrub(d.get("Joined").and_then(|x| x.as_str()).unwrap_or(""));
    let mut tail: Vec<String> = joined
        .split(" / ")
        .filter(|x| !x.trim().is_empty())
        .map(|x| x.to_string())
        .collect();
    if tail.len() > 10 {
        tail.drain(0..tail.len() - 10);
    }
    Ok(json!({"ok": code == 0, "code": code, "line": line, "lines": tail,
        "note": if code == 0 { "" } else { "更新工具非 0 退出：看下面几行（没联网 / GitHub 不通 / 装包被占）" }}))
}

/* ══ 日常计划：白名单键的行内改写 ══ */

#[derive(Clone, Copy, PartialEq)]
enum Kind {
    Bool,
    Int,
    Str,
    StrList,
}

struct Spec {
    section: &'static str,
    sub: Option<&'static str>,
    key: &'static str,
    kind: Kind,
}

fn spec_for(path: &str) -> Option<Spec> {
    use Kind::*;
    let (section, sub, key, kind) = match path {
        "ark.defaultStage" => ("ark", None, "defaultStage", Str),
        "ark.finishAllSanityWith" => ("ark", None, "finishAllSanityWith", Str),
        "ark.keepQueuesEnabled" => ("ark", None, "keepQueuesEnabled", StrList),
        "ark.disableQueues" => ("ark", None, "disableQueues", StrList),
        "ark.farmAppend" => ("ark", None, "farmAppend", Bool),
        "ark.farmMaxTimesPerRound" => ("ark", None, "farmMaxTimesPerRound", Int),
        "ark.alsoEnableMainFight" => ("ark", None, "alsoEnableMainFight", Bool),
        "runtime.retryOnce" => ("runtime", None, "retryOnce", Bool),
        "runtime.arkVisionRescue" => ("runtime", None, "arkVisionRescue", Bool),
        "runtime.mumuVisionRescue" => ("runtime", None, "mumuVisionRescue", Bool),
        "runtime.visionAllowReward" => ("runtime", None, "visionAllowReward", Bool),
        "runtime.supervisor" => ("runtime", None, "supervisor", Bool),
        "runtime.keepScreenOn" => ("runtime", None, "keepScreenOn", Bool),
        "notify.toast" => ("notify", None, "toast", Bool),
        "runtime.abortOnMaaStallWarning" => ("runtime", None, "abortOnMaaStallWarning", Bool),
        "runtime.quitAssistantAfter" => ("runtime", None, "quitAssistantAfter", Bool),
        "runtime.closeArknightsAfter" => ("runtime", None, "closeArknightsAfter", Bool),
        "runtime.launchArkFromHelper" => ("runtime", None, "launchArkFromHelper", Bool),
        "runtime.m9aCliArgs" => ("runtime", None, "m9aCliArgs", Bool),
        "runtime.m9aForceStart" => ("runtime", None, "m9aForceStart", Bool),
        "runtime.closeEmulatorAfter" => ("runtime", None, "closeEmulatorAfter", Bool),
        "runtime.takeOverAssistant" => ("runtime", None, "takeOverAssistant", Bool),
        "re1999.mainStoryRequiresAck" => ("re1999", None, "mainStoryRequiresAck", Bool),
        "re1999.shopDoneStopsEvent" => ("re1999", None, "shopDoneStopsEvent", Bool),
        "re1999.planToInstance.idle" => ("re1999", Some("planToInstance"), "idle", Str),
        "re1999.planToInstance.event" => ("re1999", Some("planToInstance"), "event", Str),
        "re1999.planToInstance.rerun" => ("re1999", Some("planToInstance"), "rerun", Str),
        "mumu.vmIndex" => ("mumu", None, "vmIndex", Str),
        _ => return None,
    };
    Some(Spec { section, sub, key, kind })
}

fn coerce(kind: Kind, v: &Value) -> Result<Value, String> {
    match kind {
        Kind::Bool => v.as_bool().map(Value::Bool).ok_or_else(|| "只收 true/false".to_string()),
        Kind::Int => {
            let n = v.as_i64().ok_or_else(|| "只收整数".to_string())?;
            if !(1..=30).contains(&n) {
                return Err("数值超出范围（1~30）".to_string());
            }
            Ok(json!(n))
        }
        Kind::Str => {
            let s = v.as_str().ok_or_else(|| "只收字符串".to_string())?;
            if s.chars().count() > 120 || s.chars().any(|c| c.is_control()) {
                return Err("字符串太长或含控制字符".to_string());
            }
            Ok(Value::String(s.to_string()))
        }
        Kind::StrList => {
            let a = v.as_array().ok_or_else(|| "只收字符串数组".to_string())?;
            if a.len() > 32 {
                return Err("名单最多 32 项".to_string());
            }
            let mut out = Vec::new();
            for x in a {
                let s = x.as_str().ok_or_else(|| "名单里只能放字符串".to_string())?;
                let s = s.trim();
                if s.is_empty() {
                    continue;
                }
                if s.chars().count() > 60 || s.chars().any(|c| c.is_control()) {
                    return Err("名单项太长或含控制字符".to_string());
                }
                out.push(Value::String(s.to_string()));
            }
            Ok(Value::Array(out))
        }
    }
}

fn coerce_plan_value(key: &str, kind: Kind, current: &Value, input: &Value) -> Result<Value, String> {
    if key == "ark.farmMaxTimesPerRound" {
        let old = current.as_i64().ok_or_else(|| "settings.json 里 ark.farmMaxTimesPerRound 不是整数；面板不猜默认值，也不覆盖".to_string())?;
        // 旧 0 只允许原样保留；内核实际按 1 次，不补刷应关闭 farmAppend。
        if input.as_i64() == Some(0) && old == 0 { return Ok(json!(0)); }
    }
    coerce(kind, input)
}

fn get_path<'a>(v: &'a Value, path: &str) -> Option<&'a Value> {
    let mut cur = v;
    for k in path.split('.') {
        cur = cur.as_object()?.get(k)?;
    }
    Some(cur)
}

fn set_path(cur: &mut Value, path: &str, val: Value) -> Result<(), String> {
    let parts: Vec<&str> = path.split('.').collect();
    let mut node = cur;
    for (i, k) in parts.iter().enumerate() {
        let obj = node.as_object_mut().ok_or_else(|| "settings 这一段不是对象".to_string())?;
        if i + 1 == parts.len() {
            obj.insert(k.to_string(), val);
            return Ok(());
        }
        node = obj.get_mut(*k).ok_or_else(|| format!("settings 里没有 {k} 这一段"))?;
    }
    Ok(())
}

/* —— 文本定位：只按"这个键的那一段值"取字节区间（字符串感知、括号配平） —— */

fn skip_str(b: &[u8], i: usize) -> usize {
    let mut j = i + 1;
    let mut esc = false;
    while j < b.len() {
        let c = b[j];
        if esc {
            esc = false;
        } else if c == b'\\' {
            esc = true;
        } else if c == b'"' {
            return j + 1;
        }
        j += 1;
    }
    b.len()
}

fn skip_container(b: &[u8], i: usize) -> usize {
    let open = b[i];
    let close = if open == b'{' { b'}' } else { b']' };
    let mut depth = 0i32;
    let mut j = i;
    while j < b.len() {
        match b[j] {
            b'"' => {
                j = skip_str(b, j);
                continue;
            }
            c if c == open => depth += 1,
            c if c == close => {
                depth -= 1;
                if depth == 0 {
                    return j + 1;
                }
            }
            _ => {}
        }
        j += 1;
    }
    b.len()
}

fn skip_ws(b: &[u8], mut i: usize) -> usize {
    while i < b.len() && (b[i] as char).is_whitespace() {
        i += 1;
    }
    i
}

fn skip_value(b: &[u8], i: usize) -> usize {
    let j = skip_ws(b, i);
    if j >= b.len() {
        return j;
    }
    match b[j] {
        b'"' => skip_str(b, j),
        b'{' | b'[' => skip_container(b, j),
        _ => {
            let mut k = j;
            while k < b.len()
                && !matches!(b[k], b',' | b'}' | b']')
                && !(b[k] as char).is_whitespace()
            {
                k += 1;
            }
            k
        }
    }
}

/// 在 obj_start（必须指向 '{'）这个对象里找 key，回它的值区间 [start,end)。
fn member_span(text: &str, obj_start: usize, key: &str) -> Option<(usize, usize)> {
    let b = text.as_bytes();
    if b.get(obj_start) != Some(&b'{') {
        return None;
    }
    let end = skip_container(b, obj_start);
    let stop = end.saturating_sub(1);
    let mut i = obj_start + 1;
    while i < stop {
        i = skip_ws(b, i);
        while i < stop && b[i] == b',' {
            i = skip_ws(b, i + 1);
        }
        if i >= stop || b[i] != b'"' {
            break;
        }
        let ks = i + 1;
        let ke = skip_str(b, i) - 1;
        let k = &text[ks..ke];
        i = skip_str(b, i);
        i = skip_ws(b, i);
        if i < b.len() && b[i] == b':' {
            i += 1;
        }
        let vs = skip_ws(b, i);
        let ve = skip_value(b, vs);
        if k == key {
            return Some((vs, ve));
        }
        i = ve;
    }
    None
}

fn root_object_start(text: &str) -> Option<usize> {
    let b = text.as_bytes();
    let i = skip_ws(b, 0);
    if b.get(i) == Some(&b'{') {
        Some(i)
    } else {
        None
    }
}

fn value_span(text: &str, spec: &Spec) -> Option<(usize, usize)> {
    let top = root_object_start(text)?;
    let (ss, _) = member_span(text, top, spec.section)?;
    match spec.sub {
        None => member_span(text, ss, spec.key),
        Some(sub) => {
            let (sub_start, _) = member_span(text, ss, sub)?;
            member_span(text, sub_start, spec.key)
        }
    }
}

fn line_indent(text: &str, pos: usize) -> String {
    let start = text[..pos].rfind('\n').map(|i| i + 1).unwrap_or(0);
    text[start..pos].chars().take_while(|c| *c == ' ' || *c == '\t').collect()
}

fn format_value(v: &Value, original: &str, indent: &str) -> String {
    match v {
        Value::Array(a) if original.contains('\n') => {
            if a.is_empty() {
                return "[]".to_string();
            }
            // 行尾跟着原文件走：CRLF 文件里塞进裸 LF 会让整份文件行尾混掉。
            let nl = if original.contains("\r\n") { "\r\n" } else { "\n" };
            let item_indent = format!("{indent}  ");
            let mut s = String::from("[");
            s.push_str(nl);
            for (n, item) in a.iter().enumerate() {
                s.push_str(&item_indent);
                s.push_str(&serde_json::to_string(item).unwrap_or_default());
                if n + 1 < a.len() {
                    s.push(',');
                }
                s.push_str(nl);
            }
            s.push_str(indent);
            s.push(']');
            s
        }
        _ => serde_json::to_string(v).unwrap_or_default(),
    }
}

pub fn save_plan(edits: Vec<Value>) -> Result<Value, String> {
    let _write_guard = write_lock(&SETTINGS_WRITE_LOCK)?;
    save_plan_locked(&root()?, edits)
}

// 调用方必须持有 SETTINGS_WRITE_LOCK；从旧值读取到写后回读全程不能释放。
fn save_plan_locked(root: &Path, edits: Vec<Value>) -> Result<Value, String> {
    let path = settings_path(root);
    if !path.is_file() {
        return Err("config/settings.json 不在：先跑 setup.ps1 生成配置".to_string());
    }
    let raw = fs::read(&path).map_err(|e| format!("读 settings.json 失败：{e}"))?;
    let had_bom = raw.starts_with(&[0xEF, 0xBB, 0xBF]);
    let body = if had_bom { &raw[3..] } else { &raw[..] };
    let text = std::str::from_utf8(body)
        .map_err(|_| "settings.json 不是 UTF-8".to_string())?
        .to_string();
    let before: Value = serde_json::from_str(&text).map_err(|e| format!("settings.json 解析失败：{e}"))?;

    let mut after = before.clone();
    let mut plans: Vec<(usize, usize, String, String)> = Vec::new();
    let mut seen: Vec<String> = Vec::new();
    for e in edits {
        let key = e
            .get("key")
            .and_then(|x| x.as_str())
            .ok_or_else(|| "改动项缺 key".to_string())?
            .to_string();
        if seen.iter().any(|k| k == &key) {
            return Err(format!("同一项出现两次：{key}"));
        }
        seen.push(key.clone());
        let spec = spec_for(&key).ok_or_else(|| format!("面板不改这个键：{key}"))?;
        let current = get_path(&before, &key).ok_or_else(|| {
            format!("settings.json 里没有 {key} 这一项（老配置？跑 setup.ps1 或手动补一行再回来）")
        })?;
        let input = e.get("value").unwrap_or(&Value::Null);
        let val = coerce_plan_value(&key, spec.kind, current, input)
            .map_err(|m| format!("{key}：{m}"))?;
        if spec.kind == Kind::Bool && !current.is_boolean() {
            return Err(format!("settings.json 里 {key} 不是布尔值；面板不猜默认值，也不覆盖"));
        }
        set_path(&mut after, &key, val.clone())?;
        let (vs, ve) = value_span(&text, &spec)
            .ok_or_else(|| format!("定位不到 {key} 的那一段，面板不敢改（请手动改）"))?;
        let rep = format_value(&val, &text[vs..ve], &line_indent(&text, vs));
        plans.push((vs, ve, rep, key));
    }
    if plans.is_empty() {
        return Ok(json!({"ok": true, "changed": [], "note": "没有改动"}));
    }
    if after == before {
        return Ok(json!({"ok": true, "changed": [], "backup": "", "note": "跟文件里一致，没写盘"}));
    }

    let mut out = text.clone();
    plans.sort_by(|a, b| b.0.cmp(&a.0));
    for (vs, ve, rep, _) in &plans {
        out.replace_range(*vs..*ve, rep);
    }
    let got: Value = serde_json::from_str(&out).map_err(|e| {
        format!("改后的 settings.json 解析失败（{e}）——面板不敢写，文件一个字节没动")
    })?;
    if got != after {
        return Err("改后的文本回读跟预期的新值对不上——面板不敢写，文件一个字节没动".to_string());
    }

    let backup = backup(root, &path).ok_or_else(|| "写前备份失败：拒绝改写 settings.json".to_string())?;
    let mut bytes = Vec::new();
    if had_bom {
        bytes.extend_from_slice(&[0xEF, 0xBB, 0xBF]);
    }
    bytes.extend_from_slice(out.as_bytes());
    fs::write(&path, &bytes).map_err(|e| format!("写 settings.json 失败：{e}"))?;

    let verify = read_settings(root)?.ok_or_else(|| "写完后 settings.json 不见了".to_string())?;
    if verify != after {
        return Err("写盘后回读不一致：请从 backups/ 里把上一份拷回来，并把这一项手动改".to_string());
    }
    let changed: Vec<String> = plans.into_iter().map(|(_, _, _, k)| k).collect();
    Ok(json!({
        "ok": true,
        "changed": changed,
        "backup": backup,
        "note": "已写入 config/settings.json（只动这几项的值；其余字节原样）"
    }))
}

fn civil_from_days(z: i64) -> (i64, u32, u32) {
    let z = z + 719468;
    let era = if z >= 0 { z } else { z - 146096 } / 146097;
    let doe = (z - era * 146097) as u64;
    let yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
    let y = yoe as i64 + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = (doy - (153 * mp + 2) / 5 + 1) as u32;
    let m = if mp < 10 { mp + 3 } else { mp - 9 } as u32;
    (y + if m <= 2 { 1 } else { 0 }, m, d)
}

fn stamp() -> String {
    let secs = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs() as i64 + 8 * 3600)
        .unwrap_or(0);
    let days = secs.div_euclid(86400);
    let sod = secs.rem_euclid(86400);
    let (y, m, d) = civil_from_days(days);
    format!("{:04}{:02}{:02}-{:02}{:02}{:02}", y, m, d, sod / 3600, (sod % 3600) / 60, sod % 60)
}

/// 改前备份 + 每文件留 20 份（与 panel-cs 的 BackupSettings 同规矩）。
/// 备份名沿用原文件名：settings.json.<时间戳>.bak / gui.new.json.<时间戳>.bak，互不挤占配额。
fn backup(root: &Path, path: &Path) -> Option<String> {
    let dir = root.join("backups");
    fs::create_dir_all(&dir).ok()?;
    let base = path
        .file_name()
        .and_then(|s| s.to_str())
        .filter(|s| !s.is_empty())
        .unwrap_or("settings.json");
    let mut name = format!("{base}.{}.bak", stamp());
    let mut dest = dir.join(&name);
    let mut n = 1;
    while dest.exists() {
        name = format!("{base}.{}-{}.bak", stamp(), n);
        dest = dir.join(&name);
        n += 1;
    }
    fs::copy(path, &dest).ok()?;
    let prefix = format!("{base}.");
    let mut olds: Vec<(SystemTime, PathBuf)> = fs::read_dir(&dir)
        .ok()?
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| {
            let f = p.file_name().and_then(|s| s.to_str()).unwrap_or("");
            f.starts_with(&prefix) && f.ends_with(".bak") && p != &dest
        })
        .filter_map(|p| fs::metadata(&p).ok().and_then(|m| m.modified().ok()).map(|t| (t, p)))
        .collect();
    olds.sort_by(|a, b| b.0.cmp(&a.0));
    for (_, p) in olds.into_iter().skip(19) {
        let _ = fs::remove_file(p);
    }
    Some(name)
}

/* ══ 自检：不碰真配置、不起任何真实进程 ══ */

#[cfg(test)]
mod selftest {
    use super::*;

    #[test]
    fn scrub_hides_abs_paths() {
        assert_eq!(scrub(r"启动端游：C:\Games\ark.exe 后等窗口"), "启动端游：<本地路径> 后等窗口");
        assert_eq!(scrub(r"\\nas\share\x.json 读不到"), "<本地路径> 读不到");
        assert_eq!(scrub("D:/a/b/c 与版本 v4.14.1"), "<本地路径> 与版本 v4.14.1");
        assert_eq!(scrub("没有路径的一句话"), "没有路径的一句话");
    }

    #[test]
    fn mumu_vms_only_returns_scrubbed_index_and_name() {
        let d = json!({"Vms": [
            {"Index": "0", "Name": "主力机", "Path": "C:\\private\\vm"},
            {"Index": "12", "Name": "D:\\private\\别名"},
            {"Index": "13", "Name": "D:\\Program Files\\MuMu\\Private"},
            {"Index": "bad", "Name": "不应返回"}
        ], "Raw": "sensitive"});
        assert_eq!(mumu_vms_from(&d), vec![
            json!({"index": "0", "name": "主力机"}),
            json!({"index": "12", "name": "<本地路径>"}),
            json!({"index": "13", "name": "<本地路径>"})
        ]);
        assert!(mumu_vms_from(&json!({})).is_empty());
        assert!(mumu_vms_from(&json!({"Vms": null})).is_empty());
    }

    #[test]
    fn whitelist_is_explicit() {
        assert!(spec_for("ark.defaultStage").is_some());
        assert!(spec_for("ark.alsoEnableMainFight").is_some());
        assert!(spec_for("re1999.planToInstance.event").is_some());
        assert!(spec_for("runtime.visionApiKey").is_none(), "Key 永不在白名单");
        assert!(spec_for("runtime.timeoutMinutes").is_none());
        assert!(spec_for("ark.keepQueuesEnabled").is_some());
        for k in ["runtime.retryOnce", "runtime.arkVisionRescue", "runtime.mumuVisionRescue", "runtime.visionAllowReward", "runtime.supervisor", "runtime.keepScreenOn", "notify.toast", "runtime.abortOnMaaStallWarning"] {
            assert!(spec_for(k).is_some(), "右栏开关必须在白名单：{k}");
        }
    }

    #[test]
    fn runtime_flags_never_guess_missing_or_wrong_types() {
        let v = runtime_flags(&json!({
            "runtime": {"retryOnce": true, "supervisor": null, "keepScreenOn": "true"},
            "notify": {"toast": false}
        }));
        assert_eq!(v["retryOnce"], json!(true));
        assert_eq!(v["notify.toast"], json!(false));
        assert_eq!(v["supervisor"], Value::Null);
        assert_eq!(v["keepScreenOn"], Value::Null);
        assert_eq!(v["abortOnMaaStallWarning"], Value::Null);
        let missing_section = runtime_flags(&json!({"runtime": {}}));
        assert_eq!(missing_section["notify.toast"], Value::Null);
    }

    #[test]
    fn starting_opers_keep_object_shape_and_reject_unknown_keys() {
        assert!(check_new_value("StartingOpers", &json!([]), &json!([
            {"Name": "缄默德克萨斯", "UseSupport": false},
            {"Name": "令"}
        ])).is_ok());
        assert!(check_new_value("StartingOpers", &json!([]), &json!([
            {"Name": "A", "UseSupport": "false"}
        ])).is_err());
        assert!(check_new_value("StartingOpers", &json!([]), &json!([
            {"Name": "A", "Unknown": true}
        ])).is_err());
        assert!(check_new_value("StartingOpers", &json!([]), &json!(["A"])).is_err());
        assert!(check_new_value("StartingOpers", &json!([]), &json!([
            {"Name": "A"}, {"Name": "A", "UseSupport": true}
        ])).is_err());
    }

    #[test]
    fn blackflow_flags_and_animals_are_validated() {
        let ok = |key: &str, value: &str| {
            assert!(check_new_value(key, &json!("None"), &json!(value)).is_ok(), "{key}={value}");
        };
        ok("BlackFlowEnding", "Ending1");
        ok("BlackFlowStartAnimal1", "FeatheredSerpent");
        ok("BlackFlowStartAnimal2", "None");
        ok("BlackFlowStartRewards", "None");
        ok("BlackFlowStartRewards", "UnnumberedItem, Pegasus, BaiZe");
        ok("BlackFlowStartRewards", "SerpentRewards");
        assert!(check_new_value("BlackFlowEnding", &json!("Ending1"), &json!("Ending2")).is_err());
        assert!(check_new_value("BlackFlowStartAnimal1", &json!("None"), &json!("Cat, Dog")).is_err());
        assert!(check_new_value("BlackFlowStartRewards", &json!("None"), &json!("None, Pegasus")).is_err());
        assert!(check_new_value("BlackFlowStartRewards", &json!("None"), &json!("Pegasus; BaiZe")).is_err());
        assert!(check_new_value("BlackFlowStartRewards", &json!("None"), &json!("未知")).is_err());
    }

    #[test]
    fn blackflow_priority_is_an_ordered_permutation() {
        let good = json!([
            "Pegasus", "SpaceLease", "ForestRide", "UnnumberedItem", "RegressionCompensation",
            "SurveyAdvance", "NestParasitism", "Hippogriff", "Dragon", "Hydra", "BaiZe", "GoldenCrow"
        ]);
        assert!(check_new_value("BlackFlowStartRewardPriority", &json!([]), &good).is_ok());
        let mut short = good.as_array().unwrap().clone();
        short.pop();
        assert!(check_new_value("BlackFlowStartRewardPriority", &json!([]), &json!(short)).is_err());
        let mut duplicate = good.as_array().unwrap().clone();
        duplicate[11] = json!("Pegasus");
        assert!(check_new_value("BlackFlowStartRewardPriority", &json!([]), &json!(duplicate)).is_err());
        let mut unknown = good.as_array().unwrap().clone();
        unknown[0] = json!("None");
        assert!(check_new_value("BlackFlowStartRewardPriority", &json!([]), &json!(unknown)).is_err());
    }

    #[test]
    fn coerce_checks_types() {
        assert!(coerce(Kind::Bool, &json!(true)).is_ok());
        assert!(coerce(Kind::Bool, &json!("true")).is_err());
        assert!(coerce(Kind::Int, &json!(12)).is_ok());
        assert!(coerce(Kind::Int, &json!(30)).is_ok());
        assert!(coerce(Kind::Int, &json!(0)).is_err());
        assert!(coerce(Kind::Int, &json!(31)).is_err());
        assert!(coerce(Kind::Int, &json!(-1)).is_err());
        assert!(coerce(Kind::StrList, &json!(["A", " B ", ""])).unwrap() == json!(["A", "B"]));
        assert!(coerce(Kind::Str, &json!("x\u{0}y")).is_err());
    }

    #[test]
    fn farm_count_keeps_legacy_zero_but_marks_bad_types() {
        assert_eq!(farm_count_ci(&json!({"farmMaxTimesPerRound": 12}), "farmMaxTimesPerRound"), json!(12));
        assert_eq!(farm_count_ci(&json!({"farmMaxTimesPerRound": 0}), "farmMaxTimesPerRound"), json!(0));
        assert_eq!(farm_count_ci(&json!({"farmMaxTimesPerRound": "12"}), "farmMaxTimesPerRound"), Value::Null);
        assert_eq!(farm_count_ci(&json!({}), "farmMaxTimesPerRound"), Value::Null);
        assert_eq!(coerce_plan_value("ark.farmMaxTimesPerRound", Kind::Int, &json!(0), &json!(0)).unwrap(), json!(0));
        assert!(coerce_plan_value("ark.farmMaxTimesPerRound", Kind::Int, &json!(12), &json!(0)).is_err());
        assert!(coerce_plan_value("ark.farmMaxTimesPerRound", Kind::Int, &json!(12), &json!(31)).is_err());
        assert!(coerce_plan_value("ark.farmMaxTimesPerRound", Kind::Int, &json!("12"), &json!(12)).is_err());
    }

    fn fixture() -> String {
        r#"{
  "_readme": {"keep": "原样"},
  "ark": {
    "defaultStage": "CE-5",
    "keepQueuesEnabled": [
      "StartUp",
      "Recruit"
    ],
    "farmAppend": true,
    "farmMaxTimesPerRound": 12
  },
  "re1999": {
    "planToInstance": {
      "idle": "日常-长草",
      "event": "日常-活动"
    }
  },
  "runtime": {
    "quitAssistantAfter": true
  }
}
"#
        .to_string()
    }

    fn edit(text: &str, key: &str, val: Value) -> String {
        let spec = spec_for(key).unwrap();
        let (vs, ve) = value_span(text, &spec).unwrap();
        let mut out = text.to_string();
        out.replace_range(vs..ve, &format_value(&val, &text[vs..ve], &line_indent(text, vs)));
        out
    }

    #[test]
    fn text_edit_only_touches_target() {
        let t = fixture();
        let before: Value = serde_json::from_str(&t).unwrap();

        let t1 = edit(&t, "ark.defaultStage", json!("LS-5"));
        let v1: Value = serde_json::from_str(&t1).unwrap();
        assert_eq!(v1["ark"]["defaultStage"], json!("LS-5"));
        assert_eq!(v1["_readme"], before["_readme"]);
        assert_eq!(v1["ark"]["farmMaxTimesPerRound"], json!(12));

        let t2 = edit(&t, "ark.keepQueuesEnabled", json!(["StartUp", "Mall"]));
        let v2: Value = serde_json::from_str(&t2).unwrap();
        assert_eq!(v2["ark"]["keepQueuesEnabled"], json!(["StartUp", "Mall"]));
        assert!(t2.contains("    \"StartUp\",\n      \"Mall\"\n    ]"), "多行数组保持多行：{t2}");

        let t3 = edit(&t, "re1999.planToInstance.event", json!("复刻"));
        let v3: Value = serde_json::from_str(&t3).unwrap();
        assert_eq!(v3["re1999"]["planToInstance"]["event"], json!("复刻"));
        assert_eq!(v3["re1999"]["planToInstance"]["idle"], json!("日常-长草"));

        let t4 = edit(&t, "runtime.quitAssistantAfter", json!(false));
        let v4: Value = serde_json::from_str(&t4).unwrap();
        assert_eq!(v4["runtime"]["quitAssistantAfter"], json!(false));
    }

    #[test]
    fn missing_key_is_refused_not_inserted() {
        let t = fixture();
        let spec = Spec { section: "ark", sub: None, key: "alsoEnableMainFight", kind: Kind::Bool };
        assert!(value_span(&t, &spec).is_none());
    }

    #[test]
    fn stamp_is_readable() {
        let s = stamp();
        assert_eq!(s.len(), 15, "YYYYMMDD-HHMMSS：{s}");
        assert_eq!(&s[8..9], "-");
        assert!(s[..8].chars().all(|c| c.is_ascii_digit()));
    }

    /// 拿真配置当"版式样本"：每枚白名单键取到的文本区间，解析回来必须跟 JSON 值逐等。
    /// 只读，不写；仓库里没有 settings.json 时跳过（CI/移植环境）。
    #[test]
    fn real_settings_spans_line_up() {
        let p = Path::new(env!("CARGO_MANIFEST_DIR")).join("../../../config/settings.json");
        let raw = match fs::read(&p) {
            Ok(b) => b,
            Err(_) => {
                eprintln!("SKIP real_settings_spans_line_up：仓库根 config/settings.json 不在，只读样本测试跳过");
                return;
            }
        };
        let body = if raw.starts_with(&[0xEF, 0xBB, 0xBF]) { &raw[3..] } else { &raw[..] };
        let text = std::str::from_utf8(body).unwrap();
        let v: Value = serde_json::from_str(text).expect("真配置得是合法 JSON");
        let keys = [
            "ark.defaultStage",
            "ark.finishAllSanityWith",
            "ark.keepQueuesEnabled",
            "ark.disableQueues",
            "ark.farmAppend",
            "ark.farmMaxTimesPerRound",
            "ark.alsoEnableMainFight",
            "runtime.retryOnce",
            "runtime.arkVisionRescue",
            "runtime.mumuVisionRescue",
            "runtime.visionAllowReward",
            "runtime.supervisor",
            "runtime.keepScreenOn",
            "notify.toast",
            "runtime.abortOnMaaStallWarning",
            "runtime.quitAssistantAfter",
            "runtime.closeArknightsAfter",
            "runtime.launchArkFromHelper",
            "runtime.m9aCliArgs",
            "runtime.m9aForceStart",
            "runtime.closeEmulatorAfter",
            "runtime.takeOverAssistant",
            "re1999.mainStoryRequiresAck",
            "re1999.shopDoneStopsEvent",
            "re1999.planToInstance.idle",
            "re1999.planToInstance.event",
            "re1999.planToInstance.rerun",
            "mumu.vmIndex",
        ];
        let mut present = 0usize;
        for k in keys {
            let spec = spec_for(k).unwrap();
            match get_path(&v, k) {
                Some(value) => {
                    let (vs, ve) = value_span(text, &spec).unwrap_or_else(|| panic!("{k} 定位不到"));
                    let span: Value = serde_json::from_str(text[vs..ve].trim())
                        .unwrap_or_else(|e| panic!("{k} 的文本区间不合法：{e}"));
                    if span != *value {
                        panic!("{k} 的文本区间跟 JSON 值不一致（不回显配置值）");
                    }
                    present += 1;
                }
                None => {
                    // 旧配置可以缺可选键；写盘本就拒绝补键。缺键与定位不到必须分开判。
                    assert!(value_span(text, &spec).is_none(), "{k} 实际缺键，文本定位却报在位");
                    eprintln!("SKIP {k}：真实配置缺这个可选键");
                }
            }
        }
        assert!(present > 0, "真实配置没有任一受测键，样本等于没读");
    }

    #[test]
    fn task_rows_follow_four_tier_rule() {
        let text = r#"{"Configurations":{"Default":{"TaskQueue":[
            {"TaskType":"StartUp","Name":"开始唤醒","IsEnable":true},
            {"TaskType":"Mall","Name":"信用收支","IsEnable":true},
            {"TaskType":"Fight","Name":"理智作战","IsEnable":true,"StagePlan":["CE-5"]},
            {"TaskType":"Fight","Name":"剩余理智","IsEnable":false,"StagePlan":["AP-5"]},
            {"TaskType":"Roguelike","Name":"自动肉鸽","IsEnable":true}
        ]}}}"#;
        let keep = vec!["StartUp".to_string()];
        let off = vec!["Mall".to_string()];
        let rows = task_rows_from(text, &keep, &off).unwrap();
        assert_eq!(rows.len(), 5);
        assert_eq!(rows[0]["on"], json!(true), "启用名单 → 开");
        assert_eq!(rows[1]["on"], json!(false), "禁用名单 → 关");
        // Fight：勾选框管的是「整类是否被禁用」，同一类型两条共用同一个勾选态；
        // 各自原本的 IsEnable 放 cur，交给决策（理智作战 开 / 剩余理智 关）。
        assert_eq!(rows[2]["on"], json!(true), "Fight 未被禁 → 勾选态为开");
        assert_eq!(rows[2]["cur"], json!(true));
        assert_eq!(rows[2]["control"], json!("decision"));
        assert_eq!(rows[2]["plan"], json!(["CE-5"]));
        assert_eq!(rows[3]["on"], json!(true), "同类型第二条共用类型级勾选态");
        assert_eq!(rows[3]["cur"], json!(false), "逐条 IsEnable 留在 cur");
        assert_eq!(rows[4]["on"], json!(true), "两名单都不在 → 维持现状");
        assert_eq!(rows[4]["control"], json!("settings"));
        // Fight 进禁用名单 → 两条都显示关（整类禁用）。
        let off2 = vec!["fight".to_string()];
        let rows = task_rows_from(text, &keep, &off2).unwrap();
        assert_eq!(rows[2]["on"], json!(false));
        assert_eq!(rows[3]["on"], json!(false));
        assert_eq!(rows[3]["cur"], json!(false), "cur 不受名单影响");
    }

    #[test]
    fn toggle_lists_moves_between_two_names() {
        let (keep, off) = toggle_lists(vec!["StartUp".into()], vec!["Roguelike".into()], "mall", true);
        assert_eq!(keep, vec!["StartUp".to_string(), "mall".to_string()]);
        assert_eq!(off, vec!["Roguelike".to_string()]);
        // 大小写不敏感、重复不加两次
        let (keep, off) = toggle_lists(keep, off, "MALL", true);
        assert_eq!(keep, vec!["StartUp".to_string(), "mall".to_string()]);
        assert_eq!(off, vec!["Roguelike".to_string()]);
        // 关：从启用名单摘掉、进禁用名单
        let (keep, off) = toggle_lists(keep, off, "StartUp", false);
        assert_eq!(keep, vec!["mall".to_string()]);
        assert_eq!(off, vec!["Roguelike".to_string(), "StartUp".to_string()]);
        // 已在禁用名单里再关一次不重复
        let (_, off) = toggle_lists(vec![], off, "startup", false);
        assert_eq!(off.len(), 2);
    }

    #[test]
    fn kind_validation_is_narrow() {
        assert!(valid_kind("StartUp"));
        assert!(valid_kind("Roguelike"));
        assert!(!valid_kind(""));
        assert!(!valid_kind("Fight; rm"));
        assert!(!valid_kind("任务"));
        assert!(!valid_kind(&"x".repeat(33)));
    }

    /* ── MAA 任务参数：原位改写 ── */

    fn maa_fixture() -> String {
        "{\r\n  \"Configurations\": {\r\n    \"Default\": {\r\n      \"TaskQueue\": [\r\n        {\r\n          \"$type\": \"FightTask\",\r\n          \"Name\": \"理智作战\",\r\n          \"IsEnable\": true,\r\n          \"TaskType\": \"Fight\",\r\n          \"UseMedicine\": false,\r\n          \"MedicineCount\": 0,\r\n          \"StagePlan\": [\r\n            \"CE-5\"\r\n          ]\r\n        },\r\n        {\r\n          \"$type\": \"InfrastTask\",\r\n          \"Name\": \"基建换班\",\r\n          \"IsEnable\": true,\r\n          \"TaskType\": \"Infrast\",\r\n          \"DormThreshold\": 30,\r\n          \"RoomList\": [\r\n            {\r\n              \"Room\": \"Power\"\r\n            }\r\n          ]\r\n        }\r\n      ]\r\n    }\r\n  }\r\n}".to_string()
    }

    fn patch_of(pairs: Vec<(&str, Value)>) -> serde_json::Map<String, Value> {
        let mut m = serde_json::Map::new();
        for (k, v) in pairs {
            m.insert(k.to_string(), v);
        }
        m
    }

    #[test]
    fn maa_patch_keeps_bytes_and_line_endings() {
        let t = maa_fixture();
        let p = patch_of(vec![
            ("UseMedicine", json!(true)),
            ("MedicineCount", json!(3)),
            ("StagePlan", json!(["TL-9", "CE-5"])),
        ]);
        let (out, changed) = apply_task_patch(&t, 0, "Fight", "理智作战", &p).unwrap();
        assert_eq!(changed.len(), 3);
        let v: Value = serde_json::from_str(&out).unwrap();
        let q = &v["Configurations"]["Default"]["TaskQueue"][0];
        assert_eq!(q["UseMedicine"], json!(true));
        assert_eq!(q["MedicineCount"], json!(3));
        assert_eq!(q["StagePlan"], json!(["TL-9", "CE-5"]));
        // 键序原样：$type 仍在 Name 前，StagePlan 仍在末尾。
        let ts = out.find("\"$type\"").unwrap();
        let nm = out.find("\"Name\"").unwrap();
        assert!(ts < nm);
        // 行尾不混：没有裸 LF（每个 \n 都属于 CRLF）。
        assert_eq!(out.matches('\n').count(), out.matches("\r\n").count(), "CRLF 文件里混进了裸 LF");
        // 第二条（基建）一个字节没动。
        assert!(out.contains("\"DormThreshold\": 30"));
    }

    #[test]
    fn maa_patch_refuses_identity_and_bad_values() {
        let t = maa_fixture();
        let bad = |pairs: Vec<(&str, Value)>| {
            let dbg = format!("{pairs:?}");
            let p = patch_of(pairs);
            let r = apply_task_patch(&t, 0, "Fight", "理智作战", &p);
            assert!(r.is_err(), "应当拒写：{dbg}");
        };
        bad(vec![("Name", json!("别的"))]);
        bad(vec![("IsEnable", json!(false))]);
        bad(vec![("TaskType", json!("Mall"))]);
        bad(vec![("不存在的键", json!(1))]);
        bad(vec![("MedicineCount", json!("三"))]);
        bad(vec![("UseMedicine", json!(1))]);
        // 索引/名字/类型对不上 → 拒（防止队列刚变过时改错条）。
        assert!(apply_task_patch(&t, 9, "Fight", "理智作战", &patch_of(vec![("UseMedicine", json!(true))])).is_err());
        assert!(apply_task_patch(&t, 0, "Mall", "理智作战", &patch_of(vec![("UseMedicine", json!(true))])).is_err());
        assert!(apply_task_patch(&t, 0, "Fight", "剩余理智", &patch_of(vec![("UseMedicine", json!(true))])).is_err());
    }

    #[test]
    fn maa_roguelike_patch_keeps_object_arrays_and_order() {
        let t = "{\r\n  \"Configurations\": {\r\n    \"Default\": {\r\n      \"TaskQueue\": [\r\n        {\r\n          \"$type\": \"RoguelikeTask\",\r\n          \"TaskType\": \"Roguelike\",\r\n          \"Name\": \"自动肉鸽\",\r\n          \"IsEnable\": true,\r\n          \"StartingOpers\": [{\"Name\": \"令\"}],\r\n          \"BlackFlowStartAnimal1\": \"None\",\r\n          \"BlackFlowStartAnimal2\": \"None\",\r\n          \"BlackFlowStartRewards\": \"None\",\r\n          \"BlackFlowStartRewardPriority\": [\"Pegasus\", \"SpaceLease\", \"ForestRide\", \"UnnumberedItem\", \"RegressionCompensation\", \"SurveyAdvance\", \"NestParasitism\", \"Hippogriff\", \"Dragon\", \"Hydra\", \"BaiZe\", \"GoldenCrow\"]\r\n        }\r\n      ]\r\n    }\r\n  }\r\n}".to_string();
        let priority = vec!["GoldenCrow", "BaiZe", "Hydra", "Dragon", "Hippogriff", "NestParasitism",
            "SurveyAdvance", "RegressionCompensation", "UnnumberedItem", "ForestRide", "SpaceLease", "Pegasus"];
        let p = patch_of(vec![
            ("StartingOpers", json!([{"Name": "缄默德克萨斯", "UseSupport": false}, {"Name": "令"}])),
            ("BlackFlowStartAnimal1", json!("FeatheredSerpent")),
            ("BlackFlowStartAnimal2", json!("Cat")),
            ("BlackFlowStartRewards", json!("Pegasus, BaiZe, GoldenCrow")),
            ("BlackFlowStartRewardPriority", json!(priority)),
        ]);
        let (out, changed) = apply_task_patch(&t, 0, "Roguelike", "自动肉鸽", &p).unwrap();
        assert_eq!(changed.len(), 5);
        let v: Value = serde_json::from_str(&out).unwrap();
        let q = &v["Configurations"]["Default"]["TaskQueue"][0];
        assert_eq!(q["StartingOpers"], json!([{"Name": "缄默德克萨斯", "UseSupport": false}, {"Name": "令"}]));
        assert_eq!(q["BlackFlowStartAnimal1"], json!("FeatheredSerpent"));
        assert_eq!(q["BlackFlowStartAnimal2"], json!("Cat"));
        assert_eq!(q["BlackFlowStartRewards"], json!("Pegasus, BaiZe, GoldenCrow"));
        assert_eq!(q["BlackFlowStartRewardPriority"][0], json!("GoldenCrow"));
        assert_eq!(q["BlackFlowStartRewardPriority"][11], json!("Pegasus"));
        assert!(out.contains("\"IsEnable\": true"), "身份键原样保留");
        assert!(out.contains("\"TaskType\": \"Roguelike\""), "身份键原样保留");
        assert_eq!(out.matches('\n').count(), out.matches("\r\n").count(), "CRLF 文件里混进了裸 LF");
    }

    #[test]
    fn maa_patch_rooms_and_noop() {
        let t = maa_fixture();
        let p = patch_of(vec![("RoomList", json!([{"Room": "Power", "IsEnabled": false}]))]);
        let (out, changed) = apply_task_patch(&t, 1, "Infrast", "基建换班", &p).unwrap();
        assert_eq!(changed, vec!["RoomList".to_string()]);
        let v: Value = serde_json::from_str(&out).unwrap();
        assert_eq!(v["Configurations"]["Default"]["TaskQueue"][1]["RoomList"][0]["IsEnabled"], json!(false));

        let rich = t.replace(
            "\"Room\": \"Power\"",
            "\"Room\": \"Power\", \"IsEnabled\": true, \"FutureFlag\": {\"keep\": 1}",
        );
        let keep_extra = patch_of(vec![("RoomList", json!([{
            "Room": "Power", "IsEnabled": false, "FutureFlag": {"keep": 1}
        }]))]);
        let (out, _) = apply_task_patch(&rich, 1, "Infrast", "基建换班", &keep_extra).unwrap();
        let v: Value = serde_json::from_str(&out).unwrap();
        assert_eq!(v["Configurations"]["Default"]["TaskQueue"][1]["RoomList"][0]["FutureFlag"], json!({"keep": 1}));
        // 面板不能借房间开关改写/删除未知字段，也不能增删或重排房间。
        let bad_extra = patch_of(vec![(
            "RoomList", json!([{"Room": "Power", "IsEnabled": false, "FutureFlag": {"keep": 2}}])
        )]);
        assert!(apply_task_patch(&rich, 1, "Infrast", "基建换班", &bad_extra).is_err());
        let missing_extra = patch_of(vec![(
            "RoomList", json!([{"Room": "Power", "IsEnabled": false}])
        )]);
        assert!(apply_task_patch(&rich, 1, "Infrast", "基建换班", &missing_extra).is_err());
        let resized = patch_of(vec![(
            "RoomList", json!([
                {"Room": "Power", "IsEnabled": false, "FutureFlag": {"keep": 1}},
                {"Room": "Mfg", "FutureFlag": {"keep": 1}}
            ])
        )]);
        assert!(apply_task_patch(&rich, 1, "Infrast", "基建换班", &resized).is_err());
        // 同一值再改一次：不写、不回键。
        let p = patch_of(vec![("DormThreshold", json!(30))]);
        let (out2, changed2) = apply_task_patch(&t, 1, "Infrast", "基建换班", &p).unwrap();
        assert!(changed2.is_empty());
        assert_eq!(out2, t);
    }

    /* ── M9A 实例配置：队列四字段 + 选项树 + 原位改写 ── */

    fn m9a_fixture() -> String {
        "{\r\n  \"_readme\": {\"keep\": \"原样\"},\r\n  \"InstanceName\": \"测试实例\",\r\n  \"Resource\": \"官服\",\r\n  \"CurrentControllerName\": \"ADB\",\r\n  \"CurrentController\": 2,\r\n  \"InstancePresetKey\": \"\",\r\n  \"CurrentTasks\": [\r\n    \"Bank:Bank\"\r\n  ],\r\n  \"DeletedTaskNames\": [],\r\n  \"MissingTaskSessions\": {\"Bank\": \"old\", \"Legacy\": \"old2\"},\r\n  \"KeepEmptyTaskQueue\": false,\r\n  \"TaskItems\": [],\r\n  \"ResourceOptionItems\": {},\r\n  \"ControllerOptionItems\": {},\r\n  \"GlobalOptionItems\": []\r\n}".to_string()
    }

    fn m9a_catalog_fixture() -> Value {
        let defs = json!([
            {"name":"柜台特惠","type":"switch","cases":[{"name":"Yes","options":["子选项"]},{"name":"No"}],"defaultCase":"Yes","inputs":[]},
            {"name":"子选项","type":"select","cases":[{"name":"A"},{"name":"B"}],"defaultCase":"A","inputs":[]},
            {"name":"多层","type":"checkbox","cases":[{"name":"One"},{"name":"Two"}],"defaultCase":[],"inputs":[]},
            {"name":"自定义兑换码","type":"input","cases":[],"defaultCase":null,"inputs":[{"name":"Code","default":"","password":false},{"name":"Secret","default":"do-not-return-default","password":true}]}
        ]);
        json!({
            "resources":[{"name":"官服","controllers":["ADB"]}],
            "controllers":["ADB","PC"],
            "presets":[],
            "tasks":[
                {"name":"Bank","entry":"Bank","options":defs},
                {"name":"Psychube","entry":"Psychube","options":[]},
                {"name":"Awards","entry":"Awards","options":[]}
            ],
            "resourceOptions":{},"controllerOptions":{},"globalOptions":[]
        })
    }

    #[test]
    fn m9a_catalog_uses_keys_and_localized_labels_without_cycles() {
        let defs = json!({
            "Parent":{"name":"WrongParent","type":"switch","label":"$Option.Parent","description":"$Option.Parent.desc","cases":[{"name":"On","option":["Child"]},{"name":"Off"}]},
            "Child":{"name":"WrongChild","type":"select","label":"$Option.Child","cases":[{"name":"A","option":["Grand"]},{"name":"B"}]},
            "Grand":{"type":"checkbox","cases":[{"name":"One","option":["Parent"]}]}
        }).as_object().unwrap().clone();
        let locale = json!({"Option.Parent":"父项标签","Option.Parent.desc":"父项说明","Option.Child":"子项标签"});
        let got = m9a_defs_from_names(&defs, &["Parent".to_string()], &locale);
        let names: Vec<&str> = got.iter().filter_map(|d| d.get("name").and_then(|x| x.as_str())).collect();
        assert_eq!(names, ["Parent", "Child", "Grand"], "身份取外层键；只给父名也必须递归取到孙项，且环不能爆栈");
        assert_eq!(got[0]["label"], json!("父项标签"));
        assert_eq!(got[0]["description"], json!("父项说明"));
        assert_eq!(got[1]["label"], json!("子项标签"));
        assert_eq!(got[2]["name"], json!("Grand"), "没有 label 时仍保留原键名");
    }

    #[test]
    fn m9a_child_edit_materializes_default_parent() {
        let t = m9a_fixture();
        let cat = m9a_catalog_fixture();
        let patch = json!({"name":"测试实例","optionEdits":[
            {"source":"task","task":0,"path":["柜台特惠","子选项"],"selected_case":"B"}
        ]});
        let (out, _) = apply_m9a_instance_patch(&t, "测试实例", &patch, &cat).unwrap();
        let v: Value = serde_json::from_str(&out).unwrap();
        let parent = &v["TaskItems"][0]["option"][0];
        assert_eq!(parent["name"], json!("柜台特惠"));
        assert_eq!(parent["selected_case"], json!("Yes"));
        assert_eq!(parent["index"], json!(0));
        assert_eq!(parent["sub_options"][0]["selected_case"], json!("B"));
    }

    #[test]
    fn m9a_patch_syncs_queue_and_option_tree() {
        let t = m9a_fixture();
        let cat = m9a_catalog_fixture();
        let patch = json!({
            "name":"测试实例",
            "currentTasks":["Bank:Bank","Psychube<|||>Psychube"],
            "optionEdits":[
                {"source":"task","task":0,"path":["柜台特惠"],"selected_case":"Yes"},
                {"source":"task","task":0,"path":["柜台特惠","子选项"],"selected_case":"B"},
                {"source":"task","task":0,"path":["多层"],"selected_cases":["Two","One"]}
            ]
        });
        let (out, changed) = apply_m9a_instance_patch(&t, "测试实例", &patch, &cat).unwrap();
        let v: Value = serde_json::from_str(&out).unwrap();
        assert_eq!(v["CurrentTasks"], json!(["Bank<|||>Bank","Psychube<|||>Psychube"]));
        assert_eq!(v["DeletedTaskNames"], json!(["Awards"]), "未选的已知任务必须进删除名单");
        assert_eq!(v["MissingTaskSessions"], json!({"Legacy":"old2"}), "已知名字清出缺失追踪，未知旧项保留");
        assert_eq!(v["KeepEmptyTaskQueue"], json!(false));
        let item = &v["TaskItems"][0];
        assert_eq!(item["name"], json!("Bank"));
        assert_eq!(item["option"][0]["selected_case"], json!("Yes"));
        assert_eq!(item["option"][0]["index"], json!(0));
        assert_eq!(item["option"][0]["sub_options"][0]["selected_case"], json!("B"));
        let multi = item["option"].as_array().unwrap().iter().find(|n| n.get("name").and_then(|x| x.as_str()) == Some("多层")).unwrap();
        assert_eq!(multi["selected_cases"], json!(["Two","One"]));
        assert!(multi.get("index").is_none());
        assert_eq!(v["_readme"], json!({"keep":"原样"}));
        assert_eq!(out.matches('\n').count(), out.matches("\r\n").count(), "CRLF 不混裸 LF");
        assert!(changed.iter().any(|x| x == "CurrentTasks"));
        assert!(changed.iter().any(|x| x.starts_with("TaskItems")));
        // 同一根成员被多个选项改动命中时，文本计划必须去重，不能叠 span。
        assert!(serde_json::from_str::<Value>(&out).is_ok());
    }

    #[test]
    fn m9a_checkbox_preserves_unknown_cases_but_rejects_new_ones() {
        let t = m9a_fixture().replace(
            "  \"TaskItems\": [],",
            "  \"TaskItems\": [{\"name\":\"Bank\",\"option\":[{\"name\":\"多层\",\"selected_cases\":[\"LegacyUnknown\",\"One\"]}]}],",
        );
        let cat = m9a_catalog_fixture();
        let run = |cases: Value| apply_m9a_instance_patch(&t, "测试实例", &json!({
            "name":"测试实例",
            "optionEdits":[{"source":"task","task":0,"path":["多层"],"selected_cases":cases}]
        }), &cat);
        let (out, _) = run(json!(["Two", "One", "LegacyUnknown"])).unwrap();
        let v: Value = serde_json::from_str(&out).unwrap();
        assert_eq!(v["TaskItems"][0]["option"][0]["selected_cases"],
            json!(["LegacyUnknown", "One", "Two"]), "未知项按磁盘顺序保留，已知项按旧序＋新增序合并");
        let (out, _) = run(json!(["Two"])).unwrap();
        let v: Value = serde_json::from_str(&out).unwrap();
        assert_eq!(v["TaskItems"][0]["option"][0]["selected_cases"],
            json!(["LegacyUnknown", "Two"]), "取消已知项不能连坐未知现值");
        assert!(run(json!(["Two", "Invented"])).is_err(), "面板不能新造目录外 case");
    }

    #[test]
    fn capture_m9a_instance_id_is_authoritative() {
        assert!(m9a_instance_matches("id-a", "旧名", "ID-A", "新名"));
        assert!(!m9a_instance_matches("id-missing", "旧名", "id-b", "旧名"), "ID 失效不得借同名实例回退");
        assert!(!m9a_instance_matches("", "", "id-b", "旧名"), "指针缺失不得猜实例");
        assert!(m9a_instance_matches("", "旧名", "id-b", "旧名"));
    }

    #[test]
    fn capture_adb_is_relative_to_mumu_dir_not_m9a_dir() {
        let paths = json!({
            "m9aDir": "m9a-root",
            "mumuDir": "mumu-root",
            "mumuAdb": "nx_main\\adb.exe"
        });
        assert_eq!(capture_adb_path(&paths), Path::new("mumu-root").join("nx_main").join("adb.exe"));
        let absolute = json!({ "mumuDir": "mumu-root", "mumuAdb": "D:\\adb.exe" });
        assert_eq!(capture_adb_path(&absolute), PathBuf::from("D:\\adb.exe"));
    }

    #[test]
    fn m9a_patch_rejects_bad_identity_candidates_and_inputs() {
        let t = m9a_fixture();
        let cat = m9a_catalog_fixture();
        let run = |p: Value| apply_m9a_instance_patch(&t, "测试实例", &p, &cat);
        assert!(apply_m9a_instance_patch(&t, "别的", &json!({"name":"别的"}), &cat).is_err(), "实例名陈旧编辑必须拒绝");
        assert!(run(json!({"name":"别的"})).is_err(), "patch 身份不能冒用别的实例名");
        assert!(run(json!({"AdbSerial":"x"})).is_err());
        assert!(run(json!({"currentTasks":["未知<|||>Unknown"]})).is_err());
        assert!(run(json!({"currentTasks":["Bank<|||>Bank<|||>x"]})).is_err());
        assert!(run(json!({"controller":"PC"})).is_err(), "官服不支持 PC");
        assert!(run(json!({"optionEdits":[{"source":"task","task":0,"path":["自定义兑换码"],"selected_case":"x"}]})).is_err());
        assert!(run(json!({"optionEdits":[{"source":"task","task":0,"path":["不存在"]}]})).is_err());
        let (same, changed) = run(json!({"name":"测试实例"})).unwrap();
        assert!(changed.is_empty());
        assert_eq!(same, t);
    }

    #[test]
    fn m9a_safe_nodes_masks_password_and_follows_active_case() {
        let cat = m9a_catalog_fixture();
        let defs = m9a_task_defs(&cat, "Bank");
        let raw = json!([
            {"name":"柜台特惠","index":1,"selected_case":"No","sub_options":[{"name":"子选项","selected_case":"B"}]},
            {"name":"自定义兑换码","data":{"Code":"ABC-123","Secret":"do-not-return"}}
        ]);
        let safe = m9a_safe_nodes(Some(&raw), &defs);
        let text = serde_json::to_string(&safe).unwrap();
        let input_def = defs.iter().find(|d| d.get("name").and_then(|x| x.as_str()) == Some("自定义兑换码")).unwrap();
        let def_text = serde_json::to_string(&m9a_option_def(input_def, "自定义兑换码", &json!({}))).unwrap();
        assert!(!def_text.contains("do-not-return-default"), "密码 input 的默认值不能从目录定义离开 Rust");
        assert!(!text.contains("do-not-return"));
        assert!(!text.contains("do-not-return-default"), "密码 input 的默认值也不能从安全节点离开 Rust");
        assert!(!text.contains("子选项"), "No 分支不显示未激活子项");
        assert!(text.contains("ABC-123"), "非密码 input 现值可展示");
    }

    #[test]
    fn m9a_history_accepts_only_real_formats() {
        assert_eq!(m9a_history_parts("Bank<|||>Bank"), Some(("Bank".into(),"Bank".into())));
        assert_eq!(m9a_history_parts("Bank:Bank"), Some(("Bank".into(),"Bank".into())));
        assert_eq!(m9a_history_parts("Bank<||>Bank"), None);
        assert_eq!(m9a_history_parts("A<|||>B<|||>C"), None);
        assert_eq!(m9a_history_parts("A:B:C"), None);
    }
}