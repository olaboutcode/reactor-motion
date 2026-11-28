using System.Globalization;
using System.Text;
using Microsoft.Maui.Controls.Shapes;

namespace Reactor.Motion.Internals;

/// <summary>
/// Rewrites path data so an affine map is already in the coordinates.
/// An identity map keeps the original text. An arc becomes cubics until the
/// midpoint sits within half a point of the arc in the emitted coordinates.
/// </summary>
internal static class SvgPathBake
{
    const double HalfPoint = 0.5;

    public static string Apply(string data, Affine matrix)
    {
        var geometry = new PathGeometry();
        PathFigureCollectionConverter.ParseStringToPathFigureCollection(geometry.Figures, data);
        if (matrix.IsIdentity)
            return data;

        var text = new StringBuilder();
        foreach (var figure in geometry.Figures)
            EmitFigure(text, figure, matrix);

        return text.ToString();
    }

    static void EmitFigure(StringBuilder text, PathFigure figure, Affine matrix)
    {
        text.Append(text.Length == 0 ? "M" : " M");
        Append(text, matrix.Map(figure.StartPoint));
        var current = figure.StartPoint;
        foreach (var segment in figure.Segments)
            current = EmitSegment(text, current, segment, matrix);

        if (figure.IsClosed)
            text.Append(" Z");
    }

    static Point EmitSegment(StringBuilder text, Point current, PathSegment segment, Affine matrix)
        => segment switch
        {
            LineSegment line => Line(text, matrix, line.Point),
            PolyLineSegment poly => Lines(text, current, poly.Points, matrix),
            BezierSegment cubic => Cubic(text, matrix, cubic.Point1, cubic.Point2, cubic.Point3),
            PolyBezierSegment cubics => Cubics(text, current, cubics.Points, matrix),
            QuadraticBezierSegment quad => Quad(text, matrix, quad.Point1, quad.Point2),
            PolyQuadraticBezierSegment quads => Quads(text, current, quads.Points, matrix),
            ArcSegment arc => Arc(text, current, arc, matrix),
            _ => throw new FormatException($"The segment {segment.GetType().Name} is not path data."),
        };

    static Point Line(StringBuilder text, Affine matrix, Point point)
    {
        text.Append(" L");
        Append(text, matrix.Map(point));
        return point;
    }

    static Point Lines(StringBuilder text, Point current, PointCollection points, Affine matrix)
    {
        foreach (var point in points)
            current = Line(text, matrix, point);

        return current;
    }

    static Point Cubic(StringBuilder text, Affine matrix, Point p1, Point p2, Point p3)
    {
        text.Append(" C");
        Append(text, matrix.Map(p1));
        text.Append(' ');
        Append(text, matrix.Map(p2));
        text.Append(' ');
        Append(text, matrix.Map(p3));
        return p3;
    }

    static Point Cubics(StringBuilder text, Point current, PointCollection points, Affine matrix)
    {
        if (points.Count % 3 != 0)
            throw new FormatException("A cubic sequence must have groups of three points.");

        for (var i = 0; i < points.Count; i += 3)
            current = Cubic(text, matrix, points[i], points[i + 1], points[i + 2]);

        return current;
    }

    static Point Quad(StringBuilder text, Affine matrix, Point p1, Point p2)
    {
        text.Append(" Q");
        Append(text, matrix.Map(p1));
        text.Append(' ');
        Append(text, matrix.Map(p2));
        return p2;
    }

    static Point Quads(StringBuilder text, Point current, PointCollection points, Affine matrix)
    {
        if (points.Count % 2 != 0)
            throw new FormatException("A quadratic sequence must have groups of two points.");

        for (var i = 0; i < points.Count; i += 2)
            current = Quad(text, matrix, points[i], points[i + 1]);

        return current;
    }

    static Point Arc(StringBuilder text, Point start, ArcSegment arc, Affine matrix)
    {
        if (!TryBasis(start, arc, out var basis))
            return Line(text, matrix, arc.Point);

        EmitSweep(text, basis, matrix, basis.Theta, basis.Sweep, start, final: true, depth: 0);
        return arc.Point;
    }

    static void EmitSweep(
        StringBuilder text,
        ArcBasis basis,
        Affine matrix,
        double theta,
        double sweep,
        Point start,
        bool final,
        int depth)
    {
        if (Math.Abs(sweep) < 1e-8)
            return;

        var first = Math.Abs(theta - basis.Theta) <= 1e-8;
        if (depth < 12 && (Math.Abs(sweep) > Math.PI / 2 || Misses(basis, matrix, theta, sweep, first ? start : null)))
        {
            EmitSweep(text, basis, matrix, theta, sweep / 2, start, final: false, depth + 1);
            EmitSweep(text, basis, matrix, theta + sweep / 2, sweep / 2, start, final, depth + 1);
            return;
        }

        EmitCubic(text, basis, matrix, theta, sweep, first ? start : null, final);
    }

    static void EmitCubic(
        StringBuilder text,
        ArcBasis basis,
        Affine matrix,
        double theta,
        double sweep,
        Point? start,
        bool final)
    {
        var k = 4d / 3d * Math.Tan(sweep / 4);
        var p0 = start ?? OnEllipse(basis, theta);
        var end = OnEllipse(basis, theta + sweep);
        var p3 = final && Math.Abs(theta + sweep - basis.Theta - basis.Sweep) <= 1e-6 ? basis.End : end;
        var d0 = Deriv(basis, theta);
        var d3 = Deriv(basis, theta + sweep);
        Cubic(
            text,
            matrix,
            new Point(p0.X + k * d0.X, p0.Y + k * d0.Y),
            new Point(end.X - k * d3.X, end.Y - k * d3.Y),
            p3);
    }

    static bool Misses(ArcBasis basis, Affine matrix, double theta, double sweep, Point? start)
    {
        var k = 4d / 3d * Math.Tan(sweep / 4);
        var p0 = start ?? OnEllipse(basis, theta);
        var p3 = OnEllipse(basis, theta + sweep);
        var d0 = Deriv(basis, theta);
        var d3 = Deriv(basis, theta + sweep);
        var p1 = new Point(p0.X + k * d0.X, p0.Y + k * d0.Y);
        var p2 = new Point(p3.X - k * d3.X, p3.Y - k * d3.Y);
        var mid = CubicAt(p0, p1, p2, p3, 0.5);
        var truth = OnEllipse(basis, theta + sweep / 2);
        var error = matrix.Map(mid);
        var aimed = matrix.Map(truth);
        var dx = error.X - aimed.X;
        var dy = error.Y - aimed.Y;
        return dx * dx + dy * dy > HalfPoint * HalfPoint;
    }

    static Point OnEllipse(ArcBasis basis, double theta)
    {
        var x = basis.Rx * Math.Cos(theta);
        var y = basis.Ry * Math.Sin(theta);
        return new Point(
            basis.Cos * x - basis.Sin * y + basis.Cx,
            basis.Sin * x + basis.Cos * y + basis.Cy);
    }

    static Point Deriv(ArcBasis basis, double theta)
    {
        var dx = -basis.Rx * Math.Sin(theta);
        var dy = basis.Ry * Math.Cos(theta);
        return new Point(
            basis.Cos * dx - basis.Sin * dy,
            basis.Sin * dx + basis.Cos * dy);
    }

    static Point CubicAt(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        var uu = u * u;
        var tt = t * t;
        return new Point(
            uu * u * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + tt * t * p3.X,
            uu * u * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + tt * t * p3.Y);
    }

    static bool TryBasis(Point start, ArcSegment arc, out ArcBasis basis)
    {
        basis = default;
        var rx = Math.Abs(arc.Size.Width);
        var ry = Math.Abs(arc.Size.Height);
        var dx = start.X - arc.Point.X;
        var dy = start.Y - arc.Point.Y;
        if (rx < 1e-6 || ry < 1e-6 || dx * dx + dy * dy < 1e-12)
            return false;

        var phi = arc.RotationAngle * Math.PI / 180;
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);
        var x1p = cos * (dx / 2) + sin * (dy / 2);
        var y1p = -sin * (dx / 2) + cos * (dy / 2);
        rx = FitRadius(rx, ry, x1p, y1p, out ry);

        var coeff = CenterScale(rx, ry, x1p, y1p);
        var clockwise = arc.SweepDirection == SweepDirection.Clockwise;
        if (arc.IsLargeArc == clockwise)
            coeff = -coeff;

        var cxp = coeff * rx * y1p / ry;
        var cyp = coeff * -ry * x1p / rx;
        var cx = cos * cxp - sin * cyp + (start.X + arc.Point.X) / 2;
        var cy = sin * cxp + cos * cyp + (start.Y + arc.Point.Y) / 2;
        var theta = VectorAngle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        var sweep = VectorAngle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (!clockwise && sweep > 0)
            sweep -= 2 * Math.PI;
        if (clockwise && sweep < 0)
            sweep += 2 * Math.PI;

        basis = new ArcBasis(cx, cy, rx, ry, cos, sin, theta, sweep, arc.Point);
        return true;
    }

    static double FitRadius(double rx, double ry, double x1p, double y1p, out double fittedRy)
    {
        var lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
        fittedRy = ry;
        if (lambda <= 1)
            return rx;

        var scale = Math.Sqrt(lambda);
        fittedRy = ry * scale;
        return rx * scale;
    }

    static double CenterScale(double rx, double ry, double x1p, double y1p)
    {
        var rx2 = rx * rx;
        var ry2 = ry * ry;
        var num = rx2 * ry2 - rx2 * y1p * y1p - ry2 * x1p * x1p;
        var den = rx2 * y1p * y1p + ry2 * x1p * x1p;
        return den <= 0 ? 0 : Math.Sqrt(Math.Max(0, num / den));
    }

    static double VectorAngle(double ux, double uy, double vx, double vy)
    {
        var lu = Math.Sqrt(ux * ux + uy * uy);
        var lv = Math.Sqrt(vx * vx + vy * vy);
        if (lu < 1e-12 || lv < 1e-12)
            return 0;

        var dot = Math.Clamp((ux * vx + uy * vy) / (lu * lv), -1, 1);
        return (ux * vy - uy * vx < 0 ? -1 : 1) * Math.Acos(dot);
    }

    static void Append(StringBuilder text, Point point)
    {
        text.Append(Format(point.X));
        text.Append(' ');
        text.Append(Format(point.Y));
    }

    static string Format(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);

    readonly struct ArcBasis(
        double cx,
        double cy,
        double rx,
        double ry,
        double cos,
        double sin,
        double theta,
        double sweep,
        Point end)
    {
        public double Cx { get; } = cx;
        public double Cy { get; } = cy;
        public double Rx { get; } = rx;
        public double Ry { get; } = ry;
        public double Cos { get; } = cos;
        public double Sin { get; } = sin;
        public double Theta { get; } = theta;
        public double Sweep { get; } = sweep;
        public Point End { get; } = end;
    }
}
