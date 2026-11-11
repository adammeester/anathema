using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // Press E to inscribe a warding sigil at the cursor (clamped to range). A ground telegraph shows the full radius
    // while an inner disc fills over the delay, then the sigil detonates: every enemy inside takes a little damage and
    // is rooted in place. Pressing C (or dying) before the detonation cancels it and refunds the cooldown.
    public class Consecrate : Ability
    {
        [SerializeField] float damage = 10f;
        [SerializeField] float range = 10f;
        [SerializeField] float radius = 3f;
        [SerializeField] float delay = 0.6f;
        [SerializeField] float rootDuration = 0.75f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] float fireDuration = 2f;   // how long the flames burn on the ground (visual only; the root is rootDuration)
        [SerializeField] Color telegraphColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] Color fillColor = new Color(1f, 1f, 1f, 0.3f);

        [Header("EX: Sanctum")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float domeRadius = 3.5f;
        [SerializeField] float domeDuration = 6f;
        [SerializeField] float domeSlow = 0.5f;
        [SerializeField] Color domeColor = new Color(0.4f, 0.7f, 1f);

        const float GroundOffset = 0.05f;

        Energy energy;
        Transform telegraphDisc;
        Transform fillDisc;
        bool pending;
        AudioSource armSound;
        float detonateTime;
        Vector3 center;
        readonly HashSet<Health> struck = new HashSet<Health>();

        protected override bool CooldownStartsOnStrike => true;

        public override string DisplayName => "Consecrate";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExDescription => "Sanctum";
        public override string ExName => "Sanctum";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 8f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();

            telegraphDisc = CreateDisc("Consecrate Telegraph", telegraphColor);
            fillDisc = CreateDisc("Consecrate Fill", fillColor);
        }

        void OnDestroy()
        {
            foreach (Transform t in new[] { telegraphDisc, fillDisc })
                if (t != null) Destroy(t.gameObject);
        }

        static Transform CreateDisc(string name, Color color)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Destroy(disc.GetComponent<Collider>());
            var renderer = disc.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = VisualUtil.UnlitMaterial(color);
            disc.SetActive(false);
            return disc.transform;
        }

        static void SetDisc(Transform disc, Vector3 position, float discRadius, float height)
        {
            disc.position = new Vector3(position.x, position.y + height, position.z);
            disc.localScale = new Vector3(discRadius * 2f, 0.01f, discRadius * 2f);
        }

        protected override void Update()
        {
            base.Update();
            if (!pending) return;

            float t = Mathf.Clamp01(1f - (detonateTime - Time.time) / delay);
            SetDisc(fillDisc, center, Mathf.Max(0.01f, radius * t), GroundOffset + 0.01f);
            if (Time.time >= detonateTime) Detonate();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.E);
        }

        protected override void Activate()
        {
            // The sigil lands where the cursor was when E was pressed, pulled in to max range if it's further.
            Vector3 origin = transform.position;
            Vector3 to = Aim.AimPoint - origin;
            to.y = 0f;
            if (to.magnitude > range) to = to.normalized * range;
            center = new Vector3(origin.x + to.x, origin.y, origin.z + to.z);

            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
            if (ExActivation)
            {
                // Sanctum: no detonation, the dome goes up at once and the cooldown starts now.
                ReflectDome.Spawn(gameObject, center, domeRadius, domeDuration, domeSlow, domeColor);
                GreyKnightSound.ConsecrateBoom();
                if (!IsReplica) StartCooldown();
                return;
            }

            pending = true;
            detonateTime = Time.time + delay;
            armSound = GreyKnightSound.ConsecrateArm();

            SetDisc(telegraphDisc, center, radius, GroundOffset);
            SetDisc(fillDisc, center, 0.01f, GroundOffset + 0.01f);
            telegraphDisc.gameObject.SetActive(true);
            fillDisc.gameObject.SetActive(true);
        }

        protected override void Cancel()
        {
            if (!pending) return;

            GreyKnightSound.Stop(armSound);
            Hide();
            RefundCooldown();
        }

        void Hide()
        {
            pending = false;
            telegraphDisc.gameObject.SetActive(false);
            fillDisc.gameObject.SetActive(false);
        }

        void Detonate()
        {
            Hide();
            CleansingFire.Spawn(center, radius, Mathf.Max(rootDuration, fireDuration), fillColor);
            GreyKnightSound.Stop(armSound);
            GreyKnightSound.ConsecrateBoom();

            // On another machine this only replays the visuals; the owner applies the damage and the root.
            if (IsReplica) return;

            struck.Clear();
            foreach (Collider col in Physics.OverlapSphere(center + Vector3.up, radius + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || struck.Contains(target)) continue;

                Vector3 to = col.ClosestPoint(center) - center;
                to.y = 0f;
                if (to.magnitude > radius) continue;

                struck.Add(target);
                Hit blast = GreyKnightUpgrade.Ignited(gameObject, Hit.Direct(damage).AsAoe().WithRoot(rootDuration));
                target.Strike(blast, gameObject, landed =>
                {
                    if (landed && energy != null) energy.Gain(energyPerHit);
                });
            }

            StartCooldown();
        }

        // The hallowed ground left behind by the detonation: a blue psychic disc ringed in orange, with cleansing fire
        // licking up from it. It lasts `fireDuration`, fading over its final moments, then destroys itself.
        class CleansingFire : MonoBehaviour
        {
            const float FadeTime = 0.25f;

            static readonly Color Blue = new Color(0.25f, 0.55f, 1f);
            static readonly Color Orange = new Color(1f, 0.55f, 0.1f);
            static readonly Color Gold = new Color(1f, 0.85f, 0.4f);
            static Texture2D softTexture;

            ParticleSystem ps;
            Material discMaterial;
            Material particleMaterial;
            Color templateColor;
            Vector3 center;
            float radius;
            float endTime;
            float emitCarry;

            public static void Spawn(Vector3 position, float radius, float duration, Color templateColor)
            {
                var go = new GameObject("Consecrate Fire");
                var fire = go.AddComponent<CleansingFire>();
                fire.center = position;
                fire.radius = radius;
                fire.endTime = Time.time + duration;
                fire.templateColor = templateColor;
                fire.Build();
            }

            void Build()
            {
                // The template stays on the ground under the fire.
                discMaterial = VisualUtil.UnlitMaterial(templateColor);
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Hallowed Ground";
                Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(transform, false);
                disc.transform.position = new Vector3(center.x, center.y + GroundOffset + 0.02f, center.z);
                disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
                var discRenderer = disc.GetComponent<Renderer>();
                discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                discRenderer.sharedMaterial = discMaterial;

                // Flames: orange cores cooling to blue, emitted from code.
                Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                particleMaterial = new Material(shader) { mainTexture = SoftTexture() };
                if (particleMaterial.HasProperty("_TintColor")) particleMaterial.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));

                ps = gameObject.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSpeed = 0f;
                main.startLifetime = 0.8f;
                main.maxParticles = 1500;
                main.gravityModifier = 0f;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.enabled = false;
                ParticleSystem.ShapeModule shape = ps.shape;
                shape.enabled = false;

                ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
                colour.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Gold, 0f),
                        new GradientColorKey(Orange, 0.3f),
                        new GradientColorKey(Blue, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.15f),
                        new GradientAlphaKey(0.6f, 0.55f),
                        new GradientAlphaKey(0f, 1f)
                    });
                colour.color = new ParticleSystem.MinMaxGradient(gradient);

                ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f,
                    new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));

                var renderer = GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = particleMaterial;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ps.Play();
            }

            void Update()
            {
                float remaining = endTime - Time.time;
                if (remaining <= -0.8f)
                {
                    Destroy(gameObject);
                    return;
                }

                float fade = Mathf.Clamp01(remaining / FadeTime);
                SetAlpha(discMaterial, templateColor.a * fade);

                // Stop feeding the fire shortly before the root ends so the flames die out with it.
                if (remaining <= 0.2f) return;

                emitCarry += radius * radius * 9f * Time.deltaTime;
                int n = (int)emitCarry;
                emitCarry -= n;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = Random.insideUnitCircle * radius;
                    bool spark = Random.value < 0.15f;
                    var e = new ParticleSystem.EmitParams
                    {
                        position = center + new Vector3(p.x, 0.1f, p.y),
                        velocity = spark
                            ? new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(2.5f, 4.5f), Random.Range(-0.6f, 0.6f))
                            : new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2.8f), Random.Range(-0.3f, 0.3f)),
                        startLifetime = spark ? Random.Range(0.6f, 1f) : Random.Range(0.4f, 0.8f),
                        startSize = spark ? Random.Range(0.1f, 0.2f) : Random.Range(0.5f, 1f),
                        startColor = Color.white
                    };
                    ps.Emit(e, 1);
                }
            }

            static void SetAlpha(Material material, float alpha)
            {
                Color c = material.color;
                c.a = alpha;
                material.color = c;
            }

            static Texture2D SoftTexture()
            {
                if (softTexture != null) return softTexture;

                const int size = 64;
                softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                        softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
                    }
                }
                softTexture.Apply();
                return softTexture;
            }

            void OnDestroy()
            {
                if (discMaterial != null) Destroy(discMaterial);
                if (particleMaterial != null) Destroy(particleMaterial);
            }
        }
    }
}
