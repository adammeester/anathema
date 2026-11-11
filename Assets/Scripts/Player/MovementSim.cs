using Anathema.Abilities;
using Anathema.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Anathema.Player
{
    // One movement input, sent by the owner to the server 60 times a second (see PlayerMovement).
    public struct MoveInput : INetworkSerializable
    {
        public const byte Walk = 0;        // `Direction` is the stick: world-space XZ, length up to 1
        public const byte AbilityMove = 1; // an ability (dash, leap, teleport) moved the character to `Position`

        public uint Seq;
        public byte Kind;
        public Vector2 Direction;
        public Vector3 Position;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seq);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Position);
        }
    }

    // Everything a movement step reads from the character's state, gathered once per step.
    public struct MoveContext
    {
        public bool Frozen;            // round-end freeze: nothing moves
        public bool CanSteer;          // false while stunned, rooted, guarding or planted by an ability
        public float Speed;            // base speed with every slow and bonus applied
        public bool Fleeing;           // panicked: runs from the source instead of steering
        public Vector3 FleeDirection;
        public bool KnockedBack;
        public Vector3 KnockbackVelocity;
        public bool Leashed;           // prep area: held within LeashRadius of LeashCentre
        public Vector3 LeashCentre;
        public float LeashRadius;
        public float Gravity;
    }

    // The single movement step shared by the owner's prediction and the server's simulation. Both run it at TickRate
    // with the same inputs and the same context, so they land in the same place; when they don't (the server knew about
    // a stun the owner hadn't heard of yet), the owner is corrected (PlayerMovement.OwnerReceiveState).
    public static class MovementSim
    {
        public const float TickRate = 60f;
        public const float Dt = 1f / TickRate;

        public static MoveContext Gather(GameObject character, float moveSpeed, float gravity)
        {
            var ctx = new MoveContext { Gravity = gravity, Frozen = Anathema.Net.NetGame.Frozen };

            var counter = character.GetComponent<Counter>();
            bool planted = (counter != null && counter.IsGuarding) || Stunned.IsStunned(character)
                           || Rooted.IsRooted(character) || RootedByAbility(character);
            ctx.CanSteer = !planted;

            var maimed = character.GetComponent<Maimed>();
            ctx.Speed = moveSpeed * (maimed != null ? maimed.SpeedMultiplier : 1f) * Enraged.MoveSpeedMultiplier(character)
                        * ReflectDome.SlowMultiplier(character) * Slowed.Multiplier(character);

            ctx.Fleeing = Panicked.TryGetFleeDirection(character, out ctx.FleeDirection);
            ctx.KnockedBack = Knockback.TryGetVelocity(character, out ctx.KnockbackVelocity);

            // Prep area between rounds: you can walk about, but not out of your spawn zone. (A ready room's own walls
            // and gate hold you in, so the leash only applies to a spawn out in the arena.)
            if (Anathema.Net.NetGame.InPrep && character.TryGetComponent(out Respawner spawn)
                && !Anathema.Arena.ReadyRoom.AnyContains(spawn.SpawnPosition))
            {
                ctx.Leashed = true;
                ctx.LeashCentre = spawn.SpawnPosition;
                ctx.LeashRadius = Anathema.Net.NetGame.PrepRadius;
            }
            return ctx;
        }

        static bool RootedByAbility(GameObject character)
        {
            foreach (Ability ability in character.GetComponents<Ability>())
                if (ability.RootsMovement) return true;
            return false;
        }

        // Moves the character one tick. `direction` is the walking input (world XZ, length up to 1).
        public static void Step(CharacterController controller, ref float verticalVelocity, Vector2 direction, in MoveContext ctx)
        {
            if (ctx.Frozen || controller == null || !controller.enabled) return;

            Vector2 input = ctx.CanSteer ? Vector2.ClampMagnitude(direction, 1f) : Vector2.zero;
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + ctx.Gravity * Dt;

            Vector3 velocity = new Vector3(input.x, 0f, input.y) * ctx.Speed;
            // Panic overrides steering: run straight away from whatever caused it (unless stunned/rooted/guarding).
            if (ctx.CanSteer && ctx.Fleeing) velocity = ctx.FleeDirection * ctx.Speed;
            if (ctx.KnockedBack) velocity += new Vector3(ctx.KnockbackVelocity.x, 0f, ctx.KnockbackVelocity.z);
            velocity.y = verticalVelocity;
            controller.Move(velocity * Dt);

            if (ctx.Leashed)
            {
                Vector3 offset = controller.transform.position - ctx.LeashCentre;
                offset.y = 0f;
                if (offset.magnitude > ctx.LeashRadius) controller.Move(-offset.normalized * (offset.magnitude - ctx.LeashRadius));
            }
        }

        // Puts the character somewhere without the controller fighting it.
        public static void Place(CharacterController controller, Transform transform, Vector3 position)
        {
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            transform.position = position;
            if (wasEnabled) controller.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
