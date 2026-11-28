# Reactor.Motion

[![CI](https://github.com/olaboutcode/reactor-motion/actions/workflows/ci.yml/badge.svg)](https://github.com/olaboutcode/reactor-motion/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Reactor.Motion.svg)](https://www.nuget.org/packages/Reactor.Motion/)

In-page view motion for [MauiReactor](https://github.com/adospace/reactorui-maui). A recipe can fade, slide, scale, stagger, follow keyframes or a timeline, spring, walk a path, write a matrix, or enter and exit while the view stays until the exit finishes. A keyed collection slides its items when they are inserted, removed, or reordered.

> [!WARNING]
> The library is still experimental, use at your own risk. The performance seems decent and all the features should be working as expected. However, expect breaking changes as I work on the API.

## Table of contents

- [Overview](#overview)
- [Getting started](#getting-started)
  - [Install](#install)
  - [Example](#example)
  - [Namespaces](#namespaces)
- [Motion](#motion)
  - [Recipes](#recipes)
  - [Property tracks](#property-tracks)
    - [Opacity](#opacity)
    - [Translation](#translation)
    - [Anchor](#anchor)
    - [Scale](#scale)
    - [Rotation](#rotation)
    - [Depth](#depth)
    - [Matrix](#matrix)
    - [Color](#color)
    - [Size](#size)
    - [Corner radius](#corner-radius)
    - [Any property](#any-property)
  - [Named recipes](#named-recipes)
  - [Timing](#timing)
  - [Color space](#color-space)
  - [Bind and play](#bind-and-play)
  - [BindMotion](#bindmotion)
  - [Enter and exit](#enter-and-exit)
  - [In view](#in-view)
  - [Scroll scrub](#scroll-scrub)
  - [Layout](#layout)
  - [Collection](#collection)
  - [MotionPlayer](#motionplayer)
    - [Forward and reverse](#forward-and-reverse)
    - [Pause resume and reset](#pause-resume-and-reset)
    - [Status](#status)
    - [Player events](#player-events)
    - [Recipe callbacks](#recipe-callbacks)
  - [Seek](#seek)
  - [Drag](#drag)
  - [Pinch](#pinch)
  - [Press](#press)
  - [Repeat and yoyo](#repeat-and-yoyo)
  - [Parallel merge](#parallel-merge)
  - [Delay](#delay)
  - [Stagger](#stagger)
  - [Keyframes](#keyframes)
  - [Timeline](#timeline)
  - [Springs](#springs)
  - [Poses](#poses)
  - [Path](#path)
  - [One player per property](#one-player-per-property)
- [Samples](#samples)
- [Building](#building)
- [License](#license)

## Overview

`Motion` is an immutable recipe with no targets. `MotionPlayer` binds that recipe to views and can run it forward, in reverse, paused, repeated, staggered, sprung, or scrubbed.

Fade, slide, and scale live on `Motion`. Give each property one writer. A `MotionPlayer` and MauiReactor `WithAnimation` on the same property of the same view fight each other.

Motion defaults to **300 ms** and **`Easing.CubicOut`**, and can wait before its tracks move.

## Getting started

Reactor.Motion is meant to work for a MauiReactor app.

### Install

1. Follow the [MauiReactor instructions](https://github.com/adospace/reactorui-maui#setting-up-mauireactor-from-cli) to create a MauiReactor app.
2. Install Reactor.Motion.

```
dotnet add package Reactor.Motion --prerelease
```

The package id is `Reactor.Motion`. The library targets .NET 10 and .NET MAUI.

### Example

Bind a recipe with `Motion` and play it from the player.

```csharp
sealed class MotionAnimation : Component
{
    private MotionPlayer? _player;

    public override VisualNode Render()
        => ContentPage("Counter Sample",
            VStack(
                Label("This is awesome!")
                    .BindMotion(
                        Motion.Define(
                            m => m
                                .RotateX(180)
                                .WithDuration(500)
                                .WithEasing(Easing.CubicInOut)
                        ),
                        p => _player = p
                    ),

                HStack(
                    Button("Play Text Animation")
                        .OnClicked(async () => await _player.ForwardAsync()),
                    Button("Reverse Text Animation")
                        .OnClicked(async () => await _player.ReverseAsync())
                )
                .Spacing(10)
            )
            .Spacing(30)
            .Center()
        );
}
```

### Namespaces

Everything you call from an app is in `Reactor.Motion`.

```csharp
global using Reactor.Motion;
```

| Type | Role |
|---|---|
| `Motion` | Recipe, plus static `Define`, `Bind`, `Play`, `ForwardAsync`, `ReverseAsync`, `Drag`, and `Collection`. |
| `MotionPlayer` | Playback. |
| `MotionPlaybackEventArgs` | Player events. State is `IsDismissed()`, `IsForward()`, `IsReverse()`, `IsPaused()`, and `IsCompleted()`. |
| `Spring`, `Stagger`, `StaggerFrom`, `SlideFrom`, `ColorSpace`, `KeyframeBuilder<T>`, `Matrix4` | Recipe values. |
| `MotionCurve` | `CubicBezier` easing. |
| `MotionPreferences` | App-wide reduce-motion override. |
| `VisualNodeExtensions` | `.BindMotion()`, `.EnterExit()`, `.InView()`, `.Scrub()`, `.Press()`, `.Layout()`, `.Drag()`, `.Pinch()`. |
| `CollectionExtensions` | `.Collection()` on a keyed collection. |
| `MotionPlayerExtensions` | Null-safe `Forward` and `Reverse` on `MotionPlayer?`. |

The snippets below assume that using, plus `MauiReactor`.

A `Motion` is an immutable recipe. It stores tracks, timing, and playback options. It does not store views. Bind it when you play it.

```csharp
static readonly Motion Pulse = Motion.Define(m => m
    .Scale(1, 1.08)
    .WithDuration(180));
```

`Motion.Define` starts from `Motion.None` and returns whatever the function returns. `Motion.None` is 300 ms, `Easing.CubicOut`, one play, HSV color, no spring, no stagger, no perspective.

### Recipes

Tracks write bindable properties. A to-only call captures the live value as `from` on the first `Forward`. A from/to call writes both ends, and the first `Forward` applies `from` before the clock starts. A third overload takes keyframes. See [Keyframes](#keyframes).

```csharp
Motion.Define(m => m
    .FadeIn()
    .Scale(0.9, 1)
    .WithDuration(280));
```

`Motion.Translate` and `Motion.Rotate` write view translation and in-plane rotation.

### Property tracks

#### Opacity

```csharp
m.Opacity(1)            // from the live value to 1
m.Opacity(0, 1)         // from 0 to 1
m.Opacity(k => k.At(0, 0).At(0.35, 1).At(1, 1))
```

Writes `VisualElement.Opacity`.

#### Translation

```csharp
m.Translate(40, 0)                 // TranslationX 40, TranslationY 0
m.Translate(-40, 0, 0, 0)          // from (-40, 0) to (0, 0)
m.TranslateX(-100, 0)
m.TranslateY(24)
m.TranslateX(k => k.At(0, -40).At(1, 0))
```

Values are device-independent pixels. `Translate(x, y)` is `TranslateX` then `TranslateY`.

#### Anchor

```csharp
m.AnchorX(0.5, 0)
m.AnchorY(1, 0.5)
m.AnchorX(k => k.At(0, 0.5).At(1, 0))
```

`AnchorX` and `AnchorY` are the pivot, from 0 to 1. The default is the center, `0.5`. `RotateX` and `RotateY` turn around that point. An anchor track can share a recipe with translation, rotation, and scale.

#### Scale

```csharp
m.Scale(1.08)
m.Scale(0.85, 1)
m.ScaleX(1, 1.2)
m.ScaleY(1, 0.8)
m.Scale(k => k.At(0, 0.8).At(0.6, 1.08).At(1, 1))
```

`Scale` writes `ScaleX` and `ScaleY`. It does not write `Scale`.

#### Rotation

```csharp
m.Rotate(180)
m.Rotate(0, 180)
m.Rotate(k => k.At(0, 0).At(1, 360))
```

Degrees, clockwise, `VisualElement.Rotation`. This is the in-plane spin.

#### Depth

`RotateX` and `RotateY` are degrees about the horizontal and vertical axes, around `AnchorX` and `AnchorY` (defaults `0.5, 0.5`).

`Perspective(entry)` sets the eye distance for that rotation. `entry` is the same number Flutter writes with `Matrix4.setEntry(3, 2, entry)`. The camera sits `1 / |entry|` device-independent pixels from the plane. `0.001` shrinks the far edge a little. `0` is orthographic: both edges stay the same height. `Perspective` is a constant on the recipe. It is not a track, and it does not animate by itself.

```csharp
Motion.None
    .Perspective(0.004)
    .RotateY(0, 60)
    .WithDuration(400)
```

Call `Perspective` together with `RotateX` or `RotateY`. On iOS and Mac Catalyst the recipe writes `CATransform3D.m34 = -entry` after the platform builds its transform, using the same anchor, translation, rotation, and scale composition as MAUI. On Android the same eye distance is passed to `CameraDistance`, scaled by `density² × √5`, so the foreshortening matches that entry. An entry of `0` parks the Android camera far enough that the plane does not foreshorten.

On Windows, a recipe that sets `Perspective` writes one pose matrix to the composition visual. That matrix carries translation, rotation, scale, and the perspective entry, so those channels are not also written as bindable properties.

A later player that takes translation, rotation, or scale replaces that pose. A player that writes opacity or color leaves it alone. `And` keeps the right-hand entry when the right-hand motion has one.

#### Matrix

`Transform` takes the eased progress of the track, from 0 to 1, and returns one matrix. That matrix is the translation, rotation, scale, and perspective for the frame. The callback applies `t`. The player does not blend the 16 entries. Reverse decreases `t`. Reset writes the matrix at 0.

```csharp
Motion.None
    .Transform(t => Matrix4.Identity
        .SetEntry(3, 2, 0.002)
        .RotateY(t * 2 * Math.PI)
        .Translate(0, t * 50))
    .WithDuration(900)
```

`Matrix4.Identity` is a new matrix each time. `SetEntry(3, 2, entry)` is the same perspective term as `Perspective(entry)`. `RotateX`, `RotateY`, and `RotateZ` take radians. `Translate` is device-independent pixels. `Scale(factor)` scales both axes. Each call post-multiplies, so in the snippet the point is translated, then rotated about Y, then perspective-divided.

The pivot is the view's `AnchorX` and `AnchorY`, default center. On iOS and Mac Catalyst the 4×4 is written to the layer, with entry `(3, 2)` stored as `CATransform3D.m34 = -entry`. On Android the same frame sets camera distance from the perspective row and writes translation, rotation, and scale. On Windows the 4×4 is written to the composition visual.

`Transform` replaces `Translate`, `Rotate`, `RotateX`, `RotateY`, `Scale`, `Path`, and `Perspective` on that recipe. Opacity, color, and size can share it.

#### Color

```csharp
m.BackgroundColor(Colors.Lime)
m.BackgroundColor(Colors.Red, Colors.Lime)
m.BackgroundColor(k => k.At(0, Colors.Red).At(1, Colors.Lime))
```

Writes `VisualElement.BackgroundColor`. Interpolation is HSV by default. See [Color space](#color-space).

#### Size

```csharp
m.Width(120)
m.Width(72, 180)
m.Height(72, 180)
m.Height(k => k.At(0, 72).At(1, 180))
```

Writes `WidthRequest` and `HeightRequest`.

#### Corner radius

```csharp
m.CornerRadius(24)
m.CornerRadius(new CornerRadius(12), new CornerRadius(24))
m.CornerRadius(k => k.At(0, 12).At(1, 24))
```

On a `BoxView` this writes `CornerRadius`. On a `Border` it writes `StrokeShape` as a `RoundRectangle`. Other target types are skipped. A debug build logs the skip.

#### Any property

```csharp
m.Property(VisualElement.OpacityProperty, 1)
m.Property(VisualElement.RotationProperty, 0d, 45d)
```

`Property` adds a track for a `BindableProperty` whose type is `double`, `float`, `Color`, `Thickness`, `CornerRadius`, or `Rect`, and for a `Border` stroke shape. Pass `from` when the start value must be explicit. Leave it out to capture the live value on the first `Forward`.

### Named recipes

The named helpers are ordinary tracks with a preset from and to.

| Member | Writes |
|---|---|
| `FadeIn()` | Opacity 0 → 1 |
| `FadeOut()` | Opacity 1 → 0 |
| `SlideIn(SlideFrom edge, double distance = 24)` | Translation from off-screen to 0 |
| `SlideOut(SlideFrom edge, double distance = 24)` | Translation from 0 to off-screen |
| `ScaleIn(double from = 0.85)` | Scale `from` → 1 |
| `ScaleOut(double to = 0.85)` | Scale 1 → `to` |

```csharp
m.FadeIn().SlideIn(SlideFrom.Left, 40)
m.SlideOut(SlideFrom.Bottom)
m.ScaleIn()
```

`SlideFrom` is `Left`, `Right`, `Top`, or `Bottom`. `Left` and `Top` use a negative translation. `Right` and `Bottom` use a positive one. The distance is in device-independent pixels.

`SlideIn` moves to translation 0. Park the view on that off-screen translation before the first forward when that offset is the from-value.

### Timing

```csharp
m.WithDuration(280).WithEasing(Easing.CubicOut)
```

| Member | Role |
|---|---|
| `Duration` | Length in milliseconds. Default 300. Does not include `Delay`. |
| `Delay` | Hold before the tracks move, in milliseconds. Default 0. |
| `Easing` | Default `Easing.CubicOut`. |
| `WithDuration(uint milliseconds)` | Sets the length. Does not clear a delay. |
| `WithDelay(uint milliseconds)` | Sets the hold. Does not change the length. `0` clears a delay. |
| `WithEasing(Easing easing)` | Sets the easing. `null` throws. |
| `WithRate(double rate)` | Multiplies every finite tick, including the delay hold. `1` is normal. `0` holds the current frame. A negative, NaN, or infinite value becomes `0`. |
| `WithReduceMotion(bool enabled)` | Writes the end pose and completes once. Wins over `MotionPreferences.ReduceMotion`. |
| `MotionCurve.CubicBezier(x1, y1, x2, y2)` | CSS cubic-bezier easing. X controls are clamped to 0–1. Y may leave that range. |

```csharp
m.FadeIn().WithDuration(280).WithEasing(MotionCurve.CubicBezier(0.2, 0.8, 0.2, 1))
m.Opacity(0, 1).WithDuration(280).WithRate(1.5)
m.FadeIn().WithReduceMotion(true)
```

`MotionPreferences.ReduceMotion` is the app-wide switch. `null` reads the platform: Reduce Motion on iOS and Mac Catalyst, animator duration scale `0` on Android, and animations disabled on Windows. A recipe's `WithReduceMotion` wins. Playback jumps to the end of that run and does not continue a repeat or a yoyo.

Live players share one 16 ms clock. A manual clock, used by tests, stays on `Tick`.

On a recipe whose tracks all span the full motion, `WithDuration` replaces the length. Keyframe offsets are fractions of that length, so they stretch with it.

On a recipe built with `Motion.Timeline`, `WithDuration` gives that length to every beat that did not call `WithDuration` itself, then rebuilds the windows. A beat that set its own length keeps it. `WithEasing` is the curve for every beat that did not call `WithEasing`. One beat still replaces its length, including a second `WithDuration` on that same beat.

On any other recipe that already has windows, `WithDuration` only grows the clock. A shorter value is ignored. Existing windows stay the same number of milliseconds and the extra time is padding at the end.

`WithDuration` and `WithSpring` are mutually exclusive. A debug build asserts if you call `WithDuration` on a spring recipe. Playback follows the spring until rest. See [Springs](#springs).

### Color space

```csharp
public enum ColorSpace
{
    Hsv,
    Rgb,
}
```

```csharp
m.BackgroundColor(Colors.Red, Colors.Lime)                         // HSV
m.BackgroundColor(Colors.Red, Colors.Lime).WithColorSpace(ColorSpace.Rgb)
```

| Member | Role |
|---|---|
| `WithColorSpace(ColorSpace colorSpace)` | Selects how `Color` tracks blend. Default `ColorSpace.Hsv`. |

HSV interpolates hue along the short arc. Saturation and value blend in that space. Alpha stays linear. A color with saturation below `1e-6` keeps the other stop's hue, so a grey does not spin the hue channel.

RGB blends each channel, including alpha, in a straight line. Red to lime passes through the middle of the cube.

`And` takes the right-hand color space.

### Bind and play

`Define`, `Bind`, `Play`, `ForwardAsync`, and `ReverseAsync` are static methods on `Motion`. The player type is `MotionPlayer`.

```csharp
MotionPlayer player = Motion.Bind(
    Motion.Define(m => m.FadeIn().WithDuration(280)),
    view);

player.Forward();
```

| Member | Role |
|---|---|
| `Define(Func<Motion, Motion> configure)` | `configure(Motion.None)`. |
| `Timeline(uint? duration = null, Easing? easing = null, uint delay = 0, ...)` | Empty timeline. Duration and easing are defaults for beats that did not set their own. |
| `Timeline(Func<Motion, Motion> configure, string? id = null)` | First beat of a timeline. `id` names it for `Seek`. |
| `Bind(Motion motion, VisualElement target)` | Bind one view. Does not start. |
| `Bind(Motion motion, IEnumerable<VisualElement> targets)` | Bind many views. Does not start. The order is the stagger order. |
| `Bind(Func<Motion, Motion> configure, VisualElement target)` | Define, then bind. |
| `Bind(Func<Motion, Motion> configure, IEnumerable<VisualElement> targets)` | Define, then bind. |
| `Play(Motion motion, params VisualElement[] targets)` | Bind and start forward. Returns the player. |
| `Play(Func<Motion, Motion> configure, VisualElement target)` | Define, bind, start. |
| `Play(Func<Motion, Motion> configure, IEnumerable<VisualElement> targets)` | Define, bind, start. |
| `ForwardAsync(Motion / configure, target / targets, CancellationToken cancellationToken = default)` | Bind and play forward. Returns the `Task` only. |
| `ReverseAsync(Motion / configure, target / targets, CancellationToken cancellationToken = default)` | Bind and play reverse. Returns the `Task` only. |

`Motion` has the same `Bind` overloads as instance methods:

```csharp
var player = Motion.None.FadeIn().WithDuration(280).Bind(view);
```

`Play` is bind, then `Forward()`, and you keep the player so you can pause or reverse it later.

```csharp
_player = Motion.Play(m => m.Scale(1, 1.08).WithDuration(180), card);
```

`ForwardAsync` and `ReverseAsync` also bind and start, and the only handle you get back is the `Task`. Use `Bind` or `Play` when you need the player. A later `Bind` or `Play` that writes a property this run writes takes that property. See [One player per property](#one-player-per-property).

Those methods accept a `CancellationToken`. Cancellation does not throw out of these methods. See [Forward and reverse](#forward-and-reverse).

### BindMotion

```csharp
Label("Hello")
    .BindMotion(
        Motion.Define(m => m.RotateY(0, 180).Perspective(0.004).WithDuration(500)),
        player => _player = player)
```

| Member | Role |
|---|---|
| `VisualNode.BindMotion(Motion motion, Action<MotionPlayer>? onBind = null)` | Binds on `Loaded`. Disposes the player on `Unloaded`. Does not start, whether or not you pass `onBind`. |

`onBind` is where you keep the player and subscribe to it. Call `Forward()` yourself.

The extension wraps the node in a `Grid`. When that grid has a single visual child, and that child is itself a single-child layout, the bind walks to the innermost visual and targets that. A layout with several visual children is the target itself. Put `BindMotion` on the view you want to move.

```csharp
Button("Pulse")
    .BindMotion(Pulse, player => _pulse = player)
    .OnTapped(() =>
    {
        if (_pulse is null)
            return;

        if (_pulse.IsCompleted())
            _pulse.Reverse();
        else
            _pulse.Forward();
    });
```

Dispose is automatic when the node unloads. If you also hold the player, drop your field in `OnWillUnmount` so you do not call a disposed player later. The null-safe extensions make that call a no-op. See below.

### Enter and exit

`BindMotion` never plays, and it disposes the player when the node unloads. A child the parent stops rendering is already gone before an exit can run. `EnterExit` is the presence wrapper: the parent keeps rendering it and passes `present`.

```csharp
Banner()
    .EnterExit(
        State.Show,
        enter: m => m.FadeIn().SlideIn(SlideFrom.Bottom, 24).WithDuration(420),
        exit: m => m.FadeOut().SlideOut(SlideFrom.Bottom, 16).WithDuration(320))
```

| Member | Role |
|---|---|
| `VisualNode.EnterExit(bool present, Motion enter, Motion exit)` | Plays `enter` when the child appears, including the first time `present` is true. Plays `exit` when `present` becomes false and keeps the child until that run finishes. |
| `VisualNode.EnterExit(bool present, Func<Motion, Motion> enter, Func<Motion, Motion> exit)` | Same. Each function receives an empty motion. |

Keep rendering `EnterExit` on every pass. If the parent omits the node, MauiReactor unmounts it immediately and the exit does not run.

The exit run is one forward play. When it finishes and `present` is still false, the child leaves the tree. If `present` becomes true during that exit, the exit reverses back to its from pose and the child stays. A later hide plays the same exit forward from where it stopped.

`WithPose` on the exit reads each property's from-value from the view and keeps the exit's `to`, so a hide in the middle of an enter leaves from the current opacity and translation. A spring that is still moving hands its velocity to that exit. See [Poses](#poses).

```csharp
exit: m => m.FadeOut().SlideOut(SlideFrom.Bottom, 16).WithPose()
```

An infinite `Repeat` on the exit never finishes, so the child never unmounts. Give each `EnterExit` its own key when one parent renders more than one. The wrapper is a `Grid` and uses the same target walk as `BindMotion`.

### In view

`EnterExit` plays when `present` changes. A view that is already mounted below the fold never gets that signal. `InView` watches the nearest `ScrollView`.

```csharp
Card()
    .InView(m => m.FadeIn().SlideIn(SlideFrom.Bottom, 24).WithDuration(420));

Card().InView(
    m => m.FadeIn().SlideIn(SlideFrom.Bottom, 24).WithDuration(420),
    replay: true);
```

It starts when the overlap reaches half the child, or half the viewport when the child is larger than the viewport. A child already that far in view on load plays immediately. A child with no `ScrollView` ancestor plays on load, and still plays once when `replay` is set.

Omit `replay` and scrolling away does not play again. `replay: true` arms when the overlap falls back below that halfway mark. The pose stays where the play left it. The next crossing seeks the from pose and plays forward. The motion does not restart while the child stays past the mark. Leaving the viewport does not reverse the motion. A different exit on the way out is a separate recipe.

On load the from pose is written, so a fade sitting below the fold starts at its from value. Reduce motion finishes when that play starts, including each later entry. The wrapper is a `Grid` and uses the same target walk as `BindMotion`.

| Member | Role |
|---|---|
| `VisualNode.InView(Motion motion, bool replay = false)` | Plays `motion` once the child has scrolled halfway into view. `replay` plays again on the next entry. |
| `VisualNode.InView(Func<Motion, Motion> motion, bool replay = false)` | Same. The function receives an empty motion. |

### Scroll scrub

`SeekFraction` parks a player and does not start the clock. `Scrub` feeds that seek from the nearest `ScrollView`. `InView` plays when the child appears, and again after it scrolls away when `replay` is set. `Scrub` keeps tracking the scroll. Put them on different views when a page wants both.

```csharp
Card()
    .Scrub(m => m.Opacity(1, 0).TranslateY(0, -40));
```

0 is when the child's leading edge meets the far side of the viewport. 1 is when the trailing edge has left the near side. Values outside that crossing clamp. The axis is vertical when the content is taller than the viewport, or when both axes overflow. It is horizontal only when the content is wider and not taller. Each scroll seeks. `Repeat` and `Yoyo` do not advance, because the clock does not run. A seek does not run the recipe callbacks.

The child sits inside that `ScrollView`. A header that is a sibling of the scroller is outside this walk. With no `ScrollView` ancestor, the seek stays at 0. On load the from pose is written, until the first measurement supplies a progress.

A pixel range maps a stretch of the scroll offset instead of the child's travel through the viewport. `from` and `to` are pixels.

```csharp
Header().Scrub(motion, from: 0, to: 120);
```

0 is `from` and 1 is `to`. Offsets outside that span clamp. When `to` is below `from`, the range runs backward. A zero span stays at 0 until the offset reaches `from`, then it is 1.

`Scrub` calls `SeekFraction`, so the fraction is eased progress. Scroll fraction 0.5 writes the halfway property values. Reduce motion seeks to the end on the first read, including load, and then stops listening. The wrapper is a `Grid` and uses the same target walk as `BindMotion`.

| Member | Role |
|---|---|
| `VisualNode.Scrub(Motion motion)` | Seeks `motion` as the child travels through the viewport. |
| `VisualNode.Scrub(Func<Motion, Motion> motion)` | Same. The function receives an empty motion. |
| `VisualNode.Scrub(Motion motion, double from, double to)` | Seeks from scroll offset `from` to `to`, in pixels. |
| `VisualNode.Scrub(Func<Motion, Motion> motion, double from, double to)` | Same. The function receives an empty motion. |

### Layout

`Collection` slides an item that stays and changes place. `Layout` is that slide for one view that is not in a collection. A card whose slot changes from one pass to the next springs from the position it left. A sibling that grows does the same: the view below slides by the new height, and the recipe does not name that distance.

```csharp
Card()
    .Layout(m => m.FromPlace().WithSpring(Spring.Gentle));
```

The first layout records the bounds and does not play. A later position change plays the recipe. `FromPlace` starts translation at the last origin and ends at the new one, on each axis that moved. A width or height change of at least half a device-independent pixel reveals the new size from the old one. Position and that reveal share the recipe's duration or spring. The layout pass has already applied the final size, so a sibling with `Layout` still springs from the origin it left. The reveal clips the wrapper. It does not read the measured size and does not write `HeightRequest`. A shift under half a pixel stays put.

An authored `Height` or `Width` track, and an `EnterExit` that names both heights, remain the tools when the caller already knows the two sizes. The reveal does not animate a `CollectionView` cell.

The translation is on the wrapper grid. `Drag` writes translation too, so leave it off this child. Other tracks on the recipe play with the slide. Reduce motion shows the new size when the change is recorded, and finishes the slide when the play starts.

| Member | Role |
|---|---|
| `VisualNode.Layout(Motion motion)` | Plays `motion` when the view's layout position or size changes. |
| `VisualNode.Layout(Func<Motion, Motion> motion)` | Same. The function receives an empty motion. |

### Collection

`Collection` is that presence for many items. Pass the items, `itemKey`, `renderItem`, and `placeItems`. A new item plays `enterMotion`. A removed item plays `exitMotion` where it was and leaves the flow, so the others close the gap while it is still on screen. An item that stays and changes place plays `moveMotion`. The first layout records positions and does not play.

```csharp
Motion.Collection(
    State.Items,
    itemKey: item => item.Id,
    renderItem: item => Row(item),
    placeItems: rows => VStack([.. rows]).Spacing(8),
    moveMotion: m => m.FromPlace().WithDuration(280),
    enterMotion: m => m.FadeIn().SlideIn(SlideFrom.Top, 16).WithDuration(280),
    exitMotion: m => m.FadeOut().WithDuration(200))
```

`items.Collection(...)` is the same call. `placeItems` can return a stack, a grid, or a scroll view around those nodes. A `CollectionView` templates and recycles its own cells, so it does not host these nodes.

| Member | Role |
|---|---|
| `Motion.Collection(IEnumerable<T> items, Func<T, object> itemKey, Func<T, VisualNode> renderItem, Func<IEnumerable<VisualNode>, VisualNode> placeItems, Func<Motion, Motion>? moveMotion = null, Func<Motion, Motion>? enterMotion = null, Func<Motion, Motion>? exitMotion = null)` | Keyed items. `placeItems` places the live nodes. `moveMotion`, `enterMotion`, and `exitMotion` each receive an empty motion. |
| `IEnumerable<T>.Collection(...)` | Same arguments, starting with `itemKey`. |
| `Motion.FromPlace()` | On a collection move, translation starts at the item's last position. Other tracks on that recipe still play. |

`itemKey` is compared with `Equals`. It must not be null. When two items share a key, the first one is kept.

`moveMotion`, `enterMotion`, and `exitMotion` play the tracks you write. `FromPlace` on `moveMotion` adds the slide from the last frame. Omit `moveMotion` and that slide is 300 ms and `CubicOut`. Omit `enterMotion` and a new item fades in with the move's timing. Omit `exitMotion` and a removed item fades out with that timing. An exit that never finishes never leaves the collection.

```csharp
moveMotion: m => m.RotateY(90, 0).WithDuration(400)
```

That turns an item that changed place. It does not slide. `m.FromPlace().RotateY(18, 0)` does both.

The move translates the item wrapper. Enter and exit use the same target walk as `BindMotion`, so a fade or a slide on the item does not take the wrapper's translation.

An item that comes back while its exit is still playing slides from the frame it left. The view is a fresh one, so the exit's halfway fade does not carry over.

### MotionPlayer

```csharp
public sealed class MotionPlayer : IDisposable
{
    public Motion Motion { get; }
    public double Progress { get; }
    public bool IsRunning { get; }
    public uint Duration { get; }

    public bool IsDismissed();
    public bool IsForward();
    public bool IsReverse();
    public bool IsPaused();
    public bool IsCompleted();

    public event EventHandler<MotionPlaybackEventArgs>? Started;
    public event EventHandler<MotionPlaybackEventArgs>? Completed;
    public event EventHandler<MotionPlaybackEventArgs>? Paused;
    public event EventHandler<MotionPlaybackEventArgs>? Resumed;
    public event EventHandler<MotionPlaybackEventArgs>? StatusChanged;

    public void At(Action<double> callback);
    public void Pause();
    public void Resume();
    public void Reset();
    public void Seek(uint milliseconds);
    public void Seek(string id);
    public bool TrySpan(string id, out double begin, out double end);
    public void SeekFraction(double t);
    public void Dispose();
}
```

`Duration` is the recipe length plus the longest stagger delay. A 300 ms motion with a 100 ms stagger across two views has `Duration == 400`.

`Progress` is eased playback in the range 0–1. It increases on the way forward and decreases on the way back. A spring reports the spring position, clamped to 0–1. `IsRunning` is true while `IsForward()` or `IsReverse()` is true.

`At` registers a callback for the life of the player. It runs immediately with the current `Progress`, then on every later tick, including ticks that decrease. A callback that throws is logged and skipped. It does not take a beat id. A beat callback is `OnBeat` on the recipe.

```csharp
player.At(t => scrubLabel.Text = $"{t:0.00}");
```

#### Forward and reverse

The playback methods that start a run are extensions on `MotionPlayer?`, in `Reactor.Motion`:

```csharp
_player.Forward();
_player.Reverse();
await _player.ForwardAsync();
await _player.ReverseAsync(cancellationToken);
```

| Member | Role |
|---|---|
| `Forward()` | Starts forward. Fire-and-forget. |
| `Reverse()` | Starts reverse. Fire-and-forget. |
| `ForwardAsync(CancellationToken cancellationToken = default)` | Starts forward and returns a task for that run. |
| `ReverseAsync(CancellationToken cancellationToken = default)` | Starts reverse and returns a task for that run. |

All four are safe on a null player and on a disposed player. They do not throw `OperationCanceledException` or `ObjectDisposedException`. `Reset`, `Dispose`, and a canceled token cancel the pending task. The extension finishes successfully anyway. `Pause` does not cancel it. The task stays pending until the run reaches an end, `Resume` finishes it, or something cancels it.

A completed `Task` is not proof the clip reached the end. After an awaited play, ask the player:

```csharp
await _player.ForwardAsync();
if (_player?.IsCompleted() == true)
{
    // The run reached the end.
}
```

The rows below use the same states as `IsDismissed()`, `IsForward()`, `IsReverse()`, `IsPaused()`, and `IsCompleted()`.

Behavior of a live player:

| Call | Result |
|---|---|
| `Forward` from `Dismissed` | Captures `from`, then plays forward. Auto-repeat is on, so `Repeat` / `Yoyo` continue. |
| `Forward` while already forwarding | Joins the in-flight task. |
| `Forward` from `Paused` after a forward | Continues that forward run. Does not capture `from` again. |
| `Forward` from `Completed` | No-op. The task is already complete. |
| `Forward` while reversing, or paused from a reverse | The reverse task completes successfully. Playback turns around without capturing `from` again. Auto-repeat is on. |
| `Reverse` from `Dismissed` | No-op. There is no captured run to reverse. |
| `Reverse` from `Completed` or mid-forward | Plays backward from the current position. Turns auto-repeat off, so a yoyo stops at the end of this reverse. |
| `ForwardAsync` / `ReverseAsync` with a token that is already canceled | `Started` fires, then the player pauses and the task is canceled. The extension completes without throwing. |

An empty target list completes `Forward` immediately (`Completed`) and leaves `Reverse` at `Dismissed`.

A duration of 0 finishes on the first tick and does not auto-repeat. `Repeat` and `Yoyo` wait until that only when the duration is non-zero, or when a spring is settling.

#### Pause resume and reset

```csharp
_player.Pause();
_player.Resume();
_player.Reset();
_player.Dispose();
```

| Member | Role |
|---|---|
| `Pause()` | Stops the clock when the player is running. `IsPaused()` becomes true. Other states are left alone. |
| `Resume()` | Continues a paused run in the direction it had. No-op unless `IsPaused()` is true. |
| `Reset()` | Stops the clock, cancels the pending task, writes every track back to its captured `from`, and leaves the player dismissed. |
| `Dispose()` | Stops, cancels, and unregisters the player from its views. A second call is a no-op. |

`Pause` stops the clock and leaves the in-flight `ForwardAsync` / `ReverseAsync` task pending. `Resume` raises `Resumed` and continues that same task. If the task was already canceled (by `Reset`, `Dispose`, or the token), `Resume` starts a new run from the paused position without capturing `from` again.

`Reset` before the first `Forward` has nothing captured, so the player is dismissed (`IsDismissed()`).

#### Status

Ask the player, or the event args, where playback is.

| Method | Meaning |
|---|---|
| `IsDismissed()` | No run, or `Reset` put the views back at `from`. Also the end of a reverse. |
| `IsForward()` | The clock is playing toward the end. |
| `IsReverse()` | The clock is playing toward the start. |
| `IsPaused()` | Stopped in the middle by `Pause`, by a cancel, or by a seek inside `(0, 1)`. |
| `IsCompleted()` | The forward run reached the end, including the last repeat. |

`MotionPlaybackEventArgs` carries the recipe, the targets, and `Progress` for the event that just fired. It has the same five methods.

#### Player events

```csharp
player.Started += (_, e) => { /* e.IsForward() or e.IsReverse() */ };
player.Completed += (_, e) => { /* a forward pass reached the end */ };
player.Paused += (_, _) => { };
player.Resumed += (_, _) => { };
player.StatusChanged += (_, e) => { };
```

| Event | When |
|---|---|
| `Started` | A run begins, including each repeat cycle and a direction change. |
| `Completed` | A forward pass reaches the end. A yoyo raises this before the automatic reverse. |
| `Paused` | `Pause`, a cancel that parks the player, or a seek that stops inside the span. Raised once when leaving `Forward` or `Reverse`, not on every scrub tick. |
| `Resumed` | `Resume` continues a paused run that still has a pending task. |
| `StatusChanged` | Any status write. |

`e.At(callback)` on the event args follows that run's progress, including decreases. It is the per-run cousin of `MotionPlayer.At`. Motion progress is not monotonic.

`e.Motion` is the recipe. `e.Targets` is the bound views.

### Recipe callbacks

These live on the recipe, so a page that uses `BindMotion`, `EnterExit`, `InView`, `Press`, `Layout`, or `Drag` does not need the player. The latest call for each one wins. `Copy` keeps a callback you do not replace. `And` keeps the right-hand callback when that side set one. A callback on a beat passed to `Add` or `Then` is not the timeline callback. Put it on the recipe that plays. A callback that throws is logged and skipped.

```csharp
Card().BindMotion(m => m
    .FadeIn()
    .SlideIn(SlideFrom.Bottom, 24)
    .WithDuration(420)
    .Repeat(2)
    .Yoyo()
    .OnBegin(() => { })
    .OnReverseStart(() => { })
    .OnRepeat(() => { })
    .OnComplete(() => { })
    .OnReverseComplete(() => { }));
```

| Member | When |
|---|---|
| `OnBegin(Action begin)` | `Delay` has elapsed and a forward leaves the start. With no delay, that is during the call that starts the run. The tick that only finishes the wait does not run it. Seek, a reverse, and each later cycle do not. A forward after `Reset` runs it again. |
| `OnReverseStart(Action reverseStart)` | A reverse leg begins. `Delay` does not hold a reverse. Press runs this on release. A yoyo runs it at the far end, where `OnComplete` does not. |
| `OnRepeat(Action repeat)` | A repeat is consumed and another cycle starts. The first play does not run it. `Repeat(-1)` runs it every cycle and never runs `OnComplete`. A beat's own `Repeat` does not run the timeline callback. |
| `OnComplete(Action complete)` | No further leg will run, the same moment the forward task completes. A yoyo does not run it at the far end. Reduce motion runs it, because that play is finished. A drag runs it when the release spring rests. |
| `OnReverseComplete(Action reverseComplete)` | A reverse arrives at the from pose and will not start another forward. `Reset` and a seek to 0 do not. Reduce motion runs it for a reverse. |

`At` stays the 0–1 listener, including seek and a decreasing value. `OnBeat` stays the named beat. `Started` still fires when a leg begins, including while a delay is holding. `Completed` still fires at the end of each forward pass, before a yoyo turns around. Seek, pinch, and scrub do not run these callbacks. Enter and exit are separate plays. An exit with `Repeat(-1)` never runs `OnComplete`, so the child stays. The first layout records the origin and does not play.

### Seek

Seeking parks the player. It does not start playback, and it does not continue a `Repeat` or `Yoyo`. Lifting a slider does not play. Call `Forward` when you want the rest of the clip.

```csharp
player.Seek(150);          // 150 ms on the linear player clock
player.SeekFraction(0.5); // eased progress, the same units as Progress
player.Seek("pulse");      // start of a named timeline child
```

| Member | Role |
|---|---|
| `Seek(uint milliseconds)` | Linear wall-clock position. `0` dismisses. A value at or past `Duration` completes. |
| `SeekFraction(double t)` | Eased progress, clamped to 0–1. `NaN` and infinities seek to 0. |
| `Seek(string id)` | Linear seek to the start of a named `Add` / `Then` child. An unknown id does nothing. |
| `TrySpan(string id, out double begin, out double end)` | Linear 0–1 window of that child inside `Duration` (after stagger). Returns false when the id is missing. |

`SeekFraction` inverts `Motion.Easing` and then seeks that linear position, so the pixels match `Progress` and `At`. `Linear`, `CubicIn`, `CubicOut`, `CubicInOut`, `SinIn`, and `SinOut` invert in closed form. Any other easing is inverted by a 24-step search. If that search cannot invert the curve, the seek uses `t` as the linear position and a debug build logs it once.

A seek strictly inside the span sets `Paused` and raises `Paused` once when the player was running. Seeking again while already paused does not raise `Paused` again. Seeking from `Dismissed` or `Completed` into the middle also sets `Paused`, without a `Paused` event. `t == 1` completes. `t == 0` dismisses and writes the from-values.

`Seek(id)` and `TrySpan` use the linear player clock, the same clock as `Seek(uint)`. They are not eased. Named windows are fractions of the recipe duration, then mapped through the stagger padding onto `player.Duration`. With no stagger the two clocks match.

```csharp
if (player.TrySpan("pulse", out var begin, out var end))
{
    // begin and end are 0–1 of player.Duration
}
```

### Drag

`Motion.Drag` binds one or two finger axes and waits, the same way `Motion.Bind` waits. `.X` follows the horizontal pan. `.Y` follows the vertical pan. Leave an axis out and that direction stays put. Each axis has its own player. On release that axis springs to a stop and keeps its own speed. Omit the stops and they are 0 and 1. A fast flick aims at the next stop in that direction. A slower release aims at the nearest stop. The frame pump is the one `Play` already uses.

The recipe is the from/to map. Give it a duration so a seek can stop between the ends. Pass the release spring to `Drag`. `WithSpring` on the recipe stays the playback spring and is still exclusive with duration. Optional `stops` on an axis replace 0 and 1.

A single `TranslateX` or `TranslateY` with a from and a to uses `to - from` as the finger travel. `TranslateX(180, 0)` drags toward the left. Pass `travel` when the finger span and the property span differ, or when the recipe is not one translation.

```csharp
var drop = new Spring(28, 12, 1);
var drag = Motion.Drag(card, drop, plane => plane
    .X(m => m.TranslateX(0, 180).WithDuration(400).WithEasing(Easing.Linear))
    .Y(m => m.TranslateY(0, 120).WithDuration(400).WithEasing(Easing.Linear)));

drag.Seek(0.6, 0.4);                 // x = 108, y = 48, both players paused
drag.Release(velocityX: 2, velocityY: -1);
```

| Member | Role |
|---|---|
| `Motion.Drag(target, spring, plane)` | Binds the axes. Does not install a pan and does not start the spring. |
| `DragPlane.X` / `Y` | Finger axis. The callback receives an empty motion. Optional `stops` replace 0 and 1. |
| `Seek(double? x, double? y)` | Parks the axes you pass. Null leaves an axis alone. `NaN` seeks to 0. |
| `Release(velocityX, velocityY)` | `SpringToNearest` on each bound axis. |
| `Coast(velocityX, velocityY)` | Decays each bound axis inside 0–1. Damping is the friction. An end stops it. |
| `X` / `Y` | The player for that finger axis, or null. |
| `Dispose()` | Stops both players. A later `Release` or `Coast` throws. |

`SpringToNearest` reads `Progress` and the axis stops. Omit the stops and they are 0 and 1, so progress at or above 0.5 targets 1. With more stops, a release slower than 2 targets the nearest stop. The same distance picks the higher stop. A release faster than 2 toward a neighbor targets that neighbor. Positive velocity moves toward 1. The first frames still move with that speed. `NaN` and infinities count as 0. Stops outside 0–1 clamp. Duplicate stops collapse. Fewer than two distinct stops means 0 and 1. Delay, repeat, and yoyo on the recipe do not continue the settle. `Seek` and finish still leave velocity at 0. The drag measures its own speed and passes it in. A canceled pan passes 0, so that release uses the nearest stop.

`WithReduceMotion(true)` jumps to the chosen stop. When the stops are only the ends, that is the end. Releasing when progress is already 0 or 1 finishes that axis immediately. The release runs `OnComplete` when the spring rests, including a stop between the ends. A coast runs `OnComplete` when it comes to rest. Seeking under the finger does not run the recipe callbacks.

`Coast` keeps the speed and rests where it dies, inside the authored span. Letting go in the middle of a lane can stop in the middle. A flick that reaches an end stops on that end. The card does not travel past the lane. Stops belong to `Release`. The release spring's damping is the friction. Stiffness does not pull toward 0 or 1. `NaN` and infinities count as 0. Delay, repeat, and yoyo do not continue the coast. `Seek` still clamps to 0–1. `WithReduceMotion(true)` stops at the current progress. A canceled pan passes 0, so the card stays where the finger left it.

```csharp
card.Coast(drop, plane => plane
    .X(m => m.TranslateX(0, 180).WithDuration(400).WithEasing(Easing.Linear)));
```

`Drag` on a page still calls `Release`. `Coast` on a page calls `Coast` with the speed the pan already measured.

On a page, `Drag` installs the pan. The wrapper fills its cell, the recognizer sits on the wrapper, and the motions run on the child. A lane is `.X` only. Four corners are `.X` and `.Y`. Each axis springs on its own, so the four pairs are the four corners. The center, where both axes are 0.5, goes to the far corner on both axes.

```csharp
card.Drag(drop, plane => plane
    .X(m => m.TranslateX(0, 180).WithDuration(400).WithEasing(Easing.Linear))
    .Y(m => m.TranslateY(0, 120).WithDuration(400).WithEasing(Easing.Linear)));

card.Drag(drop, plane => plane
    .Y(m => m.TranslateY(0, 420).WithDuration(400).WithEasing(Easing.Linear),
        stops: [0, 0.42, 1]));
```

The second call rests a sheet at peek, half, and full. A flick moves one stop. A slow release takes the nearest.

Lifting the finger releases with the sampled speed of each axis. A canceled pan releases both with speed 0, so the card still settles. Grabbing again seeks the current progress and the settle stops under the finger. Another property on the same view keeps its own player. A non-finite travel, or one smaller than a millionth of a pixel, counts as 1.

### Pinch

`Pinch` seeks while two fingers move. The scale recipe follows the span. The rotate recipe follows the angle when you pass one. Omit rotate and the view does not turn. Progress 0 is the from pose and 1 is the to pose. Release leaves that progress. There is no snap. The gesture only seeks, so the recipe callbacks do not run. A canceled pinch returns to the progress where the gesture began. Reduce motion stays at the from pose.

The finger ratio is the current span divided by the span at the start of the gesture. That ratio multiplies the scale at the start. The product is placed between the recipe's from and to. A wider span moves toward the larger scale. With `Scale(1, 2.5)` that larger scale is progress 1. The turn adds the finger angle, in degrees, to the rotation at the start. Positive is clockwise, the same direction as `Rotate`. Both results clamp to the recipe.

```csharp
Photo().Pinch(
    scale: m => m.Scale(1, 2.5).WithDuration(400).WithEasing(Easing.Linear),
    rotate: m => m.Rotate(-30, 30).WithDuration(400).WithEasing(Easing.Linear));
```

`Motion.Pinch` binds the same recipes and waits. It does not install a recognizer. `Seek(ratio, degrees)` parks the pose. `ratio` 1 keeps the scale. `degrees` 0 keeps the rotation. `Release` leaves the progress. `Cancel` restores the progress from the last `Begin`. `NaN` and infinities count as no change. A ratio at or below 0 counts as 1.

The scale player owns the scale tracks. `Scale` writes `ScaleX` and `ScaleY`. The rotate player owns rotation when a rotate recipe is set. A later player that takes one of those releases that part of the pinch. A drag that only translates can sit on the same view. This does not add a pan, a release spring, or a pinch that scrolls a `ScrollView`.

On a page, the wrapper fills its cell, the recognizer sits on the wrapper, and the motions run on the child. The card starts at the from pose.

### Press

`Press` springs a view while the pointer is down, then springs it back. The recognizer sits on the wrapper. The motion runs on the child.

```csharp
Card()
    .Press(m => m.Scale(1, 0.96).WithSpring(Spring.Snappy));
```

The pointer down plays that recipe forward. The pointer up reverses it, the same way an interrupted exit reverses. Pointer down runs `OnBegin` once any delay has elapsed. Pointer up runs `OnReverseStart`, then `OnReverseComplete` when that reverse arrives and will not play forward again. A second press during the reverse continues that run from the live value and does not run `OnBegin` again. The recipe is posed, so a forward that starts fresh reads the live value instead of jumping to the authored from. Reduce motion finishes the run.

Hover is a second recipe. It plays on pointer enter and reverses on pointer exit. Pass nothing and there is no hover. A finger on a phone does not enter or exit, so that hover stays quiet. Press still runs from touch down and up. A mouse, a trackpad, and a stylus hover do raise those events.

```csharp
Card().Press(
    pressed: m => m.Scale(1, 0.96).WithSpring(Spring.Snappy),
    hover: m => m.Scale(1, 1.03).WithSpring(Spring.Gentle));
```

Press owns the properties it writes. A hover player that was writing those properties is released. Releasing while the pointer is still inside reverses the press, which returns to the value captured when the press began. Leaving after that plays toward the hover recipe's authored from. Leaving during the press does the same when hover has already shown. A press that never saw a hover reverses on the way out.

| Member | Role |
|---|---|
| `VisualNode.Press(Motion pressed)` | Plays `pressed` forward on pointer down and reverses it on pointer up. |
| `VisualNode.Press(Func<Motion, Motion> pressed, Func<Motion, Motion>? hover = null)` | Same. Each function receives an empty motion. `hover` plays on pointer enter and reverses on pointer exit. |
| `VisualNode.Press(Motion pressed, Motion? hover)` | Same, with a hover recipe. Null skips hover. |

### Repeat and yoyo

```csharp
m.Scale(1, 1.12).Yoyo().Repeat(-1)
m.FadeIn().Repeat(2)
m.Rotate(0, 180).Yoyo()
```

| Member | Role |
|---|---|
| `Repeat(int count)` | How many forward passes to play. `1` is the default. `0` plays once. A negative count repeats until `Pause`, `Reset`, or `Dispose`. On a timeline beat, the count belongs to that beat. |
| `Yoyo(bool enabled = true)` | After each forward pass, play the reverse. One repeat is forward plus reverse. On a beat, the reverse pass is part of that beat. |

The player continues by itself. It does not recapture `from` between cycles. The `Task` from `ForwardAsync` stays pending until the last cycle. `Pause` leaves that task pending. `Reset`, `Dispose`, and a canceled token cancel it, and the extension finishes without throwing.

`Completed` fires at the end of each forward pass. `Started` fires again for the next leg. `OnRepeat` runs when that next cycle starts. `OnComplete` runs when the forward task completes, which is later than `Completed` when a yoyo or another repeat continues. See [Recipe callbacks](#recipe-callbacks).

`And` keeps the right-hand repeat when it is not `1`, otherwise the left-hand repeat. Yoyo is on if either side is on.

Calling `Reverse` yourself turns auto-repeat off, so a pulsing yoyo stops when the user reverses it. The next `Forward` turns auto-repeat back on.

Dispose a player that repeats forever when its page unloads. `BindMotion` does that on `Unloaded`. A player you created with `Bind` or `Play` needs `Dispose` in `OnWillUnmount`.

### Parallel merge

`And` plays both recipes on one clock. The parent length is the longer duration. Tracks keep their millisecond length. They are not stretched to the parent. The left-hand tracks are flattened first, then the right-hand tracks. On an overlap, the right-hand track wins. A debug build logs the overlap.

```csharp
var enter = Motion.None.FadeIn().WithDuration(280);
var grow = Motion.None.Scale(0.9, 1).WithDuration(400);

var both = enter.And(grow);   // 400 ms. Fade occupies the first 280.
```

| Piece | Which side wins |
|---|---|
| Length | The longer one. |
| Easing | The right-hand easing, when it is not the default `CubicOut`. Otherwise the left. |
| Stagger | The right-hand stagger, when it has one. Otherwise the left. |
| Repeat | The right-hand count, when it is not `1`. Otherwise the left. |
| Yoyo | On if either side is on. |
| Color space | The right-hand space. |
| Spring | The right-hand spring, when it has one. Otherwise the left. |
| Perspective | The right-hand entry, when it has one. Otherwise the left. |
| Named spans | Both, shifted into the parent. `And` does not assign new ids. |
| Delay | Baked into each side's windows. The result delay is 0. |
| Callbacks | The right-hand callback when that side set one. Otherwise the left. |

`And` does not take a name. Use `Add` when the child needs an id.

### Delay

`WithDelay` holds the from-pose, then plays the motion you already described. One view is delayed. `Duration` stays the track length, and `MotionPlayer.Duration` stays that length plus the longest stagger. The hold is outside both numbers.

```csharp
Motion.Play(
    m => m.Opacity(0, 1).TranslateY(28, 0).WithDuration(400).WithDelay(700),
    card);
```

A 400 ms fade with a 700 ms delay reports player duration 400. `ForwardAsync` completes after 1100 ms. During the first 700 ms progress stays 0. The first forward still writes `from` immediately, then holds it. `OnBegin` runs on the later tick that moves the tracks. The tick that only finishes the wait does not run it.

The hold runs once, on a forward that starts from the dismissed pose. Repeat and yoyo do not play it again. `Reset` arms it again. `Seek` is a position on the track clock. A seek during the hold drops the rest of the wait, and the next `Forward` continues from that seek.

`WithDelay` and `WithSpring` combine. The spring starts after the hold, and the hold does not count toward the spring.

This is not `Stagger`, `Add`'s `at`, or a keyframe offset. `Stagger` offsets extra targets inside the track span. `Add(child, at)` is where a child starts. A delay on that child is more time before the child's tracks (`at + child.Delay`), and the parent's own delay stays the player hold. Keyframe offsets are fractions of a track.

### Stagger

Stagger delays each bound target on the linear player clock. It applies to the root motion, and only when more than one view is bound. A `Stagger` on a child passed to `Add` or `Then` is ignored. A debug build logs that.

```csharp
await Motion.ForwardAsync(
    m => m
        .FadeIn()
        .Scale(0.9, 1)
        .Stagger(40, StaggerFrom.Start, (3, 4))
        .WithDuration(280),
    tiles);
```

| Member | Role |
|---|---|
| `Stagger(uint stepMilliseconds, StaggerFrom from = StaggerFrom.Start, (int Columns, int Rows)? grid = null)` | Delay between targets. |
| `Stagger` | The value type: step, origin, optional grid. |
| `StaggerFrom` | `Start`, `Center`, or `End`. |

The player length becomes `Duration + max delay`. Each target's window is `[delay, delay + Duration)` on that longer clock. Before its window the target holds `from`. At time 0 every target is already at `from`.

A 300 ms motion, a 100 ms step, and two views produces a 400 ms player. The first window is `[0, 300)`, the second is `[100, 400)`.

`StaggerFrom.Start` delays `index * step`. `End` delays `(count - 1 - index) * step`. `Center` on a list delays by the distance from the middle index. Those three stay in bind order even when a grid is set, except `Center`: with a grid, `Center` uses the Euclidean distance from the cell to the center of the grid. Column and row come from the index in row-major order (`index % columns`, `index / columns`). The sample stagger page is 12 tiles, 3 columns by 4 rows, step 40.

`WithSpring` and `Stagger` are mutually exclusive. A debug build asserts if both are set. The spring clocks a single position for the player.

### Keyframes

Keyframe offsets are fractions of this motion, from 0 to 1. They stretch when `WithDuration` replaces the length of a full-span recipe. `NaN` and infinities become 0. Values outside 0–1 are clamped. Offsets must be non-decreasing. A debug build asserts when one goes backwards, and the offset is then clamped up to the previous one.

Per-property form:

```csharp
m.Opacity(k => k.At(0, 0).At(0.35, 1).At(1, 1))
 .Scale(k => k.At(0, 0.8).At(0.6, 1.08, Easing.CubicOut).At(1, 1))
```

`KeyframeBuilder<T>.At(double offset, T value, Easing? easing = null)` appends a stop and returns the builder so calls chain. The easing on a stop is the curve into that stop. A null easing uses the motion easing.

When the first offset is greater than 0, the track holds `from` until that offset.

Whole-state form, several properties at the same offsets:

```csharp
m.Keyframes(
    (0.00, s => s.Scale(1)),
    (0.40, s => s.Scale(1.14)),
    (1.00, s => s.Opacity(1).Scale(1)))
```

Each entry is `(double At, Func<Motion, Motion> Set)`. `Set` receives `Motion.None` and the tracks it returns become stops at that offset. A property omitted from a later state holds its last keyframed value. It does not blend back to the implicit from. An empty list, or a builder you never call `At` on, leaves the recipe unchanged.

Sequential steps in milliseconds belong on `Then`, not on `Keyframes`.

### Timeline

`Motion.Timeline` can start empty, with the defaults for every beat, or it can open on the first beat. `Then` appends a beat. `Add` places one at a time. Each callback receives an empty motion. `Motion.Define` builds that same recipe.

```csharp
var timeline = Motion.Timeline(duration: 280, easing: Easing.CubicOut, delay: 300)
    .Add(Motion.Define(m => m.FadeIn().SlideIn(SlideFrom.Left, 48)), id: "intro")
    .Then(Motion.Define(m => m.Scale(1, 1.12).WithDuration(200)), "pulse");

player.Seek("pulse");
```

The intro uses the timeline's 280 ms and `CubicOut`. The pulse set its own 200 ms, so it stays 200 ms. `delay` waits before the whole timeline. A beat's own `WithDelay` stays a gap before that beat.

`duration` is the default beat length, not the finished timeline. The timeline grows as you add beats. The same call also takes repeat, yoyo, rate, reduce-motion, stagger, color space, perspective, spring, spring velocity, and pose.

`Add` places a beat at `at` milliseconds from the start of the timeline. The turn below starts when the slide is at 140 ms. Both beats use the 320 ms default. A later `WithDuration` leaves that `at` where it is, so the turn still begins 140 ms after the slide started.

```csharp
Motion.Timeline(duration: 320, easing: Easing.CubicOut)
    .Add(Motion.Define(m => m.TranslateX(0, 72)), id: "slide")
    .Add(Motion.Define(m => m.Rotate(0, 24)), at: 140, id: "turn");
```

`Then` measures from the previous beat's end. `offset: 80` starts 80 ms after that end. `offset: -80` starts 80 ms before it, and the start clamps at 0. A later `WithDuration` keeps that distance from the new end. A beat's own `WithDelay` still waits after the offset start.

```csharp
Motion.Timeline(duration: 320, easing: Easing.CubicOut)
    .Add(Motion.Define(m => m.TranslateX(0, 72)), id: "slide")
    .Then(Motion.Define(m => m.FadeIn().WithDuration(80)), id: "fade", offset: -80);
```

`target` follows the arguments of `Bind`. `0` is the first view, `1` is the second. Any `VisualElement` can take either place. Below, the box slides for 300 ms and the image starts a 300 ms turn 100 ms later. Omit `target` and that beat plays on every bound view. A beat with a target ignores stagger.

```csharp
var timeline = Motion.Timeline(easing: Easing.CubicOut)
    .Add(Motion.Define(m => m.TranslateX(0, 48).WithDuration(300)), target: 0)
    .Add(Motion.Define(m => m.Rotate(0, 80).WithDuration(300)), at: 100, target: 1);

timeline.Bind(box, image);
```

The timeline can wait, and a beat can wait again before it moves. The fade uses the 280 ms default. The slide keeps that default and starts 200 ms after the fade ends.

```csharp
Motion.Timeline(duration: 280, easing: Easing.CubicOut, delay: 360)
    .Add(Motion.Define(m => m.FadeIn()))
    .Then(Motion.Define(m => m.TranslateX(0, 64).WithDelay(200)));
```

`yoyo` plays the finished timeline backward after the forward pass. `repeat` counts those cycles. A cycle with `yoyo` is forward, then back.

```csharp
Motion.Timeline(duration: 420, easing: Easing.CubicInOut, yoyo: true)
    .Add(Motion.Define(m => m.TranslateX(0, 80)));
```

`Repeat` and `Yoyo` on a beat belong to that beat. The beat's window includes every cycle, and the next `Then` starts after them. `Repeat(2)` plays the beat twice. `Yoyo` plays it backward after each forward pass, so `Repeat(2).Yoyo()` is forward, back, forward, back. `Repeat(-1)` never finishes, so every later beat waits. A negative `Then` offset that overlaps the loop still plays on each pass. `repeat` and `yoyo` on `Timeline(...)` itself, and on the opening `Timeline(configure)` call, still belong to the whole timeline.

```csharp
Motion.Timeline(duration: 280, easing: Easing.CubicOut)
    .Add(Motion.Define(m => m.FadeIn()))
    .Then(Motion.Define(m => m.Scale(1, 1.12).Repeat(2).Yoyo()))
    .Then(Motion.Define(m => m.FadeOut()));
```

`stagger` delays each bound view. The recipe is still one timeline. The second view starts 90 ms after the first, and the third 90 ms after the second.

```csharp
Motion.Timeline(duration: 320, easing: Easing.CubicOut, stagger: new Stagger(90))
    .Add(Motion.Define(m => m.FadeIn().TranslateY(16, 0)));
```

A spring replaces the duration clock and ignores `duration` and `stagger`. It plays every track from the same progress, so beats on a sprung timeline run together.

```csharp
Motion.Timeline(spring: Spring.Gentle, springVelocity: 2)
    .Add(Motion.Define(m => m.TranslateX(0, 80).Scale(1, 1.12)));
```

The first beat can also be the argument. `WithDuration` and `WithEasing` afterwards do the same work as the defaults above.

```csharp
Motion.Timeline(m => m.FadeIn().SlideIn(SlideFrom.Left, 48), "intro")
    .Then(m => m.Scale(1, 1.12).WithDuration(200), "pulse")
    .WithDuration(280)
    .WithEasing(Easing.CubicOut);
```

| Member | Role |
|---|---|
| `Timeline(uint? duration = null, Easing? easing = null, uint delay = 0, int repeat = 1, bool yoyo = false, double rate = 1, bool? reduceMotion = null, Stagger? stagger = null, ColorSpace colorSpace = ColorSpace.Hsv, double? perspective = null, Spring? spring = null, double springVelocity = 0, bool pose = false)` | Empty timeline. `duration` and `easing` fill beats that did not set their own. The other arguments belong to the timeline. |
| `Timeline(Func<Motion, Motion> configure, string? id = null)` | Builds the first beat. The result is a timeline, so a later `WithDuration` / `WithEasing` applies to every beat that did not set its own. |
| `Then(Func<Motion, Motion> configure, string? id = null, int? target = null, int offset = 0)` | Builds a beat and appends it after the current span. `offset` is milliseconds from that end. Negative overlaps the previous beat. `target` picks one bound view. |
| `Add(Func<Motion, Motion> configure, uint at = 0, string? id = null, int? target = null)` | Builds a beat and inserts it at `at` milliseconds. |
| `And(Func<Motion, Motion> configure)` | Builds a recipe and plays it at the same time as the receiver. |
| `Add(Motion child, uint at = 0, string? id = null, int? target = null)` | Inserts `child` at `at + child.Delay` milliseconds. A beat `Repeat` or `Yoyo` multiplies that window. The parent's own delay stays the player hold. `target` is the bind index. Null plays on every view. |
| `Then(Motion next, string? id = null, int? target = null, int offset = 0)` | Appends `next` at `Duration + offset`. |
| `OnBeat(string id, Action enter)` | Runs `enter` when forward playback reaches `id`, including each later repeat. |

Children are flattened immediately. Their tracks are shifted in milliseconds and are not stretched. A nested `Stagger` is ignored. If two tracks write the same property of the same view in overlapping time, the later one wins and a debug build logs the overlap.

`id` records a named window as a fraction of the parent length. Duplicate ids keep the last one. `Seek(id)` and `TrySpan` read those windows. See [Seek](#seek).

`OnBeat` runs when forward playback reaches that named beat, including each later repeat. It does not run during a timeline delay, on seek, or on the way back. `MotionPlayer.At` still listens to the whole player progress. Callbacks for the whole run are [recipe callbacks](#recipe-callbacks). A `Repeat` on a beat does not run the timeline's `OnRepeat`.

```csharp
Motion.Timeline(duration: 320, easing: Easing.CubicOut)
    .Add(Motion.Define(m => m.TranslateX(0, 72)), id: "slide")
    .Then(Motion.Define(m => m.FadeIn()), id: "fade")
    .OnBeat("fade", () => { });
```

`WithDuration` on a `Timeline` sets every beat that did not call `WithDuration`, then lays the beats out again. A `Then` beat follows the new end, keeping its `offset`. An `Add` beat keeps its `at`. A child `WithDelay` stays a gap before that beat. `WithEasing` sets the curve for every beat that did not call `WithEasing`. A beat that did call it keeps that curve.

`Motion.None` is already 300 ms long, even with no tracks. Adding a 100 ms child onto a bare `None` produces a 300 ms parent, and the child occupies the first 100 ms. That form is not a `Timeline`: `WithDuration` on it still pads the end instead of setting each beat.

`And` shifts named windows into the merged length, after each side's delay. It does not give a merged recipe a new id. Pass the id to `Add` or `Then`.

### Springs

A spring plays a mass-spring-damper until rest. It is not an `Easing`. The wall-clock `Duration` is not the length of the clip.

```csharp
public readonly record struct Spring(double Stiffness, double Damping, double Mass)
{
    public static Spring Default { get; }  // 180, 16, 1
    public static Spring Snappy { get; }   // 400, 22, 1
    public static Spring Gentle { get; }   // 90, 18, 1
}
```

The parameterless constructor is `Default`.

```csharp
m.Scale(1, 1.16).WithSpring(Spring.Snappy)
m.TranslateX(0, 40).WithSpring(new Spring(220, 18, 1), velocity: 4)
m.Opacity(0, 1).Settle(Spring.Snappy).TranslateX(0, 40).WithDuration(280)
```

| Member | Role |
|---|---|
| `WithSpring(Spring spring, double velocity = 0)` | Play this recipe with that spring. `velocity` is progress units per second at the start of a forward or reverse. |
| `Settle(Spring spring, double velocity = 0)` | Spring each track added so far. Tracks added after this call stay on the duration clock. |

`WithSpring` is mutually exclusive with `WithDuration`, `Stagger`, and `Settle`. A debug build asserts if you combine them. `Settle` keeps the duration clock for every track that does not have a spring. The run finishes when those tracks are done and every spring has started and settled. A recipe whose tracks are all springs finishes when they settle, without waiting out the rest of the duration. A spring starts when its window opens, so stagger still delays it.

The integrator is semi-implicit Euler. The step is clamped to 1/30 of a second. `Progress` for `WithSpring` is the spring position clamped to 0–1. The property follows the spring position and can pass the from or to value while the spring is still moving. The spring settles when position and velocity are both within `0.002` of rest, or after 8 seconds, whichever comes first. A settled forward pass then honors `Repeat` and `Yoyo`. Seek and a finished run leave velocity at 0.

`And` keeps the right-hand spring when the right-hand motion has one.

### Poses

`WithPose` retargets every new forward. Each property track reads its from-value from the view and keeps the recipe's `to`. An in-flight spring on that view hands its velocity to the new run, so the property keeps moving the way it was. A whole-recipe spring carries the first handed property. `Settle` carries each property on its own. Seek and a finished run leave velocity at 0, and that rest does not replace the new recipe's own kick. Keyframes, paths, and matrix transforms keep their authored start.

```csharp
m.Opacity(1, 0).WithPose()
m.FadeOut().WithSpring(Spring.Snappy).WithPose()
```

`And` turns the pose on when either side has one.

### Path

`Path` writes `TranslationX` and `TranslationY` by sampling a `PathGeometry` or an SVG path string along its drawn length. The translation is in the path's own coordinates, from where the view is laid out.

```csharp
static PathGeometry Bezier()
{
    var geometry = new PathGeometry();
    var figure = new PathFigure { StartPoint = new Point(0, 0) };
    figure.Segments.Add(new BezierSegment
    {
        Point1 = new Point(70, -90),
        Point2 = new Point(170, 90),
        Point3 = new Point(260, 0),
    });
    geometry.Figures.Add(figure);
    return geometry;
}

Motion.Bind(
    m => m.Path(Bezier()).WithDuration(900).WithEasing(Easing.SinInOut),
    orb);

Marker().BindMotion(m => m
    .Path("M20,50 C20,-50 180,150 180,50 C180-50 20,150 20,50 Z")
    .WithDuration(1200)
    .WithEasing(Easing.Linear));
```

| Member | Role |
|---|---|
| `Path(PathGeometry geometry, double from = 0, double to = 1, bool orient = false)` | Move along `geometry`. `from` and `to` are fractions of the drawn length. `orient` also writes `Rotation` from the tangent. |
| `Path(string data, double from = 0, double to = 1, bool orient = false)` | Parse `data` once and walk it the same way. |

`from` and `to` are clamped to 0–1. A non-finite `from` becomes 0. A non-finite `to` becomes 1. `geometry` and `data` must not be null.

`data` accepts `M` `L` `H` `V` `C` `S` `Q` `T` `A` `Z` and the lowercase forms, repeated coordinate pairs, and compact numbers such as `C180-50`. A `transform`, a `viewBox`, and shape elements such as `circle` and `rect` are not path data. Invalid markup throws when the recipe is built. An empty path does not move the view.

A `Z` adds the closing edge when the pen is not already on the start, and adds nothing when it is. A later `M` lifts the pen. That gap has no length, and the view jumps to the next figure. It does not travel the straight line between them. The geometry overload uses that same walk. A closed `PathFigure` is the `Z`.

The sampler flattens lines, polylines, quadratic Béziers, cubic Béziers, and elliptical arcs once, when the player is created. A curve splits until the chord sits within half a point of the curve, so a long bend stays smooth and a nearly straight cubic stays short. Playback resumes at the last segment. A seek reads the length table directly. A degenerate arc (a radius that collapses) becomes a straight line. The first `Forward` writes the point at `from`. With `orient: true` that same frame writes `Rotation` in clockwise degrees from the positive X axis, matching a Y-down coordinate system. `orient` defaults to false, so an existing path does not start rotating.

Stagger delays when each target starts walking the path. Keyframes along a path are a sequence of `Then` segments, each with its own geometry.

`Path` and `Translate` both write translation. Overlapping windows follow the later track.

An arc in the same shape as the sample:

```csharp
static PathGeometry Arc()
{
    var geometry = new PathGeometry();
    var figure = new PathFigure { StartPoint = new Point(0, 0) };
    figure.Segments.Add(new ArcSegment
    {
        Point = new Point(240, 0),
        Size = new Size(120, 90),
        SweepDirection = SweepDirection.Clockwise,
        IsLargeArc = false,
    });
    geometry.Figures.Add(figure);
    return geometry;
}
```

### SVG document

```csharp
var route = SvgPath.Parse(markup, mapBounds)
    .First(path => path.Id == "route");

Photo().BindMotion(m => m
    .Path(route.Data)
    .WithDuration(6000)
    .WithEasing(Easing.Linear));
```

`SvgPath` is one followable shape. `Parse` returns `path`, `line`, `polyline`, `polygon`, `rect`, `circle`, and `ellipse` in document order. A shape that is not a `path` comes back as a `d` string. `Motion.Path` walks that string. `from`, `to`, and `orient` stay on `Path`.

`Id` is the element's id. A missing or empty id is null, and no id is invented. The shape stays in the list. Two elements with the same id both stay, in document order.

`Parse(markup)` bakes each element's `transform`, and the transforms of its parent groups, into `Data`. Coordinates stay in user units. `Parse(markup, into)` also reads the root `viewBox` and stretches it onto `into`. `into` is the rectangle where the SVG is drawn, in the same coordinates the view translates in. Without a positive root `viewBox`, coordinates stay in user units. An arc under a transform is written as cubics that stay within half a point of the arc.

Invalid markup throws when `Parse` is called. A document with no followable shapes returns an empty list. Several roads are several entries, then several `Path` calls on a timeline.

### One player per property

Each property of a view has one live player. A fade and a slide on the same view run together. A second player that writes a property the first one writes takes that property. The first player keeps every property the new recipe does not write. When the first player has nothing left, it is disposed. The dispose happens after the new player is registered, and a debug build logs the steal.

A path owns translation on both axes, and rotation when `orient` is set. A matrix transform owns translation, rotation, and scale. Taking any one of those releases the whole path or transform. A pinch owns the scale tracks in its scale recipe, and rotation when a rotate recipe is set. Taking those releases that part of the pinch. Opacity, color, corner radius, and anchor keep their own player.

A player that targets many views owns each view's properties on its own. A new player on one tile takes that tile's properties and leaves the other tiles on the first player.

A posed replacement still receives the spring speed of the properties it takes. `Dispose` unregisters only the properties this player still owns. Disposing a player that has already given a property away does not drop the replacement.

`Reset` and `Dispose` both cancel an in-flight `ForwardAsync`. Unload paths should dispose. The null-safe `Forward()` on a field you have already nulled out is a no-op.

## Samples

`samples/Sample` is a MauiReactor gallery of these recipes.

## Building

```
dotnet test tests/MauiMotion.Tests/MauiMotion.Tests.csproj -f net10.0
```

CI runs tests on Ubuntu (`net10.0`) and builds `net10.0-android` and `net10.0-ios`.

## License

MIT
