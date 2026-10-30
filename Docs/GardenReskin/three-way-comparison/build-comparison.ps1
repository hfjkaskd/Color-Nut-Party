Add-Type -AssemblyName System.Drawing
$base = Split-Path -Parent $PSScriptRoot
$out = Join-Path $PSScriptRoot 'output'
New-Item -ItemType Directory -Force $out | Out-Null
$items = @(
  @{Key='refresh'; Title='Refresh tool offer'; Ref='C:\Users\pc\AppData\Local\Temp\codex-clipboard-cb321653-b0c0-4e85-9724-18c13fe7051e.png'; Before=(Join-Path $base '..\\RewardEvidence\local-tool-offer-refresh.png'); After=(Join-Path $base 'three-way-comparison\comparison-current-refresh-game.png')},
  @{Key='cash'; Title='Level withdrawal'; Ref='C:\Users\pc\AppData\Local\Temp\codex-clipboard-e367790a-8c10-4651-9ae9-a2a8025ecab2.png'; Before=(Join-Path $base '..\\RewardEvidence\local-withdrawal-cash-BR.png'); After=(Join-Path $base 'three-way-comparison\comparison-current-cash-game.png')},
  @{Key='diamond'; Title='My Diamonds'; Ref='C:\Users\pc\AppData\Local\Temp\codex-clipboard-616d3a3f-46e1-46b1-b32e-807f2aa7a2c2.png'; Before=(Join-Path $base '..\\RewardEvidence\local-withdrawal-coin-BR.png'); After=(Join-Path $base 'three-way-comparison\comparison-current-diamond-game.png')},
  @{Key='account'; Title='Enter Information'; Ref='C:\Users\pc\AppData\Local\Temp\codex-clipboard-1ca37cce-2b87-47cc-bc67-8eea14260455.png'; Before=(Join-Path $base '..\\RewardEvidence\local-withdrawal-account-BR.png'); After=(Join-Path $base 'three-way-comparison\comparison-current-account-game.png')}
)
function FitImage($src,$dst,$x,$y,$w,$h) {
  $ratio=[Math]::Min($w/$src.Width,$h/$src.Height)
  $dw=[int]($src.Width*$ratio); $dh=[int]($src.Height*$ratio)
  $dx=$x+[int](($w-$dw)/2); $dy=$y+[int](($h-$dh)/2)
  $dst.DrawImage($src,(New-Object System.Drawing.Rectangle($dx,$dy,$dw,$dh)))
}
$fontTitle=New-Object System.Drawing.Font('Arial',24,[System.Drawing.FontStyle]::Bold)
$fontLabel=New-Object System.Drawing.Font('Arial',17,[System.Drawing.FontStyle]::Bold)
$fontNote=New-Object System.Drawing.Font('Arial',12,[System.Drawing.FontStyle]::Regular)
$brushTitle=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(88,55,30))
$brushLabel=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80,49,29))
$brushNote=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(110,76,49))
$labels=@('Reference preview','Before reskin','Unity after reskin')
$rows=@()
foreach($it in $items){
  if(!(Test-Path $it.Ref) -or !(Test-Path $it.Before) -or !(Test-Path $it.After)){ throw "Missing input for $($it.Key)" }
  $ref=[System.Drawing.Image]::FromFile($it.Ref); $before=[System.Drawing.Image]::FromFile($it.Before); $after=[System.Drawing.Image]::FromFile($it.After)
  $W=1180; $H=820; $cellW=370; $imgH=680; $gap=20
  $bmp=New-Object System.Drawing.Bitmap($W,$H)
  $g=[System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::FromArgb(246,235,216)); $g.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $g.DrawString($it.Title,$fontTitle,$brushTitle,[System.Drawing.PointF]::new(28,18))
  for($i=0;$i -lt 3;$i++){
    $x=20+$i*($cellW+$gap)
    $g.FillRectangle([System.Drawing.Brushes]::White,$x,70,$cellW,$imgH)
    $g.DrawRectangle((New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(188,137,83),2)),$x,70,$cellW,$imgH)
    $g.DrawString($labels[$i],$fontLabel,$brushLabel,[System.Drawing.PointF]::new($x+10,78))
  }
  FitImage $ref $g 20 112 $cellW 625; FitImage $before $g (20+$cellW+$gap) 112 $cellW 625; FitImage $after $g (20+2*($cellW+$gap)) 112 $cellW 625
  $g.DrawString('Reference | Before | Unity after',$fontNote,$brushNote,[System.Drawing.PointF]::new(28,775))
  $path=Join-Path $out ($it.Key+'-three-way.png'); $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
  $rows += [ordered]@{key=$it.Key;title=$it.Title;reference=$it.Ref;before=$it.Before;after=$it.After;output=$path}
  $g.Dispose();$bmp.Dispose();$ref.Dispose();$before.Dispose();$after.Dispose()
}
[ordered]@{generatedAt=(Get-Date).ToString('o');labels=$labels;comparisons=$rows} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $PSScriptRoot 'comparison-manifest.json') -Encoding UTF8



