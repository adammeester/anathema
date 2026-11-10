using UnityEngine;

namespace Anathema.Combat
{
    // World-space Rage display: one small red dot per stack above the character's head, and at full Rage a pulsing
    // Khorne rune on the floor under their feet. Built from quads (dots billboard to the camera), added by Rage.
    [RequireComponent(typeof(Rage))]
    public class RageIndicator : MonoBehaviour
    {
        const float DotHeight = 3.0f;   // above the health bar (2.6)
        const float DotSize = 0.16f;
        const float DotSpacing = 0.26f;
        const int MaxDots = 10;

        static readonly Color DotColor = new Color(0.95f, 0.08f, 0.05f, 1f);
        static readonly Color DotEmpty = new Color(0.25f, 0.03f, 0.03f, 0.45f);
        static readonly Color RuneColor = new Color(1f, 0.12f, 0.05f, 0.85f);

        static Texture2D dotTexture;
        static Texture2D runeTexture;

        Rage rage;
        Transform dotHolder;
        Transform[] dots = new Transform[MaxDots];
        Material[] dotMaterials = new Material[MaxDots];
        Transform rune;
        Material runeMaterial;
        Camera cam;

        void Awake()
        {
            rage = GetComponent<Rage>();

            dotHolder = new GameObject("RageDots").transform;
            dotHolder.SetParent(transform, false);
            dotHolder.localPosition = new Vector3(0f, DotHeight, 0f);

            for (int i = 0; i < MaxDots; i++)
            {
                dotMaterials[i] = VisualUtil.UnlitMaterial(Color.white, 3004);
                dotMaterials[i].mainTexture = DotTexture();
                dots[i] = CreateQuad("Dot", dotHolder, dotMaterials[i]);
                dots[i].localScale = new Vector3(DotSize, DotSize, 1f);
                dots[i].gameObject.SetActive(false);
            }

            runeMaterial = VisualUtil.UnlitMaterial(Color.white);
            runeMaterial.mainTexture = RuneTexture();
            rune = CreateQuad("Khorne Rune", transform, runeMaterial);
            rune.localPosition = new Vector3(0f, 0.18f, 0f); // above the arena's visible floor discs
            rune.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rune.gameObject.SetActive(false);
        }

        static Transform CreateQuad(string quadName, Transform parent, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = quadName;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            Renderer r = quad.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad.transform;
        }

        void LateUpdate()
        {
            int stacks = rage.DisplayStacks;
            int max = Mathf.Clamp(rage.DisplayMax, 1, MaxDots);
            bool full = stacks >= max;

            if (cam == null) cam = Camera.main;
            if (cam != null) dotHolder.rotation = cam.transform.rotation;

            // Dots are shown only while Rage is active; unfilled slots are dim so the cap is readable.
            for (int i = 0; i < MaxDots; i++)
            {
                bool show = stacks > 0 && i < max;
                dots[i].gameObject.SetActive(show);
                if (!show) continue;
                dots[i].localPosition = new Vector3((i - (max - 1) * 0.5f) * DotSpacing, 0f, 0f);
                dotMaterials[i].color = i < stacks ? DotColor : DotEmpty;
            }

            rune.gameObject.SetActive(full);
            if (full)
            {
                // Slow pulse; when Rage is about to expire it flickers faster and fades, then disappears.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (rage.DisplayExpiring ? 22f : 6f));
                Color c = RuneColor;
                c.a = RuneColor.a * (rage.DisplayExpiring ? 0.25f + 0.75f * pulse : 0.6f + 0.4f * pulse);
                runeMaterial.color = c;
                float size = 2.6f + 0.15f * pulse;
                rune.localScale = new Vector3(size, size, 1f);
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < MaxDots; i++) if (dotMaterials[i] != null) Destroy(dotMaterials[i]);
            if (runeMaterial != null) Destroy(runeMaterial);
        }

        // ---------- Procedural textures ----------

        static Texture2D DotTexture()
        {
            if (dotTexture != null) return dotTexture;
            const int n = 32;
            dotTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    dotTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 8f)));
                }
            dotTexture.Apply();
            return dotTexture;
        }

        // A stylised Mark of Khorne: a ring around a squared rune with a cross cut through it and a skull-like notch
        // at the top. Drawn with simple distance tests so no art asset is needed.
        static Texture2D RuneTexture()
        {
            if (runeTexture != null) return runeTexture;
            const int n = 256;
            runeTexture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Centre-origin coordinates in -1..1.
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float au = Mathf.Abs(u), av = Mathf.Abs(v);

                    bool ring = r > 0.88f && r < 0.98f;
                    bool innerRing = r > 0.80f && r < 0.83f;

                    // Squared rune body (outline) with a cross cutting through it.
                    float box = Mathf.Max(au, av);
                    bool boxOutline = box > 0.46f && box < 0.56f;
                    bool cross = (au < 0.07f && av < 0.56f) || (av < 0.07f && au < 0.56f);
                    // Four corner prongs pointing at the ring.
                    bool prong = (au > 0.5f && au < 0.72f && av < 0.07f) || (av > 0.5f && av < 0.72f && au < 0.07f);
                    // Horns: diagonal strokes from the top corners.
                    bool horn = Mathf.Abs(au - av) < 0.06f && box > 0.56f && box < 0.74f;

                    bool on = ring || innerRing || boxOutline || cross || prong || horn;
                    runeTexture.SetPixel(x, y, new Color(1f, 1f, 1f, on ? 1f : 0f));
                }
            runeTexture.Apply();
            return runeTexture;
        }
    }
}
