$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$probe = Join-Path ([IO.Path]::GetTempPath()) ('GameBoostPro-InstallerProbe-' + [Guid]::NewGuid() + '.exe')
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
try {
    & $csc /nologo /target:exe /out:$probe /reference:System.dll (Join-Path $root 'tests\InstallerProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Installer probe build failed' }
    & $probe (Join-Path $root 'dist\GameBoostPro-Setup.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Installer regression test failed' }
}
finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe -Force } }
