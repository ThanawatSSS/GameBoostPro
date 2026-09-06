# Dashboard and Profiles

Local 3.3.0 build, 2026-09-05. Supersedes the earlier ring-based 3.3.0 UI preview.
The published GitHub release remains unchanged.
The subsequent usability repair is documented in [UX repair](ux-repair.md).

## User-visible contract

- Master uses cyan and a literal MASTER label in the game list.
- Override uses amber, an explicit checkbox, and a literal OVERRIDE label.
- Moving Master changes only inherited games. Apply Master to all confirms the
  number of overrides to clear, then resets all games to inheritance.
- Checking Override copies the effective level before enabling the game slider.
  Unchecking it removes the override and immediately follows the current Master.
- During a session the active level remains lime; staged profile edits are labeled
  next session in coral. The effect rows show the active recovery snapshot.
- Selecting a different game edits its future profile; detection does not silently
  move the selected game. The active game's name appears in session status.
- Light / Balanced / Performance change allowed Windows settings, not rendering
  resolution, texture quality, NIS, DLSS, fan speed, or overclocking.
- Advanced switches are global permissions for presets, not extra per-game overrides.
- Acer laptops retain their current power plan at every preset, including Nezha.
  Desktop Performance uses the Advanced policy; Light and Balanced retain the plan.

## Implementation

- `src/Dashboard.cs`: layout, bilingual UI copy, source colors, controls, profile editing.
- `src/BoostProfiles.cs`: stable path identities, preset matrix, normalization and snapshots.
- `src/GraphicsWorkspace.cs`: driver-tool discovery and native dialog layout helpers.
- `src/GameBoostPro.cs`: existing detection, tuning, recovery, diagnostics and lifecycle.
- `tests/ProfileProbe.cs`: preset and safety checks, persistence, legacy manual import,
  keyboard interaction, CPU arithmetic and color contrast.
- `tests/GuiVisualProbe.cs`: real WinForms rendering with isolated state and fixture games.
  Covers TH/EN, Override/Master transitions, active and staged settings, empty search,
  narrow/wide windows and simulated 150/200-percent scale. Screenshots are not proof
  of real gaming performance or a live system Boost.

## Monitoring

CPU sampling now uses [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes).
Kernel time includes idle time; the calculation subtracts idle from the elapsed
kernel-plus-user total. Windows limits this API to the calling processor group
above 64 processors, so the app checks
[GetActiveProcessorCount](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getactiveprocessorcount)
and reports unavailable on such systems rather than claiming a whole-system total.

Telemetry remains off the UI thread, opt-out, paused in the tray and during
transitions. GPU counter initialization can still take hundreds of milliseconds
on its first background sample. P95 steady sampling does not describe that cold
initialization, actual CPU utilization, or the game's frame-time impact.

The tests never Boost or restore this PC: they use isolated recovery files and
the tuning engine explicitly rejects full system tuning in that host. Existing
scheduling tests exercise only a dedicated child process. Real mixed-monitor DPI
changes, prolonged sessions, and A/B gaming benchmarks remain unverified.

## Validation commands

Run `tests/Test-Release.ps1` for packaging, security assertions, behavior, performance
budgets, and UI checks. Run `tests/Test-GuiVisual.ps1 -EvidenceDirectory artifacts/dashboard`
to retain the rendered screenshots. Performance budgets were not relaxed.
