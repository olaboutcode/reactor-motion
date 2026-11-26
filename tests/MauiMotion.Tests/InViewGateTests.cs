using Reactor.Motion.Internals;
using Xunit;

namespace MauiMotion.Tests;

public class InViewGateTests
{
    [Fact]
    public void Half_of_the_child_inside_the_viewport_crosses()
    {
        var viewport = new Rect(0, 0, 100, 200);

        Assert.True(InViewGate.Crosses(new Rect(0, 0, 40, 40), viewport));
        Assert.False(InViewGate.Crosses(new Rect(0, 190, 40, 40), viewport));
        Assert.True(InViewGate.Crosses(new Rect(0, 180, 40, 40), viewport));
    }

    [Fact]
    public void A_child_larger_than_the_viewport_crosses_when_half_the_viewport_is_covered()
    {
        var viewport = new Rect(0, 0, 100, 100);

        Assert.True(InViewGate.Crosses(new Rect(0, 0, 100, 400), viewport));
        Assert.False(InViewGate.Crosses(new Rect(0, 70, 100, 400), viewport));
        Assert.True(InViewGate.Crosses(new Rect(0, 40, 100, 400), viewport));
    }

    [Fact]
    public void A_horizontal_slice_uses_the_same_half()
    {
        var viewport = new Rect(0, 0, 100, 80);

        Assert.False(InViewGate.Crosses(new Rect(90, 0, 40, 80), viewport));
        Assert.True(InViewGate.Crosses(new Rect(80, 0, 40, 80), viewport));
    }

    [Fact]
    public void An_empty_rect_does_not_cross()
    {
        var child = new Rect(0, 0, 10, 10);

        Assert.False(InViewGate.Crosses(new Rect(0, 0, 0, 10), child));
        Assert.False(InViewGate.Crosses(child, new Rect(0, 0, double.NaN, 10)));
        Assert.False(InViewGate.Crosses(new Rect(200, 0, 10, 10), child));
    }

    [Fact]
    public void Ancestor_finds_the_nearest_scroll_view()
    {
        var outer = new ScrollView();
        var inner = new ScrollView();
        var stack = new VerticalStackLayout();
        var box = new BoxView();
        stack.Children.Add(box);
        inner.Content = stack;
        outer.Content = inner;

        Assert.Same(inner, InViewGate.Ancestor(box));
        Assert.Null(InViewGate.Ancestor(new BoxView()));
    }

    [Fact]
    public void InContent_sums_offsets_up_to_the_content()
    {
        var content = new VerticalStackLayout();
        var inner = new Grid();
        var box = new BoxView();
        inner.Children.Add(box);
        content.Children.Add(inner);
        Place(content, new Rect(5, 9, 200, 800));
        Place(inner, new Rect(0, 120, 200, 40));
        Place(box, new Rect(16, 4, 40, 40));

        var rect = InViewGate.InContent(box, content);

        Assert.Equal(16, rect.X, 3);
        Assert.Equal(124, rect.Y, 3);
        Assert.Equal(40, rect.Width, 3);
        Assert.Equal(40, rect.Height, 3);
    }

    [Fact]
    public void Viewport_uses_the_scroll_offset_and_size()
    {
        var scroll = new ScrollView();
        Place(scroll, new Rect(0, 0, 180, 320));

        var viewport = InViewGate.Viewport(scroll);

        Assert.Equal(0, viewport.X, 3);
        Assert.Equal(0, viewport.Y, 3);
        Assert.Equal(180, viewport.Width, 3);
        Assert.Equal(320, viewport.Height, 3);
    }

    static void Place(VisualElement element, Rect bounds)
    {
#pragma warning disable CS0618
        element.Layout(bounds);
#pragma warning restore CS0618
    }
}
