using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: a small, short-lived barrier that soaks damage (area and damage over time included) before any
    // other shield. Granting more while one is up tops it up to a cap and refreshes its timer.
    // Used by the Plague Surgeon's Overdose (overhealing becomes a barrier) and Chitinous Wings (a barrier on landing).
    // Like a ward, it lives on the server's copy of the holder, where damage is resolved; every other machine draws a
    // visual-only copy from the server's published state (NetPlayer.SyncServerFx).
    public class Barrier : MonoBehaviour
    {
        const float ShellRadius = 1.1f;
        static readonly Color ShellColor = new Color(0.55f, 0.85f, 1f, 0.25f);

        Health health;
        float remaining;
        float endTime;
        bool visualOnly;
        Transform shell;
        Material shellMaterial;

        // Adds `amount` (up to `cap` in total) and keeps the barrier up for at least `seconds` more.
        public static void Grant(GameObject target, float amount, float seconds, float cap, GameObject source = null)
        {
            if (target == null || amount <= 0f || seconds <= 0f) return;
            if (!target.TryGetComponent(out Health holder) || holder.IsDead) return;

            if (!holder.IsAuthority)
            {
                holder.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Barrier, amount, seconds, new Vector3(cap, 0f, 0f), source);
                return;
            }

            var barrier = target.GetComponent<Barrier>();
            if (barrier == null) barrier = target.AddComponent<Barrier>();
            barrier.health = holder;
            barrier.visualOnly = false;
            barrier.remaining = Mathf.Min(Mathf.Max(cap, amount), (barrier.remaining > 0f ? barrier.remaining : 0f) + amount);
            barrier.endTime = Mathf.Max(barrier.endTime, Time.time + seconds);
            holder.BarrierAbsorber = barrier.Absorb;
        }

        // A barrier that is really soaking damage here (not a drawn copy).
        public static bool IsForReal(GameObject target) => target.TryGetComponent(out Barrier b) && !b.visualOnly;

        // Online: draws a barrier the server reports; lasts until the server says it is gone.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Barrier _)) return;
            var barrier = target.AddComponent<Barrier>();
            barrier.health = target.GetComponent<Health>();
            barrier.visualOnly = true;
            barrier.remaining = 1f;
            barrier.endTime = float.MaxValue;
        }

        void Awake()
        {
            shellMaterial = VisualUtil.UnlitMaterial(ShellColor, 3001);
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Barrier";
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = shellMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.transform.SetParent(transform, false);
            s.transform.localPosition = Vector3.up * 0.95f;
            s.transform.localScale = Vector3.one * (ShellRadius * 2f);
            shell = s.transform;
        }

        float Absorb(float amount, GameObject source)
        {
            float taken = Mathf.Min(amount, remaining);
            remaining -= taken;
            if (remaining <= 0f) Destroy(this);
            return amount - taken;
        }

        void Update()
        {
            if (health == null || health.IsDead || Time.time >= endTime || remaining <= 0f)
            {
                Destroy(this);
                return;
            }

            float timeLeft = endTime - Time.time;
            float flicker = timeLeft < 0.75f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 20f) : 1f;
            Color c = shellMaterial.color;
            c.a = ShellColor.a * flicker;
            shellMaterial.color = c;
            shell.localScale = Vector3.one * (ShellRadius * 2f * (1f + 0.04f * Mathf.Sin(Time.time * 7f)));
        }

        void OnDestroy()
        {
            if (!visualOnly && health != null && health.BarrierAbsorber == (System.Func<float, GameObject, float>)Absorb)
                health.BarrierAbsorber = null;
            if (shell != null) Destroy(shell.gameObject);
            if (shellMaterial != null) Destroy(shellMaterial);
        }
    }

    // Washes bleeding and burning off a character and briefly dulls any other damage over time (Field Triage).
    // Applied on the server's copy, where those effects deal their damage; sent there from any other machine.
    public static class Cleanse
    {
        public static void Apply(GameObject target, float dotReduction, float seconds, GameObject source = null)
        {
            if (target == null || !target.TryGetComponent(out Health holder) || holder.IsDead) return;

            if (!holder.IsAuthority)
            {
                holder.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Cleanse, dotReduction, seconds, Vector3.zero, source);
                return;
            }

            if (Bleeding.IsBleedingForReal(target)) Object.Destroy(target.GetComponent<Bleeding>());
            if (Burning.IsBurningForReal(target)) Object.Destroy(target.GetComponent<Burning>());
            holder.GrantDotWard(dotReduction, seconds);
        }
    }
}
