using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public class InViewSessionTests
{
    [Fact]
    public void Below_the_mark_does_not_play()
    {
        var session = new InViewSession();

        Assert.Equal(InViewStep.None, session.Advance(crosses: false, replay: true));
        Assert.False(session.Played);
        Assert.False(session.Armed);
    }

    [Fact]
    public void The_first_crossing_plays_once()
    {
        var session = new InViewSession();

        Assert.Equal(InViewStep.Play, session.Advance(crosses: true, replay: false));
        Assert.Equal(InViewStep.None, session.Advance(crosses: true, replay: false));
    }

    [Fact]
    public void Leaving_and_returning_does_not_play_again_by_default()
    {
        var session = new InViewSession();
        session.Advance(crosses: true, replay: false);
        session.Advance(crosses: false, replay: false);

        Assert.Equal(InViewStep.None, session.Advance(crosses: true, replay: false));
        Assert.False(session.Armed);
    }

    [Fact]
    public void Replay_arms_when_the_overlap_falls_back_below_the_mark()
    {
        var session = new InViewSession();
        session.Advance(crosses: true, replay: true);

        Assert.Equal(InViewStep.None, session.Advance(crosses: false, replay: true));
        Assert.True(session.Armed);
        Assert.True(session.Played);
    }

    [Fact]
    public void The_next_crossing_replays_and_staying_past_the_mark_does_not()
    {
        var session = new InViewSession();
        session.Advance(crosses: true, replay: true);
        session.Advance(crosses: false, replay: true);

        Assert.Equal(InViewStep.Replay, session.Advance(crosses: true, replay: true));
        Assert.False(session.Armed);
        Assert.Equal(InViewStep.None, session.Advance(crosses: true, replay: true));
    }

    [Fact]
    public void A_second_return_replays_again()
    {
        var session = new InViewSession();
        session.Advance(crosses: true, replay: true);
        session.Advance(crosses: false, replay: true);
        session.Advance(crosses: true, replay: true);
        session.Advance(crosses: false, replay: true);

        Assert.Equal(InViewStep.Replay, session.Advance(crosses: true, replay: true));
    }

    [Fact]
    public void Reduce_motion_finishes_each_entry()
    {
        var box = new BoxView();
        var player = MotionPlayer.Create(
            Motion.None.Opacity(0, 1).WithDuration(400).WithEasing(Easing.Linear).WithReduceMotion(true),
            [box],
            new MotionClock(manual: true));
        player.Seek(0);
        player.Forward();

        Assert.Equal(1, box.Opacity, 3);
        player.Seek(0);
        Assert.Equal(0, box.Opacity, 3);
        player.Forward();
        Assert.Equal(1, box.Opacity, 3);
        Assert.True(player.IsCompleted());
    }
}
