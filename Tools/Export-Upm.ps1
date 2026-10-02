$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Sync-Sample.ps1')
$output = Join-Path $projectRoot 'Builds/UPM'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Push-Location (Join-Path $projectRoot 'Packages/com.mizotake.led-wall')
try {
    $result = & npm pack --pack-destination $output --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'npm pack failed.' }
    Write-Output (Join-Path $output $result[0].filename)
} finally { Pop-Location }
