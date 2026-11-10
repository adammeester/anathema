using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Status effect: a ward of plague-light around its holder. It soaks up incoming damage (AoE and damage over time
    // included) until its strength runs out. When it is destroyed or simply expires, it ruptures: every enemy within
    // SpreadRadius of the holder is infected with a heavy dose of Festering. A holder who dies with the ward still up
    // doesn't rupture it.
    // Added to the holder at runtime. Online it is applied on the holder's own machine (it has to intercept their
    // damage), so casting on a remote ally forwards it there. Re-applying replaces the old ward.
    public class PlagueWard : MonoBehaviour
    {
        // The rupture: a much heavier rot than the basic attack's single stacks.
        public const float SpreadRadius = 6f;
        public const int SpreadStacks = 4;
        public const float SpreadDamagePerSecond = 3f;
        public const float SpreadDuration = 5f;

        const float ShellRadius = 1.5f;

        GameObject source;
        Health health;
        float remaining;
        float endTime;
        float maxStrength;
        Transform shell;
        Material shellMaterial;

        public static PlagueWard Apply(GameObject target, GameObject wardSource, float strength, float duration)
        {
            // Online: a remote player's ward lives on their own machine.
            if (Anathema.Net.NetPlayer.TryGetRemote(target, out var remote))
            {
                remote.SendStatus(Anathema.Net.NetPlayer.StatusKind.Ward, strength, duration, Vector3.zero, wardSource);
                return null;
            }

            var holder = target.GetComponent<Health>();
            if (holder == null || holder.IsDead) return null;

            var existing = target.GetComponent<PlagueWard>();
            if (existing != null) DestroyImmediate(existing);

            var ward = target.AddComponent<PlagueWard>();
            ward.source = wardSource;
            ward.health = holder;
            ward.remaining = ward.maxStrength = strength;
            ward.endTime = Time.time + duration;
            holder.Absorber = ward.Absorb;
            return ward;
        }

        void Awake()
        {
            shellMaterial = VisualUtil.UnlitMaterial(new Color(0.55f, 0.9f, 0.3f, 0.3f), 3000);

            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Plague Ward";
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = shellMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.transform.SetParent(transform, false);
            s.transform.localPosition = Vector3.up;
            s.transform.localScale = Vector3.one * (ShellRadius * 2f);
            shell = s.transform;
        }

        float Absorb(float amount, GameObject damageSource)
        {
            float taken = Mathf.Min(amount, remaining);
            remaining -= taken;
            if (remaining <= 0f) Rupture();
            return amount - taken;
        }

        void Update()
        {
            if (health == null || health.IsDead)
            {
                Destroy(this);
                return;
            }

            if (Time.time >= endTime)
            {
                Rupture();
                return;
            }

            // Thins out as it takes damage and flickers faster as it runs out of time.
            float strength01 = maxStrength > 0f ? Mathf.Clamp01(remaining / maxStrength) : 0f;
            float timeLeft = endTime - Time.time;
            float flicker = timeLeft < 2f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 18f) : 1f;
            Color c = shellMaterial.color;
            c.a = Mathf.Lerp(0.1f, 0.35f, strength01) * flicker;
            shellMaterial.color = c;
            shell.localScale = Vector3.one * (ShellRadius * 2f * (1f + 0.03f * Mathf.Sin(Time.time * 6f)));
        }

        bool ruptured;

        void Rupture()
        {
            if (ruptured) return;
            ruptured = true;

            Vector3 center = transform.position;
            int team = health.Team;
            // A ward on a remote ally is credited to the ally: the surgeon's character is only a replica here.
            GameObject credit = source != null && !Anathema.Net.NetPlayer.IsReplicaObject(source) ? source : gameObject;

            Infestation.BurstEffect.Spawn(center, SpreadRadius);

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == team) continue;

                Vector3 offset = h.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > SpreadRadius) continue;

                Festering.Apply(h.gameObject, credit, null, SpreadDuration, SpreadDamagePerSecond, SpreadStacks);
            }

            Destroy(this);
        }

        void OnDestroy()
        {
            if (health != null && health.Absorber == (System.Func<float, GameObject, float>)Absorb) health.Absorber = null;
            if (shell != null) Destroy(shell.gameObject);
            if (shellMaterial != null) Destroy(shellMaterial);
        }
    }
}
