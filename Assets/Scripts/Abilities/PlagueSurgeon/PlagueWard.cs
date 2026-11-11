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
        public const float SpreadDamagePerSecond = 3.5f;
        public const float SpreadDuration = 5f;

        const float ShellRadius = 1.5f;

        GameObject source;
        Health health;
        float remaining;
        float endTime;
        float maxStrength;
        Transform shell;
        Material shellMaterial;
        bool replica; // another machine's holder: shows the shell only, the holder's own machine does the absorbing

        // Online: draws the ward on a replica of the holder (driven by NetPlayer's published status mask).
        public static void ShowReplica(GameObject target)
        {
            if (target.TryGetComponent(out PlagueWard _)) return;
            var holder = target.GetComponent<Health>();
            if (holder == null) return;
            var ward = target.AddComponent<PlagueWard>();
            ward.health = holder;
            ward.replica = true;
            ward.maxStrength = ward.remaining = 1f;
            ward.endTime = float.MaxValue;
        }

        public static bool IsWarded(GameObject target) => target.TryGetComponent(out PlagueWard w) && !w.replica;

        public static PlagueWard Apply(GameObject target, GameObject wardSource, float strength, float duration)
        {
            var holder = target.GetComponent<Health>();
            if (holder == null || holder.IsDead) return null;

            // Online: the ward lives on the server's copy of the holder, where their damage is resolved; every machine
            // (the holder's own included) draws it from the server's published state.
            if (!holder.IsAuthority)
            {
                holder.Net.SendStatus(Anathema.Net.NetPlayer.StatusKind.Ward, strength, duration, Vector3.zero, wardSource);
                return null;
            }

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
            // The plague churning inside the ward, for as long as it holds (it goes with the shell).
            Sfx.Loop(Sfx.DiseaseCloud2, shell, 0.35f, 1f, 0.4f);
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
            // The rupture is credited to the surgeon who cast the ward (or the holder, if the surgeon is gone).
            GameObject credit = source != null ? source : gameObject;

            Infestation.BurstEffect.Spawn(center, SpreadRadius);

            // Fecund Ward: the rupture also mends the holder's side.
            bool fecund = source != null && PlagueSurgeonUpgradeEffects.Has(source, PlagueSurgeonUpgrade.FecundWard);

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead) continue;
                if (h.Team == team)
                {
                    if (!fecund) continue;
                    Vector3 toAlly = h.transform.position - center;
                    toAlly.y = 0f;
                    if (toAlly.magnitude <= SpreadRadius) Healing.Apply(h, PlagueSurgeonUpgrade.FecundHeal, credit);
                    continue;
                }

                Vector3 offset = h.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > SpreadRadius) continue;

                Festering.Apply(h.gameObject, credit, null, SpreadDuration, SpreadDamagePerSecond, SpreadStacks);
            }

            Destroy(this);
        }

        void OnDestroy()
        {
            // The holder's machine ruptured (or lost) the ward: show the burst here too.
            if (replica && health != null && !health.IsDead && !ruptured) Infestation.BurstEffect.Spawn(transform.position, SpreadRadius);
            if (!replica && health != null && health.Absorber == (System.Func<float, GameObject, float>)Absorb) health.Absorber = null;
            if (shell != null) Destroy(shell.gameObject);
            if (shellMaterial != null) Destroy(shellMaterial);
        }
    }
}
