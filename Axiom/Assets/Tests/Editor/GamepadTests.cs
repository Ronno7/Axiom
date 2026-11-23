using GeometricFlight.Core;
using GeometricFlight.Input;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    /// <summary>The Xbox-style gamepad (#60): an absolute trigger throttle that rests at cruise, flown alongside the
    /// keyboard, and prompts that name its buttons.</summary>
    public sealed class GamepadTests
    {
        [Test]
        public void TheTriggersAreAThrottleThatRestsAtCruise()
        {
            Assert.That(GamepadFlightInput.Throttle(0f, 0f), Is.Zero, "released: cruise");
            Assert.That(GamepadFlightInput.Throttle(0.04f, 0.03f), Is.Zero, "a resting finger moves nothing");
            Assert.That(GamepadFlightInput.Throttle(1f, 0f), Is.EqualTo(1f), "the right trigger all the way: the boost gate");
            Assert.That(GamepadFlightInput.Throttle(0f, 1f), Is.EqualTo(-1f), "the left trigger all the way: minimum");
            Assert.That(GamepadFlightInput.Throttle(0.5f, 0f), Is.InRange(0.45f, 0.5f));
            Assert.That(GamepadFlightInput.Throttle(1f, 1f), Is.Zero, "both cancel out");
        }

        [Test]
        public void ThePadFliesAlongsideTheKeyboard()
        {
            var keys = new FlightInput(Vector2.zero, 0.4f, false, false);
            var pad = new FlightInput(new Vector2(0.3f, -0.6f), 0.9f, true, false, 0.5f, lockHeld: true);

            FlightInput merged = FlightInputMerge.Local(keys, pad, padThrottle: true);
            Assert.That(merged.Steering, Is.EqualTo(pad.Steering), "idle keys leave steering to the pad");
            Assert.That(merged.Twist, Is.EqualTo(0.5f));
            Assert.That(merged.Throttle, Is.EqualTo(0.9f), "held triggers own the throttle");
            Assert.That(merged.FireHeld && merged.LockHeld);

            Assert.That(FlightInputMerge.Local(keys, pad, padThrottle: false).Throttle, Is.EqualTo(0.4f),
                "released triggers leave the keyboard's latched setting");

            var steeringKeys = new FlightInput(new Vector2(1f, 0f), 0f, false, false);
            Assert.That(FlightInputMerge.Local(steeringKeys, pad, true).Steering, Is.EqualTo(new Vector2(1f, 0f)),
                "held steering keys win, as they do over the cockpit stick");
        }

        [Test]
        public void ALostPadCancelsAPaintedVolleyInsteadOfLaunchingIt()
        {
            var keys = default(FlightInput);
            var lost = new FlightInput(Vector2.zero, 0f, false, false, lockCancelled: true);
            FlightInput merged = FlightInputMerge.Local(keys, lost, false);
            Assert.That(merged.LockCancelled);
            Assert.That(merged.LockHeld, Is.False);
        }

        [Test]
        public void PromptsNameTheButtonsOfWhateverIsFlying()
        {
            const string line = "{fire} {lower} {raise} {boost} {arm} {lock} {emp}";
            Assert.That(TutorialCaptions.Resolve(line, TutorialCaptions.Controls.Gamepad, false),
                Is.EqualTo("[RB] [LT] [RT] [RT] ALL THE WAY [VIEW] [LB] [Y]"));
            Assert.That(TutorialCaptions.Resolve(line, TutorialCaptions.Controls.Keyboard, false),
                Is.EqualTo("[SPACE] [Q] [E] HOLD [E] [T] [LEFT SHIFT] [R]"));
            Assert.That(TutorialCaptions.Resolve(line, TutorialCaptions.Controls.Cockpit, false),
                Is.EqualTo("[FIRE] THROTTLE BACK THROTTLE FORWARD TO THE FINAL STOP [MASTER ARM] [LOCK] [EMP]"));
            Assert.That(TutorialCaptions.Resolve("{raise}", TutorialCaptions.Controls.Cockpit, true), Is.EqualTo("[E]"),
                "the cockpit with keyboard throttle names the keys for the throttle only");
        }
    }
}
