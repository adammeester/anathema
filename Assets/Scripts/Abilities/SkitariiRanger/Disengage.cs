using Anathema.Abilities.Psyker;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // E: a quick servo-assisted leap straight away from the cursor, stopping short of walls. Gets the Ranger out of
    // melee and back to sniping range. Replicas only play the flashes; the owner's synced position does the rest.
    public class Disengage : Ability
    {
        [SerializeField] float distance = 7f;
        [SerializeField] float duration = 0.22f;
        [SerializeField] float wallPadding = 0.8f;
        [SerializeField] float arcHeight = 3f;
        [SerializeField] float maxVaultHeight = 2.5f;
        [Header("EX: Caltrop Burst (tick to enable; off = no EX on this ability)")]
        [SerializeField] bool caltropBurstEnabled = false;
        [SerializeField] float exEnergyCost = 30f;
        [SerializeField] float caltropRadius = 3f;
        [SerializeField] float caltropDuration = 4f;
        [SerializeField] float caltropDamagePerSecond = 12f;
        [SerializeField] float caltropSlow = 0.4f;

        Energy energy;
        CharacterController controller;
        bool dashing;
        float dashStart, dashEnd, dashTravel;
        Vector3 dashVelocity, dashOrigin, dashAway;

        public override string DisplayName => "Disengage";
        public override string KeyLabel => "E";
        public override bool RootsMovement => dashing;
        public override float ExCost => caltropBurstEnabled ? exEnergyCost : 0f;
        public override string ExName => "Caltrop Burst";
        public override string ExDescription => "Caltrop Burst";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 7f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.E);
        }

        protected override void Activate()
        {
            // The EX energy is spent here (owner only); replicas already have ExActivation from the owner's cue.
            if (!IsReplica) ExActivation = caltropBurstEnabled && TrySpendEx(energy, exEnergyCost);

            Vector3 from = transform.position;
            Vector3 away = from - Aim.AimPoint;
            away.y = 0f;
            away = away.sqrMagnitude < 0.0001f ? -transform.forward : away.normalized;

            // Low obstacles (barricades, bunkers) are leaped over; tall ones (the perimeter wall) still stop the dash.
            float travel = distance;
            foreach (RaycastHit hit in Physics.SphereCastAll(from + Vector3.up, 0.4f, away, travel, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.collider.bounds.max.y - from.y <= maxVaultHeight) continue;
                if (hit.distance > 0f) travel = Mathf.Min(travel, Mathf.Max(0f, hit.distance - wallPadding));
            }

            // Don't land inside something we vaulted: shorten until the landing spot is clear.
            while (travel >= 0.5f && !LandingClear(from + away * travel)) travel -= 0.5f;

            Vector3 to = from + away * travel;
            PsykerFx.Ring(from, 0.3f, 2.2f, 0.3f, RangerFx.Cyan, 0.25f);
            RangerFx.Tracer(from + Vector3.up * 0.9f, to + Vector3.up * 0.9f, 0.5f, RangerFx.Cyan, 0.3f);
            PsykerFx.Ring(to, 0.2f, 1.6f, 0.35f, RangerFx.Cyan, 0.18f);

            // EX: the launch point is left strewn with caltrops that cut and slow anyone chasing through it.
            if (ExActivation && caltropBurstEnabled)
            {
                PsykerFx.Ring(from, 0.3f, caltropRadius, 0.4f, RangerFx.Ember, 0.25f);
                ShrapnelPool.Spawn(gameObject, Health.Team, from, caltropRadius, caltropDuration, caltropDamagePerSecond, caltropSlow, !IsReplica);
            }

            if (IsReplica || travel < 0.5f) return;

            dashing = true;
            dashStart = Time.time;
            dashEnd = dashStart + duration;
            dashVelocity = away * (travel / duration);
            dashOrigin = from;
            dashTravel = travel;
            dashAway = away;
        }

        bool LandingClear(Vector3 p)
        {
            foreach (Collider c in Physics.OverlapCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.4f, 0.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(transform) || c.GetComponentInParent<Health>() != null) continue;
                return false;
            }
            return true;
        }

        protected override void Update()
        {
            base.Update();
            if (!dashing) return;

            if (Health.IsDead || Time.time >= dashEnd)
            {
                dashing = false;
                if (!Health.IsDead) SetPosition(dashOrigin + dashAway * dashTravel);
                return;
            }

            // Follow a fixed arc (collision-free so it clears low walls); the landing was validated in Activate.
            float t = Mathf.Clamp01((Time.time - dashStart) / duration);
            Vector3 p = dashOrigin + dashAway * (dashTravel * t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);
            SetPosition(p);
        }

        void SetPosition(Vector3 p)
        {
            if (controller != null && controller.enabled)
            {
                controller.enabled = false;
                transform.position = p;
                controller.enabled = true;
            }
            else transform.position = p;
        }

        protected override void Cancel()
        {
            dashing = false;
        }
    }
}
