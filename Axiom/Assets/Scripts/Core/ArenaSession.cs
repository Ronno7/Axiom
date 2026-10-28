using System.Collections.Generic;
using GeometricFlight.Combat;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GeometricFlight.Core
{
    /// <summary>
    /// Runs the grey-box arena test: pause (Escape or MENU), destroyed targets respawn after a delay so the
    /// arena never empties, and losing the shield shatters the ship, which then restarts at the spawn point
    /// with a full shield. Also draws
    /// temporary markers at the screen edge for targets that are off screen, and a small speed readout.
    /// No score, waves or results: this scene exists to judge whether flying and shooting are fun on their own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaSession : MonoBehaviour
    {
        [SerializeField] private ArenaFlight flight;
        [SerializeField] private ShipShield shield;
        [SerializeField] private ShatterEffect shipDestruction;
        [Tooltip("Hidden when the ship is destroyed (its visuals, reticles, shadow).")]
        [SerializeField] private GameObject[] hideWhenDestroyed;
        [SerializeField, Min(0f)] private float respawnDelay = 6f;
        [SerializeField, Min(0f)] private float restartDelay = 1.5f;

        [Header("Off-screen markers")]
        [SerializeField] private Color markerColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField, Min(4f)] private float markerSize = 14f;

        private readonly List<(GameObject template, ShootableTarget live)> spawns =
            new List<(GameObject, ShootableTarget)>();
        private readonly List<(float time, int index)> pending = new List<(float, int)>();
        private Transform templates;
        private Camera view;
        private bool paused, destroyed;
        private float nextStallTone;
        private float restartAt;

        private void Start()
        {
            view = Camera.main;
            Time.timeScale = 1f;
            if (shield != null) shield.Depleted += OnShieldDepleted;

            // Inactive copies of every target, made before anything moves, so a fresh one can replace each
            // target that breaks.
            var holder = new GameObject("TargetTemplates");
            holder.SetActive(false);
            templates = holder.transform;
            foreach (ShootableTarget target in FindObjectsByType<ShootableTarget>(FindObjectsSortMode.None))
            {
                GameObject template = Instantiate(target.gameObject, target.transform.position,
                    target.transform.rotation, templates);
                template.name = target.name;
                Track(spawns.Count, template, target);
            }
        }

        private void OnDestroy()
        {
            if (shield != null) shield.Depleted -= OnShieldDepleted;
            Time.timeScale = 1f;
        }

        private void Track(int index, GameObject template, ShootableTarget live)
        {
            if (index == spawns.Count) spawns.Add((template, live));
            else spawns[index] = (template, live);
            live.Destroyed += _ => pending.Add((Time.time + respawnDelay, index));
        }

        private void Update()
        {
            // Stall warning on the cockpit buzzer: a low beep that repeats while the ship is stalled.
            if (flight != null && flight.State.Stalled && !paused && Time.unscaledTime >= nextStallTone)
            {
                ControllerService.Current?.PlayTone(196, 180);
                nextStallTone = Time.unscaledTime + 0.4f;
            }

            Keyboard keys = Keyboard.current;
            if (keys != null && keys.mKey.wasPressedThisFrame && flight != null)
                flight.SetFullRotation(!flight.FullRotation);

            if (flight != null && flight.CurrentInput.PausePressed && !destroyed)
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
            }

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].time) continue;
                int index = pending[i].index;
                pending.RemoveAt(i);
                (GameObject template, ShootableTarget old) = spawns[index];
                if (old != null) Destroy(old.gameObject);
                GameObject fresh = Instantiate(template, template.transform.position, template.transform.rotation);
                fresh.name = template.name;
                Track(index, template, fresh.GetComponent<ShootableTarget>());
            }

            if (destroyed && Time.unscaledTime >= restartAt) Restart();
        }

        private void Restart()
        {
            destroyed = false;
            flight.enabled = true;
            flight.Respawn();
            flight.InputLocked = false;
            shield.Restore();
            foreach (GameObject go in hideWhenDestroyed) if (go != null) go.SetActive(true);
        }

        private void OnShieldDepleted()
        {
            destroyed = true;
            restartAt = Time.unscaledTime + restartDelay;
            flight.InputLocked = true;
            flight.enabled = false;
            foreach (GameObject go in hideWhenDestroyed) if (go != null) go.SetActive(false);
            if (shipDestruction != null) shipDestruction.Play();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || view == null) return;
            var size = new Vector2(Screen.width, Screen.height);
            Vector2 middle = size * 0.5f;
            GUI.color = markerColor;
            foreach (ShootableTarget target in ShootableTarget.Active)
            {
                Vector3 sp = view.WorldToScreenPoint(target.transform.position);
                bool behind = sp.z < 0f;
                if (!behind && sp.x >= 0f && sp.x <= size.x && sp.y >= 0f && sp.y <= size.y) continue;
                // GUI y runs down; a target behind the camera projects mirrored, so flip it.
                var dir = new Vector2(sp.x - middle.x, (size.y - sp.y) - middle.y);
                if (behind) dir = -dir;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
                Vector2 edge = middle + dir * Mathf.Min(
                    (middle.x - markerSize) / Mathf.Max(0.001f, Mathf.Abs(dir.x)),
                    (middle.y - markerSize) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, edge);
                GUI.DrawTexture(new Rect(edge.x - markerSize, edge.y - 2f, markerSize, 4f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(edge.x - 4f, edge.y - 4f, 8f, 8f), Texture2D.whiteTexture);
                GUI.matrix = saved;
            }

            GUI.color = Color.white;
            if (flight != null)
            {
                string throttle = Mathf.Abs(flight.Throttle) < 0.001f ? "CRUISE"
                    : (flight.Throttle > 0f ? "+" : "") + Mathf.RoundToInt(flight.Throttle * 100f) + "%";
                GUI.Label(new Rect(16f, size.y - 52f, 520f, 24f),
                    (flight.FullRotation ? "FULL ROTATION" : "ARCADE") + " flight  (M to switch)");
                GUI.Label(new Rect(16f, size.y - 32f, 520f, 24f), Mathf.RoundToInt(flight.CurrentSpeed) + " m/s   "
                    + throttle + (flight.State.Stalled ? "   STALL" : "") + (flight.State.Returning ? "   ARENA EDGE" : ""));
            }
            if (paused) GUI.Label(new Rect(middle.x - 40f, middle.y - 12f, 120f, 24f), "PAUSED");
        }
    }
}
