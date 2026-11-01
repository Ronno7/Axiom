using GeometricFlight.Core;
using NUnit.Framework;

namespace GeometricFlight.Tests
{
    public sealed class ArenaWaveTests
    {
        private ArenaWaveState wave;
        [SetUp] public void SetUp() { wave = new ArenaWaveState(6, 3f, 6f); wave.Restart(); }

        [Test] public void EnemiesWaitForTheFullWarning()
        {
            wave.Step(2.9f);
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Incoming));
            Assert.That(wave.ReportDestroyed(0), Is.False);
            wave.Step(0.11f);
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Combat));
            Assert.That(wave.Remaining, Is.EqualTo(6));
        }

        [Test] public void DuplicateAndUnknownTargetsCannotClearWaveEarly()
        {
            wave.Step(3f);
            Assert.That(wave.ReportDestroyed(-1), Is.False);
            Assert.That(wave.ReportDestroyed(6), Is.False);
            Assert.That(wave.ReportDestroyed(0), Is.True);
            Assert.That(wave.ReportDestroyed(0), Is.False);
            Assert.That(wave.Remaining, Is.EqualTo(5));
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Combat));
        }

        [Test] public void EveryTargetMustDieBeforeRecoveryAndNextRound()
        {
            wave.Step(3f);
            for (int i = 0; i < 5; i++) wave.ReportDestroyed(i);
            wave.Step(100f);
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Combat));
            wave.ReportDestroyed(5);
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Cleared));
            Assert.That(wave.Remaining, Is.Zero);
            wave.Step(5.9f);
            Assert.That(wave.Round, Is.EqualTo(1));
            wave.Step(0.11f);
            Assert.That(wave.Round, Is.EqualTo(2));
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Incoming));
            Assert.That(wave.Remaining, Is.EqualTo(6));
        }

        [TestCase(ArenaWavePhase.Incoming)]
        [TestCase(ArenaWavePhase.Combat)]
        [TestCase(ArenaWavePhase.Cleared)]
        public void PauseFreezesEveryTimedPhase(ArenaWavePhase phase)
        {
            if (phase != ArenaWavePhase.Incoming) wave.Step(3f);
            if (phase == ArenaWavePhase.Cleared) for (int i = 0; i < 6; i++) wave.ReportDestroyed(i);
            float time = wave.TimeLeft;
            wave.Step(0f);
            wave.Step(-1f);
            Assert.That(wave.Phase, Is.EqualTo(phase));
            Assert.That(wave.TimeLeft, Is.EqualTo(time));
        }

        [Test] public void DefeatStopsSpawningAndIgnoresLateKills()
        {
            wave.Step(3f);
            wave.ReportDestroyed(0);
            wave.Defeat();
            wave.Step(100f);
            Assert.That(wave.ReportDestroyed(1), Is.False);
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Defeated));
            wave.Restart();
            Assert.That(wave.Round, Is.EqualTo(1));
            Assert.That(wave.Remaining, Is.EqualTo(6));
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Incoming));
            Assert.That(wave.TimeLeft, Is.EqualTo(3f));
        }

        [Test] public void LargeFrameCannotSkipTheNextWarning()
        {
            wave.Step(100f);
            for (int i = 0; i < 6; i++) wave.ReportDestroyed(i);
            wave.Step(100f);
            Assert.That(wave.Phase, Is.EqualTo(ArenaWavePhase.Incoming));
            Assert.That(wave.TimeLeft, Is.EqualTo(3f));
        }
    }
}
