using Microsoft.Maui.Controls.Shapes;
using Reactor.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Named child window on a motion timeline. <c>Begin</c> and <c>End</c> are
/// fractions of the parent span. <c>Seek(id)</c> and <c>TrySpan</c> use the id.
/// </summary>
internal readonly record struct NamedSpan(string Id, double Begin, double End)
{
    /// <summary>Beat that owns this window. <c>-1</c> until a timeline beat is assigned.</summary>
    public int BeatId { get; init; } = -1;

    /// <summary>Runs when forward playback reaches <see cref="Begin"/>. Null runs nothing.</summary>
    public Action? Enter { get; init; }
}

/// <summary>
/// One flattened child on a <see cref="Motion"/> built with <c>Timeline</c>.
/// <c>StartMs</c> is where the child's tracks sit on the parent clock.
/// <c>FollowsEnd</c> means the child was placed with <c>Then</c> and moves when earlier beats change length.
/// <c>Offset</c> is milliseconds from that end. Negative overlaps the previous beat. It is 0 for an absolute <c>Add</c>.
/// <c>LengthLocked</c> means the child called <c>WithDuration</c> and a later timeline <c>WithDuration</c> leaves it alone.
/// <c>Cycles</c> is the forward and reverse passes baked into <c>Length</c>. <c>Infinite</c> loops that window.
/// </summary>
internal readonly record struct TimelineBeat(
    int Id,
    uint At,
    bool FollowsEnd,
    uint Delay,
    uint Length,
    bool LengthLocked,
    uint StartMs,
    int Offset = 0)
{
    /// <summary>Passes stored in <see cref="Length"/>. One forward pass is <c>1</c>. Yoyo makes a pair.</summary>
    public int Cycles { get; init; } = 1;

    /// <summary>When true, playback loops <see cref="Length"/> and later beats do not start.</summary>
    public bool Infinite { get; init; }
}

/// <summary>
/// Channels that are not a <see cref="BindableProperty"/>. <see cref="SemanticTrack.None"/>
/// means the track writes <see cref="MotionTrack.Property"/>. Corner radius and path
/// are sampled on their own because they are not a single bindable value.
/// </summary>
internal enum SemanticTrack
{
    None,
    CornerRadius,
    Path,
    Transform,
}

/// <summary>
/// One channel of a <see cref="Motion"/> recipe. <c>Begin</c> and <c>End</c> are
/// fractions of that motion. A null <c>From</c> is read from the view on the first
/// forward. Keyframes, when set, replace the single from/to pair.
/// </summary>
internal readonly record struct MotionTrack(
    BindableProperty? Property,
    SemanticTrack Semantic,
    object? From,
    object To,
    double Begin,
    double End,
    Easing? Easing,
    IReadOnlyList<MotionKeyframe>? Keyframes)
{
    public PathGeometry? Path { get; init; }

    public double PathFrom { get; init; }

    public double PathTo { get; init; } = 1;

    /// <summary>
    /// Called with the eased 0–1 progress of this track. The returned matrix is
    /// the whole transform for that frame.
    /// </summary>
    public Func<double, Matrix4>? Transform { get; init; }

    /// <summary>
    /// Beat that owns this track. <c>-1</c> means the track is not part of a timeline beat.
    /// </summary>
    public int BeatId { get; init; } = -1;

    /// <summary>
    /// Spring for this track only. Null keeps the track on the duration clock.
    /// </summary>
    public Spring? TrackSpring { get; init; }

    /// <summary>Progress units per second, applied when this track's spring starts.</summary>
    public double TrackSpringVelocity { get; init; }

    /// <summary>When true, a path track also writes <see cref="VisualElement.Rotation"/> from the tangent.</summary>
    public bool Orient { get; init; }

    /// <summary>
    /// Index of the bound view this track plays on. Null plays on every bound view.
    /// </summary>
    public int? Target { get; init; }

    /// <summary>Play this slice from <c>To</c> back to <c>From</c>. A yoyo pass sets it.</summary>
    public bool Reverse { get; init; }

    /// <summary>Write <c>From</c> before <c>Begin</c>. A later cycle of the same beat leaves this off.</summary>
    public bool HoldBefore { get; init; } = true;

    /// <summary>Write the end value after <c>End</c>. An earlier cycle of the same beat leaves this off.</summary>
    public bool HoldAfter { get; init; } = true;
}

/// <summary>
/// One stop on a track. <c>Offset</c> is 0–1 of that track and must not move backward.
/// <c>Easing</c> is the curve into this stop. A null easing uses the motion easing.
/// </summary>
internal readonly record struct MotionKeyframe(
    double Offset,
    object Value,
    Easing? Easing);
