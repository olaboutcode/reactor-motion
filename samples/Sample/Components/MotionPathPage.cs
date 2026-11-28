using Microsoft.Maui.Controls.Shapes;

namespace Sample.Components;

/// <summary>
/// Cubic and quadratic curves, an arc, a cornered polyline, a slice of a curve, a return trip, and a closed SVG path.
/// </summary>
sealed class MotionPathPage : Component
{
    readonly Lane[] _lanes =
    [
        new("Bézier", "A cubic curve. The square does not turn.", Colors.OrangeRed, Bezier(), 200),
        new("Arc", "A clockwise arc. The bar turns along the tangent.", Colors.CornflowerBlue, Arc(), 180, orient: true, markerWidth: 44, markerHeight: 16, corner: 4),
        new("Quadratic", "One control point pulls the hop upward.", Colors.Teal, Quadratic(), 200),
        new("Corners", "Straight segments. The bar turns at each corner.", Colors.MediumPurple, Corners(), 180, orient: true, markerWidth: 44, markerHeight: 16, corner: 4),
        new("Middle", "The same cubic, from a quarter of its length to three quarters.", Colors.Coral, Bezier(), 200, from: 0.25, to: 0.75),
        new("Return", "The arc again, walked from the end back to the start.", Colors.OrangeRed, Arc(), 180, from: 1, to: 0, orient: true, markerWidth: 44, markerHeight: 16, corner: 4),
        new("SVG", "A path string. The loop closes, and the square does not cut across the gap.", Colors.MediumSeaGreen, null, 240, data: "M20,50 C20,-50 180,150 180,50 C180-50 20,150 20,50 Z"),
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
                        Caption("Each marker follows a geometry or a path string. A bar also turns to match the tangent."),
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
            Grid(
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
            .HeightRequest(lane.Height),
            Button("Play", () => _ = Play(lane))
                .HCenter()
        )
        .Spacing(8);

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
        Park(lane.Marker);
        lane.Player = Motion.Bind(
            Motion.Define(m => Follow(m, lane)
                .WithDuration(1000)
                .WithEasing(Easing.SinInOut)),
            lane.Marker);
    }

    static async Task Play(Lane lane)
    {
        if (lane.Player is null)
            return;
        Park(lane.Marker);
        lane.Player.Reset();
        await lane.Player.ForwardAsync();
    }

    static void Park(MauiControls.BoxView? marker)
    {
        if (marker is null)
            return;
        marker.TranslationX = 0;
        marker.TranslationY = 0;
        marker.Rotation = 0;
    }

    static Motion Follow(Motion motion, Lane lane)
        => lane.Data is null
            ? motion.Path(lane.Geometry!, lane.From, lane.To, lane.Orient)
            : motion.Path(lane.Data, lane.From, lane.To, lane.Orient);

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
        string? data = null)
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
        public MauiControls.BoxView? Marker { get; set; }
        public MotionPlayer? Player { get; set; }
    }
}
