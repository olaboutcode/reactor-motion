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
                        
                        Button("Add An Item", Add)
                            .HCenter()
                            .CornerRadius(12),
                        
                        Items()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 32)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    VisualNode Items()
        => Motion.Collection(
            State.Items,
            itemKey: item => item.Id,
            renderItem: Row,
            placeItems: rows => VStack([.. rows]).Spacing(8),
            moveMotion: m => m.FromPlace().WithDuration(280).WithEasing(Easing.CubicOut),
            enterMotion: m => m.FadeIn().SlideIn(SlideFrom.Top, 16).WithDuration(280),
            exitMotion: m => m.FadeOut().WithDuration(200));

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
        var index = State.Items.FindIndex(entry => entry.Id == item.Id);
        var canMoveUp = index > 0;
        var canMoveDown = index >= 0 && index < State.Items.Count - 1;
        return Border(
            Grid("*", "*,auto",
                Border(
                    Grid(
                        Label(item.Name)
                        .FontSize(16)
                        .TextColor(Colors.White)
                        .Center()
                    )
                )
                .StrokeCornerRadius(12)
                .Stroke(Colors.Transparent)
                .BackgroundColor(Colors.Teal.WithAlpha(0.6f)),

                VStack(
                    Button("Move Up", () => Move(item.Id, -1))
                        .GridColumn(1)
                        .FontSize(12)
                        .CornerRadius(12)
                        .BackgroundColor(Colors.Teal.WithAlpha(canMoveUp ? 0.8f : 0.25f))
                        .IsEnabled(canMoveUp),
                    Button("Move Down", () => Move(item.Id, 1))
                        .GridColumn(2)
                        .FontSize(12)
                        .CornerRadius(12)
                        .BackgroundColor(Colors.Teal.WithAlpha(canMoveDown ? 0.8f : 0.25f))
                        .IsEnabled(canMoveDown),

                    Button("Delete", () => Delete(item.Id))
                        .GridColumn(3)
                        .FontSize(12)
                        .CornerRadius(12)
                        .BackgroundColor(Colors.OrangeRed)
                )
                .HCenter()
                .Spacing(4)
                .GridColumn(1)
            )
            .ColumnSpacing(8)
        )
        .Padding(8)
        .StrokeCornerRadius(18)
        .Stroke(Colors.Transparent)
        .MinimumWidthRequest(320)
        .BackgroundColor(Colors.Teal.WithAlpha(0.2f));
    }

    static MauiReactor.Label Caption(string text)
        => Label(text)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray);
}
