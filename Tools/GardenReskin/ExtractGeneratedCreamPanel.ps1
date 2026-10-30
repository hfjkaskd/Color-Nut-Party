param()
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
Add-Type -AssemblyName System.Drawing
$sourcePath = Join-Path $projectRoot 'Docs/GardenReskin/Generated/core-sheet.png'
$targetPath = Join-Path $projectRoot 'Docs/GardenReskin/Generated/cream-panel.png'
$source = [Drawing.Bitmap]::new($sourcePath)
try {
    # The generated first sprite extends beyond the nominal 313px cell edge.
    # Exact source-pixel extraction only: no painting, recolouring or scaling.
    $crop = $source.Clone([Drawing.Rectangle]::new(0, 0, 342, 313), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try { $crop.Save($targetPath, [Drawing.Imaging.ImageFormat]::Png) }
    finally { $crop.Dispose() }
} finally { $source.Dispose() }
Write-Output $targetPath
