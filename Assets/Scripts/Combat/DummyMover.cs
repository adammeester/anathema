using UnityEngine;

namespace Anathema.Combat
{
    // Test aid: slides the object back and forth along a world-space direction at constant speed,
    // so moving-target aiming can be tested.
    public class DummyMover : MonoBehaviour
    {
        [SerializeField] Vector3 direction = Vector3.right;
        [SerializeField] float distance = 6f;   // total travel from one end to the other
        [SerializeField] float speed = 3f;

        Vector3 origin;
        float travelled;

        public void Configure(Vector3 moveDirection, float travelDistance, float moveSpeed)
        {
            direction = moveDirection;
            distance = travelDistance;
            speed = moveSpeed;
        }

        void Start()
        {
            // Start at one end of the path so the dummy's placed position is the path's midpoint.
            direction.y = 0f;
            direction.Normalize();
            origin = transform.position - direction * (distance * 0.5f);
            travelled = distance * 0.5f;
        }

        void Update()
        {
            if (distance <= 0f || speed <= 0f || Stunned.IsStunned(gameObject) || Rooted.IsRooted(gameObject)) return;

            var maimed = GetComponent<Maimed>();
            float current = speed * (maimed != null ? maimed.SpeedMultiplier : 1f) * ReflectDome.SlowMultiplier(gameObject);

            // Panicked dummies run off; the patrol path is re-anchored where they stop so they resume from there.
            if (Panicked.TryGetFleeDirection(gameObject, out Vector3 flee))
            {
                transform.position += flee * (current * Time.deltaTime);
                origin = transform.position - direction * (distance - Mathf.Abs(travelled - distance));
                return;
            }

            // Triangle wave: constant speed with an instant turnaround at each end.
            travelled = Mathf.Repeat(travelled + current * Time.deltaTime, distance * 2f);
            float t = distance - Mathf.Abs(travelled - distance);
            transform.position = origin + direction * t;
        }
    }
}
