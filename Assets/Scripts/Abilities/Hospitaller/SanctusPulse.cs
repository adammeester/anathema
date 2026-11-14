using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Hospitaller
{
    // RMB: Sanctus Pulse. Place a blessing on the ground at the cursor. A golden ring fills in for a short moment, then
    // it pulses once and its healing is shared out evenly between every ally standing inside (her included): one
    // ally alone gets all of it, three allies a third each. Enemies are unaffected, so it is aimed, not thrown.
    // Three charges, each recharging on its own: she can drop them one after another. `cooldown` is only the short gap
    // between two casts; `rechargeTime` is how long a spent charge takes to come back.
    public class SanctusPulse : Ability
    {
        const int MaxCharges = 3;

        [SerializeField] float range = 10f;
        [SerializeField] float radius = 2.2f;
        [SerializeField] float delay = 0.5f;
        [SerializeField] float totalHeal = 26f;
        [SerializeField] float energyPerHeal = 5f;
        [SerializeField] float rechargeTime = 6f;

        Energy energy;
        readonly float[] chargeReady = new float[MaxCharges];

        public override string DisplayName => "Sanctus Pulse";
        public override int ChargeCapacity => MaxCharges;
        public override int ChargesReady => ReadyCharges;
        public override string KeyLabel => "RMB";

        float Recharge => rechargeTime * CooldownScale
                          * (HospitallerUpgrade.Has(gameObject, HospitallerUpgrade.SwiftPrayers) ? HospitallerUpgrade.SwiftPrayersScale : 1f);

        // Rapture: every spent charge is back at once. Her machine.
        public void RestoreCharges()
        {
            for (int i = 0; i < MaxCharges; i++) chargeReady[i] = 0f;
        }

        // Last Rites: a free blessing at `position`, placed by the copy that saved her life (the server online), which
        // also resolves its healing there. No charge is spent and it builds no energy.
        public void FreePulse(Vector3 position)
        {
            Pulse.Spawn(this, position, false, true);
        }

        int ReadyCharges
        {
            get
            {
                int n = 0;
                foreach (float t in chargeReady) if (Time.time >= t) n++;
                return n;
            }
        }

        // Earliest time a spent charge comes back (only meaningful when at least one is spent).
        float NextCharge
        {
            get
            {
                float next = float.PositiveInfinity;
                foreach (float t in chargeReady) if (t > Time.time && t < next) next = t;
                return next;
            }
        }

        // With a charge in hand only the short gap between casts counts; with none, the wait for the next charge.
        public override float CooldownRemaining =>
            ReadyCharges > 0 ? base.CooldownRemaining : Mathf.Max(0f, NextCharge - Time.time);

        public override float CooldownNormalized =>
            ReadyCharges > 0 ? base.CooldownNormalized : (Recharge > 0f ? Mathf.Clamp01(CooldownRemaining / Recharge) : 0f);

        // Some charges ready, some still coming back: how far off the next one is.
        public override float ChargeRechargeNormalized
        {
            get
            {
                int ready = ReadyCharges;
                if (ready == 0 || ready == MaxCharges || Recharge <= 0f) return 0f;
                return Mathf.Clamp01((NextCharge - Time.time) / Recharge);
            }
        }

        void Reset()
        {
            cooldown = 0.3f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButtonDown(1) && ReadyCharges > 0;
        }

        protected override void Activate()
        {
            if (!IsReplica)
            {
                // Spend the charge that has been ready longest; only that one starts recharging.
                int used = 0;
                for (int i = 1; i < MaxCharges; i++)
                    if (chargeReady[i] < chargeReady[used]) used = i;
                chargeReady[used] = Time.time + Recharge;
            }

            Vector3 offset = Aim.AimPoint - transform.position;
            offset.y = 0f;
            if (offset.magnitude > range) offset = offset.normalized * range;
            Vector3 spot = transform.position + offset;

            HospitallerPose.For(gameObject).Raise(0.45f);
            Sfx.Play(Sfx.MagicAttack2, 0.35f, 1.15f, 1.3f, 0.8f);
            Pulse.Spawn(this, spot, IsReplica);
        }

        // On the owner's machine (or offline) when the pulse goes off: split the heal between the allies inside
        // (Communion: each gets a fixed share instead, or their split if that is more). `scale` is for the echo of
        // Lingering Blessing; `free` pulses (Last Rites) build no energy.
        void Resolve(Vector3 centre, float scale = 1f, bool free = false)
        {
            var inside = new List<Health>();
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure || h.Team != Health.Team) continue;
                Vector3 d = h.transform.position - centre;
                d.y = 0f;
                if (d.magnitude <= radius) inside.Add(h);
            }

            if (inside.Count == 0) return;
            float each = totalHeal / inside.Count;
            if (HospitallerUpgrade.Has(gameObject, HospitallerUpgrade.Communion))
                each = Mathf.Max(each, totalHeal * HospitallerUpgrade.CommunionShare);
            each *= scale;
            foreach (Health h in inside) Healing.Apply(h, each, gameObject);
            if (energy != null && !free && !IsReplica) energy.Gain(energyPerHeal * scale);
        }

        // Hallowed Ground: allies standing on the blessing can hold more healable health for a moment. Applied where each
        // ally's health is resolved, so every machine's copy of the blessing checks its own authoritative allies.
        void Hallow(Vector3 centre, float seconds)
        {
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (!h.IsAuthority || h.IsDead || h.Structure || h.Team != Health.Team) continue;
                Vector3 d = h.transform.position - centre;
                d.y = 0f;
                if (d.magnitude <= radius) h.RaiseRecoverableCap(HospitallerUpgrade.HallowedBonus, seconds);
            }
        }

        protected override void Cancel()
        {
            // A placed blessing goes off regardless.
        }

        // The ground telegraph: a ring and a filling disc, then a rising golden flash.
        class Pulse : MonoBehaviour
        {
            const int RingPoints = 40;
            const float FlashTime = 0.45f;

            const float HallowRefresh = 0.25f;

            SanctusPulse owner;
            bool visualOnly;
            bool free;
            bool lingering;  // Lingering Blessing: pulses again after a moment
            bool hallowed;   // Hallowed Ground: the blessing stays on the ground
            float start;
            bool fired;
            bool echoed;
            float lastFlash;
            float nextHallow;
            LineRenderer ring;
            Transform disc;
            Transform flash;
            Material ringMaterial, discMaterial, flashMaterial;

            public static void Spawn(SanctusPulse owner, Vector3 position, bool visualOnly, bool free = false)
            {
                var go = new GameObject("Sanctus Pulse");
                go.transform.position = position + Vector3.up * 0.06f;
                var p = go.AddComponent<Pulse>();
                p.owner = owner;
                p.visualOnly = visualOnly;
                p.free = free;
                // The upgrade mask is synced, so every machine's copy of the blessing behaves alike.
                p.lingering = HospitallerUpgrade.Has(owner.gameObject, HospitallerUpgrade.LingeringBlessing);
                p.hallowed = HospitallerUpgrade.Has(owner.gameObject, HospitallerUpgrade.HallowedGround);
                p.start = Time.time;
                p.Build();
            }

            void Build()
            {
                float r = owner.radius;
                ringMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.85f, 0.4f, 0.9f), 3000);
                discMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.9f, 0.55f, 0.25f), 3000);
                flashMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.95f, 0.65f, 0.4f), 3001);

                ring = gameObject.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = RingPoints;
                ring.startWidth = ring.endWidth = 0.09f;
                ring.sharedMaterial = ringMaterial;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int i = 0; i < RingPoints; i++)
                {
                    float a = i / (float)RingPoints * Mathf.PI * 2f;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }

                disc = Prim(PrimitiveType.Cylinder, discMaterial);
                flash = Prim(PrimitiveType.Sphere, flashMaterial);
                flash.gameObject.SetActive(false);
            }

            Transform Prim(PrimitiveType type, Material m)
            {
                GameObject g = GameObject.CreatePrimitive(type);
                Destroy(g.GetComponent<Collider>());
                g.GetComponent<Renderer>().sharedMaterial = m;
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.transform.SetParent(transform, false);
                return g.transform;
            }

            void Update()
            {
                float delay = owner != null ? owner.delay : 0.5f;
                float r = owner != null ? owner.radius : 2.2f;
                float t = Time.time - start;

                if (!fired)
                {
                    // The disc grows out to the ring as the pulse nears.
                    float k = Mathf.Clamp01(t / delay);
                    disc.localScale = new Vector3(r * 2f * k, 0.01f, r * 2f * k);
                    if (t < delay) return;

                    fired = true;
                    lastFlash = Time.time;
                    // Hallowed ground keeps a faint disc for as long as it lasts.
                    disc.gameObject.SetActive(hallowed);
                    if (hallowed)
                    {
                        disc.localScale = new Vector3(r * 2f, 0.01f, r * 2f);
                        discMaterial.color = new Color(1f, 0.9f, 0.55f, 0.15f);
                    }
                    flash.gameObject.SetActive(true);
                    Sfx.Play(Sfx.MagicImpact, 0.4f, 1.2f, 1.35f, 0.8f);
                    if (!visualOnly && owner != null) owner.Resolve(transform.position, 1f, free);
                }

                float since = t - delay;

                // Lingering Blessing: a second, softer pulse.
                if (lingering && !echoed && since >= HospitallerUpgrade.LingeringDelay)
                {
                    echoed = true;
                    lastFlash = Time.time;
                    flash.gameObject.SetActive(true);
                    Sfx.Play(Sfx.MagicImpact, 0.3f, 1.3f, 1.45f, 0.6f);
                    if (!visualOnly && owner != null) owner.Resolve(transform.position, HospitallerUpgrade.LingeringScale, free);
                }

                if (hallowed && owner != null && since < HospitallerUpgrade.HallowedSeconds && Time.time >= nextHallow)
                {
                    nextHallow = Time.time + HallowRefresh;
                    owner.Hallow(transform.position, HallowRefresh + 0.15f);
                }

                float lifetime = FlashTime;
                if (lingering) lifetime = Mathf.Max(lifetime, HospitallerUpgrade.LingeringDelay + FlashTime);
                if (hallowed) lifetime = Mathf.Max(lifetime, HospitallerUpgrade.HallowedSeconds);
                if (since >= lifetime)
                {
                    Destroy(gameObject);
                    return;
                }

                float f = (Time.time - lastFlash) / FlashTime;
                if (f < 1f)
                {
                    float strength = echoed && lastFlash > start + delay ? 0.6f : 1f;
                    float eased = 1f - (1f - f) * (1f - f);
                    flash.localScale = new Vector3(r * 2f, r * 1.2f * eased, r * 2f);
                    flashMaterial.color = new Color(1f, 0.95f, 0.65f, 0.4f * strength * (1f - f));
                }
                else if (flash.gameObject.activeSelf) flash.gameObject.SetActive(false);

                // The ring holds while the blessing still has work to do, then fades over its last moment.
                float fade = Mathf.Clamp01((lifetime - since) / FlashTime);
                ringMaterial.color = new Color(1f, 0.85f, 0.4f, 0.9f * fade);
            }

            void OnDestroy()
            {
                if (ringMaterial != null) Destroy(ringMaterial);
                if (discMaterial != null) Destroy(discMaterial);
                if (flashMaterial != null) Destroy(flashMaterial);
            }
        }
    }
}
