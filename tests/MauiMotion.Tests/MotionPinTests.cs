using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionPinTests
{
    [Fact]
    public void Pinned_target_skips_all_writes_until_unpin()
    {
        var box = new BoxView { Opacity = 1 };
        var clock = new MotionClock(manual: true);
        var player = MotionPlayer.Create(
            Motion.None.FadeIn().WithDuration(300).WithEasing(Easing.Linear),
            [box],
            clock);

        MotionPins.Pin([box]);
        _ = player.ForwardAsync();
        Assert.Equal(1, box.Opacity);
        clock.Tick(150);
        Assert.Equal(1, box.Opacity);

        MotionPins.Unpin();
        clock.Tick(1);
        Assert.True(box.Opacity < 1);

        MotionPins.Unpin();
        player.Dispose();
    }

    [Fact]
    public void Pinned_matrix_writes_the_same_pose_after_unpin()
    {
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var player = MotionPlayer.Create(
            Motion.None
                .Transform(_ => Matrix4.Identity.Translate(15, 0))
                .WithDuration(1000)
                .WithEasing(Easing.Linear),
            [box],
            clock);

        _ = player.ForwardAsync();
        Assert.Equal(15, box.TranslationX, 3);

        MotionPins.Pin([box]);
        box.TranslationX = 0;
        clock.Tick(10);
        Assert.Equal(0, box.TranslationX, 3);

        MotionPins.Unpin();
        clock.Tick(10);
        Assert.Equal(15, box.TranslationX, 3);
        player.Dispose();
    }
}
