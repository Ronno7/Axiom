#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GeometricFlight.Dev
{
    /// <summary>
    /// A developer overlay for testing: <see cref="DevCheats"/> toggles, time scale, ship and course shortcuts and a live
    /// readout. F1 shows or hides it and F2 toggles god mode. It installs itself in the Editor and in Development builds,
    /// so the scene carries nothing, and a release build doesn't contain it. While any cheat or time scale is on, a line in
    /// the top right corner says so, except during a film render.
    /// </summary>
    [DefaultExecutionOrder(50)] // After the weapons and flight have spent this frame, so refills land the same frame.
    public sealed class DevPanel : MonoBehaviour
    {
        private const float ReferenceHeight = 1080f;
        private const float Width = 330f;
        private static readonly float[] TimeScales = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };

        private LevelFlow flow;
        private ShipFlight flight;
        private ShipShield shield;
        private PlayerWeapon gun;
        private LockOnWeapon missiles;
        private EmpWeapon emp;
        private TutorialCourse course;
        private ArenaLessons lessons;

        private bool open;
        private float timeScale = 1f;
        private float smoothedDelta = 1f / 60f;
        private Rect window = new Rect(16f, 16f, Width, 0f);
        private GUIStyle header, readout, corner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject(nameof(DevPanel));
            DontDestroyOnLoad(go);
            go.AddComponent<DevPanel>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void Start() => FindParts();
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => FindParts();

        private void FindParts()
        {
            flow = FindAnyObjectByType<LevelFlow>();
            flight = FindAnyObjectByType<ShipFlight>();
            shield = flight != null ? flight.GetComponent<ShipShield>() : null;
            gun = flight != null ? flight.GetComponent<PlayerWeapon>() : null;
            missiles = flight != null ? flight.GetComponent<LockOnWeapon>() : null;
            emp = flight != null ? flight.GetComponent<EmpWeapon>() : null;
            course = FindAnyObjectByType<TutorialCourse>();
            lessons = FindAnyObjectByType<ArenaLessons>();
        }

        private void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.f1Key.wasPressedThisFrame) open = !open;
                if (keys.f2Key.wasPressedThisFrame) DevCheats.GodMode = !DevCheats.GodMode;
            }

            // Pause stops time and resume sets it back to 1, so the chosen scale is reapplied whenever time runs.
            if (Time.timeScale > 0f && !Mathf.Approximately(Time.timeScale, timeScale)) Time.timeScale = timeScale;

            if (DevCheats.InfiniteMissiles && missiles != null && missiles.State != null
                && missiles.State.Ammo < missiles.State.Capacity) missiles.State.Refill();
            if (DevCheats.InfiniteEmp && emp != null && !emp.Pulsing && !emp.State.Ready && !emp.State.Offline)
                emp.ResetCharge();
            if (DevCheats.InfiniteBoost && flight != null && flight.Boost == BoostStatus.Charging) flight.RefillBoost();
        }

        private void OnGUI()
        {
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            MakeStyles();
            if (open) window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "AXIOM DEV   F1",
                GUILayout.Width(Width));
            else if (!UiClock.FollowGameTime) DrawCorner(Screen.width / scale);
        }

        private void DrawCorner(float screenWidth)
        {
            string text = Summary();
            if (text.Length == 0) return;
            GUI.Label(new Rect(screenWidth - 416f, 8f, 400f, 24f), "DEV  " + text + "   F1", corner);
        }

        private string Summary()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (DevCheats.GodMode) parts.Add("GOD");
            if (DevCheats.InfiniteMissiles) parts.Add("∞ MSL");
            if (DevCheats.InfiniteEmp) parts.Add("∞ EMP");
            if (DevCheats.InfiniteBoost) parts.Add("∞ BOOST");
            if (!Mathf.Approximately(timeScale, 1f)) parts.Add("×" + timeScale);
            return string.Join(" · ", parts);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("CHEATS", header);
            DevCheats.GodMode = GUILayout.Toggle(DevCheats.GodMode, " God mode   F2");
            DevCheats.InfiniteMissiles = GUILayout.Toggle(DevCheats.InfiniteMissiles, " Infinite missiles");
            DevCheats.InfiniteEmp = GUILayout.Toggle(DevCheats.InfiniteEmp, " Infinite EMP");
            DevCheats.InfiniteBoost = GUILayout.Toggle(DevCheats.InfiniteBoost, " Infinite boost");

            GUILayout.Label("TIME SCALE", header);
            GUILayout.BeginHorizontal();
            foreach (float t in TimeScales)
                if (GUILayout.Toggle(Mathf.Approximately(timeScale, t), t.ToString("0.##"), GUI.skin.button)
                    && !Mathf.Approximately(timeScale, t)) SetTimeScale(t);
            GUILayout.EndHorizontal();

            bool flying = flow != null && flight != null && flow.State == RunState.Playing && !flow.EntryHeld;
            GUILayout.Label("SHIP", header);
            GUILayout.BeginHorizontal();
            GUI.enabled = shield != null;
            if (GUILayout.Button("Restore shield")) shield.Restore();
            GUI.enabled = flying && shield != null && shield.TakesHits;
            if (GUILayout.Button("Self-destruct")) SelfDestruct();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = missiles != null && emp != null && (missiles.State.Unloaded || emp.State.Offline);
            if (GUILayout.Button("Issue all weapons")) IssueWeapons();
            GUI.enabled = ShootableTarget.Active.Count > 0;
            if (GUILayout.Button("Destroy targets")) DestroyTargets();
            GUILayout.EndHorizontal();

            GUILayout.Label("COURSE", header);
            bool onRail = flying && !flight.RailReleased;
            GUILayout.BeginHorizontal();
            GUI.enabled = onRail && course != null;
            if (GUILayout.Button("Next checkpoint")) course.SkipToNextCheckpoint();
            if (GUILayout.Button("Skip to arena")) course.SkipToArena();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = flying && lessons != null && lessons.Active && lessons.Current < ArenaLessons.Step.Ending;
            if (GUILayout.Button("Skip arena step")) lessons.SkipStep();
            GUI.enabled = flying;
            if (GUILayout.Button("Complete run")) flow.Complete();
            GUI.enabled = flow != null;
            if (GUILayout.Button("Retry")) flow.Retry();
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Label("READOUT", header);
            GUILayout.Label(Readout(), readout);
            if (GUILayout.Button("Clear all cheats")) { DevCheats.ClearAll(); SetTimeScale(1f); }
            GUI.DragWindow();
        }

        private string Readout()
        {
            string fps = $"{1f / Mathf.Max(1e-4f, smoothedDelta):0} fps";
            if (flow == null || flight == null) return fps + "\nNo run in this scene";
            string state = flow.EntryHeld ? "Entry" : flow.State.ToString();
            string where = !flight.RailReleased ? $"Rail, checkpoint {course?.CheckpointsReached ?? 0}"
                : lessons != null && lessons.Active ? $"Arena, {lessons.Current}" : "Arena";
            Vector3 p = flight.transform.position;
            return $"{fps}   {flow.ElapsedTime:0.0} s   {state}\n" +
                   $"{where}\n" +
                   $"Position {p.x:0} {p.y:0} {p.z:0}   {flight.CurrentSpeed:0} m/s\n" +
                   $"Shield {flow.ShieldRemaining}/{flow.ShieldMax}   Boost {flight.Boost}\n" +
                   $"Missiles {Missiles()}   EMP {Emp()}\n" +
                   $"Targets {flow.TargetsDestroyed}/{flow.TargetsTotal}   Hits {gun?.Hits ?? 0}/{gun?.ShotsFired ?? 0}";
        }

        private string Missiles()
        {
            if (missiles == null || missiles.State == null) return "none";
            return missiles.State.Unloaded ? "not issued" : $"{missiles.State.Ammo}/{missiles.State.Capacity}";
        }

        private string Emp()
        {
            if (emp == null) return "none";
            return emp.State.Offline ? "not issued" : emp.Pulsing ? "pulsing" : emp.State.Ready ? "ready" : "spent";
        }

        private void SetTimeScale(float value)
        {
            timeScale = value;
            if (Time.timeScale > 0f) Time.timeScale = value;
        }

        /// <summary>Destroys the ship even in god mode, to test failure and checkpoint retries.</summary>
        private void SelfDestruct()
        {
            bool wasSuspended = DevCheats.Suspended;
            DevCheats.Suspended = true;
            shield.Destroy();
            DevCheats.Suspended = wasSuspended;
        }

        /// <summary>Issues the missiles and EMP early, as the arena's lessons would. Those lessons then skip announcing them.</summary>
        private void IssueWeapons()
        {
            if (missiles.State.Unloaded) missiles.ResetBank();
            if (emp.State.Offline) emp.ResetCharge();
        }

        private static void DestroyTargets()
        {
            // Destruction removes targets from the registry, so work from a copy.
            foreach (ShootableTarget target in ShootableTarget.Active.ToArray())
                if (target != null && !target.IsDestroyed) target.ApplyHit(target.RemainingHitPoints);
        }

        private void MakeStyles()
        {
            if (header != null) return;
            header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 8, 2) };
            header.normal.textColor = AxiomPalette.Cyan;
            readout = new GUIStyle(GUI.skin.label) { wordWrap = true };
            readout.normal.textColor = AxiomPalette.Text;
            corner = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            corner.normal.textColor = AxiomPalette.Orange;
        }
    }
}
#endif
