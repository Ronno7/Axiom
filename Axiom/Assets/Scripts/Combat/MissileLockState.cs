using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Paint distinct targets, retain briefly outside the cone, spend ammo only on release.
    /// The caller supplies visibility; this state has no scene or presentation dependencies.</summary>
    public sealed class MissileLockState
    {
        private readonly List<int> locks = new List<int>();
        private readonly Dictionary<int, float> lostFor = new Dictionary<int, float>();
        private readonly int capacity;
        private readonly float firstLockTime, nextLockTime, retention, refillTime;
        private float progress, refill;
        private bool requireRelease;

        public IReadOnlyList<int> Locks => locks;
        public int Ammo { get; private set; }
        public int Capacity => capacity;
        public int CandidateId { get; private set; }
        public bool IsPainting { get; private set; }
        public float Progress => CandidateId == 0 ? 0f : Mathf.Clamp01(progress / AcquisitionTime);
        public float RefillProgress => Ammo == capacity ? 1f : refill / refillTime;
        private float AcquisitionTime => locks.Count == 0 ? firstLockTime : nextLockTime;

        public MissileLockState(int capacity = 8, float firstLockTime = 0.25f, float nextLockTime = 0.15f,
            float retention = 3f, float refillTime = 1.5f)
        {
            this.capacity = Mathf.Max(1, capacity);
            this.firstLockTime = Mathf.Max(0.01f, firstLockTime);
            this.nextLockTime = Mathf.Max(0.01f, nextLockTime);
            this.retention = Mathf.Max(0f, retention);
            this.refillTime = Mathf.Max(0.01f, refillTime);
            Ammo = this.capacity;
        }

        /// <param name="valid">Alive, in range, and unobstructed targets, including those outside the cone.</param>
        /// <param name="inCone">Visible targets inside the acquisition cone.</param>
        public int[] Step(float deltaTime, bool held, bool allowed, int candidate,
            ISet<int> valid, ISet<int> inCone)
        {
            if (!allowed)
            {
                Cancel();
                return Array.Empty<int>();
            }
            if (deltaTime <= 0f) return Array.Empty<int>();

            if (Ammo < capacity)
            {
                refill += deltaTime;
                while (refill >= refillTime && Ammo < capacity)
                {
                    refill -= refillTime;
                    Ammo++;
                }
            }
            if (Ammo == capacity) refill = 0f;

            for (int i = locks.Count - 1; i >= 0; i--)
            {
                int id = locks[i];
                lostFor[id] = inCone.Contains(id) ? 0f : lostFor[id] + deltaTime;
                if (!valid.Contains(id) || lostFor[id] > retention)
                {
                    locks.RemoveAt(i);
                    lostFor.Remove(id);
                }
            }

            if (requireRelease)
            {
                if (!held) requireRelease = false;
                return Array.Empty<int>();
            }
            if (!held)
            {
                int[] volley = IsPainting ? locks.ToArray() : Array.Empty<int>();
                Ammo -= volley.Length;
                ClearPaint();
                return volley;
            }

            IsPainting = true;
            if (locks.Count >= Ammo || !valid.Contains(candidate) || !inCone.Contains(candidate)
                || locks.Contains(candidate)) candidate = 0;
            if (candidate != CandidateId) progress = 0f;
            CandidateId = candidate;
            if (candidate == 0) return Array.Empty<int>();
            progress += deltaTime;
            if (progress + 0.00001f >= AcquisitionTime)
            {
                locks.Add(candidate);
                lostFor[candidate] = 0f;
                CandidateId = 0;
                progress = 0f;
            }
            return Array.Empty<int>();
        }

        public void Cancel()
        {
            ClearPaint();
            requireRelease = true;
        }

        public void Reset()
        {
            Cancel();
            Ammo = capacity;
            refill = 0f;
        }

        private void ClearPaint()
        {
            locks.Clear();
            lostFor.Clear();
            CandidateId = 0;
            progress = 0f;
            IsPainting = false;
        }
    }
}
