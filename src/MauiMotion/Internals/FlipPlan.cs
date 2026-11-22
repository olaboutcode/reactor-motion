namespace Reactor.Motion.Internals;

/// <summary>Where a row sat in the list, in device-independent pixels.</summary>
internal readonly record struct FlipFrame(double X, double Y, double Width, double Height);

/// <summary>What to do with a row after a layout.</summary>
internal enum FlipCue
{
    /// <summary>Remember the frame. The list has not settled yet.</summary>
    Record,

    /// <summary>The row was not in the last settled list.</summary>
    Enter,

    /// <summary>The frame matches the one already stored.</summary>
    Rest,

    /// <summary>The row moved. Slide it from the last frame.</summary>
    Move,
}

/// <summary>
/// Key and frame math for a list. The component measures views and plays the cue.
/// A move under half a device-independent pixel stays put.
/// </summary>
internal static class FlipPlan
{
    public const double Epsilon = 0.5;

    public static bool Equal(object? left, object? right)
        => EqualityComparer<object>.Default.Equals(left, right);

    public static bool Finite(FlipFrame frame)
        => double.IsFinite(frame.X)
            && double.IsFinite(frame.Y)
            && double.IsFinite(frame.Width)
            && double.IsFinite(frame.Height);

    public static bool SamePlace(FlipFrame from, FlipFrame to)
        => Math.Abs(from.X - to.X) < Epsilon && Math.Abs(from.Y - to.Y) < Epsilon;

    /// <summary>
    /// Translation that keeps the row on its current visual position after the
    /// layout frame changes. <paramref name="translationX"/> and
    /// <paramref name="translationY"/> are the translation already on the view.
    /// </summary>
    public static (double X, double Y) Invert(
        FlipFrame from,
        double translationX,
        double translationY,
        FlipFrame to)
        => (from.X + translationX - to.X, from.Y + translationY - to.Y);

    public static FlipCue Cue(bool placed, bool known, bool samePlace)
    {
        if (!known)
            return placed ? FlipCue.Enter : FlipCue.Record;

        if (!placed || samePlace)
            return FlipCue.Rest;

        return FlipCue.Move;
    }

    public static List<object> Removed(IReadOnlyList<object> previous, IReadOnlyList<object> next)
    {
        var live = new HashSet<object>(next, EqualityComparer<object>.Default);
        var removed = new List<object>();
        var seen = new HashSet<object>(EqualityComparer<object>.Default);
        foreach (var key in previous)
        {
            if (!seen.Add(key!))
                continue;
            if (!live.Contains(key))
                removed.Add(key);
        }

        return removed;
    }

    public static List<object> Arrived(IReadOnlyList<object> previous, IReadOnlyList<object> next)
    {
        var had = new HashSet<object>(previous, EqualityComparer<object>.Default);
        var arrived = new List<object>();
        var seen = new HashSet<object>(EqualityComparer<object>.Default);
        foreach (var key in next)
        {
            if (!seen.Add(key!))
                continue;
            if (!had.Contains(key))
                arrived.Add(key);
        }

        return arrived;
    }
}
