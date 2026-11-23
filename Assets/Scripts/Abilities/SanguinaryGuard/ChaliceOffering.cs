using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // E: Chalice Offering. He fills the Blood Chalice from his own veins and raises it: he pays `cost` of his own health,
    // and every teammate within `radius` of him (never himself) is healed, more for each enemy within `radius` of him.
    // A weak button from the back line, a strong one in the middle of the fight.
    // Like Blood Transfusion it can't be cast if paying would leave him below SanguinaryKit.SacrificeFloor of his max
    // health, or with no teammate to heal; he only pays if someone is still there to heal when it lands.
    // EX Chalice of Wrath: he drinks instead of pouring. No health cost: every enemy within `exRadius` takes `exDamage`
    // and each hit that lands heals him for `exLifesteal` of it (like the Arc, this never fills Vitae). It needs an enemy
    // in reach to cast; if none is left when it lands, the charge and energy are given back.
    public class ChaliceOffering : Ability
    {
        [SerializeField] float radius = 4f;
        [SerializeField] float cost = 15f;
        [SerializeField] float heal = 20f;
        [SerializeField] float bonusPerEnemy = 0.5f;
        [SerializeField] int maxBonusEnemies = 2;

        [Header("EX Chalice of Wrath")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float exRadius = 4f;
        [SerializeField] float exDamage = 18f;
        [SerializeField] float exLifesteal = 0.8f;   // share of each landed hit that heals him

        [Header("Charges")]
        [SerializeField] float rechargeTime = 12f;   // each spent charge comes back on its own after this long

        const int MaxCharges = 2;
        readonly float[] chargeReady = new float[MaxCharges];

        Energy energy;

        protected override float WindupDuration => 0.3f;
        protected override Color WindupColor => SanguinaryKit.Gold;

        public override string DisplayName => "Chalice Offering";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Chalice of Wrath";
        public override string ExDescription => "Drink from the chalice: every enemy around you is drained, and their blood heals you.";
        public override bool Available => SanguinaryKit.CanPay(Health, cost);

        // Two charges, each recharging on its own (like Sanctus Pulse): `cooldown` is only the short gap between two
        // casts. Chalice of Wrath spends a charge too. A cast that finds nobody to heal when it lands spends nothing.
        public override int ChargeCapacity => MaxCharges;
        public override int ChargesReady => ReadyCharges;

        float Recharge => rechargeTime * CooldownScale;

        int ReadyCharges
        {
            get
            {
                int n = 0;
                foreach (float t in chargeReady) if (Time.time >= t) n++;
                return n;
            }
        }

        float NextCharge
        {
            get
            {
                float next = float.PositiveInfinity;
                foreach (float t in chargeReady) if (t > Time.time && t < next) next = t;
                return next;
            }
        }

        public override float CooldownRemaining =>
            ReadyCharges > 0 ? base.CooldownRemaining : Mathf.Max(0f, NextCharge - Time.time);

        public override float CooldownNormalized =>
            ReadyCharges > 0 ? base.CooldownNormalized : (Recharge > 0f ? Mathf.Clamp01(CooldownRemaining / Recharge) : 0f);

        public override float ChargeRechargeNormalized
        {
            get
            {
                int ready = ReadyCharges;
                if (ready == 0 || ready == MaxCharges || Recharge <= 0f) return 0f;
                return Mathf.Clamp01((NextCharge - Time.time) / Recharge);
            }
        }

        // Spends the charge that has been ready longest; only that one starts recharging. Owner only.
        void SpendCharge()
        {
            int used = 0;
            for (int i = 1; i < MaxCharges; i++)
                if (chargeReady[i] < chargeReady[used]) used = i;
            chargeReady[used] = Time.time + Recharge;
        }

        // The Exsanguinating Arc refuels the chalice: the charge closest to coming back is brought forward by `fraction`
        // of a full recharge. Owner only (charges are tracked on the owner's machine).
        public void RefundCharge(float fraction)
        {
            int soonest = -1;
            for (int i = 0; i < MaxCharges; i++)
                if (chargeReady[i] > Time.time && (soonest < 0 || chargeReady[i] < chargeReady[soonest])) soonest = i;
            if (soonest >= 0) chargeReady[soonest] -= fraction * Recharge;
        }

        bool ExHeld => Input.GetKey(KeyCode.LeftShift);

        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            if (!Input.GetKeyDown(KeyCode.E) || ReadyCharges == 0) return false;
            if (ExHeld) return SanguinaryKit.EnemiesNear(Health, transform.position, exRadius) > 0;
            return SanguinaryKit.CanPay(Health, cost) && SanguinaryKit.AlliesNear(Health, transform.position, radius, includeSelf: false).Count > 0;
        }

        protected override void OnWindupStart()
        {
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
        }

        protected override void OnWindupCancel()
        {
            if (!IsReplica && ExActivation && energy != null) energy.Gain(exEnergyCost);
        }

        protected override void Activate()
        {
            Vector3 here = transform.position;
            Sfx.Play(Sfx.MagicAttack2, 0.35f, 0.75f, 0.85f, 0.8f);

            if (ExActivation)
            {
                Health[] victims = Mandrake.MandrakeKit.EnemiesInCone(Health, here, transform.forward, exRadius, 360f).ToArray();
                Hospitaller.HospitallerFx.Burst(here, exRadius, SanguinaryKit.Blood, 0.5f, 0.9f);
                Psyker.PsykerFx.Ring(here + Vector3.up * 0.1f, 0.3f, exRadius, 0.3f, SanguinaryKit.DarkBlood, 0.22f);
                SanguinaryKit.Mist(here, exRadius * 0.7f, 0.5f);
                Sfx.Play(Sfx.HammerSmash, 0.3f, 0.6f, 0.7f, 0.5f);
                foreach (Health victim in victims) SanguinaryKit.BloodStream(victim.transform.position, transform);
                if (IsReplica) return;
                if (victims.Length == 0)
                {
                    if (energy != null) energy.Gain(exEnergyCost);
                    return;
                }

                SpendCharge();
                bool marked = false;
                foreach (Health victim in victims)
                {
                    victim.Strike(Hit.Direct(exDamage).AsAoe(), gameObject, landed =>
                    {
                        if (!landed) return;
                        SanguinaryKit.Heal(gameObject, Health, exDamage * exLifesteal, feedsVitae: false);
                        if (marked) return;
                        marked = true;
                        HitMarkerSound.Play();
                    });
                }
                return;
            }

            Hospitaller.HospitallerFx.Burst(here, radius, SanguinaryKit.Gold, 0.5f, 0.8f);
            SanguinaryKit.Mist(here, radius * 0.6f, 0.5f);
            Health[] allies = SanguinaryKit.AlliesNear(Health, here, radius, includeSelf: false).ToArray();
            int enemies = Mathf.Min(maxBonusEnemies, SanguinaryKit.EnemiesNear(Health, here, radius));
            float amount = heal * (1f + bonusPerEnemy * enemies);
            foreach (Health ally in allies) SanguinaryKit.BloodStream(here, ally.transform);
            if (IsReplica || allies.Length == 0) return;

            SpendCharge();
            SanguinaryKit.Pay(Health, cost);
            foreach (Health ally in allies) SanguinaryKit.Heal(gameObject, ally, amount);
        }

        protected override void Cancel() { }
    }
}
