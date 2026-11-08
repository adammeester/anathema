using UnityEngine;

namespace Anathema.Combat
{
    // World-space billboarded energy bar, a thin blue strip meant to sit just under the HealthBar.
    [RequireComponent(typeof(Energy))]
    public class EnergyBar : MonoBehaviour
    {
        [SerializeField] float height = 2.45f;
        [SerializeField] float width = 1.6f;
        [SerializeField] float thickness = 0.1f;

        Energy energy;
        Health health;
        Transform holder;
        Transform fill;
        Camera cam;

        public void Configure(float barHeight)
        {
            height = barHeight;
        }

        void Awake()
        {
            energy = GetComponent<Energy>();

            holder = new GameObject("EnergyBar").transform;
            holder.SetParent(transform, false);
            holder.localPosition = new Vector3(0f, height, 0f);

            CreateQuad("Background", new Color(0.05f, 0.05f, 0.05f, 0.85f), 3000, 0f);
            fill = CreateQuad("Fill", new Color(0.25f, 0.6f, 1f), 3001, -0.01f);
        }

        Transform CreateQuad(string quadName, Color color, int renderQueue, float z)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = quadName;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(holder, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            quad.transform.localScale = new Vector3(width, thickness, 1f);

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

            // Online, energy is only known to its owner, so other players' bars stay hidden.
            if (health == null) health = GetComponent<Health>();
            bool show = health.IsMine;
            if (holder.gameObject.activeSelf != show) holder.gameObject.SetActive(show);

            float t = energy.Normalized;
            fill.localScale = new Vector3(width * t, thickness, 1f);
            fill.localPosition = new Vector3(-width * 0.5f * (1f - t), 0f, -0.01f);
        }
    }
}
