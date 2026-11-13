using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Arena
{
    // The Crucible's breakable floor. The arena builder lays a square grid of stone plates over a lava bed and hands the
    // breakable ones to this component (grid cell + plate transform each). Ground-slam abilities call Slam(); the plates
    // under the impact crumble away and leave a pit of fire that damages anyone standing in it for the rest of the
    // round (or, with regenerate on, until the lava crusts over and the plate re-forms). Everything resets with the gas every round.
    //
    // Online the slam is reported by the caster's machine to the server (NetPlayer.SendFloorBreak), which picks the
    // plates from its own copy of the floor and tells every machine which ones broke, so all machines agree. Damage
    // goes through Health.TakeDamage like the gas, so only the server's copy actually applies it.
    public class DestructibleFloor : MonoBehaviour
    {
        public static DestructibleFloor Instance { get; private set; }

        [SerializeField] float spacing = 2.4f;
        [SerializeField] Transform[] tiles;
        [SerializeField] Vector2Int[] cells;

        [Header("Fire")]
        [SerializeField] float firePerSecond = 10f;
        [SerializeField] float tickInterval = 0.5f;
        [SerializeField] float igniteDelay = 0.4f;      // after the plate breaks: the pit glows but doesn't burn yet
        [SerializeField] bool regenerate = false;       // off: a broken plate stays a fire pit until the round resets
        [SerializeField] float regenerateAfter = 18f;   // with regenerate on, the plate is whole again this long after breaking
        [SerializeField] float coolDuration = 2.5f;     // the last part of that: the lava crusts over and stops burning

        // When a broken plate comes back (never, unless regenerate is on); the lava crusts over coolDuration before that.
        float RegenerateAt => regenerate ? regenerateAfter : float.PositiveInfinity;

        const float CrumbleTime = 0.35f;
        const float RiseTime = 0.4f;
        const float SinkDepth = 0.7f;
        const float MaxLights = 10;
        // A plate breaks if its centre is within the slam's radius plus this (so a slam always takes the plate it lands on).
        const float BreakSlack = 0.25f;

        // The lava surface sits between the glowing bed (top 0.05) and the plates' top (0.08), so it reads as a hole.
        const float LavaTop = 0.07f;
        const int FlamesPerPit = 5;
        const int EmbersPerPit = 4;
        const float EmberRise = 2.4f;    // metres an ember climbs before it burns out
        const float EmberPeriod = 1.6f;  // seconds per climb

        static readonly int HeatId = Shader.PropertyToID("_Heat");

        readonly Dictionary<Vector2Int, int> lookup = new Dictionary<Vector2Int, int>();
        readonly List<int> scratch = new List<int>();
        readonly List<(Light light, float bornAt)> lights = new List<(Light, float)>();

        float[] brokenAt;
        Vector3[] restPosition;
        Quaternion[] restRotation;
        Vector3[] restScale;
        Pit[] pits;
        Material lavaMaterial, flameMaterial, tipMaterial, emberMaterial;
        bool ownsLavaMaterial;
        MaterialPropertyBlock heatBlock;
        float tickTimer;

        // The fire showing in one broken plate's hole: the lava surface, flames bobbing on it and embers rising off it.
        class Pit
        {
            public GameObject root;
            public Renderer lava;
            public Transform[] flames;
            public float[] flameSize;
            public Transform[] embers;
            public float[] flamePhase;
            public float[] emberPhase;  // 0..1
        }

        public int TileCount => tiles != null ? tiles.Length : 0;

        // Editor (arena builder): the breakable plates, their grid cells and the grid spacing (cell (0,0) is centred on this object).
        public void Configure(float gridSpacing, Transform[] plates, Vector2Int[] plateCells)
        {
            spacing = gridSpacing;
            tiles = plates;
            cells = plateCells;
        }

        void Awake()
        {
            Instance = this;
            int n = TileCount;
            brokenAt = new float[n];
            restPosition = new Vector3[n];
            restRotation = new Quaternion[n];
            restScale = new Vector3[n];
            pits = new Pit[n];
            for (int i = 0; i < n; i++)
            {
                brokenAt[i] = -1f;
                if (tiles[i] == null) continue;
                lookup[cells[i]] = i;
                restPosition[i] = tiles[i].localPosition;
                restRotation[i] = tiles[i].localRotation;
                restScale[i] = tiles[i].localScale;
            }
            // The procedural lava shader (Resources/Arena), shared by every pit; each pit sets its own heat.
            lavaMaterial = Resources.Load<Material>("Arena/CrucibleLava");
            if (lavaMaterial == null || !lavaMaterial.shader.isSupported)
            {
                lavaMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.38f, 0.05f));
                ownsLavaMaterial = true;
            }
            heatBlock = new MaterialPropertyBlock();
            // Same look as the Psyker's fire pool: orange flames with brighter yellow tips.
            flameMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.45f, 0.08f, 0.8f), 3002);
            tipMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.9f, 0.4f, 0.9f), 3003);
            emberMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.75f, 0.3f, 1f), 3004);
            GasZone.Restarted += ResetAll;
        }

        void OnDestroy()
        {
            GasZone.Restarted -= ResetAll;
            if (Instance == this) Instance = null;
            foreach (Material m in new[] { flameMaterial, tipMaterial, emberMaterial, ownsLavaMaterial ? lavaMaterial : null })
                if (m != null) Destroy(m);
        }

        // ---- Breaking ----------------------------------------------------------------------------------------------

        // Abilities call this where they hit the ground: everything within `radius` of the segment from..to (a point
        // when they are the same). Only the caster's own machine reports it (replicas are ignored), and nothing breaks in prep.
        public static void Slam(GameObject source, Vector3 from, Vector3 to, float radius)
        {
            DestructibleFloor floor = Instance;
            if (floor == null || source == null || NetGame.InPrep) return;
            if (NetPlayer.IsReplicaObject(source)) return;

            if (source.TryGetComponent(out NetPlayer player) && player.IsSpawned)
            {
                player.SendFloorBreak(from, to, radius);
                return;
            }
            floor.Break(floor.TilesIn(from, to, radius));
        }

        // The intact plates within `radius` of the segment from..to.
        public int[] TilesIn(Vector3 from, Vector3 to, float radius)
        {
            scratch.Clear();
            Vector2 a = new Vector2(from.x, from.z) - Origin;
            Vector2 b = new Vector2(to.x, to.z) - Origin;
            float reach = radius + BreakSlack;
            for (int i = 0; i < TileCount; i++)
            {
                if (tiles[i] == null || brokenAt[i] >= 0f) continue;
                Vector2 c = (Vector2)cells[i] * spacing;
                if (DistanceToSegment(c, a, b) <= reach) scratch.Add(i);
            }
            return scratch.ToArray();
        }

        public void Break(int[] indices)
        {
            if (indices == null || indices.Length == 0) return;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (int i in indices)
            {
                if (i < 0 || i >= TileCount || tiles[i] == null || brokenAt[i] >= 0f) continue;
                brokenAt[i] = Time.time;
                ShowPit(i, true);
                sum += tiles[i].position;
                count++;
            }
            if (count == 0) return;

            Vector3 centre = sum / count;
            AddLight(centre);
            Sfx.Play(Sfx.Fissure, 0.35f, 0.8f, 0.9f, 1.2f);
        }

        // Back to a whole floor (round start, rematch, offline T).
        public void ResetAll()
        {
            for (int i = 0; i < TileCount; i++)
                if (brokenAt[i] >= 0f) Restore(i);
            foreach (var entry in lights)
                if (entry.light != null) Destroy(entry.light.gameObject);
            lights.Clear();
            tickTimer = 0f;
        }

        void Restore(int i)
        {
            brokenAt[i] = -1f;
            Transform t = tiles[i];
            t.localPosition = restPosition[i];
            t.localRotation = restRotation[i];
            t.localScale = restScale[i];
            t.gameObject.SetActive(true);
            ShowPit(i, false);
        }

        // ---- Fire --------------------------------------------------------------------------------------------------

        // True while the hole at this world position is burning.
        public bool IsBurning(Vector3 position)
        {
            Vector2 local = new Vector2(position.x, position.z) - Origin;
            var cell = new Vector2Int(Mathf.RoundToInt(local.x / spacing), Mathf.RoundToInt(local.y / spacing));
            return lookup.TryGetValue(cell, out int i) && Burning(i);
        }

        bool Burning(int i)
        {
            if (brokenAt[i] < 0f) return false;
            float age = Time.time - brokenAt[i];
            return age >= igniteDelay && age < RegenerateAt - coolDuration;
        }

        void Update()
        {
            AnimateTiles();
            AnimateLights();

            if (NetGame.InPrep || NetGame.Frozen) return;
            tickTimer += Time.deltaTime;
            if (tickTimer < tickInterval) return;
            tickTimer -= tickInterval;

            float amount = firePerSecond * tickInterval;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.transform.position.y > 1.5f) continue;
                if (IsBurning(h.transform.position)) h.TakeDamage(amount, gameObject, true, dot: true);
            }
        }

        // ---- Visuals -----------------------------------------------------------------------------------------------

        Vector2 Origin => new Vector2(transform.position.x, transform.position.z);

        void AnimateTiles()
        {
            float now = Time.time;
            for (int i = 0; i < TileCount; i++)
            {
                if (brokenAt[i] < 0f) continue;
                float age = now - brokenAt[i];
                Transform t = tiles[i];

                if (age >= RegenerateAt)
                {
                    Restore(i);
                    continue;
                }

                // The plate drops into the lava, tipping and shrinking, then the hole is open until it rises again.
                float rise = RegenerateAt - age;
                if (age < CrumbleTime || rise < RiseTime)
                {
                    float k = age < CrumbleTime ? age / CrumbleTime : 1f - rise / RiseTime;
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    bool falling = age < CrumbleTime;
                    float tilt = falling ? Mathf.Sin(i * 12.9898f) * 14f * k : 0f;
                    t.localPosition = restPosition[i] + Vector3.down * SinkDepth * k;
                    t.localRotation = restRotation[i] * Quaternion.Euler(tilt, 0f, -tilt * 0.6f);
                    t.localScale = Vector3.Scale(restScale[i], new Vector3(1f - 0.25f * k, 1f, 1f - 0.25f * k));
                }
                else if (t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                }

                AnimatePit(i, age);
            }
        }

        void AnimatePit(int i, float age)
        {
            Pit pit = pits[i];
            if (pit == null) return;

            // Heat: 2 (white-hot as it opens, the warning before it burns) -> 1 (burning) -> 0 (crusted over, regenerate only).
            float coolStart = RegenerateAt - coolDuration;
            float heat, fire;
            if (age < igniteDelay)
            {
                float k = age / igniteDelay;
                heat = 2f - k;
                fire = k;
            }
            else if (age < coolStart)
            {
                heat = 1f;
                fire = 1f;
            }
            else
            {
                heat = 1f - Mathf.Clamp01((age - coolStart) / coolDuration);
                fire = heat;
            }
            heatBlock.SetFloat(HeatId, heat);
            pit.lava.SetPropertyBlock(heatBlock);

            float now = Time.time;
            // Flames bob and swell like the Psyker's fire pool, so they read from the top-down camera.
            for (int f = 0; f < pit.flames.Length; f++)
            {
                float t = now * 7f + pit.flamePhase[f];
                float size = pit.flameSize[f];
                float h = size * (0.6f + 0.6f * Mathf.Abs(Mathf.Sin(t)) + 0.2f * Mathf.Sin(t * 2.3f)) * fire;
                Transform flame = pit.flames[f];
                bool lit = h > 0.02f;
                if (flame.gameObject.activeSelf != lit) flame.gameObject.SetActive(lit);
                if (!lit) continue;
                Vector3 p = flame.localPosition;
                flame.localScale = new Vector3(size * 0.6f * fire, Mathf.Max(0.01f, h * 1.8f), size * 0.6f * fire);
                flame.localPosition = new Vector3(p.x, LavaTop + h * 0.9f, p.z);
            }

            // Embers drift up off the lava and burn out.
            for (int e = 0; e < pit.embers.Length; e++)
            {
                Transform ember = pit.embers[e];
                bool lit = fire > 0.05f;
                if (ember.gameObject.activeSelf != lit) ember.gameObject.SetActive(lit);
                if (!lit) continue;
                float phase = pit.emberPhase[e];
                float k = Mathf.Repeat(now / EmberPeriod + phase, 1f);
                float a = phase * 6.283f;
                float r = spacing * 0.3f * (0.4f + 0.6f * Mathf.Repeat(phase * 7.3f, 1f));
                float sway = Mathf.Sin(now * 2.5f + phase * 20f) * 0.25f * k;
                ember.localPosition = new Vector3(Mathf.Cos(a) * r + sway, LavaTop + k * EmberRise, Mathf.Sin(a) * r);
                ember.localScale = Vector3.one * (0.13f * (1f - k) * fire + 0.01f);
            }
        }

        void ShowPit(int i, bool show)
        {
            if (!show)
            {
                if (pits[i] != null) pits[i].root.SetActive(false);
                return;
            }

            Pit pit = pits[i] ?? (pits[i] = BuildPit(i));
            pit.root.SetActive(true);
        }

        Pit BuildPit(int i)
        {
            var pit = new Pit();
            pit.root = new GameObject($"Fire Pit {i}");
            pit.root.transform.SetParent(transform, false);
            Vector3 rest = restPosition[i];
            pit.root.transform.localPosition = new Vector3(rest.x, 0f, rest.z);

            GameObject lava = Part(pit, PrimitiveType.Cube, "Lava", lavaMaterial);
            lava.transform.localPosition = new Vector3(0f, LavaTop - 0.01f, 0f);
            lava.transform.localScale = new Vector3(spacing, 0.02f, spacing); // covers the glowing seams around the hole
            pit.lava = lava.GetComponent<Renderer>();

            // Flames scattered over the hole (a fixed pattern per plate, so every machine draws the same pit).
            var rng = new System.Random(i * 7919 + 17);
            pit.flames = new Transform[FlamesPerPit];
            pit.flameSize = new float[FlamesPerPit];
            pit.flamePhase = new float[FlamesPerPit];
            for (int f = 0; f < FlamesPerPit; f++)
            {
                GameObject flame = Part(pit, PrimitiveType.Sphere, "Flame", f % 3 == 0 ? tipMaterial : flameMaterial);
                float x = ((float)rng.NextDouble() - 0.5f) * spacing * 0.75f;
                float z = ((float)rng.NextDouble() - 0.5f) * spacing * 0.75f;
                flame.transform.localPosition = new Vector3(x, LavaTop, z);
                pit.flames[f] = flame.transform;
                pit.flameSize[f] = 0.4f + (float)rng.NextDouble() * 0.3f;
                pit.flamePhase[f] = (float)rng.NextDouble() * 10f;
            }

            pit.embers = new Transform[EmbersPerPit];
            pit.emberPhase = new float[EmbersPerPit];
            for (int e = 0; e < EmbersPerPit; e++)
            {
                pit.embers[e] = Part(pit, PrimitiveType.Sphere, "Ember", emberMaterial).transform;
                pit.emberPhase[e] = (float)rng.NextDouble();
            }
            return pit;
        }

        static GameObject Part(Pit pit, PrimitiveType type, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(pit.root.transform, false);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        void AddLight(Vector3 centre)
        {
            if (lights.Count >= MaxLights)
            {
                if (lights[0].light != null) Destroy(lights[0].light.gameObject);
                lights.RemoveAt(0);
            }
            var go = new GameObject("Fire Pit Light");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(centre.x, 1.2f, centre.z);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.45f, 0.12f);
            light.range = 7f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            // The fire crackles while the pit burns (one loop per slam, capped with the lights).
            AudioSource crackle = Sfx.Loop(Sfx.GroundFire, go.transform, 0.12f, Random.Range(0.92f, 1.08f), 0.3f);
            if (crackle != null && crackle.clip != null) crackle.time = Random.Range(0f, crackle.clip.length * 0.8f);
            lights.Add((light, Time.time));
        }

        void AnimateLights()
        {
            for (int k = lights.Count - 1; k >= 0; k--)
            {
                (Light light, float bornAt) = lights[k];
                float age = Time.time - bornAt;
                if (light == null || age >= RegenerateAt)
                {
                    if (light != null) Destroy(light.gameObject);
                    lights.RemoveAt(k);
                    continue;
                }
                float coolStart = RegenerateAt - coolDuration;
                float flash = age < igniteDelay ? Mathf.Lerp(3f, 1.6f, age / igniteDelay) : 1.6f;
                float fade = age > coolStart ? 1f - (age - coolStart) / coolDuration : 1f;
                float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 11f + k);
                light.intensity = flash * fade * flicker;
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
