#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
mod capture_tool;
mod discovery;
mod engine;
mod frames;
mod model;
mod store;
mod windows;
use model::*;
use std::sync::{Arc, Mutex};
use tauri::Manager;

struct Backend {
    status: Status,
    last_ui: std::time::Instant,
    suppressed: Option<(u32, u64)>,
    exited_at: Option<std::time::Instant>,
}
#[derive(Clone)]
struct Shared(Arc<Mutex<Backend>>);
async fn work<T: Send + 'static>(
    state: Shared,
    f: impl FnOnce(&mut Backend) -> Result<T, String> + Send + 'static,
) -> Result<T, String> {
    tauri::async_runtime::spawn_blocking(move || {
        let mut b = state.0.lock().map_err(|_| "App state needs a restart")?;
        f(&mut b)
    })
    .await
    .map_err(|e| e.to_string())?
}
#[tauri::command]
async fn get_status(state: tauri::State<'_, Shared>) -> Result<Status, String> {
    work(state.inner().clone(), |b| {
        b.last_ui = std::time::Instant::now();
        Ok(b.status.clone())
    })
    .await
}
#[tauri::command]
async fn save_settings(state: tauri::State<'_, Shared>, config: Config) -> Result<(), String> {
    work(state.inner().clone(), move |b| {
        store::save_config(&config)?;
        b.status.config = config;
        Ok(())
    })
    .await
}
#[tauri::command]
async fn boost(state: tauri::State<'_, Shared>) -> Result<(), String> {
    work(state.inner().clone(), |b| {
        if b.status.capturing {
            return Err("Wait for frame recording to finish".into());
        }
        let target = selected_running(b);
        match engine::enable(&b.status.config, &windows::platform(), target, false) {
            Ok(s) => b.status.session = Some(s),
            Err(e) => {
                b.status.session = store::recovery()?;
                return Err(e);
            }
        }
        b.status.error = None;
        if let Ok((_, name)) = windows::power() {
            b.status.platform.power_name = name;
        }
        Ok(())
    })
    .await
}
fn selected_running(b: &Backend) -> Option<ProcessIdentity> {
    let selected = b
        .status
        .config
        .selected
        .as_ref()
        .and_then(|id| b.status.config.games.iter().find(|g| &g.id == id));
    if let Some(g) = selected {
        b.status
            .running
            .iter()
            .find(|p| p.path.eq_ignore_ascii_case(&g.path) || inside(&p.path, &g.directory))
            .cloned()
    } else {
        b.status.running.first().cloned()
    }
}
#[tauri::command]
async fn restore(state: tauri::State<'_, Shared>) -> Result<Vec<String>, String> {
    work(state.inner().clone(), |b| {
        if b.status.capturing {
            return Err("Wait for frame recording to finish".into());
        }
        let notes = engine::restore()?;
        b.suppressed = b.status.running.first().map(|p| (p.pid, p.started));
        b.status.session = None;
        b.status.error = None;
        if let Ok((_, name)) = windows::power() {
            b.status.platform.power_name = name;
        }
        Ok(notes)
    })
    .await
}
#[tauri::command]
async fn scan_games(state: tauri::State<'_, Shared>) -> Result<usize, String> {
    work(state.inner().clone(), |b| {
        let games = discovery::installed()?;
        let mut added = 0;
        for game in games {
            if !b
                .status
                .config
                .games
                .iter()
                .any(|g| g.launch.eq_ignore_ascii_case(&game.launch))
            {
                b.status.config.games.push(game);
                added += 1;
            }
        }
        store::save_config(&b.status.config)?;
        Ok(added)
    })
    .await
}
#[tauri::command]
async fn add_game(state: tauri::State<'_, Shared>) -> Result<Option<String>, String> {
    let selected = tauri::async_runtime::spawn_blocking(|| {
        rfd::FileDialog::new()
            .set_title("Add game / เพิ่มเกม")
            .add_filter("Game executable", &["exe"])
            .pick_file()
    })
    .await
    .map_err(|e| e.to_string())?;
    let Some(path) = selected else {
        return Ok(None);
    };
    let path = path
        .canonicalize()
        .map_err(|e| e.to_string())?
        .to_string_lossy()
        .trim_start_matches(r"\\?\")
        .to_string();
    if !store::valid_exe(&path) {
        return Err("Choose a local game .exe. Discord, TeamSpeak, NitroSense and system processes are protected.".into());
    }
    work(state.inner().clone(), move |b| {
        if let Some(g) = b
            .status
            .config
            .games
            .iter()
            .find(|g| g.path.eq_ignore_ascii_case(&path))
        {
            return Ok(Some(g.id.clone()));
        }
        let id = uuid::Uuid::new_v4().to_string();
        let p = std::path::Path::new(&path);
        b.status.config.games.push(Game {
            id: id.clone(),
            name: p.file_stem().unwrap_or_default().to_string_lossy().into(),
            source: "manual".into(),
            directory: p.parent().unwrap_or(p).to_string_lossy().into(),
            path: path.clone(),
            launch: path,
            profile: None,
        });
        b.status.config.selected = Some(id.clone());
        store::save_config(&b.status.config)?;
        Ok(Some(id))
    })
    .await
}
#[tauri::command]
async fn launch_game(state: tauri::State<'_, Shared>, id: String) -> Result<(), String> {
    work(state.inner().clone(), move |b| {
        let g = b
            .status
            .config
            .games
            .iter()
            .find(|g| g.id == id)
            .ok_or("Select a game first")?;
        if !store::valid_launch(g) || g.launch.is_empty() {
            return Err("Add the game's .exe before launching this game.".into());
        }
        if g.source == "manual" && !std::path::Path::new(&g.path).is_file() {
            return Err("Game file was moved or removed. Add the game again.".into());
        }
        windows::launch_game(&g.launch)
    })
    .await
}
#[tauri::command]
async fn open_settings(which: String) -> Result<(), String> {
    tauri::async_runtime::spawn_blocking(move || windows::open_target(&which))
        .await
        .map_err(|e| e.to_string())?
}
#[tauri::command]
async fn restart_admin(
    state: tauri::State<'_, Shared>,
    app: tauri::AppHandle,
) -> Result<(), String> {
    work(state.inner().clone(), |b| {
        if b.status.session.is_some() {
            return Err("Restore before restarting the app".into());
        }
        Ok(())
    })
    .await?;
    if windows::admin() {
        return Ok(());
    }
    windows::shell(
        &std::env::current_exe()
            .map_err(|e| e.to_string())?
            .to_string_lossy(),
        Some("runas"),
    )?;
    app.exit(0);
    Ok(())
}
#[tauri::command]
async fn capture_frames(
    state: tauri::State<'_, Shared>,
    app: tauri::AppHandle,
) -> Result<FrameResult, String> {
    let shared = state.inner().clone();
    let resources = app.path().resource_dir().map_err(|e| e.to_string())?;
    let (target, session) = work(shared.clone(), |b| {
        if b.status.capturing {
            return Err("A frame capture is already running".into());
        }
        let target = selected_running(b).ok_or("Start a game before recording frames")?;
        b.status.capturing = true;
        Ok((target, b.status.session.clone()))
    })
    .await?;
    let captured_start = session.as_ref().map(|s| s.started);
    let result = tauri::async_runtime::spawn_blocking(move || {
        frames::capture(&resources, &target, session.as_ref())
    })
    .await
    .map_err(|e| e.to_string());
    work(shared, move |b| {
        b.status.capturing = false;
        let result = result??;
        let expected = b.status.session.as_ref().map(|s| s.started);
        if expected != captured_start {
            return Err(
                "Boost state changed during capture. Record again with a stable state.".into(),
            );
        }
        b.status.frames.push(result.clone());
        if b.status.frames.len() > 40 {
            b.status.frames.remove(0);
        }
        Ok(result)
    })
    .await
}
fn monitor(shared: Shared) {
    std::thread::spawn(move || {
        let mut previous = None;
        loop {
            let active = if let Ok(mut b) = shared.0.try_lock() {
                let start = std::time::Instant::now();
                b.status.running = discovery::running(&b.status.config);
                b.status.scan_ms = start.elapsed().as_secs_f64() * 1000.;
                if b.status.config.telemetry && b.last_ui.elapsed().as_secs() < 8 {
                    let (cpu, ram, used, total) = windows::metrics(&mut previous);
                    b.status.cpu = cpu;
                    b.status.ram_percent = ram;
                    b.status.ram_used_gb = used;
                    b.status.ram_total_gb = total;
                } else {
                    previous = None;
                }
                if !b.status.capturing {
                    if let Some(mut session) = b.status.session.clone() {
                        if session.game.is_none() {
                            if let Some(game) = selected_running(&b) {
                                match engine::attach(&mut session, &b.status.config, &game) {
                                    Ok(()) => b.status.session = Some(session),
                                    Err(e) => {
                                        b.status.error = Some(e);
                                        if let Ok(s) = store::recovery() {
                                            b.status.session = s;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if let Some(session) = b.status.session.clone() {
                        if session.automatic
                            && session.game.as_ref().is_some_and(|g| !windows::same(g))
                        {
                            if b.exited_at.is_some_and(|t| t.elapsed().as_secs() >= 10) {
                                match engine::restore() {
                                    Ok(_) => {
                                        b.status.session = None;
                                        b.status.error = None;
                                    }
                                    Err(e) => b.status.error = Some(e),
                                }
                                b.exited_at = None;
                            } else {
                                b.exited_at.get_or_insert_with(std::time::Instant::now);
                            }
                        } else {
                            b.exited_at = None;
                        }
                    } else if b.status.config.auto_mode
                        && !b.status.legacy_recovery
                        && b.status.admin
                        && b.status.error.is_none()
                    {
                        if let Some(g) = b.status.running.first().cloned() {
                            if b.suppressed != Some((g.pid, g.started)) {
                                match engine::enable(
                                    &b.status.config,
                                    &windows::platform(),
                                    Some(g),
                                    true,
                                ) {
                                    Ok(s) => b.status.session = Some(s),
                                    Err(e) => {
                                        b.status.error = Some(e);
                                        if let Ok(s) = store::recovery() {
                                            b.status.session = s;
                                        }
                                    }
                                }
                            }
                        } else {
                            b.suppressed = None;
                        }
                    }
                }
                b.status.session.is_some()
            } else {
                true
            };
            std::thread::sleep(std::time::Duration::from_millis(if active {
                3000
            } else {
                1500
            }));
        }
    });
}
fn main() {
    #[cfg(debug_assertions)]
    if std::env::args().any(|a| a == "--fixture-game") {
        std::thread::sleep(std::time::Duration::from_secs(180));
        return;
    }
    let _instance = match windows::instance() {
        Ok(h) => h,
        Err(e) => {
            unsafe {
                windows_sys::Win32::UI::WindowsAndMessaging::MessageBoxW(
                    std::ptr::null_mut(),
                    windows::wide(&e).as_ptr(),
                    windows::wide("Game Boost Pro").as_ptr(),
                    0x40,
                );
            }
            return;
        }
    };
    #[cfg(debug_assertions)]
    if std::env::args().any(|a| a == "--engine-check") {
        let result = engine_check();
        if let Ok(dir) = windows::data_dir() {
            let _ = store::atomic_json(&dir.join("rust-engine-check.json"), &result);
        }
        std::process::exit(if result.is_ok() { 0 } else { 1 });
    }
    let mut status = Status {
        admin: windows::admin(),
        platform: windows::platform(),
        legacy_recovery: store::legacy_pending(),
        ..Status::default()
    };
    match store::read_config() {
        Ok(c) => status.config = c,
        Err(e) => status.error = Some(e),
    };
    match store::recovery() {
        Ok(s) => status.session = s,
        Err(e) => status.error = Some(e),
    };
    let shared = Shared(Arc::new(Mutex::new(Backend {
        status,
        last_ui: std::time::Instant::now(),
        suppressed: None,
        exited_at: None,
    })));
    monitor(shared.clone());
    let context = tauri::generate_context!();
    #[cfg(debug_assertions)]
    let context = {
        let mut context = context;
        if std::env::args().any(|a| a == "--ui-test") {
            context.config_mut().app.windows[0].additional_browser_args =
                Some("--remote-debugging-port=9223 --remote-debugging-address=127.0.0.1".into());
            context.config_mut().app.windows[0].data_directory = Some(
                windows::data_dir()
                    .expect("Test profile directory")
                    .join("qa-webview"),
            );
        }
        context
    };
    tauri::Builder::default()
        .manage(shared)
        .setup(|app| {
            use tauri::{
                menu::{Menu, MenuItem},
                tray::{TrayIconBuilder, TrayIconEvent},
            };
            let show = MenuItem::with_id(app, "show", "เปิดหน้าต่าง / Show", true, None::<&str>)?;
            let quit = MenuItem::with_id(
                app,
                "restore-quit",
                "คืนค่าและออก / Restore & quit",
                true,
                None::<&str>,
            )?;
            let menu = Menu::with_items(app, &[&show, &quit])?;
            let mut tray = TrayIconBuilder::with_id("gameboostpro")
                .tooltip("Game Boost Pro 4.0")
                .menu(&menu)
                .show_menu_on_left_click(false)
                .on_tray_icon_event(|tray, event| {
                    if matches!(event, TrayIconEvent::Click { .. }) {
                        if let Some(w) = tray.app_handle().get_webview_window("main") {
                            let _ = w.show();
                            let _ = w.set_focus();
                        }
                    }
                })
                .on_menu_event(|app, event| match event.id().as_ref() {
                    "show" => {
                        if let Some(w) = app.get_webview_window("main") {
                            let _ = w.show();
                            let _ = w.set_focus();
                        }
                    }
                    "restore-quit" => {
                        let app = app.clone();
                        let state = app.state::<Shared>().inner().clone();
                        std::thread::spawn(move || {
                            if let Ok(mut b) = state.0.lock() {
                                if b.status.capturing {
                                    b.status.error =
                                        Some("Wait for frame recording to finish".into());
                                } else {
                                    let restored = if b.status.session.is_some() {
                                        engine::restore().map(|_| ())
                                    } else {
                                        Ok(())
                                    };
                                    match restored {
                                        Ok(()) => {
                                            app.exit(0);
                                            return;
                                        }
                                        Err(e) => b.status.error = Some(e),
                                    }
                                }
                                if let Some(w) = app.get_webview_window("main") {
                                    let _ = w.show();
                                    let _ = w.set_focus();
                                }
                            }
                        });
                    }
                    _ => {}
                });
            if let Some(icon) = app.default_window_icon() {
                tray = tray.icon(icon.clone());
            }
            tray.build(app)?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            get_status,
            save_settings,
            boost,
            restore,
            scan_games,
            add_game,
            launch_game,
            open_settings,
            restart_admin,
            capture_frames
        ])
        .on_window_event(|window, event| {
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                let state = window.state::<Shared>();
                if let Ok(b) = state.0.try_lock() {
                    if b.status.session.is_some() || b.status.capturing {
                        api.prevent_close();
                        if b.status.capturing {
                            if let Some(webview) = window.app_handle().get_webview_window("main") {
                                let _ = webview
                                    .eval("window.dispatchEvent(new Event('close-blocked'))");
                            }
                        } else {
                            let _ = window.hide();
                        }
                    }
                } else {
                    api.prevent_close();
                };
            }
        })
        .run(context)
        .expect("Game Boost Pro could not start");
}
#[cfg(debug_assertions)]
fn engine_check() -> Result<serde_json::Value, String> {
    let platform = windows::platform();
    let c = Config {
        master: Profile::Quality,
        auto_mode: false,
        ..Config::default()
    };
    let before = windows::power()?;
    let original = store::get_value(store::GAMEBAR, "AutoGameModeEnabled")?;
    let s = engine::enable(&c, &platform, None, false)?;
    let applied = store::get_value(store::GAMEBAR, "AutoGameModeEnabled")?;
    engine::restore()?;
    let power = windows::power()?;
    let after = store::get_value(store::GAMEBAR, "AutoGameModeEnabled")?;
    if before.0 != power.0 || original != after || store::recovery()?.is_some() {
        return Err("Round-trip restoration did not match original settings".into());
    }
    Ok(
        serde_json::json!({"version":"4.0.0","realWindowsRoundTrip":true,"gameModeApplied":applied==RegValue::Dword(1),"originalRegistryRestored":true,"originalPowerRestored":true,"recoveryCleared":true,"changes":s.changes.len()}),
    )
}
