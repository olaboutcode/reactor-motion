using MauiReactor;
using Reactor.Motion;
using MotionRecipe = Reactor.Motion.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Wrapper behind <c>VisualNode.InView</c>. Writes the from pose on load and
/// plays forward once the child has crossed into the nearest scroll viewport.
/// </summary>
internal sealed class InViewElement(MotionRecipe motion) : Component
{
    VisualElement? _host;
    Microsoft.Maui.Controls.ScrollView? _scroll;
    MotionPlayer? _player;
    bool _played;
    bool _hooked;

    public override VisualNode Render()
        => Grid(element => _host = element, Children())
            .OnLoaded(Bind)
            .OnUnloaded(Unbind);

    void Bind()
    {
        Unhook();
        _played = false;
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
            Play();
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
        _played = false;
    }

    void OnScroll(object? sender, ScrolledEventArgs e)
        => Check();

    void OnLayout(object? sender, EventArgs e)
        => Check();

    void Check()
    {
        if (_played || _host is null || _scroll?.Content is not Element content)
            return;
        if (!InViewGate.Crosses(InViewGate.InContent(_host, content), InViewGate.Viewport(_scroll)))
            return;
        Play();
    }

    void Play()
    {
        if (_played || _player is null)
            return;
        _played = true;
        Unhook();
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
