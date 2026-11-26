using MauiReactor;

namespace Reactor.Motion.Internals;

/// <summary>
/// Wrapper behind <c>VisualNode.Drag</c> and <c>VisualNode.Coast</c>.
/// The pan recognizer stays on this grid.
/// The motions run on the single child, so the child can translate while the hit target stays put.
/// </summary>
internal sealed class DragElement(DragPlane plane, Spring spring, bool coast = false) : Component
{
    Microsoft.Maui.Controls.Grid? _host;
    Microsoft.Maui.Controls.PanGestureRecognizer? _pan;
    Microsoft.Maui.Controls.Grid? _panHost;
    Drag? _drag;
    double _originX;
    double _originY;
    double _lastX;
    double _lastY;
    long _lastTicks;
    double _velocityX;
    double _velocityY;

    double TravelX => plane.XAxis?.Travel ?? 1;

    double TravelY => plane.YAxis?.Travel ?? 1;

    public override VisualNode Render()
        => Grid(element => _host = element, Children())
            .BackgroundColor(Microsoft.Maui.Graphics.Colors.Transparent)
            .HFill()
            .VFill()
            .OnLoaded(Bind)
            .OnUnloaded(Unbind);

    void Bind()
    {
        Unbind();
        var target = MotionElement.ResolveTarget(_host);
        if (_host is null || target is null)
            return;

        _drag = Motion.Drag(target, spring, _ => plane);
        _pan = new Microsoft.Maui.Controls.PanGestureRecognizer();
        _pan.PanUpdated += OnPan;
        _panHost = _host;
        _panHost.GestureRecognizers.Add(_pan);
    }

    void Unbind()
    {
        Detach();
        _drag?.Dispose();
        _drag = null;
    }

    void Detach()
    {
        if (_pan is null)
            return;

        _pan.PanUpdated -= OnPan;
        if (_panHost is not null)
            _panHost.GestureRecognizers.Remove(_pan);
        _pan = null;
        _panHost = null;
    }

    void OnPan(object? sender, PanUpdatedEventArgs args)
    {
        if (_drag is null)
            return;

        switch (args.StatusType)
        {
            case GestureStatus.Started:
                BeginPan();
                break;
            case GestureStatus.Running:
                Track(args.TotalX, args.TotalY);
                break;
            case GestureStatus.Completed:
                // Completed often reports a zero total. The last Running sample is the pose.
                FinishPan(_velocityX, _velocityY);
                break;
            case GestureStatus.Canceled:
                FinishPan(0, 0);
                break;
        }
    }

    void FinishPan(double velocityX, double velocityY)
    {
        if (coast)
            _drag!.Coast(velocityX, velocityY);
        else
            _drag!.Release(velocityX, velocityY);
    }

    void BeginPan()
    {
        _originX = _drag!.X?.Progress ?? 0;
        _originY = _drag.Y?.Progress ?? 0;
        _lastX = 0;
        _lastY = 0;
        _velocityX = 0;
        _velocityY = 0;
        _lastTicks = Environment.TickCount64;
        _drag.Seek(_drag.X is null ? null : _originX, _drag.Y is null ? null : _originY);
    }

    void Track(double totalX, double totalY)
    {
        double? progressX = null;
        double? progressY = null;
        var now = Environment.TickCount64;
        var dt = now - _lastTicks;
        if (_drag!.X is not null)
        {
            progressX = _originX + (totalX / TravelX);
            if (dt > 0)
            {
                var previous = _originX + (_lastX / TravelX);
                _velocityX = (progressX.Value - previous) * 1000d / dt;
            }

            _lastX = totalX;
        }

        if (_drag.Y is not null)
        {
            progressY = _originY + (totalY / TravelY);
            if (dt > 0)
            {
                var previous = _originY + (_lastY / TravelY);
                _velocityY = (progressY.Value - previous) * 1000d / dt;
            }

            _lastY = totalY;
        }

        _lastTicks = now;
        _drag.Seek(progressX, progressY);
    }
}
