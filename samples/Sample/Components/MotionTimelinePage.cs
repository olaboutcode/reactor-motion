namespace Sample.Components;

/// <summary>
/// Sequence, overlap, a box and an image on one timeline, hold, yoyo, stagger, and a spring.
/// </summary>
sealed class MotionTimelinePage : Component
{
    const double Card = 64;

    MauiControls.BoxView? _sequence;
    MauiControls.BoxView? _overlap;
    MauiControls.BoxView? _offsetBox;
    MauiControls.Image? _offsetImage;
    MauiControls.BoxView? _hold;
    MauiControls.BoxView? _yoyo;
    MauiControls.BoxView? _spring;
    MauiControls.BoxView? _stagger0;
    MauiControls.BoxView? _stagger1;
    MauiControls.BoxView? _stagger2;

    MotionPlayer? _sequencePlayer;
    MotionPlayer? _overlapPlayer;
    MotionPlayer? _offsetPlayer;
    MotionPlayer? _holdPlayer;
    MotionPlayer? _yoyoPlayer;
    MotionPlayer? _staggerPlayer;
    MotionPlayer? _springPlayer;

    protected override void OnWillUnmount()
    {
        _sequencePlayer?.Dispose();
        _overlapPlayer?.Dispose();
        _offsetPlayer?.Dispose();
        _holdPlayer?.Dispose();
        _yoyoPlayer?.Dispose();
        _staggerPlayer?.Dispose();
        _springPlayer?.Dispose();
        _sequencePlayer = null;
        _overlapPlayer = null;
        _offsetPlayer = null;
        _holdPlayer = null;
        _yoyoPlayer = null;
        _staggerPlayer = null;
        _springPlayer = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Home")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Label("Timeline")
                            .FontSize(28)
                            .HCenter(),
                        Caption("The timeline carries the defaults. Each beat keeps a duration or easing it set itself."),
                        Label("Sequence")
                            .FontSize(16)
                            .HCenter(),
                        Caption("Fade and slide for 280 ms, then a 200 ms scale. Intro and Pulse seek to those beats."),
                        Stage(Colors.CornflowerBlue, box => _sequence = box, BindSequence),
                        Actions(
                            Button("Play", () => _ = Play(_sequencePlayer, RestSequence)),
                            Button("Intro", () => _sequencePlayer?.Seek("intro")),
                            Button("Pulse", () => _sequencePlayer?.Seek("pulse"))),
                        Label("Overlap")
                            .FontSize(16)
                            .HCenter(),
                        Caption("The slide is 320 ms. The turn starts 140 ms in."),
                        Lane(Colors.Teal, 72, box => _overlap = box, BindOverlap),
                        Actions(Button("Play", () => _ = Play(_overlapPlayer, RestOverlap))),
                        Label("Box and image")
                            .FontSize(16)
                            .HCenter(),
                        Caption("The box slides for 300 ms. The image starts a 300 ms turn 100 ms later."),
                        HStack(
                            VStack(
                                Label("Box")
                                    .FontSize(12)
                                    .TextColor(Colors.SteelBlue)
                                    .HCenter(),
                                Lane(Colors.SteelBlue, 48, box => _offsetBox = box, BindOffset)
                            )
                            .Spacing(6),
                            VStack(
                                Label("Image")
                                    .FontSize(12)
                                    .TextColor(Colors.SteelBlue)
                                    .HCenter(),
                                Picture(image => _offsetImage = image, BindOffset)
                            )
                            .Spacing(6)
                            .VCenter()
                        )
                        .Spacing(24)
                        .HCenter(),
                        Actions(Button("Play", () => _ = Play(_offsetPlayer, RestOffset))),
                        Label("Hold")
                            .FontSize(16)
                            .HCenter(),
                        Caption("The timeline waits 360 ms. The slide waits another 200 ms after the fade."),
                        Lane(Colors.Coral, 64, box => _hold = box, BindHold),
                        Actions(Button("Play", () => _ = Play(_holdPlayer, RestHold))),
                        Label("Yoyo")
                            .FontSize(16)
                            .HCenter(),
                        Caption("It moves across, then the same beat plays backward."),
                        Lane(Colors.MediumPurple, 80, box => _yoyo = box, BindYoyo),
                        Actions(Button("Play", () => _ = Play(_yoyoPlayer, RestYoyo))),
                        Label("Stagger")
                            .FontSize(16)
                            .HCenter(),
                        Caption("One timeline, three views. Each starts 90 ms after the one before it."),
                        HStack(
                            Chip(Colors.Teal, box => _stagger0 = box, BindStagger),
                            Chip(Colors.Teal, box => _stagger1 = box, BindStagger),
                            Chip(Colors.Teal, box => _stagger2 = box, BindStagger))
                            .Spacing(12)
                            .HCenter(),
                        Actions(Button("Play", () => _ = Play(_staggerPlayer, RestStagger))),
                        Label("Spring")
                            .FontSize(16)
                            .HCenter(),
                        Caption("A spring plays the move. The timeline has no duration."),
                        Lane(Colors.Orange, 80, box => _spring = box, BindSpring),
                        Actions(Button("Play", () => _ = Play(_springPlayer, RestSpring))))
                    .Spacing(12)
                    .Padding(24, 24, 24, 32)
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

    static HStack Actions(params VisualNode[] buttons)
        => HStack(buttons)
            .Spacing(8)
            .HCenter();

    static MauiReactor.Grid Stage(Color color, Action<MauiControls.BoxView?> capture, Action loaded)
        => Grid("100", "180",
            Mark(color).HCenter().VCenter(),
            CardView(color, capture, loaded).HCenter().VCenter())
            .HCenter();

    static MauiReactor.Grid Lane(Color color, double travel, Action<MauiControls.BoxView?> capture, Action loaded)
        => Grid("100", $"{travel + Card}",
            BoxView()
                .HeightRequest(2)
                .BackgroundColor(Colors.Gray.WithAlpha(0.35f))
                .VCenter(),
            Mark(color)
                .HStart()
                .VCenter(),
            Mark(color)
                .Margin(travel, 0, 0, 0)
                .HStart()
                .VCenter(),
            CardView(color, capture, loaded)
                .HStart()
                .VCenter())
            .HCenter();

    static MauiReactor.Grid Picture(Action<MauiControls.Image?> capture, Action loaded)
        => Grid("100", "88",
            BoxView()
                .WidthRequest(Card)
                .HeightRequest(Card)
                .CornerRadius(14)
                .BackgroundColor(Colors.White)
                .HCenter()
                .VCenter(),
            Image(PhosphorIcons.ShareNetwork, capture)
                .WidthRequest(40)
                .HeightRequest(40)
                .Aspect(Aspect.AspectFit)
                .HCenter()
                .VCenter()
                .OnLoaded(loaded))
            .HCenter();

    static MauiReactor.Grid Chip(Color color, Action<MauiControls.BoxView?> capture, Action loaded)
        => Grid("88", "64",
            Mark(color).HCenter().VCenter(),
            CardView(color, capture, loaded).HCenter().VCenter());

    static MauiReactor.BoxView Mark(Color color)
        => BoxView()
            .WidthRequest(Card)
            .HeightRequest(Card)
            .CornerRadius(14)
            .BackgroundColor(color.WithAlpha(0.18f))
            .InputTransparent(inputTransparent: true);

    static MauiReactor.BoxView CardView(Color color, Action<MauiControls.BoxView?> capture, Action loaded)
        => BoxView(capture)
            .WidthRequest(Card)
            .HeightRequest(Card)
            .CornerRadius(14)
            .BackgroundColor(color)
            .OnLoaded(loaded);

    static Motion SequenceMotion()
        => Motion.Timeline(duration: 280, easing: Easing.CubicOut)
            .Add(Motion.Define(m => m.FadeIn().SlideIn(SlideFrom.Left, 48)), id: "intro")
            .Then(Motion.Define(m => m.Scale(1, 1.12).WithDuration(200)), "pulse");

    static Motion OverlapMotion()
        => Motion.Timeline(duration: 320, easing: Easing.CubicOut)
            .Add(Motion.Define(m => m.TranslateX(0, 72)), id: "slide")
            .Add(Motion.Define(m => m.Rotate(0, 24)), at: 140, id: "turn");

    static Motion OffsetMotion()
        => Motion.Timeline(easing: Easing.CubicOut)
            .Add(Motion.Define(m => m.TranslateX(0, 48).WithDuration(300)), id: "slide", target: 0)
            .Add(Motion.Define(m => m.Rotate(0, 80).WithDuration(300)), at: 100, id: "turn", target: 1);

    static Motion HoldMotion()
        => Motion.Timeline(duration: 280, easing: Easing.CubicOut, delay: 360)
            .Add(Motion.Define(m => m.FadeIn()))
            .Then(Motion.Define(m => m.TranslateX(0, 64).WithDelay(200)));

    static Motion YoyoMotion()
        => Motion.Timeline(duration: 420, easing: Easing.CubicInOut, yoyo: true)
            .Add(Motion.Define(m => m.TranslateX(0, 80)));

    static Motion StaggerMotion()
        => Motion.Timeline(duration: 320, easing: Easing.CubicOut, stagger: new Stagger(90))
            .Add(Motion.Define(m => m.FadeIn().TranslateY(16, 0)));

    static Motion SpringMotion()
        => Motion.Timeline(spring: Spring.Gentle, springVelocity: 2)
            .Add(Motion.Define(m => m.TranslateX(0, 80).Scale(1, 1.12)));

    void BindSequence()
    {
        if (_sequence is null)
            return;
        _sequencePlayer?.Dispose();
        RestSequence();
        _sequencePlayer = SequenceMotion().Bind(_sequence);
    }

    void BindOverlap()
    {
        if (_overlap is null)
            return;
        _overlapPlayer?.Dispose();
        RestOverlap();
        _overlapPlayer = OverlapMotion().Bind(_overlap);
    }

    void BindOffset()
    {
        if (_offsetBox is null || _offsetImage is null)
            return;
        _offsetPlayer?.Dispose();
        RestOffset();
        _offsetPlayer = OffsetMotion().Bind([_offsetBox, _offsetImage]);
    }

    void BindHold()
    {
        if (_hold is null)
            return;
        _holdPlayer?.Dispose();
        RestHold();
        _holdPlayer = HoldMotion().Bind(_hold);
    }

    void BindYoyo()
    {
        if (_yoyo is null)
            return;
        _yoyoPlayer?.Dispose();
        RestYoyo();
        _yoyoPlayer = YoyoMotion().Bind(_yoyo);
    }

    void BindStagger()
    {
        if (_stagger0 is null || _stagger1 is null || _stagger2 is null)
            return;
        _staggerPlayer?.Dispose();
        RestStagger();
        _staggerPlayer = StaggerMotion().Bind([_stagger0, _stagger1, _stagger2]);
    }

    void BindSpring()
    {
        if (_spring is null)
            return;
        _springPlayer?.Dispose();
        RestSpring();
        _springPlayer = SpringMotion().Bind(_spring);
    }

    static async Task Play(MotionPlayer? player, Action rest)
    {
        if (player is null)
            return;
        // Forward on a completed player does nothing. Reset arms another run.
        rest();
        player.Reset();
        await player.ForwardAsync();
    }

    void RestSequence()
    {
        if (_sequence is null)
            return;
        _sequence.Opacity = 0;
        _sequence.TranslationX = -48;
        _sequence.ScaleX = 1;
        _sequence.ScaleY = 1;
    }

    void RestOverlap()
    {
        if (_overlap is null)
            return;
        _overlap.TranslationX = 0;
        _overlap.Rotation = 0;
    }

    void RestOffset()
    {
        if (_offsetBox is not null)
            _offsetBox.TranslationX = 0;
        if (_offsetImage is null)
            return;
        _offsetImage.Rotation = 0;
    }

    void RestHold()
    {
        if (_hold is null)
            return;
        _hold.Opacity = 0;
        _hold.TranslationX = 0;
    }

    void RestYoyo()
    {
        if (_yoyo is null)
            return;
        _yoyo.TranslationX = 0;
    }

    void RestStagger()
    {
        RestChip(_stagger0);
        RestChip(_stagger1);
        RestChip(_stagger2);
    }

    static void RestChip(MauiControls.BoxView? box)
    {
        if (box is null)
            return;
        box.Opacity = 0;
        box.TranslationY = 16;
    }

    void RestSpring()
    {
        if (_spring is null)
            return;
        _spring.TranslationX = 0;
        _spring.ScaleX = 1;
        _spring.ScaleY = 1;
    }
}
