using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Combat
{
    // Draws a character's body (everything under "Model") with one translucent, gently pulsing colour instead of its own
    // materials. Several effects can ask for it at once (stealth's blue ghost, Shadow-Kin's faint shadow): each request
    // has a key and a priority, the highest priority wins, and the body's own materials come back when the last request
    // is released. Body renderers added later are picked up too.
    public class GhostLook : MonoBehaviour
    {
        struct Request
        {
            public Color Color;
            public int Priority;
        }

        readonly Dictionary<string, Request> requests = new Dictionary<string, Request>();
        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        Material material;

        public static void Apply(GameObject target, string key, Color color, int priority)
        {
            if (target == null) return;
            var look = target.GetComponent<GhostLook>();
            if (look == null) look = target.AddComponent<GhostLook>();
            look.requests[key] = new Request { Color = color, Priority = priority };
        }

        public static void Release(GameObject target, string key)
        {
            if (target == null || !target.TryGetComponent(out GhostLook look)) return;
            look.requests.Remove(key);
            if (look.requests.Count == 0) Destroy(look);
        }

        void LateUpdate()
        {
            if (requests.Count == 0) return;
            Request top = default;
            int best = int.MinValue;
            foreach (Request r in requests.Values)
            {
                if (r.Priority <= best) continue;
                best = r.Priority;
                top = r;
            }

            if (material == null) material = VisualUtil.UnlitMaterial(top.Color, 3000);
            Color c = top.Color;
            c.a = top.Color.a * (0.75f + 0.25f * Mathf.Sin(Time.time * 5f));
            material.color = c;

            Transform model = transform.Find("Model");
            if (model == null) return;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (originals.ContainsKey(r)) continue;
                originals[r] = r.sharedMaterials;
                var ghosts = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ghosts.Length; i++) ghosts[i] = material;
                r.sharedMaterials = ghosts;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void OnDestroy()
        {
            foreach (var pair in originals)
            {
                if (pair.Key == null) continue;
                pair.Key.sharedMaterials = pair.Value;
                pair.Key.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            originals.Clear();
            if (material != null) Destroy(material);
        }
    }
}
