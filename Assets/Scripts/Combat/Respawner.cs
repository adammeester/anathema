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

        // Instantly puts the character back at its spawn with full health (used for rematches). Online the server
        // restores the health; this machine just moves the character and shows it once the server says it lives.
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
            // Online: the server brought the character back, so show it again.
            // (Offline the death timer's Respawn does this; a character is never hidden while alive.)
            if (hidden && !health.IsDead) SetAlive(true);
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
            // A living player (the round winner) still has an enabled CharacterController, which would override the teleport.
            CharacterController controller = GetComponent<CharacterController>();
            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled) controller.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            if (controllerWasEnabled) controller.enabled = true;
            Physics.SyncTransforms();
            if (health.IsAuthority) health.ResetHealth();
            // Online the health comes back from the server; until then the character stays hidden (see Update), so a
            // late "still dead" update can't kill it again.
            if (!health.IsDead) SetAlive(true);
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
