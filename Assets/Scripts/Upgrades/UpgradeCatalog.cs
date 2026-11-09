using System;
using System.Collections.Generic;
using Anathema.Abilities.KhorneBerserker;

namespace Anathema.Upgrades
{
    // Every upgrade in the game, per character, plus the draw that picks a round's three offers.
    // Characters are keyed the same way as Resources/Stats/<Character>.json ("KhorneBerserker", ...).
    // A character with no entry simply gets no upgrade pick between rounds.
    public static class UpgradeCatalog
    {
        public const int OffersPerRound = 3;

        static readonly Dictionary<string, UpgradeDef[]> ByCharacter = new Dictionary<string, UpgradeDef[]>
        {
            { "KhorneBerserker", BerserkerUpgrade.All },
        };

        public static UpgradeDef[] For(string character)
        {
            return character != null && ByCharacter.TryGetValue(character, out UpgradeDef[] list) ? list : null;
        }

        public static bool HasUpgrades(string character) => For(character) != null;

        // True if `def` can still be offered given what is already owned.
        public static bool IsAvailable(UpgradeDef def, UpgradeDef[] all, int ownedMask)
        {
            if ((ownedMask & def.Mask) != 0) return false;
            if (def.Requirement != null && !def.Requirement(ownedMask)) return false;
            if (def.Group != null)
                foreach (UpgradeDef other in all)
                    if (other != def && other.Group == def.Group && (ownedMask & other.Mask) != 0) return false;
            return true;
        }

        // One upgrade from each category (in category order) where possible; a category with nothing left is filled
        // from whatever else remains, so the player still sees three choices. The same seed gives the same offers.
        public static List<UpgradeDef> Roll(string character, int ownedMask, int seed)
        {
            var offers = new List<UpgradeDef>();
            UpgradeDef[] all = For(character);
            if (all == null) return offers;

            var rng = new Random(seed);
            var pool = new List<UpgradeDef>();
            foreach (UpgradeDef def in all)
                if (IsAvailable(def, all, ownedMask)) pool.Add(def);

            foreach (UpgradeCategory category in Enum.GetValues(typeof(UpgradeCategory)))
            {
                var candidates = pool.FindAll(d => d.Category == category);
                if (candidates.Count == 0) continue;
                UpgradeDef pick = candidates[rng.Next(candidates.Count)];
                offers.Add(pick);
                pool.Remove(pick);
            }

            // Filler never offers two choices from one exclusive group (two weapons at once).
            while (offers.Count < OffersPerRound && pool.Count > 0)
            {
                UpgradeDef pick = pool[rng.Next(pool.Count)];
                pool.Remove(pick);
                if (pick.Group != null && offers.Exists(o => o.Group == pick.Group)) continue;
                offers.Add(pick);
            }

            return offers;
        }
    }
}
