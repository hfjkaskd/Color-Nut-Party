[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Manifest,
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\..'),
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
$configuration = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if (-not $configuration.PSObject.Properties['entries'] -or @($configuration.entries).Count -eq 0) {
    throw 'The manifest must contain a nonempty entries array.'
}

function Resolve-ProjectPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $ProjectRoot $Path))
}
function Get-Option($Object, [string]$Name, $Default) {
    if ($Object.PSObject.Properties[$Name]) { return $Object.$Name }
    return $Default
}
function Get-BytesHash([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}
function Get-Vector4([string]$Text, [string]$Property) {
    $pattern = '(?m)^\s*' + [regex]::Escape($Property) + ':\s*\{x:\s*([^,}]+),\s*y:\s*([^,}]+),\s*z:\s*([^,}]+),\s*w:\s*([^,}]+)\}'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) { return $null }
    $vector = [double[]]::new(4)
    for ($i = 0; $i -lt 4; $i++) { $vector[$i] = [double]::Parse($match.Groups[$i + 1].Value, [Globalization.CultureInfo]::InvariantCulture) }
    return ,$vector
}
function Convert-Border($Value, [string]$Label) {
    if ($Value -is [Array] -and $Value.Count -eq 4) { return ,[double[]]$Value }
    if ($Value -and $Value.PSObject.Properties['left'] -and $Value.PSObject.Properties['bottom'] -and $Value.PSObject.Properties['right'] -and $Value.PSObject.Properties['top']) {
        return ,[double[]]@($Value.left, $Value.bottom, $Value.right, $Value.top)
    }
    throw "$Label must contain [left, bottom, right, top] or the four named properties."
}
function Convert-Ratios($Value) {
    if ($Value -is [Array] -or ($Value -and $Value.PSObject.Properties['left'])) { return ,(Convert-Border $Value 'sourceSliceRatio') }
    $number = [double]$Value
    return ,[double[]]@($number, $number, $number, $number)
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (-not ('GardenReskin.GeneratedSpriteImporter' -as [type])) {
    $references = @('System.Drawing')
    if ($PSVersionTable.PSEdition -eq 'Core') {
        $references += @('System.Drawing.Common', 'System.Drawing.Primitives', 'System.Runtime', 'System.Runtime.InteropServices', 'System.Collections')
        foreach ($assembly in @('System.Private.Windows.GdiPlus', 'System.Private.Windows.Core')) {
            if (Test-Path -LiteralPath (Join-Path $PSHOME ($assembly + '.dll'))) { $references += $assembly }
        }
    }
    Add-Type -Path (Join-Path $PSScriptRoot 'GeneratedSpriteImporter.cs') -ReferencedAssemblies $references
}

$zipPath = Resolve-ProjectPath (Get-Option $configuration 'originalZip' 'Docs/GardenReskin/OriginalAssets/before-reskin.zip')
$reportPath = Resolve-ProjectPath (Get-Option $configuration 'report' 'Docs/GardenReskin/import-report.json')
$assetsRoot = (Join-Path $ProjectRoot 'Assets') + [IO.Path]::DirectorySeparatorChar
if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) { throw "Original ZIP is missing: $zipPath" }
if ($reportPath.StartsWith($assetsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'The report must be outside Assets.' }

# Index the existing serialized Sprite borders; reading the header avoids loading
# font atlases or unrelated ScriptableObjects into memory.
$spriteIndex = @{}
$spriteFiles = if (Get-Command rg -ErrorAction SilentlyContinue) {
    @(& rg -l --glob '*.asset' '^Sprite:' (Join-Path $ProjectRoot 'Assets'))
} else {
    @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'Assets') -Recurse -File -Filter '*.asset' | Select-Object -ExpandProperty FullName)
}
foreach ($spritePath in $spriteFiles) {
    $header = (Get-Content -LiteralPath $spritePath -TotalCount 50) -join "`n"
    if ($header -notmatch '(?m)^Sprite:') { continue }
    $texture = [regex]::Match($header, '(?m)^\s+texture:\s*\{fileID:\s*\d+,\s*guid:\s*([a-fA-F0-9]{32})')
    if (-not $texture.Success) { continue }
    $border = Get-Vector4 $header 'm_Border'
    if ($null -eq $border) { continue }
    $guid = $texture.Groups[1].Value.ToLowerInvariant()
    if (-not $spriteIndex.ContainsKey($guid)) { $spriteIndex[$guid] = [Collections.Generic.List[object]]::new() }
    $spriteIndex[$guid].Add([pscustomobject]@{ path = $spritePath; border = $border })
}

$protectedHashes = @{}
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$entries = [Collections.Generic.List[object]]::new()
$staging = Join-Path $ProjectRoot ('Docs\GardenReskin\ImportStaging\' + [Guid]::NewGuid().ToString('N'))
if (-not $DryRun) { New-Item -ItemType Directory -Path $staging -Force | Out-Null }
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    foreach ($entry in @($configuration.entries)) {
        $target = Resolve-ProjectPath ([string]$entry.target)
        $source = Resolve-ProjectPath ([string]$entry.source)
        if (-not $target.StartsWith($assetsRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($target) -ne '.png') {
            throw "Target must resolve to an existing PNG within Assets: $target"
        }
        if (-not $seen.Add($target)) { throw "Duplicate target: $target" }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Target does not exist: $target" }
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Generated source does not exist: $source" }
        if ($source.Equals($target, [StringComparison]::OrdinalIgnoreCase)) { throw "Source and target must differ: $target" }
        $relativeTarget = $target.Substring($ProjectRoot.Length + 1).Replace('\', '/')
        $zipEntry = $archive.GetEntry($relativeTarget)
        if ($null -eq $zipEntry) { throw "Target is absent from original ZIP: $relativeTarget" }
        $stream = $zipEntry.Open()
        $buffer = [IO.MemoryStream]::new()
        try { $stream.CopyTo($buffer); $original = $buffer.ToArray() }
        finally { $stream.Dispose(); $buffer.Dispose() }

        $mode = [string](Get-Option $entry 'mode' 'grid')
        $columns = [int](Get-Option $entry 'columns' 1)
        $rows = [int](Get-Option $entry 'rows' 1)
        $cell = [int](Get-Option $entry 'cell' 0)
        $threshold = [int](Get-Option $entry 'alphaThreshold' (Get-Option $configuration 'alphaThreshold' 100))
        $padding = [int](Get-Option $entry 'padding' (Get-Option $configuration 'padding' 2))
        $preserveOriginalAlpha = [bool](Get-Option $entry 'preserveOriginalAlpha' $false)
        $ratios = Convert-Ratios (Get-Option $entry 'sourceSliceRatio' (Get-Option $configuration 'sourceSliceRatio' 0.16))
        $border = [double[]]@(0, 0, 0, 0)
        $borderSource = 'none'
        $metaPath = $target + '.meta'
        if (-not (Test-Path -LiteralPath $metaPath -PathType Leaf)) { throw "Original meta is missing: $metaPath" }
        $protectedHashes[$metaPath] = (Get-FileHash -LiteralPath $metaPath -Algorithm SHA256).Hash
        $meta = [IO.File]::ReadAllText($metaPath)
        $guidMatch = [regex]::Match($meta, '(?m)^guid:\s*([a-fA-F0-9]{32})')
        if (-not $guidMatch.Success) { throw "Texture meta has no GUID: $metaPath" }
        $guid = $guidMatch.Groups[1].Value.ToLowerInvariant()
        $candidates = [Collections.Generic.List[object]]::new()
        $metaBorder = Get-Vector4 $meta 'spriteBorder'
        if ($null -ne $metaBorder -and ($metaBorder | Measure-Object -Sum).Sum -gt 0) { $candidates.Add([pscustomobject]@{ path = $metaPath; border = $metaBorder }) }
        if ($spriteIndex.ContainsKey($guid)) {
            foreach ($sprite in $spriteIndex[$guid]) {
                $protectedHashes[$sprite.path] = (Get-FileHash -LiteralPath $sprite.path -Algorithm SHA256).Hash
                if (Test-Path -LiteralPath ($sprite.path + '.meta') -PathType Leaf) {
                    $protectedHashes[$sprite.path + '.meta'] = (Get-FileHash -LiteralPath ($sprite.path + '.meta') -Algorithm SHA256).Hash
                }
                if (($sprite.border | Measure-Object -Sum).Sum -gt 0) { $candidates.Add($sprite) }
            }
        }
        if ($entry.PSObject.Properties['oldBorder']) {
            $border = Convert-Border $entry.oldBorder 'oldBorder'
            $borderSource = 'manifest.oldBorder'
        } elseif ($candidates.Count -gt 0) {
            $border = $candidates[0].border
            $borderSource = $candidates[0].path
            foreach ($candidate in $candidates) {
                for ($i = 0; $i -lt 4; $i++) {
                    if ([Math]::Abs($candidate.border[$i] - $border[$i]) -gt 0.01) {
                        throw "Conflicting existing borders for $relativeTarget; set oldBorder explicitly."
                    }
                }
            }
        }

        $result = [GardenReskin.GeneratedSpriteImporter]::Build($original, $source, $mode, $columns, $rows, $cell, $threshold, $padding, $border, $ratios, $preserveOriginalAlpha)
        $stagedPath = Join-Path $staging ($entries.Count.ToString('D4') + '.png')
        if (-not $DryRun) { [IO.File]::WriteAllBytes($stagedPath, $result.Png) }
        $entries.Add([pscustomobject]@{
            target = $relativeTarget; source = $source; mode = $mode; columns = $columns; rows = $rows; cell = $cell
            originalZipEntry = $zipEntry.FullName; originalSha256 = Get-BytesHash $original
            previousSha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
            sourceSha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
            outputSha256 = Get-BytesHash $result.Png; outputBytes = $result.Png.Length
            originalAndOutputSize = @($result.OutputWidth, $result.OutputHeight)
            sourceSize = @($result.SourceWidth, $result.SourceHeight); sourceCell = $result.SourceCell
            sourceBounds = $result.SourceBounds; originalAlphaBounds = $result.OriginalAlphaBounds
            largestComponentPixels = $result.LargestComponentPixels; alphaThreshold = $threshold; padding = $padding
            retainedComponents = $result.RetainedComponents; preserveOriginalAlpha = $result.PreservedOriginalAlpha
            nineSlice = $result.NineSlice; oldBorder = $border; borderSource = $borderSource
            sourceSliceRatio = $ratios; destinationSlicePixels = $result.DestinationSlicePixels
            stagedPath = if ($DryRun) { $null } else { $stagedPath }
        })
        Write-Output ("Prepared $relativeTarget ($($result.OutputWidth)x$($result.OutputHeight), nineSlice=$($result.NineSlice))")
    }
} finally { $archive.Dispose() }

# All entries have been decoded and validated before changing any target.
foreach ($entry in $entries) {
    $target = Resolve-ProjectPath $entry.target
    if (-not $DryRun) {
        [IO.File]::Copy($entry.stagedPath, $target, $true)
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.outputSha256) { throw "Written PNG hash mismatch: $target" }
    } elseif ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.previousSha256) {
        throw "Target changed during dry run: $target"
    }
}
$protected = [Collections.Generic.List[object]]::new()
foreach ($path in $protectedHashes.Keys) {
    $currentHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($currentHash -ne $protectedHashes[$path]) { throw "Protected metadata changed during import: $path" }
    $protected.Add([pscustomobject]@{ path = $path; sha256 = $currentHash.ToLowerInvariant(); unchanged = $true })
}
$reportDirectory = [IO.Path]::GetDirectoryName($reportPath)
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report = [ordered]@{
    status = 'PASS'; dryRun = [bool]$DryRun; generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    manifest = $manifestPath; originalZip = $zipPath; originalZipSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    method = 'Generated PNG pixels only; substantial alpha-component union bounds, original alpha bounds, bicubic or nine-slice resampling, optional original-alpha mask. No recolouring or painting.'
    targetCount = $entries.Count; entries = @($entries.ToArray()); protectedFiles = @($protected.ToArray())
}
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Output ("PASS: $($entries.Count) entries; DryRun=$([bool]$DryRun); protected metadata unchanged. Report: $reportPath")
