using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Hold left mouse to spit globs of plague along the aim direction. Each glob does a little damage and adds a stack
    // of Festering to the target: a weak damage over time. Each stack lasts only 2.5 attack intervals, so the rot
    // only builds up while the hits keep landing; miss for a moment and it drains away.
    public class PlagueSpit : Ability
    {
        // How many attack intervals one stack of rot lasts.
        const float StackIntervals = 2.5f;
        // Flat extra seconds added to each stack's duration.
        const float RotBonusSeconds = 1f;

        [SerializeField] float damage = 5f;
        [SerializeField] float rotDamagePerSecond = 1.5f;
        [SerializeField] float range = 13f;
        [SerializeField] float projectileSpeed = 22f;
        [SerializeField] float projectileRadius = 0.3f;
        [SerializeField] float muzzleHeight = 1.3f;
        [SerializeField] float muzzleForward = 0.7f;
        [SerializeField] float energyPerHit = 6f;
        [SerializeField] Color globColor = new Color(0.45f, 0.75f, 0.1f);

        // How long one stack of rot lasts: follows the attack rate, so changing the cooldown rescales it.
        public float RotDuration => cooldown * StackIntervals + RotBonusSeconds;

        Energy energy;
        Material globMaterial;
        Material haloMaterial;

        public override string DisplayName => "Spit";
        public override string KeyLabel => "LMB";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 0.7f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        void OnDestroy()
        {
            if (globMaterial != null) Destroy(globMaterial);
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

            // Raise the drill arm and spit from its hand.
            PlagueSurgeonPose pose = PlagueSurgeonPose.For(gameObject);
            pose.Spit();
            origin = pose.MuzzlePosition(dir, origin);

            if (globMaterial == null) globMaterial = VisualUtil.UnlitMaterial(globColor);
            if (haloMaterial == null) haloMaterial = VisualUtil.UnlitMaterial(new Color(globColor.r, globColor.g, globColor.b, 0.35f));

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Plague Glob";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (projectileRadius * 1.6f);
            go.GetComponent<Renderer>().sharedMaterial = globMaterial;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Halo";
            Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = Vector3.one * 1.8f;
            halo.GetComponent<Renderer>().sharedMaterial = haloMaterial;
            halo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = projectileRadius;
            trail.endWidth = 0f;
            trail.sharedMaterial = haloMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir, OnHit);
        }

        // The projectile only reports hits that actually dealt damage; a replica's visual glob never gets here.
        void OnHit(Health target)
        {
            HitMarkerSound.Play();
            if (energy != null) energy.Gain(energyPerHit);
            if (!target.IsDead)
                Festering.Apply(target.gameObject, gameObject, energy, RotDuration, rotDamagePerSecond);
        }

        // The spit resolves as soon as the glob leaves, so there is nothing to cancel.
        protected override void Cancel()
        {
        }
    }
}
