using Microsoft.Maui.Controls.Shapes;
using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public class LayoutRevealTests
{
    [Fact]
    public void The_clip_opens_from_the_old_size_and_clears_at_the_end()
    {
        var host = new LayoutMotionHost();
        host.ArmReveal(80, 40, 120, 100);
        host.Reveal = 0;

        var clip = Assert.IsType<RectangleGeometry>(host.Clip);
        Assert.Equal(80, clip.Rect.Width, 3);
        Assert.Equal(40, clip.Rect.Height, 3);

        host.Reveal = 0.5;
        Assert.Equal(100, clip.Rect.Width, 3);
        Assert.Equal(70, clip.Rect.Height, 3);

        host.Reveal = 1;
        Assert.Null(host.Clip);
        Assert.Equal(-1, host.HeightRequest);
        Assert.Equal(-1, host.WidthRequest);
    }

    [Fact]
    public void The_reveal_starts_on_the_old_size()
    {
        var host = new LayoutMotionHost();
        host.ArmReveal(80, 40, 160, 40);
        var recipe = FlipMotion.Reveal(
            Motion.None.FromPlace().WithSpring(Spring.Gentle),
            0,
            0,
            reveal: true);
        var player = MotionPlayer.Create(recipe!, [host], new MotionClock(manual: true));

        player.Forward();

        var clip = Assert.IsType<RectangleGeometry>(host.Clip);
        Assert.Equal(80, clip.Rect.Width, 3);
        Assert.Equal(40, clip.Rect.Height, 3);
        Assert.Equal(-1, host.HeightRequest);
    }

    [Fact]
    public void Reduce_motion_shows_the_new_size_when_the_change_is_recorded()
    {
        var host = new LayoutMotionHost();
        host.ArmReveal(80, 40, 160, 40);
        var recipe = FlipMotion.Reveal(
            Motion.None.FromPlace().WithDuration(400).WithEasing(Easing.Linear).WithReduceMotion(true),
            0,
            0,
            reveal: true);
        var player = MotionPlayer.Create(recipe!, [host], new MotionClock(manual: true));

        player.Forward();

        Assert.Null(host.Clip);
        Assert.Equal(1, host.Reveal, 3);
        Assert.Equal(-1, host.HeightRequest);
        Assert.Equal(-1, host.WidthRequest);
    }
}
