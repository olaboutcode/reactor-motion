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

One player owns each property it writes. A later player takes only the properties in its recipe. A path owns both translations, and rotation when `orient` is set. A matrix owns translation, rotation, and scale. Taking one of those releases the whole path or transform.

`EnterExit` plays when the parent keeps rendering the node and passes `present`. Removing the node unmounts it immediately. An exit with `Repeat(-1)` never finishes, so the child never leaves.

`Collection` moves an item that stays and changes place. `FromPlace` starts that translation at the last position. A `CollectionView` recycles cells and does not host these nodes.

`Drag` follows the finger on the axes in the plane and, on release, springs each axis to the nearer end. The nearer end is progress `0.5`. The release already receives the finger speed. A fast flick does not yet choose the far end. That is issue #9.

`Repeat` and `Yoyo` on `Timeline(...)`, and on the opening `Timeline(configure)` call, belong to the whole timeline. The same calls inside `Add` or `Then` belong to that beat. The beat's window includes every cycle, and the next `Then` starts after them. `Repeat(-1)` on a beat loops that beat, so later beats wait.

`Add` places a beat at an absolute time from the timeline start. `Then` offset is milliseconds from the previous beat's end. A positive offset waits. A negative offset overlaps. The start clamps at 0.

`OnBeat` runs when forward playback reaches that named beat, including each later repeat. It does not run during the timeline delay, on seek, or on reverse. `MotionPlayer.At` stays the 0–1 listener.

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
