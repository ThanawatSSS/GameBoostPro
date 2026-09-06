$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$probe = Join-Path ([IO.Path]::GetTempPath()) ('GameBoostPro-Profiles-' + [Guid]::NewGuid() + '.exe')
try {
    & $csc /nologo /target:exe /main:ProfileProbe /out:$probe `
        /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
        /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll `
        (Join-Path $root 'src\GameBoostPro.cs') (Join-Path $root 'src\Dashboard.cs') `
        (Join-Path $root 'src\BuildVersion.cs') (Join-Path $root 'src\BoostProfiles.cs') (Join-Path $root 'src\GraphicsWorkspace.cs') (Join-Path $root 'tests\ProfileProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Profile probe build failed' }
    & $probe
    if ($LASTEXITCODE -ne 0) { throw 'Profile behavior tests failed' }
}
finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe -Force } }
