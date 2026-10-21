using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// Arcade Mode's "one tube" interface. While on (Play Mode only), the given UI documents render into a
    /// screen-sized texture instead of straight to the screen; <see cref="ScreenEffectsFeature"/> lays that texture
    /// into the camera's picture with a phosphor glow just before the screen effects, so menus and the HUD bend,
    /// scan and fringe with the game. Pointer input follows the same CRT curvature, so a bent button is clicked where
    /// it appears. Off, every document goes back to its own panel settings, unchanged. Nothing here is saved.
    /// </summary>
    public sealed class ArcadeInterface
    {
        private readonly List<UIDocument> documents = new List<UIDocument>();
        private readonly List<PanelSettings> originals = new List<PanelSettings>();
        private readonly Dictionary<PanelSettings, PanelSettings> copies = new Dictionary<PanelSettings, PanelSettings>();
        private readonly List<(VisualElement element, IPanel from, int tries)> refocus = new List<(VisualElement, IPanel, int)>();
        private RenderTexture texture;
        private float curvature;

        public bool Routed => texture != null;

        /// <param name="look">Arcade Mode's look while it's on, otherwise null.</param>
        public void Apply(ArcadeProfile look, Camera camera, IEnumerable<UIDocument> uiDocuments)
        {
            RestoreFocus();
            bool on = look != null && look.interfaceOnScreen;
            on &= Application.isPlaying && camera != null;
            if (on && texture != null && (texture.width != Screen.width || texture.height != Screen.height)) Release();
            if (!on)
            {
                Release();
                return;
            }
            if (texture == null) Route(uiDocuments);
            curvature = look.CrtVector.x;
            ScreenEffects.SetInterface(camera, texture, look.interfaceBrightness,
                new Vector2(look.interfaceGlow, look.interfaceGlowRadius));
        }

        public void Release()
        {
            if (texture == null) return;
            for (int i = 0; i < documents.Count; i++)
                if (documents[i] != null) Move(documents[i], originals[i]);
            foreach (PanelSettings copy in copies.Values) Generated.Release(copy);
            documents.Clear();
            originals.Clear();
            copies.Clear();
            texture.Release();
            Generated.Release(texture);
            texture = null;
            ScreenEffects.ClearInterface();
        }

        /// <summary>Where a pointer at this screen position lands on the bent picture, in texture pixels
        /// (both top-down). Matches the curvature in the Screen Effects shader; outside the tube is no hit.</summary>
        public static Vector2 ScreenToTexture(Vector2 screen, Vector2 screenSize, Vector2 textureSize, float curvature)
        {
            var c = new Vector2(screen.x / screenSize.x - 0.5f, screen.y / screenSize.y - 0.5f);
            Vector2 uv = new Vector2(0.5f, 0.5f) + c * (1f + curvature * c.sqrMagnitude * 4f);
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return new Vector2(float.NaN, float.NaN);
            return Vector2.Scale(uv, textureSize);
        }

        private void Route(IEnumerable<UIDocument> uiDocuments)
        {
            texture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Arcade interface", hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            foreach (UIDocument doc in uiDocuments)
            {
                if (doc == null || doc.panelSettings == null || documents.Contains(doc)) continue;
                PanelSettings original = doc.panelSettings;
                if (!copies.TryGetValue(original, out PanelSettings copy))
                {
                    copy = Object.Instantiate(original);
                    copy.name = original.name + " (arcade)";
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    copy.targetTexture = texture;
                    copy.clearColor = copies.Count == 0; // the first panel clears; any others draw over it
                    copy.colorClearValue = Color.clear;
                    copy.SetScreenToPanelSpaceFunction(screen =>
                        ScreenToTexture(screen, new Vector2(Screen.width, Screen.height),
                            new Vector2(texture.width, texture.height), curvature));
                    copies.Add(original, copy);
                }
                documents.Add(doc);
                originals.Add(original);
                Move(doc, copy);
            }
        }

        /// <summary>Moves a document to other panel settings, keeping its keyboard focus. Focus belongs to a panel,
        /// and with an EventSystem a focus request on a newly selected panel only lands a frame later, so
        /// <see cref="RestoreFocus"/> keeps asking each frame until it holds.</summary>
        private void Move(UIDocument doc, PanelSettings settings)
        {
            if (doc.rootVisualElement?.focusController?.focusedElement is VisualElement element)
                refocus.Add((element, element.panel, 0));
            doc.panelSettings = settings;
        }

        private void RestoreFocus()
        {
            for (int i = refocus.Count - 1; i >= 0; i--)
            {
                (VisualElement element, IPanel from, int tries) = refocus[i];
                if (element.panel == null || element.panel == from) continue;
                if (element.focusController.focusedElement == element || tries >= 10)
                {
                    refocus.RemoveAt(i);
                    continue;
                }
                element.Focus();
                refocus[i] = (element, from, tries + 1);
            }
        }
    }
}
