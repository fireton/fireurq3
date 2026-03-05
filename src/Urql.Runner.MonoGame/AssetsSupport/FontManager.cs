namespace Urql.Runner.MonoGame.AssetsSupport;

public static class FontManager
{
    public static string ResolveFontPath()
    {
        var candidates = new List<string>();
        var bundledDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");
        candidates.Add(Path.Combine(bundledDir, "NotoSans-Regular.ttf"));
        candidates.Add(Path.Combine(bundledDir, "NotoSans-Bold.ttf"));
        candidates.Add(Path.Combine(bundledDir, "NotoSansMono-Regular.ttf"));

        if (OperatingSystem.IsWindows())
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            candidates.Add(Path.Combine(windows, "Fonts", "arial.ttf"));
            candidates.Add(Path.Combine(windows, "Fonts", "segoeui.ttf"));
        }

        if (OperatingSystem.IsMacOS())
        {
            candidates.Add("/System/Library/Fonts/Supplemental/Arial.ttf");
            candidates.Add("/System/Library/Fonts/Supplemental/Arial Unicode.ttf");
        }

        if (OperatingSystem.IsLinux())
        {
            candidates.Add("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
            candidates.Add("/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf");
        }

        var resolved = candidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new FileNotFoundException("No usable TTF font was found on this system.");
        }

        return resolved;
    }
}
