use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum Profile {
    Quality,
    #[default]
    Balanced,
    Performance,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Game {
    pub id: String,
    pub name: String,
    pub source: String,
    pub path: String,
    pub launch: String,
    pub directory: String,
    pub profile: Option<Profile>,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase", deny_unknown_fields)]
pub struct Config {
    pub language: String,
    pub master: Profile,
    pub auto_mode: bool,
    pub game_mode: bool,
    pub capture_off: bool,
    pub gpu_preference: bool,
    pub priority: bool,
    pub keep_power: bool,
    pub telemetry: bool,
    pub selected: Option<String>,
    pub games: Vec<Game>,
}
impl Default for Config {
    fn default() -> Self {
        Self {
            language: "TH".into(),
            master: Profile::Balanced,
            auto_mode: false,
            game_mode: true,
            capture_off: true,
            gpu_preference: true,
            priority: true,
            keep_power: true,
            telemetry: true,
            selected: None,
            games: vec![],
        }
    }
}
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProcessIdentity {
    pub pid: u32,
    pub started: u64,
    pub path: String,
    pub name: String,
}
#[derive(Clone, Debug, Default, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Platform {
    pub manufacturer: String,
    pub model: String,
    pub laptop: bool,
    pub nitro_sense: bool,
    pub supported: bool,
    pub gpu: Vec<String>,
    pub power_name: String,
    pub ac: bool,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq)]
#[serde(tag = "kind", content = "value", rename_all = "lowercase")]
pub enum RegValue {
    #[default]
    Missing,
    Dword(u32),
    Text(String),
}
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct RegChange {
    pub key: String,
    pub name: String,
    pub before: RegValue,
    pub applied: RegValue,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ProcessChange {
    pub identity: ProcessIdentity,
    pub priority_before: u32,
    pub priority_applied: u32,
    pub verified: bool,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct Session {
    pub version: u8,
    pub owner: String,
    pub started: u64,
    pub profile: Profile,
    pub automatic: bool,
    pub game: Option<ProcessIdentity>,
    pub game_name: String,
    pub power_before: String,
    pub power_applied: String,
    pub changes: Vec<RegChange>,
    pub process: Option<ProcessChange>,
    pub notes: Vec<String>,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FrameResult {
    pub game: String,
    pub process: ProcessIdentity,
    pub boosted: bool,
    pub profile: Option<Profile>,
    pub frames: usize,
    pub average_fps: f64,
    pub low_fps: f64,
    pub p99_ms: f64,
    pub timestamp: u64,
}
#[derive(Clone, Default, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Status {
    pub admin: bool,
    pub platform: Platform,
    pub running: Vec<ProcessIdentity>,
    pub session: Option<Session>,
    pub legacy_recovery: bool,
    pub error: Option<String>,
    pub cpu: f64,
    pub ram_percent: u32,
    pub ram_used_gb: f64,
    pub ram_total_gb: f64,
    pub scan_ms: f64,
    pub frames: Vec<FrameResult>,
    pub capturing: bool,
    pub config: Config,
}
pub fn protected(name: &str) -> bool {
    let lower = name.to_lowercase();
    let n = lower.trim_end_matches(".exe");
    [
        "discord",
        "ts3client_win64",
        "ts3client_win32",
        "teamspeak",
        "nitrosense",
        "gameboostpro",
        "system",
        "svchost",
        "msmpeng",
        "lsass",
        "csrss",
        "winlogon",
        "steam",
        "riotclientservices",
        "epicgameslauncher",
        "easyanticheat",
        "beservice",
        "vgc",
        "vgtray",
        "cmd",
        "powershell",
        "pwsh",
        "explorer",
        "msedge",
        "chrome",
        "firefox",
        "crashreportclient",
        "unrealcefsubprocess",
        "unitycrashhandler64",
        "unitycrashhandler32",
        "crashpad_handler",
        "steamwebhelper",
    ]
    .contains(&n)
        || n.starts_with("acer")
        || n.contains("anticheat")
        || n.contains("crashhandler")
}
pub fn known(name: &str) -> Option<&'static str> {
    match name.to_lowercase().trim_end_matches(".exe") {
        "cs2" => Some("Counter-Strike 2"),
        "tslgame" => Some("PUBG: BATTLEGROUNDS"),
        "valorant-win64-shipping" => Some("VALORANT"),
        "r5apex" => Some("Apex Legends"),
        "fortniteclient-win64-shipping" => Some("Fortnite"),
        "dota2" => Some("Dota 2"),
        "league of legends" => Some("League of Legends"),
        "overwatch" => Some("Overwatch 2"),
        "cyberpunk2077" => Some("Cyberpunk 2077"),
        "eldenring" => Some("ELDEN RING"),
        "gta5" | "gta5_enhanced" => Some("Grand Theft Auto V"),
        "cod" => Some("Call of Duty"),
        "helldivers2" => Some("HELLDIVERS 2"),
        "rainbowsix" | "rainbowsix_vulkan" => Some("Rainbow Six Siege"),
        "marvel-win64-shipping" => Some("Marvel Rivals"),
        "rocketleague" => Some("Rocket League"),
        _ => None,
    }
}
pub fn inside(path: &str, directory: &str) -> bool {
    let p = path.replace('/', "\\").to_lowercase();
    let d = directory
        .replace('/', "\\")
        .trim_end_matches('\\')
        .to_lowercase();
    !d.is_empty() && p.starts_with(&(d + "\\"))
}
pub fn profile_for(config: &Config, path: &str) -> Profile {
    config
        .games
        .iter()
        .filter(|g| g.path.eq_ignore_ascii_case(path) || inside(path, &g.directory))
        .max_by_key(|g| g.directory.len())
        .and_then(|g| g.profile)
        .unwrap_or(config.master)
}
pub fn now() -> u64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn directory_boundaries() {
        assert!(inside(r"C:\Games\CS2\bin\cs2.exe", r"C:\Games\CS2"));
        assert!(!inside(r"C:\Games\CS20\evil.exe", r"C:\Games\CS2"));
        assert!(!inside("a", ""));
    }
    #[test]
    fn protected_apps_and_anticheat() {
        for n in [
            "Discord.exe",
            "ts3client_win64.exe",
            "AcerService.exe",
            "vgc.exe",
            "NitroSense.exe",
        ] {
            assert!(protected(n));
        }
        assert!(!protected("cs2.exe"));
    }
    #[test]
    fn master_and_explicit_override() {
        let mut c = Config::default();
        c.master = Profile::Performance;
        c.games.push(Game {
            id: "a".into(),
            name: "CS2".into(),
            source: "manual".into(),
            path: r"C:\Games\CS2\cs2.exe".into(),
            directory: r"C:\Games\CS2".into(),
            launch: "".into(),
            profile: Some(Profile::Quality),
        });
        assert_eq!(profile_for(&c, r"C:\Games\CS2\cs2.exe"), Profile::Quality);
        c.games[0].profile = None;
        assert_eq!(
            profile_for(&c, r"C:\Games\CS2\cs2.exe"),
            Profile::Performance
        );
    }
    #[test]
    fn unknown_profile_is_rejected() {
        assert!(serde_json::from_str::<Profile>("\"realtime\"").is_err());
    }
}
