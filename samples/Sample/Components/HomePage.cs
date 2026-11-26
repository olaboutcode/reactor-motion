namespace Sample.Components;

/// <summary>
/// A list of the motion sample pages.
/// </summary>
sealed class HomePage : Component
{
    static readonly Color Ink = Color.FromArgb("#1C1C1E");
    static readonly Color Paper = Color.FromArgb("#F7F5F2");
    static readonly Color Night = Color.FromArgb("#111110");
    static readonly Color Muted = Color.FromArgb("#8E8E93");

    public override VisualNode Render()
    {
        var ink = Theme.IsDarkTheme ? Paper : Ink;
        return ContentPage("Reactor.Motion",
            ScrollView(
                VStack(
                    Label("Reactor.Motion")
                        .FontSize(22)
                        .TextColor(ink),
                    Label("Playground, stagger, scrub, color, timeline, path, matrix, enter / exit, in view, poses, drag, and collection.")
                        .FontSize(16)
                        .TextColor(Muted)
                        .Margin(0, 8, 0, 0),
                    VStack(Rows(ink))
                        .Spacing(0)
                        .Margin(0, 16, 0, 0)
                )
                .Spacing(0)
                .Padding(16)
            )
            .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
        )
        .BackgroundColor(Theme.IsDarkTheme ? Night : Paper)
        .HideNavigationBar();
    }

    static VisualNode[] Rows(Color ink) =>
    [
        Row("Playground", () => PageNavigation.Push<MotionPlaygroundPage>(), ink),
        Row("Stagger grid", () => PageNavigation.Push<StaggerGridPage>(), ink),
        Row("Scrub", () => PageNavigation.Push<MotionScrubPage>(), ink),
        Row("Color HSV vs RGB", () => PageNavigation.Push<MotionColorPage>(), ink),
        Row("Timeline", () => PageNavigation.Push<MotionTimelinePage>(), ink),
        Row("Path", () => PageNavigation.Push<MotionPathPage>(), ink),
        Row("Matrix", () => PageNavigation.Push<MotionTransformPage>(), ink),
        Row("Enter / exit", () => PageNavigation.Push<MotionPresencePage>(), ink),
        Row("In view", () => PageNavigation.Push<MotionInViewPage>(), ink),
        Row("Poses", () => PageNavigation.Push<MotionPosePage>(), ink),
        Row("Drag", () => PageNavigation.Push<MotionDragPage>(), ink),
        Row("Collection", () => PageNavigation.Push<MotionCollectionPage>(), ink),
    ];

    static MauiReactor.Grid Row(string title, Action open, Color ink)
        => Grid("44", "*,auto",
                Label(title)
                    .FontSize(16)
                    .TextColor(ink)
                    .VCenter(),
                Chevron()
                    .GridColumn(1)
                    .VCenter()
            )
            .HFill()
            .BackgroundColor(Colors.Transparent)
            .OnTapped(open);

    static MauiReactor.Image Chevron()
        => Image(PhosphorIcons.CaretRight)
            .Width(20)
            .Height(20)
            .Aspect(Aspect.AspectFit)
            .InputTransparent(inputTransparent: true);
}
