# Shooting, aiming, and target feedback

The weapons and targets of the [opening](OPENING.md). Light strips and pursuing fighters remain planned. Current art
follows the shared [palette](ART_PALETTE.md).

Hold Space to fire. Shots alternate between the two muzzles at 14 per second and converge on the far reticle.

## Aiming

Two white diamonds ride ahead of the ship on its center line: `NearReticle` (10 units)
and `AimReticle` (25 units). Together they show the firing line. Every shot is aimed from its
muzzle at `AimReticle` and inherits the ship's current forward speed, so shots meet
the reticle at any throttle setting. Past the reticle, shots from opposite muzzles
cross and slowly diverge.

## Targets and destruction

A target takes 3 hits. Each hit flashes it for 0.08 s. The third hit breaks it once: colliders and visuals turn off,
the `Destroyed` event fires, and `ShatterEffect` throws 20 tetrahedral shards that slow, spin, and shrink away over
0.9 s. The target then deactivates.

Any collider stops a shot. Only a `ShootableTarget` takes damage.

## Tuning

| Component (object) | Setting | Default |
| --- | --- | --- |
| `PlayerWeapon` (10_StudyPlayer) | Shot Interval | 0.07 s (14/s) |
| | Projectile Speed | 260 m/s relative to the ship |
| | Projectile Lifetime | 1.1 s |
| | Pool Size | 40 (shots are skipped if all are in flight) |
| `Projectile` (PlayerLaser prefab) | Radius / Streak Length | 0.35 (swept hit test) / 12 m |
| `ShootableTarget` | Hit Points / Flash duration | 3 / 0.08 s |
| `ShatterEffect` | Shard count / size / burst speed / duration | 20 / 0.5 / 5–13 / 0.9 s |

Move `AimReticle` to change the convergence distance. The weapon aims at that transform.

## Component responsibilities

- `WeaponRules`: stateless cadence, shot-velocity, and swept-hit rules, covered by tests.
- `PlayerWeapon`: runs after the flight (execution order 10) and reads its
  `CurrentInput.FireHeld`. It never reads the input source a second time. It owns a fixed pool created in
  `Awake` under a `PlayerProjectiles` root object, which is destroyed with the weapon.
- `Projectile`: moves by velocity × delta time and sphere-casts the whole frame's path,
  so fast shots cannot tunnel through thin colliders. Deactivates on impact or at the end of its lifetime.
- `ShootableTarget`: hit points, hit flash through a MaterialPropertyBlock, break-once guard,
  and the `Destroyed` event for later UI and scoring.
- `ShatterEffect`: reusable destruction burst. Creates its shards on first play; nothing
  is instantiated after that.

`WeaponProjectile`, `DestructionPrimary` and `DestructionSecondary` use the current shared palette.
Older captures predate the palette migration.

## Shield, enemies, and run flow

- **Shield** (`ShipShield`, rules in `Shield`): 3 segments; a hit removes one and grants
  1.2 s of protection (the ship blinks). `ShieldDisplay` darkens the hull segments.
- **Crashes** (`ShipHull`): overlapping scenery or a target costs a segment; the ship flies on. Scenery
  marked `LethalSurface` (enclosed passage walls, ceiling and floor) destroys the ship instead.
  The hull `BoxCollider` is on the `Player` layer; player shots and crash checks exclude it.
- **Run flow** (`LevelFlow`): Escape pauses (and so does losing window focus); a run completes past `CourseEnd` or
  when the tutorial finishes, and fails when the shield depletes. Retry reloads the scene.
  A course with checkpoints (the tutorial's `TutorialCourse`) takes a depleted shield over instead: the ship shatters
  and is rebuilt at the last checkpoint without ending the run.
  The first ending is final (`RunRecord`): its numbers are captured once in `LevelFlow.Results`, which the
  results screen and the cockpit LCD both show. Shots already in flight can still shatter a target, but
  nothing counts after the ending, a second ending is ignored, and the shield takes no more hits
  (`ShipShield.TakesHits`).

## MASTER ARM

The cockpit's flip-cover switch is weapons authority (keyboard **T** when no physical switch is selected).
ARM enables the guns, LOCK missiles and the EMP; SAFE inhibits all three and cancels missile painting while
flight continues. It never starts a run or pauses flight, and a switch that is already armed is accepted
without an off/on cycle. Shots already launched and an EMP already fired carry on. After arming, the entry,
pause or a device change, each held weapon button must be released and pressed again; releasing one button
doesn't unblock another. Pressing a weapon while SAFE shows MASTER ARM guidance and an optional short buzz.

`MasterArmInput` wraps the combined input and `WeaponAuthority` inhibits weapon intent without touching
steering, throttle or pause. The tutorial starts
SAFE on the keyboard and teaches arming at its first weapons lesson; a cockpit switch stays where it physically is.

**How MASTER ARM shows:** the state lives where the eye already is and on the weapons panel. While SAFE, the aim diamonds dim
under a quiet X with the word SAFE beside them, and the gun brackets no target, because no shot is coming. The X, the word and
the panel's bar pulse when a lesson calls for arming, and a refused press turns them Ember with a low tone. Arming clears the X
with a flare and a tone, and the word leaves the centre so normal flight stays quiet. The panel's bar reads hatched SAFE or solid
ARMED, and each weapon below it is marked x while it can't fire and framed in `< >` while it can. This mixes the F/A-18's X
through an unusable weapon, the F-16 and F-35 HUD's ARM or SAFE word, and the F-14's ready brackets. (`MissileDisplay`.)

**Stores in the tutorial:** guns come first because missiles and the EMP aren't issued until the arena teaches them. The panel
stays whole: MSL reads 0/4 and doesn't refill (`LockOnWeapon.Unload`), and EMP reads OFFLINE (`EmpWeapon.TakeOffline`). Holding LOCK
then neither paints nor stops the gun.

## Guns and enemies

- **Lasers:** the gun fires `PlayerLaser` at 14 shots a second: 260 m/s plus the ship's own velocity, 1.1 s
  life (about 300 m), a thin cyan beam that grows out of the muzzle to 12 m (`Projectile` → Streak, Streak Length).
- **Shots inherit the ship's full velocity** (`WeaponRules.ShotVelocity` with a velocity vector), so they fly true
  from a turning ship.
- **Aim assist** (`PlayerWeapon` → Aim Assist Cone and Range, 6° and 170 m): a shot
  goes straight through the target nearest the firing line, allowing for the ship's own motion
  (`WeaponRules.ShotVelocityThrough`). Cyan corner brackets frame that target (`MissileDisplay`). The assist is
  instant; a timed "acquiring" display belongs to the LOCK missiles, not the gun.
- **Turrets** use `EnemyTurret` → Measure Distance: they fire while the player is 20–180 m away in any
  direction, 40 m/s shots every 2.2 s, Lead 0.5 (aim halfway along the true intercept with the player's course).
  Rail turrets (Measure Distance off) fire only while the ship approaches them along the course; the tutorial's
  use Lead 1, so a ship that neither dodges nor shoots first gets hit.
- **Lissajous drones** (`LissajousPatrol`) fly a 3D Lissajous figure around their start point
  (x = A·sin(a·ωt + δ), y = B·sin(b·ωt), z = C·sin(c·ωt)) with the whole path drawn faintly, so the player can
  read where they will be. The huge patrolling construct uses the same motion, much slower.
- **Drop lines** (`DropLine`) run from every enemy to the floor with a ring where they land.
- Enemies use the Grid's orange (`ProgramOrange` materials); hits flash white-hot.

### Waves

Each wave in the [first arena](OPENING.md#the-first-arena) is an `ArenaEncounter`. Orange portals mark its spawn points
during an **arrival warning**, and its enemies appear when the warning ends. They cannot be hit or fire before
arriving. Clear them all to finish the wave; there are no individual respawns. Turrets only open fire inside 180 m,
so distant approaches and breaking away from the group provide breathing room.

Clearing a wave removes outstanding combat shots. Dying removes the current wave and its shots, and the retry starts
it again. Pause freezes the warning as well as combat. This is a combat-loop prototype, not the later pursuing
fighter AI.

`ArenaEncounter` owns spawning and counts; `ArenaWaveState` holds the tested phase/timer rules. The child `Portal_*`
transforms set arrival positions, and `ArenaPortalDisplay` only presents that state. Spawned drones keep their
authored path shapes, offset so their first position matches the portal. The encounter makes fresh copies of inactive
templates, preserving scene tuning and references. With **Start On Play** off, as in the tutorial, it waits until a
script starts it and can be stopped after a clear.

### LOCK missiles

Hold **Left Shift** or the cockpit's **LOCK button (D11)** and sweep the reticle across targets; release to
launch one missile at each painted target. The gun stops while LOCK is held. Up to four distinct targets can
be painted, limited by the available bank.
Painting costs nothing; only a released volley spends missiles. The bank restores one missile every 5 s.

Acquisition uses a 12-degree cone at **90–650 m**. Targets inside 90 m cannot be painted and existing locks
are dropped when they enter that distance; the panel says TOO CLOSE TO LOCK. The first target needs 0.45 s
under the reticle, subsequent targets 0.3 s. Locks remain for up to 1.5 s outside the cone, but disappear
immediately if the target dies, leaves range or goes behind scenery. Release only fires at surviving locks.
Pause, focus loss and ship destruction cancel painting; release the held LOCK inputs before beginning again. Respawn restores
the bank and clears missiles in flight.

`LockOnWeapon` on `10_StudyPlayer` exposes the acquisition, bank and missile tuning. `MissileLockState` owns the rules;
`MissileDisplay` reads them for brackets, bank pips and rising buzzer tones (see `UI.md`). `PlayerMissile`
flies at 190 m/s, turns at up to 45 degrees/s, lives for 5 s and deals 3 damage. It follows its original target,
but permanently loses guidance if that target moves outside its 65-degree forward seeker cone. Overshoots
coast onward instead of circling back. It also coasts if the target disappears, and sweeps the whole movement segment against scenery and targets with a
0.3 m radius. A fixed pool covers the bank plus ammunition regenerated during flight.

The cockpit firmware appends a debounced LOCK field to its packet. Older cockpit and kit firmware still work
with keyboard LOCK. If both inputs are held, both must be released to launch. Losing the controller connection
while its LOCK button is held cancels the volley instead of firing it. Existing controller buttons retain
their meanings; the guidance names the physical button when the connected firmware supports LOCK.

### EMP: the big red button

Press the cockpit **EMP dome (D3)** or **R** for a destructive pulse. One charge is available per life;
wave clears do **not** replenish it. Respawning restores the charge, and a held button must be released
before it can fire again. Pause, focus loss and connecting with the button held cannot queue a blast.

After a **0.22 s compression beat**, the pulse expands from the firing position to **180 m over 2.8 s**.
Damage travels with that slower front in every direction. It destroys every enemy reached,
regardless of hit points, and removes hostile shots inside the expanding volume. It passes through scenery;
the player, scenery and friendly projectiles remain intact. Enemies beyond its radius survive.

`EmpWeapon` owns the blast, and `EmpChargeState` owns the charge/press rules. `EmpDisplay` supplies a dominant
blue-white ring in the ship's wing plane, a thin trailing ring, four faint spherical arcs and radial energy
fragments. The blast keeps the position and orientation from discharge while the ship flies away.
A synthesized metallic bass impact and cockpit rumble/buzzer begin when compression ends, followed by a
long sound tail. The expanding fronts fade near their outer edge. A discharge message appears on the LCD.
The weapon HUD shows READY or SPENT, and the dome lamp follows the charge. The existing cockpit firmware
already carries EMP and accepts lamp/rumble commands; no reflash is needed for this feature. It is arena-only.
