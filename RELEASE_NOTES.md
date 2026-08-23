# Game Boost Pro 3.0.0

## Highlights

- Added explicit support profiles for Acer laptops with NitroSense and desktop PCs.
- Added a platform guard that blocks unvalidated laptops before Boost can start.
- Added a Steam, Epic Games, and Riot Game Library.
- Added GPU 3D activity telemetry beside CPU and memory.
- Game Boost owns the Windows power plan while NitroSense remains the Acer hardware
  and fan authority.
- Added native Setup, Uninstall, Start Menu/Desktop shortcuts, and a portable ZIP.
- Both installed and portable executables request Administrator permission.

## Safety

- Uses `AboveNormal`, never `High` or `Realtime` process priority.
- Preserves Discord, TeamSpeak 3, NitroSense, and Acer services.
- Writes recovery state atomically before system changes.
- Restores the original power plan, registry settings, GPU preference, and process
  scheduling state.

## Supported platforms

- Windows 10/11 x64 desktop PCs
- Acer laptops with NitroSense

Lenovo and other OEM laptops are intentionally blocked in this release.
