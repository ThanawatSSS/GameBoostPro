$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = [IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar)
$dist = [IO.Path]::GetFullPath((Join-Path $root 'dist'))
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not $dist.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean a build directory outside the repository'
}
if (Test-Path $dist) { Remove-Item -LiteralPath $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist -Force | Out-Null

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
    "$root\src\GameBoostPro.cs"

if ($LASTEXITCODE -ne 0) { throw 'GameBoostPro build failed' }

$payloadResource = '/resource:{0},GameBoostPro.Payload.exe' -f (Join-Path $dist 'GameBoostPro.exe')
$readmeResource = '/resource:{0},GameBoostPro.Readme.txt' -f (Join-Path $root 'README.txt')
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
    "$root\installer\GameBoostProInstaller.cs"

if ($LASTEXITCODE -ne 0) { throw 'Setup build failed' }

Copy-Item "$root\README.txt" "$dist\README.txt" -Force
Copy-Item "$root\LICENSE" "$dist\LICENSE" -Force

$portable = Join-Path $dist 'portable'
New-Item -ItemType Directory -Path $portable -Force | Out-Null
Copy-Item "$dist\GameBoostPro.exe" $portable -Force
Copy-Item "$root\README.txt" $portable -Force
Copy-Item "$root\LICENSE" $portable -Force

$zip = Join-Path $dist 'GameBoostPro-Portable-v3.1.0.zip'
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path "$portable\*" -DestinationPath $zip -CompressionLevel Optimal

Get-ChildItem $dist -File | Where-Object Name -ne 'SHA256SUMS.txt' | Get-FileHash -Algorithm SHA256 |
    ForEach-Object { '{0}  {1}' -f $_.Hash, (Split-Path $_.Path -Leaf) } |
    Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII

Write-Host "Build complete: $dist"
