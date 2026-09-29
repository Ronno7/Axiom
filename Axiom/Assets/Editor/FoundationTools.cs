using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GeometricFlight.Editor
{
    public static class FoundationTools
    {
        private const string ScenePath = "Assets/Scenes/Prototype.unity";

        [MenuItem("Tools/Geometric Flight/Open Prototype")]
        public static void OpenPrototype()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("Prototype.unity was not found at Assets/Scenes/Prototype.unity.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Geometric Flight/Check Foundation")]
        public static void CheckFoundation()
        {
            if (GraphicsSettings.currentRenderPipeline == null ||
                GraphicsSettings.currentRenderPipeline.GetType().Name != "UniversalRenderPipelineAsset")
            {
                Debug.LogError("Use a Universal 3D project with URP assigned in Graphics/Quality settings.");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("Open Prototype.unity before checking its scene objects.");
                return;
            }

            int missing = 0;
            int cameras = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                cameras += root.GetComponentsInChildren<Camera>(true).Length;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || !material.shader.isSupported)
                            missing++;
            }
            if (missing > 0 || cameras != 1)
                Debug.LogError($"Foundation needs attention: {missing} missing/unsupported references, {cameras} cameras. Inspect the Console.");
            else
                Debug.Log("Foundation references look correct: one camera, URP active, no missing scripts/materials. Play Mode has forward flight, WASD/arrow steering, Q/E throttle, and Space hold-to-fire at shootable targets.");
        }
    }
}
