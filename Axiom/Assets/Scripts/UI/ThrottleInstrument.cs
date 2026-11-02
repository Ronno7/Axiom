using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>The throttle gauge: the lever's travel in eight segments like the cockpit's lights, the cruise notch at the
    /// centre, actual speed as a quieter fill, the boost stretch and its reserve, and a throttle zone's band and hint.
    /// Reads the flight; owns no rules.</summary>
    public sealed class ThrottleInstrument
    {
        private readonly ShipFlight flight;
        private readonly ThrottleZone[] zones;
        private readonly VisualElement track, readouts, fill, setpoint, cruise, band, boostZone, reserve, reserveFill;
        private readonly Label speedLabel, caption, hintLabel, reserveState;
        private string shownSpeedWord, shownHint;
        private int shownSpeed = -1;
        private BoostStatus shownBoost = (BoostStatus)(-1);
        private float build = 1f;

        public ThrottleInstrument(VisualElement root, ShipFlight flight)
        {
            this.flight = flight;
            Root = root.Q("throttle");
            track = Root.Q(className: "throttle-track");
            readouts = Root.Q(className: "throttle-readouts");
            track.style.transformOrigin = new TransformOrigin(0f, Length.Percent(50f));
            speedLabel = root.Q<Label>("throttle-speed");
            caption = root.Q<Label>("throttle-caption");
            hintLabel = root.Q<Label>("throttle-hint");
            band = root.Q("throttle-band");
            fill = root.Q("throttle-fill");
            setpoint = root.Q("throttle-setpoint");
            cruise = root.Q("throttle-cruise");
            boostZone = root.Q("throttle-boost-zone");
            reserve = root.Q("throttle-reserve");
            reserveFill = root.Q("throttle-reserve-fill");
            reserveState = root.Q<Label>("throttle-reserve-state");
            zones = Object.FindObjectsByType<ThrottleZone>(FindObjectsSortMode.None);
            BuildTrack();
            ShowZone(null);
            SetBuild(1f);
        }

        public VisualElement Root { get; }
        public ThrottleZone Zone { get; private set; }
        /// <summary>The zone's recommended band as fractions of the gauge.</summary>
        public Vector2 Band { get; private set; }
        /// <summary>Which way the zone wants the throttle: +1 raise, -1 lower, 0 hold.</summary>
        public int Guidance { get; private set; }
        public bool HasReading => shownSpeed >= 0;
        public bool Built => build >= 1f;

        public void Tick()
        {
            fill.style.width = Length.Percent(SpeedFraction(flight.CurrentSpeed) * 100f);
            setpoint.style.left = Length.Percent(ThrottleGauge.LeverFraction(flight.Throttle) * 100f);
            int speed = Mathf.RoundToInt(flight.CurrentSpeed);
            string word = ThrottleGauge.Word(flight);
            if (speed != shownSpeed || word != shownSpeedWord)
            {
                shownSpeed = speed;
                shownSpeedWord = word;
                speedLabel.text = word.Length > 0 ? $"SPEED {speed} M/S · {word}" : $"SPEED {speed} M/S";
            }
            UpdateBoostReserve();
            UpdateGuidance();
        }

        /// <summary>Draws the gauge in, 0–1: the caption types, the track extends, the marks register, then live values
        /// fill in. Inline styles only while partial, so class-driven looks resume afterward.</summary>
        public void SetBuild(float amount)
        {
            build = Mathf.Clamp01(amount);
            caption.text = InstrumentBuild.Reveal(CaptionFor(Zone), Mathf.InverseLerp(0f, 0.3f, build));
            track.style.scale = build >= 1f ? new StyleScale(StyleKeyword.Null)
                : new Scale(new Vector3(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, build)), 1f, 1f));
            float marks = build >= 0.55f ? 1f : 0f;
            cruise.style.opacity = setpoint.style.opacity = InstrumentBuild.Partial(build >= 1f ? 1f : marks);
            fill.style.opacity = InstrumentBuild.Partial(Mathf.InverseLerp(0.6f, 0.85f, build));
            readouts.style.opacity = reserve.style.opacity = InstrumentBuild.Partial(Mathf.InverseLerp(0.75f, 1f, build));
        }

        /// <summary>Inside a ThrottleZone the gauge shows the recommended band and a hint.</summary>
        private void UpdateGuidance()
        {
            ThrottleZone zone = null;
            float z = flight.transform.position.z;
            foreach (ThrottleZone candidate in zones)
                if (candidate.Contains(z)) { zone = candidate; break; }
            if (zone != Zone) ShowZone(zone);
            Guidance = 0;
            if (zone == null) return;

            Vector2 speeds = zone.RecommendedCruiseMultiple * flight.CruiseSpeed;
            Guidance = flight.TargetSpeed < speeds.x - 0.01f ? 1 : flight.TargetSpeed > speeds.y + 0.01f ? -1 : 0;
            bool keys = !ControllerService.Instance.State.Connected || GameSettings.KeyboardThrottleWithController;
            string hint = Guidance > 0 ? (keys ? "RAISE  [E]" : "RAISE")
                : Guidance < 0 ? (keys ? "LOWER  [Q]" : "LOWER") : "HOLD";
            if (hint == shownHint) return;
            shownHint = hint;
            hintLabel.text = hint;
            Root.EnableInClassList("throttle--in-band", hint == "HOLD");
        }

        private void ShowZone(ThrottleZone zone)
        {
            Zone = zone;
            shownHint = null;
            bool guided = zone != null;
            Root.EnableInClassList("throttle--guided", guided);
            band.style.display = guided ? DisplayStyle.Flex : DisplayStyle.None;
            hintLabel.style.display = guided ? DisplayStyle.Flex : DisplayStyle.None;
            if (build >= 1f) caption.text = CaptionFor(zone);
            if (!guided) return;
            float from = SpeedFraction(zone.RecommendedCruiseMultiple.x * flight.CruiseSpeed);
            float to = SpeedFraction(zone.RecommendedCruiseMultiple.y * flight.CruiseSpeed);
            Band = new Vector2(from, to);
            band.style.left = Length.Percent(from * 100f);
            band.style.width = Length.Percent((to - from) * 100f);
        }

        private static string CaptionFor(ThrottleZone zone) => zone == null ? "THROTTLE"
            : zone.Type == ThrottleZone.Kind.Slow ? "THROTTLE · SLOW ZONE" : "THROTTLE · FAST ZONE";

        private float SpeedFraction(float speed) => ThrottleGauge.SpeedFraction(flight, speed);

        private void BuildTrack()
        {
            float gate = ThrottleGauge.LeverFraction(flight.BoostGate);
            bool boost = flight.Boost != BoostStatus.Off;
            for (int k = 1; k < ThrottleGauge.Segments; k++)
            {
                float at = k / (float)ThrottleGauge.Segments;
                if (Mathf.Approximately(at, 0.5f)) continue; // The cruise notch.
                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.AddToClassList("throttle-tick");
                tick.EnableInClassList("throttle-tick--gate", boost && Mathf.Approximately(at, gate));
                tick.style.left = Length.Percent(at * 100f);
                track.Insert(track.IndexOf(fill) + 1, tick); // Over the fill.
            }
            cruise.style.left = Length.Percent(50f);
            boostZone.style.display = boost ? DisplayStyle.Flex : DisplayStyle.None;
            boostZone.style.left = Length.Percent(gate * 100f);
            reserve.style.display = boost ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateBoostReserve()
        {
            BoostStatus status = flight.Boost;
            if (status == BoostStatus.Off) return;
            reserveFill.style.width = Length.Percent(flight.BoostCharge * 100f);
            if (status == shownBoost) return;
            shownBoost = status;
            reserveState.text = ThrottleGauge.ReserveText(status);
            Root.EnableInClassList("throttle--boosting", status == BoostStatus.Active);
            Root.EnableInClassList("throttle--boost-ready", status == BoostStatus.Ready);
        }
    }
}
