using MauiReactor;
using Reactor.Motion;
using MotionRecipe = Reactor.Motion.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Wrapper behind <c>VisualNode.InView</c>. Writes the from pose on load and
/// plays forward once the child has crossed into the nearest scroll viewport.
/// With replay, a later crossing seeks that from pose and plays forward again.
/// </summary>
internal sealed class InViewElement(MotionRecipe motion, bool replay = false) : Component
{
    VisualElement? _host;
    Microsoft.Maui.Controls.ScrollView? _scroll;
    MotionPlayer? _player;
    InViewSession _session = new();
    bool _hooked;

    public override VisualNode Render()
        => Grid(element => _host = element, Children())
            .OnLoaded(Bind)
            .OnUnloaded(Unbind);

    void Bind()
    {
        Unhook();
        _session = new InViewSession();
        _player?.Dispose();
        _player = null;

        var target = MotionElement.ResolveTarget(_host);
        if (_host is null || target is null)
            return;

        _player = MotionPlayer.Create(motion, [target]);
        _player.Seek(0);
        _scroll = InViewGate.Ancestor(_host);
        if (_scroll is null)
        {
            _player.Forward();
            return;
        }

        _scroll.Scrolled += OnScroll;
        _scroll.SizeChanged += OnLayout;
        _host.SizeChanged += OnLayout;
        _hooked = true;
        Check();
    }

    void Unbind()
    {
        Unhook();
        _player?.Dispose();
        _player = null;
        _session = new InViewSession();
    }

    void OnScroll(object? sender, ScrolledEventArgs e)
        => Check();

    void OnLayout(object? sender, EventArgs e)
        => Check();

    void Check()
    {
        if (_player is null || _host is null || _scroll?.Content is not Element content)
            return;

        var step = _session.Advance(
            InViewGate.Crosses(InViewGate.InContent(_host, content), InViewGate.Viewport(_scroll)),
            replay);
        if (step == InViewStep.None)
            return;

        if (!replay)
            Unhook();
        if (step == InViewStep.Replay)
            _player.Seek(0);
        _player.Forward();
    }

    void Unhook()
    {
        if (!_hooked)
            return;
        if (_scroll is not null)
        {
            _scroll.Scrolled -= OnScroll;
            _scroll.SizeChanged -= OnLayout;
        }

        if (_host is not null)
            _host.SizeChanged -= OnLayout;
        _hooked = false;
    }
}
