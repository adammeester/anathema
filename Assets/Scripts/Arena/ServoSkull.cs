using UnityEngine;

namespace Anathema.Arena
{
    // Purely decorative floating servo-skull: bobs, drifts around a small orbit and flickers its lamp.
    // Built from primitives by ArenaSceneBuilder; this only animates it (visuals, no gameplay).
    public class ServoSkull : MonoBehaviour
    {
        [SerializeField] float bobHeight = 0.25f;
        [SerializeField] float bobSpeed = 1.3f;
        [SerializeField] float orbitRadius = 1.2f;
        [SerializeField] float orbitSpeed = 0.35f;   // radians per second, sign = direction
        [SerializeField] float flickerSpeed = 14f;

        Vector3 anchor;
        float phase;
        Light lamp;
        float lampBase;

        public void Configure(float orbit, float speed)
        {
            orbitRadius = orbit;
            orbitSpeed = speed;
        }

        void Start()
        {
            anchor = transform.position;
            phase = Mathf.Abs(anchor.x * 12.9898f + anchor.z * 78.233f) % 6.2831f;
            lamp = GetComponentInChildren<Light>();
            if (lamp != null) lampBase = lamp.intensity;
        }

        void Update()
        {
            float t = Time.time * orbitSpeed + phase;
            Vector3 p = anchor;
            p.x += Mathf.Cos(t) * orbitRadius;
            p.z += Mathf.Sin(t) * orbitRadius;
            p.y += Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;

            Vector3 velocity = p - transform.position;
            transform.position = p;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity.normalized, Vector3.up), 4f * Time.deltaTime);

            if (lamp != null)
                lamp.intensity = lampBase * (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * flickerSpeed, phase));
        }
    }
}
