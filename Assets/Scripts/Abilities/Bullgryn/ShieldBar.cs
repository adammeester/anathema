using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // World-space billboarded bar just above the Bullgryn's health bar: how much his slabshield can still take (Brace's
    // stamina). Shown on every screen (every copy tracks the same stamina, see Brace), so enemies can see a guard about
    // to break. Hazard yellow normally; red and dim while a broken guard is locked out. Hidden while Living Wall
    // stands (the shield is in the ground) and when he is down. Added by Brace at runtime.
    public class ShieldBar : MonoBehaviour
    {
        [SerializeField] float height = 3.38f;     // the Bullgryn's health bar sits at 3.2 (ArenaSceneBuilder)
        [SerializeField] float width = 1.6f;
        [SerializeField] float thickness = 0.09f;

        static readonly Color Ready = new Color(0.95f, 0.65f, 0.1f);
        static readonly Color Broken = new Color(0.75f, 0.2f, 0.15f);

        Brace brace;
        Health health;
        Transform holder;
        Transform fill;
        Material fillMaterial;
        Camera cam;

        void Awake()
        {
            brace = GetComponent<Brace>();
            health = GetComponent<Health>();

            holder = new GameObject("ShieldBar").transform;
            holder.SetParent(transform, false);
            holder.localPosition = new Vector3(0f, height, 0f);

            CreateQuad("Border", new Color(0.86f, 0.70f, 0.30f), 3000, 0.005f, width + 0.05f, thickness + 0.05f);
            CreateQuad("Background", new Color(0.05f, 0.05f, 0.05f, 0.9f), 3001, 0f, width, thickness);
            fill = CreateQuad("Fill", Ready, 3002, -0.01f, width, thickness);
            fillMaterial = fill.GetComponent<Renderer>().material;
        }

        Transform CreateQuad(string quadName, Color color, int renderQueue, float z, float w, float h)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = quadName;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(holder, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            quad.transform.localScale = new Vector3(w, h, 1f);

            Renderer r = quad.GetComponent<Renderer>();
            r.material = VisualUtil.UnlitMaterial(color, renderQueue);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad.transform;
        }

        void LateUpdate()
        {
            bool show = brace != null && !health.IsDead && !BullgrynKit.ShieldPlanted(gameObject);
            if (holder.gameObject.activeSelf != show) holder.gameObject.SetActive(show);
            if (!show) return;

            if (cam == null) cam = Camera.main;
            if (cam != null) holder.rotation = cam.transform.rotation;

            float t = Mathf.Clamp01(brace.Stamina / Mathf.Max(1f, brace.MaxStamina));
            fill.localScale = new Vector3(width * t, thickness, 1f);
            fill.localPosition = new Vector3(-width * 0.5f * (1f - t), 0f, -0.01f);
            fillMaterial.color = brace.GuardBroken ? new Color(Broken.r, Broken.g, Broken.b, 0.6f) : Ready;
        }

        void OnDestroy()
        {
            if (holder == null) return;
            foreach (Renderer r in holder.GetComponentsInChildren<Renderer>()) Destroy(r.material);
        }
    }
}
