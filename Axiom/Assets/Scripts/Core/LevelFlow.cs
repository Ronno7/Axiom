using System;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GeometricFlight.Core
{
    public enum RunState { Playing, Paused, Completed, Failed }

    public enum PauseReason { None, Player, FocusLost, ControllerLost }

    /// <summary>Owns one run of a course: pause, completion at the course end, failure when the
    /// shield depletes, and run statistics. The first ending is final: its numbers are captured in
    /// <see cref="Results"/> before StateChanged fires, and later hits change nothing (<see cref="RunRecord"/>).
    /// UI observes StateChanged and calls the public actions.
    /// Retry reloads the scene, so no actor or listener survives into the next attempt.</summary>
    [DefaultExecutionOrder(20)] // After the ship's flight input and ShipHull damage this frame.
    [DisallowMultipleComponent]
    public sealed class LevelFlow : MonoBehaviour
    {
        [SerializeField] private ShipFlight flight;
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private ShipShield shield;
        [SerializeField] private ShipHull hull;
        [Tooltip("The run completes when the ship passes this Z position.")]
        [SerializeField] private Transform courseEnd;
        [Tooltip("Hidden when the ship is destroyed (aircraft visuals, reticles).")]
        [SerializeField] private GameObject[] hideWhenDestroyed;
        [SerializeField] private ShatterEffect shipDestruction;
        [SerializeField] private string menuScene = "Opening";
        [Tooltip("Loaded by Continue. Empty returns to the menu.")]
        [SerializeField] private string nextScene = "";
        [Tooltip("Target completion time in seconds, shown on results. 0 hides it.")]
        [SerializeField, Min(0f)] private float parTime;
        [Tooltip("Results title for a completed run.")]
        [SerializeField] private string completedTitle = "LEVEL COMPLETE";
        [Tooltip("The entry: keep input and pause live, but hold flight and run time until admission.")]
        [SerializeField] private bool waitForEntry;

        public event Action<RunState> StateChanged;
        public RunState State { get; private set; } = RunState.Playing;
        public PauseReason PauseReason { get; private set; }
        public float ElapsedTime { get; private set; }
        public int TargetsDestroyed => record.TargetsDestroyed;
        public int TargetsTotal { get; private set; }
        public int ShieldRemaining => shield.State.Current;
        public int ShieldMax => shield.State.Max;
        /// <summary>The final numbers, valid once the run is Completed or Failed.</summary>
        public RunResults Results => record.Results;
        public bool HasNextLevel => !string.IsNullOrEmpty(nextScene);
        public float ParTime => parTime;
        public bool HasPar => parTime > 0f;
        public string CompletedTitle => completedTitle;
        public bool EntryHeld { get; private set; }
        public bool EntryStarted { get; private set; }
        /// <summary>Set by a course with checkpoints. Returning true takes over a destroyed ship (shattered and
        /// hidden here; the course rebuilds it with <see cref="SetShipDestroyed"/>) instead of ending the run.</summary>
        public Func<bool> CheckpointRecovery { get; set; }
        /// <summary>Set by the pause menu. The pause input leaves a submenu first (returning true) before it resumes.</summary>
        public Func<bool> LeaveSubmenu { get; set; }

        private ShootableTarget[] targets;
        private readonly RunRecord record = new RunRecord();
        private int controllerConnectedFrame = -1;

        private void Awake()
        {
            Time.timeScale = 1f;
            if (waitForEntry)
            {
                EntryHeld = true;
                flight.HoldForEntry();
            }
            targets = FindObjectsByType<ShootableTarget>(FindObjectsSortMode.None);
            TargetsTotal = targets.Length;
            foreach (ShootableTarget target in targets) target.Destroyed += CountTarget;
            shield.Depleted += Fail;
            ControllerService.Instance.Disconnected += OnControllerLost;
            ControllerService.Instance.Connected += OnControllerConnected;
        }

        private void OnDestroy()
        {
            foreach (ShootableTarget target in targets) if (target != null) target.Destroyed -= CountTarget;
            if (shield != null) shield.Depleted -= Fail;
            if (ControllerService.Current != null)
            {
                ControllerService.Current.Disconnected -= OnControllerLost;
                ControllerService.Current.Connected -= OnControllerConnected;
            }
            Time.timeScale = 1f;
        }

        private void Update() => Step(Time.deltaTime);

        private void Step(float deltaTime)
        {
            if (EntryHeld && !EntryStarted) return;
            bool pausePressed = flight.isActiveAndEnabled && flight.CurrentInput.PausePressed
                && Time.frameCount != controllerConnectedFrame;
            if (State == RunState.Paused)
            {
                if (pausePressed && (LeaveSubmenu == null || !LeaveSubmenu())) Resume();
                return;
            }
            if (State != RunState.Playing) return;

            if (EntryHeld)
            {
                if (pausePressed) Pause();
                return;
            }

            ElapsedTime += deltaTime;
            if (pausePressed) Pause();
            else if (flight.transform.position.z >= courseEnd.position.z) Complete();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Pause(PauseReason.FocusLost);
        }

        // Safe response to losing the controller mid-run: stop, and say so.
        private void OnControllerLost() => Pause(PauseReason.ControllerLost);

        // ControllerService runs before flight input. Its first reconnect packet can turn a held MENU
        // into a press edge; only a later deliberate press may resume the paused run.
        private void OnControllerConnected() => controllerConnectedFrame = Time.frameCount;

        public void Pause() => Pause(PauseReason.Player);

        private void Pause(PauseReason reason)
        {
            if (State != RunState.Playing || (EntryHeld && !EntryStarted)) return;
            PauseReason = reason;
            Time.timeScale = 0f;
            SetState(RunState.Paused);
        }

        public void Resume()
        {
            if (State != RunState.Paused) return;
            PauseReason = PauseReason.None;
            Time.timeScale = 1f;
            SetState(RunState.Playing);
        }

        /// <summary>Accepts PLAY for a held entry. Native pause and recovery now own the run.</summary>
        public bool BeginEntry()
        {
            if (!EntryHeld || EntryStarted || State != RunState.Playing) return false;
            EntryStarted = true;
            return true;
        }

        /// <summary>Commits the staged actor after the entry presenter has matched its camera and HUD.
        /// A pause on this frame keeps the entry held. Normal movement and scored time begin next Update.</summary>
        public bool TryBeginEntryRun()
        {
            if (!EntryHeld || !EntryStarted || State != RunState.Playing) return false;
            flight.ReleaseEntry();
            EntryHeld = false;
            return true;
        }

        public void Retry() => Load(SceneManager.GetActiveScene().name);
        public void ExitToMenu() => Load(menuScene);
        public void Continue() => Load(HasNextLevel ? nextScene : menuScene);

        /// <summary>Ends the run as completed: at the course end, or when a tutorial finishes itself.</summary>
        public void Complete()
        {
            if (!Finish(true)) return;
            // The ship flies on hands-off; keys now belong to the results menu.
            flight.InputLocked = true;
            weapon.enabled = false;
            hull.enabled = false;
            shield.TakesHits = false;
            SetState(RunState.Completed);
        }

        private void Fail()
        {
            if (State == RunState.Playing && !EntryHeld && CheckpointRecovery != null && CheckpointRecovery())
            {
                SetShipDestroyed(true);
                return;
            }
            if (!Finish(false)) return;
            SetShipDestroyed(true);
            SetState(RunState.Failed);
        }

        /// <summary>Shatters and hides the ship, or brings it back for a checkpoint retry.</summary>
        public void SetShipDestroyed(bool destroyed)
        {
            flight.enabled = !destroyed;
            weapon.enabled = !destroyed;
            hull.enabled = !destroyed;
            shield.TakesHits = !destroyed;
            foreach (GameObject go in hideWhenDestroyed) go.SetActive(!destroyed);
            if (destroyed && shipDestruction != null) shipDestruction.Play();
        }

        private bool Finish(bool completed) => record.TryFinish(new RunResults(completed, ElapsedTime,
            record.TargetsDestroyed, TargetsTotal, weapon.ShotsFired, weapon.Hits, shield.State.Current, shield.State.Max));

        private void CountTarget(ShootableTarget _) => record.CountTarget();

        /// <summary>Adds targets that weren't in the scene at the start (a tutorial's completed steps) to the results.</summary>
        public void CountTargets(int destroyed, int total)
        {
            if (record.Finished) return;
            TargetsTotal += Mathf.Max(0, total);
            for (int i = 0; i < destroyed; i++) record.CountTarget();
        }

        private void SetState(RunState next)
        {
            State = next;
            StateChanged?.Invoke(next);
        }

        private static void Load(string scene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }
    }
}
