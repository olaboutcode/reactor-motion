using System.ComponentModel;
using MauiReactor;
using Microsoft.Maui.Controls.Shapes;
using Reactor.Motion;
using MotionRecipe = Reactor.Motion.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Grid whose arrange reports the bounds <see cref="LayoutElement"/> follows.
/// MauiReactor constructs it. It is not a recipe type.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LayoutMotionHost : Microsoft.Maui.Controls.Grid
{
    int _depth;

    public Rect ArrangedBounds { get; private set; }

    public bool HasArranged { get; private set; }

    public event EventHandler? Arranged;

    internal static readonly BindableProperty RevealProperty = BindableProperty.Create(
        nameof(Reveal),
        typeof(double),
        typeof(LayoutMotionHost),
        1d,
        propertyChanged: (bindable, _, value) =>
        {
            if (bindable is LayoutMotionHost host && value is double next)
                host.ApplyReveal(next);
        });

    double _fromWidth;
    double _fromHeight;
    double _toWidth;
    double _toHeight;
    bool _armed;
    RectangleGeometry? _clip;

    internal double Reveal
    {
        get => (double)GetValue(RevealProperty);
        set => SetValue(RevealProperty, value);
    }

    internal void ArmReveal(double fromWidth, double fromHeight, double toWidth, double toHeight)
    {
        _fromWidth = fromWidth;
        _fromHeight = fromHeight;
        _toWidth = toWidth;
        _toHeight = toHeight;
        _armed = true;
    }

    internal void ClearReveal()
    {
        _armed = false;
        _clip = null;
        Clip = null;
    }

    void ApplyReveal(double progress)
    {
        if (!_armed)
            return;
        if (!double.IsFinite(progress) || progress >= 1)
        {
            ClearReveal();
            return;
        }

        var span = Math.Clamp(progress, 0, 1);
        var rect = new Rect(
            0,
            0,
            Math.Max(0, _fromWidth + ((_toWidth - _fromWidth) * span)),
            Math.Max(0, _fromHeight + ((_toHeight - _fromHeight) * span)));
        if (_clip is null)
        {
            _clip = new RectangleGeometry { Rect = rect };
            Clip = _clip;
            return;
        }

        _clip.Rect = rect;
    }

    protected override Size ArrangeOverride(Rect bounds)
    {
        var size = base.ArrangeOverride(bounds);
        ArrangedBounds = bounds;
        HasArranged = true;
        if (_depth > 0)
            return size;

        _depth++;
        try
        {
            Arranged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _depth--;
        }

        return size;
    }
}

/// <summary>
/// Frame and player for <see cref="LayoutElement"/>. A MauiReactor merge keeps
/// this object, so a reorder still knows the previous origin.
/// </summary>
internal sealed class LayoutMotionState
{
    public bool Alive { get; set; } = true;

    public bool Applying { get; set; }

    public bool Recorded { get; set; }

    public FlipFrame Frame { get; set; }

    public LayoutMotionHost? Host { get; set; }

    public EventHandler? Arranged { get; set; }

    public MotionPlayer? Player { get; set; }
}

/// <summary>
/// Wrapper behind <c>VisualNode.Layout</c>. The first arrange records the bounds.
/// A later position change plays the recipe from that origin. A size change clips
/// this grid from the old size to the new one. The translation stays on this grid.
/// </summary>
internal sealed class LayoutElement(MotionRecipe motion) : Component<LayoutMotionState>
{
    protected override void OnWillUnmount()
    {
        State.Alive = false;
        Detach();
        State.Host?.ClearReveal();
        State.Player?.Dispose();
        State.Player = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
    {
        var host = new MauiReactor.Grid<LayoutMotionHost>(Attach);
        host.AddChildren(Children());
        return host;
    }

    void Attach(LayoutMotionHost? host)
    {
        if (host is null || ReferenceEquals(State.Host, host))
            return;

        Detach();
        State.Host = host;
        State.Arranged = OnArranged;
        host.Arranged += State.Arranged;
        Apply();
    }

    void Detach()
    {
        if (State.Host is not null && State.Arranged is not null)
            State.Host.Arranged -= State.Arranged;
        State.Arranged = null;
    }

    void OnArranged(object? sender, EventArgs args)
        => Apply();

    void Apply()
    {
        var host = State.Host;
        if (!State.Alive || State.Applying || host is null || !host.HasArranged)
            return;

        State.Applying = true;
        try
        {
            var bounds = host.ArrangedBounds;
            var next = new FlipFrame(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            var step = LayoutPlan.Next(
                State.Recorded,
                State.Frame,
                next,
                host.TranslationX,
                host.TranslationY);
            if (!step.Record)
                return;

            State.Frame = next;
            State.Recorded = true;
            if (step.Play)
                Play(host, step);
        }
        finally
        {
            State.Applying = false;
        }
    }

    void Play(LayoutMotionHost host, LayoutStep step)
    {
        var recipe = FlipMotion.Reveal(motion, step.X, step.Y, step.Reveal);
        if (recipe is null)
            return;

        if (step.Reveal)
            host.ArmReveal(step.FromWidth, step.FromHeight, step.ToWidth, step.ToHeight);
        else
            host.ClearReveal();

        State.Player?.Dispose();
        State.Player = MotionPlayer.Create(recipe, [host]);
        State.Player.Forward();
    }
}
