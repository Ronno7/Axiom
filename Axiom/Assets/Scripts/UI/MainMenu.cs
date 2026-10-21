using GeometricFlight.Input;
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
        [Tooltip("The 3D title, rendered by the MenuTitle camera on the Title layer.")]
        [SerializeField] private RenderTexture titleTexture;

        private static readonly string LcdTitle =
            LcdGlyph.Center($"{LcdGlyph.Target} A X I O M {LcdGlyph.Target}");

        private VisualElement root;
        private VisualElement titlePanel;
        private VisualElement helpPanel;
        private OptionsPanel options;
        private CalibrationPanel calibration;
        private Button playButton;
        private Button helpBack;

        private void OnEnable()
        {
            Time.timeScale = 1f;
            root = GetComponent<UIDocument>().rootVisualElement;
            titlePanel = root.Q("title-panel");
            helpPanel = root.Q("help-panel");
            options = new OptionsPanel(root.Q("options-panel"), ShowTitle, ShowCalibration);
            calibration = new CalibrationPanel(root.Q("calibration-panel"), ShowOptions);
            playButton = root.Q<Button>("play-button");
            helpBack = root.Q<Button>("help-back");
            if (titleTexture != null)
                root.Q("game-title").style.backgroundImage = Background.FromRenderTexture(titleTexture);

            playButton.clicked += () => SceneManager.LoadScene(firstLevelScene);
            root.Q<Button>("options-button").clicked += ShowOptions;
            root.Q<Button>("help-button").clicked += () => UiScreens.ShowOnly(helpPanel, helpBack, Panels);
            root.Q<Button>("quit-button").clicked += UiScreens.QuitGame;
            helpBack.clicked += ShowTitle;
            root.RegisterCallback<NavigationCancelEvent>(_ => Back());
            ShowTitle();
        }

        private VisualElement[] Panels => new[] { titlePanel, options.Root, helpPanel, calibration.Root };

        // The LCD mirrors the selected item; calibration replaces it while open.
        private void Update()
        {
            string focused = UiScreens.FocusedLabel(root);
            ControllerService.Instance.Display.Show(LcdTitle, focused == null ? "" : $"{LcdGlyph.Right} {focused}");
            calibration.Tick();
        }

        private void ShowOptions() => UiScreens.ShowOnly(options.Root, options.FirstFocus, Panels);

        private void ShowCalibration()
        {
            calibration.Open();
            UiScreens.ShowOnly(calibration.Root, calibration.FirstFocus, Panels);
        }

        private void ShowTitle() => UiScreens.ShowOnly(titlePanel, playButton, Panels);

        private void Back()
        {
            if (calibration.IsOpen) calibration.Cancel();
            else if (options.Root.resolvedStyle.display == DisplayStyle.Flex) options.Close();
            else if (helpPanel.resolvedStyle.display == DisplayStyle.Flex) ShowTitle();
        }
    }
}
