using Reactor.Motion;
using Microsoft.Maui.Controls.Shapes;

namespace Reactor.Motion.Internals;

/// <summary>
/// Arc-length table for a <see cref="Microsoft.Maui.Controls.Shapes.PathGeometry"/>.
/// Progress maps to distance along the flattened path, so a long segment and a
/// short one take time in proportion to their length. A <c>Z</c> adds the closing
/// edge when the pen is not already on the start. A later figure is a pen lift:
/// that gap has no length, and the view jumps.
/// </summary>
internal sealed class PathSampler
{
    /// <summary>A chord within this distance of the curve is flat enough.</summary>
    const double HalfPoint = 0.5;

    readonly Point[] _points;
    readonly double[] _cum;
    readonly bool[] _lift;
    readonly double _length;
    int _segment = 1;

    PathSampler(Point[] points, double[] cum, bool[] lift, double length)
    {
        _points = points;
        _cum = cum;
        _lift = lift;
        _length = length;
    }

    public static PathSampler? TryCreate(PathGeometry geometry)
    {
        var points = new List<Point>();
        var lifts = new List<bool>();
        foreach (var figure in geometry.Figures)
            AppendFigure(points, lifts, figure);
        if (points.Count < 2)
            return null;

        var cum = new double[points.Count];
        var length = 0d;
        for (var i = 1; i < points.Count; i++)
        {
            if (!lifts[i - 1])
                length += Distance(points[i - 1], points[i]);
            cum[i] = length;
        }

        if (length <= 0)
            return null;

        return new PathSampler([.. points], cum, [.. lifts], length);
    }

    public Point PointAt(double t)
    {
        var (a, b, s) = Place(t);
        return new Point(a.X + ((b.X - a.X) * s), a.Y + ((b.Y - a.Y) * s));
    }

    /// <summary>
    /// Clockwise degrees from the positive X axis. UI Y grows downward, so this
    /// matches <see cref="VisualElement.Rotation"/>.
    /// </summary>
    public double HeadingAt(double t)
    {
        var (a, b, _) = Place(t);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
            return 0;
        return Math.Atan2(dy, dx) * (180.0 / Math.PI);
    }

    (Point A, Point B, double S) Place(double t)
    {
        t = double.IsNaN(t) || double.IsInfinity(t) ? 0 : Math.Clamp(t, 0, 1);
        var d = t * _length;
        if (d <= 0)
            return Start();
        if (d >= _length)
            return End();

        var i = Resume(d);
        var span = _cum[i] - _cum[i - 1];
        var s = span <= 1e-12 ? 0 : (d - _cum[i - 1]) / span;
        if (s >= 1)
            i = AfterLift(i, ref s);
        _segment = i;
        return (_points[i - 1], _points[i], s);
    }

    int Resume(double d)
    {
        var i = _segment;
        if ((uint)(i - 1) < (uint)(_cum.Length - 1)
            && !_lift[i - 1]
            && _cum[i - 1] <= d
            && d <= _cum[i])
        {
            return i;
        }

        var lo = 1;
        var hi = _cum.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_cum[mid] < d)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    int AfterLift(int i, ref double s)
    {
        if (i >= _lift.Length || !_lift[i])
            return i;

        var start = i;
        while (start < _lift.Length && _lift[start])
            start++;
        if (start >= _lift.Length)
            return i;

        s = 0;
        return start + 1;
    }

    (Point A, Point B, double S) Start()
    {
        for (var i = 1; i < _points.Length; i++)
        {
            if (_lift[i - 1] || _cum[i] <= _cum[i - 1])
                continue;
            _segment = i;
            return (_points[i - 1], _points[i], 0);
        }

        return End();
    }

    (Point A, Point B, double S) End()
    {
        var i = _points.Length - 1;
        while (i > 1 && (_lift[i - 1] || _cum[i] <= _cum[i - 1]))
            i--;
        _segment = i;
        return (_points[i - 1], _points[i], 1);
    }

    static void AppendFigure(List<Point> points, List<bool> lifts, PathFigure figure)
    {
        var start = figure.StartPoint;
        if (points.Count > 0)
            lifts.Add(true);
        points.Add(start);

        var current = start;
        foreach (var segment in figure.Segments)
            current = AppendSegment(points, lifts, current, segment);

        if (!figure.IsClosed || Distance(current, start) <= 1e-4)
            return;

        lifts.Add(false);
        points.Add(start);
    }

    static Point AppendSegment(List<Point> points, List<bool> lifts, Point current, PathSegment segment)
    {
        switch (segment)
        {
            case LineSegment line:
                AddLine(points, lifts, line.Point);
                return line.Point;
            case PolyLineSegment poly:
                foreach (var point in poly.Points)
                    AddLine(points, lifts, point);
                return poly.Points.Count == 0 ? current : poly.Points[^1];
            case BezierSegment cubic:
                AddCubic(points, lifts, current, cubic.Point1, cubic.Point2, cubic.Point3, 0);
                return cubic.Point3;
            case PolyBezierSegment polyCubic:
                return AddPolyCubic(points, lifts, current, polyCubic.Points);
            case QuadraticBezierSegment quad:
                AddQuad(points, lifts, current, quad.Point1, quad.Point2, 0);
                return quad.Point2;
            case PolyQuadraticBezierSegment polyQuad:
                return AddPolyQuad(points, lifts, current, polyQuad.Points);
            case ArcSegment arc:
                AddArc(points, lifts, current, arc);
                return arc.Point;
            default:
                return current;
        }
    }

    static Point AddPolyCubic(List<Point> points, List<bool> lifts, Point current, PointCollection controls)
    {
        for (var i = 0; i + 2 < controls.Count; i += 3)
        {
            var end = controls[i + 2];
            AddCubic(points, lifts, current, controls[i], controls[i + 1], end, 0);
            current = end;
        }

        return current;
    }

    static Point AddPolyQuad(List<Point> points, List<bool> lifts, Point current, PointCollection controls)
    {
        for (var i = 0; i + 1 < controls.Count; i += 2)
        {
            var end = controls[i + 1];
            AddQuad(points, lifts, current, controls[i], end, 0);
            current = end;
        }

        return current;
    }

    static void AddLine(List<Point> points, List<bool> lifts, Point point)
    {
        lifts.Add(false);
        points.Add(point);
    }

    static void AddArc(List<Point> points, List<bool> lifts, Point start, ArcSegment arc)
    {
        var end = arc.Point;
        var rx = Math.Abs(arc.Size.Width);
        var ry = Math.Abs(arc.Size.Height);
        if (rx < 1e-6 || ry < 1e-6 || Distance(start, end) < 1e-6)
        {
            AddLine(points, lifts, end);
            return;
        }

        var phi = arc.RotationAngle * Math.PI / 180;
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);
        var dx = (start.X - end.X) / 2;
        var dy = (start.Y - end.Y) / 2;
        var x1p = cos * dx + sin * dy;
        var y1p = -sin * dx + cos * dy;

        var lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
        if (lambda > 1)
        {
            var scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        var rx2 = rx * rx;
        var ry2 = ry * ry;
        var num = rx2 * ry2 - rx2 * y1p * y1p - ry2 * x1p * x1p;
        var den = rx2 * y1p * y1p + ry2 * x1p * x1p;
        var coeff = den <= 0 ? 0 : Math.Sqrt(Math.Max(0, num / den));
        var clockwise = arc.SweepDirection == SweepDirection.Clockwise;
        if (arc.IsLargeArc == clockwise)
            coeff = -coeff;

        var cxp = coeff * rx * y1p / ry;
        var cyp = coeff * -ry * x1p / rx;
        var cx = cos * cxp - sin * cyp + (start.X + end.X) / 2;
        var cy = sin * cxp + cos * cyp + (start.Y + end.Y) / 2;

        var theta1 = VectorAngle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        var dtheta = VectorAngle(
            (x1p - cxp) / rx, (y1p - cyp) / ry,
            (-x1p - cxp) / rx, (-y1p - cyp) / ry);

        if (!clockwise && dtheta > 0)
            dtheta -= 2 * Math.PI;
        if (clockwise && dtheta < 0)
            dtheta += 2 * Math.PI;

        var steps = ArcSteps(Math.Max(rx, ry), dtheta);
        for (var i = 1; i <= steps; i++)
        {
            if (i == steps)
            {
                AddLine(points, lifts, end);
                return;
            }

            var theta = theta1 + dtheta * (i / (double)steps);
            var x = rx * Math.Cos(theta);
            var y = ry * Math.Sin(theta);
            AddLine(points, lifts, new Point(
                cos * x - sin * y + cx,
                sin * x + cos * y + cy));
        }
    }

    static int ArcSteps(double radius, double sweep)
    {
        var turn = Math.Abs(sweep);
        if (turn < 1e-6 || radius <= HalfPoint)
            return 1;

        var cosHalf = Math.Clamp(1 - (HalfPoint / radius), -1, 1);
        var maxTheta = 2 * Math.Acos(cosHalf);
        if (maxTheta < 1e-4)
            maxTheta = 1e-4;
        var steps = (int)Math.Ceiling(turn / maxTheta);
        return steps < 1 ? 1 : steps;
    }

    static double VectorAngle(double ux, double uy, double vx, double vy)
    {
        var sign = ux * vy - uy * vx < 0 ? -1 : 1;
        var lu = Math.Sqrt(ux * ux + uy * uy);
        var lv = Math.Sqrt(vx * vx + vy * vy);
        if (lu < 1e-12 || lv < 1e-12)
            return 0;
        var c = Math.Clamp((ux * vx + uy * vy) / (lu * lv), -1, 1);
        return sign * Math.Acos(c);
    }

    static void AddCubic(
        List<Point> points,
        List<bool> lifts,
        Point p0,
        Point p1,
        Point p2,
        Point p3,
        int depth)
    {
        if (depth >= 16 || (ChordNear(p1, p0, p3) && ChordNear(p2, p0, p3)))
        {
            AddLine(points, lifts, p3);
            return;
        }

        var p01 = Mid(p0, p1);
        var p12 = Mid(p1, p2);
        var p23 = Mid(p2, p3);
        var p012 = Mid(p01, p12);
        var p123 = Mid(p12, p23);
        var mid = Mid(p012, p123);
        AddCubic(points, lifts, p0, p01, p012, mid, depth + 1);
        AddCubic(points, lifts, mid, p123, p23, p3, depth + 1);
    }

    static void AddQuad(List<Point> points, List<bool> lifts, Point p0, Point p1, Point p2, int depth)
    {
        if (depth >= 16 || ChordNear(p1, p0, p2))
        {
            AddLine(points, lifts, p2);
            return;
        }

        var p01 = Mid(p0, p1);
        var p12 = Mid(p1, p2);
        var mid = Mid(p01, p12);
        AddQuad(points, lifts, p0, p01, mid, depth + 1);
        AddQuad(points, lifts, mid, p12, p2, depth + 1);
    }

    static bool ChordNear(Point control, Point start, Point end)
        => DistanceToChord(control, start, end) <= HalfPoint;

    static double DistanceToChord(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 <= 1e-12)
            return Distance(point, start);

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / len2;
        t = Math.Clamp(t, 0, 1);
        var x = start.X + t * dx;
        var y = start.Y + t * dy;
        var ox = point.X - x;
        var oy = point.Y - y;
        return Math.Sqrt(ox * ox + oy * oy);
    }

    static Point Mid(Point a, Point b)
        => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
