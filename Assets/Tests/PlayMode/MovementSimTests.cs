using Anathema.Player;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Anathema.Tests
{
    // The shared movement step that both the owner's prediction and the server run. If these two ever disagree for the
    // same inputs, players rubber-band, so the step must depend only on its inputs and context.
    public class MovementSimTests
    {
        GameObject go;
        CharacterController controller;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Mover");
            controller = go.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = Vector3.up;
            go.transform.position = new Vector3(0f, 50f, 0f); // well clear of anything in the test scene
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        static MoveContext Walking(float speed = 6f) => new MoveContext { CanSteer = true, Speed = speed, Gravity = 0f };

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [Test]
        public void Step_MovesAtSpeedForOneTick()
        {
            float vy = 0f;
            Vector3 start = go.transform.position;

            MovementSim.Step(controller, ref vy, new Vector2(1f, 0f), Walking(6f));

            Assert.AreEqual(6f * MovementSim.Dt, Flat(go.transform.position - start).magnitude, 0.001f);
        }

        [Test]
        public void Step_ClampsDiagonalInputToFullSpeed()
        {
            float vy = 0f;
            Vector3 start = go.transform.position;

            MovementSim.Step(controller, ref vy, new Vector2(1f, 1f), Walking(6f));

            Assert.AreEqual(6f * MovementSim.Dt, Flat(go.transform.position - start).magnitude, 0.001f);
        }

        [Test]
        public void Step_CannotSteerWhenPlanted()
        {
            float vy = 0f;
            Vector3 start = go.transform.position;
            MoveContext ctx = Walking();
            ctx.CanSteer = false;

            MovementSim.Step(controller, ref vy, new Vector2(1f, 0f), ctx);

            Assert.AreEqual(0f, Flat(go.transform.position - start).magnitude, 0.0001f);
        }

        [Test]
        public void Step_DoesNothingWhileFrozen()
        {
            float vy = 0f;
            Vector3 start = go.transform.position;
            MoveContext ctx = Walking();
            ctx.Frozen = true;
            ctx.Gravity = -25f;

            MovementSim.Step(controller, ref vy, new Vector2(1f, 0f), ctx);

            Assert.AreEqual(start, go.transform.position);
        }

        [Test]
        public void Step_KnockbackAppliesEvenWhenPlanted()
        {
            float vy = 0f;
            Vector3 start = go.transform.position;
            MoveContext ctx = Walking();
            ctx.CanSteer = false;
            ctx.KnockedBack = true;
            ctx.KnockbackVelocity = new Vector3(0f, 0f, 10f);

            MovementSim.Step(controller, ref vy, Vector2.zero, ctx);

            Assert.AreEqual(10f * MovementSim.Dt, go.transform.position.z - start.z, 0.001f);
        }

        [Test]
        public void Step_PanicOverridesSteering()
        {
            float vy = 0f;
            Vector3 start = go.transform.position;
            MoveContext ctx = Walking(6f);
            ctx.Fleeing = true;
            ctx.FleeDirection = Vector3.back;

            MovementSim.Step(controller, ref vy, new Vector2(1f, 0f), ctx); // tries to go right, runs back instead

            Vector3 moved = Flat(go.transform.position - start);
            Assert.AreEqual(0f, moved.x, 0.001f);
            Assert.AreEqual(-6f * MovementSim.Dt, moved.z, 0.001f);
        }

        [Test]
        public void Step_LeashHoldsThePlayerInsideThePrepArea()
        {
            float vy = 0f;
            MoveContext ctx = Walking(600f); // one tick would carry them 10 m
            ctx.Leashed = true;
            ctx.LeashCentre = go.transform.position;
            ctx.LeashRadius = 3f;

            MovementSim.Step(controller, ref vy, new Vector2(1f, 0f), ctx);

            Assert.LessOrEqual(Flat(go.transform.position - ctx.LeashCentre).magnitude, 3.01f);
        }

        [Test]
        public void SameInputsFromTheSameStart_LandInTheSamePlace()
        {
            // What prediction relies on: replaying the same inputs reproduces the same result.
            Vector2[] inputs = { new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(-0.5f, 0.2f) };
            Vector3 start = go.transform.position;

            float vy = 0f;
            foreach (Vector2 input in inputs) MovementSim.Step(controller, ref vy, input, Walking());
            Vector3 first = go.transform.position;

            MovementSim.Place(controller, go.transform, start);
            vy = 0f;
            foreach (Vector2 input in inputs) MovementSim.Step(controller, ref vy, input, Walking());

            Assert.AreEqual(first.x, go.transform.position.x, 0.0001f);
            Assert.AreEqual(first.z, go.transform.position.z, 0.0001f);
        }

        [Test]
        public void MoveInput_SurvivesNetworkSerialization()
        {
            var sent = new MoveInput { Seq = 4242, Kind = MoveInput.AbilityMove, Direction = new Vector2(0.6f, -0.8f), Position = new Vector3(1f, 2f, 3f) };
            MoveInput received = default;
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(sent);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                    reader.ReadNetworkSerializable(out received);
            }

            Assert.AreEqual(sent.Seq, received.Seq);
            Assert.AreEqual(sent.Kind, received.Kind);
            Assert.AreEqual(sent.Direction, received.Direction);
            Assert.AreEqual(sent.Position, received.Position);
        }
    }
}
