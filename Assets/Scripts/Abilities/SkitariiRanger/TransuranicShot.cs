using System.Collections.Generic;
using Anathema.Abilities.Psyker;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // RMB: hold to charge the arquebus, release to fire one huge piercing shot along the aim line. The longer it is
    // charged the harder it hits and the longer it stuns every enemy in the line (up to maxStun at full charge).
    // The Ranger is planted while charging; a cast bar over her head fills with the charge. Held at full charge for
    // autoReleaseDelay seconds, the shot fires by itself (the bar flashes amber to red and a fuse strip drains first).
    // Stuns, silence, panic and knockback (and C or death) break the charge with no shot.
    // Online, the owner's press cue starts the charge on other machines and a second cue (the EX flag) means "released".
    public class TransuranicShot : Ability, IKnockbackImmune
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
        [SerializeField] float autoReleaseDelay = 2f; // held at full charge this long, the shot goes off by itself
        [Header("Cast bar")]
        [SerializeField] float barHeight = 2.85f; // just above the health bar
        [SerializeField] float barWidth = 1.6f;
        [SerializeField] float barThickness = 0.12f;
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
        Transform bar, barFill, barFuse;
        Material barBorderMaterial, barBackMaterial, barFillMaterial, barFuseMaterial;
        Camera cam;

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

        // Quickened Capacitors shortens the charge (on every machine, so the replica's telegraph keeps pace).
        float ChargeTime => chargeTime * (RangerUpgrade.Has(gameObject, RangerUpgrade.QuickenedCapacitors) ? RangerUpgrade.QuickenedChargeScale : 1f);

        float Charge01 => charging ? Mathf.Clamp01((Time.time - chargeStart) / ChargeTime) : 0f;

        // 0 until the charge is full, then rises to 1 over autoReleaseDelay (1 = the shot goes off on its own).
        float Overheld01 => charging ? Mathf.Clamp01((Time.time - chargeStart - ChargeTime) / Mathf.Max(0.01f, autoReleaseDelay)) : 0f;

        // Augmetic Bracing: planted while charging, so knockback can't shove her (or break the charge).
        public bool ImmuneToKnockback => charging && RangerUpgrade.Has(gameObject, RangerUpgrade.AugmeticBracing);

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
                if (Time.time - chargeStart > ChargeTime + 5f) Cancel(); // lost the release cue
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


                // Let go, or held at full charge too long: the shot goes off either way.
                if (!Input.GetMouseButton(1) || Time.time - chargeStart >= ChargeTime + autoReleaseDelay)
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
            bool full = t >= RangerUpgrade.FullCharge;
            bool executioner = full && RangerUpgrade.Has(gameObject, RangerUpgrade.ExecutionersRound);
            bool irradiate = full && RangerUpgrade.Has(gameObject, RangerUpgrade.RadSaturation);
            bool overpenetrate = RangerUpgrade.Has(gameObject, RangerUpgrade.Overpenetration);
            int pierced = 0; // enemies this bolt has already gone through
            var bolt = new GameObject("Transuranic Bolt").AddComponent<TransuranicBolt>();
            bolt.Init(real ? gameObject : null, Health.Team, start, end, boltSpeed, shotRadius, t,
                real ? (System.Action<Health>)(target =>
                {
                    float scale = 1f;
                    if (overpenetrate) scale += RangerUpgrade.OverpenetrationPerTarget * pierced;
                    pierced++;
                    if (executioner && target.Max > 0f && target.Current < target.Max * RangerUpgrade.ExecutionerThreshold)
                        scale += RangerUpgrade.ExecutionerBonus;

                    // The stun rides on the hit: if a counter (or anything else) negates the shot, there is no stun.
                    Hit hit = Hit.Direct(damage * scale).WithStun(stun);
                    if (irradiate) hit = hit.WithIrradiate(RangerUpgrade.RadSaturationBonus, RangerUpgrade.RadSaturationSeconds);
                    target.Strike(hit, gameObject, landed =>
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
            BuildBar();
            UpdateFx();
        }

        // Cast bar: a billboarded strip over her head (parented to her so it follows), with a thin fuse strip beneath
        // it that only appears once the charge is full and drains toward the automatic release.
        void BuildBar()
        {
            bar = new GameObject("Transuranic Cast Bar").transform;
            bar.SetParent(transform, false);
            bar.localPosition = new Vector3(0f, barHeight, 0f);

            const float border = 0.03f;
            barBorderMaterial = VisualUtil.UnlitMaterial(new Color(0.86f, 0.70f, 0.30f), 3000);
            barBackMaterial = VisualUtil.UnlitMaterial(new Color(0.05f, 0.05f, 0.05f, 0.85f), 3001);
            barFillMaterial = VisualUtil.UnlitMaterial(RangerFx.Cyan, 3002);
            barFuseMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.55f, 0.1f), 3002);
            BarQuad("Border", barBorderMaterial, 0f, 0f, barWidth + border * 2f, barThickness + border * 2f);
            BarQuad("Back", barBackMaterial, 0f, -0.005f, barWidth, barThickness);
            barFill = BarQuad("Fill", barFillMaterial, 0f, -0.01f, barWidth, barThickness);
            barFuse = BarQuad("Fuse", barFuseMaterial, -(barThickness * 0.5f + border + 0.05f), -0.01f, barWidth, 0.05f);
        }

        Transform BarQuad(string quadName, Material m, float y, float z, float w, float h)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = quadName;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(bar, false);
            q.transform.localPosition = new Vector3(0f, y, z);
            q.transform.localScale = new Vector3(w, h, 1f);
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q.transform;
        }

        // Left-anchored horizontal fill of `t` (0..1).
        void SetBarFill(Transform q, float t)
        {
            q.localScale = new Vector3(barWidth * t, q.localScale.y, 1f);
            Vector3 p = q.localPosition;
            q.localPosition = new Vector3(-barWidth * 0.5f * (1f - t), p.y, p.z);
        }

        void UpdateBar(float t, float over)
        {
            if (bar == null) return;
            if (cam == null) cam = Camera.main;
            if (cam != null) bar.rotation = cam.transform.rotation;

            SetBarFill(barFill, t);
            barFuse.gameObject.SetActive(t >= 1f);

            if (t < 1f)
            {
                barFillMaterial.color = RangerFx.Cyan;
                bar.localScale = Vector3.one;
                return;
            }

            // Full: the fuse drains, and the bar flashes ever faster from amber to red as the auto-release nears.
            SetBarFill(barFuse, 1f - over);
            float hz = Mathf.Lerp(3f, 14f, over);
            float flash = 0.5f + 0.5f * Mathf.Sin(Time.time * hz * Mathf.PI * 2f);
            Color warn = Color.Lerp(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.15f, 0.1f), over);
            barFillMaterial.color = Color.Lerp(Color.white, warn, Mathf.Lerp(0.35f, 1f, flash));
            barFuseMaterial.color = warn;
            bar.localScale = Vector3.one * (1f + 0.08f * over * flash);
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

            float over = Overheld01;
            float pulse = t >= 1f ? 1f + Mathf.Lerp(0.2f, 0.4f, over) * Mathf.Sin(Time.time * Mathf.Lerp(30f, 60f, over)) : 1f;
            orb.position = start;
            orb.localScale = Vector3.one * (Mathf.Lerp(0.08f, 0.5f, t) * pulse);
            // The orb heats toward red as the auto-release nears.
            orbMaterial.color = Color.Lerp(new Color(0.8f, 1f, 1f, 0.8f), new Color(1f, 0.35f, 0.2f, 0.9f), over);

            UpdateBar(t, over);
        }

        void DestroyFx()
        {
            if (chargeFx != null) Destroy(chargeFx);
            chargeFx = null;
            if (sightMaterial != null) Destroy(sightMaterial);
            if (orbMaterial != null) Destroy(orbMaterial);
            if (bar != null) Destroy(bar.gameObject);
            bar = null;
            if (barBorderMaterial != null) Destroy(barBorderMaterial);
            if (barBackMaterial != null) Destroy(barBackMaterial);
            if (barFillMaterial != null) Destroy(barFillMaterial);
            if (barFuseMaterial != null) Destroy(barFuseMaterial);
        }
    }
}
