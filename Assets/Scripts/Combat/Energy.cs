using System;
using UnityEngine;

namespace Anathema.Combat
{
    // Battlerite-style energy: built by landing hits, spent on stronger abilities later. Resets when the owner dies.
    [RequireComponent(typeof(Health))]
    public class Energy : MonoBehaviour
    {
        [SerializeField] float maxEnergy = 100f;
        [SerializeField] float startingEnergy;

        Health health;

        public float Max => maxEnergy;
        public float Current { get; private set; }
        public float Normalized => maxEnergy > 0f ? Current / maxEnergy : 0f;

        public event Action<Energy> Changed;

        void Awake()
        {
            health = GetComponent<Health>();
            Current = Mathf.Clamp(startingEnergy, 0f, maxEnergy);
        }

        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void OnDied(Health _, GameObject killer)
        {
            Current = 0f;
            Changed?.Invoke(this);
        }

        public void Clear()
        {
            Current = 0f;
            Changed?.Invoke(this);
        }

        public void Gain(float amount)
        {
            // Nothing charges during prep: hitting the ready-room training dummies must not build energy for the
            // ultimate (or EX abilities). Energy is also cleared when prep starts and when the round begins.
            if (amount <= 0f || health.IsDead || Anathema.Net.NetGame.InPrep) return;
            Current = Mathf.Min(maxEnergy, Current + amount);
            Changed?.Invoke(this);
        }

        // Returns false (and spends nothing) if there isn't enough.
        public bool TrySpend(float amount)
        {
            // Replicas only replay the owner's ability visuals; the owner already paid for it.
            if (health.IsReplica) return true;
            if (amount > Current) return false;
            Current -= amount;
            Changed?.Invoke(this);
            return true;
        }
    }
}
