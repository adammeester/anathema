using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // The blade charging with Vitae, in stages (Vitae.GlowLevel):
    //   1 - a faint red sheen along the blade;
    //   2 - a glowing sleeve and a red light, blood dripping from the edge;
    //   3 - full: a strong pulsing glow, red arcs crackling off the blade, a blood mist trailing him and a red halo
    //       glyph above his head. This is the warning enemies read before the Exsanguinating Arc.
    // Built in world space and re-aimed every LateUpdate from the weapon bone, so it follows the swing and the spin.
    // Replicas only know "full or not", so they show stage 3 or nothing.
    [DefaultExecutionOrder(200)] // after Chainsword (50) has posed the bones this frame
    public class BloodBladeEffect : MonoBehaviour
    {
        const int ArcCount = 5;
        const int ArcPoints = 6;

        // Bone names tried in order: his own sword first, then the placeholder model's halberd, then the hand.
        static readonly string[] WeaponBones = { "Chainsword", "Sword", "Blade", "Halberd", "Hand_R", "Forearm_R" };

        // His model has a Chainsword_Tip bone at the point: the glow runs from this share of the way along (above the
        // chain-wheel) to the tip. Without one (another model), it runs bladeStart..bladeEnd metres up the bone's axis.
        const float TipStart = 0.18f;
        [SerializeField] float bladeStart = 0.3f;
        [SerializeField] float bladeEnd = 1.45f;
        Transform tip;
        [SerializeField] float arcReach = 0.45f;

        Vitae vitae;
        Transform weapon;
        Transform owner;
        Vector3 localAxis = Vector3.up;
        Transform sleeve;
        Material sleeveMaterial;
        Light glowLight;
        LineRenderer[] arcs;
        float[] arcAlong;
        Vector3[,] arcJitter;
        Transform halo;
        Material haloMaterial;
        Material arcMaterial;
        float nextFlicker;
        float nextDrip;
        float nextMist;
        int level = -1;

        public static BloodBladeEffect Create(Transform model, Vitae vitae)
        {
            if (model == null || vitae == null) return null;
            Transform bone = null;
            foreach (string name in WeaponBones)
            {
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != name) continue;
                    bone = t; // depth-first: the real skeleton, not a later glow clone
                    break;
                }
                if (bone != null) break;
            }
            if (bone == null) bone = model;

            var go = new GameObject("Blood Blade Effect");
            go.transform.SetParent(model.parent, false);
            var effect = go.AddComponent<BloodBladeEffect>();
            effect.vitae = vitae;
            effect.weapon = bone;
            effect.owner = model.parent;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == bone.name + "_Tip") { effect.tip = t; break; }
            // At rest the model stands upright, so world up in the bone's frame is along the blade.
            effect.localAxis = bone.InverseTransformDirection(Vector3.up).normalized;
            effect.Build();
            return effect;
        }

        void Build()
        {
            sleeveMaterial = VisualUtil.UnlitMaterial(SanguinaryKit.Blood, 3000);
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "Blade Glow";
            Destroy(shell.GetComponent<Collider>());
            shell.GetComponent<Renderer>().sharedMaterial = sleeveMaterial;
            shell.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sleeve = shell.transform;
            sleeve.SetParent(transform, false);

            var lightGo = new GameObject("Blade Light");
            lightGo.transform.SetParent(transform, false);
            glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.15f, 0.1f);
            glowLight.range = 4f;
            glowLight.shadows = LightShadows.None;

            arcMaterial = VisualUtil.UnlitMaterial(SanguinaryKit.BrightBlood, 3100);
            arcs = new LineRenderer[ArcCount];
            arcAlong = new float[ArcCount];
            arcJitter = new Vector3[ArcCount, ArcPoints];
            for (int i = 0; i < ArcCount; i++)
            {
                var arcGo = new GameObject("Blood Arc " + i);
                arcGo.transform.SetParent(transform, false);
                LineRenderer lr = arcGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = ArcPoints;
                lr.widthMultiplier = 0.05f;
                lr.numCapVertices = 2;
                lr.sharedMaterial = arcMaterial;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arcs[i] = lr;
            }

            // The halo glyph: a flat red ring with a cross through it, floating over his head.
            haloMaterial = VisualUtil.UnlitMaterial(SanguinaryKit.BrightBlood, 3100);
            var haloGo = new GameObject("Blood Halo");
            haloGo.transform.SetParent(transform, false);
            halo = haloGo.transform;
            var ring = haloGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 24;
            ring.widthMultiplier = 0.07f;
            ring.sharedMaterial = haloMaterial;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.45f);
            }
            for (int k = 0; k < 2; k++)
            {
                var barGo = new GameObject("Halo Bar");
                barGo.transform.SetParent(halo, false);
                var bar = barGo.AddComponent<LineRenderer>();
                bar.useWorldSpace = false;
                bar.positionCount = 2;
                bar.widthMultiplier = 0.06f;
                bar.sharedMaterial = haloMaterial;
                bar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 d = k == 0 ? Vector3.right : Vector3.forward;
                bar.SetPosition(0, -d * 0.6f);
                bar.SetPosition(1, d * 0.6f);
            }

            Apply(0);
        }

        void Apply(int newLevel)
        {
            if (newLevel == level) return;
            level = newLevel;
            sleeve.gameObject.SetActive(level >= 1);
            glowLight.enabled = level >= 2;
            foreach (LineRenderer arc in arcs) arc.enabled = level >= 3;
            halo.gameObject.SetActive(level >= 3);
            nextFlicker = 0f;
        }

        void LateUpdate()
        {
            if (vitae == null || weapon == null)
            {
                Destroy(gameObject);
                return;
            }

            Health health = vitae.GetComponent<Health>();
            Apply(health != null && health.IsDead ? 0 : vitae.GlowLevel);
            if (level <= 0) return;

            float scale = Mathf.Max(0.01f, weapon.lossyScale.y);
            Vector3 axis, start, end;
            if (tip != null && (tip.position - weapon.position).sqrMagnitude > 1e-4f)
            {
                // His own sword: grip -> point, however it is held (it trails behind him at rest).
                axis = (tip.position - weapon.position).normalized;
                start = Vector3.Lerp(weapon.position, tip.position, TipStart);
                end = tip.position;
                scale = 1f;
            }
            else
            {
                axis = weapon.TransformDirection(localAxis).normalized;
                start = weapon.position + axis * (bladeStart * scale);
                end = weapon.position + axis * (bladeEnd * scale);
            }
            float speed = level >= 3 ? 16f : 6f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * speed);

            float thickness = level >= 3 ? 0.3f : level == 2 ? 0.22f : 0.16f;
            float alpha = level >= 3 ? 0.55f : level == 2 ? 0.35f : 0.18f;
            sleeve.position = (start + end) * 0.5f;
            sleeve.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            sleeve.localScale = new Vector3(thickness * scale, (end - start).magnitude * 0.5f, thickness * scale);
            Color c = SanguinaryKit.Blood;
            c.a = alpha * pulse;
            sleeveMaterial.color = c;

            if (level >= 2)
            {
                glowLight.transform.position = Vector3.Lerp(start, end, 0.7f);
                glowLight.intensity = (level >= 3 ? 3.5f : 1.6f) * pulse;
                if (Time.time >= nextDrip)
                {
                    nextDrip = Time.time + (level >= 3 ? 0.12f : 0.3f);
                    BloodDrip.Spawn(Vector3.Lerp(start, end, Random.Range(0.3f, 1f)));
                }
            }

            if (level >= 3)
            {
                LayArcs(start, end, axis, scale);
                halo.position = owner.position + Vector3.up * 3.1f;
                halo.rotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
                Color h = SanguinaryKit.BrightBlood;
                h.a = 0.6f + 0.35f * pulse;
                haloMaterial.color = h;
                if (Time.time >= nextMist)
                {
                    nextMist = Time.time + 0.18f;
                    SanguinaryKit.Mist(owner.position, 0.7f, 0.5f);
                }
            }
        }

        void LayArcs(Vector3 start, Vector3 end, Vector3 axis, float scale)
        {
            if (Time.time >= nextFlicker)
            {
                nextFlicker = Time.time + 0.05f;
                for (int i = 0; i < ArcCount; i++)
                {
                    arcAlong[i] = Random.Range(0.2f, 1f);
                    float reach = Random.Range(0.35f, 1f) * arcReach;
                    for (int p = 0; p < ArcPoints; p++)
                    {
                        float wobble = p == 0 ? 0f : 0.08f;
                        arcJitter[i, p] = new Vector3(Random.Range(-wobble, wobble), Random.Range(-wobble, wobble), p == 0 ? 0f : reach);
                    }
                    arcs[i].enabled = Random.value > 0.15f;
                }
            }

            Vector3 side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(axis, Vector3.right);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, axis);
            for (int i = 0; i < ArcCount; i++)
            {
                Vector3 root = Vector3.Lerp(start, end, arcAlong[i]);
                for (int p = 0; p < ArcPoints; p++)
                {
                    float f = p / (float)(ArcPoints - 1);
                    Vector3 j = arcJitter[i, p];
                    Vector3 point = root + (side * j.x + up * j.y) * scale;
                    point += (side * Mathf.Sign(j.x) + up * Mathf.Sign(j.y)).normalized * (j.z * f * scale);
                    arcs[i].SetPosition(p, point);
                }
            }
        }

        void OnDestroy()
        {
            if (sleeveMaterial != null) Destroy(sleeveMaterial);
            if (arcMaterial != null) Destroy(arcMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        // A drop of blood that falls from the blade and is gone when it reaches the floor.
        sealed class BloodDrip : MonoBehaviour
        {
            static Material shared;
            float velocity;
            float floor;

            public static void Spawn(Vector3 at)
            {
                if (shared == null) shared = VisualUtil.UnlitMaterial(SanguinaryKit.Blood, 3002);
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "Blood Drip";
                Destroy(g.GetComponent<Collider>());
                var r = g.GetComponent<Renderer>();
                r.sharedMaterial = shared;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.transform.position = at;
                g.transform.localScale = new Vector3(0.06f, 0.1f, 0.06f);
                var d = g.AddComponent<BloodDrip>();
                d.floor = at.y - 2.5f;
                Destroy(g, 1.2f);
            }

            void Update()
            {
                velocity += 12f * Time.deltaTime;
                transform.position += Vector3.down * (velocity * Time.deltaTime);
                if (transform.position.y < floor) Destroy(gameObject);
            }
        }
    }
}
