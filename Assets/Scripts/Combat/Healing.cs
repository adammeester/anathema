using UnityEngine;

namespace Anathema.Combat
{
    // The one way abilities heal someone else. Online, a remote player's health is owned by their machine, so the
    // heal is forwarded there (like damage); otherwise it is applied directly. Heals only the white (recoverable) part.
    public static class Healing
    {
        public static void Apply(Health target, float amount)
        {
            if (target == null || target.IsDead || amount <= 0f) return;

            if (Anathema.Net.NetPlayer.TryGetRemote(target.gameObject, out var remote))
                remote.SendStatus(Anathema.Net.NetPlayer.StatusKind.Heal, amount, 0f, Vector3.zero);
            else
                target.Heal(amount);
        }
    }
}
