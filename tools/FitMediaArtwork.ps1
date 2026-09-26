param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Destination
)
$ErrorActionPreference = 'Stop'
# Fit an already-extracted alpha PNG to the existing 104x72 media canvas.
# The right-hand 24 pixels are reserved for the status dot; menu arrow is
# outside this canvas. No colour keying or background removal is performed.
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class MediaArtworkFitter
{
    public static void Fit(string source, string destination)
    {
        using (var input = new Bitmap(source))
        {
            int left=input.Width, top=input.Height, right=-1, bottom=-1;
            for (int y=0; y<input.Height; y++)
            for (int x=0; x<input.Width; x++)
            {
                if (input.GetPixel(x,y).A < 8) continue;
                left=Math.Min(left,x); top=Math.Min(top,y);
                right=Math.Max(right,x); bottom=Math.Max(bottom,y);
            }
            if (right<left) throw new InvalidOperationException("Empty alpha image");
            if (input.GetPixel(0,0).A != 0)
                throw new InvalidOperationException("Expected transparent input background");
            float width=right-left+1, height=bottom-top+1;
            float scale=Math.Min(78f/width,64f/height);
            var target=new RectangleF(1+(78-width*scale)/2,
                4+(64-height*scale)/2,width*scale,height*scale);
            using (var output=new Bitmap(104,72,PixelFormat.Format32bppArgb))
            using (var graphics=Graphics.FromImage(output))
            using (var attributes=new ImageAttributes())
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                graphics.CompositingQuality=CompositingQuality.HighQuality;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(input,Rectangle.Round(target),left,top,width,height,
                    GraphicsUnit.Pixel,attributes);
                output.Save(destination,ImageFormat.Png);
            }
        }
    }
}
'@
[MediaArtworkFitter]::Fit($Source,$Destination)
