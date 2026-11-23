using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // A splash of blood bursting across the ground: a dark pool in the middle, blots scattered out to `radius` and
    // streaks flung outward from the centre. It snaps open in a fraction of a second, lies there a moment, then fades.
    // Purely visual, drawn on every machine (Chalice of Wrath's landing, so everyone can tell it was used).
    public class BloodSplatter : MonoBehaviour
    {
        const float OpenTime = 0.12f;
        const float Hold = 0.6f;
        const float Fade = 0.9f;
        const float Height = 0.17f;   // above the arena's visible floor discs

        static readonly Color PoolColor = new Color(0.4f, 0.01f, 0.03f, 0.85f);
        static readonly Color SplashColor = new Color(0.7f, 0.03f, 0.05f, 0.8f);

        Material poolMaterial, splashMaterial;
        Transform[] pieces;
        float born;

        public static void Spawn(Vector3 centre, float radius)
        {
            var go = new GameObject("Blood Splatter");
            go.transform.position = new Vector3(centre.x, centre.y + Height, centre.z);
            go.AddComponent<BloodSplatter>().Build(radius);
        }

        void Build(float radius)
        {
            born = Time.time;
            poolMaterial = VisualUtil.UnlitMaterial(PoolColor, 2990);
            splashMaterial = VisualUtil.UnlitMaterial(SplashColor, 2991);

            const int Blots = 16, Streaks = 12;
            pieces = new Transform[1 + Blots + Streaks];
            int n = 0;

            // The pool where the chalice spilled.
            Add(ref n, PrimitiveType.Cylinder, poolMaterial, Vector3.zero, Quaternion.identity,
                new Vector3(radius * 0.45f, 0.005f, radius * 0.4f));

            // Blots scattered across the reach, bigger near the middle.
            for (int i = 0; i < Blots; i++)
            {
                float dist = Mathf.Sqrt(Random.value) * radius * 0.92f;
                float angle = Random.Range(0f, 360f);
                Vector3 at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
                float size = Mathf.Lerp(0.55f, 0.18f, dist / radius) * Random.Range(0.7f, 1.3f);
                Add(ref n, PrimitiveType.Cylinder, i % 3 == 0 ? poolMaterial : splashMaterial, at,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(size, 0.006f, size * Random.Range(0.6f, 1f)));
            }

            // Streaks flung outward, thin at the rim.
            for (int i = 0; i < Streaks; i++)
            {
                float angle = i * 360f / Streaks + Random.Range(-12f, 12f);
                float length = radius * Random.Range(0.45f, 0.9f);
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 at = rot * Vector3.forward * (radius * 0.2f + length * 0.5f);
                Add(ref n, PrimitiveType.Cube, splashMaterial, at, rot, new Vector3(Random.Range(0.07f, 0.14f), 0.007f, length));
            }
        }

        void Add(ref int n, PrimitiveType type, Material material, Vector3 at, Quaternion rot, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            pieces[n] = go.transform;
            n++;
        }

        void Update()
        {
            float age = Time.time - born;
            if (age >= OpenTime + Hold + Fade)
            {
                Destroy(gameObject);
                return;
            }

            // Bursts open from the middle outward (the whole splash scales out flat across the ground).
            float open = Mathf.Clamp01(age / OpenTime);
            open = 1f - (1f - open) * (1f - open);
            transform.localScale = new Vector3(open, 1f, open);

            float fade = 1f - Mathf.Clamp01((age - OpenTime - Hold) / Fade);
            poolMaterial.color = new Color(PoolColor.r, PoolColor.g, PoolColor.b, PoolColor.a * fade);
            splashMaterial.color = new Color(SplashColor.r, SplashColor.g, SplashColor.b, SplashColor.a * fade);
        }

        void OnDestroy()
        {
            if (poolMaterial != null) Destroy(poolMaterial);
            if (splashMaterial != null) Destroy(splashMaterial);
        }
    }
}
