using GeometricFlight.Combat;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class WeaponRulesTests
    {
        private const float Interval = 0.125f;

        [Test]
        public void FirstHeldFrameFiresImmediately()
        {
            float cooldown = 0f;
            Assert.That(WeaponRules.ShotsThisFrame(ref cooldown, true, 1f / 60f, Interval), Is.EqualTo(1));
        }

        [Test]
        public void NotHeldNeverFires()
        {
            float cooldown = 0f;
            for (int i = 0; i < 120; i++)
                Assert.That(WeaponRules.ShotsThisFrame(ref cooldown, false, 1f / 60f, Interval), Is.Zero);
        }

        [Test]
        public void HeldRateIsIndependentOfFrameRate()
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float cooldown = 0f;
                int shots = 0;
                for (int frame = 0; frame < fps * 2; frame++)
                    shots += WeaponRules.ShotsThisFrame(ref cooldown, true, 1f / fps, Interval);
                // Two seconds at 8 shots/second, including the immediate first shot.
                Assert.That(shots, Is.InRange(16, 17), $"at {fps} fps");
            }
        }

        [Test]
        public void RapidTappingCannotExceedHeldRate()
        {
            float cooldown = 0f;
            int shots = 0;
            for (int frame = 0; frame < 120; frame++)
                shots += WeaponRules.ShotsThisFrame(ref cooldown, frame % 2 == 0, 1f / 60f, Interval);
            Assert.That(shots, Is.LessThanOrEqualTo(17));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void PausedTimeDoesNotFire(float deltaTime)
        {
            float cooldown = 0f;
            Assert.That(WeaponRules.ShotsThisFrame(ref cooldown, true, deltaTime, Interval), Is.Zero);
        }

        [TestCase(3.6f)]
        [TestCase(6f)]
        [TestCase(9f)]
        public void ShotsMeetTheReticleAtAnyShipSpeed(float shipSpeed)
        {
            // Ship and reticle move together along +Z; the shot must arrive where the reticle is.
            Vector3 muzzle = new Vector3(-0.75f, 0f, 1.1f);
            Vector3 aim = new Vector3(0f, 0f, 25f);
            Vector3 velocity = WeaponRules.ShotVelocity(muzzle, aim, 70f, shipSpeed);
            float lateralTime = (aim.x - muzzle.x) / velocity.x;
            Vector3 shot = muzzle + velocity * lateralTime;
            Vector3 reticle = aim + Vector3.forward * (shipSpeed * lateralTime);
            Assert.That(Vector3.Distance(shot, reticle), Is.LessThan(0.001f));
        }

        [Test]
        public void SweepHitsThinColliderSkippedByOneFrameStep()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.hideFlags = HideFlags.DontSave;
            try
            {
                wall.transform.position = new Vector3(0f, 0f, 5f);
                wall.transform.localScale = new Vector3(2f, 2f, 0.02f);
                Physics.SyncTransforms();
                // A 70 u/s shot at 10 fps moves 7 units, jumping entirely over the wall.
                bool hit = WeaponRules.TrySweep(Vector3.zero, new Vector3(0f, 0f, 7f), 0.15f, ~0, out RaycastHit info);
                Assert.That(hit, Is.True);
                Assert.That(info.collider.gameObject, Is.SameAs(wall));
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void TargetBreaksExactlyOnce()
        {
            var go = new GameObject("TestTarget") { hideFlags = HideFlags.DontSave };
            try
            {
                var target = go.AddComponent<ShootableTarget>();
                // Awake does not run for EditMode AddComponent without ExecuteAlways; invoke it.
                typeof(ShootableTarget).GetMethod("Awake",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(target, null);
                int destroyedEvents = 0;
                target.Destroyed += _ => destroyedEvents++;
                Assert.That(target.ApplyHit(), Is.True);
                Assert.That(target.ApplyHit(), Is.True);
                Assert.That(target.ApplyHit(), Is.True);
                Assert.That(target.ApplyHit(), Is.False);
                Assert.That(target.IsDestroyed, Is.True);
                Assert.That(destroyedEvents, Is.EqualTo(1));
                Assert.That(go.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
