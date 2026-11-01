using System;
using UnityEngine;

namespace GeometricFlight.Player
{
    public enum BoostStatus
    {
        /// <summary>This ship has no boost gate.</summary>
        Off,
        Ready,
        Active,
        Charging,
        /// <summary>Charged, but the lever must come back below the release point before the next burst.</summary>
        PullBack,
    }

    /// <summary>Boost on the throttle's final travel: a rechargeable burst.</summary>
    [Serializable]
    public sealed class BoostTuning
    {
        [Tooltip("Off: the whole forward travel is ordinary throttle, as before.")]
        public bool enabled;
        [Tooltip("Throttle past this point (0–1, forward of the detent) fires a burst. Normal full throttle is "
               + "reached here.")]
        [Range(0.5f, 0.95f)] public float gate = 0.75f;
        [Tooltip("The lever must come back below this point to re-arm, and pulling back below it ends a burst.")]
        [Range(0.3f, 0.9f)] public float release = 0.65f;
        [Min(1f)] public float speed = 70f;
        [Tooltip("Acceleration while bursting, metres per second squared.")]
        [Min(0.1f)] public float acceleration = 60f;
        [Tooltip("Longest burst, seconds.")]
        [Min(0.1f)] public float duration = 1.5f;
        [Tooltip("Seconds after a burst before the charge starts to refill.")]
        [Min(0f)] public float rechargeDelay = 1f;
        [Tooltip("Seconds to refill from empty.")]
        [Min(0.1f)] public float rechargeTime = 6f;
    }

    /// <summary>Boost state, carried in the flight state so predictions step it too.</summary>
    public struct BoostState
    {
        /// <summary>0 = empty, 1 = full.</summary>
        public float Charge;
        /// <summary>Seconds of burst left; above 0 while bursting.</summary>
        public float Remaining;
        /// <summary>Seconds before the charge starts to refill.</summary>
        public float Delay;
        /// <summary>The lever has been below the release point since the last crossing.</summary>
        public bool Armed;

        public bool Active => Remaining > 0f;
        public static BoostState Full => new BoostState { Charge = 1f };
    }

    /// <summary>
    /// Rules for the throttle-end burst. A burst fires when the lever crosses the gate while armed and fully
    /// charged; it spends the charge, lasts at most <see cref="BoostTuning.duration"/> and ends early if the lever
    /// drops below the release point. A crossing while charging is spent without a burst, so a burst is never
    /// queued. Pure and frame-rate independent.
    /// </summary>
    public static class ThrottleBoost
    {
        /// <summary>The throttle as ordinary speed control (-1…+1): the forward travel reaches full at the gate.</summary>
        public static float Lever(float throttle, BoostTuning t)
        {
            if (t == null || !t.enabled || throttle <= 0f) return throttle;
            return Mathf.Clamp01(throttle / t.gate);
        }

        /// <summary>Advances the boost and returns whether this step bursts. A zero step (pause, held entry)
        /// arms only a lever that is back below the release point, so a lever parked forward never fires on
        /// resume or hand-over.</summary>
        public static bool Step(ref BoostState s, float throttle, BoostTuning t, float deltaTime)
        {
            if (t == null || !t.enabled) return false;
            if (deltaTime <= 0f)
            {
                s.Armed = throttle < t.release;
                return false;
            }

            if (s.Active)
            {
                s.Remaining = throttle < t.release ? 0f : s.Remaining - deltaTime;
                if (s.Remaining < 1e-5f) s.Remaining = 0f;
                if (!s.Active) s.Delay = t.rechargeDelay;
            }
            else if (s.Delay > 0f) s.Delay = Mathf.Max(0f, s.Delay - deltaTime);
            else s.Charge = Mathf.Min(1f, s.Charge + deltaTime / t.rechargeTime);

            if (throttle < t.release) s.Armed = true;
            else if (throttle > t.gate)
            {
                if (s.Armed && s.Charge >= 1f && !s.Active)
                {
                    s.Charge = 0f;
                    s.Remaining = t.duration - deltaTime; // This step is the burst's first.
                }
                s.Armed = false;
            }
            return s.Active;
        }

        /// <summary>Raw setpoint just below the gate (normal full throttle), before the cruise notch is applied.
        /// Where the game can move the throttle (the keyboard), a spent burst returns it here.</summary>
        public static float RetractSetpoint(BoostTuning t, float cruiseSnap)
        {
            float lever = t.gate - 0.002f;
            return cruiseSnap + lever * (1f - cruiseSnap);
        }

        /// <summary>The game pulled the throttle out of the gate itself, so that counts as the reset.</summary>
        public static void Retracted(ref BoostState s) => s.Armed = true;

        public static BoostStatus Status(BoostState s, BoostTuning t)
        {
            if (t == null || !t.enabled) return BoostStatus.Off;
            if (s.Active) return BoostStatus.Active;
            if (s.Charge < 1f) return BoostStatus.Charging;
            return s.Armed ? BoostStatus.Ready : BoostStatus.PullBack;
        }
    }
}
