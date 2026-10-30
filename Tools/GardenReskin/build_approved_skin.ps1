param([string]$ProjectRoot = (Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$work = Join-Path $ProjectRoot 'Docs\GardenReskin\approved-replacement'
$target = Join-Path $ProjectRoot 'Assets\Resources\atlas\approvedgarden'
New-Item -ItemType Directory -Path $target -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$refs = @('System.Drawing','System.Drawing.Common','System.Drawing.Primitives','System.Runtime','System.Runtime.InteropServices','System.Collections')
foreach($a in @('System.Private.Windows.GdiPlus','System.Private.Windows.Core')) { if(Test-Path -LiteralPath (Join-Path $PSHOME ($a+'.dll'))) { $refs += $a } }
Add-Type -Path (Join-Path $PSScriptRoot 'GeneratedSpriteImporter.cs') -ReferencedAssemblies $refs
$config = Get-Content -LiteralPath (Join-Path $work 'asset-build.json') -Raw | ConvertFrom-Json
$report = @()
foreach($e in $config) {
    # This temporary rectangle defines destination dimensions only. Every output
    # pixel comes from ImageGen art through the existing mechanical importer.
    $canvas = [Drawing.Bitmap]::new([int]$e.width,[int]$e.height)
    $g = [Drawing.Graphics]::FromImage($canvas)
    $g.Clear([Drawing.Color]::Transparent)
    $inset = [int]$e.inset
    $g.FillRectangle([Drawing.Brushes]::White,$inset,0,[int]$e.width-2*$inset,[int]$e.height)
    $stream = [IO.MemoryStream]::new()
    $canvas.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $canvas.Dispose()
    $source = Join-Path $work ('sources\'+$e.source+'.png')
    $built = [GardenReskin.GeneratedSpriteImporter]::Build($stream.ToArray(),$source,'grid',[int]$e.columns,[int]$e.rows,[int]$e.cell,160,2,[double[]]$e.border,[double[]]$e.ratios,$false)
    $stream.Dispose()
    $out = Join-Path $target ($e.name+'.png')
    [IO.File]::WriteAllBytes($out,$built.Png)
    $report += @{name=$e.name;source=$source;sourceBounds=$built.SourceBounds;size=@($built.OutputWidth,$built.OutputHeight);slices=$built.DestinationSlicePixels;sha256=(Get-FileHash -LiteralPath $out).Hash}
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $work 'asset-build-report.json') -Encoding utf8
Write-Output ('Built '+$report.Count+' text-free sprites from approved-style ImageGen sources.')
