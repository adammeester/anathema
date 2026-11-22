using Anathema.Net;
using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: pinned to the front of a sliding Hoarfrost casket (Arena.CasketField) and carried along with it.
    // While it lasts the character can't walk or steer (MovementSim), and every ability that still works is unchanged
    // except one rule: a dedicated movement ability (Ability.EscapesCasket) breaks the carry the moment it is cast, and
    // no stun follows. Anything else (a stun, silence, panic) keeps them pinned until the casket hits the wall.
    //
    // The server decides the carry (it catches the victim and resolves the impact) and holds it on its own copy; the
    // owner gets it too (RouteControl), predicts the slide with it and is the one who can escape (Escape tells the server).
    public class CasketCarried : MonoBehaviour
    {
        float endTime;

        public static bool IsCarried(GameObject target) => target != null && target.TryGetComponent(out CasketCarried c) && Time.time < c.endTime;

        // Server or offline: start the carry. Online the owner is told as well.
        public static void Apply(GameObject target, Vector3 direction, float distance, float seconds)
        {
            if (target == null) return;
            if (NetPlayer.RouteControl(target, NetPlayer.StatusKind.CasketCarry, distance, seconds, direction)) return;
            ApplyHere(target, direction, distance, seconds);
        }

        // This machine only (the server's copy, or the owner told by the server).
        public static void ApplyHere(GameObject target, Vector3 direction, float distance, float seconds)
        {
            if (target == null || seconds <= 0f) return;
            Knockback.Force(target, direction, distance, seconds);
            var carried = target.GetComponent<CasketCarried>();
            if (carried == null) carried = target.AddComponent<CasketCarried>();
            carried.endTime = Time.time + seconds;
        }

        // Drops the carry on this machine (escaped, or the round reset).
        public static void Clear(GameObject target)
        {
            if (target == null || !target.TryGetComponent(out CasketCarried c)) return;
            Knockback.Clear(target);
            c.endTime = 0f;
            Destroy(c);
        }

        // The carried character cast a movement ability: break free here and let the server know (it decides whether
        // the escape came in time; see CasketField.ServerEscape).
        public static void Escape(GameObject target)
        {
            if (!IsCarried(target)) return;
            Clear(target);
            Anathema.Arena.CasketField.ReportEscape(target);
        }

        void Update()
        {
            if (Time.time >= endTime || (TryGetComponent(out Health h) && h.IsDead)) Destroy(this);
        }
    }
}
