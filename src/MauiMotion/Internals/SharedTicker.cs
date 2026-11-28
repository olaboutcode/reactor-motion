using Microsoft.Maui.Dispatching;

namespace Reactor.Motion.Internals;

/// <summary>
/// One dispatcher timer for every live <see cref="MotionClock"/>.
/// A manual clock never joins. Tests can <see cref="Dispatch"/> a delta directly.
/// A tick reuses the clock list. A clock that joins during that tick is pumped
/// on the next one. A clock that has stopped is not pumped.
/// </summary>
internal static class SharedTicker
{
    static readonly List<MotionClock> Clocks = [];
    static MotionClock[] _snapshot = [];
    static bool _dirty = true;
    static IDispatcherTimer? _timer;
    static long _lastTicks;

    public static void Join(MotionClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (!Clocks.Contains(clock))
        {
            Clocks.Add(clock);
            _dirty = true;
        }

        EnsureTimer();
    }

    public static void Leave(MotionClock clock)
    {
        if (!Clocks.Remove(clock))
        {
            if (Clocks.Count == 0)
                _timer?.Stop();
            return;
        }

        _dirty = true;
        if (Clocks.Count == 0)
        {
            _snapshot = [];
            _dirty = false;
            _timer?.Stop();
        }
    }

    public static void Dispatch(double deltaMs)
    {
        if (Clocks.Count == 0 || deltaMs <= 0)
            return;

        var snapshot = Snapshot();
        foreach (var clock in snapshot)
            clock.Pump(deltaMs);
    }

    static ReadOnlySpan<MotionClock> Snapshot()
    {
        if (!_dirty)
            return _snapshot;

        _snapshot = Clocks.ToArray();
        _dirty = false;
        return _snapshot;
    }

    static void EnsureTimer()
    {
        if (Application.Current?.Dispatcher is not { } dispatcher)
            return;

        if (_timer is null)
        {
            _timer = dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += OnTick;
        }

        if (_timer.IsRunning)
            return;

        _lastTicks = Environment.TickCount64;
        _timer.Start();
    }

    static void OnTick(object? sender, EventArgs e)
    {
        var now = Environment.TickCount64;
        var delta = now - _lastTicks;
        _lastTicks = now;
        if (delta > 0)
            Dispatch(delta);
    }
}
