using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>What every checkpoint retry shares: the destroyed ship waits with its input locked, then comes back at a
    /// given point with a full shield, no stray shots and weapons waiting for a fresh press. Whatever a course restores
    /// around that (rings, captions, its current step) stays with the course.</summary>
    public sealed class ShipRebuild
    {
        private readonly LevelFlow flow;
        private readonly ShipFlight flight;
        private readonly ShipShield shield;
        private readonly PlayerWeapon weapon;
        private readonly MasterArmInput authority;
        private readonly ChaseCamera chase;
        private float rebuildAt = -1f;

        public ShipRebuild(LevelFlow flow, ShipFlight flight, ShipShield shield, PlayerWeapon weapon,
            MasterArmInput authority, ChaseCamera chase)
        {
            this.flow = flow;
            this.flight = flight;
            this.shield = shield;
            this.weapon = weapon;
            this.authority = authority;
            this.chase = chase;
        }

        public bool Pending => rebuildAt >= 0f;
        public bool Due => Pending && Time.time >= rebuildAt;

        public void Begin(float delay)
        {
            flight.InputLocked = true;
            weapon?.ClearProjectiles();
            rebuildAt = Time.time + delay;
        }

        public void Finish(Vector3 position)
        {
            rebuildAt = -1f;
            flight.Respawn(position);
            flow.SetShipDestroyed(false);
            shield.Restore();
            weapon?.ClearProjectiles();
            authority?.RequireRelease();
            flight.InputLocked = false;
            chase?.ResetToTarget();
        }
    }
}
