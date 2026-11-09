using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Anathema.Arena;
using Anathema.CameraRig;
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

        static readonly string[] CharacterNames = { "Grey Knight Interceptor", "Khorne Berserker", "Plague Surgeon", "Psyker", "Skitarii Ranger" };
        static readonly string[] CharacterPrefabs = { "Net/Player_GreyKnight", "Net/Player_KhorneBerserker", "Net/Player_PlagueSurgeon", "Net/Player_Psyker", "Net/Player_SkitariiRanger" };

        public static NetGame Instance { get; private set; }
        public static bool Online => Instance != null && Instance.manager != null && Instance.manager.IsListening;
        public static bool Frozen => Instance != null && Instance.frozen;
        public static bool IsHost => Online && Instance.manager.IsServer;

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
        readonly CharacterSelectView select = new CharacterSelectView();
        readonly bool[] roleFilter = new bool[3];
        bool draggingModel;
        float modelDrag;

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
                select.Hide();
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

            for (int i = 0; i < seats.Length; i++) seats[i] = default;
            seats[0] = new Seat { used = true, clientId = manager.LocalClientId, character = myCharacter, ready = false, team = 0 };
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
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Approved = FreeSeat() >= 0 && phase == Phase.Lobby;
            if (!response.Approved) response.Reason = phase == Phase.Lobby ? "The game is full." : "A match is already in progress.";
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
                int free = FreeSeat();
                if (free < 0)
                {
                    manager.DisconnectClient(clientId, "The game is full.");
                    return;
                }
                seats[free] = new Seat { used = true, clientId = clientId, character = 0, ready = false, team = SmallerTeam() };
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
                for (int i = 0; i < seats.Length; i++)
                    if (seats[i].used && seats[i].clientId == clientId) seats[i] = default;

                if (phase == Phase.Lobby) BroadcastLobby();
                else if (phase == Phase.Loading) LeaveToMenu("A player left the match.");
                else ServerEvaluateRound(clientId); // the match carries on while at least two teams remain
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

        void SetMode(GameMode value)
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
            if (UsedSeats() < 2) return;

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

            if (phase == Phase.Lobby) select.Show(); else select.Hide();

            if (!manager.IsServer && phase == Phase.Lobby && SceneManager.GetActiveScene().name == ArenaScene)
            {
                phase = Phase.Playing;
                introUntil = Time.time + RoundIntro;
            }

            if (manager.IsServer && phase == Phase.RoundEnd && Time.time >= nextRoundTime)
                StartNextRound();

            UpdateSpectate();

            if (manager.IsServer && phase == Phase.Lobby && CanStart())
            {
                phase = Phase.Loading;
                manager.SceneManager.LoadScene(ArenaScene, LoadSceneMode.Single);
            }
        }

        // --- Spectating: once you're down, the camera follows a living fighter (teammates first); click to switch ---

        NetPlayer spectateTarget;
        TopDownCamera spectateCamera;

        public bool Spectating => spectateTarget != null;

        void UpdateSpectate()
        {
            NetPlayer me = NetPlayer.Local;
            bool inMatch = phase == Phase.Playing || phase == Phase.RoundEnd || phase == Phase.Over;
            bool down = inMatch && me != null && me.Health.IsDead;

            if (spectateCamera == null) spectateCamera = FindFirstObjectByType<TopDownCamera>();
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
            if (!manager.IsServer || sceneName != ArenaScene) return;

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
            frozen = false;
            phase = Phase.Playing;
            introUntil = Time.time + RoundIntro;
        }

        const float SpawnRadius = 20f;

        // Free-for-all: everyone evenly around the pit. Teams: team A on the left, team B on the right, lined up.
        Vector3 SpawnPosition(int seat, out Quaternion rotation)
        {
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

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(winnerTeam);

            if (wins >= RoundsToWin)
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
            reader.ReadValueSafe(out winnerTeam);
            phase = Phase.RoundEnd;
            countdownEnd = Time.time + RoundPause;
            frozen = true;
        }

        void OnOverMessage(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer) return;
            reader.ReadValueSafe(out winnerTeam);
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
            winnerTeam = -1;
            lastDeadTeam = -1;
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
            GUI.Label(new Rect(0, 250, w, 40), "AN  ARENA  BRAWLER  FOR  2 - 6  PLAYERS", subtitle);
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
            Vector2 mouse = Event.current.mousePosition;
            bool repaint = Event.current.type == EventType.Repaint;

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
            if (e.type == EventType.MouseDown && dragZone.Contains(e.mousePosition)) { draggingModel = true; e.Use(); }
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
            GUI.enabled = canReady;
            string readyText = myReady ? "READY!  (cancel)" : "READY";
            if (GUI.Button(new Rect(w * 0.5f - 220, 954, 440, 80), readyText, buttonBig)) SetMyReady(!myReady);
            GUI.enabled = true;

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
            if (manager.IsServer)
            {
                GUI.Label(new Rect(w - 700, 914, 260, 44), "GAME MODE", modeLabel);
                DrawModeButton(new Rect(w - 420, 914, 230, 44), GameMode.FreeForAll, "FREE FOR ALL");
                DrawModeButton(new Rect(w - 180, 914, 150, 44), GameMode.Teams, "TEAMS");
            }
            else
            {
                GUI.Label(new Rect(w - 700, 914, 660, 44), mode == GameMode.Teams ? "TEAMS  (set by host)" : "FREE FOR ALL  (set by host)", modeLabel);
            }

            if (manager.IsServer)
            {
                GUI.Label(new Rect(w - 700, 962, 260, 56), "MATCH LENGTH", new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, fontSize = 22 });
                DrawBestOfButton(new Rect(w - 420, 962, 180, 56), 3);
                DrawBestOfButton(new Rect(w - 220, 962, 180, 56), 5);
                GUI.Label(new Rect(w - 700, 1030, 660, 30), $"Friend joins with: {localIps} (UDP {DefaultPort})", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleRight, fontSize = 16, wordWrap = false });
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

            // Ability tooltip drawn last so it sits on top of everything.
            if (hoveredAbility != null)
            {
                const float tipW = 460f;
                var nameStyle = new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = hoveredAbility.ex ? new Color(0.8f, 0.6f, 1f) : accent } };
                var descStyle = new GUIStyle(labelSmall) { fontSize = 19 };
                var statStyle = new GUIStyle(labelSmall) { fontSize = 17, normal = { textColor = Gold } };
                float descH = descStyle.CalcHeight(new GUIContent(hoveredAbility.description), tipW - 28);
                float statH = statStyle.CalcHeight(new GUIContent(hoveredAbility.stats), tipW - 28);
                string tag = hoveredAbility.ex ? "EX ABILITY" : hoveredAbility.ultimate ? "ULTIMATE" : "ABILITY";
                float tipH = 44 + 26 + descH + 14 + statH + 16;
                var tip = new Rect(hoveredRect.xMax + 16, Mathf.Min(hoveredRect.y - 10, h - tipH - 10), tipW, tipH);
                if (tip.xMax > w - 10) tip.x = hoveredRect.x - tipW - 16;
                Frame(tip, new Color(0.04f, 0.05f, 0.08f, 0.98f), hoveredAbility.ex ? new Color(0.8f, 0.6f, 1f) : accent, 2f);
                GUI.Label(new Rect(tip.x + 14, tip.y + 8, tip.width - 28, 32), $"{hoveredAbility.name}  [{hoveredAbility.key}]", nameStyle);
                GUI.Label(new Rect(tip.x + 14, tip.y + 38, tip.width - 28, 24), tag,
                    new GUIStyle(labelSmall) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = new Color(0.6f, 0.58f, 0.55f) } });
                GUI.Label(new Rect(tip.x + 14, tip.y + 66, tip.width - 28, descH), hoveredAbility.description, descStyle);
                Rect(new Rect(tip.x + 14, tip.y + 66 + descH + 4, tip.width - 28, 1), DarkGold);
                GUI.Label(new Rect(tip.x + 14, tip.y + 66 + descH + 10, tip.width - 28, statH), hoveredAbility.stats, statStyle);
            }
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
                    if (player == null || player.Health.IsDead) continue;
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

            int roundNumber = totalRounds + (phase == Phase.Playing ? 1 : 0);
            Color green = new Color(0.4f, 1f, 0.5f), red = new Color(1f, 0.4f, 0.35f);
            string scoreLine = string.Join("  -  ", order.ConvertAll(t => teamScores[t].ToString()));

            // Your score on the left, the round label in the middle, every other team spread across the right.
            int others = Mathf.Max(1, order.Count - 1);
            float side = Mathf.Max(150f, 90f * others);
            float scoreW = 160f + side * 2f;
            var scoreBox = new Rect(w * 0.5f - scoreW * 0.5f, 18, scoreW, 78);
            Frame(scoreBox, Panel, DarkGold);
            var big = new GUIStyle(headline) { fontSize = 50 };
            for (int i = 0; i < order.Count; i++)
            {
                big.normal.textColor = order[i] == myTeam ? green : red;
                Rect cellRect = i == 0
                    ? new Rect(scoreBox.x, scoreBox.y + 4, side, 70)
                    : new Rect(scoreBox.xMax - side + (i - 1) * (side / others), scoreBox.y + 4, side / others, 70);
                GUI.Label(cellRect, teamScores[order[i]].ToString(), big);
            }
            GUI.Label(new Rect(scoreBox.center.x - 80, scoreBox.y + 6, 160, 30), $"ROUND {roundNumber}", new GUIStyle(labelCentered) { fontSize = 22, fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(scoreBox.center.x - 80, scoreBox.y + 36, 160, 30), $"BEST OF {bestOf}", new GUIStyle(labelCentered) { fontSize = 20 });

            if (phase == Phase.Playing || phase == Phase.RoundEnd)
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
                GUI.Label(new Rect(0, h - 190, w, 40), "NEXT ROUND IN", subtitle);
                GUI.Label(new Rect(0, h - 150, w, 120), seconds.ToString(), new GUIStyle(title) { fontSize = 110 });
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
