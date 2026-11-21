# Reactor.Motion plan

In-page view motion for MauiReactor. This repo is motion only.

The public package and namespace is `Reactor.Motion`. The MAUI class library project is `MauiMotion`. Recipes are built with `Motion.Define`. `Play` stays `Play`. There is no `Animate` type.

## Scope

Recipes, `MotionPlayer`, springs, stagger, keyframes, timelines, path, and matrix transforms. Fade, slide, and scale are motions.

Shared-element flights are a future `Reactor.Hero` package. They are not in this repo. `Reactor.Animate` is unchanged until both packages exist.

## Seam

`MotionPins` is internal. A player skips property writes on a pinned view until `Unpin`. This package never pins a view. Publishing `Pin` and `Unpin` is the integration point for `Reactor.Hero`, and it waits until that package is designed.

Do not reference `Reactor.Animate` and `Reactor.Motion` from the same project. Both would export `BindMotion` and a `ColorSpace` enum.

## Publish

Version is `0.1.0-alpha.1`. Do not tag until a nuget.org Trusted Publishing policy exists for this repo:

- owner `olaboutcode`
- repository `olaboutcode/reactor-motion`
- workflow file `release.yml`
- package id `Reactor.Motion`

`release.yml` packs on a `v*` tag and pushes to nuget.org and GitHub Packages. There is no GitLab mirror. `GITLAB_TOKEN` is not set on this repository.
