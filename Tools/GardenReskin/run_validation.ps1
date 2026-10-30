param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',
    [ValidateSet('main', 'welcome', 'teaching', 'success', 'failure', 'withdrawal', 'tools', 'tooloffer', 'gameplay')]
    [string[]]$Suites = @('main', 'welcome', 'teaching', 'success', 'failure', 'withdrawal'),
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) { throw "Unity executable not found: $Unity" }
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectRoot ('Docs\GardenReskin\runtime-validation-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "Output directory already exists; choose a new path: $OutputDirectory" }

# Unity permits one editor per project. Do not close or alter another editor.
$runningEditors = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'"
foreach ($editor in $runningEditors) {
    if ($editor.CommandLine -and $editor.CommandLine.IndexOf($projectRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "This project is already open in Unity (PID $($editor.ProcessId)). Close it before batch validation."
    }
}

$cases = @{
    main = @{ Method = 'LocalRewardsTopPlayValidation.Run'; Marker = 'NUT_LOCAL_REWARDS_TOP_PASS'; Images = @('local-rewards-top-BR.png', 'local-rewards-top-ID.png') }
    welcome = @{ Method = 'LocalWelcomePlayValidation.Run'; Marker = 'NUT_LOCAL_WELCOME_PASS'; Images = @('welcome-startup.png') }
    teaching = @{ Method = 'LocalTeachingViewPlayValidation.Run'; Marker = 'NUT_LOCAL_TEACHING_VIEW_PASS'; Images = @('local-teaching-0-current.png', 'local-teaching-1-current.png', 'local-teaching-2-current.png', 'local-teaching-3-current.png') }
    success = @{ Method = 'LocalCashRewardPlayValidation.Run'; Marker = 'NUT_LOCAL_CASH_REWARD_PASS'; Images = @('local-cash-BR.png', 'local-cash-ID.png') }
    failure = @{ Method = 'LocalFailurePlayValidation.Run'; Marker = 'NUT_LOCAL_FAILURE_PLAY_PASS'; Images = @('local-native-failure-current.png') }
    withdrawal = @{ Method = 'LocalWithdrawalPlayValidation.Run'; Marker = 'NUT_LOCAL_WITHDRAWAL_PASS'; Images = @('local-withdrawal-cash-BR.png', 'local-withdrawal-cash-ID.png', 'local-withdrawal-account-BR.png', 'local-withdrawal-account-ID.png', 'local-withdrawal-coin-BR.png', 'local-withdrawal-coin-ID.png', 'local-diamond-withdraw-feedback-BR.png', 'local-diamond-withdraw-feedback-ID.png') }
    tools = @{ Method = 'LocalToolsPlayValidation.Run'; Marker = 'NUT_LOCAL_TOOLS_PLAY_PASS'; Images = @('local-tools-exchange-current.png', 'local-tools-bottom-current.png') }
    tooloffer = @{ Method = 'LocalToolOfferPlayValidation.Run'; Marker = 'NUT_LOCAL_TOOL_OFFER_PASS'; Images = @('local-tool-offer-refresh.png') }
    gameplay = @{ Method = 'LocalGameplayPlayValidation.Run'; Marker = 'NUT_LOCAL_GAMEPLAY_PLAY_PASS'; Images = @('local-gameplay-board-current.png', 'local-gameplay-success-current.png', 'local-gameplay-failure-current.png', 'local-gameplay-resumed-current.png') }
}

New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$results = [Collections.Generic.List[object]]::new()
foreach ($suite in $Suites) {
    $case = $cases[$suite]
    $logPath = Join-Path $OutputDirectory ($suite + '.log')
    $started = [DateTime]::UtcNow
    # No -quit: each existing validator waits for Play mode and exits itself.
    # No -nographics: real UI and world rendering is required for screenshots.
    $arguments = '-batchmode -force-d3d11 -projectPath "' + $projectRoot + '" -executeMethod NutSort.Validation.' + $case.Method + ' -logFile "' + $logPath + '"'
    Write-Output ('Starting ' + $suite + ': NutSort.Validation.' + $case.Method)
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    $process.Refresh()
    $logText = if (Test-Path -LiteralPath $logPath) { [IO.File]::ReadAllText($logPath) } else { '' }
    $passed = $process.ExitCode -eq 0 -and $logText.Contains($case.Marker)
    $captures = [Collections.Generic.List[object]]::new()
    foreach ($name in $case.Images) {
        $source = Join-Path $projectRoot ('Library\' + $name)
        $fresh = (Test-Path -LiteralPath $source) -and (Get-Item -LiteralPath $source).LastWriteTimeUtc -ge $started.AddSeconds(-2)
        if ($fresh) {
            $destination = Join-Path $OutputDirectory ($suite + '-unity-play-' + $name)
            Copy-Item -LiteralPath $source -Destination $destination
            $captures.Add(@{ path = $destination; sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash; evidenceType = 'Unity Play Mode screenshot with explicit test fixture' })
        } else {
            $passed = $false
            $captures.Add(@{ expected = $source; missingFreshCapture = $true })
        }
    }
    $results.Add(@{ suite = $suite; method = 'NutSort.Validation.' + $case.Method; passed = $passed; exitCode = $process.ExitCode; marker = $case.Marker; log = $logPath; screenshots = $captures.ToArray() })
    $results.ToArray() | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'runtime-validation.json') -Encoding utf8
    if (-not $passed) { throw "Validation failed or a fresh screenshot was missing: $suite. Inspect $logPath. Existing preference backup, if any, remains under Library/ValidationBackups/." }
    Write-Output ('Passed ' + $suite)
}
Write-Output ('All requested suites passed. Runtime evidence: ' + $OutputDirectory)
