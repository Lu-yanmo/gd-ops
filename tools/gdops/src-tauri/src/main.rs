// Windows release 不弹控制台；其余平台入口同形。
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    gdops_lib::run()
}