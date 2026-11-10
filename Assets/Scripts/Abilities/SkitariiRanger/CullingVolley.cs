using System.Collections.Generic;
using Anathema.Abilities.Psyker;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SkitariiRanger
{
    // Ultimate (R): Culling Volley. Costs a full energy bar. After a short gather the Ranger plants herself and fires
    // four enormous shots at fixed intervals, each along the cursor's direction at that moment. Each shot has a huge
    // range, pierces every enemy in its line, hits hard and slows what it hits slightly. A laser sight shows where the
    // next shot will go. Stuns, panic, silence and knockback interrupt it (and C or death); an interrupted volley keeps
    // its cooldown and the energy is gone. Only pressing C during the gather refunds it.
    public class CullingVolley : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float gatherDuration = 0.6f;
        [SerializeField] int shotCount = 4;
        [SerializeField] float shotInterval = 0.95f;
        [SerializeField] float damage = 30f;
        [SerializeField] float range = 60f;
        [SerializeField] float shotRadius = 0.6f;
        [SerializeField] float slow = 0.2f;
        [SerializeField] float slowDuration = 2f;
        [SerializeField] float recoverTime = 0.4f;
        [SerializeField] float muzzleHeight = 1.3f;
        [SerializeField] float muzzleForward = 1.6f;

        enum Phase { Idle, Gather, Fire }

        Energy energy;
        Phase phase;
        float phaseStart;
        float nextShot;
        int fired;
        readonly List<Health> hits = new List<Health>();

        GameObject sightRoot;
        LineRenderer sight;
        Material sightMaterial;

        protected override bool CooldownStartsOnStrike => true;

        public override string DisplayName => "Culling Volley";
        public override string KeyLabel => "R";
        public override bool RootsMovement => phase != Phase.Idle;
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
        }

        void OnDestroy()
        {
            DestroySight();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;
        }

        protected override void Activate()
        {
            // Replicas replay the visuals only; the owner already spent the energy here.
            if (!IsReplica && (energy == null || !energy.TrySpend(energyCost)))
            {
                RefundCooldown();
                return;
            }

            phase = Phase.Gather;
            phaseStart = Time.time;
            fired = 0;
            BuildSight();
        }

        Vector3 FlatAim()
        {
            Vector3 d = Aim.AimDirection;
            d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
        }

        protected override void Update()
        {
            base.Update();
            if (phase == Phase.Idle) return;

            if (Health.IsDead) return; // Cancel() has already run

            if (!IsReplica && IsInterrupted())
            {
                Cancel();
                if (Health.Net != null && Health.Net.IsSpawned) Health.Net.SendCue(-1, Aim.AimPoint);
                return;
            }

            Vector3 dir = FlatAim();
            RangerPose.For(gameObject).Aim(0.15f, true);
            Vector3 start = transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;
            Vector3 end = RangerFx.Trace(transform, Health.Team, start, dir, range, 0.1f, null);

            if (phase == Phase.Gather)
            {
                float t = Mathf.Clamp01((Time.time - phaseStart) / gatherDuration);
                DrawSight(start, end, 0.03f + 0.05f * t, 0.15f + 0.3f * t);
                if (t >= 1f)
                {
                    phase = Phase.Fire;
                    nextShot = Time.time;
                }
                return;
            }

            // Between shots the sight brightens as the next one approaches.
            float next = Mathf.Clamp01(1f - (nextShot - Time.time) / shotInterval);
            DrawSight(start, end, 0.04f + 0.1f * next, 0.2f + 0.6f * next);

            if (Time.time >= nextShot && fired < shotCount)
            {
                FireShot(dir, start);
                fired++;
                nextShot = Time.time + shotInterval;
                if (fired >= shotCount) nextShot = Time.time + recoverTime; // after the last shot, linger briefly
            }
            else if (fired >= shotCount && Time.time >= nextShot)
            {
                End();
            }
        }

        bool IsInterrupted()
        {
            return Stunned.IsStunned(gameObject) || Panicked.IsPanicked(gameObject) || Silenced.IsSilenced(gameObject)
                   || GetComponent<Knockback>() != null;
        }

        void FireShot(Vector3 dir, Vector3 start)
        {
            Vector3 end = RangerFx.Trace(transform, Health.Team, start, dir, range, shotRadius, hits);
            RangerFx.Tracer(start, end, 0.8f, Color.white, 0.5f);
            RangerFx.Tracer(start, end, 1.4f, RangerFx.Cyan, 0.35f);
            PsykerFx.Ring(transform.position + dir * muzzleForward, 0.2f, 3f, 0.3f, RangerFx.Cyan, 0.3f);
            PsykerFx.Ring(end, 0.2f, 2f, 0.3f, RangerFx.Cyan, 0.2f);

            if (IsReplica) return;

            bool hitAny = false;
            foreach (Health target in hits)
            {
                if (!target.TakeDamage(damage, gameObject)) continue;
                hitAny = true;
                if (!target.IsDead) Slowed.Apply(target.gameObject, slowDuration, slow);
            }
            if (hitAny) HitMarkerSound.Play();
        }

        void End()
        {
            phase = Phase.Idle;
            DestroySight();
            if (!IsReplica) StartCooldown();
        }

        protected override void Cancel()
        {
            if (phase == Phase.Idle) return;

            bool gathering = phase == Phase.Gather;
            phase = Phase.Idle;
            RangerPose.For(gameObject).Release();
            DestroySight();
            if (IsReplica) return;

            // Cancelled before the first shot: refund. Once it has started the energy stays spent.
            if (gathering && !Health.IsDead && !IsInterrupted() && energy != null)
            {
                energy.Gain(energyCost);
                RefundCooldown();
            }
            else
            {
                StartCooldown();
            }
        }

        // ---- laser sight -------------------------------------------------------------------------------------------

        void BuildSight()
        {
            DestroySight();
            sightRoot = new GameObject("Culling Volley Sight");
            sightMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.25f, 0.2f, 0.2f), 3000);
            sight = sightRoot.AddComponent<LineRenderer>();
            sight.useWorldSpace = true;
            sight.positionCount = 2;
            sight.sharedMaterial = sightMaterial;
            sight.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void DrawSight(Vector3 start, Vector3 end, float width, float alpha)
        {
            if (sight == null) return;
            sight.SetPosition(0, start);
            sight.SetPosition(1, end);
            sight.startWidth = sight.endWidth = width;
            sightMaterial.color = new Color(1f, 0.25f, 0.2f, alpha);
        }

        void DestroySight()
        {
            if (sightRoot != null) Destroy(sightRoot);
            sightRoot = null;
            sight = null;
            if (sightMaterial != null) Destroy(sightMaterial);
            sightMaterial = null;
        }
    }
}
