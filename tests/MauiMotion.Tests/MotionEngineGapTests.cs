using Reactor.Motion;
using Reactor.Motion.Internals;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionEngineGapTests
{
    [Fact]
    public void Rate_doubles_a_linear_fade()
    {
        var (player, clock, box) = Play(
            Motion.None.Opacity(0, 1).WithDuration(200).WithEasing(Easing.Linear).WithRate(2));

        clock.Tick(50);

        Assert.Equal(0.5, box.Opacity, 3);
        Assert.Equal(MotionPlaybackStatus.Forward, player.Status);
    }

    [Fact]
    public void Rate_zero_holds_the_from_pose()
    {
        var (player, clock, box) = Play(
            Motion.None.Opacity(0, 1).WithDuration(200).WithEasing(Easing.Linear).WithRate(0));

        Assert.Equal(0, box.Opacity);
        clock.Tick(200);
        Assert.Equal(0, box.Opacity);
        Assert.Equal(MotionPlaybackStatus.Forward, player.Status);
    }

    [Fact]
    public void Reduce_motion_completes_once_at_the_end()
    {
        var (player, clock, box) = Play(
            Motion.None.Opacity(0, 1).WithDuration(400).WithEasing(Easing.Linear).Repeat(-1).WithReduceMotion(true));

        Assert.True(player.ForwardAsync().IsCompletedSuccessfully);
        Assert.Equal(1, box.Opacity);
        Assert.Equal(MotionPlaybackStatus.Completed, player.Status);
        clock.Tick(400);
        Assert.Equal(MotionPlaybackStatus.Completed, player.Status);
    }

    [Fact]
    public void Spring_velocity_moves_farther_on_the_first_tick()
    {
        var (_, stillClock, still) = Play(Motion.None.Opacity(0, 1).WithSpring(Spring.Default));
        var (_, kickedClock, kicked) = Play(Motion.None.Opacity(0, 1).WithSpring(Spring.Default, velocity: 5));

        stillClock.Tick(16);
        kickedClock.Tick(16);

        Assert.True(kicked.Opacity > still.Opacity);
    }

    [Fact]
    public void Settle_springs_one_track_and_leaves_the_next_on_the_clock()
    {
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var player = MotionPlayer.Create(
            Motion.None
                .Opacity(0, 1)
                .Settle(Spring.Snappy)
                .TranslateX(0, 100)
                .WithDuration(200)
                .WithEasing(Easing.Linear),
            [box],
            clock);

        _ = player.ForwardAsync();
        for (var i = 0; i < 6; i++)
            clock.Tick(16);
        clock.Tick(4);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(box.Opacity > 0.8, $"opacity={box.Opacity}");
        Assert.Equal(MotionPlaybackStatus.Forward, player.Status);
    }

    [Fact]
    public void Settle_on_every_track_finishes_before_the_default_duration()
    {
        var (player, clock, box) = Play(
            Motion.None.Opacity(0, 1).Settle(new Spring(1200, 48.5, 1)));

        var elapsed = 0;
        while (player.Status != MotionPlaybackStatus.Completed && elapsed < 300)
        {
            clock.Tick(16);
            elapsed += 16;
        }

        Assert.Equal(MotionPlaybackStatus.Completed, player.Status);
        Assert.True(elapsed < 300, $"elapsed={elapsed}");
        Assert.Equal(1, box.Opacity, 2);
    }

    [Fact]
    public void Duration_zero_with_a_track_spring_does_not_finish_immediately()
    {
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var player = MotionPlayer.Create(
            Motion.None.Opacity(0, 1).Settle(Spring.Default).WithDuration(0),
            [box],
            clock);

        var task = player.ForwardAsync();

        Assert.False(task.IsCompleted);
        Assert.Equal(MotionPlaybackStatus.Forward, player.Status);
    }

    [Fact]
    public void Cubic_bezier_through_the_corners_is_linear()
    {
        var ease = MotionCurve.CubicBezier(0, 0, 1, 1);

        Assert.Equal(0, ease.Ease(0));
        Assert.Equal(0.5, ease.Ease(0.5), 5);
        Assert.Equal(1, ease.Ease(1));
    }

    [Fact]
    public void Named_durations_are_fast_normal_and_slow()
    {
        Assert.Equal(160u, MotionTime.Fast);
        Assert.Equal(280u, MotionTime.Normal);
        Assert.Equal(400u, MotionTime.Slow);
    }

    [Fact]
    public void Anchor_midpoint_is_halfway()
    {
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var player = MotionPlayer.Create(
            Motion.None.AnchorX(0, 1).AnchorY(0, 1).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            clock);

        _ = player.ForwardAsync();
        Assert.Equal(0, box.AnchorX);
        Assert.Equal(0, box.AnchorY);
        clock.Tick(100);
        Assert.Equal(0.5, box.AnchorX, 3);
        Assert.Equal(0.5, box.AnchorY, 3);
    }

    [Fact]
    public void Oriented_path_writes_the_tangent()
    {
        var box = new BoxView { Rotation = 12 };
        var clock = new MotionClock(manual: true);
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 0) };
        figure.Segments.Add(new LineSegment { Point = new Point(0, 100) });
        geometry.Figures.Add(figure);

        var player = MotionPlayer.Create(
            Motion.None.Path(geometry, orient: true).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            clock);

        _ = player.ForwardAsync();
        Assert.Equal(90, box.Rotation, 3);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        clock.Tick(200);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
        Assert.Equal(90, box.Rotation, 3);
    }

    [Fact]
    public void Pose_matrix_keeps_the_perspective_entry()
    {
        var matrix = PerspectivePlane.PoseMatrix(10, 0, 0, 0, 0, 1, 1, 0.002);

        Assert.Equal(0.002, matrix.Entry(3, 2), 6);
    }

    [Fact]
    public void Shared_ticker_fans_one_delta()
    {
        var first = new MotionClock();
        var second = new MotionClock();
        var firstDelta = 0d;
        var secondDelta = 0d;
        first.Connect(delta => firstDelta += delta);
        second.Connect(delta => secondDelta += delta);
        try
        {
            first.JoinShared();
            second.JoinShared();
            SharedTicker.Dispatch(16);
            Assert.Equal(16, firstDelta);
            Assert.Equal(16, secondDelta);

            first.Stop();
            SharedTicker.Dispatch(16);
            Assert.Equal(16, firstDelta);
            Assert.Equal(32, secondDelta);
        }
        finally
        {
            first.Stop();
            second.Stop();
        }
    }

    static (MotionPlayer Player, MotionClock Clock, BoxView Box) Play(Motion motion)
    {
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var player = MotionPlayer.Create(motion, [box], clock);
        _ = player.ForwardAsync();
        return (player, clock, box);
    }
}
