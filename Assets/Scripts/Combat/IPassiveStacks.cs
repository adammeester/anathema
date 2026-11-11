using UnityEngine;

namespace Anathema.Combat
{
    // A passive that builds up stacks the player should see (e.g. the Hospitaller's Faith). PlayerHud draws one pip per
    // stack just above the action bar for the local player's character when a component implements this.
    public interface IPassiveStacks
    {
        string PassiveName { get; }
        int Stacks { get; }
        int MaxStacks { get; }
        Color StackColor { get; }

        // 0..1 progress towards the next stack (drawn as a thin bar under the pips); 0 when at max.
        float NextStackProgress { get; }
    }
}
