using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // RMB: Balefire. After a short wind-up the Mandrake hurls a bolt of green witch-fire along the aim (a skillshot: the
    // first enemy in its path). It sets them alight with Balefire, a burn that only the Mandrake can fan: the next time
    // the Mandrake lands any hit on them, all the burn damage still to come lands at once.
    // C during the wind-up cancels it and refunds the cooldown.
    public class BalefireBolt : Ability
    {
        [SerializeField] float windup = 0.2f;
        [SerializeField] float boltDamage = 6f;
        [SerializeField] float burnDamagePerSecond = 5f;
        [SerializeField] float burnDuration = 4f;
        [SerializeField] float boltRange = 13f;
        [SerializeField] float boltSpeed = 22f;
        [SerializeField] float boltRadius = 0.3f;
        [SerializeField] float muzzleHeight = 1.2f;
        [SerializeField] float muzzleForward = 0.6f;

        Material boltMaterial;
        Material haloMaterial;

        public override string DisplayName => "Balefire";
        public override string KeyLabel => "RMB";
        protected override float WindupDuration => windup;
        protected override Color WindupColor => Balefire.Core;

        void Reset()
        {
            cooldown = 8f;
        }

        void OnDestroy()
        {
            if (boltMaterial != null) Destroy(boltMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        protected override bool IsTriggered() => Input.GetMouseButtonDown(1);

        // ---- Twin Flames: two bolts in reserve, each recharging on its own -----------------------------------------
        // A second bolt landing on a burning target bursts the first one's flames (any Mandrake hit does) and lights
        // them again.

        readonly float[] chargeReady = new float[2];

        bool TwoCharges => MandrakeUpgradeEffects.Has(gameObject, MandrakeUpgrade.TwinFlames);
        float ChargeCooldown => MandrakeUpgrade.TwinFlamesChargeCooldown * CooldownScale;

        int ReadyCharges
        {
            get
            {
                int n = 0;
                foreach (float t in chargeReady) if (Time.time >= t) n++;
                return n;
            }
        }

        public override int ChargeCapacity => TwoCharges ? 2 : 1;
        public override int ChargesReady => TwoCharges ? ReadyCharges : 0;
        protected override bool HasSpareCharge => TwoCharges && ReadyCharges > 0;

        public override float CooldownRemaining =>
            TwoCharges ? (ReadyCharges > 0 ? 0f : Mathf.Max(0f, Mathf.Min(chargeReady[0], chargeReady[1]) - Time.time)) : base.CooldownRemaining;

        public override float CooldownNormalized
        {
            get
            {
                if (!TwoCharges) return base.CooldownNormalized;
                float full = ChargeCooldown;
                return full > 0f ? Mathf.Clamp01(CooldownRemaining / full) : 0f;
            }
        }

        // One bolt is ready and the other is still recharging: report how far off it is.
        public override float ChargeRechargeNormalized
        {
            get
            {
                if (!TwoCharges || ReadyCharges != 1) return 0f;
                float full = ChargeCooldown;
                if (full <= 0f) return 0f;
                float pending = Time.time >= chargeReady[0] ? chargeReady[1] : chargeReady[0];
                return Mathf.Clamp01((pending - Time.time) / full);
            }
        }

        // Spends a ready charge (else the one closest to ready); only that one starts recharging.
        void SpendCharge()
        {
            int used = Time.time >= chargeReady[0] ? 0 : Time.time >= chargeReady[1] ? 1 : (chargeReady[0] <= chargeReady[1] ? 0 : 1);
            chargeReady[used] = Time.time + ChargeCooldown;
        }

        // The bolt flies on every machine; only the owner's copy strikes (Health.Strike ignores a replica's).
        protected override void Activate()
        {
            Vector3 dir = MandrakeKit.FlatAim(Aim.AimDirection, transform);
            float bonus = 0f;
            if (!IsReplica) bonus = MandrakeKit.BeginStrike(gameObject, out _);
            if (TwoCharges && !IsReplica) SpendCharge();

            Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;
            if (boltMaterial == null) boltMaterial = VisualUtil.UnlitMaterial(Balefire.Tip, 3002);
            if (haloMaterial == null)
                haloMaterial = VisualUtil.UnlitMaterial(new Color(Balefire.Core.r, Balefire.Core.g, Balefire.Core.b, 0.4f), 3001);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Balefire Bolt";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (boltRadius * 1.4f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = boltMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Halo";
            Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = new Vector3(2f, 2f, 2.8f);
            var hr = halo.GetComponent<Renderer>();
            hr.sharedMaterial = haloMaterial;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = boltRadius * 1.3f;
            trail.endWidth = 0f;
            trail.sharedMaterial = haloMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Sfx.Play(Sfx.Flamethrower, 0.35f, 1.3f, 1.5f, 0.35f);
            Sfx.Play(Sfx.MagicAttack2, 0.3f, 0.85f, 0.95f);

            go.AddComponent<Projectile>()
                .Init(gameObject, Health.Team, boltDamage + bonus, boltSpeed, boltRange, boltRadius, dir, OnBoltHit, target => MandrakeKit.VenomScale(gameObject, target))
                .WithRiders(Hit.Direct(0f).WithBalefire(burnDamagePerSecond, burnDuration));
        }

        void OnBoltHit(Health target)
        {
            HitMarkerSound.Play();
            Sfx.Play(Sfx.GroundFire, 0.3f, 1.2f, 1.35f, 0.5f);
        }

        protected override void Cancel()
        {
        }
    }
}
