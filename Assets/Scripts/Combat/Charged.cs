using UnityEngine;

namespace Anathema.Combat
{
    // Status effect (mark): the Psyker's Chain Lightning leaves its victims "charged". A small glowing rune floats
    // above their head, visible to everyone. It has no stats of its own; other abilities ask IsCharged / Consume to
    // react to it. Re-applying refreshes the duration.
    //
    // Online there is no status message: every machine replays the Psyker's Chain Lightning (the owner for real, the
    // others as a visual replica) and each applies the mark to whoever the bolt struck, so all players see the rune
    // and a Psyker's own machine can read it when deciding what their other abilities do.
    public class Charged : MonoBehaviour
    {
        public const float DefaultDuration = 6f;

        const float RuneHeight = 2.15f;
        const float RuneSize = 0.2f;

        static readonly Color RuneColor = new Color(0.55f, 0.85f, 1f, 1f);

        float endTime;
        Transform rune;
        Material runeMaterial;
        Material glowMaterial;

        public static Charged Apply(GameObject target, float duration = DefaultDuration)
        {
            if (target == null || duration <= 0f) return null;

            var charged = target.GetComponent<Charged>();
            if (charged == null) charged = target.AddComponent<Charged>();
            charged.endTime = Mathf.Max(charged.endTime, Time.time + duration);
            return charged;
        }

        // Online: draws this on a replica (driven by NetPlayer's published status mask); lasts until the mask clears.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Charged _)) return;
            target.AddComponent<Charged>().endTime = float.MaxValue;
        }

        public static bool IsCharged(GameObject target)
        {
            return target != null && target.TryGetComponent(out Charged _);
        }

        // Removes the mark and returns whether there was one - for abilities that spend it.
        public static bool Consume(GameObject target)
        {
            if (target == null || !target.TryGetComponent(out Charged charged)) return false;
            Destroy(charged);
            return true;
        }

        void Awake()
        {
            runeMaterial = VisualUtil.UnlitMaterial(RuneColor, 3006);
            glowMaterial = VisualUtil.UnlitMaterial(new Color(RuneColor.r, RuneColor.g, RuneColor.b, 0.3f), 3005);

            rune = new GameObject("Charged Rune").transform;
            rune.SetParent(transform, false);
            rune.localPosition = Vector3.up * RuneHeight;

            // A diamond frame with a lightning bolt cut through it, drawn in the rune's local XY plane.
            AddLine("Frame", true, runeMaterial, 0.03f,
                new Vector3(0f, 1f), new Vector3(0.7f, 0f), new Vector3(0f, -1f), new Vector3(-0.7f, 0f));
            AddLine("Bolt", false, runeMaterial, 0.035f,
                new Vector3(0.18f, 0.6f), new Vector3(-0.22f, 0.05f), new Vector3(0.15f, 0.05f), new Vector3(-0.18f, -0.6f));
            AddLine("Glow", false, glowMaterial, 0.12f,
                new Vector3(0.18f, 0.6f), new Vector3(-0.22f, 0.05f), new Vector3(0.15f, 0.05f), new Vector3(-0.18f, -0.6f));
        }

        void AddLine(string name, bool loop, Material material, float width, params Vector3[] points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rune, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i] * RuneSize);
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.sharedMaterial = material;
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            // Hover gently and face the camera; blink out for the last second so the player can see it is expiring.
            rune.localPosition = Vector3.up * (RuneHeight + 0.05f * Mathf.Sin(Time.time * 3f));
            Camera cam = Camera.main;
            if (cam != null) rune.rotation = cam.transform.rotation;

            float remaining = endTime - Time.time;
            rune.gameObject.SetActive(remaining > 1f || Mathf.Repeat(Time.time * 6f, 1f) > 0.4f);
        }

        void OnDestroy()
        {
            if (rune != null) Destroy(rune.gameObject);
            if (runeMaterial != null) Destroy(runeMaterial);
            if (glowMaterial != null) Destroy(glowMaterial);
        }
    }
}
