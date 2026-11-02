using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>Buzzer cues for level events. Each pairs with an on-screen cue: a shield hit with the
    /// ship's blink and dark segment, a throttle zone with the gauge's band, a wave's arrival and clearing with
    /// its portals and the guidance panel, MASTER ARM and missile locks with the weapons panel. The sensor-lost tone comes
    /// from ControllerService, paired with the HUD warning. Silent without a controller or when muted.</summary>
    [DisallowMultipleComponent]
    public sealed class ControllerFeedback : MonoBehaviour
    {
        [SerializeField] private ShipShield shield;
        [SerializeField] private ShipFlight flight;
        [Tooltip("Waves whose arrival and clearing sound on the cockpit.")]
        [SerializeField] private ArenaEncounter[] waves = new ArenaEncounter[0];

        private ThrottleZone[] zones;
        private ThrottleZone currentZone;
        private ArenaWavePhase[] wavePhases;
        private MasterArmInput authority;
        private LockOnWeapon missiles;
        private bool armKnown, wasArmed;
        private int lastLocks, lastFired, toneStep = -1;

        private void Awake()
        {
            zones = FindObjectsByType<ThrottleZone>(FindObjectsSortMode.None);
            wavePhases = new ArenaWavePhase[waves.Length];
            authority = flight.GetComponent<MasterArmInput>();
            missiles = flight.GetComponent<LockOnWeapon>();
        }

        private void OnEnable()
        {
            if (shield != null) shield.Hit += OnShieldHit;
        }

        private void OnDisable()
        {
            if (shield != null) shield.Hit -= OnShieldHit;
        }

        private void OnShieldHit(int remaining) =>
            ControllerService.Instance.PlayTone(remaining == 0 ? 110 : 196, remaining == 0 ? 500 : 150);

        private void Update()
        {
            float z = flight.transform.position.z;
            ThrottleZone zone = null;
            foreach (ThrottleZone candidate in zones)
                if (candidate.Contains(z)) { zone = candidate; break; }
            if (zone == currentZone) return;
            currentZone = zone;
            if (zone != null) ControllerService.Instance.PlayTone(zone.Type == ThrottleZone.Kind.Slow ? 523 : 1047, 90);
        }

        private void LateUpdate()
        {
            SoundWavePhases();
            SoundWeapons();
        }

        /// <summary>Arming answers, a press while SAFE is refused aloud, and painting rises in pitch. Quiet until the
        /// entry hands the weapons over.</summary>
        private void SoundWeapons()
        {
            if (!authority.WeaponsAvailable)
            {
                armKnown = false;
                return;
            }
            ControllerService service = ControllerService.Instance;
            bool armed = authority.Armed;
            if (armKnown && armed != wasArmed) service.PlayTone(armed ? 880 : 330, armed ? 160 : 90);
            armKnown = true;
            wasArmed = armed;
            if (authority.RefusedThisFrame) service.PlayTone(180, 140);
            MissileLockState state = missiles.State;
            int step = state.IsPainting ? state.Locks.Count * 4 + Mathf.FloorToInt(state.Progress * 3f) : -1;
            if (missiles.MissilesFired > lastFired) service.PlayTone(660, 120);
            else if (state.Locks.Count > lastLocks || step > toneStep)
                service.PlayTone(440 + Mathf.Max(0, step) * 35, 55);
            toneStep = step;
            lastLocks = state.Locks.Count;
            lastFired = missiles.MissilesFired;
        }

        private void SoundWavePhases()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                ArenaWaveState state = waves[i].State;
                if (state == null || state.Phase == wavePhases[i]) continue;
                wavePhases[i] = state.Phase;
                if (state.Phase == ArenaWavePhase.Incoming) ControllerService.Instance.PlayTone(440, 180);
                else if (state.Phase == ArenaWavePhase.Cleared) ControllerService.Instance.PlayTone(1320, 220);
            }
        }
    }
}
