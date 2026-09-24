using SkiaSharp;

var logoPath = @"C:\Users\User\Pictures\lumo\src\Lumo.Engine\Assets\lumologo.png";
var iconPath = @"C:\Users\User\Pictures\lumo\src\Lumo.Editor\Assets\lumo_icon.png";

using var original = SKBitmap.Decode(logoPath);
using var output = new SKBitmap(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul);

// Sample background color from corners
SKColor bg1 = original.GetPixel(0, 0);
SKColor bg2 = original.GetPixel(original.Width - 1, 0);
SKColor bgColor = original.GetPixel(original.Width / 2, original.Height / 2);

// Remove background using color-distance approach
for (int y = 0; y < original.Height; y++)
{
    for (int x = 0; x < original.Width; x++)
    {
        var pixel = original.GetPixel(x, y);
        float dist = (float)System.Math.Sqrt(
            System.Math.Pow(pixel.R - bgColor.R, 2) +
            System.Math.Pow(pixel.G - bgColor.G, 2) +
            System.Math.Pow(pixel.B - bgColor.B, 2));
        
        if (dist > 30)
        {
            // Scale to 64x64
            int sx = x * 64 / original.Width;
            int sy = y * 64 / original.Height;
            output.SetPixel(sx, sy, pixel);
        }
    }
}

// Add a nice background circle
using var paint = new SKPaint
{
    Color = new SKColor(0, 80, 180),
    IsAntialias = true,
};
using var surface = SKSurface.Create(new SKImageInfo(64, 64));
surface.Canvas.DrawCircle(32, 32, 30, paint);
surface.Canvas.DrawBitmap(output, 0, 0);

using var image = SKImage.FromBitmap(surface.Snapshot());
using var data = image.Encode(SKEncodedImageFormat.Png, 100);
using var stream = System.IO.File.Create(iconPath);
data.SaveTo(stream);

Console.WriteLine("Icon created at: " + iconPath);
