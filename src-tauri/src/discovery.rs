use crate::{model::*, windows};
pub fn installed() -> Result<Vec<Game>, String> {
    let mut result = vec![];
    if let Ok(steam) = steamlocate::locate() {
        if let Ok(libraries) = steam.libraries() {
            for library in libraries.take(24).flatten() {
                for app in library.apps().take(2000).flatten() {
                    let path = library.resolve_app_dir(&app);
                    if !path.is_dir() {
                        continue;
                    }
                    let name = app.name.clone().unwrap_or_else(|| app.install_dir.clone());
                    if ["Steamworks", "Redistributables", "Steam Linux", "Proton"]
                        .iter()
                        .any(|n| name.contains(n))
                    {
                        continue;
                    }
                    result.push(Game {
                        id: format!("steam-{}", app.app_id),
                        name,
                        source: "steam".into(),
                        path: String::new(),
                        launch: format!("steam://rungameid/{}", app.app_id),
                        directory: path.to_string_lossy().into(),
                        profile: None,
                    });
                }
            }
        }
    }
    if let Some(program_data) = std::env::var_os("PROGRAMDATA") {
        let manifests =
            std::path::PathBuf::from(program_data).join(r"Epic\EpicGamesLauncher\Data\Manifests");
        if let Ok(entries) = std::fs::read_dir(manifests) {
            for entry in entries.take(2000).flatten() {
                let path = entry.path();
                if path.extension().is_none_or(|e| e != "item") {
                    continue;
                }
                if let Ok(v) = crate::store::read_json::<serde_json::Value>(&path, 1_000_000) {
                    let Some(name) = v["DisplayName"].as_str() else {
                        continue;
                    };
                    let Some(dir) = v["InstallLocation"].as_str() else {
                        continue;
                    };
                    let Some(app) = v["AppName"].as_str() else {
                        continue;
                    };
                    if !std::path::Path::new(dir).is_dir()
                        || !app
                            .chars()
                            .all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
                    {
                        continue;
                    }
                    let exe = v["LaunchExecutable"]
                        .as_str()
                        .map(|x| std::path::Path::new(dir).join(x))
                        .filter(|p| p.is_file())
                        .map(|p| p.to_string_lossy().into_owned())
                        .unwrap_or_default();
                    let game = Game {
                        id: format!("epic-{app}"),
                        name: name.chars().take(100).collect(),
                        source: "epic".into(),
                        path: exe,
                        launch: format!(
                            "com.epicgames.launcher://apps/{app}?action=launch&silent=true"
                        ),
                        directory: dir.into(),
                        profile: None,
                    };
                    if crate::store::valid_launch(&game) {
                        result.push(game);
                    }
                }
            }
        }
    }
    // Running known games can be adopted without searching unrelated program folders.
    for p in windows::processes()
        .into_iter()
        .filter(|(_, n)| known(n).is_some())
        .filter_map(|(pid, _)| windows::identity(pid))
    {
        if !result.iter().any(|g| inside(&p.path, &g.directory)) {
            result.push(Game {
                id: uuid::Uuid::new_v4().to_string(),
                name: known(&p.name).unwrap_or(&p.name).into(),
                source: "manual".into(),
                directory: std::path::Path::new(&p.path)
                    .parent()
                    .unwrap_or(std::path::Path::new(&p.path))
                    .to_string_lossy()
                    .into(),
                path: p.path.clone(),
                launch: p.path,
                profile: None,
            });
        }
    }
    result.sort_by_key(|a| a.name.to_lowercase());
    Ok(result)
}
fn matches_game(config: &Config, process: &ProcessIdentity) -> bool {
    !protected(&process.name)
        && (known(&process.name).is_some()
            || config
                .games
                .iter()
                .any(|game| !game.path.is_empty() && process.path.eq_ignore_ascii_case(&game.path)))
}
pub fn running(config: &Config) -> Vec<ProcessIdentity> {
    let Ok(account) = windows::owner() else {
        return vec![];
    };
    let processes = windows::processes();
    processes
        .iter()
        .filter(|(_, name)| {
            !protected(name)
                && (known(name).is_some()
                    || config.games.iter().any(|g| {
                        std::path::Path::new(&g.path)
                            .file_name()
                            .is_some_and(|n| n.to_string_lossy().eq_ignore_ascii_case(name))
                    }))
        })
        .filter_map(|(pid, _)| windows::identity(*pid))
        .filter(|p| windows::owned_by(p, &account))
        .filter(|p| matches_game(config, p))
        .collect()
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn helper_inside_game_directory_is_not_a_game() {
        let mut config = Config::default();
        config.games.push(Game {
            id: "valorant".into(),
            name: "VALORANT".into(),
            source: "manual".into(),
            path: r"C:\Riot Games\VALORANT\live\VALORANT.exe".into(),
            directory: r"C:\Riot Games\VALORANT\live".into(),
            launch: r"C:\Riot Games\VALORANT\live\VALORANT.exe".into(),
            profile: None,
        });
        let helper = ProcessIdentity {
            name: "EpicWebHelper.exe".into(),
            path: r"C:\Riot Games\VALORANT\live\Engine\Binaries\Win64\EpicWebHelper.exe".into(),
            ..Default::default()
        };
        assert!(!matches_game(&config, &helper));
        let unrelated = ProcessIdentity {
            name: "unrelated-helper.exe".into(),
            path: r"C:\Riot Games\VALORANT\live\Engine\Binaries\Win64\unrelated-helper.exe".into(),
            ..Default::default()
        };
        assert!(!protected(&unrelated.name));
        assert!(!matches_game(&config, &unrelated));
        let valorant = ProcessIdentity { name: "VALORANT-Win64-Shipping.exe".into(), path: r"C:\Riot Games\VALORANT\live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe".into(), ..Default::default() };
        assert!(matches_game(&config, &valorant));
        let custom = ProcessIdentity {
            name: "VALORANT.exe".into(),
            path: config.games[0].path.clone(),
            ..Default::default()
        };
        assert!(matches_game(&config, &custom));
    }
}
