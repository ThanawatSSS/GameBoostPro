# UX Repair Verification

Local 3.3.0 build, 2026-09-05. Not published or installed automatically.

## Reproduced Problems

The previous executable failed the GUI regression probe because Admin was a
keyboard-focusable button with no action when elevated, the game list did not
grow with a taller window, and Frame Lab had no direct dashboard entry.

On this PC, read-only inspection found NVIDIA App at the standard CEF executable
path. The standalone nvcplui.exe path, Get-StartApps and Get-AppxPackage NVIDIA
queries did not locate Control Panel. The new resolver, run from a background
thread, reported NvidiaApp=true and ControlPanel=false. No driver settings were
changed or downloads started.

## Current Interaction Contract

- Admin granted is a Label with no keyboard stop; a separate button requests UAC
  only when the process is not elevated.
- Below 900 logical pixels, dashboard controls reflow: a second toolbar row,
  full-width Master slider, Boost before the game library. Wider/taller windows
  keep the library beside the session and expand its available height.
- Vertical scrolling handles short windows and large text. This is not a web app
  and does not promise phone-size layouts; the native minimum width is 640 pixels
  before Windows DPI scaling.
- Library, Advanced, Graphics and Frame Lab are resizable. Fixed-format controls
  keep their size while list/content regions grow; smaller work areas scroll.
- Graphics has separate settings and compatibility views. Save changes two
  existing global allow switches, not DLSS, NIS, Reflex or driver tuning. Preset
  policy still decides whether an allowed setting is requested on the next Boost.
  Cancel/close without Save does not mutate configuration.
- Driver destinations use full discovered executable paths or a validated,
  installed Control Panel AUMID. NVIDIA App and Control Panel remain distinct.
  Missing products offer a download/Store page, not a misleading Open action.
- Frame Lab is modeless. Opening it without a live game shows a waiting state.
  Dashboard detection supplies PID, name and start-time identity. Captures are
  enabled only for the current mode and a verified capture component. Capture
  still rechecks the target and tool hash before collecting data.
- The target and Boost-session token cannot silently change during a capture.
  Existing before/after target validation and session-change result rejection
  remain intact. Baseline/Boosted history is separated by process start time.
- Advanced edits during Boost affect future session options, not stored recovery
  data. The UI language can be changed outside a capture; an open Frame Lab is
  recreated in the chosen language while its in-memory result history is retained.

## Evidence and Limits

Run tests/Test-Release.ps1 for the complete release gate. Run
tests/Test-GuiVisual.ps1 -EvidenceDirectory artifacts/ux-repair for screenshots.

The GUI test exercises real controls with isolated configuration/recovery files:
Admin semantics, direct modeless Frame Lab, actual compact width, workspace
height growth, compact ordering, Master/Override behavior, next-session settings,
graphics save, missing Control Panel destination, matching capture modes, and
no-game capture prevention. It checks parent bounds and table-cell overlap while
forms are visible. TH/EN screenshots include desktop, compact and simulated DPI
states. Representative GPU/game data in these images is a test fixture, not a
claim that the fixture game is running on this PC.

Policy tests cover installed desktop paths, discovered Store AUMIDs, absent
products, and rejection of bare executable names or malformed identifiers.
Packaging separately verifies PresentMon's pinned hash and signature; screenshot
tests simulate component readiness without launching a capture.

Performance budgets have not been relaxed. GUI startup, ordinary monitor, paint
and game-detection probes pass. Cold GPU-counter initialization remains on a
background worker and is not represented by steady-state P95 measurements.

No live game A/B benchmark, 24-hour session, real mixed-monitor DPI transition,
Store-installed Control Panel activation, or driver-panel setting change was
performed. Screenshot tests do not prove game FPS improvement.

## Primary References

- [NVIDIA Control Panel Windows Store App](https://nvidia.custhelp.com/app/answers/detail/a_id/4733/~/nvidia-control-panel-windows-store-app):
  DCH Control Panel may need a separate Store installation.
- [Microsoft: Find an installed app's AUMID](https://learn.microsoft.com/en-us/windows/configuration/store/find-aumid):
  installed-app identity and Shell AppsFolder discovery.
- [Microsoft PowerToys Workspaces launcher](https://github.com/microsoft/PowerToys/blob/main/src/modules/Workspaces/WorkspacesLauncher/AppLauncher.cpp):
  packaged-app launch through shell:AppsFolder and a discovered AUMID.
