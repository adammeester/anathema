using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // F, ultimate (costs a full energy bar): Weaver of Fates. The Sorcerer plucks the teammate nearest the cursor (or
    // himself) out of the weave of time. They vanish for a couple of seconds, untouchable, and their cooldowns are cut.
    // Then they step back in wherever they are aiming (up to a set distance from where they vanished, over walls and
    // all), and every enemy around that spot is stunned. See Timeslipped.
    // Fires of Tzeentch (upgrade): the spot where they step back in bursts into a pool of blue and pink warpfire.
    public class WeaverOfFates : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float castRange = 18f;
        [SerializeField] float slipSeconds = 2f;
        [SerializeField] float returnRange = 13f;
        [SerializeField] float cooldownCut = 0.25f;
        [SerializeField] float arrivalStun = 1.75f;
        [SerializeField] float arrivalRadius = 2.5f;

        Energy energy;

        public override string DisplayName => "Weaver of Fates";
        public override string KeyLabel => "F";
        public override bool Available => energy != null && energy.Current >= energyCost && !Silenced.IsSilenced(gameObject) && !Panicked.IsPanicked(gameObject);

        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.F) && energy != null && energy.Current >= energyCost;

        protected override void Activate()
        {
            // Every machine picks the same teammate from the cast cue's aim point.
            Health target = SorcererFx.NearestAlly(Health, Aim.AimPoint, castRange, false);

            Vector3 a = transform.position + Vector3.up * 1.8f;
            Vector3 b = target.transform.position + Vector3.up * 1.2f;
            Anathema.Abilities.SkitariiRanger.RangerFx.Tracer(a, b, 0.12f, SorcererFx.Warp, 0.3f);
            Anathema.Abilities.Psyker.PsykerFx.Ring(transform.position + Vector3.up * 0.1f, 0.3f, 2f, 0.4f, SorcererFx.Warp, 0.2f);
            Sfx.Play(Sfx.ElectricBeam, 0.35f, 0.7f, 0.8f, 0.8f);

            if (IsReplica) return;
            if (energy == null || !energy.TrySpend(energyCost)) return;
            var arrival = new Anathema.Combat.Timeslipped.Arrival
            {
                CooldownCut = cooldownCut,
                StunSeconds = arrivalStun,
                StunRadius = arrivalRadius,
                Warpfire = SorcererUpgrade.Has(gameObject, SorcererUpgrade.FiresOfTzeentch)
            };
            Anathema.Combat.Timeslipped.Begin(target.gameObject, gameObject, slipSeconds, returnRange, arrival);
        }

        protected override void Cancel()
        {
        }
    }
}
