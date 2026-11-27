using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionPinchTests
{
    [Fact]
    public void Bind_writes_the_from_pose()
    {
        var box = new BoxView();
        var pinch = Open(box);

        Assert.Equal(1, box.ScaleX, 3);
        Assert.Equal(1, box.ScaleY, 3);
        Assert.Equal(-30, box.Rotation, 3);
        Assert.Equal(0, pinch.Scale.Progress, 3);
        Assert.Equal(0, pinch.Rotate!.Progress, 3);
    }

    [Fact]
    public void Wider_span_moves_toward_the_larger_scale()
    {
        var box = new BoxView();
        var pinch = Open(box);

        pinch.Begin();
        pinch.Seek(1.75);

        Assert.Equal(1.75, box.ScaleX, 3);
        Assert.Equal(1.75, box.ScaleY, 3);
        Assert.Equal(0.5, pinch.Scale.Progress, 3);
    }

    [Fact]
    public void Span_past_the_recipe_clamps_at_the_larger_scale()
    {
        var box = new BoxView();
        var pinch = Open(box);

        pinch.Begin();
        pinch.Seek(4);

        Assert.Equal(2.5, box.ScaleX, 3);
        Assert.True(pinch.Scale.IsCompleted());
    }

    [Fact]
    public void Narrower_span_shrinks_from_the_large_pose()
    {
        var box = new BoxView();
        var pinch = Open(box);
        pinch.Begin();
        pinch.Seek(4);
        pinch.Begin();

        pinch.Seek(0.5);

        Assert.Equal(1.25, box.ScaleX, 3);
    }

    [Fact]
    public void Inverted_recipe_still_follows_the_fingers()
    {
        var box = new BoxView();
        var pinch = Motion.Pinch(
            box,
            motion => motion.Scale(2.5, 1).WithDuration(400).WithEasing(Easing.Linear),
            null,
            manual: true);
        pinch.Begin();

        pinch.Seek(0.5);

        Assert.Equal(1.25, box.ScaleX, 3);
    }

    [Fact]
    public void Turn_follows_the_fingers()
    {
        var box = new BoxView();
        var pinch = Open(box);
        pinch.Begin();

        pinch.Seek(1, 10);

        Assert.Equal(-20, box.Rotation, 3);
    }

    [Fact]
    public void Turn_the_other_way_from_the_middle()
    {
        var box = new BoxView();
        var pinch = Open(box);
        pinch.Begin();
        pinch.Seek(1, 30);
        pinch.Begin();

        pinch.Seek(1, -10);

        Assert.Equal(-10, box.Rotation, 3);
    }

    [Fact]
    public void Release_leaves_the_pose()
    {
        var box = new BoxView();
        var pinch = Open(box);
        pinch.Begin();
        pinch.Seek(1.9);

        pinch.Release();

        Assert.Equal(1.9, box.ScaleX, 3);
        Assert.Equal(0.6, pinch.Scale.Progress, 3);
        Assert.True(pinch.Scale.IsPaused());
    }

    [Fact]
    public void Cancel_returns_to_the_progress_where_the_pinch_began()
    {
        var box = new BoxView();
        var pinch = Open(box);
        pinch.Begin();
        pinch.Seek(1.9);
        pinch.Begin();
        pinch.Seek(1.2);

        pinch.Cancel();

        Assert.Equal(1.9, box.ScaleX, 3);
        Assert.Equal(0.6, pinch.Scale.Progress, 3);
    }

    [Fact]
    public void Omit_rotate_and_the_view_does_not_turn()
    {
        var box = new BoxView();
        var pinch = Motion.Pinch(
            box,
            motion => motion.Scale(1, 2.5).WithDuration(400).WithEasing(Easing.Linear),
            null,
            manual: true);

        pinch.Begin();
        pinch.Seek(1.75, 40);

        Assert.Null(pinch.Rotate);
        Assert.Equal(1.75, box.ScaleX, 3);
        Assert.Equal(0, box.Rotation, 3);
    }

    [Fact]
    public void Reduce_motion_stays_at_the_from_pose()
    {
        var box = new BoxView();
        var pinch = Motion.Pinch(
            box,
            motion => motion.Scale(1, 2.5).WithDuration(400).WithEasing(Easing.Linear).WithReduceMotion(true),
            motion => motion.Rotate(-30, 30).WithDuration(400).WithEasing(Easing.Linear).WithReduceMotion(true),
            manual: true);

        pinch.Begin();
        pinch.Seek(2, 20);

        Assert.Equal(1, box.ScaleX, 3);
        Assert.Equal(-30, box.Rotation, 3);
    }

    [Fact]
    public void NaN_span_and_angle_leave_the_pose()
    {
        var box = new BoxView();
        var pinch = Open(box);
        pinch.Begin();

        pinch.Seek(double.NaN, double.PositiveInfinity);

        Assert.Equal(1, box.ScaleX, 3);
        Assert.Equal(-30, box.Rotation, 3);
    }

    [Fact]
    public void Drag_translation_stays_while_the_pinch_scales()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(motion => motion.TranslateX(0, 100).WithDuration(400).WithEasing(Easing.Linear)),
            manual: true);
        drag.Seek(0.5);
        var pinch = Open(box);
        pinch.Begin();

        pinch.Seek(1.75);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(1.75, box.ScaleX, 3);
    }

    [Fact]
    public void Taking_scale_releases_the_scale_player()
    {
        var box = new BoxView();
        var pinch = Open(box);
        var taken = MotionPlayer.Create(
            Motion.None.Scale(4, 4).WithDuration(100).WithEasing(Easing.Linear),
            [box]);
        taken.SeekFraction(1);

        Assert.Throws<ObjectDisposedException>(() => pinch.Seek(1.5));
        Assert.Equal(4, box.ScaleX, 3);
        pinch.Rotate!.SeekFraction(1);
        Assert.Equal(30, box.Rotation, 3);
    }

    [Fact]
    public void Taking_rotation_releases_the_rotate_player()
    {
        var box = new BoxView();
        var pinch = Open(box);
        var taken = MotionPlayer.Create(
            Motion.None.Rotate(0, 90).WithDuration(100).WithEasing(Easing.Linear),
            [box]);
        taken.SeekFraction(1);

        Assert.Throws<ObjectDisposedException>(() => pinch.Seek(1.75, 5));
        Assert.Equal(1.75, box.ScaleX, 3);
        Assert.Equal(90, box.Rotation, 3);
    }

    [Fact]
    public void Reported_scale_accumulates_a_cumulative_slice()
    {
        var ratio = PinchSpan.Apply(1, 1.2, 1, factor: false);
        ratio = PinchSpan.Apply(ratio, 1.2, 1, factor: false);

        Assert.Equal(1.4, ratio, 5);
    }

    [Fact]
    public void Reported_scale_accumulates_a_per_event_factor()
    {
        var ratio = PinchSpan.Apply(1, 1.1, 1, factor: true);
        ratio = PinchSpan.Apply(ratio, 1.1, 1, factor: true);

        Assert.Equal(1.21, ratio, 5);
    }

    [Fact]
    public void Reported_scale_uses_the_view_scale_from_the_start()
    {
        Assert.Equal(1.1, PinchSpan.Apply(1, 1.2, 2, factor: false), 5);
    }

    static Pinch Open(BoxView box)
        => Motion.Pinch(
            box,
            motion => motion.Scale(1, 2.5).WithDuration(400).WithEasing(Easing.Linear),
            motion => motion.Rotate(-30, 30).WithDuration(400).WithEasing(Easing.Linear),
            manual: true);
}
