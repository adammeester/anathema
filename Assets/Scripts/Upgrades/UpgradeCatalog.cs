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

        // Predefined offers per character: Sets[pick] lists the upgrade bits offered at that pick (0 = after round 1).
        static readonly Dictionary<string, int[][]> SetsByCharacter = new Dictionary<string, int[][]>
        {
            { "KhorneBerserker", BerserkerUpgrade.Sets },
        };

        // The offers for the pick after round `round` (1-based). Characters with fixed sets get exactly those (minus
        // anything already owned or blocked); past the last set, or for a character without sets, it falls back to a random draw.
        public static List<UpgradeDef> OffersForRound(string character, int ownedMask, int round, int randomSeed)
        {
            UpgradeDef[] all = For(character);
            if (all != null && SetsByCharacter.TryGetValue(character, out int[][] sets) && round >= 1 && round <= sets.Length)
            {
                var offers = new List<UpgradeDef>();
                foreach (int bit in sets[round - 1])
                {
                    UpgradeDef def = Array.Find(all, d => d.Bit == bit);
                    if (def != null && IsAvailable(def, all, ownedMask)) offers.Add(def);
                }
                if (offers.Count > 0) return offers;
            }
            return Roll(character, ownedMask, randomSeed);
        }

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
