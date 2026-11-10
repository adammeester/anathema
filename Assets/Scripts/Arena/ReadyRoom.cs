using System.Collections.Generic;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Arena
{
    // A walled ready room built into the arena perimeter, one on each side (-X "left", +X "right"). Players start every
    // round in here; a gate across the doorway in the arena wall is shut while NetGame.InPrep is true (the between-rounds
    // ready phase) and slides open when the round begins.
    //
    // The scene builder (ArenaSceneBuilder) creates the room: the root sits on the wall line with its forward (+Z) axis
    // pointing out of the arena, and it has a child named "Gate" holding the gate's collider and renderer. Everything else
    // here (slots, containment, gate animation) is derived from the root transform.
    public class ReadyRoom : MonoBehaviour
    {
        public static readonly List<ReadyRoom> All = new List<ReadyRoom>();

        // Interior, in the root's local space: X is across the room, Z runs outward from the wall line.
        public const float HalfWidth = 8.0f;
        public const float NearZ = 0.9f;
        public const float FarZ = 14.0f;

        const int Columns = 4;
        const float ColumnSpacing = 3.6f;
        const float FirstRowZ = 4.2f;
        const float RowSpacing = 4.0f;
        public const int Capacity = 8;

        const float GateTravel = 4.5f;   // how far the gate sinks into the floor when open (more than its height)
        const float GateSpeed = 2.5f;    // open fraction per second

        Transform gate;
        Collider gateCollider;
        Renderer[] gateRenderers;
        Vector3 gateClosedLocal;
        float open;

        public bool IsLeft => transform.position.x < 0f;

        void Awake()
        {
            gate = transform.Find("Gate");
            if (gate == null) return;
            gateCollider = gate.GetComponent<Collider>();
            gateRenderers = gate.GetComponentsInChildren<Renderer>(true);
            gateClosedLocal = gate.localPosition;
            open = NetGame.InPrep ? 0f : 1f;
            ApplyGate();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (gate == null) return;
            open = Mathf.MoveTowards(open, NetGame.InPrep ? 0f : 1f, GateSpeed * Time.deltaTime);
            ApplyGate();
        }

        void ApplyGate()
        {
            gate.localPosition = gateClosedLocal + Vector3.down * (open * GateTravel);
            if (gateCollider != null) gateCollider.enabled = open < 0.5f;
            bool visible = open < 0.98f;
            foreach (Renderer r in gateRenderers) r.enabled = visible;
        }

        public bool Contains(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return local.z > NearZ && local.z < FarZ && Mathf.Abs(local.x) < HalfWidth;
        }

        // The spot for the index-th of count players starting in this room: rows of up to four, each row centred,
        // facing the gate and the arena beyond it.
        public Vector3 SlotPosition(int index, int count, out Quaternion rotation)
        {
            index = Mathf.Clamp(index, 0, Capacity - 1);
            int row = index / Columns;
            int inRow = Mathf.Clamp(count - row * Columns, 1, Columns);
            float x = (index % Columns - (inRow - 1) * 0.5f) * ColumnSpacing;
            Vector3 world = transform.TransformPoint(new Vector3(x, 0f, FirstRowZ + row * RowSpacing));
            world.y = 0.1f;
            rotation = Quaternion.LookRotation(-transform.forward, Vector3.up);
            return world;
        }

        public static bool AnyContains(Vector3 world)
        {
            foreach (ReadyRoom room in All)
                if (room != null && room.Contains(world)) return true;
            return false;
        }

        public static ReadyRoom Find(bool left)
        {
            foreach (ReadyRoom room in All)
                if (room != null && room.IsLeft == left) return room;
            return null;
        }

        // Both rooms exist (every online arena scene built since ready rooms were added).
        public static bool Available => Find(true) != null && Find(false) != null;
    }
}
