using Urql.Player.Compat.RichText;
using Urql.Player.Compat.Session;
using Urql.Player.Compat.Skin;

namespace Urql.Core.Tests;

public sealed class PlayerSessionSkinFallbackTests
{
    [Fact]
    public void Load_ShouldUseBuiltInDefaultSkin_WhenQuestSkinMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"player-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        var questPath = Path.Combine(dir, "game.qst");
        File.WriteAllText(questPath,
            """
            :start
            pln Привет
            end
            """);

        var builtInSkinPath = Path.Combine(dir, "default-skin.xml");
        File.WriteAllText(builtInSkinPath,
            """
            <skin>
              <screen width="800" height="600" />
            </skin>
            """);

        var session = new PlayerSession(new SkinProvider(builtInSkinPath), new LegacyRichTextParser());
        session.Load(new PlayerSessionConfig(questPath));
        var frame = session.Advance(1200, 900);

        Assert.Equal(800, frame.VirtualWidth);
        Assert.Equal(600, frame.VirtualHeight);
        Assert.NotNull(frame.Skin);
    }
}
