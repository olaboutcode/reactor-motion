using Reactor.Motion;

namespace Reactor.Motion.Internals;

/// <summary>Builds the motion an item plays after its place changes.</summary>
internal static class FlipMotion
{
    public static Motion Fade(bool inbound, Motion timing)
        => Time(inbound ? Motion.None.FadeIn() : Motion.None.FadeOut(), timing);

    /// <summary>
    /// The caller's move recipe. <see cref="Motion.FromPlace"/> adds translation
    /// from the invert to rest on each axis that moved. A quiet axis is left off
    /// so another player can keep it. Null when the recipe has no tracks.
    /// </summary>
    public static Motion? Move(Motion recipe, double x, double y)
    {
        var body = recipe;
        if (recipe.Place)
        {
            if (Math.Abs(x) >= FlipPlan.Epsilon)
                body = body.TranslateX(x, 0);
            if (Math.Abs(y) >= FlipPlan.Epsilon)
                body = body.TranslateY(y, 0);
        }

        return body.Tracks.Count == 0 ? null : body;
    }

    /// <summary>
    /// <see cref="Move"/> plus a 0–1 reveal track when <paramref name="reveal"/> is set.
    /// The track carries the recipe timing when the move itself has nothing to play.
    /// Null when there is nothing to run.
    /// </summary>
    public static Motion? Reveal(Motion recipe, double x, double y, bool reveal)
    {
        var body = Move(recipe, x, y);
        if (!reveal)
            return body;

        var seed = (body ?? Motion.None).Track(LayoutMotionHost.RevealProperty, 0d, 1d);
        return body is null ? Time(seed, recipe) : seed;
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
