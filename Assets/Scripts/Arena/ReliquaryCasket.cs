using System.Collections.Generic;
using Anathema.Abilities.Psyker;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Arena
{
    // One frozen reliquary on the Hoarfrost Basilica: a block of ice with a saint's casket inside, standing on a plinth.
    // At rest it is solid cover (its collider blocks movement and shots, and wall-slam abilities treat it as a wall).
    // CasketField launches it; this component only moves and draws it, identically on every machine:
    //   Resting -> Sliding (towards End at a constant speed; its collider is off so it never blocks anyone)
    //   -> Stopped (reached End; waits for the field to resolve the impact) -> Gone (shattered) -> Resting (restored).
    // End can move while it slides (a catch shortens the slide, an escape lengthens it again); it always lies on the
    // same straight line, so the casket simply keeps going towards wherever End is now.
    public class ReliquaryCasket : MonoBehaviour
    {
        public enum State { Resting, Sliding, Stopped, Gone }

        public const float HalfSize = 0.7f;   // footprint half-width (the collider is 1.4 x 1.1 x 1.4)

        [SerializeField] Transform body;      // everything visible (spun while sliding)

        public State Current { get; private set; } = State.Resting;
        public Vector3 Direction { get; private set; }
        public Vector3 End { get; private set; }
        public float Speed { get; private set; }
        public Vector3 Position => transform.position;
        // How far it still has to go to End.
        public float Remaining => Current == State.Sliding || Current == State.Stopped ? Vector3.Distance(Flat(transform.position), Flat(End)) : 0f;

        Vector3 restPosition;
        Quaternion restRotation;
        Collider[] solids;
        float spin;
        float chipTimer;
        Transform trail;
        Vector3 trailStart;
        Material trailMaterial, chipMaterial, shardMaterial;

        readonly List<Debris> debris = new List<Debris>();

        struct Debris
        {
            public Transform t;
            public Vector3 velocity;
            public Vector3 spin;
            public float bornAt, life;
            public bool gravity;
        }

        static readonly Color Frost = new Color(0.75f, 0.92f, 1f, 0.9f);

        public void Configure(Transform visual) => body = visual;

        void Awake() => Init();

        // CasketField can reset the caskets (GasZone.Restarted) before their own Awake has run, so every entry point
        // makes sure the rest pose and colliders are known first.
        bool initialised;

        void Init()
        {
            if (initialised) return;
            initialised = true;
            restPosition = transform.position;
            restRotation = transform.rotation;
            solids = GetComponents<Collider>();
            trailMaterial = VisualUtil.UnlitMaterial(new Color(0.85f, 0.95f, 1f, 0.55f), 3001);
            chipMaterial = VisualUtil.UnlitMaterial(new Color(0.92f, 0.97f, 1f, 0.95f), 3002);
            shardMaterial = VisualUtil.UnlitMaterial(new Color(0.7f, 0.88f, 1f, 0.8f), 3002);
        }

        void OnDestroy()
        {
            foreach (Material m in new[] { trailMaterial, chipMaterial, shardMaterial })
                if (m != null) Destroy(m);
        }

        // ---- State changes (called by CasketField on every machine) ------------------------------------------------

        public void Launch(Vector3 start, Vector3 direction, Vector3 end, float speed)
        {
            Init();
            ClearTrail();
            Direction = Flat(direction).normalized;
            Speed = speed;
            transform.position = new Vector3(start.x, restPosition.y, start.z);
            End = new Vector3(end.x, restPosition.y, end.z);
            SetSolid(false);
            Current = State.Sliding;
            spin = Random.Range(-1f, 1f) < 0f ? -220f : 220f;

            // A pale score cut into the ice behind it, stretched as it goes.
            trailStart = transform.position;
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(t.GetComponent<Collider>());
            t.name = "Casket Trail";
            t.GetComponent<Renderer>().sharedMaterial = trailMaterial;
            trail = t.transform;
            UpdateTrail();

            PsykerFx.Ring(transform.position, 0.3f, 2.2f, 0.3f, Frost, 0.25f);
            Sfx.Play(Sfx.HammerSmash, 0.55f, 0.7f, 0.8f, 0.6f);
            Sfx.Play(Sfx.Fissure, 0.35f, 1.5f, 1.65f, 1.2f); // the scrape of stone on ice
        }

        // A new end point on the same line (a catch, a release after an escape).
        public void Retarget(Vector3 end)
        {
            Init();
            if (Current != State.Sliding && Current != State.Stopped) return;
            End = new Vector3(end.x, restPosition.y, end.z);
            // Already past it (this machine ran ahead of the server): settle where the server says.
            if (Vector3.Dot(Flat(End - transform.position), Direction) < 0f) transform.position = End;
            if (Remaining <= 0.01f || Current == State.Sliding) return;
            // Stopped against the victim's wall, then they escaped: carry on to the real end, trail and all.
            Current = State.Sliding;
            trailFadeStart = -1f;
            Color c = trailMaterial.color;
            c.a = 0.55f;
            trailMaterial.color = c;
        }

        public void Shatter()
        {
            Init();
            if (Current == State.Gone || Current == State.Resting) return;
            Current = State.Gone;
            SetSolid(false);
            if (body != null) body.gameObject.SetActive(false);
            Vector3 at = transform.position;

            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Random.insideUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.8f + 0.3f;
                Spawn(at + Vector3.up * Random.Range(0.3f, 1f), Random.Range(0.12f, 0.35f), shardMaterial,
                    d.normalized * Random.Range(3f, 7f) + Direction * 2f, Random.Range(0.6f, 1.1f), true);
            }
            PsykerFx.Ring(at, 0.4f, 3.2f, 0.4f, Frost, 0.35f);
            PsykerFx.Ring(at, 0.2f, 2f, 0.3f, new Color(1f, 1f, 1f, 0.9f), 0.2f);
            Sfx.Play(Sfx.SlamExplosion, 0.6f, 1.15f, 1.3f, 0.8f);
            Sfx.Play(Sfx.Impact, 0.6f, 0.8f, 0.9f);
        }

        // Back on the plinth (round reset, respawn).
        // `regrow`: the ice visibly re-forms on the plinth (a respawn mid-round) instead of just being there (round start).
        public void Restore(bool regrow = false)
        {
            Init();
            ClearTrail();
            Current = State.Resting;
            transform.SetPositionAndRotation(restPosition, restRotation);
            if (body != null)
            {
                body.gameObject.SetActive(true);
                body.localRotation = Quaternion.identity;
                body.localScale = Vector3.one;
            }
            SetSolid(true);
            growStart = -1f;
            if (!regrow) return;

            growStart = Time.time;
            if (body != null) body.localScale = new Vector3(0.2f, 0.05f, 0.2f);
            PsykerFx.Ring(restPosition, 2.4f, 0.4f, RegrowTime, Frost, 0.25f);   // frost drawn in onto the plinth
            PsykerFx.Ring(restPosition, 0.3f, 2.6f, 0.45f, new Color(1f, 1f, 1f, 0.8f), 0.2f);
            Sfx.Play(Sfx.MagicImpact, 0.4f, 1.3f, 1.45f, 0.8f);
        }

        const float RegrowTime = 0.9f;
        float growStart = -1f;

        // The ice block growing back up out of the plinth.
        void AnimateRegrow()
        {
            if (growStart < 0f || body == null) return;
            float t = Mathf.Clamp01((Time.time - growStart) / RegrowTime);
            float ease = 1f - (1f - t) * (1f - t);
            float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
            body.localScale = new Vector3(Mathf.Lerp(0.2f, 1f, ease) * overshoot, Mathf.Lerp(0.05f, 1f, ease), Mathf.Lerp(0.2f, 1f, ease) * overshoot);
            if (t < 1f) return;
            body.localScale = Vector3.one;
            growStart = -1f;
        }

        public Vector3 RestPosition
        {
            get
            {
                Init();
                return restPosition;
            }
        }

        // ---- Motion ------------------------------------------------------------------------------------------------

        void Update()
        {
            AnimateDebris();
            AnimateRegrow();
            if (trail != null) FadeTrail();
            if (Current != State.Sliding) return;

            float step = Speed * Time.deltaTime;
            float left = Remaining;
            if (step >= left)
            {
                transform.position = End;
                Current = State.Stopped;
                // The thud of hitting the wall; the shatter follows when the field resolves the impact.
                PsykerFx.Ring(transform.position + Direction * HalfSize, 0.2f, 1.8f, 0.25f, Frost, 0.25f);
                Sfx.Play(Sfx.MeleeImpact, 0.5f, 0.6f, 0.7f);
            }
            else transform.position += Direction * step;

            if (body != null) body.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
            spin = Mathf.MoveTowards(spin, Mathf.Sign(spin) * 60f, 200f * Time.deltaTime);
            UpdateTrail();

            // Ice chips sprayed off the leading corners.
            chipTimer -= Time.deltaTime;
            if (chipTimer > 0f) return;
            chipTimer = 0.025f;
            Vector3 right = Vector3.Cross(Vector3.up, Direction);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = transform.position + Direction * HalfSize + right * (s * HalfSize) + Vector3.up * 0.1f;
                Vector3 v = right * (s * Random.Range(2f, 4f)) + Vector3.up * Random.Range(1.5f, 3.5f) + Direction * Random.Range(1f, 4f);
                Spawn(p, Random.Range(0.05f, 0.12f), chipMaterial, v, Random.Range(0.3f, 0.5f), true);
            }
        }

        void UpdateTrail()
        {
            if (trail == null) return;
            Vector3 a = Flat(trailStart), b = Flat(transform.position);
            float length = Vector3.Distance(a, b);
            trail.position = (a + b) * 0.5f + Vector3.up * 0.095f; // just above the floor plates
            trail.rotation = Quaternion.LookRotation(Direction, Vector3.up);
            trail.localScale = new Vector3(HalfSize * 1.6f, 0.01f, Mathf.Max(0.01f, length));
        }

        float trailFadeStart = -1f;

        void FadeTrail()
        {
            if (Current == State.Sliding) return;
            if (trailFadeStart < 0f) trailFadeStart = Time.time;
            float k = 1f - (Time.time - trailFadeStart) / 4f;
            if (k <= 0f)
            {
                ClearTrail();
                return;
            }
            trail.localScale = new Vector3(trail.localScale.x * Mathf.Lerp(0.999f, 1f, k), trail.localScale.y, trail.localScale.z);
            Color c = trailMaterial.color;
            c.a = 0.55f * k;
            trailMaterial.color = c;
        }

        void ClearTrail()
        {
            if (trail != null) Destroy(trail.gameObject);
            trail = null;
            trailFadeStart = -1f;
            if (trailMaterial != null)
            {
                Color c = trailMaterial.color;
                c.a = 0.55f;
                trailMaterial.color = c;
            }
        }

        void SetSolid(bool on)
        {
            foreach (Collider c in solids)
                if (c != null) c.enabled = on;
        }

        // ---- Debris ------------------------------------------------------------------------------------------------

        void Spawn(Vector3 at, float size, Material material, Vector3 velocity, float life, bool gravity)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.name = "Ice";
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.position = at;
            go.transform.localScale = Vector3.one * size;
            go.transform.rotation = Random.rotation;
            debris.Add(new Debris { t = go.transform, velocity = velocity, spin = Random.insideUnitSphere * 720f, bornAt = Time.time, life = life, gravity = gravity });
        }

        void AnimateDebris()
        {
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                Debris d = debris[i];
                float age = Time.time - d.bornAt;
                if (d.t == null || age >= d.life)
                {
                    if (d.t != null) Destroy(d.t.gameObject);
                    debris.RemoveAt(i);
                    continue;
                }
                if (d.gravity) d.velocity += Physics.gravity * Time.deltaTime;
                Vector3 p = d.t.position + d.velocity * Time.deltaTime;
                if (p.y < 0.1f)
                {
                    p.y = 0.1f;
                    d.velocity = new Vector3(d.velocity.x * 0.5f, -d.velocity.y * 0.3f, d.velocity.z * 0.5f);
                }
                d.t.position = p;
                d.t.Rotate(d.spin * Time.deltaTime, Space.Self);
                float k = 1f - age / d.life;
                d.t.localScale = Vector3.one * Mathf.Max(0.01f, d.t.localScale.x * Mathf.Lerp(0.97f, 1f, k));
                debris[i] = d;
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
