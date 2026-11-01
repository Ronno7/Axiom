using GeometricFlight.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>
    /// Lets the cockpit drive every menu the way the keyboard does: the stick sends the same navigation moves as
    /// the arrow keys (once on a push, then repeating while held) and a FIRE press submits the focused button.
    /// It only acts while a visible menu element has focus, so flight input is never touched. Created once at
    /// start-up and kept across scenes.
    /// </summary>
    public sealed class CockpitMenuNavigation : MonoBehaviour
    {
        private const float PushAt = 0.6f, ReleaseAt = 0.35f, FirstRepeat = 0.5f, Repeat = 0.18f;

        private UIDocument[] documents = new UIDocument[0];
        private NavigationMoveEvent.Direction held = NavigationMoveEvent.Direction.None;
        private float nextRepeat;
        private bool previousFire = true; // A FIRE already held when a menu appears must be released first.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("Cockpit menu navigation");
            DontDestroyOnLoad(host);
            host.AddComponent<CockpitMenuNavigation>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindDocuments();
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => FindDocuments();

        private void FindDocuments() =>
            documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private void Update()
        {
            ControllerService service = ControllerService.Current;
            ControllerState device = service != null ? service.State : default;
            VisualElement focused = device.Connected ? FocusedMenuElement() : null;
            if (focused == null)
            {
                held = NavigationMoveEvent.Direction.None;
                previousFire = true;
                return;
            }

            NavigationMoveEvent.Direction direction = DirectionOf(device.Stick, held);
            float now = Time.unscaledTime; // Menus run while the game is paused.
            if (direction != held)
            {
                held = direction;
                nextRepeat = now + FirstRepeat;
                if (direction != NavigationMoveEvent.Direction.None) Move(focused, direction);
            }
            else if (direction != NavigationMoveEvent.Direction.None && now >= nextRepeat)
            {
                nextRepeat = now + Repeat;
                Move(focused, direction);
            }

            if (device.Fire && !previousFire)
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = focused;
                    focused.SendEvent(submit);
                }
            previousFire = device.Fire;
        }

        /// <summary>Up/down/left/right past the push point; a held direction lasts until the stick comes back
        /// inside the release point, so a wobble near the edge doesn't repeat.</summary>
        public static NavigationMoveEvent.Direction DirectionOf(Vector2 stick, NavigationMoveEvent.Direction held)
        {
            bool vertical = Mathf.Abs(stick.y) >= Mathf.Abs(stick.x);
            float amount = vertical ? Mathf.Abs(stick.y) : Mathf.Abs(stick.x);
            float threshold = held == NavigationMoveEvent.Direction.None ? PushAt : ReleaseAt;
            if (amount < threshold) return NavigationMoveEvent.Direction.None;
            if (vertical) return stick.y > 0f ? NavigationMoveEvent.Direction.Up : NavigationMoveEvent.Direction.Down;
            return stick.x > 0f ? NavigationMoveEvent.Direction.Right : NavigationMoveEvent.Direction.Left;
        }

        private static void Move(VisualElement focused, NavigationMoveEvent.Direction direction)
        {
            using (NavigationMoveEvent move = NavigationMoveEvent.GetPooled(direction))
            {
                move.target = focused;
                focused.SendEvent(move);
            }
        }

        /// <summary>The focused element of a visible menu in any of the scene's documents, or null.</summary>
        private VisualElement FocusedMenuElement()
        {
            bool stale = false;
            foreach (UIDocument document in documents)
            {
                if (document == null) { stale = true; continue; }
                if (!document.isActiveAndEnabled || document.rootVisualElement == null) continue;
                var focused = document.rootVisualElement.panel?.focusController?.focusedElement as VisualElement;
                if (focused != null && Shown(focused)) return focused;
            }
            if (stale) FindDocuments();
            return null;
        }

        private static bool Shown(VisualElement element)
        {
            if (!element.enabledInHierarchy || !element.canGrabFocus) return false;
            for (VisualElement e = element; e != null; e = e.hierarchy.parent)
            {
                IResolvedStyle style = e.resolvedStyle;
                if (style.display == DisplayStyle.None || style.visibility == Visibility.Hidden || style.opacity <= 0.01f)
                    return false;
            }
            return true;
        }
    }
}
