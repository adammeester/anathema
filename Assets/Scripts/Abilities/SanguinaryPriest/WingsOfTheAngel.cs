using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryPriest
{
    // R, ultimate (costs a full energy bar): Wings of the Angel. A rooted gather while golden wings unfold, then a leap to
    // the cursor (clamped to range, over walls) that crashes down in a wide ring: teammates inside are healed, enemies
    // take damage and are pushed back. For `ascendDuration` afterwards every Chainsword swing is an Exsanguinating Arc
    // (on a slower cadence) without spending the chalice. Stuns, silence, panic and knockback during the gather
    // interrupt it; C cancels and refunds.
    public class WingsOfTheAngel : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float windup = 0.6f;
        [SerializeField] float range = 9f;
        [SerializeField] float flightDuration = 0.5f;
        [SerializeField] float apexHeight = 3f;
        [SerializeField] float radius = 6f;
        [SerializeField] float allyHeal = 30f;
        [SerializeField] float damage = 20f;
        [SerializeField] float knockbackDistance = 2f;
        [SerializeField] float knockbackDuration = 0.3f;
        [SerializeField] float ascendDuration = 5f;

        Energy energy;
        Transform model;
        float liftApplied; // height this flight has added to the model; changed by difference so other offsets on it survive
        Transform wingLeft, wingRight;
        Material wingMaterial;
        bool gathering;
        bool flying;
        Vector3 direction;
        float speed, flightStart, flightEnd;
        float ascendEnd = float.NegativeInfinity;
        GameObject telegraph;

        public bool Ascended => Time.time < ascendEnd && !Health.IsDead;

        public override string DisplayName => "Wings of the Angel";
        public override string KeyLabel => "R";
        public override float MovementRange => range;
        public override bool EscapesCasket => false; // an ultimate, not a dedicated movement ability
        public override bool RootsMovement => gathering;
        public override bool Available => energy != null && energy.Current >= energyCost && !Silenced.IsSilenced(gameObject) && !Panicked.IsPanicked(gameObject);

        protected override float WindupDuration => windup;
        protected override Color WindupColor => SanguinaryKit.Gold;

        void Reset()
        {
            cooldown = 0.5f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
            model = transform.Find("Model");
            wingMaterial = VisualUtil.UnlitMaterial(SanguinaryKit.Gold, 3001);
            wingLeft = MakeWing(-1f);
            wingRight = MakeWing(1f);
        }

        void OnDestroy()
        {
            if (wingMaterial != null) Destroy(wingMaterial);
            if (telegraph != null) Destroy(telegraph);
        }

        // A wing of four long golden feathers fanning out from the shoulder blades.
        Transform MakeWing(float side)
        {
            var pivot = new GameObject(side < 0f ? "Angel Wing L" : "Angel Wing R").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(side * 0.25f, 1.7f, -0.35f);
            for (int i = 0; i < 4; i++)
            {
                GameObject f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                f.name = "Feather";
                Destroy(f.GetComponent<Collider>());
                var r = f.GetComponent<Renderer>();
                r.sharedMaterial = wingMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.transform.SetParent(pivot, false);
                float length = 1.6f - i * 0.25f;
                float angle = 25f - i * 22f;
                f.transform.localRotation = Quaternion.Euler(0f, 0f, side * angle);
                f.transform.localPosition = f.transform.localRotation * new Vector3(side * length * 0.5f, 0f, 0f);
                f.transform.localScale = new Vector3(length, 0.05f, 0.28f);
            }
            pivot.gameObject.SetActive(false);
            return pivot;
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;

        protected override void OnWindupStart()
        {
            gathering = true;
            ShowWings(true);
            if (!IsReplica && energy != null) energy.TrySpend(energyCost);
            Sfx.Play(Sfx.MagicAttack3, 0.4f, 0.7f, 0.8f, windup + 0.2f);
        }

        protected override void OnWindupCancel()
        {
            gathering = false;
            ShowWings(false);
            if (!IsReplica && energy != null) energy.Gain(energyCost);
        }

        protected override void Activate()
        {
            gathering = false;
            Vector3 origin = transform.position;
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint : Aim.AimPoint;
            Vector3 to = SanguinaryKit.Flat(aim - origin);
            if (to.sqrMagnitude < 0.01f) to = transform.forward;
            direction = to.normalized;
            float distance = LandingSpot.Clear(transform, origin, direction, Mathf.Min(to.magnitude, range));
            speed = distance / flightDuration;
            flightStart = Time.time;
            flightEnd = flightStart + flightDuration;
            flying = true;
            ShowWings(true);

            if (telegraph != null) Destroy(telegraph);
            telegraph = Bullgryn.BullgrynKit.GroundCircle(origin + direction * distance, radius, flightDuration, SanguinaryKit.Gold);
        }

        protected override void Cancel()
        {
            gathering = false;
            if (flying)
            {
                flying = false;
                SetLift(0f);
                if (telegraph != null) Destroy(telegraph);
            }
            if (!Ascended) ShowWings(false);
        }

        protected override void Update()
        {
            base.Update();

            if (flying)
            {
                if (!IsReplica)
                {
                    transform.position += direction * (speed * Time.deltaTime);
                    Physics.SyncTransforms();
                }
                float t = Mathf.Clamp01((Time.time - flightStart) / flightDuration);
                SetLift(apexHeight * 4f * t * (1f - t));
                if (Time.time >= flightEnd) Crash();
            }

            // The wings beat slowly while they are out, and fold away when the ascension ends.
            if (wingLeft.gameObject.activeSelf)
            {
                float beat = Mathf.Sin(Time.time * (flying ? 14f : 4f)) * (flying ? 30f : 10f);
                wingLeft.localRotation = Quaternion.Euler(0f, 20f, 15f + beat);
                wingRight.localRotation = Quaternion.Euler(0f, -20f, -15f - beat);
                if (!gathering && !flying && !Ascended) ShowWings(false);
            }
        }

        void SetLift(float height)
        {
            if (model != null) model.localPosition += Vector3.up * (height - liftApplied);
            liftApplied = height;
        }

        void Crash()
        {
            flying = false;
            SetLift(0f);
            if (telegraph != null) Destroy(telegraph);
            if (Health.IsDead) return;

            ascendEnd = Time.time + ascendDuration;
            Vector3 here = transform.position;
            Psyker.PsykerFx.Ring(here + Vector3.up * 0.1f, 0.5f, radius, 0.45f, SanguinaryKit.Gold, 0.3f);
            Psyker.PsykerFx.Ring(here + Vector3.up * 0.15f, 0.3f, radius * 0.7f, 0.35f, SanguinaryKit.BrightBlood, 0.2f);
            Hospitaller.HospitallerFx.Burst(here, 2f, SanguinaryKit.Gold, 0.5f, 1.6f);
            Sfx.Play(Sfx.SlamExplosion, 0.55f, 0.8f, 0.9f, 0.9f);
            if (IsReplica)
            {
                foreach (Health ally in SanguinaryKit.AlliesNear(Health, here, radius, includeSelf: false).ToArray())
                    SanguinaryKit.BloodStream(here, ally.transform);
                return;
            }

            CameraRig.TopDownCamera.Shake(0.25f, 0.35f);
            bool marked = false;
            foreach (Health enemy in Mandrake.MandrakeKit.EnemiesInCone(Health, here, transform.forward, radius, 360f).ToArray())
            {
                Vector3 push = SanguinaryKit.Flat(enemy.transform.position - here);
                if (push.sqrMagnitude < 0.0001f) push = direction;
                Hit hit = Hit.Direct(damage).AsAoe().WithKnockback(push.normalized, knockbackDistance, knockbackDuration);
                enemy.Strike(hit, gameObject, landed =>
                {
                    if (!landed || marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }
            foreach (Health ally in SanguinaryKit.AlliesNear(Health, here, radius).ToArray())
            {
                SanguinaryKit.Heal(gameObject, ally, allyHeal);
                if (ally != Health) SanguinaryKit.BloodStream(here, ally.transform);
            }
            Anathema.Arena.DestructibleFloor.Slam(gameObject, here, here, 2.5f);
        }

        void ShowWings(bool on)
        {
            if (wingLeft == null) return;
            wingLeft.gameObject.SetActive(on);
            wingRight.gameObject.SetActive(on);
        }
    }
}
