using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using GeometricFlight.Input;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    public sealed class ControllerTests
    {
        // ---------- Protocol ----------

        [Test]
        public void ParsesValidPacketIncludingCarriageReturn()
        {
            string line = ControllerProtocol.Frame("AX,1023,0,245,1,0,512,0,0,0") + "\r";
            Assert.That(ControllerProtocol.TryParse(line, out ControllerPacket p), Is.True);
            Assert.That(p.StickX, Is.EqualTo(1023));
            Assert.That(p.StickY, Is.EqualTo(0));
            Assert.That(p.ThrottleRaw, Is.EqualTo(245));
            Assert.That(p.Fire, Is.True);
            Assert.That(p.Menu, Is.False);
        }

        [TestCase(0)]
        [TestCase(1023)]
        public void ParsesTheFadersWholeRange(int raw)
        {
            Assert.That(ControllerProtocol.TryParse(ControllerProtocol.Frame($"AX,512,512,{raw},0,0,512,0,0,0"),
                out ControllerPacket p), Is.True);
            Assert.That(p.ThrottleRaw, Is.EqualTo(raw));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ParsesEveryFieldOfThePacket(int held)
        {
            Assert.That(ControllerProtocol.TryParse(ControllerProtocol.Frame($"AX,700,300,180,1,1,1023,1,0,{held}"),
                out ControllerPacket p), Is.True);
            Assert.That(p.StickX, Is.EqualTo(700));
            Assert.That(p.StickY, Is.EqualTo(300));
            Assert.That(p.ThrottleRaw, Is.EqualTo(180));
            Assert.That(p.Fire && p.Menu && p.Emp, Is.True);
            Assert.That(p.Twist, Is.EqualTo(1023));
            Assert.That(p.Armed, Is.False);
            Assert.That(p.LockHeld, Is.EqualTo(held == 1));
        }

        [TestCase("")]
        [TestCase("AX,512,512,200,0,0")]            // no checksum
        [TestCase("AX,512,512,200,0,0*00")]         // wrong checksum
        [TestCase("AX,512,512,20")]                 // truncated mid-line
        [TestCase("garbage \u0001\u0002")]
        public void RejectsMalformedLines(string line)
        {
            Assert.That(ControllerProtocol.TryParse(line, out _), Is.False);
        }

        [TestCase("AX,2000,512,200,0,0,512,0,0,0")] // axis out of range
        [TestCase("AX,512,512,-1,0,0,512,0,0,0")]   // the sonar's "no echo": the fader always reads
        [TestCase("AX,512,512,1024,0,0,512,0,0,0")] // throttle out of range
        [TestCase("AX,512,512,200,2,0,512,0,0,0")]  // button not 0/1
        [TestCase("AX,512,512,200,0")]              // missing field
        [TestCase("AX,512,512,200,0,0")]            // the retired kit packet
        [TestCase("AX,512,512,200,0,0,512,0,0")]    // the older cockpit packet, without LOCK
        [TestCase("AX,512,512,200,0,0,1024,0,0,0")] // twist out of range
        [TestCase("AX,512,512,200,0,0,512,0,2,0")]  // arm not 0/1
        [TestCase("AX,512,512,200,0,0,512,0,0,2")]  // LOCK not 0/1
        [TestCase("AX,512,512,200,0,0,512,0,0,-1")]
        [TestCase("AX,512,512,200,0,0,512,0,0,")]   // truncated LOCK
        [TestCase("AX,512,512,200,0,0,512,0,0,1,0")] // unexpected extra field
        [TestCase("BZ,440,100")]                    // wrong message type
        public void RejectsValidlyFramedButInvalidContent(string body)
        {
            Assert.That(ControllerProtocol.TryParse(ControllerProtocol.Frame(body), out _), Is.False);
        }

        [Test]
        public void ToneCommandIsFramed()
        {
            string command = ControllerProtocol.FormatTone(440, 120);
            Assert.That(command, Does.StartWith("BZ,440,120*"));
            Assert.That(command.Substring(command.Length - 2),
                Is.EqualTo(ControllerProtocol.Checksum("BZ,440,120").ToString("X2")));
        }

        [Test]
        public void RumbleCommandIsFramedAndClamped()
        {
            Assert.That(ControllerProtocol.FormatRumble(120, 0), Is.EqualTo(ControllerProtocol.Frame("RM,120,0")));
            Assert.That(ControllerProtocol.FormatRumble(400, 5000), Is.EqualTo(ControllerProtocol.Frame("RM,255,2000")));
        }

        [Test]
        public void LampCommandIsFramed()
        {
            Assert.That(ControllerProtocol.FormatLamp(ControllerProtocol.Lamp.Blink),
                Is.EqualTo(ControllerProtocol.Frame("LA,2")));
        }

        // ---------- Calibration and filtering ----------

        [Test]
        public void StickCentersWithDeadzoneAndReachesFullDeflection()
        {
            var c = new ControllerCalibration { stickCenterX = 500, stickCenterY = 530 };
            Assert.That(c.NormalizeStick(510, 520), Is.EqualTo(Vector2.zero));
            Assert.That(c.NormalizeStick(1023, 530).x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(c.NormalizeStick(0, 530).x, Is.EqualTo(-1f).Within(0.001f));
            c.invertStickY = true;
            Assert.That(c.NormalizeStick(500, 1023).y, Is.EqualTo(-1f).Within(0.001f));
        }

        [TestCase(40, 990)]
        [TestCase(990, 40)]
        public void ThrottleMapsCalibratedRangeInEitherDirection(int min, int max)
        {
            var c = new ControllerCalibration { throttleMin = min, throttleMax = max };
            Assert.That(c.ThrottleFromRaw(min), Is.EqualTo(-1f).Within(0.001f));
            Assert.That(c.ThrottleFromRaw(max), Is.EqualTo(1f).Within(0.001f));
            Assert.That(c.ThrottleFromRaw(515), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void DefaultCalibrationPutsCruiseInTheMiddleOfTheFader()
        {
            var c = new ControllerCalibration();
            Assert.That(c.ThrottleFromRaw(0), Is.EqualTo(-1f).Within(0.001f));
            Assert.That(c.ThrottleFromRaw(511.5f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(c.ThrottleFromRaw(1023), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SingleSpikeIsRejectedByMedian()
        {
            var c = new ControllerCalibration();
            var f = new ThrottleFilter { SmoothingTime = 0.001f };
            foreach (int raw in new[] { 500, 500, 1000, 500 }) f.Update(0.02f, true, raw, c);
            Assert.That(f.Filtered, Is.EqualTo(500f).Within(1f));
        }

        [Test]
        public void ReadsBothEndsOfTheFader()
        {
            var c = new ControllerCalibration();
            var f = new ThrottleFilter { SmoothingTime = 0.001f };
            for (int i = 0; i < 5; i++) f.Update(0.02f, true, 0, c);
            Assert.That(f.Setpoint, Is.EqualTo(-1f).Within(0.01f), "0 is a reading, not a missing one");
            for (int i = 0; i < 5; i++) f.Update(0.02f, true, 1023, c);
            Assert.That(f.Setpoint, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void PacketGapsHoldThenEaseToCruise()
        {
            var c = new ControllerCalibration();
            var f = new ThrottleFilter { SmoothingTime = 0.001f };
            for (int i = 0; i < 5; i++) f.Update(0.02f, true, 1023, c);  // full throttle
            Assert.That(f.Setpoint, Is.EqualTo(1f).Within(0.01f));
            for (int i = 0; i < 20; i++) f.Update(0.02f, false, 0, c);    // 0.4 s without packets: held
            Assert.That(f.SignalLost, Is.False);
            Assert.That(f.Setpoint, Is.EqualTo(1f).Within(0.01f));
            for (int i = 0; i < 100; i++) f.Update(0.02f, false, 0, c);   // lost: eases to cruise
            Assert.That(f.SignalLost, Is.True);
            Assert.That(f.Setpoint, Is.EqualTo(0f).Within(0.01f));
            f.Update(0.02f, true, 0, c);                                   // recovers immediately
            Assert.That(f.SignalLost, Is.False);
            Assert.That(f.Setpoint, Is.EqualTo(-1f).Within(0.01f));
        }

        // ---------- The cruise tick ----------

        [Test]
        public void CruiseTicksOnceEachTimeTheLeverComesIntoTheNotch()
        {
            const float notch = 0.08f;
            bool armed = false;
            Assert.That(GeometricFlight.UI.CockpitFeel.CruiseClick(ref armed, 0f, notch), Is.False,
                "no tick for a lever that starts at cruise");
            int ticks = 0;
            foreach (float lever in new[] { 0.5f, 0.3f, 0.05f, 0f, -0.04f, 0.06f })
                if (GeometricFlight.UI.CockpitFeel.CruiseClick(ref armed, lever, notch)) ticks++;
            Assert.That(ticks, Is.EqualTo(1), "one tick on the way in, none while it stays");
            foreach (float lever in new[] { -0.6f, -0.07f })
                if (GeometricFlight.UI.CockpitFeel.CruiseClick(ref armed, lever, notch)) ticks++;
            Assert.That(ticks, Is.EqualTo(2), "from the slow side too");
        }

        [Test]
        public void ALeverRestingOnTheNotchEdgeDoesNotChatter()
        {
            const float notch = 0.08f;
            bool armed = false;
            int ticks = 0;
            GeometricFlight.UI.CockpitFeel.CruiseClick(ref armed, 0.4f, notch);
            foreach (float lever in new[] { 0.079f, 0.081f, 0.079f, 0.09f, 0.078f, 0.1f, 0.075f })
                if (GeometricFlight.UI.CockpitFeel.CruiseClick(ref armed, lever, notch)) ticks++;
            Assert.That(ticks, Is.EqualTo(1));
        }

        // ---------- Merging with the keyboard ----------

        [Test]
        public void KeyboardPassesThroughWithoutController()
        {
            var keys = new FlightInput(Vector2.right, 0.5f, true, false);
            FlightInput merged = FlightInputMerge.Merge(keys, default);
            Assert.That(merged.Steering, Is.EqualTo(Vector2.right));
            Assert.That(merged.Throttle, Is.EqualTo(0.5f));
        }

        [Test]
        public void ControllerDrivesUnlessSteeringKeysHeld()
        {
            var controller = new ControllerState(true, Vector2.up, -0.8f, true, false, true);
            FlightInput idleKeys = FlightInputMerge.Merge(new FlightInput(Vector2.zero, 1f, true, false), controller);
            Assert.That(idleKeys.Steering, Is.EqualTo(Vector2.up));
            Assert.That(idleKeys.Throttle, Is.EqualTo(-0.8f).Within(0.0001f));
            Assert.That(idleKeys.FireHeld, Is.True, "fire from either device");
            Assert.That(idleKeys.PausePressed, Is.True, "stick button pauses");
            FlightInput heldKeys = FlightInputMerge.Merge(new FlightInput(Vector2.left, 0f, false, false), controller);
            Assert.That(heldKeys.Steering, Is.EqualTo(Vector2.left));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void KeyboardThrottleWorksWithControllerStickAndButtonsRegardlessOfSensor(bool throttleValid)
        {
            var keys = new FlightInput(Vector2.zero, 0.6f, false, false);
            var controller = new ControllerState(true, Vector2.up, -0.8f, throttleValid, true, true,
                twist: 0.4f, lockHeld: true, empHeld: true);
            FlightInput result = FlightInputMerge.Merge(keys, controller, true);
            Assert.That(result.Throttle, Is.EqualTo(0.6f));
            Assert.That(result.Steering, Is.EqualTo(Vector2.up));
            Assert.That(result.Twist, Is.EqualTo(0.4f));
            Assert.That(result.FireHeld && result.LockHeld && result.EmpHeld && result.PausePressed, Is.True);
        }

        [Test]
        public void SelectedKeyboardThrottleCanHoldCruiseWithoutFallingBackToHardware()
        {
            var controller = new ControllerState(true, Vector2.up, 1f, true, false, false);
            FlightInput result = FlightInputMerge.Merge(default, controller, true);
            Assert.That(result.Throttle, Is.Zero, "Cruise is a valid selected setpoint, not an idle keyboard.");
            Assert.That(result.Steering, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void HardwareThrottleLossKeepsFilteredSetpointInsteadOfSwitchingToStaleKeys()
        {
            var keys = new FlightInput(Vector2.zero, 1f, false, false);
            var controller = new ControllerState(true, Vector2.up, 0.3f, false, false, false);
            FlightInput result = FlightInputMerge.Merge(keys, controller, false);
            Assert.That(result.Throttle, Is.EqualTo(0.3f), "ThrottleFilter owns the existing ease back to cruise.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DisconnectedInputDoesNotDependOnThrottlePreference(bool keyboardThrottle)
        {
            var keys = new FlightInput(Vector2.left, -0.4f, true, true, twist: -0.2f,
                lockHeld: true, empHeld: true);
            FlightInput result = FlightInputMerge.Merge(keys, default, keyboardThrottle);
            Assert.That(result.Throttle, Is.EqualTo(-0.4f));
            Assert.That(result.Steering, Is.EqualTo(Vector2.left));
            Assert.That(result.Twist, Is.EqualTo(-0.2f));
            Assert.That(result.FireHeld && result.LockHeld && result.EmpHeld && result.PausePressed, Is.True);
        }

        // ---------- Link (background thread, simulated device) ----------

        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void LockRemainsHeldUntilBothInputsRelease(bool keyboardHeld, bool controllerHeld)
        {
            var keys = new FlightInput(Vector2.zero, 0f, false, false, lockHeld: keyboardHeld);
            var controller = new ControllerState(true, Vector2.zero, 0f, true, false, false,
                lockHeld: controllerHeld);
            Assert.That(FlightInputMerge.Merge(keys, controller).LockHeld, Is.EqualTo(keyboardHeld || controllerHeld));
        }

        [Test]
        public void LostHardwareLockCancelsEvenWhenKeyboardRemainsHeld()
        {
            var keys = new FlightInput(Vector2.right, 0.5f, true, false, lockHeld: true);
            var lost = new ControllerState(false, Vector2.zero, 0f, false, false, false, lockCancelled: true);
            FlightInput result = FlightInputMerge.Merge(keys, lost);
            Assert.That(result.LockCancelled, Is.True);
            Assert.That(result.LockHeld && result.FireHeld, Is.True);
            Assert.That(result.Steering, Is.EqualTo(Vector2.right));
            Assert.That(result.Throttle, Is.EqualTo(0.5f));
        }

        [Test]
        public void SimulatorEmitsCockpitLockPressAndRelease()
        {
            var simulator = new SimulatedLineTransport();
            simulator.SetLock(true);
            Assert.That(ControllerProtocol.TryParse(simulator.ReadLine(), out ControllerPacket held), Is.True);
            Assert.That(held.LockHeld, Is.True);
            simulator.SetLock(false);
            Assert.That(ControllerProtocol.TryParse(simulator.ReadLine(), out ControllerPacket released), Is.True);
            Assert.That(released.LockHeld, Is.False);
        }

        [Test]
        public void LinkConnectsReadsRejectsGarbageAndReconnectsAfterUnplug()
        {
            var sim = new SimulatedLineTransport();
            var link = new ControllerLink(() => new ILineTransport[] { sim }) { SilenceSeconds = 0.3f, RetrySeconds = 0.1f };
            try
            {
                link.Start();
                Assert.That(WaitFor(() => link.Snapshot().State == LinkState.Connected), Is.True, "connects");

                sim.Set(1023, 0, 700, true);
                Assert.That(WaitFor(() => link.Snapshot().Packet.Fire), Is.True, "reads fresh values");

                long rejected = link.Snapshot().RejectedCount;
                sim.Inject("AX,512,512,2");
                Assert.That(WaitFor(() => link.Snapshot().RejectedCount > rejected), Is.True, "rejects a torn packet");
                Assert.That(link.Snapshot().State, Is.EqualTo(LinkState.Connected), "stays connected");

                link.Send(ControllerProtocol.FormatTone(440, 100));
                Assert.That(WaitFor(() => sim.SentLines().Length > 0), Is.True, "sends commands");

                sim.SetUnplugged(true);
                Assert.That(WaitFor(() => link.Snapshot().State == LinkState.Searching), Is.True, "detects unplug");
                sim.SetUnplugged(false);
                Assert.That(WaitFor(() => link.Snapshot().State == LinkState.Connected), Is.True, "reconnects");
            }
            finally
            {
                link.Dispose();
            }
            Assert.That(link.Snapshot().State, Is.EqualTo(LinkState.Stopped), "closes cleanly");
        }

        [Test]
        public void BusyPortIsReportedAndOtherPortsAreTried()
        {
            var sim = new SimulatedLineTransport();
            var link = new ControllerLink(() => new ILineTransport[] { new BusyTransport(), sim }) { RetrySeconds = 0.1f };
            try
            {
                link.Start();
                Assert.That(WaitFor(() => link.Snapshot().State == LinkState.Connected), Is.True);
                Assert.That(link.Snapshot().PortName, Is.EqualTo("Simulator"));
            }
            finally
            {
                link.Dispose();
            }

            var busyOnly = new ControllerLink(() => new ILineTransport[] { new BusyTransport() }) { RetrySeconds = 0.1f };
            try
            {
                busyOnly.Start();
                Assert.That(WaitFor(() => busyOnly.Snapshot().LastError != null), Is.True);
                Assert.That(busyOnly.Snapshot().LastError, Does.Contain("in use"));
                Assert.That(busyOnly.Snapshot().State, Is.EqualTo(LinkState.Searching));
            }
            finally
            {
                busyOnly.Dispose();
            }
        }

        private static bool WaitFor(System.Func<bool> condition, float seconds = 3f)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                if (condition()) return true;
                Thread.Sleep(10);
            }
            return condition();
        }

        private sealed class BusyTransport : ILineTransport
        {
            public string Name => "COM9";
            public bool Open(out string error) { error = "COM9 is in use by another program"; return false; }
            public string ReadLine() => null;
            public void WriteLine(string line) { }
            public void Dispose() { }
        }
    }
}
