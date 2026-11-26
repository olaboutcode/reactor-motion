namespace Reactor.Motion.Internals;

/// <summary>What <see cref="PressElement"/> should do for one pointer event.</summary>
internal enum PressCue
{
    /// <summary>Leave the players alone.</summary>
    None = 0,

    /// <summary>Play the press recipe forward.</summary>
    ForwardPress,

    /// <summary>Reverse the press recipe.</summary>
    ReversePress,

    /// <summary>Play the hover recipe forward.</summary>
    ForwardHover,

    /// <summary>Reverse the hover recipe.</summary>
    ReverseHover,

    /// <summary>Play toward the authored from-value. Used when the hover player is gone.</summary>
    Rest,
}

/// <summary>Pointer flags <see cref="PressPlan"/> reads. <c>SawHover</c> means a hover enter has happened.</summary>
internal readonly record struct PressState(bool Down, bool Inside, bool SawHover);

/// <summary>The next pointer flags and the cue for one event.</summary>
internal readonly record struct PressStep(PressState State, PressCue Cue);

/// <summary>
/// Decides press and hover playback. Press wins while the pointer is down.
/// Hover plays only after a pointer enter, so a platform that never enters stays at rest until a press.
/// </summary>
internal static class PressPlan
{
    /// <summary>The pointer went down. Press plays forward. Hover that is already showing stays marked.</summary>
    public static PressStep Pressed(PressState state, bool hasHover)
    {
        var saw = state.SawHover || (hasHover && state.Inside);
        return new PressStep(state with { Down = true, SawHover = saw }, PressCue.ForwardPress);
    }

    /// <summary>
    /// The pointer came up. Inside, or with no hover history, the press reverses.
    /// A release that is already outside a hover returns to the authored rest.
    /// </summary>
    public static PressStep Released(PressState state, bool hasHover)
    {
        if (!state.Down)
            return new PressStep(state, PressCue.None);

        var next = state with { Down = false };
        if (!state.Inside && state.SawHover && hasHover)
            return new PressStep(next, PressCue.Rest);
        return new PressStep(next, PressCue.ReversePress);
    }

    /// <summary>The pointer entered. Hover plays when the pointer is up and a hover recipe exists.</summary>
    public static PressStep Entered(PressState state, bool hasHover)
    {
        var next = state with { Inside = true, SawHover = state.SawHover || hasHover };
        if (next.Down || !hasHover)
            return new PressStep(next, PressCue.None);
        return new PressStep(next, PressCue.ForwardHover);
    }

    /// <summary>
    /// The pointer left. A press in progress reverses, or returns to rest when hover had shown.
    /// A plain hover reverses.
    /// </summary>
    public static PressStep Exited(PressState state, bool hasHover)
    {
        var next = state with { Inside = false, Down = false };
        if (state.Down)
        {
            var cue = state.SawHover && hasHover ? PressCue.Rest : PressCue.ReversePress;
            return new PressStep(next, cue);
        }

        if (state.SawHover && hasHover)
            return new PressStep(next, PressCue.ReverseHover);
        return new PressStep(next, PressCue.None);
    }

    /// <summary>
    /// Hover reverse needs the hover player. Press takes its properties, so that player may already be gone.
    /// </summary>
    public static PressCue Resolve(PressCue cue, bool hoverAlive)
        => cue == PressCue.ReverseHover && !hoverAlive ? PressCue.Rest : cue;
}
