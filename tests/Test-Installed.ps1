$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$build = Join-Path $root 'dist\GameBoostPro.exe'
$installed = Join-Path $env:ProgramFiles 'Game Boost Pro\GameBoostPro.exe'
$failures = [Collections.Generic.List[string]]::new()
if (-not (Test-Path -LiteralPath $installed)) { $failures.Add('Installed application is missing') }
elseif ((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $build).Hash) {
    $failures.Add('Program Files executable does not match the latest build')
}
$shell = New-Object -ComObject WScript.Shell
$programRoots = @([Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('CommonPrograms'))
$links = @(& {
    foreach ($folder in $programRoots) {
        Get-ChildItem -LiteralPath $folder -Filter '*.lnk' -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in 'GameBoostPro.lnk','Game Boost Pro.lnk' } | ForEach-Object {
                $shortcut = $shell.CreateShortcut($_.FullName)
                try { [pscustomobject]@{Shortcut=$_.FullName;Target=$shortcut.TargetPath;Arguments=$shortcut.Arguments} }
                finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
            }
    }
})
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
$links | ConvertTo-Json -Compress | Write-Output
if ($links.Count -ne 1) { $failures.Add('Start Menu must contain exactly one Game Boost Pro application shortcut') }
foreach ($link in $links) {
    if ($link.Target -ne $installed -or $link.Arguments) { $failures.Add('Start Menu shortcut points to a legacy or unexpected executable') }
}
$key = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GameBoostPro' -ErrorAction SilentlyContinue
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($build).FileVersion
if ($key.DisplayVersion -ne ([version]$version).ToString(3)) { $failures.Add('Installed Apps version differs from the build') }
if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
Write-Output ('Installed application and Start Menu verified: ' + $version)
