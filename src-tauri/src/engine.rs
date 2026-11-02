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
        recovery_schema: Some(1),
        session_id: Some(uuid::Uuid::new_v4().to_string()),
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
        power_restored: false,
        power_restore_started: false,
        restore_notes: vec![],
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
            restored: false,
            restore_started: true,
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
                restored: false,
                restore_started: true,
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
                        restored: false,
                        restore_started: true,
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
            s.power_restore_started = true;
            store::save_recovery(&s)?;
            if windows::power()?.0 != s.power_before {
                return Err("Power plan changed while starting Boost; review recovery".into());
            }
            windows::set_power(target)?;
            s.power_restore_started = false;
            store::save_recovery(&s)?;
        }
        for index in 0..s.changes.len() {
            let change = &s.changes[index];
            if store::get_value(&change.key, &change.name)? != change.before {
                return Err(format!(
                    "{} changed while starting Boost; review recovery",
                    change.name
                ));
            }
            store::set_value(&change.key, &change.name, &change.applied)?;
            if store::get_value(&change.key, &change.name)? != change.applied {
                return Err("Windows did not retain a requested setting".into());
            }
            s.changes[index].restore_started = false;
            store::save_recovery(&s)?;
        }
        if let Some(p) = &mut s.process {
            p.restore_started = true;
        }
        if s.process.is_some() {
            store::save_recovery(&s)?;
        }
        if let Some(p) = &mut s.process {
            match windows::as_standard_user(|| {
                if windows::priority(&p.identity, None)? != p.priority_before {
                    return Err("Game priority changed while starting Boost; preserved".into());
                }
                windows::priority(&p.identity, Some(p.priority_applied))
            }) {
                Ok(_) => {
                    p.verified = true;
                    p.restore_started = false;
                }
                Err(e) => {
                    if e == windows::PRIORITY_UNCERTAIN {
                        p.restore_started = true;
                    } else {
                        p.restored = true;
                        p.restore_started = false;
                    }
                    s.notes.push(e);
                }
            }
            store::save_recovery(&s)?;
        }
        Ok(())
    })();
    if let Err(e) = apply {
        return match restore() {
            Ok(notes) if notes.is_empty() => Err(format!("{e}. Original settings restored.")),
            Ok(notes) => Err(format!("{e}. Recovery completed; {}", notes.join(" "))),
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
                    restored: false,
                    restore_started: true,
                })
            }
            Ok(_) => {}
            Err(e) => s.notes.push(e),
        }
    }
    store::save_recovery(s)?;
    if let Some(p) = &mut s.process {
        p.restore_started = true;
    }
    if s.process.is_some() {
        store::save_recovery(s)?;
    }
    if let Some(p) = &mut s.process {
        match windows::as_standard_user(|| {
            if windows::priority(&p.identity, None)? != p.priority_before {
                return Err("Game priority changed while starting Boost; preserved".into());
            }
            windows::priority(&p.identity, Some(p.priority_applied))
        }) {
            Ok(_) => {
                p.verified = true;
                p.restore_started = false;
            }
            Err(e) => {
                if e == windows::PRIORITY_UNCERTAIN {
                    p.restore_started = true;
                } else {
                    p.restored = true;
                    p.restore_started = false;
                }
                s.notes.push(e);
            }
        }
        store::save_recovery(s)?;
    }
    Ok(())
}
trait RestoreOps {
    fn same(&self, process: &ProcessIdentity) -> Result<bool, String>;
    fn priority(&self, process: &ProcessIdentity, value: Option<u32>) -> Result<u32, String>;
    fn power(&self) -> Result<(String, String), String>;
    fn set_power(&self, value: &str) -> Result<(), String>;
    fn get_value(&self, key: &str, name: &str) -> Result<RegValue, String>;
    fn set_value(&self, key: &str, name: &str, value: &RegValue) -> Result<(), String>;
    fn save(&self, session: &Session) -> Result<(), String>;
    fn clear(&self) -> Result<(), String>;
}
struct WindowsRestore;
impl RestoreOps for WindowsRestore {
    fn same(&self, p: &ProcessIdentity) -> Result<bool, String> {
        windows::same_for_restore(p)
    }
    fn priority(&self, p: &ProcessIdentity, v: Option<u32>) -> Result<u32, String> {
        windows::as_standard_user(|| windows::priority(p, v))
    }
    fn power(&self) -> Result<(String, String), String> {
        windows::power()
    }
    fn set_power(&self, v: &str) -> Result<(), String> {
        windows::set_power(v)
    }
    fn get_value(&self, k: &str, n: &str) -> Result<RegValue, String> {
        store::get_value(k, n)
    }
    fn set_value(&self, k: &str, n: &str, v: &RegValue) -> Result<(), String> {
        store::set_value(k, n, v)
    }
    fn save(&self, s: &Session) -> Result<(), String> {
        store::save_recovery(s)
    }
    fn clear(&self) -> Result<(), String> {
        store::clear_recovery()
    }
}
pub fn restore() -> Result<Vec<String>, String> {
    restore_checked(false)
}
fn restore_checked(allow_uncertain: bool) -> Result<Vec<String>, String> {
    if !windows::admin() {
        return Err("ADMIN_REQUIRED".into());
    }
    let s = store::recovery()?.ok_or("No recovery session was found")?;
    restore_from(s, allow_uncertain)
}
pub fn restore_from(session: Session, allow_uncertain: bool) -> Result<Vec<String>, String> {
    if !windows::admin() {
        return Err("ADMIN_REQUIRED".into());
    }
    restore_session(session, &WindowsRestore, allow_uncertain)
}
fn restore_session(
    mut s: Session,
    ops: &impl RestoreOps,
    allow_uncertain: bool,
) -> Result<Vec<String>, String> {
    if s.recovery_schema.is_none() {
        if !allow_uncertain {
            return Err("Recovery from an older version needs review and confirmation.".into());
        }
        s.recovery_schema = Some(1);
        if !s.power_restored {
            s.power_restore_started = true;
        }
        if let Some(process) = &mut s.process {
            if !process.restored {
                process.restore_started = true;
            }
        }
        for change in &mut s.changes {
            if !change.restored {
                change.restore_started = true;
            }
        }
        ops.save(&s)?;
    }
    let mut errors = vec![];
    if let Some(p) = s.process.clone().filter(|p| !p.restored) {
        let outcome = (|| -> Result<(), String> {
            if ops.same(&p.identity)? {
                let current = ops.priority(&p.identity, None)?;
                if current == p.priority_applied && current != p.priority_before {
                    if p.restore_started && !allow_uncertain {
                        return Err("Uncertain prior restore; review and confirm".into());
                    }
                    s.process.as_mut().unwrap().restore_started = true;
                    ops.save(&s)?;
                    if ops.priority(&p.identity, None)? != current {
                        return Err("Priority changed during recovery; retry review".into());
                    }
                    ops.priority(&p.identity, Some(p.priority_before))?;
                } else if current != p.priority_before {
                    s.restore_notes
                        .push("Priority changed outside the app; preserved.".into());
                }
            }
            Ok(())
        })();
        if let Err(e) = outcome {
            errors.push(format!("Process priority: {e}"));
        } else {
            s.process.as_mut().unwrap().restored = true;
            ops.save(&s)
                .map_err(|e| format!("Could not record process recovery progress: {e}"))?;
        }
    }
    if !s.power_restored {
        let outcome = (|| -> Result<(), String> {
            let (current, _) = ops.power()?;
            if current == s.power_applied && current != s.power_before {
                if s.power_restore_started && !allow_uncertain {
                    return Err("Uncertain prior restore; review and confirm".into());
                }
                s.power_restore_started = true;
                ops.save(&s)?;
                if ops.power()?.0 != current {
                    return Err("Power plan changed during recovery; retry review".into());
                }
                ops.set_power(&s.power_before)?;
            } else if current != s.power_before {
                s.restore_notes
                    .push("Power plan changed outside the app; preserved.".into());
            }
            Ok(())
        })();
        if let Err(e) = outcome {
            errors.push(format!("Power plan: {e}"));
        } else {
            s.power_restored = true;
            ops.save(&s)
                .map_err(|e| format!("Could not record power recovery progress: {e}"))?;
        }
    }
    for index in 0..s.changes.len() {
        let change = s.changes[index].clone();
        if change.restored {
            continue;
        }
        let outcome = (|| -> Result<(), String> {
            let current = ops.get_value(&change.key, &change.name)?;
            if current == change.applied && current != change.before {
                if change.restore_started && !allow_uncertain {
                    return Err("Uncertain prior restore; review and confirm".into());
                }
                s.changes[index].restore_started = true;
                ops.save(&s)?;
                if ops.get_value(&change.key, &change.name)? != current {
                    return Err("Setting changed during recovery; retry review".into());
                }
                ops.set_value(&change.key, &change.name, &change.before)?;
                if ops.get_value(&change.key, &change.name)? != change.before {
                    return Err("verification failed".into());
                }
            } else if current != change.before {
                let name: String = change.name.chars().take(100).collect();
                s.restore_notes
                    .push(format!("{name} was changed outside the app; preserved."));
            }
            Ok(())
        })();
        if let Err(e) = outcome {
            errors.push(format!("{}: {e}", change.name));
        } else {
            s.changes[index].restored = true;
            ops.save(&s)
                .map_err(|e| format!("Could not record registry recovery progress: {e}"))?;
        }
    }
    if !errors.is_empty() {
        return Err(format!(
            "Some settings could not be restored: {}. Recovery is retained for retry.",
            errors.join("; ")
        ));
    }
    ops.clear()?;
    Ok(s.restore_notes)
}

#[cfg(test)]
mod restore_tests {
    use super::*;
    use std::cell::{Cell, RefCell};

    struct Fake {
        inaccessible: Cell<bool>,
        plan: RefCell<String>,
        value: RefCell<RegValue>,
        saved: RefCell<Option<Session>>,
        cleared: Cell<bool>,
    }
    impl RestoreOps for Fake {
        fn same(&self, _: &ProcessIdentity) -> Result<bool, String> {
            if self.inaccessible.get() {
                Err("not readable".into())
            } else {
                Ok(false)
            }
        }
        fn priority(&self, _: &ProcessIdentity, _: Option<u32>) -> Result<u32, String> {
            Ok(0x20)
        }
        fn power(&self) -> Result<(String, String), String> {
            Ok((self.plan.borrow().clone(), String::new()))
        }
        fn set_power(&self, v: &str) -> Result<(), String> {
            *self.plan.borrow_mut() = v.into();
            Ok(())
        }
        fn get_value(&self, _: &str, _: &str) -> Result<RegValue, String> {
            Ok(self.value.borrow().clone())
        }
        fn set_value(&self, _: &str, _: &str, v: &RegValue) -> Result<(), String> {
            *self.value.borrow_mut() = v.clone();
            Ok(())
        }
        fn save(&self, s: &Session) -> Result<(), String> {
            *self.saved.borrow_mut() = Some(s.clone());
            Ok(())
        }
        fn clear(&self) -> Result<(), String> {
            self.cleared.set(true);
            Ok(())
        }
    }
    fn session() -> Session {
        Session {
            version: 4,
            recovery_schema: Some(1),
            session_id: None,
            owner: "test".into(),
            started: 1,
            profile: Profile::Performance,
            automatic: true,
            game: None,
            game_name: String::new(),
            power_before: "original".into(),
            power_applied: "boost".into(),
            power_restored: false,
            power_restore_started: false,
            restore_notes: vec![],
            changes: vec![RegChange {
                key: "key".into(),
                name: "name".into(),
                before: RegValue::Dword(1),
                applied: RegValue::Dword(0),
                restored: false,
                restore_started: false,
            }],
            process: Some(ProcessChange {
                identity: ProcessIdentity::default(),
                priority_before: 0x20,
                priority_applied: 0x8000,
                verified: true,
                restored: false,
                restore_started: false,
            }),
            notes: vec![],
        }
    }
    #[test]
    fn partial_recovery_checkpoints_and_preserves_later_user_changes() {
        let fake = Fake {
            inaccessible: Cell::new(true),
            plan: RefCell::new("boost".into()),
            value: RefCell::new(RegValue::Dword(0)),
            saved: RefCell::new(None),
            cleared: Cell::new(false),
        };
        assert!(restore_session(session(), &fake, false).is_err());
        assert_eq!(*fake.plan.borrow(), "original");
        assert_eq!(*fake.value.borrow(), RegValue::Dword(1));
        let checkpoint = fake.saved.borrow().clone().unwrap();
        assert!(checkpoint.power_restored && checkpoint.changes[0].restored);
        assert!(!fake.cleared.get());
        *fake.plan.borrow_mut() = "boost".into();
        *fake.value.borrow_mut() = RegValue::Dword(0);
        fake.inaccessible.set(false);
        assert!(restore_session(checkpoint, &fake, false).is_ok());
        assert_eq!(*fake.plan.borrow(), "boost");
        assert_eq!(*fake.value.borrow(), RegValue::Dword(0));
        assert!(fake.cleared.get());
    }
    #[test]
    fn old_recovery_records_default_to_unrestored() {
        let mut value = serde_json::to_value(session()).unwrap();
        let object = value.as_object_mut().unwrap();
        object.remove("session_id");
        object.remove("recovery_schema");
        object.remove("power_restored");
        object.get_mut("changes").unwrap()[0]
            .as_object_mut()
            .unwrap()
            .remove("restored");
        object
            .get_mut("process")
            .unwrap()
            .as_object_mut()
            .unwrap()
            .remove("restored");
        let old: Session = serde_json::from_value(value).unwrap();
        assert!(old.session_id.is_none());
        assert!(old.recovery_uncertain());
        assert!(!old.power_restored && !old.changes[0].restored && !old.process.unwrap().restored);
    }
    #[test]
    fn old_recovery_requires_review_before_changes() {
        let fake = Fake {
            inaccessible: Cell::new(false),
            plan: RefCell::new("boost".into()),
            value: RefCell::new(RegValue::Dword(0)),
            saved: RefCell::new(None),
            cleared: Cell::new(false),
        };
        let mut legacy = session();
        legacy.recovery_schema = None;
        assert!(restore_session(legacy.clone(), &fake, false).is_err());
        assert_eq!(*fake.plan.borrow(), "boost");
        assert!(restore_session(legacy, &fake, true).is_ok());
        assert_eq!(*fake.plan.borrow(), "original");
    }
    #[test]
    fn uncertain_checkpoint_requires_explicit_confirmation() {
        let fake = Fake {
            inaccessible: Cell::new(false),
            plan: RefCell::new("boost".into()),
            value: RefCell::new(RegValue::Dword(0)),
            saved: RefCell::new(None),
            cleared: Cell::new(false),
        };
        let mut pending = session();
        pending.process = None;
        pending.changes.clear();
        pending.power_restore_started = true;
        assert!(restore_session(pending.clone(), &fake, false).is_err());
        assert_eq!(*fake.plan.borrow(), "boost");
        assert!(!fake.cleared.get());
        assert!(restore_session(pending, &fake, true).is_ok());
        assert_eq!(*fake.plan.borrow(), "original");
        assert!(fake.cleared.get());
    }
}
