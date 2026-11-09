using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // Grey Knight Interceptor's counter payoff: a halberd riposte that damages the attacker, then a short window in
    // which left-clicking psychically teleports to the aimed point and charges the next halberd sweep.
    [DefaultExecutionOrder(100)] // keep in step with Counter; not every Unity version inherits the attribute
    public class InterceptorCounter : Counter
    {
        [SerializeField] float riposteDamage = 15f;
        [SerializeField] float riposteRadius = 1.6f;
        [SerializeField] float energyPerHit = 10f;
        [SerializeField] Color riposteColor = new Color(1f, 0.95f, 0.6f, 0.9f);

        [Header("Psychic teleport")]
        [Tooltip("How long after a successful counter a left click teleports.")]
        [SerializeField] float teleportWindow = 2.5f;
        [SerializeField] float teleportRange = 9f;
        [Tooltip("How long the next sweep stays empowered after teleporting.")]
        [SerializeField] float empowerDuration = 3f;
        [SerializeField] Color teleportColor = new Color(0.75f, 0.5f, 1f, 0.8f);

        CharacterController controller;
        HalberdSweep sweep;
        GameObject marker;
        bool windowOpen;
        bool swallowClick;
        float windowEnd;

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            sweep = GetComponent<HalberdSweep>();

            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Teleport Marker";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
            marker.GetComponent<Renderer>().sharedMaterial = VisualUtil.UnlitMaterial(teleportColor);
            marker.SetActive(false);
        }

        void OnDestroy()
        {
            if (marker != null) Destroy(marker);
            PrimaryFireBlocked = false;
        }


        protected override void Update()
        {
            base.Update();
            if (IsReplica) return; // the teleport window and click handling belong to the owner

            // Basic attack runs earlier in the frame, so this flag (set last frame) is what it sees: the teleport click
            // never fires a shot, and a held button stays quiet until released.
            if (swallowClick && !Input.GetMouseButton(0)) swallowClick = false;
            PrimaryFireBlocked = windowOpen || swallowClick;

            if (!windowOpen) return;

            if (Time.time >= windowEnd || Health.IsDead)
            {
                CloseWindow();
                return;
            }

            Vector3 destination = FindDestination();
            marker.transform.position = destination + Vector3.up * 0.05f;

            if (Input.GetMouseButtonDown(0))
            {
                swallowClick = true;
                Teleport(destination);
            }
        }

        protected override void Cancel()
        {
            base.Cancel();
            CloseWindow(); // C (or death) also forfeits the teleport
        }

        protected override void OnCounterSuccess(GameObject attacker)
        {
            SlashEffect.Spawn(transform.position, Aim.AimDirection, riposteRadius, 360f, 0.4f, riposteColor);
            GreyKnightSound.Parry();

            if (attacker != null)
            {
                Health target = attacker.GetComponentInParent<Health>();
                if (target != null && target.Team != Health.Team && target.TakeDamage(riposteDamage, gameObject))
                {
                    Energy energy = GetComponent<Energy>();
                    if (energy != null) energy.Gain(energyPerHit);
                }
            }

            if (sweep != null)
            {
                windowOpen = true;
                windowEnd = Time.time + teleportWindow;
                marker.SetActive(true);
            }
        }

        void CloseWindow()
        {
            windowOpen = false;
            if (marker != null) marker.SetActive(false);
        }

        // The aimed ground point, limited to teleportRange and stopped short of walls and other solid scenery.
        Vector3 FindDestination()
        {
            Vector3 from = transform.position;
            Vector3 to = Aim.AimPoint - from;
            to.y = 0f;
            float distance = Mathf.Min(to.magnitude, teleportRange);
            if (distance < 0.01f) return from;
            Vector3 direction = to.normalized;

            const float radius = 0.5f;
            RaycastHit[] hits = Physics.SphereCastAll(from + Vector3.up, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<Health>() != null) continue; // characters don't block a blink
                if (hit.distance <= 0f) continue; // already overlapping at the start
                distance = Mathf.Min(distance, hit.distance);
            }

            return from + direction * distance;
        }

        void Teleport(Vector3 destination)
        {
            Vector3 origin = transform.position;
            // CharacterController overrides transform.position writes unless it is switched off first.
            if (controller != null) controller.enabled = false;
            transform.position = destination;
            if (controller != null) controller.enabled = true;

            AbilityFx.Teleport(origin, destination, teleportColor);
            CloseWindow();
            if (sweep != null) sweep.Empower(empowerDuration);
        }
    }
}
