using System;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Core
{
    public enum OpeningStage { Menu, Arrival, Steering, Throttle, Commissioning, Guns, Course, Finished }

    /// <summary>Small authored opening experiment. Owns progression and scoring, while presentation
    /// observes it. Does not replace the existing rail LevelFlow or arena session.</summary>
    [DefaultExecutionOrder(25), DisallowMultipleComponent]
    public sealed class OpeningFlow : MonoBehaviour
    {
        private const string CompletedKey = "axiom.openingPrototype.commissioned";
        [SerializeField] private MainMenu menu;
        [SerializeField] private PlayerFlight flight;
        [SerializeField] private MasterArmInput authority;
        [SerializeField] private Transform[] gates;
        [SerializeField] private GameObject targetTemplate;
        [SerializeField] private float courseLength = 360f;
        [SerializeField] private float gateRadius = 3.2f;
        [SerializeField] private float earlyArrival = 2.4f;
        [SerializeField] private float watchedBeat = 1.1f;
        private readonly System.Collections.Generic.List<ShootableTarget> targets = new System.Collections.Generic.List<ShootableTarget>();
        private readonly OpeningCourseScore score = new OpeningCourseScore();
        private InputAction pause, skip;
        private ShootableTarget practice;
        private Vector3 spawn;
        private float progress, lessonInput, courseStart, stageStarted;
        private bool previousMenuButton;
        private bool previousFire;
        private bool wasArmed;
        private bool waitingForController;

        public event Action StageChanged;
        public OpeningStage Stage { get; private set; }
        public bool Paused { get; private set; }
        public string PauseNotice { get; private set; }
        public bool Tutorial { get; private set; }
        public bool WatchedReveal { get; set; }
        public bool ReducedAnimation { get; set; }
        public bool HasCompletedTutorial => PlayerPrefs.GetInt(CompletedKey, 0) == 1;
        public PlayerFlight Flight => flight;
        public MasterArmInput Authority => authority;
        public int Score => score.Score;
        public int GatesHit => score.GatesHit;
        public float CourseProgress => Stage == OpeningStage.Course
            ? Mathf.Clamp01((flight.transform.position.z - courseStart) / courseLength) : 0f;
        public float ArrivalDuration => ReducedAnimation ? 0.15f : !Tutorial ? 1.2f : earlyArrival + (WatchedReveal ? watchedBeat : 0f);
        public float ArrivalTime => progress;
        public float ArrivalProgress => Mathf.Clamp01(progress / ArrivalDuration);
        public float RefusalUntil { get; private set; }

        private void Awake()
        {
            spawn = flight.transform.position;
            flight.enabled = false;
            targetTemplate.SetActive(false);
            foreach (var gate in gates) gate.gameObject.SetActive(false);
            pause = new InputAction("Opening Pause", InputActionType.Button, "<Keyboard>/escape");
            skip = new InputAction("Skip Training", InputActionType.Button, "<Keyboard>/tab");
        }

        private void OnEnable() { pause.Enable(); skip.Enable(); }
        private void OnDisable() { pause.Disable(); skip.Disable(); }

        private void Start()
        {
            menu.PlayRequested += BeginDefault;
            ControllerService.Instance.Disconnected += LostController;
            ControllerService.Instance.Connected += ConnectedController;
        }

        private void OnDestroy()
        {
            if (menu != null) menu.PlayRequested -= BeginDefault;
            if (ControllerService.Current != null)
            {
                ControllerService.Current.Disconnected -= LostController;
                ControllerService.Current.Connected -= ConnectedController;
            }
            pause.Dispose(); skip.Dispose();
            Time.timeScale = 1f;
        }

        public void BeginDefault() => Begin(!HasCompletedTutorial);

        public void Begin(bool tutorial)
        {
            Time.timeScale = 1f;
            Paused = false;
            waitingForController = false;
            Tutorial = tutorial;
            menu.Suspended = true;
            ClearTargets();
            score.Reset();
            foreach (var gate in gates) gate.gameObject.SetActive(false);
            flight.enabled = false;
            flight.transform.position = spawn;
            flight.GetComponent<PlayerWeapon>().ClearProjectiles();
            flight.GetComponent<LockOnWeapon>()?.ResetBank();
            flight.GetComponent<EmpWeapon>()?.ResetCharge();
            authority.PrepareRun(tutorial);
            wasArmed = authority.Armed;
            RefusalUntil = 0f;
            SetStage(OpeningStage.Arrival);
        }

        private void Update()
        {
            var device = ControllerService.Instance.State;
            bool menuPress = device.StickButtonPressed && !previousMenuButton;
            previousMenuButton = device.StickButtonPressed;
            if (Stage == OpeningStage.Menu)
            {
                // FIRE confirms Play only while its button is focused; options retain ordinary navigation.
                if (device.Fire && !previousFire && UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(
                    menu.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement, "play-button")
                    == menu.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement.focusController.focusedElement)
                    BeginDefault();
                previousFire = device.Fire;
                return;
            }
            previousFire = device.Fire;
            if (pause.WasPressedThisFrame() || menuPress)
            {
                if (Paused) Resume(); else PauseRun("PAUSED");
            }
            if (Paused || Stage == OpeningStage.Finished) return;
            // Play can be requested during this frame's UI update. Do not count time from before that action.
            progress = Time.time - stageStarted;
            if (skip.WasPressedThisFrame() && Tutorial && Stage != OpeningStage.Course) SkipTraining();
            if (Stage == OpeningStage.Arrival)
            {
                if (progress >= ArrivalDuration)
                {
                    flight.enabled = true;
                    authority.RequireRelease();
                    if (Tutorial) SetStage(OpeningStage.Steering); else StartCourse();
                }
                return;
            }

            if (authority.RefusedThisFrame)
            {
                RefusalUntil = Time.unscaledTime + 2.5f;
                ControllerService.Instance.PlayTone(220, 90);
            }
            if (authority.Armed != wasArmed)
            {
                wasArmed = authority.Armed;
                ControllerService.Instance.PlayTone(wasArmed ? 880 : 330, 100);
            }
            switch (Stage)
            {
                case OpeningStage.Steering:
                    if (authority.RawInput.Steering.sqrMagnitude > 0.15f) lessonInput += Time.deltaTime;
                    if (lessonInput >= 0.8f) SetStage(OpeningStage.Throttle);
                    break;
                case OpeningStage.Throttle:
                    if (Mathf.Abs(authority.RawInput.Throttle) > 0.25f) lessonInput += Time.deltaTime;
                    if (lessonInput >= 0.6f) { authority.WeaponsAvailable = true; SetStage(OpeningStage.Commissioning); }
                    break;
                case OpeningStage.Commissioning:
                    lessonInput = authority.Armed ? lessonInput + Time.deltaTime : 0f;
                    if (lessonInput >= 0.7f)
                    {
                        practice = SpawnTarget(new Vector3(0, spawn.y, flight.transform.position.z + 45));
                        SetStage(OpeningStage.Guns);
                    }
                    break;
                case OpeningStage.Guns:
                    if (practice == null || practice.IsDestroyed)
                    {
                        PlayerPrefs.SetInt(CompletedKey, 1);
                        StartCourse();
                    }
                    else practice.transform.position = new Vector3(0, spawn.y, flight.transform.position.z + 45);
                    break;
                case OpeningStage.Course:
                    TickCourse();
                    break;
            }
        }

        public void SkipTraining()
        {
            if (Stage == OpeningStage.Menu || Stage == OpeningStage.Finished || Stage == OpeningStage.Course) return;
            Tutorial = false;
            PlayerPrefs.SetInt(CompletedKey, 1);
            authority.PrepareRun(false);
            flight.enabled = true;
            StartCourse();
        }

        private void StartCourse()
        {
            ClearTargets();
            score.Reset();
            courseStart = flight.transform.position.z;
            authority.WeaponsAvailable = true;
            authority.RequireRelease();
            for (int i = 0; i < gates.Length; i++)
            {
                gates[i].position = new Vector3((i % 3 - 1) * 5f, spawn.y + (i % 2 == 0 ? 0 : 2), courseStart + 35 + i * 40);
                gates[i].gameObject.SetActive(true);
            }
            for (int i = 0; i < 4; i++) SpawnTarget(new Vector3(i % 2 == 0 ? -8 : 8, spawn.y + 1, courseStart + 90 + i * 65));
            SetStage(OpeningStage.Course);
        }

        private ShootableTarget SpawnTarget(Vector3 position)
        {
            var go = Instantiate(targetTemplate, position, Quaternion.identity);
            go.name = "Opening practice target";
            go.SetActive(true);
            var target = go.GetComponent<ShootableTarget>();
            target.Destroyed += CountTarget;
            targets.Add(target);
            return target;
        }

        private void CountTarget(ShootableTarget target)
        {
            if (Stage == OpeningStage.Course) score.DestroyTarget(target.GetInstanceID());
        }

        private void TickCourse()
        {
            for (int i = 0; i < gates.Length; i++)
            {
                if (!gates[i].gameObject.activeSelf || flight.transform.position.z < gates[i].position.z) continue;
                Vector3 offset = flight.transform.position - gates[i].position;
                if (score.CrossGate(i, new Vector2(offset.x, offset.y), gateRadius))
                    ControllerService.Instance.PlayTone(1100, 50);
                gates[i].gameObject.SetActive(false);
            }
            if (flight.transform.position.z - courseStart >= courseLength)
            {
                authority.WeaponsAvailable = false;
                authority.RequireRelease();
                flight.enabled = false;
                SetStage(OpeningStage.Finished);
            }
        }

        private void ClearTargets()
        {
            foreach (var target in targets) if (target != null)
            {
                target.Destroyed -= CountTarget;
                target.gameObject.SetActive(false);
                Destroy(target.gameObject);
            }
            targets.Clear();
            practice = null;
        }

        public void PauseRun(string reason)
        {
            if (Stage == OpeningStage.Menu || Stage == OpeningStage.Finished || Paused) return;
            Paused = true;
            PauseNotice = reason;
            Time.timeScale = 0f;
            authority.RequireRelease();
            flight.GetComponent<LockOnWeapon>()?.CancelPainting();
        }

        public void Resume()
        {
            if (!Paused) return;
            if (waitingForController && !authority.KeyboardOnly && !ControllerService.Instance.State.Connected) return;
            // Reconnection is accepted only through this visible paused handover.
            authority.RequireRelease();
            Paused = false;
            waitingForController = false;
            Time.timeScale = 1f;
        }
        public void KeyboardTakeover() { if (Paused) { authority.SelectKeyboard(); Resume(); } }
        public void UseController()
        {
            if (!Paused || !ControllerService.Instance.State.Connected) return;
            authority.SelectController(); Resume();
        }
        private void LostController()
        {
            if (authority.KeyboardOnly) return;
            waitingForController = true;
            PauseRun("LINK LOST — CONTINUE WITH KEYBOARD OR RECONNECT");
        }
        private void ConnectedController() { if (!authority.KeyboardOnly) PauseRun("CONTROLLER CONNECTED — CHECK MASTER ARM BEFORE RESUMING"); }
        private void OnApplicationFocus(bool focused) { if (!focused) PauseRun("FOCUS LOST"); }

        public void ReturnToMenu()
        {
            Paused = false; Time.timeScale = 1f;
            flight.enabled = false;
            authority.WeaponsAvailable = false;
            authority.RequireRelease();
            ClearTargets();
            flight.GetComponent<PlayerWeapon>().ClearProjectiles();
            flight.GetComponent<LockOnWeapon>()?.ResetBank();
            flight.GetComponent<EmpWeapon>()?.ResetCharge();
            foreach (var gate in gates) gate.gameObject.SetActive(false);
            menu.Suspended = false;
            SetStage(OpeningStage.Menu);
        }

        private void SetStage(OpeningStage stage)
        {
            Stage = stage;
            stageStarted = Time.time;
            progress = lessonInput = 0f;
            StageChanged?.Invoke();
        }
    }
}
