using System;
using UnityEngine;

namespace Anathema.Combat
{
    // Character voice lines (recordings in Resources/Audio/Sfx, played through Sfx.Say). Two moments so far:
    //   "<Character>UltimateReady" - spoken on the player's own machine when their energy first reaches the ultimate's cost;
    //   the ultimate's own line    - shouted on every machine as it is cast.
    // A character whose line has not been recorded yet is simply silent: drop the file in and it plays.
    public static class Voice
    {
        public const string GreyKnightUltimateReady = "GreyKnightUltimateReady";
        public const string GreyKnightUltimate = "GreyKnightUltimate";
        public const string BerserkerUltimateReady = "BerserkerUltimateReady";
        public const string BerserkerUltimate = "BloodForTheBloodGod";
        public const string PlagueSurgeonUltimateReady = "PlagueSurgeonUltimateReady";
        public const string PlagueSurgeonUltimate = "PlagueSurgeonUltimate";
        public const string PsykerUltimateReady = "PsykerUltimateReady";
        public const string PsykerUltimate = "PsykerUltimate";
        public const string SkitariiUltimateReady = "SkitariiUltimateReady";
        public const string SkitariiUltimate = "SkitariiUltimate";

        public static AudioSource Say(string line, float volume = 0.7f) => Sfx.Say(line, volume);

        // Speaks `line` each time `energy` climbs from below the ultimate's cost to at or above it. Only the player who
        // owns the character hears it (it is a prompt for them, not something the enemy should be told).
        // `cost` is asked every time because upgrades can change it. Call once, from the ultimate's Awake.
        public static void AnnounceUltimateReady(Energy energy, Health health, string line, Func<float> cost)
        {
            if (energy == null || health == null) return;

            bool wasReady = energy.Current >= cost();
            energy.Changed += e =>
            {
                bool ready = e.Current >= cost();
                if (ready && !wasReady && health.IsMine && !health.IsDead) Say(line, 0.6f);
                wasReady = ready;
            };
        }
    }
}
