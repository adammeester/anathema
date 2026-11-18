using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // Shared pieces of the Bullgryn's kit: colours, the "is he braced / has he planted his wall" checks every ability
    // uses to lock itself out, the wall-slam test, and a few ground telegraphs.
    public static class BullgrynKit
    {
        public static readonly Color Hazard = new Color(0.95f, 0.65f, 0.1f, 0.9f);
        public static readonly Color Power = new Color(0.3f, 1f, 0.4f, 0.9f);
        public static readonly Color Steel = new Color(0.75f, 0.82f, 0.88f, 0.9f);
        public static readonly Color Ration = new Color(0.55f, 1f, 0.45f, 0.9f);

        // Scenery lower than this (kerbs, rubble) doesn't count as a wall for a slam.
        const float WallMinHeight = 0.6f;

        public static bool IsBracing(GameObject bullgryn)
        {
            return bullgryn.TryGetComponent(out Brace brace) && brace.IsBracing;
        }

        // True while his Living Wall stands: the slabshield is in the ground, so Brace and Shield Bash are unusable.
        public static bool ShieldPlanted(GameObject bullgryn)
        {
            return bullgryn.TryGetComponent(out LivingWall wall) && wall.WallStanding;
        }

        // Most of his abilities can't start while he is braced (he is behind the shield).
        public static bool Busy(GameObject bullgryn) => IsBracing(bullgryn);

        public static List<Health> EnemiesInCone(Health self, Vector3 origin, Vector3 aim, float reach, float arc)
        {
            return Mandrake.MandrakeKit.EnemiesInCone(self, origin, aim, reach, arc);
        }

        public static Vector3 FlatAim(Vector3 aim, Transform fallback)
        {
            aim.y = 0f;
            return aim.sqrMagnitude < 0.0001f ? fallback.forward : aim.normalized;
        }

        // True if solid scenery (not a character, and taller than a kerb) stands within `distance` of `target` along
        // `push`. A Living Wall counts (charging someone into it slams them), except `ignore` (the wall being shoved).
        // Targets that can't be shoved right now don't slam.
        public static bool FindWall(Health target, Vector3 push, float distance, out float wallDistance, out Vector3 impact,
            GameObject ignore = null)
        {
            wallDistance = 0f;
            impact = Vector3.zero;
            if (target == null || target.Structure || Knockback.IsImmune(target.gameObject) || Enraged.IsEnraged(target.gameObject)
                || Unstoppable.Is(target.gameObject))
                return false;

            Vector3 from = target.transform.position;
            float best = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.SphereCastAll(from + Vector3.up, 0.35f, push, distance + 0.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance <= 0f || hit.transform.IsChildOf(target.transform)) continue;
                if (ignore != null && hit.transform.IsChildOf(ignore.transform)) continue;
                if (hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.collider.bounds.max.y - from.y < WallMinHeight) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                impact = hit.point;
            }
            if (float.IsPositiveInfinity(best)) return false;
            wallDistance = best;
            return true;
        }

        // Drags a teammate (Chain Haul on an ally). Allies can't be shoved with a hostile knockback online, so this goes
        // through the supportive AllyHaul status; offline (or on the server) it is a plain knockback.
        public static void HaulAlly(GameObject ally, Vector3 direction, float distance, float seconds, GameObject source)
        {
            if (NetPlayer.TryGetRemote(ally, out NetPlayer remote) && !remote.IsServer)
            {
                remote.SendStatus(NetPlayer.StatusKind.AllyHaul, distance, seconds, direction, source);
                return;
            }
            Knockback.Apply(ally, direction, distance, seconds);
        }

        // ---- Telegraphs -----------------------------------------------------------------------------------------------

        // A flat ring on the ground that fills from the centre over `seconds`, then disappears.
        public static GameObject GroundCircle(Vector3 centre, float radius, float seconds, Color color)
        {
            var go = new GameObject("Bullgryn Circle");
            go.transform.position = centre + Vector3.up * 0.06f;
            go.AddComponent<GroundCircleFx>().Init(radius, seconds, color);
            return go;
        }

        // A flat rectangle on the ground from `from` along `dir` (`length` x `width`), shown for `seconds`.
        public static GameObject GroundLane(Vector3 from, Vector3 dir, float length, float width, float seconds, Color color)
        {
            var go = new GameObject("Bullgryn Lane");
            go.transform.position = from + Vector3.up * 0.06f;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            go.AddComponent<GroundLaneFx>().Init(length, width, seconds, color);
            return go;
        }

        public static LineRenderer NewLine(Transform parent, Color color, float width, int points, bool loop)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.sharedMaterial = VisualUtil.UnlitMaterial(color, 3000);
            return line;
        }

        sealed class GroundCircleFx : MonoBehaviour
        {
            const int Points = 40;
            LineRenderer rim, fill;
            float radius, duration, start;
            Color color;

            public void Init(float r, float seconds, Color c)
            {
                radius = r;
                duration = Mathf.Max(0.05f, seconds);
                color = c;
                start = Time.time;
                rim = NewLine(transform, new Color(c.r, c.g, c.b, 0.85f), 0.07f, Points, true);
                fill = NewLine(transform, new Color(c.r, c.g, c.b, 0.4f), 0.05f, Points, true);
                Update();
            }

            void Update()
            {
                float t = (Time.time - start) / duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector3 c = transform.position;
                for (int i = 0; i < Points; i++)
                {
                    float a = i / (float)Points * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    rim.SetPosition(i, c + d * radius);
                    fill.SetPosition(i, c + d * radius * Mathf.Clamp01(t));
                }
            }

            void OnDestroy()
            {
                if (rim != null) Destroy(rim.sharedMaterial);
                if (fill != null) Destroy(fill.sharedMaterial);
            }
        }

        sealed class GroundLaneFx : MonoBehaviour
        {
            LineRenderer rim;
            float length, width, duration, start;

            public void Init(float l, float w, float seconds, Color c)
            {
                length = l;
                width = w;
                duration = Mathf.Max(0.05f, seconds);
                start = Time.time;
                rim = NewLine(transform, c, 0.08f, 4, true);
                Update();
            }

            void Update()
            {
                if (Time.time - start >= duration)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector3 f = transform.forward, r = transform.right, o = transform.position;
                float h = width * 0.5f;
                rim.SetPosition(0, o - r * h);
                rim.SetPosition(1, o + r * h);
                rim.SetPosition(2, o + r * h + f * length);
                rim.SetPosition(3, o - r * h + f * length);
            }

            void OnDestroy()
            {
                if (rim != null) Destroy(rim.sharedMaterial);
            }
        }
    }
}
