// OBS 排障助手 macOS 宿主（Tauri v2）
// ---------------------------------------------------------------------------
// 作为 Blazor WebAssembly 站点的桌面外壳，提供「WebView 里做不到」的能力：
//   1. 机密加密落盘（系统钥匙串 Keychain）；
//   2. 读取本机 OBS 日志目录（限定目录 + 限定扩展名）；
//   3. 用系统浏览器打开外链；
//   4. 可选的云端 AI 转发（API Key 不进入 WebView）；
//   5. 系统托盘（菜单栏图标）——录制 / 推流 / 虚拟摄像头 / 小窗 / 退出；
//   6. 全局热键（录制 / 推流 / 虚拟摄像头 / 小窗 / 主窗口开关）；
//   7. 迷你小窗（无边框置顶，前端 MiniPanel 页面）；
//   8. 单实例（第二个实例唤起已有窗口）。
// 以上全部收敛在唯一的 IPC 命令 `host_invoke` 内，见 src/host.rs。
//
// 安全说明：
// - 站点完全本地（来自 frontendDist），除用户显式开启的云端 AI 外不发起外网请求；
//   与 OBS 的通信是发往 127.0.0.1 的 WebSocket，不出本机。
// - capabilities 仍只保留 `core:default`：应用自身的命令（非插件命令）无需在 ACL 中
//   声明，写成 `allow-host-invoke` 反而会因权限标识符不存在导致构建失败。
// - 配置 tauri.conf.json 的 security.csp 作为纵深防御，限制脚本/连接来源。
// - 窗口禁用 devtools 与远程调试，避免本地静态内容被注入脚本后借助调试协议逃逸。
// - on_navigation 拦截所有导航：站点为纯本地内容，仅允许应用内资源（asset:/tauri:/ipc:）
//   与开发期 localhost，任何离开本地资源的导航一律取消，降低钓鱼/注入风险。
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod host;

use serde::Serialize;
use tauri::tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent};
use tauri::{Emitter, Manager, WebviewUrl};

/// 托盘 / 热键推送给前端的动作载荷。
///
/// 前端（Blazor WASM）监听 `shell:action` 事件，按 action 执行对应的 OBS 操作
/// （切换录制 / 推流 / 虚拟摄像头等都由前端 ObsConnectionService 完成）。
#[derive(Clone, Serialize)]
struct ShellAction {
    action: String,
}

const EVT_SHELL_ACTION: &str = "shell:action";

/// 主窗口默认尺寸（与 tauri.conf.json 无冲突，运行时以此为准）。
const MAIN_W: f64 = 1180.0;
const MAIN_H: f64 = 800.0;
const MIN_W: f64 = 860.0;
const MIN_H: f64 = 600.0;

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_notification::init())
        // 单实例：第二个实例直接唤起已有窗口（等价于 Windows 侧 Mutex + EventWaitHandle）
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(win) = app.get_webview_window("main") {
                let _ = win.show();
                let _ = win.unminimize();
                let _ = win.set_focus();
            }
        }))
        // 全局热键（等价于 Windows 侧 RegisterHotKey / WM_HOTKEY）。
        // 按下时只推送事件给前端，具体动作（切换录制等）由前端 ObsConnectionService 执行。
        .plugin(
            tauri_plugin_global_shortcut::Builder::new()
                .with_handler(|app, shortcut, event| {
                    if event.state == tauri_plugin_global_shortcut::ShortcutState::Pressed {
                        // 按 (modifiers, key) 匹配默认热键表，映射回动作名
                        if let Some(action) = host::match_hotkey_action(shortcut) {
                            let _ = app.emit(EVT_SHELL_ACTION, ShellAction { action: action.to_string() });
                        }
                    }
                })
                .build(),
        )
        .invoke_handler(tauri::generate_handler![host::host_invoke])
        .setup(|app| {
            let _ = create_main_window(app)?;
            let _ = create_mini_window(app)?;
            let _ = build_tray(app)?;
            let _ = register_default_hotkeys(app);
            host::init_shell_state(app.handle());
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

/// 主窗口（Blazor 全功能界面）。挂载导航白名单与「关闭 → 最小化到托盘」。
fn create_main_window(app: &tauri::App) -> tauri::Result<tauri::WebviewWindow> {
    let win = tauri::WebviewWindowBuilder::new(
        app,
        "main",
        WebviewUrl::App("index.html".into()),
    )
    .title("OBS 排障助手")
    .inner_size(MAIN_W, MAIN_H)
    .min_inner_size(MIN_W, MIN_H)
    .resizable(true)
    .fullscreen(false)
    .center()
    .on_navigation(|url| {
        // 仅允许本地资源与应用内导航；外部 http(s) 链接一律拦截，
        // 由前端通过宿主命令 shell.open 交给系统浏览器打开。
        let s = url.as_str();
        s.starts_with("asset:")
            || s.starts_with("tauri:")
            || s.starts_with("ipc:")
            || s.starts_with("http://ipc.localhost")
            || s.starts_with("http://localhost")
            || s.starts_with("https://localhost")
    })
    .build()?;

    // 关闭按钮行为：设置里勾选「关闭到托盘」时隐藏窗口而不是退出（默认开启）。
    let win_handle = win.clone();
    win.on_window_event(move |event| {
        if let tauri::WindowEvent::CloseRequested { api, .. } = event {
            if host::is_close_to_tray() {
                api.prevent_close();
                let _ = win_handle.hide();
            }
            // 未开启「关闭到托盘」时：放行默认关闭（窗口销毁），应用随最后窗口退出。
        }
    });
    Ok(win)
}

/// 迷你小窗：无边框、始终置顶、不占任务栏，加载前端 MiniPanel 页面。
///
/// 前端通过 `window.__OBS_MINI__` 标记识别本窗口，并渲染 MiniPanel 而非完整导航。
fn create_mini_window(app: &tauri::App) -> tauri::Result<tauri::WebviewWindow> {
    let win = tauri::WebviewWindowBuilder::new(
        app,
        "mini",
        WebviewUrl::App("index.html".into()),
    )
    .title("OBS 小窗")
    .inner_size(360.0, 150.0)
    .resizable(false)
    .decorations(false) // 无边框：由前端 MiniPanel 提供拖拽区（data-tauri-drag-region）
    .always_on_top(true)
    .skip_taskbar(true)
    .visible(false) // 初始隐藏，由托盘 / 热键呼出
    // 标记小窗窗口：Blazor 启动后检测该标记并 NavigateTo("/mini") 渲染 MiniPanel
    .initialization_script("window.__OBS_MINI__ = true;")
    .on_navigation(|url| {
        let s = url.as_str();
        s.starts_with("asset:")
            || s.starts_with("tauri:")
            || s.starts_with("ipc:")
            || s.starts_with("http://ipc.localhost")
            || s.starts_with("http://localhost")
            || s.starts_with("https://localhost")
    })
    .build()?;

    // 小窗位置记忆：移动时写入 prefs，呼出时恢复（等价于 Windows 侧 MiniWindowSettings）
    let mini = win.clone();
    win.on_window_event(move |event| {
        if let tauri::WindowEvent::Moved(position) = event {
            host::save_mini_position(&position);
        }
    });    Ok(mini)
}

/// 系统托盘（macOS 菜单栏图标）。
///
/// 菜单结构与 Windows 侧 TrayService.BuildMenu 对应：
///   显示主窗口 / 开始·停止录制 / 开始·停止推流 / 虚拟摄像头 / 小窗控制 / 退出
/// 菜单文案随 OBS 状态刷新（前端通过 shell.trayState 上报），未连接时禁用动作项。
fn build_tray(app: &tauri::App) -> tauri::Result<tauri::tray::TrayIcon> {
    // 初始菜单：未连接状态（host::build_tray_menu 与状态上报共用同一构造）
    let menu = host::build_tray_menu(app.handle(), false, false, false, false)?;

    let tray = TrayIconBuilder::with_id("main-tray")
        .icon(app.default_window_icon().cloned().expect("缺少应用图标"))
        .menu(&menu)
        .tooltip("OBS 排障助手")
        .show_menu_on_left_click(false)
        .on_menu_event(|app, event| {
            let action = match event.id.as_ref() {
                "show_main" => Some("showMain"),
                "toggle_record" => Some("toggleRecord"),
                "toggle_stream" => Some("toggleStream"),
                "toggle_vcam" => Some("toggleVCam"),
                "toggle_mini" => Some("toggleMini"),
                "quit" => Some("quit"),
                _ => None,
            };
            if let Some(a) = action {
                let _ = app.emit(EVT_SHELL_ACTION, ShellAction { action: a.to_string() });
            }
        })
        // 左键单击托盘图标 = 显示主窗口（等价于 Windows 双击行为）
        .on_tray_icon_event(|tray, event| {
            if let TrayIconEvent::Click {
                button: MouseButton::Left,
                button_state: MouseButtonState::Up,
                ..
            } = event
            {
                let _ = tray.app_handle().emit(
                    EVT_SHELL_ACTION,
                    ShellAction { action: "showMain".to_string() },
                );
            }
        })
        .build(app)?;

    // 交给 host 模块持有，供前端 shell.trayState 更新菜单文案 / ToolTip
    host::set_tray_handle(tray.clone());
    Ok(tray)
}

/// 注册默认全局热键（与 Windows 侧 GlobalHotkeyService 默认值一致）：
///   Ctrl+Alt+R 录制开关 / Ctrl+Alt+S 推流开关 / Ctrl+Alt+C 虚拟摄像头 /
///   Ctrl+Alt+M 小窗 / Ctrl+Alt+O 显示·隐藏主窗口。
fn register_default_hotkeys(app: &tauri::App) {
    host::apply_hotkeys(app.handle(), &host::DEFAULT_HOTKEYS);
}
