using System.Collections.Generic;
using Anathema.Abilities.Psyker;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // RMB: hold to charge the arquebus, release to fire one huge piercing shot along the aim line. The longer it is
    // charged the harder it hits and the longer it stuns every enemy in the line (up to maxStun at full charge).
    // The Ranger is planted while charging. Stuns, silence, panic and knockback (and C or death) break the charge with no shot.
    // Online, the owner's press cue starts the charge on other machines and a second cue (the EX flag) means "released".
    public class TransuranicShot : Ability
    {
        [SerializeField] float chargeTime = 1.6f;
        [SerializeField] float minDamage = 18f;
        [SerializeField] float maxDamage = 40f;
        [SerializeField] float minStun = 0.25f;
        [SerializeField] float maxStun = 1.5f;
        [SerializeField] float range = 34f;
        [SerializeField] float shotRadius = 0.45f;
        [SerializeField] float boltSpeed = 54f; // units/s; slow enough to dodge or break line of sight once the shot is away
        [SerializeField] float energyPerHit = 25f;
        [SerializeField] float muzzleHeight = 1.3f;
        [SerializeField] float muzzleForward = 1.6f;
        [Header("EX: Overcharge Round")]
        [SerializeField] float exEnergyCost = 40f;
        [SerializeField, Range(0f, 1f)] float overchargeCharge = 0.6f; // fraction of a full charge the instant shot carries

        Energy energy;
        bool charging;
        float chargeStart;
        int cueIndex = -1;
        readonly List<Health> hits = new List<Health>();

        GameObject chargeFx;
        LineRenderer sight;
        Material sightMaterial, orbMaterial;
        Transform orb;

        protected override bool CooldownStartsOnStrike => true;

        public override string DisplayName => "Transuranic Shot";
        public override string KeyLabel => "RMB";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Overcharge Round";
        public override string ExDescription => "Overcharge Round";
        // Planted while the shot is charging.
        public override bool RootsMovement => charging;

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 6f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        void OnDestroy()
        {
            DestroyFx();
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButtonDown(1);
        }

        float Charge01 => charging ? Mathf.Clamp01((Time.time - chargeStart) / chargeTime) : 0f;

        protected override void Activate()
        {
            // On another machine an EX cue is either the owner letting go of a charge, or (when nothing is charging)
            // an Overcharge Round: an instant shot at a fixed partial charge, with no planted wind-up.
            if (IsReplica && ExActivation)
            {
                if (charging) Fire(false);
                else Fire(false, FlatAim(), overchargeCharge, true);
                return;
            }

            if (!IsReplica)
            {
                ExActivation = TrySpendEx(energy, exEnergyCost);
                if (ExActivation)
                {
                    Fire(true, FlatAim(), overchargeCharge, true);
                    return;
                }
            }

            charging = true;
            chargeStart = Time.time;
            BuildFx();
        }

        Vector3 FlatAim()
        {
            Vector3 d = Aim.AimDirection;
            d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
        }

        protected override void Update()
        {
            base.Update();
            if (!charging) return;

            if (Health.IsDead) return; // Cancel() has already run

            if (IsReplica)
            {
                if (Time.time - chargeStart > chargeTime + 5f) Cancel(); // lost the release cue
            }
            else
            {
                if (Stunned.IsStunned(gameObject) || Silenced.IsSilenced(gameObject) || Panicked.IsPanicked(gameObject)
                    || GetComponent<Knockback>() != null)
                {
                    Cancel();
                    if (Health.Net != null && Health.Net.IsSpawned) Health.Net.SendCue(-1, Aim.AimPoint);
                    return;
                }


                if (!Input.GetMouseButton(1))
                {
                    if (Health.Net != null && Health.Net.IsSpawned)
                    {
                        if (cueIndex < 0) cueIndex = System.Array.IndexOf(GetComponents<Ability>(), this);
                        Health.Net.SendCue(cueIndex + ExCueOffset, Aim.AimPoint);
                    }
                    Fire(true);
                    return;
                }
            }

            RangerPose.For(gameObject).Aim(0.15f, false);
            UpdateFx();
        }

        Vector3 Muzzle(Vector3 dir) => transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;

        // Counter payoff: an instant shot at `charge01` of full charge toward `dir`, leaving this ability's own
        // state (cooldown, an in-progress charge) alone. Owner only.
        public void CounterShot(Vector3 dir, float charge01)
        {
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
            Fire(true, dir, charge01, false);
        }

        void Fire(bool real)
        {
            float t = Charge01;
            charging = false;
            DestroyFx();
            Fire(real, FlatAim(), t, true);
        }

        // The shot. `real` is the owner's: it deals damage; a replica just draws it.
        void Fire(bool real, Vector3 dir, float t, bool startsCooldown)
        {
            Vector3 start = Muzzle(dir);
            // The line only finds where walls stop the shot; the damage comes from the bolt flying down it.
            Vector3 end = RangerFx.Trace(transform, Health.Team, start, dir, range, shotRadius, null);
            float flight = Vector3.Distance(start, end) / boltSpeed;

            // A faint beam marks the path (a warning to dodge across); the bolt itself is the threat.
            RangerFx.Tracer(start, end, Mathf.Lerp(0.18f, 0.6f, t), RangerFx.Cyan, flight + 0.15f, 0.3f);
            PsykerFx.Ring(transform.position + dir * muzzleForward, 0.2f, Mathf.Lerp(0.8f, 2.4f, t), 0.25f, RangerFx.Cyan);

            RangerPose.For(gameObject).Aim(0.3f, false); // hold the aim through the recoil
            // A fuller charge is a heavier shot: louder and deeper.
            Sfx.Play(Sfx.Longrifle, Mathf.Lerp(0.45f, 0.65f, t), Mathf.Lerp(0.95f, 0.72f, t), Mathf.Lerp(1f, 0.78f, t));

            float damage = Mathf.Lerp(minDamage, maxDamage, t);
            float stun = Mathf.Lerp(minStun, maxStun, t);
            var bolt = new GameObject("Transuranic Bolt").AddComponent<TransuranicBolt>();
            bolt.Init(real ? gameObject : null, Health.Team, start, end, boltSpeed, shotRadius, t,
                real ? (System.Action<Health>)(target =>
                {
                    // The stun rides on the hit: if a counter (or anything else) negates the shot, there is no stun.
                    target.Strike(Hit.Direct(damage).WithStun(stun), gameObject, landed =>
                    {
                        if (!landed) return;
                        HitMarkerSound.Play();
                        if (energy != null) energy.Gain(energyPerHit);
                    });
                }) : null);

            if (real && startsCooldown) StartCooldown();
        }

        // The travelling piercing bolt. It sweeps a capsule along its path each frame and hits every enemy once.
        // `onHit` is null on replicas, which only draw it.
        sealed class TransuranicBolt : MonoBehaviour
        {
            Vector3 pos, dir, end;
            float speed, radius;
            int team;
            System.Action<Health> onHit;
            readonly HashSet<Health> struck = new HashSet<Health>();
            Material coreMaterial, haloMaterial;

            public void Init(GameObject owner, int ownerTeam, Vector3 from, Vector3 to, float s, float r, float charge01, System.Action<Health> hit)
            {
                team = ownerTeam;
                pos = from;
                end = to;
                dir = (to - from).normalized;
                speed = s;
                radius = r;
                onHit = hit;
                transform.position = from;

                float size = Mathf.Lerp(0.25f, 0.55f, charge01);
                coreMaterial = VisualUtil.UnlitMaterial(new Color(0.9f, 1f, 1f, 1f), 3002);
                haloMaterial = VisualUtil.UnlitMaterial(new Color(RangerFx.Cyan.r, RangerFx.Cyan.g, RangerFx.Cyan.b, 0.45f), 3001);
                AddSphere("Core", size, coreMaterial);
                AddSphere("Halo", size * 2.4f, haloMaterial);

                TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.2f;
                trail.startWidth = size;
                trail.endWidth = 0f;
                trail.sharedMaterial = haloMaterial;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            void AddSphere(string name, float scale, Material m)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = name;
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(transform, false);
                s.transform.localScale = Vector3.one * scale;
                s.GetComponent<Renderer>().sharedMaterial = m;
                s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            void Update()
            {
                float step = Mathf.Min(speed * Time.deltaTime, Vector3.Distance(pos, end));
                Vector3 next = pos + dir * step;

                if (onHit != null)
                {
                    foreach (Collider c in Physics.OverlapCapsule(pos, next, radius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Health target = c.GetComponentInParent<Health>();
                        if (target == null || target.Team == team || target.IsDead || !struck.Add(target)) continue;
                        onHit(target);
                    }
                }

                pos = next;
                transform.position = pos;
                if ((end - pos).sqrMagnitude < 0.0001f) Destroy(gameObject, 0.25f); // let the trail finish
            }

            void OnDestroy()
            {
                if (coreMaterial != null) Destroy(coreMaterial);
                if (haloMaterial != null) Destroy(haloMaterial);
            }
        }

        protected override void Cancel()
        {
            if (!charging) return;
            charging = false;
            DestroyFx();
            RangerPose.For(gameObject).Release();
            if (!IsReplica) RefundCooldown();
        }

        // ---- charge telegraph: a growing orb on the muzzle and a sight line that sharpens as the shot fills --------------

        void BuildFx()
        {
            DestroyFx();
            chargeFx = new GameObject("Transuranic Charge");

            sightMaterial = VisualUtil.UnlitMaterial(new Color(RangerFx.Cyan.r, RangerFx.Cyan.g, RangerFx.Cyan.b, 0.15f), 3000);
            var lineObject = new GameObject("Sight");
            lineObject.transform.SetParent(chargeFx.transform, false);
            sight = lineObject.AddComponent<LineRenderer>();
            sight.useWorldSpace = true;
            sight.positionCount = 2;
            sight.sharedMaterial = sightMaterial;
            sight.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            orbMaterial = VisualUtil.UnlitMaterial(new Color(0.8f, 1f, 1f, 0.8f), 3001);
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Charge Orb";
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = orbMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.transform.SetParent(chargeFx.transform, false);
            orb = s.transform;
            UpdateFx();
        }

        void UpdateFx()
        {
            if (chargeFx == null) return;
            float t = Charge01;
            Vector3 dir = FlatAim();
            Vector3 start = Muzzle(dir);
            Vector3 end = RangerFx.Trace(transform, Health.Team, start, dir, range, 0.1f, null);

            sight.SetPosition(0, start);
            sight.SetPosition(1, end);
            sight.startWidth = sight.endWidth = Mathf.Lerp(0.03f, 0.2f, t);
            sightMaterial.color = new Color(RangerFx.Cyan.r, RangerFx.Cyan.g, RangerFx.Cyan.b, Mathf.Lerp(0.1f, 0.65f, t));

            float pulse = t >= 1f ? 1f + 0.2f * Mathf.Sin(Time.time * 30f) : 1f;
            orb.position = start;
            orb.localScale = Vector3.one * (Mathf.Lerp(0.08f, 0.5f, t) * pulse);
        }

        void DestroyFx()
        {
            if (chargeFx != null) Destroy(chargeFx);
            chargeFx = null;
            if (sightMaterial != null) Destroy(sightMaterial);
            if (orbMaterial != null) Destroy(orbMaterial);
        }
    }
}
