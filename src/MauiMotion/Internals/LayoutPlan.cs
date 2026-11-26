namespace Reactor.Motion.Internals;

/// <summary>
/// What one layout pass should do. <c>Record</c> stores the frame.
/// <c>Play</c> slides from the previous origin. <c>X</c> and <c>Y</c> are the
/// translation that holds the view on its old visual position.
/// </summary>
internal readonly record struct LayoutStep(bool Record, bool Play, double X, double Y);

/// <summary>
/// Position-only follow-up to a layout pass. The first finite frame is stored
/// and does not play. A later move under half a device-independent pixel stays put.
/// Width and height never start a play.
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
        if (!recorded || FlipPlan.SamePlace(previous, next))
            return new LayoutStep(true, false, 0, 0);

        var (x, y) = FlipPlan.Invert(previous, translationX, translationY, next);
        return new LayoutStep(true, true, x, y);
    }

    static bool Ready(FlipFrame frame)
        => FlipPlan.Finite(frame) && frame.Width >= 0 && frame.Height >= 0;
}
