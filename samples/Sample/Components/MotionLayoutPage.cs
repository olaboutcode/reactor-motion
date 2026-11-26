namespace Sample.Components;

/// <summary>
/// Opening a session inserts notes. Views under those notes spring from the last layout origin.
/// Closing plays a height exit, so the session shrinks closed.
/// </summary>
sealed class MotionLayoutPageState
{
    public bool Design { get; set; }

    public bool Build { get; set; }

    public bool Ship { get; set; }
}

sealed class MotionLayoutPage : Component<MotionLayoutPageState>
{
    const double ShortNotes = 88;

    const double LongNotes = 120;

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Layout")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Caption("Tap a session to open its notes. Everything under them springs from where it was. Tap it again and the notes shrink closed."),
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
                    ShortNotes,
                    State.Design,
                    () => SetState(state => state.Design = !state.Design)),
                Session(
                    "build",
                    "11:30",
                    "Implementation",
                    "Keep the notes in the day. The next session moves by their height, and that height is measured when layout runs.",
                    Colors.Teal,
                    LongNotes,
                    State.Build,
                    () => SetState(state => state.Build = !state.Build)),
                Session(
                    "ship",
                    "15:00",
                    "Release",
                    "Ship the build, then write down what the day actually changed.",
                    Colors.MediumPurple,
                    ShortNotes,
                    State.Ship,
                    () => SetState(state => state.Ship = !state.Ship)),
                Close()
            )
            .Spacing(10);

    static VisualNode Session(
        string key,
        string when,
        string title,
        string notes,
        Color color,
        double notesHeight,
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
                    Notes(open, notes, notesHeight)
                )
                .Spacing(0)
            )
            .Padding(16)
            .BackgroundColor(color)
            .Stroke(Colors.Transparent)
            .StrokeCornerRadius(16)
            .OnTapped(toggle)
            .Layout(motion => motion.FromPlace().WithSpring(Spring.Gentle))
            .WithKey(key);

    static VisualNode Notes(bool open, string notes, double height)
        => Border(
                Label(notes)
                    .FontSize(14)
                    .TextColor(Colors.White)
            )
            .Padding(12)
            .BackgroundColor(Colors.White.WithAlpha(0.16f))
            .Stroke(Colors.Transparent)
            .StrokeCornerRadius(12)
            .HeightRequest(height)
            .EnterExit(
                open,
                enter: motion => motion.Height(height, height).WithDuration(1),
                exit: motion => motion
                    .Height(height, 0)
                    .ScaleY(1, 0)
                    .AnchorY(0, 0)
                    .WithDuration(280)
                    .WithEasing(Easing.CubicOut));

    static VisualNode Close()
        => Label("End of Thursday. This line moves by the height of every note that opened.")
            .FontSize(14)
            .TextColor(Colors.Gray)
            .HorizontalTextAlignment(TextAlignment.Center)
            .Layout(motion => motion.FromPlace().WithSpring(Spring.Gentle))
            .WithKey("close");
}
