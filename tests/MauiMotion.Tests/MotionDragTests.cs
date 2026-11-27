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
        drag.Release(-1.9);
        Assert.True(drag.X!.IsForward());

        drag.Tick(16);
        Assert.True(box.TranslationX < 60, $"translation={box.TranslationX}");
        Assert.Equal(box.TranslationX / 100d, drag.X!.Progress, 3);
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X.IsCompleted());
    }

    [Fact]
    public void A_fast_flick_from_near_the_start_settles_at_the_end()
    {
        var box = new BoxView();
        var drag = Open(box);
        drag.Seek(0.2);

        drag.Release(3);
        Assert.True(drag.X!.IsForward());
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X.IsCompleted());
    }

    [Fact]
    public void A_fast_flick_from_near_the_end_settles_at_the_start()
    {
        var box = new BoxView();
        var drag = Open(box);
        drag.Seek(0.8);

        drag.Release(-3);
        Assert.True(drag.X!.IsReverse());
        Settle(drag);

        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(drag.X.IsDismissed());
    }

    [Fact]
    public void A_release_at_the_flick_threshold_keeps_the_nearer_end()
    {
        var box = new BoxView();
        var drag = Open(box);
        drag.Seek(0.2);

        drag.Release(2);
        Assert.True(drag.X!.IsReverse());
        Settle(drag);

        Assert.Equal(0, box.TranslationX, 3);
    }

    [Fact]
    public void Reduce_motion_flick_jumps_to_the_far_end()
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

        drag.Seek(0.2);
        drag.Release(3);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X!.IsCompleted());
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
        drag.Release(1);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(drag.X!.IsDismissed());
        drag.Tick(16);
        Assert.Equal(0, box.TranslationX, 3);

        drag.Seek(0.8);
        drag.Release(-1);
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

        drag.Release(velocityX: -1, velocityY: 1);
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
    public void A_coast_in_the_middle_rests_there()
    {
        var box = new BoxView();
        var drag = OpenCoast(box, new Spring(400, 10, 1));
        drag.Seek(0.3);

        drag.Coast(4);
        Assert.True(drag.X!.IsForward());
        Settle(drag);

        Assert.InRange(box.TranslationX, 50, 90);
        Assert.Equal(box.TranslationX / 100d, drag.X.Progress, 3);
        Assert.True(drag.X.IsPaused());
        Assert.False(drag.X.IsCompleted());
    }

    [Fact]
    public void A_fast_coast_stops_at_the_end()
    {
        var box = new BoxView();
        var drag = OpenCoast(box, new Spring(400, 5, 1));
        drag.Seek(0.8);

        drag.Coast(4);
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(1, drag.X!.Progress, 3);
        Assert.True(drag.X.IsPaused());
        drag.Tick(16);
        Assert.Equal(100, box.TranslationX, 3);
    }

    [Fact]
    public void A_coast_toward_the_start_stops_at_the_start()
    {
        var box = new BoxView();
        var drag = OpenCoast(box, new Spring(400, 5, 1));
        drag.Seek(0.2);

        drag.Coast(-4);
        Assert.True(drag.X!.IsReverse());
        Settle(drag);

        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, drag.X.Progress, 3);
        Assert.True(drag.X.IsPaused());
        drag.Tick(16);
        Assert.Equal(0, box.TranslationX, 3);
    }

    [Fact]
    public void A_zero_or_non_finite_coast_stays_put()
    {
        var held = OpenCoast(new BoxView(), new Spring(400, 10, 1));
        held.Seek(0.4);
        held.Coast(0);
        held.Tick(16);
        Assert.Equal(40, held.X!.Progress * 100, 3);
        Assert.True(held.X.IsPaused());

        var nan = OpenCoast(new BoxView(), new Spring(400, 10, 1));
        nan.Seek(0.4);
        nan.Coast(double.NaN);
        nan.Tick(16);
        Assert.Equal(0.4, nan.X!.Progress, 3);

        var infinite = OpenCoast(new BoxView(), new Spring(400, 10, 1));
        infinite.Seek(0.4);
        infinite.Coast(double.PositiveInfinity);
        infinite.Tick(16);
        Assert.Equal(0.4, infinite.X!.Progress, 3);
    }

    [Fact]
    public void Reduce_motion_coast_stays_at_the_current_progress()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            new Spring(400, 5, 1),
            plane => plane.X(motion => motion
                .TranslateX(0, 100)
                .WithDuration(400)
                .WithEasing(Easing.Linear)
                .WithReduceMotion(true)),
            manual: true);

        drag.Seek(0.4);
        drag.Coast(8);

        Assert.Equal(40, box.TranslationX, 3);
        Assert.Equal(0.4, drag.X!.Progress, 3);
        Assert.True(drag.X.IsPaused());
        drag.Tick(16);
        Assert.Equal(40, box.TranslationX, 3);
    }

    [Fact]
    public void Seek_after_a_coast_clamps_to_the_authored_span()
    {
        var box = new BoxView();
        var drag = OpenCoast(box, new Spring(400, 5, 1));
        drag.Seek(0.8);
        drag.Coast(4);
        Settle(drag);
        Assert.Equal(1, drag.X!.Progress, 3);

        drag.Seek(1.4);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(1, drag.X.Progress, 3);

        drag.Seek(0.25);
        Assert.Equal(25, box.TranslationX, 3);
        Assert.Equal(0.25, drag.X.Progress, 3);
    }

    [Fact]
    public void Delay_repeat_and_yoyo_do_not_continue_a_coast()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            new Spring(400, 10, 1),
            plane => plane.X(motion => motion
                .TranslateX(0, 100)
                .WithDuration(400)
                .WithEasing(Easing.Linear)
                .WithDelay(5000)
                .Repeat(4)
                .Yoyo()),
            manual: true);

        drag.Seek(0.3);
        drag.Coast(4);
        drag.Tick(16);
        Assert.True(box.TranslationX > 30);
        Settle(drag);
        var rest = box.TranslationX;
        Assert.InRange(rest, 50, 90);
        Assert.True(drag.X!.IsPaused());

        drag.Tick(16);
        drag.Tick(16);
        Assert.Equal(rest, box.TranslationX, 3);
    }

    [Fact]
    public void Two_axes_coast_to_their_own_rests()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            new Spring(400, 5, 1),
            plane => plane
                .X(motion => motion.TranslateX(0, 180).WithDuration(400).WithEasing(Easing.Linear))
                .Y(motion => motion.TranslateY(0, 120).WithDuration(400).WithEasing(Easing.Linear)),
            manual: true);

        drag.Seek(0.5, 0.5);
        drag.Coast(velocityX: 4, velocityY: -4);
        Settle(drag);

        Assert.Equal(180, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        Assert.True(drag.X!.IsPaused());
        Assert.True(drag.Y!.IsPaused());
    }

    [Fact]
    public void A_slow_release_settles_on_the_nearest_stop()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.3);

        drag.Release(0);
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0.5, drag.X!.Progress, 3);
        Assert.True(drag.X.IsPaused());
        Assert.False(drag.X.IsCompleted());
        Assert.False(drag.X.IsDismissed());
    }

    [Fact]
    public void A_slow_release_closer_to_the_start_settles_there()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.2);

        drag.Release(0);
        Settle(drag);

        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(drag.X!.IsDismissed());
    }

    [Fact]
    public void A_flick_targets_the_next_stop_and_does_not_skip()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.2);

        drag.Release(3);
        Assert.True(drag.X!.IsForward());
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(drag.X.IsPaused());
        Assert.False(drag.X.IsCompleted());
    }

    [Fact]
    public void A_flick_from_past_the_middle_stop_targets_the_end()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.6);

        drag.Release(3);
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X!.IsCompleted());
    }

    [Fact]
    public void A_flick_toward_the_start_targets_the_neighbor_stop()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.8);

        drag.Release(-3);
        Assert.True(drag.X!.IsReverse());
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(drag.X.IsPaused());
        Assert.False(drag.X.IsDismissed());
    }

    [Fact]
    public void A_release_at_the_flick_threshold_uses_the_nearest_stop()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.2);

        drag.Release(2);
        Settle(drag);

        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(drag.X!.IsDismissed());
    }

    [Fact]
    public void An_equal_distance_picks_the_higher_stop()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.25);

        drag.Release(0);
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(drag.X!.IsPaused());
    }

    [Fact]
    public void A_flick_with_no_stop_ahead_uses_the_nearest_stop()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5);
        drag.Seek(0.8);

        drag.Release(3);
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(drag.X!.IsPaused());
    }

    [Fact]
    public void Stops_outside_the_span_clamp_and_duplicates_collapse()
    {
        var box = new BoxView();
        var drag = OpenStops(box, -1, 0.5, 0.5, 2);
        drag.Seek(0.3);

        drag.Release(0);
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(drag.X!.IsPaused());
    }

    [Fact]
    public void Fewer_than_two_distinct_stops_uses_the_ends()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0.4, double.NaN);
        drag.Seek(0.6);

        drag.Release(0);
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X!.IsCompleted());
    }

    [Fact]
    public void Non_finite_stops_are_dropped()
    {
        var box = new BoxView();
        var drag = OpenStops(box, double.NaN, double.PositiveInfinity);
        drag.Seek(0.6);

        drag.Release(0);
        Settle(drag);

        Assert.Equal(100, box.TranslationX, 3);
        Assert.True(drag.X!.IsCompleted());
    }

    [Fact]
    public void A_non_finite_velocity_uses_the_nearest_stop()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.2);

        drag.Release(double.PositiveInfinity);
        Settle(drag);

        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(drag.X!.IsDismissed());
    }

    [Fact]
    public void Reduce_motion_jumps_to_the_chosen_stop()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(
                motion => motion
                    .TranslateX(0, 100)
                    .WithDuration(400)
                    .WithEasing(Easing.Linear)
                    .WithReduceMotion(true),
                stops: [0, 0.5, 1]),
            manual: true);

        drag.Seek(0.2);
        drag.Release(3);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0.5, drag.X!.Progress, 3);
        Assert.True(drag.X.IsPaused());
        Assert.False(drag.X.IsCompleted());
    }

    [Fact]
    public void A_coast_with_stops_does_not_snap_to_a_stop()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            new Spring(400, 10, 1),
            plane => plane.X(
                motion => motion.TranslateX(0, 100).WithDuration(400).WithEasing(Easing.Linear),
                stops: [0, 0.5, 1]),
            manual: true);

        drag.Seek(0.3);
        drag.Coast(4);
        Settle(drag);

        Assert.InRange(box.TranslationX, 60, 90);
        Assert.True(drag.X!.IsPaused());
    }

    [Fact]
    public void Each_axis_uses_its_own_stops()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane
                .X(
                    motion => motion.TranslateX(0, 100).WithDuration(400).WithEasing(Easing.Linear),
                    stops: [0, 0.5, 1])
                .Y(motion => motion.TranslateY(0, 100).WithDuration(400).WithEasing(Easing.Linear)),
            manual: true);

        drag.Seek(0.3, 0.6);
        drag.Release(0, 0);
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
        Assert.True(drag.X!.IsPaused());
        Assert.True(drag.Y!.IsCompleted());
    }

    [Fact]
    public void Delay_repeat_and_yoyo_do_not_continue_an_interior_stop()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(
                motion => motion
                    .TranslateX(0, 100)
                    .WithDuration(400)
                    .WithEasing(Easing.Linear)
                    .WithDelay(5000)
                    .Repeat(4)
                    .Yoyo(),
                stops: [0, 0.5, 1]),
            manual: true);

        drag.Seek(0.3);
        drag.Release(0);
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0.5, drag.X!.Progress, 3);
        Assert.True(drag.X.IsPaused());

        drag.Tick(16);
        drag.Tick(16);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(drag.X.IsPaused());
    }

    [Fact]
    public void A_release_on_a_stop_stays_there()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.5);

        drag.Release(0);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0.5, drag.X!.Progress, 3);
        Assert.True(drag.X.IsPaused());
        Assert.False(drag.X.IsRunning);
        drag.Tick(16);
        Assert.Equal(50, box.TranslationX, 3);
    }

    [Fact]
    public void Seek_after_an_interior_stop_parks_and_a_later_tick_does_not_move()
    {
        var box = new BoxView();
        var drag = OpenStops(box, 0, 0.5, 1);
        drag.Seek(0.3);
        drag.Release(0);
        Settle(drag);
        Assert.Equal(50, box.TranslationX, 3);

        drag.Seek(0.2);
        Assert.Equal(20, box.TranslationX, 3);
        Assert.True(drag.X!.IsPaused());
        drag.Tick(16);

        Assert.Equal(20, box.TranslationX, 3);
        Assert.True(drag.X.IsPaused());
    }

    [Fact]
    public void An_interior_stop_keeps_its_progress_when_the_easing_is_not_linear()
    {
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(
                motion => motion.TranslateX(0, 100).WithDuration(400).WithEasing(Easing.CubicOut),
                stops: [0, 0.5, 1]),
            manual: true);

        drag.Seek(0.5);
        drag.Release(0);

        Assert.Equal(0.5, drag.X!.Progress, 3);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.True(drag.X.IsPaused());
    }

    [Fact]
    public void Release_after_dispose_throws()
    {
        var drag = Open(new BoxView());
        drag.Dispose();

        Assert.Throws<ObjectDisposedException>(() => drag.Release());
        Assert.Throws<ObjectDisposedException>(() => drag.Coast());
    }

    static Drag Open(BoxView box)
        => Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(motion => motion.TranslateX(0, 100).WithDuration(400).WithEasing(Easing.Linear)),
            manual: true);

    static Drag OpenStops(BoxView box, params double[] stops)
        => Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(
                motion => motion.TranslateX(0, 100).WithDuration(400).WithEasing(Easing.Linear),
                stops: stops),
            manual: true);

    static Drag OpenCoast(BoxView box, Spring spring)
        => Motion.Drag(
            box,
            spring,
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
