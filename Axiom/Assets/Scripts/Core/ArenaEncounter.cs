using System;
using System.Collections.Generic;
using GeometricFlight.Combat;
using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>Owns one authored arena wave. Original scene targets are hidden only at runtime and serve
    /// as templates, preserving their tuning and scene references (inactive templates work too). Disable before
    /// Play for free practice. With Start On Play off, the wave waits for <see cref="Restart"/> (the tutorial).</summary>
    [DefaultExecutionOrder(-20), DisallowMultipleComponent]
    public sealed class ArenaEncounter : MonoBehaviour
    {
        [Serializable]
        private sealed class Spawn
        {
            public ShootableTarget template;
            public Transform point;
        }

        [Tooltip("Original practice targets, hidden before their Awake runs. Never modified in Edit Mode.")]
        [SerializeField] private ShootableTarget[] practiceTargets;
        [SerializeField] private Spawn[] spawns;
        [SerializeField, Min(0.1f)] private float warningTime = 3f;
        [SerializeField, Min(0.1f)] private float recoveryTime = 6f;
        [Tooltip("Off: stays inactive until Restart is called (the tutorial starts its waves).")]
        [SerializeField] private bool startOnPlay = true;
        private readonly List<ShootableTarget> live = new List<ShootableTarget>();
        private Transform waveRoot;
        private int generation;

        public ArenaWaveState State { get; private set; }
        public int SpawnCount => spawns == null ? 0 : spawns.Length;
        public Vector3 SpawnPosition(int index) => spawns[index].point.position;
        public Transform SpawnPoint(int index) => spawns[index].point;
        /// <summary>This wave's spawned enemies, including ones already destroyed.</summary>
        public IReadOnlyList<ShootableTarget> Live => live;

        private void Awake()
        {
            if (!enabled) return;
            if (SpawnCount == 0)
            {
                Debug.LogError("Assign spawn points to ArenaEncounter.", this);
                enabled = false;
                return;
            }
            foreach (Spawn spawn in spawns)
                if (spawn == null || spawn.template == null || spawn.point == null)
                {
                    Debug.LogError("Every encounter spawn needs a template and point.", this);
                    enabled = false;
                    return;
                }
            if (practiceTargets != null)
                foreach (ShootableTarget target in practiceTargets) if (target != null) target.gameObject.SetActive(false);
            State = new ArenaWaveState(SpawnCount, warningTime, recoveryTime);
            waveRoot = new GameObject("EncounterEnemies").transform;
            waveRoot.SetParent(transform, false);
        }

        private void Start()
        {
            if (startOnPlay) State?.Restart();
        }

        private void Update()
        {
            ArenaWavePhase before = State.Phase;
            State.Step(Time.deltaTime);
            if (State.Phase == before) return;
            if (State.Phase == ArenaWavePhase.Incoming) ClearWave();
            else if (State.Phase == ArenaWavePhase.Combat) SpawnWave();
        }

        private void SpawnWave()
        {
            for (int i = 0; i < spawns.Length; i++)
            {
                Spawn spawn = spawns[i];
                var patrol = spawn.template.GetComponent<LissajousPatrol>();
                Vector3 anchor = spawn.point.position - (patrol != null ? patrol.InitialOffset : Vector3.zero);
                // Source is inactive, so Awake sees the correct anchor and scene references on activation.
                GameObject clone = Instantiate(spawn.template.gameObject, anchor, spawn.point.rotation, waveRoot);
                clone.name = "Wave_" + State.Round + "_" + spawn.template.name;
                ShootableTarget target = clone.GetComponent<ShootableTarget>();
                int slot = i;
                int waveGeneration = generation;
                target.Destroyed += _ => OnDestroyed(slot, waveGeneration);
                live.Add(target);
                clone.SetActive(true);
                clone.transform.position = spawn.point.position;
            }
        }

        private void OnDestroyed(int slot, int waveGeneration)
        {
            if (generation != waveGeneration || !State.ReportDestroyed(slot) || State.Phase != ArenaWavePhase.Cleared) return;
            ClearEnemyShots();
        }

        public void Defeat()
        {
            State?.Defeat();
            ClearWave();
        }

        public void Restart()
        {
            ClearWave();
            State?.Restart();
        }

        /// <summary>Ends the encounter: no next round is scheduled and any remaining enemies are removed.</summary>
        public void Stop()
        {
            State?.Stop();
            ClearWave();
        }

        /// <summary>Moves the arrival points together so their middle sits at <paramref name="centre"/>, keeping their
        /// shape. Takes effect from the next arrival.</summary>
        public void PlaceAt(Vector3 centre)
        {
            Vector3 middle = Vector3.zero;
            foreach (Spawn spawn in spawns) middle += spawn.point.position;
            Vector3 shift = centre - middle / spawns.Length;
            foreach (Spawn spawn in spawns) spawn.point.position += shift;
        }

        private void ClearEnemyShots()
        {
            foreach (ShootableTarget target in live)
                if (target != null) target.GetComponent<EnemyTurret>()?.ClearProjectiles();
        }

        private void ClearWave()
        {
            generation++;
            ClearEnemyShots();
            foreach (ShootableTarget target in live)
            {
                if (target == null) continue;
                target.gameObject.SetActive(false);
                Destroy(target.gameObject);
            }
            live.Clear();
        }

        private void OnDisable()
        {
            State?.Stop();
            ClearWave();
        }
    }
}
