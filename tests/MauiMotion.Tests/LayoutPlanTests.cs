using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public class LayoutPlanTests
{
    [Fact]
    public void The_first_frame_is_recorded_and_does_not_play()
    {
        var step = LayoutPlan.Next(recorded: false, default, Frame(0, 40), 0, 0);

        Assert.True(step.Record);
        Assert.False(step.Play);
    }

    [Fact]
    public void An_unmeasured_frame_waits()
    {
        var step = LayoutPlan.Next(recorded: false, default, Frame(0, 0, width: -1, height: -1), 0, 0);

        Assert.False(step.Record);
        Assert.False(step.Play);
    }

    [Fact]
    public void A_size_change_records_the_new_frame_and_stays_put()
    {
        var step = LayoutPlan.Next(recorded: true, Frame(10, 20, 80, 40), Frame(10, 20, 120, 40), 0, 0);

        Assert.True(step.Record);
        Assert.False(step.Play);
    }

    [Fact]
    public void A_move_inverts_from_the_old_origin()
    {
        var step = LayoutPlan.Next(recorded: true, Frame(0, 10), Frame(0, 50), 0, 0);

        Assert.True(step.Play);
        Assert.Equal(0, step.X);
        Assert.Equal(-40, step.Y);
    }

    [Fact]
    public void A_move_keeps_the_translation_already_on_the_view()
    {
        var step = LayoutPlan.Next(recorded: true, Frame(0, 10), Frame(0, 50), 0, -15);

        Assert.Equal(-55, step.Y);
    }

    [Fact]
    public void A_shift_under_half_a_pixel_stays_put()
    {
        var step = LayoutPlan.Next(recorded: true, Frame(0, 10), Frame(0, 10.4), 0, 0);

        Assert.False(step.Play);
    }

    static FlipFrame Frame(double x, double y, double width = 80, double height = 40)
        => new(x, y, width, height);
}