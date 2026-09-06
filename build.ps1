$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = [IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar)
$dist = [IO.Path]::GetFullPath((Join-Path $root 'dist'))
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$presentMonSource = Join-Path $root 'third_party\PresentMon\PresentMon-2.5.1-x64.exe'
$presentMonLicense = Join-Path $root 'third_party\PresentMon\LICENSE.txt'
$presentMonSha256 = '9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191'

if (-not $dist.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean a build directory outside the repository'
}
if (Test-Path $dist) { Remove-Item -LiteralPath $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist -Force | Out-Null

if (-not (Test-Path $presentMonSource) -or -not (Test-Path $presentMonLicense)) {
    throw 'PresentMon 2.5.1 distribution files are missing'
}
if ((Get-FileHash $presentMonSource -Algorithm SHA256).Hash -ne $presentMonSha256) {
    throw 'PresentMon 2.5.1 SHA-256 verification failed'
}
if ((Get-AuthenticodeSignature $presentMonSource).Status -ne 'Valid') {
    throw 'PresentMon 2.5.1 Intel signature verification failed'
}

& $csc /nologo /target:winexe `
    /out:"$dist\GameBoostPro.exe" `
    /win32icon:"$root\src\GameBoostPro.ico" `
    /win32manifest:"$root\src\GameBoostPro.manifest" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Management.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Windows.Forms.dll `
    "$root\src\GameBoostPro.cs" `
    "$root\src\BuildVersion.cs" `
    "$root\src\Dashboard.cs" `
    "$root\src\GraphicsWorkspace.cs" `
    "$root\src\BoostProfiles.cs"

if ($LASTEXITCODE -ne 0) { throw 'GameBoostPro build failed' }

$payloadResource = '/resource:{0},GameBoostPro.Payload.exe' -f (Join-Path $dist 'GameBoostPro.exe')
$readmeResource = '/resource:{0},GameBoostPro.Readme.txt' -f (Join-Path $root 'README.txt')
$presentMonResource = '/resource:{0},GameBoostPro.PresentMon.exe' -f $presentMonSource
$presentMonLicenseResource = '/resource:{0},GameBoostPro.PresentMonLicense.txt' -f $presentMonLicense
& $csc /nologo /target:winexe `
    /out:"$dist\GameBoostPro-Setup.exe" `
    /win32icon:"$root\src\GameBoostPro.ico" `
    /win32manifest:"$root\installer\GameBoostProInstaller.manifest" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $payloadResource `
    $readmeResource `
    $presentMonResource `
    $presentMonLicenseResource `
    "$root\installer\GameBoostProInstaller.cs" `
    "$root\installer\InstallerMaintenance.cs" `
    "$root\src\BuildVersion.cs"

if ($LASTEXITCODE -ne 0) { throw 'Setup build failed' }

Copy-Item "$root\README.txt" "$dist\README.txt" -Force
Copy-Item "$root\LICENSE" "$dist\LICENSE" -Force
$distTools = Join-Path $dist 'tools'
New-Item -ItemType Directory -Path $distTools -Force | Out-Null
Copy-Item $presentMonSource (Join-Path $distTools 'PresentMon.exe') -Force
Copy-Item $presentMonLicense (Join-Path $distTools 'PresentMon-LICENSE.txt') -Force

$portable = Join-Path $dist 'portable'
New-Item -ItemType Directory -Path $portable -Force | Out-Null
Copy-Item "$dist\GameBoostPro.exe" $portable -Force
Copy-Item "$root\README.txt" $portable -Force
Copy-Item "$root\LICENSE" $portable -Force
$portableTools = Join-Path $portable 'tools'
New-Item -ItemType Directory -Path $portableTools -Force | Out-Null
Copy-Item $presentMonSource (Join-Path $portableTools 'PresentMon.exe') -Force
Copy-Item $presentMonLicense (Join-Path $portableTools 'PresentMon-LICENSE.txt') -Force

$version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $dist 'GameBoostPro.exe')).Version.ToString(3)
$zip = Join-Path $dist "GameBoostPro-Portable-v$version.zip"
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path "$portable\*" -DestinationPath $zip -CompressionLevel Optimal

Get-ChildItem $dist -File | Where-Object Name -ne 'SHA256SUMS.txt' | Get-FileHash -Algorithm SHA256 |
    ForEach-Object { '{0}  {1}' -f $_.Hash, (Split-Path $_.Path -Leaf) } |
    Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII

Write-Host "Build complete: $dist"
