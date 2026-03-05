namespace Urql.Player.Compat.Viewport;

public static class ViewportMapper
{
    public static ViewTransform ComputeLetterbox(int virtualWidth, int virtualHeight, int viewWidth, int viewHeight)
    {
        if (virtualWidth <= 0 || virtualHeight <= 0 || viewWidth <= 0 || viewHeight <= 0)
        {
            return new ViewTransform(virtualWidth, virtualHeight, viewWidth, viewHeight, 1f, 0f, 0f);
        }

        var sx = viewWidth / (float)virtualWidth;
        var sy = viewHeight / (float)virtualHeight;
        var scale = MathF.Min(sx, sy);

        var usedW = virtualWidth * scale;
        var usedH = virtualHeight * scale;
        var ox = (viewWidth - usedW) * 0.5f;
        var oy = (viewHeight - usedH) * 0.5f;
        return new ViewTransform(virtualWidth, virtualHeight, viewWidth, viewHeight, scale, ox, oy);
    }
}
