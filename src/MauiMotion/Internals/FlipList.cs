using MauiReactor;
using Reactor.Motion;

namespace Reactor.Motion.Internals;

/// <summary>One row the list is showing or letting leave.</summary>
internal sealed class FlipHeld
{
    public required object Key { get; init; }

    public required object? Item { get; init; }

    public FlipFrame Frame { get; set; }

    public bool Playing { get; set; }

    public int Generation { get; set; }
}

/// <summary>
/// State for <see cref="FlipList{T}"/>. Players and frames live here because a
/// MauiReactor merge keeps this object and drops the previous component instance.
/// </summary>
internal sealed class FlipListState
{
    public bool Placed { get; set; }

    public bool Alive { get; set; } = true;

    public bool Applying { get; set; }

    public List<FlipHeld> Live { get; set; } = [];

    public List<FlipHeld> Exits { get; } = [];

    public Dictionary<object, FlipFrame> Frames { get; } = new(EqualityComparer<object>.Default);

    public Dictionary<object, VisualElement> Hosts { get; } = new(EqualityComparer<object>.Default);

    public Dictionary<object, MotionPlayer> Players { get; } = new(EqualityComparer<object>.Default);

    public CollectionLayout? Stack { get; set; }

    public EventHandler? Arranged { get; set; }
}

/// <summary>Key for a row that is leaving, so it does not reuse the live row.</summary>
internal sealed record FlipExitKey(object Value);

/// <summary>
/// Keyed items. The caller places the live nodes. Items that stay and change
/// place play move. Items that appear play enter. Items that leave play exit
/// at the last frame, out of the flow. The first layout only records frames.
/// </summary>
internal sealed class FlipList<T>(
    IReadOnlyList<T> items,
    Func<T, object> keyOf,
    Func<T, VisualNode> render,
    Func<IEnumerable<VisualNode>, VisualNode> layout,
    Motion move,
    Motion enter,
    Motion exit) : Component<FlipListState>
{
    protected override void OnMounted()
        => Sync();

    protected override void OnPropsChanged()
    {
        Sync();
        Attach(State.Stack);
    }

    protected override void OnWillUnmount()
    {
        State.Alive = false;
        Detach();
        foreach (var player in State.Players.Values)
            player.Dispose();
        State.Players.Clear();
        State.Hosts.Clear();
        base.OnWillUnmount();
    }

    public override VisualNode Render()
    {
        var host = new MauiReactor.StackLayout<CollectionLayout>(Attach);
        host.Spacing(0);
        host.AddChildren(layout(Rows(State.Live, exiting: false)));
        var exits = new MauiReactor.Grid(Rows(State.Exits, exiting: true))
            .InputTransparent(inputTransparent: true)
            .CascadeInputTransparent(cascadeInputTransparent: true);
        return new MauiReactor.Grid([host, exits])
            .HFill()
            .VStart();
    }

    void Sync()
    {
        var next = Collect();
        if (!State.Placed)
        {
            State.Live = next;
            return;
        }

        var previousKeys = Keys(State.Live);
        var nextKeys = Keys(next);
        HoldRemovals(previousKeys, nextKeys);
        RestoreReturns(next);
        State.Live = next;
    }

    void HoldRemovals(List<object> previousKeys, List<object> nextKeys)
    {
        foreach (var key in FlipPlan.Removed(previousKeys, nextKeys))
        {
            var held = Find(State.Live, key);
            if (held is null)
                continue;

            held.Frame = State.Frames.TryGetValue(key, out var frame) ? frame : default;
            held.Playing = false;
            held.Generation++;
            State.Exits.RemoveAll(row => FlipPlan.Equal(row.Key, key));
            State.Exits.Add(held);
            State.Frames.Remove(key);
        }
    }

    void RestoreReturns(List<FlipHeld> next)
    {
        foreach (var held in next)
        {
            var leaving = Find(State.Exits, held.Key);
            if (leaving is null)
                continue;

            State.Frames[held.Key] = leaving.Frame;
            State.Exits.Remove(leaving);
            DropPlayer(held.Key);
        }
    }

    List<FlipHeld> Collect()
    {
        var next = new List<FlipHeld>(items.Count);
        var seen = new HashSet<object>(EqualityComparer<object>.Default);
        foreach (var value in items)
        {
            if (keyOf(value) is not object key)
                throw new InvalidOperationException("Collection key must not be null.");
            if (!seen.Add(key))
            {
                System.Diagnostics.Debug.Assert(false, "Collection keeps the first item when two keys are equal.");
                continue;
            }

            next.Add(new FlipHeld { Key = key, Item = value });
        }

        return next;
    }

    VisualNode[] Rows(List<FlipHeld> rows, bool exiting)
    {
        var nodes = new VisualNode[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            nodes[i] = exiting ? Ghost(rows[i]) : LiveRow(rows[i]);
        return nodes;
    }

    FlipRow LiveRow(FlipHeld held)
        => new FlipRow(held.Key, exiting: false, Child(held), Mount, Unmount)
            .WithKey(held.Key);

    MauiReactor.Grid Ghost(FlipHeld held)
        => Grid(new FlipRow(held.Key, exiting: true, Child(held), Mount, Unmount)
                .WithKey(new FlipExitKey(held.Key)))
            .Margin(held.Frame.X, held.Frame.Y, 0, 0)
            .WidthRequest(Math.Max(0, held.Frame.Width))
            .HeightRequest(Math.Max(0, held.Frame.Height))
            .HStart()
            .VStart()
            .InputTransparent(true)
            .CascadeInputTransparent(true)
            .ZIndex(1);

    VisualNode Child(FlipHeld held)
    {
        var child = render((T)held.Item!);
        if (child is null)
            throw new InvalidOperationException("Collection item must return a node.");
        return child;
    }

    void Mount(object key, VisualElement host, bool exiting)
    {
        if (exiting)
        {
            BeginExit(key, host);
            return;
        }

        State.Hosts[key] = host;
        TryApply();
    }

    void Unmount(object key, bool exiting)
    {
        if (!exiting)
            State.Hosts.Remove(key);
    }

    void Attach(CollectionLayout? stack)
    {
        if (stack is null)
            return;

        Detach();
        State.Stack = stack;
        State.Arranged = OnArranged;
        stack.Arranged += State.Arranged;
    }

    void Detach()
    {
        if (State.Stack is not null && State.Arranged is not null)
            State.Stack.Arranged -= State.Arranged;
        State.Arranged = null;
    }

    void OnArranged(object? sender, EventArgs args)
        => TryApply();

    void TryApply()
    {
        if (State.Applying || !State.Alive || !Ready())
            return;

        State.Applying = true;
        try
        {
            Apply();
        }
        finally
        {
            State.Applying = false;
        }
    }

    bool Ready()
    {
        foreach (var held in State.Live)
        {
            if (!State.Hosts.TryGetValue(held.Key, out var host))
                return false;
            if (host.Width < 0 || host.Height < 0)
                return false;
        }

        return true;
    }

    void Apply()
    {
        var placed = State.Placed;
        foreach (var held in State.Live)
        {
            if (!Place(held, placed))
                return;
        }

        State.Placed = true;
    }

    bool Place(FlipHeld held, bool placed)
    {
        var host = State.Hosts[held.Key];
        var frame = new FlipFrame(host.X, host.Y, host.Width, host.Height);
        if (!FlipPlan.Finite(frame))
            return false;

        var known = State.Frames.TryGetValue(held.Key, out var previous);
        var cue = FlipPlan.Cue(placed, known, known && FlipPlan.SamePlace(previous, frame));
        State.Frames[held.Key] = frame;
        if (cue == FlipCue.Enter)
            Play(held.Key, Target(host), enter);
        else if (cue == FlipCue.Move)
            Slide(held.Key, host, previous, frame);

        return true;
    }

    void Slide(object key, VisualElement host, FlipFrame previous, FlipFrame frame)
    {
        var (x, y) = FlipPlan.Invert(previous, host.TranslationX, host.TranslationY, frame);
        var motion = FlipMotion.Move(move, x, y);
        if (motion is not null)
            Play(key, host, motion);
    }

    void BeginExit(object key, VisualElement host)
    {
        var held = Find(State.Exits, key);
        if (held is null || held.Playing)
            return;

        System.Diagnostics.Debug.Assert(
            exit.RepeatCount >= 0,
            "Collection exit must finish. An infinite Repeat never removes the item.");
        held.Playing = true;
        var generation = held.Generation;
        var player = Arm(key, Target(host), exit);
        _ = WatchExit(key, generation, player.ForwardAsync());
    }

    async Task WatchExit(object key, int generation, Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        if (!State.Alive)
            return;

        var held = Find(State.Exits, key);
        if (held is null || held.Generation != generation)
            return;

        State.Exits.Remove(held);
        DropPlayer(key);
        Invalidate();
    }

    MotionPlayer Arm(object key, VisualElement target, Motion motion)
    {
        var player = MotionPlayer.Create(motion, [target]);
        DropPlayer(key);
        State.Players[key] = player;
        return player;
    }

    void Play(object key, VisualElement target, Motion motion)
        => Arm(key, target, motion).Forward();

    void DropPlayer(object key)
    {
        if (State.Players.Remove(key, out var player))
            player.Dispose();
    }

    static VisualElement Target(VisualElement host)
        => MotionElement.ResolveTarget(host) ?? host;

    static List<object> Keys(List<FlipHeld> rows)
    {
        var keys = new List<object>(rows.Count);
        foreach (var row in rows)
            keys.Add(row.Key);
        return keys;
    }

    static FlipHeld? Find(List<FlipHeld> rows, object key)
    {
        foreach (var row in rows)
        {
            if (FlipPlan.Equal(row.Key, key))
                return row;
        }

        return null;
    }
}

/// <summary>Host grid for one list row. The move translates this grid.</summary>
internal sealed class FlipRow : Component<FlipRowState>
{
    readonly object _key;
    readonly bool _exiting;
    readonly VisualNode _child;
    readonly Action<object, VisualElement, bool> _mount;
    readonly Action<object, bool> _unmount;

    public FlipRow(
        object key,
        bool exiting,
        VisualNode child,
        Action<object, VisualElement, bool> mount,
        Action<object, bool> unmount)
    {
        _key = key;
        _exiting = exiting;
        _child = child;
        _mount = mount;
        _unmount = unmount;
    }

    protected override void OnPropsChanged()
    {
        if (State.Host is not null)
            _mount(_key, State.Host, _exiting);
    }

    protected override void OnWillUnmount()
    {
        _unmount(_key, _exiting);
        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => Grid(
            element =>
            {
                if (element is null)
                    return;
                State.Host = element;
                _mount(_key, element, _exiting);
            },
            _child);
}

internal sealed class FlipRowState
{
    public VisualElement? Host { get; set; }
}
