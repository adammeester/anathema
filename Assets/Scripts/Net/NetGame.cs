using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Anathema.Arena;
using Anathema.Combat;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anathema.Net
{
    // Owns the whole online session: starting/joining a game, the character-select lobby, loading the arena, spawning
    // the two fighters, scoring, rematches and every menu screen (drawn with OnGUI, like the in-game HUD).
    // Lives on its own DontDestroyOnLoad object together with the NetworkManager; the Menu scene creates it.
    // Lobby/match messages use named messages so nothing here needs a NetworkObject in a scene.
    public class NetGame : MonoBehaviour
    {
        public const string MenuScene = "Menu";
        public const string ArenaScene = "Versus"; // the online arena (the "Arena" scene is the offline training ground)
        public const ushort DefaultPort = 7777;
        public const float RoundPause = 5f;   // countdown (with the damage report) between rounds
        public const float RoundIntro = 1.8f; // how long the "ROUND N" banner shows when a round begins

        public enum Phase
        {
            Menu,
            Connecting,
            Lobby,
            Loading,
            Playing,
            RoundEnd,
            Over
        }

        struct Seat
        {
            public bool used;
            public ulong clientId;
            public int character;
            public bool ready;
        }

        static readonly string[] CharacterNames = { "Grey Knight Interceptor", "Khorne Berserker", "Plague Surgeon" };
        static readonly string[] CharacterPrefabs = { "Net/Player_GreyKnight", "Net/Player_KhorneBerserker", "Net/Player_PlagueSurgeon" };
        static readonly string[] CharacterBlurb =
        {
            "Psychic warrior-monk. Ranged and precise, with a counter that turns into a teleport.",
            "Frenzied melee brawler. Closes the gap fast and wins by staying on top of you.",
            "Support caster. Poison clouds and plague flies that hurt foes and mend allies. No counter, just wings."
        };
        static readonly string[][] CharacterKit =
        {
            new[] { "LMB  Storm bolter", "Q  Halberd sweep", "RMB  Smite (piercing lance)", "Space  Counter + riposte", "R  Purgatus (ultimate)" },
            new[] { "LMB  Chain-axe chops", "Q  Axe rush (EX: whirling carnage)", "E  Maim (EX: Butcher's Nails rage)", "Space  Blood rage counter", "R  Blood for the Blood God (ultimate)", "Hold Shift  EX version" },
            new[] { "LMB  Plague spit (stacking rot)", "E  Restorative cloud (EX: damage cloud)", "RMB  Pestilent fly (EX: bursts)", "Space  Winged escape (i-frames)", "R  Ward of Plague (ultimate)", "Hold Shift  EX version" }
        };

        public static NetGame Instance { get; private set; }
        public static bool Online => Instance != null && Instance.manager != null && Instance.manager.IsListening;
        public static bool Frozen => Instance != null && Instance.frozen;
        public static bool IsHost => Online && Instance.manager.IsServer;

        static string pendingNotice = "";

        NetworkManager manager;
        UnityTransport transport;
        Phase phase = Phase.Menu;
        readonly Seat[] seats = new Seat[2];
        int myCharacter;
        bool myReady;
        int winnerSeat = -1;      // winner of the match (Over) or of the round that just ended (RoundEnd)
        int bestOf = 3;           // 3 or 5; chosen by the host in the lobby
        float nextRoundTime;      // server: when the next round starts
        float countdownEnd;       // both sides: when the between-rounds countdown reaches zero
        float introUntil;         // when the "ROUND N" banner stops showing
        bool frozen;

        int RoundsToWin => bestOf / 2 + 1;
        bool leaving;
        string joinAddress = "127.0.0.1";
        string notice = "";
        string localIps = "";

        // GUI
        Texture2D pixel;
        GUIStyle title, subtitle, label, labelSmall, labelCentered, button, buttonBig, field, headline;
        float uiScale = 1f;

        // --- Creation ---------------------------------------------------------------------------------------------

        public static void EnsureExists()
        {
            if (Instance != null) return;

            var go = new GameObject("NetGame");
            go.SetActive(false);

            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            utp.MaxConnectAttempts = 4;
            utp.ConnectTimeoutMS = 1000;
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                TickRate = 30
            };

            go.AddComponent<NetGame>();
            go.SetActive(true);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            manager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            Application.runInBackground = true;
            notice = pendingNotice;
            pendingNotice = "";
            localIps = FindLocalAddresses();
        }

        void Start()
        {
            if (Instance != this) return;
            foreach (string path in CharacterPrefabs)
            {
                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null) Debug.LogError($"Missing player prefab Resources/{path}. Run Anathema > Rebuild Everything.");
                else manager.AddNetworkPrefab(prefab);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                frozen = false;
            }
        }

        // --- Starting / joining -----------------------------------------------------------------------------------

        void HostGame()
        {
            notice = "";
            transport.SetConnectionData("127.0.0.1", DefaultPort, "0.0.0.0");
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;

            if (!manager.StartHost())
            {
                notice = "Could not start hosting (is port 7777 already in use?).";
                return;
            }

            manager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            RegisterHandlers();

            seats[0] = new Seat { used = true, clientId = manager.LocalClientId, character = myCharacter, ready = false };
            seats[1] = default;
            myReady = false;
            phase = Phase.Lobby;
        }

        void JoinGame()
        {
            notice = "";
            string address = joinAddress.Trim();
            ushort port = DefaultPort;
            int colon = address.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(address.Substring(colon + 1), out ushort parsed))
            {
                port = parsed;
                address = address.Substring(0, colon);
            }

            if (address.Length == 0)
            {
                notice = "Enter the host's IP address.";
                return;
            }

            transport.SetConnectionData(address, port);
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;

            if (!manager.StartClient())
            {
                notice = "Could not start the connection.";
                return;
            }

            RegisterHandlers();
            myReady = false;
            phase = Phase.Connecting;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool free = !seats[1].used;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Approved = free && phase == Phase.Lobby;
            if (!response.Approved) response.Reason = phase == Phase.Lobby ? "The game is full." : "A match is already in progress.";
        }

        void RegisterHandlers()
        {
            CustomMessagingManager msg = manager.CustomMessagingManager;
            msg.RegisterNamedMessageHandler("pick", OnPickMessage);
            msg.RegisterNamedMessageHandler("ready", OnReadyMessage);
            msg.RegisterNamedMessageHandler("lobby", OnLobbyMessage);
            msg.RegisterNamedMessageHandler("roundEnd", OnRoundEndMessage);
            msg.RegisterNamedMessageHandler("nextRound", OnNextRoundMessage);
            msg.RegisterNamedMessageHandler("over", OnOverMessage);
            msg.RegisterNamedMessageHandler("rematch", OnRematchMessage);
            msg.RegisterNamedMessageHandler("orbSpawn", OnOrbSpawnMessage);
            msg.RegisterNamedMessageHandler("orbClaim", OnOrbClaimMessage);
            msg.RegisterNamedMessageHandler("orbTaken", OnOrbTakenMessage);
        }

        // --- Healing orbs (host decides who gets each one) --------------------------------------------------------

        public void BroadcastOrbSpawn(int id, Vector3 position, OrbKind kind, float amount)
        {
            if (!manager.IsServer) return;
            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(position);
            writer.WriteValueSafe((int)kind);
            writer.WriteValueSafe(amount);
            manager.CustomMessagingManager.SendNamedMessageToAll("orbSpawn", writer);
        }

        void OnOrbSpawnMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out int kind);
            reader.ReadValueSafe(out float amount);
            HealingOrb.Spawn(id, position, (OrbKind)Mathf.Clamp(kind, 0, 1), amount);
        }

        public void RequestOrbClaim(int id)
        {
            if (manager.IsServer)
            {
                ServerResolveOrbClaim(id, manager.LocalClientId);
                return;
            }

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(id);
            manager.CustomMessagingManager.SendNamedMessage("orbClaim", NetworkManager.ServerClientId, writer);
        }

        void OnOrbClaimMessage(ulong sender, FastBufferReader reader)
        {
            if (!manager.IsServer) return;
            reader.ReadValueSafe(out int id);
            ServerResolveOrbClaim(id, sender);
        }

        // First claim wins; a claim for an orb that is already gone is ignored.
        void ServerResolveOrbClaim(int id, ulong claimer)
        {
            if (HealingOrb.Find(id) == null) return;

            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(claimer);
            manager.CustomMessagingManager.SendNamedMessageToAll("orbTaken", writer);
            HealingOrb.Collect(id, claimer == manager.LocalClientId);
        }

        void OnOrbTakenMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out ulong claimer);
            HealingOrb.Collect(id, claimer == manager.LocalClientId);
        }

        void OnClientConnected(ulong clientId)
        {
            if (manager.IsServer)
            {
                if (clientId == manager.LocalClientId) return;
                if (!seats[1].used) seats[1] = new Seat { used = true, clientId = clientId, character = 0, ready = false };
                BroadcastLobby();
            }
            else if (clientId == manager.LocalClientId)
            {
                phase = Phase.Lobby; // the host's lobby message fills in the seats
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (leaving) return;

            if (manager.IsServer)
            {
                if (clientId == manager.LocalClientId) return;
                if (phase == Phase.Lobby)
                {
                    for (int i = 0; i < seats.Length; i++)
                        if (seats[i].used && seats[i].clientId == clientId) seats[i] = default;
                    BroadcastLobby();
                }
                else
                {
                    LeaveToMenu("Your opponent left the match.");
                }
            }
            else if (clientId == manager.LocalClientId)
            {
                string reason = manager.DisconnectReason;
                bool wasConnecting = phase == Phase.Connecting;
                LeaveToMenu(!string.IsNullOrEmpty(reason) ? reason
                    : wasConnecting ? "Could not connect. Check the IP address and that the host's port 7777 (UDP) is open."
                    : "Disconnected from the host.");
            }
        }

        public void LeaveToMenu(string message)
        {
            if (leaving) return;
            leaving = true;
            pendingNotice = message ?? "";
            frozen = false;
            if (manager != null && manager.IsListening) manager.Shutdown();
            Destroy(gameObject);
            SceneManager.LoadScene(MenuScene);
        }

        // --- Lobby ------------------------------------------------------------------------------------------------

        int MySeat()
        {
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].used && seats[i].clientId == manager.LocalClientId) return i;
            return -1;
        }

        void SetMyCharacter(int index)
        {
            if (index == myCharacter) return;
            myCharacter = index;
            myReady = false;
            if (manager.IsServer)
            {
                ApplyPick(0, index);
            }
            else
            {
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(index);
                manager.CustomMessagingManager.SendNamedMessage("pick", NetworkManager.ServerClientId, writer);
            }
        }

        void SetMyReady(bool ready)
        {
            myReady = ready;
            if (manager.IsServer)
            {
                ApplyReady(0, ready);
            }
            else
            {
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(ready);
                manager.CustomMessagingManager.SendNamedMessage("ready", NetworkManager.ServerClientId, writer);
            }
        }

        void SetBestOf(int value)
        {
            if (!manager.IsServer || phase != Phase.Lobby || bestOf == value) return;
            bestOf = value;
            // Changing the rules un-readies everyone so nobody starts a match they didn't agree to.
            for (int i = 0; i < seats.Length; i++) seats[i].ready = false;
            myReady = false;
            BroadcastLobby();
        }

        void ApplyPick(int seat, int character)
        {
            if (seat < 0 || !seats[seat].used) return;
            seats[seat].character = Mathf.Clamp(character, 0, CharacterNames.Length - 1);
            seats[seat].ready = false;
            BroadcastLobby();
        }

        void ApplyReady(int seat, bool ready)
        {
            if (seat < 0 || !seats[seat].used) return;
            seats[seat].ready = ready;
            BroadcastLobby();
        }

        void OnPickMessage(ulong sender, FastBufferReader reader)
        {
            if (!manager.IsServer) return;
            reader.ReadValueSafe(out int character);
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].used && seats[i].clientId == sender) ApplyPick(i, character);
        }

        void OnReadyMessage(ulong sender, FastBufferReader reader)
        {
            if (!manager.IsServer) return;
            reader.ReadValueSafe(out bool ready);
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].used && seats[i].clientId == sender) ApplyReady(i, ready);
        }

        void BroadcastLobby()
        {
            if (!manager.IsServer || phase != Phase.Lobby) return;
            if (seats[1].used)
            {
                using var writer = new FastBufferWriter(64, Allocator.Temp);
                for (int i = 0; i < seats.Length; i++)
                {
                    writer.WriteValueSafe(seats[i].used);
                    writer.WriteValueSafe(seats[i].clientId);
                    writer.WriteValueSafe(seats[i].character);
                    writer.WriteValueSafe(seats[i].ready);
                }
                writer.WriteValueSafe(bestOf);
                manager.CustomMessagingManager.SendNamedMessageToAll("lobby", writer);
            }
        }

        void OnLobbyMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            for (int i = 0; i < seats.Length; i++)
            {
                reader.ReadValueSafe(out seats[i].used);
                reader.ReadValueSafe(out seats[i].clientId);
                reader.ReadValueSafe(out seats[i].character);
                reader.ReadValueSafe(out seats[i].ready);
            }
            reader.ReadValueSafe(out bestOf);

            int mine = MySeat();
            if (mine >= 0)
            {
                myCharacter = seats[mine].character;
                myReady = seats[mine].ready;
            }
        }

        // --- Match flow (server drives it) ------------------------------------------------------------------------

        void Update()
        {
            if (Instance != this || manager == null) return;

            if (!manager.IsServer && phase == Phase.Lobby && SceneManager.GetActiveScene().name == ArenaScene)
            {
                phase = Phase.Playing;
                introUntil = Time.time + RoundIntro;
            }

            if (manager.IsServer && phase == Phase.RoundEnd && Time.time >= nextRoundTime)
                StartNextRound();

            if (manager.IsServer && phase == Phase.Lobby && seats[0].used && seats[1].used && seats[0].ready && seats[1].ready)
            {
                phase = Phase.Loading;
                manager.SceneManager.LoadScene(ArenaScene, LoadSceneMode.Single);
            }
        }

        void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (!manager.IsServer || sceneName != ArenaScene) return;

            for (int seat = 0; seat < seats.Length; seat++)
            {
                if (!seats[seat].used) continue;

                var prefab = Resources.Load<GameObject>(CharacterPrefabs[seats[seat].character]);
                Vector3 position = SpawnPosition(seat, out Quaternion rotation);
                GameObject player = Instantiate(prefab, position, rotation);
                var networkObject = player.GetComponent<NetworkObject>();
                networkObject.SpawnAsPlayerObject(seats[seat].clientId, true);
                player.GetComponent<NetPlayer>().ServerSetSlot(seat);
            }

            winnerSeat = -1;
            frozen = false;
            phase = Phase.Playing;
            introUntil = Time.time + RoundIntro;
        }

        public static Vector3 SpawnPosition(int seat, out Quaternion rotation)
        {
            GameObject marker = GameObject.Find("SpawnPoint_" + seat);
            Vector3 position = marker != null ? marker.transform.position : new Vector3(seat == 0 ? -20f : 20f, 0.1f, 0f);
            Vector3 toCentre = -position;
            toCentre.y = 0f;
            rotation = toCentre.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCentre.normalized, Vector3.up) : Quaternion.identity;
            return position;
        }

        public void ServerOnKill(NetPlayer killer, NetPlayer victim)
        {
            if (!manager.IsServer || phase != Phase.Playing) return;

            // A death ends the round: whoever lived takes it ("kills" on NetPlayer counts rounds won).
            killer.ServerAddKill();
            frozen = true;
            winnerSeat = killer.Slot;

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(winnerSeat);

            if (killer.Kills >= RoundsToWin)
            {
                phase = Phase.Over;
                manager.CustomMessagingManager.SendNamedMessageToAll("over", writer);
            }
            else
            {
                phase = Phase.RoundEnd;
                nextRoundTime = Time.time + RoundPause;
                countdownEnd = nextRoundTime;
                manager.CustomMessagingManager.SendNamedMessageToAll("roundEnd", writer);
            }
        }

        void OnRoundEndMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            reader.ReadValueSafe(out winnerSeat);
            phase = Phase.RoundEnd;
            countdownEnd = Time.time + RoundPause;
            frozen = true;
        }

        void OnOverMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            reader.ReadValueSafe(out winnerSeat);
            phase = Phase.Over;
            frozen = true;
        }

        void StartNextRound()
        {
            if (!manager.IsServer) return;

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(true);
            manager.CustomMessagingManager.SendNamedMessageToAll("nextRound", writer);
            ApplyRoundReset();
        }

        void OnNextRoundMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            ApplyRoundReset();
        }

        void StartRematch()
        {
            if (!manager.IsServer) return;

            foreach (NetPlayer player in NetPlayer.All) player.ServerResetKills();
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(true);
            manager.CustomMessagingManager.SendNamedMessageToAll("rematch", writer);
            ApplyRoundReset();
        }

        void OnRematchMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            ApplyRoundReset();
        }

        // Fresh round (or fresh match after a rematch): everyone back at their spawn, arena reset.
        void ApplyRoundReset()
        {
            winnerSeat = -1;
            frozen = false;
            phase = Phase.Playing;
            introUntil = Time.time + RoundIntro;
            if (NetPlayer.Local != null) NetPlayer.Local.ResetForRematch();
            if (manager.IsServer) foreach (NetPlayer player in NetPlayer.All) player.ServerResetRoundStats();
            GasZone gas = FindFirstObjectByType<GasZone>();
            if (gas != null) gas.Restart();
            OrbSpawner orbs = FindFirstObjectByType<OrbSpawner>();
            if (orbs != null) orbs.Restart();
            else HealingOrb.ClearAll();
        }

        // --- GUI --------------------------------------------------------------------------------------------------

        static readonly Color Gold = new Color(0.86f, 0.70f, 0.30f);
        static readonly Color DarkGold = new Color(0.45f, 0.34f, 0.12f);
        static readonly Color Panel = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        static readonly Color Ember = new Color(1f, 0.30f, 0.15f);

        void BuildStyles()
        {
            if (pixel != null) return;

            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();

            Texture2D Tex(Color c)
            {
                var t = new Texture2D(1, 1);
                t.SetPixel(0, 0, c);
                t.Apply();
                return t;
            }

            title = new GUIStyle(GUI.skin.label) { fontSize = 120, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = Gold;
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            subtitle.normal.textColor = new Color(0.75f, 0.7f, 0.65f);
            headline = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            headline.normal.textColor = Gold;
            label = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = true };
            label.normal.textColor = new Color(0.9f, 0.88f, 0.82f);
            labelSmall = new GUIStyle(label) { fontSize = 21 };
            labelCentered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };

            button = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            button.normal.background = Tex(new Color(0.16f, 0.15f, 0.14f, 0.95f));
            button.hover.background = Tex(new Color(0.30f, 0.24f, 0.12f, 0.98f));
            button.active.background = Tex(new Color(0.45f, 0.34f, 0.12f, 1f));
            button.normal.textColor = Gold;
            button.hover.textColor = Color.white;
            button.active.textColor = Color.white;
            button.border = new RectOffset(2, 2, 2, 2);
            buttonBig = new GUIStyle(button) { fontSize = 40 };

            field = new GUIStyle(GUI.skin.textField) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            field.normal.background = Tex(new Color(0.04f, 0.04f, 0.05f, 0.95f));
            field.focused.background = field.normal.background;
            field.normal.textColor = Color.white;
            field.focused.textColor = Color.white;
        }

        void Rect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, pixel);
            GUI.color = old;
        }

        void Frame(Rect r, Color fill, Color border, float thickness = 3f)
        {
            Rect(r, border);
            Rect(new Rect(r.x + thickness, r.y + thickness, r.width - thickness * 2f, r.height - thickness * 2f), fill);
        }

        void OnGUI()
        {
            if (Instance != this) return;
            BuildStyles();

            uiScale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float w = Screen.width / uiScale;
            float h = 1080f;

            switch (phase)
            {
                case Phase.Menu: DrawMainMenu(w, h); break;
                case Phase.Connecting: DrawConnecting(w, h); break;
                case Phase.Lobby: DrawLobby(w, h); break;
                case Phase.Loading: DrawBackdrop(w, h); GUI.Label(new Rect(0, h * 0.45f, w, 80), "ENTERING THE ARENA...", headline); break;
                case Phase.Playing:
                case Phase.RoundEnd:
                case Phase.Over: DrawMatch(w, h); break;
            }

            GUI.matrix = Matrix4x4.identity;
        }

        void DrawBackdrop(float w, float h)
        {
            Rect(new Rect(0, 0, w, h), new Color(0.06f, 0.03f, 0.03f, 1f));
            // Ember glow along the bottom and a gold rule under the header.
            for (int i = 0; i < 12; i++)
                Rect(new Rect(0, h - 12 * (i + 1) * 4f, w, 48f), new Color(0.55f, 0.12f, 0.05f, 0.05f));
        }

        void DrawMainMenu(float w, float h)
        {
            DrawBackdrop(w, h);

            GUI.Label(new Rect(0, 110, w, 150), "ANATHEMA", title);
            GUI.Label(new Rect(0, 250, w, 40), "A  1 v 1  ARENA  BRAWLER", subtitle);
            Rect(new Rect(w * 0.5f - 260, 305, 520, 3), DarkGold);

            float bx = w * 0.5f - 230;
            if (GUI.Button(new Rect(bx, 360, 460, 84), "HOST GAME", buttonBig)) HostGame();

            GUI.Label(new Rect(bx, 485, 460, 36), "OR JOIN A FRIEND", labelCentered);
            joinAddress = GUI.TextField(new Rect(bx, 530, 460, 62), joinAddress, 64, field);
            if (GUI.Button(new Rect(bx, 610, 460, 84), "JOIN GAME", buttonBig)) JoinGame();

            if (GUI.Button(new Rect(bx, 730, 460, 66), "QUIT", button))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            if (!string.IsNullOrEmpty(notice))
            {
                var warn = new GUIStyle(labelCentered);
                warn.normal.textColor = new Color(1f, 0.55f, 0.4f);
                GUI.Label(new Rect(w * 0.5f - 480, 830, 960, 90), notice, warn);
            }

            GUI.Label(new Rect(0, h - 70, w, 40),
                "WASD move   ·   mouse aim   ·   LMB attack   ·   a round ends when someone dies", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawConnecting(float w, float h)
        {
            DrawBackdrop(w, h);
            GUI.Label(new Rect(0, h * 0.40f, w, 80), "CONNECTING...", headline);
            GUI.Label(new Rect(0, h * 0.40f + 90, w, 40), joinAddress, labelCentered);
            if (GUI.Button(new Rect(w * 0.5f - 160, h * 0.40f + 170, 320, 64), "CANCEL", button)) LeaveToMenu("");
        }

        void DrawLobby(float w, float h)
        {
            DrawBackdrop(w, h);

            GUI.Label(new Rect(0, 40, w, 80), "CHOOSE YOUR CHAMPION", headline);
            Rect(new Rect(w * 0.5f - 300, 125, 600, 3), DarkGold);

            int mine = manager.IsServer ? 0 : MySeat();
            int other = mine == 0 ? 1 : 0;
            bool opponentHere = other >= 0 && seats[other].used;

            // Character cards.
            int cardCount = CharacterNames.Length;
            float gap = 40, cardH = 520;
            float cardW = Mathf.Min(560f, (w - 80f - gap * (cardCount - 1)) / cardCount);
            float startX = w * 0.5f - (cardW * cardCount + gap * (cardCount - 1)) * 0.5f;
            for (int i = 0; i < CharacterNames.Length; i++)
            {
                var card = new Rect(startX + i * (cardW + gap), 160, cardW, cardH);
                bool selected = myCharacter == i;
                bool opponentPicked = opponentHere && seats[other].character == i;
                Color accent = i == 0 ? new Color(0.45f, 0.75f, 1f) : i == 1 ? new Color(1f, 0.32f, 0.22f) : new Color(0.55f, 0.85f, 0.2f);

                Frame(card, Panel, selected ? accent : DarkGold, selected ? 6f : 3f);
                Rect(new Rect(card.x + 6, card.y + 6, card.width - 12, 10), new Color(accent.r, accent.g, accent.b, selected ? 0.9f : 0.35f));

                GUI.Label(new Rect(card.x + 30, card.y + 34, cardW - 60, 44), CharacterNames[i].ToUpper(), new GUIStyle(label) { fontSize = 32, fontStyle = FontStyle.Bold, normal = { textColor = accent } });
                GUI.Label(new Rect(card.x + 30, card.y + 88, cardW - 60, 90), CharacterBlurb[i], label);

                float y = card.y + 200;
                foreach (string line in CharacterKit[i])
                {
                    GUI.Label(new Rect(card.x + 40, y, cardW - 80, 34), line, labelSmall);
                    y += 36;
                }

                string tag = selected && opponentPicked ? "YOU + OPPONENT" : selected ? "YOU" : opponentPicked ? "OPPONENT" : "";
                if (tag.Length > 0)
                    GUI.Label(new Rect(card.x + 30, card.yMax - 52, cardW - 60, 40), tag, new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, normal = { textColor = accent } });

                // Whole card is the button (invisible), so clicking anywhere on it picks the champion.
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) SetMyCharacter(i);
            }

            // Match length: the host picks, the guest sees it.
            if (manager.IsServer)
            {
                GUI.Label(new Rect(w * 0.5f - 400, 692, 300, 56), "MATCH LENGTH", new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold });
                DrawBestOfButton(new Rect(w * 0.5f - 80, 692, 200, 56), 3);
                DrawBestOfButton(new Rect(w * 0.5f + 140, 692, 200, 56), 5);
            }
            else
            {
                GUI.Label(new Rect(0, 692, w, 56), $"MATCH LENGTH:  BEST OF {bestOf}  (set by host)", new GUIStyle(labelCentered) { fontStyle = FontStyle.Bold });
            }

            // Status line.
            string opponentText = !opponentHere ? "Waiting for your opponent to join..."
                : $"Opponent: {CharacterNames[seats[other].character]} - {(seats[other].ready ? "READY" : "choosing")}";
            GUI.Label(new Rect(0, 760, w, 40), opponentText, labelCentered);

            if (manager.IsServer)
            {
                GUI.Label(new Rect(0, 800, w, 40), $"Your friend joins with:  {localIps}   (port {DefaultPort} UDP)", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
            }

            // Ready / back.
            bool canReady = opponentHere;
            GUI.enabled = canReady;
            string readyText = myReady ? "READY!  (click to cancel)" : "READY";
            if (GUI.Button(new Rect(w * 0.5f - 250, 850, 500, 90), readyText, buttonBig)) SetMyReady(!myReady);
            GUI.enabled = true;

            if (GUI.Button(new Rect(60, h - 110, 200, 60), "BACK", button)) LeaveToMenu("");

            if (myReady && opponentHere && seats[other].ready)
                GUI.Label(new Rect(0, 950, w, 40), "Both ready - starting...", labelCentered);
        }

        void DrawBestOfButton(Rect r, int value)
        {
            bool selected = bestOf == value;
            if (selected) Rect(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Gold); // gold outline behind the button
            var style = selected ? new GUIStyle(button) { normal = { textColor = Color.white, background = button.active.background } } : button;
            if (GUI.Button(r, $"BEST OF {value}", style)) SetBestOf(value);
        }

        void DrawMatch(float w, float h)
        {
            // Floating YOU / ENEMY tags above each fighter.
            Camera cam = Camera.main;
            if (cam != null)
            {
                foreach (NetPlayer player in NetPlayer.All)
                {
                    if (player == null || player.Health.IsDead) continue;
                    Vector3 screen = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 3.15f);
                    if (screen.z <= 0f) continue;

                    bool mine = player.IsOwner;
                    var style = new GUIStyle(labelCentered) { fontSize = 22, fontStyle = FontStyle.Bold };
                    style.normal.textColor = mine ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f);
                    float x = screen.x / uiScale;
                    float y = (Screen.height - screen.y) / uiScale;
                    GUI.Label(new Rect(x - 80, y - 16, 160, 32), mine ? "YOU" : "ENEMY", style);
                }
            }

            // Score: rounds won by each side, with the round number and match length in the middle.
            int myKills = 0, theirKills = 0;
            foreach (NetPlayer player in NetPlayer.All)
            {
                if (player.IsOwner) myKills = player.Kills;
                else theirKills = player.Kills;
            }

            int roundNumber = Mathf.Min(myKills + theirKills + (phase == Phase.Playing ? 1 : 0), bestOf);
            Color green = new Color(0.4f, 1f, 0.5f), red = new Color(1f, 0.4f, 0.35f);

            var scoreBox = new Rect(w * 0.5f - 230, 18, 460, 78);
            Frame(scoreBox, Panel, DarkGold);
            var big = new GUIStyle(headline) { fontSize = 50 };
            big.normal.textColor = green;
            GUI.Label(new Rect(scoreBox.x, scoreBox.y + 4, 150, 70), myKills.ToString(), big);
            big.normal.textColor = red;
            GUI.Label(new Rect(scoreBox.xMax - 150, scoreBox.y + 4, 150, 70), theirKills.ToString(), big);
            GUI.Label(new Rect(scoreBox.x + 150, scoreBox.y + 6, 160, 30), $"ROUND {roundNumber}", new GUIStyle(labelCentered) { fontSize = 22, fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(scoreBox.x + 150, scoreBox.y + 36, 160, 30), $"BEST OF {bestOf}", new GUIStyle(labelCentered) { fontSize = 20 });

            if (phase == Phase.Playing || phase == Phase.RoundEnd)
                if (GUI.Button(new Rect(w - 190, 18, 160, 52), "LEAVE", button)) LeaveToMenu("");

            bool won = NetPlayer.Local != null && NetPlayer.Local.Slot == winnerSeat;

            if (phase == Phase.Playing)
            {
                // "ROUND N" banner as a round begins.
                if (Time.time < introUntil)
                {
                    Rect(new Rect(0, h * 0.36f, w, 170), new Color(0f, 0f, 0f, 0.55f));
                    GUI.Label(new Rect(0, h * 0.36f + 8, w, 110), $"ROUND {roundNumber}", new GUIStyle(title) { fontSize = 90 });
                    GUI.Label(new Rect(0, h * 0.36f + 108, w, 50), $"{myKills}  -  {theirKills}      ·      BEST OF {bestOf}", subtitle);
                }
                return;
            }

            if (phase == Phase.RoundEnd)
            {
                // Round result banner + damage report; everyone is reset to their spawn when the countdown hits zero.
                float top = h * 0.08f;
                Rect(new Rect(0, top, w, 200), new Color(0f, 0f, 0f, 0.6f));
                var roundResult = new GUIStyle(title) { fontSize = 90 };
                roundResult.normal.textColor = won ? green : red;
                GUI.Label(new Rect(0, top + 4, w, 110), won ? "ROUND WON" : "ROUND LOST", roundResult);
                GUI.Label(new Rect(0, top + 110, w, 60), $"{myKills}  -  {theirKills}      ·      BEST OF {bestOf}", new GUIStyle(headline) { fontSize = 40 });
                GUI.Label(new Rect(0, top + 162, w, 34), $"First to {RoundsToWin} rounds wins the match", subtitle);

                DrawDamageReport(w, top + 220);

                int seconds = Mathf.Max(1, Mathf.CeilToInt(countdownEnd - Time.time));
                GUI.Label(new Rect(0, h - 190, w, 40), "NEXT ROUND IN", subtitle);
                GUI.Label(new Rect(0, h - 150, w, 120), seconds.ToString(), new GUIStyle(title) { fontSize = 110 });
                return;
            }

            // Match over.
            Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.65f));
            var result = new GUIStyle(title) { fontSize = 130 };
            result.normal.textColor = won ? green : red;
            GUI.Label(new Rect(0, 230, w, 170), won ? "MATCH WON" : "MATCH LOST", result);
            GUI.Label(new Rect(0, 410, w, 70), $"{myKills}  -  {theirKills}", new GUIStyle(headline) { fontSize = 60 });
            GUI.Label(new Rect(0, 480, w, 40), $"BEST OF {bestOf}", subtitle);

            DrawDamageReport(w, 540);

            float bx = w * 0.5f - 230;
            if (manager.IsServer)
            {
                if (GUI.Button(new Rect(bx, 800, 460, 84), "REMATCH", buttonBig)) StartRematch();
            }
            else
            {
                GUI.Label(new Rect(0, 820, w, 50), "Waiting for the host to start a rematch...", labelCentered);
            }
            if (GUI.Button(new Rect(bx, 900, 460, 70), "MAIN MENU", button)) LeaveToMenu("");
        }

        // Post-round damage report: one row per fighter (you first, then allies, then enemies), so it scales to 2v2 / 3v3.
        void DrawDamageReport(float w, float y)
        {
            NetPlayer me = NetPlayer.Local;
            if (me == null) return;

            var rows = new List<NetPlayer>(NetPlayer.All);
            rows.RemoveAll(p => p == null);
            int Order(NetPlayer p) => p == me ? 0 : p.Health.Team == me.Health.Team ? 1 : 2;
            rows.Sort((a, b) => Order(a).CompareTo(Order(b)));

            Color green = new Color(0.4f, 1f, 0.5f), blue = new Color(0.45f, 0.75f, 1f), red = new Color(1f, 0.4f, 0.35f);
            const float rowH = 40f, nameW = 300f, colW = 110f;
            string[] columns = { "DEALT", "TAKEN", "HEAL DONE", "HEAL GOT", "HP LEFT" };
            float panelW = nameW + colW * columns.Length + 60f;
            var panel = new Rect(w * 0.5f - panelW * 0.5f, y, panelW, 92f + rows.Count * rowH);
            Frame(panel, Panel, DarkGold);

            var head = new GUIStyle(labelCentered) { fontSize = 22, fontStyle = FontStyle.Bold };
            head.normal.textColor = Gold;
            GUI.Label(new Rect(panel.x, panel.y + 6, panel.width, 30), "DAMAGE REPORT", head);

            var colStyle = new GUIStyle(labelCentered) { fontSize = 17, fontStyle = FontStyle.Bold };
            colStyle.normal.textColor = new Color(0.75f, 0.7f, 0.65f);
            for (int c = 0; c < columns.Length; c++)
                GUI.Label(new Rect(panel.x + 30 + nameW + c * colW, panel.y + 42, colW, 26), columns[c], colStyle);

            for (int i = 0; i < rows.Count; i++)
            {
                NetPlayer p = rows[i];
                int group = Order(p);
                Color tint = group == 0 ? green : group == 1 ? blue : red;
                string who = group == 0 ? "YOU" : group == 1 ? "ALLY" : "ENEMY";
                string character = p.name.Replace("(Clone)", "").Replace("Player_", "").Trim();
                float ry = panel.y + 72 + i * rowH;

                var nameStyle = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                nameStyle.normal.textColor = tint;
                GUI.Label(new Rect(panel.x + 30, ry, nameW, rowH), $"{who}  ·  {character}", nameStyle);

                var valueStyle = new GUIStyle(labelCentered) { fontSize = 26, fontStyle = FontStyle.Bold };
                valueStyle.normal.textColor = tint;
                float[] values = { p.RoundDamageDealt, p.RoundDamageTaken, p.RoundHealingDone, p.RoundHealed, p.Health.Current };
                for (int c = 0; c < values.Length; c++)
                    GUI.Label(new Rect(panel.x + 30 + nameW + c * colW, ry, colW, rowH), Mathf.RoundToInt(values[c]).ToString(), valueStyle);
            }
        }

        static string FindLocalAddresses()
        {
            var found = new List<string>();
            try
            {
                foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                        found.Add(address.ToString());
            }
            catch (SocketException)
            {
            }

            return found.Count > 0 ? string.Join("  /  ", found) : "127.0.0.1";
        }
    }
}
