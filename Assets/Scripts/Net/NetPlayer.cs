using System.Collections.Generic;
using Anathema.Abilities;
using Anathema.Abilities.PlagueSurgeon;
using Anathema.CameraRig;
using Anathema.Combat;
using Anathema.Player;
using Anathema.Upgrades;
using Unity.Netcode;
using UnityEngine;

namespace Anathema.Net
{
    // Network side of a player character. Two kinds of authority:
    //   - The owning machine runs input, movement, aim and abilities, enforces control effects (stun, root...) and
    //     publishes position/aim. Every other machine shows a "replica" that follows it and replays its ability visuals.
    //   - The server owns combat state: it resolves every hit and heal against its own copy (counters, shields, armour,
    //     last stand), holds health, wards, bleeds and burns, decides deaths and credit, and publishes the results.
    //     Clients only send what they did (SendHit, SendHeal, SendStatus) and draw what the server says.
    [RequireComponent(typeof(Health))]
    public class NetPlayer : NetworkBehaviour
    {
        public enum StatusKind
        {
            Stun,
            Silence,
            Maim,
            Knockback,
            Root,
            Bleed,
            Heal,
            Panic,
            Ward,
            Slow,
            Burn,
            PanicBreak, // server -> owner: a solid hit snapped them out of panic
            Enrage,     // server -> owner: e.g. Defiant Roar, decided where the damage is resolved
            Barrier,    // a short damage-soaking barrier (a = amount, b = seconds, direction.x = cap), held by the server
            Cleanse,    // wash off bleeds and burns, dull other damage over time (a = reduction, b = seconds), on the server
            Poison,     // stacking toxin (a = damage per second per stack, b = seconds, direction.x = stacks), held by the server
            Balefire,   // the Mandrake's burst-on-hit burn (a = damage per second, b = seconds), held by the server
            Armor,      // short extra armour (a = fraction, b = seconds), e.g. Hive Shell, held by the server
            Leech,      // Leech Draught's mark on a festering enemy (a = seconds), held by the server
            Irradiated, // Rad-Saturation's mark: takes more damage (a = extra fraction, b = seconds); a hit rider, held where damage resolves
            FaithArmor, // the Hospitaller's Faith reduction on herself (a = fraction, b = seconds), replaced on every refresh, held by the server
            HealCut     // less healing received (a = share cut, b = seconds), e.g. Holy Pyre; a hit rider, held where damage resolves
        }

        public static readonly List<NetPlayer> All = new List<NetPlayer>();
        public static NetPlayer Local { get; private set; }

        const float SnapDistance = 5f;
        const float PositionSharpness = 22f;
        const float AimSharpness = 30f;

        // Health is the server's: it resolves every hit and heal against its own copy and publishes the result here.
        readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> netRecoverable = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> netDamageTaken = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> netHealed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Credit for the report: written by the server as it resolves damage and healing.
        readonly NetworkVariable<float> netDamageDealt = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> netHealingDone = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Position is the server's: it moves every character from its owner's input (see PlayerMovement).
        readonly NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<Vector3> netAim = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<int> slot = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> kills = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Team id (1-based, matches Health.Team). Free-for-all gives every seat its own team.
        readonly NetworkVariable<int> team = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Between-round upgrades: the owned-upgrade bitmask (see UpgradeState) and the last round-end whose pick this
        // fighter has settled. The server compares the latter with the rounds played to know when everyone has chosen.
        readonly NetworkVariable<int> netUpgradeMask = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<int> netUpgradeRound = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Attack speed bonus from the Rage passive (owner machine). Synced so replicas replay swings at the same speed.
        readonly NetworkVariable<float> netRageAttackBonus = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Rage display state (see Rage.NetState), so every client can draw the dots and rune.
        readonly NetworkVariable<int> netRageState = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Energy (whole points) so teammates' frames can show it.
        readonly NetworkVariable<float> netEnergy = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Control effects (root, panic, stun...) are enforced on the machine that moves the character, so the owner
        // publishes which ones are up and replicas draw visual-only copies (see SyncStatusFx).
        const int FxRoot = 2, FxPanic = 4, FxCharged = 8, FxEnraged = 16, FxStun = 32, FxEmpowered = 64, FxStealth = 128, FxFaded = 256, FxHasted = 512;
        readonly NetworkVariable<int> netStatusFx = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        int appliedStatusFx;

        // Effects that do their work on the server's copy (the ward's absorbing, bleed and burn damage) are published
        // by the server; every other machine, the owner included, draws visual-only copies (see SyncServerFx).
        const int ServerFxWard = 1, ServerFxBleed = 2, ServerFxBurn = 4, ServerFxBarrier = 8, ServerFxPoison = 16, ServerFxBalefire = 32;
        readonly NetworkVariable<int> netServerFx = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        int appliedServerFx;

        int ReadServerFx()
        {
            if (health.IsDead) return 0;
            int m = 0;
            if (PlagueWard.IsWarded(gameObject)) m |= ServerFxWard;
            if (Bleeding.IsBleedingForReal(gameObject)) m |= ServerFxBleed;
            if (Burning.IsBurningForReal(gameObject)) m |= ServerFxBurn;
            if (Barrier.IsForReal(gameObject)) m |= ServerFxBarrier;
            if (Poisoned.IsPoisonedForReal(gameObject)) m |= ServerFxPoison;
            if (Balefire.IsBurningForReal(gameObject)) m |= ServerFxBalefire;
            return m;
        }

        void SyncServerFx()
        {
            int mask = netServerFx.Value;
            if (mask == appliedServerFx) return;
            int changed = mask ^ appliedServerFx;
            appliedServerFx = mask;

            ToggleFx<PlagueWard>(changed, mask, ServerFxWard, PlagueWard.ShowReplica);
            ToggleFx<Bleeding>(changed, mask, ServerFxBleed, Bleeding.ShowReplica);
            ToggleFx<Burning>(changed, mask, ServerFxBurn, Burning.ShowReplica);
            ToggleFx<Barrier>(changed, mask, ServerFxBarrier, Barrier.ShowReplica);
            ToggleFx<Poisoned>(changed, mask, ServerFxPoison, Poisoned.ShowReplica);
            ToggleFx<Balefire>(changed, mask, ServerFxBalefire, Balefire.ShowReplica);
        }

        int ReadStatusFx()
        {
            int m = 0;
            if (Rooted.IsRooted(gameObject)) m |= FxRoot;
            if (Panicked.IsPanicked(gameObject)) m |= FxPanic;
            if (Charged.IsCharged(gameObject)) m |= FxCharged;
            if (Enraged.IsEnraged(gameObject)) m |= FxEnraged;
            if (Stunned.IsStunned(gameObject)) m |= FxStun;
            if (Stealthed.IsStealthed(gameObject)) m |= FxStealth;
            if (Faded.IsFaded(gameObject)) m |= FxFaded;
            if (Hasted.IsHasted(gameObject)) m |= FxHasted;
            foreach (Ability a in abilities)
            {
                if (!a.ShowsEmpowered) continue;
                m |= FxEmpowered;
                break;
            }
            return health.IsDead ? 0 : m;
        }

        // Replica: bring the drawn effects in line with what the owner published (only on changes, so an effect that
        // ends locally, such as on death, isn't redrawn).
        void SyncStatusFx()
        {
            int mask = netStatusFx.Value;
            // The server holds the real stun, root and panic on its copy (it moves the character); the owner's report
            // of them must not add or remove those.
            if (IsServer) mask &= ~(FxRoot | FxPanic | FxStun);
            if (mask == appliedStatusFx) return;
            int changed = mask ^ appliedStatusFx;
            appliedStatusFx = mask;

            ToggleFx<Rooted>(changed, mask, FxRoot, Rooted.ShowReplica);
            ToggleFx<Panicked>(changed, mask, FxPanic, Panicked.ShowReplica);
            ToggleFx<Charged>(changed, mask, FxCharged, Charged.ShowReplica);
            ToggleFx<Enraged>(changed, mask, FxEnraged, Enraged.ShowReplica);
            ToggleFx<Stunned>(changed, mask, FxStun, Stunned.ShowReplica);
            ToggleFx<Stealthed>(changed, mask, FxStealth, Stealthed.ShowReplica);
            ToggleFx<Faded>(changed, mask, FxFaded, Faded.ShowReplica);
            ToggleFx<Hasted>(changed, mask, FxHasted, Hasted.ShowReplica);
            if ((changed & FxStun) != 0 && (mask & FxStun) == 0 && TryGetComponent(out StunFx stunFx)) Destroy(stunFx);
            if ((changed & FxEmpowered) != 0)
                foreach (Ability a in abilities) a.ShowReplicaEmpowered((mask & FxEmpowered) != 0);
        }

        void ToggleFx<T>(int changed, int mask, int bit, System.Action<GameObject> show) where T : Component
        {
            if ((changed & bit) == 0) return;
            if ((mask & bit) != 0) show(gameObject);
            else if (TryGetComponent(out T existing)) Destroy(existing);
        }

        Health health;
        UpgradeState upgrades;
        Respawner respawner;
        MouseAim aim;
        Energy energy;
        CharacterController controller;
        CapsuleCollider proxyCollider;
        Ability[] abilities;
        Vector3 targetPosition;
        Vector3 smoothedAim;
        bool hasRemoteState;

        public int Slot => slot.Value;
        public int Kills => kills.Value;
        public Vector3 ReplicaAimPoint => smoothedAim;
        public Health Health => health;
        // This round's damage report numbers; the server's copy is the truth, everyone else reads what it published.
        public float RoundDamageTaken => IsServer ? health.RoundDamageTaken : netDamageTaken.Value;
        public float RoundHealed => IsServer ? health.RoundHealed : netHealed.Value;
        public float RoundDamageDealt => netDamageDealt.Value;
        public float RoundHealingDone => netHealingDone.Value;
        public int UpgradeMask => netUpgradeMask.Value;
        // Highest round-end this fighter has finished choosing an upgrade for (0 = none yet).
        public int UpgradeRound => netUpgradeRound.Value;
        public float RageAttackBonus => netRageAttackBonus.Value;
        public float EnergyMax => energy != null ? energy.Max : 0f;
        public float EnergyCurrent => energy == null ? 0f : IsOwner ? energy.Current : netEnergy.Value;
        // Rage display state published by the owner: stacks | max << 4 | expiring << 8 (0 = none).
        public int RageState => netRageState.Value;
        // This fighter's upgrade state (cached; null for characters without upgrades).
        public UpgradeState Upgrades => upgrades != null ? upgrades : (upgrades = GetComponent<UpgradeState>());

        // Server only: this fighter has died this round (cleared when the next round starts).
        public bool ServerDead { get; set; }

        public void ServerResetRoundStats()
        {
            ServerDead = false;
            netDamageDealt.Value = 0f;
            netHealingDone.Value = 0f;
        }

        // Server: credit the attacker (damage) or healer (healing) in the report.
        static void ServerCredit(GameObject credited, GameObject victim, float damage, float healing)
        {
            if (credited == null || credited == victim || !credited.TryGetComponent(out NetPlayer np) || !np.IsSpawned) return;
            np.netDamageDealt.Value += damage;
            np.netHealingDone.Value += healing;
        }

        void OnServerDamageTaken(float dealt, GameObject source) => ServerCredit(source, gameObject, dealt, 0f);
        void OnServerHealed(float healed, GameObject source) => ServerCredit(source, gameObject, 0f, healed);
        void OnServerDied(Health _, GameObject killer) => NetGame.Instance?.ServerOnDeath(this);

        // Server, between rounds: full health, no lingering damage over time or ward, ready for the next round.
        public void ServerResetForRound()
        {
            if (!IsServer) return;
            health.ResetHealth();
            foreach (Bleeding b in GetComponents<Bleeding>()) Destroy(b);
            foreach (Burning b in GetComponents<Burning>()) Destroy(b);
            foreach (Poisoned p in GetComponents<Poisoned>()) Destroy(p);
            foreach (Balefire b in GetComponents<Balefire>()) Destroy(b);
            foreach (PlagueWard w in GetComponents<PlagueWard>()) Destroy(w);
            foreach (Knockback k in GetComponents<Knockback>()) Destroy(k);
            // Back to the spawn point: the server owns position, so it places everyone and the owners snap there.
            if (movement != null && respawner != null) movement.ServerPlace(respawner.SpawnPosition);
            netPosition.Value = transform.position;
            netHealth.Value = health.Current;
            netRecoverable.Value = health.Recoverable;
            netServerFx.Value = 0;
        }

        // Server only: which lobby seat (0 = host) this character belongs to, and which team it fights for.
        public void ServerSetSlot(int seat, int teamId)
        {
            slot.Value = seat;
            team.Value = teamId;
        }
        public void ServerAddKill() => kills.Value += 1;
        public void ServerResetKills() => kills.Value = 0;

        public static bool IsReplicaObject(GameObject go)
        {
            return go != null && go.TryGetComponent(out NetPlayer np) && np.IsSpawned && !np.IsOwner;
        }

        public static bool TryGetRemote(GameObject target, out NetPlayer player)
        {
            player = null;
            return target != null && target.TryGetComponent(out player) && player.IsSpawned && !player.IsOwner;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            respawner = GetComponent<Respawner>();
            energy = GetComponent<Energy>();
            controller = GetComponent<CharacterController>();
            proxyCollider = GetComponent<CapsuleCollider>();
            movement = GetComponent<PlayerMovement>();
            abilities = GetComponents<Ability>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            health.SetTeam(team.Value);
            team.OnValueChanged += OnTeamChanged;
            smoothedAim = transform.position + transform.forward * 3f;

            if (IsServer)
            {
                // The server's copy is the truth for health: deaths, damage and healing credit are decided here.
                netHealth.Value = health.Current;
                netRecoverable.Value = health.Recoverable;
                health.Died += OnServerDied;
                health.DamageTaken += OnServerDamageTaken;
                health.Healed += OnServerHealed;
                netPosition.Value = transform.position;
                if (respawner != null) respawner.SetSpawn(transform.position, transform.rotation);
            }

            if (IsOwner)
            {
                Local = this;
                if (respawner != null) respawner.SetSpawn(transform.position, transform.rotation);
                netAim.Value = smoothedAim;

                TopDownCamera cam = FindAnyObjectByType<TopDownCamera>();
                if (cam != null) cam.SetTarget(transform);

                if (proxyCollider != null) proxyCollider.enabled = false;
            }
            else if (IsServer)
            {
                // The server's copy of another player is the real one: it is moved by the shared movement step and
                // collides with walls and other characters through its CharacterController, like the owner's.
                if (controller != null) controller.enabled = true;
                if (proxyCollider != null) proxyCollider.enabled = false;
                DestroyIfPresent<PlayerHud>();
                DestroyIfPresent<AimIndicator>();
                netAim.OnValueChanged += OnRemoteAim;
                if (netAim.Value != Vector3.zero) smoothedAim = netAim.Value;
            }
            else
            {
                // The CharacterController can't be moved by setting transform.position, so replicas use a plain capsule
                // for hits and for blocking the local player.
                if (controller != null) controller.enabled = false;
                if (proxyCollider != null) proxyCollider.enabled = true;

                // Another player's screen-space UI and aim line aren't ours to show.
                DestroyIfPresent<PlayerHud>();
                DestroyIfPresent<AimIndicator>();

                netPosition.OnValueChanged += OnRemotePosition;
                netAim.OnValueChanged += OnRemoteAim;
                if (netPosition.Value != Vector3.zero)
                {
                    hasRemoteState = true;
                    targetPosition = netPosition.Value;
                    transform.position = targetPosition;
                }
                if (netAim.Value != Vector3.zero) smoothedAim = netAim.Value;
            }
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            if (health != null)
            {
                health.Died -= OnServerDied;
                health.DamageTaken -= OnServerDamageTaken;
                health.Healed -= OnServerHealed;
            }
            netPosition.OnValueChanged -= OnRemotePosition;
            netAim.OnValueChanged -= OnRemoteAim;
            team.OnValueChanged -= OnTeamChanged;
        }

        void OnTeamChanged(int previous, int current)
        {
            health.SetTeam(current);
        }

        void DestroyIfPresent<T>() where T : Component
        {
            T c = GetComponent<T>();
            if (c != null) Destroy(c);
        }

        void OnRemotePosition(Vector3 previous, Vector3 current)
        {
            hasRemoteState = true;
            targetPosition = current;
        }

        void OnRemoteAim(Vector3 previous, Vector3 current)
        {
            netAimReceived = current;
        }

        Vector3 netAimReceived;

        void Update()
        {
            if (!IsSpawned) return;

            // Health and server-held effects: the server publishes, everyone else (the owner included) follows.
            if (IsServer)
            {
                if (!Mathf.Approximately(netHealth.Value, health.Current)) netHealth.Value = health.Current;
                if (!Mathf.Approximately(netRecoverable.Value, health.Recoverable)) netRecoverable.Value = health.Recoverable;
                if (!Mathf.Approximately(netDamageTaken.Value, health.RoundDamageTaken)) netDamageTaken.Value = health.RoundDamageTaken;
                if (!Mathf.Approximately(netHealed.Value, health.RoundHealed)) netHealed.Value = health.RoundHealed;
                int serverFx = ReadServerFx();
                if (netServerFx.Value != serverFx) netServerFx.Value = serverFx;
                if ((netPosition.Value - transform.position).sqrMagnitude > 0.0004f) netPosition.Value = transform.position;
            }
            else
            {
                health.SyncFromNetwork(netHealth.Value, netRecoverable.Value, netDamageTaken.Value, netHealed.Value);
                SyncServerFx();
            }

            if (IsOwner)
            {
                if (aim == null) aim = GetComponent<MouseAim>();
                if (aim != null && (netAim.Value - aim.AimPoint).sqrMagnitude > 0.0004f) netAim.Value = aim.AimPoint;
                float rageBonus = Rage.AttackSpeedMultiplier(gameObject) - 1f;
                if (!Mathf.Approximately(netRageAttackBonus.Value, rageBonus)) netRageAttackBonus.Value = rageBonus;
                if (energy != null)
                {
                    float e = Mathf.Floor(energy.Current);
                    if (!Mathf.Approximately(netEnergy.Value, e)) netEnergy.Value = e;
                }
                int rageState = Rage.NetState(gameObject);
                if (netRageState.Value != rageState) netRageState.Value = rageState;
                int statusFx = ReadStatusFx();
                if (netStatusFx.Value != statusFx) netStatusFx.Value = statusFx;
                return;
            }

            SyncStatusFx();

            if (upgrades == null) upgrades = GetComponent<UpgradeState>();
            if (upgrades != null) upgrades.SetMaskFromNetwork(netUpgradeMask.Value);

            float dt = Time.deltaTime;
            // Other players' replicas follow the server's position. (The server's own copies are moved by
            // PlayerMovement and are the source of that position.)
            if (hasRemoteState && !IsServer)
            {
                Vector3 pos = transform.position;
                float distance = Vector3.Distance(pos, targetPosition);
                transform.position = distance > SnapDistance
                    ? targetPosition
                    : Vector3.Lerp(pos, targetPosition, 1f - Mathf.Exp(-PositionSharpness * dt));
            }

            if (netAimReceived != Vector3.zero)
                smoothedAim = Vector3.Lerp(smoothedAim, netAimReceived, 1f - Mathf.Exp(-AimSharpness * dt));
        }

        // Back to the spawn point with full health and no energy (rematch).
        public void ResetForRematch()
        {
            if (!IsOwner) return;
            if (abilities != null)
                foreach (Ability a in abilities)
                {
                    a.ReplayCancel();
                    a.ResetCooldown(); // nothing used on the ready-room dummies carries into the round
                }
            if (respawner != null) respawner.ResetNow();
            if (energy != null) energy.Clear();
        }

        // --- Between-round upgrades ------------------------------------------------------------------------------

        // Owner: a round just ended (round number `round`). Offer the upgrade pick, or settle straight away when this
        // character has nothing to offer.
        public void OnRoundEnded(int round)
        {
            if (!IsOwner) return;
            if (upgrades == null) upgrades = GetComponent<UpgradeState>();
            if (upgrades != null) upgrades.BeginPick(round);
            else netUpgradeRound.Value = round;
        }

        // Owner: publish the owned mask and the round whose pick is now settled.
        public void PublishUpgrades(int mask, int round)
        {
            if (!IsOwner) return;
            netUpgradeMask.Value = mask;
            netUpgradeRound.Value = round;
        }

        // Owner: the next round is starting; anyone who has not chosen gets a random offer.
        public void SettleUpgradePick()
        {
            if (!IsOwner) return;
            if (upgrades == null) upgrades = GetComponent<UpgradeState>();
            if (upgrades != null) upgrades.AutoPickIfPending();
        }

        // Owner: new match, upgrades are forgotten.
        public void ResetUpgrades()
        {
            if (!IsOwner) return;
            if (upgrades == null) upgrades = GetComponent<UpgradeState>();
            if (upgrades != null) upgrades.ResetAll();
            else { netUpgradeMask.Value = 0; netUpgradeRound.Value = 0; }
        }

        // --- Ability visuals -------------------------------------------------------------------------------------

        public void SendCue(int abilityIndex, Vector3 aimPoint)
        {
            if (IsSpawned && IsOwner) CueServerRpc(abilityIndex, aimPoint);
        }

        [ServerRpc]
        void CueServerRpc(int abilityIndex, Vector3 aimPoint)
        {
            // A dedicated server runs no ClientRpcs, so it replays the cue on its own copy here: that copy needs the
            // guard, shields and wind-ups to resolve hits and to move the character correctly.
            if (!IsClient) ReplayCue(abilityIndex, aimPoint);
            CueClientRpc(abilityIndex, aimPoint);
        }

        [ClientRpc]
        void CueClientRpc(int abilityIndex, Vector3 aimPoint)
        {
            if (IsOwner) return;
            ReplayCue(abilityIndex, aimPoint);
        }

        void ReplayCue(int abilityIndex, Vector3 aimPoint)
        {
            smoothedAim = aimPoint;
            netAimReceived = aimPoint;

            if (abilityIndex < 0)
            {
                foreach (Ability a in abilities) a.ReplayCancel();
                return;
            }

            bool ex = abilityIndex >= Ability.ExCueOffset;
            if (ex) abilityIndex -= Ability.ExCueOffset;
            if (abilityIndex >= abilities.Length) return;
            Ability ability = abilities[abilityIndex];
            ability.ReplayActivate(ex);

            // Server: an ability that moves its caster (dash, leap, teleport) lets the owner report that movement
            // for a moment, up to the ability's range (see PlayerMovement's ability moves).
            if (IsServer && ability.MovementRange > 0f && movement != null)
                movement.GrantAbilityMove(ability.MovementRange, ability.MovementWindow);
        }

        // --- Breakable floor (owner -> server -> everyone) ------------------------------------------------------------

        const float MaxFloorBreakRadius = 4f;
        const float MaxFloorBreakLength = 20f;
        const float MaxFloorBreakReach = 22f; // how far from the caster a slam may start

        // Owner: one of this character's ground slams hit the floor (Arena.DestructibleFloor.Slam).
        public void SendFloorBreak(Vector3 from, Vector3 to, float radius)
        {
            if (IsSpawned && IsOwner) FloorBreakServerRpc(from, to, radius);
        }

        [ServerRpc]
        void FloorBreakServerRpc(Vector3 from, Vector3 to, float radius)
        {
            Anathema.Arena.DestructibleFloor floor = Anathema.Arena.DestructibleFloor.Instance;
            if (floor == null || health.IsDead || NetGame.Frozen || NetGame.InPrep) return;
            if (radius <= 0f || radius > MaxFloorBreakRadius) return;
            Vector3 span = to - from;
            span.y = 0f;
            if (span.magnitude > MaxFloorBreakLength) return;
            Vector3 near = from - transform.position;
            near.y = 0f;
            if (near.magnitude > MaxFloorBreakReach) return;

            // The server picks the plates from its own floor, so every machine breaks the same ones.
            int[] plates = floor.TilesIn(from, to, radius);
            if (plates.Length == 0) return;
            if (!IsClient) floor.Break(plates); // a dedicated server runs no ClientRpcs but needs the holes for the fire damage
            FloorBreakClientRpc(plates);
        }

        [ClientRpc]
        void FloorBreakClientRpc(int[] plates)
        {
            Anathema.Arena.DestructibleFloor floor = Anathema.Arena.DestructibleFloor.Instance;
            if (floor != null) floor.Break(plates);
        }

        // --- Movement (owner -> server inputs; server -> owner authoritative state) ---------------------------------

        PlayerMovement movement;

        public void SendMoveInputs(MoveInput[] inputs) => MoveInputServerRpc(inputs);

        // Unreliable: every message repeats the latest inputs, so a lost one costs nothing.
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        void MoveInputServerRpc(MoveInput[] inputs)
        {
            if (movement != null && inputs != null) movement.ServerReceiveInputs(inputs);
        }

        // Server -> owner: where the character really is after input `seq`.
        public void SendMoveState(uint seq, Vector3 position, float verticalVelocity)
        {
            if (IsOwner) return; // the host's own character needs no correction
            MoveStateClientRpc(seq, position, verticalVelocity, ToOwner);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        void MoveStateClientRpc(uint seq, Vector3 position, float verticalVelocity, ClientRpcParams rpcParams = default)
        {
            if (IsOwner && !IsServer && movement != null) movement.OwnerReceiveState(seq, position, verticalVelocity);
        }

        // Server -> owner: the game itself placed the character (spawn, round reset). Reliable; prediction restarts.
        public void SendMoveReset(Vector3 position)
        {
            if (IsOwner) return;
            MoveResetClientRpc(position, ToOwner);
        }

        [ClientRpc]
        void MoveResetClientRpc(Vector3 position, ClientRpcParams rpcParams = default)
        {
            if (IsOwner && !IsServer && movement != null) movement.OwnerReceiveReset(position);
        }

        // --- Hits (attacker -> server, which resolves them -> result back to the attacker) ----------------------------
        // A hit and its status effects travel as one message. The server checks it is legal, resolves it against its
        // own copy of the victim (counters, shields, armour, last stand) and applies the effects only if it landed:
        // damage-over-time and wards stay on the server's copy, control effects (stun, root...) go to the victim's
        // machine, which moves the character. Then the attacker is told whether it landed.

        ClientRpcParams ToOwner => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };

        static NetworkObjectReference Reference(GameObject go, out bool has)
        {
            has = go != null && go.TryGetComponent(out NetworkObject no) && no.IsSpawned;
            return has ? new NetworkObjectReference(go.GetComponent<NetworkObject>()) : default;
        }

        static GameObject Resolve(NetworkObjectReference reference, bool has)
        {
            return has && reference.TryGet(out NetworkObject no) ? no.gameObject : null;
        }

        // --- Ability targets ---------------------------------------------------------------------------------------
        // An ability that picks its own targets (the Hospitaller's tether) tells every other machine exactly whom it
        // picked, just before its cue, so their copies latch onto the same characters instead of guessing from the aim.
        // Characters that are not networked (the ready-room dummies, which every machine has its own of) arrive as null.

        public void SendTargets(int abilityIndex, GameObject[] targets)
        {
            if (!IsSpawned || !IsOwner) return;
            var refs = new NetworkObjectReference[targets.Length];
            var has = new bool[targets.Length];
            for (int i = 0; i < targets.Length; i++) refs[i] = Reference(targets[i], out has[i]);
            TargetsServerRpc(abilityIndex, refs, has);
        }

        [ServerRpc]
        void TargetsServerRpc(int abilityIndex, NetworkObjectReference[] refs, bool[] has)
        {
            if (!IsClient) ReplayTargets(abilityIndex, refs, has); // a dedicated server's own copy needs them too
            TargetsClientRpc(abilityIndex, refs, has);
        }

        [ClientRpc]
        void TargetsClientRpc(int abilityIndex, NetworkObjectReference[] refs, bool[] has)
        {
            if (IsOwner) return;
            ReplayTargets(abilityIndex, refs, has);
        }

        void ReplayTargets(int abilityIndex, NetworkObjectReference[] refs, bool[] has)
        {
            if (abilities == null || abilityIndex < 0 || abilityIndex >= abilities.Length || refs == null || has == null) return;
            var targets = new List<Health>(refs.Length);
            for (int i = 0; i < refs.Length && i < has.Length; i++)
            {
                GameObject go = Resolve(refs[i], has[i]);
                targets.Add(go != null ? go.GetComponent<Health>() : null);
            }
            abilities[abilityIndex].ReceiveTargets(targets);
        }

        // Sanity caps on a single hit or heal; anything above them is not a real attack.
        const float MaxHitDamage = 500f;
        const float MaxHeal = 300f;

        static int nextHitId;
        static readonly Dictionary<int, System.Action<bool>> PendingHits = new Dictionary<int, System.Action<bool>>();

        // Attacker's machine: send a hit at this (remote) player.
        public void SendHit(Hit hit, GameObject source, System.Action<bool> onResult)
        {
            int id = 0;
            if (onResult != null)
            {
                id = ++nextHitId;
                PendingHits[id] = onResult;
            }

            NetworkObjectReference reference = Reference(source, out bool hasSource);
            HitServerRpc(hit, reference, hasSource, id);
        }

        static ClientRpcParams ToClient(ulong clientId) => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void HitServerRpc(Hit hit, NetworkObjectReference source, bool hasSource, int hitId, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            NetPlayer attacker = null;
            if (hasSource && source.TryGet(out NetworkObject sourceObject)) sourceObject.TryGetComponent(out attacker);

            // The server's say: the sender must really own the attacker, both must be alive and on opposing teams,
            // the round must be live and the numbers must be plausible.
            bool legal = attacker != null
                         && attacker.OwnerClientId == sender
                         && !attacker.health.IsDead && !health.IsDead
                         && attacker.health.Team != health.Team
                         && !NetGame.Frozen
                         && hit.Damage > 0f && hit.Damage <= MaxHitDamage;

            // Resolved here, on the server's own copy of the victim.
            bool landed = legal && health.ApplyHitLocal(hit, attacker.gameObject);
            if (hitId != 0) HitResultClientRpc(hitId, landed, ToClient(sender));
        }

        [ClientRpc]
        void HitResultClientRpc(int hitId, bool landed, ClientRpcParams rpcParams = default)
        {
            if (PendingHits.Remove(hitId, out System.Action<bool> callback)) callback(landed);
        }

        // --- Healing (any machine -> server) ------------------------------------------------------------------------

        public void SendHeal(float amount, bool permanent, GameObject source)
        {
            NetworkObjectReference reference = Reference(source, out bool hasSource);
            HealServerRpc(amount, permanent, reference, hasSource);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void HealServerRpc(float amount, bool permanent, NetworkObjectReference source, bool hasSource, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            GameObject healer = Resolve(source, hasSource);

            // A player may heal themselves (abilities, orbs) or, through their own character, a teammate.
            bool self = OwnerClientId == sender;
            bool ally = healer != null && healer.TryGetComponent(out NetPlayer h) && h.OwnerClientId == sender && h.health.Team == health.Team;
            if (!(self || ally) || amount <= 0f || amount > MaxHeal || health.IsDead) return;

            if (permanent) health.HealPermanent(amount, healer);
            else health.Heal(amount, healer);
        }

        // --- Counters and shields resolved by the server ------------------------------------------------------------

        // Server: this character's counter caught a hit. Everyone drops the guard and sees the burst; the owner's
        // machine runs the payoff (riposte, teleport window...), sending its own hits back through the server.
        public void ServerCounterTriggered(GameObject attacker)
        {
            NetworkObjectReference reference = Reference(attacker, out bool has);
            CounterTriggeredClientRpc(reference, has);
        }

        [ClientRpc]
        void CounterTriggeredClientRpc(NetworkObjectReference attacker, bool hasAttacker)
        {
            var counter = GetComponent<Counter>();
            if (counter != null) counter.ResolveFromServer(Resolve(attacker, hasAttacker), IsOwner);
        }

        public enum ShieldKind { Kine, WarpWard }

        // Server: a shield on this character ran out of strength. Every machine removes its copy with the break effect.
        public void ServerShieldBroken(ShieldKind kind) => ShieldBrokenClientRpc((int)kind);

        // Server: this character's poison burst (full stacks, or set off early). Every other machine plays the blast at
        // the same `size` (the server's copy already did).
        public void ServerPoisonBurst(float size)
        {
            if (IsServer) PoisonBurstClientRpc(size);
        }

        // Festering (the Plague Surgeon's rot) is held on the machine that laid it, usually the surgeon's. That machine
        // reports this character's stack count here and every other machine draws a visual-only copy.
        public void SendRotStacks(int stacks, float seconds)
        {
            if (IsSpawned) RotStacksRpc(stacks, seconds);
        }

        [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
        void RotStacksRpc(int stacks, float seconds) => Festering.ShowReplica(gameObject, stacks, seconds);

        [ClientRpc]
        void PoisonBurstClientRpc(float size)
        {
            if (IsServer) return;
            Poisoned.PlayBurst(transform.position, size);
        }

        // Server: this (Mandrake) character's poison ticked on a low-health enemy; its owner refunds Shadow Veil
        // (Abilities.Mandrake.SoulHarvest), which tracks the per-target allowance.
        public void ServerSoulHarvest(GameObject victim)
        {
            if (!IsServer || victim == null || !victim.TryGetComponent(out NetworkObject victimObject) || !victimObject.IsSpawned) return;
            SoulHarvestClientRpc(victimObject, ToOwner);
        }

        [ClientRpc]
        void SoulHarvestClientRpc(NetworkObjectReference victim, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner || !victim.TryGet(out NetworkObject victimObject)) return;
            if (TryGetComponent(out Anathema.Abilities.Mandrake.SoulHarvest harvest) && victimObject.TryGetComponent(out Health victimHealth))
                harvest.Harvest(victimHealth);
        }

        // Server: this character's balefire was set off by its caster's hit. Every other machine plays the burst.
        public void ServerBalefireBurst()
        {
            if (IsServer) BalefireBurstClientRpc();
        }

        [ClientRpc]
        void BalefireBurstClientRpc()
        {
            if (IsServer) return;
            Balefire.PlayBurst(transform.position);
        }

        [ClientRpc]
        void ShieldBrokenClientRpc(int kind)
        {
            if (IsServer) return; // the server's copy broke it already
            switch ((ShieldKind)kind)
            {
                case ShieldKind.Kine:
                    var barrier = GetComponent<Anathema.Abilities.Psyker.PsychicBarrier>();
                    if (barrier != null) barrier.BreakFromServer();
                    break;
                case ShieldKind.WarpWard:
                    Anathema.Abilities.Psyker.WarpWardShield.Break(gameObject);
                    break;
            }
        }

        // --- Status effects ---------------------------------------------------------------------------------------
        // Every effect is decided and held on the server's copy. Control effects (stun, root, slow, panic, knockback...)
        // are also sent to the owner, who predicts movement with them and blocks ability use (RouteControl). Effects
        // that only work on the server's copy (ward, bleed, burn) are drawn everywhere else from netServerFx.

        public void SendStatus(StatusKind kind, float a, float b, Vector3 direction, GameObject source = null)
        {
            NetworkObjectReference reference = Reference(source, out bool hasSource);
            StatusServerRpc((int)kind, a, b, direction, reference, hasSource);
        }

        static bool IsSupportive(StatusKind kind) => kind == StatusKind.Ward || kind == StatusKind.Heal || kind == StatusKind.Enrage
                                                     || kind == StatusKind.Barrier || kind == StatusKind.Cleanse
                                                     || kind == StatusKind.Armor || kind == StatusKind.FaithArmor;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void StatusServerRpc(int kind, float a, float b, Vector3 direction, NetworkObjectReference source, bool hasSource, RpcParams rpcParams = default)
        {
            var status = (StatusKind)kind;
            ulong sender = rpcParams.Receive.SenderClientId;
            GameObject sourceObject = Resolve(source, hasSource);

            // From a client, judged by that client's own character: hostile effects only at an enemy, supportive ones only
            // at a teammate, and nothing while they are down. Panic breaks and enrages are the server's own decisions.
            if (sender != NetworkManager.ServerClientId)
            {
                if (status == StatusKind.PanicBreak || status == StatusKind.Enrage || status == StatusKind.Heal) return;
                NetPlayer from = PlayerOf(sender);
                if (from == null || from.health.IsDead || NetGame.Frozen) return;
                bool sameTeam = from.health.Team == health.Team;
                if (IsSupportive(status) != sameTeam) return;
                // A named source must be the sender's own character (it is credited for bleeds, burns and wards).
                if (sourceObject != null && sourceObject != from.gameObject) sourceObject = from.gameObject;
            }
            if (health.IsDead) return;

            // Applied to the server's copy; control effects also reach the owner through RouteControl.
            ApplyStatusHere(status, a, b, direction, sourceObject);
        }

        // Server -> owner: a control effect the server now holds, for the owner to predict with and enforce.
        public void ForwardStatusToOwner(StatusKind kind, float a, float b, Vector3 direction, GameObject source)
        {
            if (!IsServer || IsOwner) return;
            NetworkObjectReference reference = Reference(source, out bool hasSource);
            StatusClientRpc((int)kind, a, b, direction, reference, hasSource, ToOwner);
        }

        // Where a control effect (stun, root, slow, maim, silence, panic, knockback) is applied. Returns true when the
        // caller must stop (the effect was sent elsewhere), false when it should apply it on this machine:
        //   - offline, or the character is ours: apply here;
        //   - a client aiming at someone else: ask the server, which decides;
        //   - the server: hold it on its own copy (movement is simulated there) and tell the owner too.
        public static bool RouteControl(GameObject target, StatusKind kind, float a, float b, Vector3 direction, GameObject source = null)
        {
            if (!TryGetRemote(target, out NetPlayer remote)) return false;
            if (!remote.IsServer)
            {
                remote.SendStatus(kind, a, b, direction, source);
                return true;
            }
            remote.ForwardStatusToOwner(kind, a, b, direction, source);
            return false;
        }

        // Server: the character a connected client plays (null between matches).
        static NetPlayer PlayerOf(ulong clientId)
        {
            foreach (NetPlayer p in All)
                if (p != null && p.OwnerClientId == clientId) return p;
            return null;
        }

        [ClientRpc]
        void StatusClientRpc(int kind, float a, float b, Vector3 direction, NetworkObjectReference source, bool hasSource, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner || health.IsDead) return;
            ApplyStatusHere((StatusKind)kind, a, b, direction, Resolve(source, hasSource));
        }

        void ApplyStatusHere(StatusKind kind, float a, float b, Vector3 direction, GameObject attacker)
        {
            switch (kind)
            {
                case StatusKind.Stun: Stunned.Apply(gameObject, a); break;
                case StatusKind.Silence: Silenced.Apply(gameObject, a, b > 0f); break;
                case StatusKind.Maim: Maimed.Apply(gameObject, a, b); break;
                case StatusKind.Knockback: Knockback.Apply(gameObject, direction, a, b); break;
                case StatusKind.Root: Rooted.Apply(gameObject, a); break;
                case StatusKind.Bleed: Bleeding.Apply(gameObject, attacker, a, b); break;
                case StatusKind.Panic: Panicked.Apply(gameObject, attacker, a); break;
                case StatusKind.Ward: PlagueWard.Apply(gameObject, attacker, a, b); break;
                case StatusKind.Slow: Slowed.Apply(gameObject, a, b); break;
                case StatusKind.Burn: Burning.Apply(gameObject, attacker, a, b); break;
                case StatusKind.PanicBreak: Panicked.Cancel(gameObject); break;
                case StatusKind.Enrage: Enraged.Apply(gameObject, a); break;
                case StatusKind.Barrier: Barrier.Grant(gameObject, a, b, direction.x, attacker); break;
                case StatusKind.Cleanse: Cleanse.Apply(gameObject, a, b, attacker); break;
                case StatusKind.Poison: Poisoned.Apply(gameObject, attacker, Mathf.RoundToInt(direction.x), a, b); break;
                case StatusKind.Balefire: Balefire.Apply(gameObject, attacker, a, b); break;
                case StatusKind.Armor: if (health.IsAuthority) health.GrantTemporaryArmor(a, b); break;
                case StatusKind.FaithArmor:
                    if (health.IsAuthority && GetComponent<Anathema.Abilities.Hospitaller.SanctifiedTether>() != null)
                        health.HoldArmor(Mathf.Min(a, Anathema.Abilities.Hospitaller.Faith.ReductionCeiling), Mathf.Min(b, 1f));
                    break;
                case StatusKind.Leech: LeechMark.Refresh(gameObject, attacker, a); break;
            }
        }
    }
}
