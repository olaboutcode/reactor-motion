namespace Reactor.Motion;

/// <summary>
/// Finger axes for <see cref="Motion.Drag(VisualElement, Spring, Func{DragPlane, DragPlane})"/>.
/// X follows the horizontal pan. Y follows the vertical pan.
/// Leave an axis off and that direction stays put.
/// </summary>
public sealed class DragPlane
{
    readonly Axis? _x;
    readonly Axis? _y;

    /// <summary>An empty plane. Add <see cref="X(Func{Motion, Motion}, double?, IReadOnlyList{double}?)"/> or <see cref="Y(Func{Motion, Motion}, double?, IReadOnlyList{double}?)"/>.</summary>
    public DragPlane()
    {
    }

    DragPlane(Axis? x, Axis? y)
    {
        _x = x;
        _y = y;
    }

    internal Axis? XAxis => _x;

    internal Axis? YAxis => _y;

    internal readonly record struct Axis(Motion Motion, double Travel, double[] Stops);

    /// <summary>
    /// Horizontal finger motion drives <paramref name="configure"/>, which receives an empty motion.
    /// A single <c>TranslateX</c> or <c>TranslateY</c> with a from and a to uses <c>to - from</c> as the finger travel.
    /// Pass <paramref name="travel"/> when the finger span and the property span differ, or when the recipe is not one translation.
    /// <paramref name="stops"/> are optional progress stops. Omit them and the release uses 0 and 1.
    /// </summary>
    public DragPlane X(Func<Motion, Motion> configure, double? travel = null, IReadOnlyList<double>? stops = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return X(configure(Motion.None), travel, stops);
    }

    /// <summary>
    /// Horizontal finger motion drives <paramref name="motion"/>.
    /// Travel and <paramref name="stops"/> follow <see cref="X(Func{Motion, Motion}, double?, IReadOnlyList{double}?)"/>.
    /// </summary>
    public DragPlane X(Motion motion, double? travel = null, IReadOnlyList<double>? stops = null)
        => With(motion, travel, horizontal: true, stops);

    /// <summary>
    /// Vertical finger motion drives <paramref name="configure"/>, which receives an empty motion.
    /// Travel and <paramref name="stops"/> follow <see cref="X(Func{Motion, Motion}, double?, IReadOnlyList{double}?)"/>.
    /// </summary>
    public DragPlane Y(Func<Motion, Motion> configure, double? travel = null, IReadOnlyList<double>? stops = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Y(configure(Motion.None), travel, stops);
    }

    /// <summary>
    /// Vertical finger motion drives <paramref name="motion"/>.
    /// Travel and <paramref name="stops"/> follow <see cref="X(Func{Motion, Motion}, double?, IReadOnlyList{double}?)"/>.
    /// </summary>
    public DragPlane Y(Motion motion, double? travel = null, IReadOnlyList<double>? stops = null)
        => With(motion, travel, horizontal: false, stops);

    DragPlane With(Motion motion, double? travel, bool horizontal, IReadOnlyList<double>? stops)
    {
        ArgumentNullException.ThrowIfNull(motion);
        System.Diagnostics.Debug.Assert(
            motion.Duration > 0,
            "A drag recipe needs a duration so seek can park between the ends.");
        var axis = new Axis(motion, TravelOf(motion, travel), Detent.Normalize(stops));
        return horizontal ? new DragPlane(axis, _y) : new DragPlane(_x, axis);
    }

    internal static double TravelOf(Motion motion, double? travel)
    {
        if (travel is double given)
            return Normalize(given);
        if (TryTranslationSpan(motion, out var span))
            return span;
        System.Diagnostics.Debug.Assert(false, "Pass a travel when the drag recipe is not one translation.");
        return 1;
    }

    static double Normalize(double travel)
        => double.IsNaN(travel) || double.IsInfinity(travel) || Math.Abs(travel) < 1e-6 ? 1 : travel;

    static bool TryTranslationSpan(Motion motion, out double span)
    {
        span = 1;
        if (motion.Tracks.Count != 1)
            return false;
        var track = motion.Tracks[0];
        var translation = track.Property == VisualElement.TranslationXProperty
            || track.Property == VisualElement.TranslationYProperty;
        if (!translation || track.From is not double from || track.To is not double to)
            return false;
        var delta = to - from;
        if (double.IsNaN(delta) || double.IsInfinity(delta) || Math.Abs(delta) < 1e-6)
            return false;
        span = delta;
        return true;
    }
}

/// <summary>
/// A drag bound by <see cref="Motion.Drag(VisualElement, Spring, Func{DragPlane, DragPlane})"/>.
/// Each axis has its own player. <see cref="Release"/> springs that axis to a stop.
/// <see cref="Coast"/> keeps the finger speed and rests where that speed dies.
/// </summary>
public sealed class Drag : IDisposable
{
    readonly Spring _spring;
    readonly double[] _xStops;
    readonly double[] _yStops;
    readonly MotionClock? _xClock;
    readonly MotionClock? _yClock;
    bool _disposed;

    internal Drag(VisualElement target, Spring spring, DragPlane plane, bool manual)
    {
        _spring = spring;
        _xStops = plane.XAxis?.Stops ?? Detent.Ends;
        _yStops = plane.YAxis?.Stops ?? Detent.Ends;
        if (plane.XAxis is { } x)
        {
            _xClock = new MotionClock(manual);
            X = MotionPlayer.Create(x.Motion, [target], _xClock);
        }

        if (plane.YAxis is { } y)
        {
            _yClock = new MotionClock(manual);
            Y = MotionPlayer.Create(y.Motion, [target], _yClock);
        }
    }

    /// <summary>Player for the horizontal finger axis. Null when the plane has no <c>X</c>.</summary>
    public MotionPlayer? X { get; }

    /// <summary>Player for the vertical finger axis. Null when the plane has no <c>Y</c>.</summary>
    public MotionPlayer? Y { get; }

    /// <summary>
    /// Parks the axes you pass. Null leaves that axis alone.
    /// NaN and infinities seek to 0. Values outside 0–1 clamp. A seek cancels an in-flight settle on that axis.
    /// </summary>
    public void Seek(double? x = null, double? y = null)
    {
        ThrowIfDisposed();
        if (x is double px)
            X?.SeekFraction(px);
        if (y is double py)
            Y?.SeekFraction(py);
    }

    /// <summary>
    /// Springs each bound axis to a stop.
    /// Omit stops and they are 0 and 1, so progress at or above 0.5 targets 1.
    /// With more stops, a release slower than 2 targets the nearest stop.
    /// The same distance picks the higher stop. A release faster than 2 toward
    /// a neighbor targets that neighbor. Velocity is progress units per second.
    /// Positive moves toward 1. Speed 0, including a canceled pan, uses the nearest stop.
    /// </summary>
    public void Release(double velocityX = 0, double velocityY = 0)
    {
        ThrowIfDisposed();
        X?.SpringToNearest(_spring, velocityX, _xStops);
        Y?.SpringToNearest(_spring, velocityY, _yStops);
    }

    /// <summary>
    /// Decays each bound axis from its current progress.
    /// Velocity is progress units per second. Positive moves toward 1.
    /// The release spring's damping is the friction. Stiffness does not pull
    /// toward an end. The rest stays inside 0–1, and reaching an end stops there.
    /// Speed 0, including a canceled pan, stays put. NaN and infinities count as 0.
    /// </summary>
    public void Coast(double velocityX = 0, double velocityY = 0)
    {
        ThrowIfDisposed();
        X?.Coast(_spring, velocityX);
        Y?.Coast(_spring, velocityY);
    }

    /// <summary>Stops both players. A later <see cref="Release"/> or <see cref="Coast"/> throws.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        X?.Dispose();
        Y?.Dispose();
    }

    internal void Tick(double deltaMilliseconds)
    {
        _xClock?.Tick(deltaMilliseconds);
        _yClock?.Tick(deltaMilliseconds);
    }

    void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}
