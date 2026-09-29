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

## Verification (2026-09-29, Unity 6000.3.24f1)

- EditMode: 36/36 passed (25 flight + 11 weapon). The weapon tests cover the immediate first shot,
  a held rate independent of frame rate (30/60/144 fps), tap-spamming limits, paused time,
  shots meeting the reticle at 3.6/6/9 speed, a sweep hitting a 0.02-thick wall that a
  7-unit frame step would skip, and a target breaking exactly once.
- Play Mode (frames stepped; `Fire` invoked directly, because simulated keys do not reach
  the Input System while the editor is unfocused): Target_01 and Target_02 each broke after
  exactly 3 shots, with one `Destroyed` event; shots after the break flew on. The shatter and both
  reticles were checked in Game view screenshots. Sweeps confirmed Pillar_1_5, Floor,
  and HexagonalGate_1 stop shots. No shots were live after they expired; the pool stayed at 24. No console errors.
- By the user: holding Space in the Game view (see the plan's session log).
