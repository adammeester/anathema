using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Sorcerer
{
    // Passive: Echoes of Prospero. Whenever one of the Sorcerer's Curses of the Rubricae bursts, the sand it releases
    // streams to the teammate nearest the victim (within `radius`; the Sorcerer himself counts) and heals them. Setting
    // curses off early (Bolt of Change, Sinking Sands, Dust Form) turns his damage into healing when it is needed.
    //
    // Runs where the curse lives (the server's copy online, this machine offline); the heal is resolved there, and
    // every other machine is told to draw the stream of sand (NetPlayer.ServerRubricEcho).
    public class EchoesOfProspero : MonoBehaviour
    {
        [SerializeField] float heal = 4f;
        [SerializeField] float radius = 6f;

        Health health;

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void OnEnable() => RubricMark.Burst += OnBurst;
        void OnDisable() => RubricMark.Burst -= OnBurst;

        void OnBurst(Health victim, GameObject source)
        {
            if (source != gameObject || health == null || health.IsDead || victim == null) return;

            Health best = null;
            float bestDistance = radius;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure || h.Team != health.Team) continue;
                Vector3 d = h.transform.position - victim.transform.position;
                d.y = 0f;
                if (d.magnitude > bestDistance) continue;
                bestDistance = d.magnitude;
                best = h;
            }
            if (best == null) return;

            best.Heal(heal, gameObject);
            PlayEcho(victim.transform.position, best.transform);
            if (victim.Net != null && victim.Net.IsSpawned) victim.Net.ServerRubricEcho(best.gameObject);
        }

        // A thin stream of gold sand flowing from the burst to the teammate it heals. Every machine plays it.
        public static void PlayEcho(Vector3 from, Transform to)
        {
            if (to == null) return;
            var go = new GameObject("Echo of Prospero");
            go.AddComponent<Stream>().Init(from + Vector3.up * 1f, to);
        }

        sealed class Stream : MonoBehaviour
        {
            const float Flight = 0.35f;
            const int Grains = 5;

            Vector3 from;
            Transform to;
            float start;
            Transform[] grains;
            Material material;

            public void Init(Vector3 origin, Transform target)
            {
                from = origin;
                to = target;
                start = Time.time;
                material = VisualUtil.UnlitMaterial(SorcererFx.Sand, 3003);
                grains = new Transform[Grains];
                for (int i = 0; i < Grains; i++)
                {
                    GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(g.GetComponent<Collider>());
                    g.GetComponent<Renderer>().sharedMaterial = material;
                    g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    g.transform.localScale = Vector3.one * 0.12f;
                    g.transform.SetParent(transform, false);
                    grains[i] = g.transform;
                }
            }

            void Update()
            {
                if (to == null)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector3 goal = to.position + Vector3.up * 1.2f;
                float age = Time.time - start;
                for (int i = 0; i < grains.Length; i++)
                {
                    // Each grain trails a little behind the one before it, on a gentle arc.
                    float t = Mathf.Clamp01((age - i * 0.04f) / Flight);
                    Vector3 p = Vector3.Lerp(from, goal, t);
                    p.y += 0.8f * 4f * t * (1f - t);
                    grains[i].position = p;
                    grains[i].gameObject.SetActive(t > 0f && t < 1f);
                }
                if (age < Flight + Grains * 0.04f) return;
                Anathema.Abilities.Psyker.PsykerFx.Ring(to.position + Vector3.up * 0.1f, 0.2f, 0.9f, 0.25f, SorcererFx.Sand, 0.1f);
                Destroy(gameObject);
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }
        }
    }
}
