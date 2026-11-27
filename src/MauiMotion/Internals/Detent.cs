namespace Reactor.Motion.Internals;

/// <summary>
/// Stop choice for a drag release. Progress units, clamped to 0–1.
/// </summary>
internal static class Detent
{
    /// <summary>Progress per second. A faster release, toward a neighbor, targets that neighbor.</summary>
    internal const double Flick = 2;

    internal static readonly double[] Ends = [0d, 1d];

    internal static double[] Normalize(IReadOnlyList<double>? stops)
    {
        if (stops is null || stops.Count == 0)
            return Ends;

        var kept = new List<double>(stops.Count);
        for (var i = 0; i < stops.Count; i++)
        {
            var value = stops[i];
            if (double.IsNaN(value) || double.IsInfinity(value))
                continue;
            kept.Add(Math.Clamp(value, 0, 1));
        }

        if (kept.Count < 2)
            return Ends;

        kept.Sort();
        var distinct = 1;
        for (var i = 1; i < kept.Count; i++)
        {
            if (kept[i] - kept[distinct - 1] <= 1e-9)
                continue;
            kept[distinct++] = kept[i];
        }

        if (distinct < 2)
            return Ends;

        var result = new double[distinct];
        for (var i = 0; i < distinct; i++)
            result[i] = kept[i];
        return result;
    }

    internal static double Choose(double progress, double velocity, double[]? stops)
    {
        var span = Normalize(stops);
        if (velocity > Flick)
        {
            var above = NeighborAbove(span, progress);
            if (above is double next)
                return next;
        }
        else if (velocity < -Flick)
        {
            var below = NeighborBelow(span, progress);
            if (below is double next)
                return next;
        }

        return Nearest(span, progress);
    }

    static double? NeighborAbove(double[] span, double progress)
    {
        foreach (var stop in span)
        {
            if (stop > progress + 1e-9)
                return stop;
        }

        return null;
    }

    static double? NeighborBelow(double[] span, double progress)
    {
        for (var i = span.Length - 1; i >= 0; i--)
        {
            if (span[i] < progress - 1e-9)
                return span[i];
        }

        return null;
    }

    static double Nearest(double[] span, double progress)
    {
        var best = span[0];
        var bestDist = Math.Abs(progress - best);
        for (var i = 1; i < span.Length; i++)
        {
            var dist = Math.Abs(progress - span[i]);
            if (dist <= bestDist + 1e-12)
            {
                best = span[i];
                bestDist = dist;
            }
        }

        return best;
    }
}
