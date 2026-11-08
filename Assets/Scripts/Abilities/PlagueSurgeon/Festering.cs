using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.PlagueSurgeon
{
    // Status effect from the Plague Surgeon's basic attack: stacking rot. Every hit adds its own stack, and each stack
    // runs on its own timer, so the poison only piles up while hits keep landing and drains away once they stop.
    // Added to the victim at runtime on the surgeon's machine; the damage is forwarded to a remote victim like any hit.
    // One orbiting green blob per active stack shows how bad it is.
    public class Festering : MonoBehaviour
    {
        const float TickInterval = 0.25f;

        readonly List<float> expiries = new List<float>();
        readonly List<Transform> blobs = new List<Transform>();
        GameObject source;
        Energy sourceEnergy;
        Health health;
        float damagePerStackPerSecond;
        float nextTick;
        Material material;

        public int Stacks => expiries.Count;

        // `stacks` > 1 lays several stacks at once (the ward's death-cloud uses this for a heavier rot).
        public static Festering Apply(GameObject target, GameObject damageSource, Energy sourceEnergy, float duration, float damagePerSecond, int stacks = 1)
        {
            var festering = target.GetComponent<Festering>();
            if (festering == null)
            {
                festering = target.AddComponent<Festering>();
                festering.nextTick = Time.time + TickInterval;
            }

            festering.source = damageSource;
            festering.sourceEnergy = sourceEnergy;
            festering.damagePerStackPerSecond = damagePerSecond;
            for (int i = 0; i < stacks; i++) festering.expiries.Add(Time.time + duration);
            return festering;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            material = VisualUtil.UnlitMaterial(new Color(0.45f, 0.7f, 0.1f, 0.9f));
        }

        void Update()
        {
            expiries.RemoveAll(t => Time.time >= t);
            if (health == null || health.IsDead || expiries.Count == 0)
            {
                Destroy(this);
                return;
            }

            SyncBlobs();

            if (Anathema.Net.NetGame.Frozen) return;
            while (Time.time >= nextTick)
            {
                nextTick += TickInterval;
                if (expiries.Count == 0) break;

                float amount = expiries.Count * damagePerStackPerSecond * TickInterval;
                if (health.TakeDamage(amount, source, true) && sourceEnergy != null) sourceEnergy.Gain(0.5f);
                if (health.IsDead) return;
            }
        }

        void SyncBlobs()
        {
            while (blobs.Count < expiries.Count)
            {
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = "Rot Blob";
                Destroy(b.GetComponent<Collider>());
                b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b.GetComponent<Renderer>().sharedMaterial = material;
                b.transform.SetParent(transform, false);
                b.transform.localScale = Vector3.one * 0.2f;
                blobs.Add(b.transform);
            }

            while (blobs.Count > expiries.Count)
            {
                Destroy(blobs[blobs.Count - 1].gameObject);
                blobs.RemoveAt(blobs.Count - 1);
            }

            for (int i = 0; i < blobs.Count; i++)
            {
                float a = Time.time * 3f + i * Mathf.PI * 2f / blobs.Count;
                blobs[i].localPosition = new Vector3(Mathf.Cos(a) * 0.7f, 1f + Mathf.Sin(a * 1.5f) * 0.3f, Mathf.Sin(a) * 0.7f);
            }
        }

        void OnDestroy()
        {
            foreach (Transform b in blobs)
                if (b != null) Destroy(b.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
