using Microsoft.Maui.Controls.Shapes;
using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionExclusiveTests
{
    [Fact]
    public void Second_bind_disposes_the_first_player_on_the_same_property()
    {
        var box = new BoxView { Opacity = 1 };
        var first = MotionPlayer.Create(
            Motion.None.FadeIn().WithDuration(300).WithEasing(Easing.Linear),
            [box],
            new MotionClock(manual: true));
        var secondClock = new MotionClock(manual: true);
        var second = MotionPlayer.Create(
            Motion.None.Opacity(0, 0.25).WithDuration(300).WithEasing(Easing.Linear),
            [box],
            secondClock);

        Assert.Throws<ObjectDisposedException>(() => first.Pause());

        var task = second.ForwardAsync();
        secondClock.Tick(300);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(0.25, box.Opacity, 3);
        second.Dispose();
    }

    [Fact]
    public void Opacity_keeps_running_beside_translation()
    {
        var box = new BoxView { Opacity = 0 };
        var fadeClock = new MotionClock(manual: true);
        var fade = MotionPlayer.Create(
            Motion.None.Opacity(0, 1).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            fadeClock);
        var slideClock = new MotionClock(manual: true);
        var slide = MotionPlayer.Create(
            Motion.None.TranslateX(0, 80).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            slideClock);

        _ = fade.ForwardAsync();
        _ = slide.ForwardAsync();
        fadeClock.Tick(100);
        slideClock.Tick(100);

        Assert.Equal(0.5, box.Opacity, 3);
        Assert.Equal(40, box.TranslationX, 3);
        fade.Pause();
        slide.Dispose();
        fade.Dispose();
    }

    [Fact]
    public void Claiming_one_view_keeps_the_player_for_the_other_view()
    {
        var a = new BoxView { Opacity = 0 };
        var b = new BoxView { Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var group = MotionPlayer.Create(
            Motion.None.Opacity(0, 1).WithDuration(200).WithEasing(Easing.Linear),
            [a, b],
            clock);
        _ = group.ForwardAsync();
        clock.Tick(100);

        var other = MotionPlayer.Create(
            Motion.None.Opacity(0, 1).WithDuration(200).WithEasing(Easing.Linear),
            [a],
            new MotionClock(manual: true));
        clock.Tick(100);

        Assert.Equal(0.5, a.Opacity, 3);
        Assert.Equal(1, b.Opacity, 3);
        group.Pause();
        other.Dispose();
        group.Dispose();
    }

    [Fact]
    public void Translation_takes_a_path_and_leaves_scale()
    {
        var box = new BoxView { ScaleX = 1 };
        var clock = new MotionClock(manual: true);
        var combined = MotionPlayer.Create(
            Motion.None.Path(Line()).ScaleX(1, 2).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            clock);
        _ = combined.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(1.5, box.ScaleX, 3);

        var slideClock = new MotionClock(manual: true);
        var slide = MotionPlayer.Create(
            Motion.None.TranslateX(10, 10).WithDuration(100).WithEasing(Easing.Linear),
            [box],
            slideClock);
        _ = slide.ForwardAsync();
        clock.Tick(100);

        Assert.Equal(10, box.TranslationX, 3);
        Assert.Equal(2, box.ScaleX, 3);
        combined.Pause();
        slide.Dispose();
        combined.Dispose();
    }

    [Fact]
    public void Rotation_runs_beside_a_path()
    {
        var box = new BoxView();
        var pathClock = new MotionClock(manual: true);
        var path = MotionPlayer.Create(
            Motion.None.Path(Line()).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            pathClock);
        var spinClock = new MotionClock(manual: true);
        var spin = MotionPlayer.Create(
            Motion.None.Rotate(0, 40).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            spinClock);

        _ = path.ForwardAsync();
        _ = spin.ForwardAsync();
        pathClock.Tick(100);
        spinClock.Tick(100);

        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(20, box.Rotation, 3);
        path.Dispose();
        spin.Dispose();
    }

    [Fact]
    public void Rotation_takes_an_oriented_path()
    {
        var box = new BoxView();
        var path = MotionPlayer.Create(
            Motion.None.Path(Line(), orient: true).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            new MotionClock(manual: true));
        var spin = MotionPlayer.Create(
            Motion.None.Rotate(0, 40).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            new MotionClock(manual: true));

        Assert.Throws<ObjectDisposedException>(() => path.Pause());
        spin.Dispose();
    }

    [Fact]
    public void Translation_replaces_a_transform()
    {
        var box = new BoxView();
        var spin = MotionPlayer.Create(
            Motion.None.Transform(t => Matrix4.Identity.RotateZ(t)).WithDuration(200),
            [box],
            new MotionClock(manual: true));
        var slide = MotionPlayer.Create(
            Motion.None.TranslateX(0, 20).WithDuration(100).WithEasing(Easing.Linear),
            [box],
            new MotionClock(manual: true));

        Assert.Throws<ObjectDisposedException>(() => spin.Pause());
        slide.Dispose();
    }

    [Fact]
    public void Opacity_runs_beside_a_transform()
    {
        var box = new BoxView { Opacity = 0 };
        var spinClock = new MotionClock(manual: true);
        var spin = MotionPlayer.Create(
            Motion.None.Transform(t => Matrix4.Identity.RotateZ(t)).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            spinClock);
        var fadeClock = new MotionClock(manual: true);
        var fade = MotionPlayer.Create(
            Motion.None.Opacity(0, 1).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            fadeClock);

        _ = spin.ForwardAsync();
        _ = fade.ForwardAsync();
        fadeClock.Tick(100);
        spin.Pause();

        Assert.Equal(0.5, box.Opacity, 3);
        spin.Dispose();
        fade.Dispose();
    }

    [Fact]
    public void Pose_keeps_opacity_speed_while_translation_stays()
    {
        var box = new BoxView { Opacity = 0 };
        var clock = new MotionClock(manual: true);
        var both = MotionPlayer.Create(
            Motion.None.Opacity(0, 1).TranslateX(0, 100).WithSpring(Spring.Default, velocity: 5),
            [box],
            clock);
        _ = both.ForwardAsync();
        clock.Tick(16);
        var liveX = box.TranslationX;

        var nextClock = new MotionClock(manual: true);
        var next = MotionPlayer.Create(
            Motion.None.Opacity(1, 0).WithSpring(Spring.Default).WithPose(),
            [box],
            nextClock);
        _ = next.ForwardAsync();
        var atHandoff = box.Opacity;
        nextClock.Tick(16);

        Assert.True(box.Opacity > atHandoff, $"opacity={box.Opacity} handoff={atHandoff}");
        var posed = box.Opacity;
        clock.Tick(16);

        Assert.Equal(posed, box.Opacity, 5);
        Assert.True(box.TranslationX > liveX, $"x={box.TranslationX} live={liveX}");
        both.Pause();
        next.Dispose();
        both.Dispose();
    }

    static PathGeometry Line()
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 0) };
        figure.Segments.Add(new LineSegment { Point = new Point(100, 0) });
        geometry.Figures.Add(figure);
        return geometry;
    }
}
