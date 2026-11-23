using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Combat
{
    // What a fighter leaves behind when they die: their model frozen in its last pose (skinned meshes baked), which
    // topples over away from the killing blow, hits the floor with a thud, darkens, and bleeds a pool onto the ground.
    // It stays where they fell until the fighter comes back (the Respawner removes it at the round reset).
    // Purely cosmetic and local: every machine builds its own from its own copy of the fighter.
    public class Corpse : MonoBehaviour
    {
        const float FallTime = 0.55f;      // from standing to flat on the floor
        const float FallAngle = 86f;
        const float BounceTime = 0.22f;
        const float DarkenTime = 2.5f;
        const float DarkenTo = 0.42f;      // final brightness of the body
        const float PoolGrowTime = 3.5f;
        const float PoolSize = 2.4f;

        static Texture2D poolTexture;
        static Material poolMaterial;

        Transform pivot;
        Transform pool;
        Vector3 axis;
        float born;
        float startHeight;
        bool landed;
        readonly List<Material> materials = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();

        // `victim` is the dying fighter (still visible: call this before hiding it); `killer` may be null.
        public static Corpse Spawn(GameObject victim, GameObject killer)
        {
            Transform model = victim.transform.Find("Model");
            Vector3 feet = victim.transform.position;
            Vector3 fall = killer != null && killer != victim ? feet - killer.transform.position : -victim.transform.forward;
            fall.y = 0f;
            fall = fall.sqrMagnitude > 0.0001f ? fall.normalized : -victim.transform.forward;

            float ground = FindGround(victim, feet);
            var go = new GameObject("Corpse");
            go.transform.position = new Vector3(feet.x, ground, feet.z);
            var corpse = go.AddComponent<Corpse>();
            corpse.Build(victim, model, fall, feet.y - ground);

            AbilityFx.Death(go.transform.position);
            BloodImpact.Play(feet + Vector3.up * 1.2f, fall, 1.6f, silent: true);
            return corpse;
        }

        // The floor under the body (ignoring the body's own colliders); where it stands if nothing is found.
        static float FindGround(GameObject victim, Vector3 feet)
        {
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(feet + Vector3.up * 0.5f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(victim.transform)) continue;
                if (hit.point.y > best) best = hit.point.y;
            }
            return float.IsNegativeInfinity(best) ? feet.y : best;
        }

        void Build(GameObject victim, Transform model, Vector3 fall, float heightAboveGround)
        {
            born = Time.time;
            axis = Vector3.Cross(Vector3.up, fall);

            pivot = new GameObject("Body").transform;
            pivot.SetParent(transform, false);

            if (model != null)
            {
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    Mesh mesh = null;
                    if (r is SkinnedMeshRenderer skinned)
                    {
                        mesh = new Mesh();
                        skinned.BakeMesh(mesh, true); // bakes the current pose and the bone scale
                        CopyPart(r, mesh, Vector3.one, owned: true);
                    }
                    else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    {
                        CopyPart(r, filter.sharedMesh, r.transform.lossyScale, owned: false);
                    }
                }
            }
            // A body knocked into the air drops to the floor as it falls.
            startHeight = Mathf.Clamp(heightAboveGround, 0f, 6f);
            pivot.localPosition = Vector3.up * startHeight;

            pool = BuildPool(fall);
        }

        readonly List<Mesh> ownedMeshes = new List<Mesh>();

        void CopyPart(Renderer source, Mesh mesh, Vector3 worldScale, bool owned)
        {
            var part = new GameObject(source.name);
            part.transform.SetParent(pivot, false);
            part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            part.transform.localScale = worldScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = source.shadowCastingMode;

            // Own material copies so the body can darken without touching the living fighters' materials.
            Material[] shared = source.sharedMaterials;
            var copies = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null) continue;
                copies[i] = new Material(shared[i]);
                materials.Add(copies[i]);
                baseColors.Add(copies[i].HasProperty("_Color") ? copies[i].color : Color.white);
            }
            renderer.sharedMaterials = copies;
            if (owned) ownedMeshes.Add(mesh);
        }

        Transform BuildPool(Vector3 fall)
        {
            if (poolTexture == null)
            {
                const int size = 64;
                poolTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        // Slightly lumpy edge so it doesn't read as a perfect disc.
                        float angle = Mathf.Atan2(dy, dx);
                        float edge = 0.86f + 0.08f * Mathf.Sin(angle * 3f + 1f) + 0.05f * Mathf.Sin(angle * 7f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                        float a = Mathf.Clamp01((1f - d) * 6f);
                        poolTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                poolTexture.Apply();
            }
            if (poolMaterial == null)
            {
                poolMaterial = VisualUtil.UnlitMaterial(new Color(0.32f, 0.01f, 0.02f, 0.88f), 2450);
                poolMaterial.mainTexture = poolTexture;
            }

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Blood Pool";
            Destroy(quad.GetComponent<Collider>());
            quad.GetComponent<MeshRenderer>().sharedMaterial = poolMaterial;
            quad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.transform.SetParent(transform, false);
            // Under the torso, which ends up about a metre from the feet along the fall.
            quad.transform.position = transform.position + fall * 0.9f + Vector3.up * 0.04f;
            quad.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            quad.transform.localScale = Vector3.zero;
            return quad.transform;
        }

        void Update()
        {
            float t = Time.time - born;

            // Topple: accelerates like a falling plank, then a small bounce as it lands.
            float angle;
            if (t < FallTime)
            {
                float f = t / FallTime;
                angle = FallAngle * f * f;
            }
            else
            {
                if (!landed)
                {
                    landed = true;
                    AbilityFx.CorpseLanded(transform.position + (pivot.rotation * Vector3.up) * 0.9f);
                }
                float b = Mathf.Clamp01((t - FallTime) / BounceTime);
                angle = FallAngle - Mathf.Sin(b * Mathf.PI) * 7f;
            }
            pivot.localRotation = Quaternion.AngleAxis(angle, axis);

            // Drop from wherever it died to the floor; lying down, lift a little so the back doesn't sink in.
            float drop = Mathf.Clamp01(t / FallTime);
            float lift = 0.18f * Mathf.Sin(angle * Mathf.Deg2Rad);
            pivot.localPosition = new Vector3(0f, Mathf.Lerp(startHeight, lift, drop * drop), 0f);

            // Darken (and half-desaturate) to a lifeless shade.
            if (t <= DarkenTime + 0.1f)
            {
                float k = Mathf.Clamp01(t / DarkenTime);
                for (int i = 0; i < materials.Count; i++)
                {
                    if (!materials[i].HasProperty("_Color")) continue;
                    Color c = baseColors[i];
                    Color target = Color.Lerp(c, new Color(c.grayscale, c.grayscale, c.grayscale), 0.5f) * DarkenTo;
                    target.a = c.a;
                    materials[i].color = Color.Lerp(c, target, k);
                }
            }

            // The pool spreads out once the body is down.
            if (pool != null)
            {
                float p = Mathf.Clamp01((t - FallTime * 0.8f) / PoolGrowTime);
                float s = PoolSize * (1f - (1f - p) * (1f - p));
                pool.localScale = new Vector3(s, s, 1f);
            }
        }

        void OnDestroy()
        {
            foreach (Material m in materials) if (m != null) Destroy(m);
            foreach (Mesh m in ownedMeshes) if (m != null) Destroy(m);
        }
    }
}
