use crate::model::*;
use std::{collections::HashMap, io::Read, path::Path};
pub fn capture(
    resources: &Path,
    target: &ProcessIdentity,
    session: Option<&Session>,
) -> Result<FrameResult, String> {
    if !crate::windows::admin() {
        return Err("ADMIN_REQUIRED".into());
    }
    if !crate::windows::same(target) || protected(&target.name) {
        return Err("The selected game process changed".into());
    }
    let tool =
        crate::capture_tool::CaptureTool::prepare(&resources.join("tools").join("PresentMon.exe"))?;
    let mut child = crate::windows::command(tool.path())
        .args([
            "--process_id",
            &target.pid.to_string(),
            "--delay",
            "3",
            "--timed",
            "15",
            "--terminate_after_timed",
            "--no_console_stats",
            "--session_name",
            &format!("GameBoostPro-{}", uuid::Uuid::new_v4()),
            "--output_stdout",
        ])
        .stdin(std::process::Stdio::null())
        .stdout(std::process::Stdio::piped())
        .stderr(std::process::Stdio::null())
        .spawn()
        .map_err(|e| e.to_string())?;
    let Some(stdout) = child.stdout.take() else {
        let _ = child.kill();
        let _ = child.wait();
        return Err("Frame output pipe is unavailable".into());
    };
    let reader = std::thread::spawn(move || -> Result<Vec<u8>, String> {
        let mut data = Vec::new();
        stdout
            .take(64_000_001)
            .read_to_end(&mut data)
            .map_err(|e| e.to_string())?;
        if data.len() > 64_000_000 {
            Err("Frame capture exceeded its size limit".into())
        } else {
            Ok(data)
        }
    });
    let start = std::time::Instant::now();
    let exit = loop {
        let status = match child.try_wait() {
            Ok(s) => s,
            Err(e) => {
                let _ = child.kill();
                let _ = child.wait();
                let _ = reader.join();
                return Err(e.to_string());
            }
        };
        if let Some(exit) = status {
            break exit;
        }
        if start.elapsed().as_secs() > 30 {
            let _ = child.kill();
            let _ = child.wait();
            let _ = reader.join();
            return Err("Frame recording timed out and was stopped".into());
        }
        std::thread::sleep(std::time::Duration::from_millis(100));
    };
    let data = reader.join().map_err(|_| "Frame output reader failed")??;
    if !exit.success() {
        return Err("PresentMon could not record frames. Check that the game is rendering and ETW access is available.".into());
    }
    if !crate::windows::same(target) {
        return Err("The game exited or changed while recording. Record again.".into());
    }
    analyze(data.as_slice(), target, session)
}
pub fn analyze(
    reader: impl Read,
    target: &ProcessIdentity,
    session: Option<&Session>,
) -> Result<FrameResult, String> {
    let mut csv = csv::ReaderBuilder::new().flexible(true).from_reader(reader);
    let h = csv.headers().map_err(|e| e.to_string())?.clone();
    let column = |names: &[&str]| {
        h.iter()
            .position(|x| names.iter().any(|n| x.eq_ignore_ascii_case(n)))
    };
    let frame = column(&["MsBetweenPresents", "FrameTime"])
        .ok_or("PresentMon CSV has no supported frame-time column")?;
    let pid = column(&["ProcessID"]).ok_or("PresentMon CSV has no process identity")?;
    let swap = column(&["SwapChainAddress"]);
    let mut groups: HashMap<String, Vec<f64>> = HashMap::new();
    let mut count = 0;
    for row in csv.records().take(1_000_000) {
        let row = row.map_err(|e| e.to_string())?;
        if row.get(pid).and_then(|v| v.parse::<u32>().ok()) != Some(target.pid) {
            continue;
        }
        let Some(ms) = row
            .get(frame)
            .and_then(|v| v.parse::<f64>().ok())
            .filter(|v| v.is_finite() && *v > 0.0 && *v <= 10_000.)
        else {
            continue;
        };
        let key = swap
            .and_then(|index| row.get(index))
            .unwrap_or("primary")
            .to_string();
        if groups.len() >= 64 && !groups.contains_key(&key) {
            continue;
        }
        groups.entry(key).or_default().push(ms);
        count += 1;
        if count > 500_000 {
            return Err("Frame sample exceeded its limit".into());
        }
    }
    let mut times = groups
        .into_values()
        .max_by_key(|g| g.len())
        .filter(|g| g.len() >= 30)
        .ok_or("Not enough frames. Keep the game in the foreground and record again.")?;
    times.sort_by(f64::total_cmp);
    let n = times.len();
    let total: f64 = times.iter().sum();
    let slow = (n as f64 * 0.01).ceil().max(1.) as usize;
    let slow_total: f64 = times[n - slow..].iter().sum();
    Ok(FrameResult {
        game: known(&target.name).unwrap_or(&target.name).into(),
        process: target.clone(),
        boosted: session.is_some(),
        profile: session.map(|s| s.profile),
        frames: n,
        average_fps: 1000. * n as f64 / total,
        low_fps: 1000. * slow as f64 / slow_total,
        p99_ms: times[((n - 1) as f64 * 0.99).ceil() as usize],
        timestamp: now(),
    })
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn primary_swapchain_and_pid_are_isolated() {
        let mut data = "ProcessID,SwapChainAddress,MsBetweenPresents\n".to_string();
        for _ in 0..100 {
            data.push_str("42,A,10\n42,B,40\n13,A,100\n");
        }
        let target = ProcessIdentity {
            pid: 42,
            ..Default::default()
        };
        let r = analyze(data.as_bytes(), &target, None).unwrap();
        assert!(r.average_fps == 100. || r.average_fps == 25.);
        assert_eq!(r.frames, 100);
    }
    #[test]
    fn invalid_and_too_small_samples_rejected() {
        let target = ProcessIdentity {
            pid: 42,
            ..Default::default()
        };
        assert!(analyze(
            "ProcessID,FrameTime\n42,NaN\n42,0\n42,-1".as_bytes(),
            &target,
            None
        )
        .is_err());
    }
    #[test]
    fn one_percent_low_uses_slowest_frames() {
        let mut data = "ProcessID,FrameTime\n".to_string();
        for _ in 0..99 {
            data.push_str("42,10\n");
        }
        data.push_str("42,100\n");
        let r = analyze(
            data.as_bytes(),
            &ProcessIdentity {
                pid: 42,
                ..Default::default()
            },
            None,
        )
        .unwrap();
        assert_eq!(r.low_fps, 10.);
        assert!(r.average_fps > 90.);
    }
}
