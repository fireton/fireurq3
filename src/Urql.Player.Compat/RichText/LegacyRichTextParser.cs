namespace Urql.Player.Compat.RichText;

public sealed class LegacyRichTextParser : IRichTextParser
{
    public RichTextDocument Parse(string rawText)
    {
        var text = rawText ?? string.Empty;
        var runs = new List<RichRun>();
        var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf("[[", i, StringComparison.Ordinal);
            if (open < 0)
            {
                AppendText(runs, text[i..]);
                break;
            }

            if (open > i)
            {
                AppendText(runs, text[i..open]);
            }

            var close = text.IndexOf("]]", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                AppendText(runs, text[open..]);
                break;
            }

            var body = text[(open + 2)..close];
            var pipe = body.IndexOf('|');
            string caption;
            string target;
            if (pipe >= 0)
            {
                caption = body[..pipe].Trim();
                target = body[(pipe + 1)..].Trim();
            }
            else
            {
                caption = body.Trim();
                target = body.Trim();
            }

            var isMenu = target.StartsWith('%');
            var isLocal = target.StartsWith('!');
            if (isMenu || isLocal)
            {
                target = target[1..].Trim();
            }

            if (string.IsNullOrWhiteSpace(caption))
            {
                caption = target;
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                AppendText(runs, text[open..(close + 2)]);
            }
            else
            {
                runs.Add(new LinkRun(caption, target, isMenu, isLocal));
            }

            i = close + 2;
        }

        return new RichTextDocument(runs);
    }

    private static void AppendText(ICollection<RichRun> runs, string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            runs.Add(new TextRun(text));
        }
    }
}
