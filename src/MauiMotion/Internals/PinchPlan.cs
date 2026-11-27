namespace Reactor.Motion.Internals;

/// <summary>
/// Maps a finger span and a finger angle onto recipe progress.
/// Progress 0 is the from pose and 1 is the to pose.
/// </summary>
internal static class PinchPlan
{
    public static double Scale(double origin, double ratio, double from, double to)
    {
        origin = Unit(origin);
        if (!double.IsFinite(ratio) || ratio <= 0)
            ratio = 1;
        if (!double.IsFinite(from) || !double.IsFinite(to) || Math.Abs(to - from) < 1e-9)
            return origin;

        var next = (from + ((to - from) * origin)) * ratio;
        if (!double.IsFinite(next))
            return origin;
        return Unit((next - from) / (to - from));
    }

    public static double Turn(double origin, double degrees, double from, double to)
    {
        origin = Unit(origin);
        if (!double.IsFinite(degrees))
            degrees = 0;
        if (!double.IsFinite(from) || !double.IsFinite(to) || Math.Abs(to - from) < 1e-9)
            return origin;

        var next = from + ((to - from) * origin) + degrees;
        if (!double.IsFinite(next))
            return origin;
        return Unit((next - from) / (to - from));
    }

    public static bool TrySpan(Motion motion, BindableProperty property, out double from, out double to)
    {
        foreach (var track in motion.Tracks)
        {
            if (track.Property != property || track.From is not double start || track.To is not double end)
                continue;
            from = start;
            to = end;
            return true;
        }

        from = 0;
        to = 0;
        return false;
    }

    static double Unit(double value)
        => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
}

/// <summary>
/// Turns one MAUI pinch event into a finger-span ratio. The ratio starts at 1.
/// MAUI reports <c>1 + step * view.Scale</c>. Android's step is a per-event
/// factor. iOS and Mac Catalyst add slices of one cumulative factor.
/// </summary>
internal static class PinchSpan
{
    public static double Apply(double ratio, double reported, double viewScale, bool factor)
    {
        if (!double.IsFinite(ratio) || ratio <= 0)
            ratio = 1;
        var basis = double.IsFinite(viewScale) && Math.Abs(viewScale) > 1e-6 ? viewScale : 1;
        if (!double.IsFinite(reported))
            return ratio;

        var step = (reported - 1d) / basis;
        if (!double.IsFinite(step))
            return ratio;

        var next = factor ? ratio * (1d + step) : ratio + step;
        return double.IsFinite(next) && next > 0 ? next : ratio;
    }
}
