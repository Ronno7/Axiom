# Changelog

## [Unreleased]

## [0.2.1] - 2026-11-02

### Changed
- The entry's wall, roof and floor lines shoot out from behind the camera.

### Removed
- Prototype, arena and study scenes that predate the opening.
- The kit controller, including the game's support for it, and the roomy cockpit model.

### Fixed
- Checkpoints sounded the cockpit's shield hit tone.
- Pause menu options missed changes made on the title screen.
- Esc in a pause submenu resumed flight instead of stepping back.

## [0.2.0] - 2026-11-01

### Added
- Authored opening: menu, tutorial rail and first arena as one continuous flight.
- Entry sequence that builds the passage, ship and HUD around the camera while terminals print the build.
- One endless grid under the whole opening, beneath a dark cloud deck.
- Tutorial rail with seven lessons, checkpoints, lethal walls and electrified obstacles.
- First arena that seals the rail behind the ship and teaches LOCK and the EMP across its whole space.
- Tutorial guidance with one line at top centre, key chips, target flags and a lit next ring.
- Weapons panel laid out like a stores page, with MASTER ARM shown on it and on the reticle.
- Shield gauge above the throttle.
- Nose led rail flight with proximity warnings.
- Rechargeable boost on the throttle's final travel.
- Free flight arena with full rotation flight, lasers, gun aim assist and repeating encounters.
- LOCK missiles for 90–650 m targets.
- EMP: one destructive 180 m pulse per life.
- CRT display option that includes the HUD and menus.
- Twelve color palette across art and UI.
- Keyboard throttle option for use with the cockpit.
- Swept Dart player ship with 4K renders and a materialization preview, not yet in the game.
- UI showcase page.

### Changed
- Roadmap: 0.2 is the playable opening, 0.3 the procedural loop and 0.4 the vertical slice.
- HUD target count starts over for the arena.
- Desktop builds start on the authored opening.
- Help screen covers every control.
- Cockpit connection is an option, off by default.
- Throttle gauge has eight segments with speed in m/s.
- Cockpit stick and mouse hover drive menus like the keyboard.
- Menu and level floors fade into the sky.

### Fixed
- Cockpit LCD dropped to standby mid game, and reconnecting dismissed a paused run.
- HUD, results and LCD could miss updates or disagree.
- Slanted menu buttons misdrew their captions and focus markers.
- Shots flew crooked from a turning ship, and destroyed turrets could fire once more.
- Closing iris ignored the ship's rotated hull.
- Horizon glow painted over nearby scenery, and fog was uneven.

## [0.1.9] - 2026-10-20

### Added
- 3D menu title in Earth Orbiter Bold.
- Main menu look: colour grade, atmosphere, interface details and an idle signal glitch.
- Optional vector monitor look for the menu.
- Cockpit buzzer volume, light string test and EMP demo in standby.
- Laser cut panel file and cardboard mock up template for the cockpit.

### Changed
- Main menu backdrop recomposed.
- Version numbers follow development stages, and the menu shows the build's version.

### Removed
- Unused Unity template files.

### Fixed
- Glitch blocks stayed on screen after an event.
- 3D titles imported turned around.

## 0.1.8 - 2026-10-19

### Added
- Throttle responsive engine exhaust.
- Angular screen edge pulse on a shield hit.

## 0.1.7 - 2026-10-18

### Added
- Cockpit firmware: twist, EMP, MASTER ARM, rumble, dome lamp and a standby self test.

## 0.1.6 - 2026-10-02

### Added
- Another geometric construct in the menu backdrop.

## 0.1.5 - 2026-09-30

### Added
- Cockpit design: 3D model, wiring sheet and parts list.
- 16×2 LCD with game drawn screens.
- Proximity beeps with a HUD warning.

## 0.1.4 - 2026-09-29

### Added
- Arduino controller support, with automatic reconnect and keyboard fallback.
- Controller calibration.

## 0.1.3 - 2026-09-29

### Added
- Shield, turrets, barriers and a complete run: pause, finish, fail, retry and stats.
- Main menu, options, help, HUD, pause and results.
- Throttle sections with floor cues and a par time.

## 0.1.2 - 2026-09-29

### Added
- Hold to fire guns, aim reticles, breakable targets and a shatter effect.

## 0.1.1 - 2026-09-28

### Added
- Keyboard flight with throttle and a rear camera.

## 0.1.0 - 2026-09-24

### Added
- Prototype scene: geometric ship, corridor and camera.

[Unreleased]: https://github.com/Ronno7/Axiom/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/Ronno7/Axiom/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/Ronno7/Axiom/compare/v0.1.9...v0.2.0
[0.1.9]: https://github.com/Ronno7/Axiom/releases/tag/v0.1.9



Based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow the [roadmap](README.md#roadmap), not
Semantic Versioning. Versions before 0.1.9 have no tags.