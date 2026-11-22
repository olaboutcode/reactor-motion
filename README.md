# Reactor.Motion

[![CI](https://github.com/olaboutcode/reactor-motion/actions/workflows/ci.yml/badge.svg)](https://github.com/olaboutcode/reactor-motion/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Reactor.Motion.svg)](https://www.nuget.org/packages/Reactor.Motion/)

In-page view motion for [MauiReactor](https://github.com/adospace/reactorui-maui). A recipe can fade, slide, scale, stagger, follow keyframes or a timeline, spring, walk a path, write a matrix, or enter and exit while the view stays until the exit finishes.

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
  - [MotionPlayer](#motionplayer)
    - [Forward and reverse](#forward-and-reverse)
    - [Pause resume and reset](#pause-resume-and-reset)
    - [Status](#status)
    - [Player events](#player-events)
  - [Seek](#seek)
  - [Repeat and yoyo](#repeat-and-yoyo)
  - [Parallel merge](#parallel-merge)
  - [Delay](#delay)
  - [Stagger](#stagger)
  - [Keyframes](#keyframes)
  - [Timeline](#timeline)
  - [Springs](#springs)
  - [Poses](#poses)
  - [Path](#path)
  - [One player per view](#one-player-per-view)
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
| `Motion` | Recipe, plus static `Define`, `Bind`, `Play`, `ForwardAsync`, and `ReverseAsync`. |
| `MotionPlayer` | Playback. |
| `MotionPlaybackEventArgs` | Player events. State is `IsDismissed()`, `IsForward()`, `IsReverse()`, `IsPaused()`, and `IsCompleted()`. |
| `Spring`, `Stagger`, `StaggerFrom`, `SlideFrom`, `ColorSpace`, `KeyframeBuilder<T>`, `Matrix4` | Recipe values. |
| `MotionCurve` | `CubicBezier` easing. |
| `MotionPreferences` | App-wide reduce-motion override. |
| `VisualNodeExtensions` | `.BindMotion()`, `.EnterExit()`. |
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

A later recipe on the same view replaces the entry. `And` keeps the right-hand entry when the right-hand motion has one.

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

`ForwardAsync` and `ReverseAsync` also bind and start, and the only handle you get back is the `Task`. Use `Bind` or `Play` when you need the player. A later `Bind` or `Play` on that view disposes the player the one-shot call left behind. See [One player per view](#one-player-per-view).

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

`At` registers a callback for the life of the player. It runs immediately with the current `Progress`, then on every later tick, including ticks that decrease. A callback that throws is logged and skipped.

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

### Repeat and yoyo

```csharp
m.Scale(1, 1.12).Yoyo().Repeat(-1)
m.FadeIn().Repeat(2)
m.Rotate(0, 180).Yoyo()
```

| Member | Role |
|---|---|
| `Repeat(int count)` | How many forward passes to play. `1` is the default. `0` plays once. A negative count repeats until `Pause`, `Reset`, or `Dispose`. |
| `Yoyo(bool enabled = true)` | After each forward pass, play the reverse. One repeat is forward plus reverse. |

The player continues by itself. It does not recapture `from` between cycles. The `Task` from `ForwardAsync` stays pending until the last cycle. `Pause` leaves that task pending. `Reset`, `Dispose`, and a canceled token cancel it, and the extension finishes without throwing.

`Completed` fires at the end of each forward pass. `Started` fires again for the next leg.

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

`And` does not take a name. Use `Add` when the child needs an id.

### Delay

`WithDelay` holds the from-pose, then plays the motion you already described. One view is delayed. `Duration` stays the track length, and `MotionPlayer.Duration` stays that length plus the longest stagger. The hold is outside both numbers.

```csharp
Motion.Play(
    m => m.Opacity(0, 1).TranslateY(28, 0).WithDuration(400).WithDelay(700),
    card);
```

A 400 ms fade with a 700 ms delay reports player duration 400. `ForwardAsync` completes after 1100 ms. During the first 700 ms progress stays 0. The first forward still writes `from` immediately, then holds it.

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

`Motion.Timeline` is the first beat. `Then` appends a beat. `Add` places one at a time. Each callback receives an empty motion.

```csharp
var timeline = Motion.Timeline(
        m => m.FadeIn().SlideIn(SlideFrom.Left, 48),
        "intro")
    .Then(m => m.Scale(1, 1.12).WithDuration(200), "pulse")
    .WithDuration(280)
    .WithEasing(Easing.CubicOut);

player.Seek("pulse");
```

The intro is 280 ms. The pulse set its own 200 ms, so it stays 200 ms. Both use `CubicOut` unless a beat calls `WithEasing`.

| Member | Role |
|---|---|
| `Timeline(Func<Motion, Motion> configure, string? id = null)` | Builds the first beat. The result is a timeline, so a later `WithDuration` / `WithEasing` applies to every beat that did not set its own. |
| `Then(Func<Motion, Motion> configure, string? id = null)` | Builds a beat and appends it after the current span. |
| `Add(Func<Motion, Motion> configure, uint at = 0, string? id = null)` | Builds a beat and inserts it at `at` milliseconds. |
| `And(Func<Motion, Motion> configure)` | Builds a recipe and plays it at the same time as the receiver. |
| `Add(Motion child, uint at = 0, string? id = null)` | Inserts `child` at `at + child.Delay` milliseconds. Parent length becomes `max(current, at + child.Delay + child.Duration)`. The parent's own delay stays the player hold. |
| `Then(Motion next, string? id = null)` | `Add(next, Duration, id)`. |

Children are flattened immediately. Their tracks are shifted in milliseconds and are not stretched. A nested `Stagger` is ignored. If two tracks write the same property in overlapping time, the later one wins and a debug build logs the overlap.

`id` records a named window as a fraction of the parent length. Duplicate ids keep the last one. `Seek(id)` and `TrySpan` read those windows. See [Seek](#seek).

`WithDuration` on a `Timeline` sets every beat that did not call `WithDuration`, then lays the beats out again. A `Then` beat follows the new end. An `Add` beat keeps its `at`. A child `WithDelay` stays a gap before that beat. `WithEasing` sets the curve for every beat that did not call `WithEasing`. A beat that did call it keeps that curve.

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

`Path` writes `TranslationX` and `TranslationY` by sampling a `PathGeometry` along its length.

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
```

| Member | Role |
|---|---|
| `Path(PathGeometry geometry, double from = 0, double to = 1, bool orient = false)` | Move along `geometry`. `from` and `to` are fractions of the path length. `orient` also writes `Rotation` from the tangent. |

`from` and `to` are clamped to 0–1. A non-finite `from` becomes 0. A non-finite `to` becomes 1. `geometry` must not be null.

The sampler flattens lines, polylines, quadratic Béziers, cubic Béziers, and elliptical arcs. A degenerate arc (a radius that collapses) becomes a straight line. The first `Forward` writes the point at `from`. With `orient: true` that same frame writes `Rotation` in clockwise degrees from the positive X axis, matching a Y-down coordinate system. `orient` defaults to false, so an existing path does not start rotating.

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

### One player per view

One live `MotionPlayer` owns a `VisualElement`. Binding a second player to that view disposes the first. The dispose happens after the new player is registered, and a debug build logs the steal.

The table is per view, not per property. A stagger player that targets twelve tiles owns all twelve. Binding a new player to any one of them disposes the whole stagger player.

`Dispose` unregisters only when this player is still the owner. Disposing a player that has already been replaced does not drop the replacement.

`Reset` and `Dispose` both cancel an in-flight `ForwardAsync`. Unload paths should dispose. The null-safe `Forward()` on a field you have already nulled out is a no-op.

## Samples

`samples/Sample` is a MauiReactor gallery. Home lists the playground, stagger, scrub, color spaces, timeline seek, path (Bézier + arc), matrix (turn and tilt), enter / exit, and poses. On the poses page, Play springs two cards toward an outline. Halfway, Pose comes back from the live position and keeps the spring speed. Snap jumps onto the outline, then comes back.

## Building

```
dotnet test tests/MauiMotion.Tests/MauiMotion.Tests.csproj -f net10.0
```

CI runs tests on Ubuntu (`net10.0`) and builds `net10.0-android` and `net10.0-ios`.

## License

MIT
