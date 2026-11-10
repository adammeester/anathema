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
        }
    }
}
