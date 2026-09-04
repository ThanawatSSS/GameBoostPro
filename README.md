# Game Boost Pro 3.2.0

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

- Detects running games and Steam, Epic Games, and Riot installations
- Searchable Game Library with source filters, manual EXE import, profile selection,
  install-folder access, and direct launch
- Smart Power Plan by default: Acer laptops keep the user's current custom/OEM
  plan, while desktop PCs use Ultimate Performance
- Advanced Power Plan selector with Smart, forced Ultimate, and Keep Current modes
- Visible Advanced Mode with six independently reversible tuning controls
- Windows Game Mode and per-game high-performance GPU preference
- Reversible Game DVR/background capture settings
- `AboveNormal` game priority and per-game power-throttling control without
  `High` or `Realtime` priority
- Low-priority, asynchronous CPU, memory, GPU 3D, and game-process monitoring
- Adaptive monitoring: 1.5 seconds while discovering a game and 3 seconds after
  the game and Boost state are stable
- CPU, memory, and GPU telemetry pauses while the window is hidden in the tray;
  automatic game detection and restore remain active
- Background game-library discovery so launcher manifests do not block first paint
- Protected Discord, TeamSpeak 3, NitroSense, and Acer hardware processes
- Atomic recovery state saved before tuning in an administrator-protected Windows store
- Process identity guard using PID, executable name, and process start time before
  restoring scheduling values
- Verified process tuning state: Applied, Partial, Blocked, or Not Retained; blocked
  games are not repeatedly forced every monitor cycle
- Read-only Graphics Advisor for DLSS, Frame Generation, Reflex, NIS display-route
  eligibility, and Smooth Motion guidance
- User-started 15-second Frame Lab powered by the signed PresentMon 2.5.1 console
  binary; reports app-present average FPS, 1% low, P95 frame time, and present mode
- Native portable executable and native Setup/Uninstall package

## Safety model

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
- Portable: extract `GameBoostPro-Portable-v3.2.0.zip`, then run `GameBoostPro.exe`

Windows SmartScreen may identify unsigned community builds as an unknown publisher.
Verify the SHA-256 hashes in the GitHub Release before running them.

## License

Game Boost Pro uses the MIT License. See [LICENSE](LICENSE). PresentMon attribution
and its license are in [third_party/PresentMon](third_party/PresentMon).
