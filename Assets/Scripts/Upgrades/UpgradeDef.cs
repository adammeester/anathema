using System;
using UnityEngine;

namespace Anathema.Upgrades
{
    // The families of between-round upgrade. A random offer draws one from each family the character has (when it can).
    // Used as ints in places (the upgrades panel), so only ever append.
    public enum UpgradeCategory
    {
        Armament,   // weapon transformations: replace the primary, secondary and passive
        Endurance,  // defensive upgrades
        Ferocity,   // ability enhancements
        Remedy      // support: healing and protecting allies (the Plague Surgeon)
    }

    // One upgrade a character can be offered. `Bit` is its position in the player's owned-upgrade mask (0-31), so
    // the catalog order of a character must never be reshuffled once a build is shared (both machines must match).
    public sealed class UpgradeDef
    {
        public readonly int Bit;
        public readonly string Name;
        public readonly string Description;
        public readonly UpgradeCategory Category;

        // Upgrades sharing a group are mutually exclusive: owning one removes the others from the pool
        // (e.g. a Berserker only ever wields one transformed weapon).
        public string Group { get; private set; }

        // Passive stat changes applied for as long as the upgrade is owned.
        public float BonusHealth { get; private set; }
        public float CrowdControlScale { get; private set; } = 1f;   // multiplies crowd control durations on the owner
        public float Armor { get; private set; }   // fraction of incoming damage shrugged off, summed across upgrades
        public float AreaResistance { get; private set; }   // fraction of area and damage-over-time damage shrugged off

        // Optional extra gate on top of "not owned yet": receives the owned mask.
        public Func<int, bool> Requirement { get; private set; }

        public UpgradeDef(int bit, UpgradeCategory category, string name, string description)
        {
            Bit = bit;
            Category = category;
            Name = name;
            Description = description;
        }

        public UpgradeDef InGroup(string group) { Group = group; return this; }
        public UpgradeDef Health(float bonus) { BonusHealth = bonus; return this; }
        public UpgradeDef Armour(float fraction) { Armor = fraction; return this; }
        public UpgradeDef CrowdControl(float scale) { CrowdControlScale = scale; return this; }
        public UpgradeDef AreaResist(float fraction) { AreaResistance = fraction; return this; }
        public bool GrantsLastStand { get; private set; }   // survive one lethal hit per round (see Health.LastStand)
        public UpgradeDef LastStand() { GrantsLastStand = true; return this; }
        public UpgradeDef Requires(Func<int, bool> requirement) { Requirement = requirement; return this; }

        // A choice that is applied on the spot and not kept in the mask (e.g. returning to the starting weapon).
        public bool Instant { get; private set; }
        public UpgradeDef AsInstant() { Instant = true; return this; }

        public int Mask => 1 << Bit;

        public static Color CategoryColor(UpgradeCategory category)
        {
            switch (category)
            {
                case UpgradeCategory.Armament: return new Color(0.95f, 0.62f, 0.20f);
                case UpgradeCategory.Endurance: return new Color(0.35f, 0.80f, 0.55f);
                case UpgradeCategory.Remedy: return new Color(0.45f, 0.75f, 0.98f);
                default: return new Color(0.95f, 0.28f, 0.22f);
            }
        }

        public static string CategoryLabel(UpgradeCategory category)
        {
            switch (category)
            {
                case UpgradeCategory.Armament: return "ARMAMENT";
                case UpgradeCategory.Endurance: return "ENDURANCE";
                case UpgradeCategory.Remedy: return "REMEDY";
                default: return "FEROCITY";
            }
        }
    }
}
