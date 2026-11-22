namespace Sample.Components;

sealed class MotionPresencePageState
{
    public bool Present { get; set; } = true;
}

/// <summary>
/// Toggles a card. The card stays on screen until the exit run finishes.
/// </summary>
sealed class MotionPresencePage : Component<MotionPresencePageState>
{
    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Enter / exit")
                    .GridRow(0),
                VStack(
                    Label("Enter / exit")
                        .FontSize(28)
                        .HCenter(),
                    Label("Hide leaves from the current pose and waits until the exit finishes.")
                        .FontSize(14)
                        .HCenter()
                        .TextColor(Colors.Gray),
                    Grid("140", "*",
                        Grid("*,auto,*", "*",
                            Card()
                                .EnterExit(
                                    State.Present,
                                    enter: m => m.FadeIn().SlideIn(SlideFrom.Bottom, 28).WithDuration(900),
                                    exit: m => m.FadeOut().SlideOut(SlideFrom.Bottom, 20).WithDuration(700).WithPose())
                                .GridRow(1)
                        )
                    ),
                    Button(State.Present ? "Hide" : "Show", () => SetState(s => s.Present = !s.Present))
                        .HCenter()
                )
                .Spacing(16)
                .Padding(24)
                .VCenter()
                .GridRow(1)
            )
        )
        .HideNavigationBar();

    static MauiReactor.BoxView Card()
        => BoxView()
            .HeightRequest(96)
            .WidthRequest(96)
            .CornerRadius(16)
            .BackgroundColor(Colors.MediumPurple)
            .HCenter();
}
