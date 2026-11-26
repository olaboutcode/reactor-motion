namespace Reactor.Motion.Internals;

/// <summary>
/// Maps a scroll offset onto the 0–1 progress <c>Scrub</c> seeks.
/// </summary>
internal static class ScrubMap
{
    /// <summary>
    /// 0 when the child's leading edge meets the far side of the viewport.
    /// 1 when its trailing edge has left the near side. Null until both rects have area.
    /// </summary>
    internal static double? Crossing(Rect child, Rect viewport, bool vertical)
    {
        if (!Ready(child) || !Ready(viewport))
            return null;

        double travel;
        double along;
        if (vertical)
        {
            travel = viewport.Height + child.Height;
            along = viewport.Y + viewport.Height - child.Y;
        }
        else
        {
            travel = viewport.Width + child.Width;
            along = viewport.X + viewport.Width - child.X;
        }

        if (travel <= 0)
            return null;
        return Math.Clamp(along / travel, 0, 1);
    }

    /// <summary>
    /// 0 at <paramref name="from"/> and 1 at <paramref name="to"/>. Values outside that span clamp.
    /// A zero span is 1 once <paramref name="offset"/> has reached <paramref name="from"/>.
    /// </summary>
    internal static double Range(double offset, double from, double to)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(from) || !double.IsFinite(to))
            return 0;

        var span = to - from;
        if (Math.Abs(span) < 0.0001)
            return offset >= from ? 1 : 0;
        return Math.Clamp((offset - from) / span, 0, 1);
    }

    /// <summary>
    /// Vertical when the content is taller than the viewport, or when both axes scroll.
    /// Horizontal only when the content is wider and not taller.
    /// </summary>
    internal static bool TracksVertical(
        double contentWidth,
        double contentHeight,
        double viewportWidth,
        double viewportHeight)
    {
        var scrollsX = contentWidth > viewportWidth + 0.5;
        var scrollsY = contentHeight > viewportHeight + 0.5;
        return scrollsY || !scrollsX;
    }

    static bool Ready(Rect rect)
        => rect.Width > 0
            && rect.Height > 0
            && double.IsFinite(rect.X)
            && double.IsFinite(rect.Y)
            && double.IsFinite(rect.Width)
            && double.IsFinite(rect.Height);
}
