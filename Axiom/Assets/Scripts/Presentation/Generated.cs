using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Clean-up for objects the look components generate (never saved), in and out of Play Mode.</summary>
    internal static class Generated
    {
        public static void Release(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
