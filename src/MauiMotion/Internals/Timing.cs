namespace Reactor.Motion.Internals;

/// <summary>
/// Default timing for a motion: 300 ms <see cref="Easing.CubicOut"/>.
/// </summary>
internal static class Timing
{
    public const uint MotionDuration = 300;

    public static Easing MotionEasing { get; } = Easing.CubicOut;
}
