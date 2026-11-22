using Reactor.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Applies one <see cref="MotionTrack"/> to one view on each tick. Skips a view
/// <see cref="MotionPins"/> is holding so a motion does not fight another writer
/// for the same property.
/// A missing <c>From</c> is captured on the first forward and written back on reset.
/// The player already roots the view, so the runtime keeps that reference.
/// A tick that produces the same value does not write again, so a stagger hold
/// does not invalidate the view every frame. Skipping a pinned view forgets the
/// last value. The next free tick writes, even when the motion value matches.
/// </summary>
internal sealed class TrackRuntime
{
    readonly VisualElement _target;
    readonly BindableProperty _property;
    readonly object _to;
    readonly double _begin;
    readonly double _end;
    readonly Easing _easing;
    readonly IReadOnlyList<MotionKeyframe>? _keyframes;
    readonly ColorSpace _colorSpace;
    readonly PathSampler? _path;
    readonly double _pathFrom;
    readonly double _pathTo;
    readonly double? _perspective;
    readonly Func<double, Matrix4>? _transform;
    readonly bool _implicitFrom;
    readonly Spring? _trackSpring;
    readonly double _trackSpringVelocity;
    readonly bool _orient;
    object? _from;
    object? _lastWritten;
    bool _loggedPinSkip;
    bool _colorPrepared;
    CachedColorLerp _colorLerp;
    HsvColor[]? _keyframeHsv;
    bool _hasPoint;
    Point _lastPoint;
    bool _hasHeading;
    double _lastHeading;
    double _springX;
    double _springV;
    double _springElapsed;
    bool _springLive;
    bool _springStarted;
    bool _springSettled;

    public TrackRuntime(
        VisualElement target,
        BindableProperty property,
        object? from,
        object to,
        double begin,
        double end,
        Easing easing,
        IReadOnlyList<MotionKeyframe>? keyframes = null,
        ColorSpace colorSpace = ColorSpace.Hsv,
        PathSampler? path = null,
        double pathFrom = 0,
        double pathTo = 1,
        double? perspective = null,
        Func<double, Matrix4>? transform = null,
        Spring? trackSpring = null,
        double trackSpringVelocity = 0,
        bool orient = false)
    {
        _target = target;
        _property = property;
        _from = from;
        _to = to;
        _begin = begin;
        _end = end;
        _easing = easing;
        _keyframes = keyframes is { Count: > 0 } ? keyframes : null;
        _colorSpace = colorSpace;
        _path = path;
        _pathFrom = pathFrom;
        _pathTo = pathTo;
        _perspective = perspective;
        _transform = transform;
        _trackSpring = trackSpring;
        _trackSpringVelocity = trackSpringVelocity;
        _orient = orient;
        _implicitFrom = from is null;
    }

    public bool HasTrackSpring => _trackSpring is not null;

    public bool SpringLive => _springLive;

    public double WindowBegin => _begin;

    /// <summary>
    /// True while this spring owns the write. A settled spring keeps its pose
    /// until the run releases it, so the duration clock does not overwrite it.
    /// </summary>
    public bool TrackSpringOwnsFrame(double u)
        => _trackSpring is not null && _springLive && (_springStarted || u >= _begin);

    public void KickTrackSpring(int direction)
    {
        if (_trackSpring is null)
            return;

        _springX = direction > 0 ? 0 : 1;
        _springV = direction * _trackSpringVelocity;
        _springElapsed = 0;
        _springSettled = false;
        _springStarted = false;
        _springLive = true;
    }

    /// <summary>Seek leaves velocity at 0 and hands the pose back to the clock.</summary>
    public void ParkTrackSpring()
    {
        if (_trackSpring is null)
            return;

        _springV = 0;
        _springLive = false;
        _springStarted = false;
        _springSettled = false;
    }

    /// <summary>Drops spring ownership so the next clock write can land the end pose.</summary>
    public void CompleteTrackSpring()
    {
        if (_trackSpring is null)
            return;

        _springV = 0;
        _springSettled = true;
        _springStarted = false;
        _springLive = false;
    }

    /// <summary>Integrates once. Returns true when this spring has settled.</summary>
    public bool StepTrackSpring(double deltaMs, int direction)
    {
        if (_trackSpring is not { } spring || !_springLive || _springSettled)
            return true;

        _springStarted = true;
        var target = direction > 0 ? 1d : 0d;
        SpringStep.Integrate(spring, deltaMs, target, ref _springX, ref _springV, ref _springElapsed, out _springSettled);
        WriteSpringFrame();
        return _springSettled;
    }

    public void CaptureAndWriteFrom()
    {
        ForgetLastWrite();
        if (!TryTarget(out var view))
            return;

        _loggedPinSkip = false;
        if (_transform is not null)
        {
            if (!SkipPinned(view))
                MatrixPlane.Apply(view, _transform(0) ?? Matrix4.Identity);
            return;
        }

        if (_path is not null)
        {
            if (!SkipPinned(view))
                WritePoint(view, _path.PointAt(_pathFrom), Heading(_pathFrom));
            return;
        }

        if (_implicitFrom)
            _from = view.GetValue(_property);

        if (_from is not null && !SkipPinned(view))
            Commit(view, _from);
    }

    public void WriteFrom()
    {
        if (!TryTarget(out var view) || SkipPinned(view))
            return;
        if (_transform is not null)
        {
            MatrixPlane.Apply(view, _transform(0) ?? Matrix4.Identity);
            return;
        }

        if (_path is not null)
        {
            WritePoint(view, _path.PointAt(_pathFrom), Heading(_pathFrom));
            return;
        }

        if (_from is null)
            return;
        Commit(view, _from);
    }

    public void Apply(double u)
    {
        if (!TryTarget(out var view) || SkipPinned(view))
            return;
        if (TrackSpringOwnsFrame(u))
            return;

        if (_transform is not null)
        {
            MatrixPlane.Apply(view, _transform(Eased(u)) ?? Matrix4.Identity);
            return;
        }

        if (_path is not null)
        {
            var pathLocal = LocalU(u);
            var t = _pathFrom + (_pathTo - _pathFrom) * _easing.Ease(pathLocal);
            if (u <= _begin || _end <= _begin)
                t = _pathFrom;
            else if (u >= _end)
                t = _pathTo;
            WritePoint(view, _path.PointAt(t), Heading(t));
            return;
        }

        _from ??= view.GetValue(_property);
        if (_from is null)
            return;

        PrepareColors();
        var local = LocalU(u);
        object value;
        if (_keyframes is not null)
        {
            value = EvaluateKeyframes(local, _from);
        }
        else if (u <= _begin || _end <= _begin)
        {
            value = _from;
        }
        else if (u >= _end)
        {
            value = _to;
        }
        else
        {
            var eased = _easing.Ease(local);
            value = _colorLerp.LerpOrNull(eased)
                ?? PropertyLerp.Lerp(_from, _to, eased, _colorSpace)
                ?? _to;
        }

        Commit(view, value);
    }

    void Commit(VisualElement view, object value)
    {
        if (Equals(_lastWritten, value))
        {
            // Perspective is not a bindable property. Keep applying it so a
            // handler that attaches during a hold still receives the camera.
            if (_perspective is not null)
                ApplyPerspective(view);
            return;
        }

        WriteValue(view, value);
    }

    void WriteValue(VisualElement view, object value)
    {
        _lastWritten = value;
        if (TryWritePose(view, value))
            return;

        PropertyLerp.Write(view, _property, value);
        ApplyPerspective(view);
    }

    bool TryWritePose(VisualElement view, object value)
    {
        if (_perspective is not double entry || !IsPoseChannel(_property))
            return false;

#if WINDOWS
        WindowsPose.Write(view, _property, value, entry);
        return true;
#else
        _ = view;
        _ = value;
        _ = entry;
        return false;
#endif
    }

    void WriteSpringFrame()
    {
        if (!TryTarget(out var view) || SkipPinned(view))
            return;

        if (_transform is not null)
        {
            var t = Math.Clamp(_springX, 0, 1);
            MatrixPlane.Apply(view, _transform(t) ?? Matrix4.Identity);
            return;
        }

        if (_path is not null)
        {
            var t = _pathFrom + ((_pathTo - _pathFrom) * _springX);
            WritePoint(view, _path.PointAt(t), Heading(t));
            return;
        }

        _from ??= view.GetValue(_property);
        if (_from is null)
            return;

        PrepareColors();
        var value = _colorLerp.LerpOrNull(_springX)
            ?? PropertyLerp.Lerp(_from, _to, _springX, _colorSpace)
            ?? _to;
        WriteValue(view, value);
    }

    double? Heading(double t)
        => _orient && _path is not null ? _path.HeadingAt(t) : null;

    void PrepareColors()
    {
        if (_colorPrepared)
            return;

        _colorPrepared = true;
        _colorLerp.Prepare(_from, _to, _colorSpace);
        if (_keyframes is not { Count: > 0 } frames || _colorSpace == ColorSpace.Rgb)
            return;

        var hsv = new HsvColor[frames.Count];
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames[i].Value is not Color color)
                return;

            hsv[i] = HsvColor.From(color);
        }

        _keyframeHsv = hsv;
    }

    void ApplyPerspective(VisualElement view)
    {
        if (_perspective is not double entry)
            return;
        if (_property != VisualElement.RotationProperty
            && _property != VisualElement.RotationXProperty
            && _property != VisualElement.RotationYProperty)
        {
            return;
        }

        PerspectivePlane.Apply(view, entry);
    }

    double Eased(double u)
    {
        if (u <= _begin || _end <= _begin)
            return 0;
        if (u >= _end)
            return 1;
        return _easing.Ease(LocalU(u));
    }

    double LocalU(double u)
    {
        if (_end <= _begin)
            return u >= _begin ? 1 : 0;
        if (u <= _begin)
            return 0;
        if (u >= _end)
            return 1;
        return (u - _begin) / (_end - _begin);
    }

    object EvaluateKeyframes(double local, object from)
    {
        var frames = _keyframes!;
        if (local < frames[0].Offset)
            return from;

        for (var i = 0; i < frames.Count - 1; i++)
        {
            var next = frames[i + 1];
            if (local >= next.Offset)
                continue;

            var current = frames[i];
            var span = next.Offset - current.Offset;
            if (span <= 0)
                return next.Value;

            var s = (local - current.Offset) / span;
            var curve = next.Easing ?? _easing;
            if (_keyframeHsv is not null)
                return _keyframeHsv[i].Lerp(_keyframeHsv[i + 1], curve.Ease(s));

            return PropertyLerp.Lerp(current.Value, next.Value, curve.Ease(s), _colorSpace) ?? next.Value;
        }

        return frames[^1].Value;
    }

    void WritePoint(VisualElement view, Point point, double? heading)
    {
        var samePoint = _hasPoint && _lastPoint.X == point.X && _lastPoint.Y == point.Y;
        var sameHeading = heading is null || (_hasHeading && _lastHeading == heading.Value);
        if (samePoint && sameHeading)
            return;

        if (!samePoint)
        {
            _hasPoint = true;
            _lastPoint = point;
            WritePoseOrProperty(view, VisualElement.TranslationXProperty, point.X);
            WritePoseOrProperty(view, VisualElement.TranslationYProperty, point.Y);
        }

        if (heading is not double degrees || sameHeading)
            return;

        _hasHeading = true;
        _lastHeading = degrees;
        WritePoseOrProperty(view, VisualElement.RotationProperty, degrees);
    }

    void WritePoseOrProperty(VisualElement view, BindableProperty property, double value)
    {
        if (_perspective is not double entry || !IsPoseChannel(property))
        {
            PropertyLerp.Write(view, property, value);
            return;
        }

#if WINDOWS
        WindowsPose.Write(view, property, value, entry);
#else
        _ = entry;
        PropertyLerp.Write(view, property, value);
#endif
    }

    static bool IsPoseChannel(BindableProperty property)
        => property == VisualElement.TranslationXProperty
           || property == VisualElement.TranslationYProperty
           || property == VisualElement.ScaleProperty
           || property == VisualElement.ScaleXProperty
           || property == VisualElement.ScaleYProperty
           || property == VisualElement.RotationProperty
           || property == VisualElement.RotationXProperty
           || property == VisualElement.RotationYProperty;

    void ForgetLastWrite()
    {
        _colorPrepared = false;
        _keyframeHsv = null;
        _lastWritten = null;
        _hasPoint = false;
        _hasHeading = false;
    }

    bool SkipPinned(VisualElement view)
    {
        if (!MotionPins.IsPinned(view))
            return false;

        // The other writer may have changed the property. The next free tick must write.
        _lastWritten = null;
        _hasPoint = false;
        _hasHeading = false;
        if (!_loggedPinSkip)
        {
            _loggedPinSkip = true;
            System.Diagnostics.Debug.WriteLine("Motion: skip writes on a pinned view.");
        }

        return true;
    }

    bool TryTarget(out VisualElement view)
    {
        view = _target;
        if (view.Handler is not null && !view.IsLoaded)
        {
            _lastWritten = null;
            _hasPoint = false;
            _hasHeading = false;
            return false;
        }

        return true;
    }
}
