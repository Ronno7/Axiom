using UnityEditor;
using UnityEditor.SceneManagement;

namespace GeometricFlight.Editor
{
    /// <summary>Opens the game's one scene. It is authored in place and never regenerated.</summary>
    public static class OpeningTools
    {
        private const string ScenePath = "Assets/Scenes/Opening.unity";

        [MenuItem("Tools/Axiom/Open the Opening")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
