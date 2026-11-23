using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // The Haymaker charge on the slab maul: while it is ready the maul's power field flares, a green glow sleeve around
    // the slab, a green light and crackling arcs off it. This is the tell enemies read before the heavy blow.
    // Built in world space and re-aimed every LateUpdate from the Maul bone, so it follows the swing. On replicas it
    // follows the owner's published "primed" flag (SlabMaul.HaymakerGlowing).
    [DefaultExecutionOrder(200)] // after SlabMaul (50) has posed the arm this frame
    public class MaulGlowEffect : MonoBehaviour
    {
        const int ArcCount = 4;
        const int ArcPoints = 5;

        // The slab runs from 0.29 to 1.32 m along the maul from the grip (Art/bullgryn.py, power_maul). In the rest pose
        // it leans forward 28 degrees and slightly outward: this is that direction in the model's space.
        static readonly Vector3 RestMaulDirection = new Vector3(0.123f, 0.874f, 0.469f);
        const float SlabStart = 0.29f;
        const float SlabEnd = 1.32f;
        const float ArcReach = 0.4f;

        SlabMaul maul;
        Transform weapon;
        Vector3 localAxis = Vector3.up;
        Transform sleeve;
        Material sleeveMaterial;
        Light glowLight;
        LineRenderer[] arcs;
        float[] arcAlong;
        Vector3[,] arcJitter;
        Material arcMaterial;
        float nextFlicker;
        bool shown = true;

        public static MaulGlowEffect Create(Transform model, SlabMaul maul)
        {
            if (model == null || maul == null) return null;
            Transform bone = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Maul") continue;
                bone = t;
                break;
            }
            if (bone == null) return null;

            var go = new GameObject("Haymaker Glow");
            go.transform.SetParent(model.parent, false);
            var effect = go.AddComponent<MaulGlowEffect>();
            effect.maul = maul;
            effect.weapon = bone;
            effect.localAxis = bone.InverseTransformDirection(model.TransformDirection(RestMaulDirection.normalized)).normalized;
            effect.Build();
            effect.Show(false);
            return effect;
        }

        void Build()
        {
            sleeveMaterial = VisualUtil.UnlitMaterial(BullgrynKit.Power, 3000);
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "Maul Glow";
            Destroy(shell.GetComponent<Collider>());
            var r = shell.GetComponent<Renderer>();
            r.sharedMaterial = sleeveMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sleeve = shell.transform;
            sleeve.SetParent(transform, false);

            var lightGo = new GameObject("Maul Light");
            lightGo.transform.SetParent(transform, false);
            glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(0.35f, 1f, 0.45f);
            glowLight.range = 4f;
            glowLight.shadows = LightShadows.None;

            arcMaterial = VisualUtil.UnlitMaterial(new Color(0.75f, 1f, 0.8f, 0.95f), 3100);
            arcs = new LineRenderer[ArcCount];
            arcAlong = new float[ArcCount];
            arcJitter = new Vector3[ArcCount, ArcPoints];
            for (int i = 0; i < ArcCount; i++)
            {
                var arcGo = new GameObject("Maul Arc " + i);
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
        }

        void Show(bool on)
        {
            if (shown == on) return;
            shown = on;
            sleeve.gameObject.SetActive(on);
            glowLight.enabled = on;
            foreach (LineRenderer arc in arcs) arc.enabled = on;
            nextFlicker = 0f;
        }

        void LateUpdate()
        {
            if (maul == null || weapon == null)
            {
                Destroy(gameObject);
                return;
            }

            float scale = weapon.lossyScale.y;
            Show(maul.HaymakerGlowing && scale > 0.001f);
            if (!shown) return;

            Vector3 axis = weapon.TransformDirection(localAxis).normalized;
            Vector3 start = weapon.position + axis * (SlabStart * scale);
            Vector3 end = weapon.position + axis * (SlabEnd * scale);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);

            sleeve.position = (start + end) * 0.5f;
            sleeve.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            sleeve.localScale = new Vector3(0.34f * scale, (end - start).magnitude * 0.5f, 0.34f * scale);
            Color c = BullgrynKit.Power;
            c.a = 0.45f * pulse;
            sleeveMaterial.color = c;

            glowLight.transform.position = Vector3.Lerp(start, end, 0.7f);
            glowLight.intensity = 3f * pulse;

            LayArcs(start, end, axis, scale);
        }

        void LayArcs(Vector3 start, Vector3 end, Vector3 axis, float scale)
        {
            if (Time.time >= nextFlicker)
            {
                nextFlicker = Time.time + 0.06f;
                for (int i = 0; i < ArcCount; i++)
                {
                    arcAlong[i] = Random.Range(0.15f, 1f);
                    float reach = Random.Range(0.4f, 1f) * ArcReach;
                    for (int p = 0; p < ArcPoints; p++)
                    {
                        float wobble = p == 0 ? 0f : 0.08f;
                        arcJitter[i, p] = new Vector3(Random.Range(-wobble, wobble), Random.Range(-wobble, wobble), p == 0 ? 0f : reach);
                    }
                    arcs[i].enabled = Random.value > 0.2f;
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
        }
    }
}
