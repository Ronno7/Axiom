using UnityEngine;

namespace GeometricFlight.Presentation
{
    public enum DepartureTreatment
    {
        FlyThrough,
        SignalTransfer,
    }

    /// <summary>Presentation samples only; this timeline does not grant flight or change run state.</summary>
    public readonly struct DepartureSample
    {
        public readonly float Distance, Speed, FovSurge, Occlusion;
        public readonly bool TransferReady, Arrived;

        public DepartureSample(float distance, float speed, float fovSurge, float occlusion,
            bool transferReady, bool arrived)
        {
            Distance = distance;
            Speed = speed;
            FovSurge = fovSurge;
            Occlusion = occlusion;
            TransferReady = transferReady;
            Arrived = arrived;
        }
    }

    /// <summary>
    /// Absolute-time sampling keeps replay and 30/60 fps comparisons on the same path. Duration changes
    /// pacing, not the destination or its nonzero arrival velocity. Reduced animation is a separate
    /// quiet transfer, identical for both treatments, rather than a compressed high-speed fly-through.
    /// </summary>
    public static class MenuDepartureTimeline
    {
        public const float FullDistance = 240f;
        public const float ReturnDistance = 40f;
        public const float ArrivalSpeed = 30f;
        public const float IdleSpeed = 5f;
        public const float ReducedDuration = 1f;
        public const float MaxFovSurge = 8f;

        private const float CoverRise = 0.1f;
        private const float CoverHold = 0.08f;
        private const float CoverFall = 0.16f;
        private const float CoveredFrame = 1f / 30f;

        public static float TotalDistance(bool returning) => returning ? ReturnDistance : FullDistance;

        /// <summary>
        /// Full studies allow 6–14 seconds (presets 8/10/12), return studies 2–4 (preset 3).
        /// These bounds retain monotonic Hermite travel with the fixed endpoint speeds.
        /// Nonfinite durations use the profile default; reduced animation always takes one second.
        /// </summary>
        public static float EffectiveDuration(float duration, bool returning, bool reduced)
        {
            if (reduced) return ReducedDuration;
            if (!Finite(duration)) duration = returning ? 3f : 10f;
            return returning ? Mathf.Clamp(duration, 2f, 4f) : Mathf.Clamp(duration, 6f, 14f);
        }

        /// <param name="coverCenter">Fraction of the duration where the signal cover peaks.</param>
        public static DepartureSample Sample(float elapsed, float duration, bool returning, bool reduced,
            DepartureTreatment treatment, float coverCenter = 0.52f)
        {
            // A nonfinite clock is treated as a fresh sample. The upper bound only prevents overflow
            // in adversarial float inputs; it is far beyond any representable real study session.
            float time = Finite(elapsed) ? Mathf.Clamp(elapsed, 0f, float.MaxValue / (ArrivalSpeed * 2f)) : 0f;
            duration = EffectiveDuration(duration, returning, reduced);
            float distance = TotalDistance(returning);
            if (time >= duration)
                return new DepartureSample(distance + ArrivalSpeed * (time - duration), ArrivalSpeed,
                    0f, 0f, false, true);

            if (reduced)
            {
                const float rise = 0.38f;
                const float hold = 0.08f;
                const float fall = ReducedDuration - rise - hold;
                float cover = Cover(time, 0f, rise, hold, fall);
                float transferAt = rise + CoveredFrame;
                bool ready = time >= transferAt && time <= rise + hold;
                return new DepartureSample(time >= transferAt ? distance : 0f, 0f, 0f, cover, ready, false);
            }

            float t = time / duration;
            float t2 = t * t, t3 = t2 * t;
            float startTangent = IdleSpeed * duration, endTangent = ArrivalSpeed * duration;
            float travel = (t3 - 2f * t2 + t) * startTangent
                + (-2f * t3 + 3f * t2) * distance + (t3 - t2) * endTangent;
            float speed = ((3f * t2 - 4f * t + 1f) * startTangent
                + (-6f * t2 + 6f * t) * distance + (3f * t2 - 2f * t) * endTangent) / duration;
            float surge = Mathf.Sin(Mathf.PI * t);
            surge = MaxFovSurge * surge * surge;

            float occlusion = 0f;
            bool transferReady = false;
            if (treatment == DepartureTreatment.SignalTransfer)
            {
                float start = duration * Mathf.Clamp(coverCenter, 0.2f, 0.8f) - (CoverRise + CoverHold + CoverFall) * 0.5f;
                occlusion = Cover(time, start, CoverRise, CoverHold, CoverFall);
                float opaqueStart = start + CoverRise;
                // The runtime must latch the transfer. A ready sample is always fully covered and
                // follows at least one 30 fps frame of full cover. If a long frame skips this window,
                // hold cover for a rendered frame before transferring; do not commit unmasked.
                transferReady = time >= opaqueStart + CoveredFrame && time <= opaqueStart + CoverHold;
            }
            return new DepartureSample(travel, speed, surge, occlusion, transferReady, false);
        }

        private static float Cover(float time, float start, float rise, float hold, float fall)
        {
            float local = time - start;
            if (local <= 0f || local >= rise + hold + fall) return 0f;
            if (local < rise) return Mathf.SmoothStep(0f, 1f, local / rise);
            if (local <= rise + hold) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (local - rise - hold) / fall);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
