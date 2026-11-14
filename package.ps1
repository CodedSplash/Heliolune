param([string]$Output = (Join-Path $PSScriptRoot 'dist'))

$ErrorActionPreference = 'Stop'
$null = New-Item -ItemType Directory -Force -Path $Output
$destination = (Resolve-Path -LiteralPath $Output).Path
$build = Join-Path $PSScriptRoot 'bin'
& (Join-Path $PSScriptRoot 'build.ps1') -Output $build

$archive = Join-Path $destination 'Heliolune-Windows.zip'
$files = @((Join-Path $build 'Sutki.exe'), (Join-Path $build 'Sutki.exe.config'))
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Value "$hash  Heliolune-Windows.zip" -Encoding Ascii
Write-Output $archive
