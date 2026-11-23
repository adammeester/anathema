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

        [Header("Cursor pan (leashed to the character)")]
        [Tooltip("Furthest the view can drift from the character toward the cursor, in metres.")]
        [SerializeField] float maxPanDistance = 4.5f;
        [Tooltip("Fraction of the half-screen height around the centre where the cursor doesn't pan at all.")]
        [Range(0f, 0.9f)] [SerializeField] float panDeadZone = 0.2f;
        [Tooltip("Screen distance (in half-screen heights) at which the pan reaches its full leash length.")]
        [SerializeField] float panFullAt = 1f;
        [Tooltip("Easing on the pan offset so the view glides out and springs back rather than snapping.")]
        [SerializeField] float panSmoothTime = 0.2f;

        Vector3 velocity;
        Vector3 basePosition; // where the camera sits on the character, before the cursor pan and shake
        bool hasBase;
        Vector3 panOffset;
        Vector3 panTarget;
        Vector3 panVelocity;

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
            basePosition = smoothTime > 0f && hasBase
                ? Vector3.SmoothDamp(basePosition, desired, ref velocity, smoothTime)
                : desired;
            hasBase = true;

            UpdatePan();
            transform.position = basePosition + panOffset;

            if (Time.time < shakeEnd)
            {
                float fade = (shakeEnd - Time.time) / Mathf.Max(0.01f, shakeEnd - shakeStart);
                transform.position += Random.insideUnitSphere * (shakeStrength * fade);
            }
        }

        // The view drifts toward the cursor, but only up to maxPanDistance from the character: a leash, not a free camera.
        // The pan is driven by where the cursor sits on screen (not in the world), so moving the camera never moves the
        // pan target and there's no feedback loop chasing the cursor.
        void UpdatePan()
        {
            if (!Player.MouseAim.InFight() || Net.NetGame.MenuOpen || !Application.isFocused)
            {
                panTarget = Vector3.zero; // menus and prep screens: settle back on the character
            }
            else if (!Player.MouseAim.AltHeld) // Alt sends the cursor to the HUD: hold the pan where it was
            {
                float halfHeight = Mathf.Max(1f, Screen.height * 0.5f);
                Vector2 fromCentre = ((Vector2)Input.mousePosition - new Vector2(Screen.width, Screen.height) * 0.5f) / halfHeight;

                float reach = fromCentre.magnitude;
                float t = Mathf.InverseLerp(panDeadZone, Mathf.Max(panDeadZone + 0.01f, panFullAt), reach);
                t = t * t * (3f - 2f * t); // ease in and out of the leash

                Vector3 right = transform.right; right.y = 0f; right.Normalize();
                Vector3 forward = Vector3.Cross(right, Vector3.up); // screen-up, flattened onto the floor
                Vector2 dir = reach > 0.0001f ? fromCentre / reach : Vector2.zero;
                panTarget = (right * dir.x + forward * dir.y) * (maxPanDistance * t);
            }

            panOffset = panSmoothTime > 0f
                ? Vector3.SmoothDamp(panOffset, panTarget, ref panVelocity, panSmoothTime)
                : panTarget;
        }
    }
}
