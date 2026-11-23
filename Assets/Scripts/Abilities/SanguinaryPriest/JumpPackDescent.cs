using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryPriest
{
    // Q: Jump Pack Descent. A short wind-up, then a jump-pack leap to the cursor (clamped to range; over walls, landing
    // short of any he would end up inside). Where he lands:
    //   - enemies within `landingRadius` take damage and are knocked back;
    //   - teammates there are healed, and if any were, they and he get a short barrier.
    // It is his only movement ability, so spending it to dive in leaves him no way out.
    public class JumpPackDescent : Ability
    {
        [SerializeField] float range = 8f;
        [SerializeField] float flightDuration = 0.45f;
        [SerializeField] float apexHeight = 2.2f;
        [SerializeField] float landingRadius = 2.5f;
        [SerializeField] float damage = 10f;
        [SerializeField] float knockbackDistance = 2f;
        [SerializeField] float knockbackDuration = 0.25f;
        [SerializeField] float allyHeal = 12f;
        [SerializeField] float barrier = 10f;
        [SerializeField] float barrierSeconds = 2f;
        [SerializeField] float energyPerHit = 8f;

        Transform model;
        float liftApplied; // height this leap has added to the model; changed by difference so other offsets on it survive
        Energy energy;
        bool flying;
        Vector3 direction;
        float speed;
        float start;
        float end;
        Vector3 landing;
        GameObject telegraph;

        protected override float WindupDuration => 0.2f;
        protected override Color WindupColor => SanguinaryKit.Gold;

        public override string DisplayName => "Jump Pack";
        public override string KeyLabel => "Q";
        public override float MovementRange => range;

        void Reset()
        {
            cooldown = 10f;
        }

        protected override void Awake()
        {
            base.Awake();
            model = transform.Find("Model");
            energy = GetComponent<Energy>();
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
            Sfx.Play(Sfx.Flamethrower, 0.3f, 1.3f, 1.4f, flightDuration + 0.1f);
            Psyker.PsykerFx.Ring(origin + Vector3.up * 0.1f, 0.3f, 1.6f, 0.25f, SanguinaryKit.Gold, 0.15f);
        }

        protected override void Cancel()
        {
            if (flying) Land(false);
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
            SetLift(apexHeight * 4f * t * (1f - t));
            if (Time.time >= end) Land(true);
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
            if (!strike || Health.IsDead) return;

            Vector3 here = transform.position;
            Psyker.PsykerFx.Ring(here + Vector3.up * 0.1f, 0.3f, landingRadius, 0.3f, SanguinaryKit.Gold, 0.22f);
            Sfx.Play(Sfx.SlamExplosion, 0.4f, 1.05f, 1.15f, 0.6f);
            if (IsReplica) return;

            bool marked = false;
            foreach (Health enemy in Mandrake.MandrakeKit.EnemiesInCone(Health, here, transform.forward, landingRadius, 360f).ToArray())
            {
                Vector3 push = SanguinaryKit.Flat(enemy.transform.position - here);
                if (push.sqrMagnitude < 0.0001f) push = direction;
                Hit hit = Hit.Direct(damage).AsAoe().WithKnockback(push.normalized, knockbackDistance, knockbackDuration);
                enemy.Strike(hit, gameObject, landed =>
                {
                    if (!landed) return;
                    if (energy != null) energy.Gain(energyPerHit);
                    if (marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }

            bool anyAlly = false;
            foreach (Health ally in SanguinaryKit.AlliesNear(Health, here, landingRadius, includeSelf: false).ToArray())
            {
                anyAlly = true;
                SanguinaryKit.Heal(gameObject, ally, allyHeal);
                Barrier.Grant(ally.gameObject, barrier, barrierSeconds, barrier, gameObject);
                SanguinaryKit.BloodStream(here, ally.transform);
            }
            if (anyAlly) Barrier.Grant(gameObject, barrier, barrierSeconds, barrier, gameObject);
        }

        void OnDestroy()
        {
            if (telegraph != null) Destroy(telegraph);
        }
    }
}
