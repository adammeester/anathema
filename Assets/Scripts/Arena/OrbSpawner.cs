using Anathema.Combat;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Arena
{
    // Drops orbs on fixed spots in four straight lines around the arena (relative to the gas zone's centre):
    // health orbs along the north (+Z) and south (-Z) edges, energy orbs along the east (+X) and west (-X) edges.
    // Each interval the next line (north, east, south, west, ...) refills any empty spots. Offline it spawns them
    // itself; online only the host spawns (and tells the other player through NetGame), so both machines see the same
    // orbs. Spots outside the closing gas are skipped.
    public class OrbSpawner : MonoBehaviour
    {
        [SerializeField] float firstDelay = 8f;
        [SerializeField] float interval = 12f;
        [SerializeField] float healthAmount = HealingOrb.DefaultAmount;
        [SerializeField] float energyAmount = HealingOrb.DefaultEnergyAmount;
        [SerializeField] float northSouthDistance = 16f;  // health lines
        [SerializeField] float eastWestDistance = 14f;    // energy lines (kept inside the fighters' spawns at x = +-20)
        [SerializeField] int orbsPerLine = 5;
        [SerializeField] float orbSpacing = 3f;

        // Order the lines refill in: direction from the centre, and the orb kind on that line.
        static readonly Vector3[] LineDirections = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        GasZone gas;
        float nextSpawn;
        int nextId = 1;
        int nextLine;

        void Start()
        {
            gas = FindFirstObjectByType<GasZone>();
            nextSpawn = Time.time + firstDelay;
        }

        // Called on rematch.
        public void Restart()
        {
            HealingOrb.ClearAll();
            nextSpawn = Time.time + firstDelay;
            nextLine = 0;
        }

        void Update()
        {
            if (NetGame.Online && (!NetGame.IsHost || NetPlayer.All.Count < 2)) return;
            if (NetGame.Frozen || Time.time < nextSpawn) return;

            nextSpawn = Time.time + interval;
            SpawnLine(LineDirections[nextLine]);
            nextLine = (nextLine + 1) % LineDirections.Length;
        }

        void SpawnLine(Vector3 direction)
        {
            bool northSouth = Mathf.Abs(direction.z) > 0.5f;
            OrbKind kind = northSouth ? OrbKind.Health : OrbKind.Energy;
            float amount = northSouth ? healthAmount : energyAmount;
            float distance = northSouth ? northSouthDistance : eastWestDistance;
            Vector3 along = new Vector3(direction.z, 0f, direction.x); // perpendicular to the direction

            Vector3 centre = gas != null ? gas.transform.position : Vector3.zero;
            float safeRadius = gas != null ? gas.CurrentRadius - 1f : float.MaxValue;

            for (int i = 0; i < orbsPerLine; i++)
            {
                float offset = (i - (orbsPerLine - 1) * 0.5f) * orbSpacing;
                Vector3 point = centre + direction * distance + along * offset;
                point.y = 0.1f;

                Vector3 fromCentre = point - centre;
                fromCentre.y = 0f;
                if (fromCentre.magnitude > safeRadius) continue; // that part of the line is already in the gas
                if (Occupied(point)) continue;

                int id = nextId++;
                HealingOrb.Spawn(id, point, kind, amount);
                if (NetGame.Online) NetGame.Instance.BroadcastOrbSpawn(id, point, kind, amount);
            }
        }

        static bool Occupied(Vector3 point)
        {
            foreach (HealingOrb orb in HealingOrb.All)
            {
                if (orb == null) continue;
                Vector3 d = orb.BasePosition - point;
                d.y = 0f;
                if (d.sqrMagnitude < 0.25f) return true;
            }
            return false;
        }
    }
}
