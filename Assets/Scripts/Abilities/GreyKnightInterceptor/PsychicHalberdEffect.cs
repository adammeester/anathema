using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // Psychic charge on the halberd: a pulsing glow along the shaft, a point light, and lightning that crackles off it.
    // Everything is built in world space and re-aimed each LateUpdate from the "Halberd" bone, so it follows the sweep pose.
    [DefaultExecutionOrder(200)] // after HalberdSweep (50) has posed the bones this frame
    public class PsychicHalberdEffect : MonoBehaviour
    {
        const int ArcCount = 5;
        const int ArcPoints = 7;

        // Distances along the shaft from the Halberd bone (the hand), in model metres.
        [SerializeField] float shaftStart = -0.5f;
        [SerializeField] float shaftEnd = 2.45f;
        [SerializeField] Color glowColor = new Color(0.2f, 0.55f, 1f, 0.35f);
        [SerializeField] Color boltColor = new Color(0.6f, 0.9f, 1f, 1f);
        [SerializeField] float arcReach = 0.55f;
        [SerializeField] float flickerInterval = 0.05f;

        Transform halberd;
        Vector3 localAxis = Vector3.up;
        Transform glow;
        Material glowMaterial;
        Light glowLight;
        LineRenderer[] arcs;
        float[] arcAlong;          // 0..1 position of each arc's root along the shaft
        Vector3[,] arcJitter;      // per-point sideways offsets, in a shaft-aligned frame
        float nextFlicker;
        bool built;

        public static PsychicHalberdEffect Create(Transform model)
        {
            if (model == null) return null;

            Transform bone = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Halberd") { bone = t; break; } // depth-first: the real skeleton, not a later glow clone
            }
            if (bone == null) return null;

            var go = new GameObject("Psychic Halberd Effect");
            go.transform.SetParent(model.parent, false);
            var effect = go.AddComponent<PsychicHalberdEffect>();
            effect.halberd = bone;
            // At rest the model stands upright, so world up in the bone's frame is the shaft direction.
            effect.localAxis = bone.InverseTransformDirection(Vector3.up).normalized;
            effect.Build();
            effect.SetActive(false);
            return effect;
        }

        public void SetActive(bool active)
        {
            if (!built) return;
            glow.gameObject.SetActive(active);
            glowLight.enabled = active;
            foreach (LineRenderer arc in arcs) arc.enabled = active;
            enabled = active;
            if (active) nextFlicker = 0f;
        }

        void Build()
        {
            glowMaterial = VisualUtil.UnlitMaterial(glowColor, 3000);
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "Halberd Glow";
            Destroy(shell.GetComponent<Collider>());
            shell.GetComponent<Renderer>().sharedMaterial = glowMaterial;
            shell.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow = shell.transform;
            glow.SetParent(transform, false);

            var lightGo = new GameObject("Halberd Light");
            lightGo.transform.SetParent(transform, false);
            glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(0.3f, 0.6f, 1f);
            glowLight.range = 4f;
            glowLight.intensity = 2.5f;
            glowLight.shadows = LightShadows.None;

            Material boltMaterial = VisualUtil.UnlitMaterial(boltColor, 3100);
            arcs = new LineRenderer[ArcCount];
            arcAlong = new float[ArcCount];
            arcJitter = new Vector3[ArcCount, ArcPoints];
            for (int i = 0; i < ArcCount; i++)
            {
                var arcGo = new GameObject("Halberd Lightning " + i);
                arcGo.transform.SetParent(transform, false);
                LineRenderer lr = arcGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = ArcPoints;
                lr.widthMultiplier = 0.06f;
                lr.numCapVertices = 2;
                lr.sharedMaterial = boltMaterial;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.startColor = lr.endColor = boltColor;
                arcs[i] = lr;
            }

            built = true;
        }

        void OnDestroy()
        {
            if (glowMaterial != null) Destroy(glowMaterial);
        }

        void LateUpdate()
        {
            if (!built || halberd == null) return;

            float scale = halberd.lossyScale.y;
            Vector3 axis = halberd.TransformDirection(localAxis).normalized;
            Vector3 origin = halberd.position;
            Vector3 start = origin + axis * (shaftStart * scale);
            Vector3 end = origin + axis * (shaftEnd * scale);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 18f);

            // Glow sleeve around the shaft; fatter around the blade end.
            glow.position = (start + end) * 0.5f;
            glow.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            glow.localScale = new Vector3(0.28f * scale, (end - start).magnitude * 0.5f, 0.28f * scale);
            Color c = glowColor;
            c.a = glowColor.a * pulse;
            glowMaterial.color = c;

            glowLight.transform.position = Vector3.Lerp(start, end, 0.75f);
            glowLight.intensity = 2.5f * pulse;

            if (Time.time >= nextFlicker)
            {
                nextFlicker = Time.time + flickerInterval;
                Reroll();
            }

            // Re-lay every arc against the current shaft each frame so the bolts stick to the moving halberd.
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
                    // Arcs leap outward from the shaft (j.z is the outward reach, j.x/j.y wobble the path).
                    Vector3 point = root + (side * j.x + up * j.y) * scale;
                    point += (side * Mathf.Sign(j.x) + up * Mathf.Sign(j.y)).normalized * (j.z * f * scale);
                    point += axis * (j.z * (f - 0.5f) * scale);
                    arcs[i].SetPosition(p, point);
                }
                arcs[i].widthMultiplier = Random.Range(0.03f, 0.08f) * scale;
            }
        }

        void Reroll()
        {
            for (int i = 0; i < ArcCount; i++)
            {
                // Bias roots toward the blade end, where the energy is strongest.
                arcAlong[i] = Random.value < 0.6f ? Random.Range(0.6f, 1f) : Random.Range(0.1f, 0.6f);
                float reach = Random.Range(0.35f, 1f) * arcReach;
                for (int p = 0; p < ArcPoints; p++)
                {
                    float wobble = p == 0 ? 0f : 0.09f;
                    arcJitter[i, p] = new Vector3(
                        Random.Range(-wobble, wobble),
                        Random.Range(-wobble, wobble),
                        reach * (p == 0 ? 0f : 1f));
                }
                arcs[i].enabled = Random.value > 0.15f; // occasional dropout sells the flicker
            }
        }
    }
}
