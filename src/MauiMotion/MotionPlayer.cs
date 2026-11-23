
namespace Reactor.Motion;

/// <summary>
/// Plays a <see cref="Motion"/> on one or more views. One player owns each
/// property it writes. A later player takes those properties and leaves the rest.
/// </summary>
public sealed class MotionPlayer : IDisposable
{
    readonly VisualElement[] _targets;
    readonly TrackRuntime[] _runtimes;
    readonly MotionClock _clock;
    readonly ProgressListeners _at = new();
    readonly Lock _gate = new();

    TaskCompletionSource<bool>? _pending;
    CancellationTokenRegistration _tokenReg;
    MotionPlaybackEventArgs? _runArgs;
    MotionPlaybackStatus _pausedWas;
    bool _captured;
    bool _disposed;
    double _elapsedMs;
    double _holdMs;
    int _direction = 1;
    int _repeatRemaining;
    bool _autoRepeat = true;
    double _x;
    double _v;
    double _springElapsedMs;
    Spring? _gestureSpring;
    bool _hasTimedTracks;
    bool _hasTrackSprings;
    double? _poseKick;
    List<PoseSpeed>? _handoff;

    internal MotionPlayer(Motion motion, IReadOnlyList<VisualElement> targets, MotionClock? clock = null)
    {
        Motion = motion ?? throw new ArgumentNullException(nameof(motion));
        _targets = targets as VisualElement[] ?? [.. targets];
        _clock = clock ?? new MotionClock();
        _clock.Connect(OnDelta);
        var parentSpan = motion.Duration;
        var maxDelay = motion.StaggerSpec is { } stagger && _targets.Length > 1 && parentSpan > 0
            ? StaggerEval.MaxDelayMs(_targets.Length, stagger)
            : 0;
        Duration = parentSpan == 0 ? 0 : parentSpan + (uint)Math.Round(maxDelay);
        _runtimes = BuildRuntimes(motion, _targets, parentSpan, Duration);
        NoteTracks();
        foreach (var target in _targets)
            MotionPlayers.Register(target, this);
    }

    internal void CollectClaims(VisualElement view, HashSet<BindableProperty> claims)
    {
        foreach (var runtime in _runtimes)
            runtime.AddClaims(view, claims);
    }

    internal void CollectSideProperties(
        VisualElement view,
        HashSet<BindableProperty> stolen,
        HashSet<BindableProperty> side)
    {
        foreach (var runtime in _runtimes)
        {
            if (!runtime.Hits(view, stolen))
                continue;
            runtime.AddClaims(view, side);
        }

        foreach (var property in stolen)
            side.Remove(property);
    }

    /// <summary>
    /// Stops tracks on <paramref name="view"/> that write any stolen property.
    /// Returns true when this player has no tracks left.
    /// </summary>
    internal bool Release(VisualElement view, HashSet<BindableProperty> stolen)
    {
        foreach (var runtime in _runtimes)
            runtime.ReleaseIf(view, stolen);

        foreach (var runtime in _runtimes)
        {
            if (!runtime.IsReleased)
                return false;
        }

        return true;
    }

    void NoteTracks()
    {
        foreach (var runtime in _runtimes)
        {
            if (runtime.HasTrackSpring)
                _hasTrackSprings = true;
            else
                _hasTimedTracks = true;
        }

        if (Motion.Spring is not null && _hasTrackSprings)
            System.Diagnostics.Debug.Assert(false, "WithSpring and Settle cannot share a recipe.");
    }

    bool Reducing => Motion.ReduceMotion ?? MotionPreferences.IsReduceMotionEnabled();

    internal static MotionPlayer Create(
        Motion motion,
        IReadOnlyList<VisualElement> targets,
        MotionClock? clock = null)
        => new(motion, targets, clock);

    /// <summary>The recipe this player runs.</summary>
    public Motion Motion { get; }

    /// <summary>Where playback is.</summary>
    internal MotionPlaybackStatus Status { get; private set; } = MotionPlaybackStatus.Dismissed;

    /// <summary>No run, or a reverse or <see cref="Reset"/> returned to the start.</summary>
    public bool IsDismissed() => Status == MotionPlaybackStatus.Dismissed;

    /// <summary>Playing toward the end.</summary>
    public bool IsForward() => Status == MotionPlaybackStatus.Forward;

    /// <summary>Playing toward the start.</summary>
    public bool IsReverse() => Status == MotionPlaybackStatus.Reverse;

    /// <summary>Stopped in the middle by pause, cancel, or a seek inside the span.</summary>
    public bool IsPaused() => Status == MotionPlaybackStatus.Paused;

    /// <summary>A forward run reached the end, including the last repeat.</summary>
    public bool IsCompleted() => Status == MotionPlaybackStatus.Completed;

    /// <summary>
    /// Eased 0–1. Increases on forward and decreases on reverse.
    /// A spring, including a drag settle, reports its position clamped to 0–1.
    /// </summary>
    public double Progress { get; private set; }

    /// <summary>Playing forward or reverse.</summary>
    public bool IsRunning
        => Status is MotionPlaybackStatus.Forward or MotionPlaybackStatus.Reverse;

    /// <summary>
    /// Recipe length plus the longest stagger delay, in milliseconds.
    /// <see cref="Motion.Delay"/> is a hold in front of this span and is not included.
    /// </summary>
    public uint Duration { get; }

    /// <summary>A run begins, including each repeat and a direction change.</summary>
    public event EventHandler<MotionPlaybackEventArgs>? Started;

    /// <summary>
    /// A forward pass reached the end. A yoyo raises this before the automatic reverse.
    /// </summary>
    public event EventHandler<MotionPlaybackEventArgs>? Completed;

    /// <summary>
    /// The clock stopped in the middle. Raised once when leaving a running state.
    /// </summary>
    public event EventHandler<MotionPlaybackEventArgs>? Paused;

    /// <summary>
    /// <see cref="Resume"/> continued a paused run that still has a pending task.
    /// </summary>
    public event EventHandler<MotionPlaybackEventArgs>? Resumed;

    /// <summary>Any change of <see cref="Status"/>.</summary>
    public event EventHandler<MotionPlaybackEventArgs>? StatusChanged;

    /// <summary>
    /// Invokes <paramref name="callback"/> with <see cref="Progress"/> now and on
    /// every later tick, including decreases.
    /// </summary>
    public void At(Action<double> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_gate)
            _at.Add(callback);
        MotionPlaybackEventArgs.Invoke(callback, Progress);
    }

    internal Task ForwardAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        switch (Status)
        {
            case MotionPlaybackStatus.Completed:
                return Task.CompletedTask;
            case MotionPlaybackStatus.Forward when HasLivePending:
                return _pending!.Task;
            case MotionPlaybackStatus.Paused when _pausedWas == MotionPlaybackStatus.Forward && HasLivePending:
                ResumeRunning();
                return _pending!.Task;
            case MotionPlaybackStatus.Paused when _pausedWas == MotionPlaybackStatus.Forward:
                return StartRun(MotionPlaybackStatus.Forward, recapture: false, fireStarted: true, cancellationToken);
            case MotionPlaybackStatus.Reverse:
            case MotionPlaybackStatus.Paused when _pausedWas == MotionPlaybackStatus.Reverse:
                _autoRepeat = true;
                SupersedePending();
                return StartRun(MotionPlaybackStatus.Forward, recapture: false, fireStarted: true, cancellationToken);
            default:
                _autoRepeat = true;
                return StartRun(MotionPlaybackStatus.Forward, recapture: true, fireStarted: true, cancellationToken);
        }
    }

    internal Task ReverseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        switch (Status)
        {
            case MotionPlaybackStatus.Dismissed:
                return Task.CompletedTask;
            case MotionPlaybackStatus.Reverse when HasLivePending:
                return _pending!.Task;
            case MotionPlaybackStatus.Paused when _pausedWas == MotionPlaybackStatus.Reverse && HasLivePending:
                ResumeRunning();
                return _pending!.Task;
            case MotionPlaybackStatus.Paused when _pausedWas == MotionPlaybackStatus.Reverse:
                return StartRun(MotionPlaybackStatus.Reverse, recapture: false, fireStarted: true, cancellationToken);
            case MotionPlaybackStatus.Forward:
            case MotionPlaybackStatus.Paused when _pausedWas == MotionPlaybackStatus.Forward:
                _autoRepeat = false;
                SupersedePending();
                return StartRun(MotionPlaybackStatus.Reverse, recapture: false, fireStarted: true, cancellationToken);
            default:
                _autoRepeat = false;
                return StartRun(MotionPlaybackStatus.Reverse, recapture: false, fireStarted: true, cancellationToken);
        }
    }

    /// <summary>
    /// Stops the clock. The in-flight task stays pending. No-op when not running.
    /// </summary>
    public void Pause()
    {
        ThrowIfDisposed();
        if (!IsRunning)
            return;
        _clock.Stop();
        _pausedWas = Status;
        SetStatus(MotionPlaybackStatus.Paused);
        Raise(Paused);
    }

    /// <summary>
    /// Continues a paused run. No-op unless <see cref="IsPaused"/> is true.
    /// </summary>
    public void Resume()
    {
        ThrowIfDisposed();
        if (Status != MotionPlaybackStatus.Paused)
            return;

        if (HasLivePending)
        {
            ResumeRunning();
            return;
        }

        _ = StartRun(_pausedWas, recapture: false, fireStarted: true, CancellationToken.None);
    }

    /// <summary>
    /// Stops, cancels the pending task, writes captured from-values, and leaves the player dismissed.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();
        _clock.Stop();
        CancelPending();
        _elapsedMs = 0;
        _holdMs = 0;
        _gestureSpring = null;
        _direction = 1;
        if (_captured)
        {
            foreach (var runtime in _runtimes)
                runtime.WriteFrom();
        }

        SetStatus(MotionPlaybackStatus.Dismissed);
        ReportProgress();
    }

    /// <summary>
    /// Seeks to linear wall-clock <paramref name="milliseconds"/>. <c>0</c> dismisses.
    /// A value at or past <see cref="Duration"/> completes. Does not start playback.
    /// </summary>
    public void Seek(uint milliseconds)
    {
        ThrowIfDisposed();
        var u = Duration == 0 ? 1 : milliseconds / (double)Duration;
        SeekLinear(u);
    }

    /// <summary>
    /// Seeks to the start of a named timeline child. Position is linear player
    /// time (same as <see cref="Seek(uint)"/>), not eased <see cref="SeekFraction"/>.
    /// </summary>
    public void Seek(string id)
    {
        ThrowIfDisposed();
        if (!TrySpan(id, out var begin, out _))
            return;
        SeekLinear(begin);
    }

    /// <summary>
    /// Linear 0–1 of <see cref="Duration"/> (player span after stagger) for a
    /// named <see cref="Motion.Add(Motion, uint, string?, int?)"/> / <see cref="Motion.Then(Motion, string?, int?)"/> child.
    /// </summary>
    public bool TrySpan(string id, out double begin, out double end)
    {
        begin = 0;
        end = 0;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        foreach (var span in Motion.NamedSpans)
        {
            if (!string.Equals(span.Id, id, StringComparison.Ordinal))
                continue;

            var parent = Motion.Duration;
            if (parent == 0 || Duration == 0)
            {
                begin = span.Begin;
                end = span.End;
                return true;
            }

            begin = span.Begin * parent / Duration;
            end = span.End * parent / Duration;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Seeks to eased progress <paramref name="t"/> (same units as
    /// <see cref="Progress"/>). Leaves the player paused
    /// for <c>0 &lt; t &lt; 1</c>. Does not start playback. <see cref="Seek(uint)"/> is linear wall-clock.
    /// </summary>
    public void SeekFraction(double t)
    {
        ThrowIfDisposed();
        t = double.IsNaN(t) || double.IsInfinity(t) ? 0 : Math.Clamp(t, 0, 1);
        EasingInvert.TryInvert(Motion.Easing, t, out var u);
        SeekLinear(u);
    }

    /// <summary>
    /// Springs to the nearer end. Progress at or above 0.5 targets 1. Below 0.5
    /// targets 0. <paramref name="velocity"/> is progress units per second and is
    /// the spring's initial speed. Positive velocity moves toward 1. NaN and
    /// infinities count as 0. Delay, repeat, and yoyo do not continue this settle.
    /// <see cref="Motion.WithReduceMotion"/> finishes on this call.
    /// </summary>
    public void SpringToNearest(Spring spring, double velocity = 0)
    {
        ThrowIfDisposed();
        var progress = Progress;
        if (double.IsNaN(progress) || double.IsInfinity(progress))
            progress = 0;
        if (double.IsNaN(velocity) || double.IsInfinity(velocity))
            velocity = 0;

        var forward = progress >= 0.5;
        _autoRepeat = false;
        _holdMs = 0;
        _gestureSpring = spring;
        _x = progress;
        _v = velocity;
        _direction = forward ? 1 : -1;
        _springElapsedMs = 0;
        ParkTrackSprings();
        if (!HasLivePending)
            _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        SetStatus(forward ? MotionPlaybackStatus.Forward : MotionPlaybackStatus.Reverse);
        _runArgs = NewArgs();
        Raise(Started);

        var atEnd = forward ? progress >= 1 : progress <= 0;
        if (atEnd || Motion.ReduceMotion == true)
        {
            if (forward)
                FinishForward();
            else
                FinishReverse(cancelPending: false);
            return;
        }

        foreach (var runtime in _runtimes)
            runtime.WriteSpringProgress(_x);
        ReportProgress();
        StartClock();
    }

    void SeekLinear(double u)
    {
        u = double.IsNaN(u) || double.IsInfinity(u) ? 0 : Math.Clamp(u, 0, 1);
        _gestureSpring = null;
        _autoRepeat = false;
        _clock.Stop();
        _holdMs = 0;
        _elapsedMs = u * Duration;
        ResetSpring(u);
        ParkTrackSprings();
        Apply(u);

        if (u >= 1)
        {
            FinishForward();
            return;
        }

        if (u <= 0)
        {
            FinishReverse(cancelPending: true);
            return;
        }

        PauseForSeek();
        ReportProgress();
    }

    void PauseForSeek()
    {
        if (IsRunning)
        {
            _pausedWas = Status;
            SetStatus(MotionPlaybackStatus.Paused);
            Raise(Paused);
            return;
        }

        if (Status is MotionPlaybackStatus.Dismissed or MotionPlaybackStatus.Completed)
        {
            _pausedWas = MotionPlaybackStatus.Forward;
            SetStatus(MotionPlaybackStatus.Paused);
        }
    }

    /// <summary>
    /// Spring speeds still on this player, in property units per second.
    /// A dismissed or completed whole-recipe spring contributes nothing, so a
    /// replacement keeps its own authored kick.
    /// </summary>
    internal List<PoseSpeed> SampleSpeeds()
    {
        var speeds = new List<PoseSpeed>();
        if (Motion.Spring is not null)
        {
            if (Status is MotionPlaybackStatus.Dismissed or MotionPlaybackStatus.Completed)
                return speeds;
            foreach (var runtime in _runtimes)
            {
                if (!runtime.IsReleased)
                    runtime.AddSpeed(speeds, _v);
            }

            return speeds;
        }

        foreach (var runtime in _runtimes)
        {
            if (!runtime.IsReleased)
                runtime.AddTrackSpeed(speeds);
        }
        return speeds;
    }

    internal void AcceptHandoff(List<PoseSpeed> speeds)
    {
        if (!Motion.Pose || speeds.Count == 0)
            return;
        _handoff ??= [];
        _handoff.AddRange(speeds);
    }

    void ApplyPoseHandoff()
    {
        var handoff = _handoff;
        if (handoff is null || handoff.Count == 0)
            return;
        _handoff = null;

        if (Motion.Spring is not null)
        {
            foreach (var runtime in _runtimes)
            {
                if (!runtime.TryPoseProgress(handoff, out var velocity) || velocity == 0)
                    continue;
                _poseKick = velocity;
                return;
            }

            return;
        }

        foreach (var runtime in _runtimes)
            runtime.AdoptPoseKick(handoff);
    }

    /// <summary>
    /// Stops the clock, cancels the pending task, and unregisters this player from its views.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _clock.Stop();
        CancelPending();
        _tokenReg.Dispose();
        foreach (var target in _targets)
            MotionPlayers.Unregister(target, this);
    }

    bool HasLivePending => _pending is { Task.IsCompleted: false };

    Task StartRun(
        MotionPlaybackStatus running,
        bool recapture,
        bool fireStarted,
        CancellationToken cancellationToken)
    {
        if (_targets.Length == 0)
        {
            SetStatus(running == MotionPlaybackStatus.Forward
                ? MotionPlaybackStatus.Completed
                : MotionPlaybackStatus.Dismissed);
            return Task.CompletedTask;
        }

        _gestureSpring = null;
        _direction = running == MotionPlaybackStatus.Forward ? 1 : -1;
        if (recapture)
        {
            foreach (var runtime in _runtimes)
                runtime.CaptureAndWriteFrom(Motion.Pose);
            ApplyPoseHandoff();
            _captured = true;
            _elapsedMs = 0;
            _repeatRemaining = Motion.RepeatCount < 0 ? -1 : Math.Max(1, Motion.RepeatCount);
            ResetSpring(_direction > 0 ? 0 : 1, kick: true);
            KickTrackSprings();
        }
        else if (Status == MotionPlaybackStatus.Completed && running == MotionPlaybackStatus.Reverse)
        {
            _elapsedMs = Duration;
            ResetSpring(1, kick: true);
            KickTrackSprings();
        }

        ArmHold(running, recapture);

        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = _pending.Task;
        ArmToken(cancellationToken);
        SetStatus(running);
        _runArgs = NewArgs();
        if (fireStarted)
            Raise(Started);
        Apply(PlaybackU);
        ReportProgress();

        if (cancellationToken.IsCancellationRequested)
        {
            CancelToPaused();
            return task;
        }

        if (Reducing)
        {
            if (_direction > 0)
                FinishForward();
            else
                FinishReverse(cancelPending: false);
            return task;
        }

        if (Duration == 0 && Motion.Spring is null && !_hasTrackSprings && _holdMs <= 0)
        {
            if (_direction > 0)
                FinishForward();
            else
                FinishReverse(cancelPending: false);
            return task;
        }

        StartClock();
        return task;
    }

    void StartClock()
        => _clock.Start(_targets);

    void ResumeRunning()
    {
        SetStatus(_pausedWas);
        _runArgs = _runArgs is null ? NewArgs() : _runArgs;
        _runArgs.Status = Status;
        Raise(Resumed);
        StartClock();
    }

    void ArmHold(MotionPlaybackStatus running, bool recapture)
    {
        var resumeForwardHold = running == MotionPlaybackStatus.Forward
            && !recapture
            && Status == MotionPlaybackStatus.Paused
            && _pausedWas == MotionPlaybackStatus.Forward;
        if (resumeForwardHold)
            return;

        _holdMs = running == MotionPlaybackStatus.Forward && recapture
            ? Motion.Delay
            : 0;
    }

    /// <summary>
    /// A forward that starts from the dismissed pose waits out <see cref="Motion.Delay"/>
    /// before the tracks move. Returns false when this tick was spent in that hold.
    /// </summary>
    bool ConsumeHold(ref double deltaMs)
    {
        if (_holdMs <= 0 || _direction <= 0)
            return true;

        if (double.IsPositiveInfinity(deltaMs))
        {
            _holdMs = 0;
            return true;
        }

        var step = Math.Min(_holdMs, Math.Max(deltaMs, 0));
        _holdMs -= step;
        deltaMs -= step;
        if (_holdMs > 0 || deltaMs <= 0)
        {
            Apply(0);
            ReportProgress();
            return false;
        }

        return true;
    }

    void OnDelta(double deltaMs)
    {
        if (_disposed || !IsRunning)
            return;

        if (!double.IsPositiveInfinity(deltaMs))
        {
            deltaMs *= Motion.Rate;
            if (deltaMs == 0)
                return;
        }

        if (!ConsumeHold(ref deltaMs))
            return;

        if (_gestureSpring is { } gesture)
        {
            StepSpring(deltaMs, gesture);
            return;
        }

        if (Motion.Spring is { } spring)
        {
            StepSpring(deltaMs, spring);
            return;
        }

        if (double.IsPositiveInfinity(deltaMs))
        {
            FinishInDirection();
            return;
        }

        if (Duration == 0 && !_hasTrackSprings)
        {
            FinishInDirection();
            return;
        }

        if (_hasTrackSprings)
            StepMixed(deltaMs);
        else
            StepClock(deltaMs);
    }

    void StepClock(double deltaMs)
    {
        _elapsedMs += _direction * deltaMs;
        if (_elapsedMs >= Duration)
        {
            _elapsedMs = Duration;
            Apply(1);
            ReportProgress();
            FinishForward();
            return;
        }

        if (_elapsedMs <= 0)
        {
            _elapsedMs = 0;
            Apply(0);
            ReportProgress();
            FinishReverse(cancelPending: false);
            return;
        }

        Apply(LinearU);
        ReportProgress();
    }

    void StepMixed(double deltaMs)
    {
        _elapsedMs += _direction * deltaMs;
        if (_direction > 0 && _elapsedMs > Duration)
            _elapsedMs = Duration;
        else if (_direction < 0 && _elapsedMs < 0)
            _elapsedMs = 0;

        var u = LinearU;
        Apply(u);
        var awaitingSpring = false;
        var anyLiveSpring = false;
        foreach (var runtime in _runtimes)
        {
            if (!runtime.HasTrackSpring || !runtime.SpringLive)
                continue;

            anyLiveSpring = true;
            if (u < runtime.WindowBegin || !runtime.StepTrackSpring(deltaMs, _direction))
                awaitingSpring = true;
        }

        ReportProgress();
        var clockDone = _direction > 0 ? _elapsedMs >= Duration : _elapsedMs <= 0;
        var timedDone = _hasTimedTracks || !anyLiveSpring ? clockDone : true;
        if (timedDone && !awaitingSpring)
            FinishInDirection();
    }

    void FinishInDirection()
    {
        if (_direction > 0)
            FinishForward();
        else
            FinishReverse(cancelPending: false);
    }

    void StepSpring(double deltaMs, Spring spring)
    {
        var target = _direction > 0 ? 1d : 0d;
        SpringStep.Integrate(spring, deltaMs, target, ref _x, ref _v, ref _springElapsedMs, out var settled);
        foreach (var runtime in _runtimes)
            runtime.WriteSpringProgress(_x);
        ReportProgress();
        if (!settled)
            return;

        if (_direction > 0)
            FinishForward();
        else
            FinishReverse(cancelPending: false);
    }

    void ResetSpring(double x, bool kick = false)
    {
        var velocity = _poseKick ?? Motion.SpringVelocity;
        if (kick)
            _poseKick = null;
        _x = x;
        _v = kick && Motion.Spring is not null ? _direction * velocity : 0;
        _springElapsedMs = 0;
    }

    void KickTrackSprings()
    {
        if (Motion.Spring is not null)
            return;

        foreach (var runtime in _runtimes)
            runtime.KickTrackSpring(_direction);
    }

    void ParkTrackSprings()
    {
        foreach (var runtime in _runtimes)
            runtime.ParkTrackSpring();
    }

    void ReleaseTrackSprings()
    {
        foreach (var runtime in _runtimes)
            runtime.CompleteTrackSpring();
    }

    bool DrivingSpring
        => Motion.Spring is not null || _gestureSpring is not null;

    double PlaybackU
        => DrivingSpring ? Math.Clamp(_x, 0, 1) : LinearU;

    void FinishForward()
    {
        _clock.Stop();
        _gestureSpring = null;
        _elapsedMs = Duration;
        _x = 1;
        _v = 0;
        ReleaseTrackSprings();
        Apply(1);
        SetStatus(MotionPlaybackStatus.Completed);
        ReportProgress();
        Raise(Completed);
        if (_autoRepeat && !Reducing && TryContinueAfterForward())
            return;
        CompletePending();
    }

    void FinishReverse(bool cancelPending)
    {
        _clock.Stop();
        _gestureSpring = null;
        _elapsedMs = 0;
        _x = 0;
        _v = 0;
        ReleaseTrackSprings();
        Apply(0);
        SetStatus(MotionPlaybackStatus.Dismissed);
        ReportProgress();
        if (!cancelPending && _autoRepeat && !Reducing && TryContinueAfterReverse())
            return;
        if (cancelPending)
            CancelPending();
        else
            CompletePending();
    }

    bool TryContinueAfterForward()
    {
        if (_disposed || Duration == 0 && Motion.Spring is null && !_hasTrackSprings)
            return false;
        if (Motion.YoyoEnabled)
        {
            ContinueReverse();
            return true;
        }

        if (!ConsumeRepeat())
            return false;
        ContinueForward();
        return true;
    }

    bool TryContinueAfterReverse()
    {
        if (_disposed || (Duration == 0 && Motion.Spring is null && !_hasTrackSprings) || !Motion.YoyoEnabled)
            return false;
        if (!ConsumeRepeat())
            return false;
        ContinueForward();
        return true;
    }

    bool ConsumeRepeat()
    {
        if (_repeatRemaining < 0)
            return true;
        if (_repeatRemaining <= 1)
            return false;
        _repeatRemaining--;
        return true;
    }

    void ContinueForward()
    {
        _elapsedMs = 0;
        _holdMs = 0;
        _direction = 1;
        ResetSpring(0, kick: true);
        KickTrackSprings();
        foreach (var runtime in _runtimes)
            runtime.WriteFrom();
        SetStatus(MotionPlaybackStatus.Forward);
        _runArgs = NewArgs();
        Raise(Started);
        Apply(0);
        ReportProgress();
        StartClock();
    }

    void ContinueReverse()
    {
        _elapsedMs = Duration;
        _holdMs = 0;
        _direction = -1;
        ResetSpring(1, kick: true);
        KickTrackSprings();
        SetStatus(MotionPlaybackStatus.Reverse);
        _runArgs = NewArgs();
        Raise(Started);
        Apply(1);
        ReportProgress();
        StartClock();
    }

    void CancelToPaused()
    {
        if (!IsRunning && Status != MotionPlaybackStatus.Paused)
            return;
        _clock.Stop();
        if (IsRunning)
            _pausedWas = Status;
        SetStatus(MotionPlaybackStatus.Paused);
        CancelPending();
        Raise(Paused);
    }

    void Apply(double u)
    {
        foreach (var runtime in _runtimes)
            runtime.Apply(u);
    }

    double LinearU
        => Duration == 0
            ? (_direction > 0 ? 1 : 0)
            : Math.Clamp(_elapsedMs / Duration, 0, 1);

    void ReportProgress()
    {
        var progress = DrivingSpring
            ? PlaybackU
            : Motion.Easing.Ease(LinearU);
        Progress = progress;
        if (_runArgs is not null)
        {
            _runArgs.Status = Status;
            _runArgs.Report(progress);
        }

        ReadOnlySpan<Action<double>> listeners;
        lock (_gate)
            listeners = _at.Snapshot();
        foreach (var callback in listeners)
            MotionPlaybackEventArgs.Invoke(callback, progress);
    }

    void SetStatus(MotionPlaybackStatus status)
    {
        if (Status == status)
            return;
        Status = status;
        if (_runArgs is not null)
            _runArgs.Status = status;
        Raise(StatusChanged);
    }

    MotionPlaybackEventArgs NewArgs()
        => new(Motion, _targets, Status, Progress);

    void Raise(EventHandler<MotionPlaybackEventArgs>? handler)
    {
        var args = _runArgs ?? NewArgs();
        if (handler is null)
            return;
        foreach (var candidate in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<MotionPlaybackEventArgs>)candidate)(this, args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }

    void ArmToken(CancellationToken cancellationToken)
    {
        _tokenReg.Dispose();
        if (!cancellationToken.CanBeCanceled)
            return;
        _tokenReg = cancellationToken.Register(CancelToPaused);
    }

    void SupersedePending()
    {
        _tokenReg.Dispose();
        _pending?.TrySetResult(true);
        _pending = null;
    }

    void CancelPending()
    {
        _tokenReg.Dispose();
        _pending?.TrySetCanceled();
        _pending = null;
    }

    void CompletePending()
    {
        _tokenReg.Dispose();
        _pending?.TrySetResult(true);
        _pending = null;
    }

    void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    static TrackRuntime[] BuildRuntimes(
        Motion motion,
        VisualElement[] targets,
        uint parentSpan,
        uint playerSpan)
    {
        if (targets.Length == 0 || motion.Tracks.Count == 0)
            return [];

        WarnTargets(motion.Tracks, targets.Length);
        var list = new List<TrackRuntime>(targets.Length * motion.Tracks.Count);
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            var staggerDelay = motion.StaggerSpec is { } stagger && targets.Length > 1 && parentSpan > 0
                ? StaggerEval.DelayMs(i, targets.Length, stagger)
                : 0;
            foreach (var track in motion.Tracks)
            {
                if (track.Target is int only && only != i)
                    continue;
                var delay = track.Target is null ? staggerDelay : 0;
                var begin = playerSpan == 0 ? 0 : (delay + track.Begin * parentSpan) / playerSpan;
                var end = playerSpan == 0 ? 1 : (delay + track.End * parentSpan) / playerSpan;
                if (track.Semantic == SemanticTrack.Transform)
                {
                    if (track.Transform is null)
                        continue;
                    list.Add(new TrackRuntime(
                        target,
                        VisualElement.TranslationXProperty,
                        null,
                        0d,
                        begin,
                        end,
                        track.Easing ?? motion.Easing,
                        transform: track.Transform,
                        trackSpring: track.TrackSpring,
                        trackSpringVelocity: track.TrackSpringVelocity));
                    continue;
                }

                if (track.Semantic == SemanticTrack.Path)
                {
                    if (track.Path is null)
                        continue;
                    var sampler = PathSampler.TryCreate(track.Path);
                    if (sampler is null)
                        continue;
                    list.Add(new TrackRuntime(
                        target,
                        VisualElement.TranslationXProperty,
                        null,
                        track.To,
                        begin,
                        end,
                        track.Easing ?? motion.Easing,
                        path: sampler,
                        pathFrom: track.PathFrom,
                        pathTo: track.PathTo,
                        trackSpring: track.TrackSpring,
                        trackSpringVelocity: track.TrackSpringVelocity,
                        orient: track.Orient));
                    continue;
                }

                var property = ResolveProperty(track, target);
                if (property is null)
                    continue;
                list.Add(new TrackRuntime(
                    target,
                    property,
                    track.From,
                    track.To,
                    begin,
                    end,
                    track.Easing ?? motion.Easing,
                    track.Keyframes,
                    motion.ColorSpace,
                    perspective: motion.PerspectiveEntry,
                    trackSpring: track.TrackSpring,
                    trackSpringVelocity: track.TrackSpringVelocity));
            }
        }

        return [.. list];
    }

    static void WarnTargets(IReadOnlyList<MotionTrack> tracks, int count)
    {
        for (var i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Target is int index && (uint)index >= (uint)count)
            {
                System.Diagnostics.Debug.Assert(false, "Timeline target is outside the bound views.");
                return;
            }
        }
    }

    static BindableProperty? ResolveProperty(MotionTrack track, VisualElement target)
    {
        if (track.Property is not null)
            return track.Property;

        if (track.Semantic != SemanticTrack.CornerRadius)
            return null;

        if (target is BoxView)
            return BoxView.CornerRadiusProperty;
        if (target is Border)
            return Border.StrokeShapeProperty;

        System.Diagnostics.Debug.WriteLine($"Motion.CornerRadius skipped on {target.GetType().Name}.");
        return null;
    }
}
