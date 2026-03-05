namespace Urql.Player.Compat.RichText;

public abstract record RichRun(string Text);

public sealed record TextRun(string Text) : RichRun(Text);

public sealed record LinkRun(string Text, string Target, bool IsMenu, bool IsLocal) : RichRun(Text);

public sealed record ImagePlaceholderRun(string Text, string ImageName) : RichRun(Text);

public sealed record RichTextDocument(IReadOnlyList<RichRun> Runs);
