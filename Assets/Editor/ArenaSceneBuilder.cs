using System.Linq;
using System.IO;
using Anathema.Abilities.Bullgryn;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Abilities.Hospitaller;
using Anathema.Abilities.KhorneBerserker;
using Anathema.Abilities.Mandrake;
using Anathema.Abilities.PlagueSurgeon;
using Anathema.Abilities.Psyker;
using Anathema.Abilities.SkitariiRanger;
using Anathema.Abilities.Sorcerer;
using Anathema.Abilities.SanguinaryPriest;
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
        const string HospitallerModelPath = "Assets/Models/Hospitaller.fbx";
        const string MandrakeModelPath = "Assets/Models/Mandrake.fbx";
        const string BullgrynModelPath = "Assets/Models/Bullgryn.fbx";
        const string SorcererModelPath = "Assets/Models/Sorcerer.fbx";
        // Until his own model exists the Grey Knight body stands in (its halberd carries the blade glow).
        const string SanguinaryPriestModelPath = "Assets/Models/SanguinaryPriest.fbx";
        const string BullgrynShieldModelPath = "Assets/Models/BullgrynShield.fbx"; // planted by Living Wall
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
            SkitariiRanger,
            Hospitaller,
            Mandrake,
            Bullgryn,
            Sorcerer,
            SanguinaryPriest
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

        [MenuItem("Anathema/Rebuild Arena Scene (Hospitaller)")]
        static void BuildHospitaller() => Build(Character.Hospitaller);

        [MenuItem("Anathema/Rebuild Arena Scene (Mandrake)")]
        static void BuildMandrake() => Build(Character.Mandrake);

        [MenuItem("Anathema/Rebuild Arena Scene (Bullgryn)")]
        static void BuildBullgryn() => Build(Character.Bullgryn);

        [MenuItem("Anathema/Rebuild Arena Scene (Thousand Sons Sorcerer)")]
        static void BuildSorcerer() => Build(Character.Sorcerer);

        [MenuItem("Anathema/Rebuild Arena Scene (Sanguinary Priest)")]
        static void BuildSanguinaryPriest() => Build(Character.SanguinaryPriest);

        // The training ground on the Crucible map (breakable floor), in its own scene so Arena.unity stays as it is.
        // Slams that break the floor: the Berserker's R, and the Grey Knight's Q while the Daemonhammer is equipped (F1 panel).
        internal const string CrucibleTrainingPath = "Assets/Scenes/Arena_Crucible.unity";

        [MenuItem("Anathema/Arenas/Rebuild Crucible Training (Khorne Berserker)")]
        static void BuildCrucibleBerserker() => Build(Character.KhorneBerserker, MapCrucible, CrucibleTrainingPath);

        [MenuItem("Anathema/Arenas/Rebuild Crucible Training (Grey Knight)")]
        static void BuildCrucibleGreyKnight() => Build(Character.GreyKnight, MapCrucible, CrucibleTrainingPath);

        // The training ground on the Hoarfrost Basilica (slam-launched caskets), in its own scene like the Crucible's.
        // Slams that launch a casket: the Bullgryn's Slab Slam, Shield Bash and Living Wall shove, the Berserker's Gorecleave
        // and R, the Psyker's Kine Slam and the Grey Knight's Daemonhammer Q.
        internal const string HoarfrostTrainingPath = "Assets/Scenes/Arena_Hoarfrost.unity";

        [MenuItem("Anathema/Arenas/Rebuild Hoarfrost Training (Bullgryn)")]
        public static void BuildHoarfrostBullgryn() => Build(Character.Bullgryn, MapHoarfrost, HoarfrostTrainingPath);

        [MenuItem("Anathema/Arenas/Rebuild Hoarfrost Training (Khorne Berserker)")]
        static void BuildHoarfrostBerserker() => Build(Character.KhorneBerserker, MapHoarfrost, HoarfrostTrainingPath);

        static void Build(Character character) => Build(character, MapColosseum, ScenePath);

        static void Build(Character character, int map, string scenePath)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop play mode before rebuilding an arena.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight(map);
            CreateArena(map, readyRooms: true);
            new GameObject("Gas Zone").AddComponent<GasZone>();
            new GameObject("Orb Spawner").AddComponent<OrbSpawner>();
            GameObject player = CreatePlayer(character);
            // The training player starts in the left ready room (the gate opens when prep ends), like an online match.
            foreach (ReadyRoom room in Object.FindObjectsByType<ReadyRoom>())
            {
                if (!room.IsLeft) continue;
                player.transform.SetPositionAndRotation(room.SlotPosition(0, 1, out Quaternion facing), facing);
            }
            CreateCamera(player.transform, map);
            GameObject shooter = CreateDummy(new Vector3(10f, 1f, 8f));
            shooter.name = "Training Dummy (Shooter)";
            shooter.AddComponent<DummyShooter>();
            CreateDummy(new Vector3(-10f, 1f, 8f));
            CreateDummy(new Vector3(0f, 1f, 16f));
            // Chain-lightning test cluster: within the 5m jump range of each other.
            CreateDummy(new Vector3(3f, 1f, 16f));
            CreateDummy(new Vector3(6f, 1f, 16f));
            CreateDummy(new Vector3(14f, 1f, -6f));
            GameObject mover = CreateDummy(new Vector3(0f, 1f, -12f));
            mover.name = "Training Dummy (Moving)";
            mover.AddComponent<DummyMover>().Configure(Vector3.right, 10f, 4f);
            // Champion-shaped targets with real health pools, for testing kills (death knell, corpse, ...).
            CreateCharacterDummy("Psyker", "Net/Player_Psyker", new Vector3(-5f, 0f, 3f), 145f, 1.8f, 0.4f, 2.3f);
            CreateCharacterDummy("Khorne Berserker", "Net/Player_KhorneBerserker", new Vector3(5f, 0f, 3f), 215f, 2f, 0.5f, 2.7f);

            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
            // Add the training arena to the build list without dropping the Menu and Versus scenes the online game loads
            // by name. (The Crucible training scene is for the editor only.)
            if (scenePath == ScenePath && !EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built {scenePath}. Press Play.");
        }

        // Maps the online game cycles through. 0 = the gladiator colosseum, 1 = the ruined shrine, 2 = the forge crucible
        // (breakable floor over lava).
        internal const int MapColosseum = 0;
        internal const int MapShrine = 1;
        internal const int MapCrucible = 2;
        internal const int MapHoarfrost = 3; // the frozen basilica (slam-launched reliquary caskets)

        internal static void CreateLight(int map = MapColosseum)
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;

            if (map == MapShrine)
            {
                // Cold, sickly moonlight through a broken roof; the green lamps and braziers do the rest.
                light.intensity = 0.65f;
                light.color = new Color(0.62f, 0.8f, 0.78f);
                go.transform.rotation = Quaternion.Euler(58f, 25f, 0f);
                RenderSettings.ambientLight = new Color(0.13f, 0.17f, 0.16f);
                return;
            }

            if (map == MapHoarfrost)
            {
                // Pale, cold daylight through the broken roof; the braziers and candles are the only warmth.
                light.intensity = 1.0f;
                light.color = new Color(0.78f, 0.88f, 1f);
                go.transform.rotation = Quaternion.Euler(52f, 15f, 0f);
                RenderSettings.ambientLight = new Color(0.2f, 0.25f, 0.33f);
                return;
            }

            if (map == MapCrucible)
            {
                // Dim, smoky forge light from above; the lava does most of the lighting.
                light.intensity = 0.7f;
                light.color = new Color(1f, 0.66f, 0.45f);
                go.transform.rotation = Quaternion.Euler(55f, 40f, 0f);
                RenderSettings.ambientLight = new Color(0.26f, 0.13f, 0.09f);
                return;
            }

            light.intensity = 0.9f;
            light.color = new Color(1f, 0.82f, 0.72f);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Warm, dim ambient to match the red-lit colosseum mood.
            RenderSettings.ambientLight = new Color(0.32f, 0.2f, 0.18f);
        }

        // Wall segments 0 and 12 sit on the -X/+X axis; with ready rooms they become the doorways.
        static bool IsDoorSegment(int i) => i == 0 || i == 12;
        // Stand/spire pieces within one segment of a doorway are dropped so the ready rooms are not buried in the stands.
        static bool IsDoorStand(int i) => i == 0 || i == 1 || i == 23 || i == 11 || i == 12 || i == 13;
        static bool InReadyRoomZone(Vector3 p) => Mathf.Abs(p.z) < 12f && Mathf.Abs(p.x) > 28f;

        internal static void CreateArena(int map = MapColosseum, bool readyRooms = false)
        {
            if (map == MapShrine)
            {
                CreateShrineArena(readyRooms);
                return;
            }
            if (map == MapCrucible)
            {
                CreateCrucibleArena(readyRooms);
                return;
            }
            if (map == MapHoarfrost)
            {
                CreateHoarfrostArena(readyRooms);
                return;
            }


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
                if (readyRooms && IsDoorSegment(i)) continue; // doorway into a ready room

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
                    // (Built then dropped, rather than skipped, so the crowd's random layout elsewhere stays the same.)
                    bool dropped = readyRooms && IsDoorStand(i);
                    var tier = Box($"Stand {t}-{i}", RadialPos(tierRadius[t], angle, tierHeight[t] * 0.5f), new Vector3(tierChord, tierHeight[t], 4.6f), stands);
                    tier.transform.rotation = Quaternion.Euler(0f, 90f - angle, 0f);
                    RemoveCollider(tier);
                    if (dropped) Object.DestroyImmediate(tier);

                    for (int c = 0; c < 4; c++)
                    {
                        float offset = ((float)rng.NextDouble() - 0.5f) * tierChord * 0.85f;
                        float depth = ((float)rng.NextDouble() - 0.5f) * 3.4f;
                        Vector3 tangent = new Vector3(-Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
                        Vector3 pos = RadialPos(tierRadius[t] + depth, angle, tierHeight[t] + 0.45f) + tangent * offset;
                        Material crowdMaterial = crowd[rng.Next(crowd.Length)];
                        if (dropped) continue;
                        var fan = Box($"Fan {t}-{i}-{c}", pos, new Vector3(0.6f, 0.9f, 0.5f), crowdMaterial);
                        RemoveCollider(fan);
                    }
                }
            }

            // Khorne-worshipping chaos dressing: the two insignia decals, blood and skeletons.
            AddChaosDressing(new System.Random(666), ArenaRadius, WallSegments, GetMaterial("ChaosBone", new Color(0.62f, 0.58f, 0.46f)), wallDark, 1.9f, readyRooms, ground);

            if (readyRooms) CreateReadyRooms(ArenaRadius, ground, wall, wallDark, 3f);
        }

        // Ruined gothic shrine: same footprint and layout as the colosseum (round 28m floor, glowing centre ring, four
        // diagonal cover clusters, buttressed perimeter) but dressed as a derelict Imperial chapel -- cracked flagstones,
        // sickly green lamplight, bone piles, torn banners and drifting servo-skulls.
        static void CreateShrineArena(bool readyRooms = false)
        {
            const float ArenaRadius = 28f;
            const int WallSegments = 24;

            Material pit = GetMaterial("ShrinePit", new Color(0.02f, 0.03f, 0.03f));
            Material flag = GetMaterial("ShrineFlag", new Color(0.17f, 0.18f, 0.18f));
            Material flagDark = GetMaterial("ShrineFlagDark", new Color(0.1f, 0.11f, 0.11f));
            Material flagLight = GetMaterial("ShrineFlagLight", new Color(0.28f, 0.29f, 0.28f));
            Material crack = GetMaterial("ShrineCrack", new Color(0.05f, 0.06f, 0.06f));
            Material stone = GetMaterial("ShrineStone", new Color(0.22f, 0.23f, 0.22f));
            Material stoneDark = GetMaterial("ShrineStoneDark", new Color(0.12f, 0.12f, 0.13f));
            Material iron = GetMaterial("ShrineIron", new Color(0.1f, 0.09f, 0.09f));
            Material crate = GetMaterial("ShrineCrate", new Color(0.2f, 0.17f, 0.12f));
            Material bone = GetMaterial("ShrineBone", new Color(0.62f, 0.58f, 0.46f));
            Material banner = GetMaterial("ShrineBanner", new Color(0.32f, 0.04f, 0.04f));
            Material gold = GetMaterial("ShrineGold", new Color(0.65f, 0.5f, 0.15f));
            Material glow = GetUnlitMaterial("ShrineGlow", new Color(0.35f, 1f, 0.4f));
            Material glowDim = GetUnlitMaterial("ShrineGlowDim", new Color(0.12f, 0.45f, 0.2f));
            Material candle = GetUnlitMaterial("ShrineCandle", new Color(1f, 0.75f, 0.35f));

            var rng = new System.Random(4040);

            // Collider floor, also visible as the black void outside the shrine.
            Box("Ground Collider", new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 130f), pit);

            // Flagstone floor, worn rings, cracks and the glowing centre ring.
            Disc("Floor", ArenaRadius * 2f, 0.02f, flag);
            Disc("Floor Ring Outer", 41f, 0.03f, flagDark);
            Disc("Floor Ring Inner", 40.4f, 0.04f, flag);
            Disc("Floor Ring Mid", 26f, 0.05f, flagDark);
            Disc("Floor Ring Mid Inner", 25.4f, 0.06f, flag);
            for (int i = 0; i < 7; i++)
            {
                var spoke = Box($"Floor Crack {i}", new Vector3(0f, 0.07f, 0f), new Vector3(ArenaRadius * 1.9f, 0.01f, 0.14f + (float)rng.NextDouble() * 0.12f), crack);
                spoke.transform.rotation = Quaternion.Euler(0f, i * 25.7f + 8f, 0f);
                RemoveCollider(spoke);
            }
            Disc("Center Ring Outer", 6.2f, 0.08f, glow);
            Disc("Center Ring Gap", 5.9f, 0.09f, flagDark);
            Disc("Center Ring Disc", 3.6f, 0.10f, flagLight);
            Disc("Center Ring Core", 2.4f, 0.11f, flagDark);
            for (int i = 0; i < 4; i++) // a cogitator-etched cross inside the ring
            {
                var bar = Box($"Center Rune {i}", new Vector3(0f, 0.12f, 0f), new Vector3(5.5f, 0.01f, 0.12f), glowDim);
                bar.transform.rotation = Quaternion.Euler(0f, i * 45f, 0f);
                RemoveCollider(bar);
            }
            var coreLightGo = new GameObject("Center Glow Light");
            coreLightGo.transform.position = new Vector3(0f, 1.6f, 0f);
            AddLight(coreLightGo, new Color(0.3f, 1f, 0.4f), 11f, 1.4f);

            // Perimeter: broken gothic wall segments of uneven height, a buttress between each, lancet windows, banners.
            float chord = 2f * (ArenaRadius + 1f) * Mathf.Tan(Mathf.PI / WallSegments) + 0.25f;
            for (int i = 0; i < WallSegments; i++)
            {
                float angle = i * 360f / WallSegments;
                float yaw = 90f - angle;
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                float h = 3f + (float)rng.NextDouble() * 1.8f;

                bool door = readyRooms && IsDoorSegment(i); // doorway into a ready room: no wall here (random draws still made)

                if (!door)
                {
                    var seg = Box($"Wall {i}", RadialPos(ArenaRadius + 1.1f, angle, h * 0.5f), new Vector3(chord, h, 1.6f), stone);
                    seg.transform.rotation = rot;

                    var trim = Box($"Wall Trim {i}", RadialPos(ArenaRadius + 0.28f, angle, 0.1f), new Vector3(chord, 0.14f, 0.32f), glowDim);
                    trim.transform.rotation = rot;
                    RemoveCollider(trim);

                    var cap = Box($"Wall Cap {i}", RadialPos(ArenaRadius + 1.1f, angle, h + 0.12f), new Vector3(chord, 0.25f, 1.9f), stoneDark);
                    cap.transform.rotation = rot;
                    RemoveCollider(cap);
                }

                // Buttress pier on the seam to the next segment, topped with a pinnacle and a candle.
                float seam = angle + 180f / WallSegments;
                float pierH = h + 1.6f + (float)rng.NextDouble();
                var pier = Box($"Buttress {i}", RadialPos(ArenaRadius + 1.5f, seam, pierH * 0.5f), new Vector3(1.3f, pierH, 2.2f), stoneDark);
                pier.transform.rotation = Quaternion.Euler(0f, 90f - seam, 0f);
                var pinnacle = Box($"Pinnacle {i}", RadialPos(ArenaRadius + 1.5f, seam, pierH + 0.7f), new Vector3(0.7f, 1.4f, 0.7f), stoneDark);
                pinnacle.transform.rotation = Quaternion.Euler(0f, 45f - seam, 0f);
                RemoveCollider(pinnacle);
                var flame = Box($"Pier Candle {i}", RadialPos(ArenaRadius + 0.6f, seam, 1.3f), new Vector3(0.14f, 0.28f, 0.14f), candle);
                RemoveCollider(flame);

                if (door)
                {
                    // nothing hangs in a doorway
                }
                else if (i % 2 == 0)
                {
                    // Lancet window: slim glowing slab with a diamond forming the pointed arch.
                    var win = Box($"Window {i}", RadialPos(ArenaRadius + 0.22f, angle, 2.0f), new Vector3(0.7f, 1.8f, 0.08f), glowDim);
                    win.transform.rotation = rot;
                    RemoveCollider(win);
                    var arch = Box($"Window Arch {i}", RadialPos(ArenaRadius + 0.22f, angle, 2.9f), new Vector3(0.5f, 0.5f, 0.08f), glowDim);
                    arch.transform.rotation = rot * Quaternion.Euler(0f, 0f, 45f);
                    RemoveCollider(arch);
                }
                else
                {
                    // Torn Imperial banner hanging down the wall, with a gold aquila-ish crest.
                    var ban = Box($"Banner {i}", RadialPos(ArenaRadius + 0.2f, angle, 1.9f), new Vector3(1.0f, 2.4f, 0.06f), banner);
                    ban.transform.rotation = rot * Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 6f);
                    RemoveCollider(ban);
                    var crest = Box($"Banner Crest {i}", RadialPos(ArenaRadius + 0.16f, angle, 2.4f), new Vector3(0.38f, 0.38f, 0.07f), gold);
                    crest.transform.rotation = rot * Quaternion.Euler(0f, 0f, 45f);
                    RemoveCollider(crest);
                }

                // Fallen rubble at the base of some segments.
                if (rng.NextDouble() < 0.5)
                {
                    float rAngle = angle + ((float)rng.NextDouble() - 0.5f) * 10f;
                    var rubble = Box($"Rubble {i}", RadialPos(ArenaRadius - 1.1f, rAngle, 0.25f), new Vector3(1.1f, 0.5f, 0.9f), stoneDark);
                    rubble.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 18f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 18f);
                    RemoveCollider(rubble);
                    if (door) Object.DestroyImmediate(rubble); // keep the doorway clear
                }
            }

            // Four ruined chapel alcoves near the centre (the colosseum's barricades): a back wall with two stub walls,
            // a skull-lamp brazier in front. Placed on the diagonals so the cardinal orb lines stay clear.
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                Vector3 centre = RadialPos(11f, angle, 0f);
                Vector3 tangent = new Vector3(-Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
                Vector3 radial = RadialDir(angle);
                Quaternion face = Quaternion.Euler(0f, 90f - angle, 0f);

                var back = Box($"Alcove Wall {i}", centre + radial * 0.8f + Vector3.up * 1.0f, new Vector3(6f, 2f, 1.1f), stone);
                back.transform.rotation = face;
                var backTop = Box($"Alcove Cap {i}", centre + radial * 0.8f + Vector3.up * 2.1f, new Vector3(6.3f, 0.2f, 1.4f), stoneDark);
                backTop.transform.rotation = face;
                RemoveCollider(backTop);
                var backGlow = Box($"Alcove Glow {i}", centre + Vector3.up * 0.1f + radial * 0.2f, new Vector3(6.1f, 0.1f, 0.14f), glowDim);
                backGlow.transform.rotation = face;
                RemoveCollider(backGlow);

                for (int side = -1; side <= 1; side += 2)
                {
                    var stub = Box($"Alcove Stub {i}{(side < 0 ? "a" : "b")}", centre + tangent * (side * 3f) - radial * 0.9f + Vector3.up * 0.8f, new Vector3(1.1f, 1.6f, 2.4f), stoneDark);
                    stub.transform.rotation = face;
                    var skull = Box($"Alcove Skull {i}{(side < 0 ? "a" : "b")}", centre + tangent * (side * 3f) - radial * 0.9f + Vector3.up * 1.8f, new Vector3(0.38f, 0.34f, 0.34f), bone);
                    RemoveCollider(skull);
                }

                AddBrazier($"Alcove Brazier {i}", centre - radial * 0.4f, iron, glow, true);
            }

            // Four stacks of Munitorum crates further out, also on the diagonals (like the crates in the reference).
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                Vector3 centre = RadialPos(20.5f, angle, 0f);
                Vector3 tangent = new Vector3(-Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
                Quaternion face = Quaternion.Euler(0f, 90f - angle, 0f);
                for (int c = -1; c <= 1; c += 2)
                {
                    Vector3 pos = centre + tangent * (c * 1.7f);
                    var lower = Box($"Crate {i}{c}", pos + Vector3.up * 0.8f, new Vector3(2.4f, 1.6f, 2.4f), crate);
                    lower.transform.rotation = face * Quaternion.Euler(0f, c * 6f, 0f);
                    var band = Box($"Crate Band {i}{c}", pos + Vector3.up * 0.8f, new Vector3(2.5f, 0.18f, 2.5f), iron);
                    band.transform.rotation = lower.transform.rotation;
                    RemoveCollider(band);
                }
                var top = Box($"Crate Top {i}", centre + Vector3.up * 2.1f, new Vector3(2.2f, 1.2f, 2.2f), crate);
                top.transform.rotation = face * Quaternion.Euler(0f, 18f, 0f);
                var topGold = Box($"Crate Seal {i}", centre + Vector3.up * 2.1f + RadialDir(angle) * -1.12f, new Vector3(0.4f, 0.4f, 0.06f), gold);
                topGold.transform.rotation = face * Quaternion.Euler(0f, 18f, 45f);
                RemoveCollider(topGold);
            }

            // Broken statuary columns on the cardinal axes between the centre and the orb lines.
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Vector3 pos = RadialPos(7.5f, angle, 0f);
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = $"Column {i}";
                col.transform.position = pos + Vector3.up * 1.25f;
                col.transform.localScale = new Vector3(1.1f, 1.25f + (i % 2) * 0.5f, 1.1f);
                col.GetComponent<Renderer>().sharedMaterial = stone;
                var baseBlock = Box($"Column Base {i}", pos + Vector3.up * 0.2f, new Vector3(1.7f, 0.4f, 1.7f), stoneDark);
                RemoveCollider(baseBlock);
                var capital = Box($"Column Cap {i}", pos + Vector3.up * (2.5f + (i % 2) * 1f), new Vector3(1.4f, 0.28f, 1.4f), stoneDark);
                capital.transform.rotation = Quaternion.Euler(0f, 20f * i, (i % 2) * 7f);
                RemoveCollider(capital);
                var columnSkull = Box($"Column Skull {i}", pos + Vector3.up * (2.8f + (i % 2) * 1f), new Vector3(0.5f, 0.45f, 0.45f), bone);
                columnSkull.transform.rotation = Quaternion.Euler(0f, angle + 180f, 0f);
                RemoveCollider(columnSkull);
            }

            // Four skull-and-candle braziers on the cardinal edge (inside the wall, clear of spawns and orb lines).
            for (int i = 0; i < 4; i++)
                AddBrazier($"Edge Brazier {i}", RadialPos(24.5f, 22.5f + i * 90f, 0f), iron, glow, true);

            // Scattered bone piles and rubble on the floor.
            for (int i = 0; i < 26; i++)
            {
                float angle = (float)rng.NextDouble() * 360f;
                float radius = 4.5f + (float)rng.NextDouble() * 21f;
                Vector3 pos = RadialPos(radius, angle, 0.12f);
                bool isSkull = rng.NextDouble() < 0.4;
                var piece = Box($"Debris {i}", pos, isSkull ? new Vector3(0.3f, 0.26f, 0.28f) : new Vector3(0.7f, 0.22f, 0.12f), isSkull ? bone : stoneDark);
                piece.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                RemoveCollider(piece);
            }

            // Servo-skulls: a pair circling the centre ring, one over each alcove and each crate stack.
            AddServoSkull("Servo Skull Centre A", new Vector3(0f, 3.3f, 0f), 3.2f, 0.45f, new Color(0.3f, 1f, 0.4f), bone, iron);
            AddServoSkull("Servo Skull Centre B", new Vector3(0f, 3.9f, 0f), 2.2f, -0.6f, new Color(1f, 0.25f, 0.15f), bone, iron);
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                AddServoSkull($"Servo Skull Alcove {i}", RadialPos(14f, angle, 3.3f), 1.8f, (i % 2 == 0 ? 0.5f : -0.5f), new Color(0.3f, 1f, 0.4f), bone, iron);
                AddServoSkull($"Servo Skull Crate {i}", RadialPos(21.5f, angle, 4.2f), 1.2f, (i % 2 == 0 ? -0.35f : 0.35f), new Color(1f, 0.25f, 0.15f), bone, iron);
            }

            // Chaos corruption: Khorne sigils branded into the floor and hung on the walls, skeletons strewn about.
            AddChaosDressing(rng, ArenaRadius, WallSegments, bone, iron, 3.6f, readyRooms, flag, false);

            // Outside the wall: jagged ruined spires against the void, with the occasional dying lamp.
            for (int i = 0; i < 28; i++)
            {
                float angle = i * 360f / 28f + (float)rng.NextDouble() * 6f;
                float radius = 36f + (float)rng.NextDouble() * 10f;
                float h = 7f + (float)rng.NextDouble() * 14f;
                var spire = Box($"Spire {i}", RadialPos(radius, angle, h * 0.5f), new Vector3(2f + (float)rng.NextDouble() * 2f, h, 2f + (float)rng.NextDouble() * 2f), stoneDark);
                spire.transform.rotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 8f, (float)rng.NextDouble() * 90f, (float)(rng.NextDouble() - 0.5) * 8f);
                RemoveCollider(spire);
                bool inRoomZone = readyRooms && InReadyRoomZone(spire.transform.position);
                if (inRoomZone) Object.DestroyImmediate(spire);
                if (i % 4 == 0)
                {
                    var lampGlow = Box($"Spire Lamp {i}", RadialPos(radius - 2.2f, angle, h * 0.6f), new Vector3(0.3f, 0.9f, 0.3f), glowDim);
                    RemoveCollider(lampGlow);
                    if (readyRooms && InReadyRoomZone(lampGlow.transform.position)) Object.DestroyImmediate(lampGlow);
                }
            }

            if (readyRooms) CreateReadyRooms(ArenaRadius, flag, stone, stoneDark, 3.6f);
        }

        // The Crucible: a forge-temple floor of stone plates laid over a lava bed. Same 28m round footprint and ready-room
        // doorways as the other maps, but a different cover layout (mirror symmetric across both axes, so both teams get
        // the same ground):
        //   - a solid iron dais in the centre ringed by four anvils (tight fighting around the charge orb),
        //   - two split forge walls north and south of it, with a gap in the middle of each,
        //   - a bulwark in front of each doorway, shielding the approach from long lanes,
        //   - an L-shaped bunker in each corner, open towards the centre,
        //   - a solid iron walkway ring at the spawn radius and solid approaches to the doorways.
        // Every other plate can be smashed by ground slams (see DestructibleFloor), opening a fire pit for a while.
        static void CreateCrucibleArena(bool readyRooms = false)
        {
            const float ArenaRadius = 28f;
            const int WallSegments = 24;
            const float Spacing = 2.4f;   // plate grid
            const float Plate = 2.27f;    // plate size: the gap between plates shows the glowing bed beneath
            const float PlateTop = 0.08f;
            const float PlateThickness = 0.3f;
            const float DaisRadius = 3.7f;
            const float WalkwayInner = 20.4f, WalkwayOuter = 24.4f; // NetGame's free-for-all spawns are at 23m

            Material pit = GetMaterial("CruciblePit", new Color(0.03f, 0.02f, 0.02f));
            Material basaltA = GetMaterial("CrucibleBasaltA", new Color(0.16f, 0.12f, 0.11f));
            Material basaltB = GetMaterial("CrucibleBasaltB", new Color(0.19f, 0.14f, 0.12f));
            Material basaltC = GetMaterial("CrucibleBasaltC", new Color(0.13f, 0.1f, 0.1f));
            Material iron = GetMaterial("CrucibleIron", new Color(0.23f, 0.22f, 0.24f));
            // The unbreakable deck: lighter, cooler steel laid without seams, so it reads apart from the breakable stone.
            Material deck = GetMaterial("CrucibleDeck", new Color(0.36f, 0.36f, 0.4f));
            Material deckAlt = GetMaterial("CrucibleDeckAlt", new Color(0.32f, 0.32f, 0.36f));
            Material ironDark = GetMaterial("CrucibleIronDark", new Color(0.09f, 0.09f, 0.1f));
            Material brass = GetMaterial("CrucibleBrass", new Color(0.55f, 0.4f, 0.16f));
            Material molten = GetUnlitMaterial("CrucibleMolten", new Color(1f, 0.45f, 0.08f));
            Material ember = GetUnlitMaterial("CrucibleEmber", new Color(0.62f, 0.17f, 0.03f));
            Material lavaLake = GetUnlitMaterial("CrucibleLavaLake", new Color(0.45f, 0.1f, 0.02f));

            var rng = new System.Random(2026);

            // Collider floor; outside the wall it sits under a lava lake, inside under the glowing bed and the plates.
            Box("Ground Collider", new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 130f), pit);
            Disc("Lava Lake", 200f, 0.005f, lavaLake);
            Disc("Molten Bed", ArenaRadius * 2f + 2f, 0.03f, ember);

            // ---- Cover (built first: the plates under it are made solid) --------------------------------------------
            var footprints = new System.Collections.Generic.List<(Vector3 centre, Vector3 size, float yaw)>();

            GameObject Cover(string name, Vector3 centre, Vector3 size, float yaw, Material body)
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                var block = Box(name, new Vector3(centre.x, size.y * 0.5f, centre.z), size, body);
                block.transform.rotation = rot;
                var cap = Box(name + " Cap", new Vector3(centre.x, size.y + 0.08f, centre.z), new Vector3(size.x + 0.25f, 0.16f, size.z + 0.25f), ironDark);
                cap.transform.rotation = rot;
                RemoveCollider(cap);
                var glow = Box(name + " Glow", new Vector3(centre.x, 0.12f, centre.z), new Vector3(size.x + 0.18f, 0.06f, size.z + 0.18f), molten);
                glow.transform.rotation = rot;
                RemoveCollider(glow);
                footprints.Add((centre, size, yaw));
                return block;
            }

            // Four anvils around the dais, on the diagonals.
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                Vector3 c = RadialPos(5.4f, angle, 0f);
                Cover($"Anvil {i}", c, new Vector3(1.5f, 1.3f, 1.5f), 45f - angle, ironDark);
                var top = Box($"Anvil Face {i}", new Vector3(c.x, 1.42f, c.z), new Vector3(1.1f, 0.08f, 1.1f), brass);
                top.transform.rotation = Quaternion.Euler(0f, 45f - angle, 0f);
                RemoveCollider(top);
            }

            // Split forge walls north and south of the centre, each with a molten channel along the top.
            for (int sz = -1; sz <= 1; sz += 2)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 c = new Vector3(sx * 3.1f, 0f, sz * 9.6f);
                Cover($"Forge Wall {(sz < 0 ? "S" : "N")}{(sx < 0 ? "W" : "E")}", c, new Vector3(4.2f, 1.8f, 1.1f), 0f, iron);
                var channel = Box($"Forge Channel {sz}{sx}", new Vector3(c.x, 1.97f, c.z), new Vector3(3.8f, 0.04f, 0.4f), molten);
                RemoveCollider(channel);
            }

            // A bulwark in front of each doorway.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 c = new Vector3(sx * 12.6f, 0f, 0f);
                Cover($"Bulwark {(sx < 0 ? "W" : "E")}", c, new Vector3(1.1f, 1.8f, 4.6f), 0f, iron);
                for (int k = -1; k <= 1; k += 2)
                {
                    var rib = Box($"Bulwark Rib {sx}{k}", new Vector3(c.x, 0.9f, k * 1.6f), new Vector3(1.3f, 1.7f, 0.25f), brass);
                    RemoveCollider(rib);
                }
            }

            // An L-shaped bunker in each corner: two arms meeting at the outer corner, open towards the centre.
            for (int sz = -1; sz <= 1; sz += 2)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                const float CornerX = 15f, CornerZ = 14f, Arm = 4.4f, Thick = 1.1f;
                string q = (sz < 0 ? "S" : "N") + (sx < 0 ? "W" : "E");
                Cover($"Bunker {q} A", new Vector3(sx * (CornerX - Arm * 0.5f + Thick * 0.5f), 0f, sz * CornerZ), new Vector3(Arm, 1.8f, Thick), 0f, iron);
                // (B runs from A's inner face towards the centre, so the two meet without a gap.)
                Cover($"Bunker {q} B", new Vector3(sx * CornerX, 0f, sz * (CornerZ - Arm * 0.5f)), new Vector3(Thick, 1.8f, Arm - Thick), 0f, iron);
                AddBrazier($"Bunker Brazier {q}", new Vector3(sx * (CornerX - 1.5f), 0f, sz * (CornerZ - 1.5f)), ironDark, molten, false);
            }

            bool UnderCover(Vector2 c)
            {
                float half = Plate * 0.4f;
                foreach (var f in footprints)
                {
                    Vector3 local = Quaternion.Euler(0f, -f.yaw, 0f) * (new Vector3(c.x, 0f, c.y) - new Vector3(f.centre.x, 0f, f.centre.z));
                    if (Mathf.Abs(local.x) <= f.size.x * 0.5f + half && Mathf.Abs(local.z) <= f.size.z * 0.5f + half) return true;
                }
                return false;
            }

            // ---- Floor plates --------------------------------------------------------------------------------------
            var floorRoot = new GameObject("Destructible Floor");
            var deckRoot = new GameObject("Iron Deck");
            var plates = new System.Collections.Generic.List<Transform>();
            var cells = new System.Collections.Generic.List<Vector2Int>();
            Material[] basalt = { basaltA, basaltB, basaltC };
            int extent = Mathf.CeilToInt((ArenaRadius + 1f) / Spacing);
            for (int gz = -extent; gz <= extent; gz++)
            for (int gx = -extent; gx <= extent; gx++)
            {
                var c = new Vector2(gx * Spacing, gz * Spacing);
                float r = c.magnitude;
                if (r > ArenaRadius + 0.8f) continue; // the outermost plates tuck under the wall

                bool solid = r < DaisRadius
                             || (r >= WalkwayInner && r <= WalkwayOuter)
                             || (Mathf.Abs(c.y) < 3.7f && Mathf.Abs(c.x) > WalkwayInner) // doorway approaches
                             || UnderCover(c);

                Material m = solid ? ((gx + gz) % 2 == 0 ? deck : deckAlt) : basalt[rng.Next(basalt.Length)];
                float size = solid ? Spacing + 0.01f : Plate;
                var plate = Box($"Plate {gx},{gz}", new Vector3(c.x, PlateTop - PlateThickness * 0.5f, c.y), new Vector3(size, PlateThickness, size), m);
                RemoveCollider(plate); // everyone walks on the ground collider; the plates are only the look
                plate.transform.SetParent(solid ? deckRoot.transform : floorRoot.transform, true);
                if (solid) continue;
                // A faint ember crack across some plates, so the breakable stone reads differently from the iron deck.
                if (rng.NextDouble() < 0.35)
                {
                    var crack = Box("Crack", Vector3.zero, new Vector3(Plate * 0.7f, 0.01f, 0.07f), ember);
                    RemoveCollider(crack);
                    crack.transform.SetParent(plate.transform, false);
                    crack.transform.localScale = new Vector3(0.7f, 0.04f, 0.03f);
                    crack.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    crack.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
                }
                plates.Add(plate.transform);
                cells.Add(new Vector2Int(gx, gz));
            }
            floorRoot.AddComponent<DestructibleFloor>().Configure(Spacing, plates.ToArray(), cells.ToArray());

            // The dais: a brass ring around the forge core, where the charge orb appears.
            Disc("Dais Ring", DaisRadius * 2f, 0.1f, brass);
            Disc("Dais Plate", DaisRadius * 2f - 0.5f, 0.11f, ironDark);
            Disc("Forge Core Rim", 3.0f, 0.12f, brass);
            Disc("Forge Core", 2.4f, 0.13f, molten);
            var coreLight = new GameObject("Forge Core Light");
            coreLight.transform.position = new Vector3(0f, 1.4f, 0f);
            AddLight(coreLight, new Color(1f, 0.5f, 0.15f), 10f, 1.4f);

            // ---- Perimeter: iron wall segments with a molten base trim and vents --------------------------------------
            float chord = 2f * (ArenaRadius + 1f) * Mathf.Tan(Mathf.PI / WallSegments) + 0.25f;
            for (int i = 0; i < WallSegments; i++)
            {
                if (readyRooms && IsDoorSegment(i)) continue;
                float angle = i * 360f / WallSegments;
                Quaternion rot = Quaternion.Euler(0f, 90f - angle, 0f);

                var seg = Box($"Wall {i}", RadialPos(ArenaRadius + 1.1f, angle, 1.6f), new Vector3(chord, 3.2f, 1.6f), iron);
                seg.transform.rotation = rot;
                var trim = Box($"Wall Trim {i}", RadialPos(ArenaRadius + 0.28f, angle, 0.12f), new Vector3(chord, 0.16f, 0.32f), molten);
                trim.transform.rotation = rot;
                RemoveCollider(trim);
                var cap = Box($"Wall Cap {i}", RadialPos(ArenaRadius + 1.1f, angle, 3.3f), new Vector3(chord, 0.25f, 1.9f), ironDark);
                cap.transform.rotation = rot;
                RemoveCollider(cap);

                if (i % 2 == 1)
                {
                    // A glowing furnace vent in the wall face.
                    var vent = Box($"Wall Vent {i}", RadialPos(ArenaRadius + 0.26f, angle, 1.5f), new Vector3(1.6f, 0.9f, 0.06f), ember);
                    vent.transform.rotation = rot;
                    RemoveCollider(vent);
                    for (int b = -1; b <= 1; b++)
                    {
                        var bar = Box($"Wall Vent Bar {i}.{b}", RadialPos(ArenaRadius + 0.2f, angle, 1.5f) + rot * Vector3.right * (b * 0.45f), new Vector3(0.1f, 1.0f, 0.08f), ironDark);
                        bar.transform.rotation = rot;
                        RemoveCollider(bar);
                    }
                }
                else
                {
                    // A chimney stack on the wall.
                    var stack = Box($"Wall Stack {i}", RadialPos(ArenaRadius + 1.5f, angle, 4.6f), new Vector3(1f, 2.6f, 1f), ironDark);
                    stack.transform.rotation = rot;
                    RemoveCollider(stack);
                    var mouth = Box($"Wall Stack Glow {i}", RadialPos(ArenaRadius + 1.5f, angle, 5.95f), new Vector3(0.7f, 0.08f, 0.7f), molten);
                    mouth.transform.rotation = rot;
                    RemoveCollider(mouth);
                }
            }

            // Warm lights around the edge, between the doorways.
            for (int i = 0; i < 8; i++)
            {
                var lightGo = new GameObject($"Forge Light {i}");
                lightGo.transform.position = RadialPos(25.5f, 22.5f + i * 45f, 2.2f);
                AddLight(lightGo, new Color(1f, 0.45f, 0.15f), 10f, 1.1f);
            }

            // ---- Outside: forge towers rising out of the lava lake, and two great crucibles at north and south ----------
            for (int i = 0; i < 20; i++)
            {
                float angle = i * 18f + (float)rng.NextDouble() * 6f;
                float radius = 36f + (float)rng.NextDouble() * 9f;
                float h = 8f + (float)rng.NextDouble() * 12f;
                Vector3 p = RadialPos(radius, angle, h * 0.5f);
                if (readyRooms && InReadyRoomZone(p)) continue;
                if (Mathf.Abs(p.x) < 7f) continue; // leave room for the crucibles
                var tower = Box($"Forge Tower {i}", p, new Vector3(2.4f + (float)rng.NextDouble() * 2f, h, 2.4f + (float)rng.NextDouble() * 2f), ironDark);
                tower.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);
                RemoveCollider(tower);
                var window = Box($"Forge Tower Glow {i}", RadialPos(radius - 2.3f, angle, h * 0.75f), new Vector3(0.5f, h * 0.3f, 0.5f), ember);
                RemoveCollider(window);
            }
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 c = new Vector3(0f, 0f, sz * 38f);
                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = $"Great Crucible {(sz < 0 ? "S" : "N")}";
                body.transform.position = c + Vector3.up * 5f;
                body.transform.localScale = new Vector3(9f, 5f, 9f);
                body.GetComponent<Renderer>().sharedMaterial = ironDark;
                RemoveCollider(body);
                var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rim.name = "Crucible Rim";
                rim.transform.position = c + Vector3.up * 10.05f;
                rim.transform.localScale = new Vector3(9.6f, 0.15f, 9.6f);
                rim.GetComponent<Renderer>().sharedMaterial = brass;
                RemoveCollider(rim);
                var melt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                melt.name = "Crucible Melt";
                melt.transform.position = c + Vector3.up * 10.1f;
                melt.transform.localScale = new Vector3(8f, 0.12f, 8f);
                melt.GetComponent<Renderer>().sharedMaterial = molten;
                RemoveCollider(melt);
                // The pour: a stream of molten metal down into the lake on the arena side.
                var pour = Box($"Crucible Pour {sz}", c + new Vector3(0f, 5f, -sz * 4.8f), new Vector3(0.7f, 10f, 0.7f), molten);
                RemoveCollider(pour);
                var lightGo = new GameObject("Crucible Light");
                lightGo.transform.position = c + new Vector3(0f, 6f, -sz * 6f);
                AddLight(lightGo, new Color(1f, 0.5f, 0.15f), 18f, 2f);
            }

            if (readyRooms) CreateReadyRooms(ArenaRadius, iron, iron, ironDark, 3.2f);
        }

        // The Hoarfrost Basilica: a frozen Imperial cathedral on an ice world. A tighter 25m round footprint (the other maps
        // are 28m; the free-for-all spawns at 23m still fit) with the usual ready-room doorways; mirror symmetric on both axes:
        //   - four frozen reliquaries (CasketField) on plinths on the diagonals around the centre: a slam sends one sliding
        //     across the ice, carrying the first enemy in its way into whatever it meets (see ReliquaryCasket / CasketField),
        //   - four screen walls on the axes around the centre, the inner ring of hard cover between the caskets,
        //   - eight ice-clad pillars in a ring at 16m, 30 degrees either side of each axis,
        //   - a broken altar wall north and south, and a bulwark in front of each doorway (so no slide leaves through a
        //     doorway into a ready room),
        //   - snow drifting against the outer wall, icicles on its cap, warm braziers against the cold light.
        static void CreateHoarfrostArena(bool readyRooms = false)
        {
            const float ArenaRadius = 25f; // tighter than the other maps (28m); the ready rooms sit on the wall line wherever it is
            const int WallSegments = 24;

            Material water = GetMaterial("HoarfrostWater", new Color(0.03f, 0.06f, 0.09f));
            Material ice = GetMaterial("HoarfrostIce", new Color(0.6f, 0.74f, 0.84f));
            Material iceDark = GetMaterial("HoarfrostIceDark", new Color(0.42f, 0.56f, 0.68f));
            Material iceLight = GetMaterial("HoarfrostIceLight", new Color(0.78f, 0.88f, 0.95f));
            Material snow = GetMaterial("HoarfrostSnow", new Color(0.9f, 0.93f, 0.97f));
            Material stone = GetMaterial("HoarfrostStone", new Color(0.33f, 0.35f, 0.41f));
            Material stoneDark = GetMaterial("HoarfrostStoneDark", new Color(0.17f, 0.19f, 0.24f));
            Material gold = GetMaterial("HoarfrostGold", new Color(0.66f, 0.52f, 0.2f));
            Material coffin = GetMaterial("HoarfrostCoffin", new Color(0.14f, 0.1f, 0.09f));
            Material crack = GetUnlitMaterial("HoarfrostCrack", new Color(0.86f, 0.95f, 1f, 0.7f));
            Material frost = GetUnlitMaterial("HoarfrostFrost", new Color(0.78f, 0.92f, 1f, 0.7f));
            Material casketIce = GetUnlitMaterial("HoarfrostCasketIce", new Color(0.62f, 0.85f, 1f, 0.45f));
            Material holy = GetUnlitMaterial("HoarfrostHoly", new Color(0.55f, 0.85f, 1f));
            Material holyDim = GetUnlitMaterial("HoarfrostHolyDim", new Color(0.3f, 0.5f, 0.7f));
            Material flame = GetUnlitMaterial("HoarfrostFlame", new Color(1f, 0.6f, 0.22f));
            Material window = GetUnlitMaterial("HoarfrostWindow", new Color(0.45f, 0.7f, 0.95f));

            var rng = new System.Random(1818);

            // Collider floor, under a frozen lake outside the walls.
            Box("Ground Collider", new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 130f), water);
            Disc("Frozen Lake", 200f, 0.005f, iceDark);

            // The floor is a grid of ice plates (laid further down, once the cover is placed) over dark water; the seams
            // between them show this bed, so they read as cracks in the ice. The holy sigil in the middle (where the orb
            // appears) sits on top of solid packed snow.
            Disc("Ice Bed", ArenaRadius * 2f + 2f, 0.03f, iceDark);
            Disc("Center Ring Outer", 6.2f, 0.10f, holy);
            Disc("Center Ring Gap", 5.9f, 0.11f, iceDark);
            Disc("Center Ring Disc", 3.6f, 0.12f, iceLight);
            Disc("Center Ring Core", 2.4f, 0.13f, iceDark);
            for (int i = 0; i < 2; i++) // the Imperial cross inside the ring
            {
                var bar = Box($"Center Cross {i}", new Vector3(0f, 0.14f, 0f), new Vector3(5.2f, 0.01f, 0.5f), holyDim);
                bar.transform.rotation = Quaternion.Euler(0f, i * 90f + 45f, 0f);
                RemoveCollider(bar);
            }
            // Everything solid standing on the floor: the plates under it can't break.
            var footprints = new System.Collections.Generic.List<(Vector3 centre, Vector3 size, float yaw)>();
            var coreLight = new GameObject("Center Glow Light");
            coreLight.transform.position = new Vector3(0f, 1.6f, 0f);
            AddLight(coreLight, new Color(0.55f, 0.8f, 1f), 11f, 1.2f);

            // ---- Cover ----------------------------------------------------------------------------------------------

            GameObject StoneCover(string name, Vector3 centre, Vector3 size)
            {
                var block = Box(name, new Vector3(centre.x, size.y * 0.5f, centre.z), size, stone);
                var cap = Box(name + " Snow", new Vector3(centre.x, size.y + 0.08f, centre.z), new Vector3(size.x + 0.2f, 0.16f, size.z + 0.2f), snow);
                RemoveCollider(cap);
                var rime = Box(name + " Rime", new Vector3(centre.x, 0.2f, centre.z), new Vector3(size.x + 0.16f, 0.4f, size.z + 0.16f), frost);
                RemoveCollider(rime);
                footprints.Add((centre, size, 0f));
                return block;
            }

            void Ribs(string name, Vector3 centre, Vector3 size, float height)
            {
                // Gold ribs across the long face of a wall.
                bool alongZ = size.z > size.x;
                float length = alongZ ? size.z : size.x;
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 offset = (alongZ ? Vector3.forward : Vector3.right) * (k * length * 0.33f);
                    Vector3 ribSize = alongZ ? new Vector3(size.x + 0.2f, height - 0.1f, 0.25f) : new Vector3(0.25f, height - 0.1f, size.z + 0.2f);
                    var rib = Box($"{name} Rib {k}", centre + offset + Vector3.up * (height * 0.5f), ribSize, gold);
                    RemoveCollider(rib);
                }
            }

            // A bulwark in front of each doorway (nothing slides out through a doorway into a ready room).
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 c = new Vector3(sx * 18.5f, 0f, 0f), size = new Vector3(1.1f, 1.8f, 4.2f);
                StoneCover($"Bulwark {(sx < 0 ? "W" : "E")}", c, size);
                Ribs($"Bulwark {sx}", c, size, 1.8f);
            }

            // Four screen walls around the centre, on the axes: the inner ring of hard cover. Caskets sit between them.
            for (int k = 0; k < 4; k++)
            {
                float angle = k * 90f;
                Vector3 c = RadialPos(10.5f, angle, 0f);
                bool alongZ = k % 2 == 0; // the east and west walls run north-south
                Vector3 size = alongZ ? new Vector3(1.1f, 1.8f, 4.4f) : new Vector3(4.4f, 1.8f, 1.1f);
                StoneCover($"Screen Wall {k}", c, size);
                Ribs($"Screen Wall {k}", c, size, 1.8f);
            }

            // Broken altar walls, north and south, with frozen candles along the top.
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 c = new Vector3(0f, 0f, sz * 19f);
                StoneCover($"Altar Wall {(sz < 0 ? "S" : "N")}", c, new Vector3(5.6f, 1.6f, 1.1f));
                var cloth = Box($"Altar Cloth {sz}", new Vector3(0f, 1.0f, c.z - sz * 0.57f), new Vector3(2.2f, 1.1f, 0.04f), gold);
                RemoveCollider(cloth);
                for (int k = -2; k <= 2; k++)
                {
                    var candle = Box($"Altar Candle {sz}.{k}", new Vector3(k * 1.1f, 1.95f, c.z), new Vector3(0.12f, 0.3f, 0.12f), snow);
                    RemoveCollider(candle);
                    var wick = Box($"Altar Flame {sz}.{k}", new Vector3(k * 1.1f, 2.16f, c.z), new Vector3(0.08f, 0.12f, 0.08f), flame);
                    RemoveCollider(wick);
                }
                var lightGo = new GameObject($"Altar Light {sz}");
                lightGo.transform.position = new Vector3(0f, 2.6f, c.z);
                AddLight(lightGo, new Color(1f, 0.65f, 0.3f), 7f, 1.1f);
            }

            // Eight pillars sheathed in ice, in a ring between the screen walls and the outer wall (30 degrees either side
            // of each axis, so the ring is mirror symmetric on both).
            float[] pillarAngles = { 30f, 60f, 120f, 150f, 210f, 240f, 300f, 330f };
            for (int i = 0; i < pillarAngles.Length; i++)
            {
                string q = $"{i}";
                Vector3 c = RadialPos(16f, pillarAngles[i], 0f);
                var core = Box($"Pillar {q}", new Vector3(c.x, 2.2f, c.z), new Vector3(1.4f, 4.4f, 1.4f), stone);
                footprints.Add((c, new Vector3(1.8f, 0f, 1.8f), 0f));
                core.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                var sheath = Box($"Pillar Ice {q}", new Vector3(c.x, 1.3f, c.z), new Vector3(1.75f, 2.6f, 1.75f), casketIce);
                sheath.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
                RemoveCollider(sheath);
                var capital = Box($"Pillar Capital {q}", new Vector3(c.x, 4.5f, c.z), new Vector3(1.9f, 0.3f, 1.9f), stoneDark);
                capital.transform.rotation = core.transform.rotation;
                RemoveCollider(capital);
                var drift = Box($"Pillar Snow {q}", new Vector3(c.x, 4.7f, c.z), new Vector3(1.8f, 0.14f, 1.8f), snow);
                drift.transform.rotation = core.transform.rotation;
                RemoveCollider(drift);
                for (int k = 0; k < 5; k++) // icicles hanging off the capital
                {
                    float a = k * 72f + (float)rng.NextDouble() * 20f;
                    Vector3 p = c + RadialDir(a) * 0.95f;
                    float h = 0.4f + (float)rng.NextDouble() * 0.6f;
                    var icicle = Box($"Pillar Icicle {q}.{k}", new Vector3(p.x, 4.35f - h * 0.5f, p.z), new Vector3(0.1f, h, 0.1f), frost);
                    RemoveCollider(icicle);
                }
            }

            // ---- The frozen reliquaries -----------------------------------------------------------------------------
            var fieldRoot = new GameObject("Reliquary Caskets");
            var caskets = new System.Collections.Generic.List<ReliquaryCasket>();
            int n = 0;
            for (int sz = -1; sz <= 1; sz += 2)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 c = new Vector3(sx * 6.5f, 0f, sz * 6.5f);

                // The plinth stays when the casket is gone.
                var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                plinth.name = $"Casket Plinth {n}";
                plinth.transform.position = new Vector3(c.x, 0.1f, c.z);
                footprints.Add((c, new Vector3(2.4f, 0f, 2.4f), 0f));
                plinth.transform.localScale = new Vector3(2.5f, 0.03f, 2.5f);
                plinth.GetComponent<Renderer>().sharedMaterial = gold;
                RemoveCollider(plinth);
                var plinthTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                plinthTop.name = $"Casket Plinth Top {n}";
                plinthTop.transform.position = new Vector3(c.x, 0.12f, c.z);
                plinthTop.transform.localScale = new Vector3(2.2f, 0.03f, 2.2f);
                plinthTop.GetComponent<Renderer>().sharedMaterial = stoneDark;
                RemoveCollider(plinthTop);

                var root = new GameObject($"Reliquary Casket {n}");
                root.transform.position = c;
                root.transform.SetParent(fieldRoot.transform, true);
                var solid = root.AddComponent<BoxCollider>();
                solid.center = new Vector3(0f, 0.55f, 0f);
                solid.size = new Vector3(ReliquaryCasket.HalfSize * 2f, 1.1f, ReliquaryCasket.HalfSize * 2f);

                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                void Part(string name, Vector3 local, Vector3 size, Material m, float yaw = 0f)
                {
                    var p = Box(name, root.transform.TransformPoint(local), size, m);
                    p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    RemoveCollider(p);
                    p.transform.SetParent(body.transform, true);
                }
                // The saint's casket inside, then the ice around it.
                Part("Coffin", new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.5f, 1.1f), coffin, 45f);
                Part("Coffin Lid", new Vector3(0f, 0.73f, 0f), new Vector3(0.62f, 0.08f, 1.16f), gold, 45f);
                Part("Coffin Aquila", new Vector3(0f, 0.79f, 0f), new Vector3(0.5f, 0.04f, 0.12f), gold, -45f);
                Part("Ice Block", new Vector3(0f, 0.55f, 0f), new Vector3(1.4f, 1.1f, 1.4f), casketIce);
                Part("Ice Cap", new Vector3(0f, 1.13f, 0f), new Vector3(1.3f, 0.08f, 1.3f), snow);
                for (int k = 0; k < 4; k++) // jagged frost at the corners
                {
                    Vector3 corner = new Vector3((k % 2 == 0 ? 1f : -1f) * 0.62f, 0.65f, (k < 2 ? 1f : -1f) * 0.62f);
                    Part($"Ice Spur {k}", corner, new Vector3(0.22f, 1.3f, 0.22f), frost, 30f * k);
                }

                var casket = root.AddComponent<ReliquaryCasket>();
                casket.Configure(body.transform);
                caskets.Add(casket);

                var glowGo = new GameObject("Casket Glow");
                glowGo.transform.SetParent(body.transform, false);
                glowGo.transform.localPosition = Vector3.up * 0.6f;
                AddLight(glowGo, new Color(0.5f, 0.8f, 1f), 4f, 0.9f);
                n++;
            }
            fieldRoot.AddComponent<CasketField>().Configure(caskets.ToArray());

            // ---- Ice plates over freezing water --------------------------------------------------------------------
            // Clear ice can be smashed by slams (DestructibleFloor in frozen-water mode): the hole is open water that
            // slows anyone in it by 25% (no damage) until it freezes over again. Packed snow can't break: the centre,
            // everything under cover, the casket plinths and the doorway approaches.
            const float Spacing = 2.4f;   // plate grid
            const float Plate = 2.3f;     // plate size: the gap between plates shows the dark bed, like cracks
            const float PlateTop = 0.08f;
            const float PlateThickness = 0.3f;
            Material iceMid = GetMaterial("HoarfrostIceMid", new Color(0.52f, 0.66f, 0.78f));
            Material packed = GetMaterial("HoarfrostPacked", new Color(0.82f, 0.85f, 0.9f));
            Material packedAlt = GetMaterial("HoarfrostPackedAlt", new Color(0.76f, 0.8f, 0.86f));
            Material[] iceShades = { ice, iceMid, iceLight };

            bool UnderCover(Vector2 c)
            {
                float half = Plate * 0.4f;
                foreach (var f in footprints)
                {
                    Vector3 local = Quaternion.Euler(0f, -f.yaw, 0f) * (new Vector3(c.x, 0f, c.y) - new Vector3(f.centre.x, 0f, f.centre.z));
                    if (Mathf.Abs(local.x) <= f.size.x * 0.5f + half && Mathf.Abs(local.z) <= f.size.z * 0.5f + half) return true;
                }
                return false;
            }

            var iceRoot = new GameObject("Breakable Ice");
            var packedRoot = new GameObject("Packed Snow");
            var plates = new System.Collections.Generic.List<Transform>();
            var cells = new System.Collections.Generic.List<Vector2Int>();
            int extent = Mathf.CeilToInt((ArenaRadius + 1f) / Spacing);
            for (int gz = -extent; gz <= extent; gz++)
            for (int gx = -extent; gx <= extent; gx++)
            {
                var c = new Vector2(gx * Spacing, gz * Spacing);
                float r = c.magnitude;
                if (r > ArenaRadius + 0.8f) continue; // the outermost plates tuck under the wall

                bool solid = r < 3.4f
                             || (Mathf.Abs(c.y) < 3.7f && Mathf.Abs(c.x) > ArenaRadius - 3.5f) // doorway approaches
                             || UnderCover(c);

                Material m = solid ? ((gx + gz) % 2 == 0 ? packed : packedAlt) : iceShades[rng.Next(iceShades.Length)];
                float size = solid ? Spacing + 0.01f : Plate;
                var plate = Box($"Ice {gx},{gz}", new Vector3(c.x, PlateTop - PlateThickness * 0.5f, c.y), new Vector3(size, PlateThickness, size), m);
                RemoveCollider(plate); // everyone walks on the ground collider; the plates are only the look
                plate.transform.SetParent(solid ? packedRoot.transform : iceRoot.transform, true);
                if (solid) continue;
                // White fracture lines across some of the ice, so it reads apart from the packed snow.
                if (rng.NextDouble() < 0.45)
                {
                    var line = Box("Fracture", Vector3.zero, Vector3.one, crack);
                    RemoveCollider(line);
                    line.transform.SetParent(plate.transform, false);
                    line.transform.localScale = new Vector3(0.75f, 0.04f, 0.025f);
                    line.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    line.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
                }
                plates.Add(plate.transform);
                cells.Add(new Vector2Int(gx, gz));
            }
            var iceFloor = iceRoot.AddComponent<DestructibleFloor>();
            iceFloor.Configure(Spacing, plates.ToArray(), cells.ToArray());
            iceFloor.ConfigureFrozenWater(0.25f, 20f);

            // ---- Perimeter: snow-capped stone with icicles, buttresses and frosted lancet windows -------------------
            float chord = 2f * (ArenaRadius + 1f) * Mathf.Tan(Mathf.PI / WallSegments) + 0.25f;
            for (int i = 0; i < WallSegments; i++)
            {
                if (readyRooms && IsDoorSegment(i)) continue;
                float angle = i * 360f / WallSegments;
                Quaternion rot = Quaternion.Euler(0f, 90f - angle, 0f);

                var seg = Box($"Wall {i}", RadialPos(ArenaRadius + 1.1f, angle, 1.7f), new Vector3(chord, 3.4f, 1.6f), stone);
                seg.transform.rotation = rot;
                var cap = Box($"Wall Snow {i}", RadialPos(ArenaRadius + 1.1f, angle, 3.5f), new Vector3(chord, 0.3f, 1.9f), snow);
                cap.transform.rotation = rot;
                RemoveCollider(cap);
                var rime = Box($"Wall Rime {i}", RadialPos(ArenaRadius + 0.28f, angle, 0.25f), new Vector3(chord, 0.5f, 0.2f), frost);
                rime.transform.rotation = rot;
                RemoveCollider(rime);
                for (int k = 0; k < 4; k++)
                {
                    float h = 0.3f + (float)rng.NextDouble() * 0.7f;
                    Vector3 p = RadialPos(ArenaRadius + 0.3f, angle, 3.35f - h * 0.5f) + rot * Vector3.right * (((float)rng.NextDouble() - 0.5f) * chord * 0.9f);
                    var icicle = Box($"Wall Icicle {i}.{k}", p, new Vector3(0.09f, h, 0.09f), frost);
                    RemoveCollider(icicle);
                }

                if (i % 2 == 0)
                {
                    var buttress = Box($"Buttress {i}", RadialPos(ArenaRadius + 2.4f, angle, 2.6f), new Vector3(1f, 5.2f, 1.6f), stoneDark);
                    buttress.transform.rotation = rot;
                    RemoveCollider(buttress);
                    var spire = Box($"Buttress Snow {i}", RadialPos(ArenaRadius + 2.4f, angle, 5.3f), new Vector3(1.1f, 0.2f, 1.7f), snow);
                    spire.transform.rotation = rot;
                    RemoveCollider(spire);
                }
                else
                {
                    var lancet = Box($"Lancet {i}", RadialPos(ArenaRadius + 0.26f, angle, 1.9f), new Vector3(0.8f, 2.2f, 0.06f), window);
                    lancet.transform.rotation = rot;
                    RemoveCollider(lancet);
                }

                // Snow drifted against the foot of the wall.
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = RadialPos(ArenaRadius - 0.6f - (float)rng.NextDouble() * 0.8f, angle + ((float)rng.NextDouble() - 0.5f) * 12f, 0.1f);
                    var drift = Box($"Snow Drift {i}.{k}", p, new Vector3(1.6f + (float)rng.NextDouble() * 2.2f, 0.12f, 0.9f + (float)rng.NextDouble() * 0.8f), snow);
                    drift.transform.rotation = rot * Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 30f, 0f);
                    RemoveCollider(drift);
                    if (readyRooms && InReadyRoomZone(p + RadialDir(angle) * 2f)) Object.DestroyImmediate(drift);
                }
            }

            // Warm braziers in the four corners, cold light around the edge.
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = RadialPos(ArenaRadius - 2.5f, 45f + i * 90f, 0f);
                var root = new GameObject($"Brazier {i}");
                root.transform.position = p;
                foreach (var (name, local, size, m) in new[]
                         {
                             ("Stem", new Vector3(0f, 0.55f, 0f), new Vector3(0.16f, 1.1f, 0.16f), stoneDark),
                             ("Bowl", new Vector3(0f, 1.15f, 0f), new Vector3(0.7f, 0.3f, 0.7f), gold),
                             ("Flame", new Vector3(0f, 1.5f, 0f), new Vector3(0.32f, 0.5f, 0.32f), flame)
                         })
                {
                    var part = Box($"Brazier {i} {name}", p + local, size, m);
                    part.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                    RemoveCollider(part);
                    part.transform.SetParent(root.transform, true);
                }
                var lightGo = new GameObject("Brazier Light");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = Vector3.up * 1.7f;
                AddLight(lightGo, new Color(1f, 0.6f, 0.25f), 8f, 1.4f);
            }
            for (int i = 0; i < 8; i++)
            {
                var lightGo = new GameObject($"Frost Light {i}");
                lightGo.transform.position = RadialPos(ArenaRadius - 2.5f, 22.5f + i * 45f, 2.4f);
                AddLight(lightGo, new Color(0.55f, 0.78f, 1f), 10f, 0.9f);
            }

            // ---- Outside: snow-covered spires rising out of the frozen lake -------------------------------------------
            for (int i = 0; i < 22; i++)
            {
                float angle = i * 16.4f + (float)rng.NextDouble() * 6f;
                float radius = 36f + (float)rng.NextDouble() * 10f;
                float h = 9f + (float)rng.NextDouble() * 14f;
                Vector3 p = RadialPos(radius, angle, h * 0.5f);
                if (readyRooms && InReadyRoomZone(p)) continue;
                float w = 2.2f + (float)rng.NextDouble() * 2f;
                var tower = Box($"Spire {i}", p, new Vector3(w, h, w), stoneDark);
                tower.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);
                RemoveCollider(tower);
                var roof = Box($"Spire Snow {i}", new Vector3(p.x, h + 0.6f, p.z), new Vector3(w * 0.7f, 1.2f, w * 0.7f), snow);
                roof.transform.rotation = tower.transform.rotation * Quaternion.Euler(0f, 45f, 0f);
                RemoveCollider(roof);
                var glass = Box($"Spire Window {i}", RadialPos(radius - w * 0.5f - 0.05f, angle, h * 0.7f), new Vector3(0.5f, h * 0.25f, 0.5f), window);
                RemoveCollider(glass);
            }

            if (readyRooms) CreateReadyRooms(ArenaRadius, stone, stone, stoneDark, 3.4f);
        }

        // Two ready rooms outside the perimeter wall, one through each doorway (-X "left" and +X "right"). Each is a walled
        // pen with a floor, a glowing threshold and a gate that ReadyRoom raises/lowers with the ready phase. Local space:
        // the root sits on the wall line with +Z pointing out of the arena (see ReadyRoom).
        static void CreateReadyRooms(float arenaRadius, Material floorMat, Material wallMat, Material capMat, float wallHeight)
        {
            Material gateMat = GetUnlitMaterial("ReadyGate", new Color(0.95f, 0.75f, 0.3f, 0.55f));
            Material edgeMat = GetUnlitMaterial("ReadyRoomEdge", new Color(0.95f, 0.75f, 0.3f));

            for (int side = 0; side < 2; side++)
            {
                float angle = side == 0 ? 180f : 0f;
                string label = side == 0 ? "Left" : "Right";

                var root = new GameObject($"Ready Room {label}");
                root.transform.position = RadialPos(arenaRadius + 1.1f, angle, 0f);
                root.transform.rotation = Quaternion.LookRotation(RadialDir(angle), Vector3.up);
                Transform t = root.transform;

                float length = ReadyRoom.FarZ + 0.5f;   // from the wall line to the end wall
                float side0 = -0.5f;                    // side walls start inside the neighbouring perimeter segments
                float sideLen = length - side0;
                float sideX = ReadyRoom.HalfWidth + 0.55f;

                RoomBox(t, "Floor", new Vector3(0f, -0.45f, side0 + sideLen * 0.5f), new Vector3(sideX * 2f, 1f, sideLen), floorMat, false); // walked on via the ground collider
                RoomBox(t, "Threshold Glow", new Vector3(0f, 0.06f, 1.6f), new Vector3(ReadyRoom.HalfWidth * 2f, 0.04f, 0.3f), edgeMat, false);
                for (int s = -1; s <= 1; s += 2)
                {
                    RoomBox(t, $"Side Wall {s}", new Vector3(s * sideX, wallHeight * 0.5f, side0 + sideLen * 0.5f), new Vector3(1.1f, wallHeight, sideLen), wallMat);
                    RoomBox(t, $"Side Cap {s}", new Vector3(s * sideX, wallHeight + 0.12f, side0 + sideLen * 0.5f), new Vector3(1.4f, 0.25f, sideLen + 0.3f), capMat, false);
                }
                RoomBox(t, "End Wall", new Vector3(0f, wallHeight * 0.5f, length), new Vector3(sideX * 2f + 1.1f, wallHeight, 1.1f), wallMat);
                RoomBox(t, "End Cap", new Vector3(0f, wallHeight + 0.12f, length), new Vector3(sideX * 2f + 1.4f, 0.25f, 1.4f), capMat, false);
                RoomBox(t, "End Glow", new Vector3(0f, 0.12f, length - 0.65f), new Vector3(sideX * 2f - 1f, 0.1f, 0.14f), edgeMat, false);

                var lightGo = new GameObject("Room Light");
                lightGo.transform.SetParent(t, false);
                lightGo.transform.localPosition = new Vector3(0f, 3.5f, 7f);
                AddLight(lightGo, new Color(0.95f, 0.75f, 0.3f), 15f, 1.3f);

                // The gate fills the doorway between the two neighbouring wall segments (a little wider, so it bites into them).
                float gateH = wallHeight + 0.6f;
                RoomBox(t, "Gate", new Vector3(0f, gateH * 0.5f, 0f), new Vector3(7.4f, gateH, 0.5f), gateMat);

                root.AddComponent<ReadyRoom>();
            }
        }

        // A box in a ready room's local space, parented to it.
        static GameObject RoomBox(Transform room, string name, Vector3 local, Vector3 scale, Material material, bool collider = true)
        {
            GameObject box = Box(name, room.TransformPoint(local), scale, material);
            box.transform.rotation = room.rotation;
            box.transform.SetParent(room, true);
            if (!collider) RemoveCollider(box);
            return box;
        }

        static void AddLight(GameObject go, Color color, float range, float intensity)
        {
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        // Stone bowl on a tripod with a burning green flame and an optional light.
        static void AddBrazier(string name, Vector3 position, Material body, Material flame, bool withLight)
        {
            var root = new GameObject(name);
            root.transform.position = position;

            var stem = Box(name + " Stem", position + Vector3.up * 0.55f, new Vector3(0.16f, 1.1f, 0.16f), body);
            stem.transform.SetParent(root.transform, true);
            RemoveCollider(stem);
            var bowl = Box(name + " Bowl", position + Vector3.up * 1.15f, new Vector3(0.7f, 0.3f, 0.7f), body);
            bowl.transform.SetParent(root.transform, true);
            RemoveCollider(bowl);
            var fire = Box(name + " Flame", position + Vector3.up * 1.5f, new Vector3(0.32f, 0.5f, 0.32f), flame);
            fire.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            fire.transform.SetParent(root.transform, true);
            RemoveCollider(fire);

            if (!withLight) return;
            var lightGo = new GameObject("Brazier Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = Vector3.up * 1.7f;
            AddLight(lightGo, new Color(0.35f, 1f, 0.35f), 7f, 1.3f);
        }

        // Khorne dressing for the shrine: blood-red sigils on the floor and walls, skeletons slumped and impaled around the arena.
        static void AddChaosDressing(System.Random rng, float arenaRadius, int wallSegments, Material bone, Material iron, float plateHeight, bool readyRooms = false, Material floor = null, bool centreInsignia = true)
        {
            Material blood = GetUnlitMaterial("ChaosBlood", new Color(0.75f, 0.04f, 0.03f));
            Material poolMat = GetMaterial("ChaosPool", new Color(0.16f, 0.01f, 0.01f));
            var gore = new System.Random(1337); // its own stream, so the layout below doesn't shift the skeleton placement

            // The Khorne insignia (cut out of the reference image) laid in the middle of the arena, on a pool of blood. It is
            // the same shape carved into the floor: a lit, floor-coloured material with the original's bevels as relief.
            // (The original rusted and enamel-pin versions are also in Assets/Textures/Khorne, currently unused.)
            Disc("Chaos Blood Pool", 14f, 0.035f, poolMat);
            if (centreInsignia) AddInsigniaDecal("Khorne Insignia", "KhorneInsigniaFloor", new Vector3(0f, 0.125f, 0f), 13f, floor);
            var sigilLight = new GameObject("Chaos Sigil Light");
            sigilLight.transform.position = new Vector3(0f, 0.8f, 0f);
            AddLight(sigilLight, new Color(1f, 0.1f, 0.05f), 14f, 1.1f);

            // Between the cover: pools of blood, alternating with a corpse lying in its own pool.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f + 22.5f;
                Vector3 pos = RadialPos(i % 2 == 0 ? 16f : 24f, angle, 0f);
                if (i % 2 == 0)
                {
                    AddBloodPool($"Blood Pool {i}", pos, 2.4f, gore, poolMat);
                }
                else
                {
                    AddBloodPool($"Blood Pool {i}", pos, 1.7f, gore, poolMat);
                    AddSkeleton($"Skeleton Slain {i}", pos, (float)gore.NextDouble() * 360f, SkeletonPose.Sprawled, bone, iron);
                }
            }

            // Blood run down the wall in place of the old plates: a few drips and a pool at the foot.
            for (int i = 0; i < wallSegments; i += 3)
            {
                if (readyRooms && IsDoorSegment(i)) continue; // that segment is a ready-room doorway
                float angle = i * 360f / wallSegments;
                Quaternion rot = Quaternion.Euler(0f, 90f - angle, 0f);
                float top = plateHeight + 0.7f;
                for (int d = -1; d <= 1; d++)
                {
                    float length = top - (d == 0 ? 0f : (float)gore.NextDouble() * top * 0.45f);
                    float width = d == 0 ? 0.42f : 0.16f + (float)gore.NextDouble() * 0.1f;
                    Vector3 tangent = new Vector3(-Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
                    Vector3 pos = RadialPos(arenaRadius + 0.2f, angle, top - length * 0.5f + 0.05f) + tangent * (d * 0.45f);
                    var drip = Box($"Wall Blood {i}.{d}", pos, new Vector3(width, length, 0.04f), blood);
                    drip.transform.rotation = rot;
                    RemoveCollider(drip);
                }
                AddBloodPool($"Wall Blood Pool {i}", RadialPos(arenaRadius - 0.6f, angle, 0f), 1.6f, gore, poolMat);
            }

            // Skeletons: slumped against the wall, sprawled on the floor and impaled on spikes.
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 40f + (float)rng.NextDouble() * 20f;
                if (!BlocksDoorway(RadialPos(arenaRadius - 1.2f, angle, 0f), readyRooms))
                    AddSkeleton($"Skeleton Slumped {i}", RadialPos(arenaRadius - 1.2f, angle, 0f), 90f - angle, SkeletonPose.Slumped, bone, iron);
            }
            for (int i = 0; i < 12; i++)
            {
                float angle = (float)rng.NextDouble() * 360f;
                float radius = 6f + (float)rng.NextDouble() * 19f;
                if (!BlocksDoorway(RadialPos(radius, angle, 0f), readyRooms))
                    AddSkeleton($"Skeleton Fallen {i}", RadialPos(radius, angle, 0f), (float)rng.NextDouble() * 360f, SkeletonPose.Sprawled, bone, iron);
            }
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f + 15f;
                var pike = Box($"Impaling Spike {i}", RadialPos(17.5f, angle, 1.4f), new Vector3(0.1f, 2.8f, 0.1f), iron);
                RemoveCollider(pike);
                AddSkeleton($"Skeleton Impaled {i}", RadialPos(17.5f, angle, 0.9f), angle * 2f, SkeletonPose.Impaled, bone, iron);
            }
        }

        static bool BlocksDoorway(Vector3 p, bool readyRooms) => readyRooms && Mathf.Abs(p.z) < 7f && Mathf.Abs(p.x) > 18f;

        enum SkeletonPose { Slumped, Sprawled, Impaled }

        const string InsigniaDir = "Assets/Textures/Khorne";

        // Material for one of the cut-out insignia PNGs (Assets/Textures/Khorne). With a floorTone it is a lit, alpha-cutout
        // version tinted with that floor material's colour, so the shape reads as carved into the floor; without one it is
        // an unlit transparent sprite. Makes sure the texture is imported with alpha transparency, clamped edges and no
        // power-of-two rescale (which would distort the shape).
        static Material GetInsigniaMaterial(string file, Material floorTone, out float aspect)
        {
            aspect = 1f;
            string texPath = $"{InsigniaDir}/{file}.png";
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(texPath) is TextureImporter importer
                && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (texture == null)
            {
                Debug.LogWarning($"Missing insignia texture {texPath}; skipping that decal.");
                return null;
            }
            aspect = texture.width / (float)texture.height;

            string matPath = floorTone != null ? $"{MaterialDir}/Insignia_{file}_{floorTone.name}.mat" : $"{MaterialDir}/Insignia_{file}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialDir);
                Shader shader = Shader.Find("Sprites/Default");
                if (floorTone != null) shader = floorTone.shader; // the same shader as the floor, so lighting matches
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.mainTexture = texture;
            material.color = floorTone != null ? floorTone.color : Color.white;
            if (floorTone != null && material.shader.name == "Standard")
            {
                // Standard shader's "Cutout" rendering mode: solid where the texture is opaque, nothing elsewhere.
                material.SetFloat("_Mode", 1f);
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        // A flat decal of an insignia image lying on the floor, `width` metres wide, upright on screen (top towards +Z).
        // Pass the floor material to carve it into the floor (lit, floor-coloured) instead of an unlit sprite.
        static void AddInsigniaDecal(string name, string file, Vector3 centre, float width, Material floorTone = null)
        {
            Material material = GetInsigniaMaterial(file, floorTone, out float aspect);
            if (material == null) return;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.position = centre;
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(width, width / aspect, 1f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = floorTone != null; // carved into the floor, so it takes the same shadows
            RemoveCollider(quad);
        }

        // An irregular pool of blood: a handful of overlapping flat discs.
        static void AddBloodPool(string name, Vector3 centre, float radius, System.Random rng, Material material)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(centre.x, 0f, centre.z);
            int lumps = 5;
            for (int i = 0; i < lumps; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = i == 0 ? 0f : (float)rng.NextDouble() * radius * 0.5f;
                float d = radius * (i == 0 ? 1.1f : 0.5f + (float)rng.NextDouble() * 0.5f);
                var lump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lump.name = "Pool Lump";
                lump.transform.SetParent(root.transform, false);
                lump.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.09f + i * 0.002f, Mathf.Sin(a) * r); // above the floor rings (top at 0.08)
                lump.transform.localScale = new Vector3(d, 0.005f, d * (0.7f + (float)rng.NextDouble() * 0.5f));
                lump.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
                lump.GetComponent<Renderer>().sharedMaterial = material;
                lump.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                RemoveCollider(lump);
            }
        }

        // Blocky skeleton from bone cubes: skull, jaw, spine, ribs, pelvis and limbs. Built upright, then posed.
        static void AddSkeleton(string name, Vector3 position, float yaw, SkeletonPose pose, Material bone, Material metal)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            switch (pose)
            {
                case SkeletonPose.Sprawled: // lying on its back
                    body.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                    body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case SkeletonPose.Slumped: // sitting up against the wall, tilted back
                    body.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                    body.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                    break;
                default: // dangling off a spike
                    body.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
                    break;
            }

            void B(string part, Vector3 local, Vector3 scale, Vector3 euler)
            {
                var p = Box($"{name} {part}", Vector3.zero, scale, bone);
                p.transform.SetParent(body.transform, false);
                p.transform.localPosition = local;
                p.transform.localRotation = Quaternion.Euler(euler);
                RemoveCollider(p);
            }

            bool sit = pose == SkeletonPose.Slumped;
            float seatDrop = sit ? -0.45f : 0f; // sitting lowers everything above the legs

            B("Skull", new Vector3(0f, 1.62f + seatDrop, 0f), new Vector3(0.24f, 0.26f, 0.26f), sit ? new Vector3(25f, 0f, 0f) : Vector3.zero);
            B("Jaw", new Vector3(0f, 1.46f + seatDrop, 0.05f), new Vector3(0.18f, 0.07f, 0.2f), new Vector3(pose == SkeletonPose.Impaled ? 22f : 8f, 0f, 0f));
            B("Spine", new Vector3(0f, 1.1f + seatDrop, 0f), new Vector3(0.07f, 0.7f, 0.07f), Vector3.zero);
            for (int r = 0; r < 4; r++)
                B($"Rib {r}", new Vector3(0f, 1.32f - r * 0.12f + seatDrop, 0f), new Vector3(0.42f - r * 0.04f, 0.04f, 0.24f), Vector3.zero);
            B("Pelvis", new Vector3(0f, 0.72f + seatDrop, 0f), new Vector3(0.34f, 0.12f, 0.2f), Vector3.zero);

            for (int side = -1; side <= 1; side += 2)
            {
                B($"Arm {side}", new Vector3(side * 0.3f, 1.12f + seatDrop, 0f), new Vector3(0.06f, 0.6f, 0.06f), new Vector3(0f, 0f, side * (pose == SkeletonPose.Impaled ? 12f : 28f)));
                if (sit)
                {
                    B($"Thigh {side}", new Vector3(side * 0.12f, 0.3f, 0.3f), new Vector3(0.08f, 0.08f, 0.6f), new Vector3(0f, side * 10f, 0f));
                    B($"Shin {side}", new Vector3(side * 0.14f, 0.34f, 0.6f), new Vector3(0.07f, 0.7f, 0.07f), new Vector3(-8f, 0f, 0f));
                }
                else
                {
                    B($"Leg {side}", new Vector3(side * 0.12f, 0.36f, 0f), new Vector3(0.08f, 0.72f, 0.08f), new Vector3(0f, 0f, side * (pose == SkeletonPose.Impaled ? 4f : 14f)));
                }
            }

            if (pose == SkeletonPose.Sprawled)
            {
                var blade = Box($"{name} Rusted Blade", Vector3.zero, new Vector3(0.08f, 0.02f, 0.9f), metal);
                blade.transform.SetParent(root.transform, false);
                blade.transform.localPosition = new Vector3(0.45f, 0.03f, 0.2f);
                blade.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
                RemoveCollider(blade);
            }
        }

        // Floating skull with a jaw, glowing lens, antenna and a small lamp; ServoSkull animates it at runtime.
        static void AddServoSkull(string name, Vector3 position, float orbit, float orbitSpeed, Color lampColor, Material bone, Material metal)
        {
            var root = new GameObject(name);
            root.transform.position = position;

            GameObject Part(PrimitiveType type, string partName, Vector3 localPos, Vector3 scale, Material m)
            {
                var p = GameObject.CreatePrimitive(type);
                p.name = partName;
                p.transform.SetParent(root.transform, false);
                p.transform.localPosition = localPos;
                p.transform.localScale = scale;
                p.GetComponent<Renderer>().sharedMaterial = m;
                RemoveCollider(p);
                return p;
            }

            Material dark = GetMaterial("ServoSocket", new Color(0.02f, 0.02f, 0.02f));
            Material lens = GetUnlitMaterial(lampColor.r > lampColor.g ? "ServoLensRed" : "ServoLensGreen", lampColor);

            Part(PrimitiveType.Sphere, "Cranium", Vector3.zero, new Vector3(0.5f, 0.46f, 0.52f), bone);
            Part(PrimitiveType.Cube, "Jaw", new Vector3(0f, -0.27f, 0.1f), new Vector3(0.3f, 0.12f, 0.3f), bone);
            Part(PrimitiveType.Sphere, "Socket L", new Vector3(-0.1f, -0.02f, 0.2f), new Vector3(0.14f, 0.15f, 0.12f), dark);
            Part(PrimitiveType.Sphere, "Socket R", new Vector3(0.1f, -0.02f, 0.2f), new Vector3(0.14f, 0.15f, 0.12f), dark);
            Part(PrimitiveType.Sphere, "Lens", new Vector3(0.1f, -0.02f, 0.25f), new Vector3(0.08f, 0.08f, 0.08f), lens);
            Part(PrimitiveType.Cube, "Cog Plate", new Vector3(-0.22f, 0.08f, 0f), new Vector3(0.06f, 0.26f, 0.26f), metal);
            Part(PrimitiveType.Cube, "Antenna", new Vector3(0.14f, 0.34f, -0.08f), new Vector3(0.03f, 0.3f, 0.03f), metal);
            Part(PrimitiveType.Cube, "Servo Arm", new Vector3(0f, -0.1f, -0.32f), new Vector3(0.06f, 0.06f, 0.3f), metal);

            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            AddLight(lightGo, lampColor, 6f, 1.1f);

            root.AddComponent<ServoSkull>().Configure(orbit, orbitSpeed);
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
            bool hospitaller = character == Character.Hospitaller;
            bool mandrake = character == Character.Mandrake;
            bool bullgryn = character == Character.Bullgryn;
            // The Sorcerer is a Space Marine in power armour under a tall crested helm: a marine-sized body.
            bool sorcerer = character == Character.Sorcerer;
            // The Sanguinary Priest is a Blood Angels marine: a marine-sized body.
            bool priest = character == Character.SanguinaryPriest;
            bool small = psyker || ranger || hospitaller || mandrake;
            controller.height = ranger ? 1.9f : psyker ? 1.8f : hospitaller ? 1.85f : mandrake ? 1.9f : bullgryn ? 2.6f : 2f;
            // The Bullgryn is the biggest body on the roster (~2.9m model), so he is also the widest target.
            controller.radius = small ? 0.4f : bullgryn ? 0.6f : 0.5f;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            root.AddComponent<Health>().Configure(character == Character.KhorneBerserker ? 215f : ranger ? 150f : psyker ? 145f : hospitaller ? 160f : mandrake ? 155f : bullgryn ? 240f : sorcerer ? 165f : priest ? 170f : 175f, 1);
            root.AddComponent<HealthBar>().Configure(ranger ? 2.5f : psyker ? 2.3f : hospitaller ? 2.4f : mandrake ? 2.45f : bullgryn ? 3.2f : sorcerer ? 3.0f : 2.7f);
            root.AddComponent<Energy>();
            root.AddComponent<EnergyBar>().Configure(ranger ? 2.3f : psyker ? 2.1f : hospitaller ? 2.2f : mandrake ? 2.25f : bullgryn ? 3.0f : sorcerer ? 2.8f : 2.5f);
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
                root.AddComponent<PestilentFly>();
                root.AddComponent<PoxFlask>();
                root.AddComponent<PlagueCloud>();
                root.AddComponent<WingedEscape>();
                root.AddComponent<PestilentBurst>();
                root.AddComponent<WardOfPlague>();
            }
            else if (ranger)
            {
                // Long-range sniper: a quick shot, a chargeable piercing root shot, shrapnel / shotgun, a disengage, a
                // hidden stunning cog-mine (Noospheric Snare) and a 4-shot volley.
                root.AddComponent<GalvanicShot>();
                root.AddComponent<TransuranicShot>();
                root.AddComponent<ShrapnelField>();
                root.AddComponent<Disengage>();
                root.AddComponent<RangerCounter>();
                root.AddComponent<NoosphericSnare>();
                root.AddComponent<CullingVolley>();
            }
            else if (hospitaller)
            {
                // Healer: a tether beam (burns enemies / shares an ally's pain), a placed heal pulse split between the
                // allies it catches, a knockback flare, diving doves that root, a counter, an area cleanse (Absolution)
                // and the Martyrdom ultimate.
                root.AddComponent<SanctifiedTether>();
                root.AddComponent<SanctusPulse>();
                root.AddComponent<BlindingRadiance>();
                root.AddComponent<DoveStrike>();
                root.AddComponent<MartyrsGrace>();
                root.AddComponent<Absolution>();
                root.AddComponent<Martyrdom>();
            }
            else if (mandrake)
            {
                // Stealth assassin: a rhythmic three-strike blade chain (the cleave poisons), a balefire bolt that bursts
                // on his next hit, a teleport that charges the next strike, a stealth veil (EX: shed every debuff), a
                // counter (Umbral Riposte) that steps behind the attacker, a stunning cut (Shadow Rend) and Shadow Cross.
                root.AddComponent<GlimmersteelBlade>();
                root.AddComponent<BalefireBolt>();
                root.AddComponent<ShadowJump>();
                root.AddComponent<ShadowVeil>();
                root.AddComponent<UmbralRiposte>();
                root.AddComponent<ShadowRend>();
                root.AddComponent<ShadowCross>();
                root.AddComponent<SoulHarvest>();
            }
            else if (bullgryn)
            {
                // Tank: a slab maul (or, after the round-1 Armament pick, a grenade launcher), a shield bash that
                // interrupts and slows, a carrying charge, a hooked chain, a held shield block on Space (EX: eat a ration)
                // and the Living Wall ultimate. Grit (the passive) builds from blocked damage. No counter.
                root.AddComponent<SlabMaul>();
                root.AddComponent<ShieldBash>();
                root.AddComponent<BullCharge>();
                root.AddComponent<ChainHaul>();
                root.AddComponent<Brace>();
                var wall = new SerializedObject(root.AddComponent<LivingWall>());
                wall.FindProperty("shieldModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BullgrynShieldModelPath);
                wall.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<Grit>();
            }
            else if (sorcerer)
            {
                // Time-and-sand support (no counter): bolts that lay the Curse of the Rubricae, a charged delayed heal, a
                // sand-form dash, a frontal Kine Shield that catches a bolt to throw back (EX: a reflecting Warp Rift), a
                // lobbed snaring hourglass, a petrifying touch and the Weaver of Fates ultimate.
                root.AddComponent<SandsOfProspero>();
                root.AddComponent<MendingSands>();
                root.AddComponent<KineShield>();
                root.AddComponent<SinkingSands>();
                root.AddComponent<DustForm>();
                root.AddComponent<DustglassTouch>();
                root.AddComponent<WeaverOfFates>();
                root.AddComponent<EchoesOfProspero>();
            }
            else if (priest)
            {
                // Melee battle-medic: chainsword cuts that heal the most injured teammate, a dash-stab that heals around
                // its target, a jump-pack leap, an area heal stronger in melee (EX: a big single heal), a counter and the
                // Wings of the Angel ultimate. Vitae (the passive) fills from healing and turns the next swing into the
                // Exsanguinating Arc.
                root.AddComponent<Chainsword>();
                root.AddComponent<SanguineLunge>();
                root.AddComponent<JumpPackDescent>();
                root.AddComponent<ChaliceOffering>();
                root.AddComponent<SanguineParry>();
                root.AddComponent<WingsOfTheAngel>();
                root.AddComponent<Vitae>();
            }
            else if (psyker)
            {
                // Glass-cannon caster: ranged bolts, chain lightning, a discharge that spends Charges, a damage-absorbing
                // barrier, a blink, a stunning shot (Cripple) and a channelled beam ultimate.
                root.AddComponent<WarpBolt>();
                root.AddComponent<ChainLightning>();
                root.AddComponent<Discharge>();
                root.AddComponent<PsychicBarrier>();
                root.AddComponent<WarpStep>();
                root.AddComponent<Cripple>();
                root.AddComponent<PsychicBlast>();
            }
            else
            {
                root.AddComponent<BasicAttack>();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(GreyKnightModelPath) != null)
                {
                    root.AddComponent<HalberdSweep>();
                    root.AddComponent<Smite>();
                    root.AddComponent<TeleportStrike>();
                    root.AddComponent<InterceptorCounter>();
                    root.AddComponent<Consecrate>();
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
                : hospitaller ? new[] { HospitallerModelPath }.Concat(PlayerModelPaths).ToArray()
                : mandrake ? new[] { MandrakeModelPath, PsykerModelPath }.Concat(PlayerModelPaths).ToArray()
                : bullgryn ? new[] { BullgrynModelPath }.Concat(PlayerModelPaths).ToArray()
                : sorcerer ? new[] { SorcererModelPath }.Concat(PlayerModelPaths).ToArray()
                : priest ? new[] { SanguinaryPriestModelPath }.Concat(PlayerModelPaths).ToArray()
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
                if (hospitaller)
                {
                    root.AddComponent<HospitallerPose>();
                    root.AddComponent<HospitallerDoves>();
                }
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

        internal static void CreateCamera(Transform target, int map = MapColosseum)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = map == MapShrine ? new Color(0.02f, 0.04f, 0.04f)
                : map == MapCrucible ? new Color(0.1f, 0.03f, 0.01f)
                : map == MapHoarfrost ? new Color(0.03f, 0.05f, 0.08f)
                : new Color(0.08f, 0.03f, 0.03f);
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

        // A dummy wearing a champion's model (built at runtime by CharacterDummy), facing the middle of the arena.
        static GameObject CreateCharacterDummy(string label, string prefabPath, Vector3 feet, float health, float height, float radius, float barHeight)
        {
            var dummy = new GameObject($"Training Dummy ({label})");
            Vector3 toCentre = new Vector3(-feet.x, 0f, -feet.z);
            dummy.transform.SetPositionAndRotation(feet, toCentre.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCentre) : Quaternion.identity);

            var capsule = dummy.AddComponent<CapsuleCollider>();
            capsule.height = height;
            capsule.radius = radius;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            dummy.AddComponent<CharacterDummy>().Configure(prefabPath);
            dummy.AddComponent<Health>().Configure(health, 2);
            dummy.AddComponent<HealthBar>().Configure(barHeight);
            dummy.AddComponent<Respawner>().Configure(6f); // long enough to look at the corpse
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
