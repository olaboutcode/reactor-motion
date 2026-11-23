using System.ComponentModel;

namespace Reactor.Motion.Internals;

/// <summary>
/// Reports when a collection's layout has frames.
/// MauiReactor constructs it for <c>Collection</c>. It is not a recipe type.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CollectionLayout : StackLayout
{
    int _depth;

    public event EventHandler? Arranged;

    protected override Size ArrangeOverride(Rect bounds)
    {
        var size = base.ArrangeOverride(bounds);
        if (AnyUnmeasured())
            CrossPlatformArrange(bounds);
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

    bool AnyUnmeasured()
    {
        foreach (var child in this)
        {
            if (NeedsMeasure(child))
                return true;
        }

        return false;
    }

    static bool NeedsMeasure(IView view)
    {
        if (view is Microsoft.Maui.ILayout layout)
        {
            foreach (var child in layout)
            {
                if (NeedsMeasure(child))
                    return true;
            }

            return false;
        }

        return view is VisualElement visual && (visual.Width < 0 || visual.Height < 0);
    }
}
