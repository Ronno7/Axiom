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
        private const string FanKey = "axiom.fanEnabled";

        private static bool loaded;
        private static float steeringSensitivity = 1f;
        private static bool invertVertical;
        private static bool buzzerEnabled = true;
        private static bool fanEnabled = true;

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

        /// <summary>Controller fan (airflow follows flight speed). The speed bar shows the same thing.</summary>
        public static bool FanEnabled
        {
            get { Load(); return fanEnabled; }
            set
            {
                Load();
                fanEnabled = value;
                PlayerPrefs.SetInt(FanKey, value ? 1 : 0);
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
            fanEnabled = PlayerPrefs.GetInt(FanKey, 1) == 1;
        }
    }
}
