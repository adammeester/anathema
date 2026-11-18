using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // Space: Dust Form. The Sorcerer comes apart into a stream of sand and pours to the cursor, through walls and
    // characters alike, untouchable while he travels. Teammates he passes through are healed and their Mending blooms
    // at once; enemies he passes through have their Curse of the Rubricae set off. He can't end up inside a wall.
    //
    // The owner moves the character (reported to the server as an ability move); every copy hides the body and draws the
    // sand stream, and the server's copy is invulnerable for the trip.
    public class DustForm : Ability
    {
        [SerializeField] float range = 8f;
        [SerializeField] float minTravelTime = 0.5f;
        [SerializeField] float maxTravelTime = 0.65f;
        [SerializeField] float passRadius = 1.3f;
        [SerializeField] float allyHeal = 12f;

        CharacterController controller;
        bool travelling;
        Vector3 from, to;
        float start, duration;
        bool madeInvulnerable;
        readonly HashSet<Health> touched = new HashSet<Health>();
        readonly List<Renderer> hidden = new List<Renderer>();
        SandStream stream;

        public override string DisplayName => "Dust Form";
        public override string KeyLabel => "Space";
        public override float MovementRange => range;
        public override float MovementWindow => maxTravelTime + 1f;
        public override bool RootsMovement => travelling;

        void Reset()
        {
            cooldown = 10f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.Space) && !travelling;

        protected override void Activate()
        {
            from = transform.position;
            Vector3 dir = Aim.AimPoint - from;
            dir.y = 0f;
            float distance = Mathf.Min(range, dir.magnitude);
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
            distance = LandingSpot.Clear(transform, from, dir, distance);
            to = from + dir * distance;

            start = Time.time;
            duration = Mathf.Lerp(minTravelTime, maxTravelTime, range > 0f ? distance / range : 0f);
            travelling = true;
            touched.Clear();
            touched.Add(Health);

            if (Health.IsAuthority && !Health.Invulnerable)
            {
                Health.Invulnerable = true;
                madeInvulnerable = true;
            }
            if (!IsReplica && controller != null) controller.enabled = false;

            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is LineRenderer || r is TrailRenderer) continue;
                r.enabled = false;
                hidden.Add(r);
            }
            stream = new GameObject("Dust Form").AddComponent<SandStream>();
            stream.transform.position = from + Vector3.up;
            Anathema.Abilities.Psyker.PsykerFx.Ring(from + Vector3.up * 0.1f, 1.2f, 0.2f, 0.3f, SorcererFx.Sand, 0.15f);
            Sfx.Play(Sfx.DiseaseCloud, 0.35f, 1.4f, 1.6f, 0.7f);
        }

        protected override void Update()
        {
            base.Update();
            if (!travelling) return;

            float t = Mathf.Clamp01((Time.time - start) / Mathf.Max(0.01f, duration));
            float eased = t * t * (3f - 2f * t);
            Vector3 p = Vector3.Lerp(from, to, eased);

            if (!IsReplica)
            {
                transform.position = p;
                TouchAround(p);
            }
            if (stream != null) stream.transform.position = (IsReplica ? transform.position : p) + Vector3.up;

            if (t >= 1f) Finish();
        }

        // Owner: whoever the stream pours through.
        void TouchAround(Vector3 at)
        {
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure || touched.Contains(h)) continue;
                Vector3 d = h.transform.position - at;
                d.y = 0f;
                if (d.magnitude > passRadius) continue;
                touched.Add(h);
                if (h.Team == Health.Team)
                {
                    h.Heal(allyHeal, gameObject);
                    Mending.Bloom(h.gameObject, gameObject);
                    // Sandshell (upgrade): the sand hardens around both of them.
                    if (SorcererUpgrade.Has(gameObject, SorcererUpgrade.Sandshell))
                    {
                        Barrier.Grant(h.gameObject, SorcererUpgrade.SandshellAmount, SorcererUpgrade.SandshellSeconds, SorcererUpgrade.SandshellAmount, gameObject);
                        Barrier.Grant(gameObject, SorcererUpgrade.SandshellAmount, SorcererUpgrade.SandshellSeconds, SorcererUpgrade.SandshellAmount, gameObject);
                    }
                    Anathema.Abilities.Psyker.PsykerFx.Ring(h.transform.position + Vector3.up * 0.1f, 0.2f, 1f, 0.25f, Mending.Glow, 0.1f);
                }
                else
                {
                    RubricMark.Detonate(h.gameObject, gameObject);
                }
            }
        }

        void Finish()
        {
            if (!travelling) return;
            travelling = false;
            if (!IsReplica)
            {
                if (controller != null) controller.enabled = true;
                MovementSim.Place(controller, transform, to);
            }
            if (madeInvulnerable) Health.Invulnerable = false;
            madeInvulnerable = false;
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            if (stream != null) stream.Settle();
            stream = null;
            Vector3 at = IsReplica ? transform.position : to;
            Anathema.Abilities.Psyker.PsykerFx.Ring(at + Vector3.up * 0.1f, 0.2f, 1.4f, 0.3f, SorcererFx.Sand, 0.15f);
        }

        protected override void Cancel()
        {
            // A stun or C can't stop sand mid-pour, but death can: put everything back.
            if (travelling && Health.IsDead) Finish();
        }

        void OnDisable()
        {
            if (travelling) Finish();
        }

        // The stream of sand: grains swirling round a point that follows the traveller, scattering when he reforms.
        sealed class SandStream : MonoBehaviour
        {
            const int Count = 18;

            Transform[] grains;
            float[] phase;
            Material gold, blue;
            float settleStart = -1f;

            void Awake()
            {
                gold = VisualUtil.UnlitMaterial(SorcererFx.Sand, 3003);
                blue = VisualUtil.UnlitMaterial(SorcererFx.Warp, 3003);
                grains = new Transform[Count];
                phase = new float[Count];
                for (int i = 0; i < Count; i++)
                {
                    GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(g.GetComponent<Collider>());
                    g.GetComponent<Renderer>().sharedMaterial = i % 4 == 0 ? blue : gold;
                    g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    g.transform.SetParent(transform, false);
                    g.transform.localScale = Vector3.one * Random.Range(0.1f, 0.2f);
                    grains[i] = g.transform;
                    phase[i] = Random.value * 10f;
                }
                var trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.35f;
                trail.startWidth = 0.9f;
                trail.endWidth = 0f;
                trail.sharedMaterial = gold;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            public void Settle() => settleStart = Time.time;

            void Update()
            {
                float spread = 0.45f;
                if (settleStart >= 0f)
                {
                    float s = (Time.time - settleStart) / 0.4f;
                    if (s >= 1f)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    spread = Mathf.Lerp(0.45f, 1.6f, s);
                    foreach (Transform g in grains) g.localScale *= 0.94f;
                }
                for (int i = 0; i < grains.Length; i++)
                {
                    float a = Time.time * 9f + phase[i];
                    float h = Mathf.Sin(Time.time * 5f + phase[i]) * 0.7f;
                    grains[i].localPosition = new Vector3(Mathf.Cos(a) * spread, h, Mathf.Sin(a) * spread);
                }
            }

            void OnDestroy()
            {
                if (gold != null) Destroy(gold);
                if (blue != null) Destroy(blue);
            }
        }
    }
}
