using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // Q: a slow, heavy bolt of crippling warp force. It does a little damage and stuns whoever it hits for 1.5s
    // (Stunned.Apply forwards the stun to the victim's machine online). Only the first enemy in its path is hit.
    public class Cripple : Ability
    {
        [SerializeField] float damage = 5f;
        [SerializeField] float stunDuration = 1.5f;
        [SerializeField] float range = 14f;
        [SerializeField] float projectileSpeed = 20f;
        [SerializeField] float projectileRadius = 0.35f;
        [SerializeField] float muzzleHeight = 1.25f;
        [SerializeField] float muzzleForward = 0.6f;
        [SerializeField] float energyPerHit = 12f;
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float wellRange = 12f;
        [SerializeField] float wellRadius = 4f;
        [SerializeField] float wellDuration = 1.8f;
        [SerializeField] float wellPullSpeed = 6f;
        [SerializeField] float wellDetonationDamage = 25f;
        [SerializeField] Color boltColor = new Color(0.75f, 0.55f, 1f);

        Energy energy;
        Material boltMaterial;
        Material haloMaterial;

        protected override float WindupDuration => 0.4f;
        protected override Color WindupColor => boltColor;

        // The EX energy is spent when the wind-up starts and given back if it is cancelled.
        protected override void OnWindupStart()
        {
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
        }

        protected override void OnWindupCancel()
        {
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        public override string DisplayName => "Cripple";
        public override string KeyLabel => "Q";
        public override float ExCost => exEnergyCost;
        public override string ExDescription => "Gravity Well";
        public override string ExName => "Gravity Well";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 8f;
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
            return Input.GetKeyDown(KeyCode.Q);
        }

        protected override void Activate()
        {
            if (ExActivation)
            {
                // EX: no bolt. A gravity well opens at the cursor (pulled in to max range) and drags enemies to its centre.
                Vector3 here = transform.position;
                Vector3 toWell = Aim.AimPoint - here;
                toWell.y = 0f;
                if (toWell.magnitude > wellRange) toWell = toWell.normalized * wellRange;
                GravityWell.Spawn(gameObject, Health.Team, new Vector3(here.x + toWell.x, here.y, here.z + toWell.z), wellRadius, wellDuration, wellPullSpeed, !IsReplica, wellDetonationDamage);
                return;
            }

            Vector3 dir = Aim.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            Vector3 origin = transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;

            if (boltMaterial == null) boltMaterial = VisualUtil.UnlitMaterial(boltColor);
            if (haloMaterial == null) haloMaterial = VisualUtil.UnlitMaterial(new Color(boltColor.r, boltColor.g, boltColor.b, 0.3f));

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Cripple Bolt";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (projectileRadius * 1.8f);
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
            trail.time = 0.25f;
            trail.startWidth = projectileRadius * 1.4f;
            trail.endWidth = 0f;
            trail.sharedMaterial = haloMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The stun rides on the bolt's hit, so a countered bolt stuns nobody.
            go.AddComponent<Projectile>().Init(gameObject, Health.Team, damage, projectileSpeed, range, projectileRadius, dir, OnHit)
                .WithRiders(Hit.Direct(0f).WithStun(stunDuration));
        }

        // Only reached for real hits that landed (a replica's visual bolt never gets here).
        void OnHit(Health target)
        {
            HitMarkerSound.Play();
            Sfx.Play(Sfx.MagicImpact, 0.45f, 0.9f, 1f);
            if (energy != null) energy.Gain(energyPerHit);
        }

        protected override void Cancel()
        {
        }
    }
}
