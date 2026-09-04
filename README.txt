Game Boost Pro 3.1.1
====================

SUPPORTED PLATFORMS
- Acer laptop with NitroSense installed
- Desktop PC without an OEM laptop control application
- Windows 10/11 x64

NOT SUPPORTED YET
- Lenovo, ASUS, MSI, Dell, HP and other laptops
- Acer laptop without NitroSense

The app detects unsupported laptops and blocks Boost. This prevents a generic
power profile from competing with an untested OEM control application.

WHAT BEST MODE DOES
- Saves the current Windows power plan before every Boost session
- Activates Ultimate Performance and creates the plan automatically when absent
- Enables Windows Game Mode
- Disables Game DVR and background capture during the session
- Requests the high-performance GPU for the detected game
- Sets only the game process to AboveNormal priority
- Disables Windows power throttling for the game process and keeps Dynamic
  Priority Boost enabled
- Restores the original power plan and registry values when the session ends

ACER + NITROSENSE MODE
- Game Boost owns the Windows power plan
- NitroSense remains responsible for Acer system mode, fan control, temperatures,
  keyboard lighting and Acer hardware communication
- Game Boost never stops NitroSense or Acer hardware services
- Discord and TeamSpeak 3 are protected

DESKTOP PC MODE
- No NitroSense or OEM laptop utility is required
- Game Boost controls the Windows gaming settings and power plan directly

GAME LIBRARY
- Detects installed games from Steam, Epic Games and Riot
- Detects popular running games such as CS2, PUBG and VALORANT
- Search and filter by Steam, Epic, Riot or Manual source
- Use PLAY NOW to select and launch, or USE PROFILE to select without launching
- Use ADD EXE for standalone games or unusual launcher layouts

ADVANCED MODE
- The ADVANCED BEST button is always visible in the main window
- Six controls independently manage Game Mode, capture, GPU preference,
  AboveNormal priority, process power throttling and Dynamic Priority Boost
- RESET BEST enables all six controls; Ultimate Performance remains mandatory

ADMIN PERMISSION
- The portable app and installed app request Administrator permission at launch
- Setup also requests Administrator permission to install under Program Files
- The app does not inject code, modify game files, disable anti-cheat or overclock

HOW TO USE
1. Install with GameBoostPro-Setup.exe, or extract the portable ZIP.
2. Start GameBoostPro.exe and approve the Windows UAC prompt.
3. Leave automatic detection enabled and launch a game normally.
4. Game Boost restores the previous configuration after the game closes.

RECOVERY
- The previous state is saved under %LOCALAPPDATA%\CodexGameBoost before changes.
- If Windows or the app closes unexpectedly, reopen Game Boost Pro and press RESTORE.
- Uninstalling the app preserves this recovery/configuration directory.

PERFORMANCE NOTE
Game Boost cannot create performance beyond the CPU, GPU, cooling and power limits
of the computer. The target is steadier frame time and fewer avoidable background
interruptions. Actual FPS improvement depends on the existing bottleneck.

MONITORING OVERHEAD
- Monitoring runs outside the UI thread at BelowNormal worker priority.
- It checks every 1.5 seconds while waiting for a game, then every 3 seconds once
  a running game and active Boost state are stable.
- CPU, memory and GPU telemetry pauses while the app is hidden in the tray; game
  detection and automatic restore remain active.
- Recovery state is cached in memory during play, while RESTORE deliberately reads
  the saved file again so crash recovery remains authoritative.
- This release does not run continuous frame capture or inject an overlay into games.

Game Boost Pro 3.1.1 is distributed under the MIT License.
