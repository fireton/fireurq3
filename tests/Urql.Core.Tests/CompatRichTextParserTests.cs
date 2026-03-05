using Urql.Player.Compat.RichText;

namespace Urql.Core.Tests;

public sealed class CompatRichTextParserTests
{
    [Fact]
    public void Parse_ShouldExtractLinkRun_WithExplicitTarget()
    {
        var parser = new LegacyRichTextParser();
        var doc = parser.Parse("Идти в [[лес|forest]].");

        var link = Assert.Single(doc.Runs.OfType<LinkRun>());
        Assert.Equal("лес", link.Text);
        Assert.Equal("forest", link.Target);
        Assert.False(link.IsLocal);
        Assert.False(link.IsMenu);
    }

    [Fact]
    public void Parse_ShouldExtractLocalAndMenuModifiers()
    {
        var parser = new LegacyRichTextParser();
        var doc = parser.Parse("[[кнопка|!local]] [[меню|%actions]]");
        var links = doc.Runs.OfType<LinkRun>().ToList();

        Assert.Equal(2, links.Count);
        Assert.True(links[0].IsLocal);
        Assert.Equal("local", links[0].Target);
        Assert.True(links[1].IsMenu);
        Assert.Equal("actions", links[1].Target);
    }
}
