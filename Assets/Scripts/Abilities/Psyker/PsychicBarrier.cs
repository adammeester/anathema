using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // E: wraps the Psyker in a psychic shield that soaks up to `strength` damage (AoE and damage over time included)
    // and fades after `duration` seconds if it isn't used up. It is purely defensive: nothing comes out of it.
    // Damage is resolved on the owner's machine, so only the owner's copy absorbs; replicas show the shell for the same
    // duration. Recasting replaces the old shield. C (or dying) drops it.
    public class PsychicBarrier : Ability
    {
        [SerializeField] float strength = 50f;
        [SerializeField] float duration = 5f;
        [SerializeField] float exEnergyCost = 40f;
        [SerializeField] float leechRange = 11f;
        [SerializeField] float leechDuration = 2f;
        [SerializeField] float silenceDuration = 2f;
        [SerializeField] float leechDamagePerSecond = 16f;
        [SerializeField] float leechHealPerSecond = 16f;
        [SerializeField] float leechEnergyPerSecond = 12f;
        [SerializeField] Color shellColor = new Color(0.35f, 0.85f, 1f, 0.3f);

        const float ShellRadius = 1.1f;

        Energy energy;
        bool active;
        float remaining;
        float endTime;
        Transform shell;
        Material shellMaterial;

        protected override float WindupDuration => 0.4f;
        protected override Color WindupColor => PsykerFx.Cyan;

        // The EX energy is spent when the wind-up starts and given back if it is cancelled.
        protected override void OnWindupStart()
        {
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
        }

        protected override void OnWindupCancel()
        {
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        public override string DisplayName => "Barrier";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExDescription => "Mind Leech";
        public override string ExName => "Mind Leech";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 10f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        void OnDestroy()
        {
            Drop(false);
            if (shellMaterial != null) Destroy(shellMaterial);
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.E);
        }

        protected override void Activate()
        {
            if (ExActivation)
            {
                // EX: Mind Leech instead of the shield. If there is nobody in range the energy is given back.
                bool started = MindLeech.Begin(gameObject, Health, energy, Aim.AimPoint, !IsReplica, leechRange, leechDuration,
                    silenceDuration, leechDamagePerSecond, leechHealPerSecond, leechEnergyPerSecond);
                if (!started && !IsReplica && energy != null) energy.Gain(exEnergyCost);
                return;
            }

            Drop(false);

            active = true;
            remaining = strength;
            endTime = Time.time + duration;

            if (shellMaterial == null) shellMaterial = VisualUtil.UnlitMaterial(shellColor, 3000);
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Psychic Barrier";
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = shellMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.transform.SetParent(transform, false);
            s.transform.localPosition = Vector3.up * 0.9f;
            s.transform.localScale = Vector3.one * (ShellRadius * 2f);
            shell = s.transform;

            if (!IsReplica) Health.Absorber = Absorb;
            PsykerFx.Ring(transform.position, 0.3f, 1.8f, 0.3f, PsykerFx.Cyan);
        }

        float Absorb(float amount, GameObject damageSource)
        {
            float taken = Mathf.Min(amount, remaining);
            remaining -= taken;
            if (remaining <= 0f) Drop(true);
            return amount - taken;
        }

        protected override void Update()
        {
            base.Update();
            if (!active) return;

            if (Health.IsDead || Time.time >= endTime)
            {
                Drop(false);
                return;
            }

            // Thins as it soaks damage (owner only) and flickers when about to expire.
            float strength01 = IsReplica || strength <= 0f ? 1f : Mathf.Clamp01(remaining / strength);
            float timeLeft = endTime - Time.time;
            float flicker = timeLeft < 1.5f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 18f) : 1f;
            Color c = shellColor;
            c.a = Mathf.Lerp(0.1f, shellColor.a, strength01) * flicker;
            shellMaterial.color = c;
            shell.localScale = Vector3.one * (ShellRadius * 2f * (1f + 0.03f * Mathf.Sin(Time.time * 6f)));
        }

        // Removes the shield. `broken` plays a small burst when the damage ran it out.
        void Drop(bool broken)
        {
            if (!active) return;
            active = false;

            if (Health != null && Health.Absorber == (System.Func<float, GameObject, float>)Absorb) Health.Absorber = null;
            if (shell != null) Destroy(shell.gameObject);
            shell = null;
            if (broken) PsykerFx.Ring(transform.position, 0.8f, 2.4f, 0.3f, PsykerFx.Cyan);
        }

        protected override void Cancel()
        {
            var running = GetComponent<MindLeech>();
            if (running != null) running.Stop();
            Drop(false);
        }
    }
}
