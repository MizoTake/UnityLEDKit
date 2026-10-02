param([string]$PackagePath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Packages/com.mizotake.led-wall'))
if (!$PackagePath) { $PackagePath = Join-Path $projectRoot 'Builds/UPM/com.mizotake.led-wall-0.1.0.tgz' }
$extractedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ('Temp/UPM Distribution-' + [Guid]::NewGuid().ToString('N'))))
if (!$extractedRoot.StartsWith([IO.Path]::GetFullPath((Join-Path $projectRoot 'Temp')) + '\')) { throw 'Distribution verification escaped the workspace.' }
New-Item -ItemType Directory -Force -Path $extractedRoot | Out-Null
& tar -xzf $PackagePath -C $extractedRoot
if ($LASTEXITCODE -ne 0) { throw 'Tarball extraction failed.' }
$packedRoot = Join-Path $extractedRoot 'package'
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse)
$packedFiles = @(Get-ChildItem -LiteralPath $packedRoot -File -Recurse)
$mismatches = @()
foreach ($file in $sourceFiles) {
    $relative = $file.FullName.Substring($sourceRoot.Length + 1)
    $packed = Join-Path $packedRoot $relative
    if (!(Test-Path -LiteralPath $packed) -or (Get-FileHash -LiteralPath $packed).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { $mismatches += $relative }
}
$report = @{ success = ($mismatches.Count -eq 0 -and $sourceFiles.Count -eq $packedFiles.Count); files = $sourceFiles.Count; packedFiles = $packedFiles.Count; mismatches = $mismatches; sha256 = (Get-FileHash -LiteralPath $PackagePath).Hash.ToLowerInvariant() }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'Logs/LEDWall/distribution-result.json') -Encoding utf8
if (!$report.success) { throw ($report | ConvertTo-Json -Compress) }
Write-Output "Distribution: $($report.files) files, all SHA-256 hashes match. Archive SHA-256: $($report.sha256)"
