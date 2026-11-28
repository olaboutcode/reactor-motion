using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionCallbackTests
{
    [Fact]
    public void OnBegin_waits_out_the_delay_and_runs_again_after_reset()
    {
        var hooks = new Hooks();
        var (player, clock, box) = Bind(hooks.Wire(Fade(100, 50)));

        var task = player.ForwardAsync();
        Assert.Equal(0, hooks.Begin);
        clock.Tick(50);
        Assert.Equal(0, hooks.Begin);
        Assert.Equal(0, box.Opacity);

        clock.Tick(1);
        Assert.Equal(1, hooks.Begin);
        Assert.True(box.Opacity > 0);
        clock.Tick(99);
        Assert.Equal(1, hooks.Complete);
        Assert.Equal(0, hooks.Repeat);
        Assert.True(task.IsCompletedSuccessfully);

        player.Forward();
        Assert.Equal(1, hooks.Begin);
        player.Reset();
        Assert.Equal(0, hooks.ReverseComplete);
        player.Forward();
        Assert.Equal(1, hooks.Begin);
        clock.Tick(50);
        Assert.Equal(1, hooks.Begin);
        clock.Tick(100);
        Assert.Equal(2, hooks.Begin);
        Assert.Equal(2, hooks.Complete);
    }

    [Fact]
    public void Seek_does_not_run_recipe_callbacks()
    {
        var hooks = new Hooks();
        var (player, clock, _) = Bind(hooks.Wire(Fade(100, 40)));

        var task = player.ForwardAsync();
        clock.Tick(10);
        player.Seek(50);
        clock.Tick(100);
        player.Reset();
        player.SeekFraction(1);
        player.Seek(0);

        Assert.Equal(0, hooks.Begin);
        Assert.Equal(0, hooks.Complete);
        Assert.Equal(0, hooks.ReverseStart);
        Assert.Equal(0, hooks.ReverseComplete);
        Assert.True(player.IsDismissed());
        Assert.True(task.IsCanceled);
    }

    [Fact]
    public void Reverse_during_the_delay_runs_OnReverseStart_and_skips_OnBegin()
    {
        var hooks = new Hooks();
        var (player, clock, _) = Bind(hooks.Wire(Fade(100, 50)));

        _ = player.ForwardAsync();
        clock.Tick(10);
        player.Reverse();

        Assert.Equal(0, hooks.Begin);
        Assert.Equal(1, hooks.ReverseStart);
        clock.Tick(1);
        Assert.Equal(1, hooks.ReverseComplete);
        Assert.Equal(0, hooks.Begin);
        Assert.Equal(0, hooks.Complete);
        Assert.True(player.IsDismissed());
    }

    [Fact]
    public void Repeat_runs_OnRepeat_between_cycles_and_OnComplete_with_the_task()
    {
        var hooks = new Hooks();
        Task? task = null;
        var taskDone = false;
        var motion = Motion.None
            .Opacity(0, 1)
            .WithDuration(100)
            .WithEasing(Easing.Linear)
            .Repeat(2)
            .OnBegin(() => hooks.Begin++)
            .OnRepeat(() => hooks.Repeat++)
            .OnComplete(() =>
            {
                hooks.Complete++;
                taskDone = task!.IsCompleted;
            });
        var (player, clock, _) = Bind(motion);

        task = player.ForwardAsync();
        Assert.Equal(1, hooks.Begin);
        clock.Tick(100);
        Assert.Equal(1, hooks.Repeat);
        Assert.Equal(0, hooks.Complete);
        Assert.False(task.IsCompleted);

        clock.Tick(100);
        Assert.Equal(1, hooks.Repeat);
        Assert.Equal(1, hooks.Complete);
        Assert.True(taskDone);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(0, hooks.ReverseStart);
    }

    [Fact]
    public void Yoyo_runs_OnReverseStart_at_the_far_end_and_finishes_on_the_return()
    {
        var hooks = new Hooks();
        var order = new List<string>();
        var motion = Fade(100)
            .Yoyo()
            .Repeat(2)
            .OnBegin(() => hooks.Begin++)
            .OnReverseStart(() =>
            {
                hooks.ReverseStart++;
                order.Add("reverse");
            })
            .OnRepeat(() => hooks.Repeat++)
            .OnComplete(() => hooks.Complete++)
            .OnReverseComplete(() => hooks.ReverseComplete++);
        var (player, clock, _) = Bind(motion);
        player.Completed += (_, _) => order.Add("completed");

        var task = player.ForwardAsync();
        Assert.Equal(1, hooks.Begin);
        clock.Tick(100);
        Assert.Equal(["completed", "reverse"], order);
        Assert.Equal(0, hooks.Complete);
        Assert.Equal(0, hooks.Repeat);
        Assert.False(task.IsCompleted);

        clock.Tick(100);
        Assert.Equal(1, hooks.Repeat);
        Assert.Equal(0, hooks.ReverseComplete);
        clock.Tick(100);
        Assert.Equal(2, hooks.ReverseStart);
        Assert.Equal(0, hooks.Complete);
        clock.Tick(100);
        Assert.Equal(1, hooks.ReverseComplete);
        Assert.Equal(0, hooks.Complete);
        Assert.Equal(1, hooks.Begin);
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void Repeat_forever_runs_OnRepeat_and_never_OnComplete()
    {
        var hooks = new Hooks();
        var (player, clock, _) = Bind(hooks.Wire(Fade(100).Repeat(-1)));

        var task = player.ForwardAsync();
        clock.Tick(100);
        clock.Tick(100);
        clock.Tick(100);

        Assert.Equal(1, hooks.Begin);
        Assert.Equal(3, hooks.Repeat);
        Assert.Equal(0, hooks.Complete);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void A_manual_reverse_stops_a_yoyo_on_OnReverseComplete()
    {
        var hooks = new Hooks();
        var (player, clock, _) = Bind(hooks.Wire(Fade(100).Yoyo().Repeat(-1)));

        _ = player.ForwardAsync();
        clock.Tick(40);
        player.Reverse();

        Assert.Equal(1, hooks.Begin);
        Assert.Equal(1, hooks.ReverseStart);
        Assert.Equal(0, hooks.Repeat);
        clock.Tick(40);
        Assert.Equal(1, hooks.ReverseComplete);
        Assert.Equal(0, hooks.Complete);
        Assert.Equal(0, hooks.Repeat);
        Assert.True(player.IsDismissed());
    }

    [Fact]
    public void Turning_forward_during_a_reverse_does_not_run_OnBegin_again()
    {
        var hooks = new Hooks();
        var (player, clock, _) = Bind(hooks.Wire(Fade(200)));

        _ = player.ForwardAsync();
        clock.Tick(80);
        player.Reverse();
        clock.Tick(40);
        player.Forward();

        Assert.Equal(1, hooks.Begin);
        Assert.Equal(1, hooks.ReverseStart);
        clock.Tick(160);
        Assert.Equal(1, hooks.Complete);
        Assert.Equal(1, hooks.Begin);
        Assert.Equal(0, hooks.ReverseComplete);
    }

    [Fact]
    public void Pause_during_the_delay_still_runs_OnBegin_after_the_wait()
    {
        var hooks = new Hooks();
        var (player, clock, _) = Bind(hooks.Wire(Fade(100, 50)));

        _ = player.ForwardAsync();
        clock.Tick(20);
        player.Pause();
        Assert.Equal(0, hooks.Begin);
        player.Resume();
        clock.Tick(30);
        Assert.Equal(0, hooks.Begin);
        clock.Tick(1);
        Assert.Equal(1, hooks.Begin);
    }

    [Fact]
    public void Reduce_motion_finishes_the_current_direction()
    {
        var hooks = new Hooks();
        var (player, _, box) = Bind(hooks.Wire(Fade(100, 80).Repeat(4).Yoyo().WithReduceMotion(true)));

        var task = player.ForwardAsync();
        Assert.Equal(1, hooks.Begin);
        Assert.Equal(1, hooks.Complete);
        Assert.Equal(0, hooks.Repeat);
        Assert.Equal(0, hooks.ReverseStart);
        Assert.Equal(1, box.Opacity);
        Assert.True(task.IsCompletedSuccessfully);

        var back = new Hooks();
        var (reverse, _, view) = Bind(back.Wire(Fade(100, 80).WithReduceMotion(true)));
        reverse.SeekFraction(1);
        Assert.Equal(0, back.Complete);
        reverse.Reverse();
        Assert.Equal(0, back.Begin);
        Assert.Equal(1, back.ReverseStart);
        Assert.Equal(1, back.ReverseComplete);
        Assert.Equal(0, back.Complete);
        Assert.Equal(0, view.Opacity);
    }

    [Fact]
    public void A_beat_repeat_does_not_run_the_timeline_OnRepeat()
    {
        var beat = 0;
        var timeline = 0;
        var complete = 0;
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear, repeat: 2)
            .Add(m => m.TranslateX(0, 100).Repeat(3).OnRepeat(() => beat++), id: "slide")
            .OnRepeat(() => timeline++)
            .OnComplete(() => complete++);
        var (player, clock, _) = Bind(motion);

        var task = player.ForwardAsync();
        clock.Tick(300);
        Assert.Equal(0, beat);
        Assert.Equal(1, timeline);
        Assert.Equal(0, complete);
        Assert.False(task.IsCompleted);
        clock.Tick(300);
        Assert.Equal(1, timeline);
        Assert.Equal(1, complete);
        Assert.True(task.IsCompletedSuccessfully);

        var loops = 0;
        var visits = 0;
        var looping = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).Repeat(-1).OnRepeat(() => loops++), id: "loop")
            .OnRepeat(() => loops += 10)
            .OnBeat("loop", () => visits++);
        var (loopPlayer, loopClock, _) = Bind(looping);
        var loopTask = loopPlayer.ForwardAsync();
        Assert.Equal(1, visits);
        loopClock.Tick(100);
        Assert.Equal(2, visits);
        Assert.Equal(0, loops);
        Assert.False(loopTask.IsCompleted);
    }

    [Fact]
    public void Copy_keeps_a_callback_and_the_latest_call_wins()
    {
        var first = 0;
        var second = 0;
        var complete = 0;
        var motion = Motion.None
            .Opacity(0, 1)
            .OnBegin(() => first++)
            .OnComplete(() => complete++);
        var (player, clock, _) = Bind(motion.WithDuration(80).WithEasing(Easing.Linear));
        player.Forward();
        clock.Tick(80);
        Assert.Equal(1, first);
        Assert.Equal(1, complete);

        var (replaced, replacedClock, _) = Bind(
            motion.OnBegin(() => second++).WithDuration(80).WithEasing(Easing.Linear));
        replaced.Forward();
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        replacedClock.Tick(80);
        Assert.Equal(2, complete);
    }

    [Fact]
    public void And_keeps_the_right_hand_callback_when_it_has_one()
    {
        var left = 0;
        var right = 0;
        var leftMotion = Motion.None
            .Opacity(0, 1)
            .WithDuration(100)
            .WithEasing(Easing.Linear)
            .OnBegin(() => left++);
        var rightMotion = Motion.None
            .TranslateX(0, 10)
            .WithDuration(100)
            .WithEasing(Easing.Linear)
            .OnBegin(() => right++);
        var (merged, _, _) = Bind(leftMotion.And(rightMotion));
        merged.Forward();
        Assert.Equal(0, left);
        Assert.Equal(1, right);

        var (kept, _, _) = Bind(leftMotion.And(
            Motion.None.TranslateX(0, 10).WithDuration(100).WithEasing(Easing.Linear)));
        kept.Forward();
        Assert.Equal(1, left);
    }

    [Fact]
    public void A_null_callback_throws()
    {
        var motion = Motion.None;
        Assert.Throws<ArgumentNullException>(() => motion.OnBegin(null!));
        Assert.Throws<ArgumentNullException>(() => motion.OnReverseStart(null!));
        Assert.Throws<ArgumentNullException>(() => motion.OnRepeat(null!));
        Assert.Throws<ArgumentNullException>(() => motion.OnComplete(null!));
        Assert.Throws<ArgumentNullException>(() => motion.OnReverseComplete(null!));
    }

    [Fact]
    public void A_throwing_callback_is_skipped_and_the_run_finishes()
    {
        var complete = 0;
        var (player, clock, box) = Bind(Motion.None
            .Opacity(0, 1)
            .WithDuration(100)
            .WithEasing(Easing.Linear)
            .OnBegin(() => throw new InvalidOperationException("begin"))
            .OnComplete(() => complete++));

        var task = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(1, complete);
        Assert.Equal(1, box.Opacity);
        Assert.True(task.IsCompletedSuccessfully);

        var (ending, endingClock, endingBox) = Bind(Motion.None
            .Opacity(0, 1)
            .WithDuration(100)
            .WithEasing(Easing.Linear)
            .OnComplete(() => throw new InvalidOperationException("end")));
        var endingTask = ending.ForwardAsync();
        endingClock.Tick(100);
        Assert.Equal(1, endingBox.Opacity);
        Assert.True(endingTask.IsCompletedSuccessfully);
    }

    [Fact]
    public void Drag_release_runs_OnComplete_when_the_spring_rests()
    {
        var begin = 0;
        var complete = 0;
        var reverseComplete = 0;
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(
                motion => motion
                    .TranslateX(0, 100)
                    .WithDuration(400)
                    .WithEasing(Easing.Linear)
                    .OnBegin(() => begin++)
                    .OnComplete(() => complete++)
                    .OnReverseComplete(() => reverseComplete++),
                stops: [0, 0.5, 1]),
            manual: true);

        drag.Seek(0.3);
        drag.Release(0);
        Assert.Equal(0, complete);
        Settle(drag);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(1, complete);
        Assert.Equal(0, reverseComplete);
        Assert.Equal(0, begin);
        Assert.True(drag.X!.IsPaused());

        var end = new BoxView();
        var toEnd = Open(end, () => complete++, () => reverseComplete++);
        toEnd.Seek(0.8);
        toEnd.Release(0);
        Settle(toEnd);
        Assert.Equal(100, end.TranslationX, 3);
        Assert.Equal(2, complete);
        Assert.True(toEnd.X!.IsCompleted());

        var start = new BoxView();
        var toStart = Open(start, () => complete++, () => reverseComplete++);
        toStart.Seek(0.1);
        toStart.Release(0);
        Settle(toStart);
        Assert.Equal(0, start.TranslationX, 3);
        Assert.Equal(3, complete);
        Assert.Equal(0, reverseComplete);
        Assert.True(toStart.X!.IsDismissed());
    }

    [Fact]
    public void Coast_runs_OnComplete_when_it_rests()
    {
        var complete = 0;
        var box = new BoxView();
        var drag = Motion.Drag(
            box,
            new Spring(400, 10, 1),
            plane => plane.X(motion => motion
                .TranslateX(0, 100)
                .WithDuration(400)
                .WithEasing(Easing.Linear)
                .OnComplete(() => complete++)
                .OnReverseComplete(() => complete += 10)),
            manual: true);

        drag.Seek(0.3);
        drag.Coast(4);
        Assert.Equal(0, complete);
        Settle(drag);

        Assert.Equal(1, complete);
        Assert.True(drag.X!.IsPaused());
        Assert.False(drag.X.IsRunning);
    }

    [Fact]
    public void Pinch_seek_does_not_run_recipe_callbacks()
    {
        var calls = 0;
        var pinch = Motion.Pinch(
            new BoxView(),
            motion => motion
                .Scale(1, 2.5)
                .WithDuration(400)
                .WithEasing(Easing.Linear)
                .OnBegin(() => calls++)
                .OnComplete(() => calls++)
                .OnReverseStart(() => calls++)
                .OnReverseComplete(() => calls++),
            null,
            manual: true);

        pinch.Begin();
        pinch.Seek(4);
        Assert.True(pinch.Scale.IsCompleted());
        pinch.Begin();
        pinch.Cancel();
        Assert.Equal(0, calls);
    }

    static Motion Fade(uint duration, uint delay = 0)
        => Motion.None.Opacity(0, 1).WithDuration(duration).WithEasing(Easing.Linear).WithDelay(delay);

    static (MotionPlayer Player, MotionClock Clock, BoxView Box) Bind(Motion motion)
    {
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        return (MotionPlayer.Create(motion, [box], clock), clock, box);
    }

    static Drag Open(BoxView box, Action complete, Action reverseComplete)
        => Motion.Drag(
            box,
            Spring.Default,
            plane => plane.X(motion => motion
                .TranslateX(0, 100)
                .WithDuration(400)
                .WithEasing(Easing.Linear)
                .OnComplete(complete)
                .OnReverseComplete(reverseComplete)),
            manual: true);

    static void Settle(Drag drag)
    {
        for (var i = 0; i < 200 && drag.X?.IsRunning == true; i++)
            drag.Tick(16);
    }

    sealed class Hooks
    {
        public int Begin;
        public int ReverseStart;
        public int Repeat;
        public int Complete;
        public int ReverseComplete;

        public Motion Wire(Motion motion)
            => motion
                .OnBegin(() => Begin++)
                .OnReverseStart(() => ReverseStart++)
                .OnRepeat(() => Repeat++)
                .OnComplete(() => Complete++)
                .OnReverseComplete(() => ReverseComplete++);
    }
}
