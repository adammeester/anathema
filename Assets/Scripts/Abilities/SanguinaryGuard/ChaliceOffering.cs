using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // E: Chalice Offering. He fills the Blood Chalice from his own veins and raises it: he pays `cost` of his own health,
    // and every teammate within `radius` of him (never himself) is healed, more for each enemy within `radius` of him.
    // A weak button from the back line, a strong one in the middle of the fight.
    // Like Blood Transfusion it is locked while Red Thirst has him (SanguinaryKit.CanPay) and can't be cast with no
    // teammate to heal; he only pays if someone is still there to heal when it lands. Chalice of Wrath costs no health,
    // so it stays usable under Red Thirst.
    // EX Chalice of Wrath: he drinks instead of pouring. No health cost: every enemy within `exRadius` takes from
    // `exMinDamage` (empty chalice) up to `exDamage` (full), scaling with Vitae, which it does not spend. Each hit that
    // lands heals him for `exLifesteal` of it (like the Arc, this never fills Vitae). It casts with
    // nobody in reach: a whiff still spends the charge and energy. A dark ring marks its reach during the wind-up, and a
    // splash of blood bursts across the ground when it lands (BloodSplatter), so everyone can see it was used.
    public class ChaliceOffering : Ability
    {
        [SerializeField] float radius = 4f;
        [SerializeField] float cost = 15f;
        [SerializeField] float heal = 20f;
        [SerializeField] float bonusPerEnemy = 0.5f;
        [SerializeField] int maxBonusEnemies = 2;
        [SerializeField] float bonusRadius = 5f;   // enemies this close to him strengthen the heal (a little past the heal's own reach)

        [Header("EX Chalice of Wrath")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float exRadius = 4f;
        [SerializeField] float exDamage = 18f;       // with a full chalice
        [SerializeField] float exMinDamage = 6f;     // with an empty one; scales linearly with Vitae in between
        [SerializeField] float exLifesteal = 0.8f;   // share of each landed hit that heals him

        [Header("Charges")]
        [SerializeField] float rechargeTime = 12f;   // each spent charge comes back on its own after this long

        const int MaxCharges = 3;
        readonly float[] chargeReady = new float[MaxCharges];

        Energy energy;
        Vitae vitae;
        GameObject template;   // Chalice of Wrath's ground ring during the wind-up

        protected override float WindupDuration => 0.3f;
        protected override Color WindupColor => SanguinaryKit.Gold;

        public override string DisplayName => "Chalice Offering";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Chalice of Wrath";
        public override string ExDescription => "Drink from the chalice: every enemy around you is drained, and their blood heals you.";
        public override bool Available => SanguinaryKit.CanPay(Health, cost);

        // Three charges, each recharging on its own (like Sanctus Pulse): `cooldown` is only the short gap between two
        // casts. Chalice of Wrath spends a charge too. A cast that finds nobody to heal when it lands spends nothing.
        public override int ChargeCapacity => MaxCharges;
        public override int ChargesReady => ReadyCharges;

        // Sanguinary Rite shortens each charge's recharge.
        float Recharge => (GuardUpgrade.Has(gameObject, GuardUpgrade.SanguinaryRite) ? GuardUpgrade.RiteRecharge : rechargeTime) * CooldownScale;

        int ReadyCharges
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxCharges; i++) if (Time.time >= chargeReady[i]) n++;
                return n;
            }
        }

        float NextCharge
        {
            get
            {
                float next = float.PositiveInfinity;
                for (int i = 0; i < MaxCharges; i++) if (chargeReady[i] > Time.time && chargeReady[i] < next) next = chargeReady[i];
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
            vitae = GetComponent<Vitae>();
        }

        protected override bool IsTriggered()
        {
            if (!Input.GetKeyDown(KeyCode.E) || ReadyCharges == 0) return false;
            // Chalice of Wrath needs only the energy and a charge: cast at nobody, it is simply wasted.
            if (ExHeld && energy != null && energy.Current >= exEnergyCost) return true;
            return SanguinaryKit.CanPay(Health, cost) && SanguinaryKit.AlliesNear(Health, transform.position, radius, includeSelf: false).Count > 0;
        }

        protected override void OnWindupStart()
        {
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
            // Chalice of Wrath marks its reach on the ground while he raises the cup, so everyone sees it coming.
            if (ExActivation)
            {
                template = Bullgryn.BullgrynKit.GroundCircle(transform.position, exRadius, WindupDuration, SanguinaryKit.DarkBlood);
                template.transform.SetParent(transform, true);
            }
            // Chalice Offering marks the heal's reach and the enemy-bonus ring instead, in gold.
            else template = OfferingTemplate.Show(transform, radius, bonusRadius, WindupDuration);
        }

        protected override void OnWindupCancel()
        {
            if (!IsReplica && ExActivation && energy != null) energy.Gain(exEnergyCost);
            if (template != null) Destroy(template);
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
                if (template != null) Destroy(template);
                BloodSplatter.Spawn(here, exRadius);
                Sfx.Play(Sfx.HammerSmash, 0.3f, 0.6f, 0.7f, 0.5f);
                foreach (Health victim in victims) SanguinaryKit.BloodStream(victim.transform.position, transform);
                if (IsReplica) return;
                SpendCharge();
                // The fuller the chalice, the deeper the draught (read now; drinking spends no Vitae).
                float dealt = Mathf.Lerp(exMinDamage, exDamage, vitae != null ? vitae.Normalized : 1f);
                bool marked = false;
                foreach (Health victim in victims)
                {
                    Hit drain = Hit.Direct(dealt).AsAoe();
                    if (GuardUpgrade.Has(gameObject, GuardUpgrade.DeathMask)) drain = drain.WithPanic(GuardUpgrade.DeathMaskPanic);
                    victim.Strike(drain, gameObject, landed =>
                    {
                        if (!landed) return;
                        SanguinaryKit.Heal(gameObject, Health, dealt * exLifesteal, feedsVitae: false);
                        if (marked) return;
                        marked = true;
                        HitMarkerSound.Play();
                    });
                }
                return;
            }

            if (template != null) Destroy(template);
            Hospitaller.HospitallerFx.Burst(here, radius, SanguinaryKit.Gold, 0.5f, 0.8f);
            SanguinaryKit.Mist(here, radius * 0.6f, 0.5f);
            Health[] allies = SanguinaryKit.AlliesNear(Health, here, radius, includeSelf: false).ToArray();
            int enemies = Mathf.Min(maxBonusEnemies, SanguinaryKit.EnemiesNear(Health, here, bonusRadius));
            float amount = heal * (1f + bonusPerEnemy * enemies);
            foreach (Health ally in allies) SanguinaryKit.BloodStream(here, ally.transform);
            if (IsReplica || allies.Length == 0) return;

            SpendCharge();
            SanguinaryKit.Pay(Health, cost);
            foreach (Health ally in allies) SanguinaryKit.Heal(gameObject, ally, amount);
        }

        protected override void Cancel() { }

        // Chalice Offering's mark while he raises the cup: unlike Chalice of Wrath's dark ring, a soft gold pool filling
        // the heal's reach, a bright gold rim with slowly turning spokes, and a dashed red ring a little further out that
        // shows how close enemies must be to strengthen the heal. Follows him; gone when the wind-up ends.
        sealed class OfferingTemplate : MonoBehaviour
        {
            const int Points = 48, Dashes = 20, Spokes = 8;
            const float Height = 0.17f;   // above the arena's visible floor discs
            LineRenderer rim;
            LineRenderer[] dashes;
            LineRenderer[] spokes;
            Transform pool;
            Material poolMaterial;
            float radius, bonusRadius, duration, start;

            public static GameObject Show(Transform guard, float radius, float bonusRadius, float seconds)
            {
                var go = new GameObject("Chalice Offering Template");
                go.transform.SetParent(guard, false);
                go.transform.localPosition = Vector3.up * Height;
                var t = go.AddComponent<OfferingTemplate>();
                t.radius = radius;
                t.bonusRadius = bonusRadius;
                t.duration = Mathf.Max(0.05f, seconds);
                t.start = Time.time;
                t.Build();
                return go;
            }

            void Build()
            {
                Color gold = SanguinaryKit.Gold;
                rim = Bullgryn.BullgrynKit.NewLine(transform, new Color(gold.r, gold.g, gold.b, 0.95f), 0.09f, Points, true);
                dashes = new LineRenderer[Dashes];
                for (int i = 0; i < Dashes; i++)
                    dashes[i] = Bullgryn.BullgrynKit.NewLine(transform, new Color(0.9f, 0.1f, 0.08f, 0.7f), 0.05f, 2, false);
                spokes = new LineRenderer[Spokes];
                for (int i = 0; i < Spokes; i++)
                    spokes[i] = Bullgryn.BullgrynKit.NewLine(transform, new Color(gold.r, gold.g, gold.b, 0.6f), 0.05f, 2, false);

                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(disc.GetComponent<Collider>());
                poolMaterial = VisualUtil.UnlitMaterial(new Color(gold.r, gold.g, gold.b, 0.18f), 2995);
                var r = disc.GetComponent<Renderer>();
                r.sharedMaterial = poolMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                disc.transform.SetParent(transform, false);
                pool = disc.transform;
                Update();
            }

            void Update()
            {
                float t = (Time.time - start) / duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector3 c = transform.position;
                float grow = 1f - (1f - Mathf.Clamp01(t * 1.6f)) * (1f - Mathf.Clamp01(t * 1.6f));
                pool.localScale = new Vector3(radius * 2f * grow, 0.004f, radius * 2f * grow);

                for (int i = 0; i < Points; i++)
                {
                    float a = i / (float)Points * Mathf.PI * 2f;
                    rim.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                }
                // The dashed enemy ring: one short line per dash, the gaps left blank.
                for (int i = 0; i < Dashes; i++)
                {
                    float a0 = (i / (float)Dashes) * Mathf.PI * 2f, a1 = a0 + Mathf.PI * 2f / Dashes * 0.55f;
                    dashes[i].SetPosition(0, c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * bonusRadius);
                    dashes[i].SetPosition(1, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * bonusRadius);
                }
                float turn = Time.time * 0.8f;
                for (int i = 0; i < Spokes; i++)
                {
                    float a = turn + i / (float)Spokes * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    spokes[i].SetPosition(0, c + d * radius * 0.25f);
                    spokes[i].SetPosition(1, c + d * radius * Mathf.Lerp(0.3f, 0.95f, grow));
                }
            }

            void OnDestroy()
            {
                if (poolMaterial != null) Destroy(poolMaterial);
                if (rim != null) Destroy(rim.sharedMaterial);
                if (dashes != null) foreach (LineRenderer d in dashes) if (d != null) Destroy(d.sharedMaterial);
                if (spokes != null) foreach (LineRenderer s in spokes) if (s != null) Destroy(s.sharedMaterial);
            }
        }
    }
}
