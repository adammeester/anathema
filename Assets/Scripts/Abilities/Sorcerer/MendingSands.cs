using Anathema.Abilities.Bullgryn;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // RMB: Mending Sands. A handful of healing dust is thrown on a short arc to the cursor (held within range) and
    // scatters over the ground there: every teammate inside the circle, the Sorcerer included, is healed. A ring marks
    // where it will land. Three charges, each recharging on its own.
    // Rejuvenate (upgrade): everyone healed is also left with Mending, a second, smaller heal a few seconds later (Dust
    // Form sets it off at once). Shifting Dunes (upgrade): Sinking Sands hits add bonus charges that don't recharge.
    public class MendingSands : Ability
    {
        const int MaxCharges = 3;

        [SerializeField] float heal = 12f;
        [SerializeField] float mendHeal = 6f;
        [SerializeField] float mendSeconds = 3.5f;
        [SerializeField] float range = 11f;
        [SerializeField] float radius = 2.2f;
        [SerializeField] float flightTime = 0.35f;
        [SerializeField] float windup = 0.3f;
        [SerializeField] float rechargeTime = 6f;
        [SerializeField] float energyPerHeal = 5f;

        readonly float[] chargeReady = new float[MaxCharges];
        int bonusCharges; // Shifting Dunes: spent first, never recharge
        Energy energy;

        public override string DisplayName => "Mending Sands";
        public override string KeyLabel => "RMB";
        public override int ChargeCapacity => MaxCharges + bonusCharges;
        public override int ChargesReady => ReadyCharges;

        protected override float WindupDuration => windup;
        protected override Color WindupColor => Mending.Glow;

        float Recharge => rechargeTime * CooldownScale;

        int ReadyCharges
        {
            get
            {
                int n = 0;
                foreach (float t in chargeReady) if (Time.time >= t) n++;
                return n + bonusCharges;
            }
        }

        float NextCharge
        {
            get
            {
                float next = float.MaxValue;
                foreach (float t in chargeReady) if (t > Time.time && t < next) next = t;
                return next;
            }
        }

        // With a charge in hand only the short gap between throws counts; with none, the wait for the next charge.
        public override float CooldownRemaining =>
            ReadyCharges > 0 ? base.CooldownRemaining : Mathf.Max(0f, NextCharge - Time.time);
        public override float CooldownNormalized =>
            ReadyCharges > 0 ? base.CooldownNormalized : (Recharge > 0f ? Mathf.Clamp01(CooldownRemaining / Recharge) : 0f);
        public override float ChargeRechargeNormalized
        {
            get
            {
                int ready = ReadyCharges;
                if (ready == 0 || ready >= MaxCharges + bonusCharges || Recharge <= 0f) return 0f;
                return Mathf.Clamp01((NextCharge - Time.time) / Recharge);
            }
        }

        void Reset()
        {
            cooldown = 0.4f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetMouseButtonDown(1) && ReadyCharges > 0;

        protected override void Activate()
        {
            if (!IsReplica && bonusCharges > 0)
            {
                bonusCharges--;
            }
            else if (!IsReplica)
            {
                int used = 0;
                for (int i = 1; i < MaxCharges; i++)
                    if (chargeReady[i] < chargeReady[used]) used = i;
                chargeReady[used] = Time.time + Recharge;
            }

            // Every machine works out the same landing spot from the cast cue's aim point.
            Vector3 offset = Aim.AimPoint - transform.position;
            offset.y = 0f;
            if (offset.magnitude > range) offset = offset.normalized * range;
            Vector3 spot = transform.position + offset;

            Vector3 from = transform.position + Vector3.up * 1.6f + transform.forward * 0.5f;
            BullgrynKit.GroundCircle(spot, radius, flightTime, Mending.Glow);
            Sfx.Play(Sfx.MagicAttack2, 0.3f, 1.25f, 1.4f, 0.6f);
            bool owner = !IsReplica;
            var go = new GameObject("Mending Sands");
            go.AddComponent<Mote>().Launch(from, spot, flightTime, () => Land(spot, owner));
        }

        void Land(Vector3 centre, bool owner)
        {
            Anathema.Abilities.Psyker.PsykerFx.Ring(centre + Vector3.up * 0.1f, 0.2f, radius, 0.35f, Mending.Glow, 0.18f);
            Anathema.Abilities.Psyker.PsykerFx.Ring(centre + Vector3.up * 0.12f, radius, radius * 0.4f, 0.5f, SorcererFx.Sand, 0.12f);
            Sfx.Play(Sfx.MagicImpact, 0.28f, 1.35f, 1.5f, 0.5f);
            if (!owner || this == null || Health == null || Health.IsDead) return;

            bool any = false;
            foreach (Health ally in FindObjectsByType<Health>())
            {
                if (ally.IsDead || ally.Structure || ally.Team != Health.Team) continue;
                Vector3 d = ally.transform.position - centre;
                d.y = 0f;
                if (d.magnitude > radius + 0.3f) continue; // a little allowance for the body
                ally.Heal(heal, gameObject);
                if (SorcererUpgrade.Has(gameObject, SorcererUpgrade.Rejuvenate))
                    Mending.Apply(ally.gameObject, gameObject, mendHeal, mendSeconds);
                any = true;
            }
            if (any && energy != null) energy.Gain(energyPerHeal);
        }

        // Owner: Shifting Dunes. One more charge, up to the cap; it is spent before the normal ones and never recharges.
        public void AddBonusCharge()
        {
            if (bonusCharges < SorcererUpgrade.ShiftingDunesMaxBonus) bonusCharges++;
        }

        protected override void Cancel()
        {
        }

        // The thrown dust: a swirl of gold and blue motes on a short arc to the landing spot, then `onLand`. Purely visual.
        sealed class Mote : MonoBehaviour
        {
            const int Grains = 6;
            const float ApexHeight = 1.5f;

            Vector3 from, to;
            float start, duration;
            System.Action onLand;
            Transform[] grains;
            Material gold, blue;

            public void Launch(Vector3 a, Vector3 b, float seconds, System.Action landed)
            {
                from = a;
                to = b + Vector3.up * 0.3f;
                start = Time.time;
                duration = Mathf.Max(0.05f, seconds);
                onLand = landed;
                transform.position = from;
                gold = VisualUtil.UnlitMaterial(SorcererFx.Sand, 3003);
                blue = VisualUtil.UnlitMaterial(Mending.Glow, 3003);
                grains = new Transform[Grains];
                for (int i = 0; i < Grains; i++)
                {
                    GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(g.GetComponent<Collider>());
                    g.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? gold : blue;
                    g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    g.transform.SetParent(transform, false);
                    g.transform.localScale = Vector3.one * 0.14f;
                    grains[i] = g.transform;
                }
            }

            void Update()
            {
                float t = Mathf.Clamp01((Time.time - start) / duration);
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y += ApexHeight * 4f * t * (1f - t);
                transform.position = p;
                for (int i = 0; i < grains.Length; i++)
                {
                    float a = Time.time * 14f + i * Mathf.PI * 2f / grains.Length;
                    grains[i].localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a * 0.7f), Mathf.Sin(a)) * 0.25f;
                }
                if (t < 1f) return;
                onLand?.Invoke();
                Destroy(gameObject);
            }

            void OnDestroy()
            {
                if (gold != null) Destroy(gold);
                if (blue != null) Destroy(blue);
            }
        }
    }
}
