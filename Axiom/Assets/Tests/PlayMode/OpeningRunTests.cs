using System.Collections;
using System.Collections.Generic;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace GeometricFlight.Tests
{
    /// <summary>Plays the real opening in each display mode, each test from the title screen. Pictures of key moments go
    /// to Temp/Captures/OpeningRun for a visual check.</summary>
    [TestFixture(Display.Normal)]
    [TestFixture(Display.Crt)]
    [Timeout(300000)]
    public sealed class OpeningRunTests
    {
        public enum Display { Normal, Crt }

        /// <summary>Just short of the rail's end, where the ship is released into the arena.</summary>
        private const float NearTheRailEnd = 5900f;
        private const string WaveArrivalTone = "BZ,440,180", WaveClearedTone = "BZ,1320,220";
        private const string ArmedTone = "BZ,880,160", RefusedTone = "BZ,180,140";

        private readonly Display display;
        private OpeningDriver game;

        public OpeningRunTests(Display display) => this.display = display;

        [UnitySetUp]
        public IEnumerator LoadTheOpening()
        {
            game = new OpeningDriver(display.ToString());
            yield return game.Load(display == Display.Crt);
        }

        [UnityTearDown]
        public IEnumerator RestoreTheSettings()
        {
            game.Close();
            yield return null;
            Assert.That(game.Warnings, Is.Empty, "the console stays free of warnings");
        }

        [UnityTest]
        public IEnumerator PlayStartsAnEntryThatPausesAndHandsOverControl()
        {
            game.ClickMenu("play-button");
            yield return game.Until("the entry to reach 3 s", () => game.Entry.Elapsed > 3f, 8f);
            game.Flow.Pause();
            yield return game.Wait(0.4f);
            Assert.That(game.HudShows("pause-panel"), "the pause panel shows");
            Assert.That(Time.timeScale, Is.Zero, "time stops");
            Assert.That(game.HudShows("weapons-panel"), Is.False, "the weapons panel stands down under the menu");
            yield return game.Capture("entry-paused");
            float frozen = game.Entry.Elapsed;
            yield return game.Wait(1f);
            Assert.That(game.Entry.Elapsed, Is.EqualTo(frozen), "the entry clock stops");

            game.Flow.Resume();
            yield return game.Until("control to arrive", () => !game.Flow.EntryHeld, 15f);
            yield return game.Wait(0.3f);
            Assert.That(game.Ui.EntryReady, "the HUD is ready at control");
            Assert.That(game.HudShows("weapons-panel"), "the weapons panel is on");
            Assert.That(game.MenuShows("title-panel"), Is.False, "the title is gone");
            Assert.That(game.Flow.State, Is.EqualTo(RunState.Playing));
            Assert.That(game.Flow.ElapsedTime, Is.GreaterThan(0f), "run time counts");
            yield return game.Capture("control");
        }

        [UnityTest]
        public IEnumerator TheReducedEntryReachesTheSameReadyHud()
        {
            yield return game.ReachControl();
            Assert.That(game.Ui.EntryReady, "the HUD is ready at control");
            Assert.That(game.HudShows("weapons-panel"), "the weapons panel is on");
            Assert.That(game.MenuShows("title-panel"), Is.False, "the title is gone");
            yield return game.Capture("reduced-control");
        }

        [UnityTest]
        public IEnumerator FiringWhileSafeExplainsItselfAndFiresNothing()
        {
            yield return game.ReachControl();
            Assert.That(game.Authority.Armed, Is.False, "the run starts SAFE");
            int refusals = game.Tones(RefusedTone);
            game.Hands(fire: true);
            yield return game.Wait(0.3f);
            Assert.That(game.Header, Does.Contain("SAFE").Or.Contain("ARM"));
            Assert.That(game.Gun.ShotsFired, Is.Zero);
            Assert.That(game.Tones(RefusedTone), Is.GreaterThan(refusals), "a refused press sounds on the cockpit");
            yield return game.Capture("safe-refused");
        }

        [UnityTest]
        public IEnumerator ACheckpointIsQuietAndAHitSoundsTheTone()
        {
            yield return game.ReachControl();
            int tones = game.HitTones;
            game.Jump(game.FirstCheckpointZ + 5f);
            yield return game.Wait(0.6f);
            Assert.That(game.Course.CheckpointsReached, Is.GreaterThanOrEqualTo(1), "the checkpoint registers");
            Assert.That(game.HitTones, Is.EqualTo(tones), "a checkpoint isn't a hit");
            yield return game.Capture("checkpoint");

            game.Shield.ApplyHit();
            Assert.That(game.Shield.State.Current, Is.EqualTo(2), "one hit costs one segment");
            yield return game.Wait(0.1f);
            Assert.That(game.HitTones, Is.GreaterThan(tones), "the hit tone sounds");
            yield return game.Capture("hit");
        }

        [UnityTest]
        public IEnumerator DestroyedOnTheRailRebuildsAtTheCheckpoint()
        {
            yield return game.ReachControl();
            game.Jump(game.FirstCheckpointZ + 5f);
            yield return game.Wait(0.6f);
            float retryZ = game.Course.RetryZ;
            game.Shield.ApplyHit();
            yield return game.Wait(1.4f);
            game.Shield.ApplyHit();
            yield return game.Wait(1.4f);
            game.Shield.ApplyHit();
            Assert.That(game.Course.Rebuilding, "the third hit starts a rebuild");
            Assert.That(game.Flow.State, Is.EqualTo(RunState.Playing), "the run goes on");
            yield return game.Wait(0.3f);
            Assert.That(game.Flight.enabled, Is.False, "the ship is gone while destroyed");
            Assert.That(game.Header, Does.Contain("SIGNAL"));
            yield return game.Capture("signal-lost");

            game.Flow.Pause();
            yield return game.Wait(2.5f);
            Assert.That(game.Course.Rebuilding, "the rebuild waits during a pause");
            game.Flow.Resume();
            yield return game.Rebuilt();
            yield return game.Wait(0.2f);
            Assert.That(game.Shield.State.Current, Is.EqualTo(3), "the shield is full");
            Assert.That(game.Flight.transform.position.z, Is.EqualTo(retryZ).Within(30f), "rebuilt at the checkpoint");
            Assert.That(game.Flight.InputLocked, Is.False, "input is back");
            Assert.That(game.Header, Does.Contain("REBUILT"));
            yield return game.Capture("rebuilt");

            game.Shield.Destroy();
            Assert.That(game.Course.Rebuilding, "a lethal hit starts a rebuild");
            yield return game.Rebuilt();
        }

        [UnityTest]
        public IEnumerator FlyingThroughARingScoresItOnceAndARetryForgetsIt()
        {
            yield return game.ReachControl();
            Assert.That(game.HudText("hud-rings"), Is.EqualTo("RINGS 0/17"));
            Transform ring = GameObject.Find("Tutorial hoop 01").transform;
            yield return FlyThrough(ring);
            Assert.That(game.HudText("hud-rings"), Is.EqualTo("RINGS 1/17"));
            yield return game.Capture("ring-scored");
            yield return FlyThrough(ring);
            Assert.That(game.HudText("hud-rings"), Is.EqualTo("RINGS 1/17"), "a ring scores once");

            game.Shield.Destroy();
            yield return game.Rebuilt();
            yield return game.Wait(0.2f);
            Assert.That(game.HudText("hud-rings"), Is.EqualTo("RINGS 0/17"), "a retry forgets rings since the checkpoint");
        }

        private IEnumerator FlyThrough(Transform ring)
        {
            game.Flight.Respawn(ring.position + Vector3.back * 6f);
            yield return game.Until("the ship to pass the ring", () => game.Flight.transform.position.z > ring.position.z + 1f, 3f);
            yield return game.Wait(0.2f);
        }

        [UnityTest]
        public IEnumerator UnpluggingPausesAndTheArmingSurvivesTheReconnect()
        {
            yield return game.ReachControl();
            int arming = game.Tones(ArmedTone);
            game.Cockpit.SetArmed(true);
            yield return game.Wait(0.3f);
            Assert.That(game.Authority.Armed, "the switch arms");
            Assert.That(game.Tones(ArmedTone), Is.GreaterThan(arming), "arming answers on the cockpit");

            game.Cockpit.SetUnplugged(true);
            yield return game.Until("unplugging to pause", () => game.Flow.State == RunState.Paused, 4f);
            yield return game.Wait(0.3f);
            Assert.That(game.Flow.PauseReason, Is.EqualTo(PauseReason.ControllerLost));
            Assert.That(game.HudText("pause-notice"), Does.Contain("disconnected"));
            Assert.That(game.Authority.Armed, "arming survives on the keyboard");
            yield return game.Capture("disconnected");

            game.Cockpit.SetUnplugged(false);
            yield return game.Until("replugging to reconnect", () => ControllerService.Instance.State.Connected, 8f);
            yield return game.Wait(0.3f);
            Assert.That(game.Flow.State, Is.EqualTo(RunState.Paused), "it waits for the player");
            Assert.That(game.HudText("pause-notice"), Does.Contain("reconnected"));
            yield return game.Capture("reconnected");

            game.Flow.Resume();
            yield return game.Wait(0.3f);
            Assert.That(game.HudShows("pause-notice"), Is.False, "the notice clears");
            Assert.That(game.Authority.Armed, "still armed");
        }

        [UnityTest]
        public IEnumerator TArmsOnlyWithoutACockpitConnected()
        {
            yield return game.ReachControl();
            yield return game.PressKey(Key.T);
            Assert.That(game.Authority.Armed, Is.False, "the switch owns MASTER ARM while the cockpit is connected");

            game.Cockpit.SetUnplugged(true);
            yield return game.Until("the cockpit to drop", () => !ControllerService.Instance.State.Connected, 4f);
            yield return game.PressKey(Key.T);
            Assert.That(game.Authority.Armed, "T arms on the keyboard");
            yield return game.PressKey(Key.T);
            Assert.That(game.Authority.Armed, Is.False, "T makes safe");
        }

        [UnityTest]
        public IEnumerator LosingFocusPauses()
        {
            yield return game.ReachControl();
            game.Flow.SendMessage("OnApplicationFocus", false);
            Assert.That(game.Flow.State, Is.EqualTo(RunState.Paused));
            Assert.That(game.Flow.PauseReason, Is.EqualTo(PauseReason.FocusLost));
        }

        [UnityTest]
        public IEnumerator ThePauseInputStepsOutOfSubmenusThenResumes()
        {
            yield return game.ReachControl();
            yield return game.PressMenu();
            Assert.That(game.Flow.State, Is.EqualTo(RunState.Paused), "MENU pauses");

            game.ClickHud("pause-help-button");
            yield return game.Wait(0.3f);
            Assert.That(game.HudShows("help-panel"), "help opens");
            yield return game.Capture("pause-help");
            yield return game.PressMenu();
            AssertPausedOn("pause-panel", "help-panel");

            game.ClickHud("pause-help-button");
            yield return game.Wait(0.3f);
            game.ClickHud("help-back");
            yield return game.Wait(0.3f);
            AssertPausedOn("pause-panel", "help-panel");

            game.ClickHud("pause-options-button");
            yield return game.Wait(0.3f);
            Assert.That(game.HudShows("options-panel"), "options opens");
            yield return game.PressMenu();
            AssertPausedOn("pause-panel", "options-panel");

            game.ClickHud("pause-options-button");
            yield return game.Wait(0.3f);
            game.ClickHud("options-calibrate");
            yield return game.Wait(0.3f);
            Assert.That(game.HudShows("calibration-panel"), "calibration opens");
            yield return game.Capture("calibration");
            yield return game.PressMenu();
            AssertPausedOn("options-panel", "calibration-panel");

            game.ClickHud("options-back");
            yield return game.Wait(0.3f);
            AssertPausedOn("pause-panel", "options-panel");
            yield return game.Capture("pause");

            yield return game.PressMenu();
            Assert.That(game.Flow.State, Is.EqualTo(RunState.Playing), "MENU resumes from the pause panel");
            Assert.That(game.HudShows("pause-panel"), Is.False);

            game.Flow.Pause();
            yield return game.Wait(0.3f);
            game.ClickHud("resume-button");
            yield return game.Wait(0.3f);
            Assert.That(game.Flow.State, Is.EqualTo(RunState.Playing), "the resume button resumes");
        }

        private void AssertPausedOn(string shown, string left)
        {
            Assert.That(game.Flow.State, Is.EqualTo(RunState.Paused), $"still paused leaving {left}");
            Assert.That(game.HudShows(shown), $"{shown} shows");
            Assert.That(game.HudShows(left), Is.False, $"{left} is gone");
        }

        [UnityTest]
        public IEnumerator PauseOptionsShowAChangeMadeOnTheTitleScreen()
        {
            Toggle onTitle = game.MenuToggle("invert-vertical");
            onTitle.value = !onTitle.value;
            yield return game.ReachControl();
            game.Flow.Pause();
            yield return game.Wait(0.3f);
            game.ClickHud("pause-options-button");
            yield return game.Wait(0.3f);
            Assert.That(game.HudToggle("invert-vertical").value, Is.EqualTo(GameSettings.InvertVertical));
        }

        [UnityTest]
        public IEnumerator TheCrtOptionChangesLiveFromThePauseMenu()
        {
            yield return game.ReachControl();
            game.Flow.Pause();
            yield return game.Wait(0.3f);
            game.ClickHud("pause-options-button");
            yield return game.Wait(0.3f);
            Toggle crt = game.HudToggle("arcade-mode");
            bool before = GameSettings.ArcadeMode;
            Assert.That(crt.value, Is.EqualTo(before), "the toggle shows the setting");
            crt.value = !before;
            yield return game.Wait(0.5f);
            Assert.That(GameSettings.ArcadeMode, Is.Not.EqualTo(before));
            yield return game.Capture("options-crt-flipped");
        }

        [UnityTest]
        public IEnumerator RestartFromPauseReturnsToACleanTitleScreen()
        {
            yield return game.ReachControl();
            game.Flow.Pause();
            yield return game.Wait(0.3f);
            game.ClickHud("pause-retry-button");
            yield return game.Reloaded();
            AssertCleanTitleScreen();
        }

        [UnityTest]
        public IEnumerator LeavingMidEntryReturnsToACleanTitleScreen()
        {
            game.ClickMenu("play-button");
            yield return game.Until("the entry to reach 2 s", () => game.Entry.Elapsed > 2f, 6f);
            game.Flow.Pause();
            yield return game.Wait(0.3f);
            game.ClickHud("pause-menu-button");
            yield return game.Reloaded();
            AssertCleanTitleScreen();
        }

        private void AssertCleanTitleScreen()
        {
            Assert.That(Time.timeScale, Is.EqualTo(1f), "time runs");
            Assert.That(game.MenuShows("title-panel"), "the title shows");
            Assert.That(game.Flow.EntryHeld && !game.Flow.EntryStarted, "the entry waits for PLAY");
        }

        /// <summary>The whole arena, with a death at each kind of step, on to the results and back to the title.</summary>
        [UnityTest]
        public IEnumerator TheArenaTeachesEachWeaponThroughToTheResults()
        {
            yield return game.ReachControl();
            game.Cockpit.SetArmed(true);
            yield return game.Wait(0.3f);
            yield return FreeFlight();
            yield return LockLessons();
            yield return EmpLesson();
            yield return BreatherAndFinalWave();
            yield return Results();
        }

        private IEnumerator FreeFlight()
        {
            game.Jump(NearTheRailEnd);
            yield return game.Until("the rail to release the ship", () => game.Lessons.Current == ArenaLessons.Step.FreeFlight, 10f);
            RailSeal seal = Object.FindAnyObjectByType<RailSeal>();
            yield return game.Until("the seal to close behind the ship", () => seal.Sealed, 5f);
            yield return game.Wait(0.3f);
            Assert.That(game.HudShows("hud-rings"), Is.False, "the rings readout goes");
            Assert.That(game.HudText("hud-targets"), Does.Contain("/13"), "the target count starts over");
            yield return game.Capture("arena-start");

            yield return game.Wait(1f);
            game.Shield.Destroy();
            Assert.That(game.Course.Rebuilding, "a free flight death starts a rebuild");
            yield return game.Rebuilt();
            yield return game.Wait(0.3f);
            Assert.That(game.Lessons.Current, Is.EqualTo(ArenaLessons.Step.FreeFlight));
            Assert.That(game.Missiles.State.Ammo, Is.Zero, "no missiles before their lesson");
            game.SkipStepWait();
        }

        private IEnumerator LockLessons()
        {
            yield return game.Until("the LOCK step", () => game.Lessons.Current == ArenaLessons.Step.LockSingle, 3f);
            yield return game.WaveArrives("the LOCK target");
            Assert.That(game.Missiles.State.Ammo, Is.EqualTo(4), "missiles are issued");
            yield return game.Capture("lock-arrived");

            yield return game.Wait(3f); // MISSILES ONLINE has the panel first.
            game.NearestLive().ApplyHit(999);
            yield return game.Wait(0.4f);
            Assert.That(game.Header, Does.Contain("USE MISSILES"), "a gun kill asks for missiles");
            Assert.That(game.HudText("hud-targets"), Does.StartWith("TARGETS 0/"), "a gun kill doesn't count");
            yield return game.Capture("use-missiles");
            yield return game.WaveArrives("the target to come back");
            yield return game.PaintAndLaunch();

            yield return game.Until("the LOCK pair", () => game.Lessons.Current == ArenaLessons.Step.LockPair, 12f);
            yield return game.WaveArrives("the pair");
            game.Shield.Destroy();
            Assert.That(game.Course.Rebuilding, "a mid wave death starts a rebuild");
            Assert.That(game.Lessons.CurrentWave.Live.Count, Is.Zero, "the wave stands down");
            yield return game.Rebuilt();
            yield return game.Wait(0.2f);
            Assert.That(game.Lessons.Current, Is.EqualTo(ArenaLessons.Step.LockPair), "the step is kept");
            Assert.That(game.Missiles.State.Ammo, Is.EqualTo(4), "the bank is restocked");
            yield return game.WaveArrives("the pair to return");
            yield return game.PaintAndLaunch();
        }

        private IEnumerator EmpLesson()
        {
            yield return game.Until("the EMP step", () => game.Lessons.Current == ArenaLessons.Step.EmpGroup, 12f);
            yield return game.WaveArrives("the drones");
            Assert.That(game.Emp.State.Ready, "the EMP is issued");

            yield return game.PressEmp();
            Assert.That(game.Emp.Pulsing || !game.Emp.State.Ready, "a wasted pulse fires");
            yield return game.Until("the pulse to end", () => !game.Emp.Pulsing, 6f);
            yield return game.Wait(0.4f);
            Assert.That(game.Header, Does.Contain("RETRY"), "a retry is offered");
            yield return game.Capture("emp-retry-offer");
            yield return game.PressEmp();
            yield return game.Wait(0.3f);
            Assert.That(game.Emp.State.Ready, "the retry recharges");
            Assert.That(game.Lessons.Current, Is.EqualTo(ArenaLessons.Step.EmpGroup), "the retry keeps the step");

            yield return game.WaveArrives("the drones to return");
            game.ChaseWave = true;
            yield return game.Until("the line to ask for the pulse", () => game.Header.StartsWith("PRESS"), 25f);
            yield return game.Capture("press-emp");
            yield return game.PressEmp();
            yield return game.Wait(0.9f);
            yield return game.Capture("emp-pulse");
            yield return game.Until("the breather", () => game.Lessons.Current == ArenaLessons.Step.Breather, 15f);
            game.ChaseWave = false;
        }

        private IEnumerator BreatherAndFinalWave()
        {
            Assert.That(game.Shield.State.Current, Is.EqualTo(3), "the breather restores the shield");
            Assert.That(game.Missiles.State.Ammo, Is.EqualTo(4), "the breather restores the bank");
            game.Shield.Destroy();
            yield return game.Rebuilt();
            yield return game.Wait(0.1f);
            Assert.That(game.Emp.State.Ready, "a rebuild restocks the EMP");
            Assert.That(game.Lessons.Current, Is.EqualTo(ArenaLessons.Step.Breather), "still the breather");
            int arrivals = game.Tones(WaveArrivalTone);
            game.SkipStepWait();

            yield return game.Until("the final wave", () => game.Lessons.Current == ArenaLessons.Step.Encounter, 3f);
            yield return game.WaveArrives("the final wave to arrive");
            Assert.That(game.Tones(WaveArrivalTone), Is.GreaterThan(arrivals), "the arrival sounds on the cockpit");
            game.Shield.Destroy();
            yield return game.Rebuilt();
            yield return game.WaveArrives("the final wave to return");
            int clears = game.Tones(WaveClearedTone);
            foreach (ShootableTarget target in new List<ShootableTarget>(game.Lessons.CurrentWave.Live))
                if (target != null) target.ApplyHit(999);
            yield return game.Until("the ending", () => game.Lessons.Current == ArenaLessons.Step.Ending, 4f);
            yield return game.Wait(0.3f);
            Assert.That(game.Tones(WaveClearedTone), Is.GreaterThan(clears), "the clear sounds on the cockpit");
            Assert.That(game.Header, Does.Contain("TRAINING"));
            yield return game.Capture("ending");
        }

        private IEnumerator Results()
        {
            game.Flow.Pause();
            yield return game.Wait(1f);
            game.Flow.Resume();
            yield return game.Until("the results", () => game.Flow.State == RunState.Completed, 8f);
            yield return game.Wait(1.2f);
            Assert.That(game.HudShows("results-panel"), "the results show");
            Assert.That(game.HudShows("hud"), Is.False, "the HUD is hidden");
            Assert.That(game.HudText("results-title"), Does.Contain("TRAINING"));
            Assert.That(game.Flow.Results.TargetsTotal, Is.EqualTo(23), "the rail's targets and the arena's");
            yield return game.Capture("results");

            game.ClickHud("results-menu-button");
            yield return game.Reloaded();
            AssertCleanTitleScreen();
            yield return game.Capture("title-after-results");
        }
    }
}
