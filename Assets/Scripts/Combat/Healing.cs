using UnityEngine;

namespace Anathema.Combat
{
    // The one way abilities heal someone else. Health.Heal sends the heal to the server when this machine isn't the
    // authority for the target's health. Heals only the white (recoverable) part. `source` is the healer, for credit.
    public static class Healing
    {
        public static void Apply(Health target, float amount, GameObject source = null)
        {
            if (target == null || target.IsDead || amount <= 0f) return;
            target.Heal(amount, source);
        }
    }
}
