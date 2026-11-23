using Anathema.Combat;
using UnityEngine;

namespace Anathema.Player
{
    // Projects the mouse onto a chest-height plane over the character and snaps facing to it every frame.
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(Health))]
    public class MouseAim : MonoBehaviour
    {
        // Height above the feet the cursor is projected to: about where projectiles fly and bodies are struck.
        public const float AimHeight = 1.1f;

        Health health;
        Camera cam;
        Vector3 frozenOffset = Vector3.forward; // aim point relative to the character, kept while Alt freezes aim

        // On the floor (the character's foot level), directly below the point at AimHeight that the cursor is over.
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

            // In a live fight the cursor is hidden (the aim reticle stands in for it). Holding Alt shows it and freezes aim,
            // so the mouse can go to the HUD (ability tooltips) without spinning the character round.
            bool altHeld = AltHeld;
            bool fighting = !health.IsDead && InFight();
            // The in-game menu (Esc) and a pending surrender vote need the cursor too; the menu also freezes aim like Alt.
            Cursor.visible = !fighting || altHeld || Net.NetGame.WantsCursor;
            if (fighting && (altHeld || Net.NetGame.MenuOpen))
            {
                // Aim held where it was, relative to the character, so it follows them as they move.
                AimPoint = transform.position + frozenOffset;
            }
            else
            {
                if (cam == null) cam = Camera.main;
                if (cam == null) return;

                // The cursor ray meets a plane at chest height, not the floor. The camera is tilted, so the floor point
                // under the cursor sits ~0.85 m up-screen of the body the cursor is over, and shots flying at chest
                // height toward it passed beside the target. AimPoint is the floor point straight below the chest hit.
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                var chest = new Plane(Vector3.up, transform.position + Vector3.up * AimHeight);
                if (!chest.Raycast(ray, out float enter)) return;

                Vector3 hit = ray.GetPoint(enter);
                AimPoint = new Vector3(hit.x, transform.position.y, hit.z);
                frozenOffset = AimPoint - transform.position;

                Vector3 flat = frozenOffset;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f) AimDirection = flat.normalized;
            }

            // Panic takes over aim too: the character faces the way they are running, whatever the cursor says.
            if (Panicked.TryGetFleeDirection(gameObject, out Vector3 flee))
            {
                AimDirection = flee;
                AimPoint = transform.position + flee * 5f;
            }

            if (!health.IsDead) transform.rotation = Quaternion.LookRotation(AimDirection, Vector3.up);
        }

        public static bool AltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        // A round is being fought and no menu needs the mouse: online that is the Playing phase; offline (training) it is
        // any time outside the prep screens and the dev upgrade panel.
        static bool InFight()
        {
            if (Net.NetGame.Online) return Net.NetGame.RoundLive;
            if (Net.OfflinePrep.Active) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Upgrades.UpgradeTestPanel.Showing) return false;
#endif
            return true;
        }

        void OnDisable()
        {
            if (health != null && !health.IsReplica) Cursor.visible = true;
        }
    }
}
