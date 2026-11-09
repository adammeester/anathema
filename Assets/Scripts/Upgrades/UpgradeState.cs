using System;
using System.Collections.Generic;
using Anathema.Abilities.KhorneBerserker;
using Anathema.Combat;
using Anathema.Net;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Upgrades
{
    // The upgrades one character has taken this match, and the pick it is currently being offered.
    //
    // Owner machine: rolls three offers when a round ends (BeginPick), applies the choice (Choose), and publishes the
    // owned mask through NetPlayer. Replicas just follow the published mask (SetMaskFromNetwork), so the weapon a
    // fighter wields looks right on every screen. Everything that changes combat reads the mask through Has(), so the
    // owner's machine (which resolves all damage) and the replay on other machines agree.
    //
    // Added to the player at runtime by PlayerMovement, so prefabs need no rebuild. Upgrades last until the match ends.
    [DefaultExecutionOrder(-190)]
    public class UpgradeState : MonoBehaviour
    {
        string character;
        Health health;
        readonly List<UpgradeDef> offers = new List<UpgradeDef>();
        int pendingRound;

        public int Mask { get; private set; }
        public string Character => character;
        public IReadOnlyList<UpgradeDef> Offers => offers;

        // A pick is waiting for the player to choose.
        public bool Pending => offers.Count > 0;

        // Raised whenever the owned set changes.
        public event Action<UpgradeState> Changed;

        public static UpgradeState EnsureOn(GameObject player)
        {
            if (player.TryGetComponent(out UpgradeState existing)) return existing;
            if (!UpgradeCatalog.HasUpgrades(CharacterStats.Identify(player))) return null;
            return player.AddComponent<UpgradeState>();
        }

        // Does this character own upgrade `bit`? False for characters without an UpgradeState.
        public static bool Has(GameObject player, int bit)
        {
            return player != null && player.TryGetComponent(out UpgradeState state) && state.Has(bit);
        }

        public bool Has(int bit) => (Mask & (1 << bit)) != 0;

        void Awake()
        {
            health = GetComponent<Health>();
            character = CharacterStats.Identify(gameObject);
            if (character == "KhorneBerserker" && GetComponent<BerserkerUpgradeEffects>() == null)
                gameObject.AddComponent<BerserkerUpgradeEffects>();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Offline training ground only (there are no rounds there): F6 Heavy Chainaxe, F7 Eviscerator, F8 clear all.
        void Update()
        {
            if (NetGame.Online) return;
            if (Input.GetKeyDown(KeyCode.F6)) Toggle(BerserkerUpgrade.HeavyChainaxe, BerserkerUpgrade.Eviscerator);
            if (Input.GetKeyDown(KeyCode.F7)) Toggle(BerserkerUpgrade.Eviscerator, BerserkerUpgrade.HeavyChainaxe);
            if (Input.GetKeyDown(KeyCode.F8)) ResetAll();
        }

        void Toggle(int bit, int other)
        {
            if (character != "KhorneBerserker") return;
            Mask = (Mask & ~(1 << other)) ^ (1 << bit);
            Recompute();
        }
#endif

        // Owner only. A round just ended: draw three offers, or, if there is nothing to offer, report the round as settled.
        public void BeginPick(int round)
        {
            offers.Clear();
            pendingRound = round;
            offers.AddRange(UpgradeCatalog.Roll(character, Mask, UnityEngine.Random.Range(0, int.MaxValue)));
            if (offers.Count == 0) Publish(round);
        }

        // Owner only: take offer `index`. Returns false if there is nothing to take.
        public bool Choose(int index)
        {
            if (index < 0 || index >= offers.Count) return false;

            Mask |= offers[index].Mask;
            offers.Clear();
            Recompute();
            Publish(pendingRound);
            return true;
        }

        // Time ran out before the player chose: take a random offer so nobody is left behind. Weapons are a deliberate
        // choice, so an idle player is never given one (unless a weapon is the only thing on offer).
        public void AutoPickIfPending()
        {
            if (!Pending) return;

            var safe = new List<int>();
            for (int i = 0; i < offers.Count; i++)
                if (offers[i].Category != UpgradeCategory.Armament) safe.Add(i);

            Choose(safe.Count > 0 ? safe[UnityEngine.Random.Range(0, safe.Count)] : UnityEngine.Random.Range(0, offers.Count));
        }

        // New match (rematch): forget everything.
        public void ResetAll()
        {
            offers.Clear();
            Mask = 0;
            pendingRound = 0;
            Recompute();
            Publish(0);
        }

        // Replicas: follow the owner's published mask.
        public void SetMaskFromNetwork(int mask)
        {
            if (mask == Mask) return;
            Mask = mask;
            Recompute();
        }

        void Publish(int round)
        {
            if (TryGetComponent(out NetPlayer net) && net.IsSpawned && net.IsOwner) net.PublishUpgrades(Mask, round);
        }

        // Passive stat upgrades (health, armour) are data on the catalog entries; add them up.
        void Recompute()
        {
            float bonusHealth = 0f, armor = 0f, ccScale = 1f;
            UpgradeDef[] all = UpgradeCatalog.For(character);
            if (all != null)
            {
                foreach (UpgradeDef def in all)
                {
                    if ((Mask & def.Mask) == 0) continue;
                    bonusHealth += def.BonusHealth;
                    armor += def.Armor;
                    ccScale *= def.CrowdControlScale;
                }
            }

            if (health != null)
            {
                health.SetBonusMaxHealth(bonusHealth);
                health.Armor = armor;
                health.CrowdControlScale = ccScale;
            }
            Changed?.Invoke(this);
        }
    }
}
