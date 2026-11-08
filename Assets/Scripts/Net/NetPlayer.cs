using System.Collections.Generic;
using Anathema.Abilities;
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
            Bleed
        }

        public static readonly List<NetPlayer> All = new List<NetPlayer>();
        public static NetPlayer Local { get; private set; }

        const float SnapDistance = 5f;
        const float PositionSharpness = 22f;
        const float AimSharpness = 30f;

        readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> netRecoverable = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> netPosition =new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> netAim = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<int> slot = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> kills = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        // Server only: which seat (0 = host, 1 = guest) this character belongs to; call before spawning.
        public void ServerSetSlot(int value) => slot.Value = value;
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
            health.SetTeam(slot.Value + 1);
            slot.OnValueChanged += OnSlotChanged;
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
            if (health != null) health.Died -= OnOwnerDied;
            netPosition.OnValueChanged -= OnRemotePosition;
            netAim.OnValueChanged -= OnRemoteAim;
            slot.OnValueChanged -= OnSlotChanged;
        }

        // Seat 0 (host) is team 1, seat 1 is team 2, so the two fighters are always enemies.
        void OnSlotChanged(int previous, int current)
        {
            health.SetTeam(current + 1);
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

        void OnOwnerDied(Health _, GameObject killer)
        {
            NetworkObjectReference reference = default;
            bool hasKiller = false;
            if (killer != null && killer.TryGetComponent(out NetworkObject no) && no.IsSpawned && no != NetworkObject)
            {
                reference = no;
                hasKiller = true;
            }
            ReportDeathServerRpc(reference, hasKiller);
        }

        [ServerRpc]
        void ReportDeathServerRpc(NetworkObjectReference killer, bool hasKiller)
        {
            NetPlayer credit = null;
            if (hasKiller && killer.TryGet(out NetworkObject no)) credit = no.GetComponent<NetPlayer>();

            // Gas or other environmental deaths hand the point to the opponent.
            if (credit == null)
            {
                foreach (NetPlayer other in All)
                    if (other != this) credit = other;
            }

            if (credit != null && credit != this) NetGame.Instance?.ServerOnKill(credit, this);
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

            if (abilityIndex < abilities.Length) abilities[abilityIndex].ReplayActivate();
        }

        // --- Damage and status effects (aimed at a replica, applied on its owner) ---------------------------------

        public void SendDamage(float amount, GameObject source)
        {
            NetworkObjectReference reference = default;
            bool hasSource = false;
            if (source != null && source.TryGetComponent(out NetworkObject no) && no.IsSpawned)
            {
                reference = no;
                hasSource = true;
            }
            DamageServerRpc(amount, reference, hasSource);
        }

        ClientRpcParams ToOwner => new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };

        [ServerRpc(RequireOwnership = false)]
        void DamageServerRpc(float amount, NetworkObjectReference source, bool hasSource)
        {
            DamageClientRpc(amount, source, hasSource, ToOwner);
        }

        [ClientRpc]
        void DamageClientRpc(float amount, NetworkObjectReference source, bool hasSource, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner || NetGame.Frozen) return;

            GameObject attacker = null;
            if (hasSource && source.TryGet(out NetworkObject no)) attacker = no.gameObject;
            health.ApplyDamageLocal(amount, attacker);
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
                case StatusKind.Silence: Silenced.Apply(gameObject, a); break;
                case StatusKind.Maim: Maimed.Apply(gameObject, a, b); break;
                case StatusKind.Knockback: Knockback.Apply(gameObject, direction, a, b); break;
                case StatusKind.Root: Rooted.Apply(gameObject, a); break;
                case StatusKind.Bleed: Bleeding.Apply(gameObject, attacker, a, b); break;
            }
        }
    }
}
