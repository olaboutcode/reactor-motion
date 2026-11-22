using MauiReactor;
using Reactor.Motion;
using MotionRecipe = Reactor.Motion.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// State for <see cref="PresenceElement"/>. The player lives here because a
/// MauiReactor merge keeps this object and drops the previous component instance.
/// </summary>
internal sealed class PresenceState
{
    public bool Present { get; set; }

    public bool Mounted { get; set; }

    public bool Exiting { get; set; }

    public bool Alive { get; set; } = true;

    public PresenceCue Cue { get; set; }

    public PresenceCue Armed { get; set; }

    public int Generation { get; set; }

    public MotionPlayer? Player { get; set; }

    public VisualElement? Host { get; set; }

    public VisualElement? Target { get; set; }
}

/// <summary>
/// Plays enter when the child appears and exit when <c>present</c> becomes false.
/// The child stays mounted until that exit run finishes. Removing this component
/// unmounts immediately.
/// </summary>
internal sealed class PresenceElement(bool present, MotionRecipe enter, MotionRecipe exit)
    : Component<PresenceState>
{
    protected override void OnMounted()
    {
        State.Present = present;
        Apply(PresencePlan.Mount(present), invalidate: false);
    }

    protected override void OnPropsChanged()
    {
        if (State.Present == present)
            return;

        State.Present = present;
        Apply(PresencePlan.PresentChanged(State.Mounted, State.Exiting, present), invalidate: true);
    }

    protected override void OnWillUnmount()
    {
        State.Alive = false;
        DropPlayer();
        State.Target = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
    {
        // Always return a Grid so setters such as GridRow have a node to land on.
        // The inner grid comes and goes, so Loaded fires on each enter.
        if (!State.Mounted)
            return Grid();

        return Grid(
            Grid(element => State.Host = element, Children())
                .OnLoaded(OnLoaded)
                .OnUnloaded(OnUnloaded));
    }

    void OnLoaded()
    {
        State.Target = MotionElement.ResolveTarget(State.Host);
        if (State.Target is null || State.Player is not null || State.Cue == PresenceCue.None)
            return;

        Play(State.Cue);
    }

    void OnUnloaded()
    {
        DropPlayer();
        State.Target = null;
    }

    void Apply(PresenceStep step, bool invalidate)
    {
        var mountChanged = State.Mounted != step.Mounted;
        State.Mounted = step.Mounted;
        State.Exiting = step.Exiting;
        State.Cue = step.Cue;
        if (step.Cue != PresenceCue.None)
            Play(step.Cue);

        if (invalidate && mountChanged)
            Invalidate();
    }

    void Play(PresenceCue cue)
    {
        var target = State.Target;
        State.Cue = cue;
        if (!State.Alive || target is null)
            return;

        if (cue == PresenceCue.ReverseExit)
        {
            if (HasExitPlayer)
            {
                Start(reverse: true);
                return;
            }

            cue = PresenceCue.Enter;
            State.Cue = cue;
        }

        if (cue == PresenceCue.Exit && HasExitPlayer)
        {
            Start(reverse: false);
            return;
        }

        var motion = cue == PresenceCue.Enter ? enter : exit;
        if (cue == PresenceCue.Exit)
        {
            System.Diagnostics.Debug.Assert(
                motion.RepeatCount >= 0,
                "EnterExit exit must finish. An infinite Repeat never unmounts the child.");
        }

        DropPlayer();
        State.Player = MotionPlayer.Create(motion, [target]);
        Start(reverse: false);
    }

    bool HasExitPlayer
        => State.Player is not null && State.Armed is PresenceCue.Exit or PresenceCue.ReverseExit;

    void Start(bool reverse)
    {
        var player = State.Player;
        if (player is null)
            return;

        State.Armed = reverse ? PresenceCue.ReverseExit : State.Cue;
        var generation = ++State.Generation;
        // Reverse completes the in-flight forward task, so the old watch must lose.
        var task = reverse ? player.ReverseAsync() : player.ForwardAsync();
        _ = Watch(task, generation, reverse);
    }

    async Task Watch(Task task, int generation, bool reverse)
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

        if (!State.Alive || generation != State.Generation)
            return;

        var step = PresencePlan.RunFinished(State.Mounted, State.Exiting, State.Present, reverse);
        var mountChanged = State.Mounted != step.Mounted;
        State.Mounted = step.Mounted;
        State.Exiting = step.Exiting;
        State.Cue = step.Cue;
        State.Armed = PresenceCue.None;
        if (step.Cue == PresenceCue.Enter)
        {
            Play(PresenceCue.Enter);
            return;
        }

        if (!mountChanged)
            return;

        DropPlayer();
        Invalidate();
    }

    void DropPlayer()
    {
        State.Generation++;
        State.Player?.Dispose();
        State.Player = null;
        State.Armed = PresenceCue.None;
    }
}
