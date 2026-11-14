using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Faith: every `DamagePerStack` damage she takes for an ally adds a stack, up to `MaxStacksCount` (Steadfast Faith
    // raises the cap). Each stack adds `BonusPerStack` to her damage (BlessedSuffering.Scale) and cuts the damage she
    // takes by `ReductionPerStack`, as armour: the shared pain she carries is cut too, and it adds to Martyrdom's
    // reduction under Health.MaxArmor. Upgrades can feed it too (Zealous Fervour, Sanctuary). Once she goes `DecayDelay`
    // seconds without gaining any, the stacks fall away one per `DecayInterval`. Cleared on death and during prep.
    // Counted on her own machine (where the shared damage is worked out); the reduction is kept refreshed on the copy
    // that resolves damage (the server online).
    public class Faith : MonoBehaviour, IPassiveStacks
    {
        public const float DamagePerStack = 15f;
        public const int MaxStacksCount = 5;
        public const float BonusPerStack = 0.06f;
        public const float ReductionPerStack = 0.05f;
        // The most reduction any Hospitaller can claim (Steadfast Faith's cap); the server clamps to this.
        public const float ReductionCeiling = ReductionPerStack * HospitallerUpgrade.SteadfastMaxStacks;
        const float DecayDelay = 4f;
        const float DecayInterval = 1f;
        const float RefreshInterval = 0.25f;
        const float HoldSeconds = 0.6f; // the reduction lapses this long after her machine stops refreshing it

        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        Health health;
        int stacks;
        float progress;
        float nextDecay;
        float nextRefresh;
        float sentReduction;

        public string PassiveName => "Faith";
        public int Stacks => stacks;
        public float Reduction => ReductionPerStack * stacks;
        public int MaxStacks => HospitallerUpgrade.Has(gameObject, HospitallerUpgrade.SteadfastFaith)
            ? HospitallerUpgrade.SteadfastMaxStacks : MaxStacksCount;
        public Color StackColor => Gold;
        public float NextStackProgress => stacks >= MaxStacks ? 0f : Mathf.Clamp01(progress / DamagePerStack);

        public static Faith For(GameObject hospitaller)
        {
            var faith = hospitaller.GetComponent<Faith>();
            if (faith == null) faith = hospitaller.AddComponent<Faith>();
            return faith;
        }

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        void OnDied(Health _, GameObject __) => Clear();

        void Clear()
        {
            stacks = 0;
            progress = 0f;
        }

        // Damage she carried for an ally.
        public void AddShared(float damage) => AddProgress(damage);

        // Zealous Fervour: damage her tether dealt to an enemy (a stack per ZealDamagePerStack).
        public void AddZeal(float damage) => AddProgress(damage * DamagePerStack / HospitallerUpgrade.ZealDamagePerStack);

        // Whole stacks at once (Sanctuary).
        public void AddStacks(int count) => AddProgress(count * DamagePerStack);

        void AddProgress(float amount)
        {
            if (health == null || health.IsDead || health.IsReplica || Anathema.Net.NetGame.InPrep || amount <= 0f) return;

            nextDecay = Time.time + DecayDelay;
            int max = MaxStacks;
            if (stacks >= max) return;

            int before = stacks;
            progress += amount;
            while (progress >= DamagePerStack && stacks < max)
            {
                progress -= DamagePerStack;
                stacks++;
            }
            if (stacks >= max) progress = 0f;

            // A golden flash at her feet when Faith is full.
            if (stacks == max && before < max)
                HospitallerFx.Burst(transform.position, 1.6f, new Color(1f, 0.85f, 0.35f, 0.9f), 0.45f, 1.2f);
        }

        void Update()
        {
            if (health == null) return;
            if (health.IsDead || Anathema.Net.NetGame.InPrep)
            {
                if (stacks > 0 || progress > 0f) Clear();
            }
            else if ((stacks > 0 || progress > 0f) && Time.time >= nextDecay)
            {
                progress = 0f;
                if (stacks > 0) stacks--;
                nextDecay = Time.time + DecayInterval;
            }

            RefreshReduction();
        }

        // Keeps her reduction on the copy that resolves her damage: refreshed while she has stacks, and set to zero
        // once when they run out, so it drops at once instead of lapsing.
        void RefreshReduction()
        {
            if (health.IsReplica) return;
            float reduction = Reduction;
            bool changed = !Mathf.Approximately(reduction, sentReduction);
            if (!changed && (reduction <= 0f || Time.time < nextRefresh)) return;

            nextRefresh = Time.time + RefreshInterval;
            sentReduction = reduction;
            if (health.IsAuthority) health.HoldArmor(reduction, HoldSeconds);
            else if (health.Net != null)
                health.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.FaithArmor, reduction, HoldSeconds, Vector3.zero, gameObject);
        }
    }
}
