using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // Q: Jump Pack Descent. He plants his feet while the jump pack's twin engines spool up (blue flames growing out of the
    // nozzles, smoke pouring off them); C, a stun, silence, panic or knockback during the spool-up cancels it and refunds
    // the cooldown. Then the engines roar to full and launch him high into the air toward the cursor (clamped to range),
    // trailing flame and smoke. He flies over walls and cover, landing short of any he would end up inside. He rises
    // fast, hangs for a moment at the top and slams down. The impact:
    //   - enemies within `landingRadius` take damage and are slowed (pure offence: nothing for allies); under Red Thirst
    //     the damage is RedThirst.SlamDamageMultiplier times as much;
    //   - the ground cracks around the impact site; the cracks fade away after a few seconds.
    // It is his only movement ability, so spending it to dive in leaves him no way out.
    public class JumpPackDescent : Ability
    {
        [SerializeField] float range = 12f;
        [SerializeField] float spoolDuration = 0.55f;
        [SerializeField] float flightDuration = 0.7f;
        [SerializeField] float apexHeight = 4f;
        [SerializeField] float landingRadius = 2.8f;
        [SerializeField] float damage = 12f;
        [SerializeField] float slow = 0.4f;
        [SerializeField] float slowDuration = 1.5f;
        [SerializeField] float crackSeconds = 3f;
        [SerializeField] float energyPerHit = 8f;

        Transform model;
        float liftApplied; // height this leap has added to the model; changed by difference so other offsets on it survive
        Energy energy;
        RedThirst thirst;   // at low health the slam hits harder
        bool flying;
        Vector3 direction;
        float speed;
        float start;
        float end;
        Vector3 landing;
        GameObject telegraph;

        static readonly Color JetBlue = new Color(0.35f, 0.65f, 1f, 0.9f);

        protected override float WindupDuration => spoolDuration;
        protected override Color WindupColor => JetBlue;

        public override string DisplayName => "Jump Pack";
        public override string KeyLabel => "Q";
        public override float MovementRange => range;
        public override bool RootsMovement => IsWindingUp; // planted while the engines spool up

        // The engines spool up: flames grow to half, smoke starts pouring. Owner and replicas alike.
        protected override void OnWindupStart()
        {
            JetPackFx.For(gameObject).SetThrust(0.45f, spoolDuration);
            Sfx.Play(Sfx.Flamethrower, 0.2f, 0.7f, 0.8f, spoolDuration + 0.05f);
        }

        // Spool-up cancelled: the engines die with a cough of smoke.
        protected override void OnWindupCancel()
        {
            var jets = JetPackFx.For(gameObject);
            jets.SetThrust(0f, 0.15f);
            jets.Puff(3, 0.6f);
        }

        void Reset()
        {
            cooldown = 10f;
        }

        protected override void Awake()
        {
            base.Awake();
            model = transform.Find("Model");
            energy = GetComponent<Energy>();
            thirst = GetComponent<RedThirst>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.Q);

        protected override void Activate()
        {
            Vector3 origin = transform.position;
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint : Aim.AimPoint;
            Vector3 to = SanguinaryKit.Flat(aim - origin);
            if (to.sqrMagnitude < 0.01f) to = transform.forward;
            direction = to.normalized;
            float distance = LandingSpot.Clear(transform, origin, direction, Mathf.Min(to.magnitude, range));
            speed = distance / flightDuration;
            start = Time.time;
            end = start + flightDuration;
            landing = origin + direction * distance;
            flying = true;

            if (telegraph != null) Destroy(telegraph);
            telegraph = Bullgryn.BullgrynKit.GroundCircle(landing, landingRadius, flightDuration, SanguinaryKit.Gold);
            // Full burn: the flames roar out, a blast of smoke kicks off the ground where he stood.
            var jets = JetPackFx.For(gameObject);
            jets.SetThrust(1f, 0.08f);
            jets.Puff(6, 0.9f);
            Sfx.Play(Sfx.Flamethrower, 0.4f, 1.1f, 1.2f, flightDuration + 0.1f);
            Psyker.PsykerFx.Ring(origin + Vector3.up * 0.1f, 0.3f, 1.8f, 0.3f, JetBlue, 0.18f);
        }

        protected override void Cancel()
        {
            if (flying) Land(false);
            else JetPackFx.For(gameObject).SetThrust(0f, 0.15f);
        }

        protected override void Update()
        {
            base.Update();
            if (!flying) return;

            // Moved directly rather than through the controller so walls don't stop the leap; a replica follows the
            // network position instead.
            if (!IsReplica)
            {
                transform.position += direction * (speed * Time.deltaTime);
                Physics.SyncTransforms();
            }

            float t = Mathf.Clamp01((Time.time - start) / flightDuration);
            SetLift(apexHeight * Height(t));
            if (Time.time >= end) Land(true);
        }

        // 0..1 height over the flight: a fast climb, a short hang at the top, then a hard drop into the slam.
        static float Height(float t)
        {
            const float climb = 0.4f, hang = 0.65f;
            if (t < climb) return 1f - Mathf.Pow(1f - t / climb, 2f);
            if (t < hang) return 1f;
            float d = (t - hang) / (1f - hang);
            return 1f - d * d;
        }

        void SetLift(float height)
        {
            if (model != null) model.localPosition += Vector3.up * (height - liftApplied);
            liftApplied = height;
        }

        void Land(bool strike)
        {
            flying = false;
            SetLift(0f);
            if (telegraph != null) Destroy(telegraph);
            var jets = JetPackFx.For(gameObject);
            jets.SetThrust(0f, 0.25f);
            if (!strike || Health.IsDead) return;
            jets.Puff(5, 1f);

            Vector3 here = transform.position;
            // Every machine draws the impact: a ring, a dust-and-blood burst and cracks that fade from the ground.
            Psyker.PsykerFx.Ring(here + Vector3.up * 0.1f, 0.3f, landingRadius, 0.3f, SanguinaryKit.Gold, 0.22f);
            SanguinaryKit.Mist(here, landingRadius * 0.7f, 0.45f);
            ImpactCracks.Spawn(here, landingRadius, crackSeconds);
            Sfx.Play(Sfx.SlamExplosion, 0.45f, 0.95f, 1.05f, 0.7f);
            Sfx.Play(Sfx.HammerSmash, 0.35f, 0.7f, 0.8f, 0.5f);
            if (IsReplica) return;

            CameraRig.TopDownCamera.Shake(0.15f, 0.2f);
            bool marked = false;
            foreach (Health enemy in Mandrake.MandrakeKit.EnemiesInCone(Health, here, transform.forward, landingRadius, 360f).ToArray())
            {
                float dealt = damage * (thirst != null ? thirst.SlamDamageMultiplier : 1f);
                Hit hit = Hit.Direct(dealt).AsAoe().WithSlow(slowDuration, slow);
                if (GuardUpgrade.Has(gameObject, GuardUpgrade.DeathFromAbove)) hit = hit.WithStun(GuardUpgrade.DeathFromAboveStun);
                enemy.Strike(hit, gameObject, landed =>
                {
                    if (!landed) return;
                    if (energy != null) energy.Gain(energyPerHit);
                    if (marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }
        }

        void OnDestroy()
        {
            if (telegraph != null) Destroy(telegraph);
        }
    }
}
