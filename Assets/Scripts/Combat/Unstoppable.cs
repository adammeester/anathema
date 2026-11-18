using UnityEngine;

namespace Anathema.Combat
{
    // Implemented by a component that makes its character immune to crowd control for a while (e.g. the Bullgryn's
    // Bull Charge). Checked wherever stuns, roots, slows, silences, panics, maims and knockbacks are applied, on the
    // server and on the owner, so both copies agree.
    public interface IUnstoppable
    {
        bool Unstoppable { get; }
    }

    public static class Unstoppable
    {
        public static bool Is(GameObject target)
        {
            if (target == null) return false;
            foreach (IUnstoppable u in target.GetComponents<IUnstoppable>())
                if (u.Unstoppable) return true;
            return false;
        }
    }
}
