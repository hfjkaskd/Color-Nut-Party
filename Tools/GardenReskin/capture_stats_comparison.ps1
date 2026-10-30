param([string]$ProjectRoot=(Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference='Stop'
$ProjectRoot=(Resolve-Path -LiteralPath $ProjectRoot).Path
$out=Join-Path $ProjectRoot 'Docs\GardenReskin\stats-display-area'
Add-Type -AssemblyName System.Drawing
$before=Join-Path $ProjectRoot 'Docs\GardenReskin\approved-replacement\runtime-us-tall\approved-skin-cash.png'
$after=Join-Path $out 'cash-after-unity.png'
$sources=@($before,$after)
for($i=0;$i -lt 2;$i++){
    $image=[Drawing.Bitmap]::new($sources[$i])
    if($image.Width -ne 720 -or $image.Height -ne 1600){throw 'Unexpected Unity capture dimensions'}
    $crop=$image.Clone([Drawing.Rectangle]::new(22,985,676,248),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $name=if($i -eq 0){'stats-before-unity.png'}else{'stats-after-unity.png'}
    $crop.Save((Join-Path $out $name),[Drawing.Imaging.ImageFormat]::Png)
    $crop.Dispose();$image.Dispose()
}
# The three full images remain untouched: scale only for evidence presentation.
$sources=@((Join-Path $ProjectRoot 'Docs\GardenReskin\previews-clean-boundaries\level-withdrawal.png'),$before,$after)
$titles=@('原已确认效果图（AI）','本次修改前 Unity 截图','本次修改后 Unity 截图')
$canvas=[Drawing.Bitmap]::new(1572,1250)
$g=[Drawing.Graphics]::FromImage($canvas)
$g.Clear([Drawing.Color]::FromArgb(248,244,236))
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$font=[Drawing.Font]::new('Microsoft YaHei',22,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$small=[Drawing.Font]::new('Microsoft YaHei',16,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
for($i=0;$i -lt 3;$i++){
    $x=22+$i*516
    $g.DrawString($titles[$i],$font,[Drawing.Brushes]::Black,$x,20)
    $im=[Drawing.Image]::FromFile($sources[$i])
    $scale=[Math]::Min(500.0/$im.Width,1140.0/$im.Height)
    $w=[int][Math]::Round($im.Width*$scale);$h=[int][Math]::Round($im.Height*$scale)
    $g.DrawImage($im,[Drawing.Rectangle]::new([int]($x+(500-$w)/2),[int](62+(1140-$h)/2),$w,$h))
    $im.Dispose()
}
$g.DrawString('本次扩大统计文字显示空间、去除三块小底框；中、右列为验证存档下的实际运行截图，未修饰。',$small,[Drawing.Brushes]::Black,22,1216)
$canvas.Save((Join-Path $out 'cash-three-way.png'),[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$small.Dispose();$g.Dispose();$canvas.Dispose()
