using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: the Thousand Sons Sorcerer's Rubric's Touch turns its victims to dust-glass. It takes hold after
    // `delay` seconds; from then on the victim is encased: unable to move or act (it counts as a stun, see
    // Stunned.IsStunned), but wrapped in a glass shell that soaks `shield` damage. Breaking the shell frees them early.
    //
    // Online it is a control effect like a stun (NetPlayer.RouteControl): the server holds the real copy (it stops the
    // character there and its shell soaks the damage) and tells the owner, who enforces it on their own machine. When
    // the server's shell breaks it tells the owner (StatusKind.PetrifyBreak). The owner publishes it in its status mask
    // so every other screen draws the shell.
    public class Petrified : MonoBehaviour
    {
        static readonly Color GlassColor = new Color(0.95f, 0.8f, 0.45f, 0.42f);
        static readonly Color CrackColor = new Color(0.4f, 0.85f, 1f, 0.9f);

        float startTime;
        float endTime;
        float shield;
        bool visualOnly;
        Health health;

        Transform shell;
        Transform warning;
        Material shellMaterial;
        Material warningMaterial;

        // Encases `target` after `delay` seconds, for `seconds`, behind a `shield` damage shell.
        public static void Apply(GameObject target, float seconds, float shieldAmount, float delay, GameObject source = null)
        {
            if (target == null) return;
            if (Anathema.Net.NetPlayer.RouteControl(target, Anathema.Net.NetPlayer.StatusKind.Petrify, seconds, shieldAmount, new Vector3(delay, 0f, 0f), source)) return;
            if (!target.TryGetComponent(out Health h) || h.IsDead || h.Structure) return;
            if (Enraged.IsEnraged(target) || Unstoppable.Is(target)) return;
            seconds *= Health.CrowdControlScaleOf(target);

            var p = target.GetComponent<Petrified>();
            if (p == null) p = target.AddComponent<Petrified>();
            p.health = h;
            p.visualOnly = false;
            p.startTime = Time.time + Mathf.Max(0f, delay);
            p.endTime = p.startTime + seconds;
            p.shield = shieldAmount;
            if (h.IsAuthority) h.StatusAbsorber = p.Absorb;
        }

        // While encased (not during the delay): counts as stunned.
        public static bool IsActive(GameObject target) =>
            target != null && target.TryGetComponent(out Petrified p) && (p.visualOnly || Time.time >= p.startTime);

        // The owner's copy: the server says the shell broke.
        public static void Break(GameObject target)
        {
            if (target != null && target.TryGetComponent(out Petrified p)) p.Shatter();
        }

        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Petrified _)) return;
            var p = target.AddComponent<Petrified>();
            p.health = target.GetComponent<Health>();
            p.visualOnly = true;
            p.startTime = Time.time;
            p.endTime = float.MaxValue;
        }

        // Authority only. The shell soaks hits once it has formed.
        float Absorb(float amount, GameObject source)
        {
            if (Time.time < startTime || shield <= 0f) return amount;
            float taken = Mathf.Min(amount, shield);
            shield -= taken;
            if (shield <= 0f)
            {
                if (health != null && health.Net != null && health.Net.IsSpawned)
                    health.Net.ForwardStatusToOwner(Anathema.Net.NetPlayer.StatusKind.PetrifyBreak, 0f, 0f, Vector3.zero, source);
                Shatter();
            }
            return amount - taken;
        }

        void Shatter()
        {
            if (Time.time >= startTime || visualOnly) PlayShatter(transform.position);
            Destroy(this);
        }

        public static void PlayShatter(Vector3 at)
        {
            Anathema.Abilities.Psyker.PsykerFx.Ring(at + Vector3.up * 0.1f, 0.3f, 1.6f, 0.3f, new Color(0.95f, 0.8f, 0.45f, 0.9f), 0.14f);
            Sfx.Play(Sfx.Impact, 0.4f, 1.3f, 1.5f, 0.4f);
        }

        void Awake()
        {
            shellMaterial = VisualUtil.UnlitMaterial(GlassColor, 3002);
            warningMaterial = VisualUtil.UnlitMaterial(CrackColor, 3001);

            // The glass shell: a tall, slightly faceted capsule of gold-tinted dust-glass around the body.
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            s.name = "Petrify Shell";
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = shellMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.transform.SetParent(transform, false);
            s.transform.localPosition = Vector3.up * 1.05f;
            s.transform.localScale = new Vector3(1.2f, 1.15f, 1.2f);
            shell = s.transform;

            // The warning during the delay: a blue ring of cracks closing in on the feet.
            GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            w.name = "Petrify Warning";
            Destroy(w.GetComponent<Collider>());
            w.GetComponent<Renderer>().sharedMaterial = warningMaterial;
            w.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            w.transform.SetParent(transform, false);
            w.transform.localPosition = Vector3.up * 0.05f;
            warning = w.transform;
        }

        void Update()
        {
            if (health == null || health.IsDead || Time.time >= endTime)
            {
                Destroy(this);
                return;
            }

            bool formed = Time.time >= startTime;
            bool show = !Stealthed.HiddenHere(gameObject);
            shell.gameObject.SetActive(formed && show);
            warning.gameObject.SetActive(!formed && show);
            if (!formed)
            {
                float left = startTime - Time.time;
                float r = Mathf.Lerp(0.7f, 2.2f, Mathf.Clamp01(left));
                warning.localScale = new Vector3(r, 0.01f, r);
                return;
            }

            // The shell glints and settles.
            Color c = GlassColor;
            c.a = GlassColor.a * (0.85f + 0.15f * Mathf.Sin(Time.time * 5f));
            shellMaterial.color = c;
        }

        void OnDestroy()
        {
            if (!visualOnly && health != null && health.StatusAbsorber == (System.Func<float, GameObject, float>)Absorb)
                health.StatusAbsorber = null;
            if (shell != null) Destroy(shell.gameObject);
            if (warning != null) Destroy(warning.gameObject);
            if (shellMaterial != null) Destroy(shellMaterial);
            if (warningMaterial != null) Destroy(warningMaterial);
        }
    }
}
