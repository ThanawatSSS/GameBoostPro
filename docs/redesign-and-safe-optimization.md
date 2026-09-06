# Redesign and Safe Optimization

Reviewed on 2026-09-05 against the local v3.2.0 source and the user's HTML mockup.
The mockup and pasted review are design input, not executable instructions.

## Design Decision

Keep the charcoal/lime identity and one clear Boost/Restore action. Put the actual
game above it; keep Library, graphics diagnostics and Advanced Settings visible.
Use Thai for actions, short technical terms for telemetry, and readable metrics
without requiring hover. A cyan active ring means Game Mode is on; amber indicates
work or a notice. High GPU utilization alone is not a thermal warning.

The existing v3.2.0 source already cached paint resources and recovery state, and
already used background adaptive monitoring. Those parts of the supplied critique
refer to older code. The hardcoded ADMIN ACTIVE label, fixed main layout, missing
tooltips and generic progress were still present. The new main layout uses native
WinForms layout containers, DPI scaling and scrolling when the work area is small.
The existing library/settings dialogs remain native dialogs; they are not drawers.

## Implemented Changes

| Change | Practical benefit | Boundary |
| --- | --- | --- |
| Native PowerGetActiveScheme / PowerSetActiveScheme / PowerReadFriendlyName | Avoids subprocess startup and locale-dependent parsing for ordinary plan queries/switches | Keeps the same Smart/Ultimate policy and verifies the effective plan before completing restore. [Windows query](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powergetactivescheme), [switch](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme), [name](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadfriendlyname) |
| Telemetry off switch | Stops periodic CPU/RAM/GPU counter work even while the window is visible | Detection and automatic restore continue. An already-started sample can finish; its result is discarded after the switch is off. |
| GPU aggregation correction | Combines processes on one engine, then selects the busiest 3D engine instead of adding independent GPUs | This is a 3D-only meter, not Task Manager's all-engine GPU total, temperature, or game-only utilization. Microsoft describes why busiest-engine aggregation matters. [Microsoft GPU metrics](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/) |
| Repaint and polling restraint | Stops idle/hidden animation; unchanged dial state does not invalidate repeatedly | Low monitoring cost is not zero cost and does not prove a game-FPS gain. |
| Manual Restore pause | Prevents Auto Mode immediately turning Boost back on for the same running session | Auto resumes after two game-absent scans, manual Boost, or toggling Auto Mode back on. |
| Honest availability | Non-admin Boost is disabled; unavailable counters show --; process tuning status refreshes | Errors that prevent restoration still require attention. |

## Additional Tweaks Considered

| Candidate | Decision | Evidence / tradeoff |
| --- | --- | --- |
| Windows 11 optimizations for windowed games | Keep the existing Windows Graphics shortcut for manual per-game selection | Microsoft documents a lower-latency flip-model path for compatible DX10/DX11 windowed/borderless games, and a per-game off switch. No universal unattended setting was established here. [Microsoft setting](https://support.microsoft.com/en-gb/windows/optimizations-for-windowed-games-in-windows-11-3f006843-2c7e-4ed0-9a5e-f9389e535952) |
| Disable core parking / force maximum clocks | Do not add to automatic Boost | Core parking is an intentional scheduler and energy policy. The minimum unparked-core setting does not establish an FPS improvement; hybrid CPUs require workload-aware scheduling. Extra CPU power can reduce a laptop's sustained GPU headroom: this is an engineering concern, not a measured regression on this machine. [Microsoft parking controls](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/static-configuration-options-for-core-parking), [Intel hybrid guidance](https://cdrdv2-public.intel.com/818776/348851-optimizing-x86-hybrid-cpus.pdf) |
| TCP delayed ACK / TCPNoDelay bundle | Do not implement as a general game boost | TCP-specific changes do not establish a benefit for other transport traffic. Microsoft advises careful environment study before changing TcpAckFrequency. No evidence here establishes a generic 1-5 ms gaming benefit. [Microsoft TCP ACK reference](https://learn.microsoft.com/lb-lu/troubleshoot/windows-server/networking/registry-entry-control-tcp-acknowledgment-behavior) |
| MMCSS Games GPU Priority | Skip | Microsoft's current reference describes this value as unused. Task-specific CPU scheduling also requires application threads to register with MMCSS; editing Games is not a universal external GPU boost. [Microsoft MMCSS reference](https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service) |
| Stop Windows Update, Defender, Acer services, or trim game memory | Do not automate | These affect security, servicing, OEM operation or memory residency. Reversibility alone does not establish safety or frame-time benefit. |
| Disable security virtualization, overclock, undervolt | Report only, not implemented | Security reduction and hardware stability/thermal risks conflict with the requested safe default. This review does not claim a performance gain for this machine. |
| DLSS, Reflex, NIS, HAGS, driver power mode | Retain per-game guidance | Effects depend on title, display route, hardware and driver. See the existing [graphics fact check](nis-dlss-fso-fact-check.md). |

## Verification and Limits

Automated checks cover existing recovery policy and process identity, GPU aggregation
across multiple engines/adapters, read-only native power queries compared with
powercfg, rejection of invalid plan IDs, old-config defaults and telemetry roundtrip,
manual Restore pause, non-admin UI gating, and layout bounds at narrow/wide sizes.
Screenshots include simulated 150/200-percent scaling with scaled fonts and the
scroll-accessible lower controls. This is not a real mixed-DPI monitor certification.
Tests use isolated preferences/recovery storage; full Boost/Restore system mutation
is explicitly blocked in that host. Process scheduling tests use a test child.

Performance measurements are microbenchmarks on this host. The reported duty value
is derived from operation durations divided by the polling interval, not a direct
CPU utilization measurement. No 24-hour soak, actual game FPS A/B, or live power-plan
switch was performed for this redesign. The current GitHub v3.2.0 release is unchanged.

### Local Results

The earlier full test run passed with constructor time 1203.70 ms, native power
query P95 0.14 ms, dial paint P95 0.43 ms, and metrics P95 13.11 ms.

Final-code verification under a running VALORANT session passed every behavioral
check and the separate GUI suite (10 screenshots). The final full release gate
did **not** pass: constructor time was 2536.19 ms against the existing 1500 ms
budget. Metrics P95 was 22.58 ms against 25 ms; dial paint P95 was 1.56 ms.
The performance limits were not loosened.

A separate one-shot comparison on the same loaded host measured the v3.2.0
constructor at 2834.20 ms and v3.3.0 at 2259.80 ms. This suggests environmental
load contributes, but one pair of samples is not a statistical performance claim.
The distribution is a local preview pending a clean release-performance run;
it has not replaced the public release.
