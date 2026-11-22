using System.Collections.Generic;
using Anathema.Abilities.Bullgryn;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // E: Sinking Sands. The Sorcerer lobs an hourglass of cursed sand in a slow, high arc to the cursor (it flies over
    // characters and cover; a ring on the ground marks where it will land). It shatters for area damage and the ground
    // swallows everyone inside: they are stopped dead and the snare wears off over a second. Any Curse of the Rubricae on
    // them is set off at once.
    // Echoing Hourglass (upgrade): for a few seconds after a throw, E again lobs a lesser hourglass: a smaller area and less
    // damage, the same snare, and it lays the curse instead of setting it off. It doesn't touch the main cooldown.
    // Shifting Dunes (upgrade): every enemy hit gives Mending Sands a bonus charge.
    public class SinkingSands : Ability
    {
        [SerializeField] float damage = 18f;
        [SerializeField] float radius = 2.3f;
        [SerializeField] float minRange = 2f;
        [SerializeField] float maxRange = 10f;
        [SerializeField] float flightTime = 0.95f;
        [SerializeField] float windup = 0.25f;
        [SerializeField] float snareSeconds = 1f;
        [SerializeField] float energyPerHit = 6f;
        [SerializeField] float markDamage = 10f;
        [SerializeField] float markSeconds = 3.5f;

        Energy energy;
        float echoFrom = float.PositiveInfinity;     // the lesser hourglass can be thrown from then... (owner)
        float echoUntil = float.NegativeInfinity;    // ...until then
        float lastMainThrow = float.NegativeInfinity; // replicas tell the lesser throw from the main one by this
        float mainCooldownEnd;

        bool EchoReady => Time.time >= echoFrom && Time.time < echoUntil;

        public override string DisplayName => EchoReady ? "Lesser Sands" : "Sinking Sands";
        public override string KeyLabel => "E";

        // The lesser hourglass is thrown at once, whatever the cooldown says.
        protected override bool HasSpareCharge => EchoReady;
        protected override float WindupDuration => EchoReady || (IsReplica && IsEchoCue) ? 0f : windup;
        protected override Color WindupColor => SorcererFx.Sand;

        // Replica: the main throw can't come round again inside its own cooldown, so a cue soon after one is the echo.
        bool IsEchoCue => Time.time - lastMainThrow < windup + SorcererUpgrade.EchoDelay + SorcererUpgrade.EchoWindow + 0.3f;

        void Reset()
        {
            cooldown = 7f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.E);

        protected override void Activate()
        {
            bool echo = IsReplica ? IsEchoCue : EchoReady;

            Vector3 offset = Aim.AimPoint - transform.position;
            offset.y = 0f;
            float distance = Mathf.Clamp(offset.magnitude, minRange, maxRange);
            Vector3 dir = offset.sqrMagnitude < 0.0001f ? transform.forward : offset.normalized;
            Vector3 target = transform.position + dir * distance;
            float area = echo ? SorcererUpgrade.EchoRadius : radius;

            if (echo)
            {
                echoFrom = float.PositiveInfinity;
                echoUntil = float.NegativeInfinity;
                lastMainThrow = float.NegativeInfinity;
                if (!IsReplica) SetCooldownRemaining(mainCooldownEnd - Time.time); // the echo doesn't restart it
            }
            else
            {
                lastMainThrow = Time.time;
                mainCooldownEnd = Time.time + cooldown * CooldownScale;
                if (!IsReplica && SorcererUpgrade.Has(gameObject, SorcererUpgrade.EchoingHourglass))
                {
                    // A short pause before the second throw is ready.
                    echoFrom = Time.time + SorcererUpgrade.EchoDelay;
                    echoUntil = echoFrom + SorcererUpgrade.EchoWindow;
                }
            }

            Vector3 hand = transform.position + Vector3.up * 1.7f + transform.right * 0.4f + dir * 0.5f;
            BullgrynKit.GroundCircle(target, area, flightTime, SorcererFx.Sand);
            Sfx.Play(Sfx.MagicAttack2, 0.3f, 0.75f, 0.85f, 0.6f);
            bool owner = !IsReplica;
            var go = new GameObject(echo ? "Lesser Hourglass" : "Sinking Sands Hourglass");
            go.AddComponent<Hourglass>().Launch(hand, target, flightTime, echo ? 0.65f : 1f, () => Burst(target, area, echo, owner));
        }

        void Burst(Vector3 centre, float area, bool echo, bool owner)
        {
            Anathema.Abilities.Psyker.PsykerFx.Ring(centre + Vector3.up * 0.1f, 0.3f, area, 0.3f, SorcererFx.Sand, 0.3f);
            Anathema.Abilities.Psyker.PsykerFx.Ring(centre + Vector3.up * 0.12f, area, 0.3f, 0.6f, new Color(0.6f, 0.45f, 0.2f, 0.8f), 0.2f);
            Sfx.Play(Sfx.Impact, 0.45f, 0.75f, 0.85f, 0.5f);
            Sfx.Play(Sfx.Fissure, 0.35f, 1.15f, 1.3f, 0.7f); // the ground cracking open and swallowing
            if (!owner || this == null || Health == null) return;
            // The hourglass breaks the floor where it lands, like a ground slam (the Crucible's plates, the Hoarfrost ice).
            Anathema.Arena.DestructibleFloor.Crack(gameObject, centre, centre, Mathf.Min(area, 4f));

            bool dunes = SorcererUpgrade.Has(gameObject, SorcererUpgrade.ShiftingDunes);
            var mending = GetComponent<MendingSands>();
            bool marked = false;
            var struck = new List<Health>();
            foreach (Health enemy in FindObjectsByType<Health>())
            {
                if (enemy.IsDead || enemy.Team == Health.Team || struck.Contains(enemy)) continue;
                Vector3 to = enemy.transform.position - centre;
                to.y = 0f;
                if (to.magnitude > area + 0.3f) continue; // a little allowance for the target's body
                struck.Add(enemy);

                // The main hourglass sets the curse off; the lesser one lays it.
                Hit blast = Hit.Direct(echo ? SorcererUpgrade.EchoDamage : damage).AsAoe().With(HitRider.FadingSnare(snareSeconds, 1f))
                    .With(echo ? HitRider.RubricCurse(markDamage, markSeconds) : HitRider.RubricBurst());
                enemy.Strike(blast, gameObject, landed =>
                {
                    if (!landed) return;
                    if (energy != null) energy.Gain(energyPerHit);
                    if (dunes && mending != null) mending.AddBonusCharge();
                    if (marked) return;
                    marked = true;
                    HitMarkerSound.Play();
                });
            }
        }

        protected override void Cancel()
        {
        }

        // The hourglass in flight: two gold bulbs of sand on a slow, high arc, turning end over end. Purely visual.
        sealed class Hourglass : MonoBehaviour
        {
            const float ApexHeight = 4f;

            Vector3 from, to;
            float start, duration;
            System.Action onLand;
            Material frame, sand;

            public void Launch(Vector3 a, Vector3 b, float seconds, float size, System.Action landed)
            {
                transform.localScale = Vector3.one * size;
                from = a;
                to = b + Vector3.up * 0.2f;
                start = Time.time;
                duration = Mathf.Max(0.05f, seconds);
                onLand = landed;
                frame = VisualUtil.UnlitMaterial(new Color(0.85f, 0.65f, 0.25f, 1f));
                sand = VisualUtil.UnlitMaterial(new Color(0.4f, 0.8f, 1f, 0.8f));
                Part(PrimitiveType.Sphere, sand, new Vector3(0f, 0.18f, 0f), new Vector3(0.3f, 0.22f, 0.3f));
                Part(PrimitiveType.Sphere, sand, new Vector3(0f, -0.18f, 0f), new Vector3(0.3f, 0.22f, 0.3f));
                Part(PrimitiveType.Cylinder, frame, new Vector3(0f, 0.34f, 0f), new Vector3(0.36f, 0.03f, 0.36f));
                Part(PrimitiveType.Cylinder, frame, new Vector3(0f, -0.34f, 0f), new Vector3(0.36f, 0.03f, 0.36f));
                Part(PrimitiveType.Cylinder, frame, new Vector3(0.16f, 0f, 0f), new Vector3(0.03f, 0.34f, 0.03f));
                Part(PrimitiveType.Cylinder, frame, new Vector3(-0.16f, 0f, 0f), new Vector3(0.03f, 0.34f, 0.03f));
                transform.position = from;
            }

            void Part(PrimitiveType type, Material m, Vector3 local, Vector3 scale)
            {
                GameObject p = GameObject.CreatePrimitive(type);
                Destroy(p.GetComponent<Collider>());
                p.GetComponent<Renderer>().sharedMaterial = m;
                p.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.transform.SetParent(transform, false);
                p.transform.localPosition = local;
                p.transform.localScale = scale;
            }

            void Update()
            {
                float t = Mathf.Clamp01((Time.time - start) / duration);
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y += ApexHeight * 4f * t * (1f - t);
                transform.position = p;
                transform.Rotate(0f, 0f, 400f * Time.deltaTime, Space.Self);
                if (t < 1f) return;
                onLand?.Invoke();
                Destroy(gameObject);
            }

            void OnDestroy()
            {
                if (frame != null) Destroy(frame);
                if (sand != null) Destroy(sand);
            }
        }
    }
}
