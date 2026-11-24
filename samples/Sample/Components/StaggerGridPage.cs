namespace Sample.Components;

sealed class StaggerGridPage : Component
{
    readonly MauiControls.BoxView?[] _tiles = new MauiControls.BoxView?[12];
    MotionPlayer? _player;

    protected override void OnWillUnmount()
    {
        _player?.Dispose();
        _player = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Stagger grid")
                    .GridRow(0),
                VStack(
                    Grid(
                        "auto, auto, auto, auto",
                        "*, *, *",
                        [.. Tiles()]
                    )
                    .ColumnSpacing(8)
                    .RowSpacing(8),
                    Label("Start follows the list. Center leaves the middle for last. End runs backward.")
                        .FontSize(14)
                        .HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center)
                        .TextColor(Colors.Gray),
                    HStack(
                        Button("Start", () => Play(StaggerFrom.Start)),
                        Button("Center", () => Play(StaggerFrom.Center)),
                        Button("End", () => Play(StaggerFrom.End))
                    )
                    .Spacing(8)
                    .HCenter()
                )
                .Spacing(16)
                .Padding(16)
                .GridRow(1)
            )
        )
        .HideNavigationBar();

    IEnumerable<VisualNode> Tiles()
    {
        for (var i = 0; i < _tiles.Length; i++)
        {
            var index = i;
            yield return BoxView(box => _tiles[index] = box)
                .HeightRequest(72)
                .CornerRadius(16)
                .BackgroundColor(Color.FromHsla(index / 36d, 0.55, 0.52))
                .GridRow(index / 3)
                .GridColumn(index % 3);
        }
    }

    void Play(StaggerFrom from)
    {
        var views = _tiles.OfType<MauiControls.BoxView>().ToArray();
        if (views.Length == 0)
            return;

        _player?.Dispose();
        _player = Motion.Play(
            m => m
                .FadeIn()
                .Scale(0.9, 1)
                .Stagger(40, from, (3, 4))
                .WithDuration(280),
            views);
    }
}
