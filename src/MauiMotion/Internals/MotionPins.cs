using Reactor.Motion;

namespace Reactor.Motion.Internals;

/// <summary>
/// Views whose property writes <see cref="TrackRuntime"/> must skip.
/// Reactor.Motion does not pin anything itself. A later caller pins the views
/// another system is driving, then unpins them when that system is done.
/// <see cref="IsPinned"/> reads the count first, so a frame with no pins
/// takes no lock. The count is published only after the set changes. A pin
/// that skips the count lets a motion write on a pinned view.
/// </summary>
internal static class MotionPins
{
    static readonly HashSet<VisualElement> Pins = [];
    static readonly Lock Gate = new();
    static int _count;

    public static void Pin(IReadOnlyList<VisualElement> views)
    {
        lock (Gate)
        {
            Pins.Clear();
            foreach (var view in views)
                Pins.Add(view);
            Volatile.Write(ref _count, Pins.Count);
        }
    }

    public static void Unpin()
    {
        lock (Gate)
        {
            Pins.Clear();
            Volatile.Write(ref _count, 0);
        }
    }

    public static bool IsPinned(VisualElement view)
    {
        if (Volatile.Read(ref _count) == 0)
            return false;

        lock (Gate)
            return Pins.Contains(view);
    }
}
