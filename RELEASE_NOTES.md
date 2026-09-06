# Game Boost Pro 3.3.1

- Ships the dashboard and usability changes from the local 3.3.0 previews below.
- Fixes duplicate legacy Start Menu shortcuts pointing to older portable copies.
  Setup consolidates verified product shortcuts without deleting legacy binaries.
- Updates the existing Program Files installation and stable Installed Apps entry;
  preferences and recovery data are preserved. Pending recovery blocks an update.
- Stages owned payload files before replacing them and rolls back already-replaced
  files if a later file is locked or cannot be replaced.
- Centralizes the build version, removes the stale Setup 3.2 badge and shows 3.3.1
  in the main window. Adds installer idempotence, rollback and shortcut safety tests.

# Game Boost Pro 3.3.0 (local previews, not published)

- UX repair: replaced the no-op Admin button with a status label and conditional UAC action.
- Reflowed the narrow dashboard and let the game workspace expand with taller windows;
  Library, Advanced, Graphics and Frame Lab dialogs are now resizable.
- Split Graphics settings from GPU compatibility. Save now changes real allowed
  Boost settings without modifying an active recovery snapshot.
- Replaced bare nvcplui.exe launches with discovery of NVIDIA App, standalone Control
  Panel and installed Store AUMIDs; missing tools have explicit download/Store actions.
- Added a direct, non-modal Frame Lab entry, live-game prerequisites, mode-specific
  capture actions and a return-to-dashboard action for the Baseline/Boosted workflow.
- Replaced the large Boost ring with a compact native dashboard and Boost/Restore button.
- Added a persistent game list, Master level, and explicit per-game Override checkbox.
  Master changes preserve Overrides; Apply Master to all clears them after confirmation.
- Added Light / Balanced / Performance profiles, defaulting to Balanced, and detached
  session options so live recovery data never changes when a profile is edited.
- Added TH/EN across the dashboard, dialogs, and messages, with Thai-capable fonts.
- Distinguished Master (cyan), Override (amber), active (lime), and next-session
  status (coral) using text labels as well as color; added contrast assertions.
- Persisted manually imported games and enabled exact-path detection independent of selection.
- Replaced Processor counter initialization with native CPU timing; unavailable
  readings are explicit, including systems above 64 logical processors.
- Added a resizable layout, DPI scaling, scroll access on smaller work areas, Thai
  action labels, and tooltips. Library and Advanced Settings remain visible.
- Administrator status now reflects the process token; non-admin Boost is blocked
  and a UAC restart action is available.
- Added progress for power, recovery capture, Windows settings, and process verification.
- Manual Restore pauses automatic Boost until the game exits.
- Added an optional telemetry switch that leaves game detection and restore active.
- Corrected GPU 3D aggregation across engines/GPUs and unavailable metric states.
- Replaced repeated powercfg queries/switches with documented Windows power APIs,
  including verification before completing a restore.
- Added GPU aggregation, power-query, config migration, manual-restore, non-admin,
  resizing and simulated 150/200-percent scale checks.

No additional hardware, service, network, or security tweaks are enabled. These
changes reduce app work and improve correctness; they are not a measured game-FPS
claim. Real mixed-monitor DPI transitions and a 24-hour gaming session have not
been validated in this local build. Research: [redesign and optimization](docs/redesign-and-safe-optimization.md).

Current design and verification: [dashboard profiles](docs/dashboard-profiles.md).

# Game Boost Pro 3.2.0

## Measured performance release

- Fixed automatic detection pairing a protected running game with an unrelated
  manually selected executable when Windows denied access to the running path.
- Added process identity guards using PID, process name, start time, and path when
  available. Restore now skips legacy or stale process identities instead of
  risking a reused PID.
- Moved privileged recovery state to an administrator-protected Windows store.
  Legacy state is allowlisted and sanitized before one-time migration, unsafe
  legacy data blocks a new Boost, recovery is bound to its Windows account SID,
  and uninstall is blocked while restore is pending.
- Verifies every requested process scheduling value after applying it, checks
  retention once, reports Applied, Partial, Blocked, or Not Retained, and never
  fights an anti-cheat process in a retry loop.
- Added Smart Power Plan. Acer + NitroSense laptops keep the current custom/OEM
  plan by default, including plans such as Nezha; desktop PCs use Ultimate.
  Forced Ultimate and Keep Current remain explicit Advanced choices.
- Added a read-only Graphics Advisor for DLSS Super Resolution, ordinary Frame
  Generation, Multi Frame Generation eligibility, Reflex, driver NIS scan-out
  readiness, and Smooth Motion. It does not claim that an in-game option is active.
- Added Frame Lab with a user-started 15-second PresentMon capture, reporting
  app-present Average FPS, 1% Low, P95 frame time, and the observed presentation
  mode for Baseline versus Boosted comparisons.
- Bundled the official Intel-signed PresentMon 2.5.1 standalone console binary,
  verifies its SHA-256 before every capture, and removes temporary CSV output
  immediately after analysis. PresentMon never runs during normal monitoring.
- Moved platform discovery and GPU counter initialization off the first-paint path.
  Automated cold-start checks remain below the 1.5-second release budget, while
  ongoing monitor work stays on a BelowNormal background thread.

# Game Boost Pro 3.1.1

## Long-session performance patch

- Reused Boost Dial and metric fonts, pens, and brushes instead of allocating GDI
  wrappers on every paint.
- Cached recovery state in memory during monitoring and retained a fresh disk read
  for RESTORE, preserving crash-recovery authority.
- Removed the duplicate state-file read from every active monitor pass and kept the
  read/tune sequence under the existing system lock.
- Added a short access-denied cache for protected process paths during deep scans.
- Added a verified running-game PID fast path so stable sessions avoid enumerating
  every process on each monitor pass.
- Reduced stable in-game monitoring frequency from 1.5 to 3 seconds and runs monitor
  collection at `BelowNormal` worker priority.
- Paused CPU, memory, and GPU counter collection while the app is hidden in the
  tray without pausing game detection or automatic restore.
- Renamed the visible HighQoS control to Disable power throttling so the UI matches
  the Windows process policy it actually applies.
- Added a Boost Dial paint budget and stable monitor duty estimate to release tests.
- Fixed process retuning when a detected game restarts with a new PID.

Measured on the release test PC: Boost Dial paint p95 remains below 1 ms. The
conservative p95 monitor-duty estimate is below 0.3% of one logical CPU core while
the window is visible and approximately 0.1% in the tray. Results vary by machine
and game library size.

# Game Boost Pro 3.1.0

## Quality release

- Removed the main GUI stall by moving monitoring and game detection off the UI
  thread, batching GPU counter reads, caching deep process scans, and avoiding
  redundant redraws.
- Moved Steam, Epic, and Riot catalog discovery out of application startup and
  into a guarded background refresh.
- Reduced measured UI monitor p95 from about 117 ms in 3.0 to below 1 ms in 3.1
  on the release test machine. Background metrics and detection remain below the
  automated 25 ms p95 budget.
- Rebuilt Game Library with search, source filters, ADD EXE, OPEN FOLDER,
  USE PROFILE, and PLAY NOW actions.
- Added correct Riot Client launch arguments for VALORANT.
- Added a visible Advanced Mode with six controls that map directly to the next
  Boost session, plus RESET BEST.
- Replaced the duplicate Admin/Ready wording with a single ADMIN ACTIVE status.
- Fixed window sizing that could clip the footer and dialog actions.

## Performance policy

- Best Mode now uses Ultimate Performance exclusively.
- If Ultimate Performance is absent, Game Boost Pro duplicates the Windows
  Ultimate template and reuses the resulting Game Boost Pro Ultimate plan.
- The original power plan is still captured before Boost and restored afterward.

## Safety and support

- Uses `AboveNormal`, never `High` or `Realtime` process priority.
- Does not stop or reconfigure Windows, NitroSense, or Acer services.
- Preserves Discord, TeamSpeak 3, NitroSense, and Acer hardware processes.
- Writes recovery state before system changes and restores registry, GPU, power,
  and process scheduling state.
- Supports Windows 10/11 x64 desktop PCs and Acer laptops with NitroSense.
- Other OEM laptops remain intentionally blocked until their control profiles are
  validated.
