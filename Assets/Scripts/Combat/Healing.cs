using System;
using UnityEngine;

namespace Anathema.Combat
{
    // The one way abilities heal someone else. Health.Heal sends the heal to the server when this machine isn't the
    // authority for the target's health. Heals only the white (recoverable) part. `source` is the healer, for credit.
    public static class Healing
    {
        // Raised on the healer's machine just before a heal is applied or sent: (target, amount, healer). The target's
        // Recoverable is still the value before this heal (e.g. Grateful Patients turns the healing that will land into
        // energy, which lives on the healer's machine).
        public static event Action<Health, float, GameObject> Applying;

        public static void Apply(Health target, float amount, GameObject source = null)
        {
            if (target == null || target.IsDead || amount <= 0f) return;
            Applying?.Invoke(target, amount, source);
            target.Heal(amount, source);
        }
    }
}
