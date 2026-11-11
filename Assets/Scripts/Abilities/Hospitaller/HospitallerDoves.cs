using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // The white doves that circle the Hospitaller's head, each trailing a little paper scroll. Built from primitives
    // on every machine. The flock's centre trails behind her with some lag, so when she walks the birds drift after
    // her and catch up, and each bird bobs and wheels at its own pace. Hidden while she is dead.
    //
    // The flock doubles as Dove Strike's charge display: only as many birds circle as there are charges ready. Dove
    // Strike borrows a circling bird (Take) and flies it at the target itself, then hands it back (Return); the bird
    // stays hidden until its charge comes back, then pops back into the circle.
    public class HospitallerDoves : MonoBehaviour
    {
        public const int Count = 3;
        const float FollowSharpness = 4f;
        const float PopTime = 0.25f;
        const float BirdScale = 0.85f;

        class Bird
        {
            public Transform Root;
            public Transform WingL, WingR;
            public LineRenderer Scroll;
            public float Angle;
            public float Speed;
            public float Radius;
            public float Height;
            public float Phase;
            public bool Busy;   // being flown by Dove Strike
            public bool Shown;  // circling (a charge is ready for it)
            public float ShownAt;
            public readonly List<Renderer> WhiteParts = new List<Renderer>(); // turned gold for the EX dove
        }

        readonly List<Bird> birds = new List<Bird>();
        readonly List<Material> materials = new List<Material>();
        Transform flock;
        Vector3 centre;
        Health health;
        DoveStrike strike;
        Material whiteMaterial;
        Material goldMaterial;

        public static HospitallerDoves Ensure(GameObject character)
        {
            var doves = character.GetComponent<HospitallerDoves>();
            if (doves == null) doves = character.AddComponent<HospitallerDoves>();
            return doves;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            strike = GetComponent<DoveStrike>();
            // Not parented to the character, so the flock can lag behind her as she moves.
            flock = new GameObject("Hospitaller Doves").transform;
            centre = transform.position;

            Material white = Mat(new Color(0.95f, 0.95f, 0.93f));
            Material grey = Mat(new Color(0.7f, 0.72f, 0.75f));
            Material beak = Mat(new Color(0.95f, 0.55f, 0.2f));
            Material eye = Mat(new Color(0.05f, 0.05f, 0.05f));
            Material paper = Mat(new Color(0.93f, 0.87f, 0.72f));
            whiteMaterial = white;
            goldMaterial = Mat(new Color(1f, 0.78f, 0.25f));

            for (int i = 0; i < Count; i++)
            {
                var b = new Bird
                {
                    Angle = i * 360f / Count,
                    Speed = 55f + 12f * i,
                    Radius = 0.9f + 0.25f * (i % 2),
                    Height = 2.05f + 0.2f * i,
                    Phase = i * 1.7f,
                    Shown = true
                };
                b.Root = new GameObject("Dove").transform;
                b.Root.SetParent(flock, false);
                b.Root.localScale = Vector3.one * BirdScale;

                Part("Body", PrimitiveType.Sphere, white, b.Root, new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.15f, 0.32f));
                Part("Head", PrimitiveType.Sphere, white, b.Root, new Vector3(0f, 0.07f, 0.17f), new Vector3(0.11f, 0.11f, 0.12f));
                Part("Beak", PrimitiveType.Cube, beak, b.Root, new Vector3(0f, 0.06f, 0.25f), new Vector3(0.025f, 0.025f, 0.06f));
                for (int s = -1; s <= 1; s += 2)
                    Part("Eye", PrimitiveType.Sphere, eye, b.Root, new Vector3(0.045f * s, 0.09f, 0.2f), Vector3.one * 0.022f);
                Transform tail = Part("Tail", PrimitiveType.Cube, grey, b.Root, new Vector3(0f, 0.01f, -0.2f), new Vector3(0.12f, 0.015f, 0.14f));
                tail.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                b.WingL = Wing(b.Root, white, grey, 1f);
                b.WingR = Wing(b.Root, white, grey, -1f);

                // A scroll held in the feet, dangling below and swaying.
                var scrollGo = new GameObject("Scroll");
                scrollGo.transform.SetParent(b.Root, false);
                b.Scroll = scrollGo.AddComponent<LineRenderer>();
                b.Scroll.useWorldSpace = true;
                b.Scroll.positionCount = 6;
                b.Scroll.startWidth = 0.09f;
                b.Scroll.endWidth = 0.07f;
                b.Scroll.sharedMaterial = paper;
                b.Scroll.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b.Scroll.alignment = LineAlignment.View;

                foreach (Renderer r in b.Root.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial == white) b.WhiteParts.Add(r);
                birds.Add(b);
            }
        }

        // Lends a circling bird to Dove Strike, which moves it from now on. Returns -1 (and null) if none is circling.
        public Transform Take(out int id)
        {
            for (int i = 0; i < birds.Count; i++)
            {
                Bird b = birds[i];
                if (b.Busy || !b.Shown || !b.Root.gameObject.activeSelf) continue;
                b.Busy = true;
                id = i;
                return b.Root;
            }

            id = -1;
            return null;
        }

        // Turns a borrowed bird gold (Dove Strike's EX) or back to white.
        public void SetGolden(int id, bool golden)
        {
            if (id < 0 || id >= birds.Count) return;
            foreach (Renderer r in birds[id].WhiteParts)
                if (r != null) r.sharedMaterial = golden ? goldMaterial : whiteMaterial;
        }

        // The bird is back from its strike: it hides until its charge is ready again.
        public void Return(int id)
        {
            if (id < 0 || id >= birds.Count) return;
            SetGolden(id, false);
            Bird b = birds[id];
            b.Busy = false;
            b.Shown = false;
            b.Root.gameObject.SetActive(false);
        }

        Transform Wing(Transform parent, Material white, Material grey, float side)
        {
            var pivot = new GameObject(side > 0 ? "Wing L" : "Wing R").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0.06f * side, 0.05f, 0.03f);
            Part("Blade", PrimitiveType.Cube, white, pivot, new Vector3(0.2f * side, 0f, 0f), new Vector3(0.38f, 0.02f, 0.18f));
            Part("Tip", PrimitiveType.Cube, grey, pivot, new Vector3(0.4f * side, 0f, -0.03f), new Vector3(0.12f, 0.018f, 0.13f));
            return pivot;
        }

        Transform Part(string name, PrimitiveType type, Material material, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject p = GameObject.CreatePrimitive(type);
            p.name = name;
            Destroy(p.GetComponent<Collider>());
            Renderer r = p.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = position;
            p.transform.localScale = scale;
            return p.transform;
        }

        Material Mat(Color color)
        {
            Material m = VisualUtil.UnlitMaterial(color);
            materials.Add(m);
            return m;
        }

        // Shows as many idle birds as there are charges ready (all of them without Dove Strike).
        void UpdateVisibility()
        {
            // A bird in flight has already spent its charge, so the ready count is exactly how many idle birds circle.
            int want = strike != null ? strike.ChargesReady : Count;
            int shown = 0;
            foreach (Bird b in birds)
            {
                if (b.Busy) continue;
                bool show = shown < want;
                if (show) shown++;
                if (show && !b.Shown) b.ShownAt = Time.time;
                b.Shown = show;
                if (b.Root.gameObject.activeSelf != show) b.Root.gameObject.SetActive(show);
            }
        }

        void LateUpdate()
        {
            if (flock == null) return;
            bool alive = health == null || !health.IsDead;
            if (flock.gameObject.activeSelf != alive)
            {
                flock.gameObject.SetActive(alive);
                centre = transform.position; // reappear around her, not where she died
            }
            if (!alive) return;

            UpdateVisibility();
            centre = Vector3.Lerp(centre, transform.position, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
            float t = Time.time;

            foreach (Bird b in birds)
            {
                if (!b.Root.gameObject.activeSelf) continue;

                if (!b.Busy)
                {
                    b.Angle += b.Speed * Time.deltaTime;
                    float a = b.Angle * Mathf.Deg2Rad;
                    float r = b.Radius + 0.12f * Mathf.Sin(t * 0.9f + b.Phase);
                    Vector3 offset = new Vector3(Mathf.Cos(a) * r, b.Height + 0.15f * Mathf.Sin(t * 1.6f + b.Phase), Mathf.Sin(a) * r);
                    // Facing along the circle (counter-clockwise when seen from above), banked into the turn.
                    Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    b.Root.SetPositionAndRotation(centre + offset, Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, 18f));
                    // A bird whose charge just came back pops in.
                    float pop = Mathf.Clamp01((t - b.ShownAt) / PopTime);
                    b.Root.localScale = Vector3.one * (BirdScale * Mathf.SmoothStep(0f, 1f, pop));
                }

                // Slow, strong wingbeats with a glide every few seconds; a diving bird beats hard all the way.
                float glide = b.Busy ? 1f : Mathf.Clamp01(Mathf.Sin(t * 0.7f + b.Phase) * 3f);
                float flap = Mathf.Sin((t + b.Phase) * (b.Busy ? 22f : 13f)) * 45f * glide;
                b.WingL.localRotation = Quaternion.Euler(0f, 0f, 5f + flap);
                b.WingR.localRotation = Quaternion.Euler(0f, 0f, -5f - flap);

                // The scroll trails down and back from the feet, rippling.
                Vector3 pos = b.Root.position;
                Vector3 fwd = b.Root.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(fwd, Vector3.up);
                for (int i = 0; i < b.Scroll.positionCount; i++)
                {
                    float k = i / (float)(b.Scroll.positionCount - 1);
                    Vector3 sway = side * (0.05f * Mathf.Sin(t * 4f + b.Phase + k * 4f));
                    b.Scroll.SetPosition(i, pos + Vector3.down * (0.08f + 0.42f * k) - fwd * (0.18f * k) + sway * k);
                }
            }
        }

        void OnDestroy()
        {
            if (flock != null) Destroy(flock.gameObject);
            foreach (Material m in materials)
                if (m != null) Destroy(m);
        }
    }
}
