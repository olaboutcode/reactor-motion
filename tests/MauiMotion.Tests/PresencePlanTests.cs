using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class PresencePlanTests
{
    [Fact]
    public void Mount_when_present_shows_the_child_and_plays_enter()
    {
        var step = PresencePlan.Mount(present: true);

        Assert.True(step.Mounted);
        Assert.False(step.Exiting);
        Assert.Equal(PresenceCue.Enter, step.Cue);
    }

    [Fact]
    public void Mount_when_absent_renders_nothing()
    {
        var step = PresencePlan.Mount(present: false);

        Assert.False(step.Mounted);
        Assert.False(step.Exiting);
        Assert.Equal(PresenceCue.None, step.Cue);
    }

    [Fact]
    public void Hiding_a_shown_child_keeps_it_and_plays_exit()
    {
        var step = PresencePlan.PresentChanged(mounted: true, exiting: false, present: false);

        Assert.True(step.Mounted);
        Assert.True(step.Exiting);
        Assert.Equal(PresenceCue.Exit, step.Cue);
    }

    [Fact]
    public void Showing_again_while_idle_does_not_replay_enter()
    {
        var step = PresencePlan.PresentChanged(mounted: true, exiting: false, present: true);

        Assert.True(step.Mounted);
        Assert.False(step.Exiting);
        Assert.Equal(PresenceCue.None, step.Cue);
    }

    [Fact]
    public void Showing_during_exit_reverses_the_exit()
    {
        var step = PresencePlan.PresentChanged(mounted: true, exiting: true, present: true);

        Assert.True(step.Mounted);
        Assert.True(step.Exiting);
        Assert.Equal(PresenceCue.ReverseExit, step.Cue);
    }

    [Fact]
    public void Hiding_again_during_exit_plays_exit_forward()
    {
        var step = PresencePlan.PresentChanged(mounted: true, exiting: true, present: false);

        Assert.True(step.Mounted);
        Assert.True(step.Exiting);
        Assert.Equal(PresenceCue.Exit, step.Cue);
    }

    [Fact]
    public void Showing_an_absent_child_mounts_it_and_plays_enter()
    {
        var step = PresencePlan.PresentChanged(mounted: false, exiting: false, present: true);

        Assert.True(step.Mounted);
        Assert.False(step.Exiting);
        Assert.Equal(PresenceCue.Enter, step.Cue);
    }

    [Fact]
    public void Finished_exit_unmounts_when_still_absent()
    {
        var step = PresencePlan.RunFinished(mounted: true, exiting: true, present: false, reversed: false);

        Assert.False(step.Mounted);
        Assert.False(step.Exiting);
        Assert.Equal(PresenceCue.None, step.Cue);
    }

    [Fact]
    public void Finished_reverse_keeps_the_child()
    {
        var step = PresencePlan.RunFinished(mounted: true, exiting: true, present: true, reversed: true);

        Assert.True(step.Mounted);
        Assert.False(step.Exiting);
        Assert.Equal(PresenceCue.None, step.Cue);
    }

    [Fact]
    public void Finished_enter_stays_shown()
    {
        var step = PresencePlan.RunFinished(mounted: true, exiting: false, present: true, reversed: false);

        Assert.True(step.Mounted);
        Assert.False(step.Exiting);
        Assert.Equal(PresenceCue.None, step.Cue);
    }

    [Fact]
    public void Finished_exit_while_present_plays_enter()
    {
        var step = PresencePlan.RunFinished(mounted: true, exiting: true, present: true, reversed: false);

        Assert.True(step.Mounted);
        Assert.False(step.Exiting);
        Assert.Equal(PresenceCue.Enter, step.Cue);
    }
}
