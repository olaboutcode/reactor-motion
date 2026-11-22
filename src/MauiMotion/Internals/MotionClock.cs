using Reactor.Motion;
using Microsoft.Maui.Animations;

namespace Reactor.Motion.Internals;

/// <summary>
/// Frame pump for <see cref="MotionPlayer"/>.
/// Each tick reports delta milliseconds. A manual clock does not subscribe to the
/// platform animation manager; tests advance it with <c>Tick</c>.
/// Live clocks share <see cref="SharedTicker"/>. <see cref="IsPumping"/>
/// is true for a manual clock, or while that shared timer is running.
/// </summary>
internal sealed class MotionClock
{
    readonly bool _manual;
    Action<double>? _onDelta;
    IAnimationManager? _manager;
    bool _running;
    bool _pumping;
    bool _loggedEnergySaver;

    public MotionClock(bool manual = false)
    {
        _manual = manual;
    }

    public void Connect(Action<double> onDelta)
        => _onDelta = onDelta ?? throw new ArgumentNullException(nameof(onDelta));

    public bool IsRunning => _running;

    public bool IsPumping => _manual || _pumping;

    public void Start(IEnumerable<VisualElement> targets)
    {
        if (_running)
            return;
        _running = true;
        _pumping = false;
        if (_manual)
            return;

        foreach (var target in targets)
        {
            _manager = target.Handler?.MauiContext?.Services.GetService(typeof(IAnimationManager)) as IAnimationManager;
            if (_manager is not null)
                break;
        }

        if (_manager is { SpeedModifier: 0 })
        {
            _onDelta?.Invoke(double.PositiveInfinity);
            return;
        }

        if (_manager?.Ticker is { SystemEnabled: false })
        {
            if (!_loggedEnergySaver)
            {
                _loggedEnergySaver = true;
                System.Diagnostics.Debug.WriteLine("MotionClock: animation ticker disabled; freezing.");
            }

            return;
        }

        if (Application.Current?.Dispatcher is null)
            return;

        JoinShared();
    }

    /// <summary>Joins the shared ticker. Tests use this without a dispatcher.</summary>
    internal void JoinShared()
    {
        _running = true;
        _pumping = true;
        SharedTicker.Join(this);
    }

    public void Stop()
    {
        _running = false;
        _pumping = false;
        _manager = null;
        if (!_manual)
            SharedTicker.Leave(this);
    }

    internal void Pump(double deltaMs)
    {
        if (!_running)
            return;
        if (_manager?.Ticker is { SystemEnabled: false })
            return;

        deltaMs *= _manager?.SpeedModifier ?? 1;
        if (deltaMs > 0)
            _onDelta?.Invoke(deltaMs);
    }

    public void Tick(double deltaMilliseconds)
    {
        if (!_running)
            return;
        _onDelta?.Invoke(deltaMilliseconds);
    }
}
