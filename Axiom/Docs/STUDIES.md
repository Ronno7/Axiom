# Earlier studies

Three separate scenes tested parts of the opening before the [shared entry study](SHARED_ENTRY_STUDY.md)
combined them. Each one answered a question; the answers carried forward and the scenes stay for comparison.
None of them are in the build settings, which hold only the `Opening` scene, and the original MainMenu, Prototype and Arena scenes keep
their own flow.

| Scene | Question | Outcome |
| --- | --- | --- |
| Opening prototype | How do the menu, the first weapons lesson and a scored rail connect? | MASTER ARM rules kept (see [combat](COMBAT.md#master-arm)); its construction look and input-gated lessons were replaced |
| Menu departure study | How does the camera leave the menu: continuous flight or a brief signal cover? | Signal transfer at 8–10 seconds; the shared entry has since become one continuous space |
| Rail handling study | Does the nose or the throttle drive travel along a rail? | A / Nose-led, now used in the shared entry |

## Opening prototype

**Tools > Axiom > Open Opening Prototype**, then Play. PLAY runs the first-visit lessons (steering, throttle,
then weapons commissioning on a practice target); SCORED RUN goes straight to a 360 m course with eight gates
(100 points each) and four targets (50 each). Tab skips training; REPLAY TRAINING repeats it. Menu toggles
compare watching the ship complete and reduced animation.

Its short construction-and-fade arrival and lessons that wait for input are not the current design: the
authored tutorial keeps flying and offers another chance instead. `OpeningFlow` owns its stages,
`OpeningCourseScore` its scoring and `OpeningView` its UI.

## Menu departure study

**Tools > Axiom > Open Menu Departure Study**, then Play. A camera study with no playable ship: it flies from
the original menu into moving reference scenery.

| Control | Action |
| --- | --- |
| PLAY / Enter | Start the selected treatment |
| F1 / F2 | A / Continuous flight, B / Signal transfer |
| F3 | Cycle the entry length: 8, 10 or 12 seconds |
| F4 / F5 | Toggle the 3-second return preview / the 1-second reduced transfer |
| Escape / cockpit MENU | Pause |
| Backspace | Back to the menu |

The default is B at 9 seconds over a 240 m path, settling to 30 m/s. The title and buttons stay UI elements
scaled in perspective; they don't become world geometry.

## Rail handling study

**Tools > Axiom > Open Rail Handling Study**, then Play. The original rail HUD and obstacles with the arena
ship: a 240 m clear lead-in, two wall-and-gap groups, four scored hoops and the iris, then a straight
release into an arena at 900 m.

| Control | Action |
| --- | --- |
| F1 / F2 | Fresh A / Nose-led or B / Course-driven trial |
| M | Switch Roll & Pitch and Bank & Turn |
| Backspace | Retry |
| F8 | Write a JSON report to `Axiom/Logs/RailStudy/` |

**A / Nose-led:** the nose sets travel, within 45° of straight ahead, so the ship can't turn back along
the rail; steering hard sideways slows progress. **B / Course-driven:** the throttle sets forward progress and
the nose only adds sideways travel; the ship can point anywhere. Both share the course, throttle range,
weapons and camera. Neither adds hidden steering help at the arena exit.

This study has no one-way exit. The [shared entry study](SHARED_ENTRY_STUDY.md#the-first-arena) seals its rail
behind the ship.
