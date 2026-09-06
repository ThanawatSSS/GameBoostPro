Game Boost Pro 3.3.1
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

PROFILES AND LANGUAGE
- Thai by default, with a TH/EN switch for the app and its dialogs
- Light: Windows Game Mode only; retains power plan and process scheduling
- Balanced (default): adds allowed GPU preference and background-capture settings
- Performance: adds allowed AboveNormal, QoS and Dynamic Priority Boost settings
- Master (cyan) changes games that inherit Master, without erasing Overrides
- Check Override (amber) to set a game-specific level; uncheck to inherit Master
- Apply Master to all clears every Override after confirmation
- During Boost, changes apply next session; the active recovery record is unchanged

WHAT PERFORMANCE CAN DO
- Saves the current Windows power plan before every Boost session
- Acer laptops keep the current custom/OEM plan, such as Nezha
- Desktop Performance uses the Advanced power choice; Smart activates or creates Ultimate
- Light and Balanced keep the current power plan on all supported systems
- Enables Windows Game Mode
- Disables Game DVR and background capture during the session
- Requests the high-performance GPU for the detected game
- Sets only the game process to AboveNormal priority
- Disables Windows power throttling for the game process and keeps Dynamic
  Priority Boost enabled
- Verifies which process settings were actually accepted and never retries a
  blocked anti-cheat protected process in a loop
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
- Use the launch action to select and launch, or select a profile without launching
- Use the add-game action for standalone games or unusual launcher layouts

ADVANCED MODE
- Advanced Settings stays available during Boost; edits apply next session
- Six controls independently manage Game Mode, capture, GPU preference,
  AboveNormal priority, process power throttling and Dynamic Priority Boost
- Power Plan has SMART, ULTIMATE and KEEP CURRENT choices for desktop Performance
- Reset to defaults enables all six controls and selects the SMART policy

GRAPHICS
- Settings: Save allowed high-performance GPU and background-capture choices
- These global permissions are applied by the selected Boost level next session
- Opens Windows Graphics, the installed NVIDIA App, or installed Control Panel
- Supports both standalone and Microsoft Store Control Panel installations
- Missing apps show a download or Store action instead of a broken launch action
- GPU compatibility is a separate view; DLSS / Reflex / Frame Gen are set in-game
- Reads NVIDIA GPU capability and display scan-out status without changing it
- Separates DLSS Super Resolution, Frame Generation, Reflex, NIS and Smooth Motion
- Does not claim that a feature is active when only the hardware is capable
- Competitive games are guided toward native resolution and in-game Reflex first

FRAME LAB
- Open directly from the dashboard; the dashboard stays usable for Boost / Restore
- Start the game, restore normal mode and record Baseline in a repeatable scene
- Return to the dashboard, enable Boost and record Boosted in the same scene
- Only the capture action matching the current mode is available
- The app waits for a detected live game; selecting an installed game is not enough
- Runs only when the user presses CAPTURE, for 15 seconds after a 3-second delay
- Uses the official Intel-signed PresentMon 2.5.1 standalone console binary
- Reports app-present Average FPS, 1% Low, P95 frame time and Present Mode
- Keeps Baseline and Boosted results in memory for an A/B comparison
- Deletes the temporary CSV immediately after analysis and never runs during the
  ordinary Boost monitor

ADMIN PERMISSION
- Admin granted is a non-interactive status, not a disabled or no-op button
- The portable app and installed app request Administrator permission at launch
- Setup also requests Administrator permission to install under Program Files
- The app does not inject code, modify game files, disable anti-cheat or overclock

HOW TO USE
1. Install with GameBoostPro-Setup.exe, or extract the portable ZIP.
2. Start GameBoostPro.exe and approve the Windows UAC prompt.
3. Leave automatic detection enabled and launch a game normally.
4. Game Boost restores the previous configuration after the game closes.
5. Manual Restore pauses automatic Boost until the game exits. Manually starting
   Boost or re-enabling Auto Mode resumes it immediately.

RECOVERY
- Privileged recovery state is saved under the administrator-protected
  HKLM\SOFTWARE\GameBoostPro key before changes.
- User preferences remain under %LOCALAPPDATA%\CodexGameBoost.
- Valid state from an older release is allowlisted and migrated once; unverifiable
  process identity is discarded instead of being trusted.
- Recovery is bound to the Windows account that started the Boost session.
- If Windows or the app closes unexpectedly, reopen Game Boost Pro and press RESTORE.
- The uninstaller blocks removal while a RESTORE is still pending and preserves
  the user's preferences after normal removal.

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
- The telemetry checkbox can pause these counters while the window is open.
- GPU 3D reports the busiest 3D engine, without adding separate GPUs together.
- An unavailable reading is shown as --. High GPU utilization is not a temperature
  or overheating warning.
- Recovery state is cached in memory during play, while RESTORE deliberately reads
  the protected saved state again so crash recovery remains authoritative.
- This release does not run continuous frame capture or inject an overlay into games.

THIRD-PARTY COMPONENT
PresentMon 2.5.1 is redistributed under its MIT License. The portable package and
installed application include tools\PresentMon-LICENSE.txt.

Game Boost Pro 3.3.1 is distributed under the MIT License.
