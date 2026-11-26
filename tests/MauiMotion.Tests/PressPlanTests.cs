using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public class PressPlanTests
{
    [Fact]
    public void Press_plays_forward_and_release_reverses()
    {
        var down = PressPlan.Pressed(default, hasHover: false);
        var up = PressPlan.Released(down.State, hasHover: false);

        Assert.Equal(PressCue.ForwardPress, down.Cue);
        Assert.True(down.State.Down);
        Assert.Equal(PressCue.ReversePress, up.Cue);
        Assert.False(up.State.Down);
    }

    [Fact]
    public void A_second_release_does_nothing()
    {
        var step = PressPlan.Released(default, hasHover: false);

        Assert.Equal(PressCue.None, step.Cue);
    }

    [Fact]
    public void Hover_plays_on_enter_and_reverses_on_exit()
    {
        var enter = PressPlan.Entered(default, hasHover: true);
        var exit = PressPlan.Exited(enter.State, hasHover: true);

        Assert.Equal(PressCue.ForwardHover, enter.Cue);
        Assert.True(enter.State.SawHover);
        Assert.Equal(PressCue.ReverseHover, exit.Cue);
        Assert.False(exit.State.Inside);
    }

    [Fact]
    public void Enter_without_a_hover_recipe_stays_quiet()
    {
        var enter = PressPlan.Entered(default, hasHover: false);

        Assert.Equal(PressCue.None, enter.Cue);
        Assert.False(enter.State.SawHover);
    }

    [Fact]
    public void Press_during_hover_wins_and_release_inside_reverses_the_press()
    {
        var enter = PressPlan.Entered(default, hasHover: true);
        var down = PressPlan.Pressed(enter.State, hasHover: true);
        var up = PressPlan.Released(down.State, hasHover: true);

        Assert.Equal(PressCue.ForwardPress, down.Cue);
        Assert.True(down.State.SawHover);
        Assert.Equal(PressCue.ReversePress, up.Cue);
        Assert.True(up.State.Inside);
    }

    [Fact]
    public void Leaving_while_pressed_returns_to_rest_after_a_hover()
    {
        var enter = PressPlan.Entered(default, hasHover: true);
        var down = PressPlan.Pressed(enter.State, hasHover: true);
        var exit = PressPlan.Exited(down.State, hasHover: true);

        Assert.Equal(PressCue.Rest, exit.Cue);
        Assert.False(exit.State.Down);
        Assert.False(exit.State.Inside);
    }

    [Fact]
    public void Leaving_while_pressed_reverses_when_hover_never_showed()
    {
        var down = PressPlan.Pressed(default, hasHover: true);
        var exit = PressPlan.Exited(down.State, hasHover: true);

        Assert.False(down.State.SawHover);
        Assert.Equal(PressCue.ReversePress, exit.Cue);
    }

    [Fact]
    public void Exit_after_a_press_reverses_hover_only_while_that_player_is_alive()
    {
        var enter = PressPlan.Entered(default, hasHover: true);
        var down = PressPlan.Pressed(enter.State, hasHover: true);
        var up = PressPlan.Released(down.State, hasHover: true);
        var exit = PressPlan.Exited(up.State, hasHover: true);

        Assert.Equal(PressCue.ReverseHover, exit.Cue);
        Assert.Equal(PressCue.Rest, PressPlan.Resolve(exit.Cue, hoverAlive: false));
        Assert.Equal(PressCue.ReverseHover, PressPlan.Resolve(exit.Cue, hoverAlive: true));
    }

    [Fact]
    public void Rest_pose_ends_on_the_authored_from()
    {
        var motion = Motion.None.Scale(1, 1.03).WithSpring(Spring.Gentle);
        var rest = motion.RestPose();

        Assert.True(rest.Pose);
        Assert.Equal(motion.Spring, rest.Spring);
        Assert.Equal(2, rest.Tracks.Count);
        Assert.All(rest.Tracks, track =>
        {
            Assert.Null(track.From);
            Assert.Equal(1d, track.To);
        });
    }

    [Fact]
    public void Rest_pose_keeps_a_to_only_track()
    {
        var rest = Motion.None.Scale(0.96).RestPose();

        Assert.All(rest.Tracks, track =>
        {
            Assert.Null(track.From);
            Assert.Equal(0.96, track.To);
        });
    }

    [Fact]
    public void A_posed_press_reads_the_live_scale_and_a_second_press_continues_from_there()
    {
        var box = new Microsoft.Maui.Controls.BoxView { ScaleX = 1.2, ScaleY = 1.2 };
        var clock = new MotionClock(manual: true);
        var player = MotionPlayer.Create(
            Motion.None.Scale(1, 0.5).WithPose().WithDuration(200).WithEasing(Easing.Linear),
            [box],
            clock);

        player.Forward();
        Assert.Equal(1.2, box.ScaleX, 3);
        Assert.Equal(1.2, box.ScaleY, 3);

        clock.Tick(100);
        Assert.Equal(0.85, box.ScaleX, 3);

        player.Reverse();
        clock.Tick(40);
        var returning = box.ScaleX;
        Assert.True(returning > 0.85);

        player.Forward();
        clock.Tick(40);
        Assert.True(box.ScaleX < returning);
    }
}
