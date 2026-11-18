using UnityEngine;

namespace GeometricFlight.Dev
{
    /// <summary>Developer cheats for testing, set from <see cref="DevPanel"/>. They exist only in the Editor and in
    /// Development builds: in a release build every one reads false and setting it does nothing. The toggles persist in
    /// PlayerPrefs, so god mode stays on across play sessions until it is turned off. They only apply in play, and tests
    /// that play the game set <see cref="Suspended"/>, so saved cheats can't change what a test checks.</summary>
    public static class DevCheats
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const bool Available = true;
#else
        public const bool Available = false;
#endif

        private const string GodModeKey = "axiom.dev.godMode";
        private const string InfiniteMissilesKey = "axiom.dev.infiniteMissiles";
        private const string InfiniteEmpKey = "axiom.dev.infiniteEmp";
        private const string InfiniteBoostKey = "axiom.dev.infiniteBoost";

        /// <summary>Every cheat reads false while set, without changing the saved toggles (tests).</summary>
        public static bool Suspended { get; set; }

        private static bool loaded;
        private static bool godMode, infiniteMissiles, infiniteEmp, infiniteBoost;

        /// <summary>The shield refuses every hit, lethal walls included (<see cref="Player.ShipShield"/>).</summary>
        public static bool GodMode
        {
            get => Get(ref godMode);
            set => Set(ref godMode, value, GodModeKey);
        }

        /// <summary>The missile bank tops up as soon as it is spent, once missiles have been issued.</summary>
        public static bool InfiniteMissiles
        {
            get => Get(ref infiniteMissiles);
            set => Set(ref infiniteMissiles, value, InfiniteMissilesKey);
        }

        /// <summary>The EMP recharges as soon as its pulse ends, once it has been issued.</summary>
        public static bool InfiniteEmp
        {
            get => Get(ref infiniteEmp);
            set => Set(ref infiniteEmp, value, InfiniteEmpKey);
        }

        /// <summary>The boost recharges as soon as a burst ends.</summary>
        public static bool InfiniteBoost
        {
            get => Get(ref infiniteBoost);
            set => Set(ref infiniteBoost, value, InfiniteBoostKey);
        }

        public static bool AnyActive => GodMode || InfiniteMissiles || InfiniteEmp || InfiniteBoost;

        public static void ClearAll() => GodMode = InfiniteMissiles = InfiniteEmp = InfiniteBoost = false;

        private static bool Get(ref bool field)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Suspended || !Application.isPlaying) return false;
            Load();
            return field;
#else
            return false;
#endif
        }

        private static void Set(ref bool field, bool value, string key)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Load();
            field = value;
            PlayerPrefs.SetInt(key, value ? 1 : 0);
#endif
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            godMode = PlayerPrefs.GetInt(GodModeKey, 0) == 1;
            infiniteMissiles = PlayerPrefs.GetInt(InfiniteMissilesKey, 0) == 1;
            infiniteEmp = PlayerPrefs.GetInt(InfiniteEmpKey, 0) == 1;
            infiniteBoost = PlayerPrefs.GetInt(InfiniteBoostKey, 0) == 1;
        }
    }
}
