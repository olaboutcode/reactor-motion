namespace Reactor.Motion.Internals;

/// <summary>What <see cref="InViewSession.Advance"/> asks the player to do.</summary>
internal enum InViewStep
{
    /// <summary>Stay. A leave keeps the pose where the last play left it.</summary>
    None,

    /// <summary>First crossing. Play forward from the pose written on load.</summary>
    Play,

    /// <summary>A later crossing. Seek the from pose, then play forward.</summary>
    Replay,
}

/// <summary>
/// Tracks one <c>InView</c> child against the halfway mark.
/// Replay arms only after a play, and only once the overlap drops back below the mark.
/// </summary>
internal sealed class InViewSession
{
    public bool Inside { get; private set; }

    public bool Played { get; private set; }

    public bool Armed { get; private set; }

    public InViewStep Advance(bool crosses, bool replay)
    {
        if (!crosses)
        {
            if (replay && Inside)
                Armed = true;
            Inside = false;
            return InViewStep.None;
        }

        if (Inside)
            return InViewStep.None;

        Inside = true;
        if (Played && !(replay && Armed))
            return InViewStep.None;

        var again = Played;
        Played = true;
        Armed = false;
        return again ? InViewStep.Replay : InViewStep.Play;
    }
}
