using System.Collections.Generic;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class MissileLockTests
    {
        private MissileLockState state;
        private HashSet<int> visible;
        [SetUp] public void SetUp()
        {
            state = new MissileLockState();
            visible = new HashSet<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        }

        private int[] Step(float dt, bool held = true, int candidate = 1, bool allowed = true)
            => state.Step(dt, held, allowed, candidate, visible, visible);

        [Test] public void TapDoesNotSpendAmmoOrFire()
        {
            Step(0.1f);
            Assert.That(Step(0.01f, false), Is.Empty);
            Assert.That(state.Ammo, Is.EqualTo(8));
        }

        [Test] public void FirstLockNeedsDwellAndAdditionalLocksAreFaster()
        {
            Step(0.24f);
            Assert.That(state.Locks, Is.Empty);
            Step(0.01f);
            Step(0.15f, candidate: 2);
            Assert.That(state.Locks, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(state.Ammo, Is.EqualTo(8));
            Assert.That(Step(0.01f, false), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(state.Ammo, Is.EqualTo(6));
            Assert.That(Step(0.01f, false), Is.Empty);
        }

        [Test] public void SameTargetCannotConsumeMultipleLocks()
        {
            Step(0.25f);
            Step(1f);
            Assert.That(state.Locks, Has.Count.EqualTo(1));
        }

        [Test] public void SwitchingCandidateRestartsDwell()
        {
            Step(0.2f);
            Step(0.1f, candidate: 2);
            Assert.That(state.Locks, Is.Empty);
            Assert.That(state.Progress, Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test] public void CapacityAndBankBoundTheVolley()
        {
            for (int id = 1; id <= 9; id++) Step(0.25f, candidate: id);
            Assert.That(Step(0.01f, false), Has.Length.EqualTo(8));
            Assert.That(state.Ammo, Is.Zero);
            Step(0.25f);
            Assert.That(state.Locks, Is.Empty);
        }

        [Test] public void RefillStartsAfterSpendingAndCapsAtCapacity()
        {
            Step(10f);
            Step(0.01f, false);
            Step(1.49f, false);
            Assert.That(state.Ammo, Is.EqualTo(7));
            Step(0.02f, false);
            Assert.That(state.Ammo, Is.EqualTo(8));
            Step(100f, false);
            Assert.That(state.Ammo, Is.EqualTo(8));
        }

        [Test] public void LeavingConeRetainsThenExpiresLock()
        {
            Step(0.25f);
            var empty = new HashSet<int>();
            state.Step(2.9f, true, true, 0, visible, empty);
            Assert.That(state.Locks, Has.Count.EqualTo(1));
            state.Step(0.2f, true, true, 0, visible, empty);
            Assert.That(state.Locks, Is.Empty);
        }

        [Test] public void DestroyedObstructedOrOutOfRangeTargetCannotFire()
        {
            Step(0.25f);
            visible.Remove(1);
            Assert.That(Step(0.01f, false), Is.Empty);
            Assert.That(state.Ammo, Is.EqualTo(8));
        }

        [Test] public void CancelRequiresAReleaseBeforePaintingAgain()
        {
            Step(0.25f);
            Step(0f, allowed: false);
            Step(1f);
            Assert.That(state.Locks, Is.Empty);
            Assert.That(Step(0.01f, false), Is.Empty);
            Step(0.25f);
            Assert.That(state.Locks, Has.Count.EqualTo(1));
            Assert.That(state.Ammo, Is.EqualTo(8));
        }

        [Test] public void PausedTimeCannotAcquireRefillOrRelease()
        {
            Step(0.25f);
            Assert.That(Step(0f, false), Is.Empty);
            Assert.That(state.Ammo, Is.EqualTo(8));
            Assert.That(state.Locks, Has.Count.EqualTo(1));
        }

        [Test] public void RespawnResetsBankAndDiscardsOldVolley()
        {
            Step(0.25f);
            Step(0.01f, false);
            Step(0.25f, candidate: 2);
            state.Reset();
            Assert.That(state.Ammo, Is.EqualTo(8));
            Assert.That(state.Locks, Is.Empty);
            Assert.That(Step(0.01f, false), Is.Empty);
        }

        [Test] public void KeyboardLockSurvivesInputMergeWithoutRepurposingControllerButtons()
        {
            var keys = new FlightInput(Vector2.zero, 0f, false, false, lockHeld: true);
            Assert.That(FlightInputMerge.Merge(keys, default).LockHeld, Is.True);
            var controller = new ControllerState(true, Vector2.up, 0.5f, true, true, false);
            Assert.That(FlightInputMerge.Merge(keys, controller).LockHeld, Is.True);
            Assert.That(FlightInputMerge.Merge(default, controller).LockHeld, Is.False);
            // Legacy controller packets have no LOCK; the keyboard remains available.
            Assert.That(new FlightInput(Vector2.zero, 0f, false, false).LockHeld, Is.False);
        }
    }
}
