using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // Q: a short, vicious cut in front of the Mandrake. Thrown from Shadow Veil it is an ambush: it deals bonus damage,
    // briefly stuns and poisons everything it hits (and reveals the Mandrake). Out of stealth it is just a hard cut.
    // C during the short wind-up cancels it and refunds the cooldown.
    // EX (Shift, costs energy): Chain Hook. Instead of the cut the Mandrake throws a hook on a chain a short way along the
    // aim; the first enemy it bites takes a little damage and is slowed, and landing it hastes the Mandrake.
    public class Ambush : Ability
    {
        [SerializeField] float damage = 14f;
        [SerializeField] float stealthBonusDamage = 14f;
        [SerializeField] float stunDuration = 0.9f;
        [SerializeField] int stealthPoisonStacks = 2;
        [SerializeField] float poisonDamagePerSecond = 1f;  // per stack
        [SerializeField] float poisonDuration = 4f;
        [SerializeField] float range = 3f;
        [SerializeField] float arcDegrees = 80f;
        [SerializeField] float energyPerHit = 10f;
        [SerializeField] float windup = 0.15f;

        [Header("EX: Chain Hook")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float hookRange = 6.5f;
        [SerializeField] float hookSpeed = 26f;
        [SerializeField] float hookRadius = 0.35f;
        [SerializeField] float hookDamage = 8f;
        [SerializeField] float hookSlow = 0.3f;
        [SerializeField] float hookSlowDuration = 2.5f;
        [SerializeField] float hookHasteBonus = 0.15f;
        [SerializeField] float hookHasteDuration = 2.5f;

        Energy energy;
        bool replicaFromStealth;

        public override string DisplayName => "Ambush";
        public override string KeyLabel => "Q";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Chain Hook";
        public override string ExDescription => "Chain Hook";
        protected override float WindupDuration => windup;
        protected override Color WindupColor => MandrakeKit.Baleflame;

        void Reset()
        {
            cooldown = 8f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override void OnWindupStart()
        {
            // A replica can only tell it was an ambush by whether the veil is still drawn as the wind-up begins.
            if (IsReplica) replicaFromStealth = Stealthed.IsStealthed(gameObject);
            // The EX energy is spent when the wind-up starts and given back if it is cancelled.
            else ExActivation = TrySpendEx(energy, exEnergyCost);
        }

        protected override void OnWindupCancel()
        {
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.Q);

        protected override void Activate()
        {
            if (ExActivation)
            {
                ThrowHook();
                return;
            }

            Vector3 dir = MandrakeKit.FlatAim(Aim.AimDirection, transform);
            bool fromStealth = replicaFromStealth;
            float bonus = 0f;
            if (!IsReplica) bonus = MandrakeKit.BeginStrike(gameObject, out fromStealth);

            SlashEffect.Spawn(transform.position, dir, range, arcDegrees, 0.25f, fromStealth ? MandrakeKit.BaleflameBright : MandrakeKit.Baleflame);
            if (fromStealth) SlashEffect.Spawn(transform.position, dir, range * 0.7f, arcDegrees * 0.6f, 0.35f, MandrakeKit.Shadow);
            MeleeSound.Slash(0.6f, fromStealth ? 0.75f : 0.95f, fromStealth ? 0.85f : 1.05f);
            if (IsReplica) return;

            int hits = 0;
            foreach (Health target in MandrakeKit.EnemiesInCone(Health, transform.position, dir, range, arcDegrees))
            {
                float dealt = (damage + (fromStealth ? stealthBonusDamage : 0f) + bonus) * MandrakeKit.VenomScale(target);
                Hit hit = Hit.Direct(dealt);
                if (fromStealth)
                {
                    hit = hit.WithStun(stunDuration);
                    if (stealthPoisonStacks > 0) hit = hit.WithPoison(stealthPoisonStacks, poisonDamagePerSecond, poisonDuration);
                }
                hits++;
                target.Strike(hit, gameObject, landed =>
                {
                    if (landed && energy != null) energy.Gain(energyPerHit);
                });
            }
            if (hits > 0) MeleeSound.Impact(hits);
        }

        // EX: the hook flies out on every machine; only the owner's copy strikes what it bites.
        void ThrowHook()
        {
            Vector3 dir = MandrakeKit.FlatAim(Aim.AimDirection, transform);
            float bonus = 0f;
            if (!IsReplica) bonus = MandrakeKit.BeginStrike(gameObject, out _);

            Sfx.Play(Sfx.AxeToss, 0.45f, 1.15f, 1.3f, 0.5f);
            ChainHook.Launch(transform, Health, dir, hookRange, hookSpeed, hookRadius, !IsReplica, target =>
            {
                Hit hit = Hit.Direct((hookDamage + bonus) * MandrakeKit.VenomScale(target)).WithSlow(hookSlowDuration, hookSlow);
                target.Strike(hit, gameObject, landed =>
                {
                    if (!landed) return;
                    Hasted.Apply(gameObject, hookHasteDuration, hookHasteBonus);
                    HitMarkerSound.Play();
                });
            });
        }

        protected override void Cancel()
        {
        }
    }
}
