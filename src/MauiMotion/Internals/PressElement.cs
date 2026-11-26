using MauiReactor;
using Reactor.Motion;
using MotionRecipe = Reactor.Motion.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Wrapper behind <c>VisualNode.Press</c>. The pointer recognizer stays on this grid.
/// The motions run on the single child.
/// </summary>
internal sealed class PressElement(MotionRecipe pressed, MotionRecipe? hover) : Component
{
    Microsoft.Maui.Controls.Grid? _host;
    VisualElement? _target;
    Microsoft.Maui.Controls.PointerGestureRecognizer? _pointer;
    Microsoft.Maui.Controls.Grid? _pointerHost;
    MotionPlayer? _pressPlayer;
    MotionPlayer? _hoverPlayer;
    MotionPlayer? _restPlayer;
    PressState _state;

    public override VisualNode Render()
        => Grid(element => _host = element, Children())
            .BackgroundColor(Microsoft.Maui.Graphics.Colors.Transparent)
            .OnLoaded(Bind)
            .OnUnloaded(Unbind);

    void Bind()
    {
        Unbind();
        _target = MotionElement.ResolveTarget(_host);
        if (_host is null || _target is null)
            return;

        _pointer = new Microsoft.Maui.Controls.PointerGestureRecognizer();
        _pointer.PointerPressed += OnPressed;
        _pointer.PointerReleased += OnReleased;
        _pointer.PointerEntered += OnEntered;
        _pointer.PointerExited += OnExited;
        _pointerHost = _host;
        _pointerHost.GestureRecognizers.Add(_pointer);
    }

    void Unbind()
    {
        Detach();
        _pressPlayer?.Dispose();
        _hoverPlayer?.Dispose();
        _restPlayer?.Dispose();
        _pressPlayer = null;
        _hoverPlayer = null;
        _restPlayer = null;
        _target = null;
        _state = default;
    }

    void Detach()
    {
        if (_pointer is null)
            return;

        _pointer.PointerPressed -= OnPressed;
        _pointer.PointerReleased -= OnReleased;
        _pointer.PointerEntered -= OnEntered;
        _pointer.PointerExited -= OnExited;
        if (_pointerHost is not null)
            _pointerHost.GestureRecognizers.Remove(_pointer);
        _pointer = null;
        _pointerHost = null;
    }

    void OnPressed(object? sender, Microsoft.Maui.Controls.PointerEventArgs e)
        => Apply(PressPlan.Pressed(_state, hover is not null));

    void OnReleased(object? sender, Microsoft.Maui.Controls.PointerEventArgs e)
        => Apply(PressPlan.Released(_state, hover is not null));

    void OnEntered(object? sender, Microsoft.Maui.Controls.PointerEventArgs e)
        => Apply(PressPlan.Entered(_state, hover is not null));

    void OnExited(object? sender, Microsoft.Maui.Controls.PointerEventArgs e)
        => Apply(PressPlan.Exited(_state, hover is not null));

    void Apply(PressStep step)
    {
        _state = step.State;
        if (_target is null)
            return;

        switch (PressPlan.Resolve(step.Cue, _hoverPlayer is { Disposed: false }))
        {
            case PressCue.ForwardPress:
                Forward(ref _pressPlayer, pressed);
                break;
            case PressCue.ReversePress:
                _pressPlayer.Reverse();
                break;
            case PressCue.ForwardHover:
                Forward(ref _hoverPlayer, hover!);
                break;
            case PressCue.ReverseHover:
                _hoverPlayer.Reverse();
                break;
            case PressCue.Rest:
                Rest();
                break;
        }
    }

    void Forward(ref MotionPlayer? player, MotionRecipe recipe)
    {
        if (player is { Disposed: false })
        {
            player.Forward();
            return;
        }

        player = MotionPlayer.Create(recipe.WithPose(), [_target!]);
        player.Forward();
    }

    void Rest()
    {
        if (_target is null)
            return;

        var player = MotionPlayer.Create((hover ?? pressed).RestPose(), [_target]);
        _restPlayer = player;
        player.Forward();
    }
}
