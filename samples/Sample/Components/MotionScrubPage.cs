using Microsoft.Maui.Controls.Shapes;

namespace Sample.Components;

sealed class MotionScrubPageState
{
    public double T { get; set; }
    public double PathT { get; set; }
}

sealed class MotionScrubPage : Component<MotionScrubPageState>
{
    MauiControls.BoxView? _box;
    MauiControls.BoxView? _path;
    MotionPlayer? _player;
    MotionPlayer? _pathPlayer;

    protected override void OnWillUnmount()
    {
        _player?.Dispose();
        _pathPlayer?.Dispose();
        _player = null;
        _pathPlayer = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Scrub")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        RangeCard(),
                        Label("Drag a slider. SeekFraction is eased progress.")
                            .FontSize(14)
                            .HCenter()
                            .TextColor(Colors.Gray),
                        BoxView(b => _box = b)
                            .HeightRequest(96)
                            .WidthRequest(96)
                            .CornerRadius(16)
                            .BackgroundColor(Colors.OrangeRed)
                            .HCenter()
                            .OnLoaded(Bind),
                        Slider()
                            .Minimum(0)
                            .Maximum(1)
                            .Value(State.T)
                            .OnValueChanged(OnScrub),
                        Label($"t = {State.T:0.00}")
                            .HCenter()
                            .TextColor(Colors.Gray),
                        Label("Path")
                            .FontSize(16)
                            .HCenter(),
                        Label("The same fraction walks a cubic curve.")
                            .FontSize(14)
                            .HCenter()
                            .TextColor(Colors.Gray),
                        Grid(
                            BoxView()
                                .BackgroundColor(Colors.Transparent)
                                .HeightRequest(180),
                            BoxView(b => _path = b)
                                .HeightRequest(28)
                                .WidthRequest(28)
                                .CornerRadius(14)
                                .BackgroundColor(Colors.CornflowerBlue)
                                .HStart()
                                .VCenter()
                                .OnLoaded(BindPath)
                        )
                        .HeightRequest(180),
                        Slider()
                            .Minimum(0)
                            .Maximum(1)
                            .Value(State.PathT)
                            .OnValueChanged(OnScrubPath),
                        Label($"path = {State.PathT:0.00}")
                            .HCenter()
                            .TextColor(Colors.Gray),
                        HStack(
                            Button("Play", () => _ = Play()),
                            Button("Reset", Reset)
                        )
                        .Spacing(8)
                        .HCenter(),
                        TravelCard()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 32)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    void Bind()
    {
        if (_box is null)
            return;
        _player?.Dispose();
        _box.Opacity = 0;
        _box.ScaleX = 0.8;
        _box.ScaleY = 0.8;
        _box.Rotation = 0;
        _player = Motion.Bind(
            Motion.Define(m => m
                .Opacity(0, 1)
                .Scale(0.8, 1)
                .Rotate(0, 180)
                .WithDuration(800)
                .WithEasing(Easing.CubicOut)),
            _box);
    }

    void BindPath()
    {
        if (_path is null)
            return;
        _pathPlayer?.Dispose();
        ParkPath();
        _pathPlayer = Motion.Bind(
            Motion.Define(m => m
                .Path(Curve())
                .WithDuration(800)
                .WithEasing(Easing.CubicOut)),
            _path);
    }

    void OnScrub(double value)
    {
        SetState(s => s.T = value);
        _player?.SeekFraction(value);
    }

    void OnScrubPath(double value)
    {
        SetState(s => s.PathT = value);
        _pathPlayer?.SeekFraction(value);
    }

    async Task Play()
    {
        var fade = _player?.ForwardAsync() ?? Task.CompletedTask;
        var path = _pathPlayer?.ForwardAsync() ?? Task.CompletedTask;
        await Task.WhenAll(fade, path);
        if (_player?.IsCompleted() != true && _pathPlayer?.IsCompleted() != true)
            return;
        SetState(s =>
        {
            if (_player?.IsCompleted() == true)
                s.T = 1;
            if (_pathPlayer?.IsCompleted() == true)
                s.PathT = 1;
        });
    }

    void Reset()
    {
        _player?.Reset();
        _pathPlayer?.Reset();
        SetState(s =>
        {
            s.T = 0;
            s.PathT = 0;
        });
        if (_box is not null)
        {
            _box.Opacity = 0;
            _box.ScaleX = 0.8;
            _box.ScaleY = 0.8;
            _box.Rotation = 0;
        }

        ParkPath();
    }

    void ParkPath()
    {
        if (_path is null)
            return;
        _path.TranslationX = 0;
        _path.TranslationY = 0;
    }

    static MauiReactor.VStack RangeCard()
        => VStack(
            Label("Scroll the page. This card fades over the first 180px.")
                .FontSize(14)
                .HCenter()
                .HorizontalTextAlignment(TextAlignment.Center)
                .TextColor(Colors.Gray),
            BoxView()
                .HeightRequest(72)
                .HFill()
                .CornerRadius(16)
                .BackgroundColor(Colors.Teal)
                .Scrub(m => m.Opacity(1, 0).TranslateY(0, -20), from: 0, to: 180)
        )
        .Spacing(8);

    static MauiReactor.VStack TravelCard()
        => VStack(
            BoxView()
                .HeightRequest(520)
                .BackgroundColor(Colors.Transparent),
            Label("This card tracks its trip through the viewport.")
                .FontSize(14)
                .HCenter()
                .HorizontalTextAlignment(TextAlignment.Center)
                .TextColor(Colors.Gray),
            BoxView()
                .HeightRequest(96)
                .WidthRequest(96)
                .CornerRadius(16)
                .BackgroundColor(Colors.MediumPurple)
                .HCenter()
                .Scrub(m => m.Opacity(1, 0.2).TranslateY(0, -28))
        )
        .Spacing(8);

    static PathGeometry Curve()
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, 0) };
        figure.Segments.Add(new BezierSegment
        {
            Point1 = new Point(60, -70),
            Point2 = new Point(150, 70),
            Point3 = new Point(230, 0),
        });
        geometry.Figures.Add(figure);
        return geometry;
    }
}
