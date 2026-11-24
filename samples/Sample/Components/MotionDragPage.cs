namespace Sample.Components;

/// <summary>
/// Drag along a lane, across a board onto a corner, or sideways to grow a card. Release keeps the drag speed.
/// </summary>
sealed class MotionDragPage : Component
{
    const double Travel = 180;
    const double TravelY = 120;
    const double GrowTravel = 140;
    const double GrowScale = 1.6;
    const double Card = 56;
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
                        Caption("Drag the card along the lane. Let go and it springs to the nearer end, still moving at your speed."),
                        Lane(),
                        Caption("Drag the card anywhere in the board. Let go and it springs to the nearest corner, still moving at your speed."),
                        Board(),
                        Caption("Drag the card sideways. Let go and it springs to small or large, still moving at your speed."),
                        Grow()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 32)
                )
                .GridRow(1)
            )
        )
        .HideNavigationBar();

    static MauiReactor.Label Caption(string text)
        => Label(text)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray);

    static MauiReactor.Grid Lane()
        => Grid("76", $"{LaneWidth}",
            BoxView()
                .HeightRequest(2)
                .BackgroundColor(Colors.Gray.WithAlpha(0.35f))
                .VCenter(),
            BoxView()
                .WidthRequest(Card)
                .HeightRequest(Card)
                .CornerRadius(12)
                .BackgroundColor(Colors.Teal.WithAlpha(0.18f))
                .Margin(Travel, 0, 0, 0)
                .HStart()
                .VCenter(),
            CardView()
                .VCenter()
                .Drag(ReleaseSpring, plane => plane.X(
                    motion => motion.TranslateX(0, Travel).WithDuration(400).WithEasing(Easing.Linear)))
        )
        .HCenter();

    static MauiReactor.Grid Board()
        => Grid(
                Mark(0, 0),
                Mark(Travel, 0),
                Mark(0, TravelY),
                Mark(Travel, TravelY),
                CardView()
                    .VStart()
                    .Drag(ReleaseSpring, plane => plane
                        .X(motion => motion.TranslateX(0, Travel).WithDuration(400).WithEasing(Easing.Linear))
                        .Y(motion => motion.TranslateY(0, TravelY).WithDuration(400).WithEasing(Easing.Linear)))
            )
            .WidthRequest(BoardWidth)
            .HeightRequest(BoardHeight)
            .BackgroundColor(Colors.Gray.WithAlpha(0.12f))
            .HCenter();

    static MauiReactor.Grid Grow()
        => Grid($"{Card * GrowScale}", $"{GrowTravel * 2}",
            BoxView()
                .WidthRequest(Card * GrowScale)
                .HeightRequest(Card * GrowScale)
                .CornerRadius(12)
                .BackgroundColor(Colors.MediumPurple.WithAlpha(0.18f))
                .HCenter()
                .VCenter()
                .InputTransparent(inputTransparent: true),
            CardView(Colors.MediumPurple)
                .HCenter()
                .VCenter()
                .Drag(ReleaseSpring, plane => plane.X(
                    motion => motion.Scale(1, GrowScale).WithDuration(400).WithEasing(Easing.Linear),
                    travel: GrowTravel))
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

    static MauiReactor.BoxView Mark(double x, double y)
        => BoxView()
            .WidthRequest(Card)
            .HeightRequest(Card)
            .CornerRadius(12)
            .BackgroundColor(Colors.Teal.WithAlpha(0.18f))
            .Margin(x, y, 0, 0)
            .HStart()
            .VStart()
            .InputTransparent(inputTransparent: true);
}
