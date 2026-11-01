using UnityEngine;

namespace GeometricFlight.Player
{
    /// <summary>Marks scenery that destroys the ship on contact (passage walls), as opposed to
    /// obstacles that cost one shield segment and let the ship fly through. ShipHull checks for it.</summary>
    [DisallowMultipleComponent]
    public sealed class LethalSurface : MonoBehaviour
    {
    }
}
