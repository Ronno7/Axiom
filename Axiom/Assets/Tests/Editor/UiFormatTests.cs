using GeometricFlight.UI;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class UiFormatTests
    {
        [TestCase(0f, "0:00.0")]
        [TestCase(9.99f, "0:09.9")]
        [TestCase(75.35f, "1:15.3")]
        [TestCase(600f, "10:00.0")]
        [TestCase(-3f, "0:00.0")]
        public void FormatTime(float seconds, string expected)
        {
            Assert.That(UiScreens.FormatTime(seconds), Is.EqualTo(expected));
        }
    }
}
