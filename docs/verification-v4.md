# v4.0.0 Verification

Checked on 2026-10-16. This describes observed checks, not universal compatibility
or a promise of FPS gains.

## Engineering checks

- Rust unit tests: 9 passed; formatting and Clippy checks passed.
- Frontend fixture tests: 50 language/view/viewport combinations, including TH/EN,
  search escaping, Master/Override inheritance and selected-game-only launch.
- Actual embedded debug UI connected to Rust: Boost/Restore round trip, persisted
  per-game Override, and a selected fixture launched without administrator rights.
- Actual Windows round trip restored original registry values and power plan and
  cleared recovery state. The exercised round-trip profile was Quality.
- PresentMon recorded real frames through the protected staged helper/stdout path.
- NVIDIA Control Panel and Windows graphics destinations were opened from the app.
- Installed NSIS release opened through the existing Start Menu entry. Registry
  registration reports 4.0.0, with one Start Menu shortcut and one uninstall entry.
- Installed image equals the release image except Tauri's documented bundle marker
  (`UNK` in the portable image, `NSS` in the NSIS payload).
- Manual installed-release walkthrough checked rendering, window maximization,
  game selection, Override persistence and restoration to the original selection.

Fixture UI tests do not replace native tests. Short tests do not establish
24-hour gaming stability, every Performance/Ultimate/anti-cheat combination,
thermal behavior or an FPS improvement. Hardware testing was limited to one
Acer/NitroSense Windows machine; desktop-PC policy is implemented but was not
tested on a separate physical desktop during this run.

## Security review

The requested knowledge-security-audit standard workflow was completed as a
source-only review with independent hunters, candidate verification, final record
verification and coverage critics. Nine coverage units and four rejected bounded
hypotheses were recorded. No confirmed vulnerability was established; that is not
a claim that the software has no vulnerabilities.

The review retained three blocked coverage units for unavailable runtime token,
ACL, race, provenance and legacy-handoff facts. The complete hostile-code sandbox
was unavailable, so no adversarial target execution or live security probing was
performed. Normal functional tests are separate engineering evidence.

Both upstream exported JSON validators passed. Their CLI no-follow file-reader
protection could not be exercised on this Windows host. Full reports and local
machine evidence remain outside the public source tree.

Setup and the application are not Authenticode-signed. Verify release downloads
against SHA256SUMS.txt. The installer no longer downloads or runs a WebView2
bootstrapper; install a missing runtime directly from Microsoft.
