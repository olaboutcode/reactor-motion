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
    MauiControls.Label? _readout;
    MauiControls.Label? _pathReadout;
    MotionPlayer? _player;
    MotionPlayer? _pathPlayer;
    double _liveT;
    double _livePath;
    bool _dragT;
    bool _dragPath;
    bool _flushPosted;

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
                        Label("Drag the slider. The box stays in view, scales, and turns.")
                            .FontSize(14)
                            .HCenter()
                            .TextColor(Colors.Gray),
                        BoxView(b => _box = b)
                            .HeightRequest(96)
                            .WidthRequest(96)
                            .CornerRadius(16)
                            .BackgroundColor(Colors.OrangeRed)
                            .Opacity(1)
                            .HCenter()
                            .OnLoaded(Bind),
                        Slider()
                            .Minimum(0)
                            .Maximum(1)
                            .Value(State.T)
                            .OnDragStarted(BeginScrub)
                            .OnDragCompleted(EndScrub)
                            .OnValueChanged(OnScrub),
                        Label(l => _readout = l)
                            .Text(Readout("t", State.T))
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
                                .CornerRadius(16)
                                .BackgroundColor(Colors.CornflowerBlue.WithAlpha(0.08f))
                                .InputTransparent(inputTransparent: true),
                            BoxView(b => _path = b)
                                .HeightRequest(28)
                                .WidthRequest(28)
                                .CornerRadius(14)
                                .BackgroundColor(Colors.CornflowerBlue)
                                .Margin(16, 0, 0, 0)
                                .HStart()
                                .VCenter()
                                .OnLoaded(BindPath)
                        )
                        .HeightRequest(220)
                        .HFill(),
                        Slider()
                            .Minimum(0)
                            .Maximum(1)
                            .Value(State.PathT)
                            .OnDragStarted(BeginPath)
                            .OnDragCompleted(EndPath)
                            .OnValueChanged(OnScrubPath),
                        Label(l => _pathReadout = l)
                            .Text(Readout("path", State.PathT))
                            .HCenter()
                            .TextColor(Colors.Gray),
                        Button("Reset", Reset)
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
        _box.Opacity = 1;
        _player = Motion.Bind(
            Motion.Define(m => m
                .Scale(0.8, 1)
                .Rotate(0, 180)
                .WithDuration(800)
                .WithEasing(Easing.CubicOut)),
            _box);
        _player.SeekFraction(State.T);
    }

    void BindPath()
    {
        if (_path is null)
            return;
        _pathPlayer?.Dispose();
        _pathPlayer = Motion.Bind(
            Motion.Define(m => m
                .Path(Curve())
                .WithDuration(800)
                .WithEasing(Easing.CubicOut)),
            _path);
        _pathPlayer.SeekFraction(State.PathT);
    }

    void BeginScrub() => _dragT = true;

    void OnScrub(double value)
    {
        _liveT = value;
        Seek(_player, value);
        if (!_dragT)
            PostFlush();
    }

    void EndScrub()
    {
        _dragT = false;
        PostFlush();
    }

    void BeginPath() => _dragPath = true;

    void OnScrubPath(double value)
    {
        _livePath = value;
        Seek(_pathPlayer, value);
        if (!_dragPath)
            PostFlush();
    }

    void EndPath()
    {
        _dragPath = false;
        PostFlush();
    }

    // The slider callback is still inside UIKit. Rebuilding the page there
    // disposes the native view before the callback returns.
    void PostFlush()
    {
        if (_flushPosted)
            return;
        _flushPosted = true;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            Flush();
            return;
        }

        dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1), Flush);
    }

    void Flush()
    {
        _flushPosted = false;
        if (_dragT || _dragPath)
            return;

        var t = _liveT;
        var path = _livePath;
        SetState(s =>
        {
            s.T = t;
            s.PathT = path;
        });
        Seek(_player, t);
        Seek(_pathPlayer, path);
    }

    static void Seek(MotionPlayer? player, double value)
    {
        if (player is null)
            return;
        try
        {
            player.SeekFraction(value);
        }
        catch (ObjectDisposedException)
        {
            // The native view or the player was released during the gesture.
        }
    }

    static string Readout(string name, double value) => $"{name} = {value:0.00}";

    void Reset()
    {
        _liveT = 0;
        _livePath = 0;
        _player?.Reset();
        _pathPlayer?.Reset();
        if (_box is not null)
            _box.Opacity = 1;
        SetState(s =>
        {
            s.T = 0;
            s.PathT = 0;
        });
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
            Point1 = new Point(48, -52),
            Point2 = new Point(120, 52),
            Point3 = new Point(176, 0),
        });
        geometry.Figures.Add(figure);
        return geometry;
    }
}
