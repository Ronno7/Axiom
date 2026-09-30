using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>Title screen: Play, Options, Help, Quit. Esc (UI cancel) returns from sub-screens.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private string firstLevelScene = "Prototype";

        private VisualElement titlePanel;
        private VisualElement helpPanel;
        private OptionsPanel options;
        private Button playButton;
        private Button helpBack;

        private void OnEnable()
        {
            Time.timeScale = 1f;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            titlePanel = root.Q("title-panel");
            helpPanel = root.Q("help-panel");
            options = new OptionsPanel(root.Q("options-panel"), ShowTitle);
            playButton = root.Q<Button>("play-button");
            helpBack = root.Q<Button>("help-back");

            playButton.clicked += () => SceneManager.LoadScene(firstLevelScene);
            root.Q<Button>("options-button").clicked +=
                () => UiScreens.ShowOnly(options.Root, options.FirstFocus, Panels);
            root.Q<Button>("help-button").clicked += () => UiScreens.ShowOnly(helpPanel, helpBack, Panels);
            root.Q<Button>("quit-button").clicked += UiScreens.QuitGame;
            helpBack.clicked += ShowTitle;
            root.RegisterCallback<NavigationCancelEvent>(_ => Back());
            ShowTitle();
        }

        private VisualElement[] Panels => new[] { titlePanel, options.Root, helpPanel };

        private void ShowTitle() => UiScreens.ShowOnly(titlePanel, playButton, Panels);

        private void Back()
        {
            if (options.Root.resolvedStyle.display == DisplayStyle.Flex) options.Close();
            else if (helpPanel.resolvedStyle.display == DisplayStyle.Flex) ShowTitle();
        }
    }
}
