using System;
using System.Collections.Generic;
using System.Linq;
using GeometricFlight.Core;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GeometricFlight.Editor
{
    /// <summary>Explicit one-time composition. Source scenes are never saved; existing studies are never regenerated.</summary>
    public static class SharedEntryStudyTools
    {
        public const string ScenePath = "Assets/Scenes/SharedEntryStudy.unity";

        [MenuItem("Tools/Axiom/Open Shared Entry Study")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("SharedEntryStudy already exists. Open and edit it; do not regenerate it.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
            if (!AssetDatabase.CopyAsset(RailHandlingStudyTools.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy the rail study.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            ArenaFlight flight = ComponentIn<ArenaFlight>(scene);
            LevelFlow flow = ComponentIn<LevelFlow>(scene);
            LevelUi ui = ComponentIn<LevelUi>(scene);
            ChaseCamera chase = ComponentIn<ChaseCamera>(scene);
            Camera view = chase.GetComponent<Camera>();
            LevelLook previousLook = ComponentIn<LevelLook>(scene);
            Object.DestroyImmediate(previousLook.gameObject);
            RailStudyDisplay comparison = ComponentIn<RailStudyDisplay>(scene);
            Object.DestroyImmediate(comparison);
            // Retain the original course. Only extend the existing flat approach behind its start.
            Transform floor = scene.GetRootGameObjects().First(g => g.name == "20_EnvironmentBlockout").transform.Find("Floor");
            floor.position = new Vector3(0f, floor.position.y, 300f);
            floor.localScale = new Vector3(floor.localScale.x, floor.localScale.y, 1200f);

            MainMenu menu = null;
            MenuLook look = null;
            MenuBackdrop backdrop = null;
            Scene source = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var copies = new Dictionary<Object, Object>();
                foreach (string name in new[] { "Backdrop", "MainMenuUi", "MenuLook", "MenuTitle" })
                {
                    GameObject original = source.GetRootGameObjects().First(g => g.name == name);
                    GameObject clone = Object.Instantiate(original);
                    clone.name = name;
                    SceneManager.MoveGameObjectToScene(clone, scene);
                    Map(original.transform, clone.transform, copies);
                }
                // Remap every reference between the copied roots (glitch constructs, UI and title included).
                foreach (Object value in copies.Values)
                {
                    if (!(value is Component component)) continue;
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue != null && copies.TryGetValue(property.objectReferenceValue, out Object mapped))
                            property.objectReferenceValue = mapped;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                menu = ComponentIn<MainMenu>(scene);
                look = ComponentIn<MenuLook>(scene);
                backdrop = ComponentIn<MenuBackdrop>(scene);
                Camera originalCamera = source.GetRootGameObjects().First(g => g.name == "Main Camera").GetComponent<Camera>();
                view.transform.SetPositionAndRotation(originalCamera.transform.position + Vector3.left * 2000f,
                    originalCamera.transform.rotation);
                view.fieldOfView = originalCamera.fieldOfView;
                view.cullingMask = originalCamera.cullingMask;
                view.farClipPlane = 700f;
                backdrop.transform.position += Vector3.left * 2000f;
                Set(backdrop, "viewCamera", view);
                Set(look, "menuCamera", view);
                Set(look, "menuDocument", menu.GetComponent<UIDocument>());
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(scene);
            }

            chase.enabled = false;
            Edit(flow, so => so.FindProperty("waitForEntry").boolValue = true);
            menu.GetComponent<UIDocument>().sortingOrder = ui.GetComponent<UIDocument>().sortingOrder + 1f;
            var coordinator = new GameObject("50_SharedEntry").AddComponent<SharedEntryStudy>();
            var presentation = coordinator.gameObject.AddComponent<SharedEntryPresentation>();
            var voice = coordinator.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = 0.45f;
            Transform aircraft = flight.transform.Find("AircraftVisual");
            Set(presentation, "aircraft", aircraft);
            Set(presentation, "shieldDisplay", flight.GetComponent<ShieldDisplay>());
            Set(presentation, "shield", flight.GetComponent<ShipShield>());
            Set(presentation, "levelUi", ui);
            Set(presentation, "constructionMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/OpeningHull.mat"));
            Set(presentation, "traceMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/LightLineCyan.mat"));
            SetArray(presentation, "hullRenderers", aircraft.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.GetComponent<ThrustDisplay>() == null).ToArray());
            Set(coordinator, "flow", flow); Set(coordinator, "flight", flight); Set(coordinator, "chase", chase);
            Set(coordinator, "menu", menu); Set(coordinator, "levelUi", ui); Set(coordinator, "look", look);
            Set(coordinator, "backdrop", backdrop); Set(coordinator, "presentation", presentation);
            Set(coordinator, "missileDisplay", ui.GetComponent<MissileDisplay>());
            ui.GetComponent<MissileDisplay>().enabled = false;
            Set(coordinator, "voice", voice);
            Set(coordinator, "shieldReport", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Entry/shields_online.wav"));
            Set(coordinator, "controlReport", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Entry/flight_control_online.wav"));
            SetArray(coordinator, "flightDetails", new[] { "NearReticle", "AimReticle", "GroundShadow" }
                .Select(n => flight.transform.Find(n)).Where(t => t != null).Select(t => t.gameObject).ToArray());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = coordinator.gameObject;
        }

        private static void Map(Transform original, Transform clone, Dictionary<Object, Object> map)
        {
            map[original.gameObject] = clone.gameObject;
            Component[] before = original.GetComponents<Component>(), after = clone.GetComponents<Component>();
            for (int i = 0; i < before.Length; i++) if (before[i] != null) map[before[i]] = after[i];
            for (int i = 0; i < original.childCount; i++) Map(original.GetChild(i), clone.GetChild(i), map);
        }

        private static T ComponentIn<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).First();
        private static void Edit(Object target, Action<SerializedObject> change)
        {
            var so = new SerializedObject(target); change(so); so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string name, Object value) =>
            Edit(target, so => so.FindProperty(name).objectReferenceValue = value);
        private static void SetArray<T>(Object target, string name, T[] values) where T : Object => Edit(target, so =>
        {
            SerializedProperty array = so.FindProperty(name); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        });
    }
}
