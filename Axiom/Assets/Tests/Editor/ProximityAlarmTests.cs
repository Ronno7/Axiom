using GeometricFlight.UI;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class ProximityAlarmTests
    {
        [Test]
        public void BeepsQuickenAsImpactNears()
        {
            Assert.That(ProximityAlarm.BeepInterval(ProximityAlarm.Horizon), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ProximityAlarm.BeepInterval(0.6f), Is.LessThan(ProximityAlarm.BeepInterval(1f)));
            Assert.That(ProximityAlarm.BeepInterval(0.2f), Is.EqualTo(0f), "solid tone");
        }
    }
}
