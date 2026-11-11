using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // Purgatus visuals, all built in code (no assets): an expanding ring of psychic fire.
    //   Spawn()         the released ring: a glowing flame band on the ground racing outwards, flames and embers
    //                   licking up from its leading edge, a flash at the centre and a violet light that dies away.
    //   CreateChannel() the build-up: embers drawn in from the edge towards the caster, thickening as the charge fills.
    // The radius follows Ease(t), the same curve Purgatus uses for its damage, so the fire and the hit-test agree.
    // Colours default to the Grey Knight's violet warp-fire; other casters pass a Palette (the Hospitaller's holy fire).
    //   UpdateWreath()  extra: flames licking up around a caster's body, for a fire that clings to someone.
    public class PsychicFireRing : MonoBehaviour
    {
        // deep: the cool outer colour flames fade to; mid: the body of the fire; hot: the white-hot core and embers.
        public struct Palette
        {
            public Color Deep, Mid, Hot, Light;
            public bool Custom;

            public static Palette Make(Color deep, Color mid, Color hot, Color light) =>
                new Palette { Deep = deep, Mid = mid, Hot = hot, Light = light, Custom = true };
        }

        const int Segments = 96;
        const float RingHeight = 0.08f;

        static readonly Palette Warp = new Palette
        {
            Deep = new Color(0.55f, 0.25f, 1f), Mid = new Color(0.85f, 0.6f, 1f), Hot = new Color(1f, 0.95f, 1f),
            Light = new Color(0.7f, 0.4f, 1f)
        };

        Palette palette = Warp;
        Color Violet => palette.Deep;
        Color Lavender => palette.Mid;
        Color HotWhite => palette.Hot;

        static Texture2D softTexture;

        ParticleSystem ps;
        Material particleMaterial;
        Material ringMaterial;
        Transform ringTransform;
        GameObject ringObject;
        Light glow;

        Vector3 center;
        float maxRadius;
        float duration;
        float startTime;
        float emitCarry;
        bool expanding;

        // Fast out, slowing at the edge. 0..1 in, 0..1 out.
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        public static PsychicFireRing Spawn(Vector3 center, float maxRadius, float duration) => Spawn(center, maxRadius, duration, Warp);

        public static PsychicFireRing Spawn(Vector3 center, float maxRadius, float duration, Palette colours)
        {
            PsychicFireRing ring = Create("Psychic Fire Ring", colours);
            ring.center = center;
            ring.maxRadius = maxRadius;
            ring.duration = duration;
            ring.startTime = Time.time;
            ring.expanding = true;
            ring.BuildRing();
            ring.BuildGlow();

            // Flash at the centre as the nova leaves the caster.
            var flash = new ParticleSystem.EmitParams
            {
                position = center + Vector3.up * 0.4f,
                velocity = Vector3.zero,
                startLifetime = 0.35f,
                startSize = maxRadius * 1.3f,
                startColor = new Color(ring.Lavender.r, ring.Lavender.g, ring.Lavender.b, 0.55f)
            };
            ring.ps.Emit(flash, 1);
            return ring;
        }

        public static PsychicFireRing CreateChannel() => CreateChannel(Warp);

        public static PsychicFireRing CreateChannel(Palette colours)
        {
            return Create("Psychic Fire Channel", colours);
        }

        // Called every frame while someone is wreathed in this fire: flames climb around their body up to `height`.
        public void UpdateWreath(Vector3 feet, float bodyRadius, float height, float intensity)
        {
            emitCarry += 70f * intensity * Time.deltaTime;
            int n = (int)emitCarry;
            emitCarry -= n;

            for (int i = 0; i < n; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bool ember = Random.value < 0.25f;
                var p = new ParticleSystem.EmitParams
                {
                    position = feet + dir * bodyRadius * Random.Range(0.6f, 1.1f) + Vector3.up * Random.Range(0f, height * 0.6f),
                    velocity = dir * Random.Range(0f, 0.4f) + Vector3.up * Random.Range(ember ? 2.5f : 1.2f, ember ? 4.5f : 2.6f),
                    startLifetime = ember ? Random.Range(0.6f, 1f) : Random.Range(0.3f, 0.55f),
                    startSize = ember ? Random.Range(0.06f, 0.14f) : Random.Range(0.3f, 0.6f) * Mathf.Lerp(0.7f, 1.2f, intensity),
                    startColor = ember ? Color.Lerp(Lavender, HotWhite, Random.value) : FlameColour()
                };
                ps.Emit(p, 1);
            }
        }

        // The colour of one flame licking up from the fire.
        Color FlameColour()
        {
            if (!palette.Custom)
                return new Color(Mathf.Lerp(0.75f, 1f, Random.value), Mathf.Lerp(0.45f, 0.9f, Random.value), 1f, 0.6f);
            Color c = Color.Lerp(Violet, Lavender, Random.value);
            c.a = 0.6f;
            return c;
        }

        // Called every frame of the channel: embers stream in from the edge of the nova towards the caster.
        public void UpdateChannel(Vector3 casterPosition, float radius, float progress)
        {
            emitCarry += (15f + 140f * progress) * Time.deltaTime;
            int n = (int)emitCarry;
            emitCarry -= n;

            for (int i = 0; i < n; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangent = new Vector3(-dir.z, 0f, dir.x);
                float life = Random.Range(0.5f, 0.8f);
                float from = radius * Random.Range(0.55f, 1f);

                var p = new ParticleSystem.EmitParams
                {
                    position = casterPosition + dir * from + Vector3.up * 0.1f,
                    // Drift inwards over the particle's life, swirling a little, and rising as it goes.
                    velocity = -dir * (from * 0.8f / life) + tangent * Random.Range(0.5f, 1.5f) + Vector3.up * Random.Range(0.6f, 1.6f),
                    startLifetime = life,
                    startSize = Random.Range(0.12f, 0.3f),
                    startColor = Color.Lerp(Violet, HotWhite, Random.value * progress)
                };
                ps.Emit(p, 1);
            }
        }

        // The channel is over (released or cancelled): stop emitting and let the last embers burn out.
        public void EndChannel()
        {
            Destroy(gameObject, 1.5f);
        }

        static PsychicFireRing Create(string name, Palette colours)
        {
            var go = new GameObject(name);
            var ring = go.AddComponent<PsychicFireRing>();
            ring.palette = colours;
            ring.BuildParticles();
            return ring;
        }

        // ---------- Particles (flames, embers, flash) ----------

        void BuildParticles()
        {
            particleMaterial = CreateMaterial();
            particleMaterial.mainTexture = SoftTexture();

            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.maxParticles = 4000;
            main.gravityModifier = 0f;

            // Everything is emitted from code.
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            // White-hot core cooling to violet, fading in quickly and out slowly.
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Lavender, 0.35f),
                    new GradientColorKey(Violet, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0.7f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
        }

        void EmitFlames(float radius)
        {
            emitCarry += Mathf.PI * 2f * radius * 28f * Time.deltaTime;
            int n = (int)emitCarry;
            emitCarry -= n;

            // Flames trail behind the front rather than sitting exactly on it.
            float trail = Mathf.Min(1.2f, radius * 0.5f);

            for (int i = 0; i < n; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 position = center + dir * Mathf.Max(0f, radius - Random.value * trail) + Vector3.up * 0.1f;

                bool ember = Random.value < 0.2f;
                var p = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = ember
                        ? dir * Random.Range(1f, 4f) + Vector3.up * Random.Range(2.5f, 5f)
                        : dir * Random.Range(0f, 1.5f) + Vector3.up * Random.Range(1.5f, 3.5f),
                    startLifetime = ember ? Random.Range(0.6f, 1f) : Random.Range(0.35f, 0.65f),
                    startSize = ember ? Random.Range(0.1f, 0.22f) : Random.Range(0.6f, 1.2f),
                    startColor = ember ? Color.Lerp(Lavender, HotWhite, Random.value) : FlameColour()
                };
                ps.Emit(p, 1);
            }
        }

        // ---------- Ground band ----------

        // A flat annulus of unit outer radius, scaled up as the ring expands. Vertex colours give it a soft violet
        // trail behind a bright, sharp leading edge.
        void BuildRing()
        {
            ringMaterial = CreateMaterial();

            ringObject = new GameObject("Band");
            ringTransform = ringObject.transform;
            ringTransform.position = center + Vector3.up * RingHeight;
            ringObject.AddComponent<MeshFilter>().sharedMesh = BuildBandMesh();
            var renderer = ringObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ringMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringTransform.localScale = new Vector3(0.01f, 1f, 0.01f);
        }

        Mesh BuildBandMesh()
        {
            // Radius fraction and colour of each ring of vertices, inside to outside.
            float[] radii = { 0.55f, 0.85f, 0.97f, 1f };
            Color[] colors =
            {
                new Color(Violet.r, Violet.g, Violet.b, 0f),
                new Color(Violet.r, Violet.g, Violet.b, 0.5f),
                new Color(HotWhite.r, HotWhite.g, HotWhite.b, 1f),
                new Color(Lavender.r, Lavender.g, Lavender.b, 0f)
            };

            int rings = radii.Length;
            var vertices = new Vector3[rings * (Segments + 1)];
            var vertexColors = new Color[vertices.Length];
            var triangles = new int[(rings - 1) * Segments * 6];

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s <= Segments; s++)
                {
                    float angle = s / (float)Segments * Mathf.PI * 2f;
                    int index = r * (Segments + 1) + s;
                    vertices[index] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radii[r];
                    vertexColors[index] = colors[r];
                }
            }

            int t = 0;
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int a = r * (Segments + 1) + s;
                    int b = a + 1;
                    int c = a + Segments + 1;
                    int d = c + 1;
                    // Both windings, so the band is visible from above whichever way the shader culls.
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }

            var mesh = new Mesh { vertices = vertices, colors = vertexColors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildGlow()
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            go.transform.position = center + Vector3.up * 1.5f;
            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = palette.Light;
            glow.range = maxRadius * 1.6f;
            glow.intensity = 4f;
            glow.shadows = LightShadows.None;
        }

        void Update()
        {
            if (!expanding) return;

            float t = (Time.time - startTime) / duration;
            float radius = maxRadius * Ease(t);

            if (t < 1f)
            {
                EmitFlames(radius);
                ringTransform.localScale = new Vector3(Mathf.Max(radius, 0.01f), 1f, Mathf.Max(radius, 0.01f));
            }

            // The band stays bright until the very end of the run, then fades fast.
            float fade = Mathf.Clamp01(1f - t * t * t);
            SetFade(ringMaterial, fade);
            glow.intensity = 4f * fade;

            if (t >= 1f)
            {
                expanding = false;
                Destroy(ringObject);
                Destroy(glow.gameObject);
                Destroy(gameObject, 1.5f); // let the last flames burn out
            }
        }

        // ---------- Materials ----------

        // Additive, so the fire glows over the floor and overlapping flames brighten. Falls back to a plain
        // alpha-blended sprite shader where the legacy particle shader isn't available.
        static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);
            SetFade(material, 1f);
            return material;
        }

        static void SetFade(Material material, float alpha)
        {
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f * alpha));
            else material.color = new Color(1f, 1f, 1f, alpha);
        }

        // A soft round blob: the particles' sprite.
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
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                    softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            softTexture.Apply();
            return softTexture;
        }

        void OnDestroy()
        {
            if (particleMaterial != null) Destroy(particleMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}
