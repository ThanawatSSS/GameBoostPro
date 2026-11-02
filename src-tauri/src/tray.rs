use crate::{do_boost, do_restore, prepare_quit, store, Shared};
use tauri::{
    image::Image,
    menu::{CheckMenuItem, Menu, MenuItem},
    tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent},
    Manager,
};

fn show(app: &tauri::AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
}

fn indicator(base: &Image<'_>, color: [u8; 4]) -> Image<'static> {
    let (w, h) = (base.width(), base.height());
    let mut rgba = base.rgba().to_vec();
    let radius = (w.min(h) / 5).max(2) as i32;
    let (cx, cy) = (w as i32 - radius - 1, h as i32 - radius - 1);
    for y in (cy - radius).max(0)..=(cy + radius).min(h as i32 - 1) {
        for x in (cx - radius).max(0)..=(cx + radius).min(w as i32 - 1) {
            let d = (x - cx).pow(2) + (y - cy).pow(2);
            if d <= radius * radius {
                let i = ((y as u32 * w + x as u32) * 4) as usize;
                let inner = (radius - 1).pow(2);
                rgba[i..i + 4].copy_from_slice(if d <= inner {
                    &color
                } else {
                    &[20, 25, 23, 255]
                });
            }
        }
    }
    Image::new_owned(rgba, w, h)
}

pub fn setup(app: &mut tauri::App) -> Result<(), Box<dyn std::error::Error>> {
    let status = MenuItem::with_id(app, "status", "Game Boost Pro", false, None::<&str>)?;
    let game = MenuItem::with_id(app, "game", "Game: —", false, None::<&str>)?;
    let open = MenuItem::with_id(app, "show", "เปิดหน้าต่าง / Open", true, None::<&str>)?;
    let boost = MenuItem::with_id(app, "boost", "เปิด Boost / Start Boost", false, None::<&str>)?;
    let restore = MenuItem::with_id(app, "restore", "คืนค่า / Restore", false, None::<&str>)?;
    let auto = CheckMenuItem::with_id(
        app,
        "auto",
        "Boost อัตโนมัติ / Auto Boost",
        false,
        false,
        None::<&str>,
    )?;
    let quit = MenuItem::with_id(app, "restore-quit", "ออก / Quit", true, None::<&str>)?;
    let menu = Menu::with_items(
        app,
        &[&status, &game, &open, &boost, &restore, &auto, &quit],
    )?;
    let displayed_session =
        std::sync::Arc::new(std::sync::Mutex::new(None::<(Option<String>, u64)>));
    let clicked_session = displayed_session.clone();
    let mut builder = TrayIconBuilder::with_id("gameboostpro")
        .tooltip("Game Boost Pro")
        .menu(&menu)
        .show_menu_on_left_click(false)
        .on_tray_icon_event(|tray, event| {
            if matches!(event, TrayIconEvent::Click { button: MouseButton::Left, button_state: MouseButtonState::Up, .. }) {
                show(tray.app_handle());
            }
        })
        .on_menu_event(move |app, event| {
            let id = event.id().as_ref().to_string();
            if id == "show" { show(app); return; }
            if !["boost", "restore", "auto", "restore-quit"].contains(&id.as_str()) { return; }
            let expected_session = clicked_session.lock().ok().and_then(|s| s.clone());
            let app = app.clone();
            let state = app.state::<Shared>().inner().clone();
            std::thread::spawn(move || {
                let mut exit = false;
                let result = match state.0.lock() {
                    Ok(b) if b.shutting_down => Err("The application is closing".into()),
                    Ok(mut b) => match id.as_str() {
                        "boost" => if b.status.session.is_some() { Err("Boost is already active".into()) } else { do_boost(&mut b) },
                        "restore" => {
                            let current = b.status.session.as_ref().map(|s| (s.session_id.clone(), s.started));
                            if current.is_none() || current != expected_session { Err("The session changed; check its current status before restoring.".into()) }
                            else { do_restore(&mut b).map(|_| ()) }
                        },
                        "auto" => {
                            if !b.status.config_ready || !b.status.admin || !b.status.platform.supported || b.status.legacy_recovery {
                                Err("Automatic Boost is unavailable on this device or without Administrator access".into())
                            } else {
                                let mut config = b.status.config.clone();
                                config.auto_mode = !config.auto_mode;
                                store::save_config(&config).map(|_| { b.status.config = config; })
                            }
                        }
                        "restore-quit" => {
                            let current = b.status.session.as_ref().map(|s| (s.session_id.clone(), s.started));
                            if current != expected_session { Err("The session changed; check its current status before quitting.".into()) }
                            else { prepare_quit(&mut b).map(|_| { exit = true; }) }
                        }
                        _ => Ok(()),
                    },
                    Err(_) => Err("App state needs a restart".into()),
                };
                if let Err(e) = result {
                    if let Ok(mut b) = state.0.lock() {
                        if b.recovery_only { b.status.error = Some(e); }
                        else {
                            b.status.notice = Some(match b.status.notice.take() {
                                Some(details) => format!("{details} {e}"),
                                None => e,
                            });
                        }
                    }
                    show(&app);
                } else if exit { app.exit(0); }
            });
        });
    let base = app
        .default_window_icon()
        .map(|icon| Image::new_owned(icon.rgba().to_vec(), icon.width(), icon.height()));
    if let Some(icon) = &base {
        builder = builder.icon(icon.clone());
    }
    let tray = builder.build(app)?;
    let shared = app.state::<Shared>().inner().clone();
    std::thread::spawn(move || {
        let mut last = String::new();
        loop {
            let snapshot = shared.0.lock().ok().map(|b| {
                let mut status = b.status.clone();
                status.recovery_required = b.recovery_only;
                status
            });
            if let Some(s) = snapshot {
                let en = s.config.language == "EN";
                let warning = s.recovery_required
                    || s.notice.is_some()
                    || s.error.is_some()
                    || s.legacy_recovery
                    || !s.platform.supported;
                let active = s.session.is_some();
                let title = if warning {
                    if en {
                        "Attention needed"
                    } else {
                        "ต้องตรวจสอบ"
                    }
                } else if active {
                    if en {
                        "Boost active"
                    } else {
                        "Boost ทำงานอยู่"
                    }
                } else if en {
                    "Normal mode"
                } else {
                    "โหมดปกติ"
                };
                let game_name = if let Some(session) = &s.session {
                    if session.game_name.is_empty() {
                        if en {
                            "Waiting for game"
                        } else {
                            "รอเกมเริ่ม"
                        }
                    } else {
                        session.game_name.as_str()
                    }
                } else {
                    s.running.first().map(|v| v.name.as_str()).unwrap_or("—")
                };
                let profile = s
                    .session
                    .as_ref()
                    .map(|v| format!(" · {:?}", v.profile))
                    .unwrap_or_default();
                let tooltip = format!("Game Boost Pro · {title}\n{game_name}{profile}");
                let allowed = !s.capturing
                    && (active
                        || (s.admin
                            && s.platform.supported
                            && !s.legacy_recovery
                            && !s.recovery_required));
                let auto_allowed = s.admin && s.platform.supported && !s.legacy_recovery;
                let session_key = s
                    .session
                    .as_ref()
                    .map(|v| (v.session_id.clone(), v.started));
                let signature = format!(
                    "{tooltip}|{allowed}|{auto_allowed}|{}|{en}|{session_key:?}",
                    s.config.auto_mode
                );
                if signature != last {
                    let _ = status.set_text(title);
                    let _ = game.set_text(format!(
                        "{}: {game_name}{profile}",
                        if en { "Game" } else { "เกม" }
                    ));
                    let _ = open.set_text(if en {
                        "Open Game Boost Pro"
                    } else {
                        "เปิด Game Boost Pro"
                    });
                    let _ = boost.set_text(if en {
                        "Start Boost"
                    } else {
                        "เปิด Boost"
                    });
                    let _ = boost.set_enabled(allowed && !active);
                    let _ = restore.set_text(if en {
                        "Restore settings"
                    } else {
                        "คืนค่าปกติ"
                    });
                    let _ = restore.set_enabled(allowed && active);
                    let _ = auto.set_text(if en {
                        "Automatic Boost"
                    } else {
                        "Boost อัตโนมัติ"
                    });
                    let _ = auto.set_enabled(auto_allowed);
                    let _ = auto.set_checked(s.config.auto_mode);
                    let _ = quit.set_text(if active {
                        if en {
                            "Restore and quit"
                        } else {
                            "คืนค่าและออก"
                        }
                    } else if en {
                        "Quit"
                    } else {
                        "ออกจากแอป"
                    });
                    let _ = quit.set_enabled(!s.capturing);
                    let _ = tray.set_tooltip(Some(tooltip));
                    if let Ok(mut displayed) = displayed_session.lock() {
                        *displayed = session_key;
                    }
                    if let Some(icon) = &base {
                        let color = if warning {
                            [255, 176, 55, 255]
                        } else if active {
                            [175, 235, 70, 255]
                        } else {
                            [160, 175, 171, 255]
                        };
                        let _ = tray.set_icon(Some(indicator(icon, color)));
                    }
                    last = signature;
                }
            }
            std::thread::sleep(std::time::Duration::from_secs(1));
        }
    });
    Ok(())
}
