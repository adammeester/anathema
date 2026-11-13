using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Anathema.Arena;
using Anathema.CameraRig;
using Anathema.Combat;
using Anathema.Upgrades;
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
        // The online arenas (the "Arena" scene is the offline training ground). The host picks one at random per match.
        public static readonly string[] ArenaScenes = { "Versus", "Versus_Shrine", "Versus_Crucible" };
        static int lastArenaIndex = -1;

        public static bool IsArenaScene(string sceneName) => System.Array.IndexOf(ArenaScenes, sceneName) >= 0;

        // Random arena, never the same one twice in a row. Only arenas actually in this build are considered (an arena
        // missing from the build would fail to load and leave the match stuck on Loading).
        static string PickArenaScene()
        {
            var available = new List<int>();
            for (int i = 0; i < ArenaScenes.Length; i++)
            {
                if (Application.CanStreamedLevelBeLoaded(ArenaScenes[i])) available.Add(i);
                else Debug.LogWarning($"Arena scene '{ArenaScenes[i]}' is not in this build; skipping it.");
            }
            if (available.Count == 0) return ArenaScenes[0];

            if (available.Count > 1) available.Remove(lastArenaIndex);
            int index = available[Random.Range(0, available.Count)];
            lastArenaIndex = index;
            if (IsDedicated) Debug.Log($"[Server] Arena: {ArenaScenes[index]}");
            return ArenaScenes[index];
        }
        public const ushort DefaultPort = 7777;
        public const float RoundPause = 5f;   // countdown (with the damage report) between rounds
        // Between rounds: the report (RoundPause), then a prep phase where everyone is back at their spawn, confined to
        // PrepRadius, unable to fight, picking an upgrade; then a short lock-in countdown before the round begins.
        public const float PrepMin = 3f;        // the prep menu never closes sooner than this
        public const float PrepWindow = 20f;    // longest it waits for slow pickers (then a random upgrade is taken)
        public const float PrepCountdown = 3f;  // picks are locked and the barrier drops after this
        public const float PrepRadius = 4.5f;   // small ready area at the edge of the arena; players run to the middle to meet
        public const float RoundIntro = 1.8f; // how long the "ROUND N" banner shows when a round begins

        public enum Phase
        {
            Menu,
            Connecting,
            Lobby,
            Loading,
            Playing,
            RoundEnd,
            Prep,
            Over
        }

        public const int MaxSeats = 6;

        public enum GameMode
        {
            FreeForAll, // every player is their own team (1v1, 1v1v1, ...)
            Teams       // players pick team A or B (2v2, 3v3, ...)
        }

        struct Seat
        {
            public bool used;
            public ulong clientId;
            public int character;
            public bool ready;
            public int team; // 0 = team A, 1 = team B (only used in Teams mode)
        }

        static readonly string[] CharacterNames = { "Grey Knight Interceptor", "Khorne Berserker", "Plague Surgeon", "Psyker", "Skitarii Ranger", "Hospitaller", "Mandrake" };
        static readonly string[] CharacterPrefabs = { "Net/Player_GreyKnight", "Net/Player_KhorneBerserker", "Net/Player_PlagueSurgeon", "Net/Player_Psyker", "Net/Player_SkitariiRanger", "Net/Player_Hospitaller", "Net/Player_Mandrake" };

        public static NetGame Instance { get; private set; }
        public static bool Online => Instance != null && Instance.manager != null && Instance.manager.IsListening;
        public static bool Frozen => Instance != null && Instance.frozen;
        // The between-rounds prep area: movement is allowed (inside PrepRadius), fighting is not.
        public static bool InPrep => (Instance != null && Instance.phase == Phase.Prep) || OfflinePrep.Active;
        public static bool IsHost => Online && Instance.manager.IsServer;
        // A headless server with no player of its own (see StartDedicated); every fighter is a remote client.
        public static bool IsDedicated => Instance != null && Instance.dedicated;

        bool dedicated;

        static string pendingNotice = "";

        NetworkManager manager;
        UnityTransport transport;
        Phase phase = Phase.Menu;
        readonly Seat[] seats = new Seat[MaxSeats];
        GameMode mode = GameMode.FreeForAll; // chosen by the host in the lobby
        int myCharacter;
        bool myReady;
        int winnerTeam = -1;      // team id (Health.Team) that won the match (Over) or the round that just ended (RoundEnd)
        int lastDeadTeam = -1;    // server: team of the most recent death this round (wins a round everyone dies in)
        int bestOf = 3;           // 3 or 5; chosen by the host in the lobby
        float nextRoundTime;      // server: when the next round starts
        float countdownEnd;       // both sides: when the between-rounds countdown reaches zero (the pick deadline)
        float earliestNextRound;  // both sides: the prep menu stays open at least until this
        bool prepLocked;          // both sides: picks are locked in and the round-start countdown is running
        float prepGoTime;         // both sides: when the locked countdown ends
        int roundsCompleted;      // rounds finished this match; upgrade picks are numbered by it
        readonly UpgradePickerView upgradePicker = new UpgradePickerView();
        float roundElapsed;       // seconds into the current round (shown by the HUD timer)
        float introUntil;        // when the "ROUND N" banner stops showing
        bool frozen;

        int RoundsToWin => bestOf / 2 + 1;
        bool leaving;
        // The dedicated server players connect to. Change this to the public server's address once it is hosted;
        // the menu's address field still lets a player override it (remembered between launches).
        public const string DefaultServerAddress = "127.0.0.1";
        const string ServerAddressPref = "Anathema.ServerAddress";
        const byte IntentJoin = 0, IntentHost = 1;
        float hostPendingUntil; // server: a host was just approved and is about to take seat 0
        string joinAddress = DefaultServerAddress;
        string notice = "";
        string localIps = "";

        // GUI
        Texture2D pixel;
        GUIStyle title, subtitle, label, labelSmall, labelCentered, button, buttonBig, field, headline;
        float uiScale = 1f;
        readonly CharacterSelectView select = new CharacterSelectView();
        readonly bool[] roleFilter = new bool[3];
        bool draggingModel;
        float modelDrag;
        bool showUpgrades;           // the "all upgrades" overlay on the champion-select screen
        Vector2 upgradeScroll;

        // --- Creation ---------------------------------------------------------------------------------------------

        public static void EnsureExists()
        {
            if (Instance != null) return;

            var go = new GameObject("NetGame");
            go.SetActive(false);

            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            utp.MaxConnectAttempts = 10;
            utp.ConnectTimeoutMS = 3000;
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
            try { joinAddress = PlayerPrefs.GetString(ServerAddressPref, DefaultServerAddress); } catch { }
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

            if (ServerLaunch.Requested(out ushort port)) StartDedicated(port);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                select.Hide();
                Instance = null;
                frozen = false;
            }
        }

        // --- Starting / joining -----------------------------------------------------------------------------------

        // There is no peer-to-peer hosting: every match runs on a dedicated server (StartDedicated) and every
        // player is a client of it (JoinGame).

        // Headless server: listens for players, runs the lobby and the match, but has no fighter, camera or menus.
        // The first player in the lobby (lowest seat) chooses the mode, match length and rematch.
        void StartDedicated(ushort port)
        {
            dedicated = true;
            Application.targetFrameRate = 60; // a headless loop would otherwise spin a whole core
            QualitySettings.vSyncCount = 0;

            transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;

            if (!manager.StartServer())
            {
                Debug.LogError($"[Server] Could not listen on UDP port {port} (is it already in use?).");
                Application.Quit(1);
                return;
            }

            manager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            RegisterHandlers();
            for (int i = 0; i < seats.Length; i++) seats[i] = default;
            phase = Phase.Lobby;
            Debug.Log($"[Server] Dedicated server listening on UDP port {port}. Players join with: {localIps} (add :{port} if not {DefaultPort}).");
        }

        // Dedicated server: the match is over or abandoned. Anyone still connected is sent back to their menu and the
        // server opens a fresh, empty lobby (it never quits by itself).
        void ServerResetToLobby(string reason)
        {
            Debug.Log($"[Server] {reason} Resetting to an empty lobby.");

            for (int i = 0; i < seats.Length; i++) seats[i] = default; // cleared first: the disconnects below are expected
            foreach (ulong id in new List<ulong>(manager.ConnectedClientsIds))
                manager.DisconnectClient(id, string.IsNullOrEmpty(reason) ? "The server reset." : reason);

            winnerTeam = -1;
            lastDeadTeam = -1;
            roundsCompleted = 0;
            frozen = false;
            prepLocked = false;
            hostPendingUntil = 0f;

            if (SceneManager.GetActiveScene().name == MenuScene)
            {
                phase = Phase.Lobby;
            }
            else
            {
                phase = Phase.Loading; // joins are refused until the Menu scene is back (OnLoadEventCompleted reopens the lobby)
                manager.SceneManager.LoadScene(MenuScene, LoadSceneMode.Single);
            }
        }

        // Both buttons connect to the dedicated server. Hosting opens the server's lobby (you become its leader and
        // set the rules); joining enters a lobby someone has already opened. The intent travels in the connection
        // payload and the server checks it in Approve.
        void HostGame() => ConnectToServer(true);
        void JoinGame() => ConnectToServer(false);

        void ConnectToServer(bool host)
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
                notice = "Enter the server's address.";
                return;
            }
            try { PlayerPrefs.SetString(ServerAddressPref, joinAddress.Trim()); } catch { }

            transport.SetConnectionData(address, port);
            manager.NetworkConfig.ConnectionData = new[] { host ? IntentHost : IntentJoin };
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
            response.CreatePlayerObject = false;
            response.Pending = false;
            bool wantsHost = request.Payload != null && request.Payload.Length > 0 && request.Payload[0] == IntentHost;
            // A host approved a moment ago may not have a seat yet; this keeps a second host from slipping in.
            bool lobbyOpen = UsedSeats() > 0 || Time.unscaledTime < hostPendingUntil;

            if (phase != Phase.Lobby) response.Reason = "A match is already in progress.";
            else if (wantsHost && lobbyOpen) response.Reason = "A game is already being hosted on this server. Join it instead.";
            else if (!wantsHost && !lobbyOpen) response.Reason = "No game is being hosted yet. Host one, or wait for someone to.";
            else if (FreeSeat() < 0) response.Reason = "The game is full.";
            else
            {
                response.Approved = true;
                if (wantsHost) hostPendingUntil = Time.unscaledTime + 10f;
                return;
            }
            response.Approved = false;
        }

        int FreeSeat()
        {
            for (int i = 0; i < seats.Length; i++)
                if (!seats[i].used) return i;
            return -1;
        }

        int UsedSeats()
        {
            int n = 0;
            foreach (Seat s in seats) if (s.used) n++;
            return n;
        }

        // Team id (matches Health.Team, 1-based) a seat fights for.
        int TeamOfSeat(int seat) => mode == GameMode.FreeForAll ? seat + 1 : seats[seat].team + 1;

        // Everyone ready, at least two players and at least two different teams.
        bool CanStart()
        {
            if (UsedSeats() < 2) return false;
            var teams = new HashSet<int>();
            for (int i = 0; i < seats.Length; i++)
            {
                if (!seats[i].used) continue;
                if (!seats[i].ready) return false;
                teams.Add(TeamOfSeat(i));
            }
            return teams.Count >= 2;
        }

        // Puts the next joiner on whichever team is smaller.
        int SmallerTeam()
        {
            int a = 0, b = 0;
            foreach (Seat s in seats)
            {
                if (!s.used) continue;
                if (s.team == 0) a++; else b++;
            }
            return a <= b ? 0 : 1;
        }

        void RegisterHandlers()
        {
            CustomMessagingManager msg = manager.CustomMessagingManager;
            msg.RegisterNamedMessageHandler("pick", OnPickMessage);
            msg.RegisterNamedMessageHandler("ready", OnReadyMessage);
            msg.RegisterNamedMessageHandler("team", OnTeamMessage);
            msg.RegisterNamedMessageHandler("lobby", OnLobbyMessage);
            msg.RegisterNamedMessageHandler("roundEnd", OnRoundEndMessage);
            msg.RegisterNamedMessageHandler("prep", OnPrepMessage);
            msg.RegisterNamedMessageHandler("prepLock", OnPrepLockMessage);
            msg.RegisterNamedMessageHandler("nextRound", OnNextRoundMessage);
            msg.RegisterNamedMessageHandler("over", OnOverMessage);
            msg.RegisterNamedMessageHandler("rematch", OnRematchMessage);
            msg.RegisterNamedMessageHandler("toLobby", OnToLobbyMessage);
            msg.RegisterNamedMessageHandler("orbSpawn", OnOrbSpawnMessage);
            msg.RegisterNamedMessageHandler("orbClaim", OnOrbClaimMessage);
            msg.RegisterNamedMessageHandler("orbTaken", OnOrbTakenMessage);
            msg.RegisterNamedMessageHandler("chargeOrb", OnChargeOrbMessage);
            msg.RegisterNamedMessageHandler("chargeOrbHit", OnChargeOrbHitMessage);
            msg.RegisterNamedMessageHandler("bestOf", OnBestOfMessage);
            msg.RegisterNamedMessageHandler("mode", OnModeMessage);
            msg.RegisterNamedMessageHandler("matchChoice", OnMatchChoiceMessage);
        }

        // --- Lobby leader (sets the rules; the host, or on a dedicated server the lowest occupied seat) ------------

        int LeaderSeat()
        {
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].used) return i;
            return -1;
        }

        // True on the machine whose player chooses the mode, match length and rematch.
        bool IAmLeader => manager.IsServer ? !dedicated : LeaderSeat() >= 0 && LeaderSeat() == MySeat();

        bool SenderIsLeader(ulong sender)
        {
            int leader = LeaderSeat();
            return leader >= 0 && seats[leader].clientId == sender;
        }

        void SendToServer(string message, int value)
        {
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(value);
            manager.CustomMessagingManager.SendNamedMessage(message, NetworkManager.ServerClientId, writer);
        }

        void OnBestOfMessage(ulong sender, FastBufferReader reader)
        {
            if (!manager.IsServer) return;
            reader.ReadValueSafe(out int value);
            if (SenderIsLeader(sender) && (value == 3 || value == 5)) ServerSetBestOf(value);
        }

        void OnModeMessage(ulong sender, FastBufferReader reader)
        {
            if (!manager.IsServer) return;
            reader.ReadValueSafe(out int value);
            if (SenderIsLeader(sender)) ServerSetMode((GameMode)Mathf.Clamp(value, 0, 1));
        }

        const int ChoiceRematch = 0, ChoiceLobby = 1;

        void OnMatchChoiceMessage(ulong sender, FastBufferReader reader)
        {
            if (!manager.IsServer) return;
            reader.ReadValueSafe(out int choice);
            if (!SenderIsLeader(sender) || phase != Phase.Over) return;
            if (choice == ChoiceRematch) StartRematch();
            else ReturnToLobby();
        }

        void ChooseRematch()
        {
            if (manager.IsServer) StartRematch();
            else SendToServer("matchChoice", ChoiceRematch);
        }

        void ChooseLobby()
        {
            if (manager.IsServer) ReturnToLobby();
            else SendToServer("matchChoice", ChoiceLobby);
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

        // --- Charge orb (the server owns its health; see ChargeOrb) -------------------------------------------------

        // Server -> everyone: the orb appeared (value = max health), was damaged (value = health left) or was
        // destroyed (team = who destroyed it).
        public void BroadcastChargeOrb(int evt, Vector3 position, float value, int team)
        {
            if (!manager.IsServer) return;
            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(evt);
            writer.WriteValueSafe(position);
            writer.WriteValueSafe(value);
            writer.WriteValueSafe(team);
            manager.CustomMessagingManager.SendNamedMessageToAll("chargeOrb", writer);
        }

        void OnChargeOrbMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            reader.ReadValueSafe(out int evt);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out float value);
            reader.ReadValueSafe(out int team);
            ChargeOrb.OnServerEvent(evt, position, value, team);
        }

        // Client -> server: my character hit the orb.
        public void SendChargeOrbHit(float damage, bool aoe, float pierce)
        {
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(damage);
            writer.WriteValueSafe(aoe);
            writer.WriteValueSafe(pierce);
            manager.CustomMessagingManager.SendNamedMessage("chargeOrbHit", NetworkManager.ServerClientId, writer);
        }

        const float MaxChargeOrbHit = 500f;

        void OnChargeOrbHitMessage(ulong sender, FastBufferReader reader)
        {
            if (!manager.IsServer) return;
            reader.ReadValueSafe(out float damage);
            reader.ReadValueSafe(out bool aoe);
            reader.ReadValueSafe(out float pierce);

            ChargeOrb orb = ChargeOrb.Current;
            if (orb == null || phase != Phase.Playing || !(damage > 0f) || damage > MaxChargeOrbHit) return;

            // The hit is credited to the sender's own living character.
            foreach (NetPlayer p in NetPlayer.All)
            {
                if (p == null || p.OwnerClientId != sender || p.Health.IsDead) continue;
                orb.Health.ApplyDamageLocal(damage, p.gameObject, aoe, pierce);
                return;
            }
        }

        void OnClientConnected(ulong clientId)
        {
            if (manager.IsServer)
            {
                if (clientId == manager.LocalClientId) return;
                int free = FreeSeat();
                if (free < 0)
                {
                    manager.DisconnectClient(clientId, "The game is full.");
                    return;
                }
                seats[free] = new Seat { used = true, clientId = clientId, character = 0, ready = false, team = SmallerTeam() };
                hostPendingUntil = 0f;
                if (dedicated) Debug.Log($"[Server] Client {clientId} joined (seat {free}, {UsedSeats()}/{MaxSeats}).");
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
                bool hadSeat = false;
                for (int i = 0; i < seats.Length; i++)
                {
                    if (!seats[i].used || seats[i].clientId != clientId) continue;
                    seats[i] = default;
                    hadSeat = true;
                }

                // A dedicated server resetting has already cleared the seats; those disconnects need no handling.
                if (dedicated)
                {
                    if (!hadSeat) return;
                    Debug.Log($"[Server] Client {clientId} left ({UsedSeats()}/{MaxSeats}).");
                }

                if (phase == Phase.Lobby) BroadcastLobby();
                else if (phase == Phase.Loading) LeaveToMenu("A player left the match.");
                else ServerEvaluateRound(clientId); // the match carries on while at least two teams remain
            }
            else if (clientId == manager.LocalClientId)
            {
                string reason = manager.DisconnectReason;
                bool wasConnecting = phase == Phase.Connecting;
                LeaveToMenu(!string.IsNullOrEmpty(reason) ? reason
                    : wasConnecting ? $"Could not reach the server at {joinAddress}. Check that it is running and UDP port {DefaultPort} is open."
                    : "Disconnected from the server.");
            }
        }

        public void LeaveToMenu(string message)
        {
            if (dedicated)
            {
                ServerResetToLobby(message);
                return;
            }

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
            if (manager.IsServer) ServerSetBestOf(value);
            else if (IAmLeader) SendToServer("bestOf", value);
        }

        void SetMode(GameMode value)
        {
            if (manager.IsServer) ServerSetMode(value);
            else if (IAmLeader) SendToServer("mode", (int)value);
        }

        void ServerSetBestOf(int value)
        {
            if (!manager.IsServer || phase != Phase.Lobby || bestOf == value) return;
            bestOf = value;
            // Changing the rules un-readies everyone so nobody starts a match they didn't agree to.
            for (int i = 0; i < seats.Length; i++) seats[i].ready = false;
            myReady = false;
            BroadcastLobby();
        }

        void ServerSetMode(GameMode value)
        {
            if (!manager.IsServer || phase != Phase.Lobby || mode == value) return;
            mode = value;
            // Teams start out balanced (alternating A / B); players can swap sides afterwards.
            int next = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (!seats[i].used) continue;
                seats[i].team = next;
                next = 1 - next;
                seats[i].ready = false;
            }
            myReady = false;
            BroadcastLobby();
        }

        void SetMyTeam(int teamIndex)
        {
            if (mode != GameMode.Teams) return;
            myReady = false;
            if (manager.IsServer)
            {
                ApplyTeam(0, teamIndex);
            }
            else
            {
                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe(teamIndex);
                manager.CustomMessagingManager.SendNamedMessage("team", NetworkManager.ServerClientId, writer);
            }
        }

        void ApplyTeam(int seat, int teamIndex)
        {
            if (seat < 0 || !seats[seat].used || mode != GameMode.Teams || phase != Phase.Lobby) return;
            seats[seat].team = Mathf.Clamp(teamIndex, 0, 1);
            seats[seat].ready = false;
            BroadcastLobby();
        }

        void OnTeamMessage(ulong sender, FastBufferReader reader)
        {
            if (!manager.IsServer) return;
            reader.ReadValueSafe(out int teamIndex);
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].used && seats[i].clientId == sender) ApplyTeam(i, teamIndex);
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
            // A host alone has nobody to tell; a dedicated server's first player needs the lobby straight away.
            if (UsedSeats() < (dedicated ? 1 : 2)) return;

            using var writer = new FastBufferWriter(256, Allocator.Temp);
            for (int i = 0; i < seats.Length; i++)
            {
                writer.WriteValueSafe(seats[i].used);
                writer.WriteValueSafe(seats[i].clientId);
                writer.WriteValueSafe(seats[i].character);
                writer.WriteValueSafe(seats[i].ready);
                writer.WriteValueSafe(seats[i].team);
            }
            writer.WriteValueSafe(bestOf);
            writer.WriteValueSafe((int)mode);
            manager.CustomMessagingManager.SendNamedMessageToAll("lobby", writer);
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
                reader.ReadValueSafe(out seats[i].team);
            }
            reader.ReadValueSafe(out bestOf);
            reader.ReadValueSafe(out int modeValue);
            mode = (GameMode)Mathf.Clamp(modeValue, 0, 1);

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

            if (phase == Phase.Lobby && !dedicated) select.Show(); else select.Hide();

            if (!manager.IsServer && phase == Phase.Lobby && IsArenaScene(SceneManager.GetActiveScene().name))
            {
                phase = Phase.Playing;
                introUntil = Time.time + RoundIntro;
            }

            // Client: the host sent everyone back to character select; the Menu scene has finished loading.
            if (!manager.IsServer && phase == Phase.Loading && SceneManager.GetActiveScene().name == MenuScene)
                phase = Phase.Lobby;

            if (phase == Phase.Prep && !prepLocked && NetPlayer.Local != null)
                upgradePicker.HandleKeys(NetPlayer.Local.Upgrades);

            if (manager.IsServer)
            {
                // Report -> prep area.
                if (phase == Phase.RoundEnd && Time.time >= nextRoundTime) StartPrep();

                // Prep: lock in once everyone has chosen (and the menu has been up a moment) or the window runs out,
                // then count down and start the round.
                if (phase == Phase.Prep)
                {
                    if (!prepLocked && ((AllUpgradesSettled() && Time.time >= earliestNextRound) || Time.time >= countdownEnd))
                        ServerLockPrep();
                    else if (prepLocked && Time.time >= prepGoTime)
                        StartNextRound();
                }
            }

            UpdateSpectate();

            if (manager.IsServer && phase == Phase.Lobby && CanStart())
            {
                if (dedicated) Debug.Log($"[Server] Everyone is ready: starting a {UsedSeats()}-player match.");
                phase = Phase.Loading;
                manager.SceneManager.LoadScene(PickArenaScene(), LoadSceneMode.Single);
            }
        }

        // --- Spectating: once you're down, the camera follows a living fighter (teammates first); click to switch ---

        NetPlayer spectateTarget;
        TopDownCamera spectateCamera;

        public bool Spectating => spectateTarget != null;

        void UpdateSpectate()
        {
            NetPlayer me = NetPlayer.Local;
            bool inMatch = phase == Phase.Playing || phase == Phase.RoundEnd || phase == Phase.Over; // prep: everyone is back up
            bool down = inMatch && me != null && me.Health.IsDead;

            if (spectateCamera == null) spectateCamera = FindAnyObjectByType<TopDownCamera>();
            if (spectateCamera == null) return;

            if (!down)
            {
                if (spectateTarget != null)
                {
                    spectateTarget = null;
                    if (me != null) spectateCamera.SetTarget(me.transform);
                }
                return;
            }

            if (spectateTarget == null || spectateTarget.Health.IsDead || Input.GetMouseButtonDown(0))
                spectateTarget = NextSpectateTarget(me, spectateTarget);

            spectateCamera.SetTarget(spectateTarget != null ? spectateTarget.transform : me.transform);
        }

        // Living fighters other than you, teammates first; returns the one after `current` (wrapping), or the first.
        static NetPlayer NextSpectateTarget(NetPlayer me, NetPlayer current)
        {
            var candidates = new List<NetPlayer>();
            foreach (NetPlayer p in NetPlayer.All)
                if (p != null && p != me && !p.Health.IsDead) candidates.Add(p);
            if (candidates.Count == 0) return null;

            int myTeam = me.Health.Team;
            candidates.Sort((a, b) =>
            {
                int ka = a.Health.Team == myTeam ? 0 : 1, kb = b.Health.Team == myTeam ? 0 : 1;
                return ka != kb ? ka.CompareTo(kb) : a.OwnerClientId.CompareTo(b.OwnerClientId);
            });

            int index = current != null ? candidates.IndexOf(current) : -1;
            return candidates[(index + 1) % candidates.Count];
        }

        void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (!manager.IsServer) return;

            if (sceneName == MenuScene)
            {
                // Back from a finished match: everyone is in the lobby again with the same seats, teams and rules.
                for (int i = 0; i < seats.Length; i++) seats[i].ready = false;
                myReady = false;
                winnerTeam = -1;
                lastDeadTeam = -1;
                roundsCompleted = 0;
                frozen = false;
                phase = Phase.Lobby;
                BroadcastLobby();
                return;
            }

            if (!IsArenaScene(sceneName)) return;

            PlanSpawns();
            for (int seat = 0; seat < seats.Length; seat++)
            {
                if (!seats[seat].used) continue;

                var prefab = Resources.Load<GameObject>(CharacterPrefabs[seats[seat].character]);
                Vector3 position = SpawnPosition(seat, out Quaternion rotation);
                GameObject player = Instantiate(prefab, position, rotation);
                var networkObject = player.GetComponent<NetworkObject>();
                networkObject.SpawnAsPlayerObject(seats[seat].clientId, true);
                player.GetComponent<NetPlayer>().ServerSetSlot(seat, TeamOfSeat(seat));
            }

            winnerTeam = -1;
            lastDeadTeam = -1;
            roundsCompleted = 0;
            frozen = false;
            phase = Phase.Playing;
            introUntil = Time.time + RoundIntro;
        }

        const float SpawnRadius = 23f; // arena radius is 28; prep ring (PrepRadius) must stay inside it

        // Free-for-all spawn plan for the ready-room arenas, built once per match by the server before anyone is spawned.
        readonly Dictionary<int, (Vector3 position, Quaternion rotation)> ffaSpawns = new Dictionary<int, (Vector3, Quaternion)>();

        // Free-for-all in an arena with ready rooms: two random players start one in each room, everyone else at a random
        // spot around the pit (kept clear of the two doorways). Teams mode just uses the rooms by team (see SpawnPosition).
        void PlanSpawns()
        {
            ffaSpawns.Clear();
            if (mode == GameMode.Teams || !ReadyRoom.Available) return;

            var order = new List<int>();
            for (int i = 0; i < seats.Length; i++) if (seats[i].used) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            bool leftFirst = UnityEngine.Random.value < 0.5f;
            int inRooms = Mathf.Min(2, order.Count);
            for (int k = 0; k < inRooms; k++)
            {
                ReadyRoom room = ReadyRoom.Find(k == 0 ? leftFirst : !leftFirst);
                Vector3 position = room.SlotPosition(0, 1, out Quaternion rotation);
                ffaSpawns[order[k]] = (position, rotation);
            }

            // The rest: random but evenly spread positions on the two arcs between the doorways (14 degrees clear of each).
            const float Clear = 14f, Arc = 180f - 2f * Clear;
            int others = order.Count - inRooms;
            float shift = others > 0 ? UnityEngine.Random.value * (2f * Arc / others) : 0f;
            for (int k = 0; k < others; k++)
            {
                float u = (shift + (k + 0.5f + UnityEngine.Random.Range(-0.25f, 0.25f)) * (2f * Arc / others)) % (2f * Arc);
                float angle = (u < Arc ? Clear + u : 180f + Clear + (u - Arc)) * Mathf.Deg2Rad;
                Vector3 position = new Vector3(Mathf.Cos(angle) * SpawnRadius, 0.1f, Mathf.Sin(angle) * SpawnRadius);
                Vector3 toCentre = -position;
                toCentre.y = 0f;
                ffaSpawns[order[inRooms + k]] = (position, Quaternion.LookRotation(toCentre.normalized, Vector3.up));
            }
        }

        // Teams: each team in its ready room (team A left, team B right). Free-for-all: see PlanSpawns. Arenas without
        // ready rooms fall back to the old layout: teams lined up on the left/right edge, free-for-all evenly round the pit.
        Vector3 SpawnPosition(int seat, out Quaternion rotation)
        {
            if (ffaSpawns.TryGetValue(seat, out var planned))
            {
                rotation = planned.rotation;
                return planned.position;
            }

            if (mode == GameMode.Teams && ReadyRoom.Available)
            {
                int roomTeam = seats[seat].team, roomIndex = 0, roomCount = 0;
                for (int i = 0; i < seats.Length; i++)
                {
                    if (!seats[i].used || seats[i].team != roomTeam) continue;
                    if (i < seat) roomIndex++;
                    roomCount++;
                }
                return ReadyRoom.Find(roomTeam == 0).SlotPosition(roomIndex, roomCount, out rotation);
            }

            Vector3 position;
            if (mode == GameMode.Teams)
            {
                int team = seats[seat].team, index = 0, count = 0;
                for (int i = 0; i < seats.Length; i++)
                {
                    if (!seats[i].used || seats[i].team != team) continue;
                    if (i < seat) index++;
                    count++;
                }
                float z = (index - (count - 1) * 0.5f) * 5f;
                position = new Vector3(team == 0 ? -SpawnRadius : SpawnRadius, 0.1f, z);
            }
            else
            {
                int index = 0, count = 0;
                for (int i = 0; i < seats.Length; i++)
                {
                    if (!seats[i].used) continue;
                    if (i < seat) index++;
                    count++;
                }
                float angle = Mathf.PI * 2f * index / Mathf.Max(1, count);
                position = new Vector3(-Mathf.Cos(angle) * SpawnRadius, 0.1f, Mathf.Sin(angle) * SpawnRadius);
            }

            Vector3 toCentre = -position;
            toCentre.y = 0f;
            rotation = toCentre.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCentre.normalized, Vector3.up) : Quaternion.identity;
            return position;
        }

        // Everyone has finished choosing (characters with no upgrades settle immediately).
        bool AllUpgradesSettled()
        {
            foreach (NetPlayer p in NetPlayer.All)
                if (p != null && p.UpgradeRound < roundsCompleted) return false;
            return true;
        }

        public void ServerOnDeath(NetPlayer victim)
        {
            if (!manager.IsServer || phase != Phase.Playing || victim == null) return;
            victim.ServerDead = true;
            lastDeadTeam = victim.Health.Team;
            ServerEvaluateRound(ulong.MaxValue);
        }

        // A round ends when only one team still has a fighter standing; that team takes the round
        // ("kills" on NetPlayer counts rounds won, stored on every member of the team).
        // Also called when a player leaves: the match ends if fewer than two teams remain.
        void ServerEvaluateRound(ulong leftClient)
        {
            if (!manager.IsServer) return;

            var teams = new HashSet<int>();
            var alive = new HashSet<int>();
            foreach (NetPlayer p in NetPlayer.All)
            {
                if (p == null || p.OwnerClientId == leftClient) continue;
                teams.Add(p.Health.Team);
                if (!p.ServerDead) alive.Add(p.Health.Team);
            }

            if (teams.Count < 2)
            {
                LeaveToMenu("Everyone else left the match.");
                return;
            }

            if (phase != Phase.Playing || alive.Count > 1) return;

            int winner = lastDeadTeam;
            foreach (int t in alive) winner = t;
            if (!teams.Contains(winner)) foreach (int t in teams) winner = t;

            int wins = 0;
            foreach (NetPlayer p in NetPlayer.All)
            {
                if (p == null || p.OwnerClientId == leftClient || p.Health.Team != winner) continue;
                p.ServerAddKill();
                wins = p.Kills;
            }

            frozen = true;
            winnerTeam = winner;

            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(winnerTeam);

            if (wins >= RoundsToWin)
            {
                phase = Phase.Over;
                manager.CustomMessagingManager.SendNamedMessageToAll("over", writer);
            }
            else
            {
                roundsCompleted++;
                writer.WriteValueSafe(roundsCompleted);
                phase = Phase.RoundEnd;
                nextRoundTime = Time.time + RoundPause;
                countdownEnd = nextRoundTime;
                manager.CustomMessagingManager.SendNamedMessageToAll("roundEnd", writer);
                if (NetPlayer.Local != null) NetPlayer.Local.OnRoundEnded(roundsCompleted);
            }
        }

        void OnRoundEndMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            reader.ReadValueSafe(out winnerTeam);
            reader.ReadValueSafe(out roundsCompleted);
            phase = Phase.RoundEnd;
            countdownEnd = Time.time + RoundPause;
            frozen = true;
            if (NetPlayer.Local != null) NetPlayer.Local.OnRoundEnded(roundsCompleted);
        }

        void OnOverMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            reader.ReadValueSafe(out winnerTeam);
            phase = Phase.Over;
            frozen = true;
        }

        // ---- Prep area ---------------------------------------------------------------------------------------------

        void StartPrep()
        {
            if (!manager.IsServer) return;

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(true);
            manager.CustomMessagingManager.SendNamedMessageToAll("prep", writer);
            BeginPrep();
        }

        void OnPrepMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            BeginPrep();
        }

        // Everyone back at their spawn at full health, free to walk inside the prep ring but not to fight; upgrade menu open.
        void BeginPrep()
        {
            phase = Phase.Prep;
            frozen = false;
            prepLocked = false;
            earliestNextRound = Time.time + PrepMin;
            countdownEnd = Time.time + PrepWindow;

            // The server owns health: everyone is restored there and can't be hurt during prep.
            if (manager.IsServer)
                foreach (NetPlayer p in NetPlayer.All)
                {
                    if (p == null) continue;
                    p.ServerResetForRound();
                    p.Health.Invulnerable = true;
                }

            if (NetPlayer.Local != null)
            {
                NetPlayer.Local.ResetForRematch();
                NetPlayer.Local.Health.Invulnerable = true; // nothing (gas, leftover bleeds) can hurt anyone in prep
                var respawner = NetPlayer.Local.GetComponent<Respawner>();
                if (respawner != null)
                {
                    if (prepArea != null) Destroy(prepArea.gameObject);
                    // Players in a ready room are held in by its walls and gate; only a free spawn gets the barrier ring.
                    prepArea = ReadyRoom.AnyContains(respawner.SpawnPosition) ? null : PrepArea.Show(respawner.SpawnPosition);
                }
            }

            GasZone gas = FindAnyObjectByType<GasZone>();
            if (gas != null) gas.Restart();
            HealingOrb.ClearAll();
            ChargeOrb.Despawn();
        }

        void ServerLockPrep()
        {
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(true);
            manager.CustomMessagingManager.SendNamedMessageToAll("prepLock", writer);
            LockPrep();
        }

        void OnPrepLockMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            LockPrep();
        }

        // Picks are final (anyone undecided gets a random one) and the round-start countdown runs.
        void LockPrep()
        {
            prepLocked = true;
            prepGoTime = Time.time + PrepCountdown;
            if (NetPlayer.Local != null) NetPlayer.Local.SettleUpgradePick();
        }

        PrepArea prepArea;

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
            roundsCompleted = 0;
            if (NetPlayer.Local != null) NetPlayer.Local.ResetUpgrades(); // a rematch starts from scratch
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(true);
            manager.CustomMessagingManager.SendNamedMessageToAll("rematch", writer);
            ApplyRoundReset();
        }

        // Host sends everyone back to character select without tearing down the connection.
        void ReturnToLobby()
        {
            if (!manager.IsServer || phase != Phase.Over) return;

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(true);
            manager.CustomMessagingManager.SendNamedMessageToAll("toLobby", writer);
            phase = Phase.Loading;
            frozen = false;
            manager.SceneManager.LoadScene(MenuScene, LoadSceneMode.Single);
        }

        void OnToLobbyMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            phase = Phase.Loading;
            frozen = false;
        }

        void OnRematchMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            roundsCompleted = 0;
            if (NetPlayer.Local != null) NetPlayer.Local.ResetUpgrades();
            ApplyRoundReset();
        }

        // Fresh round (or fresh match after a rematch): everyone back at their spawn, arena reset.
        void ApplyRoundReset()
        {
            winnerTeam = -1;
            lastDeadTeam = -1;
            frozen = false;
            prepLocked = false;
            phase = Phase.Playing;
            introUntil = Time.time + RoundIntro;
            if (prepArea != null) Destroy(prepArea.gameObject);
            if (NetPlayer.Local != null)
            {
                NetPlayer.Local.SettleUpgradePick();
                NetPlayer.Local.Health.Invulnerable = false;
                NetPlayer.Local.ResetForRematch();
            }
            if (manager.IsServer)
                foreach (NetPlayer player in NetPlayer.All)
                {
                    if (player == null) continue;
                    player.ServerResetRoundStats();
                    player.ServerResetForRound();
                    player.Health.Invulnerable = false;
                }
            GasZone gas = FindAnyObjectByType<GasZone>();
            if (gas != null) gas.Restart();
            OrbSpawner orbs = FindAnyObjectByType<OrbSpawner>();
            if (orbs != null) orbs.Restart();
            else
            {
                HealingOrb.ClearAll();
                ChargeOrb.Despawn();
            }
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
            if (Instance != this || dedicated) return;
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
                case Phase.Prep:
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
            GUI.Label(new Rect(0, 250, w, 40), "AN  ARENA  BRAWLER  FOR  2 - 6  PLAYERS", subtitle);
            Rect(new Rect(w * 0.5f - 260, 305, 520, 3), DarkGold);

            float bx = w * 0.5f - 230;
            GUI.Label(new Rect(bx, 340, 460, 36), "SERVER ADDRESS", labelCentered);
            joinAddress = GUI.TextField(new Rect(bx, 380, 460, 56), joinAddress, 64, field);
            if (GUI.Button(new Rect(bx, 470, 460, 84), "HOST GAME", buttonBig)) HostGame();
            if (GUI.Button(new Rect(bx, 574, 460, 84), "JOIN GAME", buttonBig)) JoinGame();

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
                "WASD move   ·   mouse aim   ·   LMB attack   ·   a round ends when a whole team is down", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
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

            var roster = CharacterSelectView.Roster;
            int mine = manager.IsServer ? 0 : MySeat();
            int players = UsedSeats();
            var info = roster[Mathf.Clamp(myCharacter, 0, roster.Length - 1)];
            Color accent = info.accent;
            if (showUpgrades && UpgradeCatalog.For(UpgradeKey(info)) == null) showUpgrades = false;
            bool modal = showUpgrades;
            Vector2 mouse = modal ? new Vector2(-10000f, -10000f) : Event.current.mousePosition;
            bool repaint = Event.current.type == EventType.Repaint;
            if (modal) GUI.enabled = false; // everything beneath the overlay is inert while it is open

            // Header.
            GUI.Label(new Rect(0, 14, w, 70), "CHAMPION SELECT", new GUIStyle(headline) { fontSize = 46 });
            Rect(new Rect(w * 0.5f - 300, 86, 600, 3), DarkGold);

            // --- Centre: the full model of the chosen champion, drag to rotate -----------------------------------------
            float stageSize = 900f;
            var stageRect = new Rect(w * 0.5f - stageSize * 0.5f, 100, stageSize, stageSize);
            for (int i = 0; i < 6; i++) // soft glow behind the feet
            {
                float gw = 520f - i * 70f;
                Rect(new Rect(w * 0.5f - gw * 0.5f, 860 - i * 6f, gw, 30f), new Color(accent.r, accent.g, accent.b, 0.05f));
            }

            Event e = Event.current;
            var dragZone = new Rect(w * 0.5f - 280, 160, 560, 690);
            if (e.type == EventType.MouseDown && !modal && dragZone.Contains(e.mousePosition)) { draggingModel = true; e.Use(); }
            else if (e.type == EventType.MouseDrag && draggingModel) { modelDrag += e.delta.x; e.Use(); }
            else if (e.type == EventType.MouseUp) draggingModel = false;

            if (repaint)
            {
                select.Render(myCharacter, draggingModel ? modelDrag : 0f);
                modelDrag = 0f;
                if (select.StageTexture != null) GUI.DrawTexture(stageRect, select.StageTexture, ScaleMode.StretchToFill, true);
            }

            // --- Left: champion info + abilities ----------------------------------------------------------------------
            var left = new Rect(30, 120, 570, 790);
            Frame(left, Panel, DarkGold, 3f);
            Rect(new Rect(left.x + 3, left.y + 3, left.width - 6, 8), new Color(accent.r, accent.g, accent.b, 0.85f));

            GUI.Label(new Rect(left.x + 20, left.y + 24, left.width - 40, 56), info.name, new GUIStyle(headline) { fontSize = 38, normal = { textColor = Color.white } });
            GUI.Label(new Rect(left.x + 20, left.y + 80, left.width - 40, 34), info.epithet, new GUIStyle(labelCentered) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Gold } });
            Rect(new Rect(left.x + 30, left.y + 124, left.width - 60, 2), DarkGold);

            GUI.Label(new Rect(left.x + 40, left.y + 140, 260, 50), info.role.ToString().ToUpper(), new GUIStyle(label) { fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = accent } });
            GUI.Label(new Rect(left.x + 40, left.y + 190, left.width - 80, 140), info.blurb, label);

            // Ability icons: normal abilities (ultimate last) on the first row, EX abilities on a row beneath.
            CharacterSelectView.AbilityInfo hoveredAbility = null;
            Rect hoveredRect = default;
            var keyStyle = new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = false };

            // Passive (if the champion has one): a small icon beside the role label.
            if (info.passive != null)
            {
                var pr = new Rect(left.xMax - 40 - 56, left.y + 134, 56, 56);
                GUI.Label(new Rect(pr.x - 150, pr.y, 144, pr.height), "PASSIVE",
                    new GUIStyle(keyStyle) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.6f, 0.58f, 0.55f) } });
                if (info.passive.icon != null) GUI.DrawTexture(pr, info.passive.icon, ScaleMode.StretchToFill, true);
                GUI.Label(new Rect(pr.x - 4, pr.y + 10, pr.width + 8, 36), Abbreviation(info.passive.name),
                    new GUIStyle(keyStyle) { fontSize = 22, normal = { textColor = Color.white } });
                if (pr.Contains(mouse)) { hoveredAbility = info.passive; hoveredRect = pr; }
            }
            for (int pass = 0; pass < 2; pass++)
            {
                var row = new System.Collections.Generic.List<CharacterSelectView.AbilityInfo>();
                foreach (var a in info.abilities) if (a.ex == (pass == 1)) row.Add(a);
                if (row.Count == 0) continue;
                const float icon = 72f, spacing = 16f;
                float rowW = row.Count * icon + (row.Count - 1) * spacing;
                float rx = left.x + (left.width - rowW) * 0.5f;
                float ry = left.y + (pass == 0 ? 380 : 560);
                if (pass == 1)
                {
                    GUI.Label(new Rect(left.x + 20, ry - 40, left.width - 40, 30), "EX ABILITIES",
                        new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = new Color(0.8f, 0.6f, 1f) } });
                    Rect(new Rect(left.x + 30, ry - 46, left.width - 60, 2), DarkGold);
                }
                for (int i = 0; i < row.Count; i++)
                {
                    var ir = new Rect(rx + i * (icon + spacing), ry + 28, icon, icon);
                    GUI.Label(new Rect(ir.x - 20, ry, icon + 40, 28), row[i].key, keyStyle);
                    if (row[i].icon != null) GUI.DrawTexture(ir, row[i].icon, ScaleMode.StretchToFill, true);
                    GUI.Label(new Rect(ir.x - 4, ir.y + 20, icon + 8, 36), Abbreviation(row[i].name),
                        new GUIStyle(keyStyle) { fontSize = 26, normal = { textColor = Color.white } });
                    if (ir.Contains(mouse)) { hoveredAbility = row[i]; hoveredRect = ir; }
                }
            }

            // Opens the overlay listing every upgrade this champion can be offered.
            if (UpgradeCatalog.For(UpgradeKey(info)) != null)
            {
                var ub = new Rect(left.x + 130, left.y + 666, left.width - 260, 40);
                if (GUI.Button(ub, "VIEW ALL UPGRADES", new GUIStyle(button) { fontSize = 22 })) showUpgrades = true;
            }

            if (!string.IsNullOrEmpty(info.footnote))
                GUI.Label(new Rect(left.x + 20, left.yMax - 80, left.width - 40, 36), info.footnote,
                    new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, wordWrap = false });
            GUI.Label(new Rect(left.x + 20, left.yMax - 44, left.width - 40, 32), "Hover an ability for details",
                new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 17, wordWrap = false, normal = { textColor = new Color(0.6f, 0.58f, 0.55f) } });

            // --- Right: role filters + champion tile grid -------------------------------------------------------------
            const int cols = 5, rows = 4;
            const float tile = 104f, tileGap = 14f;
            float gridW = cols * tile + (cols - 1) * tileGap;
            var right = new Rect(w - 30 - (gridW + 60), 120, gridW + 60, 790);

            var filterPanel = new Rect(right.x, right.y, right.width, 120);
            Frame(filterPanel, Panel, DarkGold, 3f);
            GUI.Label(new Rect(filterPanel.x + 20, filterPanel.y + 8, 300, 36), "Filters", new GUIStyle(label) { fontSize = 28, normal = { textColor = Gold } });
            string[] roleNames = { "MELEE", "RANGED", "SUPPORT" };
            for (int i = 0; i < 3; i++)
            {
                var fr = new Rect(filterPanel.x + 20 + i * ((filterPanel.width - 40) / 3f), filterPanel.y + 52, (filterPanel.width - 40) / 3f - 12f, 52);
                if (roleFilter[i]) Rect(new Rect(fr.x - 3, fr.y - 3, fr.width + 6, fr.height + 6), Gold);
                if (GUI.Button(fr, roleNames[i], new GUIStyle(button) { fontSize = 22 })) roleFilter[i] = !roleFilter[i];
            }
            bool anyFilter = roleFilter[0] || roleFilter[1] || roleFilter[2];

            var gridPanel = new Rect(right.x, right.y + 140, right.width, right.height - 140);
            Frame(gridPanel, Panel, DarkGold, 3f);
            GUI.Label(new Rect(gridPanel.x + 20, gridPanel.y + 8, 300, 36), "Champions", new GUIStyle(label) { fontSize = 28, normal = { textColor = Gold } });

            int hoveredTile = -1;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int i = r * cols + c;
                var t = new Rect(gridPanel.x + 30 + c * (tile + tileGap), gridPanel.y + 60 + r * (tile + tileGap), tile, tile);
                if (i >= roster.Length)
                {
                    Frame(t, new Color(0.03f, 0.04f, 0.06f, 0.85f), new Color(0.2f, 0.2f, 0.22f), 2f);
                    continue;
                }

                var ci = roster[i];
                bool selected = myCharacter == i;
                bool opponentPicked = false;
                for (int s = 0; s < seats.Length; s++)
                    if (s != mine && seats[s].used && seats[s].character == i) opponentPicked = true;
                bool dim = anyFilter && !roleFilter[(int)ci.role];
                bool hover = t.Contains(mouse);
                if (hover) hoveredTile = i;

                Rect(t, selected ? ci.accent : hover ? Gold : DarkGold);
                var inner = new Rect(t.x + (selected ? 5 : 3), t.y + (selected ? 5 : 3), t.width - (selected ? 10 : 6), t.height - (selected ? 10 : 6));
                Rect(inner, new Color(0.05f, 0.06f, 0.09f));
                if (select.Portraits != null && select.Portraits[i] != null)
                    GUI.DrawTexture(inner, select.Portraits[i], ScaleMode.ScaleAndCrop, true);
                if (dim) Rect(inner, new Color(0f, 0f, 0f, 0.65f));

                if (opponentPicked)
                {
                    Rect(new Rect(inner.x, inner.yMax - 24, inner.width, 24), new Color(0f, 0f, 0f, 0.75f));
                    GUI.Label(new Rect(inner.x, inner.yMax - 24, inner.width, 24), "PICKED",
                        new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = ci.accent } });
                }

                if (GUI.Button(t, GUIContent.none, GUIStyle.none)) SetMyCharacter(i);
            }

            // Hover card for a tile (like the info popup in the reference).
            if (hoveredTile >= 0)
            {
                var hc = roster[hoveredTile];
                var tip = new Rect(right.x - 380, right.y + 140, 360, 200);
                Frame(tip, new Color(0.04f, 0.05f, 0.08f, 0.97f), DarkGold, 2f);
                GUI.Label(new Rect(tip.x + 14, tip.y + 8, tip.width - 28, 32), hc.name, new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } });
                GUI.Label(new Rect(tip.x + 14, tip.y + 44, tip.width - 28, 140), hc.blurb, new GUIStyle(labelSmall) { fontSize = 18 });
            }

            // --- Bottom: status, match length, ready, back -------------------------------------------------------------
            // Roster: one chip per player (3 per row), tinted by team in Teams mode.
            float chipX = 620f, chipGap = 8f;
            float chipW = (right.x - 20f - chipX - chipGap * 2f) / 3f;
            int chip = 0;
            for (int s = 0; s < seats.Length; s++)
            {
                if (!seats[s].used) continue;
                var cr = new Rect(chipX + (chip % 3) * (chipW + chipGap), 866 + (chip / 3) * 40, chipW, 34);
                chip++;
                Color tint = mode == GameMode.Teams ? (seats[s].team == 0 ? TeamAColor : TeamBColor) : Gold;
                Frame(cr, new Color(0.05f, 0.06f, 0.09f, 0.95f), tint, 2f);
                string who = s == mine ? "YOU" : s == 0 ? "HOST" : "P" + (s + 1);
                string firstName = CharacterNames[seats[s].character].Split(' ')[0];
                GUI.Label(cr, $"{who} · {firstName}{(seats[s].ready ? " [OK]" : "")}",
                    new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = seats[s].ready ? Color.white : tint } });
            }

            bool canReady = players >= 2;
            GUI.enabled = canReady && !modal;
            string readyText = myReady ? "READY!  (cancel)" : "READY";
            if (GUI.Button(new Rect(w * 0.5f - 220, 954, 440, 80), readyText, buttonBig)) SetMyReady(!myReady);
            GUI.enabled = !modal;

            if (GUI.Button(new Rect(30, 970, 200, 60), "BACK", button)) LeaveToMenu("");

            // Team picker (Teams mode only).
            if (mode == GameMode.Teams && mine >= 0)
            {
                for (int t = 0; t < 2; t++)
                {
                    var tr = new Rect(250 + t * 180, 970, 170, 60);
                    Color tc = t == 0 ? TeamAColor : TeamBColor;
                    if (seats[mine].team == t) Rect(new Rect(tr.x - 4, tr.y - 4, tr.width + 8, tr.height + 8), tc);
                    var ts = new GUIStyle(button) { fontSize = 24, normal = { textColor = tc, background = button.normal.background } };
                    if (GUI.Button(tr, t == 0 ? "TEAM A" : "TEAM B", ts)) SetMyTeam(t);
                }
            }

            // Game mode and match length: the host picks, guests see them.
            var modeLabel = new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, fontSize = 22 };
            if (IAmLeader)
            {
                GUI.Label(new Rect(w - 700, 914, 260, 44), "GAME MODE", modeLabel);
                DrawModeButton(new Rect(w - 420, 914, 230, 44), GameMode.FreeForAll, "FREE FOR ALL");
                DrawModeButton(new Rect(w - 180, 914, 150, 44), GameMode.Teams, "TEAMS");
            }
            else
            {
                GUI.Label(new Rect(w - 700, 914, 660, 44), mode == GameMode.Teams ? "TEAMS  (set by host)" : "FREE FOR ALL  (set by host)", modeLabel);
            }

            if (IAmLeader)
            {
                GUI.Label(new Rect(w - 700, 962, 260, 56), "MATCH LENGTH", new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, fontSize = 22 });
                DrawBestOfButton(new Rect(w - 420, 962, 180, 56), 3);
                DrawBestOfButton(new Rect(w - 220, 962, 180, 56), 5);
            }
            else
            {
                GUI.Label(new Rect(w - 700, 962, 660, 56), $"BEST OF {bestOf}  (set by host)", new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, fontSize = 22 });
            }

            string status = players < 2 ? "Waiting for more players to join..."
                : CanStart() ? "Everyone ready - starting..."
                : mode == GameMode.Teams && !TwoTeamsFilled() ? "Teams mode needs players on both Team A and Team B"
                : $"{players} players - waiting for everyone to ready up";
            GUI.Label(new Rect(0, 1040, w, 36), status, labelCentered);

            GUI.enabled = true;
            if (modal) DrawUpgradeOverlay(w, h, info);

            // Ability tooltip drawn last so it sits on top of everything.
            if (hoveredAbility != null)
            {
                const float tipW = 460f;
                var nameStyle = new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = hoveredAbility.ex ? new Color(0.8f, 0.6f, 1f) : accent } };
                var descStyle = new GUIStyle(labelSmall) { fontSize = 19 };
                var statStyle = new GUIStyle(labelSmall) { fontSize = 17, normal = { textColor = Gold } };
                float descH = descStyle.CalcHeight(new GUIContent(hoveredAbility.description), tipW - 28);
                float statH = statStyle.CalcHeight(new GUIContent(hoveredAbility.stats), tipW - 28);
                string tag = hoveredAbility.passive ? "PASSIVE" : hoveredAbility.ex ? "EX ABILITY" : hoveredAbility.ultimate ? "ULTIMATE" : "ABILITY";
                float tipH = 44 + 26 + descH + 14 + statH + 16;
                var tip = new Rect(hoveredRect.xMax + 16, Mathf.Min(hoveredRect.y - 10, h - tipH - 10), tipW, tipH);
                if (tip.xMax > w - 10) tip.x = hoveredRect.x - tipW - 16;
                Frame(tip, new Color(0.04f, 0.05f, 0.08f, 0.98f), hoveredAbility.ex ? new Color(0.8f, 0.6f, 1f) : accent, 2f);
                GUI.Label(new Rect(tip.x + 14, tip.y + 8, tip.width - 28, 32), hoveredAbility.passive ? hoveredAbility.name : $"{hoveredAbility.name}  [{hoveredAbility.key}]", nameStyle);
                GUI.Label(new Rect(tip.x + 14, tip.y + 38, tip.width - 28, 24), tag,
                    new GUIStyle(labelSmall) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = new Color(0.6f, 0.58f, 0.55f) } });
                GUI.Label(new Rect(tip.x + 14, tip.y + 66, tip.width - 28, descH), hoveredAbility.description, descStyle);
                Rect(new Rect(tip.x + 14, tip.y + 66 + descH + 4, tip.width - 28, 1), DarkGold);
                GUI.Label(new Rect(tip.x + 14, tip.y + 66 + descH + 10, tip.width - 28, statH), hoveredAbility.stats, statStyle);
            }
        }

        // Upgrade-catalog key for a roster entry, taken from its prefab ("Net/Player_GreyKnight" -> "GreyKnight").
        static string UpgradeKey(CharacterSelectView.CharacterInfo info)
        {
            int i = info.prefabPath.LastIndexOf("Player_", System.StringComparison.Ordinal);
            return i < 0 ? null : info.prefabPath.Substring(i + "Player_".Length);
        }

        // Modal list of every upgrade the champion can be offered, grouped by category.
        void DrawUpgradeOverlay(float w, float h, CharacterSelectView.CharacterInfo info)
        {
            var all = UpgradeCatalog.For(UpgradeKey(info));
            Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.72f));

            var panel = new Rect(w * 0.5f - 560, 110, 1120, h - 220);
            Frame(panel, new Color(0.04f, 0.05f, 0.08f, 0.99f), DarkGold, 3f);
            Rect(new Rect(panel.x + 3, panel.y + 3, panel.width - 6, 8), new Color(info.accent.r, info.accent.g, info.accent.b, 0.85f));
            GUI.Label(new Rect(panel.x + 24, panel.y + 20, panel.width - 200, 50), info.name + " - Upgrades",
                new GUIStyle(headline) { fontSize = 34, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } });
            if (GUI.Button(new Rect(panel.xMax - 170, panel.y + 22, 146, 46), "CLOSE", new GUIStyle(button) { fontSize = 22 }))
                showUpgrades = false;
            Rect(new Rect(panel.x + 24, panel.y + 76, panel.width - 48, 2), DarkGold);

            var nameStyle = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = Color.white } };
            var descStyle = new GUIStyle(labelSmall) { fontSize = 18 };
            var headStyle = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = Gold } };
            string[] catNames = { "ARMAMENT - weapon transformations", "ENDURANCE - defensive", "FEROCITY - ability enhancements", "REMEDY - support" };

            const float rowGap = 10f;
            float viewW = panel.width - 72f; // room for the scrollbar
            float textW = viewW - 28f;
            float total = 0f;
            for (int c = 0; c < catNames.Length; c++)
            {
                bool any = false;
                foreach (var u in all) if ((int)u.Category == c) { any = true; total += 30f + descStyle.CalcHeight(new GUIContent(u.Description), textW) + rowGap + 6f; }
                if (any) total += 44f;
            }

            var view = new Rect(panel.x + 24, panel.y + 90, panel.width - 48, panel.height - 110);
            upgradeScroll = GUI.BeginScrollView(view, upgradeScroll, new Rect(0, 0, viewW, total));
            float y = 0f;
            for (int c = 0; c < catNames.Length; c++)
            {
                bool first = true;
                foreach (var u in all)
                {
                    if ((int)u.Category != c) continue;
                    if (first)
                    {
                        GUI.Label(new Rect(0, y, viewW, 36), catNames[c], headStyle);
                        y += 44f;
                        first = false;
                    }
                    float dh = descStyle.CalcHeight(new GUIContent(u.Description), textW);
                    Rect(new Rect(0, y, viewW, 30f + dh + 6f), new Color(1f, 1f, 1f, 0.04f));
                    GUI.Label(new Rect(14, y + 2, textW, 28), u.Name, nameStyle);
                    GUI.Label(new Rect(14, y + 30, textW, dh), u.Description, descStyle);
                    y += 30f + dh + rowGap + 6f;
                }
            }
            GUI.EndScrollView();
        }

        static string Abbreviation(string name)
        {
            var parts = name.Split(' ', '-');
            string s = "";
            foreach (string p in parts)
                if (p.Length > 0 && s.Length < 2 && p != "for" && p != "the") s += char.ToUpper(p[0]);
            return s.Length > 0 ? s : name.Substring(0, 1);
        }

        static readonly Color TeamAColor = new Color(0.45f, 0.75f, 1f);
        static readonly Color TeamBColor = new Color(1f, 0.65f, 0.3f);

        bool TwoTeamsFilled()
        {
            bool a = false, b = false;
            foreach (Seat s in seats)
            {
                if (!s.used) continue;
                if (s.team == 0) a = true; else b = true;
            }
            return a && b;
        }

        void DrawModeButton(Rect r, GameMode value, string text)
        {
            bool selected = mode == value;
            if (selected) Rect(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Gold);
            var style = selected ? new GUIStyle(button) { fontSize = 22, normal = { textColor = Color.white, background = button.active.background } } : new GUIStyle(button) { fontSize = 22 };
            if (GUI.Button(r, text, style)) SetMode(value);
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
                    if (player == null || player.Health.IsDead || Stealthed.HiddenHere(player.gameObject)) continue;
                    Vector3 screen = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 3.15f);
                    if (screen.z <= 0f) continue;

                    bool mine = player.IsOwner;
                    bool ally = !mine && NetPlayer.Local != null && player.Health.Team == NetPlayer.Local.Health.Team;
                    var style = new GUIStyle(labelCentered) { fontSize = 22, fontStyle = FontStyle.Bold };
                    style.normal.textColor = mine ? new Color(0.4f, 1f, 0.5f) : ally ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.4f, 0.35f);
                    float x = screen.x / uiScale;
                    float y = (Screen.height - screen.y) / uiScale;
                    GUI.Label(new Rect(x - 80, y - 16, 160, 32), mine ? "YOU" : ally ? "ALLY" : "ENEMY", style);
                }
            }

            // Score: rounds won by each team (yours first, in green), with the round number and match length alongside.
            int myTeam = NetPlayer.Local != null ? NetPlayer.Local.Health.Team : -1;
            var teamScores = new SortedDictionary<int, int>();
            foreach (NetPlayer player in NetPlayer.All)
                if (player != null) teamScores[player.Health.Team] = player.Kills;
            var order = new List<int>();
            if (teamScores.ContainsKey(myTeam)) order.Add(myTeam);
            foreach (int t in teamScores.Keys) if (t != myTeam) order.Add(t);

            int totalRounds = 0;
            foreach (int score in teamScores.Values) totalRounds += score;

            int roundNumber = totalRounds + (phase == Phase.Playing || phase == Phase.Prep ? 1 : 0);
            Color green = new Color(0.4f, 1f, 0.5f), red = new Color(1f, 0.4f, 0.35f);
            string scoreLine = string.Join("  -  ", order.ConvertAll(t => teamScores[t].ToString()));

            // Top-centre strip: the round timer in the middle (counts up from the round start, frozen once the round
            // ends), with a marble per round needed to win either side: yours on the left, everyone else on the right.
            if (phase == Phase.Playing) roundElapsed = Mathf.Max(0f, Time.time - (introUntil - RoundIntro));
            else if (phase == Phase.Prep) roundElapsed = 0f;
            if (phase == Phase.Playing || phase == Phase.RoundEnd || phase == Phase.Prep)
            {
                int secs = Mathf.FloorToInt(roundElapsed);
                var timerBox = new Rect(w * 0.5f - 60, 14, 120, 44);
                Frame(timerBox, Panel, DarkGold);
                GUI.Label(timerBox, $"{secs / 60}:{secs % 60:00}", new GUIStyle(labelCentered) { fontSize = 26, fontStyle = FontStyle.Bold });

                var otherScores = new List<int>();
                bool inMatch = order.Count > 0 && order[0] == myTeam; // spectators have no team of their own
                for (int i = inMatch ? 1 : 0; i < order.Count; i++) otherScores.Add(teamScores[order[i]]);
                RoundMarbles.Draw(w * 0.5f, 14, 1f, 74f, 44f, RoundsToWin, inMatch ? teamScores[myTeam] : 0, otherScores);
            }

            if (phase == Phase.Playing || phase == Phase.RoundEnd || phase == Phase.Prep)
                if (GUI.Button(new Rect(w - 190, 18, 160, 52), "LEAVE", button)) LeaveToMenu("");

            bool won = myTeam >= 0 && myTeam == winnerTeam;

            if (spectateTarget != null && phase == Phase.Playing)
            {
                string targetName = spectateTarget.name.Replace("(Clone)", "").Replace("Player_", "").Trim();
                var spec = new GUIStyle(labelCentered) { fontSize = 26, fontStyle = FontStyle.Bold };
                spec.normal.textColor = Gold;
                GUI.Label(new Rect(0, h - 110, w, 40), $"SPECTATING  ·  {targetName}", spec);
                GUI.Label(new Rect(0, h - 72, w, 30), "Click to switch", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
            }

            if (phase == Phase.Playing)
            {
                // "ROUND N" banner as a round begins.
                if (Time.time < introUntil)
                {
                    Rect(new Rect(0, h * 0.36f, w, 170), new Color(0f, 0f, 0f, 0.55f));
                    GUI.Label(new Rect(0, h * 0.36f + 8, w, 110), $"ROUND {roundNumber}", new GUIStyle(title) { fontSize = 90 });
                    GUI.Label(new Rect(0, h * 0.36f + 108, w, 50), $"{scoreLine}      ·      BEST OF {bestOf}", subtitle);
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
                GUI.Label(new Rect(0, top + 110, w, 60), $"{scoreLine}      ·      BEST OF {bestOf}", new GUIStyle(headline) { fontSize = 40 });
                GUI.Label(new Rect(0, top + 162, w, 34), $"First to {RoundsToWin} rounds wins the match", subtitle);

                DrawDamageReport(w, top + 220);

                int seconds = Mathf.Max(1, Mathf.CeilToInt(countdownEnd - Time.time));
                GUI.Label(new Rect(0, h - 190, w, 40), "PREPARATION IN", subtitle);
                GUI.Label(new Rect(0, h - 150, w, 120), seconds.ToString(), new GUIStyle(title) { fontSize = 110 });
                return;
            }

            if (phase == Phase.Prep)
            {
                DrawPrep(w, h, roundNumber, scoreLine);
                return;
            }

            // Match over.
            Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.65f));
            var result = new GUIStyle(title) { fontSize = 130 };
            result.normal.textColor = won ? green : red;
            GUI.Label(new Rect(0, 230, w, 170), won ? "MATCH WON" : "MATCH LOST", result);
            GUI.Label(new Rect(0, 410, w, 70), scoreLine, new GUIStyle(headline) { fontSize = 60 });
            GUI.Label(new Rect(0, 480, w, 40), $"BEST OF {bestOf}", subtitle);

            DrawDamageReport(w, 540);

            float bx = w * 0.5f - 230;
            if (IAmLeader)
            {
                if (GUI.Button(new Rect(bx, 740, 460, 84), "REMATCH", buttonBig)) ChooseRematch();
                if (GUI.Button(new Rect(bx, 835, 460, 70), "CHARACTER SELECT", button)) ChooseLobby();
            }
            else
            {
                GUI.Label(new Rect(0, 780, w, 50), "Waiting for the host to choose rematch or character select...", labelCentered);
            }
            if (GUI.Button(new Rect(bx, 920, 460, 70), "MAIN MENU", button)) LeaveToMenu("");
        }

        // The prep area screen: the upgrade menu, then "waiting", then the round-start countdown.
        void DrawPrep(float w, float h, int roundNumber, string scoreLine)
        {
            UpgradeState mine = NetPlayer.Local != null ? NetPlayer.Local.Upgrades : null;

            if (prepLocked)
            {
                int go = Mathf.Max(1, Mathf.CeilToInt(prepGoTime - Time.time));
                Rect(new Rect(0, h * 0.30f, w, 330), new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(new Rect(0, h * 0.30f + 6, w, 50), $"ROUND {roundNumber} BEGINS IN", subtitle);
                GUI.Label(new Rect(0, h * 0.30f + 50, w, 200), go.ToString(), new GUIStyle(title) { fontSize = 170 });
                if (mine != null && mine.Mask != 0)
                    GUI.Label(new Rect(0, h * 0.30f + 262, w, 50), UpgradePickerView.OwnedSummary(mine), new GUIStyle(labelCentered) { fontSize = 22 });
                return;
            }

            if (mine != null && mine.Pending)
            {
                upgradePicker.Draw(w, h, mine, roundNumber, countdownEnd - Time.time);
                return;
            }

            // Chosen (or nothing to choose): wait for everyone else.
            Rect(new Rect(0, h * 0.14f, w, 190), new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(0, h * 0.14f + 8, w, 90), $"PREPARE FOR ROUND {roundNumber}", new GUIStyle(headline) { fontSize = 64 });
            GUI.Label(new Rect(0, h * 0.14f + 100, w, 40), $"{scoreLine}      ·      BEST OF {bestOf}", subtitle);
            GUI.Label(new Rect(0, h * 0.14f + 140, w, 40),
                AllUpgradesSettled() ? "Ready" : "Waiting for the other players to choose...", subtitle);
            if (mine != null && mine.Mask != 0)
                GUI.Label(new Rect(w * 0.5f - 700f, h - 250, 1400f, 50f), "UPGRADES:  " + UpgradePickerView.OwnedSummary(mine),
                    new GUIStyle(labelCentered) { fontSize = 22 });
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
