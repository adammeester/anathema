using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Anathema.Arena
{
    // The Hoarfrost Basilica's frozen reliquaries (ReliquaryCasket). A slam that lands on one launches it across the
    // ice in the slam's direction; it slides until it meets solid scenery (a pillar, another casket, the outer wall),
    // up to MaxSlide. The first enemy of whoever slammed it that stands in its way is pinned to its front and carried:
    // when they reach the wall they take ImpactDamage and are stunned for ImpactStun. A carried enemy who casts a
    // dedicated movement ability (Ability.EscapesCasket) breaks free with no stun, and the casket slides on. Enemies who
    // can't be shoved (Unstoppable, Enraged, Augmetic Bracing...) stop it dead: they take the damage, no carry, no stun.
    // A casket shatters when its slide ends and is back on its plinth RespawnAfter later (and at every round reset).
    // A free slide that ends against another resting casket knocks that one on in the same direction.
    //
    // Abilities report slams with Slam (a circle or a line, pushed away from it), Cone (a frontal swing, pushed away
    // from the attacker) or Sweep (a moving wall, pushed along it). Only the caster's own machine reports.
    //
    // Online the server decides everything: the reporting client sends the slam to the server ("casketSlam"), which
    // picks the caskets from its own copy, works out where each slide ends and tells every machine ("casket"); every
    // machine then moves its own copy the same way. Catches, escapes ("casketEscape", owner -> server) and impacts are
    // resolved on the server only, which shortens or lengthens the slide for everyone. The impact is resolved Grace
    // seconds after the casket stops, so an escape sent just before impact still counts despite the lag.
    public class CasketField : MonoBehaviour
    {
        public static CasketField Instance { get; private set; }

        public const float Speed = 20f;          // metres per second: the full width of the arena in about two seconds
        public const float MaxSlide = 42f;
        public const float ImpactDamage = 50f;
        public const float ImpactStun = 2.5f;
        public const float RespawnAfter = 10f;
        const float OnlineGrace = 0.12f;

        // Server-side limits on what a client may report.
        const float MaxSlamRadius = 6f;
        const float MaxSlamLength = 20f;
        const float MaxSlamReach = 22f;

        const string SlamMessage = "casketSlam";
        const string EscapeMessage = "casketEscape";
        const string StateMessage = "casket";

        enum Shape : byte { Radial, Cone, Sweep }
        enum Op : byte { Launch, Retarget, Shatter, Restore }

        [SerializeField] ReliquaryCasket[] caskets;

        // Server (or offline) bookkeeping for one slide.
        class Run
        {
            public GameObject launcher;
            public int team;
            public Vector3 fullEnd;           // where the slide ends if nobody is carried
            public int chainTo = -1;          // the resting casket at fullEnd, knocked on when this one hits it
            public Health victim;             // pinned to the front right now
            public bool immune;               // the victim stopped it dead instead
            public float resolveAt;
            public readonly HashSet<Health> passed = new HashSet<Health>(); // escaped (or already struck): never caught again
        }

        Run[] runs;
        float[] respawnAt;
        bool registered;

        public void Configure(ReliquaryCasket[] list) => caskets = list;

        static bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        static bool Authority => !Online || NetworkManager.Singleton.IsServer;
        static float Grace => Online ? OnlineGrace : 0f;

        void Awake()
        {
            Instance = this;
            runs = new Run[caskets != null ? caskets.Length : 0];
            respawnAt = new float[runs.Length];
            for (int i = 0; i < respawnAt.Length; i++) respawnAt[i] = -1f;
            GasZone.Restarted += ResetAll;
        }

        void Start()
        {
            if (!Online) return;
            CustomMessagingManager msg = NetworkManager.Singleton.CustomMessagingManager;
            msg.RegisterNamedMessageHandler(StateMessage, OnStateMessage);
            if (NetworkManager.Singleton.IsServer)
            {
                msg.RegisterNamedMessageHandler(SlamMessage, OnSlamMessage);
                msg.RegisterNamedMessageHandler(EscapeMessage, OnEscapeMessage);
            }
            registered = true;
        }

        void OnDestroy()
        {
            GasZone.Restarted -= ResetAll;
            if (Instance == this) Instance = null;
            if (!registered || NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;
            CustomMessagingManager msg = NetworkManager.Singleton.CustomMessagingManager;
            msg.UnregisterNamedMessageHandler(StateMessage);
            msg.UnregisterNamedMessageHandler(SlamMessage);
            msg.UnregisterNamedMessageHandler(EscapeMessage);
        }

        // ---- Reporting (the caster's machine) ----------------------------------------------------------------------

        // A circular slam centred on the segment from..to (a point when they are the same). Caskets are pushed away
        // from the nearest point of it.
        // On a frozen-water floor (the Hoarfrost Basilica) every one of these also cracks the ice under it.
        public static void Slam(GameObject source, Vector3 from, Vector3 to, float radius)
        {
            CrackIce(source, from, to, radius);
            Report(source, Shape.Radial, from, to, radius, 0f);
        }

        // A frontal swing from `origin` along `direction`: caskets within `range` and `arcDegrees` are pushed away from the attacker.
        public static void Cone(GameObject source, Vector3 origin, Vector3 direction, float range, float arcDegrees)
        {
            // The ice cracks where the blow lands, in front of the attacker.
            Vector3 flat = Flat(direction).sqrMagnitude > 0.0001f ? Flat(direction).normalized : Vector3.forward;
            Vector3 impact = origin + flat * (range * 0.6f);
            CrackIce(source, impact, impact, Mathf.Min(range * 0.5f, 2f));
            Report(source, Shape.Cone, origin, direction, range, arcDegrees);
        }

        // Something moving from..to with a half-width `radius` (a shoved wall): caskets in its path are pushed along it.
        public static void Sweep(GameObject source, Vector3 from, Vector3 to, float radius)
        {
            CrackIce(source, from, to, radius);
            Report(source, Shape.Sweep, from, to, radius, 0f);
        }

        // Caskets only (DestructibleFloor.Slam breaks the floor itself).
        internal static void LaunchOnly(GameObject source, Vector3 from, Vector3 to, float radius) => Report(source, Shape.Radial, from, to, radius, 0f);

        static void CrackIce(GameObject source, Vector3 from, Vector3 to, float radius)
        {
            DestructibleFloor floor = DestructibleFloor.Instance;
            if (floor != null && floor.FrozenWater) DestructibleFloor.Crack(source, from, to, radius);
        }

        static void Report(GameObject source, Shape shape, Vector3 a, Vector3 b, float x, float y)
        {
            CasketField field = Instance;
            if (field == null || source == null || NetGame.InPrep || NetGame.Frozen) return;
            if (NetPlayer.IsReplicaObject(source)) return;

            if (Online && !NetworkManager.Singleton.IsServer)
            {
                using var writer = new FastBufferWriter(64, Allocator.Temp);
                writer.WriteValueSafe((byte)shape);
                writer.WriteValueSafe(a);
                writer.WriteValueSafe(b);
                writer.WriteValueSafe(x);
                writer.WriteValueSafe(y);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(SlamMessage, NetworkManager.ServerClientId, writer);
                return;
            }
            field.Resolve(source, shape, a, b, x, y);
        }

        // The owner of a carried character broke free (CasketCarried.Escape).
        public static void ReportEscape(GameObject character)
        {
            CasketField field = Instance;
            if (field == null || character == null) return;
            if (Online && !NetworkManager.Singleton.IsServer)
            {
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(true);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(EscapeMessage, NetworkManager.ServerClientId, writer);
                return;
            }
            field.ServerEscape(character);
        }

        void OnSlamMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte shapeByte);
            reader.ReadValueSafe(out Vector3 a);
            reader.ReadValueSafe(out Vector3 b);
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);

            NetPlayer from = PlayerOf(sender);
            if (from == null || from.Health.IsDead || NetGame.Frozen || NetGame.InPrep) return;
            var shape = (Shape)shapeByte;
            if (shape > Shape.Sweep || x <= 0f || x > MaxSlamRadius) return;
            Vector3 near = a - from.transform.position;
            near.y = 0f;
            if (near.magnitude > MaxSlamReach) return;
            if (shape != Shape.Cone)
            {
                Vector3 span = b - a;
                span.y = 0f;
                if (span.magnitude > MaxSlamLength) return;
            }
            Resolve(from.gameObject, shape, a, b, x, y);
        }

        void OnEscapeMessage(ulong sender, FastBufferReader reader)
        {
            NetPlayer from = PlayerOf(sender);
            if (from != null) ServerEscape(from.gameObject);
        }

        static NetPlayer PlayerOf(ulong clientId)
        {
            foreach (NetPlayer p in NetPlayer.All)
                if (p != null && p.OwnerClientId == clientId) return p;
            return null;
        }

        // ---- Server (or offline): which caskets a slam launches ----------------------------------------------------

        void Resolve(GameObject source, Shape shape, Vector3 a, Vector3 b, float x, float y)
        {
            if (caskets == null || !source.TryGetComponent(out Health attacker)) return;
            Vector3 sourcePos = Flat(source.transform.position);

            for (int i = 0; i < caskets.Length; i++)
            {
                ReliquaryCasket casket = caskets[i];
                if (casket == null || casket.Current != ReliquaryCasket.State.Resting) continue;
                Vector3 c = Flat(casket.Position);
                Vector3 push;

                switch (shape)
                {
                    case Shape.Radial:
                    {
                        Vector3 q = ClosestOnSegment(c, Flat(a), Flat(b));
                        if ((c - q).magnitude > x + ReliquaryCasket.HalfSize) continue;
                        push = c - q;
                        if (push.magnitude < 0.4f) push = c - sourcePos;
                        if (push.magnitude < 0.05f) push = Flat(source.transform.forward);
                        break;
                    }
                    case Shape.Cone:
                    {
                        Vector3 to = c - Flat(a);
                        float distance = to.magnitude;
                        if (distance > x + ReliquaryCasket.HalfSize) continue;
                        // A casket is struck if any part of it is inside the arc.
                        float slack = distance > 0.01f ? Mathf.Atan2(ReliquaryCasket.HalfSize, distance) * Mathf.Rad2Deg : 180f;
                        if (distance > 0.01f && Vector3.Angle(Flat(b), to) > y * 0.5f + slack) continue;
                        push = distance > 0.05f ? to : Flat(b);
                        break;
                    }
                    default:
                    {
                        Vector3 q = ClosestOnSegment(c, Flat(a), Flat(b));
                        if ((c - q).magnitude > x + ReliquaryCasket.HalfSize) continue;
                        push = Flat(b) - Flat(a);
                        if (push.magnitude < 0.05f) push = c - sourcePos;
                        break;
                    }
                }
                if (push.sqrMagnitude < 0.0001f) continue;
                Launch(i, push.normalized, source, attacker.Team);
            }
        }

        void Launch(int index, Vector3 direction, GameObject launcher, int team)
        {
            ReliquaryCasket casket = caskets[index];
            Vector3 start = casket.Position;
            Vector3 end = Flat(start) + direction * SlideDistance(casket, start, direction, out ReliquaryCasket blocker);
            end.y = start.y;

            runs[index] = new Run
            {
                launcher = launcher, team = team, fullEnd = end,
                chainTo = blocker != null ? System.Array.IndexOf(caskets, blocker) : -1,
                resolveAt = Time.time + Vector3.Distance(Flat(start), Flat(end)) / Speed + Grace
            };
            respawnAt[index] = -1f;
            casket.Launch(start, direction, end, Speed);
            Broadcast(Op.Launch, index, start, direction, end);
        }

        // How far the casket can slide before solid scenery stops it (characters and its own collider don't count).
        // `blocker` is the resting casket it would run into, if that is what stops it.
        static float SlideDistance(ReliquaryCasket casket, Vector3 start, Vector3 direction, out ReliquaryCasket blocker)
        {
            const float Back = 0.3f;     // cast from a little behind, so scenery already touching it is still found
            const float Radius = 0.55f;
            Vector3 origin = new Vector3(start.x, 0.65f, start.z) - direction * Back;
            float best = MaxSlide;
            blocker = null;
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, Radius, direction, MaxSlide + Back, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f || hit.collider.transform.IsChildOf(casket.transform)) continue;
                if (IsCharacter(hit.collider)) continue;
                if (hit.collider.bounds.max.y < 0.4f) continue; // floor
                // The sphere's front touches the obstacle; the casket's face is HalfSize ahead of its centre.
                float d = hit.distance - Back + Radius - ReliquaryCasket.HalfSize;
                if (d >= best) continue;
                best = d;
                blocker = hit.collider.GetComponentInParent<ReliquaryCasket>();
            }
            return Mathf.Max(0f, best);
        }

        // ---- Server (or offline): catching, escaping, impact ------------------------------------------------------

        void Update()
        {
            if (caskets == null || !Authority) return;
            for (int i = 0; i < caskets.Length; i++)
            {
                ReliquaryCasket casket = caskets[i];
                if (casket == null) continue;

                if (casket.Current == ReliquaryCasket.State.Gone)
                {
                    if (respawnAt[i] >= 0f && Time.time >= respawnAt[i] && !Occupied(casket.RestPosition))
                    {
                        respawnAt[i] = -1f;
                        casket.Restore(regrow: true);
                        Broadcast(Op.Restore, i, Vector3.zero, Vector3.zero, Vector3.zero);
                    }
                    continue;
                }

                Run run = runs[i];
                if (run == null || casket.Current == ReliquaryCasket.State.Resting) continue;

                if (run.victim != null && run.victim.IsDead)
                {
                    // Killed on the way: the casket goes on without them.
                    run.passed.Add(run.victim);
                    run.victim = null;
                    Release(i, run);
                }

                if (run.victim == null && casket.Current == ReliquaryCasket.State.Sliding) TryCatch(i, run);
                if (Time.time >= run.resolveAt) Impact(i, run);
            }
        }

        void TryCatch(int index, Run run)
        {
            ReliquaryCasket casket = caskets[index];
            Vector3 pos = Flat(casket.Position);
            Vector3 dir = casket.Direction;
            Vector3 right = Vector3.Cross(Vector3.up, dir);

            Health best = null;
            float bestAlong = float.PositiveInfinity;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure || h.Team == run.team || h.Invulnerable || run.passed.Contains(h)) continue;
                if (Timeslipped.IsActive(h.gameObject)) continue;
                if (Mathf.Abs(h.transform.position.y - casket.Position.y) > 1.6f) continue; // in the air (a leap)
                Vector3 offset = Flat(h.transform.position) - pos;
                float along = Vector3.Dot(offset, dir);
                float across = Mathf.Abs(Vector3.Dot(offset, right));
                if (along < -0.3f || along > ReliquaryCasket.HalfSize + 0.55f || across > ReliquaryCasket.HalfSize + 0.45f) continue;
                if (along >= bestAlong) continue;
                bestAlong = along;
                best = h;
            }
            if (best != null) Catch(index, run, best);
        }

        void Catch(int index, Run run, Health victim)
        {
            ReliquaryCasket casket = caskets[index];
            Vector3 dir = casket.Direction;
            run.victim = victim;
            GameObject go = victim.gameObject;

            run.immune = Knockback.IsImmune(go) || Enraged.IsEnraged(go) || Unstoppable.Is(go);
            if (run.immune)
            {
                // Like hitting a wall: the casket stops dead against them.
                SetEnd(index, casket.Position);
                run.resolveAt = Time.time + Grace;
                return;
            }

            float carry = Mathf.Min(casket.Remaining, WallDistance(victim, dir, casket.Remaining + 2f));
            // A little extra push so they are pressed into the wall rather than stopping just short of it.
            float shove = carry + 0.3f;
            float seconds = Mathf.Max(0.05f, shove / Speed);
            CasketCarried.Apply(go, dir, shove, seconds);
            SetEnd(index, Flat(casket.Position) + dir * carry);
            run.resolveAt = Time.time + seconds + Grace;
            Sfx.Play(Sfx.MeleeImpact, 0.5f, 0.8f, 0.9f);
        }

        // How far `victim` can be carried along `dir` before solid scenery (not characters) stops them.
        static float WallDistance(Health victim, Vector3 dir, float max)
        {
            Vector3 from = victim.transform.position + Vector3.up;
            float best = max;
            foreach (RaycastHit hit in Physics.SphereCastAll(from, 0.35f, dir, max, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f || hit.transform.IsChildOf(victim.transform)) continue;
                if (IsCharacter(hit.collider)) continue;
                if (hit.collider.bounds.max.y - victim.transform.position.y < 0.6f) continue;
                if (hit.distance < best) best = hit.distance;
            }
            return best;
        }

        // Server: the carried character escaped. Accepted until the impact is resolved.
        public void ServerEscape(GameObject character)
        {
            if (!Authority || caskets == null) return;
            for (int i = 0; i < caskets.Length; i++)
            {
                Run run = runs[i];
                if (run == null || run.immune || run.victim == null || run.victim.gameObject != character) continue;
                if (caskets[i].Current != ReliquaryCasket.State.Sliding && caskets[i].Current != ReliquaryCasket.State.Stopped) continue;
                run.passed.Add(run.victim);
                run.victim = null;
                CasketCarried.Clear(character); // the server's copy stops sliding too
                Release(i, run);
                return;
            }
        }

        // Nobody is pinned any more: slide on to the end of the original path.
        void Release(int index, Run run)
        {
            ReliquaryCasket casket = caskets[index];
            SetEnd(index, run.fullEnd);
            run.resolveAt = Time.time + Vector3.Distance(Flat(casket.Position), Flat(run.fullEnd)) / Speed + Grace;
        }

        void Impact(int index, Run run)
        {
            ReliquaryCasket casket = caskets[index];
            // Still travelling (this machine's frame ran late): wait for it to arrive.
            if (casket.Current == ReliquaryCasket.State.Sliding && casket.Remaining > 0.05f) return;

            Health victim = run.victim;
            runs[index] = null;
            if (victim != null && !victim.IsDead)
            {
                GameObject go = victim.gameObject;
                CasketCarried.Clear(go);
                victim.TakeDamage(ImpactDamage, run.launcher);
                if (!run.immune) Stunned.Apply(go, ImpactStun); // hands the stun to the victim's machine online
            }
            // The crash cracks the ice where it ends (frozen-water floor only).
            if (DestructibleFloor.Instance != null && DestructibleFloor.Instance.FrozenWater)
                DestructibleFloor.ServerCrackAt(casket.Position + casket.Direction * ReliquaryCasket.HalfSize, 1.4f);
            casket.Shatter();
            respawnAt[index] = Time.time + RespawnAfter;
            Broadcast(Op.Shatter, index, Vector3.zero, Vector3.zero, Vector3.zero);

            // A free slide that ran into another reliquary knocks that one on, in the same direction (credited to the
            // same slammer), so a lane blocked by a casket still carries across the map.
            bool reachedEnd = Vector3.Distance(Flat(casket.Position), Flat(run.fullEnd)) < 0.1f;
            if (victim == null && reachedEnd && run.chainTo >= 0 && caskets[run.chainTo] != null
                && caskets[run.chainTo].Current == ReliquaryCasket.State.Resting)
                Launch(run.chainTo, casket.Direction, run.launcher, run.team);
        }

        void SetEnd(int index, Vector3 end)
        {
            end.y = caskets[index].Position.y;
            caskets[index].Retarget(end);
            Broadcast(Op.Retarget, index, end, Vector3.zero, Vector3.zero);
        }

        static bool Occupied(Vector3 at)
        {
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure) continue;
                if ((Flat(h.transform.position) - Flat(at)).magnitude < ReliquaryCasket.HalfSize + 0.6f) return true;
            }
            return false;
        }

        // Round start, rematch, offline T: every casket back on its plinth, nothing sliding, nobody pinned.
        public void ResetAll()
        {
            if (caskets == null) return;
            for (int i = 0; i < caskets.Length; i++)
            {
                Run run = runs[i];
                if (run != null && run.victim != null) CasketCarried.Clear(run.victim.gameObject);
                runs[i] = null;
                respawnAt[i] = -1f;
                if (caskets[i] != null) caskets[i].Restore();
            }
        }

        // ---- Server -> everyone ------------------------------------------------------------------------------------

        void Broadcast(Op op, int index, Vector3 a, Vector3 b, Vector3 c)
        {
            if (!Online || !NetworkManager.Singleton.IsServer) return;
            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe((byte)op);
            writer.WriteValueSafe((byte)index);
            writer.WriteValueSafe(a);
            writer.WriteValueSafe(b);
            writer.WriteValueSafe(c);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(StateMessage, writer);
        }

        void OnStateMessage(ulong sender, FastBufferReader reader)
        {
            if (NetworkManager.Singleton.IsServer) return; // the server changed its own copy already
            reader.ReadValueSafe(out byte opByte);
            reader.ReadValueSafe(out byte index);
            reader.ReadValueSafe(out Vector3 a);
            reader.ReadValueSafe(out Vector3 b);
            reader.ReadValueSafe(out Vector3 c);
            if (caskets == null || index >= caskets.Length || caskets[index] == null) return;
            ReliquaryCasket casket = caskets[index];
            switch ((Op)opByte)
            {
                case Op.Launch: casket.Launch(a, b, c, Speed); break;
                case Op.Retarget: casket.Retarget(a); break;
                case Op.Shatter: casket.Shatter(); break;
                case Op.Restore: casket.Restore(regrow: true); break;
            }
        }

        static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            if (len < 0.0001f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
            return a + ab * t;
        }

        // A character (passes through for scenery purposes). Structures such as a planted Living Wall count as walls.
        static bool IsCharacter(Collider c)
        {
            Health h = c.GetComponentInParent<Health>();
            return h != null && !h.Structure;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
