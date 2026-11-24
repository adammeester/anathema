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
    // any extra network traffic. The look: his eye lenses flare blood red and leave streaks as he moves, and the same
    // flares burn along his blade (whatever weapon he holds, hidden while Blade Hurl has it) and at both wing tips, streaking
    // through every swing and beat; a red glow beats on his chest like a racing heart, and dark red vapour rises off him.
    [DefaultExecutionOrder(210)] // after the poses and GuardWeaponVisual have placed the head, blade and wings
    public class RedThirst : MonoBehaviour, IMoveSpeedModifier, IAttackSpeedModifier
    {
        [SerializeField] float threshold = 0.2f;
        [SerializeField] float moveSpeedBonus = 0.2f;
        [SerializeField] float attackSpeedBonus = 0.3f;
        [SerializeField] float slamDamageBonus = 0.5f;
        [SerializeField] float vitaePerDamage = 2f;   // while thirsting, Vitae per point of damage his hits deal
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

        // The flares: 0-1 the eyes, 2-3 the blade (middle and point), 4-5 the wing tips.
        const int EyeL = 0, EyeR = 1, BladeMid = 2, BladeTip = 3, WingTipL = 4, WingTipR = 5, FlareCount = 6;
        Transform[] eyes;   // every flare (the name kept from when there were only eyes)
        TrailRenderer[] trails;
        readonly bool[] placed = new bool[FlareCount];
        GuardWeaponVisual weaponVisual;
        CrimsonRush rush;
        Transform swordBone, swordTip;       // fallback when there is no GuardWeaponVisual
        Transform wingL, wingR;
        Vector3 wingTipLocalL, wingTipLocalR;   // each wing's tip in its own bone's space, so it follows the beat
        Light heartLight;
        Material eyeMaterial, haloMaterial, vapourMaterial;
        float glow;          // 0..1, eased in and out
        float vapourDebt;
        bool wasActive;

        // Unquenched raises the threshold.
        public float CurrentThreshold => GuardUpgrade.Has(gameObject, GuardUpgrade.Unquenched) ? GuardUpgrade.UnquenchedThreshold : threshold;
        public bool Active => health != null && !health.IsDead && health.Max > 0f && health.Current <= health.Max * CurrentThreshold;

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
            rush = GetComponent<CrimsonRush>();
            if (model != null)
            {
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    // depth-first: the real skeleton, not a later glow clone
                    if (swordBone == null && t.name == "Chainsword") swordBone = t;
                    if (swordTip == null && t.name == "Chainsword_Tip") swordTip = t;
                    if (wingL == null && t.name == "Wing_L") wingL = t;
                    if (wingR == null && t.name == "Wing_R") wingR = t;
                }
                // The wing tips at rest (Art/sanguinary_guard.py: mount to tip is 0.61 out and 0.92 up), kept in the bone's
                // space so they lift and beat with Wings of the Angel.
                wingTipLocalL = WingTipLocal(wingL);
                wingTipLocalR = WingTipLocal(wingR);
            }
            BuildLook();
        }

        void BuildLook()
        {
            eyeMaterial = VisualUtil.UnlitMaterial(EyeColor, 3001);
            haloMaterial = VisualUtil.UnlitMaterial(EyeHaloColor, 3000);
            vapourMaterial = VisualUtil.UnlitMaterial(VapourColor, 2998);

            eyes = new Transform[FlareCount];
            trails = new TrailRenderer[FlareCount];
            MakeFlare(EyeL, "Thirst Eye L", new Vector3(0.11f, 0.07f, 0.07f), 0.26f, 0.07f, 0.22f);
            MakeFlare(EyeR, "Thirst Eye R", new Vector3(0.11f, 0.07f, 0.07f), 0.26f, 0.07f, 0.22f);
            MakeFlare(BladeMid, "Thirst Blade", Vector3.one * 0.1f, 0.3f, 0.12f, 0.25f);
            MakeFlare(BladeTip, "Thirst Blade Point", Vector3.one * 0.13f, 0.38f, 0.14f, 0.3f);
            MakeFlare(WingTipL, "Thirst Wing L", Vector3.one * 0.12f, 0.4f, 0.12f, 0.35f);
            MakeFlare(WingTipR, "Thirst Wing R", Vector3.one * 0.12f, 0.4f, 0.12f, 0.35f);

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

        // A blood-red core in a soft halo that leaves a streak behind it as it moves.
        void MakeFlare(int index, string name, Vector3 core, float halo, float trailWidth, float trailTime)
        {
            var flare = new GameObject(name).transform;
            flare.SetParent(transform, false);
            Primitive(PrimitiveType.Sphere, eyeMaterial, flare).transform.localScale = core;
            Primitive(PrimitiveType.Sphere, haloMaterial, flare).transform.localScale = Vector3.one * halo;

            var trail = flare.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = eyeMaterial;
            trail.time = trailTime;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, trailWidth), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;

            eyes[index] = flare;
            trails[index] = trail;
            flare.gameObject.SetActive(false);
        }

        Vector3 WingTipLocal(Transform wing)
        {
            if (wing == null) return Vector3.zero;
            float side = Vector3.Dot(wing.position - transform.position, transform.right) >= 0f ? 1f : -1f;
            Vector3 tip = wing.position + (transform.right * (side * 0.61f) + Vector3.up * 0.92f) * modelScale;
            return wing.InverseTransformPoint(tip);
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

            for (int i = 0; i < FlareCount; i++)
                if (visible) eyes[i].localScale = Vector3.one * (glow * (0.9f + 0.25f * pulse));
            thirstVisible = visible;
            thirstActive = active;

            heartLight.enabled = visible;
            if (visible) heartLight.intensity = glow * (1.2f + 2.2f * pulse);
            haloMaterial.color = new Color(EyeHaloColor.r, EyeHaloColor.g, EyeHaloColor.b, EyeHaloColor.a * (0.6f + 0.4f * pulse));

            if (active) ShedVapour();
        }

        bool thirstVisible, thirstActive;

        // Every flare is placed after the poses, the flight and the weapon have moved this frame. A flare with nowhere to
        // be (the blade while it is thrown) hides and drops its streak.
        void LateUpdate()
        {
            if (eyes == null) return;
            Vector3 root = head != null ? head.position : transform.position + Vector3.up * HeadBoneHeight * modelScale;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Place(i, root + (transform.right * (side * EyeOffset.x) + Vector3.up * EyeOffset.y
                                 + transform.forward * EyeOffset.z) * modelScale, true);
            }

            if (weaponVisual == null) weaponVisual = GetComponent<GuardWeaponVisual>();
            Transform from = weaponVisual != null ? weaponVisual.BladeFrom : swordBone;
            Transform to = weaponVisual != null ? weaponVisual.BladeTo : swordTip;
            bool inHand = weaponVisual != null ? weaponVisual.InHand : rush == null || !rush.Disarmed;
            bool blade = inHand && from != null && to != null;
            Place(BladeMid, blade ? Vector3.Lerp(from.position, to.position, 0.55f) : Vector3.zero, blade);
            Place(BladeTip, blade ? to.position : Vector3.zero, blade);

            Place(WingTipL, wingL != null ? wingL.TransformPoint(wingTipLocalL) : Vector3.zero, wingL != null);
            Place(WingTipR, wingR != null ? wingR.TransformPoint(wingTipLocalR) : Vector3.zero, wingR != null);
        }

        void Place(int i, Vector3 at, bool there)
        {
            bool show = thirstVisible && there;
            if (show) eyes[i].position = at;   // first, so a fresh streak starts here
            if (eyes[i].gameObject.activeSelf != show)
            {
                eyes[i].gameObject.SetActive(show);
                trails[i].Clear();
            }
            if (!show) return;
            trails[i].emitting = thirstActive;
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
