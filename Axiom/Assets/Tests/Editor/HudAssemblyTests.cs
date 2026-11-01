using GeometricFlight.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GeometricFlight.Tests
{
    public sealed class HudAssemblyTests
    {
        [Test]
        public void ReadoutsTypeTheirRealTextBehindACursor()
        {
            const string text = "TARGETS 0/3";
            Assert.That(InstrumentBuild.Reveal(text, 0f), Is.Empty, "nothing shows before typing");
            Assert.That(InstrumentBuild.Reveal(text, 0.5f), Is.EqualTo("TARGE_"));
            Assert.That(InstrumentBuild.Reveal(text, 0.99f), Does.StartWith("TARGETS 0/").And.EndWith("_"));
            Assert.That(InstrumentBuild.Reveal(text, 1f), Is.EqualTo(text));
        }

        [Test]
        public void ABuiltReadoutIsExactlyItsLiveText()
        {
            var parent = new VisualElement();
            var label = new Label("0:00.0");
            parent.Add(label);
            var build = new InstrumentBuild(label, false);
            Assert.That(label.childCount, Is.Zero, "the rule sits beside the label, so the label still sizes to its text");
            build.SetAmount(0.5f);
            Assert.That(label.text, Is.Not.EqualTo("0:00.0"), "still typing");
            build.Text = "KEYBOARD";
            build.SetAmount(1f);
            Assert.That(label.text, Is.EqualTo("KEYBOARD"), "the latest real value, never a stale or counted one");
            Assert.That(build.Rule.style.display.value, Is.EqualTo(DisplayStyle.None), "the rule is gone");
            Assert.That(label.style.minWidth.keyword, Is.EqualTo(StyleKeyword.Null), "no width is held afterwards");
        }
    }
}
