use crate::{model::*, store, windows};
pub fn enable(
    config: &Config,
    platform: &Platform,
    game: Option<ProcessIdentity>,
    automatic: bool,
) -> Result<Session, String> {
    if !windows::admin() {
        return Err("ADMIN_REQUIRED".into());
    }
    if !platform.supported {
        return Err(
            "This laptop is not yet supported. Acer + NitroSense and desktop PCs are supported."
                .into(),
        );
    }
    if store::legacy_pending() {
        return Err("Restore the version 3 session first, then return to version 4.".into());
    }
    if store::recovery()?.is_some() {
        return Err("Restore the current session before starting another Boost.".into());
    }
    if platform.laptop && !platform.ac {
        return Err("Connect AC power before starting Boost.".into());
    }
    if game
        .as_ref()
        .is_some_and(|g| protected(&g.name) || !windows::same(g))
    {
        return Err("The game process changed or is protected.".into());
    }
    let selected = config
        .selected
        .as_ref()
        .and_then(|id| config.games.iter().find(|g| &g.id == id));
    let profile = game
        .as_ref()
        .map(|g| profile_for(config, &g.path))
        .unwrap_or(selected.and_then(|g| g.profile).unwrap_or(config.master));
    let (power_before, _) = windows::power()?;
    let mut s = Session {
        version: 4,
        owner: windows::owner()?,
        started: now(),
        profile,
        automatic,
        game_name: game
            .as_ref()
            .map(|g| known(&g.name).unwrap_or(&g.name).to_string())
            .unwrap_or_default(),
        game,
        power_applied: power_before.clone(),
        power_before,
        changes: vec![],
        process: None,
        notes: vec![],
    };
    let mut add = |key: &str, name: &str, applied: RegValue| -> Result<(), String> {
        s.changes.push(RegChange {
            key: key.into(),
            name: name.into(),
            before: store::get_value(key, name)?,
            applied,
        });
        Ok(())
    };
    if config.game_mode {
        add(store::GAMEBAR, "AutoGameModeEnabled", RegValue::Dword(1))?;
        add(store::GAMEBAR, "AllowAutoGameMode", RegValue::Dword(1))?;
    }
    if profile != Profile::Quality && config.capture_off {
        add(store::DVR, "GameDVR_Enabled", RegValue::Dword(0))?;
        add(store::CAPTURE, "AppCaptureEnabled", RegValue::Dword(0))?;
    }
    if profile != Profile::Quality && config.gpu_preference {
        if let Some(path) = s.game.as_ref().map(|g| g.path.as_str()).or(selected
            .filter(|g| !g.path.is_empty())
            .map(|g| g.path.as_str()))
        {
            let before = store::get_value(store::GPU, path)?;
            let previous = if let RegValue::Text(t) = &before {
                t.as_str()
            } else {
                ""
            };
            let merged = previous
                .split(';')
                .filter(|part| !part.is_empty() && !part.trim_start().starts_with("GpuPreference="))
                .collect::<Vec<_>>()
                .join(";");
            s.changes.push(RegChange {
                key: store::GPU.into(),
                name: path.into(),
                before,
                applied: RegValue::Text(format!(
                    "{merged}{}GpuPreference=2;",
                    if merged.is_empty() { "" } else { ";" }
                )),
            });
            s.notes
                .push("GPU preference takes effect when the game is launched again.".into());
        }
    }
    if profile == Profile::Performance && config.priority {
        if let Some(g) = &s.game {
            match windows::priority(g, None) {
                Ok(before) if before == 0x20 || before == 0x4000 || before == 0x40 => {
                    s.process = Some(ProcessChange {
                        identity: g.clone(),
                        priority_before: before,
                        priority_applied: 0x8000,
                        verified: false,
                    })
                }
                Ok(_) => s
                    .notes
                    .push("Existing game priority is already elevated; preserved.".into()),
                Err(e) => s.notes.push(e),
            }
        }
    }
    // Recovery must exist before any mutation, including power plan creation.
    store::save_recovery(&s)?;
    let apply = (|| -> Result<(), String> {
        if profile == Profile::Performance && !config.keep_power {
            let target = "47d5f143-42c4-4f62-b26d-86a08bb21ca4";
            if windows::powercfg(&["/query", target]).is_err() {
                windows::powercfg(&[
                    "/duplicatescheme",
                    "e9a42b02-d5df-448d-aa00-03f14749eb61",
                    target,
                ])?;
                windows::powercfg(&["/changename", target, "Game Boost Pro Ultimate"])?;
            }
            s.power_applied = target.into();
            store::save_recovery(&s)?;
            windows::set_power(target)?;
        }
        for change in &s.changes {
            store::set_value(&change.key, &change.name, &change.applied)?;
            if store::get_value(&change.key, &change.name)? != change.applied {
                return Err("Windows did not retain a requested setting".into());
            }
        }
        if let Some(p) = &mut s.process {
            match windows::priority(&p.identity, Some(p.priority_applied)) {
                Ok(_) => p.verified = true,
                Err(e) => s.notes.push(e),
            }
            store::save_recovery(&s)?;
        }
        Ok(())
    })();
    if let Err(e) = apply {
        return match restore() {
            Ok(_) => Err(format!("{e}. Original settings restored.")),
            Err(r) => Err(format!("{e}. Recovery is still pending: {r}")),
        };
    }
    Ok(s)
}
pub fn attach(s: &mut Session, config: &Config, game: &ProcessIdentity) -> Result<(), String> {
    if s.game.is_some() || !windows::same(game) || protected(&game.name) {
        return Ok(());
    }
    s.game = Some(game.clone());
    s.game_name = known(&game.name).unwrap_or(&game.name).into();
    if s.profile == Profile::Performance && config.priority {
        match windows::priority(game, None) {
            Ok(before) if [0x20, 0x4000, 0x40].contains(&before) => {
                s.process = Some(ProcessChange {
                    identity: game.clone(),
                    priority_before: before,
                    priority_applied: 0x8000,
                    verified: false,
                })
            }
            Ok(_) => {}
            Err(e) => s.notes.push(e),
        }
    }
    store::save_recovery(s)?;
    if let Some(p) = &mut s.process {
        match windows::priority(&p.identity, Some(p.priority_applied)) {
            Ok(_) => p.verified = true,
            Err(e) => s.notes.push(e),
        }
        store::save_recovery(s)?;
    }
    Ok(())
}
pub fn restore() -> Result<Vec<String>, String> {
    if !windows::admin() {
        return Err("ADMIN_REQUIRED".into());
    }
    let s = store::recovery()?.ok_or("No recovery session was found")?;
    let mut notes = vec![];
    if let Some(p) = &s.process {
        if windows::same(&p.identity) {
            let current = windows::priority(&p.identity, None)?;
            if current == p.priority_applied {
                windows::priority(&p.identity, Some(p.priority_before))?;
            } else if current != p.priority_before {
                notes.push("Priority changed outside the app; preserved.".into());
            }
        }
    }
    let current = windows::power()?.0;
    if current == s.power_applied {
        windows::set_power(&s.power_before)?;
    } else if current != s.power_before {
        notes.push("Power plan changed outside the app; preserved.".into());
    }
    for change in &s.changes {
        let current = store::get_value(&change.key, &change.name)?;
        if current == change.applied {
            store::set_value(&change.key, &change.name, &change.before)?;
            if store::get_value(&change.key, &change.name)? != change.before {
                return Err("Restore verification failed. Recovery is retained.".into());
            }
        } else if current != change.before {
            notes.push(format!(
                "{} was changed outside the app; preserved.",
                change.name
            ));
        }
    }
    store::clear_recovery()?;
    Ok(notes)
}
