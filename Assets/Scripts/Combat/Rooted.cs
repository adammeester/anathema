using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: while present, the owner can't walk (PlayerMovement and DummyMover check IsRooted). Unlike a stun
    // they can still aim, attack and use abilities, and forced movement (knockback, dashes) still carries them.
    // Shows a glowing sigil ring on the floor under their feet. Added to the victim at runtime; re-applying extends it.
    public class Rooted : MonoBehaviour
    {
        const float RingRadius = 0.8f;
        const int RingSegments = 40;

        static readonly Color RingColor = new Color(0.85f, 0.7f, 1f, 0.9f);

        float endTime;
        LineRenderer ring;
        Material ringMaterial;

        public static Rooted Apply(GameObject target, float duration)
        {
            // Online: a remote player's status is applied on their own machine.
            if (Anathema.Net.NetPlayer.TryGetRemote(target, out var remote))
            {
                remote.SendStatus(Anathema.Net.NetPlayer.StatusKind.Root, duration, 0f, Vector3.zero);
                return null;
            }

            if (Enraged.IsEnraged(target)) return null;
            duration *= Health.CrowdControlScaleOf(target);

            var rooted = target.GetComponent<Rooted>();
            if (rooted == null) rooted = target.AddComponent<Rooted>();
            rooted.endTime = Mathf.Max(rooted.endTime, Time.time + duration);
            return rooted;
        }

        public static bool IsRooted(GameObject target)
        {
            return target.TryGetComponent(out Rooted _);
        }

        void Awake()
        {
            var go = new GameObject("Root Sigil");
            go.transform.SetParent(transform, false);
            ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.startWidth = ring.endWidth = 0.09f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringMaterial = VisualUtil.UnlitMaterial(RingColor);
            ring.sharedMaterial = ringMaterial;
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            // The ring stays where they were pinned, tracking the feet in case something shoves them.
            Vector3 feet = transform.position;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 10f);
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, new Vector3(feet.x + Mathf.Cos(a) * RingRadius * pulse, 0.08f, feet.z + Mathf.Sin(a) * RingRadius * pulse));
            }
        }

        void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
            if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}
