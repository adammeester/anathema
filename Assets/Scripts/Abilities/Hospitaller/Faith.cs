using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // Faith: every `DamagePerStack` damage she takes for an ally adds a stack, up to `MaxStacksCount`. Each stack adds
    // `BonusPerStack` to her damage. Once she goes `DecayDelay` seconds without sharing any damage, the stacks fall away
    // one per `DecayInterval`. Cleared on death and during prep. Kept on her own machine, where her damage is dealt.
    public class Faith : MonoBehaviour, IPassiveStacks
    {
        public const float DamagePerStack = 15f;
        public const int MaxStacksCount = 5;
        public const float BonusPerStack = 0.06f;
        const float DecayDelay = 4f;
        const float DecayInterval = 1f;

        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        Health health;
        int stacks;
        float progress;
        float nextDecay;

        public string PassiveName => "Faith";
        public int Stacks => stacks;
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
                return;
            }

            if ((stacks > 0 || progress > 0f) && Time.time >= nextDecay)
            {
                progress = 0f;
                if (stacks > 0) stacks--;
                nextDecay = Time.time + DecayInterval;
            }
        }
    }
}
