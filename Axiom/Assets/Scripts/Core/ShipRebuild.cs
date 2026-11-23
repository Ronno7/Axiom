using System;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>The ship's way back after a crash, for what displays it (the cockpit LCD).</summary>
    public interface IShipRecovery
    {
        bool Active { get; }
        /// <summary>From the crash to the cut: the camera holds on the breakup.</summary>
        bool SignalLost { get; }
        /// <summary>0 at the cut, 1 when control returns.</summary>
        float BuildProgress { get; }
    }

    /// <summary>
    /// The death sequence every checkpoint retry shares: the camera holds the crash while the
    /// ship breaks up, one cut at <see cref="CutSeconds"/> takes the view to the retry point, the hull rebuilds standing
    /// still while the shield refills, and control returns at <see cref="ControlSeconds"/> with a fresh weapons press
    /// and hit protection. It runs on game time, so a pause holds it at any point. What a course restores around it
    /// (rings, captions, its current step) comes through the two callbacks given to <see cref="Begin"/>.
    /// </summary>
    public sealed class ShipRebuild : IShipRecovery
    {
        public const float CutSeconds = 0.75f, ControlSeconds = 2.5f;
        private const int ImpactRumble = 255, ImpactRumbleMilliseconds = 300;

        private readonly LevelFlow flow;
        private readonly ShipFlight flight;
        private readonly ShipShield shield;
        private readonly PlayerWeapon weapon;
        private readonly MasterArmInput authority;
        private readonly ChaseCamera chase;
        private readonly EntryPresentation presentation;
        private readonly AudioSource sound;
        private float startedAt = -1f;
        private bool placed;
        private Vector3 retryPoint;
        private Action atCut, atControl;

        public ShipRebuild(LevelFlow flow, ShipFlight flight, ShipShield shield, PlayerWeapon weapon,
            MasterArmInput authority, ChaseCamera chase, EntryPresentation presentation = null, AudioSource sound = null)
        {
            this.flow = flow;
            this.flight = flight;
            this.shield = shield;
            this.weapon = weapon;
            this.authority = authority;
            this.chase = chase;
            this.presentation = presentation;
            this.sound = sound;
        }

        public bool Active => startedAt >= 0f;
        public bool SignalLost => Active && !placed;
        public float BuildProgress => Active && placed ? BuildAt(Time.time - startedAt) : 0f;

        /// <summary>The rebuild's progress at a time after the crash: 0 until the cut, 1 at control.</summary>
        public static float BuildAt(float elapsed) =>
            Mathf.Clamp01((elapsed - CutSeconds) / (ControlSeconds - CutSeconds));

        /// <summary>Shield segments shown at a point of the rebuild: they come back one at a time, the last at control.</summary>
        public static int SegmentsAt(float buildProgress, int max) =>
            Mathf.Clamp(Mathf.FloorToInt(buildProgress * max + 1e-4f), 0, max);

        /// <summary>At the crash, after the flow has destroyed the ship.</summary>
        /// <param name="retry">Where the ship is rebuilt.</param>
        /// <param name="onCut">At the cut, with the ship placed: what the course restores (rings, obstacles, stores).</param>
        /// <param name="onControl">When control returns (an arena step starting again).</param>
        public void Begin(Vector3 retry, Action onCut = null, Action onControl = null)
        {
            startedAt = Time.time;
            placed = false;
            retryPoint = retry;
            atCut = onCut;
            atControl = onControl;
            flight.InputLocked = true;
            weapon?.ClearProjectiles();
            if (chase != null) chase.Held = true;
            ControllerService.Current?.PulseRumble(ImpactRumble, ImpactRumbleMilliseconds);
            Play(PlaceholderSounds.ShipImpact);
        }

        /// <summary>Call every frame while <see cref="Active"/>.</summary>
        public void Update()
        {
            if (!Active) return;
            float elapsed = Time.time - startedAt;
            if (!placed && elapsed >= CutSeconds) Place();
            if (!placed) return;
            float build = BuildAt(elapsed);
            presentation?.SampleRebuild(build);
            shield.Refill(SegmentsAt(build, shield.State.Max));
            if (elapsed >= ControlSeconds) Restore();
        }

        private void Place()
        {
            placed = true;
            flight.Respawn(retryPoint);
            flow.ShowShipForRebuild();
            if (chase != null)
            {
                chase.Held = false;
                chase.ResetToTarget();
                chase.Held = true;
            }
            presentation?.BeginRebuild();
            atCut?.Invoke();
            Play(PlaceholderSounds.ShipBuild);
        }

        private void Restore()
        {
            startedAt = -1f;
            presentation?.EndRebuild();
            flow.SetShipDestroyed(false);
            shield.Restore(protect: true);
            weapon?.ClearProjectiles();
            authority?.RequireRelease();
            flight.InputLocked = false;
            if (chase != null)
            {
                chase.Held = false;
                chase.ResetToTarget();
            }
            Action control = atControl;
            atCut = atControl = null;
            control?.Invoke();
        }

        private void Play(AudioClip clip)
        {
            if (sound != null && clip != null) sound.PlayOneShot(clip);
        }
    }
}
