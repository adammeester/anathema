using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // E: the Mandrake melts into the shadows. Enemies can't see it at all (body, health bar and effects vanish from
    // their screens) and it moves faster. Any attack breaks the veil; Shadow Jump doesn't. Ambush thrown from the veil
    // stuns. Attacks still connect with a veiled Mandrake if they happen to hit it.
    // EX (Shift, costs energy): Balefire. Instead of the veil the Mandrake hurls a bolt of violet witch-fire along the aim
    // (a skillshot: the first enemy in its path). It sets them alight with Balefire, a burn that only the Mandrake can
    // fan: the next time the Mandrake lands any hit on them, all the burn damage still to come lands at once.
    public class ShadowVeil : Ability
    {
        [SerializeField] float duration = 4f;

        [Header("EX: Balefire")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float exWindup = 0.2f;
        [SerializeField] float boltDamage = 6f;
        [SerializeField] float burnDamagePerSecond = 5f;
        [SerializeField] float burnDuration = 6f;
        [SerializeField] float boltRange = 13f;
        [SerializeField] float boltSpeed = 22f;
        [SerializeField] float boltRadius = 0.3f;
        [SerializeField] float muzzleHeight = 1.2f;
        [SerializeField] float muzzleForward = 0.6f;

        Energy energy;
        Material boltMaterial;
        Material haloMaterial;

        public override string DisplayName => "Shadow Veil";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Balefire";
        public override string ExDescription => "Balefire";

        // Only the EX bolt winds up; the veil is instant. The owner reads Shift here (ExBlocked has already stopped an
        // unaffordable EX); a replica has ExActivation set from the owner's cue before the cast begins.
        protected override float WindupDuration => (IsReplica ? ExActivation : Input.GetKey(KeyCode.LeftShift)) ? exWindup : 0f;
        protected override Color WindupColor => ExActivation || (!IsReplica && Input.GetKey(KeyCode.LeftShift)) ? Balefire.Core : MandrakeKit.Baleflame;

        void Reset()
        {
            cooldown = 12f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        void OnDestroy()
        {
            if (boltMaterial != null) Destroy(boltMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        protected override void OnWindupStart()
        {
            // The EX energy is spent when the wind-up starts and given back if it is cancelled.
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
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
                HurlBalefire();
                return;
            }

            // Replicas draw the veil from the owner's published status (NetPlayer), not from the cue.
            if (IsReplica) return;
            Stealthed.Apply(gameObject, duration);
        }

        // EX: the bolt flies on every machine; only the owner's copy strikes (Health.Strike ignores a replica's).
        void HurlBalefire()
        {
            Vector3 dir = MandrakeKit.FlatAim(Aim.AimDirection, transform);
            float bonus = 0f;
            if (!IsReplica) bonus = MandrakeKit.BeginStrike(gameObject, out _);

            Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;
            if (boltMaterial == null) boltMaterial = VisualUtil.UnlitMaterial(Balefire.Tip, 3002);
            if (haloMaterial == null)
                haloMaterial = VisualUtil.UnlitMaterial(new Color(Balefire.Core.r, Balefire.Core.g, Balefire.Core.b, 0.4f), 3001);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Balefire Bolt";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (boltRadius * 1.4f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = boltMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Halo";
            Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = new Vector3(2f, 2f, 2.8f);
            var hr = halo.GetComponent<Renderer>();
            hr.sharedMaterial = haloMaterial;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = boltRadius * 1.3f;
            trail.endWidth = 0f;
            trail.sharedMaterial = haloMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Sfx.Play(Sfx.Flamethrower, 0.35f, 1.3f, 1.5f, 0.35f);
            Sfx.Play(Sfx.MagicAttack2, 0.3f, 0.85f, 0.95f);

            go.AddComponent<Projectile>()
                .Init(gameObject, Health.Team, boltDamage + bonus, boltSpeed, boltRange, boltRadius, dir, OnBoltHit, MandrakeKit.VenomScale)
                .WithRiders(Hit.Direct(0f).WithBalefire(burnDamagePerSecond, burnDuration));
        }

        void OnBoltHit(Health target)
        {
            HitMarkerSound.Play();
            Sfx.Play(Sfx.GroundFire, 0.3f, 1.2f, 1.35f, 0.5f);
        }

        protected override void Cancel()
        {
        }
    }
}
