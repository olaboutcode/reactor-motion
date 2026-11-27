namespace Reactor.Motion.Internals;

/// <summary>
/// What one layout pass should do. <c>Record</c> stores the frame.
/// <c>Play</c> runs the recipe. <c>X</c> and <c>Y</c> are the translation that
/// holds the view on its old visual position. <c>Reveal</c> opens a clip from
/// the previous arranged size to the new one.
/// </summary>
internal readonly record struct LayoutStep(
    bool Record,
    bool Play,
    double X,
    double Y,
    bool Reveal = false,
    double FromWidth = 0,
    double FromHeight = 0,
    double ToWidth = 0,
    double ToHeight = 0);

/// <summary>
/// Follow-up to a layout pass. The first finite frame is stored and does not play.
/// A later move or size change under half a device-independent pixel stays put.
/// A larger size change reveals the new size. The translation still comes from the origin change.
/// </summary>
internal static class LayoutPlan
{
    public static LayoutStep Next(
        bool recorded,
        FlipFrame previous,
        FlipFrame next,
        double translationX,
        double translationY)
    {
        if (!Ready(next))
            return new LayoutStep(false, false, 0, 0);
        if (!recorded)
            return new LayoutStep(true, false, 0, 0);

        var moved = !FlipPlan.SamePlace(previous, next);
        var reveal = SizeChanged(previous, next);
        if (!moved && !reveal)
            return new LayoutStep(true, false, 0, 0);

        var (x, y) = moved
            ? FlipPlan.Invert(previous, translationX, translationY, next)
            : (translationX, translationY);
        return new LayoutStep(
            true,
            true,
            x,
            y,
            reveal,
            previous.Width,
            previous.Height,
            next.Width,
            next.Height);
    }

    static bool SizeChanged(FlipFrame previous, FlipFrame next)
        => Math.Abs(previous.Width - next.Width) >= FlipPlan.Epsilon
            || Math.Abs(previous.Height - next.Height) >= FlipPlan.Epsilon;

    static bool Ready(FlipFrame frame)
        => FlipPlan.Finite(frame) && frame.Width >= 0 && frame.Height >= 0;
}
