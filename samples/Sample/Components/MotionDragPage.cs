namespace Sample.Components;

/// <summary>
/// Five drag settles: a nearer pocket, a flick across a bar, a glide in an open field, a corner, and a size.
/// </summary>
sealed class MotionDragPage : Component
{
    const double Travel = 180;
    const double TravelY = 120;
    const double GrowScale = 1.6;
    const double Card = 56;
    const double GrowStage = Card * GrowScale;
    const double LaneWidth = Travel + Card;
    const double BoardWidth = Travel + Card;
    const double BoardHeight = TravelY + Card;

    // Soft on purpose. A flick keeps moving after the finger lifts.
    static readonly Spring ReleaseSpring = new(28, 12, 1);

    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Drag")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Title("Nearer pocket"),
                        Caption("Drop the card. It snaps into the nearer pocket."),
                        Lane(),
                        Rule(),
                        Title("Flick"),
                        Caption("Stay left of the bar and flick. The card reaches the far pocket. A slow release returns."),
                        Flick(),
                        Rule(),
                        Title("Glide"),
                        Caption("Drag the card anywhere in the field. It glides to a stop there."),
                        Coast(),
                        Rule(),
                        Title("Corner"),
                        Caption("Drag the card across the board. It snaps to the nearest corner."),
                        Board(),
                        Rule(),
                        Title("Size"),
                        Caption("Drag sideways. The card stays put and grows, then settles small or large."),
                        Grow()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 48)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    static MauiReactor.Label Title(string text)
        => Label(text)
            .FontSize(16)
            .HCenter();

    static MauiReactor.Label Caption(string text)
        => Label(text)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray);

    static MauiReactor.BoxView Rule()
        => BoxView()
            .HeightRequest(1)
            .HFill()
            .Margin(0, 8, 0, 8)
            .BackgroundColor(Colors.Gray.WithAlpha(0.35f));

    static MauiReactor.Grid Lane()
        => Grid("76", $"{LaneWidth}",
            Pocket(0, Colors.Teal),
            Pocket(Travel, Colors.Teal),
            CardView()
                .VCenter()
                .Drag(ReleaseSpring, plane => plane.X(
                    motion => motion.TranslateX(0, Travel).WithDuration(400).WithEasing(Easing.Linear)))
        )
        .HCenter();

    static MauiReactor.Grid Flick()
        => Grid("76", $"{LaneWidth}",
            BoxView()
                .WidthRequest(LaneWidth / 2)
                .HeightRequest(76)
                .BackgroundColor(Colors.Coral.WithAlpha(0.12f))
                .HEnd()
                .VCenter()
                .InputTransparent(inputTransparent: true),
            Pocket(Travel, Colors.Coral, 0.45f),
            BoxView()
                .WidthRequest(4)
                .HeightRequest(76)
                .BackgroundColor(Colors.Coral)
                .Margin(LaneWidth / 2 - 2, 0, 0, 0)
                .HStart()
                .VCenter()
                .InputTransparent(inputTransparent: true),
            CardView(Colors.Coral)
                .VCenter()
                .Drag(ReleaseSpring, plane => plane.X(
                    motion => motion.TranslateX(0, Travel).WithDuration(400).WithEasing(Easing.Linear)))
        )
        .HCenter();

    static MauiReactor.Grid Coast()
        => Grid("96", $"{LaneWidth}",
            BoxView()
                .CornerRadius(20)
                .BackgroundColor(Colors.DodgerBlue.WithAlpha(0.10f))
                .InputTransparent(inputTransparent: true),
            CardView(Colors.DodgerBlue)
                .VCenter()
                .Coast(ReleaseSpring, plane => plane.X(
                    motion => motion.TranslateX(0, Travel).WithDuration(400).WithEasing(Easing.Linear)))
        )
        .HCenter();

    static MauiReactor.Grid Board()
        => Grid(
                BoxView()
                    .CornerRadius(16)
                    .BackgroundColor(Colors.Gray.WithAlpha(0.12f))
                    .InputTransparent(inputTransparent: true),
                Dot(0, 0),
                Dot(Travel, 0),
                Dot(0, TravelY),
                Dot(Travel, TravelY),
                CardView()
                    .VStart()
                    .Drag(ReleaseSpring, plane => plane
                        .X(motion => motion.TranslateX(0, Travel).WithDuration(400).WithEasing(Easing.Linear))
                        .Y(motion => motion.TranslateY(0, TravelY).WithDuration(400).WithEasing(Easing.Linear)))
            )
            .WidthRequest(BoardWidth)
            .HeightRequest(BoardHeight)
            .HCenter();

    static MauiReactor.Grid Grow()
        => Grid($"{GrowStage}", $"{GrowStage}",
            BoxView()
                .CornerRadius(16)
                .BackgroundColor(Colors.MediumPurple.WithAlpha(0.16f))
                .InputTransparent(inputTransparent: true),
            CardView(Colors.MediumPurple)
                .HCenter()
                .VCenter()
                .Drag(ReleaseSpring, plane => plane.X(
                    motion => motion.Scale(1, GrowScale).WithDuration(400).WithEasing(Easing.Linear),
                    travel: GrowStage))
        )
        .HCenter();

    static MauiReactor.BoxView CardView()
        => CardView(Colors.Teal);

    static MauiReactor.BoxView CardView(Color color)
        => BoxView()
            .WidthRequest(Card)
            .HeightRequest(Card)
            .CornerRadius(12)
            .BackgroundColor(color)
            .HStart()
            // The pan sits on the wrapper. The card lets that touch through.
            .InputTransparent(inputTransparent: true);

    static MauiReactor.BoxView Pocket(double x, Color color, float alpha = 0.18f)
        => BoxView()
            .WidthRequest(Card)
            .HeightRequest(Card)
            .CornerRadius(12)
            .BackgroundColor(color.WithAlpha(alpha))
            .Margin(x, 0, 0, 0)
            .HStart()
            .VCenter()
            .InputTransparent(inputTransparent: true);

    static MauiReactor.BoxView Dot(double x, double y)
    {
        const double dot = 14;
        return BoxView()
            .WidthRequest(dot)
            .HeightRequest(dot)
            .CornerRadius(dot / 2)
            .BackgroundColor(Colors.Teal.WithAlpha(0.7f))
            .Margin(x + (Card / 2) - (dot / 2), y + (Card / 2) - (dot / 2), 0, 0)
            .HStart()
            .VStart()
            .InputTransparent(inputTransparent: true);
    }
}
