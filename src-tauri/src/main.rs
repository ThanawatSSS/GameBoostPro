#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
mod capture_tool;
mod discovery;
mod engine;
mod frames;
mod model;
mod store;
mod tray;
mod windows;
use model::*;
use sha2::{Digest, Sha256};
use std::sync::{Arc, Mutex};
use tauri::Manager;

struct Backend {
    status: Status,
    shutting_down: bool,
    recovery_only: bool,
    auto_restore_retry: bool,
    last_ui: std::time::Instant,
    suppressed: Option<(u32, u64)>,
    exited_at: Option<std::time::Instant>,
}
#[derive(Clone)]
struct Shared(Arc<Mutex<Backend>>);
fn session_revision(session: &Session) -> Result<String, String> {
    let bytes = serde_json::to_vec(session).map_err(|e| e.to_string())?;
    Ok(format!("{:x}", Sha256::digest(bytes)))
}
fn config_revision(config: &Config) -> Result<String, String> {
    let bytes = serde_json::to_vec(config).map_err(|e| e.to_string())?;
    Ok(format!("{:x}", Sha256::digest(bytes)))
}
fn verify_config_revision(config: &Config, expected: &str) -> Result<(), String> {
    if config_revision(config)? != expected {
        return Err("Settings changed elsewhere. Refresh and retry.".into());
    }
    Ok(())
}
fn require_config(b: &Backend) -> Result<(), String> {
    if b.status.config_ready {
        Ok(())
    } else {
        Err("Settings could not be loaded. Retry loading before changing them.".into())
    }
}
fn add_notice(b: &mut Backend, message: String) {
    b.status.notice = Some(match b.status.notice.take() {
        Some(previous) => format!("{previous} {message}"),
        None => message,
    });
}
async fn work<T: Send + 'static>(
    state: Shared,
    f: impl FnOnce(&mut Backend) -> Result<T, String> + Send + 'static,
) -> Result<T, String> {
    tauri::async_runtime::spawn_blocking(move || {
        let mut b = state.0.lock().map_err(|_| "App state needs a restart")?;
        if b.shutting_down {
            return Err("The application is closing. Try again if it remains open.".into());
        }
        f(&mut b)
    })
    .await
    .map_err(|e| e.to_string())?
}
#[tauri::command]
async fn get_status(state: tauri::State<'_, Shared>) -> Result<Status, String> {
    work(state.inner().clone(), |b| {
        b.last_ui = std::time::Instant::now();
        let mut status = b.status.clone();
        status.recovery_required = b.recovery_only;
        status.recovery_uncertain = status
            .session
            .as_ref()
            .is_some_and(Session::recovery_uncertain);
        status.recovery_revision = status.session.as_ref().map(session_revision).transpose()?;
        status.config_revision = config_revision(&status.config)?;
        Ok(status)
    })
    .await
}
#[tauri::command]
async fn save_settings(
    state: tauri::State<'_, Shared>,
    config: Config,
    expected_revision: String,
) -> Result<(), String> {
    work(state.inner().clone(), move |b| {
        require_config(b)?;
        verify_config_revision(&b.status.config, &expected_revision)?;
        store::save_config(&config)?;
        b.status.config = config;
        Ok(())
    })
    .await
}
#[tauri::command]
async fn boost(state: tauri::State<'_, Shared>) -> Result<(), String> {
    work(state.inner().clone(), do_boost).await
}
fn do_boost(b: &mut Backend) -> Result<(), String> {
    require_config(b)?;
    if b.status.session.is_some() {
        return Err("Boost is already active".into());
    }
    if b.status.capturing {
        return Err("Wait for frame recording to finish".into());
    }
    let target = selected_running(b);
    match engine::enable(&b.status.config, &windows::platform(), target, false) {
        Ok(s) => {
            b.status.session = Some(s);
            b.recovery_only = false;
        }
        Err(e) => {
            match store::recovery() {
                Ok(session) => {
                    b.recovery_only = session.is_some();
                    b.status.session = session;
                }
                Err(recovery_error) => {
                    b.recovery_only = true;
                    b.status.error = Some(recovery_error);
                }
            }
            return Err(e);
        }
    }
    b.status.error = None;
    if let Ok((_, name)) = windows::power() {
        b.status.platform.power_name = name;
    }
    Ok(())
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
async fn restore(
    state: tauri::State<'_, Shared>,
    session_id: Option<String>,
    started: u64,
) -> Result<Vec<String>, String> {
    work(state.inner().clone(), move |b| {
        let matches = b
            .status
            .session
            .as_ref()
            .is_some_and(|s| s.session_id == session_id && s.started == started);
        if !matches {
            return Err("The session changed. Review it before restoring.".into());
        }
        do_restore(b)
    })
    .await
}
#[tauri::command]
async fn resolve_recovery(
    state: tauri::State<'_, Shared>,
    session_id: Option<String>,
    started: u64,
    revision: String,
) -> Result<Vec<String>, String> {
    work(state.inner().clone(), move |b| {
        let session = store::recovery()?.ok_or("Recovery session is no longer active")?;
        if session.session_id != session_id
            || session.started != started
            || session_revision(&session)? != revision
        {
            b.status.session = Some(session);
            b.recovery_only = true;
            return Err(
                "Recovery details changed. Review the current settings before confirming again."
                    .into(),
            );
        }
        do_restore_with_journal(b, true, session)
    })
    .await
}
#[tauri::command]
async fn reload_recovery(state: tauri::State<'_, Shared>) -> Result<(), String> {
    work(state.inner().clone(), |b| {
        if b.status.session.is_some() {
            return Err("Review the current recovery session first".into());
        }
        let session = store::recovery()?;
        b.recovery_only = session.is_some();
        b.status.session = session;
        b.status.error = None;
        Ok(())
    })
    .await
}
fn do_restore(b: &mut Backend) -> Result<Vec<String>, String> {
    let session = store::recovery()?.ok_or("No recovery session was found")?;
    let matches = b
        .status
        .session
        .as_ref()
        .is_some_and(|s| s.session_id == session.session_id && s.started == session.started);
    if !matches {
        b.status.session = Some(session);
        b.recovery_only = true;
        return Err("The recovery session changed. Review it before restoring.".into());
    }
    do_restore_with_journal(b, false, session)
}
fn do_restore_with_journal(
    b: &mut Backend,
    allow_uncertain: bool,
    session: Session,
) -> Result<Vec<String>, String> {
    if b.status.capturing {
        return Err("Wait for frame recording to finish".into());
    }
    b.recovery_only = true;
    let restored = engine::restore_from(session, allow_uncertain);
    let notes = match restored {
        Ok(notes) => notes,
        Err(e) => {
            if let Ok(Some(session)) = store::recovery() {
                b.status.session = Some(session);
            }
            b.auto_restore_retry = b
                .status
                .session
                .as_ref()
                .is_some_and(|s| s.automatic && !s.recovery_uncertain());
            if b.auto_restore_retry {
                b.exited_at = Some(std::time::Instant::now());
            }
            b.status.error = Some(e.clone());
            return Err(e);
        }
    };
    b.suppressed = b
        .status
        .session
        .as_ref()
        .and_then(|s| s.game.as_ref())
        .or_else(|| b.status.running.first())
        .map(|p| (p.pid, p.started));
    b.status.session = None;
    b.recovery_only = false;
    b.auto_restore_retry = false;
    b.status.error = None;
    if !notes.is_empty() {
        add_notice(b, notes.join(" "));
    }
    if let Ok((_, name)) = windows::power() {
        b.status.platform.power_name = name;
    }
    Ok(notes)
}
#[tauri::command]
async fn quit_app(
    state: tauri::State<'_, Shared>,
    app: tauri::AppHandle,
    session_id: Option<String>,
    started: Option<u64>,
) -> Result<(), String> {
    work(state.inner().clone(), move |b| {
        let current = b
            .status
            .session
            .as_ref()
            .map(|s| (s.session_id.clone(), s.started));
        let expected = started.map(|time| (session_id, time));
        if current != expected {
            return Err("The session changed. Review it before quitting.".into());
        }
        prepare_quit(b)
    })
    .await?;
    app.exit(0);
    Ok(())
}
#[tauri::command]
async fn dismiss_notice(state: tauri::State<'_, Shared>) -> Result<(), String> {
    work(state.inner().clone(), |b| {
        b.status.notice = None;
        Ok(())
    })
    .await
}
#[tauri::command]
async fn reload_config(state: tauri::State<'_, Shared>) -> Result<(), String> {
    work(state.inner().clone(), |b| {
        let config = store::read_config()?;
        b.status.config = config;
        b.status.config_ready = true;
        b.status.config_error = None;
        Ok(())
    })
    .await
}
fn prepare_quit(b: &mut Backend) -> Result<(), String> {
    let pending = match store::recovery() {
        Ok(session) => session,
        Err(e) => {
            b.recovery_only = true;
            if b.status.session.as_ref().is_some_and(|s| s.automatic) {
                b.auto_restore_retry = true;
                b.exited_at = Some(std::time::Instant::now());
            }
            return Err(e);
        }
    };
    if let (Some(current), Some(saved)) = (&b.status.session, &pending) {
        if current.session_id != saved.session_id || current.started != saved.started {
            b.status.session = pending;
            b.recovery_only = true;
            return Err("The recovery session changed. Review it before quitting.".into());
        }
    }
    let has_pending = pending.is_some();
    let suppress_after_restore = b
        .status
        .session
        .as_ref()
        .and_then(|s| s.game.as_ref())
        .or_else(|| b.status.running.first())
        .map(|p| (p.pid, p.started));
    let mut notes = Vec::new();
    let result = prepare_quit_with(
        b,
        || Ok(has_pending),
        || {
            engine::restore_from(pending.ok_or("Recovery session is missing")?, false).map(|v| {
                notes = v;
            })
        },
    );
    if result.is_ok() && !notes.is_empty() {
        b.shutting_down = false;
        b.recovery_only = false;
        b.auto_restore_retry = false;
        b.suppressed = suppress_after_restore;
        b.status.notice = Some(notes.join(" "));
        return Err(
            "Some external changes were preserved. Review the notice, then quit again.".into(),
        );
    }
    if result.is_err() {
        if let Ok(Some(session)) = store::recovery() {
            if session.recovery_uncertain() {
                b.auto_restore_retry = false;
            }
            b.status.session = Some(session);
        }
    }
    result
}
fn prepare_quit_with(
    b: &mut Backend,
    recovery_pending: impl FnOnce() -> Result<bool, String>,
    restore: impl FnOnce() -> Result<(), String>,
) -> Result<(), String> {
    if b.shutting_down {
        return Err("The application is already closing".into());
    }
    if b.status.capturing {
        return Err("Wait for frame recording to finish".into());
    }
    let previous_recovery_only = b.recovery_only;
    let had_session = b.status.session.is_some();
    let mut saw_pending = false;
    b.shutting_down = true;
    b.recovery_only = true;
    let result = (|| {
        let pending = recovery_pending()?;
        saw_pending = pending;
        if pending && !had_session {
            return Err(
                "Recovery appeared after the last status update; review it before quitting.".into(),
            );
        }
        if pending {
            restore()?;
            b.status.session = None;
        } else if b.status.session.is_some() {
            return Err("Recovery state is missing; cannot safely restore before quitting.".into());
        }
        Ok(())
    })();
    if result.is_err() {
        b.shutting_down = false;
        if b.status.session.as_ref().is_some_and(|s| s.automatic) {
            b.auto_restore_retry = true;
            b.exited_at = Some(std::time::Instant::now());
        }
        if !had_session && !saw_pending {
            b.recovery_only = previous_recovery_only;
        }
    }
    result
}
#[cfg(test)]
mod lifecycle_tests {
    use super::*;

    #[test]
    fn stale_config_cannot_overwrite_a_tray_change() {
        let mut config = Config::default();
        let revision = config_revision(&config).unwrap();
        assert!(verify_config_revision(&config, &revision).is_ok());
        config.auto_mode = !config.auto_mode;
        assert!(verify_config_revision(&config, &revision).is_err());
    }
    #[test]
    fn linking_steam_executable_keeps_override_and_launcher() {
        let mut config = Config::default();
        config.games.push(Game {
            id: "steam-1".into(),
            name: "Example".into(),
            source: "steam".into(),
            path: String::new(),
            launch: "steam://rungameid/1".into(),
            directory: r"C:\Games\Example".into(),
            profile: Some(Profile::Performance),
        });
        let path = r"C:\Games\Example\bin\game.exe";
        assert_eq!(
            link_existing_game(&mut config, path).as_deref(),
            Some("steam-1")
        );
        assert_eq!(config.games.len(), 1);
        assert_eq!(config.games[0].launch, "steam://rungameid/1");
        assert_eq!(config.games[0].profile, Some(Profile::Performance));
        assert_eq!(profile_for(&config, path), Profile::Performance);
    }

    fn backend() -> Backend {
        Backend {
            status: Status::default(),
            shutting_down: false,
            recovery_only: false,
            auto_restore_retry: false,
            last_ui: std::time::Instant::now(),
            suppressed: None,
            exited_at: None,
        }
    }
    fn session() -> Session {
        Session {
            version: 4,
            recovery_schema: Some(1),
            session_id: Some(uuid::Uuid::new_v4().to_string()),
            owner: "test".into(),
            started: 1,
            profile: Profile::Balanced,
            automatic: true,
            game: None,
            game_name: String::new(),
            power_before: uuid::Uuid::nil().to_string(),
            power_applied: uuid::Uuid::nil().to_string(),
            power_restored: false,
            power_restore_started: false,
            restore_notes: vec![],
            changes: vec![],
            process: None,
            notes: vec![],
        }
    }
    #[test]
    fn quit_reserves_shutdown_after_restoration() {
        let mut b = backend();
        b.status.session = Some(session());
        let mut restored = false;
        prepare_quit_with(
            &mut b,
            || Ok(true),
            || {
                restored = true;
                Ok(())
            },
        )
        .unwrap();
        assert!(restored);
        assert!(b.shutting_down);
        assert!(prepare_quit_with(&mut b, || Ok(false), || Ok(())).is_err());
    }
    #[test]
    fn failed_or_unreadable_recovery_keeps_app_open_for_retry() {
        let mut b = backend();
        b.status.session = Some(session());
        assert!(prepare_quit_with(&mut b, || Ok(true), || Err("restore failed".into())).is_err());
        assert!(!b.shutting_down);
        assert!(b.recovery_only);
        assert!(b.auto_restore_retry);
        assert!(
            prepare_quit_with(&mut b, || Err("unreadable recovery".into()), || Ok(())).is_err()
        );
        assert!(!b.shutting_down);
        b.status.capturing = true;
        assert!(prepare_quit_with(
            &mut b,
            || panic!("must not read recovery during capture"),
            || Ok(())
        )
        .is_err());
        assert!(!b.shutting_down);
    }
    #[test]
    fn stale_boost_does_not_modify_an_active_session() {
        let mut b = backend();
        b.status.session = Some(session());
        assert!(do_boost(&mut b).is_err());
        assert!(b.status.session.is_some());
        assert!(!b.recovery_only);
    }
    #[test]
    fn quit_does_not_restore_an_unseen_recovery() {
        let mut b = backend();
        assert!(prepare_quit_with(
            &mut b,
            || Ok(true),
            || panic!("unseen recovery must not be restored")
        )
        .is_err());
        assert!(!b.shutting_down);
        assert!(b.recovery_only);
    }
}
#[tauri::command]
async fn scan_games(state: tauri::State<'_, Shared>) -> Result<ScanReport, String> {
    // Discovery can involve slow filesystem calls. Keep the backend available for
    // restore and quit while it runs, then merge into the latest configuration.
    let games = tauri::async_runtime::spawn_blocking(discovery::installed)
        .await
        .map_err(|e| e.to_string())??;
    work(state.inner().clone(), move |b| {
        require_config(b)?;
        let mut added = 0;
        let mut skipped = 0;
        let mut config = b.status.config.clone();
        let mut ids: std::collections::HashSet<String> =
            config.games.iter().map(|g| g.id.clone()).collect();
        let mut launches: std::collections::HashSet<String> = config
            .games
            .iter()
            .map(|g| g.launch.to_ascii_lowercase())
            .collect();
        for game in games {
            let launch = game.launch.to_ascii_lowercase();
            if !launches.contains(&launch) {
                if !store::valid_game(&game)
                    || config.games.len() >= 2000
                    || !ids.insert(game.id.clone())
                {
                    skipped += 1;
                    continue;
                }
                launches.insert(launch);
                config.games.push(game);
                added += 1;
            }
        }
        store::save_config(&config)?;
        b.status.config = config;
        Ok(ScanReport { added, skipped })
    })
    .await
}
#[derive(serde::Serialize)]
struct ScanReport {
    added: usize,
    skipped: usize,
}
fn link_existing_game(config: &mut Config, path: &str) -> Option<String> {
    let matches: Vec<_> = config
        .games
        .iter()
        .filter(|g| ["steam", "epic"].contains(&g.source.as_str()) && inside(path, &g.directory))
        .map(|g| g.id.clone())
        .collect();
    let [id] = matches.as_slice() else {
        return None;
    };
    config.games.iter_mut().find(|g| &g.id == id)?.path = path.to_owned();
    Some(id.clone())
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
        require_config(b)?;
        if let Some(g) = b
            .status
            .config
            .games
            .iter()
            .find(|g| g.path.eq_ignore_ascii_case(&path))
        {
            return Ok(Some(g.id.clone()));
        }
        // An explicitly chosen executable can complete an existing launcher
        // entry without losing its profile or creating a second game.
        let mut config = b.status.config.clone();
        if let Some(id) = link_existing_game(&mut config, &path) {
            store::save_config(&config)?;
            b.status.config = config;
            return Ok(Some(id));
        }
        let id = uuid::Uuid::new_v4().to_string();
        let p = std::path::Path::new(&path);
        config.games.push(Game {
            id: id.clone(),
            name: p.file_stem().unwrap_or_default().to_string_lossy().into(),
            source: "manual".into(),
            directory: p.parent().unwrap_or(p).to_string_lossy().into(),
            path: path.clone(),
            launch: path,
            profile: None,
        });
        store::save_config(&config)?;
        b.status.config = config;
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
    if windows::admin() {
        return Ok(());
    }
    let executable = std::env::current_exe().map_err(|e| e.to_string())?;
    let shared = state.inner().clone();
    work(shared.clone(), |b| {
        if b.status.capturing {
            return Err("Wait for frame recording to finish".into());
        }
        b.shutting_down = true;
        Ok(())
    })
    .await?;
    let result = windows::shell_with_args(
        &executable.to_string_lossy(),
        Some("runas"),
        Some("--admin-handoff"),
    );
    if let Err(e) = result {
        if let Ok(mut b) = shared.0.lock() {
            b.shutting_down = false;
        }
        return Err(e);
    }
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
                if !b.shutting_down && !b.status.capturing {
                    if let Some(mut session) = b.status.session.clone() {
                        if session.game.is_none() && !b.recovery_only {
                            if let Some(game) = selected_running(&b) {
                                match engine::attach(&mut session, &b.status.config, &game) {
                                    Ok(()) => b.status.session = Some(session),
                                    Err(e) => {
                                        b.status.error = Some(e);
                                        b.recovery_only = true;
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
                            && (b.auto_restore_retry
                                || (!b.recovery_only
                                    && session.game.as_ref().is_some_and(|g| {
                                        matches!(windows::same_for_restore(g), Ok(false))
                                    })))
                        {
                            if b.exited_at.is_some_and(|t| t.elapsed().as_secs() >= 10) {
                                match engine::restore() {
                                    Ok(notes) => {
                                        if b.auto_restore_retry {
                                            b.suppressed =
                                                session.game.as_ref().map(|p| (p.pid, p.started));
                                        }
                                        b.status.session = None;
                                        b.recovery_only = false;
                                        b.auto_restore_retry = false;
                                        b.status.error = None;
                                        if !notes.is_empty() {
                                            add_notice(&mut b, notes.join(" "));
                                        }
                                    }
                                    Err(e) => {
                                        b.status.error = Some(e);
                                        b.recovery_only = true;
                                        if let Ok(Some(recovery)) = store::recovery() {
                                            b.status.session = Some(recovery);
                                        }
                                        b.auto_restore_retry = !b
                                            .status
                                            .session
                                            .as_ref()
                                            .is_some_and(Session::recovery_uncertain);
                                    }
                                }
                                b.exited_at = if b.auto_restore_retry {
                                    Some(std::time::Instant::now())
                                } else {
                                    None
                                };
                            } else {
                                b.exited_at.get_or_insert_with(std::time::Instant::now);
                            }
                        } else {
                            b.exited_at = None;
                        }
                    } else if b.status.config.auto_mode
                        && b.status.config_ready
                        && !b.recovery_only
                        && !b.status.legacy_recovery
                        && b.status.admin
                        && b.status.error.is_none()
                        && b.status.notice.is_none()
                    {
                        if let Some(g) = b.status.running.first().cloned() {
                            if b.suppressed != Some((g.pid, g.started)) {
                                match engine::enable(
                                    &b.status.config,
                                    &windows::platform(),
                                    Some(g),
                                    true,
                                ) {
                                    Ok(s) => {
                                        b.status.session = Some(s);
                                        b.recovery_only = false;
                                    }
                                    Err(e) => match store::recovery() {
                                        Ok(Some(s)) => {
                                            b.status.session = Some(s);
                                            b.recovery_only = true;
                                            b.status.error = Some(e);
                                        }
                                        Ok(None) => add_notice(&mut b, e),
                                        Err(recovery_error) => {
                                            b.recovery_only = true;
                                            b.status.error = Some(recovery_error);
                                        }
                                    },
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
    let _instance = match windows::instance(std::env::args().any(|a| a == "--admin-handoff")) {
        Ok(h) => h,
        Err(e) => {
            if e == "EXISTING_INSTANCE_SHOWN" {
                return;
            }
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
        Ok(c) => {
            status.config = c;
            status.config_ready = true;
        }
        Err(e) => status.config_error = Some(e),
    };
    let recovery_only = match store::recovery() {
        Ok(s) => {
            let pending = s.is_some();
            status.session = s;
            pending
        }
        Err(e) => {
            status.error = Some(e);
            true
        }
    };
    let shared = Shared(Arc::new(Mutex::new(Backend {
        status,
        shutting_down: false,
        recovery_only,
        auto_restore_retry: false,
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
        .setup(tray::setup)
        .invoke_handler(tauri::generate_handler![
            get_status,
            save_settings,
            boost,
            restore,
            resolve_recovery,
            reload_recovery,
            dismiss_notice,
            reload_config,
            quit_app,
            scan_games,
            add_game,
            launch_game,
            open_settings,
            restart_admin,
            capture_frames
        ])
        .on_window_event(|window, event| {
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                api.prevent_close();
                let _ = window.hide();
            } else if let tauri::WindowEvent::Resized(_) = event {
                if window.is_minimized().unwrap_or(false) {
                    let _ = window.hide();
                }
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
        serde_json::json!({"version":env!("CARGO_PKG_VERSION"),"realWindowsRoundTrip":true,"gameModeApplied":applied==RegValue::Dword(1),"originalRegistryRestored":true,"originalPowerRestored":true,"recoveryCleared":true,"changes":s.changes.len()}),
    )
}
