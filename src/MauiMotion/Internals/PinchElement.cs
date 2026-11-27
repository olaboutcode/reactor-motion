using MauiReactor;

namespace Reactor.Motion.Internals;

/// <summary>
/// Wrapper behind <c>VisualNode.Pinch</c>.
/// The recognizer sits on this grid. The motions run on the single child.
/// </summary>
internal sealed class PinchElement(Motion scale, Motion? rotate) : Component
{
    Microsoft.Maui.Controls.Grid? _host;
    Microsoft.Maui.Controls.PinchGestureRecognizer? _pinchGesture;
    Pinch? _pinch;
    double _ratio = 1;
    double _degrees;
    double _basis = 1;
    bool _open;
    bool _span;
    bool _turn;

    public override VisualNode Render()
        => Grid(element => _host = element, Children())
            .BackgroundColor(Colors.Transparent)
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

        _pinch = Motion.Pinch(target, scale, rotate);
        _pinchGesture = new Microsoft.Maui.Controls.PinchGestureRecognizer();
        _pinchGesture.PinchUpdated += OnPinch;
        _host.GestureRecognizers.Add(_pinchGesture);
        if (rotate is not null)
            PinchTurn.Attach(_host, OnDegrees, OnTurnEnded);
    }

    void Unbind()
    {
        if (_host is not null)
            PinchTurn.Detach(_host);
        if (_pinchGesture is not null)
        {
            _pinchGesture.PinchUpdated -= OnPinch;
            _host?.GestureRecognizers.Remove(_pinchGesture);
        }

        _pinchGesture = null;
        _pinch?.Dispose();
        _pinch = null;
        _open = false;
        _span = false;
        _turn = false;
    }

    void OnPinch(object? sender, PinchGestureUpdatedEventArgs args)
    {
        if (_pinch is null)
            return;

        switch (args.Status)
        {
            case GestureStatus.Started:
                _span = true;
                Open();
                break;
            case GestureStatus.Running:
                _span = true;
                Open();
                _ratio = PinchSpan.Apply(_ratio, args.Scale, _basis, Factor);
                _pinch.Seek(_ratio, _degrees);
                break;
            case GestureStatus.Completed:
                _span = false;
                Finish(cancel: false);
                break;
            case GestureStatus.Canceled:
                _span = false;
                Finish(cancel: true);
                break;
        }
    }

    void OnDegrees(double degrees)
    {
        if (_pinch is null)
            return;
        _turn = true;
        Open();
        _degrees = degrees;
        _pinch.Seek(_ratio, _degrees);
    }

    void OnTurnEnded(bool cancel)
    {
        _turn = false;
        Finish(cancel && !_span);
    }

    void Open()
    {
        if (_open || _pinch is null || _host is null)
            return;
        _open = true;
        _ratio = 1;
        _degrees = 0;
        _basis = _host.Scale;
        _pinch.Begin();
    }

    void Finish(bool cancel)
    {
        if (!_open || _pinch is null)
            return;
        if (cancel)
        {
            _open = false;
            _span = false;
            _turn = false;
            _pinch.Cancel();
            return;
        }

        if (_span || _turn)
            return;
        _open = false;
        _pinch.Release();
    }

#if ANDROID
    const bool Factor = true;
#else
    const bool Factor = false;
#endif
}
