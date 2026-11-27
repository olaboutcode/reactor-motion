namespace Reactor.Motion;

/// <summary>
/// A pinch bound by <see cref="Motion.Pinch(VisualElement, Motion, Motion?)"/>.
/// The scale player follows the finger span. The rotate player follows the
/// finger angle when a rotate recipe was passed. <see cref="Release"/> leaves
/// the progress. <see cref="Cancel"/> returns to where <see cref="Begin"/> ran.
/// </summary>
public sealed class Pinch : IDisposable
{
    readonly bool _scaleReducing;
    readonly bool _rotateReducing;
    readonly double _scaleFrom;
    readonly double _scaleTo;
    readonly double _rotateFrom;
    readonly double _rotateTo;
    readonly bool _hasScale;
    readonly bool _hasRotate;
    double _scaleOrigin;
    double _rotateOrigin;
    bool _disposed;

    internal Pinch(VisualElement target, Motion scale, Motion? rotate, bool manual)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(scale);
        System.Diagnostics.Debug.Assert(
            scale.Duration > 0,
            "A pinch scale recipe needs a duration so seek can park between the ends.");
        if (rotate is not null)
        {
            System.Diagnostics.Debug.Assert(
                rotate.Duration > 0,
                "A pinch rotate recipe needs a duration so seek can park between the ends.");
        }

        var clock = new MotionClock(manual);
        Scale = MotionPlayer.Create(scale, [target], clock);
        _scaleReducing = Reducing(scale);
        _hasScale = PinchPlan.TrySpan(scale, VisualElement.ScaleXProperty, out _scaleFrom, out _scaleTo)
            || PinchPlan.TrySpan(scale, VisualElement.ScaleYProperty, out _scaleFrom, out _scaleTo)
            || PinchPlan.TrySpan(scale, VisualElement.ScaleProperty, out _scaleFrom, out _scaleTo);
        System.Diagnostics.Debug.Assert(_hasScale, "A pinch scale recipe needs a from and a to.");

        if (rotate is not null)
        {
            Rotate = MotionPlayer.Create(rotate, [target], new MotionClock(manual));
            _rotateReducing = Reducing(rotate);
            _hasRotate = PinchPlan.TrySpan(rotate, VisualElement.RotationProperty, out _rotateFrom, out _rotateTo);
            System.Diagnostics.Debug.Assert(_hasRotate, "A pinch rotate recipe needs a from and a to.");
        }

        Rest();
    }

    /// <summary>Player for the scale recipe.</summary>
    public MotionPlayer Scale { get; }

    /// <summary>Player for the rotate recipe. Null when no rotate recipe was passed.</summary>
    public MotionPlayer? Rotate { get; }

    /// <summary>Remembers the progress a later <see cref="Cancel"/> restores.</summary>
    public void Begin()
    {
        ThrowIfDisposed();
        _scaleOrigin = Scale.Progress;
        _rotateOrigin = Rotate?.Progress ?? 0;
    }

    /// <summary>
    /// Parks the pose for this finger sample.
    /// <paramref name="ratio"/> is the current span divided by the span at <see cref="Begin"/>.
    /// 1 keeps the scale. Wider than 1 moves toward the larger scale.
    /// <paramref name="degrees"/> is the turn since <see cref="Begin"/>. Positive is clockwise.
    /// A non-finite ratio counts as 1. A non-positive ratio counts as 1.
    /// A non-finite angle counts as 0. Values outside the recipe clamp.
    /// Reduce motion stays at the from pose.
    /// </summary>
    public void Seek(double ratio = 1, double degrees = 0)
    {
        ThrowIfDisposed();
        if (_scaleReducing)
            Scale.SeekFraction(0);
        else if (_hasScale)
            Scale.SeekFraction(PinchPlan.Scale(_scaleOrigin, ratio, _scaleFrom, _scaleTo));

        if (Rotate is null)
            return;
        if (_rotateReducing)
            Rotate.SeekFraction(0);
        else if (_hasRotate)
            Rotate.SeekFraction(PinchPlan.Turn(_rotateOrigin, degrees, _rotateFrom, _rotateTo));
    }

    /// <summary>Leaves the progress where the last <see cref="Seek"/> put it.</summary>
    public void Release()
        => ThrowIfDisposed();

    /// <summary>Returns both axes to the progress <see cref="Begin"/> stored.</summary>
    public void Cancel()
    {
        ThrowIfDisposed();
        Scale.SeekFraction(_scaleReducing ? 0 : _scaleOrigin);
        Rotate?.SeekFraction(_rotateReducing ? 0 : _rotateOrigin);
    }

    /// <summary>Stops the players. A later <see cref="Seek"/> throws.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Scale.Dispose();
        Rotate?.Dispose();
    }

    void Rest()
    {
        Scale.SeekFraction(0);
        Rotate?.SeekFraction(0);
        _scaleOrigin = 0;
        _rotateOrigin = 0;
    }

    void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    static bool Reducing(Motion motion)
        => motion.ReduceMotion ?? MotionPreferences.IsReduceMotionEnabled();
}
