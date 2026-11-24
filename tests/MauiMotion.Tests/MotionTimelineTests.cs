using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionTimelineTests
{
    [Fact]
    public void Then_appends_at_current_span()
    {
        var box = new BoxView { Opacity = 1 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.None.FadeIn().WithDuration(200).WithEasing(Easing.Linear)
            .Then(Motion.None.Opacity(1, 0).WithDuration(200).WithEasing(Easing.Linear));
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(400u, motion.Duration);
        Assert.Equal(400u, player.Duration);

        _ = player.ForwardAsync();
        clock.Tick(200);
        Assert.Equal(1, box.Opacity, 3);
        clock.Tick(200);
        Assert.Equal(0, box.Opacity, 3);
        Assert.Equal(MotionPlaybackStatus.Completed, player.Status);
    }

    [Fact]
    public void Add_at_shifts_child_without_stretching()
    {
        var box = new BoxView { Opacity = 1, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.None.FadeIn().WithDuration(300).WithEasing(Easing.Linear)
            .Add(Motion.None.TranslateX(0, 90).WithDuration(300).WithEasing(Easing.Linear), 80);
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(380u, motion.Duration);

        _ = player.ForwardAsync();
        clock.Tick(80);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(box.Opacity > 0);
        clock.Tick(220);
        Assert.Equal(1, box.Opacity, 3);
        clock.Tick(80);
        Assert.Equal(90, box.TranslationX, 3);
    }

    [Fact]
    public void Nested_group_flattens_into_parent()
    {
        var box = new BoxView { Opacity = 1, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var group = Motion.None.FadeIn().WithDuration(100).WithEasing(Easing.Linear)
            .Then(Motion.None.FadeOut().WithDuration(100).WithEasing(Easing.Linear));
        var motion = group.Add(
            Motion.None.TranslateX(0, 40).WithDuration(200).WithEasing(Easing.Linear),
            0);
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(200u, motion.Duration);
        _ = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(20, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(0, box.Opacity, 3);
        Assert.Equal(40, box.TranslationX, 3);
    }

    [Fact]
    public void Nested_child_stagger_does_not_extend_parent_span()
    {
        var child = Motion.None.FadeIn().WithDuration(300).Stagger(100);
        var parent = Motion.None.FadeOut().WithDuration(300).Add(child);
        Assert.Equal(300u, parent.Duration);
    }

    [Fact]
    public void Named_spans_match_add_then_windows()
    {
        var intro = Motion.None.FadeIn().WithDuration(200).WithEasing(Easing.Linear);
        var pulse = Motion.None.Opacity(1, 0).WithDuration(200).WithEasing(Easing.Linear);
        var motion = Motion.None.WithDuration(200)
            .Add(intro, 0, "intro")
            .Then(pulse, "pulse");
        var player = MotionPlayer.Create(motion, [new BoxView()], new MotionClock(manual: true));

        Assert.Equal(400u, player.Duration);
        Assert.True(player.TrySpan("intro", out var introBegin, out var introEnd));
        Assert.True(player.TrySpan("pulse", out var pulseBegin, out var pulseEnd));
        Assert.Equal(0, introBegin, 3);
        Assert.Equal(0.5, introEnd, 3);
        Assert.Equal(0.5, pulseBegin, 3);
        Assert.Equal(1, pulseEnd, 3);
        Assert.False(player.TrySpan("missing", out _, out _));
    }

    [Fact]
    public void Duplicate_id_last_wins()
    {
        var fade = Motion.None.FadeIn().WithDuration(100).WithEasing(Easing.Linear);
        var slide = Motion.None.TranslateX(0, 40).WithDuration(100).WithEasing(Easing.Linear);
        var motion = Motion.None.WithDuration(100)
            .Add(fade, 0, "beat")
            .Then(slide, "beat");
        var player = MotionPlayer.Create(motion, [new BoxView()], new MotionClock(manual: true));

        Assert.True(player.TrySpan("beat", out var begin, out var end));
        Assert.Equal(0.5, begin, 3);
        Assert.Equal(1, end, 3);
    }

    [Fact]
    public void Seek_id_jumps_to_child_start()
    {
        var box = new BoxView { Opacity = 1 };
        var intro = Motion.None.FadeIn().WithDuration(200).WithEasing(Easing.Linear);
        var pulse = Motion.None.Opacity(1, 0).WithDuration(200).WithEasing(Easing.Linear);
        var player = MotionPlayer.Create(
            Motion.None.WithDuration(200).WithEasing(Easing.Linear).Add(intro, 0, "intro").Then(pulse, "pulse"),
            [box],
            new MotionClock(manual: true));

        player.Seek("pulse");
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(0.5, player.Progress, 3);
    }

    [Fact]
    public void Named_span_maps_through_stagger_player_span()
    {
        var motion = Motion.None.WithDuration(300)
            .Add(Motion.None.FadeIn().WithDuration(300), 0, "intro")
            .Stagger(100);
        var player = MotionPlayer.Create(
            motion,
            [new BoxView(), new BoxView()],
            new MotionClock(manual: true));

        Assert.Equal(400u, player.Duration);
        Assert.True(player.TrySpan("intro", out var begin, out var end));
        Assert.Equal(0, begin, 3);
        Assert.Equal(0.75, end, 3);
    }

    [Fact]
    public void Add_bakes_a_child_delay_and_keeps_the_parent_hold()
    {
        var box = new BoxView { Opacity = 1, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var child = Motion.None.TranslateX(0, 40).WithDuration(300).WithEasing(Easing.Linear).WithDelay(100);
        var parent = Motion.None.FadeIn().WithDuration(300).WithEasing(Easing.Linear).Add(child);
        var held = parent.WithDelay(50);

        Assert.Equal(0u, parent.Delay);
        Assert.Equal(400u, parent.Duration);
        Assert.Equal(50u, held.Delay);
        Assert.Equal(400u, held.Duration);

        var player = MotionPlayer.Create(parent, [box], clock);
        Assert.Equal(400u, player.Duration);
        _ = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.True(box.Opacity > 0);
        clock.Tick(200);
        Assert.Equal(1, box.Opacity, 3);
        Assert.True(box.TranslationX > 0);
        clock.Tick(100);
        Assert.Equal(40, box.TranslationX, 3);
    }

    [Fact]
    public void Then_places_a_delayed_child_after_the_span_plus_the_delay()
    {
        var box = new BoxView { Opacity = 1, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.None.FadeIn().WithDuration(200).WithEasing(Easing.Linear)
            .Then(Motion.None.TranslateX(0, 40).WithDuration(200).WithEasing(Easing.Linear).WithDelay(100));
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(500u, motion.Duration);
        Assert.Equal(0u, motion.Delay);

        _ = player.ForwardAsync();
        clock.Tick(200);
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(200);
        Assert.Equal(40, box.TranslationX, 3);
    }

    [Fact]
    public void And_bakes_each_delay_and_the_result_delay_is_zero()
    {
        var box = new BoxView { Opacity = 1, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var both = Motion.None.FadeIn().WithDuration(200).WithEasing(Easing.Linear).WithDelay(100)
            .And(Motion.None.TranslateX(0, 40).WithDuration(200).WithEasing(Easing.Linear));

        Assert.Equal(0u, both.Delay);
        Assert.Equal(300u, both.Duration);

        var player = MotionPlayer.Create(both, [box], clock);
        _ = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(0, box.Opacity, 3);
        Assert.Equal(20, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(0.5, box.Opacity, 3);
        Assert.Equal(40, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(40, box.TranslationX, 3);
    }

    [Fact]
    public void WithDelay_on_an_and_result_holds_both_sides()
    {
        var box = new BoxView { Opacity = 1, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var both = Motion.None.FadeIn().WithDuration(200).WithEasing(Easing.Linear)
            .And(Motion.None.TranslateX(0, 40).WithDuration(200).WithEasing(Easing.Linear))
            .WithDelay(100);

        Assert.Equal(100u, both.Delay);
        Assert.Equal(200u, both.Duration);

        var player = MotionPlayer.Create(both, [box], clock);
        Assert.Equal(200u, player.Duration);
        var task = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(0, box.Opacity);
        Assert.Equal(0, box.TranslationX);
        Assert.False(task.IsCompleted);
        clock.Tick(200);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(40, box.TranslationX, 3);
    }

    [Fact]
    public void Timeline_one_beat_replaces_its_duration()
    {
        var motion = Motion.Timeline(m => m.Opacity(0, 1).WithDuration(280))
            .WithDuration(200);

        Assert.Equal(200u, motion.Duration);
    }

    [Fact]
    public void Timeline_names_the_first_beat()
    {
        var motion = Motion.Timeline(m => m.Opacity(0, 1).WithDuration(200), "intro")
            .Then(m => m.Opacity(1, 0).WithDuration(200), "pulse")
            .WithEasing(Easing.Linear);
        var player = MotionPlayer.Create(motion, [new BoxView()], new MotionClock(manual: true));

        Assert.Equal(400u, motion.Duration);
        Assert.True(player.TrySpan("intro", out var introBegin, out var introEnd));
        Assert.True(player.TrySpan("pulse", out var pulseBegin, out var pulseEnd));
        Assert.Equal(0, introBegin, 3);
        Assert.Equal(0.5, introEnd, 3);
        Assert.Equal(0.5, pulseBegin, 3);
        Assert.Equal(1, pulseEnd, 3);
        player.Seek("pulse");
        Assert.Equal(0.5, player.Progress, 3);
    }

    [Fact]
    public void Timeline_duration_and_easing_apply_to_beats_that_did_not_set_them()
    {
        var box = new BoxView { Opacity = 0, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(m => m.Opacity(0, 1), "intro")
            .Then(m => m.TranslateX(0, 100).WithDuration(120).WithEasing(Easing.Linear), "pulse")
            .WithDuration(280)
            .WithEasing(Easing.CubicOut);

        Assert.Equal(400u, motion.Duration);
        var player = MotionPlayer.Create(motion, [box], clock);
        Assert.True(player.TrySpan("intro", out var introBegin, out var introEnd));
        Assert.True(player.TrySpan("pulse", out var pulseBegin, out var pulseEnd));
        Assert.Equal(0, introBegin, 3);
        Assert.Equal(0.7, introEnd, 3);
        Assert.Equal(0.7, pulseBegin, 3);
        Assert.Equal(1, pulseEnd, 3);

        _ = player.ForwardAsync();
        clock.Tick(140);
        Assert.Equal(Easing.CubicOut.Ease(0.5), box.Opacity, 3);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(140);
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(60);
        Assert.Equal(50, box.TranslationX, 3);
        clock.Tick(60);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(MotionPlaybackStatus.Completed, player.Status);
    }

    [Fact]
    public void Timeline_shared_duration_sets_every_unset_beat()
    {
        var motion = Motion.Timeline(m => m.FadeIn())
            .Then(m => m.FadeOut())
            .WithDuration(280);

        Assert.Equal(560u, motion.Duration);
    }

    [Fact]
    public void Timeline_keeps_a_child_delay_when_duration_is_shared()
    {
        var box = new BoxView { Opacity = 1, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(m => m.Opacity(0, 1))
            .Then(m => m.TranslateX(0, 40).WithDelay(100))
            .WithDuration(200)
            .WithEasing(Easing.Linear);

        Assert.Equal(500u, motion.Duration);
        var player = MotionPlayer.Create(motion, [box], clock);
        _ = player.ForwardAsync();
        clock.Tick(200);
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(20, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(40, box.TranslationX, 3);
    }

    [Fact]
    public void Timeline_add_keeps_its_start_when_duration_is_shared()
    {
        var box = new BoxView { Opacity = 0, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(m => m.Opacity(0, 1))
            .Add(m => m.TranslateX(0, 90), at: 80)
            .WithDuration(100)
            .WithEasing(Easing.Linear);

        Assert.Equal(180u, motion.Duration);
        var player = MotionPlayer.Create(motion, [box], clock);
        _ = player.ForwardAsync();
        clock.Tick(80);
        Assert.Equal(0.8, box.Opacity, 3);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(20);
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(18, box.TranslationX, 3);
        clock.Tick(80);
        Assert.Equal(90, box.TranslationX, 3);
    }

    [Fact]
    public void Then_offset_overlaps_the_previous_end()
    {
        var box = new BoxView { Opacity = 0, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 320, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 320), id: "slide")
            .Then(m => m.Opacity(0, 1).WithDuration(80), id: "fade", offset: -80);

        Assert.Equal(320u, motion.Duration);
        var player = MotionPlayer.Create(motion, [box], clock);
        Assert.True(player.TrySpan("fade", out var begin, out var end));
        Assert.Equal(240d / 320d, begin, 3);
        Assert.Equal(1, end, 3);

        _ = player.ForwardAsync();
        clock.Tick(240);
        Assert.Equal(240, box.TranslationX, 3);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(40);
        Assert.Equal(280, box.TranslationX, 3);
        Assert.Equal(0.5, box.Opacity, 3);
    }

    [Fact]
    public void Then_offset_moves_with_the_previous_beat()
    {
        var motion = Motion.Timeline(m => m.TranslateX(0, 72), "slide")
            .Then(m => m.Opacity(0, 1).WithDuration(80).WithEasing(Easing.Linear), "fade", offset: -80)
            .WithDuration(640)
            .WithEasing(Easing.Linear);
        var player = MotionPlayer.Create(motion, [new BoxView()], new MotionClock(manual: true));

        Assert.Equal(640u, motion.Duration);
        Assert.True(player.TrySpan("slide", out var slideBegin, out var slideEnd));
        Assert.True(player.TrySpan("fade", out var begin, out var end));
        Assert.Equal(0, slideBegin, 3);
        Assert.Equal(1, slideEnd, 3);
        Assert.Equal(560d / 640d, begin, 3);
        Assert.Equal(1, end, 3);
    }

    [Fact]
    public void Then_offset_clamps_at_zero_until_the_previous_beat_grows()
    {
        var motion = Motion.Timeline(duration: 40, easing: Easing.Linear)
            .Add(m => m.Opacity(0, 1), id: "slide")
            .Then(m => m.TranslateX(0, 10).WithDuration(40), id: "fade", offset: -80);
        var player = MotionPlayer.Create(motion, [new BoxView()], new MotionClock(manual: true));

        Assert.Equal(40u, motion.Duration);
        Assert.True(player.TrySpan("fade", out var begin, out var end));
        Assert.Equal(0, begin, 3);
        Assert.Equal(1, end, 3);

        motion = motion.WithDuration(200);
        player = MotionPlayer.Create(motion, [new BoxView()], new MotionClock(manual: true));
        Assert.Equal(200u, motion.Duration);
        Assert.True(player.TrySpan("fade", out begin, out end));
        Assert.Equal(120d / 200d, begin, 3);
        Assert.Equal(160d / 200d, end, 3);
    }

    [Fact]
    public void Timeline_and_runs_inside_the_first_beat()
    {
        var box = new BoxView { Opacity = 0, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(m => m
                .Opacity(0, 1)
                .And(n => n.TranslateX(0, 40)))
            .WithDuration(200)
            .WithEasing(Easing.Linear);

        Assert.Equal(200u, motion.Duration);
        var player = MotionPlayer.Create(motion, [box], clock);
        _ = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(0.5, box.Opacity, 3);
        Assert.Equal(20, box.TranslationX, 3);
    }

    [Fact]
    public void Then_lambda_appends_at_the_current_span()
    {
        var motion = Motion.None.FadeIn().WithDuration(100).WithEasing(Easing.Linear)
            .Then(m => m.FadeOut().WithDuration(100).WithEasing(Easing.Linear));

        Assert.Equal(200u, motion.Duration);
    }

    [Fact]
    public void Windowed_with_duration_still_pads_when_the_recipe_is_not_a_timeline()
    {
        var box = new BoxView { Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.None.Opacity(0, 1).WithDuration(100).WithEasing(Easing.Linear)
            .Then(Motion.None.Opacity(1, 0).WithDuration(100).WithEasing(Easing.Linear))
            .WithDuration(400);

        Assert.Equal(400u, motion.Duration);
        var player = MotionPlayer.Create(motion, [box], clock);
        _ = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(1, box.Opacity, 3);
        clock.Tick(100);
        Assert.Equal(0, box.Opacity, 3);
    }

    [Fact]
    public void Timeline_defaults_fill_beats_that_did_not_set_them()
    {
        var box = new BoxView { Opacity = 0, TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 200, easing: Easing.Linear, delay: 40)
            .Add(Motion.Define(m => m.Opacity(0, 1)), id: "intro")
            .Then(Motion.Define(m => m.TranslateX(0, 40).WithDuration(100).WithEasing(Easing.CubicOut)));

        Assert.Equal(40u, motion.Delay);
        Assert.Equal(300u, motion.Duration);
        var player = MotionPlayer.Create(motion, [box], clock);
        _ = player.ForwardAsync();
        clock.Tick(40);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(100);
        Assert.Equal(0.5, box.Opacity, 3);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(1, box.Opacity, 3);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(40, box.TranslationX, 3);
    }

    [Fact]
    public void Timeline_stores_the_playback_defaults()
    {
        var motion = Motion.Timeline(
            repeat: 3,
            yoyo: true,
            rate: 2,
            reduceMotion: true,
            stagger: new Stagger(40),
            colorSpace: ColorSpace.Rgb,
            perspective: 0.001,
            pose: true);

        Assert.Equal(0u, motion.Duration);
        Assert.Equal(3, motion.RepeatCount);
        Assert.True(motion.YoyoEnabled);
        Assert.Equal(2, motion.Rate);
        Assert.Equal(true, motion.ReduceMotion);
        Assert.Equal(new Stagger(40), motion.StaggerSpec);
        Assert.Equal(ColorSpace.Rgb, motion.ColorSpace);
        Assert.Equal(0.001, motion.PerspectiveEntry);
        Assert.True(motion.Pose);

        var sprung = Motion.Timeline(spring: Spring.Gentle, springVelocity: 2);
        Assert.Equal(Spring.Gentle, sprung.Spring);
        Assert.Equal(2, sprung.SpringVelocity);
        Assert.Null(sprung.StaggerSpec);
    }

    [Fact]
    public void Timeline_duration_before_the_beats_still_restretches()
    {
        var motion = Motion.Timeline(duration: 100)
            .Add(m => m.FadeIn())
            .Then(m => m.FadeOut())
            .WithDuration(40);

        Assert.Equal(80u, motion.Duration);
    }

    [Fact]
    public void Timeline_with_duration_on_an_empty_timeline_sets_the_beat_default()
    {
        var motion = Motion.Timeline()
            .WithDuration(180)
            .Add(m => m.FadeIn())
            .Then(m => m.FadeOut().WithDuration(40));

        Assert.Equal(220u, motion.Duration);
    }

    [Fact]
    public void Timeline_target_plays_each_beat_on_its_view()
    {
        var slide = new BoxView { TranslationX = 0, ScaleX = 1, ScaleY = 1 };
        var grow = new BoxView { TranslationX = 0, ScaleX = 1, ScaleY = 1 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 300).WithDuration(300), target: 0)
            .Add(m => m.Scale(1, 2).WithDuration(300), at: 100, target: 1);

        Assert.Equal(400u, motion.Duration);
        var player = MotionPlayer.Create(motion, [slide, grow], clock);
        _ = player.ForwardAsync();

        clock.Tick(100);
        Assert.Equal(100, slide.TranslationX, 3);
        Assert.Equal(1, slide.ScaleX, 3);
        Assert.Equal(0, grow.TranslationX, 3);
        Assert.Equal(1, grow.ScaleX, 3);
        Assert.Equal(1, grow.ScaleY, 3);

        clock.Tick(150);
        Assert.Equal(250, slide.TranslationX, 3);
        Assert.Equal(1.5, grow.ScaleX, 3);
        Assert.Equal(1.5, grow.ScaleY, 3);
        Assert.Equal(1, slide.ScaleX, 3);

        clock.Tick(150);
        Assert.Equal(300, slide.TranslationX, 3);
        Assert.Equal(2, grow.ScaleX, 3);
        Assert.Equal(0, grow.TranslationX, 3);
    }

    [Fact]
    public void Timeline_without_a_target_plays_on_every_view()
    {
        var first = new BoxView { Opacity = 0 };
        var second = new BoxView { Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.Opacity(0, 1));
        var player = MotionPlayer.Create(motion, [first, second], clock);

        _ = player.ForwardAsync();
        clock.Tick(50);
        Assert.Equal(0.5, first.Opacity, 3);
        Assert.Equal(0.5, second.Opacity, 3);
    }

    [Fact]
    public void Timeline_target_rejects_a_negative_index()
    {
        var timeline = Motion.Timeline();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            timeline.Add(m => m.FadeIn(), target: -1));
    }

    [Fact]
    public void OnBeat_runs_when_forward_playback_enters_the_beat()
    {
        var slide = 0;
        var fade = 0;
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(easing: Easing.Linear, repeat: 2)
            .Add(m => m.Opacity(0, 1).WithDuration(100), id: "slide")
            .Then(m => m.TranslateX(0, 10).WithDuration(100), id: "fade")
            .OnBeat("slide", () => slide++)
            .OnBeat("fade", () => fade++);
        var player = MotionPlayer.Create(motion, [box], clock);
        var progress = new List<double>();
        player.At(progress.Add);

        var task = player.ForwardAsync();
        Assert.Equal(1, slide);
        Assert.Equal(0, fade);
        Assert.NotEmpty(progress);

        clock.Tick(100);
        Assert.Equal(1, slide);
        Assert.Equal(1, fade);

        clock.Tick(100);
        Assert.False(task.IsCompleted);
        Assert.Equal(2, slide);
        Assert.Equal(1, fade);

        clock.Tick(100);
        Assert.Equal(2, fade);
        clock.Tick(100);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(2, slide);
        Assert.Equal(2, fade);
    }

    [Fact]
    public void OnBeat_does_not_run_on_seek_or_reverse()
    {
        var calls = 0;
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(easing: Easing.Linear, yoyo: true)
            .Add(m => m.Opacity(0, 1).WithDuration(100), id: "slide")
            .Then(m => m.TranslateX(0, 10).WithDuration(100), id: "fade")
            .OnBeat("slide", () => calls++)
            .OnBeat("fade", () => calls++);
        var player = MotionPlayer.Create(motion, [box], clock);

        player.Seek("fade");
        Assert.Equal(0, calls);

        player = MotionPlayer.Create(motion, [box], clock);
        var task = player.ForwardAsync();
        clock.Tick(200);
        Assert.False(task.IsCompleted);
        Assert.Equal(MotionPlaybackStatus.Reverse, player.Status);
        Assert.Equal(2, calls);

        clock.Tick(200);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void OnBeat_waits_out_the_timeline_delay()
    {
        var calls = 0;
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 100, delay: 50, easing: Easing.Linear)
            .Add(m => m.Opacity(0, 1), id: "fade")
            .OnBeat("fade", () => calls++);
        var player = MotionPlayer.Create(motion, [new BoxView()], clock);

        _ = player.ForwardAsync();
        Assert.Equal(0, calls);
        clock.Tick(50);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OnBeat_ignores_a_missing_id()
    {
        var motion = Motion.Timeline(duration: 40).Add(m => m.Opacity(0, 1), id: "fade");
        var same = motion.OnBeat("missing", () => { });
        Assert.Same(motion, same);
    }

    [Fact]
    public void Beat_repeat_plays_twice_and_the_next_beat_starts_after()
    {
        var box = new BoxView { TranslationX = 0, Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).Repeat(2), id: "slide")
            .Then(m => m.Opacity(0, 1).WithDuration(100), id: "fade");
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(300u, motion.Duration);
        Assert.Equal(1, motion.RepeatCount);
        Assert.False(motion.YoyoEnabled);
        Assert.True(player.TrySpan("slide", out var slideBegin, out var slideEnd));
        Assert.True(player.TrySpan("fade", out var fadeBegin, out var fadeEnd));
        Assert.Equal(0, slideBegin, 3);
        Assert.Equal(200d / 300d, slideEnd, 3);
        Assert.Equal(200d / 300d, fadeBegin, 3);
        Assert.Equal(1, fadeEnd, 3);

        var task = player.ForwardAsync();
        clock.Tick(25);
        Assert.Equal(25, box.TranslationX, 3);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(100);
        Assert.Equal(25, box.TranslationX, 3);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(125);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(0.5, box.Opacity, 3);
        clock.Tick(50);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(1, box.Opacity, 3);
    }

    [Fact]
    public void Beat_yoyo_plays_back_before_the_next_beat()
    {
        var box = new BoxView { TranslationX = 0, Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).Yoyo(), id: "slide")
            .Then(m => m.Opacity(0, 1).WithDuration(100), id: "fade");
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(300u, motion.Duration);
        Assert.False(motion.YoyoEnabled);
        var task = player.ForwardAsync();
        clock.Tick(25);
        Assert.Equal(25, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(75, box.TranslationX, 3);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(125);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0.5, box.Opacity, 3);
        clock.Tick(50);
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void Beat_repeat_and_yoyo_include_every_pass()
    {
        var box = new BoxView { TranslationX = 0, Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).Repeat(2).Yoyo())
            .Then(m => m.Opacity(0, 1).WithDuration(100));
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(500u, motion.Duration);
        _ = player.ForwardAsync();
        clock.Tick(50);
        Assert.Equal(50, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(50, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(50, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0.5, box.Opacity, 3);
    }

    [Fact]
    public void Beat_repeat_keeps_its_cycles_when_the_timeline_duration_changes()
    {
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).Repeat(2), id: "slide")
            .Then(m => m.Opacity(0, 1).WithDuration(50), id: "fade")
            .WithDuration(200);
        var player = MotionPlayer.Create(motion, [new BoxView { Opacity = 0 }], new MotionClock(manual: true));

        Assert.Equal(450u, motion.Duration);
        Assert.True(player.TrySpan("slide", out _, out var slideEnd));
        Assert.True(player.TrySpan("fade", out var fadeBegin, out _));
        Assert.Equal(400d / 450d, slideEnd, 3);
        Assert.Equal(400d / 450d, fadeBegin, 3);
    }

    [Fact]
    public void Locked_beat_repeat_keeps_its_window()
    {
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).WithDuration(100).Repeat(2), id: "slide")
            .Then(m => m.Opacity(0, 1), id: "fade")
            .WithDuration(400);
        var player = MotionPlayer.Create(motion, [new BoxView()], new MotionClock(manual: true));

        Assert.Equal(600u, motion.Duration);
        Assert.True(player.TrySpan("slide", out _, out var slideEnd));
        Assert.True(player.TrySpan("fade", out var fadeBegin, out _));
        Assert.Equal(200d / 600d, slideEnd, 3);
        Assert.Equal(200d / 600d, fadeBegin, 3);
    }

    [Fact]
    public void Timeline_repeat_stays_on_the_timeline()
    {
        var shared = Motion.Timeline(duration: 100, repeat: 3, yoyo: true)
            .Add(m => m.TranslateX(0, 10));
        Assert.Equal(100u, shared.Duration);
        Assert.Equal(3, shared.RepeatCount);
        Assert.True(shared.YoyoEnabled);

        var opening = Motion.Timeline(m => m.TranslateX(0, 10).WithDuration(80).Repeat(4));
        Assert.Equal(80u, opening.Duration);
        Assert.Equal(4, opening.RepeatCount);
    }

    [Fact]
    public void Plain_then_repeat_extends_the_following_beat()
    {
        var box = new BoxView { TranslationX = 0, Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.None.WithDuration(0)
            .Then(m => m.TranslateX(0, 100).WithDuration(100).WithEasing(Easing.Linear).Repeat(2))
            .Then(m => m.Opacity(0, 1).WithDuration(100).WithEasing(Easing.Linear));
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(300u, motion.Duration);
        Assert.Equal(1, motion.RepeatCount);
        _ = player.ForwardAsync();
        clock.Tick(150);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(100);
        Assert.Equal(0.5, box.Opacity, 3);
    }

    [Fact]
    public void Infinite_beat_holds_every_later_beat()
    {
        var calls = 0;
        var box = new BoxView { TranslationX = 0, Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).Repeat(-1), id: "loop")
            .Then(m => m.Opacity(0, 1).WithDuration(100), id: "after")
            .OnBeat("loop", () => calls++);
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(200u, motion.Duration);
        var task = player.ForwardAsync();
        Assert.Equal(1, calls);
        clock.Tick(50);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(50);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(2, calls);
        for (var i = 0; i < 10; i++)
            clock.Tick(100);
        Assert.False(task.IsCompleted);
        Assert.Equal(0, box.Opacity, 3);
        Assert.Equal(12, calls);
    }

    [Fact]
    public void Infinite_beat_yoyo_loops_the_pair()
    {
        var box = new BoxView { TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).Repeat(-1).Yoyo());
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(200u, motion.Duration);
        var task = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(100, box.TranslationX, 3);
        clock.Tick(50);
        Assert.Equal(50, box.TranslationX, 3);
        clock.Tick(50);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void Infinite_beat_replays_an_overlapping_later_beat()
    {
        var box = new BoxView { TranslationX = 0, Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).WithDuration(100).Repeat(-1))
            .Then(m => m.Opacity(0, 1).WithDuration(100), offset: -40);
        var player = MotionPlayer.Create(motion, [box], clock);

        Assert.Equal(160u, motion.Duration);
        var task = player.ForwardAsync();
        clock.Tick(80);
        Assert.Equal(80, box.TranslationX, 3);
        Assert.Equal(0.2, box.Opacity, 3);
        clock.Tick(20);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.Opacity, 3);
        clock.Tick(80);
        Assert.Equal(80, box.TranslationX, 3);
        Assert.Equal(0.2, box.Opacity, 3);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void Reverse_leaves_an_infinite_beat()
    {
        var box = new BoxView { TranslationX = 0 };
        var clock = new MotionClock(manual: true);
        var motion = Motion.Timeline(duration: 100, easing: Easing.Linear)
            .Add(m => m.TranslateX(0, 100).Repeat(-1));
        var player = MotionPlayer.Create(motion, [box], clock);

        var forward = player.ForwardAsync();
        clock.Tick(40);
        Assert.Equal(40, box.TranslationX, 3);
        var reverse = player.ReverseAsync();
        Assert.True(forward.IsCompletedSuccessfully);
        clock.Tick(40);
        Assert.True(reverse.IsCompletedSuccessfully);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(MotionPlaybackStatus.Dismissed, player.Status);
    }
}
