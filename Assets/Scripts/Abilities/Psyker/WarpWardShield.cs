using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // The Warp Ward upgrade's shield: soaks damage for a few seconds after a Warp Step. It uses Health.BonusAbsorber so
    // it stacks with the Kine Shield instead of replacing it. Only the authority's copy absorbs (damage is resolved on
    // the server online); every machine shows the shell. Re-granting refreshes the shield.
    public class WarpWardShield : MonoBehaviour
    {
        const float ShellRadius = 1.0f;
        static readonly Color ShellColor = new Color(0.6f, 0.4f, 1f, 0.28f);

        Health health;
        bool owner;
        float remaining;
        float strength;
        float endTime;
        Transform shell;
        Material shellMaterial;

        public static void Grant(GameObject player, bool owner, float strength, float duration)
        {
            var ward = player.GetComponent<WarpWardShield>();
            if (ward == null) ward = player.AddComponent<WarpWardShield>();
            ward.Begin(owner, strength, duration);
        }

        void Begin(bool isOwner, float shieldStrength, float duration)
        {
            health = GetComponent<Health>();
            owner = isOwner;
            strength = remaining = shieldStrength;
            endTime = Time.time + duration;

            if (owner && health != null) health.BonusAbsorber = Absorb;

            if (shell == null)
            {
                shellMaterial = VisualUtil.UnlitMaterial(ShellColor, 3001);
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "Warp Ward";
                Destroy(s.GetComponent<Collider>());
                s.GetComponent<Renderer>().sharedMaterial = shellMaterial;
                s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.transform.SetParent(transform, false);
                s.transform.localPosition = Vector3.up * 0.9f;
                s.transform.localScale = Vector3.one * (ShellRadius * 2f);
                shell = s.transform;
            }
        }

        float Absorb(float amount, GameObject source)
        {
            float taken = Mathf.Min(amount, remaining);
            remaining -= taken;
            if (remaining <= 0f)
            {
                End(true);
                if (health != null && health.Net != null && health.Net.IsSpawned)
                    health.Net.ServerShieldBroken(Anathema.Net.NetPlayer.ShieldKind.WarpWard);
            }
            return amount - taken;
        }

        // Online: the server's copy ran the shield out; remove this machine's copy with the break effect.
        public static void Break(GameObject player)
        {
            if (player != null && player.TryGetComponent(out WarpWardShield ward)) ward.End(true);
        }

        void Update()
        {
            if (health != null && health.IsDead || Time.time >= endTime)
            {
                End(false);
                return;
            }

            float strength01 = owner && strength > 0f ? Mathf.Clamp01(remaining / strength) : 1f;
            float timeLeft = endTime - Time.time;
            float flicker = timeLeft < 1f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 18f) : 1f;
            Color c = ShellColor;
            c.a = Mathf.Lerp(0.08f, ShellColor.a, strength01) * flicker;
            shellMaterial.color = c;
        }

        void End(bool broken)
        {
            if (broken) PsykerFx.Ring(transform.position, 0.6f, 2f, 0.3f, ShellColor);
            Destroy(this);
        }

        void OnDestroy()
        {
            if (health != null && owner) health.BonusAbsorber = null;
            if (shell != null) Destroy(shell.gameObject);
            if (shellMaterial != null) Destroy(shellMaterial);
        }
    }
}
