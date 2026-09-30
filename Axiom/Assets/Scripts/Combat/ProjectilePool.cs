using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Fixed-size projectile pool under its own root object. Nothing is created after
    /// construction; Take returns null when every projectile is in flight.</summary>
    public sealed class ProjectilePool
    {
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly GameObject root;
        private int next;

        public ProjectilePool(Projectile prefab, int size, string rootName)
        {
            root = new GameObject(rootName);
            for (int i = 0; i < size; i++)
            {
                Projectile projectile = Object.Instantiate(prefab, root.transform);
                projectile.gameObject.SetActive(false);
                projectiles.Add(projectile);
            }
        }

        public Projectile Take()
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile candidate = projectiles[(next + i) % projectiles.Count];
                if (candidate.gameObject.activeSelf) continue;
                next = (next + i + 1) % projectiles.Count;
                return candidate;
            }
            return null;
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
        }
    }
}
