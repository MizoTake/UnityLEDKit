$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Assets/LEDGallery'))
$sampleRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Packages/com.mizotake.led-wall/Samples~/LEDGallery'))
if (!$sampleRoot.StartsWith([IO.Path]::GetFullPath($projectRoot) + '\')) { throw 'Sample destination escaped the workspace.' }
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $sampleRoot -File -Recurse) {
    $relativePath = $file.FullName.Substring($sampleRoot.Length + 1)
    if (!(Test-Path -LiteralPath (Join-Path $sourceRoot $relativePath))) { Remove-Item -LiteralPath $file.FullName -Force }
}
$count = 0
foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -File -Recurse) {
    $relativePath = $file.FullName.Substring($sourceRoot.Length + 1)
    $destination = Join-Path $sampleRoot $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    $sourceHash = (Get-FileHash -LiteralPath $file.FullName).Hash
    if (!(Test-Path -LiteralPath $destination) -or $sourceHash -ne (Get-FileHash -LiteralPath $destination).Hash) {
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
    if ($sourceHash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Sample hash mismatch: $relativePath" }
    $count++
}
Write-Output "Sample synchronized: $count files, matching SHA-256 hashes."
