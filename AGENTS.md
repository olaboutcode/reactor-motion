# Reactor.Motion

In-page view motion for MauiReactor. The public package and namespace are `Reactor.Motion`. The class library project is `src/MauiMotion`. Recipes are built with `Motion`. There is no `Animate` type.

`docs/PLAN.md` is the scope and publish plan. `README.md` is the public API. XML comments on public members are part of that API.

## Scope

This repo is motion only. Shared-element flights belong in a future `Reactor.Hero` package. `MotionPins` stays internal until that package exists.

Do not reference `Reactor.Animate` from this repo. The two packages both export `BindMotion` and `ColorSpace`.

Lottie and Rive play authored files. They are not a model for this API.

## Layout

- `src/MauiMotion` is the library. Public types live in `Reactor.Motion`. Implementation details live in `Reactor.Motion.Internals`.
- `tests/MauiMotion.Tests` is xUnit. `InternalsVisibleTo` lets tests reach internal members.
- `samples/Sample` is the MauiReactor gallery.
- `Directory.Packages.props` holds package versions. `global.json` pins the SDK.

## Build

Test the library on `net10.0`. That is the CI gate.

```bash
dotnet test tests/MauiMotion.Tests/MauiMotion.Tests.csproj -c Release -f net10.0 --nologo
```

Build the sample the same way when a gallery page changes.

```bash
dotnet build samples/Sample/Sample.csproj -c Release -f net10.0 --nologo
```

The sample is a MAUI app. A successful `net10.0` build does not mean the page was exercised on a device.

Warnings fail the library and the test project. The sample is still expected to build with no warnings.

- MA0051: keep methods at or under 100 lines.
- CA1822: a helper that does not use instance state is `static`.
- CA1859: return the concrete type, such as `MauiReactor.VStack`, rather than `VisualNode`.
- `Color.WithAlpha` takes a `float`. Write `0.35f`.
- `GestureStatus` is in `Microsoft.Maui`. The name `MauiControls.GestureStatus` does not exist.

## Motion model

`Motion` is an immutable recipe. It stores tracks and timing. It does not store views. `MotionPlayer` binds a recipe and plays it. `Copy` keeps every field the caller does not pass. A new field has to be threaded through the constructor and `Copy`.

`BindMotion` creates the player on Loaded and disposes it on Unloaded. It does not play. `Forward` on a completed player does nothing. `Reset` writes the from pose so a later `Forward` can run again.

One player owns each property it writes. A later player takes only the properties in its recipe. A path owns both translations, and rotation when `orient` is set. A path string is parsed when the recipe is built. The walk includes a `Z` closing edge when the pen is not already on the start, and a later `M` is a jump with no length. Curves flatten once, until the chord is within half a point. `SvgPath.Parse` reads `path`, `line`, `polyline`, `polygon`, `rect`, `circle`, and `ellipse` in document order and bakes each `transform`, including parent groups, into the `d` string. A missing or empty id stays null. `Parse(markup, into)` stretches the root `viewBox` onto `into`. `Path` still walks the string. A matrix owns translation, rotation, and scale. Taking one of those releases the whole path or transform.

`EnterExit` plays when the parent keeps rendering the node and passes `present`. Removing the node unmounts it immediately. An exit with `Repeat(-1)` never finishes, so the child never leaves. `InView` plays when the child has scrolled halfway into the nearest `ScrollView`. A child with no `ScrollView` plays on load, including when `replay` is set. Omit `replay` and scrolling away does not play again. `replay: true` arms when the overlap falls back below that halfway mark. The pose stays. The next crossing seeks the from pose and plays forward. It does not restart while the child stays past the mark, and it does not reverse on the way out. Reduce motion finishes when that play starts, including each later entry. `Scrub` seeks `SeekFraction` from that same scroll and does not play. The child sits inside the `ScrollView`. Omit `from` and `to` to track the child's trip through the viewport. Pass pixel offsets to map a scroll span. Reduce motion seeks to the end on the first read.

`Collection` moves an item that stays and changes place. `FromPlace` starts that translation at the last position. A `CollectionView` recycles cells and does not host these nodes. `Layout` is that move for one view that is not in a collection. The first layout records the bounds and does not play. A later position change plays. A width or height change of at least half a pixel reveals the new size from the old one, on that same recipe. The layout pass has already set the new size. The reveal does not write `HeightRequest`. A shift under half a pixel stays put. Reduce motion shows the new size when the change is recorded. `Layout` and `Drag` both write translation, so they do not share a child.

`Drag` follows the finger on the axes in the plane and, on release, springs each axis to a stop. Omit stops and the ends are 0 and 1, so the nearer end is progress `0.5`. Stops on an axis replace those ends. A slower release takes the nearest stop. A release faster than 2, toward a neighbor, targets that neighbor. A canceled pan uses the nearest stop. The release already receives the finger speed. `Coast` uses that same speed and rests where it dies, inside the authored span. Reaching an end stops there. Damping is the friction. `Coast` does not snap to a stop. `Release` stays the default.

`Pinch` seeks a scale recipe from the finger span, and a rotate recipe from the finger angle when one is passed. A wider span moves toward the larger scale. A turn follows the fingers. Release leaves the progress. A canceled pinch returns to where it began. There is no snap. Reduce motion stays at the from pose. It owns that scale, and rotation when rotate is set. A drag that only translates can share the view.

`Press` plays its recipe forward while the pointer is down and reverses on release. A second press during that reverse continues from the live value. The recipe is posed, so a new forward reads the live value. Optional hover plays on pointer enter and reverses on pointer exit. A finger on a phone does not send those events, so hover stays quiet there. The recognizer sits on the wrapper and the motion runs on the child. Press takes the properties it writes. Leaving after that take plays toward the hover recipe's authored from.

`Repeat` and `Yoyo` on `Timeline(...)`, and on the opening `Timeline(configure)` call, belong to the whole timeline. The same calls inside `Add` or `Then` belong to that beat. The beat's window includes every cycle, and the next `Then` starts after them. `Repeat(-1)` on a beat loops that beat, so later beats wait.

`Add` places a beat at an absolute time from the timeline start. `Then` offset is milliseconds from the previous beat's end. A positive offset waits. A negative offset overlaps. The start clamps at 0.

`OnBeat` runs when forward playback reaches that named beat, including each later repeat. It does not run during the timeline delay, on seek, or on reverse. `MotionPlayer.At` stays the 0–1 listener. `OnBegin` runs when `Delay` has elapsed and a forward leaves the start. `OnReverseStart` runs when a reverse leg begins, including a press release and a yoyo at the far end. `OnRepeat` runs when another cycle starts, not on the first play, and not for a beat's own `Repeat`. `OnComplete` runs when the run will not continue, including a drag release that rests and a reduce-motion forward. `OnReverseComplete` runs when a reverse arrives at the from pose and will not start another forward. Seek, `Reset`, pinch, and scrub do not run them.

Tests that step time use `new MotionClock(manual: true)` and `Tick`. Do not sleep.

## Samples

A gallery page shows a behavior the library already has. Do not add a library API so that one sample can be written. Each example on a long page has its own Play button, and Play resets that example before it runs.

Do not replace a platform gesture delegate. Setting `UIScrollView.PanGestureRecognizer.ShouldBegin` overwrites MAUI's delegate and crashes on iOS.

## Docs

Update every doc the change affects in the same change.

- Public behavior: `README.md` and the XML comments on the public members.
- Scope or publish rules: `docs/PLAN.md`.
- Agent workflow or build rules: `AGENTS.md`.

Leave a doc describing the behavior that the code now has. A sample-only change updates the README samples paragraph when that paragraph lists the page.

## New features

A new feature follows this order. Wait for the user before each confirmation step.

1. Open a GitHub issue that says what the feature does and how it fits the existing API.
2. Create a branch for that issue.
3. Switch to the branch.
4. Do the work on that branch. Include tests and the doc updates from [Docs](#docs).
5. On confirmation, commit with a message that says what changed and why.
6. On confirmation, push the branch.
7. On confirmation, fast-forward `main`, push `main`, comment on the issue, close it as completed, and delete the branch locally and on origin.

Do not commit, push, merge, or close the issue before that confirmation. Leave the issue open while the branch is unmerged. Read the issue before editing. The open issues are the backlog.
