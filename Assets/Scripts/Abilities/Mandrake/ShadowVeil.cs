using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // E: the Mandrake melts into the shadows. Enemies can't see it at all (body, health bar and effects vanish from
    // their screens) and it moves faster. Any attack breaks the veil; Shadow Jump doesn't. A strike thrown from the veil
    // briefly stuns. Attacks still connect with a veiled Mandrake if they happen to hit it.
    // EX (Shift, costs energy): Shadow Rend. Instead of the veil the Mandrake throws a short, vicious cut in front of it
    // that stuns everything it hits. Thrown as a strike from stealth (e.g. out of an Umbral Riposte) it also deals bonus
    // damage and poisons. C during the short wind-up cancels it and refunds the energy and cooldown.
    public class ShadowVeil : Ability
    {
        [SerializeField] float duration = 4f;

        [Header("EX: Shadow Rend")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float exWindup = 0.15f;
        [SerializeField] float slashDamage = 14f;
        [SerializeField] float slashStealthBonusDamage = 14f;
        [SerializeField] float slashStunDuration = 1.25f;
        [SerializeField] int slashStealthPoisonStacks = 2;
        [SerializeField] float slashPoisonDamagePerSecond = 1f;  // per stack
        [SerializeField] float slashPoisonDuration = 4f;
        [SerializeField] float slashRange = 3f;
        [SerializeField] float slashArcDegrees = 80f;
        [SerializeField] float slashEnergyPerHit = 10f;

        Energy energy;
        bool replicaFromStealth;

        public override string DisplayName => "Shadow Veil";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Shadow Rend";
        public override string ExDescription => "Shadow Rend";

        // Only the EX slash winds up; the veil is instant. The owner reads Shift here (ExBlocked has already stopped an
        // unaffordable EX); a replica has ExActivation set from the owner's cue before the cast begins.
        protected override float WindupDuration => (IsReplica ? ExActivation : Input.GetKey(KeyCode.LeftShift)) ? exWindup : 0f;
        protected override Color WindupColor => MandrakeKit.BaleflameBright;

        void Reset()
        {
            cooldown = 12f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override void OnWindupStart()
        {
            // A replica can only tell the cut came from stealth by whether the veil is still drawn as the wind-up begins.
            if (IsReplica) replicaFromStealth = Stealthed.IsStealthed(gameObject);
            // The EX energy is spent when the wind-up starts and given back if it is cancelled.
            else ExActivation = TrySpendEx(energy, exEnergyCost);
        }

        protected override void OnWindupCancel()
        {
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.E);

        protected override void Activate()
        {
            if (ExActivation)
            {
                Rend();
                return;
            }

            // Replicas draw the veil from the owner's published status (NetPlayer), not from the cue.
            if (IsReplica) return;
            Stealthed.Apply(gameObject, duration);
        }

        // EX: a short cut in front of the Mandrake that stuns everything it hits. Drawn on every machine; only the
        // owner's copy strikes.
        void Rend()
        {
            Vector3 dir = MandrakeKit.FlatAim(Aim.AimDirection, transform);
            bool fromStealth = replicaFromStealth;
            float bonus = 0f;
            if (!IsReplica) bonus = MandrakeKit.BeginStrike(gameObject, out fromStealth);

            SlashEffect.Spawn(transform.position, dir, slashRange, slashArcDegrees, 0.25f, fromStealth ? MandrakeKit.BaleflameBright : MandrakeKit.Baleflame);
            if (fromStealth) SlashEffect.Spawn(transform.position, dir, slashRange * 0.7f, slashArcDegrees * 0.6f, 0.35f, MandrakeKit.Shadow);
            MeleeSound.Slash(0.6f, fromStealth ? 0.75f : 0.95f, fromStealth ? 0.85f : 1.05f);
            if (IsReplica) return;

            int hits = 0;
            foreach (Health target in MandrakeKit.EnemiesInCone(Health, transform.position, dir, slashRange, slashArcDegrees))
            {
                float dealt = (slashDamage + (fromStealth ? slashStealthBonusDamage : 0f) + bonus) * MandrakeKit.VenomScale(target);
                Hit hit = Hit.Direct(dealt).WithStun(slashStunDuration);
                if (fromStealth && slashStealthPoisonStacks > 0)
                    hit = hit.WithPoison(slashStealthPoisonStacks, slashPoisonDamagePerSecond, slashPoisonDuration);
                hits++;
                target.Strike(hit, gameObject, landed =>
                {
                    if (landed && energy != null) energy.Gain(slashEnergyPerHit);
                });
            }
            if (hits > 0) MeleeSound.Impact(hits);
        }

        protected override void Cancel()
        {
        }
    }
}
