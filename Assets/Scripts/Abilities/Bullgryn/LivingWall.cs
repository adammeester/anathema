using System.Collections;
using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // R, ultimate (costs a full energy bar): Living Wall. An outline marks the spot during a short, planted wind-up, then
    // he drives his own slabshield into the ground ahead of him. It throws up a wide arc of force, the same translucent
    // arc Brace shows but far larger, bowed out toward his aim: a barrier that blocks movement and projectiles from both
    // sides for a few seconds. (His grenades arc over it.) Chevrons on the ground in front of the shield show which way
    // it will move. He is not holding the shield while it is planted or sliding (BullgrynWeaponVisual hides it).
    //
    // Shove: press R or right-click again, from any distance, while it stands. After a short warning (the chevrons and
    // arc flash and an arrow shows on the ground) the shield and its arc slide forward the way they were facing when
    // planted. Enemies in its path are struck and pushed along, and anyone pinned against terrain is stunned. One shove.
    //
    // While it stands he has no shield: Brace and Shield Bash can't be used. Stuns, silence, panic and knockback during
    // the wind-up interrupt it (refunded); C cancels the wind-up. Every machine builds its own wall from the cues; the
    // owner's machine resolves the shove's hits.
    public class LivingWall : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float windup = 0.4f;
        [SerializeField] float distanceAhead = 2f;      // the planted shield, ahead of him
        [SerializeField] float width = 4f;              // the arc's span, end to end
        [SerializeField] float arcDegrees = 100f;       // how far it bows
        [SerializeField] float height = 2.3f;
        [SerializeField] float thickness = 0.4f;
        [SerializeField] float lifetime = 6f;
        // The slabshield on its own (Assets/Models/BullgrynShield.fbx, from Art/bullgryn.py); set by the scene builder.
        [SerializeField] GameObject shieldModel;

        [Header("Shove")]
        [SerializeField] float shoveWarning = 0.25f;
        [SerializeField] float shoveDistance = 3f;
        [SerializeField] float shoveDuration = 0.16f;   // a hard launch, ~19 m/s
        [SerializeField] float shoveDamage = 10f;
        [SerializeField] float pinStun = 1.5f;
        [SerializeField] float energyPerHit = 0f;

        Energy energy;
        WallBarrier wall;
        Vector3 plannedCentre;
        Vector3 plannedFacing = Vector3.forward;
        GameObject outline;

        public bool WallStanding => wall != null;
        bool CanShove => wall != null && !wall.Shoved;

        public override string DisplayName => CanShove ? "Shove Wall" : "Living Wall";
        public override string KeyLabel => "R";
        public override bool Available => CanShove
                                          || (energy != null && energy.Current >= energyCost && !Silenced.IsSilenced(gameObject) && !Panicked.IsPanicked(gameObject));
        public override bool RootsMovement => IsWindingUp && !CanShove;

        // Placing winds up; a shove fires at once (its own warning runs on the wall).
        protected override float WindupDuration => CanShove ? 0f : windup;
        protected override Color WindupColor => BullgrynKit.Hazard;

        void Reset()
        {
            cooldown = 0.5f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            if (CanShove) return Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(1);
            if (wall != null || BullgrynKit.Busy(gameObject)) return false;
            if (TryGetComponent(out BullCharge charge) && charge.IsCharging) return false;
            return Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;
        }

        protected override void OnWindupStart()
        {
            Vector3 aim = IsReplica && Health.Net != null ? Health.Net.ReplicaAimPoint - transform.position : Aim.AimDirection;
            plannedFacing = BullgrynKit.FlatAim(aim, transform);
            plannedCentre = transform.position + plannedFacing * distanceAhead;
            if (!IsReplica && energy != null) energy.TrySpend(energyCost);
            ClearOutline();
            outline = new GameObject("Living Wall Outline");
            outline.transform.SetPositionAndRotation(plannedCentre, Quaternion.LookRotation(plannedFacing, Vector3.up));
            float radius = WallBarrier.RadiusFor(width, arcDegrees);
            BlockArc.Create(outline.transform, new Vector3(0f, 0f, -radius), radius, arcDegrees, 0.5f, BullgrynKit.Hazard);
        }

        protected override void OnWindupCancel()
        {
            if (!IsReplica && energy != null) energy.Gain(energyCost);
            ClearOutline();
        }

        void ClearOutline()
        {
            if (outline != null) Destroy(outline);
            outline = null;
        }

        protected override void Activate()
        {
            if (CanShove)
            {
                wall.BeginShove(this);
                return;
            }

            ClearOutline();
            wall = WallBarrier.Spawn(this, plannedCentre, plannedFacing, width, arcDegrees, height, thickness, lifetime, shieldModel);
            Anathema.Abilities.Psyker.PsykerFx.Ring(plannedCentre + Vector3.up * 0.1f, 0.3f, 1.6f, 0.35f, BullgrynKit.Hazard, 0.25f);
            Sfx.Play(Sfx.SlamExplosion, 0.6f, 0.7f, 0.8f, 0.8f);
            Sfx.Play(Sfx.HammerSmash, 0.5f, 0.6f, 0.7f, 0.6f);
        }

        // A wind-up in progress is cancelled by the base class; a planted wall stays up.
        protected override void Cancel()
        {
            ClearOutline();
        }

        internal float ShoveWarning => shoveWarning;
        internal float ShoveDistance => shoveDistance;
        internal float ShoveDuration => shoveDuration;

        // The wall starts to move. The owner's machine strikes everyone in its path and schedules the pin stuns.
        internal void ResolveShove(WallBarrier barrier)
        {
            if (IsReplica || Health == null || Health.IsDead) return;

            Vector3 dir = barrier.Facing;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 front = barrier.transform.position;     // the shield: the arc's foremost point
            float speed = shoveDistance / shoveDuration;
            float halfSpan = barrier.Width * 0.5f;
            var struck = new List<Health>();
            // A Hoarfrost casket in the wall's path is shoved away along it.
            Anathema.Arena.CasketField.Sweep(gameObject, front, front + dir * shoveDistance, halfSpan);

            Vector3 boxCentre = front + dir * (shoveDistance * 0.5f) + Vector3.up;
            var halfExtents = new Vector3(halfSpan + 0.6f, 1.2f, shoveDistance * 0.5f + 1.6f);
            foreach (Collider c in Physics.OverlapBox(boxCentre, halfExtents, Quaternion.LookRotation(dir), ~0, QueryTriggerInteraction.Ignore))
            {
                Health enemy = c.GetComponentInParent<Health>();
                if (enemy == null || enemy.Team == Health.Team || enemy.IsDead || struck.Contains(enemy)) continue;
                Vector3 offset = enemy.transform.position - front;
                float across = Vector3.Dot(offset, right);
                if (Mathf.Abs(across) > halfSpan + 0.5f) continue;
                // How far in front of the arc's surface they stand (the arc curves back toward its ends).
                float gap = Vector3.Dot(offset, dir) - barrier.SurfaceOffset(across);
                if (gap < -0.4f || gap > shoveDistance + 0.6f) continue;
                float push = shoveDistance - gap + 0.9f;
                if (push <= 0.2f) continue;
                struck.Add(enemy);

                float seconds = push / speed;
                bool pinned = BullgrynKit.FindWall(enemy, dir, push, out float wallDistance, out Vector3 impact, barrier.gameObject);
                float firedAt = Time.time;
                Health target = enemy;
                target.Strike(Hit.Direct(shoveDamage).WithKnockback(dir, push, seconds), gameObject, landed =>
                {
                    if (!landed) return;
                    if (energy != null && energyPerHit > 0f) energy.Gain(energyPerHit);
                    MeleeSound.Impact();
                    if (!pinned) return;
                    float reach = seconds * Mathf.Clamp01(wallDistance / push);
                    StartCoroutine(PinLater(target, Mathf.Max(0f, reach - (Time.time - firedAt)), impact));
                });
            }
        }

        IEnumerator PinLater(Health target, float delay, Vector3 impact)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (target == null || target.IsDead) yield break;
            Stunned.Apply(target.gameObject, pinStun);
            Anathema.Abilities.Psyker.PsykerFx.Ring(new Vector3(impact.x, target.transform.position.y, impact.z), 0.2f, 1.8f, 0.3f, BullgrynKit.Hazard, 0.3f);
            Sfx.Play(Sfx.SlamExplosion, 0.55f, 0.75f, 0.85f, 0.6f);
        }
    }

    // The planted slabshield and its arc of force. The arc is a chain of solid segments (blocking movement and
    // projectiles) along a circle whose foremost point is the shield, bowing out toward `Facing`; the translucent
    // BlockArc draws it. Ground chevrons ahead of the shield show where a shove will go. Moves itself when shoved.
    // Built on every machine from the Living Wall cue.
    public class WallBarrier : MonoBehaviour
    {
        const int ColliderSegments = 9;
        const float FadeTime = 0.35f;

        LivingWall owner;
        Health ownerHealth;
        float lifetimeEnd;
        float shoveStart = float.PositiveInfinity;
        float moveStart, moveEnd;
        Vector3 moveFrom, moveTo;
        bool moving;
        bool moveDone;
        float radius;
        BlockArc arc;
        readonly List<Material> materials = new List<Material>();
        readonly List<Renderer> chevrons = new List<Renderer>();

        public Vector3 Facing { get; private set; }
        public float Width { get; private set; }
        public bool Shoved { get; private set; }

        // The circle's radius for an arc spanning `width` end to end over `arcDegrees`.
        public static float RadiusFor(float width, float arcDegrees)
        {
            return width * 0.5f / Mathf.Sin(arcDegrees * 0.5f * Mathf.Deg2Rad);
        }

        // How far the arc's surface sits behind the shield (along Facing, so <= 0) at `across` metres to the side.
        public float SurfaceOffset(float across)
        {
            float x = Mathf.Min(Mathf.Abs(across), radius);
            return -(radius - Mathf.Sqrt(radius * radius - x * x));
        }

        public static WallBarrier Spawn(LivingWall owner, Vector3 shieldPosition, Vector3 facing, float width, float arcDegrees,
            float height, float thickness, float lifetime, GameObject shieldModel)
        {
            var go = new GameObject("Living Wall");
            go.transform.SetPositionAndRotation(shieldPosition, Quaternion.LookRotation(facing, Vector3.up));
            var wall = go.AddComponent<WallBarrier>();
            wall.owner = owner;
            wall.ownerHealth = owner.GetComponent<Health>();
            wall.Facing = facing;
            wall.Width = width;
            wall.lifetimeEnd = Time.time + lifetime;
            wall.Build(arcDegrees, height, thickness, shieldModel);
            return wall;
        }

        void Build(float arcDegrees, float height, float thickness, GameObject shieldModel)
        {
            radius = RadiusFor(Width, arcDegrees);
            var centre = new Vector3(0f, 0f, -radius);

            // Kinematic so moving it keeps the physics scene in step (characters collide with it as it slides).
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // Solid segments along the arc.
            float step = arcDegrees / ColliderSegments;
            float segmentLength = 2f * radius * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad) * 1.08f;
            for (int i = 0; i < ColliderSegments; i++)
            {
                float a = (-arcDegrees * 0.5f + step * (i + 0.5f)) * Mathf.Deg2Rad;
                var seg = new GameObject("Arc Segment");
                seg.transform.SetParent(transform, false);
                seg.transform.localPosition = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius + Vector3.up * (height * 0.5f);
                seg.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                seg.AddComponent<BoxCollider>().size = new Vector3(segmentLength, height, thickness);
            }

            arc = BlockArc.Create(transform, centre, radius, arcDegrees, height, BullgrynKit.Hazard);

            // His own shield, driven into the ground at the arc's head.
            if (shieldModel != null)
            {
                GameObject shield = Instantiate(shieldModel, transform);
                shield.name = "Planted Slabshield";
                // Keep the model root's own rotation and scale: the FBX import stores Blender's Z-up -> Y-up turn there
                // (-90 degrees about X), and replacing it with identity laid the shield flat on the ground.
                shield.transform.localPosition = Vector3.zero;
                shield.transform.localRotation = shieldModel.transform.localRotation;
                shield.transform.localScale = shieldModel.transform.localScale;
                foreach (Collider c in shield.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            else
            {
                // No model assigned (an old scene): a plain slab in its place.
                Material plate = Mat(new Color(0.16f, 0.27f, 0.33f, 1f));
                Part(new Vector3(0f, 0.85f, 0.1f), new Vector3(0.9f, 1.7f, 0.25f), plate);
            }

            // Chevrons on the ground just ahead of the shield, pointing the way a shove will go.
            Material hazard = Mat(BullgrynKit.Hazard);
            for (int i = 0; i < 3; i++)
            {
                float z = 0.7f + i * 0.45f;
                chevrons.Add(Part(new Vector3(-0.17f, 0.05f, z), new Vector3(0.1f, 0.03f, 0.45f), hazard, Quaternion.Euler(0f, 40f, 0f)));
                chevrons.Add(Part(new Vector3(0.17f, 0.05f, z), new Vector3(0.1f, 0.03f, 0.45f), hazard, Quaternion.Euler(0f, -40f, 0f)));
            }
        }

        Material Mat(Color c)
        {
            Material m = VisualUtil.UnlitMaterial(c);
            materials.Add(m);
            return m;
        }

        Renderer Part(Vector3 localPos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(transform, false);
            p.transform.localPosition = localPos;
            p.transform.localRotation = rot ?? Quaternion.identity;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        public void BeginShove(LivingWall by)
        {
            if (Shoved) return;
            Shoved = true;
            shoveStart = Time.time + by.ShoveWarning;
            moveFrom = transform.position;
            moveTo = moveFrom + Facing * by.ShoveDistance;
            moveStart = shoveStart;
            moveEnd = shoveStart + by.ShoveDuration;
            // The ground arrow: the strip the shield will sweep.
            BullgrynKit.GroundLane(moveFrom, Facing, by.ShoveDistance, Width, by.ShoveWarning + by.ShoveDuration, BullgrynKit.Hazard);
            Sfx.Play(Sfx.HammerSmash, 0.4f, 0.8f, 0.9f, 0.4f);
            // A shove started just before the wall would expire still runs to the end.
            lifetimeEnd = Mathf.Max(lifetimeEnd, moveEnd + 0.2f);
        }

        void Update()
        {
            if (Time.time >= lifetimeEnd || owner == null || (ownerHealth != null && ownerHealth.IsDead))
            {
                Destroy(gameObject);
                return;
            }
            arc.SetAlpha(Mathf.Clamp01((lifetimeEnd - Time.time) / FadeTime));

            // Flash the chevrons and the arc during the shove warning.
            if (Shoved && Time.time < shoveStart)
            {
                bool on = Mathf.Repeat(Time.time * 16f, 1f) < 0.5f;
                foreach (Renderer r in chevrons) if (r != null) r.enabled = on;
                if (on) arc.Flash(0.05f);
            }

            if (Shoved && !moving && !moveDone && Time.time >= moveStart)
            {
                moving = true;
                moveDone = true;
                foreach (Renderer r in chevrons) if (r != null) r.enabled = true;
                owner.ResolveShove(this);
                Sfx.Play(Sfx.Fissure, 0.45f, 0.9f, 1f, 0.6f);
            }

            if (moving)
            {
                float t = Mathf.Clamp01((Time.time - moveStart) / Mathf.Max(0.01f, moveEnd - moveStart));
                transform.position = Vector3.Lerp(moveFrom, moveTo, t);
                if (t >= 1f) moving = false;
            }
        }

        void OnDestroy()
        {
            foreach (Material m in materials) if (m != null) Destroy(m);
        }
    }
}
