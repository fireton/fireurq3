namespace Urql.Player.Compat.Viewport;

public readonly record struct ViewTransform(
    int VirtualWidth,
    int VirtualHeight,
    int ViewWidth,
    int ViewHeight,
    float Scale,
    float OffsetX,
    float OffsetY)
{
    public bool TryMapToVirtual(float viewX, float viewY, out float virtualX, out float virtualY)
    {
        if (Scale <= 0f)
        {
            virtualX = 0f;
            virtualY = 0f;
            return false;
        }

        var vx = (viewX - OffsetX) / Scale;
        var vy = (viewY - OffsetY) / Scale;
        var inside = vx >= 0f && vy >= 0f && vx <= VirtualWidth && vy <= VirtualHeight;
        virtualX = vx;
        virtualY = vy;
        return inside;
    }
}
