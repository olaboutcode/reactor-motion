using MauiReactor.Shapes;
using Microsoft.Maui.Controls.Shapes;
using PathGeometry = Microsoft.Maui.Controls.Shapes.PathGeometry;

namespace Sample.Components;

/// <summary>
/// Cubic and quadratic curves, an arc, a cornered polyline, a slice of a curve, a return trip, a closed SVG path, and the Suzuka circuit read from an SVG.
/// </summary>
sealed class MotionPathPage : Component
{
    const double TrackWidth = 304;
    const double TrackHeight = 112;
    const double TrackPad = 8;
    const double Dot = 12;
    const double MutedThickness = 1.5;
    const double BoldThickness = 3.5;

    static readonly (string Data, double Length, double X, double Y) SuzukaLap = ReadSuzuka();

    readonly Lane[] _lanes =
    [
        new("Bézier", "A cubic curve. The square does not turn.", Colors.OrangeRed, Bezier(), 200),
        new("Arc", "A clockwise arc. The bar turns along the tangent.", Colors.CornflowerBlue, Arc(), 180, orient: true, markerWidth: 44, markerHeight: 16, corner: 4),
        new("Quadratic", "One control point pulls the hop upward.", Colors.Teal, Quadratic(), 200),
        new("Corners", "Straight segments. The bar turns at each corner.", Colors.MediumPurple, Corners(), 180, orient: true, markerWidth: 44, markerHeight: 16, corner: 4),
        new("Middle", "The same cubic, from a quarter of its length to three quarters.", Colors.Coral, Bezier(), 200, from: 0.25, to: 0.75),
        new("Return", "The arc again, walked from the end back to the start.", Colors.OrangeRed, Arc(), 180, from: 1, to: 0, orient: true, markerWidth: 44, markerHeight: 16, corner: 4),
        new("SVG", "A path string. The loop closes, and the square does not cut across the gap.", Colors.MediumSeaGreen, null, 240, data: "M20,50 C20,-50 180,150 180,50 C180-50 20,150 20,50 Z"),
        new("Suzuka F1 Circuit", "The muted line turns bold as the circle travels.", Colors.Black, null, TrackHeight + (TrackPad * 2), markerWidth: Dot, markerHeight: Dot, corner: Dot / 2, data: SuzukaLap.Data, duration: 5000, linear: true, track: true, length: SuzukaLap.Length, startX: SuzukaLap.X, startY: SuzukaLap.Y),
    ];

    protected override void OnWillUnmount()
    {
        foreach (var lane in _lanes)
        {
            lane.Player?.Dispose();
            lane.Player = null;
        }

        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Path")
                    .GridRow(0),
                ScrollView(
                    VStack([
                        Caption("Each marker follows a geometry, a path string, or a path read from an SVG. A bar also turns to match the tangent."),
                        .. Examples(),
                    ])
                    .Spacing(16)
                    .Padding(24, 24, 24, 32)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    IEnumerable<VisualNode> Examples()
    {
        for (var i = 0; i < _lanes.Length; i++)
        {
            if (i > 0)
                yield return Rule();
            yield return Stage(_lanes[i]);
        }
    }

    static MauiReactor.BoxView Rule()
        => BoxView()
            .HeightRequest(1)
            .HFill()
            .Margin(0, 8, 0, 8)
            .BackgroundColor(Colors.Gray.WithAlpha(0.35f));

    static MauiReactor.VStack Stage(Lane lane)
        => VStack(
            Label(lane.Title)
                .FontSize(16)
                .HCenter(),
            Caption(lane.Detail),
            lane.Track ? SuzukaStage(lane) : Field(lane),
            Button("Play", () => _ = Play(lane))
                .HCenter()
        )
        .Spacing(8);

    static MauiReactor.Grid Field(Lane lane)
        => Grid(
            BoxView()
                .BackgroundColor(Colors.Transparent)
                .HeightRequest(lane.Height),
            BoxView(box => lane.Marker = box)
                .HeightRequest(lane.MarkerHeight)
                .WidthRequest(lane.MarkerWidth)
                .CornerRadius(lane.Corner)
                .BackgroundColor(lane.Color)
                .HStart()
                .VCenter()
                .OnLoaded(() => Bind(lane))
        )
        .HeightRequest(lane.Height);

    static MauiReactor.Grid SuzukaStage(Lane lane)
        => Grid(
            Outline(new MauiReactor.Shapes.Path(), lane, Colors.Gray.WithAlpha(0.4f), MutedThickness),
            Outline(new MauiReactor.Shapes.Path(path => lane.Ink = path), lane, lane.Color, BoldThickness)
                .StrokeDashArray(Dash(lane.Length))
                .StrokeDashOffset(DashUnits(lane.Length)),
            BoxView(box => lane.Marker = box)
                .HeightRequest(lane.MarkerHeight)
                .WidthRequest(lane.MarkerWidth)
                .CornerRadius(lane.Corner)
                .BackgroundColor(lane.Color)
                .Margin(TrackPad - (lane.MarkerWidth / 2), TrackPad - (lane.MarkerHeight / 2), 0, 0)
                .TranslationX(lane.StartX)
                .TranslationY(lane.StartY)
                .HStart()
                .VStart()
                .OnLoaded(() => Bind(lane))
        )
        .WidthRequest(TrackWidth + (TrackPad * 2))
        .HeightRequest(lane.Height)
        .HCenter();

    static MauiReactor.Shapes.Path Outline(MauiReactor.Shapes.Path path, Lane lane, Color color, double thickness)
        => path
            .Data(lane.Data!)
            .Stroke(color)
            .StrokeThickness(thickness)
            .StrokeLineCap(PenLineCap.Round)
            .StrokeLineJoin(PenLineJoin.Round)
            .Aspect(MauiControls.Stretch.None)
            .WidthRequest(TrackWidth)
            .HeightRequest(TrackHeight)
            .Margin(TrackPad)
            .HStart()
            .VStart()
            .InputTransparent(inputTransparent: true);

    static MauiReactor.Label Caption(string text)
        => Label(text)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray);

    static void Bind(Lane lane)
    {
        if (lane.Marker is null)
            return;
        lane.Player?.Dispose();
        Park(lane);
        lane.Player = Motion.Bind(
            Motion.Define(m => Follow(m, lane)
                .WithDuration(lane.Duration)
                .WithEasing(lane.Linear ? Easing.Linear : Easing.SinInOut)),
            lane.Marker);
        if (lane.Track)
            lane.Player.At(progress => Reveal(lane, progress));
    }

    static async Task Play(Lane lane)
    {
        if (lane.Player is null)
            return;
        Park(lane);
        lane.Player.Reset();
        await lane.Player.ForwardAsync();
    }

    static void Park(Lane lane)
    {
        if (lane.Marker is null)
            return;
        lane.Marker.TranslationX = lane.StartX;
        lane.Marker.TranslationY = lane.StartY;
        lane.Marker.Rotation = 0;
        if (lane.Track)
            Reveal(lane, 0);
    }

    static void Reveal(Lane lane, double progress)
    {
        if (lane.Ink is null || lane.Length <= 0)
            return;
        var shown = double.IsNaN(progress) ? 0 : Math.Clamp(progress, 0, 1);
        lane.Ink.StrokeDashOffset = DashUnits(lane.Length) * (1 - shown);
    }

    static Motion Follow(Motion motion, Lane lane)
        => lane.Data is null
            ? motion.Path(lane.Geometry!, lane.From, lane.To, lane.Orient)
            : motion.Path(lane.Data, lane.From, lane.To, lane.Orient);

    static (string Data, double Length, double X, double Y) ReadSuzuka()
    {
        var data = SvgPath.Parse("""
            <svg viewBox="0 0 304 112">
              <path id="suzuka" d="M189.142857,4 C227.456875,4 248.420457,4.00974888 256.864191,4.00974888 C263.817211,4.00974888 271.61219,3.69583517 274.986231,6.63061513 C276.382736,7.84531176 279.193529,11.3814152 280.479499,13.4815847 C281.719344,15.5064248 284.841964,20.3571626 275.608629,20.3571626 C265.817756,20.3571626 247.262478,19.9013915 243.955117,19.9013915 C239.27946,19.9013915 235.350655,24.7304885 228.6344,24.7304885 C224.377263,24.7304885 219.472178,21.0304113 214.535324,21.0304113 C207.18393,21.0304113 200.882842,30.4798911 194.124187,30.4798911 C186.992968,30.4798911 182.652552,23.6245972 173.457298,23.6245972 C164.83277,23.6245972 157.191045,31.5424105 157.191045,39.1815359 C157.191045,48.466779 167.088672,63.6623005 166.666679,66.9065088 C166.378668,69.1206889 155.842137,79.2568633 151.508744,77.8570506 C145.044576,75.7689355 109.126667,61.6405346 98.7556561,52.9785141 C96.4766876,51.0750861 89.3680347,39.5769094 83.4195005,38.5221785 C80.6048001,38.0231057 73.0179337,38.7426555 74.4158694,42.6956376 C76.7088819,49.1796531 86.3280337,64.1214904 87.1781062,66.9065088 C88.191957,70.2280995 86.4690152,77.0567847 82.2060607,79.2503488 C79.2489435,80.7719756 73.1324132,82.8858479 64.7015706,83.0708761 C55.1604808,83.2802705 44.4254811,80.401884 39.1722168,80.401884 C25.7762119,80.401884 24.3280517,89.1260466 22.476679,94.4501705 C21.637667,96.8629767 20.4337535,108 33.2301959,108 C37.8976087,108 45.0757044,107.252595 53.4789069,103.876424 C61.8821095,100.500252 122.090049,78.119656 128.36127,75.3523302 C141.413669,69.5926477 151.190142,68.4987755 147.018529,52.0784879 C143.007818,36.291544 143.396957,23.4057975 145.221196,19.6589263 C146.450194,17.1346449 148.420955,14.8552817 153.206723,15.7880203 C155.175319,16.1716965 155.097637,15.0525421 156.757598,11.3860986 C158.417558,7.71965506 161.842736,4.00974888 167.736963,4.00974888 C177.205308,4.00974888 184.938832,4 189.142857,4 Z"/>
            </svg>
            """).First(path => string.Equals(path.Id, "suzuka", StringComparison.Ordinal)).Data;

        var geometry = new PathGeometry();
        PathFigureCollectionConverter.ParseStringToPathFigureCollection(geometry.Figures, data);
        var figure = geometry.Figures[0];
        var length = 0d;
        var current = figure.StartPoint;
        foreach (var segment in figure.Segments)
        {
            if (segment is not BezierSegment cubic)
                continue;
            length += CubicLength(current, cubic.Point1, cubic.Point2, cubic.Point3);
            current = cubic.Point3;
        }

        if (figure.IsClosed && Distance(current, figure.StartPoint) > 1e-4)
            length += Distance(current, figure.StartPoint);

        return (data, length, figure.StartPoint.X, figure.StartPoint.Y);
    }

    static double CubicLength(Point a, Point b, Point c, Point d)
    {
        const int steps = 32;
        var length = 0d;
        var previous = a;
        for (var i = 1; i <= steps; i++)
        {
            var point = CubicAt(a, b, c, d, i / (double)steps);
            length += Distance(previous, point);
            previous = point;
        }

        return length;
    }

    static Point CubicAt(Point a, Point b, Point c, Point d, double t)
    {
        var u = 1 - t;
        var uu = u * u;
        var tt = t * t;
        var x = (uu * u * a.X) + (3 * uu * t * b.X) + (3 * u * tt * c.X) + (tt * t * d.X);
        var y = (uu * u * a.Y) + (3 * uu * t * b.Y) + (3 * u * tt * c.Y) + (tt * t * d.Y);
        return new Point(x, y);
    }

    static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // iOS paints dash array and offset as multiples of the stroke thickness.
    static double DashUnits(double length)
        => length / BoldThickness;

    static MauiControls.DoubleCollection Dash(double length)
    {
        var units = DashUnits(length);
        return new() { units, units };
    }

    static PathGeometry Bezier()
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 0) };
        figure.Segments.Add(new BezierSegment
        {
            Point1 = new Point(70, -90),
            Point2 = new Point(170, 90),
            Point3 = new Point(260, 0),
        });
        geometry.Figures.Add(figure);
        return geometry;
    }

    static PathGeometry Quadratic()
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 24) };
        figure.Segments.Add(new QuadraticBezierSegment
        {
            Point1 = new Point(130, -110),
            Point2 = new Point(260, 24),
        });
        geometry.Figures.Add(figure);
        return geometry;
    }

    static PathGeometry Corners()
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 24) };
        figure.Segments.Add(new LineSegment { Point = new Point(90, 24) });
        figure.Segments.Add(new LineSegment { Point = new Point(90, -56) });
        figure.Segments.Add(new LineSegment { Point = new Point(190, -56) });
        figure.Segments.Add(new LineSegment { Point = new Point(190, 16) });
        geometry.Figures.Add(figure);
        return geometry;
    }

    static PathGeometry Arc()
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 0) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(240, 0),
            Size = new Size(120, 90),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = false,
        });
        geometry.Figures.Add(figure);
        return geometry;
    }

    sealed class Lane(
        string title,
        string detail,
        Color color,
        PathGeometry? geometry,
        double height,
        double from = 0,
        double to = 1,
        bool orient = false,
        double markerWidth = 36,
        double markerHeight = 36,
        double corner = 18,
        string? data = null,
        uint duration = 1000,
        bool linear = false,
        bool track = false,
        double length = 0,
        double startX = 0,
        double startY = 0)
    {
        public string Title { get; } = title;
        public string Detail { get; } = detail;
        public Color Color { get; } = color;
        public PathGeometry? Geometry { get; } = geometry;
        public string? Data { get; } = data;
        public double Height { get; } = height;
        public double From { get; } = from;
        public double To { get; } = to;
        public bool Orient { get; } = orient;
        public double MarkerWidth { get; } = markerWidth;
        public double MarkerHeight { get; } = markerHeight;
        public double Corner { get; } = corner;
        public uint Duration { get; } = duration;
        public bool Linear { get; } = linear;
        public bool Track { get; } = track;
        public double Length { get; } = length;
        public double StartX { get; } = startX;
        public double StartY { get; } = startY;
        public MauiControls.BoxView? Marker { get; set; }
        public MauiControls.Shapes.Path? Ink { get; set; }
        public MotionPlayer? Player { get; set; }
    }
}
