using System.Collections.Generic;
using GeometricFlight.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// Flying through a scoring ring lights it: the ring flares white and settles back, and a ghost of its outline
    /// shoots ahead down the rail, growing and fading, so the confirmation is seen in front of the ship rather than
    /// behind the camera. Any rail with scoring rings (tutorial or procedural) can use it. Visual only; reads the
    /// rail's scoring event and never changes the score.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RingPassFlash : MonoBehaviour
    {
        [SerializeField] private RailHandlingStudy rail;
        [Tooltip("GeometricFlight/Additive Glow, for the expanding outline.")]
        [SerializeField] private Shader glowShader;
        [SerializeField, Min(0.1f)] private float seconds = 0.6f;
        [Tooltip("How far the ghost outline grows, relative to the ring.")]
        [SerializeField, Min(1f)] private float ghostGrowth = 1.3f;
        [Tooltip("How far ahead the ghost outline travels, metres.")]
        [SerializeField, Min(0f)] private float ghostTravel = 60f;
        [SerializeField, Min(0f)] private float flareIntensity = 4f;

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
            rail.HoopScored += OnScored;
        }

        private void OnDisable()
        {
            if (rail != null) rail.HoopScored -= OnScored;
            foreach (Flash flash in active) End(flash);
            active.Clear();
            while (spare.Count > 0) { LineRenderer line = spare.Pop(); if (line != null) Destroy(line.gameObject); }
            if (ghostMaterial != null) Destroy(ghostMaterial);
        }

        private void OnScored(int index)
        {
            Transform ring = rail.Hoop(index);
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
