using GeometricFlight.Core;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Non-colliding arrival telegraphs. Reads the wave clock; enemies exist only after the warning.</summary>
    [DisallowMultipleComponent]
    public sealed class ArenaPortalDisplay : MonoBehaviour
    {
        [SerializeField] private ArenaEncounter encounter;
        [SerializeField] private Material material;
        [SerializeField, Min(1f)] private float radius = 9f;
        private Transform[] portals;
        private LineRenderer[] rings;
        private MaterialPropertyBlock color;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            if (encounter == null || !encounter.isActiveAndEnabled || material == null)
            {
                enabled = false;
                return;
            }
            portals = new Transform[encounter.SpawnCount];
            rings = new LineRenderer[portals.Length * 2];
            color = new MaterialPropertyBlock();
            for (int i = 0; i < portals.Length; i++)
            {
                var portal = new GameObject("ArrivalPortal_" + (i + 1));
                portal.transform.SetParent(transform, false);
                portal.transform.position = encounter.SpawnPosition(i);
                portals[i] = portal.transform;
                for (int j = 0; j < 2; j++)
                {
                    var go = new GameObject(j == 0 ? "Outer" : "Inner");
                    go.transform.SetParent(portal.transform, false);
                    var line = go.AddComponent<LineRenderer>();
                    line.sharedMaterial = material;
                    line.useWorldSpace = false;
                    line.loop = true;
                    line.widthMultiplier = j == 0 ? 0.45f : 0.2f;
                    line.positionCount = 8;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    for (int p = 0; p < 8; p++)
                    {
                        float angle = p * Mathf.PI / 4f;
                        float r = radius * (j == 0 ? 1f : 0.72f);
                        line.SetPosition(p, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
                    }
                    rings[i * 2 + j] = line;
                }
                portal.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (portals == null || encounter.State == null) return;
            ArenaWaveState wave = encounter.State;
            bool incoming = wave.Phase == ArenaWavePhase.Incoming;
            bool closing = wave.Phase == ArenaWavePhase.Combat && wave.Elapsed < 0.6f;
            float progress = incoming ? wave.WarningProgress : 1f;
            float scale = incoming ? Mathf.Lerp(0.25f, 1f, progress) : Mathf.Clamp01(1f - wave.Elapsed / 0.6f);
            float pulse = 1.5f + progress * 2f + Mathf.Sin(wave.Elapsed * 9f) * 0.3f;
            color.SetVector(BaseColorId, AxiomPalette.ShaderColor(AxiomPalette.Orange, pulse));
            Camera view = Camera.main;
            for (int i = 0; i < portals.Length; i++)
            {
                portals[i].gameObject.SetActive(encounter.isActiveAndEnabled && (incoming || closing));
                portals[i].localScale = Vector3.one * scale;
                // A wave can be placed anywhere in the arena, so a portal follows its point and faces the viewer.
                portals[i].position = encounter.SpawnPosition(i);
                if (view != null) portals[i].rotation = view.transform.rotation;
                for (int j = 0; j < 2; j++)
                {
                    LineRenderer ring = rings[i * 2 + j];
                    ring.transform.localRotation = Quaternion.Euler(0f, 0f, wave.Elapsed * (j == 0 ? 20f : -35f));
                    ring.SetPropertyBlock(color);
                }
            }
        }

        private void OnDisable()
        {
            if (portals != null) foreach (Transform portal in portals) if (portal != null) portal.gameObject.SetActive(false);
        }
    }
}
