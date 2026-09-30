using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Torque-free rigid body. Euler's equations, integrated with RK4 in the body frame:
    ///   I₁ω̇₁ = (I₂ − I₃) ω₂ω₃,  I₂ω̇₂ = (I₃ − I₁) ω₃ω₁,  I₃ω̇₃ = (I₁ − I₂) ω₁ω₂.
    /// A spin mostly about the intermediate axis is unstable and periodically flips over
    /// (the Dzhanibekov / tennis-racket effect), which gives constructs a lifelike tumble.</summary>
    public sealed class TumblingBody
    {
        private const float MaxStep = 1f / 120f;

        public Vector3 Inertia { get; }
        /// <summary>Angular velocity in the body frame, radians per second.</summary>
        public Vector3 AngularVelocity { get; private set; }
        public Quaternion Orientation { get; private set; }

        public TumblingBody(Vector3 inertia, Vector3 bodyAngularVelocity, Quaternion orientation)
        {
            Inertia = new Vector3(Mathf.Max(0.01f, inertia.x), Mathf.Max(0.01f, inertia.y), Mathf.Max(0.01f, inertia.z));
            AngularVelocity = bodyAngularVelocity;
            Orientation = orientation;
        }

        public float KineticEnergy => 0.5f * Vector3.Dot(Vector3.Scale(Inertia, AngularVelocity), AngularVelocity);
        public Vector3 WorldAngularMomentum => Orientation * Vector3.Scale(Inertia, AngularVelocity);

        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            int steps = Mathf.CeilToInt(deltaTime / MaxStep);
            float h = deltaTime / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 w = AngularVelocity;
                Vector3 k1 = Derivative(w);
                Vector3 k2 = Derivative(w + k1 * (h * 0.5f));
                Vector3 k3 = Derivative(w + k2 * (h * 0.5f));
                Vector3 k4 = Derivative(w + k3 * h);
                Vector3 next = w + (k1 + 2f * k2 + 2f * k3 + k4) * (h / 6f);

                // Body-frame rotation over the step, using the midpoint angular velocity.
                Vector3 mid = (w + next) * 0.5f;
                float angle = mid.magnitude * h;
                if (angle > 0f)
                    Orientation = (Orientation * Quaternion.AngleAxis(angle * Mathf.Rad2Deg, mid / mid.magnitude)).normalized;
                AngularVelocity = next;
            }
        }

        private Vector3 Derivative(Vector3 w)
        {
            Vector3 I = Inertia;
            return new Vector3(
                (I.y - I.z) * w.y * w.z / I.x,
                (I.z - I.x) * w.z * w.x / I.y,
                (I.x - I.y) * w.x * w.y / I.z);
        }
    }
}
