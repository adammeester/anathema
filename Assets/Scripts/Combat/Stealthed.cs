using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: the character has melted into the shadows. On an enemy's screen every renderer on it (body, health
    // and energy bars, weapon, status effects) is switched off, so there is nothing at all to see; its floating "ENEMY" tag
    // is dropped too (NetGame) and abilities that pick their own targets skip it (HiddenFrom). Its own player and its
    // allies see the body as a translucent, shimmering blue ghost instead. It moves faster while stealthed.
    // It is a self-buff, so the owner holds it: it ends when its time runs out, on death, or when the owner breaks it by
    // attacking (Break). Online the owner publishes it (NetPlayer's status mask) and every other machine draws a copy
    // (ShowReplica); the server's copy uses it to move the character at the same speed the owner predicts.
    // Attacks still connect with a hidden character: stealth hides, it doesn't protect.
    public class Stealthed : MonoBehaviour
    {
        public const float MoveSpeedBonus = 0.25f;

        static readonly Color Ghost = new Color(0.3f, 0.6f, 1f, 0.35f);

        float endTime;
        Health health;
        bool hidden;
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        const string GhostKey = "Stealth";

        public static bool IsStealthed(GameObject target) => target != null && target.TryGetComponent(out Stealthed _);

        public static float MoveSpeedMultiplier(GameObject target) => IsStealthed(target) ? 1f + MoveSpeedBonus : 1f;

        // True when this machine's player is an enemy of the stealthed character, so it is drawn invisible here.
        public static bool HiddenHere(GameObject target) => target != null && target.TryGetComponent(out Stealthed s) && s.hidden;

        // True when `target` is stealthed and not on `viewerTeam`: abilities that pick their own targets (homing,
        // chaining, nearest-enemy) must skip it. Unlike HiddenHere it gives the same answer on every machine.
        public static bool HiddenFrom(GameObject target, int viewerTeam) =>
            target != null && target.TryGetComponent(out Stealthed _) && target.TryGetComponent(out Health h) && h.Team != viewerTeam;

        // Owner (or offline): enter stealth for `duration` seconds (re-applying extends it).
        public static Stealthed Apply(GameObject target, float duration)
        {
            if (target == null) return null;
            var s = target.GetComponent<Stealthed>();
            if (s == null) s = target.AddComponent<Stealthed>();
            s.endTime = Mathf.Max(s.endTime, Time.time + duration);
            return s;
        }

        // Online: draws the stealth the owner reports; lasts until the owner's mask clears.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Stealthed _)) return;
            target.AddComponent<Stealthed>().endTime = float.MaxValue;
        }

        // Ends stealth (an attack reveals the character). Returns whether it was stealthed.
        public static bool Break(GameObject target)
        {
            if (target == null || !target.TryGetComponent(out Stealthed s)) return false;
            Destroy(s);
            return true;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            Puff(transform.position);
        }

        bool IsEnemyOfLocalViewer()
        {
            var local = Anathema.Net.NetPlayer.Local;
            if (local == null || local.gameObject == gameObject || health == null || local.Health == null) return false;
            return local.Health.Team != health.Team;
        }

        void Update()
        {
            if (health == null || health.IsDead || Time.time >= endTime)
            {
                Destroy(this);
                return;
            }

            // Decided every frame (cheap), so a team change or the local player spawning late is picked up.
            bool shouldHide = IsEnemyOfLocalViewer();
            if (shouldHide != hidden)
            {
                if (shouldHide) GhostLook.Release(gameObject, GhostKey);
                else ShowRenderers();
                hidden = shouldHide;
            }

            if (hidden) HideRenderers();
            else GhostLook.Apply(gameObject, GhostKey, Ghost, 10);
        }

        // Every frame: effects can be added at any moment (a wind-up orb, a bleed, a stun's stars) and must never flash
        // up on an enemy's screen.
        void HideRenderers()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
        }

        void ShowRenderers()
        {
            foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
            hiddenRenderers.Clear();
        }

        // A short burst of dark smoke where the character slips in or out of the shadows.
        static void Puff(Vector3 at)
        {
            var root = new GameObject("Shadow Puff");
            root.transform.position = at;
            var fx = root.AddComponent<ShadowPuff>();
            fx.Build();
        }

        void OnDestroy()
        {
            GhostLook.Release(gameObject, GhostKey);
            // A dead character stays hidden: the Respawner has switched its renderers off and brings them back itself.
            if (health != null && !health.IsDead) ShowRenderers();
            hiddenRenderers.Clear();
            if (gameObject.scene.isLoaded) Puff(transform.position);
        }

        sealed class ShadowPuff : MonoBehaviour
        {
            const float Life = 0.5f;
            const int Count = 8;
            Transform[] parts;
            Vector3[] drift;
            Material material;
            float start;

            public void Build()
            {
                start = Time.time;
                material = VisualUtil.UnlitMaterial(new Color(0.06f, 0.16f, 0.08f, 0.7f), 3004);
                parts = new Transform[Count];
                drift = new Vector3[Count];
                for (int i = 0; i < Count; i++)
                {
                    GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(p.GetComponent<Collider>());
                    var r = p.GetComponent<Renderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    p.transform.SetParent(transform, false);
                    float a = i * Mathf.PI * 2f / Count;
                    drift[i] = new Vector3(Mathf.Cos(a), 0.6f + Random.value * 0.6f, Mathf.Sin(a)) * 1.6f;
                    p.transform.localPosition = Vector3.up * 0.9f;
                    parts[i] = p.transform;
                }
            }

            void Update()
            {
                float t = (Time.time - start) / Life;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                Color c = material.color;
                c.a = 0.7f * (1f - t);
                material.color = c;
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i].localPosition = Vector3.up * 0.9f + drift[i] * t;
                    parts[i].localScale = Vector3.one * Mathf.Lerp(0.5f, 1.1f, t);
                }
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }
        }
    }
}
