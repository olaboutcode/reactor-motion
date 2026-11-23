using MauiReactor;

namespace Reactor.Motion;

/// <summary>Builds the host behind <see cref="Motion.Collection{T}"/>.</summary>
internal static class FlipEntries
{
    public static VisualNode Create<T>(
        IEnumerable<T> items,
        Func<T, object> itemKey,
        Func<T, VisualNode> renderItem,
        Func<IEnumerable<VisualNode>, VisualNode> placeItems,
        Func<Motion, Motion>? moveMotion,
        Func<Motion, Motion>? enterMotion,
        Func<Motion, Motion>? exitMotion)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(itemKey);
        ArgumentNullException.ThrowIfNull(renderItem);
        ArgumentNullException.ThrowIfNull(placeItems);
        var motion = moveMotion is null ? Motion.None.FromPlace() : moveMotion(Motion.None);
        var enter = enterMotion is null ? FlipMotion.Fade(inbound: true, motion) : enterMotion(Motion.None);
        var exit = exitMotion is null ? FlipMotion.Fade(inbound: false, motion) : exitMotion(Motion.None);
        return new FlipList<T>([.. items], itemKey, renderItem, placeItems, motion, enter, exit);
    }
}

/// <summary>
/// Keeps a keyed collection mounted and plays enter, exit, and move.
/// The caller places the item nodes.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>Same as <c>Motion.Collection</c>.</summary>
    public static VisualNode Collection<T>(
        this IEnumerable<T> items,
        Func<T, object> itemKey,
        Func<T, VisualNode> renderItem,
        Func<IEnumerable<VisualNode>, VisualNode> placeItems,
        Func<Motion, Motion>? moveMotion = null,
        Func<Motion, Motion>? enterMotion = null,
        Func<Motion, Motion>? exitMotion = null)
        => Motion.Collection(items, itemKey, renderItem, placeItems, moveMotion, enterMotion, exitMotion);
}
