using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionPoseTests
{
    [Fact]
    public void Pose_reads_the_live_opacity_instead_of_the_authored_from()
    {
        var box = new BoxView { Opacity = 0.4 };
        var (player, clock) = Play(
            Motion.None.Opacity(1, 0).WithPose().WithDuration(200).WithEasing(Easing.Linear),
            box);

        Assert.Equal(0.4, box.Opacity, 3);
        clock.Tick(100);
        Assert.Equal(0.2, box.Opacity, 3);
        Assert.Equal(MotionPlaybackStatus.Forward, player.Status);
    }

    [Fact]
    public void Explicit_from_still_writes_when_the_recipe_is_not_a_pose()
    {
        var box = new BoxView { Opacity = 0.4 };
        _ = Play(Motion.None.Opacity(1, 0).WithDuration(200).WithEasing(Easing.Linear), box);

        Assert.Equal(1, box.Opacity, 3);
    }

    [Fact]
    public void Pose_carries_an_in_flight_spring_past_the_live_opacity()
    {
        var box = new BoxView { Opacity = 0 };
        var (_, clock) = Play(
            Motion.None.Opacity(0, 1).WithSpring(Spring.Default, velocity: 5),
            box);
        clock.Tick(16);
        var live = box.Opacity;

        var nextClock = new MotionClock(manual: true);
        var next = MotionPlayer.Create(
            Motion.None.Opacity(1, 0).WithSpring(Spring.Default).WithPose(),
            [box],
            nextClock);
        _ = next.ForwardAsync();
        nextClock.Tick(16);

        Assert.True(box.Opacity > live, $"opacity={box.Opacity} live={live}");
        Assert.True(box.Opacity < 0.5, $"opacity={box.Opacity}");
    }

    [Fact]
    public void Seek_leaves_no_velocity_for_the_next_pose()
    {
        var box = new BoxView { Opacity = 0 };
        var (player, clock) = Play(
            Motion.None.Opacity(0, 1).WithSpring(Spring.Default, velocity: 5),
            box);
        clock.Tick(16);
        player.Seek(120);

        var rested = new BoxView { Opacity = box.Opacity };
        var restedClock = new MotionClock(manual: true);
        var restedPlayer = MotionPlayer.Create(PosedExit(), [rested], restedClock);
        var handoffClock = new MotionClock(manual: true);
        var handoff = MotionPlayer.Create(PosedExit(), [box], handoffClock);
        _ = restedPlayer.ForwardAsync();
        _ = handoff.ForwardAsync();
        restedClock.Tick(16);
        handoffClock.Tick(16);

        Assert.Equal(rested.Opacity, box.Opacity, 3);
    }

    [Fact]
    public void Settle_pose_carries_that_track_spring()
    {
        var box = new BoxView { Opacity = 0 };
        var (_, clock) = Play(
            Motion.None.Opacity(0, 1).Settle(Spring.Default, velocity: 5),
            box);
        clock.Tick(16);
        var live = box.Opacity;

        var nextClock = new MotionClock(manual: true);
        var next = MotionPlayer.Create(
            Motion.None.Opacity(1, 0).Settle(Spring.Default).WithPose(),
            [box],
            nextClock);
        _ = next.ForwardAsync();
        nextClock.Tick(16);

        Assert.True(box.Opacity > live, $"opacity={box.Opacity} live={live}");
        Assert.True(box.Opacity < 0.5, $"opacity={box.Opacity}");
    }

    static Motion PosedExit()
        => Motion.None.Opacity(1, 0).WithSpring(Spring.Default, velocity: 4).WithPose();

    static (MotionPlayer Player, MotionClock Clock) Play(Motion motion, BoxView box)
    {
        var clock = new MotionClock(manual: true);
        var player = MotionPlayer.Create(motion, [box], clock);
        _ = player.ForwardAsync();
        return (player, clock);
    }
}
