param([string]$PackagePath, [string]$PackageUrl)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$PackagePath) { $PackagePath = Join-Path $projectRoot 'Builds/UPM/com.mizotake.led-wall-0.1.0.tgz' }
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$consumerRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ('Temp/UPM Consumer-' + [Guid]::NewGuid().ToString('N'))))
if (!$consumerRoot.StartsWith([IO.Path]::GetFullPath((Join-Path $projectRoot 'Temp')) + '\')) { throw 'Consumer project escaped the verification workspace.' }
foreach ($directory in @('Assets/Editor', 'Packages', 'ProjectSettings', 'Extracted')) { New-Item -ItemType Directory -Force -Path (Join-Path $consumerRoot $directory) | Out-Null }
& tar -xzf $PackagePath -C (Join-Path $consumerRoot 'Extracted')
if ($LASTEXITCODE -ne 0) { throw 'Tarball extraction failed.' }
Copy-Item -LiteralPath (Join-Path $consumerRoot 'Extracted/package/Samples~/LEDGallery') -Destination (Join-Path $consumerRoot 'Assets/LEDGallery') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ConsumerSmoke.cs') -Destination (Join-Path $consumerRoot 'Assets/Editor/ConsumerSmoke.cs')
Copy-Item -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $consumerRoot 'ProjectSettings/ProjectVersion.txt')
$dependencies = [ordered]@{ 'com.mizotake.led-wall' = ('file:' + $PackagePath.Replace('\', '/')); 'com.unity.modules.audio' = '1.0.0'; 'com.unity.modules.video' = '1.0.0'; 'com.unity.modules.imgui' = '1.0.0'; 'com.unity.modules.physics' = '1.0.0'; 'com.unity.modules.imageconversion' = '1.0.0' }
if ($PackageUrl) { $dependencies['com.mizotake.led-wall'] = $PackageUrl }
$manifest = @{ dependencies = $dependencies } | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $consumerRoot 'Packages/manifest.json'), $manifest, [Text.UTF8Encoding]::new($false))
$logPath = Join-Path $projectRoot 'Logs/LEDWall/consumer-editor.log'
New-Item -ItemType Directory -Force -Path (Split-Path $logPath -Parent) | Out-Null
& unity run $consumerRoot --timeout 300 -- -executeMethod ConsumerSmoke.Run -logFile $logPath
$exitCode = $LASTEXITCODE
$resultPath = Join-Path $consumerRoot 'consumer-result.json'
if (!(Test-Path -LiteralPath $resultPath)) { throw "Consumer validation did not produce a result. Exit: $exitCode; log: $logPath" }
$reportName = if ($PackageUrl) { 'git-consumer-result.json' } else { 'consumer-result.json' }
Copy-Item -LiteralPath $resultPath -Destination (Join-Path $projectRoot ('Logs/LEDWall/' + $reportName)) -Force
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($exitCode -ne 0 -or !$result.success) { throw $result.error }
if ($PackageUrl -and $result.source -ne 'Git') { throw 'The consumer did not resolve a Git package.' }
Write-Output "Clean consumer: $($result.package)@$($result.version), source = $($result.source), $($result.shaders) shaders, $($result.panels) panels, receiver = $($result.receiverShader)."
