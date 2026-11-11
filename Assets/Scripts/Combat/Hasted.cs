using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: a short self-buff to movement speed (e.g. the Mandrake landing its Chain Hook). Re-applying refreshes
    // the duration and keeps the stronger bonus. It is a self-buff, so the owner holds it; online the owner publishes it
    // (NetPlayer's status mask) and every other machine draws a copy (ShowReplica). The server's copy uses it to move the
    // character at the same speed the owner predicts; a replica copy always uses DefaultBonus.
    public class Hasted : MonoBehaviour
    {
        public const float DefaultBonus = 0.15f;

        static readonly Color TrailColor = new Color(0.45f, 1f, 0.3f, 0.5f);

        float endTime;
        float bonus = DefaultBonus;
        Health health;
        TrailRenderer trail;
        Material trailMaterial;

        public static bool IsHasted(GameObject target) => target != null && target.TryGetComponent(out Hasted _);

        public static float MoveSpeedMultiplier(GameObject target) =>
            target != null && target.TryGetComponent(out Hasted h) ? 1f + h.bonus : 1f;

        // Owner (or offline): move `bonus` faster (0.15 = +15%) for `duration` seconds.
        public static Hasted Apply(GameObject target, float duration, float bonus = DefaultBonus)
        {
            if (target == null || duration <= 0f) return null;
            var h = target.GetComponent<Hasted>();
            if (h == null)
            {
                h = target.AddComponent<Hasted>();
                h.bonus = 0f;
            }
            h.bonus = Mathf.Max(Time.time < h.endTime ? h.bonus : 0f, Mathf.Max(0f, bonus));
            h.endTime = Mathf.Max(h.endTime, Time.time + duration);
            return h;
        }

        // Online: draws the buff the owner reports; lasts until the owner's mask clears.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Hasted _)) return;
            target.AddComponent<Hasted>().endTime = float.MaxValue;
        }

        void Awake()
        {
            health = GetComponent<Health>();

            // A faint streak trailing from the feet while the buff is up.
            var go = new GameObject("Haste Trail");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.15f;
            trail = go.AddComponent<TrailRenderer>();
            trailMaterial = VisualUtil.UnlitMaterial(TrailColor, 3000);
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.3f;
            trail.startWidth = 0.35f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.startColor = TrailColor;
            trail.endColor = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0f);
        }

        void Update()
        {
            // Hidden with the rest of the body while the character is stealthed on an enemy's screen.
            if (trail != null) trail.enabled = !Stealthed.HiddenHere(gameObject);
            if ((health != null && health.IsDead) || Time.time >= endTime) Destroy(this);
        }

        void OnDestroy()
        {
            if (trail != null) Destroy(trail.gameObject);
            if (trailMaterial != null) Destroy(trailMaterial);
        }
    }
}
