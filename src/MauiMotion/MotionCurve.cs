namespace Reactor.Motion;

/// <summary>Easing curves beyond the built-in <see cref="Easing"/> presets.</summary>
public static class MotionCurve
{
    /// <summary>
    /// Cubic-bezier easing. The controls match CSS <c>cubic-bezier(x1, y1, x2, y2)</c>.
    /// X controls are clamped to 0–1. Y may leave 0–1 so the curve can overshoot.
    /// </summary>
    public static Easing CubicBezier(double x1, double y1, double x2, double y2)
    {
        x1 = ClampUnit(x1);
        x2 = ClampUnit(x2);
        y1 = Sanitize(y1);
        y2 = Sanitize(y2);
        return new Easing(t => YAt(t, x1, y1, x2, y2));
    }

    internal static double YAt(double t, double x1, double y1, double x2, double y2)
    {
        if (double.IsNaN(t) || double.IsInfinity(t) || t <= 0)
            return 0;
        if (t >= 1)
            return 1;

        var u = t;
        for (var i = 0; i < 8; i++)
        {
            var x = Bezier(u, x1, x2);
            var slope = BezierSlope(u, x1, x2);
            if (Math.Abs(x - t) < 1e-6)
                break;
            if (Math.Abs(slope) < 1e-6)
                break;
            u = Math.Clamp(u - ((x - t) / slope), 0, 1);
        }

        return Bezier(u, y1, y2);
    }

    static double Bezier(double t, double c1, double c2)
    {
        var u = 1 - t;
        return (3 * u * u * t * c1) + (3 * u * t * t * c2) + (t * t * t);
    }

    static double BezierSlope(double t, double c1, double c2)
    {
        var u = 1 - t;
        return (3 * u * u * c1) + (6 * u * t * (c2 - c1)) + (3 * t * t * (1 - c2));
    }

    static double ClampUnit(double value)
    {
        value = Sanitize(value);
        if (value < 0)
            return 0;
        if (value > 1)
            return 1;
        return value;
    }

    static double Sanitize(double value)
        => double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
}
