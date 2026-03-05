namespace Urql.Player.Compat.Skin;

public sealed class SkinDefinition
{
    public int ScreenWidth { get; set; } = 800;
    public int ScreenHeight { get; set; } = 600;
    public bool FullScreen { get; set; }

    public Dictionary<string, SkinTexture> Textures { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SkinFont> Fonts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SkinButtonFrame> ButtonFrames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SkinTextPane TextPane { get; set; } = new();
    public SkinMenuStyle MenuStyle { get; set; } = new();
}

public sealed record SkinTexture(string Name, string FilePath);

public sealed record SkinFont(string Name, string FileSpec);

public sealed record SkinButtonFrame(
    string Name,
    string TextureName,
    string FontName,
    int TextureX,
    int TextureY,
    int Width,
    int Height,
    int LeftWidth,
    int MiddleWidth);

public sealed class SkinTextPane
{
    public int Left { get; set; } = 20;
    public int Top { get; set; } = 55;
    public int Width { get; set; } = 760;
    public int Height { get; set; } = 525;
    public string? ButtonFrameName { get; set; }
    public int ButtonAlign { get; set; } = 1;
}

public sealed class SkinMenuStyle
{
    public uint BackgroundColor { get; set; } = 0xF0101010;
    public uint BorderColor { get; set; } = 0xFF303030;
    public uint TextColor { get; set; } = 0xFFE0E0E0;
    public uint SelectionColor { get; set; } = 0xFF3A3A3A;
    public uint SelectedTextColor { get; set; } = 0xFFFFFFFF;
    public uint DisabledColor { get; set; } = 0xFF7A7A7A;
}
