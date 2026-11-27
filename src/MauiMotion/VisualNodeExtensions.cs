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
    /// Plays <paramref name="motion"/> when the child has scrolled about halfway
    /// into the nearest <c>ScrollView</c>. A child already that far in view on
    /// load plays immediately. A child with no <c>ScrollView</c> ancestor plays
    /// on load, and still plays once when <paramref name="replay"/> is set.
    /// The from pose is written on load, so a fade below the fold starts at its
    /// from value. Pass <paramref name="replay"/> and the motion arms again once
    /// the overlap falls back below that halfway mark. The pose stays where the
    /// play left it. The next crossing seeks the from pose and plays forward.
    /// It does not restart while the child stays past the mark, and it does not
    /// reverse on the way out. Omit <paramref name="replay"/> and scrolling away
    /// does not play again. Reduce motion finishes when that play starts,
    /// including each later entry.
    /// </summary>
    public static VisualNode InView(this VisualNode node, Motion motion, bool replay = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return new InViewElement(motion, replay)
        {
            node
        };
    }

    /// <summary>
    /// Same as <see cref="InView(VisualNode, Motion, bool)"/>.
    /// <paramref name="motion"/> receives an empty motion.
    /// </summary>
    public static VisualNode InView(this VisualNode node, Func<Motion, Motion> motion, bool replay = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return node.InView(motion(Motion.None), replay);
    }

    /// <summary>
    /// Seeks <paramref name="motion"/> from the nearest <c>ScrollView</c>.
    /// 0 is when the child's leading edge meets the far side of the viewport.
    /// 1 is when its trailing edge has left the near side. The axis is vertical
    /// when the content is taller than the viewport, and horizontal when it is
    /// only wider. The clock does not run. The child sits inside that
    /// <c>ScrollView</c>. With no <c>ScrollView</c>, the seek stays at 0.
    /// Reduce motion seeks to the end on the first read.
    /// </summary>
    public static VisualNode Scrub(this VisualNode node, Motion motion)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return Scrub(node, motion, range: null);
    }

    /// <summary>
    /// Same as <see cref="Scrub(VisualNode, Motion)"/>.
    /// <paramref name="motion"/> receives an empty motion.
    /// </summary>
    public static VisualNode Scrub(this VisualNode node, Func<Motion, Motion> motion)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return node.Scrub(motion(Motion.None));
    }

    /// <summary>
    /// Seeks <paramref name="motion"/> from scroll offset <paramref name="from"/>
    /// to <paramref name="to"/>, in pixels. 0 is <paramref name="from"/> and 1 is
    /// <paramref name="to"/>. Offsets outside that span clamp. The axis is vertical
    /// when the content is taller than the viewport, and horizontal when it is only
    /// wider. The clock does not run. The child sits inside that <c>ScrollView</c>.
    /// With no <c>ScrollView</c>, the seek stays at 0. Reduce motion seeks to the
    /// end on the first read.
    /// </summary>
    public static VisualNode Scrub(this VisualNode node, Motion motion, double from, double to)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return Scrub(node, motion, (from, to));
    }

    /// <summary>
    /// Same as <see cref="Scrub(VisualNode, Motion, double, double)"/>.
    /// <paramref name="motion"/> receives an empty motion.
    /// </summary>
    public static VisualNode Scrub(this VisualNode node, Func<Motion, Motion> motion, double from, double to)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return node.Scrub(motion(Motion.None), from, to);
    }

    static ScrubElement Scrub(VisualNode node, Motion motion, (double From, double To)? range)
        => new ScrubElement(motion, range?.From, range?.To)
        {
            node
        };

    /// <summary>
    /// Plays <paramref name="pressed"/> forward while the pointer is down and
    /// reverses it on release. A second press during that reverse continues from
    /// the live value. The recognizer sits on the wrapper and the motion runs on
    /// the child. Reduce motion finishes the run.
    /// </summary>
    public static VisualNode Press(this VisualNode node, Motion pressed)
        => node.Press(pressed, hover: null);

    /// <summary>
    /// Same as <see cref="Press(VisualNode, Motion, Motion?)"/>.
    /// <paramref name="pressed"/> receives an empty motion.
    /// <paramref name="hover"/> plays on pointer enter and reverses on pointer exit.
    /// Pass null when there is no hover. A finger on a phone does not raise those events, so hover stays quiet.
    /// </summary>
    public static VisualNode Press(
        this VisualNode node,
        Func<Motion, Motion> pressed,
        Func<Motion, Motion>? hover = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(pressed);
        return node.Press(pressed(Motion.None), hover?.Invoke(Motion.None));
    }

    /// <summary>
    /// Plays <paramref name="pressed"/> forward while the pointer is down and
    /// reverses it on release. The recipe is posed, so a new forward reads the
    /// live value. A second press during the reverse continues that same run.
    /// <paramref name="hover"/> plays on pointer enter and reverses on pointer exit.
    /// Pass null when there is no hover. A finger on a phone does not raise those events, so hover stays quiet.
    /// Leaving after a press that took the hover
    /// properties plays toward the hover recipe's authored from. The recognizer sits
    /// on the wrapper and the motions run on the child. Reduce motion finishes the run.
    /// </summary>
    public static VisualNode Press(this VisualNode node, Motion pressed, Motion? hover)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(pressed);
        return new PressElement(pressed, hover)
        {
            node
        };
    }

    /// <summary>
    /// Slides <paramref name="motion"/> from the last layout origin when this
    /// view's position changes. The first layout records the origin and does not
    /// play. <c>FromPlace</c> adds that slide. Width and height stay with the
    /// layout pass. The translation is on the wrapper. Keep <c>Drag</c> off this
    /// child. A collection item keeps its own move.
    /// </summary>
    public static VisualNode Layout(this VisualNode node, Motion motion)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return new LayoutElement(motion)
        {
            node
        };
    }

    /// <summary>
    /// Same as <see cref="Layout(VisualNode, Motion)"/>.
    /// <paramref name="motion"/> receives an empty motion.
    /// </summary>
    public static VisualNode Layout(this VisualNode node, Func<Motion, Motion> motion)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(motion);
        return node.Layout(motion(Motion.None));
    }

    /// <summary>
    /// Drags the child on the finger axes in <paramref name="plane"/>.
    /// On release each axis springs to a stop with <paramref name="spring"/>
    /// and keeps that axis's speed. Stops on an axis replace the two ends.
    /// A slow release takes the nearest stop. A flick takes the next stop in that direction.
    /// Omit the stops and a release faster than 2 progress units per second toward an end
    /// targets that end. A slower release uses the nearer end.
    /// The pan recognizer sits on the wrapper. The motions run on the child.
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

    /// <summary>
    /// Drags the child on the finger axes in <paramref name="plane"/>.
    /// On release each axis keeps its speed and rests where that speed dies,
    /// inside the authored span. Reaching an end stops there. Damping on
    /// <paramref name="spring"/> is the friction. Stiffness does not pull toward an end.
    /// A canceled pan stays where the finger left. The pan recognizer sits on
    /// the wrapper. The motions run on the child.
    /// </summary>
    public static VisualNode Coast(
        this VisualNode node,
        Spring spring,
        Func<DragPlane, DragPlane> plane)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plane);
        return new DragElement(plane(new DragPlane()), spring, coast: true)
        {
            node
        };
    }
}
