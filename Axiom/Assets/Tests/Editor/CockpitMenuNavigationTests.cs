using GeometricFlight.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeometricFlight.Tests
{
    public sealed class CockpitMenuNavigationTests
    {
        private const NavigationMoveEvent.Direction None = NavigationMoveEvent.Direction.None;

        [Test]
        public void StickPushesBecomeArrowKeyMoves()
        {
            Assert.That(CockpitMenuNavigation.DirectionOf(new Vector2(0f, 0.9f), None), Is.EqualTo(NavigationMoveEvent.Direction.Up));
            Assert.That(CockpitMenuNavigation.DirectionOf(new Vector2(0f, -0.9f), None), Is.EqualTo(NavigationMoveEvent.Direction.Down));
            Assert.That(CockpitMenuNavigation.DirectionOf(new Vector2(0.9f, 0.2f), None), Is.EqualTo(NavigationMoveEvent.Direction.Right));
            Assert.That(CockpitMenuNavigation.DirectionOf(new Vector2(-0.9f, -0.2f), None), Is.EqualTo(NavigationMoveEvent.Direction.Left));
            Assert.That(CockpitMenuNavigation.DirectionOf(new Vector2(0.1f, 0.4f), None), Is.EqualTo(None), "a nudge is not a push");
        }

        [Test]
        public void AHeldPushLastsUntilTheStickComesBack()
        {
            var up = NavigationMoveEvent.Direction.Up;
            Assert.That(CockpitMenuNavigation.DirectionOf(new Vector2(0f, 0.45f), up), Is.EqualTo(up), "a wobble near the edge keeps holding");
            Assert.That(CockpitMenuNavigation.DirectionOf(new Vector2(0f, 0.2f), up), Is.EqualTo(None));
        }
    }
}
