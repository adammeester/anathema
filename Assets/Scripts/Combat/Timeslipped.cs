using System.Collections.Generic;
using Anathema.Abilities;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: the Thousand Sons Sorcerer's ultimate, Weaver of Fates, pulls a teammate out of time. For `seconds`
    // they are gone: hidden, untouchable and unable to move or act (it counts as a stun, see Stunned.IsStunned). Their
    // cooldowns are cut as they vanish. When it ends they step back in wherever they are aiming, up to `range` from where
    // they vanished, and every enemy around the spot is stunned.
    //
    // Online: the caster asks the server (a supportive status). The server's copy makes them invulnerable, lets the
    // owner report the arrival as an ability move, and tells the owner; the owner hides, cuts their cooldowns, picks the
    // spot and stuns the enemies there. The owner publishes it in its status mask, so other screens hide the body too.
    public class Timeslipped : MonoBehaviour
    {
        // Arrival settings travel in the status's direction vector.
        public struct Arrival
        {
            public float CooldownCut;   // share of every cooldown still running that is taken off (0.25 = 25%)
            public float StunSeconds;
            public float StunRadius;
            public bool Warpfire;       // Fires of Tzeentch: a pool of warpfire where they step back in

            // The flag rides on the cooldown share (always 0-1) so the whole thing still fits one status vector.
            public Vector3 Pack() => new Vector3(Mathf.Clamp01(CooldownCut) + (Warpfire ? 10f : 0f), StunSeconds, StunRadius);
            public static Arrival Unpack(Vector3 v) => new Arrival
            {
                CooldownCut = v.x >= 10f ? v.x - 10f : v.x,
                Warpfire = v.x >= 10f,
                StunSeconds = v.y,
                StunRadius = v.z
            };
        }

        static readonly Color Warp = new Color(0.35f, 0.8f, 1f, 0.9f);
        static readonly Color Sand = new Color(0.95f, 0.78f, 0.38f, 0.9f);

        // The server's copy ends this much early, so the owner's arrival move is never refused for being "stunned".
        const float ServerLead = 0.12f;

        float endTime;
        float range;
        Arrival arrival;
        Vector3 origin;
        bool authority;
        bool owner;
        bool visualOnly;
        bool madeInvulnerable;
        Health health;
        readonly List<Renderer> hidden = new List<Renderer>();
        LineRenderer marker;
        Material markerMaterial;

        // The caster's side: send the teammate (or yourself) out of time.
        public static void Begin(GameObject target, GameObject caster, float seconds, float range, Arrival arrival)
        {
            if (target == null || !target.TryGetComponent(out Health h) || h.IsDead) return;
            if (!h.IsAuthority)
            {
                if (h.Net != null) h.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Timeslip, seconds, range, arrival.Pack(), caster);
                return;
            }
            bool isOwner = h.Net == null || !h.Net.IsSpawned || h.Net.IsOwner;
            ApplyHere(target, seconds, range, arrival, true, isOwner);
            if (!isOwner) h.Net.ForwardStatusToOwner(Anathema.Net.NetPlayer.StatusKind.Timeslip, seconds, range, arrival.Pack(), caster);
        }

        // Runs on the server's copy (authority) and/or the owner's machine.
        public static void ApplyHere(GameObject target, float seconds, float range, Arrival arrival, bool authority, bool owner)
        {
            if (target == null || !target.TryGetComponent(out Health h) || h.IsDead) return;
            if (target.TryGetComponent(out Timeslipped existing)) Destroy(existing);

            var t = target.AddComponent<Timeslipped>();
            t.health = h;
            t.authority = authority;
            t.owner = owner;
            t.range = range;
            t.arrival = arrival;
            t.origin = target.transform.position;
            t.endTime = Time.time + seconds - (authority && !owner ? ServerLead : 0f);

            if (authority && !h.Invulnerable)
            {
                h.Invulnerable = true;
                t.madeInvulnerable = true;
            }
            // Server: the owner will report the arrival as a jump.
            if (authority && !owner && target.TryGetComponent(out PlayerMovement movement))
                movement.GrantAbilityMove(range + 1f, seconds + 1.5f);

            if (owner)
            {
                foreach (Ability a in target.GetComponents<Ability>())
                {
                    a.InterruptForGuard();
                    a.ReduceCooldown(a.CooldownRemaining * Mathf.Clamp01(arrival.CooldownCut));
                }
                t.BuildMarker();
            }
            t.Vanish();
        }

        public static bool IsActive(GameObject target) => target != null && target.TryGetComponent(out Timeslipped _);

        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Timeslipped _)) return;
            var t = target.AddComponent<Timeslipped>();
            t.health = target.GetComponent<Health>();
            t.visualOnly = true;
            t.endTime = float.MaxValue;
            t.Vanish();
        }

        void Vanish()
        {
            Vector3 at = transform.position;
            Anathema.Abilities.Psyker.PsykerFx.Ring(at + Vector3.up * 0.1f, 1.6f, 0.2f, 0.35f, Warp, 0.2f);
            Anathema.Abilities.Psyker.PsykerFx.Ring(at + Vector3.up * 1.2f, 1.1f, 0.1f, 0.3f, Sand, 0.12f);
            Sfx.Play(Sfx.TeleportCrackle, 0.45f, 0.75f, 0.85f, 0.8f);
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is LineRenderer) continue;
                r.enabled = false;
                hidden.Add(r);
            }
        }

        // The owner sees where they will step back in: a ring following the cursor, held within range.
        void BuildMarker()
        {
            var go = new GameObject("Timeslip Marker");
            marker = go.AddComponent<LineRenderer>();
            markerMaterial = VisualUtil.UnlitMaterial(Warp, 3002);
            marker.sharedMaterial = markerMaterial;
            marker.useWorldSpace = true;
            marker.loop = true;
            marker.positionCount = 32;
            marker.startWidth = marker.endWidth = 0.1f;
            marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Vector3 Destination()
        {
            var aim = GetComponent<MouseAim>();
            Vector3 point = aim != null ? aim.AimPoint : transform.position;
            Vector3 dir = point - origin;
            dir.y = 0f;
            float distance = Mathf.Min(range, dir.magnitude);
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
            distance = LandingSpot.Clear(transform, origin, dir, distance);
            Vector3 to = origin + dir * distance;
            to.y = transform.position.y;
            return to;
        }

        void Update()
        {
            if (health == null || health.IsDead)
            {
                Destroy(this);
                return;
            }
            if (marker != null)
            {
                Vector3 c = Destination() + Vector3.up * 0.08f;
                float r = Mathf.Max(0.5f, arrival.StunRadius) * (1f + 0.06f * Mathf.Sin(Time.time * 8f));
                for (int i = 0; i < marker.positionCount; i++)
                {
                    float a = i * Mathf.PI * 2f / marker.positionCount;
                    marker.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                }
            }
            if (!visualOnly && Time.time >= endTime) Arrive();
        }

        void Arrive()
        {
            if (owner)
            {
                Vector3 to = Destination();
                MovementSim.Place(GetComponent<CharacterController>(), transform, to);
                foreach (Health h in FindObjectsByType<Health>())
                {
                    if (h.IsDead || h.Structure || h.Team == health.Team) continue;
                    Vector3 d = h.transform.position - to;
                    d.y = 0f;
                    if (d.magnitude <= arrival.StunRadius) Stunned.Apply(h.gameObject, arrival.StunSeconds);
                }
                PlayArrival(to, arrival.StunRadius);
                if (arrival.Warpfire)
                {
                    SpawnWarpfire(gameObject, to, true);
                    if (health.Net != null && health.Net.IsSpawned) health.Net.SendWarpfire(to);
                }
            }
            Destroy(this);
        }

        // Fires of Tzeentch: blue and pink warpfire where the timeslipped character stepped back in. It burns that
        // character's enemies; only the owner's copy deals the damage, every other machine draws it.
        public static void SpawnWarpfire(GameObject character, Vector3 at, bool dealsDamage)
        {
            if (character == null || !character.TryGetComponent(out Health h)) return;
            Anathema.Abilities.Psyker.FirePool.Spawn(character, h.Team, at, Anathema.Abilities.Sorcerer.SorcererUpgrade.WarpfireRadius,
                Anathema.Abilities.Sorcerer.SorcererUpgrade.WarpfireSeconds, Anathema.Abilities.Sorcerer.SorcererUpgrade.WarpfireDamagePerSecond,
                dealsDamage, Anathema.Abilities.Psyker.FirePool.Tzeentch);
        }

        public static void PlayArrival(Vector3 at, float radius)
        {
            Anathema.Abilities.Psyker.PsykerFx.Ring(at + Vector3.up * 0.1f, 0.2f, Mathf.Max(1f, radius), 0.35f, Warp, 0.3f);
            Anathema.Abilities.Psyker.PsykerFx.Ring(at + Vector3.up * 0.15f, 0.2f, Mathf.Max(1f, radius) * 0.75f, 0.45f, Sand, 0.2f);
            Sfx.Play(Sfx.SlamExplosion, 0.45f, 1.2f, 1.35f, 0.6f);
            Sfx.Play(Sfx.TeleportCrackle, 0.4f, 1.1f, 1.2f, 0.6f);
        }

        void OnDestroy()
        {
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            if (madeInvulnerable && health != null) health.Invulnerable = false;
            if (marker != null) Destroy(marker.gameObject);
            if (markerMaterial != null) Destroy(markerMaterial);
            // Other screens see the body come back (the owner played its own arrival).
            if (visualOnly && health != null && !health.IsDead) PlayArrival(transform.position, 2.5f);
        }
    }
}
