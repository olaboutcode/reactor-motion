using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionDragTests
{
    [Fact]
    public void Seek_0_6_writes_the_linear_translation()
    {
        var box = new BoxView();
        var drag = Open(box);

        drag.Seek(0.6);

        Assert.Equal(60, box.TranslationX, 3);
        Assert.Equal(0.6, drag.X!.Progress, 3);
        Assert.True(drag.X.IsPaused());
    }

    [Fact]
    public void Release_at_0_4_settles_at_the_start()
    {
        var box = new BoxView();
        var drag = Open(box);
        drag.Seek(0.4);

        drag.Release(0);
        Assert.True(drag.X!.IsReverse());
        drag.Tick(16);

        Assert.True(box.TranslationX < 40);
        Settle(drag);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(drag.X.IsDismissed());
    }

    [Fact]
    public void Release_at_0_6_settles_at_the_end()
    {
        var box = new BoxView();
        var drag = Open(box);
        drag.Seek(0.6);

        var started = 0;
        drag.X!.Started += (_, _) => started++;
        drag.Release(0);

        Assert.Equal(1, started);
        Assert.True(drag.X.IsForward());
        drag.Tick(16);
        Assert.True(box.TranslationX > 60);
        Assert.True(box.TranslationX < 100);
        Settle(drag);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X.IsCompleted());
    }

    [Fact]
    public void Release_at_one_half_settles_at_the_end()
    {
        var box = new BoxView();
        var drag = Open(box);
        drag.Seek(0.5);

        drag.Release(0);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(drag.X!.IsForward());
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X.IsCompleted());
    }

    [Fact]
    public void Release_against_the_nearer_end_keeps_that_velocity_then_settles_there()
    {
        var box = new BoxView();
        var drag = Open(box);
        drag.Seek(0.6);
        drag.Release(-8);

        drag.Tick(16);
        Assert.True(box.TranslationX < 60, $"translation={box.TranslationX}");
        Assert.Equal(box.TranslationX / 100d, drag.X!.Progress, 3);
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X.IsCompleted());
    }

    [Fact]
    public void NaN_velocity_matches_a_zero_release()
    {
        var nanBox = new BoxView();
        var zeroBox = new BoxView();
        var nan = Open(nanBox);
        var zero = Open(zeroBox);
        nan.Seek(0.6);
        zero.Seek(0.6);

        nan.Release(double.NaN);
        zero.Release(0);
        nan.Tick(16);
        zero.Tick(16);

        Assert.Equal(zeroBox.TranslationX, nanBox.TranslationX, 5);
        Assert.True(nanBox.TranslationX > 60);
    }

    [Fact]
    public void Seek_after_release_parks_and_a_later_tick_does_not_move()
    {
        var box = new BoxView();
        var drag = Open(box);
        drag.Seek(0.6);
        drag.Release(-8);
        drag.Tick(16);
        Assert.True(box.TranslationX < 60);

        drag.Seek(0.4);
        var parked = box.TranslationX;
        Assert.Equal(40, parked, 3);
        Assert.True(drag.X!.IsPaused());
        drag.Tick(16);

        Assert.Equal(parked, box.TranslationX, 3);
        Assert.True(drag.X.IsPaused());
    }

    [Fact]
    public void Opacity_keeps_its_player_while_translation_is_dragged()
    {
        var box = new BoxView { Opacity = 0 };
        var fadeClock = new MotionClock(manual: true);
        var fade = MotionPlayer.Create(
            Motion.None.Opacity(0, 1).WithDuration(320).WithEasing(Easing.Linear),
            [box],
            fadeClock);
        _ = fade.ForwardAsync();
        fadeClock.Tick(32);
        var opacity = box.Opacity;
        Assert.True(opacity > 0);
        Assert.True(fade.IsForward());

        var drag = Open(box);
        drag.Seek(0.6);
        Assert.Equal(60, box.TranslationX, 3);
        Assert.Equal(opacity, box.Opacity, 3);

        drag.Release(0);
        drag.Tick(16);
        fadeClock.Tick(32);

        Assert.True(box.TranslationX > 60);
        Assert.True(box.Opacity > opacity);
        Assert.True(fade.IsForward());
    }

    [Fact]
    public void Reduce_motion_recipe_jumps_to_the_nearer_end()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(motion => motion
                .TranslateX(0, 100)
                .WithDuration(400)
                .WithEasing(Easing.Linear)
                .WithReduceMotion(true)),
            manual: true);

        drag.Seek(0.3);
        drag.Release(5);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(drag.X!.IsDismissed());
        drag.Tick(16);
        Assert.Equal(0, box.TranslationX, 3);

        drag.Seek(0.8);
        drag.Release(-5);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X.IsCompleted());
        drag.Tick(16);
        Assert.Equal(100, box.TranslationX, 3);
    }

    [Fact]
    public void Yoyo_recipe_stays_at_the_nearer_end()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(motion => motion
                .TranslateX(0, 100)
                .WithDuration(400)
                .WithEasing(Easing.Linear)
                .Yoyo()
                .Repeat(-1)),
            manual: true);
        drag.Seek(0.6);
        drag.Release(0);
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X!.IsCompleted());
        drag.Tick(16);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X.IsCompleted());
    }

    [Fact]
    public void Delay_does_not_hold_the_settle()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(motion => motion
                .TranslateX(0, 100)
                .WithDuration(400)
                .WithEasing(Easing.Linear)
                .WithDelay(5000)),
            manual: true);
        drag.Seek(0.6);
        drag.Release(0);
        drag.Tick(16);

        Assert.True(box.TranslationX > 60);
    }

    [Fact]
    public void Two_axes_settle_on_the_corner_past_each_midpoint()
    {
        var box = new BoxView();
        var drag = Corners(box);
        drag.Seek(0.6, 0.4);

        drag.Release(velocityX: -4, velocityY: 3);
        Settle(drag);

        Assert.Equal(180, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        Assert.True(drag.X!.IsCompleted());
        Assert.True(drag.Y!.IsDismissed());
    }

    [Fact]
    public void Two_axes_at_the_center_settle_on_the_far_corner()
    {
        var box = new BoxView();
        var drag = Corners(box);
        drag.Seek(0.5, 0.5);

        drag.Release();
        Settle(drag);

        Assert.Equal(180, box.TranslationX, 3);
        Assert.Equal(120, box.TranslationY, 3);
        Assert.True(drag.X!.IsCompleted());
        Assert.True(drag.Y!.IsCompleted());
    }

    [Fact]
    public void Translation_span_is_the_finger_travel()
    {
        var plane = new DragPlane()
            .X(motion => motion.TranslateX(0, 180).WithDuration(400).WithEasing(Easing.Linear))
            .Y(motion => motion.TranslateY(40, -20).WithDuration(400).WithEasing(Easing.Linear));

        Assert.Equal(180, plane.XAxis!.Value.Travel);
        Assert.Equal(-60, plane.YAxis!.Value.Travel);
    }

    [Fact]
    public void Explicit_travel_overrides_the_translation_span()
    {
        var plane = new DragPlane().X(
            motion => motion.Opacity(0, 1).WithDuration(400).WithEasing(Easing.Linear),
            travel: 160);

        Assert.Equal(160, plane.XAxis!.Value.Travel);
    }

    [Fact]
    public void Release_after_dispose_throws()
    {
        var drag = Open(new BoxView());
        drag.Dispose();

        Assert.Throws<ObjectDisposedException>(() => drag.Release());
    }

    static Drag Open(BoxView box)
        => Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(motion => motion.TranslateX(0, 100).WithDuration(400).WithEasing(Easing.Linear)),
            manual: true);

    static Drag Corners(BoxView box)
        => Motion.Drag(
            box,
            Spring.Default,
            plane => plane
                .X(motion => motion.TranslateX(0, 180).WithDuration(400).WithEasing(Easing.Linear))
                .Y(motion => motion.TranslateY(0, 120).WithDuration(400).WithEasing(Easing.Linear)),
            manual: true);

    static void Settle(Drag drag)
    {
        for (var i = 0; i < 200 && (drag.X?.IsRunning == true || drag.Y?.IsRunning == true); i++)
            drag.Tick(16);
    }
}
