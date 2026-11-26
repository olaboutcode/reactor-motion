namespace Sample.Components;

/// <summary>
/// A card springs in while it is pressed. A second card also grows under the pointer.
/// </summary>
sealed class MotionPressPage : Component
{
    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("Press")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Caption("Press the card. It springs in, then springs back when you let go."),
                        PressCard(),
                        Caption("A mouse or trackpad grows the card on hover. A finger on a phone only presses it."),
                        HoverCard()
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 48)
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

    static VisualNode PressCard()
        => BoxView()
            .HeightRequest(96)
            .WidthRequest(96)
            .CornerRadius(16)
            .BackgroundColor(Colors.OrangeRed)
            .HCenter()
            .Press(m => m.Scale(1, 0.96).Rotate(90).WithSpring(Spring.Snappy));

    static VisualNode HoverCard()
        => BoxView()
            .HeightRequest(96)
            .WidthRequest(96)
            .CornerRadius(16)
            .BackgroundColor(Colors.Teal)
            .HCenter()
            .Press(
                pressed: m => m.Scale(1, 0.96).Rotate(180).WithSpring(Spring.Snappy),
                hover: m => m.Scale(1, 1.03).Rotate(-180).WithSpring(Spring.Gentle));
}
