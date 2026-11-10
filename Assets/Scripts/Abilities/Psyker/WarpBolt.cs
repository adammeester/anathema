using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // Hold left mouse to hurl crackling bolts of warp energy along the aim line. Fast and accurate, with a modest
    // damage per bolt; every hit feeds the energy bar that powers the Psyker's ultimate.
    // Passive: bolts deal 40% more damage to Charged targets (see Charged).
    //
    // Force Stave (Armament upgrade): this becomes Stave Sweep, a wide melee arc that hits everything in front of the
    // Psyker and shoves it back. The Charged passive still applies. Hold to keep swinging.
    public class WarpBolt : Ability
    {
        [SerializeField] float damage = 9f;
        [SerializeField] float range = 14f;
        [SerializeField] float projectileSpeed = 26f;
        [SerializeField] float projectileRadius = 0.28f;
        [SerializeField] float muzzleHeight = 1.25f;
        [SerializeField] float muzzleForward = 0.6f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] float chargedBonus = 0.4f;   // extra damage against Charged targets
        [SerializeField] Color boltColor = new Color(0.45f, 0.95f, 1f);

        [Header("Stave Sweep (Force Stave)")]
        [SerializeField] float staveDamage = 12f;
        [SerializeField] float staveRange = 3.4f;
        [SerializeField] float staveArc = 110f;
        [SerializeField] float staveWindup = 0.2f;
        [SerializeField] float staveCooldown = 0.7f;
        [SerializeField] float staveKnockback = 1.2f;
        [SerializeField] float staveKnockbackDuration = 0.15f;

        Energy energy;
        Material boltMaterial;
        Material haloMaterial;

        bool Stave => PsykerUpgrade.HoldsStave(gameObject);

        protected override float WindupDuration => Stave ? staveWindup : 0.15f;
        protected override Color WindupColor => boltColor;

        public override string DisplayName => Stave ? "Stave Sweep" : "Warp Bolt";
        public override string KeyLabel => "LMB";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 0.55f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        // The stave swings at its own pace; `cooldown` itself stays the bolt's.
        protected override void Update()
        {
            CooldownScale = Stave && cooldown > 0f ? staveCooldown / cooldown : 1f;
            base.Update();
        }

        void OnDestroy()
        {
            if (boltMaterial != null) Destroy(boltMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButton(0) && !PrimaryFireBlocked;
        }

        // Stave Sweep: every enemy in the arc in front of the Psyker is struck and shoved back. Every machine shows the
        // arc; only the owner's copy deals the damage.
        void Sweep(Vector3 dir)
        {
            Vector3 origin = transform.position;
            PsykerFx.Arc(origin, dir, staveArc * 0.5f, staveRange, 0.2f, PsykerFx.Cyan);
            if (IsReplica) return;

            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.IsDead || h.Team == Health.Team) continue;

                Vector3 to = h.transform.position - origin;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > staveRange + 0.4f) continue; // a little allowance for the target's body
                if (distance > 0.05f && Vector3.Angle(dir, to) > staveArc * 0.5f) continue;

                if (!h.TakeDamage(staveDamage * ChargedScale(h), gameObject)) continue;
                OnHit(h);
                if (!h.IsDead) Knockback.Apply(h.gameObject, to, staveKnockback, staveKnockbackDuration);
            }
        }

        protected override void Activate()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            if (Stave)
            {
                Sweep(dir);
                return;
            }

            Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;

            if (boltMaterial == null) boltMaterial = VisualUtil.UnlitMaterial(boltColor);
            if (haloMaterial == null) haloMaterial = VisualUtil.UnlitMaterial(new Color(boltColor.r, boltColor.g, boltColor.b, 0.3f));

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Warp Bolt";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (projectileRadius * 1.3f);
            go.GetComponent<Renderer>().sharedMaterial = boltMaterial;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Halo";
            Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = Vector3.one * 2.2f;
            halo.GetComponent<Renderer>().sharedMaterial = haloMaterial;
            halo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.startWidth = projectileRadius * 1.1f;
            trail.endWidth = 0f;
            trail.sharedMaterial = haloMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir, OnHit, ChargedScale);
        }

        // Passive: Warp Bolts hit Charged enemies harder.
        float ChargedScale(Health target)
        {
            return Charged.IsCharged(target.gameObject) ? 1f + chargedBonus : 1f;
        }

        void OnHit(Health target)
        {
            HitMarkerSound.Play();
            if (energy != null) energy.Gain(energyPerHit);
        }

        protected override void Cancel()
        {
        }
    }
}
