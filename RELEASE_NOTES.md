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
