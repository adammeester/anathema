using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: while present, the owner can't move, start abilities or fire (PlayerMovement, Ability.Update,
    // DummyMover and DummyShooter check IsStunned). Shows three stars orbiting above the head.
    // Added to the victim at runtime; re-applying extends the duration.
    public class Stunned : MonoBehaviour
    {
        const float HaloHeight = 2.4f;
        const float HaloRadius = 0.4f;

        float endTime;
        Transform halo;

        public static Stunned Apply(GameObject target, float duration)
        {
            // Online: a remote player's status is applied on their own machine.
            if (Anathema.Net.NetPlayer.TryGetRemote(target, out var remote))
            {
                remote.SendStatus(Anathema.Net.NetPlayer.StatusKind.Stun, duration, 0f, Vector3.zero);
                return null;
            }

            var stunned = target.GetComponent<Stunned>();
            if (stunned == null) stunned = target.AddComponent<Stunned>();
            stunned.endTime = Mathf.Max(stunned.endTime, Time.time + duration);
            return stunned;
        }

        public static bool IsStunned(GameObject target)
        {
            return target.TryGetComponent(out Stunned _);
        }

        void Awake()
        {
            halo = new GameObject("Stun Stars").transform;
            halo.SetParent(transform, false);
            halo.localPosition = Vector3.up * HaloHeight;

            Material star = VisualUtil.UnlitMaterial(new Color(1f, 0.9f, 0.3f, 1f));
            for (int i = 0; i < 3; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.GetComponent<Renderer>().sharedMaterial = star;
                s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.transform.SetParent(halo, false);
                float a = i * Mathf.PI * 2f / 3f;
                s.transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * HaloRadius;
                s.transform.localScale = Vector3.one * 0.16f;
            }
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            halo.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
        }

        void OnDestroy()
        {
            if (halo != null) Destroy(halo.gameObject);
        }
    }
}
