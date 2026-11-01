using System;
using System.Linq;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GeometricFlight.Editor
{
    /// <summary>Explicit, one-time copy of the authored menu for the departure comparison.
    /// Never runs at import, regenerates an existing study, or saves the source scenes.</summary>
    public static class MenuDepartureStudyTools
    {
        public const string ScenePath = "Assets/Scenes/MenuDepartureStudy.unity";
        private const string MenuPath = "Assets/Scenes/MainMenu.unity";
        private const string PrototypePath = "Assets/Scenes/Prototype.unity";

        [MenuItem("Tools/Axiom/Open Menu Departure Study")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating the departure study.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("MenuDepartureStudy already exists. Open and edit it; do not regenerate it.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene edits before creating the departure study.");
            if (!AssetDatabase.CopyAsset(MenuPath, ScenePath))
                throw new InvalidOperationException("Could not copy the original MainMenu scene.");

            Scene studyScene = EditorSceneManager.OpenScene(ScenePath);
            GameObject backdropObject = Root(studyScene, "Backdrop");
            Camera view = Root(studyScene, "Main Camera").GetComponent<Camera>();
            MainMenu menu = Root(studyScene, "MainMenuUi").GetComponent<MainMenu>();
            MenuLook look = Root(studyScene, "MenuLook").GetComponent<MenuLook>();
            MenuBackdrop backdrop = backdropObject.GetComponent<MenuBackdrop>();
            if (view == null || menu == null || look == null || backdrop == null)
                throw new InvalidOperationException("The copied menu is missing a required presentation component.");

            Transform world = new GameObject("DepartureWorld").transform;
            SceneManager.MoveGameObjectToScene(world.gameObject, studyScene);
            backdropObject.transform.SetParent(world, true);
            ExtendFloor(backdropObject.transform);
            view.farClipPlane = 700f;

            Transform arrival = new GameObject("ArrivalReference").transform;
            arrival.SetParent(world, false);
            Transform[] frames = CopyArrivalFrames(studyScene, arrival);

            var study = menu.gameObject.AddComponent<MenuDepartureStudy>();
            var serialized = new SerializedObject(study);
            serialized.FindProperty("view").objectReferenceValue = view;
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.FindProperty("look").objectReferenceValue = look;
            serialized.FindProperty("backdrop").objectReferenceValue = backdrop;
            serialized.FindProperty("world").objectReferenceValue = world;
            serialized.FindProperty("arrival").objectReferenceValue = arrival;
            SerializedProperty frameArray = serialized.FindProperty("frames");
            frameArray.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // MenuLook remains the sole grade, atmosphere and CRT owner. Its authored settings,
            // the menu/title cameras and UI layout are otherwise unchanged.
            SceneManager.SetActiveScene(studyScene);
            EditorSceneManager.MarkSceneDirty(studyScene);
            EditorSceneManager.SaveScene(studyScene);
            Selection.activeGameObject = study.gameObject;
        }

        private static void ExtendFloor(Transform backdrop)
        {
            Transform floorGroup = Required(backdrop, "Floor");
            foreach (string name in new[] { "Floor", "EdgeStrip_L", "EdgeStrip_R" })
            {
                Transform piece = Required(floorGroup, name);
                Vector3 position = piece.localPosition;
                Vector3 scale = piece.localScale;
                position.z = 335f;
                scale.z = 700f;
                piece.localPosition = position;
                piece.localScale = scale;
            }
        }

        private static Transform[] CopyArrivalFrames(Scene studyScene, Transform arrival)
        {
            // Source ExecuteAlways look components can temporarily write the active scene's fog.
            // Restore the copied menu's values before saving; no source scene is ever saved.
            bool fog = RenderSettings.fog;
            FogMode fogMode = RenderSettings.fogMode;
            Color fogColor = RenderSettings.fogColor;
            float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
            var frames = new Transform[8];
            Scene source = EditorSceneManager.OpenScene(PrototypePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(studyScene);
                Transform template = Required(Root(source, "20_EnvironmentBlockout").transform, "HexagonalGate_1");
                for (int i = 0; i < frames.Length; i++)
                {
                    GameObject frame = Object.Instantiate(template.gameObject, arrival);
                    frame.name = "Arrival frame " + (i + 1);
                    frame.transform.position = new Vector3(0f, 5f, 90f + i * 22f);
                    frame.transform.localScale = Vector3.one;
                    foreach (Collider collider in frame.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(collider);
                    foreach (MonoBehaviour component in frame.GetComponentsInChildren<MonoBehaviour>(true))
                        if (component != null && !(component is PrototypeMesh)) Object.DestroyImmediate(component);
                    PrototypeMesh shape = frame.GetComponent<PrototypeMesh>();
                    if (shape == null) throw new InvalidOperationException("The source hoop requires its PrototypeMesh provider.");
                    var meshSettings = new SerializedObject(shape);
                    // Preserve the six-sided, 30-degree-rotated source ring and material. Radius 12
                    // leaves at least 20 m across the opening with the authored 0.6 m band width.
                    meshSettings.FindProperty("radius").floatValue = 12f;
                    meshSettings.ApplyModifiedPropertiesWithoutUndo();
                    shape.Rebuild();
                    frame.SetActive(true);
                    frames[i] = frame.transform;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(studyScene);
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
            }
            return frames;
        }

        private static GameObject Root(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(r => r.name == name)
            ?? throw new InvalidOperationException($"Missing source root {name} in {scene.path}.");

        private static Transform Required(Transform parent, string path) => parent.Find(path)
            ?? throw new InvalidOperationException($"Missing source object {parent.name}/{path}.");
    }
}
