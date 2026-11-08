using System.Collections.Generic;
using Anathema.Abilities;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Player
{
    public enum AttackKind
    {
        Ranged,
        Melee
    }

    // Hold left mouse to attack repeatedly along the aim direction.
    public class BasicAttack : Ability
    {
        [SerializeField] AttackKind kind = AttackKind.Ranged;
        [SerializeField] float damage = 8f;

        [Header("Ranged")]
        [SerializeField] float range = 14f;
        [SerializeField] float projectileSpeed = 26f;
        [SerializeField] float projectileRadius = 0.25f;
        [SerializeField] float muzzleHeight = 1.2f;
        [SerializeField] float muzzleForward = 0.7f;
        [SerializeField] Color projectileColor = new Color(0.25f, 0.55f, 1f);

        [Header("Melee")]
        [SerializeField] float meleeRange = 2.5f;
        [SerializeField] float meleeArcDegrees = 120f;

        [Header("Energy")]
        [SerializeField] float energyPerHit = 8f;

        Energy energy;
        Transform muzzle;
        Material projectileMaterial;
        Material glowMaterial;
        readonly HashSet<Health> struck = new HashSet<Health>();

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 0.85f;
        }

        protected override void Awake()
        {
            base.Awake();
            muzzle = FindDeep(transform, "Muzzle_R");
            energy = GetComponent<Energy>();
        }

        void OnHit(Health _)
        {
            if (energy != null) energy.Gain(energyPerHit);
        }

        static Transform FindDeep(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t;
            return null;
        }

        public override string DisplayName => "Attack";
        public override string KeyLabel => "LMB";

        protected override bool IsTriggered()
        {
            return Input.GetMouseButton(0) && !PrimaryFireBlocked;
        }

        protected override void Activate()
        {
            if (kind == AttackKind.Ranged) FireProjectile();
            else Swing();
        }

        // The attack resolves instantly (the projectile or swing hits on the frame it fires), so there is no
        // wind-up or animation to cancel.
        protected override void Cancel()
        {
        }

        void FireProjectile()
        {
            Vector3 dir = Aim.AimDirection;
            Vector3 origin = muzzle != null
                ? muzzle.position
                : transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;

            // The muzzle is offset from the body's centre line, so firing parallel to AimDirection misses the cursor.
            // Aim from the muzzle at the aim point instead (flat), unless the cursor is too close to be meaningful.
            Vector3 toAim = Aim.AimPoint - origin;
            toAim.y = 0f;
            if (toAim.sqrMagnitude > 0.25f) dir = toAim.normalized;

            // Plasma bolt: small white-hot core, soft coloured glow around it, and a short fading trail.
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            // Stretched along local Z, which Projectile.Init points down the flight direction; the glow child inherits it.
            go.transform.localScale = new Vector3(projectileRadius * 0.8f, projectileRadius * 0.8f, projectileRadius * 3.5f);

            if (projectileMaterial == null) projectileMaterial = VisualUtil.UnlitMaterial(Color.Lerp(projectileColor, Color.white, 0.75f));
            if (glowMaterial == null) glowMaterial = VisualUtil.UnlitMaterial(new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0.4f));
            go.GetComponent<Renderer>().sharedMaterial = projectileMaterial;

            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Glow";
            Destroy(glow.GetComponent<Collider>());
            glow.transform.SetParent(go.transform, false);
            glow.transform.localScale = Vector3.one * 2.2f;
            glow.GetComponent<Renderer>().sharedMaterial = glowMaterial;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = projectileRadius * 1.2f;
            trail.endWidth = 0f;
            trail.sharedMaterial = glowMaterial;
            trail.startColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0.6f);
            trail.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir, OnHit);
        }

        void Swing()
        {
            struck.Clear();
            Vector3 origin = transform.position;
            Collider[] colliders = Physics.OverlapSphere(origin + Vector3.up, meleeRange, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider c in colliders)
            {
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target.Team == Health.Team || target.IsDead || !struck.Add(target)) continue;

                Vector3 to = c.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(Aim.AimDirection, to) > meleeArcDegrees * 0.5f) continue;

                if (target.TakeDamage(damage, gameObject)) OnHit(target);
            }
        }
    }
}
