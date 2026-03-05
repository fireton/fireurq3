using Urql.Player.Compat.Viewport;

namespace Urql.Core.Tests;

public sealed class CompatViewportMapperTests
{
    [Fact]
    public void ComputeLetterbox_ShouldPreserveAspectAndCenter()
    {
        var transform = ViewportMapper.ComputeLetterbox(800, 600, 1920, 1080);

        Assert.Equal(1.8f, transform.Scale, 3);
        Assert.Equal(240f, transform.OffsetX, 3);
        Assert.Equal(0f, transform.OffsetY, 3);
    }

    [Fact]
    public void TryMapToVirtual_ShouldRejectLetterboxBars()
    {
        var transform = ViewportMapper.ComputeLetterbox(800, 600, 1920, 1080);

        var inside = transform.TryMapToVirtual(300, 100, out var vx, out var vy);
        Assert.True(inside);
        Assert.InRange(vx, 0, 800);
        Assert.InRange(vy, 0, 600);

        var outside = transform.TryMapToVirtual(10, 100, out _, out _);
        Assert.False(outside);
    }
}
