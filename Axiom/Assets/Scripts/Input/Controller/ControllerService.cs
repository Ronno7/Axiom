using System;
using GeometricFlight.Core;
using UnityEngine;

namespace GeometricFlight.Input
{
    /// <summary>The cockpit's calibrated state for the current frame.</summary>
    public readonly struct ControllerState
    {
        public readonly bool Connected;
        public readonly Vector2 Stick;
        /// <summary>Throttle setpoint, -1 (minimum) to +1 (maximum). Eases to 0 if the sensor is lost.</summary>
        public readonly float Throttle;
        public readonly bool ThrottleValid;
        public readonly bool Fire;
        /// <summary>MENU press edge (used as pause).</summary>
        public readonly bool MenuPressed;
        /// <summary>Stick twist, -1 to +1.</summary>
        public readonly float Twist;
        public readonly bool LockHeld, EmpHeld;
        /// <summary>The MASTER ARM switch's physical position.</summary>
        public readonly bool Armed;
        /// <summary>One frame when a held hardware LOCK is lost with the link or packet capability.</summary>
        public readonly bool LockCancelled;

        public ControllerState(bool connected, Vector2 stick, float throttle, bool throttleValid, bool fire,
            bool menuPressed, float twist = 0f, bool lockHeld = false, bool lockCancelled = false,
            bool empHeld = false, bool armed = false)
        {
            Twist = twist;
            Connected = connected;
            Stick = stick;
            Throttle = throttle;
            ThrottleValid = throttleValid;
            Fire = fire;
            MenuPressed = menuPressed;
            LockHeld = lockHeld;
            EmpHeld = empHeld;
            LockCancelled = lockCancelled;
            Armed = armed;
        }
    }

    /// <summary>
    /// One controller connection for the whole session: the serial port must outlive scene loads,
    /// and menus and levels share it, so this is the project's single persistent service.
    /// Created on first use. Converts raw packets to a calibrated ControllerState each frame on
    /// the main thread and raises Connected/Disconnected there.
    /// </summary>
    [DefaultExecutionOrder(-50)] // Before the flight reads input.
    [DisallowMultipleComponent]
    public sealed class ControllerService : MonoBehaviour
    {
        private const string PortKey = GameSettings.ControllerPortKey;
        private const string CalibrationKey = "axiom.controllerCalibration";
        private static ControllerService instance;

        public static ControllerService Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("ControllerService");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<ControllerService>();
                }
                return instance;
            }
        }

        /// <summary>The service if it exists; never creates one (safe during teardown).</summary>
        public static ControllerService Current => instance;

        public event Action Connected;
        public event Action Disconnected;
        public ControllerState State { get; private set; }
        public LinkSnapshot Link { get; private set; }
        public ControllerCalibration Calibration { get; private set; }
        public ThrottleFilter Throttle { get; } = new ThrottleFilter();
        /// <summary>The controller's LCD. Screens call Display.Show every frame while they are active.</summary>
        public ControllerDisplay Display { get; } = new ControllerDisplay();
        /// <summary>Non-null while the simulated controller replaces serial ports.</summary>
        public SimulatedLineTransport Simulator { get; private set; }

        private ControllerLink link;
        private long lastPacketCount = -1;
        private bool wasConnected;
        private bool wasThrottleLost;
        private bool previousMenu;
        private ControllerProtocol.Lamp? empLamp;
        private bool empNeedsRelease = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            Calibration = LoadCalibration();
            StartLink(null);
        }

        private void OnDestroy()
        {
            link?.Dispose();
            if (instance == this) instance = null;
        }

        private void OnApplicationQuit() => link?.Dispose();

        /// <summary>Swaps serial scanning for an in-memory simulated controller, or back (false).</summary>
        public void UseSimulator(bool enabled) => StartLink(enabled ? new SimulatedLineTransport() : null);

        /// <summary>Applies the Options setting: starts the serial search or closes the port.</summary>
        public void RefreshLink()
        {
            if (Simulator == null) StartLink(null);
        }

        public void SaveCalibration()
        {
            PlayerPrefs.SetString(CalibrationKey, JsonUtility.ToJson(Calibration));
            PlayerPrefs.Save();
        }

        /// <summary>Buzzer tone, if a controller is connected and the buzzer is enabled in Options.</summary>
        public void PlayTone(int hz, int milliseconds)
        {
            if (GameSettings.BuzzerEnabled && link != null) link.Send(ControllerProtocol.FormatTone(hz, milliseconds));
        }

        public void SetEmpLamp(ControllerProtocol.Lamp mode)
        {
            if (!State.Connected || empLamp == mode) return;
            link?.Send(ControllerProtocol.FormatLamp(mode));
            empLamp = mode;
        }

        public void PulseRumble(int level, int milliseconds) => link?.Send(ControllerProtocol.FormatRumble(level, milliseconds));

        private void StartLink(SimulatedLineTransport simulator)
        {
            link?.Dispose();
            link = null;
            Simulator = simulator;
            Throttle.Reset();
            lastPacketCount = -1;
            string preferredPort = PlayerPrefs.GetString(PortKey, "");
            if (simulator != null) link = new ControllerLink(() => new ILineTransport[] { simulator });
            else if (GameSettings.CockpitEnabled) link = new ControllerLink(() => ControllerLink.SerialCandidates(preferredPort));
            link?.Start();
        }

        private void Update()
        {
            LinkSnapshot snapshot = link != null ? link.Snapshot() : default;
            Link = snapshot;
            bool connected = snapshot.State == LinkState.Connected;
            bool fresh = connected && snapshot.PacketCount != lastPacketCount;
            lastPacketCount = snapshot.PacketCount;

            // Unscaled: calibration and menus run while gameplay time is paused.
            Throttle.Update(Time.unscaledDeltaTime, fresh, fresh ? snapshot.Packet.DistanceMm : -1, Calibration);

            ControllerPacket p = snapshot.Packet;
            if (!connected || !wasConnected) empNeedsRelease = true;
            if (connected && !p.Emp) empNeedsRelease = false;
            bool menu = connected && p.Menu;
            bool lockCancelled = State.LockHeld && !connected;
            State = new ControllerState(connected,
                connected ? Calibration.NormalizeStick(p.StickX, p.StickY) : Vector2.zero,
                Throttle.Setpoint, connected && !Throttle.SignalLost, connected && p.Fire,
                menu && !previousMenu,
                connected ? Calibration.NormalizeTwist(p.Twist) : 0f,
                connected && p.LockHeld, lockCancelled,
                connected && p.Emp && !empNeedsRelease,
                connected && p.Armed);
            previousMenu = menu;

            if (connected != wasConnected)
            {
                wasConnected = connected;
                empLamp = null;
                if (connected)
                {
                    if (Simulator == null) PlayerPrefs.SetString(PortKey, snapshot.PortName);
                    Display.Invalidate();
                    Display.Flash(LcdGlyph.Center("LINK ESTABLISHED"), LcdGlyph.Center(snapshot.PortName),
                        Time.unscaledTimeAsDouble + 1.5);
                    Connected?.Invoke();
                }
                else Disconnected?.Invoke();
            }

            bool throttleLost = connected && Throttle.SignalLost;
            if (throttleLost && !wasThrottleLost && !GameSettings.KeyboardThrottleWithController) PlayTone(330, 250);
            wasThrottleLost = throttleLost;
        }

        // LateUpdate, so every screen has called Display.Show for this frame.
        private void LateUpdate()
        {
            if (!State.Connected) return;
            string row = Display.Next(Time.unscaledTimeAsDouble);
            if (row != null) link.Send(row);
        }

        private static ControllerCalibration LoadCalibration()
        {
            string json = PlayerPrefs.GetString(CalibrationKey, "");
            if (string.IsNullOrEmpty(json)) return new ControllerCalibration();
            try { return JsonUtility.FromJson<ControllerCalibration>(json) ?? new ControllerCalibration(); }
            catch (ArgumentException) { return new ControllerCalibration(); }
        }
    }
}
