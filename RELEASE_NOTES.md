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
