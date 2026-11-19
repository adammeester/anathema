using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // R: Shadow Rend. A short, vicious cut in front of the Mandrake that stuns everything it hits. Thrown as a strike
    // from stealth (out of Shadow Veil or an Umbral Riposte) it also deals bonus damage and poisons; a waiting Shadow
    // Jump charge adds its bonus too. C during the short wind-up cancels it and refunds the cooldown.
    public class ShadowRend : Ability
    {
        [SerializeField] float windup = 0.15f;
        [SerializeField] float damage = 14f;
        [SerializeField] float stealthBonusDamage = 14f;
        [SerializeField] float stunDuration = 1.25f;
        [SerializeField] int stealthPoisonStacks = 2;
        [SerializeField] float poisonDamagePerSecond = 1f;  // per stack
        [SerializeField] float poisonDuration = 4f;
        [SerializeField] float range = 3f;
        [SerializeField] float arcDegrees = 80f;
        [SerializeField] float energyPerHit = 10f;

        Energy energy;
        bool replicaFromStealth;

        public override string DisplayName => "Shadow Rend";
        public override string KeyLabel => "R";

        protected override float WindupDuration => windup;
        protected override Color WindupColor => MandrakeKit.BaleflameBright;

        void Reset()
        {
            cooldown = 13f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        // A replica can only tell the cut came from stealth by whether the veil is still drawn as the wind-up begins.
        protected override void OnWindupStart()
        {
            if (IsReplica) replicaFromStealth = Stealthed.IsStealthed(gameObject);
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.R);

        // Drawn on every machine; only the owner's copy strikes.
        protected override void Activate()
        {
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
                float dealt = (damage + (fromStealth ? stealthBonusDamage : 0f) + bonus) * MandrakeKit.VenomScale(gameObject, target);
                Hit hit = Hit.Direct(dealt).WithStun(stunDuration);
                if (fromStealth && stealthPoisonStacks > 0)
                    hit = hit.WithPoison(stealthPoisonStacks, poisonDamagePerSecond, poisonDuration);
                hits++;
                target.Strike(hit, gameObject, landed =>
                {
                    if (landed && energy != null) energy.Gain(energyPerHit);
                });
            }
            if (hits > 0) MeleeSound.Impact(hits);
        }

        protected override void Cancel()
        {
        }
    }
}
