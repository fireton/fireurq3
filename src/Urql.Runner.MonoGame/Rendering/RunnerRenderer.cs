using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Urql.Player.Compat.Session;
using Urql.Runner.MonoGame.Layout;

namespace Urql.Runner.MonoGame.Rendering;

public sealed class RunnerRenderer
{
    private readonly DynamicSpriteFont _font;
    private readonly Texture2D _pixel;

    public RunnerRenderer(DynamicSpriteFont font, Texture2D pixel)
    {
        _font = font;
        _pixel = pixel;
    }

    public void DrawFatal(SpriteBatch spriteBatch, string message, float padding)
    {
        spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        spriteBatch.DrawString(_font, message, new Vector2(padding, padding), Color.OrangeRed);
        spriteBatch.End();
    }

    public void DrawVirtualFrame(SpriteBatch spriteBatch, PlayerFrame frame, IReadOnlyList<RenderLine> lines, float padding, float lineSpacing)
    {
        var transform = Matrix.CreateTranslation(frame.ViewTransform.OffsetX, frame.ViewTransform.OffsetY, 0f) *
                        Matrix.CreateScale(frame.ViewTransform.Scale, frame.ViewTransform.Scale, 1f);

        spriteBatch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transform);
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, frame.VirtualWidth, frame.VirtualHeight), new Color(14, 17, 23));

        var lineHeight = _font.LineHeight + lineSpacing;
        var scroll = TranscriptLayoutEngine.ComputeScroll(frame.VirtualHeight, lines.Count, _font, padding, lineSpacing);
        var y = padding - scroll;
        foreach (var line in lines)
        {
            if (y + lineHeight >= 0 && y <= frame.VirtualHeight)
            {
                spriteBatch.DrawString(_font, line.Text, new Vector2(padding, y), line.Color);
            }

            y += lineHeight;
        }

        spriteBatch.End();
    }
}
