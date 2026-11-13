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
        bool trusted;
        Material material;
        float stackDuration;     // of the latest stack laid, for Contagion's spread
        float nextSpread;        // Contagion: when a target held at maximum next passes a stack on
        float nextLeechRefresh;  // Leech Draught: when the mark on the host is next topped up
        bool visualOnly;         // drawn from another machine's published count: shows the rot, deals nothing
        Anathema.Net.NetPlayer net;
        int publishedStacks;     // the stack count last sent to the other machines
        bool publishDirty;       // stacks were laid or refreshed since the last send

        // Without Virulence (see PlagueSurgeonUpgradeEffects.MaxFesteringStacks).
        public const int MaxStacks = 3;

        public int Stacks => expiries.Count;

        // A copy drawn from another machine's rot (not this machine's to tick, spread or burst).
        public bool IsReplica => visualOnly;

        int Cap => PlagueSurgeonUpgradeEffects.MaxFesteringStacks(source);

        // `stacks` > 1 lays several stacks at once (the ward's death-cloud uses this for a heavier rot).
        public static Festering Apply(GameObject target, GameObject damageSource, Energy sourceEnergy, float duration, float damagePerSecond, int stacks = 1)
        {
            var festering = target.GetComponent<Festering>();
            if (festering == null)
            {
                festering = target.AddComponent<Festering>();
                festering.nextTick = Time.time + TickInterval;
            }
            else if (festering.visualOnly)
            {
                // This machine now lays its own rot: the drawn copy becomes the real thing, starting from scratch.
                festering.visualOnly = false;
                festering.expiries.Clear();
                festering.nextTick = Time.time + TickInterval;
            }
            festering.publishDirty = true;

            festering.source = damageSource;
            festering.sourceEnergy = sourceEnergy;
            // Laid by the server itself (a ward's rupture), crediting another player's character: its ticks are the
            // server's own damage, not a replayed visual.
            festering.trusted = target.TryGetComponent(out Health h) && h.IsAuthority
                                && Anathema.Net.NetPlayer.IsReplicaObject(damageSource);
            festering.damagePerStackPerSecond = damagePerSecond;
            festering.stackDuration = duration;
            int cap = festering.Cap;
            for (int i = 0; i < stacks; i++)
            {
                // At the cap, a new hit resets every stack's duration instead of adding another.
                if (festering.expiries.Count >= cap)
                {
                    for (int j = 0; j < festering.expiries.Count; j++)
                        festering.expiries[j] = Time.time + duration;
                }
                else festering.expiries.Add(Time.time + duration);
            }
            return festering;
        }

        // Bursting Pustules: strips every stack at once and says how many there were. The component goes with its next
        // update unless a new stack is laid first.
        public int Consume()
        {
            if (visualOnly) return 0;
            int count = expiries.Count;
            expiries.Clear();
            return count;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            net = GetComponent<Anathema.Net.NetPlayer>();
            material = VisualUtil.UnlitMaterial(new Color(0.45f, 0.7f, 0.1f, 0.9f));
        }

        void Update()
        {
            expiries.RemoveAll(t => Time.time >= t);
            if (visualOnly)
            {
                if (health == null || health.IsDead || expiries.Count == 0) Destroy(this);
                else SyncBlobs();
                return;
            }

            if (health != null && health.IsDead && expiries.Count > 0 && HasContagion)
            {
                // Contagion: the corpse's rot finds new hosts.
                SpreadTo(expiries.Count, PlagueSurgeonUpgrade.ContagionRadius, false);
                expiries.Clear();
            }
            if (health == null || health.IsDead || expiries.Count == 0)
            {
                // Rot that ran out lets the other machines know; on a death their copies clear themselves.
                if (health != null && !health.IsDead) Publish();
                Destroy(this);
                return;
            }

            SyncBlobs();
            Publish();

            if (Anathema.Net.NetGame.Frozen) return;

            // Leech Draught: keep the mark up on the copy where damage is resolved while the rot lasts.
            if (Time.time >= nextLeechRefresh && PlagueSurgeonUpgradeEffects.Has(source, PlagueSurgeonUpgrade.LeechDraught))
            {
                nextLeechRefresh = Time.time + LeechMark.RefreshInterval;
                LeechMark.Refresh(gameObject, source, PlagueSurgeonUpgrade.LeechMarkSeconds);
            }

            // Contagion: a target held at maximum passes a stack on every few seconds.
            if (!HasContagion || expiries.Count < Cap) nextSpread = Time.time + PlagueSurgeonUpgrade.ContagionInterval;
            else if (Time.time >= nextSpread)
            {
                nextSpread = Time.time + PlagueSurgeonUpgrade.ContagionInterval;
                SpreadTo(1, PlagueSurgeonUpgrade.ContagionRadius, true);
            }

            bool harvest = PlagueSurgeonUpgradeEffects.Has(source, PlagueSurgeonUpgrade.RotHarvest);
            while (Time.time >= nextTick)
            {
                nextTick += TickInterval;
                if (expiries.Count == 0) break;

                float amount = expiries.Count * damagePerStackPerSecond * TickInterval;
                Energy energy = sourceEnergy;
                float gain = harvest ? PlagueSurgeonUpgrade.RotHarvestEnergyPerStack * expiries.Count : 0.5f;
                health.Strike(Hit.Direct(amount).AsAoe().AsDot(), source, landed =>
                {
                    if (landed && energy != null) energy.Gain(gain);
                }, trusted);
                if (health.IsDead) return;
            }
        }

        // Online: tell every other machine how many stacks the victim carries, and for how long at most (so a copy whose
        // last update never arrives still clears itself).
        void Publish()
        {
            if (!publishDirty && expiries.Count == publishedStacks) return;
            publishDirty = false;
            publishedStacks = expiries.Count;
            if (net == null || !net.IsSpawned) return;
            float latest = 0f;
            foreach (float t in expiries) latest = Mathf.Max(latest, t);
            net.SendRotStacks(expiries.Count, Mathf.Max(0f, latest - Time.time));
        }

        // Online: draws (or updates, or with 0 stacks clears) the rot another machine reports on `target`. A target this
        // machine holds real rot on keeps its own.
        public static void ShowReplica(GameObject target, int stacks, float seconds)
        {
            if (target == null) return;
            target.TryGetComponent(out Festering festering);
            if (festering != null && !festering.visualOnly) return;
            stacks = Mathf.Clamp(stacks, 0, 10);
            seconds = Mathf.Clamp(seconds, 0f, 15f);
            if (stacks == 0 || seconds <= 0f)
            {
                if (festering != null) Destroy(festering);
                return;
            }
            if (target.TryGetComponent(out Health h) && h.IsDead) return;
            if (festering == null)
            {
                festering = target.AddComponent<Festering>();
                festering.visualOnly = true;
            }
            festering.expiries.Clear();
            for (int i = 0; i < stacks; i++) festering.expiries.Add(Time.time + seconds);
        }

        bool HasContagion => PlagueSurgeonUpgradeEffects.Has(source, PlagueSurgeonUpgrade.Contagion);

        // Lays `stacks` of this rot on the host's living teammates within `radius`: every one of them, or only the
        // nearest when `nearestOnly`.
        void SpreadTo(int stacks, float radius, bool nearestOnly)
        {
            if (source == null || stacks <= 0) return;

            Health nearest = null;
            float nearestDistance = radius;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h == health || h.IsDead || h.Team != health.Team) continue;

                Vector3 offset = h.transform.position - transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > radius) continue;

                if (!nearestOnly) Festering.Apply(h.gameObject, source, sourceEnergy, stackDuration, damagePerStackPerSecond, stacks);
                else if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = h;
                }
            }

            if (nearest != null) Festering.Apply(nearest.gameObject, source, sourceEnergy, stackDuration, damagePerStackPerSecond, stacks);
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
