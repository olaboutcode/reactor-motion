using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public class ScrubMapTests
{
    [Fact]
    public void Crossing_is_zero_at_the_far_edge_and_one_after_the_near_edge()
    {
        var viewport = new Rect(0, 100, 80, 200);

        Assert.Equal(0, ScrubMap.Crossing(new Rect(0, 300, 40, 40), viewport, vertical: true));
        Assert.Equal(1, ScrubMap.Crossing(new Rect(0, 60, 40, 40), viewport, vertical: true));
        Assert.Equal(0.5, ScrubMap.Crossing(new Rect(10, 180, 40, 40), viewport, vertical: true)!.Value, 3);
    }

    [Fact]
    public void Crossing_clamps_outside_the_travel()
    {
        var viewport = new Rect(0, 0, 100, 100);

        Assert.Equal(0, ScrubMap.Crossing(new Rect(0, 250, 20, 20), viewport, vertical: true));
        Assert.Equal(1, ScrubMap.Crossing(new Rect(0, -80, 20, 20), viewport, vertical: true));
        Assert.Null(ScrubMap.Crossing(new Rect(0, 0, 0, 20), viewport, vertical: true));
    }

    [Fact]
    public void Horizontal_crossing_uses_x()
    {
        var viewport = new Rect(40, 0, 200, 50);

        Assert.Equal(0, ScrubMap.Crossing(new Rect(240, 0, 30, 20), viewport, vertical: false));
        Assert.Equal(1, ScrubMap.Crossing(new Rect(10, 0, 30, 20), viewport, vertical: false));
    }

    [Fact]
    public void Range_maps_a_pixel_span_and_clamps()
    {
        Assert.Equal(0, ScrubMap.Range(0, 0, 120));
        Assert.Equal(0.5, ScrubMap.Range(60, 0, 120), 3);
        Assert.Equal(1, ScrubMap.Range(120, 0, 120));
        Assert.Equal(1, ScrubMap.Range(200, 0, 120));
        Assert.Equal(0, ScrubMap.Range(-10, 0, 120));
    }

    [Fact]
    public void Range_runs_backward_when_to_is_below_from()
    {
        Assert.Equal(0.5, ScrubMap.Range(60, 120, 0), 3);
        Assert.Equal(1, ScrubMap.Range(0, 0, 0));
        Assert.Equal(0, ScrubMap.Range(double.NaN, 0, 120));
    }

    [Fact]
    public void Tracks_vertical_unless_only_the_width_overflows()
    {
        Assert.True(ScrubMap.TracksVertical(100, 800, 100, 200));
        Assert.False(ScrubMap.TracksVertical(800, 100, 200, 100));
        Assert.True(ScrubMap.TracksVertical(800, 800, 200, 200));
        Assert.True(ScrubMap.TracksVertical(100, 100, 200, 200));
    }
}
