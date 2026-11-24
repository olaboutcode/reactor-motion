namespace Sample.Components;

sealed class MotionColorPage : Component
{
    MauiControls.BoxView? _hsv;
    MauiControls.BoxView? _rgb;
    MauiControls.BoxView? _stops;
    MotionPlayer? _hsvPlayer;
    MotionPlayer? _rgbPlayer;
    MotionPlayer? _stopsPlayer;

    protected override void OnWillUnmount()
    {
        _hsvPlayer?.Dispose();
        _rgbPlayer?.Dispose();
        _stopsPlayer?.Dispose();
        _hsvPlayer = null;
        _rgbPlayer = null;
        _stopsPlayer = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Color lerp")
                    .GridRow(0),
                VStack(
                    Label("HSV takes the short hue. RGB mixes channels. Stops walk red, blue, then lime.")
                        .FontSize(14)
                        .HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center)
                        .TextColor(Colors.Gray),
                    HStack(
                        Swatch("HSV", b => _hsv = b, BindHsv),
                        Swatch("RGB", b => _rgb = b, BindRgb)
                    )
                    .Spacing(24)
                    .HCenter(),
                    Swatch("Stops", b => _stops = b, BindStops)
                        .HCenter(),
                    Button("Play", Play)
                        .HCenter()
                )
                .Spacing(20)
                .Padding(24)
                .VCenter()
                .GridRow(1)
            )
        )
        .HideNavigationBar();

    static VStack Swatch(string title, Action<MauiControls.BoxView?> capture, Action onLoaded)
        => VStack(
            Label(title)
                .HCenter()
                .FontSize(16),
            BoxView(capture)
                .HeightRequest(96)
                .WidthRequest(96)
                .CornerRadius(16)
                .BackgroundColor(Colors.Red)
                .OnLoaded(onLoaded)
        )
        .Spacing(8);

    void BindHsv()
    {
        if (_hsv is null)
            return;
        _hsvPlayer?.Dispose();
        _hsvPlayer = Motion.Bind(
            Motion.Define(m => m
                .BackgroundColor(Colors.Red, Colors.Lime)
                .WithDuration(900)
                .WithEasing(Easing.Linear)
                .WithColorSpace(ColorSpace.Hsv)),
            _hsv);
    }

    void BindRgb()
    {
        if (_rgb is null)
            return;
        _rgbPlayer?.Dispose();
        _rgbPlayer = Motion.Bind(
            Motion.Define(m => m
                .BackgroundColor(Colors.Red, Colors.Lime)
                .WithDuration(900)
                .WithEasing(Easing.Linear)
                .WithColorSpace(ColorSpace.Rgb)),
            _rgb);
    }

    void BindStops()
    {
        if (_stops is null)
            return;
        _stopsPlayer?.Dispose();
        _stopsPlayer = Motion.Bind(
            Motion.Define(m => m
                .BackgroundColor(k => k
                    .At(0, Colors.Red)
                    .At(0.5, Colors.Blue)
                    .At(1, Colors.Lime))
                .WithDuration(1200)
                .WithEasing(Easing.Linear)
                .WithColorSpace(ColorSpace.Hsv)),
            _stops);
    }

    void Play()
    {
        _hsv?.SetValue(VisualElement.BackgroundColorProperty, Colors.Red);
        _rgb?.SetValue(VisualElement.BackgroundColorProperty, Colors.Red);
        _stops?.SetValue(VisualElement.BackgroundColorProperty, Colors.Red);
        _hsvPlayer.Forward();
        _rgbPlayer.Forward();
        _stopsPlayer.Forward();
    }
}
