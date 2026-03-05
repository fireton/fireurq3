using Urql.Player.Compat.Skin;

namespace Urql.Core.Tests;

public sealed class CompatSkinProviderTests
{
    [Fact]
    public void LoadLegacyXml_ShouldUseDefaults_WhenScreenMissing()
    {
        var skinPath = WriteTempSkin(
            """
            <skin>
              <main>
                <textpane left="10" top="20" width="300" height="200" />
              </main>
            </skin>
            """);

        var provider = new SkinProvider(skinPath);
        var skin = provider.LoadLegacyXml(skinPath);

        Assert.Equal(800, skin.ScreenWidth);
        Assert.Equal(600, skin.ScreenHeight);
        Assert.False(skin.FullScreen);
        Assert.Equal(10, skin.TextPane.Left);
        Assert.Equal(20, skin.TextPane.Top);
    }

    [Fact]
    public void LoadLegacyXml_ShouldParseResourceCollectionsCaseInsensitive()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"compat-skin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var texturePath = Path.Combine(dir, "btn.png");
        File.WriteAllBytes(texturePath, []);
        var skinPath = Path.Combine(dir, "skin.xml");
        File.WriteAllText(skinPath,
            """
            <skin>
              <resources>
                <texture name="RES_BTN" file="btn.png" />
                <font name="MainFont" file="NotoSans-Regular.ttf[24]" />
              </resources>
            </skin>
            """);

        var provider = new SkinProvider(skinPath);
        var skin = provider.LoadLegacyXml(skinPath);

        Assert.True(skin.Textures.ContainsKey("res_btn"));
        Assert.True(skin.Fonts.ContainsKey("mainfont"));
    }

    private static string WriteTempSkin(string xml)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"compat-skin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "skin.xml");
        File.WriteAllText(path, xml);
        return path;
    }
}
