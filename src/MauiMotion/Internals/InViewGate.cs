namespace Reactor.Motion.Internals;

/// <summary>
/// Decides when an <c>InView</c> child has crossed into the scroll viewport.
/// </summary>
internal static class InViewGate
{
    /// <summary>Visible overlap required, as a fraction of the smaller area.</summary>
    internal const double Half = 0.5;

    /// <summary>
    /// True when the overlap is at least half the child, or half the viewport
    /// when the child is larger than the viewport. A zero-area rect never crosses.
    /// </summary>
    internal static bool Crosses(Rect child, Rect viewport)
    {
        if (!HasArea(child) || !HasArea(viewport))
            return false;

        var visible = IntersectionArea(child, viewport);
        var limit = Math.Min(child.Width * child.Height, viewport.Width * viewport.Height);
        return visible + 0.0001 >= Half * limit;
    }

    /// <summary>The nearest <see cref="ScrollView"/> above <paramref name="start"/>.</summary>
    internal static ScrollView? Ancestor(Element? start)
    {
        var current = start;
        while (current is not null)
        {
            if (current is ScrollView scroll)
                return scroll;
            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// <paramref name="element"/> in <paramref name="content"/> coordinates.
    /// Offsets stop at <paramref name="content"/>, so the scroll view's own origin is left out.
    /// </summary>
    internal static Rect InContent(VisualElement element, Element content)
    {
        double x = 0;
        double y = 0;
        var current = element;
        while (current is not null && !ReferenceEquals(current, content))
        {
            x += current.X;
            y += current.Y;
            current = current.Parent as VisualElement;
        }

        return new Rect(x, y, element.Width, element.Height);
    }

    /// <summary>The scroll view's visible window in content coordinates.</summary>
    internal static Rect Viewport(ScrollView scroll)
        => new(scroll.ScrollX, scroll.ScrollY, scroll.Width, scroll.Height);

    static bool HasArea(Rect rect)
        => rect.Width > 0
            && rect.Height > 0
            && double.IsFinite(rect.X)
            && double.IsFinite(rect.Y)
            && double.IsFinite(rect.Width)
            && double.IsFinite(rect.Height);

    static double IntersectionArea(Rect a, Rect b)
    {
        var width = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
        var height = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        if (width <= 0 || height <= 0)
            return 0;
        return width * height;
    }
}
