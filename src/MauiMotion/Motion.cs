using Microsoft.Maui.Controls.Shapes;

namespace Reactor.Motion;

/// <summary>Which edge the view travels from or toward.</summary>
public enum SlideFrom
{
    /// <summary>Negative <see cref="VisualElement.TranslationX"/>.</summary>
    Left,

    /// <summary>Positive <see cref="VisualElement.TranslationX"/>.</summary>
    Right,

    /// <summary>Negative <see cref="VisualElement.TranslationY"/>.</summary>
    Top,

    /// <summary>Positive <see cref="VisualElement.TranslationY"/>.</summary>
    Bottom,
}

/// <summary>
/// Immutable in-page motion recipe. Holds no targets. Bind at play time.
/// Static Define, Bind, Play, ForwardAsync, and ReverseAsync build a recipe
/// and run it. Playback lives on <see cref="MotionPlayer"/>.
/// </summary>
public sealed class Motion
{
    /// <summary>
    /// Empty recipe. 300 ms, <see cref="Easing.CubicOut"/>, one play, HSV colors.
    /// </summary>
    public static Motion None { get; } = new(
        Timing.MotionDuration,
        Timing.MotionEasing,
        [], null, 1, false,
        [], ColorSpace.Hsv, null, null, 0);

    readonly IReadOnlyList<MotionTrack> _tracks;
    readonly Stagger? _stagger;
    readonly int _repeat;
    readonly bool _yoyo;
    readonly IReadOnlyList<NamedSpan> _spans;
    readonly ColorSpace _colorSpace;
    readonly Spring? _spring;
    readonly double? _perspective;
    readonly bool _timeline;
    readonly bool _durationSet;
    readonly bool _easingSet;
    readonly bool _pose;
    readonly IReadOnlyList<TimelineBeat> _beats;

    Motion(
        uint duration,
        Easing easing,
        IReadOnlyList<MotionTrack> tracks,
        Stagger? stagger,
        int repeat,
        bool yoyo,
        IReadOnlyList<NamedSpan> spans,
        ColorSpace colorSpace,
        Spring? spring,
        double? perspective,
        uint delay,
        bool timeline = false,
        bool durationSet = false,
        bool easingSet = false,
        IReadOnlyList<TimelineBeat>? beats = null,
        double rate = 1,
        double springVelocity = 0,
        bool? reduceMotion = null,
        bool pose = false)
    {
        Duration = duration;
        Easing = easing;
        _tracks = tracks;
        _stagger = stagger;
        _repeat = repeat;
        _yoyo = yoyo;
        _spans = spans;
        _colorSpace = colorSpace;
        _spring = spring;
        _perspective = perspective;
        Delay = delay;
        _timeline = timeline;
        _durationSet = durationSet;
        _easingSet = easingSet;
        _beats = beats ?? [];
        Rate = rate;
        SpringVelocity = springVelocity;
        ReduceMotion = reduceMotion;
        _pose = pose;
    }

    /// <summary>
    /// Recipe length in milliseconds. Default is 300. This is not the player
    /// length after <see cref="Stagger"/>, and it does not include <see cref="Delay"/>.
    /// </summary>
    public uint Duration { get; }

    /// <summary>
    /// Milliseconds the player holds the from-pose before the tracks move.
    /// Default is 0. A child delay passed to <see cref="Add(Motion, uint, string?)"/> or <see cref="And(Motion)"/>
    /// is baked into that child's windows and is not this hold.
    /// </summary>
    public uint Delay { get; }

    /// <summary>
    /// Curve for tracks that do not set their own. Default is <see cref="Easing.CubicOut"/>.
    /// </summary>
    public Easing Easing { get; }

    internal Stagger? StaggerSpec => _stagger;

    internal int RepeatCount => _repeat;

    internal bool YoyoEnabled => _yoyo;

    internal IReadOnlyList<MotionTrack> Tracks => _tracks;

    internal IReadOnlyList<NamedSpan> NamedSpans => _spans;

    internal ColorSpace ColorSpace => _colorSpace;

    internal Spring? Spring => _spring;

    /// <summary>
    /// Playback speed. <c>1</c> is normal. <c>2</c> halves the time a finite tick
    /// covers. <c>0</c> holds the current frame. Default is <c>1</c>.
    /// </summary>
    public double Rate { get; }

    /// <summary>
    /// Initial spring velocity in progress units per second. <see cref="WithSpring(Spring, double)"/>
    /// sets it for the whole recipe. Default is <c>0</c>.
    /// </summary>
    internal double SpringVelocity { get; }

    /// <summary>
    /// When true, playback writes the end pose and completes once.
    /// When null, the player reads <see cref="MotionPreferences.ReduceMotion"/>.
    /// </summary>
    public bool? ReduceMotion { get; }

    /// <summary>
    /// When true, each new forward reads property from-values from the view.
    /// </summary>
    internal bool Pose => _pose;

    /// <summary>
    /// Flutter <c>Matrix4.setEntry(3, 2, entry)</c>. Null leaves the platform
    /// eye distance. <c>0</c> is orthographic.
    /// </summary>
    internal double? PerspectiveEntry => _perspective;

    /// <summary>
    /// Sets the length in milliseconds.
    /// A full-span recipe replaces <see cref="Duration"/>.
    /// On a <see cref="Timeline"/> with several beats, each beat that did not call
    /// <see cref="WithDuration"/> itself takes this length and the windows are rebuilt.
    /// A beat that called <see cref="WithDuration"/> keeps its length.
    /// Any other windowed recipe ignores a shorter value and pads the end when the value is longer.
    /// A debug assert fires when <see cref="WithSpring"/> is already set.
    /// </summary>
    public Motion WithDuration(uint milliseconds)
    {
        if (_spring is not null)
            System.Diagnostics.Debug.Assert(false, "WithDuration is ignored when WithSpring is set.");

        if (_timeline && _beats.Count > 1)
            return RestretchBeats(milliseconds);

        if (_timeline && _beats.Count == 1 && AllFullSpan(_tracks) && _beats[0].Delay == 0)
            return ReplaceSingleBeat(milliseconds);

        return ApplyDuration(milliseconds);
    }

    /// <summary>
    /// Replaces the easing. <see langword="null"/> throws.
    /// Default is <see cref="Easing.CubicOut"/>.
    /// A beat that called <see cref="WithEasing"/> keeps its curve. Other beats use this one.
    /// </summary>
    public Motion WithEasing(Easing easing)
        => Copy(
            easing: easing ?? throw new ArgumentNullException(nameof(easing)),
            easingSet: true);

    /// <summary>
    /// Multiplies every finite tick, including a <see cref="Delay"/> hold.
    /// <c>1</c> is normal. <c>0</c> holds the current frame.
    /// A negative, NaN, or infinite value becomes <c>0</c>.
    /// </summary>
    public Motion WithRate(double rate)
    {
        if (double.IsNaN(rate) || double.IsInfinity(rate) || rate < 0)
            rate = 0;
        return rate == Rate ? this : Copy(rate: rate);
    }

    /// <summary>
    /// When <paramref name="enabled"/> is true, playback writes the end pose and completes once.
    /// This wins over <see cref="MotionPreferences.ReduceMotion"/>.
    /// </summary>
    public Motion WithReduceMotion(bool enabled)
        => ReduceMotion == enabled ? this : Copy(reduceMotion: enabled, setReduceMotion: true);

    /// <summary>
    /// Hold the from-pose for <paramref name="milliseconds"/> before the tracks move.
    /// Does not change <see cref="Duration"/>. <c>0</c> clears a delay set earlier.
    /// On a child of <see cref="Add(Motion, uint, string?)"/> or <see cref="And(Motion)"/>, this wait is baked into
    /// that child's windows. On the motion you bind, it is the player hold.
    /// </summary>
    public Motion WithDelay(uint milliseconds)
        => milliseconds == Delay ? this : Copy(delay: milliseconds);

    /// <summary>
    /// Play with a mass-spring-damper until rest. Mutually exclusive with
    /// <see cref="WithDuration"/> / <see cref="Stagger"/> / <see cref="Settle"/>.
    /// Not an easing curve.
    /// <paramref name="velocity"/> is progress units per second at the start of a forward or reverse.
    /// </summary>
    public Motion WithSpring(Spring spring, double velocity = 0)
    {
        if (_stagger is not null)
            System.Diagnostics.Debug.Assert(false, "Stagger is ignored when WithSpring is set.");
        if (HasTrackSpring())
            System.Diagnostics.Debug.Assert(false, "WithSpring replaces Settle. A recipe uses one or the other.");
        return Copy(spring: spring, setSpring: true, springVelocity: SanitizeVelocity(velocity));
    }

    /// <summary>
    /// On each new forward, property tracks take their from-value from the view
    /// and keep the recipe <c>to</c>. An in-flight spring on that view hands its
    /// velocity to this run. Seek and a finished run still leave velocity at 0.
    /// Keyframes, paths, and matrix transforms keep their authored start.
    /// </summary>
    public Motion WithPose()
        => Copy(pose: true);

    /// <summary>
    /// Springs each track added so far. Tracks added after this call stay on the duration clock.
    /// Mutually exclusive with <see cref="WithSpring(Spring, double)"/>.
    /// <paramref name="velocity"/> is progress units per second when that track's spring starts.
    /// </summary>
    public Motion Settle(Spring spring, double velocity = 0)
    {
        if (_spring is not null)
        {
            System.Diagnostics.Debug.Assert(false, "Settle is ignored when WithSpring is set.");
            return this;
        }

        if (_tracks.Count == 0)
            return this;

        velocity = SanitizeVelocity(velocity);
        var tracks = new MotionTrack[_tracks.Count];
        var changed = false;
        for (var i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            if (track.TrackSpring is null)
            {
                tracks[i] = track with { TrackSpring = spring, TrackSpringVelocity = velocity };
                changed = true;
            }
            else
            {
                tracks[i] = track;
            }
        }

        return changed ? Copy(tracks: tracks) : this;
    }

    /// <summary>
    /// Color interpolation. Default is <see cref="ColorSpace.Hsv"/> (shortest
    /// hue). Use <see cref="ColorSpace.Rgb"/> for channel-wise lerp.
    /// </summary>
    public Motion WithColorSpace(ColorSpace colorSpace)
        => Copy(colorSpace: colorSpace);

    /// <summary>
    /// Fades opacity to <paramref name="to"/>. The live value is captured on the first forward.
    /// </summary>
    public Motion Opacity(double to)
        => Add(VisualElement.OpacityProperty, null, to);

    /// <summary>
    /// Fades opacity from <paramref name="from"/> to <paramref name="to"/>.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion Opacity(double from, double to)
        => Add(VisualElement.OpacityProperty, from, to);

    /// <summary>Opacity stops at 0–1 of this motion.</summary>
    public Motion Opacity(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.OpacityProperty, SemanticTrack.None, frames);

    /// <summary>
    /// View translation in device-independent pixels (to-only).
    /// Writes <see cref="VisualElement.TranslationX"/> and <see cref="VisualElement.TranslationY"/>.
    /// </summary>
    public Motion Translate(double x, double y)
        => TranslateX(x).TranslateY(y);

    /// <summary>
    /// View translation in device-independent pixels.
    /// Writes <see cref="VisualElement.TranslationX"/> and <see cref="VisualElement.TranslationY"/>.
    /// </summary>
    public Motion Translate(double fromX, double fromY, double toX, double toY)
        => TranslateX(fromX, toX).TranslateY(fromY, toY);

    /// <summary>
    /// Horizontal translation, in device-independent pixels, to <paramref name="to"/>.
    /// The live value is captured on the first forward.
    /// </summary>
    public Motion TranslateX(double to)
        => Add(VisualElement.TranslationXProperty, null, to);

    /// <summary>
    /// Horizontal translation, in device-independent pixels, from <paramref name="from"/>
    /// to <paramref name="to"/>. The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion TranslateX(double from, double to)
        => Add(VisualElement.TranslationXProperty, from, to);

    /// <summary>Horizontal translation stops at 0–1 of this motion.</summary>
    public Motion TranslateX(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.TranslationXProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Vertical translation, in device-independent pixels, to <paramref name="to"/>.
    /// The live value is captured on the first forward.
    /// </summary>
    public Motion TranslateY(double to)
        => Add(VisualElement.TranslationYProperty, null, to);

    /// <summary>
    /// Vertical translation, in device-independent pixels, from <paramref name="from"/>
    /// to <paramref name="to"/>. The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion TranslateY(double from, double to)
        => Add(VisualElement.TranslationYProperty, from, to);

    /// <summary>Vertical translation stops at 0–1 of this motion.</summary>
    public Motion TranslateY(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.TranslationYProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Moves <see cref="VisualElement.AnchorX"/> to <paramref name="to"/>.
    /// The live value is captured on the first forward. <c>0.5</c> is the center.
    /// </summary>
    public Motion AnchorX(double to)
        => Add(VisualElement.AnchorXProperty, null, to);

    /// <summary>
    /// Moves <see cref="VisualElement.AnchorX"/> from <paramref name="from"/> to <paramref name="to"/>.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion AnchorX(double from, double to)
        => Add(VisualElement.AnchorXProperty, from, to);

    /// <summary><see cref="VisualElement.AnchorX"/> stops at 0–1 of this motion.</summary>
    public Motion AnchorX(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.AnchorXProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Moves <see cref="VisualElement.AnchorY"/> to <paramref name="to"/>.
    /// The live value is captured on the first forward. <c>0.5</c> is the center.
    /// </summary>
    public Motion AnchorY(double to)
        => Add(VisualElement.AnchorYProperty, null, to);

    /// <summary>
    /// Moves <see cref="VisualElement.AnchorY"/> from <paramref name="from"/> to <paramref name="to"/>.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion AnchorY(double from, double to)
        => Add(VisualElement.AnchorYProperty, from, to);

    /// <summary><see cref="VisualElement.AnchorY"/> stops at 0–1 of this motion.</summary>
    public Motion AnchorY(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.AnchorYProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Writes <see cref="VisualElement.TranslationXProperty"/> and
    /// <see cref="VisualElement.TranslationYProperty"/> along
    /// <paramref name="geometry"/>. <paramref name="from"/> / <paramref name="to"/>
    /// are 0–1 of path length. Stagger delays the start of path <c>t</c>.
    /// When <paramref name="orient"/> is true, the track also writes
    /// <see cref="VisualElement.Rotation"/> from the curve direction.
    /// </summary>
    public Motion Path(PathGeometry geometry, double from = 0, double to = 1, bool orient = false)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        from = double.IsNaN(from) || double.IsInfinity(from) ? 0 : Math.Clamp(from, 0, 1);
        to = double.IsNaN(to) || double.IsInfinity(to) ? 1 : Math.Clamp(to, 0, 1);
        return Append(new MotionTrack(null, SemanticTrack.Path, null, to, 0, 1, null, null)
        {
            Path = geometry,
            PathFrom = from,
            PathTo = to,
            Orient = orient,
        });
    }

    /// <summary>
    /// Writes both <see cref="VisualElement.ScaleXProperty"/> and
    /// <see cref="VisualElement.ScaleYProperty"/> (not <c>ScaleProperty</c>).
    /// </summary>
    public Motion Scale(double to)
        => ScaleX(to).ScaleY(to);

    /// <summary>
    /// Writes both <see cref="VisualElement.ScaleXProperty"/> and
    /// <see cref="VisualElement.ScaleYProperty"/> from <paramref name="from"/> to <paramref name="to"/>.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion Scale(double from, double to)
        => ScaleX(from, to).ScaleY(from, to);

    /// <summary>
    /// Keyframe stops for both <see cref="VisualElement.ScaleXProperty"/> and
    /// <see cref="VisualElement.ScaleYProperty"/>. Offsets are 0–1 of this motion.
    /// </summary>
    public Motion Scale(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframesBoth(VisualElement.ScaleXProperty, VisualElement.ScaleYProperty, frames);

    /// <summary>
    /// Horizontal scale to <paramref name="to"/>. The live value is captured on the first forward.
    /// </summary>
    public Motion ScaleX(double to)
        => Add(VisualElement.ScaleXProperty, null, to);

    /// <summary>
    /// Horizontal scale from <paramref name="from"/> to <paramref name="to"/>.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion ScaleX(double from, double to)
        => Add(VisualElement.ScaleXProperty, from, to);

    /// <summary>Horizontal scale stops at 0–1 of this motion.</summary>
    public Motion ScaleX(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.ScaleXProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Vertical scale to <paramref name="to"/>. The live value is captured on the first forward.
    /// </summary>
    public Motion ScaleY(double to)
        => Add(VisualElement.ScaleYProperty, null, to);

    /// <summary>
    /// Vertical scale from <paramref name="from"/> to <paramref name="to"/>.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion ScaleY(double from, double to)
        => Add(VisualElement.ScaleYProperty, from, to);

    /// <summary>Vertical scale stops at 0–1 of this motion.</summary>
    public Motion ScaleY(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.ScaleYProperty, SemanticTrack.None, frames);

    /// <summary>
    /// View rotation in degrees. Writes <see cref="VisualElement.Rotation"/>.
    /// </summary>
    public Motion Rotate(double toDegrees)
        => Add(VisualElement.RotationProperty, null, toDegrees);

    /// <summary>
    /// In-plane rotation, in degrees, from <paramref name="fromDegrees"/> to <paramref name="toDegrees"/>.
    /// The first forward writes <paramref name="fromDegrees"/>.
    /// </summary>
    public Motion Rotate(double fromDegrees, double toDegrees)
        => Add(VisualElement.RotationProperty, fromDegrees, toDegrees);

    /// <summary>In-plane rotation stops, in degrees, at 0–1 of this motion.</summary>
    public Motion Rotate(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.RotationProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Rotation about the horizontal axis, in degrees, around
    /// <see cref="VisualElement.AnchorX"/> and <see cref="VisualElement.AnchorY"/>.
    /// Pair with <see cref="Perspective(double)"/> so the far edge shrinks.
    /// </summary>
    public Motion RotateX(double toDegrees)
        => Add(VisualElement.RotationXProperty, null, toDegrees);

    /// <summary>
    /// Rotation about the horizontal axis, in degrees, from <paramref name="fromDegrees"/>
    /// to <paramref name="toDegrees"/>. The first forward writes <paramref name="fromDegrees"/>.
    /// </summary>
    public Motion RotateX(double fromDegrees, double toDegrees)
        => Add(VisualElement.RotationXProperty, fromDegrees, toDegrees);

    /// <summary>Horizontal-axis rotation stops, in degrees, at 0–1 of this motion.</summary>
    public Motion RotateX(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.RotationXProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Rotation about the vertical axis, in degrees, around
    /// <see cref="VisualElement.AnchorX"/> and <see cref="VisualElement.AnchorY"/>.
    /// Pair with <see cref="Perspective(double)"/> so the far edge shrinks.
    /// </summary>
    public Motion RotateY(double toDegrees)
        => Add(VisualElement.RotationYProperty, null, toDegrees);

    /// <summary>
    /// Rotation about the vertical axis, in degrees, from <paramref name="fromDegrees"/>
    /// to <paramref name="toDegrees"/>. The first forward writes <paramref name="fromDegrees"/>.
    /// </summary>
    public Motion RotateY(double fromDegrees, double toDegrees)
        => Add(VisualElement.RotationYProperty, fromDegrees, toDegrees);

    /// <summary>Vertical-axis rotation stops, in degrees, at 0–1 of this motion.</summary>
    public Motion RotateY(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.RotationYProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Eye distance for <see cref="RotateX(double)"/> and <see cref="RotateY(double)"/>.
    /// <paramref name="entry"/> is Flutter's <c>Matrix4.setEntry(3, 2, entry)</c>:
    /// the camera sits <c>1/entry</c> device-independent pixels away.
    /// <c>0.001</c> shrinks the far edge a little. <c>0</c> keeps both edges the same height.
    /// On Windows the same entry is written into the composition transform.
    /// </summary>
    public Motion Perspective(double entry)
    {
        if (HasTransformTrack())
            System.Diagnostics.Debug.Assert(false, "Transform replaces Perspective on this recipe.");
        if (double.IsNaN(entry) || double.IsInfinity(entry))
            entry = 0;
        return Copy(perspective: entry, setPerspective: true);
    }

    /// <summary>
    /// One matrix for the whole view. <paramref name="onTransform"/> receives the
    /// eased progress of this track, 0 at the start and 1 at the end, and returns
    /// the matrix to write. Reverse decreases that value. The pivot is the view's
    /// <see cref="VisualElement.AnchorX"/> and <see cref="VisualElement.AnchorY"/>.
    /// Replaces <see cref="Translate(double, double)"/>, <see cref="Rotate(double)"/>,
    /// <see cref="RotateX(double)"/>, <see cref="RotateY(double)"/>, <see cref="Scale(double)"/>,
    /// <see cref="Path"/>, and <see cref="Perspective"/> on this recipe.
    /// </summary>
    public Motion Transform(Func<double, Matrix4> onTransform)
    {
        ArgumentNullException.ThrowIfNull(onTransform);
        if (HasPlaneTrack() || _perspective is not null)
            System.Diagnostics.Debug.Assert(false, "Transform replaces Translate, Rotate, RotateX, RotateY, Scale, Path, and Perspective on this recipe.");

        return Append(new MotionTrack(null, SemanticTrack.Transform, null, 0d, 0, 1, null, null)
        {
            Transform = onTransform,
        });
    }

    /// <summary>
    /// Blends the background to <paramref name="to"/> in the recipe
    /// <see cref="Reactor.Motion.ColorSpace"/>. The live color is captured on the first forward.
    /// </summary>
    public Motion BackgroundColor(Color to)
        => Add(VisualElement.BackgroundColorProperty, null, to);

    /// <summary>
    /// Blends the background from <paramref name="from"/> to <paramref name="to"/>
    /// in the recipe <see cref="Reactor.Motion.ColorSpace"/>. The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion BackgroundColor(Color from, Color to)
        => Add(VisualElement.BackgroundColorProperty, from, to);

    /// <summary>Background-color stops at 0–1 of this motion.</summary>
    public Motion BackgroundColor(Func<KeyframeBuilder<Color>, KeyframeBuilder<Color>> frames)
        => AddKeyframes(VisualElement.BackgroundColorProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Animates <see cref="VisualElement.WidthRequest"/> to <paramref name="to"/>,
    /// in device-independent pixels. The live value is captured on the first forward.
    /// </summary>
    public Motion Width(double to)
        => Add(VisualElement.WidthRequestProperty, null, to);

    /// <summary>
    /// Animates <see cref="VisualElement.WidthRequest"/> from <paramref name="from"/>
    /// to <paramref name="to"/>. The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion Width(double from, double to)
        => Add(VisualElement.WidthRequestProperty, from, to);

    /// <summary>Width stops at 0–1 of this motion.</summary>
    public Motion Width(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.WidthRequestProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Animates <see cref="VisualElement.HeightRequest"/> to <paramref name="to"/>,
    /// in device-independent pixels. The live value is captured on the first forward.
    /// </summary>
    public Motion Height(double to)
        => Add(VisualElement.HeightRequestProperty, null, to);

    /// <summary>
    /// Animates <see cref="VisualElement.HeightRequest"/> from <paramref name="from"/>
    /// to <paramref name="to"/>. The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion Height(double from, double to)
        => Add(VisualElement.HeightRequestProperty, from, to);

    /// <summary>Height stops at 0–1 of this motion.</summary>
    public Motion Height(Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
        => AddKeyframes(VisualElement.HeightRequestProperty, SemanticTrack.None, frames);

    /// <summary>
    /// Animates the corner radius to <paramref name="to"/>.
    /// The live value is captured on the first forward.
    /// </summary>
    public Motion CornerRadius(CornerRadius to)
        => AddSemantic(SemanticTrack.CornerRadius, null, to);

    /// <summary>
    /// Animates the corner radius from <paramref name="from"/> to <paramref name="to"/>.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion CornerRadius(CornerRadius from, CornerRadius to)
        => AddSemantic(SemanticTrack.CornerRadius, from, to);

    /// <summary>Corner-radius stops at 0–1 of this motion.</summary>
    public Motion CornerRadius(Func<KeyframeBuilder<CornerRadius>, KeyframeBuilder<CornerRadius>> frames)
        => AddKeyframes(null, SemanticTrack.CornerRadius, frames);

    /// <summary>
    /// Whole-state keyframes at 0–1 offsets of this motion. Omitted properties
    /// hold their last keyframed value; they do not lerp back to implicit from.
    /// Sequential millisecond steps belong on <c>Then</c>, not here.
    /// </summary>
    public Motion Keyframes(params (double At, Func<Motion, Motion> Set)[] frames)
    {
        if (frames is null || frames.Length == 0)
            return this;

        var grouped = new Dictionary<(BindableProperty? Property, SemanticTrack Semantic), List<MotionKeyframe>>();
        var lastOffset = new Dictionary<(BindableProperty? Property, SemanticTrack Semantic), double>();
        foreach (var (at, set) in frames)
        {
            if (set is null)
                throw new ArgumentNullException(nameof(frames), "A keyframe callback is null.");

            var offset = double.IsNaN(at) || double.IsInfinity(at) ? 0 : Math.Clamp(at, 0, 1);
            var snapshot = set(None);
            foreach (var track in snapshot._tracks)
            {
                if (track.Semantic == SemanticTrack.Transform)
                    continue;

                var key = (track.Property, track.Semantic);
                if (!grouped.TryGetValue(key, out var list))
                {
                    list = [];
                    grouped[key] = list;
                }

                if (lastOffset.TryGetValue(key, out var previous) && offset < previous)
                    System.Diagnostics.Debug.Assert(false, "Keyframe offsets must be non-decreasing.");

                lastOffset[key] = offset;
                list.Add(new MotionKeyframe(offset, track.To, track.Easing));
            }
        }

        var motion = this;
        foreach (var (key, list) in grouped)
        {
            if (list.Count == 0)
                continue;
            motion = motion.Append(new MotionTrack(
                key.Property,
                key.Semantic,
                null,
                list[^1].Value,
                0,
                1,
                null,
                list));
        }

        return motion;
    }

    /// <summary>
    /// Animates <paramref name="property"/> to <paramref name="to"/>.
    /// The live value is captured on the first forward.
    /// </summary>
    public Motion Property(BindableProperty property, object to)
        => Add(property ?? throw new ArgumentNullException(nameof(property)), null, to);

    /// <summary>
    /// Animates <paramref name="property"/> from <paramref name="from"/> to <paramref name="to"/>.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion Property(BindableProperty property, object from, object to)
        => Add(property ?? throw new ArgumentNullException(nameof(property)), from, to);

    /// <summary>Opacity from 0 to 1.</summary>
    public Motion FadeIn()
        => Opacity(0, 1);

    /// <summary>Opacity from 1 to 0.</summary>
    public Motion FadeOut()
        => Opacity(1, 0);

    /// <summary>
    /// Slides in from <paramref name="from"/> by <paramref name="distance"/>
    /// device-independent pixels. Default distance is 24.
    /// The first forward writes the offset.
    /// </summary>
    public Motion SlideIn(SlideFrom from, double distance = 24)
        => from switch
        {
            SlideFrom.Left => TranslateX(-distance, 0),
            SlideFrom.Right => TranslateX(distance, 0),
            SlideFrom.Top => TranslateY(-distance, 0),
            SlideFrom.Bottom => TranslateY(distance, 0),
            _ => TranslateX(-distance, 0),
        };

    /// <summary>
    /// Slides out toward <paramref name="to"/> by <paramref name="distance"/>
    /// device-independent pixels. Default distance is 24.
    /// </summary>
    public Motion SlideOut(SlideFrom to, double distance = 24)
        => to switch
        {
            SlideFrom.Left => TranslateX(0, -distance),
            SlideFrom.Right => TranslateX(0, distance),
            SlideFrom.Top => TranslateY(0, -distance),
            SlideFrom.Bottom => TranslateY(0, distance),
            _ => TranslateX(0, -distance),
        };

    /// <summary>
    /// Scales both axes from <paramref name="from"/> to 1. Default start is 0.85.
    /// The first forward writes <paramref name="from"/>.
    /// </summary>
    public Motion ScaleIn(double from = 0.85)
        => Scale(from, 1);

    /// <summary>Scales both axes from 1 to <paramref name="to"/>. Default end is 0.85.</summary>
    public Motion ScaleOut(double to = 0.85)
        => Scale(1, to);

    /// <summary>
    /// Delay each bound target on linear player time. Root only; ignored on
    /// nested children. Example: 300 ms motion, two views, stagger 100 ms →
    /// player span 400 ms. View 0 window [0, 300); view 1 [100, 400). At t=0
    /// both are already at from. During [0, 100) the second target holds from.
    /// </summary>
    public Motion Stagger(
        uint stepMilliseconds,
        StaggerFrom from = StaggerFrom.Start,
        (int Columns, int Rows)? grid = null)
    {
        if (_spring is not null)
            System.Diagnostics.Debug.Assert(false, "Stagger is ignored when WithSpring is set.");
        return Copy(stagger: new Stagger(stepMilliseconds, from, grid));
    }

    /// <summary>
    /// Play this motion <paramref name="count"/> times. <c>1</c> is once
    /// (default). <c>0</c> becomes <c>1</c>. <c>-1</c> repeats until Pause, Reset, or Dispose.
    /// </summary>
    public Motion Repeat(int count)
        => Copy(repeat: count == 0 ? 1 : count);

    /// <summary>
    /// After each forward, play reverse. A repeat cycle is forward+reverse.
    /// </summary>
    public Motion Yoyo(bool enabled = true)
        => Copy(yoyo: enabled);

    /// <summary>
    /// Places <paramref name="child"/> on this timeline at
    /// <paramref name="at"/> milliseconds. A delay on the child starts that much
    /// later. Nested <see cref="Stagger"/> is ignored. Parent span is
    /// <c>max(current, at + child.Delay + child.Duration)</c>. This motion's own
    /// <see cref="Delay"/> stays the player hold. Duplicate <paramref name="id"/>
    /// values: last wins.
    /// </summary>
    public Motion Add(Motion child, uint at = 0, string? id = null)
        => Add(child, at, id, followsEnd: false);

    /// <summary>
    /// Builds <paramref name="configure"/> from an empty motion and places it at
    /// <paramref name="at"/> milliseconds. Same window rules as <see cref="Add(Motion, uint, string?)"/>.
    /// </summary>
    public Motion Add(Func<Motion, Motion> configure, uint at = 0, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Add(configure(None), at, id);
    }

    /// <summary>
    /// Appends <paramref name="next"/> after the current span. A delay on
    /// <paramref name="next"/> waits that much longer.
    /// Use <see cref="Add(Motion, uint, string?)"/> to start at 0.
    /// </summary>
    public Motion Then(Motion next, string? id = null)
        => Add(next, Duration, id, followsEnd: true);

    /// <summary>
    /// Builds <paramref name="configure"/> from an empty motion and appends it
    /// after the current span. Same rules as <see cref="Then(Motion, string?)"/>.
    /// </summary>
    public Motion Then(Func<Motion, Motion> configure, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Then(configure(None), id);
    }

    /// <summary>
    /// Plays <paramref name="other"/> at the same time. The parent span is the
    /// longer recipe after each side's delay is baked into its windows. The
    /// result's <see cref="Delay"/> is 0. Tracks keep their length in milliseconds
    /// and are not stretched. Where tracks overlap, <paramref name="other"/> wins.
    /// </summary>
    public Motion And(Motion other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var leftEnd = (long)Delay + Duration;
        var rightEnd = (long)other.Delay + other.Duration;
        var spanLong = Math.Max(leftEnd, rightEnd);
        var span = spanLong > uint.MaxValue ? uint.MaxValue : (uint)spanLong;
        var easing = other._easingSet ? other.Easing : Easing;
        var tracks = Concat(
            Shift(_tracks, Delay, Duration, span),
            Shift(other._tracks, other.Delay, other.Duration, span));
        var spans = ConcatSpans(
            ShiftSpans(_spans, Delay, Duration, span),
            ShiftSpans(other._spans, other.Delay, other.Duration, span));
        return new Motion(
            span,
            easing,
            tracks,
            other._stagger ?? _stagger,
            other._repeat != 1 ? other._repeat : _repeat,
            other._yoyo || _yoyo,
            spans,
            other._colorSpace,
            other._spring ?? _spring,
            other._perspective ?? _perspective,
            0,
            durationSet: _durationSet || other._durationSet,
            easingSet: _easingSet || other._easingSet,
            rate: other.Rate != 1 ? other.Rate : Rate,
            springVelocity: other._spring is not null ? other.SpringVelocity : SpringVelocity,
            reduceMotion: other.ReduceMotion ?? ReduceMotion,
            pose: _pose || other._pose);
    }

    /// <summary>
    /// Builds <paramref name="configure"/> from an empty motion and plays it at the same time.
    /// Same rules as <see cref="And(Motion)"/>.
    /// </summary>
    public Motion And(Func<Motion, Motion> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return And(configure(None));
    }

    /// <summary>
    /// Binds this recipe to <paramref name="targets"/>. Does not start playback.
    /// </summary>
    public MotionPlayer Bind(params VisualElement[] targets)
        => MotionPlayer.Create(this, targets);

    /// <summary>
    /// Binds this recipe to <paramref name="targets"/>. Does not start playback.
    /// </summary>
    public MotionPlayer Bind(IEnumerable<VisualElement> targets)
        => MotionPlayer.Create(this, targets as IReadOnlyList<VisualElement> ?? [.. targets]);

    /// <summary>
    /// Builds a recipe by applying <paramref name="configure"/> to an empty motion.
    /// </summary>
    public static Motion Define(Func<Motion, Motion> configure)
        => configure(None);

    /// <summary>
    /// First beat of a timeline. <paramref name="configure"/> receives an empty motion.
    /// Pass <paramref name="id"/> to name that beat for <see cref="MotionPlayer.Seek(string)"/>.
    /// Further beats are <see cref="Then(Func{Motion, Motion}, string?)"/> and
    /// <see cref="Add(Func{Motion, Motion}, uint, string?)"/>.
    /// <see cref="WithDuration"/> then gives one length to every beat that did not set its own.
    /// <see cref="WithEasing"/> does the same for the curve.
    /// </summary>
    public static Motion Timeline(Func<Motion, Motion> configure, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var first = configure(None);
        if (!first._timeline)
            first = first.Copy(timeline: true);
        if (string.IsNullOrWhiteSpace(id) || first.Duration == 0)
            return first;
        return first.Copy(spans: PutSpan(first._spans, new NamedSpan(id, 0, 1)));
    }

    /// <summary>
    /// Binds <paramref name="motion"/> to <paramref name="target"/>. Does not start playback.
    /// </summary>
    public static MotionPlayer Bind(Motion motion, VisualElement target)
        => Bind(motion, [target]);

    /// <summary>
    /// Binds <paramref name="motion"/> to <paramref name="targets"/>. Does not start playback.
    /// </summary>
    public static MotionPlayer Bind(
        Motion motion,
        IEnumerable<VisualElement> targets)
        => MotionPlayer.Create(motion, targets as IReadOnlyList<VisualElement> ?? [.. targets]);

    /// <summary>
    /// Builds a recipe from an empty motion, then binds it to <paramref name="target"/>.
    /// Does not start playback.
    /// </summary>
    public static MotionPlayer Bind(
        Func<Motion, Motion> configure,
        VisualElement target)
        => Bind(configure(None), target);

    /// <summary>
    /// Builds a recipe from an empty motion, then binds it to <paramref name="targets"/>.
    /// Does not start playback.
    /// </summary>
    public static MotionPlayer Bind(
        Func<Motion, Motion> configure,
        IEnumerable<VisualElement> targets)
        => Bind(configure(None), targets);

    /// <summary>
    /// Binds <paramref name="motion"/> and starts it forward.
    /// Keep the player to pause or reverse.
    /// </summary>
    public static MotionPlayer Play(Motion motion, params VisualElement[] targets)
    {
        var player = Bind(motion, targets);
        player.Forward();
        return player;
    }

    /// <summary>
    /// Builds a recipe from an empty motion, binds it to <paramref name="target"/>,
    /// and starts it forward.
    /// </summary>
    public static MotionPlayer Play(
        Func<Motion, Motion> configure,
        VisualElement target)
        => Play(configure(None), target);

    /// <summary>
    /// Builds a recipe from an empty motion, binds it to <paramref name="targets"/>,
    /// and starts it forward.
    /// </summary>
    public static MotionPlayer Play(
        Func<Motion, Motion> configure,
        IEnumerable<VisualElement> targets)
        => Play(configure(None), [.. targets]);

    /// <summary>
    /// Binds <paramref name="motion"/> to <paramref name="target"/> and plays it forward.
    /// A later bind on the same view replaces this run. Cancellation does not throw.
    /// </summary>
    public static Task ForwardAsync(
        Motion motion,
        VisualElement target,
        CancellationToken cancellationToken = default)
        => MotionPlayerExtensions.ForwardAsync(Bind(motion, target), cancellationToken);

    /// <summary>
    /// Binds <paramref name="motion"/> to <paramref name="targets"/> and plays it forward.
    /// A later bind on the same view replaces this run. Cancellation does not throw.
    /// </summary>
    public static Task ForwardAsync(
        Motion motion,
        IEnumerable<VisualElement> targets,
        CancellationToken cancellationToken = default)
        => MotionPlayerExtensions.ForwardAsync(Bind(motion, targets), cancellationToken);

    /// <summary>
    /// Builds a recipe from an empty motion, binds it to <paramref name="target"/>,
    /// and plays it forward. Cancellation does not throw.
    /// </summary>
    public static Task ForwardAsync(
        Func<Motion, Motion> configure,
        VisualElement target,
        CancellationToken cancellationToken = default)
        => ForwardAsync(configure(None), target, cancellationToken);

    /// <summary>
    /// Builds a recipe from an empty motion, binds it to <paramref name="targets"/>,
    /// and plays it forward. Cancellation does not throw.
    /// </summary>
    public static Task ForwardAsync(
        Func<Motion, Motion> configure,
        IEnumerable<VisualElement> targets,
        CancellationToken cancellationToken = default)
        => ForwardAsync(configure(None), targets, cancellationToken);

    /// <summary>
    /// Binds <paramref name="motion"/> to <paramref name="target"/> and plays it in reverse.
    /// A later bind on the same view replaces this run. Cancellation does not throw.
    /// </summary>
    public static Task ReverseAsync(
        Motion motion,
        VisualElement target,
        CancellationToken cancellationToken = default)
        => MotionPlayerExtensions.ReverseAsync(Bind(motion, target), cancellationToken);

    /// <summary>
    /// Binds <paramref name="motion"/> to <paramref name="targets"/> and plays it in reverse.
    /// A later bind on the same view replaces this run. Cancellation does not throw.
    /// </summary>
    public static Task ReverseAsync(
        Motion motion,
        IEnumerable<VisualElement> targets,
        CancellationToken cancellationToken = default)
        => MotionPlayerExtensions.ReverseAsync(Bind(motion, targets), cancellationToken);

    /// <summary>
    /// Builds a recipe from an empty motion, binds it to <paramref name="target"/>,
    /// and plays it in reverse. Cancellation does not throw.
    /// </summary>
    public static Task ReverseAsync(
        Func<Motion, Motion> configure,
        VisualElement target,
        CancellationToken cancellationToken = default)
        => ReverseAsync(configure(None), target, cancellationToken);

    /// <summary>
    /// Builds a recipe from an empty motion, binds it to <paramref name="targets"/>,
    /// and plays it in reverse. Cancellation does not throw.
    /// </summary>
    public static Task ReverseAsync(
        Func<Motion, Motion> configure,
        IEnumerable<VisualElement> targets,
        CancellationToken cancellationToken = default)
        => ReverseAsync(configure(None), targets, cancellationToken);

    Motion Add(BindableProperty property, object? from, object to)
        => Append(new MotionTrack(property, SemanticTrack.None, from, to, 0, 1, null, null));

    Motion AddSemantic(SemanticTrack semantic, object? from, object to)
        => Append(new MotionTrack(null, semantic, from, to, 0, 1, null, null));

    Motion AddKeyframes<T>(
        BindableProperty? property,
        SemanticTrack semantic,
        Func<KeyframeBuilder<T>, KeyframeBuilder<T>> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var list = frames(new KeyframeBuilder<T>()).Build();
        if (list.Count == 0)
            return this;
        return Append(new MotionTrack(property, semantic, null, list[^1].Value, 0, 1, null, list));
    }

    Motion AddKeyframesBoth(
        BindableProperty first,
        BindableProperty second,
        Func<KeyframeBuilder<double>, KeyframeBuilder<double>> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var list = frames(new KeyframeBuilder<double>()).Build();
        if (list.Count == 0)
            return this;
        var to = list[^1].Value;
        return Append(new MotionTrack(first, SemanticTrack.None, null, to, 0, 1, null, list))
            .Append(new MotionTrack(second, SemanticTrack.None, null, to, 0, 1, null, list));
    }

    Motion Append(MotionTrack track)
    {
        if (track.Semantic != SemanticTrack.Transform && IsPlaneChannel(track) && HasTransformTrack())
            System.Diagnostics.Debug.Assert(false, "Transform replaces Translate, Rotate, RotateX, RotateY, Scale, Path, and Perspective on this recipe.");

        var tracks = new MotionTrack[_tracks.Count + 1];
        for (var i = 0; i < _tracks.Count; i++)
            tracks[i] = _tracks[i];
        tracks[_tracks.Count] = track;
        return Copy(tracks: tracks);
    }

    Motion Copy(
        uint? duration = null,
        Easing? easing = null,
        IReadOnlyList<MotionTrack>? tracks = null,
        Stagger? stagger = null,
        int? repeat = null,
        bool? yoyo = null,
        IReadOnlyList<NamedSpan>? spans = null,
        ColorSpace? colorSpace = null,
        Spring? spring = null,
        bool setSpring = false,
        double? perspective = null,
        bool setPerspective = false,
        uint? delay = null,
        bool? timeline = null,
        bool? durationSet = null,
        bool? easingSet = null,
        IReadOnlyList<TimelineBeat>? beats = null,
        double? rate = null,
        double? springVelocity = null,
        bool? reduceMotion = null,
        bool setReduceMotion = false,
        bool? pose = null)
        => new(
            duration ?? Duration,
            easing ?? Easing,
            tracks ?? _tracks,
            stagger ?? _stagger,
            repeat ?? _repeat,
            yoyo ?? _yoyo,
            spans ?? _spans,
            colorSpace ?? _colorSpace,
            setSpring ? spring : _spring,
            setPerspective ? perspective : _perspective,
            delay ?? Delay,
            timeline ?? _timeline,
            durationSet ?? _durationSet,
            easingSet ?? _easingSet,
            beats ?? _beats,
            rate ?? Rate,
            springVelocity ?? (setSpring ? 0 : SpringVelocity),
            setReduceMotion ? reduceMotion : ReduceMotion,
            pose ?? _pose);

    static double SanitizeVelocity(double velocity)
        => double.IsNaN(velocity) || double.IsInfinity(velocity) ? 0 : velocity;

    bool HasTrackSpring()
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (_tracks[i].TrackSpring is not null)
                return true;
        }

        return false;
    }

    bool HasTransformTrack()
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (_tracks[i].Semantic == SemanticTrack.Transform)
                return true;
        }

        return false;
    }

    bool HasPlaneTrack()
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (IsPlaneChannel(_tracks[i]))
                return true;
        }

        return false;
    }

    static bool IsPlaneChannel(MotionTrack track)
        => track.Semantic == SemanticTrack.Path
           || track.Property == VisualElement.TranslationXProperty
           || track.Property == VisualElement.TranslationYProperty
           || track.Property == VisualElement.ScaleProperty
           || track.Property == VisualElement.ScaleXProperty
           || track.Property == VisualElement.ScaleYProperty
           || track.Property == VisualElement.RotationProperty
           || track.Property == VisualElement.RotationXProperty
           || track.Property == VisualElement.RotationYProperty;

    static bool AllFullSpan(IReadOnlyList<MotionTrack> tracks)
    {
        for (var i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Begin != 0 || tracks[i].End != 1)
                return false;
        }

        return true;
    }

    static IReadOnlyList<MotionTrack> Rebase(IReadOnlyList<MotionTrack> tracks, uint fromSpan, uint toSpan)
    {
        if (fromSpan == toSpan || toSpan == 0 || tracks.Count == 0)
            return tracks;

        var scale = (double)fromSpan / toSpan;
        var rebased = new MotionTrack[tracks.Count];
        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            rebased[i] = track with
            {
                Begin = track.Begin * scale,
                End = track.End * scale,
            };
        }

        return rebased;
    }

    static IReadOnlyList<MotionTrack> Shift(
        IReadOnlyList<MotionTrack> tracks,
        uint atMs,
        uint childSpan,
        uint parentSpan)
    {
        if (tracks.Count == 0 || parentSpan == 0)
            return tracks;

        var shifted = new MotionTrack[tracks.Count];
        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            shifted[i] = track with
            {
                Begin = (atMs + track.Begin * childSpan) / parentSpan,
                End = (atMs + track.End * childSpan) / parentSpan,
            };
        }

        return shifted;
    }

    static void WarnOverlap(IReadOnlyList<MotionTrack> existing, IReadOnlyList<MotionTrack> incoming)
    {
        for (var i = 0; i < incoming.Count; i++)
        {
            var next = incoming[i];
            for (var j = 0; j < existing.Count; j++)
            {
                var prior = existing[j];
                if (prior.Property != next.Property || prior.Semantic != next.Semantic)
                    continue;
                if (prior.Begin < next.End && next.Begin < prior.End)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Motion: overlapping {next.Property?.PropertyName ?? next.Semantic.ToString()} tracks; later track wins.");
                }
            }
        }
    }

    static IReadOnlyList<MotionTrack> Concat(
        IReadOnlyList<MotionTrack> left,
        IReadOnlyList<MotionTrack> right)
    {
        if (left.Count == 0)
            return right;
        if (right.Count == 0)
            return left;

        var tracks = new MotionTrack[left.Count + right.Count];
        for (var i = 0; i < left.Count; i++)
            tracks[i] = left[i];
        for (var i = 0; i < right.Count; i++)
            tracks[left.Count + i] = right[i];
        return tracks;
    }

    static IReadOnlyList<NamedSpan> RebaseSpans(IReadOnlyList<NamedSpan> spans, uint fromSpan, uint toSpan)
    {
        if (fromSpan == toSpan || toSpan == 0 || spans.Count == 0)
            return spans;

        var scale = (double)fromSpan / toSpan;
        var rebased = new NamedSpan[spans.Count];
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            rebased[i] = span with { Begin = span.Begin * scale, End = span.End * scale };
        }

        return rebased;
    }

    static IReadOnlyList<NamedSpan> ShiftSpans(
        IReadOnlyList<NamedSpan> spans,
        uint atMs,
        uint childSpan,
        uint parentSpan)
    {
        if (spans.Count == 0 || parentSpan == 0)
            return spans;

        var shifted = new NamedSpan[spans.Count];
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            shifted[i] = span with
            {
                Begin = (atMs + span.Begin * childSpan) / parentSpan,
                End = (atMs + span.End * childSpan) / parentSpan,
            };
        }

        return shifted;
    }

    static IReadOnlyList<NamedSpan> ConcatSpans(
        IReadOnlyList<NamedSpan> left,
        IReadOnlyList<NamedSpan> right)
    {
        if (left.Count == 0)
            return right;
        if (right.Count == 0)
            return left;

        var spans = new NamedSpan[left.Count + right.Count];
        for (var i = 0; i < left.Count; i++)
            spans[i] = left[i];
        for (var i = 0; i < right.Count; i++)
            spans[left.Count + i] = right[i];
        return CompactSpans(spans);
    }

    static IReadOnlyList<NamedSpan> PutSpan(IReadOnlyList<NamedSpan> spans, NamedSpan next)
        => CompactSpans(ConcatSpans(spans, [next]));

    Motion Add(Motion child, uint at, string? id, bool followsEnd)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child._stagger is not null)
            System.Diagnostics.Debug.WriteLine("Motion.Add: nested Stagger is ignored (root only).");

        var childTracks = child._easingSet ? StampEasing(child._tracks, child.Easing) : child._tracks;
        var tracks = _tracks;
        var beats = _beats;
        var rootSpans = _spans;
        var trackBeats = _timeline && (beats.Count > 0 || tracks.Count == 0 || AllFullSpan(tracks));
        if (trackBeats && beats.Count == 0)
        {
            tracks = WithBeat(tracks, 0);
            beats = [new TimelineBeat(0, 0, false, 0, Duration, _durationSet, 0)];
            rootSpans = WithSpanBeat(rootSpans, 0, force: false);
        }

        var childStart = at + child.Delay;
        var childSpan = child.Duration;
        var newSpan = Math.Max(Duration, childStart + childSpan);
        var existing = Duration == newSpan ? tracks : Rebase(tracks, Duration, newSpan);
        var shifted = Shift(childTracks, childStart, childSpan, newSpan);
        var childSpans = ShiftSpans(child._spans, childStart, childSpan, newSpan);
        if (trackBeats)
        {
            var beatId = NextBeatId(beats);
            shifted = WithBeat(shifted, beatId);
            childSpans = WithSpanBeat(childSpans, beatId, force: true);
            var grown = new TimelineBeat[beats.Count + 1];
            for (var i = 0; i < beats.Count; i++)
                grown[i] = beats[i];
            grown[beats.Count] = new TimelineBeat(
                beatId, at, followsEnd, child.Delay, childSpan, child._durationSet, childStart);
            beats = grown;
        }

        WarnOverlap(existing, shifted);

        var spans = ConcatSpans(
            Duration == newSpan ? rootSpans : RebaseSpans(rootSpans, Duration, newSpan),
            childSpans);
        if (!string.IsNullOrWhiteSpace(id) && newSpan > 0)
        {
            var beatId = trackBeats ? beats[^1].Id : -1;
            spans = PutSpan(spans, new NamedSpan(
                id,
                (double)childStart / newSpan,
                (double)(childStart + childSpan) / newSpan)
            {
                BeatId = beatId,
            });
        }

        return Copy(
            duration: newSpan,
            tracks: Concat(existing, shifted),
            spans: spans,
            perspective: child._perspective,
            setPerspective: child._perspective.HasValue,
            beats: beats);
    }

    Motion ApplyDuration(uint milliseconds)
    {
        if (milliseconds == Duration)
            return _durationSet ? this : Copy(durationSet: true);

        var fullSpan = _tracks.Count == 0 || AllFullSpan(_tracks);
        if (fullSpan)
            return Copy(duration: milliseconds, durationSet: true);

        var span = Math.Max(Duration, milliseconds);
        if (span == Duration)
            return _durationSet ? this : Copy(durationSet: true);

        return Copy(
            duration: span,
            tracks: Rebase(_tracks, Duration, span),
            spans: RebaseSpans(_spans, Duration, span),
            durationSet: true);
    }

    Motion ReplaceSingleBeat(uint milliseconds)
    {
        var sized = ApplyDuration(milliseconds);
        var beat = sized._beats[0] with
        {
            Length = sized.Duration,
            LengthLocked = true,
            StartMs = 0,
        };
        if (ReferenceEquals(sized, this) && beat.Equals(_beats[0]))
            return this;
        return sized.Copy(beats: [beat]);
    }

    Motion RestretchBeats(uint milliseconds)
    {
        var count = _beats.Count;
        var placed = new (uint Start, uint Length)[count];
        long cursor = 0;
        var changed = false;
        for (var i = 0; i < count; i++)
        {
            var beat = _beats[i];
            var length = beat.LengthLocked ? beat.Length : milliseconds;
            if (length != beat.Length)
                changed = true;
            var origin = beat.FollowsEnd ? cursor : beat.At;
            var start = origin + beat.Delay;
            var end = start + length;
            if (end > cursor)
                cursor = end;
            placed[i] = (ClampUInt(start), ClampUInt(length));
        }

        var span = ClampUInt(cursor);
        if (!changed && span == Duration)
            return this;

        var oldDuration = Duration;
        var tracks = new MotionTrack[_tracks.Count];
        for (var i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            var index = BeatIndex(track.BeatId);
            if (index < 0 || oldDuration == 0)
            {
                tracks[i] = track;
                continue;
            }

            var beat = _beats[index];
            var place = placed[index];
            tracks[i] = track with
            {
                Begin = PlaceEdge(track.Begin, oldDuration, beat, place, span),
                End = PlaceEdge(track.End, oldDuration, beat, place, span),
            };
        }

        var spans = new NamedSpan[_spans.Count];
        for (var i = 0; i < _spans.Count; i++)
        {
            var named = _spans[i];
            spans[i] = named with
            {
                Begin = span == 0 ? 0 : MapBeatPoint(named.Begin * oldDuration, named.BeatId, _beats, placed) / span,
                End = span == 0 ? 0 : MapBeatPoint(named.End * oldDuration, named.BeatId, _beats, placed) / span,
            };
        }

        var beats = new TimelineBeat[count];
        for (var i = 0; i < count; i++)
        {
            beats[i] = _beats[i] with
            {
                Length = placed[i].Length,
                StartMs = placed[i].Start,
            };
        }

        return Copy(duration: span, tracks: tracks, spans: spans, beats: beats);
    }

    static double MapBeatPoint(
        double ms,
        int beatId,
        IReadOnlyList<TimelineBeat> beats,
        (uint Start, uint Length)[] placed)
    {
        var index = -1;
        if (beatId >= 0)
        {
            for (var i = 0; i < beats.Count; i++)
            {
                if (beats[i].Id == beatId)
                {
                    index = i;
                    break;
                }
            }
        }

        if (index < 0)
            index = BeatContaining(ms, beats);
        if (index >= 0)
            return ThroughBeat(beats[index], placed[index], ms);

        return ThroughGap(ms, beats, placed);
    }

    static int BeatContaining(double ms, IReadOnlyList<TimelineBeat> beats)
    {
        var best = -1;
        var bestLength = double.MaxValue;
        for (var i = 0; i < beats.Count; i++)
        {
            var beat = beats[i];
            var start = (double)beat.StartMs;
            var end = start + beat.Length;
            if (ms < start - 0.001 || ms > end + 0.001)
                continue;
            if (beat.Length == 0)
            {
                if (best < 0 && Math.Abs(ms - start) <= 0.001)
                    best = i;
                continue;
            }

            if (beat.Length < bestLength)
            {
                best = i;
                bestLength = beat.Length;
            }
        }

        return best;
    }

    static double ThroughGap(
        double ms,
        IReadOnlyList<TimelineBeat> beats,
        (uint Start, uint Length)[] placed)
    {
        var order = new int[beats.Count];
        for (var i = 0; i < beats.Count; i++)
            order[i] = i;
        Array.Sort(order, (a, b) => beats[a].StartMs.CompareTo(beats[b].StartMs));

        double prevOld = 0;
        double prevNew = 0;
        for (var n = 0; n < order.Length; n++)
        {
            var index = order[n];
            var start = (double)beats[index].StartMs;
            if (ms < start)
            {
                var gap = start - prevOld;
                var local = gap <= 0 ? 0 : (ms - prevOld) / gap;
                var newStart = (double)placed[index].Start;
                return prevNew + (local * (newStart - prevNew));
            }

            prevOld = start + beats[index].Length;
            prevNew = placed[index].Start + (double)placed[index].Length;
        }

        return prevNew + (ms - prevOld);
    }

    static double ThroughBeat(TimelineBeat beat, (uint Start, uint Length) place, double ms)
    {
        var local = beat.Length == 0 ? 0 : (ms - beat.StartMs) / beat.Length;
        if (local < 0)
            local = 0;
        else if (local > 1)
            local = 1;
        return place.Start + (local * place.Length);
    }

    int BeatIndex(int id)
    {
        for (var i = 0; i < _beats.Count; i++)
        {
            if (_beats[i].Id == id)
                return i;
        }

        return -1;
    }

    static double PlaceEdge(
        double fraction,
        uint oldDuration,
        TimelineBeat beat,
        (uint Start, uint Length) place,
        uint span)
    {
        if (span == 0)
            return 0;

        var local = beat.Length == 0
            ? 0
            : ((fraction * oldDuration) - beat.StartMs) / beat.Length;
        return (place.Start + (local * place.Length)) / span;
    }

    static uint ClampUInt(long value)
    {
        if (value <= 0)
            return 0;
        if (value >= uint.MaxValue)
            return uint.MaxValue;
        return (uint)value;
    }

    static MotionTrack[] StampEasing(IReadOnlyList<MotionTrack> tracks, Easing easing)
    {
        var copy = new MotionTrack[tracks.Count];
        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            copy[i] = track.Easing is null ? track with { Easing = easing } : track;
        }

        return copy;
    }

    static MotionTrack[] WithBeat(IReadOnlyList<MotionTrack> tracks, int beatId)
    {
        var copy = new MotionTrack[tracks.Count];
        for (var i = 0; i < tracks.Count; i++)
            copy[i] = tracks[i] with { BeatId = beatId };

        return copy;
    }

    static NamedSpan[] WithSpanBeat(IReadOnlyList<NamedSpan> spans, int beatId, bool force)
    {
        var copy = new NamedSpan[spans.Count];
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            copy[i] = !force && span.BeatId >= 0 ? span : span with { BeatId = beatId };
        }

        return copy;
    }

    static int NextBeatId(IReadOnlyList<TimelineBeat> beats)
    {
        var max = -1;
        for (var i = 0; i < beats.Count; i++)
        {
            if (beats[i].Id > max)
                max = beats[i].Id;
        }

        return max + 1;
    }

    static IReadOnlyList<NamedSpan> CompactSpans(IReadOnlyList<NamedSpan> spans)
    {
        if (spans.Count <= 1)
            return spans;

        var last = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < spans.Count; i++)
            last[spans[i].Id] = i;

        if (last.Count == spans.Count)
            return spans;

        System.Diagnostics.Debug.WriteLine("Motion: duplicate timeline id; last wins.");

        var compact = new NamedSpan[last.Count];
        var w = 0;
        for (var i = 0; i < spans.Count; i++)
        {
            if (last[spans[i].Id] == i)
                compact[w++] = spans[i];
        }

        return compact;
    }
}
