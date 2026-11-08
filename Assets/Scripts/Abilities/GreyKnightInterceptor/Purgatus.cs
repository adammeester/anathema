using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.GreyKnightInterceptor
{
    // Ultimate (R): Purgatus, the Cleansing Nova. Costs a full energy bar. The knight channels for a moment (a
    // ground telegraph shows the full radius, with an inner disc filling as the charge builds), then releases a ring
    // of warp-fire that expands outwards. Every enemy it sweeps over is damaged once, knocked back and silenced.
    // Pressing C (or dying) during the channel cancels it and refunds the energy. Nothing refunds it once released.
    public class Purgatus : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float channelDuration = 1f;
        [SerializeField] float radius = 7f;
        [SerializeField] float expandDuration = 0.45f;
        [SerializeField] float damage = 38f;
        [SerializeField] float knockbackDistance = 4f;
        [SerializeField] float knockbackDuration = 0.25f;
        [SerializeField] float silenceDuration = 1.5f;
        [SerializeField] Color telegraphColor = new Color(0.75f, 0.5f, 1f, 0.15f);
        [SerializeField] Color chargeColor = new Color(0.85f, 0.7f, 1f, 0.35f);

        Energy energy;
        Transform telegraphDisc;
        Transform chargeDisc;
        PsychicFireRing channelFire;
        AudioSource channelSound;
        bool channeling;
        bool expanding;
        float channelStart;
        float expandStart;
        Vector3 center;
        readonly HashSet<Health> struck = new HashSet<Health>();

        protected override bool CooldownStartsOnStrike => true;

        public override string DisplayName => "Purgatus";
        public override string KeyLabel => "R";

        // Planted for the whole cast: the channel and the moment the ring leaves him.
        public override bool RootsMovement => channeling || expanding;

        // The HUD greys the slot out until the bar is full.
        public override bool Available => energy != null && energy.Current >= energyCost && !Silenced.IsSilenced(gameObject) && !Panicked.IsPanicked(gameObject);

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();

            telegraphDisc = CreateDisc("Purgatus Telegraph", telegraphColor);
            chargeDisc = CreateDisc("Purgatus Charge", chargeColor);
        }

        void OnDestroy()
        {
            foreach (Transform t in new[] { telegraphDisc, chargeDisc })
                if (t != null) Destroy(t.gameObject);
            if (channelFire != null) channelFire.EndChannel();
            GreyKnightSound.Stop(channelSound);
        }

        static Transform CreateDisc(string name, Color color)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Destroy(disc.GetComponent<Collider>());
            var renderer = disc.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = VisualUtil.UnlitMaterial(color);
            disc.SetActive(false);
            return disc.transform;
        }

        static void SetDisc(Transform disc, Vector3 position, float radius, float height)
        {
            disc.position = new Vector3(position.x, position.y + height, position.z);
            disc.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        }

        protected override void Update()
        {
            base.Update();

            if (channeling)
            {
                center = transform.position;
                float t = Mathf.Clamp01((Time.time - channelStart) / channelDuration);
                SetDisc(telegraphDisc, center, radius, 0.05f);
                SetDisc(chargeDisc, center, Mathf.Max(0.01f, radius * t), 0.06f);
                if (channelFire != null) channelFire.UpdateChannel(center, radius, t);
                if (channelSound != null) channelSound.pitch = Mathf.Lerp(0.8f, 1.6f, t);
                if (t >= 1f) Release();
            }
            else if (expanding)
            {
                UpdateWave();
            }
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;
        }

        protected override void Activate()
        {
            if (energy == null || !energy.TrySpend(energyCost))
            {
                RefundCooldown();
                return;
            }

            channeling = true;
            channelStart = Time.time;
            telegraphDisc.gameObject.SetActive(true);
            chargeDisc.gameObject.SetActive(true);
            channelFire = PsychicFireRing.CreateChannel();
            channelSound = GreyKnightSound.FireChannel();
        }

        void EndChannelFire()
        {
            if (channelFire != null) channelFire.EndChannel();
            channelFire = null;
            GreyKnightSound.Stop(channelSound);
            channelSound = null;
        }

        protected override void Cancel()
        {
            if (!channeling) return; // a released wave carries on; the energy is spent

            channeling = false;
            telegraphDisc.gameObject.SetActive(false);
            chargeDisc.gameObject.SetActive(false);
            EndChannelFire();
            if (energy != null) energy.Gain(energyCost);
            RefundCooldown();
        }

        void Release()
        {
            channeling = false;
            telegraphDisc.gameObject.SetActive(false);
            chargeDisc.gameObject.SetActive(false);

            expanding = true;
            expandStart = Time.time;
            struck.Clear();
            EndChannelFire();
            PsychicFireRing.Spawn(center, radius, expandDuration);
            GreyKnightSound.FireRelease();
            StartCooldown();
        }

        void UpdateWave()
        {
            if (Health.IsDead)
            {
                EndWave();
                return;
            }

            float t = Mathf.Clamp01((Time.time - expandStart) / expandDuration);
            float current = radius * PsychicFireRing.Ease(t); // same curve as the fire, so hits match what you see

            foreach (Collider col in Physics.OverlapSphere(center + Vector3.up, current + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || struck.Contains(target)) continue;

                Vector3 to = col.ClosestPoint(center) - center;
                to.y = 0f;
                if (to.magnitude > current) continue;

                struck.Add(target);
                Vector3 away = (target.transform.position - center);
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = Aim.AimDirection;

                if (!target.TakeDamage(damage, gameObject, true)) continue;
                GreyKnightSound.Impact();
                if (target.IsDead) continue;
                Knockback.Apply(target.gameObject, away, knockbackDistance, knockbackDuration);
                Silenced.Apply(target.gameObject, silenceDuration);
            }

            if (t >= 1f) EndWave();
        }

        void EndWave()
        {
            expanding = false;
        }
    }
}
