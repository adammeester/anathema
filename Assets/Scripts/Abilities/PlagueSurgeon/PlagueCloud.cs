using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Press E to vent a restorative cloud at the cursor (clamped to range). It lingers on the ground and heals every ally
    // standing in it (including the surgeon); the poison is halved, so enemies still suffer but it's mainly sustain.
    // EX (hold Shift, costs energy): a full-strength plague cloud that poisons every enemy standing in it, tick by tick.
    // Pressing C during the short wind-up cancels it (refunding the cooldown and any EX energy); once vented the
    // cloud is on the ground and can't be recalled.
    public class PlagueCloud : Ability
    {
        [SerializeField] float range = 10f;
        [SerializeField] float radius = 3f;
        [SerializeField] float lifetime = 5f;
        [SerializeField] float damagePerSecond = 9f;
        [UnityEngine.Serialization.FormerlySerializedAs("exDamageMultiplier")]
        [SerializeField] float healCloudDamageMultiplier = 0.5f;
        [UnityEngine.Serialization.FormerlySerializedAs("exHealPerSecond")]
        [SerializeField] float healPerSecond = 10f;
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float energyPerTick = 2f;

        Energy energy;

        protected override float WindupDuration => 0.5f;
        protected override Color WindupColor => ExActivation ? new Color(0.45f, 0.8f, 0.15f, 1f) : new Color(0.55f, 1f, 0.7f, 1f);

        // The EX energy is spent when the wind-up starts and given back if it is cancelled.
        protected override void OnWindupStart()
        {
            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);
            PlagueSurgeonPose.For(gameObject).Conjure(WindupDuration + 0.1f);
        }

        protected override void OnWindupCancel()
        {
            PlagueSurgeonPose.For(gameObject).Release();
            if (ExActivation && !IsReplica && energy != null) energy.Gain(exEnergyCost);
        }

        public override string DisplayName => "Plague Cloud";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExDescription => "Damage cloud";
        public override string ExName => "Virulent Cloud";

        // Applied only when added in the editor.
        void Reset()
        {
            cooldown = 7f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered()
        {
            return Input.GetKeyDown(KeyCode.E);
        }

        protected override void Activate()
        {
            PlagueSurgeonPose.For(gameObject).Conjure(0.6f);

            Vector3 origin = transform.position;
            Vector3 to = Aim.AimPoint - origin;
            to.y = 0f;
            if (to.magnitude > range) to = to.normalized * range;
            Vector3 center = new Vector3(origin.x + to.x, origin.y, origin.z + to.z);

            var go = new GameObject(ExActivation ? "Plague Cloud" : "Restorative Cloud");
            go.transform.position = center;
            var cloud = go.AddComponent<GasCloud>();
            cloud.Init(
                gameObject,
                Health.Team,
                IsReplica,
                radius,
                lifetime,
                damagePerSecond * (ExActivation ? 1f : healCloudDamageMultiplier),
                ExActivation ? 0f : healPerSecond,
                energyPerTick,
                energy,
                ExActivation);
        }

        protected override void Cancel()
        {
            // Nothing to cancel: the cloud is instant.
        }

        // The cloud on the ground. Ticks twice a second: enemies inside are damaged, allies inside are healed (EX only).
        // On a replica of the surgeon it is purely visual; the owner's machine does the damage and healing.
        class GasCloud : MonoBehaviour
        {
            const float TickInterval = 0.5f;
            const int Puffs = 10;
            const float GroundOffset = 0.06f;

            static readonly Color PoisonColor = new Color(0.45f, 0.8f, 0.15f, 0.35f);
            static readonly Color HealColor = new Color(0.55f, 1f, 0.7f, 0.35f);

            GameObject owner;
            int team;
            bool visualOnly;
            float radius;
            float endTime;
            float damagePerTick;
            float healPerTick;
            float energyPerTick;
            Energy energy;
            float nextTick;
            Material material;
            Transform disc;
            Transform[] puffs;
            float[] puffPhase;
            Vector2[] puffOffset;

            public void Init(GameObject ownerObject, int ownerTeam, bool visual, float cloudRadius, float lifetime,
                float dps, float hps, float energyGain, Energy ownerEnergy, bool ex)
            {
                owner = ownerObject;
                team = ownerTeam;
                visualOnly = visual;
                radius = cloudRadius;
                endTime = Time.time + lifetime;
                damagePerTick = dps * TickInterval;
                healPerTick = hps * TickInterval;
                energyPerTick = energyGain;
                energy = ownerEnergy;
                nextTick = Time.time + TickInterval;
                BuildVisuals(ex ? PoisonColor : HealColor);
            }

            void BuildVisuals(Color color)
            {
                material = VisualUtil.UnlitMaterial(color);

                GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                d.name = "Cloud Disc";
                Destroy(d.GetComponent<Collider>());
                d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.GetComponent<Renderer>().sharedMaterial = material;
                d.transform.SetParent(transform, false);
                d.transform.localPosition = new Vector3(0f, GroundOffset, 0f);
                d.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
                disc = d.transform;

                puffs = new Transform[Puffs];
                puffPhase = new float[Puffs];
                puffOffset = new Vector2[Puffs];
                for (int i = 0; i < Puffs; i++)
                {
                    GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    p.name = "Puff";
                    Destroy(p.GetComponent<Collider>());
                    p.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    p.GetComponent<Renderer>().sharedMaterial = material;
                    p.transform.SetParent(transform, false);
                    puffs[i] = p.transform;
                    puffPhase[i] = Random.value * 6.28f;
                    puffOffset[i] = Random.insideUnitCircle * (radius * 0.75f);
                }
            }

            void Update()
            {
                float remaining = endTime - Time.time;
                if (remaining <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }

                AnimateVisuals(remaining);

                if (visualOnly || Anathema.Net.NetGame.Frozen) return;
                while (Time.time >= nextTick)
                {
                    nextTick += TickInterval;
                    Tick();
                }
            }

            void AnimateVisuals(float remaining)
            {
                float fade = Mathf.Clamp01(remaining / 0.5f);
                Color c = material.color;
                c.a = Mathf.Lerp(0f, 0.35f, fade);
                material.color = c;

                for (int i = 0; i < puffs.Length; i++)
                {
                    float t = Time.time * 0.8f + puffPhase[i];
                    float size = 1.1f + 0.4f * Mathf.Sin(t * 1.7f);
                    puffs[i].localScale = Vector3.one * size;
                    puffs[i].localPosition = new Vector3(
                        puffOffset[i].x + Mathf.Sin(t) * 0.3f,
                        0.5f + 0.25f * Mathf.Sin(t * 1.3f),
                        puffOffset[i].y + Mathf.Cos(t) * 0.3f);
                }
            }

            void Tick()
            {
                foreach (Health h in FindObjectsByType<Health>())
                {
                    if (h.IsDead) continue;

                    Vector3 offset = h.transform.position - transform.position;
                    offset.y = 0f;
                    if (offset.magnitude > radius) continue;

                    if (h.Team != team)
                    {
                        if (damagePerTick > 0f && h.TakeDamage(damagePerTick, owner, true, dot: true) && energy != null)
                            energy.Gain(energyPerTick);
                    }
                    else if (healPerTick > 0f)
                    {
                        Healing.Apply(h, healPerTick);
                    }
                }
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }
        }
    }
}
