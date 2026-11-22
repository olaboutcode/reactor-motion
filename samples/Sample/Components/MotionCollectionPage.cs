namespace Sample.Components;

sealed record FlipItem(int Id, string Name);

sealed class MotionCollectionPageState
{
    public List<FlipItem> Items { get; } =
    [
        new(0, "Cedar"),
        new(1, "Maple"),
        new(2, "Birch"),
        new(3, "Aspen"),
    ];

    public int NextId { get; set; } = 4;

    public MauiControls.StackOrientation Orientation { get; set; } = MauiControls.StackOrientation.Vertical;
}

/// <summary>
/// Inserts, removes, and reorders items. The items that stay slide from where they were.
/// </summary>
sealed class MotionCollectionPage : Component<MotionCollectionPageState>
{
    static readonly string[] Names = ["Cedar", "Maple", "Birch", "Aspen", "Pine", "Oak", "Willow", "Elm"];

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Collection")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Label("Collection")
                            .FontSize(28)
                            .HCenter(),
                        Caption("Add, delete, or move an item. The others slide from where they were. A deleted item fades in place."),
                        Button("Add", Add)
                            .HCenter(),
                        Button(State.Orientation == MauiControls.StackOrientation.Vertical ? "Horizontal" : "Vertical", ToggleOrientation)
                            .HCenter(),
                        Items()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 32)
                )
                .GridRow(1)
            )
        )
        .HideNavigationBar();

    void ToggleOrientation()
        => SetState(s => s.Orientation = s.Orientation == MauiControls.StackOrientation.Vertical
            ? MauiControls.StackOrientation.Horizontal
            : MauiControls.StackOrientation.Vertical);

    VisualNode Items()
    {
        static Motion move(Motion m) => m.WithDuration(280).WithEasing(Easing.CubicOut);
        static Motion enter(Motion m) => m.FadeIn().SlideIn(SlideFrom.Top, 16).WithDuration(280);
        static Motion exit(Motion m) => m.FadeOut().WithDuration(200);
        
        var across = State.Orientation == MauiControls.StackOrientation.Horizontal;
        VisualNode collection = across
            ? Motion.HCollection(State.Items, item => item.Id, Row, move, enter, exit, spacing: 8)
            : Motion.VCollection(State.Items, item => item.Id, Row, move, enter, exit, spacing: 8);
        if (State.Orientation != MauiControls.StackOrientation.Horizontal)
            return collection;

        return ScrollView(collection)
            .Orientation(ScrollOrientation.Horizontal)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Never);
    }

    void Add()
        => SetState(s =>
        {
            s.Items.Insert(0, new FlipItem(s.NextId, Names[s.NextId % Names.Length]));
            s.NextId++;
        });

    void Delete(int id)
        => SetState(s => s.Items.RemoveAll(item => item.Id == id));

    void Move(int id, int delta)
        => SetState(s =>
        {
            var index = s.Items.FindIndex(item => item.Id == id);
            var target = index + delta;
            if (index < 0 || target < 0 || target >= s.Items.Count)
                return;

            (s.Items[index], s.Items[target]) = (s.Items[target], s.Items[index]);
        });

    VisualNode Row(FlipItem item)
    {
        var across = State.Orientation == MauiControls.StackOrientation.Horizontal;
        var row = Border(
            Grid(
                "64",
                across ? "auto,auto,auto,auto" : "*,auto,auto,auto",
                Label(item.Name)
                    .FontSize(16)
                    .VCenter()
                    .Margin(16, 0, 0, 0),
                Button("Up", () => Move(item.Id, -1))
                    .GridColumn(1)
                    .FontSize(12)
                    .VCenter(),
                Button("Down", () => Move(item.Id, 1))
                    .GridColumn(2)
                    .FontSize(12)
                    .VCenter(),
                Button("X", () => Delete(item.Id))
                    .GridColumn(3)
                    .FontSize(12)
                    .BackgroundColor(Colors.OrangeRed)
                    .VCenter()
                    .Margin(0, 0, 12, 0))
            .BackgroundColor(Colors.Teal.WithAlpha(0.15f))
            .ColumnSpacing(4))
            .Stroke(Colors.Transparent)
            .StrokeCornerRadius(16);
        return across ? row.HStart() : row;
    }

    static MauiReactor.Label Caption(string text)
        => Label(text)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray);
}
