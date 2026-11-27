namespace Sample.Components;

/// <summary>
/// Two fingers scale a card and turn it. Letting go leaves the pose.
/// </summary>
sealed class MotionPinchPage : Component
{
    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Pinch")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Caption("Pinch with two fingers. A wider span grows the card. A turn follows your fingers. Letting go leaves it."),
                        Photo()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 48)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();

    static VisualNode Photo()
        => Border(
            Label("Photo")
                .FontSize(16)
                .TextColor(Colors.White)
                .HCenter()
                .VCenter()
        )
        .WidthRequest(160)
        .HeightRequest(160)
        .StrokeCornerRadius(18)
        .Stroke(Colors.Transparent)
        .BackgroundColor(Colors.Coral)
        .HCenter()
        .Pinch(
            scale: motion => motion.Scale(1, 2.5).WithDuration(400).WithEasing(Easing.Linear),
            rotate: motion => motion.Rotate(-30, 30).WithDuration(400).WithEasing(Easing.Linear));

    static MauiReactor.Label Caption(string text)
        => Label(text)
            .FontSize(14)
            .HCenter()
            .HorizontalTextAlignment(TextAlignment.Center)
            .TextColor(Colors.Gray);
}
