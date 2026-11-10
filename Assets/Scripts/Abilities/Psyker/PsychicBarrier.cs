using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // E: Kine Shield. Wraps the Psyker in a telekinetic shield that soaks up to `strength` damage (AoE and damage over time included)
    // and fades after `duration` seconds if it isn't used up. It is purely defensive: nothing comes out of it.
    // Damage is resolved on the owner's machine, so only the owner's copy absorbs; replicas show the shell for the same
    // duration. Recasting replaces the old shield. C (or dying) drops it.
    // EX (Shift+E): Psychic Scream instead of the shield. A wail of fear rolls out from the Psyker and panics every
    // enemy in range: they drop everything and run from her. It does no damage. A Psyker holding the Force Stave
    // loses the scream (she gets Mind Leech instead).
    public class PsychicBarrier : Ability
    {
        [SerializeField] float strength = 50f;
        [SerializeField] float duration = 5f;
        [SerializeField] float exEnergyCost = 40f;
        [SerializeField] float screamRadius = 4.2f;
        [SerializeField] float screamPanicSeconds = 2f;
        [SerializeField] Color shellColor = new Color(0.35f, 0.85f, 1f, 0.3f);

        const float ShellRadius = 1.1f;

        Energy energy;
        bool active;
        float remaining;
        float endTime;
        Transform shell;
        Material shellMaterial;

        // The Kine Shield is instant; only the EX Psychic Scream winds up. This is read once, as the key is pressed,
        // before the EX energy is spent: the owner goes by Shift and spare energy, a replica by the owner's cue.
        protected override float WindupDuration
        {
            get
            {
                if (HoldsStave) return 0f;
                bool scream = IsReplica ? ExActivation
                    : Input.GetKey(KeyCode.LeftShift) && energy != null && energy.Current >= exEnergyCost;
                return scream ? 0.4f : 0f;
            }
        }
        protected override Color WindupColor => PsykerFx.Cyan;

        // The EX energy is spent when the wind-up starts and given back if it is cancelled.
        protected override void OnWindupStart()
        {
            if (!IsReplica && !HoldsStave) ExActivation = TrySpendEx(energy, exEnergyCost);
        }

        protected override void OnWindupCancel()
        {
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        public override string DisplayName => "Kine Shield";
        public override string KeyLabel => "E";
        // The Force Stave trades the scream away for Mind Leech (on RMB): no EX version of this key while it is held.
        bool HoldsStave => PsykerUpgrade.HoldsStave(gameObject);
        public override float ExCost => HoldsStave ? 0f : exEnergyCost;
        public override string ExDescription => "Psychic Scream";
        public override string ExName => "Psychic Scream";

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
                Scream();
                return;
            }

            Drop(false);

            active = true;
            remaining = strength;
            endTime = Time.time + duration;

            if (shellMaterial == null) shellMaterial = VisualUtil.UnlitMaterial(shellColor, 3000);
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Kine Shield";
            Destroy(s.GetComponent<Collider>());
            s.GetComponent<Renderer>().sharedMaterial = shellMaterial;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.transform.SetParent(transform, false);
            s.transform.localPosition = Vector3.up * 0.9f;
            s.transform.localScale = Vector3.one * (ShellRadius * 2f);
            shell = s.transform;

            // Damage is resolved where Health is the authority (the server online), so that copy does the absorbing.
            if (Health.IsAuthority) Health.Absorber = Absorb;
            PsykerFx.Ring(transform.position, 0.3f, 1.8f, 0.3f, PsykerFx.Cyan);
        }

        // EX: every enemy within the radius is gripped by panic. Every machine draws the rings; only the owner's copy
        // applies the panic (Panicked.Apply forwards it to a remote victim's machine).
        void Scream()
        {
            Vector3 centre = transform.position;
            Color wail = new Color(0.75f, 0.45f, 1f, 0.9f);
            PsykerFx.Ring(centre, 0.4f, screamRadius, 0.45f, wail, 0.28f);
            PsykerFx.Ring(centre, 0.2f, screamRadius * 0.65f, 0.35f, new Color(1f, 1f, 1f, 0.7f), 0.16f);
            if (IsReplica) return;

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team) continue;

                Vector3 offset = h.transform.position - centre;
                offset.y = 0f;
                if (offset.magnitude > screamRadius) continue;

                Panicked.Apply(h.gameObject, gameObject, screamPanicSeconds);
            }
        }

        float Absorb(float amount, GameObject damageSource)
        {
            float taken = Mathf.Min(amount, remaining);
            remaining -= taken;
            if (remaining <= 0f)
            {
                Drop(true);
                if (Health.Net != null && Health.Net.IsSpawned) Health.Net.ServerShieldBroken(Anathema.Net.NetPlayer.ShieldKind.Kine);
            }
            return amount - taken;
        }

        // Online: the server's copy ran the shield out; drop this machine's copy with the break effect.
        public void BreakFromServer() => Drop(true);

        protected override void Update()
        {
            base.Update();
            if (!active) return;

            if (Health.IsDead || Time.time >= endTime)
            {
                Drop(false);
                return;
            }

            // Thins as it soaks damage (where it absorbs) and flickers when about to expire.
            float strength01 = !Health.IsAuthority || strength <= 0f ? 1f : Mathf.Clamp01(remaining / strength);
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
            Drop(false);
        }
    }
}
