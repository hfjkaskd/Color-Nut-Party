param(
    [ValidateSet('Core', 'All', 'CoreWide', 'Auxiliary', 'Secondary', 'Interaction', 'InteractionWide')]
    [string]$Suite = 'Core',
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$existing = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like "*$project*" }
if ($existing) { throw "This Unity project is already open. Close it before batch acceptance; no editor is interrupted automatically." }
$methods = @{
    Core = 'NutSort.Validation.OctoberSkinValidation.RunCore'
    All = 'NutSort.Validation.OctoberSkinValidation.RunAll'
    CoreWide = 'NutSort.Validation.OctoberSkinValidation.RunCoreWide'
    Auxiliary = 'NutSort.Validation.OctoberSkinValidation.RunAuxiliary'
    Secondary = 'NutSort.Validation.OctoberSkinValidation.RunSecondary'
    Interaction = 'NutSort.Validation.ApprovedSkinPlayValidation.RunUS'
    InteractionWide = 'NutSort.Validation.ApprovedSkinPlayValidation.RunUSWide'
}
$reportDir = Join-Path $project 'Docs\GardenReskin\implementation-20261008\validation'
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$log = Join-Path $reportDir ("{0}-{1}.log" -f $Suite.ToLowerInvariant(), (Get-Date -Format 'yyyyMMdd-HHmmss'))
$arguments = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod', $methods[$Suite], '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Unity validation started: suite=$Suite pid=$($process.Id) log=$log"
$process.WaitForExit()
$process.Refresh()
$pass = if ($Suite -like 'Interaction*') { 'APPROVED_SKIN_PLAY_PASS' } else { 'OCTOBER_SKIN_CAPTURE_PASS' }
$logContent = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
if ($process.ExitCode -ne 0 -or $logContent -notmatch $pass) {
    Get-Content -LiteralPath $log -Tail 65
    throw "Unity validation did not pass: exit=$($process.ExitCode), suite=$Suite, log=$log"
}
if ($Suite -like 'Interaction*') {
    $destination = Join-Path $reportDir $Suite.ToLowerInvariant()
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach ($page in @('cash', 'account', 'diamond', 'refresh')) {
        Copy-Item -LiteralPath (Join-Path $project "Library\approved-skin-$page.png") -Destination (Join-Path $destination "$page.png") -Force
    }
}
$logContent -split "`n" | Where-Object { $_ -match $pass } | Write-Output
Write-Output "Validation log: $log"
