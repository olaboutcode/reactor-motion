using System.Xml;
using Reactor.Motion;
using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public sealed class SvgPathTests
{
    [Fact]
    public void Path_keeps_its_data_and_id()
    {
        var paths = SvgPath.Parse("""
            <svg>
              <path id="route" d="M0,0 L10,0"/>
            </svg>
            """);

        var route = Assert.Single(paths);
        Assert.Equal("route", route.Id);
        Assert.Equal("M0,0 L10,0", route.Data);
        Assert.Equal(new Point(10, 0), At(route.Data, 1));
    }

    [Fact]
    public void Missing_or_empty_id_stays_null_and_the_shape_stays()
    {
        var paths = SvgPath.Parse("""
            <svg>
              <path d="M0,0 L1,0"/>
              <path id="" d="M0,0 L2,0"/>
              <path id="   " d="M0,0 L3,0"/>
            </svg>
            """);

        Assert.Equal(3, paths.Count);
        Assert.All(paths, path => Assert.Null(path.Id));
        Assert.Equal(1, At(paths[0].Data, 1).X, 3);
        Assert.Equal(2, At(paths[1].Data, 1).X, 3);
        Assert.Equal(3, At(paths[2].Data, 1).X, 3);
    }

    [Fact]
    public void Duplicate_ids_stay_in_document_order()
    {
        var paths = SvgPath.Parse("""
            <svg>
              <path id="route" d="M0,0 L1,0"/>
              <path id="route" d="M0,0 L2,0"/>
            </svg>
            """);

        Assert.Equal(2, paths.Count);
        Assert.Equal("route", paths[0].Id);
        Assert.Equal("route", paths[1].Id);
        var first = paths.First(path => string.Equals(path.Id, "route", StringComparison.Ordinal));
        Assert.Equal(1, At(first.Data, 1).X, 3);
        Assert.Equal(2, At(paths[1].Data, 1).X, 3);
    }

    [Fact]
    public void Shapes_are_read_in_document_order()
    {
        var paths = SvgPath.Parse("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <line id="line" x1="0" y1="1" x2="10" y2="1"/>
              <polyline id="poly" points="0,0 10,0 10,10"/>
              <polygon id="gon" points="0,0 10,0 10,10 0,10"/>
              <rect id="box" width="30" height="10"/>
              <circle id="dot" cx="0" cy="0" r="10"/>
              <ellipse id="oval" cx="0" cy="0" rx="20" ry="10"/>
            </svg>
            """);

        Assert.Equal("line poly gon box dot oval", string.Join(' ', paths.Select(path => path.Id)));
        Assert.Equal(new Point(10, 1), At(paths[0].Data, 1));
        Assert.Equal(new Point(10, 10), At(paths[1].Data, 1));
        Assert.Equal(new Point(0, 0), At(paths[2].Data, 1));
        Assert.Equal(new Point(30, 0), At(paths[3].Data, 0.375));
        Assert.Equal(new Point(0, 0), At(paths[3].Data, 1));
        AssertNear(new Point(-10, 0), At(paths[4].Data, 0), 0.5);
        Assert.True(Math.Abs(At(paths[4].Data, 0.25).Y) > 5);
        AssertNear(new Point(-20, 0), At(paths[5].Data, 0), 0.5);
        Assert.True(Math.Abs(At(paths[5].Data, 0.25).Y) > 5);
    }

    [Fact]
    public void Rounded_rect_starts_inset_and_closes()
    {
        var box = Only("""<rect x="0" y="0" width="20" height="20" rx="4"/>""");
        AssertNear(new Point(4, 0), At(box.Data, 0), 0.5);
        AssertNear(new Point(4, 0), At(box.Data, 1), 0.5);
    }

    [Fact]
    public void Transforms_bake_from_the_inside_out()
    {
        var nested = Only("""
            <g transform="translate(1,2)">
              <g transform="translate(3,4)">
                <path d="M0,0 L1,0"/>
              </g>
            </g>
            """);
        Assert.Equal(new Point(4, 6), At(nested.Data, 0));
        Assert.Equal(new Point(5, 6), At(nested.Data, 1));

        var one = Only("""<path transform="translate(10,0) scale(2)" d="M1,0 L2,0"/>""");
        Assert.Equal(new Point(12, 0), At(one.Data, 0));
        Assert.Equal(new Point(14, 0), At(one.Data, 1));

        var child = Only("""
            <g transform="translate(10,0)">
              <path transform="scale(2)" d="M1,0 L2,0"/>
            </g>
            """);
        Assert.Equal(new Point(12, 0), At(child.Data, 0));
        Assert.Equal(new Point(14, 0), At(child.Data, 1));
    }

    [Fact]
    public void Rotate_skew_and_matrix_move_the_points()
    {
        var turned = Only("""<path transform="rotate(90)" d="M1,0 L2,0"/>""");
        AssertNear(new Point(0, 1), At(turned.Data, 0), 0.01);
        AssertNear(new Point(0, 2), At(turned.Data, 1), 0.01);

        var around = Only("""<path transform="rotate(90,10,0)" d="M10,0 L20,0"/>""");
        AssertNear(new Point(10, 0), At(around.Data, 0), 0.01);
        AssertNear(new Point(10, 10), At(around.Data, 1), 0.01);

        var skew = Only("""<path transform="skewX(45)" d="M0,10 L0,20"/>""");
        AssertNear(new Point(10, 10), At(skew.Data, 0), 0.01);
        AssertNear(new Point(20, 20), At(skew.Data, 1), 0.01);

        var moved = Only("""<path transform="matrix(1,0,0,1,5,6)" d="M0,0 L1,0"/>""");
        Assert.Equal(new Point(5, 6), At(moved.Data, 0));
        Assert.Equal(new Point(6, 6), At(moved.Data, 1));
    }

    [Fact]
    public void Scaled_circle_keeps_the_radius()
    {
        var circle = Only("""<circle cx="0" cy="0" r="10" transform="scale(2)"/>""");
        var at = At(circle.Data, 0.25);
        var radius = Math.Sqrt(at.X * at.X + at.Y * at.Y);
        Assert.Equal(20, radius, 1);
        Assert.True(Math.Abs(at.Y) > 10, $"y={at.Y}");
    }

    [Fact]
    public void ViewBox_stretch_is_only_the_into_overload()
    {
        const string markup = """
            <svg viewBox="0 0 10 10">
              <path id="route" d="M0,0 L10,0"/>
            </svg>
            """;

        var user = Assert.Single(SvgPath.Parse(markup));
        Assert.Equal(new Point(10, 0), At(user.Data, 1));

        var fitted = Assert.Single(SvgPath.Parse(markup, new Rect(100, 50, 200, 40)));
        Assert.Equal("route", fitted.Id);
        Assert.Equal(new Point(100, 50), At(fitted.Data, 0));
        Assert.Equal(new Point(300, 50), At(fitted.Data, 1));
    }

    [Fact]
    public void ViewBox_origin_and_a_missing_viewBox_stay_put()
    {
        var shifted = Only(
            """<svg viewBox="2 0 10 10"><path d="M2,0 L12,0"/></svg>""",
            new Rect(0, 0, 100, 50));
        Assert.Equal(new Point(0, 0), At(shifted.Data, 0));
        Assert.Equal(new Point(100, 0), At(shifted.Data, 1));

        var plain = Only("""<svg><path d="M0,0 L10,0"/></svg>""", new Rect(100, 50, 200, 40));
        Assert.Equal(new Point(10, 0), At(plain.Data, 1));
    }

    [Fact]
    public void Lengths_accept_px_and_scientific_notation()
    {
        var line = Only("""<line x2="10px" y2="0"/>""");
        Assert.Equal(new Point(10, 0), At(line.Data, 1));

        var moved = Only("""<path transform="translate(1e1,0)" d="M0,0 L1,0"/>""");
        Assert.Equal(new Point(10, 0), At(moved.Data, 0));
    }

    [Fact]
    public void Defs_are_included_and_use_is_not_a_shape()
    {
        var paths = SvgPath.Parse("""
            <svg>
              <defs>
                <path id="route" d="M0,0 L4,0"/>
              </defs>
              <use href="#route"/>
            </svg>
            """);

        var route = Assert.Single(paths);
        Assert.Equal("route", route.Id);
        Assert.Equal(new Point(4, 0), At(route.Data, 1));
    }

    [Theory]
    [InlineData("<svg/>")]
    [InlineData("<svg><g/></svg>")]
    [InlineData("<svg><text>hi</text></svg>")]
    public void No_followable_shape_returns_an_empty_list(string markup)
        => Assert.Empty(SvgPath.Parse(markup));

    [Fact]
    public void Invalid_markup_throws_when_parse_is_called()
    {
        Assert.Throws<XmlException>(() => SvgPath.Parse("<svg>"));
        Assert.Throws<FormatException>(() => SvgPath.Parse("""<svg><path d="L10,0"/></svg>"""));
        Assert.Throws<FormatException>(() => SvgPath.Parse("""<svg><path transform="nope" d="M0,0"/></svg>"""));
        Assert.Throws<FormatException>(() => SvgPath.Parse("""<svg viewBox="0 0 1"><path d="M0,0"/></svg>"""));
        Assert.Throws<FormatException>(() => SvgPath.Parse("""<svg><rect width="-1" height="1"/></svg>"""));
    }

    [Fact]
    public void Null_markup_throws()
    {
        string markup = null!;
        Assert.Throws<ArgumentNullException>(() => SvgPath.Parse(markup));
        Assert.Throws<ArgumentNullException>(() => SvgPath.Parse(markup, new Rect(0, 0, 10, 10)));
    }

    static SvgPath Only(string markup, Rect? into = null)
    {
        var paths = into is Rect box ? SvgPath.Parse(Wrap(markup), box) : SvgPath.Parse(Wrap(markup));
        return Assert.Single(paths);
    }

    static string Wrap(string markup)
        => markup.TrimStart().StartsWith("<svg", StringComparison.Ordinal) ? markup : "<svg>" + markup + "</svg>";

    static Point At(string data, double fraction)
    {
        var box = new BoxView();
        var player = MotionPlayer.Create(
            Motion.None.Path(data).WithDuration(200).WithEasing(Easing.Linear),
            [box],
            new MotionClock(manual: true));
        player.SeekFraction(fraction);
        return new Point(box.TranslationX, box.TranslationY);
    }

    static void AssertNear(Point expected, Point actual, double tolerance)
    {
        Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
        Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
    }
}
