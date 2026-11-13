using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // Passive, Soul Harvest: every poison tick or Glimmersteel Blade hit that lands on an enemy below 40% health takes
    // 1s off Shadow Veil's cooldown, up to 3s from each target. A target's allowance starts over once it is back above
    // 40% health (healed, or respawned for a new round).
    // Shadow Veil's cooldown lives on the owner's machine. Blade hits are already known there; poison ticks are dealt on
    // the server, which tells the owner through NetPlayer.ServerSoulHarvest.
    public class SoulHarvest : MonoBehaviour
    {
        public const float HealthThreshold = 0.4f;
        public const float RefundPerProc = 1f;
        public const float RefundCapPerTarget = 3f;

        readonly Dictionary<Health, float> refunded = new Dictionary<Health, float>();
        readonly List<Health> expired = new List<Health>();
        Health health;
        ShadowVeil veil;
        ShadowJump jump;

        public static bool IsLow(Health target) =>
            target != null && !target.IsDead && target.Max > 0f && target.Current < target.Max * HealthThreshold;

        void Awake()
        {
            health = GetComponent<Health>();
            veil = GetComponent<ShadowVeil>();
            jump = GetComponent<ShadowJump>();
        }

        void OnEnable() => Poisoned.Ticked += OnPoisonTick;
        void OnDisable() => Poisoned.Ticked -= OnPoisonTick;

        // Authority (server or offline): a poison tick just landed. Ticks this Mandrake caused on a low target count.
        void OnPoisonTick(Health victim, GameObject source)
        {
            if (source != gameObject || victim == null || victim.Team == health.Team || !IsLow(victim)) return;
            if (health.IsReplica) health.Net.ServerSoulHarvest(victim.gameObject); // the server's copy of a remote Mandrake
            else Harvest(victim);
        }

        // Owner: a blade hit just landed. `healthBefore` is the target's health as the hit was thrown, so a hit that
        // takes it below the threshold counts even before the server's new health value has arrived.
        public static void NoteBladeHit(GameObject mandrake, Health target, float healthBefore, float damage)
        {
            if (mandrake == null || target == null || target.IsDead || !mandrake.TryGetComponent(out SoulHarvest harvest)) return;
            float after = Mathf.Min(target.Current, healthBefore - damage);
            if (after > 0f && after < target.Max * HealthThreshold) harvest.Harvest(target);
        }

        // Owner: takes up to 1s off Shadow Veil, within this target's allowance. Only cooldown actually removed counts.
        // Reaping Shadows: Shadow Jump gets the same refund, from the same allowance (whichever cooldown took more
        // counts against it).
        public void Harvest(Health target)
        {
            if (veil == null || target == null || health.IsReplica) return;
            refunded.TryGetValue(target, out float used);
            float allowed = Mathf.Min(RefundPerProc, RefundCapPerTarget - used);
            if (allowed <= 0f) return;

            float veilAmount = Mathf.Min(allowed, veil.CooldownRemaining);
            float jumpAmount = 0f;
            if (jump != null && UpgradeState.Has(gameObject, MandrakeUpgrade.ReapingShadows))
                jumpAmount = Mathf.Min(allowed, jump.CooldownRemaining);
            float amount = Mathf.Max(veilAmount, jumpAmount);
            if (amount <= 0f) return;

            if (veilAmount > 0f) veil.ReduceCooldown(veilAmount);
            if (jumpAmount > 0f) jump.ReduceCooldown(jumpAmount);
            refunded[target] = used + amount;
        }

        void Update()
        {
            if (refunded.Count == 0) return;
            expired.Clear();
            foreach (var pair in refunded)
                if (pair.Key == null || pair.Key.IsDead || !IsLow(pair.Key)) expired.Add(pair.Key);
            foreach (Health h in expired) refunded.Remove(h);
        }
    }
}
