using GeometricFlight.Core;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>Buzzer cues for level events. Each pairs with an on-screen cue: a shield hit with the
    /// ship's blink and dark segment, a throttle zone with the gauge's band, a wave's arrival and clearing with
    /// its portals and the guidance panel. The sensor-lost tone comes from ControllerService, paired with the HUD
    /// warning. Silent without a controller or when muted.</summary>
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

        private void Awake()
        {
            zones = FindObjectsByType<ThrottleZone>(FindObjectsSortMode.None);
            wavePhases = new ArenaWavePhase[waves.Length];
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
