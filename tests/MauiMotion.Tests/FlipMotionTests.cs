using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class FlipMotionTests
{
    [Fact]
    public void Shift_uses_the_move_duration_and_easing_on_the_axes_that_moved()
    {
        var timing = Motion.None.WithDuration(280).WithEasing(Easing.Linear);

        var shift = FlipMotion.Move(timing.FromPlace(), x: 12, y: -4);

        Assert.NotNull(shift);
        Assert.Equal(280u, shift.Duration);
        Assert.Equal(Easing.Linear, shift.Easing);
        Assert.Null(shift.Spring);
        Assert.Equal(2, shift.Tracks.Count);
        Assert.Equal(VisualElement.TranslationXProperty, shift.Tracks[0].Property);
        Assert.Equal(12d, shift.Tracks[0].From);
        Assert.Equal(0d, shift.Tracks[0].To);
        Assert.Equal(VisualElement.TranslationYProperty, shift.Tracks[1].Property);
        Assert.Equal(-4d, shift.Tracks[1].From);
        Assert.Equal(0d, shift.Tracks[1].To);
    }

    [Fact]
    public void Shift_leaves_off_an_axis_that_did_not_move()
    {
        var shift = FlipMotion.Move(Motion.None.FromPlace(), x: 0, y: 24);

        Assert.NotNull(shift);
        Assert.Single(shift.Tracks);
        Assert.Equal(VisualElement.TranslationYProperty, shift.Tracks[0].Property);
    }

    [Fact]
    public void Shift_is_empty_under_half_a_pixel()
    {
        Assert.Null(FlipMotion.Move(Motion.None.FromPlace(), x: 0.2, y: -0.2));
    }

    [Fact]
    public void Shift_can_spring()
    {
        var timing = Motion.None.WithSpring(Spring.Gentle, velocity: 2);

        var shift = FlipMotion.Move(timing.FromPlace(), x: 0, y: 30);

        Assert.NotNull(shift);
        Assert.Equal(Spring.Gentle, shift.Spring);
        Assert.Equal(2, shift.SpringVelocity);
    }

    [Fact]
    public void Fade_copies_delay_rate_and_reduce_motion()
    {
        var timing = Motion.None
            .WithDuration(240)
            .WithDelay(40)
            .WithRate(2)
            .WithReduceMotion(true);

        var fade = FlipMotion.Fade(inbound: false, timing);

        Assert.Equal(240u, fade.Duration);
        Assert.Equal(40u, fade.Delay);
        Assert.Equal(2, fade.Rate);
        Assert.True(fade.ReduceMotion);
        Assert.Equal(VisualElement.OpacityProperty, Assert.Single(fade.Tracks).Property);
        Assert.Equal(1d, fade.Tracks[0].From);
        Assert.Equal(0d, fade.Tracks[0].To);
    }

    [Fact]
    public void Move_plays_the_callers_tracks_without_adding_a_slide()
    {
        var recipe = Motion.None.RotateY(80, 0).WithDuration(400);

        var move = FlipMotion.Move(recipe, x: 0, y: 30);

        Assert.NotNull(move);
        var track = Assert.Single(move.Tracks);
        Assert.Equal(VisualElement.RotationYProperty, track.Property);
        Assert.Equal(80d, track.From);
        Assert.Equal(0d, track.To);
        Assert.Equal(400u, move.Duration);
    }

    [Fact]
    public void Move_adds_the_place_delta_beside_the_callers_tracks()
    {
        var recipe = Motion.None.FromPlace().RotateY(80, 0).WithDuration(400);

        var move = FlipMotion.Move(recipe, x: 0, y: 30);

        Assert.NotNull(move);
        Assert.Equal(2, move.Tracks.Count);
        Assert.Equal(VisualElement.RotationYProperty, move.Tracks[0].Property);
        Assert.Equal(VisualElement.TranslationYProperty, move.Tracks[1].Property);
        Assert.Equal(30d, move.Tracks[1].From);
        Assert.Equal(0d, move.Tracks[1].To);
    }
}
