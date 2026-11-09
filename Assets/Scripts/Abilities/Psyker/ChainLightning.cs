using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // RMB: a bolt of white lightning that strikes the first enemy along the aim line, then leaps to the nearest other
    // enemies, each jump weaker than the last. Instant. Hits feed the energy bar.
    public class ChainLightning : Ability
    {
        [SerializeField] float damage = 16f;
        [SerializeField] float range = 12f;
        [SerializeField] float aimRadius = 0.6f;
        [SerializeField] int maxJumps = 2;
        [SerializeField] float jumpRange = 5f;
        [SerializeField] float falloff = 0.7f;
        [SerializeField] float energyPerHit = 6f;
        [SerializeField] float originHeight = 1.25f;

        Energy energy;

        protected override float WindupDuration => 0.35f;
        protected override Color WindupColor => new Color(0.7f, 0.85f, 1f, 1f);

        public override string DisplayName => "Chain Lightning";
        public override string KeyLabel => "RMB";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 5f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButtonDown(1);
        }

        protected override void Activate()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
            Vector3 start = transform.position + Vector3.up * originHeight + dir * 0.6f;

            // First strike: nearest enemy along the line, unless a wall is nearer.
            Health first = null;
            float firstDistance = range;
            float wallDistance = range;
            foreach (RaycastHit hit in Physics.SphereCastAll(start, aimRadius, dir, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform)) continue;
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h == null)
                {
                    if (hit.distance > 0f) wallDistance = Mathf.Min(wallDistance, hit.distance);
                    continue;
                }
                if (h.Team == Health.Team || h.IsDead || hit.distance >= firstDistance) continue;
                first = h;
                firstDistance = hit.distance;
            }

            var points = new List<Vector3> { start };
            if (first == null || firstDistance > wallDistance)
            {
                // Nothing struck: the bolt fizzles out at the end of its range or at the wall.
                points.Add(start + dir * Mathf.Min(wallDistance, range * 0.75f));
                PsykerFx.Bolt(points, 0.2f);
                return;
            }

            var struck = new List<Health>();
            Health current = first;
            for (int jump = 0; jump <= maxJumps && current != null; jump++)
            {
                struck.Add(current);
                points.Add(ChestOf(current));

                if (!IsReplica)
                {
                    float dealt = damage * Mathf.Pow(falloff, jump);
                    if (current.TakeDamage(dealt, gameObject, true))
                    {
                        HitMarkerSound.Play();
                        if (energy != null) energy.Gain(energyPerHit);
                    }
                }

                current = NextTarget(current, struck);
            }

            PsykerFx.Bolt(points, 0.3f);
        }

        Health NextTarget(Health from, List<Health> struck)
        {
            Health best = null;
            float bestDistance = jumpRange;
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.IsDead || h.Team == Health.Team || struck.Contains(h)) continue;
                float d = Vector3.Distance(h.transform.position, from.transform.position);
                if (d >= bestDistance) continue;
                best = h;
                bestDistance = d;
            }
            return best;
        }

        static Vector3 ChestOf(Health h)
        {
            return h.transform.position + Vector3.up * 1.1f;
        }

        protected override void Cancel()
        {
        }
    }
}
