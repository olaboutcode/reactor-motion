using MauiReactor;

namespace Reactor.Motion;

/// <summary>
/// MauiReactor node extensions for binding a motion.
/// </summary>
public static class VisualNodeExtensions
{
    /// <summary>
    /// Binds <paramref name="motion"/> on Loaded. Does not start playback
    /// (even if <paramref name="onBind"/> is omitted). Call <c>Forward()</c> or
    /// <c>ForwardAsync()</c>.
    /// </summary>
    public static VisualNode BindMotion(
        this VisualNode node,
        Motion motion,
        Action<MotionPlayer>? onBind = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return new MotionElement(motion, onBind)
        {
            node
        };
    }
}
