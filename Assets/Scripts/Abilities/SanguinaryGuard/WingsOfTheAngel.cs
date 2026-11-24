using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.SanguinaryGuard
{
    // F, ultimate (costs a full energy bar): Wings of the Angel. A rooted gather while his own wings spread and blaze gold,
    // then a leap to the cursor (clamped to range, over walls) that crashes down in a wide ring: teammates inside are
    // healed, enemies take damage and are pushed back. For `ascendDuration` afterwards every Chainsword swing is an
    // Exsanguinating Arc (on a slower cadence) without spending the chalice. Stuns, silence, panic and knockback during
    // the gather interrupt it; C cancels and refunds.
    //
    // Blazing wings: the model's wings are skinned to Wing_L / Wing_R and use their own "wing" / "wing_tip" materials
    // (Art/sanguinary_guard.py). While the ultimate is out those materials are swapped for glowing gold, the wings lift
    // and beat (fast in flight, slow while ascended), a warm light shines behind him and golden motes drift off them.
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

        [Header("Blazing wings")]
        [SerializeField] float wingSpread = 28f;      // degrees each wing lifts while blazing
        [SerializeField] float wingBeat = 8f;         // degrees of the slow beat (three times that in flight)
        [SerializeField] float motesPerSecond = 14f;

        static readonly Color WingRest = new Color(0.93f, 0.93f, 0.92f, 1f);
        static readonly Color WingTipRest = new Color(0.72f, 0.84f, 0.9f, 1f);
        static readonly Color WingBlaze = new Color(1f, 0.74f, 0.22f, 1f);
        static readonly Color WingTipBlaze = new Color(1f, 0.93f, 0.62f, 1f);
        LimbPose wingPose;
        Transform wingL, wingR;
        float sideL, sideR;                   // +1 for a wing on his right, -1 on his left (what lifts it)
        bool wingsPosed;
        SkinnedMeshRenderer body;
        Material[] restMaterials, blazeMaterials;
        Material blazeMaterial, blazeTipMaterial, moteMaterial;
        int firstWingSlot = -1;
        float blaze;                          // 0..1, eased toward the target each frame
        Light wingLight;
        float moteDebt;

        Energy energy;
        Transform model;
        float liftApplied; // height this flight has added to the model; changed by difference so other offsets on it survive
        bool gathering;
        bool flying;
        Vector3 direction;
        float speed, flightStart, flightEnd;
        float ascendEnd = float.NegativeInfinity;
        GameObject telegraph;

        // Radiance: a warm-white sunburst behind and above his golden halo, out only while the ultimate is. It grows in
        // over the gather, flares on the crash and fades over the last of the ascension.
        static readonly Color Radiance = new Color(1f, 0.95f, 0.8f, 1f);
        const float RadianceTilt = 25f;      // leaned back so it still reads from the top-down camera
        const float RadianceFadeOut = 0.6f;
        Transform head;
        Transform sunburst;
        Material sunburstMaterial, sunburstDiscMaterial;
        float sunburstScale;                  // eased toward the target each frame
        float flareAt = float.NegativeInfinity;

        public bool Ascended => Time.time < ascendEnd && !Health.IsDead;

        public override string DisplayName => "Wings of the Angel";
        public override string KeyLabel => "F";
        public override float MovementRange => range;
        public override bool CarriesCharacter => flying;   // the flight carries the character over walls itself
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
            if (model != null) head = FindDeep(model, "Head");
            sunburst = MakeSunburst();
            SetUpWings();
        }

        // Finds the wing bones and the wing materials on his body, and builds the gold ones swapped in while blazing.
        void SetUpWings()
        {
            if (model == null) return;
            wingPose = new LimbPose(model, "Wing_L", "Wing_R");
            wingL = wingPose.Get("Wing_L");
            wingR = wingPose.Get("Wing_R");
            sideL = SideOf(wingL);
            sideR = SideOf(wingR);

            // Depth-first, so the real body comes before copies of the model (the counter's glow shell).
            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.StartsWith("wing")) { body = r; break; }
                if (body != null) break;
            }
            if (body == null) return;

            blazeMaterial = VisualUtil.UnlitMaterial(WingBlaze);
            blazeTipMaterial = VisualUtil.UnlitMaterial(WingTipBlaze);
            restMaterials = body.sharedMaterials;
            blazeMaterials = (Material[])restMaterials.Clone();
            for (int i = 0; i < restMaterials.Length; i++)
            {
                string n = restMaterials[i] != null ? restMaterials[i].name : "";
                if (n.StartsWith("wing_tip")) blazeMaterials[i] = blazeTipMaterial;
                else if (n.StartsWith("wing")) blazeMaterials[i] = blazeMaterial;
                else continue;
                if (firstWingSlot < 0) firstWingSlot = i;
            }

            var lightGo = new GameObject("Wing Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, -0.9f);
            wingLight = lightGo.AddComponent<Light>();
            wingLight.type = LightType.Point;
            wingLight.color = new Color(1f, 0.78f, 0.35f);
            wingLight.range = 6f;
            wingLight.shadows = LightShadows.None;
            wingLight.enabled = false;
        }

        float SideOf(Transform bone)
        {
            if (bone == null) return 0f;
            return Vector3.Dot(bone.position - transform.position, transform.right) >= 0f ? 1f : -1f;
        }

        // Every frame: ease the blaze toward on/off, then colour, spread and beat the wings, light them and shed motes.
        void UpdateWings()
        {
            bool on = !Health.IsDead && (gathering || flying || Ascended);
            float target = on ? 1f : 0f;
            if (on && Ascended && !flying) target = Mathf.Clamp01((ascendEnd - Time.time) / RadianceFadeOut);
            blaze = Mathf.MoveTowards(blaze, target, Time.deltaTime * (target > blaze ? 3f : 2f));

            // Colour: swap the wing materials in while blazing, unless something else (a ghost look) owns the body.
            if (body != null && firstWingSlot >= 0)
            {
                Material current = body.sharedMaterials[firstWingSlot];
                bool ours = current == restMaterials[firstWingSlot] || current == blazeMaterials[firstWingSlot];
                if (ours)
                {
                    bool want = blaze > 0.01f;
                    if (want != (current == blazeMaterials[firstWingSlot]))
                        body.sharedMaterials = want ? blazeMaterials : restMaterials;
                }
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
                blazeMaterial.color = Color.Lerp(WingRest, WingBlaze * pulse + Color.white * (1f - pulse) * 0.4f, blaze);
                blazeTipMaterial.color = Color.Lerp(WingTipRest, WingTipBlaze, blaze);
            }

            if (wingLight != null)
            {
                wingLight.enabled = blaze > 0.01f;
                wingLight.intensity = blaze * (2.2f + 0.4f * Mathf.Sin(Time.time * 6f));
            }

            if (blaze > 0.01f && wingL != null) ShedMotes();
        }

        void LateUpdate()
        {
            UpdateSunburst();
            PoseWings();
        }

        // Lift and beat. Runs after the walk cycle; the sword's pose only touches the spine and arms, so the wings keep
        // their own rotation on top of whatever the spine does.
        void PoseWings()
        {
            if (wingPose == null || wingL == null) return;
            if (blaze <= 0.001f)
            {
                if (wingsPosed) wingPose.Reset();
                wingsPosed = false;
                return;
            }
            float beat = Mathf.Sin(Time.time * (flying ? 14f : 4f)) * (flying ? wingBeat * 3f : wingBeat);
            float lift = (wingSpread + beat) * blaze;
            wingPose.Reset();
            wingPose.Rotate("Wing_L", transform.forward, sideL * lift);
            wingPose.Rotate("Wing_R", transform.forward, sideR * lift);
            wingsPosed = true;
        }

        // Golden motes drifting off the wings and falling away.
        void ShedMotes()
        {
            moteDebt += motesPerSecond * blaze * Time.deltaTime;
            if (moteMaterial == null) moteMaterial = VisualUtil.UnlitMaterial(WingTipBlaze, 3001);
            while (moteDebt >= 1f)
            {
                moteDebt -= 1f;
                Transform bone = Random.value < 0.5f ? wingL : wingR;
                if (bone == null) continue;
                float side = bone == wingL ? sideL : sideR;
                Vector3 spot = bone.position + transform.right * (side * Random.Range(0.1f, 0.9f))
                               + Vector3.up * Random.Range(-0.3f, 0.8f) - transform.forward * Random.Range(0.05f, 0.3f);
                WingMote.Spawn(spot, moteMaterial);
            }
        }

        // A small golden speck that drifts down and out, shrinking away. Every machine.
        sealed class WingMote : MonoBehaviour
        {
            Vector3 velocity;
            float born, life, size;

            public static void Spawn(Vector3 at, Material material)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Wing Mote";
                Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.position = at;
                var m = go.AddComponent<WingMote>();
                m.size = Random.Range(0.05f, 0.1f);
                m.life = Random.Range(0.6f, 1.1f);
                m.born = Time.time;
                m.velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.9f, -0.3f), Random.Range(-0.4f, 0.4f));
                go.transform.localScale = Vector3.one * m.size;
            }

            void Update()
            {
                float t = (Time.time - born) / life;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.position += velocity * Time.deltaTime;
                transform.localScale = Vector3.one * (size * (1f - t));
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // A soft disc with sixteen rays fanning out of it, long and short alternating. Built flat in the local XY plane
        // (facing along the character's back) and placed every LateUpdate.
        Transform MakeSunburst()
        {
            sunburstMaterial = VisualUtil.UnlitMaterial(Radiance, 3000);
            sunburstDiscMaterial = VisualUtil.UnlitMaterial(new Color(Radiance.r, Radiance.g, Radiance.b, 0.35f), 2999);
            var root = new GameObject("Angel Radiance").transform;
            root.SetParent(transform, false);

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Radiance Disc";
            Destroy(disc.GetComponent<Collider>());
            var dr = disc.GetComponent<Renderer>();
            dr.sharedMaterial = sunburstDiscMaterial;
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            disc.transform.SetParent(root, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(0.9f, 0.005f, 0.9f);

            const int Rays = 16;
            for (int i = 0; i < Rays; i++)
            {
                GameObject ray = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ray.name = "Ray";
                Destroy(ray.GetComponent<Collider>());
                var r = ray.GetComponent<Renderer>();
                r.sharedMaterial = sunburstMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ray.transform.SetParent(root, false);
                float length = i % 2 == 0 ? 0.95f : 0.6f;
                float inner = 0.35f;
                var spin = Quaternion.Euler(0f, 0f, i * 360f / Rays);
                ray.transform.localRotation = spin;
                ray.transform.localPosition = spin * new Vector3(0f, inner + length * 0.5f, 0f);
                ray.transform.localScale = new Vector3(i % 2 == 0 ? 0.07f : 0.05f, length, 0.01f);
            }

            root.gameObject.SetActive(false);
            return root;
        }

        void UpdateSunburst()
        {
            if (sunburst == null) return;
            bool on = !Health.IsDead && (gathering || flying || Ascended);
            float target = 0f;
            if (on)
            {
                // Grows over the gather, full in flight and during the ascension, shrinking away at the end.
                target = gathering ? 0.85f : 1f;
                if (Ascended) target *= Mathf.Clamp01((ascendEnd - Time.time) / RadianceFadeOut);
                float sinceFlare = Time.time - flareAt;
                if (sinceFlare < 0.4f) target += 0.6f * (1f - sinceFlare / 0.4f);
            }
            sunburstScale = Mathf.MoveTowards(sunburstScale, target, Time.deltaTime * (target > sunburstScale ? 2.5f : 3f));
            bool visible = sunburstScale > 0.01f;
            if (sunburst.gameObject.activeSelf != visible) sunburst.gameObject.SetActive(visible);
            if (!visible) return;

            // Behind and above the golden halo; anchored to the head bone so it rides the flight lift.
            Vector3 back = -transform.forward;
            Vector3 anchor = head != null ? head.position : (model != null ? model.position : transform.position) + Vector3.up * 2f;
            sunburst.position = anchor + Vector3.up * 0.45f + back * 0.55f;
            sunburst.rotation = Quaternion.LookRotation(back, Vector3.up) * Quaternion.Euler(RadianceTilt, 0f, Time.time * 20f);
            sunburst.localScale = Vector3.one * sunburstScale * 1.3f;

            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 5f);
            sunburstMaterial.color = new Color(Radiance.r, Radiance.g, Radiance.b, pulse);
        }

        void OnDestroy()
        {
            if (sunburstMaterial != null) Destroy(sunburstMaterial);
            if (sunburstDiscMaterial != null) Destroy(sunburstDiscMaterial);
            if (body != null && restMaterials != null && firstWingSlot >= 0 && body.sharedMaterials[firstWingSlot] == blazeMaterials[firstWingSlot])
                body.sharedMaterials = restMaterials;
            if (blazeMaterial != null) Destroy(blazeMaterial);
            if (blazeTipMaterial != null) Destroy(blazeTipMaterial);
            if (moteMaterial != null) Destroy(moteMaterial);
            if (telegraph != null) Destroy(telegraph);
        }


        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.F) && energy != null && energy.Current >= energyCost;

        protected override void OnWindupStart()
        {
            gathering = true;
            if (!IsReplica && energy != null) energy.TrySpend(energyCost);
            Sfx.Play(Sfx.MagicAttack3, 0.4f, 0.7f, 0.8f, windup + 0.2f);
        }

        protected override void OnWindupCancel()
        {
            gathering = false;
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

            UpdateWings();
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

            ascendEnd = Time.time + (GuardUpgrade.Has(gameObject, GuardUpgrade.SeraphicAscension) ? GuardUpgrade.SeraphicSeconds : ascendDuration);
            flareAt = Time.time;
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
    }
}
