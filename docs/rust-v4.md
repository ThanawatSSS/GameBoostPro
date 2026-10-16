# Game Boost Pro 4.0.0

Windows game-session control, rebuilt with Rust and Tauri. Thai and English interface.

## Supported PCs

- Windows 10/11 x64 desktop PCs.
- Acer laptops with NitroSense; connect AC power before Boost.
- Other laptops are detected but Boost is disabled pending platform validation.

NitroSense continues to control fans and Acer hardware. The current power plan,
including a custom Nezha plan, is preserved by default. Performance can optionally
switch to a dedicated Ultimate plan. Restore returns to the previous plan unless
another application or the user has changed it in the meantime.

## Daily workflow

1. Open the app. Use Run as admin when starting a Boost session or frame recording.
2. In Game library, Find games imports installed Steam/Epic games. Add .exe adds a
   local game. CS2, PUBG, VALORANT and other known processes are detected directly;
   other installed-library games are checked by their installation directory.
3. Select one game. Launch starts only that game, using the standard-user token
   even when Game Boost Pro is elevated. Launching a game does not launch the entire library.
4. Set the Master slider, or enable Override for the selected game. A Master badge
   means inheritance; the amber Override badge means the game has its own profile.
5. Start Boost, play, then Restore. Closing an active session hides the window to
   the tray. The tray can reopen it or Restore & quit. Idle windows close normally.

Automatic Boost is opt-in after migrating to v4. Known games are checked every
1.5 seconds while idle; active sessions use a 3-second interval. Directory discovery
has a 10-second cache. Automatic sessions restore 10 seconds after their bound
game process ends. Manual Restore suppresses automatic re-boost for that process.

## Profiles and boundaries

| Setting | Quality | Balanced | Performance |
|---|---|---|---|
| Windows Game Mode, when enabled | On | On | On |
| Background recording, when configured | Preserved | Paused | Paused |
| High-performance GPU preference, when configured | Preserved | Per game | Per game |
| AboveNormal process priority, when allowed | Preserved | Preserved | Verified per game |
| Current power plan | Preserved | Preserved | Preserved by default; optional Ultimate |

These are Windows optimization profiles, not in-game image-quality presets.
DLSS, FSR, Reflex and Frame Generation remain settings in supported games. GPU
selection generally takes effect on the next launch; changing it cannot move an
already-running game to another GPU. Anti-cheat refusals are reported, not bypassed.
Discord, TeamSpeak, NitroSense, Windows services, anti-cheat, fans, overclocking,
timer-resolution, RAM-cleaning and network registry tweaks are not modified.

## Frame Lab

Record 15 seconds with a game actively rendering. Record Baseline before Boost,
then record Boost in the same scene, resolution and frame-cap conditions. Results
show average FPS, the mean of the slowest 1% of frames converted to FPS, and P99
frame time. Samples are bound to PID/start time/path and separated by swapchain.
The normal game monitor does not run PresentMon. Recording runs only on request,
uses the pinned Intel PresentMon 2.5.1 binary and stops after a bounded capture.
Frame results are session-local; closing the app clears this comparison history.

No FPS gain or universal stutter reduction is promised. Temperature and thermal
throttling are not measured, and 24-hour gaming stability is not established by
short functional tests. OEM/driver and in-game tuning still determine performance.

## Installation and migration

Use the Setup EXE to install or update the same Game Boost Pro location. Restore
the existing session and exit the old app first. A pending v3 recovery marker
blocks migration; use v3 Restore before updating. v3 config files are retained;
v4 imports language, Master settings, manual games and their exact-path overrides.
New library discovery rebuilds Steam/Epic entries. v4 uses config-v4.json and an
owner-bound HKLM RecoveryV4 journal written before system changes.

Extract the complete Portable ZIP, including tools. Windows WebView2 Runtime is
required. The installer does not download or run prerequisite installers. If the
runtime is missing, install it directly from [Microsoft WebView2](https://developer.microsoft.com/microsoft-edge/webview2/).
Setup
and Game Boost Pro are not Authenticode-signed. PresentMon retains its Intel
signature. Verify downloaded files against SHA256SUMS.txt.

## Development

Use Windows x64, Rust MSVC (tested with 1.99.1), Visual Studio C++ tools, and Node.js
(tested with 26.2.0). npm and Cargo dependency locks are checked in.

```powershell
./scripts/build-rust.ps1
```

For offline verification after dependencies are cached:

```powershell
cargo test --manifest-path src-tauri/Cargo.toml --locked --offline
cargo clippy --manifest-path src-tauri/Cargo.toml --locked --offline -- -D warnings
```

For frontend layout tests, run npm run dev and npm run test:ui. The Playwright test
uses a test-only IPC fixture; native Windows walkthroughs separately check the
actual Rust bridge and real machine state. Legacy build.ps1 and src/*.cs remain
v3 sources for recovery/reference and are not part of the v4 executable.
