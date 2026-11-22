namespace Reactor.Motion;

/// <summary>
/// Shared durations for recipes. A recipe still starts at 300 ms until it calls
/// <see cref="Motion.WithDuration"/>.
/// </summary>
public static class MotionTime
{
    /// <summary>160 ms. A short fade or color change.</summary>
    public const uint Fast = 160;

    /// <summary>280 ms. A typical enter or exit.</summary>
    public const uint Normal = 280;

    /// <summary>400 ms. A large move or a deliberate emphasis.</summary>
    public const uint Slow = 400;
}
