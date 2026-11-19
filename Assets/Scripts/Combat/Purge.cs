using Anathema.Abilities.PlagueSurgeon;
using UnityEngine;

namespace Anathema.Combat
{
    // Strips every crowd control effect and debuff off a character (the Hospitaller's Absolution): stun, root, silence,
    // panic, slow, maim and petrify; bleed, burn, balefire and poison; the Curse of the Rubricae, Leech Draught,
    // Rad-Saturation, and any healing cut or extra damage taken (Penance, Holy Pyre). Knockbacks already in flight and
    // effects the character chose (Timeslip, buffs, shields) are left alone.
    //
    // Online: control effects live on both the server's copy (it moves the character) and the owner's (it predicts
    // movement and blocks ability use with them); damage over time, marks and healing cuts live on the server's only.
    // So the purge runs on the server, which strips its copy and forwards it to the owner (NetPlayer, StatusKind.Purge).
    // Replicas follow the published status masks.
    public static class Purge
    {
        public static void Apply(GameObject target, GameObject source = null)
        {
            if (target == null || !target.TryGetComponent(out Health holder) || holder.IsDead || holder.Structure) return;

            if (!holder.IsAuthority)
            {
                if (holder.Net != null)
                    holder.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Purge, 0f, 0f, Vector3.zero, source);
                return;
            }

            Strip(target, true);
            if (holder.Net != null && holder.Net.IsSpawned)
                holder.Net.ForwardStatusToOwner(Anathema.Net.NetPlayer.StatusKind.Purge, 0f, 0f, Vector3.zero, source);
        }

        // Runs on the server's copy (authority) and on the owner's copy. Everything here is safe on either.
        public static void Strip(GameObject target, bool authority)
        {
            if (target == null) return;

            Remove<Stunned>(target);
            Remove<Rooted>(target);
            Remove<Silenced>(target);
            Remove<Panicked>(target);
            Remove<Slowed>(target);
            Remove<Maimed>(target);
            Petrified.Break(target);

            if (!authority) return;

            if (Bleeding.IsBleedingForReal(target)) Object.Destroy(target.GetComponent<Bleeding>());
            if (Burning.IsBurningForReal(target)) Object.Destroy(target.GetComponent<Burning>());
            if (Balefire.IsBurningForReal(target)) Object.Destroy(target.GetComponent<Balefire>());
            if (Poisoned.IsPoisonedForReal(target)) Object.Destroy(target.GetComponent<Poisoned>());
            if (RubricMark.IsMarkedForReal(target)) Object.Destroy(target.GetComponent<RubricMark>());
            Remove<Irradiated>(target);
            Remove<LeechMark>(target);
            if (target.TryGetComponent(out Health health)) health.ClearAfflictions();
        }

        static void Remove<T>(GameObject target) where T : Component
        {
            if (target.TryGetComponent(out T c)) Object.Destroy(c);
        }
    }
}
