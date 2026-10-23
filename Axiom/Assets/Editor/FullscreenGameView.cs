using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace GeometricFlight.Editor
{
    // Covers the editor's monitor with a borderless Game view during Play Mode (F11).
    // Unity removed its own fullscreen Game view in 2022.2, so this opens the internal GameView
    // as a popup with its toolbar hidden. The window's instance ID is kept in SessionState so it
    // can be found again after a domain reload. It always closes when Play Mode ends.
    [InitializeOnLoad]
    public static class FullscreenGameView
    {
        private const string MenuPath = "Window/General/Fullscreen Game View";
        private const string WindowKey = "GeometricFlight.FullscreenGameView";

        private static readonly Type GameViewType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        private static readonly PropertyInfo ShowToolbar =
            GameViewType?.GetProperty("showToolbar", BindingFlags.Instance | BindingFlags.NonPublic);

        static FullscreenGameView()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) Close();
            };
            EditorApplication.delayCall += () =>
            {
                // A popup left over from a recompile or crash would otherwise cover the editor.
                if (!EditorApplication.isPlaying) Close();
                // GameView.OnEnable turns the toolbar back on after every domain reload,
                // including the one that enters Play Mode.
                else if (Find() is EditorWindow window) ShowToolbar?.SetValue(window, false);
            };
        }

        public static bool IsOpen => Find() != null;

        // A menu hotkey, not [Shortcut]: Unity drops [Shortcut] bindings while the Game view has
        // focus in Play Mode, which is exactly when this is pressed.
        [MenuItem(MenuPath + " _F11", priority = 2)]
        public static void Toggle()
        {
            if (IsOpen)
            {
                Close();
                // Otherwise Windows hands focus to whichever app was behind the popup.
                foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(GameViewType))
                {
                    ((EditorWindow)view).Focus();
                    break;
                }
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Fullscreen Game View is available in Play Mode only.");
                return;
            }
            if (ShowToolbar == null)
            {
                Debug.LogWarning("Fullscreen Game View: this Unity version no longer exposes GameView.showToolbar.");
                return;
            }

            var window = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
            ShowToolbar.SetValue(window, false);
            SessionState.SetInt(WindowKey, window.GetInstanceID());
            window.ShowPopup();
            window.position = MonitorRect();
#if UNITY_EDITOR_WIN
            CoverTaskbar();
#endif
            window.Focus();
        }

        [MenuItem(MenuPath + " _F11", true)]
        private static bool CanToggle()
        {
            bool open = IsOpen;
            Menu.SetChecked(MenuPath, open);
            return open || EditorApplication.isPlaying;
        }

        private static void Close()
        {
            if (GameViewType == null) return;
            foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(GameViewType))
                if (view is EditorWindow window && IsFullscreen(window))
                    window.Close();
            SessionState.EraseInt(WindowKey);
        }

        private static EditorWindow Find()
        {
            if (GameViewType == null) return null;
            foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(GameViewType))
                if (view is EditorWindow window && IsFullscreen(window))
                    return window;
            return null;
        }

        // Docked Game views always show their toolbar, so a hidden one also marks a popup of ours
        // that outlived its SessionState entry.
        private static bool IsFullscreen(EditorWindow window) =>
            window.GetInstanceID() == SessionState.GetInt(WindowKey, 0) ||
            (ShowToolbar != null && !(bool)ShowToolbar.GetValue(window));

        // The whole monitor under the editor's main window, in editor points.
        private static Rect MonitorRect()
        {
#if UNITY_EDITOR_WIN
            float scale = EditorGUIUtility.pixelsPerPoint;
            if (TryGetMonitor(out Bounds r))
                return new Rect(r.Left / scale, r.Top / scale, (r.Right - r.Left) / scale, (r.Bottom - r.Top) / scale);
#endif
            return EditorGUIUtility.GetMainWindowPosition();
        }

#if UNITY_EDITOR_WIN
        private const uint MonitorDefaultToNearest = 2;
        private const uint NoZOrderNoActivate = 0x0014;

        private static bool TryGetMonitor(out Bounds bounds)
        {
            Rect editor = EditorGUIUtility.GetMainWindowPosition();
            float scale = EditorGUIUtility.pixelsPerPoint;
            var centre = new Point { X = (int)(editor.center.x * scale), Y = (int)(editor.center.y * scale) };
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            bool found = GetMonitorInfo(MonitorFromPoint(centre, MonitorDefaultToNearest), ref info);
            bounds = info.Monitor;
            return found;
        }

        // Unity keeps popups inside the work area, leaving the taskbar strip uncovered. Stretch the
        // native window over the whole monitor; Unity follows the resize.
        private static void CoverTaskbar()
        {
            if (!TryGetMonitor(out Bounds monitor)) return;
            uint process = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((handle, _) =>
            {
                GetWindowThreadProcessId(handle, out uint owner);
                if (owner != process || !IsWindowVisible(handle) || !GetWindowRect(handle, out Bounds r))
                    return true;
                if (r.Left != monitor.Left || r.Top != monitor.Top || r.Right != monitor.Right || r.Bottom >= monitor.Bottom)
                    return true;
                SetWindowPos(handle, IntPtr.Zero, monitor.Left, monitor.Top,
                    monitor.Right - monitor.Left, monitor.Bottom - monitor.Top, NoZOrderNoActivate);
                return false;
            }, IntPtr.Zero);
        }

        private delegate bool EnumWindowsProc(IntPtr handle, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr handle, out Bounds bounds);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct Point { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Bounds { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Bounds Monitor;
            public Bounds Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Point point, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
#endif
    }
}
