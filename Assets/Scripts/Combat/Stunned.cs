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
            // Online: a client asks the server; the server holds the stun (it stops the character there) and tells the
            // victim's machine, which publishes it so every screen draws it.
            if (Anathema.Net.NetPlayer.RouteControl(target, Anathema.Net.NetPlayer.StatusKind.Stun, duration, 0f, Vector3.zero)) return null;

            if (Enraged.IsEnraged(target)) return null;
            duration *= Health.CrowdControlScaleOf(target);

            var stunned = target.GetComponent<Stunned>();
            if (stunned == null) stunned = target.AddComponent<Stunned>();
            stunned.endTime = Mathf.Max(stunned.endTime, Time.time + duration);
            StunFx.Show(target, duration);
            return stunned;
        }

        // Online: draws a stun on a replica (driven by NetPlayer's published status mask); lasts until the mask clears.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Stunned _)) return;
            target.AddComponent<Stunned>().endTime = float.MaxValue;
            StunFx.Show(target, float.MaxValue);
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
