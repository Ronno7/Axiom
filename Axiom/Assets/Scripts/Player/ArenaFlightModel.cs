using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace GeometricFlight.Player
{
    /// <summary>Tuning for free flight in an arena. Starting values from the Grid research report; tune in
    /// Play Mode.</summary>
    [Serializable]
    public sealed class ArenaFlightTuning
    {
        [Header("Speed")]
        [Tooltip("Cruising speed (throttle on the detent), metres per second.")]
        [Min(1f)] public float cruiseSpeed = 30f;
        [Tooltip("Minimum and maximum speed relative to cruise.")]
        public Vector2 throttleMultipliers = new Vector2(0.6f, 1.5f);
        [Min(0.1f)] public float acceleration = 20f;

        [Header("Turning")]
        [Tooltip("Roll speed at full stick, degrees per second (a full barrel roll takes 360 / this).")]
        [Min(1f)] public float rollRate = 220f;
        [Tooltip("Pitch speed at full stick on the detent, degrees per second (a loop takes 360 / this).")]
        [Min(1f)] public float loopRate = 100f;
        [Tooltip("Pitch speed relative to the detent at minimum (x) and maximum (y) throttle. Below 1 at both ends "
               + "makes the detent the fastest-turning setting; the brake end still gives the smallest circle.")]
        public Vector2 turnAtThrottleEnds = new Vector2(0.85f, 0.65f);
        [Tooltip("Rudder from the twist, degrees per second at full twist.")]
        [FormerlySerializedAs("fullTwistYawRate")]
        [Min(0f)] public float rudderRate = 40f;

        [Header("Energy and stall")]
        [Tooltip("Climbing trades speed for height and diving trades height for speed, metres per second squared "
               + "times the climb (1 = straight up). 0 = off (default): speed follows the throttle only, and there "
               + "is no stall. Try 15.")]
        [Min(0f)] public float gravity = 0f;
        [Tooltip("How strongly the engine pulls speed toward the throttle's setting, per second. With gravity, a "
               + "vertical climb settles at (setting − gravity / this).")]
        [Min(0.05f)] public float thrustResponse = 0.7f;
        [Tooltip("How strongly drag pulls speed back down once a dive has taken it above the throttle's setting, "
               + "per second. Lower than Thrust Response, so dives keep their speed.")]
        [Min(0.01f)] public float dragResponse = 0.3f;
        [Tooltip("Fastest a dive can take the ship, relative to full-throttle speed.")]
        [Min(1f)] public float diveSpeedLimit = 1.4f;
        [Min(0f)] public float minSpeed = 4f;
        [Tooltip("Below this speed the ship stalls: the controls go soft and the nose falls until speed is back "
               + "above Stall Recovery times this.")]
        [Min(0f)] public float stallSpeed = 13f;
        [Min(1f)] public float stallRecovery = 1.35f;
        [Tooltip("Share of control left while stalled.")]
        [Range(0f, 1f)] public float stallControl = 0.2f;
        [Tooltip("How fast the nose falls while stalled, degrees per second (until it points about 45° down).")]
        [Min(0f)] public float stallNoseDrop = 70f;

        [Header("Throttle")]
        [Tooltip("A soft notch: throttle this close to the middle counts as cruise exactly.")]
        [Range(0f, 0.3f)] public float cruiseSnap = 0.08f;

        [Header("Boost")]
        public BoostTuning boost = new BoostTuning();

        [Header("Response curves")]
        [Tooltip("1 = linear. Higher gives finer control near the centre.")]
        [Range(1f, 3f)] public float stickExpo = 1.7f;
        [Range(1f, 3f)] public float twistExpo = 2f;
    }

    /// <summary>A round arena: a floor disc around <see cref="Center"/> with a ceiling.</summary>
    public readonly struct ArenaLimits
    {
        public readonly Vector3 Center;
        public readonly float Radius, MinAltitude, Ceiling;

        public ArenaLimits(Vector3 center, float radius, float minAltitude, float ceiling)
        {
            Center = center;
            Radius = Mathf.Max(1f, radius);
            MinAltitude = minAltitude;
            Ceiling = Mathf.Max(minAltitude + 1f, ceiling);
        }
    }

    /// <summary>The ship in flight. Its orientation is free (loops, rolls, inverted flight) and held in
    /// <see cref="Attitude"/>; yaw, pitch and bank are read from it. A new state faces +Z, level.</summary>
    public struct ArenaFlightState
    {
        public Vector3 Position;
        public float Speed;
        /// <summary>True while the arena edge is turning the ship back.</summary>
        public bool Returning;
        /// <summary>True while the ship is below stall speed.</summary>
        public bool Stalled;
        /// <summary>The throttle-end burst (charge, time left, armed).</summary>
        public BoostState Boost;
        private Quaternion attitude;

        public Quaternion Attitude
        {
            get => attitude.Equals(default(Quaternion)) ? Quaternion.identity : attitude;
            set => attitude = value;
        }

        public Vector3 Forward => Attitude * Vector3.forward;
        /// <summary>Heading in degrees around world up (0 = +Z).</summary>
        public float Yaw => Mathf.Repeat(Mathf.Atan2(Forward.x, Forward.z) * Mathf.Rad2Deg, 360f);
        /// <summary>Nose up is positive, degrees.</summary>
        public float Pitch => Mathf.Asin(Mathf.Clamp(Forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        /// <summary>Right wing down is positive, degrees.</summary>
        public float Bank => Mathf.Asin(Mathf.Clamp(-(Attitude * Vector3.right).y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// The ship's one flight law, stateless, over a floor. The ship always flies forward. The stick rolls (X) and
    /// pitches (Y, positive = nose up) it about its own axes, the twist yaws it, and nothing levels it, so holding
    /// the stick flies barrel rolls and loops (back) or outside loops (forward). Turning is roll-then-pull, as in an
    /// aircraft, and pitch speed peaks on the detent. Near the edge the ship is turned back toward the middle, a nose
    /// pointing into the floor or ceiling is eased level, and it can never leave the arena.
    /// </summary>
    public static class ArenaFlightModel
    {
        /// <summary>Seconds of outward travel the edge turn allows for beyond the turn itself.</summary>
        private const float EdgeReaction = 0.3f;

        /// <summary>Response curve that keeps the sign: |v|^expo.</summary>
        public static float Shape(float value, float expo)
        {
            float magnitude = Mathf.Clamp01(Mathf.Abs(value));
            return Mathf.Sign(value) * Mathf.Pow(magnitude, Mathf.Max(1f, expo));
        }

        /// <summary>Turn rate relative to the detent for a throttle setpoint (-1…+1).</summary>
        public static float TurnMultiplier(float throttle, Vector2 atEnds)
        {
            return throttle < 0f ? Mathf.Lerp(1f, atEnds.x, -throttle) : Mathf.Lerp(1f, atEnds.y, throttle);
        }

        /// <summary>Speed with energy: the engine pulls toward the throttle's setting (at most Acceleration),
        /// and gravity takes speed away in a climb and adds it in a dive (<paramref name="climb"/> is the nose's
        /// vertical component, -1 to +1). With gravity 0 this is the plain constant-rate ramp. While
        /// <paramref name="boosting"/>, the target is the boost speed at the boost acceleration. Returns the frame's
        /// average speed.</summary>
        public static float Energy(ref float speed, float throttle, float climb, ArenaFlightTuning t, float deltaTime,
            bool boosting = false)
        {
            float target = boosting ? t.boost.speed
                : FlightMovement.TargetSpeed(throttle, t.cruiseSpeed, t.throttleMultipliers);
            float acceleration = boosting ? t.boost.acceleration : t.acceleration;
            if (t.gravity <= 0f) return FlightMovement.Accelerate(ref speed, target, acceleration, deltaTime);
            if (deltaTime <= 0f) return speed;
            float previous = speed;
            float response = speed > target ? t.dragResponse : t.thrustResponse;
            float thrust = Mathf.Clamp((target - speed) * response, -acceleration, acceleration);
            float top = Mathf.Max(t.cruiseSpeed * Mathf.Max(1f, t.throttleMultipliers.y) * t.diveSpeedLimit,
                boosting ? t.boost.speed : 0f);
            speed = Mathf.Clamp(speed + (thrust - t.gravity * climb) * deltaTime, t.minSpeed, top);
            return (previous + speed) * 0.5f;
        }

        /// <summary>Stall with hysteresis: starts below Stall Speed, ends above Stall Speed × Stall Recovery.
        /// Never with energy off.</summary>
        public static bool UpdateStall(bool stalled, float speed, ArenaFlightTuning t)
        {
            if (t.gravity <= 0f) return false;
            return stalled ? speed < t.stallSpeed * t.stallRecovery : speed < t.stallSpeed;
        }

        /// <summary>Soft cruise notch: setpoints within <paramref name="snap"/> of the middle become exactly 0, and
        /// the rest is rescaled so both ends are still reachable.</summary>
        public static float SnapCruise(float throttle, float snap)
        {
            float magnitude = Mathf.Abs(throttle);
            if (magnitude <= snap) return 0f;
            return Mathf.Sign(throttle) * Mathf.Clamp01((magnitude - snap) / Mathf.Max(0.0001f, 1f - snap));
        }

        /// <summary>Radius of the turn circle at a speed and turn rate (degrees per second).</summary>
        public static float TurnRadius(float speed, float turnRateDegrees)
        {
            return speed / Mathf.Max(0.01f, turnRateDegrees * Mathf.Deg2Rad);
        }

        /// <summary>One step of flight. A step of no time changes nothing.</summary>
        public static void Fly(ref ArenaFlightState s, Vector2 stick, float twist, float throttle, ArenaFlightTuning t,
            ArenaLimits limits, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            bool boosting = ThrottleBoost.Step(ref s.Boost, throttle, t.boost, deltaTime);
            throttle = ThrottleBoost.Lever(throttle, t.boost);
            float averageSpeed = Energy(ref s.Speed, throttle, (s.Attitude * Vector3.forward).y, t, deltaTime,
                boosting);
            float x = Shape(stick.x, t.stickExpo);
            float y = Shape(stick.y, t.stickExpo);
            float rudder = Shape(twist, t.twistExpo);
            float multiplier = TurnMultiplier(throttle, t.turnAtThrottleEnds);

            // Stall: once below stall speed the controls go soft and the nose falls until it points about 45°
            // down, so gravity can rebuild speed; control returns once speed is well back. Recovery is waiting
            // for the dive (or diving on purpose).
            s.Stalled = UpdateStall(s.Stalled, s.Speed, t);
            float control = s.Stalled ? t.stallControl : 1f;
            s.Attitude *= Quaternion.Euler(-y * t.loopRate * multiplier * control * deltaTime,
                rudder * t.rudderRate * control * deltaTime, -x * t.rollRate * control * deltaTime);
            if (s.Stalled)
            {
                Vector3 nose = s.Attitude * Vector3.forward;
                Vector3 axis = Vector3.Cross(nose, Vector3.down);
                float toDive = Vector3.Angle(nose, Vector3.down) - 45f;
                if (axis.sqrMagnitude > 1e-6f && toDive > 0f)
                    s.Attitude = Quaternion.AngleAxis(Mathf.Min(toDive, t.stallNoseDrop * deltaTime), axis.normalized)
                        * s.Attitude;
            }

            // Arena edge: a ship heading outward is turned back toward the middle once the wall is close enough
            // that it needs the room to turn. Turning until it runs along the wall carries it outward by
            // r·(1 − √(1 − c²)), where r is the turn radius and c how directly it is heading out, plus a moment's
            // reaction. The turn starts gently at twice that distance and is full at it. It is a flat turn about
            // world up, so it works whatever way up the ship is.
            Vector3 offset = s.Position - limits.Center;
            var flat = new Vector2(offset.x, offset.z);
            float distance = flat.magnitude;
            Vector3 forward = s.Attitude * Vector3.forward;
            var flatForward = new Vector2(forward.x, forward.z);
            float outward = distance > 0.01f ? Vector2.Dot(flatForward, flat / distance) : 0f;
            float turn = t.loopRate * multiplier;
            float push = 0f;
            if (outward > 0f)
            {
                float needed = TurnRadius(s.Speed, turn) * (1f - Mathf.Sqrt(1f - Mathf.Min(1f, outward * outward)))
                    + s.Speed * outward * EdgeReaction + 1f;
                push = Mathf.Clamp01((2f * needed - (limits.Radius - distance)) / needed);
            }
            s.Returning = push > 0f;
            if (s.Returning)
            {
                float home = Mathf.Atan2(-flat.x, -flat.y) * Mathf.Rad2Deg;
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                float side = Mathf.Sign(Mathf.DeltaAngle(yaw, home));
                s.Attitude = Quaternion.AngleAxis(side * turn * push * deltaTime, Vector3.up) * s.Attitude;
            }

            // Floor and ceiling: a nose pointing into them is eased toward level.
            forward = s.Attitude * Vector3.forward;
            float altitude = s.Position.y - limits.Center.y;
            float verticalBand = Mathf.Max(1f, s.Speed * 0.5f);
            float aboveFloor = Mathf.Clamp01(1f - (altitude - limits.MinAltitude) / verticalBand);
            float belowCeiling = Mathf.Clamp01(1f - (limits.Ceiling - altitude) / verticalBand);
            float level = forward.y < 0f ? aboveFloor : forward.y > 0f ? belowCeiling : 0f;
            var levelForward = new Vector3(forward.x, 0f, forward.z);
            if (level > 0f && levelForward.sqrMagnitude > 0.0001f)
            {
                Quaternion toLevel = Quaternion.FromToRotation(forward, levelForward.normalized);
                float share = level * (1f - Mathf.Exp(-deltaTime / 0.15f));
                s.Attitude = Quaternion.Slerp(Quaternion.identity, toLevel, share) * s.Attitude;
            }
            s.Attitude = Normalize(s.Attitude);

            s.Position = Contain(s.Position + s.Forward * (averageSpeed * deltaTime), limits);
        }

        private static Vector3 Contain(Vector3 next, ArenaLimits limits)
        {
            next.y = Mathf.Clamp(next.y, limits.Center.y + limits.MinAltitude, limits.Center.y + limits.Ceiling);
            Vector3 offset = next - limits.Center;
            var flat = new Vector2(offset.x, offset.z);
            if (flat.magnitude > limits.Radius)
            {
                flat = flat.normalized * limits.Radius;
                next.x = limits.Center.x + flat.x;
                next.z = limits.Center.z + flat.y;
            }
            return next;
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m > 0f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
        }
    }
}
