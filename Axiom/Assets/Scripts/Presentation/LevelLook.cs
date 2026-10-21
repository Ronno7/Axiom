using GeometricFlight.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace GeometricFlight.Presentation
{
    /// <summary>A level's look in one Inspector: the same colour grade and atmosphere as the main menu (haze, horizon
    /// band, stars, dust), plus Arcade Mode (the shared <see cref="ArcadeProfile"/>) when the player turns it on in
    /// Options. The menu-only extras (glitch, interface details) stay in <see cref="MenuLook"/>. Presentation only;
    /// settings apply live in the editor.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Volume))]
    [DefaultExecutionOrder(100)] // after the follow camera has moved, so the sky never lags a frame
    public sealed class LevelLook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, FormerlySerializedAs("targetCamera")] private Camera levelCamera;
        [SerializeField] private Shader glowShader;
        [Tooltip("The game's single accent colour, used by Arcade Mode's fringing.")]
        [SerializeField] private Color accent = new Color(0.84f, 0.12f, 0.16f);

        [Header("Main look")]
        [SerializeField] private GradeSettings grade = new GradeSettings();
        [SerializeField] private AtmosphereSettings atmosphere = new AtmosphereSettings { dustDrift = 0f };

        [Header("Arcade mode (player toggle in Options)")]
        [Tooltip("Shared with the main menu, so the look is tuned in one place.")]
        [SerializeField, FormerlySerializedAs("look")] private ArcadeProfile arcade;
        [Tooltip("Show Arcade Mode in the editor whatever the player's Options setting is.")]
        [SerializeField, FormerlySerializedAs("preview")] private bool previewArcade;

        private LookVolume look;
        private readonly LookAtmosphere air = new LookAtmosphere();
        private bool atmosphereDirty = true;

        private bool ArcadeOn => arcade != null && (previewArcade || GameSettings.ArcadeMode);

        private void OnEnable()
        {
            look = new LookVolume(GetComponent<Volume>(), "Level look (generated)");
            atmosphereDirty = true;
            Apply();
        }

        private void OnDisable()
        {
            if (levelCamera != null && ScreenEffects.Camera == levelCamera) ScreenEffects.Clear();
            look?.Dispose();
            look = null;
            air.Dispose();
        }

        private void OnValidate()
        {
            atmosphereDirty = true;
            if (look != null) Apply();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void LateUpdate()
        {
            if (atmosphereDirty)
            {
                atmosphereDirty = false;
                air.Build(atmosphere, levelCamera, glowShader, "Level");
            }
            air.Follow(levelCamera, Application.isPlaying ? Time.deltaTime : 0f);
            Apply();
        }

        private void Apply()
        {
            ArcadeProfile a = ArcadeOn ? arcade : null;
            look.Apply(grade, a, levelCamera);
            LookAtmosphere.ApplyHaze(atmosphere, levelCamera);
            if (levelCamera == null) return;
            if (a != null)
                ScreenEffects.Set(levelCamera, Vector4.zero, new Vector4(0f, 0f, a.FringeOffset, a.FringeStrength),
                    a.CrtVector, a.TubeEdge, a.ExtrasVector, accent);
            else if (ScreenEffects.Camera == levelCamera)
                ScreenEffects.Clear();
        }
    }
}
