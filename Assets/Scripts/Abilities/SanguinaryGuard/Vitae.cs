using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Passive: Vitae, the Blood Chalice. Healing the guard gives (to teammates or himself) fills it, one Vitae per point
    // of health actually restored (overhealing counts for nothing). Healing given while an enemy stands within
    // `frontlineRadius` of him counts `frontlineBonus` times over. Health he pays for his abilities (Blood Transfusion,
    // Chalice Offering) fills it too, `perHealthPaid` per point. Damage dealt or taken never fills it, except under Red
    // Thirst, where his hits feed it (RedThirst.OnDamageDealt).
    // After `drainDelay` seconds without healing anyone it drains, unless it is full. Full, his next Chainsword swing is
    // the Exsanguinating Arc, which empties it; while it is full he takes `fullExposure` more damage.
    //
    // The chalice lives on the owner's machine (heals are given there, through SanguinaryKit.Heal). Every other copy
    // only knows whether it is full, from the published "empowered" flag (Chainsword.ShowReplicaEmpowered); the server's
    // copy uses that for the extra damage taken.
    public class Vitae : MonoBehaviour, IPassiveStacks
    {
        [SerializeField] float maxVitae = 100f;
        [SerializeField] float frontlineRadius = 3f;
        [SerializeField] float frontlineBonus = 1.5f;
        [SerializeField] float drainDelay = 3f;
        [SerializeField] float drainPerSecond = 6f;
        [SerializeField] float fullExposure = 0.1f;
        [SerializeField] float perHealthPaid = 1f;   // Vitae per point of health he sacrifices

        const int Pips = 5;

        Health health;
        WingsOfTheAngel wings;
        float lastGain = float.NegativeInfinity;
        bool replicaFull;

        // While above zero, heals the guard gives don't fill the chalice (the Arc's own healing).
        public int Suppressed { get; set; }

        public float Current { get; private set; }
        // Brimming Chalice raises the cap.
        public float Max => GuardUpgrade.Has(gameObject, GuardUpgrade.BrimmingChalice) ? GuardUpgrade.BrimmingCap : maxVitae;
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool Full => health != null && health.IsReplica ? replicaFull : Current >= Max - 0.01f;

        // 0..3 for the blade glow: faint at a third, glowing at two thirds, blazing when full (or while Wings of the
        // Angel turns every swing into an Arc).
        public int GlowLevel
        {
            get
            {
                if (Full || (wings != null && wings.Ascended)) return 3;
                if (health != null && health.IsReplica) return 0;
                float n = Normalized;
                return n >= 0.66f ? 2 : n >= 0.33f ? 1 : 0;
            }
        }

        public string PassiveName => "Vitae";
        public int Stacks => Mathf.FloorToInt(Normalized * Pips + 0.0001f);
        public int MaxStacks => Pips;
        public Color StackColor => SanguinaryKit.Blood;
        public float NextStackProgress => Stacks >= Pips ? 0f : Normalized * Pips - Stacks;

        void Awake()
        {
            health = GetComponent<Health>();
            wings = GetComponent<WingsOfTheAngel>();
        }

        void OnEnable() => Healing.Applying += OnHealing;
        void OnDisable() => Healing.Applying -= OnHealing;

        // Raised just before a heal is applied or sent, while the target's healable health is still the value before it.
        void OnHealing(Health target, float amount, GameObject healer)
        {
            if (healer != gameObject || health == null || health.IsReplica || health.IsDead || Suppressed > 0) return;
            if (target == null || target.IsDead || target.Team != health.Team) return;
            float landed = Mathf.Min(amount, target.Recoverable);
            // Overflowing Chalice: overhealing counts too, at a reduced rate.
            if (GuardUpgrade.Has(gameObject, GuardUpgrade.OverflowingChalice))
                landed += Mathf.Max(0f, amount - landed) * GuardUpgrade.OverflowShare;
            if (landed <= 0f) return;
            bool frontline = SanguinaryKit.EnemiesNear(health, transform.position, frontlineRadius) > 0;
            Add(landed * (frontline ? frontlineBonus : 1f));
        }

        public void Add(float amount)
        {
            if (amount <= 0f) return;
            lastGain = Time.time;
            Current = Mathf.Min(Max, Current + amount);
        }

        // Owner: he paid `paid` of his own health for an ability (SanguinaryKit.Pay).
        public void OnSacrifice(float paid)
        {
            if (health == null || health.IsReplica || health.IsDead) return;
            Add(paid * perHealthPaid);
        }

        // The Arc spends the whole chalice.
        public void Spend()
        {
            Current = 0f;
            lastGain = Time.time;
        }

        public void SetReplicaFull(bool on) => replicaFull = on;

        void Update()
        {
            if (health.IsDead)
            {
                Current = 0f;
                return;
            }

            // Sanctified Blood: it never drains.
            if (!health.IsReplica && Current > 0f && Current < Max - 0.01f && Time.time - lastGain >= drainDelay
                && !GuardUpgrade.Has(gameObject, GuardUpgrade.SanctifiedBlood))
                Current = Mathf.Max(0f, Current - drainPerSecond * Time.deltaTime);

            // A full chalice makes him a target: extra damage, resolved where damage is.
            if (fullExposure > 0f && health.IsAuthority && Full) health.Expose(fullExposure, 0.2f);
        }
    }
}
