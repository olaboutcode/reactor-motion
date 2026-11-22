namespace Reactor.Motion.Internals;

/// <summary>What <see cref="PresenceElement"/> should play for a presence change.</summary>
internal enum PresenceCue
{
    /// <summary>Leave the current player alone.</summary>
    None = 0,

    /// <summary>Mount the child and play the enter recipe.</summary>
    Enter,

    /// <summary>Keep the child and play the exit recipe forward.</summary>
    Exit,

    /// <summary>Keep the child and reverse the exit back to its from pose.</summary>
    ReverseExit,
}

/// <summary>Mounted, exiting, and the cue for one presence transition.</summary>
internal readonly record struct PresenceStep(bool Mounted, bool Exiting, PresenceCue Cue);

/// <summary>
/// Decides when a presence child is in the tree. The component plays the cue.
/// A forward exit that finishes while still absent unmounts the child. Reversing
/// that exit keeps the child and returns it to the exit's from pose.
/// </summary>
internal static class PresencePlan
{
    /// <summary>First mount of the wrapper. The child is shown only when present.</summary>
    public static PresenceStep Mount(bool present)
        => present
            ? new(Mounted: true, Exiting: false, PresenceCue.Enter)
            : new(Mounted: false, Exiting: false, PresenceCue.None);

    /// <summary>
    /// <paramref name="present"/> changed while the wrapper stayed mounted.
    /// Leaving always keeps the child. Returning during an exit reverses it.
    /// </summary>
    public static PresenceStep PresentChanged(bool mounted, bool exiting, bool present)
    {
        if (!mounted)
        {
            return present
                ? new(Mounted: true, Exiting: false, PresenceCue.Enter)
                : new(Mounted: false, Exiting: false, PresenceCue.None);
        }

        if (!present)
            return new(Mounted: true, Exiting: true, PresenceCue.Exit);

        return exiting
            ? new(Mounted: true, Exiting: true, PresenceCue.ReverseExit)
            : new(Mounted: true, Exiting: false, PresenceCue.None);
    }

    /// <summary>
    /// The run finished and was not superseded.
    /// <paramref name="reversed"/> is true when that run was a reverse.
    /// </summary>
    public static PresenceStep RunFinished(bool mounted, bool exiting, bool present, bool reversed)
    {
        if (!mounted || !exiting)
            return new(mounted, Exiting: false, PresenceCue.None);

        if (reversed)
            return new(Mounted: true, Exiting: false, PresenceCue.None);

        if (present)
            return new(Mounted: true, Exiting: false, PresenceCue.Enter);

        return new(Mounted: false, Exiting: false, PresenceCue.None);
    }
}
