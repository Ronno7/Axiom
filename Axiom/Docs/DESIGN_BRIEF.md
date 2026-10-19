# Project decisions

## Purpose and scope

This is primarily a user-interface coursework project. Engaging flight and combat support clear demonstrations of diegetic, non-diegetic, spatial, and meta UI. Scope is approximately one month, with three levels initially targeted at two to three minutes each. Completion should be approachable, with optional mastery.

## Agreed direction

- Unity 6.3 with Universal Render Pipeline, starting from the Universal 3D template.
- Third-person camera behind a simple triangular, geometric aircraft.
- Forward flight along a prescribed route with horizontal and vertical steering.
- Abstract geometric architecture and mathematical/sacred-geometry-inspired enemies.
- Mostly black, white, and gray. Saturated color is reserved for weapon effects and enemy destruction.
- Enemies should eventually shatter, burst, or fizzle satisfyingly when destroyed.
- Throttle choices matter at clearly signaled moments rather than demanding constant adjustment.
- Both enemy projectiles and collisions threaten a forgiving shield.
- Required menus: Play, Options with functioning basic settings, Help, and Quit.
- Use the physical controller for as much of the experience as practical; keyboard support is acceptable.
- A small geometric boss at the end is within the proposed scope.
- The game is titled **Axiom**.

## Controller plan

The kit controller (v1: Uno, HC-SR04 throttle, thumbstick, button, buzzer, 16×2 LCD) works and is documented
in `Arduino/README.md`. It is being replaced by a cockpit box (v2): HC-SR04 throttle lever with 8 lights, an
analog stick with FIRE on top and a twist axis, a 60 mm EMP button, MENU, a flip-cover MASTER ARM switch,
the LCD, shield and warning lights, buzzer and rumble. Parts, wiring and what each control does in the game:
`Arduino/cockpit/README.md`.

## Proposed implementation defaults to revisit during playtesting

- Hold to fire with unlimited ammunition.
- Reticle ahead of the aircraft; lateral/vertical movement changes the firing line.
- Mild aim assistance near the reticle and clear hit confirmation.
- Banking is primarily visual so it does not unexpectedly redirect shots.
- Brief damage protection and quick retries.
- Optional performance ranks based on accuracy, damage, and completion time.
- Options for volume, buzzer, sensitivity, inverted vertical steering, and camera shake.
- A short interactive preflight check and a readable level debrief.

## UI intentions

| Type | Candidate use |
| --- | --- |
| Diegetic | Physical shield/status indicators and throttle-responsive geometric engine exhaust |
| Non-diegetic | Menus, score, results, and precise readouts |
| Spatial | World-positioned target brackets or route guidance |
| Meta | Restrained screen-edge feedback for impacts or damage |

Classification depends on what is physically part of the game's fiction. Prioritize readability, consistent interaction, and useful feedback. Keep the center of the flight view clear; do not rely on color alone for essential information.

The exhaust and screen-edge damage pulse are implemented in the prototype; see `UI.md`
for their behavior, tuning, and verification.

## Delivered scope

Progress against the milestones is tracked in `DEVELOPMENT_PLAN.md`. `FLIGHT.md`, `COMBAT.md` and `UI.md`
describe what is built.
