using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // Shared pieces of the Drukhari Mandrake's kit: baleflame colours, melee cone hits, the venom passive and the
    // bonus from Shadow Jump.
    public static class MandrakeKit
    {
        public static readonly Color Baleflame = new Color(0.45f, 1f, 0.3f, 0.9f);
        public static readonly Color BaleflameBright = new Color(0.75f, 1f, 0.55f, 0.95f);
        public static readonly Color Shadow = new Color(0.08f, 0.2f, 0.12f, 0.9f);

        // Hunter's Venom (part of his blades, not his passive): every Mandrake attack hits poisoned enemies this much harder.
        public const float VenomBonus = 0.1f;

        // Any strike thrown out of Shadow Veil briefly stuns what it hits (Shadow Rend has its own, longer stun).
        public const float StealthStrikeStun = 0.5f;

        // Hunter's Patience raises the bonus. (Judged on the owner's machine, which only knows whether a target is
        // poisoned, not how many stacks it carries.)
        public static float VenomScale(GameObject mandrake, Health target)
        {
            if (target == null || !Poisoned.IsPoisoned(target.gameObject)) return 1f;
            bool patient = UpgradeState.Has(mandrake, MandrakeUpgrade.HuntersPatience);
            return 1f + (patient ? MandrakeUpgrade.HuntersPatienceVenomBonus : VenomBonus);
        }

        // Called by every attack when it strikes: reveals the Mandrake and spends a waiting Shadow Jump bonus.
        // Returns the flat bonus damage to add to this attack (0 if none) and whether it came out of stealth.
        public static float BeginStrike(GameObject mandrake, out bool fromStealth)
        {
            fromStealth = Stealthed.Break(mandrake);
            // A strike out of an Umbral Riposte counts as a strike from stealth even once the brief vanish has ended.
            var riposte = mandrake.GetComponent<UmbralRiposte>();
            if (riposte != null && riposte.ConsumeShadowStrike()) fromStealth = true;
            var jump = mandrake.GetComponent<ShadowJump>();
            return jump != null ? jump.ConsumeBonus() : 0f;
        }

        static readonly Collider[] hitBuffer = new Collider[64];
        static readonly List<Health> foundBuffer = new List<Health>();

        // Living enemies with any part inside the cone. The list is reused; copy it if you keep it.
        public static List<Health> EnemiesInCone(Health self, Vector3 origin, Vector3 aim, float reach, float arc)
        {
            var found = foundBuffer;
            found.Clear();
            int count = Physics.OverlapSphereNonAlloc(origin + Vector3.up, reach, hitBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = hitBuffer[i];
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target == self || target.Team == self.Team || target.IsDead || found.Contains(target)) continue;

                Vector3 to = c.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(aim, to) > arc * 0.5f) continue;
                found.Add(target);
            }
            return found;
        }

        public static Vector3 FlatAim(Vector3 aim, Transform fallback)
        {
            aim.y = 0f;
            return aim.sqrMagnitude < 0.0001f ? fallback.forward : aim.normalized;
        }
    }
}
