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
pub fn running(config: &Config) -> Vec<ProcessIdentity> {
    use std::{
        cell::RefCell,
        time::{Duration, Instant},
    };
    thread_local! {static DEEP:RefCell<(Option<Instant>,Vec<ProcessIdentity>)>=const{RefCell::new((None,Vec::new()))};}
    let processes = windows::processes();
    let mut result: Vec<ProcessIdentity> = processes
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
        .filter(|p| {
            known(&p.name).is_some()
                || config
                    .games
                    .iter()
                    .any(|g| p.path.eq_ignore_ascii_case(&g.path) || inside(&p.path, &g.directory))
        })
        .collect();
    DEEP.with(|cache| {
        let mut cache = cache.borrow_mut();
        if cache
            .0
            .is_none_or(|t| t.elapsed() >= Duration::from_secs(10))
        {
            cache.0 = Some(Instant::now());
            cache.1 = processes
                .iter()
                .filter(|(_, name)| !protected(name) && known(name).is_none())
                .filter_map(|(pid, _)| windows::identity(*pid))
                .filter(|p| {
                    config
                        .games
                        .iter()
                        .any(|g| !g.directory.is_empty() && inside(&p.path, &g.directory))
                })
                .collect();
        }
        cache.1.retain(|p| {
            windows::same(p) && config.games.iter().any(|g| inside(&p.path, &g.directory))
        });
        for game in &cache.1 {
            if !result.iter().any(|p| p.pid == game.pid) {
                result.push(game.clone());
            }
        }
    });
    result
}
