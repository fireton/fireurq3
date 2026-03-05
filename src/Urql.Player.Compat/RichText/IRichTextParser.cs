namespace Urql.Player.Compat.RichText;

public interface IRichTextParser
{
    RichTextDocument Parse(string rawText);
}
