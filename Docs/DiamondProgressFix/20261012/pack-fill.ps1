Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class ProgressSpritePack {
    public static string Pack(string source, string target, int width, int height, Rectangle destination) {
        using (var original = new Bitmap(source)) {
            int left=original.Width, top=original.Height, right=-1, bottom=-1;
            for(int y=0;y<original.Height;y++) for(int x=0;x<original.Width;x++) {
                if(original.GetPixel(x,y).A < 8) continue;
                left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
            }
            if(right<left) throw new Exception("Empty generated alpha");
            // Ignore near-invisible alpha noise only when measuring the export bounds.
            // Preserve all original RGBA pixels within these antialiased bounds.
            var crop = new Rectangle(left-1,top-1,right-left+3,bottom-top+3);
            using(var canvas = new Bitmap(width,height,PixelFormat.Format32bppArgb))
            using(var graphics = Graphics.FromImage(canvas)) {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode=CompositingMode.SourceCopy;
                graphics.CompositingQuality=CompositingQuality.HighQuality;
                graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                using(var attributes=new ImageAttributes()) {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    // Standard three-slice packing: scale caps uniformly, extend only
                    // the straight center. Filled Images keep their original UV crop.
                    int sourceCap=crop.Height;
                    int targetCap=destination.Height;
                    graphics.DrawImage(original,new Rectangle(destination.X,destination.Y,targetCap,destination.Height),
                        crop.X,crop.Y,sourceCap,crop.Height,GraphicsUnit.Pixel,attributes);
                    graphics.DrawImage(original,new Rectangle(destination.X+targetCap,destination.Y,destination.Width-2*targetCap,destination.Height),
                        crop.X+sourceCap,crop.Y,crop.Width-2*sourceCap,crop.Height,GraphicsUnit.Pixel,attributes);
                    graphics.DrawImage(original,new Rectangle(destination.Right-targetCap,destination.Y,targetCap,destination.Height),
                        crop.Right-sourceCap,crop.Y,sourceCap,crop.Height,GraphicsUnit.Pixel,attributes);
                }
                canvas.Save(target,ImageFormat.Png);
            }
            return source+" alphaBounds="+crop+" packed="+destination;
        }
    }
}
'@

[ProgressSpritePack]::Pack((Join-Path $PSScriptRoot '../../TopProgressFix/20261012/fill-generated.png'), (Join-Path $PSScriptRoot 'diamond-progress-fill.png'), 850, 65, [System.Drawing.Rectangle]::new(0,0,850,65))
