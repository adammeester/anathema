using System.Linq;
using System.IO;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Abilities.KhorneBerserker;
using Anathema.Abilities.PlagueSurgeon;
using Anathema.Abilities.Psyker;
using Anathema.Abilities.SkitariiRanger;
using Anathema.Arena;
using Anathema.CameraRig;
using Anathema.Combat;
using Anathema.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anathema.EditorTools
{
    // Builds Assets/Scenes/Arena.unity from primitives. Runs automatically once if the scene doesn't exist yet;
    // re-run (and overwrite) from the Anathema menu.
    [InitializeOnLoad]
    public static class ArenaSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string MaterialDir = "Assets/Materials";
        const string GreyKnightModelPath = "Assets/Models/GreyKnight.fbx";
        const string KhorneBerserkerModelPath = "Assets/Models/KhorneBerserker.fbx";
        const string PlagueSurgeonModelPath = "Assets/Models/PlagueSurgeon.fbx";
        const string PsykerModelPath = "Assets/Models/Psyker.fbx";
        const string SkitariiRangerModelPath = "Assets/Models/SkitariiRanger.fbx";
        // First model that exists is used for the player.
        static readonly string[] PlayerModelPaths =
        {
            GreyKnightModelPath,
            "Assets/Models/Inquisitor.fbx"
        };

        static ArenaSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode) Build();
            };
        }

        internal enum Character
        {
            GreyKnight,
            KhorneBerserker,
            PlagueSurgeon,
            Psyker,
            SkitariiRanger
        }

        [MenuItem("Anathema/Rebuild Arena Scene")]
        public static void Build() => Build(Character.GreyKnight);

        [MenuItem("Anathema/Rebuild Arena Scene (Khorne Berserker)")]
        static void BuildBerserker() => Build(Character.KhorneBerserker);

        [MenuItem("Anathema/Rebuild Arena Scene (Plague Surgeon)")]
        static void BuildPlagueSurgeon() => Build(Character.PlagueSurgeon);

        [MenuItem("Anathema/Rebuild Arena Scene (Psyker)")]
        static void BuildPsyker() => Build(Character.Psyker);

        [MenuItem("Anathema/Rebuild Arena Scene (Skitarii Ranger)")]
        static void BuildSkitariiRanger() => Build(Character.SkitariiRanger);

        static void Build(Character character)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            CreateArena();
            new GameObject("Gas Zone").AddComponent<GasZone>();
            new GameObject("Orb Spawner").AddComponent<OrbSpawner>();
            GameObject player = CreatePlayer(character);
            CreateCamera(player.transform);
            GameObject shooter = CreateDummy(new Vector3(10f, 1f, 8f));
            shooter.name = "Training Dummy (Shooter)";
            shooter.AddComponent<DummyShooter>();
            CreateDummy(new Vector3(-10f, 1f, 8f));
            CreateDummy(new Vector3(0f, 1f, 16f));
            CreateDummy(new Vector3(14f, 1f, -6f));
            GameObject mover = CreateDummy(new Vector3(0f, 1f, -12f));
            mover.name = "Training Dummy (Moving)";
            mover.AddComponent<DummyMover>().Configure(Vector3.right, 10f, 4f);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Built {ScenePath}. Press Play.");
        }

        internal static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = new Color(1f, 0.82f, 0.72f);
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Warm, dim ambient to match the red-lit colosseum mood.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.2f, 0.18f);
        }

        internal static void CreateArena()
        {
            // Circular gladiator pit: ringed floor, spiked perimeter wall, glowing barricades, crowd stands.
            const float ArenaRadius = 28f;
            const int WallSegments = 24;

            Material pit = GetMaterial("Pit", new Color(0.07f, 0.05f, 0.05f));
            Material ground = GetMaterial("ArenaFloor", new Color(0.24f, 0.25f, 0.28f));
            Material groundDark = GetMaterial("GroundDark", new Color(0.17f, 0.18f, 0.21f));
            Material groundLight = GetMaterial("GroundLight", new Color(0.5f, 0.5f, 0.54f));
            Material crack = GetMaterial("GroundCrack", new Color(0.13f, 0.14f, 0.16f));
            Material wall = GetMaterial("ArenaWall", new Color(0.2f, 0.21f, 0.25f));
            Material wallDark = GetMaterial("WallDark", new Color(0.1f, 0.1f, 0.12f));
            Material cover = GetMaterial("ArenaCover", new Color(0.3f, 0.32f, 0.38f));
            Material stands = GetMaterial("Stands", new Color(0.28f, 0.1f, 0.07f));
            Material glow = GetUnlitMaterial("Glow", new Color(1f, 0.22f, 0.12f));
            Material ringGlow = GetUnlitMaterial("RingGlow", new Color(0.95f, 0.95f, 1f));

            // Collider floor (also visible as the dark pit outside the arena).
            Box("Ground Collider", new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 130f), pit);

            // Arena floor disc, concentric accent rings, spokes and the central ring.
            Disc("Floor", ArenaRadius * 2f, 0.02f, ground);
            Disc("Floor Ring Outer", 42f, 0.03f, groundDark);
            Disc("Floor Ring Inner", 41f, 0.04f, ground);
            Disc("Floor Ring Mid", 26f, 0.05f, groundDark);
            Disc("Floor Ring Mid Inner", 25.4f, 0.06f, ground);
            for (int i = 0; i < 5; i++)
            {
                var spoke = Box($"Floor Crack {i}", new Vector3(0f, 0.07f, 0f), new Vector3(ArenaRadius * 1.9f, 0.01f, 0.18f), crack);
                spoke.transform.rotation = Quaternion.Euler(0f, i * 36f, 0f);
                RemoveCollider(spoke);
            }
            Disc("Center Ring Outer", 6.2f, 0.08f, ringGlow);
            Disc("Center Ring Gap", 5.9f, 0.09f, groundDark);
            Disc("Center Ring Disc", 3.4f, 0.10f, groundLight);
            Disc("Center Ring Core", 2.5f, 0.11f, groundDark);

            // Perimeter wall: ring of segments, each with a glowing base strip, top trim and blade spikes.
            float chord = 2f * (ArenaRadius + 1f) * Mathf.Tan(Mathf.PI / WallSegments) + 0.25f;
            for (int i = 0; i < WallSegments; i++)
            {
                float angle = i * 360f / WallSegments;
                float yaw = 90f - angle;

                var seg = Box($"Wall {i}", RadialPos(ArenaRadius + 1.1f, angle, 1.5f), new Vector3(chord, 3f, 1.6f), wall);
                seg.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                var trim = Box($"Wall Trim {i}", RadialPos(ArenaRadius + 0.28f, angle, 0.1f), new Vector3(chord, 0.14f, 0.32f), glow);
                trim.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                RemoveCollider(trim);

                var top = Box($"Wall Cap {i}", RadialPos(ArenaRadius + 1.1f, angle, 3.1f), new Vector3(chord, 0.25f, 1.9f), wallDark);
                top.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                RemoveCollider(top);

                var spike = Box($"Wall Spike {i}", RadialPos(ArenaRadius + 1.6f, angle, 4.4f), new Vector3(0.8f, 2.6f, 0.16f), wallDark);
                // Blade faces along the radial direction and leans outward, away from the arena.
                spike.transform.rotation = Quaternion.LookRotation(RadialDir(angle), Vector3.up) * Quaternion.Euler(-14f, 0f, 0f);
                RemoveCollider(spike);
            }

            // Four big barricades near the centre, each with a glowing pylon, facing the middle.
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                var b = Box($"Barricade {i}", RadialPos(11f, angle, 0.9f), new Vector3(6.5f, 1.8f, 1.3f), cover);
                b.transform.rotation = Quaternion.Euler(0f, 90f - angle, 0f);

                var lip = Box($"Barricade Lip {i}", RadialPos(11f, angle, 1.85f), new Vector3(6.9f, 0.15f, 1.6f), wallDark);
                lip.transform.rotation = b.transform.rotation;
                RemoveCollider(lip);

                var edge = Box($"Barricade Glow {i}", RadialPos(10.33f, angle, 0.12f), new Vector3(6.6f, 0.1f, 0.14f), glow);
                edge.transform.rotation = b.transform.rotation;
                RemoveCollider(edge);

                AddPylon($"Pylon {i}", RadialPos(11.7f, angle, 2.0f), angle, wallDark, glow);
            }

            // Four chevron bunkers further out, rotated 45 degrees from the barricades.
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                Vector3 centre = RadialPos(19f, angle, 0f);
                Vector3 tangent = new Vector3(-Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
                Vector3 radial = RadialDir(angle);

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 pos = centre + tangent * (side * 2.4f) - radial * 0.7f + Vector3.up * 0.9f;
                    var arm = Box($"Bunker {i}{(side < 0 ? "a" : "b")}", pos, new Vector3(5f, 1.8f, 1.2f), cover);
                    arm.transform.rotation = Quaternion.Euler(0f, 90f - angle - side * 28f, 0f);

                    var edge = Box($"Bunker Glow {i}{(side < 0 ? "a" : "b")}", pos + Vector3.down * 0.78f - radial * 0.1f, new Vector3(5.1f, 0.1f, 1.3f), glow);
                    edge.transform.rotation = arm.transform.rotation;
                    RemoveCollider(edge);
                }
                AddPylon($"Bunker Pylon {i}", centre + radial * 0.4f, angle, wallDark, glow);
            }

            // Crowd stands: three rising tiers with a scattering of spectators.
            float[] tierRadius = { 33f, 38f, 43f };
            float[] tierHeight = { 4f, 7f, 10f };
            var rng = new System.Random(40);
            Material[] crowd =
            {
                GetMaterial("Crowd A", new Color(0.6f, 0.2f, 0.15f)),
                GetMaterial("Crowd B", new Color(0.55f, 0.45f, 0.3f)),
                GetMaterial("Crowd C", new Color(0.25f, 0.2f, 0.25f)),
            };
            for (int t = 0; t < tierRadius.Length; t++)
            {
                float tierChord = 2f * tierRadius[t] * Mathf.Tan(Mathf.PI / WallSegments) + 0.3f;
                for (int i = 0; i < WallSegments; i++)
                {
                    float angle = i * 360f / WallSegments;
                    var tier = Box($"Stand {t}-{i}", RadialPos(tierRadius[t], angle, tierHeight[t] * 0.5f), new Vector3(tierChord, tierHeight[t], 4.6f), stands);
                    tier.transform.rotation = Quaternion.Euler(0f, 90f - angle, 0f);
                    RemoveCollider(tier);

                    for (int c = 0; c < 4; c++)
                    {
                        float offset = ((float)rng.NextDouble() - 0.5f) * tierChord * 0.85f;
                        float depth = ((float)rng.NextDouble() - 0.5f) * 3.4f;
                        Vector3 tangent = new Vector3(-Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
                        Vector3 pos = RadialPos(tierRadius[t] + depth, angle, tierHeight[t] + 0.45f) + tangent * offset;
                        var fan = Box($"Fan {t}-{i}-{c}", pos, new Vector3(0.6f, 0.9f, 0.5f), crowd[rng.Next(crowd.Length)]);
                        RemoveCollider(fan);
                    }
                }
            }
        }

        static Vector3 RadialDir(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        static Vector3 RadialPos(float radius, float angleDeg, float y)
        {
            Vector3 d = RadialDir(angleDeg) * radius;
            d.y = y;
            return d;
        }

        static void Disc(string name, float diameter, float y, Material material)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            disc.transform.position = new Vector3(0f, y, 0f);
            disc.transform.localScale = new Vector3(diameter, 0.02f, diameter);
            disc.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(disc);
        }

        // Dark spiked pylon with a glowing red crystal and a point light, like the arena's cover markers.
        static void AddPylon(string name, Vector3 position, float angle, Material body, Material glow)
        {
            var root = new GameObject(name);
            root.transform.position = position;

            var post = Box(name + " Post", position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f), body);
            post.transform.SetParent(root.transform, true);
            RemoveCollider(post);

            var crystal = Box(name + " Crystal", position + Vector3.up * 2.0f, new Vector3(0.38f, 0.38f, 0.38f), glow);
            crystal.transform.rotation = Quaternion.Euler(45f, angle, 45f);
            crystal.transform.SetParent(root.transform, true);
            RemoveCollider(crystal);

            var lightGo = new GameObject("Glow Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = Vector3.up * 2.2f;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.25f, 0.12f);
            l.range = 7f;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;
        }

        static void RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        static Material GetUnlitMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(MaterialDir);
            var material = new Material(Shader.Find("Sprites/Default")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static GameObject CreatePlayer(Character character)
        {
            bool berserker = character == Character.KhorneBerserker
                             && AssetDatabase.LoadAssetAtPath<GameObject>(KhorneBerserkerModelPath) != null;

            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            // The Psyker is an ordinary human, much smaller than the marines.
            bool ranger = character == Character.SkitariiRanger;
            bool psyker = character == Character.Psyker;
            bool small = psyker || ranger;
            controller.height = ranger ? 1.9f : psyker ? 1.8f : 2f;
            controller.radius = small ? 0.4f : 0.5f;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            root.AddComponent<Health>().Configure(character == Character.KhorneBerserker ? 150f : ranger ? 105f : psyker ? 100f : 120f, 1);
            root.AddComponent<HealthBar>().Configure(ranger ? 2.5f : psyker ? 2.3f : 2.7f);
            root.AddComponent<Energy>();
            root.AddComponent<EnergyBar>().Configure(ranger ? 2.3f : psyker ? 2.1f : 2.5f);
            root.AddComponent<Respawner>().Configure(3f);
            root.AddComponent<MouseAim>();
            root.AddComponent<PlayerMovement>();
            // Character-specific abilities live in Scripts/Abilities/<Character>/.
            if (berserker)
            {
                // Melee only: the dual chain axes replace the ranged basic attack.
                root.AddComponent<AxeChop>();
                root.AddComponent<WhirlingCarnage>();
                root.AddComponent<AxeRush>();
                root.AddComponent<Maim>();
                root.AddComponent<AxeToss>();
                root.AddComponent<BloodForTheBloodGod>();
                root.AddComponent<BerserkerCounter>();
            }
            else if (character == Character.PlagueSurgeon)
            {
                // Support caster: poison gas and plague flies, no counter (Winged Escape takes the Space slot).
                root.AddComponent<PlagueSpit>();
                root.AddComponent<PlagueCloud>();
                root.AddComponent<PestilentFly>();
                root.AddComponent<WingedEscape>();
                root.AddComponent<WardOfPlague>();
            }
            else if (ranger)
            {
                // Long-range sniper: a quick shot, a chargeable piercing stun shot, shrapnel / shotgun, a disengage and a 4-shot volley.
                root.AddComponent<GalvanicShot>();
                root.AddComponent<TransuranicShot>();
                root.AddComponent<ShrapnelField>();
                root.AddComponent<Disengage>();
                root.AddComponent<RangerCounter>();
                root.AddComponent<CullingVolley>();
            }
            else if (psyker)
            {
                // Glass-cannon caster: ranged bolts, chain lightning, a stunning shot, a damage-absorbing barrier, a blink and a channelled beam ultimate.
                root.AddComponent<WarpBolt>();
                root.AddComponent<Cripple>();
                root.AddComponent<ChainLightning>();
                root.AddComponent<PsychicBarrier>();
                root.AddComponent<WarpStep>();
                root.AddComponent<PsychicBlast>();
            }
            else
            {
                root.AddComponent<BasicAttack>();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(GreyKnightModelPath) != null)
                {
                    root.AddComponent<HalberdSweep>();
                    root.AddComponent<Smite>();
                    root.AddComponent<Consecrate>();
                    root.AddComponent<InterceptorCounter>();
                    root.AddComponent<Purgatus>();
                }
            }
            root.AddComponent<AimIndicator>();
            root.AddComponent<PlayerHud>();

            GameObject modelAsset = null;
            string[] modelPaths = berserker ? new[] { KhorneBerserkerModelPath }
                : character == Character.PlagueSurgeon ? new[] { PlagueSurgeonModelPath }.Concat(PlayerModelPaths).ToArray()
                : psyker ? new[] { PsykerModelPath }.Concat(PlayerModelPaths).ToArray()
                : ranger ? new[] { SkitariiRangerModelPath }.Concat(PlayerModelPaths).ToArray()
                : PlayerModelPaths;
            foreach (string path in modelPaths)
            {
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (modelAsset != null) break;
            }

            if (modelAsset != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.AddComponent<ProceduralWalk>();
                return root;
            }

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.GetComponent<Renderer>().sharedMaterial = GetMaterial("Player", new Color(0.15f, 0.35f, 0.75f));

            // Visible "nose" so facing direction reads clearly.
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing Marker";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.4f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.3f, 0.6f);
            nose.GetComponent<Renderer>().sharedMaterial = GetMaterial("Marker", new Color(0.9f, 0.85f, 0.4f));

            return root;
        }

        internal static void CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.03f, 0.03f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;
            go.AddComponent<AudioListener>();
            go.AddComponent<TopDownCamera>().SetTarget(target);
        }

        static GameObject CreateDummy(Vector3 position)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "Training Dummy";
            dummy.transform.position = position;
            dummy.GetComponent<Renderer>().sharedMaterial = GetMaterial("Dummy", new Color(0.7f, 0.15f, 0.15f));

            dummy.AddComponent<Health>().Configure(100f, 2);
            dummy.AddComponent<HealthBar>().Configure(1.7f);
            dummy.AddComponent<Respawner>().Configure(2f);
            return dummy;
        }

        static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        static Material GetMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultShader
                : Shader.Find("Standard");

            Directory.CreateDirectory(MaterialDir);
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
