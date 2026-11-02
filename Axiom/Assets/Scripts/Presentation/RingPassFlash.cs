using System.Collections.Generic;
using GeometricFlight.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// Flying through a scoring ring lights it: the ring flares white and settles back, and a ghost of its outline
    /// shoots ahead down the rail, growing and fading, so the confirmation is seen in front of the ship rather than
    /// behind the camera. Any rail with scoring rings (tutorial or procedural) can use it. It also lights the ring a
    /// course says is next (<see cref="MarkNext"/>), brighter than the rest with a slow breath. Visual only; reads the
    /// rail's scoring event and never changes the score.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RingPassFlash : MonoBehaviour
    {
        [SerializeField] private RailRings rail;
        [Tooltip("GeometricFlight/Additive Glow, for the expanding outline.")]
        [SerializeField] private Shader glowShader;
        [SerializeField, Min(0.1f)] private float seconds = 0.6f;
        [Tooltip("How far the ghost outline grows, relative to the ring.")]
        [SerializeField, Min(1f)] private float ghostGrowth = 1.3f;
        [Tooltip("How far ahead the ghost outline travels, metres.")]
        [SerializeField, Min(0f)] private float ghostTravel = 60f;
        [SerializeField, Min(0f)] private float flareIntensity = 4f;
        [Tooltip("How bright the next ring burns.")]
        [SerializeField, Min(0f)] private float nextIntensity = 2.5f;

        private sealed class Flash
        {
            public Renderer Ring;
            public LineRenderer Ghost;
            public float Start;
            public Vector3 Center;
            public Quaternion Rotation;
            public float Radius;
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly List<Flash> active = new List<Flash>();
        private readonly Stack<LineRenderer> spare = new Stack<LineRenderer>();
        private readonly Vector3[] outline = new Vector3[6];
        private MaterialPropertyBlock block;
        private Material ghostMaterial;
        private Renderer next;

        /// <summary>Lights this ring as the next one; null lights none.</summary>
        public void MarkNext(Transform ring)
        {
            Renderer renderer = ring != null ? ring.GetComponent<Renderer>() : null;
            if (renderer == next) return;
            if (next != null && !IsFlashing(next)) next.SetPropertyBlock(null);
            next = renderer;
        }

        private bool IsFlashing(Renderer ring)
        {
            foreach (Flash flash in active) if (flash.Ring == ring) return true;
            return false;
        }

        private void OnEnable()
        {
            if (rail == null || glowShader == null)
            {
                Debug.LogError("Assign the rail and the Additive Glow shader to RingPassFlash.", this);
                enabled = false;
                return;
            }
            block ??= new MaterialPropertyBlock();
            ghostMaterial = LookAtmosphere.GlowMaterial(glowShader, AxiomPalette.Light, 3f, 0f, 2.5f);
            rail.Scored += OnScored;
            RingGate.Passed += OnGate;
        }

        private void OnDisable()
        {
            if (rail != null) rail.Scored -= OnScored;
            RingGate.Passed -= OnGate;
            foreach (Flash flash in active) End(flash);
            active.Clear();
            if (next != null) next.SetPropertyBlock(null);
            next = null;
            while (spare.Count > 0) { LineRenderer line = spare.Pop(); if (line != null) Destroy(line.gameObject); }
            if (ghostMaterial != null) Destroy(ghostMaterial);
        }

        private void OnScored(int index) => Show(rail.Ring(index));

        private void OnGate(RingGate gate) => Show(gate.transform);

        private void Show(Transform ring)
        {
            var renderer = ring != null ? ring.GetComponent<Renderer>() : null;
            if (renderer == null) return;
            MeshFilter filter = ring.GetComponent<MeshFilter>();
            float radius = filter != null && filter.sharedMesh != null
                ? filter.sharedMesh.bounds.extents.x * ring.lossyScale.x : 10f * ring.lossyScale.x;
            active.Add(new Flash { Ring = renderer, Ghost = TakeLine(), Start = Time.time, Center = ring.position,
                Rotation = ring.rotation, Radius = radius });
        }

        private void LateUpdate()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Flash flash = active[i];
                float t = (Time.time - flash.Start) / seconds;
                if (t >= 1f || flash.Ring == null)
                {
                    End(flash);
                    active.RemoveAt(i);
                    continue;
                }
                // The ring flares at once, then eases back to its own colour.
                float flare = (1f - t) * (1f - t);
                Material material = flash.Ring.sharedMaterial;
                block.Clear();
                block.SetColor(BaseColor, Color.Lerp(material.GetColor(BaseColor),
                    AxiomPalette.MaterialColor(material, "_BaseColor", AxiomPalette.Light, flareIntensity), flare));
                flash.Ring.SetPropertyBlock(block);

                float ease = 1f - (1f - t) * (1f - t);
                float grow = Mathf.Lerp(1f, ghostGrowth, ease);
                Vector3 center = flash.Center + flash.Rotation * Vector3.forward * (ghostTravel * ease);
                for (int k = 0; k < outline.Length; k++)
                {
                    float angle = k * Mathf.PI / 3f;
                    outline[k] = center + flash.Rotation
                        * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (flash.Radius * grow);
                }
                flash.Ghost.SetPositions(outline);
                float width = Mathf.Lerp(0.5f, 0.15f, t);
                flash.Ghost.startWidth = flash.Ghost.endWidth = width;
                flash.Ghost.startColor = flash.Ghost.endColor = AxiomPalette.WithAlpha(AxiomPalette.White, 1f - t);
            }
            if (next == null || IsFlashing(next)) return;
            Material lit = next.sharedMaterial;
            float breath = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            block.Clear();
            block.SetColor(BaseColor, Color.Lerp(lit.GetColor(BaseColor),
                AxiomPalette.MaterialColor(lit, "_BaseColor", AxiomPalette.Light, nextIntensity), breath));
            next.SetPropertyBlock(block);
        }

        private void End(Flash flash)
        {
            if (flash.Ring != null) flash.Ring.SetPropertyBlock(null);
            if (flash.Ghost == null) return;
            flash.Ghost.enabled = false;
            spare.Push(flash.Ghost);
        }

        private LineRenderer TakeLine()
        {
            LineRenderer line = spare.Count > 0 ? spare.Pop() : null;
            if (line == null)
            {
                line = new GameObject("Ring pass flash").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = ghostMaterial;
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = outline.Length;
                line.textureMode = LineTextureMode.RepeatPerSegment;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
            }
            line.enabled = true;
            return line;
        }
    }
}
