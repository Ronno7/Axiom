using UnityEngine;

namespace GeometricFlight.Core
{
    /// <summary>The clock menus and instruments animate on: real time, so they keep moving while the game is paused. A film
    /// renders each frame far slower than it plays, so a render makes this follow game time to keep them in step.</summary>
    public static class UiClock
    {
        public static bool FollowGameTime { get; set; }
        public static float Now => FollowGameTime ? Time.time : Time.unscaledTime;
        public static float Delta => FollowGameTime ? Time.deltaTime : Time.unscaledDeltaTime;
    }
}
