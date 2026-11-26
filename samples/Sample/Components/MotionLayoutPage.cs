namespace Sample.Components;

/// <summary>
/// Opening a session inserts notes whose height is not known ahead of time.
/// Each view under those notes springs from the origin the last layout pass recorded.
/// </summary>
sealed class MotionLayoutPageState
{
    public bool DesignOpen { get; set; }

    public bool BuildOpen { get; set; }

    public bool ShipOpen { get; set; }
}

sealed class MotionLayoutPage : Component<MotionLayoutPageState>
{
    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Layout")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Caption("Tap a session to open its notes, and tap it again to close them. Everything under the notes springs from where it was. The recipe does not name the distance."),
                        Schedule()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 48)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    static MauiReactor.Label Caption(string text)
        => Label(text)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray);

    MauiReactor.VStack Schedule()
        => VStack(
                Session(
                    "design",
                    "09:00",
                    "Design review",
                    "Walk the empty states, then the path from the list to the detail.",
                    Colors.OrangeRed,
                    State.DesignOpen,
                    () => SetState(state => state.DesignOpen = !state.DesignOpen)),
                Session(
                    "build",
                    "11:30",
                    "Implementation",
                    "Keep the notes in the day. The next session moves by their height, and that height is measured when layout runs.",
                    Colors.Teal,
                    State.BuildOpen,
                    () => SetState(state => state.BuildOpen = !state.BuildOpen)),
                Session(
                    "ship",
                    "15:00",
                    "Release",
                    "Ship the build, then write down what the day actually changed.",
                    Colors.MediumPurple,
                    State.ShipOpen,
                    () => SetState(state => state.ShipOpen = !state.ShipOpen)),
                Close()
            )
            .Spacing(10);

    static VisualNode Session(
        string key,
        string when,
        string title,
        string notes,
        Color color,
        bool open,
        Action toggle)
        => Border(
                VStack(
                    Grid(
                        "auto",
                        "auto,*",
                        Label(when)
                            .FontSize(13)
                            .TextColor(Colors.White.WithAlpha(0.75f))
                            .VCenter()
                            .Margin(0, 0, 12, 0),
                        Label(title)
                            .FontSize(18)
                            .TextColor(Colors.White)
                            .VCenter()
                            .GridColumn(1)
                    ),
                    Notes(notes, open)
                )
                .Spacing(10)
            )
            .Padding(16)
            .BackgroundColor(color)
            .Stroke(Colors.Transparent)
            .StrokeCornerRadius(16)
            .OnTapped(toggle)
            .Layout(motion => motion.FromPlace().WithSpring(Spring.Gentle))
            .WithKey(key);

    static MauiReactor.Border Notes(string notes, bool open)
        => Border(
                Label(notes)
                    .FontSize(14)
                    .TextColor(Colors.White)
            )
            .Padding(12)
            .BackgroundColor(Colors.White.WithAlpha(0.16f))
            .Stroke(Colors.Transparent)
            .StrokeCornerRadius(12)
            .IsVisible(open);

    static VisualNode Close()
        => Label("End of Thursday. This line moves by the height of every note that opened.")
            .FontSize(14)
            .TextColor(Colors.Gray)
            .HorizontalTextAlignment(TextAlignment.Center)
            .Layout(motion => motion.FromPlace().WithSpring(Spring.Gentle))
            .WithKey("close");
}
