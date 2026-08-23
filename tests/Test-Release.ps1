$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root 'dist'

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "ASSERT FAILED: $message" }
}

& (Join-Path $root 'build.ps1')

$appPath = (Resolve-Path (Join-Path $dist 'GameBoostPro.exe')).Path
$setupPath = (Resolve-Path (Join-Path $dist 'GameBoostPro-Setup.exe')).Path
$zipPath = (Resolve-Path (Join-Path $dist 'GameBoostPro-Portable-v3.0.0.zip')).Path
$assembly = [Reflection.Assembly]::LoadFile($appPath)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'

Assert-True ((Get-Item $appPath).VersionInfo.FileVersion -eq '3.0.0.0') 'app version'
Assert-True ((Get-Item $setupPath).VersionInfo.FileVersion -eq '3.0.0.0') 'setup version'

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
Assert-True ($source -notmatch 'Stop-Service|Set-Service|ProcessPriorityClass\.High|Realtime') 'forbidden tuning operations'

Write-Host 'All release tests passed.' -ForegroundColor Green
