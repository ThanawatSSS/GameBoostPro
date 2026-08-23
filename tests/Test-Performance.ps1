$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root 'dist'
$app = Join-Path $dist 'GameBoostPro.exe'
$probe = Join-Path ([IO.Path]::GetTempPath()) ('GameBoostPro-GuiPerfProbe-' + [Guid]::NewGuid() + '.exe')
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $app)) { & (Join-Path $root 'build.ps1') }

try {
    & $csc /nologo /target:exe /out:$probe /reference:System.dll /reference:System.Windows.Forms.dll `
        (Join-Path $root 'tests\GuiPerfProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Performance probe build failed' }

    $result = & $probe (Resolve-Path $app).Path
    $exitCode = $LASTEXITCODE
    $result | Write-Host
    if ($exitCode -ne 0) { throw 'Performance regression budget exceeded' }
}
finally {
    if (Test-Path $probe) { Remove-Item -LiteralPath $probe -Force }
}

Write-Host 'Performance regression test passed.' -ForegroundColor Green
