using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Urql.Runner.MonoGame.Input;

public static class ViewportInputMapper
{
    public static float GetMouseViewX(GameWindow window, GraphicsDevice graphicsDevice, int mouseX)
    {
        var bounds = window.ClientBounds;
        if (bounds.Width <= 0)
        {
            return mouseX;
        }

        return mouseX * (graphicsDevice.Viewport.Width / (float)bounds.Width);
    }

    public static float GetMouseViewY(GameWindow window, GraphicsDevice graphicsDevice, int mouseY)
    {
        var bounds = window.ClientBounds;
        if (bounds.Height <= 0)
        {
            return mouseY;
        }

        return mouseY * (graphicsDevice.Viewport.Height / (float)bounds.Height);
    }
}
