namespace Sample.Components;

/// <summary>
/// Hosts the gallery in a MauiReactor navigation page. The page callback keeps
/// <see cref="SampleNavigation.Current"/> so list rows can push a demo.
/// </summary>
sealed class App : Component
{
    public override VisualNode Render()
        => NavigationPage(
            page => SampleNavigation.Current = page?.Navigation,
            new HomePage());
}

/// <summary>
/// The navigation stack for the sample. Set when the host page is created.
/// </summary>
static class SampleNavigation
{
    public static Microsoft.Maui.Controls.INavigation? Current { get; set; }
}
