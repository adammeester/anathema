using System.Collections.Generic;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Combat
{
    // Hides the object on death and restores it at its spawn position after a delay.
    // Online: only the owning machine runs the timer; replicas hide/show to follow the synced health.
    [RequireComponent(typeof(Health))]
    public class Respawner : MonoBehaviour
    {
        [SerializeField] float delay = 3f;

        Health health;
        Vector3 startPosition;
        Quaternion startRotation;
        bool hidden;
        readonly List<Collider> disabledColliders = new List<Collider>();

        public void Configure(float respawnDelay)
        {
            delay = respawnDelay;
        }

        public Vector3 SpawnPosition => startPosition;

        public void SetSpawn(Vector3 position, Quaternion rotation)
        {
            startPosition = position;
            startRotation = rotation;
        }

        // Instantly puts the character back at its spawn with full health (used for rematches).
        public void ResetNow()
        {
            CancelInvoke(nameof(Respawn));
            Respawn();
        }

        void Awake()
        {
            health = GetComponent<Health>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void Update()
        {
            // Replica: the owner respawned, so show the character again.
            if (hidden && health.IsReplica && !health.IsDead) SetAlive(true);
        }

        void OnDied(Health _, GameObject killer)
        {
            SetAlive(false);
            // Online, a death stays a death until the round resets (the round only ends once a whole team is down).
            if (!health.IsReplica && !NetGame.Online) Invoke(nameof(Respawn), delay);
        }

        void Respawn()
        {
            // Colliders (including a CharacterController) are disabled here, so the teleport isn't overridden.
            transform.SetPositionAndRotation(startPosition, startRotation);
            health.ResetHealth();
            SetAlive(true);
        }

        void SetAlive(bool alive)
        {
            hidden = !alive;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = alive;

            if (!alive)
            {
                disabledColliders.Clear();
                foreach (Collider c in GetComponentsInChildren<Collider>(true))
                {
                    if (!c.enabled) continue;
                    c.enabled = false;
                    disabledColliders.Add(c);
                }
            }
            else
            {
                // Only restore what we turned off, so the owner/replica collider setup survives a respawn.
                foreach (Collider c in disabledColliders)
                    if (c != null) c.enabled = true;
                disabledColliders.Clear();
            }
        }
    }
}
