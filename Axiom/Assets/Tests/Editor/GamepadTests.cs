using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    /// <summary>The Xbox-style gamepad (#60): triggers that move the latched throttle the keyboard keeps, flown
    /// alongside the keyboard, free look on the right stick, and prompts that name its buttons.</summary>
    public sealed class GamepadTests
    {
        [Test]
        public void TheTriggersRaiseAndLowerTheThrottle()
        {
            Assert.That(GamepadFlightInput.Change(0f, 0f), Is.Zero, "released: no change");
            Assert.That(GamepadFlightInput.Change(0.04f, 0.03f), Is.Zero, "a resting finger moves nothing");
            Assert.That(GamepadFlightInput.Change(1f, 0f), Is.EqualTo(1f), "the right trigger all the way: up at Q/E's rate");
            Assert.That(GamepadFlightInput.Change(0f, 1f), Is.EqualTo(-1f), "the left trigger all the way: down");
            Assert.That(GamepadFlightInput.Change(0.5f, 0f), Is.InRange(0.45f, 0.5f), "half a squeeze: half the rate");
            Assert.That(GamepadFlightInput.Change(1f, 1f), Is.Zero, "both cancel out");
        }

        [Test]
        public void TheThrottleStaysWhereTheTriggersLeaveIt()
        {
            float setting = 0f;
            for (int i = 0; i < 10; i++) setting = KeyboardFlightInput.Latch(setting, 1f, 1f, 0.05f);
            Assert.That(setting, Is.EqualTo(0.5f).Within(1e-4f), "half a second of RT at Q/E's rate of 1 per second");
            for (int i = 0; i < 40; i++) setting = KeyboardFlightInput.Latch(setting, 0f, 1f, 0.05f);
            Assert.That(setting, Is.EqualTo(0.5f).Within(1e-4f), "released, it holds: the fingers are free for the bumpers");
            for (int i = 0; i < 40; i++) setting = KeyboardFlightInput.Latch(setting, 1f, 1f, 0.05f);
            Assert.That(setting, Is.EqualTo(1f), "held at full it stays in the boost gate");
            Assert.That(KeyboardFlightInput.Latch(0.2f, 2f, 1f, 0.1f), Is.EqualTo(0.3f).Within(1e-4f),
                "keys and triggers together never move it faster than one of them");
        }

        [Test]
        public void ThePadFliesAlongsideTheKeyboard()
        {
            var keys = new FlightInput(Vector2.zero, 0.4f, false, false);
            var pad = new FlightInput(new Vector2(0.3f, -0.6f), 0f, true, false, 0.5f, lockHeld: true);

            FlightInput merged = FlightInputMerge.Local(keys, pad);
            Assert.That(merged.Steering, Is.EqualTo(pad.Steering), "idle keys leave steering to the pad");
            Assert.That(merged.Twist, Is.EqualTo(0.5f));
            Assert.That(merged.Throttle, Is.EqualTo(0.4f), "one latched setting, which the triggers move through the keys'");
            Assert.That(merged.FireHeld && merged.LockHeld);

            var steeringKeys = new FlightInput(new Vector2(1f, 0f), 0f, false, false);
            Assert.That(FlightInputMerge.Local(steeringKeys, pad).Steering, Is.EqualTo(new Vector2(1f, 0f)),
                "held steering keys win, as they do over the cockpit stick");
        }

        [Test]
        public void ALostPadCancelsAPaintedVolleyInsteadOfLaunchingIt()
        {
            var keys = default(FlightInput);
            var lost = new FlightInput(Vector2.zero, 0f, false, false, lockCancelled: true);
            FlightInput merged = FlightInputMerge.Local(keys, lost);
            Assert.That(merged.LockCancelled);
            Assert.That(merged.LockHeld, Is.False);
        }

        [Test]
        public void TheRightStickLooksAroundAndNeverSteers()
        {
            var keys = new FlightInput(new Vector2(1f, 0f), 0f, false, false);
            var pad = new FlightInput(Vector2.zero, 0f, false, false, look: new Vector2(-0.8f, 0.3f));
            FlightInput local = FlightInputMerge.Local(keys, pad);
            Assert.That(local.Look, Is.EqualTo(pad.Look), "the pad's look rides along with the keys' steering");
            Assert.That(local.Steering, Is.EqualTo(keys.Steering), "and steers nothing");
            Assert.That(FlightInputMerge.Merge(local, default).Look, Is.EqualTo(pad.Look), "no cockpit");
            var cockpit = new ControllerState(true, Vector2.up, 0f, false, false, false);
            Assert.That(FlightInputMerge.Merge(local, cockpit).Look, Is.EqualTo(pad.Look), "the cockpit has no look of its own");
            Assert.That(new WeaponAuthority().Filter(local, false, true).Look, Is.EqualTo(pad.Look),
                "safe weapons don't stop it");
        }

        [Test]
        public void FreeLookTurnsTheViewAboutTheShipWhicheverWayUpItIs()
        {
            Quaternion ship = Quaternion.Euler(0f, 30f, 180f); // inverted, heading 30°
            Vector3 Viewed(Vector2 angles) => ChaseCamera.Orbit(ship, angles) * (ship * Vector3.forward);
            Assert.That(Vector3.Angle(Viewed(Vector2.zero), ship * Vector3.forward), Is.LessThan(0.01f), "centred: the nose");
            Assert.That(Vector3.Angle(Viewed(new Vector2(90f, 0f)), ship * Vector3.right), Is.LessThan(0.01f),
                "90° right: out past the right wing");
            Assert.That(Vector3.Angle(Viewed(new Vector2(180f, 0f)), ship * Vector3.back), Is.LessThan(0.01f), "behind");
            Assert.That(Vector3.Dot(Viewed(new Vector2(0f, 45f)), ship * Vector3.up), Is.EqualTo(0.7071f).Within(1e-3f),
                "up is the canopy's up, not the world's");
        }

        [Test]
        public void PromptsNameTheButtonsOfWhateverIsFlying()
        {
            const string line = "{fire} {lower} {raise} {boost} {arm} {lock} {emp}";
            Assert.That(TutorialCaptions.Resolve(line, TutorialCaptions.Controls.Gamepad, false),
                Is.EqualTo("[A] [LT] [RT] HOLD [RT] [VIEW] [B] [Y]"));
            Assert.That(TutorialCaptions.Resolve(line, TutorialCaptions.Controls.Keyboard, false),
                Is.EqualTo("[SPACE] [Q] [E] HOLD [E] [T] [LEFT SHIFT] [R]"));
            Assert.That(TutorialCaptions.Resolve(line, TutorialCaptions.Controls.Cockpit, false),
                Is.EqualTo("[FIRE] THROTTLE BACK THROTTLE FORWARD TO THE FINAL STOP [MASTER ARM] [LOCK] [EMP]"));
            Assert.That(TutorialCaptions.Resolve("{raise}", TutorialCaptions.Controls.Cockpit, true), Is.EqualTo("[E]"),
                "the cockpit with keyboard throttle names the keys for the throttle only");
        }
    }
}
