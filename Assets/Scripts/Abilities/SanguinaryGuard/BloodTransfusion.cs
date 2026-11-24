using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // RMB: Blood Transfusion. He opens his own veins and hurls a bolt of his blood along the aim direction: he pays
    // `cost` of his own health as he throws it, and the first teammate it touches (never himself) is healed for `heal`.
    // It is a skillshot: an enemy in the way soaks it up (a token hit their counter can catch, then a short bleed; the
    // heal is lost) and a miss is wasted
    // blood, so lead moving allies and throw past the melee, not through it.
    // What he pays stays healable, so the Exsanguinating Arc's lifesteal can win it back, and it fills Vitae
    // (SanguinaryKit.Pay). Like every health cost it is locked only while Red Thirst has him (SanguinaryKit.CanPay).
    // With the Encarmine Axe it becomes Sanguine Font: he pours the blood on the ground up to `FontRange` away, and allies
    // standing in the pool heal over time (SanguineFont). With the Glaive Encarmine the bolt heals less but bounces on to
    // the nearest other ally.
    [DefaultExecutionOrder(55)] // after ProceduralWalk and the sword's pose
    public class BloodTransfusion : Ability
    {
        [SerializeField] float cost = 12f;
        [SerializeField] float heal = 22f;
        [SerializeField] float range = 12f;
        [SerializeField] float projectileSpeed = 20f;
        [SerializeField] float projectileRadius = 0.45f;
        [SerializeField] float muzzleHeight = 1.4f;
        [SerializeField] float muzzleForward = 0.6f;

        [SerializeField] float windup = 0.3f;   // cancellable, telegraphed and readable: enemies can see it coming and step in
        [SerializeField] float enemyHitDamage = 1f;    // an enemy who blocks it takes a token hit (so counters can catch it)...
        [SerializeField] float bleedPerSecond = 3f;    // ...and bleeds
        [SerializeField] float bleedSeconds = 3f;

        Material boltMaterial;
        Material haloMaterial;
        static readonly string[] PoseBones = { "Spine", "UpperArm_L", "Forearm_L" };
        LimbPose pose;
        Transform hand;          // where the blood gathers (his free left hand)
        GameObject gather;       // the growing orb of blood during the wind-up
        GameObject marker;       // the aim lane (or the Font's landing circle) during the wind-up
        float throwPoseEnd = -1f;
        Vector3 lockedAim;
        bool posed;

        protected override float WindupDuration => windup;
        protected override Color WindupColor => SanguinaryKit.Blood;

        GuardWeapon Weapon => GuardUpgrade.WeaponOf(gameObject);

        public override string DisplayName => Weapon == GuardWeapon.EncarmineAxe ? "Sanguine Font" : "Blood Transfusion";
        public override string KeyLabel => "RMB";
        public override bool Available => SanguinaryKit.CanPay(Health, cost);

        void Reset()
        {
            cooldown = 4f;
        }

        protected override void Awake()
        {
            base.Awake();
            Transform model = transform.Find("Model");
            pose = new LimbPose(model, PoseBones);
            if (model != null)
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Hand_L") { hand = t; break; }
                    if (hand == null && t.name == "Forearm_L") hand = t;
                }
        }

        // The wind-up, on every machine: he draws his free arm back and blood wells up in his palm, while the ground shows
        // where it will go (the bolt's lane, or the Font's pool). Enemies can read it and step into the lane to block it.
        protected override void OnWindupStart()
        {
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint : Aim.AimPoint;
            // The aim is locked here, so the lane on the ground is exactly where the blood will go.
            lockedAim = aim;
            Vector3 to = SanguinaryKit.Flat(aim - transform.position);
            Vector3 dir = to.sqrMagnitude > 0.25f ? to.normalized : transform.forward;
            if (marker != null) Destroy(marker);
            if (Weapon == GuardWeapon.EncarmineAxe)
            {
                if (to.magnitude > GuardUpgrade.FontRange) to = to.normalized * GuardUpgrade.FontRange;
                marker = Bullgryn.BullgrynKit.GroundCircle(transform.position + to, GuardUpgrade.FontRadius, windup, SanguinaryKit.Blood);
            }
            else
            {
                marker = Bullgryn.BullgrynKit.GroundLane(transform.position, dir, range, projectileRadius * 2f, windup, SanguinaryKit.Blood);
                marker.transform.SetParent(transform, true);
            }

            if (gather != null) Destroy(gather);
            if (boltMaterial == null) boltMaterial = VisualUtil.UnlitMaterial(SanguinaryKit.BrightBlood);
            gather = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gather.name = "Gathering Blood";
            Destroy(gather.GetComponent<Collider>());
            var r = gather.GetComponent<Renderer>();
            r.sharedMaterial = boltMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gather.transform.localScale = Vector3.zero;
            SanguinaryKit.Mist(transform.position + Vector3.up, 0.5f, windup);
            Sfx.Play(Sfx.MagicAttack1, 0.2f, 0.55f, 0.65f, windup);
        }

        protected override void OnWindupCancel()
        {
            if (marker != null) Destroy(marker);
            if (gather != null) Destroy(gather);
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButtonDown(1) && SanguinaryKit.CanPay(Health, cost);
        }

        protected override void Update()
        {
            // The Font is a bigger heal, so it comes round slower (only changed while ready, for the HUD's sweep).
            if (CooldownRemaining <= 0f && cooldown > 0f)
                CooldownScale = Weapon == GuardWeapon.EncarmineAxe ? GuardUpgrade.FontCooldown / cooldown : 1f;
            base.Update();
        }

        protected override void Activate()
        {
            if (marker != null) Destroy(marker);
            if (gather != null) Destroy(gather);
            throwPoseEnd = Time.time + 0.2f;
            Vector3 aim = lockedAim;
            if (Weapon == GuardWeapon.EncarmineAxe)
            {
                PourFont(aim);
                return;
            }
            Vector3 origin = transform.position + Vector3.up * muzzleHeight;
            Vector3 dir = SanguinaryKit.Flat(aim - transform.position);
            dir = dir.sqrMagnitude > 0.25f ? dir.normalized : transform.forward;
            origin += dir * muzzleForward;

            SanguinaryKit.Mist(transform.position, 0.8f, 0.35f);
            Sfx.Play(Sfx.MagicAttack1, 0.3f, 0.8f, 0.9f, 0.6f);
            if (!IsReplica) SanguinaryKit.Pay(Health, cost);

            if (boltMaterial == null) boltMaterial = VisualUtil.UnlitMaterial(SanguinaryKit.BrightBlood);
            if (haloMaterial == null)
            {
                Color c = SanguinaryKit.Blood;
                haloMaterial = VisualUtil.UnlitMaterial(new Color(c.r, c.g, c.b, 0.4f));
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Blood Bolt";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (projectileRadius * 1.3f);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = boltMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Halo";
            Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = Vector3.one * 1.9f;
            Renderer hr = halo.GetComponent<Renderer>();
            hr.sharedMaterial = haloMaterial;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = projectileRadius * 0.9f;
            trail.endWidth = 0f;
            trail.sharedMaterial = haloMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // An enemy in the way stops the bolt: a token hit (so their counter can catch it, like any attack) that leaves
            // them bleeding. Teammates stop it and are healed.
            go.AddComponent<Projectile>()
              .Init(gameObject, Health.Team, enemyHitDamage, projectileSpeed, range, projectileRadius, dir)
              .WithRiders(Hit.Direct(0f).WithBleed(bleedPerSecond, bleedSeconds))
              .WithAllyHit(OnAllyHit);
        }

        // Every machine's bolt stops on the teammate; only the owner's heals. With the Glaive the blood leaps on to the
        // nearest other teammate (never him) within BounceRange.
        void OnAllyHit(Health ally)
        {
            if (ally == null || ally.IsDead) return;
            Hospitaller.HospitallerFx.Burst(ally.transform.position, 1f, SanguinaryKit.Blood, 0.4f, 1.1f);
            bool glaive = Weapon == GuardWeapon.GlaiveEncarmine;
            Health next = glaive ? NextAlly(ally) : null;
            if (next != null) SanguinaryKit.BloodStream(ally.transform.position, next.transform);
            if (IsReplica) return;
            SanguinaryKit.Heal(gameObject, ally, glaive ? GuardUpgrade.GlaiveBoltHeal : heal);
            if (next != null)
                SanguinaryKit.Heal(gameObject, next, GuardUpgrade.Has(gameObject, GuardUpgrade.BloodRicochet) ? GuardUpgrade.GlaiveBoltHeal : GuardUpgrade.BounceHeal);
        }

        Health NextAlly(Health from)
        {
            Health best = null;
            float bestSq = float.PositiveInfinity;
            foreach (Health h in SanguinaryKit.AlliesNear(Health, from.transform.position, GuardUpgrade.BounceRange, includeSelf: false).ToArray())
            {
                if (h == from) continue;
                float sq = SanguinaryKit.Flat(h.transform.position - from.transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = h; }
            }
            return best;
        }

        // Sanguine Font (Encarmine Axe): the blood poured on the ground where he aims (up to FontRange away).
        void PourFont(Vector3 aim)
        {
            Vector3 to = SanguinaryKit.Flat(aim - transform.position);
            if (to.magnitude > GuardUpgrade.FontRange) to = to.normalized * GuardUpgrade.FontRange;
            Vector3 at = transform.position + to;
            SanguinaryKit.Mist(transform.position, 0.8f, 0.35f);
            Sfx.Play(Sfx.MagicAttack1, 0.3f, 0.7f, 0.8f, 0.6f);
            if (!IsReplica) SanguinaryKit.Pay(Health, GuardUpgrade.FontCost);
            bool well = GuardUpgrade.Has(gameObject, GuardUpgrade.Wellspring);
            SanguineFont.Pour(at, Health, !IsReplica, well ? GuardUpgrade.WellspringSeconds : GuardUpgrade.FontSeconds,
                well ? GuardUpgrade.WellspringLinger : 0f);
        }

        void OnDestroy()
        {
            if (marker != null) Destroy(marker);
            if (gather != null) Destroy(gather);
            if (boltMaterial != null) Destroy(boltMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        // Arm drawn back over the shoulder through the wind-up (the orb swelling in his palm), then flung forward.
        void LateUpdate()
        {
            float now = Time.time;
            bool winding = IsWindingUp;
            bool flinging = now < throwPoseEnd;
            if (!winding && !flinging)
            {
                if (posed) pose.Reset();
                posed = false;
                return;
            }

            float arm, twist;
            if (winding)
            {
                float t = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(WindupRemaining / Mathf.Max(0.01f, windup)));
                arm = Mathf.Lerp(0f, -110f, t);
                twist = Mathf.Lerp(0f, -25f, t);
                if (gather != null && hand != null)
                {
                    gather.transform.position = hand.position;
                    gather.transform.localScale = Vector3.one * (projectileRadius * 1.2f * t);
                }
            }
            else
            {
                float t = 1f - (throwPoseEnd - now) / 0.2f;
                arm = Mathf.Lerp(-110f, 15f, t);
                twist = Mathf.Lerp(-25f, 20f, t);
            }
            posed = true;
            pose.Reset();
            pose.Rotate("Spine", Vector3.up, twist);
            pose.Rotate("UpperArm_L", transform.right, arm);
        }

        // Once the bolt leaves there is nothing to cancel (a wind-up in progress is cancelled by the base class).
        protected override void Cancel() { }
    }
}
