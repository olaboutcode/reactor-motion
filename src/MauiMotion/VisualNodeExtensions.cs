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

    /// <summary>
    /// Plays <paramref name="enter"/> when <paramref name="present"/> is true and
    /// <paramref name="exit"/> when it becomes false. The child stays in the tree
    /// until the exit run finishes. Keep rendering this node and pass
    /// <paramref name="present"/>. Removing the node unmounts it immediately.
    /// A from/to exit writes its from value when it starts. A to-only exit captures
    /// the live value. If <paramref name="present"/> becomes true during the exit,
    /// that exit reverses back to its from pose.
    /// </summary>
    public static VisualNode EnterExit(
        this VisualNode node,
        bool present,
        Motion enter,
        Motion exit)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(enter);
        ArgumentNullException.ThrowIfNull(exit);
        return new PresenceElement(present, enter, exit)
        {
            node
        };
    }

    /// <summary>
    /// Same as <see cref="EnterExit(VisualNode, bool, Motion, Motion)"/>.
    /// <paramref name="enter"/> and <paramref name="exit"/> receive an empty motion.
    /// </summary>
    public static VisualNode EnterExit(
        this VisualNode node,
        bool present,
        Func<Motion, Motion> enter,
        Func<Motion, Motion> exit)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(enter);
        ArgumentNullException.ThrowIfNull(exit);
        return node.EnterExit(present, enter(Motion.None), exit(Motion.None));
    }

    /// <summary>
    /// Drags the child on the finger axes in <paramref name="plane"/>.
    /// On release each axis springs to its nearer end with <paramref name="spring"/>
    /// and keeps that axis's speed. The pan recognizer sits on the wrapper.
    /// The motions run on the child.
    /// </summary>
    public static VisualNode Drag(
        this VisualNode node,
        Spring spring,
        Func<DragPlane, DragPlane> plane)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plane);
        return new DragElement(plane(new DragPlane()), spring)
        {
            node
        };
    }
}
