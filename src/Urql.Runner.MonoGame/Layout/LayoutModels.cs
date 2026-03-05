using Microsoft.Xna.Framework;
using Urql.Player.Compat.RichText;

namespace Urql.Runner.MonoGame.Layout;

public sealed record RenderLine(string Text, Color Color, int ButtonId, LinkRun? Link);

public readonly record struct FloatRect(float X, float Y, float Width, float Height)
{
    public bool Contains(float x, float y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
}

public readonly record struct ChoiceHitArea(int ButtonId, FloatRect Bounds);

public readonly record struct LinkHitArea(LinkRun? Link, FloatRect Bounds);
