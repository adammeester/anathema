using UnityEngine;

namespace Anathema.Combat
{
    // World-space billboarded health bar built from two quads. Green for team 1 (the player's team), red otherwise.
    [RequireComponent(typeof(Health))]
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] float height = 2.6f;
        [SerializeField] float width = 1.6f;
        [SerializeField] float thickness = 0.16f;

        Health health;
        bool? lastFriendly;
        Transform holder;
        Transform fill;
        Transform recoverable; // white segment: damage that can still be healed
        Camera cam;

        const float BorderSize = 0.03f;
        const float DividerSize = 0.02f;
        const float SegmentSize = Health.ChunkSize; // health per chunk
        readonly System.Collections.Generic.List<Transform> dividers = new System.Collections.Generic.List<Transform>();
        float builtMax = -1f;

        public void Configure(float barHeight)
        {
            height = barHeight;
        }

        void Awake()
        {
            health = GetComponent<Health>();

            holder = new GameObject("HealthBar").transform;
            holder.SetParent(transform, false);
            holder.localPosition = new Vector3(0f, height, 0f);

            // Outline only (four thin quads). Healable missing health is black; permanently lost health is left
            // empty (see-through), so there is no background quad.
            float bw = width + BorderSize * 2f, bh = thickness + BorderSize * 2f;
            Color border = new Color(0.86f, 0.70f, 0.30f);
            CreateEdge("BorderTop", border, 0f, (thickness + BorderSize) * 0.5f, bw, BorderSize);
            CreateEdge("BorderBottom", border, 0f, -(thickness + BorderSize) * 0.5f, bw, BorderSize);
            CreateEdge("BorderLeft", border, -(width + BorderSize) * 0.5f, 0f, BorderSize, bh);
            CreateEdge("BorderRight", border, (width + BorderSize) * 0.5f, 0f, BorderSize, bh);
            recoverable = CreateQuad("Recoverable", Color.black, 3001, -0.005f);
            fill =CreateQuad("Fill", health.Team == 1 ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.9f, 0.2f, 0.2f), 3002, -0.01f);
        }

        void CreateEdge(string edgeName, Color color, float x, float y, float w, float h)
        {
            Transform e = CreateQuad(edgeName, color, 3000, 0f, w, h);
            e.localPosition = new Vector3(x, y, 0f);
        }

        // Black dividers splitting the bar into chunks of `SegmentSize` health. Rebuilt if max health changes.
        void BuildDividers()
        {
            builtMax = health.Max;
            foreach (Transform d in dividers) if (d != null) Destroy(d.gameObject);
            dividers.Clear();

            int segments = Mathf.Max(1, Mathf.RoundToInt(health.Max / SegmentSize));
            for (int i = 1; i < segments; i++)
            {
                Transform d = CreateQuad("Divider", Color.black, 3003, -0.015f, DividerSize, thickness);
                d.localPosition = new Vector3(-width * 0.5f + width * i / segments, 0f, -0.015f);
                dividers.Add(d);
            }
        }

        Transform CreateQuad(string quadName, Color color, int renderQueue, float z, float quadWidth = -1f, float quadHeight = -1f)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = quadName;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(holder, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            quad.transform.localScale = new Vector3(quadWidth < 0f ? width : quadWidth, quadHeight < 0f ? thickness : quadHeight, 1f);

            Renderer r = quad.GetComponent<Renderer>();
            r.material = VisualUtil.UnlitMaterial(color, renderQueue);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad.transform;
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) holder.rotation = cam.transform.rotation;

            // Online: your own bar is green, everyone else's red. Offline: team 1 is green.
            bool friendly = health.Net != null && health.Net.IsSpawned ? health.IsMine : health.Team == 1;
            if (friendly != lastFriendly)
            {
                lastFriendly = friendly;
                fill.GetComponent<Renderer>().material.color = friendly ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.9f, 0.2f, 0.2f);
            }

            // Anchor the fill's left edge to the background's left edge.
            float t = health.Normalized;
            fill.localScale = new Vector3(width * t, thickness, 1f);
            fill.localPosition = new Vector3(-width * 0.5f * (1f - t), 0f, -0.01f);

            if (!Mathf.Approximately(builtMax, health.Max)) BuildDividers();

            // Black (healable) segment sits directly after the fill; everything past it is empty.
            float w = t >= 1f ? 0f : Mathf.Min(health.RecoverableNormalized, 1f - t);
            recoverable.localScale = new Vector3(width * w, thickness, 1f);
            recoverable.localPosition = new Vector3(-width * 0.5f + width * (t + w * 0.5f), 0f, -0.005f);

            // Dividers only show over the fill + healable part, not over the empty part.
            int segments = dividers.Count + 1;
            for (int i = 0; i < dividers.Count; i++)
                dividers[i].gameObject.SetActive((float)(i + 1) / segments <= t + w + 0.0001f);
        }
    }
}
