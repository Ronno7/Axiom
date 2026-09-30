using System;
using GeometricFlight.Combat;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GeometricFlight.Core
{
    public enum RunState { Playing, Paused, Completed, Failed }

    /// <summary>Owns one run of a course: pause, completion at the course end, failure when the
    /// shield depletes, and run statistics. UI observes StateChanged and calls the public actions.
    /// Retry reloads the scene, so no actor or listener survives into the next attempt.</summary>
    [DefaultExecutionOrder(20)] // After PlayerFlight (input) and ShipHull (damage) this frame.
    [DisallowMultipleComponent]
    public sealed class LevelFlow : MonoBehaviour
    {
        [SerializeField] private PlayerFlight flight;
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private ShipShield shield;
        [SerializeField] private ShipHull hull;
        [Tooltip("The run completes when the ship passes this Z position.")]
        [SerializeField] private Transform courseEnd;
        [Tooltip("Hidden when the ship is destroyed (aircraft visuals, reticles).")]
        [SerializeField] private GameObject[] hideWhenDestroyed;
        [SerializeField] private ShatterEffect shipDestruction;
        [SerializeField] private string menuScene = "MainMenu";
        [Tooltip("Loaded by Continue. Empty returns to the menu.")]
        [SerializeField] private string nextScene = "";
        [Tooltip("Target completion time in seconds, shown on results. 0 hides it.")]
        [SerializeField, Min(0f)] private float parTime;

        public event Action<RunState> StateChanged;
        public RunState State { get; private set; } = RunState.Playing;
        public float ElapsedTime { get; private set; }
        public int TargetsDestroyed { get; private set; }
        public int TargetsTotal { get; private set; }
        public int ShieldRemaining => shield.State.Current;
        public int ShieldMax => shield.State.Max;
        public int ShotsFired => weapon.ShotsFired;
        public float Accuracy => weapon.ShotsFired == 0 ? 0f : (float)weapon.Hits / weapon.ShotsFired;
        public bool HasNextLevel => !string.IsNullOrEmpty(nextScene);
        public float ParTime => parTime;
        public bool HasPar => parTime > 0f;

        private ShootableTarget[] targets;

        private void Awake()
        {
            Time.timeScale = 1f;
            targets = FindObjectsByType<ShootableTarget>(FindObjectsSortMode.None);
            TargetsTotal = targets.Length;
            foreach (ShootableTarget target in targets) target.Destroyed += CountTarget;
            shield.Depleted += Fail;
        }

        private void OnDestroy()
        {
            foreach (ShootableTarget target in targets) if (target != null) target.Destroyed -= CountTarget;
            if (shield != null) shield.Depleted -= Fail;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            bool pausePressed = flight.isActiveAndEnabled && flight.CurrentInput.PausePressed;
            if (State == RunState.Paused)
            {
                if (pausePressed) Resume();
                return;
            }
            if (State != RunState.Playing) return;

            ElapsedTime += Time.deltaTime;
            if (pausePressed) Pause();
            else if (flight.transform.position.z >= courseEnd.position.z) Complete();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Pause();
        }

        public void Pause()
        {
            if (State != RunState.Playing) return;
            Time.timeScale = 0f;
            SetState(RunState.Paused);
        }

        public void Resume()
        {
            if (State != RunState.Paused) return;
            Time.timeScale = 1f;
            SetState(RunState.Playing);
        }

        public void Retry() => Load(SceneManager.GetActiveScene().name);
        public void ExitToMenu() => Load(menuScene);
        public void Continue() => Load(HasNextLevel ? nextScene : menuScene);

        private void Complete()
        {
            // The ship flies on hands-off; keys now belong to the results menu.
            flight.InputLocked = true;
            weapon.enabled = false;
            hull.enabled = false;
            SetState(RunState.Completed);
        }

        private void Fail()
        {
            flight.enabled = false;
            weapon.enabled = false;
            hull.enabled = false;
            foreach (GameObject go in hideWhenDestroyed) go.SetActive(false);
            if (shipDestruction != null) shipDestruction.Play();
            SetState(RunState.Failed);
        }

        private void CountTarget(ShootableTarget _) => TargetsDestroyed++;

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
