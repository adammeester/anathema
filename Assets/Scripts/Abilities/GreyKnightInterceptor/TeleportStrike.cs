using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // E: Teleport Strike. The Interceptor's teleporter flares and he vanishes, reappearing at the cursor (pulled in to
    // max range, stopping short of walls and solid scenery; other characters don't block it) in a crack of psychic
    // force that damages every enemy around the landing spot. His way in and out of a fight; it does no crowd control.
    // Under the Purifier Order the burst sets enemies alight.
    //
    // Online: the owner moves (the server allows it through MovementRange) and deals the damage; every machine replays
    // the flash and the burst from the cue.
    public class TeleportStrike : Ability
    {
        [SerializeField] float range = 8f;
        [SerializeField] float burstRadius = 2.5f;
        [SerializeField] float damage = 12f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] Color color = new Color(0.75f, 0.5f, 1f, 0.8f);

        CharacterController controller;
        Energy energy;
        Vector3 replicaFrom;

        public override string DisplayName => "Teleport Strike";
        public override string KeyLabel => "E";
        public override float MovementRange => range;
        public override float MovementWindow => 1f;

        void Reset()
        {
            cooldown = 10f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.E) && !Rooted.IsRooted(gameObject);

        protected override void Activate()
        {
            Vector3 from = transform.position;
            Vector3 to = FindDestination(from);

            // A replica is moved by the synced position, which may already have arrived; the flash still runs from
            // where the owner was aiming from.
            if (!IsReplica)
            {
                if (controller != null) controller.enabled = false;
                transform.position = to;
                if (controller != null) controller.enabled = true;
            }

            AbilityFx.Teleport(from, to, color);
            GreyKnightSound.Blink();
            PsychicFireRing.Spawn(to, burstRadius, 0.3f);
            if (IsReplica) return;

            int hits = 0;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team) continue;
                Vector3 d = h.transform.position - to;
                d.y = 0f;
                if (d.magnitude > burstRadius) continue;
                hits++;
                h.Strike(GreyKnightUpgrade.Ignited(gameObject, Hit.Direct(damage).AsAoe()), gameObject, landed =>
                {
                    if (!landed) return;
                    HitMarkerSound.Play();
                    if (energy != null) energy.Gain(energyPerHit);
                });
            }
            if (hits > 0) GreyKnightSound.PsychicImpact(hits);
        }

        // The aimed ground point, limited to range and stopped short of walls and other solid scenery.
        Vector3 FindDestination(Vector3 from)
        {
            Vector3 to = Aim.AimPoint - from;
            to.y = 0f;
            float distance = Mathf.Min(to.magnitude, range);
            if (distance < 0.01f) return from;
            Vector3 direction = to.normalized;

            const float radius = 0.5f;
            foreach (RaycastHit hit in Physics.SphereCastAll(from + Vector3.up, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<Health>() != null) continue; // characters don't block a blink
                if (hit.distance <= 0f) continue; // already overlapping at the start
                distance = Mathf.Min(distance, hit.distance);
            }
            return from + direction * distance;
        }

        protected override void Cancel()
        {
        }
    }
}
