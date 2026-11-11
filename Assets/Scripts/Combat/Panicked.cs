using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: while present, the owner is gripped by panic and runs directly away from whoever caused it
    // (PlayerMovement, MouseAim and DummyMover ask TryGetFleeDirection). A full loss of control: they can't steer, aim
    // (they face where they run), attack or start any ability (Ability.Update checks IsPanicked). Stuns and roots still
    // win over the running: a pinned or stunned character doesn't go anywhere.
    // A solid hit (>= Health.PanicBreakFraction of max health, DoT ticks excluded) snaps them out of it.
    // The flight direction tracks the source's live position, so circling the victim turns them around.
    // Shows a bobbing yellow "!" above the head. Added to the victim at runtime; re-applying extends it.
    public class Panicked : MonoBehaviour
    {
        const float MarkHeight = 2.6f;

        float endTime;
        Transform source;
        Vector3 lastSourcePosition;
        Transform mark;
        Material markMaterial;

        public static Panicked Apply(GameObject target, GameObject source, float duration)
        {
            if (Anathema.Net.NetPlayer.RouteControl(target, Anathema.Net.NetPlayer.StatusKind.Panic, duration, 0f, Vector3.zero, source)) return null;

            if (Enraged.IsEnraged(target)) return null;
            duration *= Health.CrowdControlScaleOf(target);

            var panicked = target.GetComponent<Panicked>();
            if (panicked == null) panicked = target.AddComponent<Panicked>();
            panicked.endTime = Mathf.Max(panicked.endTime, Time.time + duration);
            if (source != null)
            {
                panicked.source = source.transform;
                panicked.lastSourcePosition = source.transform.position;
            }
            return panicked;
        }

        // Damage snaps the victim out of it (see Health.PanicBreakFraction); DoT ticks never call this.
        // Online: draws this on a replica (driven by NetPlayer's published status mask); lasts until the mask clears.
        public static void ShowReplica(GameObject target)
        {
            if (target == null || target.TryGetComponent(out Panicked _)) return;
            target.AddComponent<Panicked>().endTime = float.MaxValue;
        }

        public static void Cancel(GameObject target)
        {
            // Online: only the server decides a hit broke the panic. It ends its own copy and tells the victim's machine.
            if (Anathema.Net.NetPlayer.TryGetRemote(target, out var remote))
            {
                if (!remote.IsServer) return;
                if (IsPanicked(target))
                    remote.ForwardStatusToOwner(Anathema.Net.NetPlayer.StatusKind.PanicBreak, 0f, 0f, Vector3.zero, null);
            }

            if (target.TryGetComponent(out Panicked panicked)) Destroy(panicked);
        }

        public static bool IsPanicked(GameObject target)
        {
            return target.TryGetComponent(out Panicked _);
        }

        // Unit vector pointing straight away from the source on the ground plane. False if the target isn't panicked.
        public static bool TryGetFleeDirection(GameObject target, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!target.TryGetComponent(out Panicked panicked)) return false;

            if (panicked.source != null) panicked.lastSourcePosition = panicked.source.position;
            Vector3 away = target.transform.position - panicked.lastSourcePosition;
            away.y = 0f;
            // Standing right on top of the source: just bolt backwards.
            if (away.sqrMagnitude < 0.01f) away = -target.transform.forward;
            direction = away.normalized;
            return true;
        }

        void Awake()
        {
            markMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.9f, 0.2f));
            PanicSound.Play();

            mark = new GameObject("Panic Mark").transform;
            mark.SetParent(transform, false);
            mark.localPosition = Vector3.up * MarkHeight;

            AddPart(PrimitiveType.Cube, new Vector3(0.14f, 0.38f, 0.14f), new Vector3(0f, 0.15f, 0f));
            AddPart(PrimitiveType.Sphere, Vector3.one * 0.17f, new Vector3(0f, -0.15f, 0f));
        }

        void AddPart(PrimitiveType type, Vector3 scale, Vector3 localPosition)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = markMaterial;
            part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.transform.SetParent(mark, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            mark.localPosition = Vector3.up * (MarkHeight + 0.08f * Mathf.Sin(Time.time * 14f));
        }

        void OnDestroy()
        {
            if (mark != null) Destroy(mark.gameObject);
            if (markMaterial != null) Destroy(markMaterial);
        }
    }
}
