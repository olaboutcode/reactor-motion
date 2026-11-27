using MauiReactor;
using Reactor.Motion;
using MotionRecipe = Reactor.Motion.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Wrapper behind <c>VisualNode.Scrub</c>. Seeks the recipe from the nearest
/// <see cref="Microsoft.Maui.Controls.ScrollView"/> and does not start the clock.
/// </summary>
internal sealed class ScrubElement(MotionRecipe motion, double? from, double? to) : Component
{
    VisualElement? _host;
    Microsoft.Maui.Controls.ScrollView? _scroll;
    MotionPlayer? _player;
    bool _hooked;

    public override VisualNode Render()
        => Grid(element => _host = element, Children())
            .OnLoaded(Bind)
            .OnUnloaded(Unbind);

    protected override void OnWillUnmount()
    {
        Unbind();
        base.OnWillUnmount();
    }

    void Bind()
    {
        Unhook();
        _player?.Dispose();
        _player = null;

        var target = MotionElement.ResolveTarget(_host);
        if (_host is null || target is null)
            return;

        _player = MotionPlayer.Create(motion, [target]);
        _player.SeekFraction(0);
        _scroll = InViewGate.Ancestor(_host);
        if (_scroll is null)
        {
            if (Reduced)
                _player.SeekFraction(1);
            return;
        }

        _scroll.Scrolled += OnScroll;
        _scroll.SizeChanged += OnLayout;
        _host.SizeChanged += OnLayout;
        _hooked = true;
        SafeCheck();
    }

    void Unbind()
    {
        Unhook();
        _player?.Dispose();
        _player = null;
    }

    void OnScroll(object? sender, ScrolledEventArgs e)
        => SafeCheck();

    void OnLayout(object? sender, EventArgs e)
        => SafeCheck();

    void SafeCheck()
    {
        try
        {
            Check();
        }
        catch (ObjectDisposedException)
        {
            Unbind();
        }
    }

    void Check()
    {
        if (_player is null || _scroll is null)
            return;
        if (Reduced)
        {
            _player.SeekFraction(1);
            Unhook();
            return;
        }

        if (from is double start && to is double end)
        {
            _player.SeekFraction(ScrubMap.Range(Offset(), start, end));
            return;
        }

        if (_host is null || _scroll.Content is not Element content)
            return;

        var viewport = InViewGate.Viewport(_scroll);
        var progress = ScrubMap.Crossing(
            InViewGate.InContent(_host, content),
            viewport,
            ScrubMap.TracksVertical(ContentWidth(), ContentHeight(), viewport.Width, viewport.Height));
        if (progress is double t)
            _player.SeekFraction(t);
    }

    double Offset()
    {
        var viewport = InViewGate.Viewport(_scroll!);
        var vertical = ScrubMap.TracksVertical(ContentWidth(), ContentHeight(), viewport.Width, viewport.Height);
        return vertical ? _scroll!.ScrollY : _scroll!.ScrollX;
    }

    double ContentWidth()
        => _scroll?.Content is VisualElement content ? content.Width : 0;

    double ContentHeight()
        => _scroll?.Content is VisualElement content ? content.Height : 0;

    bool Reduced
        => motion.ReduceMotion ?? MotionPreferences.IsReduceMotionEnabled();

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
