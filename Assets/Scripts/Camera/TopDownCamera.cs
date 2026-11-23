using UnityEngine;

namespace Anathema.CameraRig
{
    // Battlerite-style camera: fixed angle and distance, centred on the target. No rotation or zoom.
    [RequireComponent(typeof(Camera))]
    public class TopDownCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float pitch = 55f;
        [SerializeField] float yaw = 0f;
        [SerializeField] float distance = 29f;
        [SerializeField] float fieldOfView = 30f;
        [SerializeField] float targetHeight = 1f;
        [Tooltip("0 = rigidly locked to the player. Small values add a little follow lag.")]
        [SerializeField] float smoothTime = 0f;

        Vector3 velocity;

        // A brief shake for a big impact on the local player's own screen (e.g. the Exsanguinating Arc). The stronger
        // of two overlapping shakes wins.
        static float shakeStrength;
        static float shakeStart;
        static float shakeEnd;

        public static void Shake(float strength, float seconds)
        {
            float remaining = Time.time < shakeEnd ? shakeStrength * (shakeEnd - Time.time) / Mathf.Max(0.01f, shakeEnd - shakeStart) : 0f;
            if (strength < remaining) return;
            shakeStrength = strength;
            shakeStart = Time.time;
            shakeEnd = Time.time + Mathf.Max(0.01f, seconds);
        }

        public void SetTarget(Transform t)
        {
            target = t;
        }

        void Awake()
        {
            GetComponent<Camera>().fieldOfView = fieldOfView;
        }

        void LateUpdate()
        {
            if (target == null) return;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * targetHeight;
            Vector3 desired = focus - rotation * Vector3.forward * distance;

            transform.rotation = rotation;
            transform.position = smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
                : desired;

            if (Time.time < shakeEnd)
            {
                float fade = (shakeEnd - Time.time) / Mathf.Max(0.01f, shakeEnd - shakeStart);
                transform.position += Random.insideUnitSphere * (shakeStrength * fade);
            }
        }
    }
}
