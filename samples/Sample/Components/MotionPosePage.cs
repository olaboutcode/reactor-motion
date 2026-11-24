namespace Sample.Components;

/// <summary>
/// Two cards spring toward an outline. Halfway, Pose comes back from the live
/// position and keeps the spring speed. Snap jumps onto the outline, then returns.
/// </summary>
sealed class MotionPosePage : Component
{
    const double Travel = 200;
    const double Card = 56;
    const double LaneWidth = Travel + Card;

    // Soft on purpose. A preset spring reaches the turn before the split is visible.
    static readonly Spring Glide = new(8, 8, 12);

    MauiControls.BoxView? _pose;
    MauiControls.BoxView? _snap;
    MotionPlayer? _posePlayer;
    MotionPlayer? _snapPlayer;
    int _generation;

    protected override void OnWillUnmount()
    {
        _generation++;
        _posePlayer?.Dispose();
        _snapPlayer?.Dispose();
        _posePlayer = null;
        _snapPlayer = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Poses")
                    .GridRow(0),
                VStack(
                    Label("Halfway to the outline, Pose comes back from the card and keeps its speed. Snap jumps to the outline.")
                        .FontSize(14)
                        .HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center)
                        .TextColor(Colors.Gray),
                    Lane("Pose", Colors.Teal, box => _pose = box),
                    Lane("Snap", Colors.Coral, box => _snap = box),
                    Button("Play", Play)
                        .HCenter()
                )
                .Spacing(16)
                .Padding(24)
                .VCenter()
                .GridRow(1)
            )
        )
        .HideNavigationBar();

    static VStack Lane(string title, Color color, Action<MauiControls.BoxView?> capture)
        => VStack(
            Label(title)
                .FontSize(14)
                .TextColor(color),
            Grid("76", $"{LaneWidth}",
                BoxView()
                    .HeightRequest(2)
                    .BackgroundColor(Colors.Gray.WithAlpha(0.35f))
                    .VCenter(),
                BoxView()
                    .WidthRequest(Card)
                    .HeightRequest(Card)
                    .CornerRadius(12)
                    .BackgroundColor(color.WithAlpha(0.18f))
                    .Margin(Travel, 0, 0, 0)
                    .HStart()
                    .VCenter(),
                BoxView(capture)
                    .WidthRequest(Card)
                    .HeightRequest(Card)
                    .CornerRadius(12)
                    .BackgroundColor(color)
                    .HStart()
                    .VCenter()
            )
            .HCenter()
        )
        .Spacing(6);

    void Play()
    {
        if (_pose is null || _snap is null)
            return;

        var generation = ++_generation;
        _pose.TranslationX = 0;
        _snap.TranslationX = 0;

        // Replace in place. Disposing the current player first would drop its speed.
        _posePlayer = Motion.Play(Outbound, _pose);
        _snapPlayer = Motion.Play(Outbound, _snap);
        _posePlayer.At(progress => TurnWhenHalfway(generation, progress));
    }

    void TurnWhenHalfway(int generation, double progress)
    {
        if (generation != _generation || progress < 0.5)
            return;

        var turn = ++_generation;
        var dispatcher = _pose?.Dispatcher;
        if (dispatcher is null || !dispatcher.Dispatch(() => ComeHome(turn)))
            ComeHome(turn);
    }

    void ComeHome(int turn)
    {
        if (turn != _generation || _pose is null || _snap is null)
            return;

        _posePlayer = Motion.Play(PoseHome, _pose);
        _snapPlayer = Motion.Play(SnapHome, _snap);
    }

    static Motion Outbound(Motion motion)
        => motion.TranslateX(0, Travel).WithSpring(Glide);

    static Motion PoseHome(Motion motion)
        => motion.TranslateX(Travel, 0).WithPose().WithSpring(Glide);

    static Motion SnapHome(Motion motion)
        => motion.TranslateX(Travel, 0).WithSpring(Glide);
}
