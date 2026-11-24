namespace Sample.Components;

sealed class MotionTransformPage : Component
{
    static readonly Motion Turn = Motion.Define(m => m
        .Transform(t => Matrix4.Identity
            .SetEntry(3, 2, 0.002)
            .RotateY(t * 2 * Math.PI)
            .Translate(0, t * 40))
        .WithDuration(900)
        .WithEasing(Easing.CubicInOut));

    static readonly Motion Tilt = Motion.Define(m => m
        .Transform(t => Matrix4.Identity
            .SetEntry(3, 2, 0.003)
            .RotateX(t * Math.PI / 5)
            .Scale(1 + (t * 0.2)))
        .WithDuration(700)
        .WithEasing(Easing.CubicOut));

    static readonly Motion Flip = Motion.Define(m => m
        .Transform(t => Matrix4.Identity
            .SetEntry(3, 2, 0.004)
            .RotateX(t * Math.PI))
        .WithDuration(800)
        .WithEasing(Easing.CubicInOut));

    MotionPlayer? _turnPlayer;
    MotionPlayer? _tiltPlayer;
    MotionPlayer? _flipPlayer;

    protected override void OnWillUnmount()
    {
        _turnPlayer?.Dispose();
        _tiltPlayer?.Dispose();
        _flipPlayer?.Dispose();
        _turnPlayer = null;
        _tiltPlayer = null;
        _flipPlayer = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Matrix")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Label("One callback returns the whole transform.")
                            .FontSize(14)
                            .HCenter()
                            .TextColor(Colors.Gray),
                        VStack(
                            Card("Turn and drop", Colors.OrangeRed, Turn, player => _turnPlayer = player),
                            Card("Tilt and grow", Colors.CornflowerBlue, Tilt, player => _tiltPlayer = player),
                            Card("Flip", Colors.MediumPurple, Flip, player => _flipPlayer = player),
                            HStack(
                                Button("Play", Play),
                                Button("Reverse", Reverse),
                                Button("Reset", ResetAll)
                            )
                            .Spacing(10)
                            .HCenter()
                        )
                        .Spacing(28)
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 32)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    static VStack Card(string title, Color color, Motion motion, Action<MotionPlayer> onBind)
        => VStack(
            Label(title)
                .FontSize(14)
                .TextColor(Colors.Gray)
                .HCenter(),
            BoxView()
                .HeightRequest(96)
                .WidthRequest(96)
                .CornerRadius(16)
                .BackgroundColor(color)
                .HCenter()
                .AnchorX(0.5)
                .AnchorY(0.5)
                .BindMotion(motion, onBind)
        );

    void Play()
    {
        _turnPlayer?.Forward();
        _tiltPlayer?.Forward();
        _flipPlayer?.Forward();
    }

    void Reverse()
    {
        _turnPlayer?.Reverse();
        _tiltPlayer?.Reverse();
        _flipPlayer?.Reverse();
    }

    void ResetAll()
    {
        _turnPlayer?.Reset();
        _tiltPlayer?.Reset();
        _flipPlayer?.Reset();
    }
}
