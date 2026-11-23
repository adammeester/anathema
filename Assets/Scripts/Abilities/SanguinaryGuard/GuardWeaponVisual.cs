using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // What the Sanguinary Guard holds: his own power sword (part of the model, on the "Chainsword" bone), or the
    // Encarmine Axe or Glaive Encarmine (Armament upgrades), built here from primitives in his right hand. Also hides
    // whichever weapon he holds while Blade Hurl has it in the air, and points the Vitae blade glow at the right blade.
    // Driven by the replicated upgrade mask, so every machine shows the same weapon. Added by UpgradeState.
    [DefaultExecutionOrder(150)] // after the poses (50-100), before BloodBladeEffect (200) reads the blade
    public class GuardWeaponVisual : MonoBehaviour
    {
        static readonly Color Steel = new Color(0.82f, 0.8f, 0.76f, 1f);
        static readonly Color Gold = new Color(0.9f, 0.66f, 0.25f, 1f);
        static readonly Color Haft = new Color(0.25f, 0.06f, 0.06f, 1f);
        static readonly Color Glow = new Color(1f, 0.12f, 0.08f, 0.4f);
        static readonly Color PowerSteel = new Color(0.8f, 0.88f, 0.96f, 1f);   // the axe's pale power-field blade
        static readonly Color PanelSteel = new Color(0.3f, 0.4f, 0.55f, 1f);
        static readonly Color RubyRed = new Color(0.75f, 0.03f, 0.06f, 1f);

        Transform swordBone, swordTip;
        Vector3 swordRestScale = Vector3.one;
        Vector3 boneAxis = Vector3.up;   // along the blade, in the sword bone's space
        CrimsonRush rush;
        BloodBladeEffect glow;

        GuardWeapon built = GuardWeapon.PowerSword;
        bool everBuilt;
        Transform custom;                // follows the sword bone's place in the hand
        Transform glowFrom, glowTo;
        WeaponMaterials materials;

        public GuardWeapon Current => built;

        // The cutting edge of whatever he holds, grip end to point (Red Thirst's blade flares ride it), and whether it is
        // in his hand at all (Blade Hurl).
        public Transform BladeFrom => built == GuardWeapon.PowerSword ? swordBone : glowFrom;
        public Transform BladeTo => built == GuardWeapon.PowerSword ? swordTip : glowTo;
        public bool InHand => rush == null || !rush.Disarmed;

        void Awake()
        {
            rush = GetComponent<CrimsonRush>();
            Transform model = transform.Find("Model");
            if (model == null) return;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (swordBone == null && t.name == "Chainsword") swordBone = t;   // depth-first: the real skeleton
                if (swordTip == null && t.name == "Chainsword_Tip") swordTip = t;
            }
            if (swordBone == null) return;
            swordRestScale = swordBone.localScale;
            Vector3 along = swordTip != null ? swordTip.position - swordBone.position : Vector3.up;
            boneAxis = swordBone.InverseTransformDirection(along.normalized).normalized;
            materials = WeaponMaterials.Create();
        }

        void LateUpdate()
        {
            if (swordBone == null) return;

            GuardWeapon want = GuardUpgrade.WeaponOf(gameObject);
            if (!everBuilt || want != built) Build(want);
            else if (glow == null && built != GuardWeapon.PowerSword)
            {
                // The Vitae glow is made in Chainsword.Start, which can come after the weapon was first built.
                glow = GetComponentInChildren<BloodBladeEffect>();
                if (glow != null) glow.Retarget(glowFrom, glowTo);
            }

            bool inHand = rush == null || !rush.Disarmed;
            swordBone.localScale = built == GuardWeapon.PowerSword && inHand ? swordRestScale : swordRestScale * 0.001f;
            if (custom != null)
            {
                custom.gameObject.SetActive(inHand);
                custom.localPosition = swordBone.localPosition;
                custom.localRotation = swordBone.localRotation;
                if (inHand) KeepPointOffGround();
            }
        }

        // The long weapons reach past the ground where the sword's pose holds them (the glaive's point trails into the
        // floor at rest). If the point would sink below GroundClearance, tilt the weapon up about the grip just enough.
        const float GroundClearance = 0.2f;

        void KeepPointOffGround()
        {
            if (glowTo == null) return;
            Vector3 grip = custom.position;
            Vector3 along = glowTo.position - grip;
            float length = along.magnitude;
            float floor = transform.position.y + GroundClearance;
            if (length < 0.01f || glowTo.position.y >= floor) return;
            Vector3 axis = Vector3.Cross(along, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f) return;
            float now = Mathf.Asin(Mathf.Clamp(along.y / length, -1f, 1f)) * Mathf.Rad2Deg;
            float want = Mathf.Asin(Mathf.Clamp((floor - grip.y) / length, -1f, 1f)) * Mathf.Rad2Deg;
            custom.rotation = Quaternion.AngleAxis(want - now, axis.normalized) * custom.rotation;
        }

        void Build(GuardWeapon weapon)
        {
            everBuilt = true;
            built = weapon;
            if (custom != null) Destroy(custom.gameObject);
            custom = null;
            glowFrom = glowTo = null;

            if (glow == null) glow = GetComponentInChildren<BloodBladeEffect>();
            if (weapon == GuardWeapon.PowerSword)
            {
                if (glow != null) glow.Retarget(swordBone, swordTip);
                return;
            }

            custom = new GameObject("Guard Weapon").transform;
            custom.SetParent(swordBone.parent, false);
            custom.localPosition = swordBone.localPosition;
            custom.localRotation = swordBone.localRotation;
            // The holder points +Y along the blade and works in metres whatever the model's import scale.
            var holder = new GameObject("Holder").transform;
            holder.SetParent(custom, false);
            holder.localRotation = Quaternion.FromToRotation(Vector3.up, boneAxis);
            float s = Mathf.Max(0.0001f, custom.lossyScale.x);
            holder.localScale = Vector3.one / s;

            BuildShape(weapon, holder, materials, out glowFrom, out glowTo);
            if (glow != null) glow.Retarget(glowFrom, glowTo);
        }

        void OnDestroy()
        {
            materials?.Destroy();
        }

        // ---- Shapes (also used for Blade Hurl's flying copy) ----------------------------------------------------------

        public sealed class WeaponMaterials
        {
            public Material Steel, Gold, Haft, Glow, Panel, Ruby, PowerSteel, Bone, Socket, Black;

            public static WeaponMaterials Create() => new WeaponMaterials
            {
                Steel = VisualUtil.UnlitMaterial(GuardWeaponVisual.Steel, 3001),
                Gold = VisualUtil.UnlitMaterial(GuardWeaponVisual.Gold, 3001),
                Haft = VisualUtil.UnlitMaterial(GuardWeaponVisual.Haft, 3001),
                Glow = VisualUtil.UnlitMaterial(GuardWeaponVisual.Glow, 3000),
                Panel = VisualUtil.UnlitMaterial(GuardWeaponVisual.PanelSteel, 3002),
                Ruby = VisualUtil.UnlitMaterial(GuardWeaponVisual.RubyRed, 3002),
                PowerSteel = VisualUtil.UnlitMaterial(GuardWeaponVisual.PowerSteel, 3001),
                Bone = VisualUtil.UnlitMaterial(new Color(0.88f, 0.82f, 0.68f, 1f), 3003),
                Socket = VisualUtil.UnlitMaterial(new Color(0.08f, 0.04f, 0.03f, 1f), 3004),
                Black = VisualUtil.UnlitMaterial(new Color(0.05f, 0.05f, 0.06f, 1f), 3001),
            };

            public void Destroy()
            {
                Object.Destroy(Steel);
                Object.Destroy(Gold);
                Object.Destroy(Haft);
                Object.Destroy(Glow);
                Object.Destroy(Panel);
                Object.Destroy(Ruby);
                Object.Destroy(PowerSteel);
                Object.Destroy(Bone);
                Object.Destroy(Socket);
                Object.Destroy(Black);
            }
        }

        // Builds `weapon` under `holder` with its grip at the origin and the blade along +Y, in metres. `from`/`to` mark
        // the cutting edge for the Vitae glow. The power sword is only built here for Blade Hurl's flying copy.
        public static void BuildShape(GuardWeapon weapon, Transform holder, WeaponMaterials m, out Transform from, out Transform to)
        {
            switch (weapon)
            {
                case GuardWeapon.EncarmineAxe:
                    BuildAxe(holder, m);
                    from = Marker(holder, "Glow From", new Vector3(0.5f, AxeHead - 0.3f, 0f));
                    to = Marker(holder, "Glow To", new Vector3(0.49f, AxeHead + 0.37f, 0f));
                    return;

                case GuardWeapon.GlaiveEncarmine:
                    // A tall haft with a gold guard and a long leaf blade.
                    Part(PrimitiveType.Cylinder, m.Black, holder, new Vector3(0f, 0.45f, 0f), new Vector3(0.06f, 1.05f, 0.06f));
                    Part(PrimitiveType.Cylinder, m.Gold, holder, new Vector3(0f, -0.6f, 0f), new Vector3(0.09f, 0.06f, 0.09f));
                    Part(PrimitiveType.Cube, m.Gold, holder, new Vector3(0f, 1.52f, 0f), new Vector3(0.36f, 0.06f, 0.08f));
                    Part(PrimitiveType.Cube, m.Steel, holder, new Vector3(0f, 1.95f, 0f), new Vector3(0.15f, 0.8f, 0.035f));
                    Part(PrimitiveType.Cube, m.Steel, holder, new Vector3(0f, 2.38f, 0f), new Vector3(0.09f, 0.12f, 0.035f), Quaternion.Euler(0f, 0f, 45f));
                    Part(PrimitiveType.Cube, m.Glow, holder, new Vector3(0f, 1.97f, 0f), new Vector3(0.3f, 0.92f, 0.08f));
                    from = Marker(holder, "Glow From", new Vector3(0f, 1.3f, 0f));
                    to = Marker(holder, "Glow To", new Vector3(0f, 2.42f, 0f));
                    return;

                default:
                    // The power sword: gold grip and guard, a long steel blade.
                    Part(PrimitiveType.Cylinder, m.Gold, holder, new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.14f, 0.06f));
                    Part(PrimitiveType.Cube, m.Gold, holder, new Vector3(0f, 0.17f, 0f), new Vector3(0.34f, 0.06f, 0.07f));
                    Part(PrimitiveType.Cube, m.Steel, holder, new Vector3(0f, 0.72f, 0f), new Vector3(0.1f, 1.05f, 0.03f));
                    Part(PrimitiveType.Cube, m.Glow, holder, new Vector3(0f, 0.74f, 0f), new Vector3(0.22f, 1.15f, 0.08f));
                    from = Marker(holder, "Glow From", new Vector3(0f, 0.2f, 0f));
                    to = Marker(holder, "Glow To", new Vector3(0f, 1.25f, 0f));
                    return;
            }
        }

        // ---- The Encarmine Axe ------------------------------------------------------------------------------------------
        // After the Sanguinary Guard's Encarmine axes: a gold haft with a crimson grip and a ruby-set pommel, and a
        // double head - two blades of pale power-steel, one each side, each cut like a spread wing: a straight upper edge,
        // a broad cutting edge, and a lower edge scalloped into feather tips. Darker feathers fan out over the base of
        // each blade from a bone skull at the centre, with gold spikes above and below. Outlines are (x out along the
        // blade, y up the haft), metres, for the blade on the +x side; the other is its mirror.

        public const float AxeHead = 1.05f;   // height of the head's centre up the haft

        public static readonly Vector2[] AxeBlade =
        {
            new Vector2(0.06f, 0.12f), new Vector2(0.2f, 0.2f), new Vector2(0.36f, 0.3f), new Vector2(0.48f, 0.37f),
            new Vector2(0.5f, 0.2f), new Vector2(0.51f, 0.02f), new Vector2(0.49f, -0.14f), new Vector2(0.44f, -0.28f),
            new Vector2(0.38f, -0.2f), new Vector2(0.34f, -0.3f), new Vector2(0.28f, -0.19f), new Vector2(0.23f, -0.26f),
            new Vector2(0.18f, -0.15f), new Vector2(0.13f, -0.2f), new Vector2(0.06f, -0.1f),
        };

        // One feather of the wing over a blade's base (pointing along +x before it is turned).
        public static readonly Vector2[] AxeFeather =
        {
            new Vector2(0f, 0f), new Vector2(0.05f, 0.03f), new Vector2(0.22f, 0.03f), new Vector2(0.3f, 0f),
            new Vector2(0.22f, -0.025f), new Vector2(0.05f, -0.025f),
        };

        // The spikes above and below the head.
        public static readonly Vector2[] AxeSpike =
        {
            new Vector2(0f, 0f), new Vector2(0.045f, 0.07f), new Vector2(0f, 0.3f), new Vector2(-0.045f, 0.07f),
        };

        // The feathers over each blade's base: (angle from the haft outward, length scale). Fanning from up-and-out to down.
        public static readonly Vector2[] AxeFeatherFan =
        {
            new Vector2(28f, 0.9f), new Vector2(10f, 1.05f), new Vector2(-10f, 1.05f), new Vector2(-30f, 0.95f), new Vector2(-50f, 0.8f),
        };

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        static void BuildAxe(Transform h, WeaponMaterials m)
        {
            // Haft: gold, a crimson leather grip where he holds it, gold bands, a ruby-set pommel.
            Part(PrimitiveType.Cylinder, m.Gold, h, new Vector3(0f, 0.5f, 0f), new Vector3(0.06f, 0.75f, 0.06f));
            Part(PrimitiveType.Cylinder, m.Haft, h, new Vector3(0f, 0.12f, 0f), new Vector3(0.078f, 0.2f, 0.078f));
            foreach (float y in new[] { -0.1f, 0.34f, 0.8f, 1.3f })
                Part(PrimitiveType.Cylinder, m.Gold, h, new Vector3(0f, y, 0f), new Vector3(0.095f, 0.022f, 0.095f));
            Part(PrimitiveType.Sphere, m.Gold, h, new Vector3(0f, -0.3f, 0f), Vector3.one * 0.12f);
            Part(PrimitiveType.Sphere, m.Ruby, h, new Vector3(0f, -0.3f, 0.05f), Vector3.one * 0.05f);

            Vector3 head = new Vector3(0f, AxeHead, 0f);
            Mesh blade = Shape("axe blade", AxeBlade, 0.035f);
            Mesh sheen = Shape("axe sheen", Scaled(AxeBlade, new Vector2(0.06f, 0f), 1.1f), 0.09f);
            Mesh feather = Shape("axe feather", AxeFeather, 0.05f);
            foreach (float side in new[] { 1f, -1f })
            {
                // Mirrored by a negative x scale (the materials draw both faces).
                var mirror = new Vector3(side, 1f, 1f);
                MeshPart(sheen, m.Glow, h, head, null, mirror);
                MeshPart(blade, m.PowerSteel, h, head, null, mirror);
                foreach (Vector2 f in AxeFeatherFan)
                    MeshPart(feather, m.Panel, h, head + new Vector3(side * 0.05f, 0f, 0f),
                        Quaternion.Euler(0f, 0f, side > 0f ? f.x : 180f - f.x), new Vector3(f.y, 1f, 1f));
            }

            // The centre: a gold socket wrapped round the haft and a bone skull on each face, spikes above and below.
            Part(PrimitiveType.Cube, m.Gold, h, new Vector3(0f, AxeHead, 0f), new Vector3(0.14f, 0.3f, 0.1f));
            foreach (float face in new[] { 1f, -1f })
            {
                Part(PrimitiveType.Sphere, m.Bone, h, new Vector3(0f, AxeHead + 0.02f, face * 0.05f), new Vector3(0.12f, 0.13f, 0.07f));
                Part(PrimitiveType.Cube, m.Bone, h, new Vector3(0f, AxeHead - 0.05f, face * 0.05f), new Vector3(0.07f, 0.05f, 0.05f));
                foreach (float eye in new[] { -0.025f, 0.025f })
                    Part(PrimitiveType.Sphere, m.Socket, h, new Vector3(eye, AxeHead + 0.02f, face * 0.085f), new Vector3(0.03f, 0.03f, 0.01f));
            }
            Mesh spike = Shape("axe spike", AxeSpike, 0.04f);
            MeshPart(spike, m.Gold, h, new Vector3(0f, 1.31f, 0f));
            MeshPart(spike, m.Gold, h, new Vector3(0f, AxeHead - 0.16f, 0f), Quaternion.Euler(0f, 0f, 180f), Vector3.one * 0.7f);
        }

        static Vector2[] Scaled(Vector2[] outline, Vector2 about, float k)
        {
            var result = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++) result[i] = about + (outline[i] - about) * k;
            return result;
        }

        // A flat plate cut to `outline` (any simple polygon, either winding), `thickness` deep, centred on z = 0.
        // Cached by name.
        static Mesh Shape(string name, Vector2[] outline, float thickness)
        {
            if (meshes.TryGetValue(name, out Mesh cached) && cached != null) return cached;
            int n = outline.Length;
            float z = thickness * 0.5f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            List<int> face = Triangulate(outline);

            // Front and back faces.
            for (int side = 0; side < 2; side++)
            {
                float fz = side == 0 ? -z : z;
                int c = verts.Count;
                for (int i = 0; i < n; i++) verts.Add(new Vector3(outline[i].x, outline[i].y, fz));
                for (int t = 0; t < face.Count; t += 3)
                {
                    tris.Add(c + face[t]);
                    tris.Add(c + (side == 0 ? face[t + 1] : face[t + 2]));
                    tris.Add(c + (side == 0 ? face[t + 2] : face[t + 1]));
                }
            }
            // The rim.
            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % n];
                int s = verts.Count;
                verts.Add(new Vector3(p.x, p.y, -z));
                verts.Add(new Vector3(q.x, q.y, -z));
                verts.Add(new Vector3(q.x, q.y, z));
                verts.Add(new Vector3(p.x, p.y, z));
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes[name] = mesh;
            return mesh;
        }

        // Ear clipping: triangles (index triples into `poly`) covering a simple polygon, so concave outlines such as the
        // feathered edge come out right.
        static List<int> Triangulate(Vector2[] poly)
        {
            var result = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < poly.Length; i++) idx.Add(i);
            float area = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f) idx.Reverse();   // work counter-clockwise

            int guard = 0;
            while (idx.Count > 3 && guard++ < 1000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0f) continue;   // reflex corner
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (InTriangle(poly[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        static void MeshPart(Mesh mesh, Material material, Transform parent, Vector3 at, Quaternion? rotation = null, Vector3? scale = null)
        {
            var go = new GameObject(mesh.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;
        }

        static Transform Marker(Transform parent, string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        static void Part(PrimitiveType type, Material material, Transform parent, Vector3 at, Vector3 scale, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = "Part";
            Object.DestroyImmediate(go.GetComponent<Collider>()); // never something a projectile could hit
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
        }
    }
}
