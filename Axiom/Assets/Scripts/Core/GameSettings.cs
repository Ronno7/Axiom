using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>Player options persisted with PlayerPrefs. Values apply immediately; call Save when
    /// the options screen closes (Unity also saves PlayerPrefs on quit).</summary>
    public static class GameSettings
    {
        public const float MinSteeringSensitivity = 0.5f;
        public const float MaxSteeringSensitivity = 1.5f;

        private const string SensitivityKey = "axiom.steeringSensitivity";
        private const string InvertVerticalKey = "axiom.invertVertical";
        private const string BuzzerKey = "axiom.buzzerEnabled";
        private const string ArcadeKey = "axiom.arcadeMode";
        private const string KeyboardThrottleKey = "axiom.keyboardThrottleWithController";
        private const string ReducedEntryKey = "axiom.reducedEntryAnimation";

        private static bool loaded;
        private static float steeringSensitivity = 1f;
        private static bool invertVertical;
        private static bool buzzerEnabled = true;
        private static bool arcadeMode;
        private static bool keyboardThrottleWithController;
        private static bool reducedEntryAnimation;

        /// <summary>Multiplies steering speed. 1 is the tuned default.</summary>
        public static float SteeringSensitivity
        {
            get { Load(); return steeringSensitivity; }
            set
            {
                Load();
                steeringSensitivity = Mathf.Clamp(value, MinSteeringSensitivity, MaxSteeringSensitivity);
                PlayerPrefs.SetFloat(SensitivityKey, steeringSensitivity);
            }
        }

        public static bool InvertVertical
        {
            get { Load(); return invertVertical; }
            set
            {
                Load();
                invertVertical = value;
                PlayerPrefs.SetInt(InvertVerticalKey, value ? 1 : 0);
            }
        }

        /// <summary>Controller buzzer cues. Every cue also has an on-screen equivalent.</summary>
        public static bool BuzzerEnabled
        {
            get { Load(); return buzzerEnabled; }
            set
            {
                Load();
                buzzerEnabled = value;
                PlayerPrefs.SetInt(BuzzerKey, value ? 1 : 0);
            }
        }

        /// <summary>Vintage arcade-monitor look: phosphor glow, CRT, red fringing and the like.</summary>
        public static bool ArcadeMode
        {
            get { Load(); return arcadeMode; }
            set
            {
                Load();
                arcadeMode = value;
                PlayerPrefs.SetInt(ArcadeKey, value ? 1 : 0);
            }
        }

        /// <summary>Use the latched Q/E throttle while retaining the connected cockpit's stick,
        /// buttons and MASTER ARM. Useful before the physical throttle is installed.</summary>
        public static bool KeyboardThrottleWithController
        {
            get { Load(); return keyboardThrottleWithController; }
            set
            {
                Load();
                keyboardThrottleWithController = value;
                PlayerPrefs.SetInt(KeyboardThrottleKey, value ? 1 : 0);
            }
        }

        /// <summary>Quiet entry preference shared by first and returning players.</summary>
        public static bool ReducedEntryAnimation
        {
            get { Load(); return reducedEntryAnimation; }
            set
            {
                Load();
                reducedEntryAnimation = value;
                PlayerPrefs.SetInt(ReducedEntryKey, value ? 1 : 0);
            }
        }

        public static void Save() => PlayerPrefs.Save();

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            steeringSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f),
                MinSteeringSensitivity, MaxSteeringSensitivity);
            invertVertical = PlayerPrefs.GetInt(InvertVerticalKey, 0) == 1;
            buzzerEnabled = PlayerPrefs.GetInt(BuzzerKey, 1) == 1;
            arcadeMode = PlayerPrefs.GetInt(ArcadeKey, 0) == 1;
            keyboardThrottleWithController = PlayerPrefs.GetInt(KeyboardThrottleKey, 0) == 1;
            reducedEntryAnimation = PlayerPrefs.GetInt(ReducedEntryKey, 0) == 1;
        }
    }
}
