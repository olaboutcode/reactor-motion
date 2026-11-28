using Reactor.Motion;
using Reactor.Motion.Internals;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace MauiMotion.Tests;

public sealed class MotionPathTests
{
    [Fact]
    public void Linear_path_writes_translation()
    {
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 0) };
        figure.Segments.Add(new LineSegment { Point = new Point(100, 0) });
        geometry.Figures.Add(figure);

        var player = MotionPlayer.Create(
            Motion.None.Path(geometry).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            clock);

        _ = player.ForwardAsync();
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        Assert.Equal(0, box.Rotation, 3);
        clock.Tick(100);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        clock.Tick(100);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
    }

    [Fact]
    public void Arc_midpoint_is_on_the_arc_not_the_chord()
    {
        var box = new BoxView();
        var clock = new MotionClock(manual: true);
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(100, 0) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(0, 100),
            Size = new Size(100, 100),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = false,
        });
        geometry.Figures.Add(figure);

        var player = MotionPlayer.Create(
            Motion.None.Path(geometry).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            clock);

        _ = player.ForwardAsync();
        clock.Tick(100);
        Assert.True(box.TranslationX > 60, $"x={box.TranslationX}");
        Assert.True(box.TranslationY > 60, $"y={box.TranslationY}");
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 1);
        Assert.Equal(100, box.TranslationY, 1);
    }

    [Fact]
    public void String_line_writes_translation()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,0 L100,0", clock);
        Assert.Equal(0, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        clock.Tick(100);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
    }

    [Fact]
    public void Closing_command_adds_the_edge_back_to_the_start()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,0 L100,0 Z", clock);
        clock.Tick(100);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
    }

    [Fact]
    public void Closed_figure_uses_the_same_walk_as_the_string()
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true };
        figure.Segments.Add(new LineSegment { Point = new Point(100, 0) });
        geometry.Figures.Add(figure);

        var clock = new MotionClock(manual: true);
        var box = new BoxView();
        var player = MotionPlayer.Create(
            Motion.None.Path(geometry).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            clock);
        _ = player.ForwardAsync();
        clock.Tick(100);
        Assert.Equal(100, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
    }

    [Fact]
    public void Closing_command_adds_nothing_when_the_pen_is_already_home()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,0 L100,0 L0,0 Z", clock);
        clock.Tick(100);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
    }

    [Fact]
    public void Closing_command_still_closes_a_visible_remainder()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,0 L100,0 L0.2,0 Z", clock);
        clock.Tick(200);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
    }

    [Fact]
    public void Later_move_jumps_and_adds_no_length()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,0 L100,0 M0,100 L100,100", clock);
        clock.Tick(50);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        clock.Tick(50);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
        clock.Tick(50);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
        clock.Tick(50);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
    }

    [Fact]
    public void Leading_move_starts_on_the_drawn_stroke()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,0 M0,100 L100,100", clock, orient: true);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
        Assert.Equal(0, box.Rotation, 3);
        clock.Tick(100);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
        clock.Tick(100);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
    }

    [Fact]
    public void Issue_loop_returns_home_and_leaves_the_chord()
    {
        const string data = "M20,50 C20,-50 180,150 180,50 C180-50 20,150 20,50 Z";
        var clock = new MotionClock(manual: true);
        var box = Run(data, clock);
        Assert.Equal(20, box.TranslationX, 3);
        Assert.Equal(50, box.TranslationY, 3);
        clock.Tick(100);
        Assert.True(box.TranslationX > 100, $"x={box.TranslationX}");
        clock.Tick(100);
        Assert.Equal(20, box.TranslationX, 3);
        Assert.Equal(50, box.TranslationY, 3);
    }

    [Fact]
    public void Invalid_markup_throws_when_the_recipe_is_built()
    {
        Assert.Throws<FormatException>(() => Motion.None.Path("L10,0"));
        Assert.Throws<FormatException>(() => Motion.None.Path("M"));
    }

    [Fact]
    public void Null_path_data_throws()
    {
        string data = null!;
        Assert.Throws<ArgumentNullException>(() => Motion.None.Path(data));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("M0,0")]
    public void Empty_path_does_not_move_the_view(string data)
    {
        var clock = new MotionClock(manual: true);
        var box = Run(data, clock);
        clock.Tick(200);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
    }

    [Fact]
    public void Horizontal_vertical_and_relative_commands_reach_their_end()
    {
        var absolute = new MotionClock(manual: true);
        var hv = Run("M0,0 H100 V40", absolute);
        absolute.Tick(200);
        Assert.Equal(100, hv.TranslationX, 3);
        Assert.Equal(40, hv.TranslationY, 3);

        var relative = new MotionClock(manual: true);
        var moved = Run("m10,10 l10,0", relative);
        relative.Tick(200);
        Assert.Equal(20, moved.TranslationX, 3);
        Assert.Equal(10, moved.TranslationY, 3);
    }

    [Fact]
    public void Repeated_pairs_and_compact_numbers_are_accepted()
    {
        var pairs = new MotionClock(manual: true);
        var line = Run("M0,0 L10,0,20,5", pairs);
        pairs.Tick(200);
        Assert.Equal(20, line.TranslationX, 3);
        Assert.Equal(5, line.TranslationY, 3);

        var compact = new MotionClock(manual: true);
        var curve = Run("M0,0 C180-50 20,150 30,0", compact);
        compact.Tick(200);
        Assert.Equal(30, curve.TranslationX, 3);
        Assert.Equal(0, curve.TranslationY, 3);
    }

    [Fact]
    public void Smooth_commands_are_accepted()
    {
        var cubic = new MotionClock(manual: true);
        var s = Run("M0,0 C10,0 20,0 30,0 S50,0 60,0", cubic);
        cubic.Tick(200);
        Assert.Equal(60, s.TranslationX, 3);
        Assert.Equal(0, s.TranslationY, 3);

        var quad = new MotionClock(manual: true);
        var t = Run("M0,0 Q10,0 20,0 T40,0", quad);
        quad.Tick(200);
        Assert.Equal(40, t.TranslationX, 3);
        Assert.Equal(0, t.TranslationY, 3);
    }

    [Fact]
    public void Arc_command_stays_off_the_chord()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M100,0 A100,100 0 0 1 0,100", clock);
        clock.Tick(100);
        Assert.True(box.TranslationX > 60, $"x={box.TranslationX}");
        Assert.True(box.TranslationY > 60, $"y={box.TranslationY}");
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 1);
        Assert.Equal(100, box.TranslationY, 1);
    }

    [Fact]
    public void Quadratic_command_rises_above_the_chord()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,24 Q130,-110 260,24", clock);
        clock.Tick(100);
        Assert.True(box.TranslationY < 0, $"y={box.TranslationY}");
        clock.Tick(100);
        Assert.Equal(260, box.TranslationX, 1);
        Assert.Equal(24, box.TranslationY, 1);
    }

    [Fact]
    public void Oriented_string_writes_the_tangent()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,0 L0,100", clock, orient: true);
        clock.Tick(100);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(50, box.TranslationY, 3);
        Assert.Equal(90, box.Rotation, 3);
    }

    [Fact]
    public void String_slice_starts_and_ends_inside_the_line()
    {
        var clock = new MotionClock(manual: true);
        var box = Run("M0,0 L100,0", clock, from: 0.25, to: 0.75);
        Assert.Equal(25, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(50, box.TranslationX, 3);
        clock.Tick(100);
        Assert.Equal(75, box.TranslationX, 3);
    }

    [Fact]
    public void Seek_reads_the_length_table()
    {
        var (box, player) = Bind("M0,0 L100,0 L100,100");
        player.SeekFraction(1);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
        player.SeekFraction(0.5);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        player.SeekFraction(0);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
    }

    [Fact]
    public void Seek_backward_across_a_jump_does_not_travel_the_gap()
    {
        var (box, player) = Bind("M0,0 L100,0 M0,100 L100,100");
        player.SeekFraction(1);
        Assert.Equal(100, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
        player.SeekFraction(0.25);
        Assert.Equal(50, box.TranslationX, 3);
        Assert.Equal(0, box.TranslationY, 3);
        player.SeekFraction(0.5);
        Assert.Equal(0, box.TranslationX, 3);
        Assert.Equal(100, box.TranslationY, 3);
    }

    static BoxView Run(
        string data,
        MotionClock clock,
        double from = 0,
        double to = 1,
        bool orient = false)
    {
        var box = new BoxView();
        var player = MotionPlayer.Create(
            Motion.None.Path(data, from, to, orient).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            clock);
        _ = player.ForwardAsync();
        return box;
    }

    static (BoxView Box, MotionPlayer Player) Bind(string data)
    {
        var box = new BoxView();
        var player = MotionPlayer.Create(
            Motion.None.Path(data).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            new MotionClock(manual: true));
        return (box, player);
    }
}
