using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryPriest
{
    // E: Chalice Offering. He raises the Blood Chalice: every teammate within `radius` (himself included) is healed,
    // and the heal is stronger for each enemy within `radius` of him. A weak button from the back line, a strong one in
    // the middle of the fight.
    // EX Red Thirst: the chalice is poured for one: the teammate nearest the cursor (himself if nobody else is near it)
    // gets a large heal.
    public class ChaliceOffering : Ability
    {
        [SerializeField] float radius = 4f;
        [SerializeField] float heal = 16f;
        [SerializeField] float bonusPerEnemy = 0.5f;
        [SerializeField] int maxBonusEnemies = 2;

        [Header("EX Red Thirst")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float exHeal = 30f;
        [SerializeField] float exRange = 10f;
        [SerializeField] float exPickRadius = 4f;

        Energy energy;

        protected override float WindupDuration => 0.3f;
        protected override Color WindupColor => SanguinaryKit.Gold;

        public override string DisplayName => "Chalice Offering";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Red Thirst";
        public override string ExDescription => "Pour the chalice for one teammate near the cursor: a large heal.";

        void Reset()
        {
            cooldown = 12f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.E);

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
                Health target = PickExTarget();
                if (target == null) return;
                Hospitaller.HospitallerFx.Burst(target.transform.position, 1.2f, SanguinaryKit.Blood, 0.45f, 1.2f);
                SanguinaryKit.BloodStream(here, target.transform);
                if (!IsReplica) SanguinaryKit.Heal(gameObject, target, exHeal);
                return;
            }

            Hospitaller.HospitallerFx.Burst(here, radius, SanguinaryKit.Gold, 0.5f, 0.8f);
            SanguinaryKit.Mist(here, radius * 0.6f, 0.5f);
            int enemies = Mathf.Min(maxBonusEnemies, SanguinaryKit.EnemiesNear(Health, here, radius));
            float amount = heal * (1f + bonusPerEnemy * enemies);
            foreach (Health ally in SanguinaryKit.AlliesNear(Health, here, radius).ToArray())
            {
                if (ally != Health) SanguinaryKit.BloodStream(here, ally.transform);
                if (!IsReplica) SanguinaryKit.Heal(gameObject, ally, amount);
            }
        }

        // The teammate nearest the cursor (within range of him), else himself.
        Health PickExTarget()
        {
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint : Aim.AimPoint;
            Vector3 to = SanguinaryKit.Flat(aim - transform.position);
            if (to.magnitude > exRange) aim = transform.position + to.normalized * exRange;
            Health best = null;
            float bestSq = exPickRadius * exPickRadius;
            foreach (Health h in SanguinaryKit.AlliesNear(Health, aim, exPickRadius, includeSelf: false))
            {
                float sq = SanguinaryKit.Flat(h.transform.position - aim).sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = h;
            }
            return best != null ? best : Health;
        }

        protected override void Cancel() { }
    }
}
