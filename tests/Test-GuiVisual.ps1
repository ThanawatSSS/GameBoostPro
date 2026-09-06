param([string]$EvidenceDirectory)
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
    foreach ($name in 'main.png','main-narrow.png','main-wide.png','main-scale150.png','main-scale200.png','main-scale200-bottom.png',
        'main-en.png','main-override.png','main-active.png','main-busy.png','main-search-empty.png',
        'library.png','advanced.png','graphics-advisor.png','frame-lab.png',
        'library-en.png','advanced-en.png','graphics-advisor-en.png','frame-lab-en.png',
        'main-frame-entry.png','main-narrow-bottom.png','library-narrow.png','library-narrow-en.png',
        'advanced-narrow.png','advanced-narrow-en.png','graphics-narrow.png','graphics-narrow-en.png',
        'graphics-compatibility.png','graphics-compatibility-en.png','frame-boosted.png','frame-boosted-en.png',
        'frame-empty.png','frame-empty-en.png') {
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
    if ($EvidenceDirectory) {
        New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
        Get-ChildItem -LiteralPath $outputDirectory -Filter '*.png' | Copy-Item -Destination $EvidenceDirectory
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
