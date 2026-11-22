using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class FlipPlanTests
{
    [Fact]
    public void Same_place_allows_half_a_pixel()
    {
        var origin = new FlipFrame(0, 10, 40, 20);

        Assert.True(FlipPlan.SamePlace(origin, new FlipFrame(0, 10.49, 40, 20)));
        Assert.False(FlipPlan.SamePlace(origin, new FlipFrame(0, 10.5, 40, 20)));
    }

    [Fact]
    public void Invert_keeps_the_visual_when_a_slide_is_in_flight()
    {
        var origin = new FlipFrame(0, 40, 80, 56);
        var next = new FlipFrame(0, 80, 80, 56);

        var (x, y) = FlipPlan.Invert(origin, translationX: 0, translationY: -10, next);

        Assert.Equal(0, x);
        Assert.Equal(-50, y);
        Assert.Equal(40 - 10, next.Y + y);
    }

    [Theory]
    [InlineData(false, false, false, (int)FlipCue.Record)]
    [InlineData(true, false, false, (int)FlipCue.Enter)]
    [InlineData(false, true, true, (int)FlipCue.Rest)]
    [InlineData(true, true, true, (int)FlipCue.Rest)]
    [InlineData(true, true, false, (int)FlipCue.Move)]
    public void Cue_records_the_first_layout_and_enters_a_new_row(
        bool placed,
        bool known,
        bool samePlace,
        int cue)
    {
        Assert.Equal((FlipCue)cue, FlipPlan.Cue(placed, known, samePlace));
    }

    [Fact]
    public void Removed_keeps_order_and_one_copy_of_a_key()
    {
        var removed = FlipPlan.Removed([1, 1, 2, 3], [3]);

        Assert.Equal([1, 2], removed);
    }

    [Fact]
    public void Arrived_skips_keys_that_were_already_present()
    {
        var arrived = FlipPlan.Arrived([1], [1, 2, 2, 3]);

        Assert.Equal([2, 3], arrived);
    }

    [Fact]
    public void Boxed_ints_compare_equal()
    {
        object left = 4;
        object right = 4;

        Assert.True(FlipPlan.Equal(left, right));
    }
}
