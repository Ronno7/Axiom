using System.Collections.Generic;
using System.Text;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using UnityEditor;
using UnityEngine;

namespace GeometricFlight.Editor
{
    /// <summary>
    /// Tools &gt; Axiom &gt; Check Rail Pacing: flies the open scene's rail (its scoring rings and lattice gaps)
    /// with <see cref="RailCourseCheck"/> at low, cruise and full throttle and logs what each speed makes.
    /// Read-only: changes nothing in the scene.
    /// </summary>
    public static class RailPacingTools
    {
        private const float ShipHalfSpan = 3f;

        [MenuItem("Tools/Axiom/Check Rail Pacing")]
        private static void Check()
        {
            var rings = Object.FindFirstObjectByType<RailRings>();
            var passage = Object.FindFirstObjectByType<EnclosedPassage>();
            if (rings == null || passage == null)
            {
                Debug.LogWarning("Check Rail Pacing needs a scene with rail rings and an enclosed passage.");
                return;
            }
            ArenaFlight flight = rings.GetComponent<ArenaFlight>();

            List<RailCourseCheck.Gate> gates = Gates(rings, passage);
            gates.Sort((a, b) => a.Center.z.CompareTo(b.Center.z));
            var start = new Vector3(0f, flight.transform.position.y, flight.transform.position.z);
            var bounds = new Vector2(passage.Width * 0.5f, passage.Height);
            var report = new StringBuilder($"Rail pacing ({gates.Count} gates):");
            foreach ((string label, float throttle) in new[] { ("LOW", -1f), ("CRUISE", 0f), ("FULL", 1f) })
            {
                RailCourseCheck.Result r = RailCourseCheck.Fly(gates, throttle, flight.Tuning, flight.RailHeadingLimit,
                    start, bounds);
                report.Append($"\n  {label}: {r.Passed}/{r.Total}");
                if (r.Crashed) report.Append($", hit the passage near {gates[r.FirstMiss].Name}");
                var missed = new List<string>();
                for (int i = 0; i < r.Made.Length; i++) if (!r.Made[i]) missed.Add(gates[i].Name);
                if (missed.Count > 0) report.Append(" · missed " + string.Join(", ", missed));
            }
            Debug.Log(report.ToString());
        }

        /// <summary>The rail's rings, the open side of each lattice wall inside the passage, and each closing iris
        /// as a timed gate.</summary>
        public static List<RailCourseCheck.Gate> Gates(RailRings rings, EnclosedPassage passage)
        {
            var gates = new List<RailCourseCheck.Gate>();
            for (int i = 0; i < rings.Count; i++)
                gates.Add(new RailCourseCheck.Gate(rings.Ring(i).name, rings.Ring(i).position, rings.ScoreRadius(i)));

            float half = passage.Width * 0.5f;
            foreach (ElectricHazard wall in Object.FindObjectsByType<ElectricHazard>(FindObjectsSortMode.None))
            {
                Renderer[] parts = wall.GetComponentsInChildren<Renderer>();
                if (parts.Length == 0) continue;
                Bounds b = parts[0].bounds;
                foreach (Renderer part in parts) b.Encapsulate(part.bounds);
                if (b.center.z < passage.MouthZ || b.center.z > passage.EndZ) continue;
                // The gap is the free side between the lattice and the passage wall.
                bool right = half - b.max.x > b.min.x + half;
                float from = right ? b.max.x : -half, to = right ? half : b.min.x;
                gates.Add(new RailCourseCheck.Gate(wall.name, new Vector3((from + to) * 0.5f, 16f, b.center.z), 0f,
                    Mathf.Max(0.5f, (to - from) * 0.5f - ShipHalfSpan)));
            }
            foreach (ClosingIris iris in Object.FindObjectsByType<ClosingIris>(FindObjectsSortMode.None))
                if (!iris.Manual) // A manual iris is a seal, not a timed obstacle.
                    gates.Add(new RailCourseCheck.Gate(iris.name, iris.transform.position, 2f, 0f, iris.TriggerDistance,
                        iris.ArrivalWindow()));
            return gates;
        }
    }
}
