$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$config = Get-Content -Raw -LiteralPath (Join-Path $root 'src-tauri\tauri.conf.json') | ConvertFrom-Json
$version = $config.version
$out = Join-Path $root 'rust-dist'
$portable = Join-Path $out "GameBoostPro-v$version"
New-Item -ItemType Directory -Path (Join-Path $portable 'tools') -Force | Out-Null
$app = Join-Path $root 'src-tauri\target\release\gameboostpro.exe'
$setup = Join-Path $root "src-tauri\target\release\bundle\nsis\Game Boost Pro_${version}_x64-setup.exe"
Copy-Item -LiteralPath $app -Destination (Join-Path $portable 'gameboostpro.exe') -Force
Copy-Item -LiteralPath $setup -Destination (Join-Path $out "GameBoostPro-$version-Setup.exe") -Force
Copy-Item -LiteralPath (Join-Path $root 'third_party\PresentMon\PresentMon-2.5.1-x64.exe') -Destination (Join-Path $portable 'tools\PresentMon.exe') -Force
Copy-Item -LiteralPath (Join-Path $root 'third_party\PresentMon\LICENSE.txt') -Destination (Join-Path $portable 'tools\PresentMon-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $portable 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\rust-v4.md') -Destination (Join-Path $portable 'README.md') -Force
$zip = Join-Path $out "GameBoostPro-Portable-v$version.zip"
Compress-Archive -Path "$portable\*" -DestinationPath $zip -CompressionLevel Optimal -Force
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $out "GameBoostPro-$version-Setup.exe"),$zip |
    ForEach-Object { '{0}  {1}' -f $_.Hash,(Split-Path -Leaf $_.Path) } |
    Set-Content -LiteralPath (Join-Path $out 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Release packages: $out"
