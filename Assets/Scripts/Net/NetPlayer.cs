using System.Collections.Generic;
using Anathema.Abilities;
using Anathema.Abilities.PlagueSurgeon;
using Anathema.CameraRig;
using Anathema.Combat;
using Anathema.Player;
using Unity.Netcode;
using UnityEngine;

namespace Anathema.Net
{
    // Network side of a player character. The owning machine is authoritative: it runs input, movement, abilities and
    // health, and publishes position/aim/health. Every other machine shows a "replica" that follows that state and
    // replays the owner's ability visuals. Damage and status effects aimed at a replica are forwarded to its owner.
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
            Slow
        }

        public static readonly List<NetPlayer> All = new List<NetPlayer>();
        public static NetPlayer Local { get; private set; }

        const float SnapDistance = 5f;
        const float PositionSharpness = 22f;
        const float AimSharpness = 30f;

        readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> netRecoverable = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> netDamageTaken = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> netHealed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // Credit for the report: written by the server when a victim's machine reports who hurt/healed them.
        readonly NetworkVariable<float> netDamageDealt = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> netHealingDone = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<Vector3> netPosition =new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> netAim = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<int> slot = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> kills = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Team id (1-based, matches Health.Team). Free-for-all gives every seat its own team.
        readonly NetworkVariable<int> team = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        Health health;
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
        // This round's damage report numbers; readable on both machines.
        public float RoundDamageTaken => IsOwner ? health.RoundDamageTaken : netDamageTaken.Value;
        public float RoundHealed => IsOwner ? health.RoundHealed : netHealed.Value;
        public float RoundDamageDealt => netDamageDealt.Value;
        public float RoundHealingDone => netHealingDone.Value;

        // Server only: this fighter has died this round (cleared when the next round starts).
        public bool ServerDead { get; set; }

        public void ServerResetRoundStats()
        {
            ServerDead = false;
            netDamageDealt.Value = 0f;
            netHealingDone.Value = 0f;
        }

        // Victim's machine -> server: credit the attacker (damage) or healer (healing) in the report.
        void ReportCredit(GameObject source, float damage, float healing)
        {
            if (source == null || source == gameObject || !source.TryGetComponent(out NetworkObject no) || !no.IsSpawned) return;
            CreditServerRpc(no, damage, healing);
        }

        [ServerRpc(RequireOwnership = false)]
        void CreditServerRpc(NetworkObjectReference credited, float damage, float healing)
        {
            if (!credited.TryGet(out NetworkObject no) || !no.TryGetComponent(out NetPlayer np)) return;
            np.netDamageDealt.Value += damage;
            np.netHealingDone.Value += healing;
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
            abilities = GetComponents<Ability>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            health.SetTeam(team.Value);
            team.OnValueChanged += OnTeamChanged;
            smoothedAim = transform.position + transform.forward * 3f;

            if (IsOwner)
            {
                Local = this;
                if (respawner != null) respawner.SetSpawn(transform.position, transform.rotation);
                netPosition.Value = transform.position;
                netAim.Value = smoothedAim;
                netHealth.Value = health.Max;
                netRecoverable.Value = 0f;
                health.Died += OnOwnerDied;
                health.DamageTaken += OnOwnerDamageTaken;

                TopDownCamera cam = FindFirstObjectByType<TopDownCamera>();
                if (cam != null) cam.SetTarget(transform);

                if (proxyCollider != null) proxyCollider.enabled = false;
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
                health.Died -= OnOwnerDied;
                health.DamageTaken -= OnOwnerDamageTaken;
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

            if (IsOwner)
            {
                if (!Mathf.Approximately(netHealth.Value, health.Current)) netHealth.Value = health.Current;
                if (!Mathf.Approximately(netRecoverable.Value, health.Recoverable)) netRecoverable.Value = health.Recoverable;
                if ((netPosition.Value - transform.position).sqrMagnitude > 0.0004f) netPosition.Value = transform.position;
                if (!Mathf.Approximately(netDamageTaken.Value, health.RoundDamageTaken)) netDamageTaken.Value = health.RoundDamageTaken;
                if (!Mathf.Approximately(netHealed.Value, health.RoundHealed)) netHealed.Value = health.RoundHealed;
                if (aim == null) aim = GetComponent<MouseAim>();
                if (aim != null && (netAim.Value - aim.AimPoint).sqrMagnitude > 0.0004f) netAim.Value = aim.AimPoint;
                return;
            }

            health.SyncFromNetwork(netHealth.Value, netRecoverable.Value);

            float dt = Time.deltaTime;
            if (hasRemoteState)
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

        // --- Death / score ---------------------------------------------------------------------------------------

        void OnOwnerDamageTaken(float dealt, GameObject source) => ReportCredit(source, dealt, 0f);

        void OnOwnerDied(Health _, GameObject killer) => ReportDeathServerRpc();

        // A round ends when only one team has anyone left standing (checked by the server).
        [ServerRpc]
        void ReportDeathServerRpc()
        {
            NetGame.Instance?.ServerOnDeath(this);
        }

        // Back to the spawn point with full health and no energy (rematch).
        public void ResetForRematch()
        {
            if (!IsOwner) return;
            if (abilities != null) foreach (Ability a in abilities) a.ReplayCancel();
            if (respawner != null) respawner.ResetNow();
            if (energy != null) energy.Clear();
            netPosition.Value = transform.position;
        }

        // --- Ability visuals -------------------------------------------------------------------------------------

        public void SendCue(int abilityIndex, Vector3 aimPoint)
        {
            if (IsSpawned && IsOwner) CueServerRpc(abilityIndex, aimPoint);
        }

        [ServerRpc]
        void CueServerRpc(int abilityIndex, Vector3 aimPoint)
        {
            CueClientRpc(abilityIndex, aimPoint);
        }

        [ClientRpc]
        void CueClientRpc(int abilityIndex, Vector3 aimPoint)
        {
            if (IsOwner) return;

            smoothedAim = aimPoint;
            netAimReceived = aimPoint;

            if (abilityIndex < 0)
            {
                foreach (Ability a in abilities) a.ReplayCancel();
                return;
            }

            bool ex = abilityIndex >= Ability.ExCueOffset;
            if (ex) abilityIndex -= Ability.ExCueOffset;
            if (abilityIndex < abilities.Length) abilities[abilityIndex].ReplayActivate(ex);
        }

        // --- Damage and status effects (aimed at a replica, applied on its owner) ---------------------------------

        public void SendDamage(float amount, GameObject source, bool aoe = false)
        {
            NetworkObjectReference reference = default;
            bool hasSource = false;
            if (source != null && source.TryGetComponent(out NetworkObject no) && no.IsSpawned)
            {
                reference = no;
                hasSource = true;
            }
            DamageServerRpc(amount, reference, hasSource, aoe);
        }

        ClientRpcParams ToOwner => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };

        [ServerRpc(RequireOwnership = false)]
        void DamageServerRpc(float amount, NetworkObjectReference source, bool hasSource, bool aoe)
        {
            DamageClientRpc(amount, source, hasSource, aoe, ToOwner);
        }

        [ClientRpc]
        void DamageClientRpc(float amount, NetworkObjectReference source, bool hasSource, bool aoe, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner || NetGame.Frozen) return;

            GameObject attacker = null;
            if (hasSource && source.TryGet(out NetworkObject no)) attacker = no.gameObject;
            health.ApplyDamageLocal(amount, attacker, aoe);
        }

        public void SendStatus(StatusKind kind, float a, float b, Vector3 direction, GameObject source = null)
        {
            NetworkObjectReference reference = default;
            bool hasSource = false;
            if (source != null && source.TryGetComponent(out NetworkObject no) && no.IsSpawned)
            {
                reference = no;
                hasSource = true;
            }
            StatusServerRpc((int)kind, a, b, direction, reference, hasSource);
        }

        [ServerRpc(RequireOwnership = false)]
        void StatusServerRpc(int kind, float a, float b, Vector3 direction, NetworkObjectReference source, bool hasSource)
        {
            StatusClientRpc(kind, a, b, direction, source, hasSource, ToOwner);
        }

        [ClientRpc]
        void StatusClientRpc(int kind, float a, float b, Vector3 direction, NetworkObjectReference source, bool hasSource, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner || health.IsDead) return;

            GameObject attacker = null;
            if (hasSource && source.TryGet(out NetworkObject sourceObject)) attacker = sourceObject.gameObject;

            switch ((StatusKind)kind)
            {
                case StatusKind.Stun: Stunned.Apply(gameObject, a); break;
                case StatusKind.Silence: Silenced.Apply(gameObject, a, b > 0f); break;
                case StatusKind.Maim: Maimed.Apply(gameObject, a, b); break;
                case StatusKind.Knockback: Knockback.Apply(gameObject, direction, a, b); break;
                case StatusKind.Root: Rooted.Apply(gameObject, a); break;
                case StatusKind.Bleed: Bleeding.Apply(gameObject, attacker, a, b); break;
                case StatusKind.Heal: ReportCredit(attacker, 0f, health.Heal(a)); break;
                case StatusKind.Panic: Panicked.Apply(gameObject, attacker, a); break;
                case StatusKind.Ward: PlagueWard.Apply(gameObject, attacker, a, b); break;
                case StatusKind.Slow: Slowed.Apply(gameObject, a, b); break;
            }
        }
    }
}
