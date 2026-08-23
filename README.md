# Game Boost Pro 3.1

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
- Ultimate Performance only; the plan is created automatically when Windows does
  not already expose it
- Visible Advanced Mode with six independently reversible tuning controls
- Windows Game Mode and per-game high-performance GPU preference
- Reversible Game DVR/background capture settings
- `AboveNormal` game priority and HighQoS without `High` or `Realtime` priority
- Asynchronous, cached CPU, memory, GPU 3D, and game-process monitoring
- Background game-library discovery so launcher manifests do not block first paint
- Protected Discord, TeamSpeak 3, NitroSense, and Acer hardware processes
- Atomic recovery state saved before tuning
- Native portable executable and native Setup/Uninstall package

## Safety model

Game Boost Pro does not overclock, undervolt, inject into games, modify game files,
disable anti-cheat, stop Windows services, or control laptop fans. Every changed
Windows value is captured and restored at the end of the session.

The executable requests Administrator permission because changing the active power
plan and another process's scheduling policy may require elevation.

## Build

Run `build.ps1` in Windows PowerShell. The build uses the .NET Framework C# compiler
included with Windows and has no third-party runtime dependency.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Artifacts are written to `dist/`.

## Install

- Installer: run `GameBoostPro-Setup.exe`
- Portable: extract `GameBoostPro-Portable-v3.1.0.zip`, then run `GameBoostPro.exe`

Windows SmartScreen may identify unsigned community builds as an unknown publisher.
Verify the SHA-256 hashes in the GitHub Release before running them.

## License

MIT License. See [LICENSE](LICENSE).
