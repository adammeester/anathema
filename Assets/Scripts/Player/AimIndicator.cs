using Anathema.Combat;
using UnityEngine;

namespace Anathema.Player
{
    // Aim line from the character to the cursor plus a ground reticle at the cursor.
    [RequireComponent(typeof(MouseAim), typeof(Health))]
    public class AimIndicator : MonoBehaviour
    {
        [SerializeField] float lineHeight = 0.1f;
        [SerializeField] float maxLength = 14f;
        [SerializeField] Color color = new Color(1f, 1f, 1f, 0.35f);

        MouseAim aim;
        Health health;
        LineRenderer line;
        Transform reticle;

        void Awake()
        {
            aim = GetComponent<MouseAim>();
            health = GetComponent<Health>();

            var lineObject = new GameObject("AimLine");
            line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = line.endWidth = 0.08f;
            line.sharedMaterial = VisualUtil.UnlitMaterial(color);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "AimReticle";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.localScale = new Vector3(0.6f, 0.01f, 0.6f);
            disc.GetComponent<Renderer>().sharedMaterial = VisualUtil.UnlitMaterial(new Color(1f, 1f, 1f, 0.6f));
            disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            reticle = disc.transform;
        }

        void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
            if (reticle != null) Destroy(reticle.gameObject);
        }

        void LateUpdate()
        {
            bool visible = !health.IsDead;
            line.enabled = visible;
            reticle.gameObject.SetActive(visible);
            if (!visible) return;

            Vector3 start = transform.position + Vector3.up * lineHeight;
            Vector3 toCursor = aim.AimPoint - transform.position;
            toCursor.y = 0f;
            Vector3 end = start + Vector3.ClampMagnitude(toCursor, maxLength);

            line.SetPosition(0, start);
            line.SetPosition(1, end);
            reticle.position = new Vector3(aim.AimPoint.x, transform.position.y + lineHeight, aim.AimPoint.z);
        }
    }
}
