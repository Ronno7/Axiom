namespace GeometricFlight.Combat
{
    /// <summary>Anything a projectile can damage. Returns true if the hit counted.</summary>
    public interface IDamageable
    {
        bool ApplyHit(int damage = 1);
    }
}
