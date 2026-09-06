# Game Boost Pro 3.3.1

One-button, reversible Windows gaming optimization with automatic game detection.

## Supported systems

| Platform | Status | Control boundary |
|---|---:|---|
| Acer laptop with NitroSense | Supported | Game Boost owns the Windows power plan; NitroSense owns fans and Acer hardware |
| Desktop PC | Supported | Game Boost owns Windows gaming settings and the power plan |
| Other laptops | Blocked | A dedicated OEM profile must be validated before support is enabled |

The current release intentionally supports only **Acer + NitroSense laptops** and
**desktop PCs**. Unsupported laptops are detected before system changes are made.

## Features

- Compact native dashboard with a Boost/Restore button, a persistent game list,
  explicit Master/Override selection, and a TH/EN language switch
- Thai text uses Leelawadee UI; source labels accompany cyan Master and amber
  Override colors, lime active status, and coral next-session status
- Three levels: Light, Balanced (default), and Performance. Master changes only
  inherited profiles; Apply Master to all explicitly clears Overrides after confirmation
- Per-game Override checkbox and slider; unchecking it restores Master inheritance
- Profile edits during Boost are staged for the next session, without altering
  the active session's recovery settings
- Narrow windows reflow the toolbar, Master controls and game workspace; larger
  windows give the library more space. All main dialogs are resizable
- Admin permission is a status label; the elevation button appears only when needed
- Step-by-step Boost/Restore status; minor launch errors appear inline
- Manual Restore pauses automatic Boost until the running game exits
- Detects running games and Steam, Epic Games, and Riot installations
- Searchable Game Library with source filters, manual EXE import, profile selection,
  install-folder access, and direct launch
- Light and Balanced keep the current plan. Performance keeps the current plan
  on Acer laptops; desktop Performance uses the chosen Advanced power policy
- Advanced Power Plan selector with Smart, Ultimate, and Keep Current modes;
  this selector affects desktop Performance only
- Visible Advanced Mode with six independently reversible tuning controls
- Windows Game Mode and per-game high-performance GPU preference
- Reversible Game DVR/background capture settings
- `AboveNormal` game priority and per-game power-throttling control without
  `High` or `Realtime` priority
- Low-priority, asynchronous CPU, memory, GPU 3D, and game-process monitoring
- Adaptive monitoring: 1.5 seconds while discovering a game and 3 seconds after
  the game and Boost state are stable
- CPU, memory, and GPU telemetry can be switched off and pauses while hidden in the tray;
  automatic game detection and restore remain active
- GPU 3D shows the busiest engine after combining processes on that same engine;
  separate GPUs are not added together, and missing readings appear as `--`
- Native Windows power APIs read, switch, and verify the active plan; `powercfg`
  remains limited to finding or creating an Ultimate plan
- CPU usage uses native system timing instead of initializing Processor performance
  counters. More than 64 logical processors returns unavailable rather than
  mislabeling one processor group as whole-system usage
- Background game-library discovery so launcher manifests do not block first paint
- Protected Discord, TeamSpeak 3, NitroSense, and Acer hardware processes
- Atomic recovery state saved before tuning in an administrator-protected Windows store
- Process identity guard using PID, executable name, and process start time before
  restoring scheduling values
- Verified process tuning state: Applied, Partial, Blocked, or Not Retained; blocked
  games are not repeatedly forced every monitor cycle
- Separate Graphics settings and GPU compatibility views. Explicit Save updates
  allowed GPU-preference and background-capture settings for the next Boost session
- Installed NVIDIA App, standalone Control Panel and Store Control Panel are
  discovered separately; missing apps offer labeled download/Store destinations
- DLSS, Frame Generation, Reflex, NIS display-route eligibility and Smooth Motion
  guidance remain capability information, not claims about active game settings
- User-started 15-second Frame Lab powered by the signed PresentMon 2.5.1 console
  binary; reports app-present average FPS, 1% low, P95 frame time, and present mode
- Frame Lab opens directly from the dashboard without blocking Boost/Restore;
  capture buttons require a detected live game and the matching Baseline/Boost state
- Native portable executable and native Setup/Uninstall package
- In-place updates with staged payload replacement and rollback on file-copy errors;
  confirmed legacy Start Menu shortcuts are consolidated into the installed app
- A shared build version drives the app, installer and portable archive; the main
  window displays its version to distinguish installed and older portable copies

## Safety model

Preset settings are constrained by the six Advanced allow switches:

| Level | Windows Game Mode | GPU preference / background capture | Process scheduling | Power |
|---|---|---|---|---|
| Light | Allowed | Unchanged | Unchanged | Current |
| Balanced | Allowed | Allowed | Unchanged | Current |
| Performance | Allowed | Allowed | AboveNormal / QoS / dynamic boost, when allowed | Current on Acer; Advanced policy on desktop |

These are Windows profiles, not in-game graphics quality presets. GPU preference
may require restarting the game. No FPS improvement is promised by a preset name.

Game Boost Pro does not overclock, undervolt, inject into games, modify game files,
disable anti-cheat, stop Windows services, or control laptop fans. Every changed
Windows value is captured and restored at the end of the session.

Recovery data that can drive privileged restore operations is stored under the
administrator-protected `HKLM\SOFTWARE\GameBoostPro` key. A valid recovery file
from an older release is allowlisted, stripped of unverifiable process identity,
and migrated once; an invalid legacy state blocks a new Boost instead of being
trusted. Each state is bound to the Windows account that started the session, and
user preferences remain under `%LOCALAPPDATA%\CodexGameBoost`.

Frame Lab is diagnostic and opt-in. PresentMon is never launched by the ordinary
Boost monitor, each capture is bounded, and its temporary CSV is deleted after
analysis. Game Boost Pro verifies the bundled binary's SHA-256 before running it.

The executable requests Administrator permission because changing the active power
plan and another process's scheduling policy may require elevation.

## Build

Run `build.ps1` in Windows PowerShell. The build uses the .NET Framework C# compiler
included with Windows. The repository includes the official signed PresentMon 2.5.1
x64 console binary and its MIT license for Frame Lab.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Artifacts are written to `dist/`.

## Install

- Installer: run `GameBoostPro-Setup.exe`
- Portable: extract `GameBoostPro-Portable-v3.3.1.zip`, then run `GameBoostPro.exe`

Windows SmartScreen may identify unsigned community builds as an unknown publisher.
Verify the SHA-256 hashes in the GitHub Release before running them.

## License

Game Boost Pro uses the MIT License. See [LICENSE](LICENSE). PresentMon attribution
and its license are in [third_party/PresentMon](third_party/PresentMon).
