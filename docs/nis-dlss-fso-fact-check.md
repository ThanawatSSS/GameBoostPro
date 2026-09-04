# NIS, DLSS, FSE, and FSO Fact Check

Research date: 2026-09-04

Scope: Fact-check of claims proposed for GameBoostPro, using primary documentation and official source only. "Driver NIS" below means the NVIDIA driver feature exposed by NVIDIA Control Panel/NVIDIA App. "NIS SDK" means the open-source shader integration intended to be compiled into a game or rendering application. They are not interchangeable.

## Executive Verdict

- Driver NIS is documented for proper/fullscreen operation, explicitly not borderless windowed or "fake fullscreen." NVIDIA offers lowering the desktop resolution as a fallback, but that is disruptive and should remain a manual user action.
- "Fullscreen with Windows Fullscreen Optimizations (FSO) guarantees NIS" is **not established**. Microsoft says FSO makes a game believe it has FSE while Windows actually uses an optimized borderless presentation. NVIDIA says native driver NIS excludes borderless/fake fullscreen. The two vendors do not document this combination as guaranteed; verify engagement with NVIDIA's green/blue NIS indicator.
- Windows 11 "Optimizations for windowed games" is a separate feature from FSO. It upgrades eligible DX10/DX11 windowed and borderless games from blt model to flip model. DX12 already uses modern presentation and, according to current Microsoft documentation, does not support true FSE.
- RTX 4050 Laptop GPU belongs to the RTX 40 Series laptop family and supports DLSS Super Resolution and single-frame DLSS Frame Generation where the game supports them. It does not gain DLSS merely because the GPU supports it; the game/engine must integrate or be compatible with the feature.
- NIS plus DLSS is not inherently an error. At native game output resolution, driver NIS may add sharpening only. If DLSS first reconstructs to a game output resolution below the display native resolution and driver NIS then scales that output to native, two scaling stages occur. That pipeline conclusion is an **inference** from the documented behavior of each technology, not an NVIDIA-published compatibility verdict.
- A third-party app can query/set NVIDIA driver profile settings through the documented NVAPI DRS interface. It cannot reliably confirm that NIS is actively scaling a running game, read a game's selected display mode or DLSS mode across all engines, or toggle in-game DLSS through a universal public API.

## Local Machine Evidence

This machine was inspected read-only on 2026-09-04 while GameBoostPro and VALORANT were running:

- Acer Nitro ANV15-51, Intel Core i5-13420H (4 performance cores, 4 efficient cores, 12 threads), 31.7 GB RAM, GeForce RTX 4050 Laptop GPU plus Intel UHD Graphics. [Intel CPU specification](https://www.intel.com/content/www/us/en/products/sku/232173/intel-core-i513420h-processor-12m-cache-up-to-4-60-ghz/specifications.html)
- NVIDIA driver `616.64`; NVIDIA App product version `11.0.9.251`.
- `nvidia-smi` reported `display_active=Disabled` for the RTX 4050 at inspection time. This does not by itself map every connector, but it is strong evidence that the active internal-display scan-out was not initialized on the NVIDIA GPU. Driver NIS is therefore **unlikely to be eligible on the current internal-display route**, even though the RTX GPU renders the game. NVIDIA explicitly requires NVIDIA-driven scan-out for NIS.
- Acer's current public MUX support article does not list ANV15-51 among the supported models. Absence from that list is not proof that every regional BIOS lacks the feature, so GameBoostPro should test the active route rather than infer it from the laptop model. [Acer MUX documentation](https://community.acer.com/en/kb/articles/15133-how-to-configure-a-nitro-notebook-so-the-rtx-series-gpu-is-used-for-%20applications%20enti%C3%A8rement%20graphiques)
- The machine was on GameBoostPro's Ultimate Performance plan during inspection; its saved pre-boost plan was the user's custom `Nezha` plan.

Practical consequence: on this laptop's current internal display, in-game DLSS Super Resolution is the primary upscaler. Driver NIS should be shown as `route not eligible or unverified`, not silently enabled. An external monitor may differ if its connector is physically routed to NVIDIA, so eligibility must be evaluated per active display.

## Claim-by-Claim Verdict

| Claim | Verdict | Evidence and correction |
|---|---|---|
| Driver NIS supports all major graphics APIs. | **Supported, with conditions.** | NVIDIA lists DX9/10/11/12, Vulkan, and OpenGL, but separately requires NVIDIA-driven scan-out and proper fullscreen for native engagement. [NVIDIA NIS support article](https://nvidia.custhelp.com/app/answers/detail/a_id/5280) |
| Driver NIS works only in fullscreen, not borderless. | **Supported for native engagement.** | NVIDIA explicitly says proper/exclusive fullscreen, not borderless windowed or fake fullscreen. A lower desktop resolution is the documented fallback when a game lacks proper fullscreen. [NVIDIA NIS support article](https://nvidia.custhelp.com/app/answers/detail/a_id/5280) |
| Fullscreen selected in-game while FSO remains enabled guarantees driver NIS. | **Unsupported / misleading.** | Microsoft describes FSO as presenting an apparent FSE game as optimized borderless; NVIDIA excludes borderless/fake fullscreen. Neither source guarantees NIS under FSO. Treat the result as system/game/driver-dependent and verify the NIS indicator. [Microsoft FSO explanation](https://devblogs.microsoft.com/directx/demystifying-full-screen-optimizations/), [NVIDIA NIS requirements](https://nvidia.custhelp.com/app/answers/detail/a_id/5280) |
| Disabling FSO always produces true FSE and therefore fixes NIS. | **False as a universal rule.** | It may help a DX9/DX11 game that implements FSE, but Microsoft currently states D3D12 does not support FSE; its fullscreen transition enables FSO-style behavior instead. [Microsoft D3D12 swap-chain documentation](https://learn.microsoft.com/en-us/windows/win32/direct3d12/swap-chains) |
| FSO gives faster Alt-Tab and FSE-like average performance. | **Generally supported, not guaranteed per game.** | Microsoft designed FSO for fast switching, overlays, and performance comparable to FSE, and reports average telemetry as good or better. Microsoft still provides per-game disable instructions for regressions. [Microsoft FSO explanation](https://devblogs.microsoft.com/directx/demystifying-full-screen-optimizations/) |
| Windows 11 Optimizations for windowed games is the same switch as FSO. | **False.** | The Windows 11 setting targets compatible DX10/DX11 windowed and borderless games using legacy blt presentation and moves them to flip model. FSO addresses fullscreen requests. [Microsoft Windows 11 setting](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11), [Microsoft DirectX announcement](https://devblogs.microsoft.com/directx/updates-in-graphics-and-gaming/) |
| Borderless/flip model is necessarily slower than FSE. | **False as a general rule.** | Microsoft documents DirectFlip/Independent Flip paths that can bypass composition with efficiency comparable to FSE, and recommends reconsidering classic FSE for modern applications. Actual mode depends on swap-chain, window, overlays, and hardware. [Microsoft flip-model guidance](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model) |
| The NIS SDK can make driver NIS work in borderless games from GameBoostPro. | **False.** | The SDK is a compute-shader algorithm that the rendering application integrates using its own input/output textures, post-processing placement, and DX11/DX12/Vulkan resources. It is not a supported external switch for arbitrary games. [Official NIS SDK documentation/source](https://github.com/NVIDIAGameWorks/NVIDIAImageScaling) |
| DLSS is temporal/AI while NIS is spatial. | **Supported.** | NVIDIA says DLSS Super Resolution uses lower-resolution samples, motion data, and prior-frame feedback; NIS uses a spatial, non-AI algorithm based on the current frame. [NVIDIA DLSS documentation](https://developer.nvidia.com/rtx/dlss), [NVIDIA NIS SDK FAQ](https://developer.nvidia.com/rtx/image-scaling) |
| NVIDIA prefers DLSS over NIS on supported RTX games. | **Supported as NVIDIA's documented product behavior/recommendation.** | NVIDIA states GeForce Experience selects DLSS instead of NIS for supported games on RTX hardware for image quality and performance. This does not prove every game/profile is detected correctly. [NVIDIA scaling overview](https://www.nvidia.com/en-us/geforce/news/nvidia-image-scaler-dlss-rtx-november-2021-updates/) |
| Enabling DLSS and driver NIS always causes double scaling. | **False.** | NVIDIA says native-output NIS can sharpen without scaling (blue indicator). Double scaling occurs only if the game's final output remains below display native and NIS then scales it; that conditional pipeline is an **inference** combining the DLSS output and NIS input/output descriptions. [NVIDIA NIS indicator behavior](https://nvidia.custhelp.com/app/answers/detail/a_id/5280), [NVIDIA DLSS Super Resolution behavior](https://developer.nvidia.com/rtx/dlss) |
| RTX 4050 Laptop GPU supports DLSS and Frame Generation. | **Supported, game-dependent.** | NVIDIA describes RTX 40 Series Laptop GPUs as having fourth-generation Tensor Cores and an Optical Flow Accelerator; its current compatibility table lists ordinary DLSS Frame Generation for RTX 40 Series. The title must support/enable the feature. Calling this merely "DLSS 3 support" is now imprecise because NVIDIA's suite and model versions continue to evolve; GameBoostPro should name each capability instead. [RTX 40 Series laptops](https://www.nvidia.com/en-us/geforce/laptops/40-series/), [DLSS hardware compatibility](https://www.nvidia.com/en-us/geforce/technologies/dlss/) |
| RTX 4050 Laptop supports DLSS Multi Frame Generation. | **False.** | NVIDIA limits Multi Frame Generation to RTX 50 Series; RTX 40 Series supports the original one-generated-frame Frame Generation path. [DLSS hardware compatibility](https://www.nvidia.com/en-us/geforce/technologies/dlss/) |
| Driver NIS should work on every RTX 4050 laptop internal display. | **False.** | NVIDIA requires the display scan-out to be driven by the NVIDIA GPU. On MSHybrid/Optimus notebooks, NVIDIA instructs users to switch to discrete-GPU mode. A 4050 rendering a game does not prove it drives the panel. [NVIDIA NIS limitations](https://nvidia.custhelp.com/app/answers/detail/a_id/5280) |
| A third-party utility can reliably infer FSE from window size/style. | **False.** | A borderless window can cover the display, FSO can make an apparent FSE request run as optimized borderless, and flip-model paths may independently flip. Window geometry is only a heuristic. The documented `IDXGISwapChain::GetFullscreenState` query operates on a swap-chain interface owned by the rendering application, not as a universal cross-process query. [Microsoft GetFullscreenState API](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-getfullscreenstate), [Microsoft FSO explanation](https://devblogs.microsoft.com/directx/demystifying-full-screen-optimizations/) |
| A third-party utility can detect presentation mode without injecting into the game. | **Supported for diagnostics, not as a simple setting query.** | PresentMon consumes Windows ETW events and reports per-frame `PresentMode` across graphics APIs. It requires a capture session and has permission/measurement limitations; it does not reveal the game's menu selection or prove NIS engagement. [Official PresentMon source](https://github.com/GameTechDev/PresentMon), [PresentMon metric definitions](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md) |
| A third-party utility can query/toggle NVIDIA profile settings. | **Supported through NVAPI DRS.** | NVIDIA documents loading, querying, modifying, and saving driver profile settings. Its official header exposes `NV_QUALITY_UPSCALING_ID` with OFF/ON values. This proves configuration control, not active NIS engagement. [NVAPI DRS reference](https://docs.nvidia.com/nvapi/group__drsapi.html), [official NVAPI setting definitions](https://github.com/NVIDIA/nvapi/blob/main/NvApiDriverSettings.h) |
| A third-party utility can universally detect or toggle in-game DLSS. | **False.** | DLSS/Streamline APIs are integration APIs used by the rendering application. NVIDIA driver overrides exist for compatible titles, but NVIDIA documents that relevant in-game DLSS features must already be enabled. A DLL-presence check or game list is not proof that DLSS is currently active. [NVIDIA DLSS developer integration](https://developer.nvidia.com/rtx/dlss), [NVIDIA App override prerequisites](https://www.nvidia.com/en-us/geforce/news/nvidia-app-update-dlss-overrides-and-more.html) |
| Lossless Scaling can provide an NIS-family scaler in borderless/windowed mode. | **Supported as a separate capture/post-process product.** | Its publisher states that it supports NIS scaling and requires windowed or borderless fullscreen in the normal one-display case. This is not driver NIS and should not be described as enabling NVIDIA's display-scaling path. Its publisher also warns that insufficient free GPU resources can reduce the game's original frame rate. The exact capture API and latency cost can vary by product version and selected mode, so a fixed latency claim is unsupported. [Lossless Scaling publisher page](https://store.steampowered.com/app/993090/Lossless_Scaling/) |
| Setting `CPMINCORES=100` universally improves games. | **False.** | Microsoft documents only that 100% disables parking for that processor efficiency class. It does not promise higher FPS or better frame times, and heterogeneous CPUs have separate `CPMINCORES` and `CPMINCORES1` controls. Microsoft advises obtaining silicon-vendor guidance before changing processor power policy. [Microsoft CPMinCores](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/options-for-core-parking-cpmincores), [Microsoft processor power management](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/configure-processor-power-management-options) |
| Ultimate Performance necessarily beats a custom plan. | **False.** | A power-plan name is not a benchmark result. On this machine, the two plans contain different boost and parking policies, with neither plan strictly dominating the other. Laptop CPU and GPU also share thermal and power headroom through platform controls such as Dynamic Boost. [NVIDIA Dynamic Boost](https://www.nvidia.com/en-us/geforce/laptops/40-series/) |

## Driver NIS Versus NIS SDK

| Property | Driver NIS | NIS SDK |
|---|---|---|
| Owner | NVIDIA display driver/profile | Game or rendering application |
| Integration | Driver setting plus display/game resolution | Compute shader in the application's render pipeline |
| Supported hardware | GeForce, subject to NVIDIA-driven display scan-out | Modern NVIDIA, AMD, Intel, and consoles |
| Display mode constraint | Proper fullscreen for native engagement; desktop-resolution fallback | Determined by the host application, not inherently tied to FSE |
| Suitable for GameBoostPro | Yes, as an opt-in NVAPI profile assistant | No, not for arbitrary games without capture/injection/wrapping |

The NIS SDK documentation requires access to the frame color texture, correctly sized output texture, configuration constants, and placement after tone mapping. Injecting or capturing arbitrary protected games to simulate this is outside a safe optimizer's scope and can add latency or anti-cheat compatibility risk.

## What GameBoostPro Can Reliably Do

1. **NVIDIA capability check:** Detect NVIDIA hardware/driver through NVAPI and explain that NIS additionally requires NVIDIA-driven display scan-out. Report MSHybrid/Optimus as "eligibility uncertain" unless NVIDIA display topology confirms the active output.
2. **NIS profile control, opt-in only:** Use the documented NVAPI DRS APIs and `NV_QUALITY_UPSCALING_ID` to read and change a selected executable's profile. Capture the original value, save atomically, restore exactly, and report NVAPI errors. Do not edit undocumented NVIDIA registry/database files.
3. **Guided NIS setup:** Ask the user to select proper fullscreen and a generated lower resolution inside the game. Explain green indicator = scaling plus sharpening, blue = sharpening only. If native engagement fails, offer a button to open NVIDIA Control Panel/NVIDIA App instructions, not an automatic desktop-resolution change.
4. **DLSS advisor:** Identify RTX capability and use NVIDIA's current supported-games list only as advisory metadata. Prefer "DLSS may be available; verify in game" over "DLSS detected." Recommend DLSS before NIS when the title supports it.
5. **Conflict warning:** If GameBoostPro has enabled an NIS profile for a title known to support DLSS, warn that NIS is usually unnecessary. Explain that output below native may add another scaling stage; do not claim DLSS is currently enabled.
6. **Presentation diagnostics, optional:** Offer a short, user-started PresentMon/ETW A/B benchmark to report observed presentation mode and frame-time data. Keep it off during normal Boost sessions and label it diagnostic, not authoritative game-setting detection.
7. **Windows settings shortcut:** Open the documented `ms-settings:display-advancedgraphics` or `ms-settings:display-advancedgraphics-default` page and let the user control Windows graphics settings. [Microsoft Settings URI reference](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)
8. **Per-game recommendations:** Leave FSO and Windows 11 windowed optimizations at OS defaults. Suggest a reversible per-game A/B test only after measured regression, because Microsoft documents average FSO performance as equal or better.

## Power Plan and Core Parking Findings

Read-only `powercfg /qh` inspection produced these AC values:

| Setting | Nezha | GameBoostPro Ultimate | Meaning |
|---|---:|---:|---|
| `CPMINCORES` | 100 | 4 | Minimum unparked percentage for processor efficiency class 0; 100 disables parking for this class. |
| `CPMINCORES1` | 0 | 0 | Minimum unparked percentage for processor efficiency class 1. |
| `PROCTHROTTLEMIN` | 100 | 100 | Minimum performance state for class 0. |
| `PROCTHROTTLEMIN1` | 100 | 100 | Minimum performance state for class 1. |
| `PERFEPP` / `PERFEPP1` | 0 / 0 | 0 / 0 | Both plans strongly prefer performance on AC. |
| `PERFBOOSTMODE` | 3 | 2 | Microsoft maps 3 to Enabled-equivalent and 2 to Aggressive for applicable interfaces. [Microsoft PERFBOOSTMODE](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/options-for-perf-state-engine-perfboostmode) |

This invalidates two shortcuts:

- Switching to Ultimate does not mean all cores are unparked. On this machine, Nezha is more aggressive for class-0 parking, while Ultimate is more aggressive for boost selection.
- Setting both parking classes to 100 is not a free upgrade. The i5-13420H can turbo to 115 W according to Intel, and the laptop dynamically allocates power and thermal headroom between CPU and GPU. More always-awake CPU capacity can consume headroom that a GPU-bound game would use.

Recommended implementation: preserve the user's current custom/OEM plan by default on laptops. Offer `Ultimate` and `No core parking (A/B test)` as explicit, AC-only experiments. Snapshot every changed value, restore it exactly, and keep a result only if a repeatable benchmark improves 1% low or frame-time variance without unacceptable temperature or throttling.

## Current Code and Runtime Findings

The current source already implements Windows Game Mode, Game DVR capture disable, Windows high-performance GPU preference, AboveNormal process priority, process power-throttling controls, dynamic priority boost, and Ultimate plan switching. It does **not** currently implement NIS, DLSS, FSO, Windows 11 windowed optimizations, NVAPI DRS, PresentMon, Smooth Motion, or core-parking changes. See [GameBoostPro.cs](../src/GameBoostPro.cs).

Two runtime observations should be fixed before adding more aggressive tweaks:

1. **Auto-detect can pair the right process with the wrong executable path.** The live state recorded `GameProcessId=15880` for `VALORANT-Win64-Shipping`, but `GamePath` was the configured `TEKKEN 8.exe`. The code intentionally falls back to `config.GamePath` when the detected process path is unavailable, which is common for protected games. This can write the high-performance GPU preference for the selected library game instead of the detected game.
2. **Applied is not the same as verified.** The live state recorded `ProcessTuningApplied=true`, while the observed VALORANT process priority was `Normal`, not `AboveNormal`. The game/anti-cheat may reject or later reset the change. The app should verify the effective value and report `not retained` instead of claiming success or repeatedly fighting the game.

The restore path also opens a saved PID without validating process name, executable identity, or start time. Store all three and verify them before restoring process-specific settings; otherwise a stale state can target a different process after PID reuse.

## Monitoring Cost

The current monitor is appropriately conservative:

- discovery polling is every 1.5 seconds;
- active-game polling backs off to every 3 seconds;
- CPU/GPU/RAM metrics are collected only while the GUI is visible and not minimized;
- snapshot work runs on a BelowNormal-priority worker and overlapping polls are blocked.

An 8-second local sample during VALORANT showed approximately `0.098%` of total CPU capacity, `107.8 MB` working set, and `59.0 MB` private memory for GameBoostPro. This is a single observation, not a universal benchmark, but it indicates negligible CPU contention in the measured state. Memory use is not ideal for such a small WinForms utility, but 108 MB on a 32 GB machine is not a gaming bottleneck.

PresentMon/ETW frame diagnostics are more invasive than these counters, although still designed for measurement. Run them only as a user-started 10-20 second A/B capture, stop the session cleanly, and do not leave per-frame logging active throughout a 2-24 hour gaming session. PresentMon can report actual presentation paths such as Legacy Flip, Independent Flip, and Composed Flip. [PresentMon metric definitions](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md)

## Practical Feature Priority

| Priority | Feature | Default behavior | Why |
|---|---|---|---|
| P0 | Fix process/path identity and verify applied state | Always | Correctness and restore safety come before new tuning. |
| P1 | Graphics Advisor: DLSS, Reflex, NIS eligibility, Smooth Motion | Read-only guidance | Gives the user the right choice without pretending universal in-game control exists. |
| P1 | Per-display NIS route checker | Read-only | Prevents offering NIS on Optimus/internal routes where NVIDIA is not driving scan-out. Implement with Windows DisplayConfig/DXGI topology plus NVAPI where available. [QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig), [NVAPI DRS](https://docs.nvidia.com/nvapi/group__drsapi.html) |
| P1 | Short Before/After benchmark | User-started only | Measures average FPS, 1% low, frame-time variance, and presentation mode instead of promising a generic percentage. |
| P2 | Per-game NIS profile through public NVAPI | Off by default | Useful fallback only when scan-out and fullscreen conditions are satisfied; restore original profile exactly. |
| P2 | Per-game NVIDIA `Prefer maximum performance` | Off by default on laptops | May reduce clock-ramp variance, but raises power/heat and can reduce sustained headroom. The public NVAPI header exposes the setting. [Official NVAPI settings](https://github.com/NVIDIA/nvapi/blob/main/NvApiDriverSettings.h) |
| P2 | Core-parking experiment | Off, AC-only, benchmark-gated | Situational; modify both efficiency-class values only with exact rollback. |
| P3 | Smooth Motion shortcut/advisor | Never for competitive preset | RTX 40 supports it in compatible DX11/DX12/Vulkan titles, including use with DLSS SR, but it must not be combined with native DLSS Frame Generation. [NVIDIA Smooth Motion](https://www.nvidia.com/en-sg/geforce/news/nvidia-app-global-dlss-overrides-rtx-40-series-smooth-motion/), [NVIDIA compatibility warning](https://docs.nvidia.com/datacenter/tesla/driver-installation-guide/gaming.html) |

Recommended decision order per game:

1. Use native NVIDIA Reflex when available for latency-sensitive games; it is integrated into the engine's CPU/GPU scheduling path. [NVIDIA Reflex](https://developer.nvidia.com/performance-rendering-tools/reflex)
2. Use in-game DLSS Super Resolution when GPU-bound and supported. Select Quality/Balanced/Performance based on measured FPS and image quality.
3. Use native DLSS Frame Generation only for supported non-competitive scenarios with adequate base frame rate. RTX 4050 supports normal Frame Generation, not RTX 50-only Multi Frame Generation.
4. Consider Smooth Motion only when native Frame Generation is absent/disabled. Do not stack it with native DLSS Frame Generation.
5. Consider driver NIS only when no better in-game scaler is available, NVIDIA drives the target display, the game produces a lower-than-native fullscreen output, and the green NIS indicator confirms scaling.
6. For CPU-bound esports at 1080p, lowering render resolution may not raise FPS because the GPU was not the bottleneck. Benchmark before keeping NIS or any scaler.

## What GameBoostPro Must Not Auto-Change

- Do not globally enable NIS as part of the ordinary Boost button. At native resolution it can still sharpen every affected application, and laptop scan-out/display-mode requirements vary.
- Do not automatically disable FSO. It is not a universal performance gain, it can worsen Alt-Tab/overlay behavior, and it cannot create true FSE for D3D12.
- Do not equate "Disable fullscreen optimizations" with Windows 11 "Optimizations for windowed games"; they target different presentation paths.
- Do not write undocumented AppCompat/GameConfigStore registry flags to control FSO/windowed optimizations. Microsoft documents user-facing controls but no stable public management API for these switches in the reviewed material.
- Do not automatically lower desktop resolution. It changes the whole desktop, can rearrange windows, and is only NVIDIA's manual fallback for non-engaging NIS.
- Do not auto-toggle DLSS, Frame Generation, Reflex, V-Sync, HDR, or sharpening in game configuration files. Schemas and trade-offs are title-specific, and no universal public runtime API exists.
- Do not inject the NIS SDK, hook swap chains, or add an overlay to protected/competitive games. The SDK is for engine integration; injection raises stability, latency, and anti-cheat risks.
- Do not switch a laptop MUX/Optimus mode automatically. It is vendor-specific, may require reboot, and changes battery, thermals, display routing, and external-display behavior.
- Do not claim that a process, loaded DLSS DLL, supported-game list, fullscreen-looking window, or enabled driver profile proves that DLSS/NIS/FSE is active.

## Recommended Product Decision

Ship this as an **Advanced, per-game Graphics Advisor**, not a default Boost tweak:

- `NIS configured`: authoritative NVAPI profile state.
- `NIS eligible`: hardware/topology/display-mode prerequisites appear satisfied, but engagement is unverified.
- `NIS active`: only user-confirmed from NVIDIA's green/blue indicator unless a future documented NVIDIA runtime query becomes available.
- `DLSS capable`: RTX hardware supports the technology.
- `DLSS supported by title`: advisory from NVIDIA's maintained list.
- `DLSS active`: unknown to GameBoostPro unless the game exposes a documented API.
- `Presentation mode observed`: optional ETW/PresentMon diagnostic result, not the same as the game's menu setting.

This model is factual, reversible, and useful without promising an automatic FPS gain that the available APIs cannot verify.

The safest high-value next release is therefore **Correctness + Graphics Advisor + short benchmark**, not a larger bundle of automatic registry/service changes. It will produce more trustworthy real-world gains by selecting the right tool for each game and proving whether a change improved this specific machine.
