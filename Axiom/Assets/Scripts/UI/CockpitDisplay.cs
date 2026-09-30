using System.Collections.Generic;
using System.Text;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.UI
{
    /// <summary>
    /// The controller's cockpit during a level, complementing the HUD.
    /// LCD: speed bar and speed on top (with the recommended band inside a throttle zone); shield,
    /// targets and time below, replaced by zone guidance, a hit, a proximity warning or a lost
    /// throttle sensor when those matter. Pause mirrors the selected menu item; results page
    /// through the run's stats.
    /// Runway lights beside the throttle point the lever at a zone's band (RunwayLights); the
    /// underglow flashes red on hits and magenta on kills; the fan's airflow follows flight speed,
    /// with a gust through each closing iris.
    /// </summary>
    public sealed class CockpitDisplay
    {
        private const int BarCells = 11;
        private const float PageSeconds = 2f;
        private const float ZoneWarningSeconds = 2.5f;
        private const byte UnderglowRed = 1 << 5, UnderglowBlue = 1 << 7;

        private readonly LevelFlow flow;
        private readonly PlayerFlight flight;
        private readonly ThrottleZone[] zones;
        private readonly ClosingIris[] irises;
        private int lastShield, lastTargets;
        private float hitUntil, killUntil, gustUntil;
        private float lastShipZ;
        private List<string> resultPages;

        public CockpitDisplay(LevelFlow flow, PlayerFlight flight, ThrottleZone[] zones)
        {
            this.flow = flow;
            this.flight = flight;
            this.zones = zones;
            irises = Object.FindObjectsByType<ClosingIris>(FindObjectsSortMode.None);
            lastShield = flow.ShieldRemaining;
            lastTargets = flow.TargetsDestroyed;
            lastShipZ = flight.transform.position.z;
        }

        /// <param name="band">Recommended band as fractions of the speed bar; ignored outside a zone.</param>
        /// <param name="guidance">+1 raise, -1 lower, 0 hold (inside a zone).</param>
        /// <param name="focused">The selected menu item, mirrored while paused.</param>
        public void Show(ThrottleZone zone, Vector2 band, int guidance, ProximityAlarm proximity, string focused)
        {
            float now = Time.unscaledTime;
            if (flow.ShieldRemaining < lastShield) hitUntil = now + 1.2f;
            if (flow.TargetsDestroyed > lastTargets) killUntil = now + 0.25f;
            lastShield = flow.ShieldRemaining;
            lastTargets = flow.TargetsDestroyed;
            float shipZ = flight.transform.position.z;
            foreach (ClosingIris iris in irises)
                if (lastShipZ < iris.transform.position.z && shipZ >= iris.transform.position.z) gustUntil = now + 0.6f;
            lastShipZ = shipZ;
            bool blink = now % 0.5f < 0.25f;
            bool playing = flow.State == RunState.Playing;
            float speedFraction = Mathf.InverseLerp(flight.MinSpeed, flight.MaxSpeed, flight.CurrentSpeed);

            string top, bottom;
            switch (flow.State)
            {
                case RunState.Paused:
                    top = LcdGlyph.Center(flow.PauseReason == PauseReason.ControllerLost ? "RECONNECTED" : "PAUSED");
                    bottom = focused == null ? "" : $"{LcdGlyph.Right} {focused}";
                    break;
                case RunState.Completed:
                case RunState.Failed:
                    bool completed = flow.State == RunState.Completed;
                    top = completed ? $"{LcdGlyph.Target}LEVEL COMPLETE{LcdGlyph.Target}" : LcdGlyph.Center("SHIP DESTROYED");
                    resultPages ??= ResultPages(completed);
                    bottom = resultPages[(int)(now / PageSeconds) % resultPages.Count];
                    break;
                default:
                    int speedPercent = Mathf.RoundToInt(flight.CurrentSpeed / flight.CruiseSpeed * 100f);
                    top = (zone == null ? LcdGlyph.Bar(speedFraction, BarCells) : LcdGlyph.Bar(speedFraction, BarCells, band.x, band.y))
                        + $" {speedPercent,3}%";
                    bottom = Status(zone, guidance, proximity, blink, now);
                    break;
            }
            ControllerService.Instance.Display.Show(top, bottom);

            byte lights = playing ? RunwayPattern(zone, guidance, now) : (byte)0;
            if (now < hitUntil - 0.8f) lights |= UnderglowRed;
            else if (now < killUntil) lights |= UnderglowRed | UnderglowBlue;
            ControllerService.Instance.Outputs.Set(lights, playing ? FanLevel(speedFraction, now) : (byte)0, now);
        }

        private string Status(ThrottleZone zone, int guidance, ProximityAlarm proximity, bool blink, float now)
        {
            ControllerState controller = ControllerService.Instance.State;
            if (controller.Connected && !controller.ThrottleValid)
                return LcdGlyph.Center(now % 1f < 0.5f ? "THROTTLE NO ECHO" : "HOLDING CRUISE");
            if (proximity.Active)
                return LcdGlyph.Center(proximity.Pulse ? "! PROXIMITY !" : "");
            if (now < hitUntil && blink)
                return LcdGlyph.Center($"SHIELD HIT {flow.ShieldRemaining}/{flow.ShieldMax}");
            if (zone == null)
                return LcdGlyph.Spread($"{Shields()} {LcdGlyph.Target}{flow.TargetsDestroyed}/{flow.TargetsTotal}",
                    UiScreens.FormatTime(flow.ElapsedTime));

            string kind = zone.Type == ThrottleZone.Kind.Slow ? "SLOW" : "FAST";
            if (guidance == 0) return LcdGlyph.Spread(kind, $"{LcdGlyph.Target} HOLD {LcdGlyph.Target}");
            char arrow = guidance < 0 ? LcdGlyph.Left : LcdGlyph.Right;
            string arrows = blink ? new string(arrow, 2) : "  ";
            return LcdGlyph.Spread(kind, arrows + (guidance < 0 ? " LOWER" : " RAISE"));
        }

        /// <summary>Lights the current zone's band, or blinks the next zone's band shortly before it.</summary>
        private byte RunwayPattern(ThrottleZone zone, int guidance, float now)
        {
            bool approaching = false;
            if (zone == null)
            {
                float z = flight.transform.position.z;
                float lookahead = flight.CurrentSpeed * ZoneWarningSeconds;
                foreach (ThrottleZone candidate in zones)
                {
                    float ahead = candidate.transform.position.z - z;
                    if (ahead > 0f && ahead <= lookahead) { zone = candidate; approaching = true; break; }
                }
                if (zone == null) return 0;
            }
            ControllerService service = ControllerService.Instance;
            ControllerCalibration calibration = service.Calibration;
            Vector2 speeds = zone.RecommendedCruiseMultiple * flight.CruiseSpeed;
            var leverBand = new Vector2(Lever(flight.ThrottleForSpeed(speeds.x)), Lever(flight.ThrottleForSpeed(speeds.y)));
            return RunwayLights.Pattern(Lever(service.State.Throttle), leverBand,
                calibration.throttleMinMm <= calibration.throttleMaxMm, !approaching && guidance == 0, approaching, now);
        }

        private static float Lever(float throttle) => (throttle + 1f) * 0.5f;

        /// <summary>Still air near minimum speed, rising to full at maximum, with a gust through each iris.</summary>
        private byte FanLevel(float speedFraction, float now)
        {
            if (now < gustUntil) return 255;
            return speedFraction < 0.08f ? (byte)0 : (byte)Mathf.RoundToInt(255f * speedFraction);
        }

        private string Shields()
        {
            var shields = new StringBuilder(flow.ShieldMax);
            for (int i = 0; i < flow.ShieldMax; i++)
                shields.Append(i < flow.ShieldRemaining ? LcdGlyph.ShieldUp : LcdGlyph.ShieldDown);
            return shields.ToString();
        }

        // Results are final once shown, so the pages are built once.
        private List<string> ResultPages(bool completed)
        {
            var pages = new List<string> { LcdGlyph.Spread("TIME", UiScreens.FormatTime(flow.ElapsedTime)) };
            if (completed && flow.HasPar)
                pages.Add(LcdGlyph.Spread("VS PAR", $"{flow.ElapsedTime - flow.ParTime:+0.0;-0.0} s"));
            pages.Add(LcdGlyph.Spread("TARGETS", $"{flow.TargetsDestroyed}/{flow.TargetsTotal}"));
            pages.Add(LcdGlyph.Spread("ACCURACY",
                flow.ShotsFired == 0 ? "-" : $"{Mathf.RoundToInt(flow.Accuracy * 100f)}%"));
            pages.Add(LcdGlyph.Spread("SHIELD", Shields()));
            return pages;
        }
    }
}
