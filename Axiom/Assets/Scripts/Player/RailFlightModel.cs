using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>
    /// The nose-led straight +Z rail: the arena's rotational controls and speed law, with the nose setting travel inside
    /// a heading limit. No wall contact or arena edge assistance is applied here. It requires an initially valid
    /// forward heading; it never snaps an invalid imported attitude into the permitted cone.
    /// </summary>
    public static class RailFlightModel
    {
        // These limits keep the arena model's edge/floor behavior off the rail.
        // They are not a playable corridor or an invisible containment boundary.
        private static readonly ArenaLimits DistantLimits =
            new ArenaLimits(Vector3.zero, 1e9f, -1e9f, 1e9f);

        /// <summary>A frame that may cross the rail's end: the rail's law up to the release plane, then the arena's for
        /// the rest of the frame, with nothing reset or blended between them. <paramref name="railEnd"/> is where the rail
        /// part of the frame ended. Returns whether the ship was released. A paused frame does nothing, even on the plane.</summary>
        public static bool FlyToRelease(ref ArenaFlightState state, Vector2 stick, float twist, float throttle,
            ArenaFlightTuning tuning, float headingLimitDegrees, float releaseZ, ArenaLimits arenaLimits, float deltaTime,
            out Vector3 railEnd)
        {
            railEnd = state.Position;
            if (deltaTime <= 0f) return false;
            float railTime = TimeToPlane(state, stick, twist, throttle, tuning, headingLimitDegrees, releaseZ, deltaTime);
            Step(ref state, stick, twist, throttle, tuning, headingLimitDegrees, railTime);
            railEnd = state.Position;
            if (state.Position.z < releaseZ) return false;
            ArenaFlightModel.Fly(ref state, stick, twist, throttle, tuning, arenaLimits, deltaTime - railTime);
            return true;
        }

        /// <summary>How much of this frame the rail's law flies before the ship reaches the plane; all of it if it doesn't.</summary>
        private static float TimeToPlane(ArenaFlightState state, Vector2 stick, float twist, float throttle,
            ArenaFlightTuning tuning, float headingLimitDegrees, float planeZ, float deltaTime)
        {
            if (state.Position.z >= planeZ) return 0f;
            ArenaFlightState end = state;
            Step(ref end, stick, twist, throttle, tuning, headingLimitDegrees, deltaTime);
            if (end.Position.z < planeZ) return deltaTime;
            // Monotonic forward travel makes a bounded bisection valid.
            float low = 0f, high = deltaTime;
            for (int i = 0; i < 16; i++)
            {
                float mid = (low + high) * 0.5f;
                ArenaFlightState trial = state;
                Step(ref trial, stick, twist, throttle, tuning, headingLimitDegrees, mid);
                if (trial.Position.z < planeZ) low = mid; else high = mid;
            }
            return high;
        }

        /// <summary>Advances the rail's law and returns actual displacement / deltaTime.</summary>
        public static Vector3 Step(ref ArenaFlightState state, Vector2 stick, float twist, float throttle,
            ArenaFlightTuning tuning, float headingLimitDegrees, float deltaTime)
        {
            if (deltaTime <= 0f) return Vector3.zero;

            ArenaFlightState before = state;
            ArenaFlightModel.Fly(ref state, stick, twist, throttle, tuning, DistantLimits, deltaTime);

            Vector3 requestedTravel = state.Position - before.Position;
            if (ConstrainRequestedHeading(ref state, before, headingLimitDegrees))
            {
                // Use the existing model's integrated distance, including the part of a long frame
                // spent at the target throttle speed; the mean of end speeds would be incorrect.
                state.Position = before.Position + state.Forward * requestedTravel.magnitude;
            }

            return (state.Position - before.Position) / deltaTime;
        }

        private static bool ConstrainRequestedHeading(ref ArenaFlightState state, ArenaFlightState before,
            float headingLimitDegrees)
        {
            Vector3 requested = state.Forward.normalized;
            float limit = Mathf.Clamp(headingLimitDegrees, 0.1f, 89.9f);
            // An invalid imported pose must not be silently corrected. Permit inward recovery while
            // rejecting new outward travel. Admission into a real rail remains the caller's job.
            float allowed = Mathf.Max(limit, Vector3.Angle(before.Forward, Vector3.forward));
            float radians = allowed * Mathf.Deg2Rad;
            if (requested.z >= Mathf.Cos(radians) - 1e-7f) return false;

            Vector3 transverse = Vector3.ProjectOnPlane(requested, Vector3.forward);
            if (transverse.sqrMagnitude < 1e-10f)
            {
                // A very coarse request may point exactly backwards. Its halfway attitude retains
                // which side the requested turn used; no arbitrary centering direction is needed.
                Vector3 halfway = Quaternion.Slerp(before.Rotation, state.Rotation, 0.5f) * Vector3.forward;
                transverse = Vector3.ProjectOnPlane(halfway, Vector3.forward);
            }
            if (transverse.sqrMagnitude < 1e-10f)
                transverse = Vector3.ProjectOnPlane(before.Forward, Vector3.forward);
            if (transverse.sqrMagnitude < 1e-10f) transverse = Vector3.up;

            Vector3 permitted = Vector3.forward * Mathf.Cos(radians) + transverse.normalized * Mathf.Sin(radians);
            if (state.FullRotation)
            {
                // Correct only the requested nose displacement. The shortest rotation carries the
                // requested roll with it; rejecting the whole quaternion would freeze roll at a limit.
                state.Attitude = (Quaternion.FromToRotation(requested, permitted) * state.Attitude).normalized;
                Vector3 right = state.Attitude * Vector3.right;
                state.Bank = Mathf.Asin(Mathf.Clamp(-right.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
            // Bank & Turn keeps its requested bank lag even while heading is limited. Replacing it
            // with an Euler roll extracted from a corrected quaternion changes the control law.
            state.Yaw = Mathf.Repeat(Mathf.Atan2(permitted.x, permitted.z) * Mathf.Rad2Deg, 360f);
            state.Pitch = Mathf.Asin(Mathf.Clamp(permitted.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (!state.FullRotation) state.Attitude = state.Rotation;
            return true;
        }
    }
}
