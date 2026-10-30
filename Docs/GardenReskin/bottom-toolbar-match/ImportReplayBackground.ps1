$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..')).Path
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem
$references = @('System.Drawing')
if ($PSVersionTable.PSEdition -eq 'Core') {
    $references += @('System.Drawing.Common', 'System.Drawing.Primitives', 'System.Runtime', 'System.Runtime.InteropServices', 'System.Collections')
    foreach ($assembly in @('System.Private.Windows.GdiPlus', 'System.Private.Windows.Core')) {
        if (Test-Path -LiteralPath (Join-Path $PSHOME ($assembly + '.dll'))) { $references += $assembly }
    }
}
Add-Type -Path (Join-Path $project 'Tools\GardenReskin\GeneratedSpriteImporter.cs') -ReferencedAssemblies $references
$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $project 'Docs\GardenReskin\OriginalAssets\before-reskin.zip'))
try {
    $entry = $archive.GetEntry('Assets/Texture2D/kuang.png')
    $stream = $entry.Open()
    $buffer = [IO.MemoryStream]::new()
    try { $stream.CopyTo($buffer); $template = $buffer.ToArray() }
    finally { $stream.Dispose(); $buffer.Dispose() }
} finally { $archive.Dispose() }
$source = Join-Path $project 'Docs\GardenReskin\Generated\replay-cream-disc.png'
$target = Join-Path $project 'Assets\Resources\atlas\mainpanel\replay_bg.png'
# Reuse the original 85x85 canvas/alpha bounds as dimensions only. All output
# colour and alpha come from ImageGen. No drawing or recolouring is performed.
$result = [GardenReskin.GeneratedSpriteImporter]::Build($template, $source, 'grid', 1, 1, 0, 100, 2, [double[]]@(0,0,0,0), [double[]]@(.12,.12,.12,.12), $false)
if ($result.OutputWidth -ne 85 -or $result.OutputHeight -ne 85) { throw 'Unexpected replay background dimensions.' }
[IO.File]::WriteAllBytes($target, $result.Png)
$report = [ordered]@{
    status = 'PASS'
    method = 'Mechanical generated-alpha crop and bicubic resize only'
    target = 'Assets/Resources/atlas/mainpanel/replay_bg.png'
    source = 'Docs/GardenReskin/Generated/replay-cream-disc.png'
    size = @(85,85)
    dimensionsTemplate = 'OriginalAssets/before-reskin.zip:Assets/Texture2D/kuang.png'
    originalSharedTextureModified = $false
    sourceBounds = $result.SourceBounds
    destinationBounds = $result.OriginalAlphaBounds
    outputSha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    sourceSha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
}
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'replay-background-import.json') -Encoding UTF8
Write-Output 'PASS: added 85x85 replay background; shared kuang texture unchanged.'
