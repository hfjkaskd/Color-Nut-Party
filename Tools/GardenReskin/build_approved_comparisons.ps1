param([string]$ProjectRoot=(Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference='Stop'
$ProjectRoot=(Resolve-Path -LiteralPath $ProjectRoot).Path
$work=Join-Path $ProjectRoot 'Docs\GardenReskin\approved-replacement'
$out=Join-Path $work 'comparisons'
New-Item -ItemType Directory -Path $out -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$pages=@(
    @{key='diamond';preview='my-diamonds';title='My Diamonds / 钻石提现'},
    @{key='account';preview='enter-information';title='Enter Information / 账户信息'},
    @{key='cash';preview='level-withdrawal';title='Level Withdrawal / 关卡提现'},
    @{key='refresh';preview='refresh';title='Refresh / 道具领取'}
)
$manifest=@()
$font=[Drawing.Font]::new('Microsoft YaHei',24,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$label=[Drawing.Font]::new('Microsoft YaHei',20,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$small=[Drawing.Font]::new('Microsoft YaHei',15,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$ink=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(66,42,25))
foreach($page in $pages){
    $sources=@(
        (Join-Path $ProjectRoot ('Docs\GardenReskin\previews-clean-boundaries\'+$page.preview+'.png')),
        (Join-Path $ProjectRoot ('Docs\GardenReskin\three-way-comparison\comparison-current-'+$page.key+'-game.png')),
        (Join-Path $work ('runtime-us-tall\approved-skin-'+$page.key+'.png'))
    )
    $headings=@('已确认效果图（AI）','替换前游戏截图','替换后 Unity 运行截图')
    $subheadings=@('美术方向预览','历史截图，仅裁出游戏区域','Play Mode / 验证存档 / 720×1600')
    $canvas=[Drawing.Bitmap]::new(1572,1250)
    $g=[Drawing.Graphics]::FromImage($canvas)
    $g.Clear([Drawing.Color]::FromArgb(248,244,236))
    $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.DrawString($page.title,$font,$ink,22,14)
    $records=@()
    for($i=0;$i -lt 3;$i++){
        $x=22+$i*516
        $g.DrawString($headings[$i],$label,$ink,$x,58)
        $g.DrawString($subheadings[$i],$small,$ink,$x,86)
        $im=[Drawing.Image]::FromFile($sources[$i])
        $scale=[Math]::Min(500.0/$im.Width,1090.0/$im.Height)
        $w=[int][Math]::Round($im.Width*$scale);$h=[int][Math]::Round($im.Height*$scale)
        $rect=[Drawing.Rectangle]::new([int]($x+(500-$w)/2),[int](116+(1090-$h)/2),$w,$h)
        # Evidence assembly only: no retouch, recolour, painting, or content crop.
        $g.DrawImage($im,$rect)
        $records+=@{path=$sources[$i];sha256=(Get-FileHash -LiteralPath $sources[$i]).Hash;size=@($im.Width,$im.Height);label=$headings[$i]}
        $im.Dispose()
    }
    $g.DrawString('金额、关卡及屏幕比例按各自原始画面保留；右列为实际 Unity 渲染，未作修饰。',$small,$ink,22,1220)
    $dest=Join-Path $out ($page.key+'-three-way.png')
    $canvas.Save($dest,[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$canvas.Dispose()
    $manifest+=@{page=$page.key;comparison=$dest;sources=$records}
}
$font.Dispose();$label.Dispose();$small.Dispose();$ink.Dispose()
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'provenance.json') -Encoding utf8
Write-Output ('Created four labeled evidence comparisons in '+$out)
