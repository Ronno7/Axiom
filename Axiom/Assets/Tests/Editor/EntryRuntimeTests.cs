using System;
using System.Reflection;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using NUnit.Framework;
using UnityEngine;

namespace GeometricFlight.Tests
{
    /// <summary>Exercises admission using the real flight, clock and camera components without starting
    /// a scene, serial service, weapon pool or input device.</summary>
    public sealed class EntryRuntimeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject actor, inputObject, cameraObject;
        private ShipFlight flight;
        private ArenaBounds bounds;
        private LevelFlow flow;
        private EntryCountingInput input;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Entry actor (inactive)");
            actor.SetActive(false);
            flight = actor.AddComponent<ShipFlight>();
            bounds = actor.AddComponent<ArenaBounds>();
            Set(bounds, "radius", 10000f);
            Set(bounds, "minAltitude", -1000f);
            Set(bounds, "ceiling", 1000f);
            Set(flight, "arena", bounds);
            Set(flight, "pushToDive", false);

            inputObject = new GameObject("Entry input counter");
            input = inputObject.AddComponent<EntryCountingInput>();
            Set(flight, "inputSource", input);

            flow = actor.AddComponent<LevelFlow>();
            Set(flow, "flight", flight);
            Set(flow, "targets", Array.Empty<ShootableTarget>());
            var finish = new GameObject("Course end");
            finish.transform.SetParent(actor.transform);
            finish.transform.position = Vector3.forward * 10000f;
            Set(flow, "courseEnd", finish.transform);
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (inputObject != null) UnityEngine.Object.DestroyImmediate(inputObject);
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            Time.timeScale = 1f;
        }

        private void Hold()
        {
            flight.HoldForEntry();
            Set(flow, "<EntryHeld>k__BackingField", true);
        }

        private static ArenaFlightState EntryState(bool full)
        {
            var state = new ArenaFlightState { Position = new Vector3(0f, 70f, 0f), Speed = 30f };
            state.SetFullRotation(full);
            return state;
        }

        [Test]
        public void HeldEntrySamplesEachUpdateWithoutAdvancingTheStagedState()
        {
            Hold();
            ArenaFlightState staged = EntryState(true);
            staged.Position.z = -100f;
            flight.StageEntry(staged);
            input.Value = new FlightInput(Vector2.right, 0.73f, true, true, 0.4f, true, false, true);

            Step(flight, 0.25f);
            Assert.That(input.Reads, Is.EqualTo(1));
            Assert.That(input.LastDeltaTime, Is.EqualTo(0.25f));
            Assert.That(flight.CurrentInput.Steering, Is.EqualTo(input.Value.Steering));
            Assert.That(flight.CurrentInput.PausePressed, Is.True);
            Assert.That(flight.CurrentInput.Throttle, Is.EqualTo(0.73f), "The cached input keeps the raw setpoint.");
            Assert.That(flight.Throttle, Is.EqualTo(ArenaFlightModel.SnapCruise(0.73f, flight.Tuning.cruiseSnap)),
                "The held instrument setpoint uses the same cruise notch as live flight.");
            Assert.That(flight.InputSampleFrame, Is.EqualTo(Time.frameCount));
            Assert.That(flight.State.Position, Is.EqualTo(staged.Position));
            Assert.That(flight.transform.position, Is.EqualTo(staged.Position));
            Assert.That(flight.CurrentSpeed, Is.EqualTo(30f));

            input.Value = new FlightInput(Vector2.left, -0.4f, false, false);
            Step(flight, 0.1f);
            Assert.That(input.Reads, Is.EqualTo(2));
            Assert.That(flight.CurrentInput.Throttle, Is.EqualTo(-0.4f));
            Assert.That(flight.Throttle, Is.EqualTo(ArenaFlightModel.SnapCruise(-0.4f, flight.Tuning.cruiseSnap)));
            Assert.That(flight.State.Position, Is.EqualTo(staged.Position));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdmissionKeepsHeldInputAndStartsTheNormalMovementLaw(bool full)
        {
            Hold();
            ArenaFlightState staged = EntryState(full);
            flight.StageEntry(staged);
            input.Value = new FlightInput(Vector2.right, 0.7f, false, false, 0.25f);
            Step(flight, 0.1f);
            Assert.That(flow.BeginEntry(), Is.True);
            Step(flow, 4f);
            Assert.That(flow.ElapsedTime, Is.Zero, "Cinematic time is not scored run time.");

            Assert.That(flow.TryBeginEntryRun(), Is.True);
            Assert.That(flow.TryBeginEntryRun(), Is.False, "Admission happens once.");
            Assert.That(input.Reads, Is.EqualTo(1), "Admission must not poll or reset held input.");
            Assert.That(flight.EntryHeld, Is.False);
            Assert.That(flight.State.Position.z, Is.Zero, "All 240 metres of clear route remain playable.");

            ArenaFlightState expected = staged;
            Vector2 steering = Vector2.right * GameSettings.SteeringSensitivity;
            float throttle = ArenaFlightModel.SnapCruise(input.Value.Throttle, flight.Tuning.cruiseSnap);
            if (full)
                ArenaFlightModel.StepFull(ref expected, steering, 0.25f, throttle, flight.Tuning, bounds.Limits, 0.1f);
            else
                ArenaFlightModel.Step(ref expected, steering, 0.25f, throttle, flight.Tuning, bounds.Limits, 0.1f);
            Step(flight, 0.1f);
            Step(flow, 0.1f);

            Assert.That(input.Reads, Is.EqualTo(2));
            Assert.That(Vector3.Distance(flight.State.Position, expected.Position), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(flight.State.Rotation, expected.Rotation), Is.LessThan(0.01f));
            Assert.That(flight.CurrentSpeed, Is.EqualTo(expected.Speed).Within(0.0001f));
            Assert.That(flight.Velocity, Is.EqualTo(expected.Forward * expected.Speed));
            Assert.That(flow.ElapsedTime, Is.EqualTo(0.1f));

            flight.StageEntry(staged);
            Assert.That(flight.State.Position, Is.EqualTo(expected.Position),
                "Entry presentation cannot reposition the actor after admission.");
        }

        [Test]
        public void IdleMenuIgnoresPauseThenEntryUsesNativePauseAndRefusesPausedAdmission()
        {
            Hold();
            flight.StageEntry(EntryState(true));
            Set(flow, "courseEnd", actor.transform); // Completion would be immediate without the hold.
            Assert.That(flow.TryBeginEntryRun(), Is.False);
            flow.Pause();
            Assert.That(flow.State, Is.EqualTo(RunState.Playing), "Menu owns its own back navigation.");
            Assert.That(flow.BeginEntry(), Is.True);
            Assert.That(flow.BeginEntry(), Is.False);
            flow.Pause();
            Step(flow, 2f);
            Assert.That(flow.State, Is.EqualTo(RunState.Paused));
            Assert.That(flow.TryBeginEntryRun(), Is.False);
            Assert.That(flight.EntryHeld, Is.True);
            Assert.That(flow.ElapsedTime, Is.Zero);
            flow.Resume();
            Step(flow, 2f);
            Assert.That(flow.State, Is.EqualTo(RunState.Playing));
            Assert.That(flow.ElapsedTime, Is.Zero);
            Assert.That(flow.TryBeginEntryRun(), Is.True);
        }

        [Test]
        public void ExistingScenesStartTheirRunWithoutEntryOptIn()
        {
            Assert.That(flow.EntryHeld, Is.False);
            Assert.That(flight.EntryHeld, Is.False);
            Assert.That(flow.BeginEntry(), Is.False);
            Step(flow, 0.25f);
            Assert.That(flow.ElapsedTime, Is.EqualTo(0.25f));
            flow.Pause();
            Assert.That(flow.State, Is.EqualTo(RunState.Paused));
        }

        [Test]
        public void HeldMenuOnReconnectCannotResumeButALaterDeliberatePressCan()
        {
            ShipFlight sampled = inputObject.AddComponent<ShipFlight>();
            Set(flow, "flight", sampled);
            flow.Pause();
            SetInput(sampled, new FlightInput(Vector2.zero, 0f, false, true));
            typeof(LevelFlow).GetMethod("OnControllerConnected", Private).Invoke(flow, null);
            Step(flow, 0f);
            Assert.That(flow.State, Is.EqualTo(RunState.Paused),
                "The reconnect packet's held MENU edge must not resume flight.");

            Set(flow, "controllerConnectedFrame", Time.frameCount - 1);
            SetInput(sampled, default);
            Step(flow, 0f);
            Assert.That(flow.State, Is.EqualTo(RunState.Paused));
            SetInput(sampled, new FlightInput(Vector2.zero, 0f, false, true));
            Step(flow, 0f);
            Assert.That(flow.State, Is.EqualTo(RunState.Playing), "A fresh MENU press still resumes normally.");
            Assert.That(input.Reads, Is.Zero, "Recovery observes the cached snapshot without polling input.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CameraResetReplacesOldSpringWithShipMotionAndUsesTheExactStagedPose(bool full)
        {
            Hold();
            ArenaFlightState staged = EntryState(full);
            staged.Yaw = 25f;
            staged.Pitch = 12f;
            staged.Bank = 30f;
            staged.Attitude = Quaternion.Euler(-12f, 25f, -30f);
            staged.Speed = flight.MaxSpeed;
            flight.StageEntry(staged);
            cameraObject = new GameObject("Entry camera (inactive)");
            cameraObject.SetActive(false);
            Camera view = cameraObject.AddComponent<Camera>();
            ChaseCamera chase = cameraObject.AddComponent<ChaseCamera>();
            Set(chase, "target", flight);
            Set(chase, "springVelocity", new Vector3(50f, -20f, 60f));
            Set(chase, "placed", true);
            cameraObject.transform.SetPositionAndRotation(Vector3.one * -500f, Quaternion.Euler(90f, 0f, 180f));
            chase.GetTargetPose(out Vector3 position, out Quaternion rotation, out float fov);
            Assert.That(cameraObject.transform.position, Is.EqualTo(Vector3.one * -500f),
                "Reading the chase relation must not move the cinematic camera.");

            chase.ResetToTarget();
            Assert.That(cameraObject.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(cameraObject.transform.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(view.fieldOfView, Is.EqualTo(fov));
            Assert.That(fov, Is.EqualTo(72f));
            // The stale spring is discarded; the camera continues with the ship so control has no jolt.
            Assert.That((Vector3)Get(chase, "springVelocity"), Is.EqualTo(flight.Velocity));
            Assert.That((bool)Get(chase, "placed"), Is.True);
        }

        private static void SetInput(ShipFlight flight, FlightInput input) =>
            Set(flight, "<CurrentInput>k__BackingField", input);
        private static void Step(object component, float deltaTime) =>
            component.GetType().GetMethod("Step", Private).Invoke(component, new object[] { deltaTime });
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
        private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    }

    public sealed class EntryCountingInput : FlightInputSource
    {
        public FlightInput Value;
        public int Reads;
        public float LastDeltaTime;
        public override FlightInput ReadInput(float deltaTime)
        {
            Reads++;
            LastDeltaTime = deltaTime;
            return Value;
        }
    }
}
