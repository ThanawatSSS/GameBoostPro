$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root 'dist'

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "ASSERT FAILED: $message" }
}

& (Join-Path $root 'build.ps1')

$appPath = (Resolve-Path (Join-Path $dist 'GameBoostPro.exe')).Path
$setupPath = (Resolve-Path (Join-Path $dist 'GameBoostPro-Setup.exe')).Path
$zipPath = (Resolve-Path (Join-Path $dist 'GameBoostPro-Portable-v3.2.0.zip')).Path
$assembly = [Reflection.Assembly]::LoadFile($appPath)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'

Assert-True ((Get-Item $appPath).VersionInfo.FileVersion -eq '3.2.0.0') 'app version'
Assert-True ((Get-Item $setupPath).VersionInfo.FileVersion -eq '3.2.0.0') 'setup version'

$platformDetector = $assembly.GetType('GameBoostPro.PlatformDetector')
$evaluate = $platformDetector.GetMethod('Evaluate', $flags)

$acer = $evaluate.Invoke($null, [object[]]@('Acer', 'Nitro ANV15-51', $true, $true))
$desktop = $evaluate.Invoke($null, [object[]]@('ASUS', 'Desktop', $false, $false))
$lenovo = $evaluate.Invoke($null, [object[]]@('Lenovo', 'Legion', $true, $false))

Assert-True $acer.IsSupported 'Acer + NitroSense support'
Assert-True $desktop.IsSupported 'desktop support'
Assert-True (-not $lenovo.IsSupported) 'unvalidated laptop guard'

$safety = $assembly.GetType('GameBoostPro.SafetyPolicy')
$isProtected = $safety.GetMethod('IsProtectedProcess', $flags)
foreach ($name in 'Discord','ts3client_win64','NitroSense','AcerHardwareService') {
    Assert-True ([bool]$isProtected.Invoke($null, [object[]]@($name))) "protected process $name"
}
Assert-True (-not [bool]$isProtected.Invoke($null, [object[]]@('OneDrive'))) 'non-protected control process'

$setupAssembly = [Reflection.Assembly]::LoadFile($setupPath)
$resources = $setupAssembly.GetManifestResourceNames()
Assert-True ($resources -contains 'GameBoostPro.Payload.exe') 'embedded app payload'
Assert-True ($resources -contains 'GameBoostPro.Readme.txt') 'embedded readme'
Assert-True ($resources -contains 'GameBoostPro.PresentMon.exe') 'embedded PresentMon payload'
Assert-True ($resources -contains 'GameBoostPro.PresentMonLicense.txt') 'embedded PresentMon license'

$stream = $setupAssembly.GetManifestResourceStream('GameBoostPro.Payload.exe')
$payloadSha = [Security.Cryptography.SHA256]::Create()
try {
    $payloadHash = ([BitConverter]::ToString($payloadSha.ComputeHash($stream))).Replace('-', '')
}
finally {
    $payloadSha.Dispose()
    $stream.Dispose()
}
Assert-True ($payloadHash -eq (Get-FileHash $appPath -Algorithm SHA256).Hash) 'setup payload hash'

$presentMonPath = (Resolve-Path (Join-Path $dist 'tools\PresentMon.exe')).Path
$presentMonExpectedHash = '9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191'
Assert-True ((Get-FileHash $presentMonPath -Algorithm SHA256).Hash -eq $presentMonExpectedHash) `
    'PresentMon pinned hash'
Assert-True ((Get-AuthenticodeSignature $presentMonPath).Status -eq 'Valid') 'PresentMon signature'
$presentMonStream = $setupAssembly.GetManifestResourceStream('GameBoostPro.PresentMon.exe')
$presentMonSha = [Security.Cryptography.SHA256]::Create()
try {
    $embeddedPresentMonHash = ([BitConverter]::ToString(
        $presentMonSha.ComputeHash($presentMonStream))).Replace('-', '')
}
finally {
    $presentMonSha.Dispose()
    $presentMonStream.Dispose()
}
Assert-True ($embeddedPresentMonHash -eq $presentMonExpectedHash) 'setup PresentMon hash'

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
$entries = @($zip.Entries | ForEach-Object FullName)
$zip.Dispose()
foreach ($required in 'GameBoostPro.exe','README.txt','LICENSE','tools/PresentMon.exe','tools/PresentMon-LICENSE.txt') {
    Assert-True ($entries -contains $required) "portable ZIP entry $required"
}

$appManifest = Get-Content (Join-Path $root 'src\GameBoostPro.manifest') -Raw
$setupManifest = Get-Content (Join-Path $root 'installer\GameBoostProInstaller.manifest') -Raw
$setupSource = Get-Content (Join-Path $root 'installer\GameBoostProInstaller.cs') -Raw
Assert-True ($appManifest -match 'requireAdministrator') 'app requests UAC'
Assert-True ($setupManifest -match 'requireAdministrator') 'setup requests UAC'
Assert-True ($setupSource -match 'HasPendingRecoveryState') `
    'uninstaller blocks removal while recovery state is pending'

$source = Get-Content (Join-Path $root 'src\GameBoostPro.cs') -Raw
Assert-True ($source -notmatch 'Stop-Service|Set-Service|ServiceController|ProcessPriorityClass\.(High|RealTime)') `
    'forbidden tuning operations'
Assert-True ($source -match '/duplicatescheme " \+ UltimateGuid') 'Ultimate Performance auto-create'
Assert-True ($source -match 'Game Boost Pro Ultimate\|Ultimate Performance') 'existing Ultimate plan reuse'
Assert-True ($source -notmatch 'HighPerformanceGuid') 'no High Performance fallback'
Assert-True ($source -match 'PowerPlanPolicy\.ShouldKeepCurrent') 'Smart Power policy'
Assert-True ($source -match 'SMART \(แนะนำ\).*ULTIMATE.*KEEP CURRENT') 'Power Plan selector'
Assert-True ($source -match 'ADVANCED  BEST') 'visible Advanced Mode'
Assert-True ($source -match 'BoostTargetResolver\.ResolveGamePath\(candidate, config\.GamePath\)') `
    'detected game path never falls back to another configured title'
Assert-True ($source -match 'GameProcessStartTimeUtcTicks') 'process start-time identity guard'
Assert-True ($source -match 'IsStoredProcessMatch') 'process restore identity validation'
Assert-True ($source -match 'ProcessTuningStatus = "NotRetained"') 'process retention reporting'
Assert-True ($source -match 'NeedsProcessTuning\(latest, snapshot\.Game\.Process\)') `
    'blocked process tuning is not retried continuously'
Assert-True ($source -match 'class GraphicsAdvisorForm') 'Graphics Advisor GUI'
Assert-True ($source -match 'SupportsMultiFrameGeneration = rtx && series >= 50') `
    'RTX 50-only Multi Frame Generation policy'
Assert-True ($source -match 'class FrameBenchmarkForm') 'Frame Lab GUI'
Assert-True ($source -match '--delay 3 --timed 15') 'bounded user-started frame capture'
Assert-True ($source -match 'ExpectedSha256 = "9BEC3083') 'runtime PresentMon hash guard'
Assert-True ($source -notmatch 'exclude_dropped') 'Frame Lab retains dropped-frame evidence'
Assert-True (($source | Select-String 'ValidateTargetProcess\(' -AllMatches).Matches.Count -ge 3) `
    'Frame Lab validates game identity before and after capture'
Assert-True ($source -match 'processStartTimeUtcTicks\.ToString\(CultureInfo\.InvariantCulture\)') `
    'Frame Lab history is isolated per game process session'
Assert-True ($source -match 'GetBoostSessionToken\(\), boostSession') `
    'Frame Lab rejects a Boost session changed during capture'
Assert-True ($source -notmatch 'catch\s*\{\s*result = process;\s*break;\s*\}') `
    'manual process matching never accepts an unverified executable path'
Assert-True ($source -match 'Task\.Factory\.StartNew') 'monitoring runs outside the UI thread'
Assert-True ($source -match 'DetectPlatformAsync') 'platform detection runs after first paint'
Assert-True ($source -match 'private static volatile bool catalogLoaded') 'non-blocking catalog readiness'
Assert-True ($source -match 'TryGetCachedRunningGame') 'stable running-game fast path'
Assert-True ($source -match 'detectedGame\.Process\.Dispose\(\)') 'long-session process cleanup'
Assert-True ($source -notmatch 'DrawCentered\(g, eyebrow, new Font') 'BoostDial reuses paint fonts'
Assert-True ($source -notmatch 'using \(Pen pen = new Pen\(color') 'BoostDial reuses ring pens'
Assert-True ($source -notmatch 'using \(Font label = new Font') 'MetricBar reuses paint fonts'
Assert-True ($source -match 'ActiveMonitorIntervalMs = 3000') 'adaptive stable monitor interval'
Assert-True ($source -match 'ThreadPriority\.BelowNormal') 'low-priority monitor worker'
Assert-True ($source -match 'Visible && WindowState != FormWindowState\.Minimized') `
    'telemetry pauses while hidden'
Assert-True ($source -match 'LoadStateForRestore') 'fresh recovery state read'
Assert-True ($source -match 'Registry\.LocalMachine\.CreateSubKey\(StateRegistrySubKey\)') `
    'production recovery state uses an administrator-protected store'
Assert-True ($source -match 'RecoveryStatePolicy\.SanitizeMigratedState') `
    'legacy recovery migration is allowlisted and sanitized'
Assert-True ($source -match 'String\.Equals\(state\.OwnerSid, ownerSid') `
    'protected recovery state is bound to its Windows account SID'
Assert-True ($source -match 'Storage\.HasRecoveryWarning') `
    'unsafe legacy recovery blocks a new Boost session'
Assert-True ($source -match 'GameBoostPro\.TestAppDirectory') 'isolated performance-test storage'
Assert-True ($source -match 'ApplyGamePriority\(latest, snapshot\.Game\.Process\.Id\)') `
    'detected process ID is tuned directly'

& (Join-Path $root 'tests\Test-Performance.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Performance tests failed' }

& (Join-Path $root 'tests\Test-GuiVisual.ps1')
if ($LASTEXITCODE -ne 0) { throw 'GUI visual smoke tests failed' }

Write-Host 'All release tests passed.' -ForegroundColor Green
