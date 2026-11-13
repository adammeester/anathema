using UnityEngine;

namespace Anathema.Combat
{
    // Status effect: the owner takes extra damage from every source (Health.ApplyDamageLocal reads it). Applied as a
    // hit rider, which runs where damage is resolved (the server online, this machine offline), so the amplification
    // lives exactly where it is needed. Re-applying keeps the stronger amount and the later end.
    // Shows a sickly green ring at the feet on the machine that holds it.
    public class Irradiated : MonoBehaviour
    {
        public static readonly Color Glow = new Color(0.55f, 1f, 0.25f, 1f);

        float extra;
        float endTime;
        float nextPulse;

        public static void Apply(GameObject target, float extraFraction, float seconds)
        {
            if (target == null || extraFraction <= 0f || seconds <= 0f) return;
            if (target.TryGetComponent(out Health h) && (h.Structure || h.IsDead)) return;

            var irradiated = target.GetComponent<Irradiated>();
            if (irradiated == null) irradiated = target.AddComponent<Irradiated>();
            bool live = Time.time < irradiated.endTime;
            irradiated.extra = live ? Mathf.Max(irradiated.extra, extraFraction) : extraFraction;
            irradiated.endTime = Mathf.Max(irradiated.endTime, Time.time + seconds);
        }

        // 1 + the extra damage share while irradiated, 1 otherwise.
        public static float DamageMultiplier(GameObject target)
        {
            return target.TryGetComponent(out Irradiated i) && Time.time < i.endTime ? 1f + i.extra : 1f;
        }

        void Update()
        {
            var health = GetComponent<Health>();
            if (Time.time >= endTime || (health != null && health.IsDead))
            {
                Destroy(this);
                return;
            }

            if (Time.time < nextPulse) return;
            nextPulse = Time.time + 0.6f;
            Anathema.Abilities.Psyker.PsykerFx.Ring(transform.position, 0.3f, 1.1f, 0.5f, Glow, 0.2f);
        }
    }
}
