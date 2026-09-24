using Lumo.Tools;

string basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string logoPath = Path.Combine(basePath, "lumologo.png");
string outputDir = Path.Combine(basePath, "src", "Lumo.Editor", "Assets");

if (!File.Exists(logoPath))
{
    Console.WriteLine($"Logo not found at: {logoPath}");
    return;
}

Directory.CreateDirectory(outputDir);

// 1. Remove background -> transparent PNG
string transparentPath = Path.Combine(outputDir, "lumo_logo_transparent.png");
LogoProcessor.RemoveBackground(logoPath, transparentPath);

// 2. Create icon (64x64) for window title bar
string iconPath = Path.Combine(outputDir, "lumo_icon.png");
LogoProcessor.CreateIcon(transparentPath, iconPath, 64);

// 3. Create splash (512x512)
string splashPath = Path.Combine(outputDir, "lumo_splash.png");
LogoProcessor.Resize(transparentPath, splashPath, 512);

Console.WriteLine("\nDone! All assets generated.");
