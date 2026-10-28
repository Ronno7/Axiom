using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Stateless firing and projectile rules, independent of scene objects.</summary>
    public static class WeaponRules
    {
        /// <summary>Hold-to-fire cadence. The first held frame fires immediately; leftover time
        /// carries between frames so the rate does not depend on frame rate. Releasing keeps any
        /// remaining cooldown, so rapid tapping cannot exceed the held rate.</summary>
        public static int ShotsThisFrame(ref float cooldown, bool fireHeld, float deltaTime, float interval)
        {
            if (deltaTime <= 0f) return 0;
            cooldown -= deltaTime;
            if (!fireHeld)
            {
                cooldown = Mathf.Max(0f, cooldown);
                return 0;
            }

            float step = Mathf.Max(0.01f, interval);
            int shots = 0;
            while (cooldown <= 0f)
            {
                shots++;
                cooldown += step;
            }
            return shots;
        }

        /// <summary>World velocity for a shot aimed from a muzzle at a point that travels with the
        /// ship. Inheriting forward speed keeps shots on the reticle at every throttle setting.</summary>
        public static Vector3 ShotVelocity(Vector3 muzzle, Vector3 aimPoint, float projectileSpeed,
            float shipForwardSpeed)
        {
            return ShotVelocity(muzzle, aimPoint, projectileSpeed, Vector3.forward * Mathf.Max(0f, shipForwardSpeed));
        }

        /// <summary>The same for a ship flying in any direction: the shot inherits the ship's full velocity.</summary>
        public static Vector3 ShotVelocity(Vector3 muzzle, Vector3 aimPoint, float projectileSpeed, Vector3 shipVelocity)
        {
            Vector3 toAim = aimPoint - muzzle;
            Vector3 direction = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : Vector3.forward;
            return direction * Mathf.Max(0f, projectileSpeed) + shipVelocity;
        }

        /// <summary>Velocity that carries a shot from the muzzle straight through <paramref name="point"/>
        /// even though it inherits the ship's velocity: the muzzle aims off to cancel the sideways part of
        /// that velocity. Falls back to aiming straight at the point if the ship moves sideways faster than
        /// the shot.</summary>
        public static Vector3 ShotVelocityThrough(Vector3 muzzle, Vector3 point, float projectileSpeed,
            Vector3 shipVelocity)
        {
            Vector3 toPoint = point - muzzle;
            if (toPoint.sqrMagnitude <= 0.0001f) return ShotVelocity(muzzle, point, projectileSpeed, shipVelocity);
            Vector3 line = toPoint.normalized;
            Vector3 sideways = shipVelocity - Vector3.Dot(shipVelocity, line) * line;
            float speed = Mathf.Max(0f, projectileSpeed);
            float along = speed * speed - sideways.sqrMagnitude;
            if (along <= 0f) return ShotVelocity(muzzle, point, projectileSpeed, shipVelocity);
            return line * Mathf.Sqrt(along) - sideways + shipVelocity;
        }

        /// <summary>Aim assist: the candidate nearest the aim direction within <paramref name="coneDegrees"/>
        /// and <paramref name="range"/> of <paramref name="origin"/>, or -1 if none qualifies.</summary>
        public static int PickAssistTarget(Vector3 origin, Vector3 aimDirection, Vector3[] candidates, int count,
            float coneDegrees, float range)
        {
            int best = -1;
            float bestAngle = coneDegrees;
            for (int i = 0; i < count; i++)
            {
                Vector3 to = candidates[i] - origin;
                float distance = to.magnitude;
                if (distance <= 0.001f || distance > range) continue;
                float angle = Vector3.Angle(aimDirection, to);
                if (angle > bestAngle) continue;
                best = i;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>Swept sphere test over one frame's movement, so fast shots cannot pass
        /// through thin colliders between frames.</summary>
        public static bool TrySweep(Vector3 from, Vector3 to, float radius, LayerMask mask, out RaycastHit hit)
        {
            Vector3 travel = to - from;
            float distance = travel.magnitude;
            if (distance <= 0f)
            {
                hit = default;
                return false;
            }
            return Physics.SphereCast(from, Mathf.Max(0.001f, radius), travel / distance, out hit,
                distance, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
