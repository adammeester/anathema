using Anathema.Combat;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Second passive: Red Thirst, the curse in the Blood Angels' blood. At or below `threshold` of his max health the
    // thirst takes him: he moves `moveSpeedBonus` faster, swings `attackSpeedBonus` faster (Power Sword, the Arc and the
    // ascended Arcs; through Enraged.AttackSpeedMultiplier) and Jump Pack Descent's slam hits `slamDamageBonus` harder.
    // Lifesteal is unchanged. While it lasts, damage from Power Sword cuts and Crimson Rush fills Vitae, `vitaePerDamage`
    // per point (the only time damage does; nothing else in his kit, not the Arc), so he can fight his way back to an Arc. It lifts as soon as he climbs
    // back above the threshold.
    //
    // Derived from his health alone, which every machine has, so the owner, the server and replicas all agree without
    // any extra network traffic. The look: his eye lenses flare blood red and leave streaks as he moves, a red glow
    // beats on his chest like a racing heart, and dark red vapour rises off him.
    public class RedThirst : MonoBehaviour, IMoveSpeedModifier, IAttackSpeedModifier
    {
        [SerializeField] float threshold = 0.2f;
        [SerializeField] float moveSpeedBonus = 0.2f;
        [SerializeField] float attackSpeedBonus = 0.3f;
        [SerializeField] float slamDamageBonus = 0.5f;
        [SerializeField] float vitaePerDamage = 1f;   // while thirsting, Vitae per point of damage his hits deal
        [SerializeField] float vapourPerSecond = 10f;

        static readonly Color EyeColor = new Color(1f, 0.08f, 0.04f, 1f);
        static readonly Color EyeHaloColor = new Color(1f, 0.1f, 0.05f, 0.45f);
        static readonly Color VapourColor = new Color(0.55f, 0.02f, 0.04f, 0.7f);

        // Eye lenses relative to the head bone's root, in model units (see Art/sanguinary_guard.py: the head bone starts at
        // 1.8, the lenses sit at 2.09 high, 0.265 forward and 0.09 to each side).
        const float HeadBoneHeight = 1.8f;
        static readonly Vector3 EyeOffset = new Vector3(0.09f, 0.29f, 0.28f);

        Health health;
        Vitae vitae;
        Transform head;
        float modelScale = 1f;

        Transform[] eyes;
        TrailRenderer[] trails;
        Light heartLight;
        Material eyeMaterial, haloMaterial, vapourMaterial;
        float glow;          // 0..1, eased in and out
        float vapourDebt;
        bool wasActive;

        public bool Active => health != null && !health.IsDead && health.Max > 0f && health.Current <= health.Max * threshold;

        public float MoveSpeedMultiplier => Active ? 1f + moveSpeedBonus : 1f;
        public float AttackSpeedMultiplier => Active ? 1f + attackSpeedBonus : 1f;
        public float SlamDamageMultiplier => Active ? 1f + slamDamageBonus : 1f;

        public float Threshold => threshold;
        public float MoveSpeedBonus => moveSpeedBonus;
        public float AttackSpeedBonus => attackSpeedBonus;
        public float SlamDamageBonus => slamDamageBonus;

        // Owner, from SanguinaryKit.FeedThirst: the one time damage fills the chalice, so a guard bled dry can fight his
        // way to an Arc (and its lifesteal) instead of waiting on heals. Not the Arc's own hits.
        public void OnDamageDealt(float damage)
        {
            if (!Active || vitae == null || health.IsReplica || damage <= 0f) return;
            vitae.Add(damage * vitaePerDamage);
        }

        public float VitaePerDamage => vitaePerDamage;

        void Awake()
        {
            health = GetComponent<Health>();
            vitae = GetComponent<Vitae>();
            Transform model = transform.Find("Model");
            if (model != null)
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Head") { head = t; break; }
            if (head != null)
            {
                float height = head.position.y - transform.position.y;
                if (height > 0.1f) modelScale = height / HeadBoneHeight;
            }
            BuildLook();
        }

        void BuildLook()
        {
            eyeMaterial = VisualUtil.UnlitMaterial(EyeColor, 3001);
            haloMaterial = VisualUtil.UnlitMaterial(EyeHaloColor, 3000);
            vapourMaterial = VisualUtil.UnlitMaterial(VapourColor, 2998);

            eyes = new Transform[2];
            trails = new TrailRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var eye = new GameObject(i == 0 ? "Thirst Eye L" : "Thirst Eye R").transform;
                eye.SetParent(transform, false);

                GameObject core = Primitive(PrimitiveType.Sphere, eyeMaterial, eye);
                core.transform.localScale = new Vector3(0.11f, 0.07f, 0.07f);
                GameObject halo = Primitive(PrimitiveType.Sphere, haloMaterial, eye);
                halo.transform.localScale = Vector3.one * 0.26f;

                var trail = eye.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = eyeMaterial;
                trail.time = 0.22f;
                trail.minVertexDistance = 0.05f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.emitting = false;

                eyes[i] = eye;
                trails[i] = trail;
                eye.gameObject.SetActive(false);
            }

            var lightGo = new GameObject("Thirst Heart Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.5f, 0.4f);
            heartLight = lightGo.AddComponent<Light>();
            heartLight.type = LightType.Point;
            heartLight.color = new Color(1f, 0.08f, 0.05f);
            heartLight.range = 4.5f;
            heartLight.intensity = 0f;
            heartLight.enabled = false;
        }

        static GameObject Primitive(PrimitiveType type, Material material, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(parent, false);
            return go;
        }

        void Update()
        {
            bool active = Active;
            if (active && !wasActive)
            {
                // The thirst takes him: a gasp of blood mist and a low, wet heartbeat.
                SanguinaryKit.Mist(transform.position, 1.6f, 0.5f);
                Sfx.Play(Sfx.HammerSmash, 0.25f, 0.45f, 0.5f, 0.4f);
            }
            wasActive = active;

            glow = Mathf.MoveTowards(glow, active ? 1f : 0f, Time.deltaTime * (active ? 4f : 2f));
            bool visible = glow > 0.01f;

            // A racing heartbeat: two quick pulses, then a pause.
            float beat = Mathf.Repeat(Time.time * 1.6f, 1f);
            float pulse = Mathf.Exp(-Mathf.Pow((beat - 0.1f) * 18f, 2f)) + 0.7f * Mathf.Exp(-Mathf.Pow((beat - 0.32f) * 18f, 2f));

            for (int i = 0; i < 2; i++)
            {
                if (eyes[i].gameObject.activeSelf != visible)
                {
                    eyes[i].gameObject.SetActive(visible);
                    trails[i].Clear();
                }
                if (visible) eyes[i].localScale = Vector3.one * (glow * (0.9f + 0.25f * pulse));
                trails[i].emitting = active;
            }

            heartLight.enabled = visible;
            if (visible) heartLight.intensity = glow * (1.2f + 2.2f * pulse);
            haloMaterial.color = new Color(EyeHaloColor.r, EyeHaloColor.g, EyeHaloColor.b, EyeHaloColor.a * (0.6f + 0.4f * pulse));

            if (active) ShedVapour();
        }

        void LateUpdate()
        {
            if (eyes == null || !eyes[0].gameObject.activeSelf) return;
            // Placed on the lenses every frame, after the swing and flight poses have moved the head.
            Vector3 root = head != null ? head.position : transform.position + Vector3.up * HeadBoneHeight * modelScale;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                eyes[i].position = root + (transform.right * (side * EyeOffset.x) + Vector3.up * EyeOffset.y
                                           + transform.forward * EyeOffset.z) * modelScale;
            }
        }

        void ShedVapour()
        {
            vapourDebt += vapourPerSecond * Time.deltaTime;
            while (vapourDebt >= 1f)
            {
                vapourDebt -= 1f;
                Vector2 ring = Random.insideUnitCircle * 0.6f;
                Vector3 at = transform.position + new Vector3(ring.x, Random.Range(0.3f, 1.8f) * modelScale, ring.y);
                ThirstVapour.Spawn(at, vapourMaterial);
            }
        }

        void OnDestroy()
        {
            if (eyeMaterial != null) Destroy(eyeMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
            if (vapourMaterial != null) Destroy(vapourMaterial);
        }

        // A dark red wisp that rises, swells and fades.
        sealed class ThirstVapour : MonoBehaviour
        {
            float born, life, size;
            Vector3 velocity;

            public static void Spawn(Vector3 at, Material material)
            {
                GameObject go = Primitive(PrimitiveType.Sphere, material, null);
                go.name = "Thirst Vapour";
                go.transform.position = at;
                var v = go.AddComponent<ThirstVapour>();
                v.born = Time.time;
                v.life = Random.Range(0.5f, 0.9f);
                v.size = Random.Range(0.08f, 0.16f);
                v.velocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.8f, 1.5f), Random.Range(-0.2f, 0.2f));
                go.transform.localScale = Vector3.one * v.size;
            }

            void Update()
            {
                float t = (Time.time - born) / life;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.position += velocity * Time.deltaTime;
                transform.localScale = Vector3.one * (size * (1f + t) * (1f - t * t));
            }
        }
    }
}
