using Anathema.Combat;
using Anathema.Net;
using UnityEngine;

namespace Anathema.Arena
{
    // The big orb in the centre of the arena. Anyone can attack it; the team that lands the destroying blow has every
    // living member's energy (the ultimate's charge) topped up by a quarter of their bar. OrbSpawner decides when it
    // appears. It is a neutral Health (a "structure": no status effects, gas or damage over time), so every attack
    // that can hit a character can hit it. Online the server owns its health: a client's hits are passed on to the
    // server (see NetGame's chargeOrb messages), which tells everyone how much is left and who destroyed it.
    public class ChargeOrb : MonoBehaviour
    {
        public const float ChargeFraction = 0.25f; // of each team member's full energy bar
        public const float DefaultHealth = 100f;
        public const int NeutralTeam = -1;         // on nobody's side, so every team's attacks treat it as an enemy

        public const int EventSpawn = 0, EventHealth = 1, EventDestroyed = 2;

        const float OrbHeight = 1.1f;
        const float ColliderRadius = 0.6f;
        const float ClearRadius = 1.6f;
        const float FlashSeconds = 0.12f;

        public static ChargeOrb Current { get; private set; }

        static Material coreMaterial, haloMaterial;

        public Health Health { get; private set; }

        Transform core, halo;
        float phase;
        float shownHealth;
        float flashUntil;

        public static ChargeOrb Spawn(Vector3 groundPoint, float maxHealth)
        {
            Despawn();

            var go = new GameObject("ChargeOrb");
            go.transform.position = groundPoint;

            var collider = go.AddComponent<SphereCollider>();
            collider.center = Vector3.up * OrbHeight;
            collider.radius = ColliderRadius;

            if (coreMaterial == null)
            {
                coreMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.78f, 0.25f, 1f));
                haloMaterial = VisualUtil.UnlitMaterial(new Color(1f, 0.6f, 0.15f, 0.25f));
            }

            var orb = go.AddComponent<ChargeOrb>();
            orb.core = AddSphere(go.transform, "Core", coreMaterial, 0.9f);
            orb.halo = AddSphere(go.transform, "Halo", haloMaterial, 1.5f);

            Health health = go.AddComponent<Health>();
            health.Configure(maxHealth, NeutralTeam);
            health.Structure = true;
            orb.Health = health;
            orb.shownHealth = maxHealth;
            go.AddComponent<HealthBar>();

            if (NetGame.Online && !NetGame.IsHost)
            {
                // The server's copy is the real one: this one only passes hits on and shows what the server says.
                health.StrikeRelay = orb.RelayStrike;
            }
            else
            {
                health.Damaged += orb.OnDamaged;
                health.Died += orb.OnDied;
            }

            Current = orb;
            return orb;
        }

        static Transform AddSphere(Transform parent, string name, Material mat, float size)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = name;
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(parent, false);
            s.transform.localPosition = Vector3.up * OrbHeight;
            s.transform.localScale = Vector3.one * size;
            var r = s.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return s.transform;
        }

        // Removes the orb without rewarding anyone (round reset, prep).
        public static void Despawn()
        {
            if (Current != null) Destroy(Current.gameObject);
            Current = null;
        }

        // True while a living character stands where the orb would appear.
        public static bool Blocked(Vector3 groundPoint)
        {
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Structure) continue;
                Vector3 offset = h.transform.position - groundPoint;
                offset.y = 0f;
                if (offset.sqrMagnitude < ClearRadius * ClearRadius) return true;
            }
            return false;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // --- Client: hits go to the server, state comes back ---------------------------------------------------------

        void RelayStrike(Hit hit, GameObject source, System.Action<bool> onResult)
        {
            if (Health.IsDead || NetGame.Frozen || NetGame.InPrep || NetGame.Instance == null)
            {
                onResult?.Invoke(false);
                return;
            }

            NetGame.Instance.SendChargeOrbHit(hit.Damage, hit.Aoe, hit.Pierce);
            onResult?.Invoke(true);
        }

        public static void OnServerEvent(int evt, Vector3 position, float value, int team)
        {
            switch (evt)
            {
                case EventSpawn:
                    Spawn(position, value);
                    break;
                case EventHealth:
                    if (Current != null) Current.Health.SyncFromNetwork(value, 0f, 0f, 0f);
                    break;
                case EventDestroyed:
                    if (Current != null) Current.Shatter(team);
                    else Charge(team);
                    break;
            }
        }

        // --- Server / offline: the real orb ---------------------------------------------------------------------------

        void OnDamaged(Health health, float amount, GameObject source)
        {
            if (health.IsDead || !NetGame.Online) return;
            NetGame.Instance.BroadcastChargeOrb(EventHealth, transform.position, health.Current, 0);
        }

        void OnDied(Health health, GameObject killer)
        {
            int team = killer != null && killer.TryGetComponent(out Health k) ? k.Team : NeutralTeam;
            if (NetGame.Online) NetGame.Instance.BroadcastChargeOrb(EventDestroyed, transform.position, 0f, team);
            Shatter(team);
        }

        // --- Everyone -------------------------------------------------------------------------------------------------

        void Shatter(int team)
        {
            AbilityFx.CounterTriggered(transform.position, 1.6f);
            Charge(team);
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        // Energy belongs to each player's own machine, so online everyone tops up their own character.
        static void Charge(int team)
        {
            if (NetGame.Online)
            {
                if (NetPlayer.Local != null && NetPlayer.Local.Health.Team == team) Grant(NetPlayer.Local.Health);
                return;
            }

            foreach (Health h in FindObjectsByType<Health>())
                if (h.Team == team) Grant(h);
        }

        static void Grant(Health h)
        {
            if (h.TryGetComponent(out Energy energy)) energy.Gain(energy.Max * ChargeFraction); // the dead get nothing
        }

        void Update()
        {
            if (Health.Current < shownHealth) flashUntil = Time.time + FlashSeconds;
            shownHealth = Health.Current;

            // A slow pulse; a hit makes the core swell for a moment.
            phase += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(phase * 2.4f) * 0.06f;
            core.localScale = Vector3.one * (0.9f * (Time.time < flashUntil ? 1.2f : pulse));
            halo.localScale = Vector3.one * (1.5f * (2f - pulse));
            core.Rotate(0f, 60f * Time.deltaTime, 0f);
        }
    }
}
