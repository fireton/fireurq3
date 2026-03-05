using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace Urql.Player.Compat.Skin;

public sealed class SkinProvider(string builtInSkinPath) : ISkinProvider
{
    public SkinDefinition LoadLegacyXml(string path)
    {
        var skin = new SkinDefinition();
        if (!File.Exists(path))
        {
            return skin;
        }

        var doc = XDocument.Load(path);
        var root = doc.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "skin", StringComparison.OrdinalIgnoreCase))
        {
            return skin;
        }

        var baseDir = Path.GetDirectoryName(path) ?? string.Empty;
        var screen = root.Element("screen");
        if (screen is not null)
        {
            skin.ScreenWidth = GetInt(screen, "width", 800);
            skin.ScreenHeight = GetInt(screen, "height", 600);
            skin.FullScreen = GetBool(screen, "fullscreen", false);
        }

        var resources = root.Element("resources");
        if (resources is not null)
        {
            foreach (var tex in resources.Elements("texture"))
            {
                var name = GetString(tex, "name");
                var file = GetString(tex, "file");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                var resolved = Path.GetFullPath(Path.Combine(baseDir, file));
                skin.Textures[name] = new SkinTexture(name, resolved);
            }

            foreach (var font in resources.Elements("font"))
            {
                var name = GetString(font, "name");
                var file = GetString(font, "file");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                skin.Fonts[name] = new SkinFont(name, file);
            }

            foreach (var frame in resources.Elements("buttonframe"))
            {
                var name = GetString(frame, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                skin.ButtonFrames[name] = new SkinButtonFrame(
                    Name: name,
                    TextureName: GetString(frame, "tex") ?? string.Empty,
                    FontName: GetString(frame, "font") ?? string.Empty,
                    TextureX: GetInt(frame, "texx", 0),
                    TextureY: GetInt(frame, "texy", 0),
                    Width: GetInt(frame, "width", 0),
                    Height: GetInt(frame, "height", 0),
                    LeftWidth: GetInt(frame, "leftw", 0),
                    MiddleWidth: GetInt(frame, "midw", 0));
            }
        }

        var main = root.Element("main");
        if (main is not null)
        {
            var textPane = main.Element("textpane");
            if (textPane is not null)
            {
                skin.TextPane.Left = GetInt(textPane, "left", skin.TextPane.Left);
                skin.TextPane.Top = GetInt(textPane, "top", skin.TextPane.Top);
                skin.TextPane.Width = GetInt(textPane, "width", skin.TextPane.Width);
                skin.TextPane.Height = GetInt(textPane, "height", skin.TextPane.Height);
                skin.TextPane.ButtonFrameName = GetString(textPane, "bframe");
                skin.TextPane.ButtonAlign = GetInt(textPane, "btnalign", skin.TextPane.ButtonAlign);
            }

            var menus = main.Element("menus");
            if (menus is not null)
            {
                skin.MenuStyle.SelectionColor = GetHexColor(menus, "selectioncolor", skin.MenuStyle.SelectionColor);
            }
        }

        return skin;
    }

    public SkinDefinition LoadJson(string path)
    {
        if (!File.Exists(path))
        {
            return new SkinDefinition();
        }

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<JsonSkinDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (dto is null)
        {
            return new SkinDefinition();
        }

        var skin = new SkinDefinition
        {
            ScreenWidth = dto.Screen?.Width ?? 800,
            ScreenHeight = dto.Screen?.Height ?? 600,
            FullScreen = dto.Screen?.Fullscreen ?? false
        };
        if (dto.TextPane is not null)
        {
            skin.TextPane.Left = dto.TextPane.Left;
            skin.TextPane.Top = dto.TextPane.Top;
            skin.TextPane.Width = dto.TextPane.Width;
            skin.TextPane.Height = dto.TextPane.Height;
            skin.TextPane.ButtonAlign = dto.TextPane.ButtonAlign;
        }

        return skin;
    }

    public SkinDefinition LoadBuiltInDefault()
    {
        return LoadLegacyXml(builtInSkinPath);
    }

    private static int GetInt(XElement node, string name, int fallback)
    {
        var raw = GetString(node, name);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static bool GetBool(XElement node, string name, bool fallback)
    {
        var raw = GetString(node, name);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }

    private static string? GetString(XElement node, string name)
    {
        return node.Attribute(name)?.Value;
    }

    private static uint GetHexColor(XElement node, string name, uint fallback)
    {
        var raw = GetString(node, name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        raw = raw.Trim();
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[2..];
        }

        return uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private sealed class JsonSkinDto
    {
        public JsonScreenDto? Screen { get; set; }
        public JsonTextPaneDto? TextPane { get; set; }
    }

    private sealed class JsonScreenDto
    {
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public bool Fullscreen { get; set; }
    }

    private sealed class JsonTextPaneDto
    {
        public int Left { get; set; } = 20;
        public int Top { get; set; } = 55;
        public int Width { get; set; } = 760;
        public int Height { get; set; } = 525;
        public int ButtonAlign { get; set; } = 1;
    }
}
