using System.Collections.Generic;
using System.Text;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;

namespace GeometricFlight.UI
{
    /// <summary>
    /// The controller LCD during a level: an instrument panel on the player's hands that complements
    /// the HUD. Top row: speed bar and speed, with the recommended band inside a throttle zone. Bottom
    /// row: shield, targets and time, replaced by zone guidance, a hit, a proximity warning or a lost throttle sensor when
    /// those matter. Pause mirrors the selected menu item; results page through the run's stats.
    /// </summary>
    public sealed class CockpitDisplay
    {
        private const int BarCells = 11;
        private const float PageSeconds = 2f;

        private readonly LevelFlow flow;
        private readonly PlayerFlight flight;
        private int lastShield;
        private float hitUntil;
        private List<string> resultPages;

        public CockpitDisplay(LevelFlow flow, PlayerFlight flight)
        {
            this.flow = flow;
            this.flight = flight;
            lastShield = flow.ShieldRemaining;
        }

        /// <param name="band">Recommended band as fractions of the speed bar; ignored outside a zone.</param>
        /// <param name="guidance">+1 raise, -1 lower, 0 hold (inside a zone).</param>
        /// <param name="focused">The selected menu item, mirrored while paused.</param>
        public void Show(ThrottleZone zone, Vector2 band, int guidance, ProximityAlarm proximity, string focused)
        {
            float now = Time.unscaledTime;
            if (flow.ShieldRemaining < lastShield) hitUntil = now + 1.2f;
            lastShield = flow.ShieldRemaining;
            bool blink = now % 0.5f < 0.25f;

            string top, bottom;
            switch (flow.State)
            {
                case RunState.Paused:
                    top = LcdGlyph.Center(flow.PauseReason == PauseReason.ControllerLost ? "RECONNECTED" : "PAUSED");
                    bottom = focused == null ? "" : $"{LcdGlyph.Right} {focused}";
                    break;
                case RunState.Completed:
                case RunState.Failed:
                    bool completed = flow.Results.Completed;
                    top = completed ? $"{LcdGlyph.Target}LEVEL COMPLETE{LcdGlyph.Target}" : LcdGlyph.Center("SHIP DESTROYED");
                    resultPages ??= ResultPages(flow.Results);
                    bottom = resultPages[(int)(now / PageSeconds) % resultPages.Count];
                    break;
                default:
                    float speedFraction = Mathf.InverseLerp(flight.MinSpeed, flight.MaxSpeed, flight.CurrentSpeed);
                    int speedPercent = Mathf.RoundToInt(flight.CurrentSpeed / flight.CruiseSpeed * 100f);
                    top = (zone == null ? LcdGlyph.Bar(speedFraction, BarCells) : LcdGlyph.Bar(speedFraction, BarCells, band.x, band.y))
                        + $" {speedPercent,3}%";
                    bottom = Status(zone, guidance, proximity, blink, now);
                    break;
            }
            ControllerService.Instance.Display.Show(top, bottom);
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

        private string Shields() => Shields(flow.ShieldRemaining, flow.ShieldMax);

        private static string Shields(int remaining, int max)
        {
            var shields = new StringBuilder(max);
            for (int i = 0; i < max; i++)
                shields.Append(i < remaining ? LcdGlyph.ShieldUp : LcdGlyph.ShieldDown);
            return shields.ToString();
        }

        // The numbers captured when the run ended (the results screen shows the same ones), so the pages are
        // built once.
        private List<string> ResultPages(in RunResults results)
        {
            var pages = new List<string> { LcdGlyph.Spread("TIME", UiScreens.FormatTime(results.Time)) };
            if (results.Completed && flow.HasPar)
                pages.Add(LcdGlyph.Spread("VS PAR", $"{results.Time - flow.ParTime:+0.0;-0.0} s"));
            pages.Add(LcdGlyph.Spread("TARGETS", $"{results.TargetsDestroyed}/{results.TargetsTotal}"));
            pages.Add(LcdGlyph.Spread("ACCURACY",
                results.ShotsFired == 0 ? "-" : $"{Mathf.RoundToInt(results.Accuracy * 100f)}%"));
            pages.Add(LcdGlyph.Spread("SHIELD", Shields(results.ShieldRemaining, results.ShieldMax)));
            return pages;
        }
    }
}
