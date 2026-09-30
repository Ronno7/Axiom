# Shooting, aiming, and target feedback (M2)

Open `Assets/Scenes/Prototype.unity`, press Play, click the Game view, and hold
Space to fire. Shots alternate between the two muzzles at 8 per second and converge on
the far reticle. Steering moves the whole firing line with the ship.

## Aiming

Two white diamonds ride ahead of the ship on its center line: `NearReticle` (10 units)
and `AimReticle` (25 units). Together they show the firing line. Every shot is aimed from its
muzzle at `AimReticle` and inherits the ship's current forward speed, so shots meet
the reticle at any throttle setting. Past the reticle, shots from opposite muzzles
cross and slowly diverge. No aim assistance yet: the plan says to tune alignment first.

## Targets and destruction

`Target_01`–`Target_03` each take 3 hits. Each hit flashes the target cyan for
0.08 s. The third hit breaks it once: colliders and visuals turn off, the `Destroyed`
event fires, and `ShatterEffect` throws 20 orange/magenta tetrahedral shards that
slow, spin, and shrink away over 0.9 s. The target then deactivates.
Target colliders are spheres of radius 1.4, slightly larger than the visuals, which gives
a little forgiveness.

Obstacles block shots: the floor and pillars have box colliders; the hexagonal gates have
mesh colliders that `PrototypeMesh` fills with its generated ring mesh at runtime.
Any collider stops a shot. Only a `ShootableTarget` takes damage.

## Tuning

| Component (object) | Setting | Default |
| --- | --- | --- |
| `PlayerWeapon` (10_PlayerPrototype) | Shot Interval | 0.125 s (8/s) |
| | Projectile Speed | 70 u/s relative to the ship |
| | Projectile Lifetime | 1.5 s |
| | Pool Size | 24 (shots are skipped if all are in flight) |
| | Hit Layers | Everything |
| `Projectile` (PlayerProjectile prefab) | Radius | 0.15 (swept hit test) |
| `ShootableTarget` (Target_0x) | Hit Points / Flash color / Flash duration | 3 / cyan / 0.08 s |
| `ShatterEffect` (Target_0x) | Shard count / size / burst speed / duration | 20 / 0.5 / 5–13 / 0.9 s |

Move `AimReticle` to change the convergence distance. The weapon aims at that transform.

## Component responsibilities

- `WeaponRules`: stateless cadence, shot-velocity, and swept-hit rules, covered by tests.
- `PlayerWeapon`: runs after `PlayerFlight` (execution order 10) and reads its
  `CurrentInput.FireHeld`. It never reads the input source a second time. It owns a fixed pool created in
  `Awake` under a `PlayerProjectiles` root object, which is destroyed with the weapon.
- `Projectile`: moves by velocity × delta time and sphere-casts the whole frame's path,
  so fast shots cannot tunnel through thin colliders. Deactivates on impact or at the end of its lifetime.
- `ShootableTarget`: hit points, hit flash through a MaterialPropertyBlock, break-once guard,
  and the `Destroyed` event for later UI and scoring.
- `ShatterEffect`: reusable destruction burst. Creates its shards on first play; nothing
  is instantiated after that.

Colors: `WeaponProjectile` (cyan) for shots and hit flashes, `DestructionPrimary` (orange) and
`DestructionSecondary` (magenta) for shards. Ordinary scenery stays monochrome.

## Shield, enemies, and run flow (M3)

- **Shield** (`ShipShield`, rules in `Shield`): 3 segments; a hit removes one and grants
  1.2 s of protection (the ship blinks). `ShieldDisplay` darkens the hull segments.
- **Crashes** (`ShipHull`): overlapping scenery or a target costs a segment; the ship flies on.
  The hull `BoxCollider` is on the `Player` layer; player shots and crash checks exclude it.
- **Enemy fire** (`EnemyTurret` on Target_02/03): a slow red shot at the player every 1.6 s
  while 12–45 units ahead. It stops when the target breaks.
- **Run flow** (`LevelFlow` in 50_Systems): Escape pauses (and so does losing window focus); completes past
  `CourseEnd` (Z=112); fails when the shield depletes (the ship shatters). Retry reloads the scene.
  Exposes time, targets, shots, accuracy, and shield for the UI.
- Course: `Barrier_Low` (Z=50, fly over) and `Barrier_Left` (Z=88, fly right).

Verified in the editor: 41/41 EditMode tests; fail, complete, pause/resume, and a clean retry in Play Mode.
