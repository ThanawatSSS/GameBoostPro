use crate::{model::*, windows};
use std::{fs, path::Path};
use winreg::{enums::*, RegKey};
pub const GAMEBAR: &str = r"Software\Microsoft\GameBar";
pub const DVR: &str = r"System\GameConfigStore";
pub const CAPTURE: &str = r"Software\Microsoft\Windows\CurrentVersion\GameDVR";
pub const GPU: &str = r"Software\Microsoft\DirectX\UserGpuPreferences";
const RECOVERY: &str = r"SOFTWARE\GameBoostPro";
const CONFIG_LIMIT: u64 = 2_000_000;

pub fn read_config() -> Result<Config, String> {
    let path = windows::data_dir()?.join("config-v4.json");
    if path.is_file() {
        let c: Config = read_json(&path, CONFIG_LIMIT)?;
        validate_config(&c)?;
        return Ok(c);
    }
    let mut c = Config::default();
    let old = windows::data_dir()?.join("config-pro.json");
    if old.is_file() {
        let raw: serde_json::Value = read_json(&old, 2_000_000)?;
        c.language = raw["Language"]
            .as_str()
            .filter(|s| *s == "EN")
            .unwrap_or("TH")
            .into();
        c.master = match raw["DefaultPreset"].as_str() {
            Some("Light") => Profile::Quality,
            Some("Performance") => Profile::Performance,
            _ => Profile::Balanced,
        };
        // Automatic changes stay opt-in after a major engine migration.
        c.game_mode = raw["EnableWindowsGameMode"].as_bool().unwrap_or(true);
        c.capture_off = raw["DisableBackgroundCapture"].as_bool().unwrap_or(true);
        c.gpu_preference = raw["PreferHighPerformanceGpu"].as_bool().unwrap_or(true);
        c.keep_power = raw["PowerPlanMode"].as_str() != Some("Ultimate");
        if let Some(games) = raw["ManualGames"].as_array() {
            for g in games.iter().take(2000) {
                if let Some(p) = g["LaunchTarget"].as_str() {
                    if valid_exe(p) {
                        let name = g["DisplayName"].as_str().unwrap_or("Game");
                        c.games.push(Game {
                            id: uuid::Uuid::new_v4().to_string(),
                            name: name.chars().take(100).collect(),
                            source: "manual".into(),
                            path: p.into(),
                            launch: p.into(),
                            directory: Path::new(p)
                                .parent()
                                .map(|p| p.to_string_lossy().into())
                                .unwrap_or_default(),
                            profile: raw["GamePresets"]
                                .as_object()
                                .and_then(|values| values.get(&format!("exe:{}", p.to_lowercase())))
                                .and_then(|v| v.as_str())
                                .map(legacy_profile),
                        });
                    }
                }
            }
        }
        c.selected = raw["GamePath"]
            .as_str()
            .and_then(|p| c.games.iter().find(|g| g.path.eq_ignore_ascii_case(p)))
            .map(|g| g.id.clone());
    }
    Ok(c)
}
fn legacy_profile(value: &str) -> Profile {
    match value {
        "Light" => Profile::Quality,
        "Performance" => Profile::Performance,
        _ => Profile::Balanced,
    }
}
pub fn valid_exe(path: &str) -> bool {
    let p = Path::new(path);
    p.is_absolute()
        && !path.starts_with("\\\\")
        && !path.contains('\0')
        && path.len() < 32700
        && p.extension().is_some_and(|s| s.eq_ignore_ascii_case("exe"))
        && p.file_name()
            .is_some_and(|s| !protected(&s.to_string_lossy()))
        && !path.contains('"')
}
pub fn validate_config(c: &Config) -> Result<(), String> {
    if !["TH", "EN"].contains(&c.language.as_str()) || c.games.len() > 2000 {
        return Err("Invalid settings".into());
    }
    let mut ids = std::collections::HashSet::new();
    for g in &c.games {
        if !ids.insert(&g.id) || !valid_game(g) {
            return Err("Invalid game library entry".into());
        }
    }
    if let Some(id) = &c.selected {
        if !c.games.iter().any(|g| &g.id == id) {
            return Err("Selected game is not in the library".into());
        }
    }
    Ok(())
}
pub fn valid_game(g: &Game) -> bool {
    g.id.len() <= 100
        && !g.name.is_empty()
        && g.name.len() <= 400
        && g.directory.len() <= 32700
        && (g.path.is_empty() || valid_exe(&g.path))
        && valid_launch(g)
}
pub fn valid_launch(g: &Game) -> bool {
    if g.launch.is_empty() {
        return true;
    }
    if g.source == "steam" {
        return g
            .launch
            .strip_prefix("steam://rungameid/")
            .is_some_and(|id| {
                !id.is_empty() && id.len() < 12 && id.chars().all(|c| c.is_ascii_digit())
            });
    }
    if g.source == "epic" {
        return g
            .launch
            .strip_prefix("com.epicgames.launcher://apps/")
            .and_then(|s| s.strip_suffix("?action=launch&silent=true"))
            .is_some_and(|id| {
                !id.is_empty()
                    && id.len() < 200
                    && id
                        .chars()
                        .all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
            });
    }
    g.launch == g.path && valid_exe(&g.path)
}
pub fn read_json<T: serde::de::DeserializeOwned>(path: &Path, limit: u64) -> Result<T, String> {
    if fs::metadata(path).map_err(|e| e.to_string())?.len() > limit {
        return Err("Data file exceeds its size limit".into());
    }
    use std::io::Read;
    let mut bytes = Vec::new();
    fs::File::open(path)
        .map_err(|e| e.to_string())?
        .take(limit + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() as u64 > limit {
        return Err("Data file exceeds its size limit".into());
    }
    serde_json::from_slice(&bytes).map_err(|e| format!("Data could not be read: {e}"))
}
pub fn atomic_json<T: serde::Serialize>(path: &Path, value: &T) -> Result<(), String> {
    let temp = path.with_extension(format!("{}.tmp", uuid::Uuid::new_v4()));
    let bytes = serde_json::to_vec_pretty(value).map_err(|e| e.to_string())?;
    use std::io::Write;
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&temp)
        .map_err(|e| e.to_string())?;
    file.write_all(&bytes)
        .and_then(|_| file.sync_all())
        .map_err(|e| e.to_string())?;
    drop(file);
    unsafe {
        use windows_sys::Win32::Storage::FileSystem::*;
        if MoveFileExW(
            windows::wide(&temp.to_string_lossy()).as_ptr(),
            windows::wide(&path.to_string_lossy()).as_ptr(),
            MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH,
        ) == 0
        {
            let _ = fs::remove_file(&temp);
            return Err("Cannot save settings atomically".into());
        }
    }
    Ok(())
}
fn validate_config_size(c: &Config) -> Result<(), String> {
    validate_config(c)?;
    let bytes = serde_json::to_vec_pretty(c).map_err(|e| e.to_string())?;
    if bytes.len() as u64 > CONFIG_LIMIT {
        return Err(
            "Game library exceeds the settings size limit; remove entries before saving.".into(),
        );
    }
    Ok(())
}
pub fn save_config(c: &Config) -> Result<(), String> {
    validate_config_size(c)?;
    windows::as_standard_user(|| atomic_json(&windows::data_dir()?.join("config-v4.json"), c))
}
pub fn get_value(key: &str, name: &str) -> Result<RegValue, String> {
    let key = match RegKey::predef(HKEY_CURRENT_USER).open_subkey(key) {
        Ok(k) => k,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(RegValue::Missing),
        Err(e) => return Err(e.to_string()),
    };
    match key.get_raw_value(name) {
        Ok(v) => match v.vtype {
            REG_DWORD => key
                .get_value(name)
                .map(RegValue::Dword)
                .map_err(|e| e.to_string()),
            REG_SZ => key
                .get_value(name)
                .map(RegValue::Text)
                .map_err(|e| e.to_string()),
            _ => Err("Unsupported existing registry value type; no changes applied".into()),
        },
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => Ok(RegValue::Missing),
        Err(e) => Err(e.to_string()),
    }
}
pub fn set_value(key: &str, name: &str, v: &RegValue) -> Result<(), String> {
    let (k, _) = RegKey::predef(HKEY_CURRENT_USER)
        .create_subkey(key)
        .map_err(|e| e.to_string())?;
    match v {
        RegValue::Missing => match k.delete_value(name) {
            Ok(()) => Ok(()),
            Err(e) if e.kind() == std::io::ErrorKind::NotFound => Ok(()),
            Err(e) => Err(e.to_string()),
        },
        RegValue::Dword(n) => k.set_value(name, n).map_err(|e| e.to_string()),
        RegValue::Text(s) => k.set_value(name, s).map_err(|e| e.to_string()),
    }
}
pub fn recovery() -> Result<Option<Session>, String> {
    let key = match RegKey::predef(HKEY_LOCAL_MACHINE).open_subkey(RECOVERY) {
        Ok(k) => k,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(e) => return Err(e.to_string()),
    };
    let raw: String = match key.get_value("RecoveryV4") {
        Ok(s) => s,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(e) => return Err(e.to_string()),
    };
    if raw.len() > 256_000 {
        return Err("Recovery state exceeds its size limit".into());
    }
    let s: Session =
        serde_json::from_str(&raw).map_err(|_| "Recovery data is damaged. Do not delete it.")?;
    validate_session(&s, &windows::owner()?)?;
    Ok(Some(s))
}
pub fn save_recovery(s: &Session) -> Result<(), String> {
    validate_session(s, &windows::owner()?)?;
    let (k, _) = RegKey::predef(HKEY_LOCAL_MACHINE)
        .create_subkey(RECOVERY)
        .map_err(|e| format!("Administrator permission is required: {e}"))?;
    k.set_value(
        "RecoveryV4",
        &serde_json::to_string(s).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())
}
pub fn clear_recovery() -> Result<(), String> {
    RegKey::predef(HKEY_LOCAL_MACHINE)
        .open_subkey_with_flags(RECOVERY, KEY_SET_VALUE)
        .and_then(|k| k.delete_value("RecoveryV4"))
        .map_err(|e| e.to_string())
}
pub fn legacy_pending() -> bool {
    [KEY_WOW64_64KEY, KEY_WOW64_32KEY].into_iter().any(|view| {
        RegKey::predef(HKEY_LOCAL_MACHINE)
            .open_subkey_with_flags(RECOVERY, KEY_READ | view)
            .ok()
            .is_some_and(|k| k.get_raw_value("RecoveryState").is_ok())
    }) || windows::data_dir()
        .is_ok_and(|p| p.join("state-pro.json").exists() || p.join("state.json").exists())
}
pub fn validate_session(s: &Session, owner: &str) -> Result<(), String> {
    if s.version != 4
        || s.recovery_schema.is_some_and(|schema| schema != 1)
        || s.session_id
            .as_ref()
            .is_some_and(|id| uuid::Uuid::parse_str(id).is_err())
        || s.restore_notes.len() > 10
        || s.restore_notes.iter().any(|note| note.len() > 500)
        || s.owner != owner
        || s.changes.len() > 8
        || uuid::Uuid::parse_str(&s.power_before).is_err()
        || uuid::Uuid::parse_str(&s.power_applied).is_err()
    {
        return Err("Invalid recovery state or another Windows account owns this session".into());
    }
    for c in &s.changes {
        let allowed = (c.key == GAMEBAR
            && ["AutoGameModeEnabled", "AllowAutoGameMode"].contains(&c.name.as_str()))
            || (c.key == DVR && c.name == "GameDVR_Enabled")
            || (c.key == CAPTURE && c.name == "AppCaptureEnabled");
        let gpu = c.key == GPU && valid_exe(&c.name);
        if !allowed && !gpu {
            return Err("Recovery contains a registry path outside the allowlist".into());
        }
        for v in [&c.before, &c.applied] {
            if !matches!(v, RegValue::Missing)
                && !matches!(
                    (v, gpu),
                    (RegValue::Text(_), true) | (RegValue::Dword(_), false)
                )
            {
                return Err("Recovery value type is invalid".into());
            }
            if let RegValue::Text(text) = v {
                if text.len() > 32768 {
                    return Err("Recovery value too large".into());
                }
            }
        }
    }
    if let Some(p) = &s.process {
        if protected(&p.identity.name)
            || !valid_exe(&p.identity.path)
            || p.priority_applied != 0x8000
            || ![0x20, 0x4000, 0x40, 0x80, 0x8000].contains(&p.priority_before)
        {
            return Err("Invalid process recovery identity".into());
        }
    }
    Ok(())
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn a_saved_config_must_fit_the_read_limit() {
        let mut config = Config::default();
        assert!(validate_config_size(&config).is_ok());
        for id in 0..70 {
            config.games.push(Game {
                id: format!("steam-{id}"),
                name: "Game".into(),
                source: "steam".into(),
                path: String::new(),
                launch: format!("steam://rungameid/{id}"),
                directory: "x".repeat(30_000),
                profile: None,
            });
        }
        assert!(validate_config(&config).is_ok());
        assert!(validate_config_size(&config).is_err());
    }
    #[test]
    fn launch_validation() {
        let mut g = Game {
            id: "a".into(),
            name: "a".into(),
            source: "steam".into(),
            path: "".into(),
            launch: "steam://rungameid/730".into(),
            directory: r"C:\Steam\CS2".into(),
            profile: None,
        };
        assert!(valid_launch(&g));
        g.launch = "steam://rungameid/730 -exec evil".into();
        assert!(!valid_launch(&g));
        assert!(!valid_exe(r"\\server\share\evil.exe"));
        assert!(!valid_exe(r"C:\Discord.exe"));
    }
    #[test]
    fn recovery_rejects_privilege_paths() {
        let mut s = Session {
            version: 4,
            recovery_schema: Some(1),
            session_id: None,
            owner: "test".into(),
            started: 0,
            profile: Profile::Balanced,
            automatic: false,
            game: None,
            game_name: "".into(),
            power_before: uuid::Uuid::nil().to_string(),
            power_applied: uuid::Uuid::nil().to_string(),
            power_restored: false,
            power_restore_started: false,
            restore_notes: vec![],
            changes: vec![],
            process: None,
            notes: vec![],
        };
        assert!(validate_session(&s, "other").is_err());
        s.changes.push(RegChange {
            key: r"Software\Microsoft\Windows\CurrentVersion\Run".into(),
            name: "evil".into(),
            before: RegValue::Missing,
            applied: RegValue::Text("evil".into()),
            restored: false,
            restore_started: false,
        });
        assert!(validate_session(&s, "test").is_err());
    }
}
