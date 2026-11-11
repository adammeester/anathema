using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Player
{
    // WASD moves relative to the (fixed) camera, independent of facing, so the character strafes while aiming.
    // Movement is instant: full speed immediately, stops immediately.
    //
    // Online the server owns position (Docs/ServerMovement.md). Every copy of a character runs in one role:
    //   - Local: offline, or the host's own character. Simulates directly from the keyboard.
    //   - Predicting: the owner on a client. Simulates from the keyboard straight away, sends each numbered input to
    //     the server, and when the server reports where it really was after input N, replays the later inputs from
    //     there. The visible model eases out small corrections.
    //   - Server: the server's copy of a remote player. Simulates from the inputs received, one per tick, and publishes
    //     the result.
    //   - Follower: everyone else's view of a remote player. NetPlayer moves it toward the server's position.
    // All of them move with the same MovementSim.Step, at MovementSim.TickRate.
    [DefaultExecutionOrder(-100)] // undo last frame's visual smoothing before anything else poses the model
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6.5f;
        [SerializeField] float gravity = -25f;

        public float MoveSpeed => moveSpeed;

        // Server tuning.
        const int MaxQueuedInputs = 6;       // inputs held per player; extra (a client sending too fast) are dropped
        const int MaxRepeatedInputs = 6;     // with no input, keep the last one going this many ticks (100 ms), then stop
        const int StateEveryTicks = 2;       // authoritative state to the owner at 30 Hz
        const float AbilityMoveTolerance = 1.1f; // reported ability movement may exceed the ability's range by 10%...
        const float AbilityMoveSlack = 0.5f;     // ...plus half a metre

        // Owner tuning.
        const int SendEveryTicks = 2;        // inputs to the server at 30 Hz
        const int RedundantInputs = 8;       // each send repeats the latest inputs, so a lost packet costs nothing
        const float IgnoreError = 0.01f;     // a prediction this close to the server's answer is left alone
        const float SnapError = 2f;          // bigger corrections (respawn, teleport) snap instead of easing
        const float SmoothingRate = 25f;     // how fast the model eases back onto the character (~100 ms)
        const int MaxTicksPerFrame = 5;

        enum Role { Local, Predicting, Server, Follower }

        CharacterController controller;
        Health health;
        NetPlayer net;
        Camera cam;
        Transform model;
        float verticalVelocity;
        float accumulator;
        int tick;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            net = GetComponent<NetPlayer>();
            model = transform.Find("Model");

            // Tuning values (health, speed, ability numbers) come from Resources/Stats/<Character>.json.
            CharacterStats.EnsureOn(gameObject);

            // Between-round upgrades (only for characters that have an upgrade list).
            Anathema.Upgrades.UpgradeState.EnsureOn(gameObject);

            // Scenes built before the walk cycle existed don't have it on the model; add it so they still animate.
            if (model != null && model.GetComponent<ProceduralWalk>() == null) model.gameObject.AddComponent<ProceduralWalk>();
        }

        Role CurrentRole
        {
            get
            {
                if (net == null || !net.IsSpawned) return Role.Local;
                if (net.IsOwner) return net.IsServer ? Role.Local : Role.Predicting;
                return net.IsServer ? Role.Server : Role.Follower;
            }
        }

        void Update()
        {
            UndoSmoothing();

            Role role = CurrentRole;
            if (role == Role.Follower) return;

            accumulator = Mathf.Min(accumulator + Time.deltaTime, MovementSim.Dt * MaxTicksPerFrame);
            while (accumulator >= MovementSim.Dt)
            {
                accumulator -= MovementSim.Dt;
                tick++;
                Vector3 before = transform.position;
                switch (role)
                {
                    case Role.Local: LocalTick(); break;
                    case Role.Predicting: PredictTick(); break;
                    case Role.Server: ServerTick(); break;
                }
                previousTickPosition = before;
                currentTickPosition = transform.position;
            }
        }

        // The simulation moves in 60 Hz steps, so on faster displays the character would stand still for some frames
        // and jump on others (which also reads as "not walking" to ProceduralWalk). The visible model is drawn between
        // the last two steps instead; the character itself (aiming, hits) stays on the latest step.
        Vector3 previousTickPosition;
        Vector3 currentTickPosition;
        const float MaxInterpolatedStep = 1f; // larger steps are dashes, teleports or resets: show them as they are

        Vector3 InterpolationOffset()
        {
            Role role = CurrentRole;
            if (role == Role.Follower || role == Role.Server) return Vector3.zero; // nobody watches the server's copy
            if ((transform.position - currentTickPosition).sqrMagnitude > 0.0001f) return Vector3.zero; // moved outside a step
            Vector3 step = currentTickPosition - previousTickPosition;
            if (step.sqrMagnitude > MaxInterpolatedStep * MaxInterpolatedStep) return Vector3.zero;
            float alpha = Mathf.Clamp01(accumulator / MovementSim.Dt);
            return -step * (1f - alpha);
        }

        // A correction or placement moved the character between steps: carry the interpolation along with it.
        void ShiftTickPositions(Vector3 delta)
        {
            previousTickPosition += delta;
            currentTickPosition += delta;
        }

        void LateUpdate()
        {
            ApplySmoothing();
        }

        bool CanMove => !health.IsDead;

        void Simulate(Vector2 direction)
        {
            if (!CanMove) return;
            MoveContext ctx = MovementSim.Gather(gameObject, moveSpeed, gravity);
            MovementSim.Step(controller, ref verticalVelocity, direction, ctx);
        }

        // The keyboard as a world-space direction (the camera is fixed, so this is the same everywhere it's computed).
        Vector2 SampleInput()
        {
            if (health.IsDead) return Vector2.zero;
            if (cam == null) cam = Camera.main;

            Vector2 keys = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cam != null)
            {
                forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            }
            Vector3 world = forward * keys.y + right * keys.x;
            return new Vector2(world.x, world.z);
        }

        // --- Local (offline / host's own character) ----------------------------------------------------------------

        void LocalTick()
        {
            Simulate(SampleInput());
        }

        // --- Predicting (owner on a client) ------------------------------------------------------------------------

        struct Predicted
        {
            public MoveInput Input;
            public Vector3 Position;          // where this client ended up after the input
            public float VerticalVelocity;
        }

        readonly List<Predicted> history = new List<Predicted>();
        uint nextSeq = 1;
        uint lastAcked;
        bool simPositionKnown;
        Vector3 lastSimPosition;
        Vector3 visualOffset;   // world offset of the model from the character, eased to zero
        Vector3 appliedOffset;  // what LateUpdate added to the model this frame (undone next Update)

        // Corrections seen recently, for the debug overlay (count and largest, per second).
        public int CorrectionsLastSecond { get; private set; }
        public float LargestCorrectionLastSecond { get; private set; }
        int correctionCount;
        float largestCorrection;
        float correctionWindowStart;

        void PredictTick()
        {
            // Something other than this step moved the character since the last tick: one of its own abilities
            // (dash, leap, teleport). Report where it got to instead of walking; the server checks it.
            bool abilityMoved = simPositionKnown && (transform.position - lastSimPosition).sqrMagnitude > 0.0001f;

            var input = new MoveInput { Seq = nextSeq++ };
            if (abilityMoved)
            {
                input.Kind = MoveInput.AbilityMove;
                input.Position = transform.position;
            }
            else
            {
                input.Kind = MoveInput.Walk;
                input.Direction = SampleInput();
                Simulate(input.Direction);
            }

            lastSimPosition = transform.position;
            simPositionKnown = true;
            history.Add(new Predicted { Input = input, Position = transform.position, VerticalVelocity = verticalVelocity });
            if (history.Count > 600) history.RemoveRange(0, history.Count - 600); // the server has stopped answering

            if (tick % SendEveryTicks == 0) SendInputs();
            UpdateCorrectionStats(0f, false);
        }

        void SendInputs()
        {
            int count = Mathf.Min(RedundantInputs, history.Count);
            if (count == 0) return;
            var inputs = new MoveInput[count];
            for (int i = 0; i < count; i++) inputs[i] = history[history.Count - count + i].Input;
            net.SendMoveInputs(inputs);
        }

        // The server's answer: where the character really was after input `seq`.
        public void OwnerReceiveState(uint seq, Vector3 position, float serverVerticalVelocity)
        {
            if (seq <= lastAcked) return; // out of date (unreliable messages can arrive out of order)
            lastAcked = seq;

            int index = history.FindIndex(h => h.Input.Seq == seq);
            if (index < 0) return; // already replayed past it, or never sent from this history
            Vector3 predicted = history[index].Position;
            history.RemoveRange(0, index + 1);

            float error = Vector3.Distance(predicted, position);
            if (error < IgnoreError) return;

            // Rewind to the server's answer and replay every input it hasn't processed yet.
            Vector3 before = transform.position;
            MovementSim.Place(controller, transform, position);
            verticalVelocity = serverVerticalVelocity;
            for (int i = 0; i < history.Count; i++)
            {
                Predicted h = history[i];
                if (h.Input.Kind == MoveInput.AbilityMove) MovementSim.Place(controller, transform, h.Input.Position);
                else Simulate(h.Input.Direction);
                h.Position = transform.position;
                h.VerticalVelocity = verticalVelocity;
                history[i] = h;
            }
            lastSimPosition = transform.position;

            // The character is at the corrected spot already (aiming and hits use it); the model eases over.
            Vector3 jump = before - transform.position;
            ShiftTickPositions(-jump);
            visualOffset =jump.magnitude > SnapError ? Vector3.zero : visualOffset + jump;
            UpdateCorrectionStats(error, true);
        }

        // The server placed the character (spawn, round reset): start predicting from there.
        public void OwnerReceiveReset(Vector3 position)
        {
            history.Clear();
            MovementSim.Place(controller, transform, position);
            verticalVelocity = 0f;
            lastSimPosition = position;
            simPositionKnown = true;
            visualOffset = Vector3.zero;
            previousTickPosition = currentTickPosition = position;
        }

        void UpdateCorrectionStats(float error, bool corrected)
        {
            if (corrected)
            {
                correctionCount++;
                largestCorrection = Mathf.Max(largestCorrection, error);
            }
            if (Time.time - correctionWindowStart < 1f) return;
            CorrectionsLastSecond = correctionCount;
            LargestCorrectionLastSecond = largestCorrection;
            correctionCount = 0;
            largestCorrection = 0f;
            correctionWindowStart = Time.time;
        }

        // F3: how often (and how far) the server corrected this client's prediction. Walking alone with no lag should
        // read 0; a stun or a shove that arrives late shows one small correction.
        static bool showOverlay;

        void OnGUI()
        {
            if (CurrentRole != Role.Predicting) return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F3) showOverlay = !showOverlay;
            if (!showOverlay) return;
            GUI.Label(new Rect(10, 10, 420, 24),
                $"Prediction: {CorrectionsLastSecond} corrections/s, largest {LargestCorrectionLastSecond * 100f:0.0} cm, " +
                $"{history.Count} inputs awaiting the server");
        }

        void UndoSmoothing()
        {
            if (model == null || appliedOffset == Vector3.zero) return;
            model.position -= appliedOffset;
            appliedOffset = Vector3.zero;
        }

        void ApplySmoothing()
        {
            if (model == null) return;
            if (visualOffset != Vector3.zero)
            {
                visualOffset = Vector3.Lerp(visualOffset, Vector3.zero, 1f - Mathf.Exp(-SmoothingRate * Time.deltaTime));
                if (visualOffset.sqrMagnitude < 0.000001f) visualOffset = Vector3.zero;
            }
            Vector3 offset = visualOffset + InterpolationOffset();
            if (offset == Vector3.zero) return;
            model.position += offset;
            appliedOffset = offset;
        }

        // --- Server (the server's copy of a remote player) ----------------------------------------------------------

        readonly Queue<MoveInput> queue = new Queue<MoveInput>();
        uint lastQueuedSeq;
        uint lastProcessedSeq;
        MoveInput lastInput;
        bool hasLastInput;
        int repeats;
        float abilityMoveBudget;
        float abilityMoveUntil;

        public void ServerReceiveInputs(MoveInput[] inputs)
        {
            foreach (MoveInput input in inputs)
            {
                if (input.Seq <= lastQueuedSeq) continue; // a repeat of one already received
                lastQueuedSeq = input.Seq;
                queue.Enqueue(input);
            }
            // Inputs are consumed at a fixed rate; sending them faster only fills the queue, and the excess is dropped.
            while (queue.Count > MaxQueuedInputs) queue.Dequeue();
        }

        // Inputs travel unreliably and the ability's cue reliably, so a dash's first reported step can arrive before the
        // cue that allows it. Hold such a step this many ticks (~170 ms) waiting for the cue before rejecting it.
        const int MaxAbilityMoveWait = 10;
        int abilityMoveWait;

        void ServerTick()
        {
            if (queue.Count > 0 && queue.Peek().Kind == MoveInput.AbilityMove && !AbilityMoveAllowed(queue.Peek().Position)
                && abilityMoveWait < MaxAbilityMoveWait)
            {
                abilityMoveWait++;
                Simulate(Vector2.zero);
            }
            else if (queue.Count > 0)
            {
                abilityMoveWait = 0;
                MoveInput input = queue.Dequeue();
                lastInput = input;
                hasLastInput = true;
                repeats = 0;
                if (input.Kind == MoveInput.AbilityMove) ServerAbilityMove(input.Position);
                else Simulate(input.Direction);
                lastProcessedSeq = input.Seq;
            }
            else if (hasLastInput && lastInput.Kind == MoveInput.Walk && repeats < MaxRepeatedInputs)
            {
                // Late or lost input: assume the player is still doing what they were doing.
                repeats++;
                Simulate(lastInput.Direction);
            }
            else
            {
                Simulate(Vector2.zero); // gravity, knockback and panic still apply
            }

            if (tick % StateEveryTicks == 0 && lastProcessedSeq > 0)
                net.SendMoveState(lastProcessedSeq, transform.position, verticalVelocity);
        }

        // Server: an ability that moves its caster was cast (or a counter opened a teleport): accept reported
        // movement up to `range` for `window` seconds.
        public void GrantAbilityMove(float range, float window)
        {
            abilityMoveBudget = range * AbilityMoveTolerance + AbilityMoveSlack;
            abilityMoveUntil = Time.time + window;
        }

        bool AbilityMoveAllowed(Vector3 target)
        {
            Vector3 flat = target - transform.position;
            float vertical = Mathf.Abs(flat.y);
            flat.y = 0f;
            return Time.time <= abilityMoveUntil
                   && flat.magnitude <= abilityMoveBudget
                   && vertical < 4f
                   && !health.IsDead
                   && !Stunned.IsStunned(gameObject) && !Rooted.IsRooted(gameObject);
        }

        void ServerAbilityMove(Vector3 target)
        {
            if (!AbilityMoveAllowed(target)) return; // stays put; the owner is corrected back by the next state

            Vector3 flat = target - transform.position;
            flat.y = 0f;
            abilityMoveBudget -= flat.magnitude;
            MovementSim.Place(controller, transform, target);
        }

        // Server: the game itself puts the character somewhere (spawn, round reset). The owner snaps there too.
        public void ServerPlace(Vector3 position)
        {
            queue.Clear();
            hasLastInput = false;
            verticalVelocity = 0f;
            MovementSim.Place(controller, transform, position);
            previousTickPosition = currentTickPosition = position;
            if (net != null && net.IsSpawned) net.SendMoveReset(position);
        }
    }
}
