using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: the character has half-faded into the shadows (the Mandrake's Shadow-Kin passive). Its body is drawn as
    // a dark, translucent silhouette on every screen: harder to pick out, but unlike Stealthed it is still there to see
    // (bars, tag and targeting are unaffected). The owner adds and removes it; online it is published in NetPlayer's
    // status mask and every other machine draws a copy (ShowReplica).
    public class Faded : MonoBehaviour
    {
        const string GhostKey = "Faded";
        static readonly Color Shade = new Color(0.12f, 0.2f, 0.32f, 0.45f);

        Health health;

        public static bool IsFaded(GameObject target) => target != null && target.TryGetComponent(out Faded _);

        public static void Apply(GameObject target)
        {
            if (target != null && !target.TryGetComponent(out Faded _)) target.AddComponent<Faded>();
        }

        public static void Remove(GameObject target)
        {
            if (target != null && target.TryGetComponent(out Faded f)) Destroy(f);
        }

        public static void ShowReplica(GameObject target) => Apply(target);

        void Awake()
        {
            health = GetComponent<Health>();
            GhostLook.Apply(gameObject, GhostKey, Shade, 5); // stealth's ghost (priority 10) wins while both are up
        }

        void Update()
        {
            if (health != null && health.IsDead) Destroy(this);
        }

        void OnDestroy()
        {
            GhostLook.Release(gameObject, GhostKey);
        }
    }
}
