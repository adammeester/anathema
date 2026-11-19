using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // R: Absolution. She raises the icon and a wave of light washes outward: she and every ally within reach are
    // stripped of every crowd control effect and debuff on them (see Purge). Instant, so it answers a stun or a burn as
    // it lands; like every ability it can't be cast while she herself is stunned or silenced.
    //
    // Online: every machine replays the flash from the cue; her own machine picks who is in range and asks for the
    // purge, which the server applies (Purge.Apply).
    public class Absolution : Ability
    {
        [SerializeField] float radius = 7f;

        static readonly Color Light = new Color(1f, 0.95f, 0.75f, 0.85f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.4f, 0.8f);

        public override string DisplayName => "Absolution";
        public override string KeyLabel => "R";

        void Reset()
        {
            cooldown = 16f;
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.R);
        }

        protected override void Activate()
        {
            HospitallerPose.For(gameObject).Raise(0.45f);
            HospitallerFx.Burst(transform.position, radius, Light, 0.5f, 0.5f);
            Sfx.Play(Sfx.MagicAttack3, 0.4f, 1.3f, 1.45f, 0.6f);

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure || h.Team != Health.Team) continue;
                Vector3 to = h.transform.position - transform.position;
                to.y = 0f;
                if (to.magnitude > radius) continue;

                HospitallerFx.Burst(h.transform.position, 1.2f, Gold, 0.45f, 1.1f, h.transform);
                if (!IsReplica) Purge.Apply(h.gameObject, gameObject);
            }
        }

        protected override void Cancel()
        {
        }
    }
}
