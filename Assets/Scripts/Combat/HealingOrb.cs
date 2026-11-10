using System.Collections.Generic;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Combat
{
    public enum OrbKind
    {
        Health,
        Energy
    }

    // A glowing orb on the arena floor. A health orb (green) restores lost health, healing permanently lost health first
    // (the empty part of the bar) and then the healable black part; an energy orb (blue) restores energy. Orbs are
    // only taken by someone who actually needs them. Online the host owns the orb list: a player asks to claim an orb and the host answers who
    // got it (see NetGame).
    public class HealingOrb : MonoBehaviour
    {
        public const float DefaultAmount = 10f;
        public const float DefaultEnergyAmount = 15f;

        const float PickupRadius = 0.9f;
        const float ClaimRetrySeconds = 1f;
        const float MinPermanentLoss = 0.5f;

        public static readonly List<HealingOrb> All = new List<HealingOrb>();

        static readonly Material[] coreMaterials = new Material[2];
        static readonly Material[] haloMaterials = new Material[2];
        static readonly Color[] kindColors = { new Color(0.35f, 1f, 0.55f), new Color(0.3f, 0.7f, 1f) };

        public int Id { get; private set; }
        public OrbKind Kind { get; private set; }
        public Vector3 BasePosition => basePosition;
        float amount = DefaultAmount;
        Vector3 basePosition;
        float phase;
        float nextClaimTime;
        Health candidate;

        public static HealingOrb Spawn(int id, Vector3 groundPoint, OrbKind kind, float orbAmount)
        {
            if (Find(id) != null) return null;

            var go = new GameObject((kind == OrbKind.Energy ? "EnergyOrb_" : "HealingOrb_") + id);
            go.transform.position = groundPoint;

            int k = (int)kind;
            if (coreMaterials[k] == null)
            {
                Color c = kindColors[k];
                coreMaterials[k] = VisualUtil.UnlitMaterial(new Color(c.r, c.g, c.b, 1f));
                haloMaterials[k] = VisualUtil.UnlitMaterial(new Color(c.r, c.g, c.b, 0.28f));
            }

            AddSphere(go.transform, "Core", coreMaterials[k], 0.28f, Vector3.up * 0.6f);
            AddSphere(go.transform, "Halo", haloMaterials[k], 0.55f, Vector3.up * 0.6f);

            var orb = go.AddComponent<HealingOrb>();
            orb.Id = id;
            orb.Kind = kind;
            orb.amount = orbAmount;
            orb.basePosition = groundPoint;
            orb.phase = Random.value * 6.28f;
            All.Add(orb);
            return orb;
        }

        static void AddSphere(Transform parent, string name, Material mat, float size, Vector3 localPosition)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = name;
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(parent, false);
            s.transform.localPosition = localPosition;
            s.transform.localScale = Vector3.one * size;
            var r = s.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public static HealingOrb Find(int id)
        {
            foreach (HealingOrb orb in All)
                if (orb != null && orb.Id == id) return orb;
            return null;
        }

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
                if (All[i] != null) Destroy(All[i].gameObject);
            All.Clear();
        }

        // Removes the orb; whoever claimed it (if this machine's player) gets the healing.
        public static void Collect(int id, bool healLocalPlayer)
        {
            HealingOrb orb = Find(id);
            if (orb == null) return;

            if (healLocalPlayer)
            {
                Health h = orb.LocalPlayerHealth();
                if (h != null)
                {
                    if (orb.Kind == OrbKind.Health) h.HealPermanent(orb.amount);
                    else if (h.TryGetComponent(out Energy energy)) energy.Gain(orb.amount);
                }
            }

            orb.Remove();
        }

        void Remove()
        {
            All.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy() => All.Remove(this);

        Health LocalPlayerHealth()
        {
            if (NetGame.Online) return NetPlayer.Local != null ? NetPlayer.Local.Health : null;
            if (candidate == null)
            {
                var aim = FindAnyObjectByType<Anathema.Player.MouseAim>();
                if (aim != null) candidate = aim.GetComponent<Health>();
            }
            return candidate;
        }

        bool NeededBy(Health h)
        {
            if (Kind == OrbKind.Health) return h.Max - h.Current >= MinPermanentLoss;
            return h.TryGetComponent(out Energy energy) && energy.Max - energy.Current >= MinPermanentLoss;
        }

        void Update()
        {
            // Bob and spin.
            phase += Time.deltaTime;
            transform.position = basePosition + Vector3.up * (Mathf.Sin(phase * 2.2f) * 0.1f);
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);

            if (NetGame.Frozen || NetGame.InPrep || Time.time < nextClaimTime) return;

            Health h = LocalPlayerHealth();
            if (h == null || h.IsDead) return;
            if (!NeededBy(h)) return;

            Vector3 offset = h.transform.position - basePosition;
            offset.y = 0f;
            if (offset.sqrMagnitude > PickupRadius * PickupRadius) return;

            if (NetGame.Online)
            {
                // Wait for the host's verdict; retry later if the orb is somehow still here.
                nextClaimTime = Time.time + ClaimRetrySeconds;
                NetGame.Instance.RequestOrbClaim(Id);
            }
            else
            {
                Collect(Id, true);
            }
        }
    }
}
