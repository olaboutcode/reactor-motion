namespace Sample.Components;

/// <summary>
/// Two cards below the fold fade and slide in once they are halfway on screen.
/// The green card plays once. The coral card plays again each time it comes back.
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
                        Label("Scroll until a card is halfway in view. The green card plays once. The coral card plays again each time it comes back.")
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
                                .WithDuration(420)),
                        BoxView()
                            .HeightRequest(840)
                            .BackgroundColor(Colors.Transparent),
                        BoxView()
                            .HeightRequest(96)
                            .WidthRequest(96)
                            .CornerRadius(16)
                            .BackgroundColor(Colors.Coral)
                            .HCenter()
                            .InView(
                                m => m
                                    .FadeIn()
                                    .SlideIn(SlideFrom.Bottom, 124)
                                    .Rotate(360)
                                    .WithDuration(420),
                                replay: true)
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
