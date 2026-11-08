using UnityEngine;

namespace Anathema.Combat
{
    // A flat splat of blood on the floor that shrinks away over a few seconds.
    public class BloodDrop : MonoBehaviour
    {
        const float Lifetime = 3.5f;
        const float ShrinkTime = 1.2f;

        static Material material;

        Vector3 fullScale;
        float spawnTime;

        public static void Spawn(Vector3 groundPoint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Blood";
            Destroy(go.GetComponent<Collider>());

            if (material == null) material = VisualUtil.UnlitMaterial(new Color(0.5f, 0.02f, 0.02f, 0.95f));
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float size = Random.Range(0.18f, 0.4f);
            Vector2 jitter = Random.insideUnitCircle * 0.25f;
            go.transform.position = groundPoint + new Vector3(jitter.x, 0f, jitter.y);
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var drop = go.AddComponent<BloodDrop>();
            drop.fullScale = new Vector3(size, 0.02f, size * Random.Range(0.6f, 1.2f));
            drop.spawnTime = Time.time;
            go.transform.localScale = drop.fullScale;
        }

        void Update()
        {
            float age = Time.time - spawnTime;
            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float fade = Mathf.Clamp01((Lifetime - age) / ShrinkTime);
            transform.localScale = new Vector3(fullScale.x * fade, fullScale.y, fullScale.z * fade);
        }
    }
}
