using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Q: Pox Flask. The surgeon lobs a flask of concentrated rot to the cursor (pulled in to max range; it flies over
    // characters and low cover). It shatters in a splash: every enemy inside takes a little damage, gains two stacks of
    // Festering, and has all of its Festering held: each stack is refreshed and lasts a few seconds longer than usual,
    // so the rot stays on while he heals, tends or repositions. It is how he reliably reaches full Festering (where
    // Contagion, Bursting Pustules, Rot Harvest and Leech Draught pay off). No crowd control.
    //
    // Online: every machine replays the flight and the splash from the cue; only the owner's copy strikes and lays the
    // rot (Festering lives on the surgeon's machine and publishes itself).
    public class PoxFlask : Ability
    {
        [SerializeField] float range = 10f;
        [SerializeField] float flightTime = 0.5f;
        [SerializeField] float arcHeight = 2.5f;
        [SerializeField] float splashRadius = 2.5f;
        [SerializeField] float damage = 8f;
        [SerializeField] int stacks = 2;
        [SerializeField] float holdSeconds = 3f;
        [SerializeField] float muzzleHeight = 1.4f;

        static readonly Color FlaskColor = new Color(0.5f, 0.8f, 0.15f, 0.95f);

        Energy energy;

        protected override float WindupDuration => 0.2f;
        protected override Color WindupColor => FlaskColor;

        public override string DisplayName => "Pox Flask";
        public override string KeyLabel => "Q";

        void Reset()
        {
            cooldown = 9f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override void OnWindupStart() => PlagueSurgeonPose.For(gameObject).ReadyThrow(WindupDuration + 0.1f);

        protected override void OnWindupCancel() => PlagueSurgeonPose.For(gameObject).Release();

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.Q);

        protected override void Activate()
        {
            PlagueSurgeonPose.For(gameObject).Throw();

            Vector3 from = transform.position;
            Vector3 to = Aim.AimPoint - from;
            to.y = 0f;
            if (to.magnitude > range) to = to.normalized * range;
            Vector3 landing = new Vector3(from.x + to.x, from.y, from.z + to.z);

            Sfx.Play(Sfx.Any(Sfx.MagicAttack1, Sfx.MagicAttack2), 0.3f, 0.7f, 0.8f);
            Flask.Launch(this, from + Vector3.up * muzzleHeight, landing);
        }

        // Every machine: the splash. Owner only: the damage and the rot.
        void Shatter(Vector3 at)
        {
            Infestation.BurstEffect.Spawn(at, splashRadius);
            Sfx.Play(Sfx.DiseaseBurst, 0.4f, 1.1f, 1.25f, 1.2f);
            if (IsReplica || Health == null || Health.IsDead) return;

            PlagueSpit spit = GetComponent<PlagueSpit>();
            float rotDuration = spit != null ? spit.RotDuration : 2f;
            float rotDamage = spit != null ? spit.RotDamagePerSecond : 1.5f;

            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team) continue;
                Vector3 d = h.transform.position - at;
                d.y = 0f;
                if (d.magnitude > splashRadius) continue;

                Health target = h;
                target.Strike(Hit.Direct(damage).AsAoe(), gameObject, landed =>
                {
                    if (!landed || target == null || target.IsDead) return;
                    HitMarkerSound.Play();
                    Festering rot = Festering.Apply(target.gameObject, gameObject, energy, rotDuration, rotDamage, stacks);
                    if (rot != null) rot.Hold(holdSeconds);
                });
            }
        }

        protected override void Cancel()
        {
            // Nothing to cancel once the flask is in the air (a wind-up in progress is cancelled by the base class).
        }

        // The flask in flight: a small green bottle on a high arc, spinning, landing exactly at `to`.
        sealed class Flask : MonoBehaviour
        {
            PoxFlask owner;
            Vector3 from;
            Vector3 to;
            float start;
            Material material;

            public static void Launch(PoxFlask owner, Vector3 from, Vector3 to)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Pox Flask";
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(0.18f, 0.14f, 0.18f);
                go.transform.position = from;
                var flask = go.AddComponent<Flask>();
                flask.owner = owner;
                flask.from = from;
                flask.to = to;
                flask.start = Time.time;
                flask.material = VisualUtil.UnlitMaterial(FlaskColor);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = flask.material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            void Update()
            {
                if (owner == null)
                {
                    Destroy(gameObject);
                    return;
                }

                float t = Mathf.Clamp01((Time.time - start) / owner.flightTime);
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y += 4f * owner.arcHeight * t * (1f - t);
                transform.position = p;
                transform.Rotate(540f * Time.deltaTime, 0f, 270f * Time.deltaTime, Space.Self);

                if (t < 1f) return;
                owner.Shatter(to);
                Destroy(gameObject);
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }
        }
    }
}
