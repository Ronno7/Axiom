using System;
using System.Collections.Generic;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GeometricFlight.Editor
{
    /// <summary>Explicit palette migration and audit. Never runs on import, loads a scene or saves an asset.
    /// Callers own scene/prefab loading and saving, including any existing unsaved edits.</summary>
    public static class AxiomPaletteTools
    {
        private const string MaterialFolder = "Assets/Art/Materials/";
        private const float Tolerance = 0.0001f;

        private readonly struct Tint
        {
            public readonly string Property;
            public readonly Color Color;
            public readonly float Intensity;
            public Tint(string property, Color color, float intensity = 1f)
            {
                Property = property;
                Color = color;
                Intensity = intensity;
            }
        }

        // Names remain stable: scenes and prefabs keep their existing material GUIDs. Intensities are
        // independent of opacity. Vector properties stored in m_Colors are deliberately NOT palette data.
        private static readonly Dictionary<string, Tint[]> Materials = new Dictionary<string, Tint[]>
        {
            { "Black", Base(AxiomPalette.Void) },
            { "DarkGray", Base(AxiomPalette.Edge) },
            { "LightGray", Base(AxiomPalette.Text) },
            { "White", Base(AxiomPalette.Light) },
            { "Title", Base(AxiomPalette.Light) },
            { "GridGlass", Base(AxiomPalette.Structure) },
            { "IndicatorBlue", Base(AxiomPalette.Cyan) },
            { "WeaponProjectile", Base(AxiomPalette.Cyan, 2f) },
            { "EnemyProjectile", Base(AxiomPalette.Ember, 2f) },
            { "DestructionPrimary", Base(AxiomPalette.Orange) },
            { "DestructionSecondary", Base(AxiomPalette.Hot) },
            { "LightLineCyan", Base(AxiomPalette.Cyan, 2f) },
            { "LightColumnWhite", Base(AxiomPalette.Light, 2.2f) },
            { "LightLineOrange", Base(AxiomPalette.Orange, 0.6f) },
            { "LightLineOrangeBright", Base(AxiomPalette.Orange, 1.3f) },
            { "ProgramOrange", Base(AxiomPalette.Orange, 1.3f) },
            { "ProgramOrangeDim", Base(AxiomPalette.Orange, 0.8f) },
            { "LaserCyan", Base(AxiomPalette.Cyan, 3.2f) },
            { "MissileTrail", new[] { new Tint("_BaseColor", AxiomPalette.Light, 2.4f), new Tint("_EmissionColor", AxiomPalette.Black) } },
            { "GridFloor", new[] { new Tint("_BaseColor", AxiomPalette.Structure), new Tint("_LineColor", AxiomPalette.Cyan, 1.8f) } },
            { "OpeningHull", new[] { new Tint("_BaseColor", AxiomPalette.Edge), new Tint("_EdgeColor", AxiomPalette.Cyan, 2.5f) } },
            // Enclosed passage: dark walls, neutral ribs, cyan edge lines and runway-matching markings.
            { "PassageSurface", new[] { new Tint("_BaseColor", AxiomPalette.Structure), new Tint("_EdgeColor", AxiomPalette.Cyan, 2.5f) } },
            { "PassageRib", new[] { new Tint("_BaseColor", AxiomPalette.Text, 1.5f), new Tint("_EdgeColor", AxiomPalette.Cyan, 3f) } },
            { "PassageEdge", new[] { new Tint("_BaseColor", AxiomPalette.Cyan, 1.6f), new Tint("_EdgeColor", AxiomPalette.Light, 3f) } },
            // One infinite ground for menu, passage and arena: a dark desert grid below the bloom threshold.
            { "GroundGrid", new[] { new Tint("_BaseColor", AxiomPalette.Structure), new Tint("_LineColor", AxiomPalette.Cyan, 0.9f) } },
            { "PassageMarking", new[] { new Tint("_BaseColor", AxiomPalette.Text), new Tint("_EdgeColor", AxiomPalette.Cyan, 2.5f) } },
            { "DarkRed", new[] { new Tint("_BaseColor", AxiomPalette.Ember, 0.4f), new Tint("_EmissionColor", AxiomPalette.Ember, 1.5f), new Tint("_SpecColor", AxiomPalette.White, 0.2f) } },
            { "GeometricExhaust", Array.Empty<Tint>() }
        };

        /// <returns>Number of changed properties. Assets are marked dirty but are not saved.</returns>
        public static int ApplyMaterialsAndSettings()
        {
            RequireEditMode();
            var pass = new Pass(true);
            VisitAssets(pass, false);
            if (pass.Issues.Count > 0) Debug.LogWarning(string.Join("\n", pass.Issues));
            return pass.Changes;
        }

        /// <summary>The supplied scene must be active: Unity's RenderSettings belong to the active scene.</summary>
        public static int ApplyToScene(Scene scene)
        {
            RequireEditMode();
            RequireActiveScene(scene);
            var pass = new Pass(true);
            foreach (GameObject root in scene.GetRootGameObjects()) VisitRoot(root, pass);
            VisitRenderSettings(pass);
            if (pass.Changes > 0) EditorSceneManager.MarkSceneDirty(scene);
            if (pass.Issues.Count > 0) Debug.LogWarning(string.Join("\n", pass.Issues));
            return pass.Changes;
        }

        /// <summary>Use with PrefabUtility.LoadPrefabContents, then save and unload in the caller.</summary>
        public static int ApplyToPrefab(GameObject root)
        {
            RequireEditMode();
            if (root == null) throw new ArgumentNullException(nameof(root));
            var pass = new Pass(true);
            VisitRoot(root, pass);
            if (pass.Issues.Count > 0) Debug.LogWarning(string.Join("\n", pass.Issues));
            return pass.Changes;
        }

        /// <summary>Checks every authored material, ArcadeProfile and prefab. Unloaded scenes are checked
        /// separately with ValidateScene; this never opens scenes or changes assets.</summary>
        public static string[] ValidateAssets()
        {
            var pass = new Pass(false);
            VisitAssets(pass, true);
            return pass.Issues.ToArray();
        }

        public static string[] ValidateScene(Scene scene)
        {
            RequireActiveScene(scene);
            var pass = new Pass(false);
            foreach (GameObject root in scene.GetRootGameObjects()) VisitRoot(root, pass);
            VisitRenderSettings(pass);
            return pass.Issues.ToArray();
        }

        public static string[] ValidatePrefab(GameObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var pass = new Pass(false);
            VisitRoot(root, pass);
            return pass.Issues.ToArray();
        }

        [MenuItem("Tools/Axiom/Palette/Audit Assets and Active Scene")]
        private static void AuditMenu()
        {
            var issues = new List<string>(ValidateAssets());
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded) issues.AddRange(ValidateScene(scene));
            if (issues.Count == 0) Debug.Log($"Palette audit passed: {Materials.Count} material roles, profiles, prefabs and active scene.");
            else Debug.LogWarning("Palette audit:\n" + string.Join("\n", issues));
        }

        private static Tint[] Base(Color color, float intensity = 1f) => new[] { new Tint("_BaseColor", color, intensity) };

        private static void VisitAssets(Pass pass, bool includePrefabs)
        {
            var expectedPaths = new HashSet<string>();
            foreach (var entry in Materials)
            {
                string path = MaterialFolder + entry.Key + ".mat";
                expectedPaths.Add(path);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { pass.Issues.Add("Missing palette material: " + path); continue; }
                var assigned = new HashSet<string>();
                foreach (Tint tint in entry.Value)
                {
                    assigned.Add(tint.Property);
                    VisitMaterialTint(material, tint, pass, false);
                    // URP's legacy _Color is only a compatibility alias. Other saved vectors such as
                    // _CameraFadeParams, _Fade and _KeyDirection must retain their exact data.
                    if (tint.Property == "_BaseColor")
                    {
                        assigned.Add("_Color");
                        VisitMaterialTint(material, new Tint("_Color", tint.Color, tint.Intensity), pass, true);
                    }
                }
                Shader shader = material.shader;
                if (shader == null) { pass.Issues.Add(path + ": missing shader"); continue; }
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    string name = shader.GetPropertyName(i);
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Color && !assigned.Contains(name))
                        pass.Issues.Add(path + ": unassigned shader color " + name);
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // UI Toolkit embeds a neutral rendering material in its theme stylesheet. It is
                // engine data, not authored artwork; keep imported model materials visible to audit.
                if (path.EndsWith(".tss", StringComparison.OrdinalIgnoreCase)) continue;
                if (!expectedPaths.Contains(path)) pass.Issues.Add("Unmapped authored material needs a palette role: " + path);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:ArcadeProfile", new[] { "Assets" }))
            {
                var profile = AssetDatabase.LoadAssetAtPath<ArcadeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                var serialized = new SerializedObject(profile);
                pass.Color(serialized, "colourFilter", AxiomPalette.White);
                pass.Float(serialized, "hueShift", 0f);
                pass.Float(serialized, "temperature", 0f);
                pass.Float(serialized, "tint", 0f);
            }
            if (!includePrefabs) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null) VisitRoot(prefab, pass);
            }
        }

        private static void VisitMaterialTint(Material material, Tint tint, Pass pass, bool optionalAlias)
        {
            var serialized = new SerializedObject(material);
            SerializedProperty saved = FindSavedColor(serialized, tint.Property);
            bool shaderProperty = material.HasProperty(tint.Property);
            if (!shaderProperty && saved == null)
            {
                if (!optionalAlias) pass.Issues.Add(material.name + ": missing color property " + tint.Property);
                return;
            }
            Color actual = shaderProperty ? (Color)material.GetVector(tint.Property) : saved.colorValue;
            // An undeclared legacy _Color alias follows its declared _BaseColor's storage encoding.
            string encodingProperty = optionalAlias && !shaderProperty ? "_BaseColor" : tint.Property;
            Color expected = AxiomPalette.MaterialColor(material, encodingProperty, tint.Color, tint.Intensity, actual.a);
            if (Near(actual, expected)) return;
            if (!pass.Apply) { pass.Issues.Add(material.name + "/" + tint.Property + ": outside assigned palette role"); return; }
            Undo.RecordObject(material, "Apply Axiom palette");
            // Unity's material upload still follows the shader property's encoding when using SetVector.
            // MaterialColor supplies the correct stored representation; raw MPB/global colors differ.
            if (shaderProperty) material.SetVector(tint.Property, expected);
            else { saved.colorValue = expected; serialized.ApplyModifiedProperties(); }
            EditorUtility.SetDirty(material);
            pass.Changes++;
        }

        private static SerializedProperty FindSavedColor(SerializedObject serialized, string name)
        {
            SerializedProperty colors = serialized.FindProperty("m_SavedProperties.m_Colors");
            if (colors == null) return null;
            for (int i = 0; i < colors.arraySize; i++)
            {
                SerializedProperty item = colors.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("first").stringValue == name) return item.FindPropertyRelative("second");
            }
            return null;
        }

        private static void VisitRoot(GameObject root, Pass pass)
        {
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || IsGenerated(component)) continue;
                var serialized = new SerializedObject(component);
                if (component is MenuLook)
                {
                    pass.Color(serialized, "accent", AxiomPalette.Orange);
                    pass.Color(serialized, "atmosphere.haze", AxiomPalette.Void);
                    pass.Color(serialized, "atmosphere.horizonColor", AxiomPalette.Panel);
                    pass.Color(serialized, "atmosphere.cloudLight", AxiomPalette.Panel);
                }
                else if (component is ShootableTarget) pass.Color(serialized, "hitFlashColor", AxiomPalette.Hot);
                else if (component is GeometricConstruct && component.name == "HeroConstruct")
                {
                    Material band = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "LightLineOrangeBright.mat");
                    SerializedProperty tertiary = serialized.FindProperty("tertiary");
                    if (band == null) pass.Issues.Add("Missing warm HeroConstruct band material");
                    else if (tertiary.objectReferenceValue != band)
                        pass.Change(component, "tertiary", () => { tertiary.objectReferenceValue = band; serialized.ApplyModifiedProperties(); });
                }
                // A new authored color needs a deliberate semantic role; do not silently bless it simply
                // because its current RGB happens to resemble a token. External/package components are excluded.
                if (component.GetType().Namespace?.StartsWith("GeometricFlight", StringComparison.Ordinal) == true)
                {
                    SerializedProperty property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.Color) continue;
                        string path = property.propertyPath;
                        bool assigned = (component is MenuLook &&
                            (path == "accent" || path == "atmosphere.haze" || path == "atmosphere.horizonColor"
                            || path == "atmosphere.cloudLight")) ||
                            (component is ShootableTarget && path == "hitFlashColor");
                        if (!assigned) pass.Issues.Add(component.name + "/" + path + ": unassigned serialized color");
                    }
                }
            }
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (IsGenerated(camera) || camera.backgroundColor.a == 0f) continue;
                Color expected = AxiomPalette.WithAlpha(AxiomPalette.Void, camera.backgroundColor.a);
                if (!Near(camera.backgroundColor, expected)) pass.Change(camera, "backgroundColor", () => camera.backgroundColor = expected);
                if (camera.gameObject.scene.IsValid() && camera.clearFlags == CameraClearFlags.Skybox)
                    pass.Change(camera, "clearFlags", () => camera.clearFlags = CameraClearFlags.SolidColor);
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (IsGenerated(light)) continue;
                Color neutral = Neutral(light.color);
                if (!Near(light.color, neutral)) pass.Change(light, "color", () => light.color = neutral);
                if (light.useColorTemperature) pass.Change(light, "useColorTemperature", () => light.useColorTemperature = false);
            }
            foreach (TrailRenderer trail in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (IsGenerated(trail) || trail.sharedMaterial == null || trail.sharedMaterial.name != "MissileTrail") continue;
                Gradient gradient = trail.colorGradient;
                GradientColorKey[] keys = gradient.colorKeys;
                bool changed = false;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (!Near(keys[i].color, AxiomPalette.White)) changed = true;
                    keys[i].color = AxiomPalette.White;
                }
                if (changed) pass.Change(trail, "colorGradient", () =>
                {
                    // The material supplies Light; vertex RGB stays neutral to avoid multiplying two hues.
                    // Keep the original color-key times, all alpha keys and the gradient interpolation mode.
                    gradient.SetKeys(keys, gradient.alphaKeys);
                    trail.colorGradient = gradient;
                });
            }
        }

        private static void VisitRenderSettings(Pass pass)
        {
            // The old default daylight sky is not authored Grid art, even when it only illuminates
            // reflective surfaces. Use neutral ambient light until an explicit palette sky is authored.
            if (RenderSettings.skybox != null)
            {
                if (pass.Apply) { RenderSettings.skybox = null; pass.Changes++; }
                else pass.Issues.Add("RenderSettings/skybox: legacy sky outside palette");
            }
            if (RenderSettings.ambientMode != AmbientMode.Flat)
            {
                if (pass.Apply) { RenderSettings.ambientMode = AmbientMode.Flat; pass.Changes++; }
                else pass.Issues.Add("RenderSettings/ambientMode: use neutral palette ambient light");
            }
            pass.RenderColor("fogColor", RenderSettings.fogColor, AxiomPalette.Void, value => RenderSettings.fogColor = value);
            pass.RenderColor("ambientSkyColor", RenderSettings.ambientSkyColor, Neutral(RenderSettings.ambientSkyColor), value => RenderSettings.ambientSkyColor = value);
            pass.RenderColor("ambientEquatorColor", RenderSettings.ambientEquatorColor, Neutral(RenderSettings.ambientEquatorColor), value => RenderSettings.ambientEquatorColor = value);
            pass.RenderColor("ambientGroundColor", RenderSettings.ambientGroundColor, Neutral(RenderSettings.ambientGroundColor), value => RenderSettings.ambientGroundColor = value);
            pass.RenderColor("subtractiveShadowColor", RenderSettings.subtractiveShadowColor, Neutral(RenderSettings.subtractiveShadowColor), value => RenderSettings.subtractiveShadowColor = value);
        }

        private static Color Neutral(Color color) => AxiomPalette.WithAlpha(AxiomPalette.White * color.grayscale, color.a);
        private static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < Tolerance && Mathf.Abs(a.g - b.g) < Tolerance && Mathf.Abs(a.b - b.b) < Tolerance && Mathf.Abs(a.a - b.a) < Tolerance;
        private static bool IsGenerated(Component component) => ((component.hideFlags | component.gameObject.hideFlags) & HideFlags.DontSave) != 0;
        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before applying the palette.");
        }
        private static void RequireActiveScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene != SceneManager.GetActiveScene())
                throw new InvalidOperationException("Make the loaded scene active before applying or auditing its RenderSettings.");
        }

        private sealed class Pass
        {
            public readonly bool Apply;
            public readonly List<string> Issues = new List<string>();
            public int Changes;
            public Pass(bool apply) => Apply = apply;

            public void Change(Object target, string property, Action change)
            {
                if (!Apply) { Issues.Add(target.name + "/" + property + ": outside assigned palette role"); return; }
                Undo.RecordObject(target, "Apply Axiom palette");
                change();
                EditorUtility.SetDirty(target);
                if (target is Component component && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                Changes++;
            }

            public void Color(SerializedObject serialized, string path, Color expected)
            {
                SerializedProperty property = serialized.FindProperty(path);
                if (property == null) { Issues.Add(serialized.targetObject.name + ": missing field " + path); return; }
                if (!Near(property.colorValue, expected)) Change(serialized.targetObject, path, () =>
                { property.colorValue = expected; serialized.ApplyModifiedProperties(); });
            }

            public void Float(SerializedObject serialized, string path, float expected)
            {
                SerializedProperty property = serialized.FindProperty(path);
                if (property == null) { Issues.Add(serialized.targetObject.name + ": missing field " + path); return; }
                if (!Mathf.Approximately(property.floatValue, expected)) Change(serialized.targetObject, path, () =>
                { property.floatValue = expected; serialized.ApplyModifiedProperties(); });
            }

            public void RenderColor(string name, Color actual, Color expected, Action<Color> assign)
            {
                if (Near(actual, expected)) return;
                if (!Apply) { Issues.Add("RenderSettings/" + name + ": non-palette tint"); return; }
                assign(expected);
                Changes++;
            }
        }
    }
}
