$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root 'dist'

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "ASSERT FAILED: $message" }
}

& (Join-Path $root 'build.ps1')

$appPath = (Resolve-Path (Join-Path $dist 'GameBoostPro.exe')).Path
$setupPath = (Resolve-Path (Join-Path $dist 'GameBoostPro-Setup.exe')).Path
$zipPath = (Resolve-Path (Join-Path $dist 'GameBoostPro-Portable-v3.1.1.zip')).Path
$assembly = [Reflection.Assembly]::LoadFile($appPath)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'

Assert-True ((Get-Item $appPath).VersionInfo.FileVersion -eq '3.1.1.0') 'app version'
Assert-True ((Get-Item $setupPath).VersionInfo.FileVersion -eq '3.1.1.0') 'setup version'

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

$stream = $setupAssembly.GetManifestResourceStream('GameBoostPro.Payload.exe')
$sha = [Security.Cryptography.SHA256]::Create()
$payloadHash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '')
$stream.Dispose()
Assert-True ($payloadHash -eq (Get-FileHash $appPath -Algorithm SHA256).Hash) 'setup payload hash'

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
$entries = @($zip.Entries | ForEach-Object FullName)
$zip.Dispose()
foreach ($required in 'GameBoostPro.exe','README.txt','LICENSE') {
    Assert-True ($entries -contains $required) "portable ZIP entry $required"
}

$appManifest = Get-Content (Join-Path $root 'src\GameBoostPro.manifest') -Raw
$setupManifest = Get-Content (Join-Path $root 'installer\GameBoostProInstaller.manifest') -Raw
Assert-True ($appManifest -match 'requireAdministrator') 'app requests UAC'
Assert-True ($setupManifest -match 'requireAdministrator') 'setup requests UAC'

$source = Get-Content (Join-Path $root 'src\GameBoostPro.cs') -Raw
Assert-True ($source -notmatch 'Stop-Service|Set-Service|ServiceController|ProcessPriorityClass\.(High|RealTime)') `
    'forbidden tuning operations'
Assert-True ($source -match '/duplicatescheme " \+ UltimateGuid') 'Ultimate Performance auto-create'
Assert-True ($source -match 'Game Boost Pro Ultimate\|Ultimate Performance') 'existing Ultimate plan reuse'
Assert-True ($source -notmatch 'HighPerformanceGuid') 'no High Performance fallback'
Assert-True ($source -match 'ADVANCED  BEST') 'visible Advanced Mode'
Assert-True ($source -match 'Task\.Factory\.StartNew') 'monitoring runs outside the UI thread'
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
Assert-True ($source -match 'GameBoostPro\.TestAppDirectory') 'isolated performance-test storage'
Assert-True ($source -match 'ApplyGamePriority\(latest, snapshot\.Game\.Process\.Id\)') `
    'detected process ID is tuned directly'

& (Join-Path $root 'tests\Test-Performance.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Performance tests failed' }

Write-Host 'All release tests passed.' -ForegroundColor Green
