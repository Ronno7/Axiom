using System;
using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>Breaks into small tetrahedral shards that burst outward, spin, slow, and shrink away.
    /// Shards are created once, on first play, and reused; the effect ends after a fixed duration.</summary>
    [DisallowMultipleComponent]
    public sealed class ShatterEffect : MonoBehaviour
    {
        [Tooltip("Shards alternate between these materials. Saturated colors read as destruction.")]
        [SerializeField] private Material[] shardMaterials;
        [SerializeField, Range(1, 48)] private int shardCount = 20;
        [SerializeField, Min(0.01f)] private float shardSize = 0.5f;
        [SerializeField] private Vector2 burstSpeed = new Vector2(5f, 13f);
        [Tooltip("Fraction of velocity lost per second.")]
        [SerializeField, Range(0f, 0.99f)] private float drag = 0.85f;
        [SerializeField, Min(0.05f)] private float duration = 0.9f;

        private static Mesh shardMesh;
        private Transform[] shards;
        private Vector3[] velocities;
        private Vector3[] spins;
        private float elapsed;
        private bool playing;
        private Action finished;

        public void Play(Action onFinished = null)
        {
            if (shardMaterials == null || shardMaterials.Length == 0)
            {
                Debug.LogWarning("ShatterEffect has no shard materials; skipping effect.", this);
                onFinished?.Invoke();
                return;
            }

            if (shards == null) CreateShards();
            for (int i = 0; i < shards.Length; i++)
            {
                Vector3 direction = UnityEngine.Random.onUnitSphere;
                shards[i].localPosition = direction * (shardSize * 0.5f);
                shards[i].localRotation = UnityEngine.Random.rotationUniform;
                shards[i].localScale = Vector3.one * shardSize * UnityEngine.Random.Range(0.6f, 1.4f);
                shards[i].gameObject.SetActive(true);
                velocities[i] = direction * UnityEngine.Random.Range(burstSpeed.x, burstSpeed.y);
                spins[i] = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(180f, 720f);
            }
            elapsed = 0f;
            finished = onFinished;
            playing = true;
        }

        private void Update()
        {
            if (!playing) return;
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            float damping = Mathf.Pow(1f - drag, deltaTime);
            float shrink = 1f - Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < shards.Length; i++)
            {
                velocities[i] *= damping;
                shards[i].position += velocities[i] * deltaTime;
                shards[i].Rotate(spins[i] * deltaTime, Space.Self);
                shards[i].localScale = Vector3.one * (shardSize * shrink * shrink);
            }

            if (elapsed < duration) return;
            playing = false;
            foreach (Transform shard in shards) shard.gameObject.SetActive(false);
            Action callback = finished;
            finished = null;
            callback?.Invoke();
        }

        private void CreateShards()
        {
            if (shardMesh == null) shardMesh = BuildTetrahedron();
            var root = new GameObject("Shards").transform;
            root.SetParent(transform, false);
            shards = new Transform[shardCount];
            velocities = new Vector3[shardCount];
            spins = new Vector3[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                var shard = new GameObject("Shard", typeof(MeshFilter), typeof(MeshRenderer));
                shard.transform.SetParent(root, false);
                shard.GetComponent<MeshFilter>().sharedMesh = shardMesh;
                shard.GetComponent<MeshRenderer>().sharedMaterial = shardMaterials[i % shardMaterials.Length];
                shard.SetActive(false);
                shards[i] = shard.transform;
            }
        }

        private static Mesh BuildTetrahedron()
        {
            Vector3 a = new Vector3(1f, 1f, 1f), b = new Vector3(-1f, -1f, 1f);
            Vector3 c = new Vector3(-1f, 1f, -1f), d = new Vector3(1f, -1f, -1f);
            // Duplicated vertices per face give flat facet shading.
            var mesh = new Mesh { name = "Shatter Shard", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { a, c, b, a, b, d, a, d, c, b, c, d };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
