# Changelog

All notable changes to Axiom. The format follows [Keep a Changelog](https://keepachangelog.com/), and versions
follow the development stages on the [roadmap](README.md#roadmap): **0.1.x prototype**, 0.2 vertical slice,
0.5 alpha, 0.9 beta, 1.0 release. Within a stage, each drop is a patch version. Versions before 0.1.9 were
numbered from the project history.

## [Unreleased]

### Added
- Arcade mode (Options): a vintage arcade-monitor look for the menu and levels, with phosphor glow, CRT
  curvature and scanlines, red colour fringing, rounded tube corners, flicker, static and a rolling band.
- Arcade mode colour settings: exposure, contrast, saturation, colour filter, hue shift and white balance.
- UI showcase page (`docs/`, published with GitHub Pages and linked from the README): UI types, screen flow,
  render layers, Arcade mode, feedback channels and the cockpit, with interactive screenshots.
- The level now has the main menu's look: colour grade, haze, horizon glow, stars, dust that streams past in
  flight, and distant constructs beside the course.

### Changed
- The main-menu floor now fades into the background with no visible edge or horizon line.
- The level floor runs on into the haze, so it meets the sky without an edge.

### Fixed
- Fog on large flat surfaces was uneven, leaving the far end of the floor brighter than the sky.

## [0.1.9] - 2026-10-20 — Main menu presentation

### Added
- 3D menu title: "aXiom" in Earth Orbiter Bold, extruded and chamfered, leaned to match the menu buttons,
  with shadowed sides. A Furore "AXIOM" version is kept as an alternative.
- Main-menu look: colour grade (bloom, vignette, grain), atmosphere (haze, horizon glow, stars, drifting
  dust), interface details (scrim, corner frame, live cockpit status line, key hints, focus marker) and a
  short signal glitch every 8–12 s while the menu is idle. All of it is tunable from one component.
- Optional vector-monitor look for the menu: phosphor glow, red colour fringing and a CRT pass.
- Cockpit controller: buzzer volume, light-string test and an EMP demo in standby.
- Cockpit enclosure: a 1:1 laser-cut file for the top panel and a printable cardboard mock-up template.

### Changed
- Main-menu backdrop recomposed (construct placement, orbits and horizon distance).
- Version numbering adopted (development stages); the build now shows its version on the menu.

### Fixed
- Glitch blocks no longer stay on screen after an event.
- 3D titles no longer import turned around.

### Removed
- Unused Unity template files.

## [0.1.8] - 2026-10-19 — Feedback

### Added
- Throttle-responsive geometric engine exhaust.
- Angular screen-edge pulse when the shield takes a hit.

## [0.1.7] - 2026-10-18 — Cockpit sketch

### Added
- Cockpit controller sketch: stick twist, EMP and MASTER ARM inputs, rumble with a safety timeout, EMP dome
  lamp, and a standby self-test of every input.
- The game accepts both controller generations and can drive rumble and the lamp.

## [0.1.6] - 2026-10-02 — Menu construct

### Added
- Another geometric construct in the main-menu backdrop.

## [0.1.5] - 2026-09-30 — Cockpit design

### Added
- Cockpit box design: 3D model, wiring sheet and a parts list checked against the manufacturers.
- 16×2 LCD on the controller with game-drawn screens for menus, calibration, flight and results.
- Proximity beeps with a pulsing HUD warning.

## [0.1.4] - 2026-09-29 — Controller

### Added
- Arduino controller support: checksummed serial protocol, automatic port search and reconnect, sonar throttle
  with filtering, keyboard fallback, and a pause when the controller is lost.
- Controller calibration screen (Options → Calibrate controller).

## [0.1.3] - 2026-09-29 — Game loop

### Added
- Shield, enemy turret, barriers, and a complete run: pause, level complete, fail, retry and stats.
- Main menu, options (saved), help, HUD, pause and results screens.
- Throttle sections (slow lattice, fast closing iris) with floor cues and a par time.

## [0.1.2] - 2026-09-29 — Combat

### Added
- Hold-to-fire weapon, aim reticles, breakable targets and a shatter effect.

## [0.1.1] - 2026-09-28 — Flight

### Added
- Keyboard flight with throttle and a rear camera.

## [0.1.0] - 2026-09-24 — Prototype scene

### Added
- Prototype scene: geometric ship, corridor and camera.
