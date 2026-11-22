using Reactor.Motion;

namespace Reactor.Motion.Internals;

/// <summary>Builds the translation that a list row plays after a layout change.</summary>
internal static class FlipMotion
{
    public static void RejectTracks(Motion timing)
    {
        if (timing.Tracks.Count > 0)
        {
            System.Diagnostics.Debug.Assert(
                false,
                "Collection move supplies timing. Tracks on it are ignored.");
        }
    }

    public static Motion Fade(bool inbound, Motion timing)
        => Time(inbound ? Motion.None.FadeIn() : Motion.None.FadeOut(), timing);

    /// <summary>
    /// Translation from the invert to rest. A quiet axis is left off so another
    /// player can keep it. Null when neither axis moved.
    /// </summary>
    public static Motion? Shift(Motion timing, double x, double y)
    {
        Motion? body = null;
        if (Math.Abs(x) >= FlipPlan.Epsilon)
            body = Motion.None.TranslateX(x, 0);
        if (Math.Abs(y) >= FlipPlan.Epsilon)
            body = (body ?? Motion.None).TranslateY(y, 0);
        return body is null ? null : Time(body, timing);
    }

    public static Motion Time(Motion body, Motion timing)
    {
        var motion = timing.Spring is Spring spring
            ? body.WithSpring(spring, timing.SpringVelocity)
            : body.WithDuration(timing.Duration).WithEasing(timing.Easing);
        if (timing.Delay != 0)
            motion = motion.WithDelay(timing.Delay);
        if (timing.Rate != 1)
            motion = motion.WithRate(timing.Rate);
        if (timing.ReduceMotion is bool reduce)
            motion = motion.WithReduceMotion(reduce);
        return motion;
    }
}
