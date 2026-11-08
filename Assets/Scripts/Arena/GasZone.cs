using System;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Arena
{
    // Battle-royale style closing gas. The safe circle is centred on this object's position and shrinks in phases:
    // wait -> shrink -> wait -> shrink ... Anything with a Health component outside the circle takes tick damage.
    public class GasZone : MonoBehaviour
    {
        [Serializable]
        public struct Phase
        {
            public float radius;    // safe radius at the end of this phase's shrink
            public float holdTime;  // seconds the previous circle stays before this shrink starts
            public float shrinkTime;
            public float dps;       // damage per second while standing in gas during/after this phase
        }

        [SerializeField] float startRadius = 28f;
        [SerializeField] float tickInterval = 0.5f;
        [SerializeField] Phase[] phases =
        {
            new Phase { radius = 21f, holdTime = 15f, shrinkTime = 15f, dps = 4f },
            new Phase { radius = 14f, holdTime = 12f, shrinkTime = 12f, dps = 8f },
            new Phase { radius = 8f,  holdTime = 10f, shrinkTime = 10f, dps = 14f },
            new Phase { radius = 3f,  holdTime = 8f,  shrinkTime = 8f,  dps = 24f },
        };

        public float CurrentRadius { get; private set; }

        int phaseIndex;
        float phaseTimer;
        float fromRadius;
        float currentDps;
        float tickTimer;

        Transform wall;
        Transform floor;
        Material gasMaterial;

        const float VisualHeight = 7f;
        const float FloorOuterScale = 40f;

        void Awake()
        {
            BuildVisuals();
            Restart();
        }

        public void Restart()
        {
            phaseIndex = 0;
            phaseTimer = 0f;
            CurrentRadius = fromRadius = startRadius;
            currentDps = 0f;
            tickTimer = 0f;
            ApplyVisuals();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.T) && !Anathema.Net.NetGame.Online) Restart();

            UpdateRadius();
            ApplyVisuals();

            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                DamageOutside(currentDps * tickInterval);
            }
        }

        void UpdateRadius()
        {
            if (phaseIndex >= phases.Length) return;

            Phase phase = phases[phaseIndex];
            phaseTimer += Time.deltaTime;

            if (phaseTimer < phase.holdTime)
            {
                CurrentRadius = fromRadius;
                return;
            }

            float t = phase.shrinkTime > 0f ? Mathf.Clamp01((phaseTimer - phase.holdTime) / phase.shrinkTime) : 1f;
            CurrentRadius = Mathf.Lerp(fromRadius, phase.radius, t);
            currentDps = phase.dps;

            if (t >= 1f)
            {
                fromRadius = phase.radius;
                phaseIndex++;
                phaseTimer = 0f;
            }
        }

        void DamageOutside(float amount)
        {
            if (amount <= 0f) return;

            Vector3 center = transform.position;
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.IsDead) continue;
                Vector3 offset = h.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > CurrentRadius) h.TakeDamage(amount, gameObject, true);
            }
        }

        // --- Visuals ---------------------------------------------------------------------------------------------

        void BuildVisuals()
        {
            gasMaterial = VisualUtil.UnlitMaterial(new Color(0.45f, 0.95f, 0.25f, 0.35f), 3000);

            wall = MakeMeshObject("Gas Wall", BuildTube(VisualHeight, 64)).transform;
            floor = MakeMeshObject("Gas Floor", BuildAnnulus(1f, FloorOuterScale, 64)).transform;
        }

        GameObject MakeMeshObject(string objectName, Mesh mesh)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = gasMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        void ApplyVisuals()
        {
            float r = Mathf.Max(0.01f, CurrentRadius);
            wall.localScale = new Vector3(r, 1f, r);
            floor.localScale = new Vector3(r, 1f, r);
            floor.localPosition = new Vector3(0f, 0.08f, 0f);

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f);
            gasMaterial.color = new Color(0.45f, 0.95f, 0.25f, Mathf.Lerp(0.28f, 0.42f, pulse));
        }

        // Unit-radius open cylinder, double sided so it reads from inside and out.
        static Mesh BuildTube(float height, int segments)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 12];
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = p;
                verts[i * 2 + 1] = p + Vector3.up * height;
            }
            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris[ti++] = b; tris[ti++] = b + 1; tris[ti++] = b + 2;
                tris[ti++] = b + 1; tris[ti++] = b + 3; tris[ti++] = b + 2;
                tris[ti++] = b; tris[ti++] = b + 2; tris[ti++] = b + 1;
                tris[ti++] = b + 1; tris[ti++] = b + 2; tris[ti++] = b + 3;
            }
            var mesh = new Mesh { name = "GasTube", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
        }

        // Flat ring from innerRadius to outerRadius (flat, double sided). Scaled uniformly in XZ by the zone radius.
        static Mesh BuildAnnulus(float innerRadius, float outerRadius, int segments)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 12];
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * innerRadius;
                verts[i * 2 + 1] = dir * outerRadius;
            }
            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris[ti++] = b; tris[ti++] = b + 1; tris[ti++] = b + 2;
                tris[ti++] = b + 1; tris[ti++] = b + 3; tris[ti++] = b + 2;
                tris[ti++] = b; tris[ti++] = b + 2; tris[ti++] = b + 1;
                tris[ti++] = b + 1; tris[ti++] = b + 2; tris[ti++] = b + 3;
            }
            var mesh = new Mesh { name = "GasAnnulus", vertices = verts, triangles = tris };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
        }

        // --- Minimal HUD -----------------------------------------------------------------------------------------

        void OnGUI()
        {
            string text;
            if (phaseIndex >= phases.Length) text = "GAS: final circle";
            else
            {
                Phase p = phases[phaseIndex];
                if (phaseTimer < p.holdTime) text = $"GAS CLOSING IN {Mathf.CeilToInt(p.holdTime - phaseTimer)}s";
                else text = "GAS CLOSING!";
                text += $"   ({p.dps:0} dmg/s)";
            }

            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(0.6f, 1f, 0.4f);
            GUI.Label(new Rect(0f, 16f, Screen.width, 30f), text, style);
        }
    }
}
