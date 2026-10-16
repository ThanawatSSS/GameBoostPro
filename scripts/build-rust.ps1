$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $presentMon = Join-Path $root 'third_party\PresentMon\PresentMon-2.5.1-x64.exe'
    if ((Get-FileHash -LiteralPath $presentMon).Hash -ne '9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191') { throw 'PresentMon checksum mismatch' }
    if ((Get-AuthenticodeSignature -LiteralPath $presentMon).Status -ne 'Valid') { throw 'PresentMon Intel signature verification failed' }
    if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) { throw 'Install Rust MSVC and the Visual Studio C++ build tools first' }
    & npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed' }
    & cargo fmt --manifest-path src-tauri/Cargo.toml --check
    if ($LASTEXITCODE -ne 0) { throw 'Rust formatting check failed' }
    & cargo clippy --locked --manifest-path src-tauri/Cargo.toml -- -D warnings
    if ($LASTEXITCODE -ne 0) { throw 'Clippy failed' }
    & cargo test --locked --manifest-path src-tauri/Cargo.toml
    if ($LASTEXITCODE -ne 0) { throw 'Rust tests failed' }
    & node node_modules/@tauri-apps/cli/tauri.js build --ci
    if ($LASTEXITCODE -ne 0) { throw 'Tauri build failed' }
    & (Join-Path $PSScriptRoot 'package-rust.ps1')
}
finally { Pop-Location }
