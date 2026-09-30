using GeometricFlight.Core;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>Buzzer cues for level events. Each pairs with an on-screen cue: a shield hit with the
    /// ship's blink and dark segment, a throttle zone with its banner. The sensor-lost tone comes from
    /// ControllerService, paired with the HUD warning. Silent without a controller or when muted.</summary>
    [DisallowMultipleComponent]
    public sealed class ControllerFeedback : MonoBehaviour
    {
        [SerializeField] private ShipShield shield;
        [SerializeField] private PlayerFlight flight;

        private ThrottleZone[] zones;
        private ThrottleZone currentZone;

        private void Awake() => zones = FindObjectsByType<ThrottleZone>(FindObjectsSortMode.None);

        private void OnEnable()
        {
            if (shield != null) shield.Changed += OnShieldHit;
        }

        private void OnDisable()
        {
            if (shield != null) shield.Changed -= OnShieldHit;
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
    }
}
