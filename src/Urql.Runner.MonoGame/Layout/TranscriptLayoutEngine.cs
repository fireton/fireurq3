using FontStashSharp;
using Microsoft.Xna.Framework;
using Urql.Player.Compat.RichText;
using Urql.Player.Compat.Session;

namespace Urql.Runner.MonoGame.Layout;

public static class TranscriptLayoutEngine
{
    public static List<RenderLine> BuildRenderLines(PlayerFrame frame, DynamicSpriteFont font, int selectedChoiceIndex, float padding)
    {
        var lines = new List<RenderLine>();
        var maxWidth = frame.VirtualWidth - (padding * 2f);

        foreach (var run in frame.TextRuns)
        {
            var color = run switch
            {
                LinkRun => new Color(111, 137, 252),
                _ => new Color(226, 232, 240)
            };

            var link = run as LinkRun;
            foreach (var wrapped in WrapText(run.Text, maxWidth, font))
            {
                lines.Add(new RenderLine(wrapped, color, 0, link));
            }
        }

        if (frame.Menus.Count > 0)
        {
            lines.Add(new RenderLine(string.Empty, Color.White, 0, null));
            foreach (var menu in frame.Menus)
            {
                lines.Add(new RenderLine($"[menu] {menu.Name}", new Color(255, 179, 102), 0, null));
                foreach (var entry in menu.Entries)
                {
                    lines.Add(new RenderLine($"  - {entry}", new Color(191, 201, 216), 0, null));
                }
            }
        }

        if (frame.Buttons.Count > 0)
        {
            lines.Add(new RenderLine(string.Empty, Color.White, 0, null));
            for (var i = 0; i < frame.Buttons.Count; i++)
            {
                var btn = frame.Buttons[i];
                var selected = i == selectedChoiceIndex;
                var prefix = selected ? "> " : "  ";
                var color = selected ? new Color(255, 221, 89) : new Color(147, 197, 114);
                lines.Add(new RenderLine($"{prefix}{btn.Caption}", color, btn.Id, null));
            }
        }

        return lines;
    }

    public static float ComputeScroll(int virtualHeight, int lineCount, DynamicSpriteFont font, float padding, float lineSpacing)
    {
        var lineHeight = font.LineHeight + lineSpacing;
        var contentHeight = lineCount * lineHeight + (padding * 2f);
        return MathF.Max(0f, contentHeight - virtualHeight);
    }

    private static IEnumerable<string> WrapText(string text, float maxWidth, DynamicSpriteFont font)
    {
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var paragraphs = normalized.Split('\n');
        for (var p = 0; p < paragraphs.Length; p++)
        {
            var paragraph = paragraphs[p];
            if (paragraph.Length == 0)
            {
                yield return string.Empty;
            }
            else
            {
                var remaining = paragraph;
                while (remaining.Length > 0)
                {
                    var take = FindLineLengthThatFits(remaining, maxWidth, font);
                    var line = remaining[..take].TrimEnd();
                    yield return line;
                    remaining = remaining[take..].TrimStart();
                }
            }

            if (p < paragraphs.Length - 1)
            {
                yield return string.Empty;
            }
        }
    }

    private static int FindLineLengthThatFits(string text, float maxWidth, DynamicSpriteFont font)
    {
        if (font.MeasureString(text).X <= maxWidth)
        {
            return text.Length;
        }

        var low = 1;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (font.MeasureString(text[..mid]).X <= maxWidth)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        var length = Math.Max(1, low);
        if (length < text.Length)
        {
            var space = text[..length].LastIndexOf(' ');
            if (space > 0)
            {
                return space;
            }
        }

        return length;
    }
}
