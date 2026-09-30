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
            string line = ControllerProtocol.Frame("AX,1023,0,245,1,0") + "\r";
            Assert.That(ControllerProtocol.TryParse(line, out ControllerPacket p), Is.True);
            Assert.That(p.StickX, Is.EqualTo(1023));
            Assert.That(p.StickY, Is.EqualTo(0));
            Assert.That(p.DistanceMm, Is.EqualTo(245));
            Assert.That(p.Fire, Is.True);
            Assert.That(p.StickButton, Is.False);
        }

        [Test]
        public void ParsesMissingEcho()
        {
            Assert.That(ControllerProtocol.TryParse(ControllerProtocol.Frame("AX,512,512,-1,0,0"), out ControllerPacket p), Is.True);
            Assert.That(p.DistanceMm, Is.EqualTo(-1));
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

        [TestCase("AX,2000,512,200,0,0")]           // axis out of range
        [TestCase("AX,512,512,200,2,0")]            // button not 0/1
        [TestCase("AX,512,512,200,0")]              // missing field
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

        [TestCase(60, 300)]
        [TestCase(300, 60)]
        public void ThrottleMapsCalibratedRangeInEitherDirection(int minMm, int maxMm)
        {
            var c = new ControllerCalibration { throttleMinMm = minMm, throttleMaxMm = maxMm };
            Assert.That(c.ThrottleFromDistance(minMm), Is.EqualTo(-1f).Within(0.001f));
            Assert.That(c.ThrottleFromDistance(maxMm), Is.EqualTo(1f).Within(0.001f));
            Assert.That(c.ThrottleFromDistance(180), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SingleSpikeIsRejectedByMedian()
        {
            var c = new ControllerCalibration();
            var f = new ThrottleFilter { SmoothingTime = 0.001f };
            foreach (int mm in new[] { 180, 180, 900, 180 }) f.Update(0.02f, true, mm, c);
            Assert.That(f.FilteredMm, Is.EqualTo(180f).Within(1f));
        }

        [Test]
        public void MissingEchoesHoldThenEaseToCruise()
        {
            var c = new ControllerCalibration();
            var f = new ThrottleFilter { SmoothingTime = 0.001f };
            for (int i = 0; i < 5; i++) f.Update(0.02f, true, 300, c);   // full throttle
            Assert.That(f.Setpoint, Is.EqualTo(1f).Within(0.01f));
            for (int i = 0; i < 20; i++) f.Update(0.02f, true, -1, c);    // 0.4 s of no echo: held
            Assert.That(f.SignalLost, Is.False);
            Assert.That(f.Setpoint, Is.EqualTo(1f).Within(0.01f));
            for (int i = 0; i < 100; i++) f.Update(0.02f, true, -1, c);   // lost: eases to cruise
            Assert.That(f.SignalLost, Is.True);
            Assert.That(f.Setpoint, Is.EqualTo(0f).Within(0.01f));
            f.Update(0.02f, true, 60, c);                                  // recovers immediately
            Assert.That(f.SignalLost, Is.False);
            Assert.That(f.Setpoint, Is.EqualTo(-1f).Within(0.01f));
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

        // ---------- Link (background thread, simulated device) ----------

        [Test]
        public void LinkConnectsReadsRejectsGarbageAndReconnectsAfterUnplug()
        {
            var sim = new SimulatedLineTransport();
            var link = new ControllerLink(() => new ILineTransport[] { sim }) { SilenceSeconds = 0.3f, RetrySeconds = 0.1f };
            try
            {
                link.Start();
                Assert.That(WaitFor(() => link.Snapshot().State == LinkState.Connected), Is.True, "connects");

                sim.Set(1023, 0, 250, true);
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
