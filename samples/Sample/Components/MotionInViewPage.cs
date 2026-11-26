namespace Sample.Components;

/// <summary>
/// A card below the fold fades and slides in once it is halfway on screen.
/// </summary>
sealed class MotionInViewPage : Component
{
    public override VisualNode Render()
        => ContentPage(
            Grid("auto,*", "*",
                NavigationBar
                    .BackNavigation("In view")
                    .GridRow(0),
                ScrollView(
                    VStack(
                        Label("Scroll until the card is halfway in view. It plays once.")
                            .FontSize(14)
                            .HCenter()
                            .HorizontalTextAlignment(TextAlignment.Center)
                            .TextColor(Colors.Gray),
                        BoxView()
                            .HeightRequest(840)
                            .BackgroundColor(Colors.Transparent),
                        BoxView()
                            .HeightRequest(96)
                            .WidthRequest(96)
                            .CornerRadius(16)
                            .BackgroundColor(Colors.MediumSeaGreen)
                            .HCenter()
                            .InView(m => m
                                .FadeIn()
                                .SlideIn(SlideFrom.Bottom, 124)
                                .Rotate(360)
                                .WithDuration(420))
                    )
                    .Spacing(16)
                    .Padding(24, 24, 24, 48)
                )
                .GridRow(1)
                .VerticalScrollBarVisibility(ScrollBarVisibility.Never)
            )
        )
        .HideNavigationBar();
}
