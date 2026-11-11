using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // A jagged crack in the ground that opens along a line as the Berserker charges down it: a dark gouge with a
    // glowing blood-red seam and branching cracks. Reveal(distance) opens the fissure up to that far along it;
    // Finish() starts the fade. Destroys itself afterwards.
    public class BloodFissure : MonoBehaviour
    {
        const float FloorTop = 0.14f; // just above the arena's visible floor discs
        const float Linger = 3f;
        const float FadeTime = 1.2f;

        struct Piece
        {
            public GameObject go;
            public float distance;
        }

        readonly List<Piece> pieces = new List<Piece>();
        Material darkMaterial;
        Material glowMaterial;
        Color darkColor;
        Color glowColor;
        float finishTime = -1f;

        public static BloodFissure Spawn(Vector3 start, Vector3 direction, float length, float width)
        {
            var go = new GameObject("Blood Fissure");
            go.transform.position = new Vector3(start.x, FloorTop, start.z);
            go.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            var fissure = go.AddComponent<BloodFissure>();
            fissure.Build(length, width);
            Sfx.Play(Sfx.Fissure, 0.4f, 0.95f, 1.05f); // the ground tearing open as the charge begins
            return fissure;
        }

        void Build(float length, float width)
        {
            darkColor = new Color(0.1f, 0f, 0f, 0.95f);
            glowColor = new Color(1f, 0.12f, 0.04f, 0.95f);
            darkMaterial = VisualUtil.UnlitMaterial(darkColor);
            glowMaterial = VisualUtil.UnlitMaterial(glowColor);

            var rng = new System.Random(Mathf.RoundToInt(transform.position.x * 31f + transform.position.z * 17f));
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            // Main gouge: overlapping slabs, wandering a little off the line, with a glowing seam inside each.
            for (float d = 0f; d < length; d += 0.9f)
            {
                float lateral = Rand(-0.15f, 0.15f);
                float yaw = Rand(-9f, 9f);
                float w = width * Rand(0.7f, 1.1f);
                AddSlab(d + 0.5f, lateral, yaw, w, 1.3f, 0f, darkMaterial, d);
                AddSlab(d + 0.5f, lateral, yaw, w * 0.25f, 1.25f, 0.005f, glowMaterial, d);
            }

            // Cracks branching off to both sides.
            for (float d = 0.6f; d < length; d += Rand(0.8f, 1.5f))
            {
                float side = rng.Next(2) == 0 ? -1f : 1f;
                float angle = Rand(30f, 65f) * side;
                float len = Rand(0.9f, 1.8f) * (width * 0.6f);
                // Slab centred along the branch, starting at the main line.
                float rad = angle * Mathf.Deg2Rad;
                float lateral = Mathf.Sin(rad) * len * 0.5f;
                float along = d + Mathf.Cos(rad) * len * 0.5f;
                AddSlab(along, lateral, angle, 0.14f, len, 0.01f, glowMaterial, d);
                AddSlab(along, lateral, angle, 0.38f, len * 1.05f, 0f, darkMaterial, d);
            }
        }

        // `along` / `lateral` are local to the fissure line; `yaw` turns the slab about the vertical.
        void AddSlab(float along, float lateral, float yaw, float width, float length, float lift, Material material, float revealAt)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(slab.GetComponent<Collider>());
            var renderer = slab.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            slab.transform.SetParent(transform, false);
            slab.transform.localPosition = new Vector3(lateral, lift, along);
            slab.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            slab.transform.localScale = new Vector3(width, 0.02f, length);
            slab.SetActive(false);

            pieces.Add(new Piece { go = slab, distance = revealAt });
        }

        // Opens every piece whose start is within `distance` of the origin.
        public void Reveal(float distance)
        {
            foreach (Piece p in pieces)
                if (!p.go.activeSelf && p.distance <= distance) p.go.SetActive(true);
        }

        public void Finish()
        {
            finishTime = Time.time;
        }

        void Update()
        {
            if (finishTime < 0f) return;

            float age = Time.time - finishTime;
            if (age >= Linger + FadeTime)
            {
                Destroy(darkMaterial);
                Destroy(glowMaterial);
                Destroy(gameObject);
                return;
            }

            // The glow dies down first, then the whole crack fades out.
            float fade = Mathf.Clamp01((Linger + FadeTime - age) / FadeTime);
            float glowLevel = Mathf.Clamp01((Linger + FadeTime - age) / (Linger + FadeTime));
            darkMaterial.color = new Color(darkColor.r, darkColor.g, darkColor.b, darkColor.a * fade);
            glowMaterial.color = new Color(glowColor.r, glowColor.g * glowLevel, glowColor.b, glowColor.a * Mathf.Min(fade, glowLevel * 2f));
        }
    }
}
