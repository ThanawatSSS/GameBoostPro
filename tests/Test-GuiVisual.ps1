$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$app = Join-Path $root 'dist\GameBoostPro.exe'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$runId = [Guid]::NewGuid().ToString('N')
$probe = Join-Path $tempRoot ("GameBoostPro-GuiVisualProbe-$runId.exe")
$outputDirectory = Join-Path $tempRoot ("GameBoostPro-GuiVisual-$runId")
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "ASSERT FAILED: $message" }
}

if (-not (Test-Path $app)) { & (Join-Path $root 'build.ps1') }

try {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    & $csc /nologo /target:exe /out:$probe /reference:System.dll /reference:System.Drawing.dll `
        /reference:System.Windows.Forms.dll `
        (Join-Path $root 'tests\GuiVisualProbe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'GUI visual probe build failed' }

    $resolvedApp = (Resolve-Path $app).Path
    & $probe $resolvedApp $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'GUI visual probe failed' }

    Add-Type -AssemblyName System.Drawing
    foreach ($name in 'main.png','advanced.png','graphics-advisor.png','frame-lab.png') {
        $path = Join-Path $outputDirectory $name
        Assert-True (Test-Path $path) "visual artifact $name"
        Assert-True ((Get-Item $path).Length -gt 10000) "non-empty visual artifact $name"
        $image = [Drawing.Image]::FromFile($path)
        try {
            Assert-True ($image.Width -ge 650) "visual width $name"
            Assert-True ($image.Height -ge 450) "visual height $name"
        }
        finally {
            $image.Dispose()
        }
    }
}
finally {
    if (Test-Path $probe) { Remove-Item -LiteralPath $probe -Force }
    if (Test-Path $outputDirectory) {
        $resolvedOutput = (Resolve-Path -LiteralPath $outputDirectory).Path
        Assert-True ($resolvedOutput.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
            $resolvedOutput -ne $tempRoot) 'visual cleanup remains inside the temporary directory'
        Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
    }
}

Write-Host 'GUI visual smoke test passed.' -ForegroundColor Green
