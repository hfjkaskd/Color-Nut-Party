param(
    [string]$ApkPath = 'C:/Projects/com.nut.sort.relax/Builds/Android/ColorNutParty-1.0.1-20261013-app-icon-fix.apk',
    [string]$AuditDirectory = 'C:/Projects/com.nut.sort.relax/Docs/APKBuild/20261013-app-icon/apk-icons'
)

$ErrorActionPreference = 'Stop'
$aapt = 'F:/AndroidSDK/build-tools/35.0.0/aapt.exe'
$project = 'C:/Projects/com.nut.sort.relax'
$mainSource = Join-Path $project 'Assets/Art/Branding/ColorNutParty-AppIcon.png'
$adaptiveSource = Join-Path $project 'Assets/Art/Branding/ColorNutParty-AdaptiveBackground.png'
if (-not (Test-Path -LiteralPath $ApkPath -PathType Leaf)) { throw 'Final APK is not available.' }
New-Item -ItemType Directory -Path $AuditDirectory -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

function Invoke-AaptDump([string[]]$Arguments) {
    $result = & $aapt @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('aapt failed: ' + ($Arguments -join ' ')) }
    return ($result -join "`n")
}

$manifest = Invoke-AaptDump -Arguments @('dump', 'xmltree', $ApkPath, 'AndroidManifest.xml')
$manifest | Set-Content -Encoding utf8 -LiteralPath (Join-Path $AuditDirectory 'manifest-icons.txt')
$resources = Invoke-AaptDump -Arguments @('dump', '--values', 'resources', $ApkPath)
$resources | Set-Content -Encoding utf8 -LiteralPath (Join-Path $AuditDirectory 'resources.txt')
$iconMatch = [regex]::Match($manifest, 'android:icon\([^)]*\)=@(0x[0-9a-f]+)')
if (-not $iconMatch.Success) { throw 'Application icon resource is missing from APK manifest.' }
$iconId = $iconMatch.Groups[1].Value
$roundMatch = [regex]::Match($manifest, 'android:roundIcon\([^)]*\)=@(0x[0-9a-f]+)')

$resourceMap = @{}
$resourceNames = @{}
$config = ''
$pendingId = $null
foreach ($line in ($resources -split "`n")) {
    if ($line -match '^\s*config (.*):\s*$') { $config = $Matches[1] }
    if ($line -match '^\s*resource (0x[0-9a-f]+) ([^: ]+:[^: ]+): t=(0x[0-9a-f]+) d=(0x[0-9a-f]+)') {
        $pendingId = $Matches[1]
        $resourceNames[$pendingId] = $Matches[2]
        if (-not $resourceMap.ContainsKey($pendingId)) { $resourceMap[$pendingId] = @() }
        if ($Matches[3] -eq '0x01') {
            $resourceMap[$pendingId] += [pscustomobject]@{ Config = $config; Path = $null; Alias = $Matches[4] }
        }
    }
    elseif ($pendingId -and $line -match '^\s*\(string(?:8|16)?\) "(res/[^\"]+)"') {
        $resourceMap[$pendingId] += [pscustomobject]@{ Config = $config; Path = $Matches[1]; Alias = $null }
        $pendingId = $null
    }
}

function Resolve-ResourceFiles([string]$Id, [int]$Depth = 0) {
    if ($Depth -gt 8) { throw 'Icon resource alias loop.' }
    if (-not $resourceMap.ContainsKey($Id)) { throw "Icon resource cannot be resolved: $Id" }
    foreach ($value in $resourceMap[$Id]) {
        if ($value.Path) { $value }
        elseif ($value.Alias) { Resolve-ResourceFiles $value.Alias ($Depth + 1) }
    }
}

function Compare-IconArtwork([System.Drawing.Bitmap]$Bitmap, [string]$SourcePath) {
    $source = [System.Drawing.Bitmap]::new($SourcePath)
    $scaled = [System.Drawing.Bitmap]::new($Bitmap.Width, $Bitmap.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($scaled)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $scaled.Width, $scaled.Height))
        [double]$error = 0
        [long]$samples = 0
        for ($y = 0; $y -lt $Bitmap.Height; $y++) {
            for ($x = 0; $x -lt $Bitmap.Width; $x++) {
                $actual = $Bitmap.GetPixel($x, $y)
                $expected = $scaled.GetPixel($x, $y)
                $error += [math]::Abs([int]$actual.R - [int]$expected.R)
                $error += [math]::Abs([int]$actual.G - [int]$expected.G)
                $error += [math]::Abs([int]$actual.B - [int]$expected.B)
                $samples += 3
            }
        }
        return [math]::Round($error / $samples, 4)
    }
    finally { $graphics.Dispose(); $scaled.Dispose(); $source.Dispose() }
}

$zip = [System.IO.Compression.ZipFile]::OpenRead($ApkPath)
$pngReports = [System.Collections.Generic.List[object]]::new()
$xmlReports = [System.Collections.Generic.List[object]]::new()
$seenImages = @{}
$failures = [System.Collections.Generic.List[string]]::new()

function Test-IconPng([string]$Role, [string]$Id, [object]$Resource, [string]$ExpectedSource) {
    $key = $Role + '|' + $Resource.Config + '|' + $Resource.Path
    if ($seenImages.ContainsKey($key)) { return }
    $seenImages[$key] = $true
    $entry = $zip.GetEntry($Resource.Path)
    if ($null -eq $entry) { throw ('APK icon file is missing: ' + $Resource.Path) }
    $safeConfig = $Resource.Config -replace '[^a-zA-Z0-9_-]', '_'
    $outputName = $Role + '-' + $safeConfig + '-' + [IO.Path]::GetFileName($Resource.Path)
    $outputPath = Join-Path $AuditDirectory $outputName
    $inputStream = $entry.Open()
    $outputStream = [IO.File]::Create($outputPath)
    try { $inputStream.CopyTo($outputStream) }
    finally { $outputStream.Dispose(); $inputStream.Dispose() }
    $bitmap = [System.Drawing.Bitmap]::new($outputPath)
    try {
        $fullyTransparent = $true
        $fullyOpaque = $true
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                $alpha = $bitmap.GetPixel($x, $y).A
                if ($alpha -ne 0) { $fullyTransparent = $false }
                if ($alpha -ne 255) { $fullyOpaque = $false }
            }
        }
        $meanError = $null
        if ($ExpectedSource) {
            $meanError = Compare-IconArtwork $bitmap $ExpectedSource
            # Unity and GDI+ use different downsampling filters. 12/255 allows edge
            # differences while rejecting unrelated/default Unity launcher artwork.
            if ($meanError -gt 12) { $failures.Add("Unexpected artwork in $outputName; RGB mean error=$meanError") }
            if (-not $fullyOpaque) { $failures.Add("Opaque launcher art has transparency: $outputName") }
        }
        elseif (-not $fullyTransparent) { $failures.Add("Adaptive foreground is not clear: $outputName") }
        $pngReports.Add([pscustomobject]@{
            Role = $Role; ResourceId = $Id; Config = $Resource.Config
            ArchivePath = $Resource.Path; ExtractedPath = $outputPath
            Width = $bitmap.Width; Height = $bitmap.Height
            FullyTransparent = $fullyTransparent; FullyOpaque = $fullyOpaque
            SourceArtwork = $ExpectedSource; RgbMeanAbsoluteError = $meanError
        })
    }
    finally { $bitmap.Dispose() }
}

function Test-LauncherResource([string]$Id, [string]$Role) {
    foreach ($resource in @(Resolve-ResourceFiles $Id)) {
        if ($resource.Path.EndsWith('.png')) {
            Test-IconPng $Role $Id $resource $mainSource
        }
        elseif ($resource.Path.EndsWith('.xml')) {
            $xml = Invoke-AaptDump -Arguments @('dump', 'xmltree', $ApkPath, $resource.Path)
            $xmlName = $Role + '-' + [IO.Path]::GetFileName($resource.Path) + '.txt'
            $xml | Set-Content -Encoding utf8 -LiteralPath (Join-Path $AuditDirectory $xmlName)
            if ($xml -notmatch 'E: adaptive-icon') { throw 'Launcher XML is not an adaptive icon.' }
            $background = [regex]::Match($xml, '(?s)E: background.*?android:drawable\([^)]*\)=@(0x[0-9a-f]+)')
            $foreground = [regex]::Match($xml, '(?s)E: foreground.*?android:drawable\([^)]*\)=@(0x[0-9a-f]+)')
            if (-not $background.Success -or -not $foreground.Success) { throw 'Adaptive icon layers are missing.' }
            $backgroundId = $background.Groups[1].Value
            $foregroundId = $foreground.Groups[1].Value
            $xmlReports.Add([pscustomobject]@{
                Role = $Role; ResourceId = $Id; ArchivePath = $resource.Path
                BackgroundResourceId = $backgroundId; ForegroundResourceId = $foregroundId
            })
            foreach ($layer in @(Resolve-ResourceFiles $backgroundId)) {
                Test-IconPng 'adaptive-background' $backgroundId $layer $adaptiveSource
            }
            foreach ($layer in @(Resolve-ResourceFiles $foregroundId)) {
                Test-IconPng 'adaptive-foreground' $foregroundId $layer $null
            }
        }
        else { throw ('Unsupported launcher resource: ' + $resource.Path) }
    }
}

try {
    Test-LauncherResource $iconId 'legacy'
    $roundId = $null
    if ($roundMatch.Success) { $roundId = $roundMatch.Groups[1].Value }
    else {
        foreach ($entry in $resourceNames.GetEnumerator()) {
            if ($entry.Value -match ':mipmap/app_icon_round$') { $roundId = $entry.Key; break }
        }
    }
    if ($roundId) { Test-LauncherResource $roundId 'round' }
    if ($xmlReports.Count -eq 0) { $failures.Add('No adaptive icon XML was resolved from launcher resources.') }
    if ($pngReports.Count -lt 3) { $failures.Add('Too few launcher PNG resources were resolved.') }
    $report = [pscustomobject]@{
        Passed = ($failures.Count -eq 0)
        Apk = (Resolve-Path -LiteralPath $ApkPath).Path
        ApkSha256 = (Get-FileHash -LiteralPath $ApkPath -Algorithm SHA256).Hash
        ManifestIconResourceId = $iconId
        ManifestRoundIconDeclared = $roundMatch.Success
        RoundResourceId = $roundId
        RgbMeanAbsoluteErrorLimit = 12
        XmlResources = $xmlReports.ToArray()
        PngResources = $pngReports.ToArray()
        Failures = $failures.ToArray()
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $AuditDirectory 'result.json')
    if ($failures.Count -gt 0) { throw ($failures -join "`n") }
    Write-Output ("APK_ICON_PASS pngs=" + $pngReports.Count + " adaptiveXml=" + $xmlReports.Count)
}
finally { $zip.Dispose() }
