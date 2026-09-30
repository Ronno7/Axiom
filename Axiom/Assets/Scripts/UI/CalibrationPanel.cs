using System;
using GeometricFlight.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.UI
{
    /// <summary>
    /// Binds CalibrationPanel.uxml to a CalibrationWizard. Shows live stick and throttle readings;
    /// captures on the controller's FIRE button or the on-screen button. Edits apply live so the check
    /// step shows real behaviour; Cancel restores the previous values and Save persists them.
    /// Owners call Tick every frame while it is open.
    /// </summary>
    public sealed class CalibrationPanel
    {
        private static readonly string[] Instructions =
        {
            "Let go of the stick so it rests in the middle, then press FIRE.",
            "Slide the throttle to its MINIMUM position, then press FIRE.",
            "Slide the throttle to its MAXIMUM position, then press FIRE.",
            "Check it: the dot should follow the stick and the bar should follow the throttle. "
                + "Use the invert switches if an axis runs backwards, then save.",
        };

        private static readonly string[] LcdSteps =
            { "1/3 CENTER STICK", "2/3 THROTTLE MIN", "3/3 THROTTLE MAX", "CHECK, THEN SAVE" };

        public VisualElement Root { get; }
        public VisualElement FirstFocus => primary;
        public bool IsOpen { get; private set; }

        private readonly Action close;
        private readonly Label status, step, instruction, error, readings;
        private readonly VisualElement stickDot, throttleFill;
        private readonly Toggle invertX, invertY;
        private readonly Button primary;
        private CalibrationWizard wizard;
        private string savedJson;
        private long lastPacket = -1;
        private bool previousFire = true; // Ignore a FIRE already held when the panel opens.

        public CalibrationPanel(VisualElement root, Action close)
        {
            Root = root;
            this.close = close;
            status = root.Q<Label>("calibration-status");
            step = root.Q<Label>("calibration-step");
            instruction = root.Q<Label>("calibration-instruction");
            error = root.Q<Label>("calibration-error");
            readings = root.Q<Label>("calibration-readings");
            stickDot = root.Q("calibration-stick-dot");
            throttleFill = root.Q("calibration-throttle-fill");
            invertX = root.Q<Toggle>("calibration-invert-x");
            invertY = root.Q<Toggle>("calibration-invert-y");
            primary = root.Q<Button>("calibration-primary");

            primary.clicked += Advance;
            root.Q<Button>("calibration-restart").clicked += () => { wizard.Restart(); Refresh(); };
            root.Q<Button>("calibration-cancel").clicked += Cancel;
            invertX.RegisterValueChangedCallback(e => Service.Calibration.invertStickX = e.newValue);
            invertY.RegisterValueChangedCallback(e => Service.Calibration.invertStickY = e.newValue);
            UiScreens.MarkFocusWithin(invertX);
            UiScreens.MarkFocusWithin(invertY);
        }

        private static ControllerService Service => ControllerService.Instance;

        /// <summary>Call when showing the panel.</summary>
        public void Open()
        {
            IsOpen = true;
            savedJson = JsonUtility.ToJson(Service.Calibration);
            wizard = new CalibrationWizard(Service.Calibration);
            previousFire = true;
            invertX.SetValueWithoutNotify(Service.Calibration.invertStickX);
            invertY.SetValueWithoutNotify(Service.Calibration.invertStickY);
            Refresh();
        }

        /// <summary>Restores the values from before the panel opened.</summary>
        public void Cancel()
        {
            if (!IsOpen) return;
            JsonUtility.FromJsonOverwrite(savedJson, Service.Calibration);
            IsOpen = false;
            close();
        }

        public void Tick()
        {
            if (!IsOpen) return;
            LinkSnapshot link = Service.Link;
            bool connected = link.State == LinkState.Connected;
            status.text = connected ? "Controller connected on " + link.PortName
                : "No controller connected. Plug it in; it is detected automatically.";
            primary.SetEnabled(connected || wizard.Current == CalibrationWizard.Step.Check);

            if (connected)
            {
                // Capture on a FIRE press (edge), from fresh packets only.
                if (link.PacketCount != lastPacket)
                {
                    lastPacket = link.PacketCount;
                    bool fire = link.Packet.Fire;
                    if (fire && !previousFire && wizard.Current != CalibrationWizard.Step.Check) Advance();
                    previousFire = fire;
                }
                ControllerPacket p = link.Packet;
                ControllerState s = Service.State;
                float mm = Service.Throttle.FilteredMm;
                readings.text = $"Stick {p.StickX}, {p.StickY}   ·   Distance "
                    + (s.ThrottleValid && !float.IsNaN(mm) ? Mathf.RoundToInt(mm) + " mm" : "no echo")
                    + $"   ·   Fire {(p.Fire ? "pressed" : "released")}";
                stickDot.style.left = Length.Percent((s.Stick.x + 1f) * 50f);
                stickDot.style.top = Length.Percent((1f - s.Stick.y) * 50f);
                throttleFill.style.width = Length.Percent((s.Throttle + 1f) * 50f);
                ShowOnLcd(p, s, mm);
            }
            else readings.text = "Waiting for data…";
        }

        /// <summary>The step on top, the live reading it needs below, so the player can calibrate
        /// looking at the controller. An error blinks the top row.</summary>
        private void ShowOnLcd(ControllerPacket p, ControllerState s, float mm)
        {
            bool blink = Time.unscaledTime % 0.8f < 0.4f;
            string top = wizard.Error != null && blink ? "! SEE THE SCREEN" : LcdSteps[(int)wizard.Current];
            string bottom = wizard.Current switch
            {
                CalibrationWizard.Step.Center => $"X {p.StickX,4}   Y {p.StickY,4}",
                CalibrationWizard.Step.Check => LcdGlyph.Bar((s.Throttle + 1f) * 0.5f, LcdGlyph.Columns),
                _ => s.ThrottleValid && !float.IsNaN(mm) ? $"DISTANCE {Mathf.RoundToInt(mm),4} mm" : "DISTANCE NO ECHO",
            };
            Service.Display.Show(LcdGlyph.Center(top), bottom);
        }

        private void Advance()
        {
            if (wizard.Current == CalibrationWizard.Step.Check)
            {
                Service.SaveCalibration();
                IsOpen = false;
                close();
                return;
            }
            LinkSnapshot link = Service.Link;
            if (link.State != LinkState.Connected) return;
            wizard.Capture(link.Packet, Service.Throttle.FilteredMm, Service.State.ThrottleValid);
            Refresh();
        }

        private void Refresh()
        {
            int index = (int)wizard.Current;
            bool check = wizard.Current == CalibrationWizard.Step.Check;
            step.text = check ? "CHECK" : $"STEP {index + 1} OF 3";
            instruction.text = Instructions[index];
            error.text = wizard.Error ?? "";
            error.style.display = wizard.Error != null ? DisplayStyle.Flex : DisplayStyle.None;
            primary.text = check ? "SAVE" : "CAPTURE";
            invertX.parent.style.display = check ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
