using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Faith: every `DamagePerStack` damage she takes for an ally adds a stack, up to `MaxStacksCount`. Each stack adds
    // `BonusPerStack` to her damage (BlessedSuffering.Scale) and cuts the damage she takes by `ReductionPerStack`, as
    // armour: the shared pain she carries is cut too, and it adds to Martyrdom's reduction under Health.MaxArmor. Once she goes `DecayDelay` seconds without sharing any damage, the
    // stacks fall away one per `DecayInterval`. Cleared on death and during prep. Counted on her own machine (where the
    // shared damage is worked out); the reduction is kept refreshed on the copy that resolves damage (the server online).
    public class Faith : MonoBehaviour, IPassiveStacks
    {
        public const float DamagePerStack = 15f;
        public const int MaxStacksCount = 5;
        public const float BonusPerStack = 0.06f;
        public const float ReductionPerStack = 0.05f;
        public const float MaxReduction = ReductionPerStack * MaxStacksCount;
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
        public int MaxStacks => MaxStacksCount;
        public Color StackColor => Gold;
        public float NextStackProgress => stacks >= MaxStacksCount ? 0f : Mathf.Clamp01(progress / DamagePerStack);

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

        public void AddShared(float damage)
        {
            if (health == null || health.IsDead || health.IsReplica || Anathema.Net.NetGame.InPrep) return;

            nextDecay = Time.time + DecayDelay;
            if (stacks >= MaxStacksCount) return;

            int before = stacks;
            progress += damage;
            while (progress >= DamagePerStack && stacks < MaxStacksCount)
            {
                progress -= DamagePerStack;
                stacks++;
            }
            if (stacks >= MaxStacksCount) progress = 0f;

            // A golden flash at her feet when Faith is full.
            if (stacks == MaxStacksCount && before < MaxStacksCount)
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
