using MauiReactor;

namespace Reactor.Motion;

/// <summary>Builds the stack behind <see cref="Motion.Collection{T}"/>.</summary>
internal static class FlipEntries
{
    public static VisualNode Create<T>(
        IEnumerable<T> items,
        Func<T, object> key,
        Func<T, VisualNode> item,
        Func<Motion, Motion>? move,
        Func<Motion, Motion>? enter,
        Func<Motion, Motion>? exit,
        double spacing,
        StackOrientation orientation)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(item);
        var timing = move is null ? Motion.None : move(Motion.None);
        FlipMotion.RejectTracks(timing);
        var enterMotion = enter is null ? FlipMotion.Fade(inbound: true, timing) : enter(Motion.None);
        var exitMotion = exit is null ? FlipMotion.Fade(inbound: false, timing) : exit(Motion.None);
        return new FlipList<T>(
            items.ToList(), key, item, timing, enterMotion, exitMotion, spacing, orientation);
    }
}

/// <summary>
/// Lays a keyed collection out as a stack and slides items when it changes.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>Same as <c>Motion.Collection</c>.</summary>
    public static VisualNode Collection<T>(
        this IEnumerable<T> items,
        Func<T, object> key,
        Func<T, VisualNode> item,
        Func<Motion, Motion>? move = null,
        Func<Motion, Motion>? enter = null,
        Func<Motion, Motion>? exit = null,
        double spacing = 0,
        StackOrientation orientation = StackOrientation.Vertical)
        => Motion.Collection(items, key, item, move, enter, exit, spacing, orientation);
}
