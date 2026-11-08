using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: shoves the owner along a flat direction over a short time. Uses the CharacterController when
    // there is one (so walls stop it); anything else (training dummies) is moved directly, stopping at solid scenery.
    public class Knockback : MonoBehaviour
    {
        Vector3 velocity;
        float endTime;
        CharacterController controller;

        public static void Apply(GameObject target, Vector3 direction, float distance, float duration)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || duration <= 0f) return;

            if (Anathema.Net.NetPlayer.TryGetRemote(target, out var remote))
            {
                remote.SendStatus(Anathema.Net.NetPlayer.StatusKind.Knockback, distance, duration, direction);
                return;
            }

            var knockback = target.GetComponent<Knockback>();
            if (knockback == null) knockback = target.AddComponent<Knockback>();
            knockback.velocity = direction.normalized * (distance / duration);
            knockback.endTime = Time.time + duration;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            Vector3 step = velocity * Time.deltaTime;
            if (controller != null && controller.enabled)
            {
                controller.Move(step);
                return;
            }

            // No controller: stop at solid scenery (anything that isn't a character).
            Vector3 origin = transform.position + Vector3.up;
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, 0.45f, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Health>() != null) continue;
                Destroy(this);
                return;
            }
            transform.position += step;
        }
    }
}
