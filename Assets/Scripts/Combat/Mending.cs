using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: the Thousand Sons Sorcerer's Mending, a delayed heal. After `seconds` it restores `amount`
    // (healable health only, like any heal), credited to the Sorcerer. Dust Form sets it off early (Bloom).
    // Re-applying refreshes the timer and keeps the larger heal.
    // Held on the server's copy (heals are resolved there); everyone else draws a visual-only copy from the server's
    // published state (NetPlayer.SyncServerFx).
    public class Mending : MonoBehaviour
    {
        const int Motes = 5;

        public static readonly Color Glow = new Color(0.45f, 0.9f, 1f, 0.85f);

        float endTime;
        float amount;
        GameObject source;
        Health health;
        bool visualOnly;

        Transform[] motes;
        Material moteMaterial;

        public static void Apply(GameObject target, GameObject healer, float amount, float seconds)
        {
            if (target == null || amount <= 0f || seconds <= 0f) return;
            if (!target.TryGetComponent(out Health h) || h.Structure || h.IsDead) return;
            if (!h.IsAuthority)
            {
                if (h.Net != null) h.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Mending, amount, seconds, Vector3.zero, healer);
                return;
            }

            var m = target.GetComponent<Mending>();
            if (m == null) m = target.AddComponent<Mending>();
            m.visualOnly = false;
            m.source = healer;
            m.amount = Mathf.Max(Time.time < m.endTime ? m.amount : 0f, amount);
            m.endTime = Time.time + seconds;
        }

        // Heals now, if `target` carries a Mending. Sent to the server from anywhere else.
        public static void Bloom(GameObject target, GameObject by)
        {
            if (target == null || !target.TryGetComponent(out Health h) || h.IsDead) return;
            if (!h.IsAuthority)
            {
                if (h.Net != null && IsMending(target)) h.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.MendingBloom, 0f, 0f, Vector3.zero, by);
                return;
            }
            if (target.TryGetComponent(out Mending m) && !m.visualOnly) m.Finish();
        }

        public static bool IsMending(GameObject target) => target != null && target.TryGetComponent(out Mending _);
        public static bool IsMendingForReal(GameObject target) => target.TryGetComponent(out Mending m) && !m.visualOnly;

        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Mending _)) return;
            var m = target.AddComponent<Mending>();
            m.visualOnly = true;
            m.endTime = float.MaxValue;
        }

        void Finish()
        {
            float heal = amount;
            endTime = 0f;
            Destroy(this);
            Anathema.Abilities.Psyker.PsykerFx.Ring(transform.position + Vector3.up * 0.1f, 0.2f, 1.3f, 0.35f, Glow, 0.14f);
            if (health != null && heal > 0f) health.Heal(heal, source);
        }

        void Awake()
        {
            health = GetComponent<Health>();
            moteMaterial = VisualUtil.UnlitMaterial(Glow, 3003);
            motes = new Transform[Motes];
            for (int i = 0; i < Motes; i++)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "Mending Mote";
                Destroy(g.GetComponent<Collider>());
                var r = g.GetComponent<Renderer>();
                r.sharedMaterial = moteMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.transform.SetParent(transform, false);
                g.transform.localScale = Vector3.one * 0.11f;
                motes[i] = g.transform;
            }
        }

        void Update()
        {
            if (health == null || health.IsDead)
            {
                Destroy(this);
                return;
            }
            if (!visualOnly && Time.time >= endTime)
            {
                Finish();
                return;
            }

            // Blue motes of healing dust drift slowly up around the body.
            for (int i = 0; i < motes.Length; i++)
            {
                float t = Mathf.Repeat(Time.time * 0.35f + i / (float)Motes, 1f);
                float a = (i / (float)Motes + Time.time * 0.25f) * Mathf.PI * 2f;
                motes[i].localPosition = new Vector3(Mathf.Cos(a) * 0.5f, 0.3f + 1.8f * t, Mathf.Sin(a) * 0.5f);
                motes[i].localScale = Vector3.one * (0.13f * Mathf.Sin(t * Mathf.PI));
            }
        }

        void OnDestroy()
        {
            if (motes != null) foreach (Transform m in motes) if (m != null) Destroy(m.gameObject);
            if (moteMaterial != null) Destroy(moteMaterial);
        }
    }
}
