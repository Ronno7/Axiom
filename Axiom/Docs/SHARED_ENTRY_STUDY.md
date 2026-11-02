# Shared entry study

Open **Tools > Axiom > Open the Opening**, enter Play Mode and choose **PLAY** (cockpit FIRE also
starts when PLAY is focused). This scene (`Opening`) is the game: the menu, the tutorial rail and the first arena in one
continuous space. It is the first scene in the build settings, and the only one included.

## The entry

The menu sits at the mouth of an enclosed passage over one endless grid (`InfiniteGround`), beneath a dark
cloud deck. The grid and a little dust glide slowly toward the camera (5 m/s), as if drifting over the
desert of the Grid. At PLAY the camera takes that motion over at the same speed, so there is no hitch.

After PLAY, one camera flies from the menu into the passage while the menu sweeps past it, and the dust
streams by. Once the menu has gone (about 2.3 s), a compact SYSTEMS readout opens at top centre. Each row
builds what it names on the beat it prints, starting at the camera and racing ahead:
- floor seams extend from the passage mouth;
- light ribs draw themselves into depth;
- dark walls fill in between, then the roof sweeps forward overhead to meet them.

The passage starts just behind the menu camera, so it assembles around you and races ahead; its starting edge
is never in view.

The dust clears before a bright scan line sweeps down the screen as the camera crosses the passage mouth.
Nothing cuts away. The ship then draws itself and the HUD assembles instrument by instrument (time and
targets, shield and throttle, weapons, input), two short computer reports play, and control begins on the sixteenth
beat (9.4 s) with "WELCOME, USER." at top centre. The passage finishes building ahead during the first
seconds of flight, while the ship's contour trace runs for four more seconds.

The readout only reports real state or the geometry it is building; values such as the input source, MASTER
ARM position and throttle update live. Four code windows pop up at the screen edges in turn:
`grid://passage.rez` while the tunnel forms, `grid://link`, `craft://compile` while the ship renders, and
`craft://flight`. Each streams monospace
pseudo-code for what is happening, with real values filled in, and folds away before control.

The entry currently plays without music. A temporary synthesized score written for it is kept but not assigned:
- a resonant synth bass and low arpeggio build under the passage;
- cellos join, then low brass, timpani and high strings swell at the threshold;
- a full D minor hit lands on control.

It is original placeholder music from `ArtSource/Audio/entry_build.py`. To hear it or any other track, assign
it to the study's **Build Theme**. The Inspector's tempo (96–120 BPM) sets the entry between
8 and 10 seconds; the score is written at 102 BPM.

## The passage and the tutorial rail

The passage is 64 m wide, 34 m tall and about 6 km long: the original course's proportions, scaled to the
current flight speed. Its walls, ceiling and floor are lethal. Course pieces inside it appear only once the
walls around them are built. The lattice bars carry a faint live current (`ElectricHazard`); touching one
costs a shield segment and the ship flies through. The proximity warning works here. The first arena opens
beyond the far end and stays hidden until the camera is inside.

The passage is the authored tutorial rail. One idea per lesson: one short line at top centre names it as it arrives, the course
teaches it, and a gentler repeat follows in silence. A miss never stops the ship. The next ring burns brighter about 200 m ahead with its
distance beside it. Flying through a ring makes it flare and sends a bright outline shooting ahead (`RingPassFlash`, for any rail with
rings); a RINGS readout under the targets counts them (tutorial only). All seven rail lessons are built:

| Distance | What happens |
| --- | --- |
| 0–240 m | Clear lead-in after control |
| 280–520 m | **Steer**: rings left, right, up and down ("FLY THROUGH THE RINGS") |
| 660–990 m | The same rings again with more room |
| 1050 m | Checkpoint: the shield is restored |
| 1190–1400 m | **Gaps**: rings lead to the open side of two lattice walls ("LATTICE COSTS SHIELD · WALLS DESTROY") |
| 1500–1760 m | The same gaps again, other side first |
| 1775 m | Checkpoint |
| 1790–2330 m | **Guns**: "ARM WITH [MASTER ARM] TO FIRE", the SAFE mark pulsing, with 12 s at cruise before the first target. Then "WEAPONS ARMED" and "HOLD [FIRE] TO SHOOT" until the first hit, at three static targets near the flight line, each flagged SHOOT |
| 2450–2650 m | Three more targets, more widely spaced |
| 2740 m | Checkpoint |
| 2820–3240 m | **Slow**: a slow zone and a five-wall lattice weave that only works slowly ("TIGHT TURNS · SLOW DOWN") |
| 3330–3730 m | The same weave again, other side first |
| 3800 m | Checkpoint |
| 3880–4140 m | **Fast**: a fast zone and a closing iris that only a fast ship clears ("IRIS SHUTS ON SLOW SHIPS · FULL THROTTLE") |
| 4220–4490 m | A second iris with a longer run-up |
| 4580 m | Checkpoint |
| 4720–5040 m | **Boost**: an optional lane of rings to boost through ("BOOST") |
| 5140 m | Checkpoint |
| 5330–5430 m | **Return fire**: two turrets that shoot at an approaching ship, flagged SHOOT ("TURRETS SHOOT BACK") |
| 5700–5800 m | Two more |
| 5980 m | Release: the rail seals behind the ship and the arena lessons begin |

Pacing is real. The slow weave makes all five gaps at up to 24 m/s (the zone's recommended band), but only three
at cruise and two at full throttle. The iris starts closing when the ship crosses the line on the floor 160 m
before it, and lets through only ships doing about 34 m/s or more: cruise is too slow. A missed gap or a shut
iris costs a shield; the ship flies on. Lesson turrets fire only while the ship approaches, never from behind.
The line names the right control for keyboard or cockpit, and a checkpoint shows beside the shield gauge as it refills.

**Weapons arrive in order.** The tutorial starts SAFE on the keyboard (a cockpit switch stays where it physically is), and the
weapons panel is on screen from the entry, with missiles at 0/4 and the EMP OFFLINE until their arena lessons issue them. Pressing a
weapon while SAFE says so. If the cockpit is already armed, the lesson acknowledges it and moves on to shooting.

**Checkpoints and retry** (`TutorialCourse`): if the ship is destroyed on the rail, it shatters, the camera
holds ("SIGNAL LOST"), and two seconds later it is rebuilt at the last checkpoint (or the start
of the lead-in). It returns with a full shield, the score as it was there, no stray shots, and weapons
waiting for a fresh press. The run clock keeps going, and the menu entry never replays. Targets already
destroyed stay destroyed, so nothing is counted twice. The slow and fast
zones and the irises reset when a retry starts before them.

**Pacing check:** **Tools > Axiom > Check Rail Pacing** flies the open scene's rings and lattice gaps with a
simple pilot on the real flight model (`RailCourseCheck`) at low, cruise and full throttle and logs what each
speed makes, with each iris as a timed gate. Today: LOW makes everything but the irises; CRUISE misses two gaps
in each weave and both irises; FULL clears the irises but misses three gaps in each weave.

## The first arena

The rail releases the ship at 5980 m. As the hull clears the passage's end, an iris closes over the mouth in
0.4 s. No ship can turn back that fast: an EditMode test flies every reachable exit (both handling modes, any
speed up to boost, any throttle, full stick and twist), and the quickest return takes 0.59 s. From the moment
it starts closing, the iris is a solid wall in exactly its own shape, lethal like the passage walls
(`RailSeal`). Nothing steers the ship.

The arena lessons follow (`ArenaLessons`):

| Step | What happens |
| --- | --- |
| **Free flight** | "WELCOME TO THE ARENA". The target count starts over at 0/13. The next of five rings climbing around a tower is lit, for anyone who wants it. It ends at the last ring or after 30 s. |
| **LOCK** | "MISSILES ONLINE": the bank loads from 0/4 to 4/4, one target arrives at a station ahead, and the line and flag teach it (below) |
| **LOCK again** | "PAINT BOTH · HOLD [LEFT SHIFT]": a pair at another station |
| **EMP** | "EMP ONLINE": the charge is issued and four slow drones arrive through portals at another station. "CLOSE ON THE DRONES", then "PRESS [R]" once three are surely inside the pulse. |
| **Breather** | "SECTOR CLEAR": shield and missiles restored, 8 s of open flight |
| **Encounter** | "PROGRAMS INCOMING", then "CLEAR THE SECTOR": four turrets and two drones as three pairs spread around the arena, each flagged SHOOT in turn |
| **End** | "TRAINING COMPLETE" while the ship flies on, then the results, with MAIN MENU selected |

**Using the space.** The LOCK and EMP steps arrive at one of eight stations on a 520 m ring around the arena's centre, clear of
its towers (`ArenaStations`). Each picks the one that costs the least turn at about 520 m, and avoids the station just used and
any a tower hides. A LOCK step never starts under 480 m while a farther station exists, which leaves time to paint before the
90 m minimum; the EMP, taught up close, can start from 300 m. If every station is more than 110° off the nose, the arrival waits
up to 4 s for the turn. Once its portals show it stays put. So the ship keeps flying forward and the lessons lap the arena,
whether or not the rings were flown. The final wave's pairs sit about 215 m apart inside a pair and 500–700 m between pairs, so a run separates each
fight, and the flag stays on one enemy until it is destroyed.

A LOCK step completes only when missiles launch at a painted lesson target. The volley plays out (up to 4 s)
before the next step, and a miss still counts. LOCK targets arrive through portals, and a target destroyed any
other way (guns, a collision) comes back through its portal. Arena rings flare like the rail's
(`RingGate`).

**Guidance** (see [UI](UI.md#guidance)): a Cyan flag marks the target with LOCK and its distance, or points to it from the screen
edge. One line gives the action, and changes only when the action does: "HOLD [LEFT SHIFT] TO PAINT", then "RELEASE [LEFT SHIFT] TO
FIRE". The brackets show the painting itself. CLOSE TO 650 m, TOO CLOSE, MISSILES RELOADING and USE MISSILES (after a gun kill) name
themselves. The flag steps aside while the missile brackets show that target, and comes back if they lose it. The key chip fills while it
is held, which only means the press arrived.

The EMP prompt waits until a pulse fired then would reach the drones wherever they are on their patrols, with
30 m to spare for the ship's travel. The drones fall to any weapon, and the step ends when they're gone. If the
EMP is spent and drones remain, "PRESS [R] TO RETRY" offers the group again with a fresh charge. The
retry is optional and needs no death. No checkpoint grants an EMP. The drone group and the final wave are
`ArenaEncounter`s that the lessons start and stop, so no further round follows the last clear.

**Retry in the arena:** if the ship is destroyed during these steps, it is rebuilt with a full shield, a restocked missile
bank and EMP if they have been issued (one per life, as in the arena test), no stray shots or locks, and weapons waiting for a
fresh press. It keeps its handling mode. Up to the LOCK steps it is rebuilt just inside the sealed mouth, and from the
EMP group on nearer the fight. A live wave stands down when the ship is destroyed, and the current step starts
again. The rail stays sealed, and the entry never replays.

The arena's placeholder targets are off. MAIN MENU (pause or results) reloads the study at its menu. The results
read TRAINING COMPLETE and count the rail's targets plus those of each completed arena step.

## Flying it

- **Controls and timing:**
  - Steering and throttle are read throughout the entry.
  - Held weapon buttons must be released before they fire.
  - The run timer starts at control, keeping the full 240 m clear lead-in.
- **Boost** is on here for testing ([flight](FLIGHT.md)). Push the throttle into its final segment for a
  short burst and watch the reserve bar under the gauge. On the keyboard, the throttle drops back to
  normal full when the burst ends.
- **Pause:**
  - Escape or cockpit MENU pauses the entry, its construction and its score.
  - Reconnecting the cockpit with MENU held doesn't dismiss the pause.
- **Retry** reloads the study from its menu.
- **Reduced entry animation** in Options is a one-second quiet transfer to the finished passage, without the
  terminals or score.

First and returning players share the same entry.

**Not yet built:**
- progression routing and procedural levels;
- a skip;
- a death sequence.

The entry's reports are captions for now; a calm British female voice is the later target. The rail
obstacles will be reworked later.
