namespace Sample.Components;

/// <summary>
/// A label names each stage of a yoyo, a repeat, and a press.
/// </summary>
sealed class MotionStagePage : Component
{
    const double Card = 64;

    MauiControls.BoxView? _trip;
    MauiControls.Label? _tripNow;
    MauiControls.Label? _tripLog;
    string _tripLines = "";

    MauiControls.BoxView? _twice;
    MauiControls.Label? _twiceNow;
    MauiControls.Label? _twiceLog;
    string _twiceLines = "";

    MauiControls.Label? _pressNow;
    MauiControls.Label? _pressLog;
    string _pressLines = "";

    MotionPlayer? _tripPlayer;
    MotionPlayer? _twicePlayer;

    protected override void OnWillUnmount()
    {
        _tripPlayer?.Dispose();
        _twicePlayer?.Dispose();
        _tripPlayer = null;
        _twicePlayer = null;
        base.OnWillUnmount();
    }

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Stages")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Caption("The label uses the callback name for the stage the recipe just reached."),
                        Trip(),
                        Rule(),
                        Twice(),
                        Rule(),
                        Pressed()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 48)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    MauiReactor.VStack Trip()
        => VStack(
            Label("Out and back")
                .FontSize(16)
                .HCenter(),
            Caption("The card waits, then slides out and back twice."),
            Lane(Colors.Teal, box => _trip = box, BindTrip),
            Now(label => _tripNow = label, "Waiting"),
            Lines(label => _tripLog = label),
            Button("Play", () => _ = Play(_tripPlayer, RestTrip))
                .HCenter()
        )
        .Spacing(12);

    MauiReactor.VStack Twice()
        => VStack(
            Label("Twice")
                .FontSize(16)
                .HCenter(),
            Caption("The fade plays, jumps back, and plays again."),
            Chip(Colors.Coral, box => _twice = box, BindTwice),
            Now(label => _twiceNow = label, "Waiting"),
            Lines(label => _twiceLog = label),
            Button("Play", () => _ = Play(_twicePlayer, RestTwice))
                .HCenter()
        )
        .Spacing(12);

    MauiReactor.VStack Pressed()
        => VStack(
            Label("Press")
                .FontSize(16)
                .HCenter(),
            Caption("Press the card. The label says Forward, Reversing, then Reverse completed."),
            BoxView()
                .WidthRequest(Card)
                .HeightRequest(Card)
                .CornerRadius(14)
                .BackgroundColor(Colors.MediumPurple)
                .HCenter()
                .Press(m => m
                    .Scale(1, 0.9)
                    .WithDuration(1400)
                    .WithEasing(Easing.CubicOut)
                    .OnBegin(ClearPress)
                    .OnReverseStart(() => Say(_pressNow, _pressLog, ref _pressLines, "Reversing"))
                    .OnReverseComplete(() => Say(_pressNow, _pressLog, ref _pressLines, "Reverse completed"))),
            Now(label => _pressNow = label, "Waiting"),
            Lines(label => _pressLog = label)
        )
        .Spacing(12);

    Motion TripMotion()
        => Motion.Timeline(duration: 1600, easing: Easing.CubicInOut, delay: 500, repeat: 2, yoyo: true)
            .Add(m => m.TranslateX(0, 96))
            .OnBegin(() => Say(_tripNow, _tripLog, ref _tripLines, "Forward"))
            .OnReverseStart(() => Say(_tripNow, _tripLog, ref _tripLines, "Reversing"))
            .OnRepeat(() => Say(_tripNow, _tripLog, ref _tripLines, "Repeat"))
            .OnReverseComplete(() => Say(_tripNow, _tripLog, ref _tripLines, "Reverse completed"));

    Motion TwiceMotion()
        => Motion.Timeline(duration: 1600, easing: Easing.CubicOut, repeat: 2)
            .Add(m => m.Opacity(1, 0.25))
            .OnBegin(() => Say(_twiceNow, _twiceLog, ref _twiceLines, "Forward"))
            .OnRepeat(() => Say(_twiceNow, _twiceLog, ref _twiceLines, "Repeat"))
            .OnComplete(() => Say(_twiceNow, _twiceLog, ref _twiceLines, "Completed"));

    void BindTrip()
    {
        if (_trip is null)
            return;
        _tripPlayer?.Dispose();
        RestTrip();
        _tripPlayer = TripMotion().Bind(_trip);
    }

    void BindTwice()
    {
        if (_twice is null)
            return;
        _twicePlayer?.Dispose();
        RestTwice();
        _twicePlayer = TwiceMotion().Bind(_twice);
    }

    void ClearPress()
    {
        _pressLines = "";
        Say(_pressNow, _pressLog, ref _pressLines, "Forward");
    }

    void RestTrip()
    {
        if (_trip is not null)
            _trip.TranslationX = 0;
        _tripLines = "";
        Show(_tripNow, "Waiting");
        Show(_tripLog, "");
    }

    void RestTwice()
    {
        if (_twice is not null)
            _twice.Opacity = 1;
        _twiceLines = "";
        Show(_twiceNow, "Waiting");
        Show(_twiceLog, "");
    }

    static void Say(MauiControls.Label? now, MauiControls.Label? log, ref string lines, string line)
    {
        Show(now, line);
        lines = lines.Length == 0 ? line : lines + "\n" + line;
        Show(log, lines);
    }

    static void Show(MauiControls.Label? label, string text)
    {
        if (label is null)
            return;
        label.Text = text;
    }

    static async Task Play(MotionPlayer? player, Action rest)
    {
        if (player is null)
            return;
        rest();
        player.Reset();
        await player.ForwardAsync();
    }

    static MauiReactor.Label Caption(string text)
        => Label(text)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray);

    static MauiReactor.Label Now(Action<MauiControls.Label?> capture, string text)
        => Label(capture)
            .Text(text)
            .FontSize(22)
            .HCenter();

    static MauiReactor.Label Lines(Action<MauiControls.Label?> capture)
        => Label(capture)
            .Text(string.Empty)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray)
            .HeightRequest(120);

    static MauiReactor.BoxView Rule()
        => BoxView()
            .HeightRequest(1)
            .HFill()
            .Margin(0, 8, 0, 8)
            .BackgroundColor(Colors.Gray.WithAlpha(0.35f));

    static MauiReactor.Grid Lane(Color color, Action<MauiControls.BoxView?> capture, Action loaded)
        => Grid("100", "160",
            BoxView()
                .HeightRequest(2)
                .BackgroundColor(Colors.Gray.WithAlpha(0.35f))
                .VCenter(),
            BoxView(capture)
                .WidthRequest(Card)
                .HeightRequest(Card)
                .CornerRadius(14)
                .BackgroundColor(color)
                .VCenter()
                .HStart()
                .OnLoaded(loaded))
            .HCenter();

    static MauiReactor.BoxView Chip(Color color, Action<MauiControls.BoxView?> capture, Action loaded)
        => BoxView(capture)
            .WidthRequest(Card)
            .HeightRequest(Card)
            .CornerRadius(14)
            .BackgroundColor(color)
            .HCenter()
            .OnLoaded(loaded);
}
