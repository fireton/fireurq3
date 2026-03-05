using FontStashSharp;
using Urql.Player.Compat.Session;

namespace Urql.Runner.MonoGame.Layout;

public static class InteractionHitMapBuilder
{
    public static (List<ChoiceHitArea> choices, List<LinkHitArea> links) Build(
        PlayerFrame frame,
        DynamicSpriteFont font,
        IReadOnlyList<RenderLine> lines,
        float padding,
        float lineSpacing)
    {
        var choiceHitAreas = new List<ChoiceHitArea>();
        var linkHitAreas = new List<LinkHitArea>();

        var lineHeight = font.LineHeight + lineSpacing;
        var scroll = TranscriptLayoutEngine.ComputeScroll(frame.VirtualHeight, lines.Count, font, padding, lineSpacing);
        var y = padding - scroll;
        foreach (var line in lines)
        {
            if (y + lineHeight >= 0 && y <= frame.VirtualHeight)
            {
                var width = font.MeasureString(line.Text).X;
                if (line.ButtonId != 0)
                {
                    choiceHitAreas.Add(new ChoiceHitArea(line.ButtonId, new FloatRect(padding, y, width, lineHeight)));
                }

                if (line.Link is not null)
                {
                    linkHitAreas.Add(new LinkHitArea(line.Link, new FloatRect(padding, y, width, lineHeight)));
                }
            }

            y += lineHeight;
        }

        return (choiceHitAreas, linkHitAreas);
    }
}
