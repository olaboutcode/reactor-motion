namespace Sample.Components;

sealed class MotionPresencePageState
{
    public bool Slide { get; set; } = true;
    public bool Scale { get; set; } = true;
}

/// <summary>
/// Toggles a slide card and a scale card. Each stays until its exit finishes.
/// </summary>
sealed class MotionPresencePage : Component<MotionPresencePageState>
{
    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Enter / exit")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Label("Hide leaves from the current pose and waits until the exit finishes.")
                            .FontSize(14)
                            .HCenter()
                            .HorizontalTextAlignment(TextAlignment.Center)
                            .TextColor(Colors.Gray),
                        Label("Slide")
                            .FontSize(16)
                            .HCenter(),
                        Stage(
                            Card(Colors.MediumPurple)
                                .EnterExit(
                                    State.Slide,
                                    enter: m => m.FadeIn().SlideIn(SlideFrom.Bottom, 28).WithDuration(900),
                                    exit: m => m.FadeOut().SlideOut(SlideFrom.Bottom, 20).WithDuration(700).WithPose())),
                        Button(State.Slide ? "Hide" : "Show", () => SetState(s => s.Slide = !s.Slide))
                            .HCenter(),
                        Label("Scale")
                            .FontSize(16)
                            .HCenter(),
                        Stage(
                            Card(Colors.CornflowerBlue)
                                .EnterExit(
                                    State.Scale,
                                    enter: m => m.FadeIn().ScaleIn(0.6).WithDuration(700),
                                    exit: m => m.FadeOut().ScaleOut(0.6).WithDuration(500).WithPose())),
                        Button(State.Scale ? "Hide" : "Show", () => SetState(s => s.Scale = !s.Scale))
                            .HCenter()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 32)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    static MauiReactor.Grid Stage(VisualNode card)
        => Grid("140", "*",
            Grid("*,auto,*", "*",
                card.GridRow(1)));

    static MauiReactor.BoxView Card(Color color)
        => BoxView()
            .HeightRequest(96)
            .WidthRequest(96)
            .CornerRadius(16)
            .BackgroundColor(color)
            .HCenter();
}
