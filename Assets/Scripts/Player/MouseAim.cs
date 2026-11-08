using Anathema.Combat;
using UnityEngine;

namespace Anathema.Player
{
    // Projects the mouse onto the ground plane under the character and snaps facing to it every frame.
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(Health))]
    public class MouseAim : MonoBehaviour
    {
        Health health;
        Camera cam;

        public Vector3 AimPoint { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        void Awake()
        {
            health = GetComponent<Health>();
            AimPoint = transform.position + AimDirection;
        }

        void Start()
        {
            if (!health.IsReplica) Cursor.lockState = CursorLockMode.Confined;
        }

        void Update()
        {
            // Online: another player's aim comes from the network, not this machine's mouse.
            if (health.IsReplica)
            {
                AimPoint = health.Net.ReplicaAimPoint;
                Vector3 replicaFlat = AimPoint - transform.position;
                replicaFlat.y = 0f;
                if (replicaFlat.sqrMagnitude > 0.01f) AimDirection = replicaFlat.normalized;
                if (!health.IsDead) transform.rotation = Quaternion.LookRotation(AimDirection, Vector3.up);
                return;
            }

            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, transform.position);
            if (!ground.Raycast(ray, out float enter)) return;

            AimPoint = ray.GetPoint(enter);

            Vector3 flat = AimPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) AimDirection = flat.normalized;

            if (!health.IsDead) transform.rotation = Quaternion.LookRotation(AimDirection, Vector3.up);
        }
    }
}
