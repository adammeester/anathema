using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // Space: the Psyker steps through the warp, vanishing and reappearing at the cursor (pulled in to max range, and
    // stopped short of walls). She leaves a pool of burning warp-fire where she lands that burns enemies standing in
    // it. Replicas only play the flashes and the flames; the owner's synced position does the rest.
    public class WarpStep : Ability
    {
        [SerializeField] float range = 8f;
        [SerializeField] float wallPadding = 0.8f;
        [SerializeField] float fireRadius = 2.2f;
        [SerializeField] float fireDuration = 4f;
        [SerializeField] float fireDamagePerSecond = 12f;

        CharacterController controller;

        protected override float WindupDuration => 0.25f;
        protected override Color WindupColor => PsykerFx.Cyan;

        public override string DisplayName => "Warp Step";
        public override string KeyLabel => "Space";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 6f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.Space);
        }

        protected override void Activate()
        {
            Vector3 from = transform.position;
            PsykerFx.Ring(from, 0.2f, 1.6f, 0.3f, PsykerFx.Cyan);

            // Both machines work out the same landing spot from the cast cue's aim point.
            Vector3 dir = Aim.AimPoint - from;
            dir.y = 0f;
            float distance = Mathf.Min(range, dir.magnitude);
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;

            // Stop short of any wall or pillar in the way (characters don't block the step).
            Vector3 probe = from + Vector3.up;
            foreach (RaycastHit hit in Physics.SphereCastAll(probe, 0.4f, dir, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance > 0f) distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - wallPadding));
            }

            Vector3 to = from + dir * distance;

            // The owner moves; a replica's position update carries the character to the new spot.
            if (!IsReplica && distance >= 0.5f)
            {
                if (controller != null) controller.enabled = false;
                transform.position = to;
                if (controller != null) controller.enabled = true;
            }

            if (distance >= 0.5f) AbilityFx.Teleport(from, to, PsykerFx.Cyan);
            else PsykerFx.Ring(to, 0.2f, 1.6f, 0.3f, PsykerFx.Cyan);
            FirePool.Spawn(gameObject, Health.Team, to, fireRadius, fireDuration, fireDamagePerSecond, !IsReplica);
        }

        protected override void Cancel()
        {
        }
    }
}
