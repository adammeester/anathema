using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // Hold left mouse to hurl crackling bolts of warp energy along the aim line. Fast and accurate, with a modest
    // damage per bolt; every hit feeds the energy bar that powers the Psyker's ultimate.
    public class WarpBolt : Ability
    {
        [SerializeField] float damage = 9f;
        [SerializeField] float range = 14f;
        [SerializeField] float projectileSpeed = 26f;
        [SerializeField] float projectileRadius = 0.28f;
        [SerializeField] float muzzleHeight = 1.25f;
        [SerializeField] float muzzleForward = 0.6f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] Color boltColor = new Color(0.45f, 0.95f, 1f);

        Energy energy;
        Material boltMaterial;
        Material haloMaterial;

        protected override float WindupDuration => 0.15f;
        protected override Color WindupColor => boltColor;

        public override string DisplayName => "Warp Bolt";
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

        void OnDestroy()
        {
            if (boltMaterial != null) Destroy(boltMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        protected override bool IsTriggered()
        {
            return Input.GetMouseButton(0) && !PrimaryFireBlocked;
        }

        protected override void Activate()
        {
            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

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

            go.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir, OnHit);
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
