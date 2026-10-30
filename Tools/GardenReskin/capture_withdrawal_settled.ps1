param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$sourcePath = Join-Path $projectRoot 'Assets\NutSort\Editor\Validation\LocalWithdrawalPlayValidation.cs'
$runnerPath = Join-Path $PSScriptRoot 'run_validation.ps1'
if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) { throw "Unity executable not found: $Unity" }
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Existing withdrawal validation source not found: $sourcePath" }
if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) { throw "Existing validation runner not found: $runnerPath" }

# Refuse to edit even an Editor-only test while this project is open.
# Do not terminate, close, or modify another editor.
$runningEditors = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'"
foreach ($editor in $runningEditors) {
    if (-not $editor.CommandLine) { throw "Cannot identify running Unity PID $($editor.ProcessId); no source files were modified." }
    $editorCommand = $editor.CommandLine.Replace('/', '\')
    if ($editorCommand.IndexOf($projectRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "This project is already open in Unity (PID $($editor.ProcessId)); no source files were modified."
    }
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectRoot ('Docs\GardenReskin\runtime-withdrawal-settled-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$auditDirectory = $OutputDirectory + '.patch-audit'
if (Test-Path -LiteralPath $OutputDirectory) { throw "Output directory already exists; choose a new path: $OutputDirectory" }
if (Test-Path -LiteralPath $auditDirectory) { throw "Patch audit directory already exists; choose a new path: $auditDirectory" }

$originalBytes = [IO.File]::ReadAllBytes($sourcePath)
$originalHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
$originalWriteTimeUtc = (Get-Item -LiteralPath $sourcePath).LastWriteTimeUtc
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$originalText = $utf8.GetString($originalBytes)
$lineEnding = if ($originalText.Contains("`r`n")) { "`r`n" } else { "`n" }
$before = @'
                    ScreenCapture.CaptureScreenshot("Library/local-withdrawal-cash-"+(index==0?"BR":"ID")+".png");
                    panel.WithdrawalButton.onClick.Invoke();Check(game.User.Gold==1.76f&&game.User.Coin==0,"Unmet withdrawal cannot alter balances");
                    ready=Time.time+.8f;phase=10;return;
'@
$after = @'
                    ScreenCapture.CaptureScreenshot("Library/local-withdrawal-cash-"+(index==0?"BR":"ID")+".png");
                    ready=Time.time+.2f;phase=101;return;
                }
                if(phase==101)
                {
                    var panel=local.WithdrawalPanel;
                    panel.WithdrawalButton.onClick.Invoke();Check(game.User.Gold==1.76f&&game.User.Coin==0,"Unmet withdrawal cannot alter balances");
                    ready=Time.time+.8f;phase=10;return;
'@
$before = $before.Replace("`r`n", "`n").Replace("`n", $lineEnding)
$after = $after.Replace("`r`n", "`n").Replace("`n", $lineEnding)
if ([regex]::Matches($originalText, [regex]::Escape($before)).Count -ne 1) {
    throw 'Expected screenshot/click block must match exactly once. Source changed; no files were modified.'
}
if ($originalText.Contains('phase==101') -or $originalText.Contains('phase=101')) {
    throw 'Validation already uses phase 101; no files were modified.'
}
$patchedText = $originalText.Replace($before, $after)
$patchedBytes = $utf8.GetBytes($patchedText)

# Save exact original bytes and a readable temporary-source copy outside Assets.
# The runner creates OutputDirectory itself, so audit evidence uses a sibling.
New-Item -ItemType Directory -Path $auditDirectory | Out-Null
$backupPath = Join-Path $auditDirectory 'original-source.cs'
$patchedCopyPath = Join-Path $auditDirectory 'temporary-source.cs'
$auditPath = Join-Path $auditDirectory 'capture-patch-audit.json'
[IO.File]::WriteAllBytes($backupPath, $originalBytes)
[IO.File]::WriteAllBytes($patchedCopyPath, $patchedBytes)
$patchedHash = (Get-FileHash -LiteralPath $patchedCopyPath -Algorithm SHA256).Hash
$audit = [ordered]@{
    purpose = 'Separate end-of-frame cash-panel capture from the following existing withdrawal-button click by 0.2 seconds.'
    source = $sourcePath
    sourceOriginalSha256 = $originalHash
    sourceTemporarySha256 = $patchedHash
    originalBackup = $backupPath
    temporarySource = $patchedCopyPath
    originalFragment = $before
    temporaryFragment = $after
    runner = $runnerPath
    runnerSha256 = (Get-FileHash -LiteralPath $runnerPath -Algorithm SHA256).Hash
    unity = $Unity
    suite = 'withdrawal'
    outputDirectory = $OutputDirectory
    startedUtc = [DateTime]::UtcNow.ToString('o')
    status = 'prepared'
    sourceRestored = $false
    scope = 'Temporary Editor validation timing only. Existing assertions, user data fixture, button action, and remaining test flow are retained. Runtime game files are not edited.'
}
$audit | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $auditPath -Encoding utf8

$patchWasApplied = $false
try {
    # Refuse to overwrite an intervening source edit after preparing the audit.
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $originalHash) {
        throw 'Validation source changed while preparing capture; refusing to apply the temporary patch.'
    }
    [IO.File]::WriteAllBytes($sourcePath, $patchedBytes)
    $patchWasApplied = $true
    $audit.status = 'running'
    $audit | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $auditPath -Encoding utf8
    & $runnerPath -Unity $Unity -Suites withdrawal -OutputDirectory $OutputDirectory
    $audit.status = 'completed'
}
catch {
    $audit.status = 'failed'
    $audit.error = $_.Exception.Message
    throw
}
finally {
    # Exact-byte restoration also runs if compilation or the existing test fails.
    # Preserve an external edit made while validation was running.
    $currentHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    $externalEdit = $patchWasApplied -and $currentHash -ne $patchedHash -and $currentHash -ne $originalHash
    if ($patchWasApplied -and -not $externalEdit) {
        [IO.File]::WriteAllBytes($sourcePath, $originalBytes)
        [IO.File]::SetLastWriteTimeUtc($sourcePath, $originalWriteTimeUtc)
    }
    $restoredHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    $audit.temporaryPatchApplied = $patchWasApplied
    $audit.restorationBlockedByExternalEdit = $externalEdit
    $audit.sourceSha256BeforeRestoration = $currentHash
    $audit.sourceRestoredSha256 = $restoredHash
    $audit.sourceRestored = $restoredHash -eq $originalHash
    $audit.finishedUtc = [DateTime]::UtcNow.ToString('o')
    $audit | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $auditPath -Encoding utf8
    if ($patchWasApplied -and -not $audit.sourceRestored) {
        if ($externalEdit) {
            throw "Original source restoration is incomplete: an external edit was preserved. Review it against the exact original backup: $backupPath. Audit: $auditPath"
        }
        throw "Original validation source hash was not restored. Exact backup: $backupPath. Audit: $auditPath"
    }
    if ($patchWasApplied) { Write-Output ('WITHDRAWAL_CAPTURE_SOURCE_RESTORED SHA256=' + $restoredHash) }
}
Write-Output ('Settled Unity Play Mode withdrawal evidence: ' + $OutputDirectory)
Write-Output ('Temporary Editor patch audit: ' + $auditDirectory)
