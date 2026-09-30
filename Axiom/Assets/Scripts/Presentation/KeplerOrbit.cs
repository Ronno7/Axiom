using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Elliptical two-body orbit with the attracting body at the focus (local origin).
    /// Position over time comes from Kepler's equation M = E − e·sin E, so the satellite
    /// moves fastest at periapsis and slowest at apoapsis, as a real orbit does.</summary>
    public readonly struct KeplerOrbit
    {
        public readonly float SemiMajorAxis;
        public readonly float Eccentricity;
        public readonly float Period;
        public readonly float Phase;
        public readonly Quaternion Plane;

        public KeplerOrbit(float semiMajorAxis, float eccentricity, float period, float phase, Quaternion plane)
        {
            SemiMajorAxis = Mathf.Max(0.001f, semiMajorAxis);
            Eccentricity = Mathf.Clamp(eccentricity, 0f, 0.95f);
            Period = Mathf.Max(0.01f, period);
            Phase = phase;
            Plane = plane;
        }

        public Vector3 PositionAt(float time)
        {
            float meanAnomaly = 2f * Mathf.PI * (time / Period + Phase);
            return PointAtEccentricAnomaly(SolveKepler(meanAnomaly, Eccentricity));
        }

        /// <summary>Point on the ellipse for eccentric anomaly E, in the orbit's plane, focus at the origin.</summary>
        public Vector3 PointAtEccentricAnomaly(float eccentricAnomaly)
        {
            float a = SemiMajorAxis, e = Eccentricity;
            float b = a * Mathf.Sqrt(1f - e * e);
            return Plane * new Vector3(a * (Mathf.Cos(eccentricAnomaly) - e), 0f, b * Mathf.Sin(eccentricAnomaly));
        }

        /// <summary>Solves M = E − e·sin E for E with Newton's method.</summary>
        public static float SolveKepler(float meanAnomaly, float eccentricity)
        {
            float m = Mathf.Repeat(meanAnomaly, 2f * Mathf.PI);
            float e = eccentricity;
            float E = e < 0.8f ? m : Mathf.PI;
            for (int i = 0; i < 8; i++)
            {
                float f = E - e * Mathf.Sin(E) - m;
                E -= f / (1f - e * Mathf.Cos(E));
            }
            return E;
        }
    }
}
