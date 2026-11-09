using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: the owner is consumed by rage. While present they are immune to crowd control (stun, silence,
    // root, panic, maim and knockback all refuse to apply, in each status's Apply), attack 20% faster
    // (Ability.Update shortens the cooldown of abilities that opt in via ScalesWithAttackSpeed) and move 10% faster
    // (PlayerMovement reads MoveSpeedMultiplier). Applying it also breaks any crowd control already on the owner.
    // Shows a pulsing red ring on the floor. Added to the owner at runtime; re-applying extends the duration.
    public class Enraged : MonoBehaviour
    {
        public const float AttackSpeedBonus = 0.2f;
        public const float MoveSpeedBonus = 0.1f;

        static readonly Color RingColor = new Color(1f, 0.1f, 0.05f, 0.7f);

        float endTime;
        GameObject ring;
        Material ringMaterial;

        public static Enraged Apply(GameObject target, float duration)
        {
            var enraged = target.GetComponent<Enraged>();
            if (enraged == null)
            {
                enraged = target.AddComponent<Enraged>();
                enraged.BreakCrowdControl();
            }
            enraged.endTime = Mathf.Max(enraged.endTime, Time.time + duration);
            return enraged;
        }

        public static bool IsEnraged(GameObject target)
        {
            return target.TryGetComponent(out Enraged _);
        }

        // 1.2 while enraged, otherwise 1.
        public static float AttackSpeedMultiplier(GameObject target)
        {
            return IsEnraged(target) ? 1f + AttackSpeedBonus : 1f;
        }

        // 1.1 while enraged, otherwise 1.
        public static float MoveSpeedMultiplier(GameObject target)
        {
            return IsEnraged(target) ? 1f + MoveSpeedBonus : 1f;
        }

        void BreakCrowdControl()
        {
            DestroyIfPresent<Stunned>();
            DestroyIfPresent<Silenced>();
            DestroyIfPresent<Rooted>();
            DestroyIfPresent<Panicked>();
            DestroyIfPresent<Maimed>();
            DestroyIfPresent<Knockback>();
        }

        void DestroyIfPresent<T>() where T : Component
        {
            if (TryGetComponent(out T component)) Destroy(component);
        }

        void Awake()
        {
            ringMaterial = VisualUtil.UnlitMaterial(RingColor);
            ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Enraged Aura";
            Destroy(ring.GetComponent<Collider>());
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.GetComponent<Renderer>().sharedMaterial = ringMaterial;
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.17f, 0f); // above the arena's visible floor discs
            ring.transform.localScale = new Vector3(2f, 0.01f, 2f);
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
            Color c = RingColor;
            c.a = RingColor.a * (0.4f + 0.6f * pulse);
            ringMaterial.color = c;
            float size = 2f + 0.25f * pulse;
            ring.transform.localScale = new Vector3(size, 0.01f, size);
        }

        void OnDestroy()
        {
            if (ring != null) Destroy(ring);
            if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}
