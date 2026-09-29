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
            Vector3 toAim = aimPoint - muzzle;
            Vector3 direction = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : Vector3.forward;
            return direction * Mathf.Max(0f, projectileSpeed) + Vector3.forward * Mathf.Max(0f, shipForwardSpeed);
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
