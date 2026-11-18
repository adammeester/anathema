using Anathema.Combat;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // Shared colours, visuals and targeting for the Thousand Sons Sorcerer: Prosperine sand-gold and the cold blue of
    // Tzeentch's warp-fire.
    public static class SorcererFx
    {
        public static readonly Color Sand = new Color(0.95f, 0.78f, 0.38f, 0.95f);
        public static readonly Color Warp = new Color(0.3f, 0.78f, 1f, 0.95f);
        public static readonly Color Glass = new Color(0.95f, 0.85f, 0.55f, 0.9f);

        public static Vector3 Flat(Vector3 v, Transform fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? fallback.forward : v.normalized;
        }

        // A bolt: a sand-gold core in a blue warp halo, trailing dust. Returns the root to hand to a Projectile.
        public static GameObject Bolt(string boltName, Vector3 at, float radius, Color core, Color halo)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = boltName;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = at;
            go.transform.localScale = Vector3.one * (radius * 1.3f);
            Material coreMat = VisualUtil.UnlitMaterial(core);
            Material haloMat = VisualUtil.UnlitMaterial(new Color(halo.r, halo.g, halo.b, 0.35f));
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = coreMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            h.name = "Halo";
            Object.Destroy(h.GetComponent<Collider>());
            h.transform.SetParent(go.transform, false);
            h.transform.localScale = Vector3.one * 2.1f;
            h.GetComponent<Renderer>().sharedMaterial = haloMat;
            h.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = radius * 1.2f;
            trail.endWidth = 0f;
            trail.sharedMaterial = haloMat;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.AddComponent<MaterialCleanup>().Track(coreMat, haloMat);
            return go;
        }

        // Teammate (the caster included) nearest `point`, within `range` of the caster and, when `needSight`, with no
        // wall between them. Falls back to the caster.
        public static Health NearestAlly(Health caster, Vector3 point, float range, bool needSight)
        {
            Health best = caster;
            float bestDistance = float.MaxValue;
            foreach (Health h in Object.FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure || h.Team != caster.Team || !h.TryGetComponent(out MouseAim _)) continue;
                Vector3 fromCaster = h.transform.position - caster.transform.position;
                fromCaster.y = 0f;
                if (fromCaster.magnitude > range) continue;
                if (needSight && h != caster && !ClearLine(caster.transform.position, h.transform.position)) continue;
                Vector3 d = h.transform.position - point;
                d.y = 0f;
                if (d.magnitude < bestDistance)
                {
                    bestDistance = d.magnitude;
                    best = h;
                }
            }
            return best;
        }

        // No wall or pillar between two characters (characters themselves don't block).
        public static bool ClearLine(Vector3 a, Vector3 b)
        {
            Vector3 from = a + Vector3.up * 1.2f;
            Vector3 to = b + Vector3.up * 1.2f;
            foreach (RaycastHit hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<Health>() == null) return false;
            return true;
        }

        // Destroys materials made for a throwaway visual when it goes.
        public sealed class MaterialCleanup : MonoBehaviour
        {
            Material[] materials;
            public void Track(params Material[] m) => materials = m;

            void OnDestroy()
            {
                if (materials == null) return;
                foreach (Material m in materials) if (m != null) Destroy(m);
            }
        }
    }
}
