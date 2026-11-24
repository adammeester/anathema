using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // RMB: Blood Transfusion. He opens his own veins and hurls a bolt of his blood along the aim direction: he pays
    // `cost` of his own health as he throws it, and the first teammate it touches (never himself) is healed for `heal`.
    // It is a skillshot: an enemy in the way soaks it up (no damage, the blood is simply lost) and a miss is wasted
    // blood, so lead moving allies and throw past the melee, not through it.
    // What he pays stays healable, so the Exsanguinating Arc's lifesteal can win it back, and it fills Vitae
    // (SanguinaryKit.Pay). Like every health cost it is locked only while Red Thirst has him (SanguinaryKit.CanPay).
    // With the Encarmine Axe it becomes Sanguine Font: he pours the blood on the ground up to `FontRange` away, and allies
    // standing in the pool heal over time (SanguineFont). With the Glaive Encarmine the bolt heals less but bounces on to
    // the nearest other ally.
    public class BloodTransfusion : Ability
    {
        [SerializeField] float cost = 12f;
        [SerializeField] float heal = 22f;
        [SerializeField] float range = 12f;
        [SerializeField] float projectileSpeed = 20f;
        [SerializeField] float projectileRadius = 0.45f;
        [SerializeField] float muzzleHeight = 1.4f;
        [SerializeField] float muzzleForward = 0.6f;

        Material boltMaterial;
        Material haloMaterial;

        protected override float WindupDuration => 0.12f;
        protected override Color WindupColor => SanguinaryKit.Blood;

        GuardWeapon Weapon => GuardUpgrade.WeaponOf(gameObject);

        public override string DisplayName => Weapon == GuardWeapon.EncarmineAxe ? "Sanguine Font" : "Blood Transfusion";
        public override string KeyLabel => "RMB";
        public override bool Available => SanguinaryKit.CanPay(Health, cost);

        void Reset()
        {
            cooldown = 4f;
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
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint : Aim.AimPoint;
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

            // Zero damage: an enemy in the way stops the bolt and takes nothing. Teammates stop it and are healed.
            go.AddComponent<Projectile>()
              .Init(gameObject, Health.Team, 0f, projectileSpeed, range, projectileRadius, dir)
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
            if (boltMaterial != null) Destroy(boltMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        // Once the bolt leaves there is nothing to cancel (a wind-up in progress is cancelled by the base class).
        protected override void Cancel() { }
    }
}
