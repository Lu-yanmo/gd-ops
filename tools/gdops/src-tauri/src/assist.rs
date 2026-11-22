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
use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

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
    let mut r = save_plan(vec![
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
    let r = save_plan(vec![
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

/// 值收口：新值必须跟原值同一种 JSON 类型；RoomList 例外（对象数组，只收 Room/IsEnabled）。
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
                Ok(())
            }
        }
        (Value::Array(a), Value::Array(b)) => {
            if b.iter().any(|x| x.is_null()) {
                return Err(format!("{key}：列表里不能有 null"));
            }
            if key.eq_ignore_ascii_case("RoomList") {
                for it in b {
                    let o = it.as_object().ok_or_else(|| format!("{key}：每一间得是对象"))?;
                    let room = o.get("Room").and_then(|x| x.as_str()).unwrap_or("");
                    if room.is_empty() {
                        return Err(format!("{key}：每间得带 Room"));
                    }
                    for (k, v) in o {
                        if !k.eq_ignore_ascii_case("Room") && !k.eq_ignore_ascii_case("IsEnabled") {
                            return Err(format!("{key}：房间对象多出键 {k}"));
                        }
                        if !v.is_string() && !v.is_boolean() {
                            return Err(format!("{key}：{k} 只收文本/开关"));
                        }
                    }
                }
                return Ok(());
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
    let bk = backup(&root, &cfg);
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
        "backup": bk.unwrap_or_default(),
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
            "items":[], "infrasts":[], "rogueOpers":[]});
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

    // 自定义基建配置文件名（不带扩展名），MAA 的文件选择器就列这一夹。
    let infrasts = match fs::read_dir(Path::new(dir).join("resource").join("custom_infrast")) {
        Ok(rd) => {
            let mut v: Vec<String> = rd
                .filter_map(|e| e.ok())
                .map(|e| e.file_name().to_string_lossy().to_string())
                .filter(|n| n.to_ascii_lowercase().ends_with(".json"))
                .map(|n| n[..n.len() - 5].to_string())
                .collect();
            v.sort();
            v
        }
        Err(_) => Vec::new(),
    };

    // 肉鸽开局干员：各主题 recruitment.json 的 priority[].opers[].name 并集。
    let mut rogue_opers: Vec<String> = Vec::new();
    if let Ok(rd) = fs::read_dir(Path::new(dir).join("resource").join("roguelike")) {
        for e in rd.filter_map(|x| x.ok()) {
            let p = e.path().join("recruitment.json");
            if let Some(v) = read_json_loose(&p) {
                if let Some(prio) = v.get("priority").and_then(|x| x.as_array()) {
                    for grp in prio {
                        if let Some(ops) = grp.get("opers").and_then(|x| x.as_array()) {
                            for o in ops {
                                if let Some(n) = o.get("name").and_then(|x| x.as_str()) {
                                    rogue_opers.push(n.to_string());
                                }
                            }
                        }
                    }
                }
            }
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
        "rogueOpers": uniq_strs(rogue_opers)
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

fn scrub(s: &str) -> String {
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
    Ok(json!({"settingsPresent": true, "maa": maa, "m9a": m9a}))
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
            "farmMaxTimesPerRound": i_ci(&ark, "farmMaxTimesPerRound"),
            "alsoEnableMainFight": b_ci(&ark, "alsoEnableMainFight"),
            "quitAssistantAfter": b_ci(&rt, "quitAssistantAfter"),
            "closeArknightsAfter": b_ci(&rt, "closeArknightsAfter"),
            "launchArkFromHelper": b_ci(&rt, "launchArkFromHelper")
        }
    })
}

/// 实例清单：读 appsettings 与实例目录下的逐台 json。这里是**展示用**的聚合（名字/任务名/ADB 串），
/// 不复制任何判定规则：切实例仍走 apply-m9a-instance，探活仍走 read-m9a-connection。
fn instances_now(dir: &str, app: &Value) -> (Vec<Value>, Option<Value>) {
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
        let tasks: Vec<String> = strs(v.get("CurrentTasks"))
            .into_iter()
            .map(|t| {
                // 真实件里的分隔是 `<|||>`（三竖线）；老件见过 `<||>`，两种都认，取任务名那一段。
                t.split("<|||>").next().unwrap_or("").split("<||>").next().unwrap_or("").to_string()
            })
            .filter(|t| !t.is_empty())
            .collect();
        let dev = v.get("AdbDevice").cloned().unwrap_or(json!({}));
        let row = json!({
            "id": id,
            "name": name,
            "taskCount": tasks.len(),
            "tasks": tasks.iter().take(5).cloned().collect::<Vec<_>>(),
            // 右侧二级框要完整任务表与资源/控制器；卡面仍用上面截断的五枚。
            "allTasks": tasks.clone(),
            "resource": s_ci(&v, "Resource"),
            "controller": s_ci(&v, "CurrentControllerName"),
            "preset": s_ci(&v, "InstancePresetKey"),
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

    let (instances, current) = if inst_dir.is_dir() {
        instances_now(&inst_dir.to_string_lossy(), &app)
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

    let mumu = match call(root, "mumu-state", &json!({}), 90) {
        Ok(d) => json!({
            "available": d.get("Available").and_then(|x| x.as_bool()).unwrap_or(false),
            "android": d.get("AndroidStarted").and_then(|x| x.as_bool()).unwrap_or(false),
            "process": d.get("ProcessStarted").and_then(|x| x.as_bool()).unwrap_or(false),
            "name": d.get("Name").cloned().unwrap_or(json!("")),
            "index": d.get("Index").cloned().unwrap_or(json!(""))
        }),
        Err(e) => json!({"available": false, "android": false, "process": false, "name": "", "index": "", "error": e}),
    };

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
            if !(0..=9999).contains(&n) {
                return Err("数值超出范围（0~9999）".to_string());
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
    let root = root()?;
    let path = settings_path(&root);
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
        let val = coerce(spec.kind, e.get("value").unwrap_or(&Value::Null))
            .map_err(|m| format!("{key}：{m}"))?;
        if get_path(&before, &key).is_none() {
            return Err(format!(
                "settings.json 里没有 {key} 这一项（老配置？跑 setup.ps1 或手动补一行再回来）"
            ));
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

    let backup = backup(&root, &path);
    let mut bytes = Vec::new();
    if had_bom {
        bytes.extend_from_slice(&[0xEF, 0xBB, 0xBF]);
    }
    bytes.extend_from_slice(out.as_bytes());
    fs::write(&path, &bytes).map_err(|e| format!("写 settings.json 失败：{e}"))?;

    let verify = read_settings(&root)?.ok_or_else(|| "写完后 settings.json 不见了".to_string())?;
    if verify != after {
        return Err("写盘后回读不一致：请从 backups/ 里把上一份拷回来，并把这一项手动改".to_string());
    }
    let changed: Vec<String> = plans.into_iter().map(|(_, _, _, k)| k).collect();
    Ok(json!({
        "ok": true,
        "changed": changed,
        "backup": backup.unwrap_or_default(),
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
    fn whitelist_is_explicit() {
        assert!(spec_for("ark.defaultStage").is_some());
        assert!(spec_for("ark.alsoEnableMainFight").is_some());
        assert!(spec_for("re1999.planToInstance.event").is_some());
        assert!(spec_for("runtime.visionApiKey").is_none(), "Key 永不在白名单");
        assert!(spec_for("runtime.timeoutMinutes").is_none());
        assert!(spec_for("ark.keepQueuesEnabled").is_some());
    }

    #[test]
    fn coerce_checks_types() {
        assert!(coerce(Kind::Bool, &json!(true)).is_ok());
        assert!(coerce(Kind::Bool, &json!("true")).is_err());
        assert!(coerce(Kind::Int, &json!(12)).is_ok());
        assert!(coerce(Kind::Int, &json!(-1)).is_err());
        assert!(coerce(Kind::StrList, &json!(["A", " B ", ""])).unwrap() == json!(["A", "B"]));
        assert!(coerce(Kind::Str, &json!("x\u{0}y")).is_err());
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
        let p = Path::new("../../config/settings.json");
        let raw = match fs::read(p) {
            Ok(b) => b,
            Err(_) => return,
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
        for k in keys {
            let spec = spec_for(k).unwrap();
            let (vs, ve) = value_span(text, &spec).unwrap_or_else(|| panic!("{k} 定位不到"));
            let span: Value = serde_json::from_str(text[vs..ve].trim())
                .unwrap_or_else(|e| panic!("{k} 的文本区间不合法：{e}"));
            assert_eq!(span, *get_path(&v, k).unwrap(), "{k} 的文本区间跟 JSON 值不一致");
        }
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
    fn maa_patch_rooms_and_noop() {
        let t = maa_fixture();
        let p = patch_of(vec![(
            "RoomList",
            json!([{"Room": "Power", "IsEnabled": false}, {"Room": "Mfg"}, {"Room": "Dorm", "IsEnabled": true}]),
        )]);
        let (out, changed) = apply_task_patch(&t, 1, "Infrast", "基建换班", &p).unwrap();
        assert_eq!(changed, vec!["RoomList".to_string()]);
        let v: Value = serde_json::from_str(&out).unwrap();
        let rooms = &v["Configurations"]["Default"]["TaskQueue"][1]["RoomList"];
        assert_eq!(rooms[0]["IsEnabled"], json!(false));
        assert_eq!(rooms[1]["Room"], json!("Mfg"));
        assert_eq!(rooms[2]["IsEnabled"], json!(true));
        // 房间对象只许 Room/IsEnabled。
        assert!(apply_task_patch(
            &t,
            1,
            "Infrast",
            "基建换班",
            &patch_of(vec![("RoomList", json!([{"Room": "Power", "X": 1}]))])
        )
        .is_err());
        // 同一值再改一次：不写、不回键。
        let p = patch_of(vec![("DormThreshold", json!(30))]);
        let (out2, changed2) = apply_task_patch(&t, 1, "Infrast", "基建换班", &p).unwrap();
        assert!(changed2.is_empty());
        assert_eq!(out2, t);
    }
}