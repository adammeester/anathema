using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // LMB (click or hold): the Bullgryn's weapon. Both weapons live here so the HUD slot, cues and prefab never change.
    //
    // Slab Maul (default): a slow, heavy swing in a wide frontal arc. Every third swing in a row that connects also
    // shoves what it hits.
    // Grenadier Gauntlet (round-1 Armament upgrade): lobs a grenade to the cursor, clamped between a minimum and maximum
    // range. A ring marks the landing spot and fills during the flight; it bursts for area damage on every enemy inside.
    // The grenade arcs over characters and cover.
    //
    // Grit: with a full Grit bar the next attack is empowered and spends every stack. The maul's Slab Slam hits harder
    // and stuns; the gauntlet's Krak Shell bursts wider and slows. Online, "empowered" travels as the cue's EX flag, so
    // every copy spends its Grit with the same swing.
    [DefaultExecutionOrder(50)] // after ProceduralWalk, which also writes the spine and arms every LateUpdate
    public class SlabMaul : Ability
    {
        [Header("Slab Maul")]
        [SerializeField] float damage = 13f;
        [SerializeField] float range = 2.6f;
        [SerializeField] float arcDegrees = 110f;
        [SerializeField] float windupDuration = 0.3f;
        [SerializeField] float swingDuration = 0.15f;
        [SerializeField] float recoverDuration = 0.3f;
        [SerializeField] float comboWindow = 2.5f;          // a swing later than this after the last starts the count over
        [SerializeField] float shoveDistance = 1.5f;        // every third connecting swing
        [SerializeField] float shoveDuration = 0.2f;
        [SerializeField] float slamDamageScale = 1.6f;      // Slab Slam (full Grit)
        [SerializeField] float slamStun = 0.75f;
        [SerializeField] float energyPerHit = 8f;

        [Header("Grenadier Gauntlet")]
        [SerializeField] float gauntletCooldown = 1.1f;
        [SerializeField] float gauntletWindup = 0.12f;
        [SerializeField] float minRange = 4f;
        [SerializeField] float maxRange = 11f;
        [SerializeField] float flightTime = 0.6f;
        [SerializeField] float blastRadius = 2.2f;
        [SerializeField] float grenadeDamage = 11f;
        [SerializeField] float krakRadius = 3.2f;           // Krak Shell (full Grit)
        [SerializeField] float krakSlow = 0.4f;
        [SerializeField] float krakSlowDuration = 1.5f;
        [SerializeField] float grenadeEnergyPerHit = 8f;

        [Header("Pose")]
        [SerializeField] float spineTwist = 30f;
        [SerializeField] float armRaise = 95f;

        static readonly string[] BoneNames = { "Spine", "UpperArm_R", "Forearm_R" };

        Energy energy;
        Grit grit;
        LimbPose pose;
        bool posed;
        bool active;
        bool strikePending;
        bool empowered;
        bool firingGauntlet;
        float strikeAt, swingEnd, recoverEnd, curWindup;
        Vector3 target;
        int connectStreak;
        float lastConnect = float.NegativeInfinity;

        bool Gauntlet => BullgrynUpgrade.HasGauntlet(gameObject);

        public override string DisplayName => Gauntlet ? "Grenadier" : "Slab Maul";
        public override string KeyLabel => "LMB";
        public override bool ShowsEmpowered => grit != null && grit.Full;

        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            grit = GetComponent<Grit>();
            pose = new LimbPose(transform.Find("Model"), BoneNames);
        }

        protected override bool IsTriggered()
        {
            if (PrimaryFireBlocked || BullgrynKit.Busy(gameObject)) return false;
            if (TryGetComponent(out BullCharge charge) && charge.IsCharging) return false;
            return Input.GetMouseButton(0);
        }

        protected override void Activate()
        {
            firingGauntlet = Gauntlet;
            // The owner decides whether this is the empowered attack; replicas are told through the cue's EX flag.
            if (!IsReplica) ExActivation = grit != null && grit.Full;
            empowered = ExActivation;
            if (empowered && grit != null) grit.Spend();

            curWindup = firingGauntlet ? gauntletWindup : windupDuration;
            strikeAt = Time.time + curWindup;
            swingEnd = strikeAt + swingDuration;
            recoverEnd = swingEnd + recoverDuration;
            strikePending = true;
            active = true;

            if (firingGauntlet)
            {
                target = LandingPoint();
                float radius = empowered ? krakRadius : blastRadius;
                BullgrynKit.GroundCircle(target, radius, curWindup + flightTime, empowered ? BullgrynKit.Hazard : BullgrynKit.Steel);
            }
        }

        // The cursor (the owner's aim from the cue on a replica), pulled in to the gauntlet's range band.
        Vector3 LandingPoint()
        {
            Vector3 here = transform.position;
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint : Aim.AimPoint;
            Vector3 to = aim - here;
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude < 0.0001f ? transform.forward : to.normalized;
            float dist = Mathf.Clamp(to.magnitude, minRange, maxRange);
            return new Vector3(here.x + dir.x * dist, here.y, here.z + dir.z * dist);
        }

        protected override void Cancel()
        {
            if (!active) return;
            if (strikePending) RefundCooldown();
            strikePending = false;
            active = false;
            recoverEnd = Time.time + recoverDuration;
        }

        protected override void Update()
        {
            // The gauntlet fires at its own rate (only changed while ready, so the HUD's cooldown sweep stays consistent).
            if (CooldownRemaining <= 0f && cooldown > 0f) CooldownScale = Gauntlet ? gauntletCooldown / cooldown : 1f;
            base.Update();
            if (strikePending && !IsReplica && IsDisabled) InterruptSelf();

            if (strikePending && Time.time >= strikeAt)
            {
                strikePending = false;
                if (firingGauntlet) Fire();
                else Swing();
            }
        }

        void Swing()
        {
            Vector3 dir = BullgrynKit.FlatAim(Aim.AimDirection, transform);
            if (Time.time - lastConnect > comboWindow) connectStreak = 0;
            bool shove = connectStreak == 2;

            SlashEffect.Spawn(transform.position, dir, range, arcDegrees, swingDuration * 2f, empowered ? BullgrynKit.Hazard : BullgrynKit.Power);
            MeleeSound.Swing(false, 0.45f, 0.7f, 0.8f);

            int hits = 0;
            if (!IsReplica)
            {
                foreach (Health enemy in BullgrynKit.EnemiesInCone(Health, transform.position, dir, range, arcDegrees))
                {
                    float dealt = damage * (empowered ? slamDamageScale : 1f);
                    Hit hit = Hit.Direct(dealt);
                    if (empowered) hit = hit.WithStun(slamStun);
                    if (shove || empowered)
                    {
                        Vector3 push = enemy.transform.position - transform.position;
                        push.y = 0f;
                        hit = hit.WithKnockback(push.sqrMagnitude > 0.0001f ? push.normalized : dir, shoveDistance, shoveDuration);
                    }
                    hits++;
                    enemy.Strike(hit, gameObject, landed =>
                    {
                        if (landed && energy != null) energy.Gain(energyPerHit);
                    });
                }
                if (hits > 0)
                {
                    connectStreak = shove ? 0 : connectStreak + 1;
                    lastConnect = Time.time;
                }
                else connectStreak = 0;
            }

            if (hits > 0 || IsReplica)
            {
                Sfx.Play(Sfx.HammerSmash, empowered ? 0.6f : 0.4f, empowered ? 0.75f : 0.95f, empowered ? 0.85f : 1.1f, 0.6f);
                if (hits > 0) MeleeSound.Impact(hits);
            }
            if (empowered) Anathema.Abilities.Psyker.PsykerFx.Ring(transform.position + dir * range * 0.6f, 0.3f, 1.8f, 0.3f, BullgrynKit.Hazard, 0.2f);
        }

        void Fire()
        {
            Vector3 muzzle = transform.position + Vector3.up * 1.3f - transform.right * 0.9f + transform.forward * 0.9f;
            bool krak = empowered;
            float radius = krak ? krakRadius : blastRadius;
            bool owner = !IsReplica;
            Sfx.Play(Sfx.HeavyBolterShot, 0.4f, 0.55f, 0.65f, 0.4f);
            GrenadeShell.Launch(muzzle, target, flightTime, krak, () => Burst(target, radius, krak, owner));
        }

        void Burst(Vector3 centre, float radius, bool krak, bool owner)
        {
            Anathema.Abilities.Psyker.PsykerFx.Ring(centre + Vector3.up * 0.1f, 0.2f, radius, 0.3f, krak ? BullgrynKit.Hazard : new Color(1f, 0.55f, 0.2f, 0.9f), 0.25f);
            Sfx.Play(Sfx.SlamExplosion, krak ? 0.55f : 0.4f, krak ? 0.8f : 1f, krak ? 0.9f : 1.15f, 0.8f);
            if (!owner || this == null || Health == null) return;

            bool marked = false;
            var struck = new System.Collections.Generic.List<Health>();
            foreach (Collider col in Physics.OverlapSphere(centre + Vector3.up, radius + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health enemy = col.GetComponentInParent<Health>();
                if (enemy == null || enemy.Team == Health.Team || enemy.IsDead || struck.Contains(enemy)) continue;
                Vector3 to = col.ClosestPoint(centre) - centre;
                to.y = 0f;
                if (to.magnitude > radius) continue;
                struck.Add(enemy);

                Hit blast = Hit.Direct(grenadeDamage).AsAoe();
                if (krak) blast = blast.WithSlow(krakSlowDuration, krakSlow);
                enemy.Strike(blast, gameObject, landed =>
                {
                    if (!landed) return;
                    if (energy != null) energy.Gain(grenadeEnergyPerHit);
                    if (marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }
        }

        void LateUpdate()
        {
            float now = Time.time;
            float twist, arm;

            if (firingGauntlet)
            {
                // A short recoil: the gauntlet arm lifts to aim, kicks, and settles.
                if (active && now < strikeAt) arm = Mathf.Lerp(0f, -50f, 1f - (strikeAt - now) / Mathf.Max(0.01f, curWindup));
                else if (now < recoverEnd)
                {
                    active = false;
                    arm = Mathf.Lerp(-65f, 0f, Mathf.Clamp01(1f - (recoverEnd - now) / Mathf.Max(0.01f, recoverEnd - strikeAt)));
                }
                else
                {
                    EndPose();
                    return;
                }
                posed = true;
                pose.Reset();
                pose.Rotate("UpperArm_R", transform.right, arm);
                return;
            }

            if (active && now < strikeAt)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - (strikeAt - now) / Mathf.Max(0.01f, curWindup));
                twist = Mathf.Lerp(0f, spineTwist, t);
                arm = Mathf.Lerp(0f, -armRaise, t);
            }
            else if (active && now < swingEnd)
            {
                float t = 1f - (swingEnd - now) / Mathf.Max(0.01f, swingEnd - strikeAt);
                twist = Mathf.Lerp(spineTwist, -spineTwist, t);
                arm = Mathf.Lerp(-armRaise, 20f, t);
            }
            else if (now < recoverEnd)
            {
                active = false;
                float t = Mathf.SmoothStep(0f, 1f, 1f - (recoverEnd - now) / Mathf.Max(0.01f, recoverDuration));
                twist = Mathf.Lerp(-spineTwist, 0f, t);
                arm = Mathf.Lerp(20f, 0f, t);
            }
            else
            {
                EndPose();
                return;
            }

            posed = true;
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, twist);
            pose.Rotate("UpperArm_R", transform.right, arm);
        }

        void EndPose()
        {
            active = false;
            if (!posed) return;
            pose.Reset();
            posed = false;
        }
    }

    // A grenade in flight: a small shell on a high arc from the muzzle to the landing point, then `onLand`. Purely
    // visual (it passes over characters and cover); the burst is resolved by the callback.
    public class GrenadeShell : MonoBehaviour
    {
        const float ApexHeight = 3f;

        Vector3 from, to;
        float start, duration;
        System.Action onLand;
        Material material;

        public static void Launch(Vector3 from, Vector3 to, float duration, bool krak, System.Action onLand)
        {
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = krak ? "Krak Shell" : "Frag Grenade";
            Destroy(shell.GetComponent<Collider>());
            shell.transform.localScale = Vector3.one * (krak ? 0.38f : 0.28f);
            var g = shell.AddComponent<GrenadeShell>();
            g.from = from;
            g.to = to + Vector3.up * 0.15f;
            g.start = Time.time;
            g.duration = Mathf.Max(0.05f, duration);
            g.onLand = onLand;
            g.material = VisualUtil.UnlitMaterial(krak ? BullgrynKit.Hazard : new Color(0.25f, 0.28f, 0.22f, 1f));
            var r = shell.GetComponent<Renderer>();
            r.sharedMaterial = g.material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell.transform.position = from;
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - start) / duration);
            Vector3 p = Vector3.Lerp(from, to, t);
            p.y += ApexHeight * 4f * t * (1f - t);
            transform.position = p;
            transform.Rotate(720f * Time.deltaTime, 0f, 0f, Space.Self);
            if (t < 1f) return;
            onLand?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
