using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GeometricFlight.Tests
{
    /// <summary>Plays the real Opening scene through the simulated cockpit: finds its parts, reads and presses its UI,
    /// and flies its lessons. Tests jump along the course, so the hull stays off after the entry; real collisions are
    /// left to a person flying it. Every wait goes through <see cref="Wait"/> or <see cref="Until"/>, so
    /// <see cref="Tick"/> runs each frame.</summary>
    internal sealed class OpeningDriver
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string HitTone = "BZ,196";

        private static readonly FieldInfo FlightState = typeof(ArenaFlight).GetField("state", Private);
        private static readonly FieldInfo StepStarted = typeof(ArenaLessons).GetField("stepStarted", Private);
        private static readonly FieldInfo Checkpoints = typeof(TutorialCourse).GetField("checkpoints", Private);

        private readonly string captureFolder;
        private Action restoreSettings;
        private ShipHull hull;
        private MainMenu menu;

        public OpeningDriver(string displayName)
        {
            captureFolder = Path.Combine(Application.dataPath, "..", "Temp", "Captures", "OpeningRun", displayName);
        }

        public LevelFlow Flow { get; private set; }
        public SharedEntryStudy Entry { get; private set; }
        public ArenaFlight Flight { get; private set; }
        public ShipShield Shield { get; private set; }
        public LockOnWeapon Missiles { get; private set; }
        public EmpWeapon Emp { get; private set; }
        public PlayerWeapon Gun { get; private set; }
        public MasterArmInput Authority { get; private set; }
        public ArenaLessons Lessons { get; private set; }
        public TutorialCourse Course { get; private set; }
        public LevelUi Ui { get; private set; }
        public MissileDisplay WeaponsPanel { get; private set; }
        public SimulatedLineTransport Cockpit { get; private set; }
        public List<string> Warnings { get; } = new List<string>();
        /// <summary>Keeps the nose on the current wave's nearest live target.</summary>
        public bool ChaseWave { get; set; }

        private VisualElement HudRoot => Ui.GetComponent<UIDocument>().rootVisualElement;
        private VisualElement MenuRoot => menu.GetComponent<UIDocument>().rootVisualElement;

        public float FirstCheckpointZ => ((TutorialCourse.Checkpoint[])Checkpoints.GetValue(Course))[0].z;
        public int HitTones => Cockpit.SentLines().Count(line => line.StartsWith(HitTone));

        /// <summary>The guidance panel's line as the player reads it.</summary>
        public string Header
        {
            get
            {
                VisualElement line = HudRoot.Q("guidance-address");
                if (line == null || line.resolvedStyle.display == DisplayStyle.None) return "";
                return string.Join(" ", line.Query<Label>().ToList().Select(l => l.text).Where(t => !string.IsNullOrEmpty(t)));
            }
        }

        /// <summary>Starts from the title screen with a fresh simulated cockpit. No real port opens, and the
        /// player's saved settings come back in <see cref="Close"/>.</summary>
        public IEnumerator Load(bool crt)
        {
            Application.runInBackground = true;
            Application.logMessageReceived += RecordWarning;
            bool wasCrt = GameSettings.ArcadeMode, wasReduced = GameSettings.ReducedEntryAnimation;
            bool wasInverted = GameSettings.InvertVertical, wasBuzzer = GameSettings.BuzzerEnabled;
            bool wasCockpit = GameSettings.CockpitEnabled;
            restoreSettings = () =>
            {
                GameSettings.ArcadeMode = wasCrt;
                GameSettings.ReducedEntryAnimation = wasReduced;
                GameSettings.InvertVertical = wasInverted;
                GameSettings.BuzzerEnabled = wasBuzzer;
                GameSettings.CockpitEnabled = wasCockpit;
                GameSettings.Save();
            };
            GameSettings.CockpitEnabled = false;
            GameSettings.ArcadeMode = crt;
            GameSettings.ReducedEntryAnimation = false;
            GameSettings.BuzzerEnabled = true;

            ControllerService.Instance.UseSimulator(true);
            Cockpit = ControllerService.Instance.Simulator;
            Hands();
            Cockpit.SetArmed(false);
            Cockpit.SetLock(false);
            Cockpit.SetEmp(false);

            SceneManager.LoadScene("Opening");
            yield return SceneReady(null);
            yield return Until("the simulated cockpit connects", () => ControllerService.Instance.State.Connected, 5f);
        }

        public void Close()
        {
            Application.logMessageReceived -= RecordWarning;
            restoreSettings?.Invoke();
        }

        /// <summary>Follows a button that reloads the scene.</summary>
        public IEnumerator Reloaded() => SceneReady(Flow);

        private IEnumerator SceneReady(LevelFlow previous)
        {
            yield return null;
            yield return Until("the scene loads", () =>
            {
                var loaded = UnityEngine.Object.FindAnyObjectByType<LevelFlow>();
                return loaded != null && loaded != previous;
            }, 15f);
            yield return Wait(1f); // The menu panels lay out; the entry refuses PLAY before they do.
            FindParts();
        }

        private void FindParts()
        {
            Flow = UnityEngine.Object.FindAnyObjectByType<LevelFlow>();
            Entry = UnityEngine.Object.FindAnyObjectByType<SharedEntryStudy>();
            Flight = UnityEngine.Object.FindAnyObjectByType<ArenaFlight>();
            Lessons = UnityEngine.Object.FindAnyObjectByType<ArenaLessons>();
            Course = UnityEngine.Object.FindAnyObjectByType<TutorialCourse>();
            Ui = UnityEngine.Object.FindAnyObjectByType<LevelUi>();
            menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
            WeaponsPanel = UnityEngine.Object.FindAnyObjectByType<MissileDisplay>();
            Shield = Flight.GetComponent<ShipShield>();
            hull = Flight.GetComponent<ShipHull>();
            Missiles = Flight.GetComponent<LockOnWeapon>();
            Emp = Flight.GetComponent<EmpWeapon>();
            Gun = Flight.GetComponent<PlayerWeapon>();
            Authority = Flight.GetComponent<MasterArmInput>();
        }

        private void RecordWarning(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) Warnings.Add(message);
        }

        public IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                Tick();
                yield return null;
            }
        }

        public IEnumerator Until(string what, Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"Timed out waiting for {what}. {Status}");
                Tick();
                yield return null;
            }
        }

        private string Status => Flow == null ? "No scene yet."
            : $"Step {Lessons.Current}, state {Flow.State}, line '{Header}'.";

        private void Tick()
        {
            if (Flow == null) return;
            if (hull.enabled && !Flow.EntryHeld) hull.enabled = false;
            // Someone switching windows during a run mustn't stop it. LosingFocusPauses checks this directly.
            if (Flow.State == RunState.Paused && Flow.PauseReason == PauseReason.FocusLost) Flow.Resume();
            Vector3 position = Flight.transform.position;
            Assert.That(float.IsNaN(position.x + position.y + position.z), Is.False, "the ship's position stays valid");
            if (ChaseWave && Flight.enabled && Flow.State == RunState.Playing)
            {
                ShootableTarget target = NearestLive();
                if (target != null) Steer(target.transform.position);
            }
        }

        /// <summary>The quickest way from the title screen to the rail: the reduced entry.</summary>
        public IEnumerator ReachControl()
        {
            GameSettings.ReducedEntryAnimation = true;
            yield return Wait(0.5f); // The waiting entry picks the option up.
            ClickMenu("play-button");
            yield return Until("control arrives", () => !Flow.EntryHeld, 5f);
            yield return Wait(0.3f);
        }

        public bool HudShows(string name) => IsShown(HudRoot.Q(name));
        public bool MenuShows(string name) => IsShown(MenuRoot.Q(name));
        public string HudText(string name) => HudRoot.Q<Label>(name).text;
        public Toggle HudToggle(string name) => HudRoot.Q<Toggle>(name);
        public Toggle MenuToggle(string name) => MenuRoot.Q<Toggle>(name);
        public void ClickHud(string name) => Click(HudRoot.Q(name));
        public void ClickMenu(string name) => Click(MenuRoot.Q(name));

        private static bool IsShown(VisualElement element)
        {
            for (VisualElement e = element; e != null; e = e.hierarchy.parent)
                if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return element != null;
        }

        private static void Click(VisualElement button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        /// <summary>Saves a picture of this frame for a visual check.</summary>
        public void Capture(string name)
        {
            Directory.CreateDirectory(captureFolder);
            ScreenCapture.CaptureScreenshot(Path.Combine(captureFolder, name + ".png"));
        }

        /// <summary>Stick centred, throttle mid range, with the given buttons held.</summary>
        public void Hands(bool fire = false, bool menuButton = false) => Cockpit.Set(512, 512, 200, fire, menuButton);

        public IEnumerator PressMenu()
        {
            Hands(menuButton: true);
            yield return Wait(0.15f);
            Hands();
            yield return Wait(0.2f);
        }

        public IEnumerator PressEmp()
        {
            Cockpit.SetEmp(true);
            yield return Wait(0.15f);
            Cockpit.SetEmp(false);
        }

        public IEnumerator Rebuilt() =>
            Until("the ship to be rebuilt", () => !Course.Rebuilding && Flight.enabled, 5f);

        public IEnumerator WaveArrives(string what) => Until(what, () =>
        {
            ArenaEncounter wave = Lessons.CurrentWave;
            return wave != null && wave.State != null && wave.State.Phase == ArenaWavePhase.Combat;
        }, 15f);

        /// <summary>Ends the current step's timed wait.</summary>
        public void SkipStepWait() => StepStarted.SetValue(Lessons, Time.time - 500f);

        public void Jump(float z) => Flight.Respawn(new Vector3(0f, 16f, z));

        public ShootableTarget NearestLive()
        {
            ArenaEncounter wave = Lessons.CurrentWave;
            if (wave == null) return null;
            Vector3 from = Flight.transform.position;
            ShootableTarget best = null;
            foreach (ShootableTarget target in wave.Live)
            {
                if (target == null || target.IsDestroyed) continue;
                if (best == null || (target.transform.position - from).sqrMagnitude
                    < (best.transform.position - from).sqrMagnitude) best = target;
            }
            return best;
        }

        /// <summary>Flies at each live target in turn holding LOCK until all are painted, then releases to launch.</summary>
        public IEnumerator PaintAndLaunch()
        {
            bool holding = false;
            float heldAt = 0f;
            yield return Until("painting and launching", () =>
            {
                ArenaEncounter wave = Lessons.CurrentWave;
                if (wave == null) return true;
                ShootableTarget[] alive = wave.Live.Where(t => t != null && !t.IsDestroyed).ToArray();
                if (alive.Length == 0) return false;
                ShootableTarget unpainted = alive.FirstOrDefault(t => !Missiles.State.Locks.Contains(t.GetInstanceID()));
                ShootableTarget aim = unpainted != null ? unpainted : NearestLive();
                Steer(aim.transform.position);
                if (!holding && Vector3.Distance(aim.transform.position, Flight.transform.position) < 600f)
                {
                    holding = true;
                    heldAt = Time.time;
                    Cockpit.SetLock(true);
                }
                if (!holding || Missiles.State.Locks.Count < alive.Length || Time.time - heldAt <= 1f) return false;
                Cockpit.SetLock(false);
                return true;
            }, 25f);
        }

        private void Steer(Vector3 point)
        {
            var state = (ArenaFlightState)FlightState.GetValue(Flight);
            Vector3 to = point - state.Position;
            if (to.sqrMagnitude > 1f)
                state.Attitude = Quaternion.RotateTowards(state.Attitude,
                    Quaternion.LookRotation(to.normalized, Vector3.up), 80f * Time.deltaTime);
            state.Speed = 40f;
            FlightState.SetValue(Flight, state);
        }
    }
}
