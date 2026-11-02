# v4.1.0 verification and limits

These observations describe this release candidate, not a guarantee of FPS
improvement or compatibility with every PC, driver, game, or anti-cheat.

## Engineering checks

- Rust: 22 unit tests passed; formatting and Clippy checks passed.
- UI fixture: 50 Thai/English, view, and viewport combinations passed, with
  checks for Master/Override, settings conflicts, and unavailable settings.
- On an Acer Nitro ANV15-51: a Quality Boost/Restore round trip verified the
  original power plan and Game Mode registry values and cleared recovery.
- A real Windows session exposed `EpicWebHelper.exe` being detected as a game.
  The detector was changed to match known game executables or explicitly added
  executable paths, and the fixed build was checked with that helper still running.
- On the Windows desktop, X hid the debug window while its process remained
  alive, and launching it again restored the same window. The installed build
  also hid on X. The user confirmed the tray icon and right-click menu appeared;
  individual tray commands were not all exercised in the installed build.
- Setup upgraded the previous installation in place. The Start Menu shortcut
  pointed to the installed application and there was one uninstall entry.

Steam/Epic library discovery does not necessarily identify the game executable.
For games outside the known list, add the actual game `.exe` for automatic
detection. When it belongs to exactly one discovered library directory, the
application links it to that entry and retains its profile.

The above checks do not establish 24-hour stability, all Performance/Ultimate
combinations, actual FPS gains, or behavior on a separate physical desktop PC.

## Security review limits

The requested security audit was **incomplete and source-only**. The required
hostile-code sandbox was unavailable, the Windows host could not enforce the
upstream validators' no-follow input checks, and the code changed following
initial source review. Independent source reviews did identify issues which were
addressed during development, but no complete current-source security audit or
penetration test was established. This is not a claim that the software is free
of vulnerabilities. Windows token/ACL behavior and races with external writers
need further validation in an isolated environment.

The installer and application are unsigned. PresentMon retains its publisher
signature. Verify release downloads against the published SHA256SUMS.txt.
